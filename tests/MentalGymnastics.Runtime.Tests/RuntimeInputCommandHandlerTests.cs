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
        var correction = handler.Handle(RuntimeInputCommand.Correct("correction-1", "target held without reset"));
        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var activeFinished = handler.Handle(RuntimeInputCommand.FinishPhase());
        var cueResponse = handler.Handle(RuntimeInputCommand.RespondToCue("cue-1", "switch"));
        var cueFinished = handler.Handle(RuntimeInputCommand.FinishPhase());
        var reconstruction = handler.Handle(RuntimeInputCommand.SubmitAnswer("answer-2", "reconstruction"));
        var reconstructFinished = handler.Handle(RuntimeInputCommand.FinishPhase());
        var auditStarted = handler.Handle(RuntimeInputCommand.StartAudit("audit-1"));
        var auditGuess = handler.Handle(RuntimeInputCommand.MarkGuess("audit-guess-1"));

        Assert.All(
            [drift, answer, guess, correction, activeFinished, cueResponse, cueFinished, reconstruction, reconstructFinished, auditStarted, auditGuess],
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
            kind => Assert.Equal(RuntimeEventKind.GuessMarked, kind));
        Assert.Contains(handler.Events[2].Facts, fact => fact.Name == "command_kind" && fact.Value == "mark_drift");
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
        var earlyFinish = handler.Handle(RuntimeInputCommand.FinishPhase());

        Assert.False(earlyFinish.IsAccepted);
        Assert.Equal(RuntimeInputCommandInvalidReason.InvalidPhaseCompletion, earlyFinish.InvalidReason);
        Assert.Equal(RuntimeSessionPhaseInvalidCompletionReason.ScheduledDurationNotReached, earlyFinish.PhaseInvalidReason);
        Assert.Equal(eventCountBeforeEarlyFinish, handler.Events.Count);
        Assert.Equal("encode", handler.CurrentPhase?.Id);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var finishAtDeadline = handler.Handle(RuntimeInputCommand.FinishPhase());

        Assert.True(finishAtDeadline.IsAccepted);
        Assert.Equal("reconstruct", handler.CurrentPhase?.Id);
        Assert.Contains(finishAtDeadline.Events, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.PhaseEnded);
        Assert.Contains(finishAtDeadline.Events, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.PhaseStarted);
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
