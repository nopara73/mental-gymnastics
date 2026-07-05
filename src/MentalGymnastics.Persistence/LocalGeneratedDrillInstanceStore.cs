using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public enum LocalGeneratedDrillInstanceState
{
    Reserved,
    InSession,
    Completed,
    Abandoned,
}

public sealed class LocalGeneratedDrillContentIdentity
{
    public LocalGeneratedDrillContentIdentity(
        PromptContentIdentity contentIdentity,
        string version)
        : this(
            GetRequiredPromptContentIdentity(contentIdentity).ContentId,
            contentIdentity.Branch,
            contentIdentity.Level,
            contentIdentity.Drill,
            contentIdentity.Kind,
            contentIdentity.EquivalenceClass,
            version)
    {
    }

    [JsonConstructor]
    public LocalGeneratedDrillContentIdentity(
        string contentId,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        PromptContentKind kind,
        string equivalenceClass,
        string version)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            throw new ArgumentException("Generated drill content id is required.", nameof(contentId));
        }

        if (string.IsNullOrWhiteSpace(equivalenceClass))
        {
            throw new ArgumentException(
                "Generated drill content must name the equivalence class that preserves the trained demand.",
                nameof(equivalenceClass));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Generated drill content version is required.", nameof(version));
        }

        ContentId = contentId;
        Branch = branch;
        Level = level;
        Drill = drill;
        Kind = kind;
        EquivalenceClass = equivalenceClass;
        Version = version;
    }

    public string ContentId { get; }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public PromptContentKind Kind { get; }

    public string EquivalenceClass { get; }

    public string Version { get; }

    public PromptContentIdentity ToPromptContentIdentity()
    {
        return new PromptContentIdentity(
            ContentId,
            Branch,
            Level,
            Drill,
            Kind,
            EquivalenceClass);
    }

    public bool IsFreshEquivalentOf(LocalGeneratedDrillContentIdentity other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Branch == other.Branch &&
            Level == other.Level &&
            Drill == other.Drill &&
            Kind == other.Kind &&
            string.Equals(EquivalenceClass, other.EquivalenceClass, StringComparison.Ordinal) &&
            string.Equals(Version, other.Version, StringComparison.Ordinal) &&
            !string.Equals(ContentId, other.ContentId, StringComparison.Ordinal);
    }

    private static PromptContentIdentity GetRequiredPromptContentIdentity(
        PromptContentIdentity contentIdentity)
    {
        ArgumentNullException.ThrowIfNull(contentIdentity);
        return contentIdentity;
    }
}

public sealed class LocalGeneratedDrillAuditMaterial
{
    public LocalGeneratedDrillAuditMaterial(
        string kind,
        string name,
        string value)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Generated drill audit material kind is required.", nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Generated drill audit material name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Generated drill audit material value is required.", nameof(value));
        }

        Kind = kind;
        Name = name;
        Value = value;
    }

    public string Kind { get; }

    public string Name { get; }

    public string Value { get; }
}

public sealed class LocalGeneratedDrillInstanceRecord
{
    public LocalGeneratedDrillInstanceRecord(
        string instanceId,
        TrainingDate generatedOn,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        IEnumerable<LoadVariable> loadVariables,
        LocalGeneratedDrillContentIdentity contentIdentity,
        LocalGeneratedDrillInstanceState state = LocalGeneratedDrillInstanceState.Reserved,
        string? activeSessionId = null,
        string? resultEvidenceArtifactId = null,
        string? contentSummary = null,
        PromptFreshnessPolicy? freshnessPolicy = null,
        IEnumerable<LocalGeneratedDrillAuditMaterial>? auditMaterials = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Generated drill instance id is required.", nameof(instanceId));
        }

        ArgumentNullException.ThrowIfNull(loadVariables);
        ArgumentNullException.ThrowIfNull(contentIdentity);

        var loadVariableArray = loadVariables.ToArray();
        if (loadVariableArray.Length == 0)
        {
            throw new ArgumentException(
                "Generated drill instances must record the load variables they apply.",
                nameof(loadVariables));
        }

        if (loadVariableArray.Any(variable =>
                string.IsNullOrWhiteSpace(variable.Name) ||
                string.IsNullOrWhiteSpace(variable.Value)))
        {
            throw new ArgumentException(
                "Generated drill instance load variables must include a name and value.",
                nameof(loadVariables));
        }

