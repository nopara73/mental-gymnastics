using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum ConceptOperationsRuntimeProtocolInvalidReason
{
    RuleExtractionNotSupportedByDrill,
    StructureMappingNotSupportedByDrill,
    RuleAlreadyStated,
    RuleRequiredBeforeUnseenExamples,
    ExamplesRequireAtLeastOneExample,
    DuplicateExampleId,
    UnseenExamplesAlreadyStarted,
    UnseenExamplesNotStarted,
    RuleExtractionAlreadyCompleted,
    UnseenExampleAlreadyClassified,
    RelationsAlreadyNamed,
    RelationsRequiredBeforeMapping,
    RelationsRequireAtLeastOneRelation,
    DuplicateRelationId,
    MappingAlreadyStarted,
    MappingNotStarted,
    StructureMappingAlreadyCompleted,
    UnknownRelation,
    RelationAlreadyMapped,
}

public sealed class ConceptOperationsRuntimeExample
{
    public ConceptOperationsRuntimeExample(
        string id,
        string description,
        bool isPositive)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Concept operations example id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Concept operations example description is required.", nameof(description));
        }

        Id = id;
        Description = description;
        IsPositive = isPositive;
    }

    public string Id { get; }

    public string Description { get; }

    public bool IsPositive { get; }
}

public sealed class ConceptOperationsRuntimeRelation
{
    public ConceptOperationsRuntimeRelation(string id, string description)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Concept operations relation id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Concept operations relation description is required.", nameof(description));
        }

        Id = id;
        Description = description;
    }

    public string Id { get; }

    public string Description { get; }
}

public sealed record ConceptOperationsRuntimeProtocolResult(
    bool IsAccepted,
    ConceptOperationsRuntimeProtocolInvalidReason? InvalidReason,
    RuntimeEvent? Event);

public sealed class ConceptOperationsRuntimeProtocol
{
    private readonly IRuntimeClock _clock;
    private readonly Dictionary<string, ConceptOperationsRuntimeExample> _examples = new(StringComparer.Ordinal);
    private readonly HashSet<string> _classifiedUnseenExamples = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RelationState> _relations = new(StringComparer.Ordinal);
    private string? _ruleStatement;
    private bool _unseenExamplesStarted;
    private bool _ruleExtractionCompleted;
    private bool _mappingStarted;
    private bool _structureMappingCompleted;
    private int _correctClassificationCount;
    private int _incorrectClassificationCount;
    private int _negativeExampleMisclassifiedCount;
    private int _positiveExampleMisclassifiedCount;
    private int _unsupportedInferenceCount;
    private int _preservedRelationCount;
    private int _failedRelationCount;
    private int _surfaceMatchRejectionCount;
    private int _missingRelationCount;

    private ConceptOperationsRuntimeProtocol(
        IRuntimeClock clock,
        RuntimeEventLog eventLog)
    {
        _clock = clock;
        EventLog = eventLog;
    }

    public RuntimeEventLog EventLog { get; }

    public RuntimeSessionDefinition SessionDefinition => EventLog.SessionDefinition;

    public IReadOnlyList<ConceptOperationsRuntimeExample> Examples => _examples.Values.ToArray();

    public IReadOnlyList<ConceptOperationsRuntimeRelation> Relations =>
        _relations.Values.Select(state => state.Relation).ToArray();

    public static ConceptOperationsRuntimeProtocol Start(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        IRuntimeClock clock)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Concept operations runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(clock);
        ValidateConceptOperationsSession(sessionDefinition);

