using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public enum LocalStabilizationCondition
{
    OrdinaryVariance,
    AdjacentWork,
    ControlledDistractor,
}

public sealed class LocalStabilizationPassRecord
{
    public LocalStabilizationPassRecord(
        string passId,
        string evidenceArtifactId,
        string? formalTestAttemptId,
        string? completedSessionId,
        DrillId? drill,
        LocalStabilizationCondition condition,
        string conditionDescription,
        StabilizationPassEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(passId))
        {
            throw new ArgumentException("Stabilization pass id is required.", nameof(passId));
        }

        if (string.IsNullOrWhiteSpace(evidenceArtifactId))
        {
            throw new ArgumentException("Evidence artifact id is required.", nameof(evidenceArtifactId));
        }

        if (formalTestAttemptId is not null && string.IsNullOrWhiteSpace(formalTestAttemptId))
        {
            throw new ArgumentException("Formal test attempt id cannot be blank.", nameof(formalTestAttemptId));
        }

        if (completedSessionId is not null && string.IsNullOrWhiteSpace(completedSessionId))
        {
            throw new ArgumentException("Completed session id cannot be blank.", nameof(completedSessionId));
        }

        if (string.IsNullOrWhiteSpace(conditionDescription))
        {
            throw new ArgumentException(
                "Stabilization condition description is required.",
                nameof(conditionDescription));
        }

        ArgumentNullException.ThrowIfNull(evidence);
        ValidatePassEvidence(condition, evidence);

        PassId = passId;
        EvidenceArtifactId = evidenceArtifactId;
        FormalTestAttemptId = formalTestAttemptId;
        CompletedSessionId = completedSessionId;
        Drill = drill;
        Condition = condition;
        ConditionDescription = conditionDescription;
        Evidence = evidence;
    }

    public string PassId { get; }

    public string EvidenceArtifactId { get; }

    public string? FormalTestAttemptId { get; }

    public string? CompletedSessionId { get; }

    public DrillId? Drill { get; }

    public LocalStabilizationCondition Condition { get; }

    public string ConditionDescription { get; }

    public StabilizationPassEvidence Evidence { get; }

    private static void ValidatePassEvidence(
        LocalStabilizationCondition condition,
        StabilizationPassEvidence evidence)
    {
        if (!evidence.StandardEvaluationResult.Passed ||
            evidence.StandardEvaluationResult.Failures.Count != 0)
        {
            throw new ArgumentException(
                "Stabilization pass records require clean standard evidence.",
                nameof(evidence));
        }

        if (evidence.PassState is not (FormalTestPassState.PassOnce or FormalTestPassState.StabilizationPass))
        {
            throw new ArgumentException(
                "Stabilization pass records must be pass-once or stabilization-pass evidence.",
                nameof(evidence));
        }

        var conditionRequiresDemand = condition is
            LocalStabilizationCondition.AdjacentWork or
            LocalStabilizationCondition.ControlledDistractor;

        if (conditionRequiresDemand != evidence.AfterAdjacentWorkOrControlledDistractor)
        {
            throw new ArgumentException(
                "The stabilization condition must match whether the pass followed adjacent work or a controlled distractor.",
                nameof(evidence));
        }
    }
}

