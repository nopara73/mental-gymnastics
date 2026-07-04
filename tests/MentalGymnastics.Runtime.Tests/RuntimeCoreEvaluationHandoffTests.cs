using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeCoreEvaluationHandoffTests
{
    private static readonly TrainingDate SessionDate = TrainingDate.From(2026, 7, 4);

    [Fact]
    public void MapsFormalTestRuntimeResultToCoreStandardAndGateInputsWithoutDecision()
    {
        var session = CreateSessionDefinition(
            SessionType.Test,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold);
        var result = CompleteWithEvidence(
            "session-core-formal",
            session,
            RuntimeEvidenceCaptureKind.FormalAttempt,
            [
                new RuntimeEventFact("score", "drifts=4; max_return=8s; no target change"),
                new RuntimeEventFact("output_sample", "FH-1 formal set held the original target with four marked drifts"),
            ]);

        var handoff = RuntimeCoreEvaluationHandoffMapper.Map(new RuntimeCoreEvaluationHandoffRequest(
            result,
            standardEvaluation: new RuntimeStandardEvaluationHandoffInput(
                [new NumericMeasurement("drifts", 4)],
                [new CriticalConstraintCheck("target-stable", true)],
                outputComplete: true,
                rubricOutcome: null),
            formalGate: new RuntimeFormalGateHandoffInput(
                SessionDate,
                new TestResultEvidence(TestResultEvidenceKind.Score, "drifts=4; max_return=8s"),
                FormalTestPassState.PassOnce)));

        var standardResult = StandardEvaluator.Evaluate(
            new EvaluatedStandard(
                "Runtime FH L1 standard check",
                [NumericThreshold.AtMost("drifts", 5)],
                [new CriticalConstraintRequirement("target-stable", "No target change.")],
                requiresCompleteOutput: true,
                requiredRubric: null),
            handoff.StandardEvaluationAttempt!);
        var gateDecision = FormalGateDecisionEngine.Decide(
            handoff.FormalTestAttempt!,
            standardResult);

        Assert.True(standardResult.Passed);
        Assert.Empty(standardResult.Failures);
        Assert.Equal(GateOutcome.PassOnce, gateDecision.Outcome);
        Assert.False(result.ContainsAdvancementDecision);
        Assert.DoesNotContain(result.ResultFacts, fact => fact.Name is "gate_outcome" or "branch_level_state");
    }

    [Fact]
    public void MapsPracticeRuntimeResultsToCoreReadinessPracticeSessions()
    {
        var session = CreateSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold);
        var drillDemand = ProgramCatalog.Standards
            .Single(standard => standard.Branch == BranchCode.FH && standard.Level == GlobalLevelId.L1)
            .Demand;
        var first = CompleteWithEvidence(
            "session-core-practice-1",
            session,
            RuntimeEvidenceCaptureKind.BestSet,
            [
                new RuntimeEventFact("output_sample", "FH-1 best set: target held with four marked drifts"),
                new RuntimeEventFact("score", "drifts=4; max_return=8s"),
            ]);
        var second = CompleteWithEvidence(
            "session-core-practice-2",
            session,
            RuntimeEvidenceCaptureKind.BestSet,
            [
                new RuntimeEventFact("output_sample", "FH-1 best set: target held with three marked drifts"),
                new RuntimeEventFact("score", "drifts=3; max_return=7s"),
            ]);

        var practiceSessions = new[]
        {
            RuntimeCoreEvaluationHandoffMapper.Map(new RuntimeCoreEvaluationHandoffRequest(
                first,
                readinessPractice: new RuntimeReadinessPracticeHandoffInput(drillDemand, clean: true)))
                .ReadinessPracticeSession!.Value,
            RuntimeCoreEvaluationHandoffMapper.Map(new RuntimeCoreEvaluationHandoffRequest(
                second,
                readinessPractice: new RuntimeReadinessPracticeHandoffInput(drillDemand, clean: true)))
                .ReadinessPracticeSession!.Value,
        };
        var readiness = TestReadinessEvaluator.Evaluate(new TestReadinessRequest(
            new PractitionerState([]),
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            drillDemand,
            practiceSessions,
            [],
            StandardFor(BranchCode.FH, GlobalLevelId.L1),
            HonestyConstraintFor(DrillId.FH1TargetHold)));

        Assert.True(readiness.MayTest);
        Assert.Empty(readiness.Failures);
    }

    [Fact]
    public void MapsStabilizationRuntimeResultToCoreOwnershipEvidence()
    {
        var session = CreateSessionDefinition(
            SessionType.Stabilization,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold);
        var result = CompleteWithEvidence(
            "session-core-stabilization",
            session,
            RuntimeEvidenceCaptureKind.Stabilization,
            [
                new RuntimeEventFact("repeatability_record", "third clean FH-1 pass after controlled distractor"),
                new RuntimeEventFact("score", "drifts=3; max_return=6s"),
            ]);

        var handoff = RuntimeCoreEvaluationHandoffMapper.Map(new RuntimeCoreEvaluationHandoffRequest(
            result,
            stabilization: new RuntimeStabilizationCoreHandoffInput(
                TrainingDate.From(2026, 7, 8),
                new StandardEvaluationResult(true, []),
                FormalTestPassState.StabilizationPass,
                afterAdjacentWorkOrControlledDistractor: true,
                "Target substitution avoided.")));
        var ownership = StabilizationOwnershipEvaluator.Evaluate(new StabilizationEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            [
                ExistingStabilizationPass(FormalTestPassState.PassOnce, TrainingDate.From(2026, 7, 1)),
                ExistingStabilizationPass(FormalTestPassState.StabilizationPass, TrainingDate.From(2026, 7, 4)),
                handoff.StabilizationPass!,
            ]));

        Assert.True(ownership.IsOwned);
        Assert.Equal(GateOutcome.Own, ownership.GateOutcome);
    }

    [Fact]
    public void MapsMaintenanceRuntimeResultToCoreMaintenanceCurrencyEvidence()
    {
        var session = CreateSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L2,
            DrillId.FH1TargetHold);
        var result = CompleteWithEvidence(
            "session-core-maintenance",
            session,
            RuntimeEvidenceCaptureKind.Maintenance,
            [
                new RuntimeEventFact("maintenance_check", "FH L2 reduced-volume hold check preserved drift marking"),
                new RuntimeEventFact("score", "drifts=2; max_return=5s"),
            ]);

        var handoff = RuntimeCoreEvaluationHandoffMapper.Map(new RuntimeCoreEvaluationHandoffRequest(
            result,
            maintenance: new RuntimeMaintenanceCoreHandoffInput(
                TrainingDate.From(2026, 7, 7),
                GlobalLevelId.L2,
                MaintenanceCheckKind.StandardOrTransfer,
                new StandardEvaluationResult(true, []))));
        var currency = MaintenanceCurrencyEvaluator.Evaluate(new MaintenanceCurrencyRequest(
            BranchCode.FH,
            GlobalLevelId.L2,
            TrainingDate.From(2026, 7, 7),
            [handoff.MaintenanceCheck!]));

        Assert.Equal(MaintenanceCurrencyState.Current, currency.State);
        Assert.Equal(0, currency.ConsecutiveFailures);
    }

    [Fact]
    public void MapsTransferRuntimeResultToCoreTransferEligibilityRequest()
    {
        var session = CreateSessionDefinition(
            SessionType.Transfer,
            BranchCode.WM,
            GlobalLevelId.L3,
            DrillId.WM1DelayedReconstruction);
        var definition = TransferTestCatalog.TransferTests.Single(test => test.SourceBranch == BranchCode.WM);
        var result = CompleteWithEvidence(
            "session-core-transfer",
            session,
            RuntimeEvidenceCaptureKind.Transfer,
            [
                new RuntimeEventFact("branch_mapping", "WM encoding delay and no-invention standard visible in unfamiliar content"),
                new RuntimeEventFact("score", "reconstruction accuracy met WM L3 transfer standard"),
            ]);

        var handoff = RuntimeCoreEvaluationHandoffMapper.Map(new RuntimeCoreEvaluationHandoffRequest(
            result,
            transfer: new RuntimeTransferEligibilityHandoffInput(
                GlobalLevelId.L3,
                definition.TransferTask,
                CapacityId.EncodingFidelity,
                definition.SameDemand,
                definition.ChangedContext,
                new TransferSourceStandardEvidence(
                    BranchCode.WM,
                    GlobalLevelId.L3,
                    StandardFor(BranchCode.WM, GlobalLevelId.L3),
                    visibleInTransferArtifact: true),
                new TransferRetestPlan(requiredTransferContexts: 2, usesFreshEquivalentContexts: true))));
        var eligibility = TransferEligibilityEvaluator.Evaluate(handoff.TransferEligibilityRequest!);

        Assert.True(eligibility.IsEligible);
        Assert.Empty(eligibility.Failures);
    }

    [Fact]
    public void MapsFailedRuntimeResultToCoreFailureResponseRequestWithoutInferringClassification()
    {
        var session = CreateSessionDefinition(
            SessionType.Test,
            BranchCode.FH,
            GlobalLevelId.L2,
            DrillId.FH1TargetHold);
        var result = CompleteWithEvidence(
            "session-core-failure",
            session,
            RuntimeEvidenceCaptureKind.FailedSet,
            [
                new RuntimeEventFact("error_kind", "unmarked_drift"),
                new RuntimeEventFact("failed_item_list", "set failed because drift was not marked before reset"),
            ],
            RuntimeSessionCompletionStatus.Failed);

        var handoff = RuntimeCoreEvaluationHandoffMapper.Map(new RuntimeCoreEvaluationHandoffRequest(
            result,
            failureResponse: new RuntimeFailureResponseHandoffInput(
                FailureType.Overload,
                [
                    FailureEvidenceSignal.ErrorsRiseAfterLoadIncrease,
                    FailureEvidenceSignal.ConstraintPreserved,
                ],
                isFirstFailureOfType: true,
                repeatedOverloadInSameBranch: false,
                StuckStateResponseContext.None)));
        var response = FailureResponseRouter.Route(handoff.FailureResponseRequest!);

        Assert.Equal(FailureType.Overload, response.Failure.Type);
        Assert.Contains(ProgrammingResponseAction.ReduceOneLoadVariable, response.Actions);
        Assert.Contains(ProgrammingResponseAction.TrainRegression, response.Actions);
        Assert.DoesNotContain(ProgrammingResponseAction.FailAttempt, response.Actions);
    }

    private static RuntimeSessionCompletionResult CompleteWithEvidence(
        string sessionId,
        RuntimeSessionDefinition session,
        RuntimeEvidenceCaptureKind captureKind,
        IReadOnlyList<RuntimeEventFact> facts,
        RuntimeSessionCompletionStatus status = RuntimeSessionCompletionStatus.Completed)
    {
        var log = RuntimeEventLog.Start(sessionId, session, RuntimeInstant.Zero);
        var eventKind = captureKind == RuntimeEvidenceCaptureKind.FailedSet
            ? RuntimeEventKind.ErrorRecorded
            : RuntimeEventKind.AnswerSubmitted;
        log.Append(
            eventKind,
            RuntimeDuration.FromSeconds(180).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);
        log.Append(RuntimeEventKind.SessionCompleted, RuntimeDuration.FromSeconds(185).ToInstant());

        var scoringEvents = log.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        var evidenceDraft = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            sessionId,
            session,
            SessionDate,
            captureKind,
            log.Events,
            scoringEvents));

        return RuntimeSessionCompletionResultGenerator.Generate(new RuntimeSessionCompletionResultRequest(
            sessionId,
            session,
            status,
            [CompletedPhase("active", RuntimeSessionPhaseKind.ActiveWork, 0, 185)],
            log.Events,
            scoringEvents,
            [evidenceDraft]));
    }

    private static RuntimeCompletedSessionPhase CompletedPhase(
        string id,
        RuntimeSessionPhaseKind kind,
        int startedAtSeconds,
        int completedAtSeconds)
    {
        var startedAt = RuntimeDuration.FromSeconds(startedAtSeconds).ToInstant();
        var completedAt = RuntimeDuration.FromSeconds(completedAtSeconds).ToInstant();
        return new RuntimeCompletedSessionPhase(
            RuntimeSessionPhaseDefinition.Manual(id, kind),
            startedAt,
            completedAt,
            completedAt.ElapsedSince(startedAt),
            RuntimeSessionPhaseCompletionCause.Explicit);
    }

    private static RuntimeSessionDefinition CreateSessionDefinition(
        SessionType sessionType,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill)
    {
        return new RuntimeSessionDefinition(
            sessionType,
            branch,
            level,
            drill,
            [new LoadVariable("catalog-demand", "standard fixture")],
            ProgramCatalog.Standards.Single(standard => standard.Branch == branch && standard.Level == level),
            [new CriticalConstraint(HonestyConstraintFor(drill))]);
    }

    private static StabilizationPassEvidence ExistingStabilizationPass(
        FormalTestPassState passState,
        TrainingDate date)
    {
        return new StabilizationPassEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            date,
            StandardFor(BranchCode.FH, GlobalLevelId.L1),
            passState,
            new StandardEvaluationResult(true, []),
            afterAdjacentWorkOrControlledDistractor: false,
            "Target substitution avoided.");
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
}
