using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class StandardEvaluationTests
{
    [Fact]
    public void PassingEvaluationCanCombineNumericalThresholdsCriticalConstraintsAndRubric()
    {
        var standard = Standard();
        var attempt = Attempt(
            score: 92,
            errors: 2,
            outputComplete: true,
            criticalConstraintsSatisfied: [("target-stable", true)],
            rubricOutcome: RubricOutcome.Pass);

        var result = StandardEvaluator.Evaluate(standard, attempt);

        Assert.True(result.Passed);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void BrokenCriticalConstraintFailsRegardlessOfStrongScore()
    {
        var standard = Standard();
        var attempt = Attempt(
            score: 100,
            errors: 0,
            outputComplete: true,
            criticalConstraintsSatisfied: [("target-stable", false)],
            rubricOutcome: RubricOutcome.Excellent);

        var result = StandardEvaluator.Evaluate(standard, attempt);

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, failure => failure.Kind == StandardFailureKind.CriticalConstraintBroken);
        Assert.DoesNotContain(result.Failures, failure => failure.Kind == StandardFailureKind.NumericalThresholdMissed);
    }

    [Fact]
    public void IncompleteOutputFailsIndependentlyOfScoreAndRubric()
    {
        var standard = Standard();
        var attempt = Attempt(
            score: 95,
            errors: 1,
            outputComplete: false,
            criticalConstraintsSatisfied: [("target-stable", true)],
            rubricOutcome: RubricOutcome.Excellent);

        var result = StandardEvaluator.Evaluate(standard, attempt);

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, failure => failure.Kind == StandardFailureKind.OutputIncomplete);
        Assert.DoesNotContain(result.Failures, failure => failure.Kind == StandardFailureKind.CriticalConstraintBroken);
    }

    [Fact]
    public void ErrorThresholdFailsIndependentlyOfCriticalConstraintsAndRubric()
    {
        var standard = Standard();
        var attempt = Attempt(
            score: 90,
            errors: 6,
            outputComplete: true,
            criticalConstraintsSatisfied: [("target-stable", true)],
            rubricOutcome: RubricOutcome.Pass);

        var result = StandardEvaluator.Evaluate(standard, attempt);

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, failure => failure.Kind == StandardFailureKind.NumericalThresholdMissed);
        Assert.DoesNotContain(result.Failures, failure => failure.Kind == StandardFailureKind.CriticalConstraintBroken);
    }

    [Theory]
    [InlineData(RubricOutcome.Fail, false)]
    [InlineData(RubricOutcome.Pass, true)]
    [InlineData(RubricOutcome.Excellent, true)]
    public void RubricOutcomeCanDeterminePassFailWhenRubricIsRequired(
        RubricOutcome rubricOutcome,
        bool expectedPass)
    {
        var standard = Standard();
        var attempt = Attempt(
            score: 90,
            errors: 1,
            outputComplete: true,
            criticalConstraintsSatisfied: [("target-stable", true)],
            rubricOutcome: rubricOutcome);

        var result = StandardEvaluator.Evaluate(standard, attempt);

        Assert.Equal(expectedPass, result.Passed);
        Assert.Equal(
            expectedPass,
            !result.Failures.Any(failure => failure.Kind == StandardFailureKind.RubricDidNotPass));
    }

    private static EvaluatedStandard Standard()
    {
        return new EvaluatedStandard(
            "FH L1 protected hold standard",
            [
                NumericThreshold.AtLeast("accuracy", 85),
                NumericThreshold.AtMost("errors", 5),
            ],
            [new CriticalConstraintRequirement("target-stable", "No target change.")],
            requiresCompleteOutput: true,
            requiredRubric: RubricOutcome.Pass);
    }

    private static StandardEvaluationAttempt Attempt(
        decimal score,
        decimal errors,
        bool outputComplete,
        IEnumerable<(string Id, bool Satisfied)> criticalConstraintsSatisfied,
        RubricOutcome rubricOutcome)
    {
        return new StandardEvaluationAttempt(
            [
                new NumericMeasurement("accuracy", score),
                new NumericMeasurement("errors", errors),
            ],
            criticalConstraintsSatisfied.Select(item => new CriticalConstraintCheck(item.Id, item.Satisfied)),
            outputComplete,
            rubricOutcome);
    }
}
