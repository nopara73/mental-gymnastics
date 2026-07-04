using System.Globalization;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Runtime;

public sealed class RuntimePersistenceHandoffMetadata
{
    public RuntimePersistenceHandoffMetadata(
        TrainingDate date,
        LocalSessionIntensity intensity,
        bool cleanPerformance,
        string notes,
        string? transferTask = null,
        bool recoveryMarked = false,
        bool deloadMarked = false,
        TrainingDate? generatedInstanceDate = null)
    {
        EnsureDefined(intensity, nameof(intensity));

        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new ArgumentException(
                "Persistence handoff metadata must include completed session notes.",
                nameof(notes));
        }

        Date = date;
        Intensity = intensity;
        CleanPerformance = cleanPerformance;
        Notes = notes;
        TransferTask = NormalizeOptionalString(transferTask);
        RecoveryMarked = recoveryMarked;
        DeloadMarked = deloadMarked;
        GeneratedInstanceDate = generatedInstanceDate ?? date;
    }

    public TrainingDate Date { get; }

    public LocalSessionIntensity Intensity { get; }

    public bool CleanPerformance { get; }

    public string Notes { get; }

    public string? TransferTask { get; }

    public bool RecoveryMarked { get; }

    public bool DeloadMarked { get; }

    public TrainingDate GeneratedInstanceDate { get; }

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
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown persistence handoff metadata value.");
        }
    }
}

public sealed class RuntimeFormalAttemptPersistenceInput
{
    public RuntimeFormalAttemptPersistenceInput(
        TestResultEvidence resultEvidence,
        FormalTestPassState passState,
        FailureType? failureType = null,
        string? attemptId = null,
        TestTask? task = null)
    {
        ArgumentNullException.ThrowIfNull(resultEvidence);
        EnsureDefined(passState, nameof(passState));

        if (failureType.HasValue)
        {
            EnsureDefined(failureType.Value, nameof(failureType));
        }

        ResultEvidence = resultEvidence;
        PassState = passState;
        FailureType = failureType;
        AttemptId = NormalizeOptionalString(attemptId);
        Task = task;
    }

    public TestResultEvidence ResultEvidence { get; }

    public FormalTestPassState PassState { get; }

    public FailureType? FailureType { get; }

    public string? AttemptId { get; }

    public TestTask? Task { get; }

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
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown formal attempt handoff value.");
        }
    }
}

public sealed class RuntimeStabilizationPersistenceInput
{
    public RuntimeStabilizationPersistenceInput(
        StandardEvaluationResult standardEvaluationResult,
        FormalTestPassState passState,
        LocalStabilizationCondition condition,
        string conditionDescription,
        string mainFailureModeAvoided,
        string? passId = null,
        string? formalTestAttemptId = null)
    {
        ArgumentNullException.ThrowIfNull(standardEvaluationResult);
        EnsureDefined(passState, nameof(passState));
        EnsureDefined(condition, nameof(condition));

        if (string.IsNullOrWhiteSpace(conditionDescription))
        {
            throw new ArgumentException(
                "Stabilization handoff must describe the stabilization condition.",
                nameof(conditionDescription));
        }

        if (string.IsNullOrWhiteSpace(mainFailureModeAvoided))
        {
            throw new ArgumentException(
                "Stabilization handoff must identify the main failure mode avoided.",
                nameof(mainFailureModeAvoided));
        }

        StandardEvaluationResult = standardEvaluationResult;
        PassState = passState;
        Condition = condition;
        ConditionDescription = conditionDescription;
        MainFailureModeAvoided = mainFailureModeAvoided;
        PassId = NormalizeOptionalString(passId);
        FormalTestAttemptId = NormalizeOptionalString(formalTestAttemptId);
    }

    public StandardEvaluationResult StandardEvaluationResult { get; }

    public FormalTestPassState PassState { get; }

    public LocalStabilizationCondition Condition { get; }

    public string ConditionDescription { get; }

    public string MainFailureModeAvoided { get; }

    public string? PassId { get; }

    public string? FormalTestAttemptId { get; }

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
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown stabilization handoff value.");
        }
    }
}

public sealed class RuntimeMaintenancePersistenceInput
{
    public RuntimeMaintenancePersistenceInput(
        GlobalLevelId ownedLevel,
        MaintenanceCheckKind kind,
        StandardEvaluationResult standardEvaluationResult,
        string? checkId = null)
    {
        EnsureDefined(ownedLevel, nameof(ownedLevel));
        EnsureDefined(kind, nameof(kind));
        ArgumentNullException.ThrowIfNull(standardEvaluationResult);

        OwnedLevel = ownedLevel;
        Kind = kind;
        StandardEvaluationResult = standardEvaluationResult;
        CheckId = NormalizeOptionalString(checkId);
    }

