using System.Globalization;

namespace MentalGymnastics.Runtime;

public enum RuntimeScoringEventKind
{
    CorrectResponse,
    IncorrectResponse,
    Omission,
    Commission,
    PrematureResponse,
    LateResponse,
    MarkedDrift,
    UnmarkedDrift,
    MarkedGuess,
    Correction,
    Timeout,
}

public sealed class RuntimeScoringEvent
{
    private static readonly HashSet<string> ProgressionDecisionFactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "advancement",
        "branch_level_state",
        "gate_decision",
        "gate_outcome",
        "owned",
        "pass_state",
        "progression_decision",
        "standard_passed",
    };

    public RuntimeScoringEvent(
        string sourceId,
        RuntimeScoringEventKind kind,
        RuntimeInstant occurredAt,
        string evidenceSource,
        IEnumerable<RuntimeEventFact>? observableFacts = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Runtime scoring event source id is required.", nameof(sourceId));
        }

        EnsureDefined(kind, nameof(kind));

        if (string.IsNullOrWhiteSpace(evidenceSource))
        {
            throw new ArgumentException("Runtime scoring event evidence source is required.", nameof(evidenceSource));
        }

        var factArray = (observableFacts ?? Array.Empty<RuntimeEventFact>()).ToArray();
        foreach (var fact in factArray)
        {
            ArgumentNullException.ThrowIfNull(fact);

            if (ProgressionDecisionFactNames.Contains(fact.Name))
            {
                throw new ArgumentException(
                    "Runtime scoring events must not contain progression or gate decision facts.",
                    nameof(observableFacts));
            }
        }

        SourceId = sourceId;
        Kind = kind;
        OccurredAt = occurredAt;
        EvidenceSource = evidenceSource;

        var evidenceFacts = new List<RuntimeEventFact>
        {
            new("scoring_event_kind", StableKind(kind)),
            new("scoring_source_id", sourceId),
            new("evidence_source", evidenceSource),
            new("occurred_at", FormatInstant(occurredAt)),
        };
        evidenceFacts.AddRange(factArray);
        EvidenceFacts = evidenceFacts.AsReadOnly();
    }

    public string SourceId { get; }

    public RuntimeScoringEventKind Kind { get; }

    public RuntimeInstant OccurredAt { get; }

    public string EvidenceSource { get; }

    public IReadOnlyList<RuntimeEventFact> EvidenceFacts { get; }

    internal static string StableKind(RuntimeScoringEventKind kind)
    {
        return kind switch
        {
            RuntimeScoringEventKind.CorrectResponse => "correct_response",
            RuntimeScoringEventKind.IncorrectResponse => "incorrect_response",
            RuntimeScoringEventKind.Omission => "omission",
            RuntimeScoringEventKind.Commission => "commission",
            RuntimeScoringEventKind.PrematureResponse => "premature_response",
            RuntimeScoringEventKind.LateResponse => "late_response",
            RuntimeScoringEventKind.MarkedDrift => "marked_drift",
            RuntimeScoringEventKind.UnmarkedDrift => "unmarked_drift",
            RuntimeScoringEventKind.MarkedGuess => "marked_guess",
            RuntimeScoringEventKind.Correction => "correction",
            RuntimeScoringEventKind.Timeout => "timeout",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown runtime scoring event kind."),
        };
    }

    private static string FormatInstant(RuntimeInstant instant)
    {
        return instant.Offset.ToString("c", CultureInfo.InvariantCulture);
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime scoring event value.");
        }
    }
}

public static class RuntimeScoringEventFactory
{
    public static RuntimeScoringEvent? FromRuntimeEvent(RuntimeEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        return runtimeEvent.Kind switch
        {
            RuntimeEventKind.CueResponseSubmitted => FromCueResponseEvent(runtimeEvent),
            RuntimeEventKind.DriftMarked => FromRuntimeEvent(runtimeEvent, RuntimeScoringEventKind.MarkedDrift),
            RuntimeEventKind.GuessMarked => FromRuntimeEvent(runtimeEvent, RuntimeScoringEventKind.MarkedGuess),
            RuntimeEventKind.CorrectionSubmitted => FromRuntimeEvent(runtimeEvent, RuntimeScoringEventKind.Correction),
            RuntimeEventKind.PhaseTimedOut => FromRuntimeEvent(runtimeEvent, RuntimeScoringEventKind.Timeout),
            RuntimeEventKind.ErrorRecorded => FromErrorEvent(runtimeEvent),
            _ => null,
        };
    }

