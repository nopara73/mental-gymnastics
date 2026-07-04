namespace MentalGymnastics.Core;

public sealed class RestorationCheckEvidence
{
    public RestorationCheckEvidence(
        BranchCode branch,
        GlobalLevelId lastOwnedLevel,
        TrainingDate date,
        RestorationCheckKind kind,
        StandardEvaluationResult standardEvaluationResult)
    {
        ArgumentNullException.ThrowIfNull(standardEvaluationResult);

        Branch = branch;
        LastOwnedLevel = lastOwnedLevel;
        Date = date;
        Kind = kind;
        StandardEvaluationResult = standardEvaluationResult;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId LastOwnedLevel { get; }

    public TrainingDate Date { get; }

    public RestorationCheckKind Kind { get; }

    public StandardEvaluationResult StandardEvaluationResult { get; }

    public bool Passed => StandardEvaluationResult.Passed;
}

public sealed class RestorationEvidence
{
    public RestorationEvidence(
        BranchCode branch,
        GlobalLevelId lastOwnedLevel,
        IEnumerable<RestorationCheckEvidence> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);

        Branch = branch;
        LastOwnedLevel = lastOwnedLevel;
        Checks = checks.ToArray();
    }

    public BranchCode Branch { get; }

    public GlobalLevelId LastOwnedLevel { get; }

    public IReadOnlyList<RestorationCheckEvidence> Checks { get; }
}

public sealed record DecayRestorationFailure(
    DecayRestorationFailureKind Kind,
    string Detail);

public sealed record DecayRestorationResult(
    BranchLevelStatus CurrentStatus,
    BranchLevelStatus NextStatus,
    BranchLevelTransition? Transition,
    IReadOnlyList<DecayRestorationFailure> Failures)
{
    public bool ChangedState => CurrentStatus.State != NextStatus.State;
}

public static class DecayRestorationEvaluator
{
    public static DecayRestorationResult EvaluateDecay(
        BranchLevelStatus currentStatus,
        MaintenanceCurrencyResult maintenanceCurrency)
    {
        ArgumentNullException.ThrowIfNull(maintenanceCurrency);

        var failures = new List<DecayRestorationFailure>();
        if (currentStatus.State != BranchLevelState.Maintenance)
        {
            failures.Add(new DecayRestorationFailure(
                DecayRestorationFailureKind.DecayRequiresMaintenanceState,
                "Only a branch-level in maintenance can decay from failed maintenance checks."));
        }

        if (maintenanceCurrency.Branch != currentStatus.Branch ||
            maintenanceCurrency.OwnedLevel != currentStatus.Level)
        {
            failures.Add(new DecayRestorationFailure(
                DecayRestorationFailureKind.MaintenanceCurrencyDoesNotMatchBranchLevel,
                "Maintenance currency must match the branch-level being evaluated for decay."));
        }

        if (maintenanceCurrency.State != MaintenanceCurrencyState.Failed ||
            maintenanceCurrency.ConsecutiveFailures < 2)
        {
            failures.Add(new DecayRestorationFailure(
                DecayRestorationFailureKind.MaintenanceFailureThresholdNotMet,
                "Decay requires two failed maintenance checks in the branch."));
        }

        if (failures.Count > 0)
        {
            return Unchanged(currentStatus, failures);
        }

        return ApplyTransition(currentStatus, BranchLevelTransition.MarkDecayed);
    }

    public static DecayRestorationResult EvaluateRestoration(
        BranchLevelStatus currentStatus,
        RestorationEvidence restorationEvidence)
    {
        ArgumentNullException.ThrowIfNull(restorationEvidence);

        var failures = new List<DecayRestorationFailure>();
        if (currentStatus.State != BranchLevelState.Decayed)
        {
            failures.Add(new DecayRestorationFailure(
                DecayRestorationFailureKind.RestorationRequiresDecayedState,
                "Only a decayed branch-level can be restored."));
        }

        if (restorationEvidence.Branch != currentStatus.Branch ||
            restorationEvidence.LastOwnedLevel != currentStatus.Level)
        {
            failures.Add(new DecayRestorationFailure(
                DecayRestorationFailureKind.RestorationEvidenceDoesNotMatchBranchLevel,
                "Restoration evidence must match the decayed branch and last owned level."));
        }

        var matchingChecks = restorationEvidence.Checks
            .Where(check =>
                check.Branch == restorationEvidence.Branch &&
                check.LastOwnedLevel == restorationEvidence.LastOwnedLevel &&
                check.Passed)
            .ToArray();

        if (!matchingChecks.Any(check => check.Kind == RestorationCheckKind.LastOwnedStandard))
        {
            failures.Add(new DecayRestorationFailure(
                DecayRestorationFailureKind.LastOwnedStandardPassMissing,
                "Restoration requires passing the last owned standard once."));
        }

        if (!matchingChecks.Any(check => check.Kind == RestorationCheckKind.LowerLoadTransferCheck))
        {
            failures.Add(new DecayRestorationFailure(
                DecayRestorationFailureKind.LowerLoadTransferCheckMissing,
                "Restoration requires one lower-load transfer check."));
        }

        if (failures.Count > 0)
        {
            return Unchanged(currentStatus, failures);
        }

        return ApplyTransition(currentStatus, BranchLevelTransition.RestoreToMaintenance);
    }

    private static DecayRestorationResult ApplyTransition(
        BranchLevelStatus currentStatus,
        BranchLevelTransition transition)
    {
        var transitionResult = BranchLevelStateMachine.TryApply(currentStatus, transition);
        if (!transitionResult.IsValid)
        {
            return Unchanged(
                currentStatus,
                [
                    new DecayRestorationFailure(
                        DecayRestorationFailureKind.RestorationEvidenceDoesNotMatchBranchLevel,
                        "The requested transition is not legal for the current branch-level state."),
                ]);
        }

        return new DecayRestorationResult(
            currentStatus,
            transitionResult.NextStatus,
            transition,
            []);
    }

    private static DecayRestorationResult Unchanged(
        BranchLevelStatus currentStatus,
        IReadOnlyList<DecayRestorationFailure> failures)
    {
        return new DecayRestorationResult(
            currentStatus,
            currentStatus,
            null,
            failures);
    }
}
