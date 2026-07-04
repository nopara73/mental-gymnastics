namespace MentalGymnastics.Core;

public readonly record struct TestReadinessPracticeSession
{
    public TestReadinessPracticeSession(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        string drillDemand,
        bool clean)
    {
        if (string.IsNullOrWhiteSpace(drillDemand))
        {
            throw new ArgumentException("Practice drill demand is required.", nameof(drillDemand));
        }

        Branch = branch;
        Level = level;
        Drill = drill;
        DrillDemand = drillDemand;
        Clean = clean;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public string DrillDemand { get; }

    public bool Clean { get; }
}

public readonly record struct PrerequisiteMaintenanceCheck
{
    public PrerequisiteMaintenanceCheck(BranchCode branch, GlobalLevelId level, bool isCurrent)
    {
        Branch = branch;
        Level = level;
        IsCurrent = isCurrent;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public bool IsCurrent { get; }
}

public sealed class TestReadinessRequest
{
    public TestReadinessRequest(
        PractitionerState practitionerState,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        string drillDemand,
        IEnumerable<TestReadinessPracticeSession> recentPracticeSessions,
        IEnumerable<PrerequisiteMaintenanceCheck> prerequisiteMaintenanceChecks,
        string statedStandard,
        string namedHonestyConstraint)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);
        ArgumentNullException.ThrowIfNull(recentPracticeSessions);
        ArgumentNullException.ThrowIfNull(prerequisiteMaintenanceChecks);

        if (string.IsNullOrWhiteSpace(drillDemand))
        {
            throw new ArgumentException("Target drill demand is required.", nameof(drillDemand));
        }

        PractitionerState = practitionerState;
        Branch = branch;
        Level = level;
        Drill = drill;
        DrillDemand = drillDemand;
        RecentPracticeSessions = recentPracticeSessions.ToArray();
        PrerequisiteMaintenanceChecks = prerequisiteMaintenanceChecks.ToArray();
        StatedStandard = statedStandard ?? string.Empty;
        NamedHonestyConstraint = namedHonestyConstraint ?? string.Empty;
    }

    public PractitionerState PractitionerState { get; }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public string DrillDemand { get; }

    public IReadOnlyList<TestReadinessPracticeSession> RecentPracticeSessions { get; }

    public IReadOnlyList<PrerequisiteMaintenanceCheck> PrerequisiteMaintenanceChecks { get; }

    public string StatedStandard { get; }

    public string NamedHonestyConstraint { get; }
}

public sealed record TestReadinessFailure(
    TestReadinessFailureKind Kind,
    string Detail);

public sealed record TestReadinessResult(
    bool MayTest,
    IReadOnlyList<TestReadinessFailure> Failures);

public static class TestReadinessEvaluator
{
    private const int RequiredCleanPracticeSessions = 2;

    public static TestReadinessResult Evaluate(TestReadinessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var failures = new List<TestReadinessFailure>();
        var requirements = TestReadinessPrerequisites.For(request.Branch, request.Level);

        EvaluateRequiredPrerequisites(request, requirements.RequiredLevels, failures);
        EvaluateAnyOfPrerequisites(request, requirements.AnyOfLevelGroups, failures);
        EvaluateRecentCleanPractice(request, failures);
        EvaluateStatedStandard(request, failures);
        EvaluateNamedHonestyConstraint(request, failures);

        return new TestReadinessResult(failures.Count == 0, failures);
    }

    private static void EvaluateRequiredPrerequisites(
        TestReadinessRequest request,
        IReadOnlyList<BranchLevelRequirement> requirements,
        ICollection<TestReadinessFailure> failures)
    {
        foreach (var requirement in requirements)
        {
            if (!RequirementIsSatisfied(request.PractitionerState, requirement))
            {
                failures.Add(new TestReadinessFailure(
                    TestReadinessFailureKind.PrerequisiteNotOwned,
                    RequirementDetail(requirement)));
                continue;
            }

            if (RequiresCurrentMaintenance(requirement) &&
                !MaintenanceIsCurrent(request.PrerequisiteMaintenanceChecks, requirement))
            {
                failures.Add(new TestReadinessFailure(
                    TestReadinessFailureKind.PrerequisiteMaintenanceOverdue,
                    RequirementDetail(requirement)));
            }
        }
    }

