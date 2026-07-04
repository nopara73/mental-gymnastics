using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeSessionCoordinatorTests
{
    private static readonly TrainingDate SessionDate = TrainingDate.From(2026, 7, 4);

    [Fact]
    public void CoordinatesSuccessfulFormalSessionThroughRuntimeCoreAndPersistenceHandoffs()
    {
        var session = CreateSessionDefinition(
            SessionType.Test,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            includeGeneratedInstance: true);

        var result = RuntimeSessionCoordinator.Complete(new RuntimeSessionCoordinatorRequest(
            "session-coordinator-success",
            session,
            RuntimeInstant.Zero,
            RuntimeDuration.FromSeconds(185).ToInstant(),
            RuntimeSessionCompletionStatus.Completed,
            RuntimeEvidenceCaptureKind.FormalAttempt,
            [CompletedPhase("active", RuntimeSessionPhaseKind.ActiveWork, 0, 185)],
            [
                new RuntimeSessionCoordinatorEventInput(
                    RuntimeEventKind.AnswerSubmitted,
                    RuntimeDuration.FromSeconds(180).ToInstant(),
                    "active",
                    RuntimeSessionPhaseKind.ActiveWork,
                    [
                        new RuntimeEventFact("score", "drifts=4; max_return=8s; no target change"),
                        new RuntimeEventFact("output_sample", "FH-1 formal set held original target with four marked drifts"),
                    ]),
            ],
            coreInputs: new RuntimeSessionCoordinatorCoreInputs(
                standardEvaluation: new RuntimeStandardEvaluationHandoffInput(
                    [new NumericMeasurement("drifts", 4)],
                    [new CriticalConstraintCheck("target-stable", true)],
                    outputComplete: true,
                    rubricOutcome: null),
                formalGate: new RuntimeFormalGateHandoffInput(
                    SessionDate,
                    new TestResultEvidence(TestResultEvidenceKind.Score, "drifts=4; max_return=8s"),
                    FormalTestPassState.PassOnce)),
            persistenceInputs: new RuntimeSessionCoordinatorPersistenceInputs(
                new RuntimePersistenceHandoffMetadata(
                    SessionDate,
                    LocalSessionIntensity.High,
                    cleanPerformance: true,
                    "Successful formal FH-1 session coordinated through runtime."),
                formalAttempt: new RuntimeFormalAttemptPersistenceInput(
                    new TestResultEvidence(TestResultEvidenceKind.Score, "drifts=4; max_return=8s"),
                    FormalTestPassState.PassOnce))));

        Assert.Equal("session-coordinator-success", result.CompletionResult.SessionId);
        Assert.Equal(RuntimeSessionCompletionStatus.Completed, result.CompletionResult.CompletionStatus);
        Assert.Contains(result.CompletionResult.RuntimeEvents, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.SessionStarted);
        Assert.Contains(result.CompletionResult.RuntimeEvents, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted);
        Assert.Contains(result.CompletionResult.RuntimeEvents, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.SessionCompleted);
        Assert.Contains(result.CompletionResult.EvidenceSummary.Categories, category => category == EvidenceArtifactCategory.Test);
        Assert.False(result.CompletionResult.ContainsAdvancementDecision);

        Assert.NotNull(result.CoreHandoff);
        var standardResult = StandardEvaluator.Evaluate(
            new EvaluatedStandard(
                "Runtime coordinator FH L1 standard",
                [NumericThreshold.AtMost("drifts", 5)],
                [new CriticalConstraintRequirement("target-stable", "No target change.")],
                requiresCompleteOutput: true,
                requiredRubric: null),
            result.CoreHandoff.StandardEvaluationAttempt!);
        var gateDecision = FormalGateDecisionEngine.Decide(
            result.CoreHandoff.FormalTestAttempt!,
            standardResult);
        Assert.True(standardResult.Passed);
        Assert.Equal(GateOutcome.PassOnce, gateDecision.Outcome);

        Assert.NotNull(result.PersistenceHandoff);
        Assert.Equal(LocalCompletedSessionType.Test, result.PersistenceHandoff.SessionHistory.SessionType);
        Assert.NotNull(result.PersistenceHandoff.FormalTestAttempt);
        Assert.Equal(FormalTestPassState.PassOnce, result.PersistenceHandoff.FormalTestAttempt.Attempt.PassState);
        Assert.NotNull(result.PersistenceHandoff.GeneratedDrillInstance);
        Assert.Equal(LocalGeneratedDrillInstanceState.Completed, result.PersistenceHandoff.GeneratedDrillInstance.State);
        Assert.Equal(Assert.Single(result.PersistenceHandoff.EvidenceArtifacts).ArtifactId, result.PersistenceHandoff.GeneratedDrillInstance.ResultEvidenceArtifactId);
    }

    [Fact]
    public void CoordinatesFailedFormalSessionThroughRuntimeCoreAndPersistenceHandoffs()
    {
        var session = CreateSessionDefinition(
            SessionType.Test,
            BranchCode.FH,
            GlobalLevelId.L2,
            DrillId.FH1TargetHold);

        var result = RuntimeSessionCoordinator.Complete(new RuntimeSessionCoordinatorRequest(
            "session-coordinator-failed",
            session,
            RuntimeInstant.Zero,
            RuntimeDuration.FromSeconds(185).ToInstant(),
            RuntimeSessionCompletionStatus.Failed,
            RuntimeEvidenceCaptureKind.FormalAttempt,
            [CompletedPhase("active", RuntimeSessionPhaseKind.ActiveWork, 0, 185)],
            [
                new RuntimeSessionCoordinatorEventInput(
                    RuntimeEventKind.ErrorRecorded,
                    RuntimeDuration.FromSeconds(180).ToInstant(),
                    "active",
                    RuntimeSessionPhaseKind.ActiveWork,
                    [
                        new RuntimeEventFact("error_kind", "unmarked_drift"),
                        new RuntimeEventFact("score", "drifts=8; critical constraint failed"),
                        new RuntimeEventFact("failed_constraint", "Target is stated before set; every drift is marked."),
                    ]),
            ],
            coreInputs: new RuntimeSessionCoordinatorCoreInputs(
                standardEvaluation: new RuntimeStandardEvaluationHandoffInput(
                    [new NumericMeasurement("drifts", 8)],
                    [new CriticalConstraintCheck("drift-marking", false)],
                    outputComplete: true,
                    rubricOutcome: null),
                formalGate: new RuntimeFormalGateHandoffInput(
                    SessionDate,
                    new TestResultEvidence(TestResultEvidenceKind.PassFail, "failed: unmarked drift"),
                    FormalTestPassState.Fail,
                    FailureType.EffortFailure),
                failureResponse: new RuntimeFailureResponseHandoffInput(
                    FailureType.EffortFailure,
                    [FailureEvidenceSignal.BrokenHonestyConstraint, FailureEvidenceSignal.MissingDriftMarks],
                    isFirstFailureOfType: true,
                    repeatedOverloadInSameBranch: false,
                    StuckStateResponseContext.None)),
            persistenceInputs: new RuntimeSessionCoordinatorPersistenceInputs(
                new RuntimePersistenceHandoffMetadata(
                    SessionDate,
                    LocalSessionIntensity.High,
                    cleanPerformance: false,
                    "Failed formal FH-2 session preserved evidence and classification."),
                formalAttempt: new RuntimeFormalAttemptPersistenceInput(
                    new TestResultEvidence(TestResultEvidenceKind.PassFail, "failed: unmarked drift"),
                    FormalTestPassState.Fail,
                    FailureType.EffortFailure))));

        Assert.Equal(RuntimeSessionCompletionStatus.Failed, result.CompletionResult.CompletionStatus);
        Assert.Contains(result.CompletionResult.FailureRelevantFacts, fact => fact.Name == "error_kind" && fact.Value == "unmarked_drift");
        Assert.Contains(result.CompletionResult.FailureRelevantFacts, fact => fact.Name == "failed_constraint");
        Assert.False(result.CompletionResult.ContainsAdvancementDecision);

        Assert.NotNull(result.CoreHandoff);
        var standardResult = StandardEvaluator.Evaluate(
            new EvaluatedStandard(
                "Runtime coordinator FH L2 standard",
                [NumericThreshold.AtMost("drifts", 5)],
                [new CriticalConstraintRequirement("drift-marking", "Every drift is marked.")],
                requiresCompleteOutput: true,
                requiredRubric: null),
            result.CoreHandoff.StandardEvaluationAttempt!);
        var gateDecision = FormalGateDecisionEngine.Decide(
            result.CoreHandoff.FormalTestAttempt!,
            standardResult);
        var failureResponse = FailureResponseRouter.Route(result.CoreHandoff.FailureResponseRequest!);
        Assert.False(standardResult.Passed);
        Assert.Equal(GateOutcome.Fail, gateDecision.Outcome);
        Assert.Contains(ProgrammingResponseAction.FailAttempt, failureResponse.Actions);
        Assert.Contains(ProgrammingResponseAction.RequireCleanPracticeArtifactBeforeRepeat, failureResponse.Actions);

        Assert.NotNull(result.PersistenceHandoff);
        Assert.NotNull(result.PersistenceHandoff.FormalTestAttempt);
        Assert.Equal(FormalTestPassState.Fail, result.PersistenceHandoff.FormalTestAttempt.Attempt.PassState);
        Assert.Equal(FailureType.EffortFailure, result.PersistenceHandoff.FormalTestAttempt.Attempt.FailureType);
        Assert.False(result.PersistenceHandoff.SessionHistory.CleanPerformance);
    }

    [Fact]
    public void CoordinatesAbandonedSessionWithoutCreatingSuccessEvidence()
    {
        var session = CreateSessionDefinition(
            SessionType.Practice,
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction);

        var result = RuntimeSessionCoordinator.Complete(new RuntimeSessionCoordinatorRequest(
            "session-coordinator-abandoned",
            session,
            RuntimeInstant.Zero,
            RuntimeDuration.FromSeconds(75).ToInstant(),
            RuntimeSessionCompletionStatus.Abandoned,
            RuntimeEvidenceCaptureKind.BestSet,
            [CompletedPhase("encode", RuntimeSessionPhaseKind.EncodeWindow, 0, 60)],
            [
                new RuntimeSessionCoordinatorEventInput(
                    RuntimeEventKind.UserAction,
                    RuntimeDuration.FromSeconds(10).ToInstant(),
                    "encode",
                    RuntimeSessionPhaseKind.EncodeWindow,
                    [
                        new RuntimeEventFact("source_items_encoded", "true"),
                        new RuntimeEventFact("source_item_count", "5"),
                    ]),
            ],
            completionFacts:
            [
                new RuntimeEventFact("abandon_reason", "stopped before reconstruction"),
            ]));

        Assert.Equal(RuntimeSessionCompletionStatus.Abandoned, result.CompletionResult.CompletionStatus);
        Assert.Empty(result.CompletionResult.EvidenceDrafts);
        Assert.Equal(0, result.CompletionResult.EvidenceSummary.ArtifactCount);
        Assert.Contains(result.CompletionResult.RuntimeEvents, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.SessionAbandoned);
        Assert.Contains(result.CompletionResult.FailureRelevantFacts, fact => fact.Name == "abandon_reason" && fact.Value == "stopped before reconstruction");
        Assert.Null(result.CoreHandoff);
        Assert.Null(result.PersistenceHandoff);
        Assert.False(result.CompletionResult.ContainsAdvancementDecision);
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
        DrillId drill,
        bool includeGeneratedInstance = false)
    {
        return new RuntimeSessionDefinition(
            sessionType,
            branch,
            level,
            drill,
            [new LoadVariable("duration", "3 minutes")],
            ProgramCatalog.Standards.Single(standard => standard.Branch == branch && standard.Level == level),
            [new CriticalConstraint(ProgramCatalog.Drills.Single(definition => definition.Id == drill).HonestyConstraint)],
            includeGeneratedInstance ? GeneratedInstance(branch, level, drill) : null);
    }

    private static RuntimeGeneratedDrillInstanceIdentity GeneratedInstance(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill)
    {
        return new RuntimeGeneratedDrillInstanceIdentity(
            "coordinator-generated-fh",
            new PromptContentIdentity(
                "coordinator-content-fh",
                branch,
                level,
                drill,
                PromptContentKind.EquivalentPrompt,
                "coordinator-fh-equivalent"),
            "v1");
    }
}
