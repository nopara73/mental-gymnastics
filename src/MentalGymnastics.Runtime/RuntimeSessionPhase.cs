namespace MentalGymnastics.Runtime;

public enum RuntimeSessionPhaseKind
{
    InstructionPrep,
    EncodeWindow,
    ActiveWork,
    DelayWindow,
    CueResponse,
    ReconstructionInput,
    Audit,
    Rest,
    Recovery,
    Review,
}

public enum RuntimeSessionPhaseCompletionRule
{
    Manual,
    Timed,
    ManualOrTimed,
}

public enum RuntimeSessionPhaseCompletionCause
{
    Explicit,
    Timeout,
}

public enum RuntimeSessionPhaseInvalidCompletionReason
{
    AlreadyComplete,
    CompletedBeforePhaseStart,
    ScheduledDurationNotReached,
    PhaseDoesNotSupportTimeout,
}

public sealed class RuntimeSessionPhaseDefinition
{
    private RuntimeSessionPhaseDefinition(
        string id,
        RuntimeSessionPhaseKind kind,
        RuntimeSessionPhaseCompletionRule completionRule,
        RuntimeDuration? scheduledDuration)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Runtime phase id is required.", nameof(id));
        }

        EnsureDefined(kind, nameof(kind));
        EnsureDefined(completionRule, nameof(completionRule));

        if (scheduledDuration.HasValue && scheduledDuration.Value.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scheduledDuration),
                scheduledDuration,
                "Scheduled phase duration must be positive when provided.");
        }

        var requiresScheduledDuration =
            completionRule is RuntimeSessionPhaseCompletionRule.Timed or RuntimeSessionPhaseCompletionRule.ManualOrTimed;
        if (requiresScheduledDuration && !scheduledDuration.HasValue)
        {
            throw new ArgumentException(
                "Timed runtime phases require a scheduled duration.",
                nameof(scheduledDuration));
        }

        Id = id;
        Kind = kind;
        CompletionRule = completionRule;
        ScheduledDuration = scheduledDuration;
    }

    public string Id { get; }

    public RuntimeSessionPhaseKind Kind { get; }

    public RuntimeSessionPhaseCompletionRule CompletionRule { get; }

    public RuntimeDuration? ScheduledDuration { get; }

    public bool HasScheduledDuration => ScheduledDuration.HasValue;

    public static RuntimeSessionPhaseDefinition Manual(
        string id,
        RuntimeSessionPhaseKind kind)
    {
        return new RuntimeSessionPhaseDefinition(
            id,
            kind,
            RuntimeSessionPhaseCompletionRule.Manual,
            scheduledDuration: null);
    }

    public static RuntimeSessionPhaseDefinition Timed(
        string id,
        RuntimeSessionPhaseKind kind,
        RuntimeDuration scheduledDuration)
    {
        return new RuntimeSessionPhaseDefinition(
            id,
            kind,
            RuntimeSessionPhaseCompletionRule.Timed,
            scheduledDuration);
    }

    public static RuntimeSessionPhaseDefinition ManualOrTimed(
        string id,
        RuntimeSessionPhaseKind kind,
        RuntimeDuration? scheduledDuration)
    {
        return new RuntimeSessionPhaseDefinition(
            id,
            kind,
            RuntimeSessionPhaseCompletionRule.ManualOrTimed,
            scheduledDuration);
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime phase value.");
        }
    }
}

public sealed class RuntimeSessionPhasePlan
{
    public RuntimeSessionPhasePlan(IEnumerable<RuntimeSessionPhaseDefinition> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);

        var phaseArray = phases.ToArray();
        if (phaseArray.Length == 0)
        {
            throw new ArgumentException("A runtime phase plan must include at least one phase.", nameof(phases));
        }

