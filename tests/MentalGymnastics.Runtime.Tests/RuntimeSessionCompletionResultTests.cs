using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeSessionCompletionResultTests
{
    [Fact]
    public void CompletedSessionResultIncludesPersistentRuntimeFactsWithoutAdvancementDecision()
    {
        var session = CreateSessionDefinition(SessionType.Practice);
        var log = RuntimeEventLog.Start("session-complete", session, RuntimeInstant.Zero);
        var phaseHistory = new[]
        {
            CompletedPhase("prep", RuntimeSessionPhaseKind.InstructionPrep, 0, 5, RuntimeSessionPhaseCompletionCause.Explicit),
            CompletedPhase("active", RuntimeSessionPhaseKind.ActiveWork, 5, 185, RuntimeSessionPhaseCompletionCause.Explicit),
        };
        log.Append(
            RuntimeEventKind.PhaseStarted,
            RuntimeInstant.Zero,
            "prep",
            RuntimeSessionPhaseKind.InstructionPrep);
        log.Append(
            RuntimeEventKind.DriftMarked,
            RuntimeDuration.FromSeconds(30).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [new RuntimeEventFact("drift_id", "drift-1")]);
        log.Append(
            RuntimeEventKind.AnswerSubmitted,
            RuntimeDuration.FromSeconds(180).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                new RuntimeEventFact("output_sample", "best set: target held with 4 marked drifts"),
                new RuntimeEventFact("score", "drifts=4; max_return=8s"),
            ]);
        log.Append(RuntimeEventKind.SessionCompleted, RuntimeDuration.FromSeconds(185).ToInstant());

        var scoringEvents = log.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        var evidenceDraft = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-complete",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.BestSet,
            log.Events,
            scoringEvents));

        var result = RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            "session-complete",
            session,
            RuntimeSessionCompletionStatus.Completed,
            phaseHistory,
            log.Events,
            scoringEvents,
            [evidenceDraft]));

        Assert.Equal(RuntimeSessionCompletionStatus.Completed, result.CompletionStatus);
        Assert.Equal("session-complete", result.SessionId);
        Assert.Equal(BranchCode.FH, result.Branch);
        Assert.Equal(GlobalLevelId.L1, result.Level);
        Assert.Equal(DrillId.FH1TargetHold, result.Drill);
        Assert.Contains(result.LoadVariables, variable => variable.Name == "duration" && variable.Value == "3 minutes");
        Assert.Equal(2, result.PhaseHistory.Count);
        Assert.Equal(RuntimeSessionPhaseCompletionCause.Explicit, result.PhaseHistory[1].CompletionCause);
        Assert.Equal(log.Events.Count, result.RuntimeEvents.Count);
        Assert.Contains(result.ScoringFacts, fact => fact.Name == "scoring_event_kind" && fact.Value == "marked_drift");
        Assert.Equal(1, result.EvidenceSummary.ArtifactCount);
        Assert.Contains(result.EvidenceSummary.Categories, category => category == EvidenceArtifactCategory.Practice);
        Assert.Contains(result.EvidenceSummary.ObservableEvidenceKinds, kind => kind == ObservableEvidenceKind.OutputSample);
        Assert.Empty(result.FailureRelevantFacts);
        Assert.False(result.ContainsAdvancementDecision);
        Assert.DoesNotContain(result.ResultFacts, fact => fact.Name is "gate_outcome" or "pass_state");
    }

    [Fact]
    public void FailedSessionResultCarriesFailureRelevantFactsAndScoringFacts()
    {
        var session = CreateSessionDefinition(SessionType.Test);
        var log = RuntimeEventLog.Start("session-failed", session, RuntimeInstant.Zero);
        var errorEvent = log.Append(
            RuntimeEventKind.ErrorRecorded,
            RuntimeDuration.FromSeconds(90).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                new RuntimeEventFact("error_kind", "unmarked_drift"),
                new RuntimeEventFact("failed_item_list", "set failed: drift was not marked before reset"),
                new RuntimeEventFact("failed_constraint", "Target is stated before set; every drift is marked."),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
            ]);
        var scoringEvents = new[] { RuntimeScoringEventFactory.FromRuntimeEvent(errorEvent)! };
        var evidenceDraft = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-failed",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            log.Events,
            scoringEvents));

        var result = RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            "session-failed",
            session,
            RuntimeSessionCompletionStatus.Failed,
            [CompletedPhase("active", RuntimeSessionPhaseKind.ActiveWork, 0, 90, RuntimeSessionPhaseCompletionCause.Explicit)],
            log.Events,
            scoringEvents,
            [evidenceDraft]));

        Assert.Equal(RuntimeSessionCompletionStatus.Failed, result.CompletionStatus);
        Assert.Contains(result.ScoringFacts, fact => fact.Name == "scoring_event_kind" && fact.Value == "unmarked_drift");
        Assert.Contains(result.FailureRelevantFacts, fact => fact.Name == "error_kind" && fact.Value == "unmarked_drift");
        Assert.Contains(result.FailureRelevantFacts, fact => fact.Name == "failed_constraint" && fact.Value.Contains("every drift", StringComparison.Ordinal));
        Assert.Contains(result.FailureRelevantFacts, fact => fact.Name == "failure_type_candidate" && fact.Value == "effort_failure");
        Assert.Contains(result.EvidenceSummary.ObservableEvidenceKinds, kind => kind == ObservableEvidenceKind.FailedItemList);
        Assert.False(result.ContainsAdvancementDecision);
    }

    [Theory]
    [InlineData(RuntimeSessionCompletionStatus.Failed)]
    [InlineData(RuntimeSessionCompletionStatus.TimedOut)]
    public void FailedOrTimedOutSessionRejectsBestSetEvidence(RuntimeSessionCompletionStatus completionStatus)
    {
        var session = CreateSessionDefinition(SessionType.Practice);
        var log = RuntimeEventLog.Start("session-false-success", session, RuntimeInstant.Zero);
        log.Append(
            RuntimeEventKind.AnswerSubmitted,
            RuntimeDuration.FromSeconds(30).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                new RuntimeEventFact("output_sample", "best set: target held cleanly before failure"),
                new RuntimeEventFact("score", "drifts=0"),
            ]);
        log.Append(
            completionStatus == RuntimeSessionCompletionStatus.Failed
                ? RuntimeEventKind.ErrorRecorded
                : RuntimeEventKind.PhaseTimedOut,
            RuntimeDuration.FromSeconds(40).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            completionStatus == RuntimeSessionCompletionStatus.Failed
                ? [new RuntimeEventFact("error_kind", "target_substitution")]
                : [new RuntimeEventFact("timeout_overtime", "00:00:01")]);
        var scoringEvents = log.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        var successLookingEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-false-success",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.BestSet,
            log.Events,
            scoringEvents));

        var exception = Assert.Throws<ArgumentException>(() =>
            RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
                "session-false-success",
                session,
                completionStatus,
                [CompletedPhase("active", RuntimeSessionPhaseKind.ActiveWork, 0, 40, RuntimeSessionPhaseCompletionCause.Explicit)],
                log.Events,
                scoringEvents,
                [successLookingEvidence])));

        Assert.Contains("successful evidence", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AbandonedSessionResultCarriesAbandonmentFactsWithoutRequiringEvidenceDraft()
    {
        var session = CreateSessionDefinition(SessionType.Practice);
        var log = RuntimeEventLog.Start("session-abandoned", session, RuntimeInstant.Zero);
        log.Append(
            RuntimeEventKind.SessionAbandoned,
            RuntimeDuration.FromSeconds(12).ToInstant(),
            facts: [new RuntimeEventFact("abandon_reason", "user stopped during active work")]);

        var result = RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            "session-abandoned",
            session,
            RuntimeSessionCompletionStatus.Abandoned,
            [],
            log.Events,
            [],
            []));

        Assert.Equal(RuntimeSessionCompletionStatus.Abandoned, result.CompletionStatus);
        Assert.Empty(result.EvidenceDrafts);
        Assert.Equal(0, result.EvidenceSummary.ArtifactCount);
        Assert.Contains(result.FailureRelevantFacts, fact => fact.Name == "abandon_reason" && fact.Value.Contains("active work", StringComparison.Ordinal));
        Assert.Equal(log.Events.Count, result.RuntimeEvents.Count);
        Assert.False(result.ContainsAdvancementDecision);
    }

    [Fact]
    public void AbandonedSessionResultRejectsSuccessLookingEvidenceDrafts()
    {
        var session = CreateSessionDefinition(SessionType.Practice);
        var log = RuntimeEventLog.Start("session-abandoned-with-evidence", session, RuntimeInstant.Zero);
        log.Append(
            RuntimeEventKind.AnswerSubmitted,
            RuntimeDuration.FromSeconds(8).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                new RuntimeEventFact("output_sample", "partial set before abandonment"),
                new RuntimeEventFact("score", "drifts=0"),
            ]);
        log.Append(
            RuntimeEventKind.SessionAbandoned,
            RuntimeDuration.FromSeconds(12).ToInstant(),
            facts: [new RuntimeEventFact("abandon_reason", "user stopped before the set finished")]);
        var scoringEvents = log.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        var evidenceDraft = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-abandoned-with-evidence",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.BestSet,
            log.Events,
            scoringEvents));

        var exception = Assert.Throws<ArgumentException>(() =>
            RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
                "session-abandoned-with-evidence",
                session,
                RuntimeSessionCompletionStatus.Abandoned,
                [],
                log.Events,
                scoringEvents,
                [evidenceDraft])));

        Assert.Contains("Abandoned", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TimedOutSessionResultCarriesTimeoutPhaseAndScoringFacts()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var session = CreateSessionDefinition(SessionType.Practice);
        var scheduler = new RuntimePhaseScheduler(
            session,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("active", RuntimeSessionPhaseKind.ActiveWork, RuntimeDuration.FromSeconds(10)),
            ]),
            clock);
        var log = RuntimeEventLog.Start("session-timeout", session, clock.Now);

        log.AppendSchedulerEvents(scheduler.Start().Events);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(12));
        log.AppendSchedulerEvents(scheduler.AdvanceToCurrentTime().Events);

        var scoringEvents = log.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();

        var result = RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            "session-timeout",
            session,
            RuntimeSessionCompletionStatus.TimedOut,
            scheduler.CompletedPhases,
            log.Events,
            scoringEvents,
            []));

        Assert.Equal(RuntimeSessionCompletionStatus.TimedOut, result.CompletionStatus);
        Assert.Single(result.PhaseHistory);
        Assert.Equal(RuntimeSessionPhaseCompletionCause.Timeout, result.PhaseHistory[0].CompletionCause);
        Assert.Contains(result.RuntimeEvents, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.PhaseTimedOut);
        Assert.Contains(result.ScoringFacts, fact => fact.Name == "scoring_event_kind" && fact.Value == "timeout");
        Assert.Contains(result.FailureRelevantFacts, fact => fact.Name == "timeout_overtime" && fact.Value == "00:00:02");
        Assert.Contains(result.ResultFacts, fact => fact.Name == "completion_status" && fact.Value == "timed_out");
        Assert.False(result.ContainsAdvancementDecision);
    }

    private static RuntimeCompletedSessionPhase CompletedPhase(
        string id,
        RuntimeSessionPhaseKind kind,
        int startedAtSeconds,
        int completedAtSeconds,
        RuntimeSessionPhaseCompletionCause completionCause)
    {
        var startedAt = RuntimeDuration.FromSeconds(startedAtSeconds).ToInstant();
        var completedAt = RuntimeDuration.FromSeconds(completedAtSeconds).ToInstant();
        return new RuntimeCompletedSessionPhase(
            RuntimeSessionPhaseDefinition.Manual(id, kind),
            startedAt,
            completedAt,
            completedAt.ElapsedSince(startedAt),
            completionCause);
    }

    private static RuntimeSessionDefinition CreateSessionDefinition(SessionType sessionType)
    {
        return new RuntimeSessionDefinition(
            sessionType,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")],
            new BranchLevelStandard(
                BranchCode.FH,
                GlobalLevelId.L1,
                "Hold one simple target for 3 minutes.",
                "No more than 5 marked drifts; each return within 10 seconds; no target change.",
                "Pass once enters stabilization.",
                "Repeat twice within 14 days; one after a short WM set.",
                "Hold a different target type with same standard."),
            [new CriticalConstraint("Target is stated before set; every drift is marked.")]);
    }
}
