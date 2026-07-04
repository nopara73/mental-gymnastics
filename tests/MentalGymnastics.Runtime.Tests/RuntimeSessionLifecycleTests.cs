using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeSessionLifecycleTests
{
    [Fact]
    public void LegalPauseResumeCompletionPathAdvancesRuntimeLifecycle()
    {
        var notStarted = RuntimeSessionLifecycleState.NotStarted(pauseAllowed: true);

        var started = RuntimeSessionLifecycleStateMachine.TryApply(
            notStarted,
            RuntimeSessionLifecycleTransition.Start);
        var paused = RuntimeSessionLifecycleStateMachine.TryApply(
            started.State,
            RuntimeSessionLifecycleTransition.Pause);
        var resumed = RuntimeSessionLifecycleStateMachine.TryApply(
            paused.State,
            RuntimeSessionLifecycleTransition.Resume);
        var completed = RuntimeSessionLifecycleStateMachine.TryApply(
            resumed.State,
            RuntimeSessionLifecycleTransition.Complete);

        Assert.True(started.IsValid);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, started.State.Status);
        Assert.True(paused.IsValid);
        Assert.Equal(RuntimeSessionLifecycleStatus.Paused, paused.State.Status);
        Assert.True(resumed.IsValid);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, resumed.State.Status);
        Assert.True(completed.IsValid);
        Assert.Equal(RuntimeSessionLifecycleStatus.Completed, completed.State.Status);
    }

    [Theory]
    [InlineData(RuntimeSessionLifecycleTransition.Fail, RuntimeSessionLifecycleStatus.Failed)]
    [InlineData(RuntimeSessionLifecycleTransition.Abandon, RuntimeSessionLifecycleStatus.Abandoned)]
    public void RunningSessionMayReachTerminalNonCompletionOutcomes(
        RuntimeSessionLifecycleTransition transition,
        RuntimeSessionLifecycleStatus expectedStatus)
    {
        var running = RuntimeSessionLifecycleStateMachine.TryApply(
            RuntimeSessionLifecycleState.NotStarted(pauseAllowed: false),
            RuntimeSessionLifecycleTransition.Start).State;

        var result = RuntimeSessionLifecycleStateMachine.TryApply(running, transition);

        Assert.True(result.IsValid);
        Assert.Equal(expectedStatus, result.State.Status);
    }

    [Fact]
    public void PauseIsRejectedWhenSessionDoesNotAllowPause()
    {
        var running = RuntimeSessionLifecycleStateMachine.TryApply(
            RuntimeSessionLifecycleState.NotStarted(pauseAllowed: false),
            RuntimeSessionLifecycleTransition.Start).State;

        var result = RuntimeSessionLifecycleStateMachine.TryApply(
            running,
            RuntimeSessionLifecycleTransition.Pause);

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeSessionLifecycleInvalidTransitionReason.PauseNotAllowed, result.InvalidReason);
        Assert.Same(running, result.State);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, result.State.Status);
    }

    [Theory]
    [InlineData(RuntimeSessionLifecycleStatus.NotStarted, RuntimeSessionLifecycleTransition.Pause)]
    [InlineData(RuntimeSessionLifecycleStatus.NotStarted, RuntimeSessionLifecycleTransition.Complete)]
    [InlineData(RuntimeSessionLifecycleStatus.Paused, RuntimeSessionLifecycleTransition.Start)]
    [InlineData(RuntimeSessionLifecycleStatus.Completed, RuntimeSessionLifecycleTransition.Start)]
    [InlineData(RuntimeSessionLifecycleStatus.Failed, RuntimeSessionLifecycleTransition.Resume)]
    [InlineData(RuntimeSessionLifecycleStatus.Abandoned, RuntimeSessionLifecycleTransition.Start)]
    public void IllegalTransitionsAreInvalidAndPreserveRuntimeState(
        RuntimeSessionLifecycleStatus status,
        RuntimeSessionLifecycleTransition transition)
    {
        var current = new RuntimeSessionLifecycleState(status, PauseAllowed: true);

        var result = RuntimeSessionLifecycleStateMachine.TryApply(current, transition);

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeSessionLifecycleInvalidTransitionReason.IllegalTransition, result.InvalidReason);
        Assert.Same(current, result.State);
        Assert.Equal(status, result.State.Status);
    }
}
