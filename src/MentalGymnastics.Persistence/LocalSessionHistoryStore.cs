using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public enum LocalCompletedSessionType
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

public enum LocalSessionIntensity
{
    Low,
    Moderate,
    High,
}

public readonly record struct LocalSessionBranchLevel(
    BranchCode Branch,
    GlobalLevelId Level);

public sealed class LocalSessionHistoryRecord
{
    public LocalSessionHistoryRecord(
        string sessionId,
        TrainingDate date,
        LocalCompletedSessionType sessionType,
        IEnumerable<LocalSessionBranchLevel> branchLevels,
        DrillId? drill,
        string? transferTask,
        LocalSessionIntensity intensity,
        IEnumerable<LoadVariable> loadVariables,
        bool cleanPerformance,
        string notes,
        bool recoveryMarked,
        bool deloadMarked,
        IEnumerable<string> evidenceArtifactIds)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Completed session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(branchLevels);
        ArgumentNullException.ThrowIfNull(loadVariables);
        ArgumentNullException.ThrowIfNull(evidenceArtifactIds);

        var branchLevelArray = branchLevels.ToArray();
        if (branchLevelArray.Length == 0)
        {
            throw new ArgumentException(
                "Completed sessions must reference at least one branch-level pair.",
                nameof(branchLevels));
        }

        if (branchLevelArray.Distinct().Count() != branchLevelArray.Length)
        {
            throw new ArgumentException(
                "Completed sessions cannot contain duplicate branch-level pairs.",
                nameof(branchLevels));
        }

        var transferTaskValue = NormalizeOptionalString(transferTask);
        if (sessionType is LocalCompletedSessionType.Transfer && transferTaskValue is null)
        {
            throw new ArgumentException(
                "Transfer sessions must include the changed transfer context.",
                nameof(transferTask));
        }

        if (sessionType is not LocalCompletedSessionType.Transfer && transferTaskValue is not null)
        {
            throw new ArgumentException(
                "Only transfer sessions may include a transfer task description.",
                nameof(transferTask));
        }

        var loadVariableArray = loadVariables.ToArray();
        if (loadVariableArray.Any(variable =>
                string.IsNullOrWhiteSpace(variable.Name) ||
                string.IsNullOrWhiteSpace(variable.Value)))
        {
            throw new ArgumentException(
                "Completed session load variables must include a name and value.",
                nameof(loadVariables));
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new ArgumentException(
                "Completed session notes are required for later review.",
                nameof(notes));
        }

        var evidenceArtifactIdArray = evidenceArtifactIds.ToArray();
        if (evidenceArtifactIdArray.Length == 0)
        {
            throw new ArgumentException(
                "Completed sessions must reference at least one evidence artifact.",
                nameof(evidenceArtifactIds));
        }

