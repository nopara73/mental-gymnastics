namespace MentalGymnastics.Core;

public sealed class RecoverySetResultEvidence
{
    public RecoverySetResultEvidence(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        int setNumber,
        bool failedFromOverload)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setNumber);

        Branch = branch;
        Level = level;
        Date = date;
        SetNumber = setNumber;
        FailedFromOverload = failedFromOverload;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public TrainingDate Date { get; }

    public int SetNumber { get; }

    public bool FailedFromOverload { get; }
}

public sealed class RecoveryErrorTrendEvidence
{
    public RecoveryErrorTrendEvidence(
        BranchCode branch,
        GlobalLevelId level,
        int previousErrorCount,
        int currentErrorCount,
        bool loadUnchanged)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(previousErrorCount);
        ArgumentOutOfRangeException.ThrowIfNegative(currentErrorCount);

        Branch = branch;
        Level = level;
        PreviousErrorCount = previousErrorCount;
        CurrentErrorCount = currentErrorCount;
        LoadUnchanged = loadUnchanged;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public int PreviousErrorCount { get; }

    public int CurrentErrorCount { get; }

    public bool LoadUnchanged { get; }
}

public sealed record RecoveryHonestyConstraintEvidence(
    BranchCode Branch,
    GlobalLevelId Level,
    bool Broken);

public sealed class AdjacentBranchDecayEvidence
{
    public AdjacentBranchDecayEvidence(
        BranchCode targetBranch,
        BranchCode adjacentBranch,
        bool decayed)
    {
        TargetBranch = targetBranch;
        AdjacentBranch = adjacentBranch;
        Decayed = decayed;
    }

    public BranchCode TargetBranch { get; }

    public BranchCode AdjacentBranch { get; }

    public bool Decayed { get; }
}

public sealed class RecentHighIntensityTestEvidence
{
    public RecentHighIntensityTestEvidence(
        BranchCode branch,
        GlobalLevelId level,
        TrainingIntensityKind intensity,
        int hoursBeforeSession)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hoursBeforeSession);

        Branch = branch;
        Level = level;
        Intensity = intensity;
        HoursBeforeSession = hoursBeforeSession;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public TrainingIntensityKind Intensity { get; }

    public int HoursBeforeSession { get; }
}

public sealed class RecoveryDecisionRequest
{
    public RecoveryDecisionRequest(
        BranchCode branch,
        GlobalLevelId level,
        LoadVariableKind loadVariableToReduce,
        string coreConstraint,
        IEnumerable<RecoverySetResultEvidence> setResults,
        IEnumerable<RecoveryErrorTrendEvidence> errorTrends,
        IEnumerable<RecoveryHonestyConstraintEvidence> honestyConstraintEvidence,
        IEnumerable<AdjacentBranchDecayEvidence> adjacentBranchDecayEvidence,
        IEnumerable<RecentHighIntensityTestEvidence> recentHighIntensityTests,
        IEnumerable<string> subjectiveNotes)
    {
        ArgumentNullException.ThrowIfNull(setResults);
        ArgumentNullException.ThrowIfNull(errorTrends);
        ArgumentNullException.ThrowIfNull(honestyConstraintEvidence);
        ArgumentNullException.ThrowIfNull(adjacentBranchDecayEvidence);
        ArgumentNullException.ThrowIfNull(recentHighIntensityTests);
        ArgumentNullException.ThrowIfNull(subjectiveNotes);

        if (string.IsNullOrWhiteSpace(coreConstraint))
        {
            throw new ArgumentException("Recovery must preserve a named core constraint.", nameof(coreConstraint));
        }

        Branch = branch;
        Level = level;
        LoadVariableToReduce = loadVariableToReduce;
        CoreConstraint = coreConstraint;
        SetResults = setResults.ToArray();
        ErrorTrends = errorTrends.ToArray();
        HonestyConstraintEvidence = honestyConstraintEvidence.ToArray();
        AdjacentBranchDecayEvidence = adjacentBranchDecayEvidence.ToArray();
        RecentHighIntensityTests = recentHighIntensityTests.ToArray();
        SubjectiveNotes = subjectiveNotes.ToArray();
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public LoadVariableKind LoadVariableToReduce { get; }

    public string CoreConstraint { get; }

    public IReadOnlyList<RecoverySetResultEvidence> SetResults { get; }

    public IReadOnlyList<RecoveryErrorTrendEvidence> ErrorTrends { get; }

    public IReadOnlyList<RecoveryHonestyConstraintEvidence> HonestyConstraintEvidence { get; }

    public IReadOnlyList<AdjacentBranchDecayEvidence> AdjacentBranchDecayEvidence { get; }

    public IReadOnlyList<RecentHighIntensityTestEvidence> RecentHighIntensityTests { get; }

    public IReadOnlyList<string> SubjectiveNotes { get; }
}

public sealed record RecoverySessionPrescription(
    LoadVariableKind ReducedLoadVariable,
    int LoadReductionLevels,
    string CoreConstraint,
    int EvidenceArtifactsToRecord,
    bool AdvancementTestingAllowed);

public sealed record RecoveryDecisionResult(
    BranchCode Branch,
    GlobalLevelId Level,
    bool ShouldRecover,
    IReadOnlyList<RecoveryTriggerKind> Triggers,
    RecoverySessionPrescription? Prescription);

public static class RecoveryDecisionEvaluator
{
    public static RecoveryDecisionResult Evaluate(RecoveryDecisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var triggers = BuildTriggers(request).Distinct().ToArray();

        return new RecoveryDecisionResult(
            request.Branch,
            request.Level,
            ShouldRecover: triggers.Length > 0,
            triggers,
            triggers.Length > 0 ? PrescriptionFor(request) : null);
    }

