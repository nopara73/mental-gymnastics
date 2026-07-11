using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class ExecutableStandardCatalogTests
{
    [Fact]
    public void DefinesOneExecutableStandardForEveryBranchLevel()
    {
        Assert.Equal(40, ExecutableStandardCatalog.Standards.Count);
        Assert.Equal(
            40,
            ExecutableStandardCatalog.Standards
                .Select(standard => (standard.Branch, standard.Level))
                .Distinct()
                .Count());

        foreach (var branch in ProgramCatalog.Branches)
        {
            foreach (var level in ProgramCatalog.GlobalLevels)
            {
                var executable = ExecutableStandardCatalog.Get(branch.Code, level.Id);
                var catalog = ProgramCatalog.Standards.Single(standard =>
                    standard.Branch == branch.Code && standard.Level == level.Id);

                Assert.Contains(catalog.Standard, executable.EvaluatedStandard.Name, StringComparison.Ordinal);
                Assert.NotEmpty(executable.EvaluatedStandard.NumericThresholds);
                Assert.NotEmpty(executable.EvaluatedStandard.CriticalConstraints);
            }
        }
    }

    [Fact]
    public void UsesTheDocumentedPrimaryProtocolAtEachDemandLevel()
    {
        Assert.Equal(DrillId.FH1TargetHold, ExecutableStandardCatalog.Get(BranchCode.FH, GlobalLevelId.L2).Drill);
        Assert.Equal(DrillId.FS2InvalidCueFilter, ExecutableStandardCatalog.Get(BranchCode.FS, GlobalLevelId.L3).Drill);
        Assert.Equal(DrillId.WM2MentalTransform, ExecutableStandardCatalog.Get(BranchCode.WM, GlobalLevelId.L4).Drill);
        Assert.Equal(DrillId.IR2ExceptionRule, ExecutableStandardCatalog.Get(BranchCode.IR, GlobalLevelId.L2).Drill);
        Assert.Equal(DrillId.DE2SeededAudit, ExecutableStandardCatalog.Get(BranchCode.DE, GlobalLevelId.L3).Drill);
        Assert.Equal(DrillId.CO2StructureMapping, ExecutableStandardCatalog.Get(BranchCode.CO, GlobalLevelId.L5).Drill);
        Assert.Equal(DrillId.AI2DisruptionRecovery, ExecutableStandardCatalog.Get(BranchCode.AI, GlobalLevelId.L3).Drill);
        Assert.Equal(DrillId.TI2GlobalReviewTask, ExecutableStandardCatalog.Get(BranchCode.TI, GlobalLevelId.L5).Drill);
    }

    [Fact]
    public void FocusHoldLevelOneRetainsExactBoundaryBehavior()
    {
        var standard = ExecutableStandardCatalog.Get(BranchCode.FH, GlobalLevelId.L1).EvaluatedStandard;
        var result = StandardEvaluator.Evaluate(
            standard,
            new StandardEvaluationAttempt(
                [
                    new(TrainingStandardMeasurements.ActiveDurationSeconds, 180),
                    new(TrainingStandardMeasurements.MarkedDriftCount, 5),
                    new(TrainingStandardMeasurements.UnreturnedDriftCount, 0),
                    new(TrainingStandardMeasurements.LateReturnCount, 0),
                    new(TrainingStandardMeasurements.TargetSubstitutionCount, 0),
                ],
                [
                    new(TrainingStandardConstraints.TargetStatedBeforeSet, true),
                ],
                outputComplete: true,
                rubricOutcome: null));

        Assert.True(result.Passed);
    }

    [Fact]
    public void EveryCurriculumStandardPassesAtItsExactBoundaryAndFailsEachBrokenCondition()
    {
        foreach (var definition in ExecutableStandardCatalog.Standards)
        {
            var standard = definition.EvaluatedStandard;
            var boundaryMeasurements = standard.NumericThresholds
                .Select(threshold => new NumericMeasurement(threshold.MeasurementName, threshold.Value))
                .ToArray();
            var cleanConstraints = standard.CriticalConstraints
                .Select(constraint => new CriticalConstraintCheck(constraint.Id, Satisfied: true))
                .ToArray();
            var passing = StandardEvaluator.Evaluate(
                standard,
                new StandardEvaluationAttempt(
                    boundaryMeasurements,
                    cleanConstraints,
                    outputComplete: true,
                    standard.RequiredRubric));

            Assert.True(
                passing.Passed,
                $"{definition.Branch} {definition.Level} did not pass at its documented boundary: " +
                string.Join("; ", passing.Failures.Select(failure => failure.Detail)));

            foreach (var threshold in standard.NumericThresholds)
            {
                var failingMeasurements = boundaryMeasurements
                    .Select(measurement => measurement.Name == threshold.MeasurementName
                        ? measurement with
                        {
                            Value = threshold.Direction == NumericThresholdDirection.AtLeast
                                ? threshold.Value - 1
                                : threshold.Value + 1,
                        }
                        : measurement)
                    .ToArray();
                var result = StandardEvaluator.Evaluate(
                    standard,
                    new StandardEvaluationAttempt(
                        failingMeasurements,
                        cleanConstraints,
                        outputComplete: true,
                        standard.RequiredRubric));

                Assert.False(result.Passed, $"{definition.Branch} {definition.Level} ignored {threshold.MeasurementName}.");
            }

            foreach (var constraint in standard.CriticalConstraints)
            {
                var brokenConstraints = cleanConstraints
                    .Select(check => check.Id == constraint.Id ? check with { Satisfied = false } : check)
                    .ToArray();
                var result = StandardEvaluator.Evaluate(
                    standard,
                    new StandardEvaluationAttempt(
                        boundaryMeasurements,
                        brokenConstraints,
                        outputComplete: true,
                        standard.RequiredRubric));

                Assert.False(result.Passed, $"{definition.Branch} {definition.Level} ignored {constraint.Id}.");
            }

            Assert.False(StandardEvaluator.Evaluate(
                standard,
                new StandardEvaluationAttempt(
                    boundaryMeasurements,
                    cleanConstraints,
                    outputComplete: false,
                    standard.RequiredRubric)).Passed);

            if (standard.RequiredRubric.HasValue)
            {
                Assert.False(StandardEvaluator.Evaluate(
                    standard,
                    new StandardEvaluationAttempt(
                        boundaryMeasurements,
                        cleanConstraints,
                        outputComplete: true,
                        RubricOutcome.Fail)).Passed);
            }
        }
    }
}
