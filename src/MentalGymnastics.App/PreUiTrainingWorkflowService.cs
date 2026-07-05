using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App;

public enum PreUiTrainingWorkflowPreparationStatus
{
    Prepared,
    Blocked,
    ContentRejected,
    RuntimeRejected,
    PersistenceRejected,
}

public sealed record PreUiTrainingWorkflowRejection(string Detail);

public sealed class PreUiTrainingWorkflowPreparationRequest
{
    public PreUiTrainingWorkflowPreparationRequest(
        NextTrainingWorkSelectionQuery selectionQuery,
        PromptContentKind contentKind,
        string equivalenceClass,
        PromptFreshnessPolicy freshnessPolicy,
        GeneratedContentSeed seed,
        string runtimeSessionId,
        IEnumerable<string>? previouslyUsedContentIds = null,
        IEnumerable<CriticalConstraint>? additionalCriticalConstraints = null,
        LoadChangeMode loadChangeMode = LoadChangeMode.Acquisition,
        bool increasedVariablesStableSeparately = false,
        CapacityId? transferCapacity = null,
        string? transferDistance = null,
        RuntimeInputCommandOptions? inputOptions = null)
    {
        ArgumentNullException.ThrowIfNull(selectionQuery);
        EnsureDefined(contentKind, nameof(contentKind));
        EnsureDefined(freshnessPolicy, nameof(freshnessPolicy));
        ArgumentNullException.ThrowIfNull(seed);
        EnsureDefined(loadChangeMode, nameof(loadChangeMode));

        if (string.IsNullOrWhiteSpace(equivalenceClass))
        {
            throw new ArgumentException(
                "Pre-UI training workflow content preparation requires an equivalence class.",
                nameof(equivalenceClass));
        }

        if (string.IsNullOrWhiteSpace(runtimeSessionId))
        {
            throw new ArgumentException(
                "Pre-UI training workflow runtime preparation requires a session id.",
                nameof(runtimeSessionId));
        }

        if (transferCapacity.HasValue)
        {
            EnsureDefined(transferCapacity.Value, nameof(transferCapacity));
        }

        SelectionQuery = selectionQuery;
        ContentKind = contentKind;
        EquivalenceClass = equivalenceClass;
        FreshnessPolicy = freshnessPolicy;
        Seed = seed;
        RuntimeSessionId = runtimeSessionId;
        PreviouslyUsedContentIds = (previouslyUsedContentIds ?? [])
            .Where(contentId => !string.IsNullOrWhiteSpace(contentId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        AdditionalCriticalConstraints = (additionalCriticalConstraints ?? [])
            .Where(constraint => !string.IsNullOrWhiteSpace(constraint.Description))
            .ToArray();
        LoadChangeMode = loadChangeMode;
        IncreasedVariablesStableSeparately = increasedVariablesStableSeparately;
        TransferCapacity = transferCapacity;
        TransferDistance = transferDistance;
        InputOptions = inputOptions ?? RuntimeInputCommandOptions.Default;
    }

    public NextTrainingWorkSelectionQuery SelectionQuery { get; }

    public PromptContentKind ContentKind { get; }

    public string EquivalenceClass { get; }

    public PromptFreshnessPolicy FreshnessPolicy { get; }

    public GeneratedContentSeed Seed { get; }

    public string RuntimeSessionId { get; }

    public IReadOnlyList<string> PreviouslyUsedContentIds { get; }

    public IReadOnlyList<CriticalConstraint> AdditionalCriticalConstraints { get; }

    public LoadChangeMode LoadChangeMode { get; }

    public bool IncreasedVariablesStableSeparately { get; }

    public CapacityId? TransferCapacity { get; }

    public string? TransferDistance { get; }

    public RuntimeInputCommandOptions InputOptions { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown program identifier.");
        }
    }
}

public sealed class PreUiTrainingWorkflowPreparationResult
{
    private PreUiTrainingWorkflowPreparationResult(
        PreUiTrainingWorkflowPreparationStatus status,
        NextTrainingWorkSelection selection,
        SelectedWorkGeneratedContentPreparationResult? generatedContent,
        SelectedWorkRuntimeSessionPreparationResult? runtimeSession,
        LocalGeneratedDrillInstanceRecord? generatedInstanceRecord,
        IEnumerable<PreUiTrainingWorkflowRejection> rejections)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(rejections);

