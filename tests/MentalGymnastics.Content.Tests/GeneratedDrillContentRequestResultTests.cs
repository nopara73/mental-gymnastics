using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedDrillContentRequestResultTests
{
    [Fact]
    public void GeneratedDrillContentRequestRepresentsCoreDemandAndFreshnessRequirements()
    {
        var request = new GeneratedDrillContentRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            SessionType.Test,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")],
            [new CriticalConstraint("No rereading after encode window."), new CriticalConstraint("No invented items.")],
            ["content-wm-l1-a", " ", "content-wm-l1-a", "content-wm-l1-b"]);

        Assert.Equal(BranchCode.WM, request.Branch);
        Assert.Equal(GlobalLevelId.L1, request.Level);
        Assert.Equal(DrillId.WM1DelayedReconstruction, request.Drill);
        Assert.Equal(SessionType.Test, request.SessionType);
        Assert.Equal(PromptContentKind.DelayedReconstructionTask, request.ContentKind);
        Assert.Equal("wm-l1-delayed-reconstruction", request.EquivalenceClass);
        Assert.Equal(PromptFreshnessPolicy.FreshEquivalentRequired, request.FreshnessPolicy);
        Assert.Equal("item count", request.LoadVariables[0].Name);
        Assert.Equal("No invented items.", request.CriticalConstraints[1].Description);
        Assert.Equal(["content-wm-l1-a", "content-wm-l1-b"], request.PreviouslyUsedContentIds);
    }

    [Fact]
    public void GeneratedDrillContentRequestRejectsMissingEssentials()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(branch: (BranchCode)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(level: (GlobalLevelId)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(drill: (DrillId)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(sessionType: (SessionType)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(contentKind: (PromptContentKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(freshnessPolicy: (PromptFreshnessPolicy)999));
        Assert.Throws<ArgumentException>(() => CreateRequest(equivalenceClass: " "));
        Assert.Throws<ArgumentException>(() => CreateRequest(loadVariables: []));
        Assert.Throws<ArgumentException>(() => CreateRequest(loadVariables: [new LoadVariable("", "5")]));
        Assert.Throws<ArgumentException>(() => CreateRequest(loadVariables: [new LoadVariable("item count", "")]));
        Assert.Throws<ArgumentException>(() => CreateRequest(criticalConstraints: []));
        Assert.Throws<ArgumentException>(() => CreateRequest(criticalConstraints: [new CriticalConstraint(" ")]));
    }

    [Fact]
    public void GeneratedResultPreservesRequestDemandAndCarriesRuntimeAndPersistenceReadyFacts()
    {
        var request = CreateRequest();
        var descriptor = CreateDescriptor(request);
        var result = new GeneratedDrillContentResult(
            request,
            descriptor,
            [
                new GeneratedContentPayloadFact("fixture-id", "wm-l1-delayed-reconstruction-a"),
                new GeneratedContentPayloadFact("item-count", "5"),
            ]);

        Assert.Same(request, result.Request);
        Assert.Same(descriptor, result.Instance);
        Assert.Equal("generated-wm-l1-001", result.InstanceId);
        Assert.Equal("content-wm-l1-delayed-reconstruction-c", result.ContentId);
        Assert.Equal("content-v1", result.ContentVersion);
        Assert.Equal(BranchCode.WM, result.Branch);
        Assert.Equal(GlobalLevelId.L1, result.Level);
        Assert.Equal(DrillId.WM1DelayedReconstruction, result.Drill);
        Assert.Equal(SessionType.Test, result.SessionType);
        Assert.Equal(PromptContentKind.DelayedReconstructionTask, result.ContentKind);
        Assert.Equal("wm-l1-delayed-reconstruction", result.EquivalenceClass);
        Assert.True(result.CanBeConsumedByRuntime);
        Assert.True(result.CanBeRecordedByPersistence);
        Assert.Equal("fixture-id", result.PayloadFacts[0].Name);
        Assert.Equal("wm-l1-delayed-reconstruction-a", result.PayloadFacts[0].Value);
    }

    [Fact]
    public void GeneratedInstanceIdentityMetadataIncludesStableVersionAndLoadContext()
    {
        var request = CreateRequest();
        var descriptor = CreateDescriptor(request);

        var metadata = descriptor.IdentityMetadata;

        Assert.Equal("generated-wm-l1-001", metadata.InstanceId);
        Assert.Equal("content-wm-l1-delayed-reconstruction-c", metadata.ContentId);
        Assert.Equal("content-v1", metadata.ContentVersion);
        Assert.Equal(BranchCode.WM, metadata.Branch);
        Assert.Equal(GlobalLevelId.L1, metadata.Level);
        Assert.Equal(DrillId.WM1DelayedReconstruction, metadata.Drill);
        Assert.Equal(PromptContentKind.DelayedReconstructionTask, metadata.ContentKind);
        Assert.Equal("wm-l1-delayed-reconstruction", metadata.EquivalenceClass);
        Assert.Equal(PromptFreshnessPolicy.FreshEquivalentRequired, metadata.RetestFreshnessPolicy);
        Assert.Matches("^[0-9a-f]{24}$", metadata.LoadContextFingerprint);
        Assert.DoesNotContain("item count", metadata.LoadContextFingerprint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("delay", metadata.LoadContextFingerprint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadContextFingerprintIsStableAndDistinguishesDemandChanges()
    {
        var firstLoad = new[]
        {
            new LoadVariable("item count", "5"),
            new LoadVariable("delay", "60 seconds"),
        };
        var sameLoadDifferentOrder = new[]
        {
            new LoadVariable("delay", "60 seconds"),
            new LoadVariable("item count", "5"),
        };
        var changedLoad = new[]
        {
            new LoadVariable("item count", "7"),
            new LoadVariable("delay", "60 seconds"),
        };

        var first = CreateDescriptor(CreateRequest(loadVariables: firstLoad), loadVariables: firstLoad);
        var sameContext = CreateDescriptor(
            CreateRequest(loadVariables: sameLoadDifferentOrder),
            loadVariables: sameLoadDifferentOrder);
        var changedContext = CreateDescriptor(
            CreateRequest(loadVariables: changedLoad),
            loadVariables: changedLoad);

        Assert.Equal(
            first.IdentityMetadata.LoadContextFingerprint,
            sameContext.IdentityMetadata.LoadContextFingerprint);
        Assert.NotEqual(
            first.IdentityMetadata.LoadContextFingerprint,
            changedContext.IdentityMetadata.LoadContextFingerprint);
    }

    [Fact]
    public void GeneratedResultRoundTripsIdentityAndVersionMetadata()
    {
        var request = CreateRequest();
        var descriptor = CreateDescriptor(request);

        var result = new GeneratedDrillContentResult(
            request,
            descriptor,
            [
                new GeneratedContentPayloadFact("fixture-id", "wm-l1-delayed-reconstruction-a"),
                new GeneratedContentPayloadFact("item-count", "5"),
            ]);

        Assert.Equal(descriptor.IdentityMetadata.InstanceId, result.IdentityMetadata.InstanceId);
        Assert.Equal(descriptor.IdentityMetadata.ContentId, result.IdentityMetadata.ContentId);
        Assert.Equal(descriptor.IdentityMetadata.ContentVersion, result.IdentityMetadata.ContentVersion);
        Assert.Equal(descriptor.IdentityMetadata.Branch, result.IdentityMetadata.Branch);
        Assert.Equal(descriptor.IdentityMetadata.Level, result.IdentityMetadata.Level);
        Assert.Equal(descriptor.IdentityMetadata.Drill, result.IdentityMetadata.Drill);
        Assert.Equal(descriptor.IdentityMetadata.ContentKind, result.IdentityMetadata.ContentKind);
        Assert.Equal(descriptor.IdentityMetadata.EquivalenceClass, result.IdentityMetadata.EquivalenceClass);
        Assert.Equal(
            descriptor.IdentityMetadata.LoadContextFingerprint,
            result.IdentityMetadata.LoadContextFingerprint);
    }

    [Fact]
    public void GeneratedInstanceIdentityMetadataRejectsMissingOrUnknownStableFields()
    {
        Assert.Throws<ArgumentException>(() => CreateIdentityMetadata(instanceId: " "));
        Assert.Throws<ArgumentException>(() => CreateIdentityMetadata(contentId: " "));
        Assert.Throws<ArgumentException>(() => CreateIdentityMetadata(contentVersion: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateIdentityMetadata(branch: (BranchCode)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateIdentityMetadata(level: (GlobalLevelId)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateIdentityMetadata(drill: (DrillId)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateIdentityMetadata(contentKind: (PromptContentKind)999));
        Assert.Throws<ArgumentException>(() => CreateIdentityMetadata(equivalenceClass: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateIdentityMetadata(
            retestFreshnessPolicy: (PromptFreshnessPolicy)999));
        Assert.Throws<ArgumentException>(() => CreateIdentityMetadata(loadContextFingerprint: " "));
    }

    [Fact]
    public void GeneratedResultRejectsMissingOrMismatchedEssentials()
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentNullException>(() => new GeneratedDrillContentResult(
            null!,
            CreateDescriptor(request),
            [new GeneratedContentPayloadFact("fixture-id", "wm-l1-delayed-reconstruction-a")]));
        Assert.Throws<ArgumentNullException>(() => new GeneratedDrillContentResult(
            request,
            null!,
            [new GeneratedContentPayloadFact("fixture-id", "wm-l1-delayed-reconstruction-a")]));
        Assert.Throws<ArgumentException>(() => new GeneratedDrillContentResult(
            request,
            CreateDescriptor(request),
            []));
        Assert.Throws<ArgumentException>(() => new GeneratedDrillContentResult(
            request,
            CreateDescriptor(request),
            [new GeneratedContentPayloadFact(" ", "wm-l1-delayed-reconstruction-a")]));
        Assert.Throws<ArgumentException>(() => new GeneratedDrillContentResult(
            request,
            CreateDescriptor(request, branch: BranchCode.FH),
            [new GeneratedContentPayloadFact("fixture-id", "wm-l1-delayed-reconstruction-a")]));
        Assert.Throws<ArgumentException>(() => new GeneratedDrillContentResult(
            request,
            CreateDescriptor(request, loadVariables: [new LoadVariable("item count", "7")]),
            [new GeneratedContentPayloadFact("fixture-id", "wm-l1-delayed-reconstruction-a")]));
        Assert.Throws<ArgumentException>(() => new GeneratedDrillContentResult(
            request,
            CreateDescriptor(request, criticalConstraints: [new CriticalConstraint("No rereading after encode window.")]),
            [new GeneratedContentPayloadFact("fixture-id", "wm-l1-delayed-reconstruction-a")]));
    }

    private static GeneratedDrillContentRequest CreateRequest(
        BranchCode branch = BranchCode.WM,
        GlobalLevelId level = GlobalLevelId.L1,
        DrillId drill = DrillId.WM1DelayedReconstruction,
        SessionType sessionType = SessionType.Test,
        PromptContentKind contentKind = PromptContentKind.DelayedReconstructionTask,
        string equivalenceClass = "wm-l1-delayed-reconstruction",
        PromptFreshnessPolicy freshnessPolicy = PromptFreshnessPolicy.FreshEquivalentRequired,
        IEnumerable<LoadVariable>? loadVariables = null,
        IEnumerable<CriticalConstraint>? criticalConstraints = null)
    {
        return new GeneratedDrillContentRequest(
            branch,
            level,
            drill,
            sessionType,
            contentKind,
            equivalenceClass,
            freshnessPolicy,
            loadVariables ?? [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")],
            criticalConstraints ?? [new CriticalConstraint("No rereading after encode window."), new CriticalConstraint("No invented items.")],
            ["content-wm-l1-a"]);
    }

    private static GeneratedDrillInstanceIdentityMetadata CreateIdentityMetadata(
        string instanceId = "generated-wm-l1-001",
        string contentId = "content-wm-l1-delayed-reconstruction-c",
        string contentVersion = "content-v1",
        BranchCode branch = BranchCode.WM,
        GlobalLevelId level = GlobalLevelId.L1,
        DrillId drill = DrillId.WM1DelayedReconstruction,
        PromptContentKind contentKind = PromptContentKind.DelayedReconstructionTask,
        string equivalenceClass = "wm-l1-delayed-reconstruction",
        PromptFreshnessPolicy retestFreshnessPolicy = PromptFreshnessPolicy.FreshEquivalentRequired,
        string loadContextFingerprint = "0123456789abcdef01234567")
    {
        return new GeneratedDrillInstanceIdentityMetadata(
            instanceId,
            contentId,
            contentVersion,
            branch,
            level,
            drill,
            contentKind,
            equivalenceClass,
            retestFreshnessPolicy,
            loadContextFingerprint);
    }

    private static GeneratedDrillInstanceDescriptor CreateDescriptor(
        GeneratedDrillContentRequest request,
        BranchCode? branch = null,
        IEnumerable<LoadVariable>? loadVariables = null,
        IEnumerable<CriticalConstraint>? criticalConstraints = null)
    {
        return new GeneratedDrillInstanceDescriptor(
            "generated-wm-l1-001",
            new PromptContentIdentity(
                "content-wm-l1-delayed-reconstruction-c",
                branch ?? request.Branch,
                request.Level,
                request.Drill,
                request.ContentKind,
                request.EquivalenceClass),
            "content-v1",
            request.FreshnessPolicy,
            loadVariables ?? request.LoadVariables,
            criticalConstraints ?? request.CriticalConstraints);
    }
}
