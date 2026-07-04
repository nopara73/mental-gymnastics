using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class DependencyCapTests
{
    [Fact]
    public void RepresentsDocumentedAdvancedDependencyBlocks()
    {
        Assert.Equal(
            [BranchCode.CO, BranchCode.AI, BranchCode.TI],
            DependencyCapCatalog.AdvancedDependencyBlocks.Select(block => block.Branch));

        Assert.Equal(
            [
                Requirement(BranchCode.WM, GlobalLevelId.L3),
                Requirement(BranchCode.IR, GlobalLevelId.L3),
                Requirement(BranchCode.DE, GlobalLevelId.L3),
            ],
            BlockFor(BranchCode.CO).RequiredLevels);
        Assert.Empty(BlockFor(BranchCode.CO).AnyOfLevelGroups);

        Assert.Equal(
            [
                Requirement(BranchCode.FH, GlobalLevelId.L3),
                Requirement(BranchCode.FS, GlobalLevelId.L3),
                Requirement(BranchCode.IR, GlobalLevelId.L3),
            ],
            BlockFor(BranchCode.AI).RequiredLevels);
        Assert.Empty(BlockFor(BranchCode.AI).AnyOfLevelGroups);

        var transferIntegration = BlockFor(BranchCode.TI);
        Assert.Equal(
            [
                Requirement(BranchCode.FH, GlobalLevelId.L3),
                Requirement(BranchCode.FS, GlobalLevelId.L3),
                Requirement(BranchCode.WM, GlobalLevelId.L3),
                Requirement(BranchCode.IR, GlobalLevelId.L3),
                Requirement(BranchCode.DE, GlobalLevelId.L3),
            ],
            transferIntegration.RequiredLevels);

        var advancedRoute = Assert.Single(transferIntegration.AnyOfLevelGroups);
        Assert.Equal(
            [
                Requirement(BranchCode.CO, GlobalLevelId.L2),
                Requirement(BranchCode.AI, GlobalLevelId.L2),
            ],
            advancedRoute.Requirements);
    }

    [Fact]
    public void AllowsAdvancedBranchAdvancementWhenDependenciesAreOwnedAndCurrent()
    {
        var result = DependencyCapEvaluator.Evaluate(
            Request(
                BranchCode.CO,
                [
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ],
                [
                    Current(BranchCode.WM, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                    Current(BranchCode.DE, GlobalLevelId.L3),
                ]));

        Assert.True(result.CanAdvance);
        Assert.False(result.IsCappedToMaintenanceOnly);
        Assert.Empty(result.Caps);
    }

    [Fact]
    public void CapsConceptOperationsWhenPrerequisiteBranchIsDecayed()
    {
        var result = DependencyCapEvaluator.Evaluate(
            Request(
                BranchCode.CO,
                [
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Decayed),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ],
                [
                    Current(BranchCode.WM, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                    Current(BranchCode.DE, GlobalLevelId.L3),
                ]));

        Assert.False(result.CanAdvance);
        Assert.True(result.IsCappedToMaintenanceOnly);
        Assert.Contains(
            result.Caps,
            cap => cap.Reason == DependencyCapReason.DecayedPrerequisite &&
                cap.PrerequisiteBranch == BranchCode.WM &&
                cap.PrerequisiteLevel == GlobalLevelId.L3);
    }

    [Fact]
    public void CapsAffectiveInterferenceWhenPrerequisiteMaintenanceIsOverdue()
    {
        var result = DependencyCapEvaluator.Evaluate(
            Request(
                BranchCode.AI,
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ],
                [
                    Current(BranchCode.FH, GlobalLevelId.L3),
                    Overdue(BranchCode.FS, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                ]));

        Assert.False(result.CanAdvance);
        Assert.True(result.IsCappedToMaintenanceOnly);
        Assert.Contains(
            result.Caps,
            cap => cap.Reason == DependencyCapReason.OverduePrerequisiteMaintenance &&
                cap.PrerequisiteBranch == BranchCode.FS &&
                cap.PrerequisiteLevel == GlobalLevelId.L3);
    }

    [Fact]
    public void CapsTransferIntegrationWhenEveryAdvancedPrerequisiteRouteIsBlocked()
    {
        var result = DependencyCapEvaluator.Evaluate(
            Request(
                BranchCode.TI,
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.CO, GlobalLevelId.L2, BranchLevelState.Decayed),
                    Status(BranchCode.AI, GlobalLevelId.L2, BranchLevelState.Maintenance),
                ],
                [
                    Current(BranchCode.FH, GlobalLevelId.L3),
                    Current(BranchCode.FS, GlobalLevelId.L3),
                    Current(BranchCode.WM, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                    Current(BranchCode.DE, GlobalLevelId.L3),
                    Current(BranchCode.CO, GlobalLevelId.L2),
                    Overdue(BranchCode.AI, GlobalLevelId.L2),
                ]));

        Assert.False(result.CanAdvance);
        Assert.True(result.IsCappedToMaintenanceOnly);
        Assert.Contains(result.Caps, cap => cap.PrerequisiteBranch == BranchCode.CO);
        Assert.Contains(result.Caps, cap => cap.PrerequisiteBranch == BranchCode.AI);
    }

    [Fact]
    public void TransferIntegrationCanAdvanceWhenOneAdvancedPrerequisiteRouteIsHealthy()
    {
        var result = DependencyCapEvaluator.Evaluate(
            Request(
                BranchCode.TI,
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.CO, GlobalLevelId.L2, BranchLevelState.Maintenance),
                    Status(BranchCode.AI, GlobalLevelId.L2, BranchLevelState.Decayed),
                ],
                [
                    Current(BranchCode.FH, GlobalLevelId.L3),
                    Current(BranchCode.FS, GlobalLevelId.L3),
                    Current(BranchCode.WM, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                    Current(BranchCode.DE, GlobalLevelId.L3),
                    Current(BranchCode.CO, GlobalLevelId.L2),
                    Current(BranchCode.AI, GlobalLevelId.L2),
                ]));

        Assert.True(result.CanAdvance);
        Assert.False(result.IsCappedToMaintenanceOnly);
        Assert.Empty(result.Caps);
    }

    [Fact]
    public void RestoredPrerequisiteRestoresAdvancementEligibility()
    {
        var capped = DependencyCapEvaluator.Evaluate(
            Request(
                BranchCode.AI,
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Decayed),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ],
                [
                    Current(BranchCode.FH, GlobalLevelId.L3),
                    Current(BranchCode.FS, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                ]));
        var restored = DependencyCapEvaluator.Evaluate(
            Request(
                BranchCode.AI,
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ],
                [
                    Current(BranchCode.FH, GlobalLevelId.L3),
                    Current(BranchCode.FS, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                ]));

        Assert.False(capped.CanAdvance);
        Assert.True(restored.CanAdvance);
        Assert.Empty(restored.Caps);
    }

    private static AdvancedBranchDependencyBlock BlockFor(BranchCode branch)
    {
        return DependencyCapCatalog.AdvancedDependencyBlocks.Single(block => block.Branch == branch);
    }

    private static DependencyCapRequest Request(
        BranchCode branch,
        IEnumerable<BranchLevelStatus> statuses,
        IEnumerable<MaintenanceCurrencyResult> maintenanceCurrency)
    {
        return new DependencyCapRequest(
            branch,
            new PractitionerState(statuses),
            maintenanceCurrency);
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static BranchLevelRequirement Requirement(BranchCode branch, GlobalLevelId level)
    {
        return new BranchLevelRequirement(branch, level, BranchLevelState.Owned);
    }

    private static MaintenanceCurrencyResult Current(BranchCode branch, GlobalLevelId level)
    {
        return Currency(branch, level, MaintenanceCurrencyState.Current);
    }

    private static MaintenanceCurrencyResult Overdue(BranchCode branch, GlobalLevelId level)
    {
        return Currency(branch, level, MaintenanceCurrencyState.Due);
    }

    private static MaintenanceCurrencyResult Currency(
        BranchCode branch,
        GlobalLevelId level,
        MaintenanceCurrencyState state)
    {
        return new MaintenanceCurrencyResult(
            branch,
            level,
            state,
            new MaintenanceCadence(7, 10, MaintenanceCheckKind.StandardOrTransfer),
            11,
            0);
    }
}
