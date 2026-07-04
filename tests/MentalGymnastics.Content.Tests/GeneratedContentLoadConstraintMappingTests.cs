using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentLoadConstraintMappingTests
{
    [Fact]
    public void WmDelayedReconstructionLoadConstraintsMapToConcreteContentRequirements()
    {
        var request = CreateRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("delay", "60 seconds"),
            ],
            [
                new CriticalConstraint("No rereading after encode window."),
                new CriticalConstraint("No invented items."),
            ]);

        var plan = GeneratedContentLoadConstraintMapper.Map(request);

        Assert.True(plan.IsValid);
        Assert.True(plan.CanSelectContent);
        Assert.True(plan.PreservesCoreDemand);
        Assert.True(plan.PreservesHonestyConstraint);
        Assert.False(plan.OwnsProgressionDecision);
        Assert.False(plan.GrantsAdvancement);
        Assert.Empty(plan.Failures);

        var itemCount = Assert.Single(plan.Constraints, constraint =>
            constraint.ProgramLoadVariable == LoadVariableKind.ItemCount);
        Assert.Equal(GeneratedContentLoadConstraintMatchKind.MinimumMaterialCount, itemCount.MatchKind);
        Assert.Equal(5, itemCount.MinimumCount);
        Assert.Equal([GeneratedContentMaterialKind.EncodeItem], itemCount.MaterialKinds);

        var delay = Assert.Single(plan.Constraints, constraint =>
            constraint.ProgramLoadVariable == LoadVariableKind.Delay);
        Assert.Equal(GeneratedContentLoadConstraintMatchKind.ExactMaterialValue, delay.MatchKind);
        Assert.Equal("60 seconds", delay.RequiredValue);
        Assert.Equal([GeneratedContentMaterialKind.DelayLength], delay.MaterialKinds);

        var detailDensity = Assert.Single(plan.Constraints, constraint =>
            constraint.ProgramLoadVariable == LoadVariableKind.DetailDensity);
        Assert.Equal(GeneratedContentLoadConstraintMatchKind.ExactMaterialValue, detailDensity.MatchKind);
        Assert.Equal([GeneratedContentMaterialKind.DetailDensity], detailDensity.MaterialKinds);
        Assert.Equal(2, plan.RequiredHonestyConstraints.Count);
    }

    [Fact]
    public void ValidatedGeneratedContentRejectsLoadMaterialThatDoesNotReflectRequestedDelay()
    {
        var request = CreateRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")],
            [
                new CriticalConstraint("No rereading after encode window."),
                new CriticalConstraint("No invented items."),
            ]);
        var result = CreateResult(request);
        var materials = ValidWmMaterials()
            .Where(material => material.Kind != GeneratedContentMaterialKind.DelayLength)
            .Append(new GeneratedContentMaterial(GeneratedContentMaterialKind.DelayLength, "delay", "30 seconds"))
            .ToArray();

        var validation = GeneratedContentLoadConstraintValidator.Validate(result, materials);

        Assert.False(validation.IsValid);
        Assert.False(validation.CanSelectContent);
        Assert.Contains(validation.Failures, failure =>
            failure.Kind == GeneratedContentLoadConstraintFailureKind.MaterialValueMismatch &&
            failure.LoadVariable == LoadVariableKind.Delay &&
            failure.Detail.Contains("60 seconds", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => ValidatedGeneratedDrillContent.Create(result, materials));
    }

    [Fact]
    public void FhLoadMappingRejectsUndocumentedDifficultyShortcut()
    {
        var request = CreateRequest(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            PromptContentKind.EquivalentPrompt,
            "fh-l1-target-hold",
            [new LoadVariable("duration", "3 minutes"), new LoadVariable("complex reasoning", "logic puzzle")],
            [new CriticalConstraint("Target is stated before set; every drift is marked.")]);

        var plan = GeneratedContentLoadConstraintMapper.Map(request);

        Assert.False(plan.IsValid);
        Assert.False(plan.CanSelectContent);
        Assert.False(plan.PreservesCoreDemand);
        Assert.Contains(plan.Failures, failure =>
            failure.Kind == GeneratedContentLoadConstraintFailureKind.UnknownLoadVariable &&
            failure.Detail.Contains("complex reasoning", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Constraints, constraint =>
            string.Equals(constraint.LoadVariableName, "complex reasoning", StringComparison.Ordinal));
    }

    [Fact]
    public void FhTargetHoldLoadConstraintsMapToHoldMaterialsWithoutRemovingHonestyConstraint()
    {
        var request = CreateRequest(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            PromptContentKind.EquivalentPrompt,
            "fh-l1-target-hold",
            [
                new LoadVariable("duration", "3 minutes"),
                new LoadVariable("target subtlety", "simple phrase"),
                new LoadVariable("recovery window", "10 seconds"),
            ],
            [new CriticalConstraint("Target is stated before set; every drift is marked.")]);
        var result = CreateResult(request);
        var materials = new[]
        {
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "duration", "3 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "target subtlety", "simple phrase"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "recovery window", "10 seconds"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "target-and-drift",
                "Target is stated before set; every drift is marked."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TargetStatement, "target", "Hold this phrase."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TargetType, "target-type", "phrase"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.TargetSubtlety, "target-subtlety", "simple phrase"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.HoldDuration, "duration", "3 minutes"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.RecoveryWindow, "recovery-window", "10 seconds"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DriftMarkingEvidenceShape, "drift-log", "marked drifts"),
        };

        var validation = GeneratedContentLoadConstraintValidator.Validate(result, materials);

        Assert.True(validation.IsValid);
        Assert.True(validation.PreservesCoreDemand);
        Assert.True(validation.PreservesHonestyConstraint);
        Assert.Empty(validation.Failures);
    }

    [Fact]
    public void DeDiscriminationQuantityAndSimilarityMustShapeTheItemSet()
    {
        var request = CreateRequest(
            BranchCode.DE,
            GlobalLevelId.L2,
            DrillId.DE1PairDiscrimination,
            PromptContentKind.DiscriminationItemSet,
            "de-l2-pair-discrimination",
            [new LoadVariable("quantity", "3"), new LoadVariable("similarity", "near match")],
            [new CriticalConstraint("Guessing must be marked.")]);
        var result = CreateResult(request);
        var insufficientMaterials = ValidDeMaterials(pairCount: 2);
        var sufficientMaterials = ValidDeMaterials(pairCount: 3);

        var rejected = GeneratedContentLoadConstraintValidator.Validate(result, insufficientMaterials);
        var accepted = GeneratedContentLoadConstraintValidator.Validate(result, sufficientMaterials);

        Assert.False(rejected.IsValid);
        Assert.Contains(rejected.Failures, failure =>
            failure.Kind == GeneratedContentLoadConstraintFailureKind.InsufficientMaterialCount &&
            failure.LoadVariable == LoadVariableKind.Quantity);
        Assert.True(accepted.IsValid);
        Assert.Empty(accepted.Failures);
    }

    private static GeneratedDrillContentRequest CreateRequest(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        PromptContentKind contentKind,
        string equivalenceClass,
        IEnumerable<LoadVariable> loadVariables,
        IEnumerable<CriticalConstraint> criticalConstraints)
    {
        return new GeneratedDrillContentRequest(
            branch,
            level,
            drill,
            SessionType.Practice,
            contentKind,
            equivalenceClass,
            PromptFreshnessPolicy.FreshEquivalentRequired,
            loadVariables,
            criticalConstraints,
            ["previous-content"]);
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
            new GeneratedContentMaterial(GeneratedContentMaterialKind.LoadVariable, "delay", "60 seconds"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "no-reread",
                "No rereading after encode window."),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "no-invention",
                "No invented items."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeInstruction, "encode-instruction", "Study once."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.DelayLength, "delay", "60 seconds"),
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ReconstructionInstruction,
                "reconstruct",
                "Reconstruct the five items in order."),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.ExpectedReconstruction, "expected", "alpha|bravo|cedar|delta|ember"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-1", "alpha"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-2", "bravo"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-3", "cedar"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-4", "delta"),
            new GeneratedContentMaterial(GeneratedContentMaterialKind.EncodeItem, "item-5", "ember"),
        ];
    }

    private static IReadOnlyList<GeneratedContentMaterial> ValidDeMaterials(int pairCount)
    {
        var materials = new List<GeneratedContentMaterial>
        {
            new(GeneratedContentMaterialKind.LoadVariable, "quantity", "3"),
            new(GeneratedContentMaterialKind.LoadVariable, "similarity", "near match"),
            new(GeneratedContentMaterialKind.HonestyConstraint, "marked-guesses", "Guessing must be marked."),
            new(GeneratedContentMaterialKind.RelevantFeature, "feature", "single relevant difference"),
            new(GeneratedContentMaterialKind.MatchTruth, "truth", "match or mismatch key"),
            new(GeneratedContentMaterialKind.GuessHandling, "guess-handling", "mark every guess"),
            new(GeneratedContentMaterialKind.FalsePositiveFalseNegativeKey, "error-key", "fp/fn scoring key"),
            new(GeneratedContentMaterialKind.Similarity, "similarity", "near match"),
        };

        for (var index = 1; index <= pairCount; index++)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.DiscriminationPair,
                "pair-" + index,
                "near-match-pair-" + index));
        }

        return materials;
    }
}