    private static void EvaluateAnyOfPrerequisites(
        TestReadinessRequest request,
        IReadOnlyList<BranchLevelRequirementGroup> requirementGroups,
        ICollection<TestReadinessFailure> failures)
    {
        foreach (var group in requirementGroups)
        {
            var satisfiedRequirements = group.Requirements
                .Where(requirement => RequirementIsSatisfied(request.PractitionerState, requirement))
                .ToArray();

            if (satisfiedRequirements.Length == 0)
            {
                failures.Add(new TestReadinessFailure(
                    TestReadinessFailureKind.PrerequisiteNotOwned,
                    string.Join(" or ", group.Requirements.Select(RequirementDetail))));
                continue;
            }

            var maintainedSatisfiedRequirementExists = satisfiedRequirements.Any(
                requirement => !RequiresCurrentMaintenance(requirement) ||
                    MaintenanceIsCurrent(request.PrerequisiteMaintenanceChecks, requirement));

            if (!maintainedSatisfiedRequirementExists)
            {
                failures.Add(new TestReadinessFailure(
                    TestReadinessFailureKind.PrerequisiteMaintenanceOverdue,
                    string.Join(" or ", satisfiedRequirements.Select(RequirementDetail))));
            }
        }
    }

    private static void EvaluateRecentCleanPractice(
        TestReadinessRequest request,
        ICollection<TestReadinessFailure> failures)
    {
        var matchingCleanPracticeCount = request.RecentPracticeSessions.Count(session =>
            session.Clean &&
            session.Branch == request.Branch &&
            session.Level == request.Level &&
            session.Drill == request.Drill &&
            SameText(session.DrillDemand, request.DrillDemand));

        if (matchingCleanPracticeCount < RequiredCleanPracticeSessions)
        {
            failures.Add(new TestReadinessFailure(
                TestReadinessFailureKind.RecentCleanPracticeMissing,
                "Two recent clean practice sessions on the same drill demand are required."));
        }
    }

    private static void EvaluateStatedStandard(
        TestReadinessRequest request,
        ICollection<TestReadinessFailure> failures)
    {
        var standard = ProgramCatalog.Standards.Single(
            item => item.Branch == request.Branch && item.Level == request.Level);

        if (!SameText(request.StatedStandard, standard.Standard))
        {
            failures.Add(new TestReadinessFailure(
                TestReadinessFailureKind.StandardNotStated,
                standard.Standard));
        }
    }

    private static void EvaluateNamedHonestyConstraint(
        TestReadinessRequest request,
        ICollection<TestReadinessFailure> failures)
    {
        var drill = ProgramCatalog.Drills.Single(item => item.Id == request.Drill);

        if (!SameText(request.NamedHonestyConstraint, drill.HonestyConstraint))
        {
            failures.Add(new TestReadinessFailure(
                TestReadinessFailureKind.HonestyConstraintNotNamed,
                drill.HonestyConstraint));
        }
    }

    private static bool RequirementIsSatisfied(
        PractitionerState practitionerState,
        BranchLevelRequirement requirement)
    {
        return BranchLevelRequirementEvaluator.IsSatisfied(practitionerState, requirement);
    }

    private static bool RequiresCurrentMaintenance(BranchLevelRequirement requirement)
    {
        return requirement.RequiredState == BranchLevelState.Owned;
    }

    private static bool MaintenanceIsCurrent(
        IReadOnlyList<PrerequisiteMaintenanceCheck> checks,
        BranchLevelRequirement requirement)
    {
        return checks.Any(check =>
            check.Branch == requirement.Branch &&
            check.Level == requirement.Level &&
            check.IsCurrent);
    }

    private static string RequirementDetail(BranchLevelRequirement requirement)
    {
        return $"{requirement.Branch} {requirement.Level} {requirement.RequiredState}";
    }

    private static bool SameText(string actual, string expected)
    {
        return string.Equals(actual.Trim(), expected.Trim(), StringComparison.Ordinal);
    }
}

internal sealed record TestReadinessPrerequisiteSet(
    IReadOnlyList<BranchLevelRequirement> RequiredLevels,
    IReadOnlyList<BranchLevelRequirementGroup> AnyOfLevelGroups);

