using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalPractitionerStateStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadReturnsNullWhenNoPractitionerStateHasBeenSaved()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        var loaded = await store.LoadAsync();

        Assert.Null(loaded);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task SaveAndLoadRoundTripsCurrentWholePractitionerStateAcrossStoreInstances()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var saved = new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
            new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L2, BranchLevelState.TestReady),
            new BranchLevelStatus(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.PassedOnce),
            new BranchLevelStatus(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Stabilizing),
            new BranchLevelStatus(BranchCode.CO, GlobalLevelId.L1, BranchLevelState.Maintenance),
            new BranchLevelStatus(BranchCode.AI, GlobalLevelId.L1, BranchLevelState.Decayed),
            new BranchLevelStatus(BranchCode.TI, GlobalLevelId.L1, BranchLevelState.Unopened),
        ]);

        await CreateStore(databasePath).SaveAsync(saved);
        var loaded = await CreateStore(databasePath).LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(saved.BranchLevels, loaded.BranchLevels);
        Assert.Equal(BranchLevelState.Decayed, loaded.GetBranchLevelState(BranchCode.AI, GlobalLevelId.L1));
    }

    [Fact]
    public async Task SavePersistsStableDomainIdentifiersInsteadOfDisplayWording()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var state = new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
        ]);

        await CreateStore(databasePath).SaveAsync(state);
        var storedJson = await File.ReadAllTextAsync(databasePath);

        Assert.Contains("\"Branch\": \"FH\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Level\": \"L1\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"State\": \"Owned\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Branch\": \"WM\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Level\": \"L3\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"State\": \"Maintenance\"", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Focus Hold", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Protected Control", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Working Memory and Reconstruction", storedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveReplacesTheCurrentWholePractitionerState()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        await store.SaveAsync(new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training),
        ]));

        var replacement = new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
        ]);
        await store.SaveAsync(replacement);
        var loaded = await CreateStore(databasePath).LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(replacement.BranchLevels, loaded.BranchLevels);
        Assert.False(loaded.TryGetBranchLevelState(BranchCode.WM, GlobalLevelId.L1, out _));
    }

    [Fact]
    public async Task UpdateLoadsCurrentStateAndPersistsReturnedState()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        await store.SaveAsync(new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce),
        ]));

        var updated = await store.UpdateAsync(current =>
        {
            Assert.NotNull(current);
            return new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Stabilizing),
                new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
            ]);
        });
        var loaded = await CreateStore(databasePath).LoadAsync();

        Assert.Equal(updated.BranchLevels, loaded?.BranchLevels);
        Assert.Equal(BranchLevelState.Stabilizing, loaded!.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Training, loaded.GetBranchLevelState(BranchCode.FS, GlobalLevelId.L1));
    }

    [Fact]
    public async Task PractitionerStatePersistenceDoesNotStoreAccountsOrDerivedProgressionDecisions()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        await CreateStore(databasePath).SaveAsync(new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
        ]));

        var storedJson = await File.ReadAllTextAsync(databasePath);

        Assert.DoesNotContain("User", storedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Account", storedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Email", storedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PractitionerCategory", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("TestReadiness", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("MaintenanceCurrency", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("DependencyCap", storedJson, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalPractitionerStateStore CreateStore(string databasePath)
    {
        return new LocalPractitionerStateStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }
}
