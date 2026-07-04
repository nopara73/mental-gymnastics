using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentBoundaryTests
{
    [Fact]
    public void BoundaryDeclaresOfflineDeterministicContentWithoutOwningProgressionRuntimeOrStorage()
    {
        var capabilities = GeneratedContentBoundary.Capabilities;

        Assert.True(capabilities.OfflineCapable);
        Assert.True(capabilities.DeterministicWithExplicitInputs);
        Assert.False(capabilities.RequiresAndroidUi);
        Assert.False(capabilities.AllowsAccounts);
        Assert.False(capabilities.AllowsSync);
        Assert.False(capabilities.AllowsBackendServices);
        Assert.False(capabilities.AllowsTelemetry);
        Assert.False(capabilities.AllowsNotifications);
        Assert.False(capabilities.AllowsAiOrApiDependencies);
        Assert.False(capabilities.OwnsProgressionLogic);
        Assert.False(capabilities.OwnsPersistence);
        Assert.False(capabilities.OwnsRuntimeExecution);
        Assert.True(capabilities.ExposesRuntimeConsumableInstances);
        Assert.True(capabilities.ExposesPersistenceReadyInstanceFacts);
    }

    [Fact]
    public void GeneratedInstanceDescriptorUsesCorePromptContentIdentityAndPreservesConstraints()
    {
        var identity = CreateWorkingMemoryIdentity();
        var descriptor = new GeneratedDrillInstanceDescriptor(
            "generated-wm-l1-001",
            identity,
            "content-v1",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")],
            [new CriticalConstraint("No rereading after encode window."), new CriticalConstraint("No invented items.")]);

        Assert.Equal("generated-wm-l1-001", descriptor.InstanceId);
        Assert.Same(identity, descriptor.ContentIdentity);
        Assert.Equal("content-v1", descriptor.ContentVersion);
        Assert.Equal(PromptFreshnessPolicy.FreshEquivalentRequired, descriptor.RetestFreshnessPolicy);
        Assert.Equal(BranchCode.WM, descriptor.Branch);
        Assert.Equal(GlobalLevelId.L1, descriptor.Level);
        Assert.Equal(DrillId.WM1DelayedReconstruction, descriptor.Drill);
        Assert.Equal(PromptContentKind.DelayedReconstructionTask, descriptor.ContentKind);
        Assert.Equal("wm-l1-delayed-reconstruction", descriptor.EquivalenceClass);
        Assert.True(descriptor.CanBeConsumedByRuntime);
        Assert.True(descriptor.CanBeRecordedByPersistence);
        Assert.Equal("item count", descriptor.LoadVariables[0].Name);
        Assert.Equal("No invented items.", descriptor.CriticalConstraints[1].Description);
    }

    [Fact]
    public void GeneratedInstanceDescriptorRejectsMissingEssentials()
    {
        Assert.Throws<ArgumentException>(() => CreateDescriptor(instanceId: " "));
        Assert.Throws<ArgumentException>(() => CreateDescriptor(contentVersion: " "));
        Assert.Throws<ArgumentException>(() => CreateDescriptor(loadVariables: []));
        Assert.Throws<ArgumentException>(() => CreateDescriptor(loadVariables: [new LoadVariable("", "5")]));
        Assert.Throws<ArgumentException>(() => CreateDescriptor(loadVariables: [new LoadVariable("item count", "")]));
        Assert.Throws<ArgumentException>(() => CreateDescriptor(criticalConstraints: []));
        Assert.Throws<ArgumentException>(() => CreateDescriptor(criticalConstraints: [new CriticalConstraint(" ")]));
        Assert.Throws<ArgumentNullException>(() => new GeneratedDrillInstanceDescriptor(
            "generated-wm-l1-001",
            null!,
            "content-v1",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("item count", "5")],
            [new CriticalConstraint("No invented items.")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeneratedDrillInstanceDescriptor(
            "generated-wm-l1-001",
            new PromptContentIdentity(
                "content-invalid",
                (BranchCode)999,
                GlobalLevelId.L1,
                DrillId.WM1DelayedReconstruction,
                PromptContentKind.DelayedReconstructionTask,
                "wm-l1-delayed-reconstruction"),
            "content-v1",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("item count", "5")],
            [new CriticalConstraint("No invented items.")]));
    }

    private static GeneratedDrillInstanceDescriptor CreateDescriptor(
        string instanceId = "generated-wm-l1-001",
        string contentVersion = "content-v1",
        IEnumerable<LoadVariable>? loadVariables = null,
        IEnumerable<CriticalConstraint>? criticalConstraints = null)
    {
        return new GeneratedDrillInstanceDescriptor(
            instanceId,
            CreateWorkingMemoryIdentity(),
            contentVersion,
            PromptFreshnessPolicy.FreshEquivalentRequired,
            loadVariables ?? [new LoadVariable("item count", "5")],
            criticalConstraints ?? [new CriticalConstraint("No invented items.")]);
    }

    private static PromptContentIdentity CreateWorkingMemoryIdentity()
    {
        return new PromptContentIdentity(
            "content-wm-l1-delayed-reconstruction-a",
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction");
    }
}
