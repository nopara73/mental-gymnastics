using Android.Content;
using Android.Graphics;
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
    private readonly TextView subtitle;
    private readonly Button dataButton;
    private readonly ScrollView scrollView;
    private readonly LinearLayout content;
    private readonly LinearLayout navigation;

    private AndroidTrainingStateSnapshot? snapshot;
    private AndroidSessionStartSnapshot? sessionStartSnapshot;
    private AndroidLiveSessionSnapshot? liveSessionSnapshot;
    private AndroidLiveSessionCompletionSnapshot? liveCompletionSnapshot;
    private AndroidActiveSessionResumeSnapshot? activeSessionResumeSnapshot;
    private LocalDataBackupOperationResult? localDataOperation;
    private string liveSessionInput = string.Empty;
    private bool restoreConfirmationArmed;
    private Screen currentScreen = Screen.Today;

    public MgNavigationShell(Context context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));

        Root = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        Root.SetBackgroundColor(MgColors.Canvas);
        Root.SetPadding(0, StatusBarHeightPx(), 0, 0);

        subtitle = new TextView(context)
        {
            Text = "Today",
        };
        dataButton = HeaderButton("Data");
        dataButton.Click += (_, _) =>
        {
            currentScreen = Screen.LocalData;
            restoreConfirmationArmed = false;
            RenderCurrentScreen();
        };

        Root.AddView(BuildHeader(), MatchWrap());

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
        navigation.SetPadding(Dp(MgSpacing.Sm), Dp(MgSpacing.Xs), Dp(MgSpacing.Sm), Dp(MgSpacing.Xs));
        Root.AddView(navigation, MatchWrap());
        RenderNavigation();
    }

    private enum Screen
    {
        Today,
        Map,
        Evidence,
        Review,
        LocalData,
        Preflight,
        Live,
        Result,
    }

    private readonly record struct TodayFact(string Text, Color Color, bool Filled);

    private readonly record struct VisualSignal(string Marker, string Label, string Value, Color Color, bool Filled);

    private readonly record struct InspectionSignal(string Marker, string Label, string Detail, Color Color, bool Filled);

    private readonly record struct BranchReviewSignal(BranchCode Branch, string Level, string State, Color Color, bool Filled);

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

        currentScreen = Screen.Today;
        RenderFrame();
        AddPanel("Unable to load", exception.Message, "Progress unchanged.");
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
        AddPanel("Preparing", "Selecting prescribed work and preparing the session.", "Progress unchanged.");
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
        currentScreen = Screen.Preflight;
        RenderCurrentScreen();
    }

    public void ShowSessionStartError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        currentScreen = Screen.Preflight;
        sessionStartSnapshot = null;
        liveSessionSnapshot = null;
        liveCompletionSnapshot = null;
        activeSessionResumeSnapshot = null;
        RenderFrame();
        AddPanel("Preparation blocked", exception.Message, "Progress unchanged.");
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
        AddPanel("Starting", "Opening the prepared session.", "Progress unchanged.");
        ResetScrollPosition();
    }

    public void RenderLiveSession(AndroidLiveSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        liveSessionSnapshot = snapshot;
        liveCompletionSnapshot = null;
        currentScreen = Screen.Live;
        RenderCurrentScreen(resetScroll: false);
    }

    public void ShowLiveSessionCompletionLoading(AndroidLiveSessionSnapshot terminalSnapshot)
    {
        ArgumentNullException.ThrowIfNull(terminalSnapshot);

        liveSessionSnapshot = terminalSnapshot;
        liveCompletionSnapshot = null;
        currentScreen = Screen.Result;
        RenderFrame();
        AddPanel("Recording result", "Saving the session outcome.", "Progress changes only when the program state changes.");
        ResetScrollPosition();
    }

    public void RenderLiveSessionCompletion(AndroidLiveSessionCompletionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        liveCompletionSnapshot = snapshot;
        liveSessionSnapshot = null;
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
                snapshot.LocalData);
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

        currentScreen = Screen.LocalData;
        RenderFrame();
        AddPanel("Local data failed", exception.Message, "Progress unchanged.");
        ResetScrollPosition();
    }

    public void ShowLiveSessionError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        currentScreen = Screen.Live;
        RenderFrame();
        AddPanel("Session command failed", exception.Message, "Progress unchanged.");
        ResetScrollPosition();
    }

    private View BuildHeader()
    {
        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);
        header.SetBackgroundColor(MgColors.Surface);
        header.SetPadding(Dp(MgSpacing.Lg), Dp(MgSpacing.Md), Dp(MgSpacing.Lg), Dp(MgSpacing.Sm));

        var titleStack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };

        var title = new TextView(context)
        {
            Text = "Mental Gymnastics",
        };
        MgTypography.ApplyHeading(title);
        MgTypography.ApplyLabel(subtitle);

        titleStack.AddView(title);
        titleStack.AddView(subtitle);
        header.AddView(titleStack, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        var dataLayout = new LinearLayout.LayoutParams(Dp(52), Dp(30));
        dataLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        header.AddView(dataButton, dataLayout);

        return header;
    }

    private void RenderCurrentScreen(bool resetScroll = true)
    {
        RenderFrame();

        switch (currentScreen)
        {
            case Screen.Today:
                if (snapshot is null)
                {
                    AddPanel("Loading", "Training state is not available yet.", "Progress unchanged.");
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
            case Screen.Review:
                AddReview(snapshot);
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
        subtitle.Text = SubtitleFor(currentScreen);
        var focused = currentScreen is Screen.Preflight or Screen.Live or Screen.Result;
        var horizontalPadding = focused ? MgSpacing.Md : MgSpacing.Lg;
        var verticalPadding = focused ? MgSpacing.Sm : MgSpacing.Md;
        content.SetPadding(
            Dp(horizontalPadding),
            Dp(verticalPadding),
            Dp(horizontalPadding),
            Dp(focused ? MgSpacing.Md : MgSpacing.Lg));
        dataButton.Visibility = focused ? ViewStates.Gone : ViewStates.Visible;
        navigation.Visibility = focused ? ViewStates.Gone : ViewStates.Visible;
        RenderNavigation();
    }

    private void RenderNavigation()
    {
        navigation.RemoveAllViews();
        AddNavigationButton(Screen.Today, "Today");
        AddNavigationButton(Screen.Map, "Map");
        AddNavigationButton(Screen.Evidence, "Log");
        AddNavigationButton(Screen.Review, "Review");
    }

    private void AddNavigationButton(Screen screen, string label)
    {
        var selected = currentScreen == screen;
        var button = new Button(context)
        {
            Text = label,
            Enabled = !selected,
        };
        button.SetAllCaps(false);
        button.SetSingleLine(true);
        button.SetTextColor(selected ? Color.White : MgColors.Ink);
        button.Background = selected
            ? MgTheme.Filled(context, MgColors.Ink, cornerRadius: 8)
            : MgTheme.MutedSurface(context, cornerRadius: 8);
        button.Click += (_, _) =>
        {
            currentScreen = screen;
            restoreConfirmationArmed = false;
            RenderCurrentScreen();
        };

        button.SetMinHeight(0);
        button.SetMinimumHeight(0);
        button.SetMinWidth(0);
        button.SetMinimumWidth(0);
        MgTypography.ApplyLabel(button);
        button.SetTextColor(selected ? Color.White : MgColors.Ink);

        var layout = new LinearLayout.LayoutParams(0, Dp(38), 1);
        layout.SetMargins(Dp(3), 0, Dp(3), 0);
        navigation.AddView(button, layout);
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
        var panel = Panel();
        AddTodayQuestion(panel);
        AddTodayCommandObject(panel, presentation);

        AddTodayPrimaryButton(panel, presentation);

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddActiveSessionProblem(PreUiActiveSessionResumeState resume)
    {
        var panel = Panel();
        AddTodayQuestion(panel);
        AddMarkerHeader(panel, "Resume blocked", "!", MgColors.Blocked);
        AddBody(panel, ResumeStatusLabel(resume.Status));
        AddMuted(panel, resume.Detail);
        AddPrimaryButton(panel, "Clear", enabled: true, () => ActiveSessionInvalidateRequested?.Invoke(resume.SessionId));
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
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Lg), 0, 0);

        var markerView = new TextView(context)
        {
            Text = MarkerFor(presentation.Priority),
            Gravity = GravityFlags.Center,
            Background = MgTheme.Filled(context, color, cornerRadius: 8),
            ContentDescription = TitleFor(presentation),
        };
        MgTypography.ApplyTitle(markerView);
        markerView.SetTextColor(Color.White);
        row.AddView(markerView, new LinearLayout.LayoutParams(Dp(64), Dp(88)));

        var body = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var bodyLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        bodyLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        row.AddView(body, bodyLayout);

        AddTodayHeading(body, TodayCommandTitle(presentation));
        if (presentation.PrimaryPrescribedWork is { } work &&
            !string.IsNullOrWhiteSpace(work.DrillLabel))
        {
            AddTinyLine(body, work.DrillLabel!);
        }

        AddTodayBranchNodes(body, TodayBranchLabels(presentation), color);
        AddTodayFactRow(body, TodayFacts(presentation));
        panel.AddView(row, MatchWrap());

        if (presentation.MaintenanceDecayPriority is { } maintenance &&
            presentation.Priority is TrainingPresentationPriorityKind.MaintenanceDue
                or TrainingPresentationPriorityKind.DecayRestoration)
        {
            AddPriorityLine(panel, PriorityLabel(maintenance.Kind), maintenance.Detail, color);
        }

        if (presentation.UrgentBlocker is { } blocker &&
            presentation.Priority == TrainingPresentationPriorityKind.UrgentBlocker)
        {
            AddPriorityLine(panel, "Blocked", blocker.Detail, MgColors.Blocked);
        }
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
            ActionLabel(presentation.PrimaryAction),
            presentation.PrimaryActionEnabled,
            () => SessionStartRequested?.Invoke());
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
                Text = $"+{labels.Count - 4}",
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

        var panel = Panel();
        AddMarkerHeader(panel, "Branch map", "M", MgColors.Training);
        AddBranchLadder(panel, state.CurrentState, state.Presentation);
        AddProgressSignalStrip(panel, state.CurrentState, includeZeros: false);
        AddTransferInspection(panel, state.CurrentState);
        content.AddView(panel, MatchWrapWithBottom());

        AddMaintenanceDecayInspection(state.CurrentState, state.Presentation.MaintenanceDecayPriority);
        AddBlockedAdvancementInspection(state.CurrentState, state.Presentation.UrgentBlocker);
    }

    private void AddEvidence(AndroidTrainingStateSnapshot? state)
    {
        if (state is null)
        {
            AddPanel("Evidence unavailable", "Training state is not loaded.", "Return to Today.");
            return;
        }

        var summary = state.Presentation.EvidenceSummary;
        var panel = Panel();
        AddMarkerHeader(
            panel,
            "Evidence",
            summary.HasFailureEvidence ? "!" : "E",
            summary.HasFailureEvidence ? MgColors.Blocked : MgColors.Training);
        AddEvidenceLedgerSummary(panel, summary);
        if (summary.HasFailureEvidence)
        {
            AddInspectionRow(panel, "F", "Failure evidence", "Recorded and not softened.", MgColors.Blocked, filled: true);
        }

        foreach (var artifact in state.CurrentState.EvidenceSummaries.Take(6))
        {
            AddEvidenceItem(panel, artifact);
        }

        if (state.CurrentState.EvidenceSummaries.Count == 0)
        {
            AddMuted(panel, "No local evidence recorded yet.");
        }

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddReview(AndroidTrainingStateSnapshot? state)
    {
        if (state is null)
        {
            AddPanel("Review unavailable", "Training state is not loaded.", "Return to Today.");
            return;
        }

        var review = state.CurrentState.GlobalReview;
        var panel = Panel();
        var decision = SelectReviewDecision(review);
        AddMarkerHeader(
            panel,
            "Global review",
            review.Evaluation.Passed ? "O" : "!",
            review.Evaluation.Passed ? MgColors.Owned : MgColors.Blocked);

        AddInspectionRow(
            panel,
            ReviewDecisionMarker(decision?.Kind),
            ReviewDecisionLabel(decision),
            ReviewDecisionDetail(review),
            ColorForReviewDecision(review, decision),
            filled: review.Evaluation.Passed);

        if (review.Evaluation.Failures.Count > 0)
        {
            AddInspectionRow(
                panel,
                "B",
                "Blocked input",
                ReviewFailureSummaryLabel(review.Evaluation.Failures),
                MgColors.Blocked,
                filled: true);
        }

        AddReviewBranchStateBoard(panel, state.CurrentState);
        AddReviewProgrammedResponse(panel, state.CurrentState);
        AddMaintenanceDecaySummary(panel, state.CurrentState);
        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddLocalData(AndroidTrainingStateSnapshot? state)
    {
        if (state is null)
        {
            AddPanel("Local data unavailable", "Training state is not loaded.", "Return to Today.");
            return;
        }

        var local = state.LocalData;
        var panel = Panel();
        AddSectionTitle(panel, "Local data");
        AddBody(panel, local.CurrentIntegrity.IsValid ? "Current local data is valid." : "Current local data has integrity issues.");
        if (local.LatestBackup is { } backup)
        {
            AddMuted(panel, $"Latest backup: {backup.FileName}");
        }
        else
        {
            AddMuted(panel, "No local backup file found.");
        }

        if (localDataOperation is { } operation)
        {
            AddWarningRow(
                panel,
                OperationStatusLabel(operation.Status),
                operation.Detail,
                operation.Status == LocalDataBackupOperationStatus.Succeeded ? MgColors.Owned : MgColors.Blocked);
        }

        var utilityRow = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        AddUtilityButton(utilityRow, "Export", () => LocalBackupExportRequested?.Invoke(), inline: true);
        AddUtilityButton(utilityRow, "Validate data", () => LocalDataValidateRequested?.Invoke(), inline: true);
        AddUtilityButton(utilityRow, "Validate backup", () => LocalBackupValidateRequested?.Invoke(), inline: true);
        panel.AddView(utilityRow, MatchWrapWithTop(MgSpacing.Md));
        AddUtilityButton(panel, restoreConfirmationArmed ? "Confirm restore" : "Restore latest", () =>
        {
            if (!restoreConfirmationArmed)
            {
                restoreConfirmationArmed = true;
                RenderCurrentScreen();
                return;
            }

            LocalBackupRestoreRequested?.Invoke();
        }, destructive: true);

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddPreflight(AndroidSessionStartSnapshot? start)
    {
        if (start is null)
        {
            AddPanel("Preparing", "Session preflight is not ready yet.", "Progress unchanged.");
            return;
        }

        var preflight = start.Presentation;
        var panel = Panel();
        AddMarkerHeader(
            panel,
            preflight.CanStart ? "Preflight" : "Start blocked",
            preflight.CanStart ? ">" : "!",
            preflight.CanStart ? MgColors.Training : MgColors.Blocked);

        if (preflight.Work is { } work)
        {
            AddPreflightWorkHeader(panel, work);
            AddPreflightRequirement(panel, "Load", LoadSummary(work.LoadVariables));
        }

        AddPreflightRequirement(panel, "Standard", preflight.Standard ?? "Not available.");
        AddPreflightRequirement(panel, "Constraint", preflight.HonestyConstraint ?? "Not available.");
        AddPreflightRequirement(panel, "Evidence", RequiredEvidenceLabel(preflight));

        foreach (var blocker in preflight.Blockers.Take(3))
        {
            AddWarningRow(panel, "Blocked", blocker.Detail, MgColors.Blocked);
        }

        AddPrimaryButton(panel, preflight.CanStart ? "Start" : "Blocked", preflight.CanStart, () => LiveSessionStartRequested?.Invoke());

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddPreflightWorkHeader(
        LinearLayout panel,
        TrainingPresentationWorkSummary work)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Md), 0, 0);

        var marker = new TextView(context)
        {
            Text = RoleMarker(work),
            Gravity = GravityFlags.Center,
            Background = MgTheme.Outline(context, MgColors.Training, cornerRadius: 8),
        };
        marker.SetTextColor(MgColors.Ink);
        MgTypography.ApplyHeading(marker);
        row.AddView(marker, new LinearLayout.LayoutParams(Dp(52), Dp(52)));

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

        var branch = new TextView(context)
        {
            Text = string.Join(" / ", work.BranchLevels.Select(BranchNodeLabel)),
        };
        MgTypography.ApplyLabel(branch);
        stack.AddView(branch, MatchWrapWithTop(MgSpacing.Xs));

        panel.AddView(row, MatchWrap());
    }

    private void AddPreflightRequirement(
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
        MgTypography.ApplyMicro(labelView);
        row.AddView(labelView, MatchWrap());

        var valueView = new TextView(context)
        {
            Text = value,
        };
        MgTypography.ApplyBody(valueView);
        row.AddView(valueView, MatchWrapWithTop(MgSpacing.Xs));

        panel.AddView(row, MatchWrapWithTop(MgSpacing.Sm));
    }

    private void AddLive(AndroidLiveSessionSnapshot? liveSnapshot)
    {
        if (liveSnapshot is null)
        {
            AddPanel("Live session", "Session state is not available yet.", "Progress unchanged.");
            return;
        }

        var live = liveSnapshot.LiveSession;
        var presentation = liveSnapshot.Presentation;
        var panel = Panel();
        AddLivePhaseHeader(panel, presentation);
        AddLiveMaterial(panel, presentation);
        AddLiveInputIfNeeded(panel, presentation);
        AddLivePrimaryCommand(panel, live, presentation);
        AddLiveFixedCommandRows(panel, live, presentation);
        AddLiveEvidence(panel, presentation.Evidence);

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddLivePhaseHeader(
        LinearLayout panel,
        LiveSessionPresentationReadModel presentation)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);

        var timer = new TimerRingView(context);
        timer.Update(presentation.Timer, presentation.LifecycleStatus);
        row.AddView(timer, new LinearLayout.LayoutParams(Dp(112), Dp(112)));

        var stack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var stackLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        stackLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        row.AddView(stack, stackLayout);

        var phase = new TextView(context)
        {
            Text = PhaseLabel(presentation.CurrentPhaseKind),
        };
        MgTypography.ApplyTitle(phase);
        stack.AddView(phase, MatchWrap());

        var work = new TextView(context)
        {
            Text = ShortWorkLabel(presentation.Work),
        };
        MgTypography.ApplyLabel(work);
        stack.AddView(work, MatchWrapWithTop(MgSpacing.Xs));

        var status = new TextView(context)
        {
            Text = LifecycleLabel(presentation.LifecycleStatus),
        };
        MgTypography.ApplyMicro(status);
        status.SetTextColor(ColorForLifecycle(presentation.LifecycleStatus));
        stack.AddView(status, MatchWrapWithTop(MgSpacing.Xs));

        panel.AddView(row, MatchWrap());
    }

    private void AddLiveMaterial(
        LinearLayout panel,
        LiveSessionPresentationReadModel presentation)
    {
        var box = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.MutedSurface(context, cornerRadius: 8),
        };
        box.SetPadding(Dp(MgSpacing.Lg), Dp(MgSpacing.Md), Dp(MgSpacing.Lg), Dp(MgSpacing.Md));

        var label = new TextView(context)
        {
            Text = LiveMaterialLabel(presentation),
        };
        MgTypography.ApplyMicro(label);
        box.AddView(label, MatchWrap());

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

    private void AddResult(AndroidLiveSessionCompletionSnapshot? completion)
    {
        if (completion is null)
        {
            AddPanel("Result", "Result is being recorded.", "Progress changes only when the program state changes.");
            return;
        }

        var result = completion.Presentation;
        var next = snapshot?.Presentation;
        var panel = Panel();

        AddMarkerHeader(panel, ResultTitle(result), ResultMarker(result.Outcome), ColorForResult(result.Outcome));
        AddWorkIdentity(panel, result.Work);
        AddBody(panel, OutcomeText(result));
        AddInspectionRow(panel, "E", "Evidence", ResultEvidenceText(result), ColorForResult(result.Outcome));

        if (result.BlockingFailureDetails.FirstOrDefault() is { } failure)
        {
            AddWarningRow(panel, "Failed constraint", failure, MgColors.Blocked);
        }

        AddInspectionRow(panel, "C", "Change", ResultChangeText(result), ColorForResult(result.Outcome));
        AddInspectionRow(panel, ">", "Next", ResultNextActionText(next), MgColors.Training);

        AddPrimaryButton(panel, "Next action", enabled: snapshot is not null, () =>
        {
            currentScreen = Screen.Today;
            RenderCurrentScreen();
        });

        content.AddView(panel, MatchWrapWithBottom());
    }

    private void AddLiveInputIfNeeded(
        LinearLayout panel,
        LiveSessionPresentationReadModel presentation)
    {
        var needsInput = presentation.AvailableCommands.Any(command =>
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
            Hint = "Response",
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
            AddPrimaryButton(panel, LifecycleLabel(live.LifecycleStatus), enabled: false, () => { });
            return;
        }

        var command = presentation.PrimaryCommand.Command;
        AddPrimaryButton(panel, CommandLabel(command, primary: true), enabled: true, () =>
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
        var showRow = commands.Any(command => IsCommandAvailable(presentation, command) && command != primary);
        if (!showRow)
        {
            return;
        }

        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Xs), 0, 0);

        foreach (var command in commands)
        {
            var enabled = IsCommandAvailable(presentation, command) && command != primary;
            var button = LiveCommandButton(CommandLabel(command, primary: false), enabled, command == RuntimeInputCommandKind.Abandon);
            button.Click += (_, _) =>
            {
                if (enabled)
                {
                    LiveSessionCommandRequested?.Invoke(command, TargetFor(live, command), ValueFor(command));
                }
            };
            row.AddView(button, new LinearLayout.LayoutParams(0, Dp(38), 1));
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
                var button = SecondaryButton(CommandLabel(command, primary: false));
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
        LiveEvidencePresentationSummary evidence)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Sm), 0, 0);
        row.AddView(MetricBox("Drift", evidence.DriftCount.ToString()), new LinearLayout.LayoutParams(0, Dp(44), 1));
        row.AddView(MetricBox("Guess", evidence.GuessCount.ToString()), new LinearLayout.LayoutParams(0, Dp(44), 1));
        row.AddView(MetricBox("Error", evidence.ErrorCount.ToString()), new LinearLayout.LayoutParams(0, Dp(44), 1));
        row.AddView(MetricBox("Correct", evidence.CorrectionCount.ToString()), new LinearLayout.LayoutParams(0, Dp(44), 1));
        row.AddView(
            MetricBox(
                "Evidence",
                $"{Math.Min(evidence.EvidenceFactCount, evidence.ExpectedEvidenceFactCount)}/{evidence.ExpectedEvidenceFactCount}"),
            new LinearLayout.LayoutParams(0, Dp(44), 1));
        panel.AddView(row, MatchWrap());
    }

    private void AddWorkIdentity(LinearLayout panel, TrainingPresentationWorkSummary work)
    {
        var role = WorkRoleLabel(work);
        var branch = string.Join(
            " / ",
            work.BranchLevels.Select(BranchNodeLabel));
        var identityParts = new List<string> { role };
        if (!string.IsNullOrWhiteSpace(branch))
        {
            identityParts.Add(branch);
        }

        if (!string.IsNullOrWhiteSpace(work.DrillLabel))
        {
            identityParts.Add(work.DrillLabel);
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
            new("E", "Evidence", summary.EvidenceArtifactCount.ToString(), MgColors.Training, Filled: false),
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
                ? $"{summary.RecentCleanSessionCount} clean of {summary.RecentSessionCount}; failure evidence remains visible."
                : $"{summary.RecentCleanSessionCount} clean of {summary.RecentSessionCount}; no failure marker in recent evidence.";
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
            AddInspectionRow(panel, "E", "Evidence trace", "No local evidence recorded yet.", MgColors.Hairline);
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
        box.SetTextColor(signal.Filled ? Color.White : MgColors.Ink);
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
        cell.SetTextColor(signal.Filled ? Color.White : MgColors.Ink);
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
        return panel;
    }

    private void AddMarkerHeader(LinearLayout panel, string title, string marker, Color color)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);

        var markerView = new TextView(context)
        {
            Text = marker,
            Gravity = GravityFlags.Center,
            Background = MgTheme.Filled(context, color, cornerRadius: 8),
        };
        MgTypography.ApplyHeading(markerView);
        markerView.SetTextColor(Color.White);
        row.AddView(markerView, new LinearLayout.LayoutParams(Dp(48), Dp(48)));

        var titleView = new TextView(context)
        {
            Text = title,
        };
        MgTypography.ApplyHeading(titleView);
        var titleLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        titleLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);
        row.AddView(titleView, titleLayout);

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
            Background = MgTheme.Outline(context, color, cornerRadius: 8),
        };
        row.SetPadding(Dp(MgSpacing.Md), Dp(MgSpacing.Sm), Dp(MgSpacing.Md), Dp(MgSpacing.Sm));
        AddSectionTitle(row, title);
        AddMuted(row, detail);
        panel.AddView(row, MatchWrapWithTop());
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
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(MgSpacing.Md), 0, 0);

        row.AddView(MarkerPill(marker, color, filled), new LinearLayout.LayoutParams(Dp(44), Dp(44)));

        var textStack = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        var textLayout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        textLayout.SetMargins(Dp(MgSpacing.Md), 0, 0, 0);

        var labelView = new TextView(context)
        {
            Text = label,
        };
        MgTypography.ApplyBody(labelView);
        labelView.SetTextColor(MgColors.Ink);
        textStack.AddView(labelView, MatchWrap());

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
        panel.AddView(row, MatchWrap());
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

    private void AddFocusedBlock(LinearLayout panel, string title, string text)
    {
        var block = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.MutedSurface(context, cornerRadius: 8),
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
            Background = MgTheme.MutedSurface(context, cornerRadius: 8),
        };
        MgTypography.ApplyMicro(box);
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
        button.SetTextColor(MgColors.Ink);
        button.Background = MgTheme.MutedSurface(context, cornerRadius: 8);
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

    private static string SubtitleFor(Screen screen)
    {
        return screen switch
        {
            Screen.Today => "Today",
            Screen.Map => "Map",
            Screen.Evidence => "Evidence",
            Screen.Review => "Review",
            Screen.LocalData => "Local data",
            Screen.Preflight => "Preflight",
            Screen.Live => "Live session",
            Screen.Result => "Result",
            _ => "Today",
        };
    }

    private static string TodayCommandTitle(CurrentTrainingPresentationReadModel presentation)
    {
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
            _ => presentation.PrimaryPrescribedWork is { } work ? WorkTitle(work) : "Prescribed work",
        };
    }

    private static string WorkTitle(TrainingPresentationWorkSummary work)
    {
        var role = WorkRoleLabel(work);
        return work.DrillLabel is null
            ? role
            : $"{role} · {work.DrillLabel}";
    }

    private static string ShortWorkLabel(TrainingPresentationWorkSummary work)
    {
        var branch = BranchSummary(work.BranchLevels);
        return string.IsNullOrWhiteSpace(branch)
            ? WorkRoleLabel(work)
            : $"{WorkRoleLabel(work)} · {branch}";
    }

    private static string RoleMarker(TrainingPresentationWorkSummary work)
    {
        return WorkRoleLabel(work) switch
        {
            "Practice" => "P",
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

    private static string LoadSummary(IReadOnlyList<LoadVariable> variables)
    {
        return variables.Count == 0
            ? "No added load variables."
            : string.Join(", ", variables.Select(variable => $"{variable.Name}: {variable.Value}"));
    }

    private static string RequiredEvidenceLabel(SessionPreflightPresentationReadModel preflight)
    {
        return preflight.ExpectedEvidenceFactCount <= 0
            ? "Observable evidence required."
            : $"{preflight.ExpectedEvidenceFactCount} required evidence items.";
    }

    private static string LiveMaterialText(LiveSessionPresentationReadModel presentation)
    {
        if (presentation.ActiveCue is { } cue)
        {
            return cue.Cue;
        }

        if (presentation.CurrentMaterials.Count > 0)
        {
            return presentation.CurrentMaterials[0].Value;
        }

        return LifecycleLabel(presentation.LifecycleStatus);
    }

    private static string LiveMaterialLabel(LiveSessionPresentationReadModel presentation)
    {
        if (presentation.ActiveCue is not null)
        {
            return "Cue";
        }

        return presentation.CurrentPhaseKind switch
        {
            RuntimeSessionPhaseKind.InstructionPrep => "Target",
            RuntimeSessionPhaseKind.EncodeWindow => "Encode",
            RuntimeSessionPhaseKind.ActiveWork => "Material",
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
        return presentation.AvailableCommands.Any(item => item.Command == command);
    }

    private static IReadOnlyList<string> TodayBranchLabels(CurrentTrainingPresentationReadModel presentation)
    {
        if ((presentation.Priority == TrainingPresentationPriorityKind.DecayRestoration ||
                presentation.Priority == TrainingPresentationPriorityKind.MaintenanceDue) &&
            presentation.MaintenanceDecayPriority is { } maintenance)
        {
            return [$"{maintenance.Branch} {LevelCode(maintenance.Level)}"];
        }

        if (presentation.Priority == TrainingPresentationPriorityKind.UrgentBlocker &&
            presentation.UrgentBlocker is { Branch: { } blockerBranch } blocker)
        {
            return blocker.Level.HasValue
                ? [$"{blockerBranch} {LevelCode(blocker.Level.Value)}"]
                : [blockerBranch.ToString()];
        }

        return TodayBranchSummaryLabels(presentation.PrimaryPrescribedWork?.BranchLevels ?? []);
    }

    private static string BranchNodeLabel(TrainingBranchLevelPresentation branchLevel)
    {
        return branchLevel.Level.HasValue
            ? $"{branchLevel.BranchLabel} · {branchLevel.LevelLabel ?? LevelLabel(branchLevel.Level.Value)}"
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
            : [branches[0].Branch.ToString(), $"+{branches.Length - 1}"];
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
            ? $"{branchLevel.Branch} {LevelCode(branchLevel.Level.Value)}"
            : branchLevel.Branch.ToString();
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
                AppTrainingSessionType.Regression => "Regress",
                AppTrainingSessionType.Transfer => "Transfer",
                AppTrainingSessionType.Recovery => "Recover",
                AppTrainingSessionType.Maintenance => "Maintain",
                _ => "Work",
            };
        }

        return work.WeeklySession.HasValue ? WeeklySessionLabel(work.WeeklySession.Value) : "Work";
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
            TrainingPresentationBlockerKind.PreparationRejected => "Preflight",
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
            _ => "Prescribed work",
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

    private static string CommandLabel(RuntimeInputCommandKind command, bool primary)
    {
        return command switch
        {
            RuntimeInputCommandKind.RespondToCue => primary ? "Respond" : "Cue",
            RuntimeInputCommandKind.SubmitAnswer => "Submit",
            RuntimeInputCommandKind.MarkDrift => "Drift",
            RuntimeInputCommandKind.MarkGuess => "Guess",
            RuntimeInputCommandKind.MarkError => "Error",
            RuntimeInputCommandKind.Correct => "Correct",
            RuntimeInputCommandKind.StartAudit => "Audit",
            RuntimeInputCommandKind.FinishPhase => primary ? "Finish" : "Finish",
            RuntimeInputCommandKind.Pause => "Pause",
            RuntimeInputCommandKind.Resume => "Resume",
            RuntimeInputCommandKind.Abandon => "Abandon",
            _ => "Action",
        };
    }

    private static string PhaseLabel(RuntimeSessionPhaseKind? phase)
    {
        return phase switch
        {
            RuntimeSessionPhaseKind.InstructionPrep => "Prep",
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
            RuntimeSessionLifecycleStatus.Abandoned => "Abandoned",
            _ => status.ToString(),
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
            TrainingResultPresentationOutcomeKind.Abandoned => "Abandoned",
            TrainingResultPresentationOutcomeKind.TimedOut => "Timed out",
            TrainingResultPresentationOutcomeKind.Failed => "Failed",
            TrainingResultPresentationOutcomeKind.NoAdvancement => "No advancement",
            TrainingResultPresentationOutcomeKind.PassedOnce => "Passed once",
            TrainingResultPresentationOutcomeKind.Stabilizing => "Stabilizing",
            TrainingResultPresentationOutcomeKind.Owned => "Owned",
            TrainingResultPresentationOutcomeKind.Maintenance => "Maintenance",
            TrainingResultPresentationOutcomeKind.MaintenanceWarning => "Warning",
            TrainingResultPresentationOutcomeKind.MaintenanceFailed => "Maintenance failed",
            TrainingResultPresentationOutcomeKind.Decayed => "Decayed",
            TrainingResultPresentationOutcomeKind.Recovery => "Recovery",
            TrainingResultPresentationOutcomeKind.Blocked => "Blocked",
            TrainingResultPresentationOutcomeKind.TransferEligible => "Transfer ready",
            _ => "Result pending",
        };
    }

    private static string ResultMarker(TrainingResultPresentationOutcomeKind outcome)
    {
        return outcome switch
        {
            TrainingResultPresentationOutcomeKind.Abandoned => "A",
            TrainingResultPresentationOutcomeKind.TimedOut => "T",
            TrainingResultPresentationOutcomeKind.Failed => "F",
            TrainingResultPresentationOutcomeKind.NoAdvancement => "-",
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
            TrainingResultPresentationOutcomeKind.Abandoned
                or TrainingResultPresentationOutcomeKind.TimedOut
                or TrainingResultPresentationOutcomeKind.Failed
                or TrainingResultPresentationOutcomeKind.MaintenanceFailed
                or TrainingResultPresentationOutcomeKind.Decayed
                or TrainingResultPresentationOutcomeKind.Blocked => MgColors.Blocked,
            TrainingResultPresentationOutcomeKind.Owned => MgColors.Owned,
            TrainingResultPresentationOutcomeKind.PassedOnce
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
            TrainingResultPresentationOutcomeKind.Abandoned => "Session abandoned. No successful evidence.",
            TrainingResultPresentationOutcomeKind.TimedOut => "Session timed out. No successful evidence.",
            TrainingResultPresentationOutcomeKind.Failed => FailureOutcomeText(result),
            TrainingResultPresentationOutcomeKind.NoAdvancement => "Session recorded. Progress unchanged.",
            TrainingResultPresentationOutcomeKind.PassedOnce => "One pass recorded. Ownership is still locked.",
            TrainingResultPresentationOutcomeKind.Stabilizing => "Clean pass recorded. Stabilization remains active.",
            TrainingResultPresentationOutcomeKind.Owned => "Ownership recorded.",
            TrainingResultPresentationOutcomeKind.Maintenance => "Maintenance check recorded.",
            TrainingResultPresentationOutcomeKind.MaintenanceWarning => "Maintenance warning recorded.",
            TrainingResultPresentationOutcomeKind.MaintenanceFailed => "Maintenance failed.",
            TrainingResultPresentationOutcomeKind.Decayed => "Decay recorded. Restoration required.",
            TrainingResultPresentationOutcomeKind.Recovery => "Recovery work recorded. Advancement unchanged.",
            TrainingResultPresentationOutcomeKind.Blocked => "Advancement blocked.",
            TrainingResultPresentationOutcomeKind.TransferEligible => "Transfer requirement recorded.",
            _ => "Result is still being recorded.",
        };
    }

    private static string FailureOutcomeText(ResultPresentationReadModel result)
    {
        return result.FailureType is null
            ? "Session failed. No advancement."
            : $"{FailureTypeLabel(result.FailureType.Value)}. No advancement.";
    }

    private static string ResultEvidenceText(ResultPresentationReadModel result)
    {
        if (result.Outcome is TrainingResultPresentationOutcomeKind.Abandoned
            or TrainingResultPresentationOutcomeKind.TimedOut
            or TrainingResultPresentationOutcomeKind.Failed)
        {
            return result.EvidenceSummary.HasObservableEvidence
                ? "Failure evidence recorded."
                : "No successful evidence.";
        }

        if (result.EvidenceSummary.HasFailureEvidence)
        {
            return "Failure evidence recorded.";
        }

        if (result.ProducesSuccessfulEvidence)
        {
            return EvidenceCategoryLabel(result.EvidenceSummary.LatestEvidenceCategory) + " evidence recorded.";
        }

        return result.EvidenceSummary.HasObservableEvidence
            ? "Evidence recorded. Progress unchanged."
            : "Evidence incomplete.";
    }

    private static string ResultChangeText(ResultPresentationReadModel result)
    {
        if (result.StateTransition is { Changed: true } transition)
        {
            return $"{transition.Branch} {transition.Level}: {StateLabel(transition.FromState)} -> {StateLabel(transition.ToState)}.";
        }

        return result.Outcome switch
        {
            TrainingResultPresentationOutcomeKind.Maintenance => "Branch level unchanged; maintenance contact recorded.",
            TrainingResultPresentationOutcomeKind.MaintenanceWarning => "Branch level unchanged; maintenance warning remains.",
            TrainingResultPresentationOutcomeKind.Decayed => "Dependent advancement is capped until restoration.",
            TrainingResultPresentationOutcomeKind.Recovery => "Branch level unchanged; reduced-load work recorded.",
            TrainingResultPresentationOutcomeKind.Abandoned
                or TrainingResultPresentationOutcomeKind.TimedOut
                or TrainingResultPresentationOutcomeKind.Failed
                or TrainingResultPresentationOutcomeKind.Blocked => "Progress unchanged.",
            _ => "No branch-level state changed.",
        };
    }

    private static string ResultNextActionText(CurrentTrainingPresentationReadModel? next)
    {
        if (next is null)
        {
            return "Return to Today when recording finishes.";
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
            return $"{ActionLabel(next.PrimaryAction)} {maintenance.Branch} {maintenance.Level}.";
        }

        if (next.PrimaryPrescribedWork is { } work)
        {
            return $"{ActionLabel(next.PrimaryAction)} {ShortWorkLabel(work)}.";
        }

        return "Return to Today for the next prescribed state.";
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
            GlobalReviewFailureKind.CurrentTransferOrStabilizationArtifactMissing => "Transfer or stabilization evidence missing",
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
        return $"{BranchLabel(branch)} · {LevelLabel(level)}";
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
