using MentalGymnastics.App;
using MentalGymnastics.Core;

namespace MentalGymnastics.App.Tests;

public sealed class DefaultTrainingWorkPolicyTests
{
    [Fact]
    public void IntermediateRetestSlotRoutesTestReadyLevelsToTheirFormalGate()
    {
        Assert.Equal(
            AppTrainingSessionType.Test,
            DefaultTrainingWorkPolicy.SessionTypeFor(
                WeeklySessionKind.RecoveryOrRetest,
                BranchLevelState.TestReady,
                GlobalLevelId.L3));
        Assert.Equal(
            AppTrainingSessionType.Transfer,
            DefaultTrainingWorkPolicy.SessionTypeFor(
                WeeklySessionKind.RecoveryOrRetest,
                BranchLevelState.TestReady,
                GlobalLevelId.L4));
        Assert.Equal(
            AppTrainingSessionType.Stabilization,
            DefaultTrainingWorkPolicy.SessionTypeFor(
                WeeklySessionKind.RecoveryOrRetest,
                BranchLevelState.PassedOnce,
                GlobalLevelId.L4));
    }

    [Fact]
    public void TransferOrStabilizationUsesTransferForTheL4FormalGateOnly()
    {
        Assert.Equal(
            AppTrainingSessionType.Transfer,
            DefaultTrainingWorkPolicy.SessionTypeFor(
                WeeklySessionKind.TransferOrStabilization,
                BranchLevelState.TestReady,
                GlobalLevelId.L4));
        Assert.Equal(
            AppTrainingSessionType.Practice,
            DefaultTrainingWorkPolicy.SessionTypeFor(
                WeeklySessionKind.TransferOrStabilization,
                BranchLevelState.TestReady,
                GlobalLevelId.L3));
    }

    [Fact]
    public void MaintenanceUsesTheReducedLoadStage()
    {
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.FH, GlobalLevelId.L2);

        var prescription = ProgressiveLoadPlanner.Prescribe(
            profile,
            DefaultTrainingWorkPolicy.CoreSessionTypeFor(AppTrainingSessionType.Maintenance));

        Assert.Equal(profile.Stages[0], prescription.Stage);
        Assert.NotEqual(profile.TargetStage, prescription.Stage);
    }
}
