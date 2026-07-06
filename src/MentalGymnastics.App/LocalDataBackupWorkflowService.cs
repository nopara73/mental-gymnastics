using System.Globalization;
using System.Text.Json;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App;

public enum LocalDataBackupOperationKind
{
    Export,
    ValidateCurrent,
    ValidateLatestBackup,
    RestoreLatestBackup,
}

public enum LocalDataBackupOperationStatus
{
    Succeeded,
    Failed,
    NotFound,
    ConfirmationRequired,
}

public sealed record LocalDataIntegrityReadModel(
    bool IsValid,
    IReadOnlyList<LocalPersistenceIntegrityIssue> Issues);

public sealed record LocalDataBackupFileReadModel(
    string FilePath,
    string FileName,
    long SizeBytes,
    DateTimeOffset LastModifiedUtc,
    bool IsReadableBackup,
    int? DatabaseSchemaVersion,
    LocalDatabaseStorageOwnership? StorageOwnership,
    LocalDatabaseConnectivity? Connectivity,
    string? Detail);

public sealed record LocalDataBackupReadModel(
    string LocalDatabasePath,
    string BackupDirectoryPath,
    LocalDataIntegrityReadModel CurrentIntegrity,
    LocalDataBackupFileReadModel? LatestBackup);

public sealed record LocalDataBackupOperationResult(
    LocalDataBackupOperationKind Kind,
    LocalDataBackupOperationStatus Status,
    string Detail,
    LocalDataBackupFileReadModel? BackupFile,
    LocalDataIntegrityReadModel? CurrentIntegrity);

public sealed class LocalDataBackupExportRequest
{
    public LocalDataBackupExportRequest(DateTimeOffset exportedAtUtc)
    {
        ExportedAtUtc = exportedAtUtc.ToUniversalTime();
    }

    public DateTimeOffset ExportedAtUtc { get; }
}

public sealed class LocalDataBackupRestoreRequest
{
    public LocalDataBackupRestoreRequest(bool confirmReplaceLocalData)
    {
        ConfirmReplaceLocalData = confirmReplaceLocalData;
    }

    public bool ConfirmReplaceLocalData { get; }
}

public sealed class LocalDataBackupWorkflowService
{
    private const string BackupFilePrefix = "mental-gymnastics-local-backup-";
    private const string BackupFileSearchPattern = BackupFilePrefix + "*.json";

    private readonly AppStartupConfiguration configuration;
    private readonly string backupDirectoryPath;
    private readonly MentalGymnasticsAppStartup startup;
    private readonly LocalBackupService backupService;
    private readonly LocalPersistenceIntegrityValidator integrityValidator;

