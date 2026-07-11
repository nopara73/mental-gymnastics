using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class FocusHoldStandardsTests
{
    [Fact]
    public void LevelOneExecutableStandardMatchesProgramThresholds()
    {
        var standard = FocusHoldLevelOneStandard.Create();

        Assert.Contains("No more than 5 marked drifts", standard.Name, StringComparison.Ordinal);
        Assert.Contains(
            standard.NumericThresholds,
            threshold => threshold == NumericThreshold.AtLeast(
                FocusHoldStandardMeasurements.ActiveDurationSeconds,
                180));
        Assert.Contains(
            standard.NumericThresholds,
            threshold => threshold == NumericThreshold.AtMost(
                FocusHoldStandardMeasurements.MarkedDriftCount,
                5));
        Assert.Contains(
            standard.NumericThresholds,
            threshold => threshold == NumericThreshold.AtMost(
                FocusHoldStandardMeasurements.UnreturnedDriftCount,
                0));
        Assert.Contains(
            standard.NumericThresholds,
            threshold => threshold == NumericThreshold.AtMost(
                FocusHoldStandardMeasurements.LateReturnCount,
                0));
        Assert.Contains(
            standard.NumericThresholds,
            threshold => threshold == NumericThreshold.AtMost(
                FocusHoldStandardMeasurements.TargetSubstitutionCount,
                0));
        Assert.Contains(
            standard.CriticalConstraints,
            constraint => constraint.Id == FocusHoldStandardMeasurements.TargetStatedBeforeSet);
        Assert.True(standard.RequiresCompleteOutput);
        Assert.Null(standard.RequiredRubric);
        Assert.Equal(10, FocusHoldLevelOneStandard.ReturnWindowSeconds);
    }

    [Fact]
    public void LevelOneExecutableStandardPassesOnlyCompleteCleanEvidence()
    {
        var standard = FocusHoldLevelOneStandard.Create();

        var passing = StandardEvaluator.Evaluate(
            standard,
            Attempt(
                durationSeconds: 180,
                markedDrifts: 5,
                unreturnedDrifts: 0,
                lateReturns: 0,
                targetSubstitutions: 0,
                targetStated: true,
                outputComplete: true));
        var lateReturn = StandardEvaluator.Evaluate(
            standard,
            Attempt(
                durationSeconds: 180,
                markedDrifts: 1,
                unreturnedDrifts: 0,
                lateReturns: 1,
                targetSubstitutions: 0,
                targetStated: true,
                outputComplete: true));
        var unreturnedDrift = StandardEvaluator.Evaluate(
            standard,
            Attempt(
                durationSeconds: 180,
                markedDrifts: 1,
                unreturnedDrifts: 1,
                lateReturns: 0,
                targetSubstitutions: 0,
                targetStated: true,
                outputComplete: true));
        var changedTarget = StandardEvaluator.Evaluate(
            standard,
            Attempt(
                durationSeconds: 180,
                markedDrifts: 0,
                unreturnedDrifts: 0,
                lateReturns: 0,
                targetSubstitutions: 1,
                targetStated: true,
                outputComplete: true));

        Assert.True(passing.Passed);
        Assert.False(lateReturn.Passed);
        Assert.False(unreturnedDrift.Passed);
        Assert.False(changedTarget.Passed);
    }

    private static StandardEvaluationAttempt Attempt(
        decimal durationSeconds,
        decimal markedDrifts,
        decimal unreturnedDrifts,
        decimal lateReturns,
        decimal targetSubstitutions,
        bool targetStated,
        bool outputComplete)
    {
        return new StandardEvaluationAttempt(
            [
                new NumericMeasurement(FocusHoldStandardMeasurements.ActiveDurationSeconds, durationSeconds),
                new NumericMeasurement(FocusHoldStandardMeasurements.MarkedDriftCount, markedDrifts),
                new NumericMeasurement(FocusHoldStandardMeasurements.UnreturnedDriftCount, unreturnedDrifts),
                new NumericMeasurement(FocusHoldStandardMeasurements.LateReturnCount, lateReturns),
                new NumericMeasurement(FocusHoldStandardMeasurements.TargetSubstitutionCount, targetSubstitutions),
            ],
            [
                new CriticalConstraintCheck(
                    FocusHoldStandardMeasurements.TargetStatedBeforeSet,
                    targetStated),
            ],
            outputComplete,
            rubricOutcome: null);
    }
}
