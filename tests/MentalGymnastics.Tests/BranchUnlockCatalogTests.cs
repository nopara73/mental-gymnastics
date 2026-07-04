using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class BranchUnlockCatalogTests
{
    [Fact]
    public void ExposesBranchUnlocksInBranchCatalogOrder()
    {
        Assert.Equal(
            ProgramCatalog.Branches.Select(branch => branch.Code),
            ProgramCatalog.BranchUnlocks.Select(unlock => unlock.Branch));
    }

    [Fact]
    public void FocusHoldIsUniversalStart()
    {
        var unlock = UnlockFor(BranchCode.FH);

        Assert.Empty(unlock.PrerequisiteBranches);
        Assert.Empty(unlock.RequiredLevels);
        Assert.Empty(unlock.AnyOfLevelGroups);
    }

    [Fact]
    public void FoundationalBranchesAfterFocusHoldRequireFocusHoldL1PassedOnce()
    {
        var expectedRequirement = Requirement(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce);

        foreach (var branch in new[] { BranchCode.FS, BranchCode.WM, BranchCode.IR, BranchCode.DE })
        {
            var unlock = UnlockFor(branch);

            Assert.Equal(new[] { BranchCode.FH }, unlock.PrerequisiteBranches);
            Assert.Equal(new[] { expectedRequirement }, unlock.RequiredLevels);
            Assert.Empty(unlock.AnyOfLevelGroups);
        }
    }

    [Fact]
    public void ConceptOperationsRequiresWorkingMemoryInhibitionAndDiscriminationL3Owned()
    {
        var unlock = UnlockFor(BranchCode.CO);

        Assert.Equal(new[] { BranchCode.WM, BranchCode.IR, BranchCode.DE }, unlock.PrerequisiteBranches);
        Assert.Equal(
            new[]
            {
                Requirement(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Owned),
            },
            unlock.RequiredLevels);
        Assert.Empty(unlock.AnyOfLevelGroups);
    }

    [Fact]
    public void AffectiveInterferenceControlRequiresFocusShiftAndInhibitionL3Owned()
    {
        var unlock = UnlockFor(BranchCode.AI);

        Assert.Equal(new[] { BranchCode.FH, BranchCode.FS, BranchCode.IR }, unlock.PrerequisiteBranches);
        Assert.Equal(
            new[]
            {
                Requirement(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Owned),
            },
            unlock.RequiredLevels);
        Assert.Empty(unlock.AnyOfLevelGroups);
    }

    [Fact]
    public void TransferIntegrationRequiresAllFoundationalL3OwnedAndOneAdvancedL2Owned()
    {
        var unlock = UnlockFor(BranchCode.TI);

        Assert.Equal(
            new[] { BranchCode.FH, BranchCode.FS, BranchCode.WM, BranchCode.IR, BranchCode.DE, BranchCode.CO, BranchCode.AI },
            unlock.PrerequisiteBranches);
        Assert.Equal(
            new[]
            {
                Requirement(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Owned),
            },
            unlock.RequiredLevels);

        var anyOfGroup = Assert.Single(unlock.AnyOfLevelGroups);
        Assert.Equal(
            new[]
            {
                Requirement(BranchCode.CO, GlobalLevelId.L2, BranchLevelState.Owned),
                Requirement(BranchCode.AI, GlobalLevelId.L2, BranchLevelState.Owned),
            },
            anyOfGroup.Requirements);
    }

    private static BranchUnlockDefinition UnlockFor(BranchCode branch)
    {
        return ProgramCatalog.BranchUnlocks.Single(unlock => unlock.Branch == branch);
    }

    private static BranchLevelRequirement Requirement(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState requiredState)
    {
        return new BranchLevelRequirement(branch, level, requiredState);
    }
}
