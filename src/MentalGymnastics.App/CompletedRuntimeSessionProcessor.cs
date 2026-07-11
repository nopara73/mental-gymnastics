using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App;

public sealed class CompletedRuntimeSessionProcessingRequest
{
    public CompletedRuntimeSessionProcessingRequest(
        RuntimeSessionCompletionResult result,
        RuntimePersistenceHandoffMetadata persistenceMetadata,
        EvaluatedStandard? evaluatedStandard = null,
        RuntimeStandardEvaluationHandoffInput? standardEvaluation = null,
        RuntimeFormalGateHandoffInput? formalGate = null,
        RuntimeReadinessPracticeHandoffInput? readinessPractice = null,
        RuntimeStabilizationCoreHandoffInput? stabilization = null,
        RuntimeMaintenanceCoreHandoffInput? maintenance = null,
        RuntimeTransferEligibilityHandoffInput? transfer = null,
        RuntimeFailureResponseHandoffInput? failureResponse = null,
        RuntimeFormalAttemptPersistenceInput? formalAttemptPersistence = null,
        RuntimeStabilizationPersistenceInput? stabilizationPersistence = null,
        RuntimeMaintenancePersistenceInput? maintenancePersistence = null,
        bool refreshProgressSummary = true,
        LocalProgressSummaryRefreshRequest? progressSummaryRefresh = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(persistenceMetadata);

        if (standardEvaluation is not null && evaluatedStandard is null)
        {
            throw new ArgumentException(
                "Completed-session standard evaluation requires the caller-provided core standard.",
                nameof(evaluatedStandard));
        }

        Result = result;
        PersistenceMetadata = persistenceMetadata;
        EvaluatedStandard = evaluatedStandard;
        StandardEvaluation = standardEvaluation;
        FormalGate = formalGate;
        ReadinessPractice = readinessPractice;
        Stabilization = stabilization;
        Maintenance = maintenance;
        Transfer = transfer;
        FailureResponse = failureResponse;
        FormalAttemptPersistence = formalAttemptPersistence;
        StabilizationPersistence = stabilizationPersistence;
        MaintenancePersistence = maintenancePersistence;
        RefreshProgressSummary = refreshProgressSummary;
        ProgressSummaryRefresh = progressSummaryRefresh;
    }

    public RuntimeSessionCompletionResult Result { get; }

    public RuntimePersistenceHandoffMetadata PersistenceMetadata { get; }

    public EvaluatedStandard? EvaluatedStandard { get; }

    public RuntimeStandardEvaluationHandoffInput? StandardEvaluation { get; }

    public RuntimeFormalGateHandoffInput? FormalGate { get; }

    public RuntimeReadinessPracticeHandoffInput? ReadinessPractice { get; }

    public RuntimeStabilizationCoreHandoffInput? Stabilization { get; }

    public RuntimeMaintenanceCoreHandoffInput? Maintenance { get; }

    public RuntimeTransferEligibilityHandoffInput? Transfer { get; }

    public RuntimeFailureResponseHandoffInput? FailureResponse { get; }

    public RuntimeFormalAttemptPersistenceInput? FormalAttemptPersistence { get; }

    public RuntimeStabilizationPersistenceInput? StabilizationPersistence { get; }

    public RuntimeMaintenancePersistenceInput? MaintenancePersistence { get; }

    public bool RefreshProgressSummary { get; }

    public LocalProgressSummaryRefreshRequest? ProgressSummaryRefresh { get; }
}

