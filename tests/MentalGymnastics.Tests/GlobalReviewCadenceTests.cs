using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class GlobalReviewCadenceTests
{
    [Theory]
    [InlineData(PractitionerCategory.Beginner, 42)]
    [InlineData(PractitionerCategory.Intermediate, 28)]
    [InlineData(PractitionerCategory.Advanced, 28)]
    public void ReviewBecomesDueAtCategoryCadence(
        PractitionerCategory category,
        int cadenceDays)
    {
        var start = TrainingDate.From(2026, 1, 1);

        var before = GlobalReviewCadenceEvaluator.Evaluate(new GlobalReviewCadenceRequest(
            AddDays(start, cadenceDays - 1),
            category,
            start));
        var due = GlobalReviewCadenceEvaluator.Evaluate(new GlobalReviewCadenceRequest(
            AddDays(start, cadenceDays),
            category,
            start));

        Assert.False(before.IsDue);
        Assert.True(due.IsDue);
        Assert.Equal(cadenceDays, due.CadenceDays);
        Assert.Equal(AddDays(start, cadenceDays), due.NextReviewOn);
    }

    [Fact]
    public void CompletedReviewRestartsCadenceFromReviewDate()
    {
        var start = TrainingDate.From(2026, 1, 1);
        var completed = TrainingDate.From(2026, 2, 12);
        var result = GlobalReviewCadenceEvaluator.Evaluate(new GlobalReviewCadenceRequest(
            TrainingDate.From(2026, 3, 11),
            PractitionerCategory.Beginner,
            start,
            completed));

        Assert.False(result.IsDue);
        Assert.Equal(completed, result.AnchorDate);
        Assert.Equal(27, result.DaysSinceAnchor);
        Assert.Equal(TrainingDate.From(2026, 3, 26), result.NextReviewOn);
    }

    private static TrainingDate AddDays(TrainingDate date, int days)
    {
        var value = new DateOnly(date.Year, date.Month, date.Day).AddDays(days);
        return TrainingDate.From(value.Year, value.Month, value.Day);
    }
}
