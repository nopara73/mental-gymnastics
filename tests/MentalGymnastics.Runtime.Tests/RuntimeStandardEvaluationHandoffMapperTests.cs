using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class RuntimeStandardEvaluationHandoffMapperTests
{
    [Fact]
    public void ProducesEveryMeasurementAndConstraintRequiredByAllFortyStandards()
    {
        foreach (var definition in ExecutableStandardCatalog.Standards)
        {
            var result = Result(definition.Branch, definition.Level, definition.Drill, [], []);

            var handoff = RuntimeStandardEvaluationHandoffMapper.Map(result, []);

            Assert.Equal(
                definition.EvaluatedStandard.NumericThresholds.Select(threshold => threshold.MeasurementName).Order(),
                handoff.Measurements.Select(measurement => measurement.Name).Order());
            Assert.Equal(
                definition.EvaluatedStandard.CriticalConstraints.Select(constraint => constraint.Id).Order(),
                handoff.CriticalConstraintChecks.Select(check => check.Id).Order());
        }
    }

    [Fact]
    public void MapsCompletedFocusHoldBoundaryToPassingCoreEvidence()
    {
        var active = CompletedPhase("active-work", RuntimeSessionPhaseKind.ActiveWork, 180);
        var result = Result(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            [active],
            [Event(1, RuntimeEventKind.PhaseStarted, "active-work", RuntimeSessionPhaseKind.ActiveWork)]);

        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(
            result,
            [
                new("TargetStatement", "target", "blue circle"),
                new("DriftMarkingEvidenceShape", "drifts", "mark every drift"),
            ]);
        var evaluated = StandardEvaluator.Evaluate(
            ExecutableStandardCatalog.Get(BranchCode.FH, GlobalLevelId.L1).EvaluatedStandard,
            Attempt(handoff));

        Assert.True(evaluated.Passed);
    }

    [Fact]
    public void ScoresRequiredAndWithheldCuesWithoutSelfCertification()
    {
        var events = new List<RuntimeEvent>();
        long sequence = 1;
        for (var index = 1; index <= 10; index++)
        {
            var invalid = index % 3 == 0;
            events.Add(Event(
                sequence++,
                RuntimeEventKind.CueEmitted,
                "cue-response",
                RuntimeSessionPhaseKind.CueResponse,
                new("cue_id", $"cue-{index}"),
                new("response_expectation", invalid ? "no_response_expected" : "response_required")));
            if (!invalid)
            {
                events.Add(Event(
                    sequence++,
                    RuntimeEventKind.CueResponseSubmitted,
                    "cue-response",
                    RuntimeSessionPhaseKind.CueResponse,
                    new("cue_id", $"cue-{index}"),
                    new("response_outcome", "correct")));
            }
        }

        var result = Result(
            BranchCode.FS,
            GlobalLevelId.L3,
            DrillId.FS2InvalidCueFilter,
            [CompletedPhase("cue-response", RuntimeSessionPhaseKind.CueResponse, 300)],
            events);
        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(result, []);
        var evaluated = StandardEvaluator.Evaluate(
            ExecutableStandardCatalog.Get(BranchCode.FS, GlobalLevelId.L3).EvaluatedStandard,
            Attempt(handoff));

        Assert.True(evaluated.Passed);
    }

    [Fact]
    public void ScoresDelayedReconstructionAgainstHiddenExpectedItems()
    {
        var result = Result(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            [CompletedPhase("delay", RuntimeSessionPhaseKind.DelayWindow, 60)],
            [
                Event(1, RuntimeEventKind.PhaseStarted, "encode", RuntimeSessionPhaseKind.EncodeWindow),
                Event(
                    2,
                    RuntimeEventKind.AnswerSubmitted,
                    "reconstruction",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new("answer_id", "reconstruction"),
                    new("answer_reference", "alpha | bravo | cedar | delta")),
            ]);
        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(
            result,
            [
                new("ExpectedReconstruction", "expected", "alpha|bravo|cedar|delta|ember"),
            ]);
        var evaluated = StandardEvaluator.Evaluate(
            ExecutableStandardCatalog.Get(BranchCode.WM, GlobalLevelId.L1).EvaluatedStandard,
            Attempt(handoff));

        Assert.True(evaluated.Passed);
    }

    [Fact]
    public void RuleExtractionRequiresRuleSubmissionBeforeUnseenClassification()
    {
        var result = Result(
            BranchCode.CO,
            GlobalLevelId.L1,
            DrillId.CO1RuleExtraction,
            [],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "rule-statement",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new("answer_id", "rule-statement"),
                    new("answer_reference", "rule: include even values")),
                Event(2, RuntimeEventKind.PhaseStarted, "unseen-test", RuntimeSessionPhaseKind.ActiveWork),
                Event(
                    3,
                    RuntimeEventKind.AnswerSubmitted,
                    "unseen-test",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new("answer_id", "unseen-classifications"),
                    new("answer_reference", "1=include; 2=exclude")),
            ]);
        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(
            result,
            [
                new("ExpectedClassification", "expected-classification-1", "unseen example 1: include; key reason: even"),
                new("ExpectedClassification", "expected-classification-2", "unseen example 2: exclude; key reason: odd"),
            ]);
        var evaluated = StandardEvaluator.Evaluate(
            ExecutableStandardCatalog.Get(BranchCode.CO, GlobalLevelId.L1).EvaluatedStandard,
            Attempt(handoff));

        Assert.True(evaluated.Passed);
    }

    [Fact]
    public void CompositeBranchLabelsAloneDoNotCountAsEvidenceOrPassComponents()
    {
        var result = Result(
            BranchCode.TI,
            GlobalLevelId.L1,
            DrillId.TI1CompositeTask,
            [CompletedPhase("active-work", RuntimeSessionPhaseKind.ActiveWork, 600)],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "active-work",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new("answer_id", "composite"),
                    new("answer_reference", "FH FS")),
            ]);
        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(result, ComponentMaterials());
        var evaluated = StandardEvaluator.Evaluate(
            ExecutableStandardCatalog.Get(BranchCode.TI, GlobalLevelId.L1).EvaluatedStandard,
            Attempt(handoff));

        Assert.False(evaluated.Passed);
        Assert.Equal(
            0,
            handoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.ComponentEvidencePercent).Value);
        Assert.Equal(
            0,
            handoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.ComponentPassPercent).Value);
    }

    [Fact]
    public void CompositeComponentsRequireSeparateResponsesMatchingHiddenKeys()
    {
        var result = Result(
            BranchCode.TI,
            GlobalLevelId.L1,
            DrillId.TI1CompositeTask,
            [CompletedPhase("active-work", RuntimeSessionPhaseKind.ActiveWork, 600)],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "active-work",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new("answer_id", "composite"),
                    new("answer_reference", "FH=CEDAR-7; FS=A")),
            ]);
        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(result, ComponentMaterials());
        var evaluated = StandardEvaluator.Evaluate(
            ExecutableStandardCatalog.Get(BranchCode.TI, GlobalLevelId.L1).EvaluatedStandard,
            Attempt(handoff));

        Assert.True(evaluated.Passed);
        Assert.Equal(
            100,
            handoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.ComponentEvidencePercent).Value);
        Assert.Equal(
            100,
            handoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.ComponentPassPercent).Value);
    }

    [Fact]
    public void MarkedUncertaintyDoesNotTurnCorrectComponentEvidenceIntoAnError()
    {
        var result = Result(
            BranchCode.TI,
            GlobalLevelId.L1,
            DrillId.TI1CompositeTask,
            [CompletedPhase("active-work", RuntimeSessionPhaseKind.ActiveWork, 600)],
            [
                Event(
                    1,
                    RuntimeEventKind.GuessMarked,
                    "active-work",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new RuntimeEventFact("guess_id", "uncertainty-1")),
                Event(
                    2,
                    RuntimeEventKind.AnswerSubmitted,
                    "active-work",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new RuntimeEventFact("answer_id", "composite"),
                    new RuntimeEventFact("answer_reference", "FH=CEDAR-7; FS=A")),
            ]);

        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(result, ComponentMaterials());

        Assert.Equal(
            100,
            handoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.ComponentPassPercent).Value);
    }

    private static IReadOnlyList<RuntimeScoringMaterial> ComponentMaterials()
    {
        return
        [
            new("ComponentPayload", "component-fh", "component branch FH: challenge hold CEDAR-7"),
            new("ComponentPayload", "component-fs", "component branch FS: challenge report track A"),
            new("BranchScoringKey", "scoring-fh", "component branch FH; expected response CEDAR-7; scoring standard exact"),
            new("BranchScoringKey", "scoring-fs", "component branch FS; expected response A; scoring standard exact"),
        ];
    }

    private static StandardEvaluationAttempt Attempt(RuntimeStandardEvaluationHandoffInput handoff)
    {
        return new StandardEvaluationAttempt(
            handoff.Measurements,
            handoff.CriticalConstraintChecks,
            handoff.OutputComplete,
            handoff.RubricOutcome);
    }

    private static RuntimeSessionCompletionResult Result(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        IReadOnlyList<RuntimeCompletedSessionPhase> phases,
        IReadOnlyList<RuntimeEvent> events)
    {
        var standard = ProgramCatalog.Standards.Single(item => item.Branch == branch && item.Level == level);
        var definition = new RuntimeSessionDefinition(
            SessionType.Practice,
            branch,
            level,
            drill,
            [new LoadVariable("test load", "fixed")],
            standard,
            [new CriticalConstraint("Keep the stated constraint.")]);
        return new RuntimeSessionCompletionResult(
            "session",
            definition,
            RuntimeSessionCompletionStatus.Completed,
            phases,
            events,
            scoringEvents: [],
            scoringFacts: [],
            evidenceDrafts: [],
            new RuntimeSessionEvidenceSummary(0, [], [], []),
            failureRelevantFacts: [],
            resultFacts: [],
            completedAt: new RuntimeInstant(TimeSpan.FromMinutes(20)));
    }

    private static RuntimeCompletedSessionPhase CompletedPhase(
        string id,
        RuntimeSessionPhaseKind kind,
        int seconds)
    {
        var duration = RuntimeDuration.FromSeconds(seconds);
        return new RuntimeCompletedSessionPhase(
            RuntimeSessionPhaseDefinition.Timed(id, kind, duration),
            RuntimeInstant.Zero,
            new RuntimeInstant(duration.Value),
            duration,
            RuntimeSessionPhaseCompletionCause.Timeout);
    }

    private static RuntimeEvent Event(
        long sequence,
        RuntimeEventKind kind,
        string? phaseId,
        RuntimeSessionPhaseKind? phaseKind,
        params RuntimeEventFact[] facts)
    {
        return new RuntimeEvent(
            "session",
            sequence,
            kind,
            new RuntimeInstant(TimeSpan.FromSeconds(sequence)),
            phaseId,
            phaseKind,
            facts);
    }
}
