using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App;

public sealed class CurrentTrainingStateQuery
{
    public CurrentTrainingStateQuery(
        TrainingDate asOf,
        int recentSessionLimit = 10,
        int evidenceSummaryLimit = 10,
        int progressRecordLimit = 10)
    {
        if (recentSessionLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recentSessionLimit), "Recent session limit must be positive.");
        }

        if (evidenceSummaryLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceSummaryLimit), "Evidence summary limit must be positive.");
        }

        if (progressRecordLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(progressRecordLimit), "Progress record limit must be positive.");
        }

        AsOf = asOf;
        RecentSessionLimit = recentSessionLimit;
        EvidenceSummaryLimit = evidenceSummaryLimit;
        ProgressRecordLimit = progressRecordLimit;
    }

    public TrainingDate AsOf { get; }

    public int RecentSessionLimit { get; }

    public int EvidenceSummaryLimit { get; }

    public int ProgressRecordLimit { get; }
}

public enum CurrentTrainingStateBlockerSource
{
    Category,
    WeeklyProgramming,
    DependencyCap,
    GlobalBalance,
}

public sealed record CurrentTrainingStateBlocker(
    CurrentTrainingStateBlockerSource Source,
    BranchCode? Branch,
    GlobalLevelId? Level,
    string Detail,
    PractitionerCategoryBlockerKind? CategoryBlockerKind = null,
    WeeklyProgrammingConstraintKind? WeeklyConstraintKind = null,
    DependencyCapReason? DependencyCapReason = null,
    GlobalBalanceIssueKind? GlobalBalanceIssueKind = null);

public sealed record CurrentTrainingStateNextWork(
    int DayNumber,
    WeeklySessionKind Session,
    IReadOnlyList<BranchCode> BranchEmphasis,
    bool IsAdvancementWork);

public sealed record CurrentGlobalReviewReadModel(
    GlobalReviewCadenceResult Cadence,
    GlobalReviewInput Input,
    GlobalReviewEvaluationResult Evaluation);

public sealed class CurrentTrainingStateReadModel
{
    public CurrentTrainingStateReadModel(
        PractitionerState currentPractitionerState,
        IEnumerable<BranchLevelStatus> branchLevelStates,
        IEnumerable<LocalDueMaintenanceRecord> dueMaintenance,
        IEnumerable<LocalSessionHistoryRecord> recentSessions,
        IEnumerable<LocalEvidenceArtifactRecord> evidenceSummaries,
        LocalProgressRecords progressRecords,
        PractitionerCategoryClassificationResult categoryClassification,
        WeeklyPlan weeklyPlan,
        IEnumerable<CurrentTrainingStateBlocker> blockedAdvancement,
        IEnumerable<CurrentTrainingStateNextWork> availableNextWork,
        CurrentGlobalReviewReadModel globalReview,
        RecoveryDecisionResult? recoveryDecision,
        DeloadDecisionResult deloadDecision,
        GlobalReviewResult? lastCompletedGlobalReview,
        ProgramCompletionResult programCompletion)
    {
        ArgumentNullException.ThrowIfNull(currentPractitionerState);
        ArgumentNullException.ThrowIfNull(branchLevelStates);
        ArgumentNullException.ThrowIfNull(dueMaintenance);
        ArgumentNullException.ThrowIfNull(recentSessions);
        ArgumentNullException.ThrowIfNull(evidenceSummaries);
        ArgumentNullException.ThrowIfNull(progressRecords);
        ArgumentNullException.ThrowIfNull(categoryClassification);
        ArgumentNullException.ThrowIfNull(weeklyPlan);
        ArgumentNullException.ThrowIfNull(blockedAdvancement);
        ArgumentNullException.ThrowIfNull(availableNextWork);
        ArgumentNullException.ThrowIfNull(globalReview);
        ArgumentNullException.ThrowIfNull(deloadDecision);
        ArgumentNullException.ThrowIfNull(programCompletion);

        CurrentPractitionerState = currentPractitionerState;
        BranchLevelStates = branchLevelStates.ToArray();
        DueMaintenance = dueMaintenance.ToArray();
        RecentSessions = recentSessions.ToArray();
        EvidenceSummaries = evidenceSummaries.ToArray();
        ProgressRecords = progressRecords;
        CategoryClassification = categoryClassification;
        WeeklyPlan = weeklyPlan;
        BlockedAdvancement = blockedAdvancement.ToArray();
        AvailableNextWork = availableNextWork.ToArray();
        GlobalReview = globalReview;
        RecoveryDecision = recoveryDecision;
        DeloadDecision = deloadDecision;
        LastCompletedGlobalReview = lastCompletedGlobalReview;
        ProgramCompletion = programCompletion;
    }

