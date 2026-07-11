using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public enum LocalProgressSummarySourceKind
{
    PractitionerState,
    CompletedSession,
    FormalTestAttempt,
    MaintenanceCheck,
    EvidenceArtifact,
}

public enum LocalProgressBlockerKind
{
    DecayedBranch,
    MaintenanceDue,
    MaintenanceWarning,
    MaintenanceFailed,
}

public enum LocalProgressEmphasisKind
{
    RestoreDecayedBranch,
    ResolveMaintenanceBlocker,
    EmphasizeBottleneckBranch,
    ContinueActiveTraining,
    ContinueMaintenance,
}

public sealed record LocalBranchProgressSummary(
    BranchCode Branch,
    GlobalLevelId? HighestOwnedLevel,
    BranchLevelState? StateAtHighestOwnedLevel);

public sealed record LocalMaintenanceProgressSummary(
    BranchCode Branch,
    GlobalLevelId OwnedLevel,
    MaintenanceCurrencyState State,
    string? SourceCheckId,
    int? DaysSinceLastPassingCheck,
    int ConsecutiveFailures);

public sealed record LocalProgressBlocker(
    LocalProgressBlockerKind Kind,
    BranchCode Branch,
    GlobalLevelId? Level,
    string? SourceRecordId);

public sealed record LocalProgrammedEmphasis(
    LocalProgressEmphasisKind Kind,
    BranchCode? Branch,
    GlobalLevelId? Level);

public sealed class LocalProgressSummarySourceReference
{
    public LocalProgressSummarySourceReference(
        LocalProgressSummarySourceKind kind,
        string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Progress summary source id is required.", nameof(sourceId));
        }

        Kind = kind;
        SourceId = sourceId;
    }

    public LocalProgressSummarySourceKind Kind { get; }

    public string SourceId { get; }

    public bool Equals(LocalProgressSummarySourceReference? other)
    {
        return other is not null &&
            Kind == other.Kind &&
            string.Equals(SourceId, other.SourceId, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as LocalProgressSummarySourceReference);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Kind, SourceId);
    }
}

public sealed class LocalProgressSummaryRefreshRequest
{
    public LocalProgressSummaryRefreshRequest(
        string summaryId,
        TrainingDate generatedOn,
        TrainingDate periodStart,
        TrainingDate periodEnd)
    {
        if (string.IsNullOrWhiteSpace(summaryId))
        {
            throw new ArgumentException("Progress summary id is required.", nameof(summaryId));
        }

        if (periodStart.DaysUntil(periodEnd) < 0)
        {
            throw new ArgumentException(
                "Progress summary period end must not be before the period start.",
                nameof(periodEnd));
        }

        SummaryId = summaryId;
        GeneratedOn = generatedOn;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
    }

    public string SummaryId { get; }

    public TrainingDate GeneratedOn { get; }

    public TrainingDate PeriodStart { get; }

    public TrainingDate PeriodEnd { get; }
}

