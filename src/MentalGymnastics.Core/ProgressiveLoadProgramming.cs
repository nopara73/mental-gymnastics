using System.Globalization;
using System.Text.RegularExpressions;

namespace MentalGymnastics.Core;

public sealed record TrainingLoadStage(
    int Index,
    IReadOnlyList<LoadVariable> LoadVariables,
    string? IncreasedVariable);

public sealed record TrainingLoadProfile(
    BranchCode Branch,
    GlobalLevelId Level,
    DrillId Drill,
    IReadOnlyList<TrainingLoadStage> Stages)
{
    public TrainingLoadStage TargetStage => Stages[^1];
}

public sealed record TrainingLoadHistoryEntry(
    IReadOnlyList<LoadVariable> LoadVariables,
    bool Clean,
    bool Overload);

public sealed record ProgressiveLoadPrescription(
    TrainingLoadProfile Profile,
    TrainingLoadStage Stage,
    string Reason)
{
    public bool IsFormalStandardLoad => Stage.Index == Profile.TargetStage.Index;
}

public static class TrainingLoadProfileCatalog
{
    public static IReadOnlyList<TrainingLoadProfile> Profiles { get; } = Build();

    public static TrainingLoadProfile Get(BranchCode branch, GlobalLevelId level)
    {
        return Profiles.Single(profile => profile.Branch == branch && profile.Level == level);
    }

    public static LoadVariableKind KindFor(string variableName)
    {
        return variableName.Trim().ToLowerInvariant() switch
        {
            "duration" => LoadVariableKind.Duration,
            "task length" => LoadVariableKind.TaskLength,
            "distractor salience" => LoadVariableKind.DistractorSalience,
            "recovery window" => LoadVariableKind.RecoveryWindow,
            "target subtlety" => LoadVariableKind.TargetSubtlety,
            "switch count" => LoadVariableKind.SwitchCount,
            "cue density" => LoadVariableKind.CueDensity,
            "rule contrast" => LoadVariableKind.RuleContrast,
            "return precision" => LoadVariableKind.ReturnPrecision,
            "response window" => LoadVariableKind.ResponseSpeed,
            "item count" => LoadVariableKind.ItemCount,
            "detail density" => LoadVariableKind.DetailDensity,
            "operation steps" => LoadVariableKind.OperationSteps,
            "delay" => LoadVariableKind.Delay,
            "interference" => LoadVariableKind.Interference,
            "cue conflict" => LoadVariableKind.CueConflict,
            "response speed" => LoadVariableKind.ResponseSpeed,
            "exception count" => LoadVariableKind.ExceptionCount,
            "pressure" => LoadVariableKind.Pressure,
            "similarity" => LoadVariableKind.Similarity,
            "item quantity" or "quantity" => LoadVariableKind.Quantity,
            "error subtlety" => LoadVariableKind.ErrorSubtlety,
            "audit delay" => LoadVariableKind.AuditDelay,
            "rule ambiguity" => LoadVariableKind.RuleAmbiguity,
            "example count" => LoadVariableKind.ExampleCount,
            "exception handling" => LoadVariableKind.ExceptionHandling,
            "transfer distance" => LoadVariableKind.TransferDistance,
            "time pressure" => LoadVariableKind.TimePressure,
            "observation" or "evaluative pressure" => LoadVariableKind.EvaluativePressure,
            "frustration" => LoadVariableKind.Frustration,
            "uncertainty" => LoadVariableKind.Uncertainty,
            "number of branches" => LoadVariableKind.BranchCount,
            "domain distance" => LoadVariableKind.DomainDistance,
            _ => throw new ArgumentOutOfRangeException(
                nameof(variableName),
                variableName,
                "Load variable is not part of the documented progression vocabulary."),
        };
    }

    private static IReadOnlyList<TrainingLoadProfile> Build()
    {
        return ProgramCatalog.Branches
            .SelectMany(branch => ProgramCatalog.GlobalLevels.Select(level =>
                BuildProfile(branch.Code, level.Id)))
            .ToArray();
    }

