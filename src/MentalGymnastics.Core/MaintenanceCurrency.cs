namespace MentalGymnastics.Core;

public sealed record MaintenanceCadence(
    int DueAfterDays,
    int OverdueAfterDays,
    MaintenanceCheckKind RequiredCheckKind);

public sealed class MaintenanceCheckEvidence
{
    public MaintenanceCheckEvidence(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate date,
        MaintenanceCheckKind kind,
        StandardEvaluationResult standardEvaluationResult)
    {
        ArgumentNullException.ThrowIfNull(standardEvaluationResult);

        Branch = branch;
        OwnedLevel = ownedLevel;
        Date = date;
        Kind = kind;
        StandardEvaluationResult = standardEvaluationResult;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId OwnedLevel { get; }

    public TrainingDate Date { get; }

    public MaintenanceCheckKind Kind { get; }

    public StandardEvaluationResult StandardEvaluationResult { get; }

    public bool Passed => StandardEvaluationResult.Passed;
}

public sealed class MaintenanceCurrencyRequest
{
    public MaintenanceCurrencyRequest(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        TrainingDate asOf,
        IEnumerable<MaintenanceCheckEvidence> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);

        Branch = branch;
        OwnedLevel = ownedLevel;
        AsOf = asOf;
        Checks = checks.ToArray();
    }

    public BranchCode Branch { get; }

    public GlobalLevelId OwnedLevel { get; }

    public TrainingDate AsOf { get; }

    public IReadOnlyList<MaintenanceCheckEvidence> Checks { get; }
}

public sealed record MaintenanceCurrencyResult(
    BranchCode Branch,
    GlobalLevelId OwnedLevel,
    MaintenanceCurrencyState State,
    MaintenanceCadence Cadence,
    int? DaysSinceLastPassingCheck,
    int ConsecutiveFailures);

public static class MaintenanceCurrencyEvaluator
{
    public static MaintenanceCurrencyResult Evaluate(MaintenanceCurrencyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cadence = CadenceFor(request.Branch, request.OwnedLevel);
        var relevantChecks = request.Checks
            .Where(check =>
                check.Branch == request.Branch &&
                check.OwnedLevel == request.OwnedLevel &&
                check.Kind == cadence.RequiredCheckKind &&
                check.Date.DaysUntil(request.AsOf) >= 0)
            .OrderBy(check => check.Date.ToDayNumber())
            .ToArray();

        var consecutiveFailures = CountConsecutiveFailures(relevantChecks);
        var daysSinceLastPassingCheck = DaysSinceLastPassingCheck(relevantChecks, request.AsOf);
        var state = DetermineState(cadence, daysSinceLastPassingCheck, consecutiveFailures);

        return new MaintenanceCurrencyResult(
            request.Branch,
            request.OwnedLevel,
            state,
            cadence,
            daysSinceLastPassingCheck,
            consecutiveFailures);
    }

    public static MaintenanceCadence CadenceFor(BranchCode branch, GlobalLevelId ownedLevel)
    {
        if (branch == BranchCode.TI && ownedLevel >= GlobalLevelId.L3)
        {
            return new MaintenanceCadence(28, 28, MaintenanceCheckKind.GlobalComposite);
        }

        var branchDefinition = ProgramCatalog.Branches.Single(item => item.Code == branch);
        if (branchDefinition.Type == BranchType.Advanced)
        {
            return new MaintenanceCadence(10, 14, MaintenanceCheckKind.StandardOrTransfer);
        }

        return ownedLevel switch
        {
            GlobalLevelId.L1 or GlobalLevelId.L2 => new MaintenanceCadence(
                7,
                7,
                MaintenanceCheckKind.StandardOrTransfer),
            _ => new MaintenanceCadence(7, 10, MaintenanceCheckKind.StandardOrTransfer),
        };
    }

    private static int CountConsecutiveFailures(IReadOnlyList<MaintenanceCheckEvidence> checks)
    {
        var count = 0;
        for (var index = checks.Count - 1; index >= 0; index--)
        {
            if (checks[index].Passed)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static int? DaysSinceLastPassingCheck(
        IReadOnlyList<MaintenanceCheckEvidence> checks,
        TrainingDate asOf)
    {
        var lastPass = checks.LastOrDefault(check => check.Passed);
        if (lastPass is null)
        {
            return null;
        }

        return lastPass.Date.DaysUntil(asOf);
    }

    private static MaintenanceCurrencyState DetermineState(
        MaintenanceCadence cadence,
        int? daysSinceLastPassingCheck,
        int consecutiveFailures)
    {
        if (consecutiveFailures >= 2)
        {
            return MaintenanceCurrencyState.Failed;
        }

        if (consecutiveFailures == 1)
        {
            return MaintenanceCurrencyState.Warning;
        }

        if (daysSinceLastPassingCheck is null ||
            daysSinceLastPassingCheck.Value > cadence.OverdueAfterDays)
        {
            return MaintenanceCurrencyState.Due;
        }

        return MaintenanceCurrencyState.Current;
    }
}
