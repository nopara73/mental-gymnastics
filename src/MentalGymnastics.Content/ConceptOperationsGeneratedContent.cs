using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class ConceptOperationsGeneratedContent
{
    internal ConceptOperationsGeneratedContent(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);

        var materialArray = materials.ToArray();
        Result = result;
        Materials = Array.AsReadOnly(materialArray);
        ValidatedContent = ValidatedGeneratedDrillContent.Create(result, materialArray);
    }

    public GeneratedDrillContentResult Result { get; }

    public IReadOnlyList<GeneratedContentMaterial> Materials { get; }

    public ValidatedGeneratedDrillContent ValidatedContent { get; }

    public bool CanBeConsumedByRuntime => ValidatedContent.CanBeConsumedByRuntime;

    public bool CanBeRecordedByPersistence => Result.CanBeRecordedByPersistence;

    public bool GrantsAdvancement => false;
}

public static class ConceptOperationsGeneratedContentGenerator
{
    private const string RuleBeforeUnseenConstraint = "Rule stated before unseen examples.";
    private const string RelationsNamedConstraint = "Relations must be named; surface matches do not count.";
    private const string DefaultRuleAmbiguity = "clear examples";
    private const string DefaultDomainDistance = "near domain";
    private const int DefaultExampleCount = 8;
    private const int DefaultRelationCount = 3;

    private static readonly RuleExtractionTemplate[] RuleFamilies =
    [
        new(
            "two-shared-features",
            "Items are positive when they share both color family and boundary type; single-feature matches are negative.",
            [
                "blue circle with solid rim and center mark",
                "blue oval with solid rim and side mark",
                "green square with dashed rim and corner mark",
                "green rectangle with dashed rim and center mark",
            ],
            [
                "blue circle with dashed rim and center mark",
                "green square with solid rim and corner mark",
                "red triangle with solid rim and center mark",
            ],
            [
                new("blue capsule with solid rim and lower mark", "positive", "shares color family and boundary type"),
                new("green capsule with solid rim and lower mark", "negative", "shares only boundary type"),
                new("green diamond with dashed rim and top mark", "positive", "shares color family and boundary type"),
            ]),
        new(
            "ordered-position-rule",
            "Items are positive when the marked position advances left-to-right across the pair; matching symbols without ordered advance are negative.",
            [
                "A1 left dot then center dot",
                "B2 center slash then right slash",
                "C3 lower notch then upper notch",
                "D4 first bar then second bar",
            ],
            [
                "A1 center dot then left dot",
                "B2 right slash then center slash",
                "C3 upper notch then upper notch",
            ],
            [
                new("E5 lower hook then middle hook", "positive", "position advances in the required order"),
                new("F6 right bar then left bar", "negative", "position reverses instead of advancing"),
                new("G7 first ring then second ring", "positive", "ordered advance is preserved"),
            ]),
        new(
            "category-with-exclusion",
            "Items are positive when they are transport tools with an explicit route marker; decorative route symbols alone are negative.",
            [
                "cart icon with north route marker",
                "sled icon with east route marker",
                "boat icon with river route marker",
                "tram icon with depot route marker",
            ],
            [
                "star icon with north route marker",
                "cart icon with decorative border only",
                "circle icon with east route marker",
            ],
            [
                new("bike icon with west route marker", "positive", "transport tool plus explicit route marker"),
                new("kite icon with west route marker", "negative", "route marker without transport tool"),
                new("boat icon with decorative border only", "negative", "transport tool without explicit route marker"),
            ]),
    ];