        var phaseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var phase in phaseArray)
        {
            ArgumentNullException.ThrowIfNull(phase);

            if (!phaseIds.Add(phase.Id))
            {
                throw new ArgumentException("Runtime phase ids must be unique within a phase plan.", nameof(phases));
            }
        }

        Phases = Array.AsReadOnly(phaseArray);
        TotalScheduledDuration = new RuntimeDuration(
            TimeSpan.FromTicks(phaseArray
                .Where(phase => phase.ScheduledDuration.HasValue)
                .Sum(phase => phase.ScheduledDuration.GetValueOrDefault().Value.Ticks)));
    }

    public IReadOnlyList<RuntimeSessionPhaseDefinition> Phases { get; }

    public RuntimeDuration TotalScheduledDuration { get; }
}

public sealed record RuntimeCompletedSessionPhase(
    RuntimeSessionPhaseDefinition Definition,
    RuntimeInstant StartedAt,
    RuntimeInstant CompletedAt,
    RuntimeDuration ActualDuration,
    RuntimeSessionPhaseCompletionCause CompletionCause);

public sealed record RuntimeSessionPhaseCompletionResult(
    bool IsValid,
    RuntimeSessionPhaseSequence Sequence,
    RuntimeCompletedSessionPhase? CompletedPhase,
    RuntimeSessionPhaseInvalidCompletionReason? InvalidReason);

public sealed class RuntimeSessionPhaseSequence
{
    private readonly RuntimeCompletedSessionPhase[] _completedPhases;

    private RuntimeSessionPhaseSequence(
        RuntimeSessionPhasePlan plan,
        int currentPhaseIndex,
        RuntimeInstant currentPhaseStartedAt,
        RuntimeDuration currentPhasePausedDuration,
        RuntimeCompletedSessionPhase[] completedPhases)
    {
        Plan = plan;
        CurrentPhaseIndex = currentPhaseIndex;
        CurrentPhaseStartedAt = currentPhaseStartedAt;
        CurrentPhasePausedDuration = currentPhasePausedDuration;
        _completedPhases = completedPhases;
        CompletedPhases = Array.AsReadOnly(_completedPhases);
    }

    public RuntimeSessionPhasePlan Plan { get; }

    public int CurrentPhaseIndex { get; }

    public RuntimeInstant CurrentPhaseStartedAt { get; }

    public RuntimeDuration CurrentPhasePausedDuration { get; }

    public RuntimeInstant CurrentPhaseEffectiveStartedAt => CurrentPhaseStartedAt.Add(CurrentPhasePausedDuration);

    public IReadOnlyList<RuntimeCompletedSessionPhase> CompletedPhases { get; }

    public bool IsComplete => CurrentPhaseIndex >= Plan.Phases.Count;

    public RuntimeSessionPhaseDefinition? CurrentPhase => IsComplete ? null : Plan.Phases[CurrentPhaseIndex];

