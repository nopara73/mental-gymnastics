using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

internal static class GeneratedContentValidation
{
    public static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown program identifier.");
        }
    }

    public static IReadOnlyList<LoadVariable> RequireLoadVariables(
        IEnumerable<LoadVariable> loadVariables,
        string emptyMessage,
        string invalidMessage,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(loadVariables, parameterName);

        var loadVariableArray = loadVariables.ToArray();
        if (loadVariableArray.Length == 0)
        {
            throw new ArgumentException(emptyMessage, parameterName);
        }

        if (loadVariableArray.Any(variable =>
                string.IsNullOrWhiteSpace(variable.Name) ||
                string.IsNullOrWhiteSpace(variable.Value)))
        {
            throw new ArgumentException(invalidMessage, parameterName);
        }

        return Array.AsReadOnly(loadVariableArray);
    }

    public static IReadOnlyList<CriticalConstraint> RequireCriticalConstraints(
        IEnumerable<CriticalConstraint> criticalConstraints,
        string emptyMessage,
        string invalidMessage,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(criticalConstraints, parameterName);

        var criticalConstraintArray = criticalConstraints.ToArray();
        if (criticalConstraintArray.Length == 0)
        {
            throw new ArgumentException(emptyMessage, parameterName);
        }

        if (criticalConstraintArray.Any(constraint => string.IsNullOrWhiteSpace(constraint.Description)))
        {
            throw new ArgumentException(invalidMessage, parameterName);
        }

        return Array.AsReadOnly(criticalConstraintArray);
    }
}