        if (contentIdentity.Branch != branch ||
            contentIdentity.Level != level ||
            contentIdentity.Drill != drill)
        {
            throw new ArgumentException(
                "Generated drill content identity must match the instance branch, level, and drill.",
                nameof(contentIdentity));
        }

        var normalizedActiveSessionId = NormalizeOptionalString(activeSessionId);
        var normalizedResultEvidenceArtifactId = NormalizeOptionalString(resultEvidenceArtifactId);
        var normalizedContentSummary = NormalizeOptionalString(contentSummary);
        var auditMaterialArray = (auditMaterials ?? []).ToArray();
        foreach (var material in auditMaterialArray)
        {
            ArgumentNullException.ThrowIfNull(material);
        }

        if (freshnessPolicy.HasValue)
        {
            EnsureDefined(freshnessPolicy.Value, nameof(freshnessPolicy));
        }

        if (auditMaterialArray.Length > 0 && normalizedContentSummary is null)
        {
            throw new ArgumentException(
                "Generated drill audit material requires a content summary.",
                nameof(contentSummary));
        }

        if (state == LocalGeneratedDrillInstanceState.InSession &&
            normalizedActiveSessionId is null)
        {
            throw new ArgumentException(
                "In-session generated drill instances must reference the active session.",
                nameof(activeSessionId));
        }

        if (state != LocalGeneratedDrillInstanceState.InSession &&
            normalizedActiveSessionId is not null)
        {
            throw new ArgumentException(
                "Only in-session generated drill instances may reference an active session.",
                nameof(activeSessionId));
        }

        if (state == LocalGeneratedDrillInstanceState.Completed &&
            normalizedResultEvidenceArtifactId is null)
        {
            throw new ArgumentException(
                "Completed generated drill instances must reference result evidence.",
                nameof(resultEvidenceArtifactId));
        }

        if (state != LocalGeneratedDrillInstanceState.Completed &&
            normalizedResultEvidenceArtifactId is not null)
        {
            throw new ArgumentException(
                "Only completed generated drill instances may reference result evidence.",
                nameof(resultEvidenceArtifactId));
        }

        InstanceId = instanceId;
        GeneratedOn = generatedOn;
        Branch = branch;
        Level = level;
        Drill = drill;
        LoadVariables = Array.AsReadOnly(loadVariableArray);
        ContentIdentity = contentIdentity;
        State = state;
        ActiveSessionId = normalizedActiveSessionId;
        ResultEvidenceArtifactId = normalizedResultEvidenceArtifactId;
        ContentSummary = normalizedContentSummary;
        FreshnessPolicy = freshnessPolicy;
        AuditMaterials = Array.AsReadOnly(auditMaterialArray);
    }

    public string InstanceId { get; }

    public TrainingDate GeneratedOn { get; }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public LocalGeneratedDrillContentIdentity ContentIdentity { get; }

    public LocalGeneratedDrillInstanceState State { get; }

    public string? ActiveSessionId { get; }

    public string? ResultEvidenceArtifactId { get; }

    public string? ContentSummary { get; }

    public PromptFreshnessPolicy? FreshnessPolicy { get; }

    public IReadOnlyList<LocalGeneratedDrillAuditMaterial> AuditMaterials { get; }

    public bool CanBeReused =>
        State is LocalGeneratedDrillInstanceState.Reserved or LocalGeneratedDrillInstanceState.InSession;

    private static string? NormalizeOptionalString(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown program identifier.");
        }
    }
}

