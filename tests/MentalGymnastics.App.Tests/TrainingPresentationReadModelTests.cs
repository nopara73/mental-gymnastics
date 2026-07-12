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
        Assert.NotNull(presentation.PrimaryPrescribedWork);
        Assert.Equal(BranchCode.WM, presentation.PrimaryPrescribedWork!.BranchLevels.Single().Branch);
        Assert.Equal(GlobalLevelId.L2, presentation.PrimaryPrescribedWork.BranchLevels.Single().Level);
        Assert.Equal(DrillId.WM1DelayedReconstruction, presentation.PrimaryPrescribedWork.Drill);
        Assert.Equal(AppTrainingSessionType.Regression, presentation.PrimaryPrescribedWork.SessionType);
        Assert.False(presentation.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task CurrentStatePresentationKeepsDueMaintenanceWorkAndActionAligned()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
            Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));

        var state = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(Today));
        var presentation = TrainingPresentationMapper.FromCurrentState(state);

        Assert.Equal(TrainingPresentationPriorityKind.MaintenanceDue, presentation.Priority);
        Assert.Equal(TrainingPresentationPrimaryActionKind.StartMaintenance, presentation.PrimaryAction);
        Assert.NotNull(presentation.PrimaryPrescribedWork);
        Assert.Equal(BranchCode.FH, presentation.PrimaryPrescribedWork!.BranchLevels.Single().Branch);
        Assert.Equal(DrillId.FH1TargetHold, presentation.PrimaryPrescribedWork.Drill);
        Assert.Equal(AppTrainingSessionType.Maintenance, presentation.PrimaryPrescribedWork.SessionType);
        Assert.Equal(TrainingPresentationWorkSource.Maintenance, presentation.PrimaryPrescribedWork.Source);
        Assert.True(presentation.PrimaryPrescribedWork.HasExecutableStandard);
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
        Assert.Equal("Target Hold", presentation.PrimaryPrescribedWork!.Exercise.ExerciseName);
        Assert.True(presentation.PrimaryPrescribedWork.HasExecutableStandard);
        Assert.Equal("Level 1", presentation.PrimaryPrescribedWork.Exercise.BranchLevelLabel);
        Assert.Contains("Hold one simple target", presentation.PrimaryPrescribedWork.Exercise.FirstScreenInstruction);
        Assert.Contains("Mind wandered", presentation.PrimaryPrescribedWork.Exercise.FirstScreenInstruction);
        Assert.Contains("Practice one loop", presentation.PrimaryPrescribedWork.Exercise.Purpose);
        Assert.Contains("notice attention moved", presentation.PrimaryPrescribedWork.Exercise.Purpose);
        Assert.Contains("mark it", presentation.PrimaryPrescribedWork.Exercise.Purpose);
        Assert.Contains("return to the same target", presentation.PrimaryPrescribedWork.Exercise.Purpose);
        Assert.Contains("clean return", presentation.PrimaryPrescribedWork.Exercise.PracticeGain);
        Assert.Contains("not feeling calm", presentation.PrimaryPrescribedWork.Exercise.PracticeGain);
        Assert.Contains("After this is stable", presentation.PrimaryPrescribedWork.Exercise.WhereItGoes);
        Assert.Contains("longer holds", presentation.PrimaryPrescribedWork.Exercise.WhereItGoes);
        Assert.Contains("distraction", presentation.PrimaryPrescribedWork.Exercise.WhereItGoes);
        Assert.Contains("memory", presentation.PrimaryPrescribedWork.Exercise.WhereItGoes);
        Assert.Contains("switching", presentation.PrimaryPrescribedWork.Exercise.WhereItGoes);
        Assert.Contains("transfer", presentation.PrimaryPrescribedWork.Exercise.WhereItGoes);
        Assert.DoesNotContain("Focus Shift", presentation.PrimaryPrescribedWork.Exercise.WhereItGoes);
        Assert.DoesNotContain("Inhibition", presentation.PrimaryPrescribedWork.Exercise.WhereItGoes);
        Assert.DoesNotContain("No promise this makes you smarter", presentation.PrimaryPrescribedWork.Exercise.WhereItGoes);
        Assert.DoesNotContain("Focus Hold", presentation.PrimaryPrescribedWork.Exercise.BranchLevelLabel);
        Assert.DoesNotContain("FH", presentation.PrimaryPrescribedWork.Exercise.BranchLevelLabel);
        Assert.DoesNotContain("+", presentation.PrimaryPrescribedWork.Exercise.BranchLevelLabel);
        Assert.NotNull(presentation.UrgentBlocker);
        Assert.False(presentation.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task CurrentStatePresentationShowsTheActiveBranchInsteadOfTheFirstWeeklyBranch()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
            Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training));
        await SavePassingMaintenanceAsync(
            configuration,
            BranchCode.FH,
            GlobalLevelId.L1,
            Today);

        var state = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(Today));
        var presentation = TrainingPresentationMapper.FromCurrentState(state);

        Assert.NotNull(presentation.PrimaryPrescribedWork);
        Assert.Equal(BranchCode.FS, presentation.PrimaryPrescribedWork!.BranchLevels.Single().Branch);
        Assert.Equal(DrillId.FS1CueSwitch, presentation.PrimaryPrescribedWork.Drill);
        Assert.Equal("Cue Switch", presentation.PrimaryPrescribedWork.Exercise.ExerciseName);
        Assert.Equal("Focus Shift · Level 1", presentation.PrimaryPrescribedWork.Exercise.BranchLevelLabel);
        Assert.Equal(AppTrainingSessionType.Practice, presentation.PrimaryPrescribedWork.SessionType);
        Assert.True(presentation.PrimaryPrescribedWork.HasExecutableStandard);
    }

    [Fact]
    public async Task CurrentStatePresentationPairsTestReadyWorkWithTheProgrammedTestSession()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
            Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.TestReady));

        var state = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(Today));
        var presentation = TrainingPresentationMapper.FromCurrentState(state);

        Assert.NotNull(presentation.PrimaryPrescribedWork);
        Assert.Equal(BranchCode.FS, presentation.PrimaryPrescribedWork!.BranchLevels.Single().Branch);
        Assert.Equal(WeeklySessionKind.TestOrStabilization, presentation.PrimaryPrescribedWork.WeeklySession);
        Assert.Equal(AppTrainingSessionType.Test, presentation.PrimaryPrescribedWork.SessionType);
        Assert.True(presentation.PrimaryPrescribedWork.HasExecutableStandard);
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

        var prepared = await new PreUiTrainingWorkflowService(configuration)
            .PrepareNextSessionWithDefaultsAsync(
                new PreUiTrainingWorkflowDefaultPreparationRequest(
                    new NextTrainingWorkSelectionQuery(Today)));
        var preflight = TrainingPresentationMapper.FromPreflight(prepared);

        Assert.True(preflight.CanStart);
        Assert.Empty(preflight.Blockers);
    }

    [Fact]
    public async Task ExceptionRulePreflightShowsCompactRuleAndExceptionsBeforeCuesStart()
    {
        var configuration = Configuration();
        await SaveStateAsync(
            configuration,
            Status(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Maintenance));

        var prepared = await new PreUiTrainingWorkflowService(configuration)
            .PrepareNextSessionWithDefaultsAsync(
                new PreUiTrainingWorkflowDefaultPreparationRequest(
                    new NextTrainingWorkSelectionQuery(Today)));
        var preflight = TrainingPresentationMapper.FromPreflight(prepared);

        Assert.True(preflight.CanStart);
        Assert.Equal("Tap round. Withhold angular. Exceptions win.", preflight.Work!.Exercise.PrimaryMaterial);
        var generatedExceptionCount = prepared.RuntimeSession!.InputMaterials.Count(material =>
            material.Kind == MentalGymnastics.Content.GeneratedContentMaterialKind.ExceptionDefinition);
        Assert.True(generatedExceptionCount > 0);
        Assert.Equal(generatedExceptionCount, preflight.Work.Exercise.SetupItems.Count);
        Assert.All(preflight.Work.Exercise.SetupItems, item =>
        {
            Assert.Contains(':', item);
            Assert.DoesNotContain("instead", item, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cue is", item, StringComparison.OrdinalIgnoreCase);
        });
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
        Assert.Equal("Target Hold", preflight.Work.Exercise.ExerciseName);
        Assert.Equal("Level 1", preflight.Work.Exercise.BranchLevelLabel);
        Assert.DoesNotContain("Focus Hold", preflight.Work.Exercise.BranchLevelLabel);
        Assert.Contains("Practice one loop", preflight.Work.Exercise.Purpose);
        Assert.Contains("clean return", preflight.Work.Exercise.PracticeGain);
        Assert.Contains("After this is stable", preflight.Work.Exercise.WhereItGoes);
        Assert.Contains("longer holds", preflight.Work.Exercise.WhereItGoes);
        Assert.Contains("distraction", preflight.Work.Exercise.WhereItGoes);
        Assert.Contains("memory", preflight.Work.Exercise.WhereItGoes);
        Assert.Contains("switching", preflight.Work.Exercise.WhereItGoes);
        Assert.Contains("transfer", preflight.Work.Exercise.WhereItGoes);
        Assert.DoesNotContain("Focus Shift", preflight.Work.Exercise.WhereItGoes);
        Assert.DoesNotContain("No promise this makes you smarter", preflight.Work.Exercise.WhereItGoes);
        Assert.Contains("Read the target", preflight.Work.Exercise.BeforeStartInstruction);
        Assert.Contains("Counts if", preflight.Work.Exercise.SuccessCriteria);
        Assert.Contains("finish 2 minutes", preflight.Work.Exercise.SuccessCriteria);
        Assert.Contains("tap 5 times or fewer", preflight.Work.Exercise.SuccessCriteria);
        Assert.Contains("Try again if", preflight.Work.Exercise.FailureCriteria);
        Assert.Contains("miss a wander", preflight.Work.Exercise.FailureCriteria);
        Assert.Contains("Tap Mind wandered", preflight.Work.Exercise.HonestyInstruction);
        Assert.Contains("Keep the same target", preflight.Work.Exercise.HonestyInstruction);
        Assert.Contains("saves wander taps", preflight.Work.Exercise.EvidenceRecorded);
        var primaryMaterial = Assert.IsType<string>(preflight.Work.Exercise.PrimaryMaterial);
        Assert.False(string.IsNullOrWhiteSpace(primaryMaterial));
        Assert.False(primaryMaterial.EndsWith(".", StringComparison.Ordinal));
        Assert.DoesNotContain("Hold target", primaryMaterial);
        Assert.DoesNotContain("target phrase", primaryMaterial);
        Assert.DoesNotContain("target word", primaryMaterial);
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
    public void LivePrepPresentationStatesUserActionInsteadOfRuntimePhaseJargon()
    {
        var live = new PreUiLiveSessionState(
            "raw-runtime-session",
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            RuntimeSessionLifecycleStatus.Running,
            RuntimePhaseSchedulerStatus.Running,
            "raw-phase-id",
            RuntimeSessionPhaseKind.InstructionPrep,
            RuntimeSessionPhaseCompletionRule.Manual,
            new PreUiLiveSessionTimerState(
                TimeSpan.Zero,
                Remaining: null,
                Progress: null,
                IsTimed: false),
            ActiveCue: null,
            [new PreUiLiveSessionMaterialState("TargetStatement", "Target", "Hold target phrase: blue square.")],
            [
                new PreUiLiveSessionCommandState(
                    RuntimeInputCommandKind.FinishPhase,
                    IsAvailable: true,
                    InvalidReason: null,
                    CueInvalidReason: null,
                    "Next step"),
            ],
            new PreUiLiveSessionEvidenceState(
                RuntimeEventCount: 1,
                EvidenceFactCount: 0,
                DriftCount: 0,
                GuessCount: 0,
                ErrorCount: 0,
                CueCount: 0,
                CueResponseCount: 0,
                AnswerCount: 0,
                CorrectionCount: 0,
                ExpectedEvidenceFactCount: 1),
            LastCommand: null,
            "Runtime session state captured.");

        var presentation = TrainingPresentationMapper.FromLiveSession(live);

        Assert.Equal("Eyes open. Keep the target visible. Say it once.", presentation.CurrentInstruction);
        Assert.DoesNotContain("Prep", presentation.CurrentInstruction);
        Assert.Equal("Target Hold", presentation.Work.Exercise.ExerciseName);
        Assert.Equal("blue square", presentation.Work.Exercise.PrimaryMaterial);
        Assert.Equal(RuntimeInputCommandKind.FinishPhase, presentation.PrimaryCommand?.Command);
        AssertPresentationModelsAvoidFirstLevelTechnicalIdentifiers();
    }

    [Fact]
    public void LiveTargetHoldActiveWorkPromotesDriftMarkingOverGenericSubmission()
    {
        var live = new PreUiLiveSessionState(
            "raw-runtime-session",
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            RuntimeSessionLifecycleStatus.Running,
            RuntimePhaseSchedulerStatus.Running,
            "raw-phase-id",
            RuntimeSessionPhaseKind.ActiveWork,
            RuntimeSessionPhaseCompletionRule.Timed,
            new PreUiLiveSessionTimerState(
                TimeSpan.FromSeconds(48),
                Remaining: TimeSpan.FromMinutes(2),
                Progress: 0.25,
                IsTimed: true),
            ActiveCue: null,
            [new PreUiLiveSessionMaterialState("TargetStatement", "Target", "Hold target phrase: blue square.")],
            [
                new PreUiLiveSessionCommandState(
                    RuntimeInputCommandKind.SubmitAnswer,
                    IsAvailable: true,
                    InvalidReason: null,
                    CueInvalidReason: null,
                    "Submit answer"),
                new PreUiLiveSessionCommandState(
                    RuntimeInputCommandKind.MarkDrift,
                    IsAvailable: true,
                    InvalidReason: null,
                    CueInvalidReason: null,
                    "Mind wandered"),
                new PreUiLiveSessionCommandState(
                    RuntimeInputCommandKind.Abandon,
                    IsAvailable: true,
                    InvalidReason: null,
                    CueInvalidReason: null,
                    "Stop early"),
            ],
            new PreUiLiveSessionEvidenceState(
                RuntimeEventCount: 2,
                EvidenceFactCount: 1,
                DriftCount: 0,
                GuessCount: 0,
                ErrorCount: 0,
                CueCount: 0,
                CueResponseCount: 0,
                AnswerCount: 0,
                CorrectionCount: 0,
                ExpectedEvidenceFactCount: 1),
            LastCommand: null,
            "Runtime session state captured.");

        var presentation = TrainingPresentationMapper.FromLiveSession(live);

        Assert.Equal("Eyes open. Hold the target.", presentation.CurrentInstruction);
        Assert.Equal(RuntimeInputCommandKind.MarkDrift, presentation.PrimaryCommand?.Command);
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

    [Fact]
    public async Task ResultPresentationPrefersSpecificHoldFailureOverIncompleteArtifactJargon()
    {
        var presentation = await ProcessedPresentationAsync(ProcessingResult(
            cleanPerformance: false,
            standard: new StandardEvaluationResult(
                Passed: false,
                [
                    new StandardEvaluationFailure(
                        StandardFailureKind.OutputIncomplete,
                        "Required output artifact is incomplete."),
                    new StandardEvaluationFailure(
                        StandardFailureKind.NumericalThresholdMissed,
                        FocusHoldStandardMeasurements.ActiveDurationSeconds),
                ])));

        Assert.Equal(["Hold ended before 0:30."], presentation.BlockingFailureDetails);
        Assert.Contains(
            presentation.Work.LoadVariables,
            load => load.Name == "duration" && load.Value == "30 seconds");
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

    [Fact]
    public async Task RevealLabelsUseUserFacingLanguageInsteadOfInternalWorkflowTerms()
    {
        var configuration = Configuration();
        var state = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(Today));
        var current = TrainingPresentationMapper.FromCurrentState(state);

        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(Today)));
        var preflight = TrainingPresentationMapper.FromPreflight(prepared);
        var live = TrainingPresentationMapper.FromLiveSession(LiveState(
            RuntimeSessionLifecycleStatus.Running,
            RuntimePhaseSchedulerStatus.Running,
            RuntimeSessionCompletionStatus.Completed));
        var result = await ProcessedPresentationAsync(ProcessingResult());

        var labels = current.RevealOnDemand
            .Concat(preflight.RevealOnDemand)
            .Concat(live.RevealOnDemand)
            .Concat(result.RevealOnDemand)
            .Select(reveal => reveal.Label)
            .ToArray();

        Assert.Contains("Exercise material", labels);
        Assert.Contains("Session details", labels);
        Assert.Contains("Saved exercise material", labels);
        Assert.Contains("Available controls", labels);
        Assert.Contains("Recorded events", labels);
        Assert.Contains("Program result", labels);
        Assert.Contains("Saved records", labels);
        Assert.Contains("Saved result", labels);

        string[] forbiddenLabels =
        [
            "Evidence artifacts",
            "Runtime protocol",
            "Stored drill instance",
            "Available commands",
            "Core evaluation",
            "Saved evidence",
            "Persisted result",
        ];

        foreach (var forbidden in forbiddenLabels)
        {
            Assert.DoesNotContain(labels, label => label.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void EveryDrillUsesAConcreteActiveInstruction()
    {
        foreach (var drill in Enum.GetValues<DrillId>())
        {
            var presentation = TrainingPresentationMapper.FromLiveSession(
                LiveStateForDrill(drill, RuntimeSessionPhaseKind.ActiveWork));

            Assert.False(string.IsNullOrWhiteSpace(presentation.CurrentInstruction));
            Assert.DoesNotContain(
                "do the current exercise step",
                presentation.CurrentInstruction,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "follow the current exercise step",
                presentation.CurrentInstruction,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(DrillId.FS2InvalidCueFilter, "Invalid cue. Do not tap.")]
    [InlineData(DrillId.IR2ExceptionRule, "No-go. Do not tap.")]
    public void NoTouchCuesStateThatNoTouchIsRequired(DrillId drill, string expectedInstruction)
    {
        var live = LiveStateForDrill(drill, RuntimeSessionPhaseKind.CueResponse) with
        {
            ActiveCue = new PreUiLiveSessionCueState(
                "no-touch-cue",
                RuntimeCueKind.InvalidCueFilter,
                "lure",
                RuntimeCueResponseExpectation.NoResponseExpected,
                TimeSpan.FromSeconds(2),
                ExpectedResponse: null),
        };

        var presentation = TrainingPresentationMapper.FromLiveSession(live);

        Assert.Equal(expectedInstruction, presentation.CurrentInstruction);
        Assert.False(presentation.ActiveCue!.RequiresResponse);
    }

    [Theory]
    [InlineData(DrillId.FS1CueSwitch, "Valid cue. Tap the named target.")]
    [InlineData(DrillId.IR1GoNoGoRule, "Go. Tap now.")]
    public void ActionCuesStateTheRequiredTouch(DrillId drill, string expectedInstruction)
    {
        var live = LiveStateForDrill(drill, RuntimeSessionPhaseKind.CueResponse) with
        {
            ActiveCue = new PreUiLiveSessionCueState(
                "action-cue",
                RuntimeCueKind.GoNoGo,
                "go",
                RuntimeCueResponseExpectation.ResponseRequired,
                TimeSpan.FromSeconds(2),
                ExpectedResponse: "tap"),
        };

        var presentation = TrainingPresentationMapper.FromLiveSession(live);

        Assert.Equal(expectedInstruction, presentation.CurrentInstruction);
        Assert.True(presentation.ActiveCue!.RequiresResponse);
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
                    "Next step"),
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

    private static PreUiLiveSessionState LiveStateForDrill(
        DrillId drill,
        RuntimeSessionPhaseKind phase)
    {
        return new PreUiLiveSessionState(
            $"live-{drill}",
            SessionType.Practice,
            BranchFor(drill),
            GlobalLevelId.L1,
            drill,
            RuntimeSessionLifecycleStatus.Running,
            RuntimePhaseSchedulerStatus.Running,
            "active",
            phase,
            RuntimeSessionPhaseCompletionRule.Manual,
            new PreUiLiveSessionTimerState(TimeSpan.Zero, Remaining: null, Progress: null, IsTimed: false),
            ActiveCue: null,
            CurrentMaterials: [],
            Commands:
            [
                new PreUiLiveSessionCommandState(
                    RuntimeInputCommandKind.FinishPhase,
                    IsAvailable: true,
                    InvalidReason: null,
                    CueInvalidReason: null,
                    "Next step"),
            ],
            new PreUiLiveSessionEvidenceState(
                RuntimeEventCount: 0,
                EvidenceFactCount: 0,
                DriftCount: 0,
                GuessCount: 0,
                ErrorCount: 0,
                CueCount: 0,
                CueResponseCount: 0,
                AnswerCount: 0,
                CorrectionCount: 0,
                ExpectedEvidenceFactCount: 0),
            LastCommand: null,
            "Live state for drill presentation.");
    }

    private static BranchCode BranchFor(DrillId drill)
    {
        return drill switch
        {
            DrillId.FH1TargetHold or DrillId.FH2DistractorHold => BranchCode.FH,
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter => BranchCode.FS,
            DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform => BranchCode.WM,
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => BranchCode.IR,
            DrillId.DE1PairDiscrimination or DrillId.DE2SeededAudit => BranchCode.DE,
            DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping => BranchCode.CO,
            DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery => BranchCode.AI,
            DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask => BranchCode.TI,
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unknown drill."),
        };
    }

    public static IEnumerable<object[]> ProcessedOutcomeCases()
    {
        yield return
        [
            ProcessingResult(
                transition: Transition(
                    BranchLevelState.Training,
                    BranchLevelTransition.MarkTestReady)),
            TrainingResultPresentationOutcomeKind.TestReady,
            true,
        ];
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
            TrainingResultPresentationOutcomeKind.CleanPractice,
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
            typeof(TrainingExercisePresentation),
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
