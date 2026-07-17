using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimePersistenceHandoffTests
{
    private static readonly TrainingDate SessionDate = TrainingDate.From(2026, 7, 4);

    [Fact]
    public void MapsCompletedFormalTestResultToSessionEvidenceAttemptAndGeneratedInstanceRecords()
    {
        var session = CreateSessionDefinition(SessionType.Test, includeGeneratedInstance: true);
        var result = CompleteWithEvidence(
            "session-formal-handoff",
            session,
            RuntimeEvidenceCaptureKind.FormalAttempt,
            [
                new RuntimeEventFact("score", "drifts=4; max_return=8s; no target change"),
                new RuntimeEventFact("output_sample", "FH-1 formal set held the original target with four marked drifts"),
            ]);

        var handoff = RuntimePersistenceHandoffMapper.Map(new RuntimePersistenceHandoffRequest(
            result,
            new RuntimePersistenceHandoffMetadata(
                SessionDate,
                LocalSessionIntensity.High,
                cleanPerformance: true,
                "Formal FH-1 test completed with observable runtime evidence."),
            formalAttempt: new RuntimeFormalAttemptPersistenceInput(
                new TestResultEvidence(TestResultEvidenceKind.Score, "drifts=4; max_return=8s"),
                FormalTestPassState.PassOnce)));

        var artifact = Assert.Single(handoff.EvidenceArtifacts);
        Assert.Equal("session-formal-handoff-artifact-1", artifact.ArtifactId);
        Assert.Equal(LocalProgrammingEventKind.FormalTest, artifact.Event.Kind);
        Assert.Equal("session-formal-handoff-formal-attempt", artifact.Event.EventId);
        Assert.Equal(BranchCode.FH, artifact.Event.Branch);
        Assert.Equal(GlobalLevelId.L1, artifact.Event.Level);
        Assert.Equal(DrillId.FH1TargetHold, artifact.Event.Drill);

        Assert.Equal("session-formal-handoff", handoff.SessionHistory.SessionId);
        Assert.Equal(LocalCompletedSessionType.Test, handoff.SessionHistory.SessionType);
        Assert.Equal(LocalSessionIntensity.High, handoff.SessionHistory.Intensity);
        Assert.True(handoff.SessionHistory.CleanPerformance);
        Assert.Contains("observable runtime evidence", handoff.SessionHistory.Notes, StringComparison.Ordinal);
        Assert.Contains(handoff.SessionHistory.BranchLevels, branchLevel =>
            branchLevel.Branch == BranchCode.FH &&
            branchLevel.Level == GlobalLevelId.L1);
        Assert.Equal(DrillId.FH1TargetHold, handoff.SessionHistory.Drill);
        Assert.Equal(artifact.ArtifactId, Assert.Single(handoff.SessionHistory.EvidenceArtifactIds));

        Assert.NotNull(handoff.FormalTestAttempt);
        Assert.Equal("session-formal-handoff-formal-attempt", handoff.FormalTestAttempt.AttemptId);
        Assert.Equal(artifact.ArtifactId, handoff.FormalTestAttempt.EvidenceArtifactId);
        Assert.Equal(BranchCode.FH, handoff.FormalTestAttempt.Attempt.Branch);
        Assert.Equal(GlobalLevelId.L1, handoff.FormalTestAttempt.Attempt.Level);
        Assert.Equal(DrillId.FH1TargetHold, handoff.FormalTestAttempt.Attempt.Task.Drill);
        Assert.Null(handoff.FormalTestAttempt.Attempt.Task.TransferTask);
        Assert.Equal(FormalTestPassState.PassOnce, handoff.FormalTestAttempt.Attempt.PassState);
        Assert.Equal("drifts=4; max_return=8s", handoff.FormalTestAttempt.Attempt.ResultEvidence.Value);
        Assert.Same(artifact.Artifact, handoff.FormalTestAttempt.Attempt.Artifact);

        Assert.NotNull(handoff.GeneratedDrillInstance);
        Assert.Equal("generated-fh-1", handoff.GeneratedDrillInstance.InstanceId);
        Assert.Equal(LocalGeneratedDrillInstanceState.Completed, handoff.GeneratedDrillInstance.State);
        Assert.Equal(artifact.ArtifactId, handoff.GeneratedDrillInstance.ResultEvidenceArtifactId);
        Assert.Equal("content-fh-1", handoff.GeneratedDrillInstance.ContentIdentity.ContentId);
        Assert.Equal("fh-target-hold-equivalent", handoff.GeneratedDrillInstance.ContentIdentity.EquivalenceClass);
        Assert.Equal("v1", handoff.GeneratedDrillInstance.ContentIdentity.Version);

        Assert.Null(handoff.StabilizationPass);
        Assert.Null(handoff.MaintenanceCheck);
    }

    [Fact]
    public void MapsTransferRuntimeEvidenceToTransferSessionAndArtifactRecords()
    {
        var session = CreateSessionDefinition(SessionType.Transfer);
        var result = CompleteWithEvidence(
            "session-transfer-handoff",
            session,
            RuntimeEvidenceCaptureKind.Transfer,
            [
                new RuntimeEventFact("branch_mapping", "FH drift-return standard preserved inside changed WM reconstruction context"),
                new RuntimeEventFact("score", "transfer set met FH L1 drift and return limits"),
            ]);

        var handoff = RuntimePersistenceHandoffMapper.Map(new RuntimePersistenceHandoffRequest(
            result,
            new RuntimePersistenceHandoffMetadata(
                SessionDate,
                LocalSessionIntensity.Moderate,
                cleanPerformance: true,
                "Transfer task preserved the source FH standard in a changed context.",
                transferTask: "WM reconstruction after FH target hold")));

        var artifact = Assert.Single(handoff.EvidenceArtifacts);
        Assert.Equal(EvidenceArtifactCategory.Transfer, artifact.Artifact.Category);
        Assert.Equal(LocalProgrammingEventKind.Transfer, artifact.Event.Kind);
        Assert.Equal("session-transfer-handoff", artifact.Event.EventId);

        Assert.Equal(LocalCompletedSessionType.Transfer, handoff.SessionHistory.SessionType);
        Assert.Equal("WM reconstruction after FH target hold", handoff.SessionHistory.TransferTask);
        Assert.Equal(artifact.ArtifactId, Assert.Single(handoff.SessionHistory.EvidenceArtifactIds));
        Assert.Null(handoff.FormalTestAttempt);
    }

    [Fact]
    public void MapsStabilizationEvidenceToPersistenceRecord()
    {
        var session = CreateSessionDefinition(SessionType.Stabilization);
        var result = CompleteWithEvidence(
            "session-stabilization-handoff",
            session,
            RuntimeEvidenceCaptureKind.Stabilization,
            [
                new RuntimeEventFact("repeatability_record", "second clean pass after controlled distractor"),
                new RuntimeEventFact("score", "drifts=3; max_return=6s"),
            ]);

        var handoff = RuntimePersistenceHandoffMapper.Map(new RuntimePersistenceHandoffRequest(
            result,
            new RuntimePersistenceHandoffMetadata(
                SessionDate,
                LocalSessionIntensity.Moderate,
                cleanPerformance: true,
                "Stabilization pass kept the same FH-1 standard."),
            stabilization: new RuntimeStabilizationPersistenceInput(
                new StandardEvaluationResult(true, []),
                FormalTestPassState.StabilizationPass,
                LocalStabilizationCondition.ControlledDistractor,
                "After a short WM reconstruction set.")));

        var artifact = Assert.Single(handoff.EvidenceArtifacts);
        Assert.Equal(LocalProgrammingEventKind.Stabilization, artifact.Event.Kind);
        Assert.Equal("session-stabilization-handoff-stabilization-pass", artifact.Event.EventId);

        Assert.NotNull(handoff.StabilizationPass);
        Assert.Equal("session-stabilization-handoff-stabilization-pass", handoff.StabilizationPass.PassId);
        Assert.Equal(artifact.ArtifactId, handoff.StabilizationPass.EvidenceArtifactId);
        Assert.Equal("session-stabilization-handoff", handoff.StabilizationPass.CompletedSessionId);
        Assert.Equal(LocalStabilizationCondition.ControlledDistractor, handoff.StabilizationPass.Condition);
        Assert.Equal(FormalTestPassState.StabilizationPass, handoff.StabilizationPass.Evidence.PassState);
        Assert.True(handoff.StabilizationPass.Evidence.AfterAdjacentWorkOrControlledDistractor);

        Assert.Null(handoff.FormalTestAttempt);
        Assert.Null(handoff.MaintenanceCheck);
    }

    [Fact]
    public void MapsMaintenanceEvidenceToPersistenceRecord()
    {
        var session = CreateSessionDefinition(SessionType.Practice);
        var result = CompleteWithEvidence(
            "session-maintenance-handoff",
            session,
            RuntimeEvidenceCaptureKind.Maintenance,
            [
                new RuntimeEventFact("maintenance_check", "FH L2 reduced-volume hold check preserved drift marking"),
                new RuntimeEventFact("score", "one critical constraint broken"),
            ]);
        var standardResult = new StandardEvaluationResult(
            false,
            [new StandardEvaluationFailure(StandardFailureKind.CriticalConstraintBroken, "One drift was not marked.")]);

        var handoff = RuntimePersistenceHandoffMapper.Map(new RuntimePersistenceHandoffRequest(
            result,
            new RuntimePersistenceHandoffMetadata(
                SessionDate,
                LocalSessionIntensity.Low,
                cleanPerformance: false,
                "Maintenance check failed because a required drift mark was missing."),
            maintenance: new RuntimeMaintenancePersistenceInput(
                GlobalLevelId.L2,
                MaintenanceCheckKind.StandardOrTransfer,
                standardResult)));

        var artifact = Assert.Single(handoff.EvidenceArtifacts);
        Assert.Equal(EvidenceArtifactCategory.Maintenance, artifact.Artifact.Category);
        Assert.Equal(LocalProgrammingEventKind.Maintenance, artifact.Event.Kind);
        Assert.Equal("session-maintenance-handoff-maintenance-check", artifact.Event.EventId);

        Assert.NotNull(handoff.MaintenanceCheck);
        Assert.Equal("session-maintenance-handoff-maintenance-check", handoff.MaintenanceCheck.CheckId);
        Assert.Equal(artifact.ArtifactId, handoff.MaintenanceCheck.EvidenceArtifactId);
        Assert.Equal("session-maintenance-handoff", handoff.MaintenanceCheck.CompletedSessionId);
        Assert.Equal(GlobalLevelId.L2, handoff.MaintenanceCheck.Evidence.OwnedLevel);
        Assert.Equal(MaintenanceCheckKind.StandardOrTransfer, handoff.MaintenanceCheck.Evidence.Kind);
        Assert.False(handoff.MaintenanceCheck.Evidence.StandardEvaluationResult.Passed);
        Assert.Contains("No more than 5 marked drifts", handoff.MaintenanceCheck.Standard, StringComparison.Ordinal);

        Assert.Null(handoff.FormalTestAttempt);
        Assert.Null(handoff.StabilizationPass);
    }

    private static RuntimeSessionCompletionResult CompleteWithEvidence(
        string sessionId,
        RuntimeSessionDefinition session,
        RuntimeEvidenceCaptureKind captureKind,
        IReadOnlyList<RuntimeEventFact> facts)
    {
        var log = RuntimeEventLog.Start(sessionId, session, RuntimeInstant.Zero);
        log.Append(
            RuntimeEventKind.AnswerSubmitted,
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
            RuntimeSessionCompletionStatus.Completed,
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
        bool includeGeneratedInstance = false)
    {
        return new RuntimeSessionDefinition(
            sessionType,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")],
            new BranchLevelStandard(
                BranchCode.FH,
                GlobalLevelId.L1,
                "Hold one simple target for 3 minutes.",
                "No more than 5 marked drifts; each return within 10 seconds; no target change.",
                "Pass once enters stabilization.",
                "Repeat twice within 14 days; one after a short WM set.",
                "Hold a different target type with same standard."),
            [new CriticalConstraint("Target is stated before set; every drift is marked.")],
            includeGeneratedInstance ? GeneratedInstance() : null);
    }

    private static RuntimeGeneratedDrillInstanceIdentity GeneratedInstance()
    {
        return new RuntimeGeneratedDrillInstanceIdentity(
            "generated-fh-1",
            new PromptContentIdentity(
                "content-fh-1",
                BranchCode.FH,
                GlobalLevelId.L1,
                DrillId.FH1TargetHold,
                PromptContentKind.EquivalentPrompt,
                "fh-target-hold-equivalent"),
            "v1");
    }
}
