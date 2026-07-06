using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App.Tests;

public sealed class LocalDataBackupWorkflowServiceTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExportsValidatesAndRestoresLocalBackupWithExplicitConfirmation()
    {
        var databasePath = DatabasePath("local.json");
        var backupDirectory = BackupDirectory();
        var configuration = Configuration(databasePath);
        var service = new LocalDataBackupWorkflowService(configuration, backupDirectory);
        await SaveStateAsync(
            configuration,
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance));

        var exported = await service.ExportAsync(
            new LocalDataBackupExportRequest(new DateTimeOffset(2026, 7, 5, 12, 30, 0, TimeSpan.Zero)));

        Assert.Equal(LocalDataBackupOperationStatus.Succeeded, exported.Status);
        Assert.NotNull(exported.BackupFile);
        Assert.True(File.Exists(exported.BackupFile.FilePath));
        Assert.Equal(LocalDatabaseStorageOwnership.AppOwned, exported.BackupFile.StorageOwnership);
        Assert.Equal(LocalDatabaseConnectivity.OfflineOnly, exported.BackupFile.Connectivity);
        Assert.DoesNotContain(
            "Sync",
            await File.ReadAllTextAsync(exported.BackupFile.FilePath),
            StringComparison.OrdinalIgnoreCase);

        await SaveStateAsync(
            configuration,
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training));

        var validation = await service.ValidateLatestBackupAsync();
        Assert.Equal(LocalDataBackupOperationStatus.Succeeded, validation.Status);

        var unconfirmed = await service.RestoreLatestBackupAsync(
            new LocalDataBackupRestoreRequest(confirmReplaceLocalData: false));
        Assert.Equal(LocalDataBackupOperationStatus.ConfirmationRequired, unconfirmed.Status);
        Assert.Equal(
            BranchLevelState.Training,
            (await LoadStateAsync(configuration))!.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));

        var restored = await service.RestoreLatestBackupAsync(
            new LocalDataBackupRestoreRequest(confirmReplaceLocalData: true));

        Assert.Equal(LocalDataBackupOperationStatus.Succeeded, restored.Status);
        Assert.True(restored.CurrentIntegrity!.IsValid);
        Assert.Equal(
            BranchLevelState.Maintenance,
            (await LoadStateAsync(configuration))!.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
    }

    [Fact]
    public async Task InvalidLocalBackupRestoreDoesNotReplaceCurrentProgressState()
    {
        var databasePath = DatabasePath("invalid-target.json");
        var backupDirectory = BackupDirectory();
        var configuration = Configuration(databasePath);
        await SaveStateAsync(
            configuration,
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training));
        Directory.CreateDirectory(backupDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(backupDirectory, "mental-gymnastics-local-backup-20260705-123000.json"),
            """
            {
              "Kind": "MentalGymnastics.CloudSync",
              "BackupSchemaVersion": 1,
              "DatabaseSchemaVersion": 1,
              "StorageOwnership": "AppOwned",
              "Connectivity": "OfflineOnly",
              "Data": {}
            }
            """);

        var restored = await new LocalDataBackupWorkflowService(configuration, backupDirectory)
            .RestoreLatestBackupAsync(new LocalDataBackupRestoreRequest(confirmReplaceLocalData: true));

        Assert.Equal(LocalDataBackupOperationStatus.Failed, restored.Status);
        Assert.Equal(
            BranchLevelState.Training,
            (await LoadStateAsync(configuration))!.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string DatabasePath(string fileName)
    {
        Directory.CreateDirectory(tempDirectory);
        return Path.Combine(tempDirectory, fileName);
    }

    private string BackupDirectory()
    {
        return Path.Combine(tempDirectory, "backups");
    }

    private static AppStartupConfiguration Configuration(string databasePath)
    {
        return AppStartupConfiguration.ForAppOwnedLocalStoragePath(databasePath);
    }

    private static ValueTask SaveStateAsync(
        AppStartupConfiguration configuration,
        params BranchLevelStatus[] statuses)
    {
        return new LocalPractitionerStateStore(configuration.LocalDatabaseOptions)
            .SaveAsync(new PractitionerState(statuses));
    }

    private static ValueTask<PractitionerState?> LoadStateAsync(AppStartupConfiguration configuration)
    {
        return new LocalPractitionerStateStore(configuration.LocalDatabaseOptions)
            .LoadAsync();
    }
}
