using System.Text.Json.Nodes;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalDailyTrainingPrescriptionStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SavesLoadsAndReplacesTheAuthoritativePrescriptionForOneDate()
    {
        var store = Store();
        var planned = Prescription(
            "daily-20260711",
            DailyTrainingDoseState.Planned,
            Block("block-1", 1, BranchCode.FH, LocalDailyTrainingBlockState.Planned),
            Block("block-2", 2, BranchCode.FS, LocalDailyTrainingBlockState.Planned));
        await store.SaveAsync(planned);

        var active = Prescription(
            "daily-20260711",
            DailyTrainingDoseState.Active,
            Block("block-1", 1, BranchCode.FH, LocalDailyTrainingBlockState.Active, "session-1"),
            Block("block-2", 2, BranchCode.FS, LocalDailyTrainingBlockState.Planned));
        await store.SaveAsync(active);

        var loaded = await store.LoadByDateAsync(TrainingDate.From(2026, 7, 11));

        Assert.NotNull(loaded);
        Assert.Equal(DailyTrainingDoseState.Active, loaded!.State);
        Assert.Equal("session-1", loaded.Blocks[0].SessionId);
        Assert.Equal(LocalDailyTrainingBlockState.Planned, loaded.Blocks[1].State);
        Assert.Single(await store.ListAsync());
    }

    [Fact]
    public async Task LoadsLegacyDailyBlockThatContainsFailureModeDeclaration()
    {
        var databasePath = Path.Combine(tempDirectory, "legacy-daily.json");
        var store = new LocalDailyTrainingPrescriptionStore(
            LocalDatabaseOptions.ForAppOwnedPath(databasePath));
        var expected = Prescription(
            "legacy-daily",
            DailyTrainingDoseState.Planned,
            Block("legacy-block", 1, BranchCode.FH, LocalDailyTrainingBlockState.Planned));
        await store.SaveAsync(expected);

        var document = JsonNode.Parse(await File.ReadAllTextAsync(databasePath))!.AsObject();
        document["DailyTrainingPrescriptions"]!.AsArray()[0]!.AsObject()["Blocks"]!
            .AsArray()[0]!.AsObject()["MainFailureModeAvoided"] = "target substitution";
        await File.WriteAllTextAsync(databasePath, document.ToJsonString());

        var loaded = await store.LoadByDateAsync(expected.Date);

        Assert.NotNull(loaded);
        Assert.Equal(expected.PrescriptionId, loaded.PrescriptionId);
        Assert.Equal(expected.Blocks[0].BlockId, loaded.Blocks[0].BlockId);
    }

    [Fact]
    public async Task RejectsASecondPrescriptionIdentityForTheSameCalendarDate()
    {
        var store = Store();
        await store.SaveAsync(Prescription(
            "daily-a",
            DailyTrainingDoseState.Planned,
            Block("block-a", 1, BranchCode.FH, LocalDailyTrainingBlockState.Planned)));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.SaveAsync(Prescription(
                "daily-b",
                DailyTrainingDoseState.Planned,
                Block("block-b", 1, BranchCode.FH, LocalDailyTrainingBlockState.Planned))));
    }

    [Fact]
    public async Task PersistsCompletedAndStoppedDoseShapesWithoutAllowingARetry()
    {
        var store = Store();
        var completed = Prescription(
            "daily-20260711",
            DailyTrainingDoseState.Completed,
            Block("block-1", 1, BranchCode.FH, LocalDailyTrainingBlockState.Completed, "session-1"),
            Block("block-2", 2, BranchCode.FS, LocalDailyTrainingBlockState.Failed, "session-2"));
        await store.SaveAsync(completed);

        var loaded = await store.LoadLatestAsync();

        Assert.NotNull(loaded);
        Assert.True(loaded!.IsTerminal);
        Assert.All(loaded.Blocks, block => Assert.True(block.IsTerminal));
        Assert.Throws<ArgumentException>(() => Prescription(
            "invalid-stopped",
            DailyTrainingDoseState.Stopped,
            Block("block-1", 1, BranchCode.FH, LocalDailyTrainingBlockState.Planned)));
    }

    [Fact]
    public async Task RepresentsOffDayAsAnAlreadyCompletedZeroBlockPrescription()
    {
        var store = Store();
        var off = new LocalDailyTrainingPrescriptionRecord(
            "daily-off",
            TrainingDate.From(2026, 7, 12),
            TrainingDate.From(2026, 7, 6),
            cycleDay: 7,
            WeeklySessionKind.OffOrRecovery,
            DailyTrainingDoseState.Completed,
            blocks: []);

        await store.SaveAsync(off);

        var loaded = await store.LoadLatestAsync();
        Assert.NotNull(loaded);
        Assert.Empty(loaded!.Blocks);
        Assert.True(loaded.IsTerminal);
    }

    private LocalDailyTrainingPrescriptionStore Store()
    {
        return new LocalDailyTrainingPrescriptionStore(LocalDatabaseOptions.ForAppOwnedPath(
            Path.Combine(tempDirectory, "mental-gymnastics.json")));
    }

    private static LocalDailyTrainingPrescriptionRecord Prescription(
        string id,
        DailyTrainingDoseState state,
        params LocalDailyTrainingBlockRecord[] blocks)
    {
        return new LocalDailyTrainingPrescriptionRecord(
            id,
            TrainingDate.From(2026, 7, 11),
            TrainingDate.From(2026, 7, 6),
            cycleDay: 6,
            WeeklySessionKind.TestOrStabilization,
            state,
            blocks);
    }

    private static LocalDailyTrainingBlockRecord Block(
        string id,
        int order,
        BranchCode branch,
        LocalDailyTrainingBlockState state,
        string? sessionId = null)
    {
        var level = GlobalLevelId.L1;
        var drill = ExecutableStandardCatalog.Get(branch, level).Drill;
        return new LocalDailyTrainingBlockRecord(
            id,
            order,
            branch,
            level,
            drill,
            LocalDailyTrainingBlockRole.Practice,
            TrainingLoadProfileCatalog.Get(branch, level).Stages[0].LoadVariables,
            state,
            sessionId);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
