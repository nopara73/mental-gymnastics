using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class CoreInvariantEdgeCaseTests
{
    private const string FocusShiftL1Demand = "FS-L1 cue switch demand";
    private const string ConceptOperationsL1Demand = "CO-L1 rule extraction demand";

    [Fact]
    public void FoundationalL1ReadinessRequiresFocusHoldL1PassedOnceButNotOwned()
    {
        var skippedPrerequisite = FocusShiftL1Readiness(BranchLevelState.Training);
        var openedAfterPassOnce = FocusShiftL1Readiness(BranchLevelState.PassedOnce);

        Assert.False(skippedPrerequisite.MayTest);
        Assert.Contains(
            skippedPrerequisite.Failures,
            failure => failure.Kind == TestReadinessFailureKind.PrerequisiteNotOwned);
        Assert.True(openedAfterPassOnce.MayTest);
        Assert.Empty(openedAfterPassOnce.Failures);
    }

    [Fact]
    public void AdvancedReadinessRejectsPrerequisiteThatOnlyPassedOnce()
    {
        var result = TestReadinessEvaluator.Evaluate(
            new TestReadinessRequest(
                new PractitionerState(
                [
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.PassedOnce),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ]),
                BranchCode.CO,
                GlobalLevelId.L1,
                DrillId.CO1RuleExtraction,
                ConceptOperationsL1Demand,
                [
                    CleanPractice(BranchCode.CO, GlobalLevelId.L1, DrillId.CO1RuleExtraction, ConceptOperationsL1Demand),
                    CleanPractice(BranchCode.CO, GlobalLevelId.L1, DrillId.CO1RuleExtraction, ConceptOperationsL1Demand),
                ],
                [
                    CurrentMaintenance(BranchCode.IR, GlobalLevelId.L3),
                    CurrentMaintenance(BranchCode.DE, GlobalLevelId.L3),
                ],
                StandardFor(BranchCode.CO, GlobalLevelId.L1),
                HonestyConstraintFor(DrillId.CO1RuleExtraction)));

        Assert.False(result.MayTest);
        Assert.Contains(
            result.Failures,
            failure => failure.Kind == TestReadinessFailureKind.PrerequisiteNotOwned &&
                failure.Detail.Contains("WM", StringComparison.Ordinal));
    }

    [Fact]
    public void DependencyCapsRejectAdvancedBranchWhenPrerequisiteIsNotOwned()
    {
        var passedOncePrerequisite = DependencyCapEvaluator.Evaluate(
            new DependencyCapRequest(
                BranchCode.CO,
                new PractitionerState(
                [
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.PassedOnce),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                ]),
                [
                    CurrentCurrency(BranchCode.IR, GlobalLevelId.L3),
                    CurrentCurrency(BranchCode.DE, GlobalLevelId.L3),
                ]));

        Assert.False(passedOncePrerequisite.CanAdvance);
        Assert.False(passedOncePrerequisite.IsCappedToMaintenanceOnly);
        Assert.Empty(passedOncePrerequisite.Caps);
    }

    [Fact]
    public void DependencyCapsRejectTransferIntegrationWhenOnlySatisfiedAdvancedRouteHasStaleMaintenance()
    {
        var result = DependencyCapEvaluator.Evaluate(
            new DependencyCapRequest(
                BranchCode.TI,
                new PractitionerState(
                [
                    Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
                    Status(BranchCode.CO, GlobalLevelId.L2, BranchLevelState.Training),
                    Status(BranchCode.AI, GlobalLevelId.L2, BranchLevelState.Maintenance),
                ]),
                [
                    CurrentCurrency(BranchCode.FH, GlobalLevelId.L3),
                    CurrentCurrency(BranchCode.FS, GlobalLevelId.L3),
                    CurrentCurrency(BranchCode.WM, GlobalLevelId.L3),
                    CurrentCurrency(BranchCode.IR, GlobalLevelId.L3),
                    CurrentCurrency(BranchCode.DE, GlobalLevelId.L3),
                    DueCurrency(BranchCode.AI, GlobalLevelId.L2),
                ]));

        Assert.False(result.CanAdvance);
        Assert.True(result.IsCappedToMaintenanceOnly);
        Assert.Contains(
            result.Caps,
            cap => cap.PrerequisiteBranch == BranchCode.AI &&
                cap.Reason == DependencyCapReason.OverduePrerequisiteMaintenance);
    }

    [Fact]
    public void MaintenanceCurrencyIgnoresFuturePassingChecksWhenDeterminingStaleness()
    {
        var result = MaintenanceCurrencyEvaluator.Evaluate(
            new MaintenanceCurrencyRequest(
                BranchCode.FH,
                GlobalLevelId.L2,
                TrainingDate.From(2026, 7, 4),
                [MaintenancePass(BranchCode.FH, GlobalLevelId.L2, TrainingDate.From(2026, 7, 5))]));

        Assert.Equal(MaintenanceCurrencyState.Due, result.State);
        Assert.Null(result.DaysSinceLastPassingCheck);
        Assert.Equal(0, result.ConsecutiveFailures);
    }

    [Fact]
    public void StandardEvaluationFailsWhenRequiredCriticalConstraintEvidenceIsMissing()
    {
        var result = StandardEvaluator.Evaluate(
            new EvaluatedStandard(
                "FH L1 standard",
                [NumericThreshold.AtMost("drifts", 5)],
                [new CriticalConstraintRequirement("target-stable", "No target change.")],
                requiresCompleteOutput: true,
                requiredRubric: null),
            new StandardEvaluationAttempt(
                [new NumericMeasurement("drifts", 3)],
                [],
                outputComplete: true,
                rubricOutcome: null));

        Assert.False(result.Passed);
        Assert.Contains(
            result.Failures,
            failure => failure.Kind == StandardFailureKind.CriticalConstraintBroken);
        Assert.DoesNotContain(
            result.Failures,
            failure => failure.Kind == StandardFailureKind.NumericalThresholdMissed);
    }

    [Fact]
    public void FailedFormalAttemptRequiresFailureClassification()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new FormalTestAttempt(
                BranchCode.FH,
                GlobalLevelId.L1,
                TrainingDate.From(2026, 7, 4),
                TestTask.ForDrill(DrillId.FH1TargetHold),
                [new LoadVariable("duration", "3 minutes")],
                StandardFor(BranchCode.FH, GlobalLevelId.L1),
                [new CriticalConstraint("No target change.")],
                new TestResultEvidence(TestResultEvidenceKind.PassFail, "failed critical constraint"),
                failureType: null,
                FormalTestPassState.Fail,
                Artifact(EvidenceArtifactCategory.Test)));

        Assert.Contains("failure type", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TransferEligibilityRejectsCapacityFromDifferentSourceBranch()
    {
        var result = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.WM,
                GlobalLevelId.L3,
                "Reconstruct structure from unfamiliar content.",
                CapacityId.RuleExtraction,
                "Encoding, delay, no invention.",
                "Domain or representation.",
                SourceStandardEvidence(BranchCode.WM, GlobalLevelId.L3),
                new TransferRetestPlan(requiredTransferContexts: 2, usesFreshEquivalentContexts: true)));

        Assert.False(result.IsEligible);
        Assert.Contains(
            result.Failures,
            failure => failure.Kind == TransferEligibilityFailureKind.TrainedCapacityNotInSourceBranch);
    }

    [Fact]
    public void TransferEligibilityRejectsSourceStandardFromDifferentLevel()
    {
        var result = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.FS,
                GlobalLevelId.L3,
                "Switch between two branch tasks.",
                CapacityId.DeliberateSwitching,
                "Cue obedience and return standard.",
                "Target type and branch context.",
                SourceStandardEvidence(BranchCode.FS, GlobalLevelId.L2),
                new TransferRetestPlan(requiredTransferContexts: 2, usesFreshEquivalentContexts: true)));

        Assert.False(result.IsEligible);
        Assert.Contains(
            result.Failures,
            failure => failure.Kind == TransferEligibilityFailureKind.SourceStandardDoesNotMatchCatalog);
    }

    [Fact]
    public void TransferEligibilityRejectsRetestPlanThatDoesNotRequireFreshEquivalentContent()
    {
        var result = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.CO,
                GlobalLevelId.L3,
                "Apply rule to unseen problem.",
                CapacityId.RuleExtraction,
                "Testable rule and prediction check.",
                "Domain or example set.",
                SourceStandardEvidence(BranchCode.CO, GlobalLevelId.L3),
                new TransferRetestPlan(requiredTransferContexts: 2, usesFreshEquivalentContexts: false)));

        Assert.False(result.IsEligible);
        Assert.Contains(
            result.Failures,
            failure => failure.Kind == TransferEligibilityFailureKind.RetestRequirementMissing);
    }

    private static TestReadinessResult FocusShiftL1Readiness(BranchLevelState focusHoldL1State)
    {
        return TestReadinessEvaluator.Evaluate(
            new TestReadinessRequest(
                new PractitionerState([Status(BranchCode.FH, GlobalLevelId.L1, focusHoldL1State)]),
                BranchCode.FS,
                GlobalLevelId.L1,
                DrillId.FS1CueSwitch,
                FocusShiftL1Demand,
                [
                    CleanPractice(BranchCode.FS, GlobalLevelId.L1, DrillId.FS1CueSwitch, FocusShiftL1Demand),
                    CleanPractice(BranchCode.FS, GlobalLevelId.L1, DrillId.FS1CueSwitch, FocusShiftL1Demand),
                ],
                [],
                StandardFor(BranchCode.FS, GlobalLevelId.L1),
                HonestyConstraintFor(DrillId.FS1CueSwitch)));
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

    private static MaintenanceCurrencyResult CurrentCurrency(BranchCode branch, GlobalLevelId level)
    {
        return Currency(branch, level, MaintenanceCurrencyState.Current);
    }

    private static MaintenanceCurrencyResult DueCurrency(BranchCode branch, GlobalLevelId level)
    {
        return Currency(branch, level, MaintenanceCurrencyState.Due);
    }

    private static MaintenanceCurrencyResult Currency(
        BranchCode branch,
        GlobalLevelId level,
        MaintenanceCurrencyState state)
    {
        return new MaintenanceCurrencyResult(
            branch,
            level,
            state,
            new MaintenanceCadence(7, 10, MaintenanceCheckKind.StandardOrTransfer),
            DaysSinceLastPassingCheck: state == MaintenanceCurrencyState.Current ? 3 : 11,
            ConsecutiveFailures: 0);
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

    private static TransferSourceStandardEvidence SourceStandardEvidence(
        BranchCode branch,
        GlobalLevelId level)
    {
        return new TransferSourceStandardEvidence(
            branch,
            level,
            StandardFor(branch, level),
            visibleInTransferArtifact: true);
    }

    private static EvidenceArtifact Artifact(EvidenceArtifactCategory category)
    {
        return new EvidenceArtifact(
            category,
            TrainingDate.From(2026, 7, 4),
            [new ObservableEvidence(ObservableEvidenceKind.Score, "observable record")],
            "artifact summary");
    }
}
