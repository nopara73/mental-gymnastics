using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class MaintenanceScopeTests
{
    [Fact]
    public void OnlyHighestEarnedLevelInEachBranchRequiresMaintenance()
    {
        var state = new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Owned),
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Training),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Maintenance),
        ]);

        var scope = MaintenanceScope.HighestEarnedByBranch(state);

        Assert.Equal(2, scope.Count);
        Assert.Contains(scope, status =>
            status.Branch == BranchCode.FH && status.Level == GlobalLevelId.L2);
        Assert.DoesNotContain(scope, status =>
            status.Branch == BranchCode.FH && status.Level == GlobalLevelId.L1);
    }

    [Fact]
    public void CurrentHigherLevelMaintenanceSatisfiesLowerPrerequisiteCurrency()
    {
        var state = new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Owned),
            new BranchLevelStatus(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Owned),
            new BranchLevelStatus(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Owned),
            new BranchLevelStatus(BranchCode.CO, GlobalLevelId.L1, BranchLevelState.Training),
        ]);
        var maintenance = new[]
        {
            Current(BranchCode.WM, GlobalLevelId.L4),
            Current(BranchCode.IR, GlobalLevelId.L4),
            Current(BranchCode.DE, GlobalLevelId.L4),
        };

        var result = DependencyCapEvaluator.Evaluate(
            new DependencyCapRequest(BranchCode.CO, state, maintenance));

        Assert.True(result.CanAdvance);
        Assert.Empty(result.Caps);
    }

    private static MaintenanceCurrencyResult Current(
        BranchCode branch,
        GlobalLevelId level)
    {
        return new MaintenanceCurrencyResult(
            branch,
            level,
            MaintenanceCurrencyState.Current,
            MaintenanceCurrencyEvaluator.CadenceFor(branch, level),
            DaysSinceLastPassingCheck: 1,
            ConsecutiveFailures: 0);
    }
}
