using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentMaterialValidationTests
{
    [Fact]
    public void ValidGeneratedContentCanBeMarkedRuntimeUsableWithoutGrantingAdvancement()
    {
        var request = CreateDelayedReconstructionRequest();
        var result = CreateResult(request);
        var materials = ValidDelayedReconstructionMaterials();

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);
        var validated = ValidatedGeneratedDrillContent.Create(result, materials);

        Assert.True(validation.IsValid);
        Assert.True(validation.CanBeConsumedByRuntime);
        Assert.False(validation.GrantsAdvancement);
        Assert.Empty(validation.Failures);
        Assert.Same(result, validated.Result);
        Assert.True(validated.CanBeConsumedByRuntime);
        Assert.Equal(materials.Count, validated.Materials.Count);
    }

    [Fact]
    public void MissingRequiredDrillMaterialIsRejectedBeforeRuntimeUse()
    {
        var request = CreateDelayedReconstructionRequest();
        var result = CreateResult(request);
        var materials = ValidDelayedReconstructionMaterials()
            .Where(material => material.Kind != GeneratedContentMaterialKind.ReconstructionInstruction)
            .ToArray();

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);

        Assert.False(validation.IsValid);
        Assert.False(validation.CanBeConsumedByRuntime);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingRequiredMaterial &&
            failure.MaterialKind == GeneratedContentMaterialKind.ReconstructionInstruction);
        Assert.Throws<InvalidOperationException>(() => ValidatedGeneratedDrillContent.Create(result, materials));
    }

    [Fact]
    public void MissingLoadVariableMaterialIsRejected()
    {
        var request = CreateDelayedReconstructionRequest();
        var result = CreateResult(request);
        var materials = ValidDelayedReconstructionMaterials()
            .Where(material =>
                material.Kind != GeneratedContentMaterialKind.LoadVariable ||
                !string.Equals(material.Name, "delay", StringComparison.Ordinal))
            .ToArray();

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingLoadVariableMaterial &&
            failure.Detail.Contains("delay", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingHonestyConstraintMaterialIsRejected()
    {
        var request = CreateDelayedReconstructionRequest();
        var result = CreateResult(request);
        var materials = ValidDelayedReconstructionMaterials()
            .Where(material =>
                material.Kind != GeneratedContentMaterialKind.HonestyConstraint ||
                !string.Equals(material.Value, "No invented items.", StringComparison.Ordinal))
            .ToArray();

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingHonestyConstraintMaterial &&
            failure.Detail.Contains("No invented items.", StringComparison.Ordinal));
    }

    [Fact]
    public void DrillContentKindMismatchIsRejected()
    {
        var request = CreateDelayedReconstructionRequest(contentKind: PromptContentKind.CueSequence);
        var result = CreateResult(request);

        var validation = GeneratedContentMaterialValidator.Validate(
            result,
            ValidDelayedReconstructionMaterials());

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.ContentKindMismatch &&
            failure.Detail.Contains(nameof(PromptContentKind.DelayedReconstructionTask), StringComparison.Ordinal));
    }

    [Fact]
    public void BranchDrillMismatchIsRejected()
    {
        var request = new GeneratedDrillContentRequest(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            SessionType.Test,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")],
            [new CriticalConstraint("No rereading after encode window."), new CriticalConstraint("No invented items.")],
            ["content-wm-l1-a"]);
        var result = CreateResult(request);

        var validation = GeneratedContentMaterialValidator.Validate(
            result,
            ValidDelayedReconstructionMaterials());

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.BranchDrillMismatch &&
            failure.Detail.Contains(nameof(BranchCode.WM), StringComparison.Ordinal));
    }

    [Fact]
    public void FhDistractorContentMustIncludeTargetSubtletyAndDistractorFrequency()
    {
        var request = new GeneratedDrillContentRequest(
            BranchCode.FH,
            GlobalLevelId.L3,
            DrillId.FH2DistractorHold,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "fh-l3-distractor-hold",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("duration", "5 minutes"), new LoadVariable("distractor frequency", "periodic")],
            [new CriticalConstraint("Do not respond to distractor unless drill says so.")],
            ["content-fh-l3-a"]);
        var result = CreateResult(request);
        var materials = new[]
        {
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "duration", "5 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "distractor frequency", "periodic"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "no-distractor-response",
                "Do not respond to distractor unless drill says so."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TargetStatement, "target", "Hold the target phrase."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TargetType, "target-type", "phrase"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.HoldDuration, "duration", "5 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.RecoveryWindow, "return-window", "10 seconds"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DriftMarkingEvidenceShape, "drift-log", "marked drift ids"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DistractorPrompt, "distractor-1", "irrelevant prompt"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DistractorTiming, "distractor-1-timing", "00:45"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DistractorNoResponseRule, "distractor-rule", "ignore"),
        };

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingRequiredMaterial &&
            failure.MaterialKind == GeneratedContentMaterialKind.TargetSubtlety);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingRequiredMaterial &&
            failure.MaterialKind == GeneratedContentMaterialKind.DistractorFrequency);
    }

    [Fact]
    public void MalformedGeneratedContentMaterialIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeneratedContentMaterial(
            (GeneratedContentMaterialKind)999,
            "material-id",
            "value"));
        Assert.Throws<ArgumentException>(() => new GeneratedContentMaterial(
            GeneratedContentMaterialKind.EncodeItem,
            " ",
            "item"));
        Assert.Throws<ArgumentException>(() => new GeneratedContentMaterial(
            GeneratedContentMaterialKind.EncodeItem,
            "item-1",
            " "));
    }

    private static GeneratedDrillContentRequest CreateDelayedReconstructionRequest(
        PromptContentKind contentKind = PromptContentKind.DelayedReconstructionTask)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            SessionType.Test,
            contentKind,
            "wm-l1-delayed-reconstruction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")],
            [new CriticalConstraint("No rereading after encode window."), new CriticalConstraint("No invented items.")],
            ["content-wm-l1-a"]);
    }

    private static GeneratedDrillContentResult CreateResult(GeneratedDrillContentRequest request)
    {
        return new GeneratedDrillContentResult(
            request,
            new GeneratedDrillInstanceDescriptor(
                "generated-wm-l1-001",
                new PromptContentIdentity(
                    "content-wm-l1-b",
                    request.Branch,
                    request.Level,
                    request.Drill,
                    request.ContentKind,
                    request.EquivalenceClass),
                "content-v1",
                request.FreshnessPolicy,
                request.LoadVariables,
                request.CriticalConstraints),
            [
                new GeneratedContentPayloadFact("fixture-id", "wm-l1-delayed-reconstruction-b"),
                new GeneratedContentPayloadFact("payload-family", "delayed-reconstruction"),
            ]);
    }

    private static IReadOnlyList<GeneratedContentMaterial> ValidDelayedReconstructionMaterials()
    {
        return
        [
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "item count", "5"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "delay", "60 seconds"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "no-reread",
                "No rereading after encode window."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "no-invention",
                "No invented items."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeInstruction, "encode-instruction", "Study once."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DelayLength, "delay", "60 seconds"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ReconstructionInstruction,
                "reconstruct",
                "Reconstruct the five items in order."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.ExpectedReconstruction, "expected", "alpha|bravo|cedar|delta|ember"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-1", "alpha"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-2", "bravo"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-3", "cedar"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-4", "delta"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-5", "ember"),
        ];
    }
}
