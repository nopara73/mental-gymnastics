namespace MentalGymnastics.Core;

public static class FocusHoldStandardMeasurements
{
    public const string ActiveDurationSeconds = TrainingStandardMeasurements.ActiveDurationSeconds;
    public const string MarkedDriftCount = TrainingStandardMeasurements.MarkedDriftCount;
    public const string TargetSubstitutionCount = TrainingStandardMeasurements.TargetSubstitutionCount;
    public const string TargetStatedBeforeSet = TrainingStandardConstraints.TargetStatedBeforeSet;
    public const string DriftMarked = TrainingStandardConstraints.DriftMarked;
}

public static class FocusHoldLevelOneStandard
{
    public const int RequiredDurationSeconds = 180;
    public const int MaximumMarkedDrifts = 5;

    public static EvaluatedStandard Create()
    {
        return ExecutableStandardCatalog.Get(
            BranchCode.FH,
            GlobalLevelId.L1).EvaluatedStandard;
    }
}