public sealed class LocalStabilizationPassStore
{
    private const string StabilizationPassesPropertyName = "StabilizationPasses";
    private const string PassIdPropertyName = "PassId";
    private const string EvidenceArtifactIdPropertyName = "EvidenceArtifactId";
    private const string FormalTestAttemptIdPropertyName = "FormalTestAttemptId";
    private const string CompletedSessionIdPropertyName = "CompletedSessionId";
    private const string DrillPropertyName = "Drill";
    private const string ConditionPropertyName = "Condition";
    private const string ConditionDescriptionPropertyName = "ConditionDescription";
    private const string EvidencePropertyName = "Evidence";
    private const string BranchPropertyName = "Branch";
    private const string LevelPropertyName = "Level";
    private const string DatePropertyName = "Date";
    private const string YearPropertyName = "Year";
    private const string MonthPropertyName = "Month";
    private const string DayPropertyName = "Day";
    private const string StandardPropertyName = "Standard";
    private const string PassStatePropertyName = "PassState";
    private const string StandardEvaluationResultPropertyName = "StandardEvaluationResult";
    private const string PassedPropertyName = "Passed";
    private const string FailuresPropertyName = "Failures";
    private const string FailureKindPropertyName = "FailureKind";
    private const string DetailPropertyName = "Detail";
    private const string AfterAdjacentWorkOrControlledDistractorPropertyName = "AfterAdjacentWorkOrControlledDistractor";
    private const string MainFailureModeAvoidedPropertyName = "MainFailureModeAvoided";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly IStableDomainIdentifierMap<LocalStabilizationCondition> StabilizationConditions =
        new StableDomainIdentifierMap<LocalStabilizationCondition>(new Dictionary<LocalStabilizationCondition, string>
        {
            [LocalStabilizationCondition.OrdinaryVariance] = "OrdinaryVariance",
            [LocalStabilizationCondition.AdjacentWork] = "AdjacentWork",
            [LocalStabilizationCondition.ControlledDistractor] = "ControlledDistractor",
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

    public LocalStabilizationPassStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask SaveAsync(
        LocalStabilizationPassRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var passes = ReadPassArray(document);
        var replacementIndex = FindPassIndex(passes, record.PassId);

        if (replacementIndex >= 0)
        {
            passes[replacementIndex] = WriteRecord(record);
        }
        else
        {
            passes.AddNode(WriteRecord(record));
        }

        document[StabilizationPassesPropertyName] = passes;
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalStabilizationPassRecord?> LoadAsync(
        string passId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(passId))
        {
            throw new ArgumentException("Stabilization pass id is required.", nameof(passId));
        }

        var passes = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return passes.FirstOrDefault(record => record.PassId == passId);
    }

    public ValueTask<IReadOnlyList<LocalStabilizationPassRecord>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadRecordsAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<LocalStabilizationPassRecord>> ListByBranchLevelAsync(
        BranchCode branch,
        GlobalLevelId level,
        CancellationToken cancellationToken = default)
    {
        var passes = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        return passes
            .Where(record => record.Evidence.Branch == branch && record.Evidence.Level == level)
            .ToArray();
    }

    public async ValueTask<StabilizationEvidence> LoadEvidenceAsync(
        BranchCode branch,
        GlobalLevelId level,
        CancellationToken cancellationToken = default)
    {
        var passes = await ListByBranchLevelAsync(branch, level, cancellationToken).ConfigureAwait(false);
        return new StabilizationEvidence(
            branch,
            level,
            passes.Select(record => record.Evidence));
    }

    private async ValueTask<IReadOnlyList<LocalStabilizationPassRecord>> ReadRecordsAsync(
        CancellationToken cancellationToken)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadPassArray(document)
            .Select(ReadRecord)
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

    private static JsonArray ReadPassArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(StabilizationPassesPropertyName, out var passesNode) ||
            passesNode is null)
        {
            return [];
        }

        if (passesNode is JsonArray passes)
        {
            return passes;
        }

        throw new InvalidOperationException("The stored stabilization pass history is invalid.");
    }

    private static int FindPassIndex(JsonArray passes, string passId)
    {
        for (var index = 0; index < passes.Count; index++)
        {
            if (passes[index] is JsonObject passObject &&
                passObject.TryGetPropertyValue(PassIdPropertyName, out var passIdNode) &&
                passIdNode?.GetValue<string>() == passId)
            {
                return index;
            }
        }

        return -1;
    }

