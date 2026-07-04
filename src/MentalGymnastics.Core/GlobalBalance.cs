namespace MentalGymnastics.Core;

public sealed record GlobalReviewComponentScore(
    BranchCode Branch,
    bool Passed);

public sealed class GlobalReviewResult
{
    public GlobalReviewResult(
        bool passed,
        IEnumerable<GlobalReviewComponentScore> componentScores)
    {
        ArgumentNullException.ThrowIfNull(componentScores);

        Passed = passed;
        ComponentScores = componentScores.ToArray();
    }

    public bool Passed { get; }

    public IReadOnlyList<GlobalReviewComponentScore> ComponentScores { get; }
}

public sealed class GlobalBalanceAdvancementRequest
{
    public GlobalBalanceAdvancementRequest(
        BranchCode targetBranch,
        GlobalLevelId targetLevel,
        PractitionerState practitionerState,
        IEnumerable<MaintenanceCurrencyResult> maintenanceCurrency,
        GlobalReviewResult? lastGlobalReview = null)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);
        ArgumentNullException.ThrowIfNull(maintenanceCurrency);

        TargetBranch = targetBranch;
        TargetLevel = targetLevel;
        PractitionerState = practitionerState;
        MaintenanceCurrency = maintenanceCurrency.ToArray();
        LastGlobalReview = lastGlobalReview;
    }

    public BranchCode TargetBranch { get; }

    public GlobalLevelId TargetLevel { get; }

    public PractitionerState PractitionerState { get; }

    public IReadOnlyList<MaintenanceCurrencyResult> MaintenanceCurrency { get; }

    public GlobalReviewResult? LastGlobalReview { get; }
}

public sealed record GlobalBalanceIssue(
    GlobalBalanceIssueKind Kind,
    BranchCode? Branch,
    GlobalLevelId? Level,
    string Detail);

public sealed record GlobalBalanceAdvancementResult(
    BranchCode TargetBranch,
    GlobalLevelId TargetLevel,
    bool CanAdvance,
    IReadOnlyList<GlobalBalanceIssue> Issues);

public sealed record AdvancedClassificationResult(
    bool CanClassifyAsAdvanced,
    IReadOnlyList<GlobalBalanceIssue> Issues);

public static class GlobalBalanceEvaluator
{
    private const int MaximumFoundationalOwnedLevelSpread = 2;

    public static GlobalBalanceAdvancementResult EvaluateAdvancement(
        GlobalBalanceAdvancementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<GlobalBalanceIssue>();

        EvaluateFoundationalOwnedLevelSpread(request, issues);
        EvaluateAdvancedDependencyCaps(request, issues);
        EvaluateDocumentedAdvancedDecayBlockers(request, issues);
        EvaluateTransferIntegrationReviewComponents(request, issues);

        return new GlobalBalanceAdvancementResult(
            request.TargetBranch,
            request.TargetLevel,
            CanAdvance: issues.Count == 0,
            Issues: issues);
    }

    public static AdvancedClassificationResult EvaluateAdvancedClassification(
        GlobalReviewResult? lastGlobalReview)
    {
        if (lastGlobalReview?.Passed == true)
        {
            return new AdvancedClassificationResult(CanClassifyAsAdvanced: true, Issues: []);
        }

        return new AdvancedClassificationResult(
            CanClassifyAsAdvanced: false,
            [
                new GlobalBalanceIssue(
                    GlobalBalanceIssueKind.AdvancedClassificationRequiresPassedGlobalReview,
                    Branch: null,
                    Level: null,
                    "Advanced classification requires the last global review to have passed."),
            ]);
    }

    private static void EvaluateFoundationalOwnedLevelSpread(
        GlobalBalanceAdvancementRequest request,
        ICollection<GlobalBalanceIssue> issues)
    {
        var foundationalLevels = ProgramCatalog.Branches
            .Where(branch => branch.Type == BranchType.Foundational)
            .Select(branch => new
            {
                Branch = branch.Code,
                OwnedLevel = ProjectedOwnedLevelRank(request, branch.Code),
            })
            .ToArray();

        var highestOwnedLevel = foundationalLevels.Max(item => item.OwnedLevel);
        var lowestOwnedLevel = foundationalLevels.Min(item => item.OwnedLevel);
        if (highestOwnedLevel - lowestOwnedLevel <= MaximumFoundationalOwnedLevelSpread)
        {
            return;
        }

        foreach (var laggingBranch in foundationalLevels.Where(
            item => highestOwnedLevel - item.OwnedLevel > MaximumFoundationalOwnedLevelSpread))
        {
            issues.Add(new GlobalBalanceIssue(
                GlobalBalanceIssueKind.FoundationalOwnedLevelSpreadTooWide,
                laggingBranch.Branch,
                LevelFromRank(laggingBranch.OwnedLevel),
                $"Foundational branch {laggingBranch.Branch} is more than two owned levels behind the leading foundational branch."));
        }
    }

