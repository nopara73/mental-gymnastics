using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentSelectionCoordinatorTests
{
    [Fact]
    public void SelectsValidatedFoundationalContentFromTrainingNeed()
    {
        var need = GeneratedContentSelectionNeed.ForStandardContent(CreateInvalidCueFilterRequest());

        var selection = GeneratedContentSelectionCoordinator.Select(
            need,
            new GeneratedContentSeed("selection-fs-alpha"));

        Assert.True(selection.IsValid, FailureSummary(selection));
        Assert.True(selection.CanBeConsumedByRuntime);
        Assert.True(selection.CanBeRecordedByPersistence);
        Assert.False(selection.OwnsProgressionDecision);
        Assert.False(selection.DecidesReadiness);
        Assert.False(selection.DecidesOwnership);
        Assert.False(selection.DecidesMaintenance);
        Assert.False(selection.DecidesDecay);
        Assert.False(selection.GrantsAdvancement);
        Assert.False(selection.IsTransfer);
        Assert.NotNull(selection.ValidatedContent);
        Assert.Equal(BranchCode.FS, selection.Result.Branch);
        Assert.Equal(GlobalLevelId.L3, selection.Result.Level);
        Assert.Equal(DrillId.FS2InvalidCueFilter, selection.Result.Drill);
        Assert.True(selection.DifficultyAudit?.PreservesRequestedDemand);
        Assert.True(selection.DifficultyAudit?.PreservesHonestyConstraint);
        Assert.Contains(selection.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.InvalidCue);
        Assert.Contains(selection.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.HonestyConstraint &&
            material.Value == InvalidCueConstraint);
    }

    [Fact]
    public void SelectsValidatedAdvancedContentWithoutMakingProgressionDecision()
    {
        var need = GeneratedContentSelectionNeed.ForStandardContent(CreateRuleExtractionRequest());

        var selection = GeneratedContentSelectionCoordinator.Select(
            need,
            new GeneratedContentSeed("selection-co-alpha"));

        Assert.True(selection.IsValid, FailureSummary(selection));
        Assert.True(selection.CanBeConsumedByRuntime);
        Assert.True(selection.CanBeRecordedByPersistence);
        Assert.False(selection.OwnsProgressionDecision);
        Assert.False(selection.GrantsAdvancement);
        Assert.Equal(BranchCode.CO, selection.Result.Branch);
        Assert.Equal(GlobalLevelId.L1, selection.Result.Level);
        Assert.Equal(DrillId.CO1RuleExtraction, selection.Result.Drill);
        Assert.NotNull(selection.ValidatedContent);
        Assert.NotNull(selection.DifficultyAudit);
        Assert.True(selection.DifficultyAudit.IsValid);
        Assert.Contains(selection.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.UnseenExample);
        Assert.Contains(selection.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.HonestyConstraint &&
            material.Value == RuleBeforeUnseenConstraint);
    }

    [Fact]
    public void SelectsTransferContentThatPreservesSourceStandardAndEquivalence()
    {
        var need = GeneratedContentSelectionNeed.ForTransferContent(CreateTransferRequest());

        var selection = GeneratedContentSelectionCoordinator.Select(
            need,
            new GeneratedContentSeed("selection-transfer-wm-alpha"));

        Assert.True(selection.IsValid, FailureSummary(selection));
        Assert.True(selection.IsTransfer);
        Assert.True(selection.CanBeConsumedByRuntime);
        Assert.True(selection.CanBeRecordedByPersistence);
        Assert.False(selection.GrantsAdvancement);
        Assert.False(selection.OwnsProgressionDecision);
        Assert.Null(selection.ValidatedContent);
        Assert.NotNull(selection.TransferValidation);
        Assert.True(selection.TransferValidation.IsValid);
        Assert.Equal(BranchCode.WM, selection.Result.Branch);
        Assert.Equal(GlobalLevelId.L4, selection.Result.Level);
        Assert.Equal(SessionType.Transfer, selection.Result.SessionType);
        Assert.Equal(CapacityId.EncodingFidelity, selection.TransferEligibilityRequest?.TrainedCapacity);
        Assert.Contains(selection.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.SourceBranchStandard &&
            material.Value.Contains("visible in the transfer artifact", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(selection.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.SameDemand &&
            material.Value == "Encoding, delay, no invention.");
        Assert.Contains(selection.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.ChangedContext &&
            material.Value == "Domain or representation.");
    }

    [Fact]
    public void TransferSessionRequiresTransferNeed()
    {
        var transferShapedRequest = CreateTransferRequest().ContentRequest;

        var exception = Assert.Throws<ArgumentException>(() =>
            GeneratedContentSelectionNeed.ForStandardContent(transferShapedRequest));

        Assert.Contains("Transfer session content selection requires transfer generation requirements.", exception.Message);
    }

    private const string ValidCueConstraint = "Switch only on valid cue.";
    private const string InvalidCueConstraint = "Invalid cues must not trigger switch.";
    private const string NoAnticipatorySwitchingConstraint = "No anticipatory switching.";
    private const string RuleBeforeUnseenConstraint = "Rule stated before unseen examples.";

    private static GeneratedDrillContentRequest CreateInvalidCueFilterRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.FS,
            GlobalLevelId.L3,
            DrillId.FS2InvalidCueFilter,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "fs-l3-invalid-cue-filter",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("target count", "2"),
                new LoadVariable("switch count", "6"),
                new LoadVariable("cue density", "moderate"),
                new LoadVariable("rule contrast", "valid symbol versus invalid lure"),
                new LoadVariable("return precision", "next valid cue"),
            ],
            [
                new CriticalConstraint(ValidCueConstraint),
                new CriticalConstraint(InvalidCueConstraint),
                new CriticalConstraint(NoAnticipatorySwitchingConstraint),
            ]);
    }

    private static GeneratedDrillContentRequest CreateRuleExtractionRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.CO,
            GlobalLevelId.L1,
            DrillId.CO1RuleExtraction,
            SessionType.Practice,
            PromptContentKind.RuleExampleSet,
            "co-l1-rule-extraction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("rule ambiguity", "clear examples"),
                new LoadVariable("example count", "8"),
            ],
            [new CriticalConstraint(RuleBeforeUnseenConstraint)]);
    }

    private static TransferContentGenerationRequest CreateTransferRequest()
    {
        return new TransferContentGenerationRequest(
            new GeneratedDrillContentRequest(
                BranchCode.WM,
                GlobalLevelId.L4,
                DrillId.WM1DelayedReconstruction,
                SessionType.Transfer,
                PromptContentKind.EquivalentPrompt,
                "wm-l4-transfer-unfamiliar-structure",
                PromptFreshnessPolicy.FreshEquivalentRequired,
                [new LoadVariable("transfer distance", "far transfer to unfamiliar visual structure")],
                [
                    new CriticalConstraint("No rereading after encode window."),
                    new CriticalConstraint("No invented items."),
                ]),
            CapacityId.EncodingFidelity,
            "far transfer to unfamiliar visual structure");
    }

    private static string FailureSummary(GeneratedContentSelectionResult selection)
    {
        return string.Join(
            "; ",
            selection.FreshnessValidation.Failures.Select(failure => failure.Detail)
                .Concat(selection.DifficultyAudit?.Failures.Select(failure => failure.Detail) ?? [])
                .Concat(selection.TransferValidation?.Failures.Select(failure => failure.Detail) ?? []));
    }
}
