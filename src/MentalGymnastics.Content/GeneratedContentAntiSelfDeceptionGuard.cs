using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public enum GeneratedContentEscapeRouteKind
{
    TooEasyVariant,
    MissingEvidence,
    RemovedConstraint,
    UntrackedGuesses,
    HiddenRereading,
    NoveltyOnlyTransfer,
    UnsupportedPressureChange,
}

public sealed record GeneratedContentEscapeRoute(
    GeneratedContentEscapeRouteKind Kind,
    string Detail);

public sealed class GeneratedContentAntiSelfDeceptionGuardRequest
{
    public GeneratedContentAntiSelfDeceptionGuardRequest(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials,
        GeneratedContentEquivalenceRequirement equivalenceRequirement,
        GeneratedContentEquivalenceCandidate equivalenceCandidate,
        LoadChangeMode loadChangeMode,
        bool increasedVariablesStableSeparately = false,
        TransferContentRuleValidationResult? transferValidation = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(equivalenceRequirement);
        ArgumentNullException.ThrowIfNull(equivalenceCandidate);
        GeneratedContentValidation.EnsureDefined(loadChangeMode, nameof(loadChangeMode));

        var materialArray = materials.ToArray();
        if (materialArray.Any(material => material is null))
        {
            throw new ArgumentException(
                "Generated content guard material collections cannot contain null entries.",
                nameof(materials));
        }

        Result = result;
        Materials = Array.AsReadOnly(materialArray);
        EquivalenceRequirement = equivalenceRequirement;
        EquivalenceCandidate = equivalenceCandidate;
        LoadChangeMode = loadChangeMode;
        IncreasedVariablesStableSeparately = increasedVariablesStableSeparately;
        TransferValidation = transferValidation;
    }

    public GeneratedDrillContentResult Result { get; }

    public IReadOnlyList<GeneratedContentMaterial> Materials { get; }

    public GeneratedContentEquivalenceRequirement EquivalenceRequirement { get; }

    public GeneratedContentEquivalenceCandidate EquivalenceCandidate { get; }

    public LoadChangeMode LoadChangeMode { get; }

    public bool IncreasedVariablesStableSeparately { get; }

    public TransferContentRuleValidationResult? TransferValidation { get; }
}

public sealed class GeneratedContentAntiSelfDeceptionGuardResult
{
    public GeneratedContentAntiSelfDeceptionGuardResult(
        IEnumerable<GeneratedContentEscapeRoute> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        Findings = Array.AsReadOnly(findings.ToArray());
    }

    public IReadOnlyList<GeneratedContentEscapeRoute> Findings { get; }

    public bool IsValid => Findings.Count == 0;

    public bool CanUseContent => IsValid;

    public bool CanBeConsumedByRuntime => IsValid;

    public bool CanBeRecordedByPersistence => IsValid;

    public bool OwnsProgressionDecision => false;

    public bool GrantsAdvancement => false;
}

public static class GeneratedContentAntiSelfDeceptionGuard
{
    public static GeneratedContentAntiSelfDeceptionGuardResult Evaluate(
        GeneratedContentAntiSelfDeceptionGuardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var findings = new List<GeneratedContentEscapeRoute>();
        var seen = new HashSet<(GeneratedContentEscapeRouteKind Kind, string Detail)>();
        var materialValidation = GeneratedContentMaterialValidator.Validate(request.Result, request.Materials);
        var difficultyAudit = GeneratedContentDifficultyAuditor.Audit(
            request.Result,
            request.Materials,
            request.EquivalenceRequirement,
            request.EquivalenceCandidate,
            request.LoadChangeMode,
            request.IncreasedVariablesStableSeparately);

        AddDifficultyFindings(difficultyAudit, findings, seen);
        AddMaterialFindings(materialValidation, findings, seen);
        AddProtocolConstraintFindings(request.Result, request.Materials, findings, seen);
        AddRereadingFindings(request.Result, request.Materials, findings, seen);
        AddGuessFindings(request.Result, request.Materials, findings, seen);
        AddPressureFindings(request.Result, request.Materials, findings, seen);

        if (request.TransferValidation is not null)
        {
            AddTransferFindings(request.TransferValidation, findings, seen);
        }

        return new GeneratedContentAntiSelfDeceptionGuardResult(findings);
    }