public sealed class LocalProgressSummaryRecord
{
    public LocalProgressSummaryRecord(
        string summaryId,
        TrainingDate generatedOn,
        TrainingDate periodStart,
        TrainingDate periodEnd,
        bool isAuthoritative,
        IEnumerable<LocalBranchProgressSummary> branchSummaries,
        IEnumerable<LocalMaintenanceProgressSummary> maintenanceSummaries,
        IEnumerable<LocalProgressBlocker> activeBlockers,
        BranchCode? bottleneckBranch,
        LocalProgrammedEmphasis nextProgrammedEmphasis,
        int completedSessionCount,
        int formalAttemptCount,
        int evidenceArtifactCount,
        IEnumerable<LocalProgressSummarySourceReference> sourceReferences)
    {
        if (string.IsNullOrWhiteSpace(summaryId))
        {
            throw new ArgumentException("Progress summary id is required.", nameof(summaryId));
        }

        if (periodStart.DaysUntil(periodEnd) < 0)
        {
            throw new ArgumentException(
                "Progress summary period end must not be before the period start.",
                nameof(periodEnd));
        }

        if (isAuthoritative)
        {
            throw new ArgumentException(
                "Progress summaries are non-authoritative read models and cannot be stored as progression truth.",
                nameof(isAuthoritative));
        }

        ArgumentNullException.ThrowIfNull(branchSummaries);
        ArgumentNullException.ThrowIfNull(maintenanceSummaries);
        ArgumentNullException.ThrowIfNull(activeBlockers);
        ArgumentNullException.ThrowIfNull(nextProgrammedEmphasis);
        ArgumentNullException.ThrowIfNull(sourceReferences);

        var branchSummaryArray = branchSummaries.ToArray();
        if (branchSummaryArray.Any(summary =>
                summary.HighestOwnedLevel.HasValue != summary.StateAtHighestOwnedLevel.HasValue))
        {
            throw new ArgumentException(
                "Branch progress summaries must provide owned level and state together.",
                nameof(branchSummaries));
        }

        var sourceReferenceArray = sourceReferences.Distinct().ToArray();
        if (sourceReferenceArray.Length == 0)
        {
            throw new ArgumentException(
                "Progress summaries must reference the persisted facts they were derived from.",
                nameof(sourceReferences));
        }

        if (completedSessionCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completedSessionCount));
        }

        if (formalAttemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(formalAttemptCount));
        }

        if (evidenceArtifactCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceArtifactCount));
        }

        SummaryId = summaryId;
        GeneratedOn = generatedOn;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        IsAuthoritative = isAuthoritative;
        BranchSummaries = Array.AsReadOnly(branchSummaryArray);
        MaintenanceSummaries = Array.AsReadOnly(maintenanceSummaries.ToArray());
        ActiveBlockers = Array.AsReadOnly(activeBlockers.ToArray());
        BottleneckBranch = bottleneckBranch;
        NextProgrammedEmphasis = nextProgrammedEmphasis;
        CompletedSessionCount = completedSessionCount;
        FormalAttemptCount = formalAttemptCount;
        EvidenceArtifactCount = evidenceArtifactCount;
        SourceReferences = Array.AsReadOnly(sourceReferenceArray);
    }

    public string SummaryId { get; }

    public TrainingDate GeneratedOn { get; }

    public TrainingDate PeriodStart { get; }

    public TrainingDate PeriodEnd { get; }

    public bool IsAuthoritative { get; }

    public IReadOnlyList<LocalBranchProgressSummary> BranchSummaries { get; }

    public IReadOnlyList<LocalMaintenanceProgressSummary> MaintenanceSummaries { get; }

    public IReadOnlyList<LocalProgressBlocker> ActiveBlockers { get; }

    public BranchCode? BottleneckBranch { get; }

    public LocalProgrammedEmphasis NextProgrammedEmphasis { get; }

    public int CompletedSessionCount { get; }

    public int FormalAttemptCount { get; }

    public int EvidenceArtifactCount { get; }

    public IReadOnlyList<LocalProgressSummarySourceReference> SourceReferences { get; }
}

public sealed class LocalProgressSummaryStore
{
    private const string ProgressSummariesPropertyName = "ProgressSummaries";
    private const string SummaryIdPropertyName = "SummaryId";
    private const string GeneratedOnPropertyName = "GeneratedOn";
    private const string PeriodStartPropertyName = "PeriodStart";
    private const string PeriodEndPropertyName = "PeriodEnd";
    private const string YearPropertyName = "Year";
    private const string MonthPropertyName = "Month";
    private const string DayPropertyName = "Day";
    private const string IsAuthoritativePropertyName = "IsAuthoritative";
    private const string BranchSummariesPropertyName = "BranchSummaries";
    private const string BranchPropertyName = "Branch";
    private const string HighestOwnedLevelPropertyName = "HighestOwnedLevel";
    private const string StateAtHighestOwnedLevelPropertyName = "StateAtHighestOwnedLevel";
    private const string MaintenanceSummariesPropertyName = "MaintenanceSummaries";
    private const string OwnedLevelPropertyName = "OwnedLevel";
    private const string MaintenanceStatePropertyName = "MaintenanceState";
    private const string SourceCheckIdPropertyName = "SourceCheckId";
    private const string DaysSinceLastPassingCheckPropertyName = "DaysSinceLastPassingCheck";
    private const string ConsecutiveFailuresPropertyName = "ConsecutiveFailures";
    private const string ActiveBlockersPropertyName = "ActiveBlockers";
    private const string KindPropertyName = "Kind";
    private const string LevelPropertyName = "Level";
    private const string SourceRecordIdPropertyName = "SourceRecordId";
    private const string BottleneckBranchPropertyName = "BottleneckBranch";
    private const string NextProgrammedEmphasisPropertyName = "NextProgrammedEmphasis";
    private const string CompletedSessionCountPropertyName = "CompletedSessionCount";
    private const string FormalAttemptCountPropertyName = "FormalAttemptCount";
    private const string EvidenceArtifactCountPropertyName = "EvidenceArtifactCount";
    private const string SourceReferencesPropertyName = "SourceReferences";
    private const string SourceKindPropertyName = "SourceKind";
    private const string SourceIdPropertyName = "SourceId";
    private const string PractitionerStateSourceId = "PractitionerState";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly IStableDomainIdentifierMap<LocalProgressSummarySourceKind> SourceKinds =
        new StableDomainIdentifierMap<LocalProgressSummarySourceKind>(new Dictionary<LocalProgressSummarySourceKind, string>
        {
            [LocalProgressSummarySourceKind.PractitionerState] = "PractitionerState",
            [LocalProgressSummarySourceKind.CompletedSession] = "CompletedSession",
            [LocalProgressSummarySourceKind.FormalTestAttempt] = "FormalTestAttempt",
            [LocalProgressSummarySourceKind.MaintenanceCheck] = "MaintenanceCheck",
            [LocalProgressSummarySourceKind.EvidenceArtifact] = "EvidenceArtifact",
        });

