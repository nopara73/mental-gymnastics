namespace MentalGymnastics.Core;

public static class MaintenanceScope
{
    public static IReadOnlyList<BranchLevelStatus> HighestEarnedByBranch(
        PractitionerState practitionerState)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);

        return practitionerState.BranchLevels
            .Where(status => status.State is
                BranchLevelState.Owned or
                BranchLevelState.Maintenance or
                BranchLevelState.Decayed)
            .GroupBy(status => status.Branch)
            .Select(group => group
                .OrderByDescending(status => status.Level)
                .First())
            .OrderBy(status => status.Branch)
            .ToArray();
    }
}
