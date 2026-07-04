using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum FocusHoldRuntimeProtocolInvalidReason
{
    TargetAlreadyStated,
    TargetStatementRequiredBeforeSet,
    ActiveSetAlreadyStarted,
    ActiveSetNotStarted,
    DriftAlreadyMarked,
    UnknownDrift,
    DriftAlreadyReturned,
    DistractorsNotSupportedByDrill,
    UnknownDistractor,
    DistractorAlreadyHandled,
}

public sealed class FocusHoldRuntimeProtocolOptions
{
    public FocusHoldRuntimeProtocolOptions(RuntimeDuration recoveryWindow)
    {
        if (recoveryWindow.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recoveryWindow),
                recoveryWindow,
                "Focus hold recovery window must be positive.");
        }

        RecoveryWindow = recoveryWindow;
    }

    public RuntimeDuration RecoveryWindow { get; }

    public static FocusHoldRuntimeProtocolOptions Default { get; } = new(RuntimeDuration.FromSeconds(10));
}

public sealed record FocusHoldRuntimeProtocolResult(
    bool IsAccepted,
    FocusHoldRuntimeProtocolInvalidReason? InvalidReason,
    RuntimeEvent? Event);

public sealed class FocusHoldRuntimeProtocol
{
    private readonly IRuntimeClock _clock;
    private readonly FocusHoldRuntimeProtocolOptions _options;
    private readonly Dictionary<string, FocusHoldDriftState> _drifts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FocusHoldDistractorState> _distractors = new(StringComparer.Ordinal);
    private FocusHoldTargetState? _target;
    private bool _activeSetStarted;
    private bool _setCompleted;
    private int _targetSubstitutionCount;
    private int _lateReturnCount;
    private int _distractorsIgnored;
    private int _distractorResponses;

    private FocusHoldRuntimeProtocol(
        IRuntimeClock clock,
        RuntimeEventLog eventLog,
        FocusHoldRuntimeProtocolOptions options)
    {
        _clock = clock;
        EventLog = eventLog;
        _options = options;
    }

    public RuntimeEventLog EventLog { get; }

    public RuntimeSessionDefinition SessionDefinition => EventLog.SessionDefinition;

    public static FocusHoldRuntimeProtocol Start(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        IRuntimeClock clock,
        FocusHoldRuntimeProtocolOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Focus hold runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(clock);
        ValidateFocusHoldSession(sessionDefinition);

        return new FocusHoldRuntimeProtocol(
            clock,
            RuntimeEventLog.Start(sessionId, sessionDefinition, clock.Now),
            options ?? FocusHoldRuntimeProtocolOptions.Default);
    }

