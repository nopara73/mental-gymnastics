using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public sealed record LocalRuntimeEventFactRecord
{
    public LocalRuntimeEventFactRecord(string name, string value)
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

public sealed class LocalRuntimeGeneratedDrillInstanceIdentityRecord
{
    public LocalRuntimeGeneratedDrillInstanceIdentityRecord(
        string instanceId,
        LocalGeneratedDrillContentIdentity contentIdentity)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Generated drill instance id is required.", nameof(instanceId));
        }

        ArgumentNullException.ThrowIfNull(contentIdentity);

        InstanceId = instanceId;
        ContentIdentity = contentIdentity;
    }

    public string InstanceId { get; }

    public LocalGeneratedDrillContentIdentity ContentIdentity { get; }
}

public sealed class LocalRuntimeSessionDefinitionRecord
{
    public LocalRuntimeSessionDefinitionRecord(
        SessionType sessionType,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        IReadOnlyList<LoadVariable> loadVariables,
        BranchLevelStandard standard,
        IReadOnlyList<CriticalConstraint> criticalConstraints,
        LocalRuntimeGeneratedDrillInstanceIdentityRecord? generatedDrillInstance = null,
        DrillId? sourceDrill = null)
    {
        ArgumentNullException.ThrowIfNull(loadVariables);
        ArgumentNullException.ThrowIfNull(standard);
        ArgumentNullException.ThrowIfNull(criticalConstraints);

        var loadVariableArray = loadVariables.ToArray();
        if (loadVariableArray.Length == 0)
        {
            throw new ArgumentException(
                "Runtime session snapshots must preserve load variables.",
                nameof(loadVariables));
        }

        if (loadVariableArray.Any(variable =>
                string.IsNullOrWhiteSpace(variable.Name) ||
                string.IsNullOrWhiteSpace(variable.Value)))
        {
            throw new ArgumentException(
                "Runtime session snapshot load variables must include a name and value.",
                nameof(loadVariables));
        }

        if (standard.Branch != branch || standard.Level != level)
        {
            throw new ArgumentException(
                "Runtime session snapshot standards must match the session branch and level.",
                nameof(standard));
        }

        var criticalConstraintArray = criticalConstraints.ToArray();
        if (criticalConstraintArray.Length == 0)
        {
            throw new ArgumentException(
                "Runtime session snapshots must preserve honesty constraints.",
                nameof(criticalConstraints));
        }

        if (criticalConstraintArray.Any(constraint => string.IsNullOrWhiteSpace(constraint.Description)))
        {
            throw new ArgumentException(
                "Runtime session snapshot critical constraints must include descriptions.",
                nameof(criticalConstraints));
        }

        if (generatedDrillInstance is not null &&
            (generatedDrillInstance.ContentIdentity.Branch != branch ||
                generatedDrillInstance.ContentIdentity.Level != level ||
                generatedDrillInstance.ContentIdentity.Drill != drill))
        {
            throw new ArgumentException(
                "Runtime session snapshot generated drill identity must match branch, level, and drill.",
                nameof(generatedDrillInstance));
        }

        if (sourceDrill.HasValue &&
            (branch != BranchCode.AI ||
                sourceDrill.Value is not
                    (DrillId.FH2DistractorHold or DrillId.FS2InvalidCueFilter or DrillId.IR2ExceptionRule)))
        {
            throw new ArgumentException(
                "Only Affective Interference snapshots may identify an executable foundational source drill.",
                nameof(sourceDrill));
        }

        SessionType = sessionType;
        Branch = branch;
        Level = level;
        Drill = drill;
        LoadVariables = Array.AsReadOnly(loadVariableArray);
        Standard = standard;
        CriticalConstraints = Array.AsReadOnly(criticalConstraintArray);
        GeneratedDrillInstance = generatedDrillInstance;
        SourceDrill = sourceDrill;
    }

    public SessionType SessionType { get; }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public BranchLevelStandard Standard { get; }

