using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class FocusHoldGeneratedContentTests
{
    [Fact]
    public void TargetHoldGenerationProducesValidTargetMaterialWithoutAdvancement()
    {
        var request = CreateTargetHoldRequest();

        var generated = FocusHoldGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("fh-seed-alpha"));

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
        Assert.Equal(BranchCode.FH, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L1, generated.Result.Level);
        Assert.Equal(DrillId.FH1TargetHold, generated.Result.Drill);
        Assert.Equal(PromptContentKind.EquivalentPrompt, generated.Result.ContentKind);
        Assert.DoesNotContain(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.DistractorPrompt);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TargetSubtlety, "simple phrase");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HoldDuration, "3 minutes");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.RecoveryWindow, "10 seconds");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, TargetAndDriftConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoSubstitutionConstraint);

        var targetStatement = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.TargetStatement);
        Assert.Contains("hold", targetStatement.Value, StringComparison.OrdinalIgnoreCase);
        Assert.False(
            targetStatement.Value.EndsWith(".", StringComparison.Ordinal),
            "Target material should name the target without sentence punctuation.");

        var evidenceShape = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.DriftMarkingEvidenceShape);
        Assert.Contains("drift", evidenceShape.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("return", evidenceShape.Value, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "target-statement" &&
            fact.Value == targetStatement.Value);
    }

    [Fact]
    public void DistractorHoldGenerationProducesIrrelevantDistractorsWithoutChangingTarget()
    {
        var request = CreateDistractorHoldRequest();

        var generated = FocusHoldGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("fh-seed-bravo"));

        var materialValidation = GeneratedContentMaterialValidator.Validate(
            generated.Result,
            generated.Materials);
        var loadValidation = GeneratedContentLoadConstraintValidator.Validate(
            generated.Result,
            generated.Materials);

        Assert.True(materialValidation.IsValid);
        Assert.True(loadValidation.IsValid);
        Assert.Equal(GlobalLevelId.L3, generated.Result.Level);
        Assert.Equal(DrillId.FH2DistractorHold, generated.Result.Drill);
        Assert.Equal(PromptContentKind.CueSequence, generated.Result.ContentKind);
        Assert.False(generated.GrantsAdvancement);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TargetSubtlety, "simple phrase");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HoldDuration, "5 minutes");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.RecoveryWindow, "10 seconds");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.DistractorFrequency, "periodic");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.DistractorSalience, "low");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, TargetAndDriftConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoSubstitutionConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoDistractorResponseConstraint);

        var targetStatement = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.TargetStatement);
        Assert.False(
            targetStatement.Value.EndsWith(".", StringComparison.Ordinal),
            "Target material should name the target without sentence punctuation.");
        var distractors = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.DistractorPrompt)
            .ToArray();

        Assert.NotEmpty(distractors);
        Assert.Contains(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.DistractorTiming);
        Assert.All(distractors, distractor =>
        {
            Assert.NotEqual(targetStatement.Value, distractor.Value);
            Assert.DoesNotContain(distractor.Value, targetStatement.Value, StringComparison.Ordinal);
        });

        var noResponseRule = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.DistractorNoResponseRule);
        Assert.Contains("no response", noResponseRule.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not part of target", noResponseRule.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SameSeedRepeatsContentAndFreshRequestChangesIdentityWithoutChangingDemand()
    {
        var request = CreateDistractorHoldRequest();
        var seed = new GeneratedContentSeed("fh-seed-charlie");

        var first = FocusHoldGeneratedContentGenerator.Generate(request, seed);
        var repeated = FocusHoldGeneratedContentGenerator.Generate(CreateDistractorHoldRequest(), seed);
        var fresh = FocusHoldGeneratedContentGenerator.Generate(
            CreateDistractorHoldRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(MaterialSnapshot(first.Materials), MaterialSnapshot(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
    }

    [Fact]
    public void FocusHoldTargetsUseSimpleVisualVocabulary()
    {
        var allowedSizes = new HashSet<string>(["small", "medium", "large"], StringComparer.Ordinal);
        var allowedColors = new HashSet<string>(["red", "blue", "green", "black"], StringComparer.Ordinal);
        var allowedPositions = new HashSet<string>(["left", "center", "right"], StringComparer.Ordinal);
        var allowedShapes = new HashSet<string>(["dot", "line", "square", "circle"], StringComparer.Ordinal);
        string[] forbiddenTerms = ["anchor", "lantern", "quiet", "steady", "clear"];
        var usedContentIds = new List<string>();

        for (var index = 0; index < 12; index++)
        {
            var generated = FocusHoldGeneratedContentGenerator.Generate(
                CreateTargetHoldRequest(previouslyUsedContentIds: usedContentIds),
                new GeneratedContentSeed("fh-simple-targets"));
            usedContentIds.Add(generated.Result.ContentId);

            var target = DisplayTargetValue(Assert.Single(generated.Materials, material =>
                material.Kind == GeneratedContentMaterialKind.TargetStatement).Value);
            var tokens = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(4, tokens.Length);
            Assert.Contains(tokens[0], allowedSizes);
            Assert.Contains(tokens[1], allowedColors);
            Assert.Contains(tokens[2], allowedPositions);
            Assert.Contains(tokens[3], allowedShapes);
            Assert.All(forbiddenTerms, term =>
                Assert.DoesNotContain(term, target, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void FocusHoldGenerationRejectsNonFocusHoldDrills()
    {
        var request = new GeneratedDrillContentRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            SessionType.Practice,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")],
            [new CriticalConstraint("No rereading after encode window."), new CriticalConstraint("No invented items.")]);

        Assert.Throws<ArgumentException>(() => FocusHoldGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("fh-seed-alpha")));
    }

    private const string TargetAndDriftConstraint = "Target is stated before set; every drift is marked.";

    private const string NoSubstitutionConstraint = "No target substitution.";

    private const string NoDistractorResponseConstraint = "Do not respond to distractor unless drill says so.";

    private static GeneratedDrillContentRequest CreateTargetHoldRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            SessionType.Practice,
            PromptContentKind.EquivalentPrompt,
            "fh-l1-target-hold",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("duration", "3 minutes"),
                new LoadVariable("target subtlety", "simple phrase"),
                new LoadVariable("recovery window", "10 seconds"),
            ],
            [
                new CriticalConstraint(TargetAndDriftConstraint),
                new CriticalConstraint(NoSubstitutionConstraint),
            ],
            previouslyUsedContentIds);
    }

    private static GeneratedDrillContentRequest CreateDistractorHoldRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.FH,
            GlobalLevelId.L3,
            DrillId.FH2DistractorHold,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "fh-l3-distractor-hold",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("duration", "5 minutes"),
                new LoadVariable("target subtlety", "simple phrase"),
                new LoadVariable("recovery window", "10 seconds"),
                new LoadVariable("distractor frequency", "periodic"),
                new LoadVariable("distractor salience", "low"),
            ],
            [
                new CriticalConstraint(TargetAndDriftConstraint),
                new CriticalConstraint(NoSubstitutionConstraint),
                new CriticalConstraint(NoDistractorResponseConstraint),
            ],
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

    private static string DisplayTargetValue(string value)
    {
        return StripPrefix(value, "Hold target phrase:")
            ?? StripPrefix(value, "Hold target word:")
            ?? value;
    }

    private static string? StripPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : null;
    }
}
