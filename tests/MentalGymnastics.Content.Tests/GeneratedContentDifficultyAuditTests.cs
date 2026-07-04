using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentDifficultyAuditTests
{
    [Fact]
    public void ValidGeneratedContentPassesDifficultyAuditAgainstRequestedDemand()
    {
        var request = CreateWmRequest();
        var result = CreateResult(request);
        var requirement = CreateEquivalenceRequirement(request);
        var candidate = CreateEquivalenceCandidate(request, requirement);

        var audit = GeneratedContentDifficultyAuditor.Audit(
            result,
            ValidWmMaterials(),
            requirement,
            candidate,
            LoadChangeMode.Acquisition);

        Assert.True(audit.IsValid, FailureSummary(audit));
        Assert.True(audit.PreservesRequestedDemand);
        Assert.True(audit.PreservesHonestyConstraint);
        Assert.True(audit.CanBeConsumedByRuntime);
        Assert.False(audit.GrantsAdvancement);
        Assert.Empty(audit.Failures);
        Assert.Empty(audit.UnrequestedLoadVariables);
        Assert.True(audit.EquivalenceValidation.CanUseContent);
        Assert.True(audit.LoadValidation.IsValid);
        Assert.True(audit.MaterialValidation.IsValid);
    }

    [Fact]
    public void AuditDetectsMultipleUnrequestedPrimaryLoadChangesDuringAcquisition()
    {
        var request = CreateWmRequest(
            loadVariables: [new LoadVariable("item count", "5")]);
        var result = CreateResult(request);
        var requirement = CreateEquivalenceRequirement(request);
        var candidate = CreateEquivalenceCandidate(request, requirement);
        var materials = ValidWmMaterials();

        var audit = GeneratedContentDifficultyAuditor.Audit(
            result,
            materials,
            requirement,
            candidate,
            LoadChangeMode.Acquisition);

        Assert.False(audit.IsValid);
        Assert.Contains(LoadVariableKind.DetailDensity, audit.UnrequestedLoadVariables);
        Assert.Contains(LoadVariableKind.Delay, audit.UnrequestedLoadVariables);
        Assert.Contains(audit.LoadChangeValidation.Failures, failure =>
            failure.Kind == LoadChangeFailureKind.TooManyLoadVariablesForAcquisition);
        Assert.Contains(audit.Failures, failure =>
            failure.Kind == GeneratedContentDifficultyAuditFailureKind.MultiplePrimaryLoadVariablesChangedDuringAcquisition);
        Assert.Contains(audit.Failures, failure =>
            failure.Kind == GeneratedContentDifficultyAuditFailureKind.UnrequestedPrimaryLoadVariable &&
            failure.LoadVariable == LoadVariableKind.DetailDensity);
        Assert.Contains(audit.Failures, failure =>
            failure.Kind == GeneratedContentDifficultyAuditFailureKind.UnrequestedPrimaryLoadVariable &&
            failure.LoadVariable == LoadVariableKind.Delay);
    }

    [Fact]
    public void AuditDetectsUnderSpecifiedContentMissingRequestedLoadAndHonestyConstraint()
    {
        var request = CreateWmRequest();
        var result = CreateResult(request);
        var requirement = CreateEquivalenceRequirement(request);
        var candidate = CreateEquivalenceCandidate(request, requirement);
        var materials = ValidWmMaterials()
            .Where(material =>
                material.Kind != GeneratedContentMaterialKind.DelayLength &&
                !string.Equals(material.Value, NoInventedItemsConstraint, StringComparison.Ordinal))
            .ToArray();

        var audit = GeneratedContentDifficultyAuditor.Audit(
            result,
            materials,
            requirement,
            candidate,
            LoadChangeMode.Acquisition);

        Assert.False(audit.IsValid);
        Assert.False(audit.PreservesRequestedDemand);
        Assert.False(audit.PreservesHonestyConstraint);
        Assert.Contains(audit.Failures, failure =>
            failure.Kind == GeneratedContentDifficultyAuditFailureKind.RequestedLoadNotRepresented &&
            failure.LoadVariable == LoadVariableKind.Delay);
        Assert.Contains(audit.Failures, failure =>
            failure.Kind == GeneratedContentDifficultyAuditFailureKind.HonestyConstraintRemoved);
        Assert.Contains(audit.LoadValidation.Failures, failure =>
            failure.Kind == GeneratedContentLoadConstraintFailureKind.MissingMaterial &&
            failure.LoadVariable == LoadVariableKind.Delay);
        Assert.Contains(audit.LoadValidation.Failures, failure =>
            failure.Kind == GeneratedContentLoadConstraintFailureKind.MissingHonestyConstraintMaterial);
    }

    [Fact]
    public void AuditDetectsWrongDemandEquivalenceCandidate()
    {
        var request = CreateWmRequest();
        var result = CreateResult(request);
        var requirement = CreateEquivalenceRequirement(request);
        var wrongDemandCandidate = CreateEquivalenceCandidate(
            request,
            requirement,
            level: GlobalLevelId.L2,
            visibleStandard: VisibleStandardFor(BranchCode.WM, GlobalLevelId.L2),
            loadIntent: "wm-l2-duration-density");

        var audit = GeneratedContentDifficultyAuditor.Audit(
            result,
            ValidWmMaterials(),
            requirement,
            wrongDemandCandidate,
            LoadChangeMode.Acquisition);

        Assert.False(audit.IsValid);
        Assert.False(audit.PreservesRequestedDemand);
        Assert.Contains(audit.Failures, failure =>
            failure.Kind == GeneratedContentDifficultyAuditFailureKind.EquivalenceConstraintChanged);
        Assert.Contains(audit.Failures, failure =>
            failure.Kind == GeneratedContentDifficultyAuditFailureKind.CoreDemandChanged);
        Assert.Contains(audit.EquivalenceValidation.Failures, failure =>
            failure.Kind == GeneratedContentPolicyFailureKind.BranchDemandChanged);
        Assert.Contains(audit.EquivalenceValidation.Failures, failure =>
            failure.Kind == GeneratedContentPolicyFailureKind.StandardVisibilityChanged);
        Assert.Contains(audit.EquivalenceValidation.Failures, failure =>
            failure.Kind == GeneratedContentPolicyFailureKind.LoadIntentChanged);
    }

    private const string NoRereadConstraint = "No rereading after encode window.";
    private const string NoInventedItemsConstraint = "No invented items.";

    private static GeneratedDrillContentRequest CreateWmRequest(
        IEnumerable<LoadVariable>? loadVariables = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            SessionType.Practice,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            loadVariables ??
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("delay", "60 seconds"),
            ],
            [
                new CriticalConstraint(NoRereadConstraint),
                new CriticalConstraint(NoInventedItemsConstraint),
            ]);
    }

    private static GeneratedDrillContentResult CreateResult(GeneratedDrillContentRequest request)
    {
        return new GeneratedDrillContentResult(
            request,
            CreateDescriptor(request),
            [new GeneratedContentPayloadFact("fixture-id", request.EquivalenceClass)]);
    }

    private static GeneratedDrillInstanceDescriptor CreateDescriptor(
        GeneratedDrillContentRequest request,
        GlobalLevelId? level = null,
        IEnumerable<LoadVariable>? loadVariables = null,
        IEnumerable<CriticalConstraint>? criticalConstraints = null)
    {
        return new GeneratedDrillInstanceDescriptor(
            "generated-" + request.EquivalenceClass + "-" + (level ?? request.Level),
            new PromptContentIdentity(
                "content-" + request.EquivalenceClass + "-" + (level ?? request.Level),
                request.Branch,
                level ?? request.Level,
                request.Drill,
                request.ContentKind,
                request.EquivalenceClass),
            "content-v1",
            request.FreshnessPolicy,
            loadVariables ?? request.LoadVariables,
            criticalConstraints ?? request.CriticalConstraints);
    }

    private static GeneratedContentEquivalenceRequirement CreateEquivalenceRequirement(
        GeneratedDrillContentRequest request)
    {
        return new GeneratedContentEquivalenceRequirement(
            request,
            VisibleStandardFor(request.Branch, request.Level),
            "wm-l1-delayed-reconstruction");
    }

    private static GeneratedContentEquivalenceCandidate CreateEquivalenceCandidate(
        GeneratedDrillContentRequest request,
        GeneratedContentEquivalenceRequirement requirement,
        GlobalLevelId? level = null,
        string? visibleStandard = null,
        string? loadIntent = null)
    {
        return new GeneratedContentEquivalenceCandidate(
            CreateDescriptor(request, level),
            visibleStandard ?? requirement.VisibleStandard,
            loadIntent ?? requirement.LoadIntent);
    }

    private static IReadOnlyList<GeneratedContentMaterial> ValidWmMaterials()
    {
        return
        [
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "item count", "5"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "detail density", "simple objects"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "delay", "60 seconds"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "no-reread",
                NoRereadConstraint),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "no-invention",
                NoInventedItemsConstraint),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeInstruction, "encode-instruction", "Study once."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DetailDensity, "detail-density", "simple objects"),
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

    private static string VisibleStandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards
            .Single(standard => standard.Branch == branch && standard.Level == level)
            .Standard;
    }

    private static string FailureSummary(GeneratedContentDifficultyAuditResult audit)
    {
        return string.Join("; ", audit.Failures.Select(failure => failure.Detail));
    }
}