public sealed record CompletedRuntimeSessionProcessingResult(
    RuntimeSessionCompletionStatus CompletionStatus,
    RuntimeCoreEvaluationHandoff? CoreHandoff,
    RuntimePersistenceHandoffRecords? PersistenceHandoff,
    LocalSessionHistoryRecord SessionHistory,
    IReadOnlyList<LocalEvidenceArtifactRecord> EvidenceArtifacts,
    LocalFormalTestAttemptRecord? FormalTestAttempt,
    LocalStabilizationPassRecord? StabilizationPass,
    LocalMaintenanceCheckRecord? MaintenanceCheck,
    LocalGeneratedDrillInstanceRecord? GeneratedDrillInstance,
    StandardEvaluationResult? StandardEvaluationResult,
    FormalGateDecision? FormalGateDecision,
    StabilizationOwnershipResult? StabilizationOwnershipResult,
    MaintenanceCurrencyResult? MaintenanceCurrencyResult,
    DecayRestorationResult? DecayResult,
    TransferEligibilityResult? TransferEligibilityResult,
    FailureResponse? FailureResponse,
    BranchLevelStatusTransitionResult? StateTransition,
    LocalProgressSummaryRecord? ProgressSummary,
    DailyTrainingWorkflowReadModel? DailyTraining = null,
    PractitionerProgressionProjectionResult? ProgressionProjection = null)
{
    public bool GrantsAdvancementInApp => false;
}

public sealed class CompletedRuntimeSessionProcessor
{
    private readonly LocalDatabaseOptions options;
    private readonly LocalPractitionerStateStore practitionerStateStore;
    private readonly LocalSessionHistoryStore sessionHistoryStore;
    private readonly LocalFormalTestAttemptStore formalTestAttemptStore;
    private readonly LocalStabilizationPassStore stabilizationPassStore;
    private readonly LocalProgramRepository repository;
    private readonly LocalDailyTrainingPrescriptionStore dailyTrainingStore;
    private readonly LocalProgrammingEventTransaction transaction;
    private readonly LocalProgressSummaryStore progressSummaryStore;

    public CompletedRuntimeSessionProcessor(AppStartupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        options = configuration.LocalDatabaseOptions;
        practitionerStateStore = new LocalPractitionerStateStore(options);
        sessionHistoryStore = new LocalSessionHistoryStore(options);
        formalTestAttemptStore = new LocalFormalTestAttemptStore(options);
        stabilizationPassStore = new LocalStabilizationPassStore(options);
        repository = new LocalProgramRepository(options);
        dailyTrainingStore = new LocalDailyTrainingPrescriptionStore(options);
        transaction = new LocalProgrammingEventTransaction(options);
        progressSummaryStore = new LocalProgressSummaryStore(options);
    }