    public GlobalLevelId OwnedLevel { get; }

    public MaintenanceCheckKind Kind { get; }

    public StandardEvaluationResult StandardEvaluationResult { get; }

    public string? CheckId { get; }

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
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown maintenance handoff value.");
        }
    }
}

public sealed class RuntimePersistenceHandoffRequest
{
    public RuntimePersistenceHandoffRequest(
        RuntimeSessionCompletionResult result,
        RuntimePersistenceHandoffMetadata metadata,
        RuntimeFormalAttemptPersistenceInput? formalAttempt = null,
        RuntimeStabilizationPersistenceInput? stabilization = null,
        RuntimeMaintenancePersistenceInput? maintenance = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(metadata);

        if (result.CompletionStatus == RuntimeSessionCompletionStatus.Abandoned)
        {
            throw new ArgumentException(
                "Abandoned runtime sessions cannot be converted into completed persistence evidence.",
                nameof(result));
        }

        if (result.EvidenceDrafts.Count == 0)
        {
            throw new ArgumentException(
                "Runtime persistence handoff requires at least one evidence draft.",
                nameof(result));
        }

        if (result.ContainsAdvancementDecision)
        {
            throw new ArgumentException(
                "Runtime persistence handoff must not carry progression or gate decisions.",
                nameof(result));
        }

        Result = result;
        Metadata = metadata;
        FormalAttempt = formalAttempt;
        Stabilization = stabilization;
        Maintenance = maintenance;
    }

    public RuntimeSessionCompletionResult Result { get; }

    public RuntimePersistenceHandoffMetadata Metadata { get; }

    public RuntimeFormalAttemptPersistenceInput? FormalAttempt { get; }

    public RuntimeStabilizationPersistenceInput? Stabilization { get; }

    public RuntimeMaintenancePersistenceInput? Maintenance { get; }
}

public sealed record RuntimePersistenceHandoffRecords(
    LocalSessionHistoryRecord SessionHistory,
    IReadOnlyList<LocalEvidenceArtifactRecord> EvidenceArtifacts,
    LocalFormalTestAttemptRecord? FormalTestAttempt,
    LocalStabilizationPassRecord? StabilizationPass,
    LocalMaintenanceCheckRecord? MaintenanceCheck,
    LocalGeneratedDrillInstanceRecord? GeneratedDrillInstance);

public static class RuntimePersistenceHandoffMapper
{
    public static RuntimePersistenceHandoffRecords Map(RuntimePersistenceHandoffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ids = RuntimePersistenceHandoffIds.From(request);
        var artifacts = BuildEvidenceArtifactRecords(request, ids);
        var sessionHistory = BuildSessionHistoryRecord(request, artifacts);
        var formalAttempt = BuildFormalAttemptRecord(request, ids, artifacts);
        var stabilizationPass = BuildStabilizationPassRecord(request, ids, artifacts, formalAttempt);
        var maintenanceCheck = BuildMaintenanceCheckRecord(request, ids, artifacts);
        var generatedDrillInstance = BuildGeneratedDrillInstanceRecord(request, artifacts);
        var artifactRecords = artifacts
            .Select(artifact => artifact.Record)
            .ToArray();

        return new RuntimePersistenceHandoffRecords(
            sessionHistory,
            Array.AsReadOnly(artifactRecords),
            formalAttempt,
            stabilizationPass,
            maintenanceCheck,
            generatedDrillInstance);
    }

    private static LocalSessionHistoryRecord BuildSessionHistoryRecord(
        RuntimePersistenceHandoffRequest request,
        IReadOnlyList<RuntimeEvidenceArtifactRecordContext> artifacts)
    {
        var result = request.Result;
        var metadata = request.Metadata;

        return new LocalSessionHistoryRecord(
            result.SessionId,
            metadata.Date,
            MapSessionType(result.SessionType),
            [new LocalSessionBranchLevel(result.Branch, result.Level)],
            result.Drill,
            metadata.TransferTask,
            metadata.Intensity,
            result.LoadVariables,
            metadata.CleanPerformance,
            metadata.Notes,
            metadata.RecoveryMarked,
            metadata.DeloadMarked,
            artifacts.Select(artifact => artifact.Record.ArtifactId));
    }