    public static GeneratedContentAntiSelfDeceptionGuardResult EvaluateTransfer(
        TransferContentRuleValidationResult transferValidation)
    {
        ArgumentNullException.ThrowIfNull(transferValidation);

        var findings = new List<GeneratedContentEscapeRoute>();
        var seen = new HashSet<(GeneratedContentEscapeRouteKind Kind, string Detail)>();
        AddTransferFindings(transferValidation, findings, seen);

        return new GeneratedContentAntiSelfDeceptionGuardResult(findings);
    }

    private static void AddDifficultyFindings(
        GeneratedContentDifficultyAuditResult audit,
        ICollection<GeneratedContentEscapeRoute> findings,
        ISet<(GeneratedContentEscapeRouteKind Kind, string Detail)> seen)
    {
        foreach (var failure in audit.Failures)
        {
            if (failure.Kind is
                GeneratedContentDifficultyAuditFailureKind.RequestedLoadNotRepresented or
                GeneratedContentDifficultyAuditFailureKind.CoreDemandChanged)
            {
                Add(
                    findings,
                    seen,
                    GeneratedContentEscapeRouteKind.TooEasyVariant,
                    failure.Detail);
            }

            if (failure.Kind == GeneratedContentDifficultyAuditFailureKind.HonestyConstraintRemoved)
            {
                Add(
                    findings,
                    seen,
                    GeneratedContentEscapeRouteKind.RemovedConstraint,
                    failure.Detail);
            }
        }
    }

    private static void AddMaterialFindings(
        GeneratedContentMaterialValidationResult validation,
        ICollection<GeneratedContentEscapeRoute> findings,
        ISet<(GeneratedContentEscapeRouteKind Kind, string Detail)> seen)
    {
        foreach (var failure in validation.Failures)
        {
            if (failure.Kind is
                GeneratedContentMaterialValidationFailureKind.MissingComponentEvidenceRequirement or
                GeneratedContentMaterialValidationFailureKind.MissingComponentScoringKey)
            {
                Add(
                    findings,
                    seen,
                    GeneratedContentEscapeRouteKind.MissingEvidence,
                    failure.Detail);
                continue;
            }

            if (failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingRequiredMaterial &&
                IsEvidenceMaterial(failure.MaterialKind))
            {
                Add(
                    findings,
                    seen,
                    GeneratedContentEscapeRouteKind.MissingEvidence,
                    failure.Detail);
                continue;
            }

            if (failure.Kind == GeneratedContentMaterialValidationFailureKind.InsufficientLoadMaterial)
            {
                Add(
                    findings,
                    seen,
                    GeneratedContentEscapeRouteKind.TooEasyVariant,
                    failure.Detail);
            }

            if (failure.Kind == GeneratedContentMaterialValidationFailureKind.MissingHonestyConstraintMaterial)
            {
                Add(
                    findings,
                    seen,
                    GeneratedContentEscapeRouteKind.RemovedConstraint,
                    failure.Detail);
            }
        }
    }

    private static void AddProtocolConstraintFindings(
        GeneratedDrillContentResult result,
        IReadOnlyList<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentEscapeRoute> findings,
        ISet<(GeneratedContentEscapeRouteKind Kind, string Detail)> seen)
    {
        var protocol = DrillProtocolCatalog.StandardDrills.Single(drill => drill.Id == result.Drill);
        var requestConstraints = result.Request.CriticalConstraints
            .Select(constraint => constraint.Description)
            .ToArray();
        var materialConstraints = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.HonestyConstraint)
            .Select(material => material.Value)
            .ToArray();

