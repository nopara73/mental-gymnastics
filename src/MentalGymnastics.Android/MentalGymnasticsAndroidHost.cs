using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Android;

public sealed class MentalGymnasticsAndroidHost
{
    private const string LocalDatabaseFileName = "mental-gymnastics-local.json";
    private const string LatestActiveSessionFallbackId = "latest-active-session";

    private readonly CurrentTrainingStateLoader stateLoader;
    private readonly DailyTrainingWorkflowService dailyTrainingService;
    private readonly PreUiTrainingWorkflowService workflowService;
    private readonly LocalDataBackupWorkflowService localDataService;
    private AndroidSessionStartSnapshot? preparedSessionStart;
    private PreUiLiveSessionController? liveSessionController;
    private string? activeMainFailureModeAvoided;
    private bool startupStateReconciled;
#if DEBUG
    private ManualRuntimeClock? protocolAuditClock;
#endif

    private MentalGymnasticsAndroidHost(
        AppStartupConfiguration configuration,
        string localBackupDirectoryPath)
    {
        Configuration = configuration;
        stateLoader = new CurrentTrainingStateLoader(configuration);
        dailyTrainingService = new DailyTrainingWorkflowService(configuration);
        workflowService = new PreUiTrainingWorkflowService(configuration);
        localDataService = new LocalDataBackupWorkflowService(configuration, localBackupDirectoryPath);
    }

    public AppStartupConfiguration Configuration { get; }

    public ApplicationIntegrationCapabilities Capabilities => ApplicationIntegrationBoundary.Capabilities;

    public bool HasActiveLiveSession => liveSessionController is not null;

    public static MentalGymnasticsAndroidHost Create(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var filesDirectory = activity.FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException("Android did not provide an app-owned files directory.");
        var databasePath = Path.Combine(filesDirectory, LocalDatabaseFileName);
        var externalFilesDirectory = activity.GetExternalFilesDir(null)?.AbsolutePath ?? filesDirectory;
        var localBackupDirectoryPath = Path.Combine(externalFilesDirectory, "local-backups");

        return new MentalGymnasticsAndroidHost(
            AppStartupConfiguration.ForAppOwnedLocalStoragePath(databasePath),
            localBackupDirectoryPath);
    }

#if DEBUG
    public static MentalGymnasticsAndroidHost CreateProtocolAudit(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var cacheDirectory = activity.CacheDir?.AbsolutePath
            ?? throw new InvalidOperationException("Android did not provide an app-owned cache directory.");
        var auditId = Guid.NewGuid().ToString("N");
        var databasePath = Path.Combine(
            cacheDirectory,
            $"mental-gymnastics-protocol-audit-{auditId}.json");
        var backupDirectoryPath = Path.Combine(
            cacheDirectory,
            $"protocol-audit-backups-{auditId}");

        return new MentalGymnasticsAndroidHost(
            AppStartupConfiguration.ForAppOwnedLocalStoragePath(databasePath),
            backupDirectoryPath);
    }
#endif

    public async Task<AndroidTrainingStateSnapshot> LoadTodayAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var query = new CurrentTrainingStateQuery(
            TrainingDate.From(today.Year, today.Month, today.Day));
        await EnsureStartupStateReconciledAsync(query.AsOf, cancellationToken).ConfigureAwait(false);
        var currentState = await stateLoader.LoadAsync(query, cancellationToken).ConfigureAwait(false);
        var dailyTraining = await dailyTrainingService.LoadOrCreateAsync(
            query.AsOf,
            cancellationToken).ConfigureAwait(false);

