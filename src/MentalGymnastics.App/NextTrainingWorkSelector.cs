using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App;

public enum AppTrainingSessionType
{
    Practice,
    Load,
    Test,
    Stabilization,
    Regression,
    Transfer,
    Recovery,
    Maintenance,
}

public enum NextTrainingWorkSelectionKind
{
    Allowed,
    Blocked,
    MaintenanceNeeded,
    Recovery,
    Deload,
}

public enum NextTrainingWorkBlockerSource
{
    BranchState,
    MaintenanceCurrency,
    TestReadiness,
    DependencyCap,
    GlobalBalance,
    TransferEligibility,
    WeeklyProgramming,
    Decay,
}

public sealed class RequestedTrainingWork
{
    public RequestedTrainingWork(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        AppTrainingSessionType sessionType,
        IEnumerable<LoadVariable>? loadVariables = null)
    {
        Branch = branch;
        Level = level;
        Drill = drill;
        SessionType = sessionType;
        LoadVariables = (loadVariables ?? []).ToArray();
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public AppTrainingSessionType SessionType { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }
}

public sealed class NextTrainingWorkSelectionQuery
{
    public NextTrainingWorkSelectionQuery(
        TrainingDate asOf,
        RequestedTrainingWork? requestedWork = null,
        RecoveryDecisionRequest? RecoveryEvidence = null,
        DeloadDecisionRequest? DeloadEvidence = null)
    {
        AsOf = asOf;
        RequestedWork = requestedWork;
        this.RecoveryEvidence = RecoveryEvidence;
        this.DeloadEvidence = DeloadEvidence;
    }

    public TrainingDate AsOf { get; }

    public RequestedTrainingWork? RequestedWork { get; }

    public RecoveryDecisionRequest? RecoveryEvidence { get; }

