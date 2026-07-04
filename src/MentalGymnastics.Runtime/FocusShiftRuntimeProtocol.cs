using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum FocusShiftRuntimeProtocolInvalidReason
{
    TargetSequenceAlreadyStated,
    TargetSequenceRequiredBeforeSet,
    TargetSequenceRequiresAtLeastTwoTargets,
    DuplicateTargetId,
    ActiveSetAlreadyStarted,
    ActiveSetNotStarted,
    UnknownTarget,
    UnknownCue,
    CueAlreadyHandled,
    DuplicateCueId,
    InvalidCuesNotSupportedByDrill,
    CueKindMismatch,
}

public sealed class FocusShiftRuntimeTarget
{
    public FocusShiftRuntimeTarget(string id, string description)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Focus shift target id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Focus shift target description is required.", nameof(description));
        }

        Id = id;
        Description = description;
    }

    public string Id { get; }

    public string Description { get; }
}

public sealed record FocusShiftRuntimeProtocolResult(
    bool IsAccepted,
    FocusShiftRuntimeProtocolInvalidReason? InvalidReason,
    RuntimeEvent? Event);

public sealed class FocusShiftRuntimeProtocol
{
    private readonly IRuntimeClock _clock;
    private readonly Dictionary<string, FocusShiftRuntimeTarget> _targets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FocusShiftCueState> _cues = new(StringComparer.Ordinal);
    private bool _activeSetStarted;
    private bool _setCompleted;
    private int _validCueCount;
    private int _validCorrectCount;
    private int _validIncorrectCount;
    private int _validMissedCount;
    private int _invalidCueCount;
    private int _invalidIgnoredCount;
    private int _invalidCueSwitchCount;
    private int _anticipatorySwitchCount;

    private FocusShiftRuntimeProtocol(
        IRuntimeClock clock,
        RuntimeEventLog eventLog)
    {
        _clock = clock;
        EventLog = eventLog;
    }

    public RuntimeEventLog EventLog { get; }

    public RuntimeSessionDefinition SessionDefinition => EventLog.SessionDefinition;

    public IReadOnlyList<FocusShiftRuntimeTarget> TargetSequence => _targets.Values.ToArray();

    public static FocusShiftRuntimeProtocol Start(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        IRuntimeClock clock)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Focus shift runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(clock);
        ValidateFocusShiftSession(sessionDefinition);

