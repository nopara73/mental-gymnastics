namespace MentalGymnastics.Android;

using MentalGymnastics.Runtime;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    private readonly SemaphoreSlim liveSessionGate = new(1, 1);
    private CancellationTokenSource? loadCancellation;
    private MentalGymnasticsAndroidHost? appHost;
    private MgNavigationShell? shell;
    private System.Threading.Timer? liveRefreshTimer;
    private bool initialLoadCompleted;
    private bool activeSessionRestoreChecked;
    private bool isActivityResumed;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        appHost = MentalGymnasticsAndroidHost.Create(this);
        shell = new MgNavigationShell(this);
        shell.SessionStartRequested += HandleSessionStartRequested;
        shell.LiveSessionStartRequested += HandleLiveSessionStartRequested;
        shell.LiveSessionCommandRequested += HandleLiveSessionCommandRequested;
        shell.LocalBackupExportRequested += HandleLocalBackupExportRequested;
        shell.LocalDataValidateRequested += HandleLocalDataValidateRequested;
        shell.LocalBackupValidateRequested += HandleLocalBackupValidateRequested;
        shell.LocalBackupRestoreRequested += HandleLocalBackupRestoreRequested;
        shell.ActiveSessionInvalidateRequested += HandleActiveSessionInvalidateRequested;
        SetContentView(shell.Root);

        shell.ShowLoading(appHost);
        loadCancellation = new CancellationTokenSource();
        _ = LoadTrainingStateAsync(loadCancellation.Token);
    }

    protected override void OnDestroy()
    {
        if (shell is not null)
        {
            shell.SessionStartRequested -= HandleSessionStartRequested;
            shell.LiveSessionStartRequested -= HandleLiveSessionStartRequested;
            shell.LiveSessionCommandRequested -= HandleLiveSessionCommandRequested;
            shell.LocalBackupExportRequested -= HandleLocalBackupExportRequested;
            shell.LocalDataValidateRequested -= HandleLocalDataValidateRequested;
            shell.LocalBackupValidateRequested -= HandleLocalBackupValidateRequested;
            shell.LocalBackupRestoreRequested -= HandleLocalBackupRestoreRequested;
            shell.ActiveSessionInvalidateRequested -= HandleActiveSessionInvalidateRequested;
        }

        StopLiveRefresh();
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = null;
        liveSessionGate.Dispose();
        base.OnDestroy();
    }

    protected override void OnResume()
    {
        base.OnResume();
        isActivityResumed = true;
        if (initialLoadCompleted && loadCancellation is not null)
        {
            _ = ResumeOrRestoreActiveSessionAsync(loadCancellation.Token);
        }
    }

    protected override void OnPause()
    {
        isActivityResumed = false;
        SuspendActiveSessionForLifecycle();
        base.OnPause();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        SuspendActiveSessionForLifecycle();
        base.OnSaveInstanceState(outState);
    }

    private async Task LoadTrainingStateAsync(CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        try
        {
            var snapshot = await appHost.LoadTodayAsync(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RunOnUiThread(() => shell.Render(snapshot));
            initialLoadCompleted = true;
            if (isActivityResumed)
            {
                await ResumeOrRestoreActiveSessionAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            RunOnUiThread(() => shell?.ShowError(exception));
        }
    }

    private void HandleSessionStartRequested()
    {
        if (loadCancellation is null)
        {
            return;
        }

        _ = PrepareSessionStartAsync(loadCancellation.Token);
    }

    private async Task PrepareSessionStartAsync(CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        RunOnUiThread(() => shell?.ShowSessionStartLoading());

        try
        {
            var snapshot = await appHost.PrepareSessionStartAsync(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RunOnUiThread(() => shell?.RenderSessionStart(snapshot));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            RunOnUiThread(() => shell?.ShowSessionStartError(exception));
        }
    }

    private void HandleLiveSessionStartRequested()
    {
        if (loadCancellation is null)
        {
            return;
        }

        _ = StartLiveSessionAsync(loadCancellation.Token);
    }

    private async Task StartLiveSessionAsync(CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        await liveSessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StopLiveRefresh();
            RunOnUiThread(() => shell?.ShowLiveSessionLoading());

            var snapshot = await appHost.StartPreparedLiveSessionAsync(cancellationToken)
                .ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (snapshot.LiveSession.IsTerminal)
            {
                await CompleteTerminalLiveSessionAsync(snapshot, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                RunOnUiThread(() => shell?.RenderLiveSession(snapshot));
                StartLiveRefresh();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            RunOnUiThread(() => shell?.ShowLiveSessionError(exception));
        }
        finally
        {
            liveSessionGate.Release();
        }
    }

    private void HandleLiveSessionCommandRequested(
        RuntimeInputCommandKind command,
        string? targetId,
        string? value)
    {
        if (loadCancellation is null)
        {
            return;
        }

        _ = HandleLiveSessionCommandAsync(command, targetId, value, loadCancellation.Token);
    }

    private void HandleLocalBackupExportRequested()
    {
        if (loadCancellation is null)
        {
            return;
        }

        _ = RunLocalDataOperationAsync(
            "Exporting local backup",
            cancellationToken => appHost!.ExportLocalBackupAsync(cancellationToken),
            loadCancellation.Token);
    }

    private void HandleLocalDataValidateRequested()
    {
        if (loadCancellation is null)
        {
            return;
        }

        _ = RunLocalDataOperationAsync(
            "Validating local data",
            cancellationToken => appHost!.ValidateCurrentLocalDataAsync(cancellationToken),
            loadCancellation.Token);
    }

    private void HandleLocalBackupValidateRequested()
    {
        if (loadCancellation is null)
        {
            return;
        }

        _ = RunLocalDataOperationAsync(
            "Validating local backup",
            cancellationToken => appHost!.ValidateLatestLocalBackupAsync(cancellationToken),
            loadCancellation.Token);
    }

    private void HandleLocalBackupRestoreRequested()
    {
        if (loadCancellation is null)
        {
            return;
        }

        _ = RunLocalDataOperationAsync(
            "Restoring local backup",
            async cancellationToken =>
            {
                var result = await appHost!.RestoreLatestLocalBackupAsync(
                    confirmReplaceLocalData: true,
                    cancellationToken).ConfigureAwait(false);
                if (result.Operation.Status == MentalGymnastics.App.LocalDataBackupOperationStatus.Succeeded)
                {
                    StopLiveRefresh();
                    activeSessionRestoreChecked = false;
                }

                return result;
            },
            loadCancellation.Token);
    }

    private async Task RunLocalDataOperationAsync(
        string loadingLabel,
        Func<CancellationToken, Task<AndroidLocalDataOperationSnapshot>> operation,
        CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        RunOnUiThread(() => shell?.ShowLocalDataLoading(loadingLabel));

        try
        {
            var snapshot = await operation(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RunOnUiThread(() => shell?.RenderLocalDataOperation(snapshot));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            RunOnUiThread(() => shell?.ShowLocalDataError(exception));
        }
    }

    private void HandleActiveSessionInvalidateRequested(string sessionId)
    {
        if (loadCancellation is null)
        {
            return;
        }

        _ = InvalidateActiveSessionAsync(sessionId, loadCancellation.Token);
    }

    private async Task InvalidateActiveSessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        try
        {
            StopLiveRefresh();
            var snapshot = await appHost.InvalidateActiveSessionSnapshotAsync(
                sessionId,
                "Android cleared an unsafe active session snapshot after surfacing it.",
                cancellationToken).ConfigureAwait(false);
            activeSessionRestoreChecked = false;

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RunOnUiThread(() => shell?.Render(snapshot));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            RunOnUiThread(() => shell?.ShowError(exception));
        }
    }

    private async Task HandleLiveSessionCommandAsync(
        RuntimeInputCommandKind command,
        string? targetId,
        string? value,
        CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        await liveSessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await appHost.HandleLiveSessionCommandAsync(
                command,
                targetId,
                value,
                cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (snapshot.LiveSession.IsTerminal)
            {
                StopLiveRefresh();
                await CompleteTerminalLiveSessionAsync(snapshot, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                RunOnUiThread(() => shell?.RenderLiveSession(snapshot));
                if (liveRefreshTimer is null)
                {
                    StartLiveRefresh();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            RunOnUiThread(() => shell?.ShowLiveSessionError(exception));
        }
        finally
        {
            liveSessionGate.Release();
        }
    }

    private void StartLiveRefresh()
    {
        StopLiveRefresh();
        liveRefreshTimer = new System.Threading.Timer(
            _ => _ = RefreshLiveSessionAsync(),
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    private void StopLiveRefresh()
    {
        liveRefreshTimer?.Dispose();
        liveRefreshTimer = null;
    }

    private void SuspendActiveSessionForLifecycle()
    {
        StopLiveRefresh();
        if (appHost is null || !appHost.HasActiveLiveSession)
        {
            return;
        }

        if (!liveSessionGate.Wait(TimeSpan.FromSeconds(2)))
        {
            return;
        }

        try
        {
            appHost.SuspendActiveSessionForLifecycleAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            activeSessionRestoreChecked = false;
        }
        catch (Exception exception)
        {
            RunOnUiThread(() => shell?.ShowLiveSessionError(exception));
        }
        finally
        {
            liveSessionGate.Release();
        }
    }

    private async Task ResumeOrRestoreActiveSessionAsync(CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        await liveSessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (appHost.HasActiveLiveSession)
            {
                var snapshot = await appHost.RefreshLiveSessionAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (snapshot.LiveSession.IsTerminal)
                {
                    StopLiveRefresh();
                    await CompleteTerminalLiveSessionAsync(snapshot, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    RunOnUiThread(() => shell?.RenderLiveSession(snapshot));
                    StartLiveRefresh();
                }

                return;
            }

            if (activeSessionRestoreChecked)
            {
                return;
            }

            activeSessionRestoreChecked = true;
            var resumed = await appHost.TryResumeLatestActiveSessionAsync(cancellationToken)
                .ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested ||
                resumed.ActiveSession.Status == MentalGymnastics.App.PreUiActiveSessionResumeStatus.NotFound)
            {
                return;
            }

            if (resumed.LiveSession is { } liveSnapshot)
            {
                if (liveSnapshot.LiveSession.IsTerminal)
                {
                    StopLiveRefresh();
                    RunOnUiThread(() => shell?.RenderActiveSessionResume(resumed));
                }
                else
                {
                    RunOnUiThread(() => shell?.RenderActiveSessionResume(resumed));
                    StartLiveRefresh();
                }
            }
            else
            {
                StopLiveRefresh();
                RunOnUiThread(() => shell?.RenderActiveSessionResume(resumed));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            StopLiveRefresh();
            RunOnUiThread(() => shell?.ShowLiveSessionError(exception));
        }
        finally
        {
            liveSessionGate.Release();
        }
    }

    private async Task RefreshLiveSessionAsync()
    {
        if (appHost is null || shell is null || !liveSessionGate.Wait(0))
        {
            return;
        }

        try
        {
            var snapshot = await appHost.RefreshLiveSessionAsync().ConfigureAwait(false);
            if (snapshot.LiveSession.IsTerminal)
            {
                StopLiveRefresh();
                await CompleteTerminalLiveSessionAsync(snapshot, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            else
            {
                RunOnUiThread(() => shell?.RenderLiveSession(snapshot));
            }
        }
        catch (Exception exception)
        {
            StopLiveRefresh();
            RunOnUiThread(() => shell?.ShowLiveSessionError(exception));
        }
        finally
        {
            liveSessionGate.Release();
        }
    }

    private async Task CompleteTerminalLiveSessionAsync(
        AndroidLiveSessionSnapshot terminalSnapshot,
        CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        RunOnUiThread(() => shell?.ShowLiveSessionCompletionLoading(terminalSnapshot));

        var completion = await appHost.CompleteLiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        RunOnUiThread(() => shell?.RenderLiveSessionCompletion(completion));
    }
}
