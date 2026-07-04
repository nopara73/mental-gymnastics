using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class GlobalBalanceTests
{
    [Fact]
    public void BalancedStateAllowsAdvancedAdvancementWhenBlockersAreClear()
    {
        var result = GlobalBalanceEvaluator.EvaluateAdvancement(
            Request(
                BranchCode.CO,
                GlobalLevelId.L1,
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
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
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void BlocksFoundationalAdvancementThatWouldCreateMoreThanTwoLevelSpread()
    {
        var result = GlobalBalanceEvaluator.EvaluateAdvancement(
            Request(
                BranchCode.FH,
                GlobalLevelId.L4,
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L1, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L1, BranchLevelState.Maintenance),
                ],
                []));

        Assert.False(result.CanAdvance);
        Assert.Contains(
            result.Issues,
            issue => issue.Kind == GlobalBalanceIssueKind.FoundationalOwnedLevelSpreadTooWide);
    }

    [Fact]
    public void BlocksAdvancedAdvancementWhenPrerequisiteMaintenanceIsOverdue()
    {
        var result = GlobalBalanceEvaluator.EvaluateAdvancement(
            Request(
                BranchCode.CO,
                GlobalLevelId.L2,
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ],
                [
                    Current(BranchCode.WM, GlobalLevelId.L3),
                    Due(BranchCode.IR, GlobalLevelId.L3),
                    Current(BranchCode.DE, GlobalLevelId.L3),
                ]));

        Assert.False(result.CanAdvance);
        Assert.Contains(
            result.Issues,
            issue => issue.Kind == GlobalBalanceIssueKind.AdvancedPrerequisiteMaintenanceOverdue &&
                issue.Branch == BranchCode.IR);
    }

    [Fact]
    public void BlocksTransferIntegrationWhenComponentBranchFailedLastGlobalReview()
    {
        var result = GlobalBalanceEvaluator.EvaluateAdvancement(
            Request(
                BranchCode.TI,
                GlobalLevelId.L2,
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.CO, GlobalLevelId.L2, BranchLevelState.Maintenance),
                ],
                [
                    Current(BranchCode.FH, GlobalLevelId.L3),
                    Current(BranchCode.FS, GlobalLevelId.L3),
                    Current(BranchCode.WM, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                    Current(BranchCode.DE, GlobalLevelId.L3),
                    Current(BranchCode.CO, GlobalLevelId.L2),
                ],
                new GlobalReviewResult(
                    passed: true,
                    [
                        new GlobalReviewComponentScore(BranchCode.FH, Passed: true),
                        new GlobalReviewComponentScore(BranchCode.WM, Passed: false),
                    ])));

        Assert.False(result.CanAdvance);
        Assert.Contains(
            result.Issues,
            issue => issue.Kind == GlobalBalanceIssueKind.TransferIntegrationComponentFailedLastGlobalReview &&
                issue.Branch == BranchCode.WM);
    }

    [Theory]
    [InlineData(BranchCode.CO, BranchCode.WM, GlobalBalanceIssueKind.ConceptOperationsPrerequisiteDecayed)]
    [InlineData(BranchCode.CO, BranchCode.DE, GlobalBalanceIssueKind.ConceptOperationsPrerequisiteDecayed)]
    [InlineData(BranchCode.AI, BranchCode.FH, GlobalBalanceIssueKind.AffectiveInterferencePrerequisiteDecayed)]
    [InlineData(BranchCode.AI, BranchCode.FS, GlobalBalanceIssueKind.AffectiveInterferencePrerequisiteDecayed)]
    [InlineData(BranchCode.AI, BranchCode.IR, GlobalBalanceIssueKind.AffectiveInterferencePrerequisiteDecayed)]
    public void BlocksDocumentedDecayCasesForConceptOperationsAndAffectiveInterference(
        BranchCode targetBranch,
        BranchCode decayedBranch,
        GlobalBalanceIssueKind expectedIssue)
    {
        var statuses = new List<BranchLevelStatus>
        {
            Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
        };
        statuses.RemoveAll(status => status.Branch == decayedBranch && status.Level == GlobalLevelId.L3);
        statuses.Add(Status(decayedBranch, GlobalLevelId.L3, BranchLevelState.Decayed));

        var result = GlobalBalanceEvaluator.EvaluateAdvancement(
            Request(
                targetBranch,
                GlobalLevelId.L2,
                statuses,
                [
                    Current(BranchCode.FH, GlobalLevelId.L3),
                    Current(BranchCode.FS, GlobalLevelId.L3),
                    Current(BranchCode.WM, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                    Current(BranchCode.DE, GlobalLevelId.L3),
                ]));

        Assert.False(result.CanAdvance);
        Assert.Contains(
            result.Issues,
            issue => issue.Kind == expectedIssue && issue.Branch == decayedBranch);
    }

    [Fact]
    public void AdvancedClassificationRequiresPassedLastGlobalReview()
    {
        var failedReview = GlobalBalanceEvaluator.EvaluateAdvancedClassification(
            new GlobalReviewResult(
                passed: false,
                [new GlobalReviewComponentScore(BranchCode.TI, Passed: true)]));
        var passedReview = GlobalBalanceEvaluator.EvaluateAdvancedClassification(
            new GlobalReviewResult(
                passed: true,
                [new GlobalReviewComponentScore(BranchCode.TI, Passed: true)]));

        Assert.False(failedReview.CanClassifyAsAdvanced);
        Assert.Contains(
            failedReview.Issues,
            issue => issue.Kind == GlobalBalanceIssueKind.AdvancedClassificationRequiresPassedGlobalReview);
        Assert.True(passedReview.CanClassifyAsAdvanced);
        Assert.Empty(passedReview.Issues);
    }

    private static GlobalBalanceAdvancementRequest Request(
        BranchCode targetBranch,
        GlobalLevelId targetLevel,
        IEnumerable<BranchLevelStatus> statuses,
        IEnumerable<MaintenanceCurrencyResult> maintenanceCurrency,
        GlobalReviewResult? lastGlobalReview = null)
    {
        return new GlobalBalanceAdvancementRequest(
            targetBranch,
            targetLevel,
            new PractitionerState(statuses),
            maintenanceCurrency,
            lastGlobalReview);
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static MaintenanceCurrencyResult Current(BranchCode branch, GlobalLevelId level)
    {
        return Currency(branch, level, MaintenanceCurrencyState.Current);
    }

    private static MaintenanceCurrencyResult Due(BranchCode branch, GlobalLevelId level)
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
            DaysSinceLastPassingCheck: 11,
            ConsecutiveFailures: 0);
    }
}
