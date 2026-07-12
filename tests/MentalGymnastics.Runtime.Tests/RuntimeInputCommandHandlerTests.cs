using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeInputCommandHandlerTests
{
    [Fact]
    public void HandlerAcceptsPhaseAppropriateCommandsAndRecordsEventsInOrder()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
                RuntimeSessionPhaseDefinition.Manual("cue", RuntimeSessionPhaseKind.CueResponse),
                RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
                RuntimeSessionPhaseDefinition.Manual("audit", RuntimeSessionPhaseKind.Audit),
            ]),
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var drift = handler.Handle(RuntimeInputCommand.MarkDrift("drift-1"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var answer = handler.Handle(RuntimeInputCommand.SubmitAnswer("answer-1", "target held"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var guess = handler.Handle(RuntimeInputCommand.MarkGuess("guess-1"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var error = handler.Handle(RuntimeInputCommand.MarkError("error-1", "incorrect_response"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var correction = handler.Handle(RuntimeInputCommand.Correct("correction-1", "target held without reset"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var activeFinished = handler.Handle(RuntimeInputCommand.FinishPhase());
        var cueResponse = handler.Handle(RuntimeInputCommand.RespondToCue("cue-1", "switch"));
        var cueFinished = handler.Handle(RuntimeInputCommand.FinishPhase());
        var reconstruction = handler.Handle(RuntimeInputCommand.SubmitAnswer("answer-2", "reconstruction"));
        var reconstructFinished = handler.Handle(RuntimeInputCommand.FinishPhase());
        var auditStarted = handler.Handle(RuntimeInputCommand.StartAudit("audit-1"));
        var auditAnswer = handler.Handle(RuntimeInputCommand.SubmitAnswer("audit-answer-1", "supported finding"));
        var auditGuess = handler.Handle(RuntimeInputCommand.MarkGuess("audit-guess-1"));

        Assert.All(
            [drift, answer, guess, error, correction, activeFinished, cueResponse, cueFinished, reconstruction, reconstructFinished, auditStarted, auditAnswer, auditGuess],
            result => Assert.True(result.IsAccepted));
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, handler.LifecycleState.Status);
        Assert.Equal("audit", handler.CurrentPhase?.Id);

        Assert.Collection(
            handler.Events.Select(runtimeEvent => runtimeEvent.Kind),
            kind => Assert.Equal(RuntimeEventKind.SessionStarted, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseStarted, kind),
            kind => Assert.Equal(RuntimeEventKind.DriftMarked, kind),
            kind => Assert.Equal(RuntimeEventKind.AnswerSubmitted, kind),
            kind => Assert.Equal(RuntimeEventKind.GuessMarked, kind),
            kind => Assert.Equal(RuntimeEventKind.ErrorRecorded, kind),
            kind => Assert.Equal(RuntimeEventKind.CorrectionSubmitted, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseEnded, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseStarted, kind),
            kind => Assert.Equal(RuntimeEventKind.CueResponseSubmitted, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseEnded, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseStarted, kind),
            kind => Assert.Equal(RuntimeEventKind.AnswerSubmitted, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseEnded, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseStarted, kind),
            kind => Assert.Equal(RuntimeEventKind.AuditStarted, kind),
            kind => Assert.Equal(RuntimeEventKind.AnswerSubmitted, kind),
            kind => Assert.Equal(RuntimeEventKind.GuessMarked, kind));
        Assert.Contains(handler.Events[2].Facts, fact => fact.Name == "command_kind" && fact.Value == "mark_drift");
        Assert.Contains(handler.Events[5].Facts, fact => fact.Name == "error_kind" && fact.Value == "incorrect_response");
    }

    [Fact]
    public void AvailabilityReportsRuntimeCommandStateWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
                RuntimeSessionPhaseDefinition.Manual("cue", RuntimeSessionPhaseKind.CueResponse),
            ]),
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10),
                PauseAllowedPhaseKinds: [RuntimeSessionPhaseKind.ActiveWork]));

        var eventCount = handler.Events.Count;

        Assert.True(handler.AvailabilityFor(RuntimeInputCommandKind.MarkDrift).IsAvailable);
        Assert.True(handler.AvailabilityFor(RuntimeInputCommandKind.MarkError).IsAvailable);
        Assert.True(handler.AvailabilityFor(RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
        Assert.True(handler.AvailabilityFor(RuntimeInputCommandKind.Pause).IsAvailable);
        Assert.False(handler.AvailabilityFor(RuntimeInputCommandKind.RespondToCue).IsAvailable);
        Assert.Equal(
            RuntimeInputCommandInvalidReason.CommandNotAllowedInCurrentPhase,
            handler.AvailabilityFor(RuntimeInputCommandKind.RespondToCue).InvalidReason);
        Assert.False(handler.AvailabilityFor(RuntimeInputCommandKind.Correct).IsAvailable);
        Assert.Equal(RuntimeInputCommandInvalidReason.NoCorrectableEvent, handler.AvailabilityFor(RuntimeInputCommandKind.Correct).InvalidReason);
        Assert.Equal(eventCount, handler.Events.Count);

        Assert.True(handler.Handle(RuntimeInputCommand.SubmitAnswer("answer-1", "first")).IsAccepted);
        Assert.True(handler.AvailabilityFor(RuntimeInputCommandKind.Correct).IsAvailable);
        Assert.True(handler.Handle(RuntimeInputCommand.FinishPhase()).IsAccepted);

        Assert.True(handler.AvailabilityFor(RuntimeInputCommandKind.RespondToCue).IsAvailable);
        Assert.True(handler.AvailabilityFor(RuntimeInputCommandKind.MarkError).IsAvailable);
        Assert.False(handler.AvailabilityFor(RuntimeInputCommandKind.SubmitAnswer).IsAvailable);
    }

    [Fact]
    public void CuePhaseAcceptsAnExplicitUncertaintyMark()
    {
        var handler = StartHandler(
            new ManualRuntimeClock(RuntimeInstant.Zero),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("cue", RuntimeSessionPhaseKind.CueResponse),
            ]),
            RuntimeInputCommandOptions.Default);

        var marked = handler.Handle(RuntimeInputCommand.MarkGuess("uncertainty-1"));

        Assert.True(marked.IsAccepted);
        Assert.Contains(marked.Events, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.GuessMarked);
    }

    [Fact]
    public void FocusHoldReturnClosesOpenDriftAndRecordsRuntimeOwnedTiming()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
            ]));

        var unavailableReturn = handler.AvailabilityFor(RuntimeInputCommandKind.MarkReturn);
        var drift = handler.Handle(RuntimeInputCommand.MarkDrift("drift-1"));
        var driftWhileReturning = handler.AvailabilityFor(RuntimeInputCommandKind.MarkDrift);
        var availableReturn = handler.AvailabilityFor(RuntimeInputCommandKind.MarkReturn);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(7));
        var returned = handler.Handle(
            RuntimeInputCommand.MarkReturn(RuntimeDuration.FromSeconds(10)));

        Assert.False(unavailableReturn.IsAvailable);
        Assert.Equal(RuntimeInputCommandInvalidReason.NoOpenDrift, unavailableReturn.InvalidReason);
        Assert.True(drift.IsAccepted);
        Assert.False(driftWhileReturning.IsAvailable);
        Assert.Equal(
            RuntimeInputCommandInvalidReason.OpenDriftRequiresReturn,
            driftWhileReturning.InvalidReason);
        Assert.True(availableReturn.IsAvailable);
        Assert.True(returned.IsAccepted);
        Assert.Equal(RuntimeEventKind.RecoveryCompleted, returned.Events.Single().Kind);
        Assert.Contains(returned.Events.Single().Facts, fact =>
            fact.Name == "drift_id" && fact.Value == "drift-1");
        Assert.Contains(returned.Events.Single().Facts, fact =>
            fact.Name == "recovery_time" && fact.Value == "00:00:07");
        Assert.Contains(returned.Events.Single().Facts, fact =>
            fact.Name == "return_within_window" && fact.Value == "true");
        Assert.True(handler.AvailabilityFor(RuntimeInputCommandKind.MarkDrift).IsAvailable);
        Assert.False(handler.AvailabilityFor(RuntimeInputCommandKind.MarkReturn).IsAvailable);
    }

    [Fact]
    public void FocusHoldReturnAndTargetChangePreserveDecisiveFailureEvidence()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
            ]));

        Assert.True(handler.Handle(RuntimeInputCommand.MarkDrift("drift-late")).IsAccepted);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(11));
        var lateReturn = handler.Handle(
            RuntimeInputCommand.MarkReturn(RuntimeDuration.FromSeconds(10)));
        var targetChange = handler.Handle(RuntimeInputCommand.MarkTargetChange("red circle"));

        Assert.True(lateReturn.IsAccepted);
        Assert.Contains(lateReturn.Events.Single().Facts, fact =>
            fact.Name == "return_timing_outcome" && fact.Value == "late");
        Assert.True(targetChange.IsAccepted);
        Assert.Equal(RuntimeEventKind.ErrorRecorded, targetChange.Events.Single().Kind);
        Assert.Contains(targetChange.Events.Single().Facts, fact =>
            fact.Name == "error_kind" && fact.Value == "target_substitution");
        Assert.Contains(targetChange.Events.Single().Facts, fact =>
            fact.Name == "failed_constraint" && fact.Value == "no_target_substitution");
        Assert.Contains(targetChange.Events.Single().Facts, fact =>
            fact.Name == "substitute_target" && fact.Value == "red circle");
    }

    [Fact]
    public void FocusHoldOpenDriftIsRecoveredFromRestoredEventLog()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
            ]),
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10),
                PauseAllowedPhaseKinds: [RuntimeSessionPhaseKind.ActiveWork]));

        Assert.True(handler.Handle(RuntimeInputCommand.MarkDrift("drift-restored")).IsAccepted);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(3));
        Assert.True(handler.Handle(RuntimeInputCommand.Pause()).IsAccepted);
        var snapshot = handler.CaptureSnapshot();
        var restoredClock = new ManualRuntimeClock(snapshot.CapturedAt);
        var restored = RuntimeInputCommandHandler.Restore(snapshot, restoredClock);

        Assert.True(restored.Handle(RuntimeInputCommand.Resume()).IsAccepted);
        Assert.True(restored.AvailabilityFor(RuntimeInputCommandKind.MarkReturn).IsAvailable);
        restoredClock.AdvanceBy(RuntimeDuration.FromSeconds(4));
        var returned = restored.Handle(
            RuntimeInputCommand.MarkReturn(RuntimeDuration.FromSeconds(10)));

        Assert.True(returned.IsAccepted);
        Assert.Contains(returned.Events.Single().Facts, fact =>
            fact.Name == "drift_id" && fact.Value == "drift-restored");
        Assert.Contains(returned.Events.Single().Facts, fact =>
            fact.Name == "recovery_time" && fact.Value == "00:00:07");
    }

    [Fact]
    public void HandlerRejectsCommandsThatDoNotMatchCurrentPhaseWithoutMutatingState()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
                RuntimeSessionPhaseDefinition.Manual("cue", RuntimeSessionPhaseKind.CueResponse),
                RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
            ]));

        var eventCountBeforeInvalidCueResponse = handler.Events.Count;
        var invalidCueResponse = handler.Handle(RuntimeInputCommand.RespondToCue("cue-1", "switch"));

        Assert.False(invalidCueResponse.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.CommandNotAllowedInCurrentPhase, invalidCueResponse.InvalidReason);
        Assert.Equal(eventCountBeforeInvalidCueResponse, handler.Events.Count);
        Assert.Equal("active", handler.CurrentPhase?.Id);

        Assert.True(handler.Handle(RuntimeInputCommand.FinishPhase()).IsAccepted);
        Assert.Equal("cue", handler.CurrentPhase?.Id);

        var eventCountBeforeInvalidAnswer = handler.Events.Count;
        var invalidAnswer = handler.Handle(RuntimeInputCommand.SubmitAnswer("answer-1", "too early"));

        Assert.False(invalidAnswer.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.CommandNotAllowedInCurrentPhase, invalidAnswer.InvalidReason);
        Assert.Equal(eventCountBeforeInvalidAnswer, handler.Events.Count);
        Assert.Equal("cue", handler.CurrentPhase?.Id);

        Assert.True(handler.Handle(RuntimeInputCommand.RespondToCue("cue-1", "switch")).IsAccepted);
        Assert.True(handler.Handle(RuntimeInputCommand.FinishPhase()).IsAccepted);

        var eventCountBeforeInvalidAudit = handler.Events.Count;
        var invalidAudit = handler.Handle(RuntimeInputCommand.StartAudit("audit-1"));

        Assert.False(invalidAudit.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.CommandNotAllowedInCurrentPhase, invalidAudit.InvalidReason);
        Assert.Equal(eventCountBeforeInvalidAudit, handler.Events.Count);
        Assert.Equal("reconstruct", handler.CurrentPhase?.Id);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, handler.LifecycleState.Status);
    }

    [Fact]
    public void HandlerEnforcesCorrectionWindowWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
            ]),
            new RuntimeInputCommandOptions(
                PauseAllowed: false,
                CorrectionWindow: RuntimeDuration.FromSeconds(5)));

        var missingCorrectionSource = handler.Handle(RuntimeInputCommand.Correct("correction-0", "nothing to correct"));

        Assert.False(missingCorrectionSource.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.NoCorrectableEvent, missingCorrectionSource.InvalidReason);

        Assert.True(handler.Handle(RuntimeInputCommand.SubmitAnswer("answer-1", "first answer")).IsAccepted);
        var eventCountAfterAnswer = handler.Events.Count;
        clock.AdvanceBy(RuntimeDuration.FromSeconds(6));

        var expiredCorrection = handler.Handle(RuntimeInputCommand.Correct("correction-1", "late correction"));

        Assert.False(expiredCorrection.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.CorrectionWindowExpired, expiredCorrection.InvalidReason);
        Assert.Equal(eventCountAfterAnswer, handler.Events.Count);
        Assert.Equal("reconstruct", handler.CurrentPhase?.Id);
    }

    [Fact]
    public void FailedCueCanBeCorrectedExactlyOnceDuringCuePhase()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("cue", RuntimeSessionPhaseKind.CueResponse),
            ]),
            new RuntimeInputCommandOptions(
                PauseAllowed: false,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));
        var failedCue = handler.EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            clock.Now,
            "cue",
            RuntimeSessionPhaseKind.CueResponse,
            [
                new RuntimeEventFact("cue_id", "cue-1"),
                new RuntimeEventFact("expected_response", "tap"),
                new RuntimeEventFact("response", "withhold"),
                new RuntimeEventFact("response_outcome", "incorrect"),
            ]);

        handler.ObserveExternallyRecordedEvent(failedCue);

        Assert.True(handler.AvailabilityFor(RuntimeInputCommandKind.Correct).IsAvailable);
        var corrected = handler.Handle(RuntimeInputCommand.Correct("correction-1", "tap"));

        Assert.True(corrected.IsAccepted);
        Assert.Contains(corrected.Events.Single().Facts, fact =>
            fact.Name == "corrected_event_sequence" && fact.Value == failedCue.SequenceNumber.ToString());
        Assert.Contains(corrected.Events.Single().Facts, fact =>
            fact.Name == "corrected_cue_id" && fact.Value == "cue-1");
        Assert.Contains(corrected.Events.Single().Facts, fact =>
            fact.Name == "correction_outcome" && fact.Value == "correct");
        Assert.False(handler.AvailabilityFor(RuntimeInputCommandKind.Correct).IsAvailable);

        var repeated = handler.Handle(RuntimeInputCommand.Correct("correction-2", "tap"));
        Assert.False(repeated.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.NoCorrectableEvent, repeated.InvalidReason);
    }

    [Fact]
    public void HandlerAppliesPauseResumeAndAbandonThroughLifecycle()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("rest", RuntimeSessionPhaseKind.Rest),
            ]),
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));

        var pause = handler.Handle(RuntimeInputCommand.Pause());

        Assert.True(pause.IsAccepted);
        Assert.Equal(RuntimeSessionLifecycleStatus.Paused, handler.LifecycleState.Status);
        Assert.Equal(RuntimeEventKind.SessionPaused, handler.Events[^1].Kind);

        var eventCountWhilePaused = handler.Events.Count;
        var driftWhilePaused = handler.Handle(RuntimeInputCommand.MarkDrift("drift-1"));

        Assert.False(driftWhilePaused.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.SessionPaused, driftWhilePaused.InvalidReason);
        Assert.Equal(eventCountWhilePaused, handler.Events.Count);

        var resume = handler.Handle(RuntimeInputCommand.Resume());

        Assert.True(resume.IsAccepted);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, handler.LifecycleState.Status);
        Assert.Equal(RuntimeEventKind.SessionResumed, handler.Events[^1].Kind);

        var abandon = handler.Handle(RuntimeInputCommand.Abandon("user stopped session"));

        Assert.True(abandon.IsAccepted);
        Assert.Equal(RuntimeSessionLifecycleStatus.Abandoned, handler.LifecycleState.Status);
        Assert.False(handler.EventLog.IsActive);
        Assert.Equal(RuntimeEventKind.SessionAbandoned, handler.Events[^1].Kind);

        var eventCountAfterAbandon = handler.Events.Count;
        var commandAfterAbandon = handler.Handle(RuntimeInputCommand.MarkDrift("drift-2"));

        Assert.False(commandAfterAbandon.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.CommandAfterTerminalSession, commandAfterAbandon.InvalidReason);
        Assert.Equal(eventCountAfterAbandon, handler.Events.Count);
    }

    [Fact]
    public void HandlerRejectsPauseWhenPauseIsGloballyDisabled()
    {
        var handler = StartHandler(
            new ManualRuntimeClock(RuntimeInstant.Zero),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
            ]),
            new RuntimeInputCommandOptions(
                PauseAllowed: false,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));

        var eventCountBeforePause = handler.Events.Count;
        var pause = handler.Handle(RuntimeInputCommand.Pause());

        Assert.False(pause.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.PauseNotAllowed, pause.InvalidReason);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, handler.LifecycleState.Status);
        Assert.Equal(eventCountBeforePause, handler.Events.Count);
    }

    [Fact]
    public void HandlerRejectsPauseInHonestySensitivePhaseEvenWhenPauseIsGloballyAllowed()
    {
        var handler = StartHandler(
            new ManualRuntimeClock(RuntimeInstant.Zero),
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("encode", RuntimeSessionPhaseKind.EncodeWindow, RuntimeDuration.FromSeconds(60)),
            ]),
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));

        var eventCountBeforePause = handler.Events.Count;
        var pause = handler.Handle(RuntimeInputCommand.Pause());

        Assert.False(pause.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.PauseNotAllowed, pause.InvalidReason);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, handler.LifecycleState.Status);
        Assert.Equal(RuntimePhaseSchedulerStatus.Running, pause.SchedulerStatus);
        Assert.Equal("encode", handler.CurrentPhase?.Id);
        Assert.Equal(eventCountBeforePause, handler.Events.Count);
    }

    [Fact]
    public void HandlerResumePreservesActivePhaseElapsedTimeAndEvidenceHistory()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("active", RuntimeSessionPhaseKind.ActiveWork),
                RuntimeSessionPhaseDefinition.Timed("rest", RuntimeSessionPhaseKind.Rest, RuntimeDuration.FromSeconds(10)),
                RuntimeSessionPhaseDefinition.Manual("review", RuntimeSessionPhaseKind.Review),
            ]),
            new RuntimeInputCommandOptions(
                PauseAllowed: true,
                CorrectionWindow: RuntimeDuration.FromSeconds(10)));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var drift = handler.Handle(RuntimeInputCommand.MarkDrift("drift-before-pause"));
        var activeFinished = handler.Handle(RuntimeInputCommand.FinishPhase());
        clock.AdvanceBy(RuntimeDuration.FromSeconds(4));
        var pause = handler.Handle(RuntimeInputCommand.Pause());
        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var resume = handler.Handle(RuntimeInputCommand.Resume());
        var earlyFinishAfterResume = handler.Handle(RuntimeInputCommand.FinishPhase());

        Assert.True(drift.IsAccepted);
        Assert.True(activeFinished.IsAccepted);
        Assert.True(pause.IsAccepted);
        Assert.True(resume.IsAccepted);
        Assert.False(earlyFinishAfterResume.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.InvalidPhaseCompletion, earlyFinishAfterResume.InvalidReason);
        Assert.Equal(RuntimeSessionPhaseInvalidCompletionReason.ScheduledDurationNotReached, earlyFinishAfterResume.PhaseInvalidReason);
        Assert.Equal("rest", handler.CurrentPhase?.Id);
        Assert.Contains(handler.Events, runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.DriftMarked &&
            runtimeEvent.Facts.Any(fact => fact.Name == "drift_id" && fact.Value == "drift-before-pause"));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(6));
        var restFinished = handler.Handle(RuntimeInputCommand.FinishPhase());

        Assert.True(restFinished.IsAccepted);
        Assert.Equal("review", handler.CurrentPhase?.Id);
        Assert.Contains(restFinished.Events, runtimeEvent =>
            runtimeEvent.Kind == RuntimeEventKind.PhaseEnded &&
            runtimeEvent.Facts.Any(fact => fact.Name == "phase_actual_duration" && fact.Value == "00:00:10"));
        Assert.Collection(
            handler.Events.Where(runtimeEvent => runtimeEvent.Kind is RuntimeEventKind.SessionPaused or RuntimeEventKind.SessionResumed),
            paused => Assert.Equal(RuntimeEventKind.SessionPaused, paused.Kind),
            resumed => Assert.Equal(RuntimeEventKind.SessionResumed, resumed.Kind));
    }

    [Fact]
    public void FinishPhaseCommandUsesSchedulerAndRejectsEarlyTimedCompletion()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("encode", RuntimeSessionPhaseKind.EncodeWindow, RuntimeDuration.FromSeconds(60)),
                RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
            ]));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var eventCountBeforeEarlyFinish = handler.Events.Count;
        var earlyFinishAvailability = handler.AvailabilityFor(RuntimeInputCommandKind.FinishPhase);
        var earlyFinish = handler.Handle(RuntimeInputCommand.FinishPhase());

        Assert.False(earlyFinishAvailability.IsAvailable);
        Assert.Equal(RuntimeInputCommandInvalidReason.InvalidPhaseCompletion, earlyFinishAvailability.InvalidReason);
        Assert.False(earlyFinish.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.InvalidPhaseCompletion, earlyFinish.InvalidReason);
        Assert.Equal(RuntimeSessionPhaseInvalidCompletionReason.ScheduledDurationNotReached, earlyFinish.PhaseInvalidReason);
        Assert.Equal(eventCountBeforeEarlyFinish, handler.Events.Count);
        Assert.Equal("encode", handler.CurrentPhase?.Id);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var deadlineAvailability = handler.AvailabilityFor(RuntimeInputCommandKind.FinishPhase);
        var finishAtDeadline = handler.Handle(RuntimeInputCommand.FinishPhase());

        Assert.True(deadlineAvailability.IsAvailable);
        Assert.True(finishAtDeadline.IsAccepted);
        Assert.Equal("reconstruct", handler.CurrentPhase?.Id);
        Assert.Contains(finishAtDeadline.Events, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.PhaseEnded);
        Assert.Contains(finishAtDeadline.Events, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.PhaseStarted);
    }

    [Fact]
    public void AdvanceToCurrentTimeAdvancesDueTimedPhaseWithoutCompletingManualPhases()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = StartHandler(
            clock,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Manual("prep", RuntimeSessionPhaseKind.InstructionPrep),
                RuntimeSessionPhaseDefinition.Timed("active", RuntimeSessionPhaseKind.ActiveWork, RuntimeDuration.FromSeconds(10)),
                RuntimeSessionPhaseDefinition.Manual("review", RuntimeSessionPhaseKind.Review),
            ]));

        clock.AdvanceBy(RuntimeDuration.FromSeconds(10));
        var manualAdvance = handler.AdvanceToCurrentTime();

        Assert.True(manualAdvance.IsAccepted);
        Assert.Empty(manualAdvance.Events);
        Assert.Equal("prep", handler.CurrentPhase?.Id);

        Assert.True(handler.Handle(RuntimeInputCommand.FinishPhase()).IsAccepted);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(9));
        var earlyTimedAdvance = handler.AdvanceToCurrentTime();

        Assert.True(earlyTimedAdvance.IsAccepted);
        Assert.Empty(earlyTimedAdvance.Events);
        Assert.Equal("active", handler.CurrentPhase?.Id);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var timedAdvance = handler.AdvanceToCurrentTime();

        Assert.True(timedAdvance.IsAccepted);
        Assert.Equal(RuntimeSessionLifecycleStatus.Running, handler.LifecycleState.Status);
        Assert.Equal("review", handler.CurrentPhase?.Id);
        Assert.Contains(timedAdvance.Events, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.PhaseTimedOut);
        Assert.Contains(timedAdvance.Events, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.PhaseEnded);
        Assert.Contains(timedAdvance.Events, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.PhaseStarted);

        Assert.True(handler.Handle(RuntimeInputCommand.FinishPhase()).IsAccepted);
        Assert.Equal(RuntimeSessionLifecycleStatus.Completed, handler.LifecycleState.Status);
    }

    private static RuntimeInputCommandHandler StartHandler(
        ManualRuntimeClock clock,
        RuntimeSessionPhasePlan plan,
        RuntimeInputCommandOptions? options = null)
    {
        return RuntimeInputCommandHandler.Start(
            "session-commands",
            CreateSessionDefinition(),
            plan,
            clock,
            options);
    }

    private static RuntimeSessionDefinition CreateSessionDefinition()
    {
        return new RuntimeSessionDefinition(
            SessionType.Practice,
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
