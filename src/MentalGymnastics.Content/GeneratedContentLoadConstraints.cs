using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public enum GeneratedContentLoadConstraintMatchKind
{
    ExactMaterialValue,
    MinimumMaterialCount,
}

public sealed class GeneratedContentLoadConstraint
{
    public GeneratedContentLoadConstraint(
        LoadVariableKind? programLoadVariable,
        string loadVariableName,
        string requiredValue,
        IEnumerable<GeneratedContentMaterialKind> materialKinds,
        GeneratedContentLoadConstraintMatchKind matchKind,
        int? minimumCount = null)
    {
        if (programLoadVariable is not null)
        {
            GeneratedContentValidation.EnsureDefined(programLoadVariable.Value, nameof(programLoadVariable));
        }

        if (string.IsNullOrWhiteSpace(loadVariableName))
        {
            throw new ArgumentException(
                "Generated content load constraints must name the requested load variable.",
                nameof(loadVariableName));
        }

        if (string.IsNullOrWhiteSpace(requiredValue))
        {
            throw new ArgumentException(
                "Generated content load constraints must include the requested load value.",
                nameof(requiredValue));
        }

        ArgumentNullException.ThrowIfNull(materialKinds);

        var materialKindArray = materialKinds.ToArray();
        if (materialKindArray.Length == 0)
        {
            throw new ArgumentException(
                "Generated content load constraints must name content material kinds.",
                nameof(materialKinds));
        }

        foreach (var materialKind in materialKindArray)
        {
            GeneratedContentValidation.EnsureDefined(materialKind, nameof(materialKinds));
        }

        GeneratedContentValidation.EnsureDefined(matchKind, nameof(matchKind));

        if (matchKind == GeneratedContentLoadConstraintMatchKind.MinimumMaterialCount &&
            (minimumCount is null or <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumCount),
                "Minimum material count constraints must include a positive count.");
        }

        ProgramLoadVariable = programLoadVariable;
        LoadVariableName = loadVariableName;
        RequiredValue = requiredValue;
        MaterialKinds = Array.AsReadOnly(materialKindArray);
        MatchKind = matchKind;
        MinimumCount = minimumCount;
    }

    public LoadVariableKind? ProgramLoadVariable { get; }

    public string LoadVariableName { get; }

    public string RequiredValue { get; }

    public IReadOnlyList<GeneratedContentMaterialKind> MaterialKinds { get; }

    public GeneratedContentLoadConstraintMatchKind MatchKind { get; }

    public int? MinimumCount { get; }
}

public enum GeneratedContentLoadConstraintFailureKind
{
    UnknownLoadVariable,
    LoadVariableNotPrimaryForBranch,
    LoadCountNotNumeric,
    MissingMaterial,
    MaterialValueMismatch,
    InsufficientMaterialCount,
    MissingHonestyConstraintMaterial,
}

public sealed record GeneratedContentLoadConstraintFailure(
    GeneratedContentLoadConstraintFailureKind Kind,
    LoadVariableKind? LoadVariable,
    GeneratedContentMaterialKind? MaterialKind,
    string Detail);

public sealed class GeneratedContentLoadConstraintPlan
{
    public GeneratedContentLoadConstraintPlan(
        IEnumerable<GeneratedContentLoadConstraint> constraints,
        IEnumerable<CriticalConstraint> requiredHonestyConstraints,
        IEnumerable<GeneratedContentLoadConstraintFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(requiredHonestyConstraints);
        ArgumentNullException.ThrowIfNull(failures);

        Constraints = Array.AsReadOnly(constraints.ToArray());
        RequiredHonestyConstraints = Array.AsReadOnly(requiredHonestyConstraints.ToArray());
        Failures = Array.AsReadOnly(failures.ToArray());
    }

    public IReadOnlyList<GeneratedContentLoadConstraint> Constraints { get; }

    public IReadOnlyList<CriticalConstraint> RequiredHonestyConstraints { get; }

