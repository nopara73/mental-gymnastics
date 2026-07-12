using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App;

public sealed class PreUiLiveSessionCommandRequest
{
    public PreUiLiveSessionCommandRequest(
        RuntimeInputCommandKind command,
        string? targetId = null,
        string? value = null)
    {
        if (!Enum.IsDefined(command))
        {
            throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown runtime command.");
        }

        Command = command;
        TargetId = Normalize(targetId);
        Value = Normalize(value);
    }

    public RuntimeInputCommandKind Command { get; }

    public string? TargetId { get; }

    public string? Value { get; }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record PreUiLiveSessionCommandOutcome(
    RuntimeInputCommandKind Command,
    bool IsAccepted,
    RuntimeInputCommandInvalidReason? InvalidReason,
    RuntimeCueResponseInvalidReason? CueInvalidReason,
    RuntimeCueResponseOutcome? CueOutcome,
    int EventCount,
    string Detail);

public sealed record PreUiLiveSessionCommandState(
    RuntimeInputCommandKind Command,
    bool IsAvailable,
    RuntimeInputCommandInvalidReason? InvalidReason,
    RuntimeCueResponseInvalidReason? CueInvalidReason,
    string Label);

public sealed record PreUiLiveSessionTimerState(
    TimeSpan Elapsed,
    TimeSpan? Remaining,
    double? Progress,
    bool IsTimed);

public sealed record PreUiLiveSessionCueState(
    string CueId,
    RuntimeCueKind Kind,
    string Cue,
    RuntimeCueResponseExpectation ResponseExpectation,
    TimeSpan ResponseWindow,
    string? ExpectedResponse,
    bool IsControlledDistractor = false);

public sealed record PreUiLiveSessionCorrectionState(
    long SourceEventSequenceNumber,
    string CueId,
    RuntimeCueKind Kind,
    string Cue,
    string SubmittedResponse,
    IReadOnlyList<string> ResponseOptions,
    TimeSpan Remaining);

public sealed record PreUiLiveSessionMaterialState(
    string Kind,
    string Name,
    string Value);

public sealed record PreUiLiveSessionEvidenceState(
    int RuntimeEventCount,
    int EvidenceFactCount,
    int DriftCount,
    int GuessCount,
    int ErrorCount,
    int CueCount,
    int CueResponseCount,
    int AnswerCount,
    int CorrectionCount,
    int ExpectedEvidenceFactCount,
    int ReturnCount = 0,
    int LateReturnCount = 0,
    int TargetChangeCount = 0,
    int OpenDriftCount = 0,
    TimeSpan? MaximumReturnTime = null);

public sealed record PreUiLiveSessionState(
    string SessionId,
    SessionType SessionType,
    BranchCode Branch,
    GlobalLevelId Level,
    DrillId Drill,
    RuntimeSessionLifecycleStatus LifecycleStatus,
    RuntimePhaseSchedulerStatus SchedulerStatus,
    string? CurrentPhaseId,
    RuntimeSessionPhaseKind? CurrentPhaseKind,
    RuntimeSessionPhaseCompletionRule? CurrentPhaseCompletionRule,
    PreUiLiveSessionTimerState Timer,
    PreUiLiveSessionCueState? ActiveCue,
    IReadOnlyList<PreUiLiveSessionMaterialState> CurrentMaterials,
    IReadOnlyList<PreUiLiveSessionCommandState> Commands,
    PreUiLiveSessionEvidenceState Evidence,
    PreUiLiveSessionCommandOutcome? LastCommand,
    string Detail,
    DrillId? SourceDrill = null,
    PreUiLiveSessionCorrectionState? PendingCorrection = null,
    string? CurrentFocusTarget = null)
{
    public bool IsTerminal => LifecycleStatus is
        RuntimeSessionLifecycleStatus.Completed or
        RuntimeSessionLifecycleStatus.Failed or
        RuntimeSessionLifecycleStatus.Abandoned;

    public bool GrantsAdvancementInApp => false;
}

public enum PreUiLiveSessionCompletionStatus
{
    Processed,
    CancelledBeforeWork,
    NotTerminal,
}

public sealed class PreUiLiveSessionCompletionRequest
{
    public PreUiLiveSessionCompletionRequest(
        TrainingDate completedOn,
        LocalSessionIntensity intensity = LocalSessionIntensity.Moderate,
        string? notes = null,
        bool recoveryMarked = false,
        bool deloadMarked = false,
        string? mainFailureModeAvoided = null)
    {
        if (!Enum.IsDefined(intensity))
        {
            throw new ArgumentOutOfRangeException(nameof(intensity), intensity, "Unknown session intensity.");
        }

        CompletedOn = completedOn;
        Intensity = intensity;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        RecoveryMarked = recoveryMarked;
        DeloadMarked = deloadMarked;
        MainFailureModeAvoided = string.IsNullOrWhiteSpace(mainFailureModeAvoided)
            ? null
            : mainFailureModeAvoided.Trim();
    }

    public TrainingDate CompletedOn { get; }

    public LocalSessionIntensity Intensity { get; }

    public string? Notes { get; }

    public bool RecoveryMarked { get; }

    public bool DeloadMarked { get; }

    public string? MainFailureModeAvoided { get; }
}

public sealed record PreUiLiveSessionCompletionResult(
    PreUiLiveSessionCompletionStatus Status,
    RuntimeSessionCompletionStatus? RuntimeCompletionStatus,
    PreUiTrainingWorkflowCompletionResult? WorkflowResult,
    PreUiLiveSessionState SessionState,
    string Detail)
{
    public bool IsProcessed => Status == PreUiLiveSessionCompletionStatus.Processed;

    public bool IsFinalized => Status is
        PreUiLiveSessionCompletionStatus.Processed or
        PreUiLiveSessionCompletionStatus.CancelledBeforeWork;

    public bool GrantsAdvancementInApp => false;
}

public sealed class PreUiLiveSessionController
{
    private static readonly RuntimeInputCommandKind[] RenderedCommands =
    [
        RuntimeInputCommandKind.MarkDrift,
        RuntimeInputCommandKind.MarkReturn,
        RuntimeInputCommandKind.MarkTargetChange,
        RuntimeInputCommandKind.RespondToCue,
        RuntimeInputCommandKind.SubmitAnswer,
        RuntimeInputCommandKind.MarkGuess,
        RuntimeInputCommandKind.MarkError,
        RuntimeInputCommandKind.Correct,
        RuntimeInputCommandKind.StartAudit,
        RuntimeInputCommandKind.FinishPhase,
        RuntimeInputCommandKind.Pause,
        RuntimeInputCommandKind.Resume,
        RuntimeInputCommandKind.Abandon,
    ];

    private readonly PreUiTrainingWorkflowService workflow;
    private readonly RuntimeInputCommandHandler commandHandler;
    private readonly RuntimeCueScheduler? cueScheduler;
    private readonly IReadOnlyList<GeneratedContentMaterial> inputMaterials;
    private readonly IReadOnlyList<GeneratedContentPayloadFact> expectedEvidenceFacts;
    private readonly AppTrainingSessionType appSessionType;
    private readonly bool saveActiveSnapshot;
    private bool transferContractPresented;

    public PreUiLiveSessionController(
        PreUiTrainingWorkflowService workflow,
        SelectedWorkRuntimeSessionPreparationResult runtimeSession,
        PreUiTrainingWorkflowStartResult startResult,
        bool saveActiveSnapshot = true)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(runtimeSession);
        ArgumentNullException.ThrowIfNull(startResult);

        if (!startResult.IsStarted || startResult.CommandHandler is null)
        {
            throw new ArgumentException(
                "A live session controller requires an app-started runtime session.",
                nameof(startResult));
        }

        this.workflow = workflow;
        commandHandler = startResult.CommandHandler;
        cueScheduler = startResult.CueScheduler;
        inputMaterials = runtimeSession.InputMaterials;
        expectedEvidenceFacts = runtimeSession.ExpectedEvidenceFacts;
        appSessionType = runtimeSession.SelectedWork.SessionType;
        this.saveActiveSnapshot = saveActiveSnapshot;
        SynchronizeCueSchedulerToRuntime();
    }

    public PreUiLiveSessionController(
        PreUiTrainingWorkflowService workflow,
        RuntimeInputCommandHandler commandHandler,
        RuntimeCueScheduler? cueScheduler,
        IEnumerable<GeneratedContentMaterial>? inputMaterials = null,
        IEnumerable<GeneratedContentPayloadFact>? expectedEvidenceFacts = null,
        AppTrainingSessionType? appSessionType = null,
        bool saveActiveSnapshot = true)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(commandHandler);

        var materialArray = (inputMaterials ?? RestoredInputMaterials(commandHandler.EventLog.SessionDefinition))
            .ToArray();
        var evidenceFactArray = (expectedEvidenceFacts ?? RestoredExpectedEvidenceFacts(commandHandler.EventLog.SessionDefinition))
            .ToArray();
        if (materialArray.Any(material => material is null))
        {
            throw new ArgumentException(
                "Restored live session render materials cannot contain null entries.",
                nameof(inputMaterials));
        }

        if (evidenceFactArray.Any(fact => fact is null))
        {
            throw new ArgumentException(
                "Restored live session expected evidence facts cannot contain null entries.",
                nameof(expectedEvidenceFacts));
        }

