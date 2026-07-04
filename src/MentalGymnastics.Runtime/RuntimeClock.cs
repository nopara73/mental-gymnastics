namespace MentalGymnastics.Runtime;

public readonly record struct RuntimeDuration
{
    public RuntimeDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Runtime duration cannot be negative.");
        }

        Value = value;
    }

    public TimeSpan Value { get; }

    public static RuntimeDuration Zero { get; } = new(TimeSpan.Zero);

    public static RuntimeDuration FromSeconds(int seconds)
    {
        return new RuntimeDuration(TimeSpan.FromSeconds(seconds));
    }
}

public readonly record struct RuntimeInstant
{
    public RuntimeInstant(TimeSpan offset)
    {
        if (offset < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Runtime instant cannot be negative.");
        }

        Offset = offset;
    }

    public TimeSpan Offset { get; }

    public static RuntimeInstant Zero { get; } = new(TimeSpan.Zero);

    public RuntimeInstant Add(RuntimeDuration duration)
    {
        return new RuntimeInstant(Offset + duration.Value);
    }

    public RuntimeDuration ElapsedSince(RuntimeInstant start)
    {
        if (Offset <= start.Offset)
        {
            return RuntimeDuration.Zero;
        }

        return new RuntimeDuration(Offset - start.Offset);
    }
}

public interface IRuntimeClock
{
    RuntimeInstant Now { get; }
}

public sealed class ManualRuntimeClock : IRuntimeClock
{
    public ManualRuntimeClock(RuntimeInstant initialInstant)
    {
        Now = initialInstant;
    }

    public RuntimeInstant Now { get; private set; }

    public void AdvanceBy(RuntimeDuration duration)
    {
        AdvanceTo(Now.Add(duration));
    }

    public void AdvanceTo(RuntimeInstant instant)
    {
        if (instant.Offset < Now.Offset)
        {
            throw new ArgumentOutOfRangeException(nameof(instant), instant, "Runtime clock cannot move backward.");
        }

        Now = instant;
    }
}

public sealed class RuntimeTimedPhase
{
    public RuntimeTimedPhase(
        string name,
        RuntimeInstant startedAt,
        RuntimeDuration duration)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Timed phase name is required.", nameof(name));
        }

        if (duration.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Timed phase duration must be positive.");
        }

        Name = name;
        StartedAt = startedAt;
        Duration = duration;
    }

    public string Name { get; }

    public RuntimeInstant StartedAt { get; }

    public RuntimeDuration Duration { get; }

    public RuntimeInstant Deadline => StartedAt.Add(Duration);

    public RuntimeTimedPhaseSnapshot SnapshotAt(IRuntimeClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        return SnapshotAt(clock.Now);
    }

    public RuntimeTimedPhaseSnapshot SnapshotAt(RuntimeInstant observedAt)
    {
        var elapsed = observedAt.ElapsedSince(StartedAt);
        var remainingValue = Duration.Value - elapsed.Value;
        var remaining = remainingValue > TimeSpan.Zero
            ? new RuntimeDuration(remainingValue)
            : RuntimeDuration.Zero;

        var timeoutEvent = observedAt.Offset >= Deadline.Offset
            ? new RuntimeTimeoutEvent(
                Name,
                Deadline,
                observedAt,
                new RuntimeDuration(observedAt.Offset - Deadline.Offset))
            : null;

        return new RuntimeTimedPhaseSnapshot(
            Name,
            StartedAt,
            Deadline,
            elapsed,
            remaining,
            timeoutEvent);
    }
}

public sealed record RuntimeTimedPhaseSnapshot(
    string PhaseName,
    RuntimeInstant StartedAt,
    RuntimeInstant Deadline,
    RuntimeDuration Elapsed,
    RuntimeDuration Remaining,
    RuntimeTimeoutEvent? TimeoutEvent)
{
    public bool HasTimedOut => TimeoutEvent is not null;
}

public sealed record RuntimeTimeoutEvent(
    string PhaseName,
    RuntimeInstant Deadline,
    RuntimeInstant ObservedAt,
    RuntimeDuration Overtime);
