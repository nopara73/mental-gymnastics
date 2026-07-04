namespace MentalGymnastics.Runtime;

public enum RuntimeAntiSelfDeceptionBypassKind
{
    SkippedPhase,
    ChangedTarget,
    UnallowedRereading,
    HiddenNotesWhereProhibited,
    UnmarkedGuessWhereRequired,
    PrematureResponse,
    InvalidRestart,
    BranchSpecificEvidenceRemoved,
    AbandonedEvidence,
}

public enum RuntimeAntiSelfDeceptionGuardDisposition
{
    Prevented,
    Recorded,
}

public sealed class RuntimeAntiSelfDeceptionGuardRequest
{
    public RuntimeAntiSelfDeceptionGuardRequest(
        RuntimeAntiSelfDeceptionBypassKind bypassKind,
        RuntimeSessionDefinition sessionDefinition,
        string? phaseId = null,
        RuntimeSessionPhaseKind? phaseKind = null,
        IEnumerable<RuntimeEventFact>? contextFacts = null)
    {
        EnsureDefined(bypassKind, nameof(bypassKind));
        ArgumentNullException.ThrowIfNull(sessionDefinition);

        if (phaseId is not null && string.IsNullOrWhiteSpace(phaseId))
        {
            throw new ArgumentException("Runtime guard phase id cannot be blank.", nameof(phaseId));
        }

        if (phaseKind.HasValue)
        {
            EnsureDefined(phaseKind.Value, nameof(phaseKind));
        }

        var factArray = (contextFacts ?? Array.Empty<RuntimeEventFact>()).ToArray();
        foreach (var fact in factArray)
        {
            ArgumentNullException.ThrowIfNull(fact);
        }

        BypassKind = bypassKind;
        SessionDefinition = sessionDefinition;
        PhaseId = phaseId;
        PhaseKind = phaseKind;
        ContextFacts = Array.AsReadOnly(factArray);
    }

    public RuntimeAntiSelfDeceptionBypassKind BypassKind { get; }

    public RuntimeSessionDefinition SessionDefinition { get; }

    public string? PhaseId { get; }

    public RuntimeSessionPhaseKind? PhaseKind { get; }

    public IReadOnlyList<RuntimeEventFact> ContextFacts { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime anti-self-deception guard value.");
        }
    }
}

public sealed class RuntimeAntiSelfDeceptionGuardResult
{
    internal RuntimeAntiSelfDeceptionGuardResult(
        RuntimeAntiSelfDeceptionGuardDisposition disposition,
        RuntimeAntiSelfDeceptionBypassKind bypassKind,
        string? phaseId,
        RuntimeSessionPhaseKind? phaseKind,
        IEnumerable<RuntimeEventFact> evidenceFacts)
    {
        EnsureDefined(disposition, nameof(disposition));
        EnsureDefined(bypassKind, nameof(bypassKind));

        if (phaseId is not null && string.IsNullOrWhiteSpace(phaseId))
        {
            throw new ArgumentException("Runtime guard result phase id cannot be blank.", nameof(phaseId));
        }

        if (phaseKind.HasValue)
        {
            EnsureDefined(phaseKind.Value, nameof(phaseKind));
        }

        ArgumentNullException.ThrowIfNull(evidenceFacts);
        var factArray = evidenceFacts.ToArray();
        foreach (var fact in factArray)
        {
            ArgumentNullException.ThrowIfNull(fact);
        }

        Disposition = disposition;
        BypassKind = bypassKind;
        PhaseId = phaseId;
        PhaseKind = phaseKind;
        EvidenceFacts = Array.AsReadOnly(factArray);
    }

    public RuntimeAntiSelfDeceptionGuardDisposition Disposition { get; }

    public RuntimeAntiSelfDeceptionBypassKind BypassKind { get; }

    public bool AttemptAllowed => Disposition == RuntimeAntiSelfDeceptionGuardDisposition.Recorded;

    public string? PhaseId { get; }

    public RuntimeSessionPhaseKind? PhaseKind { get; }

    public IReadOnlyList<RuntimeEventFact> EvidenceFacts { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime anti-self-deception guard result value.");
        }
    }
}

public static class RuntimeAntiSelfDeceptionGuard
{
    public static RuntimeAntiSelfDeceptionGuardResult Prevent(RuntimeAntiSelfDeceptionGuardRequest request)
    {
        return Evaluate(request, RuntimeAntiSelfDeceptionGuardDisposition.Prevented);
    }