    public PractitionerState CurrentPractitionerState { get; }

    public IReadOnlyList<BranchLevelStatus> BranchLevelStates { get; }

    public IReadOnlyList<LocalDueMaintenanceRecord> DueMaintenance { get; }

    public IReadOnlyList<LocalSessionHistoryRecord> RecentSessions { get; }

    public IReadOnlyList<LocalEvidenceArtifactRecord> EvidenceSummaries { get; }

    public LocalProgressRecords ProgressRecords { get; }

    public PractitionerCategoryClassificationResult CategoryClassification { get; }

    public WeeklyPlan WeeklyPlan { get; }

    public IReadOnlyList<CurrentTrainingStateBlocker> BlockedAdvancement { get; }

    public IReadOnlyList<CurrentTrainingStateNextWork> AvailableNextWork { get; }

    public CurrentGlobalReviewReadModel GlobalReview { get; }

    public RecoveryDecisionResult? RecoveryDecision { get; }

    public DeloadDecisionResult DeloadDecision { get; }

    public GlobalReviewResult? LastCompletedGlobalReview { get; }

    public ProgramCompletionResult ProgramCompletion { get; }

    public bool RecoveryRequired =>
        RecoveryDecision?.ShouldRecover == true || DeloadDecision.ShouldDeload;
}

public sealed class CurrentTrainingStateLoader
{
    private static readonly IReadOnlyList<BranchCode> FoundationalBranches =
        ProgramCatalog.Branches
            .Where(branch => branch.Type == BranchType.Foundational)
            .Select(branch => branch.Code)
            .ToArray();

    private static readonly IReadOnlyList<BranchCode> AdvancedBranches =
        ProgramCatalog.Branches
            .Where(branch => branch.Type == BranchType.Advanced)
            .Select(branch => branch.Code)
            .ToArray();

    private readonly MentalGymnasticsAppStartup startup;
    private readonly LocalProgramRepository repository;

    public CurrentTrainingStateLoader(AppStartupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        startup = new MentalGymnasticsAppStartup(configuration);
        repository = new LocalProgramRepository(configuration.LocalDatabaseOptions);
    }

