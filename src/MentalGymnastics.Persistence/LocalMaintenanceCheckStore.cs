using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public sealed class LocalMaintenanceCheckRecord
{
    public LocalMaintenanceCheckRecord(
        string checkId,
        string evidenceArtifactId,
        string? completedSessionId,
        DrillId? drill,
        string standard,
        MaintenanceCheckEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(checkId))
        {
            throw new ArgumentException("Maintenance check id is required.", nameof(checkId));
        }

        if (string.IsNullOrWhiteSpace(evidenceArtifactId))
        {
            throw new ArgumentException("Evidence artifact id is required.", nameof(evidenceArtifactId));
        }

        if (completedSessionId is not null && string.IsNullOrWhiteSpace(completedSessionId))
        {
            throw new ArgumentException("Completed session id cannot be blank.", nameof(completedSessionId));
        }

        if (string.IsNullOrWhiteSpace(standard))
        {
            throw new ArgumentException("Maintenance standard is required.", nameof(standard));
        }

        ArgumentNullException.ThrowIfNull(evidence);

        CheckId = checkId;
        EvidenceArtifactId = evidenceArtifactId;
        CompletedSessionId = completedSessionId;
        Drill = drill;
        Standard = standard;
        Evidence = evidence;
    }

    public string CheckId { get; }

    public string EvidenceArtifactId { get; }

    public string? CompletedSessionId { get; }

    public DrillId? Drill { get; }

    public string Standard { get; }

    public MaintenanceCheckEvidence Evidence { get; }
}

public sealed class LocalRestorationCheckRecord
{
    public LocalRestorationCheckRecord(
        string checkId,
        string evidenceArtifactId,
        string? completedSessionId,
        DrillId? drill,
        string standard,
        RestorationCheckEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(checkId))
        {
            throw new ArgumentException("Restoration check id is required.", nameof(checkId));
        }

        if (string.IsNullOrWhiteSpace(evidenceArtifactId))
        {
            throw new ArgumentException("Evidence artifact id is required.", nameof(evidenceArtifactId));
        }

        if (completedSessionId is not null && string.IsNullOrWhiteSpace(completedSessionId))
        {
            throw new ArgumentException("Completed session id cannot be blank.", nameof(completedSessionId));
        }

        if (string.IsNullOrWhiteSpace(standard))
        {
            throw new ArgumentException("Restoration standard is required.", nameof(standard));
        }

        ArgumentNullException.ThrowIfNull(evidence);

        CheckId = checkId;
        EvidenceArtifactId = evidenceArtifactId;
        CompletedSessionId = completedSessionId;
        Drill = drill;
        Standard = standard;
        Evidence = evidence;
    }

    public string CheckId { get; }

    public string EvidenceArtifactId { get; }

    public string? CompletedSessionId { get; }

    public DrillId? Drill { get; }

    public string Standard { get; }

    public RestorationCheckEvidence Evidence { get; }
}

public sealed class LocalMaintenanceCheckStore
{
    private const string MaintenanceChecksPropertyName = "MaintenanceChecks";
    private const string RestorationChecksPropertyName = "RestorationChecks";
    private const string CheckIdPropertyName = "CheckId";
    private const string EvidenceArtifactIdPropertyName = "EvidenceArtifactId";
    private const string CompletedSessionIdPropertyName = "CompletedSessionId";
    private const string DrillPropertyName = "Drill";
    private const string StandardPropertyName = "Standard";
    private const string EvidencePropertyName = "Evidence";
    private const string BranchPropertyName = "Branch";
    private const string OwnedLevelPropertyName = "OwnedLevel";
    private const string LastOwnedLevelPropertyName = "LastOwnedLevel";
    private const string DatePropertyName = "Date";
    private const string YearPropertyName = "Year";
    private const string MonthPropertyName = "Month";
    private const string DayPropertyName = "Day";
    private const string KindPropertyName = "Kind";
    private const string StandardEvaluationResultPropertyName = "StandardEvaluationResult";
    private const string PassedPropertyName = "Passed";
    private const string FailuresPropertyName = "Failures";
    private const string FailureKindPropertyName = "FailureKind";
    private const string DetailPropertyName = "Detail";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly IStableDomainIdentifierMap<MaintenanceCheckKind> MaintenanceCheckKinds =
        new StableDomainIdentifierMap<MaintenanceCheckKind>(new Dictionary<MaintenanceCheckKind, string>
        {
            [MaintenanceCheckKind.StandardOrTransfer] = "StandardOrTransfer",
            [MaintenanceCheckKind.GlobalComposite] = "GlobalComposite",
        });

