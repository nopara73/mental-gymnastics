using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeClockTests
{
    [Fact]
    public void ManualClockAdvancesDeterministicallyAndTimedPhaseReportsElapsedRemainingDeadline()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var phase = new RuntimeTimedPhase(
            "fh-hold",
            clock.Now,
            RuntimeDuration.FromSeconds(180));

        var initial = phase.SnapshotAt(clock);
        Assert.Equal(TimeSpan.Zero, initial.Elapsed.Value);
        Assert.Equal(TimeSpan.FromSeconds(180), initial.Remaining.Value);
        Assert.Equal(TimeSpan.FromSeconds(180), initial.Deadline.Offset);
        Assert.False(initial.HasTimedOut);
        Assert.Null(initial.TimeoutEvent);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(45));

        var snapshot = phase.SnapshotAt(clock);
        Assert.Equal(TimeSpan.FromSeconds(45), snapshot.Elapsed.Value);
        Assert.Equal(TimeSpan.FromSeconds(135), snapshot.Remaining.Value);
        Assert.Equal(TimeSpan.FromSeconds(180), snapshot.Deadline.Offset);
        Assert.False(snapshot.HasTimedOut);
        Assert.Null(snapshot.TimeoutEvent);
    }

    [Fact]
    public void TimedPhaseEmitsTimeoutEventWhenManualTimeReachesDeadline()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var phase = new RuntimeTimedPhase(
            "encode-window",
            clock.Now,
            RuntimeDuration.FromSeconds(60));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(60));

        var atDeadline = phase.SnapshotAt(clock);
        Assert.True(atDeadline.HasTimedOut);
        Assert.Equal(TimeSpan.Zero, atDeadline.Remaining.Value);
        Assert.NotNull(atDeadline.TimeoutEvent);
        Assert.Equal(TimeSpan.Zero, atDeadline.TimeoutEvent.Overtime.Value);
        Assert.Equal(TimeSpan.FromSeconds(60), atDeadline.TimeoutEvent.Deadline.Offset);
        Assert.Equal(TimeSpan.FromSeconds(60), atDeadline.TimeoutEvent.ObservedAt.Offset);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(10));

        var late = phase.SnapshotAt(clock);
        Assert.True(late.HasTimedOut);
        Assert.Equal(TimeSpan.Zero, late.Remaining.Value);
        Assert.NotNull(late.TimeoutEvent);
        Assert.Equal(TimeSpan.FromSeconds(10), late.TimeoutEvent.Overtime.Value);
    }

    [Fact]
    public void RuntimeTimeModelRejectsNegativeOrEmptyAdministrationInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeDuration(TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeInstant(TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentException>(() => new RuntimeTimedPhase(
            " ",
            RuntimeInstant.Zero,
            RuntimeDuration.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeTimedPhase(
            "zero",
            RuntimeInstant.Zero,
            RuntimeDuration.Zero));

        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(10));

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceTo(RuntimeInstant.Zero));
    }
}
