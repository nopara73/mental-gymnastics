using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum RuntimeSessionCompletionStatus
{
    Completed,
    Failed,
    Abandoned,
    TimedOut,
}

public sealed class RuntimeSessionCompletionResultRequest
{
    public RuntimeSessionCompletionResultRequest(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        RuntimeSessionCompletionStatus completionStatus,
        IEnumerable<RuntimeCompletedSessionPhase> phaseHistory,
        IEnumerable<RuntimeEvent> runtimeEvents,
        IEnumerable<RuntimeScoringEvent> scoringEvents,
        IEnumerable<RuntimeEvidenceDraft> evidenceDrafts,
        IEnumerable<RuntimeEventFact>? failureRelevantFacts = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session completion result session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        EnsureDefined(completionStatus, nameof(completionStatus));
        ArgumentNullException.ThrowIfNull(phaseHistory);
        ArgumentNullException.ThrowIfNull(runtimeEvents);
        ArgumentNullException.ThrowIfNull(scoringEvents);
        ArgumentNullException.ThrowIfNull(evidenceDrafts);

        var phaseArray = phaseHistory.ToArray();
        foreach (var phase in phaseArray)
        {
            ArgumentNullException.ThrowIfNull(phase);
        }

        var eventArray = runtimeEvents.ToArray();
        foreach (var runtimeEvent in eventArray)
        {
            ArgumentNullException.ThrowIfNull(runtimeEvent);
            if (!string.Equals(runtimeEvent.SessionId, sessionId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Runtime completion events must belong to the completed session.",
                    nameof(runtimeEvents));
            }
        }

        var scoringArray = scoringEvents.ToArray();
        foreach (var scoringEvent in scoringArray)
        {
            ArgumentNullException.ThrowIfNull(scoringEvent);
        }

        var evidenceArray = evidenceDrafts.ToArray();
        foreach (var evidenceDraft in evidenceArray)
        {
            ArgumentNullException.ThrowIfNull(evidenceDraft);
            if (!string.Equals(evidenceDraft.SessionId, sessionId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Runtime completion evidence must belong to the completed session.",
                    nameof(evidenceDrafts));
            }

            if (!MatchesSessionDefinition(evidenceDraft.SessionDefinition, sessionDefinition))
            {
                throw new ArgumentException(
                    "Runtime completion evidence must match the completed session definition.",
                    nameof(evidenceDrafts));
            }
        }

        if (completionStatus == RuntimeSessionCompletionStatus.Abandoned && evidenceArray.Length > 0)
        {
            throw new ArgumentException(
                "Abandoned runtime sessions must not carry evidence drafts that can be mistaken for successful completion evidence.",
                nameof(evidenceDrafts));
        }

        if (completionStatus is RuntimeSessionCompletionStatus.Failed or RuntimeSessionCompletionStatus.TimedOut &&
            evidenceArray.Any(IsSuccessLookingEvidence))
        {
            throw new ArgumentException(
                "Failed or timed-out runtime sessions must not carry successful evidence drafts.",
                nameof(evidenceDrafts));
        }

        var failureFactArray = (failureRelevantFacts ?? Array.Empty<RuntimeEventFact>()).ToArray();
        foreach (var fact in failureFactArray)
        {
            ArgumentNullException.ThrowIfNull(fact);
        }

        SessionId = sessionId;
        SessionDefinition = sessionDefinition;
        CompletionStatus = completionStatus;
        PhaseHistory = Array.AsReadOnly(phaseArray);
        RuntimeEvents = Array.AsReadOnly(eventArray);
        ScoringEvents = Array.AsReadOnly(scoringArray);
        EvidenceDrafts = Array.AsReadOnly(evidenceArray);
        ExplicitFailureRelevantFacts = Array.AsReadOnly(failureFactArray);
    }

    public string SessionId { get; }

    public RuntimeSessionDefinition SessionDefinition { get; }

    public RuntimeSessionCompletionStatus CompletionStatus { get; }

    public IReadOnlyList<RuntimeCompletedSessionPhase> PhaseHistory { get; }

    public IReadOnlyList<RuntimeEvent> RuntimeEvents { get; }

    public IReadOnlyList<RuntimeScoringEvent> ScoringEvents { get; }

    public IReadOnlyList<RuntimeEvidenceDraft> EvidenceDrafts { get; }

    public IReadOnlyList<RuntimeEventFact> ExplicitFailureRelevantFacts { get; }

