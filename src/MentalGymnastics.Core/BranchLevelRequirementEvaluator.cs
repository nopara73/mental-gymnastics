namespace MentalGymnastics.Core;

internal static class BranchLevelRequirementEvaluator
{
    public static bool IsSatisfied(
        PractitionerState practitionerState,
        BranchLevelRequirement requirement)
    {
        return practitionerState.TryGetBranchLevelState(requirement.Branch, requirement.Level, out var actualState) &&
            StateSatisfies(actualState, requirement.RequiredState);
    }

    public static bool StateSatisfies(
        BranchLevelState actualState,
        BranchLevelState requiredState)
    {
        return requiredState switch
        {
            BranchLevelState.PassedOnce => actualState is
                BranchLevelState.PassedOnce or
                BranchLevelState.Stabilizing or
                BranchLevelState.Owned or
                BranchLevelState.Maintenance,
            BranchLevelState.Owned => actualState is BranchLevelState.Owned or BranchLevelState.Maintenance,
            _ => actualState == requiredState,
        };
    }
}
