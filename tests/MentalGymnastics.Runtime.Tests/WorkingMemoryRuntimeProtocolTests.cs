using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class WorkingMemoryRuntimeProtocolTests
{
    [Fact]
    public void DelayedReconstructionRecordsEncodeDelayReconstructionAndInventedItemFacts()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = WorkingMemoryRuntimeProtocol.Start(
            "wm1-session",
            WorkingMemorySession(DrillId.WM1DelayedReconstruction, GlobalLevelId.L1),
            clock);

        var reconstructionBeforeEncode = protocol.SubmitReconstruction(
            "reconstruction-too-early",
            [new WorkingMemoryRuntimeReconstructionItem("item-1", "red square")]);

        Assert.False(reconstructionBeforeEncode.IsAccepted);
        Assert.Equal(
            WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowRequiredBeforeReconstruction,
            reconstructionBeforeEncode.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        Assert.True(protocol.StartEncodeWindow(RuntimeDuration.FromSeconds(30)).IsAccepted);
        Assert.True(protocol.EncodeSourceItems(
            [
                new WorkingMemoryRuntimeItem("item-1", "red square"),
                new WorkingMemoryRuntimeItem("item-2", "blue circle"),
                new WorkingMemoryRuntimeItem("item-3", "green triangle"),
                new WorkingMemoryRuntimeItem("item-4", "yellow line"),
                new WorkingMemoryRuntimeItem("item-5", "black dot"),
            ]).IsAccepted);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var closedEncode = protocol.CloseEncodeWindow();
        Assert.True(closedEncode.IsAccepted);
        Assert.Equal(RuntimeSessionPhaseKind.EncodeWindow, closedEncode.Event!.PhaseKind);
        Assert.Contains(closedEncode.Event.Facts, fact => fact.Name == "rereading_after_encode_allowed" && fact.Value == "false");

        var reread = protocol.AttemptRereadAfterEncode("looked back at source card");
        Assert.True(reread.IsAccepted);
        Assert.Contains(reread.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "reread_after_encode");
        Assert.Contains(reread.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "no_rereading_after_encode");

        var delayStarted = protocol.StartDelayWindow(RuntimeDuration.FromSeconds(60));
        Assert.True(delayStarted.IsAccepted);
        Assert.Equal(RuntimeSessionPhaseKind.DelayWindow, delayStarted.Event!.PhaseKind);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(60));
        Assert.True(protocol.CompleteDelayWindow().IsAccepted);

        var reconstruction = protocol.SubmitReconstruction(
            "reconstruction-1",
            [
                new WorkingMemoryRuntimeReconstructionItem("item-1", "red square"),
                new WorkingMemoryRuntimeReconstructionItem("item-2", "blue circle"),
                new WorkingMemoryRuntimeReconstructionItem("item-3", "green triangle"),
                new WorkingMemoryRuntimeReconstructionItem("item-4", "yellow line"),
                new WorkingMemoryRuntimeReconstructionItem("invented-1", "purple star"),
            ]);

        Assert.True(reconstruction.IsAccepted);
        Assert.Equal(RuntimeEventKind.AnswerSubmitted, reconstruction.Event!.Kind);
        Assert.Equal(RuntimeSessionPhaseKind.ReconstructionInput, reconstruction.Event.PhaseKind);
        Assert.Contains(reconstruction.Event.Facts, fact => fact.Name == "exact_match_count" && fact.Value == "4");
        Assert.Contains(reconstruction.Event.Facts, fact => fact.Name == "omission_count" && fact.Value == "1");
        Assert.Contains(reconstruction.Event.Facts, fact => fact.Name == "invented_item_count" && fact.Value == "1");
        Assert.Contains(reconstruction.Event.Facts, fact => fact.Name == "reconstruction_accuracy" && fact.Value == "4/5");
        Assert.Contains(reconstruction.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "no_invented_items");
        Assert.Contains(reconstruction.Event.Facts, fact => fact.Name == "failed_item_list" && fact.Value.Contains("invented-1", StringComparison.Ordinal));
        Assert.Contains(reconstruction.Event.Facts, fact => fact.Name == "reconstruction" && fact.Value.Contains("item-1=red square", StringComparison.Ordinal));

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);

        var failedEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "wm1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            protocol.EventLog.Events,
            scoringEvents));
        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "wm1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            failedEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.FailedItemList &&
                evidence.Description.Contains("looked back", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Reconstruction &&
                evidence.Description.Contains("item-1=red square", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("reconstruction_accuracy=4/5", StringComparison.Ordinal));
        Assert.DoesNotContain(
            protocol.EventLog.Events.SelectMany(runtimeEvent => runtimeEvent.Facts),
            fact => fact.Name is "gate_outcome" or "pass_state");
    }

    [Fact]
    public void MentalTransformRecordsRuleDelayRuleExplanationAndHiddenNoteFailures()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = WorkingMemoryRuntimeProtocol.Start(
            "wm2-session",
            WorkingMemorySession(DrillId.WM2MentalTransform, GlobalLevelId.L3),
            clock);

        Assert.True(protocol.StartEncodeWindow(RuntimeDuration.FromSeconds(20)).IsAccepted);
        Assert.True(protocol.EncodeSourceItems(
            [
                new WorkingMemoryRuntimeItem("source-1", "2"),
                new WorkingMemoryRuntimeItem("source-2", "4"),
                new WorkingMemoryRuntimeItem("source-3", "6"),
                new WorkingMemoryRuntimeItem("source-4", "8"),
                new WorkingMemoryRuntimeItem("source-5", "10"),
                new WorkingMemoryRuntimeItem("source-6", "12"),
            ]).IsAccepted);
        Assert.True(protocol.StateTransformRule("reverse order, then subtract two").IsAccepted);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(20));
        Assert.True(protocol.CloseEncodeWindow().IsAccepted);
        Assert.True(protocol.StartDelayWindow(RuntimeDuration.FromSeconds(120)).IsAccepted);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var hiddenNote = protocol.RecordHiddenIntermediateNote("wrote intermediate transformed list");
        Assert.True(hiddenNote.IsAccepted);
        Assert.Contains(hiddenNote.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "hidden_intermediate_notes");
        Assert.Contains(hiddenNote.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "intermediate_notes_prohibited");

        clock.AdvanceBy(RuntimeDuration.FromSeconds(90));
        Assert.True(protocol.CompleteDelayWindow().IsAccepted);

        var transform = protocol.SubmitMentalTransform(
            "transform-1",
            "10,8,6,4,2,0",
            "I reversed the held list and subtracted two from each item.",
            correctOperationCount: 5,
            expectedOperationCount: 6);

        Assert.True(transform.IsAccepted);
        Assert.Equal(RuntimeEventKind.AnswerSubmitted, transform.Event!.Kind);
        Assert.Equal(RuntimeSessionPhaseKind.ReconstructionInput, transform.Event.PhaseKind);
        Assert.Contains(transform.Event.Facts, fact => fact.Name == "transform_rule" && fact.Value == "reverse order, then subtract two");
        Assert.Contains(transform.Event.Facts, fact => fact.Name == "final_output" && fact.Value == "10,8,6,4,2,0");
        Assert.Contains(transform.Event.Facts, fact => fact.Name == "rule_explanation" && fact.Value.Contains("reversed", StringComparison.Ordinal));
        Assert.Contains(transform.Event.Facts, fact => fact.Name == "operation_accuracy" && fact.Value == "5/6");
        Assert.Contains(transform.Event.Facts, fact => fact.Name == "hidden_intermediate_note_count" && fact.Value == "1");
        Assert.Contains(transform.Event.Facts, fact => fact.Name == "score" && fact.Value.Contains("operation_accuracy=5/6", StringComparison.Ordinal));

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);

        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "wm2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.RuleExplanation &&
                evidence.Description.Contains("subtracted two", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.CriticalConstraintRecord &&
                evidence.Description.Contains("Intermediate notes prohibited", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("hidden_intermediate_note_count=1", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkingMemoryProtocolRejectsWrongSessionsAndTransformForDelayedReconstructionWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);

        Assert.Throws<ArgumentException>(() => WorkingMemoryRuntimeProtocol.Start(
            "wrong-branch",
            FocusHoldSession(),
            clock));

        var delayedReconstruction = WorkingMemoryRuntimeProtocol.Start(
            "wm1-no-transform",
            WorkingMemorySession(DrillId.WM1DelayedReconstruction, GlobalLevelId.L1),
            clock);
        var eventCountBeforeTransform = delayedReconstruction.EventLog.Events.Count;

        var transformRule = delayedReconstruction.StateTransformRule("not part of WM-1");
        var hiddenNote = delayedReconstruction.RecordHiddenIntermediateNote("not part of WM-1");

        Assert.False(transformRule.IsAccepted);
        Assert.Equal(
            WorkingMemoryRuntimeProtocolInvalidReason.MentalTransformNotSupportedByDrill,
            transformRule.InvalidReason);
        Assert.False(hiddenNote.IsAccepted);
        Assert.Equal(
            WorkingMemoryRuntimeProtocolInvalidReason.MentalTransformNotSupportedByDrill,
            hiddenNote.InvalidReason);
        Assert.Equal(eventCountBeforeTransform, delayedReconstruction.EventLog.Events.Count);
    }

    private static RuntimeSessionDefinition WorkingMemorySession(DrillId drill, GlobalLevelId level)
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.WM &&
            standard.Level == level);
        CriticalConstraint[] constraints = drill == DrillId.WM2MentalTransform
            ?
            [
                new CriticalConstraint("No rereading after encode window."),
                new CriticalConstraint("No invented items."),
                new CriticalConstraint("Intermediate notes prohibited unless specified."),
            ]
            :
            [
                new CriticalConstraint("No rereading after encode window."),
                new CriticalConstraint("No invented items."),
            ];
        LoadVariable[] loadVariables = drill == DrillId.WM2MentalTransform
            ?
            [
                new LoadVariable("item count", "6 held items"),
                new LoadVariable("delay", "120 seconds"),
                new LoadVariable("operation steps", "6 transformations"),
            ]
            :
            [
                new LoadVariable("item count", "5 simple items"),
                new LoadVariable("delay", "60 seconds"),
            ];

        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.WM,
            level,
            drill,
            loadVariables,
            standard,
            constraints);
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
