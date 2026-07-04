using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class LocalContentBankTests
{
    [Fact]
    public void LocalContentBankEntryExposesStableMetadataForEquivalenceLoadDrillAndVersion()
    {
        var entry = CreateWmEntry(
            entryId: "entry-wm-b",
            contentId: "content-wm-b",
            contentVersion: "content-v2");

        Assert.Equal("wm-bank", entry.BankId);
        Assert.Equal("entry-wm-b", entry.EntryId);
        Assert.Equal("content-wm-b", entry.ContentId);
        Assert.Equal("content-v2", entry.ContentVersion);
        Assert.Equal(LocalContentBankSourceKind.PackagedWithApp, entry.SourceKind);
        Assert.Equal(BranchCode.WM, entry.Branch);
        Assert.Equal(GlobalLevelId.L1, entry.Level);
        Assert.Equal(DrillId.WM1DelayedReconstruction, entry.Drill);
        Assert.Equal(PromptContentKind.DelayedReconstructionTask, entry.ContentKind);
        Assert.Equal("wm-l1-delayed-reconstruction", entry.EquivalenceClass);
        Assert.Equal(["item count", "delay"], entry.LoadVariables.Select(variable => variable.Name));
        Assert.Contains(entry.CriticalConstraints, constraint =>
            constraint.Description == "No invented items.");
        Assert.Contains(entry.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.ExpectedReconstruction);
        Assert.True(entry.CanBeUsedOffline);
        Assert.False(entry.RequiresNetworkAccess);
        Assert.False(entry.AllowsAiOrApiDependencies);
        Assert.False(entry.OwnsProgressionDecision);
    }

    [Fact]
    public void DeterministicallySelectsFirstMatchingLocalEntry()
    {
        var request = CreateWmRequest();
        var bank = new LocalContentBank(
            "wm-bank",
            "bank-v1",
            LocalContentBankSourceKind.PackagedWithApp,
            [
                CreateWmEntry("entry-z", "content-wm-z"),
                CreateWmEntry("entry-a", "content-wm-a"),
                CreateWmEntry(
                    "entry-wrong-drill",
                    "content-fh-a",
                    branch: BranchCode.FH,
                    drill: DrillId.FH1TargetHold,
                    contentKind: PromptContentKind.EquivalentPrompt,
                    equivalenceClass: "fh-l1-target-hold"),
            ]);

        var first = LocalContentBankSelector.Select(bank, new LocalContentBankSelectionRequest(request));
        var second = LocalContentBankSelector.Select(bank, new LocalContentBankSelectionRequest(request));

        Assert.True(first.HasSelection);
        Assert.True(first.CanUseContent);
        Assert.False(first.GrantsAdvancement);
        Assert.False(first.OwnsProgressionDecision);
        Assert.Empty(first.Failures);
        Assert.Equal("entry-a", first.Entry!.EntryId);
        Assert.Equal("content-wm-a", first.Content!.Result.ContentId);
        Assert.Equal(first.Content.Result.ContentId, second.Content!.Result.ContentId);
    }

    [Fact]
    public void FreshEquivalentSelectionSkipsPreviouslyUsedLocalContent()
    {
        var request = CreateWmRequest(previouslyUsedContentIds: ["content-wm-a"]);
        var bank = new LocalContentBank(
            "wm-bank",
            "bank-v1",
            LocalContentBankSourceKind.PackagedWithApp,
            [
                CreateWmEntry("entry-b", "content-wm-b"),
                CreateWmEntry("entry-a", "content-wm-a"),
            ]);

        var selection = LocalContentBankSelector.Select(bank, new LocalContentBankSelectionRequest(request));

        Assert.True(selection.HasSelection);
        Assert.Equal("content-wm-b", selection.Content!.Result.ContentId);
    }

    [Fact]
    public void IdenticalReusePolicyAllowsPreviouslyUsedLocalContent()
    {
        var request = CreateWmRequest(
            freshnessPolicy: PromptFreshnessPolicy.IdenticalReuseAllowed,
            previouslyUsedContentIds: ["content-wm-a"]);
        var bank = new LocalContentBank(
            "wm-bank",
            "bank-v1",
            LocalContentBankSourceKind.PackagedWithApp,
            [CreateWmEntry("entry-a", "content-wm-a")]);

        var selection = LocalContentBankSelector.Select(bank, new LocalContentBankSelectionRequest(request));

        Assert.True(selection.HasSelection);
        Assert.Equal("content-wm-a", selection.Content!.Result.ContentId);
    }

    [Fact]
    public void RequiredVersionCanBeCheckedDuringLocalSelection()
    {
        var request = CreateWmRequest();
        var bank = new LocalContentBank(
            "wm-bank",
            "bank-v1",
            LocalContentBankSourceKind.PackagedWithApp,
            [
                CreateWmEntry("entry-v1", "content-wm-v1", contentVersion: "content-v1"),
                CreateWmEntry("entry-v2", "content-wm-v2", contentVersion: "content-v2"),
            ]);

        var selected = LocalContentBankSelector.Select(
            bank,
            new LocalContentBankSelectionRequest(request, requiredContentVersion: "content-v2"));
        var rejected = LocalContentBankSelector.Select(
            bank,
            new LocalContentBankSelectionRequest(request, requiredContentVersion: "content-v3"));

        Assert.True(selected.HasSelection);
        Assert.Equal("content-v2", selected.Entry!.ContentVersion);
        Assert.False(rejected.HasSelection);
        Assert.Contains(rejected.Failures, failure =>
            failure.Kind == LocalContentBankSelectionFailureKind.VersionMismatch);
    }

    [Fact]
    public void SelectionRejectsLocalContentWhoseLoadDoesNotMatchTheRequest()
    {
        var request = CreateWmRequest();
        var bank = new LocalContentBank(
            "wm-bank",
            "bank-v1",
            LocalContentBankSourceKind.PackagedWithApp,
            [
                CreateWmEntry(
                    "entry-short-load",
                    "content-wm-short-load",
                    loadVariables: [new LoadVariable("item count", "4"), new LoadVariable("delay", "60 seconds")]),
            ]);

        var selection = LocalContentBankSelector.Select(bank, new LocalContentBankSelectionRequest(request));

        Assert.False(selection.HasSelection);
        Assert.False(selection.CanUseContent);
        Assert.Contains(selection.Failures, failure =>
            failure.Kind == LocalContentBankSelectionFailureKind.LoadVariablesMismatch);
    }

    [Fact]
    public void GeneratedLocalBankMaterialIsAllowedButStillOfflineOnly()
    {
        var entry = CreateWmEntry(
            entryId: "generated-entry",
            contentId: "generated-content",
            sourceKind: LocalContentBankSourceKind.GeneratedLocally);
        var bank = new LocalContentBank(
            "wm-bank",
            "bank-v1",
            LocalContentBankSourceKind.GeneratedLocally,
            [entry]);

        Assert.Equal(LocalContentBankSourceKind.GeneratedLocally, bank.SourceKind);
        Assert.True(bank.CanBeUsedOffline);
        Assert.False(bank.RequiresNetworkAccess);
        Assert.False(bank.AllowsAiOrApiDependencies);
    }

    [Fact]
    public void LocalContentBankRejectsMissingOrUnsupportedEssentials()
    {
        Assert.Throws<ArgumentException>(() => new LocalContentBank(
            " ",
            "bank-v1",
            LocalContentBankSourceKind.PackagedWithApp,
            [CreateWmEntry()]));
        Assert.Throws<ArgumentException>(() => new LocalContentBank(
            "wm-bank",
            " ",
            LocalContentBankSourceKind.PackagedWithApp,
            [CreateWmEntry()]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalContentBank(
            "wm-bank",
            "bank-v1",
            (LocalContentBankSourceKind)999,
            [CreateWmEntry()]));
        Assert.Throws<ArgumentException>(() => new LocalContentBank(
            "wm-bank",
            "bank-v1",
            LocalContentBankSourceKind.PackagedWithApp,
            []));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateWmEntry(
            sourceKind: (LocalContentBankSourceKind)999));
        Assert.Throws<ArgumentException>(() => new LocalContentBankSelectionRequest(
            CreateWmRequest(),
            requiredContentVersion: " "));
    }

    private static GeneratedDrillContentRequest CreateWmRequest(
        PromptFreshnessPolicy freshnessPolicy = PromptFreshnessPolicy.FreshEquivalentRequired,
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        return new GeneratedDrillContentRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            SessionType.Practice,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            freshnessPolicy,
            [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")],
            [new CriticalConstraint("No rereading after encode window."), new CriticalConstraint("No invented items.")],
            previouslyUsedContentIds);
    }

    private static LocalContentBankEntry CreateWmEntry(
        string entryId = "entry-wm-a",
        string contentId = "content-wm-a",
        string contentVersion = "content-v1",
        LocalContentBankSourceKind sourceKind = LocalContentBankSourceKind.PackagedWithApp,
        BranchCode branch = BranchCode.WM,
        GlobalLevelId level = GlobalLevelId.L1,
        DrillId drill = DrillId.WM1DelayedReconstruction,
        PromptContentKind contentKind = PromptContentKind.DelayedReconstructionTask,
        string equivalenceClass = "wm-l1-delayed-reconstruction",
        IEnumerable<LoadVariable>? loadVariables = null,
        IEnumerable<CriticalConstraint>? criticalConstraints = null)
    {
        var loads = loadVariables?.ToArray() ??
            [new LoadVariable("item count", "5"), new LoadVariable("delay", "60 seconds")];
        var constraints = criticalConstraints?.ToArray() ??
            [new CriticalConstraint("No rereading after encode window."), new CriticalConstraint("No invented items.")];

        return new LocalContentBankEntry(
            "wm-bank",
            entryId,
            new PromptContentIdentity(
                contentId,
                branch,
                level,
                drill,
                contentKind,
                equivalenceClass),
            contentVersion,
            sourceKind,
            loads,
            constraints,
            [
                new GeneratedContentPayloadFact("fixture-id", entryId),
                new GeneratedContentPayloadFact("payload-family", "delayed-reconstruction"),
            ],
            CreateWmMaterials(loads, constraints));
    }

    private static IReadOnlyList<GeneratedContentMaterial> CreateWmMaterials(
        IReadOnlyList<LoadVariable> loadVariables,
        IReadOnlyList<CriticalConstraint> criticalConstraints)
    {
        var itemCount = loadVariables.Single(variable => variable.Name == "item count").Value;
        var delay = loadVariables.Single(variable => variable.Name == "delay").Value;
        var materials = new List<GeneratedContentMaterial>
        {
            new(GeneratedContentMaterialKind.LoadVariable, "item count", itemCount),
            new(GeneratedContentMaterialKind.LoadVariable, "delay", delay),
            new(GeneratedContentMaterialKind.EncodeInstruction, "encode-instruction", "Study once."),
            new(GeneratedContentMaterialKind.DelayLength, "delay", delay),
            new(
                GeneratedContentMaterialKind.ReconstructionInstruction,
                "reconstruct",
                "Reconstruct the items in order."),
            new(GeneratedContentMaterialKind.ExpectedReconstruction, "expected", "alpha|bravo|cedar|delta|ember"),
        };

        foreach (var constraint in criticalConstraints)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.HonestyConstraint,
                "constraint-" + materials.Count,
                constraint.Description));
        }

        for (var index = 1; index <= int.Parse(itemCount, System.Globalization.CultureInfo.InvariantCulture); index++)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.EncodeItem,
                "item-" + index,
                "item-" + index));
        }

        return materials;
    }
}