    public IReadOnlyList<GeneratedContentLoadConstraintFailure> Failures { get; }

    public bool IsValid => Failures.Count == 0;

    public bool CanSelectContent => IsValid;

    public bool PreservesCoreDemand => IsValid;

    public bool PreservesHonestyConstraint => RequiredHonestyConstraints.Count > 0;

    public bool OwnsProgressionDecision => false;

    public bool GrantsAdvancement => false;
}

public sealed class GeneratedContentLoadConstraintValidationResult
{
    public GeneratedContentLoadConstraintValidationResult(
        GeneratedContentLoadConstraintPlan plan,
        IEnumerable<GeneratedContentLoadConstraintFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(failures);

        Plan = plan;
        Failures = Array.AsReadOnly(failures.ToArray());
    }

    public GeneratedContentLoadConstraintPlan Plan { get; }

    public IReadOnlyList<GeneratedContentLoadConstraintFailure> Failures { get; }

    public bool IsValid => Failures.Count == 0;

    public bool CanSelectContent => IsValid;

    public bool PreservesCoreDemand => IsValid;

    public bool PreservesHonestyConstraint =>
        Plan.RequiredHonestyConstraints.Count > 0 &&
        !Failures.Any(failure =>
            failure.Kind == GeneratedContentLoadConstraintFailureKind.MissingHonestyConstraintMaterial);

    public bool OwnsProgressionDecision => false;

    public bool GrantsAdvancement => false;
}

public static class GeneratedContentLoadConstraintMapper
{
    private static readonly IReadOnlyDictionary<string, LoadVariableKind> ProgramLoadNames =
        new Dictionary<string, LoadVariableKind>(StringComparer.Ordinal)
        {
            ["duration"] = LoadVariableKind.Duration,
            ["distractor salience"] = LoadVariableKind.DistractorSalience,
            ["recovery window"] = LoadVariableKind.RecoveryWindow,
            ["target subtlety"] = LoadVariableKind.TargetSubtlety,
            ["switch count"] = LoadVariableKind.SwitchCount,
            ["cue density"] = LoadVariableKind.CueDensity,
            ["rule contrast"] = LoadVariableKind.RuleContrast,
            ["return precision"] = LoadVariableKind.ReturnPrecision,
            ["item count"] = LoadVariableKind.ItemCount,
            ["detail density"] = LoadVariableKind.DetailDensity,
            ["operation steps"] = LoadVariableKind.OperationSteps,
            ["delay"] = LoadVariableKind.Delay,
            ["interference"] = LoadVariableKind.Interference,
            ["cue conflict"] = LoadVariableKind.CueConflict,
            ["response speed"] = LoadVariableKind.ResponseSpeed,
            ["speed"] = LoadVariableKind.ResponseSpeed,
            ["exception count"] = LoadVariableKind.ExceptionCount,
            ["pressure"] = LoadVariableKind.Pressure,
            ["similarity"] = LoadVariableKind.Similarity,
            ["quantity"] = LoadVariableKind.Quantity,
            ["item quantity"] = LoadVariableKind.Quantity,
            ["error subtlety"] = LoadVariableKind.ErrorSubtlety,
            ["audit delay"] = LoadVariableKind.AuditDelay,
            ["rule ambiguity"] = LoadVariableKind.RuleAmbiguity,
            ["example count"] = LoadVariableKind.ExampleCount,
            ["exception handling"] = LoadVariableKind.ExceptionHandling,
            ["transfer distance"] = LoadVariableKind.TransferDistance,
            ["time pressure"] = LoadVariableKind.TimePressure,
            ["time limit"] = LoadVariableKind.TimePressure,
            ["evaluative pressure"] = LoadVariableKind.EvaluativePressure,
            ["observation"] = LoadVariableKind.EvaluativePressure,
            ["frustration"] = LoadVariableKind.Frustration,
            ["uncertainty"] = LoadVariableKind.Uncertainty,
            ["branch count"] = LoadVariableKind.BranchCount,
            ["number of branches"] = LoadVariableKind.BranchCount,
            ["task length"] = LoadVariableKind.TaskLength,
            ["domain distance"] = LoadVariableKind.DomainDistance,
        };

