namespace MentalGymnastics.Runtime;

public enum RuntimeSessionLifecycleStatus
{
    NotStarted,
    Running,
    Paused,
    Completed,
    Failed,
    Abandoned,
}

public enum RuntimeSessionLifecycleTransition
{
    Start,
    Pause,
    Resume,
    Complete,
    Fail,
    Abandon,
}

public enum RuntimeSessionLifecycleInvalidTransitionReason
{
    IllegalTransition,
    PauseNotAllowed,
}

public sealed record RuntimeSessionLifecycleState(
    RuntimeSessionLifecycleStatus Status,
    bool PauseAllowed)
{
    public static RuntimeSessionLifecycleState NotStarted(bool pauseAllowed)
    {
        return new RuntimeSessionLifecycleState(RuntimeSessionLifecycleStatus.NotStarted, pauseAllowed);
    }
}

public sealed record RuntimeSessionLifecycleTransitionResult(
    bool IsValid,
    RuntimeSessionLifecycleState State,
    RuntimeSessionLifecycleInvalidTransitionReason? InvalidReason);

public static class RuntimeSessionLifecycleStateMachine
{
    public static RuntimeSessionLifecycleTransitionResult TryApply(
        RuntimeSessionLifecycleState current,
        RuntimeSessionLifecycleTransition transition)
    {
        ArgumentNullException.ThrowIfNull(current);

        return transition switch
        {
            RuntimeSessionLifecycleTransition.Start => Start(current),
            RuntimeSessionLifecycleTransition.Pause => Pause(current),
            RuntimeSessionLifecycleTransition.Resume => Resume(current),
            RuntimeSessionLifecycleTransition.Complete => Complete(current),
            RuntimeSessionLifecycleTransition.Fail => Fail(current),
            RuntimeSessionLifecycleTransition.Abandon => Abandon(current),
            _ => Invalid(current, RuntimeSessionLifecycleInvalidTransitionReason.IllegalTransition),
        };
    }

    private static RuntimeSessionLifecycleTransitionResult Start(RuntimeSessionLifecycleState current)
    {
        return current.Status == RuntimeSessionLifecycleStatus.NotStarted
            ? Valid(current with { Status = RuntimeSessionLifecycleStatus.Running })
            : Invalid(current, RuntimeSessionLifecycleInvalidTransitionReason.IllegalTransition);
    }

    private static RuntimeSessionLifecycleTransitionResult Pause(RuntimeSessionLifecycleState current)
    {
        if (current.Status != RuntimeSessionLifecycleStatus.Running)
        {
            return Invalid(current, RuntimeSessionLifecycleInvalidTransitionReason.IllegalTransition);
        }

        return current.PauseAllowed
            ? Valid(current with { Status = RuntimeSessionLifecycleStatus.Paused })
            : Invalid(current, RuntimeSessionLifecycleInvalidTransitionReason.PauseNotAllowed);
    }

    private static RuntimeSessionLifecycleTransitionResult Resume(RuntimeSessionLifecycleState current)
    {
        return current.Status == RuntimeSessionLifecycleStatus.Paused
            ? Valid(current with { Status = RuntimeSessionLifecycleStatus.Running })
            : Invalid(current, RuntimeSessionLifecycleInvalidTransitionReason.IllegalTransition);
    }

    private static RuntimeSessionLifecycleTransitionResult Complete(RuntimeSessionLifecycleState current)
    {
        return current.Status == RuntimeSessionLifecycleStatus.Running
            ? Valid(current with { Status = RuntimeSessionLifecycleStatus.Completed })
            : Invalid(current, RuntimeSessionLifecycleInvalidTransitionReason.IllegalTransition);
    }

    private static RuntimeSessionLifecycleTransitionResult Fail(RuntimeSessionLifecycleState current)
    {
        return current.Status is RuntimeSessionLifecycleStatus.Running or RuntimeSessionLifecycleStatus.Paused
            ? Valid(current with { Status = RuntimeSessionLifecycleStatus.Failed })
            : Invalid(current, RuntimeSessionLifecycleInvalidTransitionReason.IllegalTransition);
    }

    private static RuntimeSessionLifecycleTransitionResult Abandon(RuntimeSessionLifecycleState current)
    {
        return current.Status is
            RuntimeSessionLifecycleStatus.NotStarted or
            RuntimeSessionLifecycleStatus.Running or
            RuntimeSessionLifecycleStatus.Paused
            ? Valid(current with { Status = RuntimeSessionLifecycleStatus.Abandoned })
            : Invalid(current, RuntimeSessionLifecycleInvalidTransitionReason.IllegalTransition);
    }

    private static RuntimeSessionLifecycleTransitionResult Valid(RuntimeSessionLifecycleState state)
    {
        return new RuntimeSessionLifecycleTransitionResult(true, state, null);
    }

    private static RuntimeSessionLifecycleTransitionResult Invalid(
        RuntimeSessionLifecycleState state,
        RuntimeSessionLifecycleInvalidTransitionReason reason)
    {
        return new RuntimeSessionLifecycleTransitionResult(false, state, reason);
    }
}
