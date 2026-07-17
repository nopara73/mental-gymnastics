using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public sealed class LocalFormalTestAttemptRecord
{
    public LocalFormalTestAttemptRecord(
        string attemptId,
        string? evidenceArtifactId,
        FormalTestAttempt attempt)
    {
        if (string.IsNullOrWhiteSpace(attemptId))
        {
            throw new ArgumentException("Formal test attempt id is required.", nameof(attemptId));
        }

        if (evidenceArtifactId is not null && string.IsNullOrWhiteSpace(evidenceArtifactId))
        {
            throw new ArgumentException("Evidence artifact id cannot be blank.", nameof(evidenceArtifactId));
        }

        ArgumentNullException.ThrowIfNull(attempt);

        AttemptId = attemptId;
        EvidenceArtifactId = evidenceArtifactId;
        Attempt = attempt;
    }

    public string AttemptId { get; }

    public string? EvidenceArtifactId { get; }

    public FormalTestAttempt Attempt { get; }
}

public sealed class LocalFormalTestAttemptStore
{
    private const string FormalTestAttemptsPropertyName = "FormalTestAttempts";
    private const string AttemptIdPropertyName = "AttemptId";
    private const string EvidenceArtifactIdPropertyName = "EvidenceArtifactId";
    private const string AttemptPropertyName = "Attempt";
    private const string BranchPropertyName = "Branch";
    private const string LevelPropertyName = "Level";
    private const string DatePropertyName = "Date";
    private const string YearPropertyName = "Year";
    private const string MonthPropertyName = "Month";
    private const string DayPropertyName = "Day";
    private const string TaskPropertyName = "Task";
    private const string DrillPropertyName = "Drill";
    private const string TransferTaskPropertyName = "TransferTask";
    private const string LoadVariablesPropertyName = "LoadVariables";
    private const string NamePropertyName = "Name";
    private const string ValuePropertyName = "Value";
    private const string StandardPropertyName = "Standard";
    private const string CriticalConstraintsPropertyName = "CriticalConstraints";
    private const string DescriptionPropertyName = "Description";
    private const string ResultEvidencePropertyName = "ResultEvidence";
    private const string ResultKindPropertyName = "ResultKind";
    private const string ResultValuePropertyName = "ResultValue";
    private const string FailureTypePropertyName = "FailureType";
    private const string PassStatePropertyName = "PassState";
    private const string ArtifactPropertyName = "Artifact";
    private const string CategoryPropertyName = "Category";
    private const string ObservableEvidencePropertyName = "ObservableEvidence";
    private const string KindPropertyName = "Kind";
    private const string SummaryOrReferencePropertyName = "SummaryOrReference";
    private const string SubjectiveNotePropertyName = "SubjectiveNote";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalFormalTestAttemptStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask SaveAsync(
        LocalFormalTestAttemptRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var attempts = ReadAttemptArray(document);
        var replacementIndex = FindAttemptIndex(attempts, record.AttemptId);

        if (replacementIndex >= 0)
        {
            attempts[replacementIndex] = WriteRecord(record);
        }
        else
        {
            attempts.AddNode(WriteRecord(record));
        }

        document[FormalTestAttemptsPropertyName] = attempts;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalFormalTestAttemptRecord?> LoadAsync(
        string attemptId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(attemptId))
        {
            throw new ArgumentException("Formal test attempt id is required.", nameof(attemptId));
        }

        var attempts = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return attempts.FirstOrDefault(record => record.AttemptId == attemptId);
    }

    public ValueTask<IReadOnlyList<LocalFormalTestAttemptRecord>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadRecordsAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<LocalFormalTestAttemptRecord>> ListByBranchLevelAsync(
        BranchCode branch,
        GlobalLevelId level,
        CancellationToken cancellationToken = default)
    {
        var attempts = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return attempts
            .Where(record => record.Attempt.Branch == branch && record.Attempt.Level == level)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<LocalFormalTestAttemptRecord>> ReadRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadAttemptArray(document)
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

    private static JsonArray ReadAttemptArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(FormalTestAttemptsPropertyName, out var attemptsNode) ||
            attemptsNode is null)
        {
            return [];
        }

        if (attemptsNode is JsonArray attempts)
        {
            return attempts;
        }

