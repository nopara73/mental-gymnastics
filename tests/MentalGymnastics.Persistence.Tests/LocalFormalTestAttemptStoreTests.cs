using System.Text.Json.Nodes;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalFormalTestAttemptStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadReturnsNullAndHistoryIsEmptyWhenNoFormalAttemptsHaveBeenSaved()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        var loaded = await store.LoadAsync("missing-attempt");
        var history = await store.ListAsync();
        var branchLevelHistory = await store.ListByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L1);

        Assert.Null(loaded);
        Assert.Empty(history);
        Assert.Empty(branchLevelHistory);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task SaveLoadAndListRoundTripFormalDrillAttemptWithRequiredProgrammingFields()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var expected = new LocalFormalTestAttemptRecord(
            "attempt-fh-l1-20260704",
            "artifact-fh-l1-20260704",
            PassingDrillAttempt());

        await CreateStore(databasePath).SaveAsync(expected);

        var loaded = await CreateStore(databasePath).LoadAsync(expected.AttemptId);
        var history = await CreateStore(databasePath).ListAsync();

        Assert.NotNull(loaded);
        AssertEquivalent(expected, loaded);
        Assert.Single(history);
        AssertEquivalent(expected, Assert.Single(history));
    }

    [Fact]
    public async Task LoadsLegacyAttemptThatContainsFailureModeDeclaration()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var expected = new LocalFormalTestAttemptRecord(
            "legacy-attempt",
            "legacy-artifact",
            PassingDrillAttempt());
        await CreateStore(databasePath).SaveAsync(expected);

        var document = JsonNode.Parse(await File.ReadAllTextAsync(databasePath))!.AsObject();
        document["FormalTestAttempts"]!.AsArray()[0]!.AsObject()["Attempt"]!
            .AsObject()["MainFailureModeAvoided"] = "unmarked drift";
        await File.WriteAllTextAsync(databasePath, document.ToJsonString());

        var loaded = await CreateStore(databasePath).LoadAsync(expected.AttemptId);

        Assert.NotNull(loaded);
        AssertEquivalent(expected, loaded);
    }

    [Fact]
    public async Task SaveLoadAndQueryPreserveTransferTaskFailureClassificationAndRubricResult()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var unrelated = new LocalFormalTestAttemptRecord(
            "attempt-fh-l1-pass",
            "artifact-fh-l1-pass",
            PassingDrillAttempt());
        var expected = new LocalFormalTestAttemptRecord(
            "attempt-fh-l4-transfer-fail",
            "artifact-fh-l4-transfer-fail",
            FailedTransferAttempt());
        var store = CreateStore(databasePath);

        await store.SaveAsync(unrelated);
        await store.SaveAsync(expected);

        var loaded = await CreateStore(databasePath).LoadAsync(expected.AttemptId);
        var focusHoldL4History = await CreateStore(databasePath).ListByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L4);

        Assert.NotNull(loaded);
        AssertEquivalent(expected, loaded);
        Assert.Null(loaded.Attempt.Task.Drill);
        Assert.Equal("Hold target during WM task", loaded.Attempt.Task.TransferTask);
        Assert.Equal(TestResultEvidenceKind.Rubric, loaded.Attempt.ResultEvidence.Kind);
        Assert.Equal("branch score below pass", loaded.Attempt.ResultEvidence.Value);
        Assert.Equal(FailureType.Overload, loaded.Attempt.FailureType);
        Assert.Equal(FormalTestPassState.Fail, loaded.Attempt.PassState);
        Assert.Single(focusHoldL4History);
        AssertEquivalent(expected, Assert.Single(focusHoldL4History));
    }

    [Fact]
    public async Task ListByBranchLevelReturnsAttemptHistoryInSaveOrder()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var first = new LocalFormalTestAttemptRecord(
            "attempt-fh-l1-first",
            "artifact-fh-l1-first",
            PassingDrillAttempt(TrainingDate.From(2026, 7, 4), FormalTestPassState.PassOnce));
        var second = new LocalFormalTestAttemptRecord(
            "attempt-fh-l1-stabilization",
            "artifact-fh-l1-stabilization",
            PassingDrillAttempt(TrainingDate.From(2026, 7, 11), FormalTestPassState.StabilizationPass));
        var otherBranch = new LocalFormalTestAttemptRecord(
            "attempt-wm-l1",
            "artifact-wm-l1",
            PassingWorkingMemoryAttempt());

        await store.SaveAsync(first);
        await store.SaveAsync(otherBranch);
        await store.SaveAsync(second);

        var focusHoldHistory = await CreateStore(databasePath).ListByBranchLevelAsync(BranchCode.FH, GlobalLevelId.L1);

        Assert.Equal(["attempt-fh-l1-first", "attempt-fh-l1-stabilization"], focusHoldHistory.Select(record => record.AttemptId));
    }

    [Fact]
    public async Task SaveReplacesAttemptWithSameIdentifierWithoutDuplicatingHistory()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var original = new LocalFormalTestAttemptRecord(
            "attempt-fh-l1",
            "artifact-fh-l1-original",
            PassingDrillAttempt(TrainingDate.From(2026, 7, 4), FormalTestPassState.PassOnce));
        var replacement = new LocalFormalTestAttemptRecord(
            "attempt-fh-l1",
            "artifact-fh-l1-replacement",
            PassingDrillAttempt(TrainingDate.From(2026, 7, 11), FormalTestPassState.StabilizationPass));

        await store.SaveAsync(original);
        await store.SaveAsync(replacement);

        var history = await CreateStore(databasePath).ListAsync();
        var loaded = await CreateStore(databasePath).LoadAsync("attempt-fh-l1");

        Assert.Single(history);
        Assert.NotNull(loaded);
        AssertEquivalent(replacement, loaded);
    }

    [Fact]
    public async Task SavedAttemptsUseStableIdentifiersAndDoNotPersistDerivedGateDecisions()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var record = new LocalFormalTestAttemptRecord(
            "attempt-fh-l1-20260704",
            "artifact-fh-l1-20260704",
            PassingDrillAttempt());

        await CreateStore(databasePath).SaveAsync(record);
        var storedJson = await File.ReadAllTextAsync(databasePath);

        Assert.Contains("\"Branch\": \"FH\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Level\": \"L1\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Drill\": \"FH1TargetHold\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"ResultKind\": \"PassFail\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"PassState\": \"PassOnce\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"EvidenceArtifactId\": \"artifact-fh-l1-20260704\"", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Focus Hold", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Protected Control", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("GateOutcome", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("MayAdvance", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("OwnsLevel", storedJson, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalFormalTestAttemptStore CreateStore(string databasePath)
    {
        return new LocalFormalTestAttemptStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static FormalTestAttempt PassingDrillAttempt()
    {
        return PassingDrillAttempt(TrainingDate.From(2026, 7, 4), FormalTestPassState.PassOnce);
    }

    private static FormalTestAttempt PassingDrillAttempt(
        TrainingDate date,
        FormalTestPassState passState)
    {
        return new FormalTestAttempt(
            BranchCode.FH,
            GlobalLevelId.L1,
            date,
            TestTask.ForDrill(DrillId.FH1TargetHold),
            [new LoadVariable("duration", "3 minutes")],
            "No more than 5 marked drifts; each return within 10 seconds; no target change.",
            [new CriticalConstraint("target cannot change")],
            new TestResultEvidence(TestResultEvidenceKind.PassFail, "pass"),
            null,
            passState,
            Artifact(EvidenceArtifactCategory.Test, date, ObservableEvidenceKind.Score, "drifts: 4"));
    }

    private static FormalTestAttempt PassingWorkingMemoryAttempt()
    {
        var date = TrainingDate.From(2026, 7, 5);
        return new FormalTestAttempt(
            BranchCode.WM,
            GlobalLevelId.L1,
            date,
            TestTask.ForDrill(DrillId.WM1DelayedReconstruction),
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("delay", "60 seconds"),
            ],
            "At least 4 of 5 exact; no invented items.",
            [new CriticalConstraint("no invented items")],
            new TestResultEvidence(TestResultEvidenceKind.Score, "5/5"),
            null,
            FormalTestPassState.PassOnce,
            Artifact(EvidenceArtifactCategory.Test, date, ObservableEvidenceKind.Reconstruction, "5 of 5 exact"));
    }

    private static FormalTestAttempt FailedTransferAttempt()
    {
        var date = TrainingDate.From(2026, 7, 12);
        return new FormalTestAttempt(
            BranchCode.FH,
            GlobalLevelId.L4,
            date,
            TestTask.ForTransfer("Hold target during WM task"),
            [new LoadVariable("transfer distance", "near transfer")],
            "Maintain stated target while completing WM or DE task; branch score remains passing.",
            [new CriticalConstraint("source branch score must remain passing")],
            new TestResultEvidence(TestResultEvidenceKind.Rubric, "branch score below pass"),
            FailureType.Overload,
            FormalTestPassState.Fail,
            Artifact(EvidenceArtifactCategory.Transfer, date, ObservableEvidenceKind.BranchMapping, "source FH standard visible inside WM task"));
    }

    private static EvidenceArtifact Artifact(
        EvidenceArtifactCategory category,
        TrainingDate date,
        ObservableEvidenceKind evidenceKind,
        string evidenceValue)
    {
        return new EvidenceArtifact(
            category,
            date,
            [
                new ObservableEvidence(evidenceKind, evidenceValue),
                new ObservableEvidence(ObservableEvidenceKind.CriticalConstraintRecord, "critical constraint preserved"),
            ],
            $"{category} formal attempt artifact summary");
    }

    private static void AssertEquivalent(
        LocalFormalTestAttemptRecord expected,
        LocalFormalTestAttemptRecord actual)
    {
        Assert.Equal(expected.AttemptId, actual.AttemptId);
        Assert.Equal(expected.EvidenceArtifactId, actual.EvidenceArtifactId);
        AssertAttemptEquivalent(expected.Attempt, actual.Attempt);
    }

    private static void AssertAttemptEquivalent(FormalTestAttempt expected, FormalTestAttempt actual)
    {
        Assert.Equal(expected.Branch, actual.Branch);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Date, actual.Date);
        Assert.Equal(expected.Task.Drill, actual.Task.Drill);
        Assert.Equal(expected.Task.TransferTask, actual.Task.TransferTask);
        Assert.Equal(expected.LoadVariables, actual.LoadVariables);
        Assert.Equal(expected.Standard, actual.Standard);
        Assert.Equal(expected.CriticalConstraints, actual.CriticalConstraints);
        Assert.Equal(expected.ResultEvidence, actual.ResultEvidence);
        Assert.Equal(expected.FailureType, actual.FailureType);
        Assert.Equal(expected.PassState, actual.PassState);
        Assert.Equal(expected.Artifact.Category, actual.Artifact.Category);
        Assert.Equal(expected.Artifact.Date, actual.Artifact.Date);
        Assert.Equal(expected.Artifact.ObservableEvidence, actual.Artifact.ObservableEvidence);
        Assert.Equal(expected.Artifact.SummaryOrReference, actual.Artifact.SummaryOrReference);
        Assert.Equal(expected.Artifact.SubjectiveNote, actual.Artifact.SubjectiveNote);
    }
}
