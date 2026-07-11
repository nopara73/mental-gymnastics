using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App.Tests;

public sealed class CurrentTrainingStateLoaderTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadsCurrentTrainingStateFromPersistenceForFutureScreens()
    {
        var configuration = Configuration();
        var state = State(
        [
            Status(BranchCode.FH, GlobalLevelId.L2, BranchLevelState.Maintenance),
            Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
            Status(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training),
        ]);
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(state);
        await new LocalEvidenceArtifactStore(configuration.LocalDatabaseOptions).SaveAsync(
            ArtifactRecord(
                "artifact-fh-practice",
                "session-fh-practice",
                LocalProgrammingEventKind.Practice,
                EvidenceArtifactCategory.Practice,
                TrainingDate.From(2026, 7, 4),
                BranchCode.FH,
                GlobalLevelId.L2));
        await new LocalSessionHistoryStore(configuration.LocalDatabaseOptions).SaveAsync(
            Session(
                "session-fh-practice",
                TrainingDate.From(2026, 7, 4),
                BranchCode.FH,
                GlobalLevelId.L2,
                "artifact-fh-practice"));

        var readModel = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(
                TrainingDate.From(2026, 7, 5),
                recentSessionLimit: 5,
                evidenceSummaryLimit: 5,
                progressRecordLimit: 5));

        Assert.Equal(state.BranchLevels, readModel.CurrentPractitionerState.BranchLevels);
        Assert.Equal(state.BranchLevels, readModel.BranchLevelStates);
        Assert.Equal(["session-fh-practice"], readModel.RecentSessions.Select(session => session.SessionId));
        Assert.Equal(["artifact-fh-practice"], readModel.EvidenceSummaries.Select(summary => summary.ArtifactId));
        Assert.Contains(
            readModel.DueMaintenance,
            due => due.BranchLevel.Branch == BranchCode.FH &&
                due.BranchLevel.Level == GlobalLevelId.L2 &&
                due.Currency.State == MaintenanceCurrencyState.Due);
    }

    [Fact]
    public async Task ExposesCoreDerivedBlockedAdvancementAndNextWork()
    {
        var configuration = Configuration();
        var state = State(
        [
            Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.CO, GlobalLevelId.L1, BranchLevelState.Training),
        ]);
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(state);
        await new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions).SaveMaintenanceAsync(
            Maintenance("maintenance-fh-current", BranchCode.FH, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1), passed: true));
        await new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions).SaveMaintenanceAsync(
            Maintenance("maintenance-fs-current", BranchCode.FS, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1), passed: true));
        await new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions).SaveMaintenanceAsync(
            Maintenance("maintenance-wm-stale", BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 6, 20), passed: true));
        await new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions).SaveMaintenanceAsync(
            Maintenance("maintenance-ir-current", BranchCode.IR, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1), passed: true));
        await new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions).SaveMaintenanceAsync(
            Maintenance("maintenance-de-current", BranchCode.DE, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1), passed: true));

        var readModel = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(TrainingDate.From(2026, 7, 5)));

        Assert.Equal(PractitionerCategory.Beginner, readModel.CategoryClassification.Category);
        Assert.Contains(
            readModel.BlockedAdvancement,
            blocker => blocker.Source == CurrentTrainingStateBlockerSource.Category &&
                blocker.CategoryBlockerKind == PractitionerCategoryBlockerKind.MaintenanceNotCurrent &&
                blocker.Branch == BranchCode.WM);
        Assert.Contains(
            readModel.BlockedAdvancement,
            blocker => blocker.Source == CurrentTrainingStateBlockerSource.WeeklyProgramming &&
                blocker.WeeklyConstraintKind == WeeklyProgrammingConstraintKind.MaintenanceNotCurrent &&
                blocker.Branch == BranchCode.WM);
        Assert.False(readModel.WeeklyPlan.AdvancementWorkAllowed);
        Assert.Contains(
            readModel.AvailableNextWork,
            work => work.Session == WeeklySessionKind.Maintenance &&
                work.BranchEmphasis.SequenceEqual([BranchCode.WM]));
        Assert.DoesNotContain(readModel.AvailableNextWork, work => work.IsAdvancementWork);
    }

    [Fact]
    public async Task ExposesCoreGlobalReviewInputsAndDecisions()
    {
        var configuration = Configuration();
        var state = State(
        [
            Status(BranchCode.WM, GlobalLevelId.L2, BranchLevelState.Decayed),
        ]);
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(state);

        var readModel = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(TrainingDate.From(2026, 7, 5)));

        Assert.Equal(
            Enum.GetValues<BranchCode>(),
            readModel.GlobalReview.Input.CurrentOwnedLevels.Select(level => level.Branch));
        Assert.False(readModel.GlobalReview.Evaluation.Passed);
        Assert.Contains(
            readModel.GlobalReview.Evaluation.Failures,
            failure => failure.Kind == GlobalReviewFailureKind.PrerequisiteBranchDecayed &&
                failure.Branch == BranchCode.WM &&
                failure.Level == GlobalLevelId.L2);
        Assert.Contains(
            readModel.GlobalReview.Evaluation.Decisions,
            decision => decision.Kind == GlobalReviewDecisionKind.RestoreDecayedBranch &&
                decision.Branch == BranchCode.WM);
    }

    [Fact]
    public async Task DerivesRecoveryFromTwoConsecutiveOverloadSets()
    {
        var configuration = Configuration();
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(State(
        [
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
        ]));
        var sessions = new LocalSessionHistoryStore(configuration.LocalDatabaseOptions);
        await sessions.SaveAsync(Session(
            "overload-set-1",
            TrainingDate.From(2026, 7, 4),
            BranchCode.FH,
            GlobalLevelId.L1,
            "artifact-overload-1",
            LocalCompletedSessionType.Load,
            cleanPerformance: false));
        await sessions.SaveAsync(Session(
            "overload-set-2",
            TrainingDate.From(2026, 7, 4),
            BranchCode.FH,
            GlobalLevelId.L1,
            "artifact-overload-2",
            LocalCompletedSessionType.Load,
            cleanPerformance: false));

        var readModel = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(TrainingDate.From(2026, 7, 5)));

        Assert.True(readModel.RecoveryRequired);
        Assert.True(readModel.RecoveryDecision!.ShouldRecover);
        Assert.Contains(
            RecoveryTriggerKind.TwoConsecutiveOverloadSetFailures,
            readModel.RecoveryDecision.Triggers);
        Assert.Contains(
            readModel.WeeklyPlan.Constraints,
            constraint => constraint.Kind == WeeklyProgrammingConstraintKind.RecoveryRequired);
    }

    [Fact]
    public async Task DerivesDeloadWhenTwoBranchesShowOverloadInTheWeek()
    {
        var configuration = Configuration();
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(State(
        [
            Status(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training),
            Status(BranchCode.FS, GlobalLevelId.L1, BranchLevelState.Training),
        ]));
        var sessions = new LocalSessionHistoryStore(configuration.LocalDatabaseOptions);
        await sessions.SaveAsync(Session(
            "overload-fh",
            TrainingDate.From(2026, 7, 3),
            BranchCode.FH,
            GlobalLevelId.L1,
            "artifact-overload-fh",
            LocalCompletedSessionType.Load,
            cleanPerformance: false));
        await sessions.SaveAsync(Session(
            "overload-fs",
            TrainingDate.From(2026, 7, 4),
            BranchCode.FS,
            GlobalLevelId.L1,
            "artifact-overload-fs",
            LocalCompletedSessionType.Load,
            cleanPerformance: false,
            drill: DrillId.FS1CueSwitch));

        var readModel = await new CurrentTrainingStateLoader(configuration).LoadAsync(
            new CurrentTrainingStateQuery(TrainingDate.From(2026, 7, 5)));

        Assert.True(readModel.DeloadDecision.ShouldDeload);
        Assert.True(readModel.RecoveryRequired);
        Assert.Contains(
            DeloadTriggerKind.TwoOrMoreBranchesShowOverloadOrDecayInSameWeek,
            readModel.DeloadDecision.Triggers);
        Assert.False(readModel.WeeklyPlan.AdvancementWorkAllowed);
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

    private static PractitionerState State(IEnumerable<BranchLevelStatus> statuses)
    {
        return new PractitionerState(statuses);
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static LocalSessionHistoryRecord Session(
        string sessionId,
        TrainingDate date,
        BranchCode branch,
        GlobalLevelId level,
        string evidenceArtifactId,
        LocalCompletedSessionType sessionType = LocalCompletedSessionType.Practice,
        bool cleanPerformance = true,
        DrillId drill = DrillId.FH1TargetHold)
    {
        return new LocalSessionHistoryRecord(
            sessionId,
            date,
            sessionType,
            [new LocalSessionBranchLevel(branch, level)],
            drill,
            null,
            LocalSessionIntensity.Moderate,
            [new LoadVariable("duration", "3 minutes")],
            cleanPerformance,
            notes: $"{sessionId} observable set record.",
            recoveryMarked: false,
            deloadMarked: false,
            evidenceArtifactIds: [evidenceArtifactId]);
    }

    private static LocalEvidenceArtifactRecord ArtifactRecord(
        string artifactId,
        string eventId,
        LocalProgrammingEventKind eventKind,
        EvidenceArtifactCategory category,
        TrainingDate date,
        BranchCode branch,
        GlobalLevelId level)
    {
        return new LocalEvidenceArtifactRecord(
            artifactId,
            new LocalProgrammingEventReference(
                eventId,
                eventKind,
                branch,
                level,
                DrillId.FH1TargetHold),
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
