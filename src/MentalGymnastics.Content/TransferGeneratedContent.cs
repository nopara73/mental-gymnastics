using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class TransferContentGenerationRequest
{
    public TransferContentGenerationRequest(
        GeneratedDrillContentRequest contentRequest,
        CapacityId trainedCapacity,
        string transferDistance)
    {
        ArgumentNullException.ThrowIfNull(contentRequest);
        GeneratedContentValidation.EnsureDefined(trainedCapacity, nameof(trainedCapacity));

        if (contentRequest.SessionType != SessionType.Transfer)
        {
            throw new ArgumentException(
                "Transfer content generation requires a transfer session request.",
                nameof(contentRequest));
        }

        if (contentRequest.FreshnessPolicy != PromptFreshnessPolicy.FreshEquivalentRequired)
        {
            throw new ArgumentException(
                "Transfer content requires fresh equivalent generated material.",
                nameof(contentRequest));
        }

        if (string.IsNullOrWhiteSpace(transferDistance))
        {
            throw new ArgumentException(
                "Transfer content must state the transfer distance.",
                nameof(transferDistance));
        }

        ContentRequest = contentRequest;
        TrainedCapacity = trainedCapacity;
        TransferDistance = transferDistance;
    }

    public GeneratedDrillContentRequest ContentRequest { get; }

    public BranchCode SourceBranch => ContentRequest.Branch;

    public GlobalLevelId SourceLevel => ContentRequest.Level;

    public CapacityId TrainedCapacity { get; }

    public string TransferDistance { get; }
}

public sealed class TransferContentCandidate
{
    public TransferContentCandidate(
        BranchCode sourceBranch,
        GlobalLevelId sourceLevel,
        string transferTask,
        CapacityId? trainedCapacity,
        string sameDemand,
        string changedContext,
        TransferSourceStandardEvidence? sourceStandardEvidence,
        TransferRetestPlan? retestPlan,
        string transferDistance)
    {
        GeneratedContentValidation.EnsureDefined(sourceBranch, nameof(sourceBranch));
        GeneratedContentValidation.EnsureDefined(sourceLevel, nameof(sourceLevel));

        if (trainedCapacity is not null)
        {
            GeneratedContentValidation.EnsureDefined(trainedCapacity.Value, nameof(trainedCapacity));
        }

        SourceBranch = sourceBranch;
        SourceLevel = sourceLevel;
        TransferTask = transferTask ?? string.Empty;
        TrainedCapacity = trainedCapacity;
        SameDemand = sameDemand ?? string.Empty;
        ChangedContext = changedContext ?? string.Empty;
        SourceStandardEvidence = sourceStandardEvidence;
        RetestPlan = retestPlan;
        TransferDistance = transferDistance ?? string.Empty;
    }

    public BranchCode SourceBranch { get; }

    public GlobalLevelId SourceLevel { get; }

    public string TransferTask { get; }

    public CapacityId? TrainedCapacity { get; }

    public string SameDemand { get; }

    public string ChangedContext { get; }

    public TransferSourceStandardEvidence? SourceStandardEvidence { get; }

    public TransferRetestPlan? RetestPlan { get; }

    public string TransferDistance { get; }

    public TransferEligibilityRequest ToCoreRequest()
    {
        return new TransferEligibilityRequest(
            SourceBranch,
            SourceLevel,
            TransferTask,
            TrainedCapacity,
            SameDemand,
            ChangedContext,
            SourceStandardEvidence,
            RetestPlan);
    }
}

public enum TransferContentRuleFailureKind
{
    MissingTransferDistance,
    NoveltyOnlyTransferContent,
    MissingSourceStandardVisibility,
    CoreTransferEligibilityFailure,
}

public sealed record TransferContentRuleFailure(
    TransferContentRuleFailureKind Kind,
    string Detail);

public sealed class TransferContentRuleValidationResult
{
    public TransferContentRuleValidationResult(
        TransferEligibilityResult coreEligibility,
        IEnumerable<TransferContentRuleFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(coreEligibility);
        ArgumentNullException.ThrowIfNull(failures);

        CoreEligibility = coreEligibility;
        Failures = Array.AsReadOnly(failures.ToArray());
    }

    public TransferEligibilityResult CoreEligibility { get; }

    public IReadOnlyList<TransferContentRuleFailure> Failures { get; }

    public bool IsValid => CoreEligibility.IsEligible && Failures.Count == 0;

    public bool GrantsAdvancement => false;
}

