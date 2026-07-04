using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalProgrammingEventTransactionTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CommitPersistsBranchStateEvidenceAttemptAndSessionTogether()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var transaction = CreateTransaction(databasePath);
        var eventDate = TrainingDate.From(2026, 7, 4);
        var artifact = ArtifactRecord("artifact-fh-l1-test", "attempt-fh-l1-test", eventDate);
        var attempt = AttemptRecord("attempt-fh-l1-test", "artifact-fh-l1-test", eventDate);
        var session = Session("session-fh-l1-test", "artifact-fh-l1-test", eventDate);
        var state = new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce),
            new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
        ]);

        await transaction.CommitAsync(context =>
        {
            context.SetPractitionerState(state);
            context.SaveEvidenceArtifact(artifact);
            context.SaveFormalTestAttempt(attempt);
            context.SaveCompletedSession(session);
            return ValueTask.CompletedTask;
        });

        var loadedState = await new LocalPractitionerStateStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync();
        var loadedArtifact = await new LocalEvidenceArtifactStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync("artifact-fh-l1-test");
        var loadedAttempt = await new LocalFormalTestAttemptStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync("attempt-fh-l1-test");
        var loadedSession = await new LocalSessionHistoryStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync("session-fh-l1-test");

        Assert.NotNull(loadedState);
        Assert.Equal(BranchLevelState.PassedOnce, loadedState.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.NotNull(loadedArtifact);
        Assert.Equal("attempt-fh-l1-test", loadedArtifact.Event.EventId);
        Assert.NotNull(loadedAttempt);
        Assert.Equal("artifact-fh-l1-test", loadedAttempt.EvidenceArtifactId);
        Assert.NotNull(loadedSession);
        Assert.Equal(["artifact-fh-l1-test"], loadedSession.EvidenceArtifactIds);
    }

    [Fact]
    public async Task CommitRejectsIntegrityBreaksAndLeavesPriorDataIntact()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var priorDate = TrainingDate.From(2026, 7, 3);
        await new LocalPractitionerStateStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
            ]));
        await new LocalEvidenceArtifactStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(ArtifactRecord("artifact-existing", "attempt-existing", priorDate));
        await new LocalFormalTestAttemptStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(AttemptRecord("attempt-existing", "artifact-existing", priorDate));
        await new LocalSessionHistoryStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(Session("session-existing", "artifact-existing", priorDate));
        var originalJson = await File.ReadAllTextAsync(databasePath);
        var transaction = CreateTransaction(databasePath);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await transaction.CommitAsync(context =>
            {
                context.SaveFormalTestAttempt(AttemptRecord(
                    "attempt-broken",
                    "missing-artifact",
                    TrainingDate.From(2026, 7, 4)));
                return ValueTask.CompletedTask;
            }));

        var missingAttempt = await new LocalFormalTestAttemptStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync("attempt-broken");
        var finalJson = await File.ReadAllTextAsync(databasePath);

        Assert.Contains("integrity", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(missingAttempt);
        Assert.Equal(originalJson, finalJson);
    }

    [Fact]
    public async Task FailedCommitDoesNotPersistPartialProgrammingEventRecordsAndLeavesPriorDataIntact()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var priorDate = TrainingDate.From(2026, 7, 3);
        var failedEventDate = TrainingDate.From(2026, 7, 4);
        await new LocalPractitionerStateStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
            ]));
        await new LocalEvidenceArtifactStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(ArtifactRecord("artifact-existing", "event-existing", priorDate));
        await new LocalFormalTestAttemptStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(AttemptRecord("attempt-existing", "artifact-existing", priorDate));
        await new LocalSessionHistoryStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(Session("session-existing", "artifact-existing", priorDate));
        var originalJson = await File.ReadAllTextAsync(databasePath);
        var transaction = CreateTransaction(databasePath);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await transaction.CommitAsync(context =>
            {
                context.SetPractitionerState(new PractitionerState(
                [
                    new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce),
                    new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
                ]));
                context.SaveEvidenceArtifact(ArtifactRecord("artifact-new", "event-new", failedEventDate));
                context.SaveFormalTestAttempt(AttemptRecord("attempt-new", "artifact-new", failedEventDate));
                context.SaveCompletedSession(Session("session-new", "artifact-new", failedEventDate));
                throw new InvalidOperationException("simulated transaction failure");
            }));

        var loadedState = await new LocalPractitionerStateStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync();
        var existingArtifact = await new LocalEvidenceArtifactStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync("artifact-existing");
        var newArtifact = await new LocalEvidenceArtifactStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync("artifact-new");
        var existingAttempt = await new LocalFormalTestAttemptStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync("attempt-existing");
        var newAttempt = await new LocalFormalTestAttemptStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync("attempt-new");
        var existingSession = await new LocalSessionHistoryStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync("session-existing");
        var newSession = await new LocalSessionHistoryStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .LoadAsync("session-new");
        var finalJson = await File.ReadAllTextAsync(databasePath);

        Assert.Equal("simulated transaction failure", exception.Message);
        Assert.NotNull(loadedState);
        Assert.Equal(BranchLevelState.Training, loadedState.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.NotNull(existingArtifact);
        Assert.Null(newArtifact);
        Assert.NotNull(existingAttempt);
        Assert.Null(newAttempt);
        Assert.NotNull(existingSession);
        Assert.Null(newSession);
        Assert.Equal(originalJson, finalJson);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalProgrammingEventTransaction CreateTransaction(string databasePath)
    {
        return new LocalProgrammingEventTransaction(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static LocalEvidenceArtifactRecord ArtifactRecord(
        string artifactId,
        string eventId,
        TrainingDate date)
    {
        return new LocalEvidenceArtifactRecord(
            artifactId,
            new LocalProgrammingEventReference(
                eventId,
                LocalProgrammingEventKind.FormalTest,
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold),
            Artifact(EvidenceArtifactCategory.Test, date, ObservableEvidenceKind.Score, "drifts: 4; max return: 8 seconds"));
    }

    private static LocalFormalTestAttemptRecord AttemptRecord(
        string attemptId,
        string evidenceArtifactId,
        TrainingDate date)
    {
        return new LocalFormalTestAttemptRecord(
            attemptId,
            evidenceArtifactId,
            new FormalTestAttempt(
                BranchCode.FH,
                GlobalLevelId.L1,
                date,
                TestTask.ForDrill(DrillId.FH1TargetHold),
                [new LoadVariable("duration", "3 minutes")],
                "No more than 5 marked drifts; each return within 10 seconds; no target change.",
                [new CriticalConstraint("target cannot change")],
                new TestResultEvidence(TestResultEvidenceKind.PassFail, "pass"),
                null,
                FormalTestPassState.PassOnce,
                Artifact(EvidenceArtifactCategory.Test, date, ObservableEvidenceKind.Score, "drifts: 4")));
    }

    private static LocalSessionHistoryRecord Session(
        string sessionId,
        string evidenceArtifactId,
        TrainingDate date)
    {
        return new LocalSessionHistoryRecord(
            sessionId,
            date,
            LocalCompletedSessionType.Test,
            [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L1)],
            DrillId.FH1TargetHold,
            null,
            LocalSessionIntensity.High,
            [new LoadVariable("duration", "3 minutes")],
            cleanPerformance: true,
            notes: "formal FH L1 test passed with 4 marked drifts",
            recoveryMarked: false,
            deloadMarked: false,
            [evidenceArtifactId]);
    }

    private static EvidenceArtifact Artifact(
        EvidenceArtifactCategory category,
        TrainingDate date,
        ObservableEvidenceKind kind,
        string evidence)
    {
        return new EvidenceArtifact(
            category,
            date,
            [new ObservableEvidence(kind, evidence)],
            evidence);
    }
}
