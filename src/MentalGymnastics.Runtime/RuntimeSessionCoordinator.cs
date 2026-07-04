using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Runtime;

public sealed class RuntimeSessionCoordinatorEventInput
{
    public RuntimeSessionCoordinatorEventInput(
        RuntimeEventKind kind,
        RuntimeInstant occurredAt,
        string? phaseId = null,
        RuntimeSessionPhaseKind? phaseKind = null,
        IEnumerable<RuntimeEventFact>? facts = null)
    {
        EnsureDefined(kind, nameof(kind));
        if (kind is RuntimeEventKind.SessionStarted or RuntimeEventKind.SessionCompleted or RuntimeEventKind.SessionAbandoned)
        {
            throw new ArgumentException(
                "Session coordinator event input must not include lifecycle boundary events.",
                nameof(kind));
        }

        if (phaseKind.HasValue)
        {
            EnsureDefined(phaseKind.Value, nameof(phaseKind));
        }

        var factArray = (facts ?? Array.Empty<RuntimeEventFact>()).ToArray();
        foreach (var fact in factArray)
        {
            ArgumentNullException.ThrowIfNull(fact);
        }

        Kind = kind;
        OccurredAt = occurredAt;
        PhaseId = NormalizeOptionalString(phaseId);
        PhaseKind = phaseKind;
        Facts = Array.AsReadOnly(factArray);
    }

    public RuntimeEventKind Kind { get; }

    public RuntimeInstant OccurredAt { get; }

    public string? PhaseId { get; }

    public RuntimeSessionPhaseKind? PhaseKind { get; }

    public IReadOnlyList<RuntimeEventFact> Facts { get; }

    private static string? NormalizeOptionalString(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime coordinator event value.");
        }
    }
}

public sealed class RuntimeSessionCoordinatorCoreInputs
{
    public RuntimeSessionCoordinatorCoreInputs(
        RuntimeStandardEvaluationHandoffInput? standardEvaluation = null,
        RuntimeFormalGateHandoffInput? formalGate = null,
        RuntimeReadinessPracticeHandoffInput? readinessPractice = null,
        RuntimeStabilizationCoreHandoffInput? stabilization = null,
        RuntimeMaintenanceCoreHandoffInput? maintenance = null,
        RuntimeTransferEligibilityHandoffInput? transfer = null,
        RuntimeFailureResponseHandoffInput? failureResponse = null)
    {
        if (standardEvaluation is null &&
            formalGate is null &&
            readinessPractice is null &&
            stabilization is null &&
            maintenance is null &&
            transfer is null &&
            failureResponse is null)
        {
            throw new ArgumentException(
                "Coordinator core inputs must include at least one core handoff target.",
                nameof(standardEvaluation));
        }

        StandardEvaluation = standardEvaluation;
        FormalGate = formalGate;
        ReadinessPractice = readinessPractice;
        Stabilization = stabilization;
        Maintenance = maintenance;
        Transfer = transfer;
        FailureResponse = failureResponse;
    }

    public RuntimeStandardEvaluationHandoffInput? StandardEvaluation { get; }

    public RuntimeFormalGateHandoffInput? FormalGate { get; }

    public RuntimeReadinessPracticeHandoffInput? ReadinessPractice { get; }

    public RuntimeStabilizationCoreHandoffInput? Stabilization { get; }

    public RuntimeMaintenanceCoreHandoffInput? Maintenance { get; }

    public RuntimeTransferEligibilityHandoffInput? Transfer { get; }

    public RuntimeFailureResponseHandoffInput? FailureResponse { get; }
}

public sealed class RuntimeSessionCoordinatorPersistenceInputs
{
    public RuntimeSessionCoordinatorPersistenceInputs(
        RuntimePersistenceHandoffMetadata metadata,
        RuntimeFormalAttemptPersistenceInput? formalAttempt = null,
        RuntimeStabilizationPersistenceInput? stabilization = null,
        RuntimeMaintenancePersistenceInput? maintenance = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        Metadata = metadata;
        FormalAttempt = formalAttempt;
        Stabilization = stabilization;
        Maintenance = maintenance;
    }

