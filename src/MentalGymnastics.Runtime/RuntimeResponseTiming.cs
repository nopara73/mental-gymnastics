using System.Globalization;

namespace MentalGymnastics.Runtime;

public enum RuntimeResponseTimingOutcome
{
    Pending,
    Early,
    OnTime,
    Late,
    Missed,
}

public sealed class RuntimeResponseWindow
{
    public RuntimeResponseWindow(
        string id,
        RuntimeInstant opensAt,
        RuntimeDuration responseWindow,
        string evidenceSource)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Runtime response window id is required.", nameof(id));
        }

        if (responseWindow.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseWindow),
                responseWindow,
                "Runtime response window duration must be positive.");
        }

        if (string.IsNullOrWhiteSpace(evidenceSource))
        {
            throw new ArgumentException("Runtime response evidence source is required.", nameof(evidenceSource));
        }

        Id = id;
        OpensAt = opensAt;
        ResponseWindow = responseWindow;
        EvidenceSource = evidenceSource;
    }

    public string Id { get; }

    public RuntimeInstant OpensAt { get; }

    public RuntimeDuration ResponseWindow { get; }

    public RuntimeInstant Deadline => OpensAt.Add(ResponseWindow);

    public string EvidenceSource { get; }

    public RuntimeResponseTimingResult RecordResponse(RuntimeInstant respondedAt)
    {
        if (respondedAt.Offset < OpensAt.Offset)
        {
            var earlyBy = new RuntimeDuration(OpensAt.Offset - respondedAt.Offset);
            return BuildResponseResult(
                RuntimeResponseTimingOutcome.Early,
                respondedAt,
                respondedAt,
                responseTime: null,
                earlyBy,
                lateBy: null,
                remaining: null);
        }

        var responseTime = respondedAt.ElapsedSince(OpensAt);
        if (respondedAt.Offset <= Deadline.Offset)
        {
            return BuildResponseResult(
                RuntimeResponseTimingOutcome.OnTime,
                respondedAt,
                respondedAt,
                responseTime,
                earlyBy: null,
                lateBy: null,
                remaining: null);
        }

        var lateBy = new RuntimeDuration(respondedAt.Offset - Deadline.Offset);
        return BuildResponseResult(
            RuntimeResponseTimingOutcome.Late,
            respondedAt,
            respondedAt,
            responseTime,
            earlyBy: null,
            lateBy,
            remaining: null);
    }

    public RuntimeResponseTimingResult EvaluateNoResponse(RuntimeInstant observedAt)
    {
        if (observedAt.Offset <= Deadline.Offset)
        {
            var remaining = new RuntimeDuration(Deadline.Offset - observedAt.Offset);
            return BuildResponseResult(
                RuntimeResponseTimingOutcome.Pending,
                observedAt,
                respondedAt: null,
                responseTime: null,
                earlyBy: null,
                lateBy: null,
                remaining);
        }

        var lateBy = new RuntimeDuration(observedAt.Offset - Deadline.Offset);
        return BuildResponseResult(
            RuntimeResponseTimingOutcome.Missed,
            observedAt,
            respondedAt: null,
            responseTime: null,
            earlyBy: null,
            lateBy,
            remaining: null);
    }

    private RuntimeResponseTimingResult BuildResponseResult(
        RuntimeResponseTimingOutcome outcome,
        RuntimeInstant observedAt,
        RuntimeInstant? respondedAt,
        RuntimeDuration? responseTime,
        RuntimeDuration? earlyBy,
        RuntimeDuration? lateBy,
        RuntimeDuration? remaining)
    {
        return new RuntimeResponseTimingResult(
            Id,
            EvidenceSource,
            outcome,
            OpensAt,
            Deadline,
            observedAt,
            respondedAt,
            responseTime,
            earlyBy,
            lateBy,
            remaining,
            BuildResponseFacts(
                outcome,
                observedAt,
                respondedAt,
                responseTime,
                earlyBy,
                lateBy,
                remaining));
    }

    private IReadOnlyList<RuntimeEventFact> BuildResponseFacts(
        RuntimeResponseTimingOutcome outcome,
        RuntimeInstant observedAt,
        RuntimeInstant? respondedAt,
        RuntimeDuration? responseTime,
        RuntimeDuration? earlyBy,
        RuntimeDuration? lateBy,
        RuntimeDuration? remaining)
    {
        var facts = new List<RuntimeEventFact>
        {
            new("timing_window_id", Id),
            new("timing_source", EvidenceSource),
            new("opens_at", FormatInstant(OpensAt)),
            new("deadline", FormatInstant(Deadline)),
            new("observed_at", FormatInstant(observedAt)),
            new("timing_outcome", StableResponseOutcome(outcome)),
        };

        if (respondedAt.HasValue)
        {
            facts.Add(new RuntimeEventFact("responded_at", FormatInstant(respondedAt.Value)));
        }

        AddDurationFact(facts, "response_time", responseTime);
        AddDurationFact(facts, "early_by", earlyBy);
        AddDurationFact(facts, "late_by", lateBy);
        AddDurationFact(facts, "remaining", remaining);

        return facts.AsReadOnly();
    }

    private static string StableResponseOutcome(RuntimeResponseTimingOutcome outcome)
    {
        return outcome switch
        {
            RuntimeResponseTimingOutcome.Pending => "pending",
            RuntimeResponseTimingOutcome.Early => "early",
            RuntimeResponseTimingOutcome.OnTime => "on_time",
            RuntimeResponseTimingOutcome.Late => "late",
            RuntimeResponseTimingOutcome.Missed => "missed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown response timing outcome."),
        };
    }

    private static void AddDurationFact(
        ICollection<RuntimeEventFact> facts,
        string name,
        RuntimeDuration? duration)
    {
        if (duration.HasValue)
        {
            facts.Add(new RuntimeEventFact(name, FormatDuration(duration.Value)));
        }
    }

    private static string FormatDuration(RuntimeDuration duration)
    {
        return duration.Value.ToString("c", CultureInfo.InvariantCulture);
    }

    private static string FormatInstant(RuntimeInstant instant)
    {
        return instant.Offset.ToString("c", CultureInfo.InvariantCulture);
    }
}

