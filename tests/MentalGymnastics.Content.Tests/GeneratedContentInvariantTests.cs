using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentInvariantTests
{
    [Fact]
    public void CoordinatorFreshEquivalentRetestSkipsUsedContentAndPreservesDemandIdentity()
    {
        var seed = new GeneratedContentSeed("invariant-wm-retest");
        var first = GeneratedContentSelectionCoordinator.Select(
            GeneratedContentSelectionNeed.ForStandardContent(CreateDelayedReconstructionRequest()),
            seed);
        var retest = GeneratedContentSelectionCoordinator.Select(
            GeneratedContentSelectionNeed.ForStandardContent(
                CreateDelayedReconstructionRequest(previouslyUsedContentIds: [first.Result.ContentId])),
            seed);

        Assert.True(first.IsValid, FailureSummary(first));
        Assert.True(retest.IsValid, FailureSummary(retest));
        Assert.True(retest.FreshnessValidation.RequiresFreshContent);
        Assert.True(retest.FreshnessValidation.IsFreshContent);
        Assert.True(retest.FreshnessValidation.CanUseContent);
        Assert.Equal("0", PayloadFact(first.Result, "variant-index"));
        Assert.Equal("1", PayloadFact(retest.Result, "variant-index"));
        Assert.NotEqual(first.Result.ContentId, retest.Result.ContentId);
        Assert.NotEqual(first.Result.InstanceId, retest.Result.InstanceId);
        Assert.NotEqual(
            MaterialValues(first.Materials, GeneratedContentMaterialKind.EncodeItem),
            MaterialValues(retest.Materials, GeneratedContentMaterialKind.EncodeItem));
        Assert.Equal(first.Result.Branch, retest.Result.Branch);
        Assert.Equal(first.Result.Level, retest.Result.Level);
        Assert.Equal(first.Result.Drill, retest.Result.Drill);
        Assert.Equal(first.Result.ContentKind, retest.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, retest.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, retest.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, retest.Result.Request.CriticalConstraints);
        Assert.Equal(first.Result.ContentVersion, retest.Result.ContentVersion);
    }

    [Fact]
    public void GeneratedIdentityChangesWhenCriticalConstraintsChange()
    {
        var seed = new GeneratedContentSeed("invariant-constraint-identity");
        var baseline = GeneratedContentSeedDeriver.Derive(
            CreateDelayedReconstructionRequest(),
            seed);
        var changedConstraint = GeneratedContentSeedDeriver.Derive(
            CreateDelayedReconstructionRequest(
                criticalConstraints:
                [
                    new CriticalConstraint(NoRereadConstraint),
                    new CriticalConstraint(NoInventedItemsConstraint),
                    new CriticalConstraint("Comparison evidence must remain auditable."),
                ]),
            seed);

        Assert.NotEqual(baseline.RequestFingerprint, changedConstraint.RequestFingerprint);
        Assert.NotEqual(baseline.Instance.ContentIdentity.ContentId, changedConstraint.Instance.ContentIdentity.ContentId);
        Assert.NotEqual(baseline.Instance.InstanceId, changedConstraint.Instance.InstanceId);
        Assert.Equal(baseline.Instance.Branch, changedConstraint.Instance.Branch);
        Assert.Equal(baseline.Instance.Level, changedConstraint.Instance.Level);
        Assert.Equal(baseline.Instance.Drill, changedConstraint.Instance.Drill);
        Assert.Equal(baseline.Instance.ContentKind, changedConstraint.Instance.ContentKind);
        Assert.Equal(baseline.Instance.EquivalenceClass, changedConstraint.Instance.EquivalenceClass);
        Assert.Equal(baseline.Instance.LoadVariables, changedConstraint.Instance.LoadVariables);
        Assert.NotEqual(baseline.Instance.CriticalConstraints, changedConstraint.Instance.CriticalConstraints);
    }

    [Fact]
    public void RuntimeAndPersistenceHandoffsRejectStandardContentThatAllowsRereading()
    {
        var generated = WorkingMemoryGeneratedContentGenerator.Generate(
            CreateDelayedReconstructionRequest(),
            new GeneratedContentSeed("invariant-reread-rejection"));
        var tamperedMaterials = generated.Materials
            .Select(material => material.Kind == GeneratedContentMaterialKind.EncodeInstruction
                ? new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.EncodeInstruction,
                    material.Name,
                    "Rereading is allowed after the encode window if uncertain.")
                : material)
            .ToArray();

        var runtimeFailure = Assert.Throws<InvalidOperationException>(() =>
            GeneratedContentRuntimePackager.Package(
                generated.Result,
                tamperedMaterials,
                StandardFor(generated.Result)));
        var persistenceFailure = Assert.Throws<InvalidOperationException>(() =>
            GeneratedContentPersistenceHandoffMapper.Create(
                generated.Result,
                tamperedMaterials,
                TrainingDate.From(2026, 7, 4)));

        Assert.Contains("anti-self-deception", runtimeFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rereading", runtimeFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("anti-self-deception", persistenceFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rereading", persistenceFailure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TransferHandoffsRejectHiddenSourceStandardMaterial()
    {
        var generated = TransferGeneratedContentGenerator.Generate(
            CreateTransferRequest(),
            new GeneratedContentSeed("invariant-transfer-standard"));
        var tamperedMaterials = generated.Materials
            .Select(material => material.Kind == GeneratedContentMaterialKind.SourceBranchStandard
                ? new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.SourceBranchStandard,
                    material.Name,
                    "source branch standard is hidden from the transfer artifact.")
                : material)
            .ToArray();

        var runtimeFailure = Assert.Throws<InvalidOperationException>(() =>
            GeneratedContentRuntimePackager.Package(
                generated.Result,
                tamperedMaterials,
                StandardFor(generated.Result)));
        var persistenceFailure = Assert.Throws<InvalidOperationException>(() =>
            GeneratedContentPersistenceHandoffMapper.Create(
                generated.Result,
                tamperedMaterials,
                TrainingDate.From(2026, 7, 4)));

        Assert.Contains("source branch standard", runtimeFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("visible", runtimeFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source branch standard", persistenceFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("visible", persistenceFailure.Message, StringComparison.OrdinalIgnoreCase);
    }

    private const string NoRereadConstraint = "No rereading after encode window.";
    private const string NoInventedItemsConstraint = "No invented items.";

    private static GeneratedDrillContentRequest CreateDelayedReconstructionRequest(
        IEnumerable<CriticalConstraint>? criticalConstraints = null,
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
            criticalConstraints ??
            [
                new CriticalConstraint(NoRereadConstraint),
                new CriticalConstraint(NoInventedItemsConstraint),
            ],
            previouslyUsedContentIds);
    }

    private static TransferContentGenerationRequest CreateTransferRequest()
    {
        return new TransferContentGenerationRequest(
            new GeneratedDrillContentRequest(
                BranchCode.WM,
                GlobalLevelId.L4,
                DrillId.WM1DelayedReconstruction,
                SessionType.Transfer,
                PromptContentKind.EquivalentPrompt,
                "wm-l4-transfer-unfamiliar-structure",
                PromptFreshnessPolicy.FreshEquivalentRequired,
                [new LoadVariable("transfer distance", "far transfer to unfamiliar visual structure")],
                [
                    new CriticalConstraint(NoRereadConstraint),
                    new CriticalConstraint(NoInventedItemsConstraint),
                ]),
            CapacityId.EncodingFidelity,
            "far transfer to unfamiliar visual structure");
    }

    private static BranchLevelStandard StandardFor(GeneratedDrillContentResult result)
    {
        return ProgramCatalog.Standards.Single(standard =>
            standard.Branch == result.Branch &&
            standard.Level == result.Level);
    }

    private static string PayloadFact(GeneratedDrillContentResult result, string name)
    {
        return result.PayloadFacts.Single(fact => fact.Name == name).Value;
    }

    private static IReadOnlyList<string> MaterialValues(
        IEnumerable<GeneratedContentMaterial> materials,
        GeneratedContentMaterialKind kind)
    {
        return materials
            .Where(material => material.Kind == kind)
            .OrderBy(material => material.Name, StringComparer.Ordinal)
            .Select(material => material.Value)
            .ToArray();
    }

    private static string FailureSummary(GeneratedContentSelectionResult selection)
    {
        return string.Join(
            "; ",
            selection.FreshnessValidation.Failures.Select(failure => failure.Detail)
                .Concat(selection.DifficultyAudit?.Failures.Select(failure => failure.Detail) ?? [])
                .Concat(selection.TransferValidation?.Failures.Select(failure => failure.Detail) ?? [])
                .Concat(selection.AntiSelfDeceptionGuard.Findings.Select(finding => finding.Detail)));
    }
}
