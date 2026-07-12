using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public sealed record FocusHoldRuntimeEvidenceSummary(
    decimal ActiveDurationSeconds,
    int MarkedDriftCount,
    int TargetSubstitutionCount);

public static class FocusHoldStandardEvaluationHandoffMapper
{
    public static FocusHoldRuntimeEvidenceSummary Summarize(
        RuntimeSessionCompletionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateSupportedSession(result);

        var activeDuration = result.PhaseHistory
            .Where(phase => phase.Definition.Kind == RuntimeSessionPhaseKind.ActiveWork)
            .Aggregate(TimeSpan.Zero, (total, phase) => total + phase.ActualDuration.Value);
        var driftEvents = result.RuntimeEvents
            .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.DriftMarked)
            .ToArray();
        var targetSubstitutions = result.RuntimeEvents.Count(runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.ErrorRecorded &&
            string.Equals(
                FactValue(runtimeEvent.Facts, "error_kind"),
                "target_substitution",
                StringComparison.Ordinal));
        return new FocusHoldRuntimeEvidenceSummary(
            Convert.ToDecimal(activeDuration.TotalSeconds, CultureInfo.InvariantCulture),
            driftEvents.Length,
            targetSubstitutions);
    }

    public static RuntimeStandardEvaluationHandoffInput ToStandardEvaluationInput(
        RuntimeSessionCompletionResult result,
        bool targetStatedBeforeSet,
        bool everyNoticedDriftMarked)
    {
        var evidence = Summarize(result);

        return new RuntimeStandardEvaluationHandoffInput(
            [
                new NumericMeasurement(
                    FocusHoldStandardMeasurements.ActiveDurationSeconds,
                    evidence.ActiveDurationSeconds),
                new NumericMeasurement(
                    FocusHoldStandardMeasurements.MarkedDriftCount,
                    evidence.MarkedDriftCount),
                new NumericMeasurement(
                    FocusHoldStandardMeasurements.TargetSubstitutionCount,
                    evidence.TargetSubstitutionCount),
            ],
            [
                new CriticalConstraintCheck(
                    FocusHoldStandardMeasurements.TargetStatedBeforeSet,
                    targetStatedBeforeSet),
                new CriticalConstraintCheck(
                    FocusHoldStandardMeasurements.DriftMarked,
                    everyNoticedDriftMarked),
            ],
            outputComplete: result.CompletionStatus == RuntimeSessionCompletionStatus.Completed,
            rubricOutcome: null);
    }

    private static void ValidateSupportedSession(RuntimeSessionCompletionResult result)
    {
        if (result.Branch != BranchCode.FH ||
            result.Level != GlobalLevelId.L1 ||
            result.Drill != DrillId.FH1TargetHold)
        {
            throw new ArgumentException(
                "FH L1 standard evaluation handoff requires an FH-1 Target Hold level-one result.",
                nameof(result));
        }
    }

    private static string? FactValue(
        IEnumerable<RuntimeEventFact> facts,
        string name)
    {
        return facts.LastOrDefault(fact =>
            string.Equals(fact.Name, name, StringComparison.Ordinal))?.Value;
    }

}
