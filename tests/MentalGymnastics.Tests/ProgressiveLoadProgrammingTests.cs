using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class ProgressiveLoadProgrammingTests
{
    [Fact]
    public void ReducedDurationStageAdjustsPracticeDoseWithoutChangingFormalCatalog()
    {
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.FH, GlobalLevelId.L1);
        var formal = ExecutableStandardCatalog.Get(BranchCode.FH, GlobalLevelId.L1).EvaluatedStandard;

        var practice = TrainingLoadStageStandardResolver.Resolve(
            profile,
            profile.Stages[0].LoadVariables,
            formal);

        Assert.Equal(
            120,
            practice.NumericThresholds.Single(threshold =>
                threshold.MeasurementName == TrainingStandardMeasurements.ActiveDurationSeconds).Value);
        Assert.Equal(
            180,
            formal.NumericThresholds.Single(threshold =>
                threshold.MeasurementName == TrainingStandardMeasurements.ActiveDurationSeconds).Value);
        Assert.False(TrainingLoadStageStandardResolver.IsFormalStandardLoad(
            profile,
            profile.Stages[0].LoadVariables));
        Assert.True(TrainingLoadStageStandardResolver.IsFormalStandardLoad(
            profile,
            profile.TargetStage.LoadVariables));
    }

    [Fact]
    public void ReducedBranchCountStageRequiresEveryPrescribedComponent()
    {
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.TI, GlobalLevelId.L2);
        var formal = ExecutableStandardCatalog.Get(BranchCode.TI, GlobalLevelId.L2).EvaluatedStandard;

        var practice = TrainingLoadStageStandardResolver.Resolve(
            profile,
            profile.Stages[0].LoadVariables,
            formal);

        Assert.Equal(
            2,
            practice.NumericThresholds.Single(threshold =>
                threshold.MeasurementName == TrainingStandardMeasurements.ComponentCount).Value);
        Assert.Equal(
            3,
            formal.NumericThresholds.Single(threshold =>
                threshold.MeasurementName == TrainingStandardMeasurements.ComponentCount).Value);
    }
    [Fact]
    public void DefinesOneTwoStageProfileForEveryBranchLevel()
    {
        Assert.Equal(40, TrainingLoadProfileCatalog.Profiles.Count);
        Assert.All(TrainingLoadProfileCatalog.Profiles, profile =>
        {
            Assert.Equal(2, profile.Stages.Count);
            Assert.Null(profile.Stages[0].IncreasedVariable);
            Assert.False(string.IsNullOrWhiteSpace(profile.TargetStage.IncreasedVariable));
            Assert.NotEmpty(profile.TargetStage.LoadVariables);
            Assert.Equal(
                ExecutableStandardCatalog.Get(profile.Branch, profile.Level).Drill,
                profile.Drill);
        });
    }

    [Fact]
    public void TwoCleanExposuresIncreaseExactlyOneNamedVariable()
    {
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.WM, GlobalLevelId.L1);
        var regression = profile.Stages[0];

        var prescription = ProgressiveLoadPlanner.Prescribe(
            profile,
            SessionType.Load,
            [
                new TrainingLoadHistoryEntry(regression.LoadVariables, Clean: true, Overload: false),
                new TrainingLoadHistoryEntry(regression.LoadVariables, Clean: true, Overload: false),
            ]);

        Assert.True(prescription.IsFormalStandardLoad);
        Assert.Equal("item count", prescription.Stage.IncreasedVariable);
        Assert.Contains("Two clean exposures", prescription.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedWorkCannotIncreaseLoad()
    {
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.FH, GlobalLevelId.L2);

        var prescription = ProgressiveLoadPlanner.Prescribe(
            profile,
            SessionType.Load,
            [new TrainingLoadHistoryEntry(profile.Stages[0].LoadVariables, Clean: false, Overload: true)]);

        Assert.Equal(0, prescription.Stage.Index);
        Assert.False(prescription.IsFormalStandardLoad);
    }

    [Fact]
    public void FormalWorkAlwaysUsesTheExactTargetLoad()
    {
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.DE, GlobalLevelId.L2);

        var prescription = ProgressiveLoadPlanner.Prescribe(
            profile,
            SessionType.Test,
            history: []);

        Assert.True(prescription.IsFormalStandardLoad);
        Assert.Contains(
            prescription.Stage.LoadVariables,
            variable => variable.Name == "item quantity" && variable.Value == "20");
    }
}
