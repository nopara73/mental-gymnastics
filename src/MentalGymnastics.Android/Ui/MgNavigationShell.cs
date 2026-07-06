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
    private readonly ScrollView scrollView;
    private readonly LinearLayout content;
    private readonly LinearLayout navigation;
    private readonly TextView subtitle;

    private AndroidTrainingStateSnapshot? snapshot;
    private AndroidSessionStartSnapshot? sessionStartSnapshot;
    private AndroidLiveSessionSnapshot? liveSessionSnapshot;
    private AndroidLiveSessionCompletionSnapshot? liveCompletionSnapshot;
    private AndroidActiveSessionResumeSnapshot? activeSessionResumeSnapshot;
    private LocalDataBackupOperationResult? localDataOperation;
    private string liveSessionInput = string.Empty;
    private bool restoreConfirmationArmed;
    private AndroidShellScreen currentScreen = AndroidShellScreen.Work;
    private BranchCode? selectedBranch;
    private GlobalLevelId? selectedLevel;

    public MgNavigationShell(Context context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));

        Root = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        Root.SetBackgroundColor(MgColors.Canvas);

        subtitle = new TextView(context)
        {
            Text = "Work",
        };
        Root.AddView(BuildHeader(), new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));

        scrollView = new ScrollView(context);
        content = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        content.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Lg));
        scrollView.AddView(content);
        Root.AddView(scrollView, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            0,
            1));

        navigation = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        navigation.SetBackgroundColor(MgColors.Surface);
        navigation.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Sm));
        Root.AddView(navigation, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));
        RenderNavigation();
    }

    private enum AndroidShellScreen
    {
        Work,
        Map,
        Progress,
        Evidence,
        Maintenance,
        GlobalReview,
        LocalData,
        BranchDetail,
        SessionStart,
        LiveSession,
        LiveResult,
    }

    private enum ResultVisualKind
    {
        Waiting,
        Recorded,
        Failed,
        PassedOnce,
        Stabilizing,
        Owned,
        MaintenancePass,
        Warning,
        Decayed,
        Abandoned,
        TimedOut,
    }

    private sealed record ResultVisualState(
        ResultVisualKind Kind,
        string Marker,
        string Label,
        Color Color,
        bool Filled,
        string Detail);

    private sealed record EvidenceTimelineItem(
        string Key,
        string Category,
        TrainingDate Date,
        string Marker,
        Color Color,
        bool Filled,
        IReadOnlyList<string> Chips,
        string? Detail,
        LocalEvidenceArtifactRecord? Artifact = null,
        LocalSessionHistoryRecord? Session = null,
        LocalFormalTestAttemptRecord? FormalAttempt = null,
        LocalStabilizationPassRecord? StabilizationPass = null,
        LocalMaintenanceCheckRecord? MaintenanceCheck = null,
        LocalDecayHistoryRecord? Decay = null,
        LocalRestorationHistoryRecord? Restoration = null);

    public LinearLayout Root { get; }

    public event Action? SessionStartRequested;

    public event Action? LiveSessionStartRequested;

    public event Action<RuntimeInputCommandKind, string?, string?>? LiveSessionCommandRequested;

    public event Action<string>? ActiveSessionInvalidateRequested;

    public event Action? LocalBackupExportRequested;

    public event Action? LocalDataValidateRequested;

    public event Action? LocalBackupValidateRequested;

    public event Action? LocalBackupRestoreRequested;

    public void ShowLoading(MentalGymnasticsAndroidHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        content.RemoveAllViews();
        subtitle.Text = "Work";
        AddPanel(
            "Loading training state",
            "Reading the local app integration state.",
            $"Local store: {System.IO.Path.GetFileName(host.Configuration.LocalDatabasePath)}");
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
        restoreConfirmationArmed = false;
        currentScreen = AndroidShellScreen.Work;
        selectedBranch = null;
        selectedLevel = null;
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
            currentScreen = AndroidShellScreen.LiveSession;
        }
        else
        {
            liveSessionSnapshot = null;
            currentScreen = AndroidShellScreen.Work;
        }

        RenderCurrentScreen();
    }

    public void ShowError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        content.RemoveAllViews();
        subtitle.Text = "Work";
        AddPanel("Unable to load training state", exception.Message, "No progress was changed.");
        ResetScrollPosition();
    }

    public void ShowSessionStartLoading()
    {
        currentScreen = AndroidShellScreen.SessionStart;
        sessionStartSnapshot = null;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        content.RemoveAllViews();
        RenderNavigation();
        subtitle.Text = "Start";
        AddPanel(
            "Preparing session",
            "App workflow is selecting work, generated content, and runtime handoff.",
            "No progress is changed.");
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
            snapshot.LocalData);
        currentScreen = AndroidShellScreen.SessionStart;
        selectedBranch = snapshot.Preparation.Selection.SelectedWork?.Branch;
        selectedLevel = snapshot.Preparation.Selection.SelectedWork?.Level;
        RenderCurrentScreen();
    }

    public void ShowSessionStartError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        currentScreen = AndroidShellScreen.SessionStart;
        sessionStartSnapshot = null;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        content.RemoveAllViews();
        RenderNavigation();
        subtitle.Text = "Start";
        AddPanel("Unable to prepare session", exception.Message, "No progress was changed.");
        ResetScrollPosition();
    }

    public void ShowLiveSessionLoading()
    {
        currentScreen = AndroidShellScreen.LiveSession;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        liveSessionInput = string.Empty;
        content.RemoveAllViews();
        RenderNavigation();
        subtitle.Text = "Live";
        AddPanel(
            "Starting session",
            "Runtime is opening the prepared drill.",
            "No progress is granted by the screen.");
        ResetScrollPosition();
    }

    public void RenderLiveSession(AndroidLiveSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        liveSessionSnapshot = snapshot;
        liveCompletionSnapshot = null;
        currentScreen = AndroidShellScreen.LiveSession;
        RenderCurrentScreen(resetScroll: false);
    }

    public void ShowLiveSessionCompletionLoading(AndroidLiveSessionSnapshot terminalSnapshot)
    {
        ArgumentNullException.ThrowIfNull(terminalSnapshot);

        liveSessionSnapshot = terminalSnapshot;
        liveCompletionSnapshot = null;
        currentScreen = AndroidShellScreen.LiveResult;
        content.RemoveAllViews();
        RenderNavigation();
        subtitle.Text = "Result";
        AddPanel(
            "Processing result",
            "App workflow is recording the terminal runtime session.",
            "Android UI grants no progress.");
        ResetScrollPosition();
    }

    public void RenderLiveSessionCompletion(AndroidLiveSessionCompletionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        liveCompletionSnapshot = snapshot;
        liveSessionSnapshot = null;
        activeSessionResumeSnapshot = null;
        liveSessionInput = string.Empty;
        currentScreen = AndroidShellScreen.LiveResult;
        selectedBranch = snapshot.Completion.SessionState.Branch;
        selectedLevel = snapshot.Completion.SessionState.Level;

        if (snapshot.Completion.WorkflowResult is { } workflowResult)
        {
            this.snapshot = new AndroidTrainingStateSnapshot(
                workflowResult.RefreshedState,
                snapshot.Capabilities,
                snapshot.LocalDatabasePath,
                snapshot.LoadedDate,
                snapshot.LocalData);
        }

        RenderCurrentScreen();
    }

    public void ShowLocalDataLoading(string operation)
    {
        currentScreen = AndroidShellScreen.LocalData;
        content.RemoveAllViews();
        RenderNavigation();
        subtitle.Text = "Local Data";
        AddPanel(
            operation,
            "Using local app and persistence backup primitives.",
            "No sync or remote storage.");
        ResetScrollPosition();
    }

    public void RenderLocalDataOperation(AndroidLocalDataOperationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        this.snapshot = snapshot.TrainingState;
        localDataOperation = snapshot.Operation;
        restoreConfirmationArmed = false;
        currentScreen = AndroidShellScreen.LocalData;
        sessionStartSnapshot = null;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        RenderCurrentScreen();
    }

    public void ShowLocalDataError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        currentScreen = AndroidShellScreen.LocalData;
        content.RemoveAllViews();
        RenderNavigation();
        subtitle.Text = "Local Data";
        AddPanel("Local data operation failed", exception.Message, "No progress was changed.");
        ResetScrollPosition();
    }

    public void ShowLiveSessionError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        currentScreen = AndroidShellScreen.LiveSession;
        content.RemoveAllViews();
        RenderNavigation();
        subtitle.Text = "Live";
        AddPanel("Runtime command failed", exception.Message, "No progress was changed.");
        ResetScrollPosition();
    }

    private View BuildHeader()
    {
        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        header.SetBackgroundColor(MgColors.Surface);
        header.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Md));

        var title = new TextView(context)
        {
            Text = "Mental Gymnastics",
        };
        MgTypography.ApplyTitle(title);
        MgTypography.ApplyLabel(subtitle);

        header.AddView(title);
        header.AddView(subtitle);
        return header;
    }

    private void RenderCurrentScreen(bool resetScroll = true)
    {
        content.RemoveAllViews();
        RenderNavigation();

        if (snapshot is null && currentScreen is not AndroidShellScreen.LiveSession and not AndroidShellScreen.LiveResult)
        {
            if (resetScroll)
            {
                ResetScrollPosition();
            }

            return;
        }

        subtitle.Text = currentScreen switch
        {
            AndroidShellScreen.Work => "Work",
            AndroidShellScreen.Map => "Map",
            AndroidShellScreen.Progress => "Progress",
            AndroidShellScreen.Evidence => "Evidence",
            AndroidShellScreen.Maintenance => "Maintenance",
            AndroidShellScreen.GlobalReview => "Review",
            AndroidShellScreen.LocalData => "Local Data",
            AndroidShellScreen.BranchDetail when selectedBranch is not null => $"Map / {selectedBranch}",
            AndroidShellScreen.SessionStart => "Start",
            AndroidShellScreen.LiveSession => "Live",
            AndroidShellScreen.LiveResult => "Result",
            _ => "Map",
        };

        switch (currentScreen)
        {
            case AndroidShellScreen.Work:
                AddHomeToday(snapshot!);
                AddBranchPreview(snapshot!.CurrentState);
                break;
            case AndroidShellScreen.Map:
                AddBranchLadder(snapshot!.CurrentState);
                break;
            case AndroidShellScreen.Progress:
                AddProgressDashboard(snapshot!.CurrentState);
                break;
            case AndroidShellScreen.Evidence:
                AddEvidenceReview(snapshot!.CurrentState);
                break;
            case AndroidShellScreen.Maintenance:
                AddMaintenanceDecayScreen(snapshot!.CurrentState);
                break;
            case AndroidShellScreen.GlobalReview:
                AddGlobalReviewScreen(snapshot!.CurrentState);
                break;
            case AndroidShellScreen.LocalData:
                AddLocalDataScreen(snapshot!);
                break;
            case AndroidShellScreen.BranchDetail:
                AddBranchDetail(snapshot!.CurrentState, selectedBranch ?? BranchCode.FH, selectedLevel);
                break;
            case AndroidShellScreen.SessionStart:
                AddSessionStart(sessionStartSnapshot);
                break;
            case AndroidShellScreen.LiveSession:
                AddLiveSession(liveSessionSnapshot);
                break;
            case AndroidShellScreen.LiveResult:
                AddLiveResult(liveCompletionSnapshot);
                break;
        }

        if (resetScroll)
        {
            ResetScrollPosition();
        }
    }

    private void ResetScrollPosition()
    {
        scrollView.ScrollTo(0, 0);
        scrollView.Post(() => scrollView.ScrollTo(0, 0));
    }

    private void RenderNavigation()
    {
        navigation.RemoveAllViews();
        AddNavItem(navigation, "Work", "Work", currentScreen == AndroidShellScreen.Work, () =>
        {
            currentScreen = AndroidShellScreen.Work;
            selectedBranch = null;
            selectedLevel = null;
            RenderCurrentScreen();
        });
        AddNavItem(navigation, "Map", "Map", currentScreen is AndroidShellScreen.Map or AndroidShellScreen.BranchDetail, () =>
        {
            currentScreen = AndroidShellScreen.Map;
            selectedBranch = null;
            selectedLevel = null;
            RenderCurrentScreen();
        });
        AddNavItem(navigation, "Prog", "Progress", currentScreen == AndroidShellScreen.Progress, () =>
        {
            currentScreen = AndroidShellScreen.Progress;
            selectedBranch = null;
            selectedLevel = null;
            RenderCurrentScreen();
        });
        AddNavItem(navigation, "Evid", "Evidence", currentScreen == AndroidShellScreen.Evidence, () =>
        {
            currentScreen = AndroidShellScreen.Evidence;
            selectedBranch = null;
            selectedLevel = null;
            RenderCurrentScreen();
        });
        AddNavItem(navigation, "Maint", "Maintenance", currentScreen == AndroidShellScreen.Maintenance, () =>
        {
            currentScreen = AndroidShellScreen.Maintenance;
            selectedBranch = null;
            selectedLevel = null;
            RenderCurrentScreen();
        });
        AddNavItem(navigation, "Rev", "Review", currentScreen == AndroidShellScreen.GlobalReview, () =>
        {
            currentScreen = AndroidShellScreen.GlobalReview;
            selectedBranch = null;
            selectedLevel = null;
            RenderCurrentScreen();
        });
        AddNavItem(navigation, "Data", "Local data", currentScreen == AndroidShellScreen.LocalData, () =>
        {
            currentScreen = AndroidShellScreen.LocalData;
            selectedBranch = null;
            selectedLevel = null;
            RenderCurrentScreen();
        });
    }

    private void AddNavItem(
        LinearLayout nav,
        string visibleLabel,
        string accessibilityLabel,
        bool selected,
        Action? onClick)
    {
        var item = new TextView(context)
        {
            Text = visibleLabel,
            Gravity = GravityFlags.Center,
            ContentDescription = selected ? $"{accessibilityLabel}, selected" : accessibilityLabel,
        };
        item.SetSingleLine(true);
        item.Ellipsize = TextUtils.TruncateAt.End;
        MgTypography.ApplyMicro(item);
        item.SetTextColor(selected ? Color.White : MgColors.InkMuted);
        item.Background = selected
            ? MgTheme.Filled(context, MgColors.Ink, cornerRadius: 8)
            : MgTheme.Outline(context, MgColors.Hairline, cornerRadius: 8);
        item.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Xs),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Xs),
            MgSpacing.Dp(context, MgSpacing.Sm));

        if (onClick is not null)
        {
            item.Clickable = true;
            item.Focusable = true;
            item.Click += (_, _) => onClick();
        }

        var layout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        layout.SetMargins(MgSpacing.Dp(context, 2), 0, MgSpacing.Dp(context, 2), 0);
        nav.AddView(item, layout);
    }

    private void OpenBranchDetail(BranchCode branch, GlobalLevelId? level)
    {
        selectedBranch = branch;
        selectedLevel = level;
        currentScreen = AndroidShellScreen.BranchDetail;
        RenderCurrentScreen();
    }

    private void AddHomeToday(AndroidTrainingStateSnapshot snapshot)
    {
        var state = snapshot.CurrentState;
        var panel = new MgPanel(context);
        panel.ContentDescription = "Home today training state";

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var title = new TextView(context)
        {
            Text = "Today",
        };
        MgTypography.ApplyHeading(title);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));

        header.AddView(new StatusChipView(
            context,
            snapshot.LoadedDate.ToString("MMM d"),
            MgColors.Hairline));
        panel.AddView(header);

        AddActiveSessionSignal(panel);
        AddTodaySignals(panel, state);
        AddNextWorkStack(panel, state);

        var evidenceStrip = new EvidenceStripView(
            context,
            state.RecentSessions.Count,
            state.EvidenceSummaries.Count,
            state.BlockedAdvancement.Count);
        var stripLayout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        stripLayout.SetMargins(0, MgSpacing.Dp(context, MgSpacing.Md), 0, 0);
        panel.AddView(evidenceStrip, stripLayout);

        AddSection(panel);
    }

    private void AddActiveSessionSignal(LinearLayout panel)
    {
        if (liveSessionSnapshot is { LiveSession.IsTerminal: false } live)
        {
            AddSignalRow(
                panel,
                "Resume",
                ColorForLifecycle(live.LiveSession.LifecycleStatus),
                filled: true,
                [
                    FormatBranchLevel(live.LiveSession.Branch, live.LiveSession.Level) ?? "Live",
                    DrillCodeFor(live.LiveSession.Drill),
                    LiveLifecycleLabel(live.LiveSession.LifecycleStatus),
                    "No grant",
                ],
                "Runtime state is active; return to the live session before starting other work.");
            AddActiveSessionButton(
                panel,
                "Resume",
                MgColors.Training,
                () =>
                {
                    currentScreen = AndroidShellScreen.LiveSession;
                    RenderCurrentScreen();
                });
            return;
        }

        var active = activeSessionResumeSnapshot?.ActiveSession;
        if (active is null || active.Status == PreUiActiveSessionResumeStatus.NotFound)
        {
            return;
        }

        var marker = ActiveSessionMarker(active.Status, active.LifecycleStatus);
        var color = ActiveSessionColor(active.Status, active.LifecycleStatus);
        var filled = active.Status is PreUiActiveSessionResumeStatus.Unsafe or
            PreUiActiveSessionResumeStatus.Resumable;

        AddSignalRow(
            panel,
            marker,
            color,
            filled,
            ActiveSessionChips(active),
            ActiveSessionDetail(active));

        if (active.Status == PreUiActiveSessionResumeStatus.Unsafe)
        {
            AddActiveSessionButton(
                panel,
                "Clear",
                MgColors.Blocked,
                () => ActiveSessionInvalidateRequested?.Invoke(active.SessionId));
        }
        else if (active.CanResume && activeSessionResumeSnapshot?.LiveSession is not null)
        {
            AddActiveSessionButton(
                panel,
                "Resume",
                MgColors.Training,
                () =>
                {
                    currentScreen = AndroidShellScreen.LiveSession;
                    RenderCurrentScreen();
                });
        }
    }

    private void AddActiveSessionButton(
        LinearLayout panel,
        string label,
        Color color,
        Action onClick)
    {
        var button = new SessionActionButton(context, label, enabled: true)
        {
            ContentDescription = label == "Clear"
                ? "Clear unsafe active session snapshot. No progress is changed."
                : "Resume active runtime session.",
        };
        button.Background = MgTheme.Filled(context, color);
        button.SetTextColor(Color.White);
        button.Click += (_, _) => onClick();

        var layout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(0, MgSpacing.Dp(context, MgSpacing.Sm), 0, 0);
        panel.AddView(button, layout);
    }

    private void AddTodaySignals(LinearLayout panel, CurrentTrainingStateReadModel state)
    {
        var blocked = state.BlockedAdvancement.ToArray();
        if (blocked.Length > 0)
        {
            AddSignalRow(
                panel,
                "Blocked",
                MgColors.Blocked,
                filled: true,
                ChipsForBlocked(blocked),
                blocked[0].Detail);
        }

        var decayed = state.BranchLevelStates
            .Where(status => status.State == BranchLevelState.Decayed)
            .OrderBy(status => status.Branch)
            .ThenBy(status => status.Level)
            .ToArray();
        if (decayed.Length > 0)
        {
            AddSignalRow(
                panel,
                "Decayed",
                MgColors.Blocked,
                filled: true,
                LimitChips(decayed.Select(FormatBranchLevel), decayed.Length));
        }

        if (state.DueMaintenance.Count > 0)
        {
            AddSignalRow(
                panel,
                "Due",
                MgColors.Maintenance,
                filled: true,
                ChipsForMaintenance(state));
        }

        var recoveryWork = state.AvailableNextWork
            .Where(work => IsRecoverySession(work.Session))
            .ToArray();
        var recoveryConstraint = state.WeeklyPlan.Constraints.FirstOrDefault(
            constraint => constraint.Kind == WeeklyProgrammingConstraintKind.RecoveryRequired);
        if (recoveryWork.Length > 0 || recoveryConstraint is not null)
        {
            AddSignalRow(
                panel,
                "Recovery",
                MgColors.Recovery,
                filled: true,
                ChipsForRecovery(recoveryWork),
                recoveryConstraint?.Detail);
        }

        var deloadConstraint = state.WeeklyPlan.Constraints.FirstOrDefault(
            constraint => constraint.Kind == WeeklyProgrammingConstraintKind.AdvancementTestingSuspended);
        if (deloadConstraint is not null)
        {
            AddSignalRow(
                panel,
                "Deload",
                MgColors.Recovery,
                filled: true,
                ["Tests paused"],
                deloadConstraint.Detail);
        }

        var testReady = state.BranchLevelStates
            .Where(status => status.State == BranchLevelState.TestReady)
            .OrderBy(status => status.Branch)
            .ThenBy(status => status.Level)
            .ToArray();
        if (testReady.Length > 0)
        {
            AddSignalRow(
                panel,
                "Test",
                MgColors.TestReady,
                filled: false,
                LimitChips(testReady.Select(FormatBranchLevel), testReady.Length));
        }
    }

    private void AddNextWorkStack(LinearLayout panel, CurrentTrainingStateReadModel state)
    {
        var label = new TextView(context)
        {
            Text = "Next Work",
        };
        MgTypography.ApplyLabel(label);
        var labelLayout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        labelLayout.SetMargins(0, MgSpacing.Dp(context, MgSpacing.Md), 0, 0);
        panel.AddView(label, labelLayout);

        var workItems = state.AvailableNextWork
            .OrderBy(work => work.DayNumber)
            .Take(3)
            .ToArray();
        if (workItems.Length == 0)
        {
            AddSignalRow(
                panel,
                "No work",
                MgColors.Hairline,
                filled: false,
                ["App state"]);
        }
        else
        {
            foreach (var work in workItems)
            {
                AddSignalRow(
                    panel,
                    work == workItems[0] ? "Next" : $"D{work.DayNumber}",
                    ColorForSession(work.Session),
                    filled: work == workItems[0],
                    ChipsForNextWork(work));
            }
        }

        AddPrepareSessionButton(panel);
    }

    private void AddBranchPreview(CurrentTrainingStateReadModel state)
    {
        var title = new TextView(context)
        {
            Text = "Branch Map",
        };
        MgTypography.ApplyLabel(title);
        AddSection(title);

        foreach (var branchGroup in state.BranchLevelStates.GroupBy(level => level.Branch).OrderBy(group => group.Key))
        {
            var branch = branchGroup.Key;
            var tile = new BranchTileView(context, branch, branchGroup);
            tile.Clickable = true;
            tile.Focusable = true;
            tile.Click += (_, _) => OpenBranchDetail(branch, level: null);
            AddSection(tile);
        }
    }

    private void AddBranchLadder(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Branch ladder map",
        };

        var title = new TextView(context)
        {
            Text = "Branch Ladder",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        AddTodaySignals(panel, state);
        AddLegend(panel);
        AddLevelHeader(panel);

        foreach (var branch in ProgramCatalog.Branches)
        {
            AddLadderRow(panel, state, branch);
        }

        AddSection(panel);
    }

    private void AddProgressDashboard(CurrentTrainingStateReadModel state)
    {
        AddProgressOverview(state);
        AddProgressBranchBoard(state);
        AddProgressMaintenanceAndDecay(state);
        AddProgressFailuresAndEvidence(state);
        AddProgressBalanceAndTransfer(state);
        AddProgressProgramming(state);
    }

    private void AddProgressOverview(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Progress dashboard local training state",
        };

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var title = new TextView(context)
        {
            Text = "Progress",
        };
        MgTypography.ApplyHeading(title);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        header.AddView(new StatusChipView(
            context,
            state.CategoryClassification.Category.ToString(),
            MgColors.Hairline));
        panel.AddView(header);

        var owned = HighestOwnedStatuses(state);
        AddSignalRow(
            panel,
            "Owned",
            MgColors.Owned,
            filled: false,
            owned.Count == 0
                ? ["None"]
                : LimitChips(owned.Select(FormatBranchLevel), owned.Count));

        var passedOnce = StatusesByState(state, BranchLevelState.PassedOnce);
        if (passedOnce.Count > 0)
        {
            AddSignalRow(
                panel,
                "1/3",
                MgColors.PassedOnce,
                filled: false,
                LimitChips(passedOnce.Select(FormatBranchLevel), passedOnce.Count),
                "Passed once is not ownership.");
        }

        var stabilizing = StatusesByState(state, BranchLevelState.Stabilizing);
        if (stabilizing.Count > 0)
        {
            AddSignalRow(
                panel,
                "Stab",
                MgColors.TestReady,
                filled: false,
                LimitChips(
                    stabilizing.Select(status => $"{FormatBranchLevel(status)} {StabilizationProgressLabel(state, status)}"),
                    stabilizing.Count));
        }

        var testReady = StatusesByState(state, BranchLevelState.TestReady);
        if (testReady.Count > 0)
        {
            AddSignalRow(
                panel,
                "Test",
                MgColors.TestReady,
                filled: false,
                LimitChips(testReady.Select(FormatBranchLevel), testReady.Count));
        }

        if (state.ProgressRecords.LatestSummary?.BottleneckBranch is { } bottleneck)
        {
            AddSignalRow(
                panel,
                "Bottleneck",
                MgColors.Recovery,
                filled: true,
                [bottleneck.ToString(), "Local summary"]);
        }

        AddSection(panel);
    }

    private void AddProgressBranchBoard(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Progress branch board",
        };

        var title = new TextView(context)
        {
            Text = "Branch Board",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        AddLegend(panel);
        AddLevelHeader(panel);
        foreach (var branch in ProgramCatalog.Branches)
        {
            AddLadderRow(panel, state, branch);
        }

        AddSection(panel);
    }

    private void AddProgressMaintenanceAndDecay(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Progress maintenance and decay",
        };

        var title = new TextView(context)
        {
            Text = "Maintenance",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        var decayed = StatusesByState(state, BranchLevelState.Decayed);
        foreach (var status in decayed.Take(3))
        {
            AddSignalRow(
                panel,
                "Decay",
                MgColors.Blocked,
                filled: true,
                [
                    FormatBranchLevel(status),
                    "Restore",
                    "No advance",
                ]);
        }

        foreach (var due in state.DueMaintenance
                     .OrderBy(record => MaintenancePriority(record.Currency.State))
                     .ThenBy(record => record.BranchLevel.Branch)
                     .ThenBy(record => record.BranchLevel.Level)
                     .Take(4))
        {
            AddSignalRow(
                panel,
                MarkerForMaintenanceState(due.Currency.State),
                ColorForMaintenanceState(due.Currency.State),
                filled: due.Currency.State != MaintenanceCurrencyState.Current,
                [
                    FormatBranchLevel(due.BranchLevel),
                    due.Currency.State.ToString(),
                    DaysSinceMaintenancePass(due.Currency),
                ],
                due.Currency.ConsecutiveFailures > 0 ? $"fail {due.Currency.ConsecutiveFailures}" : null);
        }

        if (decayed.Count == 0 && state.DueMaintenance.Count == 0)
        {
            var currentMaintenance = state.ProgressRecords.LatestSummary?.MaintenanceSummaries
                .Where(summary => summary.State == MaintenanceCurrencyState.Current)
                .OrderBy(summary => summary.Branch)
                .ThenBy(summary => summary.OwnedLevel)
                .Take(3)
                .ToArray();

            if (currentMaintenance is { Length: > 0 })
            {
                foreach (var maintenance in currentMaintenance)
                {
                    AddSignalRow(
                        panel,
                        "Maint",
                        MgColors.Maintenance,
                        filled: false,
                        [
                            FormatBranchLevel(maintenance.Branch, maintenance.OwnedLevel) ?? maintenance.Branch.ToString(),
                            maintenance.State.ToString(),
                            maintenance.DaysSinceLastPassingCheck is { } days ? $"{days}d" : "No pass",
                        ]);
                }
            }
            else
            {
                AddSignalRow(
                    panel,
                    "Maint",
                    MgColors.Hairline,
                    filled: false,
                    ["No due"]);
            }
        }

        foreach (var decay in state.ProgressRecords.DecayHistory.Take(2))
        {
            AddSignalRow(
                panel,
                "History",
                MgColors.Blocked,
                filled: true,
                [
                    FormatBranchLevel(decay.NextStatus),
                    FormatDate(decay.Date),
                    "Decay",
                ]);
        }

        foreach (var restoration in state.ProgressRecords.RestorationHistory.Take(2))
        {
            AddSignalRow(
                panel,
                "Restore",
                MgColors.Maintenance,
                filled: false,
                [
                    FormatBranchLevel(restoration.NextStatus),
                    FormatDate(restoration.Date),
                    StateMarkerView.LabelTextFor(restoration.NextStatus.State),
                ]);
        }

        AddSection(panel);
    }

    private void AddProgressFailuresAndEvidence(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Progress failures and evidence",
        };

        var title = new TextView(context)
        {
            Text = "Evidence",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        var failures = state.ProgressRecords.FormalTestAttempts
            .Where(record => record.Attempt.PassState == FormalTestPassState.Fail)
            .Take(3)
            .ToArray();
        if (failures.Length == 0)
        {
            AddSignalRow(
                panel,
                "Failures",
                MgColors.Hairline,
                filled: false,
                ["No recent formal fail"]);
        }
        else
        {
            foreach (var failure in failures)
            {
                AddSignalRow(
                    panel,
                    "Fail",
                    MgColors.Blocked,
                    filled: true,
                    [
                        FormatBranchLevel(failure.Attempt.Branch, failure.Attempt.Level) ?? "Formal",
                        failure.Attempt.FailureType?.ToString() ?? "Unclassified",
                        FormatDate(failure.Attempt.Date),
                    ],
                    ShortText(failure.Attempt.Artifact.SummaryOrReference, 72));
            }
        }

        foreach (var attempt in state.ProgressRecords.FormalTestAttempts
                     .Where(record => record.Attempt.PassState != FormalTestPassState.Fail)
                     .Take(3))
        {
            AddSignalRow(
                panel,
                FormatPassState(attempt.Attempt.PassState),
                ColorForPassState(attempt.Attempt.PassState),
                filled: attempt.Attempt.PassState == FormalTestPassState.Owned,
                [
                    FormatBranchLevel(attempt.Attempt.Branch, attempt.Attempt.Level) ?? "Formal",
                    FormatDate(attempt.Attempt.Date),
                    attempt.Attempt.Task.TransferTask is null ? "Drill" : "Transfer",
                ]);
        }

        AddInlineChipRow(
            panel,
            [
                $"{state.RecentSessions.Count} sessions",
                $"{state.EvidenceSummaries.Count} artifacts",
                $"{state.ProgressRecords.StabilizationPasses.Count} stab",
                $"{state.ProgressRecords.MaintenanceChecks.Count} maint",
            ],
            MgColors.Hairline);

        AddSection(panel);
    }

    private void AddProgressBalanceAndTransfer(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Progress balance and transfer",
        };

        var title = new TextView(context)
        {
            Text = "Balance / Transfer",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        var balance = state.BlockedAdvancement
            .Where(blocker => blocker.Source == CurrentTrainingStateBlockerSource.GlobalBalance)
            .ToArray();
        if (balance.Length == 0)
        {
            AddSignalRow(
                panel,
                "Balance",
                MgColors.Hairline,
                filled: false,
                ["No active cap"]);
        }
        else
        {
            foreach (var blocker in balance.Take(3))
            {
                AddSignalRow(
                    panel,
                    "Balance",
                    MgColors.Blocked,
                    filled: true,
                    [
                        FormatBranchLevel(blocker.Branch, blocker.Level) ?? "Global",
                        FormatBlockerSource(blocker.Source),
                    ],
                    blocker.Detail);
            }
        }

        var dependencyCaps = state.BlockedAdvancement
            .Where(blocker => blocker.Source == CurrentTrainingStateBlockerSource.DependencyCap)
            .Take(3)
            .ToArray();
        foreach (var cap in dependencyCaps)
        {
            AddSignalRow(
                panel,
                "Cap",
                MgColors.Blocked,
                filled: true,
                [
                    FormatBranchLevel(cap.Branch, cap.Level) ?? "Dependency",
                    cap.DependencyCapReason?.ToString() ?? "Cap",
                ],
                cap.Detail);
        }

        var transferWork = state.AvailableNextWork
            .Where(work => IsTransferSession(work.Session))
            .Take(3)
            .ToArray();
        foreach (var work in transferWork)
        {
            AddSignalRow(
                panel,
                "Transfer",
                MgColors.Transfer,
                filled: work.IsAdvancementWork,
                ChipsForNextWork(work));
        }

        var transferAttempts = state.ProgressRecords.FormalTestAttempts
            .Where(record => record.Attempt.Task.TransferTask is not null)
            .Take(3)
            .ToArray();
        if (transferWork.Length == 0 && transferAttempts.Length == 0)
        {
            AddSignalRow(
                panel,
                "Transfer",
                MgColors.Hairline,
                filled: false,
                ["No current"]);
        }
        else
        {
            foreach (var attempt in transferAttempts)
            {
                AddSignalRow(
                    panel,
                    FormatPassState(attempt.Attempt.PassState),
                    ColorForPassState(attempt.Attempt.PassState),
                    filled: attempt.Attempt.PassState == FormalTestPassState.Fail,
                    [
                        FormatBranchLevel(attempt.Attempt.Branch, attempt.Attempt.Level) ?? "Transfer",
                        FormatDate(attempt.Attempt.Date),
                    ],
                    attempt.Attempt.Task.TransferTask);
            }
        }

        AddSection(panel);
    }

    private void AddProgressProgramming(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Progress programming state",
        };

        var title = new TextView(context)
        {
            Text = "Programming",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        if (state.ProgressRecords.LatestSummary is { } summary)
        {
            AddSignalRow(
                panel,
                "Emphasis",
                ColorForProgressEmphasis(summary.NextProgrammedEmphasis.Kind),
                filled: summary.NextProgrammedEmphasis.Kind is
                    LocalProgressEmphasisKind.RestoreDecayedBranch or
                    LocalProgressEmphasisKind.ResolveMaintenanceBlocker,
                ChipsForProgrammedEmphasis(summary.NextProgrammedEmphasis));

            AddSignalRow(
                panel,
                "Records",
                MgColors.Hairline,
                filled: false,
                [
                    $"{summary.FormalAttemptCount} formal",
                    $"{summary.EvidenceArtifactCount} artifacts",
                    FormatDate(summary.GeneratedOn),
                ]);
        }
        else
        {
            AddSignalRow(
                panel,
                "Summary",
                MgColors.Hairline,
                filled: false,
                ["No local summary"]);
        }

        if (state.WeeklyPlan.Constraints.Count == 0)
        {
            AddSignalRow(
                panel,
                "Week",
                MgColors.Hairline,
                filled: false,
                [state.WeeklyPlan.AdvancementWorkAllowed ? "Gates allowed" : "No gates"]);
        }
        else
        {
            foreach (var constraint in state.WeeklyPlan.Constraints.Take(3))
            {
                AddSignalRow(
                    panel,
                    "Week",
                    constraint.Kind == WeeklyProgrammingConstraintKind.MaintenanceNotCurrent
                        ? MgColors.Maintenance
                        : MgColors.Recovery,
                    filled: true,
                    [
                        constraint.Kind.ToString(),
                        constraint.Branch?.ToString() ?? "Global",
                    ],
                    constraint.Detail);
            }
        }

        AddSection(panel);
    }

    private void AddMaintenanceDecayScreen(CurrentTrainingStateReadModel state)
    {
        AddMaintenancePriorityBoard(state);
        AddMaintenanceDueChecks(state);
        AddMaintenanceDecayRestoration(state);
        AddMaintenanceDependencyCaps(state);
        AddMaintenanceAllowedWork(state);
        AddMaintenanceEvidenceHistory(state);
    }

    private void AddGlobalReviewScreen(CurrentTrainingStateReadModel state)
    {
        AddGlobalReviewDecisionBoard(state);
        AddGlobalReviewOwnedMaintenance(state);
        AddGlobalReviewBottleneckProgramming(state);
        AddGlobalReviewEvidenceFailureInputs(state);
        AddGlobalReviewLoadRecoveryInputs(state);
    }

    private void AddGlobalReviewDecisionBoard(CurrentTrainingStateReadModel state)
    {
        var review = state.GlobalReview;
        var panel = new MgPanel(context)
        {
            ContentDescription = "Core global review decision",
        };

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var title = new TextView(context)
        {
            Text = "Global Review",
        };
        MgTypography.ApplyHeading(title);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        header.AddView(new StatusChipView(
            context,
            review.Evaluation.Passed ? "Pass" : "Blocked",
            review.Evaluation.Passed ? MgColors.Owned : MgColors.Blocked));
        panel.AddView(header);

        AddSignalRow(
            panel,
            review.Evaluation.Passed ? "Pass" : "Blocked",
            review.Evaluation.Passed ? MgColors.Owned : MgColors.Blocked,
            filled: !review.Evaluation.Passed,
            [
                PascalWords(review.Input.PractitionerCategory.ToString()),
                $"{review.Evaluation.Failures.Count} fails",
                $"{review.Evaluation.Decisions.Count} decisions",
            ],
            "Core review result; no free-form self-assessment.");

        foreach (var failure in review.Evaluation.Failures.Take(4))
        {
            AddSignalRow(
                panel,
                "Fail",
                ColorForGlobalReviewFailure(failure.Kind),
                filled: true,
                [
                    FormatGlobalReviewFailure(failure.Kind),
                    FormatBranchLevel(failure.Branch, failure.Level) ?? "Global",
                ],
                failure.Detail);
        }

        if (review.Evaluation.Decisions.Count == 0)
        {
            AddSignalRow(
                panel,
                "Decision",
                MgColors.Hairline,
                filled: false,
                ["No pass decision"]);
        }
        else
        {
            foreach (var decision in review.Evaluation.Decisions.Take(4))
            {
                AddSignalRow(
                    panel,
                    "Decision",
                    ColorForGlobalReviewDecision(decision.Kind),
                    filled: decision.Kind is GlobalReviewDecisionKind.RestoreDecayedBranch or
                        GlobalReviewDecisionKind.PauseTestsForDeload,
                    [
                        FormatGlobalReviewDecision(decision.Kind),
                        decision.Branch?.ToString() ?? "Global",
                    ],
                    decision.Detail);
            }
        }

        AddSection(panel);
    }

    private void AddGlobalReviewOwnedMaintenance(CurrentTrainingStateReadModel state)
    {
        var review = state.GlobalReview.Input;
        var panel = new MgPanel(context)
        {
            ContentDescription = "Global review owned levels and maintenance status",
        };

        var title = new TextView(context)
        {
            Text = "Owned / Current",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        foreach (var owned in review.CurrentOwnedLevels)
        {
            if (owned.OwnedLevel is not { } ownedLevel)
            {
                AddSignalRow(
                    panel,
                    "None",
                    MgColors.Hairline,
                    filled: false,
                    [owned.Branch.ToString(), "No owned level"]);
                continue;
            }

            var status = GetStatus(state, owned.Branch, ownedLevel);
            AddSignalRow(
                panel,
                StateMarkerView.LabelTextFor(status.State),
                ColorForState(status.State),
                filled: status.State == BranchLevelState.Decayed,
                [
                    owned.Branch.ToString(),
                    ownedLevel.ToString(),
                    status.State == BranchLevelState.Decayed ? "Restore" : "Owned input",
                ]);
        }

        AddDivider(panel);

        if (review.MaintenanceStatus.Count == 0)
        {
            AddSignalRow(
                panel,
                "Missing",
                MgColors.Blocked,
                filled: true,
                ["No maintenance input"]);
        }
        else
        {
            foreach (var maintenance in review.MaintenanceStatus.Take(8))
            {
                AddSignalRow(
                    panel,
                    MarkerForMaintenanceState(maintenance.State),
                    ColorForMaintenanceState(maintenance.State),
                    filled: maintenance.State is MaintenanceCurrencyState.Due or
                        MaintenanceCurrencyState.Warning or
                        MaintenanceCurrencyState.Failed,
                    [
                        FormatBranchLevel(maintenance.Branch, maintenance.OwnedLevel) ?? maintenance.Branch.ToString(),
                        maintenance.State.ToString(),
                        DaysSinceMaintenancePass(maintenance),
                    ]);
            }
        }

        AddSection(panel);
    }

    private void AddGlobalReviewBottleneckProgramming(CurrentTrainingStateReadModel state)
    {
        var review = state.GlobalReview.Input;
        var panel = new MgPanel(context)
        {
            ContentDescription = "Global review bottleneck and programmed response",
        };

        var title = new TextView(context)
        {
            Text = "Bottleneck",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        if (review.Bottleneck is null)
        {
            AddSignalRow(
                panel,
                "Missing",
                MgColors.Blocked,
                filled: true,
                [
                    state.ProgressRecords.LatestSummary?.BottleneckBranch?.ToString() ?? "No branch",
                    "No Core kind",
                ],
                "Core review requires a classified bottleneck with programmed response.");
        }
        else
        {
            AddSignalRow(
                panel,
                review.Bottleneck.HasProgrammedResponse ? "Response" : "Missing",
                review.Bottleneck.HasProgrammedResponse ? MgColors.Recovery : MgColors.Blocked,
                filled: !review.Bottleneck.HasProgrammedResponse,
                [
                    review.Bottleneck.Branch.ToString(),
                    FormatBottleneck(review.Bottleneck.Bottleneck),
                    review.Bottleneck.NeedsEmphasis ? "Emphasis" : "Tracked",
                ]);
        }

        if (state.ProgressRecords.LatestSummary is { } summary)
        {
            AddSignalRow(
                panel,
                "Program",
                ColorForProgressEmphasis(summary.NextProgrammedEmphasis.Kind),
                filled: summary.NextProgrammedEmphasis.Kind is
                    LocalProgressEmphasisKind.RestoreDecayedBranch or
                    LocalProgressEmphasisKind.ResolveMaintenanceBlocker,
                ChipsForProgrammedEmphasis(summary.NextProgrammedEmphasis));
        }
        else
        {
            AddSignalRow(
                panel,
                "Summary",
                MgColors.Hairline,
                filled: false,
                ["No progress summary"]);
        }

        foreach (var constraint in state.WeeklyPlan.Constraints.Take(4))
        {
            AddSignalRow(
                panel,
                constraint.Kind == WeeklyProgrammingConstraintKind.AdvancementTestingSuspended ? "Deload" : "Guard",
                constraint.Kind == WeeklyProgrammingConstraintKind.MaintenanceNotCurrent
                    ? MgColors.Maintenance
                    : MgColors.Recovery,
                filled: true,
                [
                    PascalWords(constraint.Kind.ToString()),
                    constraint.Branch?.ToString() ?? "Global",
                ],
                constraint.Detail);
        }

        AddSection(panel);
    }

    private void AddGlobalReviewEvidenceFailureInputs(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Global review evidence and failure inputs",
        };

        var title = new TextView(context)
        {
            Text = "Evidence / Fails",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        var failedAttempts = state.ProgressRecords.FormalTestAttempts
            .Where(record => record.Attempt.FailureType is not null)
            .OrderByDescending(record => record.Attempt.Date.Year)
            .ThenByDescending(record => record.Attempt.Date.Month)
            .ThenByDescending(record => record.Attempt.Date.Day)
            .Take(3)
            .ToArray();

        if (failedAttempts.Length == 0)
        {
            AddSignalRow(
                panel,
                "Fails",
                MgColors.Hairline,
                filled: false,
                ["No formal failures"]);
        }
        else
        {
            foreach (var record in failedAttempts)
            {
                AddSignalRow(
                    panel,
                    "Fail",
                    MgColors.Blocked,
                    filled: true,
                    [
                        FormatBranchLevel(record.Attempt.Branch, record.Attempt.Level) ?? "Formal",
                        PascalWords(record.Attempt.FailureType!.Value.ToString()),
                        FormatDate(record.Attempt.Date),
                    ],
                    $"{record.Attempt.ResultEvidence.Kind}: {record.Attempt.ResultEvidence.Value}");
            }
        }

        AddDivider(panel);

        var artifacts = state.EvidenceSummaries.Take(6).ToArray();
        if (artifacts.Length == 0)
        {
            AddSignalRow(
                panel,
                "Evidence",
                MgColors.Blocked,
                filled: true,
                ["No artifacts"],
                "Global review cannot pass on vague reflection.");
        }
        else
        {
            foreach (var artifact in artifacts)
            {
                AddSignalRow(
                    panel,
                    MarkerForEvidenceArtifactCategory(artifact.Artifact.Category),
                    ColorForEvidenceArtifactCategory(artifact.Artifact.Category),
                    filled: ArtifactHasProblemMarker(artifact.Artifact),
                    [
                        FormatEvidenceArtifactCategory(artifact.Artifact.Category),
                        FormatEvidenceBranchLevel(artifact.Event, FindSessionForArtifact(state, artifact)),
                        FormatDate(artifact.Artifact.Date),
                    ],
                    ShortText(artifact.Artifact.SummaryOrReference, 96));
            }
        }

        AddSection(panel);
    }

    private void AddGlobalReviewLoadRecoveryInputs(CurrentTrainingStateReadModel state)
    {
        var review = state.GlobalReview.Input;
        var panel = new MgPanel(context)
        {
            ContentDescription = "Global review volume intensity recovery and deload history",
        };

        var title = new TextView(context)
        {
            Text = "Load / Recovery",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        if (review.VolumeAndIntensityHistory.Count == 0)
        {
            AddSignalRow(
                panel,
                "Volume",
                MgColors.Blocked,
                filled: true,
                ["No volume input"]);
        }
        else
        {
            foreach (var record in review.VolumeAndIntensityHistory.Take(8))
            {
                AddSignalRow(
                    panel,
                    MarkerForTrainingIntensity(record.Intensity),
                    ColorForTrainingIntensity(record.Intensity),
                    filled: record.Intensity == TrainingIntensityKind.High,
                    [
                        record.Branch.ToString(),
                        $"{record.WorkingSets} sets",
                        PascalWords(record.Intensity.ToString()),
                    ]);
            }
        }

        AddDivider(panel);

        if (review.RecoveryOrDeloadHistory.Count == 0)
        {
            AddSignalRow(
                panel,
                "Recovery",
                MgColors.Blocked,
                filled: true,
                ["No recovery/deload"]);
        }
        else
        {
            foreach (var record in review.RecoveryOrDeloadHistory.Take(6))
            {
                AddSignalRow(
                    panel,
                    record.WasDeload ? "Deload" : "Recover",
                    MgColors.Recovery,
                    filled: record.WasDeload,
                    [
                        FormatDate(record.Date),
                        record.WasDeload ? "Tests paused" : "Recovery",
                    ]);
            }
        }

        AddSection(panel);
    }

    private void AddLocalDataScreen(AndroidTrainingStateSnapshot snapshot)
    {
        AddLocalDataStatusPanel(snapshot);
        AddLocalBackupPanel(snapshot.LocalData);
        AddLocalDataOperationPanel();
        AddLocalDataControlPanel(snapshot.LocalData);
    }

    private void AddLocalDataStatusPanel(AndroidTrainingStateSnapshot snapshot)
    {
        var localData = snapshot.LocalData;
        var panel = new MgPanel(context)
        {
            ContentDescription = "Local backup and restore status",
        };

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var title = new TextView(context)
        {
            Text = "Local Data",
        };
        MgTypography.ApplyHeading(title);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        header.AddView(new StatusChipView(context, "Offline", MgColors.Owned));
        panel.AddView(header);

        AddSignalRow(
            panel,
            "Store",
            MgColors.Hairline,
            filled: false,
            [
                System.IO.Path.GetFileName(localData.LocalDatabasePath),
                "App-owned",
                "JSON",
            ],
            ShortText(localData.LocalDatabasePath, 96));

        AddSignalRow(
            panel,
            localData.CurrentIntegrity.IsValid ? "Valid" : "Invalid",
            localData.CurrentIntegrity.IsValid ? MgColors.Owned : MgColors.Blocked,
            filled: !localData.CurrentIntegrity.IsValid,
            [
                localData.CurrentIntegrity.IsValid ? "Integrity pass" : $"{localData.CurrentIntegrity.Issues.Count} issues",
                snapshot.Capabilities.AllowsSync ? "Sync?" : "No sync",
                snapshot.Capabilities.AllowsBackendServices ? "Backend?" : "No backend",
            ],
            localData.CurrentIntegrity.IsValid
                ? "Current local data passed persistence validation."
                : LocalIntegrityIssueDetail(localData.CurrentIntegrity));

        AddSection(panel);
    }

    private void AddLocalBackupPanel(LocalDataBackupReadModel localData)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Latest local backup package",
        };

        var title = new TextView(context)
        {
            Text = "Latest Backup",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        if (localData.LatestBackup is null)
        {
            AddSignalRow(
                panel,
                "None",
                MgColors.Hairline,
                filled: false,
                ["No local backup"],
                ShortText(localData.BackupDirectoryPath, 96));
        }
        else
        {
            var backup = localData.LatestBackup;
            AddSignalRow(
                panel,
                backup.IsReadableBackup ? "Readable" : "Invalid",
                backup.IsReadableBackup ? MgColors.Owned : MgColors.Blocked,
                filled: !backup.IsReadableBackup,
                [
                    backup.FileName,
                    FormatByteSize(backup.SizeBytes),
                    backup.DatabaseSchemaVersion is null ? "No schema" : $"Schema {backup.DatabaseSchemaVersion}",
                ],
                backup.Detail);

            AddSignalRow(
                panel,
                "Local",
                MgColors.Hairline,
                filled: false,
                [
                    backup.StorageOwnership?.ToString() ?? "Unknown",
                    backup.Connectivity?.ToString() ?? "Unknown",
                    FormatDateTime(backup.LastModifiedUtc),
                ],
                ShortText(backup.FilePath, 96));
        }

        AddSection(panel);
    }

    private void AddLocalDataOperationPanel()
    {
        if (localDataOperation is null)
        {
            return;
        }

        var panel = new MgPanel(context)
        {
            ContentDescription = "Last local data operation",
        };

        var title = new TextView(context)
        {
            Text = "Last Action",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        AddSignalRow(
            panel,
            FormatLocalDataOperationStatus(localDataOperation.Status),
            ColorForLocalDataOperation(localDataOperation.Status),
            filled: localDataOperation.Status is LocalDataBackupOperationStatus.Failed or
                LocalDataBackupOperationStatus.ConfirmationRequired,
            [
                FormatLocalDataOperation(localDataOperation.Kind),
                localDataOperation.BackupFile?.FileName ?? "No file",
                localDataOperation.CurrentIntegrity?.IsValid == false ? "Integrity issue" : "Validated",
            ],
            localDataOperation.Detail);

        if (localDataOperation.CurrentIntegrity is { IsValid: false } integrity)
        {
            AddSignalRow(
                panel,
                "Issue",
                MgColors.Blocked,
                filled: true,
                [$"{integrity.Issues.Count} issues"],
                LocalIntegrityIssueDetail(integrity));
        }

        AddSection(panel);
    }

    private void AddLocalDataControlPanel(LocalDataBackupReadModel localData)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Local backup restore controls",
        };

        var title = new TextView(context)
        {
            Text = "Controls",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        AddSignalRow(
            panel,
            "Guard",
            MgColors.Hairline,
            filled: false,
            ["Local only", "No account", "No upload"],
            "Restore replaces local data only after persistence restore validation.");

        var firstRow = AddButtonRow(panel);
        AddLocalDataActionButton(
            firstRow,
            "Export",
            MgColors.Training,
            () => LocalBackupExportRequested?.Invoke());
        AddLocalDataActionButton(
            firstRow,
            "Validate Data",
            MgColors.Owned,
            () => LocalDataValidateRequested?.Invoke());

        var secondRow = AddButtonRow(panel);
        AddLocalDataActionButton(
            secondRow,
            "Validate Backup",
            MgColors.Recovery,
            () => LocalBackupValidateRequested?.Invoke(),
            enabled: localData.LatestBackup is not null);
        AddLocalDataActionButton(
            secondRow,
            restoreConfirmationArmed ? "Restore" : "Arm Restore",
            MgColors.Blocked,
            () =>
            {
                if (!restoreConfirmationArmed)
                {
                    restoreConfirmationArmed = true;
                    RenderCurrentScreen();
                    return;
                }

                LocalBackupRestoreRequested?.Invoke();
            },
            enabled: localData.LatestBackup is not null);

        if (restoreConfirmationArmed)
        {
            AddSignalRow(
                panel,
                "Replace",
                MgColors.Blocked,
                filled: true,
                ["Second tap required", "Validation enforced"],
                "Restore will replace the current app-owned local database with the latest local backup.");
        }

        AddSection(panel);
    }

    private void AddLocalDataActionButton(
        LinearLayout parent,
        string label,
        Color color,
        Action onClick,
        bool enabled = true)
    {
        var button = new SessionActionButton(context, label, enabled)
        {
            ContentDescription = enabled ? label : $"{label} unavailable",
        };
        button.Background = enabled
            ? MgTheme.Filled(context, color)
            : MgTheme.Outline(context, color);
        button.SetTextColor(enabled ? Color.White : MgColors.InkMuted);

        if (enabled)
        {
            button.Click += (_, _) => onClick();
        }

        var layout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        layout.SetMargins(0, 0, MgSpacing.Dp(context, MgSpacing.Sm), MgSpacing.Dp(context, MgSpacing.Sm));
        parent.AddView(button, layout);
    }

    private void AddMaintenancePriorityBoard(CurrentTrainingStateReadModel state)
    {
        var decayed = ActiveDecayedStatuses(state);
        var dependencyCaps = DependencyCapBlockers(state);
        var warnings = state.DueMaintenance.Count(record => record.Currency.State == MaintenanceCurrencyState.Warning);
        var failed = state.DueMaintenance.Count(record => record.Currency.State == MaintenanceCurrencyState.Failed);
        var due = state.DueMaintenance.Count(record => record.Currency.State == MaintenanceCurrencyState.Due);

        var panel = new MgPanel(context)
        {
            ContentDescription = "Maintenance and decay priority summary",
        };

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var title = new TextView(context)
        {
            Text = "Maintenance",
        };
        MgTypography.ApplyHeading(title);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        header.AddView(new StatusChipView(context, "Local", MgColors.Hairline));
        panel.AddView(header);

        if (decayed.Count > 0)
        {
            AddSignalRow(
                panel,
                "Decayed",
                MgColors.Blocked,
                filled: true,
                LimitChips(decayed.Select(FormatBranchLevel), decayed.Count),
                "Dependent advancement remains capped until restoration evidence exists.");
        }

        if (failed > 0)
        {
            AddSignalRow(
                panel,
                "Failed",
                MgColors.Blocked,
                filled: true,
                [$"{failed} checks", "Decay route"]);
        }

        if (warnings > 0)
        {
            AddSignalRow(
                panel,
                "Warn",
                MgColors.Maintenance,
                filled: true,
                [$"{warnings} branch-levels", "Failed once"]);
        }

        if (due > 0)
        {
            AddSignalRow(
                panel,
                "Due",
                MgColors.Maintenance,
                filled: true,
                [$"{due} checks", "Overdue"]);
        }

        if (dependencyCaps.Count > 0)
        {
            AddSignalRow(
                panel,
                "Cap",
                MgColors.Blocked,
                filled: true,
                LimitChips(dependencyCaps.Select(blocker => FormatBranchLevel(blocker.Branch, blocker.Level) ?? "Dependency"), dependencyCaps.Count),
                dependencyCaps[0].Detail);
        }

        if (decayed.Count == 0 && state.DueMaintenance.Count == 0 && dependencyCaps.Count == 0)
        {
            AddSignalRow(
                panel,
                "Clear",
                MgColors.Hairline,
                filled: false,
                ["No due checks", "No decay", "No caps"]);
        }

        AddSignalRow(
            panel,
            "Guard",
            MgColors.Hairline,
            filled: false,
            ["Evidence only", "No manual current"],
            "This screen only renders app/core/persistence outputs.");

        AddSection(panel);
    }

    private void AddMaintenanceDueChecks(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Due maintenance checks",
        };

        var title = new TextView(context)
        {
            Text = "Checks",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        var records = state.DueMaintenance
            .OrderBy(record => MaintenancePriority(record.Currency.State))
            .ThenBy(record => record.BranchLevel.Branch)
            .ThenBy(record => record.BranchLevel.Level)
            .ToArray();

        if (records.Length == 0)
        {
            AddSignalRow(
                panel,
                "None",
                MgColors.Hairline,
                filled: false,
                ["No due maintenance"]);
        }
        else
        {
            foreach (var record in records)
            {
                AddSignalRow(
                    panel,
                    MarkerForMaintenanceState(record.Currency.State),
                    ColorForMaintenanceState(record.Currency.State),
                    filled: record.Currency.State != MaintenanceCurrencyState.Current,
                    [
                        FormatBranchLevel(record.BranchLevel),
                        record.Currency.State.ToString(),
                        FormatMaintenanceCadence(record.Currency.Cadence),
                    ],
                    MaintenanceCurrencyDetail(record.Currency));
            }
        }

        AddSection(panel);
    }

    private void AddMaintenanceDecayRestoration(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Decay and restoration requirements",
        };

        var title = new TextView(context)
        {
            Text = "Decay / Restore",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        var decayed = ActiveDecayedStatuses(state);
        if (decayed.Count == 0)
        {
            AddSignalRow(
                panel,
                "Decay",
                MgColors.Hairline,
                filled: false,
                ["No active decayed branch"]);
        }
        else
        {
            foreach (var status in decayed)
            {
                AddSignalRow(
                    panel,
                    "Decayed",
                    MgColors.Blocked,
                    filled: true,
                    [
                        FormatBranchLevel(status),
                        "Restore",
                        "No advance",
                    ],
                    "Needs last owned standard pass plus lower-load transfer check.");
            }
        }

        foreach (var decay in state.ProgressRecords.DecayHistory.Take(4))
        {
            AddSignalRow(
                panel,
                "Decay",
                MgColors.Blocked,
                filled: true,
                [
                    FormatBranchLevel(decay.NextStatus),
                    FormatDate(decay.Date),
                    $"{decay.MaintenanceCheckIds.Count} checks",
                ],
                $"Transition: {StateMarkerView.LabelTextFor(decay.CurrentStatus.State)} to {StateMarkerView.LabelTextFor(decay.NextStatus.State)}.");
        }

        foreach (var restoration in state.ProgressRecords.RestorationHistory.Take(4))
        {
            AddSignalRow(
                panel,
                "Restored",
                MgColors.Maintenance,
                filled: false,
                [
                    FormatBranchLevel(restoration.NextStatus),
                    FormatDate(restoration.Date),
                    $"{restoration.RestorationCheckIds.Count} checks",
                ],
                FormatRestorationEvidence(restoration.Evidence));
        }

        AddSection(panel);
    }

    private void AddMaintenanceDependencyCaps(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Maintenance dependency caps",
        };

        var title = new TextView(context)
        {
            Text = "Caps",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        var caps = DependencyCapBlockers(state);
        if (caps.Count == 0)
        {
            AddSignalRow(
                panel,
                "Cap",
                MgColors.Hairline,
                filled: false,
                ["No dependency cap"]);
        }
        else
        {
            foreach (var cap in caps)
            {
                AddSignalRow(
                    panel,
                    "Cap",
                    MgColors.Blocked,
                    filled: true,
                    [
                        FormatBranchLevel(cap.Branch, cap.Level) ?? "Dependency",
                        cap.DependencyCapReason?.ToString() ?? "Cap",
                    ],
                    cap.Detail);
            }
        }

        foreach (var constraint in state.WeeklyPlan.Constraints
                     .Where(constraint => constraint.Kind == WeeklyProgrammingConstraintKind.MaintenanceNotCurrent))
        {
            AddSignalRow(
                panel,
                "Week",
                MgColors.Maintenance,
                filled: true,
                [
                    constraint.Branch?.ToString() ?? "Global",
                    "Maintenance",
                ],
                constraint.Detail);
        }

        AddSection(panel);
    }

    private void AddMaintenanceAllowedWork(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "App-selected maintenance and restoration work",
        };

        var title = new TextView(context)
        {
            Text = "Allowed Work",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        var dueBranches = state.DueMaintenance
            .Select(record => record.BranchLevel.Branch)
            .ToHashSet();
        var decayedBranches = ActiveDecayedStatuses(state)
            .Select(status => status.Branch)
            .ToHashSet();
        var workItems = state.AvailableNextWork
            .Where(work => IsMaintenanceOrRestorationWork(work, dueBranches, decayedBranches))
            .OrderBy(work => work.DayNumber)
            .Take(4)
            .ToArray();

        if (workItems.Length == 0)
        {
            AddSignalRow(
                panel,
                "None",
                MgColors.Hairline,
                filled: false,
                ["No app-selected route"],
                "No maintenance or restoration work is currently exposed by app integration.");
        }
        else
        {
            foreach (var work in workItems)
            {
                AddSignalRow(
                    panel,
                    FormatSession(work.Session),
                    ColorForSession(work.Session),
                    filled: !work.IsAdvancementWork,
                    ChipsForNextWork(work));
            }

            AddPrepareSessionButton(panel);
        }

        AddSection(panel);
    }

    private void AddMaintenanceEvidenceHistory(CurrentTrainingStateReadModel state)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Maintenance evidence history",
        };

        var title = new TextView(context)
        {
            Text = "History",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        var hasHistory = false;
        foreach (var check in state.ProgressRecords.MaintenanceChecks.Take(5))
        {
            hasHistory = true;
            AddSignalRow(
                panel,
                check.Evidence.Passed ? "Pass" : "Fail",
                check.Evidence.Passed ? MgColors.Maintenance : MgColors.Blocked,
                filled: !check.Evidence.Passed,
                [
                    FormatBranchLevel(check.Evidence.Branch, check.Evidence.OwnedLevel) ?? "Maint",
                    FormatDate(check.Evidence.Date),
                    FormatMaintenanceKind(check.Evidence.Kind),
                ],
                FormatStandardEvaluation(check.Evidence.StandardEvaluationResult));
        }

        foreach (var decay in state.ProgressRecords.DecayHistory.Take(3))
        {
            hasHistory = true;
            AddSignalRow(
                panel,
                "Decay",
                MgColors.Blocked,
                filled: true,
                [
                    FormatBranchLevel(decay.NextStatus),
                    FormatDate(decay.Date),
                    $"{decay.MaintenanceCheckIds.Count} checks",
                ]);
        }

        foreach (var restoration in state.ProgressRecords.RestorationHistory.Take(3))
        {
            hasHistory = true;
            AddSignalRow(
                panel,
                "Restore",
                MgColors.Maintenance,
                filled: false,
                [
                    FormatBranchLevel(restoration.NextStatus),
                    FormatDate(restoration.Date),
                    $"{restoration.Evidence.Checks.Count} checks",
                ],
                FormatRestorationEvidence(restoration.Evidence));
        }

        if (!hasHistory)
        {
            AddSignalRow(
                panel,
                "Empty",
                MgColors.Hairline,
                filled: false,
                ["No maintenance evidence"]);
        }

        AddSection(panel);
    }

    private void AddEvidenceReview(CurrentTrainingStateReadModel state)
    {
        var items = BuildEvidenceTimelineItems(state);

        AddEvidenceReviewOverview(state, items);
        AddEvidenceCategoryBoard(items);
        AddEvidenceTimeline(items);
    }

    private void AddEvidenceReviewOverview(
        CurrentTrainingStateReadModel state,
        IReadOnlyList<EvidenceTimelineItem> items)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Local evidence review summary",
        };

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var title = new TextView(context)
        {
            Text = "Evidence",
        };
        MgTypography.ApplyHeading(title);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        header.AddView(new StatusChipView(context, "Local", MgColors.Hairline));
        panel.AddView(header);

        AddSignalRow(
            panel,
            "Records",
            MgColors.Hairline,
            filled: false,
            [
                $"{state.EvidenceSummaries.Count} artifacts",
                $"{state.RecentSessions.Count} sessions",
                $"{state.ProgressRecords.FormalTestAttempts.Count} formal",
            ],
            "Local evidence records only.");

        var failures = items.Count(item => item.Filled && item.Color == MgColors.Blocked);
        AddSignalRow(
            panel,
            failures > 0 ? "Fail" : "Audit",
            failures > 0 ? MgColors.Blocked : MgColors.Hairline,
            filled: failures > 0,
            failures > 0
                ? [$"{failures} problem records", "Classified where available"]
                : ["No problem markers"]);

        AddSignalRow(
            panel,
            "Guard",
            MgColors.Hairline,
            filled: false,
            ["Observable", "Standard", "Constraint", "Score"],
            "Subjective notes are not displayed as advancement evidence.");

        AddSection(panel);
    }

    private void AddEvidenceCategoryBoard(IReadOnlyList<EvidenceTimelineItem> items)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Evidence review category filters",
        };

        var title = new TextView(context)
        {
            Text = "Categories",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        if (items.Count == 0)
        {
            AddSignalRow(
                panel,
                "None",
                MgColors.Hairline,
                filled: false,
                ["No local evidence"]);
        }
        else
        {
            AddInlineChipRow(
                panel,
                items
                    .GroupBy(item => item.Category)
                    .OrderBy(group => EvidenceCategoryOrder(group.Key))
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => $"{group.Key} {group.Count()}"),
                MgColors.Hairline);
        }

        AddSection(panel);
    }

    private void AddEvidenceTimeline(IReadOnlyList<EvidenceTimelineItem> items)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Evidence review chronological timeline",
        };

        var title = new TextView(context)
        {
            Text = "Timeline",
        };
        MgTypography.ApplyHeading(title);
        panel.AddView(title);

        if (items.Count == 0)
        {
            AddSignalRow(
                panel,
                "Empty",
                MgColors.Hairline,
                filled: false,
                ["No artifacts"]);
        }
        else
        {
            foreach (var item in items)
            {
                AddEvidenceTimelineEntry(panel, item);
            }
        }

        AddSection(panel);
    }

    private void AddEvidenceTimelineEntry(LinearLayout panel, EvidenceTimelineItem item)
    {
        AddSignalRow(
            panel,
            item.Marker,
            item.Color,
            item.Filled,
            item.Chips,
            item.Detail is null ? null : ShortText(item.Detail, 96));

        AddInlineChipRow(panel, EvidenceAuditChips(item), item.Color);

        foreach (var detail in EvidenceDetailRows(item))
        {
            AddDetailRow(panel, detail.Label, detail.Value);
        }

        AddDivider(panel);
    }

    private void AddDivider(LinearLayout parent)
    {
        var divider = new View(context)
        {
            Background = MgTheme.Filled(context, MgColors.Hairline, cornerRadius: 1),
        };
        var layout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            MgSpacing.Dp(context, 1));
        layout.SetMargins(0, MgSpacing.Dp(context, MgSpacing.Sm), 0, MgSpacing.Dp(context, MgSpacing.Sm));
        parent.AddView(divider, layout);
    }

    private void AddLegend(LinearLayout panel)
    {
        var scroll = new HorizontalScrollView(context);
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Md), 0, MgSpacing.Dp(context, MgSpacing.Sm));

        foreach (var state in new[]
                 {
                     BranchLevelState.Unopened,
                     BranchLevelState.Training,
                     BranchLevelState.TestReady,
                     BranchLevelState.PassedOnce,
                     BranchLevelState.Stabilizing,
                     BranchLevelState.Owned,
                     BranchLevelState.Maintenance,
                     BranchLevelState.Decayed,
                 })
        {
            AddLegendChip(row, new StateMarkerView(context, state));
        }

        AddLegendChip(row, new StatusChipView(context, "Blocked", MgColors.Blocked, filled: true));
        scroll.AddView(row);
        panel.AddView(scroll);
    }

    private void AddLegendChip(LinearLayout row, View chip)
    {
        var layout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(0, 0, MgSpacing.Dp(context, MgSpacing.Sm), 0);
        row.AddView(chip, layout);
    }

    private void AddLevelHeader(LinearLayout panel)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Sm), 0, MgSpacing.Dp(context, MgSpacing.Xs));

        var blank = new TextView(context)
        {
            Text = "",
        };
        row.AddView(blank, new LinearLayout.LayoutParams(MgSpacing.Dp(context, 48), ViewGroup.LayoutParams.WrapContent));

        foreach (var level in ProgramCatalog.GlobalLevels)
        {
            var label = new TextView(context)
            {
                Text = level.Id.ToString(),
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyMicro(label);
            row.AddView(label, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        }

        panel.AddView(row);
    }

    private void AddLadderRow(
        LinearLayout panel,
        CurrentTrainingStateReadModel state,
        BranchDefinition branch)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Xs), 0, MgSpacing.Dp(context, MgSpacing.Xs));

        var branchLabel = new TextView(context)
        {
            Text = branch.Code.ToString(),
            Gravity = GravityFlags.Center,
            ContentDescription = $"{branch.Code}, {branch.Name}",
        };
        MgTypography.ApplyLabel(branchLabel);
        branchLabel.Background = BranchLabelBackground(state, branch.Code);
        branchLabel.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Xs),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Xs),
            MgSpacing.Dp(context, MgSpacing.Sm));
        branchLabel.Clickable = true;
        branchLabel.Focusable = true;
        branchLabel.Click += (_, _) => OpenBranchDetail(branch.Code, level: null);
        row.AddView(branchLabel, new LinearLayout.LayoutParams(MgSpacing.Dp(context, 48), ViewGroup.LayoutParams.WrapContent));

        var levels = ProgramCatalog.GlobalLevels.Select(level => level.Id).ToArray();
        for (var index = 0; index < levels.Length; index++)
        {
            var level = levels[index];
            var status = GetStatus(state, branch.Code, level);
            var cell = new LevelCellView(
                context,
                status,
                blocked: HasBlocker(state, branch.Code, level),
                dueMaintenance: HasDueMaintenance(state, branch.Code, level),
                nextWork: HasNextWork(state, branch.Code),
                selected: false);
            cell.Clickable = true;
            cell.Focusable = true;
            cell.Click += (_, _) => OpenBranchDetail(branch.Code, level);
            row.AddView(cell, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));

            if (index < levels.Length - 1)
            {
                var nextStatus = GetStatus(state, branch.Code, levels[index + 1]);
                row.AddView(BuildGateEdge(state, status, nextStatus), new LinearLayout.LayoutParams(
                    MgSpacing.Dp(context, 12),
                    ViewGroup.LayoutParams.WrapContent));
            }
        }

        panel.AddView(row);
    }

    private Drawable BranchLabelBackground(CurrentTrainingStateReadModel state, BranchCode branch)
    {
        if (HasDecayedBranch(state, branch) || HasBlocker(state, branch))
        {
            return MgTheme.Outline(context, MgColors.Blocked, cornerRadius: 8);
        }

        if (state.DueMaintenance.Any(record => record.BranchLevel.Branch == branch))
        {
            return MgTheme.Outline(context, MgColors.Maintenance, cornerRadius: 8);
        }

        return MgTheme.Outline(context, MgColors.Hairline, cornerRadius: 8);
    }

    private View BuildGateEdge(
        CurrentTrainingStateReadModel state,
        BranchLevelStatus current,
        BranchLevelStatus next)
    {
        var hardBlocked = current.State == BranchLevelState.Decayed ||
            next.State == BranchLevelState.Decayed ||
            HasBlocker(state, current.Branch, current.Level) ||
            HasBlocker(state, next.Branch, next.Level);
        var provisionalLock = current.State is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing;
        var edgeColor = hardBlocked
            ? MgColors.Blocked
            : provisionalLock
                ? MgColors.PassedOnce
                : current.State is BranchLevelState.Owned or BranchLevelState.Maintenance
                ? MgColors.Owned
                : MgColors.Hairline;

        var edge = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        edge.SetGravity(GravityFlags.Center);
        edge.ContentDescription = hardBlocked
            ? $"{current.Branch} edge after {current.Level} blocked"
            : provisionalLock
            ? $"{current.Branch} edge after {current.Level} locked until stabilization"
            : $"{current.Branch} edge after {current.Level}";

        var bar = new View(context)
        {
            Background = MgTheme.Filled(context, edgeColor, cornerRadius: 4),
        };
        edge.AddView(bar, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            MgSpacing.Dp(context, hardBlocked ? 6 : provisionalLock ? 4 : 3)));
        return edge;
    }

    private void AddSessionStart(AndroidSessionStartSnapshot? startSnapshot)
    {
        if (startSnapshot is null)
        {
            AddPanel(
                "No prepared session",
                "Use Today to prepare app-selected work.",
                "No progress was changed.");
            return;
        }

        var preparation = startSnapshot.Preparation;
        AddSessionStartHeader(startSnapshot);
        AddSessionStartGuards(preparation);

        var selectedWork = preparation.Selection.SelectedWork;
        if (selectedWork is null)
        {
            return;
        }

        AddSessionWorkPanel(preparation, selectedWork);
        AddSessionStandardPanel(selectedWork);
        AddSessionGeneratedIdentity(preparation);
        AddSessionEvidence(preparation);
        AddSessionStartAction(preparation);
    }

    private void AddSessionStartHeader(AndroidSessionStartSnapshot startSnapshot)
    {
        var preparation = startSnapshot.Preparation;
        var selectedWork = preparation.Selection.SelectedWork;
        var panel = new MgPanel(context)
        {
            ContentDescription = $"Session start, {preparation.Status}",
        };

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var back = new StatusChipView(context, "Work", MgColors.Hairline);
        back.Clickable = true;
        back.Focusable = true;
        back.Click += (_, _) =>
        {
            currentScreen = AndroidShellScreen.Work;
            selectedBranch = null;
            selectedLevel = null;
            RenderCurrentScreen();
        };
        header.AddView(back);

        var title = new TextView(context)
        {
            Text = "Start",
        };
        MgTypography.ApplyHeading(title);
        var titleLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        titleLayout.SetMargins(MgSpacing.Dp(context, MgSpacing.Sm), 0, MgSpacing.Dp(context, MgSpacing.Sm), 0);
        header.AddView(title, titleLayout);
        header.AddView(new StatusChipView(
            context,
            preparation.CanStartRuntimeSession ? "Ready" : preparation.Status.ToString(),
            preparation.CanStartRuntimeSession ? MgColors.Training : MgColors.Blocked,
            filled: preparation.CanStartRuntimeSession));
        panel.AddView(header);

        AddSignalRow(
            panel,
            preparation.CanStartRuntimeSession ? "Ready" : "Blocked",
            preparation.CanStartRuntimeSession ? MgColors.Training : MgColors.Blocked,
            filled: preparation.CanStartRuntimeSession,
            selectedWork is null
                ? [preparation.Selection.Kind.ToString()]
                :
                [
                    $"{selectedWork.Branch} {selectedWork.Level}",
                    DrillCodeFor(selectedWork.Drill),
                    selectedWork.SessionType.ToString(),
                ]);

        AddInlineChipRow(
            panel,
            [
                startSnapshot.PreparedDate.ToString("MMM d"),
                preparation.GrantsAdvancementInApp ? "Grant" : "No grant",
                preparation.Selection.Kind.ToString(),
            ],
            MgColors.Hairline);
        AddSection(panel);
    }

    private void AddSessionStartGuards(PreUiTrainingWorkflowPreparationResult preparation)
    {
        if (preparation.Rejections.Count == 0 && preparation.Selection.Blockers.Count == 0)
        {
            return;
        }

        var panel = new MgPanel(context)
        {
            ContentDescription = "Session start blockers and rejections",
        };
        var title = new TextView(context)
        {
            Text = "Guards",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        foreach (var blocker in preparation.Selection.Blockers.Take(4))
        {
            AddSignalRow(
                panel,
                "Blocked",
                MgColors.Blocked,
                filled: true,
                LimitChips(
                    [
                        blocker.Source.ToString(),
                        FormatBranchLevel(blocker.Branch, blocker.Level) ?? "Global",
                    ],
                    totalCount: 2),
                blocker.Detail);
        }

        foreach (var rejection in preparation.Rejections.Take(4))
        {
            AddSignalRow(
                panel,
                "Reject",
                MgColors.Blocked,
                filled: true,
                [preparation.Status.ToString()],
                rejection.Detail);
        }

        AddSection(panel);
    }

    private void AddSessionWorkPanel(
        PreUiTrainingWorkflowPreparationResult preparation,
        SelectedTrainingWork selectedWork)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Prepared work and immutable load",
        };
        var title = new TextView(context)
        {
            Text = "Work",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        AddSignalRow(
            panel,
            selectedWork.Branch.ToString(),
            preparation.CanStartRuntimeSession ? MgColors.Training : MgColors.Blocked,
            filled: preparation.CanStartRuntimeSession,
            [
                selectedWork.Level.ToString(),
                DrillLabelFor(selectedWork.Drill),
                selectedWork.SessionType.ToString(),
                selectedWork.AdvancementWorkAllowed ? "Gate" : "No gate",
            ]);

        AddInlineChipRow(
            panel,
            selectedWork.LoadVariables.Select(FormatLoadVariable),
            MgColors.Hairline);
        AddSection(panel);
    }

    private void AddSessionStandardPanel(SelectedTrainingWork selectedWork)
    {
        var panel = new StandardPanelView(
            context,
            "Standard",
            selectedWork.Standard,
            selectedWork.HonestyConstraint);
        AddDetailRow(panel, "Demand", selectedWork.Demand);
        AddSection(panel);
    }

    private void AddSessionGeneratedIdentity(PreUiTrainingWorkflowPreparationResult preparation)
    {
        var generatedResult = preparation.GeneratedContent?.GeneratedContent?.Result;
        var generatedRecord = preparation.GeneratedInstanceRecord;
        var runtimeSession = preparation.RuntimeSession;

        if (generatedResult is null && generatedRecord is null)
        {
            return;
        }

        var panel = new MgPanel(context)
        {
            ContentDescription = "Generated content identity",
        };
        var title = new TextView(context)
        {
            Text = "Identity",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        AddInlineChipRow(
            panel,
            [
                generatedResult?.ContentKind.ToString() ?? generatedRecord?.ContentIdentity.Kind.ToString() ?? "Content",
                generatedResult?.Instance.RetestFreshnessPolicy.ToString() ?? generatedRecord?.FreshnessPolicy?.ToString() ?? "Freshness",
                generatedRecord?.State.ToString() ?? "Prepared",
                $"{runtimeSession?.InputMaterials.Count ?? 0} materials",
                $"{runtimeSession?.PhasePlan?.Phases.Count ?? 0} phases",
            ],
            MgColors.Hairline);

        AddDetailRow(
            panel,
            "Instance",
            generatedResult?.InstanceId ?? generatedRecord?.InstanceId ?? "Unavailable");
        AddDetailRow(
            panel,
            "Content",
            generatedResult?.ContentId ?? generatedRecord?.ContentIdentity.ContentId ?? "Unavailable");
        AddDetailRow(
            panel,
            "Equiv",
            generatedResult?.EquivalenceClass ?? generatedRecord?.ContentIdentity.EquivalenceClass ?? "Unavailable");

        if (generatedResult is not null)
        {
            AddDetailRow(panel, "Load hash", generatedResult.LoadContextFingerprint);
        }

        AddSection(panel);
    }

    private void AddSessionEvidence(PreUiTrainingWorkflowPreparationResult preparation)
    {
        var facts = preparation.RuntimeSession?.ExpectedEvidenceFacts.ToArray() ?? [];
        if (facts.Length == 0)
        {
            return;
        }

        var panel = new MgPanel(context)
        {
            ContentDescription = "Required evidence facts",
        };
        var title = new TextView(context)
        {
            Text = "Evidence",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        AddInlineChipRow(
            panel,
            [
                $"{facts.Length} facts",
                preparation.RuntimeSession?.OwnsScoring == false ? "Runtime only" : "Scoring",
                preparation.RuntimeSession?.GrantsAdvancement == false ? "No grant" : "Grant",
            ],
            MgColors.Hairline);

        foreach (var fact in facts.Take(5))
        {
            AddSignalRow(
                panel,
                "Req",
                MgColors.TestReady,
                filled: false,
                [fact.Name],
                ShortText(fact.Value, maxLength: 72));
        }

        if (facts.Length > 5)
        {
            AddSignalRow(
                panel,
                "More",
                MgColors.Hairline,
                filled: false,
                [$"+{facts.Length - 5}"]);
        }

        AddSection(panel);
    }

    private void AddSessionStartAction(PreUiTrainingWorkflowPreparationResult preparation)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Session start controls",
        };
        AddSignalRow(
            panel,
            "Locked",
            preparation.CanStartRuntimeSession ? MgColors.Training : MgColors.Blocked,
            filled: preparation.CanStartRuntimeSession,
            ["Load", "Std", "Prereq", "Const"],
            "No UI override.");

        var start = new SessionActionButton(context, "Start", preparation.CanStartRuntimeSession)
        {
            ContentDescription = preparation.CanStartRuntimeSession
                ? "Start live runtime session."
                : "Start unavailable because app preparation did not produce a startable runtime session.",
        };
        if (preparation.CanStartRuntimeSession)
        {
            start.Click += (_, _) => LiveSessionStartRequested?.Invoke();
        }

        var layout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(0, MgSpacing.Dp(context, MgSpacing.Md), 0, 0);
        panel.AddView(start, layout);

        AddSection(panel);
    }

    private void AddLiveSession(AndroidLiveSessionSnapshot? liveSnapshot)
    {
        if (liveSnapshot is null)
        {
            AddPanel(
                "Starting session",
                "Waiting for runtime state.",
                "No progress is changed.");
            return;
        }

        var live = liveSnapshot.LiveSession;
        AddLiveHeader(liveSnapshot);
        AddLivePhasePanel(live);
        AddLiveCuePanel(live);
        AddLiveCommandPanel(live);
        AddLiveEvidencePanel(live);
    }

    private void AddLiveHeader(AndroidLiveSessionSnapshot liveSnapshot)
    {
        var live = liveSnapshot.LiveSession;
        var panel = new MgPanel(context)
        {
            ContentDescription = $"Live session, {live.LifecycleStatus}, {live.SchedulerStatus}",
        };

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var title = new TextView(context)
        {
            Text = "Live",
        };
        MgTypography.ApplyHeading(title);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        header.AddView(new StatusChipView(
            context,
            LiveLifecycleLabel(live.LifecycleStatus),
            ColorForLifecycle(live.LifecycleStatus),
            filled: live.LifecycleStatus is RuntimeSessionLifecycleStatus.Running));
        panel.AddView(header);

        AddSignalRow(
            panel,
            live.Branch.ToString(),
            ColorForLifecycle(live.LifecycleStatus),
            filled: live.LifecycleStatus is RuntimeSessionLifecycleStatus.Running,
            [
                live.Level.ToString(),
                DrillCodeFor(live.Drill),
                live.SessionType.ToString(),
                live.GrantsAdvancementInApp ? "Grant" : "No grant",
            ]);

        AddSection(panel);
    }

    private void AddLivePhasePanel(PreUiLiveSessionState live)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = $"Runtime phase {live.CurrentPhaseKind?.ToString() ?? "none"}",
        };

        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);

        var timerRing = new TimerRingView(context);
        timerRing.Update(live.Timer, live.LifecycleStatus);
        var timerLayout = new LinearLayout.LayoutParams(
            MgSpacing.Dp(context, 144),
            MgSpacing.Dp(context, 144));
        timerLayout.SetMargins(0, 0, MgSpacing.Dp(context, MgSpacing.Lg), 0);
        row.AddView(timerRing, timerLayout);

        var phaseStack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };

        var phase = new TextView(context)
        {
            Text = FormatPhase(live.CurrentPhaseKind),
        };
        MgTypography.ApplyTitle(phase);
        phaseStack.AddView(phase);
        AddInlineChipRow(
            phaseStack,
            [
                live.CurrentPhaseCompletionRule?.ToString() ?? "No phase",
                live.SchedulerStatus.ToString(),
                live.Timer.IsTimed ? "Timed" : "Manual",
            ],
            MgColors.Hairline);

        row.AddView(phaseStack, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        panel.AddView(row);

        if (live.LastCommand is not null)
        {
            AddSignalRow(
                panel,
                live.LastCommand.IsAccepted ? "OK" : "Stop",
                live.LastCommand.IsAccepted ? MgColors.Owned : MgColors.Blocked,
                filled: live.LastCommand.IsAccepted,
                [
                    LabelForCommand(live.LastCommand.Command),
                    live.LastCommand.EventCount > 0 ? $"{live.LastCommand.EventCount} event" : "No event",
                ],
                live.LastCommand.IsAccepted
                    ? null
                    : live.LastCommand.InvalidReason?.ToString() ??
                        live.LastCommand.CueInvalidReason?.ToString() ??
                        live.LastCommand.Detail);
        }

        AddSection(panel);
    }

    private void AddLiveCuePanel(PreUiLiveSessionState live)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Current runtime cue and prompt material",
        };

        var title = new TextView(context)
        {
            Text = live.ActiveCue is null ? "Prompt" : "Cue",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        if (live.ActiveCue is not null)
        {
            var cue = new TextView(context)
            {
                Text = live.ActiveCue.Cue,
                Gravity = GravityFlags.Center,
            };
            cue.SetMinHeight(MgSpacing.Dp(context, 108));
            cue.SetPadding(
                MgSpacing.Dp(context, MgSpacing.Lg),
                MgSpacing.Dp(context, MgSpacing.Xl),
                MgSpacing.Dp(context, MgSpacing.Lg),
                MgSpacing.Dp(context, MgSpacing.Xl));
            cue.Background = MgTheme.Outline(context, MgColors.TestReady, cornerRadius: 8);
            MgTypography.ApplyTitle(cue);
            panel.AddView(cue);

            AddInlineChipRow(
                panel,
                [
                    live.ActiveCue.Kind.ToString(),
                    live.ActiveCue.ResponseExpectation == RuntimeCueResponseExpectation.ResponseRequired ? "Respond" : "Hold",
                    FormatDuration(live.ActiveCue.ResponseWindow),
                ],
                MgColors.Hairline);
        }
        else
        {
            foreach (var material in live.CurrentMaterials.Take(4))
            {
                AddSignalRow(
                    panel,
                    ShortMaterialKind(material.Kind),
                    MgColors.Hairline,
                    filled: false,
                    [material.Name],
                    ShortText(material.Value, maxLength: 96));
            }
        }

        var input = new EditText(context)
        {
            Text = liveSessionInput,
            Hint = live.ActiveCue is null ? "Answer" : "Response",
        };
        input.SetSingleLine(false);
        input.SetMinLines(2);
        input.SetMaxLines(4);
        input.SetTextColor(MgColors.Ink);
        input.SetHintTextColor(MgColors.InkMuted);
        input.Background = MgTheme.MutedSurface(context);
        input.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Sm));
        input.TextChanged += (_, args) => liveSessionInput = args.Text?.ToString() ?? string.Empty;

        var inputLayout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        inputLayout.SetMargins(0, MgSpacing.Dp(context, MgSpacing.Md), 0, 0);
        panel.AddView(input, inputLayout);

        AddSection(panel);
    }

    private void AddLiveCommandPanel(PreUiLiveSessionState live)
    {
        var panel = new MgPanel(context)
        {
            ContentDescription = "Runtime session actions",
        };

        var title = new TextView(context)
        {
            Text = "Actions",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        var primaryRow = AddButtonRow(panel);
        AddLiveCommandButton(
            primaryRow,
            live,
            RuntimeInputCommandKind.RespondToCue,
            targetFactory: () => live.ActiveCue?.CueId,
            valueFactory: () => LiveInputOrDefault(live.ActiveCue?.ExpectedResponse ?? "response"),
            compact: true,
            forceDisabled: live.ActiveCue is null ||
                live.ActiveCue.ResponseExpectation == RuntimeCueResponseExpectation.NoResponseExpected);
        AddLiveCommandButton(
            primaryRow,
            live,
            RuntimeInputCommandKind.SubmitAnswer,
            targetFactory: null,
            valueFactory: () => LiveInputOrDefault("submitted"),
            compact: true);
        AddLiveCommandButton(
            primaryRow,
            live,
            RuntimeInputCommandKind.FinishPhase,
            targetFactory: null,
            valueFactory: null,
            compact: true);

        var evidenceRow = AddButtonRow(panel);
        AddLiveCommandButton(evidenceRow, live, RuntimeInputCommandKind.MarkDrift, null, null, compact: true);
        AddLiveCommandButton(evidenceRow, live, RuntimeInputCommandKind.MarkGuess, null, null, compact: true);
        AddLiveCommandButton(
            evidenceRow,
            live,
            RuntimeInputCommandKind.MarkError,
            targetFactory: null,
            valueFactory: () => "incorrect_response",
            compact: true,
            color: MgColors.Blocked);

        var correctionRow = AddButtonRow(panel);
        AddLiveCommandButton(correctionRow, live, RuntimeInputCommandKind.Correct, null, () => LiveInputOrDefault("corrected"), compact: true);
        AddLiveCommandButton(correctionRow, live, RuntimeInputCommandKind.StartAudit, null, null, compact: true);

        var lifecycleRow = AddButtonRow(panel);
        AddLiveCommandButton(lifecycleRow, live, RuntimeInputCommandKind.Pause, null, null, compact: true, color: MgColors.Recovery);
        AddLiveCommandButton(lifecycleRow, live, RuntimeInputCommandKind.Resume, null, null, compact: true, color: MgColors.Recovery);

        AddLiveCommandButton(
            panel,
            live,
            RuntimeInputCommandKind.Abandon,
            targetFactory: null,
            valueFactory: () => "user abandoned live session",
            color: MgColors.Blocked);

        AddSection(panel);
    }

    private void AddLiveEvidencePanel(PreUiLiveSessionState live)
    {
        var evidence = live.Evidence;
        var panel = new MgPanel(context)
        {
            ContentDescription = "Runtime evidence state",
        };

        var title = new TextView(context)
        {
            Text = "Evidence",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        AddSignalRow(
            panel,
            "Events",
            MgColors.TestReady,
            filled: evidence.RuntimeEventCount > 0,
            [
                $"{evidence.RuntimeEventCount}",
                $"{evidence.EvidenceFactCount}/{evidence.ExpectedEvidenceFactCount} facts",
                live.GrantsAdvancementInApp ? "Grant" : "No grant",
            ]);

        AddInlineChipRow(
            panel,
            [
                $"drift {evidence.DriftCount}",
                $"guess {evidence.GuessCount}",
                $"error {evidence.ErrorCount}",
                $"cue {evidence.CueCount}",
                $"resp {evidence.CueResponseCount}",
                $"ans {evidence.AnswerCount}",
                $"fix {evidence.CorrectionCount}",
            ],
            evidence.ErrorCount > 0 ? MgColors.Blocked : MgColors.Hairline);

        AddSection(panel);
    }

    private void AddLiveResult(AndroidLiveSessionCompletionSnapshot? resultSnapshot)
    {
        if (resultSnapshot is null)
        {
            AddPanel(
                "Processing result",
                "Waiting for app workflow completion.",
                "Android UI grants no progress.");
            return;
        }

        var completion = resultSnapshot.Completion;
        var live = completion.SessionState;
        var processing = completion.WorkflowResult?.ProcessingResult;
        var visual = ResultVisualFor(completion);

        var panel = new MgPanel(context)
        {
            ContentDescription = $"Live session result, {visual.Label}",
        };

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var title = new TextView(context)
        {
            Text = "Result",
        };
        MgTypography.ApplyHeading(title);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        header.AddView(new StatusChipView(
            context,
            visual.Label,
            visual.Color,
            filled: visual.Filled));
        panel.AddView(header);

        AddSignalRow(
            panel,
            visual.Marker,
            visual.Color,
            filled: visual.Filled,
            [
                live.Branch.ToString(),
                live.Level.ToString(),
                DrillCodeFor(live.Drill),
                live.SessionType.ToString(),
                completion.GrantsAdvancementInApp ? "Grant" : "No grant",
            ],
            visual.Detail);

        AddResultEvidenceRow(panel, completion);

        if (processing is not null)
        {
            AddResultDecisionRows(panel, processing);
            AddFailureResponseRows(panel, processing);
        }

        if (completion.WorkflowResult is not null)
        {
            AddResultNextActionRow(panel, completion.WorkflowResult.RefreshedState);
        }

        var work = new SessionActionButton(context, "Work", enabled: snapshot is not null)
        {
            ContentDescription = "Return to current work state.",
        };
        if (snapshot is not null)
        {
            work.Click += (_, _) =>
            {
                currentScreen = AndroidShellScreen.Work;
                RenderCurrentScreen();
            };
        }

        var workLayout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        workLayout.SetMargins(0, MgSpacing.Dp(context, MgSpacing.Md), 0, 0);
        panel.AddView(work, workLayout);

        AddSection(panel);

        if (completion.WorkflowResult is not null && snapshot is not null)
        {
            var nextPanel = new MgPanel(context)
            {
                ContentDescription = "Refreshed next work after result",
            };
            AddNextWorkStack(nextPanel, completion.WorkflowResult.RefreshedState);
            AddSection(nextPanel);
        }
    }

    private void AddResultEvidenceRow(LinearLayout panel, PreUiLiveSessionCompletionResult completion)
    {
        var evidence = completion.SessionState.Evidence;
        var processing = completion.WorkflowResult?.ProcessingResult;
        var notClean = processing?.SessionHistory.CleanPerformance == false ||
            RuntimeTerminalProblem(processing?.CompletionStatus ?? completion.RuntimeCompletionStatus) ||
            evidence.ErrorCount > 0;
        var cleanChip = processing is null
            ? "Pending"
            : processing.SessionHistory.CleanPerformance ? "Clean" : "Not clean";
        var generatedDetail = processing?.GeneratedDrillInstance is null
            ? null
            : $"Generated content: {processing.GeneratedDrillInstance.State}.";

        AddSignalRow(
            panel,
            "Evidence",
            notClean ? MgColors.Blocked : MgColors.Hairline,
            filled: notClean,
            [
                $"{evidence.RuntimeEventCount} events",
                $"{processing?.EvidenceArtifacts.Count ?? evidence.EvidenceFactCount} artifacts",
                cleanChip,
            ],
            generatedDetail);

        AddInlineChipRow(
            panel,
            [
                $"drift {evidence.DriftCount}",
                $"guess {evidence.GuessCount}",
                $"error {evidence.ErrorCount}",
                $"cue {evidence.CueCount}",
                $"resp {evidence.CueResponseCount}",
                $"ans {evidence.AnswerCount}",
                $"fix {evidence.CorrectionCount}",
            ],
            notClean ? MgColors.Blocked : MgColors.Hairline);
    }

    private void AddResultDecisionRows(
        LinearLayout panel,
        CompletedRuntimeSessionProcessingResult processing)
    {
        AddSignalRow(
            panel,
            "Runtime",
            ColorForRuntimeCompletion(processing.CompletionStatus),
            filled: RuntimeTerminalProblem(processing.CompletionStatus),
            [
                CompletionLabel(processing.CompletionStatus),
                processing.SessionHistory.SessionType.ToString(),
                processing.GrantsAdvancementInApp ? "Grant" : "No grant",
            ],
            processing.SessionHistory.Notes);

        if (processing.StandardEvaluationResult is { } standard)
        {
            AddSignalRow(
                panel,
                "Standard",
                standard.Passed ? MgColors.Hairline : MgColors.Blocked,
                filled: !standard.Passed,
                [
                    standard.Passed ? "Pass" : "Fail",
                    standard.Failures.Count == 0 ? "0 issues" : $"{standard.Failures.Count} issues",
                ],
                standard.Failures.FirstOrDefault()?.Detail);
        }

        if (processing.FormalGateDecision is { } gate)
        {
            AddSignalRow(
                panel,
                "Gate",
                ColorForGateOutcome(gate.Outcome),
                filled: GateOutcomeHasHardStop(gate.Outcome),
                [
                    FormatGateOutcome(gate.Outcome),
                    gate.BlockingFailures.Count == 0 ? "0 blocks" : $"{gate.BlockingFailures.Count} blocks",
                ],
                gate.BlockingFailures.FirstOrDefault()?.Detail);
        }

        if (processing.StabilizationOwnershipResult is { } ownership)
        {
            AddSignalRow(
                panel,
                ownership.IsOwned ? "Owned" : "Stab",
                ownership.IsOwned ? MgColors.Owned : ColorForState(ownership.BranchLevelState),
                filled: ownership.IsOwned,
                [
                    FormatGateOutcome(ownership.GateOutcome),
                    StateMarkerView.LabelTextFor(ownership.BranchLevelState),
                    ownership.Failures.Count == 0 ? "0 gaps" : $"{ownership.Failures.Count} gaps",
                ],
                ownership.Failures.FirstOrDefault()?.Detail);
        }

        if (processing.MaintenanceCurrencyResult is { } maintenance)
        {
            AddSignalRow(
                panel,
                MarkerForMaintenanceState(maintenance.State),
                ColorForMaintenanceState(maintenance.State),
                filled: maintenance.State is MaintenanceCurrencyState.Warning or MaintenanceCurrencyState.Failed,
                [
                    maintenance.State.ToString(),
                    $"fail {maintenance.ConsecutiveFailures}",
                    DaysSinceMaintenancePass(maintenance),
                ]);
        }

        if (processing.DecayResult is { } decay)
        {
            AddSignalRow(
                panel,
                decay.NextStatus.State == BranchLevelState.Decayed ? "Decay" : "Restore",
                decay.NextStatus.State == BranchLevelState.Decayed || decay.Failures.Count > 0
                    ? MgColors.Blocked
                    : ColorForState(decay.NextStatus.State),
                filled: decay.ChangedState || decay.Failures.Count > 0,
                [
                    $"{StateMarkerView.LabelTextFor(decay.CurrentStatus.State)}->{StateMarkerView.LabelTextFor(decay.NextStatus.State)}",
                    decay.Transition?.ToString() ?? "No change",
                    FormatBranchLevel(decay.NextStatus),
                ],
                decay.Failures.FirstOrDefault()?.Detail);
        }

        if (processing.TransferEligibilityResult is { } transfer)
        {
            AddSignalRow(
                panel,
                "Transfer",
                transfer.IsEligible ? MgColors.Transfer : MgColors.Blocked,
                filled: !transfer.IsEligible,
                [
                    transfer.IsEligible ? "Eligible" : "Blocked",
                    transfer.Failures.Count == 0 ? "0 gaps" : $"{transfer.Failures.Count} gaps",
                ],
                transfer.Failures.FirstOrDefault()?.Detail);
        }

        if (processing.StateTransition is { } transition)
        {
            AddSignalRow(
                panel,
                "State",
                transition.NextStatus.State == BranchLevelState.Decayed
                    ? MgColors.Blocked
                    : ColorForState(transition.NextStatus.State),
                filled: TransitionHasHighSignal(transition),
                [
                    $"{StateMarkerView.LabelTextFor(transition.CurrentStatus.State)}->{StateMarkerView.LabelTextFor(transition.NextStatus.State)}",
                    FormatBranchLevel(transition.NextStatus),
                    transition.Transition.ToString(),
                ]);
        }
    }

    private void AddFailureResponseRows(
        LinearLayout panel,
        CompletedRuntimeSessionProcessingResult processing)
    {
        if (processing.FailureResponse is not { } response)
        {
            return;
        }

        var actionChips = response.Actions
            .Select(action => ShortText(PascalWords(action.ToString()), 18))
            .ToArray();
        var chips = new List<string>
        {
            PascalWords(response.Failure.Type.ToString()),
            FormatBranchLevel(response.Failure.Branch, response.Failure.Level) ?? "Global",
        };
        chips.AddRange(actionChips);

        AddSignalRow(
            panel,
            "Action",
            MgColors.Blocked,
            filled: true,
            LimitChips(chips, chips.Count),
            response.Actions.Count == 0
                ? null
                : $"Next: {PascalWords(response.Actions[0].ToString())}.");
    }

    private void AddResultNextActionRow(LinearLayout panel, CurrentTrainingStateReadModel state)
    {
        var decayed = state.BranchLevelStates
            .FirstOrDefault(status => status.State == BranchLevelState.Decayed);
        if (decayed.State == BranchLevelState.Decayed)
        {
            AddSignalRow(
                panel,
                "Decayed",
                MgColors.Blocked,
                filled: true,
                [
                    FormatBranchLevel(decayed),
                    "Restore",
                    "No advance",
                ],
                "Dependent advancement remains capped by app/core state.");
            return;
        }

        if (state.BlockedAdvancement.Count > 0)
        {
            AddSignalRow(
                panel,
                "Blocked",
                MgColors.Blocked,
                filled: true,
                ChipsForBlocked(state.BlockedAdvancement),
                state.BlockedAdvancement[0].Detail);
            return;
        }

        if (state.DueMaintenance.Count > 0)
        {
            AddSignalRow(
                panel,
                "Due",
                MgColors.Maintenance,
                filled: true,
                ChipsForMaintenance(state),
                "Maintenance remains the next visible programming pressure.");
            return;
        }

        var next = state.AvailableNextWork
            .OrderBy(work => work.DayNumber)
            .FirstOrDefault();
        if (next is not null)
        {
            AddSignalRow(
                panel,
                "Next",
                ColorForSession(next.Session),
                filled: next.IsAdvancementWork,
                ChipsForNextWork(next));
            return;
        }

        AddSignalRow(
            panel,
            "No work",
            MgColors.Hairline,
            filled: false,
            ["App state"]);
    }

    private LinearLayout AddButtonRow(LinearLayout parent)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Sm), 0, 0);
        parent.AddView(row);
        return row;
    }

    private void AddLiveCommandButton(
        LinearLayout parent,
        PreUiLiveSessionState live,
        RuntimeInputCommandKind command,
        Func<string?>? targetFactory,
        Func<string?>? valueFactory,
        bool compact = false,
        Color? color = null,
        bool forceDisabled = false)
    {
        var state = CommandState(live, command);
        var enabled = !forceDisabled && state?.IsAvailable == true;
        var label = state?.Label ?? LabelForCommand(command);
        var button = new SessionActionButton(context, label, enabled)
        {
            ContentDescription = enabled
                ? label
                : $"{label} unavailable: {state?.InvalidReason?.ToString() ?? state?.CueInvalidReason?.ToString() ?? "runtime state"}",
        };

        if (color.HasValue)
        {
            button.Background = enabled
                ? MgTheme.Filled(context, color.Value)
                : MgTheme.Outline(context, color.Value);
            button.SetTextColor(enabled ? Color.White : MgColors.InkMuted);
        }

        if (enabled)
        {
            button.Click += (_, _) =>
                LiveSessionCommandRequested?.Invoke(command, targetFactory?.Invoke(), valueFactory?.Invoke());
        }

        var layout = new LinearLayout.LayoutParams(
            compact ? 0 : ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent,
            compact ? 1 : 0);
        layout.SetMargins(
            0,
            0,
            compact ? MgSpacing.Dp(context, MgSpacing.Sm) : 0,
            MgSpacing.Dp(context, MgSpacing.Sm));
        parent.AddView(button, layout);
    }

    private void AddBranchDetail(
        CurrentTrainingStateReadModel state,
        BranchCode branch,
        GlobalLevelId? requestedLevel)
    {
        var status = requestedLevel is null
            ? SelectDefaultStatus(state, branch)
            : GetStatus(state, branch, requestedLevel.Value);
        selectedLevel = status.Level;

        AddBranchDetailHeader(state, status);
        AddBranchDetailBlockers(state, status);
        AddBranchDetailNextWork(state, status.Branch);
        AddBranchDetailStandards(status);
        AddBranchDetailEvidence(state, status);
    }

    private void AddBranchDetailHeader(CurrentTrainingStateReadModel state, BranchLevelStatus status)
    {
        var branchDefinition = ProgramCatalog.Branches.First(branch => branch.Code == status.Branch);
        var panel = new MgPanel(context)
        {
            ContentDescription = $"{status.Branch} detail, {status.Level}, {status.State}",
        };

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var back = new StatusChipView(context, "Map", MgColors.Hairline);
        back.Clickable = true;
        back.Focusable = true;
        back.Click += (_, _) =>
        {
            currentScreen = AndroidShellScreen.Map;
            selectedBranch = null;
            selectedLevel = null;
            RenderCurrentScreen();
        };
        header.AddView(back);

        var title = new TextView(context)
        {
            Text = $"{status.Branch} {status.Level}",
        };
        MgTypography.ApplyHeading(title);
        var titleLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        titleLayout.SetMargins(MgSpacing.Dp(context, MgSpacing.Sm), 0, MgSpacing.Dp(context, MgSpacing.Sm), 0);
        header.AddView(title, titleLayout);
        header.AddView(new StateMarkerView(context, status.State));
        panel.AddView(header);

        var name = new TextView(context)
        {
            Text = branchDefinition.Name,
        };
        MgTypography.ApplyLabel(name);
        panel.AddView(name);

        AddBranchLane(panel, state, status.Branch, status.Level);
        AddStateFacts(panel, state, status);
        AddSection(panel);
    }

    private void AddBranchLane(
        LinearLayout panel,
        CurrentTrainingStateReadModel state,
        BranchCode branch,
        GlobalLevelId selected)
    {
        var lane = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        lane.SetGravity(GravityFlags.CenterVertical);
        lane.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Md), 0, 0);

        foreach (var level in ProgramCatalog.GlobalLevels.Select(level => level.Id))
        {
            var status = GetStatus(state, branch, level);
            var cell = new LevelCellView(
                context,
                status,
                blocked: HasBlocker(state, branch, level),
                dueMaintenance: HasDueMaintenance(state, branch, level),
                nextWork: HasNextWork(state, branch),
                selected: level == selected);
            cell.Clickable = true;
            cell.Focusable = true;
            cell.Click += (_, _) => OpenBranchDetail(branch, level);
            lane.AddView(cell, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        }

        panel.AddView(lane);
    }

    private void AddStateFacts(LinearLayout panel, CurrentTrainingStateReadModel state, BranchLevelStatus status)
    {
        var chips = new List<string>
        {
            StateMarkerView.LabelTextFor(status.State),
        };
        if (HasDueMaintenance(state, status.Branch, status.Level))
        {
            chips.Add("Due");
        }

        if (HasBlocker(state, status.Branch, status.Level))
        {
            chips.Add("Blocked");
        }

        if (HasNextWork(state, status.Branch))
        {
            chips.Add("Next");
        }

        AddInlineChipRow(panel, chips, status.State == BranchLevelState.Decayed ? MgColors.Blocked : ColorForState(status.State));
    }

    private void AddBranchDetailBlockers(CurrentTrainingStateReadModel state, BranchLevelStatus status)
    {
        var blockers = state.BlockedAdvancement
            .Where(blocker => blocker.Branch is null || blocker.Branch == status.Branch)
            .ToArray();
        var due = state.DueMaintenance
            .Where(record => record.BranchLevel.Branch == status.Branch)
            .ToArray();
        var decayed = state.BranchLevelStates
            .Where(level => level.Branch == status.Branch && level.State == BranchLevelState.Decayed)
            .ToArray();

        if (blockers.Length == 0 && due.Length == 0 && decayed.Length == 0)
        {
            return;
        }

        var panel = new MgPanel(context);
        var title = new TextView(context)
        {
            Text = "Priority",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        foreach (var level in decayed)
        {
            AddSignalRow(
                panel,
                "Decayed",
                MgColors.Blocked,
                filled: true,
                [FormatBranchLevel(level)],
                "Restoration must come from app/core evidence, not this screen.");
        }

        foreach (var blocker in blockers.Take(3))
        {
            AddSignalRow(
                panel,
                "Blocked",
                MgColors.Blocked,
                filled: true,
                ChipsForBlocked([blocker]),
                blocker.Detail);
        }

        foreach (var maintenance in due.Take(3))
        {
            AddSignalRow(
                panel,
                "Due",
                MgColors.Maintenance,
                filled: true,
                [FormatBranchLevel(maintenance.BranchLevel), maintenance.Currency.State.ToString()]);
        }

        AddSection(panel);
    }

    private void AddBranchDetailNextWork(CurrentTrainingStateReadModel state, BranchCode branch)
    {
        var panel = new MgPanel(context);
        var title = new TextView(context)
        {
            Text = "Next Work",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        var workItems = state.AvailableNextWork
            .Where(work => work.BranchEmphasis.Contains(branch))
            .OrderBy(work => work.DayNumber)
            .Take(3)
            .ToArray();
        if (workItems.Length == 0)
        {
            AddSignalRow(
                panel,
                "None",
                MgColors.Hairline,
                filled: false,
                ["Not app-selected"]);
        }
        else
        {
            foreach (var work in workItems)
            {
                AddSignalRow(
                    panel,
                    FormatSession(work.Session),
                    ColorForSession(work.Session),
                    filled: work.IsAdvancementWork,
                    ChipsForNextWork(work));
            }

            AddPrepareSessionButton(panel);
        }

        AddSection(panel);
    }

    private void AddPrepareSessionButton(LinearLayout panel)
    {
        var action = new SessionActionButton(context, "Prepare", enabled: true)
        {
            ContentDescription = "Prepare session start from app-selected work",
        };
        action.Click += (_, _) => SessionStartRequested?.Invoke();

        var layout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(0, MgSpacing.Dp(context, MgSpacing.Md), 0, 0);
        panel.AddView(action, layout);
    }

    private void AddBranchDetailStandards(BranchLevelStatus status)
    {
        var standard = ProgramCatalog.Standards.FirstOrDefault(
            item => item.Branch == status.Branch && item.Level == status.Level);
        var unlock = ProgramCatalog.BranchUnlocks.FirstOrDefault(item => item.Branch == status.Branch);
        if (standard is null && unlock is null)
        {
            return;
        }

        var panel = new MgPanel(context)
        {
            ContentDescription = $"{status.Branch} {status.Level} standard and prerequisites",
        };
        var title = new TextView(context)
        {
            Text = "Standard",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);

        if (standard is not null)
        {
            AddDetailRow(panel, "Demand", standard.Demand);
            AddDetailRow(panel, "Pass", standard.Standard);
            AddDetailRow(panel, "Gate", standard.Gate);
            AddDetailRow(panel, "Stab", standard.Stabilization);
            AddDetailRow(panel, "Transfer", standard.Transfer);
        }

        if (unlock is not null)
        {
            var requirements = UnlockRequirements(unlock).ToArray();
            if (requirements.Length > 0)
            {
                AddInlineChipRow(panel, requirements, MgColors.Hairline);
            }
        }

        AddSection(panel);
    }

    private void AddBranchDetailEvidence(CurrentTrainingStateReadModel state, BranchLevelStatus status)
    {
        var sessions = state.RecentSessions
            .Where(session => session.BranchLevels.Any(level =>
                level.Branch == status.Branch && level.Level == status.Level))
            .Take(3)
            .ToArray();
        var evidence = state.EvidenceSummaries
            .Where(artifact =>
                artifact.Event.Branch == status.Branch &&
                artifact.Event.Level == status.Level)
            .Take(3)
            .ToArray();

        var panel = new MgPanel(context);
        var title = new TextView(context)
        {
            Text = "Evidence",
        };
        MgTypography.ApplyLabel(title);
        panel.AddView(title);
        AddInlineChipRow(
            panel,
            [
                $"{sessions.Length} sessions",
                $"{evidence.Length} artifacts",
                $"{sessions.Count(session => session.CleanPerformance)} clean",
            ],
            MgColors.Hairline);

        foreach (var session in sessions)
        {
            AddSignalRow(
                panel,
                session.CleanPerformance ? "Clean" : "Fail",
                session.CleanPerformance ? MgColors.Owned : MgColors.Blocked,
                filled: session.CleanPerformance,
                [session.SessionType.ToString(), FormatDate(session.Date)]);
        }

        foreach (var artifact in evidence)
        {
            AddSignalRow(
                panel,
                artifact.Event.Kind.ToString(),
                MgColors.Hairline,
                filled: false,
                [FormatDate(artifact.Artifact.Date)]);
        }

        AddSection(panel);
    }

    private void AddSignalRow(
        LinearLayout parent,
        string marker,
        Color markerColor,
        bool filled,
        IEnumerable<string> chips,
        string? detail = null)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(
            0,
            MgSpacing.Dp(context, MgSpacing.Sm),
            0,
            MgSpacing.Dp(context, MgSpacing.Sm));

        var rail = new View(context)
        {
            Background = MgTheme.Filled(context, markerColor, cornerRadius: 4),
        };
        var railLayout = new LinearLayout.LayoutParams(
            MgSpacing.Dp(context, filled ? 8 : 4),
            MgSpacing.Dp(context, detail is null ? 36 : 54));
        railLayout.SetMargins(0, 0, MgSpacing.Dp(context, MgSpacing.Sm), 0);
        row.AddView(rail, railLayout);

        var stack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };

        var chipRow = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        chipRow.SetGravity(GravityFlags.CenterVertical);
        AddChip(chipRow, marker, markerColor, filled);
        foreach (var chip in chips)
        {
            AddChip(chipRow, chip, MgColors.Hairline, filled: false);
        }

        stack.AddView(chipRow);

        if (!string.IsNullOrWhiteSpace(detail))
        {
            var detailView = new TextView(context)
            {
                Text = detail,
            };
            detailView.SetMaxLines(2);
            MgTypography.ApplyMicro(detailView);
            var detailLayout = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);
            detailLayout.SetMargins(0, MgSpacing.Dp(context, MgSpacing.Xs), 0, 0);
            stack.AddView(detailView, detailLayout);
        }

        row.AddView(stack, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        parent.AddView(row);
    }

    private void AddInlineChipRow(LinearLayout parent, IEnumerable<string> chips, Color color)
    {
        var chipArray = chips
            .Where(chip => !string.IsNullOrWhiteSpace(chip))
            .ToArray();
        if (chipArray.Length == 0)
        {
            return;
        }

        var stack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        stack.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Sm), 0, 0);

        var maxRowWeight = InlineChipRowWeightBudget();
        var currentWeight = 0;
        LinearLayout? row = null;
        foreach (var chip in chipArray)
        {
            var chipWeight = Math.Max(8, chip.Length);
            if (row is null || currentWeight > 0 && currentWeight + chipWeight > maxRowWeight)
            {
                row = new LinearLayout(context)
                {
                    Orientation = Orientation.Horizontal,
                };
                row.SetGravity(GravityFlags.CenterVertical);
                if (stack.ChildCount > 0)
                {
                    row.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Xs), 0, 0);
                }

                stack.AddView(row);
                currentWeight = 0;
            }

            AddChip(row, chip, color, filled: false);
            currentWeight += chipWeight + 2;
        }

        parent.AddView(stack);
    }

    private int InlineChipRowWeightBudget()
    {
        var metrics = context.Resources?.DisplayMetrics;
        if (metrics is null || metrics.Density <= 0)
        {
            return 34;
        }

        var widthDp = metrics.WidthPixels / metrics.Density;
        return widthDp < 380 ? 32 : 48;
    }

    private void AddDetailRow(LinearLayout parent, string label, string value)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        row.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Sm), 0, 0);

        var labelView = new TextView(context)
        {
            Text = label,
        };
        MgTypography.ApplyMicro(labelView);

        var valueView = new TextView(context)
        {
            Text = value,
        };
        valueView.SetMaxLines(3);
        MgTypography.ApplyBody(valueView);

        row.AddView(labelView);
        row.AddView(valueView);
        parent.AddView(row);
    }

    private void AddChip(LinearLayout row, string text, Color color, bool filled)
    {
        var chip = new StatusChipView(context, text, color, filled);
        var layout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(0, 0, MgSpacing.Dp(context, MgSpacing.Sm), 0);
        row.AddView(chip, layout);
    }

    private string LiveInputOrDefault(string fallback)
    {
        return string.IsNullOrWhiteSpace(liveSessionInput)
            ? fallback
            : liveSessionInput.Trim();
    }

    private static PreUiLiveSessionCommandState? CommandState(
        PreUiLiveSessionState live,
        RuntimeInputCommandKind command)
    {
        return live.Commands.FirstOrDefault(item => item.Command == command);
    }

    private static string LabelForCommand(RuntimeInputCommandKind command)
    {
        return command switch
        {
            RuntimeInputCommandKind.MarkDrift => "Drift",
            RuntimeInputCommandKind.RespondToCue => "Respond",
            RuntimeInputCommandKind.SubmitAnswer => "Submit",
            RuntimeInputCommandKind.MarkGuess => "Guess",
            RuntimeInputCommandKind.MarkError => "Error",
            RuntimeInputCommandKind.Correct => "Correct",
            RuntimeInputCommandKind.StartAudit => "Audit",
            RuntimeInputCommandKind.FinishPhase => "Next",
            RuntimeInputCommandKind.Pause => "Pause",
            RuntimeInputCommandKind.Resume => "Resume",
            RuntimeInputCommandKind.Abandon => "Abandon",
            _ => command.ToString(),
        };
    }

    private static string LiveLifecycleLabel(RuntimeSessionLifecycleStatus status)
    {
        return status switch
        {
            RuntimeSessionLifecycleStatus.Running => "Run",
            RuntimeSessionLifecycleStatus.Paused => "Pause",
            RuntimeSessionLifecycleStatus.Completed => "Done",
            RuntimeSessionLifecycleStatus.Abandoned => "Left",
            RuntimeSessionLifecycleStatus.Failed => "Fail",
            RuntimeSessionLifecycleStatus.NotStarted => "Ready",
            _ => status.ToString(),
        };
    }

    private static string ActiveSessionMarker(
        PreUiActiveSessionResumeStatus status,
        RuntimeSessionLifecycleStatus? lifecycleStatus)
    {
        if (status == PreUiActiveSessionResumeStatus.Unsafe &&
            lifecycleStatus == RuntimeSessionLifecycleStatus.Abandoned)
        {
            return "Abandoned";
        }

        return status switch
        {
            PreUiActiveSessionResumeStatus.Resumable => "Resume",
            PreUiActiveSessionResumeStatus.NotPersisted => "Live",
            PreUiActiveSessionResumeStatus.Unsafe => "Unsafe",
            PreUiActiveSessionResumeStatus.NotFound => "No active",
            _ => status.ToString(),
        };
    }

    private static Color ActiveSessionColor(
        PreUiActiveSessionResumeStatus status,
        RuntimeSessionLifecycleStatus? lifecycleStatus)
    {
        if (status == PreUiActiveSessionResumeStatus.Unsafe ||
            lifecycleStatus is RuntimeSessionLifecycleStatus.Abandoned or RuntimeSessionLifecycleStatus.Failed)
        {
            return MgColors.Blocked;
        }

        if (lifecycleStatus == RuntimeSessionLifecycleStatus.Paused)
        {
            return MgColors.Recovery;
        }

        return status == PreUiActiveSessionResumeStatus.Resumable
            ? MgColors.Training
            : MgColors.Hairline;
    }

    private static IReadOnlyList<string> ActiveSessionChips(PreUiActiveSessionResumeState active)
    {
        var chips = new List<string>();
        if (FormatBranchLevel(active.Branch, active.Level) is { } branchLevel)
        {
            chips.Add(branchLevel);
        }

        if (active.Drill is { } drill)
        {
            chips.Add(DrillCodeFor(drill));
        }

        if (active.LifecycleStatus is { } lifecycle)
        {
            chips.Add(LiveLifecycleLabel(lifecycle));
        }

        if (active.ActivePhaseKind is { } phase)
        {
            chips.Add(FormatPhase(phase));
        }

        chips.Add("No grant");
        return chips;
    }

    private static string ActiveSessionDetail(PreUiActiveSessionResumeState active)
    {
        var prefix = active.Status == PreUiActiveSessionResumeStatus.Unsafe
            ? "Runtime did not accept honest continuation."
            : "Active runtime state came from the app snapshot service.";

        return $"{prefix} {active.Detail}";
    }

    private static Color ColorForLifecycle(RuntimeSessionLifecycleStatus status)
    {
        return status switch
        {
            RuntimeSessionLifecycleStatus.Running => MgColors.Training,
            RuntimeSessionLifecycleStatus.Paused => MgColors.Recovery,
            RuntimeSessionLifecycleStatus.Completed => MgColors.Owned,
            RuntimeSessionLifecycleStatus.Abandoned or RuntimeSessionLifecycleStatus.Failed => MgColors.Blocked,
            _ => MgColors.Hairline,
        };
    }

    private static string CompletionLabel(RuntimeSessionCompletionStatus? status)
    {
        return status switch
        {
            RuntimeSessionCompletionStatus.Completed => "Saved",
            RuntimeSessionCompletionStatus.Abandoned => "Left",
            RuntimeSessionCompletionStatus.Failed => "Fail",
            RuntimeSessionCompletionStatus.TimedOut => "Time",
            null => "Wait",
            _ => status.ToString()!,
        };
    }

    private static ResultVisualState ResultVisualFor(PreUiLiveSessionCompletionResult completion)
    {
        var processing = completion.WorkflowResult?.ProcessingResult;
        var status = processing?.CompletionStatus ?? completion.RuntimeCompletionStatus;
        if (status == RuntimeSessionCompletionStatus.Abandoned)
        {
            return new ResultVisualState(
                ResultVisualKind.Abandoned,
                "Left",
                "Abandoned",
                MgColors.Blocked,
                true,
                "Session was left; no progress is granted.");
        }

        if (status == RuntimeSessionCompletionStatus.TimedOut)
        {
            return new ResultVisualState(
                ResultVisualKind.TimedOut,
                "Time",
                "Timed out",
                MgColors.Blocked,
                true,
                "Runtime timed out; no progress is granted.");
        }

        if (status == RuntimeSessionCompletionStatus.Failed)
        {
            return new ResultVisualState(
                ResultVisualKind.Failed,
                "Fail",
                "Failed",
                MgColors.Blocked,
                true,
                "Runtime completed as failed; no success is implied.");
        }

        if (processing is null || !completion.IsProcessed)
        {
            return new ResultVisualState(
                ResultVisualKind.Waiting,
                "Wait",
                "Processing",
                MgColors.Hairline,
                false,
                "Waiting for app workflow completion.");
        }

        if (IsDecayedResult(processing))
        {
            return new ResultVisualState(
                ResultVisualKind.Decayed,
                "Decay",
                "Decayed",
                MgColors.Blocked,
                true,
                "Decay blocks dependent advancement until restoration.");
        }

        if (processing.MaintenanceCurrencyResult?.State == MaintenanceCurrencyState.Warning)
        {
            return new ResultVisualState(
                ResultVisualKind.Warning,
                "Warn",
                "Warning",
                MgColors.Maintenance,
                true,
                "Maintenance warning remains visible; advancement is not softened.");
        }

        if (processing.FormalGateDecision?.Outcome == GateOutcome.Fail ||
            processing.StandardEvaluationResult?.Passed == false ||
            processing.TransferEligibilityResult?.IsEligible == false ||
            processing.FailureResponse is not null)
        {
            return new ResultVisualState(
                ResultVisualKind.Failed,
                "Fail",
                "Failed",
                MgColors.Blocked,
                true,
                "Core/app result did not pass the required standard or gate.");
        }

        if (IsPassedOnceResult(processing))
        {
            return new ResultVisualState(
                ResultVisualKind.PassedOnce,
                "1/3",
                "Passed once",
                MgColors.PassedOnce,
                false,
                "Formal standard passed once; ownership remains locked.");
        }

        if (IsMaintenancePassResult(processing))
        {
            return new ResultVisualState(
                ResultVisualKind.MaintenancePass,
                "Maint",
                "Maintenance",
                MgColors.Maintenance,
                false,
                "Maintenance was preserved; this is not new advancement.");
        }

        if (IsOwnedResult(processing))
        {
            return new ResultVisualState(
                ResultVisualKind.Owned,
                "Owned",
                "Owned",
                MgColors.Owned,
                true,
                "Core/app result marks this branch-level owned.");
        }

        if (IsStabilizingResult(processing))
        {
            return new ResultVisualState(
                ResultVisualKind.Stabilizing,
                "Stab",
                "Stabilizing",
                MgColors.TestReady,
                false,
                "Clean repeatability is still being stabilized.");
        }

        return new ResultVisualState(
            ResultVisualKind.Recorded,
            "Saved",
            "Recorded",
            MgColors.Hairline,
            false,
            "Session was recorded; Android grants no progress.");
    }

    private static bool IsDecayedResult(CompletedRuntimeSessionProcessingResult processing)
    {
        return processing.StateTransition?.NextStatus.State == BranchLevelState.Decayed ||
            processing.DecayResult is { ChangedState: true, NextStatus.State: BranchLevelState.Decayed } ||
            processing.MaintenanceCurrencyResult?.State == MaintenanceCurrencyState.Failed;
    }

    private static bool IsPassedOnceResult(CompletedRuntimeSessionProcessingResult processing)
    {
        return processing.FormalGateDecision?.Outcome == GateOutcome.PassOnce ||
            processing.StateTransition?.NextStatus.State == BranchLevelState.PassedOnce;
    }

    private static bool IsOwnedResult(CompletedRuntimeSessionProcessingResult processing)
    {
        var transition = processing.StateTransition;
        return processing.StabilizationOwnershipResult?.IsOwned == true ||
            processing.FormalGateDecision?.Outcome == GateOutcome.Own ||
            (transition?.NextStatus.State == BranchLevelState.Owned &&
                transition?.Transition != BranchLevelTransition.PassMaintenance);
    }

    private static bool IsStabilizingResult(CompletedRuntimeSessionProcessingResult processing)
    {
        return processing.FormalGateDecision?.Outcome == GateOutcome.Stabilize ||
            processing.StabilizationOwnershipResult?.BranchLevelState == BranchLevelState.Stabilizing ||
            processing.StateTransition?.NextStatus.State == BranchLevelState.Stabilizing;
    }

    private static bool IsMaintenancePassResult(CompletedRuntimeSessionProcessingResult processing)
    {
        return processing.FormalGateDecision?.Outcome == GateOutcome.Maintain ||
            processing.MaintenanceCurrencyResult?.State == MaintenanceCurrencyState.Current ||
            processing.StateTransition?.Transition == BranchLevelTransition.PassMaintenance;
    }

    private static bool RuntimeTerminalProblem(RuntimeSessionCompletionStatus? status)
    {
        return status is RuntimeSessionCompletionStatus.Abandoned or
            RuntimeSessionCompletionStatus.Failed or
            RuntimeSessionCompletionStatus.TimedOut;
    }

    private static Color ColorForRuntimeCompletion(RuntimeSessionCompletionStatus status)
    {
        return RuntimeTerminalProblem(status) ? MgColors.Blocked : MgColors.Hairline;
    }

    private static Color ColorForGateOutcome(GateOutcome outcome)
    {
        return outcome switch
        {
            GateOutcome.Fail or GateOutcome.Regress => MgColors.Blocked,
            GateOutcome.PassOnce => MgColors.PassedOnce,
            GateOutcome.Stabilize => MgColors.TestReady,
            GateOutcome.Own => MgColors.Owned,
            GateOutcome.Maintain => MgColors.Maintenance,
            GateOutcome.Review => MgColors.Recovery,
            _ => MgColors.Hairline,
        };
    }

    private static bool GateOutcomeHasHardStop(GateOutcome outcome)
    {
        return outcome is GateOutcome.Fail or GateOutcome.Regress;
    }

    private static string FormatGateOutcome(GateOutcome outcome)
    {
        return outcome switch
        {
            GateOutcome.PassOnce => "Pass once",
            GateOutcome.Stabilize => "Stabilize",
            GateOutcome.Maintain => "Maintain",
            GateOutcome.Regress => "Regress",
            _ => outcome.ToString(),
        };
    }

    private static string MarkerForMaintenanceState(MaintenanceCurrencyState state)
    {
        return state switch
        {
            MaintenanceCurrencyState.Current => "Maint",
            MaintenanceCurrencyState.Due => "Due",
            MaintenanceCurrencyState.Warning => "Warn",
            MaintenanceCurrencyState.Failed => "Decay",
            _ => state.ToString(),
        };
    }

    private static Color ColorForMaintenanceState(MaintenanceCurrencyState state)
    {
        return state switch
        {
            MaintenanceCurrencyState.Current => MgColors.Maintenance,
            MaintenanceCurrencyState.Due => MgColors.Maintenance,
            MaintenanceCurrencyState.Warning => MgColors.Maintenance,
            MaintenanceCurrencyState.Failed => MgColors.Blocked,
            _ => MgColors.Hairline,
        };
    }

    private static string DaysSinceMaintenancePass(MaintenanceCurrencyResult maintenance)
    {
        return maintenance.DaysSinceLastPassingCheck is { } days
            ? $"{days}d"
            : "No pass";
    }

    private static bool TransitionHasHighSignal(BranchLevelStatusTransitionResult transition)
    {
        return transition.NextStatus.State is BranchLevelState.Owned or BranchLevelState.Decayed ||
            transition.Transition is BranchLevelTransition.FailFormalTest or
                BranchLevelTransition.FailStabilization or
                BranchLevelTransition.MarkDecayed;
    }

    private static string PascalWords(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var characters = new List<char>(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (index > 0 && char.IsUpper(current) && !char.IsWhiteSpace(value[index - 1]))
            {
                characters.Add(' ');
            }

            characters.Add(current);
        }

        return new string(characters.ToArray());
    }

    private static string FormatPhase(RuntimeSessionPhaseKind? phase)
    {
        return phase switch
        {
            RuntimeSessionPhaseKind.InstructionPrep => "Prep",
            RuntimeSessionPhaseKind.EncodeWindow => "Encode",
            RuntimeSessionPhaseKind.ActiveWork => "Work",
            RuntimeSessionPhaseKind.DelayWindow => "Delay",
            RuntimeSessionPhaseKind.CueResponse => "Cue",
            RuntimeSessionPhaseKind.ReconstructionInput => "Rebuild",
            RuntimeSessionPhaseKind.Audit => "Audit",
            RuntimeSessionPhaseKind.Rest => "Rest",
            RuntimeSessionPhaseKind.Recovery => "Recover",
            RuntimeSessionPhaseKind.Review => "Review",
            null => "Done",
            _ => phase.ToString()!,
        };
    }

    private static string FormatDuration(TimeSpan value)
    {
        return value.TotalSeconds < 60
            ? $"{Math.Ceiling(value.TotalSeconds):0}s"
            : $"{(int)value.TotalMinutes}:{value.Seconds:00}";
    }

    private static string ShortMaterialKind(string kind)
    {
        return kind.Length <= 6 ? kind : kind[..6];
    }

    private static IReadOnlyList<EvidenceTimelineItem> BuildEvidenceTimelineItems(CurrentTrainingStateReadModel state)
    {
        var items = new List<EvidenceTimelineItem>();
        var artifactIds = new HashSet<string>(
            state.EvidenceSummaries.Select(record => record.ArtifactId),
            StringComparer.Ordinal);

        foreach (var artifact in state.EvidenceSummaries)
        {
            items.Add(BuildArtifactEvidenceItem(state, artifact));
        }

        foreach (var session in state.RecentSessions.Where(session =>
                     !session.EvidenceArtifactIds.Any(artifactIds.Contains)))
        {
            items.Add(BuildSessionEvidenceItem(session));
        }

        foreach (var formal in state.ProgressRecords.FormalTestAttempts.Where(record =>
                     record.EvidenceArtifactId is null || !artifactIds.Contains(record.EvidenceArtifactId)))
        {
            items.Add(BuildFormalEvidenceItem(formal, FindSessionForFormalAttempt(state, formal)));
        }

        foreach (var stabilization in state.ProgressRecords.StabilizationPasses.Where(record =>
                     !artifactIds.Contains(record.EvidenceArtifactId)))
        {
            items.Add(BuildStabilizationEvidenceItem(
                stabilization,
                FindSessionForStabilizationPass(state, stabilization)));
        }

        foreach (var maintenance in state.ProgressRecords.MaintenanceChecks.Where(record =>
                     !artifactIds.Contains(record.EvidenceArtifactId)))
        {
            items.Add(BuildMaintenanceEvidenceItem(
                maintenance,
                FindSessionForMaintenanceCheck(state, maintenance)));
        }

        foreach (var decay in state.ProgressRecords.DecayHistory)
        {
            items.Add(BuildDecayEvidenceItem(decay));
        }

        foreach (var restoration in state.ProgressRecords.RestorationHistory)
        {
            items.Add(BuildRestorationEvidenceItem(restoration));
        }

        return items
            .OrderByDescending(item => item.Date.Year)
            .ThenByDescending(item => item.Date.Month)
            .ThenByDescending(item => item.Date.Day)
            .ThenBy(item => EvidenceCategoryOrder(item.Category))
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static EvidenceTimelineItem BuildArtifactEvidenceItem(
        CurrentTrainingStateReadModel state,
        LocalEvidenceArtifactRecord artifact)
    {
        var session = FindSessionForArtifact(state, artifact);
        var formal = FindFormalAttemptForArtifact(state, artifact);
        if (formal is not null)
        {
            return BuildFormalEvidenceItem(formal, session, artifact);
        }

        var stabilization = FindStabilizationPassForArtifact(state, artifact);
        if (stabilization is not null)
        {
            return BuildStabilizationEvidenceItem(stabilization, session, artifact);
        }

        var maintenance = FindMaintenanceCheckForArtifact(state, artifact);
        if (maintenance is not null)
        {
            return BuildMaintenanceEvidenceItem(maintenance, session, artifact);
        }

        var category = FormatEvidenceArtifactCategory(artifact.Artifact.Category);
        var problem = session?.CleanPerformance == false || ArtifactHasProblemMarker(artifact.Artifact);
        var marker = problem ? "Fail" : MarkerForEvidenceArtifactCategory(artifact.Artifact.Category);
        var color = problem ? MgColors.Blocked : ColorForEvidenceArtifactCategory(artifact.Artifact.Category);

        var chips = new List<string>
        {
            FormatDate(artifact.Artifact.Date),
            FormatEvidenceBranchLevel(artifact.Event, session),
            FormatEvidenceTask(artifact.Event, session),
            category,
        };

        return new EvidenceTimelineItem(
            $"artifact-{artifact.ArtifactId}",
            category,
            artifact.Artifact.Date,
            marker,
            color,
            Filled: problem,
            LimitChips(chips, chips.Count),
            artifact.Artifact.SummaryOrReference,
            Artifact: artifact,
            Session: session);
    }

    private static EvidenceTimelineItem BuildSessionEvidenceItem(LocalSessionHistoryRecord session)
    {
        var category = FormatCompletedSessionType(session.SessionType);
        var marker = session.CleanPerformance ? "Clean" : "Fail";
        var color = session.CleanPerformance ? MgColors.Hairline : MgColors.Blocked;
        var chips = new List<string>
        {
            FormatDate(session.Date),
            FormatSessionBranchLevels(session),
            FormatSessionTask(session),
            category,
        };

        return new EvidenceTimelineItem(
            $"session-{session.SessionId}",
            category,
            session.Date,
            marker,
            color,
            Filled: !session.CleanPerformance,
            LimitChips(chips, chips.Count),
            session.TransferTask,
            Session: session);
    }

    private static EvidenceTimelineItem BuildFormalEvidenceItem(
        LocalFormalTestAttemptRecord formal,
        LocalSessionHistoryRecord? session,
        LocalEvidenceArtifactRecord? artifact = null)
    {
        var attempt = formal.Attempt;
        var transfer = attempt.Task.TransferTask is not null;
        var failed = attempt.PassState == FormalTestPassState.Fail;
        var category = transfer ? "Transfer" : "Formal";
        var marker = failed ? "Fail" : FormatPassState(attempt.PassState);
        var color = failed ? MgColors.Blocked : ColorForPassState(attempt.PassState);
        var chips = new List<string>
        {
            FormatDate(attempt.Date),
            FormatBranchLevel(attempt.Branch, attempt.Level) ?? "Formal",
            FormatTestTask(attempt.Task, session?.Drill),
            FormatPassState(attempt.PassState),
        };

        return new EvidenceTimelineItem(
            $"formal-{formal.AttemptId}",
            category,
            attempt.Date,
            marker,
            color,
            Filled: failed || attempt.PassState == FormalTestPassState.Owned,
            LimitChips(chips, chips.Count),
            artifact?.Artifact.SummaryOrReference ?? attempt.Artifact.SummaryOrReference,
            Artifact: artifact,
            Session: session,
            FormalAttempt: formal);
    }

    private static EvidenceTimelineItem BuildStabilizationEvidenceItem(
        LocalStabilizationPassRecord stabilization,
        LocalSessionHistoryRecord? session,
        LocalEvidenceArtifactRecord? artifact = null)
    {
        var evidence = stabilization.Evidence;
        var passed = evidence.StandardEvaluationResult.Passed;
        var chips = new List<string>
        {
            FormatDate(evidence.Date),
            FormatBranchLevel(evidence.Branch, evidence.Level) ?? "Stab",
            stabilization.Drill is { } drill ? DrillCodeFor(drill) : FormatSessionTask(session),
            FormatStabilizationCondition(stabilization.Condition),
        };

        return new EvidenceTimelineItem(
            $"stabilization-{stabilization.PassId}",
            "Stabilization",
            evidence.Date,
            passed ? "Stab" : "Fail",
            passed ? MgColors.TestReady : MgColors.Blocked,
            Filled: !passed,
            LimitChips(chips, chips.Count),
            artifact?.Artifact.SummaryOrReference ?? stabilization.ConditionDescription,
            Artifact: artifact,
            Session: session,
            StabilizationPass: stabilization);
    }

    private static EvidenceTimelineItem BuildMaintenanceEvidenceItem(
        LocalMaintenanceCheckRecord maintenance,
        LocalSessionHistoryRecord? session,
        LocalEvidenceArtifactRecord? artifact = null)
    {
        var evidence = maintenance.Evidence;
        var passed = evidence.StandardEvaluationResult.Passed;
        var chips = new List<string>
        {
            FormatDate(evidence.Date),
            FormatBranchLevel(evidence.Branch, evidence.OwnedLevel) ?? "Maint",
            maintenance.Drill is { } drill ? DrillCodeFor(drill) : FormatSessionTask(session),
            FormatMaintenanceKind(evidence.Kind),
        };

        return new EvidenceTimelineItem(
            $"maintenance-{maintenance.CheckId}",
            "Maintenance",
            evidence.Date,
            passed ? "Maint" : "Fail",
            passed ? MgColors.Maintenance : MgColors.Blocked,
            Filled: !passed,
            LimitChips(chips, chips.Count),
            artifact?.Artifact.SummaryOrReference,
            Artifact: artifact,
            Session: session,
            MaintenanceCheck: maintenance);
    }

    private static EvidenceTimelineItem BuildDecayEvidenceItem(LocalDecayHistoryRecord decay)
    {
        var chips = new List<string>
        {
            FormatDate(decay.Date),
            FormatBranchLevel(decay.NextStatus),
            "Maintenance fail",
            $"{decay.MaintenanceCheckIds.Count} checks",
        };

        return new EvidenceTimelineItem(
            $"decay-{decay.DecayId}",
            "Decay",
            decay.Date,
            "Decay",
            MgColors.Blocked,
            Filled: true,
            LimitChips(chips, chips.Count),
            "Decay caps dependent advancement until restoration evidence exists.",
            Decay: decay);
    }

    private static EvidenceTimelineItem BuildRestorationEvidenceItem(LocalRestorationHistoryRecord restoration)
    {
        var chips = new List<string>
        {
            FormatDate(restoration.Date),
            FormatBranchLevel(restoration.NextStatus),
            StateMarkerView.LabelTextFor(restoration.NextStatus.State),
            $"{restoration.RestorationCheckIds.Count} checks",
        };

        return new EvidenceTimelineItem(
            $"restoration-{restoration.RestorationId}",
            "Restore",
            restoration.Date,
            "Restore",
            MgColors.Maintenance,
            Filled: false,
            LimitChips(chips, chips.Count),
            "Restoration requires last owned standard plus lower-load transfer check.",
            Restoration: restoration);
    }

    private static IReadOnlyList<string> EvidenceAuditChips(EvidenceTimelineItem item)
    {
        var chips = new List<string>
        {
            item.Category,
        };

        if (item.Artifact is not null)
        {
            chips.Add("Artifact");
        }

        if (item.Session is not null)
        {
            chips.Add("Session");
        }

        if (EvidenceStandardDetail(item) is not null)
        {
            chips.Add("Standard");
        }

        if (EvidenceConstraintDetail(item) is not null)
        {
            chips.Add("Constraint");
        }

        if (EvidenceResultDetail(item) is not null)
        {
            chips.Add("Score");
        }

        if (EvidenceFailureDetail(item) is not null)
        {
            chips.Add("Failure");
        }

        return LimitChips(chips, chips.Count);
    }

    private static IReadOnlyList<(string Label, string Value)> EvidenceDetailRows(EvidenceTimelineItem item)
    {
        var rows = new List<(string Label, string Value)>
        {
            ("Source", EvidenceSourceDetail(item)),
        };

        if (item.Session is not null)
        {
            rows.Add(("Session", EvidenceSessionDetail(item.Session)));
        }

        AddOptionalDetail(rows, "Standard", EvidenceStandardDetail(item));
        AddOptionalDetail(rows, "Constraint", EvidenceConstraintDetail(item));
        AddOptionalDetail(rows, "Result", EvidenceResultDetail(item));
        AddOptionalDetail(rows, "Failure", EvidenceFailureDetail(item));
        AddOptionalDetail(rows, "Load", EvidenceLoadDetail(item));
        AddOptionalDetail(rows, "Transfer", EvidenceTransferDetail(item));
        AddOptionalDetail(rows, "Observed", EvidenceObservedDetail(item));

        return rows;
    }

    private static void AddOptionalDetail(
        ICollection<(string Label, string Value)> rows,
        string label,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            rows.Add((label, value));
        }
    }

    private static string EvidenceSourceDetail(EvidenceTimelineItem item)
    {
        var parts = new List<string>();
        if (item.Artifact is not null)
        {
            parts.Add($"artifact {ShortId(item.Artifact.ArtifactId)}");
            parts.Add($"event {ShortId(item.Artifact.Event.EventId)}");
        }

        if (item.Session is not null)
        {
            parts.Add($"session {ShortId(item.Session.SessionId)}");
        }

        if (item.FormalAttempt is not null)
        {
            parts.Add($"attempt {ShortId(item.FormalAttempt.AttemptId)}");
        }

        if (item.StabilizationPass is not null)
        {
            parts.Add($"stabilization {ShortId(item.StabilizationPass.PassId)}");
        }

        if (item.MaintenanceCheck is not null)
        {
            parts.Add($"maintenance {ShortId(item.MaintenanceCheck.CheckId)}");
        }

        if (item.Decay is not null)
        {
            parts.Add($"decay {ShortId(item.Decay.DecayId)}");
        }

        if (item.Restoration is not null)
        {
            parts.Add($"restoration {ShortId(item.Restoration.RestorationId)}");
        }

        return parts.Count == 0 ? item.Key : string.Join("; ", parts);
    }

    private static string EvidenceSessionDetail(LocalSessionHistoryRecord session)
    {
        var state = session.CleanPerformance ? "clean" : "not clean";
        return $"{FormatCompletedSessionType(session.SessionType)}; {state}; {session.Intensity}; {session.EvidenceArtifactIds.Count} artifact refs.";
    }

    private static string? EvidenceStandardDetail(EvidenceTimelineItem item)
    {
        if (item.FormalAttempt is not null)
        {
            return item.FormalAttempt.Attempt.Standard;
        }

        if (item.StabilizationPass is not null)
        {
            return item.StabilizationPass.Evidence.Standard;
        }

        if (item.MaintenanceCheck is not null)
        {
            return item.MaintenanceCheck.Standard;
        }

        return item.Artifact?.Event.Kind == LocalProgrammingEventKind.GlobalReview
            ? "Whole-practitioner review artifact."
            : null;
    }

    private static string? EvidenceConstraintDetail(EvidenceTimelineItem item)
    {
        if (item.FormalAttempt is not null)
        {
            return string.Join("; ", item.FormalAttempt.Attempt.CriticalConstraints.Select(constraint => constraint.Description));
        }

        var criticalEvidence = item.Artifact?.Artifact.ObservableEvidence
            .Where(evidence => evidence.Kind == ObservableEvidenceKind.CriticalConstraintRecord)
            .Select(evidence => evidence.Description)
            .ToArray();
        if (criticalEvidence is { Length: > 0 })
        {
            return string.Join("; ", criticalEvidence);
        }

        var evaluation = item.StabilizationPass?.Evidence.StandardEvaluationResult ??
            item.MaintenanceCheck?.Evidence.StandardEvaluationResult;
        if (evaluation is null)
        {
            return null;
        }

        var criticalFailures = evaluation.Failures
            .Where(failure => failure.Kind == StandardFailureKind.CriticalConstraintBroken)
            .Select(failure => failure.Detail)
            .ToArray();
        return criticalFailures.Length == 0
            ? "No critical-constraint failure recorded."
            : string.Join("; ", criticalFailures);
    }

    private static string? EvidenceResultDetail(EvidenceTimelineItem item)
    {
        if (item.FormalAttempt is not null)
        {
            var attempt = item.FormalAttempt.Attempt;
            return $"{attempt.ResultEvidence.Kind}: {attempt.ResultEvidence.Value}; {FormatPassState(attempt.PassState)}.";
        }

        if (item.StabilizationPass is not null)
        {
            return $"{FormatPassState(item.StabilizationPass.Evidence.PassState)}; {FormatStandardEvaluation(item.StabilizationPass.Evidence.StandardEvaluationResult)}.";
        }

        if (item.MaintenanceCheck is not null)
        {
            return $"{FormatMaintenanceKind(item.MaintenanceCheck.Evidence.Kind)}; {FormatStandardEvaluation(item.MaintenanceCheck.Evidence.StandardEvaluationResult)}.";
        }

        if (item.Restoration is not null)
        {
            return string.Join("; ", item.Restoration.Evidence.Checks.Select(check =>
                $"{FormatRestorationKind(check.Kind)} {FormatStandardEvaluation(check.StandardEvaluationResult)}"));
        }

        var scoredEvidence = item.Artifact?.Artifact.ObservableEvidence
            .Where(evidence => evidence.Kind is
                ObservableEvidenceKind.Score or
                ObservableEvidenceKind.Time or
                ObservableEvidenceKind.ErrorCount or
                ObservableEvidenceKind.AuditResult or
                ObservableEvidenceKind.MaintenanceCheck)
            .Select(evidence => $"{PascalWords(evidence.Kind.ToString())}: {evidence.Description}")
            .ToArray();
        return scoredEvidence is { Length: > 0 } ? string.Join("; ", scoredEvidence) : null;
    }

    private static string? EvidenceFailureDetail(EvidenceTimelineItem item)
    {
        if (item.FormalAttempt?.Attempt.FailureType is { } failureType)
        {
            return PascalWords(failureType.ToString());
        }

        var evaluation = item.StabilizationPass?.Evidence.StandardEvaluationResult ??
            item.MaintenanceCheck?.Evidence.StandardEvaluationResult;
        if (evaluation is not null && evaluation.Failures.Count > 0)
        {
            return string.Join("; ", evaluation.Failures.Select(failure =>
                $"{PascalWords(failure.Kind.ToString())}: {failure.Detail}"));
        }

        if (item.Session?.CleanPerformance == false)
        {
            return "Session record marked not clean.";
        }

        if (item.Decay is not null)
        {
            return "Maintenance decay recorded from failed check references.";
        }

        return null;
    }

    private static string? EvidenceLoadDetail(EvidenceTimelineItem item)
    {
        var loadVariables = item.FormalAttempt?.Attempt.LoadVariables ?? item.Session?.LoadVariables;
        return loadVariables is null ? null : FormatLoadVariables(loadVariables);
    }

    private static string? EvidenceTransferDetail(EvidenceTimelineItem item)
    {
        if (item.FormalAttempt?.Attempt.Task.TransferTask is { } transferTask)
        {
            return transferTask;
        }

        return item.Session?.TransferTask;
    }

    private static string? EvidenceObservedDetail(EvidenceTimelineItem item)
    {
        var artifact = item.Artifact?.Artifact ?? item.FormalAttempt?.Attempt.Artifact;
        if (artifact is null)
        {
            return null;
        }

        return string.Join(
            "; ",
            artifact.ObservableEvidence
                .Take(4)
                .Select(evidence => $"{PascalWords(evidence.Kind.ToString())}: {evidence.Description}"));
    }

    private static LocalSessionHistoryRecord? FindSessionForArtifact(
        CurrentTrainingStateReadModel state,
        LocalEvidenceArtifactRecord artifact)
    {
        return state.RecentSessions.FirstOrDefault(session =>
            string.Equals(session.SessionId, artifact.Event.EventId, StringComparison.Ordinal) ||
            session.EvidenceArtifactIds.Contains(artifact.ArtifactId, StringComparer.Ordinal));
    }

    private static LocalSessionHistoryRecord? FindSessionForFormalAttempt(
        CurrentTrainingStateReadModel state,
        LocalFormalTestAttemptRecord formal)
    {
        return formal.EvidenceArtifactId is null
            ? null
            : FindSessionByEvidenceArtifactId(state, formal.EvidenceArtifactId);
    }

    private static LocalSessionHistoryRecord? FindSessionForStabilizationPass(
        CurrentTrainingStateReadModel state,
        LocalStabilizationPassRecord stabilization)
    {
        return state.RecentSessions.FirstOrDefault(session =>
            string.Equals(session.SessionId, stabilization.CompletedSessionId, StringComparison.Ordinal) ||
            session.EvidenceArtifactIds.Contains(stabilization.EvidenceArtifactId, StringComparer.Ordinal));
    }

    private static LocalSessionHistoryRecord? FindSessionForMaintenanceCheck(
        CurrentTrainingStateReadModel state,
        LocalMaintenanceCheckRecord maintenance)
    {
        return state.RecentSessions.FirstOrDefault(session =>
            string.Equals(session.SessionId, maintenance.CompletedSessionId, StringComparison.Ordinal) ||
            session.EvidenceArtifactIds.Contains(maintenance.EvidenceArtifactId, StringComparer.Ordinal));
    }

    private static LocalSessionHistoryRecord? FindSessionByEvidenceArtifactId(
        CurrentTrainingStateReadModel state,
        string evidenceArtifactId)
    {
        return state.RecentSessions.FirstOrDefault(session =>
            session.EvidenceArtifactIds.Contains(evidenceArtifactId, StringComparer.Ordinal));
    }

    private static LocalFormalTestAttemptRecord? FindFormalAttemptForArtifact(
        CurrentTrainingStateReadModel state,
        LocalEvidenceArtifactRecord artifact)
    {
        return state.ProgressRecords.FormalTestAttempts.FirstOrDefault(record =>
            string.Equals(record.AttemptId, artifact.Event.EventId, StringComparison.Ordinal) ||
            string.Equals(record.EvidenceArtifactId, artifact.ArtifactId, StringComparison.Ordinal));
    }

    private static LocalStabilizationPassRecord? FindStabilizationPassForArtifact(
        CurrentTrainingStateReadModel state,
        LocalEvidenceArtifactRecord artifact)
    {
        return state.ProgressRecords.StabilizationPasses.FirstOrDefault(record =>
            string.Equals(record.PassId, artifact.Event.EventId, StringComparison.Ordinal) ||
            string.Equals(record.EvidenceArtifactId, artifact.ArtifactId, StringComparison.Ordinal));
    }

    private static LocalMaintenanceCheckRecord? FindMaintenanceCheckForArtifact(
        CurrentTrainingStateReadModel state,
        LocalEvidenceArtifactRecord artifact)
    {
        return state.ProgressRecords.MaintenanceChecks.FirstOrDefault(record =>
            string.Equals(record.CheckId, artifact.Event.EventId, StringComparison.Ordinal) ||
            string.Equals(record.EvidenceArtifactId, artifact.ArtifactId, StringComparison.Ordinal));
    }

    private static bool ArtifactHasProblemMarker(EvidenceArtifact artifact)
    {
        return artifact.ObservableEvidence.Any(evidence =>
            evidence.Kind == ObservableEvidenceKind.FailedItemList);
    }

    private static int EvidenceCategoryOrder(string category)
    {
        return category switch
        {
            "Practice" => 0,
            "Load" => 1,
            "Formal" => 2,
            "Stabilization" => 3,
            "Transfer" => 4,
            "Maintenance" => 5,
            "Review" => 6,
            "Recovery" => 7,
            "Decay" => 8,
            "Restore" => 9,
            _ => 10,
        };
    }

    private static string FormatEvidenceArtifactCategory(EvidenceArtifactCategory category)
    {
        return category switch
        {
            EvidenceArtifactCategory.Test => "Formal",
            EvidenceArtifactCategory.Stabilization => "Stabilization",
            EvidenceArtifactCategory.Maintenance => "Maintenance",
            EvidenceArtifactCategory.GlobalReview => "Review",
            _ => category.ToString(),
        };
    }

    private static string MarkerForEvidenceArtifactCategory(EvidenceArtifactCategory category)
    {
        return category switch
        {
            EvidenceArtifactCategory.Test => "Formal",
            EvidenceArtifactCategory.Stabilization => "Stab",
            EvidenceArtifactCategory.Transfer => "Transfer",
            EvidenceArtifactCategory.Maintenance => "Maint",
            EvidenceArtifactCategory.GlobalReview => "Review",
            EvidenceArtifactCategory.Load => "Load",
            _ => "Practice",
        };
    }

    private static Color ColorForEvidenceArtifactCategory(EvidenceArtifactCategory category)
    {
        return category switch
        {
            EvidenceArtifactCategory.Test => MgColors.TestReady,
            EvidenceArtifactCategory.Stabilization => MgColors.TestReady,
            EvidenceArtifactCategory.Transfer => MgColors.Transfer,
            EvidenceArtifactCategory.Maintenance => MgColors.Maintenance,
            EvidenceArtifactCategory.GlobalReview => MgColors.Recovery,
            EvidenceArtifactCategory.Load => MgColors.Training,
            _ => MgColors.Hairline,
        };
    }

    private static string FormatEvidenceBranchLevel(
        LocalProgrammingEventReference eventReference,
        LocalSessionHistoryRecord? session)
    {
        if (eventReference.Branch is { } branch && eventReference.Level is { } level)
        {
            return FormatBranchLevel(branch, level) ?? branch.ToString();
        }

        return session is null ? "Global" : FormatSessionBranchLevels(session);
    }

    private static string FormatEvidenceTask(
        LocalProgrammingEventReference eventReference,
        LocalSessionHistoryRecord? session)
    {
        if (eventReference.Drill is { } drill)
        {
            return DrillCodeFor(drill);
        }

        return FormatSessionTask(session);
    }

    private static string FormatSessionBranchLevels(LocalSessionHistoryRecord session)
    {
        var labels = session.BranchLevels
            .Select(branchLevel => FormatBranchLevel(branchLevel.Branch, branchLevel.Level) ?? branchLevel.Branch.ToString())
            .ToArray();
        return labels.Length == 1 ? labels[0] : $"{labels[0]} +{labels.Length - 1}";
    }

    private static string FormatSessionTask(LocalSessionHistoryRecord? session)
    {
        if (session is null)
        {
            return "No session";
        }

        if (session.TransferTask is not null)
        {
            return "Transfer";
        }

        return session.Drill is { } drill ? DrillCodeFor(drill) : FormatCompletedSessionType(session.SessionType);
    }

    private static string FormatTestTask(TestTask task, DrillId? fallbackDrill)
    {
        if (task.TransferTask is not null)
        {
            return "Transfer";
        }

        return task.Drill is { } drill
            ? DrillCodeFor(drill)
            : fallbackDrill is { } fallback
                ? DrillCodeFor(fallback)
                : "Task";
    }

    private static string FormatCompletedSessionType(LocalCompletedSessionType sessionType)
    {
        return sessionType switch
        {
            LocalCompletedSessionType.Stabilization => "Stabilization",
            LocalCompletedSessionType.Maintenance => "Maintenance",
            LocalCompletedSessionType.Regression => "Regression",
            LocalCompletedSessionType.Recovery => "Recovery",
            _ => sessionType.ToString(),
        };
    }

    private static string FormatStabilizationCondition(LocalStabilizationCondition condition)
    {
        return condition switch
        {
            LocalStabilizationCondition.OrdinaryVariance => "Variance",
            LocalStabilizationCondition.AdjacentWork => "Adjacent",
            LocalStabilizationCondition.ControlledDistractor => "Distractor",
            _ => condition.ToString(),
        };
    }

    private static string FormatMaintenanceKind(MaintenanceCheckKind kind)
    {
        return kind switch
        {
            MaintenanceCheckKind.StandardOrTransfer => "Standard",
            MaintenanceCheckKind.GlobalComposite => "Composite",
            _ => kind.ToString(),
        };
    }

    private static string FormatRestorationKind(RestorationCheckKind kind)
    {
        return kind switch
        {
            RestorationCheckKind.LastOwnedStandard => "Last owned",
            RestorationCheckKind.LowerLoadTransferCheck => "Lower transfer",
            _ => kind.ToString(),
        };
    }

    private static string FormatMaintenanceCadence(MaintenanceCadence cadence)
    {
        var window = cadence.DueAfterDays == cadence.OverdueAfterDays
            ? $"{cadence.OverdueAfterDays}d"
            : $"{cadence.DueAfterDays}-{cadence.OverdueAfterDays}d";
        return $"{window} {FormatMaintenanceKind(cadence.RequiredCheckKind)}";
    }

    private static string MaintenanceCurrencyDetail(MaintenanceCurrencyResult currency)
    {
        var days = DaysSinceMaintenancePass(currency);
        if (currency.ConsecutiveFailures > 0)
        {
            return $"{currency.ConsecutiveFailures} failed check(s); {days} since pass.";
        }

        return currency.State == MaintenanceCurrencyState.Due
            ? $"{days} since pass; app/core reports due."
            : $"{days} since pass.";
    }

    private static string FormatRestorationEvidence(RestorationEvidence evidence)
    {
        if (evidence.Checks.Count == 0)
        {
            return "No restoration checks.";
        }

        return ShortText(
            string.Join("; ", evidence.Checks.Select(check =>
                $"{FormatRestorationKind(check.Kind)} {FormatStandardEvaluation(check.StandardEvaluationResult)}")),
            96);
    }

    private static string FormatStandardEvaluation(StandardEvaluationResult result)
    {
        if (result.Failures.Count == 0)
        {
            return result.Passed ? "Pass" : "Fail";
        }

        return $"{(result.Passed ? "Pass" : "Fail")}: {string.Join("; ", result.Failures.Select(failure => $"{PascalWords(failure.Kind.ToString())}: {failure.Detail}"))}";
    }

    private static string FormatLoadVariables(IEnumerable<LoadVariable> loadVariables)
    {
        return string.Join("; ", loadVariables.Select(FormatLoadVariable));
    }

    private static string ShortId(string value)
    {
        return value.Length <= 28 ? value : $"{value[..12]}...{value[^8..]}";
    }

    private static string LocalIntegrityIssueDetail(LocalDataIntegrityReadModel integrity)
    {
        if (integrity.Issues.Count == 0)
        {
            return "No integrity issues.";
        }

        return ShortText(
            string.Join("; ", integrity.Issues.Take(3).Select(issue =>
                $"{PascalWords(issue.Kind.ToString())}: {issue.Section}" +
                (issue.RecordId is null ? string.Empty : $" {issue.RecordId}"))),
            120);
    }

    private static string FormatLocalDataOperation(LocalDataBackupOperationKind kind)
    {
        return kind switch
        {
            LocalDataBackupOperationKind.ValidateCurrent => "Validate data",
            LocalDataBackupOperationKind.ValidateLatestBackup => "Validate backup",
            LocalDataBackupOperationKind.RestoreLatestBackup => "Restore",
            _ => kind.ToString(),
        };
    }

    private static string FormatLocalDataOperationStatus(LocalDataBackupOperationStatus status)
    {
        return status switch
        {
            LocalDataBackupOperationStatus.Succeeded => "Done",
            LocalDataBackupOperationStatus.Failed => "Failed",
            LocalDataBackupOperationStatus.NotFound => "Missing",
            LocalDataBackupOperationStatus.ConfirmationRequired => "Confirm",
            _ => status.ToString(),
        };
    }

    private static Color ColorForLocalDataOperation(LocalDataBackupOperationStatus status)
    {
        return status switch
        {
            LocalDataBackupOperationStatus.Succeeded => MgColors.Owned,
            LocalDataBackupOperationStatus.ConfirmationRequired => MgColors.Blocked,
            LocalDataBackupOperationStatus.NotFound => MgColors.Maintenance,
            LocalDataBackupOperationStatus.Failed => MgColors.Blocked,
            _ => MgColors.Hairline,
        };
    }

    private static string FormatByteSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:0.#} KB";
        }

        return $"{bytes / (1024d * 1024d):0.#} MB";
    }

    private static string FormatDateTime(DateTimeOffset value)
    {
        return value == DateTimeOffset.MinValue
            ? "No date"
            : $"{value.Month}/{value.Day} {value.Hour:00}:{value.Minute:00} UTC";
    }

    private static Color ColorForGlobalReviewDecision(GlobalReviewDecisionKind kind)
    {
        return kind switch
        {
            GlobalReviewDecisionKind.RestoreDecayedBranch => MgColors.Blocked,
            GlobalReviewDecisionKind.PauseTestsForDeload => MgColors.Recovery,
            GlobalReviewDecisionKind.EmphasizeBottleneckBranch => MgColors.Recovery,
            GlobalReviewDecisionKind.OpenAdvancedBranch => MgColors.TestReady,
            GlobalReviewDecisionKind.AttemptTransferIntegrationTransfer => MgColors.Transfer,
            GlobalReviewDecisionKind.ContinueCurrentProgression => MgColors.Owned,
            _ => MgColors.Hairline,
        };
    }

    private static Color ColorForGlobalReviewFailure(GlobalReviewFailureKind kind)
    {
        return kind switch
        {
            GlobalReviewFailureKind.PrerequisiteBranchDecayed => MgColors.Blocked,
            GlobalReviewFailureKind.MaintenanceCheckOverdue => MgColors.Maintenance,
            GlobalReviewFailureKind.BottleneckProgrammedResponseMissing => MgColors.Recovery,
            GlobalReviewFailureKind.CurrentTransferOrStabilizationArtifactMissing => MgColors.Transfer,
            GlobalReviewFailureKind.ParticipationOnlyAdvancement => MgColors.Blocked,
            _ => MgColors.Blocked,
        };
    }

    private static string FormatGlobalReviewDecision(GlobalReviewDecisionKind kind)
    {
        return kind switch
        {
            GlobalReviewDecisionKind.ContinueCurrentProgression => "Continue",
            GlobalReviewDecisionKind.EmphasizeBottleneckBranch => "Bottleneck",
            GlobalReviewDecisionKind.RestoreDecayedBranch => "Restore",
            GlobalReviewDecisionKind.OpenAdvancedBranch => "Open",
            GlobalReviewDecisionKind.PauseTestsForDeload => "Deload",
            GlobalReviewDecisionKind.AttemptTransferIntegrationTransfer => "TI Transfer",
            _ => PascalWords(kind.ToString()),
        };
    }

    private static string FormatGlobalReviewFailure(GlobalReviewFailureKind kind)
    {
        return kind switch
        {
            GlobalReviewFailureKind.WholePractitionerInputMissing => "Input",
            GlobalReviewFailureKind.PrerequisiteBranchDecayed => "Decay",
            GlobalReviewFailureKind.MaintenanceCheckOverdue => "Maint",
            GlobalReviewFailureKind.BottleneckProgrammedResponseMissing => "Bottleneck",
            GlobalReviewFailureKind.CurrentTransferOrStabilizationArtifactMissing => "Transfer",
            GlobalReviewFailureKind.ParticipationOnlyAdvancement => "No evidence",
            _ => PascalWords(kind.ToString()),
        };
    }

    private static string FormatBottleneck(BottleneckKind bottleneck)
    {
        return bottleneck switch
        {
            BottleneckKind.FocusHoldReturnAfterDrift => "FH return",
            BottleneckKind.WorkingMemoryEncodingFidelity => "WM encode",
            BottleneckKind.InhibitionRuleFidelity => "Rule fidelity",
            BottleneckKind.DiscriminationAuditAccuracy => "Audit",
            BottleneckKind.FocusShiftRecovery => "FS recover",
            BottleneckKind.AffectiveInterferencePressureStability => "Pressure",
            _ => PascalWords(bottleneck.ToString()),
        };
    }

    private static string MarkerForTrainingIntensity(TrainingIntensityKind intensity)
    {
        return intensity switch
        {
            TrainingIntensityKind.Low => "Low",
            TrainingIntensityKind.Moderate => "Mod",
            TrainingIntensityKind.High => "High",
            _ => intensity.ToString(),
        };
    }

    private static Color ColorForTrainingIntensity(TrainingIntensityKind intensity)
    {
        return intensity switch
        {
            TrainingIntensityKind.Low => MgColors.Hairline,
            TrainingIntensityKind.Moderate => MgColors.Training,
            TrainingIntensityKind.High => MgColors.Recovery,
            _ => MgColors.Hairline,
        };
    }

    private static BranchLevelStatus SelectDefaultStatus(CurrentTrainingStateReadModel state, BranchCode branch)
    {
        var statuses = ProgramCatalog.GlobalLevels
            .Select(level => GetStatus(state, branch, level.Id))
            .ToArray();

        foreach (var candidate in statuses.Where(status => status.State == BranchLevelState.Decayed))
        {
            return candidate;
        }

        foreach (var due in state.DueMaintenance.Where(record => record.BranchLevel.Branch == branch))
        {
            return due.BranchLevel;
        }

        foreach (var candidate in statuses.Where(status =>
                     status.State is BranchLevelState.TestReady or
                         BranchLevelState.PassedOnce or
                         BranchLevelState.Stabilizing or
                         BranchLevelState.Training))
        {
            return candidate;
        }

        var owned = statuses.LastOrDefault(status =>
            status.State is BranchLevelState.Owned or BranchLevelState.Maintenance);
        return owned.State == BranchLevelState.Unopened ? statuses[0] : owned;
    }

    private static BranchLevelStatus GetStatus(
        CurrentTrainingStateReadModel state,
        BranchCode branch,
        GlobalLevelId level)
    {
        return state.BranchLevelStates.FirstOrDefault(status =>
            status.Branch == branch && status.Level == level) is var existing &&
            existing.Branch == branch &&
            existing.Level == level
                ? existing
                : new BranchLevelStatus(branch, level, BranchLevelState.Unopened);
    }

    private static bool HasDueMaintenance(
        CurrentTrainingStateReadModel state,
        BranchCode branch,
        GlobalLevelId level)
    {
        return state.DueMaintenance.Any(record =>
            record.BranchLevel.Branch == branch && record.BranchLevel.Level == level);
    }

    private static bool HasBlocker(
        CurrentTrainingStateReadModel state,
        BranchCode branch,
        GlobalLevelId? level = null)
    {
        return state.BlockedAdvancement.Any(blocker =>
            blocker.Branch == branch &&
            (level is null || blocker.Level is null || blocker.Level == level));
    }

    private static bool HasNextWork(CurrentTrainingStateReadModel state, BranchCode branch)
    {
        return state.AvailableNextWork.Any(work => work.BranchEmphasis.Contains(branch));
    }

    private static bool HasDecayedBranch(CurrentTrainingStateReadModel state, BranchCode branch)
    {
        return state.BranchLevelStates.Any(status =>
            status.Branch == branch && status.State == BranchLevelState.Decayed);
    }

    private static IEnumerable<string> UnlockRequirements(BranchUnlockDefinition unlock)
    {
        foreach (var requirement in unlock.RequiredLevels)
        {
            yield return $"{requirement.Branch} {requirement.Level} {StateMarkerView.LabelTextFor(requirement.RequiredState)}";
        }

        foreach (var group in unlock.AnyOfLevelGroups)
        {
            var options = group.Requirements.Select(requirement =>
                $"{requirement.Branch} {requirement.Level} {StateMarkerView.LabelTextFor(requirement.RequiredState)}");
            yield return $"Any: {string.Join(" / ", options)}";
        }
    }

    private static IReadOnlyList<BranchLevelStatus> HighestOwnedStatuses(CurrentTrainingStateReadModel state)
    {
        var statuses = new List<BranchLevelStatus>();
        foreach (var branch in ProgramCatalog.Branches)
        {
            var owned = state.BranchLevelStates
                .Where(status =>
                    status.Branch == branch.Code &&
                    status.State is BranchLevelState.Owned or BranchLevelState.Maintenance)
                .OrderByDescending(status => status.Level)
                .ToArray();
            if (owned.Length > 0)
            {
                statuses.Add(owned[0]);
            }
        }

        return statuses;
    }

    private static IReadOnlyList<BranchLevelStatus> StatusesByState(
        CurrentTrainingStateReadModel state,
        BranchLevelState branchLevelState)
    {
        return state.BranchLevelStates
            .Where(status => status.State == branchLevelState)
            .OrderBy(status => status.Branch)
            .ThenBy(status => status.Level)
            .ToArray();
    }

    private static IReadOnlyList<BranchLevelStatus> ActiveDecayedStatuses(CurrentTrainingStateReadModel state)
    {
        return state.BranchLevelStates
            .Where(status => status.State == BranchLevelState.Decayed)
            .OrderBy(status => status.Branch)
            .ThenBy(status => status.Level)
            .ToArray();
    }

    private static IReadOnlyList<CurrentTrainingStateBlocker> DependencyCapBlockers(CurrentTrainingStateReadModel state)
    {
        return state.BlockedAdvancement
            .Where(blocker => blocker.Source == CurrentTrainingStateBlockerSource.DependencyCap)
            .OrderBy(blocker => blocker.Branch)
            .ThenBy(blocker => blocker.Level)
            .ToArray();
    }

    private static string StabilizationProgressLabel(
        CurrentTrainingStateReadModel state,
        BranchLevelStatus status)
    {
        var cleanPasses = state.ProgressRecords.StabilizationPasses.Count(pass =>
            pass.Evidence.Branch == status.Branch &&
            pass.Evidence.Level == status.Level &&
            pass.Evidence.IsCleanPass);
        if (status.State is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing)
        {
            cleanPasses = Math.Max(1, cleanPasses);
        }

        return $"{Math.Min(cleanPasses, 3)}/3";
    }

    private static int MaintenancePriority(MaintenanceCurrencyState state)
    {
        return state switch
        {
            MaintenanceCurrencyState.Failed => 0,
            MaintenanceCurrencyState.Warning => 1,
            MaintenanceCurrencyState.Due => 2,
            _ => 3,
        };
    }

    private static string FormatPassState(FormalTestPassState passState)
    {
        return passState switch
        {
            FormalTestPassState.Fail => "Fail",
            FormalTestPassState.PassOnce => "1/3",
            FormalTestPassState.StabilizationPass => "Stab",
            FormalTestPassState.Owned => "Owned",
            FormalTestPassState.MaintenancePass => "Maint",
            _ => passState.ToString(),
        };
    }

    private static Color ColorForPassState(FormalTestPassState passState)
    {
        return passState switch
        {
            FormalTestPassState.Fail => MgColors.Blocked,
            FormalTestPassState.PassOnce => MgColors.PassedOnce,
            FormalTestPassState.StabilizationPass => MgColors.TestReady,
            FormalTestPassState.Owned => MgColors.Owned,
            FormalTestPassState.MaintenancePass => MgColors.Maintenance,
            _ => MgColors.Hairline,
        };
    }

    private static bool IsTransferSession(WeeklySessionKind session)
    {
        return session is WeeklySessionKind.Transfer or WeeklySessionKind.TransferOrStabilization;
    }

    private static bool IsMaintenanceOrRestorationWork(
        CurrentTrainingStateNextWork work,
        IReadOnlyCollection<BranchCode> dueBranches,
        IReadOnlyCollection<BranchCode> decayedBranches)
    {
        if (work.Session is WeeklySessionKind.Maintenance or WeeklySessionKind.RecoveryOrLightMaintenance)
        {
            return true;
        }

        if (work.Session == WeeklySessionKind.Recovery &&
            work.BranchEmphasis.Any(branch => dueBranches.Contains(branch) || decayedBranches.Contains(branch)))
        {
            return true;
        }

        return decayedBranches.Count > 0 &&
            !work.IsAdvancementWork &&
            work.BranchEmphasis.Any(decayedBranches.Contains);
    }

    private static Color ColorForProgressEmphasis(LocalProgressEmphasisKind kind)
    {
        return kind switch
        {
            LocalProgressEmphasisKind.RestoreDecayedBranch => MgColors.Blocked,
            LocalProgressEmphasisKind.ResolveMaintenanceBlocker => MgColors.Maintenance,
            LocalProgressEmphasisKind.EmphasizeBottleneckBranch => MgColors.Recovery,
            LocalProgressEmphasisKind.ContinueActiveTraining => MgColors.Training,
            LocalProgressEmphasisKind.ContinueMaintenance => MgColors.Maintenance,
            _ => MgColors.Hairline,
        };
    }

    private static IReadOnlyList<string> ChipsForProgrammedEmphasis(LocalProgrammedEmphasis emphasis)
    {
        var chips = new List<string>
        {
            FormatProgressEmphasis(emphasis.Kind),
        };
        var branchLevel = FormatBranchLevel(emphasis.Branch, emphasis.Level);
        if (branchLevel is not null)
        {
            chips.Add(branchLevel);
        }

        return LimitChips(chips, chips.Count);
    }

    private static string FormatProgressEmphasis(LocalProgressEmphasisKind kind)
    {
        return kind switch
        {
            LocalProgressEmphasisKind.RestoreDecayedBranch => "Restore",
            LocalProgressEmphasisKind.ResolveMaintenanceBlocker => "Maintenance",
            LocalProgressEmphasisKind.EmphasizeBottleneckBranch => "Bottleneck",
            LocalProgressEmphasisKind.ContinueActiveTraining => "Train",
            LocalProgressEmphasisKind.ContinueMaintenance => "Maintain",
            _ => kind.ToString(),
        };
    }

    private static IReadOnlyList<string> ChipsForBlocked(IReadOnlyList<CurrentTrainingStateBlocker> blocked)
    {
        var first = blocked[0];
        var chips = new List<string>
        {
            blocked.Count == 1 ? "1 block" : $"{blocked.Count} blocks",
            FormatBlockerSource(first.Source),
        };

        var branchLevel = FormatBranchLevel(first.Branch, first.Level);
        if (branchLevel is not null)
        {
            chips.Add(branchLevel);
        }

        return LimitChips(chips, chips.Count);
    }

    private static IReadOnlyList<string> ChipsForMaintenance(CurrentTrainingStateReadModel state)
    {
        return LimitChips(
            state.DueMaintenance.Select(record => FormatBranchLevel(record.BranchLevel)),
            state.DueMaintenance.Count);
    }

    private static IReadOnlyList<string> ChipsForRecovery(IReadOnlyList<CurrentTrainingStateNextWork> recoveryWork)
    {
        if (recoveryWork.Count == 0)
        {
            return ["Advancement off"];
        }

        return LimitChips(
            recoveryWork
                .SelectMany(work => work.BranchEmphasis.Count == 0
                    ? [FormatSession(work.Session)]
                    : work.BranchEmphasis.Select(branch => $"{branch}")),
            recoveryWork.Sum(work => Math.Max(1, work.BranchEmphasis.Count)));
    }

    private static IReadOnlyList<string> ChipsForNextWork(CurrentTrainingStateNextWork work)
    {
        var chips = new List<string>
        {
            $"D{work.DayNumber}",
            FormatSession(work.Session),
            work.IsAdvancementWork ? "Gate" : "No gate",
        };
        chips.AddRange(work.BranchEmphasis.Count == 0
            ? ["No branch"]
            : work.BranchEmphasis.Select(branch => branch.ToString()));

        return LimitChips(chips, chips.Count);
    }

    private static string DrillCodeFor(DrillId drill)
    {
        return ProgramCatalog.Drills.Single(definition => definition.Id == drill).Code;
    }

    private static string DrillLabelFor(DrillId drill)
    {
        var definition = ProgramCatalog.Drills.Single(item => item.Id == drill);
        return $"{definition.Code} {definition.Name}";
    }

    private static string FormatLoadVariable(LoadVariable variable)
    {
        return $"{variable.Name}: {variable.Value}";
    }

    private static string ShortText(string value, int maxLength)
    {
        var normalized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..Math.Max(0, maxLength - 3)] + "...";
    }

    private static IReadOnlyList<string> LimitChips(IEnumerable<string> chips, int totalCount)
    {
        var limited = chips
            .Where(chip => !string.IsNullOrWhiteSpace(chip))
            .Distinct()
            .Take(3)
            .ToList();
        if (totalCount > limited.Count)
        {
            limited.Add($"+{totalCount - limited.Count}");
        }

        return limited;
    }

    private static bool IsRecoverySession(WeeklySessionKind session)
    {
        return session is
            WeeklySessionKind.RecoveryOrLightMaintenance or
            WeeklySessionKind.OffOrRecovery or
            WeeklySessionKind.Recovery or
            WeeklySessionKind.RecoveryOrRetest;
    }

    private static Color ColorForSession(WeeklySessionKind session)
    {
        return session switch
        {
            WeeklySessionKind.Maintenance => MgColors.Maintenance,
            WeeklySessionKind.TestOrStabilization or WeeklySessionKind.Stabilization => MgColors.TestReady,
            WeeklySessionKind.TransferOrStabilization or WeeklySessionKind.Transfer => MgColors.Transfer,
            WeeklySessionKind.RecoveryOrLightMaintenance or
                WeeklySessionKind.OffOrRecovery or
                WeeklySessionKind.Recovery or
                WeeklySessionKind.RecoveryOrRetest => MgColors.Recovery,
            WeeklySessionKind.Off => MgColors.Hairline,
            _ => MgColors.Training,
        };
    }

    private static Color ColorForState(BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.Training => MgColors.Training,
            BranchLevelState.TestReady => MgColors.TestReady,
            BranchLevelState.PassedOnce => MgColors.PassedOnce,
            BranchLevelState.Stabilizing => MgColors.TestReady,
            BranchLevelState.Owned => MgColors.Owned,
            BranchLevelState.Maintenance => MgColors.Maintenance,
            BranchLevelState.Decayed => MgColors.Blocked,
            _ => MgColors.Hairline,
        };
    }

    private static string FormatSession(WeeklySessionKind session)
    {
        return session switch
        {
            WeeklySessionKind.RecoveryOrLightMaintenance => "Recovery",
            WeeklySessionKind.TestOrStabilization => "Test",
            WeeklySessionKind.OffOrRecovery => "Recovery",
            WeeklySessionKind.TransferOrStabilization => "Transfer",
            WeeklySessionKind.Maintenance => "Maint",
            WeeklySessionKind.Stabilization => "Stab",
            WeeklySessionKind.RecoveryOrRetest => "Retest",
            _ => session.ToString(),
        };
    }

    private static string FormatBlockerSource(CurrentTrainingStateBlockerSource source)
    {
        return source switch
        {
            CurrentTrainingStateBlockerSource.WeeklyProgramming => "Week",
            CurrentTrainingStateBlockerSource.DependencyCap => "Cap",
            CurrentTrainingStateBlockerSource.GlobalBalance => "Balance",
            _ => source.ToString(),
        };
    }

    private static string FormatBranchLevel(BranchLevelStatus status)
    {
        return $"{status.Branch} {status.Level}";
    }

    private static string? FormatBranchLevel(BranchCode? branch, GlobalLevelId? level)
    {
        if (branch is null)
        {
            return level is null ? null : level.ToString();
        }

        return level is null
            ? branch.Value.ToString()
            : $"{branch.Value} {level.Value}";
    }

    private static string FormatDate(TrainingDate date)
    {
        return $"{date.Month}/{date.Day}";
    }

    private void AddPanel(string title, string body, string detail)
    {
        var panel = new MgPanel(context);

        var titleView = new TextView(context)
        {
            Text = title,
        };
        MgTypography.ApplyHeading(titleView);

        var bodyView = new TextView(context)
        {
            Text = body,
        };
        MgTypography.ApplyBody(bodyView);

        var detailView = new TextView(context)
        {
            Text = detail,
        };
        MgTypography.ApplyLabel(detailView);

        panel.AddView(titleView);
        panel.AddView(bodyView);
        panel.AddView(detailView);
        AddSection(panel);
    }

    private void AddSection(View view)
    {
        var layout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(0, 0, 0, MgSpacing.Dp(context, MgSpacing.Md));
        content.AddView(view, layout);
    }
}
