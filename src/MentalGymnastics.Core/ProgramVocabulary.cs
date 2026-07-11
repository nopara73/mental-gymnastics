namespace MentalGymnastics.Core;

public enum BranchType
{
    Foundational,
    Advanced,
}

public enum BranchCode
{
    FH,
    FS,
    WM,
    IR,
    DE,
    CO,
    AI,
    TI,
}

public enum GlobalLevelId
{
    L1,
    L2,
    L3,
    L4,
    L5,
}

public enum CapacityId
{
    SelectiveHold,
    ReturnAfterDrift,
    DeliberateSwitching,
    EncodingFidelity,
    ManipulationInMind,
    ResponseInhibition,
    RuleFidelity,
    FineDiscrimination,
    ErrorAudit,
    RuleExtraction,
    AbstractionMapping,
    PressureStableExecution,
    RecoveryAfterDisruption,
    IntegratedTaskControl,
}

public enum DrillId
{
    FH1TargetHold,
    FH2DistractorHold,
    FS1CueSwitch,
    FS2InvalidCueFilter,
    WM1DelayedReconstruction,
    WM2MentalTransform,
    IR1GoNoGoRule,
    IR2ExceptionRule,
    DE1PairDiscrimination,
    DE2SeededAudit,
    CO1RuleExtraction,
    CO2StructureMapping,
    AI1PressureRepeat,
    AI2DisruptionRecovery,
    TI1CompositeTask,
    TI2GlobalReviewTask,
}

public enum SessionType
{
    Practice,
    Load,
    Test,
    Stabilization,
    Regression,
    Transfer,
    Recovery,
}

public enum GateOutcome
{
    Fail,
    PassOnce,
    Stabilize,
    Own,
    Maintain,
    Regress,
    Review,
}

public enum FailureType
{
    TechnicalFailure,
    EffortFailure,
    Overload,
    BadProgramming,
}

public enum BranchLevelState
{
    Unopened,
    Training,
    TestReady,
    PassedOnce,
    Stabilizing,
    Owned,
    Maintenance,
    Decayed,
}

public enum BranchLevelTransition
{
    OpenForTraining,
    MarkTestReady,
    ExpireTestReadiness,
    FailFormalTest,
    PassFormalTestOnce,
    EnterStabilization,
    CompleteStabilization,
    FailStabilization,
    AssignMaintenance,
    PassMaintenance,
    MarkDecayed,
    BeginRestorationTraining,
    RestoreToMaintenance,
    OpenNextLevelTraining,
    OpenNextDemandFromReview,
    ContinueTrainingWork,
    ConfirmGlobalReview,
    ContinueMaintenance,
}

public enum EvidenceArtifactCategory
{
    Practice,
    Load,
    Test,
    Stabilization,
    Transfer,
    Maintenance,
    GlobalReview,
}

public enum ObservableEvidenceKind
{
    Score,
    Time,
    ErrorCount,
    Reconstruction,
    Comparison,
    RuleExplanation,
    FailedItemList,
    RepeatabilityRecord,
    OutputSample,
    BranchMapping,
    CriticalConstraintRecord,
    LoadVariableRecord,
    BottleneckNote,
    AuditResult,
    DelayedReconstruction,
    MaintenanceCheck,
    GlobalReviewSummary,
}

public enum TestResultEvidenceKind
{
    Score,
    Rubric,
    PassFail,
}

public enum FormalTestPassState
{
    Fail,
    PassOnce,
    StabilizationPass,
    Owned,
    MaintenancePass,
}

public enum NumericThresholdDirection
{
    AtLeast,
    AtMost,
}

public enum RubricOutcome
{
    Fail,
    Pass,
    Excellent,
}

public enum QualitativeRubricKind
{
    RuleQuality,
    MappingQuality,
    OpenTaskAuditQuality,
}

public enum TestReadinessFailureKind
{
    PrerequisiteNotOwned,
    RecentCleanPracticeMissing,
    PrerequisiteMaintenanceOverdue,
    StandardNotStated,
    HonestyConstraintNotNamed,
}

public enum StabilizationOwnershipFailureKind
{
    InsufficientCleanPasses,
    StabilizationPassesMissing,
    StabilizationPassesNotOnDifferentDays,
    StabilizationWindowMissed,
    SevenDaySpanMissing,
    AdjacentWorkOrDistractorPassMissing,
    StandardChanged,
    MainFailureModeMissing,
}

public enum MaintenanceCurrencyState
{
    Current,
    Due,
    Warning,
    Failed,
}

public enum MaintenanceCheckKind
{
    StandardOrTransfer,
    GlobalComposite,
}

public enum RestorationCheckKind
{
    LastOwnedStandard,
    LowerLoadTransferCheck,
}

