using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public sealed class LocalRecentSessionsQuery
{
    public LocalRecentSessionsQuery(
        TrainingDate asOf,
        int limit,
        BranchCode? branch = null,
        GlobalLevelId? level = null,
        LocalCompletedSessionType? sessionType = null)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Recent session limit must be positive.");
        }

        AsOf = asOf;
        Limit = limit;
        Branch = branch;
        Level = level;
        SessionType = sessionType;
    }

    public TrainingDate AsOf { get; }

    public int Limit { get; }

    public BranchCode? Branch { get; }

    public GlobalLevelId? Level { get; }

    public LocalCompletedSessionType? SessionType { get; }
}

public sealed class LocalEvidenceHistoryQuery
{
    public LocalEvidenceHistoryQuery(
        TrainingDate asOf,
        int limit,
        BranchCode? branch = null,
        GlobalLevelId? level = null,
        LocalProgrammingEventKind? eventKind = null)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Evidence history limit must be positive.");
        }

        AsOf = asOf;
        Limit = limit;
        Branch = branch;
        Level = level;
        EventKind = eventKind;
    }

    public TrainingDate AsOf { get; }

    public int Limit { get; }

    public BranchCode? Branch { get; }

    public GlobalLevelId? Level { get; }

    public LocalProgrammingEventKind? EventKind { get; }
}

public sealed class LocalProgressRecordsQuery
{
    public LocalProgressRecordsQuery(
        TrainingDate asOf,
        int limit,
        BranchCode? branch = null,
        GlobalLevelId? level = null)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Progress record limit must be positive.");
        }

        AsOf = asOf;
        Limit = limit;
        Branch = branch;
        Level = level;
    }

    public TrainingDate AsOf { get; }

    public int Limit { get; }

    public BranchCode? Branch { get; }

    public GlobalLevelId? Level { get; }
}

public sealed record LocalDueMaintenanceRecord(
    BranchLevelStatus BranchLevel,
    MaintenanceCurrencyResult Currency);

public sealed record LocalProgramReviewCadenceFacts(
    TrainingDate ProgramStartedOn,
    TrainingDate? LastCompletedReviewOn,
    bool? LastCompletedReviewPassed);

public sealed class LocalProgressRecords
{
    public LocalProgressRecords(
        IEnumerable<LocalFormalTestAttemptRecord> formalTestAttempts,
        IEnumerable<LocalStabilizationPassRecord> stabilizationPasses,
        IEnumerable<LocalMaintenanceCheckRecord> maintenanceChecks,
        IEnumerable<LocalDecayHistoryRecord> decayHistory,
        IEnumerable<LocalRestorationHistoryRecord> restorationHistory,
        LocalProgressSummaryRecord? latestSummary)
    {
        ArgumentNullException.ThrowIfNull(formalTestAttempts);
        ArgumentNullException.ThrowIfNull(stabilizationPasses);
        ArgumentNullException.ThrowIfNull(maintenanceChecks);
        ArgumentNullException.ThrowIfNull(decayHistory);
        ArgumentNullException.ThrowIfNull(restorationHistory);

        FormalTestAttempts = formalTestAttempts.ToArray();
        StabilizationPasses = stabilizationPasses.ToArray();
        MaintenanceChecks = maintenanceChecks.ToArray();
        DecayHistory = decayHistory.ToArray();
        RestorationHistory = restorationHistory.ToArray();
        LatestSummary = latestSummary;
    }

    public IReadOnlyList<LocalFormalTestAttemptRecord> FormalTestAttempts { get; }

    public IReadOnlyList<LocalStabilizationPassRecord> StabilizationPasses { get; }

    public IReadOnlyList<LocalMaintenanceCheckRecord> MaintenanceChecks { get; }

    public IReadOnlyList<LocalDecayHistoryRecord> DecayHistory { get; }

    public IReadOnlyList<LocalRestorationHistoryRecord> RestorationHistory { get; }

    public LocalProgressSummaryRecord? LatestSummary { get; }
}

public sealed class LocalProgramRepository
{
    private readonly LocalPractitionerStateStore practitionerStateStore;
    private readonly LocalSessionHistoryStore sessionHistoryStore;
    private readonly LocalEvidenceArtifactStore evidenceArtifactStore;
    private readonly LocalFormalTestAttemptStore formalTestAttemptStore;
    private readonly LocalStabilizationPassStore stabilizationPassStore;
    private readonly LocalMaintenanceCheckStore maintenanceCheckStore;
    private readonly LocalDecayRestorationHistoryStore decayRestorationHistoryStore;
    private readonly LocalGeneratedDrillInstanceStore generatedDrillInstanceStore;
    private readonly LocalProgressSummaryStore progressSummaryStore;
    private readonly LocalDailyTrainingPrescriptionStore dailyTrainingPrescriptionStore;

