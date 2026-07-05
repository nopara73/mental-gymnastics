using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App;

public sealed record ApplicationIntegrationCapabilities(
    bool OfflineCapable,
    bool RequiresAndroidUi,
    bool AllowsAccounts,
    bool AllowsSync,
    bool AllowsBackendServices,
    bool AllowsTelemetry,
    bool AllowsNotifications,
    bool AllowsAnalytics,
    bool AllowsAiOrApiDependencies,
    bool ReplacesJsonPersistenceWithSqlite,
    bool OwnsProgressionRules,
    bool OwnsPersistenceInternals,
    bool OwnsRuntimeExecution,
    bool OwnsGeneratedContent,
    bool ComposesCore,
    bool ComposesPersistence,
    bool ComposesRuntime,
    bool ComposesGeneratedContent);

public sealed class ApplicationLayerComposition
{
    public ApplicationLayerComposition(
        BranchDefinition branch,
        LocalPersistenceCapabilities persistence,
        SessionRuntimeCapabilities runtime,
        GeneratedContentCapabilities content)
    {
        Branch = branch ?? throw new ArgumentNullException(nameof(branch));
        Persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public BranchDefinition Branch { get; }

    public LocalPersistenceCapabilities Persistence { get; }

    public SessionRuntimeCapabilities Runtime { get; }

    public GeneratedContentCapabilities Content { get; }

    public bool CanComposeExistingLayers =>
        Persistence.OfflineOnly
        && Persistence.DeviceLocal
        && Runtime.OfflineCapable
        && Runtime.ExposesPersistenceReadyRecords
        && Content.OfflineCapable
        && Content.DeterministicWithExplicitInputs
        && Content.ExposesRuntimeConsumableInstances
        && Content.ExposesPersistenceReadyInstanceFacts;
}

public static class ApplicationIntegrationBoundary
{
    public static ApplicationIntegrationCapabilities Capabilities { get; } = new(
        OfflineCapable: true,
        RequiresAndroidUi: false,
        AllowsAccounts: false,
        AllowsSync: false,
        AllowsBackendServices: false,
        AllowsTelemetry: false,
        AllowsNotifications: false,
        AllowsAnalytics: false,
        AllowsAiOrApiDependencies: false,
        ReplacesJsonPersistenceWithSqlite: false,
        OwnsProgressionRules: false,
        OwnsPersistenceInternals: false,
        OwnsRuntimeExecution: false,
        OwnsGeneratedContent: false,
        ComposesCore: true,
        ComposesPersistence: true,
        ComposesRuntime: true,
        ComposesGeneratedContent: true);

    public static ApplicationLayerComposition ComposeSmokeTest(BranchCode branchCode)
    {
        var branch = ProgramCatalog.Branches.Single(item => item.Code == branchCode);

        return new ApplicationLayerComposition(
            branch,
            LocalPersistenceBoundary.Capabilities,
            SessionRuntimeBoundary.Capabilities,
            GeneratedContentBoundary.Capabilities);
    }
}