        var rejectionArray = rejections.ToArray();
        if (rejectionArray.Any(rejection => string.IsNullOrWhiteSpace(rejection.Detail)))
        {
            throw new ArgumentException(
                "Pre-UI training workflow preparation rejections must include details.",
                nameof(rejections));
        }

        Status = status;
        Selection = selection;
        GeneratedContent = generatedContent;
        RuntimeSession = runtimeSession;
        GeneratedInstanceRecord = generatedInstanceRecord;
        Rejections = Array.AsReadOnly(rejectionArray);
    }

    public PreUiTrainingWorkflowPreparationStatus Status { get; }

    public bool IsPrepared => Status == PreUiTrainingWorkflowPreparationStatus.Prepared;

    public NextTrainingWorkSelection Selection { get; }

    public CurrentTrainingStateReadModel CurrentState => Selection.CurrentState;

    public SelectedWorkGeneratedContentPreparationResult? GeneratedContent { get; }

    public SelectedWorkRuntimeSessionPreparationResult? RuntimeSession { get; }

    public LocalGeneratedDrillInstanceRecord? GeneratedInstanceRecord { get; }

    public IReadOnlyList<PreUiTrainingWorkflowRejection> Rejections { get; }

    public bool CanStartRuntimeSession => IsPrepared && RuntimeSession?.CanRenderLiveSession == true;

    public bool GrantsAdvancementInApp => false;

    internal static PreUiTrainingWorkflowPreparationResult Prepared(
        NextTrainingWorkSelection selection,
        SelectedWorkGeneratedContentPreparationResult generatedContent,
        SelectedWorkRuntimeSessionPreparationResult runtimeSession,
        LocalGeneratedDrillInstanceRecord generatedInstanceRecord)
    {
        return new PreUiTrainingWorkflowPreparationResult(
            PreUiTrainingWorkflowPreparationStatus.Prepared,
            selection,
            generatedContent,
            runtimeSession,
            generatedInstanceRecord,
            []);
    }

    internal static PreUiTrainingWorkflowPreparationResult Blocked(
        NextTrainingWorkSelection selection)
    {
        return new PreUiTrainingWorkflowPreparationResult(
            PreUiTrainingWorkflowPreparationStatus.Blocked,
            selection,
            generatedContent: null,
            runtimeSession: null,
            generatedInstanceRecord: null,
            selection.Blockers.Select(blocker => new PreUiTrainingWorkflowRejection(blocker.Detail)));
    }

    internal static PreUiTrainingWorkflowPreparationResult ContentRejected(
        NextTrainingWorkSelection selection,
        SelectedWorkGeneratedContentPreparationResult generatedContent)
    {
        return new PreUiTrainingWorkflowPreparationResult(
            PreUiTrainingWorkflowPreparationStatus.ContentRejected,
            selection,
            generatedContent,
            runtimeSession: null,
            generatedInstanceRecord: null,
            generatedContent.Rejections.Select(rejection => new PreUiTrainingWorkflowRejection(rejection.Detail)));
    }

    internal static PreUiTrainingWorkflowPreparationResult RuntimeRejected(
        NextTrainingWorkSelection selection,
        SelectedWorkGeneratedContentPreparationResult generatedContent,
        SelectedWorkRuntimeSessionPreparationResult runtimeSession)
    {
        return new PreUiTrainingWorkflowPreparationResult(
            PreUiTrainingWorkflowPreparationStatus.RuntimeRejected,
            selection,
            generatedContent,
            runtimeSession,
            generatedInstanceRecord: null,
            runtimeSession.Rejections.Select(rejection => new PreUiTrainingWorkflowRejection(rejection.Detail)));
    }

