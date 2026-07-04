using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class SessionRuntimeBoundaryTests
{
    [Fact]
    public void BoundaryDeclaresHeadlessOfflineRuntimeWithoutProgressionOrPersistenceOwnership()
    {
        var capabilities = SessionRuntimeBoundary.Capabilities;

        Assert.True(capabilities.OfflineCapable);
        Assert.False(capabilities.RequiresAndroidUi);
        Assert.False(capabilities.AllowsAccounts);
        Assert.False(capabilities.AllowsSync);
        Assert.False(capabilities.AllowsBackendServices);
        Assert.False(capabilities.AllowsTelemetry);
        Assert.False(capabilities.AllowsNotifications);
        Assert.False(capabilities.AllowsAiOrApiDependencies);
        Assert.False(capabilities.OwnsProgressionLogic);
        Assert.False(capabilities.OwnsPersistence);
        Assert.True(capabilities.ExposesPersistenceReadyRecords);
    }

    [Fact]
    public void SessionDefinitionConsumesCoreDomainConceptsWithoutRedefiningProgramVocabulary()
    {
        var generatedInstance = new RuntimeGeneratedDrillInstanceIdentity(
            "generated-fh-l1-001",
            new PromptContentIdentity(
                "content-fh-l1-target-hold-a",
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold,
                PromptContentKind.EquivalentPrompt,
                "fh-l1-target-hold"),
            "v1");

        var definition = new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")],
            CreateFocusHoldStandard(),
            [new CriticalConstraint("Target is stated before set; every drift is marked.")],
            generatedInstance);

        Assert.Equal(BranchCode.FH, definition.Branch);
        Assert.Equal(GlobalLevelId.L1, definition.Level);
        Assert.Equal(SessionType.Practice, definition.SessionType);
        Assert.Equal(DrillId.FH1TargetHold, definition.Drill);
        Assert.Equal("duration", Assert.Single(definition.LoadVariables).Name);
        Assert.Equal("No more than 5 marked drifts; each return within 10 seconds; no target change.", definition.Standard.Standard);
        Assert.Equal("Target is stated before set; every drift is marked.", Assert.Single(definition.CriticalConstraints).Description);
        Assert.Same(generatedInstance, definition.GeneratedDrillInstance);
    }

    [Fact]
    public void SessionDefinitionRejectsMissingEssentials()
    {
        Assert.Throws<ArgumentException>(() => CreateDefinition(loadVariables: []));
        Assert.Throws<ArgumentException>(() => CreateDefinition(loadVariables: [new LoadVariable("", "3 minutes")]));
        Assert.Throws<ArgumentException>(() => CreateDefinition(standard: CreateFocusHoldStandard(standard: " ")));
        Assert.Throws<ArgumentException>(() => CreateDefinition(standard: CreateFocusHoldStandard(branch: BranchCode.WM)));
        Assert.Throws<ArgumentException>(() => CreateDefinition(criticalConstraints: []));
        Assert.Throws<ArgumentException>(() => CreateDefinition(criticalConstraints: [new CriticalConstraint(" ")]));
        Assert.Throws<ArgumentException>(() => CreateDefinition(generatedDrillInstance: new RuntimeGeneratedDrillInstanceIdentity(
            "generated-wm-l1-001",
            new PromptContentIdentity(
                "content-wm-l1-delayed-reconstruction-a",
                BranchCode.WM,
                GlobalLevelId.L1,
                DrillId.WM1DelayedReconstruction,
                PromptContentKind.DelayedReconstructionTask,
                "wm-l1-reconstruction"),
            "v1")));
    }

    private static RuntimeSessionDefinition CreateDefinition(
        IEnumerable<LoadVariable>? loadVariables = null,
        BranchLevelStandard? standard = null,
        IEnumerable<CriticalConstraint>? criticalConstraints = null,
        RuntimeGeneratedDrillInstanceIdentity? generatedDrillInstance = null)
    {
        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            loadVariables ?? [new LoadVariable("duration", "3 minutes")],
            standard ?? CreateFocusHoldStandard(),
            criticalConstraints ?? [new CriticalConstraint("Target is stated before set; every drift is marked.")],
            generatedDrillInstance);
    }

    private static BranchLevelStandard CreateFocusHoldStandard(
        BranchCode branch = BranchCode.FH,
        string standard = "No more than 5 marked drifts; each return within 10 seconds; no target change.")
    {
        return new BranchLevelStandard(
            branch,
            GlobalLevelId.L1,
            "Hold one simple target for 3 minutes.",
            standard,
            "Pass once enters stabilization.",
            "Repeat twice within 14 days; one after a short WM set.",
            "Hold a different target type with same standard.");
    }
}
