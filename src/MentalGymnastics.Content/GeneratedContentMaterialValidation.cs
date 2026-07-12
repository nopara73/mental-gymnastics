using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public enum GeneratedContentMaterialKind
{
    LoadVariable,
    HonestyConstraint,
    TargetStatement,
    TargetType,
    TargetSubtlety,
    HoldDuration,
    RecoveryWindow,
    DriftMarkingEvidenceShape,
    DistractorPrompt,
    DistractorTiming,
    DistractorFrequency,
    DistractorSalience,
    DistractorNoResponseRule,
    TargetSet,
    CueStep,
    ValidCue,
    InvalidCue,
    CueDensity,
    RuleContrast,
    ResponseWindow,
    ReturnPrecision,
    ExpectedActiveTarget,
    EncodeItem,
    EncodeInstruction,
    DetailDensity,
    DelayLength,
    Interference,
    ReconstructionInstruction,
    ExpectedReconstruction,
    SourceItem,
    SourceTask,
    SourceDrill,
    TransformRule,
    OperationStep,
    FinalExpectedOutput,
    RuleExplanationPrompt,
    HiddenNotePolicy,
    GoNoGoCue,
    CueConflict,
    CuePace,
    NoGoFrequency,
    ExpectedAction,
    RuleStatement,
    ExceptionDefinition,
    DiscriminationPair,
    RelevantFeature,
    MatchTruth,
    GuessHandling,
    FalsePositiveFalseNegativeKey,
    AuditReference,
    LockedOriginalOutput,
    SeededError,
    ExpectedFinding,
    NonErrorDistractor,
    AuditInstruction,
    Similarity,
    ErrorSubtlety,
    AuditDelay,
    PositiveExample,
    NegativeExample,
    UnseenExample,
    ExpectedClassification,
    ExpectedRule,
    RuleFamily,
    RuleAmbiguity,
    ExceptionHandling,
    SourceStructure,
    TargetStructure,
    RequiredRelation,
    SurfaceLure,
    ExpectedMapping,
    MappingLimit,
    TransferDistance,
    SourceBranchStandard,
    PressureSource,
    TimePressure,
    EvaluativePressure,
    FrustrationPressure,
    UncertaintyPressure,
    NoStandardLoweringMarker,
    DisruptionEvent,
    DisruptionTiming,
    RestartRule,
    RestartDelay,
    TaskComplexity,
    PostDisruptionEvidence,
    ComponentPayload,
    ComponentEvidenceRequirement,
    BranchScoringKey,
    CompositeTaskPrompt,
    AuditPayload,
    DelayedReconstructionPayload,
    TaskLength,
    DomainDistance,
    TransferTask,
    SameDemand,
    ChangedContext,
    RetestRequirement,
}

public sealed class GeneratedContentMaterial
{
    public GeneratedContentMaterial(
        GeneratedContentMaterialKind kind,
        string name,
        string value)
    {
        GeneratedContentValidation.EnsureDefined(kind, nameof(kind));

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Generated content materials must include a stable name.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Generated content materials must include an observable value.", nameof(value));
        }

        Kind = kind;
        Name = name;
        Value = value;
    }

    public GeneratedContentMaterialKind Kind { get; }

    public string Name { get; }

    public string Value { get; }
}

public enum GeneratedContentMaterialValidationFailureKind
{
    MissingRequiredMaterial,
    MissingLoadVariableMaterial,
    LoadVariableMaterialMismatch,
    MissingHonestyConstraintMaterial,
    ContentKindMismatch,
    BranchDrillMismatch,
    InsufficientLoadMaterial,
    MissingComponentEvidenceRequirement,
    MissingComponentScoringKey,
}

public sealed record GeneratedContentMaterialValidationFailure(
    GeneratedContentMaterialValidationFailureKind Kind,
    GeneratedContentMaterialKind? MaterialKind,
    string Detail);

public sealed class GeneratedContentMaterialValidationResult
{
    public GeneratedContentMaterialValidationResult(
        IEnumerable<GeneratedContentMaterialValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        Failures = Array.AsReadOnly(failures.ToArray());
    }

    public bool IsValid => Failures.Count == 0;

    public bool CanBeConsumedByRuntime => IsValid;

    public bool GrantsAdvancement => false;