    public async ValueTask<CompletedRuntimeSessionProcessingResult> ProcessAsync(
        CompletedRuntimeSessionProcessingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var currentState = await practitionerStateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        var coreHandoff = BuildCoreHandoff(request);
        var coreEvaluation = await EvaluateCoreAsync(request, coreHandoff, currentState, cancellationToken)
            .ConfigureAwait(false);
        var persistenceRecords = BuildPersistenceRecords(
            request,
            coreEvaluation.StandardEvaluationResult);
        var stateTransition = DetermineStateTransition(
            request.Result,
            currentState,
            coreEvaluation.FormalGateDecision,
            coreEvaluation.StabilizationOwnershipResult,
            coreEvaluation.MaintenanceCurrencyResult,
            coreEvaluation.DecayResult,
            coreEvaluation.TestReadinessResult);
        var nextState = ApplyTransition(currentState, stateTransition);
        PractitionerProgressionProjectionResult? progressionProjection = null;
        if (nextState is not null)
        {
            var projectionMaintenance = await LoadProjectionMaintenanceAsync(
                nextState,
                request.PersistenceMetadata.Date,
                coreEvaluation.MaintenanceCurrencyResult,
                cancellationToken).ConfigureAwait(false);
            var reviewFacts = await repository.LoadReviewCadenceFactsAsync(
                request.PersistenceMetadata.Date,
                cancellationToken).ConfigureAwait(false);
            progressionProjection = PractitionerProgressionProjector.Project(
                new PractitionerProgressionProjectionRequest(
                    nextState,
                    projectionMaintenance,
                    PersistedGlobalReviewResultFactory.From(reviewFacts)));
        }
        nextState = progressionProjection?.PractitionerState ?? nextState;
        var dailyTrainingRecord = await dailyTrainingStore.LoadByDateAsync(
            request.PersistenceMetadata.Date,
            cancellationToken).ConfigureAwait(false);
        var updatedDailyTraining = dailyTrainingRecord is not null &&
            dailyTrainingRecord.Blocks.Any(block => string.Equals(
                block.SessionId,
                request.Result.SessionId,
                StringComparison.Ordinal))
                ? DailyTrainingWorkflowService.ApplyTerminalResult(
                    dailyTrainingRecord,
                    request.Result.SessionId,
                    request.Result.CompletionStatus,
                    coreEvaluation.StandardEvaluationResult)
                : null;

        await transaction.CommitAsync(context =>
        {
            if (nextState is not null)
            {
                context.SetPractitionerState(nextState);
            }

            foreach (var artifact in persistenceRecords.EvidenceArtifacts)
            {
                context.SaveEvidenceArtifact(artifact);
            }

            if (persistenceRecords.FormalTestAttempt is not null)
            {
                context.SaveFormalTestAttempt(persistenceRecords.FormalTestAttempt);
            }

            context.SaveCompletedSession(persistenceRecords.SessionHistory);

            if (persistenceRecords.StabilizationPass is not null)
            {
                context.SaveStabilizationPass(persistenceRecords.StabilizationPass);
            }

            if (persistenceRecords.MaintenanceCheck is not null)
            {
                context.SaveMaintenanceCheck(persistenceRecords.MaintenanceCheck);
            }

            if (persistenceRecords.GeneratedDrillInstance is not null)
            {
                context.SaveGeneratedDrillInstance(persistenceRecords.GeneratedDrillInstance);
            }

            if (updatedDailyTraining is not null)
            {
                context.SaveDailyTrainingPrescription(updatedDailyTraining);
            }

            return ValueTask.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);

        var summary = request.RefreshProgressSummary
            ? await progressSummaryStore.RefreshAsync(
                request.ProgressSummaryRefresh ?? DefaultSummaryRefresh(request),
                cancellationToken).ConfigureAwait(false)
            : null;

        return new CompletedRuntimeSessionProcessingResult(
            request.Result.CompletionStatus,
            coreHandoff,
            persistenceRecords.RuntimeHandoff,
            persistenceRecords.SessionHistory,
            persistenceRecords.EvidenceArtifacts,
            persistenceRecords.FormalTestAttempt,
            persistenceRecords.StabilizationPass,
            persistenceRecords.MaintenanceCheck,
            persistenceRecords.GeneratedDrillInstance,
            coreEvaluation.StandardEvaluationResult,
            coreEvaluation.FormalGateDecision,
            coreEvaluation.StabilizationOwnershipResult,
            coreEvaluation.MaintenanceCurrencyResult,
            coreEvaluation.DecayResult,
            coreEvaluation.TransferEligibilityResult,
            coreEvaluation.FailureResponse,
            stateTransition,
            summary,
            updatedDailyTraining is null
                ? null
                : DailyTrainingWorkflowService.From(updatedDailyTraining),
            progressionProjection);
    }

    private static RuntimeCoreEvaluationHandoff? BuildCoreHandoff(
        CompletedRuntimeSessionProcessingRequest request)
    {
        if (request.StandardEvaluation is null &&
            request.FormalGate is null &&
            request.ReadinessPractice is null &&
            request.Stabilization is null &&
            request.Maintenance is null &&
            request.Transfer is null &&
            request.FailureResponse is null)
        {
            return null;
        }

        return RuntimeCoreEvaluationHandoffMapper.Map(new RuntimeCoreEvaluationHandoffRequest(
            request.Result,
            request.StandardEvaluation,
            request.FormalGate,
            request.ReadinessPractice,
            request.Stabilization,
            request.Maintenance,
            request.Transfer,
            request.FailureResponse));
    }

    private async ValueTask<CoreEvaluationResults> EvaluateCoreAsync(
        CompletedRuntimeSessionProcessingRequest request,
        RuntimeCoreEvaluationHandoff? handoff,
        PractitionerState? currentState,
        CancellationToken cancellationToken)
    {
        if (handoff is null)
        {
            return CoreEvaluationResults.Empty;
        }

        var standardResult = request.EvaluatedStandard is not null &&
            handoff.StandardEvaluationAttempt is not null
                ? StandardEvaluator.Evaluate(request.EvaluatedStandard, handoff.StandardEvaluationAttempt)
                : null;

        var gateDecision = handoff.FormalTestAttempt is not null && standardResult is not null
            ? FormalGateDecisionEngine.Decide(handoff.FormalTestAttempt, standardResult)
            : null;

        var stabilizationOwnership = handoff.StabilizationPass is not null
            ? await EvaluateStabilizationOwnershipAsync(handoff.StabilizationPass, cancellationToken)
                .ConfigureAwait(false)
            : null;

        var maintenanceCurrency = handoff.MaintenanceCheck is not null
            ? await EvaluateMaintenanceCurrencyAsync(handoff.MaintenanceCheck, cancellationToken)
                .ConfigureAwait(false)
            : null;

        var decayResult = maintenanceCurrency is { State: MaintenanceCurrencyState.Failed } &&
            currentState is not null &&
            TryCurrentStatus(currentState, request.Result.Branch, maintenanceCurrency.OwnedLevel, out var maintenanceStatus)
                ? DecayRestorationEvaluator.EvaluateDecay(maintenanceStatus, maintenanceCurrency)
                : null;

        var transferEligibility = handoff.TransferEligibilityRequest is not null
            ? TransferEligibilityEvaluator.Evaluate(handoff.TransferEligibilityRequest)
            : null;

        var failureResponse = handoff.FailureResponseRequest is not null
            ? FailureResponseRouter.Route(handoff.FailureResponseRequest)
            : null;

        var testReadiness = handoff.ReadinessPracticeSession is { } readinessPractice &&
            currentState is not null
                ? await EvaluateTestReadinessAsync(
                    currentState,
                    readinessPractice,
                    request.PersistenceMetadata.Date,
                    cancellationToken).ConfigureAwait(false)
                : null;

        return new CoreEvaluationResults(
            standardResult,
            gateDecision,
            stabilizationOwnership,
            maintenanceCurrency,
            decayResult,
            transferEligibility,
            failureResponse,
            testReadiness);
    }

    private async ValueTask<TestReadinessResult> EvaluateTestReadinessAsync(
        PractitionerState currentState,
        TestReadinessPracticeSession currentPractice,
        TrainingDate asOf,
        CancellationToken cancellationToken)
    {
        var recentSessions = await sessionHistoryStore.ListAsync(cancellationToken).ConfigureAwait(false);
        var existingPractice = TestReadinessRequestFactory.FromSessionHistory(
            recentSessions.TakeLast(9));
        var maintenanceCurrency = new List<MaintenanceCurrencyResult>();
        foreach (var status in currentState.BranchLevels.Where(IsMaintenanceRelevant))
        {
            var currency = await repository.LoadMaintenanceCurrencyAsync(
                status.Branch,
                status.Level,
                asOf,
                cancellationToken).ConfigureAwait(false);
            maintenanceCurrency.Add(currency);
        }

        return TestReadinessEvaluator.Evaluate(TestReadinessRequestFactory.Create(
            currentState,
            currentPractice.Branch,
            currentPractice.Level,
            currentPractice.Drill,
            existingPractice.Append(currentPractice),
            maintenanceCurrency));
    }

    private async ValueTask<StabilizationOwnershipResult> EvaluateStabilizationOwnershipAsync(
        StabilizationPassEvidence currentPass,
        CancellationToken cancellationToken)
    {
        var existing = await stabilizationPassStore
            .ListByBranchLevelAsync(currentPass.Branch, currentPass.Level, cancellationToken)
            .ConfigureAwait(false);
        var formalPasses = await formalTestAttemptStore
            .ListByBranchLevelAsync(currentPass.Branch, currentPass.Level, cancellationToken)
            .ConfigureAwait(false);
        var firstFormalPass = formalPasses
            .Where(record => record.Attempt.PassState == FormalTestPassState.PassOnce)
            .OrderBy(record => record.Attempt.Date.Year)
            .ThenBy(record => record.Attempt.Date.Month)
            .ThenBy(record => record.Attempt.Date.Day)
            .Select(record => record.Attempt)
            .FirstOrDefault();
        var passes = existing
            .Select(record => record.Evidence)
            .Prepend(firstFormalPass is null
                ? null
                : new StabilizationPassEvidence(
                    firstFormalPass.Branch,
                    firstFormalPass.Level,
                    firstFormalPass.Date,
                    firstFormalPass.Standard,
                    firstFormalPass.PassState,
                    new StandardEvaluationResult(Passed: true, Failures: []),
                    afterAdjacentWorkOrControlledDistractor: false,
                    firstFormalPass.MainFailureModeAvoided ?? string.Empty))
            .OfType<StabilizationPassEvidence>()
            .Append(currentPass)
            .ToArray();

        return StabilizationOwnershipEvaluator.Evaluate(
            new StabilizationEvidence(currentPass.Branch, currentPass.Level, passes));
    }

    private async ValueTask<MaintenanceCurrencyResult> EvaluateMaintenanceCurrencyAsync(
        MaintenanceCheckEvidence currentCheck,
        CancellationToken cancellationToken)
    {
        var existing = await repository.LoadMaintenanceCurrencyRequestAsync(
            currentCheck.Branch,
            currentCheck.OwnedLevel,
            currentCheck.Date,
            cancellationToken).ConfigureAwait(false);

        return MaintenanceCurrencyEvaluator.Evaluate(new MaintenanceCurrencyRequest(
            currentCheck.Branch,
            currentCheck.OwnedLevel,
            currentCheck.Date,
            existing.Checks.Append(currentCheck)));
    }

    private static PersistenceRecords BuildPersistenceRecords(
        CompletedRuntimeSessionProcessingRequest request,
        StandardEvaluationResult? standardEvaluationResult)
    {
        if (request.Result.CompletionStatus == RuntimeSessionCompletionStatus.Abandoned)
        {
            return BuildAbandonedPersistenceRecords(request);
        }

        var handoff = RuntimePersistenceHandoffMapper.Map(new RuntimePersistenceHandoffRequest(
            request.Result,
            PersistenceMetadataFor(request, standardEvaluationResult),
            request.FormalAttemptPersistence ?? BuildFormalAttemptPersistenceInput(request.FormalGate),
            request.StabilizationPersistence ?? BuildStabilizationPersistenceInput(request.Stabilization),
            request.MaintenancePersistence ?? BuildMaintenancePersistenceInput(request.Maintenance)));

        return new PersistenceRecords(
            handoff,
            handoff.SessionHistory,
            handoff.EvidenceArtifacts,
            handoff.FormalTestAttempt,
            handoff.StabilizationPass,
            handoff.MaintenanceCheck,
            handoff.GeneratedDrillInstance);
    }

    private static RuntimePersistenceHandoffMetadata PersistenceMetadataFor(
        CompletedRuntimeSessionProcessingRequest request,
        StandardEvaluationResult? standardEvaluationResult)
    {
        if (standardEvaluationResult is null)
        {
            return request.PersistenceMetadata;
        }

        var metadata = request.PersistenceMetadata;
        return new RuntimePersistenceHandoffMetadata(
            metadata.Date,
            metadata.Intensity,
            cleanPerformance:
                request.Result.CompletionStatus == RuntimeSessionCompletionStatus.Completed &&
                standardEvaluationResult.Passed,
            metadata.Notes,
            metadata.TransferTask,
            metadata.RecoveryMarked,
            metadata.DeloadMarked,
            metadata.GeneratedInstanceDate);
    }

    private static RuntimeFormalAttemptPersistenceInput? BuildFormalAttemptPersistenceInput(
        RuntimeFormalGateHandoffInput? formalGate)
    {
        return formalGate is null
            ? null
            : new RuntimeFormalAttemptPersistenceInput(
                formalGate.ResultEvidence,
                formalGate.PassState,
                formalGate.FailureType,
                task: formalGate.Task,
                mainFailureModeAvoided: formalGate.MainFailureModeAvoided);
    }

    private static RuntimeStabilizationPersistenceInput? BuildStabilizationPersistenceInput(
        RuntimeStabilizationCoreHandoffInput? stabilization)
    {
        if (stabilization is null)
        {
            return null;
        }

        var condition = stabilization.AfterAdjacentWorkOrControlledDistractor
            ? LocalStabilizationCondition.ControlledDistractor
            : LocalStabilizationCondition.OrdinaryVariance;
        var description = stabilization.AfterAdjacentWorkOrControlledDistractor
            ? "Runtime stabilization pass after adjacent work or controlled distractor."
            : "Runtime stabilization pass under ordinary variance.";

        return new RuntimeStabilizationPersistenceInput(
            stabilization.StandardEvaluationResult,
            stabilization.PassState,
            condition,
            description,
            stabilization.MainFailureModeAvoided);
    }

    private static RuntimeMaintenancePersistenceInput? BuildMaintenancePersistenceInput(
        RuntimeMaintenanceCoreHandoffInput? maintenance)
    {
        return maintenance is null
            ? null
            : new RuntimeMaintenancePersistenceInput(
                maintenance.OwnedLevel,
                maintenance.Kind,
                maintenance.StandardEvaluationResult);
    }

    private static PersistenceRecords BuildAbandonedPersistenceRecords(
        CompletedRuntimeSessionProcessingRequest request)
    {
        var result = request.Result;
        var category = CategoryForSession(result.SessionType);
        var eventKind = EventKindForCategory(category);
        var artifactId = $"{result.SessionId}-abandoned-artifact";
        var artifact = new LocalEvidenceArtifactRecord(
            artifactId,
            new LocalProgrammingEventReference(
                result.SessionId,
                eventKind,
                result.Branch,
                result.Level,
                result.Drill),
            new EvidenceArtifact(
                category,
                request.PersistenceMetadata.Date,
                [new ObservableEvidence(ObservableEvidenceKind.FailedItemList, AbandonedEvidenceSummary(result))],
                $"Abandoned runtime session {result.SessionId}; no successful evidence produced."));
        var session = new LocalSessionHistoryRecord(
            result.SessionId,
            request.PersistenceMetadata.Date,
            MapSessionType(result.SessionType),
            [new LocalSessionBranchLevel(result.Branch, result.Level)],
            result.Drill,
            request.PersistenceMetadata.TransferTask,
            request.PersistenceMetadata.Intensity,
            result.LoadVariables,
            cleanPerformance: false,
            request.PersistenceMetadata.Notes,
            request.PersistenceMetadata.RecoveryMarked,
            request.PersistenceMetadata.DeloadMarked,
            [artifactId]);
        var generated = result.SessionDefinition.GeneratedDrillInstance is { } identity
            ? new LocalGeneratedDrillInstanceRecord(
                identity.InstanceId,
                request.PersistenceMetadata.GeneratedInstanceDate,
                result.Branch,
                result.Level,
                result.Drill,
                result.LoadVariables,
                new LocalGeneratedDrillContentIdentity(identity.ContentIdentity, identity.ContentVersion),
                LocalGeneratedDrillInstanceState.Abandoned)
            : null;

        return new PersistenceRecords(
            null,
            session,
            [artifact],
            null,
            null,
            null,
            generated);
    }

    private static BranchLevelStatusTransitionResult? DetermineStateTransition(
        RuntimeSessionCompletionResult result,
        PractitionerState? currentState,
        FormalGateDecision? gateDecision,
        StabilizationOwnershipResult? ownership,
        MaintenanceCurrencyResult? maintenanceCurrency,
        DecayRestorationResult? decayResult,
        TestReadinessResult? testReadiness)
    {
        if (currentState is null)
        {
            return null;
        }

        if (gateDecision is not null &&
            TryCurrentStatus(currentState, result.Branch, result.Level, out var testStatus))
        {
            if (gateDecision.Outcome == GateOutcome.PassOnce)
            {
                return ValidTransitionOrNull(testStatus, BranchLevelTransition.PassFormalTestOnce);
            }

            if (gateDecision.Outcome == GateOutcome.Fail)
            {
                return ValidTransitionOrNull(testStatus, BranchLevelTransition.FailFormalTest);
            }
        }

        if (ownership is not null &&
            TryCurrentStatus(currentState, result.Branch, result.Level, out var stabilizationStatus))
        {
            if (ownership.IsOwned)
            {
                return ValidTransitionOrNull(stabilizationStatus, BranchLevelTransition.CompleteStabilization);
            }

            if (ownership.BranchLevelState == BranchLevelState.Stabilizing)
            {
                return ValidTransitionOrNull(stabilizationStatus, BranchLevelTransition.EnterStabilization);
            }
        }

        if (decayResult is { ChangedState: true })
        {
            return BranchLevelStateMachine.TryApply(
                decayResult.CurrentStatus,
                decayResult.Transition!.Value);
        }

        if (maintenanceCurrency is not null &&
            maintenanceCurrency.State == MaintenanceCurrencyState.Current &&
            TryCurrentStatus(currentState, result.Branch, maintenanceCurrency.OwnedLevel, out var maintenanceStatus))
        {
            return ValidTransitionOrNull(maintenanceStatus, BranchLevelTransition.PassMaintenance);
        }

        if (testReadiness?.MayTest == true &&
            TryCurrentStatus(currentState, result.Branch, result.Level, out var trainingStatus))
        {
            return ValidTransitionOrNull(trainingStatus, BranchLevelTransition.MarkTestReady);
        }

        return null;
    }

    private static bool IsMaintenanceRelevant(BranchLevelStatus status)
    {
        return status.State is
            BranchLevelState.Owned or
            BranchLevelState.Maintenance or
            BranchLevelState.Decayed;
    }

    private async ValueTask<IReadOnlyList<MaintenanceCurrencyResult>> LoadProjectionMaintenanceAsync(
        PractitionerState state,
        TrainingDate asOf,
        MaintenanceCurrencyResult? justEvaluated,
        CancellationToken cancellationToken)
    {
        var results = new List<MaintenanceCurrencyResult>();
        foreach (var status in state.BranchLevels
            .Where(IsMaintenanceRelevant)
            .DistinctBy(item => (item.Branch, item.Level)))
        {
            var result = justEvaluated is not null &&
                justEvaluated.Branch == status.Branch &&
                justEvaluated.OwnedLevel == status.Level
                    ? justEvaluated
                    : await repository.LoadMaintenanceCurrencyAsync(
                        status.Branch,
                        status.Level,
                        asOf,
                        cancellationToken).ConfigureAwait(false);
            results.Add(result);
        }

        return results;
    }

    private static BranchLevelStatusTransitionResult? ValidTransitionOrNull(
        BranchLevelStatus status,
        BranchLevelTransition transition)
    {
        var result = BranchLevelStateMachine.TryApply(status, transition);
        return result.IsValid ? result : null;
    }

    private static PractitionerState? ApplyTransition(
        PractitionerState? currentState,
        BranchLevelStatusTransitionResult? transition)
    {
        if (currentState is null || transition is null || !transition.Value.IsValid)
        {
            return currentState;
        }

        var branchLevels = currentState.BranchLevels.ToArray();
        var index = Array.FindIndex(
            branchLevels,
            status => status.Branch == transition.Value.CurrentStatus.Branch &&
                status.Level == transition.Value.CurrentStatus.Level);
        if (index < 0)
        {
            return currentState;
        }

        branchLevels[index] = transition.Value.NextStatus;
        return new PractitionerState(branchLevels);
    }

    private static bool TryCurrentStatus(
        PractitionerState state,
        BranchCode branch,
        GlobalLevelId level,
        out BranchLevelStatus status)
    {
        if (state.TryGetBranchLevelState(branch, level, out var branchLevelState))
        {
            status = new BranchLevelStatus(branch, level, branchLevelState);
            return true;
        }

        status = default;
        return false;
    }

    private static LocalProgressSummaryRefreshRequest DefaultSummaryRefresh(
        CompletedRuntimeSessionProcessingRequest request)
    {
        var date = request.PersistenceMetadata.Date;
        return new LocalProgressSummaryRefreshRequest(
            $"summary-{date.Year:D4}{date.Month:D2}{date.Day:D2}-{request.Result.SessionId}",
            date,
            date,
            date);
    }

    private static string AbandonedEvidenceSummary(RuntimeSessionCompletionResult result)
    {
        var reason = result.FailureRelevantFacts
            .Concat(result.RuntimeEvents.SelectMany(runtimeEvent => runtimeEvent.Facts))
            .FirstOrDefault(fact => fact.Name == "abandon_reason")
            ?.Value;

        return reason is null
            ? "Session abandoned before successful evidence was produced."
            : $"Session abandoned before successful evidence was produced: {reason}.";
    }

    private static EvidenceArtifactCategory CategoryForSession(SessionType sessionType)
    {
        return sessionType switch
        {
            SessionType.Load => EvidenceArtifactCategory.Load,
            SessionType.Test => EvidenceArtifactCategory.Test,
            SessionType.Stabilization => EvidenceArtifactCategory.Stabilization,
            SessionType.Transfer => EvidenceArtifactCategory.Transfer,
            _ => EvidenceArtifactCategory.Practice,
        };
    }

    private static LocalProgrammingEventKind EventKindForCategory(EvidenceArtifactCategory category)
    {
        return category switch
        {
            EvidenceArtifactCategory.Load => LocalProgrammingEventKind.Load,
            EvidenceArtifactCategory.Test => LocalProgrammingEventKind.FormalTest,
            EvidenceArtifactCategory.Stabilization => LocalProgrammingEventKind.Stabilization,
            EvidenceArtifactCategory.Transfer => LocalProgrammingEventKind.Transfer,
            EvidenceArtifactCategory.Maintenance => LocalProgrammingEventKind.Maintenance,
            EvidenceArtifactCategory.GlobalReview => LocalProgrammingEventKind.GlobalReview,
            _ => LocalProgrammingEventKind.Practice,
        };
    }

    private static LocalCompletedSessionType MapSessionType(SessionType sessionType)
    {
        return sessionType switch
        {
            SessionType.Practice => LocalCompletedSessionType.Practice,
            SessionType.Load => LocalCompletedSessionType.Load,
            SessionType.Test => LocalCompletedSessionType.Test,
            SessionType.Stabilization => LocalCompletedSessionType.Stabilization,
            SessionType.Regression => LocalCompletedSessionType.Regression,
            SessionType.Transfer => LocalCompletedSessionType.Transfer,
            SessionType.Recovery => LocalCompletedSessionType.Recovery,
            _ => throw new ArgumentOutOfRangeException(nameof(sessionType), sessionType, "Unknown runtime session type."),
        };
    }

    private sealed record CoreEvaluationResults(
        StandardEvaluationResult? StandardEvaluationResult,
        FormalGateDecision? FormalGateDecision,
        StabilizationOwnershipResult? StabilizationOwnershipResult,
        MaintenanceCurrencyResult? MaintenanceCurrencyResult,
        DecayRestorationResult? DecayResult,
        TransferEligibilityResult? TransferEligibilityResult,
        FailureResponse? FailureResponse,
        TestReadinessResult? TestReadinessResult)
    {
        public static CoreEvaluationResults Empty { get; } = new(null, null, null, null, null, null, null, null);
    }

    private sealed record PersistenceRecords(
        RuntimePersistenceHandoffRecords? RuntimeHandoff,
        LocalSessionHistoryRecord SessionHistory,
        IReadOnlyList<LocalEvidenceArtifactRecord> EvidenceArtifacts,
        LocalFormalTestAttemptRecord? FormalTestAttempt,
        LocalStabilizationPassRecord? StabilizationPass,
        LocalMaintenanceCheckRecord? MaintenanceCheck,
        LocalGeneratedDrillInstanceRecord? GeneratedDrillInstance);
}
