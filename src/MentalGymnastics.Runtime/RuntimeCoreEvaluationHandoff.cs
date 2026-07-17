using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public sealed class RuntimeStandardEvaluationHandoffInput
{
    public RuntimeStandardEvaluationHandoffInput(
        IEnumerable<NumericMeasurement> measurements,
        IEnumerable<CriticalConstraintCheck> criticalConstraintChecks,
        bool outputComplete,
        RubricOutcome? rubricOutcome)
    {
        ArgumentNullException.ThrowIfNull(measurements);
        ArgumentNullException.ThrowIfNull(criticalConstraintChecks);

        var measurementArray = measurements.ToArray();
        foreach (var measurement in measurementArray)
        {
            if (string.IsNullOrWhiteSpace(measurement.Name))
            {
                throw new ArgumentException(
                    "Runtime standard handoff measurements must name the measured value.",
                    nameof(measurements));
            }
        }

        var criticalConstraintArray = criticalConstraintChecks.ToArray();
        foreach (var check in criticalConstraintArray)
        {
            if (string.IsNullOrWhiteSpace(check.Id))
            {
                throw new ArgumentException(
                    "Runtime standard handoff critical constraint checks must name the checked constraint.",
                    nameof(criticalConstraintChecks));
            }
        }

        if (rubricOutcome.HasValue)
        {
            EnsureDefined(rubricOutcome.Value, nameof(rubricOutcome));
        }

        Measurements = Array.AsReadOnly(measurementArray);
        CriticalConstraintChecks = Array.AsReadOnly(criticalConstraintArray);
        OutputComplete = outputComplete;
        RubricOutcome = rubricOutcome;
    }

    public IReadOnlyList<NumericMeasurement> Measurements { get; }

    public IReadOnlyList<CriticalConstraintCheck> CriticalConstraintChecks { get; }

    public bool OutputComplete { get; }

    public RubricOutcome? RubricOutcome { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown standard handoff value.");
        }
    }
}

public sealed class RuntimeFormalGateHandoffInput
{
    public RuntimeFormalGateHandoffInput(
        TrainingDate date,
        TestResultEvidence resultEvidence,
        FormalTestPassState passState,
        FailureType? failureType = null,
        TestTask? task = null)
    {
        ArgumentNullException.ThrowIfNull(resultEvidence);
        EnsureDefined(passState, nameof(passState));
        if (failureType.HasValue)
        {
            EnsureDefined(failureType.Value, nameof(failureType));
        }

        Date = date;
        ResultEvidence = resultEvidence;
        PassState = passState;
        FailureType = failureType;
        Task = task;
    }

    public TrainingDate Date { get; }

    public TestResultEvidence ResultEvidence { get; }

    public FormalTestPassState PassState { get; }

    public FailureType? FailureType { get; }

    public TestTask? Task { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown formal gate handoff value.");
        }
    }
}

public sealed class RuntimeReadinessPracticeHandoffInput
{
    public RuntimeReadinessPracticeHandoffInput(string drillDemand, bool clean)
    {
        if (string.IsNullOrWhiteSpace(drillDemand))
        {
            throw new ArgumentException(
                "Readiness handoff requires the practiced drill demand.",
                nameof(drillDemand));
        }

        DrillDemand = drillDemand;
        Clean = clean;
    }

    public string DrillDemand { get; }

    public bool Clean { get; }
}

public sealed class RuntimeStabilizationCoreHandoffInput
{
    public RuntimeStabilizationCoreHandoffInput(
        TrainingDate date,
        StandardEvaluationResult standardEvaluationResult,
        FormalTestPassState passState,
        bool afterAdjacentWorkOrControlledDistractor)
    {
        ArgumentNullException.ThrowIfNull(standardEvaluationResult);
        EnsureDefined(passState, nameof(passState));

        Date = date;
        StandardEvaluationResult = standardEvaluationResult;
        PassState = passState;
        AfterAdjacentWorkOrControlledDistractor = afterAdjacentWorkOrControlledDistractor;
    }

    public TrainingDate Date { get; }

    public StandardEvaluationResult StandardEvaluationResult { get; }

    public FormalTestPassState PassState { get; }

