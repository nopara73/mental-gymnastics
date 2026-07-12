using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class DiscriminationGeneratedContentTests
{
    [Fact]
    public void PairDiscriminationGenerationProducesValidAuditableComparisonSetWithoutAdvancement()
    {
        var request = CreatePairDiscriminationRequest();

        var generated = DiscriminationGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("de-pair-alpha"));

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
        Assert.Equal(BranchCode.DE, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L1, generated.Result.Level);
        Assert.Equal(DrillId.DE1PairDiscrimination, generated.Result.Drill);
        Assert.Equal(PromptContentKind.DiscriminationItemSet, generated.Result.ContentKind);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.Similarity, "near match");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TimePressure, "60 seconds");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, MarkedGuessConstraint);

        var relevantFeature = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.RelevantFeature);
        Assert.Contains("relevant", relevantFeature.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("irrelevant", relevantFeature.Value, StringComparison.OrdinalIgnoreCase);

        var pairs = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.DiscriminationPair)
            .ToArray();
        var truthKey = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.MatchTruth)
            .ToArray();
        Assert.Equal(6, pairs.Length);
        Assert.Equal(pairs.Length, truthKey.Length);
        var decodedPairs = pairs
            .Select(pair => (Material: pair, Pair: VisualStimulusCodec.DecodePair(pair.Value)))
            .ToArray();
        Assert.All(decodedPairs, item =>
        {
            Assert.NotEqual(item.Pair.First, item.Pair.Second);
            Assert.False(string.IsNullOrWhiteSpace(item.Pair.RelevantFeatureName));
            Assert.DoesNotContain("left '", item.Material.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("vs right", item.Material.Value, StringComparison.OrdinalIgnoreCase);
            var expectedTruth = item.Pair.RelevantFeatureMatches ? "match" : "mismatch";
            var truth = Assert.Single(truthKey, candidate =>
                string.Equals(candidate.Name, item.Material.Name + "-truth", StringComparison.Ordinal));
            Assert.StartsWith(
                $"{item.Material.Name}: {expectedTruth};",
                truth.Value,
                StringComparison.Ordinal);
        });
        Assert.Contains(decodedPairs, item => item.Pair.RelevantFeatureMatches);
        Assert.Contains(decodedPairs, item => !item.Pair.RelevantFeatureMatches);
        Assert.Contains(decodedPairs, item =>
            item.Pair.RelevantFeature == VisualStimulusFeature.MarkPosition);
        Assert.All(pairs, pair =>
            Assert.DoesNotContain("expected", pair.Value, StringComparison.OrdinalIgnoreCase));
        Assert.All(truthKey, truth =>
            Assert.Contains("expected", truth.Value, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(truthKey, truth =>
            truth.Value.Contains("match", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(truthKey, truth =>
            truth.Value.Contains("mismatch", StringComparison.OrdinalIgnoreCase));

        var guessHandling = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.GuessHandling);
        Assert.Contains("mark", guessHandling.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unmarked", guessHandling.Value, StringComparison.OrdinalIgnoreCase);

        var fpFnKey = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.FalsePositiveFalseNegativeKey);
        Assert.Contains("false positive", fpFnKey.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("false negative", fpFnKey.Value, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "de-pair-discrimination");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "comparison-key" &&
            fact.Value.Contains("pair-1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "guess-marking-policy" &&
            fact.Value.Contains("unmarked guesses fail", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "false-positive-evidence" &&
            fact.Value.Contains("marked different", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "false-negative-evidence" &&
            fact.Value.Contains("missed relevant difference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PairDiscriminationFreshVariantChangesPairsWithoutChangingDemand()
    {
        var request = CreatePairDiscriminationRequest();
        var seed = new GeneratedContentSeed("de-pair-bravo");

        var first = DiscriminationGeneratedContentGenerator.Generate(request, seed);
        var repeated = DiscriminationGeneratedContentGenerator.Generate(CreatePairDiscriminationRequest(), seed);
        var fresh = DiscriminationGeneratedContentGenerator.Generate(
            CreatePairDiscriminationRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(PairValues(first.Materials), PairValues(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.Similarity, "near match");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.TimePressure, "60 seconds");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.HonestyConstraint, MarkedGuessConstraint);
    }

    [Fact]
    public void SeededAuditGenerationProducesValidAuditableLockedOutputWithoutAdvancement()
    {
        var request = CreateSeededAuditRequest();

        var generated = DiscriminationGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("de-audit-alpha"));

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
        Assert.Equal(BranchCode.DE, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L3, generated.Result.Level);
        Assert.Equal(DrillId.DE2SeededAudit, generated.Result.Drill);
        Assert.Equal(PromptContentKind.DiscriminationItemSet, generated.Result.ContentKind);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.ErrorSubtlety, "subtle wording errors");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TaskLength, "6 lines");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.AuditDelay, "5 minutes");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, OriginalOutputLockedConstraint);
        AssertMaterial(
            generated.Materials,
            GeneratedContentMaterialKind.HonestyConstraint,
            SourceReferenceHiddenConstraint);

        var auditReference = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.AuditReference);
        var lockedOutput = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.LockedOriginalOutput);
        Assert.Contains("source record", auditReference.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("locked original output", lockedOutput.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line 1", lockedOutput.Value, StringComparison.OrdinalIgnoreCase);

        var auditInstruction = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.AuditInstruction);
        Assert.Contains("do not edit", auditInstruction.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("findings", auditInstruction.Value, StringComparison.OrdinalIgnoreCase);

        var seededErrors = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.SeededError)
            .ToArray();
        var expectedFindings = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedFinding)
            .ToArray();
        Assert.Equal(3, seededErrors.Length);
        Assert.Equal(seededErrors.Length, expectedFindings.Length);
        Assert.Equal(
            seededErrors.Length,
            AuditLines(auditReference.Value)
                .Zip(AuditLines(lockedOutput.Value))
                .Count(pair => !string.Equals(pair.First, pair.Second, StringComparison.Ordinal)));
        Assert.All(seededErrors, error =>
        {
            Assert.Contains("seeded error", error.Value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("location", error.Value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("criticality", error.Value, StringComparison.OrdinalIgnoreCase);
        });

        var nonErrorDistractors = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.NonErrorDistractor)
            .ToArray();
        Assert.NotEmpty(nonErrorDistractors);
        Assert.All(nonErrorDistractors, distractor =>
            Assert.Contains("not an error", distractor.Value, StringComparison.OrdinalIgnoreCase));

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "de-seeded-audit");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "audit-reference" &&
            fact.Value.Contains("line 1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "locked-original-output" &&
            fact.Value.Contains("line 1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "seeded-error-key" &&
            fact.Value.Contains("seeded error", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "expected-findings" &&
            fact.Value.Contains("finding", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "false-correction-evidence" &&
            fact.Value.Contains("false correction", StringComparison.OrdinalIgnoreCase) &&
            fact.Value.Contains("non-error", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "original-output-lock" &&
            fact.Value.Contains("cannot be edited", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SeededAuditFreshVariantChangesSeededErrorsWithoutChangingDemand()
    {
        var request = CreateSeededAuditRequest();
        var seed = new GeneratedContentSeed("de-audit-bravo");

        var first = DiscriminationGeneratedContentGenerator.Generate(request, seed);
        var repeated = DiscriminationGeneratedContentGenerator.Generate(CreateSeededAuditRequest(), seed);
        var fresh = DiscriminationGeneratedContentGenerator.Generate(
            CreateSeededAuditRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(LockedOutputValue(first.Materials), LockedOutputValue(fresh.Materials));
        Assert.NotEqual(SeededErrorValues(first.Materials), SeededErrorValues(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.ErrorSubtlety, "subtle wording errors");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.TaskLength, "6 lines");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.HonestyConstraint, OriginalOutputLockedConstraint);
    }

    [Fact]
    public void DiscriminationGenerationRejectsNonDiscriminationDrills()
    {
        var request = new GeneratedDrillContentRequest(
            BranchCode.IR,
            GlobalLevelId.L1,
            DrillId.IR1GoNoGoRule,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "ir-l1-go-no-go",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("cue conflict", "simple go/no-go symbols"),
                new LoadVariable("response speed", "2 seconds"),
            ],
            [new CriticalConstraint("Premature response fails item.")]);

        Assert.Throws<ArgumentException>(() => DiscriminationGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("de-pair-alpha")));
    }

    private const string MarkedGuessConstraint = "Guessing must be marked.";

    private const string OriginalOutputLockedConstraint = "Original output cannot be edited during audit.";

    private const string SourceReferenceHiddenConstraint =
        "The source record cannot be reopened, copied, or externally noted after study.";

    private static GeneratedDrillContentRequest CreatePairDiscriminationRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.DE,
            GlobalLevelId.L1,
            DrillId.DE1PairDiscrimination,
            SessionType.Practice,
            PromptContentKind.DiscriminationItemSet,
            "de-l1-pair-discrimination",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("similarity", "near match"),
                new LoadVariable("item quantity", "6"),
                new LoadVariable("time limit", "60 seconds"),
            ],
            [new CriticalConstraint(MarkedGuessConstraint)],
            previouslyUsedContentIds);
    }

    private static GeneratedDrillContentRequest CreateSeededAuditRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.DE,
            GlobalLevelId.L3,
            DrillId.DE2SeededAudit,
            SessionType.Practice,
            PromptContentKind.DiscriminationItemSet,
            "de-l3-seeded-audit",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("error subtlety", "subtle wording errors"),
                new LoadVariable("output length", "6 lines"),
                new LoadVariable("audit delay", "5 minutes"),
                new LoadVariable("quantity", "3"),
            ],
            [new CriticalConstraint(OriginalOutputLockedConstraint)],
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

    private static IReadOnlyList<string> PairValues(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.DiscriminationPair)
            .Select(material => material.Value)
            .ToArray();
    }

    private static string LockedOutputValue(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.LockedOriginalOutput)
            .Value;
    }

    private static IReadOnlyList<string> AuditLines(string value)
    {
        return value
            .Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line[(line.IndexOf(':') + 1)..].Trim())
            .ToArray();
    }

    private static IReadOnlyList<string> SeededErrorValues(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.SeededError)
            .Select(material => material.Value)
            .ToArray();
    }
}
