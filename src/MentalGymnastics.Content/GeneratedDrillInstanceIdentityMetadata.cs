using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class GeneratedDrillInstanceIdentityMetadata
{
    public GeneratedDrillInstanceIdentityMetadata(
        string instanceId,
        string contentId,
        string contentVersion,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        PromptContentKind contentKind,
        string equivalenceClass,
        PromptFreshnessPolicy retestFreshnessPolicy,
        string loadContextFingerprint)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Generated drill instance id is required.", nameof(instanceId));
        }

        if (string.IsNullOrWhiteSpace(contentId))
        {
            throw new ArgumentException("Generated content id is required.", nameof(contentId));
        }

        if (string.IsNullOrWhiteSpace(contentVersion))
        {
            throw new ArgumentException("Generated content version is required.", nameof(contentVersion));
        }

        GeneratedContentValidation.EnsureDefined(branch, nameof(branch));
        GeneratedContentValidation.EnsureDefined(level, nameof(level));
        GeneratedContentValidation.EnsureDefined(drill, nameof(drill));
        GeneratedContentValidation.EnsureDefined(contentKind, nameof(contentKind));

        if (string.IsNullOrWhiteSpace(equivalenceClass))
        {
            throw new ArgumentException("Generated content equivalence class is required.", nameof(equivalenceClass));
        }

        GeneratedContentValidation.EnsureDefined(retestFreshnessPolicy, nameof(retestFreshnessPolicy));

        if (string.IsNullOrWhiteSpace(loadContextFingerprint))
        {
            throw new ArgumentException("Generated load context fingerprint is required.", nameof(loadContextFingerprint));
        }

        InstanceId = instanceId;
        ContentId = contentId;
        ContentVersion = contentVersion;
        Branch = branch;
        Level = level;
        Drill = drill;
        ContentKind = contentKind;
        EquivalenceClass = equivalenceClass;
        RetestFreshnessPolicy = retestFreshnessPolicy;
        LoadContextFingerprint = loadContextFingerprint;
    }

    public string InstanceId { get; }

    public string ContentId { get; }

    public string ContentVersion { get; }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public PromptContentKind ContentKind { get; }

    public string EquivalenceClass { get; }

    public PromptFreshnessPolicy RetestFreshnessPolicy { get; }

    public string LoadContextFingerprint { get; }
}
