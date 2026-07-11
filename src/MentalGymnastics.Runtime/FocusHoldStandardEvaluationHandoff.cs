using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public sealed record FocusHoldRuntimeEvidenceSummary(
    decimal ActiveDurationSeconds,
    int MarkedDriftCount,
    int ReturnCount,
    int UnreturnedDriftCount,
    int LateReturnCount,
    int TargetSubstitutionCount,
    decimal MaximumReturnSeconds);

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
        var returnEvents = result.RuntimeEvents
            .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.RecoveryCompleted)
            .ToArray();
        var returnedDriftIds = returnEvents
            .Select(runtimeEvent => FactValue(runtimeEvent.Facts, "drift_id"))
            .Where(driftId => driftId is not null)
            .ToHashSet(StringComparer.Ordinal);
        var unreturnedDrifts = driftEvents.Count(runtimeEvent =>
            FactValue(runtimeEvent.Facts, "drift_id") is not { } driftId ||
            !returnedDriftIds.Contains(driftId));
        var lateReturns = returnEvents.Count(runtimeEvent =>
            !string.Equals(
                FactValue(runtimeEvent.Facts, "return_within_window"),
                "true",
                StringComparison.Ordinal));
        var targetSubstitutions = result.RuntimeEvents.Count(runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.ErrorRecorded &&
            string.Equals(
                FactValue(runtimeEvent.Facts, "error_kind"),
                "target_substitution",
                StringComparison.Ordinal));
        var maximumReturn = returnEvents
            .Select(runtimeEvent => ParseDuration(FactValue(runtimeEvent.Facts, "recovery_time")))
            .DefaultIfEmpty(TimeSpan.Zero)
            .Max();

        return new FocusHoldRuntimeEvidenceSummary(
            Convert.ToDecimal(activeDuration.TotalSeconds, CultureInfo.InvariantCulture),
            driftEvents.Length,
            returnEvents.Length,
            unreturnedDrifts,
            lateReturns,
            targetSubstitutions,
            Convert.ToDecimal(maximumReturn.TotalSeconds, CultureInfo.InvariantCulture));
    }

    public static RuntimeStandardEvaluationHandoffInput ToStandardEvaluationInput(
        RuntimeSessionCompletionResult result,
        bool targetStatedBeforeSet)
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
                    FocusHoldStandardMeasurements.UnreturnedDriftCount,
                    evidence.UnreturnedDriftCount),
                new NumericMeasurement(
                    FocusHoldStandardMeasurements.LateReturnCount,
                    evidence.LateReturnCount),
                new NumericMeasurement(
                    FocusHoldStandardMeasurements.TargetSubstitutionCount,
                    evidence.TargetSubstitutionCount),
            ],
            [
                new CriticalConstraintCheck(
                    FocusHoldStandardMeasurements.TargetStatedBeforeSet,
                    targetStatedBeforeSet),
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

    private static TimeSpan ParseDuration(string? value)
    {
        return value is not null &&
            TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out var duration)
                ? duration
                : TimeSpan.Zero;
    }
}
