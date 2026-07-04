using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class StuckStateDetectionTests
{
    [Fact]
    public void DetectsSameBranchGateFailsThreeTimesAcrossAtLeastTenDays()
    {
        var result = StuckStateDetector.Evaluate(
            History(
                gateFailures:
                [
                    GateFailure(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1)),
                    GateFailure(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 6)),
                    GateFailure(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 11)),
                ]));

        Assert.True(result.IsStuck);
        Assert.Contains(
            result.Conditions,
            condition => condition.Kind == StuckStateConditionKind.SameBranchGateFailedThreeTimesAcrossTenDays &&
                condition.Branch == BranchCode.WM &&
                condition.Level == GlobalLevelId.L3);
    }

    [Fact]
    public void DoesNotDetectGateStuckWhenFailuresAreTooFewOrTooCloseTogether()
    {
        var tooFew = StuckStateDetector.Evaluate(
            History(
                gateFailures:
                [
                    GateFailure(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1)),
                    GateFailure(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 11)),
                ]));
        var tooClose = StuckStateDetector.Evaluate(
            History(
                gateFailures:
                [
                    GateFailure(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1)),
                    GateFailure(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 3)),
                    GateFailure(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 8)),
                ]));

        Assert.False(tooFew.IsStuck);
        Assert.False(tooClose.IsStuck);
    }

    [Fact]
    public void DetectsSameCriticalConstraintFailureInTwoConsecutiveRegressionSessions()
    {
        var result = StuckStateDetector.Evaluate(
            History(
                regressionFailures:
                [
                    RegressionFailure(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1), "premature response"),
                    RegressionFailure(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 3), "premature response"),
                ]));

        Assert.True(result.IsStuck);
        Assert.Contains(
            result.Conditions,
            condition => condition.Kind == StuckStateConditionKind.SameCriticalConstraintFailedInConsecutiveRegressions &&
                condition.Branch == BranchCode.IR &&
                condition.Constraint == "premature response");
    }

    [Fact]
    public void DoesNotDetectRegressionStuckWhenCleanRegressionInterruptsTheRepeatedConstraint()
    {
        var result = StuckStateDetector.Evaluate(
            History(
                regressionFailures:
                [
                    RegressionFailure(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1), "premature response"),
                    RegressionPass(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 2)),
                    RegressionFailure(BranchCode.IR, GlobalLevelId.L2, TrainingDate.From(2026, 7, 3), "premature response"),
                ]));

        Assert.False(result.IsStuck);
    }

    [Fact]
    public void DetectsPrerequisiteBranchRepeatedlyDecaysWhileDependentBranchIsTraining()
    {
        var result = StuckStateDetector.Evaluate(
            History(
                prerequisiteDecayEvents:
                [
                    PrerequisiteDecay(BranchCode.WM, BranchCode.CO, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1)),
                    PrerequisiteDecay(BranchCode.WM, BranchCode.CO, GlobalLevelId.L3, TrainingDate.From(2026, 7, 8)),
                ]));

        Assert.True(result.IsStuck);
        Assert.Contains(
            result.Conditions,
            condition => condition.Kind == StuckStateConditionKind.PrerequisiteRepeatedlyDecayedWhileDependentTraining &&
                condition.Branch == BranchCode.CO &&
                condition.PrerequisiteBranch == BranchCode.WM);
    }

    [Fact]
    public void DetectsSameBottleneckNamedInTwoGlobalReviewsWithoutImprovement()
    {
        var result = StuckStateDetector.Evaluate(
            History(
                globalReviews:
                [
                    GlobalReview(TrainingDate.From(2026, 7, 1), BottleneckKind.WorkingMemoryEncodingFidelity, improvedEvidence: false),
                    GlobalReview(TrainingDate.From(2026, 7, 29), BottleneckKind.WorkingMemoryEncodingFidelity, improvedEvidence: false),
                ]));

        Assert.True(result.IsStuck);
        Assert.Contains(
            result.Conditions,
            condition => condition.Kind == StuckStateConditionKind.SameBottleneckInTwoGlobalReviewsWithoutImprovement &&
                condition.Bottleneck == BottleneckKind.WorkingMemoryEncodingFidelity);
    }

    [Fact]
    public void DoesNotDetectGlobalReviewStuckWhenEvidenceImproves()
    {
        var result = StuckStateDetector.Evaluate(
            History(
                globalReviews:
                [
                    GlobalReview(TrainingDate.From(2026, 7, 1), BottleneckKind.WorkingMemoryEncodingFidelity, improvedEvidence: false),
                    GlobalReview(TrainingDate.From(2026, 7, 29), BottleneckKind.WorkingMemoryEncodingFidelity, improvedEvidence: true),
                ]));

        Assert.False(result.IsStuck);
    }

    [Fact]
    public void DetectsIsolatedDrillsPassButTwoRelatedTransferTestsFail()
    {
        var result = StuckStateDetector.Evaluate(
            History(
                transferReadiness:
                [
                    TransferReadiness(BranchCode.FH, GlobalLevelId.L4, isolatedDrillPassed: true),
                ],
                transferFailures:
                [
                    TransferFailure(BranchCode.FH, GlobalLevelId.L4, "hold target during WM task", TrainingDate.From(2026, 7, 1)),
                    TransferFailure(BranchCode.FH, GlobalLevelId.L4, "hold target during DE task", TrainingDate.From(2026, 7, 4)),
                ]));

        Assert.True(result.IsStuck);
        Assert.Contains(
            result.Conditions,
            condition => condition.Kind == StuckStateConditionKind.IsolatedDrillsPassButRelatedTransferTestsFail &&
                condition.Branch == BranchCode.FH &&
                condition.Level == GlobalLevelId.L4);
    }

    [Fact]
    public void DoesNotUseSubjectiveFrustrationAsStuckEvidence()
    {
        var result = StuckStateDetector.Evaluate(
            History(subjectiveFrustrationNotes: ["felt stuck", "discouraged"]));

        Assert.False(result.IsStuck);
        Assert.Empty(result.Conditions);
    }

    private static StuckStateHistory History(
        IEnumerable<BranchGateFailureRecord>? gateFailures = null,
        IEnumerable<RegressionSessionResultRecord>? regressionFailures = null,
        IEnumerable<PrerequisiteDecayDuringTrainingRecord>? prerequisiteDecayEvents = null,
        IEnumerable<GlobalReviewBottleneckRecord>? globalReviews = null,
        IEnumerable<TransferReadinessRecord>? transferReadiness = null,
        IEnumerable<TransferTestFailureRecord>? transferFailures = null,
        IEnumerable<string>? subjectiveFrustrationNotes = null)
    {
        return new StuckStateHistory(
            gateFailures ?? [],
            regressionFailures ?? [],
            prerequisiteDecayEvents ?? [],
            globalReviews ?? [],
            transferReadiness ?? [],
            transferFailures ?? [],
            subjectiveFrustrationNotes ?? []);
    }

    private static BranchGateFailureRecord GateFailure(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date)
    {
        return new BranchGateFailureRecord(branch, level, date);
    }

    private static RegressionSessionResultRecord RegressionFailure(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        string criticalConstraint)
    {
        return new RegressionSessionResultRecord(branch, level, date, criticalConstraint, passed: false);
    }

    private static RegressionSessionResultRecord RegressionPass(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date)
    {
        return new RegressionSessionResultRecord(branch, level, date, criticalConstraint: null, passed: true);
    }

    private static PrerequisiteDecayDuringTrainingRecord PrerequisiteDecay(
        BranchCode prerequisiteBranch,
        BranchCode dependentBranch,
        GlobalLevelId prerequisiteLevel,
        TrainingDate date)
    {
        return new PrerequisiteDecayDuringTrainingRecord(
            prerequisiteBranch,
            dependentBranch,
            prerequisiteLevel,
            date,
            dependentBranchWasTraining: true);
    }

    private static GlobalReviewBottleneckRecord GlobalReview(
        TrainingDate date,
        BottleneckKind bottleneck,
        bool improvedEvidence)
    {
        return new GlobalReviewBottleneckRecord(date, bottleneck, improvedEvidence);
    }

    private static TransferReadinessRecord TransferReadiness(
        BranchCode branch,
        GlobalLevelId level,
        bool isolatedDrillPassed)
    {
        return new TransferReadinessRecord(branch, level, isolatedDrillPassed);
    }

    private static TransferTestFailureRecord TransferFailure(
        BranchCode branch,
        GlobalLevelId level,
        string transferTask,
        TrainingDate date)
    {
        return new TransferTestFailureRecord(branch, level, transferTask, date);
    }
}
