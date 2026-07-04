using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public sealed class LocalBranchLevelStateStore
{
    private readonly LocalPractitionerStateStore practitionerStateStore;

    public LocalBranchLevelStateStore(LocalDatabaseOptions options)
        : this(new LocalPractitionerStateStore(options))
    {
    }

    internal LocalBranchLevelStateStore(LocalPractitionerStateStore practitionerStateStore)
    {
        ArgumentNullException.ThrowIfNull(practitionerStateStore);

        this.practitionerStateStore = practitionerStateStore;
    }

    public async ValueTask<IReadOnlyList<BranchLevelStatus>> LoadAllAsync(
        CancellationToken cancellationToken = default)
    {
        var practitionerState = await practitionerStateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);

        return practitionerState?.BranchLevels ?? [];
    }

    public async ValueTask<BranchLevelStatus?> LoadAsync(
        BranchCode branch,
        GlobalLevelId level,
        CancellationToken cancellationToken = default)
    {
        var practitionerState = await practitionerStateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);

        if (practitionerState is null ||
            !practitionerState.TryGetBranchLevelState(branch, level, out var state))
        {
            return null;
        }

        return new BranchLevelStatus(branch, level, state);
    }

    public ValueTask SaveAllAsync(
        IEnumerable<BranchLevelStatus> branchLevels,
        CancellationToken cancellationToken = default)
    {
        return practitionerStateStore.SaveAsync(
            CreatePractitionerState(branchLevels),
            cancellationToken);
    }

    public async ValueTask<BranchLevelStatusTransitionResult> TryApplyTransitionAsync(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelTransition transition,
        CancellationToken cancellationToken = default)
    {
        var practitionerState = await practitionerStateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        var branchLevels = practitionerState?.BranchLevels.ToList() ?? [];
        var currentIndex = branchLevels.FindIndex(status => status.Branch == branch && status.Level == level);
        var currentStatus = currentIndex >= 0
            ? branchLevels[currentIndex]
            : new BranchLevelStatus(branch, level, BranchLevelState.Unopened);

        var result = BranchLevelStateMachine.TryApply(currentStatus, transition);
        if (!result.IsValid)
        {
            return result;
        }

        if (currentIndex >= 0)
        {
            branchLevels[currentIndex] = result.NextStatus;
        }
        else
        {
            branchLevels.Add(result.NextStatus);
        }

        await practitionerStateStore.SaveAsync(
            CreatePractitionerState(branchLevels),
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static PractitionerState CreatePractitionerState(IEnumerable<BranchLevelStatus> branchLevels)
    {
        ArgumentNullException.ThrowIfNull(branchLevels);

        var copiedBranchLevels = branchLevels.ToArray();
        var duplicateBranchLevel = copiedBranchLevels
            .GroupBy(branchLevel => (branchLevel.Branch, branchLevel.Level))
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateBranchLevel is not null)
        {
            throw new ArgumentException(
                $"Branch-level state for {duplicateBranchLevel.Key.Branch} {duplicateBranchLevel.Key.Level} was provided more than once.",
                nameof(branchLevels));
        }

        return new PractitionerState(copiedBranchLevels);
    }
}
