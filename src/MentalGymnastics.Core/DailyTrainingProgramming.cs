namespace MentalGymnastics.Core;

public sealed record DailyTrainingPrescription(
    TrainingDate Date,
    TrainingDate CycleAnchor,
    int CycleDay,
    WeeklySessionKind Session,
    IReadOnlyList<BranchCode> BranchEmphasis,
    bool IsAdvancementWork)
{
    public bool IsOff => Session is WeeklySessionKind.Off or WeeklySessionKind.OffOrRecovery;
}

public static class DailyTrainingProgrammingPlanner
{
    public static DailyTrainingPrescription Prescribe(
        TrainingDate date,
        TrainingDate cycleAnchor,
        WeeklyPlan weeklyPlan)
    {
        ArgumentNullException.ThrowIfNull(weeklyPlan);

        var elapsedDays = cycleAnchor.DaysUntil(date);
        if (elapsedDays < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(date),
                date,
                "A daily prescription cannot precede its program-cycle anchor.");
        }

        var cycleDay = (elapsedDays % 7) + 1;
        var planDay = weeklyPlan.Days.Single(day => day.DayNumber == cycleDay);
        return new DailyTrainingPrescription(
            date,
            cycleAnchor,
            cycleDay,
            planDay.Session,
            planDay.BranchEmphasis.Distinct().ToArray(),
            planDay.IsAdvancementWork);
    }
}

public enum DailyTrainingDoseState
{
    Planned,
    Active,
    Completed,
    Stopped,
}

public enum DailyTrainingDoseTransition
{
    StartActiveWork,
    CancelSetup,
    RecordTerminalBlock,
    StopRemainingWork,
}

public readonly record struct DailyTrainingDoseProgress
{
    public DailyTrainingDoseProgress(
        int requiredBlockCount,
        int terminalBlockCount,
        DailyTrainingDoseState state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requiredBlockCount);
        ArgumentOutOfRangeException.ThrowIfNegative(terminalBlockCount);
        if (terminalBlockCount > requiredBlockCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(terminalBlockCount),
                terminalBlockCount,
                "Terminal daily blocks cannot exceed prescribed blocks.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown daily dose state.");
        }

        if (state == DailyTrainingDoseState.Planned && terminalBlockCount != 0)
        {
            throw new ArgumentException("A planned daily dose cannot contain terminal blocks.");
        }


        if (requiredBlockCount == 0 && state != DailyTrainingDoseState.Completed)
        {
            throw new ArgumentException("Only an off-day dose may have zero prescribed blocks.");
        }

        if (state == DailyTrainingDoseState.Completed && terminalBlockCount != requiredBlockCount)
        {
            throw new ArgumentException("A completed daily dose requires every prescribed block to be terminal.");
        }

        if (state == DailyTrainingDoseState.Active && terminalBlockCount == requiredBlockCount)
        {
            throw new ArgumentException("An active daily dose requires at least one remaining block.");
        }

        RequiredBlockCount = requiredBlockCount;
        TerminalBlockCount = terminalBlockCount;
        State = state;
    }

    public int RequiredBlockCount { get; }

    public int TerminalBlockCount { get; }

    public DailyTrainingDoseState State { get; }

    public bool IsTerminal => State is DailyTrainingDoseState.Completed or DailyTrainingDoseState.Stopped;

    public int RemainingBlockCount => RequiredBlockCount - TerminalBlockCount;

    public static DailyTrainingDoseProgress Planned(int requiredBlockCount) =>
        requiredBlockCount > 0
            ? new(requiredBlockCount, terminalBlockCount: 0, DailyTrainingDoseState.Planned)
            : throw new ArgumentOutOfRangeException(
                nameof(requiredBlockCount),
                requiredBlockCount,
                "A planned training dose requires at least one block.");

    public static DailyTrainingDoseProgress OffDay() =>
        new(requiredBlockCount: 0, terminalBlockCount: 0, DailyTrainingDoseState.Completed);
}

public readonly record struct DailyTrainingDoseTransitionResult(
    DailyTrainingDoseProgress Current,
    DailyTrainingDoseTransition Transition,
    bool IsValid,
    DailyTrainingDoseProgress Next);

public static class DailyTrainingDoseStateMachine
{
    public static DailyTrainingDoseTransitionResult TryApply(
        DailyTrainingDoseProgress current,
        DailyTrainingDoseTransition transition)
    {
        if (!Enum.IsDefined(transition))
        {
            throw new ArgumentOutOfRangeException(nameof(transition), transition, "Unknown daily dose transition.");
        }

        var next = Next(current, transition);
        return new DailyTrainingDoseTransitionResult(
            current,
            transition,
            next.HasValue,
            next ?? current);
    }

    private static DailyTrainingDoseProgress? Next(
        DailyTrainingDoseProgress current,
        DailyTrainingDoseTransition transition)
    {
        if (current.IsTerminal)
        {
            return null;
        }

        return (current.State, transition) switch
        {
            (DailyTrainingDoseState.Planned, DailyTrainingDoseTransition.CancelSetup) => current,
            (DailyTrainingDoseState.Planned, DailyTrainingDoseTransition.StopRemainingWork) =>
                new DailyTrainingDoseProgress(
                    current.RequiredBlockCount,
                    current.TerminalBlockCount,
                    DailyTrainingDoseState.Stopped),
            (DailyTrainingDoseState.Planned, DailyTrainingDoseTransition.StartActiveWork) =>
                new DailyTrainingDoseProgress(
                    current.RequiredBlockCount,
                    current.TerminalBlockCount,
                    DailyTrainingDoseState.Active),
            (DailyTrainingDoseState.Active, DailyTrainingDoseTransition.RecordTerminalBlock) =>
                RecordTerminalBlock(current),
            (DailyTrainingDoseState.Active, DailyTrainingDoseTransition.StopRemainingWork) =>
                new DailyTrainingDoseProgress(
                    current.RequiredBlockCount,
                    current.TerminalBlockCount,
                    DailyTrainingDoseState.Stopped),
            _ => null,
        };
    }

    private static DailyTrainingDoseProgress RecordTerminalBlock(DailyTrainingDoseProgress current)
    {
        var terminalCount = current.TerminalBlockCount + 1;
        return new DailyTrainingDoseProgress(
            current.RequiredBlockCount,
            terminalCount,
            terminalCount == current.RequiredBlockCount
                ? DailyTrainingDoseState.Completed
                : DailyTrainingDoseState.Active);
    }
}
