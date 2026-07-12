using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum RuntimeCueKind
{
    FocusShift,
    InvalidCueFilter,
    GoNoGo,
    Interruption,
    TimedResponse,
}

public enum RuntimeCueResponseExpectation
{
    ResponseRequired,
    NoResponseExpected,
}

public enum RuntimeCueResponseOutcome
{
    Correct,
    Incorrect,
    Late,
}

public enum RuntimeCueResponseInvalidReason
{
    UnknownCue,
    CueNotPresented,
    CueAlreadyResponded,
    SchedulerPaused,
}

public sealed class RuntimeScheduledCue
{
    public RuntimeScheduledCue(
        string id,
        RuntimeCueKind kind,
        string cue,
        RuntimeInstant scheduledAt,
        RuntimeDuration responseWindow,
        RuntimeCueResponseExpectation responseExpectation,
        string? expectedResponse = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Runtime cue id is required.", nameof(id));
        }

        EnsureDefined(kind, nameof(kind));

        if (string.IsNullOrWhiteSpace(cue))
        {
            throw new ArgumentException("Runtime cue value is required.", nameof(cue));
        }

        if (responseWindow.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseWindow),
                responseWindow,
                "Runtime cue response window must be positive.");
        }

        EnsureDefined(responseExpectation, nameof(responseExpectation));

        if (expectedResponse is not null && string.IsNullOrWhiteSpace(expectedResponse))
        {
            throw new ArgumentException("Expected cue response cannot be blank.", nameof(expectedResponse));
        }

        Id = id;
        Kind = kind;
        Cue = cue;
        ScheduledAt = scheduledAt;
        ResponseWindow = responseWindow;
        ResponseExpectation = responseExpectation;
        ExpectedResponse = expectedResponse;
    }

    public string Id { get; }

    public RuntimeCueKind Kind { get; }

    public string Cue { get; }

    public RuntimeInstant ScheduledAt { get; }

    public RuntimeDuration ResponseWindow { get; }

    public RuntimeCueResponseExpectation ResponseExpectation { get; }

    public string? ExpectedResponse { get; }

    public RuntimeInstant ResponseDeadline => ScheduledAt.Add(ResponseWindow);

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime cue value.");
        }
    }
}

public sealed class RuntimeCueSchedule
{
    public RuntimeCueSchedule(
        RuntimeGeneratedDrillInstanceIdentity generatedDrillInstance,
        IEnumerable<RuntimeScheduledCue> cues)
        : this(generatedDrillInstance, cues, runtimeSessionType: null)
    {
    }

    public RuntimeCueSchedule(
        RuntimeGeneratedDrillInstanceIdentity generatedDrillInstance,
        IEnumerable<RuntimeScheduledCue> cues,
        SessionType runtimeSessionType)
        : this(generatedDrillInstance, cues, (SessionType?)runtimeSessionType)
    {
    }

    private RuntimeCueSchedule(
        RuntimeGeneratedDrillInstanceIdentity generatedDrillInstance,
        IEnumerable<RuntimeScheduledCue> cues,
        SessionType? runtimeSessionType)
    {
        ArgumentNullException.ThrowIfNull(generatedDrillInstance);
        ArgumentNullException.ThrowIfNull(cues);

        if (runtimeSessionType.HasValue && !Enum.IsDefined(runtimeSessionType.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeSessionType),
                runtimeSessionType,
                "Unknown runtime session type.");
        }

        var identity = generatedDrillInstance.ContentIdentity;
        var isExecutableAffectiveWrapper = identity.Branch == BranchCode.AI &&
            identity.Kind == PromptContentKind.EquivalentPrompt &&
            identity.Drill is DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery;
        var cueArray = cues.ToArray();
        if (cueArray.Length == 0)
        {
            throw new ArgumentException("Runtime cue schedules must include at least one cue.", nameof(cues));
        }

        foreach (var cue in cueArray)
        {
            ArgumentNullException.ThrowIfNull(cue);
        }

        var isControlledStabilizationDemand = runtimeSessionType == SessionType.Stabilization &&
            cueArray.Any(cue => cue.ResponseExpectation == RuntimeCueResponseExpectation.NoResponseExpected);
        if (identity.Kind != PromptContentKind.CueSequence &&
            !isExecutableAffectiveWrapper &&
            !isControlledStabilizationDemand)
        {
            throw new ArgumentException(
                "Runtime cue schedules must be tied to a generated cue sequence, an executable Affective Interference wrapper, or an explicitly scoped stabilization demand.",
                nameof(generatedDrillInstance));
        }

