namespace MentalGymnastics.Runtime;

public sealed record SessionRuntimeCapabilities(
    bool OfflineCapable,
    bool RequiresAndroidUi,
    bool AllowsAccounts,
    bool AllowsSync,
    bool AllowsBackendServices,
    bool AllowsTelemetry,
    bool AllowsNotifications,
    bool AllowsAiOrApiDependencies,
    bool OwnsProgressionLogic,
    bool OwnsPersistence,
    bool ExposesPersistenceReadyRecords);

public static class SessionRuntimeBoundary
{
    public static SessionRuntimeCapabilities Capabilities { get; } = new(
        OfflineCapable: true,
        RequiresAndroidUi: false,
        AllowsAccounts: false,
        AllowsSync: false,
        AllowsBackendServices: false,
        AllowsTelemetry: false,
        AllowsNotifications: false,
        AllowsAiOrApiDependencies: false,
        OwnsProgressionLogic: false,
        OwnsPersistence: false,
        ExposesPersistenceReadyRecords: true);
}
