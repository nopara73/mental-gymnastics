using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class AffectiveInterferenceRuntimeProtocolTests
{
    [Fact]
    public void PressureRepeatRecordsOriginalBranchStandardPressureSourceStandardLoweringAndScore()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = AffectiveInterferenceRuntimeProtocol.Start(
            "ai1-session",
            AffectiveInterferenceSession(DrillId.AI1PressureRepeat, GlobalLevelId.L1),
            clock);

        var repeatBeforeStandard = protocol.StartPressureRepeat();

        Assert.False(repeatBeforeStandard.IsAccepted);
        Assert.Equal(AffectiveInterferenceRuntimeProtocolInvalidReason.SourceStandardRequiredBeforePressure, repeatBeforeStandard.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        var sourceStandard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.FH &&
            standard.Level == GlobalLevelId.L3);
        var statedStandard = protocol.StateOriginalBranchStandard(sourceStandard);
        var pressure = protocol.DefinePressureSource(new AffectiveInterferenceRuntimePressureSource(
            "pressure-1",
            "mild visible countdown",
            RuntimeDuration.FromSeconds(180),
            "countdown visible during FH L3 repeat"));
        var active = protocol.StartPressureRepeat();

        Assert.True(statedStandard.IsAccepted);
        Assert.True(pressure.IsAccepted);
        Assert.True(active.IsAccepted);
        Assert.Contains(statedStandard.Event!.Facts, fact => fact.Name == "source_branch" && fact.Value == "FH");
        Assert.Contains(statedStandard.Event.Facts, fact => fact.Name == "source_standard" && fact.Value.Contains("periodic irrelevant prompts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(pressure.Event!.Facts, fact => fact.Name == "pressure_source_defined" && fact.Value == "true");
        Assert.Contains(pressure.Event.Facts, fact => fact.Name == "pressure_source" && fact.Value == "mild visible countdown");
        Assert.Contains(active.Event!.Facts, fact => fact.Name == "original_standard_visible" && fact.Value == "true");
        Assert.Contains(active.Event.Facts, fact => fact.Name == "standard_lowering_allowed" && fact.Value == "false");

        var lowered = protocol.RecordStandardLoweringAttempt("Raised allowed drifts from 5 to 8 after the timer started.");
        var sourceScore = protocol.RecordSourceBranchScore(
            "score-1",
            "FH L3 repeat: 4 marked drifts, no distractor response, target preserved.",
            originalStandardMet: true,
            criticalConstraintBreached: false);
        var completed = protocol.CompletePressureRepeat("repeat-1");

        Assert.True(lowered.IsAccepted);
        Assert.True(sourceScore.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(lowered.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "standard_lowered_during_pressure");
        Assert.Contains(lowered.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "original_standard_cannot_be_lowered");
        Assert.Contains(sourceScore.Event!.Facts, fact => fact.Name == "source_branch_score" && fact.Value.Contains("4 marked drifts", StringComparison.Ordinal));
        Assert.Contains(sourceScore.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "score" && fact.Value.Contains("original_standard_met=true", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "standard_lowering_attempt_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "source_standard" && fact.Value.Contains("periodic irrelevant prompts", StringComparison.OrdinalIgnoreCase));

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);

        var transferEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "ai1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.Transfer,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            transferEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.BranchMapping &&
                evidence.Description.Contains("source FH", StringComparison.Ordinal));
        Assert.Contains(
            transferEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("standard_lowering_attempt_count=1", StringComparison.Ordinal));
        Assert.DoesNotContain(
            protocol.EventLog.Events.SelectMany(runtimeEvent => runtimeEvent.Facts),
            fact => fact.Name is "gate_outcome" or "pass_state");
    }

    [Fact]
    public void DisruptionRecoveryRecordsInterruptionTimingRestartAttemptAndPostDisruptionEvidence()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = AffectiveInterferenceRuntimeProtocol.Start(
            "ai2-session",
            AffectiveInterferenceSession(DrillId.AI2DisruptionRecovery, GlobalLevelId.L3),
            clock);

        var recoveryBeforePlan = protocol.StartDisruptionRecovery();

        Assert.False(recoveryBeforePlan.IsAccepted);
        Assert.Equal(AffectiveInterferenceRuntimeProtocolInvalidReason.DisruptionPlanRequiredBeforeRecovery, recoveryBeforePlan.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        var sourceStandard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.FS &&
            standard.Level == GlobalLevelId.L3);
        Assert.True(protocol.StateOriginalBranchStandard(sourceStandard).IsAccepted);
        var plan = protocol.DefineDisruptionPlan(new AffectiveInterferenceRuntimeDisruptionPlan(
            "disruption-1",
            RuntimeDuration.FromSeconds(75),
            RuntimeDuration.FromSeconds(20),
            fullRestartAllowed: false,
            "controlled interruption after the first cue block"));
        var active = protocol.StartDisruptionRecovery();

        Assert.True(plan.IsAccepted);
        Assert.True(active.IsAccepted);
        Assert.Contains(plan.Event!.Facts, fact => fact.Name == "interruption_planned_at" && fact.Value == "00:01:15");
        Assert.Contains(plan.Event.Facts, fact => fact.Name == "full_restart_allowed" && fact.Value == "false");

        clock.AdvanceBy(RuntimeDuration.FromSeconds(75));
        var interruption = protocol.RecordInterruption("unexpected evaluator prompt");
        var restart = protocol.RecordFullRestartAttempt("Restarted the whole cue sequence after interruption.");
        clock.AdvanceBy(RuntimeDuration.FromSeconds(12));
        var recovery = protocol.RecordPostDisruptionRecovery(
            "recovery-1",
            "resumed at the next valid cue and finished above the FS L3 threshold",
            postDisruptionAboveThreshold: true);
        var completed = protocol.CompleteDisruptionRecovery("set-1");

        Assert.True(interruption.IsAccepted);
        Assert.True(restart.IsAccepted);
        Assert.True(recovery.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(interruption.Event!.Facts, fact => fact.Name == "interruption_actual_at" && fact.Value == "00:01:15");
        Assert.Contains(interruption.Event.Facts, fact => fact.Name == "interruption_timing" && fact.Value == "planned");
        Assert.Contains(restart.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "full_restart_attempt");
        Assert.Contains(restart.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "no_full_restart_unless_allowed");
        Assert.Contains(recovery.Event!.Facts, fact => fact.Name == "recovery_duration" && fact.Value == "00:00:12");
        Assert.Contains(recovery.Event.Facts, fact => fact.Name == "recovery_within_window" && fact.Value == "true");
        Assert.Contains(recovery.Event.Facts, fact => fact.Name == "post_disruption_evidence" && fact.Value.Contains("finished above", StringComparison.Ordinal));
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "score" && fact.Value.Contains("post_disruption_above_threshold=true", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "full_restart_attempt_count" && fact.Value == "1");

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);

        var transferEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "ai2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.Transfer,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            transferEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.BranchMapping &&
                evidence.Description.Contains("source FS", StringComparison.Ordinal));
        Assert.Contains(
            transferEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("full_restart_attempt_count=1", StringComparison.Ordinal));
    }

    [Fact]
    public void AffectiveInterferenceProtocolRejectsWrongSessionsAndDisruptionForPressureRepeatWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);

        Assert.Throws<ArgumentException>(() => AffectiveInterferenceRuntimeProtocol.Start(
            "wrong-branch",
            FocusHoldSession(),
            clock));

        var pressureRepeat = AffectiveInterferenceRuntimeProtocol.Start(
            "ai1-no-disruption",
            AffectiveInterferenceSession(DrillId.AI1PressureRepeat, GlobalLevelId.L1),
            clock);
        Assert.True(pressureRepeat.StateOriginalBranchStandard(ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.FH &&
            standard.Level == GlobalLevelId.L3)).IsAccepted);
        Assert.True(pressureRepeat.DefinePressureSource(new AffectiveInterferenceRuntimePressureSource(
            "pressure-1",
            "mild time pressure",
            RuntimeDuration.FromSeconds(180),
            "countdown visible")).IsAccepted);
        Assert.True(pressureRepeat.StartPressureRepeat().IsAccepted);
        var eventCountBeforeDisruption = pressureRepeat.EventLog.Events.Count;

        var disruptionPlan = pressureRepeat.DefineDisruptionPlan(new AffectiveInterferenceRuntimeDisruptionPlan(
            "disruption-1",
            RuntimeDuration.FromSeconds(30),
            RuntimeDuration.FromSeconds(10),
            fullRestartAllowed: false,
            "not part of AI-1"));

        Assert.False(disruptionPlan.IsAccepted);
        Assert.Equal(AffectiveInterferenceRuntimeProtocolInvalidReason.DisruptionRecoveryNotSupportedByDrill, disruptionPlan.InvalidReason);
        Assert.Equal(eventCountBeforeDisruption, pressureRepeat.EventLog.Events.Count);
    }

    private static RuntimeSessionDefinition AffectiveInterferenceSession(DrillId drill, GlobalLevelId level)
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.AI &&
            standard.Level == level);
        CriticalConstraint[] constraints = drill == DrillId.AI2DisruptionRecovery
            ?
            [
                new CriticalConstraint("Original branch standard must remain visible."),
                new CriticalConstraint("No full restart unless specified."),
                new CriticalConstraint("Post-disruption evidence must be recorded."),
            ]
            :
            [
                new CriticalConstraint("Original standard cannot be lowered."),
                new CriticalConstraint("Pressure source must be defined before the repeat."),
            ];
        LoadVariable[] loadVariables = drill == DrillId.AI2DisruptionRecovery
            ?
            [
                new LoadVariable("interruption timing", "75 seconds"),
                new LoadVariable("restart delay", "20 second recovery window"),
            ]
            :
            [
                new LoadVariable("time pressure", "180 second countdown"),
                new LoadVariable("pressure", "visible timer"),
            ];

        return new RuntimeSessionDefinition(
            SessionType.Transfer,
            BranchCode.AI,
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
