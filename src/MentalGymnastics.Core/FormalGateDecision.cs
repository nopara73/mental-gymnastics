namespace MentalGymnastics.Core;

public sealed record FormalGateDecision(
    GateOutcome Outcome,
    IReadOnlyList<StandardEvaluationFailure> BlockingFailures)
{
    public bool OpensStabilization => Outcome == GateOutcome.PassOnce;
}

public static class FormalGateDecisionEngine
{
    public static FormalGateDecision Decide(
        FormalTestAttempt attempt,
        StandardEvaluationResult standardEvidence)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(standardEvidence);

        if (!standardEvidence.Passed || attempt.PassState == FormalTestPassState.Fail)
        {
            return new FormalGateDecision(GateOutcome.Fail, standardEvidence.Failures);
        }

        return new FormalGateDecision(ToGateOutcome(attempt.PassState), []);
    }

    private static GateOutcome ToGateOutcome(FormalTestPassState passState)
    {
        return passState switch
        {
            FormalTestPassState.PassOnce => GateOutcome.PassOnce,
            FormalTestPassState.StabilizationPass => GateOutcome.Stabilize,
            FormalTestPassState.MaintenancePass => GateOutcome.Maintain,
            FormalTestPassState.Owned => GateOutcome.Stabilize,
            _ => GateOutcome.Fail,
        };
    }
}
