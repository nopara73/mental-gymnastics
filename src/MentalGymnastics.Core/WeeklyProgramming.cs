namespace MentalGymnastics.Core;

public sealed class WeeklyProgrammingRequest
{
    public WeeklyProgrammingRequest(
        PractitionerCategoryClassificationResult categoryClassification,
        IEnumerable<MaintenanceCurrencyResult> maintenanceCurrency,
        IEnumerable<GlobalReviewDecision> globalReviewDecisions,
        bool recoveryRequired,
        BranchCode selectedFoundationalLoadBranch,
        BranchCode weakestFoundationalBranch,
        BranchCode selectedAdvancedBranch,
        BranchCode prerequisiteSupportBranch,
        BranchCode eligibleAdvancementBranch,
        BranchCode bottleneckBranch,
        BranchCode recentlyPassedBranch,
        BranchCode transferBranch)
    {
        ArgumentNullException.ThrowIfNull(categoryClassification);
        ArgumentNullException.ThrowIfNull(maintenanceCurrency);
        ArgumentNullException.ThrowIfNull(globalReviewDecisions);

        CategoryClassification = categoryClassification;
        MaintenanceCurrency = maintenanceCurrency.ToArray();
        GlobalReviewDecisions = globalReviewDecisions.ToArray();
        RecoveryRequired = recoveryRequired;
        SelectedFoundationalLoadBranch = selectedFoundationalLoadBranch;
        WeakestFoundationalBranch = weakestFoundationalBranch;
        SelectedAdvancedBranch = selectedAdvancedBranch;
        PrerequisiteSupportBranch = prerequisiteSupportBranch;
        EligibleAdvancementBranch = eligibleAdvancementBranch;
        BottleneckBranch = bottleneckBranch;
        RecentlyPassedBranch = recentlyPassedBranch;
        TransferBranch = transferBranch;
    }

    public PractitionerCategoryClassificationResult CategoryClassification { get; }

    public IReadOnlyList<MaintenanceCurrencyResult> MaintenanceCurrency { get; }

    public IReadOnlyList<GlobalReviewDecision> GlobalReviewDecisions { get; }

    public bool RecoveryRequired { get; }

    public BranchCode SelectedFoundationalLoadBranch { get; }

    public BranchCode WeakestFoundationalBranch { get; }

    public BranchCode SelectedAdvancedBranch { get; }

    public BranchCode PrerequisiteSupportBranch { get; }

    public BranchCode EligibleAdvancementBranch { get; }

    public BranchCode BottleneckBranch { get; }

    public BranchCode RecentlyPassedBranch { get; }

    public BranchCode TransferBranch { get; }
}

public sealed record WeeklyProgrammingConstraint(
    WeeklyProgrammingConstraintKind Kind,
    BranchCode? Branch,
    string Detail);

public sealed class WeeklyPlanDay
{
    public WeeklyPlanDay(
        int dayNumber,
        WeeklySessionKind session,
        IEnumerable<BranchCode> branchEmphasis)
    {
        if (dayNumber is < 1 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(dayNumber), "Weekly plan day must be between 1 and 7.");
        }

        ArgumentNullException.ThrowIfNull(branchEmphasis);

        DayNumber = dayNumber;
        Session = session;
        BranchEmphasis = branchEmphasis.Distinct().ToArray();
    }

    public int DayNumber { get; }

    public WeeklySessionKind Session { get; }

    public IReadOnlyList<BranchCode> BranchEmphasis { get; }

    public bool IsAdvancementWork => WeeklyProgrammingPlanner.IsAdvancementSession(Session);

    public WeeklyPlanDay WithSession(
        WeeklySessionKind session,
        IEnumerable<BranchCode> branchEmphasis)
    {
        return new WeeklyPlanDay(DayNumber, session, branchEmphasis);
    }
}

public sealed record WeeklyPlan(
    PractitionerCategory PractitionerCategory,
    IReadOnlyList<WeeklyPlanDay> Days,
    IReadOnlyList<WeeklyProgrammingConstraint> Constraints,
    bool AdvancementWorkAllowed);

public static class WeeklyProgrammingPlanner
{
    private static readonly IReadOnlyList<BranchCode> FoundationalBranches =
    [
        BranchCode.FH,
        BranchCode.FS,
        BranchCode.WM,
        BranchCode.IR,
        BranchCode.DE,
    ];

    public static WeeklyPlan Generate(WeeklyProgrammingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var constraints = BuildConstraints(request).ToArray();
        var advancementWorkAllowed = constraints.All(constraint =>
            constraint.Kind == WeeklyProgrammingConstraintKind.BeginnerFixedTemplateRequired);

        var days = TemplateFor(request);
        if (!advancementWorkAllowed)
        {
            days = ReplaceAdvancementWork(days, request, constraints);
        }

        return new WeeklyPlan(
            request.CategoryClassification.Category,
            days,
            constraints,
            advancementWorkAllowed);
    }

    internal static bool IsAdvancementSession(WeeklySessionKind session)
    {
        return session is
            WeeklySessionKind.Load or
            WeeklySessionKind.TestOrStabilization or
            WeeklySessionKind.TransferOrStabilization or
            WeeklySessionKind.Transfer or
            WeeklySessionKind.Stabilization or
            WeeklySessionKind.RecoveryOrRetest;
    }

