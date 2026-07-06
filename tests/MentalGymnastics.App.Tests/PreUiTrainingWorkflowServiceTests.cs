using MentalGymnastics.App;
using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class PreUiTrainingWorkflowServiceTests : IDisposable
{
    private static readonly TrainingDate SessionDate = TrainingDate.From(2026, 7, 5);

    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SuccessfulWorkflowComposesSelectionContentRuntimeCorePersistenceAndRefresh()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.TestReady));
        await SaveCleanPracticeAsync(configuration, "practice-fh-l1-a", TrainingDate.From(2026, 7, 3));
        await SaveCleanPracticeAsync(configuration, "practice-fh-l1-b", TrainingDate.From(2026, 7, 4));

        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionAsync(new PreUiTrainingWorkflowPreparationRequest(
            new NextTrainingWorkSelectionQuery(
                SessionDate,
                new RequestedTrainingWork(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    DrillId.FH1TargetHold,
                    AppTrainingSessionType.Test,
                    [
                        new LoadVariable("duration", "3 minutes"),
                        new LoadVariable("target subtlety", "simple phrase"),
                        new LoadVariable("recovery window", "10 seconds"),
                    ])),
            PromptContentKind.EquivalentPrompt,
            "fh-l1-target-hold",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            new GeneratedContentSeed("workflow-fh-l1-pass"),
            "workflow-session-fh-l1-pass",
            additionalCriticalConstraints: [new CriticalConstraint("No target substitution.")]));

        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);
        Assert.True(prepared.IsPrepared);
        Assert.True(prepared.CanStartRuntimeSession);
        Assert.NotNull(prepared.CurrentState);
        Assert.Equal(NextTrainingWorkSelectionKind.Allowed, prepared.Selection.Kind);
        Assert.NotNull(prepared.GeneratedContent);
        Assert.NotNull(prepared.RuntimeSession);
        Assert.False(prepared.GrantsAdvancementInApp);

        var generatedBeforeCompletion = await new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.GeneratedContent!.PersistenceHandoff!.InstanceId);
        Assert.NotNull(generatedBeforeCompletion);
        Assert.Equal(LocalGeneratedDrillInstanceState.InSession, generatedBeforeCompletion.State);
        Assert.Equal("workflow-session-fh-l1-pass", generatedBeforeCompletion.ActiveSessionId);

        var runtimeResult = CompleteWithEvidence(
            "workflow-session-fh-l1-pass",
            prepared.RuntimeSession!.SessionDefinition!,
            RuntimeEvidenceCaptureKind.FormalAttempt,
            [
                new RuntimeEventFact("score", "drifts=4; max_return=8s; no target change"),
                new RuntimeEventFact("critical_constraint", "target stable and every drift marked"),
            ]);
        var completed = await workflow.CompleteSessionAsync(new PreUiTrainingWorkflowCompletionRequest(
            new CompletedRuntimeSessionProcessingRequest(
                runtimeResult,
                Metadata(LocalSessionIntensity.High, cleanPerformance: true, "Workflow formal FH L1 test passed with observable evidence."),
                EvaluatedStandard("drifts", maxAllowed: 5),
                standardEvaluation: new RuntimeStandardEvaluationHandoffInput(
                    [new NumericMeasurement("drifts", 4)],
                    [new CriticalConstraintCheck("catalog-constraint", true)],
                    outputComplete: true,
                    rubricOutcome: null),
                formalGate: new RuntimeFormalGateHandoffInput(
                    SessionDate,
                    new TestResultEvidence(TestResultEvidenceKind.Score, "drifts=4; max_return=8s"),
                    FormalTestPassState.PassOnce)),
            SessionDate));

        Assert.Equal(RuntimeSessionCompletionStatus.Completed, completed.ProcessingResult.CompletionStatus);
        Assert.True(completed.ProcessingResult.StandardEvaluationResult!.Passed);
        Assert.Equal(GateOutcome.PassOnce, completed.ProcessingResult.FormalGateDecision!.Outcome);
        Assert.False(completed.GrantsAdvancementInApp);
        Assert.Equal(
            BranchLevelState.PassedOnce,
            completed.RefreshedState.CurrentPractitionerState.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.Contains(
            completed.RefreshedState.RecentSessions,
            session => session.SessionId == "workflow-session-fh-l1-pass");

        var attempt = await new LocalFormalTestAttemptStore(configuration.LocalDatabaseOptions)
            .LoadAsync("workflow-session-fh-l1-pass-formal-attempt");
        var generatedAfterCompletion = await new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.GeneratedContent.PersistenceHandoff.InstanceId);
        var summary = await new LocalProgressSummaryStore(configuration.LocalDatabaseOptions)
            .LoadLatestAsync();

        Assert.NotNull(attempt);
        Assert.Equal(FormalTestPassState.PassOnce, attempt.Attempt.PassState);
        Assert.NotNull(generatedAfterCompletion);
        Assert.Equal(LocalGeneratedDrillInstanceState.Completed, generatedAfterCompletion.State);
        Assert.NotNull(summary);
        Assert.Contains(summary.SourceReferences, reference =>
            reference.Kind == LocalProgressSummarySourceKind.CompletedSession &&
            reference.SourceId == "workflow-session-fh-l1-pass");
    }

    [Fact]
    public async Task FirstRunDefaultWorkflowPreparesContentRuntimeAndPersistsGeneratedInstance()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);

        var prepared = await workflow.PrepareNextSessionAsync(new PreUiTrainingWorkflowPreparationRequest(
            new NextTrainingWorkSelectionQuery(SessionDate),
            PromptContentKind.EquivalentPrompt,
            "fh-l1-target-hold",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            new GeneratedContentSeed("workflow-default-first-run"),
            "workflow-session-first-run-default"));

        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);
        Assert.True(prepared.CanStartRuntimeSession);
        Assert.Equal(BranchCode.FH, prepared.Selection.SelectedWork?.Branch);
        Assert.Equal(GlobalLevelId.L1, prepared.Selection.SelectedWork?.Level);
        Assert.Equal(DrillId.FH1TargetHold, prepared.Selection.SelectedWork?.Drill);
        Assert.DoesNotContain(
            prepared.Selection.SelectedWork!.LoadVariables,
            variable => variable.Name == "catalog-load");
        Assert.Contains(
            prepared.Selection.SelectedWork.LoadVariables,
            variable => variable.Name == "duration" && variable.Value == "3 minutes");
        Assert.NotNull(prepared.GeneratedContent);
        Assert.NotNull(prepared.RuntimeSession);

        var generated = await new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.GeneratedContent!.PersistenceHandoff!.InstanceId);

        Assert.NotNull(generated);
        Assert.Equal(LocalGeneratedDrillInstanceState.InSession, generated.State);
        Assert.Equal("workflow-session-first-run-default", generated.ActiveSessionId);
    }

    [Fact]
    public async Task DefaultWorkflowDerivesContentDefaultsFromSelectedWorkBeforeRuntimePreparation()
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
                        AppTrainingSessionType.Practice))));

        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Prepared, prepared.Status);
        Assert.True(prepared.CanStartRuntimeSession);
        Assert.Equal(BranchCode.WM, prepared.Selection.SelectedWork?.Branch);
        Assert.Equal(DrillId.WM1DelayedReconstruction, prepared.Selection.SelectedWork?.Drill);
        Assert.NotNull(prepared.GeneratedContent);
        Assert.Equal(
            PromptContentKind.DelayedReconstructionTask,
            prepared.GeneratedContent!.GeneratedContent?.Result.ContentKind);
        Assert.Equal(
            "wm-l1-delayed-reconstruction",
            prepared.GeneratedContent.GeneratedContent?.Result.EquivalenceClass);
        Assert.NotNull(prepared.RuntimeSession);
        Assert.NotEmpty(prepared.RuntimeSession!.ExpectedEvidenceFacts);

        var generated = await new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions)
            .LoadAsync(prepared.GeneratedContent.PersistenceHandoff!.InstanceId);

        Assert.NotNull(generated);
        Assert.Equal(LocalGeneratedDrillInstanceState.InSession, generated.State);
        Assert.Equal(prepared.RuntimeSession.SessionId, generated.ActiveSessionId);
    }

    [Fact]
    public async Task BlockedWorkflowDoesNotGenerateContentOrPrepareRuntimeSession()
    {
        var configuration = Configuration();
        var workflow = new PreUiTrainingWorkflowService(configuration);

        var prepared = await workflow.PrepareNextSessionAsync(new PreUiTrainingWorkflowPreparationRequest(
            new NextTrainingWorkSelectionQuery(
                SessionDate,
                new RequestedTrainingWork(
                    BranchCode.CO,
                    GlobalLevelId.L1,
                    DrillId.CO1RuleExtraction,
                    AppTrainingSessionType.Test)),
            PromptContentKind.RuleExampleSet,
            "co-l1-rule-extraction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            new GeneratedContentSeed("workflow-blocked-co-l1"),
            "workflow-session-blocked"));

        Assert.Equal(PreUiTrainingWorkflowPreparationStatus.Blocked, prepared.Status);
        Assert.False(prepared.IsPrepared);
        Assert.False(prepared.CanStartRuntimeSession);
        Assert.NotNull(prepared.CurrentState);
        Assert.Equal(NextTrainingWorkSelectionKind.Blocked, prepared.Selection.Kind);
        Assert.Null(prepared.GeneratedContent);
        Assert.Null(prepared.RuntimeSession);
        Assert.False(prepared.GrantsAdvancementInApp);
        Assert.Contains(
            prepared.Selection.Blockers,
            blocker => blocker.Source == NextTrainingWorkBlockerSource.TestReadiness &&
                blocker.TestReadinessFailureKind == TestReadinessFailureKind.PrerequisiteNotOwned);

        var generatedInstances = await new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions)
            .ListAsync();
        Assert.Empty(generatedInstances);
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

    private static async ValueTask SaveCleanPracticeAsync(
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
                    [new ObservableEvidence(ObservableEvidenceKind.OutputSample, "FH L1 clean practice preserved target and drift marking.")],
                    "FH L1 clean practice set.")));

        await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions).SaveAsync(
            new LocalSessionHistoryRecord(
                sessionId,
                date,
                LocalCompletedSessionType.Practice,
                [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L1)],
                DrillId.FH1TargetHold,
                transferTask: null,
                LocalSessionIntensity.Moderate,
                [new LoadVariable("catalog-demand", DemandFor(BranchCode.FH, GlobalLevelId.L1))],
                cleanPerformance: true,
                "FH L1 clean practice prepared the formal demand.",
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

    private static RuntimePersistenceHandoffMetadata Metadata(
        LocalSessionIntensity intensity,
        bool cleanPerformance,
        string notes)
    {
        return new RuntimePersistenceHandoffMetadata(
            SessionDate,
            intensity,
            cleanPerformance,
            notes);
    }

    private static EvaluatedStandard EvaluatedStandard(
        string measurementName,
        decimal maxAllowed)
    {
        return new EvaluatedStandard(
            "Catalog standard evidence",
            [NumericThreshold.AtMost(measurementName, maxAllowed)],
            [new CriticalConstraintRequirement("catalog-constraint", "Catalog honesty constraint remained intact.")],
            requiresCompleteOutput: true,
            requiredRubric: null);
    }

    private static RuntimeSessionCompletionResult CompleteWithEvidence(
        string sessionId,
        RuntimeSessionDefinition session,
        RuntimeEvidenceCaptureKind captureKind,
        IReadOnlyList<RuntimeEventFact> facts)
    {
        var log = RuntimeEventLog.Start(sessionId, session, RuntimeInstant.Zero);
        log.Append(
            RuntimeEventKind.AnswerSubmitted,
            Instant(180),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);
        log.Append(RuntimeEventKind.SessionCompleted, Instant(185));

        var scoringEvents = log.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        var evidenceDraft = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            sessionId,
            session,
            SessionDate,
            captureKind,
            log.Events,
            scoringEvents));

        return RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            sessionId,
            session,
            RuntimeSessionCompletionStatus.Completed,
            [CompletedPhase("active", RuntimeSessionPhaseKind.ActiveWork, 0, 185)],
            log.Events,
            scoringEvents,
            [evidenceDraft]));
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

    private static RuntimeInstant Instant(int seconds)
    {
        return new RuntimeInstant(TimeSpan.FromSeconds(seconds));
    }

    private static string DemandFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards
            .Single(standard => standard.Branch == branch && standard.Level == level)
            .Demand;
    }
}
