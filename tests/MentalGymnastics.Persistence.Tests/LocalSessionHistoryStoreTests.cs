using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalSessionHistoryStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadReturnsNullAndHistoryIsEmptyWhenNoSessionsHaveBeenSaved()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        var loaded = await store.LoadAsync("missing-session");
        var history = await store.ListAsync();
        var branchLevelHistory = await store.ListByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L1);
        var practiceHistory = await store.ListBySessionTypeAsync(LocalCompletedSessionType.Practice);

        Assert.Null(loaded);
        Assert.Empty(history);
        Assert.Empty(branchLevelHistory);
        Assert.Empty(practiceHistory);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task SaveLoadAndListRoundTripCompletedSessionsForEveryRequiredSessionType()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var expected = new[]
        {
            Session("practice-fh-l1", LocalCompletedSessionType.Practice, TrainingDate.From(2026, 7, 4)),
            Session("load-fh-l1", LocalCompletedSessionType.Load, TrainingDate.From(2026, 7, 5)),
            Session("test-fh-l1", LocalCompletedSessionType.Test, TrainingDate.From(2026, 7, 6)),
            Session("stabilization-fh-l1", LocalCompletedSessionType.Stabilization, TrainingDate.From(2026, 7, 7)),
            Session("regression-fh-l1", LocalCompletedSessionType.Regression, TrainingDate.From(2026, 7, 8)),
            Session("transfer-fh-l1", LocalCompletedSessionType.Transfer, TrainingDate.From(2026, 7, 9), transferTask: "hold target during WM reconstruction"),
            Session("recovery-fh-l1", LocalCompletedSessionType.Recovery, TrainingDate.From(2026, 7, 10), recoveryMarked: true),
            Session("maintenance-fh-l1", LocalCompletedSessionType.Maintenance, TrainingDate.From(2026, 7, 11)),
        };
        var store = CreateStore(databasePath);

        foreach (var record in expected)
        {
            await store.SaveAsync(record);
        }

        var history = await CreateStore(databasePath).ListAsync();
        var loadedMaintenance = await CreateStore(databasePath).LoadAsync("maintenance-fh-l1");

        Assert.Equal(expected.Select(record => record.SessionId), history.Select(record => record.SessionId));
        Assert.NotNull(loadedMaintenance);
        AssertEquivalent(expected[^1], loadedMaintenance);
        foreach (var pair in expected.Zip(history))
        {
            AssertEquivalent(pair.First, pair.Second);
        }
    }

    [Fact]
    public async Task ListReturnsCompletedSessionsOrderedByTrainingDate()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveAsync(Session("latest", LocalCompletedSessionType.Practice, TrainingDate.From(2026, 7, 10)));
        await store.SaveAsync(Session("earliest", LocalCompletedSessionType.Maintenance, TrainingDate.From(2026, 7, 4)));
        await store.SaveAsync(Session("middle", LocalCompletedSessionType.Load, TrainingDate.From(2026, 7, 6)));

        var history = await CreateStore(databasePath).ListAsync();

        Assert.Equal(["earliest", "middle", "latest"], history.Select(record => record.SessionId));
    }

    [Fact]
    public async Task ListByBranchLevelIncludesMultiBranchSessionsAndPreservesDateOrder()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var focusHold = Session("focus-hold", LocalCompletedSessionType.Practice, TrainingDate.From(2026, 7, 4));
        var integrated = Session(
            "integrated",
            LocalCompletedSessionType.Transfer,
            TrainingDate.From(2026, 7, 6),
            branchLevels:
            [
                new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L1),
                new LocalSessionBranchLevel(BranchCode.WM, GlobalLevelId.L1),
                new LocalSessionBranchLevel(BranchCode.IR, GlobalLevelId.L1),
            ],
            transferTask: "same focus hold demand inside reconstruction and inhibition task");
        var workingMemory = Session(
            "working-memory",
            LocalCompletedSessionType.Practice,
            TrainingDate.From(2026, 7, 8),
            branchLevels: [new LocalSessionBranchLevel(BranchCode.WM, GlobalLevelId.L1)],
            drill: DrillId.WM1DelayedReconstruction);

        await store.SaveAsync(workingMemory);
        await store.SaveAsync(integrated);
        await store.SaveAsync(focusHold);

        var focusHoldHistory = await CreateStore(databasePath).ListByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L1);
        var workingMemoryHistory = await CreateStore(databasePath).ListByBranchLevelAsync(BranchCode.WM, GlobalLevelId.L1);

        Assert.Equal(["focus-hold", "integrated"], focusHoldHistory.Select(record => record.SessionId));
        Assert.Equal(["integrated", "working-memory"], workingMemoryHistory.Select(record => record.SessionId));
    }

    [Fact]
    public async Task ListBySessionTypeFiltersCompletedSessionsAndPreservesDateOrder()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveAsync(Session("practice-2", LocalCompletedSessionType.Practice, TrainingDate.From(2026, 7, 8)));
        await store.SaveAsync(Session("maintenance-1", LocalCompletedSessionType.Maintenance, TrainingDate.From(2026, 7, 6)));
        await store.SaveAsync(Session("practice-1", LocalCompletedSessionType.Practice, TrainingDate.From(2026, 7, 4)));

        var practiceHistory = await CreateStore(databasePath).ListBySessionTypeAsync(LocalCompletedSessionType.Practice);
        var maintenanceHistory = await CreateStore(databasePath).ListBySessionTypeAsync(LocalCompletedSessionType.Maintenance);

        Assert.Equal(["practice-1", "practice-2"], practiceHistory.Select(record => record.SessionId));
        Assert.Equal("maintenance-1", Assert.Single(maintenanceHistory).SessionId);
    }

    [Fact]
    public async Task SaveReplacesSessionWithSameIdentifierWithoutDuplicatingHistory()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var original = Session("same-session", LocalCompletedSessionType.Practice, TrainingDate.From(2026, 7, 4));
        var replacement = Session(
            "same-session",
            LocalCompletedSessionType.Maintenance,
            TrainingDate.From(2026, 7, 11),
            notes: "maintenance check passed with one marked drift");

        await store.SaveAsync(original);
        await store.SaveAsync(replacement);

        var history = await CreateStore(databasePath).ListAsync();
        var loaded = await CreateStore(databasePath).LoadAsync("same-session");

        Assert.Single(history);
        Assert.NotNull(loaded);
        AssertEquivalent(replacement, loaded);
    }

    [Fact]
    public async Task SavedSessionsUseStableDomainIdentifiersAndDoNotPersistLiveSessionInfrastructure()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var record = Session(
            "maintenance-fh-l2",
            LocalCompletedSessionType.Maintenance,
            TrainingDate.From(2026, 7, 12),
            branchLevels: [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L2)]);

        await CreateStore(databasePath).SaveAsync(record);
        var storedJson = await File.ReadAllTextAsync(databasePath);

        Assert.Contains("\"SessionType\": \"Maintenance\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Branch\": \"FH\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Level\": \"L2\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Drill\": \"FH1TargetHold\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Intensity\": \"Moderate\"", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Focus Hold", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Account", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Telemetry", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Notification", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ElapsedMilliseconds", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Timer", storedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionHistoryRejectsCompletedSessionsWithoutEvidenceReference()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Session("practice-without-evidence", LocalCompletedSessionType.Practice, TrainingDate.From(2026, 7, 4), evidenceArtifactIds: []));

        Assert.Contains("evidence", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalSessionHistoryStore CreateStore(string databasePath)
    {
        return new LocalSessionHistoryStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static LocalSessionHistoryRecord Session(
        string sessionId,
        LocalCompletedSessionType sessionType,
        TrainingDate date,
        IEnumerable<LocalSessionBranchLevel>? branchLevels = null,
        DrillId? drill = DrillId.FH1TargetHold,
        string? transferTask = null,
        LocalSessionIntensity intensity = LocalSessionIntensity.Moderate,
        IEnumerable<LoadVariable>? loadVariables = null,
        bool cleanPerformance = true,
        string notes = "clean performance: 4 marked drifts; no target change",
        bool recoveryMarked = false,
        bool deloadMarked = false,
        IEnumerable<string>? evidenceArtifactIds = null)
    {
        return new LocalSessionHistoryRecord(
            sessionId,
            date,
            sessionType,
            branchLevels ?? [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L1)],
            drill,
            transferTask,
            intensity,
            loadVariables ?? [new LoadVariable("duration", "3 minutes")],
            cleanPerformance,
            notes,
            recoveryMarked,
            deloadMarked,
            evidenceArtifactIds ?? [$"artifact-{sessionId}"]);
    }

    private static void AssertEquivalent(LocalSessionHistoryRecord expected, LocalSessionHistoryRecord actual)
    {
        Assert.Equal(expected.SessionId, actual.SessionId);
        Assert.Equal(expected.Date, actual.Date);
        Assert.Equal(expected.SessionType, actual.SessionType);
        Assert.Equal(expected.BranchLevels, actual.BranchLevels);
        Assert.Equal(expected.Drill, actual.Drill);
        Assert.Equal(expected.TransferTask, actual.TransferTask);
        Assert.Equal(expected.Intensity, actual.Intensity);
        Assert.Equal(expected.LoadVariables, actual.LoadVariables);
        Assert.Equal(expected.CleanPerformance, actual.CleanPerformance);
        Assert.Equal(expected.Notes, actual.Notes);
        Assert.Equal(expected.RecoveryMarked, actual.RecoveryMarked);
        Assert.Equal(expected.DeloadMarked, actual.DeloadMarked);
        Assert.Equal(expected.EvidenceArtifactIds, actual.EvidenceArtifactIds);
    }
}