        foreach (var protocolConstraint in protocol.HonestyConstraints)
        {
            if (!ContainsNormalized(requestConstraints, protocolConstraint.Description) ||
                !ContainsNormalized(materialConstraints, protocolConstraint.Description))
            {
                Add(
                    findings,
                    seen,
                    GeneratedContentEscapeRouteKind.RemovedConstraint,
                    $"Generated content does not preserve drill honesty constraint '{protocolConstraint.Description}'.");
            }
        }
    }

    private static void AddRereadingFindings(
        GeneratedDrillContentResult result,
        IReadOnlyList<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentEscapeRoute> findings,
        ISet<(GeneratedContentEscapeRouteKind Kind, string Detail)> seen)
    {
        if (result.Drill is not (DrillId.WM1DelayedReconstruction or DrillId.TI2GlobalReviewTask))
        {
            return;
        }

        var instructionMaterials = materials.Where(material =>
            material.Kind is
                GeneratedContentMaterialKind.EncodeInstruction or
                GeneratedContentMaterialKind.ReconstructionInstruction or
                GeneratedContentMaterialKind.DelayedReconstructionPayload);

        if (instructionMaterials.Any(material => AllowsRereading(material.Value)) ||
            !ContainsNormalized(result.Request.CriticalConstraints.Select(constraint => constraint.Description), "No rereading after encode window") ||
            !ContainsNormalized(
                materials
                    .Where(material => material.Kind == GeneratedContentMaterialKind.HonestyConstraint)
                    .Select(material => material.Value),
                "No rereading after encode window"))
        {
            Add(
                findings,
                seen,
                GeneratedContentEscapeRouteKind.HiddenRereading,
                "Generated content allows or fails to prohibit rereading after the encode window.");
        }
    }

    private static void AddGuessFindings(
        GeneratedDrillContentResult result,
        IReadOnlyList<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentEscapeRoute> findings,
        ISet<(GeneratedContentEscapeRouteKind Kind, string Detail)> seen)
    {
        if (result.Drill != DrillId.DE1PairDiscrimination)
        {
            return;
        }

        var guessMaterials = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.GuessHandling)
            .ToArray();
        var guessMaterialTracksGuesses = guessMaterials.Any(material =>
            ContainsWord(material.Value, "mark") ||
            ContainsWord(material.Value, "marked") ||
            ContainsWord(material.Value, "tracked"));

        if (guessMaterials.Length == 0 ||
            !guessMaterialTracksGuesses ||
            guessMaterials.Any(material =>
                ContainsNormalized(material.Value, "guessing optional") ||
                ContainsNormalized(material.Value, "untracked") ||
                ContainsNormalized(material.Value, "not tracked")) ||
            !ContainsNormalized(result.Request.CriticalConstraints.Select(constraint => constraint.Description), "Guessing must be marked") ||
            !ContainsNormalized(
                materials
                    .Where(material => material.Kind == GeneratedContentMaterialKind.HonestyConstraint)
                    .Select(material => material.Value),
                "Guessing must be marked"))
        {
            Add(
                findings,
                seen,
                GeneratedContentEscapeRouteKind.UntrackedGuesses,
                "Generated discrimination content must require marked guesses and make unmarked guesses auditable.");
        }
    }

    private static void AddPressureFindings(
        GeneratedDrillContentResult result,
        IReadOnlyList<GeneratedContentMaterial> materials,
        ICollection<GeneratedContentEscapeRoute> findings,
        ISet<(GeneratedContentEscapeRouteKind Kind, string Detail)> seen)
    {
        if (result.Branch != BranchCode.AI)
        {
            return;
        }

        foreach (var material in materials.Where(IsPressureMaterial))
        {
            if (PressurePreventsEvidence(material.Value) ||
                PressureLowersStandard(material.Value))
            {
                Add(
                    findings,
                    seen,
                    GeneratedContentEscapeRouteKind.UnsupportedPressureChange,
                    $"Unsupported pressure material '{material.Name}' changes the task by preventing clean evidence or lowering the source standard.");
            }
        }
    }

    private static void AddTransferFindings(
        TransferContentRuleValidationResult validation,
        ICollection<GeneratedContentEscapeRoute> findings,
        ISet<(GeneratedContentEscapeRouteKind Kind, string Detail)> seen)
    {
        foreach (var failure in validation.Failures)
        {
            if (failure.Kind == TransferContentRuleFailureKind.NoveltyOnlyTransferContent)
            {
                Add(
                    findings,
                    seen,
                    GeneratedContentEscapeRouteKind.NoveltyOnlyTransfer,
                    failure.Detail);
            }

            if (failure.Kind == TransferContentRuleFailureKind.MissingSourceStandardVisibility)
            {
                Add(
                    findings,
                    seen,
                    GeneratedContentEscapeRouteKind.MissingEvidence,
                    failure.Detail);
            }
        }
    }

    private static bool IsEvidenceMaterial(GeneratedContentMaterialKind? materialKind)
    {
        return materialKind is
            GeneratedContentMaterialKind.DriftMarkingEvidenceShape or
            GeneratedContentMaterialKind.ExpectedReconstruction or
            GeneratedContentMaterialKind.FinalExpectedOutput or
            GeneratedContentMaterialKind.RuleExplanationPrompt or
            GeneratedContentMaterialKind.FalsePositiveFalseNegativeKey or
            GeneratedContentMaterialKind.ExpectedFinding or
            GeneratedContentMaterialKind.ExpectedClassification or
            GeneratedContentMaterialKind.ExpectedMapping or
            GeneratedContentMaterialKind.SourceBranchStandard or
            GeneratedContentMaterialKind.PostDisruptionEvidence or
            GeneratedContentMaterialKind.ComponentEvidenceRequirement or
            GeneratedContentMaterialKind.BranchScoringKey or
            GeneratedContentMaterialKind.AuditPayload or
            GeneratedContentMaterialKind.DelayedReconstructionPayload or
            GeneratedContentMaterialKind.RetestRequirement;
    }

    private static bool IsPressureMaterial(GeneratedContentMaterial material)
    {
        return material.Kind is
            GeneratedContentMaterialKind.PressureSource or
            GeneratedContentMaterialKind.TimePressure or
            GeneratedContentMaterialKind.EvaluativePressure or
            GeneratedContentMaterialKind.FrustrationPressure or
            GeneratedContentMaterialKind.UncertaintyPressure;
    }

    private static bool AllowsRereading(string value)
    {
        return ContainsNormalized(value, "rereading is allowed") ||
            ContainsNormalized(value, "reread allowed") ||
            ContainsNormalized(value, "may reread") ||
            ContainsNormalized(value, "can reread") ||
            ContainsNormalized(value, "allow rereading");
    }

    private static bool PressurePreventsEvidence(string value)
    {
        return ContainsNormalized(value, "prevents clean evidence") ||
            ContainsNormalized(value, "prevents evidence") ||
            ContainsNormalized(value, "hides evidence") ||
            ContainsNormalized(value, "without evidence collection");
    }

    private static bool PressureLowersStandard(string value)
    {
        return ContainsNormalized(value, "standard can be lowered") ||
            ContainsNormalized(value, "lower source standard") ||
            ContainsNormalized(value, "lowered standard allowed");
    }

    private static bool ContainsNormalized(IEnumerable<string> values, string required)
    {
        var normalizedRequired = Normalize(required);

        return values.Any(value =>
        {
            var normalizedValue = Normalize(value);

            return normalizedValue.Contains(normalizedRequired, StringComparison.Ordinal) ||
                normalizedRequired.Contains(normalizedValue, StringComparison.Ordinal);
        });
    }

    private static bool ContainsNormalized(string value, string required)
    {
        return Normalize(value).Contains(Normalize(required), StringComparison.Ordinal);
    }

    private static bool ContainsWord(string value, string word)
    {
        return Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(Normalize(word), StringComparer.Ordinal);
    }

    private static string Normalize(string value)
    {
        var characters = value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();

        return string.Join(
            " ",
            new string(characters).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static void Add(
        ICollection<GeneratedContentEscapeRoute> findings,
        ISet<(GeneratedContentEscapeRouteKind Kind, string Detail)> seen,
        GeneratedContentEscapeRouteKind kind,
        string detail)
    {
        var key = (kind, detail);
        if (!seen.Add(key))
        {
            return;
        }

        findings.Add(new GeneratedContentEscapeRoute(kind, detail));
    }
}
