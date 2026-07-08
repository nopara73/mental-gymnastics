using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Android;

public sealed class MentalGymnasticsAndroidHost
{
    private const string LocalDatabaseFileName = "mental-gymnastics-local.json";
    private static readonly RuntimeInstant LifecycleRestoreInitialInstant = new(TimeSpan.FromDays(365));

    private readonly CurrentTrainingStateLoader stateLoader;
    private readonly PreUiTrainingWorkflowService workflowService;
    private readonly LocalDataBackupWorkflowService localDataService;
    private AndroidSessionStartSnapshot? preparedSessionStart;
    private PreUiLiveSessionController? liveSessionController;

    private MentalGymnasticsAndroidHost(
        AppStartupConfiguration configuration,
        string localBackupDirectoryPath)
    {
        Configuration = configuration;
        stateLoader = new CurrentTrainingStateLoader(configuration);
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

    public async Task<AndroidTrainingStateSnapshot> LoadTodayAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var query = new CurrentTrainingStateQuery(
            TrainingDate.From(today.Year, today.Month, today.Day));
        var currentState = await stateLoader.LoadAsync(query, cancellationToken).ConfigureAwait(false);

        return new AndroidTrainingStateSnapshot(
            currentState,
            Capabilities,
            Configuration.LocalDatabasePath,
            today,
            await localDataService.LoadAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<AndroidSessionStartSnapshot> PrepareSessionStartAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var trainingDate = TrainingDate.From(today.Year, today.Month, today.Day);
        var preparation = await workflowService.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(trainingDate)),
            cancellationToken).ConfigureAwait(false);

        preparedSessionStart = new AndroidSessionStartSnapshot(
            preparation,
            Capabilities,
            Configuration.LocalDatabasePath,
            today,
            await localDataService.LoadAsync(cancellationToken).ConfigureAwait(false));

        liveSessionController = null;
        return preparedSessionStart;
    }

    public async Task<AndroidLiveSessionSnapshot> StartPreparedLiveSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var prepared = preparedSessionStart?.Preparation
            ?? throw new InvalidOperationException("No prepared Android session is available to start.");
        var runtimeSession = prepared.RuntimeSession
            ?? throw new InvalidOperationException("Prepared work is not startable.");

        if (!prepared.CanStartRuntimeSession)
        {
            throw new InvalidOperationException(PreparationRejectionDetail(prepared));
        }

        var started = await workflowService.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                runtimeSession,
                new TimeProviderRuntimeClock(),
                saveActiveSnapshot: true),
            cancellationToken).ConfigureAwait(false);

        if (!started.IsStarted)
        {
            throw new InvalidOperationException(StartRejectionDetail(started));
        }

        liveSessionController = new PreUiLiveSessionController(
            workflowService,
            runtimeSession,
            started);

        var state = await liveSessionController.RefreshAsync(cancellationToken)
            .ConfigureAwait(false);
        return LiveSnapshot(state);
    }

    public async Task SuspendActiveSessionForLifecycleAsync(
        CancellationToken cancellationToken = default)
    {
        var controller = liveSessionController;
        if (controller is null)
        {
            return;
        }

        var state = controller.CaptureState("Android lifecycle saved active session state.");
        var pauseCommand = state.Commands.FirstOrDefault(command =>
            command.Command == RuntimeInputCommandKind.Pause);

        if (!state.IsTerminal && pauseCommand?.IsAvailable == true)
        {
            state = await controller.HandleCommandAsync(
                new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.Pause),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _ = await controller.PersistActiveSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (state.IsTerminal ||
            state.LifecycleStatus == RuntimeSessionLifecycleStatus.Running &&
            pauseCommand?.IsAvailable != true)
        {
            preparedSessionStart = null;
            liveSessionController = null;
        }
    }

    public async Task<AndroidActiveSessionResumeSnapshot> TryResumeLatestActiveSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var resumed = await workflowService.TryResumeLatestActiveSessionAsync(
            new PreUiActiveSessionResumeLatestRequest(CreateLifecycleRestoreClock()),
            cancellationToken).ConfigureAwait(false);
        var trainingState = await LoadTodayAsync(cancellationToken).ConfigureAwait(false);

        if (!resumed.CanResume || resumed.CommandHandler is null)
        {
            preparedSessionStart = null;
            liveSessionController = null;
            return new AndroidActiveSessionResumeSnapshot(
                resumed.State,
                LiveSession: null,
                trainingState);
        }

        preparedSessionStart = null;
        liveSessionController = new PreUiLiveSessionController(
            workflowService,
            resumed.CommandHandler,
            resumed.CueScheduler);
        var state = liveSessionController.CaptureState(
            "Active session restored from local resume state.");
        return new AndroidActiveSessionResumeSnapshot(
            resumed.State,
            LiveSnapshot(state),
            trainingState);
    }

    public async Task<AndroidTrainingStateSnapshot> InvalidateActiveSessionSnapshotAsync(
        string sessionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await workflowService.InvalidateActiveSessionSnapshotAsync(
            new PreUiActiveSessionInvalidationRequest(sessionId, reason),
            cancellationToken).ConfigureAwait(false);

        preparedSessionStart = null;
        liveSessionController = null;
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
        var result = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(
                TrainingDate.From(today.Year, today.Month, today.Day)),
            cancellationToken).ConfigureAwait(false);

        if (result.IsProcessed)
        {
            preparedSessionStart = null;
            liveSessionController = null;
        }

        return new AndroidLiveSessionCompletionSnapshot(
            result,
            Capabilities,
            Configuration.LocalDatabasePath,
            today,
            await localDataService.LoadAsync(cancellationToken).ConfigureAwait(false));
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
                    "Finish or abandon the active live session before restoring local data.",
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

    private static TimeProviderRuntimeClock CreateLifecycleRestoreClock()
    {
        return new TimeProviderRuntimeClock(TimeProvider.System, LifecycleRestoreInitialInstant);
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
            ? "Prepared session is not startable."
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
            ? "Prepared session could not start."
            : string.Join(Environment.NewLine, details);
    }
}

public sealed record AndroidTrainingStateSnapshot(
    CurrentTrainingStateReadModel CurrentState,
    ApplicationIntegrationCapabilities Capabilities,
    string LocalDatabasePath,
    DateTime LoadedDate,
    LocalDataBackupReadModel LocalData)
{
    public CurrentTrainingPresentationReadModel Presentation { get; } =
        TrainingPresentationMapper.FromCurrentState(CurrentState);
}

public sealed record AndroidSessionStartSnapshot(
    PreUiTrainingWorkflowPreparationResult Preparation,
    ApplicationIntegrationCapabilities Capabilities,
    string LocalDatabasePath,
    DateTime PreparedDate,
    LocalDataBackupReadModel LocalData)
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
    LocalDataBackupReadModel LocalData)
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
