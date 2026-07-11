namespace MentalGymnastics.Core;

public sealed record GlobalReviewCadenceRequest(
    TrainingDate AsOf,
    PractitionerCategory PractitionerCategory,
    TrainingDate ProgramStartedOn,
    TrainingDate? LastCompletedReviewOn = null);

public sealed record GlobalReviewCadenceResult(
    int CadenceDays,
    TrainingDate AnchorDate,
    TrainingDate NextReviewOn,
    int DaysSinceAnchor,
    bool IsDue);

public static class GlobalReviewCadenceEvaluator
{
    public static GlobalReviewCadenceResult Evaluate(GlobalReviewCadenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cadenceDays = request.PractitionerCategory == PractitionerCategory.Beginner ? 42 : 28;
        var anchor = request.LastCompletedReviewOn is { } completed &&
            completed.DaysUntil(request.AsOf) >= 0
                ? completed
                : request.ProgramStartedOn;
        var daysSinceAnchor = Math.Max(0, anchor.DaysUntil(request.AsOf));

        return new GlobalReviewCadenceResult(
            cadenceDays,
            anchor,
            AddDays(anchor, cadenceDays),
            daysSinceAnchor,
            daysSinceAnchor >= cadenceDays);
    }

    private static TrainingDate AddDays(TrainingDate date, int days)
    {
        var value = new DateOnly(date.Year, date.Month, date.Day).AddDays(days);
        return TrainingDate.From(value.Year, value.Month, value.Day);
    }
}
