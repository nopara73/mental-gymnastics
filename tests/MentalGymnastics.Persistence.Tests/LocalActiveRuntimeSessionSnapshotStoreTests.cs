using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using System.Text.Json.Nodes;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalActiveRuntimeSessionSnapshotStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveLoadListAndDeleteRoundTripActiveRuntimeSnapshotAfterRestart()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var expected = SnapshotRecord("session-runtime-active", TimeSpan.FromSeconds(45));

        await CreateStore(databasePath).SaveAsync(expected);

        var loaded = await CreateStore(databasePath).LoadAsync("session-runtime-active");
        var latest = await CreateStore(databasePath).LoadLatestAsync();
        var all = await CreateStore(databasePath).ListAsync();

        Assert.NotNull(loaded);
        AssertSnapshot(expected, loaded);
        Assert.NotNull(latest);
        Assert.Equal("session-runtime-active", latest.SessionId);
        Assert.Single(all);
        AssertSnapshot(expected, Assert.Single(all));

        await CreateStore(databasePath).DeleteAsync("session-runtime-active");

        Assert.Null(await CreateStore(databasePath).LoadAsync("session-runtime-active"));
        Assert.Empty(await CreateStore(databasePath).ListAsync());
    }

    [Fact]
    public async Task SaveReplacesSnapshotForSameSessionWithoutDuplicatingActiveState()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveAsync(SnapshotRecord("session-runtime-active", TimeSpan.FromSeconds(30)));
        await store.SaveAsync(SnapshotRecord("session-runtime-active", TimeSpan.FromSeconds(60)));

        var all = await CreateStore(databasePath).ListAsync();
        var loaded = await CreateStore(databasePath).LoadAsync("session-runtime-active");

        Assert.Single(all);
        Assert.NotNull(loaded);
        Assert.Equal(TimeSpan.FromSeconds(60), loaded.CapturedAt);
        Assert.Equal(TimeSpan.FromSeconds(20), loaded.PhaseScheduler.CurrentPhaseElapsed);
    }

    [Fact]
    public async Task ClearRemovesActiveSnapshotsWithoutReplacingOtherLocalData()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveAsync(SnapshotRecord("session-runtime-active-1", TimeSpan.FromSeconds(45)));
        await store.SaveAsync(SnapshotRecord("session-runtime-active-2", TimeSpan.FromSeconds(55)));
        AddLocalDataSentinel(databasePath);

        await CreateStore(databasePath).ClearAsync();

        Assert.Empty(await CreateStore(databasePath).ListAsync());
        Assert.Null(await CreateStore(databasePath).LoadLatestAsync());
        Assert.True(ReadLocalDataSentinel(databasePath));
    }

    [Fact]
    public async Task CorruptedSnapshotDataIsRejectedInsteadOfIgnored()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        await CreateStore(databasePath).SaveAsync(
            SnapshotRecord("session-runtime-active", TimeSpan.FromSeconds(45)));
        CorruptRuntimeEvents(databasePath);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateStore(databasePath).LoadAsync("session-runtime-active"));

        Assert.Contains("active runtime session snapshot", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SnapshotRecordRejectsSuccessfulCompletionEvidenceAsActiveState()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            SnapshotRecord(
                "session-runtime-active",
                TimeSpan.FromSeconds(45),
                evidenceFacts:
                [
                    new LocalRuntimeEventFactRecord("gate_outcome", "PassOnce"),
                ]));

        Assert.Contains("progression", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalActiveRuntimeSessionSnapshotStore CreateStore(string databasePath)
    {
        return new LocalActiveRuntimeSessionSnapshotStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static void AddLocalDataSentinel(string databasePath)
    {
        var document = JsonNode.Parse(File.ReadAllText(databasePath))!.AsObject();
        document["CompletedSessionHistorySentinel"] = "keep";
        File.WriteAllText(databasePath, document.ToJsonString());
    }

    private static bool ReadLocalDataSentinel(string databasePath)
    {
        var document = JsonNode.Parse(File.ReadAllText(databasePath))!.AsObject();
        return document.TryGetPropertyValue("CompletedSessionHistorySentinel", out var sentinel) &&
            sentinel?.GetValue<string>() == "keep";
    }

    private static void CorruptRuntimeEvents(string databasePath)
    {
        var document = JsonNode.Parse(File.ReadAllText(databasePath))!.AsObject();
        var snapshots = document["ActiveRuntimeSessionSnapshots"]!.AsArray();
        snapshots[0]!.AsObject()["RuntimeEvents"] = new JsonArray();
        File.WriteAllText(databasePath, document.ToJsonString());
    }

    private static LocalActiveRuntimeSessionSnapshotRecord SnapshotRecord(
        string sessionId,
        TimeSpan capturedAt,
        IEnumerable<LocalRuntimeEventFactRecord>? evidenceFacts = null)
    {
        var generated = new LocalRuntimeGeneratedDrillInstanceIdentityRecord(
            "generated-cue-sequence-1",
            new LocalGeneratedDrillContentIdentity(
                "content-cue-sequence-1",
                BranchCode.FS,
                GlobalLevelId.L1,
                DrillId.FS1CueSwitch,
                PromptContentKind.CueSequence,
                "fs-l1-cue-density",
                "v1"));
        var eventFacts = new[]
        {
            new LocalRuntimeEventFactRecord("drift_id", "drift-before-snapshot"),
        };
        var runtimeEvents = new[]
        {
            new LocalRuntimeEventRecord(
                sessionId,
                1,
                "SessionStarted",
                TimeSpan.Zero,
                phaseId: null,
                phaseKind: null,
                facts: []),
            new LocalRuntimeEventRecord(
                sessionId,
                2,
                "DriftMarked",
                TimeSpan.FromSeconds(5),
                "cue-window",
                "CueResponse",
                eventFacts),
        };

        return new LocalActiveRuntimeSessionSnapshotRecord(
            sessionId,
            capturedAt,
            new LocalRuntimeSessionDefinitionRecord(
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
                generated),
            new LocalRuntimeLifecycleStateRecord("Running", pauseAllowed: true),
            new LocalRuntimeInputOptionsRecord(
                pauseAllowed: true,
                correctionWindow: TimeSpan.FromSeconds(10),
                pauseAllowedPhaseKinds: ["CueResponse", "Rest"]),
            new LocalRuntimePhasePlanRecord(
            [
                new LocalRuntimePhaseDefinitionRecord("cue-window", "CueResponse", "Manual", scheduledDuration: null),
                new LocalRuntimePhaseDefinitionRecord("rest", "Rest", "Timed", TimeSpan.FromSeconds(10)),
            ]),
            new LocalRuntimePhaseSchedulerSnapshotRecord(
                "Running",
                capturedAt,
                currentPhaseIndex: 0,
                currentPhaseId: "cue-window",
                currentPhaseElapsed: capturedAt - TimeSpan.FromSeconds(40),
                completedPhases: []),
            runtimeEvents,
            (evidenceFacts ?? eventFacts).ToArray(),
            lastCorrectableEventSequenceNumber: 2,
            new LocalRuntimeCueSchedulerSnapshotRecord(
                generated,
                capturedAt,
                isPaused: false,
                elapsedPauseDuration: TimeSpan.Zero,
                cueStates:
                [
                    new LocalRuntimeCueStateSnapshotRecord(
                        "cue-1",
                        TimeSpan.FromSeconds(5),
                        "cue-window",
                        "CueResponse",
                        responseEventSequenceNumber: null),
                    new LocalRuntimeCueStateSnapshotRecord(
                        "cue-2",
                        presentedAt: null,
                        phaseId: null,
                        phaseKind: null,
                        responseEventSequenceNumber: null),
                ],
                runtimeEvents,
                (evidenceFacts ?? eventFacts).ToArray()));
    }

    private static void AssertSnapshot(
        LocalActiveRuntimeSessionSnapshotRecord expected,
        LocalActiveRuntimeSessionSnapshotRecord actual)
    {
        Assert.Equal(expected.SessionId, actual.SessionId);
        Assert.Equal(expected.CapturedAt, actual.CapturedAt);
        Assert.Equal(expected.SessionDefinition.GeneratedDrillInstance?.InstanceId, actual.SessionDefinition.GeneratedDrillInstance?.InstanceId);
        Assert.Equal(expected.SessionDefinition.GeneratedDrillInstance?.ContentIdentity.ContentId, actual.SessionDefinition.GeneratedDrillInstance?.ContentIdentity.ContentId);
        Assert.Equal(expected.SessionDefinition.Branch, actual.SessionDefinition.Branch);
        Assert.Equal(expected.SessionDefinition.Level, actual.SessionDefinition.Level);
        Assert.Equal(expected.SessionDefinition.Drill, actual.SessionDefinition.Drill);
        Assert.Equal("Running", actual.LifecycleState.Status);
        Assert.Equal("cue-window", actual.PhaseScheduler.CurrentPhaseId);
        Assert.Equal(expected.PhaseScheduler.CurrentPhaseElapsed, actual.PhaseScheduler.CurrentPhaseElapsed);
        Assert.Equal(["cue-2"], actual.CueScheduler?.PendingCueIds);
        Assert.Contains(actual.RuntimeEvents, runtimeEvent =>
            runtimeEvent.Kind == "DriftMarked" &&
            runtimeEvent.Facts.Any(fact => fact.Name == "drift_id" && fact.Value == "drift-before-snapshot"));
        Assert.Contains(actual.EvidenceFacts, fact =>
            fact.Name == "drift_id" &&
            fact.Value == "drift-before-snapshot");
    }
}