    public static RuntimeScoringEvent? FromResponseTiming(
        RuntimeResponseTimingResult timing,
        RuntimeCueResponseExpectation responseExpectation)
    {
        ArgumentNullException.ThrowIfNull(timing);
        EnsureDefined(responseExpectation, nameof(responseExpectation));

        var scoringKind = timing.Outcome switch
        {
            RuntimeResponseTimingOutcome.Early => RuntimeScoringEventKind.PrematureResponse,
            RuntimeResponseTimingOutcome.Late => RuntimeScoringEventKind.LateResponse,
            RuntimeResponseTimingOutcome.Missed when responseExpectation == RuntimeCueResponseExpectation.ResponseRequired =>
                RuntimeScoringEventKind.Omission,
            _ => (RuntimeScoringEventKind?)null,
        };

        return scoringKind.HasValue
            ? new RuntimeScoringEvent(
                $"response_timing:{timing.WindowId}",
                scoringKind.Value,
                timing.ObservedAt,
                timing.EvidenceSource,
                timing.EvidenceFacts)
            : null;
    }

    private static RuntimeScoringEvent FromCueResponseEvent(RuntimeEvent runtimeEvent)
    {
        var responseOutcome = RequiredFact(runtimeEvent, "response_outcome");
        var scoringKind = responseOutcome.Value switch
        {
            "correct" => RuntimeScoringEventKind.CorrectResponse,
            "incorrect" when IsCommission(runtimeEvent) => RuntimeScoringEventKind.Commission,
            "incorrect" => RuntimeScoringEventKind.IncorrectResponse,
            "late" => RuntimeScoringEventKind.LateResponse,
            _ => throw new ArgumentException("Unknown cue response outcome cannot be converted to a scoring event.", nameof(runtimeEvent)),
        };

        return FromRuntimeEvent(runtimeEvent, scoringKind);
    }

    private static RuntimeScoringEvent FromErrorEvent(RuntimeEvent runtimeEvent)
    {
        var errorKind = RequiredFact(runtimeEvent, "error_kind");
        var scoringKind = errorKind.Value switch
        {
            "incorrect_response" => RuntimeScoringEventKind.IncorrectResponse,
            "omission" => RuntimeScoringEventKind.Omission,
            "commission" => RuntimeScoringEventKind.Commission,
            "skipped_phase" => RuntimeScoringEventKind.IncorrectResponse,
            "premature_response" => RuntimeScoringEventKind.PrematureResponse,
            "late_response" => RuntimeScoringEventKind.LateResponse,
            "unmarked_drift" => RuntimeScoringEventKind.UnmarkedDrift,
            "target_substitution" => RuntimeScoringEventKind.IncorrectResponse,
            "reread_after_encode" => RuntimeScoringEventKind.IncorrectResponse,
            "invented_item" => RuntimeScoringEventKind.IncorrectResponse,
            "hidden_intermediate_notes" => RuntimeScoringEventKind.IncorrectResponse,
            "rule_error" => RuntimeScoringEventKind.IncorrectResponse,
            "lost_source_item" => RuntimeScoringEventKind.Omission,
            "no_go_response" => RuntimeScoringEventKind.Commission,
            "exception_forgotten" => RuntimeScoringEventKind.IncorrectResponse,
            "rule_changed_mid_set" => RuntimeScoringEventKind.IncorrectResponse,
            "post_error_cascade" => RuntimeScoringEventKind.IncorrectResponse,
            "false_positive" => RuntimeScoringEventKind.Commission,
            "false_negative" => RuntimeScoringEventKind.Omission,
            "unmarked_guess" => RuntimeScoringEventKind.IncorrectResponse,
            "original_output_edit_attempt" => RuntimeScoringEventKind.IncorrectResponse,
            "invented_error" => RuntimeScoringEventKind.IncorrectResponse,
            "negative_example_misclassified" => RuntimeScoringEventKind.Commission,
            "positive_example_misclassified" => RuntimeScoringEventKind.Omission,
            "unsupported_inference" => RuntimeScoringEventKind.IncorrectResponse,
            "surface_match" => RuntimeScoringEventKind.IncorrectResponse,
            "missing_relation" => RuntimeScoringEventKind.Omission,
            "standard_lowered_during_pressure" => RuntimeScoringEventKind.IncorrectResponse,
            "pressure_constraint_breach" => RuntimeScoringEventKind.IncorrectResponse,
            "source_standard_failed_under_pressure" => RuntimeScoringEventKind.IncorrectResponse,
            "full_restart_attempt" => RuntimeScoringEventKind.IncorrectResponse,
            "recovery_window_missed" => RuntimeScoringEventKind.LateResponse,
            "post_disruption_below_threshold" => RuntimeScoringEventKind.IncorrectResponse,
            "component_branch_below_passing" => RuntimeScoringEventKind.IncorrectResponse,
            "component_critical_constraint_breach" => RuntimeScoringEventKind.IncorrectResponse,
            "branch_specific_evidence_removed" => RuntimeScoringEventKind.IncorrectResponse,
            "global_review_audit_failed" => RuntimeScoringEventKind.IncorrectResponse,
            "delayed_reconstruction_failed" => RuntimeScoringEventKind.IncorrectResponse,
            "abandoned_evidence" => RuntimeScoringEventKind.IncorrectResponse,
            "timeout" => RuntimeScoringEventKind.Timeout,
            _ => throw new ArgumentException("Unknown runtime error kind cannot be converted to a scoring event.", nameof(runtimeEvent)),
        };

        return FromRuntimeEvent(runtimeEvent, scoringKind);
    }

