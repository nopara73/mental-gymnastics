using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Android;

internal sealed class MgNavigationShell
{
    private readonly Context context;
    private readonly MgTopBar topBar;
    private readonly ScrollView scrollView;
    private readonly LinearLayout content;
    private readonly MgBottomNavigationBar navigation;

    private AndroidTrainingStateSnapshot? snapshot;
    private AndroidSessionStartSnapshot? sessionStartSnapshot;
    private AndroidLiveSessionSnapshot? liveSessionSnapshot;
    private AndroidLiveSessionCompletionSnapshot? liveCompletionSnapshot;
    private LiveTrainingScreenView? liveTrainingView;
    private AndroidActiveSessionResumeSnapshot? activeSessionResumeSnapshot;
    private LocalDataBackupOperationResult? localDataOperation;
    private string liveSessionInput = string.Empty;
    private string? selectedMainFailureMode;
    private bool restoreConfirmationArmed;
    private bool stopTodayConfirmationArmed;
    private DateTimeOffset? liveBackConfirmationAt;
    private Screen currentScreen = Screen.Today;
    private Screen utilityReturnScreen = Screen.Today;
    private BranchCode selectedBranch = BranchCode.FH;
    private GlobalLevelId selectedLevel = GlobalLevelId.L1;
    private RecordFilter recordFilter = RecordFilter.All;
    private string? selectedSessionId;

