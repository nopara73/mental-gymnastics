using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class TransferGeneratedContentTests
{
    [Fact]
    public void ValidTransferContentPreservesSourceStandardDemandChangedContextAndRetestRules()
    {
        var request = CreateTransferContentRequest();

        var generated = TransferGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("transfer-wm-alpha"));

        Assert.True(generated.TransferValidation.IsValid, FailureSummary(generated.TransferValidation));
        Assert.True(generated.CanBeConsumedByRuntime);
        Assert.True(generated.CanBeRecordedByPersistence);
        Assert.False(generated.GrantsAdvancement);
        Assert.Equal(BranchCode.WM, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L4, generated.Result.Level);
        Assert.Equal(SessionType.Transfer, generated.Result.SessionType);
        Assert.Equal(CapacityId.EncodingFidelity, generated.TransferEligibilityRequest.TrainedCapacity);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TransferTask, "Reconstruct structure from unfamiliar content.");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.SameDemand, "Encoding, delay, no invention.");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.ChangedContext, "Domain or representation.");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.TransferDistance, "far transfer to unfamiliar visual structure");
        Assert.Contains(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.RetestRequirement &&
            material.Value.Contains("2 fresh equivalent transfer contexts", StringComparison.OrdinalIgnoreCase));

        var sourceStandard = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.SourceBranchStandard);
        Assert.Contains("source branch standard", sourceStandard.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("branch WM", sourceStandard.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("level L4", sourceStandard.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reconstruct structure, not surface wording", sourceStandard.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("visible in the transfer artifact", sourceStandard.Value, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "transfer-content");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "source-standard-visibility" &&
            fact.Value.Contains("visible", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "novelty-policy" &&
            fact.Value.Contains("novelty alone is not transfer", StringComparison.OrdinalIgnoreCase));

        var coreEligibility = TransferEligibilityEvaluator.Evaluate(generated.TransferEligibilityRequest);
        Assert.True(coreEligibility.IsEligible, string.Join("; ", coreEligibility.Failures.Select(failure => failure.Detail)));
    }

    [Fact]
    public void NoveltyOnlyTransferContentIsRejected()
    {
        var candidate = CreateCandidate(
            trainedCapacity: null,
            sameDemand: "Novel visual puzzle format.",
            changedContext: "Domain or representation.",
            sourceStandardEvidence: CreateSourceStandardEvidence(visible: true));

        var validation = TransferContentRuleValidator.Validate(candidate);

        Assert.False(validation.IsValid);
        Assert.False(validation.GrantsAdvancement);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == TransferContentRuleFailureKind.NoveltyOnlyTransferContent);
        Assert.Contains(validation.CoreEligibility.Failures, failure =>
            failure.Kind == TransferEligibilityFailureKind.TrainedCapacityNotSpecified);
        Assert.Contains(validation.CoreEligibility.Failures, failure =>
            failure.Kind == TransferEligibilityFailureKind.SourceDemandNotPreserved);
    }

    [Fact]
    public void TransferContentMissingSourceStandardVisibilityIsRejected()
    {
        var candidate = CreateCandidate(
            trainedCapacity: CapacityId.EncodingFidelity,
            sameDemand: "Encoding, delay, no invention.",
            changedContext: "Domain or representation.",
            sourceStandardEvidence: CreateSourceStandardEvidence(visible: false));

        var validation = TransferContentRuleValidator.Validate(candidate);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == TransferContentRuleFailureKind.MissingSourceStandardVisibility);
        Assert.Contains(validation.CoreEligibility.Failures, failure =>
            failure.Kind == TransferEligibilityFailureKind.SourceStandardNotVisible);
    }

    private static TransferContentGenerationRequest CreateTransferContentRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
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
                    new CriticalConstraint("No rereading after encode window."),
                    new CriticalConstraint("No invented items."),
                ],
                previouslyUsedContentIds),
            CapacityId.EncodingFidelity,
            "far transfer to unfamiliar visual structure");
    }

    private static TransferContentCandidate CreateCandidate(
        CapacityId? trainedCapacity,
        string sameDemand,
        string changedContext,
        TransferSourceStandardEvidence? sourceStandardEvidence)
    {
        return new TransferContentCandidate(
            BranchCode.WM,
            GlobalLevelId.L4,
            "Reconstruct structure from unfamiliar content.",
            trainedCapacity,
            sameDemand,
            changedContext,
            sourceStandardEvidence,
            new TransferRetestPlan(
                requiredTransferContexts: 2,
                usesFreshEquivalentContexts: true),
            "far transfer to unfamiliar visual structure");
    }

    private static TransferSourceStandardEvidence CreateSourceStandardEvidence(bool visible)
    {
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == BranchCode.WM &&
            item.Level == GlobalLevelId.L4);

        return new TransferSourceStandardEvidence(
            BranchCode.WM,
            GlobalLevelId.L4,
            standard.Standard,
            visible);
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

    private static string FailureSummary(TransferContentRuleValidationResult validation)
    {
        return string.Join(
            "; ",
            validation.Failures.Select(failure => failure.Detail)
                .Concat(validation.CoreEligibility.Failures.Select(failure => failure.Detail)));
    }
}
