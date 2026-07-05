using MentalGymnastics.App;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App.Tests;

public sealed class AppStartupConfigurationTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StartupInitializesAppOwnedJsonPersistenceThroughPersistenceBoundary()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.json");
        var configuration = AppStartupConfiguration.ForAppOwnedLocalStoragePath(databasePath);
        var startup = new MentalGymnasticsAppStartup(configuration);

        var result = await startup.InitializeAsync();

        Assert.True(File.Exists(databasePath));
        Assert.Equal(Path.GetFullPath(databasePath), configuration.LocalDatabasePath);
        Assert.Equal(Path.GetFullPath(databasePath), configuration.LocalDatabaseOptions.DatabasePath);
        Assert.Equal(Path.GetFullPath(databasePath), result.DatabasePath);
        Assert.True(result.DatabaseCreated);
        Assert.Equal(LocalDatabaseSchema.CurrentVersion, result.SchemaVersion);
        Assert.Equal(LocalDatabaseStorageOwnership.AppOwned, result.StorageOwnership);
        Assert.Equal(LocalDatabaseConnectivity.OfflineOnly, result.Connectivity);
        Assert.Empty(result.AppliedMigrations);
    }

    [Fact]
    public async Task StartupInitializationIsRepeatableForSameAppOwnedPath()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.json");
        var startup = new MentalGymnasticsAppStartup(
            AppStartupConfiguration.ForAppOwnedLocalStoragePath(databasePath));

        var first = await startup.InitializeAsync();
        var second = await startup.InitializeAsync();

        Assert.True(first.DatabaseCreated);
        Assert.False(second.DatabaseCreated);
        Assert.Equal(LocalDatabaseSchema.CurrentVersion, first.SchemaVersion);
        Assert.Equal(first.SchemaVersion, second.SchemaVersion);
    }

    [Theory]
    [InlineData("https://example.com/mental-gymnastics.json")]
    [InlineData("http://example.com/mental-gymnastics.json")]
    [InlineData("s3://bucket/mental-gymnastics.json")]
    [InlineData("content://mental-gymnastics/progression")]
    public void StartupConfigurationRejectsExternalServiceTargets(string externalTarget)
    {
        Assert.Throws<ArgumentException>(
            () => AppStartupConfiguration.ForAppOwnedLocalStoragePath(externalTarget));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StartupConfigurationRejectsMissingLocalPath(string missingPath)
    {
        Assert.Throws<ArgumentException>(
            () => AppStartupConfiguration.ForAppOwnedLocalStoragePath(missingPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