    internal static JsonObject WriteRecord(LocalStabilizationPassRecord record)
    {
        var recordObject = new JsonObject
        {
            [PassIdPropertyName] = record.PassId,
            [EvidenceArtifactIdPropertyName] = record.EvidenceArtifactId,
            [ConditionPropertyName] = StabilizationConditions.ToPersistedId(record.Condition),
            [ConditionDescriptionPropertyName] = record.ConditionDescription,
            [EvidencePropertyName] = WriteEvidence(record.Evidence),
        };

        if (record.FormalTestAttemptId is not null)
        {
            recordObject[FormalTestAttemptIdPropertyName] = record.FormalTestAttemptId;
        }

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

    private static JsonObject WriteEvidence(StabilizationPassEvidence evidence)
    {
        return new JsonObject
        {
            [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(evidence.Branch),
            [LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(evidence.Level),
            [DatePropertyName] = WriteDate(evidence.Date),
            [StandardPropertyName] = evidence.Standard,
            [PassStatePropertyName] = StableDomainIdentifiers.FormalTestPassStates.ToPersistedId(evidence.PassState),
            [StandardEvaluationResultPropertyName] = WriteStandardEvaluationResult(evidence.StandardEvaluationResult),
            [AfterAdjacentWorkOrControlledDistractorPropertyName] = evidence.AfterAdjacentWorkOrControlledDistractor,
            [MainFailureModeAvoidedPropertyName] = evidence.MainFailureModeAvoided,
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
            failureArray.AddNode(new JsonObject
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

    private static LocalStabilizationPassRecord ReadRecord(JsonNode? node)
    {
        if (node is not JsonObject recordObject)
        {
            throw new InvalidOperationException("The stored stabilization pass record is invalid.");
        }

        return new LocalStabilizationPassRecord(
            ReadRequiredString(recordObject, PassIdPropertyName),
            ReadRequiredString(recordObject, EvidenceArtifactIdPropertyName),
            ReadOptionalString(recordObject, FormalTestAttemptIdPropertyName),
            ReadOptionalString(recordObject, CompletedSessionIdPropertyName),
            ReadOptionalDrill(recordObject),
            StabilizationConditions.FromPersistedId(ReadRequiredString(recordObject, ConditionPropertyName)),
            ReadRequiredString(recordObject, ConditionDescriptionPropertyName),
            ReadEvidence(ReadRequiredObject(recordObject, EvidencePropertyName)));
    }

    private static StabilizationPassEvidence ReadEvidence(JsonObject evidenceObject)
    {
        return new StabilizationPassEvidence(
            StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(evidenceObject, BranchPropertyName)),
            StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(evidenceObject, LevelPropertyName)),
            ReadDate(ReadRequiredObject(evidenceObject, DatePropertyName)),
            ReadRequiredString(evidenceObject, StandardPropertyName),
            StableDomainIdentifiers.FormalTestPassStates.FromPersistedId(ReadRequiredString(evidenceObject, PassStatePropertyName)),
            ReadStandardEvaluationResult(ReadRequiredObject(evidenceObject, StandardEvaluationResultPropertyName)),
            ReadRequiredBoolean(evidenceObject, AfterAdjacentWorkOrControlledDistractorPropertyName),
            ReadRequiredString(evidenceObject, MainFailureModeAvoidedPropertyName));
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
            throw new InvalidOperationException($"The stored stabilization pass record is missing {propertyName}.");
        }

        return objectNode;
    }

    private static JsonArray ReadRequiredArray(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonArray arrayNode)
        {
            throw new InvalidOperationException($"The stored stabilization pass record is missing {propertyName}.");
        }

        return arrayNode;
    }

    private static string ReadRequiredString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored stabilization pass record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored stabilization pass record has an invalid {propertyName}.",
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
            throw new InvalidOperationException($"The stored stabilization pass record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored stabilization pass record has an invalid {propertyName}.",
                exception);
        }
    }

    private static bool ReadRequiredBoolean(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The stored stabilization pass record is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<bool>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored stabilization pass record has an invalid {propertyName}.",
                exception);
        }
    }
}