    public RuntimePersistenceHandoffMetadata Metadata { get; }

    public RuntimeFormalAttemptPersistenceInput? FormalAttempt { get; }

    public RuntimeStabilizationPersistenceInput? Stabilization { get; }

    public RuntimeMaintenancePersistenceInput? Maintenance { get; }
}

public sealed class RuntimeSessionCoordinatorRequest
{
    public RuntimeSessionCoordinatorRequest(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        RuntimeInstant startedAt,
        RuntimeInstant completedAt,
        RuntimeSessionCompletionStatus completionStatus,
        RuntimeEvidenceCaptureKind evidenceCaptureKind,
        IEnumerable<RuntimeCompletedSessionPhase> phaseHistory,
        IEnumerable<RuntimeSessionCoordinatorEventInput> events,
        RuntimeSessionCoordinatorCoreInputs? coreInputs = null,
        RuntimeSessionCoordinatorPersistenceInputs? persistenceInputs = null,
        IEnumerable<RuntimeEventFact>? completionFacts = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session coordinator session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        EnsureDefined(completionStatus, nameof(completionStatus));
        EnsureDefined(evidenceCaptureKind, nameof(evidenceCaptureKind));
        ArgumentNullException.ThrowIfNull(phaseHistory);
        ArgumentNullException.ThrowIfNull(events);

        if (completedAt.Offset < startedAt.Offset)
        {
            throw new ArgumentException(
                "Runtime session coordinator completion time cannot precede session start.",
                nameof(completedAt));
        }

        var phaseArray = phaseHistory.ToArray();
        foreach (var phase in phaseArray)
        {
            ArgumentNullException.ThrowIfNull(phase);
        }

        var eventArray = events.ToArray();
        foreach (var runtimeEvent in eventArray)
        {
            ArgumentNullException.ThrowIfNull(runtimeEvent);
        }

        var completionFactArray = (completionFacts ?? Array.Empty<RuntimeEventFact>()).ToArray();
        foreach (var fact in completionFactArray)
        {
            ArgumentNullException.ThrowIfNull(fact);
        }

        SessionId = sessionId;
        SessionDefinition = sessionDefinition;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        CompletionStatus = completionStatus;
        EvidenceCaptureKind = evidenceCaptureKind;
        PhaseHistory = Array.AsReadOnly(phaseArray);
        Events = Array.AsReadOnly(eventArray);
        CoreInputs = coreInputs;
        PersistenceInputs = persistenceInputs;
        CompletionFacts = Array.AsReadOnly(completionFactArray);
    }

    public string SessionId { get; }

    public RuntimeSessionDefinition SessionDefinition { get; }

    public RuntimeInstant StartedAt { get; }

    public RuntimeInstant CompletedAt { get; }

    public RuntimeSessionCompletionStatus CompletionStatus { get; }

    public RuntimeEvidenceCaptureKind EvidenceCaptureKind { get; }

    public IReadOnlyList<RuntimeCompletedSessionPhase> PhaseHistory { get; }

    public IReadOnlyList<RuntimeSessionCoordinatorEventInput> Events { get; }

    public RuntimeSessionCoordinatorCoreInputs? CoreInputs { get; }

    public RuntimeSessionCoordinatorPersistenceInputs? PersistenceInputs { get; }

    public IReadOnlyList<RuntimeEventFact> CompletionFacts { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown runtime coordinator request value.");
        }
    }
}

public sealed record RuntimeSessionCoordinatorResult(
    RuntimeSessionCompletionResult CompletionResult,
    RuntimeCoreEvaluationHandoff? CoreHandoff,
    RuntimePersistenceHandoffRecords? PersistenceHandoff);

