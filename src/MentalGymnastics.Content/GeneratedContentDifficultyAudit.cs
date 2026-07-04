using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public enum GeneratedContentDifficultyAuditFailureKind
{
    MaterialValidationFailed,
    RequestedLoadNotRepresented,
    UnrequestedPrimaryLoadVariable,
    MultiplePrimaryLoadVariablesChangedDuringAcquisition,
    EquivalenceConstraintChanged,
    CoreDemandChanged,
    HonestyConstraintRemoved,
}

public sealed record GeneratedContentDifficultyAuditFailure(
    GeneratedContentDifficultyAuditFailureKind Kind,
    LoadVariableKind? LoadVariable,
    string Detail);

public sealed class GeneratedContentDifficultyAuditResult
{
    public GeneratedContentDifficultyAuditResult(
        GeneratedContentMaterialValidationResult materialValidation,
        GeneratedContentLoadConstraintValidationResult loadValidation,
        GeneratedContentFreshnessPolicyResult equivalenceValidation,
        LoadChangeResult loadChangeValidation,
        IEnumerable<LoadVariableKind> unrequestedLoadVariables,
        IEnumerable<GeneratedContentDifficultyAuditFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(materialValidation);
        ArgumentNullException.ThrowIfNull(loadValidation);
        ArgumentNullException.ThrowIfNull(equivalenceValidation);
        ArgumentNullException.ThrowIfNull(loadChangeValidation);
        ArgumentNullException.ThrowIfNull(unrequestedLoadVariables);
        ArgumentNullException.ThrowIfNull(failures);

        MaterialValidation = materialValidation;
        LoadValidation = loadValidation;
        EquivalenceValidation = equivalenceValidation;
        LoadChangeValidation = loadChangeValidation;
        UnrequestedLoadVariables = Array.AsReadOnly(unrequestedLoadVariables.Distinct().ToArray());
        Failures = Array.AsReadOnly(failures.ToArray());
    }

    public GeneratedContentMaterialValidationResult MaterialValidation { get; }

    public GeneratedContentLoadConstraintValidationResult LoadValidation { get; }

    public GeneratedContentFreshnessPolicyResult EquivalenceValidation { get; }

    public LoadChangeResult LoadChangeValidation { get; }

    public IReadOnlyList<LoadVariableKind> UnrequestedLoadVariables { get; }

    public IReadOnlyList<GeneratedContentDifficultyAuditFailure> Failures { get; }

    public bool IsValid =>
        MaterialValidation.IsValid &&
        LoadValidation.IsValid &&
        EquivalenceValidation.CanUseContent &&
        LoadChangeValidation.IsValid &&
        Failures.Count == 0;

    public bool PreservesRequestedDemand =>
        !Failures.Any(failure => failure.Kind is
            GeneratedContentDifficultyAuditFailureKind.RequestedLoadNotRepresented or
            GeneratedContentDifficultyAuditFailureKind.UnrequestedPrimaryLoadVariable or
            GeneratedContentDifficultyAuditFailureKind.MultiplePrimaryLoadVariablesChangedDuringAcquisition or
            GeneratedContentDifficultyAuditFailureKind.EquivalenceConstraintChanged or
            GeneratedContentDifficultyAuditFailureKind.CoreDemandChanged);

    public bool PreservesHonestyConstraint =>
        LoadValidation.PreservesHonestyConstraint &&
        !Failures.Any(failure => failure.Kind == GeneratedContentDifficultyAuditFailureKind.HonestyConstraintRemoved);

    public bool CanBeConsumedByRuntime => IsValid;

    public bool CanBeRecordedByPersistence => IsValid;

    public bool GrantsAdvancement => false;
}

public static class GeneratedContentDifficultyAuditor
{
    public static GeneratedContentDifficultyAuditResult Audit(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials,
        GeneratedContentEquivalenceRequirement equivalenceRequirement,
        GeneratedContentEquivalenceCandidate equivalenceCandidate,
        LoadChangeMode loadChangeMode,
        bool increasedVariablesStableSeparately = false)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(equivalenceRequirement);
        ArgumentNullException.ThrowIfNull(equivalenceCandidate);

