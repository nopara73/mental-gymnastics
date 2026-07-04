using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public sealed class RuntimeGeneratedDrillInstanceIdentity
{
    public RuntimeGeneratedDrillInstanceIdentity(
        string instanceId,
        PromptContentIdentity contentIdentity,
        string contentVersion)
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

        InstanceId = instanceId;
        ContentIdentity = contentIdentity;
        ContentVersion = contentVersion;
    }

    public string InstanceId { get; }

    public PromptContentIdentity ContentIdentity { get; }

    public string ContentVersion { get; }
}
public sealed class RuntimeSessionDefinition
{
    public RuntimeSessionDefinition(
        SessionType sessionType,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        IEnumerable<LoadVariable> loadVariables,
        BranchLevelStandard standard,
        IEnumerable<CriticalConstraint> criticalConstraints,
        RuntimeGeneratedDrillInstanceIdentity? generatedDrillInstance = null)
    {
        EnsureDefined(sessionType, nameof(sessionType));
        EnsureDefined(branch, nameof(branch));
        EnsureDefined(level, nameof(level));
        EnsureDefined(drill, nameof(drill));
        ArgumentNullException.ThrowIfNull(loadVariables);
        ArgumentNullException.ThrowIfNull(standard);
        ArgumentNullException.ThrowIfNull(criticalConstraints);

        var loadVariableArray = loadVariables.ToArray();
        if (loadVariableArray.Length == 0)
        {
            throw new ArgumentException(
                "A runtime session definition must include load variables.",
                nameof(loadVariables));
        }

        if (loadVariableArray.Any(variable =>
                string.IsNullOrWhiteSpace(variable.Name) ||
                string.IsNullOrWhiteSpace(variable.Value)))
        {
            throw new ArgumentException(
                "Runtime session load variables must include a name and value.",
                nameof(loadVariables));
        }

        if (standard.Branch != branch || standard.Level != level)
        {
            throw new ArgumentException(
                "The stated standard must match the session branch and level.",
                nameof(standard));
        }

        if (string.IsNullOrWhiteSpace(standard.Standard))
        {
            throw new ArgumentException("The stated standard is required.", nameof(standard));
        }

        var criticalConstraintArray = criticalConstraints.ToArray();
        if (criticalConstraintArray.Length == 0)
        {
            throw new ArgumentException(
                "A runtime session definition must include critical constraints.",
                nameof(criticalConstraints));
        }

        if (criticalConstraintArray.Any(constraint => string.IsNullOrWhiteSpace(constraint.Description)))
        {
            throw new ArgumentException(
                "Runtime session critical constraints must include descriptions.",
                nameof(criticalConstraints));
        }

        if (generatedDrillInstance is not null &&
            (generatedDrillInstance.ContentIdentity.Branch != branch ||
                generatedDrillInstance.ContentIdentity.Level != level ||
                generatedDrillInstance.ContentIdentity.Drill != drill))
        {
            throw new ArgumentException(
                "Generated drill instance identity must match the session branch, level, and drill.",
                nameof(generatedDrillInstance));
        }

        SessionType = sessionType;
        Branch = branch;
        Level = level;
        Drill = drill;
        LoadVariables = Array.AsReadOnly(loadVariableArray);
        Standard = standard;
        CriticalConstraints = Array.AsReadOnly(criticalConstraintArray);
        GeneratedDrillInstance = generatedDrillInstance;
    }

    public SessionType SessionType { get; }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public BranchLevelStandard Standard { get; }

    public IReadOnlyList<CriticalConstraint> CriticalConstraints { get; }

    public RuntimeGeneratedDrillInstanceIdentity? GeneratedDrillInstance { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown program identifier.");
        }
    }
}
