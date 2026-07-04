using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalStabilizationPassStoreTests : IDisposable
{
    private const string FocusHoldL1Standard =
        "No more than 5 marked drifts; each return within 10 seconds; no target change.";

    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadReturnsNullAndHistoryIsEmptyWhenNoStabilizationPassesHaveBeenSaved()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        var loaded = await store.LoadAsync("missing-pass");
        var history = await store.ListAsync();
        var evidence = await store.LoadEvidenceAsync(BranchCode.FH, GlobalLevelId.L1);

        Assert.Null(loaded);
        Assert.Empty(history);
        Assert.Empty(evidence.Passes);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task SaveLoadAndListRoundTripStabilizationPassRecordAfterRestart()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var expected = PassRecord(
            "fh-l1-pass-once",
            FormalTestPassState.PassOnce,
            TrainingDate.From(2026, 7, 1),
            evidenceArtifactId: "artifact-fh-l1-pass-once",
            formalTestAttemptId: "attempt-fh-l1-pass-once",
            completedSessionId: "session-fh-l1-pass-once");

        await CreateStore(databasePath).SaveAsync(expected);

        var loaded = await CreateStore(databasePath).LoadAsync(expected.PassId);
        var history = await CreateStore(databasePath).ListAsync();

        Assert.NotNull(loaded);
        AssertEquivalent(expected, loaded);
        Assert.Single(history);
        AssertEquivalent(expected, Assert.Single(history));
    }

    [Fact]
    public async Task ListByBranchLevelReturnsOnlyMatchingPassesInDateOrder()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var latest = PassRecord("latest", FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 8));
        var otherBranch = PassRecord(
            "wm-pass",
            FormalTestPassState.PassOnce,
            TrainingDate.From(2026, 7, 2),
            branch: BranchCode.WM,
            drill: DrillId.WM1DelayedReconstruction,
            standard: "At least 4 of 5 exact; no invented items.",
            mainFailureModeAvoided: "invented item");
        var earliest = PassRecord("earliest", FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1));

        await store.SaveAsync(latest);
        await store.SaveAsync(otherBranch);
        await store.SaveAsync(earliest);

        var focusHoldHistory = await CreateStore(databasePath).ListByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L1);
        var workingMemoryHistory = await CreateStore(databasePath).ListByBranchLevelAsync(BranchCode.WM, GlobalLevelId.L1);

        Assert.Equal(["earliest", "latest"], focusHoldHistory.Select(record => record.PassId));
        Assert.Equal("wm-pass", Assert.Single(workingMemoryHistory).PassId);
    }

    [Fact]
    public async Task LoadedEvidenceCanBeSuppliedToCoreOwnershipEvaluator()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        await store.SaveAsync(PassRecord("pass-once", FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1)));
        await store.SaveAsync(PassRecord("stabilization-one", FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 4)));
        await store.SaveAsync(PassRecord(
            "stabilization-two",
            FormalTestPassState.StabilizationPass,
            TrainingDate.From(2026, 7, 8),
            afterAdjacentWorkOrControlledDistractor: true,
            condition: LocalStabilizationCondition.AdjacentWork));

        var evidence = await CreateStore(databasePath).LoadEvidenceAsync(BranchCode.FH, GlobalLevelId.L1);
        var ownership = StabilizationOwnershipEvaluator.Evaluate(evidence);

        Assert.Equal(3, evidence.Passes.Count);
        Assert.True(ownership.IsOwned);
        Assert.Equal(GateOutcome.Own, ownership.GateOutcome);
        Assert.Equal(BranchLevelState.Owned, ownership.BranchLevelState);
    }

    [Fact]
    public async Task LoadedEvidenceKeepsPassedOnceDistinctFromOwnedWhenOnlyOnePassExists()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        await CreateStore(databasePath).SaveAsync(
            PassRecord("single-peak", FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1)));

        var evidence = await CreateStore(databasePath).LoadEvidenceAsync(BranchCode.FH, GlobalLevelId.L1);
        var ownership = StabilizationOwnershipEvaluator.Evaluate(evidence);

        Assert.Single(evidence.Passes);
        Assert.False(ownership.IsOwned);
        Assert.Equal(GateOutcome.PassOnce, ownership.GateOutcome);
        Assert.Equal(BranchLevelState.PassedOnce, ownership.BranchLevelState);
    }

    [Fact]
    public async Task AdjacentWorkAndControlledDistractorConditionsSurviveRestart()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var adjacent = PassRecord(
            "adjacent-work",
            FormalTestPassState.StabilizationPass,
            TrainingDate.From(2026, 7, 4),
            afterAdjacentWorkOrControlledDistractor: true,
            condition: LocalStabilizationCondition.AdjacentWork,
            conditionDescription: "after short WM delayed reconstruction set");
        var distractor = PassRecord(
            "controlled-distractor",
            FormalTestPassState.StabilizationPass,
            TrainingDate.From(2026, 7, 8),
            afterAdjacentWorkOrControlledDistractor: true,
            condition: LocalStabilizationCondition.ControlledDistractor,
            conditionDescription: "periodic irrelevant prompts present");
        var store = CreateStore(databasePath);

        await store.SaveAsync(adjacent);
        await store.SaveAsync(distractor);

        var loaded = await CreateStore(databasePath).ListByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L1);

        Assert.Equal(LocalStabilizationCondition.AdjacentWork, loaded[0].Condition);
        Assert.Equal("after short WM delayed reconstruction set", loaded[0].ConditionDescription);
        Assert.Equal(LocalStabilizationCondition.ControlledDistractor, loaded[1].Condition);
        Assert.Equal("periodic irrelevant prompts present", loaded[1].ConditionDescription);
        Assert.All(loaded, record => Assert.True(record.Evidence.AfterAdjacentWorkOrControlledDistractor));
    }

    [Fact]
    public async Task SaveReplacesPassWithSameIdentifierWithoutDuplicatingHistory()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var original = PassRecord("same-pass", FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1));
        var replacement = PassRecord(
            "same-pass",
            FormalTestPassState.StabilizationPass,
            TrainingDate.From(2026, 7, 4),
            afterAdjacentWorkOrControlledDistractor: true,
            condition: LocalStabilizationCondition.ControlledDistractor);

        await store.SaveAsync(original);
        await store.SaveAsync(replacement);

        var history = await CreateStore(databasePath).ListAsync();
        var loaded = await CreateStore(databasePath).LoadAsync("same-pass");

        Assert.Single(history);
        Assert.NotNull(loaded);
        AssertEquivalent(replacement, loaded);
    }

    [Fact]
    public async Task SavedPassesUseStableDomainIdentifiersAndDoNotPersistOwnershipDecision()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var record = PassRecord(
            "fh-l1-stabilization",
            FormalTestPassState.StabilizationPass,
            TrainingDate.From(2026, 7, 8),
            afterAdjacentWorkOrControlledDistractor: true,
            condition: LocalStabilizationCondition.ControlledDistractor);

        await CreateStore(databasePath).SaveAsync(record);
        var storedJson = await File.ReadAllTextAsync(databasePath);

        Assert.Contains("\"Branch\": \"FH\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Level\": \"L1\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Drill\": \"FH1TargetHold\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"PassState\": \"StabilizationPass\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Condition\": \"ControlledDistractor\"", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Focus Hold", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("OwnsLevel", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Ownership", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("GateOutcome", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Account", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Telemetry", storedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void StabilizationPassRecordRequiresEvidenceArtifactReference()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            PassRecord("missing-evidence", FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 4), evidenceArtifactId: " "));

        Assert.Contains("Evidence artifact", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalStabilizationPassStore CreateStore(string databasePath)
    {
        return new LocalStabilizationPassStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static LocalStabilizationPassRecord PassRecord(
        string passId,
        FormalTestPassState passState,
        TrainingDate date,
        BranchCode branch = BranchCode.FH,
        GlobalLevelId level = GlobalLevelId.L1,
        DrillId drill = DrillId.FH1TargetHold,
        string standard = FocusHoldL1Standard,
        bool standardPassed = true,
        bool afterAdjacentWorkOrControlledDistractor = false,
        LocalStabilizationCondition condition = LocalStabilizationCondition.OrdinaryVariance,
        string conditionDescription = "ordinary weekly variance",
        string mainFailureModeAvoided = "target substitution",
        string evidenceArtifactId = "artifact-default",
        string? formalTestAttemptId = null,
        string? completedSessionId = null)
    {
        return new LocalStabilizationPassRecord(
            passId,
            evidenceArtifactId,
            formalTestAttemptId,
            completedSessionId,
            drill,
            condition,
            conditionDescription,
            new StabilizationPassEvidence(
                branch,
                level,
                date,
                standard,
                passState,
                standardPassed
                    ? new StandardEvaluationResult(true, [])
                    : new StandardEvaluationResult(
                        false,
                        [new StandardEvaluationFailure(StandardFailureKind.CriticalConstraintBroken, "target changed")]),
                afterAdjacentWorkOrControlledDistractor,
                mainFailureModeAvoided));
    }

    private static void AssertEquivalent(
        LocalStabilizationPassRecord expected,
        LocalStabilizationPassRecord actual)
    {
        Assert.Equal(expected.PassId, actual.PassId);
        Assert.Equal(expected.EvidenceArtifactId, actual.EvidenceArtifactId);
        Assert.Equal(expected.FormalTestAttemptId, actual.FormalTestAttemptId);
        Assert.Equal(expected.CompletedSessionId, actual.CompletedSessionId);
        Assert.Equal(expected.Drill, actual.Drill);
        Assert.Equal(expected.Condition, actual.Condition);
        Assert.Equal(expected.ConditionDescription, actual.ConditionDescription);
        AssertEvidenceEquivalent(expected.Evidence, actual.Evidence);
    }

    private static void AssertEvidenceEquivalent(
        StabilizationPassEvidence expected,
        StabilizationPassEvidence actual)
    {
        Assert.Equal(expected.Branch, actual.Branch);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Date, actual.Date);
        Assert.Equal(expected.Standard, actual.Standard);
        Assert.Equal(expected.PassState, actual.PassState);
        Assert.Equal(expected.StandardEvaluationResult.Passed, actual.StandardEvaluationResult.Passed);
        Assert.Equal(expected.StandardEvaluationResult.Failures, actual.StandardEvaluationResult.Failures);
        Assert.Equal(expected.AfterAdjacentWorkOrControlledDistractor, actual.AfterAdjacentWorkOrControlledDistractor);
        Assert.Equal(expected.MainFailureModeAvoided, actual.MainFailureModeAvoided);
    }
}
