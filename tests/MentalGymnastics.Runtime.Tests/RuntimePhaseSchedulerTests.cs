using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimePhaseSchedulerTests
{
    [Fact]
    public void SchedulerMovesThroughPhasesWithExplicitEventsUntilSessionCompletes()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var session = CreateSessionDefinition();
        var plan = new RuntimeSessionPhasePlan(
        [
            RuntimeSessionPhaseDefinition.Manual("prep", RuntimeSessionPhaseKind.InstructionPrep),
            RuntimeSessionPhaseDefinition.Manual("review", RuntimeSessionPhaseKind.Review),
        ]);
        var scheduler = new RuntimePhaseScheduler(session, plan, clock);

        var started = scheduler.Start();

        Assert.True(started.IsValid);
        Assert.Equal(RuntimePhaseSchedulerStatus.Running, scheduler.Status);
        Assert.Same(session, scheduler.SessionDefinition);
        Assert.Equal("prep", scheduler.CurrentPhase?.Id);
        Assert.Collection(
            started.Events,
            EventIs(RuntimePhaseSchedulerEventKind.PhaseStarted, "prep", TimeSpan.Zero));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(3));
        var prepCompleted = scheduler.CompleteCurrentPhase();

        Assert.True(prepCompleted.IsValid);
        Assert.Equal("review", scheduler.CurrentPhase?.Id);
        Assert.Collection(
            prepCompleted.Events,
            EventIs(RuntimePhaseSchedulerEventKind.PhaseEnded, "prep", TimeSpan.FromSeconds(3)),
            EventIs(RuntimePhaseSchedulerEventKind.PhaseStarted, "review", TimeSpan.FromSeconds(3)));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));
        var sessionCompleted = scheduler.CompleteCurrentPhase();

        Assert.True(sessionCompleted.IsValid);
        Assert.Equal(RuntimePhaseSchedulerStatus.Completed, scheduler.Status);
        Assert.True(scheduler.IsComplete);
        Assert.Null(scheduler.CurrentPhase);
        Assert.Equal(2, scheduler.CompletedPhases.Count);
        Assert.Collection(
            sessionCompleted.Events,
            EventIs(RuntimePhaseSchedulerEventKind.PhaseEnded, "review", TimeSpan.FromSeconds(5)),
            EventIs(RuntimePhaseSchedulerEventKind.SessionCompleted, null, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void SchedulerAdvancesTimedPhaseByTimeoutAndEmitsTimeoutBeforePhaseEnd()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var scheduler = new RuntimePhaseScheduler(
            CreateSessionDefinition(),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("encode", RuntimeSessionPhaseKind.EncodeWindow, RuntimeDuration.FromSeconds(60)),
                RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
            ]),
            clock);
        scheduler.Start();

        clock.AdvanceBy(RuntimeDuration.FromSeconds(59));
        var early = scheduler.AdvanceToCurrentTime();

        Assert.True(early.IsValid);
        Assert.Empty(early.Events);
        Assert.Equal("encode", scheduler.CurrentPhase?.Id);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));
        var timeout = scheduler.AdvanceToCurrentTime();

        Assert.True(timeout.IsValid);
        Assert.Equal("reconstruct", scheduler.CurrentPhase?.Id);
        Assert.Collection(
            timeout.Events,
            EventIs(RuntimePhaseSchedulerEventKind.PhaseTimedOut, "encode", TimeSpan.FromSeconds(61)),
            EventIs(RuntimePhaseSchedulerEventKind.PhaseEnded, "encode", TimeSpan.FromSeconds(61)),
            EventIs(RuntimePhaseSchedulerEventKind.PhaseStarted, "reconstruct", TimeSpan.FromSeconds(61)));

        var timeoutEvent = timeout.Events[0];
        Assert.NotNull(timeoutEvent.TimeoutSnapshot);
        Assert.True(timeoutEvent.TimeoutSnapshot.HasTimedOut);
        Assert.Equal(TimeSpan.FromSeconds(1), timeoutEvent.TimeoutSnapshot.TimeoutEvent?.Overtime.Value);
    }

    [Fact]
    public void SchedulerCompletesSessionWhenFinalTimedPhaseTimesOut()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var scheduler = new RuntimePhaseScheduler(
            CreateSessionDefinition(),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("active", RuntimeSessionPhaseKind.ActiveWork, RuntimeDuration.FromSeconds(10)),
            ]),
            clock);
        scheduler.Start();

        clock.AdvanceBy(RuntimeDuration.FromSeconds(10));
        var result = scheduler.AdvanceToCurrentTime();

        Assert.True(result.IsValid);
        Assert.Equal(RuntimePhaseSchedulerStatus.Completed, scheduler.Status);
        Assert.Collection(
            result.Events,
            EventIs(RuntimePhaseSchedulerEventKind.PhaseTimedOut, "active", TimeSpan.FromSeconds(10)),
            EventIs(RuntimePhaseSchedulerEventKind.PhaseEnded, "active", TimeSpan.FromSeconds(10)),
            EventIs(RuntimePhaseSchedulerEventKind.SessionCompleted, null, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void SchedulerRejectsInvalidProgressionWithoutMutatingState()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var scheduler = new RuntimePhaseScheduler(
            CreateSessionDefinition(),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("hold", RuntimeSessionPhaseKind.ActiveWork, RuntimeDuration.FromSeconds(10)),
            ]),
            clock);

        var beforeStart = scheduler.CompleteCurrentPhase();

        Assert.False(beforeStart.IsValid);
        Assert.Equal(RuntimePhaseSchedulerInvalidReason.NotStarted, beforeStart.InvalidReason);
        Assert.Equal(RuntimePhaseSchedulerStatus.NotStarted, scheduler.Status);

        scheduler.Start();
        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));

        var earlyCompletion = scheduler.CompleteCurrentPhase();

        Assert.False(earlyCompletion.IsValid);
        Assert.Equal(RuntimePhaseSchedulerInvalidReason.InvalidPhaseCompletion, earlyCompletion.InvalidReason);
        Assert.Equal(RuntimeSessionPhaseInvalidCompletionReason.ScheduledDurationNotReached, earlyCompletion.PhaseInvalidReason);
        Assert.Empty(earlyCompletion.Events);
        Assert.Equal(RuntimePhaseSchedulerStatus.Running, scheduler.Status);
        Assert.Equal("hold", scheduler.CurrentPhase?.Id);
        Assert.Empty(scheduler.CompletedPhases);

        var secondStart = scheduler.Start();

        Assert.False(secondStart.IsValid);
        Assert.Equal(RuntimePhaseSchedulerInvalidReason.AlreadyStarted, secondStart.InvalidReason);
        Assert.Equal("hold", scheduler.CurrentPhase?.Id);
    }

    [Theory]
    [InlineData(RuntimePhaseSchedulerStatus.Running)]
    [InlineData(RuntimePhaseSchedulerStatus.Paused)]
    public void RestoreRejectsActiveStatusSnapshotsWithoutCurrentPhase(RuntimePhaseSchedulerStatus status)
    {
        var session = CreateSessionDefinition();
        var plan = new RuntimeSessionPhasePlan(
        [
            RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
        ]);
        var snapshot = new RuntimePhaseSchedulerSnapshot(
            status,
            RuntimeDuration.FromSeconds(20).ToInstant(),
            currentPhaseIndex: null,
            currentPhaseId: null,
            currentPhaseElapsed: null,
            completedPhases: []);

        var exception = Assert.Throws<ArgumentException>(() =>
            RuntimePhaseScheduler.Restore(
                session,
                plan,
                new ManualRuntimeClock(RuntimeDuration.FromSeconds(30).ToInstant()),
                snapshot));

        Assert.Contains("active phase", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Action<RuntimePhaseSchedulerEvent> EventIs(
        RuntimePhaseSchedulerEventKind expectedKind,
        string? expectedPhaseId,
        TimeSpan expectedOccurredAt)
    {
        return runtimeEvent =>
        {
            Assert.Equal(expectedKind, runtimeEvent.Kind);
            Assert.Equal(expectedPhaseId, runtimeEvent.Phase?.Id);
            Assert.Equal(expectedOccurredAt, runtimeEvent.OccurredAt.Offset);
        };
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
