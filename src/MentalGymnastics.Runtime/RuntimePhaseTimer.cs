namespace MentalGymnastics.Runtime;

public sealed class TimeProviderRuntimeClock : IRuntimeClock
{
    private readonly RuntimeInstant _initialInstant;
    private readonly long _initialTimestamp;
    private readonly TimeProvider _timeProvider;

    public TimeProviderRuntimeClock(TimeProvider? timeProvider = null)
        : this(timeProvider, RuntimeInstant.Zero)
    {
    }

    public TimeProviderRuntimeClock(
        TimeProvider? timeProvider,
        RuntimeInstant initialInstant)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _initialInstant = initialInstant;
        _initialTimestamp = _timeProvider.GetTimestamp();
    }

    public RuntimeInstant Now
    {
        get
        {
            var elapsed = _timeProvider.GetElapsedTime(_initialTimestamp);
            return _initialInstant.Add(new RuntimeDuration(elapsed));
        }
    }
}

public sealed record RuntimeTimerTick(
    int SequenceNumber,
    RuntimeInstant ObservedAt,
    RuntimeTimedPhaseSnapshot Snapshot);

public sealed class RuntimePhaseTimer : IDisposable
{
    private readonly IRuntimeClock _clock;
    private readonly RuntimeTimedPhase _phase;
    private readonly object _sync = new();
    private readonly RuntimeDuration _tickInterval;
    private readonly TimeProvider _timeProvider;
    private bool _disposed;
    private bool _isRunning;
    private Action<RuntimeTimerTick>? _onTick;
    private int _sequenceNumber;
    private ITimer? _timer;

    public RuntimePhaseTimer(
        RuntimeTimedPhase phase,
        RuntimeDuration tickInterval,
        IRuntimeClock clock,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(phase);
        ArgumentNullException.ThrowIfNull(clock);

        if (tickInterval.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tickInterval),
                tickInterval,
                "Runtime timer tick interval must be positive.");
        }

        _phase = phase;
        _tickInterval = tickInterval;
        _clock = clock;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _isRunning;
            }
        }
    }

    public void Start(Action<RuntimeTimerTick> onTick)
    {
        ArgumentNullException.ThrowIfNull(onTick);

        lock (_sync)
        {
            ThrowIfDisposed();

            if (_isRunning)
            {
                throw new InvalidOperationException("Runtime phase timer is already running.");
            }

            _onTick = onTick;
            _sequenceNumber = 0;
            _isRunning = true;
            _timer = _timeProvider.CreateTimer(
                Tick,
                null,
                _tickInterval.Value,
                _tickInterval.Value);
        }
    }

    public void Stop()
    {
        ITimer? timerToDispose;

        lock (_sync)
        {
            timerToDispose = StopUnderLock();
        }

        timerToDispose?.Dispose();
    }

    public void Dispose()
    {
        ITimer? timerToDispose;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            timerToDispose = StopUnderLock();
        }

        timerToDispose?.Dispose();
    }

    private void Tick(object? _)
    {
        Action<RuntimeTimerTick>? onTick;
        RuntimeTimerTick tick;
        ITimer? timerToDispose = null;

        lock (_sync)
        {
            if (!_isRunning || _disposed || _onTick is null)
            {
                return;
            }

            var observedAt = _clock.Now;
            var snapshot = _phase.SnapshotAt(observedAt);
            tick = new RuntimeTimerTick(++_sequenceNumber, observedAt, snapshot);
            onTick = _onTick;

            if (snapshot.HasTimedOut)
            {
                timerToDispose = StopUnderLock();
            }
        }

        try
        {
            onTick(tick);
        }
        finally
        {
            timerToDispose?.Dispose();
        }
    }

    private ITimer? StopUnderLock()
    {
        if (!_isRunning && _timer is null)
        {
            return null;
        }

        _isRunning = false;
        _onTick = null;
        var timer = _timer;
        _timer = null;
        return timer;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RuntimePhaseTimer));
        }
    }
}
