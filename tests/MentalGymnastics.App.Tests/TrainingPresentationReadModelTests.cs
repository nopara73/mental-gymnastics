using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class TrainingPresentationReadModelTests : IDisposable
{
    private static readonly TrainingDate Today = TrainingDate.From(2026, 7, 5);

    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CurrentStatePresentationPrioritizesDecayBeforeDashboardDetail()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.WM, GlobalLevelId.L2, BranchLevelState.Decayed),
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training));

        var state = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(Today));
        var presentation = TrainingPresentationMapper.FromCurrentState(state);

        Assert.Equal(TrainingPresentationPriorityKind.DecayRestoration, presentation.Priority);
        Assert.Equal(TrainingPresentationPrimaryActionKind.RestoreDecayedWork, presentation.PrimaryAction);
        Assert.NotNull(presentation.MaintenanceDecayPriority);
        Assert.Equal(TrainingMaintenanceDecayPriorityKind.DecayRestoration, presentation.MaintenanceDecayPriority!.Kind);
        Assert.Equal(BranchCode.WM, presentation.MaintenanceDecayPriority.Branch);
        Assert.Equal(GlobalLevelId.L2, presentation.MaintenanceDecayPriority.Level);
        Assert.True(presentation.MaintenanceDecayPriority.BlocksAdvancement);
        Assert.False(presentation.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task CurrentStatePresentationKeepsFirstRunPrescribedWorkStartable()
    {
        var configuration = Configuration();
        var state = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(Today));
        var presentation = TrainingPresentationMapper.FromCurrentState(state);

        Assert.NotEmpty(state.AvailableNextWork);
        Assert.NotEmpty(state.BlockedAdvancement);
        Assert.Equal(TrainingPresentationPriorityKind.PrescribedWork, presentation.Priority);
        Assert.Equal(TrainingPresentationPrimaryActionKind.StartPrescribedWork, presentation.PrimaryAction);
        Assert.True(presentation.PrimaryActionEnabled);
        Assert.NotNull(presentation.PrimaryPrescribedWork);
        Assert.NotNull(presentation.UrgentBlocker);
        Assert.False(presentation.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task SelectionPresentationKeepsMaintenanceStartableWithoutSofteningBlocker()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance));

        var selection = await new NextTrainingWorkSelector(configuration).SelectAsync(
            new NextTrainingWorkSelectionQuery(Today));
        var presentation = TrainingPresentationMapper.FromSelection(selection);

        Assert.Equal(NextTrainingWorkSelectionKind.MaintenanceNeeded, selection.Kind);
        Assert.Equal(TrainingPresentationPriorityKind.MaintenanceDue, presentation.Priority);
        Assert.Equal(TrainingPresentationPrimaryActionKind.StartMaintenance, presentation.PrimaryAction);
        Assert.True(presentation.PrimaryActionEnabled);
        Assert.NotNull(presentation.PrimaryPrescribedWork);
        Assert.Equal(TrainingPresentationWorkSource.Maintenance, presentation.PrimaryPrescribedWork!.Source);
        Assert.Equal(AppTrainingSessionType.Maintenance, presentation.PrimaryPrescribedWork.SessionType);
        Assert.False(presentation.PrimaryPrescribedWork.AdvancementWorkAllowed);
        Assert.NotNull(presentation.UrgentBlocker);
        Assert.Equal(TrainingPresentationBlockerKind.MaintenanceOrDecay, presentation.UrgentBlocker!.Kind);
        Assert.False(presentation.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task PreflightPresentationKeepsStandardsAndHidesGeneratedRuntimeIdentity()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(Today)));

        var preflight = TrainingPresentationMapper.FromPreflight(prepared);

        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, preflight.Status);
        Assert.True(preflight.CanStart);
        Assert.NotNull(preflight.Work);
        Assert.Equal("Target Hold", preflight.Work!.DrillLabel);
        Assert.Contains("No more than 5 marked drifts", preflight.Standard);
        Assert.Contains("Target is stated before set", preflight.HonestyConstraint);
        Assert.True(preflight.LoadVariableCount > 0);
        Assert.True(preflight.CriticalConstraintCount > 0);
        Assert.True(preflight.ExpectedEvidenceFactCount > 0);
        Assert.Empty(preflight.Blockers);
        Assert.DoesNotContain("workflow", preflight.RevealOnDemand.Select(reveal => reveal.Label));
        AssertPresentationModelsAvoidFirstLevelTechnicalIdentifiers();
    }

    [Fact]
    public void LivePresentationPromotesOneRuntimeCommandWithoutCueOrPhaseIdentifiers()
    {
        var live = LiveState(
            RuntimeSessionLifecycleStatus.Running,
            RuntimePhaseSchedulerStatus.Running,
            RuntimeSessionCompletionStatus.Completed);

        var presentation = TrainingPresentationMapper.FromLiveSession(live);

        Assert.Equal(TrainingPresentationWorkSource.LiveSession, presentation.Work.Source);
        Assert.Equal(RuntimeSessionPhaseKind.CueResponse, presentation.CurrentPhaseKind);
        Assert.NotNull(presentation.ActiveCue);
        Assert.True(presentation.ActiveCue!.HasHiddenExpectedResponse);
        Assert.Equal(RuntimeInputCommandKind.RespondToCue, presentation.PrimaryCommand?.Command);
        Assert.DoesNotContain(
            presentation.AvailableCommands,
            command => command.Command == RuntimeInputCommandKind.FinishPhase);
        Assert.True(presentation.Evidence.HasFailureMarks);
        Assert.False(presentation.Evidence.ExpectedEvidenceComplete);
        AssertPresentationModelsAvoidFirstLevelTechnicalIdentifiers();
    }

    [Theory]
    [InlineData(RuntimeSessionCompletionStatus.Abandoned, TrainingResultPresentationOutcomeKind.Abandoned)]
    [InlineData(RuntimeSessionCompletionStatus.TimedOut, TrainingResultPresentationOutcomeKind.TimedOut)]
    [InlineData(RuntimeSessionCompletionStatus.Failed, TrainingResultPresentationOutcomeKind.Failed)]
    public void ResultPresentationDistinguishesTerminalOutcomes(
        RuntimeSessionCompletionStatus completionStatus,
        TrainingResultPresentationOutcomeKind outcome)
    {
        var completion = new PreUiLiveSessionCompletionResult(
            PreUiLiveSessionCompletionStatus.Processed,
            completionStatus,
            WorkflowResult: null,
            LiveState(
                completionStatus == RuntimeSessionCompletionStatus.Abandoned
                    ? RuntimeSessionLifecycleStatus.Abandoned
                    : RuntimeSessionLifecycleStatus.Failed,
                RuntimePhaseSchedulerStatus.Completed,
                completionStatus),
            "Terminal runtime session was processed.");

        var presentation = TrainingPresentationMapper.FromResult(completion);

        Assert.Equal(outcome, presentation.Outcome);
        Assert.Equal(TrainingPresentationPrimaryActionKind.ReturnToNextPrescribedAction, presentation.PrimaryAction);
        Assert.False(presentation.ProducesSuccessfulEvidence);
        Assert.False(presentation.CleanPerformance);
        Assert.True(presentation.EvidenceSummary.HasFailureEvidence);
        Assert.False(presentation.GrantsAdvancementInApp);
    }

    [Theory]
    [MemberData(nameof(ProcessedOutcomeCases))]
    public async Task ResultPresentationNamesProgramOutcomeWithoutInventingProgress(
        CompletedRuntimeSessionProcessingResult processing,
        TrainingResultPresentationOutcomeKind expectedOutcome,
        bool expectedSuccessfulEvidence)
    {
        var presentation = await ProcessedPresentationAsync(processing);

        Assert.Equal(expectedOutcome, presentation.Outcome);
        Assert.Equal(expectedSuccessfulEvidence, presentation.ProducesSuccessfulEvidence);
        Assert.False(presentation.GrantsAdvancementInApp);
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

    private static PreUiLiveSessionState LiveState(
        RuntimeSessionLifecycleStatus lifecycleStatus,
        RuntimePhaseSchedulerStatus schedulerStatus,
        RuntimeSessionCompletionStatus completionStatus)
    {
        _ = completionStatus;

        return new PreUiLiveSessionState(
            "raw-runtime-session",
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            lifecycleStatus,
            schedulerStatus,
            "raw-phase-id",
            RuntimeSessionPhaseKind.CueResponse,
            RuntimeSessionPhaseCompletionRule.Manual,
            new PreUiLiveSessionTimerState(
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(90),
                0.25,
                IsTimed: true),
            new PreUiLiveSessionCueState(
                "raw-cue-id",
                RuntimeCueKind.FocusShift,
                "Switch to the marked target.",
                RuntimeCueResponseExpectation.ResponseRequired,
                TimeSpan.FromSeconds(5),
                "expected-response-hidden"),
            [new PreUiLiveSessionMaterialState("TargetStatement", "Target", "Hold the stated phrase.")],
            [
                new PreUiLiveSessionCommandState(
                    RuntimeInputCommandKind.RespondToCue,
                    IsAvailable: true,
                    InvalidReason: null,
                    CueInvalidReason: null,
                    "Answer cue"),
                new PreUiLiveSessionCommandState(
                    RuntimeInputCommandKind.FinishPhase,
                    IsAvailable: false,
                    RuntimeInputCommandInvalidReason.InvalidPhaseCompletion,
                    CueInvalidReason: null,
                    "Finish phase"),
            ],
            new PreUiLiveSessionEvidenceState(
                RuntimeEventCount: 4,
                EvidenceFactCount: 2,
                DriftCount: 1,
                GuessCount: 1,
                ErrorCount: 0,
                CueCount: 1,
                CueResponseCount: 0,
                AnswerCount: 0,
                CorrectionCount: 0,
                ExpectedEvidenceFactCount: 3),
            LastCommand: null,
            "Runtime session state captured.");
    }

    public static IEnumerable<object[]> ProcessedOutcomeCases()
    {
        yield return
        [
            ProcessingResult(
                transition: Transition(
                    BranchLevelState.TestReady,
                    BranchLevelTransition.PassFormalTestOnce)),
            TrainingResultPresentationOutcomeKind.PassedOnce,
            true,
        ];
        yield return
        [
            ProcessingResult(
                transition: Transition(
                    BranchLevelState.PassedOnce,
                    BranchLevelTransition.EnterStabilization)),
            TrainingResultPresentationOutcomeKind.Stabilizing,
            true,
        ];
        yield return
        [
            ProcessingResult(
                transition: Transition(
                    BranchLevelState.Stabilizing,
                    BranchLevelTransition.CompleteStabilization)),
            TrainingResultPresentationOutcomeKind.Owned,
            true,
        ];
        yield return
        [
            ProcessingResult(
                maintenance: Maintenance(MaintenanceCurrencyState.Current),
                sessionType: LocalCompletedSessionType.Maintenance),
            TrainingResultPresentationOutcomeKind.Maintenance,
            true,
        ];
        yield return
        [
            ProcessingResult(
                maintenance: Maintenance(MaintenanceCurrencyState.Warning),
                sessionType: LocalCompletedSessionType.Maintenance),
            TrainingResultPresentationOutcomeKind.MaintenanceWarning,
            true,
        ];
        yield return
        [
            ProcessingResult(decay: Decay()),
            TrainingResultPresentationOutcomeKind.Decayed,
            true,
        ];
        yield return
        [
            ProcessingResult(sessionType: LocalCompletedSessionType.Recovery, recoveryMarked: true),
            TrainingResultPresentationOutcomeKind.Recovery,
            true,
        ];
        yield return
        [
            ProcessingResult(transfer: TransferBlocked()),
            TrainingResultPresentationOutcomeKind.Blocked,
            true,
        ];
        yield return
        [
            ProcessingResult(
                standard: new StandardEvaluationResult(
                    Passed: false,
                    [new StandardEvaluationFailure(StandardFailureKind.NumericalThresholdMissed, "Threshold was not met.")]),
                cleanPerformance: false),
            TrainingResultPresentationOutcomeKind.Failed,
            false,
        ];
        yield return
        [
            ProcessingResult(),
            TrainingResultPresentationOutcomeKind.NoAdvancement,
            true,
        ];
    }

    private async ValueTask<ResultPresentationReadModel> ProcessedPresentationAsync(
        CompletedRuntimeSessionProcessingResult processing)
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training));
        var refreshed = await new CurrentTrainingStateLoader(configuration)
            .LoadAsync(new CurrentTrainingStateQuery(Today));
        var completion = new PreUiLiveSessionCompletionResult(
            PreUiLiveSessionCompletionStatus.Processed,
            processing.CompletionStatus,
            new PreUiTrainingWorkflowCompletionResult(processing, refreshed),
            LiveState(
                RuntimeSessionLifecycleStatus.Completed,
                RuntimePhaseSchedulerStatus.Completed,
                processing.CompletionStatus),
            "Processed.");

        return TrainingPresentationMapper.FromResult(completion);
    }

    private static CompletedRuntimeSessionProcessingResult ProcessingResult(
        LocalCompletedSessionType sessionType = LocalCompletedSessionType.Practice,
        bool cleanPerformance = true,
        bool recoveryMarked = false,
        StandardEvaluationResult? standard = null,
        FormalGateDecision? gate = null,
        StabilizationOwnershipResult? stabilization = null,
        MaintenanceCurrencyResult? maintenance = null,
        DecayRestorationResult? decay = null,
        TransferEligibilityResult? transfer = null,
        FailureResponse? failureResponse = null,
        BranchLevelStatusTransitionResult? transition = null)
    {
        var artifact = EvidenceRecord(
            cleanPerformance,
            ArtifactCategoryFor(sessionType),
            EventKindFor(sessionType));
        var session = new LocalSessionHistoryRecord(
            "session-fh-l1",
            Today,
            sessionType,
            [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L1)],
            DrillId.FH1TargetHold,
            sessionType == LocalCompletedSessionType.Transfer ? "changed context" : null,
            LocalSessionIntensity.Moderate,
            [new LoadVariable("duration", "30 seconds")],
            cleanPerformance,
            "Session recorded.",
            recoveryMarked,
            deloadMarked: false,
            [artifact.ArtifactId]);

        return new CompletedRuntimeSessionProcessingResult(
            RuntimeSessionCompletionStatus.Completed,
            null,
            null,
            session,
            [artifact],
            null,
            null,
            null,
            null,
            standard,
            gate,
            stabilization,
            maintenance,
            decay,
            transfer,
            failureResponse,
            transition,
            null);
    }

    private static LocalEvidenceArtifactRecord EvidenceRecord(
        bool cleanPerformance,
        EvidenceArtifactCategory category,
        LocalProgrammingEventKind eventKind)
    {
        var evidenceKind = cleanPerformance
            ? ObservableEvidenceKind.OutputSample
            : ObservableEvidenceKind.FailedItemList;
        return new LocalEvidenceArtifactRecord(
            "artifact-fh-l1",
            new LocalProgrammingEventReference(
                "event-fh-l1",
                eventKind,
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold),
            new EvidenceArtifact(
                category,
                Today,
                [new ObservableEvidence(evidenceKind, "Observable session evidence.")],
                "Session evidence."));
    }

    private static BranchLevelStatusTransitionResult Transition(
        BranchLevelState from,
        BranchLevelTransition transition)
    {
        return BranchLevelStateMachine.TryApply(
            Status(BranchCode.FH, GlobalLevelId.L1, from),
            transition);
    }

    private static MaintenanceCurrencyResult Maintenance(MaintenanceCurrencyState state)
    {
        return new MaintenanceCurrencyResult(
            BranchCode.FH,
            GlobalLevelId.L1,
            state,
            new MaintenanceCadence(7, 10, MaintenanceCheckKind.StandardOrTransfer),
            DaysSinceLastPassingCheck: state == MaintenanceCurrencyState.Current ? 0 : 8,
            ConsecutiveFailures: state == MaintenanceCurrencyState.Warning ? 1 : 0);
    }

    private static DecayRestorationResult Decay()
    {
        return new DecayRestorationResult(
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Decayed),
            BranchLevelTransition.MarkDecayed,
            []);
    }

    private static TransferEligibilityResult TransferBlocked()
    {
        return new TransferEligibilityResult(
            IsEligible: false,
            [new TransferEligibilityFailure(
                TransferEligibilityFailureKind.SourceStandardEvidenceMissing,
                "Source standard evidence is missing.")]);
    }

    private static EvidenceArtifactCategory ArtifactCategoryFor(LocalCompletedSessionType sessionType)
    {
        return sessionType switch
        {
            LocalCompletedSessionType.Load => EvidenceArtifactCategory.Load,
            LocalCompletedSessionType.Test => EvidenceArtifactCategory.Test,
            LocalCompletedSessionType.Stabilization => EvidenceArtifactCategory.Stabilization,
            LocalCompletedSessionType.Transfer => EvidenceArtifactCategory.Transfer,
            LocalCompletedSessionType.Maintenance => EvidenceArtifactCategory.Maintenance,
            _ => EvidenceArtifactCategory.Practice,
        };
    }

    private static LocalProgrammingEventKind EventKindFor(LocalCompletedSessionType sessionType)
    {
        return sessionType switch
        {
            LocalCompletedSessionType.Load => LocalProgrammingEventKind.Load,
            LocalCompletedSessionType.Test => LocalProgrammingEventKind.FormalTest,
            LocalCompletedSessionType.Stabilization => LocalProgrammingEventKind.Stabilization,
            LocalCompletedSessionType.Transfer => LocalProgrammingEventKind.Transfer,
            LocalCompletedSessionType.Maintenance => LocalProgrammingEventKind.Maintenance,
            _ => LocalProgrammingEventKind.Practice,
        };
    }

    private static void AssertPresentationModelsAvoidFirstLevelTechnicalIdentifiers()
    {
        string[] forbidden =
        [
            "SessionId",
            "InstanceId",
            "ContentId",
            "Fingerprint",
            "Hash",
            "Path",
            "CueId",
            "PhaseId",
        ];

        Type[] presentationTypes =
        [
            typeof(CurrentTrainingPresentationReadModel),
            typeof(SessionPreflightPresentationReadModel),
            typeof(LiveSessionPresentationReadModel),
            typeof(ResultPresentationReadModel),
            typeof(TrainingPresentationWorkSummary),
            typeof(TrainingBranchLevelPresentation),
            typeof(TrainingPresentationBlockerSummary),
            typeof(TrainingMaintenanceDecayPriority),
            typeof(TrainingEvidencePresentationSummary),
            typeof(LiveCuePresentationSummary),
            typeof(LiveCommandPresentationSummary),
            typeof(LiveEvidencePresentationSummary),
            typeof(TrainingStateTransitionPresentation),
            typeof(TrainingPresentationReveal),
        ];

        foreach (var type in presentationTypes)
        {
            var propertyNames = type.GetProperties().Select(property => property.Name).ToArray();
            foreach (var forbiddenName in forbidden)
            {
                Assert.DoesNotContain(propertyNames, name => name.Contains(forbiddenName, StringComparison.Ordinal));
            }
        }
    }
}
