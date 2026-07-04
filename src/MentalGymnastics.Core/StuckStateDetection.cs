namespace MentalGymnastics.Core;

public sealed class BranchGateFailureRecord
{
    public BranchGateFailureRecord(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date)
    {
        Branch = branch;
        Level = level;
        Date = date;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public TrainingDate Date { get; }
}

public sealed class RegressionSessionResultRecord
{
    public RegressionSessionResultRecord(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        string? criticalConstraint,
        bool passed)
    {
        Branch = branch;
        Level = level;
        Date = date;
        CriticalConstraint = criticalConstraint;
        Passed = passed;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public TrainingDate Date { get; }

    public string? CriticalConstraint { get; }

    public bool Passed { get; }
}

public sealed class PrerequisiteDecayDuringTrainingRecord
{
    public PrerequisiteDecayDuringTrainingRecord(
        BranchCode prerequisiteBranch,
        BranchCode dependentBranch,
        GlobalLevelId prerequisiteLevel,
        TrainingDate date,
        bool dependentBranchWasTraining)
    {
        PrerequisiteBranch = prerequisiteBranch;
        DependentBranch = dependentBranch;
        PrerequisiteLevel = prerequisiteLevel;
        Date = date;
        DependentBranchWasTraining = dependentBranchWasTraining;
    }

    public BranchCode PrerequisiteBranch { get; }

    public BranchCode DependentBranch { get; }

    public GlobalLevelId PrerequisiteLevel { get; }

    public TrainingDate Date { get; }

    public bool DependentBranchWasTraining { get; }
}

public sealed class GlobalReviewBottleneckRecord
{
    public GlobalReviewBottleneckRecord(
        TrainingDate date,
        BottleneckKind bottleneck,
        bool improvedEvidence)
    {
        Date = date;
        Bottleneck = bottleneck;
        ImprovedEvidence = improvedEvidence;
    }

    public TrainingDate Date { get; }

    public BottleneckKind Bottleneck { get; }

    public bool ImprovedEvidence { get; }
}

public sealed class TransferReadinessRecord
{
    public TransferReadinessRecord(
        BranchCode branch,
        GlobalLevelId level,
        bool isolatedDrillPassed)
    {
        Branch = branch;
        Level = level;
        IsolatedDrillPassed = isolatedDrillPassed;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public bool IsolatedDrillPassed { get; }
}

public sealed class TransferTestFailureRecord
{
    public TransferTestFailureRecord(
        BranchCode branch,
        GlobalLevelId level,
        string transferTask,
        TrainingDate date)
    {
        if (string.IsNullOrWhiteSpace(transferTask))
        {
            throw new ArgumentException("Transfer task must be named.", nameof(transferTask));
        }

        Branch = branch;
        Level = level;
        TransferTask = transferTask;
        Date = date;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public string TransferTask { get; }

    public TrainingDate Date { get; }
}

public sealed class StuckStateHistory
{
    public StuckStateHistory(
        IEnumerable<BranchGateFailureRecord> gateFailures,
        IEnumerable<RegressionSessionResultRecord> regressionSessions,
        IEnumerable<PrerequisiteDecayDuringTrainingRecord> prerequisiteDecayEvents,
        IEnumerable<GlobalReviewBottleneckRecord> globalReviews,
        IEnumerable<TransferReadinessRecord> transferReadiness,
        IEnumerable<TransferTestFailureRecord> transferFailures,
        IEnumerable<string> subjectiveFrustrationNotes)
    {
        ArgumentNullException.ThrowIfNull(gateFailures);
        ArgumentNullException.ThrowIfNull(regressionSessions);
        ArgumentNullException.ThrowIfNull(prerequisiteDecayEvents);
        ArgumentNullException.ThrowIfNull(globalReviews);
        ArgumentNullException.ThrowIfNull(transferReadiness);
        ArgumentNullException.ThrowIfNull(transferFailures);
        ArgumentNullException.ThrowIfNull(subjectiveFrustrationNotes);

        GateFailures = gateFailures.ToArray();
        RegressionSessions = regressionSessions.ToArray();
        PrerequisiteDecayEvents = prerequisiteDecayEvents.ToArray();
        GlobalReviews = globalReviews.ToArray();
        TransferReadiness = transferReadiness.ToArray();
        TransferFailures = transferFailures.ToArray();
        SubjectiveFrustrationNotes = subjectiveFrustrationNotes.ToArray();
    }