    public IReadOnlyList<GeneratedContentMaterialValidationFailure> Failures { get; }
}

public static class GeneratedContentMaterialValidator
{
    private static readonly IReadOnlyDictionary<DrillId, PromptContentKind> ExpectedContentKinds =
        new Dictionary<DrillId, PromptContentKind>
        {
            [DrillId.FH1TargetHold] = PromptContentKind.EquivalentPrompt,
            [DrillId.FH2DistractorHold] = PromptContentKind.CueSequence,
            [DrillId.FS1CueSwitch] = PromptContentKind.CueSequence,
            [DrillId.FS2InvalidCueFilter] = PromptContentKind.CueSequence,
            [DrillId.WM1DelayedReconstruction] = PromptContentKind.DelayedReconstructionTask,
            [DrillId.WM2MentalTransform] = PromptContentKind.DelayedReconstructionTask,
            [DrillId.IR1GoNoGoRule] = PromptContentKind.CueSequence,
            [DrillId.IR2ExceptionRule] = PromptContentKind.CueSequence,
            [DrillId.DE1PairDiscrimination] = PromptContentKind.DiscriminationItemSet,
            [DrillId.DE2SeededAudit] = PromptContentKind.DiscriminationItemSet,
            [DrillId.CO1RuleExtraction] = PromptContentKind.RuleExampleSet,
            [DrillId.CO2StructureMapping] = PromptContentKind.RuleExampleSet,
            [DrillId.AI1PressureRepeat] = PromptContentKind.EquivalentPrompt,
            [DrillId.AI2DisruptionRecovery] = PromptContentKind.EquivalentPrompt,
            [DrillId.TI1CompositeTask] = PromptContentKind.EquivalentPrompt,
            [DrillId.TI2GlobalReviewTask] = PromptContentKind.EquivalentPrompt,
        };

    private static readonly IReadOnlyDictionary<DrillId, BranchCode> ExpectedBranches =
        new Dictionary<DrillId, BranchCode>
        {
            [DrillId.FH1TargetHold] = BranchCode.FH,
            [DrillId.FH2DistractorHold] = BranchCode.FH,
            [DrillId.FS1CueSwitch] = BranchCode.FS,
            [DrillId.FS2InvalidCueFilter] = BranchCode.FS,
            [DrillId.WM1DelayedReconstruction] = BranchCode.WM,
            [DrillId.WM2MentalTransform] = BranchCode.WM,
            [DrillId.IR1GoNoGoRule] = BranchCode.IR,
            [DrillId.IR2ExceptionRule] = BranchCode.IR,
            [DrillId.DE1PairDiscrimination] = BranchCode.DE,
            [DrillId.DE2SeededAudit] = BranchCode.DE,
            [DrillId.CO1RuleExtraction] = BranchCode.CO,
            [DrillId.CO2StructureMapping] = BranchCode.CO,
            [DrillId.AI1PressureRepeat] = BranchCode.AI,
            [DrillId.AI2DisruptionRecovery] = BranchCode.AI,
            [DrillId.TI1CompositeTask] = BranchCode.TI,
            [DrillId.TI2GlobalReviewTask] = BranchCode.TI,
        };