public static class TransferContentRuleValidator
{
    public static TransferContentRuleValidationResult Validate(TransferContentCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var coreEligibility = TransferEligibilityEvaluator.Evaluate(candidate.ToCoreRequest());
        var failures = new List<TransferContentRuleFailure>();

        if (string.IsNullOrWhiteSpace(candidate.TransferDistance))
        {
            failures.Add(new TransferContentRuleFailure(
                TransferContentRuleFailureKind.MissingTransferDistance,
                "Transfer content must state transfer distance; changed format alone is not enough."));
        }

        if (coreEligibility.Failures.Any(failure =>
                failure.Kind is
                    TransferEligibilityFailureKind.TrainedCapacityNotSpecified or
                    TransferEligibilityFailureKind.SourceDemandNotPreserved))
        {
            failures.Add(new TransferContentRuleFailure(
                TransferContentRuleFailureKind.NoveltyOnlyTransferContent,
                "Novelty alone is not transfer; generated content must preserve a specified source-branch demand."));
        }

        if (coreEligibility.Failures.Any(failure =>
                failure.Kind is
                    TransferEligibilityFailureKind.SourceStandardEvidenceMissing or
                    TransferEligibilityFailureKind.SourceStandardDoesNotMatchCatalog or
                    TransferEligibilityFailureKind.SourceStandardNotVisible))
        {
            failures.Add(new TransferContentRuleFailure(
                TransferContentRuleFailureKind.MissingSourceStandardVisibility,
                "Transfer content must keep the source branch standard visible in the transfer artifact."));
        }

        foreach (var failure in coreEligibility.Failures)
        {
            failures.Add(new TransferContentRuleFailure(
                TransferContentRuleFailureKind.CoreTransferEligibilityFailure,
                failure.Detail));
        }

        return new TransferContentRuleValidationResult(coreEligibility, failures);
    }
}

public sealed class TransferGeneratedContent
{
    internal TransferGeneratedContent(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials,
        TransferContentCandidate transferCandidate,
        TransferContentRuleValidationResult transferValidation)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(transferCandidate);
        ArgumentNullException.ThrowIfNull(transferValidation);

        Result = result;
        Materials = Array.AsReadOnly(materials.ToArray());
        TransferCandidate = transferCandidate;
        TransferValidation = transferValidation;
    }

    public GeneratedDrillContentResult Result { get; }

    public IReadOnlyList<GeneratedContentMaterial> Materials { get; }

    public TransferContentCandidate TransferCandidate { get; }

    public TransferContentRuleValidationResult TransferValidation { get; }

    public TransferEligibilityRequest TransferEligibilityRequest => TransferCandidate.ToCoreRequest();

    public bool CanBeConsumedByRuntime => TransferValidation.IsValid;

    public bool CanBeRecordedByPersistence => Result.CanBeRecordedByPersistence;

    public bool GrantsAdvancement => false;
}

