namespace MentalGymnastics.Runtime;

public enum RuntimePhaseSchedulerStatus
{
    NotStarted,
    Running,
    Paused,
    Completed,
}

public enum RuntimePhaseSchedulerEventKind
{
    PhaseStarted,
    PhaseTimedOut,
    PhaseEnded,
    SessionCompleted,
}

public enum RuntimePhaseSchedulerInvalidReason
{
    NotStarted,
    AlreadyStarted,
    Paused,
    NotPaused,
    AlreadyCompleted,
    InvalidPhaseCompletion,
}

public sealed record RuntimePhaseSchedulerEvent(
    RuntimePhaseSchedulerEventKind Kind,
    RuntimeInstant OccurredAt,
    RuntimeSessionDefinition SessionDefinition,
    RuntimeSessionPhaseDefinition? Phase,
    RuntimeCompletedSessionPhase? CompletedPhase,
    RuntimeTimedPhaseSnapshot? TimeoutSnapshot);

public sealed record RuntimePhaseSchedulerResult(
    bool IsValid,
    IReadOnlyList<RuntimePhaseSchedulerEvent> Events,
    RuntimePhaseSchedulerInvalidReason? InvalidReason,
    RuntimeSessionPhaseInvalidCompletionReason? PhaseInvalidReason);

public sealed class RuntimePhaseScheduler
{
    private readonly IRuntimeClock _clock;
    private RuntimeSessionPhaseSequence? _sequence;
    private RuntimeInstant? _pausedAt;

    public RuntimePhaseScheduler(
        RuntimeSessionDefinition sessionDefinition,
        RuntimeSessionPhasePlan phasePlan,
        IRuntimeClock clock)
    {
        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(phasePlan);
        ArgumentNullException.ThrowIfNull(clock);

        SessionDefinition = sessionDefinition;
        PhasePlan = phasePlan;
        _clock = clock;
    }

    public RuntimeSessionDefinition SessionDefinition { get; }

    public RuntimeSessionPhasePlan PhasePlan { get; }

    public RuntimePhaseSchedulerStatus Status { get; private set; } = RuntimePhaseSchedulerStatus.NotStarted;

    public bool IsComplete => Status == RuntimePhaseSchedulerStatus.Completed;

    public RuntimeSessionPhaseDefinition? CurrentPhase => _sequence?.CurrentPhase;

    public IReadOnlyList<RuntimeCompletedSessionPhase> CompletedPhases =>
        _sequence?.CompletedPhases ?? Array.Empty<RuntimeCompletedSessionPhase>();

    public RuntimePhaseSchedulerSnapshot CaptureSnapshot()
    {
        var capturedAt = _clock.Now;
        var observedAt = Status == RuntimePhaseSchedulerStatus.Paused
            ? _pausedAt.GetValueOrDefault(capturedAt)
            : capturedAt;
        var currentPhase = _sequence?.CurrentPhase;

        return new RuntimePhaseSchedulerSnapshot(
            Status,
            capturedAt,
            _sequence is null || _sequence.IsComplete ? null : _sequence.CurrentPhaseIndex,
            currentPhase?.Id,
            _sequence is null || _sequence.IsComplete ? null : _sequence.CurrentPhaseElapsedAt(observedAt),
            CompletedPhases);
    }

    public static RuntimePhaseScheduler Restore(
        RuntimeSessionDefinition sessionDefinition,
        RuntimeSessionPhasePlan phasePlan,
        IRuntimeClock clock,
        RuntimePhaseSchedulerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshotForRestore(phasePlan, snapshot);

        var scheduler = new RuntimePhaseScheduler(sessionDefinition, phasePlan, clock)
        {
            Status = snapshot.Status,
        };

        if (snapshot.CurrentPhaseIndex.HasValue)
        {
            var elapsed = snapshot.CurrentPhaseElapsed ?? RuntimeDuration.Zero;
            if (clock.Now.Offset < elapsed.Value)
            {
                throw new ArgumentException(
                    "Runtime restore clock cannot be earlier than the snapshotted active phase elapsed time.",
                    nameof(clock));
            }

            var currentPhaseStartedAt = new RuntimeInstant(clock.Now.Offset - elapsed.Value);
            scheduler._sequence = RuntimeSessionPhaseSequence.Restore(
                phasePlan,
                snapshot.CurrentPhaseIndex.Value,
                currentPhaseStartedAt,
                RuntimeDuration.Zero,
                snapshot.CompletedPhases);
        }
        else if (snapshot.Status == RuntimePhaseSchedulerStatus.Completed)
        {
            scheduler._sequence = RuntimeSessionPhaseSequence.Restore(
                phasePlan,
                phasePlan.Phases.Count,
                clock.Now,
                RuntimeDuration.Zero,
                snapshot.CompletedPhases);
        }

        scheduler._pausedAt = snapshot.Status == RuntimePhaseSchedulerStatus.Paused
            ? clock.Now
            : null;

        return scheduler;
    }

