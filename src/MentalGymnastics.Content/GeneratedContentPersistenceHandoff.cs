using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class GeneratedContentPersistenceAuditMaterial
{
    public GeneratedContentPersistenceAuditMaterial(
        GeneratedContentMaterialKind kind,
        string name,
        string value)
    {
        GeneratedContentValidation.EnsureDefined(kind, nameof(kind));

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Generated persistence audit material name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Generated persistence audit material value is required.", nameof(value));
        }

        Kind = kind;
        Name = name;
        Value = value;
    }

    public GeneratedContentMaterialKind Kind { get; }

    public string Name { get; }

    public string Value { get; }
}

public sealed class GeneratedContentPersistenceHandoff
{
    private GeneratedContentPersistenceHandoff(
        string instanceId,
        TrainingDate generatedOn,
        PromptContentIdentity contentIdentity,
        string contentVersion,
        IEnumerable<LoadVariable> loadVariables,
        PromptFreshnessPolicy freshnessPolicy,
        string contentSummary,
        IEnumerable<GeneratedContentPersistenceAuditMaterial> auditMaterials,
        string loadContextFingerprint)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Generated persistence handoff instance id is required.", nameof(instanceId));
        }

        ArgumentNullException.ThrowIfNull(contentIdentity);

        if (string.IsNullOrWhiteSpace(contentVersion))
        {
            throw new ArgumentException("Generated persistence handoff content version is required.", nameof(contentVersion));
        }

        GeneratedContentValidation.EnsureDefined(freshnessPolicy, nameof(freshnessPolicy));

        if (string.IsNullOrWhiteSpace(contentSummary))
        {
            throw new ArgumentException("Generated persistence handoff content summary is required.", nameof(contentSummary));
        }

        if (string.IsNullOrWhiteSpace(loadContextFingerprint))
        {
            throw new ArgumentException(
                "Generated persistence handoff load context fingerprint is required.",
                nameof(loadContextFingerprint));
        }

        var loadVariableArray = GeneratedContentValidation.RequireLoadVariables(
            loadVariables,
            "Generated persistence handoffs must include load variables.",
            "Generated persistence handoff load variables must include a name and value.",
            nameof(loadVariables));
        var auditMaterialArray = auditMaterials.ToArray();
        if (auditMaterialArray.Length == 0)
        {
            throw new ArgumentException(
                "Generated persistence handoffs must include audit-relevant material.",
                nameof(auditMaterials));
        }

        foreach (var material in auditMaterialArray)
        {
            ArgumentNullException.ThrowIfNull(material);
        }

        InstanceId = instanceId;
        GeneratedOn = generatedOn;
        ContentIdentity = contentIdentity;
        ContentVersion = contentVersion;
        LoadVariables = loadVariableArray;
        FreshnessPolicy = freshnessPolicy;
        ContentSummary = contentSummary;
        AuditMaterials = Array.AsReadOnly(auditMaterialArray);
        LoadContextFingerprint = loadContextFingerprint;
    }

    public string InstanceId { get; }

    public TrainingDate GeneratedOn { get; }

    public PromptContentIdentity ContentIdentity { get; }

    public string ContentVersion { get; }

    public BranchCode Branch => ContentIdentity.Branch;

    public GlobalLevelId Level => ContentIdentity.Level;

    public DrillId Drill => ContentIdentity.Drill;

    public PromptContentKind ContentKind => ContentIdentity.Kind;

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public PromptFreshnessPolicy FreshnessPolicy { get; }

    public string ContentSummary { get; }

    public IReadOnlyList<GeneratedContentPersistenceAuditMaterial> AuditMaterials { get; }

    public string LoadContextFingerprint { get; }

    public bool CanBeRecordedByPersistence => true;

    public bool OwnsPersistence => false;

    public bool GrantsAdvancement => false;

    public static GeneratedContentPersistenceHandoff Restore(
        string instanceId,
        TrainingDate generatedOn,
        PromptContentIdentity contentIdentity,
        string contentVersion,
        IEnumerable<LoadVariable> loadVariables,
        PromptFreshnessPolicy freshnessPolicy,
        string contentSummary,
        IEnumerable<GeneratedContentPersistenceAuditMaterial> auditMaterials)
    {
        ArgumentNullException.ThrowIfNull(contentIdentity);
        ArgumentNullException.ThrowIfNull(loadVariables);

        var loadVariableArray = loadVariables.ToArray();
        return new GeneratedContentPersistenceHandoff(
            instanceId,
            generatedOn,
            contentIdentity,
            contentVersion,
            loadVariableArray,
            freshnessPolicy,
            contentSummary,
            auditMaterials,
            GeneratedContentStableHash.BuildLoadContextFingerprint(loadVariableArray));
    }

    internal static GeneratedContentPersistenceHandoff Create(
        GeneratedDrillContentResult result,
        TrainingDate generatedOn,
        string contentSummary,
        IEnumerable<GeneratedContentPersistenceAuditMaterial> auditMaterials)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new GeneratedContentPersistenceHandoff(
            result.InstanceId,
            generatedOn,
            result.Instance.ContentIdentity,
            result.ContentVersion,
            result.Request.LoadVariables,
            result.Request.FreshnessPolicy,
            contentSummary,
            auditMaterials,
            result.LoadContextFingerprint);
    }
}

public static class GeneratedContentPersistenceHandoffMapper
{
    public static GeneratedContentPersistenceHandoff Create(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials,
        TrainingDate generatedOn)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);

        var materialArray = GeneratedContentBoundaryHandoffValidation.EnsureCanHandoff(
            result,
            materials);

        return GeneratedContentPersistenceHandoff.Create(
            result,
            generatedOn,
            BuildContentSummary(result),
            materialArray.Select(material => new GeneratedContentPersistenceAuditMaterial(
                material.Kind,
                material.Name,
                material.Value)));
    }

    private static string BuildContentSummary(GeneratedDrillContentResult result)
    {
        return string.Join(
            "; ",
            result.PayloadFacts.Select(fact => $"{fact.Name}={fact.Value}"));
    }
}