public enum DecayRestorationFailureKind
{
    DecayRequiresMaintenanceState,
    MaintenanceCurrencyDoesNotMatchBranchLevel,
    MaintenanceFailureThresholdNotMet,
    RestorationRequiresDecayedState,
    RestorationEvidenceDoesNotMatchBranchLevel,
    LastOwnedStandardPassMissing,
    LowerLoadTransferCheckMissing,
}

public enum DependencyCapReason
{
    DecayedPrerequisite,
    OverduePrerequisiteMaintenance,
}

public enum GlobalBalanceIssueKind
{
    FoundationalOwnedLevelSpreadTooWide,
    AdvancedPrerequisiteMaintenanceOverdue,
    AdvancedPrerequisiteDecayed,
    TransferIntegrationComponentFailedLastGlobalReview,
    ConceptOperationsPrerequisiteDecayed,
    AffectiveInterferencePrerequisiteDecayed,
    AdvancedClassificationRequiresPassedGlobalReview,
}

public enum LoadVariableKind
{
    Duration,
    DistractorSalience,
    RecoveryWindow,
    TargetSubtlety,
    SwitchCount,
    CueDensity,
    RuleContrast,
    ReturnPrecision,
    ItemCount,
    DetailDensity,
    OperationSteps,
    Delay,
    Interference,
    CueConflict,
    ResponseSpeed,
    ExceptionCount,
    Pressure,
    Similarity,
    Quantity,
    ErrorSubtlety,
    AuditDelay,
    RuleAmbiguity,
    ExampleCount,
    ExceptionHandling,
    TransferDistance,
    TimePressure,
    EvaluativePressure,
    Frustration,
    Uncertainty,
    BranchCount,
    TaskLength,
    DomainDistance,
}

public enum ForbiddenLoadIncreaseKind
{
    AddComplexReasoningBeforeHoldStable,
    RandomSwitchingWithoutDefinedCueRule,
    AddAbstractionWhenEncodingInaccurate,
    IncreaseSpeedAfterRuleBreakingErrors,
    IncreaseQuantityWhenFalsePositivesHigh,
    IncreaseAmbiguityBeforeTestableRule,
    EmotionalPressurePreventsCleanEvidenceCollection,
    NovelTaskFormatWithoutBranchSpecificScoring,
}

public enum LoadChangeMode
{
    Acquisition,
    AdvancedIntegration,
}

public enum LoadChangeFailureKind
{
    LoadVariableNotPrimaryForBranch,
    TooManyLoadVariablesForAcquisition,
    AdvancedIntegrationVariablesNotStable,
    ForbiddenLoadIncrease,
}

public enum RegressionMoveKind
{
    ShorterDuration,
    FewerItems,
    LowerCueDensity,
    LongerRest,
    ClearerExamples,
    LessSubtleErrors,
    ShorterDelay,
    MilderPressure,
    FewerBranchesInCompositeTask,
}

public enum ForbiddenRegressionMoveKind
{
    RemoveDriftMarkingFromFocusHold,
    AllowTargetChangesDuringFocusHold,
    AllowUncuedSwitchesDuringFocusShift,
    AllowRereadingAfterEncodeWindowInWorkingMemory,
    AllowPrematureResponsesInInhibition,
    AllowUnmarkedGuessesInDiscrimination,
    AllowVagueRulesInConceptOperations,
    LowerOriginalBranchStandardDuringAffectiveInterference,
    RemoveBranchSpecificEvidenceDuringTransferIntegration,
}

public enum RegressionRuleFailureKind
{
    RegressionMoveNotAllowed,
    ForbiddenRegressionMove,
    CoreDemandNotPreserved,
    HonestyConstraintRemoved,
}

public enum FailureEvidenceSignal
{
    ConfusionAboutRule,
    MissingArtifact,
    WrongProcedure,
    BrokenHonestyConstraint,
    MissingDriftMarks,
    UnmarkedGuesses,
    ErrorsRiseAfterLoadIncrease,
    ConstraintPreserved,
    MultipleBranchesDegrade,
    RepeatedOverload,
    PrerequisiteNotStable,
    SameBranchGateFailedThreeTimesAcrossTenDays,
}

public enum ProgrammingResponseAction
{
    StopTest,
    ReturnToPractice,
    SimplifyInstruction,
    RetestLater,
    PracticeDrillFormAtLowIntensityBeforeRetest,
    FailAttempt,
    RepeatSameOrLowerLoadWithStricterEvidence,
    RequireCleanPracticeArtifactBeforeRepeat,
    ReduceOneLoadVariable,
    TrainRegression,
    RetestAfterCleanPractice,
    StabilizeRegression,
    InspectPrerequisiteBranch,
    ReduceWeeklyLoad,
    Deload,
    RestorePrerequisites,
    ReviseWeeklyEmphasis,
    SuspendAdvancementTestingForOneWeek,
    RunMaintenanceChecks,
    NoNewTests,
    StopAdvancementTestsInStuckBranch,
    IdentifyStuckPoint,
    TrainNearestPrerequisiteForOneWeekAtModerateIntensity,
    UseRegressionPreservingFailedConstraint,
    ReduceTotalWeeklyLoad,
    RetestFailedConstraintBeforeWholeGate,
    ReturnToSourceBranch,
    TestNearerTransferDistance,
}