    public LocalProgramRepository(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        practitionerStateStore = new LocalPractitionerStateStore(options);
        sessionHistoryStore = new LocalSessionHistoryStore(options);
        evidenceArtifactStore = new LocalEvidenceArtifactStore(options);
        formalTestAttemptStore = new LocalFormalTestAttemptStore(options);
        stabilizationPassStore = new LocalStabilizationPassStore(options);
        maintenanceCheckStore = new LocalMaintenanceCheckStore(options);
        decayRestorationHistoryStore = new LocalDecayRestorationHistoryStore(options);
        generatedDrillInstanceStore = new LocalGeneratedDrillInstanceStore(options);
        progressSummaryStore = new LocalProgressSummaryStore(options);
        dailyTrainingPrescriptionStore = new LocalDailyTrainingPrescriptionStore(options);
    }

    public ValueTask<PractitionerState?> LoadCurrentStateAsync(
        CancellationToken cancellationToken = default)
    {
        return practitionerStateStore.LoadAsync(cancellationToken);
    }

    public async ValueTask<LocalProgramReviewCadenceFacts> LoadReviewCadenceFactsAsync(
        TrainingDate asOf,
        CancellationToken cancellationToken = default)
    {
        var prescriptions = await dailyTrainingPrescriptionStore.ListAsync(cancellationToken)
            .ConfigureAwait(false);
        var sessions = await sessionHistoryStore.ListAsync(cancellationToken).ConfigureAwait(false);
        var artifacts = await evidenceArtifactStore.ListAsync(cancellationToken).ConfigureAwait(false);
        var timelineDates = prescriptions
            .Where(record => OnOrBefore(record.Date, asOf))
            .Select(record => record.CycleAnchor)
            .Concat(sessions.Where(record => OnOrBefore(record.Date, asOf)).Select(record => record.Date))
            .Concat(artifacts.Where(record => OnOrBefore(record.Artifact.Date, asOf)).Select(record => record.Artifact.Date))
            .OrderBy(date => date.Year)
            .ThenBy(date => date.Month)
            .ThenBy(date => date.Day)
            .ToArray();
        var latestReview = artifacts
            .Where(record =>
                record.Artifact.Category == EvidenceArtifactCategory.GlobalReview &&
                OnOrBefore(record.Artifact.Date, asOf))
            .OrderByDescending(record => record.Artifact.Date.Year)
            .ThenByDescending(record => record.Artifact.Date.Month)
            .ThenByDescending(record => record.Artifact.Date.Day)
            .ThenBy(record => record.ArtifactId, StringComparer.Ordinal)
            .FirstOrDefault();
        var latestReviewSession = sessions
            .Where(record =>
                record.SessionType == LocalCompletedSessionType.Review &&
                OnOrBefore(record.Date, asOf))
            .OrderByDescending(record => record.Date.Year)
            .ThenByDescending(record => record.Date.Month)
            .ThenByDescending(record => record.Date.Day)
            .ThenBy(record => record.SessionId, StringComparer.Ordinal)
            .FirstOrDefault();
        var lastReviewOn = latestReviewSession is not null &&
            (latestReview is null || latestReview.Artifact.Date.DaysUntil(latestReviewSession.Date) >= 0)
                ? latestReviewSession.Date
                : latestReview?.Artifact.Date;
        var lastReviewPassed = latestReviewSession is not null &&
            lastReviewOn == latestReviewSession.Date
                ? latestReviewSession.CleanPerformance
                : (bool?)null;

        return new LocalProgramReviewCadenceFacts(
            timelineDates.FirstOrDefault(asOf),
            lastReviewOn,
            lastReviewPassed);
    }

    public async ValueTask<IReadOnlyList<LocalSessionHistoryRecord>> ListRecentSessionsAsync(
        LocalRecentSessionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sessions = await sessionHistoryStore.ListAsync(cancellationToken).ConfigureAwait(false);
        return sessions
            .Where(record => OnOrBefore(record.Date, query.AsOf))
            .Where(record => query.SessionType is null || record.SessionType == query.SessionType.Value)
            .Where(record => MatchesBranchLevel(record.BranchLevels, query.Branch, query.Level))
            .OrderByDescending(record => record.Date.Year)
            .ThenByDescending(record => record.Date.Month)
            .ThenByDescending(record => record.Date.Day)
            .ThenBy(record => record.SessionId, StringComparer.Ordinal)
            .Take(query.Limit)
            .ToArray();
    }