        var materialArray = materials.ToArray();
        if (materialArray.Any(material => material is null))
        {
            throw new ArgumentException(
                "Generated content difficulty audit material collections cannot contain null entries.",
                nameof(materials));
        }

        var materialValidation = GeneratedContentMaterialValidator.Validate(result, materialArray);
        var loadValidation = GeneratedContentLoadConstraintValidator.Validate(result, materialArray);
        var equivalenceValidation = GeneratedContentFreshnessPolicy.Evaluate(
            equivalenceRequirement,
            equivalenceCandidate);
        var unrequestedLoadVariables = DetectUnrequestedLoadVariables(result.Request, materialArray);
        var primaryUnrequestedLoadVariables = PrimaryLoadVariablesFor(result.Branch, unrequestedLoadVariables);
        var loadChangeValidation = LoadChangeEvaluator.Evaluate(new LoadChangeRequest(
            result.Branch,
            primaryUnrequestedLoadVariables,
            presentForbiddenLoadIncreases: [],
            loadChangeMode,
            increasedVariablesStableSeparately));
        var failures = new List<GeneratedContentDifficultyAuditFailure>();

        AddMaterialFailures(materialValidation, failures);
        AddLoadFailures(loadValidation, failures);
        AddEquivalenceFailures(equivalenceValidation, failures);
        AddUnrequestedPrimaryLoadFailures(primaryUnrequestedLoadVariables, failures);
        AddLoadChangeFailures(loadChangeValidation, failures);

