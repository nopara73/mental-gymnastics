using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeCueSchedulerTests
{
    [Fact]
    public void SchedulerEmitsGeneratedCueScheduleAtExpectedTimes()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var generatedInstance = CueGeneratedInstance();
        var log = RuntimeEventLog.Start(
            "session-cues",
            CreateSessionDefinition(generatedInstance),
            clock.Now);
        var schedule = new RuntimeCueSchedule(
            generatedInstance,
            [
                Cue("cue-1", RuntimeCueKind.FocusShift, "left", 5, RuntimeCueResponseExpectation.ResponseRequired, "left"),
                Cue("cue-2", RuntimeCueKind.InvalidCueFilter, "ignore-red", 8, RuntimeCueResponseExpectation.NoResponseExpected),
                Cue("cue-3", RuntimeCueKind.GoNoGo, "go", 12, RuntimeCueResponseExpectation.ResponseRequired, "tap"),
                Cue("cue-4", RuntimeCueKind.Interruption, "noise", 16, RuntimeCueResponseExpectation.ResponseRequired, "resume"),
                Cue("cue-5", RuntimeCueKind.TimedResponse, "now", 20, RuntimeCueResponseExpectation.ResponseRequired, "hit"),
            ]);
        var scheduler = new RuntimeCueScheduler(schedule, clock, log);
        var phase = RuntimeSessionPhaseDefinition.Manual("cue-window", RuntimeSessionPhaseKind.CueResponse);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(4));
        var early = scheduler.AdvanceToCurrentTime(phase);

        Assert.Empty(early.EmittedCues);
        Assert.Empty(early.Events);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var first = scheduler.AdvanceToCurrentTime(phase);

        Assert.Collection(first.EmittedCues, cue => Assert.Equal("cue-1", cue.Id));
        Assert.Collection(first.Events, EventHasCue("cue-1", RuntimeCueKind.FocusShift, TimeSpan.FromSeconds(5), generatedInstance.InstanceId));
        Assert.Equal(RuntimeEventKind.CueEmitted, log.Events[^1].Kind);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(15));
        var remaining = scheduler.AdvanceToCurrentTime(phase);

        Assert.Equal(["cue-2", "cue-3", "cue-4", "cue-5"], remaining.EmittedCues.Select(cue => cue.Id));
        Assert.Equal(5, scheduler.EmittedCues.Count);
        Assert.Collection(
            remaining.Events,
            EventHasCue("cue-2", RuntimeCueKind.InvalidCueFilter, TimeSpan.FromSeconds(20), generatedInstance.InstanceId),
            EventHasCue("cue-3", RuntimeCueKind.GoNoGo, TimeSpan.FromSeconds(20), generatedInstance.InstanceId),
            EventHasCue("cue-4", RuntimeCueKind.Interruption, TimeSpan.FromSeconds(20), generatedInstance.InstanceId),
            EventHasCue("cue-5", RuntimeCueKind.TimedResponse, TimeSpan.FromSeconds(20), generatedInstance.InstanceId));
        Assert.Contains(remaining.Events[0].Facts, fact => fact.Name == "scheduled_at" && fact.Value == "00:00:08");
        Assert.Contains(remaining.Events[0].Facts, fact => fact.Name == "response_deadline" && fact.Value == "00:00:22");
    }

    [Fact]
    public void SchedulerEvaluatesResponsesAgainstActiveCueContext()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var generatedInstance = CueGeneratedInstance();
        var log = RuntimeEventLog.Start(
            "session-cue-responses",
            CreateSessionDefinition(generatedInstance),
            clock.Now);
        var scheduler = new RuntimeCueScheduler(
            new RuntimeCueSchedule(
                generatedInstance,
                [
                    Cue("cue-1", RuntimeCueKind.FocusShift, "left", 5, RuntimeCueResponseExpectation.ResponseRequired, "left"),
                    Cue("cue-2", RuntimeCueKind.InvalidCueFilter, "ignore-red", 8, RuntimeCueResponseExpectation.NoResponseExpected),
                    Cue("cue-3", RuntimeCueKind.TimedResponse, "now", 12, RuntimeCueResponseExpectation.ResponseRequired, "hit", responseWindowSeconds: 1),
                ]),
            clock,
            log);
        var phase = RuntimeSessionPhaseDefinition.Manual("cue-window", RuntimeSessionPhaseKind.CueResponse);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        scheduler.AdvanceToCurrentTime(phase);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var correct = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-1", "left"));

        Assert.True(correct.IsAccepted);
        Assert.Equal(RuntimeCueResponseOutcome.Correct, correct.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(1), correct.ResponseTime?.Value);
        Assert.NotNull(correct.Event);
        Assert.Contains(correct.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(correct.Event.Facts, fact => fact.Name == "response_time" && fact.Value == "00:00:01");

        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));
        scheduler.AdvanceToCurrentTime(phase);
        var invalidCueResponse = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-2", "switch-anyway"));

        Assert.True(invalidCueResponse.IsAccepted);
        Assert.Equal(RuntimeCueResponseOutcome.Incorrect, invalidCueResponse.Outcome);
        Assert.Contains(invalidCueResponse.Event!.Facts, fact => fact.Name == "expected_response" && fact.Value == "withhold");
        Assert.Contains(invalidCueResponse.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "incorrect");

        clock.AdvanceBy(RuntimeDuration.FromSeconds(4));
        scheduler.AdvanceToCurrentTime(phase);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));
        var late = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-3", "hit"));

        Assert.True(late.IsAccepted);
        Assert.Equal(RuntimeCueResponseOutcome.Late, late.Outcome);
        Assert.Contains(late.Event!.Facts, fact => fact.Name == "within_window" && fact.Value == "false");
        Assert.Contains(late.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "late");
    }

    [Fact]
    public void SchedulerPauseAndResumePreservePendingCuesAndEvidenceHistory()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var generatedInstance = CueGeneratedInstance();
        var log = RuntimeEventLog.Start(
            "session-cue-pause",
            CreateSessionDefinition(generatedInstance),
            clock.Now);
        var scheduler = new RuntimeCueScheduler(
            new RuntimeCueSchedule(
                generatedInstance,
                [
                    Cue("cue-1", RuntimeCueKind.FocusShift, "left", 5, RuntimeCueResponseExpectation.ResponseRequired, "left"),
                    Cue("cue-2", RuntimeCueKind.TimedResponse, "now", 10, RuntimeCueResponseExpectation.ResponseRequired, "hit"),
                ]),
            clock,
            log);
        var phase = RuntimeSessionPhaseDefinition.Manual("cue-window", RuntimeSessionPhaseKind.CueResponse);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(4));
        scheduler.Pause();
        clock.AdvanceBy(RuntimeDuration.FromSeconds(20));
        var whilePaused = scheduler.AdvanceToCurrentTime(phase);

        Assert.Empty(whilePaused.EmittedCues);
        Assert.Empty(whilePaused.Events);
        Assert.Equal(["cue-1", "cue-2"], scheduler.PendingCues.Select(cue => cue.Id));
        Assert.Single(log.Events);

        scheduler.Resume();
        var immediatelyAfterResume = scheduler.AdvanceToCurrentTime(phase);

        Assert.Empty(immediatelyAfterResume.EmittedCues);
        Assert.Single(log.Events);
        Assert.Equal(["cue-1", "cue-2"], scheduler.PendingCues.Select(cue => cue.Id));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var firstDueAfterResume = scheduler.AdvanceToCurrentTime(phase);

        Assert.Collection(firstDueAfterResume.EmittedCues, cue => Assert.Equal("cue-1", cue.Id));
        Assert.Equal(["cue-2"], scheduler.PendingCues.Select(cue => cue.Id));
        Assert.Collection(
            firstDueAfterResume.Events,
            runtimeEvent =>
            {
                Assert.Equal(RuntimeEventKind.CueEmitted, runtimeEvent.Kind);
                Assert.Equal(TimeSpan.FromSeconds(25), runtimeEvent.OccurredAt.Offset);
                Assert.Contains(runtimeEvent.Facts, fact => fact.Name == "cue_id" && fact.Value == "cue-1");
                Assert.Contains(runtimeEvent.Facts, fact => fact.Name == "scheduled_at" && fact.Value == "00:00:05");
            });
    }

    [Fact]
    public void CueEmissionEvidenceUsesActualPresentationTimeForResponseDeadline()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var generatedInstance = CueGeneratedInstance();
        var log = RuntimeEventLog.Start(
            "session-cue-deadline",
            CreateSessionDefinition(generatedInstance),
            clock.Now);
        var scheduler = new RuntimeCueScheduler(
            new RuntimeCueSchedule(
                generatedInstance,
                [
                    Cue("cue-1", RuntimeCueKind.FocusShift, "left", 5, RuntimeCueResponseExpectation.ResponseRequired, "left"),
                ]),
            clock,
            log);
        var phase = RuntimeSessionPhaseDefinition.Manual("cue-window", RuntimeSessionPhaseKind.CueResponse);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(20));
        var emitted = scheduler.AdvanceToCurrentTime(phase);

        var cueEvent = Assert.Single(emitted.Events);
        Assert.Contains(cueEvent.Facts, fact => fact.Name == "scheduled_at" && fact.Value == "00:00:05");
        Assert.Contains(cueEvent.Facts, fact => fact.Name == "presented_at" && fact.Value == "00:00:20");
        Assert.Contains(cueEvent.Facts, fact => fact.Name == "response_deadline" && fact.Value == "00:00:22");
    }

    [Fact]
    public void SchedulerRejectsResponsesWithoutActiveGeneratedCueContextWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var generatedInstance = CueGeneratedInstance();
        var log = RuntimeEventLog.Start(
            "session-cue-invalid",
            CreateSessionDefinition(generatedInstance),
            clock.Now);
        var scheduler = new RuntimeCueScheduler(
            new RuntimeCueSchedule(
                generatedInstance,
                [Cue("cue-1", RuntimeCueKind.GoNoGo, "go", 5, RuntimeCueResponseExpectation.ResponseRequired, "tap")]),
            clock,
            log);
        var phase = RuntimeSessionPhaseDefinition.Manual("cue-window", RuntimeSessionPhaseKind.CueResponse);

        var eventCountBeforeEarlyResponse = log.Events.Count;
        var beforeCue = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-1", "tap"));

        Assert.False(beforeCue.IsAccepted);
        Assert.Equal(RuntimeCueResponseInvalidReason.CueNotPresented, beforeCue.InvalidReason);
        Assert.Equal(eventCountBeforeEarlyResponse, log.Events.Count);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        scheduler.AdvanceToCurrentTime(phase);
        var response = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-1", "tap"));
        var eventCountAfterResponse = log.Events.Count;
        var duplicate = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-1", "tap"));
        var unknown = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-missing", "tap"));

        Assert.True(response.IsAccepted);
        Assert.False(duplicate.IsAccepted);
        Assert.Equal(RuntimeCueResponseInvalidReason.CueAlreadyResponded, duplicate.InvalidReason);
        Assert.False(unknown.IsAccepted);
        Assert.Equal(RuntimeCueResponseInvalidReason.UnknownCue, unknown.InvalidReason);
        Assert.Equal(eventCountAfterResponse, log.Events.Count);
    }

    [Fact]
    public void CueScheduleRequiresGeneratedCueSequenceInstanceData()
    {
        var generatedInstance = CueGeneratedInstance();
        var wrongKind = new RuntimeGeneratedDrillInstanceIdentity(
            "generated-equivalent-1",
            new PromptContentIdentity(
                "content-equivalent-1",
                BranchCode.FS,
                GlobalLevelId.L1,
                DrillId.FS1CueSwitch,
                PromptContentKind.EquivalentPrompt,
                "fs-l1"),
            "v1");

        Assert.Throws<ArgumentException>(() => new RuntimeCueSchedule(
            wrongKind,
            [Cue("cue-1", RuntimeCueKind.FocusShift, "left", 5, RuntimeCueResponseExpectation.ResponseRequired, "left")]));
        Assert.Throws<ArgumentException>(() => new RuntimeCueSchedule(
            generatedInstance,
            [
                Cue("cue-1", RuntimeCueKind.FocusShift, "left", 5, RuntimeCueResponseExpectation.ResponseRequired, "left"),
                Cue("cue-1", RuntimeCueKind.FocusShift, "right", 8, RuntimeCueResponseExpectation.ResponseRequired, "right"),
            ]));
        Assert.Throws<ArgumentException>(() => new RuntimeScheduledCue(
            " ",
            RuntimeCueKind.FocusShift,
            "left",
            RuntimeDuration.FromSeconds(5).ToInstant(),
            RuntimeDuration.FromSeconds(2),
            RuntimeCueResponseExpectation.ResponseRequired,
            "left"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeScheduledCue(
            "cue-2",
            RuntimeCueKind.FocusShift,
            "left",
            RuntimeDuration.FromSeconds(5).ToInstant(),
            RuntimeDuration.Zero,
            RuntimeCueResponseExpectation.ResponseRequired,
            "left"));
    }

    private static Action<RuntimeEvent> EventHasCue(
        string expectedCueId,
        RuntimeCueKind expectedKind,
        TimeSpan expectedOccurredAt,
        string expectedGeneratedInstanceId)
    {
        return runtimeEvent =>
        {
            Assert.Equal(RuntimeEventKind.CueEmitted, runtimeEvent.Kind);
            Assert.Equal(expectedOccurredAt, runtimeEvent.OccurredAt.Offset);
            Assert.Contains(runtimeEvent.Facts, fact => fact.Name == "cue_id" && fact.Value == expectedCueId);
            Assert.Contains(runtimeEvent.Facts, fact => fact.Name == "cue_kind" && fact.Value == StableCueKind(expectedKind));
            Assert.Contains(runtimeEvent.Facts, fact => fact.Name == "generated_instance_id" && fact.Value == expectedGeneratedInstanceId);
        };
    }

    private static RuntimeScheduledCue Cue(
        string id,
        RuntimeCueKind kind,
        string cue,
        int scheduledAtSeconds,
        RuntimeCueResponseExpectation expectation,
        string? expectedResponse = null,
        int responseWindowSeconds = 2)
    {
        return new RuntimeScheduledCue(
            id,
            kind,
            cue,
            RuntimeDuration.FromSeconds(scheduledAtSeconds).ToInstant(),
            RuntimeDuration.FromSeconds(responseWindowSeconds),
            expectation,
            expectedResponse);
    }

    private static RuntimeGeneratedDrillInstanceIdentity CueGeneratedInstance()
    {
        return new RuntimeGeneratedDrillInstanceIdentity(
            "generated-cue-sequence-1",
            new PromptContentIdentity(
                "content-cue-sequence-1",
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
            [new LoadVariable("cue density", "5 cues in 20 seconds")],
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

    private static string StableCueKind(RuntimeCueKind kind)
    {
        return kind switch
        {
            RuntimeCueKind.FocusShift => "focus_shift",
            RuntimeCueKind.InvalidCueFilter => "invalid_cue_filter",
            RuntimeCueKind.GoNoGo => "go_no_go",
            RuntimeCueKind.Interruption => "interruption",
            RuntimeCueKind.TimedResponse => "timed_response",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cue kind."),
        };
    }
}