    public IReadOnlyList<BranchGateFailureRecord> GateFailures { get; }

    public IReadOnlyList<RegressionSessionResultRecord> RegressionSessions { get; }

    public IReadOnlyList<PrerequisiteDecayDuringTrainingRecord> PrerequisiteDecayEvents { get; }

    public IReadOnlyList<GlobalReviewBottleneckRecord> GlobalReviews { get; }

    public IReadOnlyList<TransferReadinessRecord> TransferReadiness { get; }

    public IReadOnlyList<TransferTestFailureRecord> TransferFailures { get; }

    public IReadOnlyList<string> SubjectiveFrustrationNotes { get; }
}

public sealed record StuckStateCondition(
    StuckStateConditionKind Kind,
    BranchCode? Branch,
    GlobalLevelId? Level,
    string? Constraint,
    BranchCode? PrerequisiteBranch,
    BottleneckKind? Bottleneck,
    string Detail);

public sealed record StuckStateDetectionResult(
    bool IsStuck,
    IReadOnlyList<StuckStateCondition> Conditions);

public static class StuckStateDetector
{
    private const int RequiredGateFailures = 3;
    private const int RequiredGateFailureSpanDays = 10;
    private const int RequiredRepeatedPrerequisiteDecays = 2;
    private const int RequiredTransferFailures = 2;

    public static StuckStateDetectionResult Evaluate(StuckStateHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var conditions = new List<StuckStateCondition>();
        DetectRepeatedGateFailures(history, conditions);
        DetectConsecutiveRegressionConstraintFailures(history, conditions);
        DetectRepeatedPrerequisiteDecay(history, conditions);
        DetectRepeatedGlobalReviewBottleneck(history, conditions);
        DetectTransferStuckPoint(history, conditions);

        return new StuckStateDetectionResult(conditions.Count > 0, conditions);
    }

    private static void DetectRepeatedGateFailures(
        StuckStateHistory history,
        ICollection<StuckStateCondition> conditions)
    {
        foreach (var group in history.GateFailures.GroupBy(failure => (failure.Branch, failure.Level)))
        {
            var orderedFailures = group.OrderBy(failure => failure.Date.ToDayNumber()).ToArray();
            if (orderedFailures.Length < RequiredGateFailures)
            {
                continue;
            }

            for (var startIndex = 0; startIndex <= orderedFailures.Length - RequiredGateFailures; startIndex++)
            {
                var endIndex = startIndex + RequiredGateFailures - 1;
                if (orderedFailures[startIndex].Date.DaysUntil(orderedFailures[endIndex].Date) < RequiredGateFailureSpanDays)
                {
                    continue;
                }

                conditions.Add(new StuckStateCondition(
                    StuckStateConditionKind.SameBranchGateFailedThreeTimesAcrossTenDays,
                    group.Key.Branch,
                    group.Key.Level,
                    Constraint: null,
                    PrerequisiteBranch: null,
                    Bottleneck: null,
                    $"{group.Key.Branch} {group.Key.Level} gate failed three times across at least 10 calendar days."));
                break;
            }
        }
    }