        return new AndroidTrainingStateSnapshot(
            currentState,
            Capabilities,
            Configuration.LocalDatabasePath,
            today,
            await localDataService.LoadAsync(cancellationToken).ConfigureAwait(false),
            dailyTraining);
    }

    public async Task<AndroidSessionStartSnapshot> PrepareSessionStartAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var trainingDate = TrainingDate.From(today.Year, today.Month, today.Day);
        var dailyTraining = await dailyTrainingService.LoadOrCreateAsync(
            trainingDate,
            cancellationToken).ConfigureAwait(false);
        var currentBlock = dailyTraining.CurrentBlock
            ?? throw new InvalidOperationException(DailyTerminalDetail(dailyTraining.Status));
        if (!dailyTraining.CanPrepare)
        {
            throw new InvalidOperationException("Today's current block is already prepared or active.");
        }

        var preparation = await workflowService.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(trainingDate, currentBlock.RequestedWork),
                preparationSource: $"android-{currentBlock.Record.BlockId}-{Guid.NewGuid():N}"),
            cancellationToken).ConfigureAwait(false);

        if (preparation.IsPrepared && preparation.RuntimeSession is not null)
        {
            dailyTraining = await dailyTrainingService.MarkPreparedAsync(
                trainingDate,
                currentBlock.Record.BlockId,
                preparation.RuntimeSession.SessionId,
                cancellationToken).ConfigureAwait(false);
        }

        preparedSessionStart = new AndroidSessionStartSnapshot(
            preparation,
            Capabilities,
            Configuration.LocalDatabasePath,
            today,
            await localDataService.LoadAsync(cancellationToken).ConfigureAwait(false),
            dailyTraining);

        liveSessionController = null;
        return preparedSessionStart;
    }