    private static readonly StructureMappingTemplate[] StructureMappingTemplates =
    [
        new(
            "triage-to-study-design",
            "source structure: a triage board screens reports before escalation, priority overrides queue order, accepted reports carry evidence tags, and rejected reports keep reason codes.",
            "target context: a study group screens hypotheses before experiments, mentor priority overrides default order, accepted hypotheses carry measurement plans, and rejected hypotheses keep disconfirmation notes.",
            [
                new(
                    "filter-before-action",
                    "reports are screened before escalation",
                    "hypotheses are screened before experiment",
                    "both contexts require a filter step before costly action"),
                new(
                    "priority-over-default",
                    "priority overrides the queue order",
                    "mentor priority overrides default hypothesis order",
                    "an explicit priority relation, not arrival order, controls sequence"),
                new(
                    "evidence-required-for-acceptance",
                    "accepted reports carry evidence tags",
                    "accepted hypotheses carry measurement plans",
                    "acceptance requires an evidence-bearing marker"),
                new(
                    "rejection-reason-preserved",
                    "rejected reports retain reason codes",
                    "rejected hypotheses retain disconfirmation notes",
                    "rejection keeps the reason so the decision can be audited"),
                new(
                    "acceptance-before-escalation",
                    "only accepted reports proceed to escalation",
                    "only accepted hypotheses proceed to experiment",
                    "the acceptance decision gates the next costly stage"),
            ],
            [
                "surface lure: both contexts mention boards and tags; matching the board label alone does not count.",
                "surface lure: both contexts mention priority; naming priority without its override relation does not count.",
            ],
            "limit: the mapping stops at decision flow and auditability; it does not infer that report validity and hypothesis truth are the same."),
        new(
            "canal-lock-to-access-control",
            "source structure: a canal lock admits one boat group, closes the lower gate, equalizes water level, then opens the upper gate only when pressure is safe.",
            "target context: an access-control workflow admits one request group, closes external submission, equalizes permission checks, then opens deployment only when policy pressure is safe.",
            [
                new(
                    "single-batch-admission",
                    "one boat group enters before the gate cycle begins",
                    "one request group enters before the deployment cycle begins",
                    "the unit of work is batched before the transition"),
                new(
                    "entry-closed-before-equalizing",
                    "the lower gate closes before water levels equalize",
                    "external submission closes before permission checks equalize",
                    "incoming change is blocked before balancing conditions"),
                new(
                    "condition-equalization",
                    "water level is equalized before the upper gate opens",
                    "permission checks are equalized before deployment opens",
                    "a balancing condition must be satisfied before release"),
                new(
                    "safety-gate-before-release",
                    "the upper gate opens only when pressure is safe",
                    "deployment opens only when policy pressure is safe",
                    "release depends on a safety threshold, not on impatience"),
                new(
                    "cycle-completes-before-next-batch",
                    "the current boat group exits before another group enters",
                    "the current request group deploys before another group enters",
                    "one constrained transition completes before the next batch begins"),
            ],
            [
                "surface lure: both contexts use gates; naming a gate without the sequence relation does not count.",
                "surface lure: both contexts involve pressure; treating pressure as emotional stress instead of release condition does not count.",
            ],
            "limit: the mapping stops at staged access under constraints; it does not claim software permissions behave like water physics."),
        new(
            "kitchen-pass-to-code-review",
            "source structure: a kitchen pass receives dish tickets, groups tickets by station, marks blocked dishes, and releases plates only after final inspection.",
            "target context: a code-review queue receives change requests, groups changes by subsystem, marks blocked changes, and releases merges only after final inspection.",
            [
                new(
                    "intake-to-queue",
                    "dish tickets enter the kitchen pass before work starts",
                    "change requests enter the review queue before work starts",
                    "incoming work becomes visible before execution"),
                new(
                    "group-by-responsibility",
                    "tickets are grouped by station",
                    "changes are grouped by subsystem",
                    "responsibility grouping determines who handles each item"),
                new(
                    "blocked-state-visible",
                    "blocked dishes are marked on the pass",
                    "blocked changes are marked in the queue",
                    "blocked status remains visible rather than being hidden"),
                new(
                    "inspection-before-release",
                    "plates release only after final inspection",
                    "merges release only after final inspection",
                    "final release requires an independent check"),
                new(
                    "block-resolved-before-inspection",
                    "a blocked dish must be cleared before final inspection",
                    "a blocked change must be cleared before final inspection",
                    "visible blockage prevents release review until resolved"),
            ],
            [
                "surface lure: both contexts mention a pass or review; copying the word review without relations does not count.",
                "surface lure: both contexts have queues; queue shape alone does not count unless responsibility and release relations are named.",
            ],
            "limit: the mapping stops at queue, responsibility, blockage, and inspection relations; it does not infer equivalent quality standards."),
    ];

