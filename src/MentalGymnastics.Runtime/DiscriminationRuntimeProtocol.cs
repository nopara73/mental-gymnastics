using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum DiscriminationRuntimeProtocolInvalidReason
{
    PairDiscriminationNotSupportedByDrill,
    SeededAuditNotSupportedByDrill,
    PairSetAlreadyStated,
    PairSetRequiredBeforeSet,
    PairSetRequiresAtLeastOnePair,
    DuplicatePairId,
    ActiveSetAlreadyStarted,
    ActiveSetNotStarted,
    SetAlreadyCompleted,
    UnknownPair,
    PairAlreadyAnswered,
    OriginalOutputAlreadyLocked,
    OriginalOutputRequiredBeforeAudit,
    SeededErrorsAlreadyStated,
    SeededErrorsRequired,
    DuplicateSeededErrorId,
    UnknownSeededError,
    SeededErrorAlreadyFound,
    AuditAlreadyStarted,
    AuditNotStarted,
    AuditAlreadyCompleted,
}

public sealed class DiscriminationRuntimePair
{
    public DiscriminationRuntimePair(
        string id,
        string left,
        string right,
        bool expectedDifferent)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Discrimination pair id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(left))
        {
            throw new ArgumentException("Discrimination pair left item is required.", nameof(left));
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            throw new ArgumentException("Discrimination pair right item is required.", nameof(right));
        }

        Id = id;
        Left = left;
        Right = right;
        ExpectedDifferent = expectedDifferent;
    }

    public string Id { get; }

    public string Left { get; }

    public string Right { get; }

    public bool ExpectedDifferent { get; }
}

public sealed class DiscriminationRuntimeSeededError
{
    public DiscriminationRuntimeSeededError(string id, string description)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Discrimination seeded error id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Discrimination seeded error description is required.", nameof(description));
        }

        Id = id;
        Description = description;
    }

    public string Id { get; }

    public string Description { get; }
}

public sealed record DiscriminationRuntimeProtocolResult(
    bool IsAccepted,
    DiscriminationRuntimeProtocolInvalidReason? InvalidReason,
    RuntimeEvent? Event);

public sealed class DiscriminationRuntimeProtocol
{
    private readonly IRuntimeClock _clock;
    private readonly Dictionary<string, DiscriminationRuntimePair> _pairs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _answeredPairs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _markedGuesses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SeededErrorState> _seededErrors = new(StringComparer.Ordinal);
    private bool _activeSetStarted;
    private bool _setCompleted;
    private int _correctComparisonCount;
    private int _incorrectComparisonCount;
    private int _falsePositiveCount;
    private int _falseNegativeCount;
    private int _originalOutputEditAttemptCount;
    private int _inventedErrorCount;
    private string? _originalOutputId;
    private string? _originalOutput;
    private bool _auditStarted;
    private bool _auditCompleted;

    private DiscriminationRuntimeProtocol(
        IRuntimeClock clock,
        RuntimeEventLog eventLog)
    {
        _clock = clock;
        EventLog = eventLog;
    }

    public RuntimeEventLog EventLog { get; }

    public RuntimeSessionDefinition SessionDefinition => EventLog.SessionDefinition;

    public IReadOnlyList<DiscriminationRuntimePair> Pairs => _pairs.Values.ToArray();

    public IReadOnlyList<DiscriminationRuntimeSeededError> SeededErrors =>
        _seededErrors.Values
            .Select(state => state.SeededError)
            .ToArray();

    public static DiscriminationRuntimeProtocol Start(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        IRuntimeClock clock)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Discrimination runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(clock);
        ValidateDiscriminationSession(sessionDefinition);

