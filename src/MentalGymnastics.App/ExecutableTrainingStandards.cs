using MentalGymnastics.Core;

namespace MentalGymnastics.App;

internal static class ExecutableTrainingStandards
{
    public static bool Supports(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill)
    {
        return ExecutableStandardCatalog.Supports(branch, level, drill);
    }

    public static AppTrainingSessionType HonestSessionType(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        AppTrainingSessionType requested)
    {
        return Supports(branch, level, drill)
            ? requested
            : AppTrainingSessionType.Practice;
    }
}
