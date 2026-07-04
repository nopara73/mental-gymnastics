namespace MentalGymnastics.Core;

public sealed class PractitionerCategoryClassificationRequest
{
    public PractitionerCategoryClassificationRequest(
        PractitionerState practitionerState,
        IEnumerable<MaintenanceCurrencyResult> maintenanceStatus,
        GlobalReviewResult? lastGlobalReview = null)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);
        ArgumentNullException.ThrowIfNull(maintenanceStatus);

        PractitionerState = practitionerState;
        MaintenanceStatus = maintenanceStatus.ToArray();
        LastGlobalReview = lastGlobalReview;
    }

    public PractitionerState PractitionerState { get; }

    public IReadOnlyList<MaintenanceCurrencyResult> MaintenanceStatus { get; }

    public GlobalReviewResult? LastGlobalReview { get; }
}

public sealed record PractitionerCategoryBlocker(
    PractitionerCategoryBlockerKind Kind,
    BranchCode? Branch,
    GlobalLevelId? Level,
    string Detail);

public sealed record PractitionerCategoryClassificationResult(
    PractitionerCategory Category,
    IReadOnlyList<PractitionerCategoryBlocker> Blockers);

public static class PractitionerCategoryClassifier
{
    public static PractitionerCategoryClassificationResult Classify(
        PractitionerCategoryClassificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var beginnerBlockers = BeginnerConditions(request).ToArray();
        if (beginnerBlockers.Length > 0)
        {
            return new PractitionerCategoryClassificationResult(
                PractitionerCategory.Beginner,
                beginnerBlockers);
        }

        var intermediateBlockers = IntermediateBlockers(request).ToArray();
        if (intermediateBlockers.Length > 0)
        {
            return new PractitionerCategoryClassificationResult(
                PractitionerCategory.Beginner,
                intermediateBlockers);
        }

        var advancedBlockers = AdvancedBlockers(request).ToArray();
        if (advancedBlockers.Length == 0)
        {
            return new PractitionerCategoryClassificationResult(
                PractitionerCategory.Advanced,
                []);
        }

        return new PractitionerCategoryClassificationResult(
            PractitionerCategory.Intermediate,
            advancedBlockers);
    }

    private static IEnumerable<PractitionerCategoryBlocker> BeginnerConditions(
        PractitionerCategoryClassificationRequest request)
    {
        foreach (var branch in FoundationalBranches())
        {
            var highestOwnedLevel = HighestOwnedLevel(request.PractitionerState, branch);
            if (highestOwnedLevel is not null && highestOwnedLevel.Value >= GlobalLevelId.L2)
            {
                continue;
            }

            yield return new PractitionerCategoryBlocker(
                PractitionerCategoryBlockerKind.FoundationalBranchBelowL2Owned,
                branch,
                highestOwnedLevel,
                $"{branch} is below L2 owned.");
        }

        if (!AdvancedBranchIsOpened(request.PractitionerState))
        {
            yield return new PractitionerCategoryBlocker(
                PractitionerCategoryBlockerKind.AdvancedBranchNotOpened,
                Branch: null,
                Level: null,
                "No advanced branch is opened.");
        }
    }

    private static IEnumerable<PractitionerCategoryBlocker> IntermediateBlockers(
        PractitionerCategoryClassificationRequest request)
    {
        foreach (var branch in FoundationalBranches())
        {
            var highestOwnedLevel = HighestOwnedLevel(request.PractitionerState, branch);
            if (highestOwnedLevel is null || highestOwnedLevel.Value < GlobalLevelId.L3)
            {
                yield return new PractitionerCategoryBlocker(
                    PractitionerCategoryBlockerKind.FoundationalBranchBelowL3Owned,
                    branch,
                    highestOwnedLevel,
                    $"{branch} is below L3 owned.");
                continue;
            }

            if (!MaintenanceIsCurrent(request.MaintenanceStatus, branch, highestOwnedLevel.Value))
            {
                yield return new PractitionerCategoryBlocker(
                    PractitionerCategoryBlockerKind.MaintenanceNotCurrent,
                    branch,
                    highestOwnedLevel,
                    $"{branch} maintenance is not current.");
            }
        }

        if (!AdvancedBranchIsOpened(request.PractitionerState))
        {
            yield return new PractitionerCategoryBlocker(
                PractitionerCategoryBlockerKind.AdvancedBranchNotOpened,
                Branch: null,
                Level: null,
                "At least one advanced branch must be opened.");
        }
    }