    public async ValueTask<IReadOnlyList<LocalEvidenceArtifactRecord>> ListEvidenceHistoryAsync(
        LocalEvidenceHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var artifacts = await evidenceArtifactStore.ListAsync(cancellationToken).ConfigureAwait(false);
        return artifacts
            .Where(record => OnOrBefore(record.Artifact.Date, query.AsOf))
            .Where(record => query.EventKind is null || record.Event.Kind == query.EventKind.Value)
            .Where(record => MatchesBranchLevel(record.Event, query.Branch, query.Level))
            .OrderByDescending(record => record.Artifact.Date.Year)
            .ThenByDescending(record => record.Artifact.Date.Month)
            .ThenByDescending(record => record.Artifact.Date.Day)
            .ThenBy(record => record.ArtifactId, StringComparer.Ordinal)
            .Take(query.Limit)
            .ToArray();
    }

    public async ValueTask<IReadOnlyList<LocalDueMaintenanceRecord>> ListDueMaintenanceAsync(
        TrainingDate asOf,
        CancellationToken cancellationToken = default)
    {
        var currentState = await practitionerStateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (currentState is null)
        {
            return [];
        }

        var due = new List<LocalDueMaintenanceRecord>();
        foreach (var branchLevel in MaintenanceScope.HighestEarnedByBranch(currentState))
        {
            var currency = await LoadMaintenanceCurrencyAsync(
                branchLevel.Branch,
                branchLevel.Level,
                asOf,
                cancellationToken).ConfigureAwait(false);

            if (currency.State != MaintenanceCurrencyState.Current)
            {
                due.Add(new LocalDueMaintenanceRecord(branchLevel, currency));
            }
        }

        return due
            .OrderBy(record => record.BranchLevel.Branch)
            .ThenBy(record => record.BranchLevel.Level)
            .ToArray();
    }

    public async ValueTask<MaintenanceCurrencyResult> LoadMaintenanceCurrencyAsync(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate asOf,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadMaintenanceCurrencyRequestAsync(
            branch,
            ownedLevel,
            asOf,
            cancellationToken).ConfigureAwait(false);
        return MaintenanceCurrencyEvaluator.Evaluate(request);
    }

    public async ValueTask<MaintenanceCurrencyRequest> LoadMaintenanceCurrencyRequestAsync(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate asOf,
        CancellationToken cancellationToken = default)
    {
        var request = await maintenanceCheckStore.LoadMaintenanceCurrencyRequestAsync(
            branch,
            ownedLevel,
            asOf,
            cancellationToken).ConfigureAwait(false);
        var stabilization = await stabilizationPassStore.ListByBranchLevelAsync(
            branch,
            ownedLevel,
            cancellationToken).ConfigureAwait(false);
        var cadence = MaintenanceCurrencyEvaluator.CadenceFor(branch, ownedLevel);
        var ownershipBaselines = stabilization
            .Where(record =>
                record.Evidence.IsCleanPass &&
                record.Evidence.Date.DaysUntil(asOf) >= 0)
            .Select(record => new MaintenanceCheckEvidence(
                branch,
                ownedLevel,
                record.Evidence.Date,
                cadence.RequiredCheckKind,
                record.Evidence.StandardEvaluationResult));

        return new MaintenanceCurrencyRequest(
            branch,
            ownedLevel,
            asOf,
            request.Checks.Concat(ownershipBaselines));
    }

