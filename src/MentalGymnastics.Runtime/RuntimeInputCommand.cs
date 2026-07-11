namespace MentalGymnastics.Runtime;

public enum RuntimeInputCommandKind
{
    MarkDrift,
    MarkReturn,
    MarkTargetChange,
    RespondToCue,
    SubmitAnswer,
    MarkGuess,
    MarkError,
    Correct,
    StartAudit,
    FinishPhase,
    Pause,
    Resume,
    Abandon,
}

public enum RuntimeInputCommandInvalidReason
{
    CommandAfterTerminalSession,
    SessionPaused,
    SessionNotRunning,
    NoActivePhase,
    CommandNotAllowedInCurrentPhase,
    NoCorrectableEvent,
    CorrectionWindowExpired,
    InvalidCommandPayload,
    CommandNotSupportedByDrill,
    NoOpenDrift,
    OpenDriftRequiresReturn,
    InvalidPhaseCompletion,
    PauseNotAllowed,
    IllegalLifecycleTransition,
}

public sealed class RuntimeInputCommand
{
    public RuntimeInputCommand(
        RuntimeInputCommandKind kind,
        IEnumerable<RuntimeEventFact>? facts = null)
    {
        EnsureDefined(kind, nameof(kind));

        var factArray = (facts ?? Array.Empty<RuntimeEventFact>()).ToArray();
        foreach (var fact in factArray)
        {
            ArgumentNullException.ThrowIfNull(fact);
        }

        Kind = kind;
        Facts = Array.AsReadOnly(factArray);
    }

    public RuntimeInputCommandKind Kind { get; }

    public IReadOnlyList<RuntimeEventFact> Facts { get; }

    public static RuntimeInputCommand MarkDrift(string driftId)
    {
        return new RuntimeInputCommand(
            RuntimeInputCommandKind.MarkDrift,
            [new RuntimeEventFact("drift_id", driftId)]);
    }

    public static RuntimeInputCommand MarkReturn(RuntimeDuration returnWindow)
    {
        if (returnWindow.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(returnWindow),
                returnWindow,
                "Focus return window must be positive.");
        }

        return new RuntimeInputCommand(
            RuntimeInputCommandKind.MarkReturn,
            [new RuntimeEventFact(
                "recovery_window",
                returnWindow.Value.ToString("c", System.Globalization.CultureInfo.InvariantCulture))]);
    }

    public static RuntimeInputCommand MarkTargetChange(string substituteTarget)
    {
        if (string.IsNullOrWhiteSpace(substituteTarget))
        {
            throw new ArgumentException(
                "Focus hold substitute target is required.",
                nameof(substituteTarget));
        }

        return new RuntimeInputCommand(
            RuntimeInputCommandKind.MarkTargetChange,
            [
                new RuntimeEventFact("substitute_target", substituteTarget),
                new RuntimeEventFact("error_kind", "target_substitution"),
                new RuntimeEventFact("failed_constraint", "no_target_substitution"),
                new RuntimeEventFact("target_substitution_detected", "true"),
                new RuntimeEventFact("target_substitution_allowed", "false"),
            ]);
    }

    public static RuntimeInputCommand RespondToCue(string cueId, string response)
    {
        return new RuntimeInputCommand(
            RuntimeInputCommandKind.RespondToCue,
            [
                new RuntimeEventFact("cue_id", cueId),
                new RuntimeEventFact("response", response),
            ]);
    }

    public static RuntimeInputCommand SubmitAnswer(string answerId, string answerReference)
    {
        return new RuntimeInputCommand(
            RuntimeInputCommandKind.SubmitAnswer,
            [
                new RuntimeEventFact("answer_id", answerId),
                new RuntimeEventFact("answer_reference", answerReference),
            ]);
    }

    public static RuntimeInputCommand MarkGuess(string guessId)
    {
        return new RuntimeInputCommand(
            RuntimeInputCommandKind.MarkGuess,
            [new RuntimeEventFact("guess_id", guessId)]);
    }

    public static RuntimeInputCommand MarkError(string errorId, string errorKind)
    {
        return new RuntimeInputCommand(
            RuntimeInputCommandKind.MarkError,
            [
                new RuntimeEventFact("error_id", errorId),
                new RuntimeEventFact("error_kind", errorKind),
            ]);
    }

    public static RuntimeInputCommand Correct(string correctionId, string correctionReference)
    {
        return new RuntimeInputCommand(
            RuntimeInputCommandKind.Correct,
            [
                new RuntimeEventFact("correction_id", correctionId),
                new RuntimeEventFact("correction_reference", correctionReference),
            ]);
    }

    public static RuntimeInputCommand StartAudit(string auditId)
    {
        return new RuntimeInputCommand(
            RuntimeInputCommandKind.StartAudit,
            [new RuntimeEventFact("audit_id", auditId)]);
    }

    public static RuntimeInputCommand FinishPhase()
    {
        return new RuntimeInputCommand(RuntimeInputCommandKind.FinishPhase);
    }

    public static RuntimeInputCommand Pause()
    {
        return new RuntimeInputCommand(RuntimeInputCommandKind.Pause);
    }

    public static RuntimeInputCommand Resume()
    {
        return new RuntimeInputCommand(RuntimeInputCommandKind.Resume);
    }

    public static RuntimeInputCommand Abandon(string reason)
    {
        return new RuntimeInputCommand(
            RuntimeInputCommandKind.Abandon,
            [new RuntimeEventFact("abandon_reason", reason)]);
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime input command value.");
        }
    }
}

