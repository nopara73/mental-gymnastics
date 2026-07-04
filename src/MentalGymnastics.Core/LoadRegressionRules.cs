namespace MentalGymnastics.Core;

public sealed record ForbiddenLoadIncrease(
    ForbiddenLoadIncreaseKind Kind,
    string Description);

public sealed record BranchLoadRuleDefinition(
    BranchCode Branch,
    IReadOnlyList<LoadVariableKind> PrimaryLoadVariables,
    ForbiddenLoadIncrease ForbiddenLoadIncrease);

public sealed class LoadChangeRequest
{
    public LoadChangeRequest(
        BranchCode branch,
        IEnumerable<LoadVariableKind> increasedVariables,
        IEnumerable<ForbiddenLoadIncreaseKind> presentForbiddenLoadIncreases,
        LoadChangeMode mode,
        bool increasedVariablesStableSeparately)
    {
        ArgumentNullException.ThrowIfNull(increasedVariables);
        ArgumentNullException.ThrowIfNull(presentForbiddenLoadIncreases);

        Branch = branch;
        IncreasedVariables = increasedVariables.ToArray();
        PresentForbiddenLoadIncreases = presentForbiddenLoadIncreases.ToArray();
        Mode = mode;
        IncreasedVariablesStableSeparately = increasedVariablesStableSeparately;
    }

    public BranchCode Branch { get; }

    public IReadOnlyList<LoadVariableKind> IncreasedVariables { get; }

    public IReadOnlyList<ForbiddenLoadIncreaseKind> PresentForbiddenLoadIncreases { get; }

    public LoadChangeMode Mode { get; }

    public bool IncreasedVariablesStableSeparately { get; }
}

public sealed record LoadChangeFailure(
    LoadChangeFailureKind Kind,
    LoadVariableKind? LoadVariable,
    ForbiddenLoadIncreaseKind? ForbiddenLoadIncrease,
    string Detail);

public sealed record LoadChangeResult(
    bool IsValid,
    IReadOnlyList<LoadChangeFailure> Failures);

public sealed record ForbiddenRegressionRule(
    BranchCode Branch,
    ForbiddenRegressionMoveKind Move,
    string Description);

public sealed class RegressionRuleRequest
{
    public RegressionRuleRequest(
        BranchCode branch,
        RegressionMoveKind move,
        IEnumerable<ForbiddenRegressionMoveKind> forbiddenMovesPresent,
        bool preservesCoreDemand,
        bool preservesHonestyConstraint)
    {
        ArgumentNullException.ThrowIfNull(forbiddenMovesPresent);

        Branch = branch;
        Move = move;
        ForbiddenMovesPresent = forbiddenMovesPresent.ToArray();
        PreservesCoreDemand = preservesCoreDemand;
        PreservesHonestyConstraint = preservesHonestyConstraint;
    }

    public BranchCode Branch { get; }

    public RegressionMoveKind Move { get; }

    public IReadOnlyList<ForbiddenRegressionMoveKind> ForbiddenMovesPresent { get; }

    public bool PreservesCoreDemand { get; }

    public bool PreservesHonestyConstraint { get; }
}

public sealed record RegressionRuleFailure(
    RegressionRuleFailureKind Kind,
    ForbiddenRegressionMoveKind? ForbiddenMove,
    string Detail);

public sealed record RegressionRuleResult(
    bool IsValid,
    IReadOnlyList<RegressionRuleFailure> Failures);

