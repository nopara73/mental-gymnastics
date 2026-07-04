using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalBackupServiceTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExportAndRestorePreserveOfflineProgrammingStateAndHistory()
    {
        var sourcePath = DatabasePath("source.db");
        var restorePath = DatabasePath("restored.db");
        await SeedProgrammingDataAsync(sourcePath);

        var backup = await BackupService(sourcePath).ExportAsync();
        var importedBackup = new LocalBackupPackage(backup.Payload);
        await BackupService(restorePath).RestoreAsync(importedBackup);

        Assert.Equal(LocalDatabaseStorageOwnership.AppOwned, backup.StorageOwnership);
        Assert.Equal(LocalDatabaseConnectivity.OfflineOnly, backup.Connectivity);
        Assert.Equal(LocalDatabaseSchema.CurrentVersion, backup.DatabaseSchemaVersion);
        Assert.Contains("MentalGymnastics.LocalBackup", backup.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Sync", backup.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Telemetry", backup.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Analytics", backup.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Account", backup.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Backend", backup.Payload, StringComparison.OrdinalIgnoreCase);

        var restoredState = await new LocalPractitionerStateStore(Options(restorePath)).LoadAsync();
        var restoredBranchLevels = await new LocalBranchLevelStateStore(Options(restorePath)).LoadAllAsync();
        var restoredSession = await new LocalSessionHistoryStore(Options(restorePath)).LoadAsync("session-fh-l1");
        var restoredArtifact = await new LocalEvidenceArtifactStore(Options(restorePath)).LoadAsync("artifact-fh-l1");
        var restoredAttempt = await new LocalFormalTestAttemptStore(Options(restorePath)).LoadAsync("attempt-fh-l1");
        var restoredStabilization = await new LocalStabilizationPassStore(Options(restorePath)).LoadAsync("stabilization-fh-l1");
        var restoredMaintenance = await new LocalMaintenanceCheckStore(Options(restorePath)).LoadMaintenanceAsync("maintenance-fh-l1");
        var restoredRestorationCheck = await new LocalMaintenanceCheckStore(Options(restorePath)).LoadRestorationAsync("restoration-lower-load-fh-l1");
        var restoredDecay = await new LocalDecayRestorationHistoryStore(Options(restorePath)).LoadDecayAsync("decay-fh-l1");
        var restoredRestoration = await new LocalDecayRestorationHistoryStore(Options(restorePath)).LoadRestorationAsync("restoration-fh-l1");
        var restoredGeneratedInstance = await new LocalGeneratedDrillInstanceStore(Options(restorePath)).LoadAsync("instance-fh-l1");
        var restoredSummary = await new LocalProgressSummaryStore(Options(restorePath)).LoadAsync("summary-20260704");

        Assert.NotNull(restoredState);
        Assert.Equal(BranchLevelState.Maintenance, restoredState.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.Contains(restoredBranchLevels, status =>
            status.Branch == BranchCode.FH &&
            status.Level == GlobalLevelId.L1 &&
            status.State == BranchLevelState.Maintenance);
        Assert.NotNull(restoredSession);
        Assert.Equal(["artifact-fh-l1"], restoredSession.EvidenceArtifactIds);
        Assert.NotNull(restoredArtifact);
        Assert.Equal(EvidenceArtifactCategory.Practice, restoredArtifact.Artifact.Category);
        Assert.NotNull(restoredAttempt);
        Assert.Equal(FormalTestPassState.PassOnce, restoredAttempt.Attempt.PassState);
        Assert.NotNull(restoredStabilization);
        Assert.Equal(LocalStabilizationCondition.AdjacentWork, restoredStabilization.Condition);
        Assert.NotNull(restoredMaintenance);
        Assert.True(restoredMaintenance.Evidence.Passed);
        Assert.NotNull(restoredRestorationCheck);
        Assert.Equal(RestorationCheckKind.LowerLoadTransferCheck, restoredRestorationCheck.Evidence.Kind);
        Assert.NotNull(restoredDecay);
        Assert.Equal(BranchLevelState.Decayed, restoredDecay.NextStatus.State);
        Assert.NotNull(restoredRestoration);
        Assert.Equal(BranchLevelState.Maintenance, restoredRestoration.NextStatus.State);
        Assert.NotNull(restoredGeneratedInstance);
        Assert.True(restoredGeneratedInstance.CanBeReused);
        Assert.NotNull(restoredSummary);
        Assert.False(restoredSummary.IsAuthoritative);
    }

    [Fact]
    public async Task RestoreRejectsInvalidBackupWithoutReplacingExistingLocalData()
    {
        var targetPath = DatabasePath("target.db");
        await new LocalPractitionerStateStore(Options(targetPath)).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
            ]));
        var originalJson = await File.ReadAllTextAsync(targetPath);
        var invalidBackup = new LocalBackupPackage(
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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await BackupService(targetPath).RestoreAsync(invalidBackup));

        Assert.Contains("backup", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalJson, await File.ReadAllTextAsync(targetPath));
        var state = await new LocalPractitionerStateStore(Options(targetPath)).LoadAsync();
        Assert.NotNull(state);
        Assert.Equal(BranchLevelState.Training, state.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
    }

    [Fact]
    public async Task RestoreRejectsBackupWithBrokenPersistenceIntegrityWithoutReplacingExistingLocalData()
    {
        var sourcePath = DatabasePath("source-corrupted.db");
        var targetPath = DatabasePath("target-corrupted.db");
        await SeedProgrammingDataAsync(sourcePath);
        await new LocalPractitionerStateStore(Options(targetPath)).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
            ]));
        var originalJson = await File.ReadAllTextAsync(targetPath);
        var backup = await BackupService(sourcePath).ExportAsync();
        var corruptedBackup = CorruptBackup(backup, data =>
        {
            ((JsonObject)data["FormalTestAttempts"]![0]!)["EvidenceArtifactId"] = "missing-artifact";
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await BackupService(targetPath).RestoreAsync(corruptedBackup));

        Assert.Contains("integrity", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalJson, await File.ReadAllTextAsync(targetPath));
        var state = await new LocalPractitionerStateStore(Options(targetPath)).LoadAsync();
        Assert.NotNull(state);
        Assert.Equal(BranchLevelState.Training, state.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
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

    private static LocalDatabaseOptions Options(string databasePath)
    {
        return LocalDatabaseOptions.ForAppOwnedPath(databasePath);
    }

    private static LocalBackupService BackupService(string databasePath)
    {
        return new LocalBackupService(Options(databasePath));
    }

    private static async ValueTask SeedProgrammingDataAsync(string databasePath)
    {
        await new LocalPractitionerStateStore(Options(databasePath)).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
                new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training),
            ]));
        await new LocalBranchLevelStateStore(Options(databasePath)).SaveAllAsync(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training),
        ]);
        await new LocalSessionHistoryStore(Options(databasePath)).SaveAsync(SessionRecord());
        await SaveArtifactAsync(databasePath, "artifact-fh-l1", "session-fh-l1", LocalProgrammingEventKind.Practice, EvidenceArtifactCategory.Practice);
        await SaveArtifactAsync(databasePath, "artifact-attempt-fh-l1", "attempt-fh-l1", LocalProgrammingEventKind.FormalTest, EvidenceArtifactCategory.Test);
        await SaveArtifactAsync(databasePath, "artifact-stabilization-fh-l1", "stabilization-fh-l1", LocalProgrammingEventKind.Stabilization, EvidenceArtifactCategory.Stabilization);
        await SaveArtifactAsync(databasePath, "artifact-maintenance-fh-l1", "maintenance-fh-l1", LocalProgrammingEventKind.Maintenance, EvidenceArtifactCategory.Maintenance);
        await SaveArtifactAsync(databasePath, "artifact-maintenance-fail-one", "maintenance-fail-one", LocalProgrammingEventKind.Maintenance, EvidenceArtifactCategory.Maintenance);
        await SaveArtifactAsync(databasePath, "artifact-maintenance-fail-two", "maintenance-fail-two", LocalProgrammingEventKind.Maintenance, EvidenceArtifactCategory.Maintenance);
        await SaveArtifactAsync(databasePath, "artifact-restoration-last-owned", "restoration-last-owned-fh-l1", LocalProgrammingEventKind.Maintenance, EvidenceArtifactCategory.Maintenance);
        await SaveArtifactAsync(databasePath, "artifact-restoration-lower-load", "restoration-lower-load-fh-l1", LocalProgrammingEventKind.Maintenance, EvidenceArtifactCategory.Maintenance);
        await new LocalFormalTestAttemptStore(Options(databasePath)).SaveAsync(AttemptRecord());
        await new LocalStabilizationPassStore(Options(databasePath)).SaveAsync(StabilizationRecord());
        await new LocalMaintenanceCheckStore(Options(databasePath)).SaveMaintenanceAsync(MaintenanceRecord());
        await new LocalMaintenanceCheckStore(Options(databasePath)).SaveMaintenanceAsync(MaintenanceFailureRecord("maintenance-fail-one", "artifact-maintenance-fail-one", TrainingDate.From(2026, 7, 7)));
        await new LocalMaintenanceCheckStore(Options(databasePath)).SaveMaintenanceAsync(MaintenanceFailureRecord("maintenance-fail-two", "artifact-maintenance-fail-two", TrainingDate.From(2026, 7, 8)));
        await new LocalMaintenanceCheckStore(Options(databasePath)).SaveRestorationAsync(RestorationCheckRecord(
            "restoration-last-owned-fh-l1",
            "artifact-restoration-last-owned",
            RestorationCheckKind.LastOwnedStandard,
            TrainingDate.From(2026, 7, 9)));
        await new LocalMaintenanceCheckStore(Options(databasePath)).SaveRestorationAsync(RestorationCheckRecord(
            "restoration-lower-load-fh-l1",
            "artifact-restoration-lower-load",
            RestorationCheckKind.LowerLoadTransferCheck,
            TrainingDate.From(2026, 7, 10)));
        await new LocalDecayRestorationHistoryStore(Options(databasePath)).SaveDecayAsync(DecayRecord());
        await new LocalDecayRestorationHistoryStore(Options(databasePath)).SaveRestorationAsync(RestorationHistoryRecord());
        await new LocalGeneratedDrillInstanceStore(Options(databasePath)).SaveAsync(GeneratedInstanceRecord());
        await new LocalProgressSummaryStore(Options(databasePath)).SaveAsync(ProgressSummaryRecord());
    }

    private static async ValueTask SaveArtifactAsync(
        string databasePath,
        string artifactId,
        string eventId,
        LocalProgrammingEventKind eventKind,
        EvidenceArtifactCategory category)
    {
        await new LocalEvidenceArtifactStore(Options(databasePath)).SaveAsync(
            new LocalEvidenceArtifactRecord(
                artifactId,
                new LocalProgrammingEventReference(
                    eventId,
                    eventKind,
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    DrillId.FH1TargetHold),
                Artifact(category, ObservableEvidenceKind.Score, $"{artifactId}: observable evidence")));
    }

    private static LocalSessionHistoryRecord SessionRecord()
    {
        return new LocalSessionHistoryRecord(
            "session-fh-l1",
            TrainingDate.From(2026, 7, 4),
            LocalCompletedSessionType.Practice,
            [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L1)],
            DrillId.FH1TargetHold,
            null,
            LocalSessionIntensity.Moderate,
            [new LoadVariable("duration", "3 minutes")],
            cleanPerformance: true,
            notes: "clean FH L1 set with 4 marked drifts and return inside standard",
            recoveryMarked: false,
            deloadMarked: false,
            ["artifact-fh-l1"]);
    }

    private static LocalFormalTestAttemptRecord AttemptRecord()
    {
        var attempt = new FormalTestAttempt(
            BranchCode.FH,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 4),
            TestTask.ForDrill(DrillId.FH1TargetHold),
            [new LoadVariable("duration", "3 minutes")],
            "No more than 5 marked drifts; each return within 10 seconds; no target change.",
            [new CriticalConstraint("target cannot change")],
            new TestResultEvidence(TestResultEvidenceKind.PassFail, "pass"),
            null,
            FormalTestPassState.PassOnce,
                Artifact(EvidenceArtifactCategory.Test, ObservableEvidenceKind.Score, "formal test drifts: 4"));

        return new LocalFormalTestAttemptRecord("attempt-fh-l1", "artifact-attempt-fh-l1", attempt);
    }

    private static LocalStabilizationPassRecord StabilizationRecord()
    {
        return new LocalStabilizationPassRecord(
            "stabilization-fh-l1",
            "artifact-stabilization-fh-l1",
            "attempt-fh-l1",
            null,
            DrillId.FH1TargetHold,
            LocalStabilizationCondition.AdjacentWork,
            "after short WM set",
            new StabilizationPassEvidence(
                BranchCode.FH,
                GlobalLevelId.L1,
                TrainingDate.From(2026, 7, 6),
                "No more than 5 marked drifts; each return within 10 seconds; no target change.",
                FormalTestPassState.StabilizationPass,
                PassedEvaluation(),
                afterAdjacentWorkOrControlledDistractor: true,
                mainFailureModeAvoided: "unmarked drift"));
    }

    private static LocalMaintenanceCheckRecord MaintenanceRecord()
    {
        return new LocalMaintenanceCheckRecord(
            "maintenance-fh-l1",
            "artifact-maintenance-fh-l1",
            null,
            DrillId.FH1TargetHold,
            "FH L1 maintenance check at reduced volume.",
            new MaintenanceCheckEvidence(
                BranchCode.FH,
                GlobalLevelId.L1,
                TrainingDate.From(2026, 7, 7),
                MaintenanceCheckKind.StandardOrTransfer,
                PassedEvaluation()));
    }

    private static LocalMaintenanceCheckRecord MaintenanceFailureRecord(
        string checkId,
        string artifactId,
        TrainingDate date)
    {
        return new LocalMaintenanceCheckRecord(
            checkId,
            artifactId,
            null,
            DrillId.FH1TargetHold,
            "FH L1 failed maintenance check with preserved constraint.",
            new MaintenanceCheckEvidence(
                BranchCode.FH,
                GlobalLevelId.L1,
                date,
                MaintenanceCheckKind.StandardOrTransfer,
                FailedEvaluation()));
    }

    private static LocalRestorationCheckRecord RestorationCheckRecord(
        string checkId,
        string artifactId,
        RestorationCheckKind kind,
        TrainingDate date)
    {
        return new LocalRestorationCheckRecord(
            checkId,
            artifactId,
            null,
            DrillId.FH1TargetHold,
            "Lower-load transfer check preserves FH standard.",
            new RestorationCheckEvidence(
                BranchCode.FH,
                GlobalLevelId.L1,
                date,
                kind,
                PassedEvaluation()));
    }

    private static LocalDecayHistoryRecord DecayRecord()
    {
        return new LocalDecayHistoryRecord(
            "decay-fh-l1",
            TrainingDate.From(2026, 7, 8),
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Decayed),
            BranchLevelTransition.MarkDecayed,
            ["maintenance-fail-one", "maintenance-fail-two"]);
    }

    private static LocalRestorationHistoryRecord RestorationHistoryRecord()
    {
        var current = new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Decayed);
        var evidence = new RestorationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [
                new RestorationCheckEvidence(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    TrainingDate.From(2026, 7, 9),
                    RestorationCheckKind.LastOwnedStandard,
                    PassedEvaluation()),
                new RestorationCheckEvidence(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    TrainingDate.From(2026, 7, 10),
                    RestorationCheckKind.LowerLoadTransferCheck,
                    PassedEvaluation()),
            ]);
        var result = DecayRestorationEvaluator.EvaluateRestoration(current, evidence);

        return new LocalRestorationHistoryRecord(
            "restoration-fh-l1",
            TrainingDate.From(2026, 7, 10),
            result.CurrentStatus,
            result.NextStatus,
            result.Transition!.Value,
            ["restoration-last-owned-fh-l1", "restoration-lower-load-fh-l1"],
            evidence);
    }

    private static LocalGeneratedDrillInstanceRecord GeneratedInstanceRecord()
    {
        return new LocalGeneratedDrillInstanceRecord(
            "instance-fh-l1",
            TrainingDate.From(2026, 7, 4),
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")],
            new LocalGeneratedDrillContentIdentity(
                "content-fh-l1-001",
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold,
                PromptContentKind.CueSequence,
                "FH1-target-hold",
                "v1"));
    }

    private static LocalProgressSummaryRecord ProgressSummaryRecord()
    {
        return new LocalProgressSummaryRecord(
            "summary-20260704",
            TrainingDate.From(2026, 7, 4),
            TrainingDate.From(2026, 6, 28),
            TrainingDate.From(2026, 7, 4),
            isAuthoritative: false,
            branchSummaries:
            [
                new LocalBranchProgressSummary(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
            ],
            maintenanceSummaries:
            [
                new LocalMaintenanceProgressSummary(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    MaintenanceCurrencyState.Current,
                    "maintenance-fh-l1",
                    DaysSinceLastPassingCheck: 0,
                    ConsecutiveFailures: 0),
            ],
            activeBlockers: [],
            bottleneckBranch: null,
            new LocalProgrammedEmphasis(LocalProgressEmphasisKind.ContinueMaintenance, BranchCode.FH, GlobalLevelId.L1),
            completedSessionCount: 1,
            formalAttemptCount: 1,
            evidenceArtifactCount: 1,
            sourceReferences:
            [
                new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.PractitionerState, "PractitionerState"),
                new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.CompletedSession, "session-fh-l1"),
            ]);
    }

    private static EvidenceArtifact Artifact(
        EvidenceArtifactCategory category,
        ObservableEvidenceKind kind,
        string evidence)
    {
        return new EvidenceArtifact(
            category,
            TrainingDate.From(2026, 7, 4),
            [new ObservableEvidence(kind, evidence)],
            evidence);
    }

    private static StandardEvaluationResult PassedEvaluation()
    {
        return new StandardEvaluationResult(true, []);
    }

    private static StandardEvaluationResult FailedEvaluation()
    {
        return new StandardEvaluationResult(
            false,
            [new StandardEvaluationFailure(StandardFailureKind.CriticalConstraintBroken, "Maintenance standard failed.")]);
    }

    private static LocalBackupPackage CorruptBackup(
        LocalBackupPackage backup,
        Action<JsonObject> mutateData)
    {
        var envelope = JsonNode.Parse(backup.Payload)?.AsObject();
        Assert.NotNull(envelope);
        var data = envelope["Data"]?.AsObject();
        Assert.NotNull(data);
        mutateData(data);

        return new LocalBackupPackage(envelope.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
    }
}