    public static ConceptOperationsGeneratedContent Generate(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(seed);

        EnsureSupportedConceptOperationsRequest(request);

        var seedPlan = GeneratedContentSeedDeriver.Derive(request, seed);
        IReadOnlyList<GeneratedContentMaterial> materials;
        IEnumerable<GeneratedContentPayloadFact> drillPayloadFacts;
        if (request.Drill == DrillId.CO2StructureMapping)
        {
            var plan = BuildStructureMappingPlan(request, seedPlan);
            materials = BuildStructureMappingMaterials(request, plan);
            drillPayloadFacts = BuildStructureMappingPayloadFacts(materials);
        }
        else
        {
            var plan = BuildRuleExtractionPlan(request, seedPlan);
            materials = BuildRuleExtractionMaterials(request, plan);
            drillPayloadFacts = BuildRuleExtractionPayloadFacts(request, materials);
        }

        var result = new GeneratedDrillContentResult(
            request,
            seedPlan.Instance,
            seedPlan.PayloadFacts.Concat(drillPayloadFacts));

        return new ConceptOperationsGeneratedContent(result, materials);
    }

    private static void EnsureSupportedConceptOperationsRequest(GeneratedDrillContentRequest request)
    {
        if (request.Branch != BranchCode.CO)
        {
            throw new ArgumentException(
                "Concept Operations generated content can only be produced for the CO branch.",
                nameof(request));
        }

        if (request.Drill is not (DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping))
        {
            throw new ArgumentException(
                "Concept Operations generated content supports CO-1 Rule Extraction and CO-2 Structure Mapping.",
                nameof(request));
        }

        if (request.ContentKind != PromptContentKind.RuleExampleSet)
        {
            throw new ArgumentException(
                $"Drill {request.Drill} requires generated content kind {PromptContentKind.RuleExampleSet}.",
                nameof(request));
        }
    }

    private static RuleExtractionPlan BuildRuleExtractionPlan(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan)
    {
        var template = SelectRuleFamily(seedPlan);
        var exampleCount = Math.Max(ParseLoadCount(request, "example count") ?? DefaultExampleCount, 4);
        var unseenCount = Math.Min(2, template.UnseenExamples.Count);
        var negativeCount = Math.Min(2, template.NegativeExamples.Count);
        var positiveCount = Math.Max(exampleCount - unseenCount - negativeCount, 1);
        var positiveExamples = SelectFrom(
            template.PositiveExamples,
            positiveCount,
            seedPlan,
            "positive-example");
        var negativeExamples = SelectFrom(
            template.NegativeExamples,
            negativeCount,
            seedPlan,
            "negative-example");
        var unseenExamples = SelectFrom(
            template.UnseenExamples,
            unseenCount,
            seedPlan,
            "unseen-example");

        return new RuleExtractionPlan(
            template.RuleFamily,
            template.ExpectedRule,
            positiveExamples,
            negativeExamples,
            unseenExamples,
            LoadValueOrDefault(request, "rule ambiguity", DefaultRuleAmbiguity));
    }

