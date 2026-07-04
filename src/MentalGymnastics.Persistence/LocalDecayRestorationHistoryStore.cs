using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public sealed class LocalDecayHistoryRecord
{
    public LocalDecayHistoryRecord(
        string decayId,
        TrainingDate date,
        BranchLevelStatus currentStatus,
        BranchLevelStatus nextStatus,
        BranchLevelTransition transition,
        IEnumerable<string> maintenanceCheckIds)
    {
        if (string.IsNullOrWhiteSpace(decayId))
        {
            throw new ArgumentException("Decay history id is required.", nameof(decayId));
        }

        ArgumentNullException.ThrowIfNull(maintenanceCheckIds);
        ValidateDecayTransition(currentStatus, nextStatus, transition);

        var maintenanceCheckIdArray = maintenanceCheckIds.ToArray();
        if (maintenanceCheckIdArray.Length == 0)
        {
            throw new ArgumentException(
                "Decay history must reference the failed maintenance check ids that caused decay.",
                nameof(maintenanceCheckIds));
        }

        if (maintenanceCheckIdArray.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Decay history maintenance check ids cannot be blank.",
                nameof(maintenanceCheckIds));
        }

        DecayId = decayId;
        Date = date;
        CurrentStatus = currentStatus;
        NextStatus = nextStatus;
        Transition = transition;
        MaintenanceCheckIds = Array.AsReadOnly(maintenanceCheckIdArray);
    }

    public string DecayId { get; }

    public TrainingDate Date { get; }

    public BranchLevelStatus CurrentStatus { get; }

    public BranchLevelStatus NextStatus { get; }

    public BranchLevelTransition Transition { get; }

    public IReadOnlyList<string> MaintenanceCheckIds { get; }

    public BranchCode Branch => CurrentStatus.Branch;

    public GlobalLevelId Level => CurrentStatus.Level;

    private static void ValidateDecayTransition(
        BranchLevelStatus currentStatus,
        BranchLevelStatus nextStatus,
        BranchLevelTransition transition)
    {
        if (currentStatus.Branch != nextStatus.Branch ||
            currentStatus.Level != nextStatus.Level)
        {
            throw new ArgumentException("Decay history statuses must reference the same branch-level pair.");
        }

        if (transition != BranchLevelTransition.MarkDecayed ||
            currentStatus.State != BranchLevelState.Maintenance ||
            nextStatus.State != BranchLevelState.Decayed)
        {
            throw new ArgumentException("Decay history must record a maintenance-to-decayed transition.");
        }
    }
}

public sealed class LocalRestorationHistoryRecord
{
    public LocalRestorationHistoryRecord(
        string restorationId,
        TrainingDate date,
        BranchLevelStatus currentStatus,
        BranchLevelStatus nextStatus,
        BranchLevelTransition transition,
        IEnumerable<string> restorationCheckIds,
        RestorationEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(restorationId))
        {
            throw new ArgumentException("Restoration history id is required.", nameof(restorationId));
        }

        ArgumentNullException.ThrowIfNull(restorationCheckIds);
        ArgumentNullException.ThrowIfNull(evidence);
        ValidateRestorationTransition(currentStatus, nextStatus, transition);

        var restorationCheckIdArray = restorationCheckIds.ToArray();
        if (restorationCheckIdArray.Length == 0)
        {
            throw new ArgumentException(
                "Restoration history must reference the restoration check ids that restored the branch.",
                nameof(restorationCheckIds));
        }