    private static readonly IStableDomainIdentifierMap<RestorationCheckKind> RestorationCheckKinds =
        new StableDomainIdentifierMap<RestorationCheckKind>(new Dictionary<RestorationCheckKind, string>
        {
            [RestorationCheckKind.LastOwnedStandard] = "LastOwnedStandard",
            [RestorationCheckKind.LowerLoadTransferCheck] = "LowerLoadTransferCheck",
        });

    private static readonly IStableDomainIdentifierMap<StandardFailureKind> StandardFailureKinds =
        new StableDomainIdentifierMap<StandardFailureKind>(new Dictionary<StandardFailureKind, string>
        {
            [StandardFailureKind.CriticalConstraintBroken] = "CriticalConstraintBroken",
            [StandardFailureKind.OutputIncomplete] = "OutputIncomplete",
            [StandardFailureKind.NumericalThresholdMissed] = "NumericalThresholdMissed",
            [StandardFailureKind.RubricDidNotPass] = "RubricDidNotPass",
        });

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalMaintenanceCheckStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask SaveMaintenanceAsync(
        LocalMaintenanceCheckRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var checks = ReadMaintenanceArray(document);
        var replacementIndex = FindCheckIndex(checks, record.CheckId);

        if (replacementIndex >= 0)
        {
            checks[replacementIndex] = WriteMaintenanceRecord(record);
        }
        else
        {
            checks.Add(WriteMaintenanceRecord(record));
        }

        document[MaintenanceChecksPropertyName] = checks;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalMaintenanceCheckRecord?> LoadMaintenanceAsync(
        string checkId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkId))
        {
            throw new ArgumentException("Maintenance check id is required.", nameof(checkId));
        }

