using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;
using System.Text.Json.Nodes;

namespace MentalGymnastics.App.Tests;

public sealed class ActiveRuntimeSessionSnapshotPersistenceServiceTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SavedActiveSnapshotRestoresThroughRuntimeRestoreModelWithPendingCuesAndEvidenceFacts()
    {
        var configuration = Configuration();
        var service = new ActiveRuntimeSessionSnapshotPersistenceService(configuration);
        var generatedInstance = CueGeneratedInstance();
        var session = CreateCueSessionDefinition(generatedInstance);
        var phasePlan = new RuntimeSessionPhasePlan(
        [
            RuntimeSessionPhaseDefinition.Manual("cue-window", RuntimeSessionPhaseKind.CueResponse),
            RuntimeSessionPhaseDefinition.Timed("rest", RuntimeSessionPhaseKind.Rest, RuntimeDuration.FromSeconds(10)),
        ]);
        var cueSchedule = CreateCueSchedule(generatedInstance);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = RuntimeInputCommandHandler.Start(
            "session-active-snapshot",
            session,
            phasePlan,
            clock,
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10),
                PauseAllowedPhaseKinds: [RuntimeSessionPhaseKind.CueResponse, RuntimeSessionPhaseKind.Rest]));
        var cueScheduler = new RuntimeCueScheduler(cueSchedule, clock, handler.EventLog);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        cueScheduler.AdvanceToCurrentTime(handler.CurrentPhase!);
        Assert.True(handler.Handle(RuntimeInputCommand.MarkDrift("drift-before-snapshot")).IsAccepted);

        await service.SaveAsync(new ActiveRuntimeSessionSnapshotSaveRequest(
            handler.CaptureSnapshot(),
            cueScheduler.CaptureSnapshot()));

        var persisted = await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-active-snapshot");
        var restored = await service.RestoreAsync(new ActiveRuntimeSessionSnapshotRestoreRequest(
            "session-active-snapshot",
            new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(100))),
            cueSchedule));

        Assert.NotNull(persisted);
        Assert.Equal("cue-window", persisted.PhaseScheduler.CurrentPhaseId);
        Assert.Equal(["cue-2"], persisted.CueScheduler?.PendingCueIds);
        Assert.Contains(persisted.EvidenceFacts, fact => fact.Name == "drift_id" && fact.Value == "drift-before-snapshot");

        Assert.Equal(ActiveRuntimeSessionSnapshotRestoreStatus.Restored, restored.Status);
        Assert.NotNull(restored.CommandHandler);
        Assert.NotNull(restored.CueScheduler);
        Assert.Equal("cue-window", restored.CommandHandler.CurrentPhase?.Id);
        Assert.Contains(restored.CommandHandler.Events, runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.DriftMarked &&
            runtimeEvent.Facts.Any(fact => fact.Name == "drift_id" && fact.Value == "drift-before-snapshot"));
        Assert.Equal(["cue-2"], restored.CueScheduler.PendingCues.Select(cue => cue.Id));
        Assert.False(restored.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task UnsafeSnapshotIsNotRestoredAsSuccessfulEvidence()
    {
        var configuration = Configuration();
        var service = new ActiveRuntimeSessionSnapshotPersistenceService(configuration);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = RuntimeInputCommandHandler.Start(
            "session-unsafe-snapshot",
            CreateWorkingMemorySessionDefinition(),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("encode", RuntimeSessionPhaseKind.EncodeWindow, RuntimeDuration.FromSeconds(60)),
                RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
            ]),
            clock,
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(20));
        await service.SaveAsync(new ActiveRuntimeSessionSnapshotSaveRequest(handler.CaptureSnapshot()));

        var restored = await service.RestoreAsync(new ActiveRuntimeSessionSnapshotRestoreRequest(
            "session-unsafe-snapshot",
            new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(100)))));

        Assert.Equal(ActiveRuntimeSessionSnapshotRestoreStatus.Unsafe, restored.Status);
        Assert.Null(restored.CommandHandler);
        Assert.Null(restored.CueScheduler);
        Assert.Contains("cannot be restored", restored.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.False(restored.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task CorruptedSnapshotRestoreIsRejectedAsUnsafe()
    {
        var configuration = Configuration();
        var service = new ActiveRuntimeSessionSnapshotPersistenceService(configuration);
        var handler = StartCueSession("session-corrupted-snapshot");

        await service.SaveAsync(new ActiveRuntimeSessionSnapshotSaveRequest(handler.CaptureSnapshot()));
        CorruptRuntimeEvents(configuration.LocalDatabasePath);

        var restored = await service.RestoreAsync(new ActiveRuntimeSessionSnapshotRestoreRequest(
            "session-corrupted-snapshot",
            new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(100)))));

        Assert.Equal(ActiveRuntimeSessionSnapshotRestoreStatus.Unsafe, restored.Status);
        Assert.Null(restored.CommandHandler);
        Assert.Null(restored.CueScheduler);
        Assert.False(restored.GrantsAdvancementInApp);
    }

    [Fact]
    public async Task AbandonedSnapshotIsNotRestoredAsSuccessfulEvidence()
    {
        var configuration = Configuration();
        var service = new ActiveRuntimeSessionSnapshotPersistenceService(configuration);
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartCueSession("session-abandoned-snapshot", clock);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(3));
        Assert.True(handler.Handle(RuntimeInputCommand.Abandon("app interrupted after user quit")).IsAccepted);
        await service.SaveAsync(new ActiveRuntimeSessionSnapshotSaveRequest(handler.CaptureSnapshot()));

        var restored = await service.RestoreAsync(new ActiveRuntimeSessionSnapshotRestoreRequest(
            "session-abandoned-snapshot",
            new ManualRuntimeClock(new RuntimeInstant(TimeSpan.FromSeconds(100)))));

        Assert.Equal(ActiveRuntimeSessionSnapshotRestoreStatus.Unsafe, restored.Status);
        Assert.Null(restored.CommandHandler);
        Assert.False(restored.GrantsAdvancementInApp);
        Assert.Contains("cannot be restored", restored.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearRemovesSavedActiveSnapshots()
    {
        var configuration = Configuration();
        var service = new ActiveRuntimeSessionSnapshotPersistenceService(configuration);
        var handler = StartCueSession("session-clear-snapshot");

        await service.SaveAsync(new ActiveRuntimeSessionSnapshotSaveRequest(handler.CaptureSnapshot()));
        Assert.NotNull(await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-clear-snapshot"));

        await service.ClearAsync();

        Assert.Null(await new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions)
            .LoadAsync("session-clear-snapshot"));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private AppStartupConfiguration Configuration()
    {
        return AppStartupConfiguration.ForAppOwnedLocalStoragePath(
            Path.Combine(tempDirectory, "mental-gymnastics.json"));
    }

    private static RuntimeCueSchedule CreateCueSchedule(RuntimeGeneratedDrillInstanceIdentity generatedInstance)
    {
        return new RuntimeCueSchedule(
            generatedInstance,
            [
                new RuntimeScheduledCue(
                    "cue-1",
                    RuntimeCueKind.FocusShift,
                    "left",
                    new RuntimeInstant(TimeSpan.FromSeconds(5)),
                    RuntimeDuration.FromSeconds(2),
                    RuntimeCueResponseExpectation.ResponseRequired,
                    "left"),
                new RuntimeScheduledCue(
                    "cue-2",
                    RuntimeCueKind.TimedResponse,
                    "now",
                    new RuntimeInstant(TimeSpan.FromSeconds(10)),
                    RuntimeDuration.FromSeconds(2),
                    RuntimeCueResponseExpectation.ResponseRequired,
                    "hit"),
            ]);
    }

    private static RuntimeInputCommandHandler StartCueSession(
        string sessionId,
        ManualRuntimeClock? clock = null)
    {
        var generatedInstance = CueGeneratedInstance();
        return RuntimeInputCommandHandler.Start(
            sessionId,
            CreateCueSessionDefinition(generatedInstance),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("cue-window", RuntimeSessionPhaseKind.CueResponse),
            ]),
            clock ?? new ManualRuntimeClock(RuntimeInstant.Zero),
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10),
                PauseAllowedPhaseKinds: [RuntimeSessionPhaseKind.CueResponse]));
    }

    private static void CorruptRuntimeEvents(string databasePath)
    {
        var document = JsonNode.Parse(File.ReadAllText(databasePath))!.AsObject();
        var snapshots = document["ActiveRuntimeSessionSnapshots"]!.AsArray();
        snapshots[0]!.AsObject()["RuntimeEvents"] = new JsonArray();
        File.WriteAllText(databasePath, document.ToJsonString());
    }

    private static RuntimeGeneratedDrillInstanceIdentity CueGeneratedInstance()
    {
        return new RuntimeGeneratedDrillInstanceIdentity(
            "generated-cue-sequence-1",
            new PromptContentIdentity(
                "content-cue-sequence-1",
                BranchCode.FS,
                GlobalLevelId.L1,
                DrillId.FS1CueSwitch,
                PromptContentKind.CueSequence,
                "fs-l1-cue-density"),
            "v1");
    }

    private static RuntimeSessionDefinition CreateCueSessionDefinition(
        RuntimeGeneratedDrillInstanceIdentity generatedInstance)
    {
        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FS,
            GlobalLevelId.L1,
            DrillId.FS1CueSwitch,
            [new LoadVariable("cue density", "2 cues in 10 seconds")],
            new BranchLevelStandard(
                BranchCode.FS,
                GlobalLevelId.L1,
                "Alternate between two targets on cue for 4 minutes.",
                "At least 90% correct cue responses; no more than 3 anticipatory switches.",
                "FH L1 passed once.",
                "Repeat twice; one after FH hold.",
                "Use a new pair of targets."),
            [new CriticalConstraint("Switch only on valid cue.")],
            generatedInstance);
    }

    private static RuntimeSessionDefinition CreateWorkingMemorySessionDefinition()
    {
        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            [new LoadVariable("item count", "5 simple items")],
            new BranchLevelStandard(
                BranchCode.WM,
                GlobalLevelId.L1,
                "Encode and reconstruct 5 simple items after 60 seconds.",
                "At least 4 of 5 exact; no invented items.",
                "FH L1 passed once.",
                "Repeat twice with new item sets.",
                "Use a different content type."),
            [new CriticalConstraint("No rereading after encode window; no invented items.")]);
    }
}
