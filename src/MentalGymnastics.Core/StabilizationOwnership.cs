namespace MentalGymnastics.Core;

public sealed class StabilizationPassEvidence
{
    public StabilizationPassEvidence(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        string standard,
        FormalTestPassState passState,
        StandardEvaluationResult standardEvaluationResult,
        bool afterAdjacentWorkOrControlledDistractor)
    {
        ArgumentNullException.ThrowIfNull(standardEvaluationResult);

        if (string.IsNullOrWhiteSpace(standard))
        {
            throw new ArgumentException("Passed standard is required.", nameof(standard));
        }

        Branch = branch;
        Level = level;
        Date = date;
        Standard = standard;
        PassState = passState;
        StandardEvaluationResult = standardEvaluationResult;
        AfterAdjacentWorkOrControlledDistractor = afterAdjacentWorkOrControlledDistractor;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public TrainingDate Date { get; }

    public string Standard { get; }

    public FormalTestPassState PassState { get; }

    public StandardEvaluationResult StandardEvaluationResult { get; }

    public bool AfterAdjacentWorkOrControlledDistractor { get; }

    public bool IsCleanPass =>
        StandardEvaluationResult.Passed &&
        PassState is FormalTestPassState.PassOnce or FormalTestPassState.StabilizationPass;
}

public sealed class StabilizationEvidence
{
    public StabilizationEvidence(
        BranchCode branch,
        GlobalLevelId level,
        IEnumerable<StabilizationPassEvidence> passes)
    {
        ArgumentNullException.ThrowIfNull(passes);

        Branch = branch;
        Level = level;
        Passes = passes.ToArray();
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public IReadOnlyList<StabilizationPassEvidence> Passes { get; }
}

public sealed record StabilizationOwnershipFailure(
    StabilizationOwnershipFailureKind Kind,
    string Detail);

public sealed record StabilizationOwnershipResult(
    bool IsOwned,
    GateOutcome GateOutcome,
    BranchLevelState BranchLevelState,
    IReadOnlyList<StabilizationOwnershipFailure> Failures);

public static class StabilizationOwnershipEvaluator
{
    private const int RequiredCleanPassCount = 3;
    private const int RequiredStabilizationPassCount = 2;
    private const int RequiredOwnershipSpanDays = 7;
    private const int StabilizationWindowDays = 14;

    public static StabilizationOwnershipResult Evaluate(StabilizationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var failures = new List<StabilizationOwnershipFailure>();
        var cleanPasses = evidence.Passes
            .Where(pass => pass.Branch == evidence.Branch && pass.Level == evidence.Level && pass.IsCleanPass)
            .OrderBy(pass => pass.Date.ToDayNumber())
            .ToArray();

        if (cleanPasses.Length == 0)
        {
            failures.Add(new StabilizationOwnershipFailure(
                StabilizationOwnershipFailureKind.InsufficientCleanPasses,
                "At least one clean formal pass is required."));

            return Result(cleanPasses, failures);
        }

        EvaluateCleanPassCount(cleanPasses, failures);
        EvaluateStabilizationPasses(cleanPasses, failures);
        EvaluateCalendarSpan(cleanPasses, failures);
        EvaluateAdjacentWorkOrDistractorPass(cleanPasses, failures);
        EvaluateStandardStability(cleanPasses, failures);

        return Result(cleanPasses, failures);
    }

    private static void EvaluateCleanPassCount(
        IReadOnlyList<StabilizationPassEvidence> cleanPasses,
        ICollection<StabilizationOwnershipFailure> failures)
    {
        if (cleanPasses.Count < RequiredCleanPassCount)
        {
            failures.Add(new StabilizationOwnershipFailure(
                StabilizationOwnershipFailureKind.InsufficientCleanPasses,
                "Ownership requires three clean passes of the same standard."));
        }
    }

    private static void EvaluateStabilizationPasses(
        IReadOnlyList<StabilizationPassEvidence> cleanPasses,
        ICollection<StabilizationOwnershipFailure> failures)
    {
        var firstPass = cleanPasses.First();
        var stabilizationPasses = cleanPasses
            .Where(pass => pass.PassState == FormalTestPassState.StabilizationPass)
            .ToArray();

        if (stabilizationPasses.Length < RequiredStabilizationPassCount)
        {
            failures.Add(new StabilizationOwnershipFailure(
                StabilizationOwnershipFailureKind.StabilizationPassesMissing,
                "Ownership requires two additional stabilization passes."));
            return;
        }

        if (stabilizationPasses.Select(pass => pass.Date.ToDayNumber()).Distinct().Count() < RequiredStabilizationPassCount)
        {
            failures.Add(new StabilizationOwnershipFailure(
                StabilizationOwnershipFailureKind.StabilizationPassesNotOnDifferentDays,
                "Stabilization passes must occur on different days."));
        }

        if (stabilizationPasses.Any(pass => firstPass.Date.DaysUntil(pass.Date) > StabilizationWindowDays))
        {
            failures.Add(new StabilizationOwnershipFailure(
                StabilizationOwnershipFailureKind.StabilizationWindowMissed,
                "Stabilization passes must occur within 14 days of the first pass."));
        }
    }

    private static void EvaluateCalendarSpan(
        IReadOnlyList<StabilizationPassEvidence> cleanPasses,
        ICollection<StabilizationOwnershipFailure> failures)
    {
        var daysBetweenFirstAndLastPass = cleanPasses.First().Date.DaysUntil(cleanPasses.Last().Date);
        if (daysBetweenFirstAndLastPass < RequiredOwnershipSpanDays)
        {
            failures.Add(new StabilizationOwnershipFailure(
                StabilizationOwnershipFailureKind.SevenDaySpanMissing,
                "Ownership requires passes across at least seven calendar days."));
        }
    }

    private static void EvaluateAdjacentWorkOrDistractorPass(
        IReadOnlyList<StabilizationPassEvidence> cleanPasses,
        ICollection<StabilizationOwnershipFailure> failures)
    {
        if (!cleanPasses.Any(pass => pass.AfterAdjacentWorkOrControlledDistractor))
        {
            failures.Add(new StabilizationOwnershipFailure(
                StabilizationOwnershipFailureKind.AdjacentWorkOrDistractorPassMissing,
                "At least one stabilization pass must follow adjacent work or a controlled distractor."));
        }
    }

    private static void EvaluateStandardStability(
        IReadOnlyList<StabilizationPassEvidence> cleanPasses,
        ICollection<StabilizationOwnershipFailure> failures)
    {
        var firstStandard = cleanPasses.First().Standard;
        if (cleanPasses.Any(pass => !string.Equals(pass.Standard, firstStandard, StringComparison.Ordinal)))
        {
            failures.Add(new StabilizationOwnershipFailure(
                StabilizationOwnershipFailureKind.StandardChanged,
                "Ownership cannot be granted when the standard changed during stabilization."));
        }
    }

    private static StabilizationOwnershipResult Result(
        IReadOnlyList<StabilizationPassEvidence> cleanPasses,
        IReadOnlyList<StabilizationOwnershipFailure> failures)
    {
        if (failures.Count == 0)
        {
            return new StabilizationOwnershipResult(true, GateOutcome.Own, BranchLevelState.Owned, []);
        }

        if (cleanPasses.Count == 1)
        {
            return new StabilizationOwnershipResult(false, GateOutcome.PassOnce, BranchLevelState.PassedOnce, failures);
        }

        return new StabilizationOwnershipResult(false, GateOutcome.Stabilize, BranchLevelState.Stabilizing, failures);
    }
}