        var checks = await ReadMaintenanceRecordsAsync(cancellationToken).ConfigureAwait(false);
        return checks.FirstOrDefault(record => record.CheckId == checkId);
    }

    public ValueTask<IReadOnlyList<LocalMaintenanceCheckRecord>> ListMaintenanceAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadMaintenanceRecordsAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<LocalMaintenanceCheckRecord>> ListMaintenanceByBranchLevelAsync(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        CancellationToken cancellationToken = default)
    {
        var checks = await ReadMaintenanceRecordsAsync(cancellationToken).ConfigureAwait(false);
        return checks
            .Where(record => record.Evidence.Branch == branch && record.Evidence.OwnedLevel == ownedLevel)
            .ToArray();
    }

    public async ValueTask<MaintenanceCurrencyRequest> LoadMaintenanceCurrencyRequestAsync(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate asOf,
        CancellationToken cancellationToken = default)
    {
        var checks = await ListMaintenanceByBranchLevelAsync(branch, ownedLevel, cancellationToken)
            .ConfigureAwait(false);

        return new MaintenanceCurrencyRequest(
            branch,
            ownedLevel,
            asOf,
            checks.Select(record => record.Evidence));
    }

    public async ValueTask SaveRestorationAsync(
        LocalRestorationCheckRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var checks = ReadRestorationArray(document);
        var replacementIndex = FindCheckIndex(checks, record.CheckId);

        if (replacementIndex >= 0)
        {
            checks[replacementIndex] = WriteRestorationRecord(record);
        }
        else
        {
            checks.Add(WriteRestorationRecord(record));
        }

        document[RestorationChecksPropertyName] = checks;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalRestorationCheckRecord?> LoadRestorationAsync(
        string checkId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkId))
        {
            throw new ArgumentException("Restoration check id is required.", nameof(checkId));
        }

        var checks = await ReadRestorationRecordsAsync(cancellationToken).ConfigureAwait(false);
        return checks.FirstOrDefault(record => record.CheckId == checkId);
    }

    public ValueTask<IReadOnlyList<LocalRestorationCheckRecord>> ListRestorationAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadRestorationRecordsAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<LocalRestorationCheckRecord>> ListRestorationByBranchLevelAsync(
        BranchCode branch,
        GlobalLevelId lastOwnedLevel,
        CancellationToken cancellationToken = default)
    {
        var checks = await ReadRestorationRecordsAsync(cancellationToken).ConfigureAwait(false);
        return checks
            .Where(record => record.Evidence.Branch == branch && record.Evidence.LastOwnedLevel == lastOwnedLevel)
            .ToArray();
    }

    public async ValueTask<RestorationEvidence> LoadRestorationEvidenceAsync(
        BranchCode branch,
        GlobalLevelId lastOwnedLevel,
        CancellationToken cancellationToken = default)
    {
        var checks = await ListRestorationByBranchLevelAsync(branch, lastOwnedLevel, cancellationToken)
            .ConfigureAwait(false);

        return new RestorationEvidence(
            branch,
            lastOwnedLevel,
            checks.Select(record => record.Evidence));
    }

    private async ValueTask<IReadOnlyList<LocalMaintenanceCheckRecord>> ReadMaintenanceRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadMaintenanceArray(document)
            .Select(ReadMaintenanceRecord)
            .OrderBy(record => record.Evidence.Date.Year)
            .ThenBy(record => record.Evidence.Date.Month)
            .ThenBy(record => record.Evidence.Date.Day)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<LocalRestorationCheckRecord>> ReadRestorationRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadRestorationArray(document)
            .Select(ReadRestorationRecord)
            .OrderBy(record => record.Evidence.Date.Year)
            .ThenBy(record => record.Evidence.Date.Month)
            .ThenBy(record => record.Evidence.Date.Day)
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

        var document = await JsonSerializer.DeserializeAsync<JsonObject>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

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

        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static JsonArray ReadMaintenanceArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(MaintenanceChecksPropertyName, out var checksNode) ||
            checksNode is null)
        {
            return [];
        }

        if (checksNode is JsonArray checks)
        {
            return checks;
        }

        throw new InvalidOperationException("The stored maintenance check history is invalid.");
    }

    private static JsonArray ReadRestorationArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(RestorationChecksPropertyName, out var checksNode) ||
            checksNode is null)
        {
            return [];
        }

        if (checksNode is JsonArray checks)
        {
            return checks;
        }

        throw new InvalidOperationException("The stored restoration check history is invalid.");
    }

    private static int FindCheckIndex(JsonArray checks, string checkId)
    {
        for (var index = 0; index < checks.Count; index++)
        {
            if (checks[index] is JsonObject checkObject &&
                checkObject.TryGetPropertyValue(CheckIdPropertyName, out var checkIdNode) &&
                checkIdNode?.GetValue<string>() == checkId)
            {
                return index;
            }
        }

        return -1;
    }

    internal static JsonObject WriteMaintenanceRecord(LocalMaintenanceCheckRecord record)
    {
        var recordObject = new JsonObject
        {
            [CheckIdPropertyName] = record.CheckId,
            [EvidenceArtifactIdPropertyName] = record.EvidenceArtifactId,
            [StandardPropertyName] = record.Standard,
            [EvidencePropertyName] = WriteMaintenanceEvidence(record.Evidence),
        };

        if (record.CompletedSessionId is not null)
        {
            recordObject[CompletedSessionIdPropertyName] = record.CompletedSessionId;
        }

        if (record.Drill is { } drill)
        {
            recordObject[DrillPropertyName] = StableDomainIdentifiers.Drills.ToPersistedId(drill);
        }

        return recordObject;
    }

    internal static JsonObject WriteRestorationRecord(LocalRestorationCheckRecord record)
    {
        var recordObject = new JsonObject
        {
            [CheckIdPropertyName] = record.CheckId,
            [EvidenceArtifactIdPropertyName] = record.EvidenceArtifactId,
            [StandardPropertyName] = record.Standard,
            [EvidencePropertyName] = WriteRestorationEvidence(record.Evidence),
        };

        if (record.CompletedSessionId is not null)
        {
            recordObject[CompletedSessionIdPropertyName] = record.CompletedSessionId;
        }

        if (record.Drill is { } drill)
        {
            recordObject[DrillPropertyName] = StableDomainIdentifiers.Drills.ToPersistedId(drill);
        }

        return recordObject;
    }

    private static JsonObject WriteMaintenanceEvidence(MaintenanceCheckEvidence evidence)
    {
        return new JsonObject
        {
            [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(evidence.Branch),
            [OwnedLevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(evidence.OwnedLevel),
            [DatePropertyName] = WriteDate(evidence.Date),
            [KindPropertyName] = MaintenanceCheckKinds.ToPersistedId(evidence.Kind),
            [StandardEvaluationResultPropertyName] = WriteStandardEvaluationResult(evidence.StandardEvaluationResult),
        };
    }

    private static JsonObject WriteRestorationEvidence(RestorationCheckEvidence evidence)
    {
        return new JsonObject
        {
            [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(evidence.Branch),
            [LastOwnedLevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(evidence.LastOwnedLevel),
            [DatePropertyName] = WriteDate(evidence.Date),
            [KindPropertyName] = RestorationCheckKinds.ToPersistedId(evidence.Kind),
            [StandardEvaluationResultPropertyName] = WriteStandardEvaluationResult(evidence.StandardEvaluationResult),
        };
    }

    private static JsonObject WriteStandardEvaluationResult(StandardEvaluationResult result)
    {
        return new JsonObject
        {
            [PassedPropertyName] = result.Passed,
            [FailuresPropertyName] = WriteStandardEvaluationFailures(result.Failures),
        };
    }

    private static JsonArray WriteStandardEvaluationFailures(IEnumerable<StandardEvaluationFailure> failures)
    {
        var failureArray = new JsonArray();
        foreach (var failure in failures)
        {
            failureArray.Add(new JsonObject
            {
                [FailureKindPropertyName] = StandardFailureKinds.ToPersistedId(failure.Kind),
                [DetailPropertyName] = failure.Detail,
            });
        }

        return failureArray;
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

    private static LocalMaintenanceCheckRecord ReadMaintenanceRecord(JsonNode? node)
    {
        if (node is not JsonObject recordObject)
        {
            throw new InvalidOperationException("The stored maintenance check record is invalid.");
        }

        return new LocalMaintenanceCheckRecord(
            ReadRequiredString(recordObject, CheckIdPropertyName),
            ReadRequiredString(recordObject, EvidenceArtifactIdPropertyName),
            ReadOptionalString(recordObject, CompletedSessionIdPropertyName),
            ReadOptionalDrill(recordObject),
            ReadRequiredString(recordObject, StandardPropertyName),
            ReadMaintenanceEvidence(ReadRequiredObject(recordObject, EvidencePropertyName)));
    }

    private static LocalRestorationCheckRecord ReadRestorationRecord(JsonNode? node)
    {
        if (node is not JsonObject recordObject)
        {
            throw new InvalidOperationException("The stored restoration check record is invalid.");
        }

        return new LocalRestorationCheckRecord(
            ReadRequiredString(recordObject, CheckIdPropertyName),
            ReadRequiredString(recordObject, EvidenceArtifactIdPropertyName),
            ReadOptionalString(recordObject, CompletedSessionIdPropertyName),
            ReadOptionalDrill(recordObject),
            ReadRequiredString(recordObject, StandardPropertyName),
            ReadRestorationEvidence(ReadRequiredObject(recordObject, EvidencePropertyName)));
    }

    private static MaintenanceCheckEvidence ReadMaintenanceEvidence(JsonObject evidenceObject)
    {
        return new MaintenanceCheckEvidence(
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(evidenceObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(evidenceObject, OwnedLevelPropertyName)),
            ReadDate(ReadRequiredObject(evidenceObject, DatePropertyName)),
            MaintenanceCheckKinds.FromPersistedId(ReadRequiredString(evidenceObject, KindPropertyName)),
            ReadStandardEvaluationResult(ReadRequiredObject(evidenceObject, StandardEvaluationResultPropertyName)));
    }

    private static RestorationCheckEvidence ReadRestorationEvidence(JsonObject evidenceObject)
    {
        return new RestorationCheckEvidence(
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(evidenceObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(evidenceObject, LastOwnedLevelPropertyName)),
            ReadDate(ReadRequiredObject(evidenceObject, DatePropertyName)),
            RestorationCheckKinds.FromPersistedId(ReadRequiredString(evidenceObject, KindPropertyName)),
            ReadStandardEvaluationResult(ReadRequiredObject(evidenceObject, StandardEvaluationResultPropertyName)));
    }

    private static StandardEvaluationResult ReadStandardEvaluationResult(JsonObject resultObject)
    {
        return new StandardEvaluationResult(
            ReadRequiredBoolean(resultObject, PassedPropertyName),
            ReadRequiredArray(resultObject, FailuresPropertyName).Select(ReadStandardEvaluationFailure).ToArray());
    }

    private static StandardEvaluationFailure ReadStandardEvaluationFailure(JsonNode? node)
    {
        if (node is not JsonObject failureObject)
        {
            throw new InvalidOperationException("The stored standard evaluation failure is invalid.");
        }

        return new StandardEvaluationFailure(
            StandardFailureKinds.FromPersistedId(ReadRequiredString(failureObject, FailureKindPropertyName)),
            ReadRequiredString(failureObject, DetailPropertyName));
    }

    private static DrillId? ReadOptionalDrill(JsonObject recordObject)
    {
        if (!recordObject.TryGetPropertyValue(DrillPropertyName, out var drillNode) ||
            drillNode is null)
        {
            return null;
        }

        return StableDomainIdentifiers.Drills.FromPersistedId(drillNode.GetValue<string>());
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
            throw new InvalidOperationException($"The stored maintenance record is missing {propertyName}.");
        }

        return objectNode;
    }

    private static JsonArray ReadRequiredArray(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonArray arrayNode)
        {
            throw new InvalidOperationException($"The stored maintenance record is missing {propertyName}.");
        }

        return arrayNode;
    }

    private static string ReadRequiredString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored maintenance record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored maintenance record has an invalid {propertyName}.",
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
            throw new InvalidOperationException($"The stored maintenance record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored maintenance record has an invalid {propertyName}.",
                exception);
        }
    }

    private static bool ReadRequiredBoolean(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored maintenance record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<bool>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored maintenance record has an invalid {propertyName}.",
                exception);
        }
    }
}
