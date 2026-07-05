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

    private static async ValueTask SaveStateAsync(
        AppStartupConfiguration configuration,
        params BranchLevelStatus[] statuses)
    {
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions)
            .SaveAsync(new PractitionerState(statuses));
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static RuntimeInstant Instant(int seconds)
    {
        return new RuntimeInstant(TimeSpan.FromSeconds(seconds));
    }
}