        if (restorationCheckIdArray.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Restoration history restoration check ids cannot be blank.",
                nameof(restorationCheckIds));
        }

        ValidateRestorationEvidence(currentStatus, nextStatus, evidence);

        RestorationId = restorationId;
        Date = date;
        CurrentStatus = currentStatus;
        NextStatus = nextStatus;
        Transition = transition;
        RestorationCheckIds = Array.AsReadOnly(restorationCheckIdArray);
        Evidence = evidence;
    }

    public string RestorationId { get; }

    public TrainingDate Date { get; }

    public BranchLevelStatus CurrentStatus { get; }

    public BranchLevelStatus NextStatus { get; }

    public BranchLevelTransition Transition { get; }

    public IReadOnlyList<string> RestorationCheckIds { get; }

    public RestorationEvidence Evidence { get; }

    public BranchCode Branch => CurrentStatus.Branch;

    public GlobalLevelId Level => CurrentStatus.Level;

    private static void ValidateRestorationTransition(
        BranchLevelStatus currentStatus,
        BranchLevelStatus nextStatus,
        BranchLevelTransition transition)
    {
        if (currentStatus.Branch != nextStatus.Branch ||
            currentStatus.Level != nextStatus.Level)
        {
            throw new ArgumentException("Restoration history statuses must reference the same branch-level pair.");
        }

        if (transition != BranchLevelTransition.RestoreToMaintenance ||
            currentStatus.State != BranchLevelState.Decayed ||
            nextStatus.State != BranchLevelState.Maintenance)
        {
            throw new ArgumentException("Restoration history must record a decayed-to-maintenance transition.");
        }
    }

    private static void ValidateRestorationEvidence(
        BranchLevelStatus currentStatus,
        BranchLevelStatus nextStatus,
        RestorationEvidence evidence)
    {
        var result = DecayRestorationEvaluator.EvaluateRestoration(currentStatus, evidence);
        if (!result.ChangedState ||
            result.NextStatus != nextStatus ||
            result.Transition != BranchLevelTransition.RestoreToMaintenance)
        {
            throw new ArgumentException(
                "Restoration history must preserve the evidence required to restore the decayed branch.",
                nameof(evidence));
        }
    }
}

public sealed class LocalDecayRestorationHistoryStore
{
    private const string DecayHistoryPropertyName = "DecayHistory";
    private const string RestorationHistoryPropertyName = "RestorationHistory";
    private const string DecayIdPropertyName = "DecayId";
    private const string RestorationIdPropertyName = "RestorationId";
    private const string DatePropertyName = "Date";
    private const string YearPropertyName = "Year";
    private const string MonthPropertyName = "Month";
    private const string DayPropertyName = "Day";
    private const string CurrentStatusPropertyName = "CurrentStatus";
    private const string NextStatusPropertyName = "NextStatus";
    private const string BranchPropertyName = "Branch";
    private const string LevelPropertyName = "Level";
    private const string StatePropertyName = "State";
    private const string TransitionPropertyName = "Transition";
    private const string MaintenanceCheckIdsPropertyName = "MaintenanceCheckIds";
    private const string RestorationCheckIdsPropertyName = "RestorationCheckIds";
    private const string EvidencePropertyName = "Evidence";
    private const string LastOwnedLevelPropertyName = "LastOwnedLevel";
    private const string ChecksPropertyName = "Checks";
    private const string KindPropertyName = "Kind";
    private const string StandardEvaluationResultPropertyName = "StandardEvaluationResult";
    private const string PassedPropertyName = "Passed";
    private const string FailuresPropertyName = "Failures";
    private const string FailureKindPropertyName = "FailureKind";
    private const string DetailPropertyName = "Detail";

    private const string MarkDecayedTransitionId = "MarkDecayed";
    private const string RestoreToMaintenanceTransitionId = "RestoreToMaintenance";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

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

    public LocalDecayRestorationHistoryStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask SaveDecayAsync(
        LocalDecayHistoryRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var history = ReadDecayArray(document);
        var replacementIndex = FindRecordIndex(history, DecayIdPropertyName, record.DecayId);

        if (replacementIndex >= 0)
        {
            history[replacementIndex] = WriteDecayRecord(record);
        }
        else
        {
            history.Add(WriteDecayRecord(record));
        }

        document[DecayHistoryPropertyName] = history;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalDecayHistoryRecord?> LoadDecayAsync(
        string decayId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(decayId))
        {
            throw new ArgumentException("Decay history id is required.", nameof(decayId));
        }

        var history = await ReadDecayRecordsAsync(cancellationToken).ConfigureAwait(false);
        return history.FirstOrDefault(record => record.DecayId == decayId);
    }

