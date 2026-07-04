using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class TransferIntegrationRuntimeProtocolTests
{
    [Fact]
    public void CompositeTaskRecordsSeparateComponentEvidenceAndCannotHideWeakComponent()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = TransferIntegrationRuntimeProtocol.Start(
            "ti1-session",
            TransferIntegrationSession(DrillId.TI1CompositeTask, GlobalLevelId.L1),
            clock);

        var activeBeforeComponents = protocol.StartCompositeTask();

        Assert.False(activeBeforeComponents.IsAccepted);
        Assert.Equal(TransferIntegrationRuntimeProtocolInvalidReason.ComponentBranchesRequiredBeforeComposite, activeBeforeComponents.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        var components = protocol.StateComponentBranches(
            [
                new TransferIntegrationRuntimeComponent("fh", SourceStandard(BranchCode.FH, GlobalLevelId.L3)),
                new TransferIntegrationRuntimeComponent("wm", SourceStandard(BranchCode.WM, GlobalLevelId.L3)),
            ]);
        var active = protocol.StartCompositeTask();

        Assert.True(components.IsAccepted);
        Assert.True(active.IsAccepted);
        Assert.Contains(components.Event!.Facts, fact => fact.Name == "component_count" && fact.Value == "2");
        Assert.Contains(components.Event.Facts, fact => fact.Name == "branch_specific_evidence_required" && fact.Value == "true");
        Assert.Contains(active.Event!.Facts, fact => fact.Name == "strong_component_cannot_hide_weak_component" && fact.Value == "true");

        var focusEvidence = protocol.RecordComponentEvidence(
            "fh",
            "FH L3: target held through irrelevant prompts with 4 marked drifts.",
            branchStandardMet: true,
            criticalConstraintBreached: false);
        var memoryEvidence = protocol.RecordComponentEvidence(
            "wm",
            "WM L3: transformed only 3 of 6 items after delay.",
            branchStandardMet: false,
            criticalConstraintBreached: false);
        var completed = protocol.CompleteCompositeTask("composite-1");

        Assert.True(focusEvidence.IsAccepted);
        Assert.True(memoryEvidence.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(focusEvidence.Event!.Facts, fact => fact.Name == "component_branch" && fact.Value == "FH");
        Assert.Contains(focusEvidence.Event.Facts, fact => fact.Name == "branch_specific_evidence" && fact.Value.Contains("target held", StringComparison.Ordinal));
        Assert.Contains(focusEvidence.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(memoryEvidence.Event!.Facts, fact => fact.Name == "component_branch" && fact.Value == "WM");
        Assert.Contains(memoryEvidence.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "incorrect");
        Assert.Contains(memoryEvidence.Event.Facts, fact => fact.Name == "error_kind" && fact.Value == "component_branch_below_passing");
        Assert.Contains(memoryEvidence.Event.Facts, fact => fact.Name == "failed_item_list" && fact.Value.Contains("WM", StringComparison.Ordinal));
        Assert.Contains(memoryEvidence.Event.Facts, fact => fact.Name == "strong_component_cannot_hide_weak_component" && fact.Value == "true");
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "component_pass_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "component_fail_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "all_component_standards_met" && fact.Value == "false");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "branch_mapping" && fact.Value.Contains("FH:L3", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "branch_mapping" && fact.Value.Contains("WM:L3", StringComparison.Ordinal));

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);

        var transferEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "ti1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.Transfer,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            transferEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.BranchMapping &&
                evidence.Description.Contains("WM:L3", StringComparison.Ordinal));
        Assert.Contains(
            transferEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("component_fail_count=1", StringComparison.Ordinal));
        Assert.DoesNotContain(
            protocol.EventLog.Events.SelectMany(runtimeEvent => runtimeEvent.Facts),
            fact => fact.Name is "gate_outcome" or "pass_state");
    }

    [Fact]
    public void GlobalReviewTaskRecordsComponentsAuditAndDelayedReconstructionEvidence()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = TransferIntegrationRuntimeProtocol.Start(
            "ti2-session",
            TransferIntegrationSession(DrillId.TI2GlobalReviewTask, GlobalLevelId.L5),
            clock);

        var activeBeforeComponents = protocol.StartGlobalReviewTask();

        Assert.False(activeBeforeComponents.IsAccepted);
        Assert.Equal(TransferIntegrationRuntimeProtocolInvalidReason.ComponentBranchesRequiredBeforeGlobalReview, activeBeforeComponents.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        Assert.True(protocol.StateComponentBranches(
            [
                new TransferIntegrationRuntimeComponent("fh", SourceStandard(BranchCode.FH, GlobalLevelId.L5)),
                new TransferIntegrationRuntimeComponent("wm", SourceStandard(BranchCode.WM, GlobalLevelId.L5)),
                new TransferIntegrationRuntimeComponent("de", SourceStandard(BranchCode.DE, GlobalLevelId.L5)),
            ]).IsAccepted);
        Assert.True(protocol.StartGlobalReviewTask().IsAccepted);

        Assert.True(protocol.RecordComponentEvidence(
            "fh",
            "FH L5: target preserved during integrated pressure task.",
            branchStandardMet: true,
            criticalConstraintBreached: false).IsAccepted);
        Assert.True(protocol.RecordComponentEvidence(
            "wm",
            "WM L5: critical information preserved through the review task.",
            branchStandardMet: true,
            criticalConstraintBreached: false).IsAccepted);
        Assert.True(protocol.RecordComponentEvidence(
            "de",
            "DE L5: critical errors found during timed audit.",
            branchStandardMet: true,
            criticalConstraintBreached: false).IsAccepted);

        var eventCountBeforeIncompleteCompletion = protocol.EventLog.Events.Count;
        var incompleteCompletion = protocol.CompleteGlobalReviewTask("review-1");
        Assert.False(incompleteCompletion.IsAccepted);
        Assert.Equal(TransferIntegrationRuntimeProtocolInvalidReason.AuditEvidenceRequiredBeforeGlobalReviewCompletion, incompleteCompletion.InvalidReason);
        Assert.Equal(eventCountBeforeIncompleteCompletion, protocol.EventLog.Events.Count);

        var audit = protocol.RecordAuditEvidence(
            "audit-1",
            "Audit found all critical errors and bounded uncertainty.",
            auditPassed: true);
        var delayed = protocol.RecordDelayedReconstruction(
            "reconstruction-1",
            RuntimeDuration.FromSeconds(300),
            "Delayed reconstruction preserved the task model and critical assumptions.",
            reconstructionPassed: true);
        var completed = protocol.CompleteGlobalReviewTask("review-1");

        Assert.True(audit.IsAccepted);
        Assert.True(delayed.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(audit.Event!.Facts, fact => fact.Name == "audit_required" && fact.Value == "true");
        Assert.Contains(audit.Event.Facts, fact => fact.Name == "audit_result" && fact.Value.Contains("critical errors", StringComparison.Ordinal));
        Assert.Contains(delayed.Event!.Facts, fact => fact.Name == "delayed_reconstruction_required" && fact.Value == "true");
        Assert.Contains(delayed.Event.Facts, fact => fact.Name == "delayed_reconstruction" && fact.Value.Contains("critical assumptions", StringComparison.Ordinal));
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "global_review_summary" && fact.Value.Contains("components=FH,WM,DE", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "audit_passed" && fact.Value == "true");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "delayed_reconstruction_passed" && fact.Value == "true");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "all_component_standards_met" && fact.Value == "true");

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);

        var globalReviewEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "ti2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.GlobalReview,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Equal(EvidenceArtifactCategory.GlobalReview, globalReviewEvidence.Artifact.Category);
        Assert.Contains(
            globalReviewEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.GlobalReviewSummary &&
                evidence.Description.Contains("components=FH,WM,DE", StringComparison.Ordinal));
        Assert.Contains(
            globalReviewEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.AuditResult &&
                evidence.Description.Contains("critical errors", StringComparison.Ordinal));
        Assert.Contains(
            globalReviewEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.DelayedReconstruction &&
                evidence.Description.Contains("critical assumptions", StringComparison.Ordinal));
    }

    [Fact]
    public void TransferIntegrationProtocolRejectsWrongSessionsAndAuditForCompositeWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);

        Assert.Throws<ArgumentException>(() => TransferIntegrationRuntimeProtocol.Start(
            "wrong-branch",
            FocusHoldSession(),
            clock));

        var composite = TransferIntegrationRuntimeProtocol.Start(
            "ti1-no-audit",
            TransferIntegrationSession(DrillId.TI1CompositeTask, GlobalLevelId.L1),
            clock);
        Assert.True(composite.StateComponentBranches(
            [
                new TransferIntegrationRuntimeComponent("fh", SourceStandard(BranchCode.FH, GlobalLevelId.L3)),
                new TransferIntegrationRuntimeComponent("wm", SourceStandard(BranchCode.WM, GlobalLevelId.L3)),
            ]).IsAccepted);
        Assert.True(composite.StartCompositeTask().IsAccepted);
        var eventCountBeforeAudit = composite.EventLog.Events.Count;

        var audit = composite.RecordAuditEvidence(
            "audit-1",
            "not part of TI-1",
            auditPassed: true);

        Assert.False(audit.IsAccepted);
        Assert.Equal(TransferIntegrationRuntimeProtocolInvalidReason.GlobalReviewTaskNotSupportedByDrill, audit.InvalidReason);
        Assert.Equal(eventCountBeforeAudit, composite.EventLog.Events.Count);
    }

    private static RuntimeSessionDefinition TransferIntegrationSession(DrillId drill, GlobalLevelId level)
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.TI &&
            standard.Level == level);
        CriticalConstraint[] constraints = drill == DrillId.TI2GlobalReviewTask
            ?
            [
                new CriticalConstraint("Each component branch must leave separate evidence."),
                new CriticalConstraint("Audit and delayed reconstruction are required."),
                new CriticalConstraint("Strong component cannot hide weak component."),
            ]
            :
            [
                new CriticalConstraint("Each component branch must leave separate evidence."),
                new CriticalConstraint("Strong component cannot hide weak component."),
            ];
        LoadVariable[] loadVariables = drill == DrillId.TI2GlobalReviewTask
            ?
            [
                new LoadVariable("branch count", "3"),
                new LoadVariable("task length", "global review task"),
                new LoadVariable("delay", "5 minutes"),
            ]
            :
            [
                new LoadVariable("branch count", "2"),
                new LoadVariable("task length", "10 minutes"),
                new LoadVariable("transfer distance", "near transfer"),
            ];

        return new RuntimeSessionDefinition(
            SessionType.Transfer,
            BranchCode.TI,
            level,
            drill,
            loadVariables,
            standard,
            constraints);
    }

    private static BranchLevelStandard SourceStandard(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards.Single(standard =>
            standard.Branch == branch &&
            standard.Level == level);
    }

    private static RuntimeSessionDefinition FocusHoldSession()
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.FH &&
            standard.Level == GlobalLevelId.L1);

        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [new LoadVariable("duration", "3 minutes")],
            standard,
            [new CriticalConstraint("Target is stated before set; every drift is marked.")]);
    }
}
