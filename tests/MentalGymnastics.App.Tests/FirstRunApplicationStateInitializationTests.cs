using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App.Tests;

public sealed class FirstRunApplicationStateInitializationTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FirstRunCreatesMinimumHonestPractitionerStateFromCoreCatalog()
    {
        var configuration = Configuration();
        var startup = new MentalGymnasticsAppStartup(configuration);

        await startup.InitializeAsync();

        var loaded = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();
        var branchLevels = await new LocalBranchLevelStateStore(configuration.LocalDatabaseOptions).LoadAllAsync();

        Assert.NotNull(loaded);
        Assert.Equal(ProgramCatalog.Branches.Count * ProgramCatalog.GlobalLevels.Count, loaded.BranchLevels.Count);
        Assert.Equal(loaded.BranchLevels, branchLevels);
        Assert.Equal(BranchLevelState.Training, loaded.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));

        var expectedPairs = ProgramCatalog.Branches.SelectMany(
            _ => ProgramCatalog.GlobalLevels,
            (branch, level) => (branch.Code, level.Id));
        Assert.Equal(expectedPairs, loaded.BranchLevels.Select(status => (status.Branch, status.Level)));

        Assert.All(
            loaded.BranchLevels.Where(status => status is not { Branch: BranchCode.FH, Level: GlobalLevelId.L1 }),
            status => Assert.Equal(BranchLevelState.Unopened, status.State));
        Assert.DoesNotContain(loaded.BranchLevels, GrantsProgressWithoutEvidence);
    }

    [Fact]
    public async Task RepeatedStartupKeepsTheSameInitialState()
    {
        var configuration = Configuration();
        var startup = new MentalGymnasticsAppStartup(configuration);

        await startup.InitializeAsync();
        var first = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();

        await startup.InitializeAsync();
        var second = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.BranchLevels, second.BranchLevels);
    }

    [Fact]
    public async Task ExistingPractitionerStateIsNotOverwritten()
    {
        var configuration = Configuration();
        var existing = new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce),
            new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
        ]);
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(existing);

        await new MentalGymnasticsAppStartup(configuration).InitializeAsync();

        var loaded = await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(existing.BranchLevels, loaded.BranchLevels);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private AppStartupConfiguration Configuration()
    {
        return AppStartupConfiguration.ForAppOwnedLocalStoragePath(
            Path.Combine(tempDirectory, "mental-gymnastics.json"));
    }

    private static bool GrantsProgressWithoutEvidence(BranchLevelStatus status)
    {
        return status.State is BranchLevelState.TestReady
            or BranchLevelState.PassedOnce
            or BranchLevelState.Stabilizing
            or BranchLevelState.Owned
            or BranchLevelState.Maintenance
            or BranchLevelState.Decayed;
    }
}