    public ValueTask<IReadOnlyList<LocalDecayHistoryRecord>> ListDecaysAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadDecayRecordsAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<LocalDecayHistoryRecord>> ListDecaysByBranchLevelAsync(
        BranchCode branch,
        GlobalLevelId level,
        CancellationToken cancellationToken = default)
    {
        var history = await ReadDecayRecordsAsync(cancellationToken).ConfigureAwait(false);
        return history
            .Where(record => record.Branch == branch && record.Level == level)
            .ToArray();
    }

    public async ValueTask SaveRestorationAsync(
        LocalRestorationHistoryRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var history = ReadRestorationArray(document);
        var replacementIndex = FindRecordIndex(history, RestorationIdPropertyName, record.RestorationId);

        if (replacementIndex >= 0)
        {
            history[replacementIndex] = WriteRestorationRecord(record);
        }
        else
        {
            history.Add(WriteRestorationRecord(record));
        }

        document[RestorationHistoryPropertyName] = history;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalRestorationHistoryRecord?> LoadRestorationAsync(
        string restorationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(restorationId))
        {
            throw new ArgumentException("Restoration history id is required.", nameof(restorationId));
        }

        var history = await ReadRestorationRecordsAsync(cancellationToken).ConfigureAwait(false);
        return history.FirstOrDefault(record => record.RestorationId == restorationId);
    }

    public ValueTask<IReadOnlyList<LocalRestorationHistoryRecord>> ListRestorationsAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadRestorationRecordsAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<LocalRestorationHistoryRecord>> ListRestorationsByBranchLevelAsync(
        BranchCode branch,
        GlobalLevelId level,
        CancellationToken cancellationToken = default)
    {
        var history = await ReadRestorationRecordsAsync(cancellationToken).ConfigureAwait(false);
        return history
            .Where(record => record.Branch == branch && record.Level == level)
            .ToArray();
    }

    public async ValueTask<IReadOnlyList<BranchLevelStatus>> ListActiveDecayedStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        var decays = await ReadDecayRecordsAsync(cancellationToken).ConfigureAwait(false);
        var restorations = await ReadRestorationRecordsAsync(cancellationToken).ConfigureAwait(false);

        return decays
            .Select(record => HistoryMarker.ForDecay(record))
            .Concat(restorations.Select(record => HistoryMarker.ForRestoration(record)))
            .GroupBy(marker => (marker.Status.Branch, marker.Status.Level))
            .Select(group => group
                .OrderBy(marker => marker.Date.Year)
                .ThenBy(marker => marker.Date.Month)
                .ThenBy(marker => marker.Date.Day)
                .ThenBy(marker => marker.KindOrder)
                .Last())
            .Where(marker => marker.IsDecay)
            .Select(marker => marker.Status)
            .OrderBy(status => status.Branch)
            .ThenBy(status => status.Level)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<LocalDecayHistoryRecord>> ReadDecayRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadDecayArray(document)
            .Select(ReadDecayRecord)
            .OrderBy(record => record.Date.Year)
            .ThenBy(record => record.Date.Month)
            .ThenBy(record => record.Date.Day)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<LocalRestorationHistoryRecord>> ReadRestorationRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadRestorationArray(document)
            .Select(ReadRestorationRecord)
            .OrderBy(record => record.Date.Year)
            .ThenBy(record => record.Date.Month)
            .ThenBy(record => record.Date.Day)
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