        this.workflow = workflow;
        this.commandHandler = commandHandler;
        this.cueScheduler = cueScheduler;
        this.inputMaterials = Array.AsReadOnly(materialArray);
        this.expectedEvidenceFacts = Array.AsReadOnly(evidenceFactArray);
        this.appSessionType = appSessionType ?? ToAppSessionType(commandHandler.EventLog.SessionDefinition.SessionType);
        this.saveActiveSnapshot = saveActiveSnapshot;
        SynchronizeCueSchedulerToRuntime();
    }

    public string SessionId => commandHandler.EventLog.SessionId;

    public bool GrantsAdvancementInApp => false;

    public PreUiLiveSessionState CaptureState(string? detail = null)
    {
        return BuildState(lastCommand: null, detail ?? "Runtime session state captured.");
    }

    public async ValueTask<PreUiActiveSessionResumeState> PersistActiveSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = commandHandler.CaptureSnapshot();
        var cueSnapshot = cueScheduler?.CaptureSnapshot();
        if (!saveActiveSnapshot)
        {
            return PreUiActiveSessionResumeState.NotPersistedFromRuntimeSnapshot(
                snapshot,
                cueSnapshot,
                "Active runtime session snapshot was captured but not persisted.");
        }

        return await workflow.SaveActiveSessionSnapshotAsync(
            new ActiveRuntimeSessionSnapshotSaveRequest(snapshot, cueSnapshot),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<PreUiLiveSessionState> SuspendForLifecycleAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = commandHandler.SuspendForLifecycle();
        SynchronizeCueSchedulerToRuntime();
        if (result.IsAccepted)
        {
            await SaveSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        return BuildState(lastCommand: null, result.IsAccepted
            ? "Active session suspended for app lifecycle."
            : "Active session could not be suspended for app lifecycle.");
    }

    public async ValueTask<PreUiLiveSessionState> ResumeFromLifecycleAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = commandHandler.ResumeFromLifecycleSuspension();
        SynchronizeCueSchedulerToRuntime();
        if (result.IsAccepted)
        {
            await SaveSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        return BuildState(lastCommand: null, result.IsAccepted
            ? "Active session resumed after app lifecycle suspension."
            : "Active session could not resume after app lifecycle suspension.");
    }

    public async ValueTask<PreUiLiveSessionState> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        SynchronizeCueSchedulerToRuntime();
        var timedAdvance = AdvanceRuntimeIfReady();
        SynchronizeCueSchedulerToRuntime();
        var cueAdvance = AdvanceCuesIfReady();
        if (timedAdvance.EventCount > 0 || cueAdvance.EventCount > 0)
        {
            await SaveSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        return BuildState(lastCommand: null, RefreshDetail(timedAdvance, cueAdvance));
    }

    public async ValueTask<PreUiLiveSessionState> HandleCommandAsync(
        PreUiLiveSessionCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var outcome = request.Command == RuntimeInputCommandKind.RespondToCue
            ? HandleCueResponse(request)
            : HandleRuntimeCommand(request);

        if (outcome.IsAccepted)
        {
            SynchronizeCueSchedulerToRuntime();
            AdvanceCuesIfReady();
            await SaveSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        return BuildState(outcome, outcome.Detail);
    }

    public async ValueTask<PreUiLiveSessionCompletionResult> CompleteAsync(
        PreUiLiveSessionCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = commandHandler.CaptureSnapshot();
        var completionStatus = CompletionStatusFor(snapshot.LifecycleState.Status);
        var state = BuildState(lastCommand: null, "Runtime session completion requested.");
        if (!completionStatus.HasValue)
        {
            return new PreUiLiveSessionCompletionResult(
                PreUiLiveSessionCompletionStatus.NotTerminal,
                RuntimeCompletionStatus: null,
                WorkflowResult: null,
                state,
                "Runtime session is not terminal; app workflow did not process a result.");
        }

        if (completionStatus.Value == RuntimeSessionCompletionStatus.Abandoned &&
            !HasTrainingWorkStarted(snapshot))
        {
            await workflow.CancelPreparedSessionAsync(
                new PreUiPreparedSessionCancellationRequest(
                    snapshot.SessionId,
                    snapshot.GeneratedDrillInstance?.InstanceId,
                    request.Notes ?? "User left session setup before work started."),
                cancellationToken).ConfigureAwait(false);

            return new PreUiLiveSessionCompletionResult(
                PreUiLiveSessionCompletionStatus.CancelledBeforeWork,
                RuntimeSessionCompletionStatus.Abandoned,
                WorkflowResult: null,
                state,
                "Session setup was cancelled before training work started; no attempt was recorded.");
        }

        var runtimeResult = BuildCompletionResult(snapshot, completionStatus.Value, request);
        var executable = ExecutableStandardCatalog.Get(
            snapshot.SessionDefinition.Branch,
            snapshot.SessionDefinition.Level);
        var evaluatedStandard = executable.Drill == snapshot.SessionDefinition.Drill
            ? StandardForSession(executable, snapshot)
            : null;
        var standardEvaluation = evaluatedStandard is null
            ? null
            : RuntimeStandardEvaluationHandoffMapper.Map(
                runtimeResult,
                inputMaterials.Select(material => new RuntimeScoringMaterial(
                    material.Kind.ToString(),
                    material.Name,
                    material.Value)));
        var standardResult = EvaluateStandard(evaluatedStandard, standardEvaluation);
        EnsurePassingFormalWorkNamesFailureMode(request, standardResult);
        var formalGate = FormalGateFor(request, standardResult);
        var readinessPractice = ReadinessPracticeFor(snapshot, standardResult);
        var stabilization = StabilizationFor(request, snapshot, standardResult);
        var maintenance = MaintenanceFor(request.CompletedOn, snapshot, standardResult);
        var workflowResult = await workflow.CompleteSessionAsync(
            new PreUiTrainingWorkflowCompletionRequest(
                new CompletedRuntimeSessionProcessingRequest(
                    runtimeResult,
                    new RuntimePersistenceHandoffMetadata(
                        request.CompletedOn,
                        request.Intensity,
                        cleanPerformance: false,
                        request.Notes ?? DefaultCompletionNotes(snapshot, completionStatus.Value),
                        transferTask: appSessionType == AppTrainingSessionType.Transfer
                            ? TransferTestCatalog.TransferTests.Single(test =>
                                test.SourceBranch == snapshot.SessionDefinition.Branch).TransferTask
                            : null,
                        recoveryMarked: request.RecoveryMarked ||
                            appSessionType == AppTrainingSessionType.Recovery,
                        deloadMarked: request.DeloadMarked),
                    evaluatedStandard,
                    standardEvaluation,
                    formalGate,
                    readinessPractice,
                    stabilization,
                    maintenance,
                    transfer: TransferFor(snapshot, standardResult),
                    failureResponse: FailureResponseFor(snapshot, standardResult)),
                request.CompletedOn),
            cancellationToken).ConfigureAwait(false);

        return new PreUiLiveSessionCompletionResult(
            PreUiLiveSessionCompletionStatus.Processed,
            completionStatus.Value,
            workflowResult,
            state,
            "App workflow processed the terminal runtime session.");
    }

    private PreUiLiveSessionCommandOutcome AdvanceRuntimeIfReady()
    {
        if (commandHandler.LifecycleState.Status != RuntimeSessionLifecycleStatus.Running)
        {
            return new PreUiLiveSessionCommandOutcome(
                RuntimeInputCommandKind.FinishPhase,
                IsAccepted: true,
                InvalidReason: null,
                CueInvalidReason: null,
                CueOutcome: null,
                EventCount: 0,
                "Runtime timed phases were not advanced because the session is not running.");
        }

        var result = commandHandler.AdvanceToCurrentTime();
        return new PreUiLiveSessionCommandOutcome(
            RuntimeInputCommandKind.FinishPhase,
            result.IsAccepted,
            result.InvalidReason,
            CueInvalidReason: null,
            CueOutcome: null,
            result.Events.Count,
            result.IsAccepted
                ? (result.Events.Count == 0
                    ? "No runtime timed phase was due."
                    : "Runtime advanced due timed phase events.")
                : "Runtime rejected timed phase advancement.");
    }

    private PreUiLiveSessionCommandOutcome AdvanceCuesIfReady()
    {
        if (cueScheduler is null ||
            commandHandler.LifecycleState.Status != RuntimeSessionLifecycleStatus.Running ||
            !CueSchedulingIsActive())
        {
            return new PreUiLiveSessionCommandOutcome(
                RuntimeInputCommandKind.RespondToCue,
                IsAccepted: true,
                InvalidReason: null,
                CueInvalidReason: null,
                CueOutcome: null,
                EventCount: 0,
                "No runtime cues were due.");
        }

        var advance = cueScheduler.AdvanceToCurrentTime(commandHandler.CurrentPhase);
        foreach (var runtimeEvent in advance.Events)
        {
            commandHandler.ObserveExternallyRecordedEvent(runtimeEvent);
        }

        return new PreUiLiveSessionCommandOutcome(
            RuntimeInputCommandKind.RespondToCue,
            IsAccepted: true,
            InvalidReason: null,
            CueInvalidReason: null,
            CueOutcome: null,
            EventCount: advance.Events.Count,
            advance.Events.Count == 0
                ? "No runtime cues were due."
                : "Runtime emitted due cue events.");
    }

    private void SynchronizeCueSchedulerToRuntime()
    {
        if (cueScheduler is null)
        {
            return;
        }

        if (commandHandler.LifecycleState.Status == RuntimeSessionLifecycleStatus.Running &&
            CueSchedulingIsActive())
        {
            cueScheduler.Resume();
            return;
        }

        cueScheduler.Pause();
    }

    private bool CueSchedulingIsActive()
    {
        return commandHandler.CurrentPhase?.Kind == RuntimeSessionPhaseKind.CueResponse ||
            commandHandler.EventLog.SessionDefinition.SessionType == SessionType.Stabilization &&
            commandHandler.CurrentPhase?.Kind is
                RuntimeSessionPhaseKind.EncodeWindow or
                RuntimeSessionPhaseKind.ActiveWork or
                RuntimeSessionPhaseKind.ReconstructionInput or
                RuntimeSessionPhaseKind.Audit or
                RuntimeSessionPhaseKind.Recovery ||
            (commandHandler.EventLog.SessionDefinition.Drill == DrillId.FH2DistractorHold ||
                commandHandler.EventLog.SessionDefinition.SourceDrill == DrillId.FH2DistractorHold) &&
            commandHandler.CurrentPhase?.Kind == RuntimeSessionPhaseKind.ActiveWork;
    }

    private PreUiLiveSessionCommandOutcome HandleCueResponse(
        PreUiLiveSessionCommandRequest request)
    {
        var availability = commandHandler.AvailabilityFor(RuntimeInputCommandKind.RespondToCue);
        if (!availability.IsAvailable)
        {
            return FromAvailability(availability, "Runtime rejected cue response command availability.");
        }

        if (cueScheduler is null)
        {
            return new PreUiLiveSessionCommandOutcome(
                RuntimeInputCommandKind.RespondToCue,
                IsAccepted: false,
                InvalidReason: null,
                CueInvalidReason: RuntimeCueResponseInvalidReason.UnknownCue,
                CueOutcome: null,
                EventCount: 0,
                "No runtime cue scheduler is active for this session.");
        }

        var activeCue = ActiveCueState(cueScheduler, cueScheduler.CaptureSnapshot());
        var cueId = request.TargetId ?? activeCue?.CueId;
        if (cueId is null)
        {
            return new PreUiLiveSessionCommandOutcome(
                RuntimeInputCommandKind.RespondToCue,
                IsAccepted: false,
                InvalidReason: null,
                CueInvalidReason: RuntimeCueResponseInvalidReason.CueNotPresented,
                CueOutcome: null,
                EventCount: 0,
                "Runtime has not presented a cue to answer.");
        }

        var scheduledCue = cueScheduler.Schedule.Cues.FirstOrDefault(cue =>
            string.Equals(cue.Id, cueId, StringComparison.Ordinal));
        var response = request.Value ?? scheduledCue?.ExpectedResponse ?? "respond";
        var result = cueScheduler.RecordResponse(RuntimeCueResponse.ForCue(cueId, response));
        if (result.Event is not null)
        {
            commandHandler.ObserveExternallyRecordedEvent(result.Event);
        }

        return new PreUiLiveSessionCommandOutcome(
            RuntimeInputCommandKind.RespondToCue,
            result.IsAccepted,
            InvalidReason: null,
            result.InvalidReason,
            result.Outcome,
            result.Event is null ? 0 : 1,
            result.IsAccepted
                ? "Runtime recorded the cue response."
                : "Runtime rejected the cue response.");
    }

    private PreUiLiveSessionCommandOutcome HandleRuntimeCommand(
        PreUiLiveSessionCommandRequest request)
    {
        var result = commandHandler.Handle(ToRuntimeCommand(request));
        return new PreUiLiveSessionCommandOutcome(
            request.Command,
            result.IsAccepted,
            result.InvalidReason,
            CueInvalidReason: null,
            CueOutcome: null,
            result.Events.Count,
            result.IsAccepted
                ? "Runtime accepted the command."
                : "Runtime rejected the command.");
    }

    private RuntimeInputCommand ToRuntimeCommand(PreUiLiveSessionCommandRequest request)
    {
        return request.Command switch
        {
            RuntimeInputCommandKind.MarkDrift =>
                RuntimeInputCommand.MarkDrift(request.TargetId ?? GeneratedCommandId("drift")),
            RuntimeInputCommandKind.MarkReturn =>
                RuntimeInputCommand.MarkReturn(RuntimeDuration.FromSeconds(
                    FocusHoldLevelOneStandard.ReturnWindowSeconds)),
            RuntimeInputCommandKind.MarkTargetChange =>
                RuntimeInputCommand.MarkTargetChange(request.Value ?? "different target"),
            RuntimeInputCommandKind.SubmitAnswer =>
                RuntimeInputCommand.SubmitAnswer(
                    request.TargetId ?? commandHandler.CurrentPhase?.Id ?? GeneratedCommandId("answer"),
                    request.Value ?? RuntimeResponseMarkers.Omitted),
            RuntimeInputCommandKind.MarkGuess =>
                RuntimeInputCommand.MarkGuess(request.TargetId ?? GeneratedCommandId("guess")),
            RuntimeInputCommandKind.MarkError =>
                RuntimeInputCommand.MarkError(
                    request.TargetId ?? GeneratedCommandId("error"),
                    request.Value ?? "incorrect_response"),
            RuntimeInputCommandKind.Correct =>
                RuntimeInputCommand.Correct(
                    request.TargetId ?? GeneratedCommandId("correction"),
                    request.Value ?? "corrected"),
            RuntimeInputCommandKind.StartAudit =>
                RuntimeInputCommand.StartAudit(request.TargetId ?? GeneratedCommandId("audit")),
            RuntimeInputCommandKind.FinishPhase => RuntimeInputCommand.FinishPhase(),
            RuntimeInputCommandKind.Pause => RuntimeInputCommand.Pause(),
            RuntimeInputCommandKind.Resume => RuntimeInputCommand.Resume(),
            RuntimeInputCommandKind.Abandon =>
                RuntimeInputCommand.Abandon(request.Value ?? "user abandoned live session"),
            RuntimeInputCommandKind.RespondToCue =>
                RuntimeInputCommand.RespondToCue(request.TargetId ?? GeneratedCommandId("cue"), request.Value ?? "response"),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Command, "Unknown runtime command."),
        };
    }

    private RuntimeSessionCompletionResult BuildCompletionResult(
        RuntimeSessionSnapshot snapshot,
        RuntimeSessionCompletionStatus completionStatus,
        PreUiLiveSessionCompletionRequest request)
    {
        var startedAt = snapshot.RuntimeEvents.FirstOrDefault(
                runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.SessionStarted)
            ?.OccurredAt ?? RuntimeInstant.Zero;
        var completedAt = snapshot.RuntimeEvents.LastOrDefault(IsTerminalRuntimeEvent)
            ?.OccurredAt ?? snapshot.CapturedAt;
        var captureKind = EvidenceCaptureKind(completionStatus, snapshot);

        var result = RuntimeSessionCoordinator.Complete(new RuntimeSessionCoordinatorRequest(
            snapshot.SessionId,
            snapshot.SessionDefinition,
            startedAt,
            completedAt,
            completionStatus,
            captureKind,
            snapshot.PhaseScheduler.CompletedPhases,
            CoordinatorEvents(snapshot),
            persistenceInputs: null,
            completionFacts: CompletionFacts(snapshot, completionStatus, request),
            evidenceDate: request.CompletedOn));

        return result.CompletionResult;
    }

    private RuntimeEvidenceCaptureKind EvidenceCaptureKind(
        RuntimeSessionCompletionStatus completionStatus,
        RuntimeSessionSnapshot snapshot)
    {
        if (completionStatus != RuntimeSessionCompletionStatus.Completed)
        {
            return RuntimeEvidenceCaptureKind.FailedSet;
        }

        return appSessionType switch
        {
            AppTrainingSessionType.Test => RuntimeEvidenceCaptureKind.FormalAttempt,
            AppTrainingSessionType.Stabilization => RuntimeEvidenceCaptureKind.Stabilization,
            AppTrainingSessionType.Transfer => RuntimeEvidenceCaptureKind.Transfer,
            AppTrainingSessionType.Maintenance => RuntimeEvidenceCaptureKind.Maintenance,
            _ => HasFailureMarks(snapshot)
                ? RuntimeEvidenceCaptureKind.FailedSet
                : RuntimeEvidenceCaptureKind.BestSet,
        };
    }

    private RuntimeFormalGateHandoffInput? FormalGateFor(
        PreUiLiveSessionCompletionRequest request,
        StandardEvaluationResult? standardResult)
    {
        if (!IsFormalGateSession() ||
            standardResult is null)
        {
            return null;
        }

        return new RuntimeFormalGateHandoffInput(
            request.CompletedOn,
            new TestResultEvidence(
                TestResultEvidenceKind.Score,
                standardResult.Passed ? "The stated standard passed." : "The stated standard was not met."),
            standardResult.Passed ? FormalTestPassState.PassOnce : FormalTestPassState.Fail,
            standardResult.Passed ? null : FailureType.TechnicalFailure,
            task: appSessionType == AppTrainingSessionType.Transfer
                ? TestTask.ForTransfer(TransferTestCatalog.TransferTests.Single(test =>
                    test.SourceBranch == commandHandler.EventLog.SessionDefinition.Branch).TransferTask)
                : null,
            mainFailureModeAvoided: request.MainFailureModeAvoided);
    }

    private RuntimeReadinessPracticeHandoffInput? ReadinessPracticeFor(
        RuntimeSessionSnapshot snapshot,
        StandardEvaluationResult? standardResult)
    {
        if (appSessionType is not AppTrainingSessionType.Practice and not AppTrainingSessionType.Load ||
            standardResult is null)
        {
            return null;
        }

        var profile = TrainingLoadProfileCatalog.Get(
            snapshot.SessionDefinition.Branch,
            snapshot.SessionDefinition.Level);
        var fullLoad = TrainingLoadStageStandardResolver.IsFormalStandardLoad(
            profile,
            snapshot.SessionDefinition.LoadVariables);
        return new RuntimeReadinessPracticeHandoffInput(
            snapshot.SessionDefinition.Standard.Demand,
            standardResult.Passed && fullLoad);
    }

    private EvaluatedStandard StandardForSession(
        ExecutableTrainingStandardDefinition executable,
        RuntimeSessionSnapshot snapshot)
    {
        if (appSessionType is AppTrainingSessionType.Test or
            AppTrainingSessionType.Stabilization or
            AppTrainingSessionType.Transfer)
        {
            return executable.EvaluatedStandard;
        }

        return TrainingLoadStageStandardResolver.Resolve(
            TrainingLoadProfileCatalog.Get(
                snapshot.SessionDefinition.Branch,
                snapshot.SessionDefinition.Level),
            snapshot.SessionDefinition.LoadVariables,
            executable.EvaluatedStandard);
    }

    private RuntimeStabilizationCoreHandoffInput? StabilizationFor(
        PreUiLiveSessionCompletionRequest request,
        RuntimeSessionSnapshot snapshot,
        StandardEvaluationResult? standardResult)
    {
        if (appSessionType != AppTrainingSessionType.Stabilization || standardResult is null)
        {
            return null;
        }

        return new RuntimeStabilizationCoreHandoffInput(
            request.CompletedOn,
            standardResult,
            standardResult.Passed ? FormalTestPassState.StabilizationPass : FormalTestPassState.Fail,
            afterAdjacentWorkOrControlledDistractor: ControlledDemandWasPresented(snapshot),
            request.MainFailureModeAvoided ?? string.Empty);
    }

    private bool ControlledDemandWasPresented(RuntimeSessionSnapshot snapshot)
    {
        return snapshot.RuntimeEvents.Any(runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.CueEmitted &&
            runtimeEvent.Facts.Any(fact =>
                fact.Name == "cue_id" &&
                (string.Equals(
                    fact.Value,
                    StabilizationGeneratedContent.ControlledDistractorId,
                    StringComparison.Ordinal) ||
                 inputMaterials.Any(material =>
                     (material.Kind is GeneratedContentMaterialKind.DistractorPrompt or
                         GeneratedContentMaterialKind.DisruptionEvent) &&
                     string.Equals(material.Name, fact.Value, StringComparison.Ordinal)))));
    }

    private RuntimeMaintenanceCoreHandoffInput? MaintenanceFor(
        TrainingDate date,
        RuntimeSessionSnapshot snapshot,
        StandardEvaluationResult? standardResult)
    {
        if (appSessionType != AppTrainingSessionType.Maintenance || standardResult is null)
        {
            return null;
        }

        return new RuntimeMaintenanceCoreHandoffInput(
            date,
            snapshot.SessionDefinition.Level,
            MaintenanceCheckKind.StandardOrTransfer,
            standardResult);
    }

    private RuntimeTransferEligibilityHandoffInput? TransferFor(
        RuntimeSessionSnapshot snapshot,
        StandardEvaluationResult? standardResult)
    {
        if (appSessionType != AppTrainingSessionType.Transfer || standardResult is null)
        {
            return null;
        }

        var transfer = TransferTestCatalog.TransferTests.Single(test =>
            test.SourceBranch == snapshot.SessionDefinition.Branch);
        var sourceStandard = snapshot.SessionDefinition.Standard;
        var capacity = ProgramCatalog.Drills
            .Single(drill => drill.Id == snapshot.SessionDefinition.Drill)
            .CapacityTrained
            .FirstOrDefault();
        return new RuntimeTransferEligibilityHandoffInput(
            snapshot.SessionDefinition.Level,
            transfer.TransferTask,
            capacity,
            transfer.SameDemand,
            transfer.ChangedContext,
            new TransferSourceStandardEvidence(
                snapshot.SessionDefinition.Branch,
                snapshot.SessionDefinition.Level,
                sourceStandard.Standard,
                visibleInTransferArtifact: TransferContractWasPresented(snapshot)),
            transfer.RetestRequirement);
    }

    private bool TransferContractWasPresented(RuntimeSessionSnapshot snapshot)
    {
        var hasContract = inputMaterials.Any(material =>
                material.Kind == GeneratedContentMaterialKind.TransferTask) &&
            inputMaterials.Any(material =>
                material.Kind == GeneratedContentMaterialKind.ChangedContext) &&
            inputMaterials.Any(material =>
                material.Kind == GeneratedContentMaterialKind.SourceBranchStandard);
        return hasContract &&
            (transferContractPresented || snapshot.PhaseScheduler.CompletedPhases.Any(phase =>
                phase.Definition.Kind == RuntimeSessionPhaseKind.InstructionPrep));
    }

    private RuntimeFailureResponseHandoffInput? FailureResponseFor(
        RuntimeSessionSnapshot snapshot,
        StandardEvaluationResult? standardResult)
    {
        if (standardResult is null || standardResult.Passed)
        {
            return null;
        }

        var failureType = standardResult.Failures.Any(failure =>
            failure.Kind == StandardFailureKind.CriticalConstraintBroken)
                ? FailureType.EffortFailure
                : standardResult.Failures.Any(failure =>
                    failure.Kind == StandardFailureKind.NumericalThresholdMissed)
                    ? FailureType.Overload
                    : FailureType.TechnicalFailure;
        var signals = failureType switch
        {
            FailureType.EffortFailure => new[] { FailureEvidenceSignal.BrokenHonestyConstraint },
            FailureType.Overload => new[]
            {
                FailureEvidenceSignal.ErrorsRiseAfterLoadIncrease,
                FailureEvidenceSignal.ConstraintPreserved,
            },
            _ => new[] { FailureEvidenceSignal.WrongProcedure },
        };
        _ = snapshot;
        return new RuntimeFailureResponseHandoffInput(
            failureType,
            signals,
            isFirstFailureOfType: true,
            repeatedOverloadInSameBranch: false);
    }

    private void EnsurePassingFormalWorkNamesFailureMode(
        PreUiLiveSessionCompletionRequest request,
        StandardEvaluationResult? standardResult)
    {
        if (standardResult?.Passed != true ||
            !IsFormalGateSession() && appSessionType != AppTrainingSessionType.Stabilization ||
            !string.IsNullOrWhiteSpace(request.MainFailureModeAvoided))
        {
            return;
        }

        throw new InvalidOperationException(
            "A passing formal or stabilization session must name the documented failure mode the practitioner avoided.");
    }

    private bool IsFormalGateSession()
    {
        return appSessionType == AppTrainingSessionType.Test ||
            appSessionType == AppTrainingSessionType.Transfer &&
            commandHandler.EventLog.SessionDefinition.Level == GlobalLevelId.L4;
    }

    private static StandardEvaluationResult? EvaluateStandard(
        EvaluatedStandard? standard,
        RuntimeStandardEvaluationHandoffInput? input)
    {
        if (standard is null || input is null)
        {
            return null;
        }

        return StandardEvaluator.Evaluate(
            standard,
            new StandardEvaluationAttempt(
                input.Measurements,
                input.CriticalConstraintChecks,
                input.OutputComplete,
                input.RubricOutcome));
    }

    private static AppTrainingSessionType ToAppSessionType(SessionType sessionType)
    {
        return sessionType switch
        {
            SessionType.Practice => AppTrainingSessionType.Practice,
            SessionType.Load => AppTrainingSessionType.Load,
            SessionType.Test => AppTrainingSessionType.Test,
            SessionType.Stabilization => AppTrainingSessionType.Stabilization,
            SessionType.Regression => AppTrainingSessionType.Regression,
            SessionType.Transfer => AppTrainingSessionType.Transfer,
            SessionType.Recovery => AppTrainingSessionType.Recovery,
            _ => throw new ArgumentOutOfRangeException(nameof(sessionType), sessionType, "Unknown runtime session type."),
        };
    }

    private static IEnumerable<RuntimeSessionCoordinatorEventInput> CoordinatorEvents(
        RuntimeSessionSnapshot snapshot)
    {
        return snapshot.RuntimeEvents
            .Where(runtimeEvent => runtimeEvent.Kind != RuntimeEventKind.SessionStarted &&
                !IsTerminalRuntimeEvent(runtimeEvent))
            .Select(runtimeEvent => new RuntimeSessionCoordinatorEventInput(
                runtimeEvent.Kind,
                runtimeEvent.OccurredAt,
                runtimeEvent.PhaseId,
                runtimeEvent.PhaseKind,
                runtimeEvent.Facts));
    }

    private IReadOnlyList<RuntimeEventFact> CompletionFacts(
        RuntimeSessionSnapshot snapshot,
        RuntimeSessionCompletionStatus completionStatus,
        PreUiLiveSessionCompletionRequest request)
    {
        if (completionStatus == RuntimeSessionCompletionStatus.Abandoned)
        {
            var terminalFacts = snapshot.RuntimeEvents.LastOrDefault(IsTerminalRuntimeEvent)
                ?.Facts
                .ToArray() ?? [];
            return terminalFacts.Length == 0
                ? [new RuntimeEventFact("abandon_reason", request.Notes ?? "user abandoned live session")]
                : terminalFacts;
        }

        var facts = new List<RuntimeEventFact>
        {
            new("output_sample", CompletionOutputSample(snapshot)),
            new("score", CompletionScore(snapshot)),
            new("evidence_source", "android-live-runtime-event-log"),
        };

        if (completionStatus != RuntimeSessionCompletionStatus.Completed || HasFailureMarks(snapshot))
        {
            var errorCount = CountEvents(snapshot, RuntimeEventKind.ErrorRecorded) +
                CountEvents(snapshot, RuntimeEventKind.GuessMarked) +
                CueFailureCount(snapshot);
            facts.Add(new RuntimeEventFact("failed_item_list", FailureSummary(snapshot)));
            facts.Add(new RuntimeEventFact("error_count", errorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return facts.AsReadOnly();
    }

    private static RuntimeSessionCompletionStatus? CompletionStatusFor(
        RuntimeSessionLifecycleStatus status)
    {
        return status switch
        {
            RuntimeSessionLifecycleStatus.Completed => RuntimeSessionCompletionStatus.Completed,
            RuntimeSessionLifecycleStatus.Failed => RuntimeSessionCompletionStatus.Failed,
            RuntimeSessionLifecycleStatus.Abandoned => RuntimeSessionCompletionStatus.Abandoned,
            _ => null,
        };
    }

    private bool HasFailureMarks(RuntimeSessionSnapshot snapshot)
    {
        return CountEvents(snapshot, RuntimeEventKind.ErrorRecorded) > 0 ||
            CountEvents(snapshot, RuntimeEventKind.GuessMarked) > 0 ||
            CueFailureCount(snapshot) > 0;
    }

    private static string CompletionOutputSample(RuntimeSessionSnapshot snapshot)
    {
        return $"{snapshot.SessionDefinition.Branch} {snapshot.SessionDefinition.Level} " +
            $"{snapshot.SessionDefinition.Drill} live session completed from runtime event log.";
    }

    private string CompletionScore(RuntimeSessionSnapshot snapshot)
    {
        return string.Join(
            "; ",
            [
                $"drift_count={CountEvents(snapshot, RuntimeEventKind.DriftMarked)}",
                $"return_count={CountEvents(snapshot, RuntimeEventKind.RecoveryCompleted)}",
                $"late_return_count={CountFactValue(snapshot, "return_timing_outcome", "late")}",
                $"target_substitution_count={CountFactValue(snapshot, "error_kind", "target_substitution")}",
                $"guess_count={CountEvents(snapshot, RuntimeEventKind.GuessMarked)}",
                $"error_count={CountEvents(snapshot, RuntimeEventKind.ErrorRecorded)}",
                $"cue_error_count={CueFailureCount(snapshot)}",
                $"answer_count={CountEvents(snapshot, RuntimeEventKind.AnswerSubmitted)}",
                $"cue_response_count={CountEvents(snapshot, RuntimeEventKind.CueResponseSubmitted)}",
                $"completed_phase_count={snapshot.PhaseScheduler.CompletedPhases.Count}",
            ]);
    }

    private string FailureSummary(RuntimeSessionSnapshot snapshot)
    {
        return string.Join(
            "; ",
            [
                $"guess_count={CountEvents(snapshot, RuntimeEventKind.GuessMarked)}",
                $"error_count={CountEvents(snapshot, RuntimeEventKind.ErrorRecorded)}",
                $"cue_error_count={CueFailureCount(snapshot)}",
                $"correction_count={CountEvents(snapshot, RuntimeEventKind.CorrectionSubmitted)}",
            ]);
    }

    private static string DefaultCompletionNotes(
        RuntimeSessionSnapshot snapshot,
        RuntimeSessionCompletionStatus completionStatus)
    {
        return $"{snapshot.SessionDefinition.Branch} {snapshot.SessionDefinition.Level} " +
            $"{snapshot.SessionDefinition.Drill} live session {completionStatus}; processed by app workflow from runtime events. " +
            "Android UI granted no progress.";
    }

    private static bool IsTerminalRuntimeEvent(RuntimeEvent runtimeEvent)
    {
        return runtimeEvent.Kind is RuntimeEventKind.SessionCompleted or RuntimeEventKind.SessionAbandoned;
    }

    private static bool HasTrainingWorkStarted(RuntimeSessionSnapshot snapshot)
    {
        return snapshot.RuntimeEvents.Any(runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.PhaseStarted &&
            runtimeEvent.PhaseKind is not null and not RuntimeSessionPhaseKind.InstructionPrep);
    }

    private static string RefreshDetail(
        PreUiLiveSessionCommandOutcome timedAdvance,
        PreUiLiveSessionCommandOutcome cueAdvance)
    {
        if (timedAdvance.EventCount > 0 && cueAdvance.EventCount > 0)
        {
            return "Runtime advanced due timed phase and cue events.";
        }

        if (timedAdvance.EventCount > 0)
        {
            return timedAdvance.Detail;
        }

        return cueAdvance.Detail;
    }

    private PreUiLiveSessionState BuildState(
        PreUiLiveSessionCommandOutcome? lastCommand,
        string detail)
    {
        var snapshot = commandHandler.CaptureSnapshot();
        var cueSnapshot = cueScheduler?.CaptureSnapshot();
        var activePhase = ActivePhase(snapshot);
        var timer = TimerState(snapshot, activePhase);
        var activeCue = cueScheduler is null || cueSnapshot is null
            ? null
            : ActiveCueState(cueScheduler, cueSnapshot);

        var currentMaterials = CurrentMaterials(
            snapshot.SessionDefinition.Drill,
            snapshot.SessionDefinition.SourceDrill,
            activePhase).ToArray();
        if (appSessionType == AppTrainingSessionType.Transfer &&
            TransferContractIsVisible(activePhase) &&
            currentMaterials.Any(material => material.Kind == nameof(GeneratedContentMaterialKind.TransferTask)) &&
            currentMaterials.Any(material => material.Kind == nameof(GeneratedContentMaterialKind.ChangedContext)) &&
            currentMaterials.Any(material => material.Kind == nameof(GeneratedContentMaterialKind.SourceBranchStandard)))
        {
            transferContractPresented = true;
        }

        var pendingCorrection = PendingCorrectionState(snapshot);
        return new PreUiLiveSessionState(
            snapshot.SessionId,
            snapshot.SessionDefinition.SessionType,
            snapshot.SessionDefinition.Branch,
            snapshot.SessionDefinition.Level,
            snapshot.SessionDefinition.Drill,
            snapshot.LifecycleState.Status,
            snapshot.PhaseScheduler.Status,
            snapshot.PhaseScheduler.CurrentPhaseId,
            activePhase?.Kind,
            activePhase?.CompletionRule,
            timer,
            activeCue,
            currentMaterials,
            CommandStates(activeCue, snapshot, cueSnapshot).ToArray(),
            EvidenceState(snapshot),
            lastCommand,
            detail,
            snapshot.SessionDefinition.SourceDrill,
            pendingCorrection,
            pendingCorrection is null ? CurrentFocusTarget(snapshot) : null);
    }

    private string? CurrentFocusTarget(RuntimeSessionSnapshot snapshot)
    {
        var effectiveDrill = snapshot.SessionDefinition.SourceDrill ?? snapshot.SessionDefinition.Drill;
        if (effectiveDrill is not (DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter) ||
            cueScheduler is null ||
            snapshot.PhaseScheduler.CurrentPhaseId is null)
        {
            return null;
        }

        var targets = inputMaterials
            .Where(material => material.Kind == GeneratedContentMaterialKind.TargetSet)
            .Select(material => material.Value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var current = targets.FirstOrDefault();
        foreach (var cue in cueScheduler.Schedule.Cues)
        {
            if (cue.ResponseExpectation != RuntimeCueResponseExpectation.ResponseRequired ||
                cue.ExpectedResponse is null ||
                !targets.Contains(cue.ExpectedResponse, StringComparer.Ordinal))
            {
                continue;
            }

            var resolved = snapshot.RuntimeEvents.Any(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.CueResponseSubmitted &&
                string.Equals(EventFact(runtimeEvent, "cue_id"), cue.Id, StringComparison.Ordinal));
            if (resolved)
            {
                current = cue.ExpectedResponse;
            }
        }

        return current;
    }

    private PreUiLiveSessionCorrectionState? PendingCorrectionState(RuntimeSessionSnapshot snapshot)
    {
        if (!snapshot.LastCorrectableEventSequenceNumber.HasValue ||
            cueScheduler is null ||
            !commandHandler.AvailabilityFor(RuntimeInputCommandKind.Correct).IsAvailable)
        {
            return null;
        }

        var failedEvent = snapshot.RuntimeEvents.FirstOrDefault(runtimeEvent =>
            runtimeEvent.SequenceNumber == snapshot.LastCorrectableEventSequenceNumber.Value);
        var cueId = failedEvent is null ? null : EventFact(failedEvent, "cue_id");
        var cue = cueId is null
            ? null
            : cueScheduler.Schedule.Cues.FirstOrDefault(item =>
                string.Equals(item.Id, cueId, StringComparison.Ordinal));
        if (failedEvent is null || cue is null)
        {
            return null;
        }

        var elapsed = snapshot.CapturedAt.ElapsedSince(failedEvent.OccurredAt);
        var remaining = snapshot.InputOptions.CorrectionWindow.Value - elapsed.Value;
        var options = CorrectionOptions(snapshot.SessionDefinition, cue);
        return new PreUiLiveSessionCorrectionState(
            failedEvent.SequenceNumber,
            cue.Id,
            cue.Kind,
            cue.Cue,
            EventFact(failedEvent, "response") ?? "omitted",
            options,
            remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
    }

    private IReadOnlyList<string> CorrectionOptions(
        RuntimeSessionDefinition session,
        RuntimeScheduledCue cue)
    {
        if (cue.Kind == RuntimeCueKind.Interruption &&
            string.Equals(cue.ExpectedResponse, "resume", StringComparison.OrdinalIgnoreCase))
        {
            return ["resume"];
        }

        var effectiveDrill = session.SourceDrill ?? session.Drill;
        if (effectiveDrill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter)
        {
            return inputMaterials
                .Where(material => material.Kind == GeneratedContentMaterialKind.TargetSet)
                .Select(material => material.Value.Trim())
                .Where(value => value.Length > 0)
                .Append("withhold")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (effectiveDrill is DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule)
        {
            return ["tap", "withhold"];
        }

        return ["respond"];
    }

    private IEnumerable<PreUiLiveSessionCommandState> CommandStates(
        PreUiLiveSessionCueState? activeCue,
        RuntimeSessionSnapshot snapshot,
        RuntimeCueSchedulerSnapshot? cueSnapshot)
    {
        var drill = snapshot.SessionDefinition.Drill;
        var phase = ActivePhase(snapshot);
        var phaseAnswerCount = phase is null
            ? 0
            : snapshot.RuntimeEvents.Count(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted &&
                string.Equals(runtimeEvent.PhaseId, phase.Id, StringComparison.Ordinal));
        var phaseHasAnswer = phase is not null &&
            phaseAnswerCount >= RequiredAnswerCount(drill, phase.Kind);
        var phaseHasAuditStart = phase is not null && snapshot.RuntimeEvents.Any(runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.AuditStarted &&
            string.Equals(runtimeEvent.PhaseId, phase.Id, StringComparison.Ordinal));

        foreach (var command in RenderedCommands)
        {
            var isDisruptionConfirmation =
                drill == DrillId.AI2DisruptionRecovery &&
                command == RuntimeInputCommandKind.RespondToCue &&
                activeCue is
                {
                    Kind: RuntimeCueKind.Interruption,
                    ResponseExpectation: RuntimeCueResponseExpectation.ResponseRequired,
                };
            if (!isDisruptionConfirmation &&
                !IsRenderedForDrill(
                    drill,
                    snapshot.SessionDefinition.SourceDrill,
                    phase?.Kind,
                    command))
            {
                continue;
            }

            var availability = commandHandler.AvailabilityFor(command);
            var presentationAvailable = availability.IsAvailable;
            if (command == RuntimeInputCommandKind.RespondToCue &&
                activeCue is null)
            {
                presentationAvailable = false;
            }

            if (command == RuntimeInputCommandKind.FinishPhase &&
                phase?.Kind == RuntimeSessionPhaseKind.CueResponse &&
                !CueSequenceComplete(cueSnapshot))
            {
                presentationAvailable = false;
            }

            if (command == RuntimeInputCommandKind.StartAudit && phaseHasAuditStart)
            {
                presentationAvailable = false;
            }

            if (command == RuntimeInputCommandKind.SubmitAnswer && phaseHasAnswer)
            {
                presentationAvailable = false;
            }

            if (command == RuntimeInputCommandKind.FinishPhase &&
                RequiresSubmittedAnswer(drill, snapshot.SessionDefinition.SourceDrill, phase?.Kind) &&
                !phaseHasAnswer)
            {
                presentationAvailable = false;
            }

            if (command == RuntimeInputCommandKind.SubmitAnswer &&
                phase?.Kind == RuntimeSessionPhaseKind.Audit &&
                !phaseHasAuditStart)
            {
                presentationAvailable = false;
            }

            yield return new PreUiLiveSessionCommandState(
                command,
                presentationAvailable,
                availability.InvalidReason,
                CueInvalidReason: command == RuntimeInputCommandKind.RespondToCue && activeCue is null
                    ? RuntimeCueResponseInvalidReason.CueNotPresented
                    : null,
                LabelFor(command));
        }
    }

    private int RequiredAnswerCount(DrillId drill, RuntimeSessionPhaseKind phase)
    {
        if (drill == DrillId.DE1PairDiscrimination && phase == RuntimeSessionPhaseKind.ActiveWork)
        {
            return Math.Max(
                1,
                inputMaterials.Count(material =>
                    material.Kind == GeneratedContentMaterialKind.DiscriminationPair));
        }

        return 1;
    }

    private IEnumerable<PreUiLiveSessionMaterialState> CurrentMaterials(
        DrillId drill,
        DrillId? sourceDrill,
        RuntimeSessionPhaseDefinition? activePhase)
    {
        var preferredKinds = MaterialKindsFor(
            drill,
            sourceDrill,
            activePhase?.Kind,
            activePhase?.Id).ToList();
        if (appSessionType == AppTrainingSessionType.Transfer && TransferContractIsVisible(activePhase))
        {
            preferredKinds.InsertRange(
                0,
                [
                    GeneratedContentMaterialKind.TransferTask,
                    GeneratedContentMaterialKind.SameDemand,
                    GeneratedContentMaterialKind.ChangedContext,
                    GeneratedContentMaterialKind.SourceBranchStandard,
                ]);
        }

        var hasComponents = inputMaterials.Any(material =>
            material.Kind == GeneratedContentMaterialKind.ComponentPayload);
        if (hasComponents &&
            (activePhase?.Kind == RuntimeSessionPhaseKind.InstructionPrep ||
                IsComponentExecutionPhase(activePhase)))
        {
            preferredKinds.Add(GeneratedContentMaterialKind.ComponentPayload);
            preferredKinds.Add(GeneratedContentMaterialKind.ComponentEvidenceRequirement);
        }
        else if (hasComponents && activePhase?.Kind == RuntimeSessionPhaseKind.ReconstructionInput)
        {
            preferredKinds.Add(GeneratedContentMaterialKind.ComponentEvidenceRequirement);
        }
        var materials = activePhase?.Kind == RuntimeSessionPhaseKind.Review
            ? inputMaterials.Where(material => !IsScoringOnlyMaterial(material.Kind)).ToArray()
            : preferredKinds
                .SelectMany(kind => inputMaterials.Where(material => material.Kind == kind))
                .ToArray();

        return materials
            .DistinctBy(material => (material.Kind, material.Value))
            .Select(material => new PreUiLiveSessionMaterialState(
                material.Kind.ToString(),
                material.Name,
                PresentationMaterialValue(material, activePhase)));
    }

    private static string PresentationMaterialValue(
        GeneratedContentMaterial material,
        RuntimeSessionPhaseDefinition? activePhase)
    {
        if (material.Kind != GeneratedContentMaterialKind.ComponentPayload ||
            activePhase?.Kind == RuntimeSessionPhaseKind.InstructionPrep ||
            !IsComponentExecutionPhase(activePhase) ||
            !material.Value.Contains("component branch FH:", StringComparison.OrdinalIgnoreCase))
        {
            return material.Value;
        }

        const string challengeMarker = "; challenge ";
        const string responseMarker = "; response format ";
        var challengeStart = material.Value.IndexOf(challengeMarker, StringComparison.OrdinalIgnoreCase);
        var responseStart = material.Value.IndexOf(
            responseMarker,
            challengeStart < 0 ? 0 : challengeStart + challengeMarker.Length,
            StringComparison.OrdinalIgnoreCase);
        if (challengeStart < 0 || responseStart < 0)
        {
            return material.Value;
        }

        return material.Value[..(challengeStart + challengeMarker.Length)] +
            "Hold the target memorized during setup. Report it only after every other component response is locked." +
            material.Value[responseStart..];
    }

    private static bool IsComponentExecutionPhase(RuntimeSessionPhaseDefinition? phase)
    {
        return phase?.Kind is RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.CueResponse &&
            phase.Id is not "rule-statement" and not "relation-naming" and not "rule-declaration";
    }

    private static bool TransferContractIsVisible(RuntimeSessionPhaseDefinition? phase)
    {
        return phase?.Kind is not null and
            not RuntimeSessionPhaseKind.DelayWindow and
            not RuntimeSessionPhaseKind.Rest and
            not RuntimeSessionPhaseKind.Review;
    }

    private static bool IsScoringOnlyMaterial(GeneratedContentMaterialKind kind)
    {
        return kind is
            GeneratedContentMaterialKind.BranchScoringKey or
            GeneratedContentMaterialKind.ExpectedAction or
            GeneratedContentMaterialKind.ExpectedReconstruction or
            GeneratedContentMaterialKind.FinalExpectedOutput or
            GeneratedContentMaterialKind.ExpectedFinding or
            GeneratedContentMaterialKind.ExpectedClassification or
            GeneratedContentMaterialKind.ExpectedRule or
            GeneratedContentMaterialKind.ExpectedMapping or
            GeneratedContentMaterialKind.MatchTruth;
    }

    private static IEnumerable<GeneratedContentMaterialKind> MaterialKindsFor(
        DrillId drill,
        DrillId? sourceDrill,
        RuntimeSessionPhaseKind? phase,
        string? phaseId)
    {
        if (drill is DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery && sourceDrill.HasValue)
        {
            GeneratedContentMaterialKind[] wrapperKinds = phase switch
            {
                RuntimeSessionPhaseKind.InstructionPrep => drill == DrillId.AI1PressureRepeat
                    ?
                    [
                        GeneratedContentMaterialKind.SourceBranchStandard,
                        GeneratedContentMaterialKind.PressureSource,
                        GeneratedContentMaterialKind.NoStandardLoweringMarker,
                        GeneratedContentMaterialKind.HonestyConstraint,
                    ]
                    :
                    [
                        GeneratedContentMaterialKind.SourceBranchStandard,
                        GeneratedContentMaterialKind.SourceTask,
                        GeneratedContentMaterialKind.RestartRule,
                        GeneratedContentMaterialKind.RecoveryWindow,
                        GeneratedContentMaterialKind.HonestyConstraint,
                    ],
                RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.CueResponse => drill == DrillId.AI1PressureRepeat
                    ?
                    [
                        GeneratedContentMaterialKind.SourceBranchStandard,
                        GeneratedContentMaterialKind.PressureSource,
                        GeneratedContentMaterialKind.NoStandardLoweringMarker,
                    ]
                    :
                    [
                        GeneratedContentMaterialKind.SourceBranchStandard,
                        GeneratedContentMaterialKind.SourceTask,
                        GeneratedContentMaterialKind.DisruptionEvent,
                        GeneratedContentMaterialKind.RestartRule,
                        GeneratedContentMaterialKind.RecoveryWindow,
                    ],
                _ => [],
            };

            return wrapperKinds
                .Concat(MaterialKindsFor(sourceDrill.Value, sourceDrill: null, phase, phaseId))
                .Distinct();
        }

        if (phase == RuntimeSessionPhaseKind.InstructionPrep)
        {
            return drill switch
            {
                DrillId.FH1TargetHold or DrillId.FH2DistractorHold =>
                [
                    GeneratedContentMaterialKind.TargetStatement,
                    GeneratedContentMaterialKind.DistractorNoResponseRule,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter =>
                [
                    GeneratedContentMaterialKind.TargetSet,
                    GeneratedContentMaterialKind.ResponseWindow,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.WM1DelayedReconstruction =>
                [
                    GeneratedContentMaterialKind.EncodeInstruction,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.WM2MentalTransform =>
                [
                    GeneratedContentMaterialKind.TransformRule,
                    GeneratedContentMaterialKind.HiddenNotePolicy,
                    GeneratedContentMaterialKind.RuleExplanationPrompt,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.IR1GoNoGoRule =>
                [
                    GeneratedContentMaterialKind.RuleStatement,
                    GeneratedContentMaterialKind.CuePace,
                    GeneratedContentMaterialKind.NoGoFrequency,
                    GeneratedContentMaterialKind.ResponseWindow,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.IR2ExceptionRule =>
                [
                    GeneratedContentMaterialKind.RuleStatement,
                    GeneratedContentMaterialKind.ExceptionDefinition,
                    GeneratedContentMaterialKind.CuePace,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.DE1PairDiscrimination =>
                [
                    GeneratedContentMaterialKind.RelevantFeature,
                    GeneratedContentMaterialKind.GuessHandling,
                    GeneratedContentMaterialKind.FalsePositiveFalseNegativeKey,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.DE2SeededAudit =>
                [
                    GeneratedContentMaterialKind.AuditInstruction,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.CO1RuleExtraction =>
                [
                    GeneratedContentMaterialKind.RuleFamily,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.CO2StructureMapping =>
                [
                    GeneratedContentMaterialKind.MappingLimit,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.AI1PressureRepeat =>
                [
                    GeneratedContentMaterialKind.SourceBranchStandard,
                    GeneratedContentMaterialKind.PressureSource,
                    GeneratedContentMaterialKind.NoStandardLoweringMarker,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.AI2DisruptionRecovery =>
                [
                    GeneratedContentMaterialKind.SourceTask,
                    GeneratedContentMaterialKind.RestartRule,
                    GeneratedContentMaterialKind.RecoveryWindow,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask =>
                [
                    GeneratedContentMaterialKind.CompositeTaskPrompt,
                    GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                    GeneratedContentMaterialKind.PressureSource,
                    GeneratedContentMaterialKind.HonestyConstraint,
                ],
                _ => [],
            };
        }

        if (phase == RuntimeSessionPhaseKind.ActiveWork)
        {
            return drill switch
            {
                DrillId.FH1TargetHold or DrillId.FH2DistractorHold =>
                [
                    GeneratedContentMaterialKind.TargetStatement,
                    GeneratedContentMaterialKind.HoldDuration,
                    GeneratedContentMaterialKind.RecoveryWindow,
                    GeneratedContentMaterialKind.DriftMarkingEvidenceShape,
                    GeneratedContentMaterialKind.DistractorNoResponseRule,
                ],
                DrillId.DE1PairDiscrimination =>
                [
                    GeneratedContentMaterialKind.DiscriminationPair,
                    GeneratedContentMaterialKind.RelevantFeature,
                    GeneratedContentMaterialKind.GuessHandling,
                    GeneratedContentMaterialKind.FalsePositiveFalseNegativeKey,
                ],
                DrillId.CO1RuleExtraction =>
                [
                    GeneratedContentMaterialKind.PositiveExample,
                    GeneratedContentMaterialKind.NegativeExample,
                ],
                DrillId.CO2StructureMapping =>
                [
                    GeneratedContentMaterialKind.SourceStructure,
                    GeneratedContentMaterialKind.TargetStructure,
                    GeneratedContentMaterialKind.MappingLimit,
                ],
                DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule =>
                [
                    GeneratedContentMaterialKind.RuleStatement,
                    GeneratedContentMaterialKind.ExceptionDefinition,
                ],
                DrillId.AI1PressureRepeat =>
                [
                    GeneratedContentMaterialKind.SourceBranchStandard,
                    GeneratedContentMaterialKind.PressureSource,
                    GeneratedContentMaterialKind.NoStandardLoweringMarker,
                ],
                DrillId.AI2DisruptionRecovery =>
                [
                    GeneratedContentMaterialKind.SourceTask,
                    GeneratedContentMaterialKind.DisruptionEvent,
                    GeneratedContentMaterialKind.DisruptionTiming,
                    GeneratedContentMaterialKind.RestartDelay,
                    GeneratedContentMaterialKind.TaskComplexity,
                    GeneratedContentMaterialKind.RestartRule,
                    GeneratedContentMaterialKind.RecoveryWindow,
                    GeneratedContentMaterialKind.PostDisruptionEvidence,
                ],
                DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask =>
                [
                    GeneratedContentMaterialKind.CompositeTaskPrompt,
                    GeneratedContentMaterialKind.ComponentPayload,
                    GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                    GeneratedContentMaterialKind.PressureSource,
                ],
                _ => [],
            };
        }

        return phase switch
        {
            RuntimeSessionPhaseKind.EncodeWindow => drill switch
            {
                DrillId.WM2MentalTransform =>
                [
                    GeneratedContentMaterialKind.SourceItem,
                    GeneratedContentMaterialKind.TransformRule,
                    GeneratedContentMaterialKind.OperationStep,
                    GeneratedContentMaterialKind.HiddenNotePolicy,
                ],
                DrillId.DE2SeededAudit =>
                [
                    GeneratedContentMaterialKind.AuditReference,
                ],
                _ =>
                [
                    GeneratedContentMaterialKind.EncodeInstruction,
                    GeneratedContentMaterialKind.EncodeItem,
                ],
            },
            RuntimeSessionPhaseKind.CueResponse => drill == DrillId.IR2ExceptionRule
                ?
                [
                    GeneratedContentMaterialKind.RuleStatement,
                    GeneratedContentMaterialKind.ExceptionDefinition,
                    GeneratedContentMaterialKind.CuePace,
                ]
                : drill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter
                    ?
                    [
                        GeneratedContentMaterialKind.TargetSet,
                        GeneratedContentMaterialKind.ResponseWindow,
                    ]
                    :
                    [
                        GeneratedContentMaterialKind.CuePace,
                        GeneratedContentMaterialKind.NoGoFrequency,
                        GeneratedContentMaterialKind.ResponseWindow,
                    ],
            RuntimeSessionPhaseKind.ReconstructionInput => drill == DrillId.CO1RuleExtraction
                ?
                [
                    GeneratedContentMaterialKind.UnseenExample,
                ]
                : drill == DrillId.CO2StructureMapping
                ?
                [
                    GeneratedContentMaterialKind.SourceStructure,
                    GeneratedContentMaterialKind.TargetStructure,
                    GeneratedContentMaterialKind.RequiredRelation,
                    GeneratedContentMaterialKind.MappingLimit,
                ]
                : drill == DrillId.TI1CompositeTask
                ?
                [
                    GeneratedContentMaterialKind.DelayedReconstructionPayload,
                    GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                ]
                : drill == DrillId.TI2GlobalReviewTask
                ?
                [
                    GeneratedContentMaterialKind.DelayedReconstructionPayload,
                    GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                ]
                : drill == DrillId.WM2MentalTransform
                ?
                [
                    GeneratedContentMaterialKind.TransformRule,
                    GeneratedContentMaterialKind.RuleExplanationPrompt,
                    GeneratedContentMaterialKind.HiddenNotePolicy,
                ]
                :
                [
                    GeneratedContentMaterialKind.ReconstructionInstruction,
                ],
            RuntimeSessionPhaseKind.Audit => drill == DrillId.TI2GlobalReviewTask
                ?
                [
                    GeneratedContentMaterialKind.CompositeTaskPrompt,
                    GeneratedContentMaterialKind.AuditPayload,
                    GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                    GeneratedContentMaterialKind.PressureSource,
                ]
                : drill == DrillId.CO2StructureMapping
                ?
                [
                    GeneratedContentMaterialKind.AuditPayload,
                    GeneratedContentMaterialKind.MappingLimit,
                    GeneratedContentMaterialKind.SourceStructure,
                    GeneratedContentMaterialKind.TargetStructure,
                ]
                :
                [
                    GeneratedContentMaterialKind.AuditInstruction,
                    GeneratedContentMaterialKind.LockedOriginalOutput,
                    GeneratedContentMaterialKind.NonErrorDistractor,
                ],
            RuntimeSessionPhaseKind.Recovery =>
            [
                GeneratedContentMaterialKind.SourceTask,
                GeneratedContentMaterialKind.RestartRule,
                GeneratedContentMaterialKind.RecoveryWindow,
                GeneratedContentMaterialKind.PostDisruptionEvidence,
            ],
            _ => [],
        };
    }

    private bool IsRenderedForDrill(
        DrillId drill,
        DrillId? sourceDrill,
        RuntimeSessionPhaseKind? phase,
        RuntimeInputCommandKind command)
    {
        if (command is RuntimeInputCommandKind.FinishPhase or
            RuntimeInputCommandKind.Pause or
            RuntimeInputCommandKind.Resume or
            RuntimeInputCommandKind.Abandon)
        {
            return true;
        }

        if (phase == RuntimeSessionPhaseKind.ReconstructionInput &&
            inputMaterials.Any(material => material.Kind == GeneratedContentMaterialKind.ComponentPayload) &&
            command is RuntimeInputCommandKind.SubmitAnswer or
                RuntimeInputCommandKind.MarkError or
                RuntimeInputCommandKind.Correct)
        {
            return true;
        }

        if (phase == RuntimeSessionPhaseKind.Audit &&
            drill == DrillId.CO2StructureMapping &&
            command is RuntimeInputCommandKind.StartAudit or
                RuntimeInputCommandKind.SubmitAnswer or
                RuntimeInputCommandKind.MarkError or
                RuntimeInputCommandKind.Correct)
        {
            return true;
        }

        if (phase == RuntimeSessionPhaseKind.ActiveWork &&
            drill is DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule &&
            command is RuntimeInputCommandKind.SubmitAnswer or RuntimeInputCommandKind.Correct)
        {
            return true;
        }

        if (drill is DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery &&
            command == RuntimeInputCommandKind.MarkGuess)
        {
            return true;
        }

        if (drill is DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery && sourceDrill.HasValue)
        {
            return IsRenderedForDrill(sourceDrill.Value, sourceDrill: null, phase, command);
        }

        return drill switch
        {
            DrillId.FH1TargetHold or DrillId.FH2DistractorHold => command is
                RuntimeInputCommandKind.MarkDrift or
                RuntimeInputCommandKind.MarkReturn or
                RuntimeInputCommandKind.MarkTargetChange,
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter or
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => command is
                RuntimeInputCommandKind.RespondToCue or
                RuntimeInputCommandKind.MarkError or
                RuntimeInputCommandKind.Correct,
            DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform => command is
                RuntimeInputCommandKind.SubmitAnswer or
                RuntimeInputCommandKind.MarkGuess or
                RuntimeInputCommandKind.MarkError or
                RuntimeInputCommandKind.Correct,
            DrillId.DE1PairDiscrimination => command is
                RuntimeInputCommandKind.SubmitAnswer or
                RuntimeInputCommandKind.MarkGuess or
                RuntimeInputCommandKind.Correct,
            DrillId.DE2SeededAudit or DrillId.TI2GlobalReviewTask => command is
                RuntimeInputCommandKind.StartAudit or
                RuntimeInputCommandKind.SubmitAnswer or
                RuntimeInputCommandKind.MarkGuess or
                RuntimeInputCommandKind.Correct,
            DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping or DrillId.TI1CompositeTask => command is
                RuntimeInputCommandKind.SubmitAnswer or
                RuntimeInputCommandKind.MarkError or
                RuntimeInputCommandKind.Correct,
            _ => false,
        };
    }

    private bool RequiresSubmittedAnswer(
        DrillId drill,
        DrillId? sourceDrill,
        RuntimeSessionPhaseKind? phase)
    {
        if (phase == RuntimeSessionPhaseKind.ReconstructionInput &&
            inputMaterials.Any(material => material.Kind == GeneratedContentMaterialKind.ComponentPayload))
        {
            return true;
        }


        if (phase == RuntimeSessionPhaseKind.ActiveWork &&
            drill == DrillId.WM2MentalTransform &&
            inputMaterials.Any(material => material.Kind == GeneratedContentMaterialKind.ComponentPayload))
        {
            return true;
        }

        return phase switch
        {
            RuntimeSessionPhaseKind.ReconstructionInput => drill is
                DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform or
                DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping or
                DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask,
            RuntimeSessionPhaseKind.Audit => drill is
                DrillId.DE2SeededAudit or DrillId.CO2StructureMapping or DrillId.TI2GlobalReviewTask,
            RuntimeSessionPhaseKind.ActiveWork => (sourceDrill ?? drill) is
                DrillId.DE1PairDiscrimination or
                DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping or
                DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule or
                DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask,
            _ => false,
        };
    }

    private bool CueSequenceComplete(RuntimeCueSchedulerSnapshot? snapshot)
    {
        if (cueScheduler is null || snapshot is null || snapshot.CueStates.Count == 0)
        {
            return false;
        }

        foreach (var state in snapshot.CueStates)
        {
            if (!state.PresentedAt.HasValue)
            {
                return false;
            }

            if (state.ResponseEventSequenceNumber.HasValue)
            {
                continue;
            }

            var cue = cueScheduler.Schedule.Cues.Single(item => item.Id == state.CueId);
            if (snapshot.CapturedAt.ElapsedSince(state.PresentedAt.Value).Value <= cue.ResponseWindow.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static PreUiLiveSessionTimerState TimerState(
        RuntimeSessionSnapshot snapshot,
        RuntimeSessionPhaseDefinition? activePhase)
    {
        var elapsed = snapshot.PhaseScheduler.CurrentPhaseElapsed?.Value ?? TimeSpan.Zero;
        if (activePhase?.ScheduledDuration is not { } scheduled)
        {
            return new PreUiLiveSessionTimerState(
                elapsed,
                Remaining: null,
                Progress: null,
                IsTimed: false);
        }

        var remaining = scheduled.Value > elapsed ? scheduled.Value - elapsed : TimeSpan.Zero;
        var progress = scheduled.Value.TotalMilliseconds <= 0
            ? 1d
            : Math.Clamp(elapsed.TotalMilliseconds / scheduled.Value.TotalMilliseconds, 0d, 1d);

        return new PreUiLiveSessionTimerState(
            elapsed,
            remaining,
            progress,
            IsTimed: true);
    }

    private PreUiLiveSessionEvidenceState EvidenceState(RuntimeSessionSnapshot snapshot)
    {
        return new PreUiLiveSessionEvidenceState(
            snapshot.RuntimeEvents.Count,
            snapshot.EvidenceFacts.Count,
            CountEvents(snapshot, RuntimeEventKind.DriftMarked),
            CountEvents(snapshot, RuntimeEventKind.GuessMarked),
            CountEvents(snapshot, RuntimeEventKind.ErrorRecorded) + CueFailureCount(snapshot),
            CountEvents(snapshot, RuntimeEventKind.CueEmitted),
            CountEvents(snapshot, RuntimeEventKind.CueResponseSubmitted),
            CountEvents(snapshot, RuntimeEventKind.AnswerSubmitted),
            CountEvents(snapshot, RuntimeEventKind.CorrectionSubmitted),
            expectedEvidenceFacts.Count,
            ReturnCount: CountEvents(snapshot, RuntimeEventKind.RecoveryCompleted),
            LateReturnCount: CountFactValue(snapshot, "return_timing_outcome", "late"),
            TargetChangeCount: CountFactValue(snapshot, "error_kind", "target_substitution"),
            OpenDriftCount: OpenDriftCount(snapshot),
            MaximumReturnTime: MaximumReturnTime(snapshot));
    }

    private int CueFailureCount(RuntimeSessionSnapshot snapshot)
    {
        var incorrectOrLate = CountFactValue(snapshot, "response_outcome", "incorrect") +
            CountFactValue(snapshot, "response_outcome", "late");
        if (cueScheduler is null)
        {
            return incorrectOrLate;
        }

        var cueSnapshot = cueScheduler.CaptureSnapshot();
        var omitted = cueSnapshot.CueStates.Count(state =>
        {
            if (!state.PresentedAt.HasValue || state.ResponseEventSequenceNumber.HasValue)
            {
                return false;
            }

            var cue = cueScheduler.Schedule.Cues.Single(item => item.Id == state.CueId);
            return cue.ResponseExpectation == RuntimeCueResponseExpectation.ResponseRequired &&
                cueSnapshot.CapturedAt.ElapsedSince(state.PresentedAt.Value).Value > cue.ResponseWindow.Value;
        });

        return incorrectOrLate + omitted;
    }

    private static int CountEvents(RuntimeSessionSnapshot snapshot, RuntimeEventKind kind)
    {
        return snapshot.RuntimeEvents.Count(runtimeEvent => runtimeEvent.Kind == kind);
    }

    private static int OpenDriftCount(RuntimeSessionSnapshot snapshot)
    {
        var returnedDriftIds = snapshot.RuntimeEvents
            .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.RecoveryCompleted)
            .Select(runtimeEvent => EventFact(runtimeEvent, "drift_id"))
            .Where(driftId => driftId is not null)
            .ToHashSet(StringComparer.Ordinal);

        return snapshot.RuntimeEvents.Count(runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.DriftMarked &&
            (EventFact(runtimeEvent, "drift_id") is not { } driftId ||
                !returnedDriftIds.Contains(driftId)));
    }

    private static TimeSpan? MaximumReturnTime(RuntimeSessionSnapshot snapshot)
    {
        var returnTimes = snapshot.RuntimeEvents
            .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.RecoveryCompleted)
            .Select(runtimeEvent => EventFact(runtimeEvent, "recovery_time"))
            .Where(value => value is not null)
            .Select(value => TimeSpan.TryParseExact(
                value,
                "c",
                System.Globalization.CultureInfo.InvariantCulture,
                out var duration)
                    ? duration
                    : TimeSpan.Zero)
            .ToArray();

        return returnTimes.Length == 0 ? null : returnTimes.Max();
    }

    private static string? EventFact(RuntimeEvent runtimeEvent, string name)
    {
        return runtimeEvent.Facts.LastOrDefault(fact =>
            string.Equals(fact.Name, name, StringComparison.Ordinal))?.Value;
    }

    private static int CountFactValue(
        RuntimeSessionSnapshot snapshot,
        string factName,
        string factValue)
    {
        return snapshot.RuntimeEvents.Count(runtimeEvent => runtimeEvent.Facts.Any(fact =>
            string.Equals(fact.Name, factName, StringComparison.Ordinal) &&
            string.Equals(fact.Value, factValue, StringComparison.Ordinal)));
    }

    private static bool IsLevelOneTargetHold(RuntimeSessionSnapshot snapshot)
    {
        return snapshot.SessionDefinition.Branch == BranchCode.FH &&
            snapshot.SessionDefinition.Level == GlobalLevelId.L1 &&
            snapshot.SessionDefinition.Drill == DrillId.FH1TargetHold;
    }

    private bool TargetStatedBeforeSet(RuntimeSessionSnapshot snapshot)
    {
        var targetWasAvailable = inputMaterials.Any(material =>
            material.Kind == GeneratedContentMaterialKind.TargetStatement &&
            !string.IsNullOrWhiteSpace(material.Value));
        var activeSetStarted = snapshot.RuntimeEvents.Any(runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.PhaseStarted &&
            runtimeEvent.PhaseKind == RuntimeSessionPhaseKind.ActiveWork);

        return targetWasAvailable && activeSetStarted;
    }

    private static RuntimeSessionPhaseDefinition? ActivePhase(RuntimeSessionSnapshot snapshot)
    {
        return snapshot.PhaseScheduler.CurrentPhaseId is null
            ? null
            : snapshot.PhasePlan.Phases.FirstOrDefault(phase =>
                string.Equals(phase.Id, snapshot.PhaseScheduler.CurrentPhaseId, StringComparison.Ordinal));
    }

    private static PreUiLiveSessionCueState? ActiveCueState(
        RuntimeCueScheduler scheduler,
        RuntimeCueSchedulerSnapshot cueSnapshot)
    {
        var activeState = cueSnapshot.CueStates
            .Where(state => state.PresentedAt.HasValue && !state.ResponseEventSequenceNumber.HasValue)
            .OrderByDescending(state => state.PresentedAt!.Value.Offset)
            .ThenByDescending(state =>
                scheduler.Schedule.Cues.Single(item => item.Id == state.CueId).ScheduledAt.Offset)
            .FirstOrDefault(state =>
            {
                var scheduledCue = scheduler.Schedule.Cues.Single(item => item.Id == state.CueId);
                return cueSnapshot.CapturedAt.ElapsedSince(state.PresentedAt!.Value).Value <=
                    scheduledCue.ResponseWindow.Value;
            });
        if (activeState is null)
        {
            return null;
        }

        var cue = scheduler.Schedule.Cues.Single(item => item.Id == activeState.CueId);
        return new PreUiLiveSessionCueState(
            cue.Id,
            cue.Kind,
            cue.Cue,
            cue.ResponseExpectation,
            cue.ResponseWindow.Value,
            cue.ExpectedResponse,
            string.Equals(
                cue.Id,
                StabilizationGeneratedContent.ControlledDistractorId,
                StringComparison.Ordinal));
    }

    private static PreUiLiveSessionCommandOutcome FromAvailability(
        RuntimeInputCommandAvailability availability,
        string detail)
    {
        return new PreUiLiveSessionCommandOutcome(
            availability.Command,
            IsAccepted: false,
            availability.InvalidReason,
            CueInvalidReason: null,
            CueOutcome: null,
            EventCount: 0,
            detail);
    }

    private string GeneratedCommandId(string prefix)
    {
        return $"{prefix}-{commandHandler.Events.Count + 1}";
    }

    private static IEnumerable<GeneratedContentMaterial> RestoredInputMaterials(
        RuntimeSessionDefinition definition)
    {
        var drill = ProgramCatalog.Drills.First(item => item.Id == definition.Drill);
        yield return new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TargetStatement,
            "restored-standard",
            definition.Standard.Standard);
        yield return new GeneratedContentMaterial(
            GeneratedContentMaterialKind.HonestyConstraint,
            "restored-honesty-constraint",
            drill.HonestyConstraint);

        var constraintIndex = 1;
        foreach (var constraint in definition.CriticalConstraints)
        {
            yield return new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                $"restored-critical-constraint-{constraintIndex++}",
                constraint.Description);
        }

        foreach (var variable in definition.LoadVariables)
        {
            yield return new GeneratedContentMaterial(
                GeneratedContentMaterialKind.LoadVariable,
                variable.Name,
                variable.Value);
        }

        if (definition.GeneratedDrillInstance is { } generated)
        {
            yield return new GeneratedContentMaterial(
                GeneratedContentMaterialKind.RetestRequirement,
                "generated-instance",
                generated.InstanceId);
        }
    }

    private static IEnumerable<GeneratedContentPayloadFact> RestoredExpectedEvidenceFacts(
        RuntimeSessionDefinition definition)
    {
        yield return new GeneratedContentPayloadFact("restored-session-id", definition.GeneratedDrillInstance?.InstanceId ?? "no-generated-instance");
        yield return new GeneratedContentPayloadFact("standard", definition.Standard.Standard);
        yield return new GeneratedContentPayloadFact("branch", definition.Branch.ToString());
        yield return new GeneratedContentPayloadFact("level", definition.Level.ToString());
        yield return new GeneratedContentPayloadFact("drill", definition.Drill.ToString());

        foreach (var constraint in definition.CriticalConstraints)
        {
            yield return new GeneratedContentPayloadFact("critical-constraint", constraint.Description);
        }
    }

    private async ValueTask SaveSnapshotAsync(CancellationToken cancellationToken)
    {
        _ = await PersistActiveSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string LabelFor(RuntimeInputCommandKind command)
    {
        return command switch
        {
            RuntimeInputCommandKind.MarkDrift => "Mind wandered",
            RuntimeInputCommandKind.MarkReturn => "Back on target",
            RuntimeInputCommandKind.MarkTargetChange => "Target changed",
            RuntimeInputCommandKind.RespondToCue => "Respond",
            RuntimeInputCommandKind.SubmitAnswer => "Submit answer",
            RuntimeInputCommandKind.MarkGuess => "Mark guess",
            RuntimeInputCommandKind.MarkError => "Mark error",
            RuntimeInputCommandKind.Correct => "Fix answer",
            RuntimeInputCommandKind.StartAudit => "Start audit",
            RuntimeInputCommandKind.FinishPhase => "Next step",
            RuntimeInputCommandKind.Pause => "Pause",
            RuntimeInputCommandKind.Resume => "Resume",
            RuntimeInputCommandKind.Abandon => "Stop early",
            _ => command.ToString(),
        };
    }
}
