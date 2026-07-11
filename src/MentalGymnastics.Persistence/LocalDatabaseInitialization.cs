using System.Text.Json;
using System.Text.Json.Nodes;

namespace MentalGymnastics.Persistence;

public static class LocalDatabaseSchema
{
    public const int CurrentVersion = 2;

    internal const string MetadataKind = "MentalGymnastics.LocalDatabase";
}

public enum LocalDatabaseStorageOwnership
{
    AppOwned,
}

public enum LocalDatabaseConnectivity
{
    OfflineOnly,
}

public sealed class LocalDatabaseOptions
{
    private LocalDatabaseOptions(string databasePath)
    {
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public static LocalDatabaseOptions ForAppOwnedPath(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        if (databasePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            databasePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            databasePath.Contains("://", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The local database path must be an app-owned filesystem path, not an external service target.",
                nameof(databasePath));
        }

        var fullPath = Path.GetFullPath(databasePath);
        if (!Path.IsPathFullyQualified(fullPath))
        {
            throw new ArgumentException(
                "The local database path must be fully qualified.",
                nameof(databasePath));
        }

        return new LocalDatabaseOptions(fullPath);
    }
}

public sealed record LocalDatabaseInitializationResult(
    string DatabasePath,
    int SchemaVersion,
    bool DatabaseCreated,
    LocalDatabaseStorageOwnership StorageOwnership,
    LocalDatabaseConnectivity Connectivity,
    IReadOnlyList<LocalDatabaseMigrationStep> AppliedMigrations);

public sealed record LocalDatabaseMigrationStep(
    int FromVersion,
    int ToVersion);

public interface ILocalDatabaseMigration
{
    int FromVersion { get; }

    int ToVersion { get; }

    ValueTask ApplyAsync(
        LocalDatabaseMigrationContext context,
        CancellationToken cancellationToken = default);
}

public sealed class LocalDatabaseMigrationContext
{
    private readonly JsonObject document;

    internal LocalDatabaseMigrationContext(JsonObject document)
    {
        this.document = document;
    }

    public int SchemaVersion => LocalDatabaseDocument.ReadSchemaVersion(document);

    public bool ContainsData(string name)
    {
        return document.ContainsKey(name);
    }
}

public sealed class LocalDatabaseMigrationException : Exception
{
    public LocalDatabaseMigrationException(
        int fromVersion,
        int targetVersion,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FromVersion = fromVersion;
        TargetVersion = targetVersion;
    }

    public int FromVersion { get; }

    public int TargetVersion { get; }
}

public sealed class LocalDatabaseInitializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly LocalDatabaseOptions options;
    private readonly IReadOnlyList<ILocalDatabaseMigration> migrations;

    public LocalDatabaseInitializer(
        LocalDatabaseOptions options,
        IEnumerable<ILocalDatabaseMigration>? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        this.migrations = (migrations ?? DefaultMigrations()).ToArray();
    }

    public async ValueTask<LocalDatabaseInitializationResult> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(options.DatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var databaseCreated = !File.Exists(options.DatabasePath);
        IReadOnlyList<LocalDatabaseMigrationStep> appliedMigrations = [];
        if (databaseCreated)
        {
            await WriteNewDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var existingVersion = LocalDatabaseDocument.ReadSchemaVersion(document);

            if (existingVersion > LocalDatabaseSchema.CurrentVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported local database schema version {existingVersion}.");
            }

            if (existingVersion < LocalDatabaseSchema.CurrentVersion)
            {
                appliedMigrations = await MigrateToCurrentVersionAsync(document, cancellationToken)
                    .ConfigureAwait(false);
                await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
            }
        }

        var schemaVersion = await GetCurrentSchemaVersionAsync(cancellationToken).ConfigureAwait(false);
        if (schemaVersion is null)
        {
            throw new InvalidOperationException("Local database initialization did not create schema metadata.");
        }

        if (schemaVersion.Value != LocalDatabaseSchema.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported local database schema version {schemaVersion.Value}.");
        }

        return new LocalDatabaseInitializationResult(
            options.DatabasePath,
            schemaVersion.Value,
            databaseCreated,
            LocalDatabaseStorageOwnership.AppOwned,
            LocalDatabaseConnectivity.OfflineOnly,
            appliedMigrations);
    }