    private static TrainingLoadProfile BuildProfile(BranchCode branch, GlobalLevelId level)
    {
        var executable = ExecutableStandardCatalog.Get(branch, level);
        var target = TargetLoad(branch, level).ToArray();
        var (variable, regressionValue) = RegressionStep(branch, level);
        var regression = target
            .Select(load => string.Equals(load.Name, variable, StringComparison.OrdinalIgnoreCase)
                ? new LoadVariable(load.Name, regressionValue)
                : load)
            .ToArray();

        ValidateSingleVariableProgression(branch, variable, regression, target);
        return new TrainingLoadProfile(
            branch,
            level,
            executable.Drill,
            [
                new TrainingLoadStage(0, regression, IncreasedVariable: null),
                new TrainingLoadStage(1, target, variable),
            ]);
    }

    private static IReadOnlyList<LoadVariable> TargetLoad(BranchCode branch, GlobalLevelId level)
    {
        return branch switch
        {
            BranchCode.FH => FocusHold(level),
            BranchCode.FS => FocusShift(level),
            BranchCode.WM => WorkingMemory(level),
            BranchCode.IR => Inhibition(level),
            BranchCode.DE => Discrimination(level),
            BranchCode.CO => ConceptOperations(level),
            BranchCode.AI => AffectiveInterference(level),
            BranchCode.TI => TransferIntegration(level),
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "Unknown training branch."),
        };
    }

    private static (string Variable, string RegressionValue) RegressionStep(
        BranchCode branch,
        GlobalLevelId level)
    {
        return (branch, level) switch
        {
            (BranchCode.FH, GlobalLevelId.L1) => ("duration", "2 minutes"),
            (BranchCode.FH, GlobalLevelId.L2) => ("duration", "4 minutes"),
            (BranchCode.FH, GlobalLevelId.L3) => ("distractor salience", "very low"),
            (BranchCode.FH, GlobalLevelId.L4) => ("duration", "3 minutes"),
            (BranchCode.FH, GlobalLevelId.L5) => ("duration", "9 minutes"),
            (BranchCode.FS, GlobalLevelId.L1) => ("switch count", "3"),
            (BranchCode.FS, GlobalLevelId.L2) => ("switch count", "8"),
            (BranchCode.FS, GlobalLevelId.L3) => ("cue density", "20 seconds"),
            (BranchCode.FS, GlobalLevelId.L4) => ("switch count", "8"),
            (BranchCode.FS, GlobalLevelId.L5) => ("switch count", "24"),
            (BranchCode.WM, GlobalLevelId.L1) => ("item count", "4"),
            (BranchCode.WM, GlobalLevelId.L2) => ("item count", "6"),
            (BranchCode.WM, GlobalLevelId.L3) => ("operation steps", "1"),
            (BranchCode.WM, GlobalLevelId.L4) => ("delay", "3 minutes"),
            (BranchCode.WM, GlobalLevelId.L5) => ("interference", "two-branch integration"),
            (BranchCode.IR, GlobalLevelId.L1) => ("cue conflict", "low"),
            (BranchCode.IR, GlobalLevelId.L2) => ("exception count", "2"),
            (BranchCode.IR, GlobalLevelId.L3) => ("cue conflict", "moderate"),
            (BranchCode.IR, GlobalLevelId.L4) => ("cue conflict", "structured open-task temptation"),
            (BranchCode.IR, GlobalLevelId.L5) => ("pressure", "moderate time pressure"),
            (BranchCode.DE, GlobalLevelId.L1) => ("item quantity", "8"),
            (BranchCode.DE, GlobalLevelId.L2) => ("item quantity", "16"),
            (BranchCode.DE, GlobalLevelId.L3) => ("error subtlety", "moderate"),
            (BranchCode.DE, GlobalLevelId.L4) => ("error subtlety", "structured critical errors"),
            (BranchCode.DE, GlobalLevelId.L5) => ("error subtlety", "moderate critical errors"),
            (BranchCode.CO, GlobalLevelId.L1) => ("rule ambiguity", "very clear examples"),
            (BranchCode.CO, GlobalLevelId.L2) => ("exception handling", "one exception"),
            (BranchCode.CO, GlobalLevelId.L3) => ("transfer distance", "near domain"),
            (BranchCode.CO, GlobalLevelId.L4) => ("transfer distance", "near transfer"),
            (BranchCode.CO, GlobalLevelId.L5) => ("transfer distance", "far transfer"),
            (BranchCode.AI, GlobalLevelId.L1) => ("time pressure", "120 seconds"),
            (BranchCode.AI, GlobalLevelId.L2) => ("uncertainty", "low"),
            (BranchCode.AI, GlobalLevelId.L3) => ("uncertainty", "low"),
            (BranchCode.AI, GlobalLevelId.L4) => ("time pressure", "120 seconds"),
            (BranchCode.AI, GlobalLevelId.L5) => ("uncertainty", "moderate"),
            (BranchCode.TI, GlobalLevelId.L1) => ("task length", "9 minutes"),
            (BranchCode.TI, GlobalLevelId.L2) => ("number of branches", "2"),
            (BranchCode.TI, GlobalLevelId.L3) => ("task length", "12 minutes"),
            (BranchCode.TI, GlobalLevelId.L4) => ("domain distance", "moderate distance"),
            (BranchCode.TI, GlobalLevelId.L5) => ("task length", "16 minutes"),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown branch-level load progression."),
        };
    }

    private static IReadOnlyList<LoadVariable> FocusHold(GlobalLevelId level) => level switch
    {
        GlobalLevelId.L1 => [Load("duration", "3 minutes"), Load("target subtlety", "simple visual target"), Load("recovery window", "10 seconds")],
        GlobalLevelId.L2 => [Load("duration", "6 minutes"), Load("target subtlety", "simple phrase"), Load("recovery window", "8 seconds")],
        GlobalLevelId.L3 => [Load("duration", "5 minutes"), Load("target subtlety", "simple phrase"), Load("recovery window", "10 seconds"), Load("distractor frequency", "4"), Load("distractor salience", "low")],
        GlobalLevelId.L4 => [Load("duration", "5 minutes"), Load("target subtlety", "unfamiliar simple content"), Load("recovery window", "8 seconds"), Load("distractor frequency", "4"), Load("distractor salience", "moderate")],
        GlobalLevelId.L5 => [Load("duration", "12 minutes"), Load("target subtlety", "integrated task target"), Load("recovery window", "6 seconds"), Load("distractor frequency", "8"), Load("distractor salience", "high")],
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static IReadOnlyList<LoadVariable> FocusShift(GlobalLevelId level) => level switch
    {
        GlobalLevelId.L1 => [Load("target count", "2"), Load("switch count", "4"), Load("cue density", "60 seconds"), Load("response window", "10 seconds"), Load("return precision", "next cue")],
        GlobalLevelId.L2 => [Load("target count", "2"), Load("switch count", "12"), Load("cue density", "30 seconds"), Load("response window", "8 seconds"), Load("return precision", "next cue")],
        GlobalLevelId.L3 => [Load("target count", "2"), Load("switch count", "20"), Load("cue density", "15 seconds"), Load("response window", "5 seconds"), Load("rule contrast", "valid symbol versus invalid lure"), Load("return precision", "next valid cue")],
        GlobalLevelId.L4 => [Load("target count", "2"), Load("switch count", "12"), Load("cue density", "30 seconds"), Load("response window", "5 seconds"), Load("rule contrast", "two branch tasks"), Load("return precision", "preserve both task standards")],
        GlobalLevelId.L5 => [Load("target count", "3"), Load("switch count", "30"), Load("cue density", "30 seconds"), Load("response window", "4 seconds"), Load("rule contrast", "scheduled and unscheduled cues"), Load("return precision", "no component loss")],
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static IReadOnlyList<LoadVariable> WorkingMemory(GlobalLevelId level) => level switch
    {
        GlobalLevelId.L1 => [Load("item count", "5"), Load("detail density", "simple items"), Load("delay", "60 seconds")],
        GlobalLevelId.L2 => [Load("item count", "7"), Load("detail density", "moderate detail"), Load("delay", "90 seconds")],
        GlobalLevelId.L3 => [Load("item count", "6"), Load("detail density", "simple items"), Load("operation steps", "2"), Load("delay", "2 minutes"), Load("interference", "stated transform")],
        GlobalLevelId.L4 => [Load("item count", "8"), Load("detail density", "structural detail"), Load("operation steps", "2"), Load("delay", "5 minutes"), Load("interference", "new domain")],
        GlobalLevelId.L5 => [Load("item count", "8"), Load("detail density", "critical integrated detail"), Load("operation steps", "3"), Load("delay", "5 minutes"), Load("interference", "multi-branch pressure"), Load("task length", "15 minutes")],
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static IReadOnlyList<LoadVariable> Inhibition(GlobalLevelId level) => level switch
    {
        GlobalLevelId.L1 => [Load("cue conflict", "simple go/no-go symbols"), Load("response speed", "2 seconds"), Load("exception count", "0")],
        GlobalLevelId.L2 => [Load("cue conflict", "fixed-pace exceptions"), Load("response speed", "2 seconds"), Load("exception count", "3")],
        GlobalLevelId.L3 => [Load("cue conflict", "high rule conflict"), Load("response speed", "2 seconds"), Load("exception count", "4")],
        GlobalLevelId.L4 => [Load("cue conflict", "open-task temptation"), Load("response speed", "2 seconds"), Load("exception count", "4"), Load("task length", "10 minutes")],
        GlobalLevelId.L5 => [Load("cue conflict", "integrated interruption"), Load("response speed", "2 seconds"), Load("exception count", "5"), Load("pressure", "time pressure"), Load("task length", "15 minutes")],
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static IReadOnlyList<LoadVariable> Discrimination(GlobalLevelId level) => level switch
    {
        GlobalLevelId.L1 => [Load("similarity", "simple relevant difference"), Load("item quantity", "10"), Load("time limit", "2 minutes")],
        GlobalLevelId.L2 => [Load("similarity", "near match"), Load("item quantity", "20"), Load("time limit", "4 minutes")],
        GlobalLevelId.L3 => [Load("error subtlety", "subtle"), Load("output length", "10 lines"), Load("audit delay", "5 minutes"), Load("quantity", "5")],
        GlobalLevelId.L4 => [Load("error subtlety", "unstructured critical errors"), Load("output length", "12 lines"), Load("audit delay", "5 minutes"), Load("quantity", "5")],
        GlobalLevelId.L5 => [Load("error subtlety", "critical and noncritical errors"), Load("output length", "16 lines"), Load("audit delay", "5 minutes"), Load("quantity", "8"), Load("task length", "10 minutes")],
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static IReadOnlyList<LoadVariable> ConceptOperations(GlobalLevelId level) => level switch
    {
        GlobalLevelId.L1 => [Load("rule ambiguity", "clear examples"), Load("example count", "8"), Load("exception handling", "none")],
        GlobalLevelId.L2 => [Load("rule ambiguity", "moderate ambiguity"), Load("example count", "10"), Load("exception handling", "two exceptions")],
        GlobalLevelId.L3 => [Load("relation count", "3"), Load("domain distance", "distant domain"), Load("transfer distance", "near transfer")],
        GlobalLevelId.L4 => [Load("relation count", "4"), Load("domain distance", "open domain"), Load("transfer distance", "far transfer")],
        GlobalLevelId.L5 => [Load("relation count", "5"), Load("domain distance", "integrated domain"), Load("transfer distance", "global task"), Load("task length", "15 minutes")],
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static IReadOnlyList<LoadVariable> AffectiveInterference(GlobalLevelId level) => level switch
    {
        GlobalLevelId.L1 => [Load("time pressure", "90 seconds"), Load("observation", "visible evaluator note")],
        GlobalLevelId.L2 => [Load("time pressure", "90 seconds"), Load("evaluative pressure", "visible score"), Load("uncertainty", "moderate")],
        GlobalLevelId.L3 => [Load("interruption timing", "mid-task"), Load("restart delay", "10 seconds"), Load("task complexity", "source standard"), Load("recovery window", "30 seconds"), Load("uncertainty", "moderate")],
        GlobalLevelId.L4 => [Load("time pressure", "90 seconds"), Load("evaluative pressure", "open-task score"), Load("uncertainty", "high")],
        GlobalLevelId.L5 => [Load("number of branches", "4"), Load("time pressure", "high"), Load("evaluative pressure", "global review"), Load("uncertainty", "high")],
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static IReadOnlyList<LoadVariable> TransferIntegration(GlobalLevelId level) => level switch
    {
        GlobalLevelId.L1 => [Load("number of branches", "2"), Load("task length", "12 minutes"), Load("domain distance", "near transfer")],
        GlobalLevelId.L2 => [Load("number of branches", "3"), Load("task length", "15 minutes"), Load("domain distance", "new content domain"), Load("delay", "3 minutes")],
        GlobalLevelId.L3 => [Load("number of branches", "4"), Load("task length", "15 minutes"), Load("domain distance", "advanced branch active"), Load("interference", "CO or AI demand")],
        GlobalLevelId.L4 => [Load("number of branches", "4"), Load("task length", "18 minutes"), Load("domain distance", "far transfer"), Load("interference", "bottleneck branch")],
        GlobalLevelId.L5 => [Load("number of branches", "6"), Load("task length", "20 minutes"), Load("domain distance", "global task"), Load("interference", "pressure and ambiguity"), Load("delay", "5 minutes")],
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static LoadVariable Load(string name, string value) => new(name, value);

    private static void ValidateSingleVariableProgression(
        BranchCode branch,
        string variable,
        IReadOnlyList<LoadVariable> regression,
        IReadOnlyList<LoadVariable> target)
    {
        var changes = target.Where(targetLoad =>
        {
            var previous = regression.Single(load =>
                string.Equals(load.Name, targetLoad.Name, StringComparison.OrdinalIgnoreCase));
            return !string.Equals(previous.Value, targetLoad.Value, StringComparison.Ordinal);
        }).ToArray();
        if (changes.Length != 1 ||
            !string.Equals(changes[0].Name, variable, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A training load stage must increase exactly one named variable.");
        }

        var kind = KindFor(variable);
        var validation = LoadChangeEvaluator.Evaluate(new LoadChangeRequest(
            branch,
            [kind],
            presentForbiddenLoadIncreases: [],
            LoadChangeMode.Acquisition,
            increasedVariablesStableSeparately: false));
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Training load profile {branch} uses a variable that Core does not permit for acquisition.");
        }
    }
}

public static class ProgressiveLoadPlanner
{
    public static ProgressiveLoadPrescription Prescribe(
        TrainingLoadProfile profile,
        SessionType sessionType,
        IEnumerable<TrainingLoadHistoryEntry>? history = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Enum.IsDefined(sessionType))
        {
            throw new ArgumentOutOfRangeException(nameof(sessionType), sessionType, "Unknown training session type.");
        }

        var entries = (history ?? Array.Empty<TrainingLoadHistoryEntry>()).ToArray();
        var currentIndex = entries.Length == 0
            ? 0
            : MatchingStage(profile, entries[^1].LoadVariables)?.Index ?? 0;

        if (sessionType is SessionType.Test or SessionType.Stabilization or SessionType.Transfer)
        {
            return new ProgressiveLoadPrescription(
                profile,
                profile.TargetStage,
                "Formal work uses the full stated standard load.");
        }

        if (sessionType is SessionType.Regression or SessionType.Recovery)
        {
            var reduced = profile.Stages[Math.Max(0, currentIndex - 1)];
            return new ProgressiveLoadPrescription(
                profile,
                reduced,
                "Reduced load preserves the same demand and honesty constraints.");
        }

        if (entries.LastOrDefault() is { Clean: false } latest && (latest.Overload || entries.Length > 0))
        {
            var regressed = profile.Stages[Math.Max(0, currentIndex - 1)];
            return new ProgressiveLoadPrescription(
                profile,
                regressed,
                "The latest work was not clean, so load does not increase.");
        }

        var cleanAtCurrent = entries
            .Reverse()
            .TakeWhile(entry =>
                entry.Clean && MatchingStage(profile, entry.LoadVariables)?.Index == currentIndex)
            .Take(2)
            .Count();
        if (cleanAtCurrent >= 2 && currentIndex < profile.TargetStage.Index)
        {
            var next = profile.Stages[currentIndex + 1];
            return new ProgressiveLoadPrescription(
                profile,
                next,
                $"Two clean exposures permit one increase: {next.IncreasedVariable}.");
        }

        return new ProgressiveLoadPrescription(
            profile,
            profile.Stages[currentIndex],
            entries.Length == 0
                ? "Begin below the gate while preserving the full honesty constraint."
                : "Repeat this load until two clean exposures are recorded.");
    }

    private static TrainingLoadStage? MatchingStage(
        TrainingLoadProfile profile,
        IReadOnlyList<LoadVariable> variables)
    {
        return profile.Stages.FirstOrDefault(stage => SameLoad(stage.LoadVariables, variables));
    }

    private static bool SameLoad(
        IReadOnlyList<LoadVariable> left,
        IReadOnlyList<LoadVariable> right)
    {
        return left.Count == right.Count && left.All(variable => right.Any(candidate =>
            string.Equals(candidate.Name, variable.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Value, variable.Value, StringComparison.Ordinal)));
    }
}

public static class TrainingLoadStageStandardResolver
{
    public static EvaluatedStandard Resolve(
        TrainingLoadProfile profile,
        IReadOnlyList<LoadVariable> prescribedLoad,
        EvaluatedStandard formalStandard)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(prescribedLoad);
        ArgumentNullException.ThrowIfNull(formalStandard);

        var stage = profile.Stages.FirstOrDefault(candidate => SameLoad(
            candidate.LoadVariables,
            prescribedLoad));
        if (stage is null || stage.Index == profile.TargetStage.Index)
        {
            return formalStandard;
        }

        var increasedVariable = profile.TargetStage.IncreasedVariable;
        var thresholds = formalStandard.NumericThresholds
            .Select(threshold => AdjustThreshold(threshold, prescribedLoad, increasedVariable))
            .ToArray();
        return new EvaluatedStandard(
            $"{formalStandard.Name} - load stage {stage.Index}",
            thresholds,
            formalStandard.CriticalConstraints,
            formalStandard.RequiresCompleteOutput,
            formalStandard.RequiredRubric);
    }

    public static bool IsFormalStandardLoad(
        TrainingLoadProfile profile,
        IReadOnlyList<LoadVariable> prescribedLoad)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(prescribedLoad);

        return SameLoad(profile.TargetStage.LoadVariables, prescribedLoad);
    }

    private static NumericThreshold AdjustThreshold(
        NumericThreshold threshold,
        IReadOnlyList<LoadVariable> load,
        string? increasedVariable)
    {
        if (string.IsNullOrWhiteSpace(increasedVariable) ||
            threshold.Direction != NumericThresholdDirection.AtLeast)
        {
            return threshold;
        }

        var adjusted = increasedVariable.Trim().ToLowerInvariant() switch
        {
            "duration" or "task length"
                when threshold.MeasurementName == TrainingStandardMeasurements.ActiveDurationSeconds =>
                    DurationSeconds(load, increasedVariable),
            "delay" when threshold.MeasurementName == TrainingStandardMeasurements.DelaySeconds =>
                DurationSeconds(load, increasedVariable),
            "switch count" when threshold.MeasurementName == TrainingStandardMeasurements.ActiveDurationSeconds =>
                SwitchWorkSeconds(load),
            "item count" when threshold.MeasurementName == TrainingStandardMeasurements.ExactItemCount =>
                NumericValue(load, increasedVariable),
            "item quantity" when threshold.MeasurementName == TrainingStandardMeasurements.ComparisonCount =>
                NumericValue(load, increasedVariable),
            "number of branches" when threshold.MeasurementName == TrainingStandardMeasurements.ComponentCount =>
                NumericValue(load, increasedVariable),
            _ => null,
        };

        return adjusted.HasValue
            ? threshold with { Value = Math.Min(threshold.Value, adjusted.Value) }
            : threshold;
    }

    private static decimal? SwitchWorkSeconds(IReadOnlyList<LoadVariable> load)
    {
        var count = NumericValue(load, "switch count");
        var interval = DurationSeconds(load, "cue density");
        return count.HasValue && interval.HasValue ? count.Value * interval.Value : null;
    }

    private static decimal? NumericValue(
        IReadOnlyList<LoadVariable> load,
        string name)
    {
        var value = Value(load, name);
        if (value is null)
        {
            return null;
        }

        var numericPrefix = Regex.Match(value, @"\d+(?:\.\d+)?").Value;
        return decimal.TryParse(
            numericPrefix,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var parsed)
                ? parsed
                : null;
    }

    private static decimal? DurationSeconds(
        IReadOnlyList<LoadVariable> load,
        string name)
    {
        var value = Value(load, name);
        var amount = NumericValue(load, name);
        if (value is null || !amount.HasValue)
        {
            return null;
        }

        return value.Contains("minute", StringComparison.OrdinalIgnoreCase)
            ? amount.Value * 60m
            : amount.Value;
    }

    private static string? Value(
        IReadOnlyList<LoadVariable> load,
        string name)
    {
        return load.FirstOrDefault(variable => string.Equals(
            variable.Name,
            name,
            StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static bool SameLoad(
        IReadOnlyList<LoadVariable> left,
        IReadOnlyList<LoadVariable> right)
    {
        return left.Count == right.Count && left.All(variable => right.Any(candidate =>
            string.Equals(candidate.Name, variable.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Value, variable.Value, StringComparison.Ordinal)));
    }
}