        return new DiscriminationRuntimeProtocol(
            clock,
            RuntimeEventLog.Start(sessionId, sessionDefinition, clock.Now));
    }

    public DiscriminationRuntimeProtocolResult StatePairSet(
        IEnumerable<DiscriminationRuntimePair> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        if (!SupportsPairDiscrimination())
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.PairDiscriminationNotSupportedByDrill);
        }

        if (_pairs.Count > 0)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.PairSetAlreadyStated);
        }

        var pairArray = pairs.ToArray();
        foreach (var pair in pairArray)
        {
            ArgumentNullException.ThrowIfNull(pair);
        }

        if (pairArray.Length == 0)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.PairSetRequiresAtLeastOnePair);
        }

        var duplicatePair = pairArray
            .GroupBy(pair => pair.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePair is not null)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.DuplicatePairId);
        }

        foreach (var pair in pairArray)
        {
            _pairs.Add(pair.Id, pair);
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..PairSetFacts(),
                new RuntimeEventFact("pair_set_stated_before_set", "true"),
                new RuntimeEventFact("guessing_must_be_marked", "true"),
                new RuntimeEventFact("honesty_constraint", "guessing_must_be_marked"),
            ]);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult StartActiveSet()
    {
        if (!SupportsPairDiscrimination())
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.PairDiscriminationNotSupportedByDrill);
        }

        if (_pairs.Count == 0)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.PairSetRequiredBeforeSet);
        }

        if (_activeSetStarted)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.ActiveSetAlreadyStarted);
        }

        _activeSetStarted = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..PairSetFacts(),
                new RuntimeEventFact("guessing_must_be_marked", "true"),
                new RuntimeEventFact("false_positive_tracking", "true"),
                new RuntimeEventFact("false_negative_tracking", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult MarkGuess(
        string pairId,
        string guessReason)
    {
        if (string.IsNullOrWhiteSpace(pairId))
        {
            throw new ArgumentException("Discrimination pair id is required.", nameof(pairId));
        }

        if (string.IsNullOrWhiteSpace(guessReason))
        {
            throw new ArgumentException("Discrimination guess reason is required.", nameof(guessReason));
        }

        var pairResult = RequireKnownPair(pairId, out var pair);
        if (pairResult is not null)
        {
            return pairResult;
        }

        _markedGuesses[pairId] = guessReason;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.GuessMarked,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..PairFacts(pair!),
                new RuntimeEventFact("guess_id", $"guess:{pairId}"),
                new RuntimeEventFact("pair_id", pairId),
                new RuntimeEventFact("guess_marked", "true"),
                new RuntimeEventFact("guess_reason", guessReason),
                new RuntimeEventFact("guessing_must_be_marked", "true"),
                new RuntimeEventFact("marked_guess_count", _markedGuesses.Count.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult SubmitComparison(
        string pairId,
        bool reportedDifferent)
    {
        if (string.IsNullOrWhiteSpace(pairId))
        {
            throw new ArgumentException("Discrimination pair id is required.", nameof(pairId));
        }

        var pairResult = RequireKnownPair(pairId, out var pair);
        if (pairResult is not null)
        {
            return pairResult;
        }

        if (_answeredPairs.Contains(pairId))
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.PairAlreadyAnswered);
        }

        _answeredPairs.Add(pairId);
        var isCorrect = pair!.ExpectedDifferent == reportedDifferent;
        if (isCorrect)
        {
            _correctComparisonCount++;
        }
        else
        {
            _incorrectComparisonCount++;
        }

        var errorKind = ErrorKindForComparison(pair.ExpectedDifferent, reportedDifferent);
        if (errorKind == "false_positive")
        {
            _falsePositiveCount++;
        }
        else if (errorKind == "false_negative")
        {
            _falseNegativeCount++;
        }

        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.AddRange(PairFacts(pair));
        facts.Add(new RuntimeEventFact("response", reportedDifferent ? "different" : "same"));
        facts.Add(new RuntimeEventFact("reported_different", reportedDifferent ? "true" : "false"));
        facts.Add(new RuntimeEventFact("expected_response", pair.ExpectedDifferent ? "different" : "same"));
        facts.Add(new RuntimeEventFact("response_outcome", isCorrect ? "correct" : "incorrect"));
        facts.Add(new RuntimeEventFact("guess_marked", _markedGuesses.ContainsKey(pairId) ? "true" : "false"));
        facts.Add(new RuntimeEventFact("guessing_must_be_marked", "true"));

        if (!isCorrect)
        {
            facts.Add(new RuntimeEventFact("error_kind", errorKind!));
            facts.Add(new RuntimeEventFact("failed_item_list", $"{ReadableErrorKind(errorKind!)} {pairId}: expected {ExpectedText(pair.ExpectedDifferent)}, got {ExpectedText(reportedDifferent)}"));
            facts.Add(new RuntimeEventFact("failed_constraint", errorKind!));
            facts.Add(new RuntimeEventFact("failure_type_candidate", "technical_failure"));
            facts.Add(new RuntimeEventFact("false_positive_count", _falsePositiveCount.ToString(CultureInfo.InvariantCulture)));
            facts.Add(new RuntimeEventFact("false_negative_count", _falseNegativeCount.ToString(CultureInfo.InvariantCulture)));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult CompleteSet(string setId)
    {
        if (string.IsNullOrWhiteSpace(setId))
        {
            throw new ArgumentException("Discrimination set id is required.", nameof(setId));
        }

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _setCompleted = true;
        var answeredCount = _answeredPairs.Count;
        var accuracy = Ratio(_correctComparisonCount, answeredCount);
        var comparison =
            $"comparison_accuracy={accuracy}; false_positives={_falsePositiveCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"false_negatives={_falseNegativeCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"marked_guesses={_markedGuesses.Count.ToString(CultureInfo.InvariantCulture)}";
        var outputSample =
            $"set {setId}: comparisons={answeredCount.ToString(CultureInfo.InvariantCulture)}; {comparison}";
        var score =
            $"comparison_accuracy={accuracy}; correct_comparison_count={_correctComparisonCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"incorrect_comparison_count={_incorrectComparisonCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"false_positive_count={_falsePositiveCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"false_negative_count={_falseNegativeCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"marked_guess_count={_markedGuesses.Count.ToString(CultureInfo.InvariantCulture)}";

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("set_id", setId),
                new RuntimeEventFact("output_sample", outputSample),
                new RuntimeEventFact("comparison", comparison),
                new RuntimeEventFact("score", score),
                new RuntimeEventFact("comparison_accuracy", accuracy),
                new RuntimeEventFact("correct_comparison_count", _correctComparisonCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("incorrect_comparison_count", _incorrectComparisonCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("false_positive_count", _falsePositiveCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("false_negative_count", _falseNegativeCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("marked_guess_count", _markedGuesses.Count.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("guessing_must_be_marked", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult LockOriginalOutput(
        string outputId,
        string originalOutput)
    {
        if (string.IsNullOrWhiteSpace(outputId))
        {
            throw new ArgumentException("Discrimination original output id is required.", nameof(outputId));
        }

        if (string.IsNullOrWhiteSpace(originalOutput))
        {
            throw new ArgumentException("Discrimination original output is required.", nameof(originalOutput));
        }

        if (!SupportsSeededAudit())
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.SeededAuditNotSupportedByDrill);
        }

        if (_originalOutput is not null)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.OriginalOutputAlreadyLocked);
        }

        _originalOutputId = outputId;
        _originalOutput = originalOutput;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("original_output_id", outputId),
                new RuntimeEventFact("original_output", originalOutput),
                new RuntimeEventFact("original_output_locked", "true"),
                new RuntimeEventFact("original_output_edit_allowed", "false"),
                new RuntimeEventFact("honesty_constraint", "original_output_locked_during_audit"),
            ]);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult StateSeededErrors(
        IEnumerable<DiscriminationRuntimeSeededError> seededErrors)
    {
        ArgumentNullException.ThrowIfNull(seededErrors);

        if (!SupportsSeededAudit())
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.SeededAuditNotSupportedByDrill);
        }

        if (_seededErrors.Count > 0)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.SeededErrorsAlreadyStated);
        }

        var seededErrorArray = seededErrors.ToArray();
        foreach (var seededError in seededErrorArray)
        {
            ArgumentNullException.ThrowIfNull(seededError);
        }

        if (seededErrorArray.Length == 0)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.SeededErrorsRequired);
        }

        var duplicateSeededError = seededErrorArray
            .GroupBy(error => error.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSeededError is not null)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.DuplicateSeededErrorId);
        }

        foreach (var seededError in seededErrorArray)
        {
            _seededErrors.Add(seededError.Id, new SeededErrorState(seededError));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..SeededErrorFacts(),
                new RuntimeEventFact("seeded_errors_stated_before_audit", "true"),
                new RuntimeEventFact("seeded_error_count", _seededErrors.Count.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult StartAudit()
    {
        if (!SupportsSeededAudit())
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.SeededAuditNotSupportedByDrill);
        }

        if (_originalOutput is null)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.OriginalOutputRequiredBeforeAudit);
        }

        if (_seededErrors.Count == 0)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.SeededErrorsRequired);
        }

        if (_auditStarted)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.AuditAlreadyStarted);
        }

        _auditStarted = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AuditStarted,
            _clock.Now,
            "audit",
            RuntimeSessionPhaseKind.Audit,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("original_output_id", _originalOutputId!),
                new RuntimeEventFact("original_output_locked", "true"),
                new RuntimeEventFact("original_output_edit_allowed", "false"),
                new RuntimeEventFact("seeded_error_count", _seededErrors.Count.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult RecordOriginalOutputEditAttempt(string attemptedEdit)
    {
        if (string.IsNullOrWhiteSpace(attemptedEdit))
        {
            throw new ArgumentException("Discrimination original output edit attempt is required.", nameof(attemptedEdit));
        }

        var auditResult = RequireAuditInProgress();
        if (auditResult is not null)
        {
            return auditResult;
        }

        _originalOutputEditAttemptCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "audit",
            RuntimeSessionPhaseKind.Audit,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("error_kind", "original_output_edit_attempt"),
                new RuntimeEventFact("failed_item_list", $"original output edit attempt: {attemptedEdit}"),
                new RuntimeEventFact("failed_constraint", "original_output_locked_during_audit"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
                new RuntimeEventFact("original_output_edit_allowed", "false"),
                new RuntimeEventFact("original_output_edit_attempt_count", _originalOutputEditAttemptCount.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult FindSeededError(
        string seededErrorId,
        string findingDescription)
    {
        if (string.IsNullOrWhiteSpace(seededErrorId))
        {
            throw new ArgumentException("Discrimination seeded error id is required.", nameof(seededErrorId));
        }

        if (string.IsNullOrWhiteSpace(findingDescription))
        {
            throw new ArgumentException("Discrimination seeded error finding is required.", nameof(findingDescription));
        }

        var auditResult = RequireAuditInProgress();
        if (auditResult is not null)
        {
            return auditResult;
        }

        if (!_seededErrors.TryGetValue(seededErrorId, out var state))
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.UnknownSeededError);
        }

        if (state.Found)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.SeededErrorAlreadyFound);
        }

        state.Found = true;
        state.FindingDescription = findingDescription;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "audit",
            RuntimeSessionPhaseKind.Audit,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("seeded_error_id", seededErrorId),
                new RuntimeEventFact("seeded_error_description", state.SeededError.Description),
                new RuntimeEventFact("audit_finding", findingDescription),
                new RuntimeEventFact("audit_finding_result", "seeded_error_found"),
                new RuntimeEventFact("seeded_error_found_count", FoundSeededErrorCount().ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult RecordInventedError(
        string inventedErrorId,
        string description)
    {
        if (string.IsNullOrWhiteSpace(inventedErrorId))
        {
            throw new ArgumentException("Discrimination invented error id is required.", nameof(inventedErrorId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Discrimination invented error description is required.", nameof(description));
        }

        var auditResult = RequireAuditInProgress();
        if (auditResult is not null)
        {
            return auditResult;
        }

        _inventedErrorCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "audit",
            RuntimeSessionPhaseKind.Audit,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("invented_error_id", inventedErrorId),
                new RuntimeEventFact("invented_error_description", description),
                new RuntimeEventFact("error_kind", "invented_error"),
                new RuntimeEventFact("failed_item_list", $"invented error {inventedErrorId}: {description}"),
                new RuntimeEventFact("failed_constraint", "invented_errors_do_not_count"),
                new RuntimeEventFact("failure_type_candidate", "technical_failure"),
                new RuntimeEventFact("invented_error_count", _inventedErrorCount.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public DiscriminationRuntimeProtocolResult CompleteAudit(string auditId)
    {
        if (string.IsNullOrWhiteSpace(auditId))
        {
            throw new ArgumentException("Discrimination audit id is required.", nameof(auditId));
        }

        var auditResult = RequireAuditInProgress();
        if (auditResult is not null)
        {
            return auditResult;
        }

        _auditCompleted = true;
        var foundCount = FoundSeededErrorCount();
        var missedCount = _seededErrors.Count - foundCount;
        var seededErrorRate = Ratio(foundCount, _seededErrors.Count);
        var auditSummary =
            $"seeded_errors_found={seededErrorRate}; seeded_errors_missed={missedCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"invented_errors={_inventedErrorCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"original_output_edit_attempts={_originalOutputEditAttemptCount.ToString(CultureInfo.InvariantCulture)}";
        var score =
            $"seeded_error_find_rate={seededErrorRate}; seeded_error_found_count={foundCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"seeded_error_missed_count={missedCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"invented_error_count={_inventedErrorCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"original_output_edit_attempt_count={_originalOutputEditAttemptCount.ToString(CultureInfo.InvariantCulture)}";

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "audit",
            RuntimeSessionPhaseKind.Audit,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("audit_id", auditId),
                new RuntimeEventFact("audit_result", auditSummary),
                new RuntimeEventFact("audit_findings", auditSummary),
                new RuntimeEventFact("score", score),
                new RuntimeEventFact("seeded_error_found_count", foundCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("seeded_error_missed_count", missedCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("invented_error_count", _inventedErrorCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("original_output_edit_attempt_count", _originalOutputEditAttemptCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("original_output_locked", "true"),
                new RuntimeEventFact("original_output_edit_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    private DiscriminationRuntimeProtocolResult? RequireKnownPair(
        string pairId,
        out DiscriminationRuntimePair? pair)
    {
        pair = null;

        var activeResult = RequireActiveSet();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (!_pairs.TryGetValue(pairId, out pair))
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.UnknownPair);
        }

        return null;
    }

    private DiscriminationRuntimeProtocolResult? RequireActiveSet()
    {
        if (!SupportsPairDiscrimination())
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.PairDiscriminationNotSupportedByDrill);
        }

        if (_pairs.Count == 0)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.PairSetRequiredBeforeSet);
        }

        if (!_activeSetStarted)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.ActiveSetNotStarted);
        }

        if (_setCompleted)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.SetAlreadyCompleted);
        }

        return null;
    }

    private DiscriminationRuntimeProtocolResult? RequireAuditInProgress()
    {
        if (!SupportsSeededAudit())
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.SeededAuditNotSupportedByDrill);
        }

        if (!_auditStarted)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.AuditNotStarted);
        }

        if (_auditCompleted)
        {
            return Rejected(DiscriminationRuntimeProtocolInvalidReason.AuditAlreadyCompleted);
        }

        return null;
    }

    private bool SupportsPairDiscrimination()
    {
        return SessionDefinition.Drill == DrillId.DE1PairDiscrimination;
    }

    private bool SupportsSeededAudit()
    {
        return SessionDefinition.Drill == DrillId.DE2SeededAudit;
    }

    private int FoundSeededErrorCount()
    {
        return _seededErrors.Values.Count(state => state.Found);
    }

    private IReadOnlyList<RuntimeEventFact> ProtocolFacts()
    {
        return
        [
            new RuntimeEventFact("protocol_branch", "de"),
            new RuntimeEventFact("protocol_drill", StableDrill(SessionDefinition.Drill)),
            new RuntimeEventFact("branch", SessionDefinition.Branch.ToString()),
            new RuntimeEventFact("level", SessionDefinition.Level.ToString()),
            new RuntimeEventFact("drill", StableDrill(SessionDefinition.Drill)),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> PairSetFacts()
    {
        return
        [
            new RuntimeEventFact("pair_count", _pairs.Count.ToString(CultureInfo.InvariantCulture)),
            new RuntimeEventFact("pair_ids", string.Join(",", _pairs.Keys)),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> PairFacts(DiscriminationRuntimePair pair)
    {
        return
        [
            new RuntimeEventFact("pair_id", pair.Id),
            new RuntimeEventFact("left_item", pair.Left),
            new RuntimeEventFact("right_item", pair.Right),
            new RuntimeEventFact("expected_different", pair.ExpectedDifferent ? "true" : "false"),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> SeededErrorFacts()
    {
        var facts = new List<RuntimeEventFact>
        {
            new("seeded_error_count", _seededErrors.Count.ToString(CultureInfo.InvariantCulture)),
            new("seeded_error_ids", string.Join(",", _seededErrors.Keys)),
        };

        foreach (var state in _seededErrors.Values)
        {
            facts.Add(new RuntimeEventFact("seeded_error_id", state.SeededError.Id));
            facts.Add(new RuntimeEventFact("seeded_error_description", state.SeededError.Description));
        }

        return facts.AsReadOnly();
    }

    private static DiscriminationRuntimeProtocolResult Accepted(RuntimeEvent runtimeEvent)
    {
        return new DiscriminationRuntimeProtocolResult(true, InvalidReason: null, runtimeEvent);
    }

    private static DiscriminationRuntimeProtocolResult Rejected(DiscriminationRuntimeProtocolInvalidReason invalidReason)
    {
        return new DiscriminationRuntimeProtocolResult(false, invalidReason, Event: null);
    }

    private static void ValidateDiscriminationSession(RuntimeSessionDefinition sessionDefinition)
    {
        if (sessionDefinition.Branch != BranchCode.DE)
        {
            throw new ArgumentException("Discrimination runtime protocol requires a DE session.", nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill is not DrillId.DE1PairDiscrimination and not DrillId.DE2SeededAudit)
        {
            throw new ArgumentException(
                "Discrimination runtime protocol supports only DE-1 Pair Discrimination and DE-2 Seeded Audit.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.DE1PairDiscrimination &&
            !ContainsConstraint(sessionDefinition, "guess"))
        {
            throw new ArgumentException(
                "DE-1 runtime sessions must include the marked-guess constraint.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.DE2SeededAudit &&
            !ContainsConstraint(sessionDefinition, "original output"))
        {
            throw new ArgumentException(
                "DE-2 runtime sessions must include the original-output lock constraint.",
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
            DrillId.DE1PairDiscrimination => "de_1_pair_discrimination",
            DrillId.DE2SeededAudit => "de_2_seeded_audit",
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unsupported discrimination drill."),
        };
    }

    private static string? ErrorKindForComparison(bool expectedDifferent, bool reportedDifferent)
    {
        if (expectedDifferent == reportedDifferent)
        {
            return null;
        }

        return reportedDifferent ? "false_positive" : "false_negative";
    }

    private static string ReadableErrorKind(string errorKind)
    {
        return errorKind.Replace('_', ' ');
    }

    private static string ExpectedText(bool different)
    {
        return different ? "different" : "same";
    }

    private static string Ratio(int numerator, int denominator)
    {
        return denominator == 0
            ? "0/0"
            : $"{numerator.ToString(CultureInfo.InvariantCulture)}/{denominator.ToString(CultureInfo.InvariantCulture)}";
    }

    private sealed class SeededErrorState
    {
        public SeededErrorState(DiscriminationRuntimeSeededError seededError)
        {
            SeededError = seededError;
        }

        public DiscriminationRuntimeSeededError SeededError { get; }

        public bool Found { get; set; }

        public string? FindingDescription { get; set; }
    }
}