    public FocusHoldRuntimeProtocolResult StateTarget(
        string targetId,
        string targetStatement)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("Focus hold target id is required.", nameof(targetId));
        }

        if (string.IsNullOrWhiteSpace(targetStatement))
        {
            throw new ArgumentException("Focus hold target statement is required.", nameof(targetStatement));
        }

        if (_target is not null)
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.TargetAlreadyStated);
        }

        _target = new FocusHoldTargetState(targetId, targetStatement);
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..TargetFacts(),
                new RuntimeEventFact("target_statement_timing", "before_set"),
                new RuntimeEventFact("drift_marking_required", "true"),
                new RuntimeEventFact("target_substitution_allowed", "false"),
                new RuntimeEventFact("honesty_constraint", "target_stated_before_set"),
                new RuntimeEventFact("honesty_constraint", "every_drift_marked"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusHoldRuntimeProtocolResult StartActiveSet()
    {
        if (_target is null)
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.TargetStatementRequiredBeforeSet);
        }

        if (_activeSetStarted)
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.ActiveSetAlreadyStarted);
        }

        _activeSetStarted = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..TargetFacts(),
                new RuntimeEventFact("target_statement_confirmed_before_set", "true"),
                new RuntimeEventFact("drift_marking_required", "true"),
                new RuntimeEventFact("target_substitution_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusHoldRuntimeProtocolResult MarkDrift(string driftId)
    {
        if (string.IsNullOrWhiteSpace(driftId))
        {
            throw new ArgumentException("Focus hold drift id is required.", nameof(driftId));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (_drifts.ContainsKey(driftId))
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.DriftAlreadyMarked);
        }

        _drifts.Add(driftId, new FocusHoldDriftState(driftId, _clock.Now));
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.DriftMarked,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..TargetFacts(),
                new RuntimeEventFact("drift_id", driftId),
                new RuntimeEventFact("drift_marked", "true"),
                new RuntimeEventFact("drift_marking_required", "true"),
                new RuntimeEventFact("recovery_window", FormatDuration(_options.RecoveryWindow)),
                new RuntimeEventFact("target_substitution_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusHoldRuntimeProtocolResult RecordReturn(string driftId)
    {
        if (string.IsNullOrWhiteSpace(driftId))
        {
            throw new ArgumentException("Focus hold drift id is required.", nameof(driftId));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (!_drifts.TryGetValue(driftId, out var drift))
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.UnknownDrift);
        }

        if (drift.ReturnedAt.HasValue)
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.DriftAlreadyReturned);
        }

        var recoveryTime = _clock.Now.ElapsedSince(drift.MarkedAt);
        var withinWindow = recoveryTime.Value <= _options.RecoveryWindow.Value;
        if (!withinWindow)
        {
            _lateReturnCount++;
        }

        drift.ReturnedAt = _clock.Now;
        drift.RecoveryTime = recoveryTime;

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.RecoveryCompleted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..TargetFacts(),
                new RuntimeEventFact("drift_id", driftId),
                new RuntimeEventFact("recovery_time", FormatDuration(recoveryTime)),
                new RuntimeEventFact("recovery_window", FormatDuration(_options.RecoveryWindow)),
                new RuntimeEventFact("return_within_window", withinWindow ? "true" : "false"),
                new RuntimeEventFact("return_timing_outcome", withinWindow ? "within_window" : "late"),
                new RuntimeEventFact("target_substitution_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusHoldRuntimeProtocolResult RecordTargetSubstitution(string substituteTarget)
    {
        if (string.IsNullOrWhiteSpace(substituteTarget))
        {
            throw new ArgumentException("Focus hold substitute target is required.", nameof(substituteTarget));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _targetSubstitutionCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..TargetFacts(),
                new RuntimeEventFact("error_kind", "target_substitution"),
                new RuntimeEventFact("failed_item_list", $"target substitution: {substituteTarget}"),
                new RuntimeEventFact("failed_constraint", "no_target_substitution"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
                new RuntimeEventFact("substitute_target", substituteTarget),
                new RuntimeEventFact("target_substitution_detected", "true"),
                new RuntimeEventFact("target_substitution_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusHoldRuntimeProtocolResult PresentDistractor(
        string distractorId,
        string prompt,
        RuntimeDuration responseWindow)
    {
        if (string.IsNullOrWhiteSpace(distractorId))
        {
            throw new ArgumentException("Focus hold distractor id is required.", nameof(distractorId));
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Focus hold distractor prompt is required.", nameof(prompt));
        }

        if (responseWindow.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseWindow),
                responseWindow,
                "Focus hold distractor response window must be positive.");
        }

        if (SessionDefinition.Drill != DrillId.FH2DistractorHold)
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.DistractorsNotSupportedByDrill);
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (_distractors.ContainsKey(distractorId))
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.DistractorAlreadyHandled);
        }

        var state = new FocusHoldDistractorState(distractorId, prompt, _clock.Now, responseWindow);
        _distractors.Add(distractorId, state);

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueEmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..TargetFacts(),
                ..DistractorFacts(state),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusHoldRuntimeProtocolResult RecordDistractorIgnored(string distractorId)
    {
        var stateResult = GetUnhandledDistractor(distractorId, out var state);
        if (stateResult is not null)
        {
            return stateResult;
        }

        _distractorsIgnored++;
        state!.Handled = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..TargetFacts(),
                ..DistractorResponseFacts(state, "withheld", "ignored", "correct"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusHoldRuntimeProtocolResult RecordDistractorResponse(
        string distractorId,
        string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Focus hold distractor response is required.", nameof(response));
        }

        var stateResult = GetUnhandledDistractor(distractorId, out var state);
        if (stateResult is not null)
        {
            return stateResult;
        }

        _distractorResponses++;
        state!.Handled = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..TargetFacts(),
                ..DistractorResponseFacts(state, response, "responded", "incorrect"),
                new RuntimeEventFact("failed_constraint", "do_not_respond_to_distractor"),
                new RuntimeEventFact("failed_item_list", $"responded to distractor {distractorId}: {response}"),
                new RuntimeEventFact("error_kind", "commission"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusHoldRuntimeProtocolResult CompleteSet(string setId)
    {
        if (string.IsNullOrWhiteSpace(setId))
        {
            throw new ArgumentException("Focus hold set id is required.", nameof(setId));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _setCompleted = true;
        var returnsRecorded = _drifts.Values.Count(drift => drift.ReturnedAt.HasValue);
        var maxReturn = _drifts.Values
            .Where(drift => drift.RecoveryTime.HasValue)
            .Select(drift => drift.RecoveryTime!.Value.Value)
            .DefaultIfEmpty(TimeSpan.Zero)
            .Max();
        var outputSample =
            $"set {setId}: target={_target!.Statement}; marked_drifts={_drifts.Count}; " +
            $"returns_recorded={returnsRecorded}; late_returns={_lateReturnCount}; " +
            $"target_substitution_count={_targetSubstitutionCount}; " +
            $"distractors_ignored={_distractorsIgnored}; distractor_response_count={_distractorResponses}";
        var score =
            $"marked_drifts={_drifts.Count}; returns_recorded={returnsRecorded}; " +
            $"max_return={FormatDuration(new RuntimeDuration(maxReturn))}; late_returns={_lateReturnCount}; " +
            $"target_substitution_count={_targetSubstitutionCount}; distractors_presented={_distractors.Count}; " +
            $"distractors_ignored={_distractorsIgnored}; distractor_response_count={_distractorResponses}";

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..TargetFacts(),
                new RuntimeEventFact("set_id", setId),
                new RuntimeEventFact("output_sample", outputSample),
                new RuntimeEventFact("score", score),
                new RuntimeEventFact("marked_drift_count", _drifts.Count.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("returns_recorded", returnsRecorded.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("late_return_count", _lateReturnCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("target_substitution_count", _targetSubstitutionCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("distractors_presented", _distractors.Count.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("distractors_ignored", _distractorsIgnored.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("distractor_response_count", _distractorResponses.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("target_substitution_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    private FocusHoldRuntimeProtocolResult? RequireActiveSet()
    {
        if (_target is null)
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.TargetStatementRequiredBeforeSet);
        }

        if (!_activeSetStarted || _setCompleted)
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.ActiveSetNotStarted);
        }

        return null;
    }

    private FocusHoldRuntimeProtocolResult? GetUnhandledDistractor(
        string distractorId,
        out FocusHoldDistractorState? state)
    {
        if (string.IsNullOrWhiteSpace(distractorId))
        {
            throw new ArgumentException("Focus hold distractor id is required.", nameof(distractorId));
        }

        state = null;
        if (SessionDefinition.Drill != DrillId.FH2DistractorHold)
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.DistractorsNotSupportedByDrill);
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (!_distractors.TryGetValue(distractorId, out state))
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.UnknownDistractor);
        }

        if (state.Handled)
        {
            return Rejected(FocusHoldRuntimeProtocolInvalidReason.DistractorAlreadyHandled);
        }

        return null;
    }

    private IReadOnlyList<RuntimeEventFact> ProtocolFacts()
    {
        return
        [
            new RuntimeEventFact("protocol_branch", "fh"),
            new RuntimeEventFact("protocol_drill", StableDrill(SessionDefinition.Drill)),
            new RuntimeEventFact("branch", SessionDefinition.Branch.ToString()),
            new RuntimeEventFact("level", SessionDefinition.Level.ToString()),
            new RuntimeEventFact("drill", StableDrill(SessionDefinition.Drill)),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> TargetFacts()
    {
        if (_target is null)
        {
            return [];
        }

        return
        [
            new RuntimeEventFact("target_id", _target.Id),
            new RuntimeEventFact("target_statement", _target.Statement),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> DistractorFacts(FocusHoldDistractorState state)
    {
        return
        [
            new RuntimeEventFact("distractor_id", state.Id),
            new RuntimeEventFact("cue_id", state.Id),
            new RuntimeEventFact("cue_kind", "distractor"),
            new RuntimeEventFact("cue_value", state.Prompt),
            new RuntimeEventFact("response_expectation", "no_response_expected"),
            new RuntimeEventFact("expected_response", "withhold"),
            new RuntimeEventFact("presented_at", FormatInstant(state.PresentedAt)),
            new RuntimeEventFact("response_deadline", FormatInstant(state.ResponseDeadline)),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> DistractorResponseFacts(
        FocusHoldDistractorState state,
        string response,
        string distractorResponse,
        string outcome)
    {
        var responseTime = _clock.Now.ElapsedSince(state.PresentedAt);
        var withinWindow = _clock.Now.Offset <= state.ResponseDeadline.Offset;

        return
        [
            ..DistractorFacts(state),
            new RuntimeEventFact("response", response),
            new RuntimeEventFact("distractor_response", distractorResponse),
            new RuntimeEventFact("responded_at", FormatInstant(_clock.Now)),
            new RuntimeEventFact("response_time", FormatDuration(responseTime)),
            new RuntimeEventFact("within_window", withinWindow ? "true" : "false"),
            new RuntimeEventFact("response_outcome", outcome),
        ];
    }

    private static FocusHoldRuntimeProtocolResult Accepted(RuntimeEvent runtimeEvent)
    {
        return new FocusHoldRuntimeProtocolResult(true, InvalidReason: null, runtimeEvent);
    }

    private static FocusHoldRuntimeProtocolResult Rejected(FocusHoldRuntimeProtocolInvalidReason invalidReason)
    {
        return new FocusHoldRuntimeProtocolResult(false, invalidReason, Event: null);
    }

    private static void ValidateFocusHoldSession(RuntimeSessionDefinition sessionDefinition)
    {
        if (sessionDefinition.Branch != BranchCode.FH)
        {
            throw new ArgumentException("Focus hold runtime protocol requires an FH session.", nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill is not DrillId.FH1TargetHold and not DrillId.FH2DistractorHold)
        {
            throw new ArgumentException(
                "Focus hold runtime protocol supports only FH-1 Target Hold and FH-2 Distractor Hold.",
                nameof(sessionDefinition));
        }

        if (!ContainsConstraint(sessionDefinition, "target is stated") ||
            !ContainsConstraint(sessionDefinition, "drift is marked"))
        {
            throw new ArgumentException(
                "Focus hold runtime sessions must include the target statement and drift marking constraints.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.FH2DistractorHold &&
            !ContainsConstraint(sessionDefinition, "respond to distractor"))
        {
            throw new ArgumentException(
                "FH-2 runtime sessions must include the no-response distractor constraint.",
                nameof(sessionDefinition));
        }
    }

    private static bool ContainsConstraint(RuntimeSessionDefinition sessionDefinition, string text)
    {
        return sessionDefinition.CriticalConstraints.Any(constraint =>
            constraint.Description.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static string StableDrill(DrillId drill)
    {
        return drill switch
        {
            DrillId.FH1TargetHold => "fh_1_target_hold",
            DrillId.FH2DistractorHold => "fh_2_distractor_hold",
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unsupported focus hold drill."),
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

    private sealed record FocusHoldTargetState(string Id, string Statement);

    private sealed class FocusHoldDriftState
    {
        public FocusHoldDriftState(string id, RuntimeInstant markedAt)
        {
            Id = id;
            MarkedAt = markedAt;
        }

        public string Id { get; }

        public RuntimeInstant MarkedAt { get; }

        public RuntimeInstant? ReturnedAt { get; set; }

        public RuntimeDuration? RecoveryTime { get; set; }
    }

    private sealed class FocusHoldDistractorState
    {
        public FocusHoldDistractorState(
            string id,
            string prompt,
            RuntimeInstant presentedAt,
            RuntimeDuration responseWindow)
        {
            Id = id;
            Prompt = prompt;
            PresentedAt = presentedAt;
            ResponseWindow = responseWindow;
        }

        public string Id { get; }

        public string Prompt { get; }

        public RuntimeInstant PresentedAt { get; }

        public RuntimeDuration ResponseWindow { get; }

        public RuntimeInstant ResponseDeadline => PresentedAt.Add(ResponseWindow);

        public bool Handled { get; set; }
    }
}
