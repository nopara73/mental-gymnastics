using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalDecayRestorationHistoryStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadReturnsNullAndHistoriesAreEmptyWhenNoDecayOrRestorationEventsHaveBeenSaved()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        var decay = await store.LoadDecayAsync("missing-decay");
        var restoration = await store.LoadRestorationAsync("missing-restoration");
        var decayHistory = await store.ListDecaysAsync();
        var restorationHistory = await store.ListRestorationsAsync();
        var activeDecays = await store.ListActiveDecayedStatusesAsync();

        Assert.Null(decay);
        Assert.Null(restoration);
        Assert.Empty(decayHistory);
        Assert.Empty(restorationHistory);
        Assert.Empty(activeDecays);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task SaveLoadAndListRoundTripDecayHistoryAfterRestart()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var expected = DecayRecord(
            "decay-fh-l2-20260709",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 9),
            ["maintenance-fh-l2-fail-1", "maintenance-fh-l2-fail-2"]);

        await CreateStore(databasePath).SaveDecayAsync(expected);

        var loaded = await CreateStore(databasePath).LoadDecayAsync(expected.DecayId);
        var history = await CreateStore(databasePath).ListDecaysAsync();

        Assert.NotNull(loaded);
        AssertDecayEquivalent(expected, loaded);
        Assert.Single(history);
        AssertDecayEquivalent(expected, Assert.Single(history));
    }

    [Fact]
    public async Task SaveLoadAndListRoundTripRestorationHistoryWithRequiredEvidenceAfterRestart()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var expected = RestorationRecord(
            "restore-fh-l2-20260711",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 11),
            ["restoration-fh-l2-last-owned", "restoration-fh-l2-lower-load-transfer"]);

        await CreateStore(databasePath).SaveRestorationAsync(expected);

        var loaded = await CreateStore(databasePath).LoadRestorationAsync(expected.RestorationId);
        var history = await CreateStore(databasePath).ListRestorationsAsync();

        Assert.NotNull(loaded);
        AssertRestorationEquivalent(expected, loaded);
        Assert.Single(history);
        AssertRestorationEquivalent(expected, Assert.Single(history));
        Assert.Contains(loaded.Evidence.Checks, check => check.Kind == RestorationCheckKind.LastOwnedStandard && check.Passed);
        Assert.Contains(loaded.Evidence.Checks, check => check.Kind == RestorationCheckKind.LowerLoadTransferCheck && check.Passed);
    }

    [Fact]
    public async Task QueriesReturnDecayAndRestorationHistoryByBranchLevelInDateOrder()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var latestDecay = DecayRecord("fh-l2-latest", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 20));
        var otherBranchDecay = DecayRecord("wm-l3-decay", BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 10));
        var earliestDecay = DecayRecord("fh-l2-earliest", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 9));
        var latestRestoration = RestorationRecord("fh-l2-restore-latest", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 22));
        var otherBranchRestoration = RestorationRecord("wm-l3-restore", BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 12));
        var earliestRestoration = RestorationRecord("fh-l2-restore-earliest", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 11));

        await store.SaveDecayAsync(latestDecay);
        await store.SaveDecayAsync(otherBranchDecay);
        await store.SaveDecayAsync(earliestDecay);
        await store.SaveRestorationAsync(latestRestoration);
        await store.SaveRestorationAsync(otherBranchRestoration);
        await store.SaveRestorationAsync(earliestRestoration);

        var focusHoldDecays = await CreateStore(databasePath).ListDecaysByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L2);
        var workingMemoryDecays = await CreateStore(databasePath).ListDecaysByBranchLevelAsync(BranchCode.WM, GlobalLevelId.L3);
        var focusHoldRestorations = await CreateStore(databasePath).ListRestorationsByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L2);
        var workingMemoryRestorations = await CreateStore(databasePath).ListRestorationsByBranchLevelAsync(BranchCode.WM, GlobalLevelId.L3);

        Assert.Equal(["fh-l2-earliest", "fh-l2-latest"], focusHoldDecays.Select(record => record.DecayId));
        Assert.Equal("wm-l3-decay", Assert.Single(workingMemoryDecays).DecayId);
        Assert.Equal(["fh-l2-restore-earliest", "fh-l2-restore-latest"], focusHoldRestorations.Select(record => record.RestorationId));
        Assert.Equal("wm-l3-restore", Assert.Single(workingMemoryRestorations).RestorationId);
    }

    [Fact]
    public async Task ActiveDecayedStatusProjectionReflectsDecaysNotLaterRestored()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveDecayAsync(DecayRecord("fh-l2-decay", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 9)));
        await store.SaveDecayAsync(DecayRecord("wm-l3-decay", BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 10)));
        await store.SaveRestorationAsync(RestorationRecord("fh-l2-restored", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 11)));

        var activeDecays = await CreateStore(databasePath).ListActiveDecayedStatusesAsync();

        Assert.Equal([new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Decayed)], activeDecays);
    }

    [Fact]
    public async Task ActiveDecayProjectionCanFeedCoreDependencyCaps()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        await CreateStore(databasePath).SaveDecayAsync(
            DecayRecord("wm-l3-decay", BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 9)));

        var activeDecays = await CreateStore(databasePath).ListActiveDecayedStatusesAsync();
        var practitionerState = new PractitionerState(
            activeDecays.Concat(
                [
                    new BranchLevelStatus(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    new BranchLevelStatus(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ]));
        var cap = DependencyCapEvaluator.Evaluate(
            new DependencyCapRequest(
                BranchCode.CO,
                practitionerState,
                [
                    CurrentMaintenance(BranchCode.WM, GlobalLevelId.L3),
                    CurrentMaintenance(BranchCode.IR, GlobalLevelId.L3),
                    CurrentMaintenance(BranchCode.DE, GlobalLevelId.L3),
                ]));

        Assert.False(cap.CanAdvance);
        Assert.True(cap.IsCappedToMaintenanceOnly);
        Assert.Contains(
            cap.Caps,
            item => item.Reason == DependencyCapReason.DecayedPrerequisite &&
                item.PrerequisiteBranch == BranchCode.WM &&
                item.PrerequisiteLevel == GlobalLevelId.L3);
    }

    [Fact]
    public async Task SaveReplacesDecayAndRestorationEventsWithSameIdentifierWithoutDuplicatingHistory()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var originalDecay = DecayRecord("same-decay", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 9));
        var replacementDecay = DecayRecord("same-decay", BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 10));
        var originalRestoration = RestorationRecord("same-restoration", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 11));
        var replacementRestoration = RestorationRecord("same-restoration", BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 12));

        await store.SaveDecayAsync(originalDecay);
        await store.SaveDecayAsync(replacementDecay);
        await store.SaveRestorationAsync(originalRestoration);
        await store.SaveRestorationAsync(replacementRestoration);

        var decayHistory = await CreateStore(databasePath).ListDecaysAsync();
        var restorationHistory = await CreateStore(databasePath).ListRestorationsAsync();
        var loadedDecay = await CreateStore(databasePath).LoadDecayAsync("same-decay");
        var loadedRestoration = await CreateStore(databasePath).LoadRestorationAsync("same-restoration");

        Assert.Single(decayHistory);
        Assert.Single(restorationHistory);
        Assert.NotNull(loadedDecay);
        Assert.NotNull(loadedRestoration);
        AssertDecayEquivalent(replacementDecay, loadedDecay);
        AssertRestorationEquivalent(replacementRestoration, loadedRestoration);
    }

    [Fact]
    public async Task SavedHistoryUsesStableIdentifiersAndDoesNotPersistDependencyCapDecisions()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");

        await CreateStore(databasePath).SaveDecayAsync(
            DecayRecord("fh-l2-decay", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 9)));
        await CreateStore(databasePath).SaveRestorationAsync(
            RestorationRecord("fh-l2-restored", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 11)));
        var storedJson = await File.ReadAllTextAsync(databasePath);

        Assert.Contains("\"Branch\": \"FH\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Level\": \"L2\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"State\": \"Decayed\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"State\": \"Maintenance\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Transition\": \"MarkDecayed\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Transition\": \"RestoreToMaintenance\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Kind\": \"LastOwnedStandard\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Kind\": \"LowerLoadTransferCheck\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"FailureKind\": \"CriticalConstraintBroken\"", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Focus Hold", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("DependencyCap", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("CanAdvance", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCappedToMaintenanceOnly", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Account", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Telemetry", storedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoryRecordsRequireEvidenceReferences()
    {
        var decayException = Assert.Throws<ArgumentException>(() =>
            DecayRecord("missing-decay-evidence", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 9), []));
        var restorationException = Assert.Throws<ArgumentException>(() =>
            RestorationRecord("missing-restoration-evidence", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 11), []));

        Assert.Contains("maintenance check", decayException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("restoration check", restorationException.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalDecayRestorationHistoryStore CreateStore(string databasePath)
    {
        return new LocalDecayRestorationHistoryStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static LocalDecayHistoryRecord DecayRecord(
        string decayId,
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        IEnumerable<string>? maintenanceCheckIds = null)
    {
        var current = new BranchLevelStatus(branch, level, BranchLevelState.Maintenance);
        var result = DecayRestorationEvaluator.EvaluateDecay(
            current,
            new MaintenanceCurrencyResult(
                branch,
                level,
                MaintenanceCurrencyState.Failed,
                new MaintenanceCadence(7, 10, MaintenanceCheckKind.StandardOrTransfer),
                DaysSinceLastPassingCheck: 8,
                ConsecutiveFailures: 2));

        return new LocalDecayHistoryRecord(
            decayId,
            date,
            result.CurrentStatus,
            result.NextStatus,
            result.Transition!.Value,
            maintenanceCheckIds ?? [$"{decayId}-maintenance-fail-1", $"{decayId}-maintenance-fail-2"]);
    }

    private static LocalRestorationHistoryRecord RestorationRecord(
        string restorationId,
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        IEnumerable<string>? restorationCheckIds = null)
    {
        var current = new BranchLevelStatus(branch, level, BranchLevelState.Decayed);
        var evidence = RestorationEvidence(branch, level, date);
        var result = DecayRestorationEvaluator.EvaluateRestoration(current, evidence);

        return new LocalRestorationHistoryRecord(
            restorationId,
            date,
            result.CurrentStatus,
            result.NextStatus,
            result.Transition!.Value,
            restorationCheckIds ?? [$"{restorationId}-last-owned", $"{restorationId}-lower-load-transfer"],
            evidence);
    }

    private static RestorationEvidence RestorationEvidence(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date)
    {
        return new RestorationEvidence(
            branch,
            level,
            [
                new RestorationCheckEvidence(
                    branch,
                    level,
                    date,
                    RestorationCheckKind.LastOwnedStandard,
                    new StandardEvaluationResult(true, [])),
                new RestorationCheckEvidence(
                    branch,
                    level,
                    TrainingDate.From(date.Year, date.Month, date.Day + 1),
                    RestorationCheckKind.LowerLoadTransferCheck,
                    new StandardEvaluationResult(true, [])),
                new RestorationCheckEvidence(
                    branch,
                    level,
                    TrainingDate.From(date.Year, date.Month, date.Day + 1),
                    RestorationCheckKind.LastOwnedStandard,
                    new StandardEvaluationResult(
                        false,
                        [
                            new StandardEvaluationFailure(
                                StandardFailureKind.CriticalConstraintBroken,
                                "failed restoration check retained for history round-trip"),
                        ])),
            ]);
    }

    private static MaintenanceCurrencyResult CurrentMaintenance(BranchCode branch, GlobalLevelId level)
    {
        return new MaintenanceCurrencyResult(
            branch,
            level,
            MaintenanceCurrencyState.Current,
            new MaintenanceCadence(7, 10, MaintenanceCheckKind.StandardOrTransfer),
            DaysSinceLastPassingCheck: 3,
            ConsecutiveFailures: 0);
    }

    private static void AssertDecayEquivalent(
        LocalDecayHistoryRecord expected,
        LocalDecayHistoryRecord actual)
    {
        Assert.Equal(expected.DecayId, actual.DecayId);
        Assert.Equal(expected.Date, actual.Date);
        Assert.Equal(expected.CurrentStatus, actual.CurrentStatus);
        Assert.Equal(expected.NextStatus, actual.NextStatus);
        Assert.Equal(expected.Transition, actual.Transition);
        Assert.Equal(expected.MaintenanceCheckIds, actual.MaintenanceCheckIds);
    }

    private static void AssertRestorationEquivalent(
        LocalRestorationHistoryRecord expected,
        LocalRestorationHistoryRecord actual)
    {
        Assert.Equal(expected.RestorationId, actual.RestorationId);
        Assert.Equal(expected.Date, actual.Date);
        Assert.Equal(expected.CurrentStatus, actual.CurrentStatus);
        Assert.Equal(expected.NextStatus, actual.NextStatus);
        Assert.Equal(expected.Transition, actual.Transition);
        Assert.Equal(expected.RestorationCheckIds, actual.RestorationCheckIds);
        Assert.Equal(expected.Evidence.Branch, actual.Evidence.Branch);
        Assert.Equal(expected.Evidence.LastOwnedLevel, actual.Evidence.LastOwnedLevel);
        Assert.Equal(expected.Evidence.Checks.Count, actual.Evidence.Checks.Count);
        foreach (var pair in expected.Evidence.Checks.Zip(actual.Evidence.Checks))
        {
            Assert.Equal(pair.First.Kind, pair.Second.Kind);
            Assert.Equal(pair.First.Date, pair.Second.Date);
            Assert.Equal(pair.First.StandardEvaluationResult.Passed, pair.Second.StandardEvaluationResult.Passed);
            Assert.Equal(pair.First.StandardEvaluationResult.Failures, pair.Second.StandardEvaluationResult.Failures);
        }
    }
}
