using MentalGymnastics.App;
using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.App.Tests;

public sealed class SelectedWorkGeneratedContentPreparationTests
{
    [Fact]
    public void PreparesValidatedGeneratedContentForSelectedWork()
    {
        var selectedWork = new SelectedTrainingWork(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            AppTrainingSessionType.Practice,
            DemandFor(BranchCode.WM, GlobalLevelId.L1),
            StandardFor(BranchCode.WM, GlobalLevelId.L1),
            HonestyConstraintFor(DrillId.WM1DelayedReconstruction),
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("delay", "60 seconds"),
            ],
            advancementWorkAllowed: true);

        var result = new SelectedWorkGeneratedContentPreparer().Prepare(
            new SelectedWorkGeneratedContentPreparationRequest(
                selectedWork,
                PromptContentKind.DelayedReconstructionTask,
                "wm-l1-delayed-reconstruction",
                PromptFreshnessPolicy.FreshEquivalentRequired,
                new GeneratedContentSeed("app-selected-wm-l1"),
                TrainingDate.From(2026, 7, 5)));

        Assert.Equal(SelectedWorkGeneratedContentPreparationStatus.Prepared, result.Status);
        Assert.True(result.IsPrepared);
        Assert.Empty(result.Rejections);
        Assert.NotNull(result.GeneratedContent);
        Assert.NotNull(result.RuntimePackage);
        Assert.NotNull(result.PersistenceHandoff);
        Assert.Equal(selectedWork, result.SelectedWork);
        Assert.Equal(AppTrainingSessionType.Practice, result.AppSessionType);
        Assert.Equal(SessionType.Practice, result.ContentSessionType);
        Assert.Equal(BranchCode.WM, result.GeneratedContent.Result.Branch);
        Assert.Equal(GlobalLevelId.L1, result.GeneratedContent.Result.Level);
        Assert.Equal(DrillId.WM1DelayedReconstruction, result.GeneratedContent.Result.Drill);
        Assert.Equal(PromptContentKind.DelayedReconstructionTask, result.GeneratedContent.Result.ContentKind);
        Assert.Equal(PromptFreshnessPolicy.FreshEquivalentRequired, result.GeneratedContent.Result.Request.FreshnessPolicy);
        Assert.Equal("wm-l1-delayed-reconstruction", result.GeneratedContent.Result.EquivalenceClass);
        Assert.Equal(selectedWork.LoadVariables, result.GeneratedContent.Result.Request.LoadVariables);
        Assert.Equal(
            ProtocolCriticalConstraintsFor(selectedWork.Drill),
            result.GeneratedContent.Result.Request.CriticalConstraints);
        foreach (var constraint in ProtocolCriticalConstraintsFor(selectedWork.Drill))
        {
            Assert.Contains(
                result.GeneratedContent.Materials,
                material => material.Kind == GeneratedContentMaterialKind.HonestyConstraint &&
                    material.Value == constraint.Description);
        }
        Assert.Equal(selectedWork.Standard, result.RuntimePackage.Standard.Standard);
        Assert.Equal(selectedWork.LoadVariables, result.RuntimePackage.LoadVariables);
        Assert.Equal(result.GeneratedContent.Result.InstanceId, result.PersistenceHandoff.InstanceId);
        Assert.Equal(TrainingDate.From(2026, 7, 5), result.PersistenceHandoff.GeneratedOn);
        Assert.True(result.GeneratedContent.CanBeConsumedByRuntime);
        Assert.True(result.GeneratedContent.CanBeRecordedByPersistence);
    }

    [Fact]
    public void PreparesFreshEquivalentSelectedWorkThroughPackagedLocalContentBankWhenAvailable()
    {
        var selectedWork = new SelectedTrainingWork(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            AppTrainingSessionType.Practice,
            DemandFor(BranchCode.WM, GlobalLevelId.L1),
            StandardFor(BranchCode.WM, GlobalLevelId.L1),
            HonestyConstraintFor(DrillId.WM1DelayedReconstruction),
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("delay", "60 seconds"),
            ],
            advancementWorkAllowed: true);
        var previouslyUsedContentId = MvpLocalContentBank.Create().Entries
            .Where(entry =>
                entry.Branch == BranchCode.WM &&
                entry.Level == GlobalLevelId.L1 &&
                entry.Drill == DrillId.WM1DelayedReconstruction)
            .OrderBy(entry => entry.ContentId, StringComparer.Ordinal)
            .First()
            .ContentId;

        var result = new SelectedWorkGeneratedContentPreparer().Prepare(
            new SelectedWorkGeneratedContentPreparationRequest(
                selectedWork,
                PromptContentKind.DelayedReconstructionTask,
                "wm-l1-delayed-reconstruction",
                PromptFreshnessPolicy.FreshEquivalentRequired,
                new GeneratedContentSeed("app-selected-wm-l1-bank-fallback"),
                TrainingDate.From(2026, 7, 5),
                previouslyUsedContentIds: [previouslyUsedContentId]));

        Assert.Equal(SelectedWorkGeneratedContentPreparationStatus.Prepared, result.Status);
        Assert.NotNull(result.GeneratedContent);
        Assert.StartsWith(
            MvpLocalContentBank.BankId + ":",
            result.GeneratedContent!.Result.InstanceId,
            StringComparison.Ordinal);
        Assert.NotEqual(previouslyUsedContentId, result.GeneratedContent.Result.ContentId);
        Assert.Equal(MvpLocalContentBank.BankVersion, result.GeneratedContent.Result.ContentVersion);
        Assert.Equal(ProtocolCriticalConstraintsFor(selectedWork.Drill), result.GeneratedContent.Result.Request.CriticalConstraints);
        Assert.Contains(
            result.GeneratedContent.Materials,
            material => material.Kind == GeneratedContentMaterialKind.ExpectedReconstruction);
        Assert.NotNull(result.RuntimePackage);
        Assert.NotNull(result.PersistenceHandoff);
        Assert.Equal(result.GeneratedContent.Result.InstanceId, result.PersistenceHandoff!.InstanceId);
    }

    [Fact]
    public void RejectsNonEquivalentContentThatDoesNotMatchTheSelectedDrillDemand()
    {
        var selectedWork = new SelectedTrainingWork(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            AppTrainingSessionType.Practice,
            DemandFor(BranchCode.WM, GlobalLevelId.L1),
            StandardFor(BranchCode.WM, GlobalLevelId.L1),
            HonestyConstraintFor(DrillId.WM1DelayedReconstruction),
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("delay", "60 seconds"),
            ],
            advancementWorkAllowed: true);

        var result = new SelectedWorkGeneratedContentPreparer().Prepare(
            new SelectedWorkGeneratedContentPreparationRequest(
                selectedWork,
                PromptContentKind.CueSequence,
                "wm-l1-delayed-reconstruction",
                PromptFreshnessPolicy.FreshEquivalentRequired,
                new GeneratedContentSeed("app-selected-wm-l1-invalid"),
                TrainingDate.From(2026, 7, 5)));

        Assert.Equal(SelectedWorkGeneratedContentPreparationStatus.Rejected, result.Status);
        Assert.False(result.IsPrepared);
        Assert.Null(result.GeneratedContent);
        Assert.Null(result.RuntimePackage);
        Assert.Null(result.PersistenceHandoff);
        Assert.Contains(
            result.Rejections,
            rejection => rejection.Kind == SelectedWorkGeneratedContentRejectionKind.ContentSelectionRejected &&
                rejection.Detail.Contains(nameof(PromptContentKind.DelayedReconstructionTask), StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsInvalidGeneratedContentSelectionWithoutThrowingIntoAppWorkflow()
    {
        var selectedWork = new SelectedTrainingWork(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            AppTrainingSessionType.Practice,
            DemandFor(BranchCode.FH, GlobalLevelId.L1),
            StandardFor(BranchCode.FH, GlobalLevelId.L1),
            HonestyConstraintFor(DrillId.FH1TargetHold),
            [new LoadVariable("catalog-load", "Duration, target subtlety, distractor salience.")],
            advancementWorkAllowed: true);

        var result = new SelectedWorkGeneratedContentPreparer().Prepare(
            new SelectedWorkGeneratedContentPreparationRequest(
                selectedWork,
                PromptContentKind.EquivalentPrompt,
                "fh-l1-target-hold",
                PromptFreshnessPolicy.FreshEquivalentRequired,
                new GeneratedContentSeed("app-invalid-load"),
                TrainingDate.From(2026, 7, 5)));

        Assert.Equal(SelectedWorkGeneratedContentPreparationStatus.Rejected, result.Status);
        Assert.False(result.IsPrepared);
        Assert.Contains(
            result.Rejections,
            rejection => rejection.Kind == SelectedWorkGeneratedContentRejectionKind.ContentSelectionRejected &&
                rejection.Detail.Contains("Generated content cannot be consumed", StringComparison.Ordinal));
    }

    private static string DemandFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards
            .Single(standard => standard.Branch == branch && standard.Level == level)
            .Demand;
    }

    private static string StandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards
            .Single(standard => standard.Branch == branch && standard.Level == level)
            .Standard;
    }

    private static string HonestyConstraintFor(DrillId drill)
    {
        return ProgramCatalog.Drills.Single(definition => definition.Id == drill).HonestyConstraint;
    }

    private static IReadOnlyList<CriticalConstraint> ProtocolCriticalConstraintsFor(DrillId drill)
    {
        return DrillProtocolCatalog.StandardDrills
            .Single(protocol => protocol.Id == drill)
            .HonestyConstraints
            .Select(constraint => new CriticalConstraint(constraint.Description))
            .ToArray();
    }
}
