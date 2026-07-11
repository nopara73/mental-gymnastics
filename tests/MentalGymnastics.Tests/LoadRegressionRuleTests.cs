using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class LoadRegressionRuleTests
{
    [Fact]
    public void ExposesDocumentedBranchSpecificLoadRules()
    {
        Assert.Equal(
            Enum.GetValues<BranchCode>(),
            LoadRegressionRuleCatalog.BranchLoadRules.Select(rule => rule.Branch));

        Assert.Equal(
            [
                LoadVariableKind.Duration,
                LoadVariableKind.DistractorSalience,
                LoadVariableKind.RecoveryWindow,
                LoadVariableKind.TargetSubtlety,
            ],
            LoadRuleFor(BranchCode.FH).PrimaryLoadVariables);
        Assert.Equal(
            ForbiddenLoadIncreaseKind.AddComplexReasoningBeforeHoldStable,
            LoadRuleFor(BranchCode.FH).ForbiddenLoadIncrease.Kind);

        Assert.Equal(
            [
                LoadVariableKind.SwitchCount,
                LoadVariableKind.CueDensity,
                LoadVariableKind.RuleContrast,
                LoadVariableKind.ReturnPrecision,
            ],
            LoadRuleFor(BranchCode.FS).PrimaryLoadVariables);
        Assert.Equal(
            ForbiddenLoadIncreaseKind.RandomSwitchingWithoutDefinedCueRule,
            LoadRuleFor(BranchCode.FS).ForbiddenLoadIncrease.Kind);

        Assert.Equal(
            [
                LoadVariableKind.ItemCount,
                LoadVariableKind.DetailDensity,
                LoadVariableKind.OperationSteps,
                LoadVariableKind.Delay,
                LoadVariableKind.Interference,
                LoadVariableKind.TaskLength,
            ],
            LoadRuleFor(BranchCode.WM).PrimaryLoadVariables);
        Assert.Equal(
            ForbiddenLoadIncreaseKind.AddAbstractionWhenEncodingInaccurate,
            LoadRuleFor(BranchCode.WM).ForbiddenLoadIncrease.Kind);

        Assert.Equal(
            [
                LoadVariableKind.CueConflict,
                LoadVariableKind.ResponseSpeed,
                LoadVariableKind.ExceptionCount,
                LoadVariableKind.Pressure,
                LoadVariableKind.TaskLength,
            ],
            LoadRuleFor(BranchCode.IR).PrimaryLoadVariables);
        Assert.Equal(
            ForbiddenLoadIncreaseKind.IncreaseSpeedAfterRuleBreakingErrors,
            LoadRuleFor(BranchCode.IR).ForbiddenLoadIncrease.Kind);

        Assert.Equal(
            [
                LoadVariableKind.Similarity,
                LoadVariableKind.Quantity,
                LoadVariableKind.ErrorSubtlety,
                LoadVariableKind.AuditDelay,
                LoadVariableKind.TaskLength,
            ],
            LoadRuleFor(BranchCode.DE).PrimaryLoadVariables);
        Assert.Equal(
            ForbiddenLoadIncreaseKind.IncreaseQuantityWhenFalsePositivesHigh,
            LoadRuleFor(BranchCode.DE).ForbiddenLoadIncrease.Kind);

        Assert.Equal(
            [
                LoadVariableKind.RuleAmbiguity,
                LoadVariableKind.ExampleCount,
                LoadVariableKind.ExceptionHandling,
                LoadVariableKind.TransferDistance,
                LoadVariableKind.TaskLength,
            ],
            LoadRuleFor(BranchCode.CO).PrimaryLoadVariables);
        Assert.Equal(
            ForbiddenLoadIncreaseKind.IncreaseAmbiguityBeforeTestableRule,
            LoadRuleFor(BranchCode.CO).ForbiddenLoadIncrease.Kind);

        Assert.Equal(
            [
                LoadVariableKind.TimePressure,
                LoadVariableKind.EvaluativePressure,
                LoadVariableKind.Frustration,
                LoadVariableKind.Uncertainty,
                LoadVariableKind.BranchCount,
            ],
            LoadRuleFor(BranchCode.AI).PrimaryLoadVariables);
        Assert.Equal(
            ForbiddenLoadIncreaseKind.EmotionalPressurePreventsCleanEvidenceCollection,
            LoadRuleFor(BranchCode.AI).ForbiddenLoadIncrease.Kind);

        Assert.Equal(
            [
                LoadVariableKind.BranchCount,
                LoadVariableKind.TaskLength,
                LoadVariableKind.DomainDistance,
                LoadVariableKind.Interference,
                LoadVariableKind.Delay,
            ],
            LoadRuleFor(BranchCode.TI).PrimaryLoadVariables);
        Assert.Equal(
            ForbiddenLoadIncreaseKind.NovelTaskFormatWithoutBranchSpecificScoring,
            LoadRuleFor(BranchCode.TI).ForbiddenLoadIncrease.Kind);
    }

    [Fact]
    public void AllowsSinglePrimaryLoadVariableIncreaseDuringAcquisition()
    {
        var result = LoadChangeEvaluator.Evaluate(
            new LoadChangeRequest(
                BranchCode.FH,
                [LoadVariableKind.Duration],
                [],
                LoadChangeMode.Acquisition,
                increasedVariablesStableSeparately: false));

        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void RejectsLoadIncreaseOutsideTheBranchPrimaryVariables()
    {
        var result = LoadChangeEvaluator.Evaluate(
            new LoadChangeRequest(
                BranchCode.FH,
                [LoadVariableKind.RuleAmbiguity],
                [],
                LoadChangeMode.Acquisition,
                increasedVariablesStableSeparately: false));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Failures,
            failure => failure.Kind == LoadChangeFailureKind.LoadVariableNotPrimaryForBranch &&
                failure.LoadVariable == LoadVariableKind.RuleAmbiguity);
    }

    [Fact]
    public void RejectsForbiddenLoadIncreaseCondition()
    {
        var result = LoadChangeEvaluator.Evaluate(
            new LoadChangeRequest(
                BranchCode.IR,
                [LoadVariableKind.ResponseSpeed],
                [ForbiddenLoadIncreaseKind.IncreaseSpeedAfterRuleBreakingErrors],
                LoadChangeMode.Acquisition,
                increasedVariablesStableSeparately: false));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Failures,
            failure => failure.Kind == LoadChangeFailureKind.ForbiddenLoadIncrease &&
                failure.ForbiddenLoadIncrease == ForbiddenLoadIncreaseKind.IncreaseSpeedAfterRuleBreakingErrors);
    }

    [Fact]
    public void RejectsMultiplePrimaryLoadVariablesDuringAcquisition()
    {
        var result = LoadChangeEvaluator.Evaluate(
            new LoadChangeRequest(
                BranchCode.FH,
                [LoadVariableKind.Duration, LoadVariableKind.DistractorSalience],
                [],
                LoadChangeMode.Acquisition,
                increasedVariablesStableSeparately: false));

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Kind == LoadChangeFailureKind.TooManyLoadVariablesForAcquisition);
    }

    [Fact]
    public void ExposesAllowedAndForbiddenRegressionRules()
    {
        Assert.Equal(
            [
                RegressionMoveKind.ShorterDuration,
                RegressionMoveKind.FewerItems,
                RegressionMoveKind.LowerCueDensity,
                RegressionMoveKind.LongerRest,
                RegressionMoveKind.ClearerExamples,
                RegressionMoveKind.LessSubtleErrors,
                RegressionMoveKind.ShorterDelay,
                RegressionMoveKind.MilderPressure,
                RegressionMoveKind.FewerBranchesInCompositeTask,
            ],
            LoadRegressionRuleCatalog.AllowedRegressionMoves);

        Assert.Equal(
            [
                ForbiddenRegressionMoveKind.RemoveDriftMarkingFromFocusHold,
                ForbiddenRegressionMoveKind.AllowTargetChangesDuringFocusHold,
                ForbiddenRegressionMoveKind.AllowUncuedSwitchesDuringFocusShift,
                ForbiddenRegressionMoveKind.AllowRereadingAfterEncodeWindowInWorkingMemory,
                ForbiddenRegressionMoveKind.AllowPrematureResponsesInInhibition,
                ForbiddenRegressionMoveKind.AllowUnmarkedGuessesInDiscrimination,
                ForbiddenRegressionMoveKind.AllowVagueRulesInConceptOperations,
                ForbiddenRegressionMoveKind.LowerOriginalBranchStandardDuringAffectiveInterference,
                ForbiddenRegressionMoveKind.RemoveBranchSpecificEvidenceDuringTransferIntegration,
            ],
            LoadRegressionRuleCatalog.ForbiddenRegressionRules.Select(rule => rule.Move));
    }

    [Fact]
    public void AllowsRegressionThatPreservesCoreDemandAndHonestyConstraint()
    {
        var result = RegressionRuleEvaluator.Evaluate(
            new RegressionRuleRequest(
                BranchCode.FH,
                RegressionMoveKind.ShorterDuration,
                [],
                preservesCoreDemand: true,
                preservesHonestyConstraint: true));

        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void RejectsForbiddenRegressionAndHonestyConstraintRemoval()
    {
        var result = RegressionRuleEvaluator.Evaluate(
            new RegressionRuleRequest(
                BranchCode.FH,
                RegressionMoveKind.ShorterDuration,
                [ForbiddenRegressionMoveKind.RemoveDriftMarkingFromFocusHold],
                preservesCoreDemand: true,
                preservesHonestyConstraint: false));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Failures,
            failure => failure.Kind == RegressionRuleFailureKind.ForbiddenRegressionMove &&
                failure.ForbiddenMove == ForbiddenRegressionMoveKind.RemoveDriftMarkingFromFocusHold);
        Assert.Contains(result.Failures, failure => failure.Kind == RegressionRuleFailureKind.HonestyConstraintRemoved);
    }

    [Fact]
    public void RejectsRegressionThatDoesNotPreserveCoreDemand()
    {
        var result = RegressionRuleEvaluator.Evaluate(
            new RegressionRuleRequest(
                BranchCode.TI,
                RegressionMoveKind.FewerBranchesInCompositeTask,
                [],
                preservesCoreDemand: false,
                preservesHonestyConstraint: true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Kind == RegressionRuleFailureKind.CoreDemandNotPreserved);
    }

    private static BranchLoadRuleDefinition LoadRuleFor(BranchCode branch)
    {
        return LoadRegressionRuleCatalog.BranchLoadRules.Single(rule => rule.Branch == branch);
    }
}