    public bool AfterAdjacentWorkOrControlledDistractor { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown stabilization handoff value.");
        }
    }
}

public sealed class RuntimeMaintenanceCoreHandoffInput
{
    public RuntimeMaintenanceCoreHandoffInput(
        TrainingDate date,
        GlobalLevelId ownedLevel,
        MaintenanceCheckKind kind,
        StandardEvaluationResult standardEvaluationResult)
    {
        EnsureDefined(ownedLevel, nameof(ownedLevel));
        EnsureDefined(kind, nameof(kind));
        ArgumentNullException.ThrowIfNull(standardEvaluationResult);

        Date = date;
        OwnedLevel = ownedLevel;
        Kind = kind;
        StandardEvaluationResult = standardEvaluationResult;
    }

    public TrainingDate Date { get; }

    public GlobalLevelId OwnedLevel { get; }

    public MaintenanceCheckKind Kind { get; }

    public StandardEvaluationResult StandardEvaluationResult { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown maintenance handoff value.");
        }
    }
}

public sealed class RuntimeTransferEligibilityHandoffInput
{
    public RuntimeTransferEligibilityHandoffInput(
        GlobalLevelId sourceLevel,
        string transferTask,
        CapacityId? trainedCapacity,
        string sameDemand,
        string changedContext,
        TransferSourceStandardEvidence? sourceStandardEvidence,
        TransferRetestPlan? retestPlan)
    {
        EnsureDefined(sourceLevel, nameof(sourceLevel));
        if (trainedCapacity.HasValue)
        {
            EnsureDefined(trainedCapacity.Value, nameof(trainedCapacity));
        }

        SourceLevel = sourceLevel;
        TransferTask = transferTask ?? string.Empty;
        TrainedCapacity = trainedCapacity;
        SameDemand = sameDemand ?? string.Empty;
        ChangedContext = changedContext ?? string.Empty;
        SourceStandardEvidence = sourceStandardEvidence;
        RetestPlan = retestPlan;
    }

    public GlobalLevelId SourceLevel { get; }

    public string TransferTask { get; }

    public CapacityId? TrainedCapacity { get; }

    public string SameDemand { get; }

    public string ChangedContext { get; }

    public TransferSourceStandardEvidence? SourceStandardEvidence { get; }

    public TransferRetestPlan? RetestPlan { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown transfer handoff value.");
        }
    }
}

public sealed class RuntimeFailureResponseHandoffInput
{
    public RuntimeFailureResponseHandoffInput(
        FailureType failureType,
        IEnumerable<FailureEvidenceSignal> evidenceSignals,
        bool isFirstFailureOfType,
        bool repeatedOverloadInSameBranch,
        StuckStateResponseContext? stuckStateContext = null)
    {
        EnsureDefined(failureType, nameof(failureType));
        ArgumentNullException.ThrowIfNull(evidenceSignals);

        var signalArray = evidenceSignals.ToArray();
        foreach (var signal in signalArray)
        {
            EnsureDefined(signal, nameof(evidenceSignals));
        }

        FailureType = failureType;
        EvidenceSignals = Array.AsReadOnly(signalArray);
        IsFirstFailureOfType = isFirstFailureOfType;
        RepeatedOverloadInSameBranch = repeatedOverloadInSameBranch;
        StuckStateContext = stuckStateContext ?? StuckStateResponseContext.None;
    }

    public FailureType FailureType { get; }

    public IReadOnlyList<FailureEvidenceSignal> EvidenceSignals { get; }

    public bool IsFirstFailureOfType { get; }

    public bool RepeatedOverloadInSameBranch { get; }

    public StuckStateResponseContext StuckStateContext { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown failure response handoff value.");
        }
    }
}

public sealed class RuntimeCoreEvaluationHandoffRequest
{
    public RuntimeCoreEvaluationHandoffRequest(
        RuntimeSessionCompletionResult result,
        RuntimeStandardEvaluationHandoffInput? standardEvaluation = null,
        RuntimeFormalGateHandoffInput? formalGate = null,
        RuntimeReadinessPracticeHandoffInput? readinessPractice = null,
        RuntimeStabilizationCoreHandoffInput? stabilization = null,
        RuntimeMaintenanceCoreHandoffInput? maintenance = null,
        RuntimeTransferEligibilityHandoffInput? transfer = null,
        RuntimeFailureResponseHandoffInput? failureResponse = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ContainsAdvancementDecision)
        {
            throw new ArgumentException(
                "Runtime-to-core evaluation handoff must not carry progression or gate decision facts.",
                nameof(result));
        }