    private static readonly IStableDomainIdentifierMap<LocalProgressBlockerKind> BlockerKinds =
        new StableDomainIdentifierMap<LocalProgressBlockerKind>(new Dictionary<LocalProgressBlockerKind, string>
        {
            [LocalProgressBlockerKind.DecayedBranch] = "DecayedBranch",
            [LocalProgressBlockerKind.MaintenanceDue] = "MaintenanceDue",
            [LocalProgressBlockerKind.MaintenanceWarning] = "MaintenanceWarning",
            [LocalProgressBlockerKind.MaintenanceFailed] = "MaintenanceFailed",
        });

    private static readonly IStableDomainIdentifierMap<LocalProgressEmphasisKind> EmphasisKinds =
        new StableDomainIdentifierMap<LocalProgressEmphasisKind>(new Dictionary<LocalProgressEmphasisKind, string>
        {
            [LocalProgressEmphasisKind.RestoreDecayedBranch] = "RestoreDecayedBranch",
            [LocalProgressEmphasisKind.ResolveMaintenanceBlocker] = "ResolveMaintenanceBlocker",
            [LocalProgressEmphasisKind.EmphasizeBottleneckBranch] = "EmphasizeBottleneckBranch",
            [LocalProgressEmphasisKind.ContinueActiveTraining] = "ContinueActiveTraining",
            [LocalProgressEmphasisKind.ContinueMaintenance] = "ContinueMaintenance",
        });

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;
    private readonly LocalPractitionerStateStore practitionerStateStore;
    private readonly LocalSessionHistoryStore sessionHistoryStore;
    private readonly LocalFormalTestAttemptStore formalTestAttemptStore;
    private readonly LocalMaintenanceCheckStore maintenanceCheckStore;
    private readonly LocalEvidenceArtifactStore evidenceArtifactStore;