    private static StructureMappingPlan BuildStructureMappingPlan(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan)
    {
        var template = SelectStructureMappingTemplate(seedPlan);
        var relationCount = Math.Clamp(
            ParseLoadCount(request, "relation count") ?? DefaultRelationCount,
            1,
            template.Relations.Count);
        var relations = SelectFrom(
            template.Relations,
            relationCount,
            seedPlan,
            "structure-relation");
        var surfaceLures = SelectFrom(
            template.SurfaceLures,
            Math.Min(2, template.SurfaceLures.Count),
            seedPlan,
            "surface-lure");
        var domainDistance = LoadValueOrDefault(
            request,
            "domain distance",
            LoadValueOrDefault(request, "transfer distance", DefaultDomainDistance));

        return new StructureMappingPlan(
            template.TemplateId,
            template.SourceStructure,
            template.TargetContext,
            relations,
            surfaceLures,
            template.MappingLimit,
            domainDistance);
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildRuleExtractionMaterials(
        GeneratedDrillContentRequest request,
        RuleExtractionPlan plan)
    {
        var materials = new List<GeneratedContentMaterial>();

        foreach (var loadVariable in request.LoadVariables)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.LoadVariable,
                loadVariable.Name,
                loadVariable.Value));
        }

        AddHonestyConstraints(materials, request);

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RuleStatement,
            "rule-statement-before-unseen",
            "State a testable rule before unseen examples are revealed; the rule cannot be rewritten after feedback."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RuleFamily,
            "rule-family",
            plan.RuleFamily));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RuleAmbiguity,
            "rule-ambiguity",
            plan.RuleAmbiguity));
        var exceptionHandling = request.LoadVariables.FirstOrDefault(loadVariable =>
            string.Equals(loadVariable.Name, "exception handling", StringComparison.OrdinalIgnoreCase));
        if (exceptionHandling is not null)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ExceptionHandling,
                "exception-handling",
                exceptionHandling.Value));
        }

        for (var i = 0; i < plan.PositiveExamples.Count; i++)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.PositiveExample,
                $"positive-example-{(i + 1).ToString(CultureInfo.InvariantCulture)}",
                $"positive example {(i + 1).ToString(CultureInfo.InvariantCulture)}: {plan.PositiveExamples[i]}"));
        }

        for (var i = 0; i < plan.NegativeExamples.Count; i++)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.NegativeExample,
                $"negative-example-{(i + 1).ToString(CultureInfo.InvariantCulture)}",
                $"negative example {(i + 1).ToString(CultureInfo.InvariantCulture)}: {plan.NegativeExamples[i]}"));
        }

        for (var i = 0; i < plan.UnseenExamples.Count; i++)
        {
            var unseenNumber = (i + 1).ToString(CultureInfo.InvariantCulture);
            var unseen = plan.UnseenExamples[i];
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.UnseenExample,
                $"unseen-example-{unseenNumber}",
                $"unseen example {unseenNumber}: {unseen.Text}; classify only after recording the pre-stated rule"));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ExpectedClassification,
                $"expected-classification-{unseenNumber}",
                $"unseen example {unseenNumber}: {unseen.ExpectedClassification}; key reason: {unseen.Reason}"));
        }

        return Array.AsReadOnly(materials.ToArray());
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildStructureMappingMaterials(
        GeneratedDrillContentRequest request,
        StructureMappingPlan plan)
    {
        var materials = new List<GeneratedContentMaterial>();

        foreach (var loadVariable in request.LoadVariables)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.LoadVariable,
                loadVariable.Name,
                loadVariable.Value));
        }

        AddHonestyConstraints(materials, request);

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.SourceStructure,
            "source-structure",
            plan.SourceStructure));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TargetStructure,
            "target-context",
            plan.TargetContext));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DomainDistance,
            "domain-distance",
            plan.DomainDistance));
        var taskLength = request.LoadVariables.FirstOrDefault(loadVariable =>
            string.Equals(loadVariable.Name, "task length", StringComparison.OrdinalIgnoreCase));
        if (taskLength is not null)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.TaskLength,
                "task-length",
                taskLength.Value));
        }

        var transferDistance = request.LoadVariables.FirstOrDefault(loadVariable =>
            string.Equals(loadVariable.Name, "transfer distance", StringComparison.OrdinalIgnoreCase));
        if (transferDistance is not null)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.TransferDistance,
                "transfer-distance",
                transferDistance.Value));
        }

        for (var i = 0; i < plan.Relations.Count; i++)
        {
            var relationNumber = (i + 1).ToString(CultureInfo.InvariantCulture);
            var relation = plan.Relations[i];
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.RequiredRelation,
                $"relation-{relationNumber}",
                $"relation-{relationNumber} named relation '{relation.RelationName}': source relation {relation.SourceRelation}; target relation {relation.TargetRelation}."));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ExpectedMapping,
                $"expected-mapping-{relationNumber}",
                $"relation-{relationNumber} '{relation.RelationName}' maps source to target because {relation.MappingEvidence}; surface terms are insufficient."));
        }

        for (var i = 0; i < plan.SurfaceLures.Count; i++)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.SurfaceLure,
                $"surface-lure-{(i + 1).ToString(CultureInfo.InvariantCulture)}",
                plan.SurfaceLures[i]));
        }

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.MappingLimit,
            "mapping-limit",
            plan.MappingLimit));

        if (request.Level == GlobalLevelId.L5)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.AuditPayload,
                "model-audit",
                $"Audit the submitted mapping against this declared limit: {plan.MappingLimit} Name one critical assumption, test one prediction, and submit LIMIT=SUPPORTED or LIMIT=EXCEEDED plus PREDICTION=PASSED or PREDICTION=FAILED."));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ExpectedFinding,
                "model-audit-key",
                "LIMIT=SUPPORTED; PREDICTION=PASSED"));
        }

        return Array.AsReadOnly(materials.ToArray());
    }

    private static void AddHonestyConstraints(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedDrillContentRequest request)
    {
        var added = new HashSet<string>(StringComparer.Ordinal);
        var index = 1;
        foreach (var constraint in request.CriticalConstraints)
        {
            if (added.Add(constraint.Description))
            {
                materials.Add(new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.HonestyConstraint,
                    $"request-constraint-{index.ToString(CultureInfo.InvariantCulture)}",
                    constraint.Description));
            }

            index++;
        }

        var defaultConstraint = request.Drill == DrillId.CO2StructureMapping
            ? RelationsNamedConstraint
            : RuleBeforeUnseenConstraint;
        if (!added.Add(defaultConstraint))
        {
            return;
        }

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.HonestyConstraint,
            request.Drill == DrillId.CO2StructureMapping
                ? "relations-named"
                : "rule-before-unseen",
            defaultConstraint));
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildRuleExtractionPayloadFacts(
        GeneratedDrillContentRequest request,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var ruleStatementPrompt = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.RuleStatement)
            .Value;
        var ruleFamily = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.RuleFamily)
            .Value;
        var positiveExamples = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.PositiveExample)
            .Select(material => material.Value)
            .ToArray();
        var negativeExamples = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.NegativeExample)
            .Select(material => material.Value)
            .ToArray();
        var unseenExamples = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.UnseenExample)
            .Select(material => material.Value)
            .ToArray();
        var expectedClassifications = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedClassification)
            .Select(material => material.Value)
            .ToArray();

        yield return new GeneratedContentPayloadFact("payload-family", "co-rule-extraction");
        yield return new GeneratedContentPayloadFact("rule-family", ruleFamily);
        yield return new GeneratedContentPayloadFact(
            "rule-ambiguity",
            LoadValueOrDefault(request, "rule ambiguity", DefaultRuleAmbiguity));
        yield return new GeneratedContentPayloadFact("rule-statement-prompt", ruleStatementPrompt);
        yield return new GeneratedContentPayloadFact("positive-examples", string.Join("|", positiveExamples));
        yield return new GeneratedContentPayloadFact("negative-examples", string.Join("|", negativeExamples));
        yield return new GeneratedContentPayloadFact("unseen-examples", string.Join("|", unseenExamples));
        yield return new GeneratedContentPayloadFact("expected-classifications", string.Join("|", expectedClassifications));
        yield return new GeneratedContentPayloadFact(
            "rule-statement-before-test-evidence",
            "the practitioner rule must be recorded before unseen examples are classified");
        yield return new GeneratedContentPayloadFact(
            "overfitting-evidence",
            "negative examples and unseen examples expose rules fitted only to surface features");
        yield return new GeneratedContentPayloadFact(
            "rewrite-after-feedback-evidence",
            "the pre-stated rule cannot be rewritten after feedback or after seeing expected classifications");
        yield return new GeneratedContentPayloadFact(
            "vague-rule-evidence",
            "the recorded rule must be testable against unseen examples and cannot be motivational or unfalsifiable");
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildStructureMappingPayloadFacts(
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var sourceStructure = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.SourceStructure)
            .Value;
        var targetContext = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.TargetStructure)
            .Value;
        var domainDistance = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.DomainDistance)
            .Value;
        var requiredRelations = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.RequiredRelation)
            .Select(material => material.Value)
            .ToArray();
        var expectedMappings = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedMapping)
            .Select(material => material.Value)
            .ToArray();
        var surfaceLures = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.SurfaceLure)
            .Select(material => material.Value)
            .ToArray();
        var mappingLimit = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.MappingLimit)
            .Value;

        yield return new GeneratedContentPayloadFact("payload-family", "co-structure-mapping");
        yield return new GeneratedContentPayloadFact("source-structure", sourceStructure);
        yield return new GeneratedContentPayloadFact("target-context", targetContext);
        yield return new GeneratedContentPayloadFact("domain-distance", domainDistance);
        yield return new GeneratedContentPayloadFact("required-relations", string.Join("|", requiredRelations));
        yield return new GeneratedContentPayloadFact("expected-mapping", string.Join("|", expectedMappings));
        yield return new GeneratedContentPayloadFact("surface-lures", string.Join("|", surfaceLures));
        yield return new GeneratedContentPayloadFact("mapping-limit", mappingLimit);
        yield return new GeneratedContentPayloadFact(
            "relation-naming-evidence",
            "each preserved mapping must include the named relation being carried across domains");
        yield return new GeneratedContentPayloadFact(
            "surface-match-rejection-evidence",
            "surface similarity is an explicit lure and does not count as a preserved relation");
        yield return new GeneratedContentPayloadFact(
            "unsupported-inference-evidence",
            "mapping limits expose unsupported inference beyond the named relations");
    }

    private static RuleExtractionTemplate SelectRuleFamily(
        GeneratedContentSeedPlan seedPlan)
    {
        var index = (
            SelectIndex(seedPlan.PayloadSeed, "rule-family", seedPlan.VariantIndex, RuleFamilies.Length) +
            seedPlan.VariantIndex) %
            RuleFamilies.Length;

        return RuleFamilies[index];
    }

    private static StructureMappingTemplate SelectStructureMappingTemplate(
        GeneratedContentSeedPlan seedPlan)
    {
        var index = (
            SelectIndex(seedPlan.PayloadSeed, "structure-template", seedPlan.VariantIndex, StructureMappingTemplates.Length) +
            seedPlan.VariantIndex) %
            StructureMappingTemplates.Length;

        return StructureMappingTemplates[index];
    }

    private static IReadOnlyList<string> SelectFrom(
        IReadOnlyList<string> source,
        int count,
        GeneratedContentSeedPlan seedPlan,
        string purpose)
    {
        var firstIndex = (
            SelectIndex(seedPlan.PayloadSeed, purpose, seedPlan.VariantIndex, source.Count) +
            seedPlan.VariantIndex) %
            source.Count;
        var selected = new List<string>();

        for (var i = 0; i < count; i++)
        {
            selected.Add(source[(firstIndex + i) % source.Count]);
        }

        return Array.AsReadOnly(selected.ToArray());
    }

    private static IReadOnlyList<StructureRelationTemplate> SelectFrom(
        IReadOnlyList<StructureRelationTemplate> source,
        int count,
        GeneratedContentSeedPlan seedPlan,
        string purpose)
    {
        var firstIndex = (
            SelectIndex(seedPlan.PayloadSeed, purpose, seedPlan.VariantIndex, source.Count) +
            seedPlan.VariantIndex) %
            source.Count;
        var selected = new List<StructureRelationTemplate>();

        for (var i = 0; i < count; i++)
        {
            selected.Add(source[(firstIndex + i) % source.Count]);
        }

        return Array.AsReadOnly(selected.ToArray());
    }

    private static IReadOnlyList<UnseenRuleExample> SelectFrom(
        IReadOnlyList<UnseenRuleExample> source,
        int count,
        GeneratedContentSeedPlan seedPlan,
        string purpose)
    {
        var firstIndex = (
            SelectIndex(seedPlan.PayloadSeed, purpose, seedPlan.VariantIndex, source.Count) +
            seedPlan.VariantIndex) %
            source.Count;
        var selected = new List<UnseenRuleExample>();

        for (var i = 0; i < count; i++)
        {
            selected.Add(source[(firstIndex + i) % source.Count]);
        }

        return Array.AsReadOnly(selected.ToArray());
    }

    private static int SelectIndex(
        string payloadSeed,
        string purpose,
        int variantIndex,
        int length)
    {
        var hash = GeneratedContentStableHash.HashSegment(
            string.Join("|", payloadSeed, purpose, variantIndex.ToString(CultureInfo.InvariantCulture)));
        var baseIndex = Convert.ToInt32(hash[..6], 16);

        return (baseIndex + variantIndex) % length;
    }

    private static string LoadValueOrDefault(
        GeneratedDrillContentRequest request,
        string name,
        string defaultValue)
    {
        return request.LoadVariables
            .FirstOrDefault(loadVariable => string.Equals(
                loadVariable.Name,
                name,
                StringComparison.OrdinalIgnoreCase))
            ?.Value ?? defaultValue;
    }

    private static int? ParseLoadCount(
        GeneratedDrillContentRequest request,
        string name)
    {
        var rawValue = request.LoadVariables
            .FirstOrDefault(loadVariable => string.Equals(
                loadVariable.Name,
                name,
                StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (rawValue is null)
        {
            return null;
        }

        var numericPrefix = new string(rawValue
            .Trim()
            .TakeWhile(char.IsDigit)
            .ToArray());

        return int.TryParse(numericPrefix, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count
            : null;
    }

    private sealed record RuleExtractionTemplate(
        string RuleFamily,
        string ExpectedRule,
        IReadOnlyList<string> PositiveExamples,
        IReadOnlyList<string> NegativeExamples,
        IReadOnlyList<UnseenRuleExample> UnseenExamples);

    private sealed record StructureMappingTemplate(
        string TemplateId,
        string SourceStructure,
        string TargetContext,
        IReadOnlyList<StructureRelationTemplate> Relations,
        IReadOnlyList<string> SurfaceLures,
        string MappingLimit);

    private sealed record StructureRelationTemplate(
        string RelationName,
        string SourceRelation,
        string TargetRelation,
        string MappingEvidence);

    private sealed record UnseenRuleExample(
        string Text,
        string ExpectedClassification,
        string Reason);

    private sealed record RuleExtractionPlan(
        string RuleFamily,
        string ExpectedRule,
        IReadOnlyList<string> PositiveExamples,
        IReadOnlyList<string> NegativeExamples,
        IReadOnlyList<UnseenRuleExample> UnseenExamples,
        string RuleAmbiguity);

    private sealed record StructureMappingPlan(
        string TemplateId,
        string SourceStructure,
        string TargetContext,
        IReadOnlyList<StructureRelationTemplate> Relations,
        IReadOnlyList<string> SurfaceLures,
        string MappingLimit,
        string DomainDistance);
}
