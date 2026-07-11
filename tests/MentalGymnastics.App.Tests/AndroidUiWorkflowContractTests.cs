using MentalGymnastics.App;
using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class AndroidUiWorkflowContractTests : IDisposable
{
    private static readonly TrainingDate ScreenDate = TrainingDate.From(2026, 7, 5);

    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task HomeBranchProgressEvidenceAndMaintenanceScreensRenderAppReadModelsWithoutUiAdvancement()
    {
        var configuration = Configuration("android-state.json");
        await SaveStateAsync(
            configuration,
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce),
            Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Owned),
            Status(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.TestReady),
            Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.DE, GlobalLevelId.L2, BranchLevelState.Decayed),
            Status(BranchCode.CO, GlobalLevelId.L1, BranchLevelState.Training));
        await SaveMaintenanceAsync(configuration, "maintenance-wm-stale", BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 6, 20), passed: true);
        await SaveMaintenanceAsync(configuration, "maintenance-ir-current", BranchCode.IR, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1), passed: true);
        await SaveEvidenceAndSessionAsync(
            configuration,
            "android-evidence-fs-transfer",
            "android-session-fs-transfer",
            LocalProgrammingEventKind.Transfer,
            EvidenceArtifactCategory.Transfer,
            BranchCode.FS,
            GlobalLevelId.L1,
            DrillId.FS1CueSwitch,
            cleanPerformance: true);

        var readModel = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(
                ScreenDate,
                recentSessionLimit: 5,
                evidenceSummaryLimit: 5,
                progressRecordLimit: 5));

        var home = AndroidHomeContract.From(readModel);
        var ladder = AndroidBranchLadderContract.From(readModel);
        var progress = AndroidProgressContract.From(readModel);
        var evidence = AndroidEvidenceReviewContract.From(readModel);
        var maintenance = AndroidMaintenanceContract.From(readModel);

        Assert.Equal(readModel.AvailableNextWork.Count, home.NextWorkCount);
        Assert.Equal(readModel.DueMaintenance.Count, home.MaintenanceDueCount);
        Assert.True(home.HasBlockedAdvancement);
        Assert.True(home.HasRecoveryOrMaintenanceWork);
        Assert.False(home.GrantsAdvancementFromUi);

        var passedOnceMarker = ladder.MarkerFor(BranchCode.FH, GlobalLevelId.L1);
        var ownedMarker = ladder.MarkerFor(BranchCode.FS, GlobalLevelId.L1);
        Assert.Equal(BranchLevelState.PassedOnce, passedOnceMarker.State);
        Assert.Equal(BranchLevelState.Owned, ownedMarker.State);
        Assert.NotEqual(ownedMarker.Shape, passedOnceMarker.Shape);
        Assert.NotEqual(ownedMarker.Hierarchy, passedOnceMarker.Hierarchy);
        Assert.True(ladder.MarkerFor(BranchCode.DE, GlobalLevelId.L2).RequiresImmediateAttention);
        Assert.False(ladder.GrantsAdvancementFromUi);

        Assert.Equal(readModel.BranchLevelStates.Count(state => state.State == BranchLevelState.Owned), progress.OwnedLevelCount);
        Assert.Equal(readModel.BranchLevelStates.Count(state => state.State == BranchLevelState.PassedOnce), progress.PassedOnceCount);
        Assert.Contains(BranchCode.WM, progress.TestReadyBranches);
        Assert.Contains(BranchCode.DE, progress.DecayedBranches);
        Assert.False(progress.GrantsAdvancementFromUi);

        Assert.Contains(
            evidence.Artifacts,
            artifact => artifact.SessionId == "android-session-fs-transfer" &&
                artifact.Branch == BranchCode.FS &&
                artifact.Level == GlobalLevelId.L1 &&
                artifact.Drill == DrillId.FS1CueSwitch &&
                artifact.Category == EvidenceArtifactCategory.Transfer &&
                artifact.ObservableEvidenceKinds.Contains(ObservableEvidenceKind.Score) &&
                artifact.ObservableEvidenceKinds.Contains(ObservableEvidenceKind.CriticalConstraintRecord));
        Assert.All(evidence.Artifacts, artifact => Assert.True(artifact.HasObservableEvidence));
        Assert.False(evidence.GrantsAdvancementFromUi);

        Assert.Contains(maintenance.DueBranches, branch => branch == BranchCode.WM);
        Assert.Contains(BranchCode.DE, maintenance.DecayedBranches);
        Assert.True(maintenance.BlockedAdvancementCount > 0);
        Assert.False(maintenance.CanDismissDecayFromUi);
        Assert.False(maintenance.GrantsAdvancementFromUi);
    }

    [Fact]
    public async Task SessionStartConsumesPreparedAppOutputAndRejectsBlockedBypassPaths()
    {
        var configuration = Configuration("android-session-start.json");
        var workflow = new PreUiTrainingWorkflowService(configuration);

        var blocked = await workflow.PrepareNextSessionAsync(new PreUiTrainingWorkflowPreparationRequest(
            new NextTrainingWorkSelectionQuery(
                ScreenDate,
                new RequestedTrainingWork(
                    BranchCode.CO,
                    GlobalLevelId.L1,
                    DrillId.CO1RuleExtraction,
                    AppTrainingSessionType.Test)),
            PromptContentKind.RuleExampleSet,
            "co-l1-rule-extraction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            new GeneratedContentSeed("android-blocked-co-l1"),
            "android-blocked-session"));

        var blockedScreen = AndroidSessionStartContract.From(blocked);

        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Blocked, blocked.Status);
        Assert.False(blockedScreen.PrimaryActionEnabled);
        Assert.NotEmpty(blockedScreen.BlockerDetails);
        Assert.Null(blocked.GeneratedContent);
        Assert.Null(blocked.RuntimeSession);
        Assert.False(blockedScreen.GrantsAdvancementFromUi);

        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(ScreenDate),
                "android-session-start"));

        var screen = AndroidSessionStartContract.From(prepared);

        Assert.True(prepared.CanStartRuntimeSession);
        Assert.True(screen.PrimaryActionEnabled);
        Assert.Equal(prepared.Selection.SelectedWork!.Standard, screen.VisibleStandard);
        Assert.Equal(prepared.Selection.SelectedWork.HonestyConstraint, screen.VisibleHonestyConstraint);
        Assert.Equal(prepared.Selection.SelectedWork.LoadVariables.Count, screen.LockedLoadVariableCount);
        Assert.Equal(prepared.RuntimeSession!.ExpectedEvidenceFacts.Count, screen.RequiredEvidenceFactCount);
        Assert.Equal(prepared.GeneratedContent!.PersistenceHandoff!.InstanceId, screen.GeneratedContentInstanceId);
        Assert.Contains("standard", screen.LockedInputs);
        Assert.Contains("honesty-constraint", screen.LockedInputs);
        Assert.Contains("load", screen.LockedInputs);
        Assert.False(prepared.RuntimeSession.GrantsAdvancement);
        Assert.False(screen.GrantsAdvancementFromUi);
    }

    [Fact]
    public async Task LiveSessionForwardsRuntimeCommandsAndRejectsUnavailableBypassActions()
    {
        var configuration = Configuration("android-live.json");
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "android-live-cue");
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: true));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started);

        var initial = await controller.RefreshAsync();
        var initialScreen = AndroidLiveSessionContract.From(initial);

        Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, initialScreen.CurrentPhaseKind);
        Assert.Equal(
            initial.Commands.Where(command => command.IsAvailable).Select(command => command.Command),
            initialScreen.AvailableCommands);
        Assert.DoesNotContain(RuntimeInputCommandKind.RespondToCue, initialScreen.AvailableCommands);
        Assert.False(initialScreen.GrantsAdvancementFromUi);
        Assert.False(prepared.RuntimeSession!.OwnsTiming);
        Assert.False(prepared.RuntimeSession.OwnsCueScheduling);
        Assert.False(prepared.RuntimeSession.OwnsScoring);

        var rejectedCue = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.RespondToCue, value: "switch"));
        var rejectedScreen = AndroidLiveSessionContract.From(rejectedCue);

        Assert.False(rejectedCue.LastCommand!.IsAccepted);
        Assert.Equal(0, rejectedScreen.Evidence.CueResponseCount);
        Assert.False(rejectedScreen.GrantsAdvancementFromUi);

        var cuePhase = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.True(cuePhase.LastCommand!.IsAccepted);
        Assert.Equal(RuntimeSessionPhaseKind.CueResponse, cuePhase.CurrentPhaseKind);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var cued = await controller.RefreshAsync();
        var cuedScreen = AndroidLiveSessionContract.From(cued);

        Assert.NotNull(cued.ActiveCue);
        Assert.Contains(RuntimeInputCommandKind.RespondToCue, cuedScreen.AvailableCommands);
        Assert.Equal(1, cuedScreen.Evidence.CueCount);

        var response = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.RespondToCue,
                cued.ActiveCue!.CueId,
                cued.ActiveCue.ExpectedResponse ?? "switch"));

        Assert.True(response.LastCommand!.IsAccepted);
        Assert.Equal(1, AndroidLiveSessionContract.From(response).Evidence.CueResponseCount);
        Assert.False(response.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task LifecycleResumeUsesActiveSnapshotAndUnsafeSnapshotDoesNotBecomeEvidence()
    {
        var configuration = Configuration("android-lifecycle.json");
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "android-lifecycle-resume");
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: true));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var saved = await controller.PersistActiveSnapshotAsync();
        var resume = await workflow.TryResumeActiveSessionAsync(
            new PreUiActiveSessionResumeRequest(
                prepared.RuntimeSession!.SessionId,
                new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(30))),
                prepared.RuntimeSession.CueSchedule));
        var resumeScreen = AndroidActiveSessionContract.From(resume.State);

        Assert.Equal(PreUiActiveSessionResumeStatus.Resumable, resumeScreen.Status);
        Assert.True(resumeScreen.PrimaryActionEnabled);
        Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, resumeScreen.ActivePhaseKind);
        Assert.Equal(saved.RuntimeEventCount, resumeScreen.RuntimeEventCount);
        Assert.False(resumeScreen.GrantsAdvancementFromUi);

        var stateBeforeCompletion = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(ScreenDate));
        Assert.DoesNotContain(
            stateBeforeCompletion.RecentSessions,
            session => session.SessionId == prepared.RuntimeSession.SessionId);
        Assert.DoesNotContain(
            stateBeforeCompletion.EvidenceSummaries,
            artifact => artifact.Event.EventId == prepared.RuntimeSession.SessionId);

        var unsafeHandler = RuntimeInputCommandHandler.Start(
            "android-lifecycle-unsafe",
            WorkingMemorySessionDefinition(),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("encode", RuntimeSessionPhaseKind.EncodeWindow, RuntimeDuration.FromSeconds(60)),
                RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
            ]),
            new ManualRuntimeClock(RuntimeInstant.Zero),
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));
        await workflow.SaveActiveSessionSnapshotAsync(
            new ActiveRuntimeSessionSnapshotSaveRequest(unsafeHandler.CaptureSnapshot()));

        var unsafeResume = await workflow.TryResumeActiveSessionAsync(
            new PreUiActiveSessionResumeRequest(
                "android-lifecycle-unsafe",
                new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(100)))));
        var unsafeScreen = AndroidActiveSessionContract.From(unsafeResume.State);

        Assert.Equal(PreUiActiveSessionResumeStatus.Unsafe, unsafeScreen.Status);
        Assert.False(unsafeScreen.PrimaryActionEnabled);
        Assert.Null(unsafeResume.CommandHandler);
        Assert.False(unsafeScreen.GrantsAdvancementFromUi);

        var stateAfterUnsafeResume = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(ScreenDate));
        Assert.DoesNotContain(
            stateAfterUnsafeResume.RecentSessions,
            session => session.SessionId == "android-lifecycle-unsafe");
        Assert.DoesNotContain(
            stateAfterUnsafeResume.EvidenceSummaries,
            artifact => artifact.Event.EventId == "android-lifecycle-unsafe");
    }

    [Fact]
    public async Task ResultScreenUsesRuntimeOutcomeAndDoesNotPromoteAbandonedSessions()
    {
        var configuration = Configuration("android-result.json");
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(ScreenDate),
                "android-result-abandon"));
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                new ManualRuntimeClock(RuntimeInstant.Zero),
                saveActiveSnapshot: true));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started);

        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        var abandoned = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.Abandon,
                value: "user abandoned live session"));
        Assert.True(abandoned.IsTerminal);

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(ScreenDate));
        var resultScreen = AndroidResultContract.From(completed);

        Assert.Equal(RuntimeSessionCompletionStatus.Abandoned, resultScreen.RuntimeOutcome);
        Assert.True(resultScreen.RequiresFailureHierarchy);
        Assert.False(resultScreen.UsesSuccessHierarchy);
        Assert.Equal(BranchLevelState.Training, resultScreen.RefreshedBranchState);
        Assert.False(completed.WorkflowResult!.ProcessingResult.SessionHistory.CleanPerformance);
        Assert.DoesNotContain(
            completed.WorkflowResult.RefreshedState.BranchLevelStates,
            state => state.Branch == BranchCode.FH &&
                state.Level == GlobalLevelId.L1 &&
                state.State is BranchLevelState.PassedOnce or BranchLevelState.Owned);
        Assert.False(resultScreen.GrantsAdvancementFromUi);
    }

    [Fact]
    public async Task BackupRestoreControlsStayLocalOfflineAndRequireValidatedConfirmedRestore()
    {
        var configuration = Configuration("android-backup.json");
        var service = new LocalDataBackupWorkflowService(configuration, BackupDirectory());
        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance));

        var initial = await service.LoadAsync();
        var initialScreen = AndroidBackupRestoreContract.From(
            initial,
            ApplicationIntegrationBoundary.Capabilities);

        Assert.True(initialScreen.CurrentIntegrityValid);
        Assert.True(initialScreen.LocalOnly);
        Assert.False(initialScreen.AllowsAccounts);
        Assert.False(initialScreen.AllowsSync);
        Assert.False(initialScreen.AllowsBackendServices);
        Assert.False(initialScreen.AllowsTelemetry);
        Assert.False(initialScreen.AllowsAnalytics);
        Assert.False(initialScreen.AllowsAiOrApiDependencies);
        Assert.False(initialScreen.GrantsAdvancementFromUi);

        var exported = await service.ExportAsync(
            new LocalDataBackupExportRequest(new DateTimeOffset(2026, 7, 5, 12, 30, 0, TimeSpan.Zero)));
        Assert.Equal(LocalDataBackupOperationStatus.Succeeded, exported.Status);
        Assert.NotNull(exported.BackupFile);
        Assert.Equal(LocalDatabaseConnectivity.OfflineOnly, exported.BackupFile.Connectivity);

        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training));

        var unconfirmed = await service.RestoreLatestBackupAsync(
            new LocalDataBackupRestoreRequest(confirmReplaceLocalData: false));
        var unconfirmedScreen = AndroidBackupRestoreContract.From(unconfirmed);

        Assert.Equal(LocalDataBackupOperationStatus.ConfirmationRequired, unconfirmedScreen.OperationStatus);
        Assert.False(unconfirmedScreen.PrimaryActionEnabled);
        Assert.Equal(
            BranchLevelState.Training,
            (await LoadStateAsync(configuration))!.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));

        var validation = await service.ValidateLatestBackupAsync();
        Assert.Equal(LocalDataBackupOperationStatus.Succeeded, validation.Status);

        var restored = await service.RestoreLatestBackupAsync(
            new LocalDataBackupRestoreRequest(confirmReplaceLocalData: true));
        var restoredScreen = AndroidBackupRestoreContract.From(restored);

        Assert.Equal(LocalDataBackupOperationStatus.Succeeded, restoredScreen.OperationStatus);
        Assert.True(restoredScreen.CurrentIntegrityValid);
        Assert.Equal(
            BranchLevelState.Maintenance,
            (await LoadStateAsync(configuration))!.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.False(restoredScreen.GrantsAdvancementFromUi);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private AppStartupConfiguration Configuration(string fileName)
    {
        Directory.CreateDirectory(tempDirectory);
        return AppStartupConfiguration.ForAppOwnedLocalStoragePath(
            Path.Combine(tempDirectory, fileName));
    }

    private string BackupDirectory()
    {
        return Path.Combine(tempDirectory, "backups");
    }

    private static async ValueTask<PreUiTrainingWorkflowPreparationResult> PrepareCuePracticeAsync(
        PreUiTrainingWorkflowService workflow,
        string sessionId)
    {
        var prepared = await workflow.PrepareNextSessionAsync(new PreUiTrainingWorkflowPreparationRequest(
            new NextTrainingWorkSelectionQuery(
                ScreenDate,
                new RequestedTrainingWork(
                    BranchCode.FS,
                    GlobalLevelId.L1,
                    DrillId.FS1CueSwitch,
                    AppTrainingSessionType.Practice,
                    [
                        new LoadVariable("target count", "2"),
                        new LoadVariable("switch count", "4"),
                        new LoadVariable("cue density", "5 seconds"),
                        new LoadVariable("return precision", "next cue"),
                    ])),
            PromptContentKind.CueSequence,
            "fs-l1-cue-switch",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            new GeneratedContentSeed($"{sessionId}-seed"),
            sessionId,
            additionalCriticalConstraints: [new CriticalConstraint("No anticipatory switching.")],
            inputOptions: new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10),
                PauseAllowedPhaseKinds:
                [
                    RuntimeSessionPhaseKind.InstructionPrep,
                    RuntimeSessionPhaseKind.CueResponse,
                    RuntimeSessionPhaseKind.Review,
                ])));

        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);
        Assert.NotNull(prepared.RuntimeSession);
        Assert.NotNull(prepared.RuntimeSession.CueSchedule);
        return prepared;
    }

    private static RuntimeSessionDefinition WorkingMemorySessionDefinition()
    {
        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            [new LoadVariable("item count", "5 simple items")],
            new BranchLevelStandard(
                BranchCode.WM,
                GlobalLevelId.L1,
                "Encode and reconstruct 5 simple items after 60 seconds.",
                "At least 4 of 5 exact; no invented items.",
                "FH L1 passed once.",
                "Repeat twice with new item sets.",
                "Use a different content type."),
            [new CriticalConstraint("No rereading after encode window; no invented items.")]);
    }

    private static async ValueTask SaveStateAsync(
        AppStartupConfiguration configuration,
        params BranchLevelStatus[] statuses)
    {
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions)
            .SaveAsync(new PractitionerState(statuses));
    }

    private static ValueTask<PractitionerState?> LoadStateAsync(AppStartupConfiguration configuration)
    {
        return new LocalPractitionerStateStore(configuration.LocalDatabaseOptions)
            .LoadAsync();
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static ValueTask SaveMaintenanceAsync(
        AppStartupConfiguration configuration,
        string checkId,
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        bool passed)
    {
        return new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions).SaveMaintenanceAsync(
            new LocalMaintenanceCheckRecord(
                checkId,
                $"artifact-{checkId}",
                null,
                DrillId.FH1TargetHold,
                "Maintenance standard remained stated before the check.",
                new MaintenanceCheckEvidence(
                    branch,
                    level,
                    date,
                    MaintenanceCheckKind.StandardOrTransfer,
                    passed
                        ? new StandardEvaluationResult(true, [])
                        : new StandardEvaluationResult(
                            false,
                            [new StandardEvaluationFailure(StandardFailureKind.CriticalConstraintBroken, "Critical constraint broken.")]))));
    }

    private static async ValueTask SaveEvidenceAndSessionAsync(
        AppStartupConfiguration configuration,
        string artifactId,
        string sessionId,
        LocalProgrammingEventKind eventKind,
        EvidenceArtifactCategory category,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        bool cleanPerformance)
    {
        await new LocalEvidenceArtifactStore(configuration.LocalDatabaseOptions).SaveAsync(
            new LocalEvidenceArtifactRecord(
                artifactId,
                new LocalProgrammingEventReference(
                    sessionId,
                    eventKind,
                    branch,
                    level,
                    drill),
                new EvidenceArtifact(
                    category,
                    ScreenDate,
                    [
                        new ObservableEvidence(ObservableEvidenceKind.OutputSample, "Observable transfer output was captured."),
                        new ObservableEvidence(ObservableEvidenceKind.Score, "correct_responses=4; anticipatory_switches=0"),
                        new ObservableEvidence(ObservableEvidenceKind.CriticalConstraintRecord, "No anticipatory switching was recorded."),
                    ],
                    "Transfer evidence with score and critical constraint.")));

        await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions).SaveAsync(
            new LocalSessionHistoryRecord(
                sessionId,
                ScreenDate,
                LocalCompletedSessionType.Transfer,
                [new LocalSessionBranchLevel(branch, level)],
                drill,
                transferTask: "changed transfer context",
                LocalSessionIntensity.Moderate,
                [new LoadVariable("cue density", "5 seconds")],
                cleanPerformance,
                "Observable transfer session record.",
                recoveryMarked: false,
                deloadMarked: false,
                evidenceArtifactIds: [artifactId]));
    }

    private sealed record AndroidHomeContract(
        int NextWorkCount,
        int MaintenanceDueCount,
        bool HasBlockedAdvancement,
        bool HasRecoveryOrMaintenanceWork,
        bool GrantsAdvancementFromUi)
    {
        public static AndroidHomeContract From(CurrentTrainingStateReadModel state)
        {
            return new AndroidHomeContract(
                state.AvailableNextWork.Count,
                state.DueMaintenance.Count,
                state.BlockedAdvancement.Count > 0,
                state.DueMaintenance.Count > 0 ||
                    state.AvailableNextWork.Any(work =>
                        work.Session is WeeklySessionKind.Recovery or WeeklySessionKind.Maintenance),
                GrantsAdvancementFromUi: false);
        }
    }

    private sealed record AndroidBranchLadderContract(
        IReadOnlyList<AndroidStateMarker> Markers,
        bool GrantsAdvancementFromUi)
    {
        public AndroidStateMarker MarkerFor(BranchCode branch, GlobalLevelId level)
        {
            return Markers.Single(marker => marker.Branch == branch && marker.Level == level);
        }

        public static AndroidBranchLadderContract From(CurrentTrainingStateReadModel state)
        {
            return new AndroidBranchLadderContract(
                state.BranchLevelStates.Select(AndroidStateMarker.From).ToArray(),
                GrantsAdvancementFromUi: false);
        }
    }

    private sealed record AndroidStateMarker(
        BranchCode Branch,
        GlobalLevelId Level,
        BranchLevelState State,
        string Shape,
        int Hierarchy,
        bool RequiresImmediateAttention)
    {
        public static AndroidStateMarker From(BranchLevelStatus status)
        {
            return status.State switch
            {
                BranchLevelState.PassedOnce => Marker(status, "single-check-outline", hierarchy: 3, attention: false),
                BranchLevelState.Owned => Marker(status, "solid-lock-ring", hierarchy: 5, attention: false),
                BranchLevelState.Maintenance => Marker(status, "side-tab", hierarchy: 6, attention: true),
                BranchLevelState.Decayed => Marker(status, "split-warning-block", hierarchy: 10, attention: true),
                BranchLevelState.TestReady => Marker(status, "open-diamond", hierarchy: 4, attention: false),
                BranchLevelState.Stabilizing => Marker(status, "stacked-checks", hierarchy: 4, attention: false),
                BranchLevelState.Training => Marker(status, "half-filled-cell", hierarchy: 2, attention: false),
                _ => Marker(status, "empty-cell", hierarchy: 1, attention: false),
            };
        }

        private static AndroidStateMarker Marker(
            BranchLevelStatus status,
            string shape,
            int hierarchy,
            bool attention)
        {
            return new AndroidStateMarker(
                status.Branch,
                status.Level,
                status.State,
                shape,
                hierarchy,
                attention);
        }
    }

    private sealed record AndroidProgressContract(
        int OwnedLevelCount,
        int PassedOnceCount,
        IReadOnlyList<BranchCode> TestReadyBranches,
        IReadOnlyList<BranchCode> DecayedBranches,
        bool GrantsAdvancementFromUi)
    {
        public static AndroidProgressContract From(CurrentTrainingStateReadModel state)
        {
            return new AndroidProgressContract(
                state.BranchLevelStates.Count(status => status.State == BranchLevelState.Owned),
                state.BranchLevelStates.Count(status => status.State == BranchLevelState.PassedOnce),
                state.BranchLevelStates
                    .Where(status => status.State == BranchLevelState.TestReady)
                    .Select(status => status.Branch)
                    .Distinct()
                    .ToArray(),
                state.BranchLevelStates
                    .Where(status => status.State == BranchLevelState.Decayed)
                    .Select(status => status.Branch)
                    .Distinct()
                    .ToArray(),
                GrantsAdvancementFromUi: false);
        }
    }

    private sealed record AndroidEvidenceReviewContract(
        IReadOnlyList<AndroidEvidenceArtifactContract> Artifacts,
        bool GrantsAdvancementFromUi)
    {
        public static AndroidEvidenceReviewContract From(CurrentTrainingStateReadModel state)
        {
            return new AndroidEvidenceReviewContract(
                state.EvidenceSummaries.Select(AndroidEvidenceArtifactContract.From).ToArray(),
                GrantsAdvancementFromUi: false);
        }
    }

    private sealed record AndroidEvidenceArtifactContract(
        string SessionId,
        BranchCode? Branch,
        GlobalLevelId? Level,
        DrillId? Drill,
        EvidenceArtifactCategory Category,
        IReadOnlyList<ObservableEvidenceKind> ObservableEvidenceKinds,
        bool HasObservableEvidence)
    {
        public static AndroidEvidenceArtifactContract From(LocalEvidenceArtifactRecord artifact)
        {
            return new AndroidEvidenceArtifactContract(
                artifact.Event.EventId,
                artifact.Event.Branch,
                artifact.Event.Level,
                artifact.Event.Drill,
                artifact.Artifact.Category,
                artifact.Artifact.ObservableEvidence.Select(item => item.Kind).ToArray(),
                artifact.Artifact.ObservableEvidence.Count > 0);
        }
    }

    private sealed record AndroidMaintenanceContract(
        IReadOnlyList<BranchCode> DueBranches,
        IReadOnlyList<BranchCode> DecayedBranches,
        int BlockedAdvancementCount,
        bool CanDismissDecayFromUi,
        bool GrantsAdvancementFromUi)
    {
        public static AndroidMaintenanceContract From(CurrentTrainingStateReadModel state)
        {
            return new AndroidMaintenanceContract(
                state.DueMaintenance.Select(due => due.BranchLevel.Branch).Distinct().ToArray(),
                state.BranchLevelStates
                    .Where(status => status.State == BranchLevelState.Decayed)
                    .Select(status => status.Branch)
                    .Distinct()
                    .ToArray(),
                state.BlockedAdvancement.Count,
                CanDismissDecayFromUi: false,
                GrantsAdvancementFromUi: false);
        }
    }

    private sealed record AndroidSessionStartContract(
        bool PrimaryActionEnabled,
        string? VisibleStandard,
        string? VisibleHonestyConstraint,
        int LockedLoadVariableCount,
        int RequiredEvidenceFactCount,
        string? GeneratedContentInstanceId,
        IReadOnlyList<string> LockedInputs,
        IReadOnlyList<string> BlockerDetails,
        bool GrantsAdvancementFromUi)
    {
        public static AndroidSessionStartContract From(PreUiTrainingWorkflowPreparationResult result)
        {
            var selectedWork = result.Selection.SelectedWork;
            return new AndroidSessionStartContract(
                result.CanStartRuntimeSession,
                selectedWork?.Standard,
                selectedWork?.HonestyConstraint,
                selectedWork?.LoadVariables.Count ?? 0,
                result.RuntimeSession?.ExpectedEvidenceFacts.Count ?? 0,
                result.GeneratedContent?.PersistenceHandoff?.InstanceId,
                ["standard", "honesty-constraint", "load", "prerequisites"],
                result.Selection.Blockers.Select(blocker => blocker.Detail).ToArray(),
                GrantsAdvancementFromUi: false);
        }
    }

    private sealed record AndroidLiveSessionContract(
        RuntimeSessionPhaseKind? CurrentPhaseKind,
        IReadOnlyList<RuntimeInputCommandKind> AvailableCommands,
        PreUiLiveSessionEvidenceState Evidence,
        bool GrantsAdvancementFromUi)
    {
        public static AndroidLiveSessionContract From(PreUiLiveSessionState state)
        {
            return new AndroidLiveSessionContract(
                state.CurrentPhaseKind,
                state.Commands
                    .Where(command => command.IsAvailable)
                    .Select(command => command.Command)
                    .ToArray(),
                state.Evidence,
                GrantsAdvancementFromUi: false);
        }
    }

    private sealed record AndroidActiveSessionContract(
        PreUiActiveSessionResumeStatus Status,
        bool PrimaryActionEnabled,
        RuntimeSessionPhaseKind? ActivePhaseKind,
        int RuntimeEventCount,
        bool GrantsAdvancementFromUi)
    {
        public static AndroidActiveSessionContract From(PreUiActiveSessionResumeState state)
        {
            return new AndroidActiveSessionContract(
                state.Status,
                state.CanResume,
                state.ActivePhaseKind,
                state.RuntimeEventCount,
                GrantsAdvancementFromUi: false);
        }
    }

    private sealed record AndroidResultContract(
        RuntimeSessionCompletionStatus? RuntimeOutcome,
        bool RequiresFailureHierarchy,
        bool UsesSuccessHierarchy,
        BranchLevelState RefreshedBranchState,
        bool GrantsAdvancementFromUi)
    {
        public static AndroidResultContract From(PreUiLiveSessionCompletionResult result)
        {
            var workflow = result.WorkflowResult;
            return new AndroidResultContract(
                result.RuntimeCompletionStatus,
                result.RuntimeCompletionStatus is RuntimeSessionCompletionStatus.Abandoned
                    or RuntimeSessionCompletionStatus.Failed
                    or RuntimeSessionCompletionStatus.TimedOut,
                result.RuntimeCompletionStatus == RuntimeSessionCompletionStatus.Completed &&
                    workflow?.ProcessingResult.SessionHistory.CleanPerformance == true,
                workflow?.RefreshedState.CurrentPractitionerState.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1)
                    ?? BranchLevelState.Unopened,
                GrantsAdvancementFromUi: false);
        }
    }

    private sealed record AndroidBackupRestoreContract(
        bool CurrentIntegrityValid,
        bool LocalOnly,
        LocalDataBackupOperationStatus? OperationStatus,
        bool PrimaryActionEnabled,
        bool AllowsAccounts,
        bool AllowsSync,
        bool AllowsBackendServices,
        bool AllowsTelemetry,
        bool AllowsAnalytics,
        bool AllowsAiOrApiDependencies,
        bool GrantsAdvancementFromUi)
    {
        public static AndroidBackupRestoreContract From(
            LocalDataBackupReadModel model,
            ApplicationIntegrationCapabilities capabilities)
        {
            return new AndroidBackupRestoreContract(
                model.CurrentIntegrity.IsValid,
                Path.IsPathRooted(model.LocalDatabasePath) && Path.IsPathRooted(model.BackupDirectoryPath),
                OperationStatus: null,
                PrimaryActionEnabled: true,
                capabilities.AllowsAccounts,
                capabilities.AllowsSync,
                capabilities.AllowsBackendServices,
                capabilities.AllowsTelemetry,
                capabilities.AllowsAnalytics,
                capabilities.AllowsAiOrApiDependencies,
                GrantsAdvancementFromUi: false);
        }

        public static AndroidBackupRestoreContract From(LocalDataBackupOperationResult result)
        {
            return new AndroidBackupRestoreContract(
                result.CurrentIntegrity?.IsValid == true,
                result.BackupFile is null || Path.IsPathRooted(result.BackupFile.FilePath),
                result.Status,
                result.Status is LocalDataBackupOperationStatus.Succeeded,
                AllowsAccounts: false,
                AllowsSync: false,
                AllowsBackendServices: false,
                AllowsTelemetry: false,
                AllowsAnalytics: false,
                AllowsAiOrApiDependencies: false,
                GrantsAdvancementFromUi: false);
        }
    }
}
