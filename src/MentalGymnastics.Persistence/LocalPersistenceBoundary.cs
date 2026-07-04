namespace MentalGymnastics.Persistence;

public sealed record LocalPersistenceCapabilities(
    bool OfflineOnly,
    bool Userless,
    bool DeviceLocal,
    bool AllowsAccounts,
    bool AllowsSync,
    bool AllowsBackendServices,
    bool AllowsTelemetry,
    bool AllowsPushNotifications,
    bool AllowsAiOrApiDependencies,
    bool OwnsProgressionLogic);

public static class LocalPersistenceBoundary
{
    public static LocalPersistenceCapabilities Capabilities { get; } = new(
        OfflineOnly: true,
        Userless: true,
        DeviceLocal: true,
        AllowsAccounts: false,
        AllowsSync: false,
        AllowsBackendServices: false,
        AllowsTelemetry: false,
        AllowsPushNotifications: false,
        AllowsAiOrApiDependencies: false,
        OwnsProgressionLogic: false);
}
