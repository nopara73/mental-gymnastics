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
    public void WorkingMemoryRequiresSequenceOrderAndExplicitOperationExplanation()
    {
        RuntimeScoringMaterial[] memoryMaterials =
        [
            new("ExpectedReconstruction", "expected", "alpha|bravo|cedar|delta|ember"),
        ];
        var reordered = Result(
            BranchCode.WM,
            GlobalLevelId.L2,
            DrillId.WM1DelayedReconstruction,
            [CompletedPhase("delay", RuntimeSessionPhaseKind.DelayWindow, 90)],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "reconstruction",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new RuntimeEventFact("answer_reference", "ember|delta|cedar|bravo|alpha")),
            ]);
        var reorderedHandoff = RuntimeStandardEvaluationHandoffMapper.Map(reordered, memoryMaterials);

        Assert.Equal(20, Measurement(
            reorderedHandoff,
            TrainingStandardMeasurements.ReconstructionAccuracyPercent));
        Assert.Equal(0, Measurement(reorderedHandoff, TrainingStandardMeasurements.InventedItemCount));

        var omitted = Result(
            BranchCode.WM,
            GlobalLevelId.L2,
            DrillId.WM1DelayedReconstruction,
            [CompletedPhase("delay", RuntimeSessionPhaseKind.DelayWindow, 90)],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "reconstruction",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new RuntimeEventFact("answer_reference", RuntimeResponseMarkers.Omitted)),
            ]);
        var omittedHandoff = RuntimeStandardEvaluationHandoffMapper.Map(omitted, memoryMaterials);

        Assert.Equal(0, Measurement(
            omittedHandoff,
            TrainingStandardMeasurements.ReconstructionAccuracyPercent));
        Assert.Equal(0, Measurement(omittedHandoff, TrainingStandardMeasurements.InventedItemCount));
        Assert.False(omittedHandoff.OutputComplete);

        RuntimeScoringMaterial[] transformMaterials =
        [
            new("FinalExpectedOutput", "expected", "cedar|bravo|alpha"),
            new("OperationStep", "operation-step-1", "Step 1: reverse the held source item order."),
            new("OperationStep", "operation-step-2", "Step 2: rotate the current order one position left."),
        ];
        var vagueTransform = Result(
            BranchCode.WM,
            GlobalLevelId.L3,
            DrillId.WM2MentalTransform,
            [CompletedPhase("delay", RuntimeSessionPhaseKind.DelayWindow, 120)],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "reconstruction",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new RuntimeEventFact("answer_reference", "RESULT=cedar|bravo|alpha; RULE=I did the operations mentally")),
            ]);
        var exactTransform = Result(
            BranchCode.WM,
            GlobalLevelId.L3,
            DrillId.WM2MentalTransform,
            [CompletedPhase("delay", RuntimeSessionPhaseKind.DelayWindow, 120)],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "reconstruction",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new RuntimeEventFact("answer_reference", "RESULT=cedar|bravo|alpha; RULE=reverse the order, then rotate left one position")),
            ]);

        Assert.Equal(0, Measurement(
            RuntimeStandardEvaluationHandoffMapper.Map(vagueTransform, transformMaterials),
            TrainingStandardMeasurements.RuleExplanationCorrect));
        Assert.Equal(1, Measurement(
            RuntimeStandardEvaluationHandoffMapper.Map(exactTransform, transformMaterials),
            TrainingStandardMeasurements.RuleExplanationCorrect));
    }

    [Fact]
    public void UnseenClassificationsAreScoredAgainstTheirOwnExampleIndexes()
    {
        RuntimeScoringMaterial[] materials =
        [
            new("ExpectedClassification", "expected-classification-1", "unseen example 1: positive; key reason: even"),
            new("ExpectedClassification", "expected-classification-2", "unseen example 2: negative; key reason: odd"),
        ];
        var swapped = Result(
            BranchCode.CO,
            GlobalLevelId.L1,
            DrillId.CO1RuleExtraction,
            [],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "unseen-test",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new RuntimeEventFact("answer_reference", "1=negative; 2=positive")),
            ]);
        var exact = Result(
            BranchCode.CO,
            GlobalLevelId.L1,
            DrillId.CO1RuleExtraction,
            [],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "unseen-test",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new RuntimeEventFact("answer_reference", "1=positive; 2=negative")),
            ]);

        Assert.Equal(0, Measurement(
            RuntimeStandardEvaluationHandoffMapper.Map(swapped, materials),
            TrainingStandardMeasurements.UnseenClassificationAccuracyPercent));
        Assert.Equal(100, Measurement(
            RuntimeStandardEvaluationHandoffMapper.Map(exact, materials),
            TrainingStandardMeasurements.UnseenClassificationAccuracyPercent));
    }

    [Fact]
    public void SeededAuditRequiresLineTypeAndExactCorrectionInTheSameFinding()
    {
        RuntimeScoringMaterial[] materials =
        [
            new(
                "SeededError",
                "seeded-error-1",
                "seeded error route-crew-count: location line 2; type number mismatch; " +
                "criticality critical; source requires two crews, not three"),
            new(
                "ExpectedFinding",
                "expected-finding-1",
                "finding route-crew-count: finding: line 2 should report two crews instead of three"),
        ];
        var lineOnly = Result(
            BranchCode.DE,
            GlobalLevelId.L3,
            DrillId.DE2SeededAudit,
            [CompletedPhase("delay", RuntimeSessionPhaseKind.DelayWindow, 300)],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "seeded-audit",
                    RuntimeSessionPhaseKind.Audit,
                    new RuntimeEventFact("answer_reference", "FINDING-1=line 2")),
            ]);
        var exact = Result(
            BranchCode.DE,
            GlobalLevelId.L3,
            DrillId.DE2SeededAudit,
            [CompletedPhase("delay", RuntimeSessionPhaseKind.DelayWindow, 300)],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "seeded-audit",
                    RuntimeSessionPhaseKind.Audit,
                    new RuntimeEventFact("answer_reference", "FINDING-1=line 2, number mismatch, report two crews")),
            ]);
        var lineOnlyHandoff = RuntimeStandardEvaluationHandoffMapper.Map(lineOnly, materials);
        var exactHandoff = RuntimeStandardEvaluationHandoffMapper.Map(exact, materials);

        Assert.Equal(0, Measurement(lineOnlyHandoff, TrainingStandardMeasurements.SeededErrorDetectionPercent));
        Assert.Equal(1, Measurement(lineOnlyHandoff, TrainingStandardMeasurements.FalseCorrectionCount));
        Assert.Equal(100, Measurement(exactHandoff, TrainingStandardMeasurements.SeededErrorDetectionPercent));
        Assert.Equal(0, Measurement(exactHandoff, TrainingStandardMeasurements.FalseCorrectionCount));
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
                    new("answer_reference", "include even values; exclude odd values")),
                Event(2, RuntimeEventKind.PhaseStarted, "unseen-test", RuntimeSessionPhaseKind.ReconstructionInput),
                Event(
                    3,
                    RuntimeEventKind.AnswerSubmitted,
                    "unseen-test",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new("answer_id", "unseen-classifications"),
                    new("answer_reference", "1=include; 2=exclude")),
            ]);
        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(
            result,
            [
                new("ExpectedRule", "expected-rule", "Include even values; exclude odd values."),
                new("ExpectedClassification", "expected-classification-1", "unseen example 1: include; key reason: even"),
                new("ExpectedClassification", "expected-classification-2", "unseen example 2: exclude; key reason: odd"),
            ]);
        var evaluated = StandardEvaluator.Evaluate(
            ExecutableStandardCatalog.Get(BranchCode.CO, GlobalLevelId.L1).EvaluatedStandard,
            Attempt(handoff));

        Assert.True(evaluated.Passed);
    }

    [Fact]
    public void CorrectUnseenClassificationsCannotRescueAnUnrelatedPrestatedRule()
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
                    new("answer_reference", "always include every value")),
                Event(2, RuntimeEventKind.PhaseStarted, "unseen-test", RuntimeSessionPhaseKind.ReconstructionInput),
                Event(
                    3,
                    RuntimeEventKind.AnswerSubmitted,
                    "unseen-test",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new("answer_id", "unseen-classifications"),
                    new("answer_reference", "1=include; 2=exclude")),
            ]);
        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(
            result,
            [
                new("ExpectedRule", "expected-rule", "Include even values; exclude odd values."),
                new("ExpectedClassification", "expected-classification-1", "unseen example 1: include; key reason: even"),
                new("ExpectedClassification", "expected-classification-2", "unseen example 2: exclude; key reason: odd"),
            ]);

        Assert.False(handoff.CriticalConstraintChecks.Single(check =>
            check.Id == TrainingStandardConstraints.RuleStatedBeforeUnseen).Satisfied);
        Assert.False(StandardEvaluator.Evaluate(
            ExecutableStandardCatalog.Get(BranchCode.CO, GlobalLevelId.L1).EvaluatedStandard,
            Attempt(handoff)).Passed);
    }

    [Fact]
    public void InhibitionRuleMaterialDoesNotCountAsAUserDeclaration()
    {
        var result = Result(
            BranchCode.IR,
            GlobalLevelId.L1,
            DrillId.IR1GoNoGoRule,
            [],
            [
                Event(1, RuntimeEventKind.PhaseStarted, "cue-response", RuntimeSessionPhaseKind.CueResponse),
                Event(
                    2,
                    RuntimeEventKind.CueEmitted,
                    "cue-response",
                    RuntimeSessionPhaseKind.CueResponse,
                    new("cue_id", "cue-1"),
                    new("response_expectation", "response_required")),
                Event(
                    3,
                    RuntimeEventKind.CueResponseSubmitted,
                    "cue-response",
                    RuntimeSessionPhaseKind.CueResponse,
                    new("cue_id", "cue-1"),
                    new("response_outcome", "correct")),
            ]);

        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(
            result,
            [new("RuleStatement", "rule", "Tap blue circles; withhold red squares.")]);

        Assert.False(handoff.CriticalConstraintChecks.Single(check =>
            check.Id == TrainingStandardConstraints.RuleStatedBeforeSet).Satisfied);
    }

    [Fact]
    public void InhibitionDeclarationMustNameBothActionsAndTheirCueClasses()
    {
        RuntimeScoringMaterial[] materials =
        [
            new(
                "RuleStatement",
                "rule",
                "Rule before set: respond only to go cues and withhold every no-go cue; " +
                "the rule cannot be changed after the cue stream starts."),
        ];
        var phaseStart = Event(
            3,
            RuntimeEventKind.PhaseStarted,
            "cue-response",
            RuntimeSessionPhaseKind.CueResponse);
        var vague = Result(
            BranchCode.IR,
            GlobalLevelId.L1,
            DrillId.IR1GoNoGoRule,
            [],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "rule-declaration",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new RuntimeEventFact("answer_reference", "RULE=the rule is locked before the set")),
                phaseStart,
            ]);
        var exact = Result(
            BranchCode.IR,
            GlobalLevelId.L1,
            DrillId.IR1GoNoGoRule,
            [],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "rule-declaration",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new RuntimeEventFact("answer_reference", "RULE=respond to GO cues; withhold every NO-GO cue")),
                phaseStart,
            ]);

        Assert.False(RuntimeStandardEvaluationHandoffMapper.Map(vague, materials)
            .CriticalConstraintChecks.Single(check =>
                check.Id == TrainingStandardConstraints.RuleStatedBeforeSet).Satisfied);
        Assert.True(RuntimeStandardEvaluationHandoffMapper.Map(exact, materials)
            .CriticalConstraintChecks.Single(check =>
                check.Id == TrainingStandardConstraints.RuleStatedBeforeSet).Satisfied);
    }

    [Fact]
    public void InhibitionDeclarationMustStateTheRuleAndEveryExceptionBeforeCues()
    {
        var result = Result(
            BranchCode.IR,
            GlobalLevelId.L2,
            DrillId.IR2ExceptionRule,
            [],
            [
                Event(1, RuntimeEventKind.PhaseStarted, "rule-declaration", RuntimeSessionPhaseKind.ActiveWork),
                Event(
                    2,
                    RuntimeEventKind.AnswerSubmitted,
                    "rule-declaration",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new("answer_id", "rule-declaration"),
                    new("answer_reference", "Tap blue circles; withhold red squares; triangle -> withhold; diamond -> tap.")),
                Event(3, RuntimeEventKind.PhaseStarted, "cue-response", RuntimeSessionPhaseKind.CueResponse),
            ]);

        var handoff = RuntimeStandardEvaluationHandoffMapper.Map(
            result,
            [
                new("RuleStatement", "rule", "Tap blue circles; withhold red squares."),
                new("ExceptionDefinition", "exception-1", "exception 1: triangle -> withhold"),
                new("ExceptionDefinition", "exception-2", "exception 2: diamond -> tap"),
            ]);

        Assert.Equal(
            100,
            handoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.ExceptionStatementPercent).Value);
        Assert.All(
            handoff.CriticalConstraintChecks.Where(check => check.Id is
                TrainingStandardConstraints.RuleStatedBeforeSet or
                TrainingStandardConstraints.ExceptionsStatedBeforeSet),
            check => Assert.True(check.Satisfied));
    }

    [Fact]
    public void CueRecoveryRequiresAnExactCorrectionLinkedToTheFailedCue()
    {
        RuntimeEvent[] failedCue =
        [
            Event(
                1,
                RuntimeEventKind.CueEmitted,
                "cue-response",
                RuntimeSessionPhaseKind.CueResponse,
                new("cue_id", "cue-1"),
                new("response_expectation", "response_required")),
            Event(
                2,
                RuntimeEventKind.CueResponseSubmitted,
                "cue-response",
                RuntimeSessionPhaseKind.CueResponse,
                new("cue_id", "cue-1"),
                new("expected_response", "tap"),
                new("response", "withhold"),
                new("response_outcome", "incorrect")),
        ];
        var wrongCorrection = failedCue.Append(Event(
            3,
            RuntimeEventKind.CorrectionSubmitted,
            "cue-response",
            RuntimeSessionPhaseKind.CueResponse,
            new("corrected_event_sequence", "2"),
            new("corrected_cue_id", "cue-1"),
            new("correction_outcome", "incorrect"))).ToArray();
        var exactCorrection = failedCue.Append(Event(
            3,
            RuntimeEventKind.CorrectionSubmitted,
            "cue-response",
            RuntimeSessionPhaseKind.CueResponse,
            new("corrected_event_sequence", "2"),
            new("corrected_cue_id", "cue-1"),
            new("correction_outcome", "correct"))).ToArray();

        var unrecovered = RuntimeStandardEvaluationHandoffMapper.Map(
            Result(BranchCode.IR, GlobalLevelId.L3, DrillId.IR2ExceptionRule, [], failedCue),
            []);
        var falselyClaimed = RuntimeStandardEvaluationHandoffMapper.Map(
            Result(BranchCode.IR, GlobalLevelId.L3, DrillId.IR2ExceptionRule, [], wrongCorrection),
            []);
        var recovered = RuntimeStandardEvaluationHandoffMapper.Map(
            Result(BranchCode.IR, GlobalLevelId.L3, DrillId.IR2ExceptionRule, [], exactCorrection),
            []);
        var focusShiftUnrecovered = RuntimeStandardEvaluationHandoffMapper.Map(
            Result(BranchCode.FS, GlobalLevelId.L2, DrillId.FS1CueSwitch, [], failedCue),
            []);
        var focusShiftFalseClaim = RuntimeStandardEvaluationHandoffMapper.Map(
            Result(BranchCode.FS, GlobalLevelId.L2, DrillId.FS1CueSwitch, [], wrongCorrection),
            []);
        var focusShiftRecovered = RuntimeStandardEvaluationHandoffMapper.Map(
            Result(BranchCode.FS, GlobalLevelId.L2, DrillId.FS1CueSwitch, [], exactCorrection),
            []);

        Assert.Equal(decimal.MaxValue, Measurement(unrecovered, TrainingStandardMeasurements.MaximumCorrectionDistance));
        Assert.Equal(decimal.MaxValue, Measurement(falselyClaimed, TrainingStandardMeasurements.MaximumCorrectionDistance));
        Assert.Equal(0, Measurement(recovered, TrainingStandardMeasurements.MaximumCorrectionDistance));
        Assert.Equal(0, Measurement(recovered, TrainingStandardMeasurements.AccuracyPercent));
        Assert.Equal(1, Measurement(focusShiftUnrecovered, TrainingStandardMeasurements.UnrecoveredErrorCount));
        Assert.Equal(1, Measurement(focusShiftFalseClaim, TrainingStandardMeasurements.UnrecoveredErrorCount));
        Assert.Equal(0, Measurement(focusShiftRecovered, TrainingStandardMeasurements.UnrecoveredErrorCount));
    }

    [Fact]
    public void PressureRepeatCannotPassAnInhibitionSourceWithoutItsDeclaration()
    {
        RuntimeScoringMaterial[] materials =
        [
            new("SourceBranchStandard", "source", "Original IR standard remains visible."),
            new("RuleStatement", "rule", "Tap blue circles; withhold red squares."),
            new("ExceptionDefinition", "exception-1", "exception 1: triangle -> withhold"),
        ];
        var cueEvents = new[]
        {
            Event(1, RuntimeEventKind.PhaseStarted, "cue-response", RuntimeSessionPhaseKind.CueResponse),
            Event(
                2,
                RuntimeEventKind.CueEmitted,
                "cue-response",
                RuntimeSessionPhaseKind.CueResponse,
                new("cue_id", "cue-1"),
                new("response_expectation", "response_required")),
            Event(
                3,
                RuntimeEventKind.CueResponseSubmitted,
                "cue-response",
                RuntimeSessionPhaseKind.CueResponse,
                new("cue_id", "cue-1"),
                new("response_outcome", "correct")),
        };
        var withoutDeclaration = Result(
            BranchCode.AI,
            GlobalLevelId.L1,
            DrillId.AI1PressureRepeat,
            [],
            cueEvents,
            sourceDrill: DrillId.IR2ExceptionRule);
        var withDeclaration = Result(
            BranchCode.AI,
            GlobalLevelId.L1,
            DrillId.AI1PressureRepeat,
            [],
            [
                Event(1, RuntimeEventKind.PhaseStarted, "rule-declaration", RuntimeSessionPhaseKind.ActiveWork),
                Event(
                    2,
                    RuntimeEventKind.AnswerSubmitted,
                    "rule-declaration",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new("answer_id", "rule-declaration"),
                    new("answer_reference", "Tap blue circles; withhold red squares; triangle -> withhold.")),
                Event(3, RuntimeEventKind.PhaseStarted, "cue-response", RuntimeSessionPhaseKind.CueResponse),
                Event(
                    4,
                    RuntimeEventKind.CueEmitted,
                    "cue-response",
                    RuntimeSessionPhaseKind.CueResponse,
                    new("cue_id", "cue-1"),
                    new("response_expectation", "response_required")),
                Event(
                    5,
                    RuntimeEventKind.CueResponseSubmitted,
                    "cue-response",
                    RuntimeSessionPhaseKind.CueResponse,
                    new("cue_id", "cue-1"),
                    new("response_outcome", "correct")),
            ],
            sourceDrill: DrillId.IR2ExceptionRule);

        var withoutHandoff = RuntimeStandardEvaluationHandoffMapper.Map(withoutDeclaration, materials);
        var withHandoff = RuntimeStandardEvaluationHandoffMapper.Map(withDeclaration, materials);

        Assert.Equal(
            0,
            withoutHandoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.SourceStandardPassed).Value);
        Assert.Equal(
            1,
            withHandoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.SourceStandardPassed).Value);
    }

    [Fact]
    public void PressureWorkCannotPassAShortenedFocusHoldSource()
    {
        RuntimeScoringMaterial[] materials =
        [
            new("SourceBranchStandard", "source", "Original FH L3 standard remains visible."),
            new("TargetStatement", "target", "blue dot"),
            new("DriftMarkingEvidenceShape", "drift-control", "Mark every drift and return."),
        ];
        RuntimeEvent[] events =
        [
            Event(1, RuntimeEventKind.PhaseStarted, "active-work", RuntimeSessionPhaseKind.ActiveWork),
        ];
        var shortened = Result(
            BranchCode.AI,
            GlobalLevelId.L1,
            DrillId.AI1PressureRepeat,
            [CompletedPhase("active-work", RuntimeSessionPhaseKind.ActiveWork, 299)],
            events,
            sourceDrill: DrillId.FH2DistractorHold);
        var complete = Result(
            BranchCode.AI,
            GlobalLevelId.L1,
            DrillId.AI1PressureRepeat,
            [CompletedPhase("active-work", RuntimeSessionPhaseKind.ActiveWork, 300)],
            events,
            sourceDrill: DrillId.FH2DistractorHold);

        var shortenedHandoff = RuntimeStandardEvaluationHandoffMapper.Map(shortened, materials);
        var completeHandoff = RuntimeStandardEvaluationHandoffMapper.Map(complete, materials);

        Assert.Equal(
            0,
            shortenedHandoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.SourceStandardPassed).Value);
        Assert.Equal(
            1,
            completeHandoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.SourceStandardPassed).Value);
    }

    [Fact]
    public void PressureSourceUsesVisibleFocusShiftThresholdAndStillForbidsInvalidCueTaps()
    {
        RuntimeScoringMaterial[] materials =
        [
            new("SourceBranchStandard", "source", "Original FS L3 standard remains visible."),
        ];
        var phases = new[]
        {
            CompletedPhase("cue-response", RuntimeSessionPhaseKind.CueResponse, 60),
        };
        var thresholdResult = Result(
            BranchCode.AI,
            GlobalLevelId.L1,
            DrillId.AI1PressureRepeat,
            phases,
            FocusShiftSourceEvents(invalidTap: false),
            sourceDrill: DrillId.FS2InvalidCueFilter);
        var invalidTapResult = Result(
            BranchCode.AI,
            GlobalLevelId.L1,
            DrillId.AI1PressureRepeat,
            phases,
            FocusShiftSourceEvents(invalidTap: true),
            sourceDrill: DrillId.FS2InvalidCueFilter);

        var thresholdHandoff = RuntimeStandardEvaluationHandoffMapper.Map(thresholdResult, materials);
        var invalidTapHandoff = RuntimeStandardEvaluationHandoffMapper.Map(invalidTapResult, materials);

        Assert.Equal(1, Measurement(thresholdHandoff, TrainingStandardMeasurements.SourceStandardPassed));
        Assert.Equal(0, Measurement(invalidTapHandoff, TrainingStandardMeasurements.SourceStandardPassed));
    }

    [Fact]
    public void StructureMappingRequiresSourceTargetAndPreservingEvidence()
    {
        RuntimeScoringMaterial[] materials =
        [
            new(
                "ExpectedMapping",
                "expected-mapping-1",
                "relation-1: containment; expected source relation inside boundary; " +
                "expected target relation nested region; preserving evidence both encode membership."),
        ];
        var labelOnly = Result(
            BranchCode.CO,
            GlobalLevelId.L3,
            DrillId.CO2StructureMapping,
            [],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "relation-naming",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new("answer_id", "relation-naming"),
                    new("answer_reference", "containment")),
                Event(2, RuntimeEventKind.PhaseStarted, "mapping-input", RuntimeSessionPhaseKind.ReconstructionInput),
                Event(
                    3,
                    RuntimeEventKind.AnswerSubmitted,
                    "mapping-input",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new("answer_id", "mapping-input"),
                    new("answer_reference", "1=containment because the relation is preserved")),
            ]);
        var completeMapping = Result(
            BranchCode.CO,
            GlobalLevelId.L3,
            DrillId.CO2StructureMapping,
            [],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "relation-naming",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new("answer_id", "relation-naming"),
                    new("answer_reference", "inside boundary")),
                Event(2, RuntimeEventKind.PhaseStarted, "mapping-input", RuntimeSessionPhaseKind.ReconstructionInput),
                Event(
                    3,
                    RuntimeEventKind.AnswerSubmitted,
                    "mapping-input",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new("answer_id", "mapping-input"),
                    new("answer_reference", "1=inside boundary -> nested region because both preserve membership")),
            ]);

        var labelOnlyHandoff = RuntimeStandardEvaluationHandoffMapper.Map(labelOnly, materials);
        var completeHandoff = RuntimeStandardEvaluationHandoffMapper.Map(completeMapping, materials);

        Assert.Equal(
            0,
            labelOnlyHandoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.RelationPreservationPercent).Value);
        Assert.False(labelOnlyHandoff.CriticalConstraintChecks.Single(check =>
            check.Id == TrainingStandardConstraints.RelationsNamed).Satisfied);
        Assert.Equal(
            100,
            completeHandoff.Measurements.Single(measurement =>
                measurement.Name == TrainingStandardMeasurements.RelationPreservationPercent).Value);
        Assert.True(completeHandoff.CriticalConstraintChecks.Single(check =>
            check.Id == TrainingStandardConstraints.RelationsNamed).Satisfied);
    }

    [Fact]
    public void OpenStructureMappingRequiresExactProbeVerdictAndConcreteTestEvidence()
    {
        RuntimeScoringMaterial[] materials =
        [
            new(
                "ExpectedMapping",
                "expected-mapping-1",
                "relation-1: containment; expected source relation inside boundary; " +
                "expected target relation nested region; preserving evidence both encode membership."),
            new("ExpectedFinding", "model-audit-key", "PREDICTION=FAILED"),
        ];
        var common = new RuntimeEvent[]
        {
            Event(
                1,
                RuntimeEventKind.AnswerSubmitted,
                "relation-naming",
                RuntimeSessionPhaseKind.ActiveWork,
                new("answer_id", "relation-naming"),
                new("answer_reference", "inside boundary")),
            Event(2, RuntimeEventKind.PhaseStarted, "mapping-input", RuntimeSessionPhaseKind.ReconstructionInput),
            Event(
                3,
                RuntimeEventKind.AnswerSubmitted,
                "mapping-input",
                RuntimeSessionPhaseKind.ReconstructionInput,
                new("answer_id", "mapping-input"),
                new("answer_reference", "1=inside boundary -> nested region because both preserve membership")),
        };
        var vague = Result(
            BranchCode.CO,
            GlobalLevelId.L4,
            DrillId.CO2StructureMapping,
            [],
            common.Append(Event(
                4,
                RuntimeEventKind.AnswerSubmitted,
                "model-audit",
                RuntimeSessionPhaseKind.Audit,
                new("answer_id", "model-audit"),
                new("answer_reference", "prediction tested"))).ToArray());
        var exact = Result(
            BranchCode.CO,
            GlobalLevelId.L4,
            DrillId.CO2StructureMapping,
            [],
            common.Append(Event(
                4,
                RuntimeEventKind.AnswerSubmitted,
                "model-audit",
                RuntimeSessionPhaseKind.Audit,
                new("answer_id", "model-audit"),
                new("answer_reference", "PREDICTION=FAILED; TEST=missing evidence breaks acceptance relation"))).ToArray());

        var vagueHandoff = RuntimeStandardEvaluationHandoffMapper.Map(vague, materials);
        var exactHandoff = RuntimeStandardEvaluationHandoffMapper.Map(exact, materials);

        Assert.Equal(0, Measurement(vagueHandoff, TrainingStandardMeasurements.AuditPassed));
        Assert.False(vagueHandoff.CriticalConstraintChecks.Single(check =>
            check.Id == TrainingStandardConstraints.PredictionTested).Satisfied);
        Assert.Equal(1, Measurement(exactHandoff, TrainingStandardMeasurements.AuditPassed));
        Assert.True(exactHandoff.CriticalConstraintChecks.Single(check =>
            check.Id == TrainingStandardConstraints.PredictionTested).Satisfied);
    }

    [Fact]
    public void PairDiscriminationReportsFalsePositiveAndFalseNegativeInTheCorrectDirection()
    {
        var falsePositive = Result(
            BranchCode.DE,
            GlobalLevelId.L2,
            DrillId.DE1PairDiscrimination,
            [],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "active-work",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new RuntimeEventFact("answer_reference", "pair-1=same")),
            ]);
        var falseNegative = Result(
            BranchCode.DE,
            GlobalLevelId.L2,
            DrillId.DE1PairDiscrimination,
            [],
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "active-work",
                    RuntimeSessionPhaseKind.ActiveWork,
                    new RuntimeEventFact("answer_reference", "pair-2=different")),
            ]);
        RuntimeScoringMaterial[] materials =
        [
            new("MatchTruth", "pair-1-truth", "pair-1: mismatch"),
            new("MatchTruth", "pair-2-truth", "pair-2: match"),
        ];

        var positiveHandoff = RuntimeStandardEvaluationHandoffMapper.Map(falsePositive, materials);
        var negativeHandoff = RuntimeStandardEvaluationHandoffMapper.Map(falseNegative, materials);

        Assert.Equal(1, Measurement(positiveHandoff, TrainingStandardMeasurements.FalsePositiveCount));
        Assert.Equal(0, Measurement(positiveHandoff, TrainingStandardMeasurements.FalseNegativeCount));
        Assert.Equal(0, Measurement(negativeHandoff, TrainingStandardMeasurements.FalsePositiveCount));
        Assert.Equal(1, Measurement(negativeHandoff, TrainingStandardMeasurements.FalseNegativeCount));
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
    public void IntegratedCueWorkIsIncompleteUntilItsSeparateComponentReportExists()
    {
        var cueEvents = FocusShiftSourceEvents(invalidTap: false);
        var withoutReport = Result(
            BranchCode.FS,
            GlobalLevelId.L5,
            DrillId.FS2InvalidCueFilter,
            [
                CompletedPhase("cue-response", RuntimeSessionPhaseKind.CueResponse, 120),
                CompletedPhase("component-evidence", RuntimeSessionPhaseKind.ReconstructionInput, 30),
            ],
            cueEvents);
        var withReport = Result(
            BranchCode.FS,
            GlobalLevelId.L5,
            DrillId.FS2InvalidCueFilter,
            [
                CompletedPhase("cue-response", RuntimeSessionPhaseKind.CueResponse, 120),
                CompletedPhase("component-evidence", RuntimeSessionPhaseKind.ReconstructionInput, 30),
            ],
            cueEvents.Append(Event(
                100,
                RuntimeEventKind.AnswerSubmitted,
                "component-evidence",
                RuntimeSessionPhaseKind.ReconstructionInput,
                new RuntimeEventFact("answer_reference", "FH=CEDAR-7; FS=A"))).ToArray());

        Assert.False(RuntimeStandardEvaluationHandoffMapper.Map(
            withoutReport,
            ComponentMaterials()).OutputComplete);
        Assert.True(RuntimeStandardEvaluationHandoffMapper.Map(
            withReport,
            ComponentMaterials()).OutputComplete);
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

    [Fact]
    public void GlobalReviewRequiresThePlantedFindingAndExactLockedReport()
    {
        var phases = new[]
        {
            CompletedPhase("active-work", RuntimeSessionPhaseKind.ActiveWork, 60),
            CompletedPhase("audit", RuntimeSessionPhaseKind.Audit, 60),
            CompletedPhase("reconstruction", RuntimeSessionPhaseKind.ReconstructionInput, 60),
        };
        var materials = new RuntimeScoringMaterial[]
        {
            new("ExpectedFinding", "global-review-audit-key", "BRANCH=FS; CORRECTION=A"),
            new("ExpectedReconstruction", "global-review-reconstruction-1", "FH=NOT-CEDAR-7"),
            new("ExpectedReconstruction", "global-review-reconstruction-2", "FS=A"),
        };
        var arbitrary = Result(
            BranchCode.TI,
            GlobalLevelId.L5,
            DrillId.TI2GlobalReviewTask,
            phases,
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "audit",
                    RuntimeSessionPhaseKind.Audit,
                    new RuntimeEventFact("answer_reference", "looks fine")),
                Event(
                    2,
                    RuntimeEventKind.AnswerSubmitted,
                    "reconstruction",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new RuntimeEventFact("answer_reference", "something remembered")),
            ]);
        var exact = Result(
            BranchCode.TI,
            GlobalLevelId.L5,
            DrillId.TI2GlobalReviewTask,
            phases,
            [
                Event(
                    1,
                    RuntimeEventKind.AnswerSubmitted,
                    "audit",
                    RuntimeSessionPhaseKind.Audit,
                    new RuntimeEventFact("answer_reference", "BRANCH=FS; CORRECTION=A")),
                Event(
                    2,
                    RuntimeEventKind.AnswerSubmitted,
                    "reconstruction",
                    RuntimeSessionPhaseKind.ReconstructionInput,
                    new RuntimeEventFact("answer_reference", "FH=NOT-CEDAR-7; FS=A")),
            ]);

        var arbitraryHandoff = RuntimeStandardEvaluationHandoffMapper.Map(arbitrary, materials);
        var exactHandoff = RuntimeStandardEvaluationHandoffMapper.Map(exact, materials);

        Assert.Equal(0, Measurement(arbitraryHandoff, TrainingStandardMeasurements.AuditPassed));
        Assert.Equal(0, Measurement(arbitraryHandoff, TrainingStandardMeasurements.ReconstructionPassed));
        Assert.Equal(1, Measurement(exactHandoff, TrainingStandardMeasurements.AuditPassed));
        Assert.Equal(1, Measurement(exactHandoff, TrainingStandardMeasurements.ReconstructionPassed));
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

    private static IReadOnlyList<RuntimeEvent> FocusShiftSourceEvents(bool invalidTap)
    {
        var events = new List<RuntimeEvent>();
        long sequence = 1;
        for (var index = 0; index < 10; index++)
        {
            var cueId = $"valid-{index + 1}";
            events.Add(Event(
                sequence++,
                RuntimeEventKind.CueEmitted,
                "cue-response",
                RuntimeSessionPhaseKind.CueResponse,
                new RuntimeEventFact("cue_id", cueId),
                new RuntimeEventFact("response_expectation", "response_required")));
            events.Add(Event(
                sequence++,
                RuntimeEventKind.CueResponseSubmitted,
                "cue-response",
                RuntimeSessionPhaseKind.CueResponse,
                new RuntimeEventFact("cue_id", cueId),
                new RuntimeEventFact("response_outcome", index == 9 ? "incorrect" : "correct")));
        }

        events.Add(Event(
            sequence++,
            RuntimeEventKind.CueEmitted,
            "cue-response",
            RuntimeSessionPhaseKind.CueResponse,
            new RuntimeEventFact("cue_id", "invalid-1"),
            new RuntimeEventFact("response_expectation", "no_response_expected")));
        if (invalidTap)
        {
            events.Add(Event(
                sequence,
                RuntimeEventKind.CueResponseSubmitted,
                "cue-response",
                RuntimeSessionPhaseKind.CueResponse,
                new RuntimeEventFact("cue_id", "invalid-1"),
                new RuntimeEventFact("response_outcome", "incorrect")));
        }

        return events;
    }

    private static decimal Measurement(RuntimeStandardEvaluationHandoffInput handoff, string name)
    {
        return handoff.Measurements.Single(measurement => measurement.Name == name).Value;
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
        IReadOnlyList<RuntimeEvent> events,
        DrillId? sourceDrill = null)
    {
        var standard = ProgramCatalog.Standards.Single(item => item.Branch == branch && item.Level == level);
        var definition = new RuntimeSessionDefinition(
            SessionType.Practice,
            branch,
            level,
            drill,
            [new LoadVariable("test load", "fixed")],
            standard,
            [new CriticalConstraint("Keep the stated constraint.")],
            sourceDrill: sourceDrill);
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