    public MgNavigationShell(Context context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));

        Root = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        Root.SetBackgroundColor(MgColors.Canvas);
        Root.SetPadding(0, StatusBarHeightPx(), 0, 0);

        topBar = new MgTopBar(context);
        topBar.BackRequested += () => HandleBack();
        topBar.DataRequested += () =>
        {
            if (currentScreen != Screen.LocalData)
            {
                utilityReturnScreen = currentScreen;
            }

            currentScreen = Screen.LocalData;
            restoreConfirmationArmed = false;
            RenderCurrentScreen();
        };
        Root.AddView(topBar, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            Dp(56)));

        scrollView = new ScrollView(context);
        content = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        content.SetPadding(
            Dp(MgSpacing.Lg),
            Dp(MgSpacing.Md),
            Dp(MgSpacing.Lg),
            Dp(MgSpacing.Lg));
        var contentHost = new FrameLayout(context);
        var screenWidth = context.Resources?.DisplayMetrics?.WidthPixels ?? Dp(640);
        var contentWidth = Math.Min(screenWidth, Dp(640));
        contentHost.AddView(content, new FrameLayout.LayoutParams(
            contentWidth,
            ViewGroup.LayoutParams.WrapContent,
            GravityFlags.Top | GravityFlags.CenterHorizontal));
        scrollView.AddView(contentHost, new ScrollView.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));
        Root.AddView(scrollView, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            0,
            1));

        navigation = new MgBottomNavigationBar(context);
        navigation.DestinationSelected += NavigateToDestination;
        Root.AddView(navigation, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            Dp(64) + NavigationBarHeightPx()));
        navigation.SetPadding(0, Dp(2), 0, NavigationBarHeightPx());
    }

    private enum Screen
    {
        Today,
        Map,
        Evidence,
        RecordDetail,
        Review,
        BranchDetail,
        LocalData,
        Preflight,
        Live,
        Result,
    }

    private readonly record struct TodayFact(string Text, Color Color, bool Filled);

    private readonly record struct VisualSignal(string Marker, string Label, string Value, Color Color, bool Filled);

    private readonly record struct InspectionSignal(string Marker, string Label, string Detail, Color Color, bool Filled);

    private readonly record struct BranchReviewSignal(BranchCode Branch, string Level, string State, Color Color, bool Filled);

    private enum RecordFilter
    {
        All,
        Clean,
        Missed,
    }

    public LinearLayout Root { get; }

    public event Action? SessionStartRequested;

    public event Action<string?>? LiveSessionStartRequested;

    public event Action? PreparedSessionCancelRequested;

    public event Action? LiveSessionStopRequested;

    public event Action? StopTodayRequested;

    public event Action? GlobalReviewCompletionRequested;

    public event Action<RuntimeInputCommandKind, string?, string?>? LiveSessionCommandRequested;

    public event Action<string>? ActiveSessionInvalidateRequested;

    public event Action? LocalBackupExportRequested;

    public event Action? LocalDataValidateRequested;

    public event Action? LocalBackupValidateRequested;

    public event Action? LocalBackupRestoreRequested;

    public bool HandleBack()
    {
        if (currentScreen == Screen.Preflight)
        {
            PreparedSessionCancelRequested?.Invoke();
            return true;
        }

        if (currentScreen == Screen.Live)
        {
            var now = DateTimeOffset.UtcNow;
            if (!liveBackConfirmationAt.HasValue ||
                now - liveBackConfirmationAt.Value > TimeSpan.FromSeconds(3))
            {
                liveBackConfirmationAt = now;
                Toast.MakeText(
                    context,
                    "Press back again to stop today's workout.",
                    ToastLength.Short)?.Show();
                return true;
            }

            liveBackConfirmationAt = null;
            LiveSessionStopRequested?.Invoke();
            return true;
        }

        var destination = currentScreen switch
        {
            Screen.LocalData => utilityReturnScreen,
            Screen.BranchDetail => Screen.Map,
            Screen.RecordDetail => Screen.Evidence,
            Screen.Result => Screen.Today,
            Screen.Map or Screen.Evidence or Screen.Review => Screen.Today,
            _ => currentScreen,
        };

        if (destination == currentScreen)
        {
            return false;
        }

        currentScreen = destination;
        restoreConfirmationArmed = false;
        stopTodayConfirmationArmed = false;
        RenderCurrentScreen();
        return true;
    }

    public void ShowLoading(MentalGymnasticsAndroidHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        snapshot = null;
        currentScreen = Screen.Today;
        RenderFrame();
        AddPanel("Loading", "Reading local training state.", "Today will open on prescribed work.");
        ResetScrollPosition();
    }

    public void Render(AndroidTrainingStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        this.snapshot = snapshot;
        sessionStartSnapshot = null;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        localDataOperation = null;
        liveSessionInput = string.Empty;
        selectedMainFailureMode = null;
        restoreConfirmationArmed = false;
        currentScreen = Screen.Today;
        RenderCurrentScreen();
    }

    public void RenderActiveSessionResume(AndroidActiveSessionResumeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        this.snapshot = snapshot.TrainingState;
        activeSessionResumeSnapshot = snapshot.ActiveSession.Status == PreUiActiveSessionResumeStatus.NotFound
            ? null
            : snapshot;
        sessionStartSnapshot = null;
        liveCompletionSnapshot = null;
        localDataOperation = null;
        liveSessionInput = string.Empty;

        if (snapshot.LiveSession is not null)
        {
            liveSessionSnapshot = snapshot.LiveSession;
            currentScreen = Screen.Live;
        }
        else
        {
            liveSessionSnapshot = null;
            currentScreen = Screen.Today;
        }

        RenderCurrentScreen();
    }

    public void ShowError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        LogUiException(exception);

        currentScreen = Screen.Today;
        RenderFrame();
        AddPanel("Unable to load", FriendlyExceptionDetail(exception), "Your local record was not changed.");
        ResetScrollPosition();
    }

    public void ShowSessionStartLoading()
    {
        currentScreen = Screen.Preflight;
        sessionStartSnapshot = null;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        RenderFrame();
        var exercise = snapshot?.Presentation.PrimaryPrescribedWork?.Exercise.ExerciseName ?? "exercise";
        AddPanel($"Getting {exercise} ready", "Setting up the exercise.", "Nothing counts until the exercise starts.");
        ResetScrollPosition();
    }

    public void ShowGlobalReviewCompletionLoading()
    {
        currentScreen = Screen.Review;
        RenderFrame();
        AddPanel("Recording review", "Applying the current evidence.", "The review result will close today's prescription.");
        ResetScrollPosition();
    }

    public void RenderSessionStart(AndroidSessionStartSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        sessionStartSnapshot = snapshot;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        liveSessionInput = string.Empty;
        this.snapshot = new AndroidTrainingStateSnapshot(
            snapshot.Preparation.CurrentState,
            snapshot.Capabilities,
            snapshot.LocalDatabasePath,
            snapshot.PreparedDate,
            snapshot.LocalData,
            snapshot.DailyTraining);
        currentScreen = Screen.Preflight;
        RenderCurrentScreen();
    }

    public void ShowSessionStartError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        LogUiException(exception);

        currentScreen = Screen.Preflight;
        sessionStartSnapshot = null;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        RenderFrame();
        AddPanel("Setup blocked", FriendlyExceptionDetail(exception), "Nothing was counted.");
        AddPrimaryButton("Today", enabled: snapshot is not null, () =>
        {
            currentScreen = Screen.Today;
            RenderCurrentScreen();
        });
        ResetScrollPosition();
    }

    public void ShowLiveSessionLoading()
    {
        currentScreen = Screen.Live;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        liveSessionInput = string.Empty;
        RenderFrame();
        var exercise = sessionStartSnapshot?.Presentation.Work?.Exercise.ExerciseName ??
            snapshot?.Presentation.PrimaryPrescribedWork?.Exercise.ExerciseName ??
            "exercise";
        AddPanel("Starting exercise", $"Opening {exercise}.", "Nothing counts until you finish or stop.");
        ResetScrollPosition();
    }

    public void RenderLiveSession(AndroidLiveSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var phaseChanged = liveSessionSnapshot is { } previous &&
            string.Equals(previous.LiveSession.SessionId, snapshot.LiveSession.SessionId, StringComparison.Ordinal) &&
            !string.Equals(previous.LiveSession.CurrentPhaseId, snapshot.LiveSession.CurrentPhaseId, StringComparison.Ordinal);
        liveSessionSnapshot = snapshot;
        liveCompletionSnapshot = null;
        liveTrainingView?.Update(snapshot);
        if (currentScreen != Screen.Live)
        {
            navigation.Update(
                DestinationFor(currentScreen),
                trainSessionActive: !snapshot.LiveSession.IsTerminal);
            return;
        }

        if (liveTrainingView?.Parent is not null)
        {
            if (phaseChanged)
            {
                ResetScrollPosition();
            }

            return;
        }

        RenderCurrentScreen(resetScroll: phaseChanged);
    }

    public void ShowLiveSessionCompletionLoading(AndroidLiveSessionSnapshot terminalSnapshot)
    {
        ArgumentNullException.ThrowIfNull(terminalSnapshot);

        liveSessionSnapshot = terminalSnapshot;
        liveCompletionSnapshot = null;
        currentScreen = Screen.Result;
        RenderFrame();
        AddPanel("Recording result", "Saving what happened.", "Progress changes only when the program state changes.");
        ResetScrollPosition();
    }

    public void RenderLiveSessionCompletion(AndroidLiveSessionCompletionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        liveCompletionSnapshot = snapshot;
        liveSessionSnapshot = null;
        sessionStartSnapshot = null;
        activeSessionResumeSnapshot = null;
        liveSessionInput = string.Empty;
        currentScreen = Screen.Result;

        if (snapshot.Completion.WorkflowResult is { } workflowResult)
        {
            this.snapshot = new AndroidTrainingStateSnapshot(
                workflowResult.RefreshedState,
                snapshot.Capabilities,
                snapshot.LocalDatabasePath,
                snapshot.LoadedDate,
                snapshot.LocalData,
                snapshot.DailyTraining);
        }

        RenderCurrentScreen();
    }

    public void ShowLocalDataLoading(string operation)
    {
        currentScreen = Screen.LocalData;
        RenderFrame();
        AddPanel(operation, "Using local backup and integrity checks.", "No sync or remote storage.");
        ResetScrollPosition();
    }

    public void RenderLocalDataOperation(AndroidLocalDataOperationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        this.snapshot = snapshot.TrainingState;
        localDataOperation = snapshot.Operation;
        restoreConfirmationArmed = false;
        currentScreen = Screen.LocalData;
        sessionStartSnapshot = null;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        RenderCurrentScreen();
    }

    public void ShowLocalDataError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        LogUiException(exception);

        currentScreen = Screen.LocalData;
        RenderFrame();
        AddPanel("Local data failed", FriendlyExceptionDetail(exception), "Your existing record was not changed.");
        ResetScrollPosition();
    }

    public void ShowLiveSessionError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        LogUiException(exception);

        currentScreen = Screen.Live;
        RenderFrame();
        AddPanel("Could not save result", FriendlyExceptionDetail(exception), "No progress change was applied.");
        AddPrimaryButton("Open local data", enabled: snapshot is not null, () =>
        {
            currentScreen = Screen.LocalData;
            RenderCurrentScreen();
        });
        ResetScrollPosition();
    }

    private static string FriendlyExceptionDetail(Exception exception)
    {
        if (exception.Message.Contains("integrity", StringComparison.OrdinalIgnoreCase))
        {
            return "Your local record did not pass its integrity check. Inspect or restore it in Local data.";
        }

        return exception is IOException
            ? "The local record could not be read or written."
            : "The action could not be completed.";
    }

    private static void LogUiException(Exception exception)
    {
        global::Android.Util.Log.Error("MentalGymnastics", exception.ToString());
    }

    private void RenderCurrentScreen(bool resetScroll = true)
    {
        RenderFrame();

        switch (currentScreen)
        {
            case Screen.Today:
                if (snapshot is null)
                {
                    AddPanel("Loading", "Training state is not available yet.", "Today will show the next step.");
                }
                else
                {
                    AddToday(snapshot);
                }

                break;
            case Screen.Map:
                AddMap(snapshot);
                break;
            case Screen.Evidence:
                AddEvidence(snapshot);
                break;
            case Screen.RecordDetail:
                AddRecordDetail(snapshot);
                break;
            case Screen.Review:
                AddReview(snapshot);
                break;
            case Screen.BranchDetail:
                AddBranchDetail(snapshot);
                break;
            case Screen.LocalData:
                AddLocalData(snapshot);
                break;
            case Screen.Preflight:
                AddPreflight(sessionStartSnapshot);
                break;
            case Screen.Live:
                AddLive(liveSessionSnapshot);
                break;
            case Screen.Result:
                AddResult(liveCompletionSnapshot);
                break;
        }

        if (resetScroll)
        {
            ResetScrollPosition();
        }
    }

    private void RenderFrame()
    {
        content.RemoveAllViews();
        var focused = UsesFocusedTrainingFrame();
        var isWorkflow = currentScreen is Screen.Preflight or Screen.Live or Screen.Result;
        var horizontalPadding = focused ? MgSpacing.Md : MgSpacing.Lg;
        var verticalPadding = focused ? MgSpacing.Sm : MgSpacing.Md;
        content.SetPadding(
            Dp(horizontalPadding),
            Dp(verticalPadding),
            Dp(horizontalPadding),
            Dp(focused ? MgSpacing.Md : MgSpacing.Lg));
        topBar.Update(
            CurrentScreenTitle(),
            CanNavigateBack(currentScreen),
            dataSelected: currentScreen == Screen.LocalData,
            showDataAction: !isWorkflow);
        navigation.Visibility = isWorkflow ? ViewStates.Gone : ViewStates.Visible;
        navigation.Update(
            DestinationFor(currentScreen),
            trainSessionActive: liveSessionSnapshot is { LiveSession.IsTerminal: false });
    }

    private bool UsesFocusedTrainingFrame()
    {
        if (currentScreen is Screen.Preflight or Screen.Live or Screen.Result)
        {
            return true;
        }

        return currentScreen == Screen.Today && IsStartableTodayExercise(snapshot?.Presentation);
    }

    private static bool IsStartableTodayExercise(CurrentTrainingPresentationReadModel? presentation)
    {
        return presentation?.PrimaryPrescribedWork is not null &&
               presentation.PrimaryActionEnabled &&
               presentation.PrimaryAction is TrainingPresentationPrimaryActionKind.StartPrescribedWork
                   or TrainingPresentationPrimaryActionKind.StartLiveSession
                   or TrainingPresentationPrimaryActionKind.ReturnToNextPrescribedAction;
    }

    private static bool ShouldShowLifecycleStatus(RuntimeSessionLifecycleStatus status)
    {
        return status is not RuntimeSessionLifecycleStatus.Running;
    }

    private void NavigateToDestination(MgNavigationDestination destination)
    {
        currentScreen = destination switch
        {
            MgNavigationDestination.Train => TrainingScreen(),
            MgNavigationDestination.Map => Screen.Map,
            MgNavigationDestination.Record => Screen.Evidence,
            MgNavigationDestination.Review => Screen.Review,
            _ => Screen.Today,
        };
        restoreConfirmationArmed = false;
        RenderCurrentScreen();
    }

    private Screen TrainingScreen()
    {
        if (liveSessionSnapshot is not null)
        {
            return Screen.Live;
        }

        if (liveCompletionSnapshot is not null)
        {
            return Screen.Result;
        }

        if (sessionStartSnapshot is not null)
        {
            return Screen.Preflight;
        }

        return Screen.Today;
    }

    private static MgNavigationDestination DestinationFor(Screen screen)
    {
        return screen switch
        {
            Screen.Map => MgNavigationDestination.Map,
            Screen.Evidence => MgNavigationDestination.Record,
            Screen.RecordDetail => MgNavigationDestination.Record,
            Screen.Review => MgNavigationDestination.Review,
            Screen.BranchDetail => MgNavigationDestination.Map,
            _ => MgNavigationDestination.Train,
        };
    }

    private static bool CanNavigateBack(Screen screen)
    {
        return screen is
            Screen.LocalData or
            Screen.Preflight or
            Screen.Live or
            Screen.Result or
            Screen.BranchDetail or
            Screen.RecordDetail;
    }

    private string CurrentScreenTitle()
    {
        return currentScreen switch
        {
            Screen.Live => liveSessionSnapshot?.Presentation.Work.Exercise.ExerciseName ?? "Training",
            Screen.Result => "Result",
            Screen.BranchDetail => ProgramCatalog.Branches.Single(branch => branch.Code == selectedBranch).Name,
            Screen.RecordDetail => "Session",
            _ => TitleFor(currentScreen),
        };
    }

    private void AddToday(AndroidTrainingStateSnapshot state)
    {
        if (activeSessionResumeSnapshot is { ActiveSession.Status: not PreUiActiveSessionResumeStatus.NotFound } resume &&
            resume.LiveSession is null)
        {
            AddActiveSessionProblem(resume.ActiveSession);
            return;
        }

        var presentation = state.Presentation;
        var panel = FocusedPanel();
        if (presentation.PrimaryPrescribedWork is not { } work)
        {
            AddCompactScreenHeading(
                panel,
                TodayCommandTitle(presentation),
                DailyTerminalSubtitle(presentation));
            AddDailyDoseProgress(panel, presentation);
            if (presentation.UrgentBlocker is { } blocker)
            {
                AddWarningRow(panel, "Blocked", blocker.Detail, MgColors.Blocked);
            }

            if (presentation.DailyStatus is null ||
                presentation.DailyStatus is not DailyTrainingWorkflowStatus.Done and not
                    DailyTrainingWorkflowStatus.Stopped and not DailyTrainingWorkflowStatus.OffDay)
            {
                AddTodayPrimaryButton(panel, presentation);
            }
            content.AddView(panel, MatchWrapWithBottom());
            return;
        }

        if (state.DailyTraining?.CurrentBlock?.Record.Role == LocalDailyTrainingBlockRole.Review)
        {
            AddDueGlobalReviewToday(panel, state);
            content.AddView(panel, MatchWrapWithBottom());
            return;
        }

        var identity = WorkRoleLabel(work) == "Practice"
            ? work.Exercise.BranchLevelLabel
            : $"{work.Exercise.BranchLevelLabel} · {WorkRoleLabel(work)}";
        AddCompactScreenHeading(panel, work.Exercise.ExerciseName, identity);
        AddDailyDoseProgress(panel, presentation);

        var exerciseBand = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Background = MgTheme.TintedSurface(
                context,
                MgColors.TrainingTint,
                MgColors.HairlineSoft,
                cornerRadius: 8),
        };
        exerciseBand.SetGravity(GravityFlags.CenterVertical);
        exerciseBand.SetPadding(
            Dp(MgSpacing.Lg),
            Dp(MgSpacing.Lg),
            Dp(MgSpacing.Lg),
            Dp(MgSpacing.Lg));

        var icon = new MgGlyphView(
            context,
            GlyphForDrill(work.Drill),
            MgColors.TrainingDark,
            filled: false,
            showContainer: false)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        exerciseBand.AddView(icon, new LinearLayout.LayoutParams(Dp(64), Dp(64)));

        var instruction = new TextView(context)
        {
            Text = work.Drill == DrillId.FH1TargetHold
                ? "Hold one target. Mark every wander, then return."
                : SessionDoseInstruction(work),
            Gravity = GravityFlags.CenterVertical,
        };
        MgTypography.ApplyBody(instruction);
        var instructionLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        instructionLayout.SetMargins(Dp(MgSpacing.Lg), 0, 0, 0);
        exerciseBand.AddView(instruction, instructionLayout);
        panel.AddView(exerciseBand, MatchWrapWithTop(MgSpacing.Lg));

        AddPrimaryButton(
            panel,
            TodaySetupLabel(work, presentation),
            presentation.PrimaryActionEnabled,
            () => SessionStartRequested?.Invoke());

        if (work.Drill == DrillId.FH1TargetHold)
        {
            panel.AddView(FocusHoldCriteriaStrip(work.LoadVariables), MatchWrapWithTop(MgSpacing.Md));
        }
        else if (!string.IsNullOrWhiteSpace(work.Standard))
        {
            AddFocusedBlock(
                panel,
                StandardBlockLabel(work),
                StandardDisplay(work));
        }

        if (presentation.DailyTotalBlockCount > 0)
        {
            AddUtilityButton(
                panel,
                stopTodayConfirmationArmed ? "Confirm stop today" : "Stop today",
                () =>
                {
                    if (!stopTodayConfirmationArmed)
                    {
                        stopTodayConfirmationArmed = true;
                        RenderCurrentScreen();
                        return;
                    }

                    StopTodayRequested?.Invoke();
                },
                destructive: stopTodayConfirmationArmed);
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddDailyDoseProgress(
        LinearLayout panel,
        CurrentTrainingPresentationReadModel presentation)
    {
        if (!presentation.DailyStatus.HasValue || presentation.DailyTotalBlockCount == 0)
        {
            return;
        }

        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);

        var label = new TextView(context)
        {
            Text = $"{presentation.DailyCompletedBlockCount} of {presentation.DailyTotalBlockCount}",
        };
        MgTypography.ApplyLabel(label);
        label.SetTextColor(MgColors.InkMuted);
        label.SetMinWidth(Dp(48));
        label.SetSingleLine(true);
        row.AddView(label, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent));

        var bars = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        var barsLayout = new LinearLayout.LayoutParams(0, Dp(8), 1);
        barsLayout.SetMargins(Dp(MgSpacing.Sm), 0, 0, 0);
        row.AddView(bars, barsLayout);

        for (var index = 0; index < presentation.DailyTotalBlockCount; index++)
        {
            var completed = index < presentation.DailyCompletedBlockCount;
            var current = index == presentation.DailyCompletedBlockCount &&
                presentation.DailyStatus is not DailyTrainingWorkflowStatus.Done and not
                    DailyTrainingWorkflowStatus.Stopped;
            var color = completed
                ? MgColors.Owned
                : current
                    ? MgColors.Training
                    : MgColors.Hairline;
            var bar = new View(context)
            {
                Background = MgTheme.Filled(context, color, cornerRadius: 2),
            };
            var layout = new LinearLayout.LayoutParams(0, Dp(6), 1);
            if (index > 0)
            {
                layout.SetMargins(Dp(3), 0, 0, 0);
            }

            bars.AddView(bar, layout);
        }

        panel.AddView(row, MatchWrapWithTop(MgSpacing.Sm));
    }

    private static string DailyTerminalSubtitle(CurrentTrainingPresentationReadModel presentation)
    {
        return presentation.DailyStatus switch
        {
            DailyTrainingWorkflowStatus.Done => "Today's recorded workout is complete.",
            DailyTrainingWorkflowStatus.Stopped => "No more work can be started today.",
            DailyTrainingWorkflowStatus.OffDay => "No training is prescribed today.",
            _ => "No startable exercise right now.",
        };
    }

    private static MgGlyphKind GlyphForDrill(DrillId? drill)
    {
        return drill switch
        {
            DrillId.FH1TargetHold or DrillId.FH2DistractorHold => MgGlyphKind.Target,
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter => MgGlyphKind.Next,
            DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform => MgGlyphKind.Read,
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => MgGlyphKind.Stop,
            DrillId.DE1PairDiscrimination or DrillId.DE2SeededAudit => MgGlyphKind.Review,
            DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping => MgGlyphKind.Map,
            DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery => MgGlyphKind.Alert,
            DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask => MgGlyphKind.Record,
            _ => MgGlyphKind.Target,
        };
    }

    private static string SessionDoseInstruction(TrainingPresentationWorkSummary work)
    {
        string Load(string name, string fallback) => work.LoadVariables
            .FirstOrDefault(variable => string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Value ?? fallback;

        return work.Drill switch
        {
            DrillId.FH2DistractorHold => $"{Load("duration", "5 minutes")} · Ignore each distractor.",
            DrillId.FS1CueSwitch => $"{Load("target count", "2")} targets · {Load("switch count", "4")} cues. Switch only on cue.",
            DrillId.FS2InvalidCueFilter => $"{Load("target count", "2")} targets · {Load("switch count", "6")} cues. Ignore invalid cues.",
            DrillId.WM1DelayedReconstruction => $"{Load("item count", "5")} items · {Load("delay", "60 seconds")} hidden recall.",
            DrillId.WM2MentalTransform => $"{Load("item count", "6")} items · {Load("operation steps", "2")} mental steps.",
            DrillId.IR1GoNoGoRule => $"{Load("response speed", "2 seconds")} response window. Withhold on no-go.",
            DrillId.IR2ExceptionRule => $"{Load("exception count", "3")} exceptions · {Load("response speed", "2 seconds")} responses.",
            DrillId.DE1PairDiscrimination => $"{Load("item quantity", "6")} comparisons · {Load("time limit", "60 seconds")}.",
            DrillId.DE2SeededAudit => $"Find {Load("quantity", "3")} supported errors in a locked output.",
            DrillId.CO1RuleExtraction => $"Infer one rule from {Load("example count", "8")} examples.",
            DrillId.CO2StructureMapping => $"Map {Load("relation count", "3")} relations across domains.",
            DrillId.AI1PressureRepeat => $"Repeat the source task under {Load("time pressure", "90 seconds")} pressure.",
            DrillId.AI2DisruptionRecovery => $"Resume within {Load("recovery window", "30 seconds")} after disruption.",
            DrillId.TI1CompositeTask => $"{Load("number of branches", "2")} branches · {Load("task length", "12 minutes")}.",
            DrillId.TI2GlobalReviewTask => $"Audit the program record for {Load("task length", "20 minutes")}.",
            _ => work.Exercise.FirstScreenInstruction,
        };
    }

    private static string StandardDisplay(TrainingPresentationWorkSummary work)
    {
        if (work.Drill == DrillId.FS1CueSwitch)
        {
            return "4 min · 90%+ correct · max 3 early switches";
        }

        return work.Standard ?? "Complete the visible task to its stated standard.";
    }

    private void AddActiveSessionProblem(PreUiActiveSessionResumeState resume)
    {
        var panel = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        AddTodayQuestion(panel);
        AddMarkerHeader(panel, "Attempt interrupted", "!", MgColors.Blocked);
        AddBody(panel, "This attempt cannot continue safely.");
        AddMuted(panel, "It is recorded as today's stopped attempt.");
        AddPrimaryButton(panel, "Clear attempt", enabled: true, () => ActiveSessionInvalidateRequested?.Invoke(resume.SessionId));
        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddTodayQuestion(LinearLayout panel)
    {
        var question = new TextView(context)
        {
            Text = "What should I do now?",
        };
        MgTypography.ApplyTitle(question);
        panel.AddView(question, MatchWrap());
    }

    private void AddTodayCommandObject(
        LinearLayout panel,
        CurrentTrainingPresentationReadModel presentation)
    {
        var color = ColorForPriority(presentation.Priority);
        if (presentation.PrimaryPrescribedWork is { } startableWork)
        {
            var hero = new LinearLayout(context)
            {
                Orientation = Orientation.Horizontal,
                Background = MgTheme.Filled(context, MgColors.InkDeep, cornerRadius: 8),
            };
            hero.SetGravity(GravityFlags.CenterVertical);
            hero.SetPadding(Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg));
            hero.Elevation = Dp(3);

            var markerStack = new LinearLayout(context)
            {
                Orientation = Orientation.Vertical,
            };
            markerStack.SetGravity(GravityFlags.Center);

            markerStack.AddView(
                new MgGlyphView(context, MgGlyphKind.Start, MgColors.TrainingAction),
                new LinearLayout.LayoutParams(Dp(56), Dp(56)));

            var markerLabel = new TextView(context)
            {
                Text = TodayMarkerFor(presentation),
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyLabel(markerLabel);
            markerLabel.SetTextColor(Color.White);
            markerStack.AddView(markerLabel, MatchWrapWithTop(MgSpacing.Xs));

            hero.AddView(markerStack, new LinearLayout.LayoutParams(Dp(78), ViewGroup.LayoutParams.WrapContent));

            var heroBody = new LinearLayout(context)
            {
                Orientation = Orientation.Vertical,
            };
            var heroBodyLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
            heroBodyLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
            hero.AddView(heroBody, heroBodyLayout);

            var title = new TextView(context)
            {
                Text = TodayCommandTitle(presentation),
            };
            MgTypography.ApplyHeading(title);
            title.SetTextColor(Color.White);
            heroBody.AddView(title, MatchWrap());

            var level = new TextView(context)
            {
                Text = startableWork.Exercise.BranchLevelLabel,
            };
            MgTypography.ApplyLabel(level);
            level.SetTextColor(MgColors.InkSoft);
            heroBody.AddView(level, MatchWrapWithTop(MgSpacing.Xs));

            var instruction = new TextView(context)
            {
                Text = startableWork.Exercise.FirstScreenInstruction,
            };
            MgTypography.ApplyBody(instruction);
            instruction.SetTextColor(MgColors.InkSoft);
            heroBody.AddView(instruction, MatchWrapWithTop(MgSpacing.Sm));

            if (startableWork.Drill == DrillId.FH1TargetHold)
            {
                var chipRow = new LinearLayout(context)
                {
                    Orientation = Orientation.Horizontal,
                };
                chipRow.SetGravity(GravityFlags.CenterVertical);
                AddHeroChip(chipRow, FocusHoldDurationValue(startableWork.LoadVariables));
                AddHeroChip(chipRow, "Same target");
                AddHeroChip(chipRow, "Tap Mind wandered", last: true);
                heroBody.AddView(chipRow, MatchWrapWithTop(MgSpacing.Md));
            }

            panel.AddView(hero, MatchWrapWithTop(MgSpacing.Md));

            if (presentation.MaintenanceDecayPriority is { } heroMaintenance &&
                presentation.Priority is TrainingPresentationPriorityKind.MaintenanceDue
                    or TrainingPresentationPriorityKind.DecayRestoration)
            {
                AddPriorityLine(panel, PriorityLabel(heroMaintenance.Kind), MaintenancePriorityUserDetail(heroMaintenance), color);
            }

            if (presentation.UrgentBlocker is { } heroBlocker &&
                presentation.Priority == TrainingPresentationPriorityKind.UrgentBlocker)
            {
                AddPriorityLine(panel, "Blocked", heroBlocker.Detail, MgColors.Blocked);
            }

            return;
        }

        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Lg), 0, 0);

        var markerView = new TextView(context)
        {
            Text = TodayMarkerFor(presentation),
            Gravity = GravityFlags.Center,
            Background = MgTheme.Filled(context, color == MgColors.Training ? MgColors.TrainingDark : color, cornerRadius: 8),
            ContentDescription = TitleFor(presentation),
        };
        MgTypography.ApplyTitle(markerView);
        markerView.SetTextColor(Color.White);
        markerView.Elevation = Dp(2);
        row.AddView(markerView, new LinearLayout.LayoutParams(Dp(64), Dp(88)));

        var body = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var bodyLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        bodyLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        row.AddView(body, bodyLayout);

        AddTodayHeading(body, TodayCommandTitle(presentation));
        if (presentation.PrimaryPrescribedWork is { } work)
        {
            AddTinyLine(body, work.Exercise.BranchLevelLabel);
            AddMuted(body, work.Exercise.FirstScreenInstruction);
        }
        else
        {
            AddTodayBranchNodes(body, TodayBranchLabels(presentation), color);
        }

        AddTodayFactRow(body, TodayFacts(presentation));
        panel.AddView(row, MatchWrap());

        if (presentation.MaintenanceDecayPriority is { } maintenance &&
            presentation.Priority is TrainingPresentationPriorityKind.MaintenanceDue
                or TrainingPresentationPriorityKind.DecayRestoration)
        {
            AddPriorityLine(panel, PriorityLabel(maintenance.Kind), MaintenancePriorityUserDetail(maintenance), color);
        }

        if (presentation.UrgentBlocker is { } blocker &&
            presentation.Priority == TrainingPresentationPriorityKind.UrgentBlocker)
        {
            AddPriorityLine(panel, "Blocked", blocker.Detail, MgColors.Blocked);
        }
    }

    private void AddDueGlobalReviewToday(
        LinearLayout panel,
        AndroidTrainingStateSnapshot state)
    {
        var review = state.CurrentState.GlobalReview;
        AddCompactScreenHeading(panel, "Global review", "Cycle review due today");
        AddDailyDoseProgress(panel, state.Presentation);
        var decision = SelectReviewDecision(review);
        AddWarningRow(
            panel,
            "Decision",
            ReviewDecisionLabel(decision),
            ColorForReviewDecision(review, decision));
        AddPrimaryButton(panel, "Review evidence", enabled: true, () =>
        {
            currentScreen = Screen.Review;
            RenderCurrentScreen();
        });
    }

    private void AddTodayPrimaryButton(
        LinearLayout panel,
        CurrentTrainingPresentationReadModel presentation)
    {
        if (presentation.PrimaryAction == TrainingPresentationPrimaryActionKind.ResolveBlocker)
        {
            AddPrimaryButton(panel, "Show blocker", enabled: true, () =>
            {
                currentScreen = Screen.Map;
                RenderCurrentScreen();
            });
            return;
        }

        AddPrimaryButton(
            panel,
            TodayPrimaryActionLabel(presentation),
            presentation.PrimaryActionEnabled,
            () => SessionStartRequested?.Invoke());
    }

    private void AddTodayPurposePreview(LinearLayout panel, TrainingExercisePresentation exercise)
    {
        var card = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.TintedSurface(context, MgColors.Surface, MgColors.HairlineSoft, cornerRadius: 8),
        };
        card.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md));
        card.Elevation = Dp(1);

        var whyDetail = string.IsNullOrWhiteSpace(exercise.PracticeGain)
            ? exercise.Purpose
            : $"{exercise.Purpose} {exercise.PracticeGain}";
        AddTodayPurposeRow(card, MgGlyphKind.Target, "What you practice", whyDetail, MgColors.Training);
        AddTodayPurposeRow(card, MgGlyphKind.Next, "How it gets harder", exercise.WhereItGoes, MgColors.TestReady);

        panel.AddView(card, MatchWrapWithTop(MgSpacing.Md));
    }

    private void AddTodayPurposeRow(
        LinearLayout card,
        MgGlyphKind glyph,
        string title,
        string detail,
        Color color)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);

        row.AddView(
            new MgGlyphView(context, glyph, color, filled: false),
            new LinearLayout.LayoutParams(Dp(40), Dp(40)));

        var stack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var stackLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        stackLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        row.AddView(stack, stackLayout);

        var titleView = new TextView(context)
        {
            Text = title,
        };
        MgTypography.ApplyBody(titleView);
        stack.AddView(titleView, MatchWrap());

        var detailView = new TextView(context)
        {
            Text = detail,
        };
        MgTypography.ApplyMicro(detailView);
        detailView.SetTextColor(MgColors.InkMuted);
        stack.AddView(detailView, MatchWrapWithTop(MgSpacing.Xs));

        card.AddView(row, MatchWrapWithTop(card.ChildCount == 0 ? 0 : MgSpacing.Sm));
    }

    private void AddHeroChip(LinearLayout row, string label, bool last = false)
    {
        var chip = new TextView(context)
        {
            Text = label,
            Gravity = GravityFlags.Center,
            Background = MgTheme.TintedSurface(context, Color.Argb(42, 255, 255, 255), MgColors.TrainingAction, cornerRadius: 8),
        };
        MgTypography.ApplyMicro(chip);
        chip.SetTextColor(Color.White);
        chip.SetPadding(Dp(MgSpacing.Sm), Dp(MgSpacing.Xs), Dp(MgSpacing.Sm), Dp(MgSpacing.Xs));

        var layout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        if (!last)
        {
            layout.SetMargins(0, 0, Dp(MgSpacing.Xs), 0);
        }

        row.AddView(chip, layout);
    }

    private void AddTodayHeading(LinearLayout panel, string title)
    {
        var view = new TextView(context)
        {
            Text = title,
        };
        MgTypography.ApplyHeading(view);
        panel.AddView(view, MatchWrap());
    }

    private void AddTinyLine(LinearLayout panel, string text)
    {
        var view = new TextView(context)
        {
            Text = text,
        };
        MgTypography.ApplyLabel(view);
        panel.AddView(view, MatchWrapWithTop(MgSpacing.Xs));
    }

    private void AddTodayBranchNodes(
        LinearLayout panel,
        IReadOnlyList<string> labels,
        Color color)
    {
        if (labels.Count == 0)
        {
            return;
        }

        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Sm), 0, 0);

        foreach (var label in labels.Take(4))
        {
            var node = new TextView(context)
            {
                Text = label,
                Gravity = GravityFlags.Center,
                Background = MgTheme.Outline(context, color, cornerRadius: 6),
            };
            node.SetSingleLine(true);
            node.SetTextColor(MgColors.Ink);
            node.SetMinWidth(Dp(48));
            node.SetPadding(Dp(MgSpacing.Sm), 0, Dp(MgSpacing.Sm), 0);
            MgTypography.ApplyLabel(node);

            var layout = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                Dp(34));
            layout.SetMargins(0, 0, Dp(MgSpacing.Sm), 0);
            row.AddView(node, layout);
        }

        if (labels.Count > 4)
        {
            var remaining = new TextView(context)
            {
                Text = $"{labels.Count - 4} more",
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyLabel(remaining);
            row.AddView(remaining, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                Dp(34)));
        }

        panel.AddView(row, MatchWrap());
    }

    private void AddTodayFactRow(
        LinearLayout panel,
        IReadOnlyList<TodayFact> facts)
    {
        if (facts.Count == 0)
        {
            return;
        }

        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Sm), 0, 0);

        foreach (var fact in facts.Take(3))
        {
            var view = new StatusChipView(context, fact.Text, fact.Color, fact.Filled);
            var layout = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent);
            layout.SetMargins(0, 0, Dp(MgSpacing.Sm), 0);
            row.AddView(view, layout);
        }

        panel.AddView(row, MatchWrap());
    }

    private void AddPriorityLine(
        LinearLayout panel,
        string title,
        string detail,
        Color color)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Md), 0, 0);

        var bar = new View(context)
        {
            Background = MgTheme.Filled(context, color, cornerRadius: 3),
        };
        row.AddView(bar, new LinearLayout.LayoutParams(Dp(6), Dp(46)));

        var textStack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var textLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        textLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        row.AddView(textStack, textLayout);

        var titleView = new TextView(context)
        {
            Text = title,
        };
        MgTypography.ApplyLabel(titleView);
        textStack.AddView(titleView, MatchWrap());

        var detailView = new TextView(context)
        {
            Text = detail,
        };
        MgTypography.ApplyBody(detailView);
        detailView.SetTextColor(MgColors.Ink);
        textStack.AddView(detailView, MatchWrap());

        panel.AddView(row, MatchWrap());
    }

    private void AddBranchPreview(CurrentTrainingStateReadModel state)
    {
        var panel = Panel();
        AddSectionTitle(panel, "Branch state");
        foreach (var branch in ProgramCatalog.Branches.Select(branch => branch.Code))
        {
            var statuses = state.BranchLevelStates
                .Where(status => status.Branch == branch)
                .OrderBy(status => status.Level)
                .ToArray();
            if (statuses.Length == 0)
            {
                continue;
            }

            var row = new LinearLayout(context)
            {
                Orientation = Orientation.Horizontal,
            };
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(0, Dp(MgSpacing.Xs), 0, Dp(MgSpacing.Xs));

            var branchLabel = Label(branch.ToString(), minWidthDp: 42);
            row.AddView(branchLabel);
            foreach (var status in statuses.Take(5))
            {
                var blocked = state.BlockedAdvancement.Any(blocker => blocker.Branch == status.Branch && blocker.Level == status.Level);
                var due = state.DueMaintenance.Any(item => item.BranchLevel.Branch == status.Branch && item.BranchLevel.Level == status.Level);
                row.AddView(new LevelCellView(context, status, blocked, due), WrapWrap());
            }

            panel.AddView(row);
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddMap(AndroidTrainingStateSnapshot? state)
    {
        if (state is null)
        {
            AddPanel("Map unavailable", "Training state is not loaded.", "Return to Today.");
            return;
        }

        var panel = FocusedPanel();
        AddBranchMapSection(panel, state, BranchType.Foundational, "Foundations");
        AddBranchMapSection(panel, state, BranchType.Advanced, "Advanced");
        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddBranchMapSection(
        LinearLayout panel,
        AndroidTrainingStateSnapshot state,
        BranchType type,
        string title)
    {
        var section = new TextView(context)
        {
            Text = title,
        };
        MgTypography.ApplyLabel(section);
        section.SetTextColor(MgColors.InkMuted);
        panel.AddView(section, MatchWrapWithTop(MgSpacing.Xl));

        foreach (var branch in ProgramCatalog.Branches.Where(branch => branch.Type == type))
        {
            AddBranchMapRow(panel, state, branch);
        }
    }

    private void AddBranchMapRow(
        LinearLayout panel,
        AndroidTrainingStateSnapshot state,
        BranchDefinition branch)
    {
        var levels = state.CurrentState.BranchLevelStates
            .Where(status => status.Branch == branch.Code)
            .OrderBy(status => status.Level)
            .ToArray();
        if (levels.Length == 0)
        {
            return;
        }

        var current = levels
            .Where(status => status.State != BranchLevelState.Unopened)
            .OrderByDescending(status => status.Level)
            .FirstOrDefault();
        if (current == default)
        {
            current = levels[0];
        }

        var currentMaintenanceDue = state.CurrentState.DueMaintenance.Any(item =>
            item.BranchLevel.Branch == current.Branch && item.BranchLevel.Level == current.Level);

        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Clickable = true,
            Focusable = true,
            Background = MgTheme.Surface(context, cornerRadius: 8),
            ContentDescription = $"{branch.Name}, {current.Level}, {(currentMaintenanceDue ? "Maintenance due" : BranchStateLabel(current.State))}. Open branch.",
        };
        row.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md));
        row.Click += (_, _) =>
        {
            selectedBranch = branch.Code;
            selectedLevel = current.Level;
            currentScreen = Screen.BranchDetail;
            RenderCurrentScreen();
        };

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);
        var name = new TextView(context)
        {
            Text = branch.Name,
        };
        name.SetMaxLines(2);
        MgTypography.ApplyBody(name);
        name.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Bold), TypefaceStyle.Bold);
        header.AddView(name, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        var stateMarkerLayout = WrapWrap();
        stateMarkerLayout.SetMargins(Dp(MgSpacing.Sm), 0, 0, 0);
        header.AddView(new StateMarkerView(context, current.State, currentMaintenanceDue), stateMarkerLayout);
        row.AddView(header, MatchWrap());

        var rail = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        rail.SetGravity(GravityFlags.CenterVertical);
        rail.SetPadding(0, Dp(MgSpacing.Sm), 0, 0);
        foreach (var level in levels)
        {
            var blocked = state.CurrentState.BlockedAdvancement.Any(blocker =>
                blocker.Branch == level.Branch && blocker.Level == level.Level);
            var due = state.CurrentState.DueMaintenance.Any(item =>
                item.BranchLevel.Branch == level.Branch && item.BranchLevel.Level == level.Level);
            var next = state.Presentation.PrimaryPrescribedWork?.BranchLevels.Any(item =>
                item.Branch == level.Branch && item.Level == level.Level) == true;
            rail.AddView(
                new LevelCellView(context, level, blocked, due, nextWork: next),
                new LinearLayout.LayoutParams(0, Dp(58), 1));
        }

        row.AddView(rail, MatchWrap());
        panel.AddView(row, MatchWrapWithTop(MgSpacing.Sm));
    }

    private void AddBranchDetail(AndroidTrainingStateSnapshot? state)
    {
        if (state is null)
        {
            AddPanel("Branch unavailable", "Training state is not loaded.", "Return to Map.");
            return;
        }

        var branch = ProgramCatalog.Branches.Single(item => item.Code == selectedBranch);
        var levels = state.CurrentState.BranchLevelStates
            .Where(status => status.Branch == selectedBranch)
            .OrderBy(status => status.Level)
            .ToArray();
        var selected = levels.FirstOrDefault(status => status.Level == selectedLevel);
        if (selected == default)
        {
            selected = levels[0];
            selectedLevel = selected.Level;
        }

        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == selectedBranch && item.Level == selectedLevel);
        var panel = FocusedPanel();
        AddCompactScreenHeading(panel, $"Level {LevelRank(selectedLevel)}", BranchStateLabel(selected.State));

        var levelRow = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        foreach (var level in levels)
        {
            var button = new Button(context)
            {
                Text = level.Level.ToString(),
                Enabled = level.Level != selectedLevel,
                ContentDescription = $"{level.Level}, {BranchStateLabel(level.State)}",
            };
            button.SetAllCaps(false);
            button.SetMinHeight(Dp(48));
            button.SetMinimumHeight(Dp(48));
            MgTypography.ApplyLabel(button);
            button.SetTextColor(level.Level == selectedLevel ? Color.White : MgColors.Ink);
            button.Background = level.Level == selectedLevel
                ? MgTheme.Filled(context, ColorForBranchState(level.State), cornerRadius: 8)
                : MgTheme.MutedSurface(context, cornerRadius: 8);
            button.Click += (_, _) =>
            {
                selectedLevel = level.Level;
                RenderCurrentScreen();
            };
            var layout = new LinearLayout.LayoutParams(0, Dp(48), 1);
            layout.SetMargins(levelRow.ChildCount == 0 ? 0 : Dp(MgSpacing.Xs), 0, 0, 0);
            levelRow.AddView(button, layout);
        }

        panel.AddView(levelRow, MatchWrapWithTop(MgSpacing.Lg));

        if (selectedBranch == BranchCode.FH && selectedLevel == GlobalLevelId.L1)
        {
            panel.AddView(FocusHoldCriteriaStrip(), MatchWrapWithTop(MgSpacing.Lg));
        }
        else
        {
            AddFocusedBlock(panel, "Standard", standard.Standard);
        }

        AddFocusedBlock(panel, "Demand", standard.Demand);
        if (selected.State == BranchLevelState.Unopened)
        {
            var unlockRule = selectedLevel == GlobalLevelId.L1
                ? branch.UnlockRule
                : standard.Gate;
            AddWarningRow(panel, "Unlock", unlockRule, MgColors.Recovery);
        }

        var evidenceCount = state.CurrentState.EvidenceSummaries.Count(artifact =>
            artifact.Event.Branch == selectedBranch && artifact.Event.Level == selectedLevel);
        var evidence = new TextView(context)
        {
            Text = evidenceCount == 1 ? "1 recorded set" : $"{evidenceCount} recorded sets",
        };
        MgTypography.ApplyLabel(evidence);
        evidence.SetTextColor(MgColors.InkMuted);
        panel.AddView(evidence, MatchWrapWithTop(MgSpacing.Md));

        var isCurrentWork = state.Presentation.PrimaryPrescribedWork?.BranchLevels.Any(item =>
            item.Branch == selectedBranch && item.Level == selectedLevel) == true;
        if (isCurrentWork)
        {
            AddPrimaryButton(panel, "Open current exercise", enabled: true, () =>
            {
                liveCompletionSnapshot = null;
                if (liveSessionSnapshot is not null)
                {
                    currentScreen = Screen.Live;
                    RenderCurrentScreen();
                    return;
                }

                currentScreen = Screen.Today;
                SessionStartRequested?.Invoke();
            });
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private static string BranchStateLabel(BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.Unopened => "Locked",
            BranchLevelState.Training => "Training",
            BranchLevelState.TestReady => "Test ready",
            BranchLevelState.PassedOnce => "Passed once",
            BranchLevelState.Stabilizing => "Stabilizing",
            BranchLevelState.Owned => "Owned",
            BranchLevelState.Maintenance => "Maintenance due",
            BranchLevelState.Decayed => "Decayed",
            _ => state.ToString(),
        };
    }

    private void AddEvidence(AndroidTrainingStateSnapshot? state)
    {
        if (state is null)
        {
            AddPanel("Log unavailable", "Training state is not loaded.", "Return to Today.");
            return;
        }

        var panel = FocusedPanel();
        AddRecordFilters(panel);

        var sessions = state.CurrentState.RecentSessions
            .Where(session => recordFilter switch
            {
                RecordFilter.Clean => session.CleanPerformance,
                RecordFilter.Missed => !session.CleanPerformance,
                _ => true,
            })
            .OrderByDescending(session => session.Date.Year)
            .ThenByDescending(session => session.Date.Month)
            .ThenByDescending(session => session.Date.Day)
            .ToArray();
        foreach (var session in sessions)
        {
            AddRecordSessionRow(panel, session);
        }

        if (sessions.Length == 0)
        {
            var empty = new TextView(context)
            {
                Text = recordFilter == RecordFilter.All
                    ? "No completed sessions yet."
                    : "No sessions match this filter.",
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyBody(empty);
            empty.SetTextColor(MgColors.InkMuted);
            empty.SetPadding(0, Dp(MgSpacing.Xl), 0, Dp(MgSpacing.Xl));
            panel.AddView(empty, MatchWrapWithTop(MgSpacing.Md));
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddRecordFilters(LinearLayout panel)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        foreach (var (filter, label) in new[]
        {
            (RecordFilter.All, "All"),
            (RecordFilter.Clean, "Clean"),
            (RecordFilter.Missed, "Missed"),
        })
        {
            var selected = recordFilter == filter;
            var button = new Button(context)
            {
                Text = label,
                Enabled = !selected,
                ContentDescription = selected ? $"{label}, selected" : label,
            };
            button.SetAllCaps(false);
            button.SetMinHeight(Dp(48));
            button.SetMinimumHeight(Dp(48));
            MgTypography.ApplyLabel(button);
            button.SetTextColor(selected ? Color.White : MgColors.Ink);
            button.Background = selected
                ? MgTheme.Filled(context, MgColors.InkDeep, cornerRadius: 8)
                : MgTheme.MutedSurface(context, cornerRadius: 8);
            button.Click += (_, _) =>
            {
                recordFilter = filter;
                RenderCurrentScreen(resetScroll: false);
            };
            var layout = new LinearLayout.LayoutParams(0, Dp(48), 1);
            layout.SetMargins(row.ChildCount == 0 ? 0 : Dp(MgSpacing.Xs), 0, 0, 0);
            row.AddView(button, layout);
        }

        panel.AddView(row, MatchWrap());
    }

    private void AddRecordSessionRow(
        LinearLayout panel,
        LocalSessionHistoryRecord session)
    {
        var clean = session.CleanPerformance;
        var color = clean ? MgColors.Owned : MgColors.Blocked;
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Clickable = true,
            Focusable = true,
            Background = MgTheme.Surface(context, cornerRadius: 8),
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md));

        var marker = new MgGlyphView(
            context,
            clean ? MgGlyphKind.Check : MgGlyphKind.Alert,
            color,
            filled: false,
            showContainer: false)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        row.AddView(marker, new LinearLayout.LayoutParams(Dp(40), Dp(40)));

        var stack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var stackLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        stackLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        row.AddView(stack, stackLayout);

        var drill = session.Drill.HasValue
            ? ProgramCatalog.Drills.Single(item => item.Id == session.Drill.Value).Name
            : SessionTypeLabel(session.SessionType);
        var title = new TextView(context)
        {
            Text = drill,
        };
        MgTypography.ApplyBody(title);
        title.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Bold), TypefaceStyle.Bold);
        stack.AddView(title, MatchWrap());

        var detail = new TextView(context)
        {
            Text = $"{FormatDate(session.Date)} · {SessionBranchLevel(session)}",
        };
        MgTypography.ApplyLabel(detail);
        detail.SetTextColor(MgColors.InkMuted);
        stack.AddView(detail, MatchWrapWithTop(MgSpacing.Xs));

        var state = new TextView(context)
        {
            Text = clean ? "Clean" : SessionMissLabel(session),
        };
        MgTypography.ApplyLabel(state);
        state.SetTextColor(color);
        row.AddView(state, WrapWrap());
        row.ContentDescription = $"{drill}, {detail.Text}, {state.Text}. Open session.";
        row.Click += (_, _) =>
        {
            selectedSessionId = session.SessionId;
            currentScreen = Screen.RecordDetail;
            RenderCurrentScreen();
        };
        panel.AddView(row, MatchWrapWithTop(MgSpacing.Sm));
    }

    private void AddRecordDetail(AndroidTrainingStateSnapshot? state)
    {
        var session = state?.CurrentState.RecentSessions.FirstOrDefault(item =>
            string.Equals(item.SessionId, selectedSessionId, StringComparison.Ordinal));
        if (state is null || session is null)
        {
            AddPanel("Session unavailable", "This session is not in the loaded record window.", "Return to Record.");
            return;
        }

        var drill = session.Drill.HasValue
            ? ProgramCatalog.Drills.Single(item => item.Id == session.Drill.Value).Name
            : SessionTypeLabel(session.SessionType);
        var panel = FocusedPanel();
        AddCompactScreenHeading(panel, drill, $"{FormatDate(session.Date)} · {SessionBranchLevel(session)}");

        var outcome = new TextView(context)
        {
            Text = session.CleanPerformance ? "Clean practice" : SessionMissLabel(session),
            Gravity = GravityFlags.Center,
            Background = MgTheme.TintedSurface(
                context,
                session.CleanPerformance ? MgColors.TrainingTint : MgColors.BlockedTint,
                session.CleanPerformance ? MgColors.Owned : MgColors.Blocked,
                cornerRadius: 8),
        };
        MgTypography.ApplyHeading(outcome);
        outcome.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md));
        panel.AddView(outcome, MatchWrapWithTop(MgSpacing.Lg));

        var artifacts = state.CurrentState.EvidenceSummaries
            .Where(artifact => session.EvidenceArtifactIds.Contains(artifact.ArtifactId))
            .ToArray();
        if (artifacts.Length == 0)
        {
            AddMuted(panel, "No detailed evidence is available in the current record window.");
        }
        else if (session.Drill == DrillId.FH1TargetHold)
        {
            AddFocusHoldRecordEvidence(panel, session, artifacts);
        }
        else
        {
            AddReadableRecordEvidence(panel, session, artifacts);
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private static string SessionBranchLevel(LocalSessionHistoryRecord session)
    {
        return session.BranchLevels.FirstOrDefault() is { } branchLevel
            ? FormatBranchLevel(branchLevel.Branch, branchLevel.Level)
            : "General";
    }

    private void AddFocusHoldRecordEvidence(
        LinearLayout panel,
        LocalSessionHistoryRecord session,
        IReadOnlyList<LocalEvidenceArtifactRecord> artifacts)
    {
        var facts = EvidenceFacts(artifacts);
        var drifts = IntFact(facts, "drift_count");
        var returns = IntFact(facts, "return_count");
        var lateReturns = IntFact(facts, "late_return_count");
        var changes = IntFact(facts, "target_substitution_count");
        var completed = !session.Notes.Contains("abandon", StringComparison.OrdinalIgnoreCase);
        panel.AddView(
            new CriteriaStripView(
                context,
                [
                    (completed ? FocusHoldDurationLabel(session.LoadVariables) : "--", "hold"),
                    ($"{drifts}/5", "wanders"),
                    (drifts == 0 ? "--" : $"{returns}/{drifts}", "returns"),
                    (changes.ToString(), "changes"),
                ]),
            MatchWrapWithTop(MgSpacing.Md));

        if (session.CleanPerformance)
        {
            return;
        }

        var reason = !completed
            ? $"The hold ended before {FocusHoldDurationLabel(session.LoadVariables)}."
            : returns < drifts
                ? "A wander was not followed by a return."
                : lateReturns > 0
                    ? "A return took longer than 10 seconds."
                    : changes > 0
                        ? "The target changed during the hold."
                        : drifts > FocusHoldLevelOneStandard.MaximumMarkedDrifts
                            ? "The hold exceeded 5 wanders."
                            : "The recorded set did not meet the standard.";
        AddWarningRow(panel, "Why it missed", reason, MgColors.Blocked);
    }

    private void AddReadableRecordEvidence(
        LinearLayout panel,
        LocalSessionHistoryRecord session,
        IReadOnlyList<LocalEvidenceArtifactRecord> artifacts)
    {
        var standard = session.BranchLevels.FirstOrDefault() is { } branchLevel
            ? ProgramCatalog.Standards.FirstOrDefault(item =>
                item.Branch == branchLevel.Branch && item.Level == branchLevel.Level)
            : null;
        if (standard is not null)
        {
            AddFocusedBlock(panel, "Standard", standard.Standard);
        }

        var shown = 0;
        foreach (var evidence in artifacts.SelectMany(item => item.Artifact.ObservableEvidence))
        {
            if (evidence.Kind == ObservableEvidenceKind.OutputSample && LooksLikeRuntimeSummary(evidence.Description))
            {
                continue;
            }

            if (evidence.Kind == ObservableEvidenceKind.Score)
            {
                foreach (var (name, value) in ParseEvidenceFacts(evidence.Description)
                    .Where(item => IsUsefulEvidenceFact(item.Key)))
                {
                    AddEvidenceFact(panel, HumanizeEvidenceFact(name), value);
                    shown++;
                }

                continue;
            }

            if (evidence.Kind is ObservableEvidenceKind.LoadVariableRecord or
                ObservableEvidenceKind.CriticalConstraintRecord)
            {
                continue;
            }

            AddEvidenceFact(panel, ObservableEvidenceLabel(evidence.Kind), evidence.Description);
            shown++;
        }

        if (shown == 0)
        {
            AddMuted(panel, "No readable evidence was saved for this set.");
        }
    }

    private void AddEvidenceFact(
        LinearLayout panel,
        string label,
        string value)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.MutedSurface(context, cornerRadius: 8),
        };
        row.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Sm), Dp(MgSpacing.Md), Dp(MgSpacing.Sm));
        var labelView = new TextView(context)
        {
            Text = label,
        };
        MgTypography.ApplyLabel(labelView);
        labelView.SetTextColor(MgColors.InkMuted);
        row.AddView(labelView, MatchWrap());
        var valueView = new TextView(context)
        {
            Text = value,
        };
        MgTypography.ApplyBody(valueView);
        row.AddView(valueView, MatchWrapWithTop(MgSpacing.Xs));
        panel.AddView(row, MatchWrapWithTop(MgSpacing.Sm));
    }

    private static IReadOnlyDictionary<string, string> EvidenceFacts(
        IReadOnlyList<LocalEvidenceArtifactRecord> artifacts)
    {
        var score = artifacts
            .SelectMany(item => item.Artifact.ObservableEvidence)
            .FirstOrDefault(item => item.Kind == ObservableEvidenceKind.Score)?.Description;
        return ParseEvidenceFacts(score);
    }

    private static IReadOnlyDictionary<string, string> ParseEvidenceFacts(string? value)
    {
        var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return facts;
        }

        foreach (var segment in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0 || separator == segment.Length - 1)
            {
                continue;
            }

            facts[segment[..separator].Trim()] = segment[(separator + 1)..].Trim();
        }

        return facts;
    }

    private static int IntFact(IReadOnlyDictionary<string, string> facts, string name)
    {
        return facts.TryGetValue(name, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : 0;
    }

    private static bool LooksLikeRuntimeSummary(string value)
    {
        return value.Contains("runtime event log", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("live session completed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUsefulEvidenceFact(string name)
    {
        return !name.Contains("phase", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("event", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("protocol", StringComparison.OrdinalIgnoreCase);
    }

    private static string HumanizeEvidenceFact(string name)
    {
        var words = name.Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(words)
            ? "Measure"
            : char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static string SessionMissLabel(LocalSessionHistoryRecord session)
    {
        return session.Notes.Contains("abandon", StringComparison.OrdinalIgnoreCase)
            ? "Stopped"
            : "Missed";
    }

    private void AddReview(AndroidTrainingStateSnapshot? state)
    {
        if (state is null)
        {
            AddPanel("Review unavailable", "Training state is not loaded.", "Return to Today.");
            return;
        }

        var panel = FocusedPanel();

        var reviewDue = state.DailyTraining?.CurrentBlock?.Record.Role == LocalDailyTrainingBlockRole.Review;
        var next = reviewDue ? null : state.Presentation.PrimaryPrescribedWork;
        var due = state.Presentation.MaintenanceDecayPriority;
        var blocker = next is null ? state.Presentation.UrgentBlocker : null;
        var repeatCurrentLevel = next is not null && state.CurrentState.RecentSessions
            .OrderByDescending(session => session.Date.Year)
            .ThenByDescending(session => session.Date.Month)
            .ThenByDescending(session => session.Date.Day)
            .FirstOrDefault() is { CleanPerformance: false } lastSession &&
            lastSession.BranchLevels.Any(level => next.BranchLevels.Any(workLevel =>
                workLevel.Branch == level.Branch && workLevel.Level == level.Level));
        var color = due is not null
            ? due.BlocksAdvancement ? MgColors.Blocked : MgColors.Maintenance
            : blocker is not null
                ? MgColors.Blocked
                : repeatCurrentLevel ? MgColors.Recovery : MgColors.Training;
        var statusBand = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        statusBand.SetGravity(GravityFlags.CenterVertical);
        statusBand.SetPadding(0, Dp(MgSpacing.Sm), 0, Dp(MgSpacing.Sm));
        var rail = new View(context);
        rail.SetBackgroundColor(color);
        statusBand.AddView(rail, new LinearLayout.LayoutParams(Dp(4), Dp(56)));
        var statusStack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var decisionLabel = new TextView(context)
        {
            Text = "PROGRAM DECISION",
        };
        MgTypography.ApplyLabel(decisionLabel);
        decisionLabel.SetTextColor(MgColors.InkMuted);
        statusStack.AddView(decisionLabel, MatchWrap());
        var status = new TextView(context)
        {
            Text = reviewDue
                ? "Global review due"
                : due is not null
                ? PriorityLabel(due.Kind)
                : blocker is not null
                    ? "Resolve blocker"
                    : repeatCurrentLevel
                        ? "Repeat current level"
                        : next is not null ? "Continue current level" : "No work due",
        };
        MgTypography.ApplyHeading(status);
        statusStack.AddView(status, MatchWrapWithTop(MgSpacing.Xs));
        var statusLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        statusLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        statusBand.AddView(statusStack, statusLayout);
        panel.AddView(statusBand, MatchWrap());

        var review = state.CurrentState.GlobalReview;
        var reviewDecision = SelectReviewDecision(review);
        AddWarningRow(
            panel,
            "Decision",
            ReviewDecisionLabel(reviewDecision),
            ColorForReviewDecision(review, reviewDecision));
        AddWarningRow(
            panel,
            "Inputs",
            ReviewFailureSummaryLabel(review.Evaluation.Failures),
            review.Evaluation.Passed ? MgColors.Owned : MgColors.Blocked);
        AddFocusedBlock(
            panel,
            "Cadence",
            review.Cadence.IsDue
                ? $"Due now · every {review.Cadence.CadenceDays} days"
                : $"Next {ReviewDateLabel(review.Cadence.NextReviewOn)}");

        if (next is not null)
        {
            var nextRow = new LinearLayout(context)
            {
                Orientation = Orientation.Vertical,
                Background = MgTheme.Surface(context, cornerRadius: 8),
            };
            nextRow.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md));
            var label = new TextView(context)
            {
                Text = "NEXT EXERCISE",
            };
            MgTypography.ApplyLabel(label);
            label.SetTextColor(MgColors.InkMuted);
            nextRow.AddView(label, MatchWrap());
            var title = new TextView(context)
            {
                Text = next.Exercise.ExerciseName,
            };
            MgTypography.ApplyHeading(title);
            nextRow.AddView(title, MatchWrapWithTop(MgSpacing.Xs));
            var detail = new TextView(context)
            {
                Text = next.Exercise.BranchLevelLabel,
            };
            MgTypography.ApplyBody(detail);
            detail.SetTextColor(MgColors.InkMuted);
            nextRow.AddView(detail, MatchWrapWithTop(MgSpacing.Xs));
            panel.AddView(nextRow, MatchWrapWithTop(MgSpacing.Md));
        }

        if (due is not null)
        {
            AddWarningRow(panel, "Why", MaintenancePriorityUserDetail(due), color);
        }
        else if (blocker is not null)
        {
            AddWarningRow(panel, "Why", blocker.Detail, MgColors.Blocked);
        }

        AddReviewFoundationSummary(panel, state.CurrentState);
        if (reviewDue)
        {
            AddPrimaryButton(
                panel,
                "Complete review",
                enabled: true,
                () => GlobalReviewCompletionRequested?.Invoke());
        }
        else if (next is not null)
        {
            AddPrimaryButton(panel, "Open next exercise", enabled: true, () =>
            {
                liveCompletionSnapshot = null;
                if (liveSessionSnapshot is not null)
                {
                    currentScreen = Screen.Live;
                    RenderCurrentScreen();
                    return;
                }

                currentScreen = Screen.Today;
                SessionStartRequested?.Invoke();
            });
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddReviewFoundationSummary(
        LinearLayout panel,
        CurrentTrainingStateReadModel state)
    {
        var label = new TextView(context)
        {
            Text = "FOUNDATIONS",
        };
        MgTypography.ApplyLabel(label);
        label.SetTextColor(MgColors.InkMuted);
        panel.AddView(label, MatchWrapWithTop(MgSpacing.Xl));

        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        foreach (var branch in ProgramCatalog.Branches.Where(item => item.Type == BranchType.Foundational))
        {
            var current = state.BranchLevelStates
                .Where(status => status.Branch == branch.Code && status.State != BranchLevelState.Unopened)
                .OrderByDescending(status => status.Level)
                .FirstOrDefault();
            var cell = new LinearLayout(context)
            {
                Orientation = Orientation.Vertical,
            };
            cell.SetGravity(GravityFlags.Center);
            var branchLabel = new TextView(context)
            {
                Text = FoundationShortLabel(branch.Code),
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyLabel(branchLabel);
            var stateLabel = new TextView(context)
            {
                Text = current == default ? "Locked" : current.Level.ToString(),
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyBody(stateLabel);
            stateLabel.SetTextColor(current == default ? MgColors.InkMuted : ColorForBranchState(current.State));
            cell.AddView(branchLabel, MatchWrap());
            cell.AddView(stateLabel, MatchWrapWithTop(MgSpacing.Xs));
            row.AddView(cell, new LinearLayout.LayoutParams(0, Dp(58), 1));
        }

        panel.AddView(row, MatchWrapWithTop(MgSpacing.Sm));
    }

    private static string FoundationShortLabel(BranchCode branch)
    {
        return branch switch
        {
            BranchCode.FH => "Focus",
            BranchCode.FS => "Shift",
            BranchCode.WM => "Memory",
            BranchCode.IR => "Control",
            BranchCode.DE => "Detail",
            _ => BranchLabel(branch),
        };
    }

    private void AddLocalData(AndroidTrainingStateSnapshot? state)
    {
        if (state is null)
        {
            AddPanel("Local data unavailable", "Training state is not loaded.", "Return to Today.");
            return;
        }

        var local = state.LocalData;
        var panel = FocusedPanel();

        var integrity = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Background = MgTheme.TintedSurface(
                context,
                local.CurrentIntegrity.IsValid ? MgColors.TrainingTint : MgColors.BlockedTint,
                local.CurrentIntegrity.IsValid ? MgColors.Owned : MgColors.Blocked,
                cornerRadius: 8),
        };
        integrity.SetGravity(GravityFlags.CenterVertical);
        integrity.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md));
        var integrityIcon = new MgGlyphView(
            context,
            local.CurrentIntegrity.IsValid ? MgGlyphKind.Check : MgGlyphKind.Alert,
            local.CurrentIntegrity.IsValid ? MgColors.Owned : MgColors.Blocked,
            filled: false,
            showContainer: false)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        integrity.AddView(integrityIcon, new LinearLayout.LayoutParams(Dp(40), Dp(40)));
        var integrityText = new TextView(context)
        {
            Text = local.CurrentIntegrity.IsValid ? "Current data is valid" : "Current data needs attention",
        };
        MgTypography.ApplyHeading(integrityText);
        var integrityTextLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        integrityTextLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        integrity.AddView(integrityText, integrityTextLayout);
        panel.AddView(integrity, MatchWrap());

        var backupText = new TextView(context)
        {
            Text = local.LatestBackup is { } backup
                ? $"Latest backup · {backup.LastModifiedUtc.ToLocalTime():yyyy-MM-dd HH:mm}"
                : "No backup yet",
        };
        MgTypography.ApplyBody(backupText);
        backupText.SetTextColor(MgColors.InkMuted);
        panel.AddView(backupText, MatchWrapWithTop(MgSpacing.Md));

        if (localDataOperation is { } operation)
        {
            AddWarningRow(
                panel,
                OperationStatusLabel(operation.Status),
                operation.Detail,
                operation.Status == LocalDataBackupOperationStatus.Succeeded ? MgColors.Owned : MgColors.Blocked);
        }

        AddLocalDataAction(panel, "Create backup", enabled: true, () => LocalBackupExportRequested?.Invoke());
        AddLocalDataAction(panel, "Check current data", enabled: true, () => LocalDataValidateRequested?.Invoke());
        AddLocalDataAction(
            panel,
            "Check latest backup",
            enabled: local.LatestBackup is not null,
            () => LocalBackupValidateRequested?.Invoke());
        if (restoreConfirmationArmed)
        {
            AddWarningRow(
                panel,
                "Replace local data?",
                "The latest backup will replace the current local record.",
                MgColors.Blocked);
        }

        AddLocalDataAction(panel, restoreConfirmationArmed ? "Confirm replace local data" : "Restore latest backup", local.LatestBackup is not null, () =>
        {
            if (!restoreConfirmationArmed)
            {
                restoreConfirmationArmed = true;
                RenderCurrentScreen(resetScroll: false);
                scrollView.Post(() => scrollView.FullScroll(FocusSearchDirection.Down));
                return;
            }

            LocalBackupRestoreRequested?.Invoke();
        }, destructive: true);

        if (restoreConfirmationArmed)
        {
            AddLocalDataAction(panel, "Cancel restore", enabled: true, () =>
            {
                restoreConfirmationArmed = false;
                RenderCurrentScreen(resetScroll: false);
            });
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddLocalDataAction(
        LinearLayout panel,
        string label,
        bool enabled,
        Action action,
        bool destructive = false)
    {
        var button = new Button(context)
        {
            Text = label,
            Enabled = enabled,
            ContentDescription = enabled ? label : $"{label} unavailable",
        };
        button.SetAllCaps(false);
        button.SetMinHeight(Dp(52));
        button.SetMinimumHeight(Dp(52));
        button.SetSingleLine(false);
        MgTypography.ApplyBody(button);
        button.SetTextColor(enabled
            ? destructive ? MgColors.Blocked : MgColors.Ink
            : MgColors.InkMuted);
        button.Background = enabled && destructive
            ? MgTheme.Outline(context, MgColors.Blocked, cornerRadius: 8)
            : MgTheme.MutedSurface(context, cornerRadius: 8);
        button.Click += (_, _) =>
        {
            if (enabled)
            {
                action();
            }
        };
        panel.AddView(button, MatchWrapWithTop(MgSpacing.Sm));
    }

    private void AddPreflight(AndroidSessionStartSnapshot? start)
    {
        if (start is null)
        {
            AddPanel("Getting ready", "Before-start details are not ready yet.", "Nothing counts until the exercise starts.");
            return;
        }

        var preflight = start.Presentation;
        var panel = FocusedPanel();

        if (preflight.Work is { } work)
        {
            var requiresFailureMode = RequiresFailureModeSelection(work);
            AddCompactScreenHeading(panel, work.Exercise.ExerciseName, work.Exercise.BranchLevelLabel);

            if (work.Drill == DrillId.FH1TargetHold)
            {
                var target = new TargetMaterialView(context, compact: true);
                target.Update(work.Exercise.PrimaryMaterial);
                panel.AddView(target, MatchWrapWithTop(MgSpacing.Md));

                AddFailureModeSelector(panel, work);

                AddPrimaryButton(
                    panel,
                    preflight.CanStart ? PreflightStartLabel(work) : "Blocked",
                    preflight.CanStart && (!requiresFailureMode || selectedMainFailureMode is not null),
                    HandlePreflightStartRequested);

                var prompt = new TextView(context)
                {
                    Text = "Say it once. Keep it unchanged.",
                    Gravity = GravityFlags.Center,
                };
                MgTypography.ApplyHeading(prompt);
                panel.AddView(prompt, MatchWrapWithTop(MgSpacing.Md));
                panel.AddView(FocusHoldCriteriaStrip(work.LoadVariables), MatchWrapWithTop(MgSpacing.Md));
                AddSetupSteps(panel);
            }
            else
            {
                AddPreflightMaterial(panel, work);
                AddDrillSetupSteps(panel, work.Drill ?? throw new InvalidOperationException("Prepared work must identify its drill."));
                AddFocusedBlock(
                    panel,
                    StandardBlockLabel(work),
                    StandardDisplay(work));
                AddFailureModeSelector(panel, work);
            }

            if (work.Drill != DrillId.FH1TargetHold)
            {
                AddPrimaryButton(
                    panel,
                    preflight.CanStart ? PreflightStartLabel(work) : "Blocked",
                    preflight.CanStart && (!requiresFailureMode || selectedMainFailureMode is not null),
                    HandlePreflightStartRequested);
            }
        }
        else
        {
            AddCompactScreenHeading(panel, "Start blocked", "This exercise is not ready.");
            AddPreflightRequirement(panel, "Success", preflight.Standard ?? "Not available.");
            AddPrimaryButton(panel, "Blocked", enabled: false, HandlePreflightStartRequested);
        }

        foreach (var blocker in preflight.Blockers.Take(3))
        {
            AddWarningRow(panel, "Blocked", blocker.Detail, MgColors.Blocked);
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddCompactScreenHeading(
        LinearLayout panel,
        string title,
        string detail)
    {
        var titleView = new TextView(context)
        {
            Text = title,
        };
        MgTypography.ApplyTitle(titleView);
        panel.AddView(titleView, MatchWrap());

        if (!string.IsNullOrWhiteSpace(detail))
        {
            var detailView = new TextView(context)
            {
                Text = detail,
            };
            MgTypography.ApplyLabel(detailView);
            detailView.SetTextColor(MgColors.InkMuted);
            panel.AddView(detailView, MatchWrapWithTop(MgSpacing.Xs));
        }
    }

    private CriteriaStripView FocusHoldCriteriaStrip(
        IReadOnlyList<LoadVariable>? loadVariables = null)
    {
        return new CriteriaStripView(
            context,
            [
                (FocusHoldDurationLabel(loadVariables), "hold"),
                ("5 max", "wanders"),
                ("10s", "return"),
            ]);
    }

    private static string FocusHoldDurationLabel(IReadOnlyList<LoadVariable>? loadVariables)
    {
        var value = FocusHoldDurationValue(loadVariables);

        var numberText = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!int.TryParse(numberText, out var amount))
        {
            return value;
        }

        if (value.Contains("minute", StringComparison.OrdinalIgnoreCase))
        {
            return $"{amount}:00";
        }

        return value.Contains("second", StringComparison.OrdinalIgnoreCase)
            ? $"{amount / 60}:{amount % 60:00}"
            : value;
    }

    private static string FocusHoldDurationValue(IReadOnlyList<LoadVariable>? loadVariables)
    {
        var value = loadVariables?.FirstOrDefault(variable => string.Equals(
            variable.Name,
            "duration",
            StringComparison.OrdinalIgnoreCase))?.Value;
        return string.IsNullOrWhiteSpace(value) ? "3 minutes" : value;
    }

    private void AddSetupSteps(LinearLayout panel)
    {
        var steps = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        AddSetupStep(steps, MgGlyphKind.Read, "Say", MgColors.TrainingDark);
        AddSetupStep(steps, MgGlyphKind.Hold, "Hold", MgColors.TestReady);
        AddSetupStep(steps, MgGlyphKind.Drift, "Mark + return", MgColors.PassedOnce);
        panel.AddView(steps, MatchWrapWithTop(MgSpacing.Md));
    }

    private void AddPreflightMaterial(
        LinearLayout panel,
        TrainingPresentationWorkSummary work)
    {
        if (string.IsNullOrWhiteSpace(work.Exercise.PrimaryMaterial))
        {
            return;
        }

        var material = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.TintedSurface(
                context,
                MgColors.TrainingTint,
                MgColors.HairlineSoft,
                cornerRadius: 8),
        };
        material.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md));
        var label = new TextView(context)
        {
            Text = "TASK",
        };
        MgTypography.ApplyLabel(label);
        label.SetTextColor(MgColors.TrainingDark);
        material.AddView(label, MatchWrap());
        var value = new TextView(context)
        {
            Text = work.Exercise.PrimaryMaterial,
        };
        MgTypography.ApplyHeading(value);
        material.AddView(value, MatchWrapWithTop(MgSpacing.Xs));
        foreach (var item in work.Exercise.SetupItems)
        {
            var itemView = new TextView(context)
            {
                Text = item,
            };
            MgTypography.ApplyBody(itemView);
            itemView.SetTextColor(MgColors.InkMuted);
            material.AddView(itemView, MatchWrapWithTop(MgSpacing.Sm));
        }
        panel.AddView(material, MatchWrapWithTop(MgSpacing.Md));
    }

    private void AddDrillSetupSteps(
        LinearLayout panel,
        DrillId drill)
    {
        var labels = drill switch
        {
            DrillId.FH2DistractorHold => ("Target", "Ignore", "Return"),
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter => ("Targets", "Watch", "Switch"),
            DrillId.WM1DelayedReconstruction => ("Study", "Hide", "Rebuild"),
            DrillId.WM2MentalTransform => ("Study", "Transform", "Explain"),
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => ("Rule", "Watch", "Withhold"),
            DrillId.DE1PairDiscrimination => ("Compare", "Mark guess", "Decide"),
            DrillId.DE2SeededAudit => ("Lock", "Find", "Submit"),
            DrillId.CO1RuleExtraction => ("Examples", "Rule", "Test"),
            DrillId.CO2StructureMapping => ("Roles", "Relations", "Map"),
            DrillId.AI1PressureRepeat => ("Standard", "Pressure", "Repeat"),
            DrillId.AI2DisruptionRecovery => ("Task", "Disrupt", "Resume"),
            DrillId.TI1CompositeTask => ("Parts", "Perform", "Evidence"),
            DrillId.TI2GlobalReviewTask => ("Evidence", "Audit", "Decide"),
            _ => ("Read", "Perform", "Record"),
        };

        var steps = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        AddSetupStep(steps, MgGlyphKind.Read, labels.Item1, MgColors.TrainingDark);
        AddSetupStep(steps, MgGlyphKind.Hold, labels.Item2, MgColors.TestReady);
        AddSetupStep(steps, MgGlyphKind.Check, labels.Item3, MgColors.PassedOnce);
        panel.AddView(steps, MatchWrapWithTop(MgSpacing.Md));
    }

    private void AddSetupStep(
        LinearLayout row,
        MgGlyphKind glyph,
        string label,
        Color color)
    {
        var step = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        step.SetGravity(GravityFlags.Center);
        var icon = new MgGlyphView(
            context,
            glyph,
            color,
            filled: false,
            showContainer: false)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        step.AddView(icon, new LinearLayout.LayoutParams(Dp(36), Dp(36)));
        var text = new TextView(context)
        {
            Text = label,
            Gravity = GravityFlags.Center,
        };
        MgTypography.ApplyLabel(text);
        step.AddView(text, MatchWrapWithTop(MgSpacing.Xs));
        row.AddView(step, new LinearLayout.LayoutParams(0, Dp(64), 1));
    }

    private void HandlePreflightStartRequested()
    {
        ShowLiveSessionLoading();
        LiveSessionStartRequested?.Invoke(selectedMainFailureMode);
    }

    private void AddFailureModeSelector(
        LinearLayout panel,
        TrainingPresentationWorkSummary work)
    {
        if (!RequiresFailureModeSelection(work) || work.Drill is null)
        {
            return;
        }

        var label = new TextView(context)
        {
            Text = "Guard against",
        };
        MgTypography.ApplyLabel(label);
        label.SetTextColor(MgColors.InkMuted);
        panel.AddView(label, MatchWrapWithTop(MgSpacing.Md));

        var modes = ProgramCatalog.Drills.Single(drill => drill.Id == work.Drill.Value)
            .FailureModes
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Take(3);
        foreach (var mode in modes)
        {
            var selected = string.Equals(mode, selectedMainFailureMode, StringComparison.Ordinal);
            var button = new Button(context)
            {
                Text = mode,
                ContentDescription = selected ? $"Selected: {mode}" : $"Select: {mode}",
            };
            button.SetAllCaps(false);
            button.SetSingleLine(false);
            button.SetMinHeight(Dp(44));
            button.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Xs), Dp(MgSpacing.Md), Dp(MgSpacing.Xs));
            MgTypography.ApplyBody(button);
            button.SetTextColor(selected ? Color.White : MgColors.Ink);
            button.Background = selected
                ? MgTheme.Filled(context, MgColors.TrainingDark, cornerRadius: 8)
                : MgTheme.Outline(context, MgColors.Hairline, cornerRadius: 8);
            button.Click += (_, _) =>
            {
                selectedMainFailureMode = mode;
                RenderCurrentScreen(resetScroll: false);
            };
            panel.AddView(button, MatchWrapWithTop(MgSpacing.Xs));
        }
    }

    private static bool RequiresFailureModeSelection(TrainingPresentationWorkSummary work)
    {
        return work.SessionType is
            AppTrainingSessionType.Test or
            AppTrainingSessionType.Stabilization or
            AppTrainingSessionType.Transfer;
    }

    private void AddPreflightWorkHeader(
        LinearLayout panel,
        TrainingPresentationWorkSummary work)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Background = MgTheme.TintedSurface(context, MgColors.TrainingPanel, MgColors.HairlineSoft, cornerRadius: 8),
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Dp(MgSpacing.Lg), Dp(MgSpacing.Md), Dp(MgSpacing.Lg), Dp(MgSpacing.Md));

        row.AddView(
            new MgGlyphView(context, MgGlyphKind.Hold, MgColors.TrainingDark),
            new LinearLayout.LayoutParams(Dp(52), Dp(52)));

        var stack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var stackLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        stackLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        row.AddView(stack, stackLayout);

        var title = new TextView(context)
        {
            Text = WorkTitle(work),
        };
        MgTypography.ApplyHeading(title);
        stack.AddView(title, MatchWrap());

        var marker = new TextView(context)
        {
            Text = PreflightWorkMarker(work),
            Gravity = GravityFlags.Center,
            Background = MgTheme.TintedSurface(context, MgColors.Surface, MgColors.Training, cornerRadius: 8),
        };
        marker.SetTextColor(MgColors.Ink);
        marker.SetPadding(Dp(MgSpacing.Sm), Dp(MgSpacing.Xs), Dp(MgSpacing.Sm), Dp(MgSpacing.Xs));
        MgTypography.ApplyMicro(marker);
        stack.AddView(marker, WrapWrapWithTop(MgSpacing.Xs));

        var branch = new TextView(context)
        {
            Text = work.Exercise.BranchLevelLabel,
        };
        MgTypography.ApplyLabel(branch);
        stack.AddView(branch, MatchWrapWithTop(MgSpacing.Xs));

        panel.AddView(row, MatchWrapWithTop(MgSpacing.Md));
    }

    private void AddPreflightRequirement(
        LinearLayout panel,
        string label,
        string value)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = PreflightRequirementBackground(label),
        };
        row.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md));

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);
        header.AddView(
            new MgGlyphView(context, GlyphForRequirement(label), ColorForRequirement(label), filled: false),
            new LinearLayout.LayoutParams(Dp(32), Dp(32)));

        var labelView = new TextView(context)
        {
            Text = label,
        };
        MgTypography.ApplyMicro(labelView);
        var labelLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        labelLayout.SetMargins(Dp(MgSpacing.Sm), 0, 0, 0);
        header.AddView(labelView, labelLayout);
        row.AddView(header, MatchWrap());

        var valueView = new TextView(context)
        {
            Text = value,
        };
        MgTypography.ApplyBody(valueView);
        row.AddView(valueView, MatchWrapWithTop(MgSpacing.Xs));

        panel.AddView(row, MatchWrapWithTop(MgSpacing.Sm));
    }

    private GradientDrawable PreflightRequirementBackground(string label)
    {
        var fill = label switch
        {
            "When it counts" => MgColors.TestReadyTint,
            "What gets saved" => MgColors.RecoveryTint,
            _ => MgColors.TrainingTint,
        };

        return MgTheme.TintedSurface(context, fill, MgColors.HairlineSoft, cornerRadius: 8);
    }

    private void AddLive(AndroidLiveSessionSnapshot? liveSnapshot)
    {
        if (liveSnapshot is null)
        {
            AddPanel("Exercise", "Exercise details are not available yet.", "Return to Today to continue.");
            return;
        }

        if (liveTrainingView is null)
        {
            liveTrainingView = new LiveTrainingScreenView(context);
            liveTrainingView.CommandRequested += (command, targetId, value) =>
                LiveSessionCommandRequested?.Invoke(command, targetId, value);
        }

        liveTrainingView.Update(liveSnapshot);
        content.AddView(liveTrainingView, MatchWrapWithBottom());
    }

    private void AddLivePhaseHeader(
        LinearLayout panel,
        LiveSessionPresentationReadModel presentation)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Background = MgTheme.Filled(context, MgColors.InkDeep, cornerRadius: 8),
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg));
        row.Elevation = Dp(3);

        var timer = new TimerRingView(context);
        timer.Update(presentation.Timer, presentation.LifecycleStatus);
        row.AddView(timer, new LinearLayout.LayoutParams(Dp(108), Dp(108)));

        var stack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var stackLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        stackLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        row.AddView(stack, stackLayout);

        var phase = new TextView(context)
        {
            Text = presentation.CurrentInstruction,
        };
        MgTypography.ApplyHeading(phase);
        phase.SetTextColor(Color.White);
        stack.AddView(phase, MatchWrap());

        var work = new TextView(context)
        {
            Text = LiveWorkLabel(presentation.Work),
        };
        MgTypography.ApplyLabel(work);
        work.SetTextColor(MgColors.InkSoft);
        stack.AddView(work, MatchWrapWithTop(MgSpacing.Xs));

        if (ShouldShowLifecycleStatus(presentation.LifecycleStatus))
        {
            var status = new TextView(context)
            {
                Text = LifecycleLabel(presentation.LifecycleStatus),
            };
            MgTypography.ApplyMicro(status);
            status.SetTextColor(ColorForLifecycle(presentation.LifecycleStatus));
            stack.AddView(status, MatchWrapWithTop(MgSpacing.Xs));
        }

        panel.AddView(row, MatchWrap());
    }

    private void AddLiveMaterial(
        LinearLayout panel,
        LiveSessionPresentationReadModel presentation)
    {
        var box = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.TintedSurface(context, MgColors.TrainingPanel, MgColors.HairlineSoft, cornerRadius: 8),
        };
        box.SetPadding(Dp(MgSpacing.Lg), Dp(MgSpacing.Md), Dp(MgSpacing.Lg), Dp(MgSpacing.Md));

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);
        header.AddView(
            new MgGlyphView(context, MgGlyphKind.Target, MgColors.TrainingDark, filled: false),
            new LinearLayout.LayoutParams(Dp(34), Dp(34)));

        var label = new TextView(context)
        {
            Text = LiveMaterialLabel(presentation),
        };
        MgTypography.ApplyMicro(label);
        var labelLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        labelLayout.SetMargins(Dp(MgSpacing.Sm), 0, 0, 0);
        header.AddView(label, labelLayout);
        box.AddView(header, MatchWrap());

        var text = new TextView(context)
        {
            Text = LiveMaterialText(presentation),
            Ellipsize = TextUtils.TruncateAt.End,
        };
        text.SetMaxLines(4);
        MgTypography.ApplyHeading(text);
        box.AddView(text, MatchWrapWithTop(MgSpacing.Xs));

        if (presentation.ActiveCue is { } cue)
        {
            var cueDetail = new TextView(context)
            {
                Text = cue.RequiresResponse ? $"{FormatDuration(cue.ResponseWindow)} window" : "No response",
            };
            MgTypography.ApplyMicro(cueDetail);
            box.AddView(cueDetail, MatchWrapWithTop(MgSpacing.Xs));
        }

        panel.AddView(box, MatchWrapWithTop(MgSpacing.Md));
    }

    private void AddResultOverview(
        LinearLayout panel,
        TrainingPresentationWorkSummary work,
        string outcomeText)
    {
        var box = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.TintedSurface(context, MgColors.Surface, MgColors.HairlineSoft, cornerRadius: 8),
        };
        box.SetPadding(Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg));
        box.Elevation = Dp(1);

        var identity = new TextView(context)
        {
            Text = $"{work.Exercise.ExerciseName} · {work.Exercise.BranchLevelLabel}",
        };
        MgTypography.ApplyLabel(identity);
        box.AddView(identity, MatchWrap());

        if (!string.IsNullOrWhiteSpace(work.Demand))
        {
            var demand = new TextView(context)
            {
                Text = work.Demand!,
            };
            MgTypography.ApplyBody(demand);
            demand.SetTextColor(MgColors.InkMuted);
            box.AddView(demand, MatchWrapWithTop(MgSpacing.Sm));
        }

        var outcome = new TextView(context)
        {
            Text = outcomeText,
        };
        MgTypography.ApplyBody(outcome);
        box.AddView(outcome, MatchWrapWithTop(MgSpacing.Md));

        panel.AddView(box, MatchWrapWithTop(MgSpacing.Md));
    }

    private void AddResult(AndroidLiveSessionCompletionSnapshot? completion)
    {
        if (completion is null)
        {
            AddPanel("Result", "Result is being recorded.", "Progress changes only when the program state changes.");
            return;
        }

        var result = completion.Presentation;
        var panel = FocusedPanel();
        AddCompactScreenHeading(
            panel,
            ResultTitle(result),
            $"{result.Work.Exercise.ExerciseName} · {result.Work.Exercise.BranchLevelLabel}");

        var stateBand = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Background = MgTheme.TintedSurface(
                context,
                ResultTint(result.Outcome),
                ColorForResult(result.Outcome),
                cornerRadius: 8),
        };
        stateBand.SetGravity(GravityFlags.CenterVertical);
        stateBand.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md), Dp(MgSpacing.Md));
        var resultIcon = new MgGlyphView(
            context,
            ResultGlyph(result.Outcome),
            ColorForResult(result.Outcome),
            filled: false,
            showContainer: false)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        stateBand.AddView(resultIcon, new LinearLayout.LayoutParams(Dp(40), Dp(40)));
        var stateText = new TextView(context)
        {
            Text = ResultStateText(result),
        };
        MgTypography.ApplyHeading(stateText);
        var stateTextLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        stateTextLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        stateBand.AddView(stateText, stateTextLayout);
        panel.AddView(stateBand, MatchWrapWithTop(MgSpacing.Lg));

        if (result.Work.Drill == DrillId.FH1TargetHold &&
            result.Outcome != TrainingResultPresentationOutcomeKind.Cancelled)
        {
            AddResultFocusHoldEvidence(panel, result);
        }
        else if (result.Outcome != TrainingResultPresentationOutcomeKind.Cancelled)
        {
            AddResultGeneralEvidence(panel, result);
        }

        foreach (var failure in result.BlockingFailureDetails.Take(2))
        {
            AddWarningRow(panel, "Missed", failure, MgColors.Blocked);
        }

        AddPrimaryButton(panel, ResultActionLabel(result.Outcome, liveCompletionSnapshot?.DailyTraining), enabled: true, () =>
        {
            liveCompletionSnapshot = null;
            currentScreen = Screen.Today;
            RenderCurrentScreen();
        });

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddResultFocusHoldEvidence(
        LinearLayout panel,
        ResultPresentationReadModel result)
    {
        var evidence = result.SessionEvidence;
        var maximumReturn = evidence.MaximumReturnTime.HasValue
            ? $"{Math.Ceiling(evidence.MaximumReturnTime.Value.TotalSeconds):0}s"
            : "--";
        var strip = new CriteriaStripView(
            context,
            [
                (result.RuntimeCompletionStatus == RuntimeSessionCompletionStatus.Completed
                    ? FocusHoldDurationLabel(result.Work.LoadVariables)
                    : "--", "hold"),
                ($"{evidence.DriftCount}/5", "wanders"),
                ($"{maximumReturn}/10s", "max return"),
                (evidence.TargetChangeCount.ToString(), "changes"),
            ]);
        panel.AddView(strip, MatchWrapWithTop(MgSpacing.Md));
    }

    private void AddResultGeneralEvidence(
        LinearLayout panel,
        ResultPresentationReadModel result)
    {
        var evidence = result.SessionEvidence;
        IReadOnlyList<(string Value, string Label)> criteria;
        if (result.Work.Drill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter or
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule)
        {
            criteria =
            [
                (evidence.CueCount.ToString(), "cues"),
                (evidence.CueResponseCount.ToString(), "responses"),
                (evidence.ErrorCount.ToString(), "errors"),
                (evidence.CorrectionCount.ToString(), "fixes"),
            ];
        }
        else
        {
            var dose = ResultDose(result.Work);
            criteria =
            [
                dose,
                (evidence.AnswerCount.ToString(), "submitted"),
                (evidence.GuessCount.ToString(), "guesses"),
                (evidence.ErrorCount.ToString(), "errors"),
            ];
        }

        panel.AddView(new CriteriaStripView(context, criteria), MatchWrapWithTop(MgSpacing.Md));
    }

    private static (string Value, string Label) ResultDose(TrainingPresentationWorkSummary work)
    {
        string? Load(string name) => work.LoadVariables
            .FirstOrDefault(variable => string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return work.Drill switch
        {
            DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform => (Load("item count") ?? "--", "items"),
            DrillId.DE1PairDiscrimination => (Load("item quantity") ?? "--", "comparisons"),
            DrillId.DE2SeededAudit => (Load("quantity") ?? "--", "errors set"),
            DrillId.CO1RuleExtraction => (Load("example count") ?? "--", "examples"),
            DrillId.CO2StructureMapping => (Load("relation count") ?? "--", "relations"),
            DrillId.TI1CompositeTask => (Load("number of branches") ?? "--", "branches"),
            _ => ("1", "task"),
        };
    }

    private static string ResultStateText(ResultPresentationReadModel result)
    {
        return result.Outcome switch
        {
            TrainingResultPresentationOutcomeKind.Cancelled => "No attempt recorded",
            TrainingResultPresentationOutcomeKind.CleanPractice => "Clean practice counted",
            TrainingResultPresentationOutcomeKind.TestReady => "Practice requirement met",
            TrainingResultPresentationOutcomeKind.Abandoned => "Stopped attempt saved",
            TrainingResultPresentationOutcomeKind.Failed or
            TrainingResultPresentationOutcomeKind.TimedOut => "Standard not met",
            TrainingResultPresentationOutcomeKind.PassedOnce => "First pass counted",
            TrainingResultPresentationOutcomeKind.Stabilizing => "Clean repeat counted",
            TrainingResultPresentationOutcomeKind.Owned => "Level owned",
            TrainingResultPresentationOutcomeKind.NoAdvancement => "Practice recorded",
            _ => "Result saved",
        };
    }

    private static string ResultActionLabel(
        TrainingResultPresentationOutcomeKind outcome,
        DailyTrainingWorkflowReadModel? dailyTraining)
    {
        if (dailyTraining?.Status == DailyTrainingWorkflowStatus.BetweenBlocks)
        {
            return "Next block";
        }

        if (dailyTraining?.IsTerminal == true)
        {
            return "Done";
        }

        return outcome switch
        {
            TrainingResultPresentationOutcomeKind.Failed or
            TrainingResultPresentationOutcomeKind.TimedOut or
            TrainingResultPresentationOutcomeKind.Abandoned => "Return to training",
            TrainingResultPresentationOutcomeKind.Cancelled => "Back to Train",
            _ => "Continue",
        };
    }

    private static MgGlyphKind ResultGlyph(TrainingResultPresentationOutcomeKind outcome)
    {
        return outcome switch
        {
            TrainingResultPresentationOutcomeKind.CleanPractice or
            TrainingResultPresentationOutcomeKind.TestReady or
            TrainingResultPresentationOutcomeKind.PassedOnce or
            TrainingResultPresentationOutcomeKind.Stabilizing or
            TrainingResultPresentationOutcomeKind.Owned => MgGlyphKind.Check,
            TrainingResultPresentationOutcomeKind.Failed or
            TrainingResultPresentationOutcomeKind.TimedOut => MgGlyphKind.Alert,
            TrainingResultPresentationOutcomeKind.Abandoned or
            TrainingResultPresentationOutcomeKind.Cancelled => MgGlyphKind.Stop,
            _ => MgGlyphKind.Record,
        };
    }

    private static Color ResultTint(TrainingResultPresentationOutcomeKind outcome)
    {
        return outcome switch
        {
            TrainingResultPresentationOutcomeKind.CleanPractice or
            TrainingResultPresentationOutcomeKind.TestReady or
            TrainingResultPresentationOutcomeKind.PassedOnce or
            TrainingResultPresentationOutcomeKind.Stabilizing or
            TrainingResultPresentationOutcomeKind.Owned => MgColors.TrainingTint,
            TrainingResultPresentationOutcomeKind.Failed or
            TrainingResultPresentationOutcomeKind.TimedOut => MgColors.BlockedTint,
            _ => MgColors.RecoveryTint,
        };
    }

    private void AddLiveInputIfNeeded(
        LinearLayout panel,
        LiveSessionPresentationReadModel presentation)
    {
        var needsInput = presentation.AvailableCommands.Any(command =>
            IsCommandAvailable(presentation, command.Command) &&
            command.Command is RuntimeInputCommandKind.RespondToCue
                or RuntimeInputCommandKind.SubmitAnswer
                or RuntimeInputCommandKind.MarkError
                or RuntimeInputCommandKind.Correct);
        if (!needsInput)
        {
            return;
        }

        var input = new EditText(context)
        {
            Hint = "Your answer",
            Text = liveSessionInput,
        };
        input.SetSingleLine(false);
        input.SetMinLines(1);
        input.SetMaxLines(2);
        input.TextChanged += (_, args) => liveSessionInput = args.Text?.ToString() ?? string.Empty;
        panel.AddView(input, MatchWrapWithTop(MgSpacing.Sm));
    }

    private void AddLivePrimaryCommand(
        LinearLayout panel,
        PreUiLiveSessionState live,
        LiveSessionPresentationReadModel presentation)
    {
        if (presentation.PrimaryCommand is null)
        {
            AddPrimaryButton(panel, LiveUnavailableActionLabel(live.LifecycleStatus), enabled: false, () => { });
            return;
        }

        var command = presentation.PrimaryCommand.Command;
        AddPrimaryButton(panel, CommandLabel(command, primary: true, presentation.CurrentPhaseKind), enabled: true, () =>
            LiveSessionCommandRequested?.Invoke(command, TargetFor(live, command), ValueFor(command)));
    }

    private void AddLiveFixedCommandRows(
        LinearLayout panel,
        PreUiLiveSessionState live,
        LiveSessionPresentationReadModel presentation)
    {
        AddLiveCommandRow(
            panel,
            live,
            presentation,
            [
                RuntimeInputCommandKind.MarkDrift,
                RuntimeInputCommandKind.MarkGuess,
                RuntimeInputCommandKind.MarkError,
                RuntimeInputCommandKind.Correct,
            ]);

        AddLiveCommandRow(
            panel,
            live,
            presentation,
            [
                RuntimeInputCommandKind.FinishPhase,
                RuntimeInputCommandKind.StartAudit,
                RuntimeInputCommandKind.Pause,
                RuntimeInputCommandKind.Resume,
            ]);

        AddLiveCommandRow(
            panel,
            live,
            presentation,
            [
                RuntimeInputCommandKind.Abandon,
            ]);
    }

    private void AddLiveCommandRow(
        LinearLayout panel,
        PreUiLiveSessionState live,
        LiveSessionPresentationReadModel presentation,
        IReadOnlyList<RuntimeInputCommandKind> commands)
    {
        var primary = presentation.PrimaryCommand?.Command;
        var visibleCommands = commands
            .Where(command => IsCommandAvailable(presentation, command) && command != primary)
            .ToArray();
        if (visibleCommands.Length == 0)
        {
            return;
        }

        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Xs), 0, 0);

        for (var index = 0; index < visibleCommands.Length; index++)
        {
            var command = visibleCommands[index];
            var button = LiveCommandButton(
                CommandLabel(command, primary: false, presentation.CurrentPhaseKind),
                enabled: true,
                command == RuntimeInputCommandKind.Abandon);
            button.Click += (_, _) =>
            {
                LiveSessionCommandRequested?.Invoke(command, TargetFor(live, command), ValueFor(command));
            };

            var layout = new LinearLayout.LayoutParams(0, Dp(40), 1);
            if (index < visibleCommands.Length - 1)
            {
                layout.SetMargins(0, 0, Dp(MgSpacing.Sm), 0);
            }

            row.AddView(button, layout);
        }

        panel.AddView(row, MatchWrap());
    }

    private void AddLiveSecondaryCommands(
        LinearLayout panel,
        PreUiLiveSessionState live,
        LiveSessionPresentationReadModel presentation)
    {
        var commands = presentation.AvailableCommands
            .Where(command => presentation.PrimaryCommand is null || command.Command != presentation.PrimaryCommand.Command)
            .Where(command => command.Command is not RuntimeInputCommandKind.RespondToCue)
            .Take(6)
            .ToArray();
        if (commands.Length == 0)
        {
            return;
        }

        var grid = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        for (var index = 0; index < commands.Length; index += 2)
        {
            var row = new LinearLayout(context)
            {
                Orientation = Orientation.Horizontal,
            };
            row.SetPadding(0, Dp(MgSpacing.Xs), 0, 0);

            foreach (var item in commands.Skip(index).Take(2))
            {
                var command = item.Command;
                var button = SecondaryButton(CommandLabel(command, primary: false, presentation.CurrentPhaseKind));
                button.Click += (_, _) => LiveSessionCommandRequested?.Invoke(
                    command,
                    TargetFor(live, command),
                    ValueFor(command));
                row.AddView(button, new LinearLayout.LayoutParams(0, Dp(42), 1));
            }

            grid.AddView(row, MatchWrap());
        }

        panel.AddView(grid, MatchWrapWithTop());
    }

    private void AddLiveEvidence(
        LinearLayout panel,
        LiveSessionPresentationReadModel presentation)
    {
        var evidence = presentation.Evidence;
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Sm), 0, 0);
        if (presentation.Work.Drill == DrillId.FH1TargetHold)
        {
            AddMetricBox(row, "Wanders", evidence.DriftCount.ToString());
            AddMetricBox(
                row,
                "Saved",
                $"{Math.Min(evidence.EvidenceFactCount, evidence.ExpectedEvidenceFactCount)}/{evidence.ExpectedEvidenceFactCount}",
                last: true);
        }
        else
        {
            AddMetricBox(row, "Drifts", evidence.DriftCount.ToString());
            AddMetricBox(row, "Guesses", evidence.GuessCount.ToString());
            AddMetricBox(row, "Errors", evidence.ErrorCount.ToString());
            AddMetricBox(row, "Fixes", evidence.CorrectionCount.ToString());
            AddMetricBox(
                row,
                "Recorded",
                $"{Math.Min(evidence.EvidenceFactCount, evidence.ExpectedEvidenceFactCount)}/{evidence.ExpectedEvidenceFactCount}",
                last: true);
        }

        panel.AddView(row, MatchWrap());
    }

    private void AddMetricBox(LinearLayout row, string label, string value, bool last = false)
    {
        var layout = new LinearLayout.LayoutParams(0, Dp(48), 1);
        if (!last)
        {
            layout.SetMargins(0, 0, Dp(MgSpacing.Xs), 0);
        }

        row.AddView(MetricBox(label, value), layout);
    }

    private void AddWorkIdentity(LinearLayout panel, TrainingPresentationWorkSummary work)
    {
        var role = WorkRoleLabel(work);
        var identityParts = new List<string>
        {
            work.Exercise.ExerciseName,
            work.Exercise.BranchLevelLabel,
        };

        if (!string.Equals(role, "Practice", StringComparison.Ordinal))
        {
            identityParts.Add(role);
        }

        AddBody(panel, string.Join(" · ", identityParts));

        if (!string.IsNullOrWhiteSpace(work.Demand))
        {
            AddMuted(panel, work.Demand!);
        }
    }

    private void AddBranchLadder(
        LinearLayout panel,
        CurrentTrainingStateReadModel state,
        CurrentTrainingPresentationReadModel presentation)
    {
        foreach (var branch in ProgramCatalog.Branches.Select(branch => branch.Code))
        {
            var statuses = state.BranchLevelStates
                .Where(status => status.Branch == branch)
                .OrderBy(status => status.Level)
                .ToArray();
            if (statuses.Length == 0)
            {
                continue;
            }

            AddBranchLadderRow(panel, branch, statuses, state, presentation);
        }
    }

    private void AddBranchLadderRow(
        LinearLayout panel,
        BranchCode branch,
        IReadOnlyList<BranchLevelStatus> statuses,
        CurrentTrainingStateReadModel state,
        CurrentTrainingPresentationReadModel presentation)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Md), 0, 0);

        row.AddView(Label(branch.ToString(), minWidthDp: 40), WrapWrap());

        var track = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        track.SetGravity(GravityFlags.CenterVertical);

        for (var index = 0; index < statuses.Count; index++)
        {
            var status = statuses[index];
            if (index > 0)
            {
                track.AddView(RailSegment(RailColorFor(statuses[index - 1], status, state), RailInterrupted(statuses[index - 1], status, state)));
            }

            var blocked = IsBlockedAtLevel(state, status);
            var due = IsDueMaintenance(state, status);
            var next = IsPrimaryWorkLevel(presentation, status);
            track.AddView(new LevelCellView(context, status, blocked, due, next), WrapWrap());
        }

        if (HasTransferWork(state, branch))
        {
            track.AddView(RailSegment(MgColors.Transfer, interrupted: false));
            track.AddView(MarkerPill("TR", MgColors.Transfer, filled: false), new LinearLayout.LayoutParams(Dp(42), Dp(36)));
        }

        row.AddView(track, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        panel.AddView(row, MatchWrap());
    }

    private View RailSegment(Color color, bool interrupted)
    {
        var rail = new View(context)
        {
            Background = MgTheme.Filled(context, color, cornerRadius: interrupted ? 2 : 1),
        };
        var layout = new LinearLayout.LayoutParams(Dp(interrupted ? 12 : 18), Dp(interrupted ? 6 : 3));
        layout.SetMargins(Dp(2), 0, Dp(2), 0);
        rail.LayoutParameters = layout;
        return rail;
    }

    private void AddEvidenceSignalStrip(LinearLayout panel, TrainingEvidencePresentationSummary summary)
    {
        var signals = new List<VisualSignal>
        {
            new(
                "C",
                "Clean",
                $"{Math.Min(summary.RecentCleanSessionCount, summary.RecentSessionCount)}/{summary.RecentSessionCount}",
                summary.HasFailureEvidence ? MgColors.Blocked : MgColors.Owned,
                Filled: !summary.HasFailureEvidence && summary.RecentSessionCount > 0),
            new("E", "Records", summary.EvidenceArtifactCount.ToString(), MgColors.Training, Filled: false),
        };

        if (summary.FormalAttemptCount > 0)
        {
            signals.Add(new("T", "Tests", summary.FormalAttemptCount.ToString(), MgColors.TestReady, Filled: false));
        }

        if (summary.StabilizationPassCount > 0)
        {
            signals.Add(new("S", "Stabilize", summary.StabilizationPassCount.ToString(), MgColors.TestReady, Filled: false));
        }

        if (summary.MaintenanceCheckCount > 0)
        {
            signals.Add(new("M", "Maintain", summary.MaintenanceCheckCount.ToString(), MgColors.Maintenance, Filled: false));
        }

        AddSignalStrip(panel, signals);
    }

    private void AddEvidenceLedgerSummary(
        LinearLayout panel,
        TrainingEvidencePresentationSummary summary)
    {
        if (summary.RecentSessionCount > 0)
        {
            var detail = summary.HasFailureEvidence
                ? $"{summary.RecentCleanSessionCount} clean of {summary.RecentSessionCount}; failure records stay visible."
                : $"{summary.RecentCleanSessionCount} clean of {summary.RecentSessionCount}; no failure marker in recent records.";
            AddInspectionRow(
                panel,
                summary.HasFailureEvidence ? "F" : "C",
                "Recent session quality",
                detail,
                summary.HasFailureEvidence ? MgColors.Blocked : MgColors.Owned,
                filled: summary.HasFailureEvidence);
        }

        if (summary.LatestEvidenceCategory.HasValue)
        {
            var target = summary.LatestBranch.HasValue && summary.LatestLevel.HasValue
                ? $"{summary.LatestBranch.Value} {LevelCode(summary.LatestLevel.Value)}"
                : "Program";
            AddInspectionRow(
                panel,
                EvidenceMarker(summary.LatestEvidenceCategory.Value),
                "Latest artifact",
                $"{EvidenceCategoryLabel(summary.LatestEvidenceCategory.Value)} · {target}",
                ColorForEvidenceCategory(summary.LatestEvidenceCategory.Value));
        }

        if (!summary.HasObservableEvidence && summary.RecentSessionCount == 0)
        {
            AddInspectionRow(panel, "E", "Practice log", "No local practice records yet.", MgColors.Hairline);
        }
    }

    private void AddProgressSignalStrip(
        LinearLayout panel,
        CurrentTrainingStateReadModel state,
        bool includeZeros)
    {
        var blockedCount = state.BlockedAdvancement.Count;
        var decayedCount = state.BranchLevelStates.Count(status => status.State == BranchLevelState.Decayed);
        var dueCount = state.DueMaintenance.Count(record => record.Currency.State != MaintenanceCurrencyState.Current);
        var passedOnceCount = state.BranchLevelStates.Count(status => status.State == BranchLevelState.PassedOnce);
        var stabilizingCount = state.BranchLevelStates.Count(status => status.State == BranchLevelState.Stabilizing);
        var ownedCount = state.BranchLevelStates.Count(status => status.State == BranchLevelState.Owned);
        var transferCount = state.AvailableNextWork.Count(IsTransferWork);

        var signals = new List<VisualSignal>();
        AddStateSignal(signals, includeZeros, "B", "Blocked", blockedCount, MgColors.Blocked, blockedCount > 0);
        AddStateSignal(signals, includeZeros, "D", "Decayed", decayedCount, MgColors.Blocked, decayedCount > 0);
        AddStateSignal(signals, includeZeros, "M", "Due", dueCount, MgColors.Maintenance, dueCount > 0);
        AddStateSignal(signals, includeZeros, "1", "Pass once", passedOnceCount, MgColors.PassedOnce, false);
        AddStateSignal(signals, includeZeros, "S", "Stabilize", stabilizingCount, MgColors.TestReady, false);
        AddStateSignal(signals, includeZeros, "O", "Owned", ownedCount, MgColors.Owned, ownedCount > 0);
        AddStateSignal(signals, includeZeros, "TR", "Transfer", transferCount, MgColors.Transfer, false);

        AddSignalStrip(panel, signals);
    }

    private static void AddStateSignal(
        ICollection<VisualSignal> signals,
        bool includeZeros,
        string marker,
        string label,
        int value,
        Color color,
        bool filled)
    {
        if (includeZeros || value > 0)
        {
            signals.Add(new VisualSignal(marker, label, value.ToString(), color, filled));
        }
    }

    private void AddSignalStrip(LinearLayout panel, IReadOnlyList<VisualSignal> signals)
    {
        if (signals.Count == 0)
        {
            return;
        }

        for (var index = 0; index < signals.Count; index += 3)
        {
            var row = new LinearLayout(context)
            {
                Orientation = Orientation.Horizontal,
            };
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(0, Dp(MgSpacing.Md), 0, 0);

            foreach (var signal in signals.Skip(index).Take(3))
            {
                var layout = new LinearLayout.LayoutParams(0, Dp(54), 1);
                layout.SetMargins(Dp(2), 0, Dp(2), 0);
                row.AddView(SignalBox(signal), layout);
            }

            panel.AddView(row, MatchWrap());
        }
    }

    private TextView SignalBox(VisualSignal signal)
    {
        var box = new TextView(context)
        {
            Text = $"{signal.Marker} {signal.Value}\n{signal.Label}",
            Gravity = GravityFlags.Center,
            Background = signal.Filled
                ? MgTheme.Filled(context, signal.Color, cornerRadius: 8)
                : MgTheme.Outline(context, signal.Color, cornerRadius: 8),
        };
        MgTypography.ApplyMicro(box);
        box.SetTextColor(signal.Filled ? MgColors.ReadableTextOn(signal.Color) : MgColors.Ink);
        return box;
    }

    private void AddTransferInspection(LinearLayout panel, CurrentTrainingStateReadModel state)
    {
        var transferWork = state.AvailableNextWork
            .Where(IsTransferWork)
            .Take(2)
            .ToArray();
        if (transferWork.Length == 0)
        {
            return;
        }

        foreach (var work in transferWork)
        {
            var branch = work.BranchEmphasis.Count == 0
                ? "Program"
                : string.Join(" / ", work.BranchEmphasis);
            AddInspectionRow(panel, "TR", "Transfer", $"{WeeklySessionLabel(work.Session)} · {branch}", MgColors.Transfer);
        }
    }

    private void AddMaintenanceDecayInspection(
        CurrentTrainingStateReadModel state,
        TrainingMaintenanceDecayPriority? priority)
    {
        var rows = BuildMaintenanceDecayRows(state, priority).Take(5).ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        var panel = Panel();
        var first = rows[0];
        AddMarkerHeader(panel, "Maintenance / decay", first.Marker, first.Color);
        foreach (var row in rows)
        {
            AddInspectionRow(panel, row.Marker, row.Label, row.Detail, row.Color, row.Filled);
        }

        var total = BuildMaintenanceDecayRows(state, priority).Count;
        if (total > rows.Length)
        {
            AddMuted(panel, $"{total - rows.Length} more hidden until requested.");
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddMaintenanceDecaySummary(LinearLayout panel, CurrentTrainingStateReadModel state)
    {
        var decayedCount = state.BranchLevelStates.Count(status => status.State == BranchLevelState.Decayed);
        var dueCount = state.DueMaintenance.Count(record => record.Currency.State is MaintenanceCurrencyState.Due or MaintenanceCurrencyState.Warning);
        var failedCount = state.DueMaintenance.Count(record => record.Currency.State == MaintenanceCurrencyState.Failed);
        if (decayedCount == 0 && dueCount == 0 && failedCount == 0)
        {
            return;
        }

        var parts = new List<string>();
        if (decayedCount > 0)
        {
            parts.Add($"{decayedCount} decayed");
        }

        if (failedCount > 0)
        {
            parts.Add($"{failedCount} failed");
        }

        if (dueCount > 0)
        {
            parts.Add($"{dueCount} due");
        }

        AddInspectionRow(
            panel,
            decayedCount > 0 ? "D" : "M",
            "Maintenance / decay",
            string.Join(" · ", parts),
            decayedCount > 0 || failedCount > 0 ? MgColors.Blocked : MgColors.Maintenance,
            filled: decayedCount > 0 || failedCount > 0);
    }

    private void AddBlockedAdvancementInspection(
        CurrentTrainingStateReadModel state,
        TrainingPresentationBlockerSummary? urgentBlocker)
    {
        if (urgentBlocker is null && state.BlockedAdvancement.Count == 0)
        {
            return;
        }

        var panel = Panel();
        AddMarkerHeader(panel, "Blocked advancement", "B", MgColors.Blocked);
        if (urgentBlocker is { } urgent)
        {
            AddInspectionRow(
                panel,
                "B",
                BlockerTargetLabel(urgent),
                BlockerKindLabel(urgent.Kind),
                MgColors.Blocked,
                filled: true);
        }

        var displayedBlockerCount = urgentBlocker is null ? 3 : 2;
        foreach (var blocker in state.BlockedAdvancement.Take(displayedBlockerCount))
        {
            AddInspectionRow(
                panel,
                "B",
                CurrentBlockerTargetLabel(blocker),
                CurrentBlockerSourceLabel(blocker.Source),
                MgColors.Blocked,
                filled: true);
        }

        if (state.BlockedAdvancement.Count > displayedBlockerCount)
        {
            AddMuted(panel, $"{state.BlockedAdvancement.Count - displayedBlockerCount} more blockers hidden until requested.");
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddProgrammedEmphasis(LinearLayout panel, CurrentTrainingStateReadModel state)
    {
        if (state.ProgressRecords.LatestSummary is { } summary)
        {
            AddInspectionRow(
                panel,
                ">",
                "Progress",
                ProgressEmphasisLabel(summary.NextProgrammedEmphasis),
                ColorForProgressEmphasis(summary.NextProgrammedEmphasis),
                filled: false);
            return;
        }

        if (state.AvailableNextWork.FirstOrDefault() is { } next)
        {
            var branch = next.BranchEmphasis.Count == 0
                ? "Program"
                : string.Join(" / ", next.BranchEmphasis.Select(BranchLabel));
            AddInspectionRow(
                panel,
                ">",
                "Progress",
                $"{WeeklySessionLabel(next.Session)} · {branch}",
                MgColors.Training,
                filled: false);
        }
    }

    private void AddReviewBranchStateBoard(LinearLayout panel, CurrentTrainingStateReadModel state)
    {
        var signals = ProgramCatalog.Branches
            .Select(branch => ReviewSignalForBranch(state, branch.Code))
            .Where(signal => signal.HasValue)
            .Select(signal => signal!.Value)
            .ToArray();
        if (signals.Length == 0)
        {
            return;
        }

        for (var index = 0; index < signals.Length; index += 4)
        {
            var row = new LinearLayout(context)
            {
                Orientation = Orientation.Horizontal,
            };
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(0, Dp(MgSpacing.Md), 0, 0);

            foreach (var signal in signals.Skip(index).Take(4))
            {
                var layout = new LinearLayout.LayoutParams(0, Dp(58), 1);
                layout.SetMargins(Dp(2), 0, Dp(2), 0);
                row.AddView(ReviewBranchCell(signal), layout);
            }

            panel.AddView(row, MatchWrap());
        }
    }

    private BranchReviewSignal? ReviewSignalForBranch(CurrentTrainingStateReadModel state, BranchCode branch)
    {
        var statuses = state.BranchLevelStates
            .Where(status => status.Branch == branch)
            .OrderBy(status => ReviewStatusRank(status, state))
            .ThenByDescending(status => LevelRank(status.Level))
            .ToArray();
        if (statuses.Length == 0)
        {
            return null;
        }

        var selected = statuses[0];
        var due = IsDueMaintenance(state, selected);
        var blocked = IsBlockedAtLevel(state, selected);
        var label = due
            ? "Due"
            : blocked
                ? "Blocked"
                : StateLabel(selected.State);
        var color = due
            ? MgColors.Maintenance
            : blocked
                ? MgColors.Blocked
                : ColorForBranchState(selected.State);
        var filled = selected.State is BranchLevelState.Decayed or BranchLevelState.Owned ||
            blocked;

        return new BranchReviewSignal(
            selected.Branch,
            LevelCode(selected.Level),
            label,
            color,
            filled);
    }

    private TextView ReviewBranchCell(BranchReviewSignal signal)
    {
        var cell = new TextView(context)
        {
            Text = $"{signal.Branch}\n{signal.Level} · {signal.State}",
            Gravity = GravityFlags.Center,
            Background = signal.Filled
                ? MgTheme.Filled(context, signal.Color, cornerRadius: 8)
                : MgTheme.Outline(context, signal.Color, cornerRadius: 8),
        };
        MgTypography.ApplyMicro(cell);
        cell.SetTextColor(signal.Filled ? MgColors.ReadableTextOn(signal.Color) : MgColors.Ink);
        return cell;
    }

    private void AddReviewProgrammedResponse(LinearLayout panel, CurrentTrainingStateReadModel state)
    {
        if (state.ProgressRecords.LatestSummary is { } summary)
        {
            AddInspectionRow(
                panel,
                ">",
                "Programmed response",
                ProgressEmphasisLabel(summary.NextProgrammedEmphasis),
                ColorForProgressEmphasis(summary.NextProgrammedEmphasis),
                filled: false);
            return;
        }

        if (state.AvailableNextWork.FirstOrDefault() is { } next)
        {
            var branch = next.BranchEmphasis.Count == 0
                ? "Program"
                : string.Join(" / ", next.BranchEmphasis);
            AddInspectionRow(
                panel,
                ">",
                "Programmed response",
                $"{WeeklySessionLabel(next.Session)} · {branch}",
                MgColors.Training,
                filled: false);
        }
    }

    private void AddEvidenceItem(LinearLayout panel, LocalEvidenceArtifactRecord artifact)
    {
        var branch = artifact.Event.Branch.HasValue && artifact.Event.Level.HasValue
            ? FormatBranchLevel(artifact.Event.Branch.Value, artifact.Event.Level.Value)
            : "Program";
        var detail = $"{FormatDate(artifact.Artifact.Date)} · {branch} · {artifact.Artifact.ObservableEvidence.Count} observable";
        AddInspectionRow(
            panel,
            EvidenceMarker(artifact.Artifact.Category),
            EvidenceCategoryLabel(artifact.Artifact.Category),
            detail,
            ColorForEvidenceCategory(artifact.Artifact.Category),
            filled: artifact.Artifact.Category == EvidenceArtifactCategory.GlobalReview);
    }

    private void AddPanel(string title, string body, string detail)
    {
        var panel = Panel();
        AddSectionTitle(panel, title);
        AddBody(panel, body);
        AddMuted(panel, detail);
        content.AddView(panel, MatchWrapWithBottom());
    }

    private LinearLayout Panel()
    {
        var panel = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.Surface(context, cornerRadius: 8),
        };
        panel.SetPadding(Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg));
        panel.Elevation = Dp(2);
        return panel;
    }

    private LinearLayout FocusedPanel()
    {
        var panel = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        panel.SetPadding(0, 0, 0, 0);
        return panel;
    }

    private void AddMarkerHeader(LinearLayout panel, string title, string marker, Color color)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Background = MgTheme.Filled(context, MgColors.InkDeep, cornerRadius: 8),
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg));
        row.Elevation = Dp(3);

        row.AddView(
            new MgGlyphView(context, GlyphForMarker(marker, title), color),
            new LinearLayout.LayoutParams(Dp(56), Dp(56)));

        var titleStack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var titleLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        titleLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        row.AddView(titleStack, titleLayout);

        var titleView = new TextView(context)
        {
            Text = title,
        };
        MgTypography.ApplyHeading(titleView);
        titleView.SetTextColor(Color.White);
        titleStack.AddView(titleView, MatchWrap());

        var markerLabel = MarkerChipLabel(marker);
        if (!string.IsNullOrWhiteSpace(markerLabel))
        {
            var markerView = new TextView(context)
            {
                Text = markerLabel,
                Gravity = GravityFlags.Center,
                Background = MgTheme.TintedSurface(context, Color.Argb(42, 255, 255, 255), color, cornerRadius: 8),
            };
            MgTypography.ApplyMicro(markerView);
            markerView.SetTextColor(Color.White);
            markerView.SetPadding(Dp(MgSpacing.Sm), Dp(MgSpacing.Xs), Dp(MgSpacing.Sm), Dp(MgSpacing.Xs));
            titleStack.AddView(markerView, WrapWrapWithTop(MgSpacing.Xs));
        }

        panel.AddView(row, MatchWrap());
    }

    private void AddSectionTitle(LinearLayout panel, string title)
    {
        var text = new TextView(context)
        {
            Text = title,
        };
        MgTypography.ApplyHeading(text);
        panel.AddView(text, MatchWrap());
    }

    private void AddBody(LinearLayout panel, string text)
    {
        var view = new TextView(context)
        {
            Text = text,
        };
        MgTypography.ApplyBody(view);
        panel.AddView(view, MatchWrapWithTop(MgSpacing.Sm));
    }

    private void AddMuted(LinearLayout panel, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var view = new TextView(context)
        {
            Text = text,
        };
        MgTypography.ApplyBody(view);
        view.SetTextColor(MgColors.InkMuted);
        panel.AddView(view, MatchWrapWithTop(MgSpacing.Xs));
    }

    private void AddWarningRow(LinearLayout panel, string title, string detail, Color color)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.TintedSurface(context, SignalTint(color), color, cornerRadius: 8),
        };
        row.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Sm), Dp(MgSpacing.Md), Dp(MgSpacing.Sm));
        AddSectionTitle(row, title);
        AddMuted(row, detail);
        panel.AddView(row, MatchWrapWithTop());
    }

    private static Color SignalTint(Color color)
    {
        if (color == MgColors.Blocked)
        {
            return MgColors.BlockedTint;
        }

        if (color == MgColors.Recovery || color == MgColors.Maintenance)
        {
            return MgColors.RecoveryTint;
        }

        return MgColors.TrainingTint;
    }

    private void AddInspectionRow(
        LinearLayout panel,
        string marker,
        string label,
        string? detail,
        Color color,
        bool filled = false)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Background = MgTheme.TintedSurface(context, MgColors.SurfaceMuted, MgColors.HairlineSoft, cornerRadius: 8),
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Dp(MgSpacing.Sm), Dp(MgSpacing.Sm), Dp(MgSpacing.Sm), Dp(MgSpacing.Sm));

        row.AddView(
            new MgGlyphView(context, GlyphForMarker(marker, label), color, filled || IsMarkerless(marker)),
            new LinearLayout.LayoutParams(Dp(44), Dp(44)));

        var textStack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var textLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        textLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);

        var markerLabel = MarkerChipLabel(marker);
        if (!string.IsNullOrWhiteSpace(markerLabel))
        {
            var markerView = new TextView(context)
            {
                Text = markerLabel,
                Gravity = GravityFlags.Center,
                Background = MgTheme.TintedSurface(context, MgColors.Surface, color, cornerRadius: 8),
            };
            MgTypography.ApplyMicro(markerView);
            markerView.SetTextColor(MgColors.Ink);
            markerView.SetPadding(Dp(MgSpacing.Sm), Dp(MgSpacing.Xs), Dp(MgSpacing.Sm), Dp(MgSpacing.Xs));
            textStack.AddView(markerView, WrapWrap());
        }

        var labelView = new TextView(context)
        {
            Text = label,
        };
        MgTypography.ApplyBody(labelView);
        labelView.SetTextColor(MgColors.Ink);
        textStack.AddView(labelView, MatchWrapWithTop(string.IsNullOrWhiteSpace(markerLabel) ? 0 : MgSpacing.Xs));

        if (!string.IsNullOrWhiteSpace(detail))
        {
            var detailView = new TextView(context)
            {
                Text = detail,
            };
            MgTypography.ApplyMicro(detailView);
            detailView.SetTextColor(MgColors.InkMuted);
            textStack.AddView(detailView, MatchWrap());
        }

        row.AddView(textStack, textLayout);
        panel.AddView(row, MatchWrapWithTop(MgSpacing.Sm));
    }

    private TextView MarkerPill(string marker, Color color, bool filled)
    {
        var view = new TextView(context)
        {
            Text = marker,
            Gravity = GravityFlags.Center,
            Background = filled
                ? MgTheme.Filled(context, color, cornerRadius: 8)
                : MgTheme.Outline(context, color, cornerRadius: 8),
        };
        MgTypography.ApplyLabel(view);
        view.SetTextColor(filled ? Color.White : MgColors.Ink);
        return view;
    }

    private static bool IsMarkerless(string marker)
    {
        return string.IsNullOrWhiteSpace(marker) ||
            string.Equals(marker, "-", StringComparison.Ordinal) ||
            string.Equals(marker, "!", StringComparison.Ordinal);
    }

    private static string MarkerChipLabel(string marker)
    {
        return IsMarkerless(marker) ? string.Empty : marker;
    }

    private static MgGlyphKind GlyphForMarker(string marker, string label)
    {
        if (string.Equals(marker, "Start", StringComparison.OrdinalIgnoreCase))
        {
            return MgGlyphKind.Start;
        }

        if (string.Equals(marker, "Read", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Before", StringComparison.OrdinalIgnoreCase))
        {
            return MgGlyphKind.Read;
        }

        if (string.Equals(marker, "Hold", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Target Hold", StringComparison.OrdinalIgnoreCase))
        {
            return MgGlyphKind.Hold;
        }

        if (label.Contains("Recorded", StringComparison.OrdinalIgnoreCase))
        {
            return MgGlyphKind.Record;
        }

        if (label.Contains("Today", StringComparison.OrdinalIgnoreCase))
        {
            return MgGlyphKind.Today;
        }

        if (label.Contains("Next", StringComparison.OrdinalIgnoreCase))
        {
            return MgGlyphKind.Next;
        }

        if (label.Contains("Stopped", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Stop", StringComparison.OrdinalIgnoreCase))
        {
            return MgGlyphKind.Stop;
        }

        if (label.Contains("Success", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Complete", StringComparison.OrdinalIgnoreCase))
        {
            return MgGlyphKind.Check;
        }

        if (string.Equals(marker, "!", StringComparison.Ordinal) ||
            label.Contains("Block", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Failed", StringComparison.OrdinalIgnoreCase))
        {
            return MgGlyphKind.Alert;
        }

        return MgGlyphKind.Target;
    }

    private static MgGlyphKind GlyphForRequirement(string label)
    {
        return label switch
        {
            "Steps" => MgGlyphKind.Read,
            "When it counts" or "Success" => MgGlyphKind.Check,
            "What gets saved" => MgGlyphKind.Record,
            "Keep it honest" => MgGlyphKind.Alert,
            _ => MgGlyphKind.Target,
        };
    }

    private static Color ColorForRequirement(string label)
    {
        return label switch
        {
            "When it counts" or "Success" => MgColors.TestReady,
            "What gets saved" => MgColors.Recovery,
            "Keep it honest" => MgColors.Blocked,
            _ => MgColors.Training,
        };
    }

    private void AddFocusedBlock(LinearLayout panel, string title, string text)
    {
        var block = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.TintedSurface(context, MgColors.SurfaceMuted, MgColors.HairlineSoft, cornerRadius: 8),
        };
        block.SetPadding(Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg), Dp(MgSpacing.Lg));
        AddSectionTitle(block, title);
        AddBody(block, text);
        panel.AddView(block, MatchWrapWithTop());
    }

    private TextView MetricBox(string label, string value)
    {
        var box = new TextView(context)
        {
            Text = $"{value}\n{label}",
            Gravity = GravityFlags.Center,
            Background = MgTheme.TintedSurface(context, MgColors.SurfaceStrong, MgColors.HairlineSoft, cornerRadius: 8),
        };
        MgTypography.ApplyLabel(box);
        box.SetTextColor(MgColors.Ink);
        return box;
    }

    private void AddPrimaryButton(LinearLayout panel, string label, bool enabled, Action action)
    {
        var button = new SessionActionButton(context, label, enabled);
        button.Click += (_, _) =>
        {
            if (enabled)
            {
                action();
            }
        };
        panel.AddView(button, MatchWrapWithTop());
    }

    private void AddPrimaryButton(string label, bool enabled, Action action)
    {
        var panel = Panel();
        AddPrimaryButton(panel, label, enabled, action);
        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddUtilityButton(
        LinearLayout panel,
        string label,
        Action action,
        bool destructive = false,
        bool inline = false)
    {
        var button = SecondaryButton(label);
        MgTypography.ApplyLabel(button);
        button.SetTextColor(MgColors.Ink);
        if (destructive)
        {
            button.SetTextColor(MgColors.Blocked);
            button.Background = MgTheme.Outline(context, MgColors.Blocked, cornerRadius: 8);
        }

        button.Click += (_, _) => action();
        if (inline)
        {
            var inlineLayout = new LinearLayout.LayoutParams(0, Dp(34), 1);
            inlineLayout.SetMargins(Dp(3), 0, Dp(3), 0);
            panel.AddView(button, inlineLayout);
            return;
        }

        var layout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            Dp(38));
        layout.SetMargins(0, Dp(MgSpacing.Sm), 0, 0);
        panel.AddView(button, layout);
    }

    private Button SecondaryButton(string label)
    {
        var button = new Button(context)
        {
            Text = label,
        };
        button.SetAllCaps(false);
        button.SetSingleLine(true);
        button.SetMinHeight(0);
        button.SetMinimumHeight(0);
        button.SetMinWidth(0);
        button.SetMinimumWidth(0);
        button.SetPadding(Dp(MgSpacing.Sm), 0, Dp(MgSpacing.Sm), 0);
        MgTypography.ApplyLabel(button);
        button.SetTextColor(MgColors.Ink);
        button.Background = MgTheme.TintedSurface(context, MgColors.Surface, MgColors.Hairline, cornerRadius: 8);
        return button;
    }

    private Button LiveCommandButton(string label, bool enabled, bool destructive)
    {
        var button = new Button(context)
        {
            Text = label,
            Enabled = enabled,
        };
        button.SetAllCaps(false);
        button.SetSingleLine(true);
        button.SetMinHeight(0);
        button.SetMinimumHeight(0);
        button.SetMinWidth(0);
        button.SetMinimumWidth(0);
        MgTypography.ApplyLabel(button);
        button.SetTextColor(enabled
            ? destructive ? MgColors.Blocked : MgColors.Ink
            : MgColors.InkMuted);
        button.Background = enabled
            ? destructive
                ? MgTheme.Outline(context, MgColors.Blocked, cornerRadius: 8)
                : MgTheme.MutedSurface(context, cornerRadius: 8)
            : MgTheme.MutedSurface(context, cornerRadius: 8);
        button.ContentDescription = enabled ? label : $"{label} unavailable";
        return button;
    }

    private Button HeaderButton(string label)
    {
        var button = SecondaryButton(label);
        button.SetMinWidth(0);
        button.SetMinHeight(0);
        button.SetMinimumWidth(0);
        button.SetMinimumHeight(0);
        button.SetPadding(Dp(MgSpacing.Sm), 0, Dp(MgSpacing.Sm), 0);
        MgTypography.ApplyMicro(button);
        return button;
    }

    private int StatusBarHeightPx()
    {
        var resources = context.Resources;
        if (resources is null)
        {
            return 0;
        }

        var resourceId = resources.GetIdentifier("status_bar_height", "dimen", "android");
        return resourceId > 0
            ? resources.GetDimensionPixelSize(resourceId)
            : 0;
    }

    private int NavigationBarHeightPx()
    {
        var resources = context.Resources;
        if (resources is null)
        {
            return 0;
        }

        var resourceId = resources.GetIdentifier("navigation_bar_height", "dimen", "android");
        return resourceId > 0
            ? resources.GetDimensionPixelSize(resourceId)
            : 0;
    }

    private TextView Label(string text, int minWidthDp)
    {
        var label = new TextView(context)
        {
            Text = text,
            Gravity = GravityFlags.CenterVertical,
        };
        MgTypography.ApplyLabel(label);
        label.SetMinWidth(Dp(minWidthDp));
        return label;
    }

    private void AddDivider()
    {
        var divider = new View(context)
        {
            Background = MgTheme.Filled(context, MgColors.Hairline, cornerRadius: 1),
        };
        content.AddView(divider, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            Dp(1)));
    }

    private string? TargetFor(PreUiLiveSessionState live, RuntimeInputCommandKind command)
    {
        return command == RuntimeInputCommandKind.RespondToCue
            ? live.ActiveCue?.CueId
            : null;
    }

    private string? ValueFor(RuntimeInputCommandKind command)
    {
        var value = string.IsNullOrWhiteSpace(liveSessionInput)
            ? null
            : liveSessionInput.Trim();

        return command is RuntimeInputCommandKind.RespondToCue
                or RuntimeInputCommandKind.SubmitAnswer
                or RuntimeInputCommandKind.MarkError
                or RuntimeInputCommandKind.Correct
                or RuntimeInputCommandKind.Abandon
            ? value
            : null;
    }

    private static Color RailColorFor(
        BranchLevelStatus left,
        BranchLevelStatus right,
        CurrentTrainingStateReadModel state)
    {
        if (RailInterrupted(left, right, state))
        {
            return MgColors.Blocked;
        }

        if (IsDueMaintenance(state, left) || IsDueMaintenance(state, right))
        {
            return MgColors.Maintenance;
        }

        return right.State == BranchLevelState.Unopened
            ? MgColors.Hairline
            : ColorForBranchState(left.State);
    }

    private static bool RailInterrupted(
        BranchLevelStatus left,
        BranchLevelStatus right,
        CurrentTrainingStateReadModel state)
    {
        return left.State == BranchLevelState.Decayed ||
            right.State == BranchLevelState.Decayed ||
            IsBlockedAtLevel(state, left) ||
            IsBlockedAtLevel(state, right);
    }

    private static bool IsBlockedAtLevel(CurrentTrainingStateReadModel state, BranchLevelStatus status)
    {
        return state.BlockedAdvancement.Any(blocker =>
            blocker.Branch == status.Branch &&
            blocker.Level.HasValue &&
            blocker.Level == status.Level);
    }

    private static bool IsDueMaintenance(CurrentTrainingStateReadModel state, BranchLevelStatus status)
    {
        return state.DueMaintenance.Any(item =>
            item.BranchLevel.Branch == status.Branch &&
            item.BranchLevel.Level == status.Level &&
            item.Currency.State != MaintenanceCurrencyState.Current);
    }

    private static int ReviewStatusRank(BranchLevelStatus status, CurrentTrainingStateReadModel state)
    {
        if (status.State == BranchLevelState.Decayed)
        {
            return 0;
        }

        if (IsBlockedAtLevel(state, status))
        {
            return 1;
        }

        if (IsDueMaintenance(state, status))
        {
            return 2;
        }

        return status.State switch
        {
            BranchLevelState.Stabilizing => 3,
            BranchLevelState.PassedOnce => 4,
            BranchLevelState.TestReady => 5,
            BranchLevelState.Training => 6,
            BranchLevelState.Maintenance => 7,
            BranchLevelState.Owned => 8,
            _ => 9,
        };
    }

    private static bool IsPrimaryWorkLevel(
        CurrentTrainingPresentationReadModel presentation,
        BranchLevelStatus status)
    {
        return presentation.PrimaryPrescribedWork?.BranchLevels.Any(level =>
            level.Branch == status.Branch &&
            level.Level == status.Level) == true;
    }

    private static bool HasTransferWork(CurrentTrainingStateReadModel state, BranchCode branch)
    {
        return state.AvailableNextWork.Any(work => IsTransferWork(work) && work.BranchEmphasis.Contains(branch));
    }

    private static bool IsTransferWork(CurrentTrainingStateNextWork work)
    {
        return work.Session is WeeklySessionKind.Transfer or WeeklySessionKind.TransferOrStabilization;
    }

    private static IReadOnlyList<InspectionSignal> BuildMaintenanceDecayRows(
        CurrentTrainingStateReadModel state,
        TrainingMaintenanceDecayPriority? priority)
    {
        var rows = new List<InspectionSignal>();
        if (priority is not null)
        {
            rows.Add(new InspectionSignal(
                MaintenancePriorityMarker(priority.Kind),
                $"{FormatBranchLevel(priority.Branch, priority.Level)} · {PriorityLabel(priority.Kind)}",
                MaintenancePriorityDetail(priority),
                MaintenancePriorityColor(priority),
                MaintenancePriorityFilled(priority)));
        }

        foreach (var status in state.BranchLevelStates
            .Where(status => status.State == BranchLevelState.Decayed)
            .Where(status => priority is null || !IsSameBranchLevel(priority, status))
            .OrderBy(status => status.Branch)
            .ThenByDescending(status => status.Level))
        {
            rows.Add(new InspectionSignal(
                "D",
                $"{FormatBranchLevel(status.Branch, status.Level)} · Decayed",
                "Restoration required before advancement.",
                MgColors.Blocked,
                Filled: true));
        }

        foreach (var record in state.DueMaintenance
            .Where(record => record.Currency.State != MaintenanceCurrencyState.Current)
            .Where(record => priority is null || !IsSameBranchLevel(priority, record.BranchLevel))
            .OrderByDescending(record => MaintenancePriorityRank(record.Currency.State))
            .ThenBy(record => record.BranchLevel.Branch)
            .ThenByDescending(record => record.BranchLevel.Level))
        {
            rows.Add(new InspectionSignal(
                MaintenanceStateMarker(record.Currency.State),
                $"{FormatBranchLevel(record.BranchLevel.Branch, record.BranchLevel.Level)} · {MaintenanceStateLabel(record.Currency.State)}",
                MaintenanceRecordDetail(record.Currency),
                ColorForMaintenanceState(record.Currency.State),
                Filled: record.Currency.State == MaintenanceCurrencyState.Failed));
        }

        return rows;
    }

    private static bool IsSameBranchLevel(TrainingMaintenanceDecayPriority priority, BranchLevelStatus status)
    {
        return priority.Branch == status.Branch && priority.Level == status.Level;
    }

    private static string BlockerTargetLabel(TrainingPresentationBlockerSummary blocker)
    {
        if (blocker.Branch.HasValue && blocker.Level.HasValue)
        {
            return FormatBranchLevel(blocker.Branch.Value, blocker.Level.Value);
        }

        return blocker.Branch?.ToString() ?? BlockerKindLabel(blocker.Kind);
    }

    private static string CurrentBlockerTargetLabel(CurrentTrainingStateBlocker blocker)
    {
        if (blocker.Branch.HasValue && blocker.Level.HasValue)
        {
            return FormatBranchLevel(blocker.Branch.Value, blocker.Level.Value);
        }

        return blocker.Branch?.ToString() ?? CurrentBlockerSourceLabel(blocker.Source);
    }

    private static string CurrentBlockerSourceLabel(CurrentTrainingStateBlockerSource source)
    {
        return source switch
        {
            CurrentTrainingStateBlockerSource.Category => "Category gate",
            CurrentTrainingStateBlockerSource.WeeklyProgramming => "Weekly gate",
            CurrentTrainingStateBlockerSource.DependencyCap => "Dependency gate",
            CurrentTrainingStateBlockerSource.GlobalBalance => "Balance gate",
            _ => "Blocked",
        };
    }

    private static GlobalReviewDecision? SelectReviewDecision(CurrentGlobalReviewReadModel review)
    {
        return review.Evaluation.Decisions
            .OrderBy(decision => ReviewDecisionRank(decision.Kind))
            .FirstOrDefault();
    }

    private static string ReviewDecisionMarker(GlobalReviewDecisionKind? kind)
    {
        return kind switch
        {
            GlobalReviewDecisionKind.RestoreDecayedBranch => "D",
            GlobalReviewDecisionKind.EmphasizeBottleneckBranch => "B",
            GlobalReviewDecisionKind.OpenAdvancedBranch => "O",
            GlobalReviewDecisionKind.PauseTestsForDeload => "P",
            GlobalReviewDecisionKind.AttemptTransferIntegrationTransfer => "TR",
            GlobalReviewDecisionKind.ContinueCurrentProgression => ">",
            _ => "-",
        };
    }

    private static string ReviewDecisionLabel(GlobalReviewDecision? decision)
    {
        if (decision is null)
        {
            return "Hold advancement";
        }

        var label = ReviewDecisionKindLabel(decision.Kind);
        return decision.Branch.HasValue ? $"{label} · {decision.Branch.Value}" : label;
    }

    private static string ReviewDecisionDetail(CurrentGlobalReviewReadModel review)
    {
        var decision = SelectReviewDecision(review);
        if (decision is not null)
        {
            return decision.Detail;
        }

        return review.Evaluation.Failures.Count == 0
            ? "No programmed change."
            : "Repair blocked input before advancement.";
    }

    private static Color ColorForReviewDecision(CurrentGlobalReviewReadModel review, GlobalReviewDecision? decision)
    {
        if (!review.Evaluation.Passed)
        {
            return MgColors.Blocked;
        }

        return decision?.Kind switch
        {
            GlobalReviewDecisionKind.RestoreDecayedBranch => MgColors.Blocked,
            GlobalReviewDecisionKind.PauseTestsForDeload => MgColors.Recovery,
            GlobalReviewDecisionKind.AttemptTransferIntegrationTransfer => MgColors.Transfer,
            GlobalReviewDecisionKind.EmphasizeBottleneckBranch => MgColors.Training,
            GlobalReviewDecisionKind.OpenAdvancedBranch => MgColors.TestReady,
            _ => MgColors.Owned,
        };
    }

    private static string ReviewFailureSummaryLabel(IReadOnlyList<GlobalReviewFailure> failures)
    {
        if (failures.Count == 0)
        {
            return "No blocked input.";
        }

        var first = GlobalReviewFailureLabel(failures[0].Kind);
        return failures.Count == 1
            ? first
            : $"{failures.Count} blocked inputs; first: {first}.";
    }

    private static string ReviewDateLabel(TrainingDate date) =>
        $"{date.Year:0000}-{date.Month:00}-{date.Day:00}";

    private static string ProgressEmphasisLabel(LocalProgrammedEmphasis emphasis)
    {
        var label = emphasis.Kind switch
        {
            LocalProgressEmphasisKind.RestoreDecayedBranch => "Restore decayed branch",
            LocalProgressEmphasisKind.ResolveMaintenanceBlocker => "Resolve maintenance",
            LocalProgressEmphasisKind.EmphasizeBottleneckBranch => "Emphasize bottleneck",
            LocalProgressEmphasisKind.ContinueMaintenance => "Continue maintenance",
            _ => "Continue training",
        };

        if (emphasis.Branch.HasValue && emphasis.Level.HasValue)
        {
            return $"{label} · {FormatBranchLevel(emphasis.Branch.Value, emphasis.Level.Value)}";
        }

        return emphasis.Branch.HasValue ? $"{label} · {emphasis.Branch.Value}" : label;
    }

    private static Color ColorForProgressEmphasis(LocalProgrammedEmphasis emphasis)
    {
        return emphasis.Kind switch
        {
            LocalProgressEmphasisKind.RestoreDecayedBranch => MgColors.Blocked,
            LocalProgressEmphasisKind.ResolveMaintenanceBlocker or LocalProgressEmphasisKind.ContinueMaintenance => MgColors.Maintenance,
            LocalProgressEmphasisKind.EmphasizeBottleneckBranch => MgColors.TestReady,
            _ => MgColors.Training,
        };
    }

    private static string TitleFor(Screen screen)
    {
        return screen switch
        {
            Screen.Today => "Train",
            Screen.Map => "Map",
            Screen.Evidence => "Record",
            Screen.Review => "Review",
            Screen.LocalData => "Local data",
            Screen.Preflight => "Set up",
            Screen.Live => "Training",
            Screen.Result => "Result",
            _ => "Today",
        };
    }

    private static string TodayCommandTitle(CurrentTrainingPresentationReadModel presentation)
    {
        if (presentation.DailyStatus is { } dailyStatus)
        {
            return dailyStatus switch
            {
                DailyTrainingWorkflowStatus.Done => "Done today",
                DailyTrainingWorkflowStatus.Stopped => "Stopped for today",
                DailyTrainingWorkflowStatus.OffDay => "Off today",
                _ => presentation.PrimaryPrescribedWork?.Exercise.ExerciseName ?? "Today's workout",
            };
        }

        return presentation.Priority switch
        {
            TrainingPresentationPriorityKind.DecayRestoration => "Restore this level",
            TrainingPresentationPriorityKind.MaintenanceDue => presentation.MaintenanceDecayPriority?.Kind == TrainingMaintenanceDecayPriorityKind.MaintenanceFailed
                ? "Repair maintenance"
                : "Maintain this level",
            TrainingPresentationPriorityKind.UrgentBlocker => "Start blocked",
            TrainingPresentationPriorityKind.Recovery => "Reduced-load work",
            TrainingPresentationPriorityKind.Deload => "Deload work",
            TrainingPresentationPriorityKind.NoAvailableWork => "No startable work",
            _ => presentation.PrimaryPrescribedWork is { } work ? work.Exercise.ExerciseName : "Next exercise",
        };
    }

    private static string WorkTitle(TrainingPresentationWorkSummary work)
    {
        return work.Exercise.ExerciseName;
    }

    private static string ShortWorkLabel(TrainingPresentationWorkSummary work)
    {
        return work.Exercise.ExerciseName;
    }

    private static string LiveWorkLabel(TrainingPresentationWorkSummary work)
    {
        return $"{work.Exercise.ExerciseName} · {work.Exercise.BranchLevelLabel}";
    }

    private static string RoleMarker(TrainingPresentationWorkSummary work)
    {
        return WorkRoleLabel(work) switch
        {
            "Practice" => ">",
            "Load" => "L",
            "Test" => "T",
            "Stabilize" => "S",
            "Regress" => "R",
            "Transfer" => "X",
            "Recover" => "R",
            "Maintain" => "M",
            _ => ">",
        };
    }

    private static string PreflightWorkMarker(TrainingPresentationWorkSummary work)
    {
        return work.Drill == DrillId.FH1TargetHold ? "Hold" : RoleMarker(work);
    }

    private static string LoadSummary(IReadOnlyList<LoadVariable> variables)
    {
        return variables.Count == 0
            ? "No added load variables."
            : string.Join(", ", variables.Select(variable => $"{variable.Name}: {variable.Value}"));
    }

    private static string RequiredEvidenceLabel(SessionPreflightPresentationReadModel preflight)
    {
        return preflight.ExpectedEvidenceFactCount <= 0
            ? "The app saves what happened."
            : $"The app saves {preflight.ExpectedEvidenceFactCount} observable items.";
    }

    private static string PreflightStartLabel(TrainingPresentationWorkSummary work)
    {
        return work.Drill switch
        {
            DrillId.FH1TargetHold => $"Start {FocusHoldDurationValue(work.LoadVariables)} hold",
            DrillId.FH2DistractorHold => "Start hold",
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter or
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => "Start cues",
            DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform => "Show items",
            DrillId.DE1PairDiscrimination => "Start comparisons",
            DrillId.DE2SeededAudit => "Open audit",
            DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping => "Start task",
            DrillId.AI1PressureRepeat => "Start repeat",
            DrillId.AI2DisruptionRecovery => "Start recovery task",
            DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask => "Start task",
            _ => "Start exercise",
        };
    }

    private static string PreflightSetupText(TrainingPresentationWorkSummary work)
    {
        if (work.Drill == DrillId.FH1TargetHold)
        {
            var targetLine = string.IsNullOrWhiteSpace(work.Exercise.PrimaryMaterial)
                ? string.Empty
                : $"{Environment.NewLine}{Environment.NewLine}Target: {work.Exercise.PrimaryMaterial}";
            return string.Join(
                Environment.NewLine,
                "1. Read the target.",
                "2. Say it once in your head.",
                "3. Hold only that target when the timer starts.",
                "4. If attention leaves, tap Mind wandered and return.") + targetLine;
        }

        var target = string.IsNullOrWhiteSpace(work.Exercise.PrimaryMaterial)
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}Target: {work.Exercise.PrimaryMaterial}";
        return $"{work.Exercise.FirstScreenInstruction}{Environment.NewLine}{Environment.NewLine}{work.Exercise.BeforeStartInstruction}{target}";
    }

    private static string PreflightCriteriaText(TrainingPresentationWorkSummary work)
    {
        if (work.Drill == DrillId.FH1TargetHold)
        {
            return string.Join(
                Environment.NewLine,
                "This counts when:",
                $"{FocusHoldDurationValue(work.LoadVariables)} finished",
                "5 taps or fewer",
                "Each return is within 10 seconds",
                "Same target the whole time",
                string.Empty,
                "Try again when:",
                "You stop early",
                "Target changed",
                "Wander not tapped",
                "More than 5 taps",
                "Slow return");
        }

        return $"{work.Exercise.SuccessCriteria}{Environment.NewLine}{Environment.NewLine}{work.Exercise.FailureCriteria}{Environment.NewLine}{Environment.NewLine}{work.Exercise.HonestyInstruction}";
    }

    private static string LiveMaterialText(LiveSessionPresentationReadModel presentation)
    {
        if (presentation.ActiveCue is { } cue)
        {
            return cue.Cue;
        }

        if (presentation.CurrentMaterials.Count > 0)
        {
            var material = presentation.CurrentMaterials[0];
            return DisplayMaterialValue(material.Kind, material.Value);
        }

        return "Use the instruction above.";
    }

    private static string DisplayMaterialValue(string kind, string value)
    {
        if (string.Equals(kind, "TargetStatement", StringComparison.Ordinal))
        {
            var target = StripPrefix(value, "Hold target phrase:")
                ?? StripPrefix(value, "Hold target word:")
                ?? value;

            return StripTargetSentencePunctuation(target);
        }

        return value;
    }

    private static string StripTargetSentencePunctuation(string value)
    {
        var trimmed = value.Trim();
        return trimmed.EndsWith(".", StringComparison.Ordinal)
            ? trimmed[..^1].TrimEnd()
            : trimmed;
    }

    private static string? StripPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : null;
    }

    private static string LiveMaterialLabel(LiveSessionPresentationReadModel presentation)
    {
        if (presentation.ActiveCue is not null)
        {
            return "Cue";
        }

        if (presentation.CurrentMaterials.FirstOrDefault() is { } material)
        {
            return material.Kind switch
            {
                "TargetStatement" => "Your target",
                "HoldDuration" => "Hold time",
                "RecoveryWindow" => "Return window",
                "DriftMarkingEvidenceShape" => "What to mark",
                "RuleStatement" => "Rule",
                "EncodeInstruction" => "What to study",
                _ => "Exercise material",
            };
        }

        return presentation.CurrentPhaseKind switch
        {
            RuntimeSessionPhaseKind.InstructionPrep => "Get ready",
            RuntimeSessionPhaseKind.EncodeWindow => "Encode",
            RuntimeSessionPhaseKind.ActiveWork => "Exercise",
            RuntimeSessionPhaseKind.DelayWindow => "Delay",
            RuntimeSessionPhaseKind.CueResponse => "Cue",
            RuntimeSessionPhaseKind.ReconstructionInput => "Reconstruct",
            RuntimeSessionPhaseKind.Audit => "Audit",
            RuntimeSessionPhaseKind.Rest => "Rest",
            RuntimeSessionPhaseKind.Recovery => "Recover",
            RuntimeSessionPhaseKind.Review => "Review",
            _ => "Material",
        };
    }

    private static bool IsCommandAvailable(
        LiveSessionPresentationReadModel presentation,
        RuntimeInputCommandKind command)
    {
        return IsCommandVisible(presentation, command) &&
            presentation.AvailableCommands.Any(item => item.Command == command);
    }

    private static bool IsCommandVisible(
        LiveSessionPresentationReadModel presentation,
        RuntimeInputCommandKind command)
    {
        if (presentation.Work.Drill == DrillId.FH1TargetHold &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.InstructionPrep)
        {
            return command is RuntimeInputCommandKind.FinishPhase
                or RuntimeInputCommandKind.Abandon;
        }

        if (presentation.Work.Drill == DrillId.FH1TargetHold &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork)
        {
            return command is RuntimeInputCommandKind.MarkDrift
                or RuntimeInputCommandKind.Abandon;
        }

        return true;
    }

    private static IReadOnlyList<string> TodayBranchLabels(CurrentTrainingPresentationReadModel presentation)
    {
        if ((presentation.Priority == TrainingPresentationPriorityKind.DecayRestoration ||
                presentation.Priority == TrainingPresentationPriorityKind.MaintenanceDue) &&
            presentation.MaintenanceDecayPriority is { } maintenance)
        {
            return [$"{maintenance.BranchLabel} level {LevelRank(maintenance.Level)}"];
        }

        if (presentation.Priority == TrainingPresentationPriorityKind.UrgentBlocker &&
            presentation.UrgentBlocker is { Branch: { } blockerBranch } blocker)
        {
            return blocker.Level.HasValue
                ? [$"{BranchLabel(blockerBranch)} level {LevelRank(blocker.Level.Value)}"]
                : [BranchLabel(blockerBranch)];
        }

        return TodayBranchSummaryLabels(presentation.PrimaryPrescribedWork?.BranchLevels ?? []);
    }

    private static string BranchNodeLabel(TrainingBranchLevelPresentation branchLevel)
    {
        return branchLevel.Level.HasValue
            ? $"{branchLevel.BranchLabel} level {LevelRank(branchLevel.Level.Value)}"
            : branchLevel.BranchLabel;
    }

    private static IReadOnlyList<string> TodayBranchSummaryLabels(
        IReadOnlyList<TrainingBranchLevelPresentation> branchLevels)
    {
        var branches = branchLevels
            .GroupBy(item => item.Branch)
            .Select(group => group.First())
            .ToArray();
        if (branches.Length == 0)
        {
            return [];
        }

        return branches.Length == 1
            ? [BranchShortLabel(branches[0])]
            : [branches[0].BranchLabel, $"{branches.Length} branches"];
    }

    private static string BranchSummary(IReadOnlyList<TrainingBranchLevelPresentation> branchLevels)
    {
        var labels = TodayBranchSummaryLabels(branchLevels);
        return labels.Count == 0 ? string.Empty : string.Join(" ", labels);
    }

    private static string BranchLabel(BranchCode branch)
    {
        return ProgramCatalog.Branches.Single(item => item.Code == branch).Name;
    }

    private static string BranchShortLabel(TrainingBranchLevelPresentation branchLevel)
    {
        return branchLevel.Level.HasValue
            ? $"{branchLevel.BranchLabel} level {LevelRank(branchLevel.Level.Value)}"
            : branchLevel.BranchLabel;
    }

    private static string LevelLabel(GlobalLevelId level)
    {
        return ProgramCatalog.GlobalLevels.Single(item => item.Id == level).Name;
    }

    private static string LevelCode(GlobalLevelId level)
    {
        return $"L{LevelRank(level)}";
    }

    private static int LevelRank(GlobalLevelId level)
    {
        return ((int)level) + 1;
    }

    private static IReadOnlyList<TodayFact> TodayFacts(CurrentTrainingPresentationReadModel presentation)
    {
        if (IsStartableTodayExercise(presentation))
        {
            return [];
        }

        var facts = new List<TodayFact>
        {
            new(TodayStateLabel(presentation), ColorForPriority(presentation.Priority), IsFilledPriority(presentation.Priority)),
        };

        if ((presentation.Priority == TrainingPresentationPriorityKind.DecayRestoration ||
                presentation.Priority == TrainingPresentationPriorityKind.MaintenanceDue) &&
            presentation.MaintenanceDecayPriority is { } maintenance)
        {
            if (maintenance.BlocksAdvancement)
            {
                facts.Add(new("Blocks work", MgColors.Blocked, false));
            }

            if (maintenance.ConsecutiveFailures > 0)
            {
                facts.Add(new($"{maintenance.ConsecutiveFailures} failed", MgColors.Blocked, false));
            }
            else if (maintenance.DaysSinceLastPassingCheck.HasValue)
            {
                facts.Add(new($"{maintenance.DaysSinceLastPassingCheck.Value} days", MgColors.Maintenance, false));
            }

            return facts;
        }

        if (presentation.Priority == TrainingPresentationPriorityKind.UrgentBlocker &&
            presentation.UrgentBlocker is { } blocker)
        {
            facts.Add(new(BlockerKindLabel(blocker.Kind), MgColors.Blocked, false));
            return facts;
        }

        if (presentation.PrimaryPrescribedWork is { } work)
        {
            if (work.LoadVariables.Count > 0)
            {
                facts.Add(new("Load set", MgColors.Training, false));
            }

            if (work.IsAdvancementWork)
            {
                facts.Add(new(work.AdvancementWorkAllowed ? "Advance work" : "No advance", MgColors.TestReady, false));
            }
        }

        return facts;
    }

    private static string TodayStateLabel(CurrentTrainingPresentationReadModel presentation)
    {
        if (presentation.DailyStatus is { } dailyStatus)
        {
            return dailyStatus switch
            {
                DailyTrainingWorkflowStatus.Done => "Done",
                DailyTrainingWorkflowStatus.Stopped => "Stopped",
                DailyTrainingWorkflowStatus.OffDay => "Off",
                DailyTrainingWorkflowStatus.BetweenBlocks => "Next",
                DailyTrainingWorkflowStatus.Active => "Active",
                DailyTrainingWorkflowStatus.Prepared => "Ready",
                _ => "Ready",
            };
        }

        return presentation.Priority switch
        {
            TrainingPresentationPriorityKind.DecayRestoration => "Decayed",
            TrainingPresentationPriorityKind.MaintenanceDue => presentation.MaintenanceDecayPriority?.Kind switch
            {
                TrainingMaintenanceDecayPriorityKind.MaintenanceFailed => "Failed",
                TrainingMaintenanceDecayPriorityKind.MaintenanceWarning => "Warn",
                _ => "Due",
            },
            TrainingPresentationPriorityKind.UrgentBlocker => "Blocked",
            TrainingPresentationPriorityKind.Recovery => "Recovery",
            TrainingPresentationPriorityKind.Deload => "Deload",
            TrainingPresentationPriorityKind.NoAvailableWork => "Blocked",
            _ => presentation.PrimaryActionEnabled ? "Ready" : "Blocked",
        };
    }

    private static bool IsFilledPriority(TrainingPresentationPriorityKind priority)
    {
        return priority is TrainingPresentationPriorityKind.DecayRestoration
            or TrainingPresentationPriorityKind.UrgentBlocker;
    }

    private static string WorkRoleLabel(TrainingPresentationWorkSummary work)
    {
        if (work.SessionType.HasValue)
        {
            return work.SessionType.Value switch
            {
                AppTrainingSessionType.Practice => "Practice",
                AppTrainingSessionType.Load => "Load",
                AppTrainingSessionType.Test => "Test",
                AppTrainingSessionType.Stabilization => "Stabilize",
                AppTrainingSessionType.Regression => "Restoration",
                AppTrainingSessionType.Transfer => "Transfer",
                AppTrainingSessionType.Recovery => "Recover",
                AppTrainingSessionType.Maintenance => "Maintenance due",
                _ => "Work",
            };
        }

        return work.WeeklySession.HasValue ? WeeklySessionLabel(work.WeeklySession.Value) : "Work";
    }

    private static string TodaySetupLabel(
        TrainingPresentationWorkSummary work,
        CurrentTrainingPresentationReadModel presentation)
    {
        if (!work.HasExecutableStandard)
        {
            return "Set up practice";
        }

        return work.SessionType switch
        {
            AppTrainingSessionType.Maintenance => "Set up check",
            AppTrainingSessionType.Regression => "Set up restoration",
            AppTrainingSessionType.Test => "Set up test",
            AppTrainingSessionType.Stabilization => "Set up repeat",
            _ => work.Drill == DrillId.FH1TargetHold
                ? "Set up hold"
                : TodayPrimaryActionLabel(presentation),
        };
    }

    private static string StandardBlockLabel(TrainingPresentationWorkSummary work)
    {
        if (!work.HasExecutableStandard)
        {
            return "Practice target";
        }

        return work.SessionType == AppTrainingSessionType.Practice ? "Level test" : "Pass";
    }

    private static string WeeklySessionLabel(WeeklySessionKind session)
    {
        return session switch
        {
            WeeklySessionKind.Practice => "Practice",
            WeeklySessionKind.Load => "Load",
            WeeklySessionKind.RecoveryOrLightMaintenance => "Recover or maintain",
            WeeklySessionKind.TestOrStabilization => "Test or stabilize",
            WeeklySessionKind.OffOrRecovery => "Recover",
            WeeklySessionKind.Maintenance => "Maintain",
            WeeklySessionKind.TransferOrStabilization => "Transfer or stabilize",
            WeeklySessionKind.Recovery => "Recover",
            WeeklySessionKind.Transfer => "Transfer",
            WeeklySessionKind.Stabilization => "Stabilize",
            WeeklySessionKind.RecoveryOrRetest => "Recover or retest",
            _ => "Work",
        };
    }

    private static string BlockerKindLabel(TrainingPresentationBlockerKind kind)
    {
        return kind switch
        {
            TrainingPresentationBlockerKind.BranchUnavailable => "Locked",
            TrainingPresentationBlockerKind.MaintenanceOrDecay => "Maintenance",
            TrainingPresentationBlockerKind.Readiness => "Readiness",
            TrainingPresentationBlockerKind.Dependency => "Dependency",
            TrainingPresentationBlockerKind.GlobalBalance => "Balance",
            TrainingPresentationBlockerKind.Transfer => "Transfer",
            TrainingPresentationBlockerKind.WeeklyConstraint => "Schedule",
            TrainingPresentationBlockerKind.PractitionerCategory => "Review",
            TrainingPresentationBlockerKind.PreparationRejected => "Setup",
            _ => "Blocked",
        };
    }

    private static string TitleFor(CurrentTrainingPresentationReadModel presentation)
    {
        return presentation.Priority switch
        {
            TrainingPresentationPriorityKind.DecayRestoration => "Restore required",
            TrainingPresentationPriorityKind.MaintenanceDue => "Maintenance due",
            TrainingPresentationPriorityKind.UrgentBlocker => "Start blocked",
            TrainingPresentationPriorityKind.Recovery => "Recovery work",
            TrainingPresentationPriorityKind.Deload => "Deload",
            TrainingPresentationPriorityKind.NoAvailableWork => "No work available",
            _ => presentation.PrimaryPrescribedWork is { } work ? work.Exercise.ExerciseName : "Next exercise",
        };
    }

    private static string MarkerFor(TrainingPresentationPriorityKind priority)
    {
        return priority switch
        {
            TrainingPresentationPriorityKind.UrgentBlocker or TrainingPresentationPriorityKind.DecayRestoration => "!",
            TrainingPresentationPriorityKind.MaintenanceDue => "M",
            TrainingPresentationPriorityKind.Recovery or TrainingPresentationPriorityKind.Deload => "R",
            _ => ">",
        };
    }

    private static string TodayMarkerFor(CurrentTrainingPresentationReadModel presentation)
    {
        return IsStartableTodayExercise(presentation) ? "Start" : MarkerFor(presentation.Priority);
    }

    private static Color ColorForPriority(TrainingPresentationPriorityKind priority)
    {
        return priority switch
        {
            TrainingPresentationPriorityKind.UrgentBlocker or TrainingPresentationPriorityKind.DecayRestoration => MgColors.Blocked,
            TrainingPresentationPriorityKind.MaintenanceDue => MgColors.Maintenance,
            TrainingPresentationPriorityKind.Recovery or TrainingPresentationPriorityKind.Deload => MgColors.Recovery,
            _ => MgColors.Training,
        };
    }

    private static string ActionLabel(TrainingPresentationPrimaryActionKind action)
    {
        return action switch
        {
            TrainingPresentationPrimaryActionKind.StartMaintenance => "Maintain",
            TrainingPresentationPrimaryActionKind.StartRecovery => "Recover",
            TrainingPresentationPrimaryActionKind.StartDeload => "Deload",
            TrainingPresentationPrimaryActionKind.RestoreDecayedWork => "Restore",
            TrainingPresentationPrimaryActionKind.ResolveBlocker => "Blocked",
            TrainingPresentationPrimaryActionKind.StartLiveSession => "Start",
            TrainingPresentationPrimaryActionKind.ContinueLiveSession => "Resume",
            TrainingPresentationPrimaryActionKind.ReturnToNextPrescribedAction => "Today",
            _ => "Start",
        };
    }

    private static string TodayPrimaryActionLabel(CurrentTrainingPresentationReadModel presentation)
    {
        if (presentation.PrimaryPrescribedWork is { } work &&
            presentation.PrimaryAction is TrainingPresentationPrimaryActionKind.StartPrescribedWork
                or TrainingPresentationPrimaryActionKind.StartLiveSession
                or TrainingPresentationPrimaryActionKind.ReturnToNextPrescribedAction)
        {
            return $"Start {work.Exercise.ExerciseName}";
        }

        return ActionLabel(presentation.PrimaryAction);
    }

    private static string PriorityLabel(TrainingMaintenanceDecayPriorityKind kind)
    {
        return kind switch
        {
            TrainingMaintenanceDecayPriorityKind.DecayRestoration => "Restore required",
            TrainingMaintenanceDecayPriorityKind.MaintenanceFailed => "Maintenance failed",
            TrainingMaintenanceDecayPriorityKind.MaintenanceWarning => "Maintenance warning",
            _ => "Maintenance due",
        };
    }

    private static string MaintenancePriorityMarker(TrainingMaintenanceDecayPriorityKind kind)
    {
        return kind switch
        {
            TrainingMaintenanceDecayPriorityKind.DecayRestoration => "D",
            TrainingMaintenanceDecayPriorityKind.MaintenanceFailed => "!",
            TrainingMaintenanceDecayPriorityKind.MaintenanceWarning => "W",
            _ => "M",
        };
    }

    private static string MaintenancePriorityDetail(TrainingMaintenanceDecayPriority priority)
    {
        if (priority.Kind == TrainingMaintenanceDecayPriorityKind.DecayRestoration)
        {
            return "Restoration required before advancement.";
        }

        if (priority.BlocksAdvancement)
        {
            return "Blocks advancement.";
        }

        if (priority.ConsecutiveFailures > 0)
        {
            return $"{priority.ConsecutiveFailures} failed checks.";
        }

        return priority.DaysSinceLastPassingCheck.HasValue
            ? $"{priority.DaysSinceLastPassingCheck.Value} days since passing check."
            : "Check required.";
    }

    private static string MaintenancePriorityUserDetail(TrainingMaintenanceDecayPriority priority)
    {
        var branchLevel = $"{BranchLabel(priority.Branch)} level {LevelRank(priority.Level)}";
        if (priority.Kind == TrainingMaintenanceDecayPriorityKind.DecayRestoration)
        {
            return $"{branchLevel} needs restoration before related progress can continue.";
        }

        if (priority.ConsecutiveFailures > 0)
        {
            return $"{branchLevel} has {priority.ConsecutiveFailures} failed maintenance checks.";
        }

        return priority.DaysSinceLastPassingCheck.HasValue
            ? $"{branchLevel} was last checked {priority.DaysSinceLastPassingCheck.Value} days ago."
            : $"{branchLevel} needs a passing maintenance check.";
    }

    private static Color MaintenancePriorityColor(TrainingMaintenanceDecayPriority priority)
    {
        return priority.Kind is TrainingMaintenanceDecayPriorityKind.DecayRestoration
                or TrainingMaintenanceDecayPriorityKind.MaintenanceFailed
            ? MgColors.Blocked
            : MgColors.Maintenance;
    }

    private static bool MaintenancePriorityFilled(TrainingMaintenanceDecayPriority priority)
    {
        return priority.BlocksAdvancement ||
            priority.Kind is TrainingMaintenanceDecayPriorityKind.DecayRestoration
                or TrainingMaintenanceDecayPriorityKind.MaintenanceFailed;
    }

    private static string MaintenanceStateMarker(MaintenanceCurrencyState state)
    {
        return state switch
        {
            MaintenanceCurrencyState.Failed => "!",
            MaintenanceCurrencyState.Warning => "W",
            _ => "M",
        };
    }

    private static string MaintenanceStateLabel(MaintenanceCurrencyState state)
    {
        return state switch
        {
            MaintenanceCurrencyState.Failed => "Maintenance failed",
            MaintenanceCurrencyState.Warning => "Maintenance warning",
            MaintenanceCurrencyState.Due => "Maintenance due",
            _ => "Maintenance current",
        };
    }

    private static string MaintenanceRecordDetail(MaintenanceCurrencyResult result)
    {
        return result.State switch
        {
            MaintenanceCurrencyState.Failed => "Repair required before advancement.",
            MaintenanceCurrencyState.Warning => result.ConsecutiveFailures > 0
                ? $"{result.ConsecutiveFailures} failed checks."
                : "Warning active.",
            MaintenanceCurrencyState.Due => result.DaysSinceLastPassingCheck.HasValue
                ? $"{result.DaysSinceLastPassingCheck.Value} days since passing check."
                : "Check due.",
            _ => "Current.",
        };
    }

    private static Color ColorForMaintenanceState(MaintenanceCurrencyState state)
    {
        return state == MaintenanceCurrencyState.Failed ? MgColors.Blocked : MgColors.Maintenance;
    }

    private static int MaintenancePriorityRank(MaintenanceCurrencyState state)
    {
        return state switch
        {
            MaintenanceCurrencyState.Failed => 3,
            MaintenanceCurrencyState.Due => 2,
            MaintenanceCurrencyState.Warning => 1,
            _ => 0,
        };
    }

    private static string ResumeStatusLabel(PreUiActiveSessionResumeStatus status)
    {
        return status switch
        {
            PreUiActiveSessionResumeStatus.Resumable => "Resume available.",
            PreUiActiveSessionResumeStatus.NotPersisted => "Resume was not persisted.",
            PreUiActiveSessionResumeStatus.Unsafe => "Resume is unsafe.",
            _ => "Resume not found.",
        };
    }

    private static string CommandLabel(
        RuntimeInputCommandKind command,
        bool primary,
        RuntimeSessionPhaseKind? phase)
    {
        return command switch
        {
            RuntimeInputCommandKind.RespondToCue => "Respond",
            RuntimeInputCommandKind.SubmitAnswer => "Submit answer",
            RuntimeInputCommandKind.MarkDrift => "Mind wandered",
            RuntimeInputCommandKind.MarkGuess => "Mark guess",
            RuntimeInputCommandKind.MarkError => "Mark error",
            RuntimeInputCommandKind.Correct => "Fix answer",
            RuntimeInputCommandKind.StartAudit => "Start audit",
            RuntimeInputCommandKind.FinishPhase => FinishPhaseLabel(primary, phase),
            RuntimeInputCommandKind.Pause => "Pause",
            RuntimeInputCommandKind.Resume => "Resume",
            RuntimeInputCommandKind.Abandon => AbandonLabel(primary, phase),
            _ => "Action",
        };
    }

    private static string AbandonLabel(bool primary, RuntimeSessionPhaseKind? phase)
    {
        if (primary)
        {
            return "Stop early";
        }

        return phase == RuntimeSessionPhaseKind.InstructionPrep ? "Not now" : "Stop early";
    }

    private static string FinishPhaseLabel(bool primary, RuntimeSessionPhaseKind? phase)
    {
        return phase switch
        {
            RuntimeSessionPhaseKind.InstructionPrep => "Start holding",
            RuntimeSessionPhaseKind.Review => primary ? "Finish session" : "Finish",
            RuntimeSessionPhaseKind.ReconstructionInput => "Submit answer",
            RuntimeSessionPhaseKind.Audit => primary ? "Finish audit" : "Audit done",
            _ => "Next step",
        };
    }

    private static string PhaseLabel(RuntimeSessionPhaseKind? phase)
    {
        return phase switch
        {
            RuntimeSessionPhaseKind.InstructionPrep => "Get ready",
            RuntimeSessionPhaseKind.EncodeWindow => "Encode",
            RuntimeSessionPhaseKind.ActiveWork => "Work",
            RuntimeSessionPhaseKind.DelayWindow => "Delay",
            RuntimeSessionPhaseKind.CueResponse => "Cue",
            RuntimeSessionPhaseKind.ReconstructionInput => "Reconstruct",
            RuntimeSessionPhaseKind.Audit => "Audit",
            RuntimeSessionPhaseKind.Rest => "Rest",
            RuntimeSessionPhaseKind.Recovery => "Recover",
            RuntimeSessionPhaseKind.Review => "Review",
            _ => "Phase",
        };
    }

    private static string LifecycleLabel(RuntimeSessionLifecycleStatus status)
    {
        return status switch
        {
            RuntimeSessionLifecycleStatus.NotStarted => "Not started",
            RuntimeSessionLifecycleStatus.Running => "Running",
            RuntimeSessionLifecycleStatus.Paused => "Paused",
            RuntimeSessionLifecycleStatus.Completed => "Completed",
            RuntimeSessionLifecycleStatus.Failed => "Failed",
            RuntimeSessionLifecycleStatus.Abandoned => "Stopped",
            _ => status.ToString(),
        };
    }

    private static string LiveUnavailableActionLabel(RuntimeSessionLifecycleStatus status)
    {
        return status switch
        {
            RuntimeSessionLifecycleStatus.Completed => "Exercise complete",
            RuntimeSessionLifecycleStatus.Failed => "Exercise stopped",
            RuntimeSessionLifecycleStatus.Abandoned => "Exercise stopped",
            _ => "No action available",
        };
    }

    private static Color ColorForLifecycle(RuntimeSessionLifecycleStatus status)
    {
        return status switch
        {
            RuntimeSessionLifecycleStatus.Paused => MgColors.Recovery,
            RuntimeSessionLifecycleStatus.Completed => MgColors.Owned,
            RuntimeSessionLifecycleStatus.Failed or RuntimeSessionLifecycleStatus.Abandoned => MgColors.Blocked,
            _ => MgColors.Training,
        };
    }

    private static string ResultTitle(ResultPresentationReadModel result)
    {
        return result.Outcome switch
        {
            TrainingResultPresentationOutcomeKind.Cancelled => "Not started",
            TrainingResultPresentationOutcomeKind.Abandoned => "Stopped early",
            TrainingResultPresentationOutcomeKind.TimedOut => "Timed out",
            TrainingResultPresentationOutcomeKind.Failed => "Failed",
            TrainingResultPresentationOutcomeKind.NoAdvancement => "Recorded",
            TrainingResultPresentationOutcomeKind.CleanPractice => "Clean set",
            TrainingResultPresentationOutcomeKind.TestReady => "Test ready",
            TrainingResultPresentationOutcomeKind.PassedOnce => "Successful set",
            TrainingResultPresentationOutcomeKind.Stabilizing => "Needs repeat",
            TrainingResultPresentationOutcomeKind.Owned => "Level stable",
            TrainingResultPresentationOutcomeKind.Maintenance => "Maintenance recorded",
            TrainingResultPresentationOutcomeKind.MaintenanceWarning => "Needs maintenance",
            TrainingResultPresentationOutcomeKind.MaintenanceFailed => "Maintenance failed",
            TrainingResultPresentationOutcomeKind.Decayed => "Needs restoration",
            TrainingResultPresentationOutcomeKind.Recovery => "Recovery recorded",
            TrainingResultPresentationOutcomeKind.Blocked => "Still blocked",
            TrainingResultPresentationOutcomeKind.TransferEligible => "Ready for transfer",
            _ => "Result pending",
        };
    }

    private static string ResultMarker(TrainingResultPresentationOutcomeKind outcome)
    {
        return outcome switch
        {
            TrainingResultPresentationOutcomeKind.Cancelled => "-",
            TrainingResultPresentationOutcomeKind.Abandoned => "-",
            TrainingResultPresentationOutcomeKind.TimedOut => "!",
            TrainingResultPresentationOutcomeKind.Failed => "!",
            TrainingResultPresentationOutcomeKind.NoAdvancement => "-",
            TrainingResultPresentationOutcomeKind.CleanPractice => "C",
            TrainingResultPresentationOutcomeKind.TestReady => "T",
            TrainingResultPresentationOutcomeKind.PassedOnce => "1",
            TrainingResultPresentationOutcomeKind.Stabilizing => "S",
            TrainingResultPresentationOutcomeKind.Owned => "O",
            TrainingResultPresentationOutcomeKind.Maintenance => "M",
            TrainingResultPresentationOutcomeKind.MaintenanceWarning => "W",
            TrainingResultPresentationOutcomeKind.MaintenanceFailed => "!",
            TrainingResultPresentationOutcomeKind.Decayed => "D",
            TrainingResultPresentationOutcomeKind.Recovery => "R",
            TrainingResultPresentationOutcomeKind.Blocked => "B",
            TrainingResultPresentationOutcomeKind.TransferEligible => ">",
            _ => "-",
        };
    }

    private static Color ColorForResult(TrainingResultPresentationOutcomeKind outcome)
    {
        return outcome switch
        {
            TrainingResultPresentationOutcomeKind.Cancelled => MgColors.Recovery,
            TrainingResultPresentationOutcomeKind.Abandoned => MgColors.Recovery,
            TrainingResultPresentationOutcomeKind.TimedOut
                or TrainingResultPresentationOutcomeKind.Failed
                or TrainingResultPresentationOutcomeKind.MaintenanceFailed
                or TrainingResultPresentationOutcomeKind.Decayed
                or TrainingResultPresentationOutcomeKind.Blocked => MgColors.Blocked,
            TrainingResultPresentationOutcomeKind.Owned => MgColors.Owned,
            TrainingResultPresentationOutcomeKind.CleanPractice => MgColors.Training,
            TrainingResultPresentationOutcomeKind.TestReady
                or TrainingResultPresentationOutcomeKind.PassedOnce
                or TrainingResultPresentationOutcomeKind.Stabilizing
                or TrainingResultPresentationOutcomeKind.TransferEligible => MgColors.Training,
            TrainingResultPresentationOutcomeKind.Maintenance
                or TrainingResultPresentationOutcomeKind.MaintenanceWarning => MgColors.Maintenance,
            _ => MgColors.Recovery,
        };
    }

    private static string OutcomeText(ResultPresentationReadModel result)
    {
        return result.Outcome switch
        {
            TrainingResultPresentationOutcomeKind.Cancelled => "No attempt was recorded.",
            TrainingResultPresentationOutcomeKind.Abandoned => "You stopped before finishing. No successful set counted.",
            TrainingResultPresentationOutcomeKind.TimedOut => "The exercise ran out of time before the required work was complete. No successful set was recorded.",
            TrainingResultPresentationOutcomeKind.Failed => FailureOutcomeText(result),
            TrainingResultPresentationOutcomeKind.NoAdvancement => "Session saved. Continue from Today.",
            TrainingResultPresentationOutcomeKind.CleanPractice => "Clean practice counted. The branch level is unchanged.",
            TrainingResultPresentationOutcomeKind.TestReady => "Two clean practices counted. The level test is next.",
            TrainingResultPresentationOutcomeKind.PassedOnce => "One successful set counted. Repeat cleanly before this level is considered stable.",
            TrainingResultPresentationOutcomeKind.Stabilizing => "Clean repeat counted. Keep repeating until the level is stable.",
            TrainingResultPresentationOutcomeKind.Owned => "This level is now stable enough to count.",
            TrainingResultPresentationOutcomeKind.Maintenance => "Maintenance check counted.",
            TrainingResultPresentationOutcomeKind.MaintenanceWarning => "Maintenance check recorded, but this level still needs attention.",
            TrainingResultPresentationOutcomeKind.MaintenanceFailed => "Maintenance check did not pass. The app will prescribe repair work.",
            TrainingResultPresentationOutcomeKind.Decayed => "This level has slipped and needs restoration work before related progress can continue.",
            TrainingResultPresentationOutcomeKind.Recovery => "Reduced-load recovery work saved. Continue from Today.",
            TrainingResultPresentationOutcomeKind.Blocked => "Progress stayed blocked until the listed issue is resolved.",
            TrainingResultPresentationOutcomeKind.TransferEligible => "This work is ready to be used in a transfer exercise.",
            _ => "Result is still being recorded.",
        };
    }

    private static string FailureOutcomeText(ResultPresentationReadModel result)
    {
        return result.FailureType is null
            ? "This set did not meet the success rules. Try it again."
            : $"{FailureTypeLabel(result.FailureType.Value)}. Try it again.";
    }

    private static string ResultEvidenceText(ResultPresentationReadModel result)
    {
        if (result.Outcome is TrainingResultPresentationOutcomeKind.Abandoned
            or TrainingResultPresentationOutcomeKind.TimedOut
            or TrainingResultPresentationOutcomeKind.Failed)
        {
            if (result.Outcome == TrainingResultPresentationOutcomeKind.Abandoned)
            {
                return "Stopped attempt saved. No success counted.";
            }

            if (result.Outcome == TrainingResultPresentationOutcomeKind.TimedOut)
            {
                return result.EvidenceSummary.HasObservableEvidence
                    ? "Timed-out attempt saved."
                    : "No successful set was recorded.";
            }

            return result.EvidenceSummary.HasObservableEvidence
                ? "Failed attempt saved."
                : "No successful set was recorded.";
        }

        if (result.EvidenceSummary.HasFailureEvidence)
        {
            return "Failure record saved.";
        }

        if (result.ProducesSuccessfulEvidence)
        {
            return EvidenceCategoryLabel(result.EvidenceSummary.LatestEvidenceCategory) + " record saved.";
        }

        return result.EvidenceSummary.HasObservableEvidence
            ? "Attempt saved. Continue from Today."
            : "Attempt record incomplete.";
    }

    private static string ResultChangeText(ResultPresentationReadModel result)
    {
        if (result.StateTransition is { Changed: true } transition)
        {
            return $"{FormatBranchLevel(transition.Branch, transition.Level)}: {ResultStateLabel(transition.FromState)} -> {ResultStateLabel(transition.ToState)}.";
        }

        return result.Outcome switch
        {
            TrainingResultPresentationOutcomeKind.Maintenance => "Branch level unchanged; maintenance contact recorded.",
            TrainingResultPresentationOutcomeKind.MaintenanceWarning => "Branch level unchanged; maintenance warning remains.",
            TrainingResultPresentationOutcomeKind.Decayed => "Dependent advancement is capped until restoration.",
            TrainingResultPresentationOutcomeKind.Recovery => "Branch level unchanged; reduced-load work recorded.",
            TrainingResultPresentationOutcomeKind.Abandoned
                or TrainingResultPresentationOutcomeKind.TimedOut
                or TrainingResultPresentationOutcomeKind.Failed => "You stay on this exercise; try it again when ready.",
            TrainingResultPresentationOutcomeKind.Blocked => "Resolve the blocker before progress can change.",
            _ => "No branch-level state changed.",
        };
    }

    private static string ResultChangeLabel(ResultPresentationReadModel result)
    {
        return result.Outcome is TrainingResultPresentationOutcomeKind.Abandoned
            or TrainingResultPresentationOutcomeKind.TimedOut
            or TrainingResultPresentationOutcomeKind.Failed
            ? "Your place"
            : "Progress";
    }

    private static string ResultNextActionText(CurrentTrainingPresentationReadModel? next)
    {
        if (next is null)
        {
            return "Go back to Today when recording finishes.";
        }

        if (next.Priority == TrainingPresentationPriorityKind.UrgentBlocker &&
            next.UrgentBlocker is { } blocker)
        {
            return $"Show blocker: {BlockerKindLabel(blocker.Kind)}.";
        }

        if ((next.Priority == TrainingPresentationPriorityKind.DecayRestoration ||
                next.Priority == TrainingPresentationPriorityKind.MaintenanceDue) &&
            next.MaintenanceDecayPriority is { } maintenance)
        {
            return $"{ActionLabel(next.PrimaryAction)} {maintenance.BranchLabel} level {LevelRank(maintenance.Level)}.";
        }

        if (next.PrimaryPrescribedWork is not null)
        {
            return $"{TodayPrimaryActionLabel(next)} when ready.";
        }

        return "Go back to Today for the next step.";
    }

    private static string EvidenceCategoryLabel(EvidenceArtifactCategory? category)
    {
        return category switch
        {
            EvidenceArtifactCategory.Test => "Test",
            EvidenceArtifactCategory.Stabilization => "Stabilization",
            EvidenceArtifactCategory.Transfer => "Transfer",
            EvidenceArtifactCategory.Maintenance => "Maintenance",
            EvidenceArtifactCategory.GlobalReview => "Review",
            EvidenceArtifactCategory.Load => "Load",
            _ => "Session",
        };
    }

    private static string SessionTypeLabel(LocalCompletedSessionType sessionType)
    {
        return sessionType switch
        {
            LocalCompletedSessionType.Practice => "Practice",
            LocalCompletedSessionType.Load => "Load",
            LocalCompletedSessionType.Test => "Test",
            LocalCompletedSessionType.Stabilization => "Stabilization",
            LocalCompletedSessionType.Regression => "Regression",
            LocalCompletedSessionType.Transfer => "Transfer",
            LocalCompletedSessionType.Recovery => "Recovery",
            LocalCompletedSessionType.Review => "Review",
            _ => "Session",
        };
    }

    private static string ObservableEvidenceLabel(ObservableEvidenceKind kind)
    {
        return kind switch
        {
            ObservableEvidenceKind.Score => "Score",
            ObservableEvidenceKind.Time => "Time",
            ObservableEvidenceKind.ErrorCount => "Errors",
            ObservableEvidenceKind.Reconstruction => "Reconstruction",
            ObservableEvidenceKind.Comparison => "Comparison",
            ObservableEvidenceKind.RuleExplanation => "Rule",
            ObservableEvidenceKind.FailedItemList => "Missed items",
            ObservableEvidenceKind.RepeatabilityRecord => "Repeatability",
            ObservableEvidenceKind.OutputSample => "Output",
            ObservableEvidenceKind.BranchMapping => "Branch mapping",
            ObservableEvidenceKind.CriticalConstraintRecord => "Constraint",
            ObservableEvidenceKind.LoadVariableRecord => "Load",
            ObservableEvidenceKind.BottleneckNote => "Bottleneck",
            ObservableEvidenceKind.AuditResult => "Audit",
            ObservableEvidenceKind.DelayedReconstruction => "Delayed recall",
            ObservableEvidenceKind.MaintenanceCheck => "Maintenance",
            ObservableEvidenceKind.GlobalReviewSummary => "Review",
            _ => "Evidence",
        };
    }

    private static string EvidenceMarker(EvidenceArtifactCategory category)
    {
        return category switch
        {
            EvidenceArtifactCategory.Load => "L",
            EvidenceArtifactCategory.Test => "T",
            EvidenceArtifactCategory.Stabilization => "S",
            EvidenceArtifactCategory.Transfer => "TR",
            EvidenceArtifactCategory.Maintenance => "M",
            EvidenceArtifactCategory.GlobalReview => "R",
            _ => "P",
        };
    }

    private static Color ColorForEvidenceCategory(EvidenceArtifactCategory category)
    {
        return category switch
        {
            EvidenceArtifactCategory.Test or EvidenceArtifactCategory.Stabilization => MgColors.TestReady,
            EvidenceArtifactCategory.Transfer => MgColors.Transfer,
            EvidenceArtifactCategory.Maintenance => MgColors.Maintenance,
            EvidenceArtifactCategory.GlobalReview => MgColors.Recovery,
            _ => MgColors.Training,
        };
    }

    private static string FailureTypeLabel(FailureType failureType)
    {
        return failureType switch
        {
            FailureType.TechnicalFailure => "Technical failure",
            FailureType.EffortFailure => "Effort failure",
            FailureType.Overload => "Overload failure",
            FailureType.BadProgramming => "Programming failure",
            _ => "Failure",
        };
    }

    private static string StateLabel(BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.Unopened => "Locked",
            BranchLevelState.Training => "Training",
            BranchLevelState.TestReady => "Test ready",
            BranchLevelState.PassedOnce => "Passed once",
            BranchLevelState.Stabilizing => "Stabilizing",
            BranchLevelState.Owned => "Owned",
            BranchLevelState.Maintenance => "Maintenance",
            BranchLevelState.Decayed => "Decayed",
            _ => state.ToString(),
        };
    }

    private static string ResultStateLabel(BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.Unopened => "Not started",
            BranchLevelState.Training => "Training",
            BranchLevelState.TestReady => "Ready to test",
            BranchLevelState.PassedOnce => "One pass",
            BranchLevelState.Stabilizing => "Needs repeats",
            BranchLevelState.Owned => "Stable",
            BranchLevelState.Maintenance => "Maintenance",
            BranchLevelState.Decayed => "Needs restoration",
            _ => StateLabel(state),
        };
    }

    private static Color ColorForBranchState(BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.Training => MgColors.Training,
            BranchLevelState.TestReady or BranchLevelState.Stabilizing => MgColors.TestReady,
            BranchLevelState.PassedOnce => MgColors.PassedOnce,
            BranchLevelState.Owned => MgColors.Owned,
            BranchLevelState.Maintenance => MgColors.Maintenance,
            BranchLevelState.Decayed => MgColors.Blocked,
            _ => MgColors.Hairline,
        };
    }

    private static string ReviewDecisionKindLabel(GlobalReviewDecisionKind kind)
    {
        return kind switch
        {
            GlobalReviewDecisionKind.ContinueCurrentProgression => "Continue current progression",
            GlobalReviewDecisionKind.EmphasizeBottleneckBranch => "Emphasize bottleneck",
            GlobalReviewDecisionKind.RestoreDecayedBranch => "Restore decayed branch",
            GlobalReviewDecisionKind.OpenAdvancedBranch => "Open advanced branch",
            GlobalReviewDecisionKind.PauseTestsForDeload => "Pause tests for deload",
            GlobalReviewDecisionKind.AttemptTransferIntegrationTransfer => "Attempt transfer",
            _ => "Programmed decision",
        };
    }

    private static string GlobalReviewFailureLabel(GlobalReviewFailureKind kind)
    {
        return kind switch
        {
            GlobalReviewFailureKind.WholePractitionerInputMissing => "Whole-practitioner input missing",
            GlobalReviewFailureKind.PrerequisiteBranchDecayed => "Prerequisite decayed",
            GlobalReviewFailureKind.MaintenanceCheckOverdue => "Maintenance overdue",
            GlobalReviewFailureKind.BottleneckProgrammedResponseMissing => "Programmed response missing",
            GlobalReviewFailureKind.CurrentTransferOrStabilizationArtifactMissing => "Transfer or stabilization record missing",
            GlobalReviewFailureKind.ParticipationOnlyAdvancement => "Participation-only advancement",
            _ => "Blocked review input",
        };
    }

    private static int ReviewDecisionRank(GlobalReviewDecisionKind kind)
    {
        return kind switch
        {
            GlobalReviewDecisionKind.RestoreDecayedBranch => 0,
            GlobalReviewDecisionKind.PauseTestsForDeload => 1,
            GlobalReviewDecisionKind.EmphasizeBottleneckBranch => 2,
            GlobalReviewDecisionKind.AttemptTransferIntegrationTransfer => 3,
            GlobalReviewDecisionKind.OpenAdvancedBranch => 4,
            GlobalReviewDecisionKind.ContinueCurrentProgression => 5,
            _ => 6,
        };
    }

    private static string FormatBranchLevel(BranchCode branch, GlobalLevelId level)
    {
        return $"{BranchLabel(branch)} · Level {LevelRank(level)}";
    }

    private static string FormatDate(TrainingDate date)
    {
        return $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2}";
    }

    private static string OperationStatusLabel(LocalDataBackupOperationStatus status)
    {
        return status switch
        {
            LocalDataBackupOperationStatus.Succeeded => "Succeeded",
            LocalDataBackupOperationStatus.NotFound => "Not found",
            LocalDataBackupOperationStatus.ConfirmationRequired => "Confirm",
            _ => "Failed",
        };
    }

    private static string FormatDuration(TimeSpan value)
    {
        var seconds = Math.Max(0, (int)Math.Ceiling(value.TotalSeconds));
        return $"{seconds}s";
    }

    private LinearLayout.LayoutParams MatchWrap()
    {
        return new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
    }

    private LinearLayout.LayoutParams WrapWrap()
    {
        return new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent);
    }

    private LinearLayout.LayoutParams WrapWrapWithTop(int spacing = MgSpacing.Md)
    {
        var layout = WrapWrap();
        layout.SetMargins(0, Dp(spacing), 0, 0);
        return layout;
    }

    private LinearLayout.LayoutParams MatchWrapWithBottom()
    {
        var layout = MatchWrap();
        layout.SetMargins(0, 0, 0, Dp(MgSpacing.Lg));
        return layout;
    }

    private LinearLayout.LayoutParams MatchWrapWithTop(int spacing = MgSpacing.Md)
    {
        var layout = MatchWrap();
        layout.SetMargins(0, Dp(spacing), 0, 0);
        return layout;
    }

    private int Dp(int value)
    {
        return MgSpacing.Dp(context, value);
    }

    private void ResetScrollPosition()
    {
        scrollView.Post(() => scrollView.ScrollTo(0, 0));
    }
}
