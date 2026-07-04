using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum InhibitionRuntimeProtocolInvalidReason
{
    RuleAlreadyStated,
    RuleRequiredBeforeSet,
    ExceptionsRequiredForExceptionRule,
    ExceptionRuleNotSupportedByDrill,
    DuplicateExceptionId,
    UnknownException,
    ActiveSetAlreadyStarted,
    ActiveSetNotStarted,
    SetAlreadyCompleted,
    DuplicateCueId,
    UnknownCue,
    CueAlreadyHandled,
    CueKindMismatch,
    InvalidCorrectionDistance,
}

public sealed class InhibitionRuntimeExceptionRule
{
    public InhibitionRuntimeExceptionRule(string id, string description, string expectedResponse)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Inhibition exception id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Inhibition exception description is required.", nameof(description));
        }

        if (string.IsNullOrWhiteSpace(expectedResponse))
        {
            throw new ArgumentException("Inhibition exception expected response is required.", nameof(expectedResponse));
        }

        Id = id;
        Description = description;
        ExpectedResponse = expectedResponse;
    }

    public string Id { get; }

    public string Description { get; }

    public string ExpectedResponse { get; }
}

public sealed record InhibitionRuntimeProtocolResult(
    bool IsAccepted,
    InhibitionRuntimeProtocolInvalidReason? InvalidReason,
    RuntimeEvent? Event);

public sealed class InhibitionRuntimeProtocol
{
    private readonly IRuntimeClock _clock;
    private readonly Dictionary<string, InhibitionRuntimeExceptionRule> _exceptions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InhibitionCueState> _cues = new(StringComparer.Ordinal);
    private string? _ruleStatement;
    private bool _activeSetStarted;
    private bool _setCompleted;
    private RuntimeDuration? _cuePace;
    private int _correctItemCount;
    private int _incorrectItemCount;
    private int _goCueCount;
    private int _noGoCueCount;
    private int _noGoWithheldCount;
    private int _noGoResponseCount;
    private int _exceptionCueCount;
    private int _exceptionHandledCount;
    private int _exceptionForgottenCount;
    private int _prematureResponseCount;
    private int _ruleChangeCount;
    private int _postErrorCascadeCount;
    private int _postErrorCascadeAffectedItemCount;
    private int _correctionCount;

    private InhibitionRuntimeProtocol(
        IRuntimeClock clock,
        RuntimeEventLog eventLog)
    {
        _clock = clock;
        EventLog = eventLog;
    }

    public RuntimeEventLog EventLog { get; }

    public RuntimeSessionDefinition SessionDefinition => EventLog.SessionDefinition;

    public IReadOnlyList<InhibitionRuntimeExceptionRule> Exceptions => _exceptions.Values.ToArray();

    public static InhibitionRuntimeProtocol Start(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        IRuntimeClock clock)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Inhibition runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(clock);
        ValidateInhibitionSession(sessionDefinition);