        throw new InvalidOperationException("The stored formal test attempt history is invalid.");
    }

    private static int FindAttemptIndex(JsonArray attempts, string attemptId)
    {
        for (var index = 0; index < attempts.Count; index++)
        {
            if (attempts[index] is JsonObject attemptObject &&
                attemptObject.TryGetPropertyValue(AttemptIdPropertyName, out var attemptIdNode) &&
                attemptIdNode?.GetValue<string>() == attemptId)
            {
                return index;
            }
        }

        return -1;
    }

    private static JsonObject WriteRecord(LocalFormalTestAttemptRecord record)
    {
        var recordObject = new JsonObject
        {
            [AttemptIdPropertyName] = record.AttemptId,
            [AttemptPropertyName] = WriteAttempt(record.Attempt),
        };

        if (record.EvidenceArtifactId is not null)
        {
            recordObject[EvidenceArtifactIdPropertyName] = record.EvidenceArtifactId;
        }

        return recordObject;
    }

    private static JsonObject WriteAttempt(FormalTestAttempt attempt)
    {
        var attemptObject = new JsonObject
        {
            [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(attempt.Branch),
            [LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(attempt.Level),
            [DatePropertyName] = WriteDate(attempt.Date),
            [TaskPropertyName] = WriteTask(attempt.Task),
            [LoadVariablesPropertyName] = WriteLoadVariables(attempt.LoadVariables),
            [StandardPropertyName] = attempt.Standard,
            [CriticalConstraintsPropertyName] = WriteCriticalConstraints(attempt.CriticalConstraints),
            [ResultEvidencePropertyName] = WriteResultEvidence(attempt.ResultEvidence),
            [PassStatePropertyName] = StableDomainIdentifiers.FormalTestPassStates.ToPersistedId(attempt.PassState),
            [ArtifactPropertyName] = WriteArtifact(attempt.Artifact),
        };

        if (attempt.FailureType is { } failureType)
        {
            attemptObject[FailureTypePropertyName] = StableDomainIdentifiers.FailureTypes.ToPersistedId(failureType);
        }

        return attemptObject;
    }

    private static JsonObject WriteTask(TestTask task)
    {
        var taskObject = new JsonObject();

        if (task.Drill is { } drill)
        {
            taskObject[DrillPropertyName] = StableDomainIdentifiers.Drills.ToPersistedId(drill);
        }

        if (task.TransferTask is not null)
        {
            taskObject[TransferTaskPropertyName] = task.TransferTask;
        }

        return taskObject;
    }

    private static JsonArray WriteLoadVariables(IEnumerable<LoadVariable> loadVariables)
    {
        var loadVariableArray = new JsonArray();
        foreach (var loadVariable in loadVariables)
        {
            loadVariableArray.AddNode(new JsonObject
            {
                [NamePropertyName] = loadVariable.Name,
                [ValuePropertyName] = loadVariable.Value,
            });
        }

        return loadVariableArray;
    }

    private static JsonArray WriteCriticalConstraints(IEnumerable<CriticalConstraint> constraints)
    {
        var constraintArray = new JsonArray();
        foreach (var constraint in constraints)
        {
            constraintArray.AddNode(new JsonObject
            {
                [DescriptionPropertyName] = constraint.Description,
            });
        }

        return constraintArray;
    }

    private static JsonObject WriteResultEvidence(TestResultEvidence resultEvidence)
    {
        return new JsonObject
        {
            [ResultKindPropertyName] = StableDomainIdentifiers.TestResultEvidenceKinds.ToPersistedId(resultEvidence.Kind),
            [ResultValuePropertyName] = resultEvidence.Value,
        };
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

    private static LocalFormalTestAttemptRecord ReadRecord(JsonNode? node)
    {
        if (node is not JsonObject recordObject)
        {
            throw new InvalidOperationException("The stored formal test attempt record is invalid.");
        }

        return new LocalFormalTestAttemptRecord(
            ReadRequiredString(recordObject, AttemptIdPropertyName),
            ReadOptionalString(recordObject, EvidenceArtifactIdPropertyName),
            ReadAttempt(ReadRequiredObject(recordObject, AttemptPropertyName)));
    }

    private static FormalTestAttempt ReadAttempt(JsonObject attemptObject)
    {
        return new FormalTestAttempt(
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(attemptObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(attemptObject, LevelPropertyName)),
            ReadDate(ReadRequiredObject(attemptObject, DatePropertyName)),
            ReadTask(ReadRequiredObject(attemptObject, TaskPropertyName)),
            ReadRequiredArray(attemptObject, LoadVariablesPropertyName).Select(ReadLoadVariable),
            ReadRequiredString(attemptObject, StandardPropertyName),
            ReadRequiredArray(attemptObject, CriticalConstraintsPropertyName).Select(ReadCriticalConstraint),
            ReadResultEvidence(ReadRequiredObject(attemptObject, ResultEvidencePropertyName)),
            ReadOptionalEnum(attemptObject, FailureTypePropertyName, StableDomainIdentifiers.FailureTypes),
            StableDomainIdentifiers.FormalTestPassStates.FromPersistedId(ReadRequiredString(attemptObject, PassStatePropertyName)),
            ReadArtifact(ReadRequiredObject(attemptObject, ArtifactPropertyName)));
    }

    private static TestTask ReadTask(JsonObject taskObject)
    {
        if (taskObject.TryGetPropertyValue(DrillPropertyName, out var drillNode) &&
            drillNode is not null)
        {
            return TestTask.ForDrill(StableDomainIdentifiers.Drills.FromPersistedId(drillNode.GetValue<string>()));
        }

        if (taskObject.TryGetPropertyValue(TransferTaskPropertyName, out var transferTaskNode) &&
            transferTaskNode is not null)
        {
            return TestTask.ForTransfer(transferTaskNode.GetValue<string>());
        }

        throw new InvalidOperationException("The stored formal test attempt task is missing drill or transfer task.");
    }

    private static LoadVariable ReadLoadVariable(JsonNode? node)
    {
        if (node is not JsonObject loadVariableObject)
        {
            throw new InvalidOperationException("The stored load variable is invalid.");
        }

        return new LoadVariable(
            ReadRequiredString(loadVariableObject, NamePropertyName),
            ReadRequiredString(loadVariableObject, ValuePropertyName));
    }

    private static CriticalConstraint ReadCriticalConstraint(JsonNode? node)
    {
        if (node is not JsonObject constraintObject)
        {
            throw new InvalidOperationException("The stored critical constraint is invalid.");
        }

        return new CriticalConstraint(ReadRequiredString(constraintObject, DescriptionPropertyName));
    }

    private static TestResultEvidence ReadResultEvidence(JsonObject resultEvidenceObject)
    {
        return new TestResultEvidence(
            StableDomainIdentifiers.TestResultEvidenceKinds.FromPersistedId(ReadRequiredString(resultEvidenceObject, ResultKindPropertyName)),
            ReadRequiredString(resultEvidenceObject, ResultValuePropertyName));
    }

    private static EvidenceArtifact ReadArtifact(JsonObject artifactObject)
    {
        var evidence = ReadRequiredArray(artifactObject, ObservableEvidencePropertyName)
            .Select(ReadObservableEvidence)
            .ToArray();

        return new EvidenceArtifact(
            StableDomainIdentifiers.EvidenceArtifactCategories.FromPersistedId(ReadRequiredString(artifactObject, CategoryPropertyName)),
            ReadDate(ReadRequiredObject(artifactObject, DatePropertyName)),
            evidence,
            ReadRequiredString(artifactObject, SummaryOrReferencePropertyName),
            ReadOptionalString(artifactObject, SubjectiveNotePropertyName));
    }

    private static ObservableEvidence ReadObservableEvidence(JsonNode? node)
    {
        if (node is not JsonObject evidenceObject)
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
            throw new InvalidOperationException($"The stored formal test attempt record is missing {propertyName}.");
        }

        return objectNode;
    }

    private static JsonArray ReadRequiredArray(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonArray arrayNode)
        {
            throw new InvalidOperationException($"The stored formal test attempt record is missing {propertyName}.");
        }

        return arrayNode;
    }

    private static string ReadRequiredString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored formal test attempt record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored formal test attempt record has an invalid {propertyName}.",
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
            throw new InvalidOperationException($"The stored formal test attempt record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored formal test attempt record has an invalid {propertyName}.",
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