    private static RuntimeEvidenceArtifactRecordContext[] BuildEvidenceArtifactRecords(
        RuntimePersistenceHandoffRequest request,
        RuntimePersistenceHandoffIds ids)
    {
        var result = request.Result;
        return result.EvidenceDrafts
            .Select((draft, index) =>
            {
                var artifactId = $"{result.SessionId}-artifact-{(index + 1).ToString(CultureInfo.InvariantCulture)}";
                var eventKind = MapEventKind(draft.Artifact.Category);
                var eventId = EventIdForArtifact(result.SessionId, draft.Artifact.Category, ids);
                var eventReference = eventKind == LocalProgrammingEventKind.GlobalReview
                    ? new LocalProgrammingEventReference(eventId, eventKind)
                    : new LocalProgrammingEventReference(
                        eventId,
                        eventKind,
                        result.Branch,
                        result.Level,
                        result.Drill);

                return new RuntimeEvidenceArtifactRecordContext(
                    draft,
                    new LocalEvidenceArtifactRecord(artifactId, eventReference, draft.Artifact));
            })
            .ToArray();
    }

    private static LocalFormalTestAttemptRecord? BuildFormalAttemptRecord(
        RuntimePersistenceHandoffRequest request,
        RuntimePersistenceHandoffIds ids,
        IReadOnlyList<RuntimeEvidenceArtifactRecordContext> artifacts)
    {
        if (request.FormalAttempt is null)
        {
            return null;
        }

        var result = request.Result;
        var formalArtifact = RequireArtifact(artifacts, EvidenceArtifactCategory.Test, "formal test attempt");
        var task = request.FormalAttempt.Task ?? DefaultTestTask(result, request.Metadata);
        var attempt = new FormalTestAttempt(
            result.Branch,
            result.Level,
            request.Metadata.Date,
            task,
            result.LoadVariables,
            result.SessionDefinition.Standard.Standard,
            result.SessionDefinition.CriticalConstraints,
            request.FormalAttempt.ResultEvidence,
            request.FormalAttempt.FailureType,
            request.FormalAttempt.PassState,
            formalArtifact.Record.Artifact);

        return new LocalFormalTestAttemptRecord(
            ids.FormalAttemptId!,
            formalArtifact.Record.ArtifactId,
            attempt);
    }

    private static LocalStabilizationPassRecord? BuildStabilizationPassRecord(
        RuntimePersistenceHandoffRequest request,
        RuntimePersistenceHandoffIds ids,
        IReadOnlyList<RuntimeEvidenceArtifactRecordContext> artifacts,
        LocalFormalTestAttemptRecord? formalAttempt)
    {
        if (request.Stabilization is null)
        {
            return null;
        }

        var result = request.Result;
        var stabilizationArtifact = RequireArtifact(artifacts, EvidenceArtifactCategory.Stabilization, "stabilization pass");
        var afterAdjacentWorkOrControlledDistractor = request.Stabilization.Condition is
            LocalStabilizationCondition.AdjacentWork or
            LocalStabilizationCondition.ControlledDistractor;
        var evidence = new StabilizationPassEvidence(
            result.Branch,
            result.Level,
            request.Metadata.Date,
            result.SessionDefinition.Standard.Standard,
            request.Stabilization.PassState,
            request.Stabilization.StandardEvaluationResult,
            afterAdjacentWorkOrControlledDistractor,
            request.Stabilization.MainFailureModeAvoided);

        return new LocalStabilizationPassRecord(
            ids.StabilizationPassId!,
            stabilizationArtifact.Record.ArtifactId,
            request.Stabilization.FormalTestAttemptId ?? formalAttempt?.AttemptId,
            result.SessionId,
            result.Drill,
            request.Stabilization.Condition,
            request.Stabilization.ConditionDescription,
            evidence);
    }

    private static LocalMaintenanceCheckRecord? BuildMaintenanceCheckRecord(
        RuntimePersistenceHandoffRequest request,
        RuntimePersistenceHandoffIds ids,
        IReadOnlyList<RuntimeEvidenceArtifactRecordContext> artifacts)
    {
        if (request.Maintenance is null)
        {
            return null;
        }

        var result = request.Result;
        var maintenanceArtifact = RequireArtifact(artifacts, EvidenceArtifactCategory.Maintenance, "maintenance check");
        var evidence = new MaintenanceCheckEvidence(
            result.Branch,
            request.Maintenance.OwnedLevel,
            request.Metadata.Date,
            request.Maintenance.Kind,
            request.Maintenance.StandardEvaluationResult);

        return new LocalMaintenanceCheckRecord(
            ids.MaintenanceCheckId!,
            maintenanceArtifact.Record.ArtifactId,
            result.SessionId,
            result.Drill,
            result.SessionDefinition.Standard.Standard,
            evidence);
    }

