using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class WorkingMemoryGeneratedContentTests
{
    [Fact]
    public void DelayedReconstructionGenerationProducesValidAuditableEncodeMaterial()
    {
        var request = CreateDelayedReconstructionRequest();

        var generated = WorkingMemoryGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("wm-seed-alpha"));

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
        Assert.Equal(BranchCode.WM, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L1, generated.Result.Level);
        Assert.Equal(DrillId.WM1DelayedReconstruction, generated.Result.Drill);
        Assert.Equal(PromptContentKind.DelayedReconstructionTask, generated.Result.ContentKind);

        var encodeItems = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.EncodeItem)
            .ToArray();
        Assert.Equal(5, encodeItems.Length);
        Assert.All(encodeItems, item => Assert.False(string.IsNullOrWhiteSpace(item.Value)));
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.DetailDensity, "simple objects");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.DelayLength, "60 seconds");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoRereadConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoInventedItemsConstraint);

        var encodeInstruction = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.EncodeInstruction);
        Assert.Contains("once", encodeInstruction.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no rereading", encodeInstruction.Value, StringComparison.OrdinalIgnoreCase);

        var reconstructionInstruction = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.ReconstructionInstruction);
        Assert.Contains("without rereading", reconstructionInstruction.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not invent", reconstructionInstruction.Value, StringComparison.OrdinalIgnoreCase);

        var expected = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.ExpectedReconstruction);
        Assert.Equal(string.Join("|", encodeItems.Select(item => item.Value)), expected.Value);

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "comparison-key" &&
            fact.Value == expected.Value);
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "omission-evidence" &&
            fact.Value.Contains("missing expected item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "invention-evidence" &&
            fact.Value.Contains("not present in encode set", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DelayedReconstructionGenerationProducesFreshEquivalentContentWhenRequired()
    {
        var request = CreateDelayedReconstructionRequest();
        var seed = new GeneratedContentSeed("wm-seed-bravo");

        var first = WorkingMemoryGeneratedContentGenerator.Generate(request, seed);
        var repeated = WorkingMemoryGeneratedContentGenerator.Generate(CreateDelayedReconstructionRequest(), seed);
        var fresh = WorkingMemoryGeneratedContentGenerator.Generate(
            CreateDelayedReconstructionRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(EncodeItemValues(first.Materials), EncodeItemValues(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.DetailDensity, "simple objects");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.DelayLength, "60 seconds");
    }

    [Fact]
    public void MentalTransformGenerationProducesValidAuditableTransformMaterial()
    {
        var request = CreateMentalTransformRequest();

        var generated = WorkingMemoryGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("wm-transform-alpha"));

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
        Assert.Equal(BranchCode.WM, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L3, generated.Result.Level);
        Assert.Equal(DrillId.WM2MentalTransform, generated.Result.Drill);
        Assert.Equal(PromptContentKind.DelayedReconstructionTask, generated.Result.ContentKind);

        var sourceItems = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.SourceItem)
            .ToArray();
        var operationSteps = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.OperationStep)
            .ToArray();
        Assert.Equal(6, sourceItems.Length);
        Assert.Equal(2, operationSteps.Length);
        Assert.All(sourceItems, item => Assert.False(string.IsNullOrWhiteSpace(item.Value)));
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.DetailDensity, "simple objects");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.DelayLength, "2 minutes");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.Interference, "reversal");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoHiddenIntermediateNotesConstraint);

        var transformRule = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.TransformRule);
        Assert.Contains("revers", transformRule.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("held source", transformRule.Value, StringComparison.OrdinalIgnoreCase);

        var hiddenNotePolicy = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.HiddenNotePolicy);
        Assert.Contains("hidden", hiddenNotePolicy.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prohibited", hiddenNotePolicy.Value, StringComparison.OrdinalIgnoreCase);

        var finalExpectedOutput = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.FinalExpectedOutput);
        Assert.False(string.IsNullOrWhiteSpace(finalExpectedOutput.Value));
        Assert.NotEqual(string.Join("|", sourceItems.Select(item => item.Value)), finalExpectedOutput.Value);

        var explanationPrompt = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.RuleExplanationPrompt);
        Assert.Contains("explain", explanationPrompt.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operation", explanationPrompt.Value, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "wm-mental-transform");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "final-expected-output" &&
            fact.Value == finalExpectedOutput.Value);
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "operation-explanation-evidence" &&
            fact.Value.Contains("explain", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "hidden-note-policy" &&
            fact.Value.Contains("hidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MentalTransformGenerationProducesFreshEquivalentContentWhenRequired()
    {
        var request = CreateMentalTransformRequest();
        var seed = new GeneratedContentSeed("wm-transform-bravo");

        var first = WorkingMemoryGeneratedContentGenerator.Generate(request, seed);
        var repeated = WorkingMemoryGeneratedContentGenerator.Generate(CreateMentalTransformRequest(), seed);
        var fresh = WorkingMemoryGeneratedContentGenerator.Generate(
            CreateMentalTransformRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(SourceItemValues(first.Materials), SourceItemValues(fresh.Materials));
        Assert.NotEqual(FinalExpectedOutput(first.Materials), FinalExpectedOutput(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.DelayLength, "2 minutes");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.Interference, "reversal");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoHiddenIntermediateNotesConstraint);
    }

    [Fact]
    public void WorkingMemoryGenerationRejectsNonWorkingMemoryDrills()
    {
        var request = new GeneratedDrillContentRequest(
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
            ],
            [
                new CriticalConstraint("Target is stated before set; every noticed drift is marked once."),
            ]);

        Assert.Throws<ArgumentException>(() => WorkingMemoryGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("wm-seed-alpha")));
    }

    private const string NoRereadConstraint = "No rereading after encode window.";

    private const string NoInventedItemsConstraint = "No invented items.";

    private const string NoHiddenIntermediateNotesConstraint = "Intermediate notes prohibited unless specified.";

    private static GeneratedDrillContentRequest CreateDelayedReconstructionRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            SessionType.Practice,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("delay", "60 seconds"),
            ],
            [
                new CriticalConstraint(NoRereadConstraint),
                new CriticalConstraint(NoInventedItemsConstraint),
            ],
            previouslyUsedContentIds);
    }

    private static GeneratedDrillContentRequest CreateMentalTransformRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.WM,
            GlobalLevelId.L3,
            DrillId.WM2MentalTransform,
            SessionType.Practice,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l3-mental-transform",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("item count", "6"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("operation steps", "2"),
                new LoadVariable("delay", "2 minutes"),
                new LoadVariable("interference", "reversal"),
            ],
            [
                new CriticalConstraint(NoHiddenIntermediateNotesConstraint),
                new CriticalConstraint("Final output and operation explanation must be auditable."),
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

    private static IReadOnlyList<string> EncodeItemValues(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.EncodeItem)
            .Select(material => material.Value)
            .ToArray();
    }

    private static IReadOnlyList<string> SourceItemValues(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.SourceItem)
            .Select(material => material.Value)
            .ToArray();
    }

    private static string FinalExpectedOutput(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.FinalExpectedOutput)
            .Value;
    }
}