    public static RuntimeAntiSelfDeceptionGuardResult Record(RuntimeAntiSelfDeceptionGuardRequest request)
    {
        return Evaluate(request, RuntimeAntiSelfDeceptionGuardDisposition.Recorded);
    }

    public static RuntimeEvent AppendEvidence(
        RuntimeEventLog eventLog,
        RuntimeAntiSelfDeceptionGuardResult result,
        RuntimeInstant occurredAt)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        ArgumentNullException.ThrowIfNull(result);

        return eventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            occurredAt,
            result.PhaseId,
            result.PhaseKind,
            result.EvidenceFacts);
    }

    private static RuntimeAntiSelfDeceptionGuardResult Evaluate(
        RuntimeAntiSelfDeceptionGuardRequest request,
        RuntimeAntiSelfDeceptionGuardDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = RuntimeAntiSelfDeceptionBypassDefinition.For(request.BypassKind);
        var evidenceFacts = new List<RuntimeEventFact>
        {
            new("anti_self_deception_guard", "true"),
            new("guard_violation", definition.StableViolation),
            new("error_kind", definition.ErrorKind),
            new("failed_constraint", definition.FailedConstraint),
            new("attempt_prevented", disposition == RuntimeAntiSelfDeceptionGuardDisposition.Prevented ? "true" : "false"),
            new("attempt_recorded", "true"),
            new("failure_type_candidate", definition.FailureTypeCandidate),
            new("branch", request.SessionDefinition.Branch.ToString()),
            new("level", request.SessionDefinition.Level.ToString()),
            new("drill", StableEnumId(request.SessionDefinition.Drill)),
            new("session_type", StableEnumId(request.SessionDefinition.SessionType)),
        };

        if (request.SessionDefinition.GeneratedDrillInstance is not null)
        {
            evidenceFacts.Add(new RuntimeEventFact(
                "generated_drill_instance_id",
                request.SessionDefinition.GeneratedDrillInstance.InstanceId));
        }

        evidenceFacts.AddRange(request.ContextFacts);
        return new RuntimeAntiSelfDeceptionGuardResult(
            disposition,
            request.BypassKind,
            request.PhaseId,
            request.PhaseKind,
            evidenceFacts);
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
}

internal sealed record RuntimeAntiSelfDeceptionBypassDefinition(
    string StableViolation,
    string ErrorKind,
    string FailedConstraint,
    string FailureTypeCandidate)
{
    public static RuntimeAntiSelfDeceptionBypassDefinition For(RuntimeAntiSelfDeceptionBypassKind bypassKind)
    {
        return bypassKind switch
        {
            RuntimeAntiSelfDeceptionBypassKind.SkippedPhase => new(
                "skipped_phase",
                "skipped_phase",
                "phase_order_required",
                "bad_programming"),
            RuntimeAntiSelfDeceptionBypassKind.ChangedTarget => new(
                "changed_target",
                "target_substitution",
                "no_target_substitution",
                "effort_failure"),
            RuntimeAntiSelfDeceptionBypassKind.UnallowedRereading => new(
                "unallowed_rereading",
                "reread_after_encode",
                "no_rereading_after_encode",
                "effort_failure"),
            RuntimeAntiSelfDeceptionBypassKind.HiddenNotesWhereProhibited => new(
                "hidden_notes_where_prohibited",
                "hidden_intermediate_notes",
                "intermediate_notes_prohibited",
                "effort_failure"),
            RuntimeAntiSelfDeceptionBypassKind.UnmarkedGuessWhereRequired => new(
                "unmarked_guess",
                "unmarked_guess",
                "guess_marking_required",
                "effort_failure"),
            RuntimeAntiSelfDeceptionBypassKind.PrematureResponse => new(
                "premature_response",
                "premature_response",
                "no_premature_response",
                "effort_failure"),
            RuntimeAntiSelfDeceptionBypassKind.InvalidRestart => new(
                "invalid_restart",
                "full_restart_attempt",
                "no_full_restart_unless_allowed",
                "effort_failure"),
            RuntimeAntiSelfDeceptionBypassKind.BranchSpecificEvidenceRemoved => new(
                "branch_specific_evidence_removed",
                "branch_specific_evidence_removed",
                "branch_specific_evidence_required",
                "effort_failure"),
            RuntimeAntiSelfDeceptionBypassKind.AbandonedEvidence => new(
                "abandoned_evidence",
                "abandoned_evidence",
                "abandoned_session_cannot_supply_success_evidence",
                "effort_failure"),
            _ => throw new ArgumentOutOfRangeException(nameof(bypassKind), bypassKind, "Unknown runtime anti-self-deception bypass kind."),
        };
    }
}