        if (standardEvaluation is null &&
            formalGate is null &&
            readinessPractice is null &&
            stabilization is null &&
            maintenance is null &&
            transfer is null &&
            failureResponse is null)
        {
            throw new ArgumentException(
                "Runtime-to-core evaluation handoff requires at least one core input target.",
                nameof(result));
        }

        Result = result;
        StandardEvaluation = standardEvaluation;
        FormalGate = formalGate;
        ReadinessPractice = readinessPractice;
        Stabilization = stabilization;
        Maintenance = maintenance;
        Transfer = transfer;
        FailureResponse = failureResponse;
    }

    public RuntimeSessionCompletionResult Result { get; }

    public RuntimeStandardEvaluationHandoffInput? StandardEvaluation { get; }

    public RuntimeFormalGateHandoffInput? FormalGate { get; }

    public RuntimeReadinessPracticeHandoffInput? ReadinessPractice { get; }

    public RuntimeStabilizationCoreHandoffInput? Stabilization { get; }

    public RuntimeMaintenanceCoreHandoffInput? Maintenance { get; }

    public RuntimeTransferEligibilityHandoffInput? Transfer { get; }

    public RuntimeFailureResponseHandoffInput? FailureResponse { get; }
}

public sealed record RuntimeCoreEvaluationHandoff(
    StandardEvaluationAttempt? StandardEvaluationAttempt,
    FormalTestAttempt? FormalTestAttempt,
    TestReadinessPracticeSession? ReadinessPracticeSession,
    StabilizationPassEvidence? StabilizationPass,
    MaintenanceCheckEvidence? MaintenanceCheck,
    TransferEligibilityRequest? TransferEligibilityRequest,
    FailureResponseRequest? FailureResponseRequest);

public static class RuntimeCoreEvaluationHandoffMapper
{
    public static RuntimeCoreEvaluationHandoff Map(RuntimeCoreEvaluationHandoffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new RuntimeCoreEvaluationHandoff(
            BuildStandardEvaluationAttempt(request),
            BuildFormalTestAttempt(request),
            BuildReadinessPracticeSession(request),
            BuildStabilizationPass(request),
            BuildMaintenanceCheck(request),
            BuildTransferEligibilityRequest(request),
            BuildFailureResponseRequest(request));
    }

    private static StandardEvaluationAttempt? BuildStandardEvaluationAttempt(
        RuntimeCoreEvaluationHandoffRequest request)
    {
        if (request.StandardEvaluation is null)
        {
            return null;
        }

        return new StandardEvaluationAttempt(
            request.StandardEvaluation.Measurements,
            request.StandardEvaluation.CriticalConstraintChecks,
            request.StandardEvaluation.OutputComplete,
            request.StandardEvaluation.RubricOutcome);
    }

    private static FormalTestAttempt? BuildFormalTestAttempt(RuntimeCoreEvaluationHandoffRequest request)
    {
        if (request.FormalGate is null)
        {
            return null;
        }

        var result = request.Result;
        var formalCategory = result.SessionType == SessionType.Transfer
            ? EvidenceArtifactCategory.Transfer
            : EvidenceArtifactCategory.Test;
        RequireEvidenceCategory(result, formalCategory, "formal gate evaluation");
        var artifact = FirstArtifact(result, formalCategory);
        var task = request.FormalGate.Task ?? DefaultFormalTestTask(result);

        return new FormalTestAttempt(
            result.Branch,
            result.Level,
            request.FormalGate.Date,
            task,
            result.LoadVariables,
            result.SessionDefinition.Standard.Standard,
            result.SessionDefinition.CriticalConstraints,
            request.FormalGate.ResultEvidence,
            request.FormalGate.FailureType,
            request.FormalGate.PassState,
            artifact);
    }

