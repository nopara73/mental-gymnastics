using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed record GeneratedContentCapabilities(
    bool OfflineCapable,
    bool DeterministicWithExplicitInputs,
    bool RequiresAndroidUi,
    bool AllowsAccounts,
    bool AllowsSync,
    bool AllowsBackendServices,
    bool AllowsTelemetry,
    bool AllowsNotifications,
    bool AllowsAiOrApiDependencies,
    bool OwnsProgressionLogic,
    bool OwnsPersistence,
    bool OwnsRuntimeExecution,
    bool ExposesRuntimeConsumableInstances,
    bool ExposesPersistenceReadyInstanceFacts);

public static class GeneratedContentBoundary
{
    public static GeneratedContentCapabilities Capabilities { get; } = new(
        OfflineCapable: true,
        DeterministicWithExplicitInputs: true,
        RequiresAndroidUi: false,
        AllowsAccounts: false,
        AllowsSync: false,
        AllowsBackendServices: false,
        AllowsTelemetry: false,
        AllowsNotifications: false,
        AllowsAiOrApiDependencies: false,
        OwnsProgressionLogic: false,
        OwnsPersistence: false,
        OwnsRuntimeExecution: false,
        ExposesRuntimeConsumableInstances: true,
        ExposesPersistenceReadyInstanceFacts: true);
}

public sealed class GeneratedDrillInstanceDescriptor
{
    public GeneratedDrillInstanceDescriptor(
        string instanceId,
        PromptContentIdentity contentIdentity,
        string contentVersion,
        PromptFreshnessPolicy retestFreshnessPolicy,
        IEnumerable<LoadVariable> loadVariables,
        IEnumerable<CriticalConstraint> criticalConstraints)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Generated drill instance id is required.", nameof(instanceId));
        }

        ArgumentNullException.ThrowIfNull(contentIdentity);

        if (string.IsNullOrWhiteSpace(contentVersion))
        {
            throw new ArgumentException("Generated drill content version is required.", nameof(contentVersion));
        }

        GeneratedContentValidation.EnsureDefined(contentIdentity.Branch, nameof(contentIdentity));
        GeneratedContentValidation.EnsureDefined(contentIdentity.Level, nameof(contentIdentity));
        GeneratedContentValidation.EnsureDefined(contentIdentity.Drill, nameof(contentIdentity));
        GeneratedContentValidation.EnsureDefined(contentIdentity.Kind, nameof(contentIdentity));
        GeneratedContentValidation.EnsureDefined(retestFreshnessPolicy, nameof(retestFreshnessPolicy));

        var loadVariableArray = GeneratedContentValidation.RequireLoadVariables(
            loadVariables,
            "Generated drill instances must expose the load variables they apply.",
            "Generated drill instance load variables must include a name and value.",
            nameof(loadVariables));
        var criticalConstraintArray = GeneratedContentValidation.RequireCriticalConstraints(
            criticalConstraints,
            "Generated drill instances must preserve at least one critical or honesty constraint.",
            "Generated drill instance critical constraints must include descriptions.",
            nameof(criticalConstraints));

        InstanceId = instanceId;
        ContentIdentity = contentIdentity;
        ContentVersion = contentVersion;
        RetestFreshnessPolicy = retestFreshnessPolicy;
        LoadVariables = loadVariableArray;
        CriticalConstraints = criticalConstraintArray;
        IdentityMetadata = new GeneratedDrillInstanceIdentityMetadata(
            instanceId,
            contentIdentity.ContentId,
            contentVersion,
            contentIdentity.Branch,
            contentIdentity.Level,
            contentIdentity.Drill,
            contentIdentity.Kind,
            contentIdentity.EquivalenceClass,
            retestFreshnessPolicy,
            GeneratedContentStableHash.BuildLoadContextFingerprint(loadVariableArray));
    }

    public string InstanceId { get; }

    public PromptContentIdentity ContentIdentity { get; }

    public string ContentVersion { get; }

    public PromptFreshnessPolicy RetestFreshnessPolicy { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public IReadOnlyList<CriticalConstraint> CriticalConstraints { get; }

    public GeneratedDrillInstanceIdentityMetadata IdentityMetadata { get; }

    public BranchCode Branch => ContentIdentity.Branch;

    public GlobalLevelId Level => ContentIdentity.Level;

    public DrillId Drill => ContentIdentity.Drill;

    public PromptContentKind ContentKind => ContentIdentity.Kind;

    public string EquivalenceClass => ContentIdentity.EquivalenceClass;

    public bool CanBeConsumedByRuntime => true;

    public bool CanBeRecordedByPersistence => true;
}
