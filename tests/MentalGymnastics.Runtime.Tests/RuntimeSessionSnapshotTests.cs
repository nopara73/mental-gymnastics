using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeSessionSnapshotTests
{
    [Fact]
    public void RestoredCommandHandlerPreservesPhaseElapsedEventsGeneratedInstanceAndEvidenceFacts()
    {
        var generatedInstance = CueGeneratedInstance();
        var session = CreateCueSessionDefinition(generatedInstance);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = RuntimeInputCommandHandler.Start(
            "session-snapshot",
            session,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
                RuntimeSessionPhaseDefinition.Timed("rest", RuntimeSessionPhaseKind.Rest, RuntimeDuration.FromSeconds(10)),
                RuntimeSessionPhaseDefinition.Manual("review", RuntimeSessionPhaseKind.Review),
            ]),
            clock,
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        Assert.True(handler.Handle(RuntimeInputCommand.MarkDrift("drift-before-snapshot")).IsAccepted);
        Assert.True(handler.Handle(RuntimeInputCommand.FinishPhase()).IsAccepted);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(4));

        var snapshot = handler.CaptureSnapshot();

        Assert.Equal("session-snapshot", snapshot.SessionId);
        Assert.Equal("generated-cue-sequence-1", snapshot.GeneratedDrillInstance?.InstanceId);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, snapshot.LifecycleState.Status);
        Assert.Equal(RuntimePhaseSchedulerStatus.Running, snapshot.PhaseScheduler.Status);
        Assert.Equal("rest", snapshot.PhaseScheduler.CurrentPhaseId);
        Assert.Equal(TimeSpan.FromSeconds(4), snapshot.PhaseScheduler.CurrentPhaseElapsed?.Value);
        Assert.Contains(snapshot.RuntimeEvents, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.DriftMarked);
        Assert.Contains(snapshot.EvidenceFacts, fact => fact.Name == "drift_id" && fact.Value == "drift-before-snapshot");

        var restoreClock = new ManualRuntimeClock(RuntimeDuration.FromSeconds(100).ToInstant());
        var restored = RuntimeInputCommandHandler.Restore(snapshot, restoreClock);

        Assert.Equal("rest", restored.CurrentPhase?.Id);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, restored.LifecycleState.Status);
        Assert.Equal(snapshot.RuntimeEvents.Count, restored.Events.Count);
        Assert.Contains(restored.Events, runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.DriftMarked &&
            runtimeEvent.Facts.Any(fact => fact.Name == "drift_id" && fact.Value == "drift-before-snapshot"));

        restoreClock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var earlyFinish = restored.Handle(RuntimeInputCommand.FinishPhase());

        Assert.False(earlyFinish.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.InvalidPhaseCompletion, earlyFinish.InvalidReason);
        Assert.Equal(RuntimeSessionPhaseInvalidCompletionReason.ScheduledDurationNotReached, earlyFinish.PhaseInvalidReason);

        restoreClock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var restFinished = restored.Handle(RuntimeInputCommand.FinishPhase());

        Assert.True(restFinished.IsAccepted);
        Assert.Equal("review", restored.CurrentPhase?.Id);
        Assert.Contains(restFinished.Events, runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.PhaseEnded &&
            runtimeEvent.Facts.Any(fact => fact.Name == "phase_actual_duration" && fact.Value == "00:00:10"));
    }

    [Fact]
    public void RestoreRejectsSnapshotCapturedWhereContinuationWouldCorruptHonestyConstraint()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = RuntimeInputCommandHandler.Start(
            "session-unsafe-snapshot",
            CreateWorkingMemorySessionDefinition(),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("encode", RuntimeSessionPhaseKind.EncodeWindow, RuntimeDuration.FromSeconds(60)),
                RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
            ]),
            clock,
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(20));
        var snapshot = handler.CaptureSnapshot();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RuntimeInputCommandHandler.Restore(
                snapshot,
                new ManualRuntimeClock(RuntimeDuration.FromSeconds(100).ToInstant())));

        Assert.Contains("cannot be restored", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("encode", snapshot.PhaseScheduler.CurrentPhaseId);
        Assert.Equal(TimeSpan.FromSeconds(20), snapshot.PhaseScheduler.CurrentPhaseElapsed?.Value);
    }

    [Fact]
    public void RestoredCueSchedulerPreservesPendingCuesGeneratedInstanceEventsAndResponseState()
    {
        var generatedInstance = CueGeneratedInstance();
        var session = CreateCueSessionDefinition(generatedInstance);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var log = RuntimeEventLog.Start("session-cue-snapshot", session, clock.Now);
        var schedule = CreateCueSchedule(generatedInstance);
        var scheduler = new RuntimeCueScheduler(schedule, clock, log);
        var phase = RuntimeSessionPhaseDefinition.Manual("cue-window", RuntimeSessionPhaseKind.CueResponse);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        scheduler.AdvanceToCurrentTime(phase);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var firstResponse = scheduler.RecordResponse(RuntimeCueResponse.ForCue("cue-1", "left"));
        var snapshot = scheduler.CaptureSnapshot();

        Assert.True(firstResponse.IsAccepted);
        Assert.Equal("generated-cue-sequence-1", snapshot.GeneratedDrillInstance.InstanceId);
        Assert.Equal(["cue-2"], snapshot.PendingCueIds);
        Assert.Contains(snapshot.RuntimeEvents, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.CueResponseSubmitted);
        Assert.Contains(snapshot.EvidenceFacts, fact => fact.Name == "cue_id" && fact.Value == "cue-1");
        Assert.Contains(snapshot.EvidenceFacts, fact => fact.Name == "response_outcome" && fact.Value == "correct");

        var restoreClock = new ManualRuntimeClock(RuntimeDuration.FromSeconds(100).ToInstant());
        var restoredLog = RuntimeEventLog.Restore("session-cue-snapshot", session, snapshot.RuntimeEvents);
        var restored = RuntimeCueScheduler.Restore(schedule, restoreClock, restoredLog, snapshot);

        var duplicateResponse = restored.RecordResponse(RuntimeCueResponse.ForCue("cue-1", "left"));
        Assert.False(duplicateResponse.IsAccepted);
        Assert.Equal(RuntimeCueResponseInvalidReason.CueAlreadyResponded, duplicateResponse.InvalidReason);

        restoreClock.AdvanceBy(RuntimeDuration.FromSeconds(3));
        Assert.Empty(restored.AdvanceToCurrentTime(phase).EmittedCues);

        restoreClock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var secondCue = restored.AdvanceToCurrentTime(phase);

        Assert.Collection(secondCue.EmittedCues, cue => Assert.Equal("cue-2", cue.Id));
        Assert.Empty(restored.PendingCues);
        Assert.Contains(secondCue.Events[0].Facts, fact => fact.Name == "cue_id" && fact.Value == "cue-2");
        Assert.Equal(TimeSpan.FromSeconds(104), secondCue.Events[0].OccurredAt.Offset);
    }

    [Fact]
    public void RestoredAbandonedSessionCannotBeMistakenForSuccessfulEvidence()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = RuntimeInputCommandHandler.Start(
            "session-restored-abandoned",
            CreateFocusHoldSessionDefinition(),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("rest", RuntimeSessionPhaseKind.Rest, RuntimeDuration.FromSeconds(30)),
            ]),
            clock,
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var snapshot = handler.CaptureSnapshot();
        var restored = RuntimeInputCommandHandler.Restore(
            snapshot,
            new ManualRuntimeClock(RuntimeDuration.FromSeconds(50).ToInstant()));

        var abandon = restored.Handle(RuntimeInputCommand.Abandon("android lifecycle interruption made the set invalid"));
        var result = RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            "session-restored-abandoned",
            restored.EventLog.SessionDefinition,
            RuntimeSessionCompletionStatus.Abandoned,
            [],
            restored.Events,
            [],
            []));

        Assert.True(abandon.IsAccepted);
        Assert.Equal(RuntimeSessionCompletionStatus.Abandoned, result.CompletionStatus);
        Assert.Empty(result.EvidenceDrafts);
        Assert.Equal(0, result.EvidenceSummary.ArtifactCount);
        Assert.Contains(result.FailureRelevantFacts, fact =>
            fact.Name == "abandon_reason" &&
            fact.Value.Contains("invalid", StringComparison.OrdinalIgnoreCase));
        Assert.False(result.ContainsAdvancementDecision);
    }

    private static RuntimeCueSchedule CreateCueSchedule(RuntimeGeneratedDrillInstanceIdentity generatedInstance)
    {
        return new RuntimeCueSchedule(
            generatedInstance,
            [
                new RuntimeScheduledCue(
                    "cue-1",
                    RuntimeCueKind.FocusShift,
                    "left",
                    RuntimeDuration.FromSeconds(5).ToInstant(),
                    RuntimeDuration.FromSeconds(2),
                    RuntimeCueResponseExpectation.ResponseRequired,
                    "left"),
                new RuntimeScheduledCue(
                    "cue-2",
                    RuntimeCueKind.TimedResponse,
                    "now",
                    RuntimeDuration.FromSeconds(10).ToInstant(),
                    RuntimeDuration.FromSeconds(2),
                    RuntimeCueResponseExpectation.ResponseRequired,
                    "hit"),
            ]);
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

    private static RuntimeSessionDefinition CreateCueSessionDefinition(
        RuntimeGeneratedDrillInstanceIdentity generatedInstance)
    {
        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FS,
            GlobalLevelId.L1,
            DrillId.FS1CueSwitch,
            [new LoadVariable("cue density", "2 cues in 10 seconds")],
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

    private static RuntimeSessionDefinition CreateWorkingMemorySessionDefinition()
    {
        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            [new LoadVariable("item count", "5 simple items")],
            new BranchLevelStandard(
                BranchCode.WM,
                GlobalLevelId.L1,
                "Encode and reconstruct 5 simple items after 60 seconds.",
                "At least 4 of 5 exact; no invented items.",
                "FH L1 passed once.",
                "Repeat twice with new item sets.",
                "Use a different content type."),
            [new CriticalConstraint("No rereading after encode window; no invented items.")]);
    }

    private static RuntimeSessionDefinition CreateFocusHoldSessionDefinition()
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