    private static TestReadinessPracticeSession? BuildReadinessPracticeSession(
        RuntimeCoreEvaluationHandoffRequest request)
    {
        if (request.ReadinessPractice is null)
        {
            return null;
        }

        if (request.Result.SessionType is not (SessionType.Practice or SessionType.Load))
        {
            throw new InvalidOperationException(
                "Only practice or load runtime sessions can be handed to core as readiness practice sessions.");
        }

        return new TestReadinessPracticeSession(
            request.Result.Branch,
            request.Result.Level,
            request.Result.Drill,
            request.ReadinessPractice.DrillDemand,
            request.ReadinessPractice.Clean);
    }

    private static StabilizationPassEvidence? BuildStabilizationPass(
        RuntimeCoreEvaluationHandoffRequest request)
    {
        if (request.Stabilization is null)
        {
            return null;
        }

        RequireEvidenceCategory(request.Result, EvidenceArtifactCategory.Stabilization, "stabilization evaluation");

        return new StabilizationPassEvidence(
            request.Result.Branch,
            request.Result.Level,
            request.Stabilization.Date,
            request.Result.SessionDefinition.Standard.Standard,
            request.Stabilization.PassState,
            request.Stabilization.StandardEvaluationResult,
            request.Stabilization.AfterAdjacentWorkOrControlledDistractor);
    }

    private static MaintenanceCheckEvidence? BuildMaintenanceCheck(
        RuntimeCoreEvaluationHandoffRequest request)
    {
        if (request.Maintenance is null)
        {
            return null;
        }

        RequireEvidenceCategory(request.Result, EvidenceArtifactCategory.Maintenance, "maintenance evaluation");

        return new MaintenanceCheckEvidence(
            request.Result.Branch,
            request.Maintenance.OwnedLevel,
            request.Maintenance.Date,
            request.Maintenance.Kind,
            request.Maintenance.StandardEvaluationResult);
    }

    private static TransferEligibilityRequest? BuildTransferEligibilityRequest(
        RuntimeCoreEvaluationHandoffRequest request)
    {
        if (request.Transfer is null)
        {
            return null;
        }

        RequireEvidenceCategory(request.Result, EvidenceArtifactCategory.Transfer, "transfer evaluation");

        return new TransferEligibilityRequest(
            request.Result.Branch,
            request.Transfer.SourceLevel,
            request.Transfer.TransferTask,
            request.Transfer.TrainedCapacity,
            request.Transfer.SameDemand,
            request.Transfer.ChangedContext,
            request.Transfer.SourceStandardEvidence,
            request.Transfer.RetestPlan);
    }

    private static FailureResponseRequest? BuildFailureResponseRequest(
        RuntimeCoreEvaluationHandoffRequest request)
    {
        if (request.FailureResponse is null)
        {
            return null;
        }

        return new FailureResponseRequest(
            new ClassifiedFailure(
                request.Result.Branch,
                request.Result.Level,
                request.FailureResponse.FailureType,
                request.FailureResponse.EvidenceSignals),
            request.FailureResponse.IsFirstFailureOfType,
            request.FailureResponse.RepeatedOverloadInSameBranch,
            request.FailureResponse.StuckStateContext);
    }

    private static TestTask DefaultFormalTestTask(RuntimeSessionCompletionResult result)
    {
        if (result.SessionType == SessionType.Transfer)
        {
            throw new InvalidOperationException(
                "Transfer formal gate handoff requires an explicit transfer TestTask.");
        }

        return TestTask.ForDrill(result.Drill);
    }

    private static EvidenceArtifact FirstArtifact(
        RuntimeSessionCompletionResult result,
        EvidenceArtifactCategory category)
    {
        return result.EvidenceDrafts
            .First(draft => draft.Artifact.Category == category)
            .Artifact;
    }

    private static void RequireEvidenceCategory(
        RuntimeSessionCompletionResult result,
        EvidenceArtifactCategory category,
        string target)
    {
        if (!result.EvidenceDrafts.Any(draft => draft.Artifact.Category == category))
        {
            throw new InvalidOperationException(
                $"Runtime-to-core {target} requires {category} evidence from the runtime result.");
        }
    }
}
