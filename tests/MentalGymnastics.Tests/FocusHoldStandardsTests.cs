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
                FocusHoldStandardMeasurements.TargetSubstitutionCount,
                0));
        Assert.Contains(
            standard.CriticalConstraints,
            constraint => constraint.Id == FocusHoldStandardMeasurements.TargetStatedBeforeSet);
        Assert.Contains(
            standard.CriticalConstraints,
            constraint => constraint.Id == FocusHoldStandardMeasurements.DriftMarked);
        Assert.True(standard.RequiresCompleteOutput);
        Assert.Null(standard.RequiredRubric);
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
                targetSubstitutions: 0,
                targetStated: true,
                everyNoticedDriftMarked: true,
                outputComplete: true));
        var tooManyDrifts = StandardEvaluator.Evaluate(
            standard,
            Attempt(
                durationSeconds: 180,
                markedDrifts: 6,
                targetSubstitutions: 0,
                targetStated: true,
                everyNoticedDriftMarked: true,
                outputComplete: true));
        var unmarkedNoticedDrift = StandardEvaluator.Evaluate(
            standard,
            Attempt(
                durationSeconds: 180,
                markedDrifts: 1,
                targetSubstitutions: 0,
                targetStated: true,
                everyNoticedDriftMarked: false,
                outputComplete: true));
        var changedTarget = StandardEvaluator.Evaluate(
            standard,
            Attempt(
                durationSeconds: 180,
                markedDrifts: 0,
                targetSubstitutions: 1,
                targetStated: true,
                everyNoticedDriftMarked: true,
                outputComplete: true));

        Assert.True(passing.Passed);
        Assert.False(tooManyDrifts.Passed);
        Assert.False(unmarkedNoticedDrift.Passed);
        Assert.False(changedTarget.Passed);
    }

    private static StandardEvaluationAttempt Attempt(
        decimal durationSeconds,
        decimal markedDrifts,
        decimal targetSubstitutions,
        bool targetStated,
        bool everyNoticedDriftMarked,
        bool outputComplete)
    {
        return new StandardEvaluationAttempt(
            [
                new NumericMeasurement(FocusHoldStandardMeasurements.ActiveDurationSeconds, durationSeconds),
                new NumericMeasurement(FocusHoldStandardMeasurements.MarkedDriftCount, markedDrifts),
                new NumericMeasurement(FocusHoldStandardMeasurements.TargetSubstitutionCount, targetSubstitutions),
            ],
            [
                new CriticalConstraintCheck(
                    FocusHoldStandardMeasurements.TargetStatedBeforeSet,
                    targetStated),
                new CriticalConstraintCheck(
                    FocusHoldStandardMeasurements.DriftMarked,
                    everyNoticedDriftMarked),
            ],
            outputComplete,
            rubricOutcome: null);
    }
}
