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
    public async Task SelectsActiveTrainingBeforeAnEarlierOwnedBranch()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            [
                Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
                Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
            ]);
        await SavePassingMaintenanceAsync(
            configuration,
            BranchCode.FH,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 5));

        var selection = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(TrainingDate.From(2026, 7, 5)));

        Assert.Equal(NextTrainingWorkSelectionKind.Allowed, selection.Kind);
        Assert.NotNull(selection.SelectedWork);
        Assert.Equal(BranchCode.FS, selection.SelectedWork.Branch);
        Assert.Equal(GlobalLevelId.L1, selection.SelectedWork.Level);
        Assert.Equal(DrillId.FS1CueSwitch, selection.SelectedWork.Drill);
        Assert.Equal(AppTrainingSessionType.Practice, selection.SelectedWork.SessionType);
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
    public async Task KeepsFormalMaintenanceForTheExecutableFocusHoldStandard()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            [Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned)]);

        var selection = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(TrainingDate.From(2026, 7, 5)));

        Assert.Equal(NextTrainingWorkSelectionKind.MaintenanceNeeded, selection.Kind);
        Assert.NotNull(selection.SelectedWork);
        Assert.Equal(DrillId.FH1TargetHold, selection.SelectedWork.Drill);
        Assert.Equal(AppTrainingSessionType.Maintenance, selection.SelectedWork.SessionType);
    }

    [Theory]
    [InlineData(BranchCode.FH, GlobalLevelId.L3, DrillId.FH2DistractorHold)]
    [InlineData(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter)]
    [InlineData(BranchCode.WM, GlobalLevelId.L3, DrillId.WM2MentalTransform)]
    [InlineData(BranchCode.IR, GlobalLevelId.L2, DrillId.IR2ExceptionRule)]
    [InlineData(BranchCode.DE, GlobalLevelId.L3, DrillId.DE2SeededAudit)]
    [InlineData(BranchCode.CO, GlobalLevelId.L3, DrillId.CO2StructureMapping)]
    [InlineData(BranchCode.AI, GlobalLevelId.L3, DrillId.AI2DisruptionRecovery)]
    [InlineData(BranchCode.TI, GlobalLevelId.L5, DrillId.TI2GlobalReviewTask)]
    public async Task SelectsSecondProtocolWhenLevelDemandRequiresIt(
        BranchCode branch,
        GlobalLevelId level,
        DrillId expectedDrill)
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            [Status(branch, level, BranchLevelState.Maintenance)]);

        var selection = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(TrainingDate.From(2026, 7, 5)));

        Assert.Equal(NextTrainingWorkSelectionKind.MaintenanceNeeded, selection.Kind);
        Assert.NotNull(selection.SelectedWork);
        Assert.Equal(expectedDrill, selection.SelectedWork!.Drill);

        var prepared = await new PreUiTrainingWorkflowService(configuration)
            .PrepareNextSessionWithDefaultsAsync(
                new PreUiTrainingWorkflowDefaultPreparationRequest(
                    new NextTrainingWorkSelectionQuery(TrainingDate.From(2026, 7, 5))));

        Assert.True(
            prepared.Status == PreUiTrainingWorkflowPreparationStatus.Prepared,
            $"{prepared.Status}: {string.Join(" | ", prepared.Rejections.Select(rejection => rejection.Detail))}");
        Assert.True(prepared.CanStartRuntimeSession);
        Assert.Equal(expectedDrill, prepared.RuntimeSession!.SessionDefinition!.Drill);
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

    private static ValueTask SavePassingMaintenanceAsync(
        AppStartupConfiguration configuration,
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date)
    {
        return new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions).SaveMaintenanceAsync(
            new LocalMaintenanceCheckRecord(
                $"maintenance-{branch}-{level}",
                $"artifact-{branch}-{level}",
                completedSessionId: null,
                DrillId.FH1TargetHold,
                "The stated standard remained visible before the check.",
                new MaintenanceCheckEvidence(
                    branch,
                    level,
                    date,
                    MaintenanceCheckKind.StandardOrTransfer,
                    new StandardEvaluationResult(Passed: true, Failures: []))));
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