    private static IEnumerable<RecoveryTriggerKind> BuildTriggers(RecoveryDecisionRequest request)
    {
        if (HasTwoConsecutiveOverloadSetFailures(request))
        {
            yield return RecoveryTriggerKind.TwoConsecutiveOverloadSetFailures;
        }

        if (request.ErrorTrends.Any(IsRisingErrorsWithUnchangedLoad(request)))
        {
            yield return RecoveryTriggerKind.ErrorCountRisesWithUnchangedLoad;
        }

        if (request.HonestyConstraintEvidence.Any(IsBrokenHonestyConstraint(request)))
        {
            yield return RecoveryTriggerKind.HonestyConstraintBroken;
        }

        if (HasBroadAdjacentDecay(request))
        {
            yield return RecoveryTriggerKind.AdjacentBranchesShowBroadDecay;
        }

        if (request.RecentHighIntensityTests.Any(IsSameBranchHighIntensityWithinTwentyFourHours(request)))
        {
            yield return RecoveryTriggerKind.SameBranchHighIntensityTestWithin24Hours;
        }
    }

    private static bool HasTwoConsecutiveOverloadSetFailures(RecoveryDecisionRequest request)
    {
        var relevantSets = request.SetResults
            .Where(result => result.Branch == request.Branch && result.Level == request.Level)
            .OrderBy(result => result.Date.ToDayNumber())
            .ThenBy(result => result.SetNumber)
            .ToArray();

        for (var index = 1; index < relevantSets.Length; index++)
        {
            var previous = relevantSets[index - 1];
            var current = relevantSets[index];

            if (previous.Date == current.Date &&
                previous.SetNumber + 1 == current.SetNumber &&
                previous.FailedFromOverload &&
                current.FailedFromOverload)
            {
                return true;
            }
        }

        return false;
    }

    private static Func<RecoveryErrorTrendEvidence, bool> IsRisingErrorsWithUnchangedLoad(
        RecoveryDecisionRequest request)
    {
        return evidence =>
            evidence.Branch == request.Branch &&
            evidence.Level == request.Level &&
            evidence.LoadUnchanged &&
            evidence.CurrentErrorCount > evidence.PreviousErrorCount;
    }

    private static Func<RecoveryHonestyConstraintEvidence, bool> IsBrokenHonestyConstraint(
        RecoveryDecisionRequest request)
    {
        return evidence =>
            evidence.Branch == request.Branch &&
            evidence.Level == request.Level &&
            evidence.Broken;
    }