    public async ValueTask<LocalProgressRecords> LoadProgressRecordsAsync(
        LocalProgressRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var attempts = await formalTestAttemptStore.ListAsync(cancellationToken).ConfigureAwait(false);
        var stabilizationPasses = await stabilizationPassStore.ListAsync(cancellationToken).ConfigureAwait(false);
        var maintenanceChecks = await maintenanceCheckStore.ListMaintenanceAsync(cancellationToken).ConfigureAwait(false);
        var decays = await decayRestorationHistoryStore.ListDecaysAsync(cancellationToken).ConfigureAwait(false);
        var restorations = await decayRestorationHistoryStore.ListRestorationsAsync(cancellationToken).ConfigureAwait(false);
        var summaries = await progressSummaryStore.ListAsync(cancellationToken).ConfigureAwait(false);

        return new LocalProgressRecords(
            FilterDated(
                attempts.Where(record => MatchesBranchLevel(record.Attempt.Branch, record.Attempt.Level, query.Branch, query.Level)),
                record => record.Attempt.Date,
                record => record.AttemptId,
                query.AsOf,
                query.Limit),
            FilterDated(
                stabilizationPasses.Where(record => MatchesBranchLevel(record.Evidence.Branch, record.Evidence.Level, query.Branch, query.Level)),
                record => record.Evidence.Date,
                record => record.PassId,
                query.AsOf,
                query.Limit),
            FilterDated(
                maintenanceChecks.Where(record => MatchesBranchLevel(record.Evidence.Branch, record.Evidence.OwnedLevel, query.Branch, query.Level)),
                record => record.Evidence.Date,
                record => record.CheckId,
                query.AsOf,
                query.Limit),
            FilterDated(
                decays.Where(record => MatchesBranchLevel(record.Branch, record.Level, query.Branch, query.Level)),
                record => record.Date,
                record => record.DecayId,
                query.AsOf,
                query.Limit),
            FilterDated(
                restorations.Where(record => MatchesBranchLevel(record.Branch, record.Level, query.Branch, query.Level)),
                record => record.Date,
                record => record.RestorationId,
                query.AsOf,
                query.Limit),
            summaries
                .Where(record => OnOrBefore(record.GeneratedOn, query.AsOf))
                .OrderByDescending(record => record.GeneratedOn.Year)
                .ThenByDescending(record => record.GeneratedOn.Month)
                .ThenByDescending(record => record.GeneratedOn.Day)
                .ThenBy(record => record.SummaryId, StringComparer.Ordinal)
                .FirstOrDefault());
    }

    public ValueTask<LocalGeneratedDrillInstanceRecord?> LoadGeneratedDrillInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        return generatedDrillInstanceStore.LoadAsync(instanceId, cancellationToken);
    }

    public ValueTask<IReadOnlyList<LocalGeneratedDrillInstanceRecord>> ListReusableGeneratedDrillInstancesAsync(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        IEnumerable<LoadVariable> loadVariables,
        CancellationToken cancellationToken = default)
    {
        return generatedDrillInstanceStore.ListReusableAsync(
            branch,
            level,
            drill,
            loadVariables,
            cancellationToken);
    }

    public ValueTask<IReadOnlyList<LocalGeneratedDrillInstanceRecord>> ListFreshEquivalentGeneratedDrillInstancesAsync(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        IEnumerable<LoadVariable> loadVariables,
        LocalGeneratedDrillContentIdentity contentIdentity,
        CancellationToken cancellationToken = default)
    {
        return generatedDrillInstanceStore.ListFreshEquivalentAsync(
            branch,
            level,
            drill,
            loadVariables,
            contentIdentity,
            cancellationToken);
    }

    private static bool IsMaintenanceRelevant(BranchLevelStatus status)
    {
        return status.State is
            BranchLevelState.Owned or
            BranchLevelState.Maintenance or
            BranchLevelState.Decayed;
    }

    private static bool MatchesBranchLevel(
        IReadOnlyList<LocalSessionBranchLevel> branchLevels,
        BranchCode? branch,
        GlobalLevelId? level)
    {
        return branch is null && level is null ||
            branchLevels.Any(branchLevel =>
                (!branch.HasValue || branchLevel.Branch == branch.Value) &&
                (!level.HasValue || branchLevel.Level == level.Value));
    }

    private static bool MatchesBranchLevel(
        LocalProgrammingEventReference eventReference,
        BranchCode? branch,
        GlobalLevelId? level)
    {
        return (!branch.HasValue || eventReference.Branch == branch.Value) &&
            (!level.HasValue || eventReference.Level == level.Value);
    }

    private static bool MatchesBranchLevel(
        BranchCode branch,
        GlobalLevelId level,
        BranchCode? requestedBranch,
        GlobalLevelId? requestedLevel)
    {
        return (!requestedBranch.HasValue || branch == requestedBranch.Value) &&
            (!requestedLevel.HasValue || level == requestedLevel.Value);
    }

    private static IReadOnlyList<TRecord> FilterDated<TRecord>(
        IEnumerable<TRecord> records,
        Func<TRecord, TrainingDate> dateSelector,
        Func<TRecord, string> stableIdSelector,
        TrainingDate asOf,
        int limit)
    {
        return records
            .Where(record => OnOrBefore(dateSelector(record), asOf))
            .OrderByDescending(record => dateSelector(record).Year)
            .ThenByDescending(record => dateSelector(record).Month)
            .ThenByDescending(record => dateSelector(record).Day)
            .ThenBy(record => stableIdSelector(record), StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static bool OnOrBefore(TrainingDate date, TrainingDate asOf)
    {
        return date.DaysUntil(asOf) >= 0;
    }
}
