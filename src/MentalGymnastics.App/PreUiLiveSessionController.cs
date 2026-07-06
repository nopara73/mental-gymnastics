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
    string? ExpectedResponse);

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
    int ExpectedEvidenceFactCount);

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
    string Detail)
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
    NotTerminal,
}

public sealed class PreUiLiveSessionCompletionRequest
{
    public PreUiLiveSessionCompletionRequest(
        TrainingDate completedOn,
        LocalSessionIntensity intensity = LocalSessionIntensity.Moderate,
        string? notes = null,
        bool recoveryMarked = false,
        bool deloadMarked = false)
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
    }

    public TrainingDate CompletedOn { get; }

    public LocalSessionIntensity Intensity { get; }

    public string? Notes { get; }

    public bool RecoveryMarked { get; }

    public bool DeloadMarked { get; }
}

public sealed record PreUiLiveSessionCompletionResult(
    PreUiLiveSessionCompletionStatus Status,
    RuntimeSessionCompletionStatus? RuntimeCompletionStatus,
    PreUiTrainingWorkflowCompletionResult? WorkflowResult,
    PreUiLiveSessionState SessionState,
    string Detail)
{
    public bool IsProcessed => Status == PreUiLiveSessionCompletionStatus.Processed;

    public bool GrantsAdvancementInApp => false;
}

public sealed class PreUiLiveSessionController
{
    private static readonly RuntimeInputCommandKind[] RenderedCommands =
    [
        RuntimeInputCommandKind.MarkDrift,
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
    private readonly bool saveActiveSnapshot;

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
        this.saveActiveSnapshot = saveActiveSnapshot;
    }

    public PreUiLiveSessionController(
        PreUiTrainingWorkflowService workflow,
        RuntimeInputCommandHandler commandHandler,
        RuntimeCueScheduler? cueScheduler,
        IEnumerable<GeneratedContentMaterial>? inputMaterials = null,
        IEnumerable<GeneratedContentPayloadFact>? expectedEvidenceFacts = null,
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
        this.saveActiveSnapshot = saveActiveSnapshot;
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

    public async ValueTask<PreUiLiveSessionState> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        var timedAdvance = AdvanceRuntimeIfReady();
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
            if (request.Command == RuntimeInputCommandKind.Pause)
            {
                cueScheduler?.Pause();
            }
            else if (request.Command == RuntimeInputCommandKind.Resume)
            {
                cueScheduler?.Resume();
            }

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

        var runtimeResult = BuildCompletionResult(snapshot, completionStatus.Value, request);
        var workflowResult = await workflow.CompleteSessionAsync(
            new PreUiTrainingWorkflowCompletionRequest(
                new CompletedRuntimeSessionProcessingRequest(
                    runtimeResult,
                    new RuntimePersistenceHandoffMetadata(
                        request.CompletedOn,
                        request.Intensity,
                        CleanPerformance(completionStatus.Value, snapshot),
                        request.Notes ?? DefaultCompletionNotes(snapshot, completionStatus.Value),
                        recoveryMarked: request.RecoveryMarked,
                        deloadMarked: request.DeloadMarked)),
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
            commandHandler.CurrentPhase?.Kind != RuntimeSessionPhaseKind.CueResponse)
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

        var cueId = request.TargetId ?? ActiveCue(cueScheduler.CaptureSnapshot())?.CueId;
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

        var response = request.Value ?? "response";
        var result = cueScheduler.RecordResponse(RuntimeCueResponse.ForCue(cueId, response));
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
            RuntimeInputCommandKind.SubmitAnswer =>
                RuntimeInputCommand.SubmitAnswer(
                    request.TargetId ?? GeneratedCommandId("answer"),
                    request.Value ?? "submitted"),
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
        var captureKind = completionStatus != RuntimeSessionCompletionStatus.Completed
            ? RuntimeEvidenceCaptureKind.FailedSet
            : HasFailureMarks(snapshot)
                ? RuntimeEvidenceCaptureKind.FailedSet
                : RuntimeEvidenceCaptureKind.BestSet;

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
            completionFacts: CompletionFacts(snapshot, completionStatus, request)));

        return result.CompletionResult;
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