    public IReadOnlyList<CriticalConstraint> CriticalConstraints { get; }

    public LocalRuntimeGeneratedDrillInstanceIdentityRecord? GeneratedDrillInstance { get; }

    public DrillId? SourceDrill { get; }
}

public sealed class LocalRuntimeLifecycleStateRecord
{
    public LocalRuntimeLifecycleStateRecord(string status, bool pauseAllowed)
    {
        Status = Required(status, nameof(status));
        PauseAllowed = pauseAllowed;
    }

    public string Status { get; }

    public bool PauseAllowed { get; }

    private static string Required(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Runtime lifecycle state value is required.", parameterName)
            : value;
    }
}

public sealed class LocalRuntimeInputOptionsRecord
{
    public LocalRuntimeInputOptionsRecord(
        bool pauseAllowed,
        TimeSpan correctionWindow,
        IReadOnlyList<string> pauseAllowedPhaseKinds)
    {
        ArgumentNullException.ThrowIfNull(pauseAllowedPhaseKinds);

        if (correctionWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correctionWindow),
                correctionWindow,
                "Runtime correction window must be positive.");
        }

        var phaseKinds = pauseAllowedPhaseKinds
            .Select(RequiredPhaseKind)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        PauseAllowed = pauseAllowed;
        CorrectionWindow = correctionWindow;
        PauseAllowedPhaseKinds = Array.AsReadOnly(phaseKinds);
    }

    public bool PauseAllowed { get; }

    public TimeSpan CorrectionWindow { get; }

    public IReadOnlyList<string> PauseAllowedPhaseKinds { get; }

    private static string RequiredPhaseKind(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Runtime pause-allowed phase kind cannot be blank.")
            : value;
    }
}

public sealed class LocalRuntimePhaseDefinitionRecord
{
    public LocalRuntimePhaseDefinitionRecord(
        string id,
        string kind,
        string completionRule,
        TimeSpan? scheduledDuration)
    {
        if (scheduledDuration.HasValue && scheduledDuration.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scheduledDuration),
                scheduledDuration,
                "Runtime phase scheduled duration must be positive when present.");
        }

        Id = Required(id, nameof(id));
        Kind = Required(kind, nameof(kind));
        CompletionRule = Required(completionRule, nameof(completionRule));
        ScheduledDuration = scheduledDuration;
    }

    public string Id { get; }

    public string Kind { get; }

    public string CompletionRule { get; }

    public TimeSpan? ScheduledDuration { get; }

    private static string Required(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Runtime phase value is required.", parameterName)
            : value;
    }
}

public sealed class LocalRuntimePhasePlanRecord
{
    public LocalRuntimePhasePlanRecord(IReadOnlyList<LocalRuntimePhaseDefinitionRecord> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);

        var phaseArray = phases.ToArray();
        if (phaseArray.Length == 0)
        {
            throw new ArgumentException("Runtime phase plans require at least one phase.", nameof(phases));
        }

        foreach (var phase in phaseArray)
        {
            ArgumentNullException.ThrowIfNull(phase);
        }

        var duplicatePhase = phaseArray
            .GroupBy(phase => phase.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePhase is not null)
        {
            throw new ArgumentException("Runtime phase ids must be unique.", nameof(phases));
        }

        Phases = Array.AsReadOnly(phaseArray);
    }

    public IReadOnlyList<LocalRuntimePhaseDefinitionRecord> Phases { get; }
}

