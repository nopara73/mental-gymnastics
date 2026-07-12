using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class StabilizationGeneratedContentTests
{
    [Fact]
    public void StabilizationPackageContainsOneObservableNoResponseDistractor()
    {
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.FH, GlobalLevelId.L1);
        var request = new GeneratedDrillContentRequest(
            profile.Branch,
            profile.Level,
            profile.Drill,
            SessionType.Stabilization,
            PromptContentKind.EquivalentPrompt,
            "fh-l1-stabilization",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            profile.TargetStage.LoadVariables,
            [new CriticalConstraint("Target is stated before set; every noticed drift is marked once.")]);
        var generated = FocusHoldGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("stabilization-controlled-demand"));
        var materials = StabilizationGeneratedContent.AddControlledDistractor(
            generated.Result,
            generated.Materials);
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == profile.Branch && item.Level == profile.Level);

        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            materials,
            standard);

        var cue = Assert.Single(package.Cues, item =>
            item.Id == StabilizationGeneratedContent.ControlledDistractorId);
        Assert.Equal(GeneratedRuntimeCueResponseExpectation.NoResponseExpected, cue.ResponseExpectation);
        Assert.Contains(materials, material =>
            material.Kind == GeneratedContentMaterialKind.Interference &&
            material.Name == StabilizationGeneratedContent.ControlledDistractorId);
    }
}