        return new InhibitionRuntimeProtocol(
            clock,
            RuntimeEventLog.Start(sessionId, sessionDefinition, clock.Now));
    }

    public InhibitionRuntimeProtocolResult StateRule(
        string ruleStatement,
        IEnumerable<InhibitionRuntimeExceptionRule>? exceptions = null)
    {
        if (string.IsNullOrWhiteSpace(ruleStatement))
        {
            throw new ArgumentException("Inhibition rule statement is required.", nameof(ruleStatement));
        }

        if (_ruleStatement is not null)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.RuleAlreadyStated);
        }

        var exceptionArray = (exceptions ?? Array.Empty<InhibitionRuntimeExceptionRule>()).ToArray();
        foreach (var exception in exceptionArray)
        {
            ArgumentNullException.ThrowIfNull(exception);
        }

        if (exceptionArray.Length > 0 && !SupportsExceptionRule())
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.ExceptionRuleNotSupportedByDrill);
        }

        if (SupportsExceptionRule() && exceptionArray.Length == 0)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.ExceptionsRequiredForExceptionRule);
        }

        var duplicateException = exceptionArray
            .GroupBy(exception => exception.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateException is not null)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.DuplicateExceptionId);
        }

        _ruleStatement = ruleStatement;
        foreach (var exception in exceptionArray)
        {
            _exceptions.Add(exception.Id, exception);
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..RuleFacts(),
                new RuntimeEventFact("rule_stated_before_set", "true"),
                new RuntimeEventFact("premature_response_fails_item", "true"),
                new RuntimeEventFact("honesty_constraint", "rule_stated_before_set"),
                new RuntimeEventFact("honesty_constraint", "premature_response_fails_item"),
            ]);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult StartActiveSet(RuntimeDuration cuePace)
    {
        if (cuePace.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cuePace),
                cuePace,
                "Inhibition cue pace must be positive.");
        }

        if (_ruleStatement is null)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.RuleRequiredBeforeSet);
        }

        if (_activeSetStarted)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.ActiveSetAlreadyStarted);
        }

        _activeSetStarted = true;
        _cuePace = cuePace;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..RuleFacts(),
                new RuntimeEventFact("cue_pace", FormatDuration(cuePace)),
                new RuntimeEventFact("rule_confirmed_before_set", "true"),
                new RuntimeEventFact("premature_response_fails_item", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult PresentGoCue(
        string cueId,
        string cueValue,
        string expectedResponse,
        RuntimeDuration responseWindow)
    {
        if (string.IsNullOrWhiteSpace(expectedResponse))
        {
            throw new ArgumentException("Inhibition go cue expected response is required.", nameof(expectedResponse));
        }

        var validation = ValidateCuePresentation(cueId, cueValue, responseWindow);
        if (validation is not null)
        {
            return validation;
        }

        var state = new InhibitionCueState(
            cueId,
            InhibitionCueKind.Go,
            cueValue,
            _clock.Now,
            responseWindow,
            expectedResponse,
            exceptionId: null);
        _goCueCount++;
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

    public InhibitionRuntimeProtocolResult PresentNoGoCue(
        string cueId,
        string cueValue,
        RuntimeDuration responseWindow)
    {
        var validation = ValidateCuePresentation(cueId, cueValue, responseWindow);
        if (validation is not null)
        {
            return validation;
        }

        var state = new InhibitionCueState(
            cueId,
            InhibitionCueKind.NoGo,
            cueValue,
            _clock.Now,
            responseWindow,
            expectedResponse: "withhold",
            exceptionId: null);
        _noGoCueCount++;
        _cues.Add(cueId, state);

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueEmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..CueFacts(state),
                new RuntimeEventFact("no_go_handling_required", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult PresentExceptionCue(
        string cueId,
        string cueValue,
        string exceptionId,
        RuntimeDuration responseWindow)
    {
        if (string.IsNullOrWhiteSpace(exceptionId))
        {
            throw new ArgumentException("Inhibition exception id is required.", nameof(exceptionId));
        }

        if (!SupportsExceptionRule())
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.ExceptionRuleNotSupportedByDrill);
        }

        if (!_exceptions.TryGetValue(exceptionId, out var exception))
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.UnknownException);
        }

        var validation = ValidateCuePresentation(cueId, cueValue, responseWindow);
        if (validation is not null)
        {
            return validation;
        }

        var state = new InhibitionCueState(
            cueId,
            InhibitionCueKind.Exception,
            cueValue,
            _clock.Now,
            responseWindow,
            exception.ExpectedResponse,
            exceptionId);
        _exceptionCueCount++;
        _cues.Add(cueId, state);

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueEmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..CueFacts(state),
                ..ExceptionFacts(exception),
            ]);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult RecordGoResponse(string cueId, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Inhibition go response is required.", nameof(response));
        }

        var stateResult = GetUnhandledCue(cueId, InhibitionCueKind.Go, out var state);
        if (stateResult is not null)
        {
            return stateResult;
        }

        state!.Handled = true;
        var responseTime = _clock.Now.ElapsedSince(state.PresentedAt);
        var withinWindow = _clock.Now.Offset <= state.ResponseDeadline.Offset;
        var isCorrect = withinWindow && string.Equals(response, state.ExpectedResponse, StringComparison.Ordinal);
        var responseOutcome = !withinWindow ? "late" : isCorrect ? "correct" : "incorrect";
        RecordItemOutcome(isCorrect);

        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.AddRange(CueResponseFacts(state, response, responseOutcome, responseTime, withinWindow));
        if (!isCorrect)
        {
            facts.Add(new RuntimeEventFact("failed_item_list", $"go cue {cueId}: expected {state.ExpectedResponse}, got {response}"));
            facts.Add(new RuntimeEventFact("failed_constraint", "go_response_rule"));
            facts.Add(new RuntimeEventFact("failure_type_candidate", withinWindow ? "technical_failure" : "overload"));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult RecordNoGoWithheld(string cueId)
    {
        var stateResult = GetUnhandledCue(cueId, InhibitionCueKind.NoGo, out var state);
        if (stateResult is not null)
        {
            return stateResult;
        }

        state!.Handled = true;
        _noGoWithheldCount++;
        _correctItemCount++;
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
                new RuntimeEventFact("no_go_result", "withheld"),
                new RuntimeEventFact("no_go_handling_required", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult RecordNoGoResponse(string cueId, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Inhibition no-go response is required.", nameof(response));
        }

        var stateResult = GetUnhandledCue(cueId, InhibitionCueKind.NoGo, out var state);
        if (stateResult is not null)
        {
            return stateResult;
        }

        state!.Handled = true;
        _noGoResponseCount++;
        _incorrectItemCount++;
        var responseTime = _clock.Now.ElapsedSince(state.PresentedAt);
        var withinWindow = _clock.Now.Offset <= state.ResponseDeadline.Offset;

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..CueResponseFacts(state, response, "incorrect", responseTime, withinWindow),
                new RuntimeEventFact("no_go_result", "responded"),
                new RuntimeEventFact("failed_item_list", $"no-go cue {cueId}: responded {response}"),
                new RuntimeEventFact("failed_constraint", "no_go_requires_withholding"),
                new RuntimeEventFact("error_kind", "no_go_response"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
            ]);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult RecordExceptionResponse(string cueId, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Inhibition exception response is required.", nameof(response));
        }

        var stateResult = GetUnhandledCue(cueId, InhibitionCueKind.Exception, out var state);
        if (stateResult is not null)
        {
            return stateResult;
        }

        state!.Handled = true;
        var responseTime = _clock.Now.ElapsedSince(state.PresentedAt);
        var withinWindow = _clock.Now.Offset <= state.ResponseDeadline.Offset;
        var isCorrect = withinWindow && string.Equals(response, state.ExpectedResponse, StringComparison.Ordinal);
        RecordItemOutcome(isCorrect);

        if (isCorrect)
        {
            _exceptionHandledCount++;
            var runtimeEvent = EventLog.Append(
                RuntimeEventKind.CueResponseSubmitted,
                _clock.Now,
                "active",
                RuntimeSessionPhaseKind.ActiveWork,
                [
                    ..ProtocolFacts(),
                    ..CueResponseFacts(state, response, "correct", responseTime, withinWindow),
                    new RuntimeEventFact("exception_result", "handled"),
                ]);

            return Accepted(runtimeEvent);
        }

        _exceptionForgottenCount++;
        var errorEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..CueFacts(state),
                new RuntimeEventFact("response", response),
                new RuntimeEventFact("response_time", FormatDuration(responseTime)),
                new RuntimeEventFact("within_window", withinWindow ? "true" : "false"),
                new RuntimeEventFact("response_outcome", withinWindow ? "incorrect" : "late"),
                new RuntimeEventFact("exception_result", "forgotten"),
                new RuntimeEventFact("error_kind", "exception_forgotten"),
                new RuntimeEventFact("failed_item_list", $"exception cue {cueId}: expected {state.ExpectedResponse}, got {response}"),
                new RuntimeEventFact("failed_constraint", "exceptions_stated_before_set"),
                new RuntimeEventFact("failure_type_candidate", withinWindow ? "technical_failure" : "overload"),
            ]);

        return Accepted(errorEvent);
    }

    public InhibitionRuntimeProtocolResult RecordPrematureResponse(string itemId, string response)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Inhibition premature response item id is required.", nameof(itemId));
        }

        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Inhibition premature response is required.", nameof(response));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _prematureResponseCount++;
        _incorrectItemCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("item_id", itemId),
                new RuntimeEventFact("response", response),
                new RuntimeEventFact("error_kind", "premature_response"),
                new RuntimeEventFact("failed_item_list", $"premature response {itemId}: {response}"),
                new RuntimeEventFact("failed_constraint", "premature_response_fails_item"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
                new RuntimeEventFact("item_failed", "true"),
                new RuntimeEventFact("premature_response_count", _prematureResponseCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("cue_pace", FormatCuePace()),
            ]);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult RecordRuleChange(string changedRule)
    {
        if (string.IsNullOrWhiteSpace(changedRule))
        {
            throw new ArgumentException("Inhibition changed rule is required.", nameof(changedRule));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _ruleChangeCount++;
        _incorrectItemCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..RuleFacts(),
                new RuntimeEventFact("changed_rule", changedRule),
                new RuntimeEventFact("error_kind", "rule_changed_mid_set"),
                new RuntimeEventFact("failed_item_list", $"rule changed mid-set: {changedRule}"),
                new RuntimeEventFact("failed_constraint", "rule_statement_before_set"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
                new RuntimeEventFact("rule_change_count", _ruleChangeCount.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult RecordPostErrorCascade(
        string cascadeId,
        int affectedItemCount,
        string description)
    {
        if (string.IsNullOrWhiteSpace(cascadeId))
        {
            throw new ArgumentException("Inhibition post-error cascade id is required.", nameof(cascadeId));
        }

        if (affectedItemCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(affectedItemCount),
                affectedItemCount,
                "Post-error cascade affected item count must be positive.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Inhibition post-error cascade description is required.", nameof(description));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _postErrorCascadeCount++;
        _postErrorCascadeAffectedItemCount += affectedItemCount;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("cascade_id", cascadeId),
                new RuntimeEventFact("error_kind", "post_error_cascade"),
                new RuntimeEventFact("failed_item_list", $"post-error cascade {cascadeId}: {description}"),
                new RuntimeEventFact("failed_constraint", "post_error_recovery"),
                new RuntimeEventFact("failure_type_candidate", "overload"),
                new RuntimeEventFact("affected_item_count", affectedItemCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("post_error_cascade_count", _postErrorCascadeCount.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult RecordCorrection(
        string sourceCueId,
        int itemsAfterError,
        string correctionDescription)
    {
        if (string.IsNullOrWhiteSpace(sourceCueId))
        {
            throw new ArgumentException("Inhibition correction source cue id is required.", nameof(sourceCueId));
        }

        if (itemsAfterError < 0)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.InvalidCorrectionDistance);
        }

        if (string.IsNullOrWhiteSpace(correctionDescription))
        {
            throw new ArgumentException("Inhibition correction description is required.", nameof(correctionDescription));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _correctionCount++;
        var withinRequiredItems = itemsAfterError <= 2;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CorrectionSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("source_cue_id", sourceCueId),
                new RuntimeEventFact("items_after_error", itemsAfterError.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("correction_within_required_items", withinRequiredItems ? "true" : "false"),
                new RuntimeEventFact("correction_reference", correctionDescription),
                new RuntimeEventFact("correction_count", _correctionCount.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public InhibitionRuntimeProtocolResult CompleteSet(string setId)
    {
        if (string.IsNullOrWhiteSpace(setId))
        {
            throw new ArgumentException("Inhibition set id is required.", nameof(setId));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _setCompleted = true;
        var totalItems = _correctItemCount + _incorrectItemCount;
        var accuracy = Ratio(_correctItemCount, totalItems);
        var exceptionAccuracy = Ratio(_exceptionHandledCount, _exceptionCueCount);
        var outputSample =
            $"set {setId}: rule={_ruleStatement}; cue_pace={FormatCuePace()}; " +
            $"accuracy={accuracy}; premature_response_count={_prematureResponseCount}; " +
            $"exception_accuracy={exceptionAccuracy}; post_error_cascade_count={_postErrorCascadeCount}";
        var score =
            $"accuracy={accuracy}; correct_items={_correctItemCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"incorrect_items={_incorrectItemCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"cue_pace={FormatCuePace()}; go_cue_count={_goCueCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"no_go_cue_count={_noGoCueCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"no_go_withheld_count={_noGoWithheldCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"no_go_response_count={_noGoResponseCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"premature_response_count={_prematureResponseCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"exception_accuracy={exceptionAccuracy}; exception_cue_count={_exceptionCueCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"exception_handled_count={_exceptionHandledCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"exception_forgotten_count={_exceptionForgottenCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"rule_change_count={_ruleChangeCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"post_error_cascade_count={_postErrorCascadeCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"post_error_cascade_affected_item_count={_postErrorCascadeAffectedItemCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"correction_count={_correctionCount.ToString(CultureInfo.InvariantCulture)}";

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..RuleFacts(),
                new RuntimeEventFact("set_id", setId),
                new RuntimeEventFact("output_sample", outputSample),
                new RuntimeEventFact("score", score),
                new RuntimeEventFact("accuracy", accuracy),
                new RuntimeEventFact("correct_item_count", _correctItemCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("incorrect_item_count", _incorrectItemCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("cue_pace", FormatCuePace()),
                new RuntimeEventFact("premature_response_count", _prematureResponseCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("exception_accuracy", exceptionAccuracy),
                new RuntimeEventFact("rule_change_count", _ruleChangeCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("post_error_cascade_count", _postErrorCascadeCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("post_error_cascade_affected_item_count", _postErrorCascadeAffectedItemCount.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    private InhibitionRuntimeProtocolResult? ValidateCuePresentation(
        string cueId,
        string cueValue,
        RuntimeDuration responseWindow)
    {
        if (string.IsNullOrWhiteSpace(cueId))
        {
            throw new ArgumentException("Inhibition cue id is required.", nameof(cueId));
        }

        if (string.IsNullOrWhiteSpace(cueValue))
        {
            throw new ArgumentException("Inhibition cue value is required.", nameof(cueValue));
        }

        if (responseWindow.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseWindow),
                responseWindow,
                "Inhibition cue response window must be positive.");
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (_cues.ContainsKey(cueId))
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.DuplicateCueId);
        }

        return null;
    }

    private InhibitionRuntimeProtocolResult? GetUnhandledCue(
        string cueId,
        InhibitionCueKind requiredKind,
        out InhibitionCueState? state)
    {
        if (string.IsNullOrWhiteSpace(cueId))
        {
            throw new ArgumentException("Inhibition cue id is required.", nameof(cueId));
        }

        state = null;
        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (!_cues.TryGetValue(cueId, out state))
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.UnknownCue);
        }

        if (state.Kind != requiredKind)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.CueKindMismatch);
        }

        if (state.Handled)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.CueAlreadyHandled);
        }

        return null;
    }

    private InhibitionRuntimeProtocolResult? RequireActiveSet()
    {
        if (_ruleStatement is null)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.RuleRequiredBeforeSet);
        }

        if (!_activeSetStarted)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.ActiveSetNotStarted);
        }

        if (_setCompleted)
        {
            return Rejected(InhibitionRuntimeProtocolInvalidReason.SetAlreadyCompleted);
        }

        return null;
    }

    private void RecordItemOutcome(bool isCorrect)
    {
        if (isCorrect)
        {
            _correctItemCount++;
        }
        else
        {
            _incorrectItemCount++;
        }
    }

    private bool SupportsExceptionRule()
    {
        return SessionDefinition.Drill == DrillId.IR2ExceptionRule;
    }

    private IReadOnlyList<RuntimeEventFact> ProtocolFacts()
    {
        return
        [
            new RuntimeEventFact("protocol_branch", "ir"),
            new RuntimeEventFact("protocol_drill", StableDrill(SessionDefinition.Drill)),
            new RuntimeEventFact("branch", SessionDefinition.Branch.ToString()),
            new RuntimeEventFact("level", SessionDefinition.Level.ToString()),
            new RuntimeEventFact("drill", StableDrill(SessionDefinition.Drill)),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> RuleFacts()
    {
        if (_ruleStatement is null)
        {
            return [];
        }

        var facts = new List<RuntimeEventFact>
        {
            new("rule_statement", _ruleStatement),
            new("rule_explanation", _ruleStatement),
            new("exception_count", _exceptions.Count.ToString(CultureInfo.InvariantCulture)),
        };

        if (SupportsExceptionRule())
        {
            facts.Add(new RuntimeEventFact("exceptions_stated_before_set", _exceptions.Count > 0 ? "true" : "false"));
        }

        foreach (var exception in _exceptions.Values)
        {
            facts.AddRange(ExceptionFacts(exception));
        }

        return facts.AsReadOnly();
    }

    private IReadOnlyList<RuntimeEventFact> ExceptionFacts(InhibitionRuntimeExceptionRule exception)
    {
        return
        [
            new RuntimeEventFact("exception_id", exception.Id),
            new RuntimeEventFact("exception_description", exception.Description),
            new RuntimeEventFact("exception_expected_response", exception.ExpectedResponse),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> CueFacts(InhibitionCueState state)
    {
        var cueKind = state.Kind switch
        {
            InhibitionCueKind.Go => "go",
            InhibitionCueKind.NoGo => "no_go",
            InhibitionCueKind.Exception => "exception",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "Unknown inhibition cue kind."),
        };
        var noResponseExpected = state.Kind is InhibitionCueKind.NoGo;
        var facts = new List<RuntimeEventFact>
        {
            new("cue_id", state.Id),
            new("cue_kind", cueKind),
            new("cue_value", state.Value),
            new("cue_pace", FormatCuePace()),
            new("response_expectation", noResponseExpected ? "no_response_expected" : "response_required"),
            new("expected_response", state.ExpectedResponse),
            new("presented_at", FormatInstant(state.PresentedAt)),
            new("response_deadline", FormatInstant(state.ResponseDeadline)),
            new("premature_response_fails_item", "true"),
        };

        if (state.ExceptionId is not null)
        {
            facts.Add(new RuntimeEventFact("exception_id", state.ExceptionId));
        }

        return facts.AsReadOnly();
    }

    private IReadOnlyList<RuntimeEventFact> CueResponseFacts(
        InhibitionCueState state,
        string response,
        string outcome,
        RuntimeDuration responseTime,
        bool withinWindow)
    {
        return
        [
            ..CueFacts(state),
            new RuntimeEventFact("response", response),
            new RuntimeEventFact("responded_at", FormatInstant(_clock.Now)),
            new RuntimeEventFact("response_time", FormatDuration(responseTime)),
            new RuntimeEventFact("within_window", withinWindow ? "true" : "false"),
            new RuntimeEventFact("response_outcome", outcome),
        ];
    }

    private static InhibitionRuntimeProtocolResult Accepted(RuntimeEvent runtimeEvent)
    {
        return new InhibitionRuntimeProtocolResult(true, InvalidReason: null, runtimeEvent);
    }

    private static InhibitionRuntimeProtocolResult Rejected(InhibitionRuntimeProtocolInvalidReason invalidReason)
    {
        return new InhibitionRuntimeProtocolResult(false, invalidReason, Event: null);
    }

    private static void ValidateInhibitionSession(RuntimeSessionDefinition sessionDefinition)
    {
        if (sessionDefinition.Branch != BranchCode.IR)
        {
            throw new ArgumentException("Inhibition runtime protocol requires an IR session.", nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill is not DrillId.IR1GoNoGoRule and not DrillId.IR2ExceptionRule)
        {
            throw new ArgumentException(
                "Inhibition runtime protocol supports only IR-1 Go/No-Go Rule and IR-2 Exception Rule.",
                nameof(sessionDefinition));
        }

        if (!ContainsConstraint(sessionDefinition, "rule") ||
            !ContainsConstraint(sessionDefinition, "premature response"))
        {
            throw new ArgumentException(
                "Inhibition runtime sessions must include rule statement and premature-response constraints.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.IR2ExceptionRule &&
            !ContainsConstraint(sessionDefinition, "exceptions"))
        {
            throw new ArgumentException(
                "IR-2 runtime sessions must include the pre-stated exception constraint.",
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
            DrillId.IR1GoNoGoRule => "ir_1_go_no_go_rule",
            DrillId.IR2ExceptionRule => "ir_2_exception_rule",
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unsupported inhibition drill."),
        };
    }

    private string FormatCuePace()
    {
        return _cuePace.HasValue
            ? FormatDuration(_cuePace.Value)
            : "not_started";
    }

    private static string Ratio(int numerator, int denominator)
    {
        return denominator == 0
            ? "0/0"
            : $"{numerator.ToString(CultureInfo.InvariantCulture)}/{denominator.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatDuration(RuntimeDuration duration)
    {
        return duration.Value.ToString("c", CultureInfo.InvariantCulture);
    }

    private static string FormatInstant(RuntimeInstant instant)
    {
        return instant.Offset.ToString("c", CultureInfo.InvariantCulture);
    }

    private enum InhibitionCueKind
    {
        Go,
        NoGo,
        Exception,
    }

    private sealed class InhibitionCueState
    {
        public InhibitionCueState(
            string id,
            InhibitionCueKind kind,
            string value,
            RuntimeInstant presentedAt,
            RuntimeDuration responseWindow,
            string expectedResponse,
            string? exceptionId)
        {
            Id = id;
            Kind = kind;
            Value = value;
            PresentedAt = presentedAt;
            ResponseWindow = responseWindow;
            ExpectedResponse = expectedResponse;
            ExceptionId = exceptionId;
        }

        public string Id { get; }

        public InhibitionCueKind Kind { get; }

        public string Value { get; }

        public RuntimeInstant PresentedAt { get; }

        public RuntimeDuration ResponseWindow { get; }

        public RuntimeInstant ResponseDeadline => PresentedAt.Add(ResponseWindow);

        public string ExpectedResponse { get; }

        public string? ExceptionId { get; }

        public bool Handled { get; set; }
    }
}
