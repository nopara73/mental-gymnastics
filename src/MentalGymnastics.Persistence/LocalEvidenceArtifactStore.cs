using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public enum LocalProgrammingEventKind
{
    Practice,
    Load,
    FormalTest,
    Stabilization,
    Transfer,
    Maintenance,
    GlobalReview,
}

public sealed class LocalProgrammingEventReference
{
    public LocalProgrammingEventReference(
        string eventId,
        LocalProgrammingEventKind kind,
        BranchCode? branch = null,
        GlobalLevelId? level = null,
        DrillId? drill = null)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("Programming event id is required.", nameof(eventId));
        }

        if (kind is not LocalProgrammingEventKind.GlobalReview &&
            (branch is null || level is null))
        {
            throw new ArgumentException(
                "Branch-level programming events must include branch and level.",
                nameof(branch));
        }

        EventId = eventId;
        Kind = kind;
        Branch = branch;
        Level = level;
        Drill = drill;
    }

    public string EventId { get; }

    public LocalProgrammingEventKind Kind { get; }

    public BranchCode? Branch { get; }

    public GlobalLevelId? Level { get; }

    public DrillId? Drill { get; }
}

public sealed class LocalEvidenceArtifactRecord
{
    public LocalEvidenceArtifactRecord(
        string artifactId,
        LocalProgrammingEventReference @event,
        EvidenceArtifact artifact)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            throw new ArgumentException("Evidence artifact id is required.", nameof(artifactId));
        }

        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(artifact);

        ArtifactId = artifactId;
        Event = @event;
        Artifact = artifact;
    }

    public string ArtifactId { get; }

    public LocalProgrammingEventReference Event { get; }

    public EvidenceArtifact Artifact { get; }
}

public sealed class LocalEvidenceArtifactStore
{
    private const string EvidenceArtifactsPropertyName = "EvidenceArtifacts";
    private const string ArtifactIdPropertyName = "ArtifactId";
    private const string EventPropertyName = "Event";
    private const string EventIdPropertyName = "EventId";
    private const string EventKindPropertyName = "EventKind";
    private const string BranchPropertyName = "Branch";
    private const string LevelPropertyName = "Level";
    private const string DrillPropertyName = "Drill";
    private const string ArtifactPropertyName = "Artifact";
    private const string CategoryPropertyName = "Category";
    private const string DatePropertyName = "Date";
    private const string YearPropertyName = "Year";
    private const string MonthPropertyName = "Month";
    private const string DayPropertyName = "Day";
    private const string ObservableEvidencePropertyName = "ObservableEvidence";
    private const string KindPropertyName = "Kind";
    private const string DescriptionPropertyName = "Description";
    private const string SummaryOrReferencePropertyName = "SummaryOrReference";
    private const string SubjectiveNotePropertyName = "SubjectiveNote";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly IReadOnlyDictionary<LocalProgrammingEventKind, string> EventKindIds =
        new Dictionary<LocalProgrammingEventKind, string>
        {
            [LocalProgrammingEventKind.Practice] = "Practice",
            [LocalProgrammingEventKind.Load] = "Load",
            [LocalProgrammingEventKind.FormalTest] = "FormalTest",
            [LocalProgrammingEventKind.Stabilization] = "Stabilization",
            [LocalProgrammingEventKind.Transfer] = "Transfer",
            [LocalProgrammingEventKind.Maintenance] = "Maintenance",
            [LocalProgrammingEventKind.GlobalReview] = "GlobalReview",
        };

    private static readonly IReadOnlyDictionary<string, LocalProgrammingEventKind> EventKindsById =
        EventKindIds.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<LocalProgrammingEventKind, EvidenceArtifactCategory> ExpectedCategoryByEventKind =
        new Dictionary<LocalProgrammingEventKind, EvidenceArtifactCategory>
        {
            [LocalProgrammingEventKind.Practice] = EvidenceArtifactCategory.Practice,
            [LocalProgrammingEventKind.Load] = EvidenceArtifactCategory.Load,
            [LocalProgrammingEventKind.FormalTest] = EvidenceArtifactCategory.Test,
            [LocalProgrammingEventKind.Stabilization] = EvidenceArtifactCategory.Stabilization,
            [LocalProgrammingEventKind.Transfer] = EvidenceArtifactCategory.Transfer,
            [LocalProgrammingEventKind.Maintenance] = EvidenceArtifactCategory.Maintenance,
            [LocalProgrammingEventKind.GlobalReview] = EvidenceArtifactCategory.GlobalReview,
        };