    private static IReadOnlyList<RuntimeEventFact> CompletionFacts(
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
                CountEvents(snapshot, RuntimeEventKind.GuessMarked);
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

    private static bool CleanPerformance(
        RuntimeSessionCompletionStatus completionStatus,
        RuntimeSessionSnapshot snapshot)
    {
        _ = completionStatus;
        _ = snapshot;

        // Generic live completion persists evidence; clean readiness credit requires explicit standard evaluation.
        return false;
    }

    private static bool HasFailureMarks(RuntimeSessionSnapshot snapshot)
    {
        return CountEvents(snapshot, RuntimeEventKind.ErrorRecorded) > 0 ||
            CountEvents(snapshot, RuntimeEventKind.GuessMarked) > 0;
    }

    private static string CompletionOutputSample(RuntimeSessionSnapshot snapshot)
    {
        return $"{snapshot.SessionDefinition.Branch} {snapshot.SessionDefinition.Level} " +
            $"{snapshot.SessionDefinition.Drill} live session completed from runtime event log.";
    }

    private static string CompletionScore(RuntimeSessionSnapshot snapshot)
    {
        return string.Join(
            "; ",
            [
                $"drift_count={CountEvents(snapshot, RuntimeEventKind.DriftMarked)}",
                $"guess_count={CountEvents(snapshot, RuntimeEventKind.GuessMarked)}",
                $"error_count={CountEvents(snapshot, RuntimeEventKind.ErrorRecorded)}",
                $"answer_count={CountEvents(snapshot, RuntimeEventKind.AnswerSubmitted)}",
                $"cue_response_count={CountEvents(snapshot, RuntimeEventKind.CueResponseSubmitted)}",
                $"completed_phase_count={snapshot.PhaseScheduler.CompletedPhases.Count}",
            ]);
    }

    private static string FailureSummary(RuntimeSessionSnapshot snapshot)
    {
        return string.Join(
            "; ",
            [
                $"guess_count={CountEvents(snapshot, RuntimeEventKind.GuessMarked)}",
                $"error_count={CountEvents(snapshot, RuntimeEventKind.ErrorRecorded)}",
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
            CurrentMaterials(activePhase).ToArray(),
            CommandStates(activeCue).ToArray(),
            EvidenceState(snapshot),
            lastCommand,
            detail);
    }

    private IEnumerable<PreUiLiveSessionCommandState> CommandStates(
        PreUiLiveSessionCueState? activeCue)
    {
        foreach (var command in RenderedCommands)
        {
            var availability = commandHandler.AvailabilityFor(command);
            if (command == RuntimeInputCommandKind.RespondToCue && availability.IsAvailable && activeCue is null)
            {
                yield return new PreUiLiveSessionCommandState(
                    command,
                    IsAvailable: false,
                    InvalidReason: null,
                    CueInvalidReason: RuntimeCueResponseInvalidReason.CueNotPresented,
                    LabelFor(command));
                continue;
            }

            yield return new PreUiLiveSessionCommandState(
                command,
                availability.IsAvailable,
                availability.InvalidReason,
                CueInvalidReason: null,
                LabelFor(command));
        }
    }

    private IEnumerable<PreUiLiveSessionMaterialState> CurrentMaterials(
        RuntimeSessionPhaseDefinition? activePhase)
    {
        var preferredKinds = MaterialKindsFor(activePhase?.Kind).ToArray();
        var materials = inputMaterials
            .Where(material => preferredKinds.Length == 0 || preferredKinds.Contains(material.Kind))
            .Take(8)
            .ToArray();

        if (materials.Length == 0)
        {
            materials = inputMaterials.Take(8).ToArray();
        }

        return materials.Select(material => new PreUiLiveSessionMaterialState(
            material.Kind.ToString(),
            material.Name,
            material.Value));
    }

    private static IEnumerable<GeneratedContentMaterialKind> MaterialKindsFor(
        RuntimeSessionPhaseKind? phase)
    {
        return phase switch
        {
            RuntimeSessionPhaseKind.InstructionPrep =>
            [
                GeneratedContentMaterialKind.TargetStatement,
                GeneratedContentMaterialKind.TargetSet,
                GeneratedContentMaterialKind.RuleStatement,
                GeneratedContentMaterialKind.HonestyConstraint,
                GeneratedContentMaterialKind.SourceBranchStandard,
                GeneratedContentMaterialKind.PressureSource,
            ],
            RuntimeSessionPhaseKind.EncodeWindow =>
            [
                GeneratedContentMaterialKind.EncodeInstruction,
                GeneratedContentMaterialKind.EncodeItem,
                GeneratedContentMaterialKind.SourceItem,
            ],
            RuntimeSessionPhaseKind.CueResponse =>
            [
                GeneratedContentMaterialKind.CueStep,
                GeneratedContentMaterialKind.ValidCue,
                GeneratedContentMaterialKind.InvalidCue,
                GeneratedContentMaterialKind.GoNoGoCue,
                GeneratedContentMaterialKind.ExpectedAction,
            ],
            RuntimeSessionPhaseKind.ReconstructionInput =>
            [
                GeneratedContentMaterialKind.ReconstructionInstruction,
                GeneratedContentMaterialKind.ExpectedReconstruction,
                GeneratedContentMaterialKind.FinalExpectedOutput,
            ],
            RuntimeSessionPhaseKind.Audit =>
            [
                GeneratedContentMaterialKind.AuditInstruction,
                GeneratedContentMaterialKind.AuditPayload,
                GeneratedContentMaterialKind.ExpectedFinding,
            ],
            _ => [],
        };
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
            CountEvents(snapshot, RuntimeEventKind.ErrorRecorded),
            CountEvents(snapshot, RuntimeEventKind.CueEmitted),
            CountEvents(snapshot, RuntimeEventKind.CueResponseSubmitted),
            CountEvents(snapshot, RuntimeEventKind.AnswerSubmitted),
            CountEvents(snapshot, RuntimeEventKind.CorrectionSubmitted),
            expectedEvidenceFacts.Count);
    }

    private static int CountEvents(RuntimeSessionSnapshot snapshot, RuntimeEventKind kind)
    {
        return snapshot.RuntimeEvents.Count(runtimeEvent => runtimeEvent.Kind == kind);
    }

    private static RuntimeSessionPhaseDefinition? ActivePhase(RuntimeSessionSnapshot snapshot)
    {
        return snapshot.PhaseScheduler.CurrentPhaseId is null
            ? null
            : snapshot.PhasePlan.Phases.FirstOrDefault(phase =>
                string.Equals(phase.Id, snapshot.PhaseScheduler.CurrentPhaseId, StringComparison.Ordinal));
    }

    private static RuntimeCueStateSnapshot? ActiveCue(RuntimeCueSchedulerSnapshot cueSnapshot)
    {
        return cueSnapshot.CueStates
            .Where(cueState => cueState.PresentedAt.HasValue && !cueState.ResponseEventSequenceNumber.HasValue)
            .OrderBy(cueState => cueState.PresentedAt!.Value.Offset)
            .LastOrDefault();
    }

    private static PreUiLiveSessionCueState? ActiveCueState(
        RuntimeCueScheduler scheduler,
        RuntimeCueSchedulerSnapshot cueSnapshot)
    {
        var activeCue = ActiveCue(cueSnapshot);
        if (activeCue is null)
        {
            return null;
        }

        var cue = scheduler.Schedule.Cues.Single(item => item.Id == activeCue.CueId);
        return new PreUiLiveSessionCueState(
            cue.Id,
            cue.Kind,
            cue.Cue,
            cue.ResponseExpectation,
            cue.ResponseWindow.Value,
            cue.ExpectedResponse);
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
            RuntimeInputCommandKind.MarkDrift => "Drift",
            RuntimeInputCommandKind.RespondToCue => "Respond",
            RuntimeInputCommandKind.SubmitAnswer => "Submit",
            RuntimeInputCommandKind.MarkGuess => "Guess",
            RuntimeInputCommandKind.MarkError => "Error",
            RuntimeInputCommandKind.Correct => "Correct",
            RuntimeInputCommandKind.StartAudit => "Audit",
            RuntimeInputCommandKind.FinishPhase => "Next",
            RuntimeInputCommandKind.Pause => "Pause",
            RuntimeInputCommandKind.Resume => "Resume",
            RuntimeInputCommandKind.Abandon => "Abandon",
            _ => command.ToString(),
        };
    }
}