        var duplicateCue = cueArray
            .GroupBy(cue => cue.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCue is not null)
        {
            throw new ArgumentException("Runtime cue ids must be unique within a schedule.", nameof(cues));
        }

        GeneratedDrillInstance = generatedDrillInstance;
        RuntimeSessionType = runtimeSessionType;
        Cues = Array.AsReadOnly(cueArray
            .OrderBy(cue => cue.ScheduledAt.Offset)
            .ThenBy(cue => cue.Id, StringComparer.Ordinal)
            .ToArray());
    }

    public RuntimeGeneratedDrillInstanceIdentity GeneratedDrillInstance { get; }

    public SessionType? RuntimeSessionType { get; }

    public IReadOnlyList<RuntimeScheduledCue> Cues { get; }
}

public sealed class RuntimeCueResponse
{
    public RuntimeCueResponse(string cueId, string response)
    {
        if (string.IsNullOrWhiteSpace(cueId))
        {
            throw new ArgumentException("Runtime cue response must identify the cue.", nameof(cueId));
        }

        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Runtime cue response value is required.", nameof(response));
        }

        CueId = cueId;
        Response = response;
    }

    public string CueId { get; }

    public string Response { get; }

    public static RuntimeCueResponse ForCue(string cueId, string response)
    {
        return new RuntimeCueResponse(cueId, response);
    }
}

public sealed record RuntimeCueAdvanceResult(
    IReadOnlyList<RuntimeScheduledCue> EmittedCues,
    IReadOnlyList<RuntimeEvent> Events);

public sealed record RuntimeCueResponseResult(
    bool IsAccepted,
    RuntimeCueResponseOutcome? Outcome,
    RuntimeCueResponseInvalidReason? InvalidReason,
    RuntimeScheduledCue? Cue,
    RuntimeDuration? ResponseTime,
    RuntimeEvent? Event);

public sealed class RuntimeCueScheduler
{
    private readonly IRuntimeClock _clock;
    private readonly RuntimeEventLog _eventLog;
    private readonly Dictionary<string, RuntimeCueState> _cueStates;
    private bool _isPaused;
    private RuntimeInstant? _pausedAt;
    private RuntimeDuration _elapsedPauseDuration = RuntimeDuration.Zero;

    public RuntimeCueScheduler(
        RuntimeCueSchedule schedule,
        IRuntimeClock clock,
        RuntimeEventLog eventLog)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(eventLog);

        if (eventLog.SessionDefinition.GeneratedDrillInstance?.InstanceId != schedule.GeneratedDrillInstance.InstanceId)
        {
            throw new ArgumentException(
                "Runtime cue scheduler must use the generated drill instance attached to the active session.",
                nameof(schedule));
        }

        if (schedule.RuntimeSessionType.HasValue &&
            eventLog.SessionDefinition.SessionType != schedule.RuntimeSessionType.Value)
        {
            throw new ArgumentException(
                "Runtime cue schedule session type must match the active session.",
                nameof(schedule));
        }

