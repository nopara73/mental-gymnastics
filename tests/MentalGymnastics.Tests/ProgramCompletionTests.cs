using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class ProgramCompletionTests
{
    [Fact]
    public void AllEarnedLevelsAndPassedReviewCompleteTheCurriculumWithoutEndingMaintenance()
    {
        var result = ProgramCompletionEvaluator.Evaluate(
            State(BranchLevelState.Owned),
            CurrentMaintenance(),
            new GlobalReviewResult(true, []));

        Assert.Equal(ProgramCompletionState.CompleteMaintaining, result.State);
        Assert.True(result.CurriculumComplete);
        Assert.Equal(40, result.EarnedLevelCount);
        Assert.Equal(0, result.RemainingLevelCount);
    }

    [Fact]
    public void OwnershipWithoutPassedGlobalReviewIsNotComplete()
    {
        var result = ProgramCompletionEvaluator.Evaluate(
            State(BranchLevelState.Owned),
            CurrentMaintenance(),
            lastGlobalReview: null);

        Assert.Equal(ProgramCompletionState.AwaitingGlobalReview, result.State);
        Assert.False(result.CurriculumComplete);
    }

    [Fact]
    public void CompletedCurriculumSurfacesRequiredMaintenance()
    {
        var maintenance = CurrentMaintenance().ToArray();
        maintenance[0] = maintenance[0] with { State = MaintenanceCurrencyState.Due };
        var result = ProgramCompletionEvaluator.Evaluate(
            State(BranchLevelState.Owned),
            maintenance,
            new GlobalReviewResult(true, []));

        Assert.Equal(ProgramCompletionState.CompleteMaintenanceRequired, result.State);
        Assert.True(result.CurriculumComplete);
        Assert.Equal(1, result.MaintenanceRequirementCount);
    }

    private static PractitionerState State(BranchLevelState state)
    {
        return new PractitionerState(ProgramCatalog.Branches.SelectMany(branch =>
            ProgramCatalog.GlobalLevels.Select(level =>
                new BranchLevelStatus(branch.Code, level.Id, state))));
    }

    private static IReadOnlyList<MaintenanceCurrencyResult> CurrentMaintenance()
    {
        return ProgramCatalog.Branches.SelectMany(branch =>
            ProgramCatalog.GlobalLevels.Select(level =>
                new MaintenanceCurrencyResult(
                    branch.Code,
                    level.Id,
                    MaintenanceCurrencyState.Current,
                    MaintenanceCurrencyEvaluator.CadenceFor(branch.Code, level.Id),
                    DaysSinceLastPassingCheck: 1,
                    ConsecutiveFailures: 0))).ToArray();
    }
}
