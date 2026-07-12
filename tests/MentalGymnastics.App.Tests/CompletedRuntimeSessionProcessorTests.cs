using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class CompletedRuntimeSessionProcessorTests : IDisposable
{
    private static readonly TrainingDate SessionDate = TrainingDate.From(2026, 7, 5);

    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PassedFormalTestIsEvaluatedByCoreAndPersistedAtomically()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.TestReady));
        var session = CreateSessionDefinition(
            SessionType.Test,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            generatedInstanceId: "generated-session-formal-pass");
        var result = CompleteWithEvidence(
            "session-formal-pass",
            session,
            RuntimeEvidenceCaptureKind.FormalAttempt,
            [
                new RuntimeEventFact("score", "drifts=4; max_return=8s; no target change"),
                new RuntimeEventFact("critical_constraint", "target stable and every drift marked"),
            ]);

        var processed = await new CompletedRuntimeSessionProcessor(configuration).ProcessAsync(
            new CompletedRuntimeSessionProcessingRequest(
                result,
                Metadata(LocalSessionIntensity.High, cleanPerformance: false, "Formal FH L1 test passed with observable evidence."),
                EvaluatedStandard("drifts", maxAllowed: 5),
                standardEvaluation: new RuntimeStandardEvaluationHandoffInput(
                    [new NumericMeasurement("drifts", 4)],
                    [new CriticalConstraintCheck("catalog-constraint", true)],
                    outputComplete: true,
                    rubricOutcome: null),
                formalGate: new RuntimeFormalGateHandoffInput(
                    SessionDate,
                    new TestResultEvidence(TestResultEvidenceKind.Score, "drifts=4; max_return=8s"),
                    FormalTestPassState.PassOnce,
                    mainFailureModeAvoided: "Target substitution avoided.")));

        var state = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();
        var sessionRecord = await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-formal-pass");
        var attempt = await new LocalFormalTestAttemptStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-formal-pass-formal-attempt");
        var generated = await new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions)
            .LoadAsync("generated-session-formal-pass");
        var summary = await new LocalProgressSummaryStore(configuration.LocalDatabaseOptions)
            .LoadLatestAsync();

        Assert.True(processed.StandardEvaluationResult!.Passed);
        Assert.Equal(GateOutcome.PassOnce, processed.FormalGateDecision!.Outcome);
        Assert.False(processed.GrantsAdvancementInApp);
        Assert.NotNull(state);
        Assert.Equal(BranchLevelState.PassedOnce, state.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.NotNull(sessionRecord);
        Assert.Equal(LocalCompletedSessionType.Test, sessionRecord.SessionType);
        Assert.True(sessionRecord.CleanPerformance);
        Assert.NotNull(attempt);
        Assert.Equal(FormalTestPassState.PassOnce, attempt.Attempt.PassState);
        Assert.Equal("Target substitution avoided.", attempt.Attempt.MainFailureModeAvoided);
        Assert.NotNull(generated);
        Assert.Equal(LocalGeneratedDrillInstanceState.Completed, generated.State);
        Assert.Equal("session-formal-pass-artifact-1", generated.ResultEvidenceArtifactId);
        Assert.NotNull(summary);
        Assert.Contains(summary.SourceReferences, reference =>
            reference.Kind == LocalProgressSummarySourceKind.CompletedSession &&
            reference.SourceId == "session-formal-pass");
        Assert.Contains(summary.SourceReferences, reference =>
            reference.Kind == LocalProgressSummarySourceKind.FormalTestAttempt &&
            reference.SourceId == "session-formal-pass-formal-attempt");
    }

    [Fact]
    public async Task PassedUniversalStartProjectsAllEarnedFoundationalOpenings()
    {
        var configuration = Configuration();
        var state = new PractitionerState(
            ProgramCatalog.Branches.SelectMany(branch => ProgramCatalog.GlobalLevels.Select(level =>
                new BranchLevelStatus(
                    branch.Code,
                    level.Id,
                    branch.Code == BranchCode.FH && level.Id == GlobalLevelId.L1
                        ? BranchLevelState.TestReady
                        : BranchLevelState.Unopened))));
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(state);
        var session = CreateSessionDefinition(
            SessionType.Test,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold);
        var result = CompleteWithEvidence(
            "session-universal-start-pass",
            session,
            RuntimeEvidenceCaptureKind.FormalAttempt,
            [new RuntimeEventFact("score", "drifts=4; max_return=8s")]);

        var processed = await new CompletedRuntimeSessionProcessor(configuration).ProcessAsync(
            new CompletedRuntimeSessionProcessingRequest(
                result,
                Metadata(LocalSessionIntensity.High, cleanPerformance: true, "Universal start standard passed."),
                EvaluatedStandard("drifts", maxAllowed: 5),
                standardEvaluation: new RuntimeStandardEvaluationHandoffInput(
                    [new NumericMeasurement("drifts", 4)],
                    [new CriticalConstraintCheck("catalog-constraint", true)],
                    outputComplete: true,
                    rubricOutcome: null),
                formalGate: new RuntimeFormalGateHandoffInput(
                    SessionDate,
                    new TestResultEvidence(TestResultEvidenceKind.Score, "Universal start passed."),
                    FormalTestPassState.PassOnce,
                    mainFailureModeAvoided: "Target substitution avoided.")));

        var stored = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();

        Assert.Equal(4, processed.ProgressionProjection?.OpenedForTraining.Count);
        Assert.Equal(BranchLevelState.PassedOnce, stored?.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Training, stored?.GetBranchLevelState(BranchCode.FS, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Training, stored?.GetBranchLevelState(BranchCode.WM, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Training, stored?.GetBranchLevelState(BranchCode.IR, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Training, stored?.GetBranchLevelState(BranchCode.DE, GlobalLevelId.L1));
    }

    [Fact]
    public async Task ReadinessKeepsRelevantPracticeWhenUnrelatedHistoryIsNewer()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training));
        await SaveFormalLoadPracticeSessionAsync(
            configuration,
            "fh-clean-before-unrelated-history",
            TrainingDate.From(2026, 6, 1),
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold);
        for (var day = 2; day <= 11; day++)
        {
            await SaveFormalLoadPracticeSessionAsync(
                configuration,
                $"fs-unrelated-{day}",
                TrainingDate.From(2026, 6, day),
                BranchCode.FS,
                GlobalLevelId.L1,
                DrillId.FS1CueSwitch);
        }

        var demand = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.FH &&
            standard.Level == GlobalLevelId.L1).Demand;
        var session = CreateSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold);
        var result = CompleteWithEvidence(
            "fh-clean-after-unrelated-history",
            session,
            RuntimeEvidenceCaptureKind.BestSet,
            [new RuntimeEventFact("score", "formal-load target held cleanly")]);

        var processed = await new CompletedRuntimeSessionProcessor(configuration).ProcessAsync(
            new CompletedRuntimeSessionProcessingRequest(
                result,
                Metadata(LocalSessionIntensity.Moderate, cleanPerformance: true, "Second clean FH L1 exposure."),
                readinessPractice: new RuntimeReadinessPracticeHandoffInput(demand, clean: true),
                refreshProgressSummary: false));

        var stored = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();
        Assert.NotNull(processed.StateTransition);
        Assert.Equal(BranchLevelState.TestReady, stored?.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
    }

    [Fact]
    public async Task FailedFormalTestPersistsFailureEvidenceAndReturnsTrainingWithoutAppGrantingProgress()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.TestReady));
        var session = CreateSessionDefinition(
            SessionType.Test,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold);
        var result = CompleteWithEvidence(
            "session-formal-fail",
            session,
            RuntimeEvidenceCaptureKind.FormalAttempt,
            [
                new RuntimeEventFact("score", "drifts=8; max_return=13s"),
                new RuntimeEventFact("error_kind", "unmarked_drift"),
                new RuntimeEventFact("critical_constraint", "one drift was not marked before reset"),
            ],
            RuntimeSessionCompletionStatus.Failed);

        var processed = await new CompletedRuntimeSessionProcessor(configuration).ProcessAsync(
            new CompletedRuntimeSessionProcessingRequest(
                result,
                Metadata(LocalSessionIntensity.High, cleanPerformance: true, "Formal FH L1 test failed because drift marking broke."),
                EvaluatedStandard("drifts", maxAllowed: 5),
                standardEvaluation: new RuntimeStandardEvaluationHandoffInput(
                    [new NumericMeasurement("drifts", 8)],
                    [new CriticalConstraintCheck("catalog-constraint", false)],
                    outputComplete: true,
                    rubricOutcome: null),
                formalGate: new RuntimeFormalGateHandoffInput(
                    SessionDate,
                    new TestResultEvidence(TestResultEvidenceKind.Score, "drifts=8; max_return=13s"),
                    FormalTestPassState.Fail,
                    FailureType.EffortFailure),
                failureResponse: new RuntimeFailureResponseHandoffInput(
                    FailureType.EffortFailure,
                    [FailureEvidenceSignal.BrokenHonestyConstraint],
                    isFirstFailureOfType: true,
                    repeatedOverloadInSameBranch: false)));

        var state = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();
        var attempt = await new LocalFormalTestAttemptStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-formal-fail-formal-attempt");
        var sessionRecord = await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-formal-fail");

        Assert.False(processed.StandardEvaluationResult!.Passed);
        Assert.Equal(GateOutcome.Fail, processed.FormalGateDecision!.Outcome);
        Assert.Contains(ProgrammingResponseAction.FailAttempt, processed.FailureResponse!.Actions);
        Assert.False(processed.GrantsAdvancementInApp);
        Assert.NotNull(state);
        Assert.Equal(BranchLevelState.Training, state.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.NotNull(attempt);
        Assert.Equal(FormalTestPassState.Fail, attempt.Attempt.PassState);
        Assert.Equal(FailureType.EffortFailure, attempt.Attempt.FailureType);
        Assert.NotNull(sessionRecord);
        Assert.False(sessionRecord.CleanPerformance);
    }

    [Fact]
    public async Task AbandonedSessionPersistsNonSuccessfulSessionAndGeneratedInstanceWithoutFormalAttempt()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.TestReady));
        var session = CreateSessionDefinition(
            SessionType.Test,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            generatedInstanceId: "generated-session-abandoned-test");
        var result = AbandonedResult("session-abandoned-test", session);

        var processed = await new CompletedRuntimeSessionProcessor(configuration).ProcessAsync(
            new CompletedRuntimeSessionProcessingRequest(
                result,
                Metadata(LocalSessionIntensity.High, cleanPerformance: false, "Runtime session was abandoned; no successful evidence was produced.")));

        var state = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();
        var sessionRecord = await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-abandoned-test");
        var attempt = await new LocalFormalTestAttemptStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-abandoned-test-formal-attempt");
        var generated = await new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions)
            .LoadAsync("generated-session-abandoned-test");

        Assert.Null(processed.PersistenceHandoff);
        Assert.False(processed.GrantsAdvancementInApp);
        Assert.NotNull(state);
        Assert.Equal(BranchLevelState.TestReady, state.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.NotNull(sessionRecord);
        Assert.False(sessionRecord.CleanPerformance);
        Assert.Contains("abandoned", sessionRecord.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Null(attempt);
        Assert.NotNull(generated);
        Assert.Equal(LocalGeneratedDrillInstanceState.Abandoned, generated.State);
        Assert.Null(generated.ResultEvidenceArtifactId);
    }

    [Fact]
    public async Task MaintenanceSessionPersistsMaintenanceRecordAndAppliesCoreMaintenanceTransition()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance));
        var session = CreateSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L2,
            DrillId.FH1TargetHold);
        var result = CompleteWithEvidence(
            "session-maintenance-pass",
            session,
            RuntimeEvidenceCaptureKind.Maintenance,
            [
                new RuntimeEventFact("maintenance_check", "FH L2 reduced-volume hold check preserved drift marking"),
                new RuntimeEventFact("score", "drifts=2; max_return=5s"),
            ]);
        var clean = new StandardEvaluationResult(true, []);

        var processed = await new CompletedRuntimeSessionProcessor(configuration).ProcessAsync(
            new CompletedRuntimeSessionProcessingRequest(
                result,
                Metadata(LocalSessionIntensity.Low, cleanPerformance: true, "Maintenance pass preserved the FH L2 standard."),
                maintenance: new RuntimeMaintenanceCoreHandoffInput(
                    SessionDate,
                    GlobalLevelId.L2,
                    MaintenanceCheckKind.StandardOrTransfer,
                    clean)));

        var state = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();
        var maintenance = await new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions)
            .LoadMaintenanceAsync("session-maintenance-pass-maintenance-check");

        Assert.Equal(MaintenanceCurrencyState.Current, processed.MaintenanceCurrencyResult!.State);
        Assert.NotNull(state);
        Assert.Equal(BranchLevelState.Owned, state.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L2));
        Assert.NotNull(maintenance);
        Assert.Equal(LocalCompletedSessionType.Maintenance, processed.SessionHistory.SessionType);
        Assert.True(maintenance.Evidence.StandardEvaluationResult.Passed);
        Assert.Equal("session-maintenance-pass", maintenance.CompletedSessionId);
    }

    [Fact]
    public async Task StabilizationSessionUsesCoreOwnershipEvidenceBeforeOwningState()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Stabilizing));
        await SaveExistingFormalPassAsync(
            configuration,
            "formal-pass-once",
            "artifact-formal-pass-once",
            TrainingDate.From(2026, 6, 28));
        await SaveExistingStabilizationAsync(
            configuration,
            "stabilization-repeat-one",
            "artifact-stabilization-repeat-one",
            TrainingDate.From(2026, 7, 4),
            FormalTestPassState.StabilizationPass,
            afterAdjacent: false);
        var session = CreateSessionDefinition(
            SessionType.Stabilization,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold);
        var result = CompleteWithEvidence(
            "session-stabilization-own",
            session,
            RuntimeEvidenceCaptureKind.Stabilization,
            [
                new RuntimeEventFact("repeatability_record", "third clean pass after controlled distractor"),
                new RuntimeEventFact("score", "drifts=3; max_return=6s"),
            ]);
        var clean = new StandardEvaluationResult(true, []);

        var processed = await new CompletedRuntimeSessionProcessor(configuration).ProcessAsync(
            new CompletedRuntimeSessionProcessingRequest(
                result,
                Metadata(LocalSessionIntensity.Moderate, cleanPerformance: true, "Stabilization pass kept the same FH L1 standard."),
                stabilization: new RuntimeStabilizationCoreHandoffInput(
                    SessionDate,
                    clean,
                    FormalTestPassState.StabilizationPass,
                    afterAdjacentWorkOrControlledDistractor: true,
                    "Target substitution avoided."),
                stabilizationPersistence: new RuntimeStabilizationPersistenceInput(
                    clean,
                    FormalTestPassState.StabilizationPass,
                    LocalStabilizationCondition.ControlledDistractor,
                    "After controlled distractor.",
                    "Target substitution avoided.")));

        var state = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();
        var stabilization = await new LocalStabilizationPassStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-stabilization-own-stabilization-pass");

        Assert.True(processed.StabilizationOwnershipResult!.IsOwned);
        Assert.Equal(GateOutcome.Own, processed.StabilizationOwnershipResult.GateOutcome);
        Assert.NotNull(state);
        Assert.Equal(BranchLevelState.Owned, state.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.NotNull(stabilization);
        Assert.Equal(LocalStabilizationCondition.ControlledDistractor, stabilization.Condition);
        Assert.True(stabilization.Evidence.AfterAdjacentWorkOrControlledDistractor);
    }

    [Fact]
    public async Task TransferSessionIsEvaluatedForEligibilityAndPersistedWithoutAppAdvancement()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.WM, GlobalLevelId.L4, BranchLevelState.TestReady));
        var session = CreateSessionDefinition(
            SessionType.Transfer,
            BranchCode.WM,
            GlobalLevelId.L4,
            DrillId.WM1DelayedReconstruction);
        var transferDefinition = TransferTestCatalog.TransferTests.Single(test => test.SourceBranch == BranchCode.WM);
        var result = CompleteWithEvidence(
            "session-transfer-eligible",
            session,
            RuntimeEvidenceCaptureKind.Transfer,
            [
                new RuntimeEventFact("branch_mapping", "WM encoding delay and no-invention standard visible in unfamiliar content"),
                new RuntimeEventFact("score", "reconstruction accuracy met WM transfer standard"),
            ]);

        var processed = await new CompletedRuntimeSessionProcessor(configuration).ProcessAsync(
            new CompletedRuntimeSessionProcessingRequest(
                result,
                Metadata(
                    LocalSessionIntensity.Moderate,
                    cleanPerformance: true,
                    "Transfer preserved the WM source standard in a changed context.",
                    transferTask: transferDefinition.TransferTask),
                transfer: new RuntimeTransferEligibilityHandoffInput(
                    GlobalLevelId.L3,
                    transferDefinition.TransferTask,
                    CapacityId.EncodingFidelity,
                    transferDefinition.SameDemand,
                    transferDefinition.ChangedContext,
                    new TransferSourceStandardEvidence(
                        BranchCode.WM,
                        GlobalLevelId.L3,
                        StandardFor(BranchCode.WM, GlobalLevelId.L3),
                        visibleInTransferArtifact: true),
                    transferDefinition.RetestRequirement)));

        var sessionRecord = await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-transfer-eligible");
        var artifact = await new LocalEvidenceArtifactStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-transfer-eligible-artifact-1");
        var attempt = await new LocalFormalTestAttemptStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-transfer-eligible-formal-attempt");

        Assert.True(processed.TransferEligibilityResult!.IsEligible);
        Assert.False(processed.GrantsAdvancementInApp);
        Assert.NotNull(sessionRecord);
        Assert.Equal(LocalCompletedSessionType.Transfer, sessionRecord.SessionType);
        Assert.Equal(transferDefinition.TransferTask, sessionRecord.TransferTask);
        Assert.NotNull(artifact);
        Assert.Equal(EvidenceArtifactCategory.Transfer, artifact.Artifact.Category);
        Assert.Null(attempt);
    }

    [Fact]
    public async Task FailedDailyBlockAndCompletedSessionCommitAsOneProgrammingEvent()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training));
        await new LocalDailyTrainingPrescriptionStore(configuration.LocalDatabaseOptions).SaveAsync(
            DailyPrescription("session-daily-failed"));
        var session = CreateSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold);
        var result = CompleteWithEvidence(
            "session-daily-failed",
            session,
            RuntimeEvidenceCaptureKind.BestSet,
            [new RuntimeEventFact("score", "drifts=8")]);

        var processed = await new CompletedRuntimeSessionProcessor(configuration).ProcessAsync(
            new CompletedRuntimeSessionProcessingRequest(
                result,
                Metadata(LocalSessionIntensity.Moderate, cleanPerformance: true, "Daily block completed below its standard."),
                EvaluatedStandard("drifts", maxAllowed: 5),
                standardEvaluation: new RuntimeStandardEvaluationHandoffInput(
                    [new NumericMeasurement("drifts", 8)],
                    [new CriticalConstraintCheck("catalog-constraint", true)],
                    outputComplete: true,
                    rubricOutcome: null)));

        var stored = await new LocalDailyTrainingPrescriptionStore(configuration.LocalDatabaseOptions)
            .LoadByDateAsync(SessionDate);

        Assert.Equal(DailyTrainingWorkflowStatus.BetweenBlocks, processed.DailyTraining?.Status);
        Assert.Equal(DailyTrainingDoseState.Active, stored?.State);
        Assert.Equal(LocalDailyTrainingBlockState.Failed, stored?.Blocks[0].State);
        Assert.Equal(LocalDailyTrainingBlockState.Planned, stored?.Blocks[1].State);
        Assert.NotNull(await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-daily-failed"));
    }

    [Fact]
    public async Task AbandoningActiveDailyWorkStopsAndConsumesRemainingBlocks()
    {
        var configuration = Configuration();
        await SaveStateAsync(configuration, Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training));
        await new LocalDailyTrainingPrescriptionStore(configuration.LocalDatabaseOptions).SaveAsync(
            DailyPrescription("session-daily-abandoned"));
        var session = CreateSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold);

        var processed = await new CompletedRuntimeSessionProcessor(configuration).ProcessAsync(
            new CompletedRuntimeSessionProcessingRequest(
                AbandonedResult("session-daily-abandoned", session),
                Metadata(LocalSessionIntensity.Moderate, cleanPerformance: true, "Active daily work was explicitly stopped.")));

        var stored = await new LocalDailyTrainingPrescriptionStore(configuration.LocalDatabaseOptions)
            .LoadByDateAsync(SessionDate);

        Assert.Equal(DailyTrainingWorkflowStatus.Stopped, processed.DailyTraining?.Status);
        Assert.Equal(DailyTrainingDoseState.Stopped, stored?.State);
        Assert.Equal(LocalDailyTrainingBlockState.Abandoned, stored?.Blocks[0].State);
        Assert.Equal(LocalDailyTrainingBlockState.Skipped, stored?.Blocks[1].State);
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

    private static async ValueTask SaveExistingStabilizationAsync(
        AppStartupConfiguration configuration,
        string passId,
        string artifactId,
        TrainingDate date,
        FormalTestPassState passState,
        bool afterAdjacent)
    {
        var artifact = new LocalEvidenceArtifactRecord(
            artifactId,
            new LocalProgrammingEventReference(
                passId,
                LocalProgrammingEventKind.Stabilization,
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold),
            Artifact(
                EvidenceArtifactCategory.Stabilization,
                date,
                ObservableEvidenceKind.RepeatabilityRecord,
                $"{passId} clean stabilization evidence."));
        var record = new LocalStabilizationPassRecord(
            passId,
            artifactId,
            null,
            null,
            DrillId.FH1TargetHold,
            afterAdjacent ? LocalStabilizationCondition.ControlledDistractor : LocalStabilizationCondition.OrdinaryVariance,
            afterAdjacent ? "After controlled distractor." : "Ordinary variance repeat.",
            new StabilizationPassEvidence(
                BranchCode.FH,
                GlobalLevelId.L1,
                date,
                StandardFor(BranchCode.FH, GlobalLevelId.L1),
                passState,
                new StandardEvaluationResult(true, []),
                afterAdjacent,
                "Target substitution avoided."));

        await new LocalEvidenceArtifactStore(configuration.LocalDatabaseOptions).SaveAsync(artifact);
        await new LocalStabilizationPassStore(configuration.LocalDatabaseOptions).SaveAsync(record);
    }

    private static async ValueTask SaveExistingFormalPassAsync(
        AppStartupConfiguration configuration,
        string attemptId,
        string artifactId,
        TrainingDate date)
    {
        var artifact = new LocalEvidenceArtifactRecord(
            artifactId,
            new LocalProgrammingEventReference(
                attemptId,
                LocalProgrammingEventKind.FormalTest,
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold),
            Artifact(
                EvidenceArtifactCategory.Test,
                date,
                ObservableEvidenceKind.Score,
                $"{attemptId} clean formal-pass evidence."));
        var attempt = new FormalTestAttempt(
            BranchCode.FH,
            GlobalLevelId.L1,
            date,
            TestTask.ForDrill(DrillId.FH1TargetHold),
            [new LoadVariable("duration", "3 minutes")],
            StandardFor(BranchCode.FH, GlobalLevelId.L1),
            [new CriticalConstraint(HonestyConstraintFor(DrillId.FH1TargetHold))],
            new TestResultEvidence(TestResultEvidenceKind.Score, "Formal standard passed."),
            failureType: null,
            FormalTestPassState.PassOnce,
            artifact.Artifact,
            "Target substitution avoided.");

        await new LocalEvidenceArtifactStore(configuration.LocalDatabaseOptions).SaveAsync(artifact);
        await new LocalFormalTestAttemptStore(configuration.LocalDatabaseOptions).SaveAsync(
            new LocalFormalTestAttemptRecord(attemptId, artifactId, attempt));
    }

    private static RuntimePersistenceHandoffMetadata Metadata(
        LocalSessionIntensity intensity,
        bool cleanPerformance,
        string notes,
        string? transferTask = null)
    {
        return new RuntimePersistenceHandoffMetadata(
            SessionDate,
            intensity,
            cleanPerformance,
            notes,
            transferTask);
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
        IReadOnlyList<RuntimeEventFact> facts,
        RuntimeSessionCompletionStatus status = RuntimeSessionCompletionStatus.Completed)
    {
        var log = RuntimeEventLog.Start(sessionId, session, RuntimeInstant.Zero);
        var eventKind = status == RuntimeSessionCompletionStatus.Failed
            ? RuntimeEventKind.ErrorRecorded
            : RuntimeEventKind.AnswerSubmitted;
        log.Append(
            eventKind,
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
            status,
            [CompletedPhase("active", RuntimeSessionPhaseKind.ActiveWork, 0, 185)],
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
            Instant(35),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [new RuntimeEventFact("abandon_reason", "user abandoned before producing evidence")]);

        return RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            sessionId,
            session,
            RuntimeSessionCompletionStatus.Abandoned,
            [CompletedPhase("active", RuntimeSessionPhaseKind.ActiveWork, 0, 35)],
            log.Events,
            [],
            []));
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

    private static RuntimeSessionDefinition CreateSessionDefinition(
        SessionType sessionType,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        string? generatedInstanceId = null)
    {
        return new RuntimeSessionDefinition(
            sessionType,
            branch,
            level,
            drill,
            [new LoadVariable("catalog-demand", "standard fixture")],
            ProgramCatalog.Standards.Single(standard => standard.Branch == branch && standard.Level == level),
            [new CriticalConstraint(HonestyConstraintFor(drill))],
            generatedInstanceId is null ? null : GeneratedInstance(generatedInstanceId, branch, level, drill));
    }

    private static RuntimeGeneratedDrillInstanceIdentity GeneratedInstance(
        string instanceId,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill)
    {
        return new RuntimeGeneratedDrillInstanceIdentity(
            instanceId,
            new PromptContentIdentity(
                $"content-{instanceId}",
                branch,
                level,
                drill,
                PromptContentKind.EquivalentPrompt,
                $"{branch.ToString().ToLowerInvariant()}-{level.ToString().ToLowerInvariant()}-equivalent"),
            "v1");
    }

    private static LocalSessionHistoryRecord FormalLoadPracticeSession(
        string sessionId,
        TrainingDate date,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill)
    {
        return new LocalSessionHistoryRecord(
            sessionId,
            date,
            LocalCompletedSessionType.Practice,
            [new LocalSessionBranchLevel(branch, level)],
            drill,
            transferTask: null,
            LocalSessionIntensity.Moderate,
            TrainingLoadProfileCatalog.Get(branch, level).TargetStage.LoadVariables,
            cleanPerformance: true,
            notes: "Clean practice at the formal standard load.",
            recoveryMarked: false,
            deloadMarked: false,
            evidenceArtifactIds: [$"artifact-{sessionId}"]);
    }

    private static async ValueTask SaveFormalLoadPracticeSessionAsync(
        AppStartupConfiguration configuration,
        string sessionId,
        TrainingDate date,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill)
    {
        var artifactId = $"artifact-{sessionId}";
        await new LocalEvidenceArtifactStore(configuration.LocalDatabaseOptions).SaveAsync(
            new LocalEvidenceArtifactRecord(
                artifactId,
                new LocalProgrammingEventReference(
                    sessionId,
                    LocalProgrammingEventKind.Practice,
                    branch,
                    level,
                    drill),
                Artifact(
                    EvidenceArtifactCategory.Practice,
                    date,
                    ObservableEvidenceKind.OutputSample,
                    $"{sessionId} clean formal-load practice evidence.")));
        await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions).SaveAsync(
            FormalLoadPracticeSession(sessionId, date, branch, level, drill));
    }

    private static LocalDailyTrainingPrescriptionRecord DailyPrescription(string sessionId)
    {
        return new LocalDailyTrainingPrescriptionRecord(
            "daily-20260705",
            SessionDate,
            SessionDate,
            cycleDay: 1,
            WeeklySessionKind.Practice,
            DailyTrainingDoseState.Active,
            [
                new LocalDailyTrainingBlockRecord(
                    "daily-20260705-block-01",
                    1,
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    DrillId.FH1TargetHold,
                    LocalDailyTrainingBlockRole.Practice,
                    [new LoadVariable("catalog-demand", "standard fixture")],
                    LocalDailyTrainingBlockState.Active,
                    sessionId),
                new LocalDailyTrainingBlockRecord(
                    "daily-20260705-block-02",
                    2,
                    BranchCode.FS,
                    GlobalLevelId.L1,
                    DrillId.FS1CueSwitch,
                    LocalDailyTrainingBlockRole.Practice,
                    [new LoadVariable("switch count", "3")],
                    LocalDailyTrainingBlockState.Planned),
            ]);
    }

    private static EvidenceArtifact Artifact(
        EvidenceArtifactCategory category,
        TrainingDate date,
        ObservableEvidenceKind kind,
        string evidence)
    {
        return new EvidenceArtifact(
            category,
            date,
            [new ObservableEvidence(kind, evidence)],
            evidence);
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
