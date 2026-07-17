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
    public async Task DelayedRefreshPresentsTheLatestDueCue()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "workflow-delayed-cue-refresh");
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
        var dueCues = prepared.RuntimeSession!.CueSchedule!.Cues.Take(3).ToArray();

        clock.AdvanceTo(dueCues[^1].ScheduledAt);
        var refreshed = await controller.RefreshAsync();

        Assert.Equal(3, refreshed.Evidence.CueCount);
        Assert.Equal(dueCues[^1].Id, refreshed.ActiveCue?.CueId);
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
    public async Task StartingPreparedSessionWaitsInInstructionPrepUntilUserBeginsWork()
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

        var initial = controller.CaptureState();

        Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, initial.CurrentPhaseKind);
        Assert.False(initial.Timer.IsTimed);
        Assert.Equal(TimeSpan.Zero, initial.Timer.Elapsed);
        Assert.Null(initial.ActiveCue);
        Assert.True(CommandState(initial, RuntimeInputCommandKind.FinishPhase).IsAvailable);
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
    public async Task FailedCueExposesStructuredCorrectionAndRecordsExactRecovery()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await PrepareCuePracticeAsync(workflow, "workflow-cue-correction");
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
        var cueState = await controller.RefreshAsync();
        var cue = Assert.IsType<PreUiLiveSessionCueState>(cueState.ActiveCue);
        var expectedResponse = Assert.IsType<string>(cue.ExpectedResponse);

        var missed = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.RespondToCue,
                cue.CueId,
                "withhold"));

        var correction = Assert.IsType<PreUiLiveSessionCorrectionState>(missed.PendingCorrection);
        Assert.Equal(cue.CueId, correction.CueId);
        Assert.Equal(cue.Cue, correction.Cue);
        Assert.Equal("withhold", correction.SubmittedResponse);
        Assert.Contains(expectedResponse, correction.ResponseOptions);
        Assert.Contains("withhold", correction.ResponseOptions);
        Assert.True(CommandState(missed, RuntimeInputCommandKind.Correct).IsAvailable);
        var presentation = TrainingPresentationMapper.FromLiveSession(missed);
        Assert.NotNull(presentation.PendingCorrection);
        Assert.Equal(cue.Cue, presentation.PendingCorrection.Cue);

        var recovered = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.Correct,
                correction.SourceEventSequenceNumber.ToString(),
                expectedResponse));

        Assert.Null(recovered.PendingCorrection);
        Assert.Equal(expectedResponse, recovered.CurrentFocusTarget);
        Assert.False(CommandState(recovered, RuntimeInputCommandKind.Correct).IsAvailable);
        Assert.Equal(1, recovered.Evidence.CorrectionCount);
        Assert.Contains(started.CommandHandler!.Events, runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.CorrectionSubmitted &&
            runtimeEvent.Facts.Any(fact =>
                fact.Name == "correction_outcome" && fact.Value == "correct") &&
            runtimeEvent.Facts.Any(fact =>
                fact.Name == "corrected_cue_id" && fact.Value == cue.CueId));
    }

    [Fact]
    public async Task InvalidCueRemainsTappableSoAnInhibitionErrorIsObservable()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    SessionDate,
                    new RequestedTrainingWork(
                        BranchCode.FS,
                        GlobalLevelId.L3,
                        DrillId.FS2InvalidCueFilter,
                        AppTrainingSessionType.Maintenance)),
                "workflow-invalid-cue-capture"));
        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);
        var invalidCue = prepared.RuntimeSession!.CueSchedule!.Cues.First(cue =>
            cue.ResponseExpectation == RuntimeCueResponseExpectation.NoResponseExpected);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession,
            started,
            saveActiveSnapshot: false);

        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        clock.AdvanceBy(new RuntimeDuration(invalidCue.ScheduledAt.Offset));
        var withhold = await controller.RefreshAsync();

        Assert.Equal(invalidCue.Id, withhold.ActiveCue?.CueId);
        Assert.Equal(RuntimeCueResponseExpectation.NoResponseExpected, withhold.ActiveCue?.ResponseExpectation);
        Assert.Equal(invalidCue.ResponseWindow.Value, withhold.ActiveCue?.Remaining);
        Assert.False(string.IsNullOrWhiteSpace(withhold.CurrentFocusTarget));
        Assert.True(CommandState(withhold, RuntimeInputCommandKind.RespondToCue).IsAvailable);
        var presentation = TrainingPresentationMapper.FromLiveSession(withhold);
        Assert.True(presentation.ActiveCue?.ExpectedActionIsHidden);
        Assert.DoesNotContain("invalid", presentation.CurrentInstruction, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("withhold", presentation.CurrentInstruction, StringComparison.OrdinalIgnoreCase);

        var tapped = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.RespondToCue,
                invalidCue.Id,
                "switched anyway"));

        Assert.Equal(RuntimeCueResponseOutcome.Incorrect, tapped.LastCommand?.CueOutcome);
        Assert.Equal(1, tapped.Evidence.ErrorCount);
        Assert.NotNull(tapped.PendingCorrection);
        Assert.Null(tapped.CurrentFocusTarget);
        Assert.Contains("withhold", tapped.PendingCorrection.ResponseOptions);
    }

    [Fact]
    public async Task FocusHoldDisruptionExposesAResumeControlAndRecordsTheSemanticResponse()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.AI, GlobalLevelId.L3, BranchLevelState.Maintenance));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.AI, GlobalLevelId.L3);
        var prepared = await workflow.PrepareNextSessionAsync(new PreUiTrainingWorkflowPreparationRequest(
            new NextTrainingWorkSelectionQuery(
                SessionDate,
                new RequestedTrainingWork(
                    BranchCode.AI,
                    GlobalLevelId.L3,
                    DrillId.AI2DisruptionRecovery,
                    AppTrainingSessionType.Maintenance,
                    profile.TargetStage.LoadVariables)),
            PromptContentKind.EquivalentPrompt,
            "ai2-fh-source",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            new GeneratedContentSeed("workflow-ai2-fh-disruption-seed"),
            "workflow-ai2-fh-disruption"));
        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);
        Assert.Equal(DrillId.FH2DistractorHold, prepared.RuntimeSession?.SessionDefinition?.SourceDrill);
        var interruption = prepared.RuntimeSession!.CueSchedule!.Cues.Single(cue =>
            cue.Kind == RuntimeCueKind.Interruption &&
            string.Equals(cue.ExpectedResponse, "resume", StringComparison.OrdinalIgnoreCase));
        var distractor = prepared.RuntimeSession.CueSchedule.Cues.First(cue =>
            cue.Kind == RuntimeCueKind.Interruption &&
            cue.ResponseExpectation == RuntimeCueResponseExpectation.NoResponseExpected &&
            cue.ScheduledAt.Offset < interruption.ScheduledAt.Offset);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession,
            started,
            saveActiveSnapshot: false);

        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        clock.AdvanceTo(distractor.ScheduledAt);
        var distracted = await controller.RefreshAsync();

        Assert.Equal(RuntimeCueResponseExpectation.NoResponseExpected, distracted.ActiveCue?.ResponseExpectation);
        Assert.True(distracted.ActiveCue?.IsControlledDistractor);
        Assert.DoesNotContain(distracted.Commands, command =>
            command.Command == RuntimeInputCommandKind.RespondToCue && command.IsAvailable);
        Assert.DoesNotContain(
            "source rule still applies",
            TrainingPresentationMapper.FromLiveSession(distracted).CurrentInstruction,
            StringComparison.OrdinalIgnoreCase);

        clock.AdvanceTo(interruption.ScheduledAt);
        var disrupted = await controller.RefreshAsync();

        Assert.Equal(RuntimeCueKind.Interruption, disrupted.ActiveCue?.Kind);
        Assert.True(CommandState(disrupted, RuntimeInputCommandKind.RespondToCue).IsAvailable);

        var resumed = await controller.HandleCommandAsync(new PreUiLiveSessionCommandRequest(
            RuntimeInputCommandKind.RespondToCue,
            interruption.Id,
            "resume"));

        Assert.True(resumed.LastCommand?.IsAccepted);
        Assert.Equal(RuntimeCueResponseOutcome.Correct, resumed.LastCommand?.CueOutcome);
    }

    [Fact]
    public async Task PairChoicesPersistIndividuallyAndThePhaseUnlocksOnlyAfterEveryPair()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.DE, GlobalLevelId.L1, BranchLevelState.Training));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    SessionDate,
                    new RequestedTrainingWork(
                        BranchCode.DE,
                        GlobalLevelId.L1,
                        DrillId.DE1PairDiscrimination,
                        AppTrainingSessionType.Practice)),
                "workflow-pair-item-persistence"));
        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: true));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: true);
        var active = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        var pairs = active.CurrentMaterials
            .Where(material => material.Kind == "DiscriminationPair")
            .ToArray();
        Assert.True(pairs.Length > 1);

        for (var index = 0; index < pairs.Length; index++)
        {
            active = await controller.HandleCommandAsync(new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.SubmitAnswer,
                pairs[index].Name,
                $"{pairs[index].Name}=same"));

            Assert.Equal(index + 1, active.Evidence.AnswerCount);
            if (index < pairs.Length - 1)
            {
                Assert.True(CommandState(active, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
                Assert.False(CommandState(active, RuntimeInputCommandKind.FinishPhase).IsAvailable);
            }
        }

        Assert.False(CommandState(active, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
        Assert.True(CommandState(active, RuntimeInputCommandKind.FinishPhase).IsAvailable);
        var stored = await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.RuntimeSession!.SessionId);
        Assert.Equal(pairs.Length, stored?.RuntimeEvents.Count(runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted.ToString()));
    }

    [Fact]
    public async Task LiveSessionControllerExposesOneWanderActionAndNeverASeparateReturn()
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
        Assert.DoesNotContain(active.Commands, command => command.Command == RuntimeInputCommandKind.MarkReturn);
        Assert.True(CommandState(active, RuntimeInputCommandKind.MarkTargetChange).IsAvailable);
        Assert.Equal("Target changed", CommandState(active, RuntimeInputCommandKind.MarkTargetChange).Label);
        Assert.DoesNotContain(
            active.Commands,
            command => command.Command == RuntimeInputCommandKind.SubmitAnswer);
        Assert.Equal("Stop early", CommandState(active, RuntimeInputCommandKind.Abandon).Label);
        Assert.DoesNotContain(active.Commands.Select(command => command.Label), label =>
            label is "Abandon" or "Submit" or "Drift");

        var firstWander = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.MarkDrift));

        Assert.True(CommandState(firstWander, RuntimeInputCommandKind.MarkDrift).IsAvailable);
        Assert.Equal(1, firstWander.Evidence.DriftCount);
        Assert.DoesNotContain(firstWander.Commands, command => command.Command == RuntimeInputCommandKind.MarkReturn);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var rejectedLegacyReturn = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.MarkReturn));
        var secondWander = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.MarkDrift));

        Assert.False(rejectedLegacyReturn.LastCommand!.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.CommandNotSupportedByDrill, rejectedLegacyReturn.LastCommand.InvalidReason);
        Assert.Equal(2, secondWander.Evidence.DriftCount);
        Assert.DoesNotContain(started.CommandHandler!.Events, runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.RecoveryCompleted);

        clock.AdvanceBy(new RuntimeDuration(secondWander.Timer.Remaining!.Value));
        var terminal = await controller.RefreshAsync();

        Assert.True(terminal.IsTerminal);
        Assert.Equal(RuntimeSessionLifecycleStatus.Completed, terminal.LifecycleStatus);
        Assert.DoesNotContain(
            terminal.Commands,
            command => command.Command == RuntimeInputCommandKind.MarkTargetChange && command.IsAvailable);
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
        var terminal = await controller.RefreshAsync();
        Assert.True(terminal.IsTerminal);
        Assert.Equal(RuntimeSessionLifecycleStatus.Completed, terminal.LifecycleStatus);
        Assert.Contains(
            "Runtime advanced due timed phase events",
            terminal.Detail,
            StringComparison.OrdinalIgnoreCase);

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
        var evidenceDescriptions = string.Join(
            "; ",
            completed.WorkflowResult.ProcessingResult.EvidenceArtifacts
                .SelectMany(artifact => artifact.Artifact.ObservableEvidence)
                .Select(evidence => evidence.Description));
        Assert.DoesNotContain("return_count=", evidenceDescriptions, StringComparison.Ordinal);
        Assert.DoesNotContain("late_return_count=", evidenceDescriptions, StringComparison.Ordinal);
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
    public async Task LegacyFocusHoldReviewIsCompletedWithoutPresentingAQuestion()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(SessionDate),
                "legacy-focus-review"));
        var current = prepared.RuntimeSession!;
        var legacy = SelectedWorkRuntimeSessionPreparationResult.Prepared(
            current.SessionId,
            current.GeneratedContent,
            current.SessionDefinition!,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual(
                    "instruction-prep",
                    RuntimeSessionPhaseKind.InstructionPrep),
                RuntimeSessionPhaseDefinition.Timed(
                    "active-work",
                    RuntimeSessionPhaseKind.ActiveWork,
                    RuntimeDuration.FromSeconds(120)),
                RuntimeSessionPhaseDefinition.Manual(
                    "review",
                    RuntimeSessionPhaseKind.Review),
            ]),
            current.CueSchedule,
            current.InputOptions,
            current.InputMaterials,
            current.ExpectedEvidenceFacts);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                legacy,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            legacy,
            started,
            saveActiveSnapshot: false);

        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(120));
        var terminal = await controller.RefreshAsync();

        Assert.True(terminal.IsTerminal);
        Assert.Equal(RuntimeSessionLifecycleStatus.Completed, terminal.LifecycleStatus);
        Assert.NotEqual(RuntimeSessionPhaseKind.Review, terminal.CurrentPhaseKind);
        Assert.DoesNotContain(
            terminal.Commands,
            command => command.Command == RuntimeInputCommandKind.MarkTargetChange && command.IsAvailable);
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
        Assert.DoesNotContain(resumed.Commands, command => command.Command == RuntimeInputCommandKind.MarkReturn);
        Assert.False(CommandState(resumed, RuntimeInputCommandKind.Pause).IsAvailable);

        restoreClock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var secondWander = await resumedController.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.MarkDrift, "drift-after-resume"));

        Assert.True(secondWander.LastCommand!.IsAccepted);
        Assert.Equal(TimeSpan.FromSeconds(25), secondWander.Timer.Elapsed);
        Assert.Equal(2, secondWander.Evidence.DriftCount);
        Assert.DoesNotContain(started.CommandHandler!.Events, runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.RecoveryCompleted);
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
    public async Task LiveSessionControllerStartsAndCompletesFocusHoldTestWithoutASelfDescription()
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

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(SessionDate));

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
    public async Task LiveSessionControllerDoesNotScoreReturnTimeAndStillFailsATargetChange()
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
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.MarkTargetChange,
                value: "red circle"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(169));
        var terminal = await controller.RefreshAsync();
        Assert.True(terminal.IsTerminal);

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(SessionDate));

        Assert.True(completed.IsProcessed);
        Assert.NotNull(completed.WorkflowResult);
        Assert.False(completed.WorkflowResult!.ProcessingResult.SessionHistory.CleanPerformance);
        Assert.False(completed.WorkflowResult.ProcessingResult.StandardEvaluationResult!.Passed);
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
    public async Task InhibitionLiveFlowRequiresRuleDeclarationBeforeCueSet()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Maintenance));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    SessionDate,
                    new RequestedTrainingWork(
                        BranchCode.IR,
                        GlobalLevelId.L2,
                        DrillId.IR2ExceptionRule,
                        AppTrainingSessionType.Maintenance)),
                "ir-live-declaration"));
        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);

        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                new ManualRuntimeClock(RuntimeInstant.Zero),
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        var declaration = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal("rule-declaration", declaration.CurrentPhaseId);
        Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, declaration.CurrentPhaseKind);
        Assert.Contains(declaration.CurrentMaterials, material => material.Kind == "RuleStatement");
        Assert.Contains(declaration.CurrentMaterials, material => material.Kind == "ExceptionDefinition");
        Assert.True(CommandState(declaration, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
        Assert.False(CommandState(declaration, RuntimeInputCommandKind.FinishPhase).IsAvailable);

        var submitted = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.SubmitAnswer,
                value: "Tap round; withhold angular; state every listed exception."));
        Assert.False(CommandState(submitted, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
        Assert.True(CommandState(submitted, RuntimeInputCommandKind.FinishPhase).IsAvailable);

        var cues = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal("cue-response", cues.CurrentPhaseId);
        Assert.Equal(RuntimeSessionPhaseKind.CueResponse, cues.CurrentPhaseKind);
    }

    [Fact]
    public async Task PressureRepeatWithInhibitionSourceCannotSkipRuleDeclaration()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.AI, GlobalLevelId.L1, BranchLevelState.Maintenance));
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.AI, GlobalLevelId.L1);
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionAsync(
            new PreUiTrainingWorkflowPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    SessionDate,
                    new RequestedTrainingWork(
                        BranchCode.AI,
                        GlobalLevelId.L1,
                        DrillId.AI1PressureRepeat,
                        AppTrainingSessionType.Maintenance,
                        profile.TargetStage.LoadVariables)),
                PromptContentKind.EquivalentPrompt,
                "ai-l1-pressure-repeat-ir-l3",
                PromptFreshnessPolicy.FreshEquivalentRequired,
                new GeneratedContentSeed("ai-ir-live-declaration"),
                "ai-ir-live-declaration",
                additionalCriticalConstraints:
                [
                    new CriticalConstraint("Original standard cannot be lowered."),
                ]));
        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);
        Assert.Equal(DrillId.IR2ExceptionRule, prepared.RuntimeSession!.SessionDefinition!.SourceDrill);
        var preflight = TrainingPresentationMapper.FromPreflight(prepared);
        Assert.Contains(preflight.Work!.Exercise.SetupItems, item =>
            item.StartsWith("KEEP PASSING  88% correct", StringComparison.Ordinal));

        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession,
                new ManualRuntimeClock(RuntimeInstant.Zero),
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession,
            started,
            saveActiveSnapshot: false);
        Assert.Contains(controller.CaptureState().CurrentMaterials, material =>
            material.Kind == "SourceBranchStandard");

        var declaration = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal("rule-declaration", declaration.CurrentPhaseId);
        Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, declaration.CurrentPhaseKind);
        Assert.Contains(declaration.CurrentMaterials, material => material.Kind == "RuleStatement");
        Assert.Contains(declaration.CurrentMaterials, material => material.Kind == "ExceptionDefinition");
        Assert.DoesNotContain(declaration.CurrentMaterials, material => material.Kind == "SourceBranchStandard");
        Assert.True(CommandState(declaration, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
        Assert.False(CommandState(declaration, RuntimeInputCommandKind.FinishPhase).IsAvailable);

        var submitted = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.SubmitAnswer,
                value: "Tap round; withhold angular; apply every listed exception first."));
        Assert.True(CommandState(submitted, RuntimeInputCommandKind.FinishPhase).IsAvailable);
    }

    [Fact]
    public async Task SeededAuditShowsSourceThenLocksReportAfterDelay()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    SessionDate,
                    new RequestedTrainingWork(
                        BranchCode.DE,
                        GlobalLevelId.L3,
                        DrillId.DE2SeededAudit,
                        AppTrainingSessionType.Maintenance)),
                "de2-live-locked-original"));
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

        var source = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal("source-review", source.CurrentPhaseId);
        Assert.Equal(RuntimeSessionPhaseKind.EncodeWindow, source.CurrentPhaseKind);
        Assert.Contains(source.CurrentMaterials, material => material.Kind == "AuditReference");
        Assert.DoesNotContain(source.CurrentMaterials, material => material.Kind == "LockedOriginalOutput");
        Assert.DoesNotContain(source.CurrentMaterials, IsHiddenAnswerMaterial);

        var delay = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.DelayWindow, delay.CurrentPhaseKind);
        Assert.Empty(delay.CurrentMaterials);

        var audit = await AdvancePhaseAsync(controller, clock, delay);
        Assert.Equal(RuntimeSessionPhaseKind.Audit, audit.CurrentPhaseKind);
        Assert.Empty(audit.CurrentMaterials);
        Assert.True(CommandState(audit, RuntimeInputCommandKind.StartAudit).IsAvailable);

        var auditStarted = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.StartAudit));
        Assert.Contains(auditStarted.CurrentMaterials, material => material.Kind == "LockedOriginalOutput");
        Assert.DoesNotContain(auditStarted.CurrentMaterials, material => material.Kind == "AuditReference");
        Assert.Contains(auditStarted.CurrentMaterials, material => material.Kind == "AuditInstruction");
        Assert.DoesNotContain(auditStarted.CurrentMaterials, IsHiddenAnswerMaterial);
    }

    [Fact]
    public async Task IntegratedComponentPromptsDisappearBeforeReconstruction()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.WM, GlobalLevelId.L5, BranchLevelState.Maintenance));
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    SessionDate,
                    new RequestedTrainingWork(
                        BranchCode.WM,
                        GlobalLevelId.L5,
                        DrillId.WM2MentalTransform,
                        AppTrainingSessionType.Maintenance)),
                "wm2-live-integrated-components"));
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
        Assert.Contains(active.CurrentMaterials, material => material.Kind == "ComponentEvidenceRequirement");
        await controller.HandleCommandAsync(new PreUiLiveSessionCommandRequest(
            RuntimeInputCommandKind.SubmitAnswer,
            value: "FH=held; FS=switched"));

        var state = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        while (state.CurrentPhaseKind != RuntimeSessionPhaseKind.ReconstructionInput)
        {
            state = await AdvancePhaseAsync(controller, clock, state);
        }

        Assert.DoesNotContain(state.CurrentMaterials, material => material.Kind == "ComponentPayload");
        Assert.Contains(state.CurrentMaterials, material => material.Kind == "ComponentEvidenceRequirement");
        Assert.DoesNotContain(state.CurrentMaterials, IsHiddenAnswerMaterial);
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
        var componentCount = prepared.RuntimeSession!.InputMaterials.Count(material =>
            material.Kind == GeneratedContentMaterialKind.ComponentPayload);
        Assert.True(componentCount >= 3);
        var componentNames = new HashSet<string>(StringComparer.Ordinal);
        var componentState = active;
        for (var index = 0; index < componentCount; index++)
        {
            Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, componentState.CurrentPhaseKind);
            Assert.True(GeneratedRuntimeComponentPhaseIdentity.TryGetMaterialName(
                componentState.CurrentPhaseId,
                out var activeMaterialName));
            var component = Assert.Single(componentState.CurrentMaterials);
            Assert.Equal("ComponentPayload", component.Kind);
            Assert.Equal(activeMaterialName, component.Name);
            Assert.True(componentNames.Add(component.Name));
            Assert.DoesNotContain(componentState.CurrentMaterials, material => material.Kind == "CompositeTaskPrompt");
            Assert.DoesNotContain(componentState.CurrentMaterials, material => material.Kind == "ComponentEvidenceRequirement");
            Assert.DoesNotContain(componentState.CurrentMaterials, material => material.Kind == "BranchScoringKey");
            Assert.DoesNotContain(componentState.CurrentMaterials, material => material.Kind == "AuditPayload");
            Assert.DoesNotContain(componentState.CurrentMaterials, material => material.Kind == "DelayedReconstructionPayload");
            Assert.DoesNotContain(componentState.CurrentMaterials, IsHiddenAnswerMaterial);
            Assert.False(CommandState(componentState, RuntimeInputCommandKind.FinishPhase).IsAvailable);

            var answered = await controller.HandleCommandAsync(new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.SubmitAnswer,
                value: $"component-{index + 1}=separate evidence"));
            Assert.True(answered.LastCommand!.IsAccepted);
            Assert.True(CommandState(answered, RuntimeInputCommandKind.FinishPhase).IsAvailable);
            componentState = await controller.HandleCommandAsync(
                new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        }

        Assert.Equal(componentCount, componentNames.Count);
        var audit = componentState;
        Assert.Equal(RuntimeSessionPhaseKind.Audit, audit.CurrentPhaseKind);
        Assert.Empty(audit.CurrentMaterials);
        Assert.True(CommandState(audit, RuntimeInputCommandKind.StartAudit).IsAvailable);
        Assert.False(CommandState(audit, RuntimeInputCommandKind.SubmitAnswer).IsAvailable);

        var auditStarted = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.StartAudit));
        Assert.Contains(auditStarted.CurrentMaterials, material =>
            material.Kind == "AuditPayload" &&
            material.Value.Contains("locked component report", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(auditStarted.CurrentMaterials, material => material.Kind == "DelayedReconstructionPayload");
        Assert.DoesNotContain(auditStarted.CurrentMaterials, IsHiddenAnswerMaterial);
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
        Assert.Contains(reconstruct.CurrentMaterials, material =>
            material.Kind == "DelayedReconstructionPayload" &&
            material.Value.Contains("exact locked component report", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(reconstruct.CurrentMaterials, material => material.Kind == "AuditPayload");
        Assert.DoesNotContain(reconstruct.CurrentMaterials, IsHiddenAnswerMaterial);
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
            "ExpectedRule" or
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
