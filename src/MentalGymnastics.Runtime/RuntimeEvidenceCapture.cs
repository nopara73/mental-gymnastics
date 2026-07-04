using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum RuntimeEvidenceCaptureKind
{
    BestSet,
    FailedSet,
    BottleneckNote,
    FormalAttempt,
    Stabilization,
    Transfer,
    Maintenance,
    Audit,
    GlobalReview,
}

public sealed class RuntimeEvidenceCaptureRequest
{
    public RuntimeEvidenceCaptureRequest(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        TrainingDate date,
        RuntimeEvidenceCaptureKind captureKind,
        IEnumerable<RuntimeEvent> runtimeEvents,
        IEnumerable<RuntimeScoringEvent> scoringEvents)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime evidence capture session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        EnsureDefined(captureKind, nameof(captureKind));
        ArgumentNullException.ThrowIfNull(runtimeEvents);
        ArgumentNullException.ThrowIfNull(scoringEvents);

        var eventArray = runtimeEvents.ToArray();
        foreach (var runtimeEvent in eventArray)
        {
            ArgumentNullException.ThrowIfNull(runtimeEvent);
            if (!string.Equals(runtimeEvent.SessionId, sessionId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Runtime evidence source events must belong to the captured session.",
                    nameof(runtimeEvents));
            }
        }

        var scoringArray = scoringEvents.ToArray();
        foreach (var scoringEvent in scoringArray)
        {
            ArgumentNullException.ThrowIfNull(scoringEvent);
        }

        SessionId = sessionId;
        SessionDefinition = sessionDefinition;
        Date = date;
        CaptureKind = captureKind;
        RuntimeEvents = Array.AsReadOnly(eventArray);
        ScoringEvents = Array.AsReadOnly(scoringArray);
    }

    public string SessionId { get; }

    public RuntimeSessionDefinition SessionDefinition { get; }

    public TrainingDate Date { get; }

    public RuntimeEvidenceCaptureKind CaptureKind { get; }

    public IReadOnlyList<RuntimeEvent> RuntimeEvents { get; }

    public IReadOnlyList<RuntimeScoringEvent> ScoringEvents { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime evidence capture value.");
        }
    }
}

public sealed record RuntimeEvidenceDraft(
    string SessionId,
    RuntimeSessionDefinition SessionDefinition,
    RuntimeEvidenceCaptureKind CaptureKind,
    EvidenceArtifact Artifact,
    IReadOnlyList<RuntimeEvent> SourceEvents,
    IReadOnlyList<RuntimeScoringEvent> ScoringEvents);

