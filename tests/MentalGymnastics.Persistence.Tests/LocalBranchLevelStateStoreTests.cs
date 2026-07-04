using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalBranchLevelStateStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoadRoundTripsBranchLevelStatesAcrossAllBranchesAndLevels()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var saved = CreateAllBranchLevelStatuses();

        await CreateStore(databasePath).SaveAllAsync(saved);
        var loaded = await CreateStore(databasePath).LoadAllAsync();

        Assert.Equal(saved, loaded);
        foreach (var branch in Enum.GetValues<BranchCode>())
        {
            Assert.Contains(loaded, status => status.Branch == branch);
        }

        foreach (var level in Enum.GetValues<GlobalLevelId>())
        {
            Assert.Contains(loaded, status => status.Level == level);
        }

        foreach (var state in Enum.GetValues<BranchLevelState>())
        {
            Assert.Contains(loaded, status => status.State == state);
        }
    }

    [Fact]
    public async Task LoadReturnsEmptyCollectionWhenNoBranchLevelStateHasBeenSaved()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");

        var loaded = await CreateStore(databasePath).LoadAllAsync();

        Assert.Empty(loaded);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task CanLoadOneBranchLevelPairFromSavedWholeState()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        await CreateStore(databasePath).SaveAllAsync(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
        ]);

        var found = await CreateStore(databasePath).LoadAsync(BranchCode.WM, GlobalLevelId.L3);
        var missing = await CreateStore(databasePath).LoadAsync(BranchCode.WM, GlobalLevelId.L4);

        Assert.Equal(new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance), found);
        Assert.Null(missing);
    }

    [Fact]
    public async Task LegalTransitionUpdatePersistsOnlyTheTargetBranchLevelPair()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        await store.SaveAllAsync(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Unopened),
            new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L2, BranchLevelState.Maintenance),
        ]);

        var result = await store.TryApplyTransitionAsync(
            BranchCode.FS,
            GlobalLevelId.L1,
            BranchLevelTransition.MarkTestReady);
        var loaded = await CreateStore(databasePath).LoadAllAsync();

        Assert.True(result.IsValid);
        Assert.Equal(new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.TestReady), result.NextStatus);
        Assert.Equal(BranchLevelState.Unopened, StateOf(loaded, BranchCode.FH, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.TestReady, StateOf(loaded, BranchCode.FS, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Maintenance, StateOf(loaded, BranchCode.WM, GlobalLevelId.L2));
    }

    [Fact]
    public async Task UpdateCanApplyTransitionsAcrossMultipleBranchLevelPairs()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        await store.SaveAllAsync(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Unopened),
            new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L2, BranchLevelState.Maintenance),
        ]);

        var focusHold = await store.TryApplyTransitionAsync(
            BranchCode.FH,
            GlobalLevelId.L1,
            BranchLevelTransition.OpenForTraining);
        var focusShift = await store.TryApplyTransitionAsync(
            BranchCode.FS,
            GlobalLevelId.L1,
            BranchLevelTransition.MarkTestReady);
        var workingMemory = await store.TryApplyTransitionAsync(
            BranchCode.WM,
            GlobalLevelId.L2,
            BranchLevelTransition.MarkDecayed);
        var loaded = await CreateStore(databasePath).LoadAllAsync();

        Assert.True(focusHold.IsValid);
        Assert.True(focusShift.IsValid);
        Assert.True(workingMemory.IsValid);
        Assert.Equal(BranchLevelState.Training, StateOf(loaded, BranchCode.FH, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.TestReady, StateOf(loaded, BranchCode.FS, GlobalLevelId.L1));
        Assert.Equal(BranchLevelState.Decayed, StateOf(loaded, BranchCode.WM, GlobalLevelId.L2));
    }

    [Fact]
    public async Task IllegalTransitionUpdateDoesNotMutateStoredState()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var current = new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training);
        await store.SaveAllAsync([current]);

        var result = await store.TryApplyTransitionAsync(
            BranchCode.FH,
            GlobalLevelId.L1,
            BranchLevelTransition.CompleteStabilization);
        var loaded = await CreateStore(databasePath).LoadAllAsync();
        var coreResult = BranchLevelStateMachine.TryApply(
            current,
            BranchLevelTransition.CompleteStabilization);

        Assert.Equal(coreResult.IsValid, result.IsValid);
        Assert.False(result.IsValid);
        Assert.Equal(current, result.CurrentStatus);
        Assert.Equal(current, result.NextStatus);
        Assert.Equal(BranchLevelState.Training, StateOf(loaded, BranchCode.FH, GlobalLevelId.L1));
    }

    [Fact]
    public async Task MissingBranchLevelPairTransitionsFromUnopenedUsingCoreStateMachine()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var result = await CreateStore(databasePath).TryApplyTransitionAsync(
            BranchCode.TI,
            GlobalLevelId.L5,
            BranchLevelTransition.OpenForTraining);

        var loaded = await CreateStore(databasePath).LoadAllAsync();

        Assert.True(result.IsValid);
        Assert.Equal(new BranchLevelStatus(BranchCode.TI, GlobalLevelId.L5, BranchLevelState.Unopened), result.CurrentStatus);
        Assert.Equal(new BranchLevelStatus(BranchCode.TI, GlobalLevelId.L5, BranchLevelState.Training), result.NextStatus);
        Assert.Equal(BranchLevelState.Training, StateOf(loaded, BranchCode.TI, GlobalLevelId.L5));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalBranchLevelStateStore CreateStore(string databasePath)
    {
        return new LocalBranchLevelStateStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static IReadOnlyList<BranchLevelStatus> CreateAllBranchLevelStatuses()
    {
        var states = Enum.GetValues<BranchLevelState>();
        var branchLevels = new List<BranchLevelStatus>();
        var stateIndex = 0;

        foreach (var branch in Enum.GetValues<BranchCode>())
        {
            foreach (var level in Enum.GetValues<GlobalLevelId>())
            {
                branchLevels.Add(new BranchLevelStatus(
                    branch,
                    level,
                    states[stateIndex % states.Length]));
                stateIndex++;
            }
        }

        return branchLevels;
    }

    private static BranchLevelState StateOf(
        IEnumerable<BranchLevelStatus> statuses,
        BranchCode branch,
        GlobalLevelId level)
    {
        return statuses.Single(status => status.Branch == branch && status.Level == level).State;
    }
}