    public async ValueTask<CurrentTrainingStateReadModel> LoadAsync(
        CurrentTrainingStateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await startup.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var currentState = await repository.LoadCurrentStateAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("App startup completed without a practitioner state.");
        var maintenanceCurrency = await LoadMaintenanceCurrencyAsync(
            currentState,
            query.AsOf,
            cancellationToken).ConfigureAwait(false);
        var dueMaintenance = await repository.ListDueMaintenanceAsync(
            query.AsOf,
            cancellationToken).ConfigureAwait(false);
        var recentSessions = await repository.ListRecentSessionsAsync(
            new LocalRecentSessionsQuery(query.AsOf, query.RecentSessionLimit),
            cancellationToken).ConfigureAwait(false);
        var evidenceSummaries = await repository.ListEvidenceHistoryAsync(
            new LocalEvidenceHistoryQuery(query.AsOf, query.EvidenceSummaryLimit),
            cancellationToken).ConfigureAwait(false);
        var progressRecords = await repository.LoadProgressRecordsAsync(
            new LocalProgressRecordsQuery(query.AsOf, query.ProgressRecordLimit),
            cancellationToken).ConfigureAwait(false);
        var reviewCadenceFacts = await repository.LoadReviewCadenceFactsAsync(
            query.AsOf,
            cancellationToken).ConfigureAwait(false);

        var lastCompletedGlobalReview = PersistedGlobalReviewResultFactory.From(reviewCadenceFacts);
        var categoryClassification = PractitionerCategoryClassifier.Classify(
            new PractitionerCategoryClassificationRequest(
                currentState,
                maintenanceCurrency,
                lastCompletedGlobalReview));
        var pressure = PersistedProgrammingPressureEvaluator.Evaluate(
            query.AsOf,
            currentState,
            recentSessions,
            progressRecords);
        var reviewCadence = GlobalReviewCadenceEvaluator.Evaluate(new GlobalReviewCadenceRequest(
            query.AsOf,
            categoryClassification.Category,
            reviewCadenceFacts.ProgramStartedOn,
            reviewCadenceFacts.LastCompletedReviewOn));
        var provisionalWeeklyPlan = WeeklyProgrammingPlanner.Generate(
            BuildWeeklyProgrammingRequest(
                currentState,
                maintenanceCurrency,
                categoryClassification,
                progressRecords,
                globalReviewDecisions: [],
                recoveryRequired: pressure.RecoveryRequired));
        var provisionalGlobalReview = BuildGlobalReviewReadModel(
            query.AsOf,
            currentState,
            maintenanceCurrency,
            recentSessions,
            evidenceSummaries,
            progressRecords,
            categoryClassification,
            provisionalWeeklyPlan,
            reviewCadence,
            pauseTestsForDeload: pressure.DeloadDecision.ShouldDeload);
        var weeklyPlan = WeeklyProgrammingPlanner.Generate(
            BuildWeeklyProgrammingRequest(
                currentState,
                maintenanceCurrency,
                categoryClassification,
                progressRecords,
                reviewCadence.IsDue ? provisionalGlobalReview.Evaluation.Decisions : [],
                pressure.RecoveryRequired));
        var blockedAdvancement = BuildBlockedAdvancement(
            currentState,
            maintenanceCurrency,
            categoryClassification,
            weeklyPlan,
            lastCompletedGlobalReview);
        var availableNextWork = weeklyPlan.Days
            .Where(day => day.Session is not WeeklySessionKind.Off and not WeeklySessionKind.OffOrRecovery)
            .Select(day => new CurrentTrainingStateNextWork(
                day.DayNumber,
                day.Session,
                day.BranchEmphasis,
                day.IsAdvancementWork))
            .ToArray();
        var globalReview = BuildGlobalReviewReadModel(
            query.AsOf,
            currentState,
            maintenanceCurrency,
            recentSessions,
            evidenceSummaries,
            progressRecords,
            categoryClassification,
            weeklyPlan,
            reviewCadence,
            pauseTestsForDeload: pressure.DeloadDecision.ShouldDeload);

        return new CurrentTrainingStateReadModel(
            currentState,
            currentState.BranchLevels,
            dueMaintenance,
            recentSessions,
            evidenceSummaries,
            progressRecords,
            categoryClassification,
            weeklyPlan,
            blockedAdvancement,
            availableNextWork,
            globalReview,
            pressure.RecoveryDecision,
            pressure.DeloadDecision,
            lastCompletedGlobalReview,
            ProgramCompletionEvaluator.Evaluate(
                currentState,
                maintenanceCurrency,
                lastCompletedGlobalReview));
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

    internal static WeeklyProgrammingRequest BuildWeeklyProgrammingRequest(
        PractitionerState currentState,
        IReadOnlyList<MaintenanceCurrencyResult> maintenanceCurrency,
        PractitionerCategoryClassificationResult categoryClassification,
        LocalProgressRecords progressRecords,
        IReadOnlyList<GlobalReviewDecision> globalReviewDecisions,
        bool recoveryRequired)
    {
        var weakestFoundationalBranch = SelectWeakestFoundationalBranch(currentState);
        var selectedFoundationalLoadBranch = SelectSelectedFoundationalLoadBranch(
            currentState,
            weakestFoundationalBranch);
        var selectedAdvancedBranch = SelectSelectedAdvancedBranch(currentState);

        return new WeeklyProgrammingRequest(
            categoryClassification,
            maintenanceCurrency,
            globalReviewDecisions,
            recoveryRequired,
            selectedFoundationalLoadBranch,
            weakestFoundationalBranch,
            selectedAdvancedBranch,
            SelectPrerequisiteSupportBranch(selectedAdvancedBranch, weakestFoundationalBranch),
            eligibleAdvancementBranch: SelectEligibleAdvancementBranch(currentState, selectedFoundationalLoadBranch),
            bottleneckBranch: progressRecords.LatestSummary?.BottleneckBranch ?? weakestFoundationalBranch,
            recentlyPassedBranch: SelectRecentlyPassedBranch(currentState, selectedAdvancedBranch),
            transferBranch: SelectTransferBranch(currentState, selectedAdvancedBranch));
    }

    private static IReadOnlyList<CurrentTrainingStateBlocker> BuildBlockedAdvancement(
        PractitionerState currentState,
        IReadOnlyList<MaintenanceCurrencyResult> maintenanceCurrency,
        PractitionerCategoryClassificationResult categoryClassification,
        WeeklyPlan weeklyPlan,
        GlobalReviewResult? lastCompletedGlobalReview)
    {
        return
        [
            .. categoryClassification.Blockers.Select(blocker => new CurrentTrainingStateBlocker(
                CurrentTrainingStateBlockerSource.Category,
                blocker.Branch,
                blocker.Level,
                blocker.Detail,
                CategoryBlockerKind: blocker.Kind)),
            .. weeklyPlan.Constraints.Select(constraint => new CurrentTrainingStateBlocker(
                CurrentTrainingStateBlockerSource.WeeklyProgramming,
                constraint.Branch,
                Level: null,
                constraint.Detail,
                WeeklyConstraintKind: constraint.Kind)),
            .. AdvancedBranches.SelectMany(branch => DependencyCapEvaluator.Evaluate(
                    new DependencyCapRequest(branch, currentState, maintenanceCurrency)).Caps)
                .Select(cap => new CurrentTrainingStateBlocker(
                    CurrentTrainingStateBlockerSource.DependencyCap,
                    cap.PrerequisiteBranch,
                    cap.PrerequisiteLevel,
                    cap.Detail,
                    DependencyCapReason: cap.Reason)),
            .. ProgramCatalog.Branches.Select(branch => GlobalBalanceEvaluator.EvaluateAdvancement(
                    new GlobalBalanceAdvancementRequest(
                        branch.Code,
                        SelectTargetLevelForBalance(currentState, branch.Code),
                        currentState,
                        maintenanceCurrency,
                        lastCompletedGlobalReview)))
                .SelectMany(result => result.Issues)
                .Select(issue => new CurrentTrainingStateBlocker(
                    CurrentTrainingStateBlockerSource.GlobalBalance,
                    issue.Branch,
                    issue.Level,
                    issue.Detail,
                    GlobalBalanceIssueKind: issue.Kind)),
        ];
    }

    private static CurrentGlobalReviewReadModel BuildGlobalReviewReadModel(
        TrainingDate asOf,
        PractitionerState currentState,
        IReadOnlyList<MaintenanceCurrencyResult> maintenanceCurrency,
        IReadOnlyList<LocalSessionHistoryRecord> recentSessions,
        IReadOnlyList<LocalEvidenceArtifactRecord> evidenceSummaries,
        LocalProgressRecords progressRecords,
        PractitionerCategoryClassificationResult categoryClassification,
        WeeklyPlan weeklyPlan,
        GlobalReviewCadenceResult cadence,
        bool pauseTestsForDeload)
    {
        var input = new GlobalReviewInput(
            asOf,
            categoryClassification.Category,
            currentState,
            BuildGlobalReviewOwnedLevels(currentState),
            maintenanceCurrency,
            BuildGlobalReviewFailures(progressRecords),
            evidenceSummaries.Select(record => record.Artifact),
            BuildGlobalReviewBottleneck(progressRecords.LatestSummary, weeklyPlan),
            BuildGlobalReviewVolumeIntensityHistory(recentSessions),
            BuildGlobalReviewRecoveryHistory(recentSessions),
            BuildGlobalReviewAdvancements(progressRecords),
            pauseTestsForDeload,
            openAdvancedBranch: false,
            attemptTransferIntegrationTransfer: false);

        return new CurrentGlobalReviewReadModel(cadence, input, GlobalReviewEvaluator.Evaluate(input));
    }

    private static IReadOnlyList<GlobalReviewOwnedLevel> BuildGlobalReviewOwnedLevels(
        PractitionerState currentState)
    {
        return ProgramCatalog.Branches
            .Select(branch => new GlobalReviewOwnedLevel(
                branch.Code,
                currentState.BranchLevels
                    .Where(status =>
                        status.Branch == branch.Code &&
                        status.State is BranchLevelState.Owned
                            or BranchLevelState.Maintenance
                            or BranchLevelState.Decayed)
                    .OrderByDescending(status => LevelRank(status.Level))
                    .Select(status => (GlobalLevelId?)status.Level)
                    .FirstOrDefault()))
            .ToArray();
    }

    private static IReadOnlyList<ClassifiedFailure> BuildGlobalReviewFailures(
        LocalProgressRecords progressRecords)
    {
        return progressRecords.FormalTestAttempts
            .Where(record => record.Attempt.FailureType is not null)
            .OrderByDescending(record => record.Attempt.Date.Year)
            .ThenByDescending(record => record.Attempt.Date.Month)
            .ThenByDescending(record => record.Attempt.Date.Day)
            .Take(3)
            .Select(record => new ClassifiedFailure(
                record.Attempt.Branch,
                record.Attempt.Level,
                record.Attempt.FailureType!.Value,
                Array.Empty<FailureEvidenceSignal>()))
            .ToArray();
    }

    private static GlobalReviewBottleneckInput? BuildGlobalReviewBottleneck(
        LocalProgressSummaryRecord? latestSummary,
        WeeklyPlan weeklyPlan)
    {
        if (latestSummary?.BottleneckBranch is not { } bottleneckBranch ||
            BottleneckKindForBranch(bottleneckBranch) is not { } bottleneck)
        {
            return null;
        }

        var hasProgrammedResponse =
            latestSummary.NextProgrammedEmphasis.Branch == bottleneckBranch ||
            weeklyPlan.Days.Any(day => day.BranchEmphasis.Contains(bottleneckBranch));
        var needsEmphasis =
            latestSummary.NextProgrammedEmphasis.Kind == LocalProgressEmphasisKind.EmphasizeBottleneckBranch &&
            latestSummary.NextProgrammedEmphasis.Branch == bottleneckBranch;

        return new GlobalReviewBottleneckInput(
            bottleneckBranch,
            bottleneck,
            hasProgrammedResponse,
            needsEmphasis);
    }

    private static IReadOnlyList<GlobalReviewVolumeIntensityRecord> BuildGlobalReviewVolumeIntensityHistory(
        IReadOnlyList<LocalSessionHistoryRecord> recentSessions)
    {
        return recentSessions
            .SelectMany(session => session.BranchLevels.Select(branchLevel => new
            {
                branchLevel.Branch,
                session.Intensity,
            }))
            .GroupBy(item => (item.Branch, item.Intensity))
            .OrderBy(group => BranchOrder(group.Key.Branch))
            .ThenBy(group => group.Key.Intensity)
            .Select(group => new GlobalReviewVolumeIntensityRecord(
                group.Key.Branch,
                group.Count(),
                ToTrainingIntensity(group.Key.Intensity)))
            .ToArray();
    }

    private static IReadOnlyList<GlobalReviewRecoveryRecord> BuildGlobalReviewRecoveryHistory(
        IReadOnlyList<LocalSessionHistoryRecord> recentSessions)
    {
        return recentSessions
            .Where(session =>
                session.RecoveryMarked ||
                session.DeloadMarked ||
                session.SessionType == LocalCompletedSessionType.Recovery)
            .OrderByDescending(session => session.Date.Year)
            .ThenByDescending(session => session.Date.Month)
            .ThenByDescending(session => session.Date.Day)
            .Select(session => new GlobalReviewRecoveryRecord(session.Date, session.DeloadMarked))
            .ToArray();
    }

    private static IReadOnlyList<GlobalReviewAdvancementRecord> BuildGlobalReviewAdvancements(
        LocalProgressRecords progressRecords)
    {
        return progressRecords.FormalTestAttempts
            .Where(record => record.Attempt.PassState is not FormalTestPassState.Fail)
            .OrderByDescending(record => record.Attempt.Date.Year)
            .ThenByDescending(record => record.Attempt.Date.Month)
            .ThenByDescending(record => record.Attempt.Date.Day)
            .Select(record => new GlobalReviewAdvancementRecord(
                record.Attempt.Branch,
                record.Attempt.Level,
                advancedByParticipationAlone: false))
            .ToArray();
    }

    private static BottleneckKind? BottleneckKindForBranch(BranchCode branch)
    {
        return branch switch
        {
            BranchCode.FH => BottleneckKind.FocusHoldReturnAfterDrift,
            BranchCode.FS => BottleneckKind.FocusShiftRecovery,
            BranchCode.WM => BottleneckKind.WorkingMemoryEncodingFidelity,
            BranchCode.IR => BottleneckKind.InhibitionRuleFidelity,
            BranchCode.DE => BottleneckKind.DiscriminationAuditAccuracy,
            BranchCode.AI => BottleneckKind.AffectiveInterferencePressureStability,
            _ => null,
        };
    }

    private static TrainingIntensityKind ToTrainingIntensity(LocalSessionIntensity intensity)
    {
        return intensity switch
        {
            LocalSessionIntensity.Low => TrainingIntensityKind.Low,
            LocalSessionIntensity.Moderate => TrainingIntensityKind.Moderate,
            LocalSessionIntensity.High => TrainingIntensityKind.High,
            _ => throw new ArgumentOutOfRangeException(nameof(intensity), intensity, "Unsupported session intensity."),
        };
    }

    private static BranchCode SelectWeakestFoundationalBranch(PractitionerState currentState)
    {
        return FoundationalBranches
            .OrderBy(branch => HighestOwnedRank(currentState, branch))
            .ThenBy(branch => BranchOrder(branch))
            .First();
    }

    private static BranchCode SelectSelectedFoundationalLoadBranch(
        PractitionerState currentState,
        BranchCode fallback)
    {
        return FirstMatchingBranch(
            FoundationalBranches,
            branch => currentState.BranchLevels.Any(status =>
                status.Branch == branch &&
                status.State is BranchLevelState.Training
                    or BranchLevelState.TestReady
                    or BranchLevelState.PassedOnce
                    or BranchLevelState.Stabilizing))
            ?? fallback;
    }

    private static BranchCode SelectEligibleAdvancementBranch(
        PractitionerState currentState,
        BranchCode fallback)
    {
        return currentState.BranchLevels
            .Where(status =>
                FoundationalBranches.Contains(status.Branch) &&
                status.State is BranchLevelState.TestReady
                    or BranchLevelState.PassedOnce
                    or BranchLevelState.Stabilizing)
            .OrderBy(status => status.State is
                BranchLevelState.PassedOnce or BranchLevelState.Stabilizing ? 0 : 1)
            .ThenBy(status => status.Level)
            .ThenBy(status => BranchOrder(status.Branch))
            .Select(status => status.Branch)
            .FirstOrDefault(fallback);
    }

    private static BranchCode SelectSelectedAdvancedBranch(PractitionerState currentState)
    {
        return AdvancedBranches
            .Where(branch => currentState.BranchLevels.Any(status =>
                status.Branch == branch &&
                status.State != BranchLevelState.Unopened))
            .OrderBy(branch => HasActiveAdvancementState(currentState, branch) ? 0 : 1)
            .ThenBy(branch => HighestOwnedRank(currentState, branch))
            .ThenBy(BranchOrder)
            .FirstOrDefault(BranchCode.CO);
    }

    private static bool HasActiveAdvancementState(
        PractitionerState currentState,
        BranchCode branch)
    {
        return currentState.BranchLevels.Any(status =>
            status.Branch == branch &&
            status.State is
                BranchLevelState.Training or
                BranchLevelState.TestReady or
                BranchLevelState.PassedOnce or
                BranchLevelState.Stabilizing);
    }

    private static BranchCode SelectPrerequisiteSupportBranch(
        BranchCode selectedAdvancedBranch,
        BranchCode fallback)
    {
        return ProgramCatalog.BranchUnlocks
            .SingleOrDefault(unlock => unlock.Branch == selectedAdvancedBranch)
            ?.RequiredLevels
            .Select(requirement => requirement.Branch)
            .FirstOrDefault() ?? fallback;
    }

    private static BranchCode SelectRecentlyPassedBranch(
        PractitionerState currentState,
        BranchCode fallback)
    {
        return currentState.BranchLevels
            .Where(status => status.State is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing)
            .OrderByDescending(status => LevelRank(status.Level))
            .ThenBy(status => BranchOrder(status.Branch))
            .Select(status => status.Branch)
            .FirstOrDefault(fallback);
    }

    private static BranchCode SelectTransferBranch(
        PractitionerState currentState,
        BranchCode fallback)
    {
        if (currentState.BranchLevels.Any(status =>
                status.Branch == BranchCode.TI &&
                status.State != BranchLevelState.Unopened))
        {
            return BranchCode.TI;
        }

        return fallback;
    }

    private static GlobalLevelId SelectTargetLevelForBalance(
        PractitionerState currentState,
        BranchCode branch)
    {
        var highestOwnedRank = HighestOwnedRank(currentState, branch);
        var targetRank = Math.Clamp(highestOwnedRank + 1, 1, ProgramCatalog.GlobalLevels.Count);

        return (GlobalLevelId)(targetRank - 1);
    }

    private static int HighestOwnedRank(PractitionerState currentState, BranchCode branch)
    {
        return currentState.BranchLevels
            .Where(status =>
                status.Branch == branch &&
                status.State is BranchLevelState.Owned or BranchLevelState.Maintenance)
            .Select(status => LevelRank(status.Level))
            .DefaultIfEmpty(0)
            .Max();
    }

    private static int LevelRank(GlobalLevelId level)
    {
        return ((int)level) + 1;
    }

    private static int BranchOrder(BranchCode branch)
    {
        return ProgramCatalog.Branches
            .Select((definition, index) => (definition.Code, index))
            .Single(item => item.Code == branch)
            .index;
    }

    private static BranchCode? FirstMatchingBranch(
        IEnumerable<BranchCode> branches,
        Func<BranchCode, bool> predicate)
    {
        foreach (var branch in branches)
        {
            if (predicate(branch))
            {
                return branch;
            }
        }

        return null;
    }

    private static bool IsMaintenanceRelevant(BranchLevelStatus status)
    {
        return status.State is
            BranchLevelState.Owned or
            BranchLevelState.Maintenance or
            BranchLevelState.Decayed;
    }
}