    public static GeneratedContentLoadConstraintPlan Map(GeneratedDrillContentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var constraints = new List<GeneratedContentLoadConstraint>();
        var failures = new List<GeneratedContentLoadConstraintFailure>();
        var primaryLoadVariables = LoadRegressionRuleCatalog.BranchLoadRules
            .Single(rule => rule.Branch == request.Branch)
            .PrimaryLoadVariables
            .ToHashSet();

        foreach (var loadVariable in request.LoadVariables)
        {
            var normalizedName = Normalize(loadVariable.Name);
            if (TryCreateDrillSpecificConstraint(request.Branch, request.Drill, normalizedName, loadVariable, out var drillConstraint))
            {
                constraints.Add(drillConstraint);
                continue;
            }

            if (!ProgramLoadNames.TryGetValue(normalizedName, out var programLoadVariable))
            {
                failures.Add(new GeneratedContentLoadConstraintFailure(
                    GeneratedContentLoadConstraintFailureKind.UnknownLoadVariable,
                    LoadVariable: null,
                    MaterialKind: null,
                    $"'{loadVariable.Name}' is not a documented load variable for generated content selection."));
                continue;
            }

            if (!primaryLoadVariables.Contains(programLoadVariable))
            {
                failures.Add(new GeneratedContentLoadConstraintFailure(
                    GeneratedContentLoadConstraintFailureKind.LoadVariableNotPrimaryForBranch,
                    programLoadVariable,
                    MaterialKind: null,
                    $"{programLoadVariable} is not a documented primary load variable for {request.Branch}."));
                continue;
            }

            var descriptor = ConstraintDescriptorFor(programLoadVariable, request.Drill);
            var minimumCount = descriptor.MatchKind == GeneratedContentLoadConstraintMatchKind.MinimumMaterialCount
                ? ParseLoadCount(loadVariable.Value)
                : null;

            if (descriptor.MatchKind == GeneratedContentLoadConstraintMatchKind.MinimumMaterialCount &&
                minimumCount is null)
            {
                failures.Add(new GeneratedContentLoadConstraintFailure(
                    GeneratedContentLoadConstraintFailureKind.LoadCountNotNumeric,
                    programLoadVariable,
                    descriptor.MaterialKinds[0],
                    $"Load variable '{loadVariable.Name}' must include a numeric count to select matching content."));
                continue;
            }

            constraints.Add(new GeneratedContentLoadConstraint(
                programLoadVariable,
                loadVariable.Name,
                loadVariable.Value,
                descriptor.MaterialKinds,
                descriptor.MatchKind,
                minimumCount));
        }

        return new GeneratedContentLoadConstraintPlan(
            constraints,
            request.CriticalConstraints,
            failures);
    }

    public static bool TryMapProgramLoadVariableName(
        string loadVariableName,
        out LoadVariableKind loadVariable)
    {
        if (string.IsNullOrWhiteSpace(loadVariableName))
        {
            loadVariable = default;
            return false;
        }

        return ProgramLoadNames.TryGetValue(Normalize(loadVariableName), out loadVariable);
    }