    private static readonly IReadOnlyDictionary<DrillId, IReadOnlySet<GeneratedContentMaterialKind>> RequiredMaterials =
        new Dictionary<DrillId, IReadOnlySet<GeneratedContentMaterialKind>>
        {
            [DrillId.FH1TargetHold] = Required(
                GeneratedContentMaterialKind.TargetStatement,
                GeneratedContentMaterialKind.TargetType,
                GeneratedContentMaterialKind.TargetSubtlety,
                GeneratedContentMaterialKind.HoldDuration,
                GeneratedContentMaterialKind.RecoveryWindow,
                GeneratedContentMaterialKind.DriftMarkingEvidenceShape),
            [DrillId.FH2DistractorHold] = Required(
                GeneratedContentMaterialKind.TargetStatement,
                GeneratedContentMaterialKind.TargetType,
                GeneratedContentMaterialKind.TargetSubtlety,
                GeneratedContentMaterialKind.HoldDuration,
                GeneratedContentMaterialKind.RecoveryWindow,
                GeneratedContentMaterialKind.DriftMarkingEvidenceShape,
                GeneratedContentMaterialKind.DistractorPrompt,
                GeneratedContentMaterialKind.DistractorTiming,
                GeneratedContentMaterialKind.DistractorFrequency,
                GeneratedContentMaterialKind.DistractorNoResponseRule),
            [DrillId.FS1CueSwitch] = Required(
                GeneratedContentMaterialKind.TargetSet,
                GeneratedContentMaterialKind.CueStep,
                GeneratedContentMaterialKind.ValidCue,
                GeneratedContentMaterialKind.ResponseWindow,
                GeneratedContentMaterialKind.ExpectedActiveTarget),
            [DrillId.FS2InvalidCueFilter] = Required(
                GeneratedContentMaterialKind.TargetSet,
                GeneratedContentMaterialKind.CueStep,
                GeneratedContentMaterialKind.ValidCue,
                GeneratedContentMaterialKind.InvalidCue,
                GeneratedContentMaterialKind.ResponseWindow,
                GeneratedContentMaterialKind.ExpectedActiveTarget),
            [DrillId.WM1DelayedReconstruction] = Required(
                GeneratedContentMaterialKind.EncodeItem,
                GeneratedContentMaterialKind.EncodeInstruction,
                GeneratedContentMaterialKind.DelayLength,
                GeneratedContentMaterialKind.ReconstructionInstruction,
                GeneratedContentMaterialKind.ExpectedReconstruction),
            [DrillId.WM2MentalTransform] = Required(
                GeneratedContentMaterialKind.SourceItem,
                GeneratedContentMaterialKind.TransformRule,
                GeneratedContentMaterialKind.OperationStep,
                GeneratedContentMaterialKind.FinalExpectedOutput,
                GeneratedContentMaterialKind.RuleExplanationPrompt,
                GeneratedContentMaterialKind.HiddenNotePolicy),
            [DrillId.IR1GoNoGoRule] = Required(
                GeneratedContentMaterialKind.GoNoGoCue,
                GeneratedContentMaterialKind.CuePace,
                GeneratedContentMaterialKind.NoGoFrequency,
                GeneratedContentMaterialKind.ExpectedAction,
                GeneratedContentMaterialKind.ResponseWindow),
            [DrillId.IR2ExceptionRule] = Required(
                GeneratedContentMaterialKind.RuleStatement,
                GeneratedContentMaterialKind.ExceptionDefinition,
                GeneratedContentMaterialKind.CueStep,
                GeneratedContentMaterialKind.ExpectedAction,
                GeneratedContentMaterialKind.CuePace),
            [DrillId.DE1PairDiscrimination] = Required(
                GeneratedContentMaterialKind.DiscriminationPair,
                GeneratedContentMaterialKind.RelevantFeature,
                GeneratedContentMaterialKind.MatchTruth,
                GeneratedContentMaterialKind.GuessHandling,
                GeneratedContentMaterialKind.FalsePositiveFalseNegativeKey),
            [DrillId.DE2SeededAudit] = Required(
                GeneratedContentMaterialKind.AuditReference,
                GeneratedContentMaterialKind.LockedOriginalOutput,
                GeneratedContentMaterialKind.SeededError,
                GeneratedContentMaterialKind.ExpectedFinding,
                GeneratedContentMaterialKind.NonErrorDistractor,
                GeneratedContentMaterialKind.AuditInstruction),
            [DrillId.CO1RuleExtraction] = Required(
                GeneratedContentMaterialKind.RuleStatement,
                GeneratedContentMaterialKind.ExpectedRule,
                GeneratedContentMaterialKind.PositiveExample,
                GeneratedContentMaterialKind.UnseenExample,
                GeneratedContentMaterialKind.ExpectedClassification,
                GeneratedContentMaterialKind.RuleFamily),
            [DrillId.CO2StructureMapping] = Required(
                GeneratedContentMaterialKind.SourceStructure,
                GeneratedContentMaterialKind.TargetStructure,
                GeneratedContentMaterialKind.RequiredRelation,
                GeneratedContentMaterialKind.SurfaceLure,
                GeneratedContentMaterialKind.ExpectedMapping,
                GeneratedContentMaterialKind.MappingLimit),
            [DrillId.AI1PressureRepeat] = Required(
                GeneratedContentMaterialKind.SourceBranchStandard,
                GeneratedContentMaterialKind.SourceDrill,
                GeneratedContentMaterialKind.SourceTask,
                GeneratedContentMaterialKind.PressureSource,
                GeneratedContentMaterialKind.NoStandardLoweringMarker),
            [DrillId.AI2DisruptionRecovery] = Required(
                GeneratedContentMaterialKind.SourceDrill,
                GeneratedContentMaterialKind.SourceTask,
                GeneratedContentMaterialKind.DisruptionEvent,
                GeneratedContentMaterialKind.DisruptionTiming,
                GeneratedContentMaterialKind.RestartDelay,
                GeneratedContentMaterialKind.TaskComplexity,
                GeneratedContentMaterialKind.RestartRule,
                GeneratedContentMaterialKind.RecoveryWindow,
                GeneratedContentMaterialKind.PostDisruptionEvidence),
            [DrillId.TI1CompositeTask] = Required(
                GeneratedContentMaterialKind.ComponentPayload,
                GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                GeneratedContentMaterialKind.BranchScoringKey,
                GeneratedContentMaterialKind.CompositeTaskPrompt),
            [DrillId.TI2GlobalReviewTask] = Required(
                GeneratedContentMaterialKind.CompositeTaskPrompt,
                GeneratedContentMaterialKind.AuditPayload,
                GeneratedContentMaterialKind.ExpectedFinding,
                GeneratedContentMaterialKind.DelayedReconstructionPayload,
                GeneratedContentMaterialKind.ExpectedReconstruction,
                GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                GeneratedContentMaterialKind.BranchScoringKey,
                GeneratedContentMaterialKind.PressureSource),
        };

