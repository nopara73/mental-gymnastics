using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class PractitionerProgressionProjectionTests
{
    [Fact]
    public void PassingFocusHoldOnceOpensFoundationalLevelOnePracticeOnly()
    {
        var state = State((BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce));

        var result = PractitionerProgressionProjector.Project(
            new PractitionerProgressionProjectionRequest(state));

        Assert.Equal(BranchLevelState.PassedOnce, result.PractitionerState.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Training, result.PractitionerState.GetBranchLevelState(BranchCode.FS, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Training, result.PractitionerState.GetBranchLevelState(BranchCode.WM, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Training, result.PractitionerState.GetBranchLevelState(BranchCode.IR, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Training, result.PractitionerState.GetBranchLevelState(BranchCode.DE, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Unopened, result.PractitionerState.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L2));
        Assert.Equal(BranchLevelState.Unopened, result.PractitionerState.GetBranchLevelState(BranchCode.CO, GlobalLevelId.L1));
        Assert.Equal(4, result.OpenedForTraining.Count);
    }

    [Fact]
    public void OwningALevelOpensTheNextDemandWhenItsPrerequisitesHold()
    {
        var state = State(
            (BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
            (BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Owned));

        var result = PractitionerProgressionProjector.Project(
            new PractitionerProgressionProjectionRequest(state));

        Assert.Equal(BranchLevelState.Training, result.PractitionerState.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L2));
        Assert.Equal(BranchLevelState.Training, result.PractitionerState.GetBranchLevelState(BranchCode.FS, GlobalLevelId.L2));
        Assert.Equal(BranchLevelState.Unopened, result.PractitionerState.GetBranchLevelState(BranchCode.FS, GlobalLevelId.L3));
    }

    [Fact]
    public void CrossBranchGatePreventsOpeningHigherDemandEarly()
    {
        var state = State(
            (BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
            (BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Owned),
            (BranchCode.FS, GlobalLevelId.L2, BranchLevelState.Owned),
            (BranchCode.IR, GlobalLevelId.L1, BranchLevelState.Owned),
            (BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Training));

        var result = PractitionerProgressionProjector.Project(
            new PractitionerProgressionProjectionRequest(state));

        Assert.Equal(BranchLevelState.Unopened, result.PractitionerState.GetBranchLevelState(BranchCode.FS, GlobalLevelId.L3));
    }

    private static PractitionerState State(
        params (BranchCode Branch, GlobalLevelId Level, BranchLevelState State)[] overrides)
    {
        var values = overrides.ToDictionary(item => (item.Branch, item.Level), item => item.State);
        return new PractitionerState(
            ProgramCatalog.Branches.SelectMany(
                branch => ProgramCatalog.GlobalLevels.Select(level => new BranchLevelStatus(
                    branch.Code,
                    level.Id,
                    values.GetValueOrDefault((branch.Code, level.Id), BranchLevelState.Unopened)))));
    }
}
