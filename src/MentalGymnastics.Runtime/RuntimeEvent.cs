using System.Globalization;

namespace MentalGymnastics.Runtime;

public enum RuntimeEventKind
{
    SessionStarted,
    PhaseStarted,
    PhaseEnded,
    PhaseTimedOut,
    TimerTick,
    CueEmitted,
    CueResponseSubmitted,
    UserAction,
    AnswerSubmitted,
    DriftMarked,
    GuessMarked,
    AuditStarted,
    InterruptionRecorded,
    CorrectionSubmitted,
    ErrorRecorded,
    RecoveryStarted,
    RecoveryCompleted,
    SessionPaused,
    SessionResumed,
    SessionAbandoned,
    SessionCompleted,
}

public sealed record RuntimeEventFact
{
    public RuntimeEventFact(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Runtime event fact name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Runtime event fact value is required.", nameof(value));
        }

        Name = name;
        Value = value;
    }

    public string Name { get; }

    public string Value { get; }
}

public sealed class RuntimeEvent
{
    public RuntimeEvent(
        string sessionId,
        long sequenceNumber,
        RuntimeEventKind kind,
        RuntimeInstant occurredAt,
        string? phaseId = null,
        RuntimeSessionPhaseKind? phaseKind = null,
        IEnumerable<RuntimeEventFact>? facts = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime event session id is required.", nameof(sessionId));
        }

        if (sequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                sequenceNumber,
                "Runtime event sequence number must be positive.");
        }

        EnsureDefined(kind, nameof(kind));

        if (phaseId is not null && string.IsNullOrWhiteSpace(phaseId))
        {
            throw new ArgumentException("Runtime event phase id cannot be blank.", nameof(phaseId));
        }

        if (phaseKind.HasValue)
        {
            EnsureDefined(phaseKind.Value, nameof(phaseKind));
        }

        var factArray = (facts ?? Array.Empty<RuntimeEventFact>()).ToArray();
        foreach (var fact in factArray)
        {
            ArgumentNullException.ThrowIfNull(fact);
        }

        SessionId = sessionId;
        SequenceNumber = sequenceNumber;
        Kind = kind;
        OccurredAt = occurredAt;
        PhaseId = phaseId;
        PhaseKind = phaseKind;
        Facts = Array.AsReadOnly(factArray);
    }

    public string SessionId { get; }

    public long SequenceNumber { get; }

    public RuntimeEventKind Kind { get; }

    public RuntimeInstant OccurredAt { get; }

    public string? PhaseId { get; }

    public RuntimeSessionPhaseKind? PhaseKind { get; }

    public IReadOnlyList<RuntimeEventFact> Facts { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime event value.");
        }
    }
}

public sealed class RuntimeEventLog
{
    private readonly List<RuntimeEvent> _events = [];
    private bool _hasTerminalEvent;

    private RuntimeEventLog(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime event log session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);