    public static GeneratedContentMaterialValidationResult Validate(
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

        var failures = new List<GeneratedContentMaterialValidationFailure>();
        AddBranchDrillFailures(result, failures);
        AddContentKindFailures(result, failures);
        AddRequiredMaterialFailures(result.Drill, materialArray, failures);
        AddLevelSpecificRequiredMaterialFailures(result, materialArray, failures);
        AddCompositeComponentVisibilityFailures(result.Drill, materialArray, failures);
        AddLoadVariableFailures(result.Request.LoadVariables, materialArray, failures);
        AddHonestyConstraintFailures(result.Request.CriticalConstraints, materialArray, failures);
        AddLoadQuantityFailures(result.Request.LoadVariables, result.Drill, materialArray, failures);

        return new GeneratedContentMaterialValidationResult(failures);
    }

    private static void AddBranchDrillFailures(
        GeneratedDrillContentResult result,
        ICollection<GeneratedContentMaterialValidationFailure> failures)
    {
        if (!ExpectedBranches.TryGetValue(result.Drill, out var expectedBranch))
        {
            return;
        }

        if (result.Branch == expectedBranch)
        {
            return;
        }

        failures.Add(new GeneratedContentMaterialValidationFailure(
            GeneratedContentMaterialValidationFailureKind.BranchDrillMismatch,
            null,
            $"Drill {result.Drill} belongs to branch {expectedBranch}, not {result.Branch}."));
    }

    private static void AddContentKindFailures(
        GeneratedDrillContentResult result,
        ICollection<GeneratedContentMaterialValidationFailure> failures)
    {
        if (!ExpectedContentKinds.TryGetValue(result.Drill, out var expectedKind))
        {
            return;
        }

        if (result.ContentKind == expectedKind)
        {
            return;
        }

        failures.Add(new GeneratedContentMaterialValidationFailure(
            GeneratedContentMaterialValidationFailureKind.ContentKindMismatch,
            null,
            $"Drill {result.Drill} requires {expectedKind} generated content, not {result.ContentKind}."));
    }

    private static void AddRequiredMaterialFailures(
        DrillId drill,
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentMaterialValidationFailure> failures)
    {
        if (!RequiredMaterials.TryGetValue(drill, out var requiredMaterials))
        {
            return;
        }

        var materialKinds = materials
            .Select(material => material.Kind)
            .ToHashSet();

        foreach (var requiredMaterial in requiredMaterials.OrderBy(kind => kind))
        {
            if (materialKinds.Contains(requiredMaterial))
            {
                continue;
            }

            failures.Add(new GeneratedContentMaterialValidationFailure(
                GeneratedContentMaterialValidationFailureKind.MissingRequiredMaterial,
                requiredMaterial,
                $"Generated content for {drill} is missing required {requiredMaterial} material."));
        }
    }

