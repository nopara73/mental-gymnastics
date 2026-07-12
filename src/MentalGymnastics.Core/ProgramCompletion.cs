namespace MentalGymnastics.Core;

public enum ProgramCompletionState
{
    InProgress,
    AwaitingGlobalReview,
    CompleteMaintaining,
    CompleteMaintenanceRequired,
}

public sealed record ProgramCompletionResult(
    ProgramCompletionState State,
    int EarnedLevelCount,
    int TotalLevelCount,
    bool GlobalReviewPassed,
    int MaintenanceRequirementCount)
{
    public bool CurriculumComplete => State is
        ProgramCompletionState.CompleteMaintaining or
        ProgramCompletionState.CompleteMaintenanceRequired;

    public int RemainingLevelCount => Math.Max(0, TotalLevelCount - EarnedLevelCount);
}

public static class ProgramCompletionEvaluator
{
    public static ProgramCompletionResult Evaluate(
        PractitionerState practitionerState,
        IEnumerable<MaintenanceCurrencyResult> maintenanceCurrency,
        GlobalReviewResult? lastGlobalReview)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);
        ArgumentNullException.ThrowIfNull(maintenanceCurrency);

        var expectedLevels = ProgramCatalog.Branches.Count * ProgramCatalog.GlobalLevels.Count;
        var earned = practitionerState.BranchLevels.Count(status => status.State is
            BranchLevelState.Owned or
            BranchLevelState.Maintenance or
            BranchLevelState.Decayed);
        var maintenanceRequired = maintenanceCurrency.Count(currency => currency.State is
            MaintenanceCurrencyState.Due or
            MaintenanceCurrencyState.Warning or
            MaintenanceCurrencyState.Failed);
        var reviewPassed = lastGlobalReview?.Passed == true;
        var state = earned < expectedLevels
            ? ProgramCompletionState.InProgress
            : !reviewPassed
                ? ProgramCompletionState.AwaitingGlobalReview
                : maintenanceRequired > 0 || practitionerState.BranchLevels.Any(status =>
                    status.State == BranchLevelState.Decayed)
                    ? ProgramCompletionState.CompleteMaintenanceRequired
                    : ProgramCompletionState.CompleteMaintaining;

        return new ProgramCompletionResult(
            state,
            earned,
            expectedLevels,
            reviewPassed,
            maintenanceRequired);
    }
}
