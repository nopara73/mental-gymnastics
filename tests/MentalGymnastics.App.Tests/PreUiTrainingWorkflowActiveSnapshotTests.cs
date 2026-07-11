using MentalGymnastics.App;
using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class PreUiTrainingWorkflowActiveSnapshotTests : IDisposable
{
    private static readonly TrainingDate SessionDate = TrainingDate.From(2026, 7, 5);

    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StartingResumableSessionSavesActiveSnapshotAndExposesResumeState()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "workflow-active-resumable");

        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                new ManualRuntimeClock(RuntimeInstant.Zero),
                saveActiveSnapshot: true));

        Assert.Equal(PreUiTrainingWorkflowStartStatus.Started, started.Status);
        Assert.NotNull(started.CommandHandler);
        Assert.NotNull(started.CueScheduler);
        Assert.True(started.ActiveSession.CanResume);
        Assert.Equal("workflow-active-resumable", started.ActiveSession.SessionId);
        Assert.Equal(BranchCode.FS, started.ActiveSession.Branch);
        Assert.Equal(GlobalLevelId.L1, started.ActiveSession.Level);
        Assert.Equal(DrillId.FS1CueSwitch, started.ActiveSession.Drill);
        Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, started.ActiveSession.ActivePhaseKind);
        Assert.NotEmpty(started.ActiveSession.PendingCueIds);
        Assert.False(started.GrantsAdvancementInApp);

        var stored = await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync("workflow-active-resumable");
        Assert.NotNull(stored);
        Assert.Equal(prepared.RuntimeSession!.SessionDefinition!.GeneratedDrillInstance!.InstanceId, stored.SessionDefinition.GeneratedDrillInstance!.InstanceId);

        var resumed = await workflow.TryResumeActiveSessionAsync(
            new PreUiActiveSessionResumeRequest(
                "workflow-active-resumable",
                new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(30))),
                prepared.RuntimeSession.CueSchedule));

        Assert.Equal(PreUiActiveSessionResumeStatus.Resumable, resumed.State.Status);
        Assert.True(resumed.State.CanResume);
        Assert.NotNull(resumed.CommandHandler);
        Assert.NotNull(resumed.CueScheduler);
        Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, resumed.State.ActivePhaseKind);
        Assert.Equal(started.ActiveSession.PendingCueIds, resumed.State.PendingCueIds);
        Assert.False(resumed.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task StartingWithoutSavingSnapshotExposesLiveOnlyStateAsNonResumable()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "workflow-active-live-only");

        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                new ManualRuntimeClock(RuntimeInstant.Zero),
                saveActiveSnapshot: false));

        Assert.Equal(PreUiTrainingWorkflowStartStatus.Started, started.Status);
        Assert.NotNull(started.CommandHandler);
        Assert.NotNull(started.CueScheduler);
        Assert.Equal(PreUiActiveSessionResumeStatus.NotPersisted, started.ActiveSession.Status);
        Assert.False(started.ActiveSession.CanResume);
        Assert.Equal("workflow-active-live-only", started.ActiveSession.SessionId);
        Assert.Equal(BranchCode.FS, started.ActiveSession.Branch);
        Assert.Equal(GlobalLevelId.L1, started.ActiveSession.Level);
        Assert.Equal(DrillId.FS1CueSwitch, started.ActiveSession.Drill);
        Assert.Null(await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync("workflow-active-live-only"));

        var resumed = await workflow.TryResumeActiveSessionAsync(
            new PreUiActiveSessionResumeRequest(
                "workflow-active-live-only",
                new ManualRuntimeClock(RuntimeInstant.Zero),
                prepared.RuntimeSession!.CueSchedule));

        Assert.Equal(PreUiActiveSessionResumeStatus.NotFound, resumed.State.Status);
        Assert.False(resumed.CanResume);
        Assert.False(started.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task CompletedSessionProcessingClearsActiveSnapshot()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "workflow-active-completed");
        await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                new ManualRuntimeClock(RuntimeInstant.Zero),
                saveActiveSnapshot: true));

        var runtimeResult = CompletedPracticeResult(
            "workflow-active-completed",
            prepared.RuntimeSession!.SessionDefinition!);

        await workflow.CompleteSessionAsync(new PreUiTrainingWorkflowCompletionRequest(
            new CompletedRuntimeSessionProcessingRequest(
                runtimeResult,
                Metadata(cleanPerformance: true, "Completed FS practice with observable cue evidence.")),
            SessionDate));

        Assert.Null(await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync("workflow-active-completed"));
        Assert.Equal(
            PreUiActiveSessionResumeStatus.NotFound,
            (await workflow.TryResumeActiveSessionAsync(new PreUiActiveSessionResumeRequest(
                "workflow-active-completed",
                new ManualRuntimeClock(RuntimeInstant.Zero)))).State.Status);
    }

    [Fact]
    public async Task AbandonedSessionProcessingClearsActiveSnapshotWithoutSuccessfulEvidence()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "workflow-active-abandoned");
        await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                new ManualRuntimeClock(RuntimeInstant.Zero),
                saveActiveSnapshot: true));

        var runtimeResult = AbandonedResult(
            "workflow-active-abandoned",
            prepared.RuntimeSession!.SessionDefinition!);

        var completed = await workflow.CompleteSessionAsync(new PreUiTrainingWorkflowCompletionRequest(
            new CompletedRuntimeSessionProcessingRequest(
                runtimeResult,
                Metadata(cleanPerformance: false, "Abandoned before producing successful evidence.")),
            SessionDate));

        Assert.Equal(RuntimeSessionCompletionStatus.Abandoned, completed.ProcessingResult.CompletionStatus);
        Assert.Empty(completed.ProcessingResult.PersistenceHandoff?.EvidenceArtifacts ?? []);
        Assert.Null(await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync("workflow-active-abandoned"));
        Assert.False(completed.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task UnsafeRestoreIsExposedAsNonResumableAndCanBeInvalidated()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = RuntimeInputCommandHandler.Start(
            "workflow-active-unsafe",
            WorkingMemorySessionDefinition(),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("encode", RuntimeSessionPhaseKind.EncodeWindow, RuntimeDuration.FromSeconds(60)),
                RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
            ]),
            clock,
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(20));
        await workflow.SaveActiveSessionSnapshotAsync(new ActiveRuntimeSessionSnapshotSaveRequest(handler.CaptureSnapshot()));

        var resumed = await workflow.TryResumeActiveSessionAsync(
            new PreUiActiveSessionResumeRequest(
                "workflow-active-unsafe",
                new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(100)))));

        Assert.Equal(PreUiActiveSessionResumeStatus.Unsafe, resumed.State.Status);
        Assert.False(resumed.State.CanResume);
        Assert.Null(resumed.CommandHandler);
        Assert.Contains("cannot be restored", resumed.State.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.False(resumed.GrantsAdvancementInApp);

        var invalidated = await workflow.InvalidateActiveSessionSnapshotAsync(
            new PreUiActiveSessionInvalidationRequest(
                "workflow-active-unsafe",
                "Runtime rejected the snapshot for honest continuation."));

        Assert.True(invalidated.Cleared);
        Assert.False(invalidated.GrantsAdvancementInApp);
        Assert.Null(await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync("workflow-active-unsafe"));
    }

    [Fact]
    public async Task LatestActiveSessionRestoreUsesMostRecentlyCapturedSnapshot()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var phasePlan = new RuntimeSessionPhasePlan(
        [
            RuntimeSessionPhaseDefinition.Manual("prep", RuntimeSessionPhaseKind.InstructionPrep),
            RuntimeSessionPhaseDefinition.Manual("review", RuntimeSessionPhaseKind.Review),
        ]);
        var inputOptions = new RuntimeInputCommandOptions(
            PauseAllowed: true,
            CorrectionWindow: RuntimeDuration.FromSeconds(10));

        var oldClock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var oldHandler = RuntimeInputCommandHandler.Start(
            "workflow-active-latest-old",
            WorkingMemorySessionDefinition(),
            phasePlan,
            oldClock,
            inputOptions);
        await workflow.SaveActiveSessionSnapshotAsync(
            new ActiveRuntimeSessionSnapshotSaveRequest(oldHandler.CaptureSnapshot()));

        var newClock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var newHandler = RuntimeInputCommandHandler.Start(
            "workflow-active-latest-new",
            WorkingMemorySessionDefinition(),
            phasePlan,
            newClock,
            inputOptions);
        newClock.AdvanceBy(RuntimeDuration.FromSeconds(12));
        await workflow.SaveActiveSessionSnapshotAsync(
            new ActiveRuntimeSessionSnapshotSaveRequest(newHandler.CaptureSnapshot()));

        var resumed = await workflow.TryResumeLatestActiveSessionAsync(
            new PreUiActiveSessionResumeLatestRequest(
                new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(100)))));

        Assert.Equal(PreUiActiveSessionResumeStatus.Resumable, resumed.State.Status);
        Assert.True(resumed.CanResume);
        Assert.Equal("workflow-active-latest-new", resumed.State.SessionId);
        Assert.NotNull(resumed.CommandHandler);
        Assert.False(resumed.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task LiveSessionControllerRendersRuntimeStateAndRecordsEvidenceWithoutAdvancement()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "workflow-live-controller");
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

        Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, initial.CurrentPhaseKind);
        Assert.False(initial.GrantsAdvancementInApp);
        Assert.True(CommandState(initial, RuntimeInputCommandKind.FinishPhase).IsAvailable);
        Assert.False(CommandState(initial, RuntimeInputCommandKind.RespondToCue).IsAvailable);
        Assert.Equal(0, initial.Evidence.CueResponseCount);

        var cuePhase = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.True(cuePhase.LastCommand!.IsAccepted);
        Assert.Equal(RuntimeSessionPhaseKind.CueResponse, cuePhase.CurrentPhaseKind);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var cued = await controller.RefreshAsync();

        Assert.NotNull(cued.ActiveCue);
        Assert.True(CommandState(cued, RuntimeInputCommandKind.RespondToCue).IsAvailable);
        Assert.Equal(1, cued.Evidence.CueCount);

        var response = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.RespondToCue,
                cued.ActiveCue!.CueId));
        Assert.True(response.LastCommand!.IsAccepted);
        Assert.Equal(RuntimeCueResponseOutcome.Correct, response.LastCommand.CueOutcome);
        Assert.Equal(1, response.Evidence.CueResponseCount);

        var error = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.MarkError,
                value: "incorrect_response"));
        Assert.True(error.LastCommand!.IsAccepted);
        Assert.Equal(1, error.Evidence.ErrorCount);
        Assert.False(error.GrantsAdvancementInApp);

        var stored = await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync("workflow-live-controller");
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task IncorrectAndOmittedCueResponsesRemainVisibleAsFailureEvidence()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "workflow-cue-failure-evidence");
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var cued = await controller.RefreshAsync();
        var wrong = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.RespondToCue,
                cued.ActiveCue!.CueId,
                "not-the-active-target"));

        Assert.Equal(RuntimeCueResponseOutcome.Incorrect, wrong.LastCommand!.CueOutcome);
        Assert.Equal(1, wrong.Evidence.ErrorCount);

        clock.AdvanceBy(new RuntimeDuration(TimeSpan.FromMinutes(1)));
        await controller.RefreshAsync();
        clock.AdvanceBy(RuntimeDuration.FromSeconds(3));
        var expired = await controller.RefreshAsync();
        Assert.True(expired.Evidence.ErrorCount > 1);
        var review = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.Review, review.CurrentPhaseKind);
        var terminal = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.True(terminal.IsTerminal);

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(SessionDate));

        Assert.False(completed.WorkflowResult!.ProcessingResult.SessionHistory.CleanPerformance);
        Assert.Contains(completed.WorkflowResult.ProcessingResult.EvidenceArtifacts, artifact =>
            artifact.Artifact.ObservableEvidence.Any(evidence =>
                evidence.Kind == ObservableEvidenceKind.FailedItemList));
    }

    [Fact]
    public async Task CueScheduleDoesNotRunWhileUserReadsInstructionPrep()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "workflow-cue-prep-clock");
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        Assert.True(started.CueScheduler!.IsPaused);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var prep = await controller.RefreshAsync();

        Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, prep.CurrentPhaseKind);
        Assert.Null(prep.ActiveCue);
        Assert.Equal(0, prep.Evidence.CueCount);

        var cuePhase = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));

        Assert.Equal(RuntimeSessionPhaseKind.CueResponse, cuePhase.CurrentPhaseKind);
        Assert.False(started.CueScheduler.IsPaused);
        Assert.Null(cuePhase.ActiveCue);
        Assert.False(CommandState(cuePhase, RuntimeInputCommandKind.FinishPhase).IsAvailable);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var firstCue = await controller.RefreshAsync();

        Assert.NotNull(firstCue.ActiveCue);
        Assert.Equal(1, firstCue.Evidence.CueCount);
        Assert.True(CommandState(firstCue, RuntimeInputCommandKind.RespondToCue).IsAvailable);
    }

    [Fact]
    public async Task LiveSessionControllerLabelsCommandsFromUserIntent()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "workflow-live-command-labels"));
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        var initial = await controller.RefreshAsync();

        Assert.Equal("Next step", CommandState(initial, RuntimeInputCommandKind.FinishPhase).Label);
        Assert.Equal("Stop early", CommandState(initial, RuntimeInputCommandKind.Abandon).Label);

        var active = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));

        Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, active.CurrentPhaseKind);
        Assert.Equal("Mind wandered", CommandState(active, RuntimeInputCommandKind.MarkDrift).Label);
        Assert.False(CommandState(active, RuntimeInputCommandKind.MarkReturn).IsAvailable);
        Assert.Equal("Back on target", CommandState(active, RuntimeInputCommandKind.MarkReturn).Label);
        Assert.True(CommandState(active, RuntimeInputCommandKind.MarkTargetChange).IsAvailable);
        Assert.Equal("Target changed", CommandState(active, RuntimeInputCommandKind.MarkTargetChange).Label);
        Assert.DoesNotContain(
            active.Commands,
            command => command.Command == RuntimeInputCommandKind.SubmitAnswer);
        Assert.Equal("Stop early", CommandState(active, RuntimeInputCommandKind.Abandon).Label);
        Assert.DoesNotContain(active.Commands.Select(command => command.Label), label =>
            label is "Abandon" or "Submit" or "Drift");

        var returning = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.MarkDrift));

        Assert.False(CommandState(returning, RuntimeInputCommandKind.MarkDrift).IsAvailable);
        Assert.True(CommandState(returning, RuntimeInputCommandKind.MarkReturn).IsAvailable);
        Assert.Equal(1, returning.Evidence.OpenDriftCount);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var returned = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.MarkReturn));

        Assert.True(CommandState(returned, RuntimeInputCommandKind.MarkDrift).IsAvailable);
        Assert.False(CommandState(returned, RuntimeInputCommandKind.MarkReturn).IsAvailable);
        Assert.Equal(1, returned.Evidence.ReturnCount);
        Assert.Equal(0, returned.Evidence.OpenDriftCount);
        Assert.Equal(TimeSpan.FromSeconds(5), returned.Evidence.MaximumReturnTime);
        Assert.Contains(started.CommandHandler!.Events, runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.RecoveryCompleted &&
            runtimeEvent.Facts.Any(fact =>
                fact.Name == "recovery_time" && fact.Value == "00:00:05"));
    }

    [Fact]
    public async Task LiveSessionControllerPersistsLifecycleSnapshotWithoutAdvancingPhase()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "android-live-fh-lifecycle"));
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

        var active = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, active.CurrentPhaseKind);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var saved = await controller.PersistActiveSnapshotAsync();

        Assert.Equal(PreUiActiveSessionResumeStatus.Resumable, saved.Status);
        Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, saved.ActivePhaseKind);
        Assert.False(saved.GrantsAdvancementInApp);

        var stored = await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.RuntimeSession!.SessionId);
        Assert.NotNull(stored);
        Assert.Equal(TimeSpan.FromSeconds(30), stored.PhaseScheduler.CurrentPhaseElapsed);
        Assert.DoesNotContain(stored.RuntimeEvents, runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.PhaseTimedOut.ToString());
    }

    [Fact]
    public async Task LiveSessionControllerCompletesFirstRunFocusHoldPathThroughWorkflow()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "android-live-fh"));
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
        Assert.Equal(DrillId.FH1TargetHold, initial.Drill);
        Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, initial.CurrentPhaseKind);

        var active = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, active.CurrentPhaseKind);
        Assert.True(active.Timer.IsTimed);
        Assert.Equal(TimeSpan.FromMinutes(2), active.Timer.Remaining);

        clock.AdvanceBy(new RuntimeDuration(TimeSpan.FromMinutes(2)));
        var review = await controller.RefreshAsync();
        Assert.Equal(RuntimeSessionPhaseKind.Review, review.CurrentPhaseKind);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, review.LifecycleStatus);
        Assert.Contains(
            "Runtime advanced due timed phase events",
            review.Detail,
            StringComparison.OrdinalIgnoreCase);

        var terminal = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.True(terminal.IsTerminal);
        Assert.Equal(RuntimeSessionLifecycleStatus.Completed, terminal.LifecycleStatus);

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(SessionDate));

        Assert.True(completed.IsProcessed);
        Assert.Equal(RuntimeSessionCompletionStatus.Completed, completed.RuntimeCompletionStatus);
        Assert.False(completed.GrantsAdvancementInApp);
        Assert.NotNull(completed.WorkflowResult);
        Assert.Equal(RuntimeSessionCompletionStatus.Completed, completed.WorkflowResult!.ProcessingResult.CompletionStatus);
        Assert.Single(completed.WorkflowResult.ProcessingResult.EvidenceArtifacts);
        Assert.All(
            completed.WorkflowResult.ProcessingResult.EvidenceArtifacts,
            artifact => Assert.Equal(SessionDate, artifact.Artifact.Date));
        Assert.True(completed.WorkflowResult.ProcessingResult.SessionHistory.CleanPerformance);
        Assert.True(completed.WorkflowResult.ProcessingResult.StandardEvaluationResult!.Passed);
        Assert.Contains(
            completed.WorkflowResult.RefreshedState.RecentSessions,
            session => session.SessionId == prepared.RuntimeSession!.SessionId);

        var generated = await new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.GeneratedContent!.PersistenceHandoff!.InstanceId);
        Assert.NotNull(generated);
        Assert.Equal(LocalGeneratedDrillInstanceState.Completed, generated.State);
        Assert.Null(await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.RuntimeSession!.SessionId));
    }

    [Fact]
    public async Task LifecycleSuspendRestoreAndResumePreserveLiveFocusHoldEvidenceAndTime()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "android-lifecycle-fh"));
        var originalTarget = prepared.RuntimeSession!.InputMaterials.Single(material =>
            material.Kind == GeneratedContentMaterialKind.TargetStatement);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(prepared.RuntimeSession!, clock, saveActiveSnapshot: true));
        var controller = new PreUiLiveSessionController(workflow, prepared.RuntimeSession!, started);

        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(18));
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.MarkDrift, "lifecycle-drift"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(2));

        var suspended = await controller.SuspendForLifecycleAsync();
        var persisted = await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.RuntimeSession!.SessionId);

        Assert.Equal(RuntimeSessionLifecycleStatus.Paused, suspended.LifecycleStatus);
        Assert.Equal(TimeSpan.FromSeconds(20), suspended.Timer.Elapsed);
        Assert.Equal(1, suspended.Evidence.DriftCount);
        Assert.NotNull(persisted);
        Assert.Equal(RuntimeSessionLifecycleStatus.Paused.ToString(), persisted.LifecycleState.Status);

        var restoreClock = new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(100)));
        var restored = await workflow.TryResumeLatestActiveSessionAsync(
            new PreUiActiveSessionResumeLatestRequest(restoreClock));
        Assert.True(restored.CanResume);
        Assert.Contains(restored.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.TargetStatement &&
            material.Name == originalTarget.Name &&
            material.Value == originalTarget.Value);
        var resumedController = new PreUiLiveSessionController(
            workflow,
            restored.CommandHandler!,
            restored.CueScheduler,
            inputMaterials: restored.InputMaterials);
        var resumed = await resumedController.ResumeFromLifecycleAsync();

        Assert.Equal(RuntimeSessionLifecycleStatus.Running, resumed.LifecycleStatus);
        Assert.Equal(TimeSpan.FromSeconds(20), resumed.Timer.Elapsed);
        Assert.Equal(1, resumed.Evidence.DriftCount);
        Assert.False(CommandState(resumed, RuntimeInputCommandKind.Pause).IsAvailable);

        restoreClock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var returned = await resumedController.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.MarkReturn));

        Assert.True(returned.LastCommand!.IsAccepted);
        Assert.Equal(TimeSpan.FromSeconds(25), returned.Timer.Elapsed);
        Assert.Equal(1, returned.Evidence.ReturnCount);
        Assert.Equal(0, returned.Evidence.LateReturnCount);
        Assert.Equal(TimeSpan.FromSeconds(7), returned.Evidence.MaximumReturnTime);
    }

    [Fact]
    public async Task LatestCueSessionRestoreRebuildsPersistedScheduleAndMaterialsInsideApp()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "android-lifecycle-fs");
        var originalCueMaterial = prepared.RuntimeSession!.InputMaterials.First(material =>
            material.Kind is GeneratedContentMaterialKind.ValidCue or GeneratedContentMaterialKind.InvalidCue);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(prepared.RuntimeSession, clock, saveActiveSnapshot: true));
        var controller = new PreUiLiveSessionController(workflow, prepared.RuntimeSession, started);

        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var cued = await controller.RefreshAsync();
        Assert.NotNull(cued.ActiveCue);
        await controller.SuspendForLifecycleAsync();

        var restored = await workflow.TryResumeLatestActiveSessionAsync(
            new PreUiActiveSessionResumeLatestRequest(
                new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(100)))));

        Assert.True(restored.CanResume);
        Assert.NotNull(restored.CueScheduler);
        Assert.Contains(restored.InputMaterials, material =>
            material.Kind == originalCueMaterial.Kind &&
            material.Name == originalCueMaterial.Name &&
            material.Value == originalCueMaterial.Value);
        Assert.Equal(
            prepared.RuntimeSession.CueSchedule!.Cues.Select(cue => cue.Id),
            restored.CueScheduler!.Schedule.Cues.Select(cue => cue.Id));

        var resumedController = new PreUiLiveSessionController(
            workflow,
            restored.CommandHandler!,
            restored.CueScheduler,
            inputMaterials: restored.InputMaterials);
        var resumed = await resumedController.ResumeFromLifecycleAsync();

        Assert.Equal(RuntimeSessionLifecycleStatus.Running, resumed.LifecycleStatus);
        Assert.NotNull(resumed.ActiveCue);
        Assert.Equal(cued.ActiveCue!.CueId, resumed.ActiveCue!.CueId);
    }

    [Fact]
    public async Task SecondCleanFocusHoldPracticeMarksTheLevelTestReady()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training));
        await SaveCleanFocusHoldPracticeAsync(
            configuration,
            "fh-clean-first",
            TrainingDate.From(2026, 7, 4));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "fh-clean-second"));
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        clock.AdvanceBy(new RuntimeDuration(TimeSpan.FromMinutes(3)));
        await controller.RefreshAsync();
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(SessionDate));

        Assert.True(completed.IsProcessed);
        Assert.NotNull(completed.WorkflowResult);
        Assert.Equal(
            BranchLevelTransition.MarkTestReady,
            completed.WorkflowResult!.ProcessingResult.StateTransition!.Value.Transition);
        Assert.Equal(
            BranchLevelState.TestReady,
            completed.WorkflowResult.RefreshedState.CurrentPractitionerState.GetBranchLevelState(
                BranchCode.FH,
                GlobalLevelId.L1));
    }

    [Fact]
    public async Task LiveSessionControllerCompletesFocusHoldMaintenanceAsMaintenance()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
            Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "android-live-fh-maintenance"));
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        Assert.Equal(AppTrainingSessionType.Maintenance, prepared.Selection.SelectedWork!.SessionType);
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        clock.AdvanceBy(new RuntimeDuration(TimeSpan.FromMinutes(3)));
        await controller.RefreshAsync();
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(SessionDate));

        Assert.True(completed.IsProcessed);
        Assert.NotNull(completed.WorkflowResult);
        Assert.Equal(
            LocalCompletedSessionType.Maintenance,
            completed.WorkflowResult!.ProcessingResult.SessionHistory.SessionType);
        Assert.NotNull(completed.WorkflowResult.ProcessingResult.MaintenanceCheck);
        Assert.Equal(
            MaintenanceCurrencyState.Current,
            completed.WorkflowResult.ProcessingResult.MaintenanceCurrencyResult!.State);
        Assert.Equal(
            BranchLevelState.Owned,
            completed.WorkflowResult.RefreshedState.CurrentPractitionerState.GetBranchLevelState(
                BranchCode.FH,
                GlobalLevelId.L1));
    }

    [Fact]
    public async Task LiveSessionControllerCompletesFocusHoldTestThroughTheFormalGate()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.TestReady));
        await SaveCleanFocusHoldPracticeAsync(
            configuration,
            "fh-ready-a",
            TrainingDate.From(2026, 7, 3));
        await SaveCleanFocusHoldPracticeAsync(
            configuration,
            "fh-ready-b",
            TrainingDate.From(2026, 7, 4));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "android-live-fh-test"));
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        Assert.Equal(AppTrainingSessionType.Test, prepared.Selection.SelectedWork!.SessionType);
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        clock.AdvanceBy(new RuntimeDuration(TimeSpan.FromMinutes(3)));
        await controller.RefreshAsync();
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));

        var missingFailureMode = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CompleteAsync(
                new PreUiLiveSessionCompletionRequest(SessionDate)).AsTask());
        Assert.Contains("must name", missingFailureMode.Message, StringComparison.OrdinalIgnoreCase);

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(
                SessionDate,
                mainFailureModeAvoided: "unmarked drift"));

        Assert.True(completed.IsProcessed);
        Assert.NotNull(completed.WorkflowResult);
        Assert.Equal(GateOutcome.PassOnce, completed.WorkflowResult!.ProcessingResult.FormalGateDecision!.Outcome);
        Assert.NotNull(completed.WorkflowResult.ProcessingResult.FormalTestAttempt);
        Assert.Equal(
            BranchLevelState.PassedOnce,
            completed.WorkflowResult.RefreshedState.CurrentPractitionerState.GetBranchLevelState(
                BranchCode.FH,
                GlobalLevelId.L1));
    }

    [Fact]
    public async Task LiveSessionControllerPersistsFailedStandardWhenReturnIsLateAndTargetChanges()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "android-live-fh-failed-standard"));
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.MarkDrift));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(11));
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.MarkReturn));
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.MarkTargetChange,
                value: "red circle"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(169));
        var review = await controller.RefreshAsync();
        Assert.Equal(RuntimeSessionPhaseKind.Review, review.CurrentPhaseKind);
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(SessionDate));

        Assert.True(completed.IsProcessed);
        Assert.NotNull(completed.WorkflowResult);
        Assert.False(completed.WorkflowResult!.ProcessingResult.SessionHistory.CleanPerformance);
        Assert.False(completed.WorkflowResult.ProcessingResult.StandardEvaluationResult!.Passed);
        Assert.Contains(
            completed.WorkflowResult.ProcessingResult.StandardEvaluationResult.Failures,
            failure => failure.Detail == FocusHoldStandardMeasurements.LateReturnCount);
        Assert.Contains(
            completed.WorkflowResult.ProcessingResult.StandardEvaluationResult.Failures,
            failure => failure.Detail == FocusHoldStandardMeasurements.TargetSubstitutionCount);
    }

    [Fact]
    public async Task LiveSessionControllerCancelsSetupBeforeWorkWithoutRecordingAttempt()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "android-live-fh-abandon"));
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                new ManualRuntimeClock(RuntimeInstant.Zero),
                saveActiveSnapshot: true));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started);

        var abandonedState = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.Abandon,
                value: "user abandoned live session"));

        Assert.True(abandonedState.IsTerminal);
        Assert.Equal(RuntimeSessionLifecycleStatus.Abandoned, abandonedState.LifecycleStatus);

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(SessionDate));

        Assert.False(completed.IsProcessed);
        Assert.True(completed.IsFinalized);
        Assert.Equal(
            PreUiLiveSessionCompletionStatus.CancelledBeforeWork,
            completed.Status);
        Assert.Equal(RuntimeSessionCompletionStatus.Abandoned, completed.RuntimeCompletionStatus);
        Assert.Null(completed.WorkflowResult);
        Assert.Null(await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.RuntimeSession!.SessionId));

        var generated = await new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.GeneratedContent!.PersistenceHandoff!.InstanceId);
        Assert.NotNull(generated);
        Assert.Equal(LocalGeneratedDrillInstanceState.Abandoned, generated.State);
        Assert.Null(generated.ActiveSessionId);
        Assert.Null(generated.ResultEvidenceArtifactId);
        Assert.Null(await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.RuntimeSession!.SessionId));
    }

    [Fact]
    public async Task LiveSessionControllerRecordsAbandonmentAfterTrainingWorkStarts()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "android-live-fh-abandon-active"));
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
        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.Abandon,
                value: "user stopped active hold"));
        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(SessionDate));

        Assert.True(completed.IsProcessed);
        Assert.True(completed.IsFinalized);
        Assert.NotNull(completed.WorkflowResult);
        Assert.Equal(
            RuntimeSessionCompletionStatus.Abandoned,
            completed.WorkflowResult!.ProcessingResult.CompletionStatus);
        Assert.False(completed.WorkflowResult.ProcessingResult.SessionHistory.CleanPerformance);
        Assert.Single(completed.WorkflowResult.ProcessingResult.EvidenceArtifacts);
    }

    [Fact]
    public async Task WorkingMemoryLiveFlowHidesAnswerKeyAndRequiresOneSubmission()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce),
            Status(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    SessionDate,
                    new RequestedTrainingWork(
                        BranchCode.WM,
                        GlobalLevelId.L1,
                        DrillId.WM1DelayedReconstruction,
                        AppTrainingSessionType.Practice)),
                "wm-live-visibility"));
        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);

        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        var prep = await controller.RefreshAsync();
        Assert.DoesNotContain(prep.CurrentMaterials, IsHiddenAnswerMaterial);

        var encode = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.EncodeWindow, encode.CurrentPhaseKind);
        Assert.Contains(encode.CurrentMaterials, material => material.Kind == "EncodeItem");
        Assert.DoesNotContain(encode.CurrentMaterials, IsHiddenAnswerMaterial);

        var delay = await AdvancePhaseAsync(controller, clock, encode);
        Assert.Equal(RuntimeSessionPhaseKind.DelayWindow, delay.CurrentPhaseKind);
        Assert.Empty(delay.CurrentMaterials);

        var reconstruct = await AdvancePhaseAsync(controller, clock, delay);
        Assert.Equal(RuntimeSessionPhaseKind.ReconstructionInput, reconstruct.CurrentPhaseKind);
        Assert.DoesNotContain(reconstruct.CurrentMaterials, IsHiddenAnswerMaterial);
        Assert.True(CommandState(reconstruct, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
        Assert.False(CommandState(reconstruct, RuntimeInputCommandKind.FinishPhase).IsAvailable);

        var submitted = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.SubmitAnswer,
                value: "one, two, three, four, five"));
        Assert.False(CommandState(submitted, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
        Assert.True(CommandState(submitted, RuntimeInputCommandKind.FinishPhase).IsAvailable);
    }

    [Fact]
    public async Task GlobalReviewLiveFlowRunsWorkAuditDelayThenReconstruction()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.TI, GlobalLevelId.L5, BranchLevelState.Maintenance));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "ti2-live-order"));
        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);

        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        var active = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, active.CurrentPhaseKind);
        Assert.Contains(active.CurrentMaterials, material => material.Kind == "ComponentPayload");
        Assert.DoesNotContain(active.CurrentMaterials, material => material.Kind == "BranchScoringKey");
        Assert.DoesNotContain(active.CurrentMaterials, material => material.Kind == "AuditPayload");
        Assert.DoesNotContain(active.CurrentMaterials, material => material.Kind == "DelayedReconstructionPayload");
        Assert.False(CommandState(active, RuntimeInputCommandKind.FinishPhase).IsAvailable);

        await controller.HandleCommandAsync(new PreUiLiveSessionCommandRequest(
            RuntimeInputCommandKind.SubmitAnswer,
            value: "separate component evidence"));
        var audit = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.Audit, audit.CurrentPhaseKind);
        Assert.Contains(audit.CurrentMaterials, material => material.Kind == "AuditPayload");
        Assert.DoesNotContain(audit.CurrentMaterials, material => material.Kind == "DelayedReconstructionPayload");
        Assert.True(CommandState(audit, RuntimeInputCommandKind.StartAudit).IsAvailable);
        Assert.False(CommandState(audit, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);

        var auditStarted = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.StartAudit));
        Assert.True(CommandState(auditStarted, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
        await controller.HandleCommandAsync(new PreUiLiveSessionCommandRequest(
            RuntimeInputCommandKind.SubmitAnswer,
            value: "supported findings"));
        var delay = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.DelayWindow, delay.CurrentPhaseKind);
        Assert.Empty(delay.CurrentMaterials);

        var reconstruct = await AdvancePhaseAsync(controller, clock, delay);
        Assert.Equal(RuntimeSessionPhaseKind.ReconstructionInput, reconstruct.CurrentPhaseKind);
        Assert.Contains(reconstruct.CurrentMaterials, material => material.Kind == "DelayedReconstructionPayload");
        Assert.DoesNotContain(reconstruct.CurrentMaterials, material => material.Kind == "AuditPayload");
        Assert.True(CommandState(reconstruct, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
        Assert.False(CommandState(reconstruct, RuntimeInputCommandKind.FinishPhase).IsAvailable);
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

    private static async ValueTask<PreUiTrainingWorkflowPreparationResult> PrepareCuePracticeAsync(
        PreUiTrainingWorkflowService workflow,
        string sessionId)
    {
        var prepared = await workflow.PrepareNextSessionAsync(new PreUiTrainingWorkflowPreparationRequest(
            new NextTrainingWorkSelectionQuery(
                SessionDate,
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

    private static RuntimeSessionCompletionResult CompletedPracticeResult(
        string sessionId,
        RuntimeSessionDefinition session)
    {
        var log = RuntimeEventLog.Start(sessionId, session, RuntimeInstant.Zero);
        log.Append(
            RuntimeEventKind.AnswerSubmitted,
            Instant(90),
            "cue-response",
            RuntimeSessionPhaseKind.CueResponse,
            [
                new RuntimeEventFact("output_sample", "FS cue sequence completed with cue-bound target switches."),
                new RuntimeEventFact("score", "correct_responses=4; anticipatory_switches=0"),
                new RuntimeEventFact("correct_responses", "4"),
                new RuntimeEventFact("anticipatory_switches", "0"),
            ]);
        log.Append(RuntimeEventKind.SessionCompleted, Instant(95));

        var scoringEvents = log.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        var evidenceDraft = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            sessionId,
            session,
            SessionDate,
            RuntimeEvidenceCaptureKind.BestSet,
            log.Events,
            scoringEvents));

        return RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            sessionId,
            session,
            RuntimeSessionCompletionStatus.Completed,
            [CompletedPhase("cue-response", RuntimeSessionPhaseKind.CueResponse, 0, 95)],
            log.Events,
            scoringEvents,
            [evidenceDraft]));
    }

    private static RuntimeSessionCompletionResult AbandonedResult(
        string sessionId,
        RuntimeSessionDefinition session)
    {
        var log = RuntimeEventLog.Start(sessionId, session, RuntimeInstant.Zero);
        log.Append(
            RuntimeEventKind.SessionAbandoned,
            Instant(15),
            facts: [new RuntimeEventFact("abandon_reason", "user left before cue evidence")]);

        return RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            sessionId,
            session,
            RuntimeSessionCompletionStatus.Abandoned,
            [CompletedPhase("instruction", RuntimeSessionPhaseKind.InstructionPrep, 0, 15)],
            log.Events,
            [],
            []));
    }

    private static RuntimePersistenceHandoffMetadata Metadata(
        bool cleanPerformance,
        string notes)
    {
        return new RuntimePersistenceHandoffMetadata(
            SessionDate,
            LocalSessionIntensity.Moderate,
            cleanPerformance,
            notes);
    }

    private static RuntimeCompletedSessionPhase CompletedPhase(
        string id,
        RuntimeSessionPhaseKind kind,
        int startedAtSeconds,
        int completedAtSeconds)
    {
        var startedAt = Instant(startedAtSeconds);
        var completedAt = Instant(completedAtSeconds);
        return new RuntimeCompletedSessionPhase(
            RuntimeSessionPhaseDefinition.Manual(id, kind),
            startedAt,
            completedAt,
            completedAt.ElapsedSince(startedAt),
            RuntimeSessionPhaseCompletionCause.Explicit);
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

    private static async ValueTask<PreUiLiveSessionState> AdvancePhaseAsync(
        PreUiLiveSessionController controller,
        ManualRuntimeClock clock,
        PreUiLiveSessionState state)
    {
        if (state.Timer.IsTimed)
        {
            clock.AdvanceBy(new RuntimeDuration(state.Timer.Remaining!.Value));
            return await controller.RefreshAsync();
        }

        return await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
    }

    private static bool IsHiddenAnswerMaterial(PreUiLiveSessionMaterialState material)
    {
        return material.Kind is
            "ExpectedActiveTarget" or
            "ExpectedAction" or
            "ExpectedReconstruction" or
            "FinalExpectedOutput" or
            "MatchTruth" or
            "SeededError" or
            "ExpectedFinding" or
            "ExpectedClassification" or
            "ExpectedMapping" or
            "BranchScoringKey";
    }

    private static async ValueTask SaveStateAsync(
        AppStartupConfiguration configuration,
        params BranchLevelStatus[] statuses)
    {
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions)
            .SaveAsync(new PractitionerState(statuses));
    }

    private static async ValueTask SaveCleanFocusHoldPracticeAsync(
        AppStartupConfiguration configuration,
        string sessionId,
        TrainingDate date)
    {
        var artifactId = $"{sessionId}-artifact";
        await new LocalEvidenceArtifactStore(configuration.LocalDatabaseOptions).SaveAsync(
            new LocalEvidenceArtifactRecord(
                artifactId,
                new LocalProgrammingEventReference(
                    sessionId,
                    LocalProgrammingEventKind.Practice,
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    DrillId.FH1TargetHold),
                new EvidenceArtifact(
                    EvidenceArtifactCategory.Practice,
                    date,
                    [new ObservableEvidence(ObservableEvidenceKind.OutputSample, "Clean Target Hold evidence.")],
                    "Clean Target Hold practice.")));
        await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions).SaveAsync(
            new LocalSessionHistoryRecord(
                sessionId,
                date,
                LocalCompletedSessionType.Practice,
                [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L1)],
                DrillId.FH1TargetHold,
                transferTask: null,
                LocalSessionIntensity.Moderate,
                TrainingLoadProfileCatalog.Get(BranchCode.FH, GlobalLevelId.L1)
                    .TargetStage.LoadVariables,
                cleanPerformance: true,
                "Clean Target Hold practice.",
                recoveryMarked: false,
                deloadMarked: false,
                [artifactId]));
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static PreUiLiveSessionCommandState CommandState(
        PreUiLiveSessionState state,
        RuntimeInputCommandKind command)
    {
        return state.Commands.Single(item => item.Command == command);
    }

    private static RuntimeInstant Instant(int seconds)
    {
        return new RuntimeInstant(TimeSpan.FromSeconds(seconds));
    }
}
