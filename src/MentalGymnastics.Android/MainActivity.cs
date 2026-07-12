namespace MentalGymnastics.Android;

using MentalGymnastics.Core;
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
    private const string ScreenshotProtocolAudit = "protocol-audit";
    private const string ProtocolAuditDrillExtra = "mental_gymnastics.audit_drill";
    private const string ProtocolAuditLevelExtra = "mental_gymnastics.audit_level";
    private const string ProtocolAuditViewExtra = "mental_gymnastics.audit_view";
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
    private bool immersivePracticeRequested;
    private bool immersiveValidatedForCurrentResume;
    private bool suspendedForWindowFocusLoss;
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
        Window?.SetStatusBarColor(global::Android.Graphics.Color.Rgb(244, 248, 250));
        Window?.SetNavigationBarColor(global::Android.Graphics.Color.Rgb(244, 248, 250));
        if (Window?.DecorView is { } decorView)
        {
            decorView.SystemUiFlags = NormalSystemUiFlags();
        }
#pragma warning restore CA1422

#if DEBUG
        appHost = IsProtocolAuditRequested
            ? MentalGymnasticsAndroidHost.CreateProtocolAudit(this)
            : MentalGymnasticsAndroidHost.Create(this);
#else
        appHost = MentalGymnasticsAndroidHost.Create(this);
#endif
        shell = new MgNavigationShell(this);
        if (savedInstanceState is not null)
        {
            shell.RestoreLiveSessionDraft(savedInstanceState);
        }

        shell.SessionStartRequested += HandleSessionStartRequested;
        shell.LiveSessionStartRequested += HandleLiveSessionStartRequested;
        shell.PreparedSessionCancelRequested += HandlePreparedSessionCancelRequested;
        shell.LiveSessionStopRequested += HandleLiveSessionStopRequested;
        shell.StopTodayRequested += HandleStopTodayRequested;
        shell.GlobalReviewCompletionRequested += HandleGlobalReviewCompletionRequested;
        shell.LiveSessionCommandRequested += HandleLiveSessionCommandRequested;
        shell.ImmersivePracticeChanged += HandleImmersivePracticeChanged;
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
            shell.ImmersivePracticeChanged -= HandleImmersivePracticeChanged;
            shell.LocalBackupExportRequested -= HandleLocalBackupExportRequested;
            shell.LocalDataValidateRequested -= HandleLocalDataValidateRequested;
            shell.LocalBackupValidateRequested -= HandleLocalBackupValidateRequested;
            shell.LocalBackupRestoreRequested -= HandleLocalBackupRestoreRequested;
            shell.ActiveSessionInvalidateRequested -= HandleActiveSessionInvalidateRequested;
        }

        StopLiveRefresh();
        ApplyImmersiveSystemChrome(false);
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
            if (suspendedForWindowFocusLoss &&
                isActivityResumed &&
                loadCancellation is { } focusCancellation)
            {
                suspendedForWindowFocusLoss = false;
                _ = ResumeOrRestoreActiveSessionAsync(focusCancellation.Token);
                return;
            }

            if (isActivityResumed &&
                immersiveValidatedForCurrentResume &&
                immersivePracticeRequested)
            {
                ApplyImmersiveSystemChrome(true);
            }

            return;
        }

        if (isActivityResumed && immersivePracticeRequested)
        {
            suspendedForWindowFocusLoss = true;
            immersiveValidatedForCurrentResume = false;
            shell?.SuppressImmersivePracticeUntilFreshSnapshot();
            ApplyImmersiveSystemChrome(false);
            SuspendActiveSessionForLifecycle();
        }
    }

    protected override void OnPause()
    {
        isActivityResumed = false;
        suspendedForWindowFocusLoss = false;
        immersiveValidatedForCurrentResume = false;
        shell?.SuppressImmersivePracticeUntilFreshSnapshot();
        ApplyImmersiveSystemChrome(false);
#if DEBUG
        if (!IsScreenshotStepRequested)
#endif
        {
            SuspendActiveSessionForLifecycle();
        }

        base.OnPause();
    }

    private void HandleImmersivePracticeChanged(bool active)
    {
        immersivePracticeRequested = active;
        if (!isActivityResumed)
        {
            if (!active)
            {
                ApplyImmersiveSystemChrome(false);
            }

            return;
        }

        immersiveValidatedForCurrentResume = true;
        ApplyImmersiveSystemChrome(active);
    }

    private void ApplyImmersiveSystemChrome(bool immersive)
    {
        if (Window is not { } window)
        {
            return;
        }

        if (immersive)
        {
            window.AddFlags(global::Android.Views.WindowManagerFlags.KeepScreenOn);
        }
        else
        {
            window.ClearFlags(global::Android.Views.WindowManagerFlags.KeepScreenOn);
        }

#pragma warning disable CA1422
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            window.DecorView.SystemUiFlags = immersive
                ? global::Android.Views.SystemUiFlags.LayoutStable |
                  global::Android.Views.SystemUiFlags.LayoutFullscreen |
                  global::Android.Views.SystemUiFlags.LayoutHideNavigation
                : NormalSystemUiFlags();
            ApplyModernSystemChrome(window, immersive);
            return;
        }

        window.DecorView.SystemUiFlags = immersive
            ? global::Android.Views.SystemUiFlags.ImmersiveSticky |
              global::Android.Views.SystemUiFlags.Fullscreen |
              global::Android.Views.SystemUiFlags.HideNavigation |
              global::Android.Views.SystemUiFlags.LayoutStable |
              global::Android.Views.SystemUiFlags.LayoutFullscreen |
              global::Android.Views.SystemUiFlags.LayoutHideNavigation
            : NormalSystemUiFlags();