    private static IEnumerable<PractitionerCategoryBlocker> AdvancedBlockers(
        PractitionerCategoryClassificationRequest request)
    {
        foreach (var branch in FoundationalBranches())
        {
            var highestOwnedLevel = HighestOwnedLevel(request.PractitionerState, branch);
            if (highestOwnedLevel is null || highestOwnedLevel.Value < GlobalLevelId.L4)
            {
                yield return new PractitionerCategoryBlocker(
                    PractitionerCategoryBlockerKind.FoundationalBranchBelowL4Owned,
                    branch,
                    highestOwnedLevel,
                    $"{branch} is below L4 owned or maintained.");
                continue;
            }

            if (!MaintenanceIsCurrent(request.MaintenanceStatus, branch, highestOwnedLevel.Value))
            {
                yield return new PractitionerCategoryBlocker(
                    PractitionerCategoryBlockerKind.MaintenanceNotCurrent,
                    branch,
                    highestOwnedLevel,
                    $"{branch} maintenance is not current.");
            }
        }

        foreach (var blocker in AdvancedBranchLevelBlockers(request))
        {
            yield return blocker;
        }

        if (request.LastGlobalReview?.Passed != true)
        {
            yield return new PractitionerCategoryBlocker(
                PractitionerCategoryBlockerKind.LastGlobalReviewNotPassed,
                Branch: null,
                Level: null,
                "Advanced classification requires the last global review to have passed.");
        }
    }

    private static IEnumerable<PractitionerCategoryBlocker> AdvancedBranchLevelBlockers(
        PractitionerCategoryClassificationRequest request)
    {
        foreach (var branch in new[] { BranchCode.CO, BranchCode.AI, BranchCode.TI })
        {
            var highestOwnedLevel = HighestOwnedLevel(request.PractitionerState, branch);
            var blockerKind = branch switch
            {
                BranchCode.CO => PractitionerCategoryBlockerKind.ConceptOperationsBelowL3Owned,
                BranchCode.AI => PractitionerCategoryBlockerKind.AffectiveInterferenceBelowL3Owned,
                BranchCode.TI => PractitionerCategoryBlockerKind.TransferIntegrationBelowL3Owned,
                _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "Unsupported advanced branch."),
            };

            if (highestOwnedLevel is null || highestOwnedLevel.Value < GlobalLevelId.L3)
            {
                yield return new PractitionerCategoryBlocker(
                    blockerKind,
                    branch,
                    highestOwnedLevel,
                    $"{branch} is below L3 owned.");
                continue;
            }

            if (!MaintenanceIsCurrent(request.MaintenanceStatus, branch, highestOwnedLevel.Value))
            {
                yield return new PractitionerCategoryBlocker(
                    PractitionerCategoryBlockerKind.MaintenanceNotCurrent,
                    branch,
                    highestOwnedLevel,
                    $"{branch} maintenance is not current.");
            }
        }
    }

    private static IReadOnlyList<BranchCode> FoundationalBranches()
    {
        return ProgramCatalog.Branches
            .Where(branch => branch.Type == BranchType.Foundational)
            .Select(branch => branch.Code)
            .ToArray();
    }

    private static GlobalLevelId? HighestOwnedLevel(
        PractitionerState practitionerState,
        BranchCode branch)
    {
        var ownedRanks = practitionerState.BranchLevels
            .Where(status =>
                status.Branch == branch &&
                status.State is BranchLevelState.Owned or BranchLevelState.Maintenance)
            .Select(status => Rank(status.Level))
            .ToArray();

        if (ownedRanks.Length == 0)
        {
            return null;
        }

        return LevelFromRank(ownedRanks.Max());
    }

    private static bool AdvancedBranchIsOpened(PractitionerState practitionerState)
    {
        var advancedBranches = ProgramCatalog.Branches
            .Where(branch => branch.Type == BranchType.Advanced)
            .Select(branch => branch.Code)
            .ToArray();

        return practitionerState.BranchLevels.Any(status =>
            advancedBranches.Contains(status.Branch) &&
            status.State != BranchLevelState.Unopened);
    }

    private static bool MaintenanceIsCurrent(
        IReadOnlyList<MaintenanceCurrencyResult> maintenanceStatus,
        BranchCode branch,
        GlobalLevelId level)
    {
        return maintenanceStatus.Any(status =>
            status.Branch == branch &&
            status.OwnedLevel == level &&
            status.State == MaintenanceCurrencyState.Current);
    }

    private static int Rank(GlobalLevelId level)
    {
        return ((int)level) + 1;
    }

    private static GlobalLevelId LevelFromRank(int rank)
    {
        return (GlobalLevelId)(rank - 1);
    }
}
