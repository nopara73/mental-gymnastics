using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class DiscriminationRuntimeProtocolTests
{
    [Fact]
    public void PairDiscriminationRecordsMarkedGuessesFalsePositiveFalseNegativeAndComparisonEvidence()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = DiscriminationRuntimeProtocol.Start(
            "de1-session",
            DiscriminationSession(DrillId.DE1PairDiscrimination, GlobalLevelId.L1),
            clock);

        var activeBeforePairs = protocol.StartActiveSet();

        Assert.False(activeBeforePairs.IsAccepted);
        Assert.Equal(DiscriminationRuntimeProtocolInvalidReason.PairSetRequiredBeforeSet, activeBeforePairs.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        Assert.True(protocol.StatePairSet(
            [
                new DiscriminationRuntimePair("pair-1", "red square", "red square with dot", expectedDifferent: true),
                new DiscriminationRuntimePair("pair-2", "blue circle", "blue circle", expectedDifferent: false),
                new DiscriminationRuntimePair("pair-3", "green triangle tall", "green triangle short", expectedDifferent: true),
            ]).IsAccepted);
        Assert.True(protocol.StartActiveSet().IsAccepted);

        var markedGuess = protocol.MarkGuess("pair-2", "same color and shape, unsure about size");
        var correct = protocol.SubmitComparison("pair-1", reportedDifferent: true);
        var falsePositive = protocol.SubmitComparison("pair-2", reportedDifferent: true);
        var falseNegative = protocol.SubmitComparison("pair-3", reportedDifferent: false);
        var completed = protocol.CompleteSet("set-1");

        Assert.True(markedGuess.IsAccepted);
        Assert.True(correct.IsAccepted);
        Assert.True(falsePositive.IsAccepted);
        Assert.True(falseNegative.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Equal(RuntimeEventKind.GuessMarked, markedGuess.Event!.Kind);
        Assert.Contains(markedGuess.Event.Facts, fact => fact.Name == "pair_id" && fact.Value == "pair-2");
        Assert.Contains(markedGuess.Event.Facts, fact => fact.Name == "guess_marked" && fact.Value == "true");
        Assert.Contains(correct.Event!.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(falsePositive.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "false_positive");
        Assert.Contains(falsePositive.Event.Facts, fact => fact.Name == "failed_item_list" && fact.Value.Contains("pair-2", StringComparison.Ordinal));
        Assert.Contains(falseNegative.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "false_negative");
        Assert.Contains(falseNegative.Event.Facts, fact => fact.Name == "failed_item_list" && fact.Value.Contains("pair-3", StringComparison.Ordinal));
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "comparison_accuracy" && fact.Value == "1/3");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "false_positive_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "false_negative_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "marked_guess_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "comparison" && fact.Value.Contains("false_positives=1", StringComparison.Ordinal));

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.MarkedGuess);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);

        var failedEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "de1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            protocol.EventLog.Events,
            scoringEvents));
        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "de1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            failedEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.FailedItemList &&
                evidence.Description.Contains("false positive", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Comparison &&
                evidence.Description.Contains("false_positives=1", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("false_negative_count=1", StringComparison.Ordinal));
        Assert.DoesNotContain(
            protocol.EventLog.Events.SelectMany(runtimeEvent => runtimeEvent.Facts),
            fact => fact.Name is "gate_outcome" or "pass_state");
    }

    [Fact]
    public void SeededAuditLocksOriginalOutputRecordsSeededFindingsInventedErrorsAndEditAttempts()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = DiscriminationRuntimeProtocol.Start(
            "de2-session",
            DiscriminationSession(DrillId.DE2SeededAudit, GlobalLevelId.L3),
            clock);

        var auditBeforeOutput = protocol.StartAudit();

        Assert.False(auditBeforeOutput.IsAccepted);
        Assert.Equal(DiscriminationRuntimeProtocolInvalidReason.OriginalOutputRequiredBeforeAudit, auditBeforeOutput.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        var outputLocked = protocol.LockOriginalOutput(
            "output-1",
            "Original reconstruction: three causes, two examples, one unsupported claim.");
        Assert.True(outputLocked.IsAccepted);
        Assert.Contains(outputLocked.Event!.Facts, fact => fact.Name == "original_output_locked" && fact.Value == "true");
        Assert.Contains(outputLocked.Event.Facts, fact => fact.Name == "original_output_edit_allowed" && fact.Value == "false");

        Assert.True(protocol.StateSeededErrors(
            [
                new DiscriminationRuntimeSeededError("seed-1", "unsupported claim in final sentence"),
                new DiscriminationRuntimeSeededError("seed-2", "missing second example"),
            ]).IsAccepted);
        Assert.True(protocol.StartAudit().IsAccepted);

        var editAttempt = protocol.RecordOriginalOutputEditAttempt("changed unsupported claim before audit finished");
        var seededFinding = protocol.FindSeededError("seed-1", "Found unsupported claim in the final sentence.");
        var inventedError = protocol.RecordInventedError("invented-1", "Claimed the first example was wrong, but it was correct.");
        var completed = protocol.CompleteAudit("audit-1");

        Assert.True(editAttempt.IsAccepted);
        Assert.True(seededFinding.IsAccepted);
        Assert.True(inventedError.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(editAttempt.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "original_output_edit_attempt");
        Assert.Contains(editAttempt.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "original_output_locked_during_audit");
        Assert.Contains(editAttempt.Event.Facts, fact => fact.Name == "original_output_edit_allowed" && fact.Value == "false");
        Assert.Contains(seededFinding.Event!.Facts, fact => fact.Name == "audit_finding_result" && fact.Value == "seeded_error_found");
        Assert.Contains(seededFinding.Event.Facts, fact => fact.Name == "seeded_error_id" && fact.Value == "seed-1");
        Assert.Contains(inventedError.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "invented_error");
        Assert.Contains(inventedError.Event.Facts, fact => fact.Name == "invented_error_count" && fact.Value == "1");
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "audit_result" && fact.Value.Contains("seeded_errors_found=1/2", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "seeded_error_found_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "seeded_error_missed_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "invented_error_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "original_output_locked" && fact.Value == "true");

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);

        var failedEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "de2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            protocol.EventLog.Events,
            scoringEvents));
        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "de2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            failedEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.FailedItemList &&
                evidence.Description.Contains("changed unsupported claim", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.AuditResult &&
                evidence.Description.Contains("seeded_errors_found=1/2", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("invented_error_count=1", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscriminationProtocolRejectsWrongSessionsAndAuditForPairDiscriminationWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);

        Assert.Throws<ArgumentException>(() => DiscriminationRuntimeProtocol.Start(
            "wrong-branch",
            FocusHoldSession(),
            clock));

        var pairDiscrimination = DiscriminationRuntimeProtocol.Start(
            "de1-no-audit",
            DiscriminationSession(DrillId.DE1PairDiscrimination, GlobalLevelId.L1),
            clock);
        Assert.True(pairDiscrimination.StatePairSet(
            [new DiscriminationRuntimePair("pair-1", "A", "B", expectedDifferent: true)]).IsAccepted);
        Assert.True(pairDiscrimination.StartActiveSet().IsAccepted);
        var eventCountBeforeAudit = pairDiscrimination.EventLog.Events.Count;

        var outputLock = pairDiscrimination.LockOriginalOutput("output-1", "not part of DE-1");

        Assert.False(outputLock.IsAccepted);
        Assert.Equal(DiscriminationRuntimeProtocolInvalidReason.SeededAuditNotSupportedByDrill, outputLock.InvalidReason);
        Assert.Equal(eventCountBeforeAudit, pairDiscrimination.EventLog.Events.Count);
    }

    private static RuntimeSessionDefinition DiscriminationSession(DrillId drill, GlobalLevelId level)
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.DE &&
            standard.Level == level);
        CriticalConstraint[] constraints = drill == DrillId.DE2SeededAudit
            ?
            [
                new CriticalConstraint("Original output cannot be edited during audit."),
                new CriticalConstraint("Invented errors do not count as findings."),
            ]
            :
            [
                new CriticalConstraint("Guessing must be marked."),
                new CriticalConstraint("False positives and false negatives must remain within threshold."),
            ];
        LoadVariable[] loadVariables = drill == DrillId.DE2SeededAudit
            ?
            [
                new LoadVariable("error subtlety", "2 seeded errors"),
                new LoadVariable("audit delay", "5 minutes"),
            ]
            :
            [
                new LoadVariable("similarity", "simple pairs"),
                new LoadVariable("quantity", "3 comparisons"),
            ];

        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.DE,
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
