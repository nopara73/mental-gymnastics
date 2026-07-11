using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class FocusShiftGeneratedContentTests
{
    [Fact]
    public void CueSwitchGenerationProducesValidCueScheduleWithoutAdvancement()
    {
        var request = CreateCueSwitchRequest();

        var generated = FocusShiftGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("fs-seed-alpha"));

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
        Assert.Equal(BranchCode.FS, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L1, generated.Result.Level);
        Assert.Equal(DrillId.FS1CueSwitch, generated.Result.Drill);
        Assert.Equal(PromptContentKind.CueSequence, generated.Result.ContentKind);
        Assert.DoesNotContain(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.InvalidCue);

        Assert.Equal(2, generated.Materials.Count(material =>
            material.Kind == GeneratedContentMaterialKind.TargetSet));
        Assert.True(generated.Materials.Count(material =>
            material.Kind == GeneratedContentMaterialKind.CueStep) >= 4);
        Assert.True(generated.Materials.Count(material =>
            material.Kind == GeneratedContentMaterialKind.ValidCue) >= 4);
        var expectedTargets = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedActiveTarget)
            .Select(material => material.Value)
            .ToArray();
        Assert.All(expectedTargets.Zip(expectedTargets.Skip(1)), pair =>
            Assert.NotEqual(pair.First, pair.Second));
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.CueDensity, "low");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.ReturnPrecision, "next cue");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, ValidCueConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoAnticipatorySwitchingConstraint);

        var targetCountConstraint = Assert.Single(loadValidation.Plan.Constraints, constraint =>
            string.Equals(constraint.LoadVariableName, "target count", StringComparison.Ordinal));
        Assert.Equal(GeneratedContentLoadConstraintMatchKind.MinimumMaterialCount, targetCountConstraint.MatchKind);
        Assert.Equal(2, targetCountConstraint.MinimumCount);
        Assert.Equal([GeneratedContentMaterialKind.TargetSet], targetCountConstraint.MaterialKinds);

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "sequence-accuracy-evidence" &&
            fact.Value.Contains("valid cue responses", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "uncued-switch-policy" &&
            fact.Value.Contains("invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidCueFilterGenerationPreservesInvalidCueNoSwitchRule()
    {
        var request = CreateInvalidCueFilterRequest();

        var generated = FocusShiftGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("fs-seed-bravo"));

        var materialValidation = GeneratedContentMaterialValidator.Validate(
            generated.Result,
            generated.Materials);
        var loadValidation = GeneratedContentLoadConstraintValidator.Validate(
            generated.Result,
            generated.Materials);

        Assert.True(materialValidation.IsValid);
        Assert.True(loadValidation.IsValid);
        Assert.Equal(GlobalLevelId.L3, generated.Result.Level);
        Assert.Equal(DrillId.FS2InvalidCueFilter, generated.Result.Drill);
        Assert.False(generated.GrantsAdvancement);

        Assert.Equal(2, generated.Materials.Count(material =>
            material.Kind == GeneratedContentMaterialKind.TargetSet));
        Assert.True(generated.Materials.Count(material =>
            material.Kind == GeneratedContentMaterialKind.CueStep) >= 6);
        Assert.Contains(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.ValidCue);
        Assert.Contains(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.InvalidCue);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.CueDensity, "moderate");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.RuleContrast, "valid symbol versus invalid lure");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.ReturnPrecision, "next valid cue");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, ValidCueConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, InvalidCueConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoAnticipatorySwitchingConstraint);

        var validCueValues = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ValidCue)
            .Select(material => material.Value)
            .ToHashSet(StringComparer.Ordinal);
        var invalidCueValues = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.InvalidCue)
            .Select(material => material.Value)
            .ToArray();

        Assert.All(validCueValues, validCue =>
            Assert.DoesNotContain("valid cue", validCue, StringComparison.OrdinalIgnoreCase));
        Assert.All(invalidCueValues, invalidCue =>
            Assert.DoesNotContain("lure", invalidCue, StringComparison.OrdinalIgnoreCase));
        Assert.All(invalidCueValues, invalidCue =>
            Assert.DoesNotContain(invalidCue, validCueValues));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "invalid-cue-policy" &&
            fact.Value.Contains("must not trigger switch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SameSeedRepeatsContentAndFreshRequestChangesSequenceWithoutChangingDemand()
    {
        var request = CreateInvalidCueFilterRequest();
        var seed = new GeneratedContentSeed("fs-seed-charlie");

        var first = FocusShiftGeneratedContentGenerator.Generate(request, seed);
        var repeated = FocusShiftGeneratedContentGenerator.Generate(CreateInvalidCueFilterRequest(), seed);
        var fresh = FocusShiftGeneratedContentGenerator.Generate(
            CreateInvalidCueFilterRequest(previouslyUsedContentIds: [first.Result.ContentId]),
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
    public void FocusShiftGenerationRejectsNonFocusShiftDrills()
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
                new LoadVariable("recovery window", "10 seconds"),
            ],
            [new CriticalConstraint("Target is stated before set; every drift is marked.")]);

        Assert.Throws<ArgumentException>(() => FocusShiftGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("fs-seed-alpha")));
    }

    private const string ValidCueConstraint = "Switch only on valid cue.";

    private const string InvalidCueConstraint = "Invalid cues must not trigger switch.";

    private const string NoAnticipatorySwitchingConstraint = "No anticipatory switching.";

    private static GeneratedDrillContentRequest CreateCueSwitchRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.FS,
            GlobalLevelId.L1,
            DrillId.FS1CueSwitch,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "fs-l1-cue-switch",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("target count", "2"),
                new LoadVariable("switch count", "4"),
                new LoadVariable("cue density", "low"),
                new LoadVariable("return precision", "next cue"),
            ],
            [
                new CriticalConstraint(ValidCueConstraint),
                new CriticalConstraint(NoAnticipatorySwitchingConstraint),
            ]);
    }

    private static GeneratedDrillContentRequest CreateInvalidCueFilterRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
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
                new CriticalConstraint(ValidCueConstraint),
                new CriticalConstraint(InvalidCueConstraint),
                new CriticalConstraint(NoAnticipatorySwitchingConstraint),
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
}