    internal static bool MatchesSessionDefinition(
        RuntimeSessionDefinition left,
        RuntimeSessionDefinition right)
    {
        return left.SessionType == right.SessionType &&
            left.Branch == right.Branch &&
            left.Level == right.Level &&
            left.Drill == right.Drill &&
            left.GeneratedDrillInstance?.InstanceId == right.GeneratedDrillInstance?.InstanceId;
    }

    private static bool IsSuccessLookingEvidence(RuntimeEvidenceDraft evidenceDraft)
    {
        return evidenceDraft.CaptureKind == RuntimeEvidenceCaptureKind.BestSet;
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime completion value.");
        }
    }
}

public sealed record RuntimeSessionEvidenceSummary(
    int ArtifactCount,
    IReadOnlyList<EvidenceArtifactCategory> Categories,
    IReadOnlyList<ObservableEvidenceKind> ObservableEvidenceKinds,
    IReadOnlyList<RuntimeEventFact> SummaryFacts);

public sealed class RuntimeSessionCompletionResult
{
    public RuntimeSessionCompletionResult(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        RuntimeSessionCompletionStatus completionStatus,
        IEnumerable<RuntimeCompletedSessionPhase> phaseHistory,
        IEnumerable<RuntimeEvent> runtimeEvents,
        IEnumerable<RuntimeScoringEvent> scoringEvents,
        IEnumerable<RuntimeEventFact> scoringFacts,
        IEnumerable<RuntimeEvidenceDraft> evidenceDrafts,
        RuntimeSessionEvidenceSummary evidenceSummary,
        IEnumerable<RuntimeEventFact> failureRelevantFacts,
        IEnumerable<RuntimeEventFact> resultFacts,
        RuntimeInstant? completedAt)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session completion result session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(phaseHistory);
        ArgumentNullException.ThrowIfNull(runtimeEvents);
        ArgumentNullException.ThrowIfNull(scoringEvents);
        ArgumentNullException.ThrowIfNull(scoringFacts);
        ArgumentNullException.ThrowIfNull(evidenceDrafts);
        ArgumentNullException.ThrowIfNull(evidenceSummary);
        ArgumentNullException.ThrowIfNull(failureRelevantFacts);
        ArgumentNullException.ThrowIfNull(resultFacts);

        SessionId = sessionId;
        SessionDefinition = sessionDefinition;
        CompletionStatus = completionStatus;
        PhaseHistory = Array.AsReadOnly(phaseHistory.ToArray());
        RuntimeEvents = Array.AsReadOnly(runtimeEvents.ToArray());
        ScoringEvents = Array.AsReadOnly(scoringEvents.ToArray());
        ScoringFacts = Array.AsReadOnly(scoringFacts.ToArray());
        EvidenceDrafts = Array.AsReadOnly(evidenceDrafts.ToArray());
        EvidenceSummary = evidenceSummary;
        FailureRelevantFacts = Array.AsReadOnly(failureRelevantFacts.ToArray());
        ResultFacts = Array.AsReadOnly(resultFacts.ToArray());
        CompletedAt = completedAt;
    }

    public string SessionId { get; }

    public RuntimeSessionDefinition SessionDefinition { get; }

    public SessionType SessionType => SessionDefinition.SessionType;

    public BranchCode Branch => SessionDefinition.Branch;

    public GlobalLevelId Level => SessionDefinition.Level;

    public DrillId Drill => SessionDefinition.Drill;

    public IReadOnlyList<LoadVariable> LoadVariables => SessionDefinition.LoadVariables;

    public RuntimeSessionCompletionStatus CompletionStatus { get; }

    public IReadOnlyList<RuntimeCompletedSessionPhase> PhaseHistory { get; }

    public IReadOnlyList<RuntimeEvent> RuntimeEvents { get; }

    public IReadOnlyList<RuntimeScoringEvent> ScoringEvents { get; }

    public IReadOnlyList<RuntimeEventFact> ScoringFacts { get; }

    public IReadOnlyList<RuntimeEvidenceDraft> EvidenceDrafts { get; }

    public RuntimeSessionEvidenceSummary EvidenceSummary { get; }

    public IReadOnlyList<RuntimeEventFact> FailureRelevantFacts { get; }

    public IReadOnlyList<RuntimeEventFact> ResultFacts { get; }

    public RuntimeInstant? CompletedAt { get; }

    public bool ContainsAdvancementDecision =>
        RuntimeSessionCompletionResultGenerator.ContainsProgressionDecisionFact(ResultFacts) ||
        RuntimeSessionCompletionResultGenerator.ContainsProgressionDecisionFact(ScoringFacts) ||
        RuntimeSessionCompletionResultGenerator.ContainsProgressionDecisionFact(FailureRelevantFacts) ||
        RuntimeSessionCompletionResultGenerator.ContainsProgressionDecisionFact(
            RuntimeEvents.SelectMany(runtimeEvent => runtimeEvent.Facts));
}