    public LocalProgressSummaryStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
        practitionerStateStore = new LocalPractitionerStateStore(options);
        sessionHistoryStore = new LocalSessionHistoryStore(options);
        formalTestAttemptStore = new LocalFormalTestAttemptStore(options);
        maintenanceCheckStore = new LocalMaintenanceCheckStore(options);
        evidenceArtifactStore = new LocalEvidenceArtifactStore(options);
    }

    public async ValueTask<LocalProgressSummaryRecord> RefreshAsync(
        LocalProgressSummaryRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var practitionerState = await practitionerStateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (practitionerState is null)
        {
            throw new InvalidOperationException(
                "Progress summaries require persisted practitioner state as an authoritative source.");
        }

        var sessions = (await sessionHistoryStore.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(record => IsInPeriod(record.Date, request.PeriodStart, request.PeriodEnd))
            .ToArray();
        var attempts = (await formalTestAttemptStore.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(record => IsInPeriod(record.Attempt.Date, request.PeriodStart, request.PeriodEnd))
            .ToArray();
        var allMaintenanceChecks = await maintenanceCheckStore.ListMaintenanceAsync(cancellationToken)
            .ConfigureAwait(false);
        var maintenanceChecks = allMaintenanceChecks
            .Where(record => IsOnOrBefore(record.Evidence.Date, request.GeneratedOn))
            .ToArray();
        var artifacts = (await evidenceArtifactStore.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(record => IsInPeriod(record.Artifact.Date, request.PeriodStart, request.PeriodEnd))
            .ToArray();

        var branchSummaries = CreateBranchSummaries(practitionerState);
        var maintenanceSummaries = CreateMaintenanceSummaries(
            branchSummaries,
            maintenanceChecks,
            request.GeneratedOn);
        var activeBlockers = CreateActiveBlockers(
            practitionerState,
            maintenanceSummaries);
        var bottleneckBranch = FindBottleneckBranch(attempts);
        var nextProgrammedEmphasis = ChooseNextProgrammedEmphasis(
            practitionerState,
            activeBlockers,
            bottleneckBranch);
        var sourceReferences = CreateSourceReferences(
            sessions,
            attempts,
            maintenanceChecks,
            artifacts);

        var summary = new LocalProgressSummaryRecord(
            request.SummaryId,
            request.GeneratedOn,
            request.PeriodStart,
            request.PeriodEnd,
            isAuthoritative: false,
            branchSummaries,
            maintenanceSummaries,
            activeBlockers,
            bottleneckBranch,
            nextProgrammedEmphasis,
            sessions.Length,
            attempts.Length,
            artifacts.Length,
            sourceReferences);

        await SaveAsync(summary, cancellationToken).ConfigureAwait(false);
        return summary;
    }

    public async ValueTask SaveAsync(
        LocalProgressSummaryRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var summaries = ReadSummaryArray(document);
        var replacementIndex = FindSummaryIndex(summaries, record.SummaryId);

        if (replacementIndex >= 0)
        {
            summaries[replacementIndex] = WriteRecord(record);
        }
        else
        {
            summaries.AddNode(WriteRecord(record));
        }

        document[ProgressSummariesPropertyName] = summaries;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalProgressSummaryRecord?> LoadAsync(
        string summaryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(summaryId))
        {
            throw new ArgumentException("Progress summary id is required.", nameof(summaryId));
        }

        var summaries = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return summaries.FirstOrDefault(record => record.SummaryId == summaryId);
    }

    public async ValueTask<LocalProgressSummaryRecord?> LoadLatestAsync(
        CancellationToken cancellationToken = default)
    {
        var summaries = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return summaries.LastOrDefault();
    }

    public ValueTask<IReadOnlyList<LocalProgressSummaryRecord>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadRecordsAsync(cancellationToken);
    }

    private async ValueTask<IReadOnlyList<LocalProgressSummaryRecord>> ReadRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadSummaryArray(document)
            .Select(ReadRecord)
            .OrderBy(record => record.GeneratedOn.Year)
            .ThenBy(record => record.GeneratedOn.Month)
            .ThenBy(record => record.GeneratedOn.Day)
            .ThenBy(record => record.SummaryId, StringComparer.Ordinal)
            .ToArray();
    }

    private async ValueTask<JsonObject> ReadInitializedDocumentAsync(CancellationToken cancellationToken)
    {
        await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var stream = new FileStream(
            options.DatabasePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var document = await LocalJsonDocumentIO.ReadObjectAsync(stream, cancellationToken)
            .ConfigureAwait(false);

        if (document is null ||
            !document.TryGetPropertyValue("Kind", out var kindNode) ||
            kindNode?.GetValue<string>() != LocalDatabaseSchema.MetadataKind)
        {
            throw new InvalidOperationException("The local database metadata is missing or invalid.");
        }

        LocalDatabaseDocument.ReadSchemaVersion(document);
        return document;
    }

    private async ValueTask ReplaceDatabaseAsync(JsonObject document, CancellationToken cancellationToken)
    {
        var tempPath = $"{options.DatabasePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await WriteDocumentAsync(tempPath, document, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, options.DatabasePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async ValueTask WriteDocumentAsync(
        string path,
        JsonObject document,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await LocalJsonDocumentIO.WriteObjectAsync(stream, document, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<LocalBranchProgressSummary> CreateBranchSummaries(
        PractitionerState practitionerState)
    {
        return Enum.GetValues<BranchCode>()
            .Select(branch =>
            {
                var highestOwnedStatus = practitionerState.BranchLevels
                    .Where(status =>
                        status.Branch == branch &&
                        IsOwnedLikeState(status.State))
                    .OrderByDescending(status => status.Level)
                    .FirstOrDefault();

                return highestOwnedStatus == default
                    ? new LocalBranchProgressSummary(branch, null, null)
                    : new LocalBranchProgressSummary(
                        branch,
                        highestOwnedStatus.Level,
                        highestOwnedStatus.State);
            })
            .ToArray();
    }

    private static IReadOnlyList<LocalMaintenanceProgressSummary> CreateMaintenanceSummaries(
        IEnumerable<LocalBranchProgressSummary> branchSummaries,
        IReadOnlyList<LocalMaintenanceCheckRecord> maintenanceChecks,
        TrainingDate generatedOn)
    {
        var summaries = new List<LocalMaintenanceProgressSummary>();
        foreach (var branchSummary in branchSummaries)
        {
            if (branchSummary.HighestOwnedLevel is not { } ownedLevel)
            {
                continue;
            }

            var result = MaintenanceCurrencyEvaluator.Evaluate(
                new MaintenanceCurrencyRequest(
                    branchSummary.Branch,
                    ownedLevel,
                    generatedOn,
                    maintenanceChecks.Select(record => record.Evidence)));
            var latestCheck = maintenanceChecks
                .Where(record =>
                    record.Evidence.Branch == branchSummary.Branch &&
                    record.Evidence.OwnedLevel == ownedLevel &&
                    record.Evidence.Kind == result.Cadence.RequiredCheckKind &&
                    IsOnOrBefore(record.Evidence.Date, generatedOn))
                .OrderBy(record => record.Evidence.Date.Year)
                .ThenBy(record => record.Evidence.Date.Month)
                .ThenBy(record => record.Evidence.Date.Day)
                .ThenBy(record => record.CheckId, StringComparer.Ordinal)
                .LastOrDefault();

            summaries.Add(new LocalMaintenanceProgressSummary(
                branchSummary.Branch,
                ownedLevel,
                result.State,
                latestCheck?.CheckId,
                result.DaysSinceLastPassingCheck,
                result.ConsecutiveFailures));
        }

        return summaries;
    }

    private static IReadOnlyList<LocalProgressBlocker> CreateActiveBlockers(
        PractitionerState practitionerState,
        IEnumerable<LocalMaintenanceProgressSummary> maintenanceSummaries)
    {
        var blockers = new List<LocalProgressBlocker>();
        foreach (var status in practitionerState.BranchLevels
            .Where(status => status.State == BranchLevelState.Decayed)
            .OrderBy(status => status.Branch)
            .ThenBy(status => status.Level))
        {
            blockers.Add(new LocalProgressBlocker(
                LocalProgressBlockerKind.DecayedBranch,
                status.Branch,
                status.Level,
                PractitionerStateSourceId));
        }

        foreach (var maintenanceSummary in maintenanceSummaries)
        {
            var blockerKind = maintenanceSummary.State switch
            {
                MaintenanceCurrencyState.Due => LocalProgressBlockerKind.MaintenanceDue,
                MaintenanceCurrencyState.Warning => LocalProgressBlockerKind.MaintenanceWarning,
                MaintenanceCurrencyState.Failed => LocalProgressBlockerKind.MaintenanceFailed,
                _ => (LocalProgressBlockerKind?)null,
            };

            if (blockerKind is null)
            {
                continue;
            }

            blockers.Add(new LocalProgressBlocker(
                blockerKind.Value,
                maintenanceSummary.Branch,
                maintenanceSummary.OwnedLevel,
                maintenanceSummary.SourceCheckId));
        }

        return blockers
            .OrderBy(blocker => BlockerPriority(blocker.Kind))
            .ThenBy(blocker => blocker.Branch)
            .ThenBy(blocker => blocker.Level)
            .ToArray();
    }

    private static BranchCode? FindBottleneckBranch(
        IReadOnlyList<LocalFormalTestAttemptRecord> attempts)
    {
        return attempts
            .Where(record => record.Attempt.PassState == FormalTestPassState.Fail)
            .OrderBy(record => record.Attempt.Date.Year)
            .ThenBy(record => record.Attempt.Date.Month)
            .ThenBy(record => record.Attempt.Date.Day)
            .ThenBy(record => record.AttemptId, StringComparer.Ordinal)
            .LastOrDefault()
            ?.Attempt.Branch;
    }

    private static LocalProgrammedEmphasis ChooseNextProgrammedEmphasis(
        PractitionerState practitionerState,
        IReadOnlyList<LocalProgressBlocker> activeBlockers,
        BranchCode? bottleneckBranch)
    {
        var decayedBlocker = activeBlockers.FirstOrDefault(
            blocker => blocker.Kind == LocalProgressBlockerKind.DecayedBranch);
        if (decayedBlocker is not null)
        {
            return new LocalProgrammedEmphasis(
                LocalProgressEmphasisKind.RestoreDecayedBranch,
                decayedBlocker.Branch,
                decayedBlocker.Level);
        }

        var maintenanceBlocker = activeBlockers.FirstOrDefault(
            blocker => blocker.Kind is
                LocalProgressBlockerKind.MaintenanceFailed or
                LocalProgressBlockerKind.MaintenanceWarning or
                LocalProgressBlockerKind.MaintenanceDue);
        if (maintenanceBlocker is not null)
        {
            return new LocalProgrammedEmphasis(
                LocalProgressEmphasisKind.ResolveMaintenanceBlocker,
                maintenanceBlocker.Branch,
                maintenanceBlocker.Level);
        }

        if (bottleneckBranch is { } bottleneck)
        {
            return new LocalProgrammedEmphasis(
                LocalProgressEmphasisKind.EmphasizeBottleneckBranch,
                bottleneck,
                null);
        }

        var activeTraining = practitionerState.BranchLevels
            .Where(status => status.State is
                BranchLevelState.Training or
                BranchLevelState.TestReady or
                BranchLevelState.PassedOnce or
                BranchLevelState.Stabilizing)
            .OrderBy(status => status.Branch)
            .ThenBy(status => status.Level)
            .FirstOrDefault();
        if (activeTraining != default)
        {
            return new LocalProgrammedEmphasis(
                LocalProgressEmphasisKind.ContinueActiveTraining,
                activeTraining.Branch,
                activeTraining.Level);
        }

        return new LocalProgrammedEmphasis(
            LocalProgressEmphasisKind.ContinueMaintenance,
            null,
            null);
    }

    private static IReadOnlyList<LocalProgressSummarySourceReference> CreateSourceReferences(
        IEnumerable<LocalSessionHistoryRecord> sessions,
        IEnumerable<LocalFormalTestAttemptRecord> attempts,
        IEnumerable<LocalMaintenanceCheckRecord> maintenanceChecks,
        IEnumerable<LocalEvidenceArtifactRecord> artifacts)
    {
        var references = new List<LocalProgressSummarySourceReference>
        {
            new(LocalProgressSummarySourceKind.PractitionerState, PractitionerStateSourceId),
        };

        references.AddRange(sessions.Select(record =>
            new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.CompletedSession, record.SessionId)));
        references.AddRange(attempts.Select(record =>
            new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.FormalTestAttempt, record.AttemptId)));
        references.AddRange(maintenanceChecks.Select(record =>
            new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.MaintenanceCheck, record.CheckId)));
        references.AddRange(artifacts.Select(record =>
            new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.EvidenceArtifact, record.ArtifactId)));

        return references
            .Distinct()
            .OrderBy(reference => reference.Kind)
            .ThenBy(reference => reference.SourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsOwnedLikeState(BranchLevelState state)
    {
        return state is BranchLevelState.Owned or BranchLevelState.Maintenance or BranchLevelState.Decayed;
    }

    private static bool IsInPeriod(
        TrainingDate date,
        TrainingDate periodStart,
        TrainingDate periodEnd)
    {
        return periodStart.DaysUntil(date) >= 0 && date.DaysUntil(periodEnd) >= 0;
    }

    private static bool IsOnOrBefore(TrainingDate date, TrainingDate other)
    {
        return date.DaysUntil(other) >= 0;
    }

    private static int BlockerPriority(LocalProgressBlockerKind kind)
    {
        return kind switch
        {
            LocalProgressBlockerKind.DecayedBranch => 0,
            LocalProgressBlockerKind.MaintenanceFailed => 1,
            LocalProgressBlockerKind.MaintenanceWarning => 2,
            LocalProgressBlockerKind.MaintenanceDue => 3,
            _ => 4,
        };
    }

    private static JsonArray ReadSummaryArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(ProgressSummariesPropertyName, out var summariesNode) ||
            summariesNode is null)
        {
            return [];
        }

        if (summariesNode is JsonArray summaries)
        {
            return summaries;
        }

        throw new InvalidOperationException("The stored progress summaries are invalid.");
    }

    private static int FindSummaryIndex(JsonArray summaries, string summaryId)
    {
        for (var index = 0; index < summaries.Count; index++)
        {
            if (summaries[index] is JsonObject summaryObject &&
                summaryObject.TryGetPropertyValue(SummaryIdPropertyName, out var summaryIdNode) &&
                summaryIdNode?.GetValue<string>() == summaryId)
            {
                return index;
            }
        }

        return -1;
    }

    private static JsonObject WriteRecord(LocalProgressSummaryRecord record)
    {
        var recordObject = new JsonObject
        {
            [SummaryIdPropertyName] = record.SummaryId,
            [GeneratedOnPropertyName] = WriteDate(record.GeneratedOn),
            [PeriodStartPropertyName] = WriteDate(record.PeriodStart),
            [PeriodEndPropertyName] = WriteDate(record.PeriodEnd),
            [IsAuthoritativePropertyName] = record.IsAuthoritative,
            [BranchSummariesPropertyName] = WriteBranchSummaries(record.BranchSummaries),
            [MaintenanceSummariesPropertyName] = WriteMaintenanceSummaries(record.MaintenanceSummaries),
            [ActiveBlockersPropertyName] = WriteActiveBlockers(record.ActiveBlockers),
            [NextProgrammedEmphasisPropertyName] = WriteEmphasis(record.NextProgrammedEmphasis),
            [CompletedSessionCountPropertyName] = record.CompletedSessionCount,
            [FormalAttemptCountPropertyName] = record.FormalAttemptCount,
            [EvidenceArtifactCountPropertyName] = record.EvidenceArtifactCount,
            [SourceReferencesPropertyName] = WriteSourceReferences(record.SourceReferences),
        };

        if (record.BottleneckBranch is { } bottleneckBranch)
        {
            recordObject[BottleneckBranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(bottleneckBranch);
        }

        return recordObject;
    }

    private static JsonArray WriteBranchSummaries(IEnumerable<LocalBranchProgressSummary> branchSummaries)
    {
        var summaries = new JsonArray();
        foreach (var branchSummary in branchSummaries)
        {
            var summaryObject = new JsonObject
            {
                [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(branchSummary.Branch),
            };

            if (branchSummary.HighestOwnedLevel is { } highestOwnedLevel)
            {
                summaryObject[HighestOwnedLevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(highestOwnedLevel);
            }

            if (branchSummary.StateAtHighestOwnedLevel is { } stateAtHighestOwnedLevel)
            {
                summaryObject[StateAtHighestOwnedLevelPropertyName] = StableDomainIdentifiers.BranchLevelStates.ToPersistedId(stateAtHighestOwnedLevel);
            }

            summaries.AddNode(summaryObject);
        }

        return summaries;
    }

    private static JsonArray WriteMaintenanceSummaries(IEnumerable<LocalMaintenanceProgressSummary> maintenanceSummaries)
    {
        var summaries = new JsonArray();
        foreach (var maintenanceSummary in maintenanceSummaries)
        {
            var summaryObject = new JsonObject
            {
                [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(maintenanceSummary.Branch),
                [OwnedLevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(maintenanceSummary.OwnedLevel),
                [MaintenanceStatePropertyName] = StableDomainIdentifiers.MaintenanceStates.ToPersistedId(maintenanceSummary.State),
                [ConsecutiveFailuresPropertyName] = maintenanceSummary.ConsecutiveFailures,
            };

            if (maintenanceSummary.SourceCheckId is not null)
            {
                summaryObject[SourceCheckIdPropertyName] = maintenanceSummary.SourceCheckId;
            }

            if (maintenanceSummary.DaysSinceLastPassingCheck is { } daysSinceLastPassingCheck)
            {
                summaryObject[DaysSinceLastPassingCheckPropertyName] = daysSinceLastPassingCheck;
            }

            summaries.AddNode(summaryObject);
        }

        return summaries;
    }

    private static JsonArray WriteActiveBlockers(IEnumerable<LocalProgressBlocker> activeBlockers)
    {
        var blockers = new JsonArray();
        foreach (var blocker in activeBlockers)
        {
            var blockerObject = new JsonObject
            {
                [KindPropertyName] = BlockerKinds.ToPersistedId(blocker.Kind),
                [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(blocker.Branch),
            };

            if (blocker.Level is { } level)
            {
                blockerObject[LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(level);
            }

            if (blocker.SourceRecordId is not null)
            {
                blockerObject[SourceRecordIdPropertyName] = blocker.SourceRecordId;
            }

            blockers.AddNode(blockerObject);
        }

        return blockers;
    }

    private static JsonObject WriteEmphasis(LocalProgrammedEmphasis emphasis)
    {
        var emphasisObject = new JsonObject
        {
            [KindPropertyName] = EmphasisKinds.ToPersistedId(emphasis.Kind),
        };

        if (emphasis.Branch is { } branch)
        {
            emphasisObject[BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(branch);
        }

        if (emphasis.Level is { } level)
        {
            emphasisObject[LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(level);
        }

        return emphasisObject;
    }

    private static JsonArray WriteSourceReferences(IEnumerable<LocalProgressSummarySourceReference> sourceReferences)
    {
        var references = new JsonArray();
        foreach (var sourceReference in sourceReferences)
        {
            references.AddNode(new JsonObject
            {
                [SourceKindPropertyName] = SourceKinds.ToPersistedId(sourceReference.Kind),
                [SourceIdPropertyName] = sourceReference.SourceId,
            });
        }

        return references;
    }

    private static JsonObject WriteDate(TrainingDate date)
    {
        return new JsonObject
        {
            [YearPropertyName] = date.Year,
            [MonthPropertyName] = date.Month,
            [DayPropertyName] = date.Day,
        };
    }

    private static LocalProgressSummaryRecord ReadRecord(JsonNode? node)
    {
        if (node is not JsonObject recordObject)
        {
            throw new InvalidOperationException("The stored progress summary is invalid.");
        }

        return new LocalProgressSummaryRecord(
            ReadRequiredString(recordObject, SummaryIdPropertyName),
            ReadDate(ReadRequiredObject(recordObject, GeneratedOnPropertyName)),
            ReadDate(ReadRequiredObject(recordObject, PeriodStartPropertyName)),
            ReadDate(ReadRequiredObject(recordObject, PeriodEndPropertyName)),
            ReadRequiredBoolean(recordObject, IsAuthoritativePropertyName),
            ReadRequiredArray(recordObject, BranchSummariesPropertyName).Select(ReadBranchSummary),
            ReadRequiredArray(recordObject, MaintenanceSummariesPropertyName).Select(ReadMaintenanceSummary),
            ReadRequiredArray(recordObject, ActiveBlockersPropertyName).Select(ReadBlocker),
            ReadOptionalEnum(recordObject, BottleneckBranchPropertyName, StableDomainIdentifiers.Branches),
            ReadEmphasis(ReadRequiredObject(recordObject, NextProgrammedEmphasisPropertyName)),
            ReadRequiredInt32(recordObject, CompletedSessionCountPropertyName),
            ReadRequiredInt32(recordObject, FormalAttemptCountPropertyName),
            ReadRequiredInt32(recordObject, EvidenceArtifactCountPropertyName),
            ReadRequiredArray(recordObject, SourceReferencesPropertyName).Select(ReadSourceReference));
    }

    private static LocalBranchProgressSummary ReadBranchSummary(JsonNode? node)
    {
        if (node is not JsonObject summaryObject)
        {
            throw new InvalidOperationException("The stored branch progress summary is invalid.");
        }

        return new LocalBranchProgressSummary(
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(summaryObject, BranchPropertyName)),
            ReadOptionalEnum(summaryObject, HighestOwnedLevelPropertyName, StableDomainIdentifiers.Levels),
            ReadOptionalEnum(summaryObject, StateAtHighestOwnedLevelPropertyName, StableDomainIdentifiers.BranchLevelStates));
    }

    private static LocalMaintenanceProgressSummary ReadMaintenanceSummary(JsonNode? node)
    {
        if (node is not JsonObject summaryObject)
        {
            throw new InvalidOperationException("The stored maintenance progress summary is invalid.");
        }

        return new LocalMaintenanceProgressSummary(
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(summaryObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(summaryObject, OwnedLevelPropertyName)),
            StableDomainIdentifiers.MaintenanceStates.FromPersistedId(ReadRequiredString(summaryObject, MaintenanceStatePropertyName)),
            ReadOptionalString(summaryObject, SourceCheckIdPropertyName),
            ReadOptionalInt32(summaryObject, DaysSinceLastPassingCheckPropertyName),
            ReadRequiredInt32(summaryObject, ConsecutiveFailuresPropertyName));
    }

    private static LocalProgressBlocker ReadBlocker(JsonNode? node)
    {
        if (node is not JsonObject blockerObject)
        {
            throw new InvalidOperationException("The stored progress blocker is invalid.");
        }

        return new LocalProgressBlocker(
            BlockerKinds.FromPersistedId(ReadRequiredString(blockerObject, KindPropertyName)),
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(blockerObject, BranchPropertyName)),
            ReadOptionalEnum(blockerObject, LevelPropertyName, StableDomainIdentifiers.Levels),
            ReadOptionalString(blockerObject, SourceRecordIdPropertyName));
    }

    private static LocalProgrammedEmphasis ReadEmphasis(JsonObject emphasisObject)
    {
        return new LocalProgrammedEmphasis(
            EmphasisKinds.FromPersistedId(ReadRequiredString(emphasisObject, KindPropertyName)),
            ReadOptionalEnum(emphasisObject, BranchPropertyName, StableDomainIdentifiers.Branches),
            ReadOptionalEnum(emphasisObject, LevelPropertyName, StableDomainIdentifiers.Levels));
    }

    private static LocalProgressSummarySourceReference ReadSourceReference(JsonNode? node)
    {
        if (node is not JsonObject referenceObject)
        {
            throw new InvalidOperationException("The stored progress summary source reference is invalid.");
        }

        return new LocalProgressSummarySourceReference(
            SourceKinds.FromPersistedId(ReadRequiredString(referenceObject, SourceKindPropertyName)),
            ReadRequiredString(referenceObject, SourceIdPropertyName));
    }

    private static TrainingDate ReadDate(JsonObject dateObject)
    {
        return TrainingDate.From(
            ReadRequiredInt32(dateObject, YearPropertyName),
            ReadRequiredInt32(dateObject, MonthPropertyName),
            ReadRequiredInt32(dateObject, DayPropertyName));
    }

    private static JsonObject ReadRequiredObject(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonObject objectNode)
        {
            throw new InvalidOperationException($"The stored progress summary is missing {propertyName}.");
        }

        return objectNode;
    }

    private static JsonArray ReadRequiredArray(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonArray arrayNode)
        {
            throw new InvalidOperationException($"The stored progress summary is missing {propertyName}.");
        }

        return arrayNode;
    }

    private static string ReadRequiredString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored progress summary is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored progress summary has an invalid {propertyName}.",
                exception);
        }
    }

    private static string? ReadOptionalString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            return null;
        }

        return node.GetValue<string>();
    }

    private static int ReadRequiredInt32(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored progress summary is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored progress summary has an invalid {propertyName}.",
                exception);
        }
    }

    private static int? ReadOptionalInt32(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            return null;
        }

        return node.GetValue<int>();
    }

    private static bool ReadRequiredBoolean(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored progress summary is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<bool>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored progress summary has an invalid {propertyName}.",
                exception);
        }
    }

    private static TDomain? ReadOptionalEnum<TDomain>(
        JsonObject jsonObject,
        string propertyName,
        IStableDomainIdentifierMap<TDomain> map)
        where TDomain : struct, Enum
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            return null;
        }

        return map.FromPersistedId(node.GetValue<string>());
    }
}