        Schedule = schedule;
        _clock = clock;
        _eventLog = eventLog;
        _cueStates = schedule.Cues.ToDictionary(
            cue => cue.Id,
            cue => new RuntimeCueState(cue),
            StringComparer.Ordinal);
    }

    public RuntimeCueSchedule Schedule { get; }

    public IReadOnlyList<RuntimeScheduledCue> EmittedCues => _cueStates.Values
        .Where(state => state.PresentedAt.HasValue)
        .OrderBy(state => state.Cue.ScheduledAt.Offset)
        .ThenBy(state => state.Cue.Id, StringComparer.Ordinal)
        .Select(state => state.Cue)
        .ToArray();

    public IReadOnlyList<RuntimeScheduledCue> PendingCues => _cueStates.Values
        .Where(state => !state.PresentedAt.HasValue)
        .OrderBy(state => state.Cue.ScheduledAt.Offset)
        .ThenBy(state => state.Cue.Id, StringComparer.Ordinal)
        .Select(state => state.Cue)
        .ToArray();

    public bool IsPaused => _isPaused;

    public RuntimeCueSchedulerSnapshot CaptureSnapshot()
    {
        return new RuntimeCueSchedulerSnapshot(
            Schedule.GeneratedDrillInstance,
            _clock.Now,
            _isPaused,
            _elapsedPauseDuration,
            _cueStates.Values
                .OrderBy(state => state.Cue.ScheduledAt.Offset)
                .ThenBy(state => state.Cue.Id, StringComparer.Ordinal)
                .Select(state => new RuntimeCueStateSnapshot(
                    state.Cue.Id,
                    state.PresentedAt,
                    state.PhaseId,
                    state.PhaseKind,
                    state.ResponseEvent?.SequenceNumber)),
            _eventLog.Events);
    }

    public static RuntimeCueScheduler Restore(
        RuntimeCueSchedule schedule,
        IRuntimeClock clock,
        RuntimeEventLog eventLog,
        RuntimeCueSchedulerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!string.Equals(
                schedule.GeneratedDrillInstance.InstanceId,
                snapshot.GeneratedDrillInstance.InstanceId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Runtime cue snapshot generated instance must match the restored cue schedule.",
                nameof(snapshot));
        }

        var scheduler = new RuntimeCueScheduler(schedule, clock, eventLog);
        var interruptionDuration = clock.Now.ElapsedSince(snapshot.CapturedAt);
        scheduler._elapsedPauseDuration = new RuntimeDuration(
            snapshot.ElapsedPauseDuration.Value + interruptionDuration.Value);
        scheduler._isPaused = snapshot.IsPaused;
        scheduler._pausedAt = snapshot.IsPaused ? clock.Now : null;

        foreach (var cueStateSnapshot in snapshot.CueStates)
        {
            if (!scheduler._cueStates.TryGetValue(cueStateSnapshot.CueId, out var state))
            {
                throw new ArgumentException(
                    "Runtime cue snapshot contains a cue that is not present in the restored schedule.",
                    nameof(snapshot));
            }

            state.PresentedAt = cueStateSnapshot.PresentedAt?.Add(interruptionDuration);
            state.PhaseId = cueStateSnapshot.PhaseId;
            state.PhaseKind = cueStateSnapshot.PhaseKind;

            if (cueStateSnapshot.ResponseEventSequenceNumber.HasValue)
            {
                state.ResponseEvent = eventLog.Events.FirstOrDefault(runtimeEvent =>
                    runtimeEvent.SequenceNumber == cueStateSnapshot.ResponseEventSequenceNumber.Value);
                if (state.ResponseEvent is null)
                {
                    throw new ArgumentException(
                        "Runtime cue snapshot references a missing response event.",
                        nameof(snapshot));
                }
            }
        }

        return scheduler;
    }

    public void Pause()
    {
        if (_isPaused)
        {
            return;
        }

        _isPaused = true;
        _pausedAt = _clock.Now;
    }

    public void Resume()
    {
        if (!_isPaused)
        {
            return;
        }

        _elapsedPauseDuration = new RuntimeDuration(
            _elapsedPauseDuration.Value + _clock.Now.ElapsedSince(_pausedAt.GetValueOrDefault()).Value);
        _pausedAt = null;
        _isPaused = false;
    }

    public RuntimeCueAdvanceResult AdvanceToCurrentTime(
        RuntimeSessionPhaseDefinition? phase = null)
    {
        if (_isPaused)
        {
            return new RuntimeCueAdvanceResult(
                EmittedCues: Array.Empty<RuntimeScheduledCue>(),
                Events: Array.Empty<RuntimeEvent>());
        }

        var events = new List<RuntimeEvent>();
        var expiredRequiredStates = _cueStates.Values
            .Where(state => state.PresentedAt.HasValue &&
                state.ResponseEvent is null &&
                state.Cue.ResponseExpectation == RuntimeCueResponseExpectation.ResponseRequired &&
                _clock.Now.Offset > state.PresentedAt.Value.Offset + state.Cue.ResponseWindow.Value)
            .OrderBy(state => state.Cue.ScheduledAt.Offset)
            .ThenBy(state => state.Cue.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (var state in expiredRequiredStates)
        {
            var response = RuntimeCueResponse.ForCue(state.Cue.Id, "omitted");
            var responseTime = _clock.Now.ElapsedSince(state.PresentedAt!.Value);
            state.ResponseEvent = _eventLog.Append(
                RuntimeEventKind.CueResponseSubmitted,
                _clock.Now,
                state.PhaseId,
                state.PhaseKind,
                BuildResponseFacts(
                    state.Cue,
                    state.PresentedAt.Value,
                    response,
                    responseTime,
                    withinWindow: false,
                    RuntimeCueResponseOutcome.Late));
            events.Add(state.ResponseEvent);
        }

        var dueStates = _cueStates.Values
            .Where(state => !state.PresentedAt.HasValue &&
                EffectiveScheduledAt(state.Cue).Offset <= _clock.Now.Offset)
            .OrderBy(state => state.Cue.ScheduledAt.Offset)
            .ThenBy(state => state.Cue.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (var state in dueStates)
        {
            state.PresentedAt = _clock.Now;
            state.PhaseId = phase?.Id;
            state.PhaseKind = phase?.Kind;

            events.Add(_eventLog.Append(
                RuntimeEventKind.CueEmitted,
                _clock.Now,
                phase?.Id,
                phase?.Kind,
                BuildCueFacts(state.Cue, state.PresentedAt.Value)));
        }

        return new RuntimeCueAdvanceResult(
            dueStates.Select(state => state.Cue).ToArray(),
            events.AsReadOnly());
    }

    public RuntimeCueResponseResult RecordResponse(RuntimeCueResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (_isPaused)
        {
            return Rejected(RuntimeCueResponseInvalidReason.SchedulerPaused);
        }

        if (!_cueStates.TryGetValue(response.CueId, out var state))
        {
            return Rejected(RuntimeCueResponseInvalidReason.UnknownCue);
        }

        if (!state.PresentedAt.HasValue)
        {
            return Rejected(RuntimeCueResponseInvalidReason.CueNotPresented, state.Cue);
        }

        if (state.ResponseEvent is not null)
        {
            return Rejected(RuntimeCueResponseInvalidReason.CueAlreadyResponded, state.Cue);
        }

        var responseTime = _clock.Now.ElapsedSince(state.PresentedAt.Value);
        var withinWindow = _clock.Now.Offset <= state.PresentedAt.Value.Offset + state.Cue.ResponseWindow.Value;
        var outcome = EvaluateOutcome(state.Cue, response.Response, withinWindow);
        var responseEvent = _eventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            state.PhaseId,
            state.PhaseKind,
            BuildResponseFacts(state.Cue, state.PresentedAt.Value, response, responseTime, withinWindow, outcome));

        state.ResponseEvent = responseEvent;

        return new RuntimeCueResponseResult(
            IsAccepted: true,
            outcome,
            InvalidReason: null,
            state.Cue,
            responseTime,
            responseEvent);
    }

    private RuntimeInstant EffectiveScheduledAt(RuntimeScheduledCue cue)
    {
        return cue.ScheduledAt.Add(_elapsedPauseDuration);
    }

    private static RuntimeCueResponseOutcome EvaluateOutcome(
        RuntimeScheduledCue cue,
        string response,
        bool withinWindow)
    {
        if (!withinWindow)
        {
            return RuntimeCueResponseOutcome.Late;
        }

        if (cue.ResponseExpectation == RuntimeCueResponseExpectation.NoResponseExpected)
        {
            return RuntimeCueResponseOutcome.Incorrect;
        }

        return cue.ExpectedResponse is null ||
            string.Equals(cue.ExpectedResponse, response, StringComparison.Ordinal)
                ? RuntimeCueResponseOutcome.Correct
                : RuntimeCueResponseOutcome.Incorrect;
    }

    private IReadOnlyList<RuntimeEventFact> BuildCueFacts(
        RuntimeScheduledCue cue,
        RuntimeInstant presentedAt)
    {
        return
        [
            new RuntimeEventFact("generated_instance_id", Schedule.GeneratedDrillInstance.InstanceId),
            new RuntimeEventFact("content_id", Schedule.GeneratedDrillInstance.ContentIdentity.ContentId),
            new RuntimeEventFact("content_version", Schedule.GeneratedDrillInstance.ContentVersion),
            new RuntimeEventFact("cue_id", cue.Id),
            new RuntimeEventFact("cue_kind", StableCueKind(cue.Kind)),
            new RuntimeEventFact("cue_value", cue.Cue),
            new RuntimeEventFact("response_expectation", StableExpectation(cue.ResponseExpectation)),
            new RuntimeEventFact("expected_response", ExpectedResponseFact(cue)),
            new RuntimeEventFact("scheduled_at", FormatInstant(cue.ScheduledAt)),
            new RuntimeEventFact("presented_at", FormatInstant(presentedAt)),
            new RuntimeEventFact("response_deadline", FormatInstant(presentedAt.Add(cue.ResponseWindow))),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> BuildResponseFacts(
        RuntimeScheduledCue cue,
        RuntimeInstant presentedAt,
        RuntimeCueResponse response,
        RuntimeDuration responseTime,
        bool withinWindow,
        RuntimeCueResponseOutcome outcome)
    {
        return
        [
            new RuntimeEventFact("generated_instance_id", Schedule.GeneratedDrillInstance.InstanceId),
            new RuntimeEventFact("content_id", Schedule.GeneratedDrillInstance.ContentIdentity.ContentId),
            new RuntimeEventFact("content_version", Schedule.GeneratedDrillInstance.ContentVersion),
            new RuntimeEventFact("cue_id", cue.Id),
            new RuntimeEventFact("cue_kind", StableCueKind(cue.Kind)),
            new RuntimeEventFact("cue_value", cue.Cue),
            new RuntimeEventFact("response_expectation", StableExpectation(cue.ResponseExpectation)),
            new RuntimeEventFact("expected_response", ExpectedResponseFact(cue)),
            new RuntimeEventFact("response", response.Response),
            new RuntimeEventFact("scheduled_at", FormatInstant(cue.ScheduledAt)),
            new RuntimeEventFact("presented_at", FormatInstant(presentedAt)),
            new RuntimeEventFact("responded_at", FormatInstant(_clock.Now)),
            new RuntimeEventFact("response_deadline", FormatInstant(presentedAt.Add(cue.ResponseWindow))),
            new RuntimeEventFact("response_time", FormatDuration(responseTime)),
            new RuntimeEventFact("within_window", withinWindow ? "true" : "false"),
            new RuntimeEventFact("response_outcome", StableOutcome(outcome)),
        ];
    }

    private static RuntimeCueResponseResult Rejected(
        RuntimeCueResponseInvalidReason invalidReason,
        RuntimeScheduledCue? cue = null)
    {
        return new RuntimeCueResponseResult(
            IsAccepted: false,
            Outcome: null,
            invalidReason,
            cue,
            ResponseTime: null,
            Event: null);
    }

    private static string ExpectedResponseFact(RuntimeScheduledCue cue)
    {
        return cue.ResponseExpectation == RuntimeCueResponseExpectation.NoResponseExpected
            ? "withhold"
            : cue.ExpectedResponse ?? "any";
    }

    private static string StableCueKind(RuntimeCueKind kind)
    {
        return kind switch
        {
            RuntimeCueKind.FocusShift => "focus_shift",
            RuntimeCueKind.InvalidCueFilter => "invalid_cue_filter",
            RuntimeCueKind.GoNoGo => "go_no_go",
            RuntimeCueKind.Interruption => "interruption",
            RuntimeCueKind.TimedResponse => "timed_response",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown runtime cue kind."),
        };
    }

    private static string StableExpectation(RuntimeCueResponseExpectation expectation)
    {
        return expectation switch
        {
            RuntimeCueResponseExpectation.ResponseRequired => "response_required",
            RuntimeCueResponseExpectation.NoResponseExpected => "no_response_expected",
            _ => throw new ArgumentOutOfRangeException(nameof(expectation), expectation, "Unknown runtime cue expectation."),
        };
    }

    private static string StableOutcome(RuntimeCueResponseOutcome outcome)
    {
        return outcome switch
        {
            RuntimeCueResponseOutcome.Correct => "correct",
            RuntimeCueResponseOutcome.Incorrect => "incorrect",
            RuntimeCueResponseOutcome.Late => "late",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown runtime cue response outcome."),
        };
    }

    private static string FormatDuration(RuntimeDuration duration)
    {
        return duration.Value.ToString("c", CultureInfo.InvariantCulture);
    }

    private static string FormatInstant(RuntimeInstant instant)
    {
        return instant.Offset.ToString("c", CultureInfo.InvariantCulture);
    }

    private sealed class RuntimeCueState
    {
        public RuntimeCueState(RuntimeScheduledCue cue)
        {
            Cue = cue;
        }

        public RuntimeScheduledCue Cue { get; }

        public RuntimeInstant? PresentedAt { get; set; }

        public string? PhaseId { get; set; }

        public RuntimeSessionPhaseKind? PhaseKind { get; set; }

        public RuntimeEvent? ResponseEvent { get; set; }
    }
}
