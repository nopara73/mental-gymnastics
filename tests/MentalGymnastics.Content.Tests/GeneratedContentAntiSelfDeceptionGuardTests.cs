using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentAntiSelfDeceptionGuardTests
{
    [Fact]
    public void GuardRejectsTooEasyVariantThatDoesNotRepresentRequestedLoad()
    {
        var request = CreateWmRequest();
        var result = CreateResult(request);

        var guard = GeneratedContentAntiSelfDeceptionGuard.Evaluate(
            CreateGuardRequest(
                result,
                ValidWmMaterials().Where(material =>
                    material.Kind != GeneratedContentMaterialKind.EncodeItem ||
                    material.Name is "item-1" or "item-2" or "item-3")));

        Assert.False(guard.IsValid);
        Assert.False(guard.CanBeConsumedByRuntime);
        Assert.False(guard.CanBeRecordedByPersistence);
        Assert.False(guard.GrantsAdvancement);
        Assert.Contains(guard.Findings, finding =>
            finding.Kind == GeneratedContentEscapeRouteKind.TooEasyVariant);
    }

    [Fact]
    public void GuardRejectsHiddenRereadingAndRemovedProtocolConstraint()
    {
        var request = CreateWmRequest(
            criticalConstraints: [new CriticalConstraint(NoInventedItemsConstraint)]);
        var result = CreateResult(request);
        var materials = ValidWmMaterials()
            .Where(material =>
                material.Kind != GeneratedContentMaterialKind.HonestyConstraint ||
                material.Value == NoInventedItemsConstraint)
            .Select(material => material.Kind == GeneratedContentMaterialKind.EncodeInstruction
                ? new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.EncodeInstruction,
                    "encode-instruction",
                    "Rereading is allowed during reconstruction if uncertain.")
                : material)
            .ToArray();

        var guard = GeneratedContentAntiSelfDeceptionGuard.Evaluate(
            CreateGuardRequest(result, materials));

        Assert.False(guard.IsValid);
        Assert.Contains(guard.Findings, finding =>
            finding.Kind == GeneratedContentEscapeRouteKind.HiddenRereading);
        Assert.Contains(guard.Findings, finding =>
            finding.Kind == GeneratedContentEscapeRouteKind.RemovedConstraint &&
            finding.Detail.Contains("No rereading", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GuardRejectsUntrackedGuessesEvenWhenGuessMaterialExists()
    {
        var request = CreateDeRequest();
        var result = CreateResult(request);
        var materials = ValidDeMaterials()
            .Select(material => material.Kind == GeneratedContentMaterialKind.GuessHandling
                ? new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.GuessHandling,
                    "guess-handling",
                    "guessing optional and untracked")
                : material)
            .ToArray();

        var guard = GeneratedContentAntiSelfDeceptionGuard.Evaluate(
            CreateGuardRequest(result, materials));

        Assert.False(guard.IsValid);
        Assert.Contains(guard.Findings, finding =>
            finding.Kind == GeneratedContentEscapeRouteKind.UntrackedGuesses);
    }

    [Fact]
    public void GuardRejectsMissingBranchSpecificEvidenceInCompositeContent()
    {
        var request = CreateTiRequest();
        var result = CreateResult(request);

        var guard = GeneratedContentAntiSelfDeceptionGuard.Evaluate(
            CreateGuardRequest(result, CompositeMaterialsMissingEvidenceForWeakBranch()));

        Assert.False(guard.IsValid);
        Assert.Contains(guard.Findings, finding =>
            finding.Kind == GeneratedContentEscapeRouteKind.MissingEvidence &&
            finding.Detail.Contains("component", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GuardRejectsNoveltyOnlyTransferContent()
    {
        var transferValidation = TransferContentRuleValidator.Validate(
            new TransferContentCandidate(
                BranchCode.WM,
                GlobalLevelId.L4,
                "Novel visual puzzle with a fresh format.",
                trainedCapacity: null,
                sameDemand: "Novel visual puzzle format.",
                changedContext: "Domain or representation.",
                sourceStandardEvidence: CreateSourceStandardEvidence(visible: true),
                retestPlan: new TransferRetestPlan(
                    requiredTransferContexts: 2,
                    usesFreshEquivalentContexts: true),
                transferDistance: "far transfer to unfamiliar visual structure"));

        var guard = GeneratedContentAntiSelfDeceptionGuard.EvaluateTransfer(transferValidation);

        Assert.False(guard.IsValid);
        Assert.False(guard.GrantsAdvancement);
        Assert.Contains(guard.Findings, finding =>
            finding.Kind == GeneratedContentEscapeRouteKind.NoveltyOnlyTransfer);
    }

    [Fact]
    public void GuardRejectsUnsupportedPressureThatPreventsCleanEvidence()
    {
        var request = CreateAiRequest();
        var result = CreateResult(request);

        var guard = GeneratedContentAntiSelfDeceptionGuard.Evaluate(
            CreateGuardRequest(result, ValidAiMaterials()));

        Assert.False(guard.IsValid);
        Assert.Contains(guard.Findings, finding =>
            finding.Kind == GeneratedContentEscapeRouteKind.UnsupportedPressureChange);
    }

    private const string NoRereadConstraint = "No rereading after encode window.";
    private const string NoInventedItemsConstraint = "No invented items.";
    private const string MarkedGuessConstraint = "Guessing must be marked.";
    private const string SeparateEvidenceConstraint = "Each branch must leave separate evidence.";
    private const string OriginalStandardCannotBeLoweredConstraint = "Original standard cannot be lowered.";
    private const string UnsupportedPressure = "emotional pressure that prevents clean evidence collection";

    private static GeneratedContentAntiSelfDeceptionGuardRequest CreateGuardRequest(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials)
    {
        var requirement = new GeneratedContentEquivalenceRequirement(
            result.Request,
            VisibleStandardFor(result.Request.Branch, result.Request.Level),
            LoadIntentFor(result.Request));
        var candidate = new GeneratedContentEquivalenceCandidate(
            result.Instance,
            requirement.VisibleStandard,
            requirement.LoadIntent);

        return new GeneratedContentAntiSelfDeceptionGuardRequest(
            result,
            materials,
            requirement,
            candidate,
            LoadChangeMode.Acquisition);
    }

    private static GeneratedDrillContentRequest CreateWmRequest(
        IEnumerable<CriticalConstraint>? criticalConstraints = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            SessionType.Practice,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("delay", "60 seconds"),
            ],
            criticalConstraints ??
            [
                new CriticalConstraint(NoRereadConstraint),
                new CriticalConstraint(NoInventedItemsConstraint),
            ]);
    }

    private static GeneratedDrillContentRequest CreateDeRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.DE,
            GlobalLevelId.L1,
            DrillId.DE1PairDiscrimination,
            SessionType.Practice,
            PromptContentKind.DiscriminationItemSet,
            "de-l1-pair-discrimination",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("quantity", "2"),
                new LoadVariable("similarity", "simple"),
            ],
            [new CriticalConstraint(MarkedGuessConstraint)]);
    }

    private static GeneratedDrillContentRequest CreateTiRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.TI,
            GlobalLevelId.L1,
            DrillId.TI1CompositeTask,
            SessionType.Practice,
            PromptContentKind.EquivalentPrompt,
            "ti-l1-composite-fs-wm",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("number of branches", "2"),
                new LoadVariable("task length", "12 minutes"),
                new LoadVariable("transfer distance", "near transfer"),
            ],
            [new CriticalConstraint(SeparateEvidenceConstraint)]);
    }

    private static GeneratedDrillContentRequest CreateAiRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.AI,
            GlobalLevelId.L1,
            DrillId.AI1PressureRepeat,
            SessionType.Practice,
            PromptContentKind.EquivalentPrompt,
            "ai-l1-pressure-repeat-fh-l3",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("time pressure", UnsupportedPressure)],
            [new CriticalConstraint(OriginalStandardCannotBeLoweredConstraint)]);
    }

    private static GeneratedDrillContentResult CreateResult(GeneratedDrillContentRequest request)
    {
        return new GeneratedDrillContentResult(
            request,
            new GeneratedDrillInstanceDescriptor(
                "generated-" + request.EquivalenceClass,
                new PromptContentIdentity(
                    "content-" + request.EquivalenceClass,
                    request.Branch,
                    request.Level,
                    request.Drill,
                    request.ContentKind,
                    request.EquivalenceClass),
                "content-v1",
                request.FreshnessPolicy,
                request.LoadVariables,
                request.CriticalConstraints),
            [new GeneratedContentPayloadFact("fixture-id", request.EquivalenceClass)]);
    }

    private static IReadOnlyList<GeneratedContentMaterial> ValidWmMaterials()
    {
        return
        [
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "item count", "5"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "detail density", "simple objects"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "delay", "60 seconds"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.HonestyConstraint, "no-reread", NoRereadConstraint),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.HonestyConstraint, "no-invention", NoInventedItemsConstraint),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeInstruction, "encode-instruction", "Study once; no rereading after the encode window closes."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DetailDensity, "detail-density", "simple objects"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DelayLength, "delay", "60 seconds"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.ReconstructionInstruction, "reconstruct", "Reconstruct the five items in order without rereading; do not invent items."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.ExpectedReconstruction, "expected", "alpha|bravo|cedar|delta|ember"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-1", "alpha"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-2", "bravo"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-3", "cedar"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-4", "delta"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-5", "ember"),
        ];
    }

    private static IReadOnlyList<GeneratedContentMaterial> ValidDeMaterials()
    {
        var differentShape = new VisualStimulusPairSpec(
            new VisualStimulusSpec(VisualStimulusShape.Circle, VisualStimulusColor.Blue),
            new VisualStimulusSpec(VisualStimulusShape.Square, VisualStimulusColor.Blue),
            VisualStimulusFeature.Shape);
        var sameMarkCount = new VisualStimulusPairSpec(
            new VisualStimulusSpec(
                VisualStimulusShape.Square,
                VisualStimulusColor.Green,
                Mark: VisualStimulusMark.Dot,
                MarkCount: 2),
            new VisualStimulusSpec(
                VisualStimulusShape.Square,
                VisualStimulusColor.Amber,
                Mark: VisualStimulusMark.Dot,
                MarkCount: 2),
            VisualStimulusFeature.MarkCount);

        return
        [
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "quantity", "2"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "similarity", "simple"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.HonestyConstraint, "marked-guesses", MarkedGuessConstraint),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DiscriminationPair, "pair-1", VisualStimulusCodec.EncodePair(differentShape)),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DiscriminationPair, "pair-2", VisualStimulusCodec.EncodePair(sameMarkCount)),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.RelevantFeature, "feature", "shape edge"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.Similarity, "similarity", "simple"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.MatchTruth, "pair-1-truth", "pair-1: mismatch; expected answer based only on relevant feature"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.MatchTruth, "pair-2-truth", "pair-2: match; expected answer based only on relevant feature"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.GuessHandling, "guess-handling", "mark every guess before answering"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.FalsePositiveFalseNegativeKey, "fp-fn", "false positives and false negatives tracked separately"),
        ];
    }

    private static IReadOnlyList<GeneratedContentMaterial> CompositeMaterialsMissingEvidenceForWeakBranch()
    {
        return
        [
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "number of branches", "2"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "task length", "12 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "transfer distance", "near transfer"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.HonestyConstraint, "separate-evidence", SeparateEvidenceConstraint),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.ComponentPayload, "component-fs", "component branch FS cue switching"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.ComponentPayload, "component-wm", "component branch WM delayed reconstruction"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.ComponentEvidenceRequirement, "evidence-fs", "component branch FS evidence remains separate"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.BranchScoringKey, "scoring-fs", "component branch FS scoring remains visible"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.BranchScoringKey, "scoring-wm", "component branch WM scoring remains visible"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.CompositeTaskPrompt, "prompt", "complete the composite task with visible component boundaries"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TaskLength, "task length", "12 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TransferDistance, "transfer distance", "near transfer"),
        ];
    }

    private static IReadOnlyList<GeneratedContentMaterial> ValidAiMaterials()
    {
        return
        [
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "time pressure", UnsupportedPressure),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.HonestyConstraint, "no-lowering", OriginalStandardCannotBeLoweredConstraint),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.SourceBranchStandard, "source-standard", "source branch standard remains visible and unchanged"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.PressureSource, "pressure-source", UnsupportedPressure),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TimePressure, "time pressure", UnsupportedPressure),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.NoStandardLoweringMarker, "no-standard-lowering", "original standard cannot be lowered"),
        ];
    }

    private static TransferSourceStandardEvidence CreateSourceStandardEvidence(bool visible)
    {
        return new TransferSourceStandardEvidence(
            BranchCode.WM,
            GlobalLevelId.L4,
            VisibleStandardFor(BranchCode.WM, GlobalLevelId.L4),
            visible);
    }

    private static string VisibleStandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards
            .Single(standard => standard.Branch == branch && standard.Level == level)
            .Standard;
    }

    private static string LoadIntentFor(GeneratedDrillContentRequest request)
    {
        return string.Join(
            "; ",
            request.LoadVariables.Select(variable => variable.Name + ": " + variable.Value));
    }
}