#if DEBUG
    public async Task<AndroidSessionStartSnapshot> PrepareProtocolAuditSessionAsync(
        DrillId drill,
        GlobalLevelId? level = null,
        CancellationToken cancellationToken = default)
    {
        var definition = ExecutableStandardCatalog.Standards
            .Where(item => item.Drill == drill && (!level.HasValue || item.Level == level.Value))
            .OrderBy(item => item.Level)
            .FirstOrDefault()
            ?? throw new ArgumentOutOfRangeException(
                nameof(drill),
                drill,
                $"Drill has no executable standard{(level.HasValue ? $" at {level.Value}" : string.Empty)}.");
        var today = DateTime.Now.Date;
        var trainingDate = TrainingDate.From(today.Year, today.Month, today.Day);

        await new LocalPractitionerStateStore(Configuration.LocalDatabaseOptions)
            .SaveAsync(
                new PractitionerState(
                [
                    new BranchLevelStatus(
                        definition.Branch,
                        definition.Level,
                        BranchLevelState.Maintenance),
                ]),
                cancellationToken)
            .ConfigureAwait(false);

        var preparation = await workflowService.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    trainingDate,
                    new RequestedTrainingWork(
                        definition.Branch,
                        definition.Level,
                        drill,
                        AppTrainingSessionType.Maintenance)),
                preparationSource: $"android-protocol-audit-{drill}-{definition.Level}-{Guid.NewGuid():N}"),
            cancellationToken).ConfigureAwait(false);
        if (!preparation.IsPrepared || preparation.RuntimeSession is null)
        {
            throw new InvalidOperationException(PreparationRejectionDetail(preparation));
        }

        preparedSessionStart = new AndroidSessionStartSnapshot(
            preparation,
            Capabilities,
            Configuration.LocalDatabasePath,
            today,
            await localDataService.LoadAsync(cancellationToken).ConfigureAwait(false));
        liveSessionController = null;
        activeMainFailureModeAvoided = null;
        return preparedSessionStart;
    }

    public async Task<AndroidLiveSessionSnapshot> StartPreparedProtocolAuditSessionAsync(
        bool beginWork = true,
        CancellationToken cancellationToken = default)
    {
        var prepared = preparedSessionStart?.Preparation
            ?? throw new InvalidOperationException("No protocol-audit exercise is ready to start.");
        var runtimeSession = prepared.RuntimeSession
            ?? throw new InvalidOperationException("The protocol-audit exercise has no runtime session.");
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        protocolAuditClock = clock;
        var started = await workflowService.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                runtimeSession,
                clock,
                saveActiveSnapshot: false),
            cancellationToken).ConfigureAwait(false);
        if (!started.IsStarted)
        {
            throw new InvalidOperationException(StartRejectionDetail(started));
        }

        liveSessionController = new PreUiLiveSessionController(
            workflowService,
            runtimeSession,
            started,
            saveActiveSnapshot: false);
        var state = liveSessionController.CaptureState();
        if (beginWork && state.CurrentPhaseKind == RuntimeSessionPhaseKind.InstructionPrep)
        {
            state = await liveSessionController.HandleCommandAsync(
                new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase),
                cancellationToken).ConfigureAwait(false);
        }

        if (state.CurrentPhaseKind == RuntimeSessionPhaseKind.CueResponse &&
            state.ActiveCue is null)
        {
            AdvanceProtocolAuditClockToFirstCue(clock);
            state = await liveSessionController.RefreshAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return LiveSnapshot(state);
    }

    public async Task<AndroidLiveSessionSnapshot> AdvanceProtocolAuditToResponseAsync(
        CancellationToken cancellationToken = default)
    {
        var controller = liveSessionController
            ?? throw new InvalidOperationException("No protocol-audit session is active.");
        var clock = protocolAuditClock
            ?? throw new InvalidOperationException("The protocol-audit clock is not available.");
        var state = controller.CaptureState();
        var effectiveDrill = state.SourceDrill ?? state.Drill;

        if (effectiveDrill is DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule &&
            state.CurrentPhaseId == "rule-declaration")
        {
            var declaration = string.Join(
                "; ",
                state.CurrentMaterials
                    .Where(material => material.Kind is "RuleStatement" or "ExceptionDefinition")
                    .Select(material => material.Value));
            state = await AuditCommandAsync(
                controller,
                RuntimeInputCommandKind.SubmitAnswer,
                declaration,
                cancellationToken).ConfigureAwait(false);
            state = await AuditCommandAsync(
                controller,
                RuntimeInputCommandKind.FinishPhase,
                value: null,
                cancellationToken).ConfigureAwait(false);
        }
        else if (state.Drill is DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping &&
            state.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork)
        {
            var answer = state.Drill == DrillId.CO1RuleExtraction
                ? "One testable rule covering the shown positive and negative examples."
                : "Screens reports before escalation; accepted reports carry evidence tags.";
            state = await AuditCommandAsync(
                controller,
                RuntimeInputCommandKind.SubmitAnswer,
                answer,
                cancellationToken).ConfigureAwait(false);
            state = await AuditCommandAsync(
                controller,
                RuntimeInputCommandKind.FinishPhase,
                value: null,
                cancellationToken).ConfigureAwait(false);
        }
        else if (state.CurrentPhaseKind == RuntimeSessionPhaseKind.EncodeWindow)
        {
            state = await AuditCommandAsync(
                controller,
                RuntimeInputCommandKind.FinishPhase,
                value: null,
                cancellationToken).ConfigureAwait(false);
        }

        while (state.CurrentPhaseKind == RuntimeSessionPhaseKind.DelayWindow)
        {
            var remaining = state.Timer.Remaining
                ?? throw new InvalidOperationException("A protocol-audit delay has no remaining duration.");
            clock.AdvanceBy(new RuntimeDuration(remaining));
            state = await controller.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        if (state.Drill == DrillId.AI2DisruptionRecovery &&
            state.CurrentPhaseKind is RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.CueResponse)
        {
            AdvanceProtocolAuditClockToFirstCue(
                clock,
                RuntimeCueResponseExpectation.ResponseRequired,
                RuntimeCueKind.Interruption);
            state = await controller.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (state.CurrentPhaseKind == RuntimeSessionPhaseKind.CueResponse &&
            effectiveDrill is DrillId.FS2InvalidCueFilter or DrillId.IR2ExceptionRule)
        {
            AdvanceProtocolAuditClockToFirstCue(
                clock,
                RuntimeCueResponseExpectation.NoResponseExpected);
            state = await controller.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (state.CurrentPhaseKind == RuntimeSessionPhaseKind.CueResponse && state.ActiveCue is null)
        {
            AdvanceProtocolAuditClockToFirstCue(clock);
            state = await controller.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        if (state.CurrentPhaseKind == RuntimeSessionPhaseKind.Audit &&
            state.Commands.Any(command =>
                command.Command == RuntimeInputCommandKind.StartAudit && command.IsAvailable))
        {
            state = await AuditCommandAsync(
                controller,
                RuntimeInputCommandKind.StartAudit,
                value: null,
                cancellationToken).ConfigureAwait(false);
        }

        return LiveSnapshot(state);
    }

    private void AdvanceProtocolAuditClockToFirstCue(
        ManualRuntimeClock clock,
        RuntimeCueResponseExpectation? expectation = null,
        RuntimeCueKind? kind = null)
    {
        var cues = preparedSessionStart?.Preparation.RuntimeSession?.CueSchedule?.Cues
            ?? throw new InvalidOperationException("The protocol-audit exercise has no scheduled cue.");
        var cue = cues.FirstOrDefault(item =>
            (!expectation.HasValue || item.ResponseExpectation == expectation.Value) &&
            (!kind.HasValue || item.Kind == kind.Value));
        if (cue is null)
        {
            throw new InvalidOperationException(
                $"The protocol-audit exercise has no matching cue ({kind?.ToString() ?? "any kind"}, " +
                $"{expectation?.ToString() ?? "any response"}).");
        }

        if (cue.ScheduledAt.Offset > clock.Now.Offset)
        {
            clock.AdvanceTo(cue.ScheduledAt);
        }
    }

    private static async Task<PreUiLiveSessionState> AuditCommandAsync(
        PreUiLiveSessionController controller,
        RuntimeInputCommandKind command,
        string? value,
        CancellationToken cancellationToken)
    {
        var state = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(command, value: value),
            cancellationToken).ConfigureAwait(false);
        if (state.LastCommand?.IsAccepted != true)
        {
            throw new InvalidOperationException(
                $"Protocol-audit command {command} was rejected: {state.LastCommand?.Detail}");
        }

        return state;
    }
#endif

    public async Task<AndroidTrainingStateSnapshot> CompleteDueGlobalReviewAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var trainingDate = TrainingDate.From(today.Year, today.Month, today.Day);
        var daily = await dailyTrainingService.LoadOrCreateAsync(trainingDate, cancellationToken)
            .ConfigureAwait(false);
        var block = daily.CurrentBlock?.Record
            ?? throw new InvalidOperationException("No global review is due today.");
        if (block.Role != LocalDailyTrainingBlockRole.Review)
        {
            throw new InvalidOperationException("Today's prescription is not a global review.");
        }

        await dailyTrainingService.CompleteDueGlobalReviewAsync(
            trainingDate,
            block.BlockId,
            cancellationToken).ConfigureAwait(false);
        preparedSessionStart = null;
        liveSessionController = null;
        activeMainFailureModeAvoided = null;
        return await LoadTodayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AndroidLiveSessionSnapshot> StartPreparedLiveSessionAsync(
        CancellationToken cancellationToken = default,
        bool saveActiveSnapshot = true,
        string? mainFailureModeAvoided = null)
    {
        var prepared = preparedSessionStart?.Preparation
            ?? throw new InvalidOperationException("No exercise is ready to start.");
        var runtimeSession = prepared.RuntimeSession
            ?? throw new InvalidOperationException("This exercise is not ready to start.");

        if (!prepared.CanStartRuntimeSession)
        {
            throw new InvalidOperationException(PreparationRejectionDetail(prepared));
        }

        activeMainFailureModeAvoided = ValidateFailureModeSelection(
            prepared.Selection.SelectedWork,
            mainFailureModeAvoided);

        var started = await workflowService.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                runtimeSession,
                CreateLifecycleClock(),
                saveActiveSnapshot: saveActiveSnapshot),
            cancellationToken).ConfigureAwait(false);

        if (!started.IsStarted)
        {
            throw new InvalidOperationException(StartRejectionDetail(started));
        }

        liveSessionController = new PreUiLiveSessionController(
            workflowService,
            runtimeSession,
            started,
            saveActiveSnapshot);

        var state = liveSessionController.CaptureState();
        if (state.CurrentPhaseKind == RuntimeSessionPhaseKind.InstructionPrep)
        {
            state = await liveSessionController.HandleCommandAsync(
                new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase),
                cancellationToken).ConfigureAwait(false);
            if (state.LastCommand?.IsAccepted != true)
            {
                throw new InvalidOperationException("The exercise could not enter its first work phase.");
            }
        }
        else
        {
            state = await liveSessionController.RefreshAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await dailyTrainingService.MarkActiveAsync(
            runtimeSession.SessionId,
            activeMainFailureModeAvoided,
            cancellationToken).ConfigureAwait(false);

        return LiveSnapshot(state);
    }

    public async Task<AndroidTrainingStateSnapshot> CancelPreparedSessionStartAsync(
        CancellationToken cancellationToken = default)
    {
        var prepared = preparedSessionStart?.Preparation;
        var sessionId = prepared?.RuntimeSession?.SessionId;
        if (sessionId is not null)
        {
            await workflowService.CancelPreparedSessionAsync(
                new PreUiPreparedSessionCancellationRequest(
                    sessionId,
                    prepared?.GeneratedInstanceRecord?.InstanceId,
                    "User left setup before active work started."),
                cancellationToken).ConfigureAwait(false);
            await dailyTrainingService.CancelPreparedAsync(sessionId, cancellationToken)
                .ConfigureAwait(false);
        }

        preparedSessionStart = null;
        liveSessionController = null;
        activeMainFailureModeAvoided = null;
        return await LoadTodayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AndroidTrainingStateSnapshot> StopTodayAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var date = TrainingDate.From(today.Year, today.Month, today.Day);
        await dailyTrainingService.StopRemainingAsync(date, cancellationToken).ConfigureAwait(false);
        preparedSessionStart = null;
        liveSessionController = null;
        activeMainFailureModeAvoided = null;
        return await LoadTodayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SuspendActiveSessionForLifecycleAsync(
        CancellationToken cancellationToken = default)
    {
        var controller = liveSessionController;
        if (controller is null)
        {
            return;
        }

        var state = await controller.SuspendForLifecycleAsync(cancellationToken)
            .ConfigureAwait(false);
        if (state.IsTerminal)
        {
            preparedSessionStart = null;
            liveSessionController = null;
        }
    }

    public async Task<AndroidLiveSessionSnapshot> ResumeActiveSessionForLifecycleAsync(
        CancellationToken cancellationToken = default)
    {
        var controller = liveSessionController ??
            throw new InvalidOperationException("No active live session is available to resume.");
        var state = await controller.ResumeFromLifecycleAsync(cancellationToken)
            .ConfigureAwait(false);
        return LiveSnapshot(state);
    }

    public async Task<AndroidActiveSessionResumeSnapshot> TryResumeLatestActiveSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var trainingDate = TrainingDate.From(today.Year, today.Month, today.Day);
        await EnsureStartupStateReconciledAsync(trainingDate, cancellationToken).ConfigureAwait(false);
        var resumed = await workflowService.TryResumeLatestActiveSessionAsync(
            new PreUiActiveSessionResumeLatestRequest(CreateLifecycleClock()),
            cancellationToken).ConfigureAwait(false);
        if (resumed.State.Status == PreUiActiveSessionResumeStatus.Unsafe)
        {
            var activeDaily = await dailyTrainingService.LoadOrCreateAsync(trainingDate, cancellationToken)
                .ConfigureAwait(false);
            var interruptedSessionId = resumed.State.SessionId == LatestActiveSessionFallbackId
                ? activeDaily.CurrentBlock?.Record.SessionId
                : resumed.State.SessionId;
            if (interruptedSessionId is not null)
            {
                await dailyTrainingService.AbandonInterruptedSessionAsync(
                    trainingDate,
                    interruptedSessionId,
                    $"Stored active work could not be resumed safely: {resumed.State.Detail}",
                    clearAllSnapshots: resumed.State.SessionId == LatestActiveSessionFallbackId,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var trainingState = await LoadTodayAsync(cancellationToken).ConfigureAwait(false);

        if (!resumed.CanResume || resumed.CommandHandler is null)
        {
            preparedSessionStart = null;
            liveSessionController = null;
            activeMainFailureModeAvoided = null;
            return new AndroidActiveSessionResumeSnapshot(
                resumed.State,
                LiveSession: null,
                trainingState);
        }

        preparedSessionStart = null;
        liveSessionController = new PreUiLiveSessionController(
            workflowService,
            resumed.CommandHandler,
            resumed.CueScheduler,
            inputMaterials: resumed.InputMaterials,
            appSessionType: RestoredAppSessionType(resumed.State, trainingState));
        activeMainFailureModeAvoided = trainingState.DailyTraining?.CurrentBlock?.Record.MainFailureModeAvoided;
        var state = await liveSessionController.ResumeFromLifecycleAsync(cancellationToken)
            .ConfigureAwait(false);
        return new AndroidActiveSessionResumeSnapshot(
            resumed.State,
            LiveSnapshot(state),
            trainingState);
    }

    private static AppTrainingSessionType? RestoredAppSessionType(
        PreUiActiveSessionResumeState resume,
        AndroidTrainingStateSnapshot trainingState)
    {
        var work = trainingState.Presentation.PrimaryPrescribedWork;
        return work?.SessionType is { } sessionType &&
            work.Drill == resume.Drill &&
            work.BranchLevels.Any(branchLevel =>
                branchLevel.Branch == resume.Branch && branchLevel.Level == resume.Level)
                ? sessionType
                : null;
    }

    public async Task<AndroidTrainingStateSnapshot> InvalidateActiveSessionSnapshotAsync(
        string sessionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var trainingDate = TrainingDate.From(today.Year, today.Month, today.Day);
        var daily = await dailyTrainingService.LoadOrCreateAsync(trainingDate, cancellationToken)
            .ConfigureAwait(false);
        var interruptedSessionId = sessionId == LatestActiveSessionFallbackId
            ? daily.CurrentBlock?.Record.SessionId
            : sessionId;
        var reconciled = interruptedSessionId is null
            ? null
            : await dailyTrainingService.AbandonInterruptedSessionAsync(
                trainingDate,
                interruptedSessionId,
                reason,
                clearAllSnapshots: sessionId == LatestActiveSessionFallbackId,
                cancellationToken).ConfigureAwait(false);
        if (reconciled?.Status != DailyTrainingInterruptionReconciliationStatus.Abandoned)
        {
            await workflowService.InvalidateActiveSessionSnapshotAsync(
                new PreUiActiveSessionInvalidationRequest(sessionId, reason),
                cancellationToken).ConfigureAwait(false);
        }

        preparedSessionStart = null;
        liveSessionController = null;
        activeMainFailureModeAvoided = null;
        return await LoadTodayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AndroidLiveSessionSnapshot> RefreshLiveSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var controller = liveSessionController
            ?? throw new InvalidOperationException("No live session is active.");

        var state = await controller.RefreshAsync(cancellationToken)
            .ConfigureAwait(false);
        return LiveSnapshot(state);
    }

    public async Task<AndroidLiveSessionSnapshot> HandleLiveSessionCommandAsync(
        RuntimeInputCommandKind command,
        string? targetId = null,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        var controller = liveSessionController
            ?? throw new InvalidOperationException("No live session is active.");

        var state = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(command, targetId, value),
            cancellationToken).ConfigureAwait(false);
        return LiveSnapshot(state);
    }

    public async Task<AndroidLiveSessionCompletionSnapshot> CompleteLiveSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var controller = liveSessionController
            ?? throw new InvalidOperationException("No live session is active.");

        var today = DateTime.Now.Date;
        var completedOn = TrainingDate.From(today.Year, today.Month, today.Day);
        var liveState = controller.CaptureState();
        var isRecovery = liveState.SessionType == SessionType.Recovery;
        var currentState = await stateLoader.LoadAsync(
            new CurrentTrainingStateQuery(completedOn),
            cancellationToken).ConfigureAwait(false);
        var deloadAlreadyMarked = currentState.RecentSessions.Any(session =>
            session.DeloadMarked &&
            session.Date.DaysUntil(completedOn) is >= 0 and < 7);
        var result = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(
                completedOn,
                recoveryMarked: isRecovery,
                deloadMarked: isRecovery &&
                    currentState.DeloadDecision.ShouldDeload &&
                    !deloadAlreadyMarked,
                mainFailureModeAvoided: activeMainFailureModeAvoided),
            cancellationToken).ConfigureAwait(false);
        var dailyTraining = result.WorkflowResult?.ProcessingResult.DailyTraining;

        if (result.IsFinalized)
        {
            if (result.Status == PreUiLiveSessionCompletionStatus.CancelledBeforeWork)
            {
                dailyTraining = await dailyTrainingService.CancelPreparedAsync(
                    controller.CaptureState().SessionId,
                    cancellationToken).ConfigureAwait(false);
            }

            preparedSessionStart = null;
            liveSessionController = null;
            activeMainFailureModeAvoided = null;
        }

        return new AndroidLiveSessionCompletionSnapshot(
            result,
            Capabilities,
            Configuration.LocalDatabasePath,
            today,
            await localDataService.LoadAsync(cancellationToken).ConfigureAwait(false),
            dailyTraining);
    }

    public async Task<AndroidLocalDataOperationSnapshot> ExportLocalBackupAsync(
        CancellationToken cancellationToken = default)
    {
        var operation = await localDataService.ExportAsync(
            new LocalDataBackupExportRequest(DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return await LocalDataOperationSnapshotAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AndroidLocalDataOperationSnapshot> ValidateCurrentLocalDataAsync(
        CancellationToken cancellationToken = default)
    {
        var operation = await localDataService.ValidateCurrentAsync(cancellationToken)
            .ConfigureAwait(false);

        return await LocalDataOperationSnapshotAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AndroidLocalDataOperationSnapshot> ValidateLatestLocalBackupAsync(
        CancellationToken cancellationToken = default)
    {
        var operation = await localDataService.ValidateLatestBackupAsync(cancellationToken)
            .ConfigureAwait(false);

        return await LocalDataOperationSnapshotAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AndroidLocalDataOperationSnapshot> RestoreLatestLocalBackupAsync(
        bool confirmReplaceLocalData,
        CancellationToken cancellationToken = default)
    {
        if (liveSessionController is not null)
        {
            var localData = await localDataService.LoadAsync(cancellationToken).ConfigureAwait(false);
            return await LocalDataOperationSnapshotAsync(
                new LocalDataBackupOperationResult(
                    LocalDataBackupOperationKind.RestoreLatestBackup,
                    LocalDataBackupOperationStatus.Failed,
                    "Finish or stop the active live session before restoring local data.",
                    localData.LatestBackup,
                    localData.CurrentIntegrity),
                cancellationToken).ConfigureAwait(false);
        }

        var operation = await localDataService.RestoreLatestBackupAsync(
            new LocalDataBackupRestoreRequest(confirmReplaceLocalData),
            cancellationToken).ConfigureAwait(false);

        if (operation.Status == LocalDataBackupOperationStatus.Succeeded)
        {
            preparedSessionStart = null;
            liveSessionController = null;
            activeMainFailureModeAvoided = null;
            startupStateReconciled = false;
        }

        return await LocalDataOperationSnapshotAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    private AndroidLiveSessionSnapshot LiveSnapshot(PreUiLiveSessionState liveSession)
    {
        return new AndroidLiveSessionSnapshot(
            liveSession,
            Capabilities,
            Configuration.LocalDatabasePath,
            DateTime.Now.Date);
    }

    private static TimeProviderRuntimeClock CreateLifecycleClock()
    {
        return TimeProviderRuntimeClock.CreateUtcTimeline();
    }

    private async Task EnsureStartupStateReconciledAsync(
        TrainingDate date,
        CancellationToken cancellationToken)
    {
        if (startupStateReconciled)
        {
            return;
        }

        await dailyTrainingService.ReconcileInterruptedStateAsync(date, cancellationToken)
            .ConfigureAwait(false);
        startupStateReconciled = true;
    }

    private async Task<AndroidLocalDataOperationSnapshot> LocalDataOperationSnapshotAsync(
        LocalDataBackupOperationResult operation,
        CancellationToken cancellationToken)
    {
        var trainingState = await LoadTodayAsync(cancellationToken).ConfigureAwait(false);
        return new AndroidLocalDataOperationSnapshot(operation, trainingState);
    }

    private static string PreparationRejectionDetail(PreUiTrainingWorkflowPreparationResult preparation)
    {
        var details = preparation.Rejections.Select(rejection => rejection.Detail)
            .Concat(preparation.Selection.Blockers.Select(blocker => blocker.Detail))
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return details.Length == 0
            ? "This exercise is not ready to start."
            : string.Join(Environment.NewLine, details);
    }

    private static string StartRejectionDetail(PreUiTrainingWorkflowStartResult started)
    {
        var details = started.Rejections
            .Select(rejection => rejection.Detail)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return details.Length == 0
            ? "This exercise could not start."
            : string.Join(Environment.NewLine, details);
    }

    private static string DailyTerminalDetail(DailyTrainingWorkflowStatus status)
    {
        return status switch
        {
            DailyTrainingWorkflowStatus.Done => "Today's training is complete.",
            DailyTrainingWorkflowStatus.Stopped => "Today's remaining work was stopped.",
            DailyTrainingWorkflowStatus.OffDay => "Today is an off day.",
            _ => "No daily training block is available.",
        };
    }

    private static string? ValidateFailureModeSelection(
        SelectedTrainingWork? selectedWork,
        string? selectedFailureMode)
    {
        if (selectedWork is null)
        {
            return null;
        }

        var requiresSelection = selectedWork.SessionType is
            AppTrainingSessionType.Test or
            AppTrainingSessionType.Stabilization or
            AppTrainingSessionType.Transfer;
        if (!requiresSelection)
        {
            return null;
        }

        var documented = ProgramCatalog.Drills.Single(drill => drill.Id == selectedWork.Drill)
            .FailureModes
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.IsNullOrWhiteSpace(selectedFailureMode)
            ? null
            : selectedFailureMode.Trim();
        if (normalized is null || !documented.Contains(normalized, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Select the documented failure mode you will guard against before formal work starts.");
        }

        return normalized;
    }
}

public sealed record AndroidTrainingStateSnapshot(
    CurrentTrainingStateReadModel CurrentState,
    ApplicationIntegrationCapabilities Capabilities,
    string LocalDatabasePath,
    DateTime LoadedDate,
    LocalDataBackupReadModel LocalData,
    DailyTrainingWorkflowReadModel? DailyTraining = null)
{
    public CurrentTrainingPresentationReadModel Presentation { get; } =
        TrainingPresentationMapper.FromCurrentState(CurrentState, DailyTraining);
}

public sealed record AndroidSessionStartSnapshot(
    PreUiTrainingWorkflowPreparationResult Preparation,
    ApplicationIntegrationCapabilities Capabilities,
    string LocalDatabasePath,
    DateTime PreparedDate,
    LocalDataBackupReadModel LocalData,
    DailyTrainingWorkflowReadModel? DailyTraining = null)
{
    public SessionPreflightPresentationReadModel Presentation { get; } =
        TrainingPresentationMapper.FromPreflight(Preparation);
}

public sealed record AndroidLiveSessionSnapshot(
    PreUiLiveSessionState LiveSession,
    ApplicationIntegrationCapabilities Capabilities,
    string LocalDatabasePath,
    DateTime LoadedDate)
{
    public LiveSessionPresentationReadModel Presentation { get; } =
        TrainingPresentationMapper.FromLiveSession(LiveSession);
}

public sealed record AndroidLiveSessionCompletionSnapshot(
    PreUiLiveSessionCompletionResult Completion,
    ApplicationIntegrationCapabilities Capabilities,
    string LocalDatabasePath,
    DateTime LoadedDate,
    LocalDataBackupReadModel LocalData,
    DailyTrainingWorkflowReadModel? DailyTraining = null)
{
    public ResultPresentationReadModel Presentation { get; } =
        TrainingPresentationMapper.FromResult(Completion);
}

public sealed record AndroidActiveSessionResumeSnapshot(
    PreUiActiveSessionResumeState ActiveSession,
    AndroidLiveSessionSnapshot? LiveSession,
    AndroidTrainingStateSnapshot TrainingState);

public sealed record AndroidLocalDataOperationSnapshot(
    LocalDataBackupOperationResult Operation,
    AndroidTrainingStateSnapshot TrainingState);
