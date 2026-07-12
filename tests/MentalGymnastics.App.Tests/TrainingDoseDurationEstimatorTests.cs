using MentalGymnastics.App;
using MentalGymnastics.Core;

namespace MentalGymnastics.App.Tests;

public sealed class TrainingDoseDurationEstimatorTests
{
    [Fact]
    public void TimedHoldIncludesSetupAndReviewOverhead()
    {
        var minutes = TrainingDoseDurationEstimator.RoundedMinutes(
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")]);

        Assert.Equal(4, minutes);
    }

    [Fact]
    public void CompositeEstimateIncludesTaskDelayAndEvidencePhases()
    {
        var minutes = TrainingDoseDurationEstimator.RoundedMinutes(
            DrillId.TI2GlobalReviewTask,
            [
                new LoadVariable("task length", "20 minutes"),
                new LoadVariable("delay", "5 minutes"),
            ]);

        Assert.Equal(32, minutes);
    }
}