        if (evidenceArtifactIdArray.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Completed session evidence artifact ids cannot be blank.",
                nameof(evidenceArtifactIds));
        }

        SessionId = sessionId;
        Date = date;
        SessionType = sessionType;
        BranchLevels = Array.AsReadOnly(branchLevelArray);
        Drill = drill;
        TransferTask = transferTaskValue;
        Intensity = intensity;
        LoadVariables = Array.AsReadOnly(loadVariableArray);
        CleanPerformance = cleanPerformance;
        Notes = notes;
        RecoveryMarked = recoveryMarked;
        DeloadMarked = deloadMarked;
        EvidenceArtifactIds = Array.AsReadOnly(evidenceArtifactIdArray);
    }

    public string SessionId { get; }

    public TrainingDate Date { get; }

    public LocalCompletedSessionType SessionType { get; }

    public IReadOnlyList<LocalSessionBranchLevel> BranchLevels { get; }

    public DrillId? Drill { get; }

    public string? TransferTask { get; }

    public LocalSessionIntensity Intensity { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public bool CleanPerformance { get; }

    public string Notes { get; }

    public bool RecoveryMarked { get; }

    public bool DeloadMarked { get; }

    public IReadOnlyList<string> EvidenceArtifactIds { get; }

    private static string? NormalizeOptionalString(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public sealed class LocalSessionHistoryStore
{
    private const string CompletedSessionsPropertyName = "CompletedSessions";
    private const string SessionIdPropertyName = "SessionId";
    private const string DatePropertyName = "Date";
    private const string YearPropertyName = "Year";
    private const string MonthPropertyName = "Month";
    private const string DayPropertyName = "Day";
    private const string SessionTypePropertyName = "SessionType";
    private const string BranchLevelsPropertyName = "BranchLevels";
    private const string BranchPropertyName = "Branch";
    private const string LevelPropertyName = "Level";
    private const string DrillPropertyName = "Drill";
    private const string TransferTaskPropertyName = "TransferTask";
    private const string IntensityPropertyName = "Intensity";
    private const string LoadVariablesPropertyName = "LoadVariables";
    private const string NamePropertyName = "Name";
    private const string ValuePropertyName = "Value";
    private const string CleanPerformancePropertyName = "CleanPerformance";
    private const string NotesPropertyName = "Notes";
    private const string RecoveryMarkedPropertyName = "RecoveryMarked";
    private const string DeloadMarkedPropertyName = "DeloadMarked";
    private const string EvidenceArtifactIdsPropertyName = "EvidenceArtifactIds";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly IStableDomainIdentifierMap<LocalCompletedSessionType> CompletedSessionTypes =
        new StableDomainIdentifierMap<LocalCompletedSessionType>(new Dictionary<LocalCompletedSessionType, string>
        {
            [LocalCompletedSessionType.Practice] = "Practice",
            [LocalCompletedSessionType.Load] = "Load",
            [LocalCompletedSessionType.Test] = "Test",
            [LocalCompletedSessionType.Stabilization] = "Stabilization",
            [LocalCompletedSessionType.Regression] = "Regression",
            [LocalCompletedSessionType.Transfer] = "Transfer",
            [LocalCompletedSessionType.Recovery] = "Recovery",
            [LocalCompletedSessionType.Maintenance] = "Maintenance",
        });

    private static readonly IStableDomainIdentifierMap<LocalSessionIntensity> SessionIntensities =
        new StableDomainIdentifierMap<LocalSessionIntensity>(new Dictionary<LocalSessionIntensity, string>
        {
            [LocalSessionIntensity.Low] = "Low",
            [LocalSessionIntensity.Moderate] = "Moderate",
            [LocalSessionIntensity.High] = "High",
        });

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalSessionHistoryStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask SaveAsync(
        LocalSessionHistoryRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var sessions = ReadSessionArray(document);
        var replacementIndex = FindSessionIndex(sessions, record.SessionId);

        if (replacementIndex >= 0)
        {
            sessions[replacementIndex] = WriteRecord(record);
        }
        else
        {
            sessions.Add(WriteRecord(record));
        }

        document[CompletedSessionsPropertyName] = sessions;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalSessionHistoryRecord?> LoadAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Completed session id is required.", nameof(sessionId));
        }

        var sessions = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return sessions.FirstOrDefault(record => record.SessionId == sessionId);
    }

    public ValueTask<IReadOnlyList<LocalSessionHistoryRecord>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadRecordsAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<LocalSessionHistoryRecord>> ListByBranchLevelAsync(
        BranchCode branch,
        GlobalLevelId level,
        CancellationToken cancellationToken = default)
    {
        var sessions = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return sessions
            .Where(record => record.BranchLevels.Any(branchLevel =>
                branchLevel.Branch == branch &&
                branchLevel.Level == level))
            .ToArray();
    }

    public async ValueTask<IReadOnlyList<LocalSessionHistoryRecord>> ListBySessionTypeAsync(
        LocalCompletedSessionType sessionType,
        CancellationToken cancellationToken = default)
    {
        var sessions = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return sessions
            .Where(record => record.SessionType == sessionType)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<LocalSessionHistoryRecord>> ReadRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadSessionArray(document)
            .Select(ReadRecord)
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

    private static JsonArray ReadSessionArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(CompletedSessionsPropertyName, out var sessionsNode) ||
            sessionsNode is null)
        {
            return [];
        }

        if (sessionsNode is JsonArray sessions)
        {
            return sessions;
        }

        throw new InvalidOperationException("The stored completed session history is invalid.");
    }

    private static int FindSessionIndex(JsonArray sessions, string sessionId)
    {
        for (var index = 0; index < sessions.Count; index++)
        {
            if (sessions[index] is JsonObject sessionObject &&
                sessionObject.TryGetPropertyValue(SessionIdPropertyName, out var sessionIdNode) &&
                sessionIdNode?.GetValue<string>() == sessionId)
            {
                return index;
            }
        }

        return -1;
    }

    private static JsonObject WriteRecord(LocalSessionHistoryRecord record)
    {
        var recordObject = new JsonObject
        {
            [SessionIdPropertyName] = record.SessionId,
            [DatePropertyName] = WriteDate(record.Date),
            [SessionTypePropertyName] = CompletedSessionTypes.ToPersistedId(record.SessionType),
            [BranchLevelsPropertyName] = WriteBranchLevels(record.BranchLevels),
            [IntensityPropertyName] = SessionIntensities.ToPersistedId(record.Intensity),
            [LoadVariablesPropertyName] = WriteLoadVariables(record.LoadVariables),
            [CleanPerformancePropertyName] = record.CleanPerformance,
            [NotesPropertyName] = record.Notes,
            [RecoveryMarkedPropertyName] = record.RecoveryMarked,
            [DeloadMarkedPropertyName] = record.DeloadMarked,
            [EvidenceArtifactIdsPropertyName] = WriteEvidenceArtifactIds(record.EvidenceArtifactIds),
        };

        if (record.Drill is { } drill)
        {
            recordObject[DrillPropertyName] = StableDomainIdentifiers.Drills.ToPersistedId(drill);
        }

        if (record.TransferTask is not null)
        {
            recordObject[TransferTaskPropertyName] = record.TransferTask;
        }

        return recordObject;
    }

    private static JsonArray WriteBranchLevels(IEnumerable<LocalSessionBranchLevel> branchLevels)
    {
        var branchLevelArray = new JsonArray();
        foreach (var branchLevel in branchLevels)
        {
            branchLevelArray.Add(new JsonObject
            {
                [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(branchLevel.Branch),
                [LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(branchLevel.Level),
            });
        }

        return branchLevelArray;
    }

    private static JsonArray WriteLoadVariables(IEnumerable<LoadVariable> loadVariables)
    {
        var loadVariableArray = new JsonArray();
        foreach (var loadVariable in loadVariables)
        {
            loadVariableArray.Add(new JsonObject
            {
                [NamePropertyName] = loadVariable.Name,
                [ValuePropertyName] = loadVariable.Value,
            });
        }

        return loadVariableArray;
    }

    private static JsonArray WriteEvidenceArtifactIds(IEnumerable<string> evidenceArtifactIds)
    {
        var artifactIdArray = new JsonArray();
        foreach (var artifactId in evidenceArtifactIds)
        {
            artifactIdArray.Add(artifactId);
        }

        return artifactIdArray;
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

    private static LocalSessionHistoryRecord ReadRecord(JsonNode? node)
    {
        if (node is not JsonObject recordObject)
        {
            throw new InvalidOperationException("The stored completed session record is invalid.");
        }

        return new LocalSessionHistoryRecord(
            ReadRequiredString(recordObject, SessionIdPropertyName),
            ReadDate(ReadRequiredObject(recordObject, DatePropertyName)),
            CompletedSessionTypes.FromPersistedId(ReadRequiredString(recordObject, SessionTypePropertyName)),
            ReadRequiredArray(recordObject, BranchLevelsPropertyName).Select(ReadBranchLevel),
            ReadOptionalDrill(recordObject),
            ReadOptionalString(recordObject, TransferTaskPropertyName),
            SessionIntensities.FromPersistedId(ReadRequiredString(recordObject, IntensityPropertyName)),
            ReadRequiredArray(recordObject, LoadVariablesPropertyName).Select(ReadLoadVariable),
            ReadRequiredBoolean(recordObject, CleanPerformancePropertyName),
            ReadRequiredString(recordObject, NotesPropertyName),
            ReadRequiredBoolean(recordObject, RecoveryMarkedPropertyName),
            ReadRequiredBoolean(recordObject, DeloadMarkedPropertyName),
            ReadRequiredArray(recordObject, EvidenceArtifactIdsPropertyName).Select(ReadEvidenceArtifactId));
    }

    private static LocalSessionBranchLevel ReadBranchLevel(JsonNode? node)
    {
        if (node is not JsonObject branchLevelObject)
        {
            throw new InvalidOperationException("The stored session branch-level reference is invalid.");
        }

        return new LocalSessionBranchLevel(
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(branchLevelObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(branchLevelObject, LevelPropertyName)));
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

    private static LoadVariable ReadLoadVariable(JsonNode? node)
    {
        if (node is not JsonObject loadVariableObject)
        {
            throw new InvalidOperationException("The stored session load variable is invalid.");
        }

        return new LoadVariable(
            ReadRequiredString(loadVariableObject, NamePropertyName),
            ReadRequiredString(loadVariableObject, ValuePropertyName));
    }

    private static string ReadEvidenceArtifactId(JsonNode? node)
    {
        if (node is null)
        {
            throw new InvalidOperationException("The stored session evidence artifact id is invalid.");
        }

        return node.GetValue<string>();
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
            throw new InvalidOperationException($"The stored completed session record is missing {propertyName}.");
        }

        return objectNode;
    }

    private static JsonArray ReadRequiredArray(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonArray arrayNode)
        {
            throw new InvalidOperationException($"The stored completed session record is missing {propertyName}.");
        }

        return arrayNode;
    }

    private static string ReadRequiredString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored completed session record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored completed session record has an invalid {propertyName}.",
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
            throw new InvalidOperationException($"The stored completed session record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored completed session record has an invalid {propertyName}.",
                exception);
        }
    }

    private static bool ReadRequiredBoolean(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored completed session record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<bool>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored completed session record has an invalid {propertyName}.",
                exception);
        }
    }
}