    public static RuntimeSessionPhaseSequence Start(
        RuntimeSessionPhasePlan plan,
        RuntimeInstant startedAt)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new RuntimeSessionPhaseSequence(
            plan,
            currentPhaseIndex: 0,
            startedAt,
            RuntimeDuration.Zero,
            []);
    }

    public static RuntimeSessionPhaseSequence Restore(
        RuntimeSessionPhasePlan plan,
        int currentPhaseIndex,
        RuntimeInstant currentPhaseStartedAt,
        RuntimeDuration currentPhasePausedDuration,
        IEnumerable<RuntimeCompletedSessionPhase> completedPhases)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(completedPhases);

        if (currentPhaseIndex < 0 || currentPhaseIndex > plan.Phases.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentPhaseIndex),
                currentPhaseIndex,
                "Restored runtime phase index must be inside the phase plan.");
        }

        var completedPhaseArray = completedPhases.ToArray();
        foreach (var completedPhase in completedPhaseArray)
        {
            ArgumentNullException.ThrowIfNull(completedPhase);
        }

        return new RuntimeSessionPhaseSequence(
            plan,
            currentPhaseIndex,
            currentPhaseStartedAt,
            currentPhasePausedDuration,
            completedPhaseArray);
    }

    public RuntimeSessionPhaseSequence AddCurrentPhasePause(RuntimeDuration pauseDuration)
    {
        return new RuntimeSessionPhaseSequence(
            Plan,
            CurrentPhaseIndex,
            CurrentPhaseStartedAt,
            new RuntimeDuration(CurrentPhasePausedDuration.Value + pauseDuration.Value),
            _completedPhases);
    }

    public RuntimeDuration CurrentPhaseElapsedAt(RuntimeInstant observedAt)
    {
        var elapsed = observedAt.ElapsedSince(CurrentPhaseStartedAt);
        if (elapsed.Value <= CurrentPhasePausedDuration.Value)
        {
            return RuntimeDuration.Zero;
        }

        return new RuntimeDuration(elapsed.Value - CurrentPhasePausedDuration.Value);
    }

    public RuntimeSessionPhaseCompletionResult TryCompleteCurrent(
        RuntimeInstant completedAt,
        RuntimeSessionPhaseCompletionCause completionCause)
    {
        EnsureDefined(completionCause, nameof(completionCause));

        if (IsComplete)
        {
            return Invalid(RuntimeSessionPhaseInvalidCompletionReason.AlreadyComplete);
        }

        if (completedAt.Offset < CurrentPhaseStartedAt.Offset)
        {
            return Invalid(RuntimeSessionPhaseInvalidCompletionReason.CompletedBeforePhaseStart);
        }

        var currentPhase = CurrentPhase!;
        var elapsed = CurrentPhaseElapsedAt(completedAt);
        var durationResult = ValidateDuration(currentPhase, elapsed, completionCause);
        if (durationResult is not null)
        {
            return Invalid(durationResult.Value);
        }

        var completedPhase = new RuntimeCompletedSessionPhase(
            currentPhase,
            CurrentPhaseStartedAt,
            completedAt,
            elapsed,
            completionCause);
        var completedPhases = new RuntimeCompletedSessionPhase[_completedPhases.Length + 1];
        Array.Copy(_completedPhases, completedPhases, _completedPhases.Length);
        completedPhases[^1] = completedPhase;

        var nextSequence = new RuntimeSessionPhaseSequence(
            Plan,
            CurrentPhaseIndex + 1,
            completedAt,
            RuntimeDuration.Zero,
            completedPhases);

        return new RuntimeSessionPhaseCompletionResult(
            IsValid: true,
            nextSequence,
            completedPhase,
            InvalidReason: null);
    }

    private static RuntimeSessionPhaseInvalidCompletionReason? ValidateDuration(
        RuntimeSessionPhaseDefinition phase,
        RuntimeDuration elapsed,
        RuntimeSessionPhaseCompletionCause completionCause)
    {
        if (completionCause == RuntimeSessionPhaseCompletionCause.Timeout &&
            phase.CompletionRule == RuntimeSessionPhaseCompletionRule.Manual)
        {
            return RuntimeSessionPhaseInvalidCompletionReason.PhaseDoesNotSupportTimeout;
        }

        if (phase.CompletionRule == RuntimeSessionPhaseCompletionRule.Manual)
        {
            return null;
        }

        if (phase.CompletionRule == RuntimeSessionPhaseCompletionRule.ManualOrTimed &&
            completionCause == RuntimeSessionPhaseCompletionCause.Explicit)
        {
            return null;
        }

        var scheduledDuration = phase.ScheduledDuration.GetValueOrDefault();
        if (elapsed.Value < scheduledDuration.Value)
        {
            return RuntimeSessionPhaseInvalidCompletionReason.ScheduledDurationNotReached;
        }

        return null;
    }

    private RuntimeSessionPhaseCompletionResult Invalid(RuntimeSessionPhaseInvalidCompletionReason reason)
    {
        return new RuntimeSessionPhaseCompletionResult(
            IsValid: false,
            this,
            CompletedPhase: null,
            reason);
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime phase value.");
        }
    }
}
