using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalGeneratedDrillInstanceStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadReturnsNullAndListsAreEmptyWhenNoGeneratedInstancesHaveBeenSaved()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        var loaded = await store.LoadAsync("missing-instance");
        var allInstances = await store.ListAsync();
        var branchLevelInstances = await store.ListByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L1);
        var reusableInstances = await store.ListReusableAsync(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")]);

        Assert.Null(loaded);
        Assert.Empty(allInstances);
        Assert.Empty(branchLevelInstances);
        Assert.Empty(reusableInstances);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task SaveLoadAndListRoundTripGeneratedInstanceAfterRestart()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var expected = Instance(
            "instance-fh-l1-001",
            TrainingDate.From(2026, 7, 4),
            state: LocalGeneratedDrillInstanceState.InSession,
            activeSessionId: "session-fh-l1-001");

        await CreateStore(databasePath).SaveAsync(expected);

        var loaded = await CreateStore(databasePath).LoadAsync(expected.InstanceId);
        var allInstances = await CreateStore(databasePath).ListAsync();

        Assert.NotNull(loaded);
        AssertEquivalent(expected, loaded);
        Assert.Single(allInstances);
        AssertEquivalent(expected, Assert.Single(allInstances));
    }

    [Fact]
    public async Task ListByBranchLevelAndDrillFiltersGeneratedInstancesInGenerationDateOrder()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveAsync(Instance(
            "wm-l1",
            TrainingDate.From(2026, 7, 7),
            branch: BranchCode.WM,
            drill: DrillId.WM1DelayedReconstruction,
            loadVariables:
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("delay", "60 seconds"),
            ],
            contentIdentity: ContentIdentity(
                "wm-content-001",
                branch: BranchCode.WM,
                drill: DrillId.WM1DelayedReconstruction,
                kind: PromptContentKind.DelayedReconstructionTask,
                equivalenceClass: "WM1-five-items-short-delay",
                version: "v1")));
        await store.SaveAsync(Instance("fh-l1-later", TrainingDate.From(2026, 7, 8)));
        await store.SaveAsync(Instance("fh-l1-earlier", TrainingDate.From(2026, 7, 4)));
        await store.SaveAsync(Instance(
            "fh-l2",
            TrainingDate.From(2026, 7, 5),
            level: GlobalLevelId.L2,
            contentIdentity: ContentIdentity("fh-l2-content-001", level: GlobalLevelId.L2)));

        var focusHoldL1 = await CreateStore(databasePath).ListByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L1);
        var focusHoldDrill = await CreateStore(databasePath).ListByDrillAsync(DrillId.FH1TargetHold);

        Assert.Equal(["fh-l1-earlier", "fh-l1-later"], focusHoldL1.Select(record => record.InstanceId));
        Assert.Equal(["fh-l1-earlier", "fh-l2", "fh-l1-later"], focusHoldDrill.Select(record => record.InstanceId));
    }

    [Fact]
    public async Task ReusableInstancesMatchSameBranchLevelDrillAndLoadVariablesWithoutCompletedResults()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveAsync(Instance("same-reserved", TrainingDate.From(2026, 7, 4)));
        await store.SaveAsync(Instance(
            "same-in-session",
            TrainingDate.From(2026, 7, 5),
            state: LocalGeneratedDrillInstanceState.InSession,
            activeSessionId: "session-fh-l1-002"));
        await store.SaveAsync(Instance(
            "same-completed",
            TrainingDate.From(2026, 7, 6),
            state: LocalGeneratedDrillInstanceState.Completed,
            resultEvidenceArtifactId: "artifact-result-001"));
        await store.SaveAsync(Instance(
            "different-load",
            TrainingDate.From(2026, 7, 7),
            loadVariables: [new LoadVariable("duration", "4 minutes")]));
        await store.SaveAsync(Instance(
            "different-drill",
            TrainingDate.From(2026, 7, 8),
            drill: DrillId.FH2DistractorHold,
            contentIdentity: ContentIdentity(
                "fh2-content-001",
                drill: DrillId.FH2DistractorHold,
                equivalenceClass: "FH2-visible-distractor",
                version: "v1")));

        var reusable = await CreateStore(databasePath).ListReusableAsync(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")]);

        Assert.Equal(["same-reserved", "same-in-session"], reusable.Select(record => record.InstanceId));
    }

    [Fact]
    public async Task FreshEquivalentInstancesShareDemandAndVersionButKeepDistinctContentIdentity()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var originalContent = ContentIdentity("fh-content-001");
        var freshEquivalentContent = ContentIdentity("fh-content-002");

        await store.SaveAsync(Instance("original", TrainingDate.From(2026, 7, 4), contentIdentity: originalContent));
        await store.SaveAsync(Instance("fresh-equivalent", TrainingDate.From(2026, 7, 5), contentIdentity: freshEquivalentContent));
        await store.SaveAsync(Instance(
            "same-content",
            TrainingDate.From(2026, 7, 6),
            contentIdentity: originalContent));
        await store.SaveAsync(Instance(
            "different-version",
            TrainingDate.From(2026, 7, 7),
            contentIdentity: ContentIdentity("fh-content-003", version: "v2")));
        await store.SaveAsync(Instance(
            "different-equivalence-class",
            TrainingDate.From(2026, 7, 8),
            contentIdentity: ContentIdentity("fh-content-004", equivalenceClass: "FH1-longer-target-hold")));

        var freshEquivalents = await CreateStore(databasePath).ListFreshEquivalentAsync(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")],
            originalContent);

        var loadedOriginal = await CreateStore(databasePath).LoadAsync("original");
        var loadedFreshEquivalent = await CreateStore(databasePath).LoadAsync("fresh-equivalent");

        Assert.Equal("fresh-equivalent", Assert.Single(freshEquivalents).InstanceId);
        Assert.NotNull(loadedOriginal);
        Assert.NotNull(loadedFreshEquivalent);
        Assert.Equal(loadedOriginal.ContentIdentity.EquivalenceClass, loadedFreshEquivalent.ContentIdentity.EquivalenceClass);
        Assert.Equal(loadedOriginal.ContentIdentity.Version, loadedFreshEquivalent.ContentIdentity.Version);
        Assert.NotEqual(loadedOriginal.ContentIdentity.ContentId, loadedFreshEquivalent.ContentIdentity.ContentId);
    }

    [Fact]
    public async Task SaveReplacesInstanceWithSameIdentifierWithoutDuplicatingHistory()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var original = Instance("same-instance", TrainingDate.From(2026, 7, 4));
        var replacement = Instance(
            "same-instance",
            TrainingDate.From(2026, 7, 4),
            state: LocalGeneratedDrillInstanceState.Completed,
            resultEvidenceArtifactId: "artifact-result-001");
        var store = CreateStore(databasePath);

        await store.SaveAsync(original);
        await store.SaveAsync(replacement);

        var allInstances = await CreateStore(databasePath).ListAsync();
        var loaded = await CreateStore(databasePath).LoadAsync("same-instance");

        Assert.Single(allInstances);
        Assert.NotNull(loaded);
        AssertEquivalent(replacement, loaded);
    }

    [Fact]
    public async Task SavedInstancesUseStableDomainIdentifiersAndDoNotPersistGenerationInfrastructure()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var record = Instance("instance-fh-l1-001", TrainingDate.From(2026, 7, 4));

        await CreateStore(databasePath).SaveAsync(record);
        var storedJson = await File.ReadAllTextAsync(databasePath);

        Assert.Contains("\"Branch\": \"FH\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Level\": \"L1\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Drill\": \"FH1TargetHold\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"ContentKind\": \"EquivalentPrompt\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"ContentId\": \"fh-content-001\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Version\": \"v1\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"EquivalenceClass\": \"FH1-simple-target-hold\"", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Focus Hold", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Account", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Telemetry", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Endpoint", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiKey", storedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedInstanceRequiresLoadVariablesAndContentIdentity()
    {
        var missingLoadVariable = Assert.Throws<ArgumentException>(() =>
            Instance("missing-load", TrainingDate.From(2026, 7, 4), loadVariables: []));
        var missingContentIdentity = Assert.Throws<ArgumentNullException>(() =>
            new LocalGeneratedDrillInstanceRecord(
                "missing-content",
                TrainingDate.From(2026, 7, 4),
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold,
                [new LoadVariable("duration", "3 minutes")],
                contentIdentity: null!));

        Assert.Contains("load", missingLoadVariable.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("contentIdentity", missingContentIdentity.ParamName);
    }

    [Fact]
    public void ContentIdentityConsumesCorePromptContentIdentityWithoutDroppingVersion()
    {
        var coreIdentity = new PromptContentIdentity(
            "fh-content-001",
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            PromptContentKind.EquivalentPrompt,
            "FH1-simple-target-hold");

        var persistedIdentity = new LocalGeneratedDrillContentIdentity(coreIdentity, version: "v1");
        var restoredCoreIdentity = persistedIdentity.ToPromptContentIdentity();

        Assert.Equal(coreIdentity.ContentId, persistedIdentity.ContentId);
        Assert.Equal(coreIdentity.Branch, persistedIdentity.Branch);
        Assert.Equal(coreIdentity.Level, persistedIdentity.Level);
        Assert.Equal(coreIdentity.Drill, persistedIdentity.Drill);
        Assert.Equal(coreIdentity.Kind, persistedIdentity.Kind);
        Assert.Equal(coreIdentity.EquivalenceClass, persistedIdentity.EquivalenceClass);
        Assert.Equal("v1", persistedIdentity.Version);
        Assert.Equal(coreIdentity.ContentId, restoredCoreIdentity.ContentId);
        Assert.Equal(coreIdentity.Branch, restoredCoreIdentity.Branch);
        Assert.Equal(coreIdentity.Level, restoredCoreIdentity.Level);
        Assert.Equal(coreIdentity.Drill, restoredCoreIdentity.Drill);
        Assert.Equal(coreIdentity.Kind, restoredCoreIdentity.Kind);
        Assert.Equal(coreIdentity.EquivalenceClass, restoredCoreIdentity.EquivalenceClass);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalGeneratedDrillInstanceStore CreateStore(string databasePath)
    {
        return new LocalGeneratedDrillInstanceStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static LocalGeneratedDrillInstanceRecord Instance(
        string instanceId,
        TrainingDate generatedOn,
        BranchCode branch = BranchCode.FH,
        GlobalLevelId level = GlobalLevelId.L1,
        DrillId drill = DrillId.FH1TargetHold,
        IEnumerable<LoadVariable>? loadVariables = null,
        LocalGeneratedDrillContentIdentity? contentIdentity = null,
        LocalGeneratedDrillInstanceState state = LocalGeneratedDrillInstanceState.Reserved,
        string? activeSessionId = null,
        string? resultEvidenceArtifactId = null)
    {
        return new LocalGeneratedDrillInstanceRecord(
            instanceId,
            generatedOn,
            branch,
            level,
            drill,
            loadVariables ?? [new LoadVariable("duration", "3 minutes")],
            contentIdentity ?? ContentIdentity("fh-content-001", branch, level, drill),
            state,
            activeSessionId,
            resultEvidenceArtifactId);
    }

    private static LocalGeneratedDrillContentIdentity ContentIdentity(
        string contentId,
        BranchCode branch = BranchCode.FH,
        GlobalLevelId level = GlobalLevelId.L1,
        DrillId drill = DrillId.FH1TargetHold,
        PromptContentKind kind = PromptContentKind.EquivalentPrompt,
        string equivalenceClass = "FH1-simple-target-hold",
        string version = "v1")
    {
        return new LocalGeneratedDrillContentIdentity(
            contentId,
            branch,
            level,
            drill,
            kind,
            equivalenceClass,
            version);
    }

    private static void AssertEquivalent(
        LocalGeneratedDrillInstanceRecord expected,
        LocalGeneratedDrillInstanceRecord actual)
    {
        Assert.Equal(expected.InstanceId, actual.InstanceId);
        Assert.Equal(expected.GeneratedOn, actual.GeneratedOn);
        Assert.Equal(expected.Branch, actual.Branch);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Drill, actual.Drill);
        Assert.Equal(expected.LoadVariables, actual.LoadVariables);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.ActiveSessionId, actual.ActiveSessionId);
        Assert.Equal(expected.ResultEvidenceArtifactId, actual.ResultEvidenceArtifactId);
        Assert.Equal(expected.ContentIdentity.ContentId, actual.ContentIdentity.ContentId);
        Assert.Equal(expected.ContentIdentity.Branch, actual.ContentIdentity.Branch);
        Assert.Equal(expected.ContentIdentity.Level, actual.ContentIdentity.Level);
        Assert.Equal(expected.ContentIdentity.Drill, actual.ContentIdentity.Drill);
        Assert.Equal(expected.ContentIdentity.Kind, actual.ContentIdentity.Kind);
        Assert.Equal(expected.ContentIdentity.EquivalenceClass, actual.ContentIdentity.EquivalenceClass);
        Assert.Equal(expected.ContentIdentity.Version, actual.ContentIdentity.Version);
    }
}