    private static JsonArray ReadDecayArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(DecayHistoryPropertyName, out var historyNode) ||
            historyNode is null)
        {
            return [];
        }

        if (historyNode is JsonArray history)
        {
            return history;
        }

        throw new InvalidOperationException("The stored decay history is invalid.");
    }

    private static JsonArray ReadRestorationArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(RestorationHistoryPropertyName, out var historyNode) ||
            historyNode is null)
        {
            return [];
        }

        if (historyNode is JsonArray history)
        {
            return history;
        }

        throw new InvalidOperationException("The stored restoration history is invalid.");
    }

    private static int FindRecordIndex(JsonArray records, string idPropertyName, string id)
    {
        for (var index = 0; index < records.Count; index++)
        {
            if (records[index] is JsonObject recordObject &&
                recordObject.TryGetPropertyValue(idPropertyName, out var idNode) &&
                idNode?.GetValue<string>() == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static JsonObject WriteDecayRecord(LocalDecayHistoryRecord record)
    {
        return new JsonObject
        {
            [DecayIdPropertyName] = record.DecayId,
            [DatePropertyName] = WriteDate(record.Date),
            [CurrentStatusPropertyName] = WriteStatus(record.CurrentStatus),
            [NextStatusPropertyName] = WriteStatus(record.NextStatus),
            [TransitionPropertyName] = ToTransitionId(record.Transition),
            [MaintenanceCheckIdsPropertyName] = WriteStringArray(record.MaintenanceCheckIds),
        };
    }

    private static JsonObject WriteRestorationRecord(LocalRestorationHistoryRecord record)
    {
        return new JsonObject
        {
            [RestorationIdPropertyName] = record.RestorationId,
            [DatePropertyName] = WriteDate(record.Date),
            [CurrentStatusPropertyName] = WriteStatus(record.CurrentStatus),
            [NextStatusPropertyName] = WriteStatus(record.NextStatus),
            [TransitionPropertyName] = ToTransitionId(record.Transition),
            [RestorationCheckIdsPropertyName] = WriteStringArray(record.RestorationCheckIds),
            [EvidencePropertyName] = WriteRestorationEvidence(record.Evidence),
        };
    }

    private static JsonObject WriteStatus(BranchLevelStatus status)
    {
        return new JsonObject
        {
            [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(status.Branch),
            [LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(status.Level),
            [StatePropertyName] = StableDomainIdentifiers.BranchLevelStates.ToPersistedId(status.State),
        };
    }

    private static JsonObject WriteRestorationEvidence(RestorationEvidence evidence)
    {
        var checks = new JsonArray();
        foreach (var check in evidence.Checks)
        {
            checks.Add(WriteRestorationCheckEvidence(check));
        }

        return new JsonObject
        {
            [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(evidence.Branch),
            [LastOwnedLevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(evidence.LastOwnedLevel),
            [ChecksPropertyName] = checks,
        };
    }

    private static JsonObject WriteRestorationCheckEvidence(RestorationCheckEvidence check)
    {
        return new JsonObject
        {
            [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(check.Branch),
            [LastOwnedLevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(check.LastOwnedLevel),
            [DatePropertyName] = WriteDate(check.Date),
            [KindPropertyName] = RestorationCheckKinds.ToPersistedId(check.Kind),
            [StandardEvaluationResultPropertyName] = WriteStandardEvaluationResult(check.StandardEvaluationResult),
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

    private static JsonArray WriteStringArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
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

    private static LocalDecayHistoryRecord ReadDecayRecord(JsonNode? node)
    {
        if (node is not JsonObject recordObject)
        {
            throw new InvalidOperationException("The stored decay history record is invalid.");
        }

        return new LocalDecayHistoryRecord(
            ReadRequiredString(recordObject, DecayIdPropertyName),
            ReadDate(ReadRequiredObject(recordObject, DatePropertyName)),
            ReadStatus(ReadRequiredObject(recordObject, CurrentStatusPropertyName)),
            ReadStatus(ReadRequiredObject(recordObject, NextStatusPropertyName)),
            FromTransitionId(ReadRequiredString(recordObject, TransitionPropertyName)),
            ReadRequiredArray(recordObject, MaintenanceCheckIdsPropertyName).Select(ReadStringValue));
    }

    private static LocalRestorationHistoryRecord ReadRestorationRecord(JsonNode? node)
    {
        if (node is not JsonObject recordObject)
        {
            throw new InvalidOperationException("The stored restoration history record is invalid.");
        }

        return new LocalRestorationHistoryRecord(
            ReadRequiredString(recordObject, RestorationIdPropertyName),
            ReadDate(ReadRequiredObject(recordObject, DatePropertyName)),
            ReadStatus(ReadRequiredObject(recordObject, CurrentStatusPropertyName)),
            ReadStatus(ReadRequiredObject(recordObject, NextStatusPropertyName)),
            FromTransitionId(ReadRequiredString(recordObject, TransitionPropertyName)),
            ReadRequiredArray(recordObject, RestorationCheckIdsPropertyName).Select(ReadStringValue),
            ReadRestorationEvidence(ReadRequiredObject(recordObject, EvidencePropertyName)));
    }

    private static BranchLevelStatus ReadStatus(JsonObject statusObject)
    {
        return new BranchLevelStatus(
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(statusObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(statusObject, LevelPropertyName)),
            StableDomainIdentifiers.BranchLevelStates.FromPersistedId(ReadRequiredString(statusObject, StatePropertyName)));
    }

    private static RestorationEvidence ReadRestorationEvidence(JsonObject evidenceObject)
    {
        return new RestorationEvidence(
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(evidenceObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(evidenceObject, LastOwnedLevelPropertyName)),
            ReadRequiredArray(evidenceObject, ChecksPropertyName).Select(ReadRestorationCheckEvidence));
    }

    private static RestorationCheckEvidence ReadRestorationCheckEvidence(JsonNode? node)
    {
        if (node is not JsonObject checkObject)
        {
            throw new InvalidOperationException("The stored restoration check evidence is invalid.");
        }

        return new RestorationCheckEvidence(
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(checkObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(checkObject, LastOwnedLevelPropertyName)),
            ReadDate(ReadRequiredObject(checkObject, DatePropertyName)),
            RestorationCheckKinds.FromPersistedId(ReadRequiredString(checkObject, KindPropertyName)),
            ReadStandardEvaluationResult(ReadRequiredObject(checkObject, StandardEvaluationResultPropertyName)));
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

    private static TrainingDate ReadDate(JsonObject dateObject)
    {
        return TrainingDate.From(
            ReadRequiredInt32(dateObject, YearPropertyName),
            ReadRequiredInt32(dateObject, MonthPropertyName),
            ReadRequiredInt32(dateObject, DayPropertyName));
    }

    private static string ReadStringValue(JsonNode? node)
    {
        if (node is null)
        {
            throw new InvalidOperationException("The stored history reference id is invalid.");
        }

        return node.GetValue<string>();
    }

    private static string ToTransitionId(BranchLevelTransition transition)
    {
        return transition switch
        {
            BranchLevelTransition.MarkDecayed => MarkDecayedTransitionId,
            BranchLevelTransition.RestoreToMaintenance => RestoreToMaintenanceTransitionId,
            _ => throw new ArgumentException("Transition is not supported by decay/restoration history.", nameof(transition)),
        };
    }

    private static BranchLevelTransition FromTransitionId(string transitionId)
    {
        return transitionId switch
        {
            MarkDecayedTransitionId => BranchLevelTransition.MarkDecayed,
            RestoreToMaintenanceTransitionId => BranchLevelTransition.RestoreToMaintenance,
            _ => throw new ArgumentException(
                $"'{transitionId}' is not a supported decay/restoration transition id.",
                nameof(transitionId)),
        };
    }

    private static JsonObject ReadRequiredObject(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonObject objectNode)
        {
            throw new InvalidOperationException($"The stored decay/restoration history record is missing {propertyName}.");
        }

        return objectNode;
    }

    private static JsonArray ReadRequiredArray(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonArray arrayNode)
        {
            throw new InvalidOperationException($"The stored decay/restoration history record is missing {propertyName}.");
        }

        return arrayNode;
    }

    private static string ReadRequiredString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored decay/restoration history record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored decay/restoration history record has an invalid {propertyName}.",
                exception);
        }
    }

    private static int ReadRequiredInt32(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored decay/restoration history record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored decay/restoration history record has an invalid {propertyName}.",
                exception);
        }
    }

    private static bool ReadRequiredBoolean(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored decay/restoration history record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<bool>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored decay/restoration history record has an invalid {propertyName}.",
                exception);
        }
    }

    private sealed record HistoryMarker(
        TrainingDate Date,
        int KindOrder,
        bool IsDecay,
        BranchLevelStatus Status)
    {
        public static HistoryMarker ForDecay(LocalDecayHistoryRecord record)
        {
            return new HistoryMarker(record.Date, KindOrder: 0, IsDecay: true, record.NextStatus);
        }

        public static HistoryMarker ForRestoration(LocalRestorationHistoryRecord record)
        {
            return new HistoryMarker(record.Date, KindOrder: 1, IsDecay: false, record.NextStatus);
        }
    }
}