    private static readonly string[] VagueEvidencePhrases =
    [
        "good effort",
        "great job",
        "nice work",
        "keep going",
        "felt focused",
        "felt meaningful",
        "felt good",
    ];

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalEvidenceArtifactStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask SaveAsync(
        LocalEvidenceArtifactRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ValidateRecord(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var artifacts = ReadArtifactArray(document);
        var replacementIndex = FindArtifactIndex(artifacts, record.ArtifactId);

        if (replacementIndex >= 0)
        {
            artifacts[replacementIndex] = WriteRecord(record);
        }
        else
        {
            artifacts.AddNode(WriteRecord(record));
        }

        document[EvidenceArtifactsPropertyName] = artifacts;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalEvidenceArtifactRecord?> LoadAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            throw new ArgumentException("Evidence artifact id is required.", nameof(artifactId));
        }

        var artifacts = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return artifacts.FirstOrDefault(record => record.ArtifactId == artifactId);
    }

    public ValueTask<IReadOnlyList<LocalEvidenceArtifactRecord>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadRecordsAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<LocalEvidenceArtifactRecord>> ListForEventAsync(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("Programming event id is required.", nameof(eventId));
        }

        var artifacts = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return artifacts
            .Where(record => record.Event.EventId == eventId)
            .ToArray();
    }

    private static void ValidateRecord(LocalEvidenceArtifactRecord record)
    {
        var expectedCategory = ExpectedCategoryByEventKind[record.Event.Kind];
        if (record.Artifact.Category != expectedCategory)
        {
            throw new ArgumentException(
                $"Evidence artifact category {record.Artifact.Category} does not match programming event kind {record.Event.Kind}.",
                nameof(record));
        }

        foreach (var evidence in record.Artifact.ObservableEvidence)
        {
            if (string.IsNullOrWhiteSpace(evidence.Description))
            {
                throw new ArgumentException(
                    "Observable evidence description is required.",
                    nameof(record));
            }

            if (VagueEvidencePhrases.Any(phrase => evidence.Description.Equals(phrase, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    "Observable evidence must constrain interpretation; vague encouragement cannot be persisted as evidence.",
                    nameof(record));
            }
        }
    }

    private async ValueTask<IReadOnlyList<LocalEvidenceArtifactRecord>> ReadRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadArtifactArray(document)
            .Select(ReadRecord)
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

    private static JsonArray ReadArtifactArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(EvidenceArtifactsPropertyName, out var artifactsNode) ||
            artifactsNode is null)
        {
            return [];
        }

        if (artifactsNode is JsonArray artifacts)
        {
            return artifacts;
        }

        throw new InvalidOperationException("The stored evidence artifact list is invalid.");
    }

    private static int FindArtifactIndex(JsonArray artifacts, string artifactId)
    {
        for (var index = 0; index < artifacts.Count; index++)
        {
            if (artifacts[index] is JsonObject artifactObject &&
                artifactObject.TryGetPropertyValue(ArtifactIdPropertyName, out var artifactIdNode) &&
                artifactIdNode?.GetValue<string>() == artifactId)
            {
                return index;
            }
        }

        return -1;
    }

    private static JsonObject WriteRecord(LocalEvidenceArtifactRecord record)
    {
        return new JsonObject
        {
            [ArtifactIdPropertyName] = record.ArtifactId,
            [EventPropertyName] = WriteEvent(record.Event),
            [ArtifactPropertyName] = WriteArtifact(record.Artifact),
        };
    }

    private static JsonObject WriteEvent(LocalProgrammingEventReference eventReference)
    {
        var eventObject = new JsonObject
        {
            [EventIdPropertyName] = eventReference.EventId,
            [EventKindPropertyName] = EventKindIds[eventReference.Kind],
        };

        if (eventReference.Branch is { } branch)
        {
            eventObject[BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(branch);
        }

        if (eventReference.Level is { } level)
        {
            eventObject[LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(level);
        }

        if (eventReference.Drill is { } drill)
        {
            eventObject[DrillPropertyName] = StableDomainIdentifiers.Drills.ToPersistedId(drill);
        }

        return eventObject;
    }

    private static JsonObject WriteArtifact(EvidenceArtifact artifact)
    {
        var evidence = new JsonArray();
        foreach (var item in artifact.ObservableEvidence)
        {
            evidence.AddNode(new JsonObject
            {
                [KindPropertyName] = StableDomainIdentifiers.ObservableEvidenceKinds.ToPersistedId(item.Kind),
                [DescriptionPropertyName] = item.Description,
            });
        }

        var artifactObject = new JsonObject
        {
            [CategoryPropertyName] = StableDomainIdentifiers.EvidenceArtifactCategories.ToPersistedId(artifact.Category),
            [DatePropertyName] = WriteDate(artifact.Date),
            [ObservableEvidencePropertyName] = evidence,
            [SummaryOrReferencePropertyName] = artifact.SummaryOrReference,
        };

        if (artifact.SubjectiveNote is not null)
        {
            artifactObject[SubjectiveNotePropertyName] = artifact.SubjectiveNote;
        }

        return artifactObject;
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

    private static LocalEvidenceArtifactRecord ReadRecord(JsonNode? node)
    {
        if (node is not JsonObject recordObject)
        {
            throw new InvalidOperationException("The stored evidence artifact record is invalid.");
        }

        return new LocalEvidenceArtifactRecord(
            ReadRequiredString(recordObject, ArtifactIdPropertyName),
            ReadEvent(ReadRequiredObject(recordObject, EventPropertyName)),
            ReadArtifact(ReadRequiredObject(recordObject, ArtifactPropertyName)));
    }

    private static LocalProgrammingEventReference ReadEvent(JsonObject eventObject)
    {
        var eventKindId = ReadRequiredString(eventObject, EventKindPropertyName);
        if (!EventKindsById.TryGetValue(eventKindId, out var eventKind))
        {
            throw new InvalidOperationException($"'{eventKindId}' is not a stable persisted identifier for local programming event kind.");
        }

        return new LocalProgrammingEventReference(
            ReadRequiredString(eventObject, EventIdPropertyName),
            eventKind,
            ReadOptionalEnum(eventObject, BranchPropertyName, StableDomainIdentifiers.Branches),
            ReadOptionalEnum(eventObject, LevelPropertyName, StableDomainIdentifiers.Levels),
            ReadOptionalEnum(eventObject, DrillPropertyName, StableDomainIdentifiers.Drills));
    }

    private static EvidenceArtifact ReadArtifact(JsonObject artifactObject)
    {
        var evidenceNode = ReadRequiredArray(artifactObject, ObservableEvidencePropertyName);
        var evidence = evidenceNode
            .Select(ReadObservableEvidence)
            .ToArray();

        return new EvidenceArtifact(
            StableDomainIdentifiers.EvidenceArtifactCategories.FromPersistedId(ReadRequiredString(artifactObject, CategoryPropertyName)),
            ReadDate(ReadRequiredObject(artifactObject, DatePropertyName)),
            evidence,
            ReadRequiredString(artifactObject, SummaryOrReferencePropertyName),
            ReadOptionalString(artifactObject, SubjectiveNotePropertyName));
    }

    private static ObservableEvidence ReadObservableEvidence(JsonNode? evidenceNode)
    {
        if (evidenceNode is not JsonObject evidenceObject)
        {
            throw new InvalidOperationException("The stored observable evidence item is invalid.");
        }

        return new ObservableEvidence(
            StableDomainIdentifiers.ObservableEvidenceKinds.FromPersistedId(ReadRequiredString(evidenceObject, KindPropertyName)),
            ReadRequiredString(evidenceObject, DescriptionPropertyName));
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
            throw new InvalidOperationException($"The stored evidence artifact record is missing {propertyName}.");
        }

        return objectNode;
    }

    private static JsonArray ReadRequiredArray(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonArray arrayNode)
        {
            throw new InvalidOperationException($"The stored evidence artifact record is missing {propertyName}.");
        }

        return arrayNode;
    }

    private static string ReadRequiredString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored evidence artifact record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored evidence artifact record has an invalid {propertyName}.",
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
            throw new InvalidOperationException($"The stored evidence artifact record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored evidence artifact record has an invalid {propertyName}.",
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
