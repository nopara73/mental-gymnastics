using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeEventLogTests
{
    [Fact]
    public void EventLogCapturesDocumentedRuntimeEventsInOrderForOneActiveSession()
    {
        var session = CreateSessionDefinition();
        var log = RuntimeEventLog.Start("session-001", session, RuntimeInstant.Zero);

        log.Append(
            RuntimeEventKind.PhaseStarted,
            RuntimeDuration.FromSeconds(5).ToInstant(),
            phaseId: "prep",
            phaseKind: RuntimeSessionPhaseKind.InstructionPrep);
        log.Append(
            RuntimeEventKind.TimerTick,
            RuntimeDuration.FromSeconds(6).ToInstant(),
            phaseId: "prep",
            phaseKind: RuntimeSessionPhaseKind.InstructionPrep,
            facts:
            [
                new RuntimeEventFact("elapsed", "00:00:06"),
                new RuntimeEventFact("remaining", "00:00:54"),
            ]);
        log.Append(RuntimeEventKind.CueEmitted, RuntimeDuration.FromSeconds(7).ToInstant(), "prep", RuntimeSessionPhaseKind.InstructionPrep);
        log.Append(RuntimeEventKind.UserAction, RuntimeDuration.FromSeconds(8).ToInstant(), "prep", RuntimeSessionPhaseKind.InstructionPrep);
        log.Append(RuntimeEventKind.AnswerSubmitted, RuntimeDuration.FromSeconds(9).ToInstant(), "prep", RuntimeSessionPhaseKind.InstructionPrep);
        log.Append(RuntimeEventKind.DriftMarked, RuntimeDuration.FromSeconds(10).ToInstant(), "prep", RuntimeSessionPhaseKind.InstructionPrep);
        log.Append(RuntimeEventKind.GuessMarked, RuntimeDuration.FromSeconds(11).ToInstant(), "prep", RuntimeSessionPhaseKind.InstructionPrep);
        log.Append(RuntimeEventKind.InterruptionRecorded, RuntimeDuration.FromSeconds(12).ToInstant(), "prep", RuntimeSessionPhaseKind.InstructionPrep);
        log.Append(RuntimeEventKind.CorrectionSubmitted, RuntimeDuration.FromSeconds(13).ToInstant(), "prep", RuntimeSessionPhaseKind.InstructionPrep);
        log.Append(RuntimeEventKind.ErrorRecorded, RuntimeDuration.FromSeconds(14).ToInstant(), "prep", RuntimeSessionPhaseKind.InstructionPrep);
        log.Append(RuntimeEventKind.RecoveryStarted, RuntimeDuration.FromSeconds(15).ToInstant(), "recovery", RuntimeSessionPhaseKind.Recovery);
        log.Append(RuntimeEventKind.RecoveryCompleted, RuntimeDuration.FromSeconds(20).ToInstant(), "recovery", RuntimeSessionPhaseKind.Recovery);
        log.Append(RuntimeEventKind.SessionAbandoned, RuntimeDuration.FromSeconds(21).ToInstant());

        Assert.False(log.IsActive);
        Assert.Collection(
            log.Events,
            EventIs(1, RuntimeEventKind.SessionStarted, "session-001", null, null),
            EventIs(2, RuntimeEventKind.PhaseStarted, "session-001", "prep", RuntimeSessionPhaseKind.InstructionPrep),
            EventIs(3, RuntimeEventKind.TimerTick, "session-001", "prep", RuntimeSessionPhaseKind.InstructionPrep),
            EventIs(4, RuntimeEventKind.CueEmitted, "session-001", "prep", RuntimeSessionPhaseKind.InstructionPrep),
            EventIs(5, RuntimeEventKind.UserAction, "session-001", "prep", RuntimeSessionPhaseKind.InstructionPrep),
            EventIs(6, RuntimeEventKind.AnswerSubmitted, "session-001", "prep", RuntimeSessionPhaseKind.InstructionPrep),
            EventIs(7, RuntimeEventKind.DriftMarked, "session-001", "prep", RuntimeSessionPhaseKind.InstructionPrep),
            EventIs(8, RuntimeEventKind.GuessMarked, "session-001", "prep", RuntimeSessionPhaseKind.InstructionPrep),
            EventIs(9, RuntimeEventKind.InterruptionRecorded, "session-001", "prep", RuntimeSessionPhaseKind.InstructionPrep),
            EventIs(10, RuntimeEventKind.CorrectionSubmitted, "session-001", "prep", RuntimeSessionPhaseKind.InstructionPrep),
            EventIs(11, RuntimeEventKind.ErrorRecorded, "session-001", "prep", RuntimeSessionPhaseKind.InstructionPrep),
            EventIs(12, RuntimeEventKind.RecoveryStarted, "session-001", "recovery", RuntimeSessionPhaseKind.Recovery),
            EventIs(13, RuntimeEventKind.RecoveryCompleted, "session-001", "recovery", RuntimeSessionPhaseKind.Recovery),
            EventIs(14, RuntimeEventKind.SessionAbandoned, "session-001", null, null));

        Assert.Same(session, log.SessionDefinition);
        Assert.All(log.Events, runtimeEvent => Assert.Equal("session-001", runtimeEvent.SessionId));
        Assert.Contains(log.Events[2].Facts, fact => fact.Name == "elapsed" && fact.Value == "00:00:06");
    }

    [Fact]
    public void EventLogRejectsOutOfOrderEventsAndEventsAfterTerminalOutcome()
    {
        var log = RuntimeEventLog.Start(
            "session-002",
            CreateSessionDefinition(),
            RuntimeDuration.FromSeconds(10).ToInstant());

        log.Append(RuntimeEventKind.UserAction, RuntimeDuration.FromSeconds(12).ToInstant());

        Assert.Throws<InvalidOperationException>(() =>
            log.Append(RuntimeEventKind.AnswerSubmitted, RuntimeDuration.FromSeconds(11).ToInstant()));

        log.Append(RuntimeEventKind.SessionCompleted, RuntimeDuration.FromSeconds(13).ToInstant());

        Assert.False(log.IsActive);
        Assert.Throws<InvalidOperationException>(() =>
            log.Append(RuntimeEventKind.UserAction, RuntimeDuration.FromSeconds(14).ToInstant()));
    }

    [Fact]
    public void EventLogMapsSchedulerEventsWithPhaseTimelineFacts()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var session = CreateSessionDefinition();
        var scheduler = new RuntimePhaseScheduler(
            session,
            new RuntimeSessionPhasePlan(
            [
                RuntimeSessionPhaseDefinition.Timed("encode", RuntimeSessionPhaseKind.EncodeWindow, RuntimeDuration.FromSeconds(60)),
                RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
            ]),
            clock);
        var log = RuntimeEventLog.Start("session-003", session, clock.Now);

        log.AppendSchedulerEvents(scheduler.Start().Events);
        clock.AdvanceBy(RuntimeDuration.FromSeconds(61));
        log.AppendSchedulerEvents(scheduler.AdvanceToCurrentTime().Events);

        Assert.Collection(
            log.Events.Select(runtimeEvent => runtimeEvent.Kind),
            kind => Assert.Equal(RuntimeEventKind.SessionStarted, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseStarted, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseTimedOut, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseEnded, kind),
            kind => Assert.Equal(RuntimeEventKind.PhaseStarted, kind));

        var timeout = log.Events[2];
        Assert.Equal("encode", timeout.PhaseId);
        Assert.Equal(RuntimeSessionPhaseKind.EncodeWindow, timeout.PhaseKind);
        Assert.Contains(timeout.Facts, fact => fact.Name == "phase_elapsed" && fact.Value == "00:01:01");
        Assert.Contains(timeout.Facts, fact => fact.Name == "timeout_overtime" && fact.Value == "00:00:01");

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        log.AppendSchedulerEvents(scheduler.CompleteCurrentPhase().Events);

        Assert.False(log.IsActive);
        Assert.Equal(RuntimeEventKind.SessionCompleted, log.Events[^1].Kind);
        Assert.Equal(7, log.Events[^1].SequenceNumber);
    }

    [Fact]
    public void EventModelRejectsMissingStableSessionOrEvidenceFactIdentifiers()
    {
        var session = CreateSessionDefinition();

        Assert.Throws<ArgumentException>(() => RuntimeEventLog.Start(" ", session, RuntimeInstant.Zero));
        Assert.Throws<ArgumentException>(() => new RuntimeEventFact(" ", "drift-1"));
        Assert.Throws<ArgumentException>(() => new RuntimeEventFact("event", " "));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RuntimeEventLog.Start("session-004", session, RuntimeInstant.Zero)
                .Append((RuntimeEventKind)999, RuntimeInstant.Zero));
    }

    private static Action<RuntimeEvent> EventIs(
        long expectedSequence,
        RuntimeEventKind expectedKind,
        string expectedSessionId,
        string? expectedPhaseId,
        RuntimeSessionPhaseKind? expectedPhaseKind)
    {
        return runtimeEvent =>
        {
            Assert.Equal(expectedSequence, runtimeEvent.SequenceNumber);
            Assert.Equal(expectedKind, runtimeEvent.Kind);
            Assert.Equal(expectedSessionId, runtimeEvent.SessionId);
            Assert.Equal(expectedPhaseId, runtimeEvent.PhaseId);
            Assert.Equal(expectedPhaseKind, runtimeEvent.PhaseKind);
        };
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

internal static class RuntimeDurationTestExtensions
{
    public static RuntimeInstant ToInstant(this RuntimeDuration duration)
    {
        return new RuntimeInstant(duration.Value);
    }
}