public sealed class LocalGeneratedDrillInstanceStore
{
    private const string GeneratedDrillInstancesPropertyName = "GeneratedDrillInstances";
    private const string InstanceIdPropertyName = "InstanceId";
    private const string GeneratedOnPropertyName = "GeneratedOn";
    private const string YearPropertyName = "Year";
    private const string MonthPropertyName = "Month";
    private const string DayPropertyName = "Day";
    private const string BranchPropertyName = "Branch";
    private const string LevelPropertyName = "Level";
    private const string DrillPropertyName = "Drill";
    private const string LoadVariablesPropertyName = "LoadVariables";
    private const string NamePropertyName = "Name";
    private const string ValuePropertyName = "Value";
    private const string ContentIdentityPropertyName = "ContentIdentity";
    private const string ContentIdPropertyName = "ContentId";
    private const string ContentKindPropertyName = "ContentKind";
    private const string EquivalenceClassPropertyName = "EquivalenceClass";
    private const string VersionPropertyName = "Version";
    private const string FreshnessPolicyPropertyName = "FreshnessPolicy";
    private const string ContentSummaryPropertyName = "ContentSummary";
    private const string AuditMaterialsPropertyName = "AuditMaterials";
    private const string KindPropertyName = "Kind";
    private const string StatePropertyName = "State";
    private const string ActiveSessionIdPropertyName = "ActiveSessionId";
    private const string ResultEvidenceArtifactIdPropertyName = "ResultEvidenceArtifactId";

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

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalGeneratedDrillInstanceStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask SaveAsync(
        LocalGeneratedDrillInstanceRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var instances = ReadInstanceArray(document);
        var replacementIndex = FindInstanceIndex(instances, record.InstanceId);

        if (replacementIndex >= 0)
        {
            instances[replacementIndex] = WriteRecord(record);
        }
        else
        {
            instances.Add(WriteRecord(record));
        }

        document[GeneratedDrillInstancesPropertyName] = instances;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalGeneratedDrillInstanceRecord?> LoadAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Generated drill instance id is required.", nameof(instanceId));
        }

