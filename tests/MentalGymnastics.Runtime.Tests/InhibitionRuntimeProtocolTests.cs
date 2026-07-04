using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class InhibitionRuntimeProtocolTests
{
    [Fact]
    public void GoNoGoRuleRecordsRuleCuePaceNoGoHandlingPrematureResponseAndCascadeFacts()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = InhibitionRuntimeProtocol.Start(
            "ir1-session",
            InhibitionSession(DrillId.IR1GoNoGoRule, GlobalLevelId.L1),
            clock);

        var activeBeforeRule = protocol.StartActiveSet(RuntimeDuration.FromSeconds(2));

        Assert.False(activeBeforeRule.IsAccepted);
        Assert.Equal(InhibitionRuntimeProtocolInvalidReason.RuleRequiredBeforeSet, activeBeforeRule.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        var rule = protocol.StateRule("Press for green cues; withhold for red cues.");
        var active = protocol.StartActiveSet(RuntimeDuration.FromSeconds(2));

        Assert.True(rule.IsAccepted);
        Assert.True(active.IsAccepted);
        Assert.Contains(rule.Event!.Facts, fact => fact.Name == "rule_stated_before_set" && fact.Value == "true");
        Assert.Contains(active.Event!.Facts, fact => fact.Name == "cue_pace" && fact.Value == "00:00:02");

        var goCue = protocol.PresentGoCue("cue-1", "green", "press", RuntimeDuration.FromSeconds(1));
        clock.AdvanceBy(Milliseconds(500));
        var goResponse = protocol.RecordGoResponse("cue-1", "press");
        clock.AdvanceBy(Milliseconds(1500));
        var noGoCue = protocol.PresentNoGoCue("cue-2", "red", RuntimeDuration.FromSeconds(1));
        clock.AdvanceBy(Milliseconds(500));
        var noGoWithheld = protocol.RecordNoGoWithheld("cue-2");
        var premature = protocol.RecordPrematureResponse("cue-3", "pressed before cue");
        var cascade = protocol.RecordPostErrorCascade(
            "cascade-1",
            affectedItemCount: 2,
            "missed the next two cue decisions after the premature response");
        var completed = protocol.CompleteSet("set-1");

        Assert.True(goCue.IsAccepted);
        Assert.True(goResponse.IsAccepted);
        Assert.True(noGoCue.IsAccepted);
        Assert.True(noGoWithheld.IsAccepted);
        Assert.True(premature.IsAccepted);
        Assert.True(cascade.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(goCue.Event!.Facts, fact => fact.Name == "cue_kind" && fact.Value == "go");
        Assert.Contains(goCue.Event.Facts, fact => fact.Name == "cue_pace" && fact.Value == "00:00:02");
        Assert.Contains(goResponse.Event!.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(noGoCue.Event!.Facts, fact => fact.Name == "cue_kind" && fact.Value == "no_go");
        Assert.Contains(noGoCue.Event.Facts, fact => fact.Name == "expected_response" && fact.Value == "withhold");
        Assert.Contains(noGoWithheld.Event!.Facts, fact => fact.Name == "no_go_result" && fact.Value == "withheld");
        Assert.Contains(noGoWithheld.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(premature.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "premature_response");
        Assert.Contains(premature.Event.Facts, fact => fact.Name == "item_failed" && fact.Value == "true");
        Assert.Contains(premature.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "premature_response_fails_item");
        Assert.Contains(cascade.Event!.Facts, fact => fact.Name == "post_error_cascade_count" && fact.Value == "1");
        Assert.Contains(cascade.Event.Facts, fact => fact.Name == "affected_item_count" && fact.Value == "2");
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "score" && fact.Value.Contains("accuracy=2/3", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "premature_response_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "post_error_cascade_count" && fact.Value == "1");

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.PrematureResponse);

        var failedEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "ir1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            protocol.EventLog.Events,
            scoringEvents));
        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "ir1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            failedEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.FailedItemList &&
                evidence.Description.Contains("pressed before cue", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("premature_response_count=1", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.CriticalConstraintRecord &&
                evidence.Description.Contains("Premature response fails item", StringComparison.Ordinal));
        Assert.DoesNotContain(
            protocol.EventLog.Events.SelectMany(runtimeEvent => runtimeEvent.Facts),
            fact => fact.Name is "gate_outcome" or "pass_state");
    }

    [Fact]
    public void ExceptionRuleRecordsPreStatedExceptionsExceptionHandlingRuleChangeAndCorrectionFacts()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = InhibitionRuntimeProtocol.Start(
            "ir2-session",
            InhibitionSession(DrillId.IR2ExceptionRule, GlobalLevelId.L2),
            clock);

        Assert.True(protocol.StateRule(
            "Press for symbols unless the red-border exception appears.",
            [new InhibitionRuntimeExceptionRule("red-border", "Red border means withhold.", "withhold")]).IsAccepted);
        Assert.True(protocol.StartActiveSet(RuntimeDuration.FromSeconds(1)).IsAccepted);

        var exceptionCue1 = protocol.PresentExceptionCue(
            "cue-ex-1",
            "red triangle",
            "red-border",
            Milliseconds(800));
        clock.AdvanceBy(Milliseconds(500));
        var exceptionHandled = protocol.RecordExceptionResponse("cue-ex-1", "withhold");
        clock.AdvanceBy(Milliseconds(500));
        var ruleChange = protocol.RecordRuleChange("changed to press for every symbol");
        var exceptionCue2 = protocol.PresentExceptionCue(
            "cue-ex-2",
            "red square",
            "red-border",
            Milliseconds(800));
        clock.AdvanceBy(Milliseconds(500));
        var exceptionForgotten = protocol.RecordExceptionResponse("cue-ex-2", "press");
        var correction = protocol.RecordCorrection(
            "cue-ex-2",
            itemsAfterError: 2,
            "Restated the red-border exception before the third following item.");
        var completed = protocol.CompleteSet("set-1");

        Assert.True(exceptionCue1.IsAccepted);
        Assert.True(exceptionHandled.IsAccepted);
        Assert.True(ruleChange.IsAccepted);
        Assert.True(exceptionCue2.IsAccepted);
        Assert.True(exceptionForgotten.IsAccepted);
        Assert.True(correction.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(exceptionCue1.Event!.Facts, fact => fact.Name == "cue_kind" && fact.Value == "exception");
        Assert.Contains(exceptionCue1.Event.Facts, fact => fact.Name == "exception_id" && fact.Value == "red-border");
        Assert.Contains(exceptionHandled.Event!.Facts, fact => fact.Name == "exception_result" && fact.Value == "handled");
        Assert.Contains(exceptionHandled.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(ruleChange.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "rule_changed_mid_set");
        Assert.Contains(ruleChange.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "rule_statement_before_set");
        Assert.Contains(exceptionForgotten.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "exception_forgotten");
        Assert.Contains(exceptionForgotten.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "exceptions_stated_before_set");
        Assert.Contains(correction.Event!.Facts, fact => fact.Name == "correction_within_required_items" && fact.Value == "true");
        Assert.Contains(correction.Event.Facts, fact => fact.Name == "items_after_error" && fact.Value == "2");
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "exception_accuracy" && fact.Value == "1/2");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "rule_change_count" && fact.Value == "1");

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.Correction);

        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "ir2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.RuleExplanation &&
                evidence.Description.Contains("red-border exception", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("exception_accuracy=1/2", StringComparison.Ordinal));
    }

    [Fact]
    public void InhibitionProtocolRejectsWrongSessionsAndExceptionCueForGoNoGoWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);

        Assert.Throws<ArgumentException>(() => InhibitionRuntimeProtocol.Start(
            "wrong-branch",
            FocusHoldSession(),
            clock));

        var goNoGo = InhibitionRuntimeProtocol.Start(
            "ir1-no-exceptions",
            InhibitionSession(DrillId.IR1GoNoGoRule, GlobalLevelId.L1),
            clock);
        Assert.True(goNoGo.StateRule("Press for green; withhold for red.").IsAccepted);
        Assert.True(goNoGo.StartActiveSet(RuntimeDuration.FromSeconds(2)).IsAccepted);
        var eventCountBeforeExceptionCue = goNoGo.EventLog.Events.Count;

        var exceptionCue = goNoGo.PresentExceptionCue(
            "cue-exception",
            "not part of IR-1",
            "red-border",
            RuntimeDuration.FromSeconds(1));

        Assert.False(exceptionCue.IsAccepted);
        Assert.Equal(
            InhibitionRuntimeProtocolInvalidReason.ExceptionRuleNotSupportedByDrill,
            exceptionCue.InvalidReason);
        Assert.Equal(eventCountBeforeExceptionCue, goNoGo.EventLog.Events.Count);
    }

    private static RuntimeSessionDefinition InhibitionSession(DrillId drill, GlobalLevelId level)
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.IR &&
            standard.Level == level);
        CriticalConstraint[] constraints = drill == DrillId.IR2ExceptionRule
            ?
            [
                new CriticalConstraint("Rule and exceptions stated before set."),
                new CriticalConstraint("Premature response fails item."),
            ]
            :
            [
                new CriticalConstraint("Rule must be stated before set."),
                new CriticalConstraint("Premature response fails item."),
            ];
        LoadVariable[] loadVariables = drill == DrillId.IR2ExceptionRule
            ?
            [
                new LoadVariable("cue pace", "1 second"),
                new LoadVariable("exception count", "1"),
            ]
            :
            [
                new LoadVariable("cue pace", "2 seconds"),
                new LoadVariable("no-go frequency", "1 of 2 cues"),
            ];

        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.IR,
            level,
            drill,
            loadVariables,
            standard,
            constraints);
    }

    private static RuntimeDuration Milliseconds(int milliseconds)
    {
        return new RuntimeDuration(TimeSpan.FromMilliseconds(milliseconds));
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