    internal static PreUiTrainingWorkflowPreparationResult PersistenceRejected(
        NextTrainingWorkSelection selection,
        SelectedWorkGeneratedContentPreparationResult generatedContent,
        SelectedWorkRuntimeSessionPreparationResult runtimeSession,
        Exception exception)
    {
        return new PreUiTrainingWorkflowPreparationResult(
            PreUiTrainingWorkflowPreparationStatus.PersistenceRejected,
            selection,
            generatedContent,
            runtimeSession,
            generatedInstanceRecord: null,
            [new PreUiTrainingWorkflowRejection(exception.Message)]);
    }
}

public sealed class PreUiTrainingWorkflowCompletionRequest
{
    public PreUiTrainingWorkflowCompletionRequest(
        CompletedRuntimeSessionProcessingRequest processingRequest,
        TrainingDate refreshStateAsOf,
        int recentSessionLimit = 10,
        int evidenceSummaryLimit = 10,
        int progressRecordLimit = 10)
    {
        ArgumentNullException.ThrowIfNull(processingRequest);

        ProcessingRequest = processingRequest;
        RefreshStateAsOf = refreshStateAsOf;
        RecentSessionLimit = recentSessionLimit;
        EvidenceSummaryLimit = evidenceSummaryLimit;
        ProgressRecordLimit = progressRecordLimit;
    }

    public CompletedRuntimeSessionProcessingRequest ProcessingRequest { get; }

    public TrainingDate RefreshStateAsOf { get; }

    public int RecentSessionLimit { get; }

    public int EvidenceSummaryLimit { get; }

    public int ProgressRecordLimit { get; }
}

public sealed record PreUiTrainingWorkflowCompletionResult(
    CompletedRuntimeSessionProcessingResult ProcessingResult,
    CurrentTrainingStateReadModel RefreshedState)
{
    public bool GrantsAdvancementInApp => false;
}

public enum PreUiTrainingWorkflowStartStatus
{
    Started,
    RuntimeRejected,
    SnapshotRejected,
}

public sealed class PreUiTrainingWorkflowStartRequest
{
    public PreUiTrainingWorkflowStartRequest(
        SelectedWorkRuntimeSessionPreparationResult runtimeSession,
        IRuntimeClock clock,
        bool saveActiveSnapshot = true)
    {
        ArgumentNullException.ThrowIfNull(runtimeSession);
        ArgumentNullException.ThrowIfNull(clock);

        RuntimeSession = runtimeSession;
        Clock = clock;
        SaveActiveSnapshot = saveActiveSnapshot;
    }

    public SelectedWorkRuntimeSessionPreparationResult RuntimeSession { get; }

    public IRuntimeClock Clock { get; }

    public bool SaveActiveSnapshot { get; }
}

public sealed class PreUiTrainingWorkflowStartResult
{
    private PreUiTrainingWorkflowStartResult(
        PreUiTrainingWorkflowStartStatus status,
        string sessionId,
        RuntimeInputCommandHandler? commandHandler,
        RuntimeCueScheduler? cueScheduler,
        PreUiActiveSessionResumeState activeSession,
        IEnumerable<PreUiTrainingWorkflowRejection> rejections)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(activeSession);
        ArgumentNullException.ThrowIfNull(rejections);

        Status = status;
        SessionId = sessionId;
        CommandHandler = commandHandler;
        CueScheduler = cueScheduler;
        ActiveSession = activeSession;
        Rejections = Array.AsReadOnly(rejections.ToArray());
    }

    public PreUiTrainingWorkflowStartStatus Status { get; }

    public bool IsStarted => Status == PreUiTrainingWorkflowStartStatus.Started;

    public string SessionId { get; }

    public RuntimeInputCommandHandler? CommandHandler { get; }

    public RuntimeCueScheduler? CueScheduler { get; }

    public PreUiActiveSessionResumeState ActiveSession { get; }

    public IReadOnlyList<PreUiTrainingWorkflowRejection> Rejections { get; }

    public bool GrantsAdvancementInApp => false;

    internal static PreUiTrainingWorkflowStartResult Started(
        string sessionId,
        RuntimeInputCommandHandler commandHandler,
        RuntimeCueScheduler? cueScheduler,
        PreUiActiveSessionResumeState activeSession)
    {
        return new PreUiTrainingWorkflowStartResult(
            PreUiTrainingWorkflowStartStatus.Started,
            sessionId,
            commandHandler,
            cueScheduler,
            activeSession,
            []);
    }

    internal static PreUiTrainingWorkflowStartResult Rejected(
        PreUiTrainingWorkflowStartStatus status,
        string sessionId,
        string detail)
    {
        return new PreUiTrainingWorkflowStartResult(
            status,
            sessionId,
            commandHandler: null,
            cueScheduler: null,
            PreUiActiveSessionResumeState.NotFound(sessionId, detail),
            [new PreUiTrainingWorkflowRejection(detail)]);
    }
}

