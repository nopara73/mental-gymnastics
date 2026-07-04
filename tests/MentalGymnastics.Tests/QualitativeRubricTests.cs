using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class QualitativeRubricTests
{
    [Fact]
    public void ExposesDocumentedQualitativeRubrics()
    {
        Assert.Equal(
            new[]
            {
                (QualitativeRubricKind.RuleQuality, "Rule Quality"),
                (QualitativeRubricKind.MappingQuality, "Mapping Quality"),
                (QualitativeRubricKind.OpenTaskAuditQuality, "Open-Task Audit Quality"),
            },
            QualitativeRubricCatalog.All.Select(rubric => (rubric.Kind, rubric.Name)));
    }

    [Theory]
    [InlineData(QualitativeRubricKind.RuleQuality)]
    [InlineData(QualitativeRubricKind.MappingQuality)]
    [InlineData(QualitativeRubricKind.OpenTaskAuditQuality)]
    public void EachRubricHasFailPassAndExcellentOutcomes(QualitativeRubricKind kind)
    {
        var rubric = QualitativeRubricCatalog.Get(kind);

        Assert.Equal(
            new[] { RubricOutcome.Fail, RubricOutcome.Pass, RubricOutcome.Excellent },
            rubric.Outcomes.Select(outcome => outcome.Outcome));
    }

    [Fact]
    public void RuleQualityOutcomesMatchProgramMeanings()
    {
        var rubric = QualitativeRubricCatalog.RuleQuality;

        Assert.Equal(
            "Rule is circular, motivational, unfalsifiable, or changed after feedback.",
            rubric.GetOutcome(RubricOutcome.Fail).ObservableMeaning);
        Assert.Equal(
            "Rule is stated before test, predicts unseen cases, and handles defined exceptions.",
            rubric.GetOutcome(RubricOutcome.Pass).ObservableMeaning);
        Assert.Equal(
            "Rule is concise, predicts unseen cases, names limits, and survives audit.",
            rubric.GetOutcome(RubricOutcome.Excellent).ObservableMeaning);
    }

    [Fact]
    public void MappingQualityOutcomesMatchProgramMeanings()
    {
        var rubric = QualitativeRubricCatalog.MappingQuality;

        Assert.Equal(
            "Mapping relies on surface similarity or omits required relations.",
            rubric.GetOutcome(RubricOutcome.Fail).ObservableMeaning);
        Assert.Equal(
            "Required relations are preserved and irrelevant similarities are ignored.",
            rubric.GetOutcome(RubricOutcome.Pass).ObservableMeaning);
        Assert.Equal(
            "Mapping preserves required relations and explains where the mapping stops.",
            rubric.GetOutcome(RubricOutcome.Excellent).ObservableMeaning);
    }

    [Fact]
    public void OpenTaskAuditQualityOutcomesMatchProgramMeanings()
    {
        var rubric = QualitativeRubricCatalog.OpenTaskAuditQuality;

        Assert.Equal(
            "Critical errors missed, uncertainty hidden, or corrections made without evidence.",
            rubric.GetOutcome(RubricOutcome.Fail).ObservableMeaning);
        Assert.Equal(
            "Critical errors found, uncertainty marked, and corrections justified.",
            rubric.GetOutcome(RubricOutcome.Pass).ObservableMeaning);
        Assert.Equal(
            "Critical errors found, noncritical errors mostly found, uncertainty well bounded.",
            rubric.GetOutcome(RubricOutcome.Excellent).ObservableMeaning);
    }

    [Theory]
    [InlineData(QualitativeRubricKind.RuleQuality, RubricOutcome.Fail, false)]
    [InlineData(QualitativeRubricKind.RuleQuality, RubricOutcome.Pass, true)]
    [InlineData(QualitativeRubricKind.RuleQuality, RubricOutcome.Excellent, true)]
    [InlineData(QualitativeRubricKind.MappingQuality, RubricOutcome.Fail, false)]
    [InlineData(QualitativeRubricKind.MappingQuality, RubricOutcome.Pass, true)]
    [InlineData(QualitativeRubricKind.MappingQuality, RubricOutcome.Excellent, true)]
    [InlineData(QualitativeRubricKind.OpenTaskAuditQuality, RubricOutcome.Fail, false)]
    [InlineData(QualitativeRubricKind.OpenTaskAuditQuality, RubricOutcome.Pass, true)]
    [InlineData(QualitativeRubricKind.OpenTaskAuditQuality, RubricOutcome.Excellent, true)]
    public void RubricAssessmentIsUsableByLaterGateLogic(
        QualitativeRubricKind kind,
        RubricOutcome outcome,
        bool expectedPassingStandard)
    {
        var assessment = new QualitativeRubricAssessment(
            kind,
            outcome);

        Assert.Equal(expectedPassingStandard, assessment.MeetsPassingStandard);
    }

    [Fact]
    public void RubricAssessmentUsesCatalogMeaningInsteadOfFreeFormEncouragement()
    {
        var assessment = new QualitativeRubricAssessment(
            QualitativeRubricKind.OpenTaskAuditQuality,
            RubricOutcome.Pass);

        Assert.Equal(
            "Critical errors found, uncertainty marked, and corrections justified.",
            assessment.ObservableMeaning);
    }
}
