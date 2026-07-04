using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class StabilizationOwnershipTests
{
    private const string FocusHoldL1Standard =
        "No more than 5 marked drifts; each return within 10 seconds; no target change.";

    [Fact]
    public void ThreeCleanPassesAcrossSevenDaysWithStabilizationDemandProduceOwnership()
    {
        var evidence = new StabilizationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [
                Pass(FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1)),
                Pass(FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 4)),
                Pass(
                    FormalTestPassState.StabilizationPass,
                    TrainingDate.From(2026, 7, 8),
                    afterAdjacentWorkOrControlledDistractor: true),
            ]);

        var result = StabilizationOwnershipEvaluator.Evaluate(evidence);

        Assert.True(result.IsOwned);
        Assert.Equal(GateOutcome.Own, result.GateOutcome);
        Assert.Equal(BranchLevelState.Owned, result.BranchLevelState);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void SinglePeakPerformanceRemainsPassedOnceAndDoesNotCreateOwnership()
    {
        var evidence = new StabilizationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [Pass(FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1))]);

        var result = StabilizationOwnershipEvaluator.Evaluate(evidence);

        Assert.False(result.IsOwned);
        Assert.Equal(GateOutcome.PassOnce, result.GateOutcome);
        Assert.Equal(BranchLevelState.PassedOnce, result.BranchLevelState);
        Assert.Contains(result.Failures, failure => failure.Kind == StabilizationOwnershipFailureKind.InsufficientCleanPasses);
    }

    [Fact]
    public void TwoCleanPassesRemainStabilizingAndDoNotCreateOwnership()
    {
        var evidence = new StabilizationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [
                Pass(FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1)),
                Pass(FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 4)),
            ]);

        var result = StabilizationOwnershipEvaluator.Evaluate(evidence);

        Assert.False(result.IsOwned);
        Assert.Equal(GateOutcome.Stabilize, result.GateOutcome);
        Assert.Equal(BranchLevelState.Stabilizing, result.BranchLevelState);
        Assert.Contains(result.Failures, failure => failure.Kind == StabilizationOwnershipFailureKind.InsufficientCleanPasses);
    }

    [Fact]
    public void OwnershipRequiresPassesAcrossAtLeastSevenCalendarDays()
    {
        var evidence = new StabilizationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [
                Pass(FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1)),
                Pass(FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 3)),
                Pass(
                    FormalTestPassState.StabilizationPass,
                    TrainingDate.From(2026, 7, 6),
                    afterAdjacentWorkOrControlledDistractor: true),
            ]);

        var result = StabilizationOwnershipEvaluator.Evaluate(evidence);

        Assert.False(result.IsOwned);
        Assert.Equal(BranchLevelState.Stabilizing, result.BranchLevelState);
        Assert.Contains(result.Failures, failure => failure.Kind == StabilizationOwnershipFailureKind.SevenDaySpanMissing);
    }

    [Fact]
    public void OwnershipRequiresTwoAdditionalStabilizationPassesOnDifferentDaysWithinFourteenDays()
    {
        var evidence = new StabilizationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [
                Pass(FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1)),
                Pass(FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 4)),
                Pass(
                    FormalTestPassState.StabilizationPass,
                    TrainingDate.From(2026, 7, 20),
                    afterAdjacentWorkOrControlledDistractor: true),
            ]);

        var result = StabilizationOwnershipEvaluator.Evaluate(evidence);

        Assert.False(result.IsOwned);
        Assert.Contains(result.Failures, failure => failure.Kind == StabilizationOwnershipFailureKind.StabilizationWindowMissed);
    }

    [Fact]
    public void OwnershipRequiresAdjacentWorkOrControlledDistractorPass()
    {
        var evidence = new StabilizationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [
                Pass(FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1)),
                Pass(FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 4)),
                Pass(FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 8)),
            ]);

        var result = StabilizationOwnershipEvaluator.Evaluate(evidence);

        Assert.False(result.IsOwned);
        Assert.Contains(result.Failures, failure => failure.Kind == StabilizationOwnershipFailureKind.AdjacentWorkOrDistractorPassMissing);
    }

    [Fact]
    public void OwnershipFailsWhenStandardChangesDuringStabilization()
    {
        var evidence = new StabilizationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [
                Pass(FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1)),
                Pass(FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 4)),
                Pass(
                    FormalTestPassState.StabilizationPass,
                    TrainingDate.From(2026, 7, 8),
                    afterAdjacentWorkOrControlledDistractor: true,
                    standard: "No more than 8 marked drifts."),
            ]);

        var result = StabilizationOwnershipEvaluator.Evaluate(evidence);

        Assert.False(result.IsOwned);
        Assert.Contains(result.Failures, failure => failure.Kind == StabilizationOwnershipFailureKind.StandardChanged);
    }

    [Fact]
    public void OwnershipRequiresMainFailureModeAvoided()
    {
        var evidence = new StabilizationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [
                Pass(FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1)),
                Pass(FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 4)),
                Pass(
                    FormalTestPassState.StabilizationPass,
                    TrainingDate.From(2026, 7, 8),
                    afterAdjacentWorkOrControlledDistractor: true,
                    mainFailureModeAvoided: ""),
            ]);

        var result = StabilizationOwnershipEvaluator.Evaluate(evidence);

        Assert.False(result.IsOwned);
        Assert.Contains(result.Failures, failure => failure.Kind == StabilizationOwnershipFailureKind.MainFailureModeMissing);
    }

    private static StabilizationPassEvidence Pass(
        FormalTestPassState passState,
        TrainingDate date,
        bool afterAdjacentWorkOrControlledDistractor = false,
        string standard = FocusHoldL1Standard,
        string mainFailureModeAvoided = "target substitution")
    {
        return new StabilizationPassEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            date,
            standard,
            passState,
            standardEvaluationResult: PassingStandard(),
            afterAdjacentWorkOrControlledDistractor,
            mainFailureModeAvoided);
    }

    private static StandardEvaluationResult PassingStandard()
    {
        return new StandardEvaluationResult(true, []);
    }
}
