using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class PromptContentAbstractionTests
{
    [Fact]
    public void SelectsEquivalentPromptsDeterministicallyFromFixtureSource()
    {
        var source = new FixturePromptContentSource(
        [
            TextPrompt("wm-l1-b", "Encode: river, bell, stone, amber, north."),
            TextPrompt("wm-l1-a", "Encode: red, blue, green, gold, white."),
        ]);
        var request = Request(PromptContentKind.EquivalentPrompt);

        var first = DeterministicPromptContentSelector.Select(source, request);
        var second = DeterministicPromptContentSelector.Select(source, request);

        Assert.True(first.HasSelection);
        Assert.True(second.HasSelection);
        Assert.Equal("wm-l1-a", first.SelectedContent?.Identity.ContentId);
        Assert.Equal(first.SelectedContent?.Identity.ContentId, second.SelectedContent?.Identity.ContentId);
    }

    [Fact]
    public void FreshEquivalentRetestSkipsPreviouslyUsedContentAndPreservesDemand()
    {
        var source = new FixturePromptContentSource(
        [
            DelayedReconstruction("wm-l1-a", ["red", "blue", "green", "gold", "white"]),
            DelayedReconstruction("wm-l1-b", ["river", "bell", "stone", "amber", "north"]),
        ]);
        var request = Request(
            PromptContentKind.DelayedReconstructionTask,
            freshnessPolicy: PromptFreshnessPolicy.FreshEquivalentRequired,
            previouslyUsedContentIds: ["wm-l1-a"]);

        var result = DeterministicPromptContentSelector.Select(source, request);

        Assert.True(result.HasSelection);
        Assert.Equal("wm-l1-b", result.SelectedContent?.Identity.ContentId);
        var task = Assert.IsAssignableFrom<IDelayedReconstructionContent>(result.SelectedContent);
        Assert.Equal("WM-L1-five-items-sixty-second-delay", task.Identity.EquivalenceClass);
        Assert.Equal(5, task.Items.Count);
        Assert.Equal(60, task.DelaySeconds);
        Assert.Contains(
            task.CriticalConstraints,
            constraint => constraint.Description == "no rereading after encode window");
    }

    [Fact]
    public void KnownCueSequenceCanBeReusedWhenRetestPolicyAllowsIdenticalPrompts()
    {
        var source = new FixturePromptContentSource(
        [
            new CueSequenceContent(
                Identity(
                    "fs-known-cues-a",
                    BranchCode.FS,
                    DrillId.FS1CueSwitch,
                    PromptContentKind.CueSequence,
                    "FS-L1-known-two-target-cue-sequence"),
                PromptFreshnessPolicy.IdenticalReuseAllowed,
                [new LoadVariable("cue density", "low")],
                [new CriticalConstraint("switch only on valid cue")],
                [
                    new CueStep(1, "left", isValidCue: true),
                    new CueStep(2, "right", isValidCue: true),
                ]),
        ]);
        var request = new PromptContentSelectionRequest(
            BranchCode.FS,
            GlobalLevelId.L1,
            DrillId.FS1CueSwitch,
            PromptContentKind.CueSequence,
            "FS-L1-known-two-target-cue-sequence",
            PromptFreshnessPolicy.IdenticalReuseAllowed,
            previouslyUsedContentIds: ["fs-known-cues-a"]);

        var result = DeterministicPromptContentSelector.Select(source, request);

        Assert.True(result.HasSelection);
        Assert.Equal("fs-known-cues-a", result.SelectedContent?.Identity.ContentId);
        var cueSequence = Assert.IsAssignableFrom<ICueSequenceContent>(result.SelectedContent);
        Assert.Equal(["left", "right"], cueSequence.Cues.Select(cue => cue.Cue));
    }

    [Fact]
    public void FreshEquivalentRetestFailsWhenOnlyUsedContentIsAvailable()
    {
        var source = new FixturePromptContentSource(
        [
            DelayedReconstruction("wm-l1-a", ["red", "blue", "green", "gold", "white"]),
        ]);
        var request = Request(
            PromptContentKind.DelayedReconstructionTask,
            freshnessPolicy: PromptFreshnessPolicy.FreshEquivalentRequired,
            previouslyUsedContentIds: ["wm-l1-a"]);

        var result = DeterministicPromptContentSelector.Select(source, request);

        Assert.False(result.HasSelection);
        Assert.Null(result.SelectedContent);
        Assert.Contains(
            result.Failures,
            failure => failure.Kind == PromptContentSelectionFailureKind.FreshEquivalentContentUnavailable);
    }

    [Fact]
    public void RepresentsDiscriminationItemsAndRuleExamplesWithoutGeneratingContent()
    {
        var discrimination = new DiscriminationItemSetContent(
            Identity(
                "de-l1-a",
                BranchCode.DE,
                DrillId.DE1PairDiscrimination,
                PromptContentKind.DiscriminationItemSet,
                "DE-L1-simple-pairs"),
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("similarity", "simple")],
            [new CriticalConstraint("guessing must be marked")],
            [
                new DiscriminationItemPair("circle", "circle", "same shape", isMatch: true),
                new DiscriminationItemPair("circle", "oval", "shape differs", isMatch: false),
            ]);
        var ruleExamples = new RuleExampleSetContent(
            Identity(
                "co-l1-a",
                BranchCode.CO,
                DrillId.CO1RuleExtraction,
                PromptContentKind.RuleExampleSet,
                "CO-L1-clear-rule-examples"),
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("example count", "4 examples plus 2 unseen")],
            [new CriticalConstraint("rule stated before unseen examples")],
            [
                new RuleExample("2, 4, 6", "even", isPositiveExample: true, isUnseenTestExample: false),
                new RuleExample("1, 3, 5", "odd", isPositiveExample: false, isUnseenTestExample: false),
                new RuleExample("8, 10, 12", "even", isPositiveExample: true, isUnseenTestExample: true),
            ]);

        Assert.Equal(PromptContentKind.DiscriminationItemSet, discrimination.Identity.Kind);
        Assert.Equal(2, discrimination.Items.Count);
        Assert.Contains(discrimination.CriticalConstraints, constraint => constraint.Description == "guessing must be marked");

        Assert.Equal(PromptContentKind.RuleExampleSet, ruleExamples.Identity.Kind);
        Assert.Contains(ruleExamples.Examples, example => example.IsUnseenTestExample);
        Assert.Contains(ruleExamples.CriticalConstraints, constraint => constraint.Description == "rule stated before unseen examples");
    }

    private static PromptContentSelectionRequest Request(
        PromptContentKind kind,
        PromptFreshnessPolicy freshnessPolicy = PromptFreshnessPolicy.IdenticalReuseAllowed,
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new PromptContentSelectionRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            kind,
            "WM-L1-five-items-sixty-second-delay",
            freshnessPolicy,
            previouslyUsedContentIds ?? []);
    }

    private static EquivalentPromptContent TextPrompt(string contentId, string promptText)
    {
        return new EquivalentPromptContent(
            Identity(
                contentId,
                BranchCode.WM,
                DrillId.WM1DelayedReconstruction,
                PromptContentKind.EquivalentPrompt,
                "WM-L1-five-items-sixty-second-delay"),
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("item count", "5")],
            [new CriticalConstraint("no invented items")],
            promptText);
    }

    private static DelayedReconstructionContent DelayedReconstruction(
        string contentId,
        IEnumerable<string> items)
    {
        return new DelayedReconstructionContent(
            Identity(
                contentId,
                BranchCode.WM,
                DrillId.WM1DelayedReconstruction,
                PromptContentKind.DelayedReconstructionTask,
                "WM-L1-five-items-sixty-second-delay"),
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")],
            [
                new CriticalConstraint("no rereading after encode window"),
                new CriticalConstraint("no invented items"),
            ],
            items.Select((item, index) => new ReconstructionItem(index + 1, item)),
            delaySeconds: 60,
            reconstructionInstruction: "Reconstruct the encoded items exactly.");
    }

    private static PromptContentIdentity Identity(
        string contentId,
        BranchCode branch,
        DrillId drill,
        PromptContentKind kind,
        string equivalenceClass)
    {
        return new PromptContentIdentity(
            contentId,
            branch,
            GlobalLevelId.L1,
            drill,
            kind,
            equivalenceClass);
    }

    private sealed class FixturePromptContentSource : IPromptContentSource
    {
        public FixturePromptContentSource(IEnumerable<IDrillPromptContent> content)
        {
            Content = content.ToArray();
        }

        private IReadOnlyList<IDrillPromptContent> Content { get; }

        public IReadOnlyList<IDrillPromptContent> ListContent()
        {
            return Content;
        }
    }
}
