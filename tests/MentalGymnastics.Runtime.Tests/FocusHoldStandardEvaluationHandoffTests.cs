using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class FocusHoldStandardEvaluationHandoffTests
{
    [Fact]
    public void MapperProducesPassingCoreAttemptFromCompleteCleanRuntimeEvidence()
    {
        var result = Result(
            RuntimeSessionCompletionStatus.Completed,
            activeDurationSeconds: 180,
            [
                Event(RuntimeEventKind.DriftMarked, 30, new RuntimeEventFact("drift_id", "drift-1")),
            ]);

        var summary = FocusHoldStandardEvaluationHandoffMapper.Summarize(result);
        var handoff = FocusHoldStandardEvaluationHandoffMapper.ToStandardEvaluationInput(
            result,
            targetStatedBeforeSet: true,
            everyNoticedDriftMarked: true);
        var attempt = new StandardEvaluationAttempt(
            handoff.Measurements,
            handoff.CriticalConstraintChecks,
            handoff.OutputComplete,
            handoff.RubricOutcome);
        var evaluation = StandardEvaluator.Evaluate(
            FocusHoldLevelOneStandard.Create(),
            attempt);

        Assert.Equal(180, summary.ActiveDurationSeconds);
        Assert.Equal(1, summary.MarkedDriftCount);
        Assert.Equal(0, summary.TargetSubstitutionCount);
        Assert.True(evaluation.Passed);
    }

    [Fact]
    public void MapperIgnoresLegacyReturnTimingAndPreservesTargetChangeFailure()
    {
        var result = Result(
            RuntimeSessionCompletionStatus.Completed,
            activeDurationSeconds: 180,
            [
                Event(RuntimeEventKind.DriftMarked, 30, new RuntimeEventFact("drift_id", "drift-late")),
                Event(
                    RuntimeEventKind.RecoveryCompleted,
                    42,
                    new RuntimeEventFact("drift_id", "drift-late"),
                    new RuntimeEventFact("recovery_time", "00:00:12"),
                    new RuntimeEventFact("return_within_window", "false")),
                Event(RuntimeEventKind.DriftMarked, 80, new RuntimeEventFact("drift_id", "drift-open")),
                Event(
                    RuntimeEventKind.ErrorRecorded,
                    90,
                    new RuntimeEventFact("error_kind", "target_substitution")),
            ]);

        var summary = FocusHoldStandardEvaluationHandoffMapper.Summarize(result);
        var handoff = FocusHoldStandardEvaluationHandoffMapper.ToStandardEvaluationInput(
            result,
            targetStatedBeforeSet: true,
            everyNoticedDriftMarked: true);
        var evaluation = StandardEvaluator.Evaluate(
            FocusHoldLevelOneStandard.Create(),
            new StandardEvaluationAttempt(
                handoff.Measurements,
                handoff.CriticalConstraintChecks,
                handoff.OutputComplete,
                handoff.RubricOutcome));

        Assert.Equal(2, summary.MarkedDriftCount);
        Assert.Equal(1, summary.TargetSubstitutionCount);
        Assert.False(evaluation.Passed);
        Assert.Single(
            evaluation.Failures,
            failure => failure.Kind == StandardFailureKind.NumericalThresholdMissed);
    }

    private static RuntimeSessionCompletionResult Result(
        RuntimeSessionCompletionStatus status,
        int activeDurationSeconds,
        IReadOnlyList<RuntimeSessionCoordinatorEventInput> events)
    {
        var session = new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")],
            ProgramCatalog.Standards.Single(standard =>
                standard.Branch == BranchCode.FH && standard.Level == GlobalLevelId.L1),
            [new CriticalConstraint("Target is stated before set; every drift is marked.")]);
        var startedAt = RuntimeInstant.Zero;
        var completedAt = RuntimeDuration.FromSeconds(activeDurationSeconds).ToInstant();
        var phase = new RuntimeCompletedSessionPhase(
            RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
            startedAt,
            completedAt,
            completedAt.ElapsedSince(startedAt),
            RuntimeSessionPhaseCompletionCause.Explicit);

        return RuntimeSessionCoordinator.Complete(new RuntimeSessionCoordinatorRequest(
            "focus-hold-standard-handoff",
            session,
            startedAt,
            completedAt,
            status,
            status == RuntimeSessionCompletionStatus.Completed
                ? RuntimeEvidenceCaptureKind.BestSet
                : RuntimeEvidenceCaptureKind.FailedSet,
            [phase],
            events,
            completionFacts:
            [
                new RuntimeEventFact("output_sample", "FH-1 target hold evidence"),
                new RuntimeEventFact("score", "runtime focus hold summary"),
            ])).CompletionResult;
    }

    private static RuntimeSessionCoordinatorEventInput Event(
        RuntimeEventKind kind,
        int occurredAtSeconds,
        params RuntimeEventFact[] facts)
    {
        return new RuntimeSessionCoordinatorEventInput(
            kind,
            RuntimeDuration.FromSeconds(occurredAtSeconds).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);
    }
}