    private static bool HasBroadAdjacentDecay(RecoveryDecisionRequest request)
    {
        return request.AdjacentBranchDecayEvidence
            .Where(evidence => evidence.TargetBranch == request.Branch && evidence.Decayed)
            .Select(evidence => evidence.AdjacentBranch)
            .Distinct()
            .Count() >= 2;
    }

    private static Func<RecentHighIntensityTestEvidence, bool> IsSameBranchHighIntensityWithinTwentyFourHours(
        RecoveryDecisionRequest request)
    {
        return evidence =>
            evidence.Branch == request.Branch &&
            evidence.Level == request.Level &&
            evidence.Intensity == TrainingIntensityKind.High &&
            evidence.HoursBeforeSession <= 24;
    }

    private static RecoverySessionPrescription PrescriptionFor(RecoveryDecisionRequest request)
    {
        return new RecoverySessionPrescription(
            request.LoadVariableToReduce,
            LoadReductionLevels: 1,
            request.CoreConstraint,
            EvidenceArtifactsToRecord: 1,
            AdvancementTestingAllowed: false);
    }
}

public sealed class DeloadBranchWeekEvidence
{
    public DeloadBranchWeekEvidence(
        BranchCode branch,
        TrainingDate weekStart,
        bool overloadObserved,
        bool decayObserved)
    {
        Branch = branch;
        WeekStart = weekStart;
        OverloadObserved = overloadObserved;
        DecayObserved = decayObserved;
    }

    public BranchCode Branch { get; }

    public TrainingDate WeekStart { get; }

    public bool OverloadObserved { get; }

    public bool DecayObserved { get; }
}

public sealed class DeloadDecisionRequest
{
    public DeloadDecisionRequest(
        TrainingDate weekStart,
        IEnumerable<DeloadBranchWeekEvidence> branchEvidence,
        IEnumerable<string> subjectiveNotes)
    {
        ArgumentNullException.ThrowIfNull(branchEvidence);
        ArgumentNullException.ThrowIfNull(subjectiveNotes);

        WeekStart = weekStart;
        BranchEvidence = branchEvidence.ToArray();
        SubjectiveNotes = subjectiveNotes.ToArray();
    }

    public TrainingDate WeekStart { get; }

    public IReadOnlyList<DeloadBranchWeekEvidence> BranchEvidence { get; }

    public IReadOnlyList<string> SubjectiveNotes { get; }
}

public sealed record DeloadWeekPrescription(
    int WorkingSetReductionNumerator,
    int WorkingSetReductionDenominator,
    bool AdvancementTestingAllowed,
    bool MaintenanceChecksRemain);

public sealed record DeloadDecisionResult(
    TrainingDate WeekStart,
    bool ShouldDeload,
    IReadOnlyList<DeloadTriggerKind> Triggers,
    DeloadWeekPrescription? Prescription);

public static class DeloadDecisionEvaluator
{
    public static DeloadDecisionResult Evaluate(DeloadDecisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var affectedBranchCount = request.BranchEvidence
            .Where(evidence => evidence.WeekStart == request.WeekStart)
            .Where(evidence => evidence.OverloadObserved || evidence.DecayObserved)
            .Select(evidence => evidence.Branch)
            .Distinct()
            .Count();

        var triggers = affectedBranchCount >= 2
            ? new[] { DeloadTriggerKind.TwoOrMoreBranchesShowOverloadOrDecayInSameWeek }
            : [];

        return new DeloadDecisionResult(
            request.WeekStart,
            ShouldDeload: triggers.Length > 0,
            triggers,
            triggers.Length > 0 ? Prescription() : null);
    }

    private static DeloadWeekPrescription Prescription()
    {
        return new DeloadWeekPrescription(
            WorkingSetReductionNumerator: 1,
            WorkingSetReductionDenominator: 3,
            AdvancementTestingAllowed: false,
            MaintenanceChecksRemain: true);
    }
}
