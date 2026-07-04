using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentFreshnessPolicyTests
{
    [Fact]
    public void ReusableContentIsAcceptedWhenIdenticalReuseIsAllowed()
    {
        var request = CreateRequest(
            freshnessPolicy: PromptFreshnessPolicy.IdenticalReuseAllowed,
            previouslyUsedContentIds: ["content-wm-l1-a"]);
        var requirement = CreateRequirement(request);
        var candidate = CreateCandidate(
            request,
            contentId: "content-wm-l1-a",
            visibleStandard: requirement.VisibleStandard,
            loadIntent: requirement.LoadIntent);

        var result = GeneratedContentFreshnessPolicy.Evaluate(requirement, candidate);

        Assert.True(result.CanUseContent);
        Assert.True(result.IsEquivalent);
        Assert.True(result.MeetsFreshnessPolicy);
        Assert.False(result.RequiresFreshContent);
        Assert.False(result.IsFreshContent);
        Assert.False(result.GrantsAdvancement);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void FreshEquivalentContentMustChangeContentIdWhilePreservingDemand()
    {
        var request = CreateRequest(
            freshnessPolicy: PromptFreshnessPolicy.FreshEquivalentRequired,
            previouslyUsedContentIds: ["content-wm-l1-a"]);
        var requirement = CreateRequirement(request);
        var freshCandidate = CreateCandidate(
            request,
            contentId: "content-wm-l1-b",
            visibleStandard: requirement.VisibleStandard,
            loadIntent: requirement.LoadIntent);
        var reusedCandidate = CreateCandidate(
            request,
            contentId: "content-wm-l1-a",
            visibleStandard: requirement.VisibleStandard,
            loadIntent: requirement.LoadIntent);

        var accepted = GeneratedContentFreshnessPolicy.Evaluate(requirement, freshCandidate);
        var rejected = GeneratedContentFreshnessPolicy.Evaluate(requirement, reusedCandidate);

        Assert.True(accepted.CanUseContent);
        Assert.True(accepted.IsEquivalent);
        Assert.True(accepted.MeetsFreshnessPolicy);
        Assert.True(accepted.RequiresFreshContent);
        Assert.True(accepted.IsFreshContent);
        Assert.False(accepted.GrantsAdvancement);

        Assert.False(rejected.CanUseContent);
        Assert.True(rejected.IsEquivalent);
        Assert.False(rejected.MeetsFreshnessPolicy);
        Assert.False(rejected.IsFreshContent);
        Assert.Contains(rejected.Failures, failure =>
            failure.Kind == GeneratedContentPolicyFailureKind.FreshEquivalentContentAlreadyUsed);
        Assert.False(rejected.GrantsAdvancement);
    }

    [Fact]
    public void NonEquivalentReplacementIsRejectedEvenWhenItIsFresh()
    {
        var request = CreateRequest(
            freshnessPolicy: PromptFreshnessPolicy.FreshEquivalentRequired,
            previouslyUsedContentIds: ["content-wm-l1-a"]);
        var requirement = CreateRequirement(request);
        var replacement = CreateCandidate(
            request,
            contentId: "content-wm-l2-fresh-but-wrong-demand",
            level: GlobalLevelId.L2,
            loadVariables: [new LoadVariable("item count", "7"), new LoadVariable("delay", "60 seconds")],
            criticalConstraints: [new CriticalConstraint("No rereading after encode window.")],
            visibleStandard: "A different or hidden standard.",
            loadIntent: "increase item count instead of preserving WM L1 load");

        var result = GeneratedContentFreshnessPolicy.Evaluate(requirement, replacement);

        Assert.False(result.CanUseContent);
        Assert.False(result.IsEquivalent);
        Assert.True(result.IsFreshContent);
        Assert.True(result.MeetsFreshnessPolicy);
        Assert.Contains(result.Failures, failure =>
            failure.Kind == GeneratedContentPolicyFailureKind.BranchDemandChanged);
        Assert.Contains(result.Failures, failure =>
            failure.Kind == GeneratedContentPolicyFailureKind.StandardVisibilityChanged);
        Assert.Contains(result.Failures, failure =>
            failure.Kind == GeneratedContentPolicyFailureKind.LoadIntentChanged);
        Assert.Contains(result.Failures, failure =>
            failure.Kind == GeneratedContentPolicyFailureKind.LoadVariablesChanged);
        Assert.Contains(result.Failures, failure =>
            failure.Kind == GeneratedContentPolicyFailureKind.CriticalConstraintsChanged);
        Assert.False(result.GrantsAdvancement);
    }

    [Fact]
    public void FreshnessPolicyRejectsMissingEssentials()
    {
        var request = CreateRequest();
        var requirement = CreateRequirement(request);
        var candidate = CreateCandidate(
            request,
            contentId: "content-wm-l1-b",
            visibleStandard: requirement.VisibleStandard,
            loadIntent: requirement.LoadIntent);

        Assert.Throws<ArgumentNullException>(() => new GeneratedContentEquivalenceRequirement(
            null!,
            requirement.VisibleStandard,
            requirement.LoadIntent));
        Assert.Throws<ArgumentException>(() => new GeneratedContentEquivalenceRequirement(
            request,
            " ",
            requirement.LoadIntent));
        Assert.Throws<ArgumentException>(() => new GeneratedContentEquivalenceRequirement(
            request,
            requirement.VisibleStandard,
            " "));
        Assert.Throws<ArgumentNullException>(() => new GeneratedContentEquivalenceCandidate(
            null!,
            requirement.VisibleStandard,
            requirement.LoadIntent));
        Assert.Throws<ArgumentException>(() => new GeneratedContentEquivalenceCandidate(
            candidate.Instance,
            " ",
            requirement.LoadIntent));
        Assert.Throws<ArgumentException>(() => new GeneratedContentEquivalenceCandidate(
            candidate.Instance,
            requirement.VisibleStandard,
            " "));
        Assert.Throws<ArgumentNullException>(() => GeneratedContentFreshnessPolicy.Evaluate(null!, candidate));
        Assert.Throws<ArgumentNullException>(() => GeneratedContentFreshnessPolicy.Evaluate(requirement, null!));
    }

    private static GeneratedContentEquivalenceRequirement CreateRequirement(
        GeneratedDrillContentRequest request)
    {
        return new GeneratedContentEquivalenceRequirement(
            request,
            VisibleStandardFor(request.Branch, request.Level),
            "wm-l1-delayed-reconstruction-load");
    }

    private static GeneratedContentEquivalenceCandidate CreateCandidate(
        GeneratedDrillContentRequest request,
        string contentId,
        string visibleStandard,
        string loadIntent,
        GlobalLevelId? level = null,
        IEnumerable<LoadVariable>? loadVariables = null,
        IEnumerable<CriticalConstraint>? criticalConstraints = null)
    {
        return new GeneratedContentEquivalenceCandidate(
            new GeneratedDrillInstanceDescriptor(
                "generated-" + contentId,
                new PromptContentIdentity(
                    contentId,
                    request.Branch,
                    level ?? request.Level,
                    request.Drill,
                    request.ContentKind,
                    request.EquivalenceClass),
                "content-v1",
                request.FreshnessPolicy,
                loadVariables ?? request.LoadVariables,
                criticalConstraints ?? request.CriticalConstraints),
            visibleStandard,
            loadIntent);
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

    private static string VisibleStandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards
            .Single(standard => standard.Branch == branch && standard.Level == level)
            .Standard;
    }
}
