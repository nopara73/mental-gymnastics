using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class BranchLevelStateMachineTests
{
    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public void AllowsDocumentedLifecycleTransitions(
        BranchLevelState currentState,
        BranchLevelTransition transition,
        BranchLevelState expectedNextState)
    {
        var result = BranchLevelStateMachine.TryApply(currentState, transition);

        Assert.True(result.IsValid);
        Assert.Equal(currentState, result.CurrentState);
        Assert.Equal(expectedNextState, result.NextState);
    }

    [Fact]
    public void PassingOnceDoesNotEqualOwnership()
    {
        var passedOnce = BranchLevelStateMachine.TryApply(
            BranchLevelState.TestReady,
            BranchLevelTransition.PassFormalTestOnce);

        Assert.True(passedOnce.IsValid);
        Assert.Equal(BranchLevelState.PassedOnce, passedOnce.NextState);
        Assert.NotEqual(BranchLevelState.Owned, passedOnce.NextState);

        var directOwnership = BranchLevelStateMachine.TryApply(
            BranchLevelState.PassedOnce,
            BranchLevelTransition.CompleteStabilization);

        Assert.False(directOwnership.IsValid);
        Assert.Equal(BranchLevelState.PassedOnce, directOwnership.NextState);
    }

    [Fact]
    public void RejectsRepresentativeIllegalTransitionsWithoutChangingState()
    {
        AssertInvalid(BranchLevelState.Unopened, BranchLevelTransition.PassFormalTestOnce);
        AssertInvalid(BranchLevelState.Training, BranchLevelTransition.PassFormalTestOnce);
        AssertInvalid(BranchLevelState.TestReady, BranchLevelTransition.CompleteStabilization);
        AssertInvalid(BranchLevelState.Decayed, BranchLevelTransition.PassMaintenance);
        AssertInvalid(BranchLevelState.Decayed, BranchLevelTransition.OpenNextLevelTraining);
    }

    [Fact]
    public void RejectsEveryUndocumentedTransitionWithoutChangingState()
    {
        var legalTransitions = LegalTransitionPairs();

        foreach (var currentState in Enum.GetValues<BranchLevelState>())
        {
            foreach (var transition in Enum.GetValues<BranchLevelTransition>())
            {
                if (legalTransitions.Contains((currentState, transition)))
                {
                    continue;
                }

                var result = BranchLevelStateMachine.TryApply(currentState, transition);

                Assert.False(result.IsValid);
                Assert.Equal(currentState, result.CurrentState);
                Assert.Equal(currentState, result.NextState);
            }
        }
    }

    [Fact]
    public void AppliesTransitionToBranchLevelStatusWithoutMutatingOriginalStatus()
    {
        var status = new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.TestReady);

        var result = BranchLevelStateMachine.TryApply(status, BranchLevelTransition.PassFormalTestOnce);

        Assert.True(result.IsValid);
        Assert.Equal(new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.TestReady), status);
        Assert.Equal(new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce), result.NextStatus);
    }

    [Fact]
    public void KeepsDecayAndRestorationSeparateFromOrdinaryAdvancement()
    {
        var decayed = BranchLevelStateMachine.TryApply(
            BranchLevelState.Maintenance,
            BranchLevelTransition.MarkDecayed);
        var restorationTraining = BranchLevelStateMachine.TryApply(
            BranchLevelState.Decayed,
            BranchLevelTransition.BeginRestorationTraining);
        var restoredMaintenance = BranchLevelStateMachine.TryApply(
            BranchLevelState.Decayed,
            BranchLevelTransition.RestoreToMaintenance);

        Assert.Equal(BranchLevelState.Decayed, decayed.NextState);
        Assert.Equal(BranchLevelState.Training, restorationTraining.NextState);
        Assert.Equal(BranchLevelState.Maintenance, restoredMaintenance.NextState);

        AssertInvalid(BranchLevelState.Decayed, BranchLevelTransition.MarkTestReady);
        AssertInvalid(BranchLevelState.Decayed, BranchLevelTransition.OpenNextLevelTraining);
    }

    public static TheoryData<BranchLevelState, BranchLevelTransition, BranchLevelState> LegalTransitions()
    {
        return new TheoryData<BranchLevelState, BranchLevelTransition, BranchLevelState>
        {
            { BranchLevelState.Unopened, BranchLevelTransition.OpenForTraining, BranchLevelState.Training },
            { BranchLevelState.Training, BranchLevelTransition.MarkTestReady, BranchLevelState.TestReady },
            { BranchLevelState.TestReady, BranchLevelTransition.ExpireTestReadiness, BranchLevelState.Training },
            { BranchLevelState.TestReady, BranchLevelTransition.FailFormalTest, BranchLevelState.Training },
            { BranchLevelState.TestReady, BranchLevelTransition.PassFormalTestOnce, BranchLevelState.PassedOnce },
            { BranchLevelState.PassedOnce, BranchLevelTransition.EnterStabilization, BranchLevelState.Stabilizing },
            { BranchLevelState.PassedOnce, BranchLevelTransition.FailStabilization, BranchLevelState.Training },
            { BranchLevelState.Stabilizing, BranchLevelTransition.CompleteStabilization, BranchLevelState.Owned },
            { BranchLevelState.Stabilizing, BranchLevelTransition.FailStabilization, BranchLevelState.Training },
            { BranchLevelState.Owned, BranchLevelTransition.AssignMaintenance, BranchLevelState.Maintenance },
            { BranchLevelState.Maintenance, BranchLevelTransition.PassMaintenance, BranchLevelState.Owned },
            { BranchLevelState.Maintenance, BranchLevelTransition.MarkDecayed, BranchLevelState.Decayed },
            { BranchLevelState.Decayed, BranchLevelTransition.BeginRestorationTraining, BranchLevelState.Training },
            { BranchLevelState.Decayed, BranchLevelTransition.RestoreToMaintenance, BranchLevelState.Maintenance },
            { BranchLevelState.Owned, BranchLevelTransition.OpenNextLevelTraining, BranchLevelState.Training },
            { BranchLevelState.Maintenance, BranchLevelTransition.OpenNextDemandFromReview, BranchLevelState.Training },
            { BranchLevelState.Training, BranchLevelTransition.ContinueTrainingWork, BranchLevelState.Training },
            { BranchLevelState.Owned, BranchLevelTransition.ConfirmGlobalReview, BranchLevelState.Owned },
            { BranchLevelState.Maintenance, BranchLevelTransition.ContinueMaintenance, BranchLevelState.Maintenance },
        };
    }

    private static void AssertInvalid(BranchLevelState currentState, BranchLevelTransition transition)
    {
        var result = BranchLevelStateMachine.TryApply(currentState, transition);

        Assert.False(result.IsValid);
        Assert.Equal(currentState, result.CurrentState);
        Assert.Equal(currentState, result.NextState);
    }

    private static IReadOnlySet<(BranchLevelState State, BranchLevelTransition Transition)> LegalTransitionPairs()
    {
        return new HashSet<(BranchLevelState State, BranchLevelTransition Transition)>
        {
            (BranchLevelState.Unopened, BranchLevelTransition.OpenForTraining),
            (BranchLevelState.Training, BranchLevelTransition.MarkTestReady),
            (BranchLevelState.TestReady, BranchLevelTransition.ExpireTestReadiness),
            (BranchLevelState.TestReady, BranchLevelTransition.FailFormalTest),
            (BranchLevelState.TestReady, BranchLevelTransition.PassFormalTestOnce),
            (BranchLevelState.PassedOnce, BranchLevelTransition.EnterStabilization),
            (BranchLevelState.PassedOnce, BranchLevelTransition.FailStabilization),
            (BranchLevelState.Stabilizing, BranchLevelTransition.CompleteStabilization),
            (BranchLevelState.Stabilizing, BranchLevelTransition.FailStabilization),
            (BranchLevelState.Owned, BranchLevelTransition.AssignMaintenance),
            (BranchLevelState.Maintenance, BranchLevelTransition.PassMaintenance),
            (BranchLevelState.Maintenance, BranchLevelTransition.MarkDecayed),
            (BranchLevelState.Decayed, BranchLevelTransition.BeginRestorationTraining),
            (BranchLevelState.Decayed, BranchLevelTransition.RestoreToMaintenance),
            (BranchLevelState.Owned, BranchLevelTransition.OpenNextLevelTraining),
            (BranchLevelState.Maintenance, BranchLevelTransition.OpenNextDemandFromReview),
            (BranchLevelState.Training, BranchLevelTransition.ContinueTrainingWork),
            (BranchLevelState.Owned, BranchLevelTransition.ConfirmGlobalReview),
            (BranchLevelState.Maintenance, BranchLevelTransition.ContinueMaintenance),
        };
    }
}