public static class RuntimeEvidenceCapture
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

    public static RuntimeEvidenceDraft Capture(RuntimeEvidenceCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RuntimeEvents.Count == 0 && request.ScoringEvents.Count == 0)
        {
            throw new InvalidOperationException("Runtime evidence capture requires observable runtime events or scoring events.");
        }

        RejectProgressionDecisionFacts(request);

        var observableEvidence = BuildObservableEvidence(request)
            .DistinctBy(evidence => (evidence.Kind, evidence.Description), EqualityComparer<(ObservableEvidenceKind, string)>.Default)
            .ToArray();

        if (observableEvidence.Length == 0)
        {
            throw new InvalidOperationException("Runtime evidence capture found no observable facts for the requested artifact kind.");
        }

        var artifact = new EvidenceArtifact(
            DetermineCategory(request),
            request.Date,
            observableEvidence,
            BuildSummary(request));

        return new RuntimeEvidenceDraft(
            request.SessionId,
            request.SessionDefinition,
            request.CaptureKind,
            artifact,
            request.RuntimeEvents,
            request.ScoringEvents);
    }

    private static IEnumerable<ObservableEvidence> BuildObservableEvidence(RuntimeEvidenceCaptureRequest request)
    {
        var evidence = new List<ObservableEvidence>();

        switch (request.CaptureKind)
        {
            case RuntimeEvidenceCaptureKind.BestSet:
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.OutputSample, "output_sample", "best_set", "answer_reference");
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.Reconstruction, "reconstruction");
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.Comparison, "comparison");
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.RuleExplanation, "rule_explanation");
                AddScoreEvidence(evidence, request);
                break;
            case RuntimeEvidenceCaptureKind.FailedSet:
                AddFactListEvidence(evidence, request, ObservableEvidenceKind.FailedItemList, "failed_item_list", "failed_item", "error_kind");
                AddErrorCountEvidence(evidence, request);
                break;
            case RuntimeEvidenceCaptureKind.BottleneckNote:
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.BottleneckNote, "bottleneck_note");
                break;
            case RuntimeEvidenceCaptureKind.FormalAttempt:
                AddLoadVariableEvidence(evidence, request);
                AddCriticalConstraintEvidence(evidence, request);
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.Reconstruction, "reconstruction");
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.DelayedReconstruction, "delayed_reconstruction");
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.Comparison, "comparison");
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.RuleExplanation, "rule_explanation");
                AddScoreEvidence(evidence, request);
                AddErrorCountEvidence(evidence, request);
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.AuditResult, "audit_result", "audit_findings");
                break;
            case RuntimeEvidenceCaptureKind.Stabilization:
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.RepeatabilityRecord, "repeatability_record", "stabilization_condition", "condition");
                AddCriticalConstraintEvidence(evidence, request);
                AddScoreEvidence(evidence, request);
                break;
            case RuntimeEvidenceCaptureKind.Transfer:
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.BranchMapping, "branch_mapping", "transfer_mapping", "source_standard");
                AddCriticalConstraintEvidence(evidence, request);
                AddScoreEvidence(evidence, request);
                break;
            case RuntimeEvidenceCaptureKind.Maintenance:
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.MaintenanceCheck, "maintenance_check", "maintenance_result");
                AddCriticalConstraintEvidence(evidence, request);
                AddScoreEvidence(evidence, request);
                break;
            case RuntimeEvidenceCaptureKind.Audit:
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.AuditResult, "audit_result", "audit_findings", "correction_reference");
                break;
            case RuntimeEvidenceCaptureKind.GlobalReview:
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.GlobalReviewSummary, "global_review_summary");
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.BranchMapping, "branch_mapping", "component_branches");
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.AuditResult, "audit_result", "audit_findings");
                AddFirstFactEvidence(evidence, request, ObservableEvidenceKind.DelayedReconstruction, "delayed_reconstruction");
                AddCriticalConstraintEvidence(evidence, request);
                AddScoreEvidence(evidence, request);
                AddErrorCountEvidence(evidence, request);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.CaptureKind, "Unknown runtime evidence capture kind.");
        }

        return evidence;
    }

    private static EvidenceArtifactCategory DetermineCategory(RuntimeEvidenceCaptureRequest request)
    {
        return request.CaptureKind switch
        {
            RuntimeEvidenceCaptureKind.BestSet or
            RuntimeEvidenceCaptureKind.FailedSet or
            RuntimeEvidenceCaptureKind.BottleneckNote =>
                request.SessionDefinition.SessionType == SessionType.Load
                    ? EvidenceArtifactCategory.Load
                    : EvidenceArtifactCategory.Practice,
            RuntimeEvidenceCaptureKind.FormalAttempt => EvidenceArtifactCategory.Test,
            RuntimeEvidenceCaptureKind.Stabilization => EvidenceArtifactCategory.Stabilization,
            RuntimeEvidenceCaptureKind.Transfer => EvidenceArtifactCategory.Transfer,
            RuntimeEvidenceCaptureKind.Maintenance => EvidenceArtifactCategory.Maintenance,
            RuntimeEvidenceCaptureKind.Audit => CategoryForSession(request.SessionDefinition.SessionType),
            RuntimeEvidenceCaptureKind.GlobalReview => EvidenceArtifactCategory.GlobalReview,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.CaptureKind, "Unknown runtime evidence capture kind."),
        };
    }

    private static EvidenceArtifactCategory CategoryForSession(SessionType sessionType)
    {
        return sessionType switch
        {
            SessionType.Load => EvidenceArtifactCategory.Load,
            SessionType.Test => EvidenceArtifactCategory.Test,
            SessionType.Stabilization => EvidenceArtifactCategory.Stabilization,
            SessionType.Transfer => EvidenceArtifactCategory.Transfer,
            _ => EvidenceArtifactCategory.Practice,
        };
    }

    private static void AddLoadVariableEvidence(
        ICollection<ObservableEvidence> evidence,
        RuntimeEvidenceCaptureRequest request)
    {
        evidence.Add(new ObservableEvidence(
            ObservableEvidenceKind.LoadVariableRecord,
            string.Join("; ", request.SessionDefinition.LoadVariables.Select(variable => $"{variable.Name}={variable.Value}"))));
    }

    private static void AddCriticalConstraintEvidence(
        ICollection<ObservableEvidence> evidence,
        RuntimeEvidenceCaptureRequest request)
    {
        evidence.Add(new ObservableEvidence(
            ObservableEvidenceKind.CriticalConstraintRecord,
            string.Join("; ", request.SessionDefinition.CriticalConstraints.Select(constraint => constraint.Description))));
    }

    private static void AddScoreEvidence(
        ICollection<ObservableEvidence> evidence,
        RuntimeEvidenceCaptureRequest request)
    {
        var score = FirstFactValue(request, "score", "response_score", "rubric_result", "pass_fail");
        if (score is not null)
        {
            evidence.Add(new ObservableEvidence(ObservableEvidenceKind.Score, score));
            return;
        }

        var scoringSummary = ScoringSummary(request.ScoringEvents);
        if (scoringSummary is not null)
        {
            evidence.Add(new ObservableEvidence(ObservableEvidenceKind.Score, scoringSummary));
        }
    }

    private static void AddErrorCountEvidence(
        ICollection<ObservableEvidence> evidence,
        RuntimeEvidenceCaptureRequest request)
    {
        var explicitErrorCount = FirstFactValue(request, "error_count");
        if (explicitErrorCount is not null)
        {
            evidence.Add(new ObservableEvidence(ObservableEvidenceKind.ErrorCount, explicitErrorCount));
            return;
        }

        var errorSummary = ErrorSummary(request);
        if (errorSummary is not null)
        {
            evidence.Add(new ObservableEvidence(ObservableEvidenceKind.ErrorCount, errorSummary));
        }
    }

    private static void AddFirstFactEvidence(
        ICollection<ObservableEvidence> evidence,
        RuntimeEvidenceCaptureRequest request,
        ObservableEvidenceKind kind,
        params string[] factNames)
    {
        var value = FirstFactValue(request, factNames);
        if (value is not null)
        {
            evidence.Add(new ObservableEvidence(kind, value));
        }
    }

    private static void AddFactListEvidence(
        ICollection<ObservableEvidence> evidence,
        RuntimeEvidenceCaptureRequest request,
        ObservableEvidenceKind kind,
        params string[] factNames)
    {
        var values = FactValues(request, factNames).ToArray();
        if (values.Length > 0)
        {
            evidence.Add(new ObservableEvidence(kind, string.Join("; ", values)));
        }
    }

    private static string? FirstFactValue(RuntimeEvidenceCaptureRequest request, params string[] names)
    {
        return FactValues(request, names).FirstOrDefault();
    }

    private static IEnumerable<string> FactValues(RuntimeEvidenceCaptureRequest request, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var runtimeEvent in request.RuntimeEvents)
            {
                foreach (var fact in runtimeEvent.Facts)
                {
                    if (string.Equals(fact.Name, name, StringComparison.Ordinal))
                    {
                        yield return fact.Value;
                    }
                }
            }

            foreach (var scoringEvent in request.ScoringEvents)
            {
                foreach (var fact in scoringEvent.EvidenceFacts)
                {
                    if (string.Equals(fact.Name, name, StringComparison.Ordinal))
                    {
                        yield return fact.Value;
                    }
                }
            }
        }
    }

    private static string? ScoringSummary(IReadOnlyList<RuntimeScoringEvent> scoringEvents)
    {
        if (scoringEvents.Count == 0)
        {
            return null;
        }

        return string.Join(
            "; ",
            scoringEvents
                .GroupBy(scoringEvent => RuntimeScoringEvent.StableKind(scoringEvent.Kind))
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}={group.Count()}"));
    }

    private static string? ErrorSummary(RuntimeEvidenceCaptureRequest request)
    {
        var scoringErrors = request.ScoringEvents
            .Where(scoringEvent => scoringEvent.Kind != RuntimeScoringEventKind.CorrectResponse)
            .GroupBy(scoringEvent => RuntimeScoringEvent.StableKind(scoringEvent.Kind))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}={group.Count()}")
            .ToArray();

        if (scoringErrors.Length > 0)
        {
            return string.Join("; ", scoringErrors);
        }

        var runtimeErrorKinds = request.RuntimeEvents
            .SelectMany(runtimeEvent => runtimeEvent.Facts)
            .Where(fact => fact.Name == "error_kind")
            .GroupBy(fact => fact.Value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}={group.Count()}")
            .ToArray();

        return runtimeErrorKinds.Length == 0
            ? null
            : string.Join("; ", runtimeErrorKinds);
    }

    private static string BuildSummary(RuntimeEvidenceCaptureRequest request)
    {
        return $"{request.CaptureKind} evidence for {request.SessionDefinition.SessionType} " +
            $"{request.SessionDefinition.Branch} {request.SessionDefinition.Level} {request.SessionDefinition.Drill}.";
    }

    private static void RejectProgressionDecisionFacts(RuntimeEvidenceCaptureRequest request)
    {
        foreach (var fact in request.RuntimeEvents.SelectMany(runtimeEvent => runtimeEvent.Facts))
        {
            if (ProgressionDecisionFactNames.Contains(fact.Name))
            {
                throw new ArgumentException(
                    "Runtime evidence capture must not include progression or gate decision facts.",
                    nameof(request));
            }
        }

        foreach (var fact in request.ScoringEvents.SelectMany(scoringEvent => scoringEvent.EvidenceFacts))
        {
            if (ProgressionDecisionFactNames.Contains(fact.Name))
            {
                throw new ArgumentException(
                    "Runtime evidence capture must not include progression or gate decision facts.",
                    nameof(request));
            }
        }
    }
}
