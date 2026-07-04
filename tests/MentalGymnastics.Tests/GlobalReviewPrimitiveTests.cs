using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class GlobalReviewPrimitiveTests
{
    private static readonly TrainingDate ReviewDate = TrainingDate.From(2026, 7, 4);

    [Fact]
    public void RepresentsDocumentedGlobalReviewInputsAndPossibleDecisions()
    {
        var input = PassingInput();

        Assert.Equal(PractitionerCategory.Intermediate, input.PractitionerCategory);
        Assert.Equal(Enum.GetValues<BranchCode>(), input.CurrentOwnedLevels.Select(level => level.Branch));
        Assert.Equal(3, input.LastThreeFailures.Count);
        Assert.Contains(input.EvidenceArtifacts, artifact => artifact.Category == EvidenceArtifactCategory.Transfer);
        Assert.NotNull(input.Bottleneck);
        Assert.NotEmpty(input.VolumeAndIntensityHistory);
        Assert.NotEmpty(input.RecoveryOrDeloadHistory);

        Assert.Equal(
            [
                GlobalReviewDecisionKind.ContinueCurrentProgression,
                GlobalReviewDecisionKind.EmphasizeBottleneckBranch,
                GlobalReviewDecisionKind.RestoreDecayedBranch,
                GlobalReviewDecisionKind.OpenAdvancedBranch,
                GlobalReviewDecisionKind.PauseTestsForDeload,
                GlobalReviewDecisionKind.AttemptTransferIntegrationTransfer,
            ],
            GlobalReviewDecisionCatalog.PossibleDecisions);
    }

    [Fact]
    public void PassingReviewRequiresAllDocumentedPassConditions()
    {
        var result = GlobalReviewEvaluator.Evaluate(PassingInput());

        Assert.True(result.Passed);
        Assert.Empty(result.Failures);
        Assert.Contains(result.Decisions, decision => decision.Kind == GlobalReviewDecisionKind.ContinueCurrentProgression);
    }

    [Fact]
    public void FailedReviewReportsDocumentedConditionFailures()
    {
        var result = GlobalReviewEvaluator.Evaluate(
            PassingInput(
                practitionerState: new PractitionerState(
                [
                    Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Decayed),
                ]),
                maintenanceStatus:
                [
                    Maintenance(BranchCode.WM, GlobalLevelId.L3, MaintenanceCurrencyState.Due),
                ],
                evidenceArtifacts: [Artifact(EvidenceArtifactCategory.Test, ReviewDate)],
                bottleneck: new GlobalReviewBottleneckInput(
                    BranchCode.WM,
                    BottleneckKind.WorkingMemoryEncodingFidelity,
                    hasProgrammedResponse: false,
                    needsEmphasis: true),
                advancements:
                [
                    new GlobalReviewAdvancementRecord(
                        BranchCode.FH,
                        GlobalLevelId.L3,
                        advancedByParticipationAlone: true),
                ],
                currentOwnedLevels:
                [
                    OwnedLevel(BranchCode.WM, GlobalLevelId.L3),
                ]));

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, failure => failure.Kind == GlobalReviewFailureKind.PrerequisiteBranchDecayed);
        Assert.Contains(result.Failures, failure => failure.Kind == GlobalReviewFailureKind.MaintenanceCheckOverdue);
        Assert.Contains(result.Failures, failure => failure.Kind == GlobalReviewFailureKind.BottleneckProgrammedResponseMissing);
        Assert.Contains(result.Failures, failure => failure.Kind == GlobalReviewFailureKind.CurrentTransferOrStabilizationArtifactMissing);
        Assert.Contains(result.Failures, failure => failure.Kind == GlobalReviewFailureKind.ParticipationOnlyAdvancement);
        Assert.Contains(result.Failures, failure => failure.Kind == GlobalReviewFailureKind.WholePractitionerInputMissing);
        Assert.Contains(result.Decisions, decision => decision.Kind == GlobalReviewDecisionKind.RestoreDecayedBranch);
    }

    [Fact]
    public void EmphasizesBottleneckBranchWhenProgrammedResponseRequiresEmphasis()
    {
        var result = GlobalReviewEvaluator.Evaluate(
            PassingInput(
                bottleneck: new GlobalReviewBottleneckInput(
                    BranchCode.DE,
                    BottleneckKind.DiscriminationAuditAccuracy,
                    hasProgrammedResponse: true,
                    needsEmphasis: true)));

        Assert.True(result.Passed);
        Assert.Contains(
            result.Decisions,
            decision => decision.Kind == GlobalReviewDecisionKind.EmphasizeBottleneckBranch &&
                decision.Branch == BranchCode.DE);
    }

    [Fact]
    public void PausesAdvancementTestsForDeloadWhenReviewInputRequiresPause()
    {
        var result = GlobalReviewEvaluator.Evaluate(
            PassingInput(pauseTestsForDeload: true));

        Assert.True(result.Passed);
        Assert.Contains(result.Decisions, decision => decision.Kind == GlobalReviewDecisionKind.PauseTestsForDeload);
        Assert.DoesNotContain(result.Decisions, decision => decision.Kind == GlobalReviewDecisionKind.OpenAdvancedBranch);
    }

    private static GlobalReviewInput PassingInput(
        PractitionerState? practitionerState = null,
        IEnumerable<GlobalReviewOwnedLevel>? currentOwnedLevels = null,
        IEnumerable<MaintenanceCurrencyResult>? maintenanceStatus = null,
        IEnumerable<EvidenceArtifact>? evidenceArtifacts = null,
        GlobalReviewBottleneckInput? bottleneck = null,
        IEnumerable<GlobalReviewAdvancementRecord>? advancements = null,
        bool pauseTestsForDeload = false)
    {
        return new GlobalReviewInput(
            ReviewDate,
            PractitionerCategory.Intermediate,
            practitionerState ?? WholePractitionerState(),
            currentOwnedLevels ?? AllCurrentOwnedLevels(),
            maintenanceStatus ?? CurrentMaintenanceForFoundations(),
            LastThreeFailures(),
            evidenceArtifacts ?? [Artifact(EvidenceArtifactCategory.Transfer, ReviewDate)],
            bottleneck ?? new GlobalReviewBottleneckInput(
                BranchCode.FS,
                BottleneckKind.FocusShiftRecovery,
                hasProgrammedResponse: true,
                needsEmphasis: false),
            [
                new GlobalReviewVolumeIntensityRecord(BranchCode.FH, workingSets: 3, TrainingIntensityKind.Moderate),
                new GlobalReviewVolumeIntensityRecord(BranchCode.WM, workingSets: 2, TrainingIntensityKind.High),
            ],
            [
                new GlobalReviewRecoveryRecord(TrainingDate.From(2026, 7, 2), wasDeload: false),
            ],
            advancements ?? [],
            pauseTestsForDeload,
            openAdvancedBranch: false,
            attemptTransferIntegrationTransfer: false);
    }

    private static PractitionerState WholePractitionerState()
    {
        return new PractitionerState(
        [
            Status(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Maintenance),
            Status(BranchCode.CO, GlobalLevelId.L2, BranchLevelState.Maintenance),
        ]);
    }

    private static IReadOnlyList<GlobalReviewOwnedLevel> AllCurrentOwnedLevels()
    {
        return
        [
            OwnedLevel(BranchCode.FH, GlobalLevelId.L3),
            OwnedLevel(BranchCode.FS, GlobalLevelId.L3),
            OwnedLevel(BranchCode.WM, GlobalLevelId.L3),
            OwnedLevel(BranchCode.IR, GlobalLevelId.L3),
            OwnedLevel(BranchCode.DE, GlobalLevelId.L3),
            OwnedLevel(BranchCode.CO, GlobalLevelId.L2),
            OwnedLevel(BranchCode.AI, ownedLevel: null),
            OwnedLevel(BranchCode.TI, ownedLevel: null),
        ];
    }

    private static IReadOnlyList<MaintenanceCurrencyResult> CurrentMaintenanceForFoundations()
    {
        return
        [
            Maintenance(BranchCode.FH, GlobalLevelId.L3, MaintenanceCurrencyState.Current),
            Maintenance(BranchCode.FS, GlobalLevelId.L3, MaintenanceCurrencyState.Current),
            Maintenance(BranchCode.WM, GlobalLevelId.L3, MaintenanceCurrencyState.Current),
            Maintenance(BranchCode.IR, GlobalLevelId.L3, MaintenanceCurrencyState.Current),
            Maintenance(BranchCode.DE, GlobalLevelId.L3, MaintenanceCurrencyState.Current),
            Maintenance(BranchCode.CO, GlobalLevelId.L2, MaintenanceCurrencyState.Current),
        ];
    }

    private static IReadOnlyList<ClassifiedFailure> LastThreeFailures()
    {
        return
        [
            Failure(BranchCode.FH, FailureType.Overload),
            Failure(BranchCode.WM, FailureType.TechnicalFailure),
            Failure(BranchCode.DE, FailureType.EffortFailure),
        ];
    }

    private static ClassifiedFailure Failure(BranchCode branch, FailureType type)
    {
        return new ClassifiedFailure(
            branch,
            GlobalLevelId.L3,
            type,
            [FailureEvidenceSignal.ConstraintPreserved]);
    }

    private static BranchLevelStatus Status(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState state)
    {
        return new BranchLevelStatus(branch, level, state);
    }

    private static GlobalReviewOwnedLevel OwnedLevel(
        BranchCode branch,
        GlobalLevelId? ownedLevel)
    {
        return new GlobalReviewOwnedLevel(branch, ownedLevel);
    }

    private static MaintenanceCurrencyResult Maintenance(
        BranchCode branch,
        GlobalLevelId ownedLevel,
        MaintenanceCurrencyState state)
    {
        return new MaintenanceCurrencyResult(
            branch,
            ownedLevel,
            state,
            new MaintenanceCadence(7, 10, MaintenanceCheckKind.StandardOrTransfer),
            DaysSinceLastPassingCheck: 3,
            ConsecutiveFailures: 0);
    }

    private static EvidenceArtifact Artifact(
        EvidenceArtifactCategory category,
        TrainingDate date)
    {
        return new EvidenceArtifact(
            category,
            date,
            [new ObservableEvidence(ObservableEvidenceKind.GlobalReviewSummary, $"{category} review evidence")],
            $"{category} artifact");
    }
}
