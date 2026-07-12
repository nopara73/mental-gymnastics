using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class AffectiveInterferenceGeneratedContentTests
{
    [Fact]
    public void PressureRepeatGenerationProducesValidSourceStandardWrapperWithoutAdvancement()
    {
        var request = CreatePressureRepeatRequest();

        var generated = AffectiveInterferenceGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("ai-pressure-alpha"));

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
        Assert.Equal(BranchCode.AI, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L1, generated.Result.Level);
        Assert.Equal(DrillId.AI1PressureRepeat, generated.Result.Drill);
        Assert.Equal(PromptContentKind.EquivalentPrompt, generated.Result.ContentKind);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TimePressure, "90 seconds");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.EvaluativePressure, "visible evaluator note");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, OriginalStandardCannotBeLoweredConstraint);

        var sourceStandard = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.SourceBranchStandard);
        Assert.Contains("original branch standard", sourceStandard.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FH-L3", sourceStandard.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FH2DistractorHold", sourceStandard.Value, StringComparison.Ordinal);
        Assert.Contains("5 minutes", sourceStandard.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no response to distractor", sourceStandard.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no more than 5 drifts", sourceStandard.Value, StringComparison.OrdinalIgnoreCase);

        var sourceTask = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.SourceTask);
        Assert.Contains("executable source task", sourceTask.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FH-L3", sourceTask.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wrapped source criterion", sourceTask.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no more than 5 drifts", sourceTask.Value, StringComparison.OrdinalIgnoreCase);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.SourceDrill, "FH2DistractorHold");
        Assert.Contains(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.TargetStatement);
        Assert.Contains(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.DistractorPrompt);

        var pressureSource = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.PressureSource);
        Assert.Contains("defined pressure source", pressureSource.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clean evidence collection", pressureSource.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("90 seconds", pressureSource.Value, StringComparison.OrdinalIgnoreCase);

        var noLoweringMarker = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.NoStandardLoweringMarker);
        Assert.Contains("cannot be lowered", noLoweringMarker.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pressure cannot excuse errors", noLoweringMarker.Value, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "ai-pressure-repeat");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "source-branch-standard" &&
            fact.Value.Contains("FH-L3", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "pressure-source" &&
            fact.Value.Contains("defined pressure source", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "no-standard-lowering-evidence" &&
            fact.Value.Contains("cannot be lowered", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "clean-evidence-collection-policy" &&
            fact.Value.Contains("clean evidence", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "source-instance-id" &&
            !string.IsNullOrWhiteSpace(fact.Value));
    }

    [Fact]
    public void PressureRepeatFreshVariantChangesPressureSourceWithoutChangingSourceStandard()
    {
        var request = CreatePressureRepeatRequest();
        var seed = new GeneratedContentSeed("ai-pressure-bravo");

        var first = AffectiveInterferenceGeneratedContentGenerator.Generate(request, seed);
        var repeated = AffectiveInterferenceGeneratedContentGenerator.Generate(CreatePressureRepeatRequest(), seed);
        var fresh = AffectiveInterferenceGeneratedContentGenerator.Generate(
            CreatePressureRepeatRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(PressureSourceValue(first.Materials), PressureSourceValue(fresh.Materials));
        Assert.Equal(SourceBranchStandardValue(first.Materials), SourceBranchStandardValue(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.TimePressure, "90 seconds");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.EvaluativePressure, "visible evaluator note");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.HonestyConstraint, OriginalStandardCannotBeLoweredConstraint);
    }

    [Fact]
    public void DisruptionRecoveryGenerationProducesValidSourceTaskWrapperWithRecoveryConstraints()
    {
        var request = CreateDisruptionRecoveryRequest();

        var generated = AffectiveInterferenceGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("ai-disruption-alpha"));

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
        Assert.Equal(BranchCode.AI, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L3, generated.Result.Level);
        Assert.Equal(DrillId.AI2DisruptionRecovery, generated.Result.Drill);
        Assert.Equal(PromptContentKind.EquivalentPrompt, generated.Result.ContentKind);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, FullRestartProhibitedConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.DisruptionTiming, "mid-task after first checkpoint");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.RestartDelay, "10 seconds");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TaskComplexity, "two-target cue sequence");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.RecoveryWindow, "30 seconds");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.SourceDrill, "FS2InvalidCueFilter");
        Assert.Contains(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.TargetSet);
        Assert.Contains(generated.Materials, material =>
            material.Kind is GeneratedContentMaterialKind.ValidCue or GeneratedContentMaterialKind.InvalidCue);

        var sourceTask = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.SourceTask);
        Assert.Contains("source task", sourceTask.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FS-L3", sourceTask.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FS2InvalidCueFilter", sourceTask.Value, StringComparison.Ordinal);
        Assert.Contains("wrapped source criterion", sourceTask.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid cues must never", sourceTask.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("underlying branch demand", sourceTask.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not replace", sourceTask.Value, StringComparison.OrdinalIgnoreCase);

        var disruptionEvent = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.DisruptionEvent);
        Assert.Contains("disruption event", disruptionEvent.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("underlying source task continues", disruptionEvent.Value, StringComparison.OrdinalIgnoreCase);

        var restartRule = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.RestartRule);
        Assert.Contains("full restart prohibited", restartRule.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("10 seconds", restartRule.Value, StringComparison.OrdinalIgnoreCase);

        var postDisruptionEvidence = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.PostDisruptionEvidence);
        Assert.Contains("recovery time", postDisruptionEvidence.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("post-disruption errors", postDisruptionEvidence.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source standard", postDisruptionEvidence.Value, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "ai-disruption-recovery");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "source-task" &&
            fact.Value.Contains("FS-L3", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "disruption-event" &&
            fact.Value.Contains("disruption event", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "restart-prohibition-evidence" &&
            fact.Value.Contains("full restart prohibited", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "recovery-window" &&
            fact.Value.Contains("30 seconds", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "source-demand-preservation" &&
            fact.Value.Contains("underlying branch demand", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DisruptionRecoveryFreshVariantChangesDisruptionEventWithoutChangingSourceTask()
    {
        var request = CreateDisruptionRecoveryRequest();
        var seed = new GeneratedContentSeed("ai-disruption-bravo");

        var first = AffectiveInterferenceGeneratedContentGenerator.Generate(request, seed);
        var repeated = AffectiveInterferenceGeneratedContentGenerator.Generate(CreateDisruptionRecoveryRequest(), seed);
        var fresh = AffectiveInterferenceGeneratedContentGenerator.Generate(
            CreateDisruptionRecoveryRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(DisruptionEventValue(first.Materials), DisruptionEventValue(fresh.Materials));
        Assert.Equal(SourceTaskValue(first.Materials), SourceTaskValue(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.HonestyConstraint, FullRestartProhibitedConstraint);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.DisruptionTiming, "mid-task after first checkpoint");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.RestartDelay, "10 seconds");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.TaskComplexity, "two-target cue sequence");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.RecoveryWindow, "30 seconds");
    }

    [Fact]
    public void AffectiveInterferenceGenerationRejectsNonAffectiveInterferenceDrills()
    {
        var request = new GeneratedDrillContentRequest(
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
            [new CriticalConstraint("Rule stated before unseen examples.")]);

        Assert.Throws<ArgumentException>(() => AffectiveInterferenceGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("ai-pressure-alpha")));
    }

    private const string OriginalStandardCannotBeLoweredConstraint = "Original standard cannot be lowered.";
    private const string FullRestartProhibitedConstraint = "Full restart prohibited unless specified.";

    private static GeneratedDrillContentRequest CreatePressureRepeatRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.AI,
            GlobalLevelId.L1,
            DrillId.AI1PressureRepeat,
            SessionType.Practice,
            PromptContentKind.EquivalentPrompt,
            "ai-l1-pressure-repeat-fh-l3",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("time pressure", "90 seconds"),
                new LoadVariable("observation", "visible evaluator note"),
            ],
            [new CriticalConstraint(OriginalStandardCannotBeLoweredConstraint)],
            previouslyUsedContentIds);
    }

    private static GeneratedDrillContentRequest CreateDisruptionRecoveryRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.AI,
            GlobalLevelId.L3,
            DrillId.AI2DisruptionRecovery,
            SessionType.Practice,
            PromptContentKind.EquivalentPrompt,
            "ai-l3-disruption-recovery-fs-l3",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("interruption timing", "mid-task after first checkpoint"),
                new LoadVariable("restart delay", "10 seconds"),
                new LoadVariable("task complexity", "two-target cue sequence"),
                new LoadVariable("recovery window", "30 seconds"),
            ],
            [new CriticalConstraint(FullRestartProhibitedConstraint)],
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

    private static string PressureSourceValue(IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.PressureSource)
            .Value;
    }

    private static string SourceBranchStandardValue(IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.SourceBranchStandard)
            .Value;
    }

    private static string DisruptionEventValue(IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.DisruptionEvent)
            .Value;
    }

    private static string SourceTaskValue(IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.SourceTask)
            .Value;
    }
}
