using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class WeeklyProgrammingTemplateTests
{
    [Fact]
    public void GeneratesDocumentedBeginnerWeeklyStructure()
    {
        var plan = WeeklyProgrammingPlanner.Generate(
            Request(BeginnerClassification()));

        Assert.Equal(PractitionerCategory.Beginner, plan.PractitionerCategory);
        Assert.Equal(7, plan.Days.Count);
        AssertDay(plan, 1, WeeklySessionKind.Practice, BranchCode.FH, BranchCode.FS, BranchCode.WM);
        AssertDay(plan, 2, WeeklySessionKind.Practice, BranchCode.FH, BranchCode.IR, BranchCode.DE);
        AssertDay(plan, 3, WeeklySessionKind.RecoveryOrLightMaintenance, BranchCode.FH, BranchCode.WM);
        AssertDay(plan, 4, WeeklySessionKind.Load, BranchCode.WM, BranchCode.FH);
        AssertDay(plan, 5, WeeklySessionKind.Practice, BranchCode.WM, BranchCode.IR, BranchCode.DE);
        AssertDay(plan, 6, WeeklySessionKind.TestOrStabilization, BranchCode.WM);
        AssertDay(plan, 7, WeeklySessionKind.OffOrRecovery);
    }

    [Fact]
    public void GeneratesDocumentedIntermediateWeeklyStructure()
    {
        var plan = WeeklyProgrammingPlanner.Generate(
            Request(IntermediateClassification()));

        Assert.Equal(PractitionerCategory.Intermediate, plan.PractitionerCategory);
        AssertDay(plan, 1, WeeklySessionKind.Practice, BranchCode.WM, BranchCode.CO);
        AssertDay(plan, 2, WeeklySessionKind.Load, BranchCode.CO);
        AssertDay(plan, 3, WeeklySessionKind.Maintenance, BranchCode.FH, BranchCode.FS, BranchCode.WM, BranchCode.IR, BranchCode.DE);
        AssertDay(plan, 4, WeeklySessionKind.Practice, BranchCode.CO, BranchCode.WM);
        AssertDay(plan, 5, WeeklySessionKind.TransferOrStabilization, BranchCode.CO);
        AssertDay(plan, 6, WeeklySessionKind.RecoveryOrRetest);
        AssertDay(plan, 7, WeeklySessionKind.Off);
    }

    [Fact]
    public void GeneratesDocumentedAdvancedWeeklyStructure()
    {
        var plan = WeeklyProgrammingPlanner.Generate(
            Request(
                AdvancedClassification(),
                selectedAdvancedBranch: BranchCode.TI));

        Assert.Equal(PractitionerCategory.Advanced, plan.PractitionerCategory);
        AssertDay(plan, 1, WeeklySessionKind.Maintenance, BranchCode.FH, BranchCode.FS, BranchCode.WM, BranchCode.IR, BranchCode.DE);
        AssertDay(plan, 2, WeeklySessionKind.Load, BranchCode.TI);
        AssertDay(plan, 3, WeeklySessionKind.Practice, BranchCode.DE);
        AssertDay(plan, 4, WeeklySessionKind.Transfer, BranchCode.TI);
        AssertDay(plan, 5, WeeklySessionKind.Stabilization, BranchCode.AI);
        AssertDay(plan, 6, WeeklySessionKind.RecoveryOrRetest);
        AssertDay(plan, 7, WeeklySessionKind.Off);
    }

    [Fact]
    public void BeginnerFollowsFixedTemplateUntilAllFoundationalBranchesReachL2Owned()
    {
        var classification = PractitionerCategoryClassifier.Classify(
            new PractitionerCategoryClassificationRequest(
                new PractitionerState(
                [
                    Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L2, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L2, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L2, BranchLevelState.Maintenance),
                    Status(BranchCode.CO, GlobalLevelId.L1, BranchLevelState.Training),
                ]),
                [
                    Current(BranchCode.FH, GlobalLevelId.L1),
                    Current(BranchCode.FS, GlobalLevelId.L2),
                    Current(BranchCode.WM, GlobalLevelId.L2),
                    Current(BranchCode.IR, GlobalLevelId.L2),
                    Current(BranchCode.DE, GlobalLevelId.L2),
                ]));

        var plan = WeeklyProgrammingPlanner.Generate(
            Request(classification, selectedAdvancedBranch: BranchCode.CO));

        Assert.Equal(PractitionerCategory.Beginner, plan.PractitionerCategory);
        Assert.Contains(
            plan.Constraints,
            constraint => constraint.Kind == WeeklyProgrammingConstraintKind.BeginnerFixedTemplateRequired);
        AssertDay(plan, 1, WeeklySessionKind.Practice, BranchCode.FH, BranchCode.FS, BranchCode.WM);
    }

    [Fact]
    public void OverdueMaintenanceBlocksAdvancementWorkAndRoutesToMaintenance()
    {
        var plan = WeeklyProgrammingPlanner.Generate(
            Request(
                IntermediateClassification(),
                maintenanceCurrency:
                [
                    Current(BranchCode.FH, GlobalLevelId.L3),
                    Current(BranchCode.FS, GlobalLevelId.L3),
                    Due(BranchCode.WM, GlobalLevelId.L3),
                    Current(BranchCode.IR, GlobalLevelId.L3),
                    Current(BranchCode.DE, GlobalLevelId.L3),
                ]));

        Assert.False(plan.AdvancementWorkAllowed);
        Assert.Contains(
            plan.Constraints,
            constraint => constraint.Kind == WeeklyProgrammingConstraintKind.MaintenanceNotCurrent &&
                constraint.Branch == BranchCode.WM);
        Assert.DoesNotContain(plan.Days, day => day.IsAdvancementWork);
        Assert.Contains(
            plan.Days,
            day => day.Session == WeeklySessionKind.Maintenance &&
                day.BranchEmphasis.SequenceEqual([BranchCode.WM]));
    }

    [Fact]
    public void RecoveryRequirementReplacesAdvancementWorkWithRecovery()
    {
        var plan = WeeklyProgrammingPlanner.Generate(
            Request(
                IntermediateClassification(),
                recoveryRequired: true));

        Assert.False(plan.AdvancementWorkAllowed);
        Assert.Contains(plan.Constraints, constraint => constraint.Kind == WeeklyProgrammingConstraintKind.RecoveryRequired);
        Assert.DoesNotContain(plan.Days, day => day.IsAdvancementWork);
        Assert.Contains(plan.Days, day => day.Session == WeeklySessionKind.Recovery);
    }

    [Fact]
    public void AdvancementPausePreventsTestingTransferAndLoadSlots()
    {
        var plan = WeeklyProgrammingPlanner.Generate(
            Request(
                AdvancedClassification(),
                globalReviewDecisions:
                [
                    new GlobalReviewDecision(
                        GlobalReviewDecisionKind.PauseTestsForDeload,
                        Branch: null,
                        "Pause tests for deload."),
                ]));

        Assert.False(plan.AdvancementWorkAllowed);
        Assert.Contains(plan.Constraints, constraint => constraint.Kind == WeeklyProgrammingConstraintKind.AdvancementTestingSuspended);
        Assert.DoesNotContain(plan.Days, day => day.IsAdvancementWork);
    }

    private static WeeklyProgrammingRequest Request(
        PractitionerCategoryClassificationResult classification,
        IEnumerable<MaintenanceCurrencyResult>? maintenanceCurrency = null,
        IEnumerable<GlobalReviewDecision>? globalReviewDecisions = null,
        bool recoveryRequired = false,
        BranchCode selectedFoundationalLoadBranch = BranchCode.WM,
        BranchCode weakestFoundationalBranch = BranchCode.WM,
        BranchCode selectedAdvancedBranch = BranchCode.CO,
        BranchCode prerequisiteSupportBranch = BranchCode.WM,
        BranchCode eligibleAdvancementBranch = BranchCode.WM,
        BranchCode bottleneckBranch = BranchCode.DE,
        BranchCode recentlyPassedBranch = BranchCode.AI,
        BranchCode transferBranch = BranchCode.TI)
    {
        return new WeeklyProgrammingRequest(
            classification,
            maintenanceCurrency ?? CurrentMaintenance(GlobalLevelId.L3),
            globalReviewDecisions ?? [],
            recoveryRequired,
            selectedFoundationalLoadBranch,
            weakestFoundationalBranch,
            selectedAdvancedBranch,
            prerequisiteSupportBranch,
            eligibleAdvancementBranch,
            bottleneckBranch,
            recentlyPassedBranch,
            transferBranch);
    }

    private static PractitionerCategoryClassificationResult BeginnerClassification()
    {
        return new PractitionerCategoryClassificationResult(
            PractitionerCategory.Beginner,
            [
                new PractitionerCategoryBlocker(
                    PractitionerCategoryBlockerKind.FoundationalBranchBelowL2Owned,
                    BranchCode.WM,
                    GlobalLevelId.L1,
                    "WM is below L2 owned."),
            ]);
    }

    private static PractitionerCategoryClassificationResult IntermediateClassification()
    {
        return new PractitionerCategoryClassificationResult(PractitionerCategory.Intermediate, []);
    }

    private static PractitionerCategoryClassificationResult AdvancedClassification()
    {
        return new PractitionerCategoryClassificationResult(PractitionerCategory.Advanced, []);
    }

    private static IReadOnlyList<MaintenanceCurrencyResult> CurrentMaintenance(GlobalLevelId level)
    {
        return
        [
            Current(BranchCode.FH, level),
            Current(BranchCode.FS, level),
            Current(BranchCode.WM, level),
            Current(BranchCode.IR, level),
            Current(BranchCode.DE, level),
            Current(BranchCode.CO, level),
            Current(BranchCode.AI, level),
            Current(BranchCode.TI, level),
        ];
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
            DaysSinceLastPassingCheck: state == MaintenanceCurrencyState.Current ? 3 : 11,
            ConsecutiveFailures: 0);
    }

    private static void AssertDay(
        WeeklyPlan plan,
        int dayNumber,
        WeeklySessionKind session,
        params BranchCode[] branchEmphasis)
    {
        var day = plan.Days.Single(item => item.DayNumber == dayNumber);

        Assert.Equal(session, day.Session);
        Assert.Equal(branchEmphasis, day.BranchEmphasis);
    }
}
