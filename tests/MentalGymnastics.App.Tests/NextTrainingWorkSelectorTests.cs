using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App.Tests;

public sealed class NextTrainingWorkSelectorTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SelectsAllowedPracticeFromCurrentLocalState()
    {
        var configuration = Configuration();

        var selection = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(TrainingDate.From(2026, 7, 5)));

        Assert.Equal(NextTrainingWorkSelectionKind.Allowed, selection.Kind);
        Assert.NotNull(selection.SelectedWork);
        Assert.Equal(BranchCode.FH, selection.SelectedWork.Branch);
        Assert.Equal(GlobalLevelId.L1, selection.SelectedWork.Level);
        Assert.Equal(DrillId.FH1TargetHold, selection.SelectedWork.Drill);
        Assert.Equal(AppTrainingSessionType.Practice, selection.SelectedWork.SessionType);
        Assert.Equal(StandardFor(BranchCode.FH, GlobalLevelId.L1), selection.SelectedWork.Standard);
        Assert.Equal(HonestyConstraintFor(DrillId.FH1TargetHold), selection.SelectedWork.HonestyConstraint);
        Assert.Empty(selection.Blockers);
    }

    [Fact]
    public async Task BlocksRequestedAdvancedWorkWhenPrerequisitesAndReadinessFail()
    {
        var configuration = Configuration();
        var requested = new RequestedTrainingWork(
            BranchCode.CO,
            GlobalLevelId.L1,
            DrillId.CO1RuleExtraction,
            AppTrainingSessionType.Test);

        var selection = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(
                TrainingDate.From(2026, 7, 5),
                requested));

        Assert.Equal(NextTrainingWorkSelectionKind.Blocked, selection.Kind);
        Assert.Null(selection.SelectedWork);
        Assert.Contains(
            selection.Blockers,
            blocker => blocker.Source == NextTrainingWorkBlockerSource.TestReadiness &&
                blocker.TestReadinessFailureKind == TestReadinessFailureKind.PrerequisiteNotOwned);
        Assert.Contains(
            selection.Blockers,
            blocker => blocker.Source == NextTrainingWorkBlockerSource.TestReadiness &&
                blocker.TestReadinessFailureKind == TestReadinessFailureKind.RecentCleanPracticeMissing);
    }

    [Fact]
    public async Task RoutesToMaintenanceBeforeAdvancementWorkWhenMaintenanceIsDue()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            [
                Status(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance),
                Status(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training),
            ]);

        var selection = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(
                TrainingDate.From(2026, 7, 5),
                new RequestedTrainingWork(
                    BranchCode.WM,
                    GlobalLevelId.L1,
                    DrillId.WM1DelayedReconstruction,
                    AppTrainingSessionType.Load)));

        Assert.Equal(NextTrainingWorkSelectionKind.MaintenanceNeeded, selection.Kind);
        Assert.NotNull(selection.SelectedWork);
        Assert.Equal(BranchCode.FH, selection.SelectedWork.Branch);
        Assert.Equal(GlobalLevelId.L2, selection.SelectedWork.Level);
        Assert.Equal(AppTrainingSessionType.Maintenance, selection.SelectedWork.SessionType);
        Assert.Contains(
            selection.Blockers,
            blocker => blocker.Source == NextTrainingWorkBlockerSource.MaintenanceCurrency &&
                blocker.MaintenanceCurrencyState == MaintenanceCurrencyState.Due);
    }

    [Fact]
    public async Task AllowsStabilizationOnlyForPassedOnceOrStabilizingBranchLevel()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            [Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce)]);

        var allowed = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(
                TrainingDate.From(2026, 7, 5),
                new RequestedTrainingWork(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    DrillId.FH1TargetHold,
                    AppTrainingSessionType.Stabilization)));

        Assert.Equal(NextTrainingWorkSelectionKind.Allowed, allowed.Kind);
        Assert.Equal(AppTrainingSessionType.Stabilization, allowed.SelectedWork?.SessionType);

        await SaveStateAsync(
            configuration,
            [Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training)]);

        var blocked = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(
                TrainingDate.From(2026, 7, 5),
                new RequestedTrainingWork(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    DrillId.FH1TargetHold,
                    AppTrainingSessionType.Stabilization)));

        Assert.Equal(NextTrainingWorkSelectionKind.Blocked, blocked.Kind);
        Assert.Contains(
            blocked.Blockers,
            blocker => blocker.Source == NextTrainingWorkBlockerSource.BranchState);
    }

    [Fact]
    public async Task BlocksTransferWhenCoreTransferEligibilityRejectsTheCandidate()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            [Status(BranchCode.FS, GlobalLevelId.L4, BranchLevelState.Training)]);

        var selection = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(
                TrainingDate.From(2026, 7, 5),
                new RequestedTrainingWork(
                    BranchCode.FS,
                    GlobalLevelId.L4,
                    DrillId.WM1DelayedReconstruction,
                    AppTrainingSessionType.Transfer)));

        Assert.Equal(NextTrainingWorkSelectionKind.Blocked, selection.Kind);
        Assert.Contains(
            selection.Blockers,
            blocker => blocker.Source == NextTrainingWorkBlockerSource.TransferEligibility &&
                blocker.TransferEligibilityFailureKind == TransferEligibilityFailureKind.TrainedCapacityNotInSourceBranch);
    }

    [Fact]
    public async Task RecoveryEvidenceSelectsRecoveryAndSuspendsAdvancement()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            [Status(BranchCode.WM, GlobalLevelId.L2, BranchLevelState.Training)]);

        var selection = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(
                TrainingDate.From(2026, 7, 6),
                new RequestedTrainingWork(
                    BranchCode.WM,
                    GlobalLevelId.L2,
                    DrillId.WM1DelayedReconstruction,
                    AppTrainingSessionType.Load),
                RecoveryEvidence: RecoveryRequest()));

        Assert.Equal(NextTrainingWorkSelectionKind.Recovery, selection.Kind);
        Assert.NotNull(selection.SelectedWork);
        Assert.Equal(AppTrainingSessionType.Recovery, selection.SelectedWork.SessionType);
        Assert.Equal(BranchCode.WM, selection.SelectedWork.Branch);
        Assert.NotNull(selection.RecoveryDecision);
        Assert.True(selection.RecoveryDecision.ShouldRecover);
        Assert.False(selection.SelectedWork.AdvancementWorkAllowed);
        Assert.Contains(
            RecoveryTriggerKind.TwoConsecutiveOverloadSetFailures,
            selection.RecoveryDecision.Triggers);
    }

    [Fact]
    public async Task DeloadEvidenceSelectsDeloadAndSuspendsAdvancementTesting()
    {
        var configuration = Configuration();
        var weekStart = TrainingDate.From(2026, 7, 6);

        var selection = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(
                TrainingDate.From(2026, 7, 6),
                DeloadEvidence: new DeloadDecisionRequest(
                    weekStart,
                    [
                        new DeloadBranchWeekEvidence(BranchCode.WM, weekStart, overloadObserved: true, decayObserved: false),
                        new DeloadBranchWeekEvidence(BranchCode.IR, weekStart, overloadObserved: false, decayObserved: true),
                    ],
                    subjectiveNotes: [])));

        Assert.Equal(NextTrainingWorkSelectionKind.Deload, selection.Kind);
        Assert.NotNull(selection.SelectedWork);
        Assert.Equal(AppTrainingSessionType.Recovery, selection.SelectedWork.SessionType);
        Assert.False(selection.SelectedWork.AdvancementWorkAllowed);
        Assert.NotNull(selection.DeloadDecision);
        Assert.True(selection.DeloadDecision.ShouldDeload);
        Assert.False(selection.DeloadDecision.Prescription?.AdvancementTestingAllowed);
        Assert.True(selection.DeloadDecision.Prescription?.MaintenanceChecksRemain);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private AppStartupConfiguration Configuration()
    {
        return AppStartupConfiguration.ForAppOwnedLocalStoragePath(
            Path.Combine(tempDirectory, "mental-gymnastics.json"));
    }

    private static ValueTask SaveStateAsync(
        AppStartupConfiguration configuration,
        IEnumerable<BranchLevelStatus> statuses)
    {
        return new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(
            new PractitionerState(statuses));
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static RecoveryDecisionRequest RecoveryRequest()
    {
        return new RecoveryDecisionRequest(
            BranchCode.WM,
            GlobalLevelId.L2,
            LoadVariableKind.ItemCount,
            "no invented items",
            setResults:
            [
                new RecoverySetResultEvidence(
                    BranchCode.WM,
                    GlobalLevelId.L2,
                    TrainingDate.From(2026, 7, 6),
                    setNumber: 1,
                    failedFromOverload: true),
                new RecoverySetResultEvidence(
                    BranchCode.WM,
                    GlobalLevelId.L2,
                    TrainingDate.From(2026, 7, 6),
                    setNumber: 2,
                    failedFromOverload: true),
            ],
            errorTrends: [],
            honestyConstraintEvidence: [],
            adjacentBranchDecayEvidence: [],
            recentHighIntensityTests: [],
            subjectiveNotes: []);
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
