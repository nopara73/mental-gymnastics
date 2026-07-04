using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalMaintenanceCheckStoreTests : IDisposable
{
    private const string FocusHoldL2MaintenanceStandard =
        "Standard is met at one level below the owned level or at the owned level with reduced volume; critical constraints remain intact.";

    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadReturnsNullAndHistoriesAreEmptyWhenNoMaintenanceChecksHaveBeenSaved()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        var maintenance = await store.LoadMaintenanceAsync("missing-maintenance");
        var restoration = await store.LoadRestorationAsync("missing-restoration");
        var maintenanceHistory = await store.ListMaintenanceAsync();
        var restorationHistory = await store.ListRestorationAsync();
        var request = await store.LoadMaintenanceCurrencyRequestAsync(
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 8));
        var restorationEvidence = await store.LoadRestorationEvidenceAsync(BranchCode.FH, GlobalLevelId.L2);

        Assert.Null(maintenance);
        Assert.Null(restoration);
        Assert.Empty(maintenanceHistory);
        Assert.Empty(restorationHistory);
        Assert.Empty(request.Checks);
        Assert.Empty(restorationEvidence.Checks);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task SaveLoadAndListRoundTripMaintenancePassAndFailureRecordsAfterRestart()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var pass = MaintenanceRecord(
            "fh-l2-maintenance-pass",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 1),
            passed: true,
            evidenceArtifactId: "artifact-fh-l2-maintenance-pass",
            completedSessionId: "session-fh-l2-maintenance-pass");
        var warningRelevantFailure = MaintenanceRecord(
            "fh-l2-maintenance-warning-failure",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 8),
            passed: false,
            evidenceArtifactId: "artifact-fh-l2-maintenance-failure");

        await CreateStore(databasePath).SaveMaintenanceAsync(pass);
        await CreateStore(databasePath).SaveMaintenanceAsync(warningRelevantFailure);

        var loadedPass = await CreateStore(databasePath).LoadMaintenanceAsync(pass.CheckId);
        var loadedFailure = await CreateStore(databasePath).LoadMaintenanceAsync(warningRelevantFailure.CheckId);
        var history = await CreateStore(databasePath).ListMaintenanceAsync();

        Assert.NotNull(loadedPass);
        Assert.NotNull(loadedFailure);
        AssertEquivalent(pass, loadedPass);
        AssertEquivalent(warningRelevantFailure, loadedFailure);
        Assert.Equal(["fh-l2-maintenance-pass", "fh-l2-maintenance-warning-failure"], history.Select(record => record.CheckId));
    }

    [Fact]
    public async Task ListMaintenanceByBranchLevelReturnsOnlyMatchingChecksInDateOrder()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var latest = MaintenanceRecord("fh-l2-latest", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 8), passed: false);
        var otherBranch = MaintenanceRecord("wm-l2", BranchCode.WM, GlobalLevelId.L2, TrainingDate.From(2026, 7, 2), passed: true);
        var earliest = MaintenanceRecord("fh-l2-earliest", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1), passed: true);

        await store.SaveMaintenanceAsync(latest);
        await store.SaveMaintenanceAsync(otherBranch);
        await store.SaveMaintenanceAsync(earliest);

        var focusHoldHistory = await CreateStore(databasePath).ListMaintenanceByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L2);
        var workingMemoryHistory = await CreateStore(databasePath).ListMaintenanceByBranchLevelAsync(BranchCode.WM, GlobalLevelId.L2);

        Assert.Equal(["fh-l2-earliest", "fh-l2-latest"], focusHoldHistory.Select(record => record.CheckId));
        Assert.Equal("wm-l2", Assert.Single(workingMemoryHistory).CheckId);
    }

    [Fact]
    public async Task LoadedMaintenanceHistoryFeedsCoreCurrencyEvaluatorForCurrentDueWarningAndFailedCases()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveMaintenanceAsync(MaintenanceRecord("fh-current", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1), passed: true));
        await store.SaveMaintenanceAsync(MaintenanceRecord("wm-overdue", BranchCode.WM, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1), passed: true));
        await store.SaveMaintenanceAsync(MaintenanceRecord("de-pass", BranchCode.DE, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1), passed: true));
        await store.SaveMaintenanceAsync(MaintenanceRecord("de-warning", BranchCode.DE, GlobalLevelId.L3, TrainingDate.From(2026, 7, 8), passed: false));
        await store.SaveMaintenanceAsync(MaintenanceRecord("ir-pass", BranchCode.IR, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1), passed: true));
        await store.SaveMaintenanceAsync(MaintenanceRecord("ir-fail-one", BranchCode.IR, GlobalLevelId.L3, TrainingDate.From(2026, 7, 8), passed: false));
        await store.SaveMaintenanceAsync(MaintenanceRecord("ir-fail-two", BranchCode.IR, GlobalLevelId.L3, TrainingDate.From(2026, 7, 9), passed: false));

        var current = MaintenanceCurrencyEvaluator.Evaluate(
            await CreateStore(databasePath).LoadMaintenanceCurrencyRequestAsync(
                BranchCode.FH,
                GlobalLevelId.L2,
                TrainingDate.From(2026, 7, 7)));
        var due = MaintenanceCurrencyEvaluator.Evaluate(
            await CreateStore(databasePath).LoadMaintenanceCurrencyRequestAsync(
                BranchCode.WM,
                GlobalLevelId.L2,
                TrainingDate.From(2026, 7, 9)));
        var warning = MaintenanceCurrencyEvaluator.Evaluate(
            await CreateStore(databasePath).LoadMaintenanceCurrencyRequestAsync(
                BranchCode.DE,
                GlobalLevelId.L3,
                TrainingDate.From(2026, 7, 8)));
        var failed = MaintenanceCurrencyEvaluator.Evaluate(
            await CreateStore(databasePath).LoadMaintenanceCurrencyRequestAsync(
                BranchCode.IR,
                GlobalLevelId.L3,
                TrainingDate.From(2026, 7, 9)));

        Assert.Equal(MaintenanceCurrencyState.Current, current.State);
        Assert.Equal(MaintenanceCurrencyState.Due, due.State);
        Assert.Equal(MaintenanceCurrencyState.Warning, warning.State);
        Assert.Equal(1, warning.ConsecutiveFailures);
        Assert.Equal(MaintenanceCurrencyState.Failed, failed.State);
        Assert.Equal(2, failed.ConsecutiveFailures);
    }

    [Fact]
    public async Task LoadedFailedMaintenanceHistoryCanBeSuppliedToCoreDecayEvaluator()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveMaintenanceAsync(MaintenanceRecord("fh-pass", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1), passed: true));
        await store.SaveMaintenanceAsync(MaintenanceRecord("fh-fail-one", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 8), passed: false));
        await store.SaveMaintenanceAsync(MaintenanceRecord("fh-fail-two", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 9), passed: false));

        var currency = MaintenanceCurrencyEvaluator.Evaluate(
            await CreateStore(databasePath).LoadMaintenanceCurrencyRequestAsync(
                BranchCode.FH,
                GlobalLevelId.L2,
                TrainingDate.From(2026, 7, 9)));
        var decay = DecayRestorationEvaluator.EvaluateDecay(
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance),
            currency);

        Assert.Equal(MaintenanceCurrencyState.Failed, currency.State);
        Assert.True(decay.ChangedState);
        Assert.Equal(BranchLevelTransition.MarkDecayed, decay.Transition);
        Assert.Equal(BranchLevelState.Decayed, decay.NextStatus.State);
    }

    [Fact]
    public async Task SaveLoadAndListRoundTripRestorationEvidenceAfterRestart()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var lastOwnedStandard = RestorationRecord(
            "fh-l2-last-owned-standard",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 10),
            RestorationCheckKind.LastOwnedStandard,
            passed: true,
            evidenceArtifactId: "artifact-last-owned-standard");
        var lowerLoadTransfer = RestorationRecord(
            "fh-l2-lower-load-transfer",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 11),
            RestorationCheckKind.LowerLoadTransferCheck,
            passed: true,
            evidenceArtifactId: "artifact-lower-load-transfer");

        await CreateStore(databasePath).SaveRestorationAsync(lastOwnedStandard);
        await CreateStore(databasePath).SaveRestorationAsync(lowerLoadTransfer);

        var loaded = await CreateStore(databasePath).LoadRestorationAsync(lowerLoadTransfer.CheckId);
        var history = await CreateStore(databasePath).ListRestorationByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L2);

        Assert.NotNull(loaded);
        AssertRestorationEquivalent(lowerLoadTransfer, loaded);
        Assert.Equal(["fh-l2-last-owned-standard", "fh-l2-lower-load-transfer"], history.Select(record => record.CheckId));
    }

    [Fact]
    public async Task LoadedRestorationEvidenceCanBeSuppliedToCoreRestorationEvaluator()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveRestorationAsync(RestorationRecord(
            "fh-l2-last-owned-standard",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 10),
            RestorationCheckKind.LastOwnedStandard,
            passed: true));
        await store.SaveRestorationAsync(RestorationRecord(
            "fh-l2-lower-load-transfer",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 11),
            RestorationCheckKind.LowerLoadTransferCheck,
            passed: true));

        var evidence = await CreateStore(databasePath).LoadRestorationEvidenceAsync(BranchCode.FH, GlobalLevelId.L2);
        var restoration = DecayRestorationEvaluator.EvaluateRestoration(
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Decayed),
            evidence);

        Assert.Equal(2, evidence.Checks.Count);
        Assert.True(restoration.ChangedState);
        Assert.Equal(BranchLevelTransition.RestoreToMaintenance, restoration.Transition);
        Assert.Equal(BranchLevelState.Maintenance, restoration.NextStatus.State);
    }

    [Fact]
    public async Task SaveReplacesMaintenanceAndRestorationChecksWithSameIdentifierWithoutDuplicatingHistory()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var maintenanceOriginal = MaintenanceRecord("same-maintenance", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1), passed: true);
        var maintenanceReplacement = MaintenanceRecord("same-maintenance", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 8), passed: false);
        var restorationOriginal = RestorationRecord(
            "same-restoration",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 10),
            RestorationCheckKind.LastOwnedStandard,
            passed: false);
        var restorationReplacement = RestorationRecord(
            "same-restoration",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 11),
            RestorationCheckKind.LowerLoadTransferCheck,
            passed: true);

        await store.SaveMaintenanceAsync(maintenanceOriginal);
        await store.SaveMaintenanceAsync(maintenanceReplacement);
        await store.SaveRestorationAsync(restorationOriginal);
        await store.SaveRestorationAsync(restorationReplacement);

        var maintenanceHistory = await CreateStore(databasePath).ListMaintenanceAsync();
        var restorationHistory = await CreateStore(databasePath).ListRestorationAsync();
        var loadedMaintenance = await CreateStore(databasePath).LoadMaintenanceAsync("same-maintenance");
        var loadedRestoration = await CreateStore(databasePath).LoadRestorationAsync("same-restoration");

        Assert.Single(maintenanceHistory);
        Assert.Single(restorationHistory);
        Assert.NotNull(loadedMaintenance);
        Assert.NotNull(loadedRestoration);
        AssertEquivalent(maintenanceReplacement, loadedMaintenance);
        AssertRestorationEquivalent(restorationReplacement, loadedRestoration);
    }

    [Fact]
    public async Task SavedChecksUseStableIdentifiersAndDoNotPersistDerivedMaintenanceOrDecayDecisions()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var maintenance = MaintenanceRecord(
            "fh-l2-maintenance-failure",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 8),
            passed: false,
            drill: DrillId.FH1TargetHold);
        var restoration = RestorationRecord(
            "fh-l2-lower-load-transfer",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 11),
            RestorationCheckKind.LowerLoadTransferCheck,
            passed: true,
            drill: DrillId.FH2DistractorHold);

        await CreateStore(databasePath).SaveMaintenanceAsync(maintenance);
        await CreateStore(databasePath).SaveRestorationAsync(restoration);
        var storedJson = await File.ReadAllTextAsync(databasePath);

        Assert.Contains("\"Branch\": \"FH\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"OwnedLevel\": \"L2\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"LastOwnedLevel\": \"L2\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Kind\": \"StandardOrTransfer\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Kind\": \"LowerLoadTransferCheck\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Drill\": \"FH1TargetHold\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Drill\": \"FH2DistractorHold\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"FailureKind\": \"CriticalConstraintBroken\"", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Focus Hold", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("MaintenanceCurrencyState", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ConsecutiveFailures", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("DaysSinceLastPassingCheck", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("BranchLevelTransition", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("MarkDecayed", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreToMaintenance", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Account", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Telemetry", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Notification", storedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckRecordsRequireEvidenceArtifactReferences()
    {
        var maintenanceException = Assert.Throws<ArgumentException>(() =>
            MaintenanceRecord("missing-maintenance-artifact", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 1), passed: true, evidenceArtifactId: " "));
        var restorationException = Assert.Throws<ArgumentException>(() =>
            RestorationRecord("missing-restoration-artifact", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 10), RestorationCheckKind.LastOwnedStandard, passed: true, evidenceArtifactId: " "));

        Assert.Contains("Evidence artifact", maintenanceException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Evidence artifact", restorationException.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalMaintenanceCheckStore CreateStore(string databasePath)
    {
        return new LocalMaintenanceCheckStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static LocalMaintenanceCheckRecord MaintenanceRecord(
        string checkId,
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate date,
        bool passed,
        MaintenanceCheckKind kind = MaintenanceCheckKind.StandardOrTransfer,
        DrillId? drill = DrillId.FH1TargetHold,
        string standard = FocusHoldL2MaintenanceStandard,
        string evidenceArtifactId = "artifact-default",
        string? completedSessionId = null)
    {
        return new LocalMaintenanceCheckRecord(
            checkId,
            evidenceArtifactId,
            completedSessionId,
            drill,
            standard,
            new MaintenanceCheckEvidence(
                branch,
                ownedLevel,
                date,
                kind,
                StandardResult(passed)));
    }

    private static LocalRestorationCheckRecord RestorationRecord(
        string checkId,
        BranchCode branch,
        GlobalLevelId lastOwnedLevel,
        TrainingDate date,
        RestorationCheckKind kind,
        bool passed,
        DrillId? drill = DrillId.FH1TargetHold,
        string standard = FocusHoldL2MaintenanceStandard,
        string evidenceArtifactId = "artifact-default",
        string? completedSessionId = null)
    {
        return new LocalRestorationCheckRecord(
            checkId,
            evidenceArtifactId,
            completedSessionId,
            drill,
            standard,
            new RestorationCheckEvidence(
                branch,
                lastOwnedLevel,
                date,
                kind,
                StandardResult(passed)));
    }

    private static StandardEvaluationResult StandardResult(bool passed)
    {
        return passed
            ? new StandardEvaluationResult(true, [])
            : new StandardEvaluationResult(
                false,
                [new StandardEvaluationFailure(StandardFailureKind.CriticalConstraintBroken, "critical constraint failed")]);
    }

    private static void AssertEquivalent(
        LocalMaintenanceCheckRecord expected,
        LocalMaintenanceCheckRecord actual)
    {
        Assert.Equal(expected.CheckId, actual.CheckId);
        Assert.Equal(expected.EvidenceArtifactId, actual.EvidenceArtifactId);
        Assert.Equal(expected.CompletedSessionId, actual.CompletedSessionId);
        Assert.Equal(expected.Drill, actual.Drill);
        Assert.Equal(expected.Standard, actual.Standard);
        AssertMaintenanceEvidenceEquivalent(expected.Evidence, actual.Evidence);
    }

    private static void AssertRestorationEquivalent(
        LocalRestorationCheckRecord expected,
        LocalRestorationCheckRecord actual)
    {
        Assert.Equal(expected.CheckId, actual.CheckId);
        Assert.Equal(expected.EvidenceArtifactId, actual.EvidenceArtifactId);
        Assert.Equal(expected.CompletedSessionId, actual.CompletedSessionId);
        Assert.Equal(expected.Drill, actual.Drill);
        Assert.Equal(expected.Standard, actual.Standard);
        AssertRestorationEvidenceEquivalent(expected.Evidence, actual.Evidence);
    }

    private static void AssertMaintenanceEvidenceEquivalent(
        MaintenanceCheckEvidence expected,
        MaintenanceCheckEvidence actual)
    {
        Assert.Equal(expected.Branch, actual.Branch);
        Assert.Equal(expected.OwnedLevel, actual.OwnedLevel);
        Assert.Equal(expected.Date, actual.Date);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.StandardEvaluationResult.Passed, actual.StandardEvaluationResult.Passed);
        Assert.Equal(expected.StandardEvaluationResult.Failures, actual.StandardEvaluationResult.Failures);
    }

    private static void AssertRestorationEvidenceEquivalent(
        RestorationCheckEvidence expected,
        RestorationCheckEvidence actual)
    {
        Assert.Equal(expected.Branch, actual.Branch);
        Assert.Equal(expected.LastOwnedLevel, actual.LastOwnedLevel);
        Assert.Equal(expected.Date, actual.Date);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.StandardEvaluationResult.Passed, actual.StandardEvaluationResult.Passed);
        Assert.Equal(expected.StandardEvaluationResult.Failures, actual.StandardEvaluationResult.Failures);
    }
}