public enum PreUiActiveSessionResumeStatus
{
    Resumable,
    NotPersisted,
    NotFound,
    Unsafe,
}

public sealed class PreUiActiveSessionResumeRequest
{
    public PreUiActiveSessionResumeRequest(
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

public sealed record PreUiActiveSessionResumeResult(
    PreUiActiveSessionResumeState State,
    RuntimeInputCommandHandler? CommandHandler,
    RuntimeCueScheduler? CueScheduler)
{
    public bool CanResume => State.CanResume && CommandHandler is not null;

    public bool GrantsAdvancementInApp => false;
}

public sealed class PreUiActiveSessionInvalidationRequest
{
    public PreUiActiveSessionInvalidationRequest(
        string sessionId,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session id is required.", nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Active session invalidation requires a reason.", nameof(reason));
        }

        SessionId = sessionId;
        Reason = reason;
    }

    public string SessionId { get; }

    public string Reason { get; }
}

public sealed record PreUiActiveSessionInvalidationResult(
    string SessionId,
    bool Cleared,
    string Reason)
{
    public bool GrantsAdvancementInApp => false;
}

public sealed class PreUiActiveSessionResumeState
{
    private PreUiActiveSessionResumeState(
        PreUiActiveSessionResumeStatus status,
        string sessionId,
        TimeSpan? capturedAt,
        SessionType? sessionType,
        BranchCode? branch,
        GlobalLevelId? level,
        DrillId? drill,
        string? generatedDrillInstanceId,
        RuntimeSessionLifecycleStatus? lifecycleStatus,
        string? activePhaseId,
        RuntimeSessionPhaseKind? activePhaseKind,
        IEnumerable<string> pendingCueIds,
        int runtimeEventCount,
        int evidenceFactCount,
        string detail)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(pendingCueIds);
        if (string.IsNullOrWhiteSpace(detail))
        {
            throw new ArgumentException("Active session resume state requires detail.", nameof(detail));
        }