    public async ValueTask<int?> GetCurrentSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(options.DatabasePath))
        {
            return null;
        }

        var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
        return LocalDatabaseDocument.ReadSchemaVersion(document);
    }

    private async ValueTask WriteNewDatabaseAsync(CancellationToken cancellationToken)
    {
        var document = LocalDatabaseDocument.Create(LocalDatabaseSchema.CurrentVersion);

        await WriteDocumentAsync(
            options.DatabasePath,
            document,
            FileMode.CreateNew,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<LocalDatabaseMigrationStep>> MigrateToCurrentVersionAsync(
        JsonObject document,
        CancellationToken cancellationToken)
    {
        var appliedMigrations = new List<LocalDatabaseMigrationStep>();
        var migrationsByVersion = BuildMigrationIndex();
        var version = LocalDatabaseDocument.ReadSchemaVersion(document);

        while (version < LocalDatabaseSchema.CurrentVersion)
        {
            if (!migrationsByVersion.TryGetValue(version, out var migration))
            {
                throw new LocalDatabaseMigrationException(
                    version,
                    LocalDatabaseSchema.CurrentVersion,
                    $"No migration is registered from local database schema version {version}.");
            }

            if (migration.ToVersion <= version ||
                migration.ToVersion > LocalDatabaseSchema.CurrentVersion)
            {
                throw new LocalDatabaseMigrationException(
                    version,
                    LocalDatabaseSchema.CurrentVersion,
                    $"Migration from version {migration.FromVersion} to {migration.ToVersion} is invalid.");
            }

            try
            {
                await migration.ApplyAsync(
                    new LocalDatabaseMigrationContext(document),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not LocalDatabaseMigrationException)
            {
                throw new LocalDatabaseMigrationException(
                    migration.FromVersion,
                    migration.ToVersion,
                    $"Migration from version {migration.FromVersion} to {migration.ToVersion} failed.",
                    exception);
            }

            LocalDatabaseDocument.SetSchemaVersion(document, migration.ToVersion);
            appliedMigrations.Add(new LocalDatabaseMigrationStep(migration.FromVersion, migration.ToVersion));
            version = migration.ToVersion;
        }

        return appliedMigrations;
    }

    private IReadOnlyDictionary<int, ILocalDatabaseMigration> BuildMigrationIndex()
    {
        return migrations.ToDictionary(
            migration => migration.FromVersion,
            migration => migration);
    }

    private async ValueTask<JsonObject> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            options.DatabasePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var document = await LocalJsonDocumentIO.ReadObjectAsync(stream, cancellationToken)
            .ConfigureAwait(false);

        if (document is null ||
            !document.TryGetPropertyValue("Kind", out var kindNode) ||
            kindNode?.GetValue<string>() != LocalDatabaseSchema.MetadataKind)
        {
            throw new InvalidOperationException("The local database metadata is missing or invalid.");
        }

        LocalDatabaseDocument.ReadSchemaVersion(document);
        return document;
    }

    private async ValueTask ReplaceDatabaseAsync(JsonObject document, CancellationToken cancellationToken)
    {
        var tempPath = $"{options.DatabasePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await WriteDocumentAsync(
                tempPath,
                document,
                FileMode.CreateNew,
                cancellationToken).ConfigureAwait(false);

            File.Move(tempPath, options.DatabasePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async ValueTask WriteDocumentAsync(
        string path,
        JsonObject document,
        FileMode mode,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            mode,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await LocalJsonDocumentIO.WriteObjectAsync(stream, document, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<ILocalDatabaseMigration> DefaultMigrations()
    {
        return [new LocalDatabaseMigration0To1(), new LocalDatabaseMigration1To2()];
    }

    private sealed class LocalDatabaseMigration0To1 : ILocalDatabaseMigration
    {
        public int FromVersion => 0;

        public int ToVersion => 1;

        public ValueTask ApplyAsync(
            LocalDatabaseMigrationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class LocalDatabaseMigration1To2 : ILocalDatabaseMigration
    {
        public int FromVersion => 1;

        public int ToVersion => 2;

        public ValueTask ApplyAsync(
            LocalDatabaseMigrationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}

internal static class LocalDatabaseDocument
{
    public static JsonObject Create(int schemaVersion)
    {
        return new JsonObject
        {
            ["Kind"] = LocalDatabaseSchema.MetadataKind,
            ["SchemaVersion"] = schemaVersion,
        };
    }

    public static int ReadSchemaVersion(JsonObject document)
    {
        if (!document.TryGetPropertyValue("SchemaVersion", out var schemaVersionNode) ||
            schemaVersionNode is null)
        {
            throw new InvalidOperationException("The local database schema version is missing.");
        }

        try
        {
            return schemaVersionNode.GetValue<int>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                "The local database schema version is invalid.",
                exception);
        }
    }

    public static void SetSchemaVersion(JsonObject document, int schemaVersion)
    {
        document["SchemaVersion"] = schemaVersion;
    }
}
