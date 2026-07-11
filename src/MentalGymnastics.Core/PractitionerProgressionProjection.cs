namespace MentalGymnastics.Core;

public sealed class PractitionerProgressionProjectionRequest
{
    public PractitionerProgressionProjectionRequest(
        PractitionerState practitionerState,
        IEnumerable<MaintenanceCurrencyResult>? maintenanceCurrency = null,
        GlobalReviewResult? lastGlobalReview = null)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);

        PractitionerState = practitionerState;
        MaintenanceCurrency = (maintenanceCurrency ?? Array.Empty<MaintenanceCurrencyResult>()).ToArray();
        LastGlobalReview = lastGlobalReview;
    }

    public PractitionerState PractitionerState { get; }

    public IReadOnlyList<MaintenanceCurrencyResult> MaintenanceCurrency { get; }

    public GlobalReviewResult? LastGlobalReview { get; }
}

public sealed record PractitionerProgressionProjectionResult(
    PractitionerState PractitionerState,
    IReadOnlyList<BranchLevelStatusTransitionResult> OpenedForTraining,
    IReadOnlyList<GlobalBalanceIssue> BalanceBlocks);

public static class PractitionerProgressionProjector
{
    public static PractitionerProgressionProjectionResult Project(
        PractitionerProgressionProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var statuses = request.PractitionerState.BranchLevels.ToArray();
        var opened = new List<BranchLevelStatusTransitionResult>();
        var balanceBlocks = new List<GlobalBalanceIssue>();

        foreach (var candidate in statuses
            .Where(status => status.State == BranchLevelState.Unopened)
            .OrderBy(status => status.Level)
            .ThenBy(status => status.Branch))
        {
            var current = new PractitionerState(statuses);
            if (!PrerequisitesSatisfied(current, candidate.Branch, candidate.Level))
            {
                continue;
            }

            var balance = GlobalBalanceEvaluator.EvaluateAdvancement(
                new GlobalBalanceAdvancementRequest(
                    candidate.Branch,
                    candidate.Level,
                    current,
                    request.MaintenanceCurrency,
                    request.LastGlobalReview));
            if (!balance.CanAdvance)
            {
                balanceBlocks.AddRange(balance.Issues);
                continue;
            }

            var transition = BranchLevelStateMachine.TryApply(
                candidate,
                BranchLevelTransition.OpenForTraining);
            if (!transition.IsValid)
            {
                continue;
            }

            var index = Array.FindIndex(statuses, status =>
                status.Branch == candidate.Branch && status.Level == candidate.Level);
            statuses[index] = transition.NextStatus;
            opened.Add(transition);
        }

        return new PractitionerProgressionProjectionResult(
            new PractitionerState(statuses),
            opened.AsReadOnly(),
            balanceBlocks
                .DistinctBy(issue => (issue.Kind, issue.Branch, issue.Level, issue.Detail))
                .ToArray());
    }

    private static bool PrerequisitesSatisfied(
        PractitionerState state,
        BranchCode branch,
        GlobalLevelId level)
    {
        var prerequisites = TestReadinessPrerequisites.For(branch, level);
        if (prerequisites.RequiredLevels.Any(requirement =>
                !BranchLevelRequirementEvaluator.IsSatisfied(state, requirement)))
        {
            return false;
        }

        return prerequisites.AnyOfLevelGroups.All(group =>
            group.Requirements.Any(requirement =>
                BranchLevelRequirementEvaluator.IsSatisfied(state, requirement)));
    }
}