    private static void ValidateSnapshotForRestore(
        RuntimeSessionPhasePlan phasePlan,
        RuntimePhaseSchedulerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(phasePlan);

        if (snapshot.Status is RuntimePhaseSchedulerStatus.Running or RuntimePhaseSchedulerStatus.Paused)
        {
            if (!snapshot.CurrentPhaseIndex.HasValue || snapshot.CurrentPhaseId is null)
            {
                throw new ArgumentException(
                    "Runtime active phase snapshots must include the active phase index and id.",
                    nameof(snapshot));
            }
        }

        if (snapshot.CurrentPhaseIndex.HasValue)
        {
            var index = snapshot.CurrentPhaseIndex.Value;
            if (index >= phasePlan.Phases.Count)
            {
                if (snapshot.CurrentPhaseId is not null)
                {
                    throw new ArgumentException(
                        "Completed runtime phase snapshots must not include an active phase id.",
                        nameof(snapshot));
                }

                return;
            }

            var expectedPhaseId = phasePlan.Phases[index].Id;
            if (!string.Equals(snapshot.CurrentPhaseId, expectedPhaseId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Runtime phase snapshot active phase id must match the phase plan at the restored index.",
                    nameof(snapshot));
            }
        }
    }

    public RuntimePhaseSchedulerResult Start()
    {
        if (Status == RuntimePhaseSchedulerStatus.Running)
        {
            return Invalid(RuntimePhaseSchedulerInvalidReason.AlreadyStarted);
        }

        if (Status == RuntimePhaseSchedulerStatus.Completed)
        {
            return Invalid(RuntimePhaseSchedulerInvalidReason.AlreadyCompleted);
        }

        var startedAt = _clock.Now;
        _sequence = RuntimeSessionPhaseSequence.Start(PhasePlan, startedAt);
        Status = RuntimePhaseSchedulerStatus.Running;

        return Valid(
        [
            PhaseStarted(_sequence.CurrentPhase!, startedAt),
        ]);
    }

    public RuntimePhaseSchedulerResult CompleteCurrentPhase()
    {
        var ready = EnsureRunning();
        if (ready is not null)
        {
            return ready;
        }

        var completion = _sequence!.TryCompleteCurrent(
            _clock.Now,
            RuntimeSessionPhaseCompletionCause.Explicit);

        if (!completion.IsValid)
        {
            return Invalid(
                RuntimePhaseSchedulerInvalidReason.InvalidPhaseCompletion,
                completion.InvalidReason);
        }

        return ApplyValidCompletion(completion, timeoutSnapshot: null);
    }

    public RuntimePhaseSchedulerResult Pause()
    {
        var ready = EnsureRunning();
        if (ready is not null)
        {
            return ready;
        }

        _pausedAt = _clock.Now;
        Status = RuntimePhaseSchedulerStatus.Paused;

        return Valid([]);
    }

    public RuntimePhaseSchedulerResult Resume()
    {
        if (Status != RuntimePhaseSchedulerStatus.Paused)
        {
            return Invalid(Status == RuntimePhaseSchedulerStatus.Completed
                ? RuntimePhaseSchedulerInvalidReason.AlreadyCompleted
                : RuntimePhaseSchedulerInvalidReason.NotPaused);
        }

        var pausedDuration = _clock.Now.ElapsedSince(_pausedAt.GetValueOrDefault());
        _sequence = _sequence!.AddCurrentPhasePause(pausedDuration);
        _pausedAt = null;
        Status = RuntimePhaseSchedulerStatus.Running;

        return Valid([]);
    }