public sealed class RuntimeInputCommandOptions
{
    private static readonly RuntimeSessionPhaseKind[] DefaultPauseAllowedPhaseKinds =
    [
        RuntimeSessionPhaseKind.InstructionPrep,
        RuntimeSessionPhaseKind.Rest,
        RuntimeSessionPhaseKind.Review,
    ];

    public RuntimeInputCommandOptions(
        bool PauseAllowed,
        RuntimeDuration CorrectionWindow,
        IEnumerable<RuntimeSessionPhaseKind>? PauseAllowedPhaseKinds = null)
    {
        if (CorrectionWindow.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CorrectionWindow),
                CorrectionWindow,
                "Correction window must be positive.");
        }

        var pauseAllowedPhaseKinds = (PauseAllowedPhaseKinds ?? DefaultPauseAllowedPhaseKinds)
            .Distinct()
            .ToArray();
        foreach (var phaseKind in pauseAllowedPhaseKinds)
        {
            EnsureDefined(phaseKind, nameof(PauseAllowedPhaseKinds));
        }

        this.PauseAllowed = PauseAllowed;
        this.CorrectionWindow = CorrectionWindow;
        this.PauseAllowedPhaseKinds = Array.AsReadOnly(pauseAllowedPhaseKinds);
    }

    public bool PauseAllowed { get; }

    public RuntimeDuration CorrectionWindow { get; }

    public IReadOnlyList<RuntimeSessionPhaseKind> PauseAllowedPhaseKinds { get; }

    public static RuntimeInputCommandOptions Default { get; } = new(
        PauseAllowed: false,
        CorrectionWindow: RuntimeDuration.FromSeconds(10));

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime input option value.");
        }
    }
}

public sealed record RuntimeInputCommandResult(
    bool IsAccepted,
    RuntimeInputCommandInvalidReason? InvalidReason,
    RuntimeSessionLifecycleState LifecycleState,
    RuntimePhaseSchedulerStatus SchedulerStatus,
    RuntimeSessionPhaseDefinition? CurrentPhase,
    IReadOnlyList<RuntimeEvent> Events,
    RuntimeSessionPhaseInvalidCompletionReason? PhaseInvalidReason,
    RuntimeSessionLifecycleInvalidTransitionReason? LifecycleInvalidReason);

public sealed record RuntimeInputCommandAvailability(
    RuntimeInputCommandKind Command,
    bool IsAvailable,
    RuntimeInputCommandInvalidReason? InvalidReason,
    RuntimeSessionLifecycleState LifecycleState,
    RuntimePhaseSchedulerStatus SchedulerStatus,
    RuntimeSessionPhaseDefinition? CurrentPhase);

public sealed class RuntimeInputCommandHandler
{
    private readonly IRuntimeClock _clock;
    private readonly RuntimePhaseScheduler _scheduler;
    private readonly RuntimeInputCommandOptions _options;
    private RuntimeEvent? _lastCorrectableEvent;

    private RuntimeInputCommandHandler(
        IRuntimeClock clock,
        RuntimePhaseScheduler scheduler,
        RuntimeEventLog eventLog,
        RuntimeSessionLifecycleState lifecycleState,
        RuntimeInputCommandOptions options)
    {
        _clock = clock;
        _scheduler = scheduler;
        EventLog = eventLog;
        LifecycleState = lifecycleState;
        _options = options;
    }

    public RuntimeEventLog EventLog { get; }

    public RuntimeSessionLifecycleState LifecycleState { get; private set; }

    public RuntimeSessionPhaseDefinition? CurrentPhase => _scheduler.CurrentPhase;

    public IReadOnlyList<RuntimeEvent> Events => EventLog.Events;

    public RuntimeSessionSnapshot CaptureSnapshot()
    {
        return new RuntimeSessionSnapshot(
            EventLog.SessionId,
            EventLog.SessionDefinition,
            LifecycleState,
            _options,
            _scheduler.PhasePlan,
            _scheduler.CaptureSnapshot(),
            EventLog.Events,
            _lastCorrectableEvent?.SequenceNumber,
            _clock.Now);
    }