    public LocalDataBackupWorkflowService(
        AppStartupConfiguration configuration,
        string backupDirectoryPath)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.backupDirectoryPath = NormalizeLocalDirectoryPath(backupDirectoryPath);
        startup = new MentalGymnasticsAppStartup(configuration);
        backupService = new LocalBackupService(configuration.LocalDatabaseOptions);
        integrityValidator = new LocalPersistenceIntegrityValidator(configuration.LocalDatabaseOptions);
    }

    public async ValueTask<LocalDataBackupReadModel> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await startup.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var integrity = await LoadCurrentIntegrityAsync(cancellationToken).ConfigureAwait(false);

        return new LocalDataBackupReadModel(
            configuration.LocalDatabasePath,
            backupDirectoryPath,
            integrity,
            ReadLatestBackupFile());
    }

    public async ValueTask<LocalDataBackupOperationResult> ExportAsync(
        LocalDataBackupExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await startup.InitializeAsync(cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(backupDirectoryPath);

            var backup = await backupService.ExportAsync(cancellationToken).ConfigureAwait(false);
            var backupPath = Path.Combine(
                backupDirectoryPath,
                BackupFileName(request.ExportedAtUtc));
            await File.WriteAllTextAsync(backupPath, backup.Payload, cancellationToken)
                .ConfigureAwait(false);

            return new LocalDataBackupOperationResult(
                LocalDataBackupOperationKind.Export,
                LocalDataBackupOperationStatus.Succeeded,
                "Local backup exported.",
                ReadBackupFile(backupPath),
                await LoadCurrentIntegrityAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (IsLocalDataFailure(exception))
        {
            return new LocalDataBackupOperationResult(
                LocalDataBackupOperationKind.Export,
                LocalDataBackupOperationStatus.Failed,
                exception.Message,
                null,
                await LoadCurrentIntegrityAfterFailureAsync(cancellationToken).ConfigureAwait(false));
        }
    }

    public async ValueTask<LocalDataBackupOperationResult> ValidateCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var integrity = await LoadCurrentIntegrityAsync(cancellationToken).ConfigureAwait(false);

        return new LocalDataBackupOperationResult(
            LocalDataBackupOperationKind.ValidateCurrent,
            integrity.IsValid
                ? LocalDataBackupOperationStatus.Succeeded
                : LocalDataBackupOperationStatus.Failed,
            integrity.IsValid
                ? "Current local data passed integrity validation."
                : "Current local data has integrity issues.",
            ReadLatestBackupFile(),
            integrity);
    }

    public async ValueTask<LocalDataBackupOperationResult> ValidateLatestBackupAsync(
        CancellationToken cancellationToken = default)
    {
        var latest = LatestBackupFile();
        if (latest is null)
        {
            return new LocalDataBackupOperationResult(
                LocalDataBackupOperationKind.ValidateLatestBackup,
                LocalDataBackupOperationStatus.NotFound,
                "No local backup file is available.",
                null,
                await LoadCurrentIntegrityAsync(cancellationToken).ConfigureAwait(false));
        }

        return await ValidateBackupFileAsync(latest.FullName, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalDataBackupOperationResult> RestoreLatestBackupAsync(
        LocalDataBackupRestoreRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var latest = LatestBackupFile();
        var backupFile = latest is null ? null : ReadBackupFile(latest.FullName);
        if (!request.ConfirmReplaceLocalData)
        {
            return new LocalDataBackupOperationResult(
                LocalDataBackupOperationKind.RestoreLatestBackup,
                LocalDataBackupOperationStatus.ConfirmationRequired,
                "Restore requires explicit confirmation because it replaces local data.",
                backupFile,
                await LoadCurrentIntegrityAsync(cancellationToken).ConfigureAwait(false));
        }

        if (latest is null)
        {
            return new LocalDataBackupOperationResult(
                LocalDataBackupOperationKind.RestoreLatestBackup,
                LocalDataBackupOperationStatus.NotFound,
                "No local backup file is available.",
                null,
                await LoadCurrentIntegrityAsync(cancellationToken).ConfigureAwait(false));
        }

        try
        {
            var payload = await File.ReadAllTextAsync(latest.FullName, cancellationToken)
                .ConfigureAwait(false);
            var package = new LocalBackupPackage(payload);

            await backupService.RestoreAsync(package, cancellationToken).ConfigureAwait(false);
            await startup.InitializeAsync(cancellationToken).ConfigureAwait(false);
            var integrity = await LoadCurrentIntegrityAsync(cancellationToken).ConfigureAwait(false);

            return new LocalDataBackupOperationResult(
                LocalDataBackupOperationKind.RestoreLatestBackup,
                integrity.IsValid
                    ? LocalDataBackupOperationStatus.Succeeded
                    : LocalDataBackupOperationStatus.Failed,
                integrity.IsValid
                    ? "Local backup restored after integrity validation."
                    : "Restore completed but current local data still has integrity issues.",
                ReadBackupFile(latest.FullName),
                integrity);
        }
        catch (Exception exception) when (IsLocalDataFailure(exception))
        {
            return new LocalDataBackupOperationResult(
                LocalDataBackupOperationKind.RestoreLatestBackup,
                LocalDataBackupOperationStatus.Failed,
                exception.Message,
                ReadBackupFile(latest.FullName),
                await LoadCurrentIntegrityAfterFailureAsync(cancellationToken).ConfigureAwait(false));
        }
    }

    private async ValueTask<LocalDataBackupOperationResult> ValidateBackupFileAsync(
        string backupFilePath,
        CancellationToken cancellationToken)
    {
        var backupFile = ReadBackupFile(backupFilePath);
        if (!backupFile.IsReadableBackup)
        {
            return new LocalDataBackupOperationResult(
                LocalDataBackupOperationKind.ValidateLatestBackup,
                LocalDataBackupOperationStatus.Failed,
                backupFile.Detail ?? "The local backup package is not readable.",
                backupFile,
                await LoadCurrentIntegrityAsync(cancellationToken).ConfigureAwait(false));
        }

        var validationPath = Path.Combine(
            backupDirectoryPath,
            $".validation-{Guid.NewGuid():N}.json");
        try
        {
            var payload = await File.ReadAllTextAsync(backupFilePath, cancellationToken)
                .ConfigureAwait(false);
            var package = new LocalBackupPackage(payload);
            await new LocalBackupService(LocalDatabaseOptions.ForAppOwnedPath(validationPath))
                .RestoreAsync(package, cancellationToken)
                .ConfigureAwait(false);

            return new LocalDataBackupOperationResult(
                LocalDataBackupOperationKind.ValidateLatestBackup,
                LocalDataBackupOperationStatus.Succeeded,
                "Backup package passed restore integrity validation.",
                ReadBackupFile(backupFilePath),
                await LoadCurrentIntegrityAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (IsLocalDataFailure(exception))
        {
            return new LocalDataBackupOperationResult(
                LocalDataBackupOperationKind.ValidateLatestBackup,
                LocalDataBackupOperationStatus.Failed,
                exception.Message,
                ReadBackupFile(backupFilePath),
                await LoadCurrentIntegrityAfterFailureAsync(cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            DeleteIfExists(validationPath);
        }
    }

    private async ValueTask<LocalDataIntegrityReadModel> LoadCurrentIntegrityAsync(
        CancellationToken cancellationToken)
    {
        await startup.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var report = await integrityValidator.ValidateAsync(cancellationToken).ConfigureAwait(false);
        return new LocalDataIntegrityReadModel(report.IsValid, report.Issues);
    }

    private async ValueTask<LocalDataIntegrityReadModel?> LoadCurrentIntegrityAfterFailureAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await LoadCurrentIntegrityAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsLocalDataFailure(exception))
        {
            return new LocalDataIntegrityReadModel(
                false,
                [
                    new LocalPersistenceIntegrityIssue(
                        LocalPersistenceIntegrityIssueKind.InvalidDocument,
                        "Database",
                        null,
                        exception.Message),
                ]);
        }
    }

    private LocalDataBackupFileReadModel? ReadLatestBackupFile()
    {
        var latest = LatestBackupFile();
        return latest is null ? null : ReadBackupFile(latest.FullName);
    }

    private FileInfo? LatestBackupFile()
    {
        Directory.CreateDirectory(backupDirectoryPath);

        return new DirectoryInfo(backupDirectoryPath)
            .EnumerateFiles(BackupFileSearchPattern)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static LocalDataBackupFileReadModel ReadBackupFile(string backupFilePath)
    {
        var info = new FileInfo(backupFilePath);
        try
        {
            var package = new LocalBackupPackage(File.ReadAllText(backupFilePath));
            return new LocalDataBackupFileReadModel(
                info.FullName,
                info.Name,
                info.Exists ? info.Length : 0,
                info.Exists ? new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero) : DateTimeOffset.MinValue,
                IsReadableBackup: true,
                package.DatabaseSchemaVersion,
                package.StorageOwnership,
                package.Connectivity,
                "Readable local backup package.");
        }
        catch (Exception exception) when (IsLocalDataFailure(exception))
        {
            return new LocalDataBackupFileReadModel(
                info.FullName,
                info.Name,
                info.Exists ? info.Length : 0,
                info.Exists ? new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero) : DateTimeOffset.MinValue,
                IsReadableBackup: false,
                DatabaseSchemaVersion: null,
                StorageOwnership: null,
                Connectivity: null,
                exception.Message);
        }
    }

    private static string BackupFileName(DateTimeOffset exportedAtUtc)
    {
        return BackupFilePrefix +
            exportedAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) +
            ".json";
    }

    private static string NormalizeLocalDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A local backup directory path is required.", nameof(path));
        }

        if (LooksLikeRemotePath(path))
        {
            throw new ArgumentException("Local backup directory must not be a remote or sync endpoint.", nameof(path));
        }

        return Path.GetFullPath(path);
    }

    private static bool LooksLikeRemotePath(string path)
    {
        return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("s3://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("gs://", StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool IsLocalDataFailure(Exception exception)
    {
        return exception is InvalidOperationException or
            JsonException or
            IOException or
            UnauthorizedAccessException or
            ArgumentException;
    }
}
