namespace MentalGymnastics.Core;

public static class TrainingStandardMeasurements
{
    public const string ActiveDurationSeconds = "active-duration-seconds";
    public const string SetCount = "set-count";
    public const string MarkedDriftCount = "marked-drift-count";
    public const string UnreturnedDriftCount = "unreturned-drift-count";
    public const string LateReturnCount = "late-return-count";
    public const string AverageReturnSeconds = "average-return-seconds";
    public const string TargetSubstitutionCount = "target-substitution-count";
    public const string DistractorResponseCount = "distractor-response-count";
    public const string CorrectResponsePercent = "correct-response-percent";
    public const string ValidCueAccuracyPercent = "valid-cue-accuracy-percent";
    public const string InvalidCueInhibitionPercent = "invalid-cue-inhibition-percent";
    public const string AnticipatoryResponseCount = "anticipatory-response-count";
    public const string UnrecoveredErrorCount = "unrecovered-error-count";
    public const string ExactItemCount = "exact-item-count";
    public const string ReconstructionAccuracyPercent = "reconstruction-accuracy-percent";
    public const string InventedItemCount = "invented-item-count";
    public const string TransformAccuracyPercent = "transform-accuracy-percent";
    public const string RuleExplanationCorrect = "rule-explanation-correct";
    public const string DelaySeconds = "delay-seconds";
    public const string HiddenNoteCount = "hidden-note-count";
    public const string CriticalOmissionCount = "critical-omission-count";
    public const string PrematureResponseCount = "premature-response-count";
    public const string ExceptionStatementPercent = "exception-statement-percent";
    public const string UnmarkedRuleDriftCount = "unmarked-rule-drift-count";
    public const string MaximumCorrectionDistance = "maximum-correction-distance";
    public const string AccuracyPercent = "accuracy-percent";
    public const string ComparisonCount = "comparison-count";
    public const string UnmarkedGuessCount = "unmarked-guess-count";
    public const string FalsePositiveCount = "false-positive-count";
    public const string FalseNegativeCount = "false-negative-count";
    public const string SeededErrorDetectionPercent = "seeded-error-detection-percent";
    public const string FalseCorrectionCount = "false-correction-count";
    public const string CriticalErrorDetectionPercent = "critical-error-detection-percent";
    public const string NoncriticalErrorDetectionPercent = "noncritical-error-detection-percent";
    public const string UnseenClassificationAccuracyPercent = "unseen-classification-accuracy-percent";
    public const string RuleChangedAfterFeedbackCount = "rule-changed-after-feedback-count";
    public const string RelationPreservationPercent = "relation-preservation-percent";
    public const string SurfaceOnlyMappingCount = "surface-only-mapping-count";
    public const string SourceStandardPassed = "source-standard-passed";
    public const string CriticalConstraintBreachCount = "critical-constraint-breach-count";
    public const string UnmarkedUncertaintyCount = "unmarked-uncertainty-count";
    public const string RecoverySeconds = "recovery-seconds";
    public const string StandardLoweringCount = "standard-lowering-count";
    public const string ComponentCount = "component-count";
    public const string ComponentPassPercent = "component-pass-percent";
    public const string ComponentEvidencePercent = "component-evidence-percent";
    public const string BottleneckComponentPassed = "bottleneck-component-passed";
    public const string AdvancedDemandActive = "advanced-demand-active";
    public const string DelayedArtifactComplete = "delayed-artifact-complete";
    public const string CompositePassed = "composite-passed";
    public const string AuditPassed = "audit-passed";
    public const string ReconstructionPassed = "reconstruction-passed";
    public const string PressureRuleIntact = "pressure-rule-intact";
}

public static class TrainingStandardConstraints
{
    public const string TargetStatedBeforeSet = "target-stated-before-set";
    public const string TargetUnchanged = "target-unchanged";
    public const string DriftMarked = "drift-marked";
    public const string SwitchOnlyOnCue = "switch-only-on-cue";
    public const string NoRereading = "no-rereading";
    public const string NoIntermediateNotes = "no-intermediate-notes";
    public const string RuleStatedBeforeSet = "rule-stated-before-set";
    public const string ExceptionsStatedBeforeSet = "exceptions-stated-before-set";
    public const string GuessesMarked = "guesses-marked";
    public const string OriginalOutputLocked = "original-output-locked";
    public const string RuleStatedBeforeUnseen = "rule-stated-before-unseen";
    public const string RelationsNamed = "relations-named";
    public const string SourceStandardVisible = "source-standard-visible";
    public const string StandardNotLowered = "standard-not-lowered";
    public const string FullRestartProhibited = "full-restart-prohibited";
    public const string BranchEvidenceSeparated = "branch-evidence-separated";
    public const string UncertaintyMarked = "uncertainty-marked";
    public const string PredictionTested = "prediction-tested";
    public const string CriticalAssumptionsNamed = "critical-assumptions-named";
}

