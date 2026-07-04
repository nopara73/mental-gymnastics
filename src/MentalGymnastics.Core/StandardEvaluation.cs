namespace MentalGymnastics.Core;

public sealed record NumericThreshold(
    string MeasurementName,
    NumericThresholdDirection Direction,
    decimal Value)
{
    public static NumericThreshold AtLeast(string measurementName, decimal value)
    {
        return new NumericThreshold(measurementName, NumericThresholdDirection.AtLeast, value);
    }

    public static NumericThreshold AtMost(string measurementName, decimal value)
    {
        return new NumericThreshold(measurementName, NumericThresholdDirection.AtMost, value);
    }
}

public sealed record NumericMeasurement(
    string Name,
    decimal Value);

public sealed record CriticalConstraintRequirement(
    string Id,
    string Description);

public sealed record CriticalConstraintCheck(
    string Id,
    bool Satisfied);

public sealed class EvaluatedStandard
{
    public EvaluatedStandard(
        string name,
        IEnumerable<NumericThreshold> numericThresholds,
        IEnumerable<CriticalConstraintRequirement> criticalConstraints,
        bool requiresCompleteOutput,
        RubricOutcome? requiredRubric)
    {
        ArgumentNullException.ThrowIfNull(numericThresholds);
        ArgumentNullException.ThrowIfNull(criticalConstraints);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Standard name is required.", nameof(name));
        }

        Name = name;
        NumericThresholds = numericThresholds.ToArray();
        CriticalConstraints = criticalConstraints.ToArray();
        RequiresCompleteOutput = requiresCompleteOutput;
        RequiredRubric = requiredRubric;
    }

    public string Name { get; }

    public IReadOnlyList<NumericThreshold> NumericThresholds { get; }

    public IReadOnlyList<CriticalConstraintRequirement> CriticalConstraints { get; }

    public bool RequiresCompleteOutput { get; }

    public RubricOutcome? RequiredRubric { get; }
}

public sealed class StandardEvaluationAttempt
{
    public StandardEvaluationAttempt(
        IEnumerable<NumericMeasurement> measurements,
        IEnumerable<CriticalConstraintCheck> criticalConstraintChecks,
        bool outputComplete,
        RubricOutcome? rubricOutcome)
    {
        ArgumentNullException.ThrowIfNull(measurements);
        ArgumentNullException.ThrowIfNull(criticalConstraintChecks);

        Measurements = measurements.ToArray();
        CriticalConstraintChecks = criticalConstraintChecks.ToArray();
        OutputComplete = outputComplete;
        RubricOutcome = rubricOutcome;
    }

    public IReadOnlyList<NumericMeasurement> Measurements { get; }

    public IReadOnlyList<CriticalConstraintCheck> CriticalConstraintChecks { get; }

    public bool OutputComplete { get; }

    public RubricOutcome? RubricOutcome { get; }
}

public sealed record StandardEvaluationFailure(
    StandardFailureKind Kind,
    string Detail);

public sealed record StandardEvaluationResult(
    bool Passed,
    IReadOnlyList<StandardEvaluationFailure> Failures);

public static class StandardEvaluator
{
    public static StandardEvaluationResult Evaluate(
        EvaluatedStandard standard,
        StandardEvaluationAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(standard);
        ArgumentNullException.ThrowIfNull(attempt);

        var failures = new List<StandardEvaluationFailure>();

        failures.AddRange(EvaluateCriticalConstraints(standard, attempt));
        if (standard.RequiresCompleteOutput && !attempt.OutputComplete)
        {
            failures.Add(new StandardEvaluationFailure(
                StandardFailureKind.OutputIncomplete,
                "Required output artifact is incomplete."));
        }

        failures.AddRange(EvaluateNumericThresholds(standard, attempt));
        if (standard.RequiredRubric is not null &&
            !RubricMeetsRequirement(attempt.RubricOutcome, standard.RequiredRubric.Value))
        {
            failures.Add(new StandardEvaluationFailure(
                StandardFailureKind.RubricDidNotPass,
                "Rubric outcome did not meet the required rating."));
        }

        return new StandardEvaluationResult(failures.Count == 0, failures);
    }

    private static IEnumerable<StandardEvaluationFailure> EvaluateCriticalConstraints(
        EvaluatedStandard standard,
        StandardEvaluationAttempt attempt)
    {
        var checks = attempt.CriticalConstraintChecks.ToDictionary(check => check.Id);

        foreach (var constraint in standard.CriticalConstraints)
        {
            if (!checks.TryGetValue(constraint.Id, out var check) || !check.Satisfied)
            {
                yield return new StandardEvaluationFailure(
                    StandardFailureKind.CriticalConstraintBroken,
                    constraint.Description);
            }
        }
    }

    private static IEnumerable<StandardEvaluationFailure> EvaluateNumericThresholds(
        EvaluatedStandard standard,
        StandardEvaluationAttempt attempt)
    {
        var measurements = attempt.Measurements.ToDictionary(measurement => measurement.Name);

        foreach (var threshold in standard.NumericThresholds)
        {
            if (!measurements.TryGetValue(threshold.MeasurementName, out var measurement) ||
                !MeasurementMeetsThreshold(measurement.Value, threshold))
            {
                yield return new StandardEvaluationFailure(
                    StandardFailureKind.NumericalThresholdMissed,
                    threshold.MeasurementName);
            }
        }
    }

    private static bool MeasurementMeetsThreshold(decimal measurement, NumericThreshold threshold)
    {
        return threshold.Direction switch
        {
            NumericThresholdDirection.AtLeast => measurement >= threshold.Value,
            NumericThresholdDirection.AtMost => measurement <= threshold.Value,
            _ => false,
        };
    }

    private static bool RubricMeetsRequirement(RubricOutcome? actual, RubricOutcome required)
    {
        return actual is not null && actual.Value >= required;
    }
}
