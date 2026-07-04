using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class DecayRestorationTests
{
    [Fact]
    public void TwoFailedMaintenanceChecksCanMarkBranchDecayed()
    {
        var currentStatus = new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance);
        var currency = MaintenanceCurrencyEvaluator.Evaluate(
            new MaintenanceCurrencyRequest(
                BranchCode.FH,
                GlobalLevelId.L2,
                TrainingDate.From(2026, 7, 9),
                [
                    MaintenancePass(BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1)),
                    MaintenanceFail(BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 8)),
                    MaintenanceFail(BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 9)),
                ]));

        var result = DecayRestorationEvaluator.EvaluateDecay(currentStatus, currency);

        Assert.True(result.ChangedState);
        Assert.Equal(BranchLevelTransition.MarkDecayed, result.Transition);
        Assert.Equal(new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Decayed), result.NextStatus);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void FirstMaintenanceFailureWarningDoesNotCreateDecay()
    {
        var currentStatus = new BranchLevelStatus(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance);
        var currency = MaintenanceCurrencyEvaluator.Evaluate(
            new MaintenanceCurrencyRequest(
                BranchCode.DE,
                GlobalLevelId.L3,
                TrainingDate.From(2026, 7, 8),
                [
                    MaintenancePass(BranchCode.DE, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1)),
                    MaintenanceFail(BranchCode.DE, GlobalLevelId.L3, TrainingDate.From(2026, 7, 8)),
                ]));

        var result = DecayRestorationEvaluator.EvaluateDecay(currentStatus, currency);

        Assert.False(result.ChangedState);
        Assert.Equal(BranchLevelState.Maintenance, result.NextStatus.State);
        Assert.Contains(result.Failures, failure => failure.Kind == DecayRestorationFailureKind.MaintenanceFailureThresholdNotMet);
    }

    [Fact]
    public void OrdinaryTrainingFailureDoesNotCreateDecay()
    {
        var currentStatus = new BranchLevelStatus(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Training);
        var currency = MaintenanceCurrencyEvaluator.Evaluate(
            new MaintenanceCurrencyRequest(
                BranchCode.IR,
                GlobalLevelId.L2,
                TrainingDate.From(2026, 7, 9),
                [
                    MaintenancePass(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1)),
                    MaintenanceFail(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 8)),
                    MaintenanceFail(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 9)),
                ]));

        var result = DecayRestorationEvaluator.EvaluateDecay(currentStatus, currency);
        var ordinaryTrainingFailure = BranchLevelStateMachine.TryApply(
            BranchLevelState.TestReady,
            BranchLevelTransition.FailFormalTest);

        Assert.False(result.ChangedState);
        Assert.Equal(BranchLevelState.Training, result.NextStatus.State);
        Assert.Contains(result.Failures, failure => failure.Kind == DecayRestorationFailureKind.DecayRequiresMaintenanceState);
        Assert.Equal(BranchLevelState.Training, ordinaryTrainingFailure.NextState);
        Assert.NotEqual(BranchLevelState.Decayed, ordinaryTrainingFailure.NextState);
    }

    [Fact]
    public void FailedMaintenanceCurrencyCannotDecayAnotherBranch()
    {
        var currentStatus = new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance);
        var currency = MaintenanceCurrencyEvaluator.Evaluate(
            new MaintenanceCurrencyRequest(
                BranchCode.IR,
                GlobalLevelId.L2,
                TrainingDate.From(2026, 7, 9),
                [
                    MaintenancePass(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1)),
                    MaintenanceFail(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 8)),
                    MaintenanceFail(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 9)),
                ]));

        var result = DecayRestorationEvaluator.EvaluateDecay(currentStatus, currency);

        Assert.False(result.ChangedState);
        Assert.Equal(BranchLevelState.Maintenance, result.NextStatus.State);
        Assert.Contains(result.Failures, failure => failure.Kind == DecayRestorationFailureKind.MaintenanceCurrencyDoesNotMatchBranchLevel);
    }

    [Fact]
    public void DecayedBranchRestoresToMaintenanceWithDocumentedEvidence()
    {
        var currentStatus = new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Decayed);
        var evidence = new RestorationEvidence(
            BranchCode.FH,
            GlobalLevelId.L2,
            [
                RestorationPass(RestorationCheckKind.LastOwnedStandard, TrainingDate.From(2026, 7, 10)),
                RestorationPass(RestorationCheckKind.LowerLoadTransferCheck, TrainingDate.From(2026, 7, 11)),
            ]);

        var result = DecayRestorationEvaluator.EvaluateRestoration(currentStatus, evidence);

        Assert.True(result.ChangedState);
        Assert.Equal(BranchLevelTransition.RestoreToMaintenance, result.Transition);
        Assert.Equal(new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance), result.NextStatus);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void RestorationRequiresLastOwnedStandardPass()
    {
        var currentStatus = new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Decayed);
        var evidence = new RestorationEvidence(
            BranchCode.FH,
            GlobalLevelId.L2,
            [RestorationPass(RestorationCheckKind.LowerLoadTransferCheck, TrainingDate.From(2026, 7, 11))]);

        var result = DecayRestorationEvaluator.EvaluateRestoration(currentStatus, evidence);

        Assert.False(result.ChangedState);
        Assert.Equal(BranchLevelState.Decayed, result.NextStatus.State);
        Assert.Contains(result.Failures, failure => failure.Kind == DecayRestorationFailureKind.LastOwnedStandardPassMissing);
    }

    [Fact]
    public void RestorationRequiresLowerLoadTransferCheck()
    {
        var currentStatus = new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Decayed);
        var evidence = new RestorationEvidence(
            BranchCode.FH,
            GlobalLevelId.L2,
            [RestorationPass(RestorationCheckKind.LastOwnedStandard, TrainingDate.From(2026, 7, 10))]);

        var result = DecayRestorationEvaluator.EvaluateRestoration(currentStatus, evidence);

        Assert.False(result.ChangedState);
        Assert.Equal(BranchLevelState.Decayed, result.NextStatus.State);
        Assert.Contains(result.Failures, failure => failure.Kind == DecayRestorationFailureKind.LowerLoadTransferCheckMissing);
    }

    [Fact]
    public void FailedRestorationEvidenceDoesNotRestore()
    {
        var currentStatus = new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Decayed);
        var evidence = new RestorationEvidence(
            BranchCode.FH,
            GlobalLevelId.L2,
            [
                RestorationPass(RestorationCheckKind.LastOwnedStandard, TrainingDate.From(2026, 7, 10)),
                RestorationFail(RestorationCheckKind.LowerLoadTransferCheck, TrainingDate.From(2026, 7, 11)),
            ]);

        var result = DecayRestorationEvaluator.EvaluateRestoration(currentStatus, evidence);

        Assert.False(result.ChangedState);
        Assert.Equal(BranchLevelState.Decayed, result.NextStatus.State);
        Assert.Contains(result.Failures, failure => failure.Kind == DecayRestorationFailureKind.LowerLoadTransferCheckMissing);
    }

    private static MaintenanceCheckEvidence MaintenancePass(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate date)
    {
        return new MaintenanceCheckEvidence(
            branch,
            ownedLevel,
            date,
            MaintenanceCheckKind.StandardOrTransfer,
            new StandardEvaluationResult(true, []));
    }

    private static MaintenanceCheckEvidence MaintenanceFail(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate date)
    {
        return new MaintenanceCheckEvidence(
            branch,
            ownedLevel,
            date,
            MaintenanceCheckKind.StandardOrTransfer,
            new StandardEvaluationResult(
                false,
                [new StandardEvaluationFailure(StandardFailureKind.CriticalConstraintBroken, "constraint failed")]));
    }

    private static RestorationCheckEvidence RestorationPass(RestorationCheckKind kind, TrainingDate date)
    {
        return RestorationEvidence(kind, date, passed: true);
    }

    private static RestorationCheckEvidence RestorationFail(RestorationCheckKind kind, TrainingDate date)
    {
        return RestorationEvidence(kind, date, passed: false);
    }

    private static RestorationCheckEvidence RestorationEvidence(
        RestorationCheckKind kind,
        TrainingDate date,
        bool passed)
    {
        return new RestorationCheckEvidence(
            BranchCode.FH,
            GlobalLevelId.L2,
            date,
            kind,
            new StandardEvaluationResult(
                passed,
                passed
                    ? []
                    : [new StandardEvaluationFailure(StandardFailureKind.NumericalThresholdMissed, "score")]));
    }
}