public static class TransferGeneratedContentGenerator
{
    public static TransferGeneratedContent Generate(
        TransferContentGenerationRequest request,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(seed);

        var seedPlan = GeneratedContentSeedDeriver.Derive(request.ContentRequest, seed);
        var transferDefinition = TransferTestCatalog.TransferTests.Single(test =>
            test.SourceBranch == request.SourceBranch);
        var sourceStandard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == request.SourceBranch &&
            standard.Level == request.SourceLevel);
        var sourceStandardEvidence = new TransferSourceStandardEvidence(
            request.SourceBranch,
            request.SourceLevel,
            sourceStandard.Standard,
            visibleInTransferArtifact: true);
        var candidate = new TransferContentCandidate(
            request.SourceBranch,
            request.SourceLevel,
            transferDefinition.TransferTask,
            request.TrainedCapacity,
            transferDefinition.SameDemand,
            transferDefinition.ChangedContext,
            sourceStandardEvidence,
            transferDefinition.RetestRequirement,
            request.TransferDistance);
        var validation = TransferContentRuleValidator.Validate(candidate);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "Transfer generated content must satisfy core transfer eligibility before it can be emitted.");
        }

        var executableSource = GenerateExecutableSource(request, seedPlan);
        var materials = Array.AsReadOnly(BuildMaterials(request, transferDefinition, sourceStandard)
            .Concat(executableSource.Materials.Where(material => material.Kind is not
                GeneratedContentMaterialKind.LoadVariable and not
                GeneratedContentMaterialKind.HonestyConstraint and not
                GeneratedContentMaterialKind.SourceBranchStandard))
            .ToArray());
        var result = new GeneratedDrillContentResult(
            request.ContentRequest,
            seedPlan.Instance,
            seedPlan.PayloadFacts
                .Concat(BuildPayloadFacts(candidate, materials))
                .Concat(executableSource.Result.PayloadFacts.Select(fact =>
                    new GeneratedContentPayloadFact($"transfer-source-{fact.Name}", fact.Value))));

        return new TransferGeneratedContent(result, materials, candidate, validation);
    }

    private static ExecutableTransferSource GenerateExecutableSource(
        TransferContentGenerationRequest request,
        GeneratedContentSeedPlan seedPlan)
    {
        var sourceLoads = request.ContentRequest.LoadVariables.Where(loadVariable =>
                !string.Equals(loadVariable.Name, "transfer distance", StringComparison.OrdinalIgnoreCase) ||
                request.SourceBranch is BranchCode.CO or BranchCode.TI)
            .ToArray();
        if (sourceLoads.Length == 0)
        {
            sourceLoads = TrainingLoadProfileCatalog.Profiles
                .Where(profile =>
                    profile.Branch == request.SourceBranch &&
                    profile.Drill == request.ContentRequest.Drill)
                .OrderBy(profile => profile.Level)
                .First()
                .Stages[0]
                .LoadVariables
                .ToArray();
        }

        var sourceRequest = new GeneratedDrillContentRequest(
            request.SourceBranch,
            request.SourceLevel,
            request.ContentRequest.Drill,
            SessionType.Test,
            ContentKindFor(request.ContentRequest.Drill),
            $"{request.ContentRequest.EquivalenceClass}-executable-transfer-source",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            sourceLoads,
            request.ContentRequest.CriticalConstraints);
        var sourceSeed = new GeneratedContentSeed(
            $"{seedPlan.PayloadSeed}|executable-transfer-source|{request.TransferDistance}");

        return request.SourceBranch switch
        {
            BranchCode.FH => From(FocusHoldGeneratedContentGenerator.Generate(sourceRequest, sourceSeed)),
            BranchCode.FS => From(FocusShiftGeneratedContentGenerator.Generate(sourceRequest, sourceSeed)),
            BranchCode.WM => From(WorkingMemoryGeneratedContentGenerator.Generate(sourceRequest, sourceSeed)),
            BranchCode.IR => From(InhibitionGeneratedContentGenerator.Generate(sourceRequest, sourceSeed)),
            BranchCode.DE => From(DiscriminationGeneratedContentGenerator.Generate(sourceRequest, sourceSeed)),
            BranchCode.CO => From(ConceptOperationsGeneratedContentGenerator.Generate(sourceRequest, sourceSeed)),
            BranchCode.AI => From(AffectiveInterferenceGeneratedContentGenerator.Generate(sourceRequest, sourceSeed)),
            BranchCode.TI => From(TransferIntegrationGeneratedContentGenerator.Generate(sourceRequest, sourceSeed)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.SourceBranch,
                "Unsupported transfer source branch."),
        };
    }

    private static PromptContentKind ContentKindFor(DrillId drill)
    {
        return drill switch
        {
            DrillId.FH1TargetHold or DrillId.AI1PressureRepeat or
                DrillId.AI2DisruptionRecovery or DrillId.TI1CompositeTask or
                DrillId.TI2GlobalReviewTask => PromptContentKind.EquivalentPrompt,
            DrillId.FH2DistractorHold or DrillId.FS1CueSwitch or
                DrillId.FS2InvalidCueFilter or DrillId.IR1GoNoGoRule or
                DrillId.IR2ExceptionRule => PromptContentKind.CueSequence,
            DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform =>
                PromptContentKind.DelayedReconstructionTask,
            DrillId.DE1PairDiscrimination or DrillId.DE2SeededAudit =>
                PromptContentKind.DiscriminationItemSet,
            DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping =>
                PromptContentKind.RuleExampleSet,
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unknown drill."),
        };
    }

    private static ExecutableTransferSource From(FocusHoldGeneratedContent content) =>
        new(content.Result, content.Materials);

    private static ExecutableTransferSource From(FocusShiftGeneratedContent content) =>
        new(content.Result, content.Materials);

    private static ExecutableTransferSource From(WorkingMemoryGeneratedContent content) =>
        new(content.Result, content.Materials);

    private static ExecutableTransferSource From(InhibitionGeneratedContent content) =>
        new(content.Result, content.Materials);

    private static ExecutableTransferSource From(DiscriminationGeneratedContent content) =>
        new(content.Result, content.Materials);

    private static ExecutableTransferSource From(ConceptOperationsGeneratedContent content) =>
        new(content.Result, content.Materials);

    private static ExecutableTransferSource From(AffectiveInterferenceGeneratedContent content) =>
        new(content.Result, content.Materials);

    private static ExecutableTransferSource From(TransferIntegrationGeneratedContent content) =>
        new(content.Result, content.Materials);

    private static IReadOnlyList<GeneratedContentMaterial> BuildMaterials(
        TransferContentGenerationRequest request,
        TransferTestDefinition transferDefinition,
        BranchLevelStandard sourceStandard)
    {
        var materials = new List<GeneratedContentMaterial>();

        foreach (var loadVariable in request.ContentRequest.LoadVariables)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.LoadVariable,
                loadVariable.Name,
                loadVariable.Value));
        }

        var constraintIndex = 1;
        foreach (var constraint in request.ContentRequest.CriticalConstraints)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "transfer-constraint-" + constraintIndex.ToString(CultureInfo.InvariantCulture),
                constraint.Description));
            constraintIndex++;
        }

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.SourceBranchStandard,
            "source-branch-standard",
            BuildSourceStandardMaterial(request, sourceStandard)));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.SourceTask,
            "source-task",
            $"source task branch {request.SourceBranch}; level {request.SourceLevel}; drill {request.ContentRequest.Drill}; source demand remains the branch standard and must be scored before transfer interpretation."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TransferTask,
            "transfer-task",
            transferDefinition.TransferTask));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.SameDemand,
            "same-demand",
            transferDefinition.SameDemand));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.ChangedContext,
            "changed-context",
            transferDefinition.ChangedContext));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TransferDistance,
            "transfer-distance",
            request.TransferDistance));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RetestRequirement,
            "retest-requirement",
            BuildRetestRequirementMaterial(transferDefinition.RetestRequirement)));

        if (request.SourceLevel == GlobalLevelId.L4 && request.SourceBranch is BranchCode.FH or BranchCode.FS)
        {
            var partner = request.SourceBranch == BranchCode.FH ? BranchCode.WM : BranchCode.IR;
            ObjectiveComponentTaskCatalog.AddMaterials(
                materials,
                [request.SourceBranch, partner],
                request.ContentRequest.EquivalenceClass,
                (int)request.SourceLevel,
                "transfer-integration");
        }

        return Array.AsReadOnly(materials.ToArray());
    }

    private static string BuildSourceStandardMaterial(
        TransferContentGenerationRequest request,
        BranchLevelStandard sourceStandard)
    {
        return $"source branch standard: branch {request.SourceBranch}; level {request.SourceLevel}; demand {sourceStandard.Demand}; standard {sourceStandard.Standard}; visible in the transfer artifact; generated transfer content cannot lower or hide this standard.";
    }

    private static string BuildRetestRequirementMaterial(TransferRetestPlan retestPlan)
    {
        var contextCount = retestPlan.RequiredTransferContexts.ToString(CultureInfo.InvariantCulture);
        var freshness = retestPlan.UsesFreshEquivalentContexts
            ? "fresh equivalent transfer contexts"
            : "reusable transfer contexts";

        return $"requires {contextCount} {freshness}; retesting confirms transfer rather than one-off novelty.";
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildPayloadFacts(
        TransferContentCandidate candidate,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        yield return new GeneratedContentPayloadFact("payload-family", "transfer-content");
        yield return new GeneratedContentPayloadFact("source-branch", candidate.SourceBranch.ToString());
        yield return new GeneratedContentPayloadFact("source-level", candidate.SourceLevel.ToString());
        yield return new GeneratedContentPayloadFact("trained-capacity", candidate.TrainedCapacity?.ToString() ?? "unspecified");
        yield return new GeneratedContentPayloadFact("transfer-task", candidate.TransferTask);
        yield return new GeneratedContentPayloadFact("same-demand", candidate.SameDemand);
        yield return new GeneratedContentPayloadFact("changed-context", candidate.ChangedContext);
        yield return new GeneratedContentPayloadFact("transfer-distance", candidate.TransferDistance);
        yield return new GeneratedContentPayloadFact(
            "source-standard-visibility",
            materials.Single(material => material.Kind == GeneratedContentMaterialKind.SourceBranchStandard).Value);
        yield return new GeneratedContentPayloadFact(
            "retest-requirement",
            materials.Single(material => material.Kind == GeneratedContentMaterialKind.RetestRequirement).Value);
        yield return new GeneratedContentPayloadFact(
            "novelty-policy",
            "novelty alone is not transfer; the generated content preserves source demand, changes context, and exposes the source standard.");
    }

    private sealed record ExecutableTransferSource(
        GeneratedDrillContentResult Result,
        IReadOnlyList<GeneratedContentMaterial> Materials);
}