#pragma warning restore CA1422
    }

    private static global::Android.Views.SystemUiFlags NormalSystemUiFlags()
    {
        var flags = global::Android.Views.SystemUiFlags.LayoutStable |
                    global::Android.Views.SystemUiFlags.LightStatusBar;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            flags |= global::Android.Views.SystemUiFlags.LightNavigationBar;
        }

        return flags;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android30.0")]
    private static void ApplyModernSystemChrome(
        global::Android.Views.Window window,
        bool immersive)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            window.SetDecorFitsSystemWindows(!immersive);
        }
        if (window.InsetsController is not { } controller)
        {
            return;
        }

        controller.SystemBarsBehavior = (int)
            global::Android.Views.WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
        if (immersive)
        {
            controller.Hide(global::Android.Views.WindowInsets.Type.SystemBars());
        }
        else
        {
            controller.Show(global::Android.Views.WindowInsets.Type.SystemBars());
            var lightSystemBars =
                (int)global::Android.Views.WindowInsetsControllerAppearance.LightStatusBars |
                (int)global::Android.Views.WindowInsetsControllerAppearance.LightNavigationBars;
            controller.SetSystemBarsAppearance(
                lightSystemBars,
                lightSystemBars);
        }
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        shell?.SaveLiveSessionDraft(outState);
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
#if DEBUG
            global::Android.Util.Log.Error("MentalGymnastics", exception.ToString());
#endif
            RunOnUiThread(() => shell?.ShowError(exception));
        }
    }

