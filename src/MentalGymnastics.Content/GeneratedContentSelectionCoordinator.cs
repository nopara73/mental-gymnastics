using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class GeneratedContentSelectionNeed
{
    private GeneratedContentSelectionNeed(
        GeneratedDrillContentRequest contentRequest,
        TransferContentGenerationRequest? transferRequest,
        LoadChangeMode loadChangeMode,
        bool increasedVariablesStableSeparately)
    {
        ArgumentNullException.ThrowIfNull(contentRequest);
        GeneratedContentValidation.EnsureDefined(loadChangeMode, nameof(loadChangeMode));

        if (transferRequest is not null &&
            !ReferenceEquals(contentRequest, transferRequest.ContentRequest))
        {
            throw new ArgumentException(
                "Transfer content selection must use the same content request as the transfer generation request.",
                nameof(transferRequest));
        }

        ContentRequest = contentRequest;
        TransferRequest = transferRequest;
        LoadChangeMode = loadChangeMode;
        IncreasedVariablesStableSeparately = increasedVariablesStableSeparately;
    }

    public GeneratedDrillContentRequest ContentRequest { get; }

    public TransferContentGenerationRequest? TransferRequest { get; }

    public LoadChangeMode LoadChangeMode { get; }

    public bool IncreasedVariablesStableSeparately { get; }

    public bool IsTransfer => TransferRequest is not null;

    public static GeneratedContentSelectionNeed ForStandardContent(
        GeneratedDrillContentRequest contentRequest,
        LoadChangeMode loadChangeMode = LoadChangeMode.Acquisition,
        bool increasedVariablesStableSeparately = false)
    {
        ArgumentNullException.ThrowIfNull(contentRequest);

        if (contentRequest.SessionType == SessionType.Transfer)
        {
            throw new ArgumentException(
                "Transfer session content selection requires transfer generation requirements.",
                nameof(contentRequest));
        }

        return new GeneratedContentSelectionNeed(
            contentRequest,
            transferRequest: null,
            loadChangeMode,
            increasedVariablesStableSeparately);
    }

    public static GeneratedContentSelectionNeed ForTransferContent(
        TransferContentGenerationRequest transferRequest)
    {
        ArgumentNullException.ThrowIfNull(transferRequest);

        return new GeneratedContentSelectionNeed(
            transferRequest.ContentRequest,
            transferRequest,
            LoadChangeMode.Acquisition,
            increasedVariablesStableSeparately: false);
    }
}

public sealed class GeneratedContentSelectionResult
{
    internal GeneratedContentSelectionResult(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials,
        GeneratedContentFreshnessPolicyResult freshnessValidation,
        GeneratedContentAntiSelfDeceptionGuardResult antiSelfDeceptionGuard,
        ValidatedGeneratedDrillContent? validatedContent,
        GeneratedContentDifficultyAuditResult? difficultyAudit,
        TransferContentRuleValidationResult? transferValidation,
        TransferEligibilityRequest? transferEligibilityRequest)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(freshnessValidation);
        ArgumentNullException.ThrowIfNull(antiSelfDeceptionGuard);

        var materialArray = materials.ToArray();
        if (materialArray.Any(material => material is null))
        {
            throw new ArgumentException(
                "Generated content selection materials cannot contain null entries.",
                nameof(materials));
        }

