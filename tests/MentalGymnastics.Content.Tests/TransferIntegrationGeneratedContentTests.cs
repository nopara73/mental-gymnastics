using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class TransferIntegrationGeneratedContentTests
{
    [Fact]
    public void CompositeTaskGenerationProducesSeparateEvidenceRequirementsForEveryComponentBranch()
    {
        var request = CreateCompositeTaskRequest();

        var generated = TransferIntegrationGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("ti-composite-alpha"));

        var materialValidation = GeneratedContentMaterialValidator.Validate(
            generated.Result,
            generated.Materials);
        var loadValidation = GeneratedContentLoadConstraintValidator.Validate(
            generated.Result,
            generated.Materials);

        Assert.True(materialValidation.IsValid, string.Join("; ", materialValidation.Failures.Select(failure => failure.Detail)));
        Assert.True(loadValidation.IsValid, string.Join("; ", loadValidation.Failures.Select(failure => failure.Detail)));
        Assert.True(generated.ValidatedContent.CanBeConsumedByRuntime);
        Assert.False(generated.GrantsAdvancement);
        Assert.Equal(BranchCode.TI, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L1, generated.Result.Level);
        Assert.Equal(DrillId.TI1CompositeTask, generated.Result.Drill);
        Assert.Equal(PromptContentKind.EquivalentPrompt, generated.Result.ContentKind);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, SeparateEvidenceConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TaskLength, "12 minutes");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TransferDistance, "near transfer");

        var componentPayloads = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ComponentPayload)
            .ToArray();
        Assert.Equal(2, componentPayloads.Length);
        Assert.Contains(componentPayloads, material =>
            material.Value.Contains("component branch FS", StringComparison.OrdinalIgnoreCase) &&
            material.Value.Contains("FS2InvalidCueFilter", StringComparison.Ordinal));
        Assert.Contains(componentPayloads, material =>
            material.Value.Contains("component branch WM", StringComparison.OrdinalIgnoreCase) &&
            material.Value.Contains("WM2MentalTransform", StringComparison.Ordinal));

        var evidenceRequirements = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ComponentEvidenceRequirement)
            .ToArray();
        Assert.Equal(2, evidenceRequirements.Length);
        Assert.Contains(evidenceRequirements, material =>
            material.Value.Contains("branch FS", StringComparison.OrdinalIgnoreCase) &&
            material.Value.Contains("sequence accuracy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(evidenceRequirements, material =>
            material.Value.Contains("branch WM", StringComparison.OrdinalIgnoreCase) &&
            material.Value.Contains("reconstruction", StringComparison.OrdinalIgnoreCase));

        var scoringKeys = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.BranchScoringKey)
            .ToArray();
        Assert.Equal(2, scoringKeys.Length);
        Assert.Contains(scoringKeys, material =>
            material.Value.Contains("branch FS", StringComparison.OrdinalIgnoreCase) &&
            material.Value.Contains("cannot be replaced by composite total", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(scoringKeys, material =>
            material.Value.Contains("branch WM", StringComparison.OrdinalIgnoreCase) &&
            material.Value.Contains("cannot be replaced by composite total", StringComparison.OrdinalIgnoreCase));

        var prompt = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.CompositeTaskPrompt);
        Assert.Contains("composite task", prompt.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FS", prompt.Value, StringComparison.Ordinal);
        Assert.Contains("WM", prompt.Value, StringComparison.Ordinal);
        Assert.Contains("branch-specific evidence", prompt.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("strong branch cannot hide", prompt.Value, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "ti-composite-task");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "component-branches" &&
            fact.Value == "FS|WM");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "component-evidence-requirements" &&
            fact.Value.Contains("branch FS", StringComparison.OrdinalIgnoreCase) &&
            fact.Value.Contains("branch WM", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "branch-scoring-keys" &&
            fact.Value.Contains("branch FS", StringComparison.OrdinalIgnoreCase) &&
            fact.Value.Contains("branch WM", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "anti-collapse-policy" &&
            fact.Value.Contains("strong branch cannot hide", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CompositeTaskValidationRejectsMissingEvidenceOrScoringForAComponentBranch()
    {
        var request = CreateCompositeTaskRequest();
        var result = CreateResult(request);
        var materials = MaterialsMissingWeakComponentEvidence();

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingComponentEvidenceRequirement &&
            failure.MaterialKind == GeneratedContentMaterialKind.ComponentEvidenceRequirement &&
            failure.Detail.Contains("WM", StringComparison.Ordinal));
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingComponentScoringKey &&
            failure.MaterialKind == GeneratedContentMaterialKind.BranchScoringKey &&
            failure.Detail.Contains("WM", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() =>
            ValidatedGeneratedDrillContent.Create(result, materials));
    }

    [Fact]
    public void GlobalReviewTaskGenerationPreservesCompositeAuditAndDelayedReconstructionChannels()
    {
        var request = CreateGlobalReviewTaskRequest();

        var generated = TransferIntegrationGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("ti-global-alpha"));

        var materialValidation = GeneratedContentMaterialValidator.Validate(
            generated.Result,
            generated.Materials);
        var loadValidation = GeneratedContentLoadConstraintValidator.Validate(
            generated.Result,
            generated.Materials);

        Assert.True(materialValidation.IsValid, string.Join("; ", materialValidation.Failures.Select(failure => failure.Detail)));
        Assert.True(loadValidation.IsValid, string.Join("; ", loadValidation.Failures.Select(failure => failure.Detail)));
        Assert.True(generated.ValidatedContent.CanBeConsumedByRuntime);
        Assert.False(generated.GrantsAdvancement);
        Assert.Equal(BranchCode.TI, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L5, generated.Result.Level);
        Assert.Equal(DrillId.TI2GlobalReviewTask, generated.Result.Drill);
        Assert.Equal(PromptContentKind.EquivalentPrompt, generated.Result.ContentKind);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, AuditAndDelayedReconstructionConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TaskLength, "20 minutes");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.PressureSource, "visible review pressure");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.RuleAmbiguity, "moderate ambiguity");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.DelayLength, "5 minutes");

        var prompt = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.CompositeTaskPrompt);
        Assert.Contains("global review task", prompt.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("composite output", prompt.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("audit", prompt.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("delayed reconstruction", prompt.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("branch-specific scoring", prompt.Value, StringComparison.OrdinalIgnoreCase);

        var audit = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.AuditPayload);
        Assert.Contains("audit required", audit.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("original output", audit.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("critical errors", audit.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("component branch", audit.Value, StringComparison.OrdinalIgnoreCase);

        var delayedReconstruction = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.DelayedReconstructionPayload);
        Assert.Contains("delayed reconstruction required", delayedReconstruction.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("5 minutes", delayedReconstruction.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("critical information", delayedReconstruction.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("memory gap", delayedReconstruction.Value, StringComparison.OrdinalIgnoreCase);

        var evidenceRequirements = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ComponentEvidenceRequirement)
            .ToArray();
        var scoringKeys = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.BranchScoringKey)
            .ToArray();
        Assert.Equal(4, evidenceRequirements.Length);
        Assert.Equal(4, scoringKeys.Length);
        AssertContainsBranchEvidence(evidenceRequirements, BranchCode.FH);
        AssertContainsBranchEvidence(evidenceRequirements, BranchCode.WM);
        AssertContainsBranchEvidence(evidenceRequirements, BranchCode.DE);
        AssertContainsBranchEvidence(evidenceRequirements, BranchCode.AI);
        AssertContainsBranchScoring(scoringKeys, BranchCode.FH);
        AssertContainsBranchScoring(scoringKeys, BranchCode.WM);
        AssertContainsBranchScoring(scoringKeys, BranchCode.DE);
        AssertContainsBranchScoring(scoringKeys, BranchCode.AI);

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "ti-global-review-task");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "required-evidence-channels" &&
            fact.Value.Contains("composite output", StringComparison.OrdinalIgnoreCase) &&
            fact.Value.Contains("audit", StringComparison.OrdinalIgnoreCase) &&
            fact.Value.Contains("delayed reconstruction", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "branch-scoring-keys" &&
            fact.Value.Contains("branch FH", StringComparison.OrdinalIgnoreCase) &&
            fact.Value.Contains("branch AI", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "all-channels-required-policy" &&
            fact.Value.Contains("composite output, audit, and delayed reconstruction all matter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GlobalReviewTaskValidationRejectsMissingAuditOrDelayedReconstructionEvidence()
    {
        var request = CreateGlobalReviewTaskRequest();
        var result = CreateResult(request);
        var materials = MaterialsMissingGlobalReviewRequiredChannels();

        var validation = GeneratedContentMaterialValidator.Validate(result, materials);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingRequiredMaterial &&
            failure.MaterialKind == GeneratedContentMaterialKind.AuditPayload);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingRequiredMaterial &&
            failure.MaterialKind == GeneratedContentMaterialKind.DelayedReconstructionPayload);
        Assert.Throws<InvalidOperationException>(() =>
            ValidatedGeneratedDrillContent.Create(result, materials));
    }

    private const string SeparateEvidenceConstraint = "Each branch must leave separate evidence.";
    private const string AuditAndDelayedReconstructionConstraint = "Audit and delayed reconstruction are required.";

    private static GeneratedDrillContentRequest CreateCompositeTaskRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.TI,
            GlobalLevelId.L1,
            DrillId.TI1CompositeTask,
            SessionType.Practice,
            PromptContentKind.EquivalentPrompt,
            "ti-l1-composite-fs-wm",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("number of branches", "2"),
                new LoadVariable("task length", "12 minutes"),
                new LoadVariable("transfer distance", "near transfer"),
            ],
            [new CriticalConstraint(SeparateEvidenceConstraint)],
            previouslyUsedContentIds);
    }

    private static GeneratedDrillContentRequest CreateGlobalReviewTaskRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.TI,
            GlobalLevelId.L5,
            DrillId.TI2GlobalReviewTask,
            SessionType.Test,
            PromptContentKind.EquivalentPrompt,
            "ti-l5-global-review-fh-wm-de-ai",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("task length", "20 minutes"),
                new LoadVariable("pressure", "visible review pressure"),
                new LoadVariable("ambiguity", "moderate ambiguity"),
                new LoadVariable("delay", "5 minutes"),
            ],
            [new CriticalConstraint(AuditAndDelayedReconstructionConstraint)],
            previouslyUsedContentIds);
    }

    private static GeneratedDrillContentResult CreateResult(GeneratedDrillContentRequest request)
    {
        return new GeneratedDrillContentResult(
            request,
            new GeneratedDrillInstanceDescriptor(
                "generated-" + request.EquivalenceClass,
                new PromptContentIdentity(
                    "content-" + request.EquivalenceClass,
                    request.Branch,
                    request.Level,
                    request.Drill,
                    request.ContentKind,
                    request.EquivalenceClass),
                "content-v1",
                request.FreshnessPolicy,
                request.LoadVariables,
                request.CriticalConstraints),
            [new GeneratedContentPayloadFact("fixture-id", request.EquivalenceClass)]);
    }

    private static IReadOnlyList<GeneratedContentMaterial> MaterialsMissingWeakComponentEvidence()
    {
        return
        [
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "number of branches", "2"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "task length", "12 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "transfer distance", "near transfer"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.HonestyConstraint, "separate-evidence", SeparateEvidenceConstraint),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TaskLength, "task-length", "12 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TransferDistance, "transfer-distance", "near transfer"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.CompositeTaskPrompt,
                "composite-task-prompt",
                "Composite task combines FS and WM with branch-specific evidence."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentPayload,
                "component-fs",
                "component branch FS: source standard and component boundary are visible."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentPayload,
                "component-wm",
                "component branch WM: source standard and component boundary are visible."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                "component-evidence-fs",
                "branch FS required evidence: sequence accuracy and invalid cue inhibition."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.BranchScoringKey,
                "branch-scoring-fs",
                "branch FS scoring key cannot be replaced by composite total."),
        ];
    }

    private static IReadOnlyList<GeneratedContentMaterial> MaterialsMissingGlobalReviewRequiredChannels()
    {
        return
        [
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "task length", "20 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "pressure", "visible review pressure"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "ambiguity", "moderate ambiguity"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "delay", "5 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.HonestyConstraint, "audit-delay-required", AuditAndDelayedReconstructionConstraint),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TaskLength, "task-length", "20 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.PressureSource, "pressure-source", "visible review pressure"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.RuleAmbiguity, "ambiguity", "moderate ambiguity"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DelayLength, "review-delay", "5 minutes"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.CompositeTaskPrompt,
                "global-review-prompt",
                "Global review task requires composite output and branch-specific scoring."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                "component-evidence-fh",
                "branch FH required evidence: target preservation."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                "component-evidence-wm",
                "branch WM required evidence: delayed reconstruction."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.BranchScoringKey,
                "branch-scoring-fh",
                "branch FH scoring key cannot be replaced by composite total."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.BranchScoringKey,
                "branch-scoring-wm",
                "branch WM scoring key cannot be replaced by composite total."),
        ];
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

    private static void AssertContainsBranchEvidence(
        IEnumerable<GeneratedContentMaterial> materials,
        BranchCode branch)
    {
        Assert.Contains(materials, material =>
            material.Value.Contains($"branch {branch}", StringComparison.OrdinalIgnoreCase) &&
            material.Value.Contains("required evidence", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertContainsBranchScoring(
        IEnumerable<GeneratedContentMaterial> materials,
        BranchCode branch)
    {
        Assert.Contains(materials, material =>
            material.Value.Contains($"branch {branch}", StringComparison.OrdinalIgnoreCase) &&
            material.Value.Contains("cannot be replaced by composite total", StringComparison.OrdinalIgnoreCase));
    }
}
