namespace MentalGymnastics.Runtime;

public sealed class RuntimePhaseSchedulerSnapshot
{
    public RuntimePhaseSchedulerSnapshot(
        RuntimePhaseSchedulerStatus status,
        RuntimeInstant capturedAt,
        int? currentPhaseIndex,
        string? currentPhaseId,
        RuntimeDuration? currentPhaseElapsed,
        IEnumerable<RuntimeCompletedSessionPhase> completedPhases)
    {
        EnsureDefined(status, nameof(status));
        ArgumentNullException.ThrowIfNull(completedPhases);

        if (currentPhaseIndex.HasValue && currentPhaseIndex.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentPhaseIndex),
                currentPhaseIndex,
                "Runtime snapshot phase index cannot be negative.");
        }

        if (currentPhaseId is not null && string.IsNullOrWhiteSpace(currentPhaseId))
        {
            throw new ArgumentException("Runtime snapshot current phase id cannot be blank.", nameof(currentPhaseId));
        }

        var phaseArray = completedPhases.ToArray();
        foreach (var phase in phaseArray)
        {
            ArgumentNullException.ThrowIfNull(phase);
        }

        Status = status;
        CapturedAt = capturedAt;
        CurrentPhaseIndex = currentPhaseIndex;
        CurrentPhaseId = currentPhaseId;
        CurrentPhaseElapsed = currentPhaseElapsed;
        CompletedPhases = Array.AsReadOnly(phaseArray);
    }

    public RuntimePhaseSchedulerStatus Status { get; }

    public RuntimeInstant CapturedAt { get; }

    public int? CurrentPhaseIndex { get; }

    public string? CurrentPhaseId { get; }

    public RuntimeDuration? CurrentPhaseElapsed { get; }

    public IReadOnlyList<RuntimeCompletedSessionPhase> CompletedPhases { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime snapshot value.");
        }
    }
}

public sealed class RuntimeSessionSnapshot
{
    public RuntimeSessionSnapshot(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        RuntimeSessionLifecycleState lifecycleState,
        RuntimeInputCommandOptions inputOptions,
        RuntimeSessionPhasePlan phasePlan,
        RuntimePhaseSchedulerSnapshot phaseScheduler,
        IEnumerable<RuntimeEvent> runtimeEvents,
        long? lastCorrectableEventSequenceNumber,
        RuntimeInstant capturedAt)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session snapshot id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(lifecycleState);
        ArgumentNullException.ThrowIfNull(inputOptions);
        ArgumentNullException.ThrowIfNull(phasePlan);
        ArgumentNullException.ThrowIfNull(phaseScheduler);
        ArgumentNullException.ThrowIfNull(runtimeEvents);

        if (lastCorrectableEventSequenceNumber.HasValue &&
            lastCorrectableEventSequenceNumber.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastCorrectableEventSequenceNumber),
                lastCorrectableEventSequenceNumber,
                "Last correctable event sequence number must be positive when present.");
        }

        var eventArray = runtimeEvents.ToArray();
        foreach (var runtimeEvent in eventArray)
        {
            ArgumentNullException.ThrowIfNull(runtimeEvent);
            if (!string.Equals(runtimeEvent.SessionId, sessionId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Runtime snapshot events must belong to the snapshotted session.",
                    nameof(runtimeEvents));
            }
        }

        var evidenceFacts = eventArray
            .SelectMany(runtimeEvent => runtimeEvent.Facts)
            .ToArray();
        if (RuntimeSessionCompletionResultGenerator.ContainsProgressionDecisionFact(evidenceFacts))
        {
            throw new ArgumentException(
                "Runtime snapshots must not contain progression or gate decision facts.",
                nameof(runtimeEvents));
        }

        SessionId = sessionId;
        SessionDefinition = sessionDefinition;
        LifecycleState = lifecycleState;
        InputOptions = inputOptions;
        PhasePlan = phasePlan;
        PhaseScheduler = phaseScheduler;
        RuntimeEvents = Array.AsReadOnly(eventArray);
        EvidenceFacts = Array.AsReadOnly(evidenceFacts);
        LastCorrectableEventSequenceNumber = lastCorrectableEventSequenceNumber;
        CapturedAt = capturedAt;
        GeneratedDrillInstance = sessionDefinition.GeneratedDrillInstance;
    }

    public string SessionId { get; }

    public RuntimeSessionDefinition SessionDefinition { get; }

    public RuntimeSessionLifecycleState LifecycleState { get; }

    public RuntimeInputCommandOptions InputOptions { get; }

    public RuntimeSessionPhasePlan PhasePlan { get; }

    public RuntimePhaseSchedulerSnapshot PhaseScheduler { get; }

    public IReadOnlyList<RuntimeEvent> RuntimeEvents { get; }

    public IReadOnlyList<RuntimeEventFact> EvidenceFacts { get; }

    public long? LastCorrectableEventSequenceNumber { get; }

    public RuntimeInstant CapturedAt { get; }

    public RuntimeGeneratedDrillInstanceIdentity? GeneratedDrillInstance { get; }
}