public sealed record RuntimeResponseTimingResult(
    string WindowId,
    string EvidenceSource,
    RuntimeResponseTimingOutcome Outcome,
    RuntimeInstant OpensAt,
    RuntimeInstant Deadline,
    RuntimeInstant ObservedAt,
    RuntimeInstant? RespondedAt,
    RuntimeDuration? ResponseTime,
    RuntimeDuration? EarlyBy,
    RuntimeDuration? LateBy,
    RuntimeDuration? Remaining,
    IReadOnlyList<RuntimeEventFact> EvidenceFacts);

public enum RuntimeRecoveryTriggerKind
{
    Drift,
    Disruption,
}

public enum RuntimeRecoveryTimingOutcome
{
    Pending,
    Recovered,
    Late,
    Missed,
}

public sealed class RuntimeRecoveryWindow
{
    private RuntimeRecoveryWindow(
        string id,
        RuntimeRecoveryTriggerKind triggerKind,
        string triggerId,
        RuntimeInstant startedAt,
        RuntimeDuration recoveryWindow)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Runtime recovery window id is required.", nameof(id));
        }

        EnsureDefined(triggerKind, nameof(triggerKind));

        if (string.IsNullOrWhiteSpace(triggerId))
        {
            throw new ArgumentException("Runtime recovery trigger id is required.", nameof(triggerId));
        }

        if (recoveryWindow.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recoveryWindow),
                recoveryWindow,
                "Runtime recovery window duration must be positive.");
        }

        Id = id;
        TriggerKind = triggerKind;
        TriggerId = triggerId;
        StartedAt = startedAt;
        RecoveryWindow = recoveryWindow;
    }

    public string Id { get; }

    public RuntimeRecoveryTriggerKind TriggerKind { get; }

    public string TriggerId { get; }

    public RuntimeInstant StartedAt { get; }

    public RuntimeDuration RecoveryWindow { get; }

    public RuntimeInstant Deadline => StartedAt.Add(RecoveryWindow);

    public static RuntimeRecoveryWindow AfterDrift(
        string id,
        string driftId,
        RuntimeInstant startedAt,
        RuntimeDuration recoveryWindow)
    {
        return new RuntimeRecoveryWindow(id, RuntimeRecoveryTriggerKind.Drift, driftId, startedAt, recoveryWindow);
    }

    public static RuntimeRecoveryWindow AfterDisruption(
        string id,
        string disruptionId,
        RuntimeInstant startedAt,
        RuntimeDuration recoveryWindow)
    {
        return new RuntimeRecoveryWindow(id, RuntimeRecoveryTriggerKind.Disruption, disruptionId, startedAt, recoveryWindow);
    }

    public RuntimeRecoveryTimingResult RecordRecovery(RuntimeInstant recoveredAt)
    {
        if (recoveredAt.Offset < StartedAt.Offset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recoveredAt),
                recoveredAt,
                "Runtime recovery cannot be recorded before the recovery window starts.");
        }

        var recoveryTime = recoveredAt.ElapsedSince(StartedAt);
        if (recoveredAt.Offset <= Deadline.Offset)
        {
            return BuildRecoveryResult(
                RuntimeRecoveryTimingOutcome.Recovered,
                recoveredAt,
                recoveredAt,
                recoveryTime,
                lateBy: null,
                remaining: null);
        }

        var lateBy = new RuntimeDuration(recoveredAt.Offset - Deadline.Offset);
        return BuildRecoveryResult(
            RuntimeRecoveryTimingOutcome.Late,
            recoveredAt,
            recoveredAt,
            recoveryTime,
            lateBy,
            remaining: null);
    }

    public RuntimeRecoveryTimingResult EvaluateNoRecovery(RuntimeInstant observedAt)
    {
        if (observedAt.Offset <= Deadline.Offset)
        {
            var remaining = new RuntimeDuration(Deadline.Offset - observedAt.Offset);
            return BuildRecoveryResult(
                RuntimeRecoveryTimingOutcome.Pending,
                observedAt,
                recoveredAt: null,
                recoveryTime: null,
                lateBy: null,
                remaining);
        }

        var lateBy = new RuntimeDuration(observedAt.Offset - Deadline.Offset);
        return BuildRecoveryResult(
            RuntimeRecoveryTimingOutcome.Missed,
            observedAt,
            recoveredAt: null,
            recoveryTime: null,
            lateBy,
            remaining: null);
    }

    private RuntimeRecoveryTimingResult BuildRecoveryResult(
        RuntimeRecoveryTimingOutcome outcome,
        RuntimeInstant observedAt,
        RuntimeInstant? recoveredAt,
        RuntimeDuration? recoveryTime,
        RuntimeDuration? lateBy,
        RuntimeDuration? remaining)
    {
        return new RuntimeRecoveryTimingResult(
            Id,
            TriggerKind,
            TriggerId,
            outcome,
            StartedAt,
            Deadline,
            observedAt,
            recoveredAt,
            recoveryTime,
            lateBy,
            remaining,
            BuildRecoveryFacts(outcome, observedAt, recoveredAt, recoveryTime, lateBy, remaining));
    }

    private IReadOnlyList<RuntimeEventFact> BuildRecoveryFacts(
        RuntimeRecoveryTimingOutcome outcome,
        RuntimeInstant observedAt,
        RuntimeInstant? recoveredAt,
        RuntimeDuration? recoveryTime,
        RuntimeDuration? lateBy,
        RuntimeDuration? remaining)
    {
        var facts = new List<RuntimeEventFact>
        {
            new("recovery_window_id", Id),
            new("recovery_trigger_kind", StableTriggerKind(TriggerKind)),
            new("trigger_id", TriggerId),
            new("recovery_started_at", FormatInstant(StartedAt)),
            new("recovery_deadline", FormatInstant(Deadline)),
            new("observed_at", FormatInstant(observedAt)),
            new("recovery_outcome", StableRecoveryOutcome(outcome)),
        };

        if (recoveredAt.HasValue)
        {
            facts.Add(new RuntimeEventFact("recovered_at", FormatInstant(recoveredAt.Value)));
        }

        AddDurationFact(facts, "recovery_time", recoveryTime);
        AddDurationFact(facts, "late_by", lateBy);
        AddDurationFact(facts, "remaining", remaining);

        return facts.AsReadOnly();
    }

    private static string StableTriggerKind(RuntimeRecoveryTriggerKind triggerKind)
    {
        return triggerKind switch
        {
            RuntimeRecoveryTriggerKind.Drift => "drift",
            RuntimeRecoveryTriggerKind.Disruption => "disruption",
            _ => throw new ArgumentOutOfRangeException(nameof(triggerKind), triggerKind, "Unknown recovery trigger kind."),
        };
    }

    private static string StableRecoveryOutcome(RuntimeRecoveryTimingOutcome outcome)
    {
        return outcome switch
        {
            RuntimeRecoveryTimingOutcome.Pending => "pending",
            RuntimeRecoveryTimingOutcome.Recovered => "recovered",
            RuntimeRecoveryTimingOutcome.Late => "late",
            RuntimeRecoveryTimingOutcome.Missed => "missed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown recovery timing outcome."),
        };
    }

    private static void AddDurationFact(
        ICollection<RuntimeEventFact> facts,
        string name,
        RuntimeDuration? duration)
    {
        if (duration.HasValue)
        {
            facts.Add(new RuntimeEventFact(name, FormatDuration(duration.Value)));
        }
    }

    private static string FormatDuration(RuntimeDuration duration)
    {
        return duration.Value.ToString("c", CultureInfo.InvariantCulture);
    }

    private static string FormatInstant(RuntimeInstant instant)
    {
        return instant.Offset.ToString("c", CultureInfo.InvariantCulture);
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime recovery value.");
        }
    }
}

public sealed record RuntimeRecoveryTimingResult(
    string WindowId,
    RuntimeRecoveryTriggerKind TriggerKind,
    string TriggerId,
    RuntimeRecoveryTimingOutcome Outcome,
    RuntimeInstant StartedAt,
    RuntimeInstant Deadline,
    RuntimeInstant ObservedAt,
    RuntimeInstant? RecoveredAt,
    RuntimeDuration? RecoveryTime,
    RuntimeDuration? LateBy,
    RuntimeDuration? Remaining,
    IReadOnlyList<RuntimeEventFact> EvidenceFacts);
