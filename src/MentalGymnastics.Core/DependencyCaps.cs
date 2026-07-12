namespace MentalGymnastics.Core;

public sealed record AdvancedBranchDependencyBlock(
    BranchCode Branch,
    IReadOnlyList<BranchLevelRequirement> RequiredLevels,
    IReadOnlyList<BranchLevelRequirementGroup> AnyOfLevelGroups);

public sealed class DependencyCapRequest
{
    public DependencyCapRequest(
        BranchCode branch,
        PractitionerState practitionerState,
        IEnumerable<MaintenanceCurrencyResult> maintenanceCurrency)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);
        ArgumentNullException.ThrowIfNull(maintenanceCurrency);

        Branch = branch;
        PractitionerState = practitionerState;
        MaintenanceCurrency = maintenanceCurrency.ToArray();
    }

    public BranchCode Branch { get; }

    public PractitionerState PractitionerState { get; }

    public IReadOnlyList<MaintenanceCurrencyResult> MaintenanceCurrency { get; }
}

public sealed record DependencyCap(
    BranchCode TargetBranch,
    BranchCode PrerequisiteBranch,
    GlobalLevelId PrerequisiteLevel,
    DependencyCapReason Reason,
    string Detail);

public sealed record DependencyCapResult(
    BranchCode Branch,
    bool CanAdvance,
    bool IsCappedToMaintenanceOnly,
    IReadOnlyList<DependencyCap> Caps);

public static class DependencyCapCatalog
{
    public static IReadOnlyList<AdvancedBranchDependencyBlock> AdvancedDependencyBlocks { get; } =
        ProgramCatalog.BranchUnlocks
            .Where(unlock => ProgramCatalog.Branches.Single(branch => branch.Code == unlock.Branch).Type == BranchType.Advanced)
            .Select(unlock => new AdvancedBranchDependencyBlock(
                unlock.Branch,
                unlock.RequiredLevels,
                unlock.AnyOfLevelGroups))
            .ToArray();
}

public static class DependencyCapEvaluator
{
    public static DependencyCapResult Evaluate(DependencyCapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var block = DependencyCapCatalog.AdvancedDependencyBlocks.SingleOrDefault(item => item.Branch == request.Branch);
        if (block is null)
        {
            return new DependencyCapResult(request.Branch, CanAdvance: true, IsCappedToMaintenanceOnly: false, Caps: []);
        }

        var caps = new List<DependencyCap>();
        var dependenciesSatisfied = true;
        foreach (var requirement in block.RequiredLevels)
        {
            var evaluation = EvaluateRequirement(request, requirement);

            dependenciesSatisfied &= evaluation.IsSatisfied;
            caps.AddRange(evaluation.Caps);
        }

        foreach (var group in block.AnyOfLevelGroups)
        {
            var evaluation = EvaluateAnyOfGroup(request, group);

            dependenciesSatisfied &= evaluation.IsSatisfied;
            caps.AddRange(evaluation.Caps);
        }

        return new DependencyCapResult(
            request.Branch,
            CanAdvance: dependenciesSatisfied && caps.Count == 0,
            IsCappedToMaintenanceOnly: caps.Count > 0,
            Caps: caps);
    }

    private static DependencyRequirementEvaluation EvaluateAnyOfGroup(
        DependencyCapRequest request,
        BranchLevelRequirementGroup group)
    {
        var routeEvaluations = group.Requirements
            .Select(requirement => EvaluateRequirement(request, requirement))
            .ToArray();

        if (routeEvaluations.Any(evaluation => evaluation.IsSatisfied && evaluation.Caps.Count == 0))
        {
            return new DependencyRequirementEvaluation(IsSatisfied: true, Caps: []);
        }

        return new DependencyRequirementEvaluation(
            IsSatisfied: false,
            routeEvaluations.SelectMany(evaluation => evaluation.Caps).ToArray());
    }

    private static DependencyRequirementEvaluation EvaluateRequirement(
        DependencyCapRequest request,
        BranchLevelRequirement requirement)
    {
        var caps = new List<DependencyCap>();
        var actualStateKnown = request.PractitionerState.TryGetBranchLevelState(
            requirement.Branch,
            requirement.Level,
            out var actualState);

        if (actualStateKnown && actualState == BranchLevelState.Decayed)
        {
            caps.Add(new DependencyCap(
                request.Branch,
                requirement.Branch,
                requirement.Level,
                DependencyCapReason.DecayedPrerequisite,
                $"{request.Branch} advancement is capped because {requirement.Branch} {requirement.Level} is decayed."));

            return new DependencyRequirementEvaluation(IsSatisfied: false, Caps: caps);
        }

        var prerequisiteIsSatisfied = actualStateKnown &&
            BranchLevelRequirementEvaluator.StateSatisfies(actualState, requirement.RequiredState);
        if (prerequisiteIsSatisfied &&
            MaintenanceIsOverdueOrFailed(request.MaintenanceCurrency, requirement))
        {
            caps.Add(new DependencyCap(
                request.Branch,
                requirement.Branch,
                requirement.Level,
                DependencyCapReason.OverduePrerequisiteMaintenance,
                $"{request.Branch} advancement is capped because {requirement.Branch} {requirement.Level} maintenance is overdue."));
        }

        return new DependencyRequirementEvaluation(prerequisiteIsSatisfied && caps.Count == 0, caps);
    }

    private static bool MaintenanceIsOverdueOrFailed(
        IReadOnlyList<MaintenanceCurrencyResult> maintenanceCurrency,
        BranchLevelRequirement requirement)
    {
        var currency = maintenanceCurrency
            .Where(item =>
                item.Branch == requirement.Branch &&
                item.OwnedLevel >= requirement.Level)
            .OrderByDescending(item => item.OwnedLevel)
            .FirstOrDefault();

        return currency is null ||
            currency.State is MaintenanceCurrencyState.Due or MaintenanceCurrencyState.Failed;
    }

    private sealed record DependencyRequirementEvaluation(
        bool IsSatisfied,
        IReadOnlyList<DependencyCap> Caps);
}