        return new FocusShiftRuntimeProtocol(
            clock,
            RuntimeEventLog.Start(sessionId, sessionDefinition, clock.Now));
    }

    public FocusShiftRuntimeProtocolResult StateTargetSequence(
        IEnumerable<FocusShiftRuntimeTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        if (_targets.Count > 0)
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.TargetSequenceAlreadyStated);
        }

        var targetArray = targets.ToArray();
        foreach (var target in targetArray)
        {
            ArgumentNullException.ThrowIfNull(target);
        }

        if (targetArray.Length < 2)
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.TargetSequenceRequiresAtLeastTwoTargets);
        }

        var duplicateTarget = targetArray
            .GroupBy(target => target.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTarget is not null)
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.DuplicateTargetId);
        }

        foreach (var target in targetArray)
        {
            _targets.Add(target.Id, target);
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..TargetSequenceFacts(),
                new RuntimeEventFact("target_sequence_stated_before_set", "true"),
                new RuntimeEventFact("cue_obedience_required", "true"),
                new RuntimeEventFact("valid_cue_only", "true"),
                new RuntimeEventFact("anticipatory_switch_allowed", "false"),
                new RuntimeEventFact("honesty_constraint", "switch_only_on_valid_cue"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusShiftRuntimeProtocolResult StartActiveSet()
    {
        if (_targets.Count == 0)
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.TargetSequenceRequiredBeforeSet);
        }

        if (_activeSetStarted)
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.ActiveSetAlreadyStarted);
        }

        _activeSetStarted = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..TargetSequenceFacts(),
                new RuntimeEventFact("target_sequence_confirmed_before_set", "true"),
                new RuntimeEventFact("cue_obedience_required", "true"),
                new RuntimeEventFact("valid_cue_only", "true"),
                new RuntimeEventFact("anticipatory_switch_allowed", "false"),
                new RuntimeEventFact("invalid_cue_filtering_required", SupportsInvalidCueFiltering() ? "true" : "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusShiftRuntimeProtocolResult PresentValidCue(
        string cueId,
        string cueValue,
        string expectedTargetId,
        RuntimeDuration responseWindow)
    {
        if (string.IsNullOrWhiteSpace(expectedTargetId))
        {
            throw new ArgumentException("Expected focus shift target id is required.", nameof(expectedTargetId));
        }

        if (!_targets.ContainsKey(expectedTargetId))
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.UnknownTarget);
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        var cueValidation = ValidateNewCue(cueId, cueValue, responseWindow);
        if (cueValidation is not null)
        {
            return cueValidation;
        }

        var state = new FocusShiftCueState(
            cueId,
            FocusShiftCueStateKind.Valid,
            cueValue,
            _clock.Now,
            responseWindow,
            expectedTargetId,
            sequencePosition: _validCueCount + 1);
        _validCueCount++;
        _cues.Add(cueId, state);

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueEmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..CueFacts(state),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusShiftRuntimeProtocolResult RecordCueSwitch(
        string cueId,
        string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("Focus shift response target id is required.", nameof(targetId));
        }

        if (!_targets.ContainsKey(targetId))
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.UnknownTarget);
        }

        var stateResult = GetUnhandledCue(cueId, FocusShiftCueStateKind.Valid, out var state);
        if (stateResult is not null)
        {
            return stateResult;
        }

        var responseTime = _clock.Now.ElapsedSince(state!.PresentedAt);
        var withinWindow = _clock.Now.Offset <= state.ResponseDeadline.Offset;
        var isCorrect = withinWindow && string.Equals(state.ExpectedTargetId, targetId, StringComparison.Ordinal);
        var responseOutcome = !withinWindow ? "late" : isCorrect ? "correct" : "incorrect";

        state.Handled = true;
        if (isCorrect)
        {
            _validCorrectCount++;
        }
        else
        {
            _validIncorrectCount++;
        }

        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.AddRange(CueResponseFacts(state, targetId, responseOutcome, responseTime, withinWindow));
        facts.Add(new RuntimeEventFact(
            "sequence_position",
            state.SequencePosition.GetValueOrDefault().ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("sequence_accuracy_delta", isCorrect ? "correct" : "incorrect"));

        if (!isCorrect)
        {
            facts.Add(new RuntimeEventFact(
                "failed_item_list",
                $"cue {cueId}: expected {state.ExpectedTargetId}, got {targetId}"));
            facts.Add(new RuntimeEventFact("failed_constraint", "sequence_accuracy"));
            facts.Add(new RuntimeEventFact("failure_type_candidate", "technical_failure"));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);

        return Accepted(runtimeEvent);
    }

    public FocusShiftRuntimeProtocolResult RecordMissedCue(string cueId)
    {
        var stateResult = GetUnhandledCue(cueId, FocusShiftCueStateKind.Valid, out var state);
        if (stateResult is not null)
        {
            return stateResult;
        }

        state!.Handled = true;
        _validMissedCount++;
        _validIncorrectCount++;

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..CueFacts(state),
                new RuntimeEventFact("error_kind", "omission"),
                new RuntimeEventFact("failed_item_list", $"missed valid cue {cueId}"),
                new RuntimeEventFact("failed_constraint", "valid_cue_handling"),
                new RuntimeEventFact("failure_type_candidate", "technical_failure"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusShiftRuntimeProtocolResult RecordAnticipatorySwitch(
        string targetId,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("Focus shift anticipatory target id is required.", nameof(targetId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Focus shift anticipatory switch reason is required.", nameof(reason));
        }

        if (!_targets.ContainsKey(targetId))
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.UnknownTarget);
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _anticipatorySwitchCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("target_id", targetId),
                new RuntimeEventFact("target_description", _targets[targetId].Description),
                new RuntimeEventFact("error_kind", "premature_response"),
                new RuntimeEventFact("failed_item_list", $"anticipatory switch to {targetId}: {reason}"),
                new RuntimeEventFact("failed_constraint", "switch_only_on_valid_cue"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
                new RuntimeEventFact("anticipatory_switch_count", _anticipatorySwitchCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("anticipatory_switch_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusShiftRuntimeProtocolResult PresentInvalidCue(
        string cueId,
        string cueValue,
        RuntimeDuration responseWindow)
    {
        if (!SupportsInvalidCueFiltering())
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.InvalidCuesNotSupportedByDrill);
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        var cueValidation = ValidateNewCue(cueId, cueValue, responseWindow);
        if (cueValidation is not null)
        {
            return cueValidation;
        }

        var state = new FocusShiftCueState(
            cueId,
            FocusShiftCueStateKind.Invalid,
            cueValue,
            _clock.Now,
            responseWindow,
            expectedTargetId: null,
            sequencePosition: null);
        _invalidCueCount++;
        _cues.Add(cueId, state);

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueEmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..CueFacts(state),
                new RuntimeEventFact("invalid_cue_must_be_ignored", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusShiftRuntimeProtocolResult RecordInvalidCueIgnored(string cueId)
    {
        var stateResult = GetUnhandledCue(cueId, FocusShiftCueStateKind.Invalid, out var state);
        if (stateResult is not null)
        {
            return stateResult;
        }

        state!.Handled = true;
        _invalidIgnoredCount++;
        var responseTime = _clock.Now.ElapsedSince(state.PresentedAt);
        var withinWindow = _clock.Now.Offset <= state.ResponseDeadline.Offset;

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..CueResponseFacts(state, "withheld", "correct", responseTime, withinWindow),
                new RuntimeEventFact("invalid_cue_result", "ignored"),
                new RuntimeEventFact("invalid_cue_must_be_ignored", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusShiftRuntimeProtocolResult RecordInvalidCueSwitch(
        string cueId,
        string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("Focus shift invalid cue response target id is required.", nameof(targetId));
        }

        if (!_targets.ContainsKey(targetId))
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.UnknownTarget);
        }

        var stateResult = GetUnhandledCue(cueId, FocusShiftCueStateKind.Invalid, out var state);
        if (stateResult is not null)
        {
            return stateResult;
        }

        state!.Handled = true;
        _invalidCueSwitchCount++;
        var responseTime = _clock.Now.ElapsedSince(state.PresentedAt);
        var withinWindow = _clock.Now.Offset <= state.ResponseDeadline.Offset;

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..CueResponseFacts(state, targetId, "incorrect", responseTime, withinWindow),
                new RuntimeEventFact("invalid_cue_result", "switched"),
                new RuntimeEventFact("failed_constraint", "invalid_cue_must_not_trigger_switch"),
                new RuntimeEventFact("failed_item_list", $"invalid cue triggered switch to {targetId}"),
                new RuntimeEventFact("error_kind", "commission"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
                new RuntimeEventFact("invalid_cue_must_be_ignored", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public FocusShiftRuntimeProtocolResult CompleteSet(string setId)
    {
        if (string.IsNullOrWhiteSpace(setId))
        {
            throw new ArgumentException("Focus shift set id is required.", nameof(setId));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _setCompleted = true;
        var sequenceAccuracy = Ratio(_validCorrectCount, _validCueCount);
        var invalidCueFilterAccuracy = Ratio(_invalidIgnoredCount, _invalidCueCount);
        var outputSample =
            $"set {setId}: valid_cues={_validCueCount}; sequence_accuracy={sequenceAccuracy}; " +
            $"anticipatory_switch_count={_anticipatorySwitchCount}; invalid_cues={_invalidCueCount}; " +
            $"invalid_cue_filter_accuracy={invalidCueFilterAccuracy}";
        var score =
            $"sequence_accuracy={sequenceAccuracy}; valid_sequence_accuracy={sequenceAccuracy}; " +
            $"valid_cues={_validCueCount}; correct_valid_responses={_validCorrectCount}; " +
            $"incorrect_valid_responses={_validIncorrectCount}; missed_valid_cues={_validMissedCount}; " +
            $"anticipatory_switch_count={_anticipatorySwitchCount}; invalid_cues={_invalidCueCount}; " +
            $"invalid_cues_ignored={_invalidIgnoredCount}; invalid_cue_switch_count={_invalidCueSwitchCount}; " +
            $"invalid_cue_filter_accuracy={invalidCueFilterAccuracy}";

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("set_id", setId),
                new RuntimeEventFact("output_sample", outputSample),
                new RuntimeEventFact("score", score),
                new RuntimeEventFact("valid_cue_count", _validCueCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("correct_valid_response_count", _validCorrectCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("incorrect_valid_response_count", _validIncorrectCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("missed_valid_cue_count", _validMissedCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("sequence_accuracy", sequenceAccuracy),
                new RuntimeEventFact("valid_sequence_accuracy", sequenceAccuracy),
                new RuntimeEventFact("anticipatory_switch_count", _anticipatorySwitchCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("invalid_cue_count", _invalidCueCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("invalid_cues_ignored", _invalidIgnoredCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("invalid_cue_switch_count", _invalidCueSwitchCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("invalid_cue_filter_accuracy", invalidCueFilterAccuracy),
                new RuntimeEventFact("cue_obedience_required", "true"),
                new RuntimeEventFact("anticipatory_switch_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    private FocusShiftRuntimeProtocolResult? RequireActiveSet()
    {
        if (_targets.Count == 0)
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.TargetSequenceRequiredBeforeSet);
        }

        if (!_activeSetStarted || _setCompleted)
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.ActiveSetNotStarted);
        }

        return null;
    }

    private FocusShiftRuntimeProtocolResult? ValidateNewCue(
        string cueId,
        string cueValue,
        RuntimeDuration responseWindow)
    {
        if (string.IsNullOrWhiteSpace(cueId))
        {
            throw new ArgumentException("Focus shift cue id is required.", nameof(cueId));
        }

        if (string.IsNullOrWhiteSpace(cueValue))
        {
            throw new ArgumentException("Focus shift cue value is required.", nameof(cueValue));
        }

        if (responseWindow.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseWindow),
                responseWindow,
                "Focus shift cue response window must be positive.");
        }

        if (_cues.ContainsKey(cueId))
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.DuplicateCueId);
        }

        return null;
    }

    private FocusShiftRuntimeProtocolResult? GetUnhandledCue(
        string cueId,
        FocusShiftCueStateKind requiredKind,
        out FocusShiftCueState? state)
    {
        if (string.IsNullOrWhiteSpace(cueId))
        {
            throw new ArgumentException("Focus shift cue id is required.", nameof(cueId));
        }

        state = null;
        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (!_cues.TryGetValue(cueId, out state))
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.UnknownCue);
        }

        if (state.Kind != requiredKind)
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.CueKindMismatch);
        }

        if (state.Handled)
        {
            return Rejected(FocusShiftRuntimeProtocolInvalidReason.CueAlreadyHandled);
        }

        return null;
    }

    private bool SupportsInvalidCueFiltering()
    {
        return SessionDefinition.Drill == DrillId.FS2InvalidCueFilter;
    }

    private IReadOnlyList<RuntimeEventFact> ProtocolFacts()
    {
        return
        [
            new RuntimeEventFact("protocol_branch", "fs"),
            new RuntimeEventFact("protocol_drill", StableDrill(SessionDefinition.Drill)),
            new RuntimeEventFact("branch", SessionDefinition.Branch.ToString()),
            new RuntimeEventFact("level", SessionDefinition.Level.ToString()),
            new RuntimeEventFact("drill", StableDrill(SessionDefinition.Drill)),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> TargetSequenceFacts()
    {
        var facts = new List<RuntimeEventFact>
        {
            new("target_count", _targets.Count.ToString(CultureInfo.InvariantCulture)),
            new("target_sequence", string.Join(",", _targets.Keys)),
        };

        foreach (var target in _targets.Values)
        {
            facts.Add(new RuntimeEventFact("target_id", target.Id));
            facts.Add(new RuntimeEventFact("target_description", target.Description));
        }

        return facts.AsReadOnly();
    }

    private IReadOnlyList<RuntimeEventFact> CueFacts(FocusShiftCueState state)
    {
        var facts = new List<RuntimeEventFact>
        {
            new("cue_id", state.Id),
            new("cue_kind", state.Kind == FocusShiftCueStateKind.Valid ? "focus_shift" : "invalid_cue_filter"),
            new("cue_value", state.Value),
            new(
                "response_expectation",
                state.Kind == FocusShiftCueStateKind.Valid ? "response_required" : "no_response_expected"),
            new(
                "expected_response",
                state.Kind == FocusShiftCueStateKind.Valid ? state.ExpectedTargetId! : "withhold"),
            new("presented_at", FormatInstant(state.PresentedAt)),
            new("response_deadline", FormatInstant(state.ResponseDeadline)),
        };

        if (state.ExpectedTargetId is not null)
        {
            facts.Add(new RuntimeEventFact("expected_target_id", state.ExpectedTargetId));
            facts.Add(new RuntimeEventFact("expected_target_description", _targets[state.ExpectedTargetId].Description));
        }

        if (state.SequencePosition.HasValue)
        {
            facts.Add(new RuntimeEventFact(
                "sequence_position",
                state.SequencePosition.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return facts.AsReadOnly();
    }

    private IReadOnlyList<RuntimeEventFact> CueResponseFacts(
        FocusShiftCueState state,
        string response,
        string outcome,
        RuntimeDuration responseTime,
        bool withinWindow)
    {
        return
        [
            ..CueFacts(state),
            new RuntimeEventFact("response", response),
            new RuntimeEventFact("response_target_id", response),
            new RuntimeEventFact("responded_at", FormatInstant(_clock.Now)),
            new RuntimeEventFact("response_time", FormatDuration(responseTime)),
            new RuntimeEventFact("within_window", withinWindow ? "true" : "false"),
            new RuntimeEventFact("response_outcome", outcome),
        ];
    }

    private static FocusShiftRuntimeProtocolResult Accepted(RuntimeEvent runtimeEvent)
    {
        return new FocusShiftRuntimeProtocolResult(true, InvalidReason: null, runtimeEvent);
    }

    private static FocusShiftRuntimeProtocolResult Rejected(FocusShiftRuntimeProtocolInvalidReason invalidReason)
    {
        return new FocusShiftRuntimeProtocolResult(false, invalidReason, Event: null);
    }

    private static void ValidateFocusShiftSession(RuntimeSessionDefinition sessionDefinition)
    {
        if (sessionDefinition.Branch != BranchCode.FS)
        {
            throw new ArgumentException("Focus shift runtime protocol requires an FS session.", nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill is not DrillId.FS1CueSwitch and not DrillId.FS2InvalidCueFilter)
        {
            throw new ArgumentException(
                "Focus shift runtime protocol supports only FS-1 Cue Switch and FS-2 Invalid Cue Filter.",
                nameof(sessionDefinition));
        }

        if (!ContainsConstraint(sessionDefinition, "valid cue"))
        {
            throw new ArgumentException(
                "Focus shift runtime sessions must include the valid cue obedience constraint.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.FS2InvalidCueFilter &&
            !ContainsConstraint(sessionDefinition, "invalid cues"))
        {
            throw new ArgumentException(
                "FS-2 runtime sessions must include the invalid cue filtering constraint.",
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
            DrillId.FS1CueSwitch => "fs_1_cue_switch",
            DrillId.FS2InvalidCueFilter => "fs_2_invalid_cue_filter",
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unsupported focus shift drill."),
        };
    }

    private static string Ratio(int numerator, int denominator)
    {
        return $"{numerator.ToString(CultureInfo.InvariantCulture)}/{denominator.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatDuration(RuntimeDuration duration)
    {
        return duration.Value.ToString("c", CultureInfo.InvariantCulture);
    }

    private static string FormatInstant(RuntimeInstant instant)
    {
        return instant.Offset.ToString("c", CultureInfo.InvariantCulture);
    }

    private enum FocusShiftCueStateKind
    {
        Valid,
        Invalid,
    }

    private sealed class FocusShiftCueState
    {
        public FocusShiftCueState(
            string id,
            FocusShiftCueStateKind kind,
            string value,
            RuntimeInstant presentedAt,
            RuntimeDuration responseWindow,
            string? expectedTargetId,
            int? sequencePosition)
        {
            Id = id;
            Kind = kind;
            Value = value;
            PresentedAt = presentedAt;
            ResponseWindow = responseWindow;
            ExpectedTargetId = expectedTargetId;
            SequencePosition = sequencePosition;
        }

        public string Id { get; }

        public FocusShiftCueStateKind Kind { get; }

        public string Value { get; }

        public RuntimeInstant PresentedAt { get; }

        public RuntimeDuration ResponseWindow { get; }

        public RuntimeInstant ResponseDeadline => PresentedAt.Add(ResponseWindow);

        public string? ExpectedTargetId { get; }

        public int? SequencePosition { get; }

        public bool Handled { get; set; }
    }
}