public sealed record ExecutableTrainingStandardDefinition(
    BranchCode Branch,
    GlobalLevelId Level,
    DrillId Drill,
    EvaluatedStandard EvaluatedStandard);

public static class ExecutableStandardCatalog
{
    public static IReadOnlyList<ExecutableTrainingStandardDefinition> Standards { get; } = Build();

    public static ExecutableTrainingStandardDefinition Get(
        BranchCode branch,
        GlobalLevelId level)
    {
        return Standards.Single(standard => standard.Branch == branch && standard.Level == level);
    }

    public static bool Supports(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill)
    {
        return Standards.Any(standard =>
            standard.Branch == branch &&
            standard.Level == level &&
            standard.Drill == drill);
    }

    private static IReadOnlyList<ExecutableTrainingStandardDefinition> Build()
    {
        return
        [
            Define(BranchCode.FH, GlobalLevelId.L1, DrillId.FH1TargetHold,
                [AtLeast(TrainingStandardMeasurements.ActiveDurationSeconds, 180), AtMost(TrainingStandardMeasurements.MarkedDriftCount, 5), AtMost(TrainingStandardMeasurements.UnreturnedDriftCount, 0), AtMost(TrainingStandardMeasurements.LateReturnCount, 0), AtMost(TrainingStandardMeasurements.TargetSubstitutionCount, 0)],
                [Constraint(TrainingStandardConstraints.TargetStatedBeforeSet, "Target must be stated before the set starts.")]),
            Define(BranchCode.FH, GlobalLevelId.L2, DrillId.FH1TargetHold,
                [AtLeast(TrainingStandardMeasurements.ActiveDurationSeconds, 360), AtLeast(TrainingStandardMeasurements.SetCount, 1), AtMost(TrainingStandardMeasurements.MarkedDriftCount, 7), AtMost(TrainingStandardMeasurements.AverageReturnSeconds, 8), AtMost(TrainingStandardMeasurements.TargetSubstitutionCount, 0)],
                [Constraint(TrainingStandardConstraints.TargetStatedBeforeSet, "Target must be stated before the set starts."), Constraint(TrainingStandardConstraints.DriftMarked, "Every drift must be marked.")]),
            Define(BranchCode.FH, GlobalLevelId.L3, DrillId.FH2DistractorHold,
                [AtLeast(TrainingStandardMeasurements.ActiveDurationSeconds, 300), AtMost(TrainingStandardMeasurements.MarkedDriftCount, 5), AtMost(TrainingStandardMeasurements.DistractorResponseCount, 0), AtMost(TrainingStandardMeasurements.TargetSubstitutionCount, 0)],
                [Constraint(TrainingStandardConstraints.TargetStatedBeforeSet, "Target must be stated before the set starts."), Constraint(TrainingStandardConstraints.DriftMarked, "Every drift must be marked.")]),
            Define(BranchCode.FH, GlobalLevelId.L4, DrillId.FH2DistractorHold,
                [AtLeast(TrainingStandardMeasurements.SourceStandardPassed, 1), AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100), AtMost(TrainingStandardMeasurements.TargetSubstitutionCount, 0)],
                [Constraint(TrainingStandardConstraints.SourceStandardVisible, "The source hold standard must remain visible."), Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "The transfer branch must leave separate passing evidence.")]),
            Define(BranchCode.FH, GlobalLevelId.L5, DrillId.FH2DistractorHold,
                [AtLeast(TrainingStandardMeasurements.ActiveDurationSeconds, 720), AtMost(TrainingStandardMeasurements.MarkedDriftCount, 5), AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100), AtMost(TrainingStandardMeasurements.CriticalConstraintBreachCount, 0)],
                [Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Every integrated branch must leave separate evidence.")]),

            Define(BranchCode.FS, GlobalLevelId.L1, DrillId.FS1CueSwitch,
                [AtLeast(TrainingStandardMeasurements.ActiveDurationSeconds, 240), AtLeast(TrainingStandardMeasurements.CorrectResponsePercent, 90), AtMost(TrainingStandardMeasurements.AnticipatoryResponseCount, 3)],
                [Constraint(TrainingStandardConstraints.SwitchOnlyOnCue, "Switch only on a valid cue.")]),
            Define(BranchCode.FS, GlobalLevelId.L2, DrillId.FS1CueSwitch,
                [AtLeast(TrainingStandardMeasurements.ActiveDurationSeconds, 360), AtLeast(TrainingStandardMeasurements.CorrectResponsePercent, 92), AtMost(TrainingStandardMeasurements.UnrecoveredErrorCount, 0)],
                [Constraint(TrainingStandardConstraints.SwitchOnlyOnCue, "Switch only on a valid cue.")]),
            Define(BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter,
                [AtLeast(TrainingStandardMeasurements.ValidCueAccuracyPercent, 90), AtLeast(TrainingStandardMeasurements.InvalidCueInhibitionPercent, 100)],
                [Constraint(TrainingStandardConstraints.SwitchOnlyOnCue, "Invalid cues must not trigger a switch.")]),
            Define(BranchCode.FS, GlobalLevelId.L4, DrillId.FS2InvalidCueFilter,
                [AtLeast(TrainingStandardMeasurements.SourceStandardPassed, 1), AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100)],
                [Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Both branch tasks must leave separate passing evidence."), Constraint(TrainingStandardConstraints.SwitchOnlyOnCue, "Switch only on the stated cue rule.")]),
            Define(BranchCode.FS, GlobalLevelId.L5, DrillId.FS2InvalidCueFilter,
                [AtLeast(TrainingStandardMeasurements.ActiveDurationSeconds, 900), AtLeast(TrainingStandardMeasurements.ValidCueAccuracyPercent, 90), AtLeast(TrainingStandardMeasurements.InvalidCueInhibitionPercent, 90), AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100), AtMost(TrainingStandardMeasurements.CriticalConstraintBreachCount, 0)],
                [Constraint(TrainingStandardConstraints.SwitchOnlyOnCue, "Scheduled and unscheduled switches must preserve the cue rule."), Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Every integrated branch must leave separate evidence.")]),

            Define(BranchCode.WM, GlobalLevelId.L1, DrillId.WM1DelayedReconstruction,
                [AtLeast(TrainingStandardMeasurements.ExactItemCount, 4), AtMost(TrainingStandardMeasurements.InventedItemCount, 0), AtLeast(TrainingStandardMeasurements.DelaySeconds, 60)],
                [Constraint(TrainingStandardConstraints.NoRereading, "Source material cannot be reread after encoding closes.")]),
            Define(BranchCode.WM, GlobalLevelId.L2, DrillId.WM1DelayedReconstruction,
                [AtLeast(TrainingStandardMeasurements.ReconstructionAccuracyPercent, 85), AtLeast(TrainingStandardMeasurements.DelaySeconds, 90), AtMost(TrainingStandardMeasurements.InventedItemCount, 0)],
                [Constraint(TrainingStandardConstraints.NoRereading, "Source material cannot be reread after encoding closes.")]),
            Define(BranchCode.WM, GlobalLevelId.L3, DrillId.WM2MentalTransform,
                [AtLeast(TrainingStandardMeasurements.TransformAccuracyPercent, 80), AtLeast(TrainingStandardMeasurements.RuleExplanationCorrect, 1), AtLeast(TrainingStandardMeasurements.DelaySeconds, 120), AtMost(TrainingStandardMeasurements.HiddenNoteCount, 0)],
                [Constraint(TrainingStandardConstraints.NoIntermediateNotes, "Intermediate transform steps cannot be externalized."), Constraint(TrainingStandardConstraints.NoRereading, "Source material cannot be reread after encoding closes.")]),
            Define(BranchCode.WM, GlobalLevelId.L4, DrillId.WM2MentalTransform,
                [AtLeast(TrainingStandardMeasurements.DelaySeconds, 300), AtLeast(TrainingStandardMeasurements.ReconstructionAccuracyPercent, 80), AtMost(TrainingStandardMeasurements.InventedItemCount, 0)],
                [Constraint(TrainingStandardConstraints.NoRereading, "Source material cannot be reread after encoding closes.")], RubricOutcome.Pass),
            Define(BranchCode.WM, GlobalLevelId.L5, DrillId.WM2MentalTransform,
                [AtLeast(TrainingStandardMeasurements.ActiveDurationSeconds, 900), AtMost(TrainingStandardMeasurements.CriticalOmissionCount, 0), AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100)],
                [Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Integrated memory evidence must remain separately inspectable.")]),

            Define(BranchCode.IR, GlobalLevelId.L1, DrillId.IR1GoNoGoRule,
                [AtLeast(TrainingStandardMeasurements.AccuracyPercent, 90), AtMost(TrainingStandardMeasurements.PrematureResponseCount, 2)],
                [Constraint(TrainingStandardConstraints.RuleStatedBeforeSet, "The go/no-go rule must be stated before the set.")]),
            Define(BranchCode.IR, GlobalLevelId.L2, DrillId.IR2ExceptionRule,
                [AtLeast(TrainingStandardMeasurements.AccuracyPercent, 90), AtLeast(TrainingStandardMeasurements.ExceptionStatementPercent, 100)],
                [Constraint(TrainingStandardConstraints.RuleStatedBeforeSet, "The response rule must be stated before the set."), Constraint(TrainingStandardConstraints.ExceptionsStatedBeforeSet, "Every exception must be named before the set.")]),
            Define(BranchCode.IR, GlobalLevelId.L3, DrillId.IR2ExceptionRule,
                [AtLeast(TrainingStandardMeasurements.AccuracyPercent, 88), AtMost(TrainingStandardMeasurements.UnmarkedRuleDriftCount, 0), AtMost(TrainingStandardMeasurements.MaximumCorrectionDistance, 2)],
                [Constraint(TrainingStandardConstraints.RuleStatedBeforeSet, "The conflict rule must be stated before the set."), Constraint(TrainingStandardConstraints.ExceptionsStatedBeforeSet, "Every conflict-rule exception must be stated before the set.")]),
            Define(BranchCode.IR, GlobalLevelId.L4, DrillId.IR2ExceptionRule,
                [AtLeast(TrainingStandardMeasurements.SourceStandardPassed, 1), AtMost(TrainingStandardMeasurements.CriticalConstraintBreachCount, 0)],
                [Constraint(TrainingStandardConstraints.RuleStatedBeforeSet, "The open-task rule must be stated before work."), Constraint(TrainingStandardConstraints.ExceptionsStatedBeforeSet, "Every open-task exception must be stated before work."), Constraint(TrainingStandardConstraints.SourceStandardVisible, "The source rule standard must remain visible.")]),
            Define(BranchCode.IR, GlobalLevelId.L5, DrillId.IR2ExceptionRule,
                [AtLeast(TrainingStandardMeasurements.ActiveDurationSeconds, 900), AtLeast(TrainingStandardMeasurements.AccuracyPercent, 88), AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100), AtMost(TrainingStandardMeasurements.CriticalConstraintBreachCount, 0)],
                [Constraint(TrainingStandardConstraints.RuleStatedBeforeSet, "The integrated rule must be stated before work."), Constraint(TrainingStandardConstraints.ExceptionsStatedBeforeSet, "Every integrated-rule exception must be stated before work."), Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Every integrated branch must leave separate evidence.")]),

            Define(BranchCode.DE, GlobalLevelId.L1, DrillId.DE1PairDiscrimination,
                [AtLeast(TrainingStandardMeasurements.AccuracyPercent, 90), AtMost(TrainingStandardMeasurements.UnmarkedGuessCount, 2)],
                [Constraint(TrainingStandardConstraints.GuessesMarked, "Every guess must be marked.")]),
            Define(BranchCode.DE, GlobalLevelId.L2, DrillId.DE1PairDiscrimination,
                [AtLeast(TrainingStandardMeasurements.AccuracyPercent, 88), AtLeast(TrainingStandardMeasurements.ComparisonCount, 20), AtMost(TrainingStandardMeasurements.FalsePositiveCount, 2), AtMost(TrainingStandardMeasurements.FalseNegativeCount, 2)],
                [Constraint(TrainingStandardConstraints.GuessesMarked, "Every guess must be marked.")]),
            Define(BranchCode.DE, GlobalLevelId.L3, DrillId.DE2SeededAudit,
                [AtLeast(TrainingStandardMeasurements.SeededErrorDetectionPercent, 80), AtMost(TrainingStandardMeasurements.FalseCorrectionCount, 2), AtLeast(TrainingStandardMeasurements.DelaySeconds, 300)],
                [Constraint(TrainingStandardConstraints.OriginalOutputLocked, "The original output cannot be edited during audit.")]),
            Define(BranchCode.DE, GlobalLevelId.L4, DrillId.DE2SeededAudit,
                [AtLeast(TrainingStandardMeasurements.CriticalErrorDetectionPercent, 100)],
                [Constraint(TrainingStandardConstraints.OriginalOutputLocked, "The original output cannot be edited during audit."), Constraint(TrainingStandardConstraints.UncertaintyMarked, "Uncertainty must be marked rather than hidden.")], RubricOutcome.Pass),
            Define(BranchCode.DE, GlobalLevelId.L5, DrillId.DE2SeededAudit,
                [AtLeast(TrainingStandardMeasurements.CriticalErrorDetectionPercent, 100), AtLeast(TrainingStandardMeasurements.NoncriticalErrorDetectionPercent, 80)],
                [Constraint(TrainingStandardConstraints.OriginalOutputLocked, "The original output cannot be edited during audit.")], RubricOutcome.Pass),

            Define(BranchCode.CO, GlobalLevelId.L1, DrillId.CO1RuleExtraction,
                [AtLeast(TrainingStandardMeasurements.UnseenClassificationAccuracyPercent, 85)],
                [Constraint(TrainingStandardConstraints.RuleStatedBeforeUnseen, "A testable rule must be stated before unseen examples.")], RubricOutcome.Pass),
            Define(BranchCode.CO, GlobalLevelId.L2, DrillId.CO1RuleExtraction,
                [AtLeast(TrainingStandardMeasurements.UnseenClassificationAccuracyPercent, 82), AtMost(TrainingStandardMeasurements.RuleChangedAfterFeedbackCount, 0)],
                [Constraint(TrainingStandardConstraints.RuleStatedBeforeUnseen, "The rule must be stated before unseen examples and cannot be rewritten after feedback.")], RubricOutcome.Pass),
            Define(BranchCode.CO, GlobalLevelId.L3, DrillId.CO2StructureMapping,
                [AtLeast(TrainingStandardMeasurements.RelationPreservationPercent, 80), AtMost(TrainingStandardMeasurements.SurfaceOnlyMappingCount, 0)],
                [Constraint(TrainingStandardConstraints.RelationsNamed, "Required relations must be named before mapping.")], RubricOutcome.Pass),
            Define(BranchCode.CO, GlobalLevelId.L4, DrillId.CO2StructureMapping,
                [AtLeast(TrainingStandardMeasurements.RelationPreservationPercent, 80), AtLeast(TrainingStandardMeasurements.AuditPassed, 1)],
                [Constraint(TrainingStandardConstraints.RelationsNamed, "The open model must name its required relations."), Constraint(TrainingStandardConstraints.PredictionTested, "The model must make and test an unseen prediction.")], RubricOutcome.Pass),
            Define(BranchCode.CO, GlobalLevelId.L5, DrillId.CO2StructureMapping,
                [AtLeast(TrainingStandardMeasurements.RelationPreservationPercent, 80), AtLeast(TrainingStandardMeasurements.AuditPassed, 1)],
                [Constraint(TrainingStandardConstraints.CriticalAssumptionsNamed, "Critical assumptions must be named."), Constraint(TrainingStandardConstraints.PredictionTested, "The model must make and test an unseen prediction.")], RubricOutcome.Pass),

            Define(BranchCode.AI, GlobalLevelId.L1, DrillId.AI1PressureRepeat,
                [AtLeast(TrainingStandardMeasurements.SourceStandardPassed, 1), AtMost(TrainingStandardMeasurements.CriticalConstraintBreachCount, 0)],
                [Constraint(TrainingStandardConstraints.SourceStandardVisible, "The owned source standard must remain visible."), Constraint(TrainingStandardConstraints.StandardNotLowered, "Pressure cannot lower the source standard.")]),
            Define(BranchCode.AI, GlobalLevelId.L2, DrillId.AI1PressureRepeat,
                [AtLeast(TrainingStandardMeasurements.SourceStandardPassed, 1), AtMost(TrainingStandardMeasurements.UnmarkedUncertaintyCount, 0), AtMost(TrainingStandardMeasurements.CriticalConstraintBreachCount, 0)],
                [Constraint(TrainingStandardConstraints.SourceStandardVisible, "The owned source standard must remain visible."), Constraint(TrainingStandardConstraints.StandardNotLowered, "Pressure cannot lower the source standard."), Constraint(TrainingStandardConstraints.UncertaintyMarked, "Uncertainty must be marked rather than hidden.")]),
            Define(BranchCode.AI, GlobalLevelId.L3, DrillId.AI2DisruptionRecovery,
                [AtMost(TrainingStandardMeasurements.RecoverySeconds, 30), AtLeast(TrainingStandardMeasurements.SourceStandardPassed, 1)],
                [Constraint(TrainingStandardConstraints.FullRestartProhibited, "Recovery must resume from the last stable step without a full restart."), Constraint(TrainingStandardConstraints.StandardNotLowered, "Disruption cannot lower the source standard.")]),
            Define(BranchCode.AI, GlobalLevelId.L4, DrillId.AI2DisruptionRecovery,
                [AtLeast(TrainingStandardMeasurements.SourceStandardPassed, 1), AtMost(TrainingStandardMeasurements.StandardLoweringCount, 0)],
                [Constraint(TrainingStandardConstraints.SourceStandardVisible, "The open-task source standard must remain visible."), Constraint(TrainingStandardConstraints.StandardNotLowered, "Pressure cannot lower the source standard.")]),
            Define(BranchCode.AI, GlobalLevelId.L5, DrillId.AI2DisruptionRecovery,
                [AtLeast(TrainingStandardMeasurements.SourceStandardPassed, 1), AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100), AtMost(TrainingStandardMeasurements.CriticalConstraintBreachCount, 0)],
                [Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Every pressure-task component must leave separate evidence."), Constraint(TrainingStandardConstraints.StandardNotLowered, "Pressure cannot lower any component standard.")]),

            Define(BranchCode.TI, GlobalLevelId.L1, DrillId.TI1CompositeTask,
                [AtLeast(TrainingStandardMeasurements.ComponentCount, 2), AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100), AtLeast(TrainingStandardMeasurements.ComponentEvidencePercent, 100)],
                [Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Each component branch must leave separate evidence.")]),
            Define(BranchCode.TI, GlobalLevelId.L2, DrillId.TI1CompositeTask,
                [AtLeast(TrainingStandardMeasurements.ComponentCount, 3), AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100), AtLeast(TrainingStandardMeasurements.ComponentEvidencePercent, 100), AtLeast(TrainingStandardMeasurements.DelayedArtifactComplete, 1)],
                [Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Each component branch must leave separate evidence.")]),
            Define(BranchCode.TI, GlobalLevelId.L3, DrillId.TI1CompositeTask,
                [AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100), AtLeast(TrainingStandardMeasurements.ComponentEvidencePercent, 100), AtLeast(TrainingStandardMeasurements.AdvancedDemandActive, 1)],
                [Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Foundational and advanced evidence must remain separate.")]),
            Define(BranchCode.TI, GlobalLevelId.L4, DrillId.TI1CompositeTask,
                [AtLeast(TrainingStandardMeasurements.ComponentPassPercent, 100), AtLeast(TrainingStandardMeasurements.ComponentEvidencePercent, 100), AtLeast(TrainingStandardMeasurements.BottleneckComponentPassed, 1)],
                [Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Every far-transfer component must leave separate evidence.")]),
            Define(BranchCode.TI, GlobalLevelId.L5, DrillId.TI2GlobalReviewTask,
                [AtLeast(TrainingStandardMeasurements.CompositePassed, 1), AtLeast(TrainingStandardMeasurements.AuditPassed, 1), AtLeast(TrainingStandardMeasurements.ReconstructionPassed, 1), AtLeast(TrainingStandardMeasurements.PressureRuleIntact, 1), AtLeast(TrainingStandardMeasurements.ComponentEvidencePercent, 100)],
                [Constraint(TrainingStandardConstraints.BranchEvidenceSeparated, "Every global-task branch must leave separate evidence."), Constraint(TrainingStandardConstraints.StandardNotLowered, "Pressure cannot lower a component standard.")], RubricOutcome.Pass),
        ];
    }

    private static ExecutableTrainingStandardDefinition Define(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        IEnumerable<NumericThreshold> thresholds,
        IEnumerable<CriticalConstraintRequirement> constraints,
        RubricOutcome? rubric = null)
    {
        var catalogStandard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == branch && standard.Level == level);

        return new ExecutableTrainingStandardDefinition(
            branch,
            level,
            drill,
            new EvaluatedStandard(
                $"{branch} {level}: {catalogStandard.Standard}",
                thresholds,
                constraints,
                requiresCompleteOutput: true,
                requiredRubric: rubric));
    }

    private static NumericThreshold AtLeast(string name, decimal value) =>
        NumericThreshold.AtLeast(name, value);

    private static NumericThreshold AtMost(string name, decimal value) =>
        NumericThreshold.AtMost(name, value);

    private static CriticalConstraintRequirement Constraint(string id, string description) =>
        new(id, description);
}
