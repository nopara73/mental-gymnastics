using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class CoreIntegrationPassTests
{
    private const string FocusShiftL3Demand = "FS-L3 invalid-cue conflict demand";

    [Fact]
    public void ReadinessGateStateMachineStabilizationAndTransferComposeWithoutSkippingOwnership()
    {
        var readiness = TestReadinessEvaluator.Evaluate(
            new TestReadinessRequest(
                new PractitionerState(
                [
                    Status(BranchCode.FS, GlobalLevelId.L2, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Maintenance),
                ]),
                BranchCode.FS,
                GlobalLevelId.L3,
                DrillId.FS2InvalidCueFilter,
                FocusShiftL3Demand,
                [
                    CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
                    CleanPractice(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter, FocusShiftL3Demand),
                ],
                [
                    CurrentMaintenance(BranchCode.FS, GlobalLevelId.L2),
                    CurrentMaintenance(BranchCode.IR, GlobalLevelId.L2),
                ],
                StandardFor(BranchCode.FS, GlobalLevelId.L3),
                HonestyConstraintFor(DrillId.FS2InvalidCueFilter)));

        Assert.True(readiness.MayTest);

        var standardEvidence = PassingStandard();
        var gate = FormalGateDecisionEngine.Decide(
            FormalAttempt(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter),
            standardEvidence);
        var stateAfterFormalPass = BranchLevelStateMachine.TryApply(
            BranchLevelState.TestReady,
            BranchLevelTransition.PassFormalTestOnce);
        var ownership = StabilizationOwnershipEvaluator.Evaluate(
            new StabilizationEvidence(
                BranchCode.FS,
                GlobalLevelId.L3,
                [
                    StabilizationPass(
                        BranchCode.FS,
                        GlobalLevelId.L3,
                        FormalTestPassState.PassOnce,
                        TrainingDate.From(2026, 7, 4),
                        standardEvidence),
                ]));
        var transferDefinition = TransferTestCatalog.TransferTests.Single(test => test.SourceBranch == BranchCode.FS);
        var transfer = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.FS,
                GlobalLevelId.L3,
                transferDefinition.TransferTask,
                CapacityId.DeliberateSwitching,
                transferDefinition.SameDemand,
                transferDefinition.ChangedContext,
                new TransferSourceStandardEvidence(
                    BranchCode.FS,
                    GlobalLevelId.L3,
                    StandardFor(BranchCode.FS, GlobalLevelId.L3),
                    visibleInTransferArtifact: true),
                transferDefinition.RetestRequirement));

        Assert.Equal(GateOutcome.PassOnce, gate.Outcome);
        Assert.True(gate.OpensStabilization);
        Assert.True(stateAfterFormalPass.IsValid);
        Assert.Equal(BranchLevelState.PassedOnce, stateAfterFormalPass.NextState);
        Assert.False(ownership.IsOwned);
        Assert.Equal(GateOutcome.PassOnce, ownership.GateOutcome);
        Assert.Equal(BranchLevelState.PassedOnce, ownership.BranchLevelState);
        Assert.True(transfer.IsEligible);
    }

    [Fact]
    public void MaintenanceDecayCapsBalanceRecoveryDeloadPlanningFailureAndReviewStayCoherent()
    {
        var asOf = TrainingDate.From(2026, 7, 9);
        var weekStart = TrainingDate.From(2026, 7, 6);
        var failedMaintenance = MaintenanceCurrencyEvaluator.Evaluate(
            new MaintenanceCurrencyRequest(
                BranchCode.WM,
                GlobalLevelId.L3,
                asOf,
                [
                    MaintenancePass(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 1)),
                    MaintenanceFail(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 8)),
                    MaintenanceFail(BranchCode.WM, GlobalLevelId.L3, TrainingDate.From(2026, 7, 9)),
                ]));
        var decay = DecayRestorationEvaluator.EvaluateDecay(
            Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
            failedMaintenance);
        var practitionerState = new PractitionerState(
        [
            Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
            decay.NextStatus,
            Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
        ]);
        var maintenanceCurrency = new[]
        {
            CurrentCurrency(BranchCode.FH, GlobalLevelId.L3),
            CurrentCurrency(BranchCode.FS, GlobalLevelId.L3),
            failedMaintenance,
            CurrentCurrency(BranchCode.IR, GlobalLevelId.L3),
            CurrentCurrency(BranchCode.DE, GlobalLevelId.L3),
        };

        var caps = DependencyCapEvaluator.Evaluate(
            new DependencyCapRequest(BranchCode.CO, practitionerState, maintenanceCurrency));
        var balance = GlobalBalanceEvaluator.EvaluateAdvancement(
            new GlobalBalanceAdvancementRequest(
                BranchCode.CO,
                GlobalLevelId.L1,
                practitionerState,
                maintenanceCurrency));
        var recovery = RecoveryDecisionEvaluator.Evaluate(
            new RecoveryDecisionRequest(
                BranchCode.WM,
                GlobalLevelId.L3,
                LoadVariableKind.ItemCount,
                "No rereading after encode window.",
                [
                    new RecoverySetResultEvidence(BranchCode.WM, GlobalLevelId.L3, asOf, 1, failedFromOverload: true),
                    new RecoverySetResultEvidence(BranchCode.WM, GlobalLevelId.L3, asOf, 2, failedFromOverload: true),
                ],
                [],
                [],
                [],
                [],
                ["felt hard"]));
        var deload = DeloadDecisionEvaluator.Evaluate(
            new DeloadDecisionRequest(
                weekStart,
                [
                    new DeloadBranchWeekEvidence(BranchCode.WM, weekStart, overloadObserved: false, decayObserved: true),
                    new DeloadBranchWeekEvidence(BranchCode.IR, weekStart, overloadObserved: true, decayObserved: false),
                ],
                ["frustrated"]));
        var classifiedFailure = new ClassifiedFailure(
            BranchCode.WM,
            GlobalLevelId.L3,
            FailureType.BadProgramming,
            [FailureEvidenceSignal.MultipleBranchesDegrade, FailureEvidenceSignal.PrerequisiteNotStable]);
        var failureResponse = FailureResponseRouter.Route(
            new FailureResponseRequest(
                classifiedFailure,
                isFirstFailureOfType: false,
                repeatedOverloadInSameBranch: false,
                StuckStateResponseContext.None));
        var globalReview = GlobalReviewEvaluator.Evaluate(
            new GlobalReviewInput(
                asOf,
                PractitionerCategory.Intermediate,
                practitionerState,
                CurrentOwnedLevels(),
                maintenanceCurrency,
                [classifiedFailure],
                [CurrentStabilizationArtifact(asOf)],
                new GlobalReviewBottleneckInput(
                    BranchCode.WM,
                    BottleneckKind.WorkingMemoryEncodingFidelity,
                    hasProgrammedResponse: true,
                    needsEmphasis: true),
                [new GlobalReviewVolumeIntensityRecord(BranchCode.WM, 3, TrainingIntensityKind.Moderate)],
                [new GlobalReviewRecoveryRecord(weekStart, wasDeload: true)],
                [],
                pauseTestsForDeload: deload.ShouldDeload,
                openAdvancedBranch: false,
                attemptTransferIntegrationTransfer: false));
        var weeklyPlan = WeeklyProgrammingPlanner.Generate(
            new WeeklyProgrammingRequest(
                new PractitionerCategoryClassificationResult(PractitionerCategory.Intermediate, []),
                maintenanceCurrency,
                globalReview.Decisions,
                recovery.ShouldRecover,
                selectedFoundationalLoadBranch: BranchCode.WM,
                weakestFoundationalBranch: BranchCode.WM,
                selectedAdvancedBranch: BranchCode.CO,
                prerequisiteSupportBranch: BranchCode.WM,
                eligibleAdvancementBranch: BranchCode.CO,
                bottleneckBranch: BranchCode.WM,
                recentlyPassedBranch: BranchCode.FS,
                transferBranch: BranchCode.TI));

        Assert.Equal(MaintenanceCurrencyState.Failed, failedMaintenance.State);
        Assert.True(decay.ChangedState);
        Assert.Equal(BranchLevelState.Decayed, decay.NextStatus.State);
        Assert.False(caps.CanAdvance);
        Assert.Contains(caps.Caps, cap => cap.Reason == DependencyCapReason.DecayedPrerequisite);
        Assert.False(balance.CanAdvance);
        Assert.Contains(balance.Issues, issue => issue.Kind == GlobalBalanceIssueKind.AdvancedPrerequisiteDecayed);
        Assert.True(recovery.ShouldRecover);
        Assert.False(recovery.Prescription!.AdvancementTestingAllowed);
        Assert.True(deload.ShouldDeload);
        Assert.False(deload.Prescription!.AdvancementTestingAllowed);
        Assert.True(deload.Prescription.MaintenanceChecksRemain);
        Assert.Contains(ProgrammingResponseAction.Deload, failureResponse.Actions);
        Assert.Contains(ProgrammingResponseAction.RestorePrerequisites, failureResponse.Actions);
        Assert.False(globalReview.Passed);
        Assert.Contains(globalReview.Failures, failure => failure.Kind == GlobalReviewFailureKind.PrerequisiteBranchDecayed);
        Assert.Contains(globalReview.Decisions, decision => decision.Kind == GlobalReviewDecisionKind.RestoreDecayedBranch);
        Assert.Contains(globalReview.Decisions, decision => decision.Kind == GlobalReviewDecisionKind.PauseTestsForDeload);
        Assert.False(weeklyPlan.AdvancementWorkAllowed);
        Assert.Contains(weeklyPlan.Constraints, constraint => constraint.Kind == WeeklyProgrammingConstraintKind.RecoveryRequired);
        Assert.Contains(weeklyPlan.Constraints, constraint => constraint.Kind == WeeklyProgrammingConstraintKind.AdvancementTestingSuspended);
        Assert.DoesNotContain(weeklyPlan.Days, day => day.IsAdvancementWork);
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static TestReadinessPracticeSession CleanPractice(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        string demand)
    {
        return new TestReadinessPracticeSession(branch, level, drill, demand, clean: true);
    }

    private static PrerequisiteMaintenanceCheck CurrentMaintenance(BranchCode branch, GlobalLevelId level)
    {
        return new PrerequisiteMaintenanceCheck(branch, level, isCurrent: true);
    }

    private static string StandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards
            .Single(standard => standard.Branch == branch && standard.Level == level)
            .Standard;
    }

    private static string HonestyConstraintFor(DrillId drill)
    {
        return ProgramCatalog.Drills.Single(definition => definition.Id == drill).HonestyConstraint;
    }

    private static StandardEvaluationResult PassingStandard()
    {
        return StandardEvaluator.Evaluate(
            new EvaluatedStandard(
                "integration standard",
                [NumericThreshold.AtLeast("score", 90)],
                [new CriticalConstraintRequirement("honesty", "Named honesty constraint preserved.")],
                requiresCompleteOutput: true,
                requiredRubric: RubricOutcome.Pass),
            new StandardEvaluationAttempt(
                [new NumericMeasurement("score", 95)],
                [new CriticalConstraintCheck("honesty", Satisfied: true)],
                outputComplete: true,
                rubricOutcome: RubricOutcome.Pass));
    }

    private static FormalTestAttempt FormalAttempt(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill)
    {
        return new FormalTestAttempt(
            branch,
            level,
            TrainingDate.From(2026, 7, 4),
            TestTask.ForDrill(drill),
            [new LoadVariable("cue density", "documented L3 load")],
            StandardFor(branch, level),
            [new CriticalConstraint("Named honesty constraint preserved.")],
            new TestResultEvidence(TestResultEvidenceKind.PassFail, "clean pass under stated standard"),
            failureType: null,
            FormalTestPassState.PassOnce,
            new EvidenceArtifact(
                EvidenceArtifactCategory.Test,
                TrainingDate.From(2026, 7, 4),
                [
                    new ObservableEvidence(ObservableEvidenceKind.Score, "95"),
                    new ObservableEvidence(ObservableEvidenceKind.CriticalConstraintRecord, "honesty preserved"),
                ],
                "formal gate attempt"));
    }

    private static StabilizationPassEvidence StabilizationPass(
        BranchCode branch,
        GlobalLevelId level,
        FormalTestPassState passState,
        TrainingDate date,
        StandardEvaluationResult standardEvidence)
    {
        return new StabilizationPassEvidence(
            branch,
            level,
            date,
            StandardFor(branch, level),
            passState,
            standardEvidence,
            afterAdjacentWorkOrControlledDistractor: false);
    }

    private static MaintenanceCheckEvidence MaintenancePass(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date)
    {
        return new MaintenanceCheckEvidence(
            branch,
            level,
            date,
            MaintenanceCheckKind.StandardOrTransfer,
            new StandardEvaluationResult(true, []));
    }

    private static MaintenanceCheckEvidence MaintenanceFail(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date)
    {
        return new MaintenanceCheckEvidence(
            branch,
            level,
            date,
            MaintenanceCheckKind.StandardOrTransfer,
            new StandardEvaluationResult(
                false,
                [new StandardEvaluationFailure(StandardFailureKind.CriticalConstraintBroken, "constraint failed")]));
    }

    private static MaintenanceCurrencyResult CurrentCurrency(BranchCode branch, GlobalLevelId level)
    {
        return new MaintenanceCurrencyResult(
            branch,
            level,
            MaintenanceCurrencyState.Current,
            new MaintenanceCadence(7, 10, MaintenanceCheckKind.StandardOrTransfer),
            DaysSinceLastPassingCheck: 3,
            ConsecutiveFailures: 0);
    }

    private static IReadOnlyList<GlobalReviewOwnedLevel> CurrentOwnedLevels()
    {
        return
        [
            new GlobalReviewOwnedLevel(BranchCode.FH, GlobalLevelId.L3),
            new GlobalReviewOwnedLevel(BranchCode.FS, GlobalLevelId.L3),
            new GlobalReviewOwnedLevel(BranchCode.WM, GlobalLevelId.L3),
            new GlobalReviewOwnedLevel(BranchCode.IR, GlobalLevelId.L3),
            new GlobalReviewOwnedLevel(BranchCode.DE, GlobalLevelId.L3),
            new GlobalReviewOwnedLevel(BranchCode.CO, null),
            new GlobalReviewOwnedLevel(BranchCode.AI, null),
            new GlobalReviewOwnedLevel(BranchCode.TI, null),
        ];
    }

    private static EvidenceArtifact CurrentStabilizationArtifact(TrainingDate date)
    {
        return new EvidenceArtifact(
            EvidenceArtifactCategory.Stabilization,
            date,
            [new ObservableEvidence(ObservableEvidenceKind.RepeatabilityRecord, "stabilization evidence current")],
            "current stabilization artifact");
    }
}
