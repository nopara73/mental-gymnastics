using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class DailyTrainingWorkflowServiceTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreatesOneStablePrescriptionPerDateWithoutMissedDayBacklog()
    {
        var service = new DailyTrainingWorkflowService(Configuration());
        var firstDate = TrainingDate.From(2026, 7, 6);

        var first = await service.LoadOrCreateAsync(firstDate);
        var sameDate = await service.LoadOrCreateAsync(firstDate);
        var nextDate = await service.LoadOrCreateAsync(TrainingDate.From(2026, 7, 7));
        var offDate = await service.LoadOrCreateAsync(TrainingDate.From(2026, 7, 12));

        Assert.Equal(first.Prescription.PrescriptionId, sameDate.Prescription.PrescriptionId);
        Assert.Equal(1, first.Prescription.CycleDay);
        Assert.Equal(2, nextDate.Prescription.CycleDay);
        Assert.Equal(7, offDate.Prescription.CycleDay);
        Assert.Equal(DailyTrainingWorkflowStatus.OffDay, offDate.Status);
        Assert.Empty(offDate.Blocks);
    }

    [Fact]
    public async Task PrescribesEveryOpenBranchEmphasisAsAnOrderedDailyBlock()
    {
        var configuration = Configuration();
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
                new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
                new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training),
            ]));

        var daily = await new DailyTrainingWorkflowService(configuration).LoadOrCreateAsync(
            TrainingDate.From(2026, 7, 6));

        Assert.Equal(DailyTrainingWorkflowStatus.Ready, daily.Status);
        Assert.Equal([BranchCode.FH, BranchCode.FS, BranchCode.WM],
            daily.Blocks.Select(block => block.Record.Branch));
        Assert.Equal([1, 2, 3], daily.Blocks.Select(block => block.Record.Order));
        Assert.All(daily.Blocks, block => Assert.Equal(
            LocalDailyTrainingBlockRole.Practice,
            block.Record.Role));
        Assert.Contains(daily.Blocks[0].Record.LoadVariables, load =>
            load.Name == "duration" && load.Value == "2 minutes");
        Assert.Contains(daily.Blocks[1].Record.LoadVariables, load =>
            load.Name == "switch count" && load.Value == "3");
        Assert.Contains(daily.Blocks[2].Record.LoadVariables, load =>
            load.Name == "item count" && load.Value == "4");
    }

    [Fact]
    public async Task CancellingSetupLeavesTheSameBlockAvailableWithoutConsumingIt()
    {
        var service = new DailyTrainingWorkflowService(Configuration());
        var date = TrainingDate.From(2026, 7, 6);
        var daily = await service.LoadOrCreateAsync(date);
        var block = Assert.IsType<DailyTrainingBlockReadModel>(daily.CurrentBlock);

        var prepared = await service.MarkPreparedAsync(
            date,
            block.Record.BlockId,
            "session-first-setup");
        var cancelled = await service.CancelPreparedAsync("session-first-setup");

        Assert.Equal(DailyTrainingWorkflowStatus.Prepared, prepared.Status);
        Assert.Equal(DailyTrainingWorkflowStatus.Ready, cancelled.Status);
        Assert.Equal(block.Record.BlockId, cancelled.CurrentBlock?.Record.BlockId);
        Assert.Equal(LocalDailyTrainingBlockState.Planned, cancelled.CurrentBlock?.Record.State);
        Assert.Null(cancelled.CurrentBlock?.Record.SessionId);
        Assert.Equal(0, cancelled.CompletedBlockCount);
    }

    [Fact]
    public async Task ActiveWorkCannotBeRepreparedOrSilentlySkipped()
    {
        var service = new DailyTrainingWorkflowService(Configuration());
        var date = TrainingDate.From(2026, 7, 6);
        var daily = await service.LoadOrCreateAsync(date);
        var block = Assert.IsType<DailyTrainingBlockReadModel>(daily.CurrentBlock);
        await service.MarkPreparedAsync(date, block.Record.BlockId, "session-active");

        var active = await service.MarkActiveAsync("session-active");

        Assert.Equal(DailyTrainingWorkflowStatus.Active, active.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.MarkPreparedAsync(date, block.Record.BlockId, "session-duplicate"));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.StopRemainingAsync(date));
    }

    [Fact]
    public async Task ExplicitStopConsumesEveryUnstartedBlockForTheDate()
    {
        var configuration = Configuration();
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
                new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
                new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training),
            ]));
        var service = new DailyTrainingWorkflowService(configuration);
        var date = TrainingDate.From(2026, 7, 6);
        _ = await service.LoadOrCreateAsync(date);

        var stopped = await service.StopRemainingAsync(date);

        Assert.Equal(DailyTrainingWorkflowStatus.Stopped, stopped.Status);
        Assert.Equal(3, stopped.SkippedBlockCount);
        Assert.All(stopped.Blocks, block => Assert.Equal(
            LocalDailyTrainingBlockState.Skipped,
            block.Record.State));
        Assert.False(stopped.CanPrepare);
    }

    [Fact]
    public async Task TwoCleanExposuresIncreaseExactlyOneNamedLoadVariable()
    {
        var configuration = Configuration();
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.FH, GlobalLevelId.L1);
        var history = new LocalSessionHistoryStore(configuration.LocalDatabaseOptions);
        await history.SaveAsync(Session(
            "clean-one",
            TrainingDate.From(2026, 7, 4),
            profile.Stages[0].LoadVariables));
        await history.SaveAsync(Session(
            "clean-two",
            TrainingDate.From(2026, 7, 5),
            profile.Stages[0].LoadVariables));

        var daily = await new DailyTrainingWorkflowService(configuration).LoadOrCreateAsync(
            TrainingDate.From(2026, 7, 6));

        var block = Assert.Single(daily.Blocks, candidate =>
            candidate.Record.Branch == BranchCode.FH &&
            candidate.Record.Level == GlobalLevelId.L1).Record;
        Assert.Contains(block.LoadVariables, load => load.Name == "duration" && load.Value == "3 minutes");
        Assert.Equal(
            profile.TargetStage.LoadVariables.Where(target =>
                profile.Stages[0].LoadVariables.Any(previous =>
                    previous.Name == target.Name && previous.Value != target.Value)).Select(load => load.Name),
            ["duration"]);
    }

    [Fact]
    public async Task DueBeginnerReviewBecomesTheDailyActionAndResetsCadenceWhenRecorded()
    {
        var configuration = Configuration();
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
            ]));
        var service = new DailyTrainingWorkflowService(configuration);
        _ = await service.LoadOrCreateAsync(TrainingDate.From(2026, 1, 1));
        var date = TrainingDate.From(2026, 2, 12);

        var due = await service.LoadOrCreateAsync(date);

        var reviewBlock = Assert.Single(due.Blocks);
        Assert.Equal(LocalDailyTrainingBlockRole.Review, reviewBlock.Record.Role);
        Assert.Equal(DailyTrainingWorkflowStatus.Ready, due.Status);

        var completed = await service.CompleteDueGlobalReviewAsync(
            date,
            reviewBlock.Record.BlockId);

        Assert.Equal(DailyTrainingWorkflowStatus.Done, completed.DailyTraining.Status);
        Assert.Equal(EvidenceArtifactCategory.GlobalReview, completed.EvidenceArtifact.Artifact.Category);
        Assert.Contains(
            completed.EvidenceArtifact.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.GlobalReviewSummary);
        var refreshed = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(date));
        Assert.False(refreshed.GlobalReview.Cadence.IsDue);
        Assert.Equal(date, refreshed.GlobalReview.Cadence.AnchorDate);
        Assert.NotNull(refreshed.LastCompletedGlobalReview);
        Assert.Equal(completed.GlobalReview.Evaluation.Passed, refreshed.LastCompletedGlobalReview!.Passed);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteDueGlobalReviewAsync(date, reviewBlock.Record.BlockId).AsTask());
    }

    [Fact]
    public async Task PersistedOverloadMakesRecoveryTheDailyPrescription()
    {
        var configuration = Configuration();
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
            ]));
        var history = new LocalSessionHistoryStore(configuration.LocalDatabaseOptions);
        var loads = TrainingLoadProfileCatalog.Get(BranchCode.FH, GlobalLevelId.L1).Stages[0].LoadVariables;
        await history.SaveAsync(FailedLoadSession("overload-a", TrainingDate.From(2026, 7, 5), loads));
        await history.SaveAsync(FailedLoadSession("overload-b", TrainingDate.From(2026, 7, 5), loads));

        var daily = await new DailyTrainingWorkflowService(configuration).LoadOrCreateAsync(
            TrainingDate.From(2026, 7, 6));

        var block = Assert.Single(daily.Blocks);
        Assert.Equal(LocalDailyTrainingBlockRole.Recovery, block.Record.Role);
        Assert.Equal(AppTrainingSessionType.Recovery, block.SessionType);
    }

    [Fact]
    public async Task RecoveryOrRetestFindsPendingStabilizationOutsideDayEmphasis()
    {
        var configuration = Configuration();
        var startDate = TrainingDate.From(2026, 7, 6);
        await SaveIntermediateStateAsync(
            configuration,
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L4, BranchLevelState.PassedOnce),
            startDate);
        var service = new DailyTrainingWorkflowService(configuration);
        _ = await service.LoadOrCreateAsync(startDate);

        var daily = await service.LoadOrCreateAsync(TrainingDate.From(2026, 7, 11));

        var block = Assert.Single(daily.Blocks);
        Assert.Equal(WeeklySessionKind.RecoveryOrRetest, daily.Prescription.WeeklySession);
        Assert.Equal(BranchCode.FH, block.Record.Branch);
        Assert.Equal(GlobalLevelId.L4, block.Record.Level);
        Assert.Equal(LocalDailyTrainingBlockRole.Stabilization, block.Record.Role);
        Assert.Equal(AppTrainingSessionType.Stabilization, block.SessionType);
    }

    [Fact]
    public async Task RecoveryOrRetestUsesTransferAsTheL4FormalGate()
    {
        var configuration = Configuration();
        var startDate = TrainingDate.From(2026, 7, 6);
        await SaveIntermediateStateAsync(
            configuration,
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L4, BranchLevelState.TestReady),
            startDate);
        var service = new DailyTrainingWorkflowService(configuration);
        _ = await service.LoadOrCreateAsync(startDate);

        var daily = await service.LoadOrCreateAsync(TrainingDate.From(2026, 7, 11));

        var block = Assert.Single(daily.Blocks);
        Assert.Equal(WeeklySessionKind.RecoveryOrRetest, daily.Prescription.WeeklySession);
        Assert.Equal(BranchCode.FH, block.Record.Branch);
        Assert.Equal(GlobalLevelId.L4, block.Record.Level);
        Assert.Equal(LocalDailyTrainingBlockRole.Transfer, block.Record.Role);
        Assert.Equal(AppTrainingSessionType.Transfer, block.SessionType);
    }

    [Fact]
    public async Task DueMaintenanceIsCappedAtThreeBlocksForOneDailyDose()
    {
        var configuration = Configuration();
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
                new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Maintenance),
                new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Maintenance),
                new BranchLevelStatus(BranchCode.IR, GlobalLevelId.L1, BranchLevelState.Maintenance),
                new BranchLevelStatus(BranchCode.DE, GlobalLevelId.L1, BranchLevelState.Maintenance),
            ]));

        var daily = await new DailyTrainingWorkflowService(configuration).LoadOrCreateAsync(
            TrainingDate.From(2026, 7, 6));

        Assert.Equal(3, daily.Blocks.Count);
        Assert.Equal(3, daily.Blocks.Select(block => block.Record.Branch).Distinct().Count());
        Assert.All(daily.Blocks, block =>
        {
            Assert.Equal(LocalDailyTrainingBlockRole.Maintenance, block.Record.Role);
            Assert.Equal(AppTrainingSessionType.Maintenance, block.SessionType);
        });
    }

    [Fact]
    public async Task StartupReconciliationResetsPreparedSetupWithoutRecordingAttempt()
    {
        var configuration = Configuration();
        var dailyService = new DailyTrainingWorkflowService(configuration);
        var date = TrainingDate.From(2026, 7, 6);
        var daily = await dailyService.LoadOrCreateAsync(date);
        var block = Assert.IsType<DailyTrainingBlockReadModel>(daily.CurrentBlock);
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(date, block.RequestedWork),
                "prepared-crash"));
        Assert.True(prepared.IsPrepared);
        await dailyService.MarkPreparedAsync(
            date,
            block.Record.BlockId,
            prepared.RuntimeSession!.SessionId);

        var reconciled = await dailyService.ReconcileInterruptedStateAsync(date);

        Assert.Equal(
            DailyTrainingInterruptionReconciliationStatus.PreparedReset,
            reconciled.Status);
        Assert.Equal(DailyTrainingWorkflowStatus.Ready, reconciled.DailyTraining!.Status);
        Assert.Equal(
            LocalDailyTrainingBlockState.Planned,
            reconciled.DailyTraining.CurrentBlock!.Record.State);
        Assert.Null(await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.RuntimeSession.SessionId));
        var generated = await new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.GeneratedInstanceRecord!.InstanceId);
        Assert.Equal(LocalGeneratedDrillInstanceState.Abandoned, generated!.State);
    }

    [Fact]
    public async Task StartupReconciliationRecordsMissingActiveSnapshotAsAbandonedAttempt()
    {
        var configuration = Configuration();
        var service = new DailyTrainingWorkflowService(configuration);
        var date = TrainingDate.From(2026, 7, 6);
        var daily = await service.LoadOrCreateAsync(date);
        var block = Assert.IsType<DailyTrainingBlockReadModel>(daily.CurrentBlock);
        await service.MarkPreparedAsync(date, block.Record.BlockId, "missing-active-snapshot");
        await service.MarkActiveAsync("missing-active-snapshot");

        var reconciled = await service.ReconcileInterruptedStateAsync(date);

        Assert.Equal(
            DailyTrainingInterruptionReconciliationStatus.Abandoned,
            reconciled.Status);
        Assert.Equal(DailyTrainingWorkflowStatus.Stopped, reconciled.DailyTraining!.Status);
        Assert.Equal(
            LocalDailyTrainingBlockState.Abandoned,
            reconciled.DailyTraining.Blocks[0].Record.State);
        Assert.All(
            reconciled.DailyTraining.Blocks.Skip(1),
            remaining => Assert.Equal(LocalDailyTrainingBlockState.Skipped, remaining.Record.State));
        var session = await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions)
            .LoadAsync("missing-active-snapshot");
        Assert.NotNull(session);
        Assert.False(session.CleanPerformance);
        Assert.Single(session.EvidenceArtifactIds);
        Assert.NotNull(await new LocalEvidenceArtifactStore(configuration.LocalDatabaseOptions)
            .LoadAsync(session.EvidenceArtifactIds[0]));
    }

    [Fact]
    public async Task StartupReconciliationKeepsActiveWorkWithSnapshotResumable()
    {
        var configuration = Configuration();
        var service = new DailyTrainingWorkflowService(configuration);
        var date = TrainingDate.From(2026, 7, 6);
        var daily = await service.LoadOrCreateAsync(date);
        var block = Assert.IsType<DailyTrainingBlockReadModel>(daily.CurrentBlock);
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(date, block.RequestedWork),
                "active-crash"));
        await service.MarkPreparedAsync(
            date,
            block.Record.BlockId,
            prepared.RuntimeSession!.SessionId);
        await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession,
                new ManualRuntimeClock(RuntimeInstant.Zero),
                saveActiveSnapshot: true));
        await service.MarkActiveAsync(prepared.RuntimeSession.SessionId);

        var reconciled = await service.ReconcileInterruptedStateAsync(date);

        Assert.Equal(
            DailyTrainingInterruptionReconciliationStatus.ActiveResumable,
            reconciled.Status);
        Assert.Equal(DailyTrainingWorkflowStatus.Active, reconciled.DailyTraining!.Status);
        Assert.Null(await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.RuntimeSession.SessionId));
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

    private static async ValueTask SaveIntermediateStateAsync(
        AppStartupConfiguration configuration,
        BranchLevelStatus pendingStatus,
        TrainingDate maintenanceDate)
    {
        var foundational = new[]
        {
            BranchCode.FH,
            BranchCode.FS,
            BranchCode.WM,
            BranchCode.IR,
            BranchCode.DE,
        };
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(
            new PractitionerState(
                foundational.Select(branch => new BranchLevelStatus(
                        branch,
                        GlobalLevelId.L3,
                        BranchLevelState.Maintenance))
                    .Append(pendingStatus)
                    .Append(new BranchLevelStatus(
                        BranchCode.CO,
                        GlobalLevelId.L1,
                        BranchLevelState.Training))));

        var maintenanceStore = new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions);
        foreach (var branch in foundational)
        {
            await maintenanceStore.SaveMaintenanceAsync(new LocalMaintenanceCheckRecord(
                $"maintenance-{branch}-l3",
                $"artifact-maintenance-{branch}-l3",
                completedSessionId: null,
                DrillId.FH1TargetHold,
                "The current L3 standard remained visible during the check.",
                new MaintenanceCheckEvidence(
                    branch,
                    GlobalLevelId.L3,
                    maintenanceDate,
                    MaintenanceCheckKind.StandardOrTransfer,
                    new StandardEvaluationResult(Passed: true, Failures: []))));
        }
    }

    private static LocalSessionHistoryRecord Session(
        string sessionId,
        TrainingDate date,
        IReadOnlyList<LoadVariable> loadVariables)
    {
        return new LocalSessionHistoryRecord(
            sessionId,
            date,
            LocalCompletedSessionType.Practice,
            [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L1)],
            DrillId.FH1TargetHold,
            transferTask: null,
            LocalSessionIntensity.Moderate,
            loadVariables,
            cleanPerformance: true,
            notes: "Clean progressive-load exposure.",
            recoveryMarked: false,
            deloadMarked: false,
            evidenceArtifactIds: [$"artifact-{sessionId}"]);
    }

    private static LocalSessionHistoryRecord FailedLoadSession(
        string sessionId,
        TrainingDate date,
        IReadOnlyList<LoadVariable> loadVariables)
    {
        return new LocalSessionHistoryRecord(
            sessionId,
            date,
            LocalCompletedSessionType.Load,
            [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L1)],
            DrillId.FH1TargetHold,
            transferTask: null,
            LocalSessionIntensity.High,
            loadVariables,
            cleanPerformance: false,
            notes: "Observable overload set failure.",
            recoveryMarked: false,
            deloadMarked: false,
            evidenceArtifactIds: [$"artifact-{sessionId}"]);
    }
}
