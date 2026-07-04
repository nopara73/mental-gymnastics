using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeAntiSelfDeceptionGuardTests
{
    [Theory]
    [MemberData(nameof(PreventedBypassCases))]
    public void GuardPreventsConstraintBypassesWithObservableEvidenceFacts(
        RuntimeAntiSelfDeceptionBypassKind bypassKind,
        string expectedViolation,
        string expectedErrorKind,
        string expectedFailedConstraint,
        RuntimeEventFact[] contextFacts)
    {
        var session = CreateSessionDefinition();
        var result = RuntimeAntiSelfDeceptionGuard.Prevent(new RuntimeAntiSelfDeceptionGuardRequest(
            bypassKind,
            session,
            phaseId: "active",
            phaseKind: RuntimeSessionPhaseKind.ActiveWork,
            contextFacts: contextFacts));

        Assert.False(result.AttemptAllowed);
        Assert.Equal(RuntimeAntiSelfDeceptionGuardDisposition.Prevented, result.Disposition);
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "anti_self_deception_guard" && fact.Value == "true");
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "guard_violation" && fact.Value == expectedViolation);
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "error_kind" && fact.Value == expectedErrorKind);
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "failed_constraint" && fact.Value == expectedFailedConstraint);
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "attempt_prevented" && fact.Value == "true");
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "branch" && fact.Value == "FH");
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "level" && fact.Value == "L1");
        Assert.DoesNotContain(result.EvidenceFacts, IsMotivationalOrUiCopy);

        var log = RuntimeEventLog.Start("session-guard", session, RuntimeInstant.Zero);
        var evidenceEvent = RuntimeAntiSelfDeceptionGuard.AppendEvidence(
            log,
            result,
            RuntimeDuration.FromSeconds(1).ToInstant());

        Assert.Equal(RuntimeEventKind.ErrorRecorded, evidenceEvent.Kind);
        Assert.Equal("active", evidenceEvent.PhaseId);
        Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, evidenceEvent.PhaseKind);
        Assert.Contains(contextFacts, context =>
            evidenceEvent.Facts.Any(fact => fact.Name == context.Name && fact.Value == context.Value));

        var scoringEvent = RuntimeScoringEventFactory.FromRuntimeEvent(evidenceEvent);

        Assert.NotNull(scoringEvent);
        Assert.DoesNotContain(scoringEvent!.EvidenceFacts, IsMotivationalOrUiCopy);
    }

    [Fact]
    public void GuardCanRecordDetectableBypassAttemptsWhenTheyCannotBePreventedLive()
    {
        var session = CreateSessionDefinition();
        var result = RuntimeAntiSelfDeceptionGuard.Record(new RuntimeAntiSelfDeceptionGuardRequest(
            RuntimeAntiSelfDeceptionBypassKind.UnmarkedGuessWhereRequired,
            session,
            phaseId: "reconstruct",
            phaseKind: RuntimeSessionPhaseKind.ReconstructionInput,
            contextFacts:
            [
                new RuntimeEventFact("answer_id", "answer-7"),
                new RuntimeEventFact("guess_marking_required", "true"),
            ]));

        Assert.True(result.AttemptAllowed);
        Assert.Equal(RuntimeAntiSelfDeceptionGuardDisposition.Recorded, result.Disposition);
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "guard_violation" && fact.Value == "unmarked_guess");
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "error_kind" && fact.Value == "unmarked_guess");
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "attempt_prevented" && fact.Value == "false");
        Assert.Contains(result.EvidenceFacts, fact => fact.Name == "attempt_recorded" && fact.Value == "true");
        Assert.DoesNotContain(result.EvidenceFacts, IsMotivationalOrUiCopy);
    }

    [Fact]
    public void AbandonedEvidenceGuardCreatesFailureFactsWithoutTurningAbandonmentIntoEvidence()
    {
        var session = CreateSessionDefinition();
        var log = RuntimeEventLog.Start("session-abandoned-guarded", session, RuntimeInstant.Zero);
        var guardResult = RuntimeAntiSelfDeceptionGuard.Prevent(new RuntimeAntiSelfDeceptionGuardRequest(
            RuntimeAntiSelfDeceptionBypassKind.AbandonedEvidence,
            session,
            phaseId: "active",
            phaseKind: RuntimeSessionPhaseKind.ActiveWork,
            contextFacts:
            [
                new RuntimeEventFact("attempted_evidence_category", "practice"),
                new RuntimeEventFact("attempted_evidence_source", "partial_set"),
            ]));

        RuntimeAntiSelfDeceptionGuard.AppendEvidence(
            log,
            guardResult,
            RuntimeDuration.FromSeconds(20).ToInstant());
        log.Append(
            RuntimeEventKind.SessionAbandoned,
            RuntimeDuration.FromSeconds(21).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [new RuntimeEventFact("abandon_reason", "stopped before set completion")]);

        var scoringEvents = log.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        var result = RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            "session-abandoned-guarded",
            session,
            RuntimeSessionCompletionStatus.Abandoned,
            [],
            log.Events,
            scoringEvents,
            []));

        Assert.Empty(result.EvidenceDrafts);
        Assert.Equal(0, result.EvidenceSummary.ArtifactCount);
        Assert.Contains(result.FailureRelevantFacts, fact => fact.Name == "guard_violation" && fact.Value == "abandoned_evidence");
        Assert.Contains(result.FailureRelevantFacts, fact => fact.Name == "error_kind" && fact.Value == "abandoned_evidence");
        Assert.Contains(result.FailureRelevantFacts, fact => fact.Name == "abandon_reason" && fact.Value.Contains("before set completion", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ResultFacts, IsMotivationalOrUiCopy);
    }

    public static IEnumerable<object[]> PreventedBypassCases()
    {
        yield return
        [
            RuntimeAntiSelfDeceptionBypassKind.SkippedPhase,
            "skipped_phase",
            "skipped_phase",
            "phase_order_required",
            new[]
            {
                new RuntimeEventFact("expected_phase", "encode"),
                new RuntimeEventFact("attempted_phase", "reconstruct"),
            },
        ];
        yield return
        [
            RuntimeAntiSelfDeceptionBypassKind.ChangedTarget,
            "changed_target",
            "target_substitution",
            "no_target_substitution",
            new[]
            {
                new RuntimeEventFact("original_target", "red square"),
                new RuntimeEventFact("attempted_target", "blue circle"),
            },
        ];
        yield return
        [
            RuntimeAntiSelfDeceptionBypassKind.UnallowedRereading,
            "unallowed_rereading",
            "reread_after_encode",
            "no_rereading_after_encode",
            new[]
            {
                new RuntimeEventFact("reread_reference", "source_card_3"),
                new RuntimeEventFact("rereading_after_encode_allowed", "false"),
            },
        ];
        yield return
        [
            RuntimeAntiSelfDeceptionBypassKind.HiddenNotesWhereProhibited,
            "hidden_notes_where_prohibited",
            "hidden_intermediate_notes",
            "intermediate_notes_prohibited",
            new[]
            {
                new RuntimeEventFact("note_reference", "scratchpad"),
                new RuntimeEventFact("intermediate_notes_allowed", "false"),
            },
        ];
        yield return
        [
            RuntimeAntiSelfDeceptionBypassKind.UnmarkedGuessWhereRequired,
            "unmarked_guess",
            "unmarked_guess",
            "guess_marking_required",
            new[]
            {
                new RuntimeEventFact("answer_id", "answer-2"),
                new RuntimeEventFact("guess_marking_required", "true"),
            },
        ];
        yield return
        [
            RuntimeAntiSelfDeceptionBypassKind.PrematureResponse,
            "premature_response",
            "premature_response",
            "no_premature_response",
            new[]
            {
                new RuntimeEventFact("cue_id", "cue-9"),
                new RuntimeEventFact("response_timing", "early"),
            },
        ];
        yield return
        [
            RuntimeAntiSelfDeceptionBypassKind.InvalidRestart,
            "invalid_restart",
            "full_restart_attempt",
            "no_full_restart_unless_allowed",
            new[]
            {
                new RuntimeEventFact("full_restart_allowed", "false"),
                new RuntimeEventFact("restart_attempted", "true"),
            },
        ];
        yield return
        [
            RuntimeAntiSelfDeceptionBypassKind.BranchSpecificEvidenceRemoved,
            "branch_specific_evidence_removed",
            "branch_specific_evidence_removed",
            "branch_specific_evidence_required",
            new[]
            {
                new RuntimeEventFact("component_branch", "WM"),
                new RuntimeEventFact("branch_specific_evidence_required", "true"),
            },
        ];
    }

    private static bool IsMotivationalOrUiCopy(RuntimeEventFact fact)
    {
        return fact.Name.Contains("message", StringComparison.OrdinalIgnoreCase) ||
            fact.Name.Contains("copy", StringComparison.OrdinalIgnoreCase) ||
            fact.Name.Contains("encouragement", StringComparison.OrdinalIgnoreCase) ||
            fact.Name.Contains("motivation", StringComparison.OrdinalIgnoreCase);
    }

    private static RuntimeSessionDefinition CreateSessionDefinition()
    {
        return new RuntimeSessionDefinition(
            SessionType.Practice,
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
