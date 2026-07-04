using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class FormalGateDecisionTests
{
    [Fact]
    public void PassingFormalAttemptProducesPassOnceGateOutcome()
    {
        var attempt = Attempt(FormalTestPassState.PassOnce);
        var standardEvidence = EvaluateStandard(
            score: 91,
            criticalConstraintSatisfied: true,
            outputComplete: true);

        var decision = FormalGateDecisionEngine.Decide(attempt, standardEvidence);

        Assert.Equal(GateOutcome.PassOnce, decision.Outcome);
        Assert.True(decision.OpensStabilization);
        Assert.Empty(decision.BlockingFailures);
    }

    [Fact]
    public void FailedStandardProducesFailGateOutcome()
    {
        var attempt = Attempt(
            FormalTestPassState.Fail,
            failureType: FailureType.EffortFailure,
            resultEvidence: "score below threshold");
        var standardEvidence = EvaluateStandard(
            score: 70,
            criticalConstraintSatisfied: true,
            outputComplete: true);

        var decision = FormalGateDecisionEngine.Decide(attempt, standardEvidence);

        Assert.Equal(GateOutcome.Fail, decision.Outcome);
        Assert.False(decision.OpensStabilization);
        Assert.Contains(
            decision.BlockingFailures,
            failure => failure.Kind == StandardFailureKind.NumericalThresholdMissed);
    }

    [Fact]
    public void CriticalConstraintFailurePreventsPassingEvenWithStrongScore()
    {
        var attempt = Attempt(FormalTestPassState.PassOnce, resultEvidence: "score: 100");
        var standardEvidence = EvaluateStandard(
            score: 100,
            criticalConstraintSatisfied: false,
            outputComplete: true);

        var decision = FormalGateDecisionEngine.Decide(attempt, standardEvidence);

        Assert.Equal(GateOutcome.Fail, decision.Outcome);
        Assert.False(decision.OpensStabilization);
        Assert.Contains(
            decision.BlockingFailures,
            failure => failure.Kind == StandardFailureKind.CriticalConstraintBroken);
    }

    [Fact]
    public void ParticipationEffortAndInsightAloneDoNotProduceAdvancement()
    {
        var attempt = Attempt(
            FormalTestPassState.PassOnce,
            resultEvidence: "participated fully, tried hard, and had an insight");
        var standardEvidence = EvaluateStandard(
            score: 0,
            criticalConstraintSatisfied: true,
            outputComplete: false);

        var decision = FormalGateDecisionEngine.Decide(attempt, standardEvidence);

        Assert.Equal(GateOutcome.Fail, decision.Outcome);
        Assert.False(decision.OpensStabilization);
        Assert.Contains(
            decision.BlockingFailures,
            failure => failure.Kind == StandardFailureKind.OutputIncomplete);
    }

    [Fact]
    public void StabilizationPassDoesNotConvertToOwnership()
    {
        var attempt = Attempt(
            FormalTestPassState.StabilizationPass,
            resultEvidence: "stabilization standard passed");
        var standardEvidence = EvaluateStandard(
            score: 91,
            criticalConstraintSatisfied: true,
            outputComplete: true);

        var decision = FormalGateDecisionEngine.Decide(attempt, standardEvidence);

        Assert.Equal(GateOutcome.Stabilize, decision.Outcome);
        Assert.False(decision.OpensStabilization);
        Assert.Empty(decision.BlockingFailures);
    }

    [Fact]
    public void SingleFormalAttemptClaimingOwnedDoesNotConvertToOwnership()
    {
        var attempt = Attempt(
            FormalTestPassState.Owned,
            resultEvidence: "claimed ownership");
        var standardEvidence = EvaluateStandard(
            score: 91,
            criticalConstraintSatisfied: true,
            outputComplete: true);

        var decision = FormalGateDecisionEngine.Decide(attempt, standardEvidence);

        Assert.Equal(GateOutcome.Stabilize, decision.Outcome);
        Assert.False(decision.OpensStabilization);
    }

    private static StandardEvaluationResult EvaluateStandard(
        decimal score,
        bool criticalConstraintSatisfied,
        bool outputComplete)
    {
        var standard = new EvaluatedStandard(
            "FH L1 formal gate standard",
            [NumericThreshold.AtLeast("score", 85)],
            [new CriticalConstraintRequirement("target-stable", "No target change.")],
            requiresCompleteOutput: true,
            requiredRubric: null);

        var evidence = new StandardEvaluationAttempt(
            [new NumericMeasurement("score", score)],
            [new CriticalConstraintCheck("target-stable", criticalConstraintSatisfied)],
            outputComplete,
            rubricOutcome: null);

        return StandardEvaluator.Evaluate(standard, evidence);
    }

    private static FormalTestAttempt Attempt(
        FormalTestPassState passState,
        FailureType? failureType = null,
        string resultEvidence = "score: 91")
    {
        return new FormalTestAttempt(
            BranchCode.FH,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 4),
            TestTask.ForDrill(DrillId.FH1TargetHold),
            [new LoadVariable("duration", "3 minutes")],
            "No more than 5 marked drifts; each return within 10 seconds; no target change.",
            [new CriticalConstraint("No target change.")],
            new TestResultEvidence(TestResultEvidenceKind.PassFail, resultEvidence),
            failureType,
            passState,
            new EvidenceArtifact(
                EvidenceArtifactCategory.Test,
                TrainingDate.From(2026, 7, 4),
                [new ObservableEvidence(ObservableEvidenceKind.Score, resultEvidence)],
                "FH L1 formal gate attempt"));
    }
}
