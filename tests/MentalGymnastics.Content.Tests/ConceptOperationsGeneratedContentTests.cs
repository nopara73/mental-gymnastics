using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class ConceptOperationsGeneratedContentTests
{
    [Fact]
    public void RuleExtractionGenerationProducesValidRuleExampleSetWithoutAdvancement()
    {
        var request = CreateRuleExtractionRequest();

        var generated = ConceptOperationsGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("co-rule-alpha"));

        var materialValidation = GeneratedContentMaterialValidator.Validate(
            generated.Result,
            generated.Materials);
        var loadValidation = GeneratedContentLoadConstraintValidator.Validate(
            generated.Result,
            generated.Materials);

        Assert.True(materialValidation.IsValid);
        Assert.True(loadValidation.IsValid);
        Assert.True(generated.ValidatedContent.CanBeConsumedByRuntime);
        Assert.False(generated.GrantsAdvancement);
        Assert.Equal(BranchCode.CO, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L1, generated.Result.Level);
        Assert.Equal(DrillId.CO1RuleExtraction, generated.Result.Drill);
        Assert.Equal(PromptContentKind.RuleExampleSet, generated.Result.ContentKind);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.RuleAmbiguity, "clear examples");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, RuleBeforeUnseenConstraint);

        var ruleStatement = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.RuleStatement);
        Assert.Contains("before unseen", ruleStatement.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("testable rule", ruleStatement.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot be rewritten", ruleStatement.Value, StringComparison.OrdinalIgnoreCase);

        var ruleFamily = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.RuleFamily);
        Assert.False(string.IsNullOrWhiteSpace(ruleFamily.Value));

        var positiveExamples = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.PositiveExample)
            .ToArray();
        var negativeExamples = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.NegativeExample)
            .ToArray();
        var unseenExamples = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.UnseenExample)
            .ToArray();
        var expectedClassifications = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedClassification)
            .ToArray();
        Assert.Equal(4, positiveExamples.Length);
        Assert.Equal(2, negativeExamples.Length);
        Assert.Equal(2, unseenExamples.Length);
        Assert.Equal(unseenExamples.Length, expectedClassifications.Length);
        Assert.All(unseenExamples, example =>
            Assert.Contains("unseen", example.Value, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedClassifications, classification =>
            classification.Value.Contains("positive", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedClassifications, classification =>
            classification.Value.Contains("negative", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "co-rule-extraction");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "rule-statement-before-test-evidence" &&
            fact.Value.Contains("before unseen", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "overfitting-evidence" &&
            fact.Value.Contains("negative examples", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "rewrite-after-feedback-evidence" &&
            fact.Value.Contains("cannot be rewritten", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "vague-rule-evidence" &&
            fact.Value.Contains("testable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RuleExtractionFreshVariantChangesExamplesWithoutChangingDemand()
    {
        var request = CreateRuleExtractionRequest();
        var seed = new GeneratedContentSeed("co-rule-bravo");

        var first = ConceptOperationsGeneratedContentGenerator.Generate(request, seed);
        var repeated = ConceptOperationsGeneratedContentGenerator.Generate(CreateRuleExtractionRequest(), seed);
        var fresh = ConceptOperationsGeneratedContentGenerator.Generate(
            CreateRuleExtractionRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(ExampleValues(first.Materials), ExampleValues(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.RuleAmbiguity, "clear examples");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.HonestyConstraint, RuleBeforeUnseenConstraint);
    }

    [Fact]
    public void StructureMappingGenerationProducesValidRelationMappingWithoutAdvancement()
    {
        var request = CreateStructureMappingRequest();

        var generated = ConceptOperationsGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("co-map-alpha"));

        var materialValidation = GeneratedContentMaterialValidator.Validate(
            generated.Result,
            generated.Materials);
        var loadValidation = GeneratedContentLoadConstraintValidator.Validate(
            generated.Result,
            generated.Materials);

        Assert.True(materialValidation.IsValid);
        Assert.True(loadValidation.IsValid);
        Assert.True(generated.ValidatedContent.CanBeConsumedByRuntime);
        Assert.False(generated.GrantsAdvancement);
        Assert.Equal(BranchCode.CO, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L3, generated.Result.Level);
        Assert.Equal(DrillId.CO2StructureMapping, generated.Result.Drill);
        Assert.Equal(PromptContentKind.RuleExampleSet, generated.Result.ContentKind);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.DomainDistance, "near domain");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, RelationsNamedConstraint);

        var sourceStructure = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.SourceStructure);
        Assert.Contains("source structure", sourceStructure.Value, StringComparison.OrdinalIgnoreCase);

        var targetStructure = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.TargetStructure);
        Assert.Contains("target context", targetStructure.Value, StringComparison.OrdinalIgnoreCase);

        var requiredRelations = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.RequiredRelation)
            .ToArray();
        var expectedMappings = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedMapping)
            .ToArray();
        Assert.Equal(3, requiredRelations.Length);
        Assert.Equal(requiredRelations.Length, expectedMappings.Length);
        Assert.All(requiredRelations, relation =>
            Assert.Contains("named relation", relation.Value, StringComparison.OrdinalIgnoreCase));

        Assert.Contains(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.SurfaceLure &&
            material.Value.Contains("surface", StringComparison.OrdinalIgnoreCase) &&
            material.Value.Contains("does not count", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.MappingLimit &&
            material.Value.Contains("limit", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "co-structure-mapping");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "required-relations" &&
            fact.Value.Contains("relation-1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "expected-mapping" &&
            fact.Value.Contains("relation-1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "relation-naming-evidence" &&
            fact.Value.Contains("named", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "surface-match-rejection-evidence" &&
            fact.Value.Contains("surface", StringComparison.OrdinalIgnoreCase) &&
            fact.Value.Contains("does not count", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "unsupported-inference-evidence" &&
            fact.Value.Contains("unsupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StructureMappingFreshVariantChangesStructuresWithoutChangingDemand()
    {
        var request = CreateStructureMappingRequest();
        var seed = new GeneratedContentSeed("co-map-bravo");

        var first = ConceptOperationsGeneratedContentGenerator.Generate(request, seed);
        var repeated = ConceptOperationsGeneratedContentGenerator.Generate(CreateStructureMappingRequest(), seed);
        var fresh = ConceptOperationsGeneratedContentGenerator.Generate(
            CreateStructureMappingRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(StructureMappingValues(first.Materials), StructureMappingValues(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.DomainDistance, "near domain");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.HonestyConstraint, RelationsNamedConstraint);
    }

    [Fact]
    public void ConceptOperationsGenerationRejectsNonConceptOperationsDrills()
    {
        var request = new GeneratedDrillContentRequest(
            BranchCode.DE,
            GlobalLevelId.L1,
            DrillId.DE1PairDiscrimination,
            SessionType.Practice,
            PromptContentKind.DiscriminationItemSet,
            "de-l1-pair-discrimination",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("similarity", "near match"),
                new LoadVariable("quantity", "6"),
            ],
            [new CriticalConstraint("Guessing must be marked.")]);

        Assert.Throws<ArgumentException>(() => ConceptOperationsGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("co-rule-alpha")));
    }

    private const string RuleBeforeUnseenConstraint = "Rule stated before unseen examples.";
    private const string RelationsNamedConstraint = "Relations must be named; surface matches do not count.";

    private static GeneratedDrillContentRequest CreateRuleExtractionRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.CO,
            GlobalLevelId.L1,
            DrillId.CO1RuleExtraction,
            SessionType.Practice,
            PromptContentKind.RuleExampleSet,
            "co-l1-rule-extraction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("rule ambiguity", "clear examples"),
                new LoadVariable("example count", "8"),
            ],
            [new CriticalConstraint(RuleBeforeUnseenConstraint)],
            previouslyUsedContentIds);
    }

    private static GeneratedDrillContentRequest CreateStructureMappingRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.CO,
            GlobalLevelId.L3,
            DrillId.CO2StructureMapping,
            SessionType.Practice,
            PromptContentKind.RuleExampleSet,
            "co-l3-structure-mapping",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("relation count", "3"),
                new LoadVariable("domain distance", "near domain"),
            ],
            [new CriticalConstraint(RelationsNamedConstraint)],
            previouslyUsedContentIds);
    }

    private static void AssertMaterial(
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        GeneratedContentMaterialKind kind,
        string value)
    {
        Assert.Contains(materials, material =>
            material.Kind == kind &&
            string.Equals(material.Value, value, StringComparison.Ordinal));
    }

    private static IReadOnlyList<(GeneratedContentMaterialKind Kind, string Name, string Value)> MaterialSnapshot(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Select(material => (material.Kind, material.Name, material.Value))
            .ToArray();
    }

    private static IReadOnlyList<string> ExampleValues(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Where(material =>
                material.Kind is GeneratedContentMaterialKind.PositiveExample or
                    GeneratedContentMaterialKind.NegativeExample or
                    GeneratedContentMaterialKind.UnseenExample)
            .Select(material => material.Value)
            .ToArray();
    }

    private static IReadOnlyList<string> StructureMappingValues(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Where(material =>
                material.Kind is GeneratedContentMaterialKind.SourceStructure or
                    GeneratedContentMaterialKind.TargetStructure or
                    GeneratedContentMaterialKind.RequiredRelation or
                    GeneratedContentMaterialKind.ExpectedMapping or
                    GeneratedContentMaterialKind.SurfaceLure)
            .Select(material => material.Value)
            .ToArray();
    }
}
