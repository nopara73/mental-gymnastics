using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App;

public sealed class ActiveRuntimeSessionSnapshotSaveRequest
{
    public ActiveRuntimeSessionSnapshotSaveRequest(
        RuntimeSessionSnapshot snapshot,
        RuntimeCueSchedulerSnapshot? cueSchedulerSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (cueSchedulerSnapshot is not null)
        {
            if (snapshot.GeneratedDrillInstance is null)
            {
                throw new ArgumentException(
                    "Cue scheduler snapshots require a generated drill instance on the active runtime session.",
                    nameof(cueSchedulerSnapshot));
            }

            if (!string.Equals(
                    snapshot.GeneratedDrillInstance.InstanceId,
                    cueSchedulerSnapshot.GeneratedDrillInstance.InstanceId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Cue scheduler snapshot generated instance must match the active runtime session.",
                    nameof(cueSchedulerSnapshot));
            }
        }

        Snapshot = snapshot;
        CueSchedulerSnapshot = cueSchedulerSnapshot;
    }

    public RuntimeSessionSnapshot Snapshot { get; }

    public RuntimeCueSchedulerSnapshot? CueSchedulerSnapshot { get; }
}

public sealed class ActiveRuntimeSessionSnapshotRestoreRequest
{
    public ActiveRuntimeSessionSnapshotRestoreRequest(
        string sessionId,
        IRuntimeClock clock,
        RuntimeCueSchedule? cueSchedule = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(clock);

        SessionId = sessionId;
        Clock = clock;
        CueSchedule = cueSchedule;
    }

    public string SessionId { get; }

    public IRuntimeClock Clock { get; }

    public RuntimeCueSchedule? CueSchedule { get; }
}

public sealed class ActiveRuntimeSessionSnapshotRestoreLatestRequest
{
    public ActiveRuntimeSessionSnapshotRestoreLatestRequest(
        IRuntimeClock clock,
        RuntimeCueSchedule? cueSchedule = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        Clock = clock;
        CueSchedule = cueSchedule;
    }

    public IRuntimeClock Clock { get; }

    public RuntimeCueSchedule? CueSchedule { get; }
}

public enum ActiveRuntimeSessionSnapshotRestoreStatus
{
    Restored,
    NotFound,
    Unsafe,
}

public sealed record ActiveRuntimeSessionSnapshotRestoreResult(
    ActiveRuntimeSessionSnapshotRestoreStatus Status,
    RuntimeInputCommandHandler? CommandHandler,
    RuntimeCueScheduler? CueScheduler,
    LocalActiveRuntimeSessionSnapshotRecord? SnapshotRecord,
    string Detail)
{
    public bool GrantsAdvancementInApp => false;
}

public sealed class ActiveRuntimeSessionSnapshotPersistenceService
{
    private readonly LocalActiveRuntimeSessionSnapshotStore snapshotStore;