        Status = status;
        SessionId = sessionId;
        CapturedAt = capturedAt;
        SessionType = sessionType;
        Branch = branch;
        Level = level;
        Drill = drill;
        GeneratedDrillInstanceId = generatedDrillInstanceId;
        LifecycleStatus = lifecycleStatus;
        ActivePhaseId = activePhaseId;
        ActivePhaseKind = activePhaseKind;
        PendingCueIds = Array.AsReadOnly(pendingCueIds.ToArray());
        RuntimeEventCount = runtimeEventCount;
        EvidenceFactCount = evidenceFactCount;
        Detail = detail;
    }

    public PreUiActiveSessionResumeStatus Status { get; }

    public bool CanResume => Status == PreUiActiveSessionResumeStatus.Resumable;

    public string SessionId { get; }

    public TimeSpan? CapturedAt { get; }

    public SessionType? SessionType { get; }

    public BranchCode? Branch { get; }

    public GlobalLevelId? Level { get; }

    public DrillId? Drill { get; }

    public string? GeneratedDrillInstanceId { get; }

    public RuntimeSessionLifecycleStatus? LifecycleStatus { get; }

    public string? ActivePhaseId { get; }

    public RuntimeSessionPhaseKind? ActivePhaseKind { get; }

    public IReadOnlyList<string> PendingCueIds { get; }

    public int RuntimeEventCount { get; }

    public int EvidenceFactCount { get; }

    public string Detail { get; }

    public bool GrantsAdvancementInApp => false;

    internal static PreUiActiveSessionResumeState Resumable(
        LocalActiveRuntimeSessionSnapshotRecord record,
        string detail)
    {
        return FromRecord(PreUiActiveSessionResumeStatus.Resumable, record, detail);
    }

    internal static PreUiActiveSessionResumeState Unsafe(
        LocalActiveRuntimeSessionSnapshotRecord? record,
        string sessionId,
        string detail)
    {
        return record is null
            ? new PreUiActiveSessionResumeState(
                PreUiActiveSessionResumeStatus.Unsafe,
                sessionId,
                capturedAt: null,
                sessionType: null,
                branch: null,
                level: null,
                drill: null,
                generatedDrillInstanceId: null,
                lifecycleStatus: null,
                activePhaseId: null,
                activePhaseKind: null,
                pendingCueIds: [],
                runtimeEventCount: 0,
                evidenceFactCount: 0,
                detail)
            : FromRecord(PreUiActiveSessionResumeStatus.Unsafe, record, detail);
    }

    internal static PreUiActiveSessionResumeState NotFound(
        string sessionId,
        string detail)
    {
        return new PreUiActiveSessionResumeState(
            PreUiActiveSessionResumeStatus.NotFound,
            sessionId,
            capturedAt: null,
            sessionType: null,
            branch: null,
            level: null,
            drill: null,
            generatedDrillInstanceId: null,
            lifecycleStatus: null,
            activePhaseId: null,
            activePhaseKind: null,
            pendingCueIds: [],
            runtimeEventCount: 0,
            evidenceFactCount: 0,
            detail);
    }

    internal static PreUiActiveSessionResumeState FromRuntimeSnapshot(
        RuntimeSessionSnapshot snapshot,
        RuntimeCueSchedulerSnapshot? cueSchedulerSnapshot,
        string detail)
    {
        return FromRuntimeSnapshot(
            PreUiActiveSessionResumeStatus.Resumable,
            snapshot,
            cueSchedulerSnapshot,
            detail);
    }

    internal static PreUiActiveSessionResumeState NotPersistedFromRuntimeSnapshot(
        RuntimeSessionSnapshot snapshot,
        RuntimeCueSchedulerSnapshot? cueSchedulerSnapshot,
        string detail)
    {
        return FromRuntimeSnapshot(
            PreUiActiveSessionResumeStatus.NotPersisted,
            snapshot,
            cueSchedulerSnapshot,
            detail);
    }

    private static PreUiActiveSessionResumeState FromRuntimeSnapshot(
        PreUiActiveSessionResumeStatus status,
        RuntimeSessionSnapshot snapshot,
        RuntimeCueSchedulerSnapshot? cueSchedulerSnapshot,
        string detail)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new PreUiActiveSessionResumeState(
            status,
            snapshot.SessionId,
            snapshot.CapturedAt.Offset,
            snapshot.SessionDefinition.SessionType,
            snapshot.SessionDefinition.Branch,
            snapshot.SessionDefinition.Level,
            snapshot.SessionDefinition.Drill,
            snapshot.SessionDefinition.GeneratedDrillInstance?.InstanceId,
            snapshot.LifecycleState.Status,
            snapshot.PhaseScheduler.CurrentPhaseId,
            ResolveActivePhaseKind(snapshot.PhasePlan, snapshot.PhaseScheduler.CurrentPhaseId),
            cueSchedulerSnapshot?.CueStates
                .Where(cueState => !cueState.PresentedAt.HasValue)
                .Select(cueState => cueState.CueId) ?? [],
            snapshot.RuntimeEvents.Count,
            snapshot.EvidenceFacts.Count + (cueSchedulerSnapshot?.EvidenceFacts.Count ?? 0),
            detail);
    }

    private static PreUiActiveSessionResumeState FromRecord(
        PreUiActiveSessionResumeStatus status,
        LocalActiveRuntimeSessionSnapshotRecord record,
        string detail)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new PreUiActiveSessionResumeState(
            status,
            record.SessionId,
            record.CapturedAt,
            record.SessionDefinition.SessionType,
            record.SessionDefinition.Branch,
            record.SessionDefinition.Level,
            record.SessionDefinition.Drill,
            record.SessionDefinition.GeneratedDrillInstance?.InstanceId,
            ParseOrNull<RuntimeSessionLifecycleStatus>(record.LifecycleState.Status),
            record.PhaseScheduler.CurrentPhaseId,
            ResolveActivePhaseKind(record),
            record.CueScheduler?.PendingCueIds ?? [],
            record.RuntimeEvents.Count,
            record.EvidenceFacts.Count + (record.CueScheduler?.EvidenceFacts.Count ?? 0),
            detail);
    }

    private static RuntimeSessionPhaseKind? ResolveActivePhaseKind(
        RuntimeSessionPhasePlan phasePlan,
        string? activePhaseId)
    {
        if (activePhaseId is null)
        {
            return null;
        }

        return phasePlan.Phases.FirstOrDefault(phase =>
            string.Equals(phase.Id, activePhaseId, StringComparison.Ordinal))?.Kind;
    }

    private static RuntimeSessionPhaseKind? ResolveActivePhaseKind(
        LocalActiveRuntimeSessionSnapshotRecord record)
    {
        var activePhaseId = record.PhaseScheduler.CurrentPhaseId;
        if (activePhaseId is null)
        {
            return null;
        }

        var phaseKind = record.PhasePlan.Phases
            .FirstOrDefault(phase => string.Equals(phase.Id, activePhaseId, StringComparison.Ordinal))
            ?.Kind;
        return phaseKind is null ? null : ParseOrNull<RuntimeSessionPhaseKind>(phaseKind);
    }

    private static TEnum? ParseOrNull<TEnum>(string value)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed)
            ? parsed
            : null;
    }
}

