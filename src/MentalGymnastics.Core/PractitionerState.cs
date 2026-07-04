namespace MentalGymnastics.Core;

public readonly record struct BranchLevelStatus(
    BranchCode Branch,
    GlobalLevelId Level,
    BranchLevelState State);

public sealed class PractitionerState
{
    private readonly IReadOnlyDictionary<(BranchCode Branch, GlobalLevelId Level), BranchLevelState> branchLevelStates;

    public PractitionerState(IEnumerable<BranchLevelStatus> branchLevels)
    {
        ArgumentNullException.ThrowIfNull(branchLevels);

        BranchLevels = branchLevels.ToArray();
        branchLevelStates = BranchLevels.ToDictionary(
            branchLevel => (branchLevel.Branch, branchLevel.Level),
            branchLevel => branchLevel.State);
    }

    public IReadOnlyList<BranchLevelStatus> BranchLevels { get; }

    public BranchLevelState GetBranchLevelState(BranchCode branch, GlobalLevelId level)
    {
        return branchLevelStates[(branch, level)];
    }

    public bool TryGetBranchLevelState(
        BranchCode branch,
        GlobalLevelId level,
        out BranchLevelState state)
    {
        return branchLevelStates.TryGetValue((branch, level), out state);
    }
}
