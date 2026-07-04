using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class TestReadinessEvaluationTests
{
    private const string FocusShiftL3Demand = "FS-L3 invalid-cue conflict demand";

    [Fact]
    public void AllowsFormalTestWhenAllReadinessConditionsAreMet()
    {
        var request = FocusShiftL3Request(
            [
                Status(BranchCode.FS, GlobalLevelId.L2, BranchLevelState.Owned),
                Status(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Owned),
            ],
            [
                CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
                CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
            ],
            [
                CurrentMaintenance(BranchCode.FS, GlobalLevelId.L2),
                CurrentMaintenance(BranchCode.IR, GlobalLevelId.L2),
            ]);

        var result = TestReadinessEvaluator.Evaluate(request);

        Assert.True(result.MayTest);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void RequiresDocumentedPrerequisiteOwnership()
    {
        var request = FocusShiftL3Request(
            [
                Status(BranchCode.FS, GlobalLevelId.L2, BranchLevelState.Owned),
                Status(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Training),
            ],
            [
                CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
                CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
            ],
            [
                CurrentMaintenance(BranchCode.FS, GlobalLevelId.L2),
                CurrentMaintenance(BranchCode.IR, GlobalLevelId.L2),
            ]);

        var result = TestReadinessEvaluator.Evaluate(request);

        Assert.False(result.MayTest);
        Assert.Contains(result.Failures, failure => failure.Kind == TestReadinessFailureKind.PrerequisiteNotOwned);
    }

    [Fact]
    public void RequiresTwoRecentCleanPracticeSessionsOnSameDrillDemand()
    {
        var request = FocusShiftL3Request(
            [
                Status(BranchCode.FS, GlobalLevelId.L2, BranchLevelState.Owned),
                Status(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Owned),
            ],
            [
                CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
                CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, "different demand"),
                DirtyPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
            ],
            [
                CurrentMaintenance(BranchCode.FS, GlobalLevelId.L2),
                CurrentMaintenance(BranchCode.IR, GlobalLevelId.L2),
            ]);

        var result = TestReadinessEvaluator.Evaluate(request);

        Assert.False(result.MayTest);
        Assert.Contains(result.Failures, failure => failure.Kind == TestReadinessFailureKind.RecentCleanPracticeMissing);
    }

    [Fact]
    public void RequiresCurrentMaintenanceForOwnedPrerequisites()
    {
        var request = FocusShiftL3Request(
            [
                Status(BranchCode.FS, GlobalLevelId.L2, BranchLevelState.Owned),
                Status(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Owned),
            ],
            [
                CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
                CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
            ],
            [
                CurrentMaintenance(BranchCode.FS, GlobalLevelId.L2),
                OverdueMaintenance(BranchCode.IR, GlobalLevelId.L2),
            ]);

        var result = TestReadinessEvaluator.Evaluate(request);

        Assert.False(result.MayTest);
        Assert.Contains(result.Failures, failure => failure.Kind == TestReadinessFailureKind.PrerequisiteMaintenanceOverdue);
    }

    [Fact]
    public void RequiresStatedStandardAndNamedHonestyConstraintToMatchProgramCatalog()
    {
        var request = FocusShiftL3Request(
            [
                Status(BranchCode.FS, GlobalLevelId.L2, BranchLevelState.Owned),
                Status(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Owned),
            ],
            [
                CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
                CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
            ],
            [
                CurrentMaintenance(BranchCode.FS, GlobalLevelId.L2),
                CurrentMaintenance(BranchCode.IR, GlobalLevelId.L2),
            ],
            statedStandard: "I will do the task well.",
            namedHonestyConstraint: "Try to stay focused.");

        var result = TestReadinessEvaluator.Evaluate(request);

        Assert.False(result.MayTest);
        Assert.Contains(result.Failures, failure => failure.Kind == TestReadinessFailureKind.StandardNotStated);
        Assert.Contains(result.Failures, failure => failure.Kind == TestReadinessFailureKind.HonestyConstraintNotNamed);
    }

    [Fact]
    public void AllowsAdvancedTestWhenAnyDocumentedAdvancedPrerequisiteOptionIsOwnedAndMaintained()
    {
        const string transferIntegrationDemand = "TI-L1 two-branch composite demand";
        var request = new TestReadinessRequest(
            new PractitionerState(
            [
                Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Owned),
                Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Owned),
                Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Owned),
                Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Owned),
                Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Owned),
                Status(BranchCode.CO, GlobalLevelId.L2, BranchLevelState.Owned),
                Status(BranchCode.AI, GlobalLevelId.L2, BranchLevelState.Training),
            ]),
            BranchCode.TI,
            GlobalLevelId.L1,
            DrillId.TI1CompositeTask,
            transferIntegrationDemand,
            [
                CleanPractice(BranchCode.TI, GlobalLevelId.L1, DrillId.TI1CompositeTask, transferIntegrationDemand),
                CleanPractice(BranchCode.TI, GlobalLevelId.L1, DrillId.TI1CompositeTask, transferIntegrationDemand),
            ],
            [
                CurrentMaintenance(BranchCode.FH, GlobalLevelId.L3),
                CurrentMaintenance(BranchCode.FS, GlobalLevelId.L3),
                CurrentMaintenance(BranchCode.WM, GlobalLevelId.L3),
                CurrentMaintenance(BranchCode.IR, GlobalLevelId.L3),
                CurrentMaintenance(BranchCode.DE, GlobalLevelId.L3),
                CurrentMaintenance(BranchCode.CO, GlobalLevelId.L2),
            ],
            StandardFor(BranchCode.TI, GlobalLevelId.L1),
            HonestyConstraintFor(DrillId.TI1CompositeTask));

        var result = TestReadinessEvaluator.Evaluate(request);

        Assert.True(result.MayTest);
        Assert.Empty(result.Failures);
    }

    private static TestReadinessRequest FocusShiftL3Request(
        IEnumerable<BranchLevelStatus> branchLevels,
        IEnumerable<TestReadinessPracticeSession> recentPracticeSessions,
        IEnumerable<PrerequisiteMaintenanceCheck> maintenanceChecks,
        string? statedStandard = null,
        string? namedHonestyConstraint = null)
    {
        return new TestReadinessRequest(
            new PractitionerState(branchLevels),
            BranchCode.FS,
            GlobalLevelId.L3,
            DrillId.FS2InvalidCueFilter,
            FocusShiftL3Demand,
            recentPracticeSessions,
            maintenanceChecks,
            statedStandard ?? StandardFor(BranchCode.FS, GlobalLevelId.L3),
            namedHonestyConstraint ?? HonestyConstraintFor(DrillId.FS2InvalidCueFilter));
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static TestReadinessPracticeSession CleanPractice(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        string demand)
    {
        return new TestReadinessPracticeSession(branch, level, drill, demand, clean: true);
    }

    private static TestReadinessPracticeSession DirtyPractice(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        string demand)
    {
        return new TestReadinessPracticeSession(branch, level, drill, demand, clean: false);
    }

    private static PrerequisiteMaintenanceCheck CurrentMaintenance(BranchCode branch, GlobalLevelId level)
    {
        return new PrerequisiteMaintenanceCheck(branch, level, isCurrent: true);
    }

    private static PrerequisiteMaintenanceCheck OverdueMaintenance(BranchCode branch, GlobalLevelId level)
    {
        return new PrerequisiteMaintenanceCheck(branch, level, isCurrent: false);
    }

    private static string StandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards
            .Single(standard => standard.Branch == branch && standard.Level == level)
            .Standard;
    }

    private static string HonestyConstraintFor(DrillId drill)
    {
        return ProgramCatalog.Drills.Single(definition => definition.Id == drill).HonestyConstraint;
    }
}