#if DEBUG
    private bool IsProtocolAuditRequested =>
        string.Equals(
            Intent?.GetStringExtra(ScreenshotStepExtra),
            ScreenshotProtocolAudit,
            StringComparison.OrdinalIgnoreCase);

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

                case ScreenshotProtocolAudit:
                    var drillValue = Intent?.GetStringExtra(ProtocolAuditDrillExtra);
                    if (!Enum.TryParse<DrillId>(drillValue, ignoreCase: true, out var drill))
                    {
                        throw new InvalidOperationException($"Unknown protocol-audit drill: {drillValue}");
                    }

                    var levelValue = Intent?.GetStringExtra(ProtocolAuditLevelExtra);
                    GlobalLevelId? level = null;
                    if (!string.IsNullOrWhiteSpace(levelValue))
                    {
                        if (!Enum.TryParse<GlobalLevelId>(levelValue, ignoreCase: true, out var parsedLevel))
                        {
                            throw new InvalidOperationException($"Unknown protocol-audit level: {levelValue}");
                        }

                        level = parsedLevel;
                    }

                    var auditStart = await host.PrepareProtocolAuditSessionAsync(drill, level, cancellationToken)
                        .ConfigureAwait(false);
                    var auditView = Intent?.GetStringExtra(ProtocolAuditViewExtra);
                    if (string.Equals(auditView, "preflight", StringComparison.OrdinalIgnoreCase))
                    {
                        RunOnUiThread(() => navigationShell.RenderSessionStart(auditStart));
                        return true;
                    }

                    var auditLive = await host.StartPreparedProtocolAuditSessionAsync(
                            beginWork: !string.Equals(
                                auditView,
                                "get-ready",
                                StringComparison.OrdinalIgnoreCase),
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    if (string.Equals(auditView, "response", StringComparison.OrdinalIgnoreCase))
                    {
                        auditLive = await host.AdvanceProtocolAuditToResponseAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                    RunOnUiThread(() =>
                    {
                        navigationShell.RenderSessionStart(auditStart);
                        navigationShell.ShowLiveSessionLoading();
                        navigationShell.RenderLiveSession(auditLive);
                    });
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
                RunOnUiThread(() =>
                {
                    shell?.RenderLiveSession(snapshot);
                    ShowLiveCommandFeedback(snapshot);
                });
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

    private void ShowLiveCommandFeedback(AndroidLiveSessionSnapshot snapshot)
    {
        var outcome = snapshot.LiveSession.LastCommand;
        if (outcome is null)
        {
            return;
        }

        var message = outcome.IsAccepted
            ? AcceptedCommandFeedback(outcome.Command)
            : $"Not recorded. {RejectedCommandFeedback(outcome.InvalidReason, outcome.CueInvalidReason)}";
        if (message is null)
        {
            return;
        }

        global::Android.Widget.Toast.MakeText(
            this,
            message,
            outcome.IsAccepted
                ? global::Android.Widget.ToastLength.Short
                : global::Android.Widget.ToastLength.Long)?.Show();
    }

    private static string? AcceptedCommandFeedback(RuntimeInputCommandKind command)
    {
        return command switch
        {
            RuntimeInputCommandKind.MarkTargetChange => "Target change recorded.",
            RuntimeInputCommandKind.MarkError => "Error recorded.",
            _ => null,
        };
    }

    private static string RejectedCommandFeedback(
        RuntimeInputCommandInvalidReason? invalidReason,
        RuntimeCueResponseInvalidReason? cueInvalidReason)
    {
        if (cueInvalidReason.HasValue)
        {
            return cueInvalidReason.Value switch
            {
                RuntimeCueResponseInvalidReason.UnknownCue => "That cue is no longer active.",
                RuntimeCueResponseInvalidReason.CueNotPresented => "Wait for a cue before responding.",
                RuntimeCueResponseInvalidReason.CueAlreadyResponded => "That cue was already answered.",
                RuntimeCueResponseInvalidReason.SchedulerPaused => "Resume before responding to cues.",
                _ => "The cue could not accept that response.",
            };
        }

        return invalidReason switch
        {
            RuntimeInputCommandInvalidReason.CommandAfterTerminalSession =>
                "This exercise has already ended.",
            RuntimeInputCommandInvalidReason.SessionPaused =>
                "Resume the exercise before doing that.",
            RuntimeInputCommandInvalidReason.SessionNotRunning =>
                "The exercise is not running yet.",
            RuntimeInputCommandInvalidReason.NoActivePhase =>
                "There is no active step to receive that action.",
            RuntimeInputCommandInvalidReason.CommandNotAllowedInCurrentPhase =>
                "That action is not available in this step.",
            RuntimeInputCommandInvalidReason.NoCorrectableEvent =>
                "There is no recent response to correct.",
            RuntimeInputCommandInvalidReason.CorrectionWindowExpired =>
                "The correction window has closed.",
            RuntimeInputCommandInvalidReason.InvalidCommandPayload =>
                "That response is incomplete or invalid.",
            RuntimeInputCommandInvalidReason.CommandNotSupportedByDrill =>
                "This exercise does not use that action.",
            RuntimeInputCommandInvalidReason.NoOpenDrift =>
                "There is no wander report waiting for that action.",
            RuntimeInputCommandInvalidReason.OpenDriftRequiresReturn =>
                "The wander was already recorded.",
            RuntimeInputCommandInvalidReason.InvalidPhaseCompletion =>
                "This timed step has not finished yet.",
            RuntimeInputCommandInvalidReason.PauseNotAllowed =>
                "This step cannot be paused.",
            RuntimeInputCommandInvalidReason.IllegalLifecycleTransition =>
                "That action no longer matches the exercise state.",
            _ => "The exercise state changed; follow the current prompt.",
        };
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