public static class RuntimeSessionCoordinator
{
    public static RuntimeSessionCoordinatorResult Complete(RuntimeSessionCoordinatorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var eventLog = BuildEventLog(request);
        var scoringEvents = eventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        var evidenceDrafts = CaptureEvidenceDrafts(request, eventLog.Events, scoringEvents);
        var completion = RuntimeSessionCompletionResultGenerator.Generate(
            new RuntimeSessionCompletionResultRequest(
                request.SessionId,
                request.SessionDefinition,
                request.CompletionStatus,
                request.PhaseHistory,
                eventLog.Events,
                scoringEvents,
                evidenceDrafts));
        var coreHandoff = BuildCoreHandoff(request, completion);
        var persistenceHandoff = BuildPersistenceHandoff(request, completion);

        return new RuntimeSessionCoordinatorResult(completion, coreHandoff, persistenceHandoff);
    }

    private static RuntimeEventLog BuildEventLog(RuntimeSessionCoordinatorRequest request)
    {
        var eventLog = RuntimeEventLog.Start(
            request.SessionId,
            request.SessionDefinition,
            request.StartedAt);

        foreach (var input in request.Events)
        {
            eventLog.Append(
                input.Kind,
                input.OccurredAt,
                input.PhaseId,
                input.PhaseKind,
                input.Facts);
        }

        eventLog.Append(
            TerminalEventKind(request.CompletionStatus),
            request.CompletedAt,
            facts: request.CompletionFacts);

        return eventLog;
    }

    private static IReadOnlyList<RuntimeEvidenceDraft> CaptureEvidenceDrafts(
        RuntimeSessionCoordinatorRequest request,
        IReadOnlyList<RuntimeEvent> runtimeEvents,
        IReadOnlyList<RuntimeScoringEvent> scoringEvents)
    {
        if (request.CompletionStatus == RuntimeSessionCompletionStatus.Abandoned)
        {
            return Array.Empty<RuntimeEvidenceDraft>();
        }

        return
        [
            RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
                request.SessionId,
                request.SessionDefinition,
                DetermineEvidenceDate(request),
                request.EvidenceCaptureKind,
                runtimeEvents,
                scoringEvents)),
        ];
    }

    private static RuntimeCoreEvaluationHandoff? BuildCoreHandoff(
        RuntimeSessionCoordinatorRequest request,
        RuntimeSessionCompletionResult completion)
    {
        if (request.CoreInputs is null)
        {
            return null;
        }

        return RuntimeCoreEvaluationHandoffMapper.Map(new RuntimeCoreEvaluationHandoffRequest(
            completion,
            request.CoreInputs.StandardEvaluation,
            request.CoreInputs.FormalGate,
            request.CoreInputs.ReadinessPractice,
            request.CoreInputs.Stabilization,
            request.CoreInputs.Maintenance,
            request.CoreInputs.Transfer,
            request.CoreInputs.FailureResponse));
    }

    private static RuntimePersistenceHandoffRecords? BuildPersistenceHandoff(
        RuntimeSessionCoordinatorRequest request,
        RuntimeSessionCompletionResult completion)
    {
        if (request.PersistenceInputs is null)
        {
            return null;
        }

        return RuntimePersistenceHandoffMapper.Map(new RuntimePersistenceHandoffRequest(
            completion,
            request.PersistenceInputs.Metadata,
            request.PersistenceInputs.FormalAttempt,
            request.PersistenceInputs.Stabilization,
            request.PersistenceInputs.Maintenance));
    }

    private static RuntimeEventKind TerminalEventKind(RuntimeSessionCompletionStatus status)
    {
        return status == RuntimeSessionCompletionStatus.Abandoned
            ? RuntimeEventKind.SessionAbandoned
            : RuntimeEventKind.SessionCompleted;
    }

    private static TrainingDate DetermineEvidenceDate(RuntimeSessionCoordinatorRequest request)
    {
        if (request.PersistenceInputs is not null)
        {
            return request.PersistenceInputs.Metadata.Date;
        }

        if (request.CoreInputs?.FormalGate is not null)
        {
            return request.CoreInputs.FormalGate.Date;
        }

        if (request.CoreInputs?.Stabilization is not null)
        {
            return request.CoreInputs.Stabilization.Date;
        }

        if (request.CoreInputs?.Maintenance is not null)
        {
            return request.CoreInputs.Maintenance.Date;
        }

        return TrainingDate.From(1, 1, 1);
    }
}
