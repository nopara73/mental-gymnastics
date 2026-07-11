namespace MentalGymnastics.Android;

using MentalGymnastics.Runtime;

[Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/AppTheme.Starting")]
public class MainActivity : Activity
{
#if DEBUG
    private const string ScreenshotStepExtra = "mental_gymnastics.screenshot_step";
    private const string ScreenshotToday = "today";
    private const string ScreenshotBeforeStart = "before-start";
    private const string ScreenshotLiveGetReady = "live-get-ready";
    private const string ScreenshotLiveHold = "live-hold";
    private const string ScreenshotStoppedResult = "stopped-result";
    private const string ScreenshotReturnToday = "return-today";
#endif

    private readonly SemaphoreSlim liveSessionGate = new(1, 1);
    private readonly object lifecycleSuspensionSync = new();
    private readonly TaskCompletionSource<bool> initialWindowFocus = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? loadCancellation;
    private MentalGymnasticsAndroidHost? appHost;
    private MgNavigationShell? shell;
    private System.Threading.Timer? liveRefreshTimer;
    private bool initialLoadCompleted;
    private bool activeSessionRestoreChecked;
    private bool isActivityResumed;
    private Task lifecycleSuspensionTask = Task.CompletedTask;
    private Exception? lifecycleSuspensionException;
#if DEBUG
    private bool screenshotStepHandled;
#endif

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ActionBar?.Hide();
#pragma warning disable CA1422
        Window?.SetStatusBarColor(global::Android.Graphics.Color.Rgb(24, 27, 32));
        if (Window?.DecorView is { } decorView)
        {
            decorView.SystemUiFlags = global::Android.Views.SystemUiFlags.LightStatusBar;
        }
#pragma warning restore CA1422

        appHost = MentalGymnasticsAndroidHost.Create(this);
        shell = new MgNavigationShell(this);
        shell.SessionStartRequested += HandleSessionStartRequested;
        shell.LiveSessionStartRequested += HandleLiveSessionStartRequested;
        shell.PreparedSessionCancelRequested += HandlePreparedSessionCancelRequested;
        shell.LiveSessionStopRequested += HandleLiveSessionStopRequested;
        shell.StopTodayRequested += HandleStopTodayRequested;
        shell.GlobalReviewCompletionRequested += HandleGlobalReviewCompletionRequested;
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
            shell.PreparedSessionCancelRequested -= HandlePreparedSessionCancelRequested;
            shell.LiveSessionStopRequested -= HandleLiveSessionStopRequested;
            shell.StopTodayRequested -= HandleStopTodayRequested;
            shell.GlobalReviewCompletionRequested -= HandleGlobalReviewCompletionRequested;
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
        base.OnDestroy();
    }

    public override void OnBackPressed()
    {
        if (shell?.HandleBack() == true)
        {
            return;
        }

        FinishAfterTransition();
    }

    protected override void OnResume()
    {
        base.OnResume();
        isActivityResumed = true;
#if DEBUG
        if (IsScreenshotStepRequested)
        {
            return;
        }
#endif
        if (initialLoadCompleted && loadCancellation is not null)
        {
            _ = ResumeOrRestoreActiveSessionAsync(loadCancellation.Token);
        }
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
        {
            initialWindowFocus.TrySetResult(true);
        }
    }

    protected override void OnPause()
    {
        isActivityResumed = false;
#if DEBUG
        if (!IsScreenshotStepRequested)
#endif
        {
            SuspendActiveSessionForLifecycle();
        }

        base.OnPause();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
#if DEBUG
        if (!IsScreenshotStepRequested)
#endif
        {
            SuspendActiveSessionForLifecycle();
        }

        base.OnSaveInstanceState(outState);
    }

    private async Task LoadTrainingStateAsync(CancellationToken cancellationToken)
    {
        var host = appHost;
        var navigationShell = shell;
        if (host is null || navigationShell is null)
        {
            return;
        }

        try
        {
            var snapshot = await host.LoadTodayAsync(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await initialWindowFocus.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(16, cancellationToken).ConfigureAwait(false);
            RunOnUiThread(() => navigationShell.Render(snapshot));
            initialLoadCompleted = true;
#if DEBUG
            if (await TryRenderScreenshotStepAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }
#endif
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

#if DEBUG
    private bool IsScreenshotStepRequested =>
        !string.IsNullOrWhiteSpace(Intent?.GetStringExtra(ScreenshotStepExtra));

    private async Task<bool> TryRenderScreenshotStepAsync(CancellationToken cancellationToken)
    {
        var step = Intent?.GetStringExtra(ScreenshotStepExtra);
        if (string.IsNullOrWhiteSpace(step) || screenshotStepHandled || appHost is null || shell is null)
        {
            return false;
        }

        var host = appHost;
        var navigationShell = shell;
        screenshotStepHandled = true;
        activeSessionRestoreChecked = true;
        StopLiveRefresh();

        await liveSessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            switch (step.Trim().ToLowerInvariant())
            {
                case ScreenshotToday:
                    return true;

                case ScreenshotBeforeStart:
                    var preflight = await host.PrepareSessionStartAsync(cancellationToken)
                        .ConfigureAwait(false);
                    RunOnUiThread(() => navigationShell.RenderSessionStart(preflight));
                    return true;

                case ScreenshotLiveGetReady:
                    _ = await host.PrepareSessionStartAsync(cancellationToken).ConfigureAwait(false);
                    var liveGetReady = await host.StartPreparedLiveSessionAsync(
                            cancellationToken,
                            saveActiveSnapshot: false)
                        .ConfigureAwait(false);
                    RunOnUiThread(() => navigationShell.RenderLiveSession(liveGetReady));
                    return true;

                case ScreenshotLiveHold:
                    var liveHold = await StartTargetHoldActiveWorkAsync(host, cancellationToken)
                        .ConfigureAwait(false);
                    RunOnUiThread(() => navigationShell.RenderLiveSession(liveHold));
                    return true;

                case ScreenshotStoppedResult:
                    var stopped = await StopTargetHoldForScreenshotAsync(host, cancellationToken)
                        .ConfigureAwait(false);
                    RunOnUiThread(() => navigationShell.RenderLiveSessionCompletion(stopped));
                    return true;

                case ScreenshotReturnToday:
                    _ = await StopTargetHoldForScreenshotAsync(host, cancellationToken).ConfigureAwait(false);
                    var today = await host.LoadTodayAsync(cancellationToken).ConfigureAwait(false);
                    RunOnUiThread(() => navigationShell.Render(today));
                    return true;

                default:
                    RunOnUiThread(() => navigationShell.ShowError(
                        new InvalidOperationException($"Unknown screenshot step: {step}")));
                    return true;
            }
        }
        finally
        {
            liveSessionGate.Release();
        }
    }

    private static async Task<AndroidLiveSessionSnapshot> StartTargetHoldActiveWorkAsync(
        MentalGymnasticsAndroidHost host,
        CancellationToken cancellationToken)
    {
        _ = await host.PrepareSessionStartAsync(cancellationToken).ConfigureAwait(false);
        var live = await host.StartPreparedLiveSessionAsync(
                cancellationToken,
                saveActiveSnapshot: false)
            .ConfigureAwait(false);
        if (live.LiveSession.CurrentPhaseKind != RuntimeSessionPhaseKind.InstructionPrep)
        {
            return live;
        }

        var canBegin = live.LiveSession.Commands.Any(command =>
            command.Command == RuntimeInputCommandKind.FinishPhase &&
            command.IsAvailable);
        return canBegin
            ? await host.HandleLiveSessionCommandAsync(
                RuntimeInputCommandKind.FinishPhase,
                cancellationToken: cancellationToken).ConfigureAwait(false)
            : live;
    }

    private static async Task<AndroidLiveSessionCompletionSnapshot> StopTargetHoldForScreenshotAsync(
        MentalGymnasticsAndroidHost host,
        CancellationToken cancellationToken)
    {
        _ = await StartTargetHoldActiveWorkAsync(host, cancellationToken).ConfigureAwait(false);
        var terminal = await host.HandleLiveSessionCommandAsync(
            RuntimeInputCommandKind.Abandon,
            value: "Screenshot workflow stopped early.",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!terminal.LiveSession.IsTerminal)
        {
            throw new InvalidOperationException("Screenshot workflow did not reach a terminal stopped state.");
        }

        return await host.CompleteLiveSessionAsync(cancellationToken).ConfigureAwait(false);
    }
#endif

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

    private void HandleLiveSessionStartRequested(string? mainFailureModeAvoided)
    {
        if (loadCancellation is null)
        {
            return;
        }

        _ = StartLiveSessionAsync(mainFailureModeAvoided, loadCancellation.Token);
    }

    private void HandlePreparedSessionCancelRequested()
    {
        if (loadCancellation is not null)
        {
            _ = CancelPreparedSessionAsync(loadCancellation.Token);
        }
    }

    private async Task CancelPreparedSessionAsync(CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        try
        {
            var snapshot = await appHost.CancelPreparedSessionStartAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
            {
                RunOnUiThread(() => shell?.Render(snapshot));
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

    private void HandleLiveSessionStopRequested()
    {
        if (loadCancellation is not null)
        {
            _ = HandleLiveSessionCommandAsync(
                RuntimeInputCommandKind.Abandon,
                targetId: null,
                value: "User stopped today's workout from the live screen.",
                cancellationToken: loadCancellation.Token);
        }
    }

    private void HandleStopTodayRequested()
    {
        if (loadCancellation is not null)
        {
            _ = StopTodayAsync(loadCancellation.Token);
        }
    }

    private void HandleGlobalReviewCompletionRequested()
    {
        if (loadCancellation is not null)
        {
            _ = CompleteGlobalReviewAsync(loadCancellation.Token);
        }
    }

    private async Task CompleteGlobalReviewAsync(CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        RunOnUiThread(() => shell?.ShowGlobalReviewCompletionLoading());
        try
        {
            var snapshot = await appHost.CompleteDueGlobalReviewAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
            {
                RunOnUiThread(() => shell?.Render(snapshot));
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

    private async Task StopTodayAsync(CancellationToken cancellationToken)
    {
        if (appHost is null || shell is null)
        {
            return;
        }

        try
        {
            var snapshot = await appHost.StopTodayAsync(cancellationToken).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
            {
                RunOnUiThread(() => shell?.Render(snapshot));
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

    private async Task StartLiveSessionAsync(
        string? mainFailureModeAvoided,
        CancellationToken cancellationToken)
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

            var snapshot = await appHost.StartPreparedLiveSessionAsync(
                cancellationToken,
                mainFailureModeAvoided: mainFailureModeAvoided)
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

        lock (lifecycleSuspensionSync)
        {
            if (!lifecycleSuspensionTask.IsCompleted)
            {
                return;
            }

            lifecycleSuspensionTask = SuspendActiveSessionInBackgroundAsync();
        }
    }

    private async Task SuspendActiveSessionInBackgroundAsync()
    {
        await liveSessionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (appHost is not null && appHost.HasActiveLiveSession)
            {
                await appHost.SuspendActiveSessionForLifecycleAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                activeSessionRestoreChecked = false;
            }
        }
        catch (Exception exception)
        {
            lifecycleSuspensionException = exception;
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

        Task pendingSuspension;
        lock (lifecycleSuspensionSync)
        {
            pendingSuspension = lifecycleSuspensionTask;
        }

        await pendingSuspension.ConfigureAwait(false);
        if (lifecycleSuspensionException is { } suspensionException)
        {
            lifecycleSuspensionException = null;
            RunOnUiThread(() => shell?.ShowLiveSessionError(suspensionException));
            return;
        }

        await liveSessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (appHost.HasActiveLiveSession)
            {
                var snapshot = await appHost.ResumeActiveSessionForLifecycleAsync(cancellationToken)
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