    private static bool TryCreateDrillSpecificConstraint(
        BranchCode branch,
        DrillId drill,
        string normalizedName,
        LoadVariable loadVariable,
        out GeneratedContentLoadConstraint constraint)
    {
        constraint = null!;

        if (branch == BranchCode.FH &&
            drill == DrillId.FH2DistractorHold &&
            string.Equals(normalizedName, "distractor frequency", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.DistractorFrequency],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.IR &&
            drill == DrillId.IR1GoNoGoRule &&
            string.Equals(normalizedName, "no-go frequency", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.NoGoFrequency],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.IR &&
            drill == DrillId.IR2ExceptionRule &&
            string.Equals(normalizedName, "similarity", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.Similarity],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.DE &&
            drill == DrillId.DE1PairDiscrimination &&
            string.Equals(normalizedName, "time limit", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.TimePressure],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.DE &&
            drill == DrillId.DE2SeededAudit &&
            string.Equals(normalizedName, "output length", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.TaskLength],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.CO &&
            drill == DrillId.CO2StructureMapping &&
            string.Equals(normalizedName, "relation count", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.RequiredRelation],
                GeneratedContentLoadConstraintMatchKind.MinimumMaterialCount,
                ParseLoadCount(loadVariable.Value) ?? 1);
            return true;
        }