    private static void DetectConsecutiveRegressionConstraintFailures(
        StuckStateHistory history,
        ICollection<StuckStateCondition> conditions)
    {
        foreach (var group in history.RegressionSessions.GroupBy(session => (session.Branch, session.Level)))
        {
            var orderedSessions = group.OrderBy(session => session.Date.ToDayNumber()).ToArray();
            for (var index = 1; index < orderedSessions.Length; index++)
            {
                var previous = orderedSessions[index - 1];
                var current = orderedSessions[index];
                if (previous.Passed ||
                    current.Passed ||
                    string.IsNullOrWhiteSpace(previous.CriticalConstraint) ||
                    !string.Equals(previous.CriticalConstraint, current.CriticalConstraint, StringComparison.Ordinal))
                {
                    continue;
                }

                conditions.Add(new StuckStateCondition(
                    StuckStateConditionKind.SameCriticalConstraintFailedInConsecutiveRegressions,
                    group.Key.Branch,
                    group.Key.Level,
                    current.CriticalConstraint,
                    PrerequisiteBranch: null,
                    Bottleneck: null,
                    $"{group.Key.Branch} {group.Key.Level} failed the same critical constraint in two consecutive regression sessions."));
                break;
            }
        }
    }

    private static void DetectRepeatedPrerequisiteDecay(
        StuckStateHistory history,
        ICollection<StuckStateCondition> conditions)
    {
        var repeatedDecays = history.PrerequisiteDecayEvents
            .Where(decay => decay.DependentBranchWasTraining)
            .GroupBy(decay => (decay.PrerequisiteBranch, decay.DependentBranch, decay.PrerequisiteLevel))
            .Where(group => group.Count() >= RequiredRepeatedPrerequisiteDecays);

        foreach (var decayGroup in repeatedDecays)
        {
            conditions.Add(new StuckStateCondition(
                StuckStateConditionKind.PrerequisiteRepeatedlyDecayedWhileDependentTraining,
                decayGroup.Key.DependentBranch,
                Level: null,
                Constraint: null,
                decayGroup.Key.PrerequisiteBranch,
                Bottleneck: null,
                $"{decayGroup.Key.PrerequisiteBranch} repeatedly decayed while {decayGroup.Key.DependentBranch} was training."));
        }
    }

    private static void DetectRepeatedGlobalReviewBottleneck(
        StuckStateHistory history,
        ICollection<StuckStateCondition> conditions)
    {
        var orderedReviews = history.GlobalReviews.OrderBy(review => review.Date.ToDayNumber()).ToArray();
        for (var index = 1; index < orderedReviews.Length; index++)
        {
            var previous = orderedReviews[index - 1];
            var current = orderedReviews[index];
            if (previous.Bottleneck != current.Bottleneck ||
                previous.ImprovedEvidence ||
                current.ImprovedEvidence)
            {
                continue;
            }

            conditions.Add(new StuckStateCondition(
                StuckStateConditionKind.SameBottleneckInTwoGlobalReviewsWithoutImprovement,
                Branch: null,
                Level: null,
                Constraint: null,
                PrerequisiteBranch: null,
                current.Bottleneck,
                $"Two global reviews named {current.Bottleneck} without improvement in evidence."));
            break;
        }
    }

    private static void DetectTransferStuckPoint(
        StuckStateHistory history,
        ICollection<StuckStateCondition> conditions)
    {
        foreach (var readiness in history.TransferReadiness.Where(item => item.IsolatedDrillPassed))
        {
            var relatedFailures = history.TransferFailures
                .Where(failure => failure.Branch == readiness.Branch && failure.Level == readiness.Level)
                .Select(failure => failure.TransferTask)
                .Distinct(StringComparer.Ordinal)
                .Count();

            if (relatedFailures < RequiredTransferFailures)
            {
                continue;
            }

            conditions.Add(new StuckStateCondition(
                StuckStateConditionKind.IsolatedDrillsPassButRelatedTransferTestsFail,
                readiness.Branch,
                readiness.Level,
                Constraint: null,
                PrerequisiteBranch: null,
                Bottleneck: null,
                $"{readiness.Branch} {readiness.Level} isolated drills passed but two related transfer tests failed."));
        }
    }
}
