using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeResponseTimingTests
{
    [Fact]
    public void ResponseWindowClassifiesOnTimeEarlyLateAndMissedResponsesWithEvidence()
    {
        var window = new RuntimeResponseWindow(
            "cue-response-1",
            RuntimeDuration.FromSeconds(10).ToInstant(),
            RuntimeDuration.FromSeconds(3),
            "cue:cue-1");

        var onTime = window.RecordResponse(RuntimeDuration.FromSeconds(12).ToInstant());
        var early = window.RecordResponse(RuntimeDuration.FromSeconds(8).ToInstant());
        var late = window.RecordResponse(RuntimeDuration.FromSeconds(15).ToInstant());
        var pending = window.EvaluateNoResponse(RuntimeDuration.FromSeconds(12).ToInstant());
        var missed = window.EvaluateNoResponse(RuntimeDuration.FromSeconds(14).ToInstant());

        Assert.Equal(TimeSpan.FromSeconds(13), window.Deadline.Offset);
        Assert.Equal(RuntimeResponseTimingOutcome.OnTime, onTime.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(2), onTime.ResponseTime?.Value);
        Assert.Contains(onTime.EvidenceFacts, fact => fact.Name == "timing_outcome" && fact.Value == "on_time");
        Assert.Contains(onTime.EvidenceFacts, fact => fact.Name == "response_time" && fact.Value == "00:00:02");

        Assert.Equal(RuntimeResponseTimingOutcome.Early, early.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(2), early.EarlyBy?.Value);
        Assert.Contains(early.EvidenceFacts, fact => fact.Name == "early_by" && fact.Value == "00:00:02");

        Assert.Equal(RuntimeResponseTimingOutcome.Late, late.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(2), late.LateBy?.Value);
        Assert.Contains(late.EvidenceFacts, fact => fact.Name == "late_by" && fact.Value == "00:00:02");

        Assert.Equal(RuntimeResponseTimingOutcome.Pending, pending.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(1), pending.Remaining?.Value);
        Assert.Contains(pending.EvidenceFacts, fact => fact.Name == "remaining" && fact.Value == "00:00:01");

        Assert.Equal(RuntimeResponseTimingOutcome.Missed, missed.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(1), missed.LateBy?.Value);
        Assert.Null(missed.RespondedAt);
        Assert.Contains(missed.EvidenceFacts, fact => fact.Name == "timing_outcome" && fact.Value == "missed");
        Assert.Contains(missed.EvidenceFacts, fact => fact.Name == "late_by" && fact.Value == "00:00:01");
    }

    [Fact]
    public void RecoveryWindowsClassifyDriftAndDisruptionRecoveryWithEvidence()
    {
        var driftRecovery = RuntimeRecoveryWindow.AfterDrift(
            "drift-recovery-1",
            "drift-1",
            RuntimeDuration.FromSeconds(30).ToInstant(),
            RuntimeDuration.FromSeconds(10));
        var disruptionRecovery = RuntimeRecoveryWindow.AfterDisruption(
            "disruption-recovery-1",
            "interruption-1",
            RuntimeDuration.FromSeconds(50).ToInstant(),
            RuntimeDuration.FromSeconds(5));

        var driftRecovered = driftRecovery.RecordRecovery(RuntimeDuration.FromSeconds(37).ToInstant());
        var disruptionRecovered = disruptionRecovery.RecordRecovery(RuntimeDuration.FromSeconds(53).ToInstant());
        var disruptionLate = disruptionRecovery.RecordRecovery(RuntimeDuration.FromSeconds(58).ToInstant());
        var disruptionMissed = disruptionRecovery.EvaluateNoRecovery(RuntimeDuration.FromSeconds(56).ToInstant());

        Assert.Equal(RuntimeRecoveryTimingOutcome.Recovered, driftRecovered.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(7), driftRecovered.RecoveryTime?.Value);
        Assert.Contains(driftRecovered.EvidenceFacts, fact => fact.Name == "recovery_trigger_kind" && fact.Value == "drift");
        Assert.Contains(driftRecovered.EvidenceFacts, fact => fact.Name == "trigger_id" && fact.Value == "drift-1");
        Assert.Contains(driftRecovered.EvidenceFacts, fact => fact.Name == "recovery_time" && fact.Value == "00:00:07");

        Assert.Equal(RuntimeRecoveryTimingOutcome.Recovered, disruptionRecovered.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(3), disruptionRecovered.RecoveryTime?.Value);
        Assert.Contains(disruptionRecovered.EvidenceFacts, fact => fact.Name == "recovery_trigger_kind" && fact.Value == "disruption");

        Assert.Equal(RuntimeRecoveryTimingOutcome.Late, disruptionLate.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(3), disruptionLate.LateBy?.Value);
        Assert.Contains(disruptionLate.EvidenceFacts, fact => fact.Name == "recovery_outcome" && fact.Value == "late");

        Assert.Equal(RuntimeRecoveryTimingOutcome.Missed, disruptionMissed.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(1), disruptionMissed.LateBy?.Value);
        Assert.Null(disruptionMissed.RecoveredAt);
        Assert.Contains(disruptionMissed.EvidenceFacts, fact => fact.Name == "recovery_outcome" && fact.Value == "missed");
    }

    [Fact]
    public void TimingPrimitivesRejectMissingOrImpossibleWindows()
    {
        Assert.Throws<ArgumentException>(() => new RuntimeResponseWindow(
            " ",
            RuntimeInstant.Zero,
            RuntimeDuration.FromSeconds(1),
            "cue:cue-1"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeResponseWindow(
            "cue-response-1",
            RuntimeInstant.Zero,
            RuntimeDuration.Zero,
            "cue:cue-1"));
        Assert.Throws<ArgumentException>(() => RuntimeRecoveryWindow.AfterDrift(
            "drift-recovery-1",
            " ",
            RuntimeInstant.Zero,
            RuntimeDuration.FromSeconds(10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeRecoveryWindow.AfterDisruption(
            "disruption-recovery-1",
            "interruption-1",
            RuntimeInstant.Zero,
            RuntimeDuration.Zero));
    }
}