    public ActiveRuntimeSessionSnapshotPersistenceService(AppStartupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        snapshotStore = new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions);
    }

    public async ValueTask SaveAsync(
        ActiveRuntimeSessionSnapshotSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await snapshotStore.SaveAsync(
            ToLocalRecord(request.Snapshot, request.CueSchedulerSnapshot),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ActiveRuntimeSessionSnapshotRestoreResult> RestoreAsync(
        ActiveRuntimeSessionSnapshotRestoreRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        LocalActiveRuntimeSessionSnapshotRecord? record = null;
        try
        {
            record = await snapshotStore.LoadAsync(request.SessionId, cancellationToken)
                .ConfigureAwait(false);
            return RestoreRecord(record, request.Clock, request.CueSchedule);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Unsafe(record, exception.Message);
        }
    }

    public async ValueTask<ActiveRuntimeSessionSnapshotRestoreResult> RestoreLatestAsync(
        ActiveRuntimeSessionSnapshotRestoreLatestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        LocalActiveRuntimeSessionSnapshotRecord? record = null;
        try
        {
            record = await snapshotStore.LoadLatestAsync(cancellationToken)
                .ConfigureAwait(false);
            return RestoreRecord(record, request.Clock, request.CueSchedule);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Unsafe(record, exception.Message);
        }
    }

    public ValueTask DeleteAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return snapshotStore.DeleteAsync(sessionId, cancellationToken);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        return snapshotStore.ClearAsync(cancellationToken);
    }

    private static ActiveRuntimeSessionSnapshotRestoreResult RestoreRecord(
        LocalActiveRuntimeSessionSnapshotRecord? record,
        IRuntimeClock clock,
        RuntimeCueSchedule? cueSchedule)
    {
        try
        {
            if (record is null)
            {
                return new ActiveRuntimeSessionSnapshotRestoreResult(
                    ActiveRuntimeSessionSnapshotRestoreStatus.NotFound,
                    CommandHandler: null,
                    CueScheduler: null,
                    SnapshotRecord: null,
                    "No active runtime session snapshot was found.");
            }

            var snapshot = ToRuntimeSnapshot(record);
            var commandHandler = RuntimeInputCommandHandler.Restore(snapshot, clock);
            var cueScheduler = RestoreCueSchedulerIfPresent(record, clock, cueSchedule, commandHandler);

            return new ActiveRuntimeSessionSnapshotRestoreResult(
                ActiveRuntimeSessionSnapshotRestoreStatus.Restored,
                commandHandler,
                cueScheduler,
                record,
                "Active runtime session snapshot restored.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Unsafe(record, exception.Message);
        }
    }

    private static ActiveRuntimeSessionSnapshotRestoreResult Unsafe(
        LocalActiveRuntimeSessionSnapshotRecord? record,
        string detail)
    {
        return new ActiveRuntimeSessionSnapshotRestoreResult(
            ActiveRuntimeSessionSnapshotRestoreStatus.Unsafe,
            CommandHandler: null,
            CueScheduler: null,
            record,
            detail);
    }

    private static RuntimeCueScheduler? RestoreCueSchedulerIfPresent(
        LocalActiveRuntimeSessionSnapshotRecord record,
        IRuntimeClock clock,
        RuntimeCueSchedule? cueSchedule,
        RuntimeInputCommandHandler commandHandler)
    {
        if (record.CueScheduler is null)
        {
            return null;
        }

        if (cueSchedule is null)
        {
            throw new InvalidOperationException(
                "A cue schedule is required to restore an active cue scheduler snapshot.");
        }

        return RuntimeCueScheduler.Restore(
            cueSchedule,
            clock,
            commandHandler.EventLog,
            ToRuntimeCueSchedulerSnapshot(record.CueScheduler));
    }

    private static LocalActiveRuntimeSessionSnapshotRecord ToLocalRecord(
        RuntimeSessionSnapshot snapshot,
        RuntimeCueSchedulerSnapshot? cueSchedulerSnapshot)
    {
        return new LocalActiveRuntimeSessionSnapshotRecord(
            snapshot.SessionId,
            snapshot.CapturedAt.Offset,
            ToLocalRecord(snapshot.SessionDefinition),
            new LocalRuntimeLifecycleStateRecord(
                snapshot.LifecycleState.Status.ToString(),
                snapshot.LifecycleState.PauseAllowed),
            new LocalRuntimeInputOptionsRecord(
                snapshot.InputOptions.PauseAllowed,
                snapshot.InputOptions.CorrectionWindow.Value,
                snapshot.InputOptions.PauseAllowedPhaseKinds.Select(kind => kind.ToString()).ToArray()),
            new LocalRuntimePhasePlanRecord(snapshot.PhasePlan.Phases.Select(ToLocalRecord).ToArray()),
            new LocalRuntimePhaseSchedulerSnapshotRecord(
                snapshot.PhaseScheduler.Status.ToString(),
                snapshot.PhaseScheduler.CapturedAt.Offset,
                snapshot.PhaseScheduler.CurrentPhaseIndex,
                snapshot.PhaseScheduler.CurrentPhaseId,
                snapshot.PhaseScheduler.CurrentPhaseElapsed?.Value,
                snapshot.PhaseScheduler.CompletedPhases.Select(ToLocalRecord).ToArray()),
            snapshot.RuntimeEvents.Select(ToLocalRecord).ToArray(),
            snapshot.EvidenceFacts.Select(ToLocalRecord).ToArray(),
            snapshot.LastCorrectableEventSequenceNumber,
            cueSchedulerSnapshot is null ? null : ToLocalRecord(cueSchedulerSnapshot));
    }

    private static LocalRuntimeSessionDefinitionRecord ToLocalRecord(RuntimeSessionDefinition definition)
    {
        return new LocalRuntimeSessionDefinitionRecord(
            definition.SessionType,
            definition.Branch,
            definition.Level,
            definition.Drill,
            definition.LoadVariables.ToArray(),
            definition.Standard,
            definition.CriticalConstraints.ToArray(),
            definition.GeneratedDrillInstance is null ? null : ToLocalRecord(definition.GeneratedDrillInstance),
            definition.SourceDrill);
    }

    private static LocalRuntimeGeneratedDrillInstanceIdentityRecord ToLocalRecord(
        RuntimeGeneratedDrillInstanceIdentity identity)
    {
        return new LocalRuntimeGeneratedDrillInstanceIdentityRecord(
            identity.InstanceId,
            new LocalGeneratedDrillContentIdentity(
                identity.ContentIdentity,
                identity.ContentVersion));
    }

    private static LocalRuntimePhaseDefinitionRecord ToLocalRecord(RuntimeSessionPhaseDefinition phase)
    {
        return new LocalRuntimePhaseDefinitionRecord(
            phase.Id,
            phase.Kind.ToString(),
            phase.CompletionRule.ToString(),
            phase.ScheduledDuration?.Value);
    }

    private static LocalRuntimeCompletedPhaseRecord ToLocalRecord(RuntimeCompletedSessionPhase phase)
    {
        return new LocalRuntimeCompletedPhaseRecord(
            ToLocalRecord(phase.Definition),
            phase.StartedAt.Offset,
            phase.CompletedAt.Offset,
            phase.ActualDuration.Value,
            phase.CompletionCause.ToString());
    }

    private static LocalRuntimeEventRecord ToLocalRecord(RuntimeEvent runtimeEvent)
    {
        return new LocalRuntimeEventRecord(
            runtimeEvent.SessionId,
            runtimeEvent.SequenceNumber,
            runtimeEvent.Kind.ToString(),
            runtimeEvent.OccurredAt.Offset,
            runtimeEvent.PhaseId,
            runtimeEvent.PhaseKind?.ToString(),
            runtimeEvent.Facts.Select(ToLocalRecord).ToArray());
    }

    private static LocalRuntimeEventFactRecord ToLocalRecord(RuntimeEventFact fact)
    {
        return new LocalRuntimeEventFactRecord(fact.Name, fact.Value);
    }

    private static LocalRuntimeCueSchedulerSnapshotRecord ToLocalRecord(RuntimeCueSchedulerSnapshot snapshot)
    {
        return new LocalRuntimeCueSchedulerSnapshotRecord(
            ToLocalRecord(snapshot.GeneratedDrillInstance),
            snapshot.CapturedAt.Offset,
            snapshot.IsPaused,
            snapshot.ElapsedPauseDuration.Value,
            snapshot.CueStates.Select(ToLocalRecord).ToArray(),
            snapshot.RuntimeEvents.Select(ToLocalRecord).ToArray(),
            snapshot.EvidenceFacts.Select(ToLocalRecord).ToArray());
    }

    private static LocalRuntimeCueStateSnapshotRecord ToLocalRecord(RuntimeCueStateSnapshot snapshot)
    {
        return new LocalRuntimeCueStateSnapshotRecord(
            snapshot.CueId,
            snapshot.PresentedAt?.Offset,
            snapshot.PhaseId,
            snapshot.PhaseKind?.ToString(),
            snapshot.ResponseEventSequenceNumber);
    }

    private static RuntimeSessionSnapshot ToRuntimeSnapshot(LocalActiveRuntimeSessionSnapshotRecord record)
    {
        return new RuntimeSessionSnapshot(
            record.SessionId,
            ToRuntimeSessionDefinition(record.SessionDefinition),
            new RuntimeSessionLifecycleState(
                Parse<RuntimeSessionLifecycleStatus>(record.LifecycleState.Status),
                record.LifecycleState.PauseAllowed),
            new RuntimeInputCommandOptions(
                record.InputOptions.PauseAllowed,
                new RuntimeDuration(record.InputOptions.CorrectionWindow),
                record.InputOptions.PauseAllowedPhaseKinds.Select(Parse<RuntimeSessionPhaseKind>)),
            new RuntimeSessionPhasePlan(record.PhasePlan.Phases.Select(ToRuntimePhaseDefinition)),
            new RuntimePhaseSchedulerSnapshot(
                Parse<RuntimePhaseSchedulerStatus>(record.PhaseScheduler.Status),
                new RuntimeInstant(record.PhaseScheduler.CapturedAt),
                record.PhaseScheduler.CurrentPhaseIndex,
                record.PhaseScheduler.CurrentPhaseId,
                record.PhaseScheduler.CurrentPhaseElapsed.HasValue
                    ? new RuntimeDuration(record.PhaseScheduler.CurrentPhaseElapsed.Value)
                    : null,
                record.PhaseScheduler.CompletedPhases.Select(ToRuntimeCompletedPhase)),
            record.RuntimeEvents.Select(ToRuntimeEvent),
            record.LastCorrectableEventSequenceNumber,
            new RuntimeInstant(record.CapturedAt));
    }

    private static RuntimeCueSchedulerSnapshot ToRuntimeCueSchedulerSnapshot(
        LocalRuntimeCueSchedulerSnapshotRecord record)
    {
        return new RuntimeCueSchedulerSnapshot(
            ToRuntimeGeneratedDrillInstanceIdentity(record.GeneratedDrillInstance),
            new RuntimeInstant(record.CapturedAt),
            record.IsPaused,
            new RuntimeDuration(record.ElapsedPauseDuration),
            record.CueStates.Select(ToRuntimeCueStateSnapshot),
            record.RuntimeEvents.Select(ToRuntimeEvent));
    }

    private static RuntimeSessionDefinition ToRuntimeSessionDefinition(
        LocalRuntimeSessionDefinitionRecord definition)
    {
        return new RuntimeSessionDefinition(
            definition.SessionType,
            definition.Branch,
            definition.Level,
            definition.Drill,
            definition.LoadVariables,
            definition.Standard,
            definition.CriticalConstraints,
            definition.GeneratedDrillInstance is null
                ? null
                : ToRuntimeGeneratedDrillInstanceIdentity(definition.GeneratedDrillInstance),
            definition.SourceDrill);
    }

    private static RuntimeGeneratedDrillInstanceIdentity ToRuntimeGeneratedDrillInstanceIdentity(
        LocalRuntimeGeneratedDrillInstanceIdentityRecord identity)
    {
        return new RuntimeGeneratedDrillInstanceIdentity(
            identity.InstanceId,
            identity.ContentIdentity.ToPromptContentIdentity(),
            identity.ContentIdentity.Version);
    }

    private static RuntimeSessionPhaseDefinition ToRuntimePhaseDefinition(
        LocalRuntimePhaseDefinitionRecord phase)
    {
        var kind = Parse<RuntimeSessionPhaseKind>(phase.Kind);
        var completionRule = Parse<RuntimeSessionPhaseCompletionRule>(phase.CompletionRule);
        return completionRule switch
        {
            RuntimeSessionPhaseCompletionRule.Manual =>
                RuntimeSessionPhaseDefinition.Manual(phase.Id, kind),
            RuntimeSessionPhaseCompletionRule.Timed =>
                RuntimeSessionPhaseDefinition.Timed(
                    phase.Id,
                    kind,
                    new RuntimeDuration(phase.ScheduledDuration.GetValueOrDefault())),
            RuntimeSessionPhaseCompletionRule.ManualOrTimed =>
                RuntimeSessionPhaseDefinition.ManualOrTimed(
                    phase.Id,
                    kind,
                    phase.ScheduledDuration.HasValue
                        ? new RuntimeDuration(phase.ScheduledDuration.Value)
                        : null),
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase.CompletionRule, "Unknown runtime phase completion rule."),
        };
    }

    private static RuntimeCompletedSessionPhase ToRuntimeCompletedPhase(
        LocalRuntimeCompletedPhaseRecord phase)
    {
        return new RuntimeCompletedSessionPhase(
            ToRuntimePhaseDefinition(phase.Definition),
            new RuntimeInstant(phase.StartedAt),
            new RuntimeInstant(phase.CompletedAt),
            new RuntimeDuration(phase.ActualDuration),
            Parse<RuntimeSessionPhaseCompletionCause>(phase.CompletionCause));
    }

    private static RuntimeEvent ToRuntimeEvent(LocalRuntimeEventRecord runtimeEvent)
    {
        return new RuntimeEvent(
            runtimeEvent.SessionId,
            runtimeEvent.SequenceNumber,
            Parse<RuntimeEventKind>(runtimeEvent.Kind),
            new RuntimeInstant(runtimeEvent.OccurredAt),
            runtimeEvent.PhaseId,
            runtimeEvent.PhaseKind is null ? null : Parse<RuntimeSessionPhaseKind>(runtimeEvent.PhaseKind),
            runtimeEvent.Facts.Select(ToRuntimeFact));
    }

    private static RuntimeEventFact ToRuntimeFact(LocalRuntimeEventFactRecord fact)
    {
        return new RuntimeEventFact(fact.Name, fact.Value);
    }

    private static RuntimeCueStateSnapshot ToRuntimeCueStateSnapshot(
        LocalRuntimeCueStateSnapshotRecord snapshot)
    {
        return new RuntimeCueStateSnapshot(
            snapshot.CueId,
            snapshot.PresentedAt.HasValue ? new RuntimeInstant(snapshot.PresentedAt.Value) : null,
            snapshot.PhaseId,
            snapshot.PhaseKind is null ? null : Parse<RuntimeSessionPhaseKind>(snapshot.PhaseKind),
            snapshot.ResponseEventSequenceNumber);
    }

    private static TEnum Parse<TEnum>(string value)
        where TEnum : struct, Enum
    {
        return Enum.Parse<TEnum>(value, ignoreCase: false);
    }
}
