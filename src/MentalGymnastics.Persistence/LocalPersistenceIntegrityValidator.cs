using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public enum LocalPersistenceIntegrityIssueKind
{
    InvalidDocument,
    MissingRequiredRecord,
    InvalidReference,
    UnknownIdentifier,
    ImpossiblePersistedState,
    OrphanedEvidence,
}

public sealed record LocalPersistenceIntegrityIssue(
    LocalPersistenceIntegrityIssueKind Kind,
    string Section,
    string? RecordId,
    string Detail);

public sealed class LocalPersistenceIntegrityReport
{
    public LocalPersistenceIntegrityReport(IEnumerable<LocalPersistenceIntegrityIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        Issues = issues.ToArray();
    }

    public IReadOnlyList<LocalPersistenceIntegrityIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;
}

public sealed class LocalPersistenceIntegrityValidator
{
    private const string PractitionerStateSection = "PractitionerState";
    private const string BranchLevelsPropertyName = "BranchLevels";
    private const string EvidenceArtifactsSection = "EvidenceArtifacts";
    private const string CompletedSessionsSection = "CompletedSessions";
    private const string FormalTestAttemptsSection = "FormalTestAttempts";
    private const string StabilizationPassesSection = "StabilizationPasses";
    private const string MaintenanceChecksSection = "MaintenanceChecks";
    private const string RestorationChecksSection = "RestorationChecks";
    private const string DecayHistorySection = "DecayHistory";
    private const string RestorationHistorySection = "RestorationHistory";
    private const string GeneratedDrillInstancesSection = "GeneratedDrillInstances";
    private const string ActiveRuntimeSessionSnapshotsSection = "ActiveRuntimeSessionSnapshots";
    private const string ProgressSummariesSection = "ProgressSummaries";
    private const string DailyTrainingPrescriptionsSection = "DailyTrainingPrescriptions";

    private const string ArtifactIdPropertyName = "ArtifactId";
    private const string SessionIdPropertyName = "SessionId";
    private const string AttemptIdPropertyName = "AttemptId";
    private const string PassIdPropertyName = "PassId";
    private const string CheckIdPropertyName = "CheckId";
    private const string DecayIdPropertyName = "DecayId";
    private const string RestorationIdPropertyName = "RestorationId";
    private const string InstanceIdPropertyName = "InstanceId";
    private const string SummaryIdPropertyName = "SummaryId";
    private const string PrescriptionIdPropertyName = "PrescriptionId";

    private const string EvidenceArtifactIdPropertyName = "EvidenceArtifactId";
    private const string EvidenceArtifactIdsPropertyName = "EvidenceArtifactIds";
    private const string FormalTestAttemptIdPropertyName = "FormalTestAttemptId";
    private const string CompletedSessionIdPropertyName = "CompletedSessionId";
    private const string MaintenanceCheckIdsPropertyName = "MaintenanceCheckIds";
    private const string RestorationCheckIdsPropertyName = "RestorationCheckIds";
    private const string ActiveSessionIdPropertyName = "ActiveSessionId";
    private const string ResultEvidenceArtifactIdPropertyName = "ResultEvidenceArtifactId";
    private const string SourceCheckIdPropertyName = "SourceCheckId";
    private const string SourceRecordIdPropertyName = "SourceRecordId";
    private const string SourceReferencesPropertyName = "SourceReferences";
    private const string SourceKindPropertyName = "SourceKind";
    private const string SourceIdPropertyName = "SourceId";

    private const string EventPropertyName = "Event";
    private const string EventIdPropertyName = "EventId";
    private const string BranchPropertyName = "Branch";
    private const string LevelPropertyName = "Level";
    private const string StatePropertyName = "State";
    private const string CurrentStatusPropertyName = "CurrentStatus";
    private const string NextStatusPropertyName = "NextStatus";
    private const string TransitionPropertyName = "Transition";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly IStableDomainIdentifierMap<LocalGeneratedDrillInstanceState> GeneratedInstanceStates =
        new StableDomainIdentifierMap<LocalGeneratedDrillInstanceState>(new Dictionary<LocalGeneratedDrillInstanceState, string>
        {
            [LocalGeneratedDrillInstanceState.Reserved] = "Reserved",
            [LocalGeneratedDrillInstanceState.InSession] = "InSession",
            [LocalGeneratedDrillInstanceState.Completed] = "Completed",
            [LocalGeneratedDrillInstanceState.Abandoned] = "Abandoned",
        });

    private static readonly IStableDomainIdentifierMap<LocalProgressSummarySourceKind> ProgressSummarySourceKinds =
        new StableDomainIdentifierMap<LocalProgressSummarySourceKind>(new Dictionary<LocalProgressSummarySourceKind, string>
        {
            [LocalProgressSummarySourceKind.PractitionerState] = "PractitionerState",
            [LocalProgressSummarySourceKind.CompletedSession] = "CompletedSession",
            [LocalProgressSummarySourceKind.FormalTestAttempt] = "FormalTestAttempt",
            [LocalProgressSummarySourceKind.MaintenanceCheck] = "MaintenanceCheck",
            [LocalProgressSummarySourceKind.EvidenceArtifact] = "EvidenceArtifact",
        });

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalPersistenceIntegrityValidator(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask<LocalPersistenceIntegrityReport> ValidateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(options.DatabasePath))
        {
            await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        JsonObject document;
        try
        {
            document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or IOException)
        {
            return new LocalPersistenceIntegrityReport(
            [
                new LocalPersistenceIntegrityIssue(
                    LocalPersistenceIntegrityIssueKind.InvalidDocument,
                    "Database",
                    null,
                    exception.Message),
            ]);
        }

        return ValidateDocument(document);
    }

