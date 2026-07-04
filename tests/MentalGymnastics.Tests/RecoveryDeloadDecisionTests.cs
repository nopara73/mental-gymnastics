using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class RecoveryDeloadDecisionTests
{
    [Fact]
    public void RecoveryIsTriggeredByEachDocumentedEvidenceSignal()
    {
        var cases = new[]
        {
            (
                RecoveryTriggerKind.TwoConsecutiveOverloadSetFailures,
                Request(
                    setResults:
                    [
                        SetFailure(1, failedFromOverload: true),
                        SetFailure(2, failedFromOverload: true),
                    ])),
            (
                RecoveryTriggerKind.ErrorCountRisesWithUnchangedLoad,
                Request(errorTrends: [ErrorTrend(previousErrors: 2, currentErrors: 4, loadUnchanged: true)])),
            (
                RecoveryTriggerKind.HonestyConstraintBroken,
                Request(honestyConstraintEvidence: [HonestyConstraint(broken: true)])),
            (
                RecoveryTriggerKind.AdjacentBranchesShowBroadDecay,
                Request(
                    adjacentBranchDecayEvidence:
                    [
                        AdjacentDecay(BranchCode.FS),
                        AdjacentDecay(BranchCode.IR),
                    ])),
            (
                RecoveryTriggerKind.SameBranchHighIntensityTestWithin24Hours,
                Request(recentHighIntensityTests: [RecentHighIntensityTest(hoursBeforeSession: 24)])),
        };

        foreach (var (expectedTrigger, request) in cases)
        {
            var result = RecoveryDecisionEvaluator.Evaluate(request);

            Assert.True(result.ShouldRecover);
            Assert.Contains(expectedTrigger, result.Triggers);
        }
    }

    [Fact]
    public void RecoveryPrescriptionReducesOneLoadVariableAndPreservesCoreConstraint()
    {
        var result = RecoveryDecisionEvaluator.Evaluate(
            Request(
                loadVariableToReduce: LoadVariableKind.Delay,
                coreConstraint: "no rereading after encode window",
                setResults:
                [
                    SetFailure(1, failedFromOverload: true),
                    SetFailure(2, failedFromOverload: true),
                ]));

        Assert.True(result.ShouldRecover);
        Assert.NotNull(result.Prescription);
        Assert.Equal(LoadVariableKind.Delay, result.Prescription.ReducedLoadVariable);
        Assert.Equal(1, result.Prescription.LoadReductionLevels);
        Assert.Equal("no rereading after encode window", result.Prescription.CoreConstraint);
        Assert.Equal(1, result.Prescription.EvidenceArtifactsToRecord);
        Assert.False(result.Prescription.AdvancementTestingAllowed);
    }

    [Fact]
    public void RecoveryIsNotTriggeredByUndocumentedOrIncompleteEvidence()
    {
        var result = RecoveryDecisionEvaluator.Evaluate(
            Request(
                setResults: [SetFailure(1, failedFromOverload: true)],
                errorTrends: [ErrorTrend(previousErrors: 4, currentErrors: 4, loadUnchanged: true)],
                adjacentBranchDecayEvidence: [AdjacentDecay(BranchCode.FS)],
                recentHighIntensityTests: [RecentHighIntensityTest(hoursBeforeSession: 25)],
                subjectiveNotes: ["felt tired and unmotivated"]));

        Assert.False(result.ShouldRecover);
        Assert.Empty(result.Triggers);
        Assert.Null(result.Prescription);
    }

    [Fact]
    public void DeloadIsTriggeredWhenTwoBranchesShowOverloadOrDecayInTheSameWeek()
    {
        var weekStart = TrainingDate.From(2026, 7, 6);

        var result = DeloadDecisionEvaluator.Evaluate(
            new DeloadDecisionRequest(
                weekStart,
                [
                    new DeloadBranchWeekEvidence(BranchCode.WM, weekStart, overloadObserved: true, decayObserved: false),
                    new DeloadBranchWeekEvidence(BranchCode.IR, weekStart, overloadObserved: false, decayObserved: true),
                ],
                subjectiveNotes: []));

        Assert.True(result.ShouldDeload);
        Assert.Contains(
            DeloadTriggerKind.TwoOrMoreBranchesShowOverloadOrDecayInSameWeek,
            result.Triggers);
        Assert.NotNull(result.Prescription);
        Assert.Equal(1, result.Prescription.WorkingSetReductionNumerator);
        Assert.Equal(3, result.Prescription.WorkingSetReductionDenominator);
        Assert.False(result.Prescription.AdvancementTestingAllowed);
        Assert.True(result.Prescription.MaintenanceChecksRemain);
    }

    [Fact]
    public void DeloadIsNotTriggeredWithoutTwoDocumentedBranchesInTheSameWeek()
    {
        var weekStart = TrainingDate.From(2026, 7, 6);

        var result = DeloadDecisionEvaluator.Evaluate(
            new DeloadDecisionRequest(
                weekStart,
                [
                    new DeloadBranchWeekEvidence(BranchCode.WM, weekStart, overloadObserved: true, decayObserved: false),
                    new DeloadBranchWeekEvidence(
                        BranchCode.IR,
                        TrainingDate.From(2026, 7, 13),
                        overloadObserved: false,
                        decayObserved: true),
                ],
                subjectiveNotes: ["the week felt too difficult"]));

        Assert.False(result.ShouldDeload);
        Assert.Empty(result.Triggers);
        Assert.Null(result.Prescription);
    }

    private static RecoveryDecisionRequest Request(
        LoadVariableKind loadVariableToReduce = LoadVariableKind.ItemCount,
        string coreConstraint = "no invented items",
        IEnumerable<RecoverySetResultEvidence>? setResults = null,
        IEnumerable<RecoveryErrorTrendEvidence>? errorTrends = null,
        IEnumerable<RecoveryHonestyConstraintEvidence>? honestyConstraintEvidence = null,
        IEnumerable<AdjacentBranchDecayEvidence>? adjacentBranchDecayEvidence = null,
        IEnumerable<RecentHighIntensityTestEvidence>? recentHighIntensityTests = null,
        IEnumerable<string>? subjectiveNotes = null)
    {
        return new RecoveryDecisionRequest(
            BranchCode.WM,
            GlobalLevelId.L2,
            loadVariableToReduce,
            coreConstraint,
            setResults ?? [],
            errorTrends ?? [],
            honestyConstraintEvidence ?? [],
            adjacentBranchDecayEvidence ?? [],
            recentHighIntensityTests ?? [],
            subjectiveNotes ?? []);
    }

    private static RecoverySetResultEvidence SetFailure(int setNumber, bool failedFromOverload)
    {
        return new RecoverySetResultEvidence(
            BranchCode.WM,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 6),
            setNumber,
            failedFromOverload);
    }

    private static RecoveryErrorTrendEvidence ErrorTrend(
        int previousErrors,
        int currentErrors,
        bool loadUnchanged)
    {
        return new RecoveryErrorTrendEvidence(
            BranchCode.WM,
            GlobalLevelId.L2,
            previousErrors,
            currentErrors,
            loadUnchanged);
    }

    private static RecoveryHonestyConstraintEvidence HonestyConstraint(bool broken)
    {
        return new RecoveryHonestyConstraintEvidence(
            BranchCode.WM,
            GlobalLevelId.L2,
            broken);
    }

    private static AdjacentBranchDecayEvidence AdjacentDecay(BranchCode adjacentBranch)
    {
        return new AdjacentBranchDecayEvidence(
            BranchCode.WM,
            adjacentBranch,
            decayed: true);
    }

    private static RecentHighIntensityTestEvidence RecentHighIntensityTest(int hoursBeforeSession)
    {
        return new RecentHighIntensityTestEvidence(
            BranchCode.WM,
            GlobalLevelId.L2,
            TrainingIntensityKind.High,
            hoursBeforeSession);
    }
}