        var instances = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return instances.FirstOrDefault(record => record.InstanceId == instanceId);
    }

    public ValueTask<IReadOnlyList<LocalGeneratedDrillInstanceRecord>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadRecordsAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<LocalGeneratedDrillInstanceRecord>> ListByBranchLevelAsync(
        BranchCode branch,
        GlobalLevelId level,
        CancellationToken cancellationToken = default)
    {
        var instances = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return instances
            .Where(record => record.Branch == branch && record.Level == level)
            .ToArray();
    }

    public async ValueTask<IReadOnlyList<LocalGeneratedDrillInstanceRecord>> ListByDrillAsync(
        DrillId drill,
        CancellationToken cancellationToken = default)
    {
        var instances = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return instances
            .Where(record => record.Drill == drill)
            .ToArray();
    }

    public async ValueTask<IReadOnlyList<LocalGeneratedDrillInstanceRecord>> ListReusableAsync(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        IEnumerable<LoadVariable> loadVariables,
        CancellationToken cancellationToken = default)
    {
        var requestedLoadVariables = ValidateRequestedLoadVariables(loadVariables);
        var instances = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return instances
            .Where(record =>
                record.CanBeReused &&
                record.Branch == branch &&
                record.Level == level &&
                record.Drill == drill &&
                LoadVariablesEqual(record.LoadVariables, requestedLoadVariables))
            .ToArray();
    }

    public async ValueTask<IReadOnlyList<LocalGeneratedDrillInstanceRecord>> ListFreshEquivalentAsync(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        IEnumerable<LoadVariable> loadVariables,
        LocalGeneratedDrillContentIdentity contentIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIdentity);

        if (contentIdentity.Branch != branch ||
            contentIdentity.Level != level ||
            contentIdentity.Drill != drill)
        {
            throw new ArgumentException(
                "Content identity must match the requested branch, level, and drill.",
                nameof(contentIdentity));
        }

        var requestedLoadVariables = ValidateRequestedLoadVariables(loadVariables);
        var instances = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return instances
            .Where(record =>
                record.Branch == branch &&
                record.Level == level &&
                record.Drill == drill &&
                LoadVariablesEqual(record.LoadVariables, requestedLoadVariables) &&
                record.ContentIdentity.IsFreshEquivalentOf(contentIdentity))
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<LocalGeneratedDrillInstanceRecord>> ReadRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadInstanceArray(document)
            .Select(ReadRecord)
            .OrderBy(record => record.GeneratedOn.Year)
            .ThenBy(record => record.GeneratedOn.Month)
            .ThenBy(record => record.GeneratedOn.Day)
            .ThenBy(record => record.InstanceId, StringComparer.Ordinal)
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

    private static JsonArray ReadInstanceArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(GeneratedDrillInstancesPropertyName, out var instancesNode) ||
            instancesNode is null)
        {
            return [];
        }

        if (instancesNode is JsonArray instances)
        {
            return instances;
        }

        throw new InvalidOperationException("The stored generated drill instance history is invalid.");
    }

    private static int FindInstanceIndex(JsonArray instances, string instanceId)
    {
        for (var index = 0; index < instances.Count; index++)
        {
            if (instances[index] is JsonObject instanceObject &&
                instanceObject.TryGetPropertyValue(InstanceIdPropertyName, out var instanceIdNode) &&
                instanceIdNode?.GetValue<string>() == instanceId)
            {
                return index;
            }
        }

        return -1;
    }

    internal static JsonObject WriteRecord(LocalGeneratedDrillInstanceRecord record)
    {
        var recordObject = new JsonObject
        {
            [InstanceIdPropertyName] = record.InstanceId,
            [GeneratedOnPropertyName] = WriteDate(record.GeneratedOn),
            [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(record.Branch),
            [LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(record.Level),
            [DrillPropertyName] = StableDomainIdentifiers.Drills.ToPersistedId(record.Drill),
            [LoadVariablesPropertyName] = WriteLoadVariables(record.LoadVariables),
            [ContentIdentityPropertyName] = WriteContentIdentity(record.ContentIdentity),
            [StatePropertyName] = GeneratedInstanceStates.ToPersistedId(record.State),
        };

        if (record.ActiveSessionId is not null)
        {
            recordObject[ActiveSessionIdPropertyName] = record.ActiveSessionId;
        }

        if (record.ResultEvidenceArtifactId is not null)
        {
            recordObject[ResultEvidenceArtifactIdPropertyName] = record.ResultEvidenceArtifactId;
        }

        if (record.ContentSummary is not null)
        {
            recordObject[ContentSummaryPropertyName] = record.ContentSummary;
        }

        if (record.FreshnessPolicy is { } freshnessPolicy)
        {
            recordObject[FreshnessPolicyPropertyName] =
                StableDomainIdentifiers.PromptFreshnessPolicies.ToPersistedId(freshnessPolicy);
        }

        if (record.AuditMaterials.Count > 0)
        {
            recordObject[AuditMaterialsPropertyName] = WriteAuditMaterials(record.AuditMaterials);
        }

        return recordObject;
    }

    private static JsonObject WriteContentIdentity(LocalGeneratedDrillContentIdentity contentIdentity)
    {
        return new JsonObject
        {
            [ContentIdPropertyName] = contentIdentity.ContentId,
            [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(contentIdentity.Branch),
            [LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(contentIdentity.Level),
            [DrillPropertyName] = StableDomainIdentifiers.Drills.ToPersistedId(contentIdentity.Drill),
            [ContentKindPropertyName] = StableDomainIdentifiers.PromptContentKinds.ToPersistedId(contentIdentity.Kind),
            [EquivalenceClassPropertyName] = contentIdentity.EquivalenceClass,
            [VersionPropertyName] = contentIdentity.Version,
        };
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

    private static JsonArray WriteAuditMaterials(IEnumerable<LocalGeneratedDrillAuditMaterial> materials)
    {
        var materialArray = new JsonArray();
        foreach (var material in materials)
        {
            materialArray.Add(new JsonObject
            {
                [KindPropertyName] = material.Kind,
                [NamePropertyName] = material.Name,
                [ValuePropertyName] = material.Value,
            });
        }

        return materialArray;
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

    private static LocalGeneratedDrillInstanceRecord ReadRecord(JsonNode? node)
    {
        if (node is not JsonObject recordObject)
        {
            throw new InvalidOperationException("The stored generated drill instance record is invalid.");
        }

        return new LocalGeneratedDrillInstanceRecord(
            ReadRequiredString(recordObject, InstanceIdPropertyName),
            ReadDate(ReadRequiredObject(recordObject, GeneratedOnPropertyName)),
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(recordObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(recordObject, LevelPropertyName)),
            StableDomainIdentifiers.Drills.FromPersistedId(ReadRequiredString(recordObject, DrillPropertyName)),
            ReadRequiredArray(recordObject, LoadVariablesPropertyName).Select(ReadLoadVariable),
            ReadContentIdentity(ReadRequiredObject(recordObject, ContentIdentityPropertyName)),
            GeneratedInstanceStates.FromPersistedId(ReadRequiredString(recordObject, StatePropertyName)),
            ReadOptionalString(recordObject, ActiveSessionIdPropertyName),
            ReadOptionalString(recordObject, ResultEvidenceArtifactIdPropertyName),
            ReadOptionalString(recordObject, ContentSummaryPropertyName),
            ReadOptionalEnum(recordObject, FreshnessPolicyPropertyName, StableDomainIdentifiers.PromptFreshnessPolicies),
            ReadAuditMaterials(recordObject));
    }

    private static LocalGeneratedDrillContentIdentity ReadContentIdentity(JsonObject contentObject)
    {
        return new LocalGeneratedDrillContentIdentity(
            ReadRequiredString(contentObject, ContentIdPropertyName),
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(contentObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(contentObject, LevelPropertyName)),
            StableDomainIdentifiers.Drills.FromPersistedId(ReadRequiredString(contentObject, DrillPropertyName)),
            StableDomainIdentifiers.PromptContentKinds.FromPersistedId(ReadRequiredString(contentObject, ContentKindPropertyName)),
            ReadRequiredString(contentObject, EquivalenceClassPropertyName),
            ReadRequiredString(contentObject, VersionPropertyName));
    }

    private static LoadVariable ReadLoadVariable(JsonNode? node)
    {
        if (node is not JsonObject loadVariableObject)
        {
            throw new InvalidOperationException("The stored generated drill instance load variable is invalid.");
        }

        return new LoadVariable(
            ReadRequiredString(loadVariableObject, NamePropertyName),
            ReadRequiredString(loadVariableObject, ValuePropertyName));
    }

    private static IReadOnlyList<LocalGeneratedDrillAuditMaterial> ReadAuditMaterials(JsonObject recordObject)
    {
        if (!recordObject.TryGetPropertyValue(AuditMaterialsPropertyName, out var node) ||
            node is null)
        {
            return [];
        }

        if (node is not JsonArray materialArray)
        {
            throw new InvalidOperationException("The stored generated drill audit material is invalid.");
        }

        return materialArray.Select(ReadAuditMaterial).ToArray();
    }

    private static LocalGeneratedDrillAuditMaterial ReadAuditMaterial(JsonNode? node)
    {
        if (node is not JsonObject materialObject)
        {
            throw new InvalidOperationException("The stored generated drill audit material is invalid.");
        }

        return new LocalGeneratedDrillAuditMaterial(
            ReadRequiredString(materialObject, KindPropertyName),
            ReadRequiredString(materialObject, NamePropertyName),
            ReadRequiredString(materialObject, ValuePropertyName));
    }

    private static TrainingDate ReadDate(JsonObject dateObject)
    {
        return TrainingDate.From(
            ReadRequiredInt32(dateObject, YearPropertyName),
            ReadRequiredInt32(dateObject, MonthPropertyName),
            ReadRequiredInt32(dateObject, DayPropertyName));
    }

    private static IReadOnlyList<LoadVariable> ValidateRequestedLoadVariables(
        IEnumerable<LoadVariable> loadVariables)
    {
        ArgumentNullException.ThrowIfNull(loadVariables);

        var loadVariableArray = loadVariables.ToArray();
        if (loadVariableArray.Length == 0)
        {
            throw new ArgumentException(
                "Generated drill instance queries must include load variables.",
                nameof(loadVariables));
        }

        if (loadVariableArray.Any(variable =>
                string.IsNullOrWhiteSpace(variable.Name) ||
                string.IsNullOrWhiteSpace(variable.Value)))
        {
            throw new ArgumentException(
                "Generated drill instance query load variables must include a name and value.",
                nameof(loadVariables));
        }

        return loadVariableArray;
    }

    private static bool LoadVariablesEqual(
        IReadOnlyList<LoadVariable> left,
        IReadOnlyList<LoadVariable> right)
    {
        return left.SequenceEqual(right);
    }

    private static JsonObject ReadRequiredObject(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonObject objectNode)
        {
            throw new InvalidOperationException($"The stored generated drill instance record is missing {propertyName}.");
        }

        return objectNode;
    }

    private static JsonArray ReadRequiredArray(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonArray arrayNode)
        {
            throw new InvalidOperationException($"The stored generated drill instance record is missing {propertyName}.");
        }

        return arrayNode;
    }

    private static string ReadRequiredString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored generated drill instance record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored generated drill instance record has an invalid {propertyName}.",
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

    private static TEnum? ReadOptionalEnum<TEnum>(
        JsonObject jsonObject,
        string propertyName,
        IStableDomainIdentifierMap<TEnum> map)
        where TEnum : struct, Enum
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            return null;
        }

        return map.FromPersistedId(node.GetValue<string>());
    }

    private static int ReadRequiredInt32(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored generated drill instance record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored generated drill instance record has an invalid {propertyName}.",
                exception);
        }
    }
}
