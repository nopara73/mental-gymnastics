using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimePhaseTimerTests
{
    [Fact]
    public void TimeProviderClockReportsElapsedProviderTimeAsRuntimeInstant()
    {
        var timeProvider = new ManualTimerTimeProvider();
        var clock = new TimeProviderRuntimeClock(timeProvider);

        timeProvider.AdvanceBy(TimeSpan.FromSeconds(25));

        Assert.Equal(TimeSpan.FromSeconds(25), clock.Now.Offset);
    }

    [Fact]
    public void PhaseTimerEmitsTimedPhaseSnapshotsFromInjectedProviderWithoutSleeping()
    {
        var timeProvider = new ManualTimerTimeProvider();
        var clock = new TimeProviderRuntimeClock(timeProvider);
        var phase = new RuntimeTimedPhase(
            "active-set",
            clock.Now,
            RuntimeDuration.FromSeconds(10));
        using var timer = new RuntimePhaseTimer(
            phase,
            RuntimeDuration.FromSeconds(1),
            clock,
            timeProvider);
        var ticks = new List<RuntimeTimerTick>();

        timer.Start(ticks.Add);

        Assert.True(timer.IsRunning);
        Assert.NotNull(timeProvider.LastTimer);
        Assert.Equal(TimeSpan.FromSeconds(1), timeProvider.LastTimer.DueTime);
        Assert.Equal(TimeSpan.FromSeconds(1), timeProvider.LastTimer.Period);

        timeProvider.AdvanceBy(TimeSpan.FromSeconds(3));
        timeProvider.LastTimer.Fire();

        var first = Assert.Single(ticks);
        Assert.Equal(1, first.SequenceNumber);
        Assert.Equal(TimeSpan.FromSeconds(3), first.ObservedAt.Offset);
        Assert.Equal(TimeSpan.FromSeconds(3), first.Snapshot.Elapsed.Value);
        Assert.Equal(TimeSpan.FromSeconds(7), first.Snapshot.Remaining.Value);
        Assert.False(first.Snapshot.HasTimedOut);

        timeProvider.AdvanceBy(TimeSpan.FromSeconds(7));
        timeProvider.LastTimer.Fire();

        Assert.Equal(2, ticks.Count);
        var timeout = ticks[1];
        Assert.Equal(2, timeout.SequenceNumber);
        Assert.Equal(TimeSpan.FromSeconds(10), timeout.ObservedAt.Offset);
        Assert.Equal(TimeSpan.Zero, timeout.Snapshot.Remaining.Value);
        Assert.True(timeout.Snapshot.HasTimedOut);
        Assert.NotNull(timeout.Snapshot.TimeoutEvent);
        Assert.Equal(TimeSpan.Zero, timeout.Snapshot.TimeoutEvent.Overtime.Value);
        Assert.False(timer.IsRunning);
        Assert.True(timeProvider.LastTimer.IsDisposed);
    }

    [Fact]
    public void StoppedPhaseTimerDoesNotEmitFurtherTicks()
    {
        var timeProvider = new ManualTimerTimeProvider();
        var clock = new TimeProviderRuntimeClock(timeProvider);
        var phase = new RuntimeTimedPhase(
            "recovery-window",
            clock.Now,
            RuntimeDuration.FromSeconds(5));
        using var timer = new RuntimePhaseTimer(
            phase,
            RuntimeDuration.FromSeconds(1),
            clock,
            timeProvider);
        var ticks = new List<RuntimeTimerTick>();

        timer.Start(ticks.Add);
        timer.Stop();

        timeProvider.AdvanceBy(TimeSpan.FromSeconds(1));
        timeProvider.LastTimer!.Fire();

        Assert.Empty(ticks);
        Assert.False(timer.IsRunning);
        Assert.True(timeProvider.LastTimer.IsDisposed);
    }

    [Fact]
    public void PhaseTimerRejectsInvalidAdministrationInputs()
    {
        var timeProvider = new ManualTimerTimeProvider();
        var clock = new TimeProviderRuntimeClock(timeProvider);
        var phase = new RuntimeTimedPhase(
            "encode-window",
            clock.Now,
            RuntimeDuration.FromSeconds(5));

        Assert.Throws<ArgumentNullException>(() => new RuntimePhaseTimer(
            null!,
            RuntimeDuration.FromSeconds(1),
            clock,
            timeProvider));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimePhaseTimer(
            phase,
            RuntimeDuration.Zero,
            clock,
            timeProvider));

        using var timer = new RuntimePhaseTimer(
            phase,
            RuntimeDuration.FromSeconds(1),
            clock,
            timeProvider);

        Assert.Throws<ArgumentNullException>(() => timer.Start(null!));

        timer.Start(_ => { });

        Assert.Throws<InvalidOperationException>(() => timer.Start(_ => { }));
    }

    private sealed class ManualTimerTimeProvider : TimeProvider
    {
        private long _timestampTicks;

        public ManualTimer? LastTimer { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return _timestampTicks;
        }

        public void AdvanceBy(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Test time cannot move backward.");
            }

            _timestampTicks += duration.Ticks;
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            LastTimer = new ManualTimer(callback, state, dueTime, period);
            return LastTimer;
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly TimerCallback _callback;
        private readonly object? _state;

        public ManualTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            _callback = callback;
            _state = state;
            DueTime = dueTime;
            Period = period;
        }

        public TimeSpan DueTime { get; private set; }

        public TimeSpan Period { get; private set; }

        public bool IsDisposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (IsDisposed)
            {
                return false;
            }

            DueTime = dueTime;
            Period = period;
            return true;
        }

        public void Fire()
        {
            if (!IsDisposed)
            {
                _callback(_state);
            }
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