    public DeloadDecisionRequest? DeloadEvidence { get; }
}

public sealed record NextTrainingWorkBlocker(
    NextTrainingWorkBlockerSource Source,
    BranchCode? Branch,
    GlobalLevelId? Level,
    string Detail,
    MaintenanceCurrencyState? MaintenanceCurrencyState = null,
    TestReadinessFailureKind? TestReadinessFailureKind = null,
    DependencyCapReason? DependencyCapReason = null,
    GlobalBalanceIssueKind? GlobalBalanceIssueKind = null,
    TransferEligibilityFailureKind? TransferEligibilityFailureKind = null,
    WeeklyProgrammingConstraintKind? WeeklyConstraintKind = null);

public sealed class SelectedTrainingWork
{
    public SelectedTrainingWork(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        AppTrainingSessionType sessionType,
        string demand,
        string standard,
        string honestyConstraint,
        IEnumerable<LoadVariable> loadVariables,
        bool advancementWorkAllowed)
    {
        if (string.IsNullOrWhiteSpace(demand))
        {
            throw new ArgumentException("Selected work demand is required.", nameof(demand));
        }

        if (string.IsNullOrWhiteSpace(standard))
        {
            throw new ArgumentException("Selected work standard is required.", nameof(standard));
        }

        if (string.IsNullOrWhiteSpace(honestyConstraint))
        {
            throw new ArgumentException("Selected work honesty constraint is required.", nameof(honestyConstraint));
        }

        ArgumentNullException.ThrowIfNull(loadVariables);

        Branch = branch;
        Level = level;
        Drill = drill;
        SessionType = sessionType;
        Demand = demand;
        Standard = standard;
        HonestyConstraint = honestyConstraint;
        LoadVariables = loadVariables.ToArray();
        AdvancementWorkAllowed = advancementWorkAllowed;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public AppTrainingSessionType SessionType { get; }

    public string Demand { get; }

    public string Standard { get; }

    public string HonestyConstraint { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public bool AdvancementWorkAllowed { get; }
}

public sealed class NextTrainingWorkSelection
{
    public NextTrainingWorkSelection(
        NextTrainingWorkSelectionKind kind,
        SelectedTrainingWork? selectedWork,
        IEnumerable<NextTrainingWorkBlocker> blockers,
        CurrentTrainingStateReadModel currentState,
        TestReadinessResult? testReadiness = null,
        TransferEligibilityResult? transferEligibility = null,
        RecoveryDecisionResult? recoveryDecision = null,
        DeloadDecisionResult? deloadDecision = null)
    {
        ArgumentNullException.ThrowIfNull(blockers);
        ArgumentNullException.ThrowIfNull(currentState);

        Kind = kind;
        SelectedWork = selectedWork;
        Blockers = blockers.ToArray();
        CurrentState = currentState;
        TestReadiness = testReadiness;
        TransferEligibility = transferEligibility;
        RecoveryDecision = recoveryDecision;
        DeloadDecision = deloadDecision;
    }

    public NextTrainingWorkSelectionKind Kind { get; }

    public SelectedTrainingWork? SelectedWork { get; }

    public IReadOnlyList<NextTrainingWorkBlocker> Blockers { get; }

    public CurrentTrainingStateReadModel CurrentState { get; }

    public TestReadinessResult? TestReadiness { get; }

    public TransferEligibilityResult? TransferEligibility { get; }

    public RecoveryDecisionResult? RecoveryDecision { get; }

    public DeloadDecisionResult? DeloadDecision { get; }
}

public sealed class NextTrainingWorkSelector
{
    private readonly CurrentTrainingStateLoader stateLoader;
    private readonly LocalMaintenanceCheckStore maintenanceCheckStore;

    public NextTrainingWorkSelector(AppStartupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        stateLoader = new CurrentTrainingStateLoader(configuration);
        maintenanceCheckStore = new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions);
    }

    public async ValueTask<NextTrainingWorkSelection> SelectAsync(
        NextTrainingWorkSelectionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var currentState = await stateLoader.LoadAsync(
            new CurrentTrainingStateQuery(query.AsOf),
            cancellationToken).ConfigureAwait(false);
        var maintenanceCurrency = await LoadMaintenanceCurrencyAsync(
            currentState.CurrentPractitionerState,
            query.AsOf,
            cancellationToken).ConfigureAwait(false);

        var deloadDecision = query.DeloadEvidence is null
            ? null
            : DeloadDecisionEvaluator.Evaluate(query.DeloadEvidence);
        if (deloadDecision?.ShouldDeload == true)
        {
            return DeloadSelection(currentState, query, deloadDecision);
        }

        var recoveryDecision = query.RecoveryEvidence is null
            ? null
            : RecoveryDecisionEvaluator.Evaluate(query.RecoveryEvidence);
        if (recoveryDecision?.ShouldRecover == true)
        {
            return RecoverySelection(currentState, query, recoveryDecision);
        }

        if (currentState.DueMaintenance.Count > 0)
        {
            return MaintenanceSelection(currentState);
        }

        if (query.RequestedWork is not null)
        {
            return RequestedSelection(
                currentState,
                maintenanceCurrency,
                query.RequestedWork);
        }

        return DefaultPlanSelection(currentState, maintenanceCurrency);
    }

    private static NextTrainingWorkSelection DeloadSelection(
        CurrentTrainingStateReadModel currentState,
        NextTrainingWorkSelectionQuery query,
        DeloadDecisionResult deloadDecision)
    {
        var selectedWork = currentState.DueMaintenance.Count > 0
            ? WorkFromMaintenanceRecord(currentState.DueMaintenance[0])
            : RecoveryWorkFor(
                query.RequestedWork ??
                    DefaultRequestedWork(currentState, AppTrainingSessionType.Recovery),
                advancementWorkAllowed: false);

        return new NextTrainingWorkSelection(
            NextTrainingWorkSelectionKind.Deload,
            selectedWork,
            [],
            currentState,
            deloadDecision: deloadDecision);
    }

    private static NextTrainingWorkSelection RecoverySelection(
        CurrentTrainingStateReadModel currentState,
        NextTrainingWorkSelectionQuery query,
        RecoveryDecisionResult recoveryDecision)
    {
        var requested = query.RequestedWork ??
            new RequestedTrainingWork(
                recoveryDecision.Branch,
                recoveryDecision.Level,
                PrimaryDrillFor(recoveryDecision.Branch),
                AppTrainingSessionType.Recovery);

        return new NextTrainingWorkSelection(
            NextTrainingWorkSelectionKind.Recovery,
            RecoveryWorkFor(requested, advancementWorkAllowed: false),
            [],
            currentState,
            recoveryDecision: recoveryDecision);
    }

    private static NextTrainingWorkSelection MaintenanceSelection(
        CurrentTrainingStateReadModel currentState)
    {
        var dueMaintenance = currentState.DueMaintenance[0];

        return new NextTrainingWorkSelection(
            NextTrainingWorkSelectionKind.MaintenanceNeeded,
            WorkFromMaintenanceRecord(dueMaintenance),
            [
                new NextTrainingWorkBlocker(
                    NextTrainingWorkBlockerSource.MaintenanceCurrency,
                    dueMaintenance.BranchLevel.Branch,
                    dueMaintenance.BranchLevel.Level,
                    $"{dueMaintenance.BranchLevel.Branch} {dueMaintenance.BranchLevel.Level} maintenance is {dueMaintenance.Currency.State}.",
                    MaintenanceCurrencyState: dueMaintenance.Currency.State),
            ],
            currentState);
    }

    private static NextTrainingWorkSelection RequestedSelection(
        CurrentTrainingStateReadModel currentState,
        IReadOnlyList<MaintenanceCurrencyResult> maintenanceCurrency,
        RequestedTrainingWork requested)
    {
        var validation = ValidateRequestedWork(currentState, maintenanceCurrency, requested);

        if (validation.Blockers.Count > 0)
        {
            return new NextTrainingWorkSelection(
                NextTrainingWorkSelectionKind.Blocked,
                selectedWork: null,
                validation.Blockers,
                currentState,
                validation.TestReadiness,
                validation.TransferEligibility);
        }

        return new NextTrainingWorkSelection(
            NextTrainingWorkSelectionKind.Allowed,
            WorkFromRequest(requested, advancementWorkAllowed: true),
            [],
            currentState,
            validation.TestReadiness,
            validation.TransferEligibility);
    }

    private static NextTrainingWorkSelection DefaultPlanSelection(
        CurrentTrainingStateReadModel currentState,
        IReadOnlyList<MaintenanceCurrencyResult> maintenanceCurrency)
    {
        var requested = DefaultRequestedWork(currentState);
        var validation = ValidateRequestedWork(currentState, maintenanceCurrency, requested);

        if (validation.Blockers.Count > 0)
        {
            return new NextTrainingWorkSelection(
                NextTrainingWorkSelectionKind.Blocked,
                selectedWork: null,
                validation.Blockers,
                currentState,
                validation.TestReadiness,
                validation.TransferEligibility);
        }

        return new NextTrainingWorkSelection(
            NextTrainingWorkSelectionKind.Allowed,
            WorkFromRequest(requested, advancementWorkAllowed: true),
            [],
            currentState,
            validation.TestReadiness,
            validation.TransferEligibility);
    }

    private static WorkValidationResult ValidateRequestedWork(
        CurrentTrainingStateReadModel currentState,
        IReadOnlyList<MaintenanceCurrencyResult> maintenanceCurrency,
        RequestedTrainingWork requested)
    {
        var blockers = new List<NextTrainingWorkBlocker>();
        var testReadiness = default(TestReadinessResult);
        var transferEligibility = default(TransferEligibilityResult);

        if (!currentState.CurrentPractitionerState.TryGetBranchLevelState(
                requested.Branch,
                requested.Level,
                out var branchLevelState) ||
            branchLevelState == BranchLevelState.Unopened)
        {
            blockers.Add(new NextTrainingWorkBlocker(
                NextTrainingWorkBlockerSource.BranchState,
                requested.Branch,
                requested.Level,
                $"{requested.Branch} {requested.Level} is not open for training."));
        }

        if (!DrillBelongsToBranch(requested.Drill, requested.Branch))
        {
            blockers.Add(new NextTrainingWorkBlocker(
                NextTrainingWorkBlockerSource.BranchState,
                requested.Branch,
                requested.Level,
                $"{requested.Drill} does not belong to {requested.Branch}."));
        }

        if (branchLevelState == BranchLevelState.Decayed &&
            requested.SessionType is not AppTrainingSessionType.Maintenance
                and not AppTrainingSessionType.Recovery
                and not AppTrainingSessionType.Regression)
        {
            blockers.Add(new NextTrainingWorkBlocker(
                NextTrainingWorkBlockerSource.Decay,
                requested.Branch,
                requested.Level,
                $"{requested.Branch} {requested.Level} is decayed and must be restored before ordinary advancement work."));
        }

        if (requested.SessionType == AppTrainingSessionType.Stabilization &&
            branchLevelState is not BranchLevelState.PassedOnce and not BranchLevelState.Stabilizing)
        {
            blockers.Add(new NextTrainingWorkBlocker(
                NextTrainingWorkBlockerSource.BranchState,
                requested.Branch,
                requested.Level,
                "Stabilization work requires a passed-once or stabilizing branch-level state."));
        }

        if (IsAdvancementWork(requested.SessionType) &&
            !currentState.WeeklyPlan.AdvancementWorkAllowed)
        {
            blockers.AddRange(currentState.WeeklyPlan.Constraints.Select(constraint =>
                new NextTrainingWorkBlocker(
                    NextTrainingWorkBlockerSource.WeeklyProgramming,
                    constraint.Branch,
                    Level: null,
                    constraint.Detail,
                    WeeklyConstraintKind: constraint.Kind)));
        }

        if (requested.SessionType == AppTrainingSessionType.Test)
        {
            testReadiness = TestReadinessEvaluator.Evaluate(
                BuildTestReadinessRequest(currentState, maintenanceCurrency, requested));

            blockers.AddRange(testReadiness.Failures.Select(failure =>
                new NextTrainingWorkBlocker(
                    NextTrainingWorkBlockerSource.TestReadiness,
                    requested.Branch,
                    requested.Level,
                    failure.Detail,
                    TestReadinessFailureKind: failure.Kind)));
        }

        if (requested.SessionType == AppTrainingSessionType.Transfer)
        {
            transferEligibility = TransferEligibilityEvaluator.Evaluate(
                BuildTransferEligibilityRequest(requested));

            blockers.AddRange(transferEligibility.Failures.Select(failure =>
                new NextTrainingWorkBlocker(
                    NextTrainingWorkBlockerSource.TransferEligibility,
                    requested.Branch,
                    requested.Level,
                    failure.Detail,
                    TransferEligibilityFailureKind: failure.Kind)));
        }

        if (IsAdvancementWork(requested.SessionType))
        {
            blockers.AddRange(DependencyCapEvaluator.Evaluate(
                    new DependencyCapRequest(
                        requested.Branch,
                        currentState.CurrentPractitionerState,
                        maintenanceCurrency)).Caps
                .Select(cap => new NextTrainingWorkBlocker(
                    NextTrainingWorkBlockerSource.DependencyCap,
                    cap.PrerequisiteBranch,
                    cap.PrerequisiteLevel,
                    cap.Detail,
                    DependencyCapReason: cap.Reason)));

            blockers.AddRange(GlobalBalanceEvaluator.EvaluateAdvancement(
                    new GlobalBalanceAdvancementRequest(
                        requested.Branch,
                        requested.Level,
                        currentState.CurrentPractitionerState,
                        maintenanceCurrency)).Issues
                .Select(issue => new NextTrainingWorkBlocker(
                    NextTrainingWorkBlockerSource.GlobalBalance,
                    issue.Branch,
                    issue.Level,
                    issue.Detail,
                    GlobalBalanceIssueKind: issue.Kind)));
        }

        return new WorkValidationResult(blockers, testReadiness, transferEligibility);
    }

    private static TestReadinessRequest BuildTestReadinessRequest(
        CurrentTrainingStateReadModel currentState,
        IReadOnlyList<MaintenanceCurrencyResult> maintenanceCurrency,
        RequestedTrainingWork requested)
    {
        var demand = StandardFor(requested.Branch, requested.Level).Demand;

        return new TestReadinessRequest(
            currentState.CurrentPractitionerState,
            requested.Branch,
            requested.Level,
            requested.Drill,
            demand,
            currentState.RecentSessions
                .Where(session => session.SessionType is LocalCompletedSessionType.Practice or LocalCompletedSessionType.Load)
                .Where(session => session.Drill.HasValue)
                .SelectMany(session => session.BranchLevels.Select(branchLevel =>
                    new TestReadinessPracticeSession(
                        branchLevel.Branch,
                        branchLevel.Level,
                        session.Drill!.Value,
                        StandardFor(branchLevel.Branch, branchLevel.Level).Demand,
                        session.CleanPerformance))),
            maintenanceCurrency.Select(currency =>
                new PrerequisiteMaintenanceCheck(
                    currency.Branch,
                    currency.OwnedLevel,
                    currency.State == MaintenanceCurrencyState.Current)),
            StandardFor(requested.Branch, requested.Level).Standard,
            DrillFor(requested.Drill).HonestyConstraint);
    }

    private static TransferEligibilityRequest BuildTransferEligibilityRequest(
        RequestedTrainingWork requested)
    {
        var transfer = TransferTestCatalog.TransferTests.Single(test => test.SourceBranch == requested.Branch);
        var standard = StandardFor(requested.Branch, requested.Level);
        var capacity = DrillFor(requested.Drill).CapacityTrained.FirstOrDefault();

        return new TransferEligibilityRequest(
            requested.Branch,
            requested.Level,
            transfer.TransferTask,
            capacity,
            transfer.SameDemand,
            transfer.ChangedContext,
            new TransferSourceStandardEvidence(
                requested.Branch,
                requested.Level,
                standard.Standard,
                visibleInTransferArtifact: true),
            transfer.RetestRequirement);
    }

    private async ValueTask<IReadOnlyList<MaintenanceCurrencyResult>> LoadMaintenanceCurrencyAsync(
        PractitionerState currentState,
        TrainingDate asOf,
        CancellationToken cancellationToken)
    {
        var results = new List<MaintenanceCurrencyResult>();
        foreach (var status in currentState.BranchLevels.Where(IsMaintenanceRelevant))
        {
            var request = await maintenanceCheckStore.LoadMaintenanceCurrencyRequestAsync(
                status.Branch,
                status.Level,
                asOf,
                cancellationToken).ConfigureAwait(false);

            results.Add(MaintenanceCurrencyEvaluator.Evaluate(request));
        }

        return results
            .OrderBy(result => result.Branch)
            .ThenBy(result => result.OwnedLevel)
            .ToArray();
    }

    private static RequestedTrainingWork DefaultRequestedWork(
        CurrentTrainingStateReadModel currentState,
        AppTrainingSessionType? forcedSessionType = null)
    {
        foreach (var nextWork in currentState.AvailableNextWork.OrderBy(work => work.DayNumber))
        {
            foreach (var branch in nextWork.BranchEmphasis)
            {
                var status = SelectStatusForDefaultWork(currentState.CurrentPractitionerState, branch);
                if (status is null)
                {
                    continue;
                }

                var selectedStatus = status.Value;
                var sessionType = forcedSessionType ?? SessionTypeFor(nextWork.Session, selectedStatus.State);
                return new RequestedTrainingWork(
                    selectedStatus.Branch,
                    selectedStatus.Level,
                    PrimaryDrillFor(selectedStatus.Branch),
                    sessionType);
            }
        }

        return new RequestedTrainingWork(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            forcedSessionType ?? AppTrainingSessionType.Practice);
    }

    private static BranchLevelStatus? SelectStatusForDefaultWork(
        PractitionerState currentState,
        BranchCode branch)
    {
        return currentState.BranchLevels
            .Where(status => status.Branch == branch && status.State != BranchLevelState.Unopened)
            .OrderBy(status => StatusPriority(status.State))
            .ThenByDescending(status => LevelRank(status.Level))
            .FirstOrDefault();
    }

    private static SelectedTrainingWork WorkFromMaintenanceRecord(
        LocalDueMaintenanceRecord dueMaintenance)
    {
        return WorkFromRequest(
            new RequestedTrainingWork(
                dueMaintenance.BranchLevel.Branch,
                dueMaintenance.BranchLevel.Level,
                PrimaryDrillFor(dueMaintenance.BranchLevel.Branch),
                AppTrainingSessionType.Maintenance),
            advancementWorkAllowed: false);
    }

    private static SelectedTrainingWork RecoveryWorkFor(
        RequestedTrainingWork requested,
        bool advancementWorkAllowed)
    {
        return WorkFromRequest(
            new RequestedTrainingWork(
                requested.Branch,
                requested.Level,
                requested.Drill,
                AppTrainingSessionType.Recovery,
                requested.LoadVariables),
            advancementWorkAllowed);
    }

    private static SelectedTrainingWork WorkFromRequest(
        RequestedTrainingWork requested,
        bool advancementWorkAllowed)
    {
        var standard = StandardFor(requested.Branch, requested.Level);
        var drill = DrillFor(requested.Drill);
        var loadVariables = requested.LoadVariables.Count == 0
            ? DefaultLoadVariablesFor(requested.Drill)
            : requested.LoadVariables;

        return new SelectedTrainingWork(
            requested.Branch,
            requested.Level,
            requested.Drill,
            requested.SessionType,
            standard.Demand,
            standard.Standard,
            drill.HonestyConstraint,
            loadVariables,
            advancementWorkAllowed);
    }

    private static IReadOnlyList<LoadVariable> DefaultLoadVariablesFor(DrillId drill)
    {
        return drill switch
        {
            DrillId.FH1TargetHold =>
            [
                new LoadVariable("duration", "3 minutes"),
                new LoadVariable("target subtlety", "simple phrase"),
                new LoadVariable("recovery window", "10 seconds"),
            ],
            DrillId.FH2DistractorHold =>
            [
                new LoadVariable("duration", "5 minutes"),
                new LoadVariable("target subtlety", "simple phrase"),
                new LoadVariable("recovery window", "10 seconds"),
                new LoadVariable("distractor frequency", "periodic"),
                new LoadVariable("distractor salience", "low"),
            ],
            DrillId.FS1CueSwitch =>
            [
                new LoadVariable("target count", "2"),
                new LoadVariable("switch count", "4"),
                new LoadVariable("cue density", "low"),
                new LoadVariable("return precision", "next cue"),
            ],
            DrillId.FS2InvalidCueFilter =>
            [
                new LoadVariable("target count", "2"),
                new LoadVariable("switch count", "6"),
                new LoadVariable("cue density", "moderate"),
                new LoadVariable("rule contrast", "valid symbol versus invalid lure"),
                new LoadVariable("return precision", "next valid cue"),
            ],
            DrillId.WM1DelayedReconstruction =>
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("delay", "60 seconds"),
            ],
            DrillId.WM2MentalTransform =>
            [
                new LoadVariable("item count", "6"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("operation steps", "2"),
                new LoadVariable("delay", "2 minutes"),
                new LoadVariable("interference", "reversal"),
            ],
            DrillId.IR1GoNoGoRule =>
            [
                new LoadVariable("cue conflict", "simple go/no-go symbols"),
                new LoadVariable("response speed", "2 seconds"),
                new LoadVariable("no-go frequency", "every third cue"),
            ],
            DrillId.IR2ExceptionRule =>
            [
                new LoadVariable("exception count", "3"),
                new LoadVariable("response speed", "2 seconds"),
                new LoadVariable("similarity", "near symbols"),
            ],
            DrillId.DE1PairDiscrimination =>
            [
                new LoadVariable("similarity", "near match"),
                new LoadVariable("item quantity", "6"),
                new LoadVariable("time limit", "60 seconds"),
            ],
            DrillId.DE2SeededAudit =>
            [
                new LoadVariable("error subtlety", "subtle wording errors"),
                new LoadVariable("output length", "6 lines"),
                new LoadVariable("audit delay", "5 minutes"),
                new LoadVariable("quantity", "3"),
            ],
            DrillId.CO1RuleExtraction =>
            [
                new LoadVariable("rule ambiguity", "clear examples"),
                new LoadVariable("example count", "8"),
            ],
            DrillId.CO2StructureMapping =>
            [
                new LoadVariable("relation count", "3"),
                new LoadVariable("domain distance", "near domain"),
            ],
            DrillId.AI1PressureRepeat =>
            [
                new LoadVariable("time pressure", "90 seconds"),
                new LoadVariable("observation", "visible evaluator note"),
            ],
            DrillId.AI2DisruptionRecovery =>
            [
                new LoadVariable("interruption timing", "mid-task after first checkpoint"),
                new LoadVariable("restart delay", "10 seconds"),
                new LoadVariable("task complexity", "two-target cue sequence"),
                new LoadVariable("recovery window", "30 seconds"),
            ],
            DrillId.TI1CompositeTask =>
            [
                new LoadVariable("number of branches", "2"),
                new LoadVariable("task length", "12 minutes"),
                new LoadVariable("transfer distance", "near transfer"),
            ],
            DrillId.TI2GlobalReviewTask =>
            [
                new LoadVariable("task length", "20 minutes"),
                new LoadVariable("pressure", "visible review pressure"),
                new LoadVariable("ambiguity", "moderate ambiguity"),
                new LoadVariable("delay", "5 minutes"),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unknown drill."),
        };
    }

    private static BranchLevelStandard StandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards.Single(standard => standard.Branch == branch && standard.Level == level);
    }

    private static DrillDefinition DrillFor(DrillId drill)
    {
        return ProgramCatalog.Drills.Single(definition => definition.Id == drill);
    }

    private static AppTrainingSessionType SessionTypeFor(
        WeeklySessionKind weeklySession,
        BranchLevelState branchLevelState)
    {
        return weeklySession switch
        {
            WeeklySessionKind.Load => AppTrainingSessionType.Load,
            WeeklySessionKind.TestOrStabilization =>
                branchLevelState is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing
                    ? AppTrainingSessionType.Stabilization
                    : AppTrainingSessionType.Test,
            WeeklySessionKind.TransferOrStabilization =>
                branchLevelState is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing
                    ? AppTrainingSessionType.Stabilization
                    : AppTrainingSessionType.Transfer,
            WeeklySessionKind.Transfer => AppTrainingSessionType.Transfer,
            WeeklySessionKind.Stabilization => AppTrainingSessionType.Stabilization,
            WeeklySessionKind.Maintenance => AppTrainingSessionType.Maintenance,
            WeeklySessionKind.Recovery
                or WeeklySessionKind.RecoveryOrLightMaintenance
                or WeeklySessionKind.OffOrRecovery
                or WeeklySessionKind.RecoveryOrRetest => AppTrainingSessionType.Recovery,
            _ => AppTrainingSessionType.Practice,
        };
    }

    private static bool IsAdvancementWork(AppTrainingSessionType sessionType)
    {
        return sessionType is
            AppTrainingSessionType.Load or
            AppTrainingSessionType.Test or
            AppTrainingSessionType.Stabilization or
            AppTrainingSessionType.Transfer;
    }

    private static bool IsMaintenanceRelevant(BranchLevelStatus status)
    {
        return status.State is
            BranchLevelState.Owned or
            BranchLevelState.Maintenance or
            BranchLevelState.Decayed;
    }

    private static int StatusPriority(BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.TestReady => 0,
            BranchLevelState.PassedOnce or BranchLevelState.Stabilizing => 1,
            BranchLevelState.Training => 2,
            BranchLevelState.Maintenance => 3,
            BranchLevelState.Owned => 4,
            BranchLevelState.Decayed => 5,
            _ => 6,
        };
    }

    private static int LevelRank(GlobalLevelId level)
    {
        return ((int)level) + 1;
    }

    private static DrillId PrimaryDrillFor(BranchCode branch)
    {
        return ProgramCatalog.Drills
            .First(definition => definition.Code.StartsWith(
                $"{branch}-1",
                StringComparison.Ordinal))
            .Id;
    }

    private static bool DrillBelongsToBranch(DrillId drill, BranchCode branch)
    {
        return DrillFor(drill).Code.StartsWith(
            $"{branch}-",
            StringComparison.Ordinal);
    }

    private sealed record WorkValidationResult(
        IReadOnlyList<NextTrainingWorkBlocker> Blockers,
        TestReadinessResult? TestReadiness,
        TransferEligibilityResult? TransferEligibility);
}
