using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeSessionPhaseTests
{
    [Fact]
    public void PhasePlanRepresentsGenericRuntimePhasesForDocumentedDrills()
    {
        var plan = new RuntimeSessionPhasePlan(
        [
            RuntimeSessionPhaseDefinition.Manual("prep", RuntimeSessionPhaseKind.InstructionPrep),
            RuntimeSessionPhaseDefinition.Timed("encode", RuntimeSessionPhaseKind.EncodeWindow, RuntimeDuration.FromSeconds(60)),
            RuntimeSessionPhaseDefinition.Timed("work", RuntimeSessionPhaseKind.ActiveWork, RuntimeDuration.FromSeconds(180)),
            RuntimeSessionPhaseDefinition.ManualOrTimed("valid-cue", RuntimeSessionPhaseKind.CueResponse, RuntimeDuration.FromSeconds(2)),
            RuntimeSessionPhaseDefinition.ManualOrTimed("invalid-cue", RuntimeSessionPhaseKind.CueResponse, RuntimeDuration.FromSeconds(2)),
            RuntimeSessionPhaseDefinition.Timed("delay", RuntimeSessionPhaseKind.DelayWindow, RuntimeDuration.FromSeconds(90)),
            RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
            RuntimeSessionPhaseDefinition.Manual("audit", RuntimeSessionPhaseKind.Audit),
            RuntimeSessionPhaseDefinition.Timed("rest", RuntimeSessionPhaseKind.Rest, RuntimeDuration.FromSeconds(60)),
            RuntimeSessionPhaseDefinition.Timed("recovery", RuntimeSessionPhaseKind.Recovery, RuntimeDuration.FromSeconds(30)),
            RuntimeSessionPhaseDefinition.Manual("review", RuntimeSessionPhaseKind.Review),
        ]);

        Assert.Collection(
            plan.Phases.Select(phase => phase.Kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.EncodeWindow, kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.CueResponse, kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.CueResponse, kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.DelayWindow, kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.ReconstructionInput, kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.Audit, kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.Rest, kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.Recovery, kind),
            kind => Assert.Equal(RuntimeSessionPhaseKind.Review, kind));
        Assert.Equal(TimeSpan.FromSeconds(424), plan.TotalScheduledDuration.Value);
        Assert.Equal("invalid-cue", plan.Phases[4].Id);
    }

    [Fact]
    public void PhasePlanRejectsMissingPhasesDuplicateIdsAndInvalidDurations()
    {
        Assert.Throws<ArgumentException>(() => new RuntimeSessionPhasePlan([]));
        Assert.Throws<ArgumentException>(() => new RuntimeSessionPhasePlan(
        [
            RuntimeSessionPhaseDefinition.Manual("prep", RuntimeSessionPhaseKind.InstructionPrep),
            RuntimeSessionPhaseDefinition.Timed("prep", RuntimeSessionPhaseKind.ActiveWork, RuntimeDuration.FromSeconds(1)),
        ]));
        Assert.Throws<ArgumentException>(() => RuntimeSessionPhaseDefinition.Manual(" ", RuntimeSessionPhaseKind.InstructionPrep));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeSessionPhaseDefinition.Timed(
            "zero-duration",
            RuntimeSessionPhaseKind.DelayWindow,
            RuntimeDuration.Zero));
        Assert.Throws<ArgumentException>(() => RuntimeSessionPhaseDefinition.ManualOrTimed(
            "missing-duration",
            RuntimeSessionPhaseKind.CueResponse,
            null));
    }

    [Fact]
    public void PhaseSequenceCompletesInOrderAndPreservesDeterministicDurations()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var plan = new RuntimeSessionPhasePlan(
        [
            RuntimeSessionPhaseDefinition.Manual("prep", RuntimeSessionPhaseKind.InstructionPrep),
            RuntimeSessionPhaseDefinition.Timed("encode", RuntimeSessionPhaseKind.EncodeWindow, RuntimeDuration.FromSeconds(60)),
            RuntimeSessionPhaseDefinition.Manual("reconstruct", RuntimeSessionPhaseKind.ReconstructionInput),
        ]);
        var sequence = RuntimeSessionPhaseSequence.Start(plan, clock.Now);

        Assert.Equal("prep", sequence.CurrentPhase?.Id);
        Assert.Equal(RuntimeInstant.Zero, sequence.CurrentPhaseStartedAt);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var prepCompleted = sequence.TryCompleteCurrent(clock.Now, RuntimeSessionPhaseCompletionCause.Explicit);

        Assert.True(prepCompleted.IsValid);
        Assert.NotNull(prepCompleted.CompletedPhase);
        Assert.Equal("prep", prepCompleted.CompletedPhase.Definition.Id);
        Assert.Equal(TimeSpan.FromSeconds(5), prepCompleted.CompletedPhase.ActualDuration.Value);
        Assert.Equal("encode", prepCompleted.Sequence.CurrentPhase?.Id);
        Assert.Equal(TimeSpan.FromSeconds(5), prepCompleted.Sequence.CurrentPhaseStartedAt.Offset);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var earlyEncodeCompletion = prepCompleted.Sequence.TryCompleteCurrent(
            clock.Now,
            RuntimeSessionPhaseCompletionCause.Explicit);

        Assert.False(earlyEncodeCompletion.IsValid);
        Assert.Equal(RuntimeSessionPhaseInvalidCompletionReason.ScheduledDurationNotReached, earlyEncodeCompletion.InvalidReason);
        Assert.Same(prepCompleted.Sequence, earlyEncodeCompletion.Sequence);
        Assert.Equal("encode", earlyEncodeCompletion.Sequence.CurrentPhase?.Id);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(30));
        var encodeCompleted = prepCompleted.Sequence.TryCompleteCurrent(
            clock.Now,
            RuntimeSessionPhaseCompletionCause.Timeout);

        Assert.True(encodeCompleted.IsValid);
        Assert.NotNull(encodeCompleted.CompletedPhase);
        Assert.Equal("encode", encodeCompleted.CompletedPhase.Definition.Id);
        Assert.Equal(TimeSpan.FromSeconds(60), encodeCompleted.CompletedPhase.ActualDuration.Value);
        Assert.Equal("reconstruct", encodeCompleted.Sequence.CurrentPhase?.Id);
        Assert.Equal(TimeSpan.FromSeconds(65), encodeCompleted.Sequence.CurrentPhaseStartedAt.Offset);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(20));
        var reconstructionCompleted = encodeCompleted.Sequence.TryCompleteCurrent(
            clock.Now,
            RuntimeSessionPhaseCompletionCause.Explicit);

        Assert.True(reconstructionCompleted.IsValid);
        Assert.True(reconstructionCompleted.Sequence.IsComplete);
        Assert.Null(reconstructionCompleted.Sequence.CurrentPhase);
        Assert.Equal(3, reconstructionCompleted.Sequence.CompletedPhases.Count);
        Assert.Equal(TimeSpan.FromSeconds(85), reconstructionCompleted.Sequence.CurrentPhaseStartedAt.Offset);
    }

    [Fact]
    public void ManualOrTimedPhaseMayCompleteByInputBeforeTimeoutOrByTimeoutAtDeadline()
    {
        var inputPlan = new RuntimeSessionPhasePlan(
        [
            RuntimeSessionPhaseDefinition.ManualOrTimed(
                "cue-response",
                RuntimeSessionPhaseKind.CueResponse,
                RuntimeDuration.FromSeconds(5)),
        ]);
        var inputSequence = RuntimeSessionPhaseSequence.Start(inputPlan, RuntimeInstant.Zero);

        var explicitResult = inputSequence.TryCompleteCurrent(
            new RuntimeInstant(TimeSpan.FromSeconds(2)),
            RuntimeSessionPhaseCompletionCause.Explicit);

        Assert.True(explicitResult.IsValid);
        Assert.Equal(TimeSpan.FromSeconds(2), explicitResult.CompletedPhase?.ActualDuration.Value);

        var timeoutSequence = RuntimeSessionPhaseSequence.Start(inputPlan, RuntimeInstant.Zero);
        var earlyTimeout = timeoutSequence.TryCompleteCurrent(
            new RuntimeInstant(TimeSpan.FromSeconds(4)),
            RuntimeSessionPhaseCompletionCause.Timeout);

        Assert.False(earlyTimeout.IsValid);
        Assert.Equal(RuntimeSessionPhaseInvalidCompletionReason.ScheduledDurationNotReached, earlyTimeout.InvalidReason);

        var timeoutResult = timeoutSequence.TryCompleteCurrent(
            new RuntimeInstant(TimeSpan.FromSeconds(5)),
            RuntimeSessionPhaseCompletionCause.Timeout);

        Assert.True(timeoutResult.IsValid);
        Assert.Equal(RuntimeSessionPhaseCompletionCause.Timeout, timeoutResult.CompletedPhase?.CompletionCause);
    }
}