public sealed class LocalRuntimeCompletedPhaseRecord
{
    public LocalRuntimeCompletedPhaseRecord(
        LocalRuntimePhaseDefinitionRecord definition,
        TimeSpan startedAt,
        TimeSpan completedAt,
        TimeSpan actualDuration,
        string completionCause)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (startedAt < TimeSpan.Zero ||
            completedAt < TimeSpan.Zero ||
            actualDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualDuration),
                actualDuration,
                "Runtime phase timing values cannot be negative.");
        }

        if (completedAt < startedAt)
        {
            throw new ArgumentException("Runtime completed phases cannot finish before they start.", nameof(completedAt));
        }

        Definition = definition;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        ActualDuration = actualDuration;
        CompletionCause = Required(completionCause, nameof(completionCause));
    }

    public LocalRuntimePhaseDefinitionRecord Definition { get; }

    public TimeSpan StartedAt { get; }

    public TimeSpan CompletedAt { get; }

    public TimeSpan ActualDuration { get; }

    public string CompletionCause { get; }

    private static string Required(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Runtime completed phase cause is required.", parameterName)
            : value;
    }
}

public sealed class LocalRuntimePhaseSchedulerSnapshotRecord
{
    public LocalRuntimePhaseSchedulerSnapshotRecord(
        string status,
        TimeSpan capturedAt,
        int? currentPhaseIndex,
        string? currentPhaseId,
        TimeSpan? currentPhaseElapsed,
        IReadOnlyList<LocalRuntimeCompletedPhaseRecord> completedPhases)
    {
        ArgumentNullException.ThrowIfNull(completedPhases);

        if (capturedAt < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capturedAt),
                capturedAt,
                "Runtime snapshot timing values cannot be negative.");
        }

        if (currentPhaseIndex.HasValue && currentPhaseIndex.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentPhaseIndex),
                currentPhaseIndex,
                "Runtime phase index cannot be negative.");
        }

        var completedPhaseArray = completedPhases.ToArray();
        foreach (var phase in completedPhaseArray)
        {
            ArgumentNullException.ThrowIfNull(phase);
        }

        Status = Required(status, nameof(status));
        CapturedAt = capturedAt;
        CurrentPhaseIndex = currentPhaseIndex;
        CurrentPhaseId = NormalizeOptional(currentPhaseId);
        CurrentPhaseElapsed = currentPhaseElapsed;
        CompletedPhases = Array.AsReadOnly(completedPhaseArray);
    }

    public string Status { get; }

    public TimeSpan CapturedAt { get; }

    public int? CurrentPhaseIndex { get; }

    public string? CurrentPhaseId { get; }

    public TimeSpan? CurrentPhaseElapsed { get; }

    public IReadOnlyList<LocalRuntimeCompletedPhaseRecord> CompletedPhases { get; }

    private static string Required(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Runtime phase scheduler status is required.", parameterName)
            : value;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public sealed class LocalRuntimeEventRecord
{
    public LocalRuntimeEventRecord(
        string sessionId,
        long sequenceNumber,
        string kind,
        TimeSpan occurredAt,
        string? phaseId = null,
        string? phaseKind = null,
        IReadOnlyList<LocalRuntimeEventFactRecord>? facts = null)
    {
        if (sequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                sequenceNumber,
                "Runtime event sequence number must be positive.");
        }

        if (occurredAt < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurredAt),
                occurredAt,
                "Runtime event occurrence cannot be negative.");
        }

        var factArray = (facts ?? []).ToArray();
        foreach (var fact in factArray)
        {
            ArgumentNullException.ThrowIfNull(fact);
        }

        SessionId = Required(sessionId, nameof(sessionId));
        SequenceNumber = sequenceNumber;
        Kind = Required(kind, nameof(kind));
        OccurredAt = occurredAt;
        PhaseId = NormalizeOptional(phaseId);
        PhaseKind = NormalizeOptional(phaseKind);
        Facts = Array.AsReadOnly(factArray);
    }

    public string SessionId { get; }

    public long SequenceNumber { get; }

    public string Kind { get; }

    public TimeSpan OccurredAt { get; }

    public string? PhaseId { get; }

    public string? PhaseKind { get; }

    public IReadOnlyList<LocalRuntimeEventFactRecord> Facts { get; }

    private static string Required(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Runtime event value is required.", parameterName)
            : value;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public sealed class LocalRuntimeCueStateSnapshotRecord
{
    public LocalRuntimeCueStateSnapshotRecord(
        string cueId,
        TimeSpan? presentedAt,
        string? phaseId,
        string? phaseKind,
        long? responseEventSequenceNumber)
    {
        if (presentedAt.HasValue && presentedAt.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(presentedAt),
                presentedAt,
                "Runtime cue presentation time cannot be negative.");
        }

        if (responseEventSequenceNumber.HasValue && responseEventSequenceNumber.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseEventSequenceNumber),
                responseEventSequenceNumber,
                "Runtime cue response event sequence number must be positive.");
        }

        CueId = string.IsNullOrWhiteSpace(cueId)
            ? throw new ArgumentException("Runtime cue id is required.", nameof(cueId))
            : cueId;
        PresentedAt = presentedAt;
        PhaseId = string.IsNullOrWhiteSpace(phaseId) ? null : phaseId;
        PhaseKind = string.IsNullOrWhiteSpace(phaseKind) ? null : phaseKind;
        ResponseEventSequenceNumber = responseEventSequenceNumber;
    }

    public string CueId { get; }

    public TimeSpan? PresentedAt { get; }

    public string? PhaseId { get; }

    public string? PhaseKind { get; }

    public long? ResponseEventSequenceNumber { get; }
}

