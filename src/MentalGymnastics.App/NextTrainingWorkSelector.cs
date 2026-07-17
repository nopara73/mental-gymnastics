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
    private readonly LocalProgramRepository repository;

    public NextTrainingWorkSelector(AppStartupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        stateLoader = new CurrentTrainingStateLoader(configuration);
        repository = new LocalProgramRepository(configuration.LocalDatabaseOptions);
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
            ? currentState.DeloadDecision
            : DeloadDecisionEvaluator.Evaluate(query.DeloadEvidence);
        if (deloadDecision?.ShouldDeload == true)
        {
            return DeloadSelection(currentState, query, deloadDecision);
        }

        var recoveryDecision = query.RecoveryEvidence is null
            ? currentState.RecoveryDecision
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
                advancementWorkAllowed: false,
                recentSessions: currentState.RecentSessions);

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
                PrimaryDrillFor(recoveryDecision.Branch, recoveryDecision.Level),
                AppTrainingSessionType.Recovery);

        return new NextTrainingWorkSelection(
            NextTrainingWorkSelectionKind.Recovery,
            RecoveryWorkFor(
                requested,
                advancementWorkAllowed: false,
                recentSessions: currentState.RecentSessions),
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
                    "This skill needs a passing recheck before ordinary progress can continue.",
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
            WorkFromRequest(
                requested,
                advancementWorkAllowed: true,
                recentSessions: currentState.RecentSessions),
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
            WorkFromRequest(
                requested,
                advancementWorkAllowed: true,
                recentSessions: currentState.RecentSessions),
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
                "This skill level is not open yet."));
        }

        if (!DrillBelongsToBranch(requested.Drill, requested.Branch))
        {
            blockers.Add(new NextTrainingWorkBlocker(
                NextTrainingWorkBlockerSource.BranchState,
                requested.Branch,
                requested.Level,
                "This exercise is not part of the selected skill."));
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
                "This skill needs a passing recheck before ordinary progress can continue."));
        }

        if (requested.SessionType == AppTrainingSessionType.Stabilization &&
            branchLevelState is not BranchLevelState.PassedOnce and not BranchLevelState.Stabilizing)
        {
            blockers.Add(new NextTrainingWorkBlocker(
                NextTrainingWorkBlockerSource.BranchState,
                requested.Branch,
                requested.Level,
                "A repeat test is available only after the first test has passed."));
        }

        if (IsAdvancementWork(requested.SessionType) &&
            !currentState.WeeklyPlan.AdvancementWorkAllowed)
        {
            blockers.AddRange(currentState.WeeklyPlan.Constraints.Select(constraint =>
                new NextTrainingWorkBlocker(
                    NextTrainingWorkBlockerSource.WeeklyProgramming,
                    constraint.Branch,
                    Level: null,
                    WeeklyConstraintDetail(constraint.Kind),
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
                    TestReadinessDetail(failure.Kind),
                    TestReadinessFailureKind: failure.Kind)));
        }

        if (requested.SessionType == AppTrainingSessionType.Transfer)
        {
            if (requested.Level == GlobalLevelId.L4 &&
                branchLevelState != BranchLevelState.TestReady)
            {
                blockers.Add(new NextTrainingWorkBlocker(
                    NextTrainingWorkBlockerSource.BranchState,
                    requested.Branch,
                    requested.Level,
                    "The Level 4 transfer test is not ready yet."));
            }

            transferEligibility = TransferEligibilityEvaluator.Evaluate(
                BuildTransferEligibilityRequest(requested));

            blockers.AddRange(transferEligibility.Failures.Select(failure =>
                new NextTrainingWorkBlocker(
                    NextTrainingWorkBlockerSource.TransferEligibility,
                    requested.Branch,
                    requested.Level,
                    TransferDetail(failure.Kind),
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
                    DependencyDetail(cap.Reason),
                    DependencyCapReason: cap.Reason)));

            blockers.AddRange(GlobalBalanceEvaluator.EvaluateAdvancement(
                    new GlobalBalanceAdvancementRequest(
                        requested.Branch,
                        requested.Level,
                        currentState.CurrentPractitionerState,
                        maintenanceCurrency,
                        currentState.LastCompletedGlobalReview)).Issues
                .Select(issue => new NextTrainingWorkBlocker(
                    NextTrainingWorkBlockerSource.GlobalBalance,
                    issue.Branch,
                    issue.Level,
                    GlobalBalanceDetail(issue.Kind),
                    GlobalBalanceIssueKind: issue.Kind)));
        }

        return new WorkValidationResult(blockers, testReadiness, transferEligibility);
    }

    private static string TestReadinessDetail(TestReadinessFailureKind failure)
    {
        return failure switch
        {
            TestReadinessFailureKind.PrerequisiteNotOwned =>
                "A required earlier skill has not passed all of its repeat tests.",
            TestReadinessFailureKind.PrerequisiteMaintenanceOverdue =>
                "A required earlier skill needs a passing recheck.",
            TestReadinessFailureKind.RecentCleanPracticeMissing =>
                "Complete two clean practices at this exercise and difficulty before testing.",
            TestReadinessFailureKind.StandardNotStated or
            TestReadinessFailureKind.HonestyConstraintNotNamed =>
                "The required test rules could not be verified. Set up the test again.",
            _ => "This test is not ready yet.",
        };
    }

    private static string TransferDetail(TransferEligibilityFailureKind failure)
    {
        return failure switch
        {
            TransferEligibilityFailureKind.TransferTaskDoesNotMatchSourceBranch =>
                "This transfer task does not match the skill being tested.",
            TransferEligibilityFailureKind.TrainedCapacityNotSpecified or
            TransferEligibilityFailureKind.TrainedCapacityNotInSourceBranch =>
                "The transfer setup does not identify the skill it is meant to test.",
            TransferEligibilityFailureKind.SourceDemandNotPreserved =>
                "The task changed the tested difficulty instead of only changing the context.",
            TransferEligibilityFailureKind.ContextNotChanged =>
                "The transfer task must use a genuinely different context.",
            TransferEligibilityFailureKind.SourceStandardEvidenceMissing or
            TransferEligibilityFailureKind.SourceStandardDoesNotMatchCatalog or
            TransferEligibilityFailureKind.SourceStandardNotVisible =>
                "The original skill's passing result was not recorded with this transfer.",
            TransferEligibilityFailureKind.RetestRequirementMissing =>
                "A second equivalent transfer task is still required.",
            _ => "This transfer test is not ready yet.",
        };
    }

    private static string DependencyDetail(DependencyCapReason reason)
    {
        return reason switch
        {
            DependencyCapReason.DecayedPrerequisite =>
                "A required earlier skill needs to be restored with a passing recheck.",
            DependencyCapReason.OverduePrerequisiteMaintenance =>
                "A required earlier skill needs a passing recheck.",
            _ => "A required earlier skill must pass before this one can advance.",
        };
    }

    private static string GlobalBalanceDetail(GlobalBalanceIssueKind issue)
    {
        return issue switch
        {
            GlobalBalanceIssueKind.FoundationalOwnedLevelSpreadTooWide =>
                "Bring the lower foundational skills up before advancing this one.",
            GlobalBalanceIssueKind.AdvancedPrerequisiteMaintenanceOverdue =>
                "A required earlier skill needs a passing recheck.",
            GlobalBalanceIssueKind.AdvancedPrerequisiteDecayed or
            GlobalBalanceIssueKind.ConceptOperationsPrerequisiteDecayed or
            GlobalBalanceIssueKind.AffectiveInterferencePrerequisiteDecayed =>
                "A required earlier skill needs to be restored with a passing recheck.",
            GlobalBalanceIssueKind.TransferIntegrationComponentFailedLastGlobalReview =>
                "A component that failed the latest whole-program check must pass before this skill advances.",
            GlobalBalanceIssueKind.AdvancedClassificationRequiresPassedGlobalReview =>
                "The latest whole-program check must pass before advanced classification.",
            _ => "A required related skill must pass before this one can advance.",
        };
    }

    private static string WeeklyConstraintDetail(WeeklyProgrammingConstraintKind constraint)
    {
        return constraint switch
        {
            WeeklyProgrammingConstraintKind.BeginnerFixedTemplateRequired =>
                "Use this week's prescribed beginner schedule before adding different test work.",
            WeeklyProgrammingConstraintKind.MaintenanceNotCurrent =>
                "Complete the due recheck before starting another test.",
            WeeklyProgrammingConstraintKind.RecoveryRequired =>
                "Complete the prescribed reduced practice before another test.",
            WeeklyProgrammingConstraintKind.AdvancementTestingSuspended =>
                "New tests are paused for the rest of this reduced-load week.",
            _ => "This week's schedule does not allow another test yet.",
        };
    }

    private static TestReadinessRequest BuildTestReadinessRequest(
        CurrentTrainingStateReadModel currentState,
        IReadOnlyList<MaintenanceCurrencyResult> maintenanceCurrency,
        RequestedTrainingWork requested)
    {
        return TestReadinessRequestFactory.Create(
            currentState.CurrentPractitionerState,
            requested.Branch,
            requested.Level,
            requested.Drill,
            TestReadinessRequestFactory.FromSessionHistory(currentState.RecentSessions),
            maintenanceCurrency);
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
        foreach (var status in MaintenanceScope.HighestEarnedByBranch(currentState))
        {
            var currency = await repository.LoadMaintenanceCurrencyAsync(
                status.Branch,
                status.Level,
                asOf,
                cancellationToken).ConfigureAwait(false);
            results.Add(currency);
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
        var candidate = DefaultTrainingWorkPolicy.Select(currentState);
        if (candidate is not null)
        {
            var sessionType = forcedSessionType ??
                DefaultTrainingWorkPolicy.SessionTypeFor(
                    candidate.WeeklyWork.Session,
                    candidate.Status.State,
                    candidate.Status.Level);
            return new RequestedTrainingWork(
                candidate.Status.Branch,
                candidate.Status.Level,
                PrimaryDrillFor(candidate.Status.Branch, candidate.Status.Level),
                sessionType);
        }

        return new RequestedTrainingWork(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            forcedSessionType ?? AppTrainingSessionType.Practice);
    }

    private static SelectedTrainingWork WorkFromMaintenanceRecord(
        LocalDueMaintenanceRecord dueMaintenance)
    {
        return WorkFromRequest(
            new RequestedTrainingWork(
                dueMaintenance.BranchLevel.Branch,
                dueMaintenance.BranchLevel.Level,
                PrimaryDrillFor(dueMaintenance.BranchLevel.Branch, dueMaintenance.BranchLevel.Level),
                AppTrainingSessionType.Maintenance),
            advancementWorkAllowed: false);
    }

    private static SelectedTrainingWork RecoveryWorkFor(
        RequestedTrainingWork requested,
        bool advancementWorkAllowed,
        IReadOnlyList<LocalSessionHistoryRecord> recentSessions)
    {
        return WorkFromRequest(
            new RequestedTrainingWork(
                requested.Branch,
                requested.Level,
                requested.Drill,
                AppTrainingSessionType.Recovery,
                requested.LoadVariables),
            advancementWorkAllowed,
            recentSessions);
    }

    private static SelectedTrainingWork WorkFromRequest(
        RequestedTrainingWork requested,
        bool advancementWorkAllowed,
        IReadOnlyList<LocalSessionHistoryRecord>? recentSessions = null)
    {
        var standard = StandardFor(requested.Branch, requested.Level);
        var drill = DrillFor(requested.Drill);
        var loadVariables = requested.LoadVariables.Count == 0
            ? ProgressiveLoadPlanner.Prescribe(
                TrainingLoadProfileCatalog.Get(requested.Branch, requested.Level),
                DefaultTrainingWorkPolicy.CoreSessionTypeFor(requested.SessionType),
                LoadHistoryFor(requested, recentSessions ?? [])).Stage.LoadVariables
            : requested.LoadVariables;

        return new SelectedTrainingWork(
            requested.Branch,
            requested.Level,
            requested.Drill,
            ExecutableTrainingStandards.HonestSessionType(
                requested.Branch,
                requested.Level,
                requested.Drill,
                requested.SessionType),
            standard.Demand,
            standard.Standard,
            drill.HonestyConstraint,
            loadVariables,
            advancementWorkAllowed);
    }

    private static IReadOnlyList<TrainingLoadHistoryEntry> LoadHistoryFor(
        RequestedTrainingWork requested,
        IEnumerable<LocalSessionHistoryRecord> sessions)
    {
        return sessions
            .Where(session =>
                (session.SessionType is LocalCompletedSessionType.Practice or
                    LocalCompletedSessionType.Load) &&
                session.Drill == requested.Drill &&
                session.BranchLevels.Contains(new LocalSessionBranchLevel(
                    requested.Branch,
                    requested.Level)))
            .OrderBy(session => session.Date.Year)
            .ThenBy(session => session.Date.Month)
            .ThenBy(session => session.Date.Day)
            .Select(session => new TrainingLoadHistoryEntry(
                session.LoadVariables,
                session.CleanPerformance,
                Overload: !session.CleanPerformance))
            .ToArray();
    }

    private static BranchLevelStandard StandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards.Single(standard => standard.Branch == branch && standard.Level == level);
    }

    private static DrillDefinition DrillFor(DrillId drill)
    {
        return ProgramCatalog.Drills.Single(definition => definition.Id == drill);
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

    private static DrillId PrimaryDrillFor(BranchCode branch, GlobalLevelId level)
    {
        return DefaultTrainingWorkPolicy.PrimaryDrillFor(branch, level);
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