    public RuntimePhaseSchedulerResult AdvanceToCurrentTime()
    {
        var ready = EnsureRunning();
        if (ready is not null)
        {
            return ready;
        }

        var currentPhase = _sequence!.CurrentPhase;
        if (currentPhase is null ||
            currentPhase.CompletionRule == RuntimeSessionPhaseCompletionRule.Manual ||
            !currentPhase.ScheduledDuration.HasValue)
        {
            return Valid([]);
        }

        var now = _clock.Now;
        var elapsed = _sequence.CurrentPhaseElapsedAt(now);
        if (elapsed.Value < currentPhase.ScheduledDuration.Value.Value)
        {
            return Valid([]);
        }

        var timedPhase = new RuntimeTimedPhase(
            currentPhase.Id,
            _sequence.CurrentPhaseEffectiveStartedAt,
            currentPhase.ScheduledDuration.Value);
        var timeoutSnapshot = timedPhase.SnapshotAt(now);
        var completion = _sequence.TryCompleteCurrent(
            now,
            RuntimeSessionPhaseCompletionCause.Timeout);

        if (!completion.IsValid)
        {
            return Invalid(
                RuntimePhaseSchedulerInvalidReason.InvalidPhaseCompletion,
                completion.InvalidReason);
        }

        return ApplyValidCompletion(completion, timeoutSnapshot);
    }

    private RuntimePhaseSchedulerResult? EnsureRunning()
    {
        return Status switch
        {
            RuntimePhaseSchedulerStatus.NotStarted => Invalid(RuntimePhaseSchedulerInvalidReason.NotStarted),
            RuntimePhaseSchedulerStatus.Paused => Invalid(RuntimePhaseSchedulerInvalidReason.Paused),
            RuntimePhaseSchedulerStatus.Completed => Invalid(RuntimePhaseSchedulerInvalidReason.AlreadyCompleted),
            _ => null,
        };
    }

    private RuntimePhaseSchedulerResult ApplyValidCompletion(
        RuntimeSessionPhaseCompletionResult completion,
        RuntimeTimedPhaseSnapshot? timeoutSnapshot)
    {
        var completedPhase = completion.CompletedPhase!;
        var events = new List<RuntimePhaseSchedulerEvent>();

        if (timeoutSnapshot is not null)
        {
            events.Add(new RuntimePhaseSchedulerEvent(
                RuntimePhaseSchedulerEventKind.PhaseTimedOut,
                completedPhase.CompletedAt,
                SessionDefinition,
                completedPhase.Definition,
                completedPhase,
                timeoutSnapshot));
        }

        events.Add(new RuntimePhaseSchedulerEvent(
            RuntimePhaseSchedulerEventKind.PhaseEnded,
            completedPhase.CompletedAt,
            SessionDefinition,
            completedPhase.Definition,
            completedPhase,
            TimeoutSnapshot: null));

        _sequence = completion.Sequence;

        if (_sequence.IsComplete)
        {
            Status = RuntimePhaseSchedulerStatus.Completed;
            events.Add(new RuntimePhaseSchedulerEvent(
                RuntimePhaseSchedulerEventKind.SessionCompleted,
                completedPhase.CompletedAt,
                SessionDefinition,
                Phase: null,
                CompletedPhase: null,
                TimeoutSnapshot: null));
        }
        else
        {
            events.Add(PhaseStarted(_sequence.CurrentPhase!, completedPhase.CompletedAt));
        }

        return Valid(events);
    }

    private RuntimePhaseSchedulerEvent PhaseStarted(
        RuntimeSessionPhaseDefinition phase,
        RuntimeInstant occurredAt)
    {
        return new RuntimePhaseSchedulerEvent(
            RuntimePhaseSchedulerEventKind.PhaseStarted,
            occurredAt,
            SessionDefinition,
            phase,
            CompletedPhase: null,
            TimeoutSnapshot: null);
    }

    private static RuntimePhaseSchedulerResult Valid(IReadOnlyList<RuntimePhaseSchedulerEvent> events)
    {
        return new RuntimePhaseSchedulerResult(
            IsValid: true,
            events,
            InvalidReason: null,
            PhaseInvalidReason: null);
    }

    private static RuntimePhaseSchedulerResult Invalid(
        RuntimePhaseSchedulerInvalidReason invalidReason,
        RuntimeSessionPhaseInvalidCompletionReason? phaseInvalidReason = null)
    {
        return new RuntimePhaseSchedulerResult(
            IsValid: false,
            Events: Array.Empty<RuntimePhaseSchedulerEvent>(),
            invalidReason,
            phaseInvalidReason);
    }
}