internal static class TestReadinessPrerequisites
{
    public static TestReadinessPrerequisiteSet For(BranchCode branch, GlobalLevelId level)
    {
        if (level == GlobalLevelId.L1)
        {
            var unlock = ProgramCatalog.BranchUnlocks.Single(item => item.Branch == branch);
            return new TestReadinessPrerequisiteSet(unlock.RequiredLevels, unlock.AnyOfLevelGroups);
        }

        return (branch, level) switch
        {
            (_, GlobalLevelId.L2) => Required(Requirement(branch, GlobalLevelId.L1)),

            (BranchCode.FH, GlobalLevelId.L3) => Required(Requirement(BranchCode.FH, GlobalLevelId.L2)),
            (BranchCode.FS, GlobalLevelId.L3) => Required(
                Requirement(BranchCode.FS, GlobalLevelId.L2),
                Requirement(BranchCode.IR, GlobalLevelId.L2)),
            (BranchCode.WM, GlobalLevelId.L3) => Required(
                Requirement(BranchCode.WM, GlobalLevelId.L2),
                Requirement(BranchCode.IR, GlobalLevelId.L2)),
            (BranchCode.IR, GlobalLevelId.L3) => Required(Requirement(BranchCode.IR, GlobalLevelId.L2)),
            (BranchCode.DE, GlobalLevelId.L3) => Required(
                Requirement(BranchCode.DE, GlobalLevelId.L2),
                Requirement(BranchCode.WM, GlobalLevelId.L2)),
            (BranchCode.CO, GlobalLevelId.L3) => Required(Requirement(BranchCode.CO, GlobalLevelId.L2)),
            (BranchCode.AI, GlobalLevelId.L3) => Required(Requirement(BranchCode.AI, GlobalLevelId.L2)),
            (BranchCode.TI, GlobalLevelId.L3) => RequiredWithAnyOf(
                [Requirement(BranchCode.TI, GlobalLevelId.L2)],
                [AnyOf(Requirement(BranchCode.CO, GlobalLevelId.L2), Requirement(BranchCode.AI, GlobalLevelId.L2))]),

            (BranchCode.CO, GlobalLevelId.L4) => Required(
                Requirement(BranchCode.CO, GlobalLevelId.L3),
                Requirement(BranchCode.DE, GlobalLevelId.L4)),
            (BranchCode.AI, GlobalLevelId.L4) => Required(Requirement(BranchCode.AI, GlobalLevelId.L3)),
            (_, GlobalLevelId.L4) => Required(Requirement(branch, GlobalLevelId.L3)),

            (BranchCode.FH, GlobalLevelId.L5) => Required(
                Requirement(BranchCode.FH, GlobalLevelId.L4),
                Requirement(BranchCode.TI, GlobalLevelId.L3)),
            (BranchCode.FS, GlobalLevelId.L5) => Required(
                Requirement(BranchCode.FS, GlobalLevelId.L4),
                Requirement(BranchCode.TI, GlobalLevelId.L3)),
            (BranchCode.WM, GlobalLevelId.L5) => Required(
                Requirement(BranchCode.WM, GlobalLevelId.L4),
                Requirement(BranchCode.TI, GlobalLevelId.L3)),
            (BranchCode.IR, GlobalLevelId.L5) => Required(
                Requirement(BranchCode.IR, GlobalLevelId.L4),
                Requirement(BranchCode.TI, GlobalLevelId.L3)),
            (BranchCode.DE, GlobalLevelId.L5) => Required(
                Requirement(BranchCode.DE, GlobalLevelId.L4),
                Requirement(BranchCode.TI, GlobalLevelId.L3)),
            (BranchCode.CO, GlobalLevelId.L5) => Required(
                Requirement(BranchCode.CO, GlobalLevelId.L4),
                Requirement(BranchCode.TI, GlobalLevelId.L3)),
            (BranchCode.AI, GlobalLevelId.L5) => Required(
                Requirement(BranchCode.AI, GlobalLevelId.L4),
                Requirement(BranchCode.TI, GlobalLevelId.L3)),
            (BranchCode.TI, GlobalLevelId.L5) => Required(
                Requirement(BranchCode.TI, GlobalLevelId.L4),
                Requirement(BranchCode.CO, GlobalLevelId.L4),
                Requirement(BranchCode.AI, GlobalLevelId.L4)),

            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported branch-level readiness target."),
        };
    }

    private static TestReadinessPrerequisiteSet Required(params BranchLevelRequirement[] requirements)
    {
        return new TestReadinessPrerequisiteSet(requirements, []);
    }

    private static TestReadinessPrerequisiteSet RequiredWithAnyOf(
        IReadOnlyList<BranchLevelRequirement> requirements,
        IReadOnlyList<BranchLevelRequirementGroup> anyOfGroups)
    {
        return new TestReadinessPrerequisiteSet(requirements, anyOfGroups);
    }

    private static BranchLevelRequirementGroup AnyOf(params BranchLevelRequirement[] requirements)
    {
        return new BranchLevelRequirementGroup(requirements);
    }

    private static BranchLevelRequirement Requirement(BranchCode branch, GlobalLevelId level)
    {
        return new BranchLevelRequirement(branch, level, BranchLevelState.Owned);
    }
}
