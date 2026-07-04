using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Runtime.Tests;

public sealed class ConceptOperationsRuntimeProtocolTests
{
    [Fact]
    public void RuleExtractionRecordsPreStatedRuleNegativeExamplesUnseenClassificationAndUnsupportedInference()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = ConceptOperationsRuntimeProtocol.Start(
            "co1-session",
            ConceptOperationsSession(DrillId.CO1RuleExtraction, GlobalLevelId.L1),
            clock);

        var unseenBeforeRule = protocol.StartUnseenExamples();

        Assert.False(unseenBeforeRule.IsAccepted);
        Assert.Equal(ConceptOperationsRuntimeProtocolInvalidReason.RuleRequiredBeforeUnseenExamples, unseenBeforeRule.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        var rule = protocol.StateRule(
            "A valid item has exactly two attributes and the second narrows the first.",
            [
                new ConceptOperationsRuntimeExample("ex-1", "red square with border", isPositive: true),
                new ConceptOperationsRuntimeExample("ex-2", "red square with border and dot", isPositive: false),
            ]);
        var active = protocol.StartUnseenExamples();

        Assert.True(rule.IsAccepted);
        Assert.True(active.IsAccepted);
        Assert.Contains(rule.Event!.Facts, fact => fact.Name == "rule_stated_before_unseen_examples" && fact.Value == "true");
        Assert.Contains(rule.Event.Facts, fact => fact.Name == "negative_example_count" && fact.Value == "1");
        Assert.Contains(rule.Event.Facts, fact => fact.Name == "rule_explanation" && fact.Value.Contains("exactly two attributes", StringComparison.Ordinal));

        var correct = protocol.ClassifyUnseenExample(
            "unseen-1",
            "blue circle with border",
            predictedPositive: true,
            expectedPositive: true);
        var incorrect = protocol.ClassifyUnseenExample(
            "unseen-2",
            "blue circle with border and dot",
            predictedPositive: true,
            expectedPositive: false);
        var unsupportedInference = protocol.RecordUnsupportedInference(
            "inference-1",
            "Assumed every blue item must be valid without an example.");
        var completed = protocol.CompleteRuleExtraction("set-1");

        Assert.True(correct.IsAccepted);
        Assert.True(incorrect.IsAccepted);
        Assert.True(unsupportedInference.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(correct.Event!.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(incorrect.Event!.Facts, fact => fact.Name == "response_outcome" && fact.Value == "incorrect");
        Assert.Contains(incorrect.Event.Facts, fact => fact.Name == "negative_example_handled" && fact.Value == "false");
        Assert.Contains(incorrect.Event.Facts, fact => fact.Name == "error_kind" && fact.Value == "negative_example_misclassified");
        Assert.Contains(unsupportedInference.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "unsupported_inference");
        Assert.Contains(unsupportedInference.Event.Facts, fact => fact.Name == "failed_item_list" && fact.Value.Contains("blue item", StringComparison.Ordinal));
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "unseen_classification_accuracy" && fact.Value == "1/2");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "unsupported_inference_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "rule_explanation" && fact.Value.Contains("exactly two attributes", StringComparison.Ordinal));

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);

        var failedEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "co1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FailedSet,
            protocol.EventLog.Events,
            scoringEvents));
        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "co1-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.FormalAttempt,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            failedEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.FailedItemList &&
                evidence.Description.Contains("unsupported inference", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.RuleExplanation &&
                evidence.Description.Contains("exactly two attributes", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("unseen_classification_accuracy=1/2", StringComparison.Ordinal));
        Assert.DoesNotContain(
            protocol.EventLog.Events.SelectMany(runtimeEvent => runtimeEvent.Facts),
            fact => fact.Name is "gate_outcome" or "pass_state");
    }

    [Fact]
    public void StructureMappingRecordsRelationNamesSurfaceMatchRejectionAndUnsupportedInference()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var protocol = ConceptOperationsRuntimeProtocol.Start(
            "co2-session",
            ConceptOperationsSession(DrillId.CO2StructureMapping, GlobalLevelId.L3),
            clock);

        var mappingBeforeRelations = protocol.StartMapping();

        Assert.False(mappingBeforeRelations.IsAccepted);
        Assert.Equal(ConceptOperationsRuntimeProtocolInvalidReason.RelationsRequiredBeforeMapping, mappingBeforeRelations.InvalidReason);
        Assert.Single(protocol.EventLog.Events);

        var relations = protocol.NameRelations(
            [
                new ConceptOperationsRuntimeRelation("rel-1", "source constraint narrows candidate set"),
                new ConceptOperationsRuntimeRelation("rel-2", "exception overrides default rule"),
            ]);
        var active = protocol.StartMapping();

        Assert.True(relations.IsAccepted);
        Assert.True(active.IsAccepted);
        Assert.Contains(relations.Event!.Facts, fact => fact.Name == "relations_named" && fact.Value == "true");
        Assert.Contains(relations.Event.Facts, fact => fact.Name == "relation_count" && fact.Value == "2");

        var preserved = protocol.RecordRelationMapping(
            "rel-1",
            "target filter narrows eligible options",
            preservesRelation: true,
            surfaceOnly: false);
        var surfaceMatch = protocol.RecordRelationMapping(
            "rel-2",
            "both examples mention a red label",
            preservesRelation: false,
            surfaceOnly: true);
        var unsupportedInference = protocol.RecordUnsupportedInference(
            "mapping-inference-1",
            "Concluded the target must include every source exception without a mapped relation.");
        var completed = protocol.CompleteStructureMapping("mapping-1");

        Assert.True(preserved.IsAccepted);
        Assert.True(surfaceMatch.IsAccepted);
        Assert.True(unsupportedInference.IsAccepted);
        Assert.True(completed.IsAccepted);

        Assert.Contains(preserved.Event!.Facts, fact => fact.Name == "mapping_result" && fact.Value == "relation_preserved");
        Assert.Contains(preserved.Event.Facts, fact => fact.Name == "response_outcome" && fact.Value == "correct");
        Assert.Contains(surfaceMatch.Event!.Facts, fact => fact.Name == "mapping_result" && fact.Value == "surface_match_rejected");
        Assert.Contains(surfaceMatch.Event.Facts, fact => fact.Name == "error_kind" && fact.Value == "surface_match");
        Assert.Contains(surfaceMatch.Event.Facts, fact => fact.Name == "failed_constraint" && fact.Value == "relations_not_surface_terms");
        Assert.Contains(unsupportedInference.Event!.Facts, fact => fact.Name == "error_kind" && fact.Value == "unsupported_inference");
        Assert.Contains(completed.Event!.Facts, fact => fact.Name == "branch_mapping" && fact.Value.Contains("relation_accuracy=1/2", StringComparison.Ordinal));
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "surface_match_rejection_count" && fact.Value == "1");
        Assert.Contains(completed.Event.Facts, fact => fact.Name == "unsupported_inference_count" && fact.Value == "1");

        var scoringEvents = protocol.EventLog.Events
            .Select(RuntimeScoringEventFactory.FromRuntimeEvent)
            .OfType<RuntimeScoringEvent>()
            .ToArray();
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.CorrectResponse);
        Assert.Contains(scoringEvents, scoringEvent => scoringEvent.Kind == RuntimeScoringEventKind.IncorrectResponse);

        var formalEvidence = RuntimeEvidenceCapture.Capture(new RuntimeEvidenceCaptureRequest(
            "co2-session",
            protocol.SessionDefinition,
            TrainingDate.From(2026, 7, 4),
            RuntimeEvidenceCaptureKind.Transfer,
            protocol.EventLog.Events,
            scoringEvents));

        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.BranchMapping &&
                evidence.Description.Contains("relation_accuracy=1/2", StringComparison.Ordinal));
        Assert.Contains(
            formalEvidence.Artifact.ObservableEvidence,
            evidence => evidence.Kind == ObservableEvidenceKind.Score &&
                evidence.Description.Contains("surface_match_rejection_count=1", StringComparison.Ordinal));
    }

    [Fact]
    public void ConceptOperationsProtocolRejectsWrongSessionsAndMappingForRuleExtractionWithoutMutatingEvents()
    {
        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);

        Assert.Throws<ArgumentException>(() => ConceptOperationsRuntimeProtocol.Start(
            "wrong-branch",
            FocusHoldSession(),
            clock));

        var ruleExtraction = ConceptOperationsRuntimeProtocol.Start(
            "co1-no-mapping",
            ConceptOperationsSession(DrillId.CO1RuleExtraction, GlobalLevelId.L1),
            clock);
        Assert.True(ruleExtraction.StateRule(
            "Rule stated before unseen examples.",
            [new ConceptOperationsRuntimeExample("ex-1", "positive item", isPositive: true)]).IsAccepted);
        Assert.True(ruleExtraction.StartUnseenExamples().IsAccepted);
        var eventCountBeforeMapping = ruleExtraction.EventLog.Events.Count;

        var relations = ruleExtraction.NameRelations(
            [new ConceptOperationsRuntimeRelation("rel-1", "not part of CO-1")]);

        Assert.False(relations.IsAccepted);
        Assert.Equal(ConceptOperationsRuntimeProtocolInvalidReason.StructureMappingNotSupportedByDrill, relations.InvalidReason);
        Assert.Equal(eventCountBeforeMapping, ruleExtraction.EventLog.Events.Count);
    }

    private static RuntimeSessionDefinition ConceptOperationsSession(DrillId drill, GlobalLevelId level)
    {
        var standard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == BranchCode.CO &&
            standard.Level == level);
        CriticalConstraint[] constraints = drill == DrillId.CO2StructureMapping
            ?
            [
                new CriticalConstraint("Relations must be named."),
                new CriticalConstraint("Surface matches do not count."),
                new CriticalConstraint("Unsupported inferences are recorded as evidence."),
            ]
            :
            [
                new CriticalConstraint("Rule stated before unseen examples."),
                new CriticalConstraint("Negative examples must be handled without rewriting the rule."),
                new CriticalConstraint("Unsupported inferences are recorded as evidence."),
            ];
        LoadVariable[] loadVariables = drill == DrillId.CO2StructureMapping
            ?
            [
                new LoadVariable("relation count", "2"),
                new LoadVariable("domain distance", "near transfer"),
            ]
            :
            [
                new LoadVariable("ambiguity", "clear examples"),
                new LoadVariable("example count", "2 training examples"),
                new LoadVariable("negative examples", "1"),
            ];

        return new RuntimeSessionDefinition(
            SessionType.Practice,
            BranchCode.CO,
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