public static class LoadRegressionRuleCatalog
{
    public static IReadOnlyList<BranchLoadRuleDefinition> BranchLoadRules { get; } =
    [
        LoadRule(
            BranchCode.FH,
            [
                LoadVariableKind.Duration,
                LoadVariableKind.DistractorSalience,
                LoadVariableKind.RecoveryWindow,
                LoadVariableKind.TargetSubtlety,
            ],
            ForbiddenLoadIncreaseKind.AddComplexReasoningBeforeHoldStable,
            "Adding complex reasoning before hold is stable."),
        LoadRule(
            BranchCode.FS,
            [
                LoadVariableKind.SwitchCount,
                LoadVariableKind.CueDensity,
                LoadVariableKind.RuleContrast,
                LoadVariableKind.ReturnPrecision,
            ],
            ForbiddenLoadIncreaseKind.RandomSwitchingWithoutDefinedCueRule,
            "Random switching without a defined cue rule."),
        LoadRule(
            BranchCode.WM,
            [
                LoadVariableKind.ItemCount,
                LoadVariableKind.DetailDensity,
                LoadVariableKind.OperationSteps,
                LoadVariableKind.Delay,
                LoadVariableKind.Interference,
            ],
            ForbiddenLoadIncreaseKind.AddAbstractionWhenEncodingInaccurate,
            "Adding abstraction when encoding is inaccurate."),
        LoadRule(
            BranchCode.IR,
            [
                LoadVariableKind.CueConflict,
                LoadVariableKind.ResponseSpeed,
                LoadVariableKind.ExceptionCount,
                LoadVariableKind.Pressure,
            ],
            ForbiddenLoadIncreaseKind.IncreaseSpeedAfterRuleBreakingErrors,
            "Increasing speed after rule-breaking errors."),
        LoadRule(
            BranchCode.DE,
            [
                LoadVariableKind.Similarity,
                LoadVariableKind.Quantity,
                LoadVariableKind.ErrorSubtlety,
                LoadVariableKind.AuditDelay,
            ],
            ForbiddenLoadIncreaseKind.IncreaseQuantityWhenFalsePositivesHigh,
            "Increasing quantity when false positives are high."),
        LoadRule(
            BranchCode.CO,
            [
                LoadVariableKind.RuleAmbiguity,
                LoadVariableKind.ExampleCount,
                LoadVariableKind.ExceptionHandling,
                LoadVariableKind.TransferDistance,
            ],
            ForbiddenLoadIncreaseKind.IncreaseAmbiguityBeforeTestableRule,
            "Ambiguity before the practitioner can state a testable rule."),
        LoadRule(
            BranchCode.AI,
            [
                LoadVariableKind.TimePressure,
                LoadVariableKind.EvaluativePressure,
                LoadVariableKind.Frustration,
                LoadVariableKind.Uncertainty,
            ],
            ForbiddenLoadIncreaseKind.EmotionalPressurePreventsCleanEvidenceCollection,
            "Emotional pressure that prevents clean evidence collection."),
        LoadRule(
            BranchCode.TI,
            [
                LoadVariableKind.BranchCount,
                LoadVariableKind.TaskLength,
                LoadVariableKind.DomainDistance,
                LoadVariableKind.Interference,
            ],
            ForbiddenLoadIncreaseKind.NovelTaskFormatWithoutBranchSpecificScoring,
            "Novel task format without branch-specific scoring."),
    ];

    public static IReadOnlyList<RegressionMoveKind> AllowedRegressionMoves { get; } =
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
    ];

    public static IReadOnlyList<ForbiddenRegressionRule> ForbiddenRegressionRules { get; } =
    [
        ForbiddenRegression(
            BranchCode.FH,
            ForbiddenRegressionMoveKind.RemoveDriftMarkingFromFocusHold,
            "Removing drift marking from FH."),
        ForbiddenRegression(
            BranchCode.FH,
            ForbiddenRegressionMoveKind.AllowTargetChangesDuringFocusHold,
            "Allowing target changes during FH."),
        ForbiddenRegression(
            BranchCode.FS,
            ForbiddenRegressionMoveKind.AllowUncuedSwitchesDuringFocusShift,
            "Allowing uncued switches during FS."),
        ForbiddenRegression(
            BranchCode.WM,
            ForbiddenRegressionMoveKind.AllowRereadingAfterEncodeWindowInWorkingMemory,
            "Allowing rereading during WM after the encode window."),
        ForbiddenRegression(
            BranchCode.IR,
            ForbiddenRegressionMoveKind.AllowPrematureResponsesInInhibition,
            "Allowing premature responses in IR."),
        ForbiddenRegression(
            BranchCode.DE,
            ForbiddenRegressionMoveKind.AllowUnmarkedGuessesInDiscrimination,
            "Allowing unmarked guesses in DE."),
        ForbiddenRegression(
            BranchCode.CO,
            ForbiddenRegressionMoveKind.AllowVagueRulesInConceptOperations,
            "Allowing vague rules in CO."),
        ForbiddenRegression(
            BranchCode.AI,
            ForbiddenRegressionMoveKind.LowerOriginalBranchStandardDuringAffectiveInterference,
            "Lowering the original branch standard during AI."),
        ForbiddenRegression(
            BranchCode.TI,
            ForbiddenRegressionMoveKind.RemoveBranchSpecificEvidenceDuringTransferIntegration,
            "Removing branch-specific evidence during TI."),
    ];

    private static BranchLoadRuleDefinition LoadRule(
        BranchCode branch,
        IReadOnlyList<LoadVariableKind> primaryLoadVariables,
        ForbiddenLoadIncreaseKind forbiddenLoadIncrease,
        string description)
    {
        return new BranchLoadRuleDefinition(
            branch,
            primaryLoadVariables,
            new ForbiddenLoadIncrease(forbiddenLoadIncrease, description));
    }

    private static ForbiddenRegressionRule ForbiddenRegression(
        BranchCode branch,
        ForbiddenRegressionMoveKind move,
        string description)
    {
        return new ForbiddenRegressionRule(branch, move, description);
    }
}