        return new GeneratedContentDifficultyAuditResult(
            materialValidation,
            loadValidation,
            equivalenceValidation,
            loadChangeValidation,
            primaryUnrequestedLoadVariables,
            failures);
    }

    private static IReadOnlyList<LoadVariableKind> DetectUnrequestedLoadVariables(
        GeneratedDrillContentRequest request,
        IEnumerable<GeneratedContentMaterial> materials)
    {
        var requested = request.LoadVariables
            .Select(variable => TryMapLoadName(variable.Name, out var loadVariable)
                ? loadVariable
                : (LoadVariableKind?)null)
            .Where(variable => variable is not null)
            .Select(variable => variable!.Value)
            .ToHashSet();
        var actual = new HashSet<LoadVariableKind>();

        foreach (var material in materials)
        {
            if (material.Kind == GeneratedContentMaterialKind.LoadVariable &&
                TryMapLoadName(material.Name, out var loadVariable))
            {
                actual.Add(loadVariable);
                continue;
            }

            if (TryMapLoadMaterial(material.Kind, out var materialLoadVariable))
            {
                actual.Add(materialLoadVariable);
            }
        }

        actual.ExceptWith(requested);

        return actual.ToArray();
    }

    private static IReadOnlyList<LoadVariableKind> PrimaryLoadVariablesFor(
        BranchCode branch,
        IEnumerable<LoadVariableKind> loadVariables)
    {
        var primaryLoadVariables = LoadRegressionRuleCatalog.BranchLoadRules
            .Single(rule => rule.Branch == branch)
            .PrimaryLoadVariables
            .ToHashSet();

        return loadVariables
            .Where(primaryLoadVariables.Contains)
            .Distinct()
            .ToArray();
    }

    private static void AddMaterialFailures(
        GeneratedContentMaterialValidationResult materialValidation,
        ICollection<GeneratedContentDifficultyAuditFailure> failures)
    {
        foreach (var failure in materialValidation.Failures)
        {
            failures.Add(new GeneratedContentDifficultyAuditFailure(
                GeneratedContentDifficultyAuditFailureKind.MaterialValidationFailed,
                LoadVariable: null,
                failure.Detail));
        }
    }

    private static void AddLoadFailures(
        GeneratedContentLoadConstraintValidationResult loadValidation,
        ICollection<GeneratedContentDifficultyAuditFailure> failures)
    {
        foreach (var failure in loadValidation.Failures)
        {
            if (failure.Kind == GeneratedContentLoadConstraintFailureKind.MissingHonestyConstraintMaterial)
            {
                failures.Add(new GeneratedContentDifficultyAuditFailure(
                    GeneratedContentDifficultyAuditFailureKind.HonestyConstraintRemoved,
                    failure.LoadVariable,
                    failure.Detail));
                continue;
            }

            if (failure.Kind is
                GeneratedContentLoadConstraintFailureKind.MissingMaterial or
                GeneratedContentLoadConstraintFailureKind.MaterialValueMismatch or
                GeneratedContentLoadConstraintFailureKind.InsufficientMaterialCount)
            {
                failures.Add(new GeneratedContentDifficultyAuditFailure(
                    GeneratedContentDifficultyAuditFailureKind.RequestedLoadNotRepresented,
                    failure.LoadVariable,
                    failure.Detail));
            }
        }
    }

    private static void AddEquivalenceFailures(
        GeneratedContentFreshnessPolicyResult equivalenceValidation,
        ICollection<GeneratedContentDifficultyAuditFailure> failures)
    {
        foreach (var failure in equivalenceValidation.Failures)
        {
            failures.Add(new GeneratedContentDifficultyAuditFailure(
                GeneratedContentDifficultyAuditFailureKind.EquivalenceConstraintChanged,
                LoadVariable: null,
                failure.Detail));

            if (failure.Kind is
                GeneratedContentPolicyFailureKind.BranchDemandChanged or
                GeneratedContentPolicyFailureKind.StandardVisibilityChanged or
                GeneratedContentPolicyFailureKind.LoadIntentChanged or
                GeneratedContentPolicyFailureKind.LoadVariablesChanged)
            {
                failures.Add(new GeneratedContentDifficultyAuditFailure(
                    GeneratedContentDifficultyAuditFailureKind.CoreDemandChanged,
                    LoadVariable: null,
                    failure.Detail));
            }

            if (failure.Kind == GeneratedContentPolicyFailureKind.CriticalConstraintsChanged)
            {
                failures.Add(new GeneratedContentDifficultyAuditFailure(
                    GeneratedContentDifficultyAuditFailureKind.HonestyConstraintRemoved,
                    LoadVariable: null,
                    failure.Detail));
            }
        }
    }

    private static void AddUnrequestedPrimaryLoadFailures(
        IEnumerable<LoadVariableKind> primaryUnrequestedLoadVariables,
        ICollection<GeneratedContentDifficultyAuditFailure> failures)
    {
        foreach (var loadVariable in primaryUnrequestedLoadVariables.Distinct())
        {
            failures.Add(new GeneratedContentDifficultyAuditFailure(
                GeneratedContentDifficultyAuditFailureKind.UnrequestedPrimaryLoadVariable,
                loadVariable,
                $"{loadVariable} is a primary load variable for the requested branch but was not requested."));
        }
    }

    private static void AddLoadChangeFailures(
        LoadChangeResult loadChangeValidation,
        ICollection<GeneratedContentDifficultyAuditFailure> failures)
    {
        foreach (var failure in loadChangeValidation.Failures)
        {
            if (failure.Kind == LoadChangeFailureKind.TooManyLoadVariablesForAcquisition)
            {
                failures.Add(new GeneratedContentDifficultyAuditFailure(
                    GeneratedContentDifficultyAuditFailureKind.MultiplePrimaryLoadVariablesChangedDuringAcquisition,
                    failure.LoadVariable,
                    failure.Detail));
            }
        }
    }

    private static bool TryMapLoadName(
        string loadVariableName,
        out LoadVariableKind loadVariable)
    {
        return GeneratedContentLoadConstraintMapper.TryMapProgramLoadVariableName(
            loadVariableName,
            out loadVariable);
    }

    private static bool TryMapLoadMaterial(
        GeneratedContentMaterialKind materialKind,
        out LoadVariableKind loadVariable)
    {
        switch (materialKind)
        {
            case GeneratedContentMaterialKind.HoldDuration:
                loadVariable = LoadVariableKind.Duration;
                return true;
            case GeneratedContentMaterialKind.DistractorSalience:
                loadVariable = LoadVariableKind.DistractorSalience;
                return true;
            case GeneratedContentMaterialKind.RecoveryWindow:
                loadVariable = LoadVariableKind.RecoveryWindow;
                return true;
            case GeneratedContentMaterialKind.TargetSubtlety:
                loadVariable = LoadVariableKind.TargetSubtlety;
                return true;
            case GeneratedContentMaterialKind.CueDensity:
                loadVariable = LoadVariableKind.CueDensity;
                return true;
            case GeneratedContentMaterialKind.RuleContrast:
                loadVariable = LoadVariableKind.RuleContrast;
                return true;
            case GeneratedContentMaterialKind.ReturnPrecision:
                loadVariable = LoadVariableKind.ReturnPrecision;
                return true;
            case GeneratedContentMaterialKind.EncodeItem:
            case GeneratedContentMaterialKind.SourceItem:
                loadVariable = LoadVariableKind.ItemCount;
                return true;
            case GeneratedContentMaterialKind.DetailDensity:
                loadVariable = LoadVariableKind.DetailDensity;
                return true;
            case GeneratedContentMaterialKind.OperationStep:
                loadVariable = LoadVariableKind.OperationSteps;
                return true;
            case GeneratedContentMaterialKind.DelayLength:
                loadVariable = LoadVariableKind.Delay;
                return true;
            case GeneratedContentMaterialKind.Interference:
                loadVariable = LoadVariableKind.Interference;
                return true;
            case GeneratedContentMaterialKind.CueConflict:
                loadVariable = LoadVariableKind.CueConflict;
                return true;
            case GeneratedContentMaterialKind.ResponseWindow:
                loadVariable = LoadVariableKind.ResponseSpeed;
                return true;
            case GeneratedContentMaterialKind.ExceptionDefinition:
                loadVariable = LoadVariableKind.ExceptionCount;
                return true;
            case GeneratedContentMaterialKind.PressureSource:
                loadVariable = LoadVariableKind.Pressure;
                return true;
            case GeneratedContentMaterialKind.Similarity:
                loadVariable = LoadVariableKind.Similarity;
                return true;
            case GeneratedContentMaterialKind.DiscriminationPair:
            case GeneratedContentMaterialKind.SeededError:
                loadVariable = LoadVariableKind.Quantity;
                return true;
            case GeneratedContentMaterialKind.ErrorSubtlety:
                loadVariable = LoadVariableKind.ErrorSubtlety;
                return true;
            case GeneratedContentMaterialKind.AuditDelay:
                loadVariable = LoadVariableKind.AuditDelay;
                return true;
            case GeneratedContentMaterialKind.RuleAmbiguity:
                loadVariable = LoadVariableKind.RuleAmbiguity;
                return true;
            case GeneratedContentMaterialKind.PositiveExample:
            case GeneratedContentMaterialKind.NegativeExample:
            case GeneratedContentMaterialKind.UnseenExample:
                loadVariable = LoadVariableKind.ExampleCount;
                return true;
            case GeneratedContentMaterialKind.ExceptionHandling:
                loadVariable = LoadVariableKind.ExceptionHandling;
                return true;
            case GeneratedContentMaterialKind.TransferDistance:
                loadVariable = LoadVariableKind.TransferDistance;
                return true;
            case GeneratedContentMaterialKind.TimePressure:
                loadVariable = LoadVariableKind.TimePressure;
                return true;
            case GeneratedContentMaterialKind.EvaluativePressure:
                loadVariable = LoadVariableKind.EvaluativePressure;
                return true;
            case GeneratedContentMaterialKind.FrustrationPressure:
                loadVariable = LoadVariableKind.Frustration;
                return true;
            case GeneratedContentMaterialKind.UncertaintyPressure:
                loadVariable = LoadVariableKind.Uncertainty;
                return true;
            case GeneratedContentMaterialKind.ComponentPayload:
                loadVariable = LoadVariableKind.BranchCount;
                return true;
            case GeneratedContentMaterialKind.TaskLength:
                loadVariable = LoadVariableKind.TaskLength;
                return true;
            case GeneratedContentMaterialKind.DomainDistance:
                loadVariable = LoadVariableKind.DomainDistance;
                return true;
            default:
                loadVariable = default;
                return false;
        }
    }
}