    private static void AddLevelSpecificRequiredMaterialFailures(
        GeneratedDrillContentResult result,
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentMaterialValidationFailure> failures)
    {
        if (result.Drill != DrillId.CO2StructureMapping ||
            (int)result.Level < (int)GlobalLevelId.L4)
        {
            return;
        }

        var materialKinds = materials.Select(material => material.Kind).ToHashSet();
        foreach (var requiredMaterial in new[]
                 {
                     GeneratedContentMaterialKind.AuditPayload,
                     GeneratedContentMaterialKind.ExpectedFinding,
                 })
        {
            if (!materialKinds.Contains(requiredMaterial))
            {
                failures.Add(new GeneratedContentMaterialValidationFailure(
                    GeneratedContentMaterialValidationFailureKind.MissingRequiredMaterial,
                    requiredMaterial,
                    $"Generated content for {result.Branch} {result.Level} is missing required {requiredMaterial} material."));
            }
        }
    }

    private static void AddCompositeComponentVisibilityFailures(
        DrillId drill,
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentMaterialValidationFailure> failures)
    {
        if (drill != DrillId.TI1CompositeTask)
        {
            return;
        }

        var componentBranches = BranchesNamedByMaterials(
            materials,
            GeneratedContentMaterialKind.ComponentPayload);
        if (componentBranches.Count == 0)
        {
            return;
        }

        var evidenceBranches = BranchesNamedByMaterials(
            materials,
            GeneratedContentMaterialKind.ComponentEvidenceRequirement);
        var scoringBranches = BranchesNamedByMaterials(
            materials,
            GeneratedContentMaterialKind.BranchScoringKey);

        foreach (var branch in componentBranches.OrderBy(item => item))
        {
            if (!evidenceBranches.Contains(branch))
            {
                failures.Add(new GeneratedContentMaterialValidationFailure(
                    GeneratedContentMaterialValidationFailureKind.MissingComponentEvidenceRequirement,
                    GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                    $"TI component {branch} is missing a separate component evidence requirement."));
            }

            if (!scoringBranches.Contains(branch))
            {
                failures.Add(new GeneratedContentMaterialValidationFailure(
                    GeneratedContentMaterialValidationFailureKind.MissingComponentScoringKey,
                    GeneratedContentMaterialKind.BranchScoringKey,
                    $"TI component {branch} is missing a visible branch scoring key."));
            }
        }
    }

    private static IReadOnlySet<BranchCode> BranchesNamedByMaterials(
        IEnumerable<GeneratedContentMaterial> materials,
        GeneratedContentMaterialKind materialKind)
    {
        var branches = new HashSet<BranchCode>();

        foreach (var material in materials.Where(item => item.Kind == materialKind))
        {
            foreach (var branch in Enum.GetValues<BranchCode>())
            {
                if (MaterialNamesBranch(material, branch))
                {
                    branches.Add(branch);
                }
            }
        }

        return branches;
    }