public static class RuntimeSessionCompletionResultGenerator
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

    public static RuntimeSessionCompletionResult Generate(RuntimeSessionCompletionResultRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RejectProgressionDecisionFacts(request);

        var scoringFacts = request.ScoringEvents
            .SelectMany(scoringEvent => scoringEvent.EvidenceFacts)
            .ToArray();
        var evidenceSummary = BuildEvidenceSummary(request.EvidenceDrafts);
        var failureRelevantFacts = BuildFailureRelevantFacts(request, scoringFacts);
        var resultFacts = BuildResultFacts(request, evidenceSummary);
        var completedAt = DetermineCompletedAt(request);

        return new RuntimeSessionCompletionResult(
            request.SessionId,
            request.SessionDefinition,
            request.CompletionStatus,
            request.PhaseHistory,
            request.RuntimeEvents,
            request.ScoringEvents,
            scoringFacts,
            request.EvidenceDrafts,
            evidenceSummary,
            failureRelevantFacts,
            resultFacts,
            completedAt);
    }

    internal static bool ContainsProgressionDecisionFact(IEnumerable<RuntimeEventFact> facts)
    {
        return facts.Any(fact => ProgressionDecisionFactNames.Contains(fact.Name));
    }

    private static RuntimeSessionEvidenceSummary BuildEvidenceSummary(
        IReadOnlyList<RuntimeEvidenceDraft> evidenceDrafts)
    {
        var categories = evidenceDrafts
            .Select(draft => draft.Artifact.Category)
            .Distinct()
            .ToArray();
        var observableEvidenceKinds = evidenceDrafts
            .SelectMany(draft => draft.Artifact.ObservableEvidence)
            .Select(evidence => evidence.Kind)
            .Distinct()
            .ToArray();

        var facts = new List<RuntimeEventFact>
        {
            new("evidence_artifact_count", evidenceDrafts.Count.ToString(CultureInfo.InvariantCulture)),
        };
        facts.AddRange(categories.Select(category => new RuntimeEventFact("evidence_category", StableEnumId(category))));
        facts.AddRange(observableEvidenceKinds.Select(kind => new RuntimeEventFact("observable_evidence_kind", StableEnumId(kind))));

        return new RuntimeSessionEvidenceSummary(
            evidenceDrafts.Count,
            Array.AsReadOnly(categories),
            Array.AsReadOnly(observableEvidenceKinds),
            facts.AsReadOnly());
    }

    private static IReadOnlyList<RuntimeEventFact> BuildFailureRelevantFacts(
        RuntimeSessionCompletionResultRequest request,
        IReadOnlyList<RuntimeEventFact> scoringFacts)
    {
        var facts = new List<RuntimeEventFact>();
        facts.AddRange(request.ExplicitFailureRelevantFacts);

        if (request.CompletionStatus == RuntimeSessionCompletionStatus.Abandoned)
        {
            AddRuntimeFactsByName(facts, request.RuntimeEvents, "abandon_reason");
            AddRuntimeFactsByPrefix(facts, request.RuntimeEvents, "error", "failed", "failure", "guard", "attempt");
        }

        if (request.CompletionStatus == RuntimeSessionCompletionStatus.TimedOut)
        {
            AddRuntimeFactsByName(
                facts,
                request.RuntimeEvents,
                "phase_deadline",
                "phase_elapsed",
                "phase_remaining",
                "timeout_overtime");
            AddScoringFactsByName(facts, scoringFacts, "scoring_event_kind", "timeout_overtime");
        }

        if (request.CompletionStatus == RuntimeSessionCompletionStatus.Failed)
        {
            AddRuntimeFactsByPrefix(facts, request.RuntimeEvents, "error", "failed", "failure");
            AddScoringFactsByName(facts, scoringFacts, "scoring_event_kind", "error_kind", "failed_constraint", "failure_type_candidate");
        }

        return facts
            .DistinctBy(fact => (fact.Name, fact.Value), EqualityComparer<(string, string)>.Default)
            .ToArray();
    }

    private static IReadOnlyList<RuntimeEventFact> BuildResultFacts(
        RuntimeSessionCompletionResultRequest request,
        RuntimeSessionEvidenceSummary evidenceSummary)
    {
        var facts = new List<RuntimeEventFact>
        {
            new("session_id", request.SessionId),
            new("completion_status", StableCompletionStatus(request.CompletionStatus)),
            new("session_type", StableEnumId(request.SessionDefinition.SessionType)),
            new("branch", request.SessionDefinition.Branch.ToString()),
            new("level", request.SessionDefinition.Level.ToString()),
            new("drill", StableEnumId(request.SessionDefinition.Drill)),
            new("phase_count", request.PhaseHistory.Count.ToString(CultureInfo.InvariantCulture)),
            new("runtime_event_count", request.RuntimeEvents.Count.ToString(CultureInfo.InvariantCulture)),
            new("scoring_event_count", request.ScoringEvents.Count.ToString(CultureInfo.InvariantCulture)),
            new("evidence_artifact_count", evidenceSummary.ArtifactCount.ToString(CultureInfo.InvariantCulture)),
        };

        foreach (var loadVariable in request.SessionDefinition.LoadVariables)
        {
            facts.Add(new RuntimeEventFact("load_variable", $"{loadVariable.Name}={loadVariable.Value}"));
        }

        foreach (var phase in request.PhaseHistory)
        {
            facts.Add(new RuntimeEventFact("phase_history", $"{phase.Definition.Id}:{StableEnumId(phase.CompletionCause)}"));
        }

        return facts.AsReadOnly();
    }

    private static RuntimeInstant? DetermineCompletedAt(RuntimeSessionCompletionResultRequest request)
    {
        if (request.RuntimeEvents.Count > 0)
        {
            return request.RuntimeEvents[^1].OccurredAt;
        }

        if (request.PhaseHistory.Count > 0)
        {
            return request.PhaseHistory[^1].CompletedAt;
        }

        return null;
    }

    private static void AddRuntimeFactsByName(
        ICollection<RuntimeEventFact> target,
        IEnumerable<RuntimeEvent> runtimeEvents,
        params string[] names)
    {
        var nameSet = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var fact in runtimeEvents
            .SelectMany(runtimeEvent => runtimeEvent.Facts)
            .Where(fact => nameSet.Contains(fact.Name)))
        {
            target.Add(fact);
        }
    }

    private static void AddScoringFactsByName(
        ICollection<RuntimeEventFact> target,
        IEnumerable<RuntimeEventFact> scoringFacts,
        params string[] names)
    {
        var nameSet = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var fact in scoringFacts.Where(fact => nameSet.Contains(fact.Name)))
        {
            target.Add(fact);
        }
    }

    private static void AddRuntimeFactsByPrefix(
        ICollection<RuntimeEventFact> target,
        IEnumerable<RuntimeEvent> runtimeEvents,
        params string[] prefixes)
    {
        foreach (var fact in runtimeEvents
            .SelectMany(runtimeEvent => runtimeEvent.Facts)
            .Where(fact => prefixes.Any(prefix => fact.Name.StartsWith(prefix, StringComparison.Ordinal))))
        {
            target.Add(fact);
        }
    }

    private static void RejectProgressionDecisionFacts(RuntimeSessionCompletionResultRequest request)
    {
        if (ContainsProgressionDecisionFact(request.RuntimeEvents.SelectMany(runtimeEvent => runtimeEvent.Facts)) ||
            ContainsProgressionDecisionFact(request.ScoringEvents.SelectMany(scoringEvent => scoringEvent.EvidenceFacts)) ||
            ContainsProgressionDecisionFact(request.ExplicitFailureRelevantFacts))
        {
            throw new ArgumentException(
                "Runtime session completion results must not contain progression or gate decision facts.",
                nameof(request));
        }
    }

    private static string StableCompletionStatus(RuntimeSessionCompletionStatus status)
    {
        return status switch
        {
            RuntimeSessionCompletionStatus.Completed => "completed",
            RuntimeSessionCompletionStatus.Failed => "failed",
            RuntimeSessionCompletionStatus.Abandoned => "abandoned",
            RuntimeSessionCompletionStatus.TimedOut => "timed_out",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown runtime completion status."),
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
}
