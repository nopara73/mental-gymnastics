using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class EvidenceArtifactTests
{
    [Fact]
    public void ExposesDocumentedEvidenceArtifactCategories()
    {
        Assert.Equal(
            new[]
            {
                "Practice",
                "Load",
                "Test",
                "Stabilization",
                "Transfer",
                "Maintenance",
                "GlobalReview",
            },
            Enum.GetNames<EvidenceArtifactCategory>());
    }

    [Fact]
    public void RepresentsEvidenceArtifactsForProgramSessionAndReviewCategories()
    {
        var artifacts = new[]
        {
            Artifact(EvidenceArtifactCategory.Practice, ObservableEvidenceKind.OutputSample),
            Artifact(EvidenceArtifactCategory.Load, ObservableEvidenceKind.LoadVariableRecord),
            Artifact(EvidenceArtifactCategory.Test, ObservableEvidenceKind.Score),
            Artifact(EvidenceArtifactCategory.Stabilization, ObservableEvidenceKind.RepeatabilityRecord),
            Artifact(EvidenceArtifactCategory.Transfer, ObservableEvidenceKind.BranchMapping),
            Artifact(EvidenceArtifactCategory.Maintenance, ObservableEvidenceKind.MaintenanceCheck),
            Artifact(EvidenceArtifactCategory.GlobalReview, ObservableEvidenceKind.GlobalReviewSummary),
        };

        Assert.Equal(
            Enum.GetValues<EvidenceArtifactCategory>(),
            artifacts.Select(artifact => artifact.Category));
        Assert.All(artifacts, artifact => Assert.NotEmpty(artifact.ObservableEvidence));
    }

    [Fact]
    public void RejectsArtifactThatOnlyContainsSubjectiveSelfDescription()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new EvidenceArtifact(
                EvidenceArtifactCategory.Practice,
                TrainingDate.From(2026, 7, 4),
                [],
                "felt more focused today"));

        Assert.Contains("observable evidence", exception.Message);
    }

    [Fact]
    public void FormalTestAttemptRepresentsRequiredProgrammingRecord()
    {
        var artifact = new EvidenceArtifact(
            EvidenceArtifactCategory.Test,
            TrainingDate.From(2026, 7, 4),
            [
                new ObservableEvidence(ObservableEvidenceKind.Score, "drifts: 4"),
                new ObservableEvidence(ObservableEvidenceKind.Time, "3 minutes"),
                new ObservableEvidence(ObservableEvidenceKind.OutputSample, "hold log retained as short summary"),
            ],
            "FH L1 formal attempt summary");

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
            artifact);

        Assert.Equal(BranchCode.FH, attempt.Branch);
        Assert.Equal(GlobalLevelId.L1, attempt.Level);
        Assert.Equal(DrillId.FH1TargetHold, attempt.Task.Drill);
        Assert.Null(attempt.Task.TransferTask);
        Assert.Equal("duration", Assert.Single(attempt.LoadVariables).Name);
        Assert.Equal("target cannot change", Assert.Single(attempt.CriticalConstraints).Description);
        Assert.Equal(TestResultEvidenceKind.PassFail, attempt.ResultEvidence.Kind);
        Assert.Null(attempt.FailureType);
        Assert.Equal(FormalTestPassState.PassOnce, attempt.PassState);
        Assert.Equal(artifact, attempt.Artifact);
    }

    [Fact]
    public void FormalTransferAttemptCanRepresentTransferTaskAndFailureClassification()
    {
        var artifact = new EvidenceArtifact(
            EvidenceArtifactCategory.Transfer,
            TrainingDate.From(2026, 7, 5),
            [
                new ObservableEvidence(ObservableEvidenceKind.BranchMapping, "source FH standard visible inside WM task"),
                new ObservableEvidence(ObservableEvidenceKind.ErrorCount, "branch score failed after distractor"),
            ],
            "transfer attempt summary",
            subjectiveNote: "felt difficult but this note is not the evidence");

        var attempt = new FormalTestAttempt(
            BranchCode.FH,
            GlobalLevelId.L4,
            TrainingDate.From(2026, 7, 5),
            TestTask.ForTransfer("Hold target during WM task"),
            [new LoadVariable("transfer distance", "near transfer")],
            "Maintain stated target while completing WM or DE task; branch score remains passing.",
            [new CriticalConstraint("source branch score must remain passing")],
            new TestResultEvidence(TestResultEvidenceKind.Rubric, "branch score below pass"),
            FailureType.Overload,
            FormalTestPassState.Fail,
            artifact);

        Assert.Null(attempt.Task.Drill);
        Assert.Equal("Hold target during WM task", attempt.Task.TransferTask);
        Assert.Equal(FailureType.Overload, attempt.FailureType);
        Assert.Equal(FormalTestPassState.Fail, attempt.PassState);
        Assert.NotEmpty(attempt.Artifact.ObservableEvidence);
        Assert.NotNull(attempt.Artifact.SubjectiveNote);
    }

    private static EvidenceArtifact Artifact(
        EvidenceArtifactCategory category,
        ObservableEvidenceKind evidenceKind)
    {
        return new EvidenceArtifact(
            category,
            TrainingDate.From(2026, 7, 4),
            [new ObservableEvidence(evidenceKind, $"{category} observable record")],
            $"{category} artifact summary");
    }
}