public static class LoadChangeEvaluator
{
    public static LoadChangeResult Evaluate(LoadChangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var failures = new List<LoadChangeFailure>();
        var rule = LoadRegressionRuleCatalog.BranchLoadRules.Single(item => item.Branch == request.Branch);

        foreach (var loadVariable in request.IncreasedVariables.Distinct())
        {
            if (!rule.PrimaryLoadVariables.Contains(loadVariable))
            {
                failures.Add(new LoadChangeFailure(
                    LoadChangeFailureKind.LoadVariableNotPrimaryForBranch,
                    loadVariable,
                    ForbiddenLoadIncrease: null,
                    $"{loadVariable} is not a primary load variable for {request.Branch}."));
            }
        }

        if (request.Mode == LoadChangeMode.Acquisition && request.IncreasedVariables.Distinct().Count() > 1)
        {
            failures.Add(new LoadChangeFailure(
                LoadChangeFailureKind.TooManyLoadVariablesForAcquisition,
                LoadVariable: null,
                ForbiddenLoadIncrease: null,
                "Acquisition may increase only one primary load variable at a time."));
        }

        if (request.Mode == LoadChangeMode.AdvancedIntegration &&
            request.IncreasedVariables.Distinct().Count() > 1 &&
            !request.IncreasedVariablesStableSeparately)
        {
            failures.Add(new LoadChangeFailure(
                LoadChangeFailureKind.AdvancedIntegrationVariablesNotStable,
                LoadVariable: null,
                ForbiddenLoadIncrease: null,
                "Advanced integration may combine load increases only when both are already stable separately."));
        }

        if (request.PresentForbiddenLoadIncreases.Contains(rule.ForbiddenLoadIncrease.Kind))
        {
            failures.Add(new LoadChangeFailure(
                LoadChangeFailureKind.ForbiddenLoadIncrease,
                LoadVariable: null,
                rule.ForbiddenLoadIncrease.Kind,
                rule.ForbiddenLoadIncrease.Description));
        }

        return new LoadChangeResult(failures.Count == 0, failures);
    }
}

public static class RegressionRuleEvaluator
{
    public static RegressionRuleResult Evaluate(RegressionRuleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var failures = new List<RegressionRuleFailure>();
        if (!LoadRegressionRuleCatalog.AllowedRegressionMoves.Contains(request.Move))
        {
            failures.Add(new RegressionRuleFailure(
                RegressionRuleFailureKind.RegressionMoveNotAllowed,
                ForbiddenMove: null,
                $"{request.Move} is not a documented regression move."));
        }

        var forbiddenForBranch = LoadRegressionRuleCatalog.ForbiddenRegressionRules
            .Where(rule => rule.Branch == request.Branch)
            .ToArray();

        foreach (var forbiddenMove in request.ForbiddenMovesPresent.Distinct())
        {
            var matchingForbiddenRule = forbiddenForBranch.SingleOrDefault(rule => rule.Move == forbiddenMove);
            if (matchingForbiddenRule is not null)
            {
                failures.Add(new RegressionRuleFailure(
                    RegressionRuleFailureKind.ForbiddenRegressionMove,
                    forbiddenMove,
                    matchingForbiddenRule.Description));
            }
        }

        if (!request.PreservesCoreDemand)
        {
            failures.Add(new RegressionRuleFailure(
                RegressionRuleFailureKind.CoreDemandNotPreserved,
                ForbiddenMove: null,
                "A regression must preserve the same core demand."));
        }

        if (!request.PreservesHonestyConstraint)
        {
            failures.Add(new RegressionRuleFailure(
                RegressionRuleFailureKind.HonestyConstraintRemoved,
                ForbiddenMove: null,
                "A regression must not remove the honesty constraint."));
        }

        return new RegressionRuleResult(failures.Count == 0, failures);
    }
}
