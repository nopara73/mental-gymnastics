using System.Text.Json;
using System.Text.Json.Nodes;

namespace MentalGymnastics.Persistence;

public sealed class LocalBackupPackage
{
    public LocalBackupPackage(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Local backup payload is required.", nameof(payload));
        }

        Payload = payload;
    }

    public string Payload { get; }

    public int DatabaseSchemaVersion => LocalBackupEnvelope.Read(Payload).DatabaseSchemaVersion;

    public LocalDatabaseStorageOwnership StorageOwnership => LocalBackupEnvelope.Read(Payload).StorageOwnership;

    public LocalDatabaseConnectivity Connectivity => LocalBackupEnvelope.Read(Payload).Connectivity;
}

public sealed class LocalBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalBackupService(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask<LocalBackupPackage> ExportAsync(
        CancellationToken cancellationToken = default)
    {
        await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var document = await ReadDatabaseDocumentAsync(cancellationToken).ConfigureAwait(false);
        var schemaVersion = LocalDatabaseDocument.ReadSchemaVersion(document);
        var envelope = LocalBackupEnvelope.Create(document, schemaVersion);

        return new LocalBackupPackage(envelope.ToJsonString(JsonOptions));
    }

    public async ValueTask RestoreAsync(
        LocalBackupPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        cancellationToken.ThrowIfCancellationRequested();

        var envelope = LocalBackupEnvelope.Read(package.Payload);
        if (envelope.DatabaseSchemaVersion != LocalDatabaseSchema.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Local backup schema version {envelope.DatabaseSchemaVersion} is not supported by this app version.");
        }

        var databaseDocument = envelope.DatabaseDocument.DeepClone().AsObject();
        var databaseSchemaVersion = ValidateDatabaseDocument(databaseDocument);
        if (databaseSchemaVersion != envelope.DatabaseSchemaVersion)
        {
            throw new InvalidOperationException("The local backup database schema metadata is inconsistent.");
        }

        LocalPersistenceIntegrityValidator.ThrowIfInvalid(databaseDocument, "restoring a local backup");

        await ReplaceDatabaseAsync(databaseDocument, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<JsonObject> ReadDatabaseDocumentAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            options.DatabasePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var document = await JsonSerializer.DeserializeAsync<JsonObject>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (document is null)
        {
            throw new InvalidOperationException("The local database document is missing.");
        }

        ValidateDatabaseDocument(document);
        return document;
    }

    private async ValueTask ReplaceDatabaseAsync(
        JsonObject document,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(options.DatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{options.DatabasePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

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

    private static int ValidateDatabaseDocument(JsonObject document)
    {
        if (!document.TryGetPropertyValue("Kind", out var kindNode) ||
            kindNode?.GetValue<string>() != LocalDatabaseSchema.MetadataKind)
        {
            throw new InvalidOperationException("The local backup database metadata is missing or invalid.");
        }

        var schemaVersion = LocalDatabaseDocument.ReadSchemaVersion(document);
        if (schemaVersion > LocalDatabaseSchema.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Local backup database schema version {schemaVersion} is not supported by this app version.");
        }

        return schemaVersion;
    }
}

internal sealed record LocalBackupEnvelope(
    int DatabaseSchemaVersion,
    LocalDatabaseStorageOwnership StorageOwnership,
    LocalDatabaseConnectivity Connectivity,
    JsonObject DatabaseDocument)
{
    private const string BackupKind = "MentalGymnastics.LocalBackup";
    private const int BackupSchemaVersion = 1;
    private const string KindPropertyName = "Kind";
    private const string BackupSchemaVersionPropertyName = "BackupSchemaVersion";
    private const string DatabaseSchemaVersionPropertyName = "DatabaseSchemaVersion";
    private const string StorageOwnershipPropertyName = "StorageOwnership";
    private const string ConnectivityPropertyName = "Connectivity";
    private const string DataPropertyName = "Data";

    public static JsonObject Create(JsonObject databaseDocument, int databaseSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(databaseDocument);

        return new JsonObject
        {
            [KindPropertyName] = BackupKind,
            [BackupSchemaVersionPropertyName] = BackupSchemaVersion,
            [DatabaseSchemaVersionPropertyName] = databaseSchemaVersion,
            [StorageOwnershipPropertyName] = nameof(LocalDatabaseStorageOwnership.AppOwned),
            [ConnectivityPropertyName] = nameof(LocalDatabaseConnectivity.OfflineOnly),
            [DataPropertyName] = databaseDocument.DeepClone(),
        };
    }

    public static LocalBackupEnvelope Read(string payload)
    {
        JsonObject envelopeObject;
        try
        {
            envelopeObject = JsonNode.Parse(payload) as JsonObject
                ?? throw new InvalidOperationException("The local backup payload is invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The local backup payload is invalid.", exception);
        }

        var kind = ReadRequiredString(envelopeObject, KindPropertyName);
        if (kind != BackupKind)
        {
            throw new InvalidOperationException("The local backup kind is missing or invalid.");
        }

        var backupSchemaVersion = ReadRequiredInt32(envelopeObject, BackupSchemaVersionPropertyName);
        if (backupSchemaVersion != BackupSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Local backup package schema version {backupSchemaVersion} is not supported.");
        }

        var storageOwnership = ReadRequiredEnum<LocalDatabaseStorageOwnership>(
            envelopeObject,
            StorageOwnershipPropertyName);
        var connectivity = ReadRequiredEnum<LocalDatabaseConnectivity>(
            envelopeObject,
            ConnectivityPropertyName);
        if (storageOwnership != LocalDatabaseStorageOwnership.AppOwned ||
            connectivity != LocalDatabaseConnectivity.OfflineOnly)
        {
            throw new InvalidOperationException("Local backup packages must be app-owned and offline-only.");
        }

        var data = ReadRequiredObject(envelopeObject, DataPropertyName);
        return new LocalBackupEnvelope(
            ReadRequiredInt32(envelopeObject, DatabaseSchemaVersionPropertyName),
            storageOwnership,
            connectivity,
            data.DeepClone().AsObject());
    }

    private static JsonObject ReadRequiredObject(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is not JsonObject objectNode)
        {
            throw new InvalidOperationException($"The local backup payload is missing {propertyName}.");
        }

        return objectNode;
    }

    private static string ReadRequiredString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The local backup payload is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The local backup payload has an invalid {propertyName}.",
                exception);
        }
    }

    private static int ReadRequiredInt32(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) ||
            node is null)
        {
            throw new InvalidOperationException($"The local backup payload is missing {propertyName}.");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The local backup payload has an invalid {propertyName}.",
                exception);
        }
    }

    private static TEnum ReadRequiredEnum<TEnum>(
        JsonObject jsonObject,
        string propertyName)
        where TEnum : struct, Enum
    {
        var value = ReadRequiredString(jsonObject, propertyName);
        if (!Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed))
        {
            throw new InvalidOperationException(
                $"The local backup payload has an invalid {propertyName}.");
        }

        return parsed;
    }
}