    public RuntimeInputCommandAvailability AvailabilityFor(RuntimeInputCommandKind command)
    {
        EnsureDefined(command, nameof(command));

        if (IsTerminal(LifecycleState.Status))
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.CommandAfterTerminalSession);
        }

        return command switch
        {
            RuntimeInputCommandKind.Pause => PauseAvailability(command),
            RuntimeInputCommandKind.Resume => ResumeAvailability(command),
            RuntimeInputCommandKind.Abandon => Availability(command, true, invalidReason: null),
            RuntimeInputCommandKind.FinishPhase => FinishPhaseAvailability(command),
            _ => PhaseInputAvailability(command),
        };
    }

    public static RuntimeInputCommandHandler Start(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        RuntimeSessionPhasePlan phasePlan,
        IRuntimeClock clock,
        RuntimeInputCommandOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(phasePlan);
        ArgumentNullException.ThrowIfNull(clock);

        var resolvedOptions = options ?? RuntimeInputCommandOptions.Default;
        var lifecycleStart = RuntimeSessionLifecycleStateMachine.TryApply(
            RuntimeSessionLifecycleState.NotStarted(resolvedOptions.PauseAllowed),
            RuntimeSessionLifecycleTransition.Start);
        if (!lifecycleStart.IsValid)
        {
            throw new InvalidOperationException("Runtime input handler could not start the session lifecycle.");
        }

        var eventLog = RuntimeEventLog.Start(sessionId, sessionDefinition, clock.Now);
        var scheduler = new RuntimePhaseScheduler(sessionDefinition, phasePlan, clock);
        var schedulerStart = scheduler.Start();
        if (!schedulerStart.IsValid)
        {
            throw new InvalidOperationException("Runtime input handler could not start the phase scheduler.");
        }

        eventLog.AppendSchedulerEvents(schedulerStart.Events);

        return new RuntimeInputCommandHandler(
            clock,
            scheduler,
            eventLog,
            lifecycleStart.State,
            resolvedOptions);
    }

    public static RuntimeInputCommandHandler Restore(
        RuntimeSessionSnapshot snapshot,
        IRuntimeClock clock)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(clock);

        if (!CanRestoreForContinuation(snapshot))
        {
            throw new InvalidOperationException(
                "Runtime session snapshot cannot be restored without corrupting the drill's honesty constraints.");
        }

        var restoreOffset = clock.Now.ElapsedSince(snapshot.CapturedAt);
        var restoredEvents = snapshot.RuntimeEvents.Select(runtimeEvent => new RuntimeEvent(
            runtimeEvent.SessionId,
            runtimeEvent.SequenceNumber,
            runtimeEvent.Kind,
            runtimeEvent.OccurredAt.Add(restoreOffset),
            runtimeEvent.PhaseId,
            runtimeEvent.PhaseKind,
            runtimeEvent.Facts));
        var eventLog = RuntimeEventLog.Restore(
            snapshot.SessionId,
            snapshot.SessionDefinition,
            restoredEvents);
        var scheduler = RuntimePhaseScheduler.Restore(
            snapshot.SessionDefinition,
            snapshot.PhasePlan,
            clock,
            snapshot.PhaseScheduler);
        var handler = new RuntimeInputCommandHandler(
            clock,
            scheduler,
            eventLog,
            snapshot.LifecycleState,
            snapshot.InputOptions);

        if (snapshot.LastCorrectableEventSequenceNumber.HasValue)
        {
            handler._lastCorrectableEvent = eventLog.Events.FirstOrDefault(runtimeEvent =>
                runtimeEvent.SequenceNumber == snapshot.LastCorrectableEventSequenceNumber.Value);
            if (handler._lastCorrectableEvent is null)
            {
                throw new InvalidOperationException("Runtime session snapshot references a missing correctable event.");
            }
        }

        return handler;
    }

    public RuntimeInputCommandResult Handle(RuntimeInputCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (IsTerminal(LifecycleState.Status))
        {
            return Rejected(RuntimeInputCommandInvalidReason.CommandAfterTerminalSession);
        }

        return command.Kind switch
        {
            RuntimeInputCommandKind.Pause => Pause(command),
            RuntimeInputCommandKind.Resume => Resume(command),
            RuntimeInputCommandKind.Abandon => Abandon(command),
            RuntimeInputCommandKind.FinishPhase => FinishPhase(),
            _ => CapturePhaseInput(command),
        };
    }

    public RuntimeInputCommandResult SuspendForLifecycle()
    {
        if (IsTerminal(LifecycleState.Status))
        {
            return Rejected(RuntimeInputCommandInvalidReason.CommandAfterTerminalSession);
        }

        if (LifecycleState.Status == RuntimeSessionLifecycleStatus.Paused)
        {
            return Accepted([]);
        }

        if (LifecycleState.Status != RuntimeSessionLifecycleStatus.Running)
        {
            return Rejected(RuntimeInputCommandInvalidReason.SessionNotRunning);
        }

        var schedulerPause = _scheduler.Pause();
        if (!schedulerPause.IsValid)
        {
            return Rejected(RuntimeInputCommandInvalidReason.IllegalLifecycleTransition);
        }

        LifecycleState = LifecycleState with { Status = RuntimeSessionLifecycleStatus.Paused };
        var appendedEvent = EventLog.Append(
            RuntimeEventKind.SessionPaused,
            _clock.Now,
            CurrentPhase?.Id,
            CurrentPhase?.Kind,
            [new RuntimeEventFact("suspension_origin", "app_lifecycle")]);

        return Accepted([appendedEvent]);
    }

    public RuntimeInputCommandResult ResumeFromLifecycleSuspension()
    {
        if (IsTerminal(LifecycleState.Status))
        {
            return Rejected(RuntimeInputCommandInvalidReason.CommandAfterTerminalSession);
        }

        if (LifecycleState.Status == RuntimeSessionLifecycleStatus.Running)
        {
            return Accepted([]);
        }

        if (LifecycleState.Status != RuntimeSessionLifecycleStatus.Paused)
        {
            return Rejected(RuntimeInputCommandInvalidReason.SessionNotRunning);
        }

        var schedulerResume = _scheduler.Resume();
        if (!schedulerResume.IsValid)
        {
            return Rejected(RuntimeInputCommandInvalidReason.IllegalLifecycleTransition);
        }

        LifecycleState = LifecycleState with { Status = RuntimeSessionLifecycleStatus.Running };
        var appendedEvent = EventLog.Append(
            RuntimeEventKind.SessionResumed,
            _clock.Now,
            CurrentPhase?.Id,
            CurrentPhase?.Kind,
            [new RuntimeEventFact("suspension_origin", "app_lifecycle")]);

        return Accepted([appendedEvent]);
    }

    public RuntimeInputCommandResult AdvanceToCurrentTime()
    {
        if (IsTerminal(LifecycleState.Status))
        {
            return Rejected(RuntimeInputCommandInvalidReason.CommandAfterTerminalSession);
        }

        if (LifecycleState.Status == RuntimeSessionLifecycleStatus.Paused)
        {
            return Rejected(RuntimeInputCommandInvalidReason.SessionPaused);
        }

        if (LifecycleState.Status != RuntimeSessionLifecycleStatus.Running)
        {
            return Rejected(RuntimeInputCommandInvalidReason.SessionNotRunning);
        }

        var schedulerResult = _scheduler.AdvanceToCurrentTime();
        if (!schedulerResult.IsValid)
        {
            return Rejected(
                RuntimeInputCommandInvalidReason.InvalidPhaseCompletion,
                phaseInvalidReason: schedulerResult.PhaseInvalidReason);
        }

        var appendedEvents = EventLog.AppendSchedulerEvents(schedulerResult.Events);
        if (_scheduler.Status == RuntimePhaseSchedulerStatus.Completed)
        {
            var lifecycleResult = RuntimeSessionLifecycleStateMachine.TryApply(
                LifecycleState,
                RuntimeSessionLifecycleTransition.Complete);
            if (lifecycleResult.IsValid)
            {
                LifecycleState = lifecycleResult.State;
            }
        }

        return Accepted(appendedEvents);
    }

    private RuntimeInputCommandResult CapturePhaseInput(RuntimeInputCommand command)
    {
        if (LifecycleState.Status == RuntimeSessionLifecycleStatus.Paused)
        {
            return Rejected(RuntimeInputCommandInvalidReason.SessionPaused);
        }

        if (LifecycleState.Status != RuntimeSessionLifecycleStatus.Running)
        {
            return Rejected(RuntimeInputCommandInvalidReason.SessionNotRunning);
        }

        var phase = CurrentPhase;
        if (phase is null)
        {
            return Rejected(RuntimeInputCommandInvalidReason.NoActivePhase);
        }

        if (!IsAllowedInPhase(command.Kind, phase.Kind))
        {
            return Rejected(RuntimeInputCommandInvalidReason.CommandNotAllowedInCurrentPhase);
        }

        if (RequiresFocusHoldSession(command.Kind) && !IsFocusHoldSession())
        {
            return Rejected(RuntimeInputCommandInvalidReason.CommandNotSupportedByDrill);
        }

        if (command.Kind == RuntimeInputCommandKind.MarkDrift &&
            IsFocusHoldSession() &&
            FindOpenDrift() is not null)
        {
            return Rejected(RuntimeInputCommandInvalidReason.OpenDriftRequiresReturn);
        }

        if (command.Kind == RuntimeInputCommandKind.MarkReturn)
        {
            return CaptureFocusReturn(command, phase);
        }

        if (command.Kind == RuntimeInputCommandKind.MarkTargetChange &&
            FactValue(command.Facts, "substitute_target") is null)
        {
            return Rejected(RuntimeInputCommandInvalidReason.InvalidCommandPayload);
        }

        if (command.Kind == RuntimeInputCommandKind.Correct)
        {
            var correctionValidation = ValidateCorrectionWindow();
            if (correctionValidation is not null)
            {
                return correctionValidation;
            }
        }

        var appendedEvent = EventLog.Append(
            MapCommandEventKind(command.Kind),
            _clock.Now,
            phase.Id,
            phase.Kind,
            BuildCommandFacts(command));

        if (IsCorrectable(command.Kind))
        {
            _lastCorrectableEvent = appendedEvent;
        }

        return Accepted([appendedEvent]);
    }

    private RuntimeInputCommandAvailability PhaseInputAvailability(RuntimeInputCommandKind command)
    {
        if (LifecycleState.Status == RuntimeSessionLifecycleStatus.Paused)
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.SessionPaused);
        }

        if (LifecycleState.Status != RuntimeSessionLifecycleStatus.Running)
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.SessionNotRunning);
        }

        var phase = CurrentPhase;
        if (phase is null)
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.NoActivePhase);
        }

        if (!IsAllowedInPhase(command, phase.Kind))
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.CommandNotAllowedInCurrentPhase);
        }

        if (RequiresFocusHoldSession(command) && !IsFocusHoldSession())
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.CommandNotSupportedByDrill);
        }

        if (command == RuntimeInputCommandKind.MarkReturn && FindOpenDrift() is null)
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.NoOpenDrift);
        }

        if (command == RuntimeInputCommandKind.MarkDrift &&
            IsFocusHoldSession() &&
            FindOpenDrift() is not null)
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.OpenDriftRequiresReturn);
        }

        if (command == RuntimeInputCommandKind.Correct)
        {
            var correctionAvailability = CorrectionAvailability(command);
            if (correctionAvailability is not null)
            {
                return correctionAvailability;
            }
        }

        return Availability(command, true, invalidReason: null);
    }

    private RuntimeInputCommandAvailability? CorrectionAvailability(RuntimeInputCommandKind command)
    {
        if (_lastCorrectableEvent is null)
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.NoCorrectableEvent);
        }

        var elapsed = _clock.Now.ElapsedSince(_lastCorrectableEvent.OccurredAt);
        if (elapsed.Value > _options.CorrectionWindow.Value)
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.CorrectionWindowExpired);
        }

        return null;
    }

    private RuntimeInputCommandResult? ValidateCorrectionWindow()
    {
        if (_lastCorrectableEvent is null)
        {
            return Rejected(RuntimeInputCommandInvalidReason.NoCorrectableEvent);
        }

        var elapsed = _clock.Now.ElapsedSince(_lastCorrectableEvent.OccurredAt);
        if (elapsed.Value > _options.CorrectionWindow.Value)
        {
            return Rejected(RuntimeInputCommandInvalidReason.CorrectionWindowExpired);
        }

        return null;
    }

    private RuntimeInputCommandAvailability FinishPhaseAvailability(RuntimeInputCommandKind command)
    {
        if (LifecycleState.Status == RuntimeSessionLifecycleStatus.Paused)
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.SessionPaused);
        }

        if (LifecycleState.Status != RuntimeSessionLifecycleStatus.Running)
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.SessionNotRunning);
        }

        if (CurrentPhase is null)
        {
            return Availability(command, false, RuntimeInputCommandInvalidReason.NoActivePhase);
        }

        if (CurrentPhase.CompletionRule == RuntimeSessionPhaseCompletionRule.Timed)
        {
            var phaseElapsed = _scheduler.CaptureSnapshot().CurrentPhaseElapsed;
            if (!CurrentPhase.ScheduledDuration.HasValue ||
                !phaseElapsed.HasValue ||
                phaseElapsed.Value.Value < CurrentPhase.ScheduledDuration.Value.Value)
            {
                return Availability(command, false, RuntimeInputCommandInvalidReason.InvalidPhaseCompletion);
            }
        }

        return Availability(command, true, invalidReason: null);
    }

    private RuntimeInputCommandResult FinishPhase()
    {
        if (LifecycleState.Status == RuntimeSessionLifecycleStatus.Paused)
        {
            return Rejected(RuntimeInputCommandInvalidReason.SessionPaused);
        }

        if (LifecycleState.Status != RuntimeSessionLifecycleStatus.Running)
        {
            return Rejected(RuntimeInputCommandInvalidReason.SessionNotRunning);
        }

        if (CurrentPhase is null)
        {
            return Rejected(RuntimeInputCommandInvalidReason.NoActivePhase);
        }

        var schedulerResult = _scheduler.CompleteCurrentPhase();
        if (!schedulerResult.IsValid)
        {
            return Rejected(
                RuntimeInputCommandInvalidReason.InvalidPhaseCompletion,
                phaseInvalidReason: schedulerResult.PhaseInvalidReason);
        }

        var appendedEvents = EventLog.AppendSchedulerEvents(schedulerResult.Events);
        if (_scheduler.Status == RuntimePhaseSchedulerStatus.Completed)
        {
            var lifecycleResult = RuntimeSessionLifecycleStateMachine.TryApply(
                LifecycleState,
                RuntimeSessionLifecycleTransition.Complete);
            if (lifecycleResult.IsValid)
            {
                LifecycleState = lifecycleResult.State;
            }
        }

        return Accepted(appendedEvents);
    }

    private RuntimeInputCommandResult Pause(RuntimeInputCommand command)
    {
        var lifecycleResult = RuntimeSessionLifecycleStateMachine.TryApply(
            LifecycleState,
            RuntimeSessionLifecycleTransition.Pause);
        if (!lifecycleResult.IsValid)
        {
            var reason = lifecycleResult.InvalidReason == RuntimeSessionLifecycleInvalidTransitionReason.PauseNotAllowed
                ? RuntimeInputCommandInvalidReason.PauseNotAllowed
                : RuntimeInputCommandInvalidReason.IllegalLifecycleTransition;
            return Rejected(reason, lifecycleInvalidReason: lifecycleResult.InvalidReason);
        }

        if (!CanPauseCurrentPhase())
        {
            return Rejected(
                RuntimeInputCommandInvalidReason.PauseNotAllowed,
                lifecycleInvalidReason: RuntimeSessionLifecycleInvalidTransitionReason.PauseNotAllowed);
        }

        var schedulerPause = _scheduler.Pause();
        if (!schedulerPause.IsValid)
        {
            return Rejected(RuntimeInputCommandInvalidReason.IllegalLifecycleTransition);
        }

        LifecycleState = lifecycleResult.State;
        var appendedEvent = EventLog.Append(
            RuntimeEventKind.SessionPaused,
            _clock.Now,
            CurrentPhase?.Id,
            CurrentPhase?.Kind,
            BuildCommandFacts(command));

        return Accepted([appendedEvent]);
    }

    private RuntimeInputCommandAvailability PauseAvailability(RuntimeInputCommandKind command)
    {
        var lifecycleResult = RuntimeSessionLifecycleStateMachine.TryApply(
            LifecycleState,
            RuntimeSessionLifecycleTransition.Pause);
        if (!lifecycleResult.IsValid)
        {
            var reason = lifecycleResult.InvalidReason == RuntimeSessionLifecycleInvalidTransitionReason.PauseNotAllowed
                ? RuntimeInputCommandInvalidReason.PauseNotAllowed
                : RuntimeInputCommandInvalidReason.IllegalLifecycleTransition;
            return Availability(command, false, reason);
        }

        return CanPauseCurrentPhase()
            ? Availability(command, true, invalidReason: null)
            : Availability(command, false, RuntimeInputCommandInvalidReason.PauseNotAllowed);
    }

    private RuntimeInputCommandResult Resume(RuntimeInputCommand command)
    {
        var lifecycleResult = RuntimeSessionLifecycleStateMachine.TryApply(
            LifecycleState,
            RuntimeSessionLifecycleTransition.Resume);
        if (!lifecycleResult.IsValid)
        {
            return Rejected(
                RuntimeInputCommandInvalidReason.IllegalLifecycleTransition,
                lifecycleInvalidReason: lifecycleResult.InvalidReason);
        }

        var schedulerResume = _scheduler.Resume();
        if (!schedulerResume.IsValid)
        {
            return Rejected(RuntimeInputCommandInvalidReason.IllegalLifecycleTransition);
        }

        LifecycleState = lifecycleResult.State;
        var appendedEvent = EventLog.Append(
            RuntimeEventKind.SessionResumed,
            _clock.Now,
            CurrentPhase?.Id,
            CurrentPhase?.Kind,
            BuildCommandFacts(command));

        return Accepted([appendedEvent]);
    }

    private RuntimeInputCommandAvailability ResumeAvailability(RuntimeInputCommandKind command)
    {
        var lifecycleResult = RuntimeSessionLifecycleStateMachine.TryApply(
            LifecycleState,
            RuntimeSessionLifecycleTransition.Resume);
        return lifecycleResult.IsValid
            ? Availability(command, true, invalidReason: null)
            : Availability(command, false, RuntimeInputCommandInvalidReason.IllegalLifecycleTransition);
    }

    private RuntimeInputCommandResult Abandon(RuntimeInputCommand command)
    {
        var lifecycleResult = RuntimeSessionLifecycleStateMachine.TryApply(
            LifecycleState,
            RuntimeSessionLifecycleTransition.Abandon);
        if (!lifecycleResult.IsValid)
        {
            return Rejected(
                RuntimeInputCommandInvalidReason.IllegalLifecycleTransition,
                lifecycleInvalidReason: lifecycleResult.InvalidReason);
        }

        LifecycleState = lifecycleResult.State;
        var appendedEvent = EventLog.Append(
            RuntimeEventKind.SessionAbandoned,
            _clock.Now,
            CurrentPhase?.Id,
            CurrentPhase?.Kind,
            BuildCommandFacts(command));

        return Accepted([appendedEvent]);
    }

    private RuntimeInputCommandResult Accepted(IReadOnlyList<RuntimeEvent> events)
    {
        return new RuntimeInputCommandResult(
            IsAccepted: true,
            InvalidReason: null,
            LifecycleState,
            _scheduler.Status,
            CurrentPhase,
            events,
            PhaseInvalidReason: null,
            LifecycleInvalidReason: null);
    }

    private RuntimeInputCommandResult Rejected(
        RuntimeInputCommandInvalidReason invalidReason,
        RuntimeSessionPhaseInvalidCompletionReason? phaseInvalidReason = null,
        RuntimeSessionLifecycleInvalidTransitionReason? lifecycleInvalidReason = null)
    {
        return new RuntimeInputCommandResult(
            IsAccepted: false,
            invalidReason,
            LifecycleState,
            _scheduler.Status,
            CurrentPhase,
            Events: Array.Empty<RuntimeEvent>(),
            phaseInvalidReason,
            lifecycleInvalidReason);
    }

    private RuntimeInputCommandAvailability Availability(
        RuntimeInputCommandKind command,
        bool isAvailable,
        RuntimeInputCommandInvalidReason? invalidReason)
    {
        return new RuntimeInputCommandAvailability(
            command,
            isAvailable,
            invalidReason,
            LifecycleState,
            _scheduler.Status,
            CurrentPhase);
    }

    private static bool IsAllowedInPhase(
        RuntimeInputCommandKind command,
        RuntimeSessionPhaseKind phase)
    {
        return command switch
        {
            RuntimeInputCommandKind.MarkDrift =>
                phase is RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.CueResponse or RuntimeSessionPhaseKind.Recovery,
            RuntimeInputCommandKind.MarkReturn =>
                phase is RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.Recovery,
            RuntimeInputCommandKind.MarkTargetChange =>
                phase == RuntimeSessionPhaseKind.ActiveWork,
            RuntimeInputCommandKind.RespondToCue =>
                phase == RuntimeSessionPhaseKind.CueResponse,
            RuntimeInputCommandKind.SubmitAnswer =>
                phase is RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.ReconstructionInput or RuntimeSessionPhaseKind.Audit,
            RuntimeInputCommandKind.MarkGuess =>
                phase is RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.CueResponse or
                    RuntimeSessionPhaseKind.ReconstructionInput or RuntimeSessionPhaseKind.Audit,
            RuntimeInputCommandKind.MarkError =>
                phase is RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.CueResponse or
                    RuntimeSessionPhaseKind.ReconstructionInput or RuntimeSessionPhaseKind.Audit or RuntimeSessionPhaseKind.Recovery,
            RuntimeInputCommandKind.Correct =>
                phase is RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.ReconstructionInput or RuntimeSessionPhaseKind.Audit,
            RuntimeInputCommandKind.StartAudit =>
                phase == RuntimeSessionPhaseKind.Audit,
            _ => false,
        };
    }

    private static bool IsCorrectable(RuntimeInputCommandKind command)
    {
        return command is RuntimeInputCommandKind.SubmitAnswer or RuntimeInputCommandKind.MarkGuess;
    }

    private static RuntimeEventKind MapCommandEventKind(RuntimeInputCommandKind command)
    {
        return command switch
        {
            RuntimeInputCommandKind.MarkDrift => RuntimeEventKind.DriftMarked,
            RuntimeInputCommandKind.MarkTargetChange => RuntimeEventKind.ErrorRecorded,
            RuntimeInputCommandKind.RespondToCue => RuntimeEventKind.CueResponseSubmitted,
            RuntimeInputCommandKind.SubmitAnswer => RuntimeEventKind.AnswerSubmitted,
            RuntimeInputCommandKind.MarkGuess => RuntimeEventKind.GuessMarked,
            RuntimeInputCommandKind.MarkError => RuntimeEventKind.ErrorRecorded,
            RuntimeInputCommandKind.Correct => RuntimeEventKind.CorrectionSubmitted,
            RuntimeInputCommandKind.StartAudit => RuntimeEventKind.AuditStarted,
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Command does not map to an input event."),
        };
    }

    private static IReadOnlyList<RuntimeEventFact> BuildCommandFacts(RuntimeInputCommand command)
    {
        var facts = new List<RuntimeEventFact>
        {
            new("command_kind", StableCommandId(command.Kind)),
        };
        facts.AddRange(command.Facts);
        return facts.AsReadOnly();
    }

    private static string StableCommandId(RuntimeInputCommandKind command)
    {
        return command switch
        {
            RuntimeInputCommandKind.MarkDrift => "mark_drift",
            RuntimeInputCommandKind.MarkReturn => "mark_return",
            RuntimeInputCommandKind.MarkTargetChange => "mark_target_change",
            RuntimeInputCommandKind.RespondToCue => "respond_to_cue",
            RuntimeInputCommandKind.SubmitAnswer => "submit_answer",
            RuntimeInputCommandKind.MarkGuess => "mark_guess",
            RuntimeInputCommandKind.MarkError => "mark_error",
            RuntimeInputCommandKind.Correct => "correct",
            RuntimeInputCommandKind.StartAudit => "start_audit",
            RuntimeInputCommandKind.FinishPhase => "finish_phase",
            RuntimeInputCommandKind.Pause => "pause",
            RuntimeInputCommandKind.Resume => "resume",
            RuntimeInputCommandKind.Abandon => "abandon",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown runtime input command kind."),
        };
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime input command value.");
        }
    }

    private bool CanPauseCurrentPhase()
    {
        var phase = CurrentPhase;
        return phase is not null &&
            _options.PauseAllowedPhaseKinds.Contains(phase.Kind);
    }

    private RuntimeInputCommandResult CaptureFocusReturn(
        RuntimeInputCommand command,
        RuntimeSessionPhaseDefinition phase)
    {
        var openDrift = FindOpenDrift();
        if (openDrift is null)
        {
            return Rejected(RuntimeInputCommandInvalidReason.NoOpenDrift);
        }

        var recoveryWindowValue = FactValue(command.Facts, "recovery_window");
        if (recoveryWindowValue is null ||
            !TimeSpan.TryParseExact(
                recoveryWindowValue,
                "c",
                System.Globalization.CultureInfo.InvariantCulture,
                out var recoveryWindow) ||
            recoveryWindow <= TimeSpan.Zero)
        {
            return Rejected(RuntimeInputCommandInvalidReason.InvalidCommandPayload);
        }

        var driftId = FactValue(openDrift.Facts, "drift_id");
        if (driftId is null)
        {
            return Rejected(RuntimeInputCommandInvalidReason.InvalidCommandPayload);
        }

        var recoveryTime = _clock.Now.ElapsedSince(openDrift.OccurredAt);
        var withinWindow = recoveryTime.Value <= recoveryWindow;
        var facts = new List<RuntimeEventFact>(BuildCommandFacts(command))
        {
            new("drift_id", driftId),
            new("recovery_time", recoveryTime.Value.ToString("c", System.Globalization.CultureInfo.InvariantCulture)),
            new("return_within_window", withinWindow ? "true" : "false"),
            new("return_timing_outcome", withinWindow ? "within_window" : "late"),
            new("target_substitution_allowed", "false"),
        };
        var appendedEvent = EventLog.Append(
            RuntimeEventKind.RecoveryCompleted,
            _clock.Now,
            phase.Id,
            phase.Kind,
            facts);

        return Accepted([appendedEvent]);
    }

    private RuntimeEvent? FindOpenDrift()
    {
        var returnedDriftIds = EventLog.Events
            .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.RecoveryCompleted)
            .Select(runtimeEvent => FactValue(runtimeEvent.Facts, "drift_id"))
            .Where(driftId => driftId is not null)
            .ToHashSet(StringComparer.Ordinal);

        return EventLog.Events
            .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.DriftMarked)
            .LastOrDefault(runtimeEvent =>
                FactValue(runtimeEvent.Facts, "drift_id") is { } driftId &&
                !returnedDriftIds.Contains(driftId));
    }

    private bool IsFocusHoldSession()
    {
        var definition = EventLog.SessionDefinition;
        return definition.Branch == MentalGymnastics.Core.BranchCode.FH &&
            definition.Drill is
                MentalGymnastics.Core.DrillId.FH1TargetHold or
                MentalGymnastics.Core.DrillId.FH2DistractorHold ||
            definition.Branch == MentalGymnastics.Core.BranchCode.AI &&
            definition.SourceDrill == MentalGymnastics.Core.DrillId.FH2DistractorHold;
    }

    private static bool RequiresFocusHoldSession(RuntimeInputCommandKind command)
    {
        return command is
            RuntimeInputCommandKind.MarkReturn or
            RuntimeInputCommandKind.MarkTargetChange;
    }

    private static string? FactValue(
        IEnumerable<RuntimeEventFact> facts,
        string name)
    {
        return facts.LastOrDefault(fact =>
            string.Equals(fact.Name, name, StringComparison.Ordinal))?.Value;
    }

    private static bool CanRestoreForContinuation(RuntimeSessionSnapshot snapshot)
    {
        if (IsTerminal(snapshot.LifecycleState.Status))
        {
            return false;
        }

        if (snapshot.LifecycleState.Status == RuntimeSessionLifecycleStatus.Paused)
        {
            return true;
        }

        if (snapshot.LifecycleState.Status != RuntimeSessionLifecycleStatus.Running)
        {
            return false;
        }

        var currentPhaseId = snapshot.PhaseScheduler.CurrentPhaseId;
        if (currentPhaseId is null)
        {
            return false;
        }

        var currentPhase = snapshot.PhasePlan.Phases.FirstOrDefault(phase =>
            string.Equals(phase.Id, currentPhaseId, StringComparison.Ordinal));

        return currentPhase is not null &&
            snapshot.InputOptions.PauseAllowedPhaseKinds.Contains(currentPhase.Kind);
    }

    private static bool IsTerminal(RuntimeSessionLifecycleStatus status)
    {
        return status is
            RuntimeSessionLifecycleStatus.Completed or
            RuntimeSessionLifecycleStatus.Failed or
            RuntimeSessionLifecycleStatus.Abandoned;
    }
}