    private static IEnumerable<WeeklyProgrammingConstraint> BuildConstraints(
        WeeklyProgrammingRequest request)
    {
        if (request.CategoryClassification.Category == PractitionerCategory.Beginner)
        {
            yield return new WeeklyProgrammingConstraint(
                WeeklyProgrammingConstraintKind.BeginnerFixedTemplateRequired,
                Branch: null,
                "Beginners follow the fixed template until all foundational branches reach L2 owned and advanced work is opened.");
        }

        foreach (var maintenance in request.MaintenanceCurrency.Where(
            currency => currency.State is MaintenanceCurrencyState.Due or MaintenanceCurrencyState.Failed))
        {
            yield return new WeeklyProgrammingConstraint(
                WeeklyProgrammingConstraintKind.MaintenanceNotCurrent,
                maintenance.Branch,
                $"{maintenance.Branch} maintenance is not current.");
        }

        if (request.RecoveryRequired)
        {
            yield return new WeeklyProgrammingConstraint(
                WeeklyProgrammingConstraintKind.RecoveryRequired,
                Branch: null,
                "Recovery is required; advancement work is suspended.");
        }

        if (request.GlobalReviewDecisions.Any(
            decision => decision.Kind == GlobalReviewDecisionKind.PauseTestsForDeload))
        {
            yield return new WeeklyProgrammingConstraint(
                WeeklyProgrammingConstraintKind.AdvancementTestingSuspended,
                Branch: null,
                "Global review paused tests for deload.");
        }
    }

    private static IReadOnlyList<WeeklyPlanDay> TemplateFor(WeeklyProgrammingRequest request)
    {
        return request.CategoryClassification.Category switch
        {
            PractitionerCategory.Beginner => BeginnerTemplate(request),
            PractitionerCategory.Intermediate => IntermediateTemplate(request),
            PractitionerCategory.Advanced => AdvancedTemplate(request),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.CategoryClassification.Category,
                "Unsupported practitioner category."),
        };
    }

    private static IReadOnlyList<WeeklyPlanDay> BeginnerTemplate(WeeklyProgrammingRequest request)
    {
        return
        [
            Day(1, WeeklySessionKind.Practice, BranchCode.FH, BranchCode.FS, BranchCode.WM),
            Day(2, WeeklySessionKind.Practice, BranchCode.FH, BranchCode.IR, BranchCode.DE),
            Day(3, WeeklySessionKind.RecoveryOrLightMaintenance, BranchCode.FH, request.WeakestFoundationalBranch),
            Day(4, WeeklySessionKind.Load, request.SelectedFoundationalLoadBranch, BranchCode.FH),
            Day(5, WeeklySessionKind.Practice, BranchCode.WM, BranchCode.IR, BranchCode.DE),
            Day(6, WeeklySessionKind.TestOrStabilization, request.EligibleAdvancementBranch),
            Day(7, WeeklySessionKind.OffOrRecovery),
        ];
    }

    private static IReadOnlyList<WeeklyPlanDay> IntermediateTemplate(WeeklyProgrammingRequest request)
    {
        return
        [
            Day(1, WeeklySessionKind.Practice, request.WeakestFoundationalBranch, request.SelectedAdvancedBranch),
            Day(2, WeeklySessionKind.Load, request.SelectedAdvancedBranch),
            Day(3, WeeklySessionKind.Maintenance, FoundationalBranches),
            Day(4, WeeklySessionKind.Practice, request.SelectedAdvancedBranch, request.PrerequisiteSupportBranch),
            Day(5, WeeklySessionKind.TransferOrStabilization, request.SelectedAdvancedBranch),
            Day(6, WeeklySessionKind.RecoveryOrRetest),
            Day(7, WeeklySessionKind.Off),
        ];
    }

    private static IReadOnlyList<WeeklyPlanDay> AdvancedTemplate(WeeklyProgrammingRequest request)
    {
        return
        [
            Day(1, WeeklySessionKind.Maintenance, FoundationalBranches),
            Day(2, WeeklySessionKind.Load, request.SelectedAdvancedBranch),
            Day(3, WeeklySessionKind.Practice, request.BottleneckBranch),
            Day(4, WeeklySessionKind.Transfer, request.TransferBranch),
            Day(5, WeeklySessionKind.Stabilization, request.RecentlyPassedBranch),
            Day(6, WeeklySessionKind.RecoveryOrRetest),
            Day(7, WeeklySessionKind.Off),
        ];
    }

    private static IReadOnlyList<WeeklyPlanDay> ReplaceAdvancementWork(
        IReadOnlyList<WeeklyPlanDay> days,
        WeeklyProgrammingRequest request,
        IReadOnlyList<WeeklyProgrammingConstraint> constraints)
    {
        var maintenanceBranches = constraints
            .Where(constraint => constraint.Kind == WeeklyProgrammingConstraintKind.MaintenanceNotCurrent)
            .Select(constraint => constraint.Branch)
            .OfType<BranchCode>()
            .Distinct()
            .ToArray();

        var replacementSession = request.RecoveryRequired
            ? WeeklySessionKind.Recovery
            : WeeklySessionKind.Maintenance;

        var replacementBranches = request.RecoveryRequired
            ? Array.Empty<BranchCode>()
            : maintenanceBranches;

        if (replacementSession == WeeklySessionKind.Maintenance && replacementBranches.Length == 0)
        {
            replacementSession = WeeklySessionKind.Recovery;
        }

        return days
            .Select(day => day.IsAdvancementWork
                ? day.WithSession(replacementSession, replacementBranches)
                : day)
            .ToArray();
    }

    private static WeeklyPlanDay Day(
        int dayNumber,
        WeeklySessionKind session,
        params BranchCode[] branchEmphasis)
    {
        return new WeeklyPlanDay(dayNumber, session, branchEmphasis);
    }

    private static WeeklyPlanDay Day(
        int dayNumber,
        WeeklySessionKind session,
        IReadOnlyList<BranchCode> branchEmphasis)
    {
        return new WeeklyPlanDay(dayNumber, session, branchEmphasis);
    }
}
