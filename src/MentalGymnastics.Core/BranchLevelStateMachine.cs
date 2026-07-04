namespace MentalGymnastics.Core;

public readonly record struct BranchLevelStateTransitionResult(
    BranchLevelState CurrentState,
    BranchLevelTransition Transition,
    bool IsValid,
    BranchLevelState NextState);

public readonly record struct BranchLevelStatusTransitionResult(
    BranchLevelStatus CurrentStatus,
    BranchLevelTransition Transition,
    bool IsValid,
    BranchLevelStatus NextStatus);

public static class BranchLevelStateMachine
{
    public static BranchLevelStateTransitionResult TryApply(
        BranchLevelState currentState,
        BranchLevelTransition transition)
    {
        var nextState = GetNextState(currentState, transition);

        return new BranchLevelStateTransitionResult(
            currentState,
            transition,
            nextState is not null,
            nextState ?? currentState);
    }

    public static BranchLevelStatusTransitionResult TryApply(
        BranchLevelStatus currentStatus,
        BranchLevelTransition transition)
    {
        var stateResult = TryApply(currentStatus.State, transition);
        var nextStatus = currentStatus with { State = stateResult.NextState };

        return new BranchLevelStatusTransitionResult(
            currentStatus,
            transition,
            stateResult.IsValid,
            nextStatus);
    }

    private static BranchLevelState? GetNextState(
        BranchLevelState currentState,
        BranchLevelTransition transition)
    {
        return (currentState, transition) switch
        {
            (BranchLevelState.Unopened, BranchLevelTransition.OpenForTraining) => BranchLevelState.Training,

            (BranchLevelState.Training, BranchLevelTransition.MarkTestReady) => BranchLevelState.TestReady,
            (BranchLevelState.Training, BranchLevelTransition.ContinueTrainingWork) => BranchLevelState.Training,

            (BranchLevelState.TestReady, BranchLevelTransition.ExpireTestReadiness) => BranchLevelState.Training,
            (BranchLevelState.TestReady, BranchLevelTransition.FailFormalTest) => BranchLevelState.Training,
            (BranchLevelState.TestReady, BranchLevelTransition.PassFormalTestOnce) => BranchLevelState.PassedOnce,

            (BranchLevelState.PassedOnce, BranchLevelTransition.EnterStabilization) => BranchLevelState.Stabilizing,

            (BranchLevelState.Stabilizing, BranchLevelTransition.CompleteStabilization) => BranchLevelState.Owned,
            (BranchLevelState.Stabilizing, BranchLevelTransition.FailStabilization) => BranchLevelState.Training,

            (BranchLevelState.Owned, BranchLevelTransition.AssignMaintenance) => BranchLevelState.Maintenance,
            (BranchLevelState.Owned, BranchLevelTransition.OpenNextLevelTraining) => BranchLevelState.Training,
            (BranchLevelState.Owned, BranchLevelTransition.ConfirmGlobalReview) => BranchLevelState.Owned,

            (BranchLevelState.Maintenance, BranchLevelTransition.PassMaintenance) => BranchLevelState.Owned,
            (BranchLevelState.Maintenance, BranchLevelTransition.MarkDecayed) => BranchLevelState.Decayed,
            (BranchLevelState.Maintenance, BranchLevelTransition.OpenNextDemandFromReview) => BranchLevelState.Training,
            (BranchLevelState.Maintenance, BranchLevelTransition.ContinueMaintenance) => BranchLevelState.Maintenance,

            (BranchLevelState.Decayed, BranchLevelTransition.BeginRestorationTraining) => BranchLevelState.Training,
            (BranchLevelState.Decayed, BranchLevelTransition.RestoreToMaintenance) => BranchLevelState.Maintenance,

            _ => null,
        };
    }
}
