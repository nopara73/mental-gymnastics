using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class FocusHoldRuntimeProtocolTests
{
    [Fact]
    public void TargetHoldRequiresStatedTargetAndRecordsDriftReturnAndTargetSubstitutionFacts()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = FocusHoldRuntimeProtocol.Start(
            "fh1-session",
            FocusHoldSession(DrillId.FH1TargetHold, GlobalLevelId.L1),
            clock,
            new FocusHoldRuntimeProtocolOptions(RuntimeDuration.FromSeconds(10)));

        var rejectedStart = protocol.StartActiveSet();

        Assert.False(rejectedStart.IsAccepted);
        Assert.Equal(FocusHoldRuntimeProtocolInvalidReason.TargetStatementRequiredBeforeSet, rejectedStart.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        var target = protocol.StateTarget("target-1", "breath at nostrils");
        var active = protocol.StartActiveSet();
        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var drift = protocol.MarkDrift("drift-1");
        clock.AdvanceBy(RuntimeDuration.FromSeconds(7));
        var returned = protocol.RecordReturn("drift-1");
        clock.AdvanceBy(RuntimeDuration.FromSeconds(8));
        var substitution = protocol.RecordTargetSubstitution("counting ambient sounds");
        var completed = protocol.CompleteSet("set-1");

        Assert.True(target.IsAccepted);
        Assert.True(active.IsAccepted);
        Assert.True(drift.IsAccepted);
        Assert.True(returned.IsAccepted);
        Assert.True(substitution.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(target.Event!.Facts, fact => fact.Name == "target_statement" && fact.Value == "breath at nostrils");
        Assert.Contains(target.Event.Facts, fact => fact.Name == "target_statement_timing" && fact.Value == "before_set");
        Assert.Contains(drift.Event!.Facts, fact => fact.Name == "drift_marking_required" && fact.Value == "true");
        Assert.Contains(drift.Event.Facts, fact => fact.Name == "target_id" && fact.Value == "target-1");
        Assert.Contains(returned.Event!.Facts, fact => fact.Name == "recovery_time" && fact.Value == "00:00:07");
        Assert.Contains(returned.Event.Facts, fact => fact.Name == "return_within_window" && fact.Value == "true");
        Assert.Contains(substitution.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "target_substitution");
        Assert.Contains(substitution.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "no_target_substitution");
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "score" && fact.Value.Contains("marked_drifts=1", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "target_substitution_count" && fact.Value == "1");

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.MarkedDrift);
        Assert.Contains(
            scoringEvents,
            scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse &&
                scoringEvent.EvidenceFacts.Any(fact => fact.Name == "error_kind" && fact.Value == "target_substitution"));

        var failedEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "fh1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            protocol.EventLog.Events,
            scoringEvents));
        var bestEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "fh1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.BestSet,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            failedEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.FailedItemList &&
                evidence.Description.Contains("target substitution", StringComparison.Ordinal));
        Assert.Contains(
            bestEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("target_substitution_count=1", StringComparison.Ordinal));
        Assert.DoesNotContain(
            protocol.EventLog.Events.SelectMany(runtimeEvent => runtimeEvent.Facts),
            fact => fact.Name is "gate_outcome" or "pass_state");
    }

    [Fact]
    public void DistractorHoldRecordsIgnoredAndRespondedDistractorsAsEvidenceAndScoringFacts()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = FocusHoldRuntimeProtocol.Start(
            "fh2-session",
            FocusHoldSession(DrillId.FH2DistractorHold, GlobalLevelId.L3),
            clock,
            new FocusHoldRuntimeProtocolOptions(RuntimeDuration.FromSeconds(10)));

        Assert.True(protocol.StateTarget("target-1", "blue dot at center").IsAccepted);
        Assert.True(protocol.StartActiveSet().IsAccepted);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var firstDistractor = protocol.PresentDistractor("distractor-1", "irrelevant word: apple", RuntimeDuration.FromSeconds(2));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));
        var ignored = protocol.RecordDistractorIgnored("distractor-1");

        clock.AdvanceBy(RuntimeDuration.FromSeconds(20));
        var secondDistractor = protocol.PresentDistractor("distractor-2", "irrelevant word: stone", RuntimeDuration.FromSeconds(2));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var responded = protocol.RecordDistractorResponse("distractor-2", "looked");
        var completed = protocol.CompleteSet("set-1");

        Assert.True(firstDistractor.IsAccepted);
        Assert.True(ignored.IsAccepted);
        Assert.True(secondDistractor.IsAccepted);
        Assert.True(responded.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Equal(RuntimeEventKind.CueEmitted, firstDistractor.Event!.Kind);
        Assert.Contains(firstDistractor.Event.Facts, fact => fact.Name == "cue_kind" && fact.Value == "distractor");
        Assert.Contains(firstDistractor.Event.Facts, fact => fact.Name == "response_expectation" && fact.Value == "no_response_expected");
        Assert.Contains(ignored.Event!.Facts, fact => fact.Name == "distractor_response" && fact.Value == "ignored");
        Assert.Contains(ignored.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(responded.Event!.Facts, fact => fact.Name == "response_outcome" && fact.Value == "incorrect");
        Assert.Contains(responded.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "do_not_respond_to_distractor");
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "score" && fact.Value.Contains("distractors_ignored=1", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "distractor_response_count" && fact.Value == "1");

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();

        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.Commission);

        var failedEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "fh2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            protocol.EventLog.Events,
            scoringEvents));
        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "fh2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            failedEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.FailedItemList &&
                evidence.Description.Contains("responded to distractor", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.CriticalConstraintRecord &&
                evidence.Description.Contains("Do not respond to distractor", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("distractor_response_count=1", StringComparison.Ordinal));
    }

    [Fact]
    public void FocusHoldProtocolRejectsWrongSessionsAndTargetHoldDistractorsWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);

        Assert.Throws<ArgumentException>(() => FocusHoldRuntimeProtocol.Start(
            "wrong-branch",
            FocusShiftSession(),
            clock));

        var targetHold = FocusHoldRuntimeProtocol.Start(
            "fh1-no-distractors",
            FocusHoldSession(DrillId.FH1TargetHold, GlobalLevelId.L1),
            clock);
        Assert.True(targetHold.StateTarget("target-1", "breath").IsAccepted);
        Assert.True(targetHold.StartActiveSet().IsAccepted);
        var eventCountBeforeDistractor = targetHold.EventLog.Events.Count;

        var rejectedDistractor = targetHold.PresentDistractor(
            "distractor-1",
            "not part of FH-1",
            RuntimeDuration.FromSeconds(2));

        Assert.False(rejectedDistractor.IsAccepted);
        Assert.Equal(FocusHoldRuntimeProtocolInvalidReason.DistractorsNotSupportedByDrill, rejectedDistractor.InvalidReason);
        Assert.Equal(eventCountBeforeDistractor, targetHold.EventLog.Events.Count);
    }

    private static RuntimeSessionDefinition FocusHoldSession(DrillId drill, GlobalLevelId level)
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.FH &&
            standard.Level == level);
        CriticalConstraint[] constraints = drill == DrillId.FH2DistractorHold
            ?
            [
                new CriticalConstraint("Target is stated before set; every drift is marked."),
                new CriticalConstraint("Do not respond to distractor unless drill says so."),
                new CriticalConstraint("No target substitution."),
            ]
            :
            [
                new CriticalConstraint("Target is stated before set; every drift is marked."),
                new CriticalConstraint("No target substitution."),
            ];

        LoadVariable[] loadVariables = drill == DrillId.FH2DistractorHold
            ? [new LoadVariable("duration", "5 minutes"), new LoadVariable("distractor frequency", "2 prompts")]
            : [new LoadVariable("duration", "3 minutes")];

        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            level,
            drill,
            loadVariables,
            standard,
            constraints);
    }

    private static RuntimeSessionDefinition FocusShiftSession()
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.FS &&
            standard.Level == GlobalLevelId.L1);

        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FS,
            GlobalLevelId.L1,
            DrillId.FS1CueSwitch,
            [new LoadVariable("cue density", "4 minutes")],
            standard,
            [new CriticalConstraint("Switch only on valid cue.")]);
    }
}
