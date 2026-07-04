using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalProgressSummaryStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadReturnsNullAndListIsEmptyWhenNoProgressSummariesHaveBeenSaved()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        var loaded = await store.LoadAsync("missing-summary");
        var latest = await store.LoadLatestAsync();
        var summaries = await store.ListAsync();

        Assert.Null(loaded);
        Assert.Null(latest);
        Assert.Empty(summaries);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task RefreshProducesSummaryFromPersistedLocalStateAndHistory()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        await SavePractitionerStateAsync(
            databasePath,
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance),
                new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
                new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Owned),
                new BranchLevelStatus(BranchCode.IR, GlobalLevelId.L1, BranchLevelState.Decayed),
            ]);
        await SaveSessionAsync(databasePath, Session("practice-fh", LocalCompletedSessionType.Practice, TrainingDate.From(2026, 7, 2)));
        await SaveSessionAsync(databasePath, Session("maintenance-fh", LocalCompletedSessionType.Maintenance, TrainingDate.From(2026, 7, 3)));
        await SaveMaintenanceAsync(
            databasePath,
            Maintenance("maintenance-fh-l2", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 3), passed: true));
        await SaveMaintenanceAsync(
            databasePath,
            Maintenance("maintenance-wm-l1", BranchCode.WM, GlobalLevelId.L1, TrainingDate.From(2026, 7, 3), passed: false));
        await SaveAttemptAsync(
            databasePath,
            Attempt("attempt-fh-pass", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 2), FormalTestPassState.PassOnce));
        await SaveAttemptAsync(
            databasePath,
            Attempt("attempt-wm-fail", BranchCode.WM, GlobalLevelId.L1, TrainingDate.From(2026, 7, 4), FormalTestPassState.Fail));
        await SaveArtifactAsync(
            databasePath,
            ArtifactRecord(
                "artifact-global-review",
                LocalProgrammingEventKind.GlobalReview,
                EvidenceArtifactCategory.GlobalReview,
                TrainingDate.From(2026, 7, 4),
                ObservableEvidenceKind.GlobalReviewSummary,
                "WM failed latest formal attempt; IR is decayed"));

        var summary = await CreateStore(databasePath).RefreshAsync(
            new LocalProgressSummaryRefreshRequest(
                "summary-20260704",
                TrainingDate.From(2026, 7, 4),
                TrainingDate.From(2026, 6, 28),
                TrainingDate.From(2026, 7, 4)));

        var loaded = await CreateStore(databasePath).LoadAsync("summary-20260704");
        var latest = await CreateStore(databasePath).LoadLatestAsync();

        Assert.NotNull(loaded);
        Assert.NotNull(latest);
        AssertEquivalent(summary, loaded);
        AssertEquivalent(summary, latest);
        Assert.False(summary.IsAuthoritative);
        Assert.Equal("summary-20260704", summary.SummaryId);
        Assert.Equal(TrainingDate.From(2026, 7, 4), summary.GeneratedOn);
        Assert.Equal(TrainingDate.From(2026, 6, 28), summary.PeriodStart);
        Assert.Equal(TrainingDate.From(2026, 7, 4), summary.PeriodEnd);
        Assert.Equal(2, summary.CompletedSessionCount);
        Assert.Equal(2, summary.FormalAttemptCount);
        Assert.Equal(1, summary.EvidenceArtifactCount);

        var focusHold = BranchSummary(summary, BranchCode.FH);
        var workingMemory = BranchSummary(summary, BranchCode.WM);
        var inhibition = BranchSummary(summary, BranchCode.IR);
        Assert.Equal(GlobalLevelId.L2, focusHold.HighestOwnedLevel);
        Assert.Equal(BranchLevelState.Maintenance, focusHold.StateAtHighestOwnedLevel);
        Assert.Equal(GlobalLevelId.L1, workingMemory.HighestOwnedLevel);
        Assert.Equal(BranchLevelState.Owned, workingMemory.StateAtHighestOwnedLevel);
        Assert.Equal(GlobalLevelId.L1, inhibition.HighestOwnedLevel);
        Assert.Equal(BranchLevelState.Decayed, inhibition.StateAtHighestOwnedLevel);

        Assert.Contains(summary.MaintenanceSummaries, item =>
            item.Branch == BranchCode.FH &&
            item.OwnedLevel == GlobalLevelId.L2 &&
            item.State == MaintenanceCurrencyState.Current &&
            item.SourceCheckId == "maintenance-fh-l2");
        Assert.Contains(summary.MaintenanceSummaries, item =>
            item.Branch == BranchCode.WM &&
            item.OwnedLevel == GlobalLevelId.L1 &&
            item.State == MaintenanceCurrencyState.Warning &&
            item.SourceCheckId == "maintenance-wm-l1");
        Assert.Contains(summary.ActiveBlockers, blocker =>
            blocker.Kind == LocalProgressBlockerKind.DecayedBranch &&
            blocker.Branch == BranchCode.IR &&
            blocker.Level == GlobalLevelId.L1);
        Assert.Contains(summary.ActiveBlockers, blocker =>
            blocker.Kind == LocalProgressBlockerKind.MaintenanceWarning &&
            blocker.Branch == BranchCode.WM &&
            blocker.Level == GlobalLevelId.L1);
        Assert.Equal(BranchCode.WM, summary.BottleneckBranch);
        Assert.Equal(LocalProgressEmphasisKind.RestoreDecayedBranch, summary.NextProgrammedEmphasis.Kind);
        Assert.Equal(BranchCode.IR, summary.NextProgrammedEmphasis.Branch);
        Assert.Contains(Source(LocalProgressSummarySourceKind.PractitionerState, "PractitionerState"), summary.SourceReferences);
        Assert.Contains(Source(LocalProgressSummarySourceKind.CompletedSession, "practice-fh"), summary.SourceReferences);
        Assert.Contains(Source(LocalProgressSummarySourceKind.CompletedSession, "maintenance-fh"), summary.SourceReferences);
        Assert.Contains(Source(LocalProgressSummarySourceKind.MaintenanceCheck, "maintenance-wm-l1"), summary.SourceReferences);
        Assert.Contains(Source(LocalProgressSummarySourceKind.FormalTestAttempt, "attempt-wm-fail"), summary.SourceReferences);
        Assert.Contains(Source(LocalProgressSummarySourceKind.EvidenceArtifact, "artifact-global-review"), summary.SourceReferences);
    }

    [Fact]
    public async Task RefreshReplacesCachedSummaryWhenUnderlyingPersistedFactsChange()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        await SavePractitionerStateAsync(
            databasePath,
            [new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned)]);
        var first = await CreateStore(databasePath).RefreshAsync(
            new LocalProgressSummaryRefreshRequest(
                "latest",
                TrainingDate.From(2026, 7, 4),
                TrainingDate.From(2026, 6, 28),
                TrainingDate.From(2026, 7, 4)));

        await SavePractitionerStateAsync(
            databasePath,
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Owned),
            ]);
        await SaveSessionAsync(databasePath, Session("practice-fh-l2", LocalCompletedSessionType.Practice, TrainingDate.From(2026, 7, 5)));
        var refreshed = await CreateStore(databasePath).RefreshAsync(
            new LocalProgressSummaryRefreshRequest(
                "latest",
                TrainingDate.From(2026, 7, 5),
                TrainingDate.From(2026, 6, 29),
                TrainingDate.From(2026, 7, 5)));

        var summaries = await CreateStore(databasePath).ListAsync();
        var loaded = await CreateStore(databasePath).LoadAsync("latest");

        Assert.Equal(GlobalLevelId.L1, BranchSummary(first, BranchCode.FH).HighestOwnedLevel);
        Assert.Single(summaries);
        Assert.NotNull(loaded);
        AssertEquivalent(refreshed, loaded);
        Assert.Equal(GlobalLevelId.L2, BranchSummary(refreshed, BranchCode.FH).HighestOwnedLevel);
        Assert.Equal(1, refreshed.CompletedSessionCount);
        Assert.Contains(Source(LocalProgressSummarySourceKind.CompletedSession, "practice-fh-l2"), refreshed.SourceReferences);
    }

    [Fact]
    public async Task SavedSummaryUsesStableIdsAndDoesNotPersistAnalyticsOrAuthoritativeDecisions()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        await SavePractitionerStateAsync(
            databasePath,
            [new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance)]);
        await SaveMaintenanceAsync(
            databasePath,
            Maintenance("maintenance-fh-l2", BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 4), passed: true));

        await CreateStore(databasePath).RefreshAsync(
            new LocalProgressSummaryRefreshRequest(
                "summary-20260704",
                TrainingDate.From(2026, 7, 4),
                TrainingDate.From(2026, 6, 28),
                TrainingDate.From(2026, 7, 4)));
        var storedJson = await File.ReadAllTextAsync(databasePath);

        Assert.Contains("\"ProgressSummaries\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Branch\": \"FH\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"HighestOwnedLevel\": \"L2\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"StateAtHighestOwnedLevel\": \"Maintenance\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"MaintenanceState\": \"Current\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"IsAuthoritative\": false", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Focus Hold", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Telemetry", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Analytics", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Account", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Sync", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Backend", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("MayAdvance", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("GateOutcome", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("OwnsProgressionLogic", storedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void SummaryRejectsMissingSourceReferencesBecauseItCannotBeTheSourceOfTruth()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new LocalProgressSummaryRecord(
                "summary-without-sources",
                TrainingDate.From(2026, 7, 4),
                TrainingDate.From(2026, 6, 28),
                TrainingDate.From(2026, 7, 4),
                isAuthoritative: false,
                [],
                [],
                [],
                bottleneckBranch: null,
                new LocalProgrammedEmphasis(LocalProgressEmphasisKind.ContinueMaintenance, null, null),
                completedSessionCount: 0,
                formalAttemptCount: 0,
                evidenceArtifactCount: 0,
                []));

        Assert.Contains("source", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalProgressSummaryStore CreateStore(string databasePath)
    {
        return new LocalProgressSummaryStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static async ValueTask SavePractitionerStateAsync(
        string databasePath,
        IEnumerable<BranchLevelStatus> statuses)
    {
        await new LocalPractitionerStateStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(new PractitionerState(statuses));
    }

    private static async ValueTask SaveSessionAsync(
        string databasePath,
        LocalSessionHistoryRecord record)
    {
        await new LocalSessionHistoryStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(record);
    }

    private static async ValueTask SaveMaintenanceAsync(
        string databasePath,
        LocalMaintenanceCheckRecord record)
    {
        await new LocalMaintenanceCheckStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveMaintenanceAsync(record);
    }

    private static async ValueTask SaveAttemptAsync(
        string databasePath,
        LocalFormalTestAttemptRecord record)
    {
        await new LocalFormalTestAttemptStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(record);
    }

    private static async ValueTask SaveArtifactAsync(
        string databasePath,
        LocalEvidenceArtifactRecord record)
    {
        await new LocalEvidenceArtifactStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath))
            .SaveAsync(record);
    }

    private static LocalSessionHistoryRecord Session(
        string sessionId,
        LocalCompletedSessionType sessionType,
        TrainingDate date)
    {
        return new LocalSessionHistoryRecord(
            sessionId,
            date,
            sessionType,
            [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L2)],
            DrillId.FH1TargetHold,
            null,
            LocalSessionIntensity.Moderate,
            [new LoadVariable("duration", "3 minutes")],
            cleanPerformance: true,
            notes: "clean set: drift count 3; return within standard",
            recoveryMarked: false,
            deloadMarked: false,
            [$"artifact-{sessionId}"]);
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
            completedSessionId: null,
            DrillId.FH1TargetHold,
            "Maintenance check preserves the owned standard at reduced volume.",
            new MaintenanceCheckEvidence(
                branch,
                level,
                date,
                MaintenanceCheckKind.StandardOrTransfer,
                Evaluation(passed)));
    }

    private static LocalFormalTestAttemptRecord Attempt(
        string attemptId,
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        FormalTestPassState passState)
    {
        var failed = passState == FormalTestPassState.Fail;
        var attempt = new FormalTestAttempt(
            branch,
            level,
            date,
            TestTask.ForDrill(DrillId.FH1TargetHold),
            [new LoadVariable("duration", "3 minutes")],
            "No more than 5 marked drifts; each return within 10 seconds; no target change.",
            [new CriticalConstraint("target cannot change")],
            new TestResultEvidence(TestResultEvidenceKind.PassFail, failed ? "fail" : "pass"),
            failed ? FailureType.Overload : null,
            passState,
            Artifact(
                EvidenceArtifactCategory.Test,
                date,
                failed ? ObservableEvidenceKind.FailedItemList : ObservableEvidenceKind.Score,
                failed ? "overload: drift count exceeded threshold" : "drifts: 4"));

        return new LocalFormalTestAttemptRecord(attemptId, $"artifact-{attemptId}", attempt);
    }

    private static LocalEvidenceArtifactRecord ArtifactRecord(
        string artifactId,
        LocalProgrammingEventKind eventKind,
        EvidenceArtifactCategory category,
        TrainingDate date,
        ObservableEvidenceKind evidenceKind,
        string evidence)
    {
        return new LocalEvidenceArtifactRecord(
            artifactId,
            new LocalProgrammingEventReference("global-review-20260704", eventKind),
            Artifact(category, date, evidenceKind, evidence));
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

    private static StandardEvaluationResult Evaluation(bool passed)
    {
        return passed
            ? new StandardEvaluationResult(true, [])
            : new StandardEvaluationResult(
                false,
                [new StandardEvaluationFailure(StandardFailureKind.NumericalThresholdMissed, "drift threshold missed")]);
    }

    private static LocalBranchProgressSummary BranchSummary(
        LocalProgressSummaryRecord summary,
        BranchCode branch)
    {
        return summary.BranchSummaries.Single(item => item.Branch == branch);
    }

    private static LocalProgressSummarySourceReference Source(
        LocalProgressSummarySourceKind kind,
        string sourceId)
    {
        return new LocalProgressSummarySourceReference(kind, sourceId);
    }

    private static void AssertEquivalent(
        LocalProgressSummaryRecord expected,
        LocalProgressSummaryRecord actual)
    {
        Assert.Equal(expected.SummaryId, actual.SummaryId);
        Assert.Equal(expected.GeneratedOn, actual.GeneratedOn);
        Assert.Equal(expected.PeriodStart, actual.PeriodStart);
        Assert.Equal(expected.PeriodEnd, actual.PeriodEnd);
        Assert.Equal(expected.IsAuthoritative, actual.IsAuthoritative);
        Assert.Equal(expected.BranchSummaries, actual.BranchSummaries);
        Assert.Equal(expected.MaintenanceSummaries, actual.MaintenanceSummaries);
        Assert.Equal(expected.ActiveBlockers, actual.ActiveBlockers);
        Assert.Equal(expected.BottleneckBranch, actual.BottleneckBranch);
        Assert.Equal(expected.NextProgrammedEmphasis, actual.NextProgrammedEmphasis);
        Assert.Equal(expected.CompletedSessionCount, actual.CompletedSessionCount);
        Assert.Equal(expected.FormalAttemptCount, actual.FormalAttemptCount);
        Assert.Equal(expected.EvidenceArtifactCount, actual.EvidenceArtifactCount);
        Assert.Equal(expected.SourceReferences, actual.SourceReferences);
    }
}