    private static bool MaterialNamesBranch(
        GeneratedContentMaterial material,
        BranchCode branch)
    {
        var branchId = branch.ToString();
        var branchNameSuffix = "-" + branchId.ToLowerInvariant();

        return material.Name.EndsWith(branchNameSuffix, StringComparison.OrdinalIgnoreCase) ||
            material.Value.Contains("branch " + branchId, StringComparison.OrdinalIgnoreCase) ||
            material.Value.Contains("component branch " + branchId, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddLoadVariableFailures(
        IEnumerable<LoadVariable> loadVariables,
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentMaterialValidationFailure> failures)
    {
        var loadMaterialByName = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.LoadVariable)
            .GroupBy(material => material.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        foreach (var loadVariable in loadVariables)
        {
            if (!loadMaterialByName.TryGetValue(loadVariable.Name, out var candidates))
            {
                failures.Add(new GeneratedContentMaterialValidationFailure(
                    GeneratedContentMaterialValidationFailureKind.MissingLoadVariableMaterial,
                    GeneratedContentMaterialKind.LoadVariable,
                    $"Generated content is missing material for load variable '{loadVariable.Name}'."));
                continue;
            }

            if (candidates.Any(material => string.Equals(material.Value, loadVariable.Value, StringComparison.Ordinal)))
            {
                continue;
            }

            failures.Add(new GeneratedContentMaterialValidationFailure(
                GeneratedContentMaterialValidationFailureKind.LoadVariableMaterialMismatch,
                GeneratedContentMaterialKind.LoadVariable,
                $"Generated content load variable '{loadVariable.Name}' does not match required value '{loadVariable.Value}'."));
        }
    }

    private static void AddHonestyConstraintFailures(
        IEnumerable<CriticalConstraint> criticalConstraints,
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentMaterialValidationFailure> failures)
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

            failures.Add(new GeneratedContentMaterialValidationFailure(
                GeneratedContentMaterialValidationFailureKind.MissingHonestyConstraintMaterial,
                GeneratedContentMaterialKind.HonestyConstraint,
                $"Generated content does not preserve honesty constraint '{constraint.Description}'."));
        }
    }

    private static void AddLoadQuantityFailures(
        IEnumerable<LoadVariable> loadVariables,
        DrillId drill,
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentMaterialValidationFailure> failures)
    {
        var itemCount = ParseLoadCount(loadVariables, "item count");
        if (itemCount is null)
        {
            return;
        }

        var materialKind = drill switch
        {
            DrillId.WM1DelayedReconstruction => GeneratedContentMaterialKind.EncodeItem,
            DrillId.WM2MentalTransform => GeneratedContentMaterialKind.SourceItem,
            _ => (GeneratedContentMaterialKind?)null,
        };

        if (materialKind is null)
        {
            return;
        }

        var providedCount = materials.Count(material => material.Kind == materialKind.Value);
        if (providedCount >= itemCount.Value)
        {
            return;
        }

        failures.Add(new GeneratedContentMaterialValidationFailure(
            GeneratedContentMaterialValidationFailureKind.InsufficientLoadMaterial,
            materialKind,
            $"Generated content has {providedCount} {materialKind} materials but load variable 'item count' requires {itemCount.Value}."));
    }

    private static int? ParseLoadCount(IEnumerable<LoadVariable> loadVariables, string name)
    {
        var rawValue = loadVariables
            .FirstOrDefault(variable => string.Equals(variable.Name, name, StringComparison.Ordinal))
            ?.Value;

        if (rawValue is null)
        {
            return null;
        }

        var numericPrefix = new string(rawValue
            .Trim()
            .TakeWhile(char.IsDigit)
            .ToArray());

        if (int.TryParse(numericPrefix, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            return count;
        }

        return null;
    }

    private static IReadOnlySet<GeneratedContentMaterialKind> Required(params GeneratedContentMaterialKind[] kinds)
    {
        return new HashSet<GeneratedContentMaterialKind>(kinds);
    }
}

public sealed class ValidatedGeneratedDrillContent
{
    private ValidatedGeneratedDrillContent(
        GeneratedDrillContentResult result,
        IReadOnlyList<GeneratedContentMaterial> materials)
    {
        Result = result;
        Materials = materials;
    }

    public GeneratedDrillContentResult Result { get; }

    public IReadOnlyList<GeneratedContentMaterial> Materials { get; }

    public bool CanBeConsumedByRuntime => true;

    public bool GrantsAdvancement => false;

    public static ValidatedGeneratedDrillContent Create(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);

        var materialArray = materials.ToArray();
        var materialValidation = GeneratedContentMaterialValidator.Validate(result, materialArray);
        var loadValidation = GeneratedContentLoadConstraintValidator.Validate(result, materialArray);
        if (!materialValidation.IsValid || !loadValidation.IsValid)
        {
            var details = materialValidation.Failures.Select(failure => failure.Detail)
                .Concat(loadValidation.Failures.Select(failure => failure.Detail))
                .Distinct(StringComparer.Ordinal);
            throw new InvalidOperationException(
                "Generated content cannot be consumed by the runtime until material validation passes: " +
                string.Join("; ", details));
        }

        return new ValidatedGeneratedDrillContent(result, Array.AsReadOnly(materialArray));
    }
}
