using System.Globalization;
using System.Text.RegularExpressions;
using MentalGymnastics.Core;

namespace MentalGymnastics.App;

public static partial class TrainingDoseDurationEstimator
{
    public static TimeSpan Estimate(
        DrillId drill,
        IReadOnlyList<LoadVariable> loadVariables)
    {
        ArgumentNullException.ThrowIfNull(loadVariables);

        var estimate = drill switch
        {
            DrillId.FH1TargetHold or DrillId.FH2DistractorHold =>
                Value(loadVariables, "duration") + TimeSpan.FromMinutes(1),
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter =>
                CueDuration(loadVariables) + TimeSpan.FromMinutes(1),
            DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform =>
                Value(loadVariables, "delay") + TimeSpan.FromMinutes(4),
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => TimeSpan.FromMinutes(4),
            DrillId.DE1PairDiscrimination =>
                Value(loadVariables, "time limit") + TimeSpan.FromMinutes(1),
            DrillId.DE2SeededAudit =>
                Value(loadVariables, "audit delay") + TimeSpan.FromMinutes(5),
            DrillId.CO1RuleExtraction => TimeSpan.FromMinutes(7),
            DrillId.CO2StructureMapping => TimeSpan.FromMinutes(9),
            DrillId.AI1PressureRepeat =>
                Value(loadVariables, "time pressure") + TimeSpan.FromMinutes(3),
            DrillId.AI2DisruptionRecovery => TimeSpan.FromMinutes(7),
            DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask =>
                Value(loadVariables, "task length") +
                Value(loadVariables, "delay") +
                TimeSpan.FromMinutes(drill == DrillId.TI2GlobalReviewTask ? 7 : 4),
            _ => TimeSpan.FromMinutes(5),
        };

        return estimate < TimeSpan.FromMinutes(2)
            ? TimeSpan.FromMinutes(2)
            : estimate;
    }

    public static int RoundedMinutes(
        DrillId drill,
        IReadOnlyList<LoadVariable> loadVariables)
    {
        return (int)Math.Ceiling(Estimate(drill, loadVariables).TotalMinutes);
    }

    private static TimeSpan CueDuration(IReadOnlyList<LoadVariable> loadVariables)
    {
        var count = IntegerValue(loadVariables, "switch count") ?? 6;
        var interval = Value(loadVariables, "cue density");
        return interval <= TimeSpan.Zero
            ? TimeSpan.FromMinutes(4)
            : TimeSpan.FromTicks(interval.Ticks * count);
    }

    private static TimeSpan Value(
        IEnumerable<LoadVariable> loadVariables,
        string name)
    {
        var value = loadVariables.FirstOrDefault(variable => string.Equals(
            variable.Name,
            name,
            StringComparison.OrdinalIgnoreCase))?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeSpan.Zero;
        }

        var match = DurationValueRegex().Match(value);
        if (!match.Success || !decimal.TryParse(
                match.Groups[1].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return TimeSpan.Zero;
        }

        return match.Groups[2].Value.StartsWith("second", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromSeconds((double)amount)
            : TimeSpan.FromMinutes((double)amount);
    }

    private static int? IntegerValue(
        IEnumerable<LoadVariable> loadVariables,
        string name)
    {
        var value = loadVariables.FirstOrDefault(variable => string.Equals(
            variable.Name,
            name,
            StringComparison.OrdinalIgnoreCase))?.Value;
        var match = value is null ? Match.Empty : IntegerRegex().Match(value);
        return match.Success && int.TryParse(match.Value, out var count) ? count : null;
    }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(seconds?|minutes?)", RegexOptions.IgnoreCase)]
    private static partial Regex DurationValueRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex IntegerRegex();
}
