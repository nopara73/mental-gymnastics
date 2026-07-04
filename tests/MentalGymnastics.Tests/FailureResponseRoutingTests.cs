using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class FailureResponseRoutingTests
{
    [Fact]
    public void RoutesTechnicalFailureToPracticeAndRetestLater()
    {
        var result = FailureResponseRouter.Route(
            Request(
                FailureType.TechnicalFailure,
                [FailureEvidenceSignal.ConfusionAboutRule],
                isFirstFailureOfType: true));

        Assert.Equal(FailureType.TechnicalFailure, result.Failure.Type);
        Assert.Equal(
            [
                ProgrammingResponseAction.StopTest,
                ProgrammingResponseAction.ReturnToPractice,
                ProgrammingResponseAction.SimplifyInstruction,
                ProgrammingResponseAction.RetestLater,
                ProgrammingResponseAction.PracticeDrillFormAtLowIntensityBeforeRetest,
            ],
            result.Actions);
    }

    [Fact]
    public void RoutesEffortFailureToSameOrLowerLoadWithStricterEvidence()
    {
        var result = FailureResponseRouter.Route(
            Request(
                FailureType.EffortFailure,
                [FailureEvidenceSignal.BrokenHonestyConstraint],
                isFirstFailureOfType: true));

        Assert.Equal(
            [
                ProgrammingResponseAction.FailAttempt,
                ProgrammingResponseAction.RepeatSameOrLowerLoadWithStricterEvidence,
                ProgrammingResponseAction.RequireCleanPracticeArtifactBeforeRepeat,
            ],
            result.Actions);
    }

    [Fact]
    public void RoutesFirstOverloadToRegressionAndCleanPracticeRetest()
    {
        var result = FailureResponseRouter.Route(
            Request(
                FailureType.Overload,
                [
                    FailureEvidenceSignal.ErrorsRiseAfterLoadIncrease,
                    FailureEvidenceSignal.ConstraintPreserved,
                ],
                isFirstFailureOfType: true));

        Assert.Equal(
            [
                ProgrammingResponseAction.ReduceOneLoadVariable,
                ProgrammingResponseAction.TrainRegression,
                ProgrammingResponseAction.RetestAfterCleanPractice,
                ProgrammingResponseAction.StabilizeRegression,
            ],
            result.Actions);
    }

    [Fact]
    public void RepeatedOverloadAddsPrerequisiteInspectionAndWeeklyLoadReduction()
    {
        var result = FailureResponseRouter.Route(
            new FailureResponseRequest(
                ClassifiedFailure(FailureType.Overload, [FailureEvidenceSignal.RepeatedOverload]),
                isFirstFailureOfType: false,
                repeatedOverloadInSameBranch: true,
                StuckStateResponseContext.None));

        Assert.Contains(ProgrammingResponseAction.ReduceOneLoadVariable, result.Actions);
        Assert.Contains(ProgrammingResponseAction.TrainRegression, result.Actions);
        Assert.Contains(ProgrammingResponseAction.InspectPrerequisiteBranch, result.Actions);
        Assert.Contains(ProgrammingResponseAction.ReduceWeeklyLoad, result.Actions);
    }

    [Fact]
    public void RoutesBadProgrammingToDeloadPrerequisiteRestorationAndTestSuspension()
    {
        var result = FailureResponseRouter.Route(
            Request(
                FailureType.BadProgramming,
                [
                    FailureEvidenceSignal.MultipleBranchesDegrade,
                    FailureEvidenceSignal.PrerequisiteNotStable,
                ],
                isFirstFailureOfType: false));

        Assert.Equal(
            [
                ProgrammingResponseAction.Deload,
                ProgrammingResponseAction.RestorePrerequisites,
                ProgrammingResponseAction.ReviseWeeklyEmphasis,
                ProgrammingResponseAction.SuspendAdvancementTestingForOneWeek,
                ProgrammingResponseAction.RunMaintenanceChecks,
                ProgrammingResponseAction.NoNewTests,
            ],
            result.Actions);
    }

    [Fact]
    public void StuckSignalsAddOnlyTheDocumentedStuckResponseActions()
    {
        var result = FailureResponseRouter.Route(
            new FailureResponseRequest(
                ClassifiedFailure(
                    FailureType.Overload,
                    [FailureEvidenceSignal.SameBranchGateFailedThreeTimesAcrossTenDays]),
                isFirstFailureOfType: false,
                repeatedOverloadInSameBranch: false,
                new StuckStateResponseContext(
                    isStuck: true,
                    failuresAppearInMoreThanOneBranch: true,
                    transferIsStuckPoint: true)));

        Assert.Contains(ProgrammingResponseAction.StopAdvancementTestsInStuckBranch, result.Actions);
        Assert.Contains(ProgrammingResponseAction.IdentifyStuckPoint, result.Actions);
        Assert.Contains(ProgrammingResponseAction.TrainNearestPrerequisiteForOneWeekAtModerateIntensity, result.Actions);
        Assert.Contains(ProgrammingResponseAction.UseRegressionPreservingFailedConstraint, result.Actions);
        Assert.Contains(ProgrammingResponseAction.ReduceTotalWeeklyLoad, result.Actions);
        Assert.Contains(ProgrammingResponseAction.RetestFailedConstraintBeforeWholeGate, result.Actions);
        Assert.Contains(ProgrammingResponseAction.ReturnToSourceBranch, result.Actions);
        Assert.Contains(ProgrammingResponseAction.TestNearerTransferDistance, result.Actions);
    }

    [Fact]
    public void EvidenceSignalsDoNotOverrideTheProvidedClassification()
    {
        var result = FailureResponseRouter.Route(
            Request(
                FailureType.TechnicalFailure,
                [FailureEvidenceSignal.BrokenHonestyConstraint],
                isFirstFailureOfType: false));

        Assert.Equal(FailureType.TechnicalFailure, result.Failure.Type);
        Assert.Contains(ProgrammingResponseAction.StopTest, result.Actions);
        Assert.DoesNotContain(ProgrammingResponseAction.FailAttempt, result.Actions);
    }

    private static FailureResponseRequest Request(
        FailureType failureType,
        IEnumerable<FailureEvidenceSignal> evidenceSignals,
        bool isFirstFailureOfType)
    {
        return new FailureResponseRequest(
            ClassifiedFailure(failureType, evidenceSignals),
            isFirstFailureOfType,
            repeatedOverloadInSameBranch: false,
            StuckStateResponseContext.None);
    }

    private static ClassifiedFailure ClassifiedFailure(
        FailureType failureType,
        IEnumerable<FailureEvidenceSignal> evidenceSignals)
    {
        return new ClassifiedFailure(
            BranchCode.FH,
            GlobalLevelId.L2,
            failureType,
            evidenceSignals);
    }
}
