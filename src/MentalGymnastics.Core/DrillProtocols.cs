namespace MentalGymnastics.Core;

public sealed record DrillProtocolLoad(string Description);

public sealed record DrillHonestyConstraint(string Description);

public sealed record DrillFailureMode(string Description);

public sealed record DrillRegressionSpecification(
    string Description,
    IReadOnlyList<DrillHonestyConstraint> PreservedHonestyConstraints);

public sealed class DrillProtocolSpecification
{
    public DrillProtocolSpecification(
        DrillId id,
        string code,
        string name,
        string purpose,
        IEnumerable<CapacityId> capacitiesTrained,
        IEnumerable<DrillProtocolLoad> loadApplied,
        IEnumerable<DrillHonestyConstraint> honestyConstraints,
        string cleanPerformance,
        IEnumerable<DrillFailureMode> failureModes,
        DrillRegressionSpecification regression)
    {
        ArgumentNullException.ThrowIfNull(capacitiesTrained);
        ArgumentNullException.ThrowIfNull(loadApplied);
        ArgumentNullException.ThrowIfNull(honestyConstraints);
        ArgumentNullException.ThrowIfNull(failureModes);
        ArgumentNullException.ThrowIfNull(regression);

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A drill protocol must preserve the drill code.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A drill protocol must preserve the drill name.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("A drill protocol must preserve the drill purpose.", nameof(purpose));
        }

        if (string.IsNullOrWhiteSpace(cleanPerformance))
        {
            throw new ArgumentException(
                "A drill protocol must preserve clean performance criteria.",
                nameof(cleanPerformance));
        }

        var capacityArray = capacitiesTrained.ToArray();
        if (capacityArray.Length == 0)
        {
            throw new ArgumentException(
                "A drill protocol must name at least one trained capacity.",
                nameof(capacitiesTrained));
        }

        var loadArray = loadApplied.ToArray();
        if (loadArray.Length == 0 || loadArray.Any(load => string.IsNullOrWhiteSpace(load.Description)))
        {
            throw new ArgumentException(
                "A drill protocol must name the load it applies.",
                nameof(loadApplied));
        }

        var constraintArray = honestyConstraints.ToArray();
        if (constraintArray.Length == 0 ||
            constraintArray.Any(constraint => string.IsNullOrWhiteSpace(constraint.Description)))
        {
            throw new ArgumentException(
                "A drill protocol must preserve honesty constraints.",
                nameof(honestyConstraints));
        }

        var failureModeArray = failureModes.ToArray();
        if (failureModeArray.Length == 0 ||
            failureModeArray.Any(mode => string.IsNullOrWhiteSpace(mode.Description)))
        {
            throw new ArgumentException(
                "A drill protocol must preserve expected failure modes.",
                nameof(failureModes));
        }

        if (string.IsNullOrWhiteSpace(regression.Description) ||
            regression.PreservedHonestyConstraints.Count == 0)
        {
            throw new ArgumentException(
                "A drill protocol must define a regression that preserves the honesty constraint.",
                nameof(regression));
        }

        Id = id;
        Code = code;
        Name = name;
        Purpose = purpose;
        CapacitiesTrained = capacityArray;
        LoadApplied = loadArray;
        HonestyConstraints = constraintArray;
        CleanPerformance = cleanPerformance;
        FailureModes = failureModeArray;
        Regression = regression;
    }

    public DrillId Id { get; }

    public string Code { get; }

    public string Name { get; }

    public string Purpose { get; }

    public IReadOnlyList<CapacityId> CapacitiesTrained { get; }

    public IReadOnlyList<DrillProtocolLoad> LoadApplied { get; }

    public IReadOnlyList<DrillHonestyConstraint> HonestyConstraints { get; }

    public string CleanPerformance { get; }

    public IReadOnlyList<DrillFailureMode> FailureModes { get; }

    public DrillRegressionSpecification Regression { get; }
}

public static class DrillProtocolCatalog
{
    public static IReadOnlyList<DrillProtocolSpecification> StandardDrills { get; } =
        ProgramCatalog.Drills.Select(FromDefinition).ToArray();

    private static DrillProtocolSpecification FromDefinition(DrillDefinition definition)
    {
        var honestyConstraints = SplitProtocolText(definition.HonestyConstraint)
            .Select(description => new DrillHonestyConstraint(description))
            .ToArray();

        return new DrillProtocolSpecification(
            definition.Id,
            definition.Code,
            definition.Name,
            definition.Purpose,
            definition.CapacityTrained,
            SplitProtocolText(definition.LoadApplied).Select(description => new DrillProtocolLoad(description)),
            honestyConstraints,
            definition.CleanPerformance,
            SplitProtocolText(definition.FailureModes).Select(description => new DrillFailureMode(description)),
            new DrillRegressionSpecification(definition.Regression, honestyConstraints));
    }

    private static IReadOnlyList<string> SplitProtocolText(string text)
    {
        return text
            .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }
}