public enum BottleneckKind
{
    FocusHoldReturnAfterDrift,
    WorkingMemoryEncodingFidelity,
    InhibitionRuleFidelity,
    DiscriminationAuditAccuracy,
    FocusShiftRecovery,
    AffectiveInterferencePressureStability,
}

public enum StuckStateConditionKind
{
    SameBranchGateFailedThreeTimesAcrossTenDays,
    SameCriticalConstraintFailedInConsecutiveRegressions,
    PrerequisiteRepeatedlyDecayedWhileDependentTraining,
    SameBottleneckInTwoGlobalReviewsWithoutImprovement,
    IsolatedDrillsPassButRelatedTransferTestsFail,
}

public enum PractitionerCategory
{
    Beginner,
    Intermediate,
    Advanced,
}

public enum PractitionerCategoryBlockerKind
{
    FoundationalBranchBelowL2Owned,
    AdvancedBranchNotOpened,
    FoundationalBranchBelowL3Owned,
    MaintenanceNotCurrent,
    FoundationalBranchBelowL4Owned,
    ConceptOperationsBelowL3Owned,
    AffectiveInterferenceBelowL3Owned,
    TransferIntegrationBelowL3Owned,
    LastGlobalReviewNotPassed,
}

public enum TrainingIntensityKind
{
    Low,
    Moderate,
    High,
}

public enum GlobalReviewDecisionKind
{
    ContinueCurrentProgression,
    EmphasizeBottleneckBranch,
    RestoreDecayedBranch,
    OpenAdvancedBranch,
    PauseTestsForDeload,
    AttemptTransferIntegrationTransfer,
}

public enum GlobalReviewFailureKind
{
    WholePractitionerInputMissing,
    PrerequisiteBranchDecayed,
    MaintenanceCheckOverdue,
    BottleneckProgrammedResponseMissing,
    CurrentTransferOrStabilizationArtifactMissing,
    ParticipationOnlyAdvancement,
}

public enum WeeklySessionKind
{
    Practice,
    Load,
    RecoveryOrLightMaintenance,
    TestOrStabilization,
    OffOrRecovery,
    Maintenance,
    TransferOrStabilization,
    Recovery,
    Off,
    Transfer,
    Stabilization,
    RecoveryOrRetest,
}

public enum WeeklyProgrammingConstraintKind
{
    BeginnerFixedTemplateRequired,
    MaintenanceNotCurrent,
    RecoveryRequired,
    AdvancementTestingSuspended,
}

public enum RecoveryTriggerKind
{
    TwoConsecutiveOverloadSetFailures,
    ErrorCountRisesWithUnchangedLoad,
    HonestyConstraintBroken,
    AdjacentBranchesShowBroadDecay,
    SameBranchHighIntensityTestWithin24Hours,
}

public enum DeloadTriggerKind
{
    TwoOrMoreBranchesShowOverloadOrDecayInSameWeek,
    ActiveDeloadWeekIncomplete,
}

public enum PromptContentKind
{
    EquivalentPrompt,
    CueSequence,
    DelayedReconstructionTask,
    DiscriminationItemSet,
    RuleExampleSet,
}

public enum PromptFreshnessPolicy
{
    IdenticalReuseAllowed,
    FreshEquivalentRequired,
}

public enum PromptContentSelectionFailureKind
{
    NoEquivalentContentAvailable,
    FreshEquivalentContentUnavailable,
}

public enum AntiSelfDeceptionEvidenceKind
{
    Participation,
    Effort,
    Insight,
    Novelty,
    StandardPerformance,
    StabilizationRetest,
    TransferPerformance,
    MaintenanceCheck,
    ObservableArtifact,
}

public enum AntiSelfDeceptionViolationKind
{
    AdvancementByParticipation,
    AdvancementByEffort,
    AdvancementByInsight,
    NoveltyPresentedAsAdvancement,
    SkippedPrerequisite,
    MissingEvidence,
    ChangedStandard,
    RemovedHonestyConstraint,
    RetestAvoided,
}

public enum StandardFailureKind
{
    CriticalConstraintBroken,
    OutputIncomplete,
    NumericalThresholdMissed,
    RubricDidNotPass,
}

public enum TransferEligibilityFailureKind
{
    TransferTaskDoesNotMatchSourceBranch,
    TrainedCapacityNotSpecified,
    TrainedCapacityNotInSourceBranch,
    SourceDemandNotPreserved,
    ContextNotChanged,
    SourceStandardEvidenceMissing,
    SourceStandardDoesNotMatchCatalog,
    SourceStandardNotVisible,
    RetestRequirementMissing,
}