        return new ConceptOperationsRuntimeProtocol(
            clock,
            RuntimeEventLog.Start(sessionId, sessionDefinition, clock.Now));
    }

    public ConceptOperationsRuntimeProtocolResult StateRule(
        string ruleStatement,
        IEnumerable<ConceptOperationsRuntimeExample> examples)
    {
        if (string.IsNullOrWhiteSpace(ruleStatement))
        {
            throw new ArgumentException("Concept operations rule statement is required.", nameof(ruleStatement));
        }

        ArgumentNullException.ThrowIfNull(examples);

        if (!SupportsRuleExtraction())
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.RuleExtractionNotSupportedByDrill);
        }

        if (_ruleStatement is not null)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.RuleAlreadyStated);
        }

        var exampleArray = examples.ToArray();
        foreach (var example in exampleArray)
        {
            ArgumentNullException.ThrowIfNull(example);
        }

        if (exampleArray.Length == 0)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.ExamplesRequireAtLeastOneExample);
        }

        var duplicateExample = exampleArray
            .GroupBy(example => example.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateExample is not null)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.DuplicateExampleId);
        }

        _ruleStatement = ruleStatement;
        foreach (var example in exampleArray)
        {
            _examples.Add(example.Id, example);
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..RuleFacts(),
                ..ExampleSetFacts(),
                new RuntimeEventFact("rule_stated_before_unseen_examples", "true"),
                new RuntimeEventFact("negative_examples_handled_by_same_rule", "true"),
                new RuntimeEventFact("honesty_constraint", "rule_stated_before_unseen_examples"),
            ]);

        return Accepted(runtimeEvent);
    }

    public ConceptOperationsRuntimeProtocolResult StartUnseenExamples()
    {
        if (!SupportsRuleExtraction())
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.RuleExtractionNotSupportedByDrill);
        }

        if (_ruleStatement is null)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.RuleRequiredBeforeUnseenExamples);
        }

        if (_unseenExamplesStarted)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.UnseenExamplesAlreadyStarted);
        }

        _unseenExamplesStarted = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..RuleFacts(),
                ..ExampleSetFacts(),
                new RuntimeEventFact("rule_confirmed_before_unseen_examples", "true"),
                new RuntimeEventFact("negative_example_tracking", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public ConceptOperationsRuntimeProtocolResult ClassifyUnseenExample(
        string unseenExampleId,
        string description,
        bool predictedPositive,
        bool expectedPositive)
    {
        if (string.IsNullOrWhiteSpace(unseenExampleId))
        {
            throw new ArgumentException("Concept operations unseen example id is required.", nameof(unseenExampleId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Concept operations unseen example description is required.", nameof(description));
        }

        var activeResult = RequireUnseenExamplesInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (_classifiedUnseenExamples.Contains(unseenExampleId))
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.UnseenExampleAlreadyClassified);
        }

        _classifiedUnseenExamples.Add(unseenExampleId);
        var isCorrect = predictedPositive == expectedPositive;
        if (isCorrect)
        {
            _correctClassificationCount++;
        }
        else
        {
            _incorrectClassificationCount++;
        }

        var errorKind = ErrorKindForClassification(expectedPositive, predictedPositive);
        if (errorKind == "negative_example_misclassified")
        {
            _negativeExampleMisclassifiedCount++;
        }
        else if (errorKind == "positive_example_misclassified")
        {
            _positiveExampleMisclassifiedCount++;
        }

        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.AddRange(RuleFacts());
        facts.Add(new RuntimeEventFact("unseen_example_id", unseenExampleId));
        facts.Add(new RuntimeEventFact("unseen_example", description));
        facts.Add(new RuntimeEventFact("response", ClassificationText(predictedPositive)));
        facts.Add(new RuntimeEventFact("predicted_classification", ClassificationText(predictedPositive)));
        facts.Add(new RuntimeEventFact("expected_response", ClassificationText(expectedPositive)));
        facts.Add(new RuntimeEventFact("expected_classification", ClassificationText(expectedPositive)));
        facts.Add(new RuntimeEventFact("response_outcome", isCorrect ? "correct" : "incorrect"));
        facts.Add(new RuntimeEventFact("rule_stated_before_unseen_examples", "true"));

        if (!expectedPositive)
        {
            facts.Add(new RuntimeEventFact("negative_example_handled", isCorrect ? "true" : "false"));
        }

        if (!isCorrect)
        {
            facts.Add(new RuntimeEventFact("error_kind", errorKind!));
            facts.Add(new RuntimeEventFact("failed_item_list", $"{ReadableErrorKind(errorKind!)} {unseenExampleId}: expected {ClassificationText(expectedPositive)}, got {ClassificationText(predictedPositive)}"));
            facts.Add(new RuntimeEventFact("failed_constraint", expectedPositive ? "unseen_positive_classification" : "negative_example_handling"));
            facts.Add(new RuntimeEventFact("failure_type_candidate", "technical_failure"));
            facts.Add(new RuntimeEventFact("negative_example_misclassified_count", _negativeExampleMisclassifiedCount.ToString(CultureInfo.InvariantCulture)));
            facts.Add(new RuntimeEventFact("positive_example_misclassified_count", _positiveExampleMisclassifiedCount.ToString(CultureInfo.InvariantCulture)));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);

        return Accepted(runtimeEvent);
    }

    public ConceptOperationsRuntimeProtocolResult CompleteRuleExtraction(string setId)
    {
        if (string.IsNullOrWhiteSpace(setId))
        {
            throw new ArgumentException("Concept operations rule extraction set id is required.", nameof(setId));
        }

        var activeResult = RequireUnseenExamplesInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _ruleExtractionCompleted = true;
        var classifiedCount = _classifiedUnseenExamples.Count;
        var accuracy = Ratio(_correctClassificationCount, classifiedCount);
        var outputSample =
            $"set {setId}: unseen_examples={classifiedCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"negative_examples={NegativeExampleCount().ToString(CultureInfo.InvariantCulture)}";
        var score =
            $"unseen_classification_accuracy={accuracy}; correct_classification_count={_correctClassificationCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"incorrect_classification_count={_incorrectClassificationCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"negative_example_misclassified_count={_negativeExampleMisclassifiedCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"positive_example_misclassified_count={_positiveExampleMisclassifiedCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"unsupported_inference_count={_unsupportedInferenceCount.ToString(CultureInfo.InvariantCulture)}";

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..RuleFacts(),
                new RuntimeEventFact("set_id", setId),
                new RuntimeEventFact("output_sample", outputSample),
                new RuntimeEventFact("score", score),
                new RuntimeEventFact("unseen_classification_accuracy", accuracy),
                new RuntimeEventFact("correct_classification_count", _correctClassificationCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("incorrect_classification_count", _incorrectClassificationCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("negative_example_misclassified_count", _negativeExampleMisclassifiedCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("positive_example_misclassified_count", _positiveExampleMisclassifiedCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("unsupported_inference_count", _unsupportedInferenceCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("rule_stated_before_unseen_examples", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public ConceptOperationsRuntimeProtocolResult NameRelations(
        IEnumerable<ConceptOperationsRuntimeRelation> relations)
    {
        ArgumentNullException.ThrowIfNull(relations);

        if (!SupportsStructureMapping())
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.StructureMappingNotSupportedByDrill);
        }

        if (_relations.Count > 0)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.RelationsAlreadyNamed);
        }

        var relationArray = relations.ToArray();
        foreach (var relation in relationArray)
        {
            ArgumentNullException.ThrowIfNull(relation);
        }

        if (relationArray.Length == 0)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.RelationsRequireAtLeastOneRelation);
        }

        var duplicateRelation = relationArray
            .GroupBy(relation => relation.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateRelation is not null)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.DuplicateRelationId);
        }

        foreach (var relation in relationArray)
        {
            _relations.Add(relation.Id, new RelationState(relation));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..RelationSetFacts(),
                new RuntimeEventFact("relations_named", "true"),
                new RuntimeEventFact("surface_matches_count", "false"),
                new RuntimeEventFact("honesty_constraint", "relations_named"),
                new RuntimeEventFact("honesty_constraint", "surface_matches_do_not_count"),
            ]);

        return Accepted(runtimeEvent);
    }

    public ConceptOperationsRuntimeProtocolResult StartMapping()
    {
        if (!SupportsStructureMapping())
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.StructureMappingNotSupportedByDrill);
        }

        if (_relations.Count == 0)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.RelationsRequiredBeforeMapping);
        }

        if (_mappingStarted)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.MappingAlreadyStarted);
        }

        _mappingStarted = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..RelationSetFacts(),
                new RuntimeEventFact("relations_confirmed_before_mapping", "true"),
                new RuntimeEventFact("surface_match_rejection_tracking", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public ConceptOperationsRuntimeProtocolResult RecordRelationMapping(
        string relationId,
        string targetMapping,
        bool preservesRelation,
        bool surfaceOnly)
    {
        if (string.IsNullOrWhiteSpace(relationId))
        {
            throw new ArgumentException("Concept operations relation id is required.", nameof(relationId));
        }

        if (string.IsNullOrWhiteSpace(targetMapping))
        {
            throw new ArgumentException("Concept operations target mapping is required.", nameof(targetMapping));
        }

        var mappingResult = RequireMappingInProgress();
        if (mappingResult is not null)
        {
            return mappingResult;
        }

        if (!_relations.TryGetValue(relationId, out var state))
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.UnknownRelation);
        }

        if (state.Mapped)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.RelationAlreadyMapped);
        }

        state.Mapped = true;
        state.TargetMapping = targetMapping;
        state.PreservesRelation = preservesRelation && !surfaceOnly;
        state.SurfaceOnly = surfaceOnly;

        if (state.PreservesRelation)
        {
            _preservedRelationCount++;
        }
        else
        {
            _failedRelationCount++;
        }

        var errorKind = ErrorKindForMapping(preservesRelation, surfaceOnly);
        if (errorKind == "surface_match")
        {
            _surfaceMatchRejectionCount++;
        }
        else if (errorKind == "missing_relation")
        {
            _missingRelationCount++;
        }

        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.AddRange(RelationFacts(state.Relation));
        facts.Add(new RuntimeEventFact("target_mapping", targetMapping));
        facts.Add(new RuntimeEventFact("mapping_result", state.PreservesRelation ? "relation_preserved" : surfaceOnly ? "surface_match_rejected" : "relation_missing"));
        facts.Add(new RuntimeEventFact("preserves_relation", state.PreservesRelation ? "true" : "false"));
        facts.Add(new RuntimeEventFact("surface_only", surfaceOnly ? "true" : "false"));
        facts.Add(new RuntimeEventFact("relations_named", "true"));
        facts.Add(new RuntimeEventFact("response_outcome", state.PreservesRelation ? "correct" : "incorrect"));

        if (!state.PreservesRelation)
        {
            facts.Add(new RuntimeEventFact("error_kind", errorKind!));
            facts.Add(new RuntimeEventFact("failed_item_list", $"{ReadableErrorKind(errorKind!)} {relationId}: {targetMapping}"));
            facts.Add(new RuntimeEventFact("failed_constraint", surfaceOnly ? "relations_not_surface_terms" : "required_relation_preserved"));
            facts.Add(new RuntimeEventFact("failure_type_candidate", "technical_failure"));
            facts.Add(new RuntimeEventFact("surface_match_rejection_count", _surfaceMatchRejectionCount.ToString(CultureInfo.InvariantCulture)));
            facts.Add(new RuntimeEventFact("missing_relation_count", _missingRelationCount.ToString(CultureInfo.InvariantCulture)));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);

        return Accepted(runtimeEvent);
    }

    public ConceptOperationsRuntimeProtocolResult RecordUnsupportedInference(
        string inferenceId,
        string description)
    {
        if (string.IsNullOrWhiteSpace(inferenceId))
        {
            throw new ArgumentException("Concept operations unsupported inference id is required.", nameof(inferenceId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Concept operations unsupported inference description is required.", nameof(description));
        }

        var activeResult = SupportsRuleExtraction()
            ? RequireUnseenExamplesInProgress()
            : RequireMappingInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _unsupportedInferenceCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("unsupported_inference_id", inferenceId),
                new RuntimeEventFact("unsupported_inference", description),
                new RuntimeEventFact("error_kind", "unsupported_inference"),
                new RuntimeEventFact("failed_item_list", $"unsupported inference {inferenceId}: {description}"),
                new RuntimeEventFact("failed_constraint", "inference_requires_visible_support"),
                new RuntimeEventFact("failure_type_candidate", "technical_failure"),
                new RuntimeEventFact("unsupported_inference_count", _unsupportedInferenceCount.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public ConceptOperationsRuntimeProtocolResult CompleteStructureMapping(string mappingId)
    {
        if (string.IsNullOrWhiteSpace(mappingId))
        {
            throw new ArgumentException("Concept operations mapping id is required.", nameof(mappingId));
        }

        var activeResult = RequireMappingInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _structureMappingCompleted = true;
        var mappedCount = _relations.Values.Count(relation => relation.Mapped);
        var relationAccuracy = Ratio(_preservedRelationCount, mappedCount);
        var branchMapping =
            $"relation_accuracy={relationAccuracy}; relations_preserved={_preservedRelationCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"relations_failed={_failedRelationCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"surface_matches_rejected={_surfaceMatchRejectionCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"unsupported_inferences={_unsupportedInferenceCount.ToString(CultureInfo.InvariantCulture)}";
        var score =
            $"relation_accuracy={relationAccuracy}; preserved_relation_count={_preservedRelationCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"failed_relation_count={_failedRelationCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"surface_match_rejection_count={_surfaceMatchRejectionCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"missing_relation_count={_missingRelationCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"unsupported_inference_count={_unsupportedInferenceCount.ToString(CultureInfo.InvariantCulture)}";

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..RelationSetFacts(),
                new RuntimeEventFact("mapping_id", mappingId),
                new RuntimeEventFact("branch_mapping", branchMapping),
                new RuntimeEventFact("score", score),
                new RuntimeEventFact("relation_accuracy", relationAccuracy),
                new RuntimeEventFact("preserved_relation_count", _preservedRelationCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("failed_relation_count", _failedRelationCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("surface_match_rejection_count", _surfaceMatchRejectionCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("missing_relation_count", _missingRelationCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("unsupported_inference_count", _unsupportedInferenceCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("relations_named", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    private ConceptOperationsRuntimeProtocolResult? RequireUnseenExamplesInProgress()
    {
        if (!SupportsRuleExtraction())
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.RuleExtractionNotSupportedByDrill);
        }

        if (!_unseenExamplesStarted)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.UnseenExamplesNotStarted);
        }

        if (_ruleExtractionCompleted)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.RuleExtractionAlreadyCompleted);
        }

        return null;
    }

    private ConceptOperationsRuntimeProtocolResult? RequireMappingInProgress()
    {
        if (!SupportsStructureMapping())
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.StructureMappingNotSupportedByDrill);
        }

        if (!_mappingStarted)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.MappingNotStarted);
        }

        if (_structureMappingCompleted)
        {
            return Rejected(ConceptOperationsRuntimeProtocolInvalidReason.StructureMappingAlreadyCompleted);
        }

        return null;
    }

    private bool SupportsRuleExtraction()
    {
        return SessionDefinition.Drill == DrillId.CO1RuleExtraction;
    }

    private bool SupportsStructureMapping()
    {
        return SessionDefinition.Drill == DrillId.CO2StructureMapping;
    }

    private int NegativeExampleCount()
    {
        return _examples.Values.Count(example => !example.IsPositive);
    }

    private IReadOnlyList<RuntimeEventFact> ProtocolFacts()
    {
        return
        [
            new RuntimeEventFact("protocol_branch", "co"),
            new RuntimeEventFact("protocol_drill", StableDrill(SessionDefinition.Drill)),
            new RuntimeEventFact("branch", SessionDefinition.Branch.ToString()),
            new RuntimeEventFact("level", SessionDefinition.Level.ToString()),
            new RuntimeEventFact("drill", StableDrill(SessionDefinition.Drill)),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> RuleFacts()
    {
        return
        [
            new RuntimeEventFact("rule_statement", _ruleStatement ?? "not_stated"),
            new RuntimeEventFact("rule_explanation", _ruleStatement ?? "not_stated"),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> ExampleSetFacts()
    {
        var positiveCount = _examples.Values.Count(example => example.IsPositive);
        var negativeCount = NegativeExampleCount();
        var facts = new List<RuntimeEventFact>
        {
            new("example_count", _examples.Count.ToString(CultureInfo.InvariantCulture)),
            new("positive_example_count", positiveCount.ToString(CultureInfo.InvariantCulture)),
            new("negative_example_count", negativeCount.ToString(CultureInfo.InvariantCulture)),
            new("example_ids", string.Join(",", _examples.Keys)),
        };

        foreach (var example in _examples.Values)
        {
            facts.Add(new RuntimeEventFact("example_id", example.Id));
            facts.Add(new RuntimeEventFact("example_description", example.Description));
            facts.Add(new RuntimeEventFact("example_kind", example.IsPositive ? "positive" : "negative"));
        }

        return facts.AsReadOnly();
    }

    private IReadOnlyList<RuntimeEventFact> RelationSetFacts()
    {
        var facts = new List<RuntimeEventFact>
        {
            new("relation_count", _relations.Count.ToString(CultureInfo.InvariantCulture)),
            new("relation_ids", string.Join(",", _relations.Keys)),
        };

        foreach (var state in _relations.Values)
        {
            facts.AddRange(RelationFacts(state.Relation));
        }

        return facts.AsReadOnly();
    }

    private IReadOnlyList<RuntimeEventFact> RelationFacts(ConceptOperationsRuntimeRelation relation)
    {
        return
        [
            new RuntimeEventFact("relation_id", relation.Id),
            new RuntimeEventFact("relation_name", relation.Description),
        ];
    }

    private static ConceptOperationsRuntimeProtocolResult Accepted(RuntimeEvent runtimeEvent)
    {
        return new ConceptOperationsRuntimeProtocolResult(true, InvalidReason: null, runtimeEvent);
    }

    private static ConceptOperationsRuntimeProtocolResult Rejected(ConceptOperationsRuntimeProtocolInvalidReason invalidReason)
    {
        return new ConceptOperationsRuntimeProtocolResult(false, invalidReason, Event: null);
    }

    private static void ValidateConceptOperationsSession(RuntimeSessionDefinition sessionDefinition)
    {
        if (sessionDefinition.Branch != BranchCode.CO)
        {
            throw new ArgumentException("Concept operations runtime protocol requires a CO session.", nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill is not DrillId.CO1RuleExtraction and not DrillId.CO2StructureMapping)
        {
            throw new ArgumentException(
                "Concept operations runtime protocol supports only CO-1 Rule Extraction and CO-2 Structure Mapping.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.CO1RuleExtraction &&
            (!ContainsConstraint(sessionDefinition, "rule") || !ContainsConstraint(sessionDefinition, "unseen")))
        {
            throw new ArgumentException(
                "CO-1 runtime sessions must include the rule-before-unseen constraint.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.CO2StructureMapping &&
            (!ContainsConstraint(sessionDefinition, "relation") || !ContainsConstraint(sessionDefinition, "surface")))
        {
            throw new ArgumentException(
                "CO-2 runtime sessions must include relation-naming and surface-match constraints.",
                nameof(sessionDefinition));
        }
    }

    private static bool ContainsConstraint(RuntimeSessionDefinition sessionDefinition, string text)
    {
        return sessionDefinition.CriticalConstraints.Any(constraint =>
            constraint.Description.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static string StableDrill(DrillId drill)
    {
        return drill switch
        {
            DrillId.CO1RuleExtraction => "co_1_rule_extraction",
            DrillId.CO2StructureMapping => "co_2_structure_mapping",
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unsupported concept operations drill."),
        };
    }

    private static string? ErrorKindForClassification(bool expectedPositive, bool predictedPositive)
    {
        if (expectedPositive == predictedPositive)
        {
            return null;
        }

        return expectedPositive ? "positive_example_misclassified" : "negative_example_misclassified";
    }

    private static string? ErrorKindForMapping(bool preservesRelation, bool surfaceOnly)
    {
        if (preservesRelation && !surfaceOnly)
        {
            return null;
        }

        return surfaceOnly ? "surface_match" : "missing_relation";
    }

    private static string ClassificationText(bool isPositive)
    {
        return isPositive ? "positive" : "negative";
    }

    private static string ReadableErrorKind(string errorKind)
    {
        return errorKind.Replace('_', ' ');
    }

    private static string Ratio(int numerator, int denominator)
    {
        return denominator == 0
            ? "0/0"
            : $"{numerator.ToString(CultureInfo.InvariantCulture)}/{denominator.ToString(CultureInfo.InvariantCulture)}";
    }

    private sealed class RelationState
    {
        public RelationState(ConceptOperationsRuntimeRelation relation)
        {
            Relation = relation;
        }

        public ConceptOperationsRuntimeRelation Relation { get; }

        public bool Mapped { get; set; }

        public string? TargetMapping { get; set; }

        public bool PreservesRelation { get; set; }

        public bool SurfaceOnly { get; set; }
    }
}
