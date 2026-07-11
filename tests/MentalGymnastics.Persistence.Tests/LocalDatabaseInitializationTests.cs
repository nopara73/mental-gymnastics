using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalDatabaseInitializationTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeCreatesLocalDatabaseAndTracksCurrentSchemaVersion()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var initializer = new LocalDatabaseInitializer(LocalDatabaseOptions.ForAppOwnedPath(databasePath));

        Assert.Null(await initializer.GetCurrentSchemaVersionAsync());

        var result = await initializer.InitializeAsync();

        Assert.True(File.Exists(databasePath));
        Assert.True(result.DatabaseCreated);
        Assert.Equal(Path.GetFullPath(databasePath), result.DatabasePath);
        Assert.Equal(LocalDatabaseSchema.CurrentVersion, result.SchemaVersion);
        Assert.Equal(LocalDatabaseStorageOwnership.AppOwned, result.StorageOwnership);
        Assert.Equal(LocalDatabaseConnectivity.OfflineOnly, result.Connectivity);
        Assert.Empty(result.AppliedMigrations);
        Assert.Equal(LocalDatabaseSchema.CurrentVersion, await initializer.GetCurrentSchemaVersionAsync());
    }

    [Fact]
    public async Task InitializationIsRepeatableWithoutChangingSchemaVersion()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var initializer = new LocalDatabaseInitializer(LocalDatabaseOptions.ForAppOwnedPath(databasePath));

        var first = await initializer.InitializeAsync();
        var second = await initializer.InitializeAsync();

        Assert.True(first.DatabaseCreated);
        Assert.False(second.DatabaseCreated);
        Assert.Equal(LocalDatabaseSchema.CurrentVersion, first.SchemaVersion);
        Assert.Equal(first.SchemaVersion, second.SchemaVersion);
        Assert.Empty(first.AppliedMigrations);
        Assert.Empty(second.AppliedMigrations);
        Assert.Equal(LocalDatabaseSchema.CurrentVersion, await initializer.GetCurrentSchemaVersionAsync());
    }

    [Fact]
    public async Task InitializationUpgradesOlderSchemaToCurrentVersionWithoutDroppingExistingData()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        Directory.CreateDirectory(tempDirectory);
        await File.WriteAllTextAsync(
            databasePath,
            """
            {
              "Kind": "MentalGymnastics.LocalDatabase",
              "SchemaVersion": 0,
              "ProgressionData": {
                "Branch": "FH",
                "State": "Training"
              }
            }
            """);
        var initializer = new LocalDatabaseInitializer(LocalDatabaseOptions.ForAppOwnedPath(databasePath));

        var result = await initializer.InitializeAsync();
        var migratedJson = await File.ReadAllTextAsync(databasePath);

        Assert.False(result.DatabaseCreated);
        Assert.Equal(LocalDatabaseSchema.CurrentVersion, result.SchemaVersion);
        Assert.Contains(new LocalDatabaseMigrationStep(0, 1), result.AppliedMigrations);
        Assert.Contains(new LocalDatabaseMigrationStep(1, 2), result.AppliedMigrations);
        Assert.Equal(LocalDatabaseSchema.CurrentVersion, await initializer.GetCurrentSchemaVersionAsync());
        Assert.Contains("\"ProgressionData\"", migratedJson, StringComparison.Ordinal);
        Assert.Contains("\"Branch\": \"FH\"", migratedJson, StringComparison.Ordinal);
        Assert.Contains("\"State\": \"Training\"", migratedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedMigrationLeavesExistingDatabaseUnchanged()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        Directory.CreateDirectory(tempDirectory);
        var originalDatabase = """
            {
              "Kind": "MentalGymnastics.LocalDatabase",
              "SchemaVersion": 0,
              "ProgressionData": {
                "Branch": "FH",
                "State": "Training"
              }
            }
            """;
        await File.WriteAllTextAsync(databasePath, originalDatabase);
        var initializer = new LocalDatabaseInitializer(
            LocalDatabaseOptions.ForAppOwnedPath(databasePath),
            [new FailingMigration()]);

        await Assert.ThrowsAsync<LocalDatabaseMigrationException>(
            async () => await initializer.InitializeAsync());

        Assert.Equal(originalDatabase, await File.ReadAllTextAsync(databasePath));
    }

    [Theory]
    [InlineData("https://example.com/mental-gymnastics.db")]
    [InlineData("http://example.com/mental-gymnastics.db")]
    public void AppOwnedDatabasePathMustBeLocalNotExternalService(string externalTarget)
    {
        Assert.Throws<ArgumentException>(() => LocalDatabaseOptions.ForAppOwnedPath(externalTarget));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private sealed class FailingMigration : ILocalDatabaseMigration
    {
        public int FromVersion => 0;

        public int ToVersion => 1;

        public ValueTask ApplyAsync(
            LocalDatabaseMigrationContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated migration failure.");
        }
    }
}
