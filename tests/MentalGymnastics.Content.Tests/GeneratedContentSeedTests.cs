using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentSeedTests
{
    [Fact]
    public void SameLocalSeedAndRequestProduceRepeatableSeedPlan()
    {
        var request = CreateRequest();

        var first = GeneratedContentSeedDeriver.Derive(
            request,
            new GeneratedContentSeed("local-seed-alpha"));
        var second = GeneratedContentSeedDeriver.Derive(
            CreateRequest(),
            new GeneratedContentSeed("local-seed-alpha"));

        Assert.Equal(first.RequestFingerprint, second.RequestFingerprint);
        Assert.Equal(first.Instance.InstanceId, second.Instance.InstanceId);
        Assert.Equal(first.Instance.ContentIdentity.ContentId, second.Instance.ContentIdentity.ContentId);
        Assert.Equal(first.PayloadSeed, second.PayloadSeed);
        Assert.Equal(0, first.VariantIndex);
        Assert.Equal("deterministic-seed-v3", first.ContentVersion);
        Assert.Equal(BranchCode.WM, first.Instance.Branch);
        Assert.Equal(GlobalLevelId.L1, first.Instance.Level);
        Assert.Equal(DrillId.WM1DelayedReconstruction, first.Instance.Drill);

        var result = first.ToGeneratedResult();

        Assert.Same(request, result.Request);
        Assert.Same(first.Instance, result.Instance);
        Assert.Contains(result.PayloadFacts, fact =>
            fact.Name == "payload-seed" &&
            fact.Value == first.PayloadSeed);
    }

    [Fact]
    public void DifferentLocalSeedChangesDerivedContentIdentityWithoutChangingDemand()
    {
        var request = CreateRequest();

        var first = GeneratedContentSeedDeriver.Derive(request, new GeneratedContentSeed("local-seed-alpha"));
        var second = GeneratedContentSeedDeriver.Derive(request, new GeneratedContentSeed("local-seed-beta"));

        Assert.NotEqual(first.Instance.ContentIdentity.ContentId, second.Instance.ContentIdentity.ContentId);
        Assert.NotEqual(first.Instance.InstanceId, second.Instance.InstanceId);
        Assert.Equal(first.RequestFingerprint, second.RequestFingerprint);
        Assert.Equal(first.Instance.Branch, second.Instance.Branch);
        Assert.Equal(first.Instance.Level, second.Instance.Level);
        Assert.Equal(first.Instance.Drill, second.Instance.Drill);
        Assert.Equal(first.Instance.ContentKind, second.Instance.ContentKind);
        Assert.Equal(first.Instance.EquivalenceClass, second.Instance.EquivalenceClass);
        Assert.Equal(first.Instance.LoadVariables, second.Instance.LoadVariables);
        Assert.Equal(first.Instance.CriticalConstraints, second.Instance.CriticalConstraints);
    }

    [Fact]
    public void FreshEquivalentRequirementSkipsPreviouslyUsedContentIds()
    {
        var seed = new GeneratedContentSeed("local-seed-alpha");
        var first = GeneratedContentSeedDeriver.Derive(CreateRequest(), seed);
        var freshRequest = CreateRequest(previouslyUsedContentIds: [first.Instance.ContentIdentity.ContentId]);

        var freshVariant = GeneratedContentSeedDeriver.Derive(freshRequest, seed);

        Assert.Equal(1, freshVariant.VariantIndex);
        Assert.NotEqual(first.Instance.ContentIdentity.ContentId, freshVariant.Instance.ContentIdentity.ContentId);
        Assert.NotEqual(first.PayloadSeed, freshVariant.PayloadSeed);
        Assert.Equal(first.RequestFingerprint, freshVariant.RequestFingerprint);
        Assert.Equal(first.Instance.Branch, freshVariant.Instance.Branch);
        Assert.Equal(first.Instance.Level, freshVariant.Instance.Level);
        Assert.Equal(first.Instance.Drill, freshVariant.Instance.Drill);
        Assert.Equal(first.Instance.ContentKind, freshVariant.Instance.ContentKind);
        Assert.Equal(first.Instance.EquivalenceClass, freshVariant.Instance.EquivalenceClass);
        Assert.Equal(first.Instance.LoadVariables, freshVariant.Instance.LoadVariables);
        Assert.Equal(first.Instance.CriticalConstraints, freshVariant.Instance.CriticalConstraints);
    }

    [Fact]
    public void IdenticalReusePolicyKeepsRepeatableVariantEvenWhenContentWasPreviouslyUsed()
    {
        var seed = new GeneratedContentSeed("local-seed-alpha");
        var first = GeneratedContentSeedDeriver.Derive(
            CreateRequest(freshnessPolicy: PromptFreshnessPolicy.IdenticalReuseAllowed),
            seed);
        var repeatRequest = CreateRequest(
            freshnessPolicy: PromptFreshnessPolicy.IdenticalReuseAllowed,
            previouslyUsedContentIds: [first.Instance.ContentIdentity.ContentId]);

        var repeat = GeneratedContentSeedDeriver.Derive(repeatRequest, seed);

        Assert.Equal(0, repeat.VariantIndex);
        Assert.Equal(first.Instance.ContentIdentity.ContentId, repeat.Instance.ContentIdentity.ContentId);
        Assert.Equal(first.Instance.InstanceId, repeat.Instance.InstanceId);
        Assert.Equal(first.PayloadSeed, repeat.PayloadSeed);
    }

    [Fact]
    public void SeedDerivationRejectsMissingEssentials()
    {
        Assert.Throws<ArgumentException>(() => new GeneratedContentSeed(" "));
        Assert.Throws<ArgumentNullException>(() => GeneratedContentSeedDeriver.Derive(
            null!,
            new GeneratedContentSeed("local-seed-alpha")));
        Assert.Throws<ArgumentNullException>(() => GeneratedContentSeedDeriver.Derive(
            CreateRequest(),
            null!));
    }

    private static GeneratedDrillContentRequest CreateRequest(
        PromptFreshnessPolicy freshnessPolicy = PromptFreshnessPolicy.FreshEquivalentRequired,
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            SessionType.Test,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            freshnessPolicy,
            [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")],
            [new CriticalConstraint("No rereading after encode window."), new CriticalConstraint("No invented items.")],
            previouslyUsedContentIds);
    }
}
