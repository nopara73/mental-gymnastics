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
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TargetStatement, "target", "Visual target: medium blue square"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TargetType, "target-type", "visual shape"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.HoldDuration, "duration", "5 minutes"),
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
    public void FocusHoldVisualTargetMaterialAcceptsTheExactCurrentContract()
    {
        var request = CreateFocusHoldRequest();
        var result = CreateResult(request);
        var materials = ValidFocusHoldMaterials();

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Failures);
    }

    [Theory]
    [InlineData("Visual target: large green left square")]
    [InlineData("Visual target: small red center dot")]
    [InlineData("Visual target: medium blue right circle")]
    public void FocusHoldVisualTargetMaterialRejectsUntestedPositionAttributes(string statement)
    {
        var request = CreateFocusHoldRequest();
        var result = CreateResult(request);
        var materials = ValidFocusHoldMaterials(targetStatement: statement);

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.InvalidMaterialValue &&
            failure.MaterialKind == GeneratedContentMaterialKind.TargetStatement &&
            failure.Detail.Contains("untested", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<InvalidOperationException>(() => ValidatedGeneratedDrillContent.Create(result, materials));
    }

    [Theory]
    [InlineData("Hold target phrase: medium blue square")]
    [InlineData("Visual target: medium blue square.")]
    [InlineData("Visual target: huge blue square")]
    [InlineData("Visual target: medium teal square")]
    [InlineData("Visual target: medium blue hexagon")]
    [InlineData("Visual target:  medium blue square")]
    public void FocusHoldVisualTargetMaterialRejectsMalformedOrUnsupportedStatements(string statement)
    {
        var request = CreateFocusHoldRequest();
        var result = CreateResult(request);
        var materials = ValidFocusHoldMaterials(targetStatement: statement);

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.InvalidMaterialValue &&
            failure.MaterialKind == GeneratedContentMaterialKind.TargetStatement &&
            failure.Detail.Contains("match exactly", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("visual phrase")]
    [InlineData("Visual shape")]
    [InlineData("shape")]
    public void FocusHoldVisualTargetMaterialRequiresExactVisualShapeType(string targetType)
    {
        var request = CreateFocusHoldRequest();
        var result = CreateResult(request);
        var materials = ValidFocusHoldMaterials(targetType: targetType);

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.InvalidMaterialValue &&
            failure.MaterialKind == GeneratedContentMaterialKind.TargetType &&
            failure.Detail.Contains("visual shape", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(GeneratedContentMaterialKind.TargetSet)]
    [InlineData(GeneratedContentMaterialKind.CueStep)]
    [InlineData(GeneratedContentMaterialKind.ValidCue)]
    [InlineData(GeneratedContentMaterialKind.InvalidCue)]
    [InlineData(GeneratedContentMaterialKind.ExpectedActiveTarget)]
    public void FocusShiftVisualMaterialsRejectLegacyOrMalformedProse(
        GeneratedContentMaterialKind materialKind)
    {
        var generated = FocusShiftGeneratedContentGenerator.Generate(
            CreateInvalidCueFilterRequest(),
            new GeneratedContentSeed("material-validation-fs"));
        var materials = ReplaceFirstMaterial(
            generated.Materials,
            materialKind,
            material => new GeneratedContentMaterial(material.Kind, material.Name, "legacy visual cue prose"));

        var validation = GeneratedContentMaterialValidator.Validate(generated.Result, materials);

        AssertInvalidMaterial(validation, materialKind, VisualStimulusCodec.FormatVersion);
    }

    [Theory]
    [InlineData(DrillId.IR1GoNoGoRule, GeneratedContentMaterialKind.GoNoGoCue)]
    [InlineData(DrillId.IR2ExceptionRule, GeneratedContentMaterialKind.CueStep)]
    public void InhibitionVisualCueMaterialsRejectLegacyOrMalformedProse(
        DrillId drill,
        GeneratedContentMaterialKind materialKind)
    {
        var request = drill == DrillId.IR1GoNoGoRule
            ? CreateGoNoGoRequest()
            : CreateExceptionRuleRequest();
        var generated = InhibitionGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed($"material-validation-{drill}"));
        var materials = ReplaceFirstMaterial(
            generated.Materials,
            materialKind,
            material => new GeneratedContentMaterial(material.Kind, material.Name, "legacy visual cue prose"));

        var validation = GeneratedContentMaterialValidator.Validate(generated.Result, materials);

        AssertInvalidMaterial(validation, materialKind, VisualStimulusCodec.FormatVersion);
    }

    [Fact]
    public void ExceptionRuleDefinitionsRejectLegacyOrMalformedProse()
    {
        var generated = InhibitionGeneratedContentGenerator.Generate(
            CreateExceptionRuleRequest(),
            new GeneratedContentSeed("material-validation-ir-exception"));
        var materials = ReplaceFirstMaterial(
            generated.Materials,
            GeneratedContentMaterialKind.ExceptionDefinition,
            material => new GeneratedContentMaterial(
                material.Kind,
                material.Name,
                "except the outlined square; withhold"));

        var validation = GeneratedContentMaterialValidator.Validate(generated.Result, materials);

        AssertInvalidMaterial(
            validation,
            GeneratedContentMaterialKind.ExceptionDefinition,
            "canonical encoded visual exception");
    }

    [Fact]
    public void DiscriminationPairsRejectLegacyOrMalformedProse()
    {
        var generated = DiscriminationGeneratedContentGenerator.Generate(
            CreatePairDiscriminationRequest(),
            new GeneratedContentSeed("material-validation-de-pair"));
        var materials = ReplaceFirstMaterial(
            generated.Materials,
            GeneratedContentMaterialKind.DiscriminationPair,
            material => new GeneratedContentMaterial(
                material.Kind,
                material.Name,
                "left blue mark versus right blue mark"));

        var validation = GeneratedContentMaterialValidator.Validate(generated.Result, materials);

        AssertInvalidMaterial(
            validation,
            GeneratedContentMaterialKind.DiscriminationPair,
            VisualStimulusCodec.PairFormatVersion);
    }

    [Fact]
    public void DiscriminationTruthMustAgreeWithDecodedRelevantFeatureResult()
    {
        var generated = DiscriminationGeneratedContentGenerator.Generate(
            CreatePairDiscriminationRequest(),
            new GeneratedContentSeed("material-validation-de-truth"));
        var firstPairMaterial = generated.Materials.First(material =>
            material.Kind == GeneratedContentMaterialKind.DiscriminationPair);
        var firstPair = VisualStimulusCodec.DecodePair(firstPairMaterial.Value);
        var wrongTruth = firstPair.RelevantFeatureMatches ? "mismatch" : "match";
        var materials = ReplaceFirstMaterial(
            generated.Materials,
            GeneratedContentMaterialKind.MatchTruth,
            material => new GeneratedContentMaterial(
                material.Kind,
                material.Name,
                $"{firstPairMaterial.Name}: {wrongTruth}; expected answer based only on relevant feature"));

        var validation = GeneratedContentMaterialValidator.Validate(generated.Result, materials);

        AssertInvalidMaterial(
            validation,
            GeneratedContentMaterialKind.MatchTruth,
            "decoded relevant-feature result");
    }

    [Fact]
    public void DiscriminationTruthMustFollowPairIdAndMaterialOrder()
    {
        var generated = DiscriminationGeneratedContentGenerator.Generate(
            CreatePairDiscriminationRequest(),
            new GeneratedContentSeed("material-validation-de-order"));
        var truths = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.MatchTruth)
            .ToArray();
        var secondTruth = truths[1];
        var materials = ReplaceFirstMaterial(
            generated.Materials,
            GeneratedContentMaterialKind.MatchTruth,
            _ => new GeneratedContentMaterial(secondTruth.Kind, secondTruth.Name, secondTruth.Value));

        var validation = GeneratedContentMaterialValidator.Validate(generated.Result, materials);

        AssertInvalidMaterial(
            validation,
            GeneratedContentMaterialKind.MatchTruth,
            "pair ID");
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

    private static GeneratedDrillContentRequest CreateFocusHoldRequest()
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
                new LoadVariable("target subtlety", "simple visual target"),
            ],
            [
                new CriticalConstraint("Target is stated before set; every noticed drift is marked once."),
                new CriticalConstraint("No target substitution."),
            ],
            ["content-fh-l1-a"]);
    }

    private static GeneratedDrillContentRequest CreateInvalidCueFilterRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.FS,
            GlobalLevelId.L3,
            DrillId.FS2InvalidCueFilter,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "fs-l3-invalid-cue-filter",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("target count", "2"),
                new LoadVariable("switch count", "6"),
                new LoadVariable("cue density", "moderate"),
                new LoadVariable("rule contrast", "valid symbol versus invalid lure"),
                new LoadVariable("return precision", "next valid cue"),
            ],
            [
                new CriticalConstraint("Switch only on valid cue."),
                new CriticalConstraint("Invalid cues must not trigger switch."),
                new CriticalConstraint("No anticipatory switching."),
            ]);
    }

    private static GeneratedDrillContentRequest CreateGoNoGoRequest()
    {
        return new GeneratedDrillContentRequest(
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
                new LoadVariable("no-go frequency", "every third cue"),
            ],
            [new CriticalConstraint("Premature response fails item.")]);
    }

    private static GeneratedDrillContentRequest CreateExceptionRuleRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.IR,
            GlobalLevelId.L2,
            DrillId.IR2ExceptionRule,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "ir-l2-exception-rule",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("exception count", "3"),
                new LoadVariable("response speed", "2 seconds"),
                new LoadVariable("similarity", "near symbols"),
            ],
            [
                new CriticalConstraint("Rule and exceptions stated before set."),
                new CriticalConstraint("Rule cannot change mid-set."),
            ]);
    }

    private static GeneratedDrillContentRequest CreatePairDiscriminationRequest()
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
            [new CriticalConstraint("Guessing must be marked.")]);
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

    private static IReadOnlyList<GeneratedContentMaterial> ValidFocusHoldMaterials(
        string targetStatement = "Visual target: medium blue square",
        string targetType = "visual shape")
    {
        return
        [
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "duration", "3 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "target subtlety", "simple visual target"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "target-and-drift",
                "Target is stated before set; every noticed drift is marked once."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "no-target-substitution",
                "No target substitution."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.TargetStatement,
                "target-statement",
                targetStatement),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.TargetType,
                "target-type",
                targetType),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.TargetSubtlety,
                "target-subtlety",
                "simple visual target"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HoldDuration,
                "duration",
                "3 minutes"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.DriftMarkingEvidenceShape,
                "drift-evidence",
                "mark every noticed drift once; continue with the same target; target substitution prohibited"),
        ];
    }

    private static IReadOnlyList<GeneratedContentMaterial> ReplaceFirstMaterial(
        IEnumerable<GeneratedContentMaterial> materials,
        GeneratedContentMaterialKind kind,
        Func<GeneratedContentMaterial, GeneratedContentMaterial> replacement)
    {
        var replaced = false;
        var result = materials.Select(material =>
        {
            if (replaced || material.Kind != kind)
            {
                return material;
            }

            replaced = true;
            return replacement(material);
        }).ToArray();

        Assert.True(replaced, $"Expected generated material kind {kind} was not found.");
        return result;
    }

    private static void AssertInvalidMaterial(
        GeneratedContentMaterialValidationResult validation,
        GeneratedContentMaterialKind materialKind,
        string detailFragment)
    {
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.InvalidMaterialValue &&
            failure.MaterialKind == materialKind &&
            failure.Detail.Contains(detailFragment, StringComparison.OrdinalIgnoreCase));
    }
}
