using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class PractitionerCategoryClassificationTests
{
    [Fact]
    public void ClassifiesBeginnerWhenAnyFoundationalBranchIsBelowL2Owned()
    {
        var result = PractitionerCategoryClassifier.Classify(
            Request(
                [
                    Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L2, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L2, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L2, BranchLevelState.Maintenance),
                    Status(BranchCode.CO, GlobalLevelId.L1, BranchLevelState.Training),
                ],
                [
                    Current(BranchCode.FH, GlobalLevelId.L1),
                    Current(BranchCode.FS, GlobalLevelId.L2),
                    Current(BranchCode.WM, GlobalLevelId.L2),
                    Current(BranchCode.IR, GlobalLevelId.L2),
                    Current(BranchCode.DE, GlobalLevelId.L2),
                ]));

        Assert.Equal(PractitionerCategory.Beginner, result.Category);
        Assert.Contains(
            result.Blockers,
            blocker => blocker.Kind == PractitionerCategoryBlockerKind.FoundationalBranchBelowL2Owned &&
                blocker.Branch == BranchCode.FH);
    }

    [Fact]
    public void ClassifiesBeginnerWhenNoAdvancedBranchIsOpened()
    {
        var result = PractitionerCategoryClassifier.Classify(
            Request(
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ],
                CurrentFoundationalMaintenance(GlobalLevelId.L3)));

        Assert.Equal(PractitionerCategory.Beginner, result.Category);
        Assert.Contains(result.Blockers, blocker => blocker.Kind == PractitionerCategoryBlockerKind.AdvancedBranchNotOpened);
    }

    [Fact]
    public void ClassifiesIntermediateFromFoundationalL3CurrentMaintenanceAndOpenedAdvancedBranch()
    {
        var result = PractitionerCategoryClassifier.Classify(
            Request(
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.CO, GlobalLevelId.L1, BranchLevelState.Training),
                ],
                CurrentFoundationalMaintenance(GlobalLevelId.L3)));

        Assert.Equal(PractitionerCategory.Intermediate, result.Category);
        Assert.DoesNotContain(result.Blockers, blocker => blocker.Kind == PractitionerCategoryBlockerKind.FoundationalBranchBelowL3Owned);
        Assert.DoesNotContain(result.Blockers, blocker => blocker.Kind == PractitionerCategoryBlockerKind.MaintenanceNotCurrent);
        Assert.DoesNotContain(result.Blockers, blocker => blocker.Kind == PractitionerCategoryBlockerKind.AdvancedBranchNotOpened);
    }

    [Fact]
    public void ClassifiesAdvancedOnlyWhenAllAdvancedPrerequisitesAndLastGlobalReviewPass()
    {
        var result = PractitionerCategoryClassifier.Classify(
            Request(
                [
                    Status(BranchCode.FH, GlobalLevelId.L4, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L4, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L4, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L4, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L4, BranchLevelState.Maintenance),
                    Status(BranchCode.CO, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.AI, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.TI, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ],
                [
                    .. CurrentFoundationalMaintenance(GlobalLevelId.L4),
                    Current(BranchCode.CO, GlobalLevelId.L3),
                    Current(BranchCode.AI, GlobalLevelId.L3),
                    Current(BranchCode.TI, GlobalLevelId.L3),
                ],
                lastGlobalReview: PassedGlobalReview()));

        Assert.Equal(PractitionerCategory.Advanced, result.Category);
        Assert.Empty(result.Blockers);
    }

    [Fact]
    public void BlocksAdvancedWhenLastGlobalReviewDidNotPass()
    {
        var result = PractitionerCategoryClassifier.Classify(
            Request(
                [
                    Status(BranchCode.FH, GlobalLevelId.L4, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L4, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L4, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L4, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L4, BranchLevelState.Maintenance),
                    Status(BranchCode.CO, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.AI, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.TI, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ],
                [
                    .. CurrentFoundationalMaintenance(GlobalLevelId.L4),
                    Current(BranchCode.CO, GlobalLevelId.L3),
                    Current(BranchCode.AI, GlobalLevelId.L3),
                    Current(BranchCode.TI, GlobalLevelId.L3),
                ],
                lastGlobalReview: FailedGlobalReview()));

        Assert.Equal(PractitionerCategory.Intermediate, result.Category);
        Assert.Contains(result.Blockers, blocker => blocker.Kind == PractitionerCategoryBlockerKind.LastGlobalReviewNotPassed);
    }

    [Fact]
    public void MaintenanceDueBlocksIntermediateClassificationWithoutUsingSelfDescription()
    {
        var requestType = typeof(PractitionerCategoryClassificationRequest);
        Assert.DoesNotContain(requestType.GetProperties(), property => property.Name.Contains("Self", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(requestType.GetProperties(), property => property.Name.Contains("Claim", StringComparison.OrdinalIgnoreCase));

        var result = PractitionerCategoryClassifier.Classify(
            Request(
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.CO, GlobalLevelId.L1, BranchLevelState.Training),
                ],
                [
                    Current(BranchCode.FH, GlobalLevelId.L3),
                    Current(BranchCode.FS, GlobalLevelId.L3),
                    Due(BranchCode.WM, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                    Current(BranchCode.DE, GlobalLevelId.L3),
                ]));

        Assert.Equal(PractitionerCategory.Beginner, result.Category);
        Assert.Contains(
            result.Blockers,
            blocker => blocker.Kind == PractitionerCategoryBlockerKind.MaintenanceNotCurrent &&
                blocker.Branch == BranchCode.WM);
    }

    private static PractitionerCategoryClassificationRequest Request(
        IEnumerable<BranchLevelStatus> statuses,
        IEnumerable<MaintenanceCurrencyResult> maintenance,
        GlobalReviewResult? lastGlobalReview = null)
    {
        return new PractitionerCategoryClassificationRequest(
            new PractitionerState(statuses),
            maintenance,
            lastGlobalReview);
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static IReadOnlyList<MaintenanceCurrencyResult> CurrentFoundationalMaintenance(GlobalLevelId level)
    {
        return
        [
            Current(BranchCode.FH, level),
            Current(BranchCode.FS, level),
            Current(BranchCode.WM, level),
            Current(BranchCode.IR, level),
            Current(BranchCode.DE, level),
        ];
    }

    private static MaintenanceCurrencyResult Current(BranchCode branch, GlobalLevelId level)
    {
        return Maintenance(branch, level, MaintenanceCurrencyState.Current);
    }

    private static MaintenanceCurrencyResult Due(BranchCode branch, GlobalLevelId level)
    {
        return Maintenance(branch, level, MaintenanceCurrencyState.Due);
    }

    private static MaintenanceCurrencyResult Maintenance(
        BranchCode branch,
        GlobalLevelId level,
        MaintenanceCurrencyState state)
    {
        return new MaintenanceCurrencyResult(
            branch,
            level,
            state,
            new MaintenanceCadence(7, 10, MaintenanceCheckKind.StandardOrTransfer),
            DaysSinceLastPassingCheck: state == MaintenanceCurrencyState.Current ? 3 : 11,
            ConsecutiveFailures: 0);
    }

    private static GlobalReviewResult PassedGlobalReview()
    {
        return new GlobalReviewResult(
            passed: true,
            [new GlobalReviewComponentScore(BranchCode.TI, Passed: true)]);
    }

    private static GlobalReviewResult FailedGlobalReview()
    {
        return new GlobalReviewResult(
            passed: false,
            [new GlobalReviewComponentScore(BranchCode.TI, Passed: true)]);
    }
}
