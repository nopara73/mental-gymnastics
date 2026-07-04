using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class PractitionerStateTests
{
    [Fact]
    public void RepresentsDocumentedBranchLevelStatesAcrossMultipleBranchesAndLevels()
    {
        var branchLevels = new[]
        {
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Unopened),
            new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L2, BranchLevelState.TestReady),
            new BranchLevelStatus(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.PassedOnce),
            new BranchLevelStatus(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Stabilizing),
            new BranchLevelStatus(BranchCode.CO, GlobalLevelId.L1, BranchLevelState.Owned),
            new BranchLevelStatus(BranchCode.AI, GlobalLevelId.L4, BranchLevelState.Maintenance),
            new BranchLevelStatus(BranchCode.TI, GlobalLevelId.L5, BranchLevelState.Decayed),
        };

        var practitionerState = new PractitionerState(branchLevels);

        Assert.Equal(branchLevels, practitionerState.BranchLevels);
        foreach (var branchLevel in branchLevels)
        {
            Assert.True(practitionerState.TryGetBranchLevelState(branchLevel.Branch, branchLevel.Level, out var state));
            Assert.Equal(branchLevel.State, state);
            Assert.Equal(branchLevel.State, practitionerState.GetBranchLevelState(branchLevel.Branch, branchLevel.Level));
        }
    }

    [Fact]
    public void DoesNotInferUnrecordedBranchLevelState()
    {
        var practitionerState = new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
        ]);

        Assert.False(practitionerState.TryGetBranchLevelState(BranchCode.FH, GlobalLevelId.L2, out _));
    }
}