    private static RuntimeScoringEvent FromRuntimeEvent(
        RuntimeEvent runtimeEvent,
        RuntimeScoringEventKind scoringKind)
    {
        return new RuntimeScoringEvent(
            $"runtime_event:{runtimeEvent.SequenceNumber.ToString(CultureInfo.InvariantCulture)}",
            scoringKind,
            runtimeEvent.OccurredAt,
            StableRuntimeEventKind(runtimeEvent.Kind),
            BuildRuntimeEventFacts(runtimeEvent));
    }

    private static IReadOnlyList<RuntimeEventFact> BuildRuntimeEventFacts(RuntimeEvent runtimeEvent)
    {
        var facts = new List<RuntimeEventFact>
        {
            new("session_id", runtimeEvent.SessionId),
            new("runtime_event_sequence", runtimeEvent.SequenceNumber.ToString(CultureInfo.InvariantCulture)),
            new("runtime_event_kind", StableRuntimeEventKind(runtimeEvent.Kind)),
        };

        if (runtimeEvent.PhaseId is not null)
        {
            facts.Add(new RuntimeEventFact("phase_id", runtimeEvent.PhaseId));
        }

        if (runtimeEvent.PhaseKind.HasValue)
        {
            facts.Add(new RuntimeEventFact("phase_kind", StableEnumId(runtimeEvent.PhaseKind.Value)));
        }

        facts.AddRange(runtimeEvent.Facts);
        return facts.AsReadOnly();
    }

    private static bool IsCommission(RuntimeEvent runtimeEvent)
    {
        return HasFact(runtimeEvent, "expected_response", "withhold") ||
            HasFact(runtimeEvent, "response_expectation", "no_response_expected");
    }

    private static RuntimeEventFact RequiredFact(RuntimeEvent runtimeEvent, string name)
    {
        return runtimeEvent.Facts.FirstOrDefault(fact => string.Equals(fact.Name, name, StringComparison.Ordinal))
            ?? throw new ArgumentException(
                $"Runtime event cannot be converted to a scoring event without a '{name}' fact.",
                nameof(runtimeEvent));
    }

    private static bool HasFact(RuntimeEvent runtimeEvent, string name, string value)
    {
        return runtimeEvent.Facts.Any(fact =>
            string.Equals(fact.Name, name, StringComparison.Ordinal) &&
            string.Equals(fact.Value, value, StringComparison.Ordinal));
    }

    private static string StableRuntimeEventKind(RuntimeEventKind kind)
    {
        return kind switch
        {
            RuntimeEventKind.SessionStarted => "session_started",
            RuntimeEventKind.PhaseStarted => "phase_started",
            RuntimeEventKind.PhaseEnded => "phase_ended",
            RuntimeEventKind.PhaseTimedOut => "phase_timed_out",
            RuntimeEventKind.TimerTick => "timer_tick",
            RuntimeEventKind.CueEmitted => "cue_emitted",
            RuntimeEventKind.CueResponseSubmitted => "cue_response_submitted",
            RuntimeEventKind.UserAction => "user_action",
            RuntimeEventKind.AnswerSubmitted => "answer_submitted",
            RuntimeEventKind.DriftMarked => "drift_marked",
            RuntimeEventKind.GuessMarked => "guess_marked",
            RuntimeEventKind.AuditStarted => "audit_started",
            RuntimeEventKind.InterruptionRecorded => "interruption_recorded",
            RuntimeEventKind.CorrectionSubmitted => "correction_submitted",
            RuntimeEventKind.ErrorRecorded => "error_recorded",
            RuntimeEventKind.RecoveryStarted => "recovery_started",
            RuntimeEventKind.RecoveryCompleted => "recovery_completed",
            RuntimeEventKind.SessionPaused => "session_paused",
            RuntimeEventKind.SessionResumed => "session_resumed",
            RuntimeEventKind.SessionAbandoned => "session_abandoned",
            RuntimeEventKind.SessionCompleted => "session_completed",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown runtime event kind."),
        };
    }

    private static string StableEnumId<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var text = value.ToString();
        var builder = new System.Text.StringBuilder(text.Length + 4);
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (char.IsUpper(current) && index > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime scoring input value.");
        }
    }
}
