using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class MaintenanceCurrencyTests
{
    [Fact]
    public void PassingMaintenanceCheckKeepsFoundationalL1L2CurrentForWeeklyCadence()
    {
        var request = Request(
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 7),
            [Pass(BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1))]);

        var result = MaintenanceCurrencyEvaluator.Evaluate(request);

        Assert.Equal(MaintenanceCurrencyState.Current, result.State);
        Assert.Equal(7, result.Cadence.DueAfterDays);
        Assert.Equal(7, result.Cadence.OverdueAfterDays);
        Assert.Equal(6, result.DaysSinceLastPassingCheck);
        Assert.Equal(0, result.ConsecutiveFailures);
    }

    [Fact]
    public void FoundationalL1L2MaintenanceBecomesDueAfterWeeklyCadenceIsMissed()
    {
        var request = Request(
            BranchCode.WM,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 9),
            [Pass(BranchCode.WM, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1))]);

        var result = MaintenanceCurrencyEvaluator.Evaluate(request);

        Assert.Equal(MaintenanceCurrencyState.Due, result.State);
        Assert.Equal(8, result.DaysSinceLastPassingCheck);
    }

    [Fact]
    public void FirstFailedMaintenanceCheckCreatesWarning()
    {
        var request = Request(
            BranchCode.DE,
            GlobalLevelId.L3,
            TrainingDate.From(2026, 7, 8),
            [
                Pass(BranchCode.DE, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1)),
                Fail(BranchCode.DE, GlobalLevelId.L3, TrainingDate.From(2026, 7, 8)),
            ]);

        var result = MaintenanceCurrencyEvaluator.Evaluate(request);

        Assert.Equal(MaintenanceCurrencyState.Warning, result.State);
        Assert.Equal(1, result.ConsecutiveFailures);
    }

    [Fact]
    public void RepeatedMaintenanceFailuresMarkBranchFailed()
    {
        var request = Request(
            BranchCode.IR,
            GlobalLevelId.L3,
            TrainingDate.From(2026, 7, 9),
            [
                Pass(BranchCode.IR, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1)),
                Fail(BranchCode.IR, GlobalLevelId.L3, TrainingDate.From(2026, 7, 8)),
                Fail(BranchCode.IR, GlobalLevelId.L3, TrainingDate.From(2026, 7, 9)),
            ]);

        var result = MaintenanceCurrencyEvaluator.Evaluate(request);

        Assert.Equal(MaintenanceCurrencyState.Failed, result.State);
        Assert.Equal(2, result.ConsecutiveFailures);
    }

    [Fact]
    public void FoundationalL3UsesSevenToTenDayMaintenanceCadence()
    {
        var request = Request(
            BranchCode.FS,
            GlobalLevelId.L3,
            TrainingDate.From(2026, 7, 11),
            [Pass(BranchCode.FS, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1))]);

        var result = MaintenanceCurrencyEvaluator.Evaluate(request);

        Assert.Equal(MaintenanceCurrencyState.Current, result.State);
        Assert.Equal(7, result.Cadence.DueAfterDays);
        Assert.Equal(10, result.Cadence.OverdueAfterDays);
        Assert.Equal(10, result.DaysSinceLastPassingCheck);
    }

    [Fact]
    public void AdvancedBranchesUseTenToFourteenDayMaintenanceCadence()
    {
        var current = Request(
            BranchCode.CO,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 15),
            [Pass(BranchCode.CO, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1))]);
        var due = Request(
            BranchCode.CO,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 16),
            [Pass(BranchCode.CO, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1))]);

        var currentResult = MaintenanceCurrencyEvaluator.Evaluate(current);
        var dueResult = MaintenanceCurrencyEvaluator.Evaluate(due);

        Assert.Equal(MaintenanceCurrencyState.Current, currentResult.State);
        Assert.Equal(10, currentResult.Cadence.DueAfterDays);
        Assert.Equal(14, currentResult.Cadence.OverdueAfterDays);
        Assert.Equal(MaintenanceCurrencyState.Due, dueResult.State);
    }

    [Fact]
    public void TransferIntegrationL3OrHigherUsesFourWeekGlobalCompositeCadence()
    {
        var request = Request(
            BranchCode.TI,
            GlobalLevelId.L3,
            TrainingDate.From(2026, 7, 29),
            [Pass(BranchCode.TI, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1), MaintenanceCheckKind.GlobalComposite)]);

        var result = MaintenanceCurrencyEvaluator.Evaluate(request);

        Assert.Equal(MaintenanceCurrencyState.Current, result.State);
        Assert.Equal(MaintenanceCheckKind.GlobalComposite, result.Cadence.RequiredCheckKind);
        Assert.Equal(28, result.Cadence.DueAfterDays);
        Assert.Equal(28, result.Cadence.OverdueAfterDays);
    }

    [Fact]
    public void WrongMaintenanceWorkKindDoesNotSatisfyTransferIntegrationGlobalCadence()
    {
        var request = Request(
            BranchCode.TI,
            GlobalLevelId.L3,
            TrainingDate.From(2026, 7, 10),
            [Pass(BranchCode.TI, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1), MaintenanceCheckKind.StandardOrTransfer)]);

        var result = MaintenanceCurrencyEvaluator.Evaluate(request);

        Assert.Equal(MaintenanceCurrencyState.Due, result.State);
        Assert.Null(result.DaysSinceLastPassingCheck);
    }

    private static MaintenanceCurrencyRequest Request(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate asOf,
        IEnumerable<MaintenanceCheckEvidence> checks)
    {
        return new MaintenanceCurrencyRequest(branch, ownedLevel, asOf, checks);
    }

    private static MaintenanceCheckEvidence Pass(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate date,
        MaintenanceCheckKind kind = MaintenanceCheckKind.StandardOrTransfer)
    {
        return new MaintenanceCheckEvidence(
            branch,
            ownedLevel,
            date,
            kind,
            standardEvaluationResult: new StandardEvaluationResult(true, []));
    }

    private static MaintenanceCheckEvidence Fail(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate date,
        MaintenanceCheckKind kind = MaintenanceCheckKind.StandardOrTransfer)
    {
        return new MaintenanceCheckEvidence(
            branch,
            ownedLevel,
            date,
            kind,
            standardEvaluationResult: new StandardEvaluationResult(
                false,
                [new StandardEvaluationFailure(StandardFailureKind.CriticalConstraintBroken, "constraint failed")]));
    }
}