    private static LocalGeneratedDrillInstanceRecord? BuildGeneratedDrillInstanceRecord(
        RuntimePersistenceHandoffRequest request,
        IReadOnlyList<RuntimeEvidenceArtifactRecordContext> artifacts)
    {
        var generated = request.Result.SessionDefinition.GeneratedDrillInstance;
        if (generated is null)
        {
            return null;
        }

        var firstArtifactId = artifacts[0].Record.ArtifactId;
        return new LocalGeneratedDrillInstanceRecord(
            generated.InstanceId,
            request.Metadata.GeneratedInstanceDate,
            request.Result.Branch,
            request.Result.Level,
            request.Result.Drill,
            request.Result.LoadVariables,
            new LocalGeneratedDrillContentIdentity(generated.ContentIdentity, generated.ContentVersion),
            LocalGeneratedDrillInstanceState.Completed,
            resultEvidenceArtifactId: firstArtifactId);
    }

    private static RuntimeEvidenceArtifactRecordContext RequireArtifact(
        IEnumerable<RuntimeEvidenceArtifactRecordContext> artifacts,
        EvidenceArtifactCategory category,
        string recordName)
    {
        return artifacts.FirstOrDefault(artifact => artifact.Record.Artifact.Category == category)
            ?? throw new InvalidOperationException(
                $"Runtime persistence handoff cannot create a {recordName} record without {category} evidence.");
    }

    private static TestTask DefaultTestTask(
        RuntimeSessionCompletionResult result,
        RuntimePersistenceHandoffMetadata metadata)
    {
        if (result.SessionType == SessionType.Transfer)
        {
            if (metadata.TransferTask is null)
            {
                throw new InvalidOperationException(
                    "Transfer formal test attempts require a transfer task description.");
            }

            return TestTask.ForTransfer(metadata.TransferTask);
        }

        return TestTask.ForDrill(result.Drill);
    }

    private static string EventIdForArtifact(
        string sessionId,
        EvidenceArtifactCategory category,
        RuntimePersistenceHandoffIds ids)
    {
        return category switch
        {
            EvidenceArtifactCategory.Test when ids.FormalAttemptId is not null => ids.FormalAttemptId,
            EvidenceArtifactCategory.Stabilization when ids.StabilizationPassId is not null => ids.StabilizationPassId,
            EvidenceArtifactCategory.Maintenance when ids.MaintenanceCheckId is not null => ids.MaintenanceCheckId,
            _ => sessionId,
        };
    }

    private static LocalCompletedSessionType MapSessionType(SessionType sessionType)
    {
        return sessionType switch
        {
            SessionType.Practice => LocalCompletedSessionType.Practice,
            SessionType.Load => LocalCompletedSessionType.Load,
            SessionType.Test => LocalCompletedSessionType.Test,
            SessionType.Stabilization => LocalCompletedSessionType.Stabilization,
            SessionType.Regression => LocalCompletedSessionType.Regression,
            SessionType.Transfer => LocalCompletedSessionType.Transfer,
            SessionType.Recovery => LocalCompletedSessionType.Recovery,
            _ => throw new ArgumentOutOfRangeException(nameof(sessionType), sessionType, "Unknown runtime session type."),
        };
    }

    private static LocalProgrammingEventKind MapEventKind(EvidenceArtifactCategory category)
    {
        return category switch
        {
            EvidenceArtifactCategory.Practice => LocalProgrammingEventKind.Practice,
            EvidenceArtifactCategory.Load => LocalProgrammingEventKind.Load,
            EvidenceArtifactCategory.Test => LocalProgrammingEventKind.FormalTest,
            EvidenceArtifactCategory.Stabilization => LocalProgrammingEventKind.Stabilization,
            EvidenceArtifactCategory.Transfer => LocalProgrammingEventKind.Transfer,
            EvidenceArtifactCategory.Maintenance => LocalProgrammingEventKind.Maintenance,
            EvidenceArtifactCategory.GlobalReview => LocalProgrammingEventKind.GlobalReview,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown evidence artifact category."),
        };
    }

    private sealed record RuntimeEvidenceArtifactRecordContext(
        RuntimeEvidenceDraft Draft,
        LocalEvidenceArtifactRecord Record);

    private sealed record RuntimePersistenceHandoffIds(
        string? FormalAttemptId,
        string? StabilizationPassId,
        string? MaintenanceCheckId)
    {
        public static RuntimePersistenceHandoffIds From(RuntimePersistenceHandoffRequest request)
        {
            return new RuntimePersistenceHandoffIds(
                request.FormalAttempt is null
                    ? null
                    : request.FormalAttempt.AttemptId ?? $"{request.Result.SessionId}-formal-attempt",
                request.Stabilization is null
                    ? null
                    : request.Stabilization.PassId ?? $"{request.Result.SessionId}-stabilization-pass",
                request.Maintenance is null
                    ? null
                    : request.Maintenance.CheckId ?? $"{request.Result.SessionId}-maintenance-check");
        }
    }
}