        Result = result;
        Materials = Array.AsReadOnly(materialArray);
        FreshnessValidation = freshnessValidation;
        AntiSelfDeceptionGuard = antiSelfDeceptionGuard;
        ValidatedContent = validatedContent;
        DifficultyAudit = difficultyAudit;
        TransferValidation = transferValidation;
        TransferEligibilityRequest = transferEligibilityRequest;
    }

    public GeneratedDrillContentResult Result { get; }

    public IReadOnlyList<GeneratedContentMaterial> Materials { get; }

    public GeneratedContentFreshnessPolicyResult FreshnessValidation { get; }

    public GeneratedContentAntiSelfDeceptionGuardResult AntiSelfDeceptionGuard { get; }

    public ValidatedGeneratedDrillContent? ValidatedContent { get; }

    public GeneratedContentDifficultyAuditResult? DifficultyAudit { get; }

    public TransferContentRuleValidationResult? TransferValidation { get; }

    public TransferEligibilityRequest? TransferEligibilityRequest { get; }

    public bool IsTransfer => TransferValidation is not null;

    public bool IsValid =>
        FreshnessValidation.CanUseContent &&
        AntiSelfDeceptionGuard.IsValid &&
        (IsTransfer
            ? TransferValidation?.IsValid == true
            : ValidatedContent is not null && DifficultyAudit?.IsValid == true);

    public bool CanBeConsumedByRuntime =>
        IsValid &&
        Result.CanBeConsumedByRuntime &&
        AntiSelfDeceptionGuard.CanBeConsumedByRuntime;

    public bool CanBeRecordedByPersistence =>
        IsValid &&
        Result.CanBeRecordedByPersistence &&
        AntiSelfDeceptionGuard.CanBeRecordedByPersistence;

    public bool OwnsProgressionDecision => false;

    public bool DecidesReadiness => false;

    public bool DecidesOwnership => false;

    public bool DecidesMaintenance => false;

    public bool DecidesDecay => false;

    public bool GrantsAdvancement => false;
}

public static class GeneratedContentSelectionCoordinator
{
    public static GeneratedContentSelectionResult Select(
        GeneratedContentSelectionNeed need,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(need);
        ArgumentNullException.ThrowIfNull(seed);

        if (need.TransferRequest is not null)
        {
            return SelectTransferContent(need.TransferRequest, seed);
        }

        return SelectStandardContent(need, seed);
    }

    private static GeneratedContentSelectionResult SelectStandardContent(
        GeneratedContentSelectionNeed need,
        GeneratedContentSeed seed)
    {
        var generated = GenerateStandardContent(need.ContentRequest, seed);
        var freshnessValidation = EvaluateFreshness(generated.Result);
        var difficultyAudit = AuditDifficulty(
            generated.Result,
            generated.Materials,
            need.LoadChangeMode,
            need.IncreasedVariablesStableSeparately);
        var antiSelfDeceptionGuard = EvaluateAntiSelfDeceptionGuard(
            generated.Result,
            generated.Materials,
            need.LoadChangeMode,
            need.IncreasedVariablesStableSeparately);

        return new GeneratedContentSelectionResult(
            generated.Result,
            generated.Materials,
            freshnessValidation,
            antiSelfDeceptionGuard,
            generated.ValidatedContent,
            difficultyAudit,
            transferValidation: null,
            transferEligibilityRequest: null);
    }

    private static GeneratedContentSelectionResult SelectTransferContent(
        TransferContentGenerationRequest request,
        GeneratedContentSeed seed)
    {
        var generated = TransferGeneratedContentGenerator.Generate(request, seed);
        var freshnessValidation = EvaluateFreshness(generated.Result);
        var antiSelfDeceptionGuard = GeneratedContentAntiSelfDeceptionGuard.EvaluateTransfer(
            generated.TransferValidation);

        return new GeneratedContentSelectionResult(
            generated.Result,
            generated.Materials,
            freshnessValidation,
            antiSelfDeceptionGuard,
            validatedContent: null,
            difficultyAudit: null,
            generated.TransferValidation,
            generated.TransferEligibilityRequest);
    }

