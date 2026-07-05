using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App;

public sealed class AppStartupConfiguration
{
    private AppStartupConfiguration(LocalDatabaseOptions localDatabaseOptions)
    {
        LocalDatabaseOptions = localDatabaseOptions;
    }

    public string LocalDatabasePath => LocalDatabaseOptions.DatabasePath;

    public LocalDatabaseOptions LocalDatabaseOptions { get; }

    public static AppStartupConfiguration ForAppOwnedLocalStoragePath(string localDatabasePath)
    {
        return new AppStartupConfiguration(LocalDatabaseOptions.ForAppOwnedPath(localDatabasePath));
    }
}

public sealed class MentalGymnasticsAppStartup
{
    private readonly LocalDatabaseInitializer initializer;
    private readonly LocalPractitionerStateStore practitionerStateStore;

    public MentalGymnasticsAppStartup(AppStartupConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        initializer = new LocalDatabaseInitializer(configuration.LocalDatabaseOptions);
        practitionerStateStore = new LocalPractitionerStateStore(configuration.LocalDatabaseOptions);
    }

    public AppStartupConfiguration Configuration { get; }

    public async ValueTask<LocalDatabaseInitializationResult> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await EnsureFirstRunPractitionerStateAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async ValueTask EnsureFirstRunPractitionerStateAsync(CancellationToken cancellationToken)
    {
        var current = await practitionerStateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (current is not null)
        {
            return;
        }

        await practitionerStateStore.SaveAsync(
            InitialPractitionerStateFactory.Create(),
            cancellationToken).ConfigureAwait(false);
    }
}

internal static class InitialPractitionerStateFactory
{
    public static PractitionerState Create()
    {
        var branchLevels = ProgramCatalog.Branches
            .SelectMany(
                _ => ProgramCatalog.GlobalLevels,
                (branch, level) => new BranchLevelStatus(
                    branch.Code,
                    level.Id,
                    BranchLevelState.Unopened))
            .ToArray();

        var universalStartIndex = Array.FindIndex(
            branchLevels,
            status => status.Branch == BranchCode.FH && status.Level == GlobalLevelId.L1);
        if (universalStartIndex < 0)
        {
            throw new InvalidOperationException("The core program catalog does not define the FH L1 universal start.");
        }

        var opened = BranchLevelStateMachine.TryApply(
            branchLevels[universalStartIndex],
            BranchLevelTransition.OpenForTraining);
        if (!opened.IsValid)
        {
            throw new InvalidOperationException("The core branch-level state machine rejected the universal start state.");
        }

        branchLevels[universalStartIndex] = opened.NextStatus;

        return new PractitionerState(branchLevels);
    }
}
