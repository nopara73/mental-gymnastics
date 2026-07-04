using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class FocusShiftRuntimeProtocolTests
{
    [Fact]
    public void CueSwitchRecordsValidCueResponsesSequenceAccuracyAndAnticipatorySwitchFacts()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = FocusShiftRuntimeProtocol.Start(
            "fs1-session",
            FocusShiftSession(DrillId.FS1CueSwitch, GlobalLevelId.L1),
            clock);

        var activeBeforeTargets = protocol.StartActiveSet();

        Assert.False(activeBeforeTargets.IsAccepted);
        Assert.Equal(FocusShiftRuntimeProtocolInvalidReason.TargetSequenceRequiredBeforeSet, activeBeforeTargets.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        Assert.True(protocol.StateTargetSequence(
            [
                new FocusShiftRuntimeTarget("left", "left breath count"),
                new FocusShiftRuntimeTarget("right", "right breath count"),
            ]).IsAccepted);
        Assert.True(protocol.StartActiveSet().IsAccepted);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var cue1 = protocol.PresentValidCue("cue-1", "switch left", "left", RuntimeDuration.FromSeconds(2));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var response1 = protocol.RecordCueSwitch("cue-1", "left");
        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));
        var anticipation = protocol.RecordAnticipatorySwitch("right", "switched before cue");
        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));
        var cue2 = protocol.PresentValidCue("cue-2", "switch right", "right", RuntimeDuration.FromSeconds(2));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var response2 = protocol.RecordCueSwitch("cue-2", "left");
        var completed = protocol.CompleteSet("set-1");

        Assert.True(cue1.IsAccepted);
        Assert.True(response1.IsAccepted);
        Assert.True(anticipation.IsAccepted);
        Assert.True(cue2.IsAccepted);
        Assert.True(response2.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Equal(RuntimeEventKind.CueEmitted, cue1.Event!.Kind);
        Assert.Contains(cue1.Event.Facts, fact => fact.Name == "cue_kind" && fact.Value == "focus_shift");
        Assert.Contains(cue1.Event.Facts, fact => fact.Name == "expected_target_id" && fact.Value == "left");
        Assert.Contains(response1.Event!.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(response1.Event.Facts, fact => fact.Name == "sequence_position" && fact.Value == "1");
        Assert.Contains(response1.Event.Facts, fact => fact.Name == "sequence_accuracy_delta" && fact.Value == "correct");
        Assert.Contains(anticipation.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "premature_response");
        Assert.Contains(anticipation.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "switch_only_on_valid_cue");
        Assert.Contains(anticipation.Event.Facts, fact => fact.Name == "anticipatory_switch_count" && fact.Value == "1");
        Assert.Contains(response2.Event!.Facts, fact => fact.Name == "response_outcome" && fact.Value == "incorrect");
        Assert.Contains(response2.Event.Facts, fact => fact.Name == "failed_item_list" && fact.Value.Contains("expected right", StringComparison.Ordinal));
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "score" && fact.Value.Contains("sequence_accuracy=1/2", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "anticipatory_switch_count" && fact.Value == "1");

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.PrematureResponse);

        var failedEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "fs1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            protocol.EventLog.Events,
            scoringEvents));
        var bestEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "fs1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.BestSet,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            failedEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.FailedItemList &&
                evidence.Description.Contains("anticipatory switch", StringComparison.Ordinal));
        Assert.Contains(
            bestEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("sequence_accuracy=1/2", StringComparison.Ordinal));
        Assert.DoesNotContain(
            protocol.EventLog.Events.SelectMany(runtimeEvent => runtimeEvent.Facts),
            fact => fact.Name is "gate_outcome" or "pass_state");
    }

    [Fact]
    public void InvalidCueFilterRecordsIgnoredInvalidCuesCommissionsAndAccuracyFacts()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = FocusShiftRuntimeProtocol.Start(
            "fs2-session",
            FocusShiftSession(DrillId.FS2InvalidCueFilter, GlobalLevelId.L3),
            clock);

        Assert.True(protocol.StateTargetSequence(
            [
                new FocusShiftRuntimeTarget("alpha", "alpha item"),
                new FocusShiftRuntimeTarget("beta", "beta item"),
            ]).IsAccepted);
        Assert.True(protocol.StartActiveSet().IsAccepted);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(4));
        var validCue = protocol.PresentValidCue("cue-valid-1", "switch alpha", "alpha", RuntimeDuration.FromSeconds(2));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var validResponse = protocol.RecordCueSwitch("cue-valid-1", "alpha");
        clock.AdvanceBy(RuntimeDuration.FromSeconds(3));
        var invalidCue1 = protocol.PresentInvalidCue("cue-invalid-1", "ignore red flash", RuntimeDuration.FromSeconds(2));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));
        var ignored = protocol.RecordInvalidCueIgnored("cue-invalid-1");
        clock.AdvanceBy(RuntimeDuration.FromSeconds(3));
        var invalidCue2 = protocol.PresentInvalidCue("cue-invalid-2", "ignore blue flash", RuntimeDuration.FromSeconds(2));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var invalidResponse = protocol.RecordInvalidCueSwitch("cue-invalid-2", "beta");
        var completed = protocol.CompleteSet("set-1");

        Assert.True(validCue.IsAccepted);
        Assert.True(validResponse.IsAccepted);
        Assert.True(invalidCue1.IsAccepted);
        Assert.True(ignored.IsAccepted);
        Assert.True(invalidCue2.IsAccepted);
        Assert.True(invalidResponse.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(invalidCue1.Event!.Facts, fact => fact.Name == "cue_kind" && fact.Value == "invalid_cue_filter");
        Assert.Contains(invalidCue1.Event.Facts, fact => fact.Name == "response_expectation" && fact.Value == "no_response_expected");
        Assert.Contains(ignored.Event!.Facts, fact => fact.Name == "invalid_cue_result" && fact.Value == "ignored");
        Assert.Contains(ignored.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(invalidResponse.Event!.Facts, fact => fact.Name == "response_outcome" && fact.Value == "incorrect");
        Assert.Contains(invalidResponse.Event.Facts, fact => fact.Name == "expected_response" && fact.Value == "withhold");
        Assert.Contains(invalidResponse.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "invalid_cue_must_not_trigger_switch");
        Assert.Contains(invalidResponse.Event.Facts, fact => fact.Name == "failed_item_list" && fact.Value.Contains("invalid cue triggered switch", StringComparison.Ordinal));
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "score" && fact.Value.Contains("valid_sequence_accuracy=1/1", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "invalid_cue_filter_accuracy" && fact.Value == "1/2");

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.Commission);

        var failedEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "fs2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            protocol.EventLog.Events,
            scoringEvents));
        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "fs2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            failedEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.FailedItemList &&
                evidence.Description.Contains("invalid cue triggered switch", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.CriticalConstraintRecord &&
                evidence.Description.Contains("Invalid cues must not trigger switch", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("invalid_cue_filter_accuracy=1/2", StringComparison.Ordinal));
    }

    [Fact]
    public void FocusShiftProtocolRejectsWrongSessionsAndInvalidCuesForCueSwitchWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);

        Assert.Throws<ArgumentException>(() => FocusShiftRuntimeProtocol.Start(
            "wrong-branch",
            FocusHoldSession(),
            clock));

        var cueSwitch = FocusShiftRuntimeProtocol.Start(
            "fs1-no-invalid",
            FocusShiftSession(DrillId.FS1CueSwitch, GlobalLevelId.L1),
            clock);
        Assert.True(cueSwitch.StateTargetSequence(
            [
                new FocusShiftRuntimeTarget("left", "left"),
                new FocusShiftRuntimeTarget("right", "right"),
            ]).IsAccepted);
        Assert.True(cueSwitch.StartActiveSet().IsAccepted);
        var eventCountBeforeInvalidCue = cueSwitch.EventLog.Events.Count;

        var rejectedInvalidCue = cueSwitch.PresentInvalidCue(
            "invalid-cue",
            "not part of FS-1",
            RuntimeDuration.FromSeconds(2));

        Assert.False(rejectedInvalidCue.IsAccepted);
        Assert.Equal(FocusShiftRuntimeProtocolInvalidReason.InvalidCuesNotSupportedByDrill, rejectedInvalidCue.InvalidReason);
        Assert.Equal(eventCountBeforeInvalidCue, cueSwitch.EventLog.Events.Count);
    }

    private static RuntimeSessionDefinition FocusShiftSession(DrillId drill, GlobalLevelId level)
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.FS &&
            standard.Level == level);
        CriticalConstraint[] constraints = drill == DrillId.FS2InvalidCueFilter
            ?
            [
                new CriticalConstraint("Switch only on valid cue."),
                new CriticalConstraint("Invalid cues must not trigger switch."),
                new CriticalConstraint("No anticipatory switching."),
            ]
            :
            [
                new CriticalConstraint("Switch only on valid cue."),
                new CriticalConstraint("No anticipatory switching."),
            ];
        LoadVariable[] loadVariables = drill == DrillId.FS2InvalidCueFilter
            ? [new LoadVariable("valid cue density", "1 cue"), new LoadVariable("invalid cue ratio", "2 invalid cues")]
            : [new LoadVariable("cue density", "2 cues"), new LoadVariable("target count", "2 targets")];

        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FS,
            level,
            drill,
            loadVariables,
            standard,
            constraints);
    }

    private static RuntimeSessionDefinition FocusHoldSession()
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.FH &&
            standard.Level == GlobalLevelId.L1);

        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")],
            standard,
            [new CriticalConstraint("Target is stated before set; every drift is marked.")]);
    }
}
