using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class DailyTrainingProgrammingTests
{
    [Fact]
    public void CalendarDateSelectsExactlyOneWeeklyProgramDay()
    {
        var plan = BeginnerPlan();
        var anchor = TrainingDate.From(2026, 7, 6);

        var first = DailyTrainingProgrammingPlanner.Prescribe(anchor, anchor, plan);
        var sixth = DailyTrainingProgrammingPlanner.Prescribe(TrainingDate.From(2026, 7, 11), anchor, plan);
        var nextCycle = DailyTrainingProgrammingPlanner.Prescribe(TrainingDate.From(2026, 7, 13), anchor, plan);

        Assert.Equal(1, first.CycleDay);
        Assert.Equal(WeeklySessionKind.Practice, first.Session);
        Assert.Equal([BranchCode.FH, BranchCode.FS, BranchCode.WM], first.BranchEmphasis);
        Assert.Equal(6, sixth.CycleDay);
        Assert.Equal(WeeklySessionKind.TestOrStabilization, sixth.Session);
        Assert.Equal(1, nextCycle.CycleDay);
    }

    [Fact]
    public void MissedDatesDoNotCreateBacklog()
    {
        var plan = BeginnerPlan();
        var anchor = TrainingDate.From(2026, 7, 6);

        var dayFive = DailyTrainingProgrammingPlanner.Prescribe(
            TrainingDate.From(2026, 7, 10),
            anchor,
            plan);

        Assert.Equal(5, dayFive.CycleDay);
        Assert.Equal([BranchCode.WM, BranchCode.IR, BranchCode.DE], dayFive.BranchEmphasis);
    }

    [Fact]
    public void DailyDoseAllowsSetupCancellationButNeverRestartsTerminalWork()
    {
        var planned = DailyTrainingDoseProgress.Planned(requiredBlockCount: 3);
        var cancelled = DailyTrainingDoseStateMachine.TryApply(
            planned,
            DailyTrainingDoseTransition.CancelSetup);
        var started = DailyTrainingDoseStateMachine.TryApply(
            cancelled.Next,
            DailyTrainingDoseTransition.StartActiveWork);
        var first = DailyTrainingDoseStateMachine.TryApply(
            started.Next,
            DailyTrainingDoseTransition.RecordTerminalBlock);
        var second = DailyTrainingDoseStateMachine.TryApply(
            first.Next,
            DailyTrainingDoseTransition.RecordTerminalBlock);
        var completed = DailyTrainingDoseStateMachine.TryApply(
            second.Next,
            DailyTrainingDoseTransition.RecordTerminalBlock);
        var restart = DailyTrainingDoseStateMachine.TryApply(
            completed.Next,
            DailyTrainingDoseTransition.StartActiveWork);

        Assert.True(cancelled.IsValid);
        Assert.Equal(DailyTrainingDoseState.Planned, cancelled.Next.State);
        Assert.Equal(2, first.Next.RemainingBlockCount);
        Assert.Equal(DailyTrainingDoseState.Completed, completed.Next.State);
        Assert.False(restart.IsValid);
    }

    [Fact]
    public void ExplicitStopConsumesTheRemainingDailyDose()
    {
        var started = DailyTrainingDoseStateMachine.TryApply(
            DailyTrainingDoseProgress.Planned(2),
            DailyTrainingDoseTransition.StartActiveWork).Next;

        var stopped = DailyTrainingDoseStateMachine.TryApply(
            started,
            DailyTrainingDoseTransition.StopRemainingWork);

        Assert.True(stopped.IsValid);
        Assert.True(stopped.Next.IsTerminal);
        Assert.Equal(DailyTrainingDoseState.Stopped, stopped.Next.State);
        Assert.Equal(2, stopped.Next.RemainingBlockCount);
    }

    [Fact]
    public void ExplicitStopCanConsumeAnUnstartedDailyDose()
    {
        var stopped = DailyTrainingDoseStateMachine.TryApply(
            DailyTrainingDoseProgress.Planned(3),
            DailyTrainingDoseTransition.StopRemainingWork);

        Assert.True(stopped.IsValid);
        Assert.Equal(DailyTrainingDoseState.Stopped, stopped.Next.State);
        Assert.Equal(3, stopped.Next.RemainingBlockCount);
    }

    [Fact]
    public void ActiveDoseCannotAlreadyHaveEveryBlockTerminal()
    {
        Assert.Throws<ArgumentException>(() => new DailyTrainingDoseProgress(
            requiredBlockCount: 2,
            terminalBlockCount: 2,
            DailyTrainingDoseState.Active));
    }

    private static WeeklyPlan BeginnerPlan()
    {
        var classification = new PractitionerCategoryClassificationResult(
            PractitionerCategory.Beginner,
            []);
        return WeeklyProgrammingPlanner.Generate(new WeeklyProgrammingRequest(
            classification,
            maintenanceCurrency: [],
            globalReviewDecisions: [],
            recoveryRequired: false,
            selectedFoundationalLoadBranch: BranchCode.FH,
            weakestFoundationalBranch: BranchCode.FS,
            selectedAdvancedBranch: BranchCode.CO,
            prerequisiteSupportBranch: BranchCode.WM,
            eligibleAdvancementBranch: BranchCode.FH,
            bottleneckBranch: BranchCode.FH,
            recentlyPassedBranch: BranchCode.FH,
            transferBranch: BranchCode.FH));
    }
}
