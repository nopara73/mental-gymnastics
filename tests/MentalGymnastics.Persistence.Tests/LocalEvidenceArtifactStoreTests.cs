using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalEvidenceArtifactStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Persistence.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadReturnsNullAndListReturnsEmptyWhenNoEvidenceArtifactsHaveBeenSaved()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);

        var loaded = await store.LoadAsync("missing-artifact");
        var listed = await store.ListAsync();

        Assert.Null(loaded);
        Assert.Empty(listed);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task SaveLoadAndListRoundTripEvidenceArtifactsForAllProgramCategories()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var records = new[]
        {
            Record("practice-artifact", LocalProgrammingEventKind.Practice, EvidenceArtifactCategory.Practice, ObservableEvidenceKind.OutputSample),
            Record("load-artifact", LocalProgrammingEventKind.Load, EvidenceArtifactCategory.Load, ObservableEvidenceKind.LoadVariableRecord),
            Record("formal-test-artifact", LocalProgrammingEventKind.FormalTest, EvidenceArtifactCategory.Test, ObservableEvidenceKind.Score),
            Record("stabilization-artifact", LocalProgrammingEventKind.Stabilization, EvidenceArtifactCategory.Stabilization, ObservableEvidenceKind.RepeatabilityRecord),
            Record("transfer-artifact", LocalProgrammingEventKind.Transfer, EvidenceArtifactCategory.Transfer, ObservableEvidenceKind.BranchMapping),
            Record("maintenance-artifact", LocalProgrammingEventKind.Maintenance, EvidenceArtifactCategory.Maintenance, ObservableEvidenceKind.MaintenanceCheck),
            Record("global-review-artifact", LocalProgrammingEventKind.GlobalReview, EvidenceArtifactCategory.GlobalReview, ObservableEvidenceKind.GlobalReviewSummary),
        };
        var store = CreateStore(databasePath);

        foreach (var record in records)
        {
            await store.SaveAsync(record);
        }

        var listed = await CreateStore(databasePath).ListAsync();

        Assert.Equal(records.Select(record => record.ArtifactId), listed.Select(record => record.ArtifactId));
        foreach (var expected in records)
        {
            var loaded = await CreateStore(databasePath).LoadAsync(expected.ArtifactId);
            Assert.NotNull(loaded);
            AssertEquivalent(expected, loaded);
        }
    }

    [Fact]
    public async Task SavedArtifactsUseStableIdentifiersForCategoriesAndObservableEvidenceKinds()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var record = Record(
            "formal-test-artifact",
            LocalProgrammingEventKind.FormalTest,
            EvidenceArtifactCategory.Test,
            ObservableEvidenceKind.Score);

        await CreateStore(databasePath).SaveAsync(record);
        var storedJson = await File.ReadAllTextAsync(databasePath);

        Assert.Contains("\"Category\": \"Test\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"Kind\": \"Score\"", storedJson, StringComparison.Ordinal);
        Assert.Contains("\"EventKind\": \"FormalTest\"", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Formal test", storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Score evidence", storedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListForEventReturnsOnlyArtifactsAssociatedWithThatProgrammingEvent()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var store = CreateStore(databasePath);
        var sharedEventId = "formal-test-fh-l1-20260704";
        var first = Record(
            "formal-test-score",
            sharedEventId,
            LocalProgrammingEventKind.FormalTest,
            EvidenceArtifactCategory.Test,
            ObservableEvidenceKind.Score);
        var second = Record(
            "formal-test-constraint",
            sharedEventId,
            LocalProgrammingEventKind.FormalTest,
            EvidenceArtifactCategory.Test,
            ObservableEvidenceKind.CriticalConstraintRecord);
        var unrelated = Record(
            "maintenance-check",
            "maintenance-fh-l2-20260711",
            LocalProgrammingEventKind.Maintenance,
            EvidenceArtifactCategory.Maintenance,
            ObservableEvidenceKind.MaintenanceCheck);

        await store.SaveAsync(first);
        await store.SaveAsync(second);
        await store.SaveAsync(unrelated);

        var associated = await CreateStore(databasePath).ListForEventAsync(sharedEventId);

        Assert.Equal(["formal-test-score", "formal-test-constraint"], associated.Select(record => record.ArtifactId));
        Assert.All(associated, record => Assert.Equal(sharedEventId, record.Event.EventId));
        Assert.All(associated, record => Assert.Equal(LocalProgrammingEventKind.FormalTest, record.Event.Kind));
    }

    [Fact]
    public async Task SaveRejectsVagueEncouragementPretendingToBeObservableEvidence()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var vagueRecord = new LocalEvidenceArtifactRecord(
            "vague-practice",
            Event(
                "practice-fh-l1-20260704",
                LocalProgrammingEventKind.Practice,
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold),
            new EvidenceArtifact(
                EvidenceArtifactCategory.Practice,
                TrainingDate.From(2026, 7, 4),
                [new ObservableEvidence(ObservableEvidenceKind.OutputSample, "good effort")],
                "nice work"));

        await Assert.ThrowsAsync<ArgumentException>(async () => await CreateStore(databasePath).SaveAsync(vagueRecord));
        Assert.Empty(await CreateStore(databasePath).ListAsync());
    }

    [Fact]
    public async Task SaveRejectsArtifactWhenCategoryDoesNotMatchProgrammingEventKind()
    {
        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var mismatchedRecord = new LocalEvidenceArtifactRecord(
            "mismatched-artifact",
            Event(
                "practice-fh-l1-20260704",
                LocalProgrammingEventKind.Practice,
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold),
            Artifact(EvidenceArtifactCategory.Test, ObservableEvidenceKind.Score));

        await Assert.ThrowsAsync<ArgumentException>(async () => await CreateStore(databasePath).SaveAsync(mismatchedRecord));
        Assert.Empty(await CreateStore(databasePath).ListAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalEvidenceArtifactStore CreateStore(string databasePath)
    {
        return new LocalEvidenceArtifactStore(LocalDatabaseOptions.ForAppOwnedPath(databasePath));
    }

    private static LocalEvidenceArtifactRecord Record(
        string artifactId,
        LocalProgrammingEventKind eventKind,
        EvidenceArtifactCategory category,
        ObservableEvidenceKind evidenceKind)
    {
        return Record(
            artifactId,
            $"{eventKind}-event",
            eventKind,
            category,
            evidenceKind);
    }

    private static LocalEvidenceArtifactRecord Record(
        string artifactId,
        string eventId,
        LocalProgrammingEventKind eventKind,
        EvidenceArtifactCategory category,
        ObservableEvidenceKind evidenceKind)
    {
        var eventReference = eventKind == LocalProgrammingEventKind.GlobalReview
            ? Event(eventId, eventKind)
            : Event(eventId, eventKind, BranchCode.FH, GlobalLevelId.L1, DrillId.FH1TargetHold);

        return new LocalEvidenceArtifactRecord(
            artifactId,
            eventReference,
            Artifact(category, evidenceKind));
    }

    private static LocalProgrammingEventReference Event(
        string eventId,
        LocalProgrammingEventKind eventKind,
        BranchCode? branch = null,
        GlobalLevelId? level = null,
        DrillId? drill = null)
    {
        return new LocalProgrammingEventReference(eventId, eventKind, branch, level, drill);
    }

    private static EvidenceArtifact Artifact(
        EvidenceArtifactCategory category,
        ObservableEvidenceKind evidenceKind)
    {
        return new EvidenceArtifact(
            category,
            TrainingDate.From(2026, 7, 4),
            [new ObservableEvidence(evidenceKind, $"{evidenceKind}: observable value recorded")],
            $"{category} artifact summary",
            subjectiveNote: "felt difficult, not used as evidence");
    }

    private static void AssertEquivalent(
        LocalEvidenceArtifactRecord expected,
        LocalEvidenceArtifactRecord actual)
    {
        Assert.Equal(expected.ArtifactId, actual.ArtifactId);
        Assert.Equal(expected.Event.EventId, actual.Event.EventId);
        Assert.Equal(expected.Event.Kind, actual.Event.Kind);
        Assert.Equal(expected.Event.Branch, actual.Event.Branch);
        Assert.Equal(expected.Event.Level, actual.Event.Level);
        Assert.Equal(expected.Event.Drill, actual.Event.Drill);
        Assert.Equal(expected.Artifact.Category, actual.Artifact.Category);
        Assert.Equal(expected.Artifact.Date, actual.Artifact.Date);
        Assert.Equal(expected.Artifact.SummaryOrReference, actual.Artifact.SummaryOrReference);
        Assert.Equal(expected.Artifact.SubjectiveNote, actual.Artifact.SubjectiveNote);
        Assert.Equal(expected.Artifact.ObservableEvidence, actual.Artifact.ObservableEvidence);
    }
}