    internal static LocalPersistenceIntegrityReport ValidateDocument(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var issues = new List<LocalPersistenceIntegrityIssue>();
        ValidateKnownIdentifiers(document, "$", issues);

        var practitionerStateExists = ValidatePractitionerState(document, issues);

        var evidenceRecords = ReadRecordArray(document, EvidenceArtifactsSection, ArtifactIdPropertyName, issues);
        var sessionRecords = ReadRecordArray(document, CompletedSessionsSection, SessionIdPropertyName, issues);
        var attemptRecords = ReadRecordArray(document, FormalTestAttemptsSection, AttemptIdPropertyName, issues);
        var stabilizationRecords = ReadRecordArray(document, StabilizationPassesSection, PassIdPropertyName, issues);
        var maintenanceRecords = ReadRecordArray(document, MaintenanceChecksSection, CheckIdPropertyName, issues);
        var restorationCheckRecords = ReadRecordArray(document, RestorationChecksSection, CheckIdPropertyName, issues);
        var decayRecords = ReadRecordArray(document, DecayHistorySection, DecayIdPropertyName, issues);
        var restorationRecords = ReadRecordArray(document, RestorationHistorySection, RestorationIdPropertyName, issues);
        var generatedInstanceRecords = ReadRecordArray(document, GeneratedDrillInstancesSection, InstanceIdPropertyName, issues);
        var activeSnapshotRecords = ReadRecordArray(
            document,
            ActiveRuntimeSessionSnapshotsSection,
            SessionIdPropertyName,
            issues);
        var progressSummaryRecords = ReadRecordArray(document, ProgressSummariesSection, SummaryIdPropertyName, issues);
        var dailyPrescriptionRecords = ReadRecordArray(
            document,
            DailyTrainingPrescriptionsSection,
            PrescriptionIdPropertyName,
            issues);

        var evidenceIds = IndexRecords(evidenceRecords, issues);
        var sessionIds = IndexRecords(sessionRecords, issues);
        var attemptIds = IndexRecords(attemptRecords, issues);
        var stabilizationIds = IndexRecords(stabilizationRecords, issues);
        var maintenanceIds = IndexRecords(maintenanceRecords, issues);
        var restorationCheckIds = IndexRecords(restorationCheckRecords, issues);
        var decayIds = IndexRecords(decayRecords, issues);
        var restorationIds = IndexRecords(restorationRecords, issues);
        var generatedInstanceIds = IndexRecords(generatedInstanceRecords, issues);
        var activeSnapshotIds = IndexRecords(activeSnapshotRecords, issues);
        var progressSummaryIds = IndexRecords(progressSummaryRecords, issues);
        _ = IndexRecords(dailyPrescriptionRecords, issues);

        var referencedEvidenceIds = new HashSet<string>(StringComparer.Ordinal);
        var programmingEventIds = new HashSet<string>(StringComparer.Ordinal);
        AddIds(programmingEventIds, sessionIds);
        AddIds(programmingEventIds, attemptIds);
        AddIds(programmingEventIds, stabilizationIds);
        AddIds(programmingEventIds, maintenanceIds);
        AddIds(programmingEventIds, restorationCheckIds);
        AddIds(programmingEventIds, decayIds);
        AddIds(programmingEventIds, restorationIds);
        AddIds(programmingEventIds, generatedInstanceIds);
        AddIds(programmingEventIds, progressSummaryIds);

        ValidateSessionEvidenceReferences(sessionRecords, evidenceIds, referencedEvidenceIds, issues);
        ValidateAttemptReferences(attemptRecords, evidenceIds, referencedEvidenceIds, issues);
        ValidateStabilizationReferences(
            stabilizationRecords,
            evidenceIds,
            attemptIds,
            sessionIds,
            referencedEvidenceIds,
            issues);
        ValidateMaintenanceReferences(
            maintenanceRecords,
            evidenceIds,
            sessionIds,
            referencedEvidenceIds,
            issues);
        ValidateRestorationCheckReferences(
            restorationCheckRecords,
            evidenceIds,
            sessionIds,
            referencedEvidenceIds,
            issues);
        ValidateDecayReferences(decayRecords, maintenanceIds, issues);
        ValidateRestorationHistoryReferences(restorationRecords, restorationCheckIds, issues);
        var inProgressSessionIds = new HashSet<string>(activeSnapshotIds.Keys, StringComparer.Ordinal);
        try
        {
            foreach (var sessionId in LocalDailyTrainingPrescriptionStore.ReadFrom(document)
                .SelectMany(record => record.Blocks)
                .Where(block => block.State is
                    LocalDailyTrainingBlockState.Prepared or LocalDailyTrainingBlockState.Active)
                .Select(block => block.SessionId)
                .OfType<string>())
            {
                inProgressSessionIds.Add(sessionId);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // Daily prescription validation reports the malformed section separately.
        }

        ValidateGeneratedInstanceReferences(
            generatedInstanceRecords,
            evidenceIds,
            inProgressSessionIds,
            referencedEvidenceIds,
            issues);
        ValidateProgressSummaryReferences(
            progressSummaryRecords,
            practitionerStateExists,
            sessionIds,
            attemptIds,
            maintenanceIds,
            evidenceIds,
            referencedEvidenceIds,
            issues);
        ValidateProgressSummaryReadModelReferences(
            progressSummaryRecords,
            maintenanceIds,
            decayIds,
            restorationIds,
            issues);
        ValidateDailyTrainingPrescriptions(document, sessionIds, issues);
        ValidateStateHistoryTransitions(decayRecords, issues);
        ValidateStateHistoryTransitions(restorationRecords, issues);
        ValidateOrphanedEvidence(
            evidenceRecords,
            referencedEvidenceIds,
            programmingEventIds,
            issues);

        return new LocalPersistenceIntegrityReport(issues);
    }

    private static void ValidateDailyTrainingPrescriptions(
        JsonObject document,
        IReadOnlyDictionary<string, JsonRecord> completedSessions,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        IReadOnlyList<LocalDailyTrainingPrescriptionRecord> prescriptions;
        try
        {
            prescriptions = LocalDailyTrainingPrescriptionStore.ReadFrom(document);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.ImpossiblePersistedState,
                DailyTrainingPrescriptionsSection,
                null,
                exception.Message);
            return;
        }

        foreach (var duplicateDate in prescriptions
            .GroupBy(record => record.Date)
            .Where(group => group.Count() > 1))
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.ImpossiblePersistedState,
                DailyTrainingPrescriptionsSection,
                null,
                $"More than one prescription owns {duplicateDate.Key.Year:D4}-{duplicateDate.Key.Month:D2}-{duplicateDate.Key.Day:D2}.");
        }

        foreach (var prescription in prescriptions)
        {
            foreach (var block in prescription.Blocks.Where(block => block.IsTerminal))
            {
                if (block.SessionId is null || completedSessions.ContainsKey(block.SessionId))
                {
                    continue;
                }

                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.InvalidReference,
                    DailyTrainingPrescriptionsSection,
                    prescription.PrescriptionId,
                    $"Terminal block {block.BlockId} references missing completed session {block.SessionId}.");
            }
        }
    }

    internal static void ThrowIfInvalid(JsonObject document, string operation)
    {
        var report = ValidateDocument(document);
        if (report.IsValid)
        {
            return;
        }

        var issueSummary = string.Join(
            "; ",
            report.Issues
                .Take(5)
                .Select(issue =>
                    $"{issue.Kind} in {issue.Section}" +
                    (issue.RecordId is null ? string.Empty : $" '{issue.RecordId}'") +
                    $": {issue.Detail}"));

        throw new InvalidOperationException(
            $"Local persistence integrity validation failed before {operation}: {issueSummary}");
    }

    private async ValueTask<JsonObject> ReadDocumentAsync(CancellationToken cancellationToken)
    {
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

        var schemaVersion = LocalDatabaseDocument.ReadSchemaVersion(document);
        if (schemaVersion != LocalDatabaseSchema.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported local database schema version {schemaVersion}.");
        }

        return document;
    }

    private static bool ValidatePractitionerState(
        JsonObject document,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        if (!document.TryGetPropertyValue(PractitionerStateSection, out var stateNode) ||
            stateNode is null)
        {
            return false;
        }

        if (stateNode is not JsonObject stateObject)
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.InvalidDocument,
                PractitionerStateSection,
                null,
                "PractitionerState must be an object.");
            return false;
        }

        if (!stateObject.TryGetPropertyValue(BranchLevelsPropertyName, out var branchLevelsNode) ||
            branchLevelsNode is not JsonArray branchLevels)
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                PractitionerStateSection,
                null,
                "PractitionerState is missing BranchLevels.");
            return true;
        }

        var seenPairs = new HashSet<(BranchCode Branch, GlobalLevelId Level)>();
        for (var index = 0; index < branchLevels.Count; index++)
        {
            if (branchLevels[index] is not JsonObject branchLevel)
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.InvalidDocument,
                    PractitionerStateSection,
                    null,
                    $"BranchLevels[{index}] must be an object.");
                continue;
            }

            if (!TryReadBranchLevelPair(
                    branchLevel,
                    PractitionerStateSection,
                    null,
                    issues,
                    out var branch,
                    out var level))
            {
                continue;
            }

            if (!TryReadBranchLevelState(
                    branchLevel,
                    StatePropertyName,
                    PractitionerStateSection,
                    null,
                    issues,
                    out _))
            {
                continue;
            }

            if (!seenPairs.Add((branch, level)))
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.ImpossiblePersistedState,
                    PractitionerStateSection,
                    null,
                    $"PractitionerState contains duplicate branch-level state for {branch} {level}.");
            }
        }

        return true;
    }

    private static IReadOnlyList<JsonRecord> ReadRecordArray(
        JsonObject document,
        string section,
        string idPropertyName,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        if (!document.TryGetPropertyValue(section, out var recordsNode) ||
            recordsNode is null)
        {
            return [];
        }

        if (recordsNode is not JsonArray recordsArray)
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.InvalidDocument,
                section,
                null,
                $"{section} must be an array.");
            return [];
        }

        var records = new List<JsonRecord>();
        for (var index = 0; index < recordsArray.Count; index++)
        {
            if (recordsArray[index] is not JsonObject recordObject)
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.InvalidDocument,
                    section,
                    null,
                    $"{section}[{index}] must be an object.");
                continue;
            }

            var recordId = ReadRequiredString(
                recordObject,
                idPropertyName,
                section,
                null,
                issues);
            records.Add(new JsonRecord(section, recordId, recordObject));
        }

        return records;
    }

    private static IReadOnlyDictionary<string, JsonRecord> IndexRecords(
        IEnumerable<JsonRecord> records,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        var indexedRecords = new Dictionary<string, JsonRecord>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Id))
            {
                continue;
            }

            if (!indexedRecords.TryAdd(record.Id, record))
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.ImpossiblePersistedState,
                    record.Section,
                    record.Id,
                    $"Duplicate persisted id '{record.Id}' in {record.Section}.");
            }
        }

        return indexedRecords;
    }

    private static void ValidateSessionEvidenceReferences(
        IEnumerable<JsonRecord> records,
        IReadOnlyDictionary<string, JsonRecord> evidenceIds,
        ISet<string> referencedEvidenceIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in records)
        {
            var evidenceReferences = ReadRequiredStringArray(
                record.Object,
                EvidenceArtifactIdsPropertyName,
                record.Section,
                record.Id,
                issues);
            if (evidenceReferences.Length == 0)
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                    record.Section,
                    record.Id,
                    "Completed session must reference at least one evidence artifact.");
            }

            foreach (var evidenceId in evidenceReferences)
            {
                ValidateRequiredReference(
                    record,
                    EvidenceArtifactIdsPropertyName,
                    evidenceId,
                    EvidenceArtifactsSection,
                    evidenceIds,
                    referencedEvidenceIds,
                    issues);
            }
        }
    }

    private static void ValidateAttemptReferences(
        IEnumerable<JsonRecord> records,
        IReadOnlyDictionary<string, JsonRecord> evidenceIds,
        ISet<string> referencedEvidenceIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in records)
        {
            ValidateRequiredReference(
                record,
                EvidenceArtifactIdPropertyName,
                EvidenceArtifactsSection,
                evidenceIds,
                referencedEvidenceIds,
                issues);
        }
    }

    private static void ValidateStabilizationReferences(
        IEnumerable<JsonRecord> records,
        IReadOnlyDictionary<string, JsonRecord> evidenceIds,
        IReadOnlyDictionary<string, JsonRecord> attemptIds,
        IReadOnlyDictionary<string, JsonRecord> sessionIds,
        ISet<string> referencedEvidenceIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in records)
        {
            ValidateRequiredReference(
                record,
                EvidenceArtifactIdPropertyName,
                EvidenceArtifactsSection,
                evidenceIds,
                referencedEvidenceIds,
                issues);
            ValidateOptionalReference(
                record,
                FormalTestAttemptIdPropertyName,
                FormalTestAttemptsSection,
                attemptIds,
                issues);
            ValidateOptionalReference(
                record,
                CompletedSessionIdPropertyName,
                CompletedSessionsSection,
                sessionIds,
                issues);
        }
    }

    private static void ValidateMaintenanceReferences(
        IEnumerable<JsonRecord> records,
        IReadOnlyDictionary<string, JsonRecord> evidenceIds,
        IReadOnlyDictionary<string, JsonRecord> sessionIds,
        ISet<string> referencedEvidenceIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in records)
        {
            ValidateRequiredReference(
                record,
                EvidenceArtifactIdPropertyName,
                EvidenceArtifactsSection,
                evidenceIds,
                referencedEvidenceIds,
                issues);
            ValidateOptionalReference(
                record,
                CompletedSessionIdPropertyName,
                CompletedSessionsSection,
                sessionIds,
                issues);
        }
    }

    private static void ValidateRestorationCheckReferences(
        IEnumerable<JsonRecord> records,
        IReadOnlyDictionary<string, JsonRecord> evidenceIds,
        IReadOnlyDictionary<string, JsonRecord> sessionIds,
        ISet<string> referencedEvidenceIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in records)
        {
            ValidateRequiredReference(
                record,
                EvidenceArtifactIdPropertyName,
                EvidenceArtifactsSection,
                evidenceIds,
                referencedEvidenceIds,
                issues);
            ValidateOptionalReference(
                record,
                CompletedSessionIdPropertyName,
                CompletedSessionsSection,
                sessionIds,
                issues);
        }
    }

    private static void ValidateDecayReferences(
        IEnumerable<JsonRecord> records,
        IReadOnlyDictionary<string, JsonRecord> maintenanceIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in records)
        {
            var maintenanceReferences = ReadRequiredStringArray(
                record.Object,
                MaintenanceCheckIdsPropertyName,
                record.Section,
                record.Id,
                issues);
            if (maintenanceReferences.Length == 0)
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                    record.Section,
                    record.Id,
                    "Decay history must reference the maintenance checks that caused decay.");
            }

            foreach (var maintenanceId in maintenanceReferences)
            {
                ValidateRequiredReference(
                    record,
                    MaintenanceCheckIdsPropertyName,
                    maintenanceId,
                    MaintenanceChecksSection,
                    maintenanceIds,
                    null,
                    issues);
            }
        }
    }

    private static void ValidateRestorationHistoryReferences(
        IEnumerable<JsonRecord> records,
        IReadOnlyDictionary<string, JsonRecord> restorationCheckIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in records)
        {
            var restorationReferences = ReadRequiredStringArray(
                record.Object,
                RestorationCheckIdsPropertyName,
                record.Section,
                record.Id,
                issues);
            if (restorationReferences.Length == 0)
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                    record.Section,
                    record.Id,
                    "Restoration history must reference the restoration checks that restored the branch.");
            }

            foreach (var restorationId in restorationReferences)
            {
                ValidateRequiredReference(
                    record,
                    RestorationCheckIdsPropertyName,
                    restorationId,
                    RestorationChecksSection,
                    restorationCheckIds,
                    null,
                    issues);
            }
        }
    }

    private static void ValidateGeneratedInstanceReferences(
        IEnumerable<JsonRecord> records,
        IReadOnlyDictionary<string, JsonRecord> evidenceIds,
        IReadOnlySet<string> inProgressSessionIds,
        ISet<string> referencedEvidenceIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in records)
        {
            if (!TryReadGeneratedInstanceState(record, issues, out var state))
            {
                continue;
            }

            var activeSessionId = ReadOptionalString(
                record.Object,
                ActiveSessionIdPropertyName,
                record.Section,
                record.Id,
                issues);
            var resultEvidenceId = ReadOptionalString(
                record.Object,
                ResultEvidenceArtifactIdPropertyName,
                record.Section,
                record.Id,
                issues);

            if (state == LocalGeneratedDrillInstanceState.InSession)
            {
                if (activeSessionId is null)
                {
                    AddIssue(
                        issues,
                        LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                        record.Section,
                        record.Id,
                        "An in-session generated drill instance must reference its prepared or active runtime session.");
                }
                else if (!inProgressSessionIds.Contains(activeSessionId))
                {
                    AddIssue(
                        issues,
                        LocalPersistenceIntegrityIssueKind.InvalidReference,
                        record.Section,
                        record.Id,
                        $"ActiveSessionId references missing prepared or active session {activeSessionId}.");
                }
            }
            else if (activeSessionId is not null)
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.ImpossiblePersistedState,
                    record.Section,
                    record.Id,
                    "Only in-session generated drill instances may reference an active session.");
            }

            if (state == LocalGeneratedDrillInstanceState.Completed)
            {
                ValidateRequiredReference(
                    record,
                    ResultEvidenceArtifactIdPropertyName,
                    resultEvidenceId,
                    EvidenceArtifactsSection,
                    evidenceIds,
                    referencedEvidenceIds,
                    issues);
            }
            else if (resultEvidenceId is not null)
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.ImpossiblePersistedState,
                    record.Section,
                    record.Id,
                    "Only completed generated drill instances may reference result evidence.");
            }
        }
    }

    private static void ValidateProgressSummaryReferences(
        IEnumerable<JsonRecord> records,
        bool practitionerStateExists,
        IReadOnlyDictionary<string, JsonRecord> sessionIds,
        IReadOnlyDictionary<string, JsonRecord> attemptIds,
        IReadOnlyDictionary<string, JsonRecord> maintenanceIds,
        IReadOnlyDictionary<string, JsonRecord> evidenceIds,
        ISet<string> referencedEvidenceIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in records)
        {
            var sourceReferences = ReadRequiredArray(
                record.Object,
                SourceReferencesPropertyName,
                record.Section,
                record.Id,
                issues);
            if (sourceReferences is null)
            {
                continue;
            }

            if (sourceReferences.Count == 0)
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                    record.Section,
                    record.Id,
                    "Progress summaries must reference the local facts they were derived from.");
            }

            for (var index = 0; index < sourceReferences.Count; index++)
            {
                if (sourceReferences[index] is not JsonObject sourceReference)
                {
                    AddIssue(
                        issues,
                        LocalPersistenceIntegrityIssueKind.InvalidDocument,
                        record.Section,
                        record.Id,
                        $"SourceReferences[{index}] must be an object.");
                    continue;
                }

                var sourceKindId = ReadRequiredString(
                    sourceReference,
                    SourceKindPropertyName,
                    record.Section,
                    record.Id,
                    issues);
                var sourceId = ReadRequiredString(
                    sourceReference,
                    SourceIdPropertyName,
                    record.Section,
                    record.Id,
                    issues);
                if (sourceKindId is null || sourceId is null)
                {
                    continue;
                }

                if (!ProgressSummarySourceKinds.TryFromPersistedId(sourceKindId, out var sourceKind))
                {
                    AddIssue(
                        issues,
                        LocalPersistenceIntegrityIssueKind.UnknownIdentifier,
                        record.Section,
                        record.Id,
                        $"Unknown progress summary source kind '{sourceKindId}'.");
                    continue;
                }

                switch (sourceKind)
                {
                    case LocalProgressSummarySourceKind.PractitionerState:
                        if (!practitionerStateExists || sourceId != PractitionerStateSection)
                        {
                            AddIssue(
                                issues,
                                LocalPersistenceIntegrityIssueKind.InvalidReference,
                                record.Section,
                                record.Id,
                                $"Progress summary references missing practitioner state source '{sourceId}'.");
                        }

                        break;
                    case LocalProgressSummarySourceKind.CompletedSession:
                        ValidateReferenceId(
                            record,
                            SourceReferencesPropertyName,
                            sourceId,
                            CompletedSessionsSection,
                            sessionIds,
                            null,
                            issues,
                            LocalPersistenceIntegrityIssueKind.InvalidReference);
                        break;
                    case LocalProgressSummarySourceKind.FormalTestAttempt:
                        ValidateReferenceId(
                            record,
                            SourceReferencesPropertyName,
                            sourceId,
                            FormalTestAttemptsSection,
                            attemptIds,
                            null,
                            issues,
                            LocalPersistenceIntegrityIssueKind.InvalidReference);
                        break;
                    case LocalProgressSummarySourceKind.MaintenanceCheck:
                        ValidateReferenceId(
                            record,
                            SourceReferencesPropertyName,
                            sourceId,
                            MaintenanceChecksSection,
                            maintenanceIds,
                            null,
                            issues,
                            LocalPersistenceIntegrityIssueKind.InvalidReference);
                        break;
                    case LocalProgressSummarySourceKind.EvidenceArtifact:
                        ValidateReferenceId(
                            record,
                            SourceReferencesPropertyName,
                            sourceId,
                            EvidenceArtifactsSection,
                            evidenceIds,
                            referencedEvidenceIds,
                            issues,
                            LocalPersistenceIntegrityIssueKind.InvalidReference);
                        break;
                }
            }
        }
    }

    private static void ValidateProgressSummaryReadModelReferences(
        IEnumerable<JsonRecord> records,
        IReadOnlyDictionary<string, JsonRecord> maintenanceIds,
        IReadOnlyDictionary<string, JsonRecord> decayIds,
        IReadOnlyDictionary<string, JsonRecord> restorationIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        var blockerSourceIds = new HashSet<string>(StringComparer.Ordinal);
        AddIds(blockerSourceIds, maintenanceIds);
        AddIds(blockerSourceIds, decayIds);
        AddIds(blockerSourceIds, restorationIds);

        foreach (var record in records)
        {
            var maintenanceSummaries = ReadOptionalArray(record.Object, "MaintenanceSummaries");
            if (maintenanceSummaries is not null)
            {
                foreach (var summary in maintenanceSummaries.OfType<JsonObject>())
                {
                    var sourceCheckId = ReadOptionalString(
                        summary,
                        SourceCheckIdPropertyName,
                        record.Section,
                        record.Id,
                        issues);
                    if (sourceCheckId is not null && !maintenanceIds.ContainsKey(sourceCheckId))
                    {
                        AddIssue(
                            issues,
                            LocalPersistenceIntegrityIssueKind.InvalidReference,
                            record.Section,
                            record.Id,
                            $"Progress summary maintenance source check '{sourceCheckId}' does not exist.");
                    }
                }
            }

            var activeBlockers = ReadOptionalArray(record.Object, "ActiveBlockers");
            if (activeBlockers is null)
            {
                continue;
            }

            foreach (var blocker in activeBlockers.OfType<JsonObject>())
            {
                var sourceRecordId = ReadOptionalString(
                    blocker,
                    SourceRecordIdPropertyName,
                    record.Section,
                    record.Id,
                    issues);
                if (sourceRecordId is not null && !blockerSourceIds.Contains(sourceRecordId))
                {
                    AddIssue(
                        issues,
                        LocalPersistenceIntegrityIssueKind.InvalidReference,
                        record.Section,
                        record.Id,
                        $"Progress summary blocker source record '{sourceRecordId}' does not exist.");
                }
            }
        }
    }

    private static void ValidateStateHistoryTransitions(
        IEnumerable<JsonRecord> records,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in records)
        {
            if (!TryReadStatus(
                    record,
                    CurrentStatusPropertyName,
                    issues,
                    out var currentStatus) ||
                !TryReadStatus(
                    record,
                    NextStatusPropertyName,
                    issues,
                    out var nextStatus) ||
                !TryReadTransition(record, issues, out var transition, out var transitionId))
            {
                continue;
            }

            var transitionResult = BranchLevelStateMachine.TryApply(currentStatus, transition);
            if (!transitionResult.IsValid || transitionResult.NextStatus != nextStatus)
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.ImpossiblePersistedState,
                    record.Section,
                    record.Id,
                    $"Persisted transition '{transitionId}' from {currentStatus.State} to {nextStatus.State} is not legal for {currentStatus.Branch} {currentStatus.Level}.");
            }
        }
    }

    private static void ValidateOrphanedEvidence(
        IEnumerable<JsonRecord> evidenceRecords,
        IReadOnlySet<string> referencedEvidenceIds,
        IReadOnlySet<string> programmingEventIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        foreach (var record in evidenceRecords)
        {
            var eventId = ReadEvidenceEventId(record, issues);
            if (record.Id is not null &&
                !referencedEvidenceIds.Contains(record.Id) &&
                (eventId is null || !programmingEventIds.Contains(eventId)))
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.OrphanedEvidence,
                    record.Section,
                    record.Id,
                    $"Evidence artifact '{record.Id}' is not referenced by a persisted programming event.");
            }

            if (eventId is not null &&
                !programmingEventIds.Contains(eventId) &&
                record.Id is not null &&
                referencedEvidenceIds.Contains(record.Id))
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.InvalidReference,
                    record.Section,
                    record.Id,
                    $"Evidence artifact event '{eventId}' does not exist.");
            }
        }
    }

    private static void ValidateKnownIdentifiers(
        JsonNode? node,
        string path,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var property in jsonObject)
                {
                    var nextPath = $"{path}.{property.Key}";
                    ValidateKnownIdentifierProperty(property.Key, property.Value, nextPath, issues);
                    ValidateKnownIdentifiers(property.Value, nextPath, issues);
                }

                break;
            case JsonArray jsonArray:
                for (var index = 0; index < jsonArray.Count; index++)
                {
                    ValidateKnownIdentifiers(jsonArray[index], $"{path}[{index}]", issues);
                }

                break;
        }
    }

    private static void ValidateKnownIdentifierProperty(
        string propertyName,
        JsonNode? node,
        string path,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        switch (propertyName)
        {
            case BranchPropertyName or "BottleneckBranch":
                ValidateMappedIdentifier(node, StableDomainIdentifiers.Branches, path, "Branch", issues);
                break;
            case LevelPropertyName or "OwnedLevel" or "LastOwnedLevel" or "HighestOwnedLevel":
                ValidateMappedIdentifier(node, StableDomainIdentifiers.Levels, path, "Level", issues);
                break;
            case "Drill":
                ValidateMappedIdentifier(node, StableDomainIdentifiers.Drills, path, "Drill", issues);
                break;
            case "Category":
                ValidateMappedIdentifier(node, StableDomainIdentifiers.EvidenceArtifactCategories, path, "Evidence artifact category", issues);
                break;
            case "ResultKind":
                ValidateMappedIdentifier(node, StableDomainIdentifiers.TestResultEvidenceKinds, path, "Test result evidence kind", issues);
                break;
            case "FailureType":
                ValidateMappedIdentifier(node, StableDomainIdentifiers.FailureTypes, path, "Failure type", issues);
                break;
            case "PassState":
                ValidateMappedIdentifier(node, StableDomainIdentifiers.FormalTestPassStates, path, "Formal test pass state", issues);
                break;
            case "ContentKind":
                ValidateMappedIdentifier(node, StableDomainIdentifiers.PromptContentKinds, path, "Prompt content kind", issues);
                break;
            case "MaintenanceState":
                ValidateMappedIdentifier(node, StableDomainIdentifiers.MaintenanceStates, path, "Maintenance state", issues);
                break;
            case "StateAtHighestOwnedLevel":
                ValidateMappedIdentifier(node, StableDomainIdentifiers.BranchLevelStates, path, "Branch-level state", issues);
                break;
            case StatePropertyName when path.Contains($".{PractitionerStateSection}.", StringComparison.Ordinal) ||
                                        path.Contains($".{CurrentStatusPropertyName}.", StringComparison.Ordinal) ||
                                        path.Contains($".{NextStatusPropertyName}.", StringComparison.Ordinal):
                ValidateMappedIdentifier(node, StableDomainIdentifiers.BranchLevelStates, path, "Branch-level state", issues);
                break;
        }
    }

    private static void ValidateMappedIdentifier<TDomain>(
        JsonNode? node,
        IStableDomainIdentifierMap<TDomain> map,
        string path,
        string label,
        ICollection<LocalPersistenceIntegrityIssue> issues)
        where TDomain : struct, Enum
    {
        if (node is null)
        {
            return;
        }

        if (!TryGetStringValue(node, out var persistedId) ||
            string.IsNullOrWhiteSpace(persistedId) ||
            !map.TryFromPersistedId(persistedId, out _))
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.UnknownIdentifier,
                PathSection(path),
                null,
                $"{label} identifier '{NodeText(node)}' at {path} is not known.");
        }
    }

    private static void ValidateRequiredReference(
        JsonRecord record,
        string propertyName,
        string targetSection,
        IReadOnlyDictionary<string, JsonRecord> targetIds,
        ISet<string>? referencedIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        var referencedId = ReadRequiredString(
            record.Object,
            propertyName,
            record.Section,
            record.Id,
            issues);
        ValidateRequiredReference(
            record,
            propertyName,
            referencedId,
            targetSection,
            targetIds,
            referencedIds,
            issues);
    }

    private static void ValidateRequiredReference(
        JsonRecord record,
        string propertyName,
        string? referencedId,
        string targetSection,
        IReadOnlyDictionary<string, JsonRecord> targetIds,
        ISet<string>? referencedIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(referencedId))
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                record.Section,
                record.Id,
                $"{record.Section} must reference {targetSection} through {propertyName}.");
            return;
        }

        ValidateReferenceId(
            record,
            propertyName,
            referencedId,
            targetSection,
            targetIds,
            referencedIds,
            issues,
            LocalPersistenceIntegrityIssueKind.MissingRequiredRecord);
    }

    private static void ValidateOptionalReference(
        JsonRecord record,
        string propertyName,
        string targetSection,
        IReadOnlyDictionary<string, JsonRecord> targetIds,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        var referencedId = ReadOptionalString(
            record.Object,
            propertyName,
            record.Section,
            record.Id,
            issues);
        if (referencedId is null)
        {
            return;
        }

        ValidateReferenceId(
            record,
            propertyName,
            referencedId,
            targetSection,
            targetIds,
            null,
            issues,
            LocalPersistenceIntegrityIssueKind.InvalidReference);
    }

    private static void ValidateReferenceId(
        JsonRecord record,
        string propertyName,
        string referencedId,
        string targetSection,
        IReadOnlyDictionary<string, JsonRecord> targetIds,
        ISet<string>? referencedIds,
        ICollection<LocalPersistenceIntegrityIssue> issues,
        LocalPersistenceIntegrityIssueKind missingKind)
    {
        referencedIds?.Add(referencedId);

        if (!targetIds.ContainsKey(referencedId))
        {
            AddIssue(
                issues,
                missingKind,
                record.Section,
                record.Id,
                $"{propertyName} references missing {targetSection} record '{referencedId}'.");
        }
    }

    private static bool TryReadStatus(
        JsonRecord record,
        string propertyName,
        ICollection<LocalPersistenceIntegrityIssue> issues,
        out BranchLevelStatus status)
    {
        status = default;
        var statusObject = ReadRequiredObject(
            record.Object,
            propertyName,
            record.Section,
            record.Id,
            issues);
        if (statusObject is null)
        {
            return false;
        }

        if (!TryReadBranchLevelPair(
                statusObject,
                record.Section,
                record.Id,
                issues,
                out var branch,
                out var level) ||
            !TryReadBranchLevelState(
                statusObject,
                StatePropertyName,
                record.Section,
                record.Id,
                issues,
                out var state))
        {
            return false;
        }

        status = new BranchLevelStatus(branch, level, state);
        return true;
    }

    private static bool TryReadBranchLevelPair(
        JsonObject jsonObject,
        string section,
        string? recordId,
        ICollection<LocalPersistenceIntegrityIssue> issues,
        out BranchCode branch,
        out GlobalLevelId level)
    {
        branch = default;
        level = default;

        var branchId = ReadRequiredString(jsonObject, BranchPropertyName, section, recordId, issues);
        var levelId = ReadRequiredString(jsonObject, LevelPropertyName, section, recordId, issues);
        var hasBranch = TryReadMappedIdentifier(
            branchId,
            StableDomainIdentifiers.Branches,
            "Branch",
            section,
            recordId,
            issues,
            out branch);
        var hasLevel = TryReadMappedIdentifier(
            levelId,
            StableDomainIdentifiers.Levels,
            "Level",
            section,
            recordId,
            issues,
            out level);

        return hasBranch && hasLevel;
    }

    private static bool TryReadBranchLevelState(
        JsonObject jsonObject,
        string propertyName,
        string section,
        string? recordId,
        ICollection<LocalPersistenceIntegrityIssue> issues,
        out BranchLevelState state)
    {
        var stateId = ReadRequiredString(jsonObject, propertyName, section, recordId, issues);
        return TryReadMappedIdentifier(
            stateId,
            StableDomainIdentifiers.BranchLevelStates,
            "Branch-level state",
            section,
            recordId,
            issues,
            out state);
    }

    private static bool TryReadGeneratedInstanceState(
        JsonRecord record,
        ICollection<LocalPersistenceIntegrityIssue> issues,
        out LocalGeneratedDrillInstanceState state)
    {
        var stateId = ReadRequiredString(
            record.Object,
            StatePropertyName,
            record.Section,
            record.Id,
            issues);
        return TryReadMappedIdentifier(
            stateId,
            GeneratedInstanceStates,
            "Generated drill instance state",
            record.Section,
            record.Id,
            issues,
            out state);
    }

    private static bool TryReadMappedIdentifier<TDomain>(
        string? persistedId,
        IStableDomainIdentifierMap<TDomain> map,
        string label,
        string section,
        string? recordId,
        ICollection<LocalPersistenceIntegrityIssue> issues,
        out TDomain value)
        where TDomain : struct, Enum
    {
        if (persistedId is not null &&
            map.TryFromPersistedId(persistedId, out value))
        {
            return true;
        }

        value = default;
        if (persistedId is not null)
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.UnknownIdentifier,
                section,
                recordId,
                $"{label} identifier '{persistedId}' is not known.");
        }

        return false;
    }

    private static bool TryReadTransition(
        JsonRecord record,
        ICollection<LocalPersistenceIntegrityIssue> issues,
        out BranchLevelTransition transition,
        out string? transitionId)
    {
        transition = default;
        transitionId = ReadRequiredString(
            record.Object,
            TransitionPropertyName,
            record.Section,
            record.Id,
            issues);
        if (transitionId is null)
        {
            return false;
        }

        if (Enum.TryParse(transitionId, ignoreCase: false, out transition))
        {
            return true;
        }

        AddIssue(
            issues,
            LocalPersistenceIntegrityIssueKind.ImpossiblePersistedState,
            record.Section,
            record.Id,
            $"Persisted transition '{transitionId}' is not known.");
        return false;
    }

    private static string? ReadEvidenceEventId(
        JsonRecord record,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        var eventObject = ReadRequiredObject(
            record.Object,
            EventPropertyName,
            record.Section,
            record.Id,
            issues);
        return eventObject is null
            ? null
            : ReadRequiredString(
                eventObject,
                EventIdPropertyName,
                record.Section,
                record.Id,
                issues);
    }

    private static JsonObject? ReadRequiredObject(
        JsonObject jsonObject,
        string propertyName,
        string section,
        string? recordId,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonObject objectNode)
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                section,
                recordId,
                $"{section} is missing required object {propertyName}.");
            return null;
        }

        return objectNode;
    }

    private static JsonArray? ReadRequiredArray(
        JsonObject jsonObject,
        string propertyName,
        string section,
        string? recordId,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonArray arrayNode)
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                section,
                recordId,
                $"{section} is missing required array {propertyName}.");
            return null;
        }

        return arrayNode;
    }

    private static JsonArray? ReadOptionalArray(JsonObject jsonObject, string propertyName)
    {
        return jsonObject.TryGetPropertyValue(propertyName, out var node) && node is JsonArray arrayNode
            ? arrayNode
            : null;
    }

    private static string? ReadRequiredString(
        JsonObject jsonObject,
        string propertyName,
        string section,
        string? recordId,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null ||
            !TryGetStringValue(node, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                section,
                recordId,
                $"{section} is missing required value {propertyName}.");
            return null;
        }

        return value;
    }

    private static string? ReadOptionalString(
        JsonObject jsonObject,
        string propertyName,
        string section,
        string? recordId,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            return null;
        }

        if (!TryGetStringValue(node, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            AddIssue(
                issues,
                LocalPersistenceIntegrityIssueKind.InvalidDocument,
                section,
                recordId,
                $"{propertyName} must be a non-empty string when present.");
            return null;
        }

        return value;
    }

    private static string[] ReadRequiredStringArray(
        JsonObject jsonObject,
        string propertyName,
        string section,
        string? recordId,
        ICollection<LocalPersistenceIntegrityIssue> issues)
    {
        var array = ReadRequiredArray(jsonObject, propertyName, section, recordId, issues);
        if (array is null)
        {
            return [];
        }

        var values = new List<string>();
        for (var index = 0; index < array.Count; index++)
        {
            var item = array[index];
            if (item is null ||
                !TryGetStringValue(item, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                AddIssue(
                    issues,
                    LocalPersistenceIntegrityIssueKind.MissingRequiredRecord,
                    section,
                    recordId,
                    $"{propertyName}[{index}] must be a non-empty record id.");
                continue;
            }

            values.Add(value);
        }

        return values.ToArray();
    }

    private static bool TryGetStringValue(JsonNode node, out string value)
    {
        try
        {
            value = node.GetValue<string>();
            return true;
        }
        catch (InvalidOperationException)
        {
            value = string.Empty;
            return false;
        }
        catch (FormatException)
        {
            value = string.Empty;
            return false;
        }
    }

    private static string NodeText(JsonNode? node)
    {
        return node is null ? "<null>" : node.ToJsonString();
    }

    private static string PathSection(string path)
    {
        var trimmedPath = path.TrimStart('$', '.');
        var separatorIndex = trimmedPath.IndexOfAny(['.', '[']);
        return separatorIndex < 0
            ? trimmedPath
            : trimmedPath[..separatorIndex];
    }

    private static void AddIds(
        ISet<string> ids,
        IReadOnlyDictionary<string, JsonRecord> records)
    {
        foreach (var id in records.Keys)
        {
            ids.Add(id);
        }
    }

    private static void AddIssue(
        ICollection<LocalPersistenceIntegrityIssue> issues,
        LocalPersistenceIntegrityIssueKind kind,
        string section,
        string? recordId,
        string detail)
    {
        issues.Add(new LocalPersistenceIntegrityIssue(kind, section, recordId, detail));
    }

    private sealed record JsonRecord(
        string Section,
        string? Id,
        JsonObject Object);
}
