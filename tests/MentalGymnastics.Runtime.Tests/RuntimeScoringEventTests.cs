using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeScoringEventTests
{
    [Fact]
    public void ScoringEventsClassifyCueResponsesAndResponseTimingEvidence()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var generatedInstance = CueGeneratedInstance();
        var log = RuntimeEventLog.Start(
            "session-scoring-cues",
            CreateSessionDefinition(generatedInstance),
            clock.Now);
        var scheduler = new RuntimeCueScheduler(
            new RuntimeCueSchedule(
                generatedInstance,
                [
                    Cue("cue-correct", RuntimeCueKind.FocusShift, "left", 1, RuntimeCueResponseExpectation.ResponseRequired, "left"),
                    Cue("cue-incorrect", RuntimeCueKind.FocusShift, "right", 3, RuntimeCueResponseExpectation.ResponseRequired, "right"),
                    Cue("cue-commission", RuntimeCueKind.InvalidCueFilter, "ignore-red", 5, RuntimeCueResponseExpectation.NoResponseExpected),
                ]),
            clock,
            log);
        var phase = RuntimeSessionPhaseDefinition.Manual("cue-window", RuntimeSessionPhaseKind.CueResponse);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        scheduler.AdvanceToCurrentTime(phase);
        var correctResponse = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-correct", "left"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));
        scheduler.AdvanceToCurrentTime(phase);
        var incorrectResponse = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-incorrect", "left"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));
        scheduler.AdvanceToCurrentTime(phase);
        var commissionResponse = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-commission", "switch-anyway"));

        var correct = RuntimeScoringEventFactory.FromRuntimeEvent(correctResponse.Event!)!;
        var incorrect = RuntimeScoringEventFactory.FromRuntimeEvent(incorrectResponse.Event!)!;
        var commission = RuntimeScoringEventFactory.FromRuntimeEvent(commissionResponse.Event!)!;
        var premature = RuntimeScoringEventFactory.FromResponseTiming(
            new RuntimeResponseWindow(
                "cue-premature",
                RuntimeDuration.FromSeconds(10).ToInstant(),
                RuntimeDuration.FromSeconds(2),
                "cue:cue-premature").RecordResponse(RuntimeDuration.FromSeconds(9).ToInstant()),
            RuntimeCueResponseExpectation.ResponseRequired)!;
        var late = RuntimeScoringEventFactory.FromResponseTiming(
            new RuntimeResponseWindow(
                "cue-late",
                RuntimeDuration.FromSeconds(10).ToInstant(),
                RuntimeDuration.FromSeconds(2),
                "cue:cue-late").RecordResponse(RuntimeDuration.FromSeconds(13).ToInstant()),
            RuntimeCueResponseExpectation.ResponseRequired)!;
        var omission = RuntimeScoringEventFactory.FromResponseTiming(
            new RuntimeResponseWindow(
                "cue-omission",
                RuntimeDuration.FromSeconds(10).ToInstant(),
                RuntimeDuration.FromSeconds(2),
                "cue:cue-omission").EvaluateNoResponse(RuntimeDuration.FromSeconds(13).ToInstant()),
            RuntimeCueResponseExpectation.ResponseRequired)!;

        Assert.Equal(RuntimeScoringEventKind.CorrectResponse, correct.Kind);
        Assert.Equal(RuntimeScoringEventKind.IncorrectResponse, incorrect.Kind);
        Assert.Equal(RuntimeScoringEventKind.Commission, commission.Kind);
        Assert.Equal(RuntimeScoringEventKind.PrematureResponse, premature.Kind);
        Assert.Equal(RuntimeScoringEventKind.LateResponse, late.Kind);
        Assert.Equal(RuntimeScoringEventKind.Omission, omission.Kind);

        Assert.Contains(correct.EvidenceFacts, fact => fact.Name == "scoring_event_kind" && fact.Value == "correct_response");
        Assert.Contains(correct.EvidenceFacts, fact => fact.Name == "cue_id" && fact.Value == "cue-correct");
        Assert.Contains(incorrect.EvidenceFacts, fact => fact.Name == "response_outcome" && fact.Value == "incorrect");
        Assert.Contains(commission.EvidenceFacts, fact => fact.Name == "expected_response" && fact.Value == "withhold");
        Assert.Contains(premature.EvidenceFacts, fact => fact.Name == "timing_outcome" && fact.Value == "early");
        Assert.Contains(late.EvidenceFacts, fact => fact.Name == "late_by" && fact.Value == "00:00:01");
        Assert.Contains(omission.EvidenceFacts, fact => fact.Name == "timing_outcome" && fact.Value == "missed");

        Assert.All(
            [correct, incorrect, commission, premature, late, omission],
            scoringEvent =>
            {
                Assert.DoesNotContain(scoringEvent.EvidenceFacts, fact => fact.Name == "gate_outcome");
                Assert.DoesNotContain(scoringEvent.EvidenceFacts, fact => fact.Name == "pass_state");
            });
    }

    [Fact]
    public void ScoringEventsClassifyDriftGuessCorrectionUnmarkedDriftAndTimeoutFromRuntimeEvents()
    {
        var log = RuntimeEventLog.Start(
            "session-scoring-events",
            CreateSessionDefinition(CueGeneratedInstance()),
            RuntimeInstant.Zero);
        var markedDriftEvent = log.Append(
            RuntimeEventKind.DriftMarked,
            RuntimeDuration.FromSeconds(1).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [new RuntimeEventFact("drift_id", "drift-1")]);
        var markedGuessEvent = log.Append(
            RuntimeEventKind.GuessMarked,
            RuntimeDuration.FromSeconds(2).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [new RuntimeEventFact("guess_id", "guess-1")]);
        var correctionEvent = log.Append(
            RuntimeEventKind.CorrectionSubmitted,
            RuntimeDuration.FromSeconds(3).ToInstant(),
            "audit",
            RuntimeSessionPhaseKind.Audit,
            [new RuntimeEventFact("correction_id", "correction-1")]);
        var unmarkedDriftEvent = log.Append(
            RuntimeEventKind.ErrorRecorded,
            RuntimeDuration.FromSeconds(4).ToInstant(),
            "audit",
            RuntimeSessionPhaseKind.Audit,
            [
                new RuntimeEventFact("error_kind", "unmarked_drift"),
                new RuntimeEventFact("detection_method", "post_set_audit"),
            ]);
        var timeoutEvent = log.Append(
            RuntimeEventKind.PhaseTimedOut,
            RuntimeDuration.FromSeconds(5).ToInstant(),
            "delay",
            RuntimeSessionPhaseKind.DelayWindow,
            [
                new RuntimeEventFact("phase_deadline", "00:00:04"),
                new RuntimeEventFact("timeout_overtime", "00:00:01"),
            ]);

        var markedDrift = RuntimeScoringEventFactory.FromRuntimeEvent(markedDriftEvent)!;
        var markedGuess = RuntimeScoringEventFactory.FromRuntimeEvent(markedGuessEvent)!;
        var correction = RuntimeScoringEventFactory.FromRuntimeEvent(correctionEvent)!;
        var unmarkedDrift = RuntimeScoringEventFactory.FromRuntimeEvent(unmarkedDriftEvent)!;
        var timeout = RuntimeScoringEventFactory.FromRuntimeEvent(timeoutEvent)!;

        Assert.Equal(RuntimeScoringEventKind.MarkedDrift, markedDrift.Kind);
        Assert.Equal(RuntimeScoringEventKind.MarkedGuess, markedGuess.Kind);
        Assert.Equal(RuntimeScoringEventKind.Correction, correction.Kind);
        Assert.Equal(RuntimeScoringEventKind.UnmarkedDrift, unmarkedDrift.Kind);
        Assert.Equal(RuntimeScoringEventKind.Timeout, timeout.Kind);

        Assert.Contains(markedDrift.EvidenceFacts, fact => fact.Name == "drift_id" && fact.Value == "drift-1");
        Assert.Contains(markedGuess.EvidenceFacts, fact => fact.Name == "guess_id" && fact.Value == "guess-1");
        Assert.Contains(correction.EvidenceFacts, fact => fact.Name == "correction_id" && fact.Value == "correction-1");
        Assert.Contains(unmarkedDrift.EvidenceFacts, fact => fact.Name == "detection_method" && fact.Value == "post_set_audit");
        Assert.Contains(timeout.EvidenceFacts, fact => fact.Name == "timeout_overtime" && fact.Value == "00:00:01");
        Assert.Contains(timeout.EvidenceFacts, fact => fact.Name == "scoring_event_kind" && fact.Value == "timeout");
    }

    [Fact]
    public void ScoringEventsRejectMissingSourcesAndProgressionDecisionFacts()
    {
        Assert.Throws<ArgumentException>(() => new RuntimeScoringEvent(
            " ",
            RuntimeScoringEventKind.CorrectResponse,
            RuntimeInstant.Zero,
            "runtime_event:1"));
        Assert.Throws<ArgumentException>(() => new RuntimeScoringEvent(
            "runtime_event:1",
            RuntimeScoringEventKind.CorrectResponse,
            RuntimeInstant.Zero,
            " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeScoringEvent(
            "runtime_event:1",
            (RuntimeScoringEventKind)999,
            RuntimeInstant.Zero,
            "runtime_event:1"));
        Assert.Throws<ArgumentException>(() => new RuntimeScoringEvent(
            "runtime_event:1",
            RuntimeScoringEventKind.CorrectResponse,
            RuntimeInstant.Zero,
            "runtime_event:1",
            [new RuntimeEventFact("gate_outcome", "pass_once")]));
        Assert.Throws<ArgumentException>(() => new RuntimeScoringEvent(
            "runtime_event:1",
            RuntimeScoringEventKind.CorrectResponse,
            RuntimeInstant.Zero,
            "runtime_event:1",
            [new RuntimeEventFact("pass_state", "owned")]));
    }

    private static RuntimeScheduledCue Cue(
        string id,
        RuntimeCueKind kind,
        string cue,
        int scheduledAtSeconds,
        RuntimeCueResponseExpectation expectation,
        string? expectedResponse = null)
    {
        return new RuntimeScheduledCue(
            id,
            kind,
            cue,
            RuntimeDuration.FromSeconds(scheduledAtSeconds).ToInstant(),
            RuntimeDuration.FromSeconds(1),
            expectation,
            expectedResponse);
    }

    private static RuntimeGeneratedDrillInstanceIdentity CueGeneratedInstance()
    {
        return new RuntimeGeneratedDrillInstanceIdentity(
            "generated-scoring-cues",
            new PromptContentIdentity(
                "content-scoring-cues",
                BranchCode.FS,
                GlobalLevelId.L1,
                DrillId.FS1CueSwitch,
                PromptContentKind.CueSequence,
                "fs-l1-cue-density"),
            "v1");
    }

    private static RuntimeSessionDefinition CreateSessionDefinition(
        RuntimeGeneratedDrillInstanceIdentity generatedInstance)
    {
        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FS,
            GlobalLevelId.L1,
            DrillId.FS1CueSwitch,
            [new LoadVariable("cue density", "3 cues in 5 seconds")],
            new BranchLevelStandard(
                BranchCode.FS,
                GlobalLevelId.L1,
                "Alternate between two targets on cue for 4 minutes.",
                "At least 90% correct cue responses; no more than 3 anticipatory switches.",
                "FH L1 passed once.",
                "Repeat twice; one after FH hold.",
                "Use a new pair of targets."),
            [new CriticalConstraint("Switch only on valid cue.")],
            generatedInstance);
    }
}