        if (branch == BranchCode.CO &&
            drill == DrillId.CO2StructureMapping &&
            string.Equals(normalizedName, "domain distance", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.DomainDistance],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.FS &&
            (drill == DrillId.FS1CueSwitch || drill == DrillId.FS2InvalidCueFilter) &&
            string.Equals(normalizedName, "target count", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.TargetSet],
                GeneratedContentLoadConstraintMatchKind.MinimumMaterialCount,
                ParseLoadCount(loadVariable.Value) ?? 1);
            return true;
        }

        if (branch == BranchCode.AI &&
            drill == DrillId.AI2DisruptionRecovery &&
            string.Equals(normalizedName, "interruption timing", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.DisruptionTiming],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.AI &&
            drill == DrillId.AI2DisruptionRecovery &&
            string.Equals(normalizedName, "restart delay", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.RestartDelay],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.AI &&
            drill == DrillId.AI2DisruptionRecovery &&
            string.Equals(normalizedName, "task complexity", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.TaskComplexity],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.AI &&
            drill == DrillId.AI2DisruptionRecovery &&
            string.Equals(normalizedName, "recovery window", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.RecoveryWindow],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.TI &&
            drill == DrillId.TI1CompositeTask &&
            string.Equals(normalizedName, "transfer distance", StringComparison.Ordinal))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.TransferDistance],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.TI &&
            drill == DrillId.TI2GlobalReviewTask &&
            (string.Equals(normalizedName, "pressure", StringComparison.Ordinal) ||
                string.Equals(normalizedName, "pressure source", StringComparison.Ordinal)))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.PressureSource],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.TI &&
            drill == DrillId.TI2GlobalReviewTask &&
            (string.Equals(normalizedName, "ambiguity", StringComparison.Ordinal) ||
                string.Equals(normalizedName, "rule ambiguity", StringComparison.Ordinal)))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.RuleAmbiguity],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        if (branch == BranchCode.TI &&
            drill == DrillId.TI2GlobalReviewTask &&
            (string.Equals(normalizedName, "delay", StringComparison.Ordinal) ||
                string.Equals(normalizedName, "review delay", StringComparison.Ordinal)))
        {
            constraint = new GeneratedContentLoadConstraint(
                programLoadVariable: null,
                loadVariable.Name,
                loadVariable.Value,
                [GeneratedContentMaterialKind.DelayLength],
                GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
            return true;
        }

        return false;
    }

    private static GeneratedContentLoadConstraintDescriptor ConstraintDescriptorFor(
        LoadVariableKind loadVariable,
        DrillId drill)
    {
        return loadVariable switch
        {
            LoadVariableKind.Duration => Exact(GeneratedContentMaterialKind.HoldDuration),
            LoadVariableKind.DistractorSalience => Exact(GeneratedContentMaterialKind.DistractorSalience),
            LoadVariableKind.RecoveryWindow => Exact(GeneratedContentMaterialKind.RecoveryWindow),
            LoadVariableKind.TargetSubtlety => Exact(GeneratedContentMaterialKind.TargetSubtlety),
            LoadVariableKind.SwitchCount => Count(GeneratedContentMaterialKind.CueStep),
            LoadVariableKind.CueDensity => Exact(GeneratedContentMaterialKind.CueDensity),
            LoadVariableKind.RuleContrast => Exact(GeneratedContentMaterialKind.RuleContrast),
            LoadVariableKind.ReturnPrecision => Exact(GeneratedContentMaterialKind.ReturnPrecision),
            LoadVariableKind.ItemCount => Count(
                drill == DrillId.WM2MentalTransform
                    ? GeneratedContentMaterialKind.SourceItem
                    : GeneratedContentMaterialKind.EncodeItem),
            LoadVariableKind.DetailDensity => Exact(GeneratedContentMaterialKind.DetailDensity),
            LoadVariableKind.OperationSteps => Count(GeneratedContentMaterialKind.OperationStep),
            LoadVariableKind.Delay => Exact(GeneratedContentMaterialKind.DelayLength),
            LoadVariableKind.Interference => Exact(GeneratedContentMaterialKind.Interference),
            LoadVariableKind.CueConflict => Exact(GeneratedContentMaterialKind.CueConflict),
            LoadVariableKind.ResponseSpeed => Exact(GeneratedContentMaterialKind.ResponseWindow),
            LoadVariableKind.ExceptionCount => Count(GeneratedContentMaterialKind.ExceptionDefinition),
            LoadVariableKind.Pressure => Exact(GeneratedContentMaterialKind.PressureSource),
            LoadVariableKind.Similarity => Exact(GeneratedContentMaterialKind.Similarity),
            LoadVariableKind.Quantity => Count(
                drill == DrillId.DE2SeededAudit
                    ? GeneratedContentMaterialKind.SeededError
                    : GeneratedContentMaterialKind.DiscriminationPair),
            LoadVariableKind.ErrorSubtlety => Exact(GeneratedContentMaterialKind.ErrorSubtlety),
            LoadVariableKind.AuditDelay => Exact(GeneratedContentMaterialKind.AuditDelay),
            LoadVariableKind.RuleAmbiguity => Exact(GeneratedContentMaterialKind.RuleAmbiguity),
            LoadVariableKind.ExampleCount => Count(
                GeneratedContentMaterialKind.PositiveExample,
                GeneratedContentMaterialKind.NegativeExample,
                GeneratedContentMaterialKind.UnseenExample),
            LoadVariableKind.ExceptionHandling => Exact(GeneratedContentMaterialKind.ExceptionHandling),
            LoadVariableKind.TransferDistance => Exact(GeneratedContentMaterialKind.TransferDistance),
            LoadVariableKind.TimePressure => Exact(GeneratedContentMaterialKind.TimePressure),
            LoadVariableKind.EvaluativePressure => Exact(GeneratedContentMaterialKind.EvaluativePressure),
            LoadVariableKind.Frustration => Exact(GeneratedContentMaterialKind.FrustrationPressure),
            LoadVariableKind.Uncertainty => Exact(GeneratedContentMaterialKind.UncertaintyPressure),
            LoadVariableKind.BranchCount => Count(GeneratedContentMaterialKind.ComponentPayload),
            LoadVariableKind.TaskLength => Exact(GeneratedContentMaterialKind.TaskLength),
            LoadVariableKind.DomainDistance => Exact(GeneratedContentMaterialKind.DomainDistance),
            _ => throw new ArgumentOutOfRangeException(nameof(loadVariable), loadVariable, "Unknown load variable."),
        };
    }

    private static GeneratedContentLoadConstraintDescriptor Exact(params GeneratedContentMaterialKind[] materialKinds)
    {
        return new GeneratedContentLoadConstraintDescriptor(
            materialKinds,
            GeneratedContentLoadConstraintMatchKind.ExactMaterialValue);
    }

    private static GeneratedContentLoadConstraintDescriptor Count(params GeneratedContentMaterialKind[] materialKinds)
    {
        return new GeneratedContentLoadConstraintDescriptor(
            materialKinds,
            GeneratedContentLoadConstraintMatchKind.MinimumMaterialCount);
    }

    private static int? ParseLoadCount(string value)
    {
        var numericPrefix = new string(value
            .Trim()
            .TakeWhile(char.IsDigit)
            .ToArray());

        return int.TryParse(numericPrefix, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count
            : null;
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}

public static class GeneratedContentLoadConstraintValidator
{
    public static GeneratedContentLoadConstraintValidationResult Validate(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);

        var materialArray = materials.ToArray();
        if (materialArray.Any(material => material is null))
        {
            throw new ArgumentException(
                "Generated content material collections cannot contain null entries.",
                nameof(materials));
        }

        var plan = GeneratedContentLoadConstraintMapper.Map(result.Request);
        var failures = new List<GeneratedContentLoadConstraintFailure>(plan.Failures);

        foreach (var constraint in plan.Constraints)
        {
            AddConstraintFailures(constraint, materialArray, failures);
        }

        AddHonestyConstraintFailures(plan.RequiredHonestyConstraints, materialArray, failures);

        return new GeneratedContentLoadConstraintValidationResult(plan, failures);
    }

    private static void AddConstraintFailures(
        GeneratedContentLoadConstraint constraint,
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentLoadConstraintFailure> failures)
    {
        var matchingMaterials = materials
            .Where(material => constraint.MaterialKinds.Contains(material.Kind))
            .ToArray();

        if (constraint.MatchKind == GeneratedContentLoadConstraintMatchKind.MinimumMaterialCount)
        {
            if (matchingMaterials.Length >= constraint.MinimumCount)
            {
                return;
            }

            failures.Add(new GeneratedContentLoadConstraintFailure(
                GeneratedContentLoadConstraintFailureKind.InsufficientMaterialCount,
                constraint.ProgramLoadVariable,
                constraint.MaterialKinds[0],
                $"Load variable '{constraint.LoadVariableName}' requires at least {constraint.MinimumCount} matching content materials."));
            return;
        }

        if (matchingMaterials.Length == 0)
        {
            failures.Add(new GeneratedContentLoadConstraintFailure(
                GeneratedContentLoadConstraintFailureKind.MissingMaterial,
                constraint.ProgramLoadVariable,
                constraint.MaterialKinds[0],
                $"Load variable '{constraint.LoadVariableName}' requires {string.Join(", ", constraint.MaterialKinds)} content material."));
            return;
        }

        if (matchingMaterials.Any(material =>
                string.Equals(material.Value, constraint.RequiredValue, StringComparison.Ordinal)))
        {
            return;
        }

        failures.Add(new GeneratedContentLoadConstraintFailure(
            GeneratedContentLoadConstraintFailureKind.MaterialValueMismatch,
            constraint.ProgramLoadVariable,
            constraint.MaterialKinds[0],
            $"Load variable '{constraint.LoadVariableName}' requires content material value '{constraint.RequiredValue}'."));
    }

    private static void AddHonestyConstraintFailures(
        IEnumerable<CriticalConstraint> criticalConstraints,
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentLoadConstraintFailure> failures)
    {
        var honestyConstraintValues = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.HonestyConstraint)
            .Select(material => material.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var constraint in criticalConstraints)
        {
            if (honestyConstraintValues.Contains(constraint.Description))
            {
                continue;
            }

            failures.Add(new GeneratedContentLoadConstraintFailure(
                GeneratedContentLoadConstraintFailureKind.MissingHonestyConstraintMaterial,
                LoadVariable: null,
                GeneratedContentMaterialKind.HonestyConstraint,
                $"Generated content does not preserve honesty constraint '{constraint.Description}'."));
        }
    }
}

internal sealed record GeneratedContentLoadConstraintDescriptor(
    IReadOnlyList<GeneratedContentMaterialKind> MaterialKinds,
    GeneratedContentLoadConstraintMatchKind MatchKind);