public sealed class LocalRuntimeCueSchedulerSnapshotRecord
{
    public LocalRuntimeCueSchedulerSnapshotRecord(
        LocalRuntimeGeneratedDrillInstanceIdentityRecord generatedDrillInstance,
        TimeSpan capturedAt,
        bool isPaused,
        TimeSpan elapsedPauseDuration,
        IReadOnlyList<LocalRuntimeCueStateSnapshotRecord> cueStates,
        IReadOnlyList<LocalRuntimeEventRecord> runtimeEvents,
        IReadOnlyList<LocalRuntimeEventFactRecord> evidenceFacts)
    {
        ArgumentNullException.ThrowIfNull(generatedDrillInstance);
        ArgumentNullException.ThrowIfNull(cueStates);
        ArgumentNullException.ThrowIfNull(runtimeEvents);
        ArgumentNullException.ThrowIfNull(evidenceFacts);

        if (capturedAt < TimeSpan.Zero ||
            elapsedPauseDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capturedAt),
                capturedAt,
                "Runtime cue snapshot timing values cannot be negative.");
        }

        var cueStateArray = cueStates.ToArray();
        if (cueStateArray.Length == 0)
        {
            throw new ArgumentException("Runtime cue snapshots require cue states.", nameof(cueStates));
        }

        foreach (var cueState in cueStateArray)
        {
            ArgumentNullException.ThrowIfNull(cueState);
        }

        var duplicateCue = cueStateArray
            .GroupBy(cueState => cueState.CueId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCue is not null)
        {
            throw new ArgumentException("Runtime cue snapshot cue ids must be unique.", nameof(cueStates));
        }

        var eventArray = runtimeEvents.ToArray();
        var factArray = evidenceFacts.ToArray();
        ValidateEvidenceDoesNotContainProgressionDecision(factArray);

        GeneratedDrillInstance = generatedDrillInstance;
        CapturedAt = capturedAt;
        IsPaused = isPaused;
        ElapsedPauseDuration = elapsedPauseDuration;
        CueStates = Array.AsReadOnly(cueStateArray);
        RuntimeEvents = Array.AsReadOnly(eventArray);
        EvidenceFacts = Array.AsReadOnly(factArray);
        PendingCueIds = Array.AsReadOnly(cueStateArray
            .Where(cueState => !cueState.PresentedAt.HasValue)
            .Select(cueState => cueState.CueId)
            .ToArray());
    }

    public LocalRuntimeGeneratedDrillInstanceIdentityRecord GeneratedDrillInstance { get; }

    public TimeSpan CapturedAt { get; }

    public bool IsPaused { get; }

    public TimeSpan ElapsedPauseDuration { get; }

    public IReadOnlyList<LocalRuntimeCueStateSnapshotRecord> CueStates { get; }

    public IReadOnlyList<LocalRuntimeEventRecord> RuntimeEvents { get; }

    public IReadOnlyList<LocalRuntimeEventFactRecord> EvidenceFacts { get; }

    public IReadOnlyList<string> PendingCueIds { get; }

    internal static void ValidateEvidenceDoesNotContainProgressionDecision(
        IEnumerable<LocalRuntimeEventFactRecord> facts)
    {
        foreach (var fact in facts)
        {
            ArgumentNullException.ThrowIfNull(fact);
            if (LocalActiveRuntimeSessionSnapshotRecord.IsProgressionDecisionFact(fact.Name))
            {
                throw new ArgumentException(
                    "Active runtime snapshots must not contain progression or gate decision facts.",
                    nameof(facts));
            }
        }
    }
}