    private static StandardGeneratedContent GenerateStandardContent(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed)
    {
        return request.Branch switch
        {
            BranchCode.FH => From(FocusHoldGeneratedContentGenerator.Generate(request, seed)),
            BranchCode.FS => From(FocusShiftGeneratedContentGenerator.Generate(request, seed)),
            BranchCode.WM => From(WorkingMemoryGeneratedContentGenerator.Generate(request, seed)),
            BranchCode.IR => From(InhibitionGeneratedContentGenerator.Generate(request, seed)),
            BranchCode.DE => From(DiscriminationGeneratedContentGenerator.Generate(request, seed)),
            BranchCode.CO => From(ConceptOperationsGeneratedContentGenerator.Generate(request, seed)),
            BranchCode.AI => From(AffectiveInterferenceGeneratedContentGenerator.Generate(request, seed)),
            BranchCode.TI => From(TransferIntegrationGeneratedContentGenerator.Generate(request, seed)),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Branch, "Unsupported generated content branch."),
        };
    }

    private static StandardGeneratedContent From(FocusHoldGeneratedContent content)
    {
        return new StandardGeneratedContent(content.Result, content.Materials, content.ValidatedContent);
    }

    private static StandardGeneratedContent From(FocusShiftGeneratedContent content)
    {
        return new StandardGeneratedContent(content.Result, content.Materials, content.ValidatedContent);
    }

    private static StandardGeneratedContent From(WorkingMemoryGeneratedContent content)
    {
        return new StandardGeneratedContent(content.Result, content.Materials, content.ValidatedContent);
    }

    private static StandardGeneratedContent From(InhibitionGeneratedContent content)
    {
        return new StandardGeneratedContent(content.Result, content.Materials, content.ValidatedContent);
    }

    private static StandardGeneratedContent From(DiscriminationGeneratedContent content)
    {
        return new StandardGeneratedContent(content.Result, content.Materials, content.ValidatedContent);
    }

    private static StandardGeneratedContent From(ConceptOperationsGeneratedContent content)
    {
        return new StandardGeneratedContent(content.Result, content.Materials, content.ValidatedContent);
    }

    private static StandardGeneratedContent From(AffectiveInterferenceGeneratedContent content)
    {
        return new StandardGeneratedContent(content.Result, content.Materials, content.ValidatedContent);
    }

    private static StandardGeneratedContent From(TransferIntegrationGeneratedContent content)
    {
        return new StandardGeneratedContent(content.Result, content.Materials, content.ValidatedContent);
    }

    private static GeneratedContentFreshnessPolicyResult EvaluateFreshness(
        GeneratedDrillContentResult result)
    {
        var standard = StandardFor(result.Request);
        var loadIntent = LoadIntentFor(result.Request);

        return GeneratedContentFreshnessPolicy.Evaluate(
            new GeneratedContentEquivalenceRequirement(result.Request, standard, loadIntent),
            new GeneratedContentEquivalenceCandidate(result.Instance, standard, loadIntent));
    }

    private static GeneratedContentDifficultyAuditResult AuditDifficulty(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials,
        LoadChangeMode loadChangeMode,
        bool increasedVariablesStableSeparately)
    {
        var standard = StandardFor(result.Request);
        var loadIntent = LoadIntentFor(result.Request);

        return GeneratedContentDifficultyAuditor.Audit(
            result,
            materials,
            new GeneratedContentEquivalenceRequirement(result.Request, standard, loadIntent),
            new GeneratedContentEquivalenceCandidate(result.Instance, standard, loadIntent),
            loadChangeMode,
            increasedVariablesStableSeparately);
    }

    private static GeneratedContentAntiSelfDeceptionGuardResult EvaluateAntiSelfDeceptionGuard(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials,
        LoadChangeMode loadChangeMode,
        bool increasedVariablesStableSeparately)
    {
        var standard = StandardFor(result.Request);
        var loadIntent = LoadIntentFor(result.Request);

        return GeneratedContentAntiSelfDeceptionGuard.Evaluate(
            new GeneratedContentAntiSelfDeceptionGuardRequest(
                result,
                materials,
                new GeneratedContentEquivalenceRequirement(result.Request, standard, loadIntent),
                new GeneratedContentEquivalenceCandidate(result.Instance, standard, loadIntent),
                loadChangeMode,
                increasedVariablesStableSeparately));
    }

    private static string StandardFor(GeneratedDrillContentRequest request)
    {
        return ProgramCatalog.Standards.Single(standard =>
            standard.Branch == request.Branch &&
            standard.Level == request.Level).Standard;
    }

    private static string LoadIntentFor(GeneratedDrillContentRequest request)
    {
        return string.Join(
            "; ",
            request.LoadVariables.Select(variable => variable.Name + ": " + variable.Value));
    }

    private sealed record StandardGeneratedContent(
        GeneratedDrillContentResult Result,
        IReadOnlyList<GeneratedContentMaterial> Materials,
        ValidatedGeneratedDrillContent ValidatedContent);
}