public sealed class PreUiTrainingWorkflowService
{
    private readonly CurrentTrainingStateLoader stateLoader;
    private readonly NextTrainingWorkSelector workSelector;
    private readonly SelectedWorkGeneratedContentPreparer contentPreparer;
    private readonly SelectedWorkRuntimeSessionPreparer runtimeSessionPreparer;
    private readonly LocalGeneratedDrillInstanceStore generatedDrillInstanceStore;
    private readonly CompletedRuntimeSessionProcessor completedSessionProcessor;
    private readonly ActiveRuntimeSessionSnapshotPersistenceService activeSnapshotService;

    public PreUiTrainingWorkflowService(AppStartupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        stateLoader = new CurrentTrainingStateLoader(configuration);
        workSelector = new NextTrainingWorkSelector(configuration);
        contentPreparer = new SelectedWorkGeneratedContentPreparer();
        runtimeSessionPreparer = new SelectedWorkRuntimeSessionPreparer();
        generatedDrillInstanceStore = new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions);
        completedSessionProcessor = new CompletedRuntimeSessionProcessor(configuration);
        activeSnapshotService = new ActiveRuntimeSessionSnapshotPersistenceService(configuration);
    }

    public async ValueTask<PreUiTrainingWorkflowPreparationResult> PrepareNextSessionAsync(
        PreUiTrainingWorkflowPreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var selection = await workSelector.SelectAsync(
            request.SelectionQuery,
            cancellationToken).ConfigureAwait(false);

        if (selection.SelectedWork is null)
        {
            return PreUiTrainingWorkflowPreparationResult.Blocked(selection);
        }

        var generatedContent = contentPreparer.Prepare(
            new SelectedWorkGeneratedContentPreparationRequest(
                selection.SelectedWork,
                request.ContentKind,
                request.EquivalenceClass,
                request.FreshnessPolicy,
                request.Seed,
                request.SelectionQuery.AsOf,
                request.PreviouslyUsedContentIds,
                request.AdditionalCriticalConstraints,
                request.LoadChangeMode,
                request.IncreasedVariablesStableSeparately,
                request.TransferCapacity,
                request.TransferDistance));
        if (!generatedContent.IsPrepared)
        {
            return PreUiTrainingWorkflowPreparationResult.ContentRejected(selection, generatedContent);
        }

        var runtimeSession = runtimeSessionPreparer.Prepare(
            new SelectedWorkRuntimeSessionPreparationRequest(
                request.RuntimeSessionId,
                generatedContent,
                request.InputOptions));
        if (!runtimeSession.IsPrepared)
        {
            return PreUiTrainingWorkflowPreparationResult.RuntimeRejected(
                selection,
                generatedContent,
                runtimeSession);
        }

        LocalGeneratedDrillInstanceRecord generatedRecord;
        try
        {
            generatedRecord = ToInSessionGeneratedInstanceRecord(
                generatedContent.PersistenceHandoff!,
                runtimeSession.SessionId);
            await generatedDrillInstanceStore.SaveAsync(generatedRecord, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return PreUiTrainingWorkflowPreparationResult.PersistenceRejected(
                selection,
                generatedContent,
                runtimeSession,
                exception);
        }
        catch (InvalidOperationException exception)
        {
            return PreUiTrainingWorkflowPreparationResult.PersistenceRejected(
                selection,
                generatedContent,
                runtimeSession,
                exception);
        }

        return PreUiTrainingWorkflowPreparationResult.Prepared(
            selection,
            generatedContent,
            runtimeSession,
            generatedRecord);
    }

    public async ValueTask<PreUiTrainingWorkflowCompletionResult> CompleteSessionAsync(
        PreUiTrainingWorkflowCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var processingResult = await completedSessionProcessor.ProcessAsync(
            request.ProcessingRequest,
            cancellationToken).ConfigureAwait(false);
        await activeSnapshotService.DeleteAsync(
            request.ProcessingRequest.Result.SessionId,
            cancellationToken).ConfigureAwait(false);
        var refreshedState = await stateLoader.LoadAsync(
            new CurrentTrainingStateQuery(
                request.RefreshStateAsOf,
                request.RecentSessionLimit,
                request.EvidenceSummaryLimit,
                request.ProgressRecordLimit),
            cancellationToken).ConfigureAwait(false);

        return new PreUiTrainingWorkflowCompletionResult(
            processingResult,
            refreshedState);
    }

    public async ValueTask<PreUiTrainingWorkflowStartResult> StartResumableSessionAsync(
        PreUiTrainingWorkflowStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var runtimeSession = request.RuntimeSession;
        if (!runtimeSession.IsPrepared ||
            runtimeSession.SessionDefinition is null ||
            runtimeSession.PhasePlan is null)
        {
            return PreUiTrainingWorkflowStartResult.Rejected(
                PreUiTrainingWorkflowStartStatus.RuntimeRejected,
                runtimeSession.SessionId,
                "Runtime session must be prepared before it can be started.");
        }

        RuntimeInputCommandHandler commandHandler;
        RuntimeCueScheduler? cueScheduler;
        try
        {
            commandHandler = RuntimeInputCommandHandler.Start(
                runtimeSession.SessionId,
                runtimeSession.SessionDefinition,
                runtimeSession.PhasePlan,
                request.Clock,
                runtimeSession.InputOptions);
            cueScheduler = runtimeSession.CueSchedule is null
                ? null
                : new RuntimeCueScheduler(runtimeSession.CueSchedule, request.Clock, commandHandler.EventLog);
        }
        catch (ArgumentException exception)
        {
            return PreUiTrainingWorkflowStartResult.Rejected(
                PreUiTrainingWorkflowStartStatus.RuntimeRejected,
                runtimeSession.SessionId,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return PreUiTrainingWorkflowStartResult.Rejected(
                PreUiTrainingWorkflowStartStatus.RuntimeRejected,
                runtimeSession.SessionId,
                exception.Message);
        }

        var snapshot = commandHandler.CaptureSnapshot();
        var cueSnapshot = cueScheduler?.CaptureSnapshot();
        var activeSession = PreUiActiveSessionResumeState.FromRuntimeSnapshot(
            snapshot,
            cueSnapshot,
            request.SaveActiveSnapshot
                ? "Runtime session started and active snapshot saved."
                : "Runtime session started without saving an active snapshot.");

        if (!request.SaveActiveSnapshot)
        {
            activeSession = PreUiActiveSessionResumeState.NotPersistedFromRuntimeSnapshot(
                snapshot,
                cueSnapshot,
                "Runtime session started without saving an active snapshot.");

            return PreUiTrainingWorkflowStartResult.Started(
                runtimeSession.SessionId,
                commandHandler,
                cueScheduler,
                activeSession);
        }

        try
        {
            await activeSnapshotService.SaveAsync(
                new ActiveRuntimeSessionSnapshotSaveRequest(snapshot, cueSnapshot),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return PreUiTrainingWorkflowStartResult.Rejected(
                PreUiTrainingWorkflowStartStatus.SnapshotRejected,
                runtimeSession.SessionId,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return PreUiTrainingWorkflowStartResult.Rejected(
                PreUiTrainingWorkflowStartStatus.SnapshotRejected,
                runtimeSession.SessionId,
                exception.Message);
        }

        return PreUiTrainingWorkflowStartResult.Started(
            runtimeSession.SessionId,
            commandHandler,
            cueScheduler,
            activeSession);
    }

    public async ValueTask<PreUiActiveSessionResumeState> SaveActiveSessionSnapshotAsync(
        ActiveRuntimeSessionSnapshotSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await activeSnapshotService.SaveAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return PreUiActiveSessionResumeState.FromRuntimeSnapshot(
            request.Snapshot,
            request.CueSchedulerSnapshot,
            "Active runtime session snapshot saved.");
    }

    public async ValueTask<PreUiActiveSessionResumeResult> TryResumeActiveSessionAsync(
        PreUiActiveSessionResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var restored = await activeSnapshotService.RestoreAsync(
            new ActiveRuntimeSessionSnapshotRestoreRequest(
                request.SessionId,
                request.Clock,
                request.CueSchedule),
            cancellationToken).ConfigureAwait(false);

        var state = restored.Status switch
        {
            ActiveRuntimeSessionSnapshotRestoreStatus.Restored =>
                PreUiActiveSessionResumeState.Resumable(restored.SnapshotRecord!, restored.Detail),
            ActiveRuntimeSessionSnapshotRestoreStatus.NotFound =>
                PreUiActiveSessionResumeState.NotFound(request.SessionId, restored.Detail),
            ActiveRuntimeSessionSnapshotRestoreStatus.Unsafe =>
                PreUiActiveSessionResumeState.Unsafe(restored.SnapshotRecord, request.SessionId, restored.Detail),
            _ => throw new ArgumentOutOfRangeException(nameof(restored), restored.Status, "Unknown active session restore status."),
        };

        return new PreUiActiveSessionResumeResult(
            state,
            restored.CommandHandler,
            restored.CueScheduler);
    }

    public async ValueTask<PreUiActiveSessionInvalidationResult> InvalidateActiveSessionSnapshotAsync(
        PreUiActiveSessionInvalidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await activeSnapshotService.DeleteAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        return new PreUiActiveSessionInvalidationResult(
            request.SessionId,
            Cleared: true,
            request.Reason);
    }

    private static LocalGeneratedDrillInstanceRecord ToInSessionGeneratedInstanceRecord(
        GeneratedContentPersistenceHandoff handoff,
        string runtimeSessionId)
    {
        ArgumentNullException.ThrowIfNull(handoff);

        return new LocalGeneratedDrillInstanceRecord(
            handoff.InstanceId,
            handoff.GeneratedOn,
            handoff.Branch,
            handoff.Level,
            handoff.Drill,
            handoff.LoadVariables,
            new LocalGeneratedDrillContentIdentity(
                handoff.ContentIdentity,
                handoff.ContentVersion),
            LocalGeneratedDrillInstanceState.InSession,
            activeSessionId: runtimeSessionId,
            resultEvidenceArtifactId: null,
            handoff.ContentSummary,
            handoff.FreshnessPolicy,
            handoff.AuditMaterials.Select(material => new LocalGeneratedDrillAuditMaterial(
                material.Kind.ToString(),
                material.Name,
                material.Value)));
    }
}
