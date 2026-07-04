using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeEvidenceCaptureTests
{
    [Fact]
    public void CapturesBestFailedAndBottleneckPracticeEvidenceFromRuntimeEvents()
    {
        var session = CreateSessionDefinition(SessionType.Practice);
        var log = RuntimeEventLog.Start("session-practice-evidence", session, RuntimeInstant.Zero);
        log.Append(
            RuntimeEventKind.AnswerSubmitted,
            RuntimeDuration.FromSeconds(30).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                new RuntimeEventFact("output_sample", "set 2: target held; 4 marked drifts; max return 8 seconds"),
                new RuntimeEventFact("score", "drifts=4; max_return=8s"),
            ]);
        var failedEvent = log.Append(
            RuntimeEventKind.ErrorRecorded,
            RuntimeDuration.FromSeconds(60).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                new RuntimeEventFact("error_kind", "unmarked_drift"),
                new RuntimeEventFact("failed_item_list", "set 3: unmarked drift detected during audit"),
            ]);
        log.Append(
            RuntimeEventKind.UserAction,
            RuntimeDuration.FromSeconds(90).ToInstant(),
            "review",
            RuntimeSessionPhaseKind.Review,
            [new RuntimeEventFact("bottleneck_note", "FH return after drift: return time lengthened after the second drift")]);
        var scoringEvents = new[] { RuntimeScoringEventFactory.FromRuntimeEvent(failedEvent)! };

        var bestSet = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-practice-evidence",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.BestSet,
            log.Events,
            scoringEvents));
        var failedSet = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-practice-evidence",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            log.Events,
            scoringEvents));
        var bottleneck = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-practice-evidence",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.BottleneckNote,
            log.Events,
            scoringEvents));

        Assert.Equal("session-practice-evidence", bestSet.SessionId);
        Assert.Same(session, bestSet.SessionDefinition);
        Assert.Equal(RuntimeEvidenceCaptureKind.BestSet, bestSet.CaptureKind);
        Assert.Equal(EvidenceArtifactCategory.Practice, bestSet.Artifact.Category);
        Assert.Contains(bestSet.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.OutputSample, "set 2"));
        Assert.Contains(bestSet.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.Score, "drifts=4"));
        Assert.DoesNotContain(bestSet.Artifact.ObservableEvidence, evidence => evidence.Kind == ObservableEvidenceKind.BottleneckNote);

        Assert.Equal(EvidenceArtifactCategory.Practice, failedSet.Artifact.Category);
        Assert.Contains(failedSet.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.FailedItemList, "unmarked drift"));
        Assert.Contains(failedSet.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.ErrorCount, "unmarked_drift=1"));

        Assert.Equal(EvidenceArtifactCategory.Practice, bottleneck.Artifact.Category);
        var bottleneckEvidence = Assert.Single(bottleneck.Artifact.ObservableEvidence);
        Assert.Equal(ObservableEvidenceKind.BottleneckNote, bottleneckEvidence.Kind);
        Assert.Contains("return time lengthened", bottleneckEvidence.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void CapturesFormalStabilizationTransferMaintenanceAndAuditEvidence()
    {
        var formalSession = CreateSessionDefinition(SessionType.Test);
        var formalLog = RuntimeEventLog.Start("session-formal-evidence", formalSession, RuntimeInstant.Zero);
        formalLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            RuntimeDuration.FromSeconds(180).ToInstant(),
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [new RuntimeEventFact("score", "drifts=4; max_return=8s; no target change")]);

        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-formal-evidence",
            formalSession,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            formalLog.Events,
            []));

        Assert.Equal(EvidenceArtifactCategory.Test, formalEvidence.Artifact.Category);
        Assert.Contains(formalEvidence.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.Score, "drifts=4"));
        Assert.Contains(formalEvidence.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.LoadVariableRecord, "duration=3 minutes"));
        Assert.Contains(formalEvidence.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.CriticalConstraintRecord, "every drift is marked"));

        var stabilizationEvidence = CaptureSingleFact(
            "session-stabilization-evidence",
            CreateSessionDefinition(SessionType.Stabilization),
            RuntimeEvidenceCaptureKind.Stabilization,
            "repeatability_record",
            "pass 2 after controlled distractor; standard unchanged");
        var transferEvidence = CaptureSingleFact(
            "session-transfer-evidence",
            CreateSessionDefinition(SessionType.Transfer),
            RuntimeEvidenceCaptureKind.Transfer,
            "branch_mapping",
            "source FH drift and return standard visible inside WM reconstruction task");
        var maintenanceEvidence = CaptureSingleFact(
            "session-maintenance-evidence",
            CreateSessionDefinition(SessionType.Practice),
            RuntimeEvidenceCaptureKind.Maintenance,
            "maintenance_check",
            "FH L2 reduced-volume hold check; critical constraint preserved");
        var auditEvidence = CaptureSingleFact(
            "session-audit-evidence",
            CreateSessionDefinition(SessionType.Test),
            RuntimeEvidenceCaptureKind.Audit,
            "audit_result",
            "audit found 2 of 2 seeded errors and no false corrections");

        Assert.Equal(EvidenceArtifactCategory.Stabilization, stabilizationEvidence.Artifact.Category);
        Assert.Contains(stabilizationEvidence.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.RepeatabilityRecord, "controlled distractor"));

        Assert.Equal(EvidenceArtifactCategory.Transfer, transferEvidence.Artifact.Category);
        Assert.Contains(transferEvidence.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.BranchMapping, "source FH"));

        Assert.Equal(EvidenceArtifactCategory.Maintenance, maintenanceEvidence.Artifact.Category);
        Assert.Contains(maintenanceEvidence.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.MaintenanceCheck, "reduced-volume"));

        Assert.Equal(EvidenceArtifactCategory.Test, auditEvidence.Artifact.Category);
        Assert.Contains(auditEvidence.Artifact.ObservableEvidence, EvidenceIs(ObservableEvidenceKind.AuditResult, "2 of 2 seeded errors"));
    }

    [Fact]
    public void EvidenceCaptureRejectsMissingObservableFactsAndProgressionDecisionFacts()
    {
        var session = CreateSessionDefinition(SessionType.Practice);

        Assert.Throws<ArgumentException>(() => new RuntimeEvidenceCaptureRequest(
            " ",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.BestSet,
            [],
            []));

        Assert.Throws<InvalidOperationException>(() => RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-empty-evidence",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.BestSet,
            [],
            [])));

        var log = RuntimeEventLog.Start("session-invalid-evidence", session, RuntimeInstant.Zero);
        log.Append(
            RuntimeEventKind.UserAction,
            RuntimeDuration.FromSeconds(1).ToInstant(),
            "review",
            RuntimeSessionPhaseKind.Review,
            [new RuntimeEventFact("gate_outcome", "pass_once")]);

        Assert.Throws<ArgumentException>(() => RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-invalid-evidence",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            log.Events,
            [])));

        Assert.Throws<InvalidOperationException>(() => RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "session-wrong-evidence",
            session,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.BottleneckNote,
            [
                new RuntimeEvent(
                    "session-wrong-evidence",
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    RuntimeDuration.FromSeconds(1).ToInstant(),
                    facts: [new RuntimeEventFact("output_sample", "set completed with 4 drifts")]),
            ],
            [])));
    }

    private static RuntimeEvidenceDraft CaptureSingleFact(
        string sessionId,
        RuntimeSessionDefinition session,
        RuntimeEvidenceCaptureKind captureKind,
        string factName,
        string factValue)
    {
        var log = RuntimeEventLog.Start(sessionId, session, RuntimeInstant.Zero);
        log.Append(
            RuntimeEventKind.UserAction,
            RuntimeDuration.FromSeconds(30).ToInstant(),
            "review",
            RuntimeSessionPhaseKind.Review,
            [new RuntimeEventFact(factName, factValue)]);

        return RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            sessionId,
            session,
            TrainingDate.From(2026, 7, 4),
            captureKind,
            log.Events,
            []));
    }

    private static Predicate<ObservableEvidence> EvidenceIs(
        ObservableEvidenceKind expectedKind,
        string expectedText)
    {
        return evidence =>
            evidence.Kind == expectedKind &&
            evidence.Description.Contains(expectedText, StringComparison.Ordinal);
    }

    private static RuntimeSessionDefinition CreateSessionDefinition(SessionType sessionType)
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
            [new CriticalConstraint("Target is stated before set; every drift is marked.")]);
    }
}
