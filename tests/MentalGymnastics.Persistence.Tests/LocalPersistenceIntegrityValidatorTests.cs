using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalPersistenceIntegrityValidatorTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ValidProgrammingDataProducesNoIntegrityIssues()
    {
        var databasePath = DatabasePath("valid.db");
        await SeedValidProgrammingDataAsync(databasePath);

        var report = await Validator(databasePath).ValidateAsync();

        Assert.True(report.IsValid);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public async Task MissingEvidenceRecordsAndInvalidRecordReferencesAreReported()
    {
        var databasePath = DatabasePath("missing-references.db");
        await SeedValidProgrammingDataAsync(databasePath);
        await MutateDatabaseAsync(databasePath, document =>
        {
            ((JsonObject)document["FormalTestAttempts"]![0]!)["EvidenceArtifactId"] = "missing-artifact";
            ((JsonObject)document["StabilizationPasses"]![0]!)["FormalTestAttemptId"] = "missing-attempt";
        });

        var report = await Validator(databasePath).ValidateAsync();

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue =>
            issue.Kind == LocalPersistenceIntegrityIssueKind.MissingRequiredRecord &&
            issue.Section == "FormalTestAttempts" &&
            issue.RecordId == "attempt-fh-l1" &&
            issue.Detail.Contains("missing-artifact", StringComparison.Ordinal));
        Assert.Contains(report.Issues, issue =>
            issue.Kind == LocalPersistenceIntegrityIssueKind.InvalidReference &&
            issue.Section == "StabilizationPasses" &&
            issue.RecordId == "stabilization-fh-l1" &&
            issue.Detail.Contains("missing-attempt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnknownBranchAndLevelIdentifiersAreReported()
    {
        var databasePath = DatabasePath("unknown-identifiers.db");
        await SeedValidProgrammingDataAsync(databasePath);
        await MutateDatabaseAsync(databasePath, document =>
        {
            var branchLevel = (JsonObject)((JsonObject)document["PractitionerState"]!)["BranchLevels"]![0]!;
            branchLevel["Branch"] = "ZZ";
            branchLevel["Level"] = "L9";
        });

        var report = await Validator(databasePath).ValidateAsync();

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue =>
            issue.Kind == LocalPersistenceIntegrityIssueKind.UnknownIdentifier &&
            issue.Detail.Contains("Branch", StringComparison.Ordinal) &&
            issue.Detail.Contains("ZZ", StringComparison.Ordinal));
        Assert.Contains(report.Issues, issue =>
            issue.Kind == LocalPersistenceIntegrityIssueKind.UnknownIdentifier &&
            issue.Detail.Contains("Level", StringComparison.Ordinal) &&
            issue.Detail.Contains("L9", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImpossiblePersistedStateHistoryIsReportedThroughCoreStateMachineValidation()
    {
        var databasePath = DatabasePath("impossible-state.db");
        await SeedValidProgrammingDataAsync(databasePath);
        await MutateDatabaseAsync(databasePath, document =>
        {
            var decay = (JsonObject)document["DecayHistory"]![0]!;
            ((JsonObject)decay["CurrentStatus"]!)["State"] = "Training";
        });

        var report = await Validator(databasePath).ValidateAsync();

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue =>
            issue.Kind == LocalPersistenceIntegrityIssueKind.ImpossiblePersistedState &&
            issue.Section == "DecayHistory" &&
            issue.RecordId == "decay-fh-l1" &&
            issue.Detail.Contains("MarkDecayed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OrphanedEvidenceIsReportedWhenItIsNotReferencedByAProgrammingEvent()
    {
        var databasePath = DatabasePath("orphaned-evidence.db");
        await SeedValidProgrammingDataAsync(databasePath);
        await new LocalEvidenceArtifactStore(Options(databasePath)).SaveAsync(
            new LocalEvidenceArtifactRecord(
                "artifact-orphaned",
                new LocalProgrammingEventReference(
                    "missing-session",
                    LocalProgrammingEventKind.Practice,
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    DrillId.FH1TargetHold),
                Artifact(EvidenceArtifactCategory.Practice, "orphaned score: 4 drifts")));

        var report = await Validator(databasePath).ValidateAsync();

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue =>
            issue.Kind == LocalPersistenceIntegrityIssueKind.OrphanedEvidence &&
            issue.Section == "EvidenceArtifacts" &&
            issue.RecordId == "artifact-orphaned");
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string DatabasePath(string fileName)
    {
        Directory.CreateDirectory(tempDirectory);
        return Path.Combine(tempDirectory, fileName);
    }

    private static LocalDatabaseOptions Options(string databasePath)
    {
        return LocalDatabaseOptions.ForAppOwnedPath(databasePath);
    }

    private static LocalPersistenceIntegrityValidator Validator(string databasePath)
    {
        return new LocalPersistenceIntegrityValidator(Options(databasePath));
    }

    private static async ValueTask MutateDatabaseAsync(
        string databasePath,
        Action<JsonObject> mutate)
    {
        var storedJson = await File.ReadAllTextAsync(databasePath);
        var document = JsonNode.Parse(storedJson)?.AsObject();
        Assert.NotNull(document);
        mutate(document);

        await File.WriteAllTextAsync(databasePath, document.ToJsonString(JsonOptions));
    }

    private static async ValueTask SeedValidProgrammingDataAsync(string databasePath)
    {
        await new LocalPractitionerStateStore(Options(databasePath)).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
                new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training),
            ]));
        await new LocalBranchLevelStateStore(Options(databasePath)).SaveAllAsync(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training),
        ]);
        await new LocalSessionHistoryStore(Options(databasePath)).SaveAsync(SessionRecord());

        await SaveArtifactAsync(databasePath, "artifact-session-fh-l1", "session-fh-l1", LocalProgrammingEventKind.Practice, EvidenceArtifactCategory.Practice);
        await SaveArtifactAsync(databasePath, "artifact-attempt-fh-l1", "attempt-fh-l1", LocalProgrammingEventKind.FormalTest, EvidenceArtifactCategory.Test);
        await SaveArtifactAsync(databasePath, "artifact-stabilization-fh-l1", "stabilization-fh-l1", LocalProgrammingEventKind.Stabilization, EvidenceArtifactCategory.Stabilization);
        await SaveArtifactAsync(databasePath, "artifact-maintenance-fh-l1", "maintenance-fh-l1", LocalProgrammingEventKind.Maintenance, EvidenceArtifactCategory.Maintenance);
        await SaveArtifactAsync(databasePath, "artifact-restoration-last-owned", "restoration-check-last-owned", LocalProgrammingEventKind.Maintenance, EvidenceArtifactCategory.Maintenance);
        await SaveArtifactAsync(databasePath, "artifact-restoration-lower-load", "restoration-check-lower-load", LocalProgrammingEventKind.Maintenance, EvidenceArtifactCategory.Maintenance);
        await SaveArtifactAsync(databasePath, "artifact-generated-result", "instance-fh-l1", LocalProgrammingEventKind.Practice, EvidenceArtifactCategory.Practice);

        await new LocalFormalTestAttemptStore(Options(databasePath)).SaveAsync(AttemptRecord());
        await new LocalStabilizationPassStore(Options(databasePath)).SaveAsync(StabilizationRecord());
        await new LocalMaintenanceCheckStore(Options(databasePath)).SaveMaintenanceAsync(MaintenanceRecord());
        await new LocalMaintenanceCheckStore(Options(databasePath)).SaveRestorationAsync(RestorationCheckRecord("restoration-check-last-owned", "artifact-restoration-last-owned", RestorationCheckKind.LastOwnedStandard));
        await new LocalMaintenanceCheckStore(Options(databasePath)).SaveRestorationAsync(RestorationCheckRecord("restoration-check-lower-load", "artifact-restoration-lower-load", RestorationCheckKind.LowerLoadTransferCheck));
        await new LocalDecayRestorationHistoryStore(Options(databasePath)).SaveDecayAsync(DecayRecord());
        await new LocalDecayRestorationHistoryStore(Options(databasePath)).SaveRestorationAsync(RestorationHistoryRecord());
        await new LocalGeneratedDrillInstanceStore(Options(databasePath)).SaveAsync(GeneratedInstanceRecord());
        await new LocalProgressSummaryStore(Options(databasePath)).SaveAsync(ProgressSummaryRecord());
    }

    private static async ValueTask SaveArtifactAsync(
        string databasePath,
        string artifactId,
        string eventId,
        LocalProgrammingEventKind eventKind,
        EvidenceArtifactCategory category)
    {
        await new LocalEvidenceArtifactStore(Options(databasePath)).SaveAsync(
            new LocalEvidenceArtifactRecord(
                artifactId,
                new LocalProgrammingEventReference(
                    eventId,
                    eventKind,
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    DrillId.FH1TargetHold),
                Artifact(category, $"{artifactId}: observable score")));
    }

    private static LocalSessionHistoryRecord SessionRecord()
    {
        return new LocalSessionHistoryRecord(
            "session-fh-l1",
            TrainingDate.From(2026, 7, 4),
            LocalCompletedSessionType.Practice,
            [new LocalSessionBranchLevel(BranchCode.FH, GlobalLevelId.L1)],
            DrillId.FH1TargetHold,
            null,
            LocalSessionIntensity.Moderate,
            [new LoadVariable("duration", "3 minutes")],
            cleanPerformance: true,
            notes: "clean FH L1 set with 4 marked drifts",
            recoveryMarked: false,
            deloadMarked: false,
            ["artifact-session-fh-l1"]);
    }

    private static LocalFormalTestAttemptRecord AttemptRecord()
    {
        var attempt = new FormalTestAttempt(
            BranchCode.FH,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 4),
            TestTask.ForDrill(DrillId.FH1TargetHold),
            [new LoadVariable("duration", "3 minutes")],
            "No more than 5 marked drifts; each return within 10 seconds; no target change.",
            [new CriticalConstraint("target cannot change")],
            new TestResultEvidence(TestResultEvidenceKind.PassFail, "pass"),
            null,
            FormalTestPassState.PassOnce,
            Artifact(EvidenceArtifactCategory.Test, "formal test drifts: 4"));

        return new LocalFormalTestAttemptRecord("attempt-fh-l1", "artifact-attempt-fh-l1", attempt);
    }

    private static LocalStabilizationPassRecord StabilizationRecord()
    {
        return new LocalStabilizationPassRecord(
            "stabilization-fh-l1",
            "artifact-stabilization-fh-l1",
            "attempt-fh-l1",
            "session-fh-l1",
            DrillId.FH1TargetHold,
            LocalStabilizationCondition.AdjacentWork,
            "after short WM set",
            new StabilizationPassEvidence(
                BranchCode.FH,
                GlobalLevelId.L1,
                TrainingDate.From(2026, 7, 6),
                "No more than 5 marked drifts; each return within 10 seconds; no target change.",
                FormalTestPassState.StabilizationPass,
                PassedEvaluation(),
                afterAdjacentWorkOrControlledDistractor: true));
    }

    private static LocalMaintenanceCheckRecord MaintenanceRecord()
    {
        return new LocalMaintenanceCheckRecord(
            "maintenance-fh-l1",
            "artifact-maintenance-fh-l1",
            "session-fh-l1",
            DrillId.FH1TargetHold,
            "FH L1 maintenance check at reduced volume.",
            new MaintenanceCheckEvidence(
                BranchCode.FH,
                GlobalLevelId.L1,
                TrainingDate.From(2026, 7, 7),
                MaintenanceCheckKind.StandardOrTransfer,
                PassedEvaluation()));
    }

    private static LocalRestorationCheckRecord RestorationCheckRecord(
        string checkId,
        string artifactId,
        RestorationCheckKind kind)
    {
        return new LocalRestorationCheckRecord(
            checkId,
            artifactId,
            "session-fh-l1",
            DrillId.FH1TargetHold,
            "Restoration check preserves FH standard.",
            new RestorationCheckEvidence(
                BranchCode.FH,
                GlobalLevelId.L1,
                TrainingDate.From(2026, 7, 9),
                kind,
                PassedEvaluation()));
    }

    private static LocalDecayHistoryRecord DecayRecord()
    {
        return new LocalDecayHistoryRecord(
            "decay-fh-l1",
            TrainingDate.From(2026, 7, 8),
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Decayed),
            BranchLevelTransition.MarkDecayed,
            ["maintenance-fh-l1"]);
    }

    private static LocalRestorationHistoryRecord RestorationHistoryRecord()
    {
        var current = new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Decayed);
        var evidence = new RestorationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [
                new RestorationCheckEvidence(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    TrainingDate.From(2026, 7, 9),
                    RestorationCheckKind.LastOwnedStandard,
                    PassedEvaluation()),
                new RestorationCheckEvidence(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    TrainingDate.From(2026, 7, 10),
                    RestorationCheckKind.LowerLoadTransferCheck,
                    PassedEvaluation()),
            ]);
        var result = DecayRestorationEvaluator.EvaluateRestoration(current, evidence);

        return new LocalRestorationHistoryRecord(
            "restoration-fh-l1",
            TrainingDate.From(2026, 7, 10),
            result.CurrentStatus,
            result.NextStatus,
            result.Transition!.Value,
            ["restoration-check-last-owned", "restoration-check-lower-load"],
            evidence);
    }

    private static LocalGeneratedDrillInstanceRecord GeneratedInstanceRecord()
    {
        return new LocalGeneratedDrillInstanceRecord(
            "instance-fh-l1",
            TrainingDate.From(2026, 7, 4),
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")],
            new LocalGeneratedDrillContentIdentity(
                "content-fh-l1-001",
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold,
                PromptContentKind.CueSequence,
                "FH1-target-hold",
                "v1"),
            LocalGeneratedDrillInstanceState.Completed,
            resultEvidenceArtifactId: "artifact-generated-result");
    }

    private static LocalProgressSummaryRecord ProgressSummaryRecord()
    {
        return new LocalProgressSummaryRecord(
            "summary-20260704",
            TrainingDate.From(2026, 7, 4),
            TrainingDate.From(2026, 6, 28),
            TrainingDate.From(2026, 7, 4),
            isAuthoritative: false,
            branchSummaries:
            [
                new LocalBranchProgressSummary(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Maintenance),
            ],
            maintenanceSummaries:
            [
                new LocalMaintenanceProgressSummary(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    MaintenanceCurrencyState.Current,
                    "maintenance-fh-l1",
                    DaysSinceLastPassingCheck: 0,
                    ConsecutiveFailures: 0),
            ],
            activeBlockers: [],
            bottleneckBranch: null,
            new LocalProgrammedEmphasis(LocalProgressEmphasisKind.ContinueMaintenance, BranchCode.FH, GlobalLevelId.L1),
            completedSessionCount: 1,
            formalAttemptCount: 1,
            evidenceArtifactCount: 6,
            sourceReferences:
            [
                new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.PractitionerState, "PractitionerState"),
                new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.CompletedSession, "session-fh-l1"),
                new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.FormalTestAttempt, "attempt-fh-l1"),
                new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.MaintenanceCheck, "maintenance-fh-l1"),
                new LocalProgressSummarySourceReference(LocalProgressSummarySourceKind.EvidenceArtifact, "artifact-session-fh-l1"),
            ]);
    }

    private static EvidenceArtifact Artifact(EvidenceArtifactCategory category, string evidence)
    {
        return new EvidenceArtifact(
            category,
            TrainingDate.From(2026, 7, 4),
            [new ObservableEvidence(ObservableEvidenceKind.Score, evidence)],
            evidence);
    }

    private static StandardEvaluationResult PassedEvaluation()
    {
        return new StandardEvaluationResult(true, []);
    }
}
