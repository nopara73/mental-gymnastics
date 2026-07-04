using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalProgramRepositoryTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadCurrentStateRecentSessionsAndEvidenceHistoryForAppScreens()
    {
        var databasePath = DatabasePath();
        var options = Options(databasePath);
        await new LocalPractitionerStateStore(options).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
                new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
            ]));

        await SaveSessionAsync(databasePath, Session("session-fh-older", TrainingDate.From(2026, 7, 2), BranchCode.FH));
        await SaveSessionAsync(databasePath, Session("session-fh-newer", TrainingDate.From(2026, 7, 5), BranchCode.FH, LocalCompletedSessionType.Load));
        await SaveSessionAsync(databasePath, Session("session-fh-future", TrainingDate.From(2026, 7, 6), BranchCode.FH));
        await SaveSessionAsync(databasePath, Session("session-fs", TrainingDate.From(2026, 7, 4), BranchCode.FS));

        await SaveArtifactAsync(databasePath, ArtifactRecord(
            "artifact-fh-older",
            "session-fh-older",
            LocalProgrammingEventKind.Practice,
            EvidenceArtifactCategory.Practice,
            TrainingDate.From(2026, 7, 2),
            BranchCode.FH));
        await SaveArtifactAsync(databasePath, ArtifactRecord(
            "artifact-fh-newer",
            "session-fh-newer",
            LocalProgrammingEventKind.Load,
            EvidenceArtifactCategory.Load,
            TrainingDate.From(2026, 7, 5),
            BranchCode.FH));
        await SaveArtifactAsync(databasePath, ArtifactRecord(
            "artifact-fh-future",
            "session-fh-future",
            LocalProgrammingEventKind.Practice,
            EvidenceArtifactCategory.Practice,
            TrainingDate.From(2026, 7, 6),
            BranchCode.FH));
        await SaveArtifactAsync(databasePath, ArtifactRecord(
            "artifact-global-review",
            "global-review-20260704",
            LocalProgrammingEventKind.GlobalReview,
            EvidenceArtifactCategory.GlobalReview,
            TrainingDate.From(2026, 7, 4)));
        await SaveArtifactAsync(databasePath, ArtifactRecord(
            "artifact-fs",
            "session-fs",
            LocalProgrammingEventKind.Practice,
            EvidenceArtifactCategory.Practice,
            TrainingDate.From(2026, 7, 4),
            BranchCode.FS));

        var repository = Repository(databasePath);
        var currentState = await repository.LoadCurrentStateAsync();
        var recentSessions = await repository.ListRecentSessionsAsync(
            new LocalRecentSessionsQuery(
                TrainingDate.From(2026, 7, 5),
                2,
                BranchCode.FH,
                GlobalLevelId.L1));
        var evidenceHistory = await repository.ListEvidenceHistoryAsync(
            new LocalEvidenceHistoryQuery(
                TrainingDate.From(2026, 7, 5),
                2,
                BranchCode.FH,
                GlobalLevelId.L1));

        Assert.NotNull(currentState);
        Assert.Equal(BranchLevelState.Maintenance, currentState.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
        Assert.Equal(["session-fh-newer", "session-fh-older"], recentSessions.Select(record => record.SessionId));
        Assert.Equal(["artifact-fh-newer", "artifact-fh-older"], evidenceHistory.Select(record => record.ArtifactId));
    }

    [Fact]
    public async Task ListDueMaintenanceUsesCoreCurrencyRulesWithoutPersistingDerivedDecisions()
    {
        var databasePath = DatabasePath();
        var options = Options(databasePath);
        await new LocalPractitionerStateStore(options).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance),
                new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Maintenance),
                new BranchLevelStatus(BranchCode.DE, GlobalLevelId.L1, BranchLevelState.Owned),
                new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
            ]));

        await SaveMaintenanceAsync(databasePath, Maintenance(
            "maintenance-fh-current",
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 4),
            passed: true));
        await SaveMaintenanceAsync(databasePath, Maintenance(
            "maintenance-wm-warning",
            BranchCode.WM,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 9),
            passed: false));
        await SaveMaintenanceAsync(databasePath, Maintenance(
            "maintenance-de-future",
            BranchCode.DE,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 20),
            passed: true));

        var dueMaintenance = await Repository(databasePath).ListDueMaintenanceAsync(TrainingDate.From(2026, 7, 10));

        Assert.DoesNotContain(dueMaintenance, record => record.BranchLevel.Branch == BranchCode.FH);
        Assert.DoesNotContain(dueMaintenance, record => record.BranchLevel.Branch == BranchCode.FS);
        Assert.Contains(dueMaintenance, record =>
            record.BranchLevel.Branch == BranchCode.WM &&
            record.BranchLevel.Level == GlobalLevelId.L1 &&
            record.Currency.State == MaintenanceCurrencyState.Warning &&
            record.Currency.ConsecutiveFailures == 1);
        Assert.Contains(dueMaintenance, record =>
            record.BranchLevel.Branch == BranchCode.DE &&
            record.BranchLevel.Level == GlobalLevelId.L1 &&
            record.Currency.State == MaintenanceCurrencyState.Due &&
            record.Currency.DaysSinceLastPassingCheck is null);
    }

    [Fact]
    public async Task LoadProgressRecordsReturnsAttemptsStabilizationMaintenanceDecayAndSummaryFacts()
    {
        var databasePath = DatabasePath();
        await SaveAttemptAsync(databasePath, Attempt(
            "attempt-fh-l1",
            BranchCode.FH,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 2),
            FormalTestPassState.PassOnce));
        await SaveAttemptAsync(databasePath, Attempt(
            "attempt-fs-l1",
            BranchCode.FS,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 3),
            FormalTestPassState.PassOnce));
        await SaveAttemptAsync(databasePath, Attempt(
            "attempt-fh-future",
            BranchCode.FH,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 8),
            FormalTestPassState.PassOnce));
        await SaveStabilizationAsync(databasePath, StabilizationPass(
            "stabilization-fh-l1",
            BranchCode.FH,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 3)));
        await SaveMaintenanceAsync(databasePath, Maintenance(
            "maintenance-fh-l1",
            BranchCode.FH,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 4),
            passed: true));
        await SaveDecayAsync(databasePath, Decay("decay-fh-l1", BranchCode.FH, GlobalLevelId.L1, TrainingDate.From(2026, 7, 5)));
        await SaveProgressSummaryAsync(databasePath, Summary("summary-current", TrainingDate.From(2026, 7, 5)));
        await SaveProgressSummaryAsync(databasePath, Summary("summary-future", TrainingDate.From(2026, 7, 8)));

        var records = await Repository(databasePath).LoadProgressRecordsAsync(
            new LocalProgressRecordsQuery(
                TrainingDate.From(2026, 7, 5),
                10,
                BranchCode.FH,
                GlobalLevelId.L1));

        Assert.Equal(["attempt-fh-l1"], records.FormalTestAttempts.Select(record => record.AttemptId));
        Assert.Equal(["stabilization-fh-l1"], records.StabilizationPasses.Select(record => record.PassId));
        Assert.Equal(["maintenance-fh-l1"], records.MaintenanceChecks.Select(record => record.CheckId));
        Assert.Equal(["decay-fh-l1"], records.DecayHistory.Select(record => record.DecayId));
        Assert.Empty(records.RestorationHistory);
        Assert.Equal("summary-current", records.LatestSummary?.SummaryId);
    }

    [Fact]
    public async Task GeneratedDrillQueriesExposeReusableSessionRuntimeInstances()
    {
        var databasePath = DatabasePath();
        await SaveGeneratedInstanceAsync(databasePath, GeneratedInstance("reserved-fh", TrainingDate.From(2026, 7, 4)));
        await SaveGeneratedInstanceAsync(databasePath, GeneratedInstance(
            "in-session-fh",
            TrainingDate.From(2026, 7, 5),
            LocalGeneratedDrillInstanceState.InSession,
            activeSessionId: "session-active"));
        await SaveGeneratedInstanceAsync(databasePath, GeneratedInstance(
            "completed-fh",
            TrainingDate.From(2026, 7, 6),
            LocalGeneratedDrillInstanceState.Completed,
            resultEvidenceArtifactId: "artifact-completed"));

        var repository = Repository(databasePath);
        var loaded = await repository.LoadGeneratedDrillInstanceAsync("in-session-fh");
        var reusable = await repository.ListReusableGeneratedDrillInstancesAsync(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")]);

        Assert.NotNull(loaded);
        Assert.Equal(LocalGeneratedDrillInstanceState.InSession, loaded.State);
        Assert.Equal(["reserved-fh", "in-session-fh"], reusable.Select(record => record.InstanceId));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string DatabasePath()
    {
        Directory.CreateDirectory(tempDirectory);
        return Path.Combine(tempDirectory, "mental-gymnastics.db");
    }

    private static LocalDatabaseOptions Options(string databasePath)
    {
        return LocalDatabaseOptions.ForAppOwnedPath(databasePath);
    }

    private static LocalProgramRepository Repository(string databasePath)
    {
        return new LocalProgramRepository(Options(databasePath));
    }

    private static async ValueTask SaveSessionAsync(string databasePath, LocalSessionHistoryRecord record)
    {
        await new LocalSessionHistoryStore(Options(databasePath)).SaveAsync(record);
    }

    private static async ValueTask SaveArtifactAsync(string databasePath, LocalEvidenceArtifactRecord record)
    {
        await new LocalEvidenceArtifactStore(Options(databasePath)).SaveAsync(record);
    }

    private static async ValueTask SaveMaintenanceAsync(string databasePath, LocalMaintenanceCheckRecord record)
    {
        await new LocalMaintenanceCheckStore(Options(databasePath)).SaveMaintenanceAsync(record);
    }

    private static async ValueTask SaveAttemptAsync(string databasePath, LocalFormalTestAttemptRecord record)
    {
        await new LocalFormalTestAttemptStore(Options(databasePath)).SaveAsync(record);
    }

    private static async ValueTask SaveStabilizationAsync(string databasePath, LocalStabilizationPassRecord record)
    {
        await new LocalStabilizationPassStore(Options(databasePath)).SaveAsync(record);
    }

    private static async ValueTask SaveDecayAsync(string databasePath, LocalDecayHistoryRecord record)
    {
        await new LocalDecayRestorationHistoryStore(Options(databasePath)).SaveDecayAsync(record);
    }

    private static async ValueTask SaveProgressSummaryAsync(string databasePath, LocalProgressSummaryRecord record)
    {
        await new LocalProgressSummaryStore(Options(databasePath)).SaveAsync(record);
    }

    private static async ValueTask SaveGeneratedInstanceAsync(string databasePath, LocalGeneratedDrillInstanceRecord record)
    {
        await new LocalGeneratedDrillInstanceStore(Options(databasePath)).SaveAsync(record);
    }

    private static LocalSessionHistoryRecord Session(
        string sessionId,
        TrainingDate date,
        BranchCode branch,
        LocalCompletedSessionType sessionType = LocalCompletedSessionType.Practice)
    {
        return new LocalSessionHistoryRecord(
            sessionId,
            date,
            sessionType,
            [new LocalSessionBranchLevel(branch, GlobalLevelId.L1)],
            DrillId.FH1TargetHold,
            null,
            LocalSessionIntensity.Moderate,
            [new LoadVariable("duration", "3 minutes")],
            cleanPerformance: true,
            notes: $"{sessionId} observable set record.",
            recoveryMarked: false,
            deloadMarked: false,
            evidenceArtifactIds: [$"artifact-{sessionId}"]);
    }

    private static LocalEvidenceArtifactRecord ArtifactRecord(
        string artifactId,
        string eventId,
        LocalProgrammingEventKind eventKind,
        EvidenceArtifactCategory category,
        TrainingDate date,
        BranchCode? branch = null)
    {
        return new LocalEvidenceArtifactRecord(
            artifactId,
            new LocalProgrammingEventReference(
                eventId,
                eventKind,
                branch,
                branch.HasValue ? GlobalLevelId.L1 : null,
                branch.HasValue ? DrillId.FH1TargetHold : null),
            Artifact(category, date, $"{artifactId} observable evidence summary."));
    }

    private static LocalMaintenanceCheckRecord Maintenance(
        string checkId,
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        bool passed)
    {
        return new LocalMaintenanceCheckRecord(
            checkId,
            $"artifact-{checkId}",
            null,
            DrillId.FH1TargetHold,
            "Maintenance standard remained stated before the check.",
            new MaintenanceCheckEvidence(
                branch,
                level,
                date,
                MaintenanceCheckKind.StandardOrTransfer,
                passed ? CleanResult() : FailedResult()));
    }

    private static LocalFormalTestAttemptRecord Attempt(
        string attemptId,
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        FormalTestPassState passState)
    {
        return new LocalFormalTestAttemptRecord(
            attemptId,
            $"artifact-{attemptId}",
            new FormalTestAttempt(
                branch,
                level,
                date,
                TestTask.ForDrill(DrillId.FH1TargetHold),
                [new LoadVariable("duration", "3 minutes")],
                "FH L1 target hold standard",
                [new CriticalConstraint("Target is stated before the set and every drift is marked.")],
                new TestResultEvidence(TestResultEvidenceKind.PassFail, passState == FormalTestPassState.Fail ? "fail" : "pass"),
                passState == FormalTestPassState.Fail ? FailureType.Overload : null,
                passState,
                Artifact(EvidenceArtifactCategory.Test, date, $"{attemptId} formal test evidence.")));
    }

    private static LocalStabilizationPassRecord StabilizationPass(
        string passId,
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date)
    {
        return new LocalStabilizationPassRecord(
            passId,
            $"artifact-{passId}",
            null,
            null,
            DrillId.FH1TargetHold,
            LocalStabilizationCondition.OrdinaryVariance,
            "Ordinary weekly variance.",
            new StabilizationPassEvidence(
                branch,
                level,
                date,
                "FH L1 target hold standard",
                FormalTestPassState.StabilizationPass,
                CleanResult(),
                afterAdjacentWorkOrControlledDistractor: false,
                mainFailureModeAvoided: "unmarked drift"));
    }

    private static LocalDecayHistoryRecord Decay(
        string decayId,
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date)
    {
        return new LocalDecayHistoryRecord(
            decayId,
            date,
            new BranchLevelStatus(branch, level, BranchLevelState.Maintenance),
            new BranchLevelStatus(branch, level, BranchLevelState.Decayed),
            BranchLevelTransition.MarkDecayed,
            ["maintenance-failure-one", "maintenance-failure-two"]);
    }

    private static LocalProgressSummaryRecord Summary(string summaryId, TrainingDate generatedOn)
    {
        return new LocalProgressSummaryRecord(
            summaryId,
            generatedOn,
            TrainingDate.From(2026, 6, 28),
            generatedOn,
            isAuthoritative: false,
            branchSummaries:
            [
                new LocalBranchProgressSummary(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
            ],
            maintenanceSummaries: [],
            activeBlockers: [],
            bottleneckBranch: null,
            new LocalProgrammedEmphasis(LocalProgressEmphasisKind.ContinueActiveTraining, BranchCode.FH, GlobalLevelId.L1),
            completedSessionCount: 1,
            formalAttemptCount: 1,
            evidenceArtifactCount: 1,
            sourceReferences:
            [
                new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.PractitionerState, "PractitionerState"),
            ]);
    }

    private static LocalGeneratedDrillInstanceRecord GeneratedInstance(
        string instanceId,
        TrainingDate generatedOn,
        LocalGeneratedDrillInstanceState state = LocalGeneratedDrillInstanceState.Reserved,
        string? activeSessionId = null,
        string? resultEvidenceArtifactId = null)
    {
        return new LocalGeneratedDrillInstanceRecord(
            instanceId,
            generatedOn,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")],
            new LocalGeneratedDrillContentIdentity(
                $"content-{instanceId}",
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold,
                PromptContentKind.CueSequence,
                "FH1-target-hold",
                "v1"),
            state,
            activeSessionId,
            resultEvidenceArtifactId);
    }

    private static EvidenceArtifact Artifact(
        EvidenceArtifactCategory category,
        TrainingDate date,
        string summary)
    {
        return new EvidenceArtifact(
            category,
            date,
            [new ObservableEvidence(ObservableEvidenceKind.OutputSample, summary)],
            summary);
    }

    private static StandardEvaluationResult CleanResult()
    {
        return new StandardEvaluationResult(true, []);
    }

    private static StandardEvaluationResult FailedResult()
    {
        return new StandardEvaluationResult(
            false,
            [new StandardEvaluationFailure(StandardFailureKind.CriticalConstraintBroken, "Critical constraint broken.")]);
    }
}
