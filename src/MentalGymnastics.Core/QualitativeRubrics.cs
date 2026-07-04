namespace MentalGymnastics.Core;

public sealed record QualitativeRubricOutcomeDefinition
{
    public QualitativeRubricOutcomeDefinition(RubricOutcome outcome, string observableMeaning)
    {
        if (string.IsNullOrWhiteSpace(observableMeaning))
        {
            throw new ArgumentException("Observable meaning is required.", nameof(observableMeaning));
        }

        Outcome = outcome;
        ObservableMeaning = observableMeaning;
    }

    public RubricOutcome Outcome { get; }

    public string ObservableMeaning { get; }

    public bool MeetsPassingStandard => Outcome is RubricOutcome.Pass or RubricOutcome.Excellent;
}

public sealed class QualitativeRubricDefinition
{
    private static readonly RubricOutcome[] RequiredOutcomes =
    [
        RubricOutcome.Fail,
        RubricOutcome.Pass,
        RubricOutcome.Excellent,
    ];

    private readonly IReadOnlyDictionary<RubricOutcome, QualitativeRubricOutcomeDefinition> _outcomesByRating;

    public QualitativeRubricDefinition(
        QualitativeRubricKind kind,
        string name,
        IEnumerable<QualitativeRubricOutcomeDefinition> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Rubric name is required.", nameof(name));
        }

        var outcomeArray = outcomes.ToArray();
        if (!outcomeArray.Select(outcome => outcome.Outcome).SequenceEqual(RequiredOutcomes))
        {
            throw new ArgumentException(
                "Qualitative rubrics must define fail, pass, and excellent outcomes in order.",
                nameof(outcomes));
        }

        Kind = kind;
        Name = name;
        Outcomes = outcomeArray;
        _outcomesByRating = outcomeArray.ToDictionary(outcome => outcome.Outcome);
    }

    public QualitativeRubricKind Kind { get; }

    public string Name { get; }

    public IReadOnlyList<QualitativeRubricOutcomeDefinition> Outcomes { get; }

    public QualitativeRubricOutcomeDefinition GetOutcome(RubricOutcome outcome)
    {
        if (!_outcomesByRating.TryGetValue(outcome, out var definition))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown rubric outcome.");
        }

        return definition;
    }

    public bool MeetsPassingStandard(RubricOutcome outcome)
    {
        return GetOutcome(outcome).MeetsPassingStandard;
    }
}

public sealed record QualitativeRubricAssessment(
    QualitativeRubricKind RubricKind,
    RubricOutcome Outcome)
{
    public QualitativeRubricDefinition Rubric => QualitativeRubricCatalog.Get(RubricKind);

    public QualitativeRubricOutcomeDefinition OutcomeDefinition => Rubric.GetOutcome(Outcome);

    public string ObservableMeaning => OutcomeDefinition.ObservableMeaning;

    public bool MeetsPassingStandard => OutcomeDefinition.MeetsPassingStandard;
}

public static class QualitativeRubricCatalog
{
    public static QualitativeRubricDefinition RuleQuality { get; } = new(
        QualitativeRubricKind.RuleQuality,
        "Rule Quality",
        [
            new QualitativeRubricOutcomeDefinition(
                RubricOutcome.Fail,
                "Rule is circular, motivational, unfalsifiable, or changed after feedback."),
            new QualitativeRubricOutcomeDefinition(
                RubricOutcome.Pass,
                "Rule is stated before test, predicts unseen cases, and handles defined exceptions."),
            new QualitativeRubricOutcomeDefinition(
                RubricOutcome.Excellent,
                "Rule is concise, predicts unseen cases, names limits, and survives audit."),
        ]);

    public static QualitativeRubricDefinition MappingQuality { get; } = new(
        QualitativeRubricKind.MappingQuality,
        "Mapping Quality",
        [
            new QualitativeRubricOutcomeDefinition(
                RubricOutcome.Fail,
                "Mapping relies on surface similarity or omits required relations."),
            new QualitativeRubricOutcomeDefinition(
                RubricOutcome.Pass,
                "Required relations are preserved and irrelevant similarities are ignored."),
            new QualitativeRubricOutcomeDefinition(
                RubricOutcome.Excellent,
                "Mapping preserves required relations and explains where the mapping stops."),
        ]);

    public static QualitativeRubricDefinition OpenTaskAuditQuality { get; } = new(
        QualitativeRubricKind.OpenTaskAuditQuality,
        "Open-Task Audit Quality",
        [
            new QualitativeRubricOutcomeDefinition(
                RubricOutcome.Fail,
                "Critical errors missed, uncertainty hidden, or corrections made without evidence."),
            new QualitativeRubricOutcomeDefinition(
                RubricOutcome.Pass,
                "Critical errors found, uncertainty marked, and corrections justified."),
            new QualitativeRubricOutcomeDefinition(
                RubricOutcome.Excellent,
                "Critical errors found, noncritical errors mostly found, uncertainty well bounded."),
        ]);

    public static IReadOnlyList<QualitativeRubricDefinition> All { get; } =
    [
        RuleQuality,
        MappingQuality,
        OpenTaskAuditQuality,
    ];

    public static QualitativeRubricDefinition Get(QualitativeRubricKind kind)
    {
        var rubric = All.SingleOrDefault(rubric => rubric.Kind == kind);
        if (rubric is null)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown qualitative rubric kind.");
        }

        return rubric;
    }
}