        SessionId = sessionId;
        SessionDefinition = sessionDefinition;
    }

    public string SessionId { get; }

    public RuntimeSessionDefinition SessionDefinition { get; }

    public bool IsActive => !_hasTerminalEvent;

    public IReadOnlyList<RuntimeEvent> Events => _events.AsReadOnly();

    public static RuntimeEventLog Start(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        RuntimeInstant occurredAt)
    {
        var log = new RuntimeEventLog(sessionId, sessionDefinition);
        log.Append(RuntimeEventKind.SessionStarted, occurredAt);
        return log;
    }

    public static RuntimeEventLog Restore(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        IEnumerable<RuntimeEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var eventArray = events.ToArray();
        if (eventArray.Length == 0)
        {
            throw new ArgumentException("Runtime event log restore requires at least one event.", nameof(events));
        }

        var log = new RuntimeEventLog(sessionId, sessionDefinition);
        var previousOccurredAt = RuntimeInstant.Zero;
        for (var index = 0; index < eventArray.Length; index++)
        {
            var runtimeEvent = eventArray[index];
            ArgumentNullException.ThrowIfNull(runtimeEvent);

            if (!string.Equals(runtimeEvent.SessionId, sessionId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Restored runtime events must belong to the restored session.", nameof(events));
            }

            if (runtimeEvent.SequenceNumber != index + 1L)
            {
                throw new ArgumentException("Restored runtime event sequence numbers must be contiguous.", nameof(events));
            }

            if (index == 0 && runtimeEvent.Kind != RuntimeEventKind.SessionStarted)
            {
                throw new ArgumentException("Restored runtime event logs must start with a session start event.", nameof(events));
            }

            if (index > 0 && runtimeEvent.OccurredAt.Offset < previousOccurredAt.Offset)
            {
                throw new ArgumentException("Restored runtime events must be in chronological order.", nameof(events));
            }

            if (IsTerminalEvent(runtimeEvent.Kind) && index != eventArray.Length - 1)
            {
                throw new ArgumentException("Terminal runtime events must be the final restored event.", nameof(events));
            }

            previousOccurredAt = runtimeEvent.OccurredAt;
            log._events.Add(runtimeEvent);
        }

        log._hasTerminalEvent = IsTerminalEvent(eventArray[^1].Kind);
        return log;
    }

    public RuntimeEvent Append(
        RuntimeEventKind kind,
        RuntimeInstant occurredAt,
        string? phaseId = null,
        RuntimeSessionPhaseKind? phaseKind = null,
        IEnumerable<RuntimeEventFact>? facts = null)
    {
        EnsureDefined(kind, nameof(kind));

        if (_hasTerminalEvent)
        {
            throw new InvalidOperationException("Cannot append runtime events after the session has ended.");
        }

        if (kind == RuntimeEventKind.SessionStarted && _events.Count > 0)
        {
            throw new InvalidOperationException("A runtime event log can only contain one session start.");
        }

        if (_events.Count > 0 && occurredAt.Offset < _events[^1].OccurredAt.Offset)
        {
            throw new InvalidOperationException("Runtime events must be captured in chronological order.");
        }

        var runtimeEvent = new RuntimeEvent(
            SessionId,
            _events.Count + 1L,
            kind,
            occurredAt,
            phaseId,
            phaseKind,
            facts);

        _events.Add(runtimeEvent);

        if (kind is RuntimeEventKind.SessionAbandoned or RuntimeEventKind.SessionCompleted)
        {
            _hasTerminalEvent = true;
        }

        return runtimeEvent;
    }

    public IReadOnlyList<RuntimeEvent> AppendSchedulerEvents(
        IEnumerable<RuntimePhaseSchedulerEvent> schedulerEvents)
    {
        ArgumentNullException.ThrowIfNull(schedulerEvents);

        var appendedEvents = new List<RuntimeEvent>();
        foreach (var schedulerEvent in schedulerEvents)
        {
            ArgumentNullException.ThrowIfNull(schedulerEvent);

            if (!MatchesSessionDefinition(schedulerEvent.SessionDefinition))
            {
                throw new InvalidOperationException("Scheduler event does not belong to this runtime session.");
            }

            var phase = schedulerEvent.Phase ?? schedulerEvent.CompletedPhase?.Definition;
            appendedEvents.Add(Append(
                MapSchedulerEventKind(schedulerEvent.Kind),
                schedulerEvent.OccurredAt,
                phase?.Id,
                phase?.Kind,
                BuildSchedulerFacts(schedulerEvent)));
        }

        return appendedEvents.AsReadOnly();
    }

    private bool MatchesSessionDefinition(RuntimeSessionDefinition sessionDefinition)
    {
        return SessionDefinition.SessionType == sessionDefinition.SessionType &&
            SessionDefinition.Branch == sessionDefinition.Branch &&
            SessionDefinition.Level == sessionDefinition.Level &&
            SessionDefinition.Drill == sessionDefinition.Drill &&
            SessionDefinition.GeneratedDrillInstance?.InstanceId == sessionDefinition.GeneratedDrillInstance?.InstanceId;
    }

    private static RuntimeEventKind MapSchedulerEventKind(RuntimePhaseSchedulerEventKind kind)
    {
        return kind switch
        {
            RuntimePhaseSchedulerEventKind.PhaseStarted => RuntimeEventKind.PhaseStarted,
            RuntimePhaseSchedulerEventKind.PhaseTimedOut => RuntimeEventKind.PhaseTimedOut,
            RuntimePhaseSchedulerEventKind.PhaseEnded => RuntimeEventKind.PhaseEnded,
            RuntimePhaseSchedulerEventKind.SessionCompleted => RuntimeEventKind.SessionCompleted,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown phase scheduler event kind."),
        };
    }

    private static IReadOnlyList<RuntimeEventFact> BuildSchedulerFacts(RuntimePhaseSchedulerEvent schedulerEvent)
    {
        var facts = new List<RuntimeEventFact>();

        if (schedulerEvent.CompletedPhase is not null)
        {
            facts.Add(new RuntimeEventFact(
                "phase_actual_duration",
                FormatDuration(schedulerEvent.CompletedPhase.ActualDuration)));
            facts.Add(new RuntimeEventFact(
                "phase_completion_cause",
                schedulerEvent.CompletedPhase.CompletionCause.ToString()));
        }

        if (schedulerEvent.TimeoutSnapshot is not null)
        {
            facts.Add(new RuntimeEventFact(
                "phase_started_at",
                FormatInstant(schedulerEvent.TimeoutSnapshot.StartedAt)));
            facts.Add(new RuntimeEventFact(
                "phase_deadline",
                FormatInstant(schedulerEvent.TimeoutSnapshot.Deadline)));
            facts.Add(new RuntimeEventFact(
                "phase_elapsed",
                FormatDuration(schedulerEvent.TimeoutSnapshot.Elapsed)));
            facts.Add(new RuntimeEventFact(
                "phase_remaining",
                FormatDuration(schedulerEvent.TimeoutSnapshot.Remaining)));

            if (schedulerEvent.TimeoutSnapshot.TimeoutEvent is not null)
            {
                facts.Add(new RuntimeEventFact(
                    "timeout_overtime",
                    FormatDuration(schedulerEvent.TimeoutSnapshot.TimeoutEvent.Overtime)));
            }
        }

        return facts.AsReadOnly();
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
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime event value.");
        }
    }

    private static bool IsTerminalEvent(RuntimeEventKind kind)
    {
        return kind is RuntimeEventKind.SessionAbandoned or RuntimeEventKind.SessionCompleted;
    }
}
