using System.Collections.ObjectModel;
using MentalGymnastics.Core;

namespace MentalGymnastics.App;

public sealed record ProgramDurationForecast(
    int BestCaseCalendarDays,
    int BestCaseTrainingDays,
    int AverageMinutesPerTrainingDay,
    int LongSessionMinutes);

public static class ProgramDurationForecastCatalog
{
    public static ProgramDurationForecast FirstInstallPerfectPath { get; } = new(
        BestCaseCalendarDays: 727,
        BestCaseTrainingDays: 624,
        AverageMinutesPerTrainingDay: 16,
        LongSessionMinutes: 32);

    public static IReadOnlyDictionary<DrillId, int> FirstInstallPerfectPathSessionsByDrill { get; } =
        new ReadOnlyDictionary<DrillId, int>(new Dictionary<DrillId, int>
        {
            [DrillId.FH1TargetHold] = 85,
            [DrillId.FH2DistractorHold] = 232,
            [DrillId.FS1CueSwitch] = 41,
            [DrillId.FS2InvalidCueFilter] = 97,
            [DrillId.WM1DelayedReconstruction] = 61,
            [DrillId.WM2MentalTransform] = 121,
            [DrillId.IR1GoNoGoRule] = 28,
            [DrillId.IR2ExceptionRule] = 150,
            [DrillId.DE1PairDiscrimination] = 70,
            [DrillId.DE2SeededAudit] = 111,
            [DrillId.CO1RuleExtraction] = 20,
            [DrillId.CO2StructureMapping] = 42,
            [DrillId.AI1PressureRepeat] = 20,
            [DrillId.AI2DisruptionRecovery] = 77,
            [DrillId.TI1CompositeTask] = 47,
            [DrillId.TI2GlobalReviewTask] = 28,
        });
}