public sealed class RuntimeCueStateSnapshot
{
    public RuntimeCueStateSnapshot(
        string cueId,
        RuntimeInstant? presentedAt,
        string? phaseId,
        RuntimeSessionPhaseKind? phaseKind,
        long? responseEventSequenceNumber)
    {
        if (string.IsNullOrWhiteSpace(cueId))
        {
            throw new ArgumentException("Runtime cue snapshot id is required.", nameof(cueId));
        }

        if (phaseId is not null && string.IsNullOrWhiteSpace(phaseId))
        {
            throw new ArgumentException("Runtime cue snapshot phase id cannot be blank.", nameof(phaseId));
        }

        if (phaseKind.HasValue)
        {
            EnsureDefined(phaseKind.Value, nameof(phaseKind));
        }

        if (responseEventSequenceNumber.HasValue &&
            responseEventSequenceNumber.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseEventSequenceNumber),
                responseEventSequenceNumber,
                "Runtime cue response event sequence number must be positive when present.");
        }

        CueId = cueId;
        PresentedAt = presentedAt;
        PhaseId = phaseId;
        PhaseKind = phaseKind;
        ResponseEventSequenceNumber = responseEventSequenceNumber;
    }

    public string CueId { get; }

    public RuntimeInstant? PresentedAt { get; }

    public string? PhaseId { get; }

    public RuntimeSessionPhaseKind? PhaseKind { get; }

    public long? ResponseEventSequenceNumber { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime cue snapshot value.");
        }
    }
}

public sealed class RuntimeCueSchedulerSnapshot
{
    public RuntimeCueSchedulerSnapshot(
        RuntimeGeneratedDrillInstanceIdentity generatedDrillInstance,
        RuntimeInstant capturedAt,
        bool isPaused,
        RuntimeDuration elapsedPauseDuration,
        IEnumerable<RuntimeCueStateSnapshot> cueStates,
        IEnumerable<RuntimeEvent> runtimeEvents)
    {
        ArgumentNullException.ThrowIfNull(generatedDrillInstance);
        ArgumentNullException.ThrowIfNull(cueStates);
        ArgumentNullException.ThrowIfNull(runtimeEvents);

        var cueStateArray = cueStates.ToArray();
        if (cueStateArray.Length == 0)
        {
            throw new ArgumentException("Runtime cue scheduler snapshots require cue states.", nameof(cueStates));
        }

        foreach (var cueState in cueStateArray)
        {
            ArgumentNullException.ThrowIfNull(cueState);
        }

        var duplicateCue = cueStateArray
            .GroupBy(state => state.CueId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCue is not null)
        {
            throw new ArgumentException("Runtime cue scheduler snapshot cue ids must be unique.", nameof(cueStates));
        }

        var eventArray = runtimeEvents.ToArray();
        foreach (var runtimeEvent in eventArray)
        {
            ArgumentNullException.ThrowIfNull(runtimeEvent);
        }

        var evidenceFacts = eventArray
            .SelectMany(runtimeEvent => runtimeEvent.Facts)
            .ToArray();
        if (RuntimeSessionCompletionResultGenerator.ContainsProgressionDecisionFact(evidenceFacts))
        {
            throw new ArgumentException(
                "Runtime cue snapshots must not contain progression or gate decision facts.",
                nameof(runtimeEvents));
        }

        GeneratedDrillInstance = generatedDrillInstance;
        CapturedAt = capturedAt;
        IsPaused = isPaused;
        ElapsedPauseDuration = elapsedPauseDuration;
        CueStates = Array.AsReadOnly(cueStateArray);
        RuntimeEvents = Array.AsReadOnly(eventArray);
        EvidenceFacts = Array.AsReadOnly(evidenceFacts);
        PendingCueIds = Array.AsReadOnly(cueStateArray
            .Where(state => !state.PresentedAt.HasValue)
            .Select(state => state.CueId)
            .ToArray());
    }

    public RuntimeGeneratedDrillInstanceIdentity GeneratedDrillInstance { get; }

    public RuntimeInstant CapturedAt { get; }

    public bool IsPaused { get; }

    public RuntimeDuration ElapsedPauseDuration { get; }

    public IReadOnlyList<RuntimeCueStateSnapshot> CueStates { get; }

    public IReadOnlyList<RuntimeEvent> RuntimeEvents { get; }

    public IReadOnlyList<RuntimeEventFact> EvidenceFacts { get; }

    public IReadOnlyList<string> PendingCueIds { get; }
}