public sealed class LocalActiveRuntimeSessionSnapshotRecord
{
    private static readonly string[] ProgressionDecisionFactNames =
    [
        "advancement",
        "advancement_decision",
        "branch_level_state",
        "gate_outcome",
        "gateOutcome",
        "maintenance_currency",
        "ownership",
        "progression_decision",
    ];

    public LocalActiveRuntimeSessionSnapshotRecord(
        string sessionId,
        TimeSpan capturedAt,
        LocalRuntimeSessionDefinitionRecord sessionDefinition,
        LocalRuntimeLifecycleStateRecord lifecycleState,
        LocalRuntimeInputOptionsRecord inputOptions,
        LocalRuntimePhasePlanRecord phasePlan,
        LocalRuntimePhaseSchedulerSnapshotRecord phaseScheduler,
        IReadOnlyList<LocalRuntimeEventRecord> runtimeEvents,
        IReadOnlyList<LocalRuntimeEventFactRecord> evidenceFacts,
        long? lastCorrectableEventSequenceNumber,
        LocalRuntimeCueSchedulerSnapshotRecord? cueScheduler = null)
    {
        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(lifecycleState);
        ArgumentNullException.ThrowIfNull(inputOptions);
        ArgumentNullException.ThrowIfNull(phasePlan);
        ArgumentNullException.ThrowIfNull(phaseScheduler);
        ArgumentNullException.ThrowIfNull(runtimeEvents);
        ArgumentNullException.ThrowIfNull(evidenceFacts);

        if (capturedAt < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capturedAt),
                capturedAt,
                "Runtime snapshot capture time cannot be negative.");
        }

        if (lastCorrectableEventSequenceNumber.HasValue &&
            lastCorrectableEventSequenceNumber.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastCorrectableEventSequenceNumber),
                lastCorrectableEventSequenceNumber,
                "Last correctable runtime event sequence number must be positive when present.");
        }

        var normalizedSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? throw new ArgumentException("Runtime session snapshot id is required.", nameof(sessionId))
            : sessionId;
        var eventArray = runtimeEvents.ToArray();
        var factArray = evidenceFacts.ToArray();

        ValidateRuntimeEvents(normalizedSessionId, eventArray);
        LocalRuntimeCueSchedulerSnapshotRecord.ValidateEvidenceDoesNotContainProgressionDecision(factArray);

        if (lastCorrectableEventSequenceNumber.HasValue &&
            eventArray.All(runtimeEvent => runtimeEvent.SequenceNumber != lastCorrectableEventSequenceNumber.Value))
        {
            throw new ArgumentException(
                "Active runtime snapshot references a missing correctable event.",
                nameof(lastCorrectableEventSequenceNumber));
        }

        if (cueScheduler is not null &&
            sessionDefinition.GeneratedDrillInstance is not null &&
            !string.Equals(
                cueScheduler.GeneratedDrillInstance.InstanceId,
                sessionDefinition.GeneratedDrillInstance.InstanceId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Runtime cue snapshot generated instance must match the active session.",
                nameof(cueScheduler));
        }

        SessionId = normalizedSessionId;
        CapturedAt = capturedAt;
        SessionDefinition = sessionDefinition;
        LifecycleState = lifecycleState;
        InputOptions = inputOptions;
        PhasePlan = phasePlan;
        PhaseScheduler = phaseScheduler;
        RuntimeEvents = Array.AsReadOnly(eventArray);
        EvidenceFacts = Array.AsReadOnly(factArray);
        LastCorrectableEventSequenceNumber = lastCorrectableEventSequenceNumber;
        CueScheduler = cueScheduler;
    }

    public string SessionId { get; }

    public TimeSpan CapturedAt { get; }

    public LocalRuntimeSessionDefinitionRecord SessionDefinition { get; }

    public LocalRuntimeLifecycleStateRecord LifecycleState { get; }

    public LocalRuntimeInputOptionsRecord InputOptions { get; }

    public LocalRuntimePhasePlanRecord PhasePlan { get; }

    public LocalRuntimePhaseSchedulerSnapshotRecord PhaseScheduler { get; }

    public IReadOnlyList<LocalRuntimeEventRecord> RuntimeEvents { get; }

    public IReadOnlyList<LocalRuntimeEventFactRecord> EvidenceFacts { get; }

    public long? LastCorrectableEventSequenceNumber { get; }

    public LocalRuntimeCueSchedulerSnapshotRecord? CueScheduler { get; }

    internal static bool IsProgressionDecisionFact(string factName)
    {
        return ProgressionDecisionFactNames.Contains(factName, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateRuntimeEvents(
        string sessionId,
        IReadOnlyList<LocalRuntimeEventRecord> runtimeEvents)
    {
        if (runtimeEvents.Count == 0)
        {
            throw new ArgumentException("Active runtime snapshots require runtime event history.", nameof(runtimeEvents));
        }

        var previousOccurredAt = TimeSpan.Zero;
        for (var index = 0; index < runtimeEvents.Count; index++)
        {
            var runtimeEvent = runtimeEvents[index];
            ArgumentNullException.ThrowIfNull(runtimeEvent);

            if (!string.Equals(runtimeEvent.SessionId, sessionId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Runtime snapshot events must belong to the snapshotted session.", nameof(runtimeEvents));
            }

            if (runtimeEvent.SequenceNumber != index + 1L)
            {
                throw new ArgumentException("Runtime snapshot event sequence numbers must be contiguous.", nameof(runtimeEvents));
            }

            if (index > 0 && runtimeEvent.OccurredAt < previousOccurredAt)
            {
                throw new ArgumentException("Runtime snapshot events must be chronological.", nameof(runtimeEvents));
            }

            foreach (var fact in runtimeEvent.Facts)
            {
                if (IsProgressionDecisionFact(fact.Name))
                {
                    throw new ArgumentException(
                        "Active runtime snapshots must not contain progression or gate decision facts.",
                        nameof(runtimeEvents));
                }
            }

            previousOccurredAt = runtimeEvent.OccurredAt;
        }
    }
}

public sealed class LocalActiveRuntimeSessionSnapshotStore
{
    private const string ActiveRuntimeSessionSnapshotsPropertyName = "ActiveRuntimeSessionSnapshots";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalActiveRuntimeSessionSnapshotStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask SaveAsync(
        LocalActiveRuntimeSessionSnapshotRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = ReadSnapshotArray(document);
        var replacementIndex = FindSnapshotIndex(snapshots, record.SessionId);
        var recordNode = JsonSerializer.SerializeToNode(
            record,
            LocalPersistenceJsonContext.Default.LocalActiveRuntimeSessionSnapshotRecord) ??
            throw new InvalidOperationException("Active runtime snapshot serialization produced no data.");

        if (replacementIndex >= 0)
        {
            snapshots[replacementIndex] = recordNode;
        }
        else
        {
            snapshots.AddNode(recordNode);
        }

        document[ActiveRuntimeSessionSnapshotsPropertyName] = snapshots;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalActiveRuntimeSessionSnapshotRecord?> LoadAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session id is required.", nameof(sessionId));
        }

        var records = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return records.FirstOrDefault(record => record.SessionId == sessionId);
    }

    public async ValueTask<LocalActiveRuntimeSessionSnapshotRecord?> LoadLatestAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return records
            .OrderByDescending(record => record.CapturedAt)
            .ThenBy(record => record.SessionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public ValueTask<IReadOnlyList<LocalActiveRuntimeSessionSnapshotRecord>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadRecordsAsync(cancellationToken);
    }

    public async ValueTask DeleteAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session id is required.", nameof(sessionId));
        }

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = ReadSnapshotArray(document);
        var index = FindSnapshotIndex(snapshots, sessionId);
        if (index >= 0)
        {
            snapshots.RemoveAt(index);
            document[ActiveRuntimeSessionSnapshotsPropertyName] = snapshots;
            await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        document[ActiveRuntimeSessionSnapshotsPropertyName] = new JsonArray();
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<LocalActiveRuntimeSessionSnapshotRecord>> ReadRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadSnapshotArray(document)
            .Select(ReadRecord)
            .OrderBy(record => record.CapturedAt)
            .ThenBy(record => record.SessionId, StringComparer.Ordinal)
            .ToArray();
    }

    private async ValueTask<JsonObject> ReadInitializedDocumentAsync(CancellationToken cancellationToken)
    {
        await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var stream = new FileStream(
            options.DatabasePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var document = await LocalJsonDocumentIO.ReadObjectAsync(stream, cancellationToken)
            .ConfigureAwait(false);

        if (document is null ||
            !document.TryGetPropertyValue("Kind", out var kindNode) ||
            kindNode?.GetValue<string>() != LocalDatabaseSchema.MetadataKind)
        {
            throw new InvalidOperationException("The local database metadata is missing or invalid.");
        }

        LocalDatabaseDocument.ReadSchemaVersion(document);
        return document;
    }

    private async ValueTask ReplaceDatabaseAsync(JsonObject document, CancellationToken cancellationToken)
    {
        var tempPath = $"{options.DatabasePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await WriteDocumentAsync(tempPath, document, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, options.DatabasePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async ValueTask WriteDocumentAsync(
        string path,
        JsonObject document,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await LocalJsonDocumentIO.WriteObjectAsync(stream, document, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static JsonArray ReadSnapshotArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(ActiveRuntimeSessionSnapshotsPropertyName, out var snapshotsNode) ||
            snapshotsNode is null)
        {
            return [];
        }

        if (snapshotsNode is JsonArray snapshots)
        {
            return snapshots;
        }

        throw new InvalidOperationException("The stored active runtime session snapshots are invalid.");
    }

    private static LocalActiveRuntimeSessionSnapshotRecord ReadRecord(JsonNode? node)
    {
        if (node is null)
        {
            throw new InvalidOperationException("The stored active runtime session snapshot is invalid.");
        }

        try
        {
            return node.Deserialize(
                    LocalPersistenceJsonContext.Default.LocalActiveRuntimeSessionSnapshotRecord) ??
                throw new InvalidOperationException("The stored active runtime session snapshot is empty.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "The stored active runtime session snapshot could not be read.",
                exception);
        }
    }

    private static int FindSnapshotIndex(JsonArray snapshots, string sessionId)
    {
        for (var index = 0; index < snapshots.Count; index++)
        {
            if (snapshots[index] is JsonObject snapshotObject &&
                snapshotObject.TryGetPropertyValue(nameof(LocalActiveRuntimeSessionSnapshotRecord.SessionId), out var sessionIdNode) &&
                sessionIdNode?.GetValue<string>() == sessionId)
            {
                return index;
            }
        }

        return -1;
    }
}