    private static int ProjectedOwnedLevelRank(
        GlobalBalanceAdvancementRequest request,
        BranchCode branch)
    {
        var currentRank = HighestCurrentOwnedLevelRank(request.PractitionerState, branch);
        if (branch == request.TargetBranch && BranchTypeFor(branch) == BranchType.Foundational)
        {
            return Math.Max(currentRank, Rank(request.TargetLevel));
        }

        return currentRank;
    }

    private static int HighestCurrentOwnedLevelRank(
        PractitionerState practitionerState,
        BranchCode branch)
    {
        return practitionerState.BranchLevels
            .Where(status =>
                status.Branch == branch &&
                status.State is BranchLevelState.Owned or BranchLevelState.Maintenance)
            .Select(status => Rank(status.Level))
            .DefaultIfEmpty(0)
            .Max();
    }

    private static void EvaluateAdvancedDependencyCaps(
        GlobalBalanceAdvancementRequest request,
        ICollection<GlobalBalanceIssue> issues)
    {
        if (BranchTypeFor(request.TargetBranch) != BranchType.Advanced)
        {
            return;
        }

        var capResult = DependencyCapEvaluator.Evaluate(
            new DependencyCapRequest(
                request.TargetBranch,
                request.PractitionerState,
                request.MaintenanceCurrency));

        foreach (var cap in capResult.Caps)
        {
            var issueKind = cap.Reason switch
            {
                DependencyCapReason.OverduePrerequisiteMaintenance =>
                    GlobalBalanceIssueKind.AdvancedPrerequisiteMaintenanceOverdue,
                DependencyCapReason.DecayedPrerequisite =>
                    GlobalBalanceIssueKind.AdvancedPrerequisiteDecayed,
                _ => throw new ArgumentOutOfRangeException(nameof(cap.Reason), cap.Reason, "Unsupported dependency cap reason."),
            };

            issues.Add(new GlobalBalanceIssue(
                issueKind,
                cap.PrerequisiteBranch,
                cap.PrerequisiteLevel,
                cap.Detail));
        }
    }

    private static void EvaluateDocumentedAdvancedDecayBlockers(
        GlobalBalanceAdvancementRequest request,
        ICollection<GlobalBalanceIssue> issues)
    {
        if (request.TargetBranch == BranchCode.CO)
        {
            AddDecayedBranchIssues(
                request,
                [BranchCode.WM, BranchCode.DE],
                GlobalBalanceIssueKind.ConceptOperationsPrerequisiteDecayed,
                issues);
        }

        if (request.TargetBranch == BranchCode.AI)
        {
            AddDecayedBranchIssues(
                request,
                [BranchCode.FH, BranchCode.FS, BranchCode.IR],
                GlobalBalanceIssueKind.AffectiveInterferencePrerequisiteDecayed,
                issues);
        }
    }

    private static void AddDecayedBranchIssues(
        GlobalBalanceAdvancementRequest request,
        IReadOnlyList<BranchCode> branches,
        GlobalBalanceIssueKind issueKind,
        ICollection<GlobalBalanceIssue> issues)
    {
        foreach (var branch in branches.Where(branch => BranchHasDecayedLevel(request.PractitionerState, branch)))
        {
            issues.Add(new GlobalBalanceIssue(
                issueKind,
                branch,
                Level: null,
                $"{request.TargetBranch} cannot advance while {branch} is decayed."));
        }
    }

    private static bool BranchHasDecayedLevel(
        PractitionerState practitionerState,
        BranchCode branch)
    {
        return practitionerState.BranchLevels.Any(status =>
            status.Branch == branch &&
            status.State == BranchLevelState.Decayed);
    }

    private static void EvaluateTransferIntegrationReviewComponents(
        GlobalBalanceAdvancementRequest request,
        ICollection<GlobalBalanceIssue> issues)
    {
        if (request.TargetBranch != BranchCode.TI || request.LastGlobalReview is null)
        {
            return;
        }

        foreach (var failedComponent in request.LastGlobalReview.ComponentScores.Where(score => !score.Passed))
        {
            issues.Add(new GlobalBalanceIssue(
                GlobalBalanceIssueKind.TransferIntegrationComponentFailedLastGlobalReview,
                failedComponent.Branch,
                Level: null,
                $"TI cannot advance because {failedComponent.Branch} scored below passing in the last global review."));
        }
    }

    private static BranchType BranchTypeFor(BranchCode branch)
    {
        return ProgramCatalog.Branches.Single(definition => definition.Code == branch).Type;
    }

    private static int Rank(GlobalLevelId level)
    {
        return ((int)level) + 1;
    }

    private static GlobalLevelId? LevelFromRank(int rank)
    {
        if (rank == 0)
        {
            return null;
        }

        return (GlobalLevelId)(rank - 1);
    }
}
