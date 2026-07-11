using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App;

public enum TrainingPresentationPriorityKind
{
    PrescribedWork,
    UrgentBlocker,
    MaintenanceDue,
    DecayRestoration,
    Recovery,
    Deload,
    LiveSession,
    Result,
    NoAvailableWork,
}

public enum TrainingPresentationPrimaryActionKind
{
    None,
    StartPrescribedWork,
    StartMaintenance,
    StartRecovery,
    StartDeload,
    RestoreDecayedWork,
    ResolveBlocker,
    StartLiveSession,
    ContinueLiveSession,
    SaveTerminalSession,
    ReturnToNextPrescribedAction,
}

public enum TrainingPresentationWorkSource
{
    DailyPrescription,
    WeeklyPlan,
    SelectedWork,
    Maintenance,
    Recovery,
    Deload,
    LiveSession,
    Result,
}

public enum TrainingPresentationBlockerSeverity
{
    Advisory,
    Blocking,
    Urgent,
}

public enum TrainingPresentationBlockerKind
{
    BranchUnavailable,
    MaintenanceOrDecay,
    Readiness,
    Dependency,
    GlobalBalance,
    Transfer,
    WeeklyConstraint,
    PractitionerCategory,
    PreparationRejected,
    Unknown,
}

public enum TrainingPresentationRevealKind
{
    BranchLadder,
    WeeklyPlan,
    BlockerDetails,
    MaintenanceDetails,
    EvidenceArtifacts,
    RecentSessions,
    GeneratedContentDetails,
    RuntimeProtocolDetails,
    CoreEvaluationDetails,
    LocalPersistenceDetails,
    GlobalReviewDetails,
}

public enum TrainingMaintenanceDecayPriorityKind
{
    MaintenanceWarning,
    MaintenanceDue,
    MaintenanceFailed,
    DecayRestoration,
}

public enum TrainingResultPresentationOutcomeKind
{
    NotTerminal,
    Cancelled,
    Abandoned,
    TimedOut,
    Failed,
    NoAdvancement,
    CleanPractice,
    TestReady,
    PassedOnce,
    Stabilizing,
    Owned,
    Maintenance,
    MaintenanceWarning,
    MaintenanceFailed,
    Decayed,
    Recovery,
    Blocked,
    TransferEligible,
}

public sealed record TrainingPresentationReveal(
    TrainingPresentationRevealKind Kind,
    int Count,
    string Label);

public sealed record TrainingBranchLevelPresentation(
    BranchCode Branch,
    GlobalLevelId? Level,
    string BranchLabel,
    string? LevelLabel,
    BranchLevelState? State);

public sealed record TrainingExercisePresentation(
    string ExerciseName,
    string BranchLevelLabel,
    string FirstScreenInstruction,
    string Purpose,
    string PracticeGain,
    string WhereItGoes,
    string BeforeStartInstruction,
    string SuccessCriteria,
    string FailureCriteria,
    string HonestyInstruction,
    string EvidenceRecorded,
    string? PrimaryMaterial,
    IReadOnlyList<string> SetupItems);

public sealed record TrainingPresentationWorkSummary(
    TrainingPresentationWorkSource Source,
    IReadOnlyList<TrainingBranchLevelPresentation> BranchLevels,
    DrillId? Drill,
    string? DrillCode,
    string? DrillLabel,
    AppTrainingSessionType? SessionType,
    WeeklySessionKind? WeeklySession,
    bool IsAdvancementWork,
    bool AdvancementWorkAllowed,
    string? Demand,
    string? Standard,
    string? HonestyConstraint,
    IReadOnlyList<LoadVariable> LoadVariables,
    TrainingExercisePresentation Exercise,
    bool HasExecutableStandard);

public sealed record TrainingPresentationBlockerSummary(
    TrainingPresentationBlockerKind Kind,
    BranchCode? Branch,
    GlobalLevelId? Level,
    TrainingPresentationBlockerSeverity Severity,
    string Detail);

public sealed record TrainingMaintenanceDecayPriority(
    TrainingMaintenanceDecayPriorityKind Kind,
    BranchCode Branch,
    GlobalLevelId Level,
    string BranchLabel,
    string LevelLabel,
    BranchLevelState BranchState,
    MaintenanceCurrencyState? CurrencyState,
    int? DaysSinceLastPassingCheck,
    int ConsecutiveFailures,
    bool BlocksAdvancement,
    string Detail);

public sealed record TrainingEvidencePresentationSummary(
    int RecentSessionCount,
    int RecentCleanSessionCount,
    int EvidenceArtifactCount,
    int FormalAttemptCount,
    int StabilizationPassCount,
    int MaintenanceCheckCount,
    EvidenceArtifactCategory? LatestEvidenceCategory,
    BranchCode? LatestBranch,
    GlobalLevelId? LatestLevel,
    DrillId? LatestDrill,
    IReadOnlyList<ObservableEvidenceKind> LatestObservableEvidenceKinds,
    bool HasObservableEvidence,
    bool HasFailureEvidence);

public sealed record CurrentTrainingPresentationReadModel(
    TrainingPresentationPriorityKind Priority,
    TrainingPresentationPrimaryActionKind PrimaryAction,
    bool PrimaryActionEnabled,
    TrainingPresentationWorkSummary? PrimaryPrescribedWork,
    TrainingPresentationBlockerSummary? UrgentBlocker,
    TrainingMaintenanceDecayPriority? MaintenanceDecayPriority,
    TrainingEvidencePresentationSummary EvidenceSummary,
    IReadOnlyList<TrainingPresentationReveal> RevealOnDemand,
    bool GrantsAdvancementInApp,
    DailyTrainingWorkflowStatus? DailyStatus = null,
    int DailyCompletedBlockCount = 0,
    int DailyTotalBlockCount = 0);

public sealed record SessionPreflightPresentationReadModel(
    PreUiTrainingWorkflowPreparationStatus Status,
    bool CanStart,
    TrainingPresentationWorkSummary? Work,
    string? Demand,
    string? Standard,
    string? HonestyConstraint,
    int LoadVariableCount,
    int CriticalConstraintCount,
    int ExpectedEvidenceFactCount,
    int PhaseCount,
    int CueCount,
    int MaterialCount,
    IReadOnlyList<TrainingPresentationBlockerSummary> Blockers,
    IReadOnlyList<TrainingPresentationReveal> RevealOnDemand,
    bool GrantsAdvancementInApp);

public sealed record LiveCuePresentationSummary(
    RuntimeCueKind Kind,
    string Cue,
    RuntimeCueResponseExpectation ResponseExpectation,
    TimeSpan ResponseWindow,
    bool RequiresResponse,
    bool HasHiddenExpectedResponse);

public sealed record LiveCommandPresentationSummary(
    RuntimeInputCommandKind Command,
    string Label);

public sealed record LiveEvidencePresentationSummary(
    int RuntimeEventCount,
    int EvidenceFactCount,
    int ExpectedEvidenceFactCount,
    int DriftCount,
    int GuessCount,
    int ErrorCount,
    int CueCount,
    int CueResponseCount,
    int AnswerCount,
    int CorrectionCount,
    bool HasFailureMarks,
    bool ExpectedEvidenceComplete,
    int ReturnCount = 0,
    int LateReturnCount = 0,
    int TargetChangeCount = 0,
    int OpenDriftCount = 0,
    TimeSpan? MaximumReturnTime = null);

public sealed record LiveSessionPresentationReadModel(
    TrainingPresentationWorkSummary Work,
    RuntimeSessionLifecycleStatus LifecycleStatus,
    RuntimePhaseSchedulerStatus SchedulerStatus,
    RuntimeSessionPhaseKind? CurrentPhaseKind,
    RuntimeSessionPhaseCompletionRule? CurrentPhaseCompletionRule,
    PreUiLiveSessionTimerState Timer,
    LiveCuePresentationSummary? ActiveCue,
    IReadOnlyList<PreUiLiveSessionMaterialState> CurrentMaterials,
    string CurrentInstruction,
    LiveCommandPresentationSummary? PrimaryCommand,
    IReadOnlyList<LiveCommandPresentationSummary> AvailableCommands,
    LiveEvidencePresentationSummary Evidence,
    bool IsTerminal,
    IReadOnlyList<TrainingPresentationReveal> RevealOnDemand,
    bool GrantsAdvancementInApp,
    DrillId? SourceDrill = null);

public sealed record TrainingStateTransitionPresentation(
    BranchCode Branch,
    GlobalLevelId Level,
    BranchLevelState FromState,
    BranchLevelState ToState,
    BranchLevelTransition Transition,
    bool Changed);

public sealed record ResultPresentationReadModel(
    TrainingResultPresentationOutcomeKind Outcome,
    TrainingPresentationPrimaryActionKind PrimaryAction,
    bool PrimaryActionEnabled,
    RuntimeSessionCompletionStatus? RuntimeCompletionStatus,
    TrainingPresentationWorkSummary Work,
    bool IsProcessed,
    bool ProducesSuccessfulEvidence,
    bool CleanPerformance,
    TrainingStateTransitionPresentation? StateTransition,
    GateOutcome? GateOutcome,
    MaintenanceCurrencyState? MaintenanceCurrencyState,
    FailureType? FailureType,
    IReadOnlyList<string> BlockingFailureDetails,
    TrainingEvidencePresentationSummary EvidenceSummary,
    LiveEvidencePresentationSummary SessionEvidence,
    IReadOnlyList<TrainingPresentationReveal> RevealOnDemand,
    bool GrantsAdvancementInApp);

public static class TrainingPresentationMapper
{
    public static CurrentTrainingPresentationReadModel FromCurrentState(
        CurrentTrainingStateReadModel state,
        DailyTrainingWorkflowReadModel? dailyTraining = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (dailyTraining is not null)
        {
            var dailyPrimaryWork = dailyTraining.CurrentBlock is null
                ? null
                : WorkFor(dailyTraining.CurrentBlock, state);
            var dailyPriority = DailyPriorityFor(dailyTraining, dailyPrimaryWork);
            var action = dailyTraining.Status == DailyTrainingWorkflowStatus.Active
                ? TrainingPresentationPrimaryActionKind.ContinueLiveSession
                : PrimaryActionFor(dailyPriority);
            return new CurrentTrainingPresentationReadModel(
                dailyPriority,
                action,
                (dailyTraining.CanPrepare || dailyTraining.Status == DailyTrainingWorkflowStatus.Active) &&
                    dailyPrimaryWork is not null,
                dailyPrimaryWork,
                UrgentBlocker: null,
                MaintenanceDecayPriorityFor(state),
                EvidenceSummaryFor(state),
                RevealsFor(state),
                GrantsAdvancementInApp: false,
                dailyTraining.Status,
                dailyTraining.CompletedBlockCount,
                dailyTraining.TotalBlockCount);
        }

        var maintenancePriority = MaintenanceDecayPriorityFor(state);
        var urgentBlocker = FirstBlocker(state);
        var primaryWork = maintenancePriority is null
            ? FirstWeeklyWork(state)
            : WorkFor(maintenancePriority);
        var priority = PriorityFor(state, maintenancePriority, urgentBlocker, primaryWork);
        var primaryAction = PrimaryActionFor(priority);

        return new CurrentTrainingPresentationReadModel(
            priority,
            primaryAction,
            PrimaryActionEnabled(primaryAction, primaryWork, maintenancePriority),
            primaryWork,
            urgentBlocker,
            maintenancePriority,
            EvidenceSummaryFor(state),
            RevealsFor(state),
            GrantsAdvancementInApp: false);
    }

    private static TrainingPresentationPriorityKind DailyPriorityFor(
        DailyTrainingWorkflowReadModel dailyTraining,
        TrainingPresentationWorkSummary? primaryWork)
    {
        if (dailyTraining.IsTerminal || primaryWork is null)
        {
            return TrainingPresentationPriorityKind.NoAvailableWork;
        }

        return primaryWork.SessionType switch
        {
            AppTrainingSessionType.Maintenance => TrainingPresentationPriorityKind.MaintenanceDue,
            AppTrainingSessionType.Regression => TrainingPresentationPriorityKind.DecayRestoration,
            AppTrainingSessionType.Recovery => TrainingPresentationPriorityKind.Recovery,
            _ => TrainingPresentationPriorityKind.PrescribedWork,
        };
    }

    public static CurrentTrainingPresentationReadModel FromSelection(
        NextTrainingWorkSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var maintenancePriority = MaintenanceDecayPriorityFor(selection.CurrentState);
        var urgentBlocker = FirstBlocker(selection) ?? FirstBlocker(selection.CurrentState);
        var selectedWork = WorkFor(selection);
        var priority = PriorityFor(selection, maintenancePriority, urgentBlocker, selectedWork);
        var primaryAction = PrimaryActionFor(selection, priority);

        return new CurrentTrainingPresentationReadModel(
            priority,
            primaryAction,
            PrimaryActionEnabled(selection),
            selectedWork,
            urgentBlocker,
            maintenancePriority,
            EvidenceSummaryFor(selection.CurrentState),
            RevealsFor(selection),
            GrantsAdvancementInApp: false);
    }

    public static SessionPreflightPresentationReadModel FromPreflight(
        PreUiTrainingWorkflowPreparationResult preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);

        var runtimeSession = preparation.RuntimeSession;
        var runtimeDefinition = runtimeSession?.SessionDefinition;
        var work = preparation.Selection.SelectedWork is null
            ? null
            : WorkFor(
                preparation.Selection.SelectedWork,
                WorkSourceFor(preparation.Selection.Kind),
                PrimaryMaterialValue(
                    preparation.Selection.SelectedWork.Drill,
                    runtimeSession?.InputMaterials ?? []),
                SetupItemsFor(
                    preparation.Selection.SelectedWork.Drill,
                    runtimeSession?.InputMaterials ?? []));
        var blockers = preparation.Selection.Blockers
            .Where(_ => !preparation.CanStartRuntimeSession)
            .Select(BlockerSummaryFor)
            .Concat(preparation.Rejections.Select(RejectionSummaryFor))
            .ToArray();

        return new SessionPreflightPresentationReadModel(
            preparation.Status,
            preparation.CanStartRuntimeSession,
            work,
            work?.Demand,
            work?.Standard,
            work?.HonestyConstraint,
            work?.LoadVariables.Count ?? 0,
            runtimeDefinition?.CriticalConstraints.Count ?? 0,
            runtimeSession?.ExpectedEvidenceFacts.Count ?? 0,
            runtimeSession?.PhasePlan?.Phases.Count ?? 0,
            runtimeSession?.CueSchedule?.Cues.Count ?? 0,
            runtimeSession?.InputMaterials.Count ?? 0,
            blockers,
            RevealsFor(preparation),
            GrantsAdvancementInApp: false);
    }

    public static LiveSessionPresentationReadModel FromLiveSession(
        PreUiLiveSessionState live)
    {
        ArgumentNullException.ThrowIfNull(live);

        var availableCommands = live.Commands
            .Where(command => command.IsAvailable)
            .Select(command => new LiveCommandPresentationSummary(command.Command, command.Label))
            .ToArray();

        return new LiveSessionPresentationReadModel(
            WorkFor(live),
            live.LifecycleStatus,
            live.SchedulerStatus,
            live.CurrentPhaseKind,
            live.CurrentPhaseCompletionRule,
            live.Timer,
            live.ActiveCue is null ? null : CueSummaryFor(live.ActiveCue),
            live.CurrentMaterials,
            LiveInstructionFor(live),
            PrimaryCommandFor(live, availableCommands),
            availableCommands,
            EvidenceSummaryFor(live.Evidence),
            live.IsTerminal,
            RevealsFor(live),
            GrantsAdvancementInApp: false,
            SourceDrill: live.SourceDrill);
    }

    public static ResultPresentationReadModel FromResult(
        PreUiLiveSessionCompletionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var processing = result.WorkflowResult?.ProcessingResult;
        var outcome = OutcomeFor(result, processing);
        var evidenceSummary = processing is null
            ? EvidenceSummaryFor(result.SessionState.Evidence, result.RuntimeCompletionStatus)
            : EvidenceSummaryFor(processing);
        var stateTransition = processing?.StateTransition is null
            ? null
            : TransitionSummaryFor(processing.StateTransition.Value);
        var work = WorkFor(
            result.SessionState,
            TrainingPresentationWorkSource.Result,
            processing?.SessionHistory.LoadVariables);

        return new ResultPresentationReadModel(
            outcome,
            PrimaryActionFor(result),
            PrimaryActionEnabled: result.IsProcessed || result.RuntimeCompletionStatus.HasValue,
            result.RuntimeCompletionStatus,
            work,
            result.IsProcessed,
            ProducesSuccessfulEvidence(result, processing),
            processing?.SessionHistory.CleanPerformance ?? false,
            stateTransition,
            processing?.FormalGateDecision?.Outcome,
            processing?.MaintenanceCurrencyResult?.State,
            processing?.FailureResponse?.Failure.Type,
            BlockingFailuresFor(processing, work.LoadVariables),
            evidenceSummary,
            EvidenceSummaryFor(result.SessionState.Evidence),
            RevealsFor(result),
            GrantsAdvancementInApp: false);
    }

    public static TrainingEvidencePresentationSummary EvidenceSummary(
        CurrentTrainingStateReadModel state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return EvidenceSummaryFor(state);
    }

    private static TrainingPresentationPriorityKind PriorityFor(
        CurrentTrainingStateReadModel state,
        TrainingMaintenanceDecayPriority? maintenancePriority,
        TrainingPresentationBlockerSummary? urgentBlocker,
        TrainingPresentationWorkSummary? primaryWork)
    {
        _ = state;

        if (maintenancePriority?.Kind == TrainingMaintenanceDecayPriorityKind.DecayRestoration)
        {
            return TrainingPresentationPriorityKind.DecayRestoration;
        }

        if (maintenancePriority is not null)
        {
            return TrainingPresentationPriorityKind.MaintenanceDue;
        }

        if (urgentBlocker is not null && primaryWork is null)
        {
            return TrainingPresentationPriorityKind.UrgentBlocker;
        }

        return primaryWork is null
            ? TrainingPresentationPriorityKind.NoAvailableWork
            : TrainingPresentationPriorityKind.PrescribedWork;
    }

    private static TrainingPresentationPriorityKind PriorityFor(
        NextTrainingWorkSelection selection,
        TrainingMaintenanceDecayPriority? maintenancePriority,
        TrainingPresentationBlockerSummary? urgentBlocker,
        TrainingPresentationWorkSummary? selectedWork)
    {
        return selection.Kind switch
        {
            NextTrainingWorkSelectionKind.Deload => TrainingPresentationPriorityKind.Deload,
            NextTrainingWorkSelectionKind.Recovery => TrainingPresentationPriorityKind.Recovery,
            NextTrainingWorkSelectionKind.MaintenanceNeeded => maintenancePriority?.Kind == TrainingMaintenanceDecayPriorityKind.DecayRestoration
                ? TrainingPresentationPriorityKind.DecayRestoration
                : TrainingPresentationPriorityKind.MaintenanceDue,
            NextTrainingWorkSelectionKind.Blocked => TrainingPresentationPriorityKind.UrgentBlocker,
            _ => maintenancePriority?.Kind == TrainingMaintenanceDecayPriorityKind.DecayRestoration
                ? TrainingPresentationPriorityKind.DecayRestoration
                : urgentBlocker is not null && selectedWork is null
                    ? TrainingPresentationPriorityKind.UrgentBlocker
                    : selectedWork is null
                        ? TrainingPresentationPriorityKind.NoAvailableWork
                        : TrainingPresentationPriorityKind.PrescribedWork,
        };
    }

    private static TrainingPresentationPrimaryActionKind PrimaryActionFor(
        TrainingPresentationPriorityKind priority)
    {
        return priority switch
        {
            TrainingPresentationPriorityKind.DecayRestoration => TrainingPresentationPrimaryActionKind.RestoreDecayedWork,
            TrainingPresentationPriorityKind.MaintenanceDue => TrainingPresentationPrimaryActionKind.StartMaintenance,
            TrainingPresentationPriorityKind.UrgentBlocker => TrainingPresentationPrimaryActionKind.ResolveBlocker,
            TrainingPresentationPriorityKind.Recovery => TrainingPresentationPrimaryActionKind.StartRecovery,
            TrainingPresentationPriorityKind.Deload => TrainingPresentationPrimaryActionKind.StartDeload,
            TrainingPresentationPriorityKind.PrescribedWork => TrainingPresentationPrimaryActionKind.StartPrescribedWork,
            _ => TrainingPresentationPrimaryActionKind.None,
        };
    }

    private static TrainingPresentationPrimaryActionKind PrimaryActionFor(
        NextTrainingWorkSelection selection,
        TrainingPresentationPriorityKind priority)
    {
        if (selection.SelectedWork is null)
        {
            return PrimaryActionFor(priority);
        }

        return selection.Kind switch
        {
            NextTrainingWorkSelectionKind.MaintenanceNeeded => TrainingPresentationPrimaryActionKind.StartMaintenance,
            NextTrainingWorkSelectionKind.Recovery => TrainingPresentationPrimaryActionKind.StartRecovery,
            NextTrainingWorkSelectionKind.Deload => TrainingPresentationPrimaryActionKind.StartDeload,
            NextTrainingWorkSelectionKind.Blocked => TrainingPresentationPrimaryActionKind.ResolveBlocker,
            _ => TrainingPresentationPrimaryActionKind.StartPrescribedWork,
        };
    }

    private static TrainingPresentationPrimaryActionKind PrimaryActionFor(
        PreUiLiveSessionCompletionResult result)
    {
        if (!result.RuntimeCompletionStatus.HasValue)
        {
            return TrainingPresentationPrimaryActionKind.ContinueLiveSession;
        }

        return result.IsProcessed
            ? TrainingPresentationPrimaryActionKind.ReturnToNextPrescribedAction
            : TrainingPresentationPrimaryActionKind.SaveTerminalSession;
    }

    private static bool PrimaryActionEnabled(
        TrainingPresentationPrimaryActionKind action,
        TrainingPresentationWorkSummary? primaryWork,
        TrainingMaintenanceDecayPriority? maintenancePriority)
    {
        return action switch
        {
            TrainingPresentationPrimaryActionKind.None => false,
            TrainingPresentationPrimaryActionKind.ResolveBlocker => false,
            TrainingPresentationPrimaryActionKind.RestoreDecayedWork => maintenancePriority is not null,
            _ => primaryWork is not null || maintenancePriority is not null,
        };
    }

    private static bool PrimaryActionEnabled(NextTrainingWorkSelection selection)
    {
        return selection.Kind != NextTrainingWorkSelectionKind.Blocked &&
            selection.SelectedWork is not null;
    }

    private static TrainingPresentationWorkSummary? WorkFor(NextTrainingWorkSelection selection)
    {
        if (selection.SelectedWork is not null)
        {
            return WorkFor(selection.SelectedWork, WorkSourceFor(selection.Kind));
        }

        return FirstWeeklyWork(selection.CurrentState);
    }

    private static TrainingPresentationWorkSummary WorkFor(
        SelectedTrainingWork selectedWork,
        TrainingPresentationWorkSource source,
        string? primaryMaterial = null,
        IReadOnlyList<string>? setupItems = null)
    {
        var drill = DrillFor(selectedWork.Drill);

        return new TrainingPresentationWorkSummary(
            source,
            [BranchLevelFor(selectedWork.Branch, selectedWork.Level, state: null)],
            selectedWork.Drill,
            drill.Code,
            drill.Name,
            selectedWork.SessionType,
            WeeklySession: null,
            IsAdvancementWork(selectedWork.SessionType),
            selectedWork.AdvancementWorkAllowed,
            selectedWork.Demand,
            selectedWork.Standard,
            selectedWork.HonestyConstraint,
            selectedWork.LoadVariables,
            ExerciseFor(
                selectedWork.Branch,
                selectedWork.Level,
                selectedWork.Drill,
                selectedWork.Demand,
                selectedWork.Standard,
                selectedWork.HonestyConstraint,
                selectedWork.LoadVariables,
                primaryMaterial,
                setupItems),
            HasExecutableStandard(selectedWork.Branch, selectedWork.Level, selectedWork.Drill));
    }

    private static TrainingPresentationWorkSummary WorkFor(
        DailyTrainingBlockReadModel dailyBlock,
        CurrentTrainingStateReadModel state)
    {
        var block = dailyBlock.Record;
        var standard = StandardFor(block.Branch, block.Level);
        var drill = DrillFor(block.Drill);
        var selected = new SelectedTrainingWork(
            block.Branch,
            block.Level,
            block.Drill,
            dailyBlock.SessionType,
            standard.Demand,
            standard.Standard,
            drill.HonestyConstraint,
            block.LoadVariables,
            advancementWorkAllowed: state.WeeklyPlan.AdvancementWorkAllowed &&
                IsAdvancementWork(dailyBlock.SessionType));
        return WorkFor(selected, TrainingPresentationWorkSource.DailyPrescription);
    }

    private static TrainingPresentationWorkSummary WorkFor(
        PreUiLiveSessionState live,
        TrainingPresentationWorkSource source = TrainingPresentationWorkSource.LiveSession,
        IReadOnlyList<LoadVariable>? loadVariables = null)
    {
        var drill = DrillFor(live.Drill);
        var standard = StandardFor(live.Branch, live.Level);
        var effectiveLoadVariables = loadVariables ?? [];

        return new TrainingPresentationWorkSummary(
            source,
            [BranchLevelFor(live.Branch, live.Level, state: null)],
            live.Drill,
            drill.Code,
            drill.Name,
            ToAppSessionType(live.SessionType),
            WeeklySession: null,
            IsAdvancementWork(ToAppSessionType(live.SessionType)),
            AdvancementWorkAllowed: false,
            standard.Demand,
            standard.Standard,
            drill.HonestyConstraint,
            effectiveLoadVariables,
            ExerciseFor(
                live.Branch,
                live.Level,
                live.Drill,
                standard.Demand,
                standard.Standard,
                drill.HonestyConstraint,
                effectiveLoadVariables,
                PrimaryMaterialValue(live.Drill, live.CurrentMaterials),
                setupItems: null),
            HasExecutableStandard(live.Branch, live.Level, live.Drill));
    }

    private static TrainingPresentationWorkSummary? FirstWeeklyWork(
        CurrentTrainingStateReadModel state)
    {
        var candidate = DefaultTrainingWorkPolicy.Select(state);
        if (candidate is not null)
        {
            var selectedStatus = candidate.Status;
            var work = candidate.WeeklyWork;
            var drill = DrillFor(DefaultTrainingWorkPolicy.PrimaryDrillFor(selectedStatus.Branch, selectedStatus.Level));
            var standard = StandardFor(selectedStatus.Branch, selectedStatus.Level);
            var sessionType = ExecutableTrainingStandards.HonestSessionType(
                selectedStatus.Branch,
                selectedStatus.Level,
                drill.Id,
                SessionTypeFor(work.Session, selectedStatus.State));
            var loadVariables = Array.Empty<LoadVariable>();

            return new TrainingPresentationWorkSummary(
                TrainingPresentationWorkSource.WeeklyPlan,
                [BranchLevelFor(selectedStatus.Branch, selectedStatus.Level, selectedStatus.State)],
                drill.Id,
                drill.Code,
                drill.Name,
                sessionType,
                work.Session,
                IsAdvancementWork(sessionType),
                work.IsAdvancementWork && ExecutableTrainingStandards.Supports(
                    selectedStatus.Branch,
                    selectedStatus.Level,
                    drill.Id),
                standard.Demand,
                standard.Standard,
                drill.HonestyConstraint,
                loadVariables,
                ExerciseFor(
                    selectedStatus.Branch,
                    selectedStatus.Level,
                    drill.Id,
                    standard.Demand,
                    standard.Standard,
                    drill.HonestyConstraint,
                    loadVariables,
                    primaryMaterial: null),
                HasExecutableStandard(selectedStatus.Branch, selectedStatus.Level, drill.Id));
        }

        return null;
    }

    private static TrainingPresentationWorkSummary WorkFor(
        TrainingMaintenanceDecayPriority priority)
    {
        var drill = DrillFor(DefaultTrainingWorkPolicy.PrimaryDrillFor(priority.Branch, priority.Level));
        var standard = StandardFor(priority.Branch, priority.Level);
        var sessionType = ExecutableTrainingStandards.HonestSessionType(
            priority.Branch,
            priority.Level,
            drill.Id,
            priority.Kind == TrainingMaintenanceDecayPriorityKind.DecayRestoration
                ? AppTrainingSessionType.Regression
                : AppTrainingSessionType.Maintenance);
        var loadVariables = Array.Empty<LoadVariable>();

        return new TrainingPresentationWorkSummary(
            priority.Kind == TrainingMaintenanceDecayPriorityKind.DecayRestoration
                ? TrainingPresentationWorkSource.Recovery
                : TrainingPresentationWorkSource.Maintenance,
            [BranchLevelFor(priority.Branch, priority.Level, priority.BranchState)],
            drill.Id,
            drill.Code,
            drill.Name,
            sessionType,
            WeeklySession: null,
            IsAdvancementWork: false,
            AdvancementWorkAllowed: false,
            standard.Demand,
            standard.Standard,
            drill.HonestyConstraint,
            loadVariables,
            ExerciseFor(
                priority.Branch,
                priority.Level,
                drill.Id,
                standard.Demand,
                standard.Standard,
                drill.HonestyConstraint,
                loadVariables,
                primaryMaterial: null),
            HasExecutableStandard(priority.Branch, priority.Level, drill.Id));
    }

    private static TrainingPresentationWorkSource WorkSourceFor(
        NextTrainingWorkSelectionKind selectionKind)
    {
        return selectionKind switch
        {
            NextTrainingWorkSelectionKind.MaintenanceNeeded => TrainingPresentationWorkSource.Maintenance,
            NextTrainingWorkSelectionKind.Recovery => TrainingPresentationWorkSource.Recovery,
            NextTrainingWorkSelectionKind.Deload => TrainingPresentationWorkSource.Deload,
            _ => TrainingPresentationWorkSource.SelectedWork,
        };
    }

    private static TrainingPresentationBlockerSummary? FirstBlocker(
        NextTrainingWorkSelection selection)
    {
        return selection.Blockers.Select(BlockerSummaryFor).FirstOrDefault();
    }

    private static TrainingPresentationBlockerSummary? FirstBlocker(
        CurrentTrainingStateReadModel state)
    {
        return state.BlockedAdvancement.Select(BlockerSummaryFor).FirstOrDefault();
    }

    private static TrainingPresentationBlockerSummary BlockerSummaryFor(
        NextTrainingWorkBlocker blocker)
    {
        return new TrainingPresentationBlockerSummary(
            BlockerKindFor(blocker.Source),
            blocker.Branch,
            blocker.Level,
            SeverityFor(blocker.Source),
            blocker.Detail);
    }

    private static TrainingPresentationBlockerSummary BlockerSummaryFor(
        CurrentTrainingStateBlocker blocker)
    {
        return new TrainingPresentationBlockerSummary(
            BlockerKindFor(blocker.Source),
            blocker.Branch,
            blocker.Level,
            TrainingPresentationBlockerSeverity.Blocking,
            blocker.Detail);
    }

    private static TrainingPresentationBlockerSummary RejectionSummaryFor(
        PreUiTrainingWorkflowRejection rejection)
    {
        return new TrainingPresentationBlockerSummary(
            TrainingPresentationBlockerKind.PreparationRejected,
            Branch: null,
            Level: null,
            TrainingPresentationBlockerSeverity.Blocking,
            rejection.Detail);
    }

    private static TrainingPresentationBlockerKind BlockerKindFor(
        NextTrainingWorkBlockerSource source)
    {
        return source switch
        {
            NextTrainingWorkBlockerSource.BranchState => TrainingPresentationBlockerKind.BranchUnavailable,
            NextTrainingWorkBlockerSource.MaintenanceCurrency => TrainingPresentationBlockerKind.MaintenanceOrDecay,
            NextTrainingWorkBlockerSource.TestReadiness => TrainingPresentationBlockerKind.Readiness,
            NextTrainingWorkBlockerSource.DependencyCap => TrainingPresentationBlockerKind.Dependency,
            NextTrainingWorkBlockerSource.GlobalBalance => TrainingPresentationBlockerKind.GlobalBalance,
            NextTrainingWorkBlockerSource.TransferEligibility => TrainingPresentationBlockerKind.Transfer,
            NextTrainingWorkBlockerSource.WeeklyProgramming => TrainingPresentationBlockerKind.WeeklyConstraint,
            NextTrainingWorkBlockerSource.Decay => TrainingPresentationBlockerKind.MaintenanceOrDecay,
            _ => TrainingPresentationBlockerKind.Unknown,
        };
    }

    private static TrainingPresentationBlockerKind BlockerKindFor(
        CurrentTrainingStateBlockerSource source)
    {
        return source switch
        {
            CurrentTrainingStateBlockerSource.Category => TrainingPresentationBlockerKind.PractitionerCategory,
            CurrentTrainingStateBlockerSource.WeeklyProgramming => TrainingPresentationBlockerKind.WeeklyConstraint,
            CurrentTrainingStateBlockerSource.DependencyCap => TrainingPresentationBlockerKind.Dependency,
            CurrentTrainingStateBlockerSource.GlobalBalance => TrainingPresentationBlockerKind.GlobalBalance,
            _ => TrainingPresentationBlockerKind.Unknown,
        };
    }

    private static TrainingPresentationBlockerSeverity SeverityFor(
        NextTrainingWorkBlockerSource source)
    {
        return source switch
        {
            NextTrainingWorkBlockerSource.Decay => TrainingPresentationBlockerSeverity.Urgent,
            NextTrainingWorkBlockerSource.MaintenanceCurrency => TrainingPresentationBlockerSeverity.Urgent,
            NextTrainingWorkBlockerSource.WeeklyProgramming => TrainingPresentationBlockerSeverity.Blocking,
            _ => TrainingPresentationBlockerSeverity.Blocking,
        };
    }

    private static TrainingMaintenanceDecayPriority? MaintenanceDecayPriorityFor(
        CurrentTrainingStateReadModel state)
    {
        var decayed = state.BranchLevelStates
            .Where(status => status.State == BranchLevelState.Decayed)
            .OrderBy(status => BranchOrder(status.Branch))
            .ThenByDescending(status => LevelRank(status.Level))
            .FirstOrDefault();
        if (decayed.State == BranchLevelState.Decayed)
        {
            return new TrainingMaintenanceDecayPriority(
                TrainingMaintenanceDecayPriorityKind.DecayRestoration,
                decayed.Branch,
                decayed.Level,
                BranchName(decayed.Branch),
                LevelName(decayed.Level),
                decayed.State,
                CurrencyState: null,
                DaysSinceLastPassingCheck: null,
                ConsecutiveFailures: 0,
                BlocksAdvancement: true,
                $"{BranchName(decayed.Branch)} {LevelName(decayed.Level)} is decayed and must be restored before ordinary advancement work.");
        }

        var due = state.DueMaintenance
            .OrderByDescending(record => MaintenancePriorityRank(record.Currency.State))
            .ThenBy(record => BranchOrder(record.BranchLevel.Branch))
            .ThenByDescending(record => LevelRank(record.BranchLevel.Level))
            .FirstOrDefault();
        if (due is null)
        {
            return null;
        }

        return new TrainingMaintenanceDecayPriority(
            MaintenanceKindFor(due.Currency.State),
            due.BranchLevel.Branch,
            due.BranchLevel.Level,
            BranchName(due.BranchLevel.Branch),
            LevelName(due.BranchLevel.Level),
            due.BranchLevel.State,
            due.Currency.State,
            due.Currency.DaysSinceLastPassingCheck,
            due.Currency.ConsecutiveFailures,
            state.BlockedAdvancement.Any(blocker => blocker.Branch == due.BranchLevel.Branch),
            $"{BranchName(due.BranchLevel.Branch)} {LevelName(due.BranchLevel.Level)} maintenance is {due.Currency.State}.");
    }

    private static TrainingMaintenanceDecayPriorityKind MaintenanceKindFor(
        MaintenanceCurrencyState state)
    {
        return state switch
        {
            MaintenanceCurrencyState.Warning => TrainingMaintenanceDecayPriorityKind.MaintenanceWarning,
            MaintenanceCurrencyState.Failed => TrainingMaintenanceDecayPriorityKind.MaintenanceFailed,
            _ => TrainingMaintenanceDecayPriorityKind.MaintenanceDue,
        };
    }

    private static int MaintenancePriorityRank(MaintenanceCurrencyState state)
    {
        return state switch
        {
            MaintenanceCurrencyState.Failed => 3,
            MaintenanceCurrencyState.Due => 2,
            MaintenanceCurrencyState.Warning => 1,
            _ => 0,
        };
    }

    private static TrainingEvidencePresentationSummary EvidenceSummaryFor(
        CurrentTrainingStateReadModel state)
    {
        var latest = state.EvidenceSummaries.FirstOrDefault();
        var latestEvidenceKinds = latest?.Artifact.ObservableEvidence
            .Select(evidence => evidence.Kind)
            .ToArray() ?? [];

        return new TrainingEvidencePresentationSummary(
            state.RecentSessions.Count,
            state.RecentSessions.Count(session => session.CleanPerformance),
            state.EvidenceSummaries.Count,
            state.ProgressRecords.FormalTestAttempts.Count,
            state.ProgressRecords.StabilizationPasses.Count,
            state.ProgressRecords.MaintenanceChecks.Count,
            latest?.Artifact.Category,
            latest?.Event.Branch,
            latest?.Event.Level,
            latest?.Event.Drill,
            latestEvidenceKinds,
            state.EvidenceSummaries.Any(artifact => artifact.Artifact.ObservableEvidence.Count > 0),
            HasFailureEvidence(state));
    }

    private static TrainingEvidencePresentationSummary EvidenceSummaryFor(
        CompletedRuntimeSessionProcessingResult processing)
    {
        var latest = processing.EvidenceArtifacts.FirstOrDefault();
        var latestEvidenceKinds = latest?.Artifact.ObservableEvidence
            .Select(evidence => evidence.Kind)
            .ToArray() ?? [];

        return new TrainingEvidencePresentationSummary(
            RecentSessionCount: 1,
            RecentCleanSessionCount: processing.SessionHistory.CleanPerformance ? 1 : 0,
            processing.EvidenceArtifacts.Count,
            processing.FormalTestAttempt is null ? 0 : 1,
            processing.StabilizationPass is null ? 0 : 1,
            processing.MaintenanceCheck is null ? 0 : 1,
            latest?.Artifact.Category,
            latest?.Event.Branch,
            latest?.Event.Level,
            latest?.Event.Drill,
            latestEvidenceKinds,
            processing.EvidenceArtifacts.Any(artifact => artifact.Artifact.ObservableEvidence.Count > 0),
            HasFailureEvidence(processing));
    }

    private static TrainingEvidencePresentationSummary EvidenceSummaryFor(
        PreUiLiveSessionEvidenceState evidence,
        RuntimeSessionCompletionStatus? completionStatus)
    {
        return new TrainingEvidencePresentationSummary(
            RecentSessionCount: 0,
            RecentCleanSessionCount: 0,
            EvidenceArtifactCount: 0,
            FormalAttemptCount: 0,
            StabilizationPassCount: 0,
            MaintenanceCheckCount: 0,
            LatestEvidenceCategory: null,
            LatestBranch: null,
            LatestLevel: null,
            LatestDrill: null,
            LatestObservableEvidenceKinds: [],
            HasObservableEvidence: evidence.EvidenceFactCount > 0,
            HasFailureEvidence: completionStatus is RuntimeSessionCompletionStatus.Abandoned
                    or RuntimeSessionCompletionStatus.Failed
                    or RuntimeSessionCompletionStatus.TimedOut ||
                evidence.ErrorCount > 0 ||
                evidence.GuessCount > 0);
    }

    private static LiveEvidencePresentationSummary EvidenceSummaryFor(
        PreUiLiveSessionEvidenceState evidence)
    {
        return new LiveEvidencePresentationSummary(
            evidence.RuntimeEventCount,
            evidence.EvidenceFactCount,
            evidence.ExpectedEvidenceFactCount,
            evidence.DriftCount,
            evidence.GuessCount,
            evidence.ErrorCount,
            evidence.CueCount,
            evidence.CueResponseCount,
            evidence.AnswerCount,
            evidence.CorrectionCount,
            evidence.GuessCount > 0 || evidence.ErrorCount > 0,
            evidence.EvidenceFactCount >= evidence.ExpectedEvidenceFactCount,
            evidence.ReturnCount,
            evidence.LateReturnCount,
            evidence.TargetChangeCount,
            evidence.OpenDriftCount,
            evidence.MaximumReturnTime);
    }

    private static bool HasFailureEvidence(CurrentTrainingStateReadModel state)
    {
        return state.RecentSessions.Any(session => !session.CleanPerformance) ||
            state.EvidenceSummaries.Any(ArtifactHasFailureEvidence) ||
            state.ProgressRecords.FormalTestAttempts.Any(attempt =>
                attempt.Attempt.PassState == FormalTestPassState.Fail ||
                attempt.Attempt.FailureType.HasValue);
    }

    private static bool HasFailureEvidence(CompletedRuntimeSessionProcessingResult processing)
    {
        return processing.CompletionStatus is RuntimeSessionCompletionStatus.Abandoned
                or RuntimeSessionCompletionStatus.Failed
                or RuntimeSessionCompletionStatus.TimedOut ||
            !processing.SessionHistory.CleanPerformance ||
            processing.FormalGateDecision?.Outcome == GateOutcome.Fail ||
            processing.StandardEvaluationResult?.Passed == false ||
            processing.MaintenanceCurrencyResult?.State == MaintenanceCurrencyState.Failed ||
            processing.FailureResponse is not null ||
            processing.EvidenceArtifacts.Any(ArtifactHasFailureEvidence);
    }

    private static bool ArtifactHasFailureEvidence(LocalEvidenceArtifactRecord artifact)
    {
        return artifact.Artifact.ObservableEvidence.Any(evidence =>
            evidence.Kind is ObservableEvidenceKind.FailedItemList
                or ObservableEvidenceKind.ErrorCount);
    }

    private static LiveCuePresentationSummary CueSummaryFor(
        PreUiLiveSessionCueState cue)
    {
        return new LiveCuePresentationSummary(
            cue.Kind,
            cue.Cue,
            cue.ResponseExpectation,
            cue.ResponseWindow,
            cue.ResponseExpectation == RuntimeCueResponseExpectation.ResponseRequired,
            HasHiddenExpectedResponse: cue.ExpectedResponse is not null);
    }

    private static string LiveInstructionFor(PreUiLiveSessionState live)
    {
        if (live.ActiveCue is { } cue)
        {
            if (live.Drill == DrillId.AI2DisruptionRecovery && cue.Kind == RuntimeCueKind.Interruption)
            {
                return "The source rule still applies.";
            }

            var effectiveDrill = live.SourceDrill ?? live.Drill;
            if (effectiveDrill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter)
            {
                return "Apply the cue rule.";
            }

            if (effectiveDrill == DrillId.IR2ExceptionRule)
            {
                return "Apply the rule.";
            }

            return cue.ResponseExpectation == RuntimeCueResponseExpectation.ResponseRequired
                ? "Respond now."
                : "Do not respond.";
        }

        return live.CurrentPhaseKind switch
        {
            RuntimeSessionPhaseKind.InstructionPrep => live.SourceDrill.HasValue
                ? $"{PrepInstructionFor(live.SourceDrill.Value)} The source standard still counts."
                : PrepInstructionFor(live.Drill),
            RuntimeSessionPhaseKind.ActiveWork => ActiveInstructionFor(live),
            RuntimeSessionPhaseKind.EncodeWindow => live.Drill == DrillId.WM2MentalTransform
                ? "Study the source and operations. Use no notes."
                : live.CurrentPhaseCompletionRule == RuntimeSessionPhaseCompletionRule.Timed
                    ? "Study the items now. This window will close."
                    : "Study the items once, then continue.",
            RuntimeSessionPhaseKind.DelayWindow => "Keep the material hidden.",
            RuntimeSessionPhaseKind.CueResponse => CommandIsAvailable(live, RuntimeInputCommandKind.FinishPhase)
                ? "Cue set complete."
                : "Wait for the next cue.",
            RuntimeSessionPhaseKind.ReconstructionInput => live.Drill switch
            {
                DrillId.WM2MentalTransform => "Enter the final result and explain the rule.",
                DrillId.CO1RuleExtraction => "Apply the locked rule to every unseen example.",
                DrillId.CO2StructureMapping => "Map the named relations. Reject surface matches.",
                DrillId.TI1CompositeTask => "Reconstruct the component order and evidence after the delay.",
                _ => "Reconstruct from memory. Do not reopen the items.",
            },
            RuntimeSessionPhaseKind.Audit => CommandIsAvailable(live, RuntimeInputCommandKind.StartAudit)
                ? "Lock the original, then start the audit."
                : "Record only findings you can support.",
            RuntimeSessionPhaseKind.Rest => "Rest. The next set will appear when ready.",
            RuntimeSessionPhaseKind.Recovery => "Restart from the last stable step.",
            RuntimeSessionPhaseKind.Review => "Check the evidence, then finish.",
            _ => "Session complete.",
        };
    }

    private static string PrepInstructionFor(DrillId drill)
    {
        return drill switch
        {
            DrillId.FH1TargetHold => "Say the target once. Start when ready.",
            DrillId.FH2DistractorHold => "Keep the target. Ignore every distractor.",
            DrillId.FS1CueSwitch => "Read the targets. Switch only on a valid cue.",
            DrillId.FS2InvalidCueFilter => "Read the targets. Invalid cues change nothing.",
            DrillId.WM1DelayedReconstruction => "Read the encode rule. Rereading will be blocked.",
            DrillId.WM2MentalTransform => "Read the transform rule. Use no intermediate notes.",
            DrillId.IR1GoNoGoRule => "Respond to go cues. Withhold on no-go cues.",
            DrillId.IR2ExceptionRule => "Read the rule and its exception before starting.",
            DrillId.DE1PairDiscrimination => "Compare only the named feature. Mark guesses.",
            DrillId.DE2SeededAudit => "The original stays locked during the audit.",
            DrillId.CO1RuleExtraction => "Infer one rule that fits every shown example.",
            DrillId.CO2StructureMapping => "Map roles and relations, not surface words.",
            DrillId.AI1PressureRepeat => "Pressure changes the context, not the standard.",
            DrillId.AI2DisruptionRecovery => "After disruption, resume from the last stable step.",
            DrillId.TI1CompositeTask => "Each component keeps its own passing standard.",
            DrillId.TI2GlobalReviewTask => "Use the evidence to make one program decision.",
            _ => "Start when the rule is clear.",
        };
    }

    private static string ActiveInstructionFor(PreUiLiveSessionState live)
    {
        var effectiveDrill = live.SourceDrill ?? live.Drill;
        return effectiveDrill switch
        {
            DrillId.FH1TargetHold or DrillId.FH2DistractorHold => live.Evidence.OpenDriftCount > 0
                ? "Return to the target now."
                : "Hold the target.",
            DrillId.DE1PairDiscrimination => "Judge each pair on the named feature.",
            DrillId.CO1RuleExtraction => "Commit one testable rule before unseen examples appear.",
            DrillId.CO2StructureMapping => "Name the relations before the mapping prompt appears.",
            DrillId.AI1PressureRepeat => "Repeat the source task to the same standard.",
            DrillId.AI2DisruptionRecovery => "Resume from the last stable step.",
            DrillId.TI1CompositeTask => "Complete each component to its own standard.",
            DrillId.TI2GlobalReviewTask => "Complete each component. Keep its evidence separate.",
            _ => "Complete the visible task to its stated standard.",
        };
    }

    private static bool CommandIsAvailable(
        PreUiLiveSessionState live,
        RuntimeInputCommandKind command)
    {
        return live.Commands.Any(item => item.Command == command && item.IsAvailable);
    }

    private static TrainingExercisePresentation ExerciseFor(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        string demand,
        string standard,
        string honestyConstraint,
        IReadOnlyList<LoadVariable> loadVariables,
        string? primaryMaterial,
        IReadOnlyList<string>? setupItems = null)
    {
        var drillDefinition = DrillFor(drill);
        var levelLabel = $"Level {LevelRank(level)}";
        var branchLevel = $"{CompactBranchName(branch)} · {levelLabel}";

        if (drill == DrillId.FH1TargetHold)
        {
            var duration = LoadVariableValue(loadVariables, "duration", "3 minutes");
            return new TrainingExercisePresentation(
                drillDefinition.Name,
                levelLabel,
                $"Hold one simple target for {duration}. When attention leaves, tap Mind wandered and return.",
                "Practice one loop: notice attention moved, mark it, return to the same target.",
                "The point is a clean return, not feeling calm or forcing the mind blank.",
                "After this is stable, later exercises add longer holds, distraction, memory, switching, and transfer.",
                "Read the target. Say it once in your head. Start when you are ready to hold it.",
                $"Counts if: finish {duration}, tap 5 times or fewer, return within 10 seconds, keep the same target.",
                "Try again if: you stop early, switch targets, miss a wander, tap more than 5 times, or take too long to return.",
                "Tap Mind wandered every time attention leaves. Keep the same target.",
                "The app saves wander taps, return time, target changes, and whether you finished or stopped.",
                primaryMaterial,
                setupItems ?? []);
        }

        return new TrainingExercisePresentation(
            drillDefinition.Name,
            branchLevel,
            demand,
            "Train a named capacity under a clear constraint.",
            "Repeated clean attempts show whether the capacity is becoming stable.",
            "Clean work earns harder constraints, stabilization, and transfer.",
            "Before you start, read the rule or material and make sure you know the response the exercise asks for.",
            $"Success means: {standard}",
            "This will not count as successful if the success rule or honesty rule is broken.",
            $"Keep it honest: {honestyConstraint}",
            "The session records observable results, missed steps, and failed constraints.",
            primaryMaterial,
            setupItems ?? []);
    }

    private static string? PrimaryMaterialValue(
        DrillId drill,
        IReadOnlyList<PreUiLiveSessionMaterialState> materials)
    {
        var target = materials.FirstOrDefault(material =>
            string.Equals(material.Kind, "TargetStatement", StringComparison.Ordinal))?.Value;
        if (!string.IsNullOrWhiteSpace(target))
        {
            return DisplayTargetValue(target);
        }

        if (drill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter)
        {
            return JoinMaterialValues(
                materials
                    .Where(material => string.Equals(material.Kind, "TargetSet", StringComparison.Ordinal))
                    .Select(material => material.Value));
        }

        if (drill is DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery)
        {
            var sourceTargets = JoinMaterialValues(materials
                .Where(material => string.Equals(material.Kind, "TargetSet", StringComparison.Ordinal))
                .Select(material => material.Value));
            if (sourceTargets is not null)
            {
                return $"Switch between {sourceTargets}. Invalid cues change nothing.";
            }

            var sourceRule = materials.FirstOrDefault(material =>
                string.Equals(material.Kind, "RuleStatement", StringComparison.Ordinal))?.Value;
            if (sourceRule is not null)
            {
                return CompactExceptionRule(sourceRule);
            }
        }

        var preferredKind = SafePrimaryMaterialKind(drill);
        var value = materials.FirstOrDefault(material =>
                string.Equals(material.Kind, preferredKind, StringComparison.Ordinal))?.Value ??
            materials.FirstOrDefault()?.Value;
        return DisplayPrimaryMaterialValue(drill, value);
    }

    private static string? PrimaryMaterialValue(
        DrillId drill,
        IReadOnlyList<GeneratedContentMaterial> materials)
    {
        var target = materials.FirstOrDefault(material =>
            material.Kind == GeneratedContentMaterialKind.TargetStatement)?.Value;
        if (!string.IsNullOrWhiteSpace(target))
        {
            return DisplayTargetValue(target);
        }

        if (drill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter)
        {
            return JoinMaterialValues(
                materials
                    .Where(material => material.Kind == GeneratedContentMaterialKind.TargetSet)
                    .Select(material => material.Value));
        }

        if (drill is DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery)
        {
            var sourceTargets = JoinMaterialValues(materials
                .Where(material => material.Kind == GeneratedContentMaterialKind.TargetSet)
                .Select(material => material.Value));
            if (sourceTargets is not null)
            {
                return $"Switch between {sourceTargets}. Invalid cues change nothing.";
            }

            var sourceRule = materials.FirstOrDefault(material =>
                material.Kind == GeneratedContentMaterialKind.RuleStatement)?.Value;
            if (sourceRule is not null)
            {
                return CompactExceptionRule(sourceRule);
            }
        }

        var preferredKind = SafePrimaryMaterialKind(drill);
        var value = materials.FirstOrDefault(material =>
                string.Equals(material.Kind.ToString(), preferredKind, StringComparison.Ordinal))?.Value ??
            materials.FirstOrDefault(material => !HiddenBeforeReview(material.Kind))?.Value;
        return DisplayPrimaryMaterialValue(drill, value);
    }

    private static string? DisplayPrimaryMaterialValue(DrillId drill, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return drill switch
        {
            DrillId.WM1DelayedReconstruction => "Study once. No rereading.",
            DrillId.WM2MentalTransform => "Transform mentally. Use no notes.",
            DrillId.IR2ExceptionRule => CompactExceptionRule(value),
            DrillId.DE1PairDiscrimination => "Compare only the named feature.",
            DrillId.DE2SeededAudit => "Report only errors you can support.",
            DrillId.CO1RuleExtraction => "Find one rule that fits every example.",
            DrillId.CO2StructureMapping => "Map roles and relations, not surface words.",
            DrillId.AI1PressureRepeat => "Repeat the source task to the same standard.",
            DrillId.AI2DisruptionRecovery => "Resume the source task after disruption.",
            DrillId.TI1CompositeTask => "Complete each component to its own standard.",
            DrillId.TI2GlobalReviewTask => "Audit the evidence and make one program decision.",
            _ => value,
        };
    }

    private static IReadOnlyList<string> SetupItemsFor(
        DrillId drill,
        IReadOnlyList<GeneratedContentMaterial> materials)
    {
        if (drill != DrillId.IR2ExceptionRule &&
            drill is not (DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery))
        {
            return [];
        }

        return materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExceptionDefinition)
            .Select(material => CompactException(material.Value))
            .Where(value => value.Length > 0)
            .ToArray();
    }

    private static string CompactExceptionRule(string value)
    {
        var normalized = value.ToLowerInvariant();
        if (normalized.Contains("tap round", StringComparison.Ordinal) &&
            normalized.Contains("withhold angular", StringComparison.Ordinal))
        {
            return "Tap round. Withhold angular. Exceptions win.";
        }

        return value;
    }

    private static string CompactException(string value)
    {
        var colon = value.IndexOf(':');
        var rule = colon >= 0 ? value[(colon + 1)..].Trim() : value.Trim();
        var detail = rule.IndexOf(';');
        if (detail >= 0)
        {
            rule = rule[..detail].Trim();
        }

        rule = rule.Replace(" -> ", ": ", StringComparison.Ordinal)
            .Replace(" instead", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim()
            .TrimEnd('.');
        return rule.Length == 0
            ? string.Empty
            : char.ToUpperInvariant(rule[0]) + rule[1..];
    }

    private static string? JoinMaterialValues(IEnumerable<string> values)
    {
        var items = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return items.Length == 0 ? null : string.Join(" / ", items);
    }

    private static string SafePrimaryMaterialKind(DrillId drill)
    {
        return drill switch
        {
            DrillId.FH1TargetHold or DrillId.FH2DistractorHold => "TargetStatement",
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter => "TargetSet",
            DrillId.WM1DelayedReconstruction => "EncodeInstruction",
            DrillId.WM2MentalTransform => "TransformRule",
            DrillId.IR1GoNoGoRule => "CuePace",
            DrillId.IR2ExceptionRule => "RuleStatement",
            DrillId.DE1PairDiscrimination => "RelevantFeature",
            DrillId.DE2SeededAudit => "AuditInstruction",
            DrillId.CO1RuleExtraction => "RuleFamily",
            DrillId.CO2StructureMapping => "MappingLimit",
            DrillId.AI1PressureRepeat => "SourceBranchStandard",
            DrillId.AI2DisruptionRecovery => "SourceTask",
            DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask => "CompositeTaskPrompt",
            _ => string.Empty,
        };
    }

    private static bool HiddenBeforeReview(GeneratedContentMaterialKind kind)
    {
        return kind is GeneratedContentMaterialKind.ExpectedActiveTarget or
            GeneratedContentMaterialKind.ExpectedAction or
            GeneratedContentMaterialKind.ExpectedReconstruction or
            GeneratedContentMaterialKind.FinalExpectedOutput or
            GeneratedContentMaterialKind.MatchTruth or
            GeneratedContentMaterialKind.SeededError or
            GeneratedContentMaterialKind.ExpectedFinding or
            GeneratedContentMaterialKind.RuleStatement or
            GeneratedContentMaterialKind.ExpectedClassification or
            GeneratedContentMaterialKind.ExpectedMapping;
    }

    private static string DisplayTargetValue(string value)
    {
        var target = StripPrefix(value, "Hold target phrase:")
            ?? StripPrefix(value, "Hold target word:")
            ?? value;

        return StripTargetSentencePunctuation(target);
    }

    private static string StripTargetSentencePunctuation(string value)
    {
        var trimmed = value.Trim();
        return trimmed.EndsWith(".", StringComparison.Ordinal)
            ? trimmed[..^1].TrimEnd()
            : trimmed;
    }

    private static string? StripPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : null;
    }

    private static LiveCommandPresentationSummary? PrimaryCommandFor(
        PreUiLiveSessionState live,
        IReadOnlyList<LiveCommandPresentationSummary> commands)
    {
        if (live.LifecycleStatus == RuntimeSessionLifecycleStatus.Paused)
        {
            return FirstCommand(commands, RuntimeInputCommandKind.Resume);
        }

        return FirstCommand(commands, RuntimeInputCommandKind.RespondToCue)
            ?? FirstCommand(commands, RuntimeInputCommandKind.MarkReturn)
            ?? FirstCommand(commands, RuntimeInputCommandKind.MarkDrift)
            ?? FirstCommand(commands, RuntimeInputCommandKind.StartAudit)
            ?? FirstCommand(commands, RuntimeInputCommandKind.SubmitAnswer)
            ?? FirstCommand(commands, RuntimeInputCommandKind.FinishPhase)
            ?? FirstCommand(commands, RuntimeInputCommandKind.Resume);
    }

    private static LiveCommandPresentationSummary? FirstCommand(
        IReadOnlyList<LiveCommandPresentationSummary> commands,
        RuntimeInputCommandKind command)
    {
        return commands.FirstOrDefault(item => item.Command == command);
    }

    private static TrainingResultPresentationOutcomeKind OutcomeFor(
        PreUiLiveSessionCompletionResult result,
        CompletedRuntimeSessionProcessingResult? processing)
    {
        if (!result.RuntimeCompletionStatus.HasValue)
        {
            return TrainingResultPresentationOutcomeKind.NotTerminal;
        }

        if (result.Status == PreUiLiveSessionCompletionStatus.CancelledBeforeWork)
        {
            return TrainingResultPresentationOutcomeKind.Cancelled;
        }

        if (result.RuntimeCompletionStatus.Value == RuntimeSessionCompletionStatus.Abandoned)
        {
            return TrainingResultPresentationOutcomeKind.Abandoned;
        }

        if (result.RuntimeCompletionStatus.Value == RuntimeSessionCompletionStatus.TimedOut)
        {
            return TrainingResultPresentationOutcomeKind.TimedOut;
        }

        if (result.RuntimeCompletionStatus.Value == RuntimeSessionCompletionStatus.Failed)
        {
            return TrainingResultPresentationOutcomeKind.Failed;
        }

        if (processing?.DecayResult is { ChangedState: true })
        {
            return TrainingResultPresentationOutcomeKind.Decayed;
        }

        if (processing?.MaintenanceCurrencyResult is { } maintenance)
        {
            return maintenance.State switch
            {
                MaintenanceCurrencyState.Current => TrainingResultPresentationOutcomeKind.Maintenance,
                MaintenanceCurrencyState.Warning => TrainingResultPresentationOutcomeKind.MaintenanceWarning,
                MaintenanceCurrencyState.Failed => TrainingResultPresentationOutcomeKind.MaintenanceFailed,
                _ => TrainingResultPresentationOutcomeKind.NoAdvancement,
            };
        }

        if (processing?.StandardEvaluationResult?.Passed == false ||
            processing?.FormalGateDecision?.Outcome == GateOutcome.Fail ||
            processing?.FailureResponse is not null)
        {
            return TrainingResultPresentationOutcomeKind.Failed;
        }

        if (processing?.TransferEligibilityResult is { IsEligible: false })
        {
            return TrainingResultPresentationOutcomeKind.Blocked;
        }

        if (processing?.StateTransition is { } transition &&
            transition.IsValid &&
            transition.CurrentStatus.State != transition.NextStatus.State)
        {
            return transition.NextStatus.State switch
            {
                BranchLevelState.TestReady => TrainingResultPresentationOutcomeKind.TestReady,
                BranchLevelState.PassedOnce => TrainingResultPresentationOutcomeKind.PassedOnce,
                BranchLevelState.Stabilizing => TrainingResultPresentationOutcomeKind.Stabilizing,
                BranchLevelState.Owned => TrainingResultPresentationOutcomeKind.Owned,
                BranchLevelState.Maintenance => TrainingResultPresentationOutcomeKind.Maintenance,
                BranchLevelState.Decayed => TrainingResultPresentationOutcomeKind.Decayed,
                _ => TrainingResultPresentationOutcomeKind.NoAdvancement,
            };
        }

        if (processing?.StabilizationOwnershipResult is { } stabilization)
        {
            return stabilization.BranchLevelState switch
            {
                BranchLevelState.Owned when stabilization.IsOwned => TrainingResultPresentationOutcomeKind.Owned,
                BranchLevelState.Stabilizing => TrainingResultPresentationOutcomeKind.Stabilizing,
                BranchLevelState.PassedOnce => TrainingResultPresentationOutcomeKind.PassedOnce,
                _ => TrainingResultPresentationOutcomeKind.NoAdvancement,
            };
        }

        if (processing?.FormalGateDecision is { } gate)
        {
            return gate.Outcome switch
            {
                GateOutcome.PassOnce => TrainingResultPresentationOutcomeKind.PassedOnce,
                GateOutcome.Stabilize => TrainingResultPresentationOutcomeKind.Stabilizing,
                GateOutcome.Own => TrainingResultPresentationOutcomeKind.Owned,
                GateOutcome.Maintain => TrainingResultPresentationOutcomeKind.Maintenance,
                GateOutcome.Regress => TrainingResultPresentationOutcomeKind.Recovery,
                GateOutcome.Review => TrainingResultPresentationOutcomeKind.Blocked,
                _ => TrainingResultPresentationOutcomeKind.NoAdvancement,
            };
        }

        if (processing?.TransferEligibilityResult is { } transfer)
        {
            return transfer.IsEligible
                ? TrainingResultPresentationOutcomeKind.TransferEligible
                : TrainingResultPresentationOutcomeKind.Blocked;
        }

        if (IsRecoveryResult(result, processing))
        {
            return TrainingResultPresentationOutcomeKind.Recovery;
        }

        if (processing?.SessionHistory.CleanPerformance == true)
        {
            return TrainingResultPresentationOutcomeKind.CleanPractice;
        }

        return TrainingResultPresentationOutcomeKind.NoAdvancement;
    }

    private static bool IsRecoveryResult(
        PreUiLiveSessionCompletionResult result,
        CompletedRuntimeSessionProcessingResult? processing)
    {
        return result.SessionState.SessionType == SessionType.Recovery ||
            processing?.SessionHistory.SessionType == LocalCompletedSessionType.Recovery ||
            processing?.SessionHistory.RecoveryMarked == true;
    }

    private static bool ProducesSuccessfulEvidence(
        PreUiLiveSessionCompletionResult result,
        CompletedRuntimeSessionProcessingResult? processing)
    {
        return result.RuntimeCompletionStatus == RuntimeSessionCompletionStatus.Completed &&
            processing?.SessionHistory.CleanPerformance == true;
    }

    private static TrainingStateTransitionPresentation TransitionSummaryFor(
        BranchLevelStatusTransitionResult transition)
    {
        return new TrainingStateTransitionPresentation(
            transition.CurrentStatus.Branch,
            transition.CurrentStatus.Level,
            transition.CurrentStatus.State,
            transition.NextStatus.State,
            transition.Transition,
            transition.IsValid && transition.CurrentStatus.State != transition.NextStatus.State);
    }

    private static IReadOnlyList<string> BlockingFailuresFor(
        CompletedRuntimeSessionProcessingResult? processing,
        IReadOnlyList<LoadVariable> loadVariables)
    {
        if (processing is null)
        {
            return [];
        }

        var standardFailures = processing.StandardEvaluationResult?.Failures ?? [];
        var hasSpecificIncompleteOutputReason = standardFailures.Any(failure =>
            failure.Kind == StandardFailureKind.NumericalThresholdMissed &&
            failure.Detail == FocusHoldStandardMeasurements.ActiveDurationSeconds);

        return
        [
            .. standardFailures
                .Where(failure => failure.Kind != StandardFailureKind.OutputIncomplete ||
                    !hasSpecificIncompleteOutputReason)
                .Select(failure => StandardFailurePresentation(failure, loadVariables)),
            .. processing.FormalGateDecision?.BlockingFailures.Select(failure => failure.Detail) ?? [],
            .. processing.StabilizationOwnershipResult?.Failures.Select(failure => failure.Detail) ?? [],
            .. processing.DecayResult?.Failures.Select(failure => failure.Detail) ?? [],
            .. processing.TransferEligibilityResult?.Failures.Select(failure => failure.Detail) ?? [],
        ];
    }

    private static string StandardFailurePresentation(
        StandardEvaluationFailure failure,
        IReadOnlyList<LoadVariable> loadVariables)
    {
        if (failure.Kind == StandardFailureKind.OutputIncomplete)
        {
            return "The exercise was not completed.";
        }

        return failure.Detail switch
        {
            FocusHoldStandardMeasurements.ActiveDurationSeconds =>
                $"Hold ended before {DurationLabel(loadVariables)}.",
            FocusHoldStandardMeasurements.MarkedDriftCount => "More than 5 wanders were marked.",
            FocusHoldStandardMeasurements.UnreturnedDriftCount => "A wander was not followed by a return.",
            FocusHoldStandardMeasurements.LateReturnCount => "A return took longer than 10 seconds.",
            FocusHoldStandardMeasurements.TargetSubstitutionCount => "The target changed.",
            FocusHoldStandardMeasurements.TargetStatedBeforeSet => "The target was not confirmed before the hold.",
            _ => failure.Detail,
        };
    }

    private static string DurationLabel(IReadOnlyList<LoadVariable> loadVariables)
    {
        var value = LoadVariableValue(loadVariables, "duration", "3 minutes");

        var numberText = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!int.TryParse(numberText, out var amount))
        {
            return value;
        }

        if (value.Contains("minute", StringComparison.OrdinalIgnoreCase))
        {
            return $"{amount}:00";
        }

        return value.Contains("second", StringComparison.OrdinalIgnoreCase)
            ? $"{amount / 60}:{amount % 60:00}"
            : value;
    }

    private static string LoadVariableValue(
        IReadOnlyList<LoadVariable> loadVariables,
        string name,
        string fallback)
    {
        var value = loadVariables.FirstOrDefault(variable => string.Equals(
            variable.Name,
            name,
            StringComparison.OrdinalIgnoreCase))?.Value;
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static IReadOnlyList<TrainingPresentationReveal> RevealsFor(
        CurrentTrainingStateReadModel state)
    {
        var reveals = new List<TrainingPresentationReveal>
        {
            new(TrainingPresentationRevealKind.BranchLadder, state.BranchLevelStates.Count, "Branch ladder"),
            new(TrainingPresentationRevealKind.WeeklyPlan, state.AvailableNextWork.Count, "Weekly plan"),
        };

        AddReveal(reveals, TrainingPresentationRevealKind.BlockerDetails, state.BlockedAdvancement.Count, "Blocked advancement");
        AddReveal(reveals, TrainingPresentationRevealKind.MaintenanceDetails, state.DueMaintenance.Count, "Maintenance and decay");
        AddReveal(reveals, TrainingPresentationRevealKind.RecentSessions, state.RecentSessions.Count, "Recent sessions");
        AddReveal(reveals, TrainingPresentationRevealKind.EvidenceArtifacts, state.EvidenceSummaries.Count, "Practice records");
        AddReveal(reveals, TrainingPresentationRevealKind.GlobalReviewDetails, state.GlobalReview.Evaluation.Failures.Count, "Global review");

        return reveals;
    }

    private static IReadOnlyList<TrainingPresentationReveal> RevealsFor(
        NextTrainingWorkSelection selection)
    {
        var reveals = RevealsFor(selection.CurrentState).ToList();
        AddReveal(reveals, TrainingPresentationRevealKind.BlockerDetails, selection.Blockers.Count, "Next-work blockers");
        return reveals;
    }

    private static IReadOnlyList<TrainingPresentationReveal> RevealsFor(
        PreUiTrainingWorkflowPreparationResult preparation)
    {
        var reveals = new List<TrainingPresentationReveal>();
        AddReveal(reveals, TrainingPresentationRevealKind.BlockerDetails, preparation.Rejections.Count, "Setup blockers");
        AddReveal(reveals, TrainingPresentationRevealKind.GeneratedContentDetails, preparation.GeneratedContent is null ? 0 : 1, "Exercise material");
        AddReveal(
            reveals,
            TrainingPresentationRevealKind.RuntimeProtocolDetails,
            (preparation.RuntimeSession?.PhasePlan?.Phases.Count ?? 0) +
                (preparation.RuntimeSession?.CueSchedule?.Cues.Count ?? 0),
            "Session details");
        AddReveal(reveals, TrainingPresentationRevealKind.LocalPersistenceDetails, preparation.GeneratedInstanceRecord is null ? 0 : 1, "Saved exercise material");
        return reveals;
    }

    private static IReadOnlyList<TrainingPresentationReveal> RevealsFor(
        PreUiLiveSessionState live)
    {
        var reveals = new List<TrainingPresentationReveal>();
        AddReveal(reveals, TrainingPresentationRevealKind.RuntimeProtocolDetails, live.Commands.Count, "Available controls");
        AddReveal(reveals, TrainingPresentationRevealKind.EvidenceArtifacts, live.Evidence.EvidenceFactCount, "Recorded events");
        return reveals;
    }

    private static IReadOnlyList<TrainingPresentationReveal> RevealsFor(
        PreUiLiveSessionCompletionResult result)
    {
        var reveals = new List<TrainingPresentationReveal>();
        AddReveal(reveals, TrainingPresentationRevealKind.CoreEvaluationDetails, result.WorkflowResult is null ? 0 : 1, "Program result");
        AddReveal(reveals, TrainingPresentationRevealKind.EvidenceArtifacts, result.WorkflowResult?.ProcessingResult.EvidenceArtifacts.Count ?? 0, "Saved records");
        AddReveal(reveals, TrainingPresentationRevealKind.LocalPersistenceDetails, result.WorkflowResult is null ? 0 : 1, "Saved result");
        return reveals;
    }

    private static void AddReveal(
        ICollection<TrainingPresentationReveal> reveals,
        TrainingPresentationRevealKind kind,
        int count,
        string label)
    {
        if (count > 0)
        {
            reveals.Add(new TrainingPresentationReveal(kind, count, label));
        }
    }

    private static AppTrainingSessionType SessionTypeFor(
        WeeklySessionKind weeklySession,
        BranchLevelState branchLevelState)
    {
        return weeklySession switch
        {
            WeeklySessionKind.Load => AppTrainingSessionType.Load,
            WeeklySessionKind.TestOrStabilization =>
                branchLevelState is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing
                    ? AppTrainingSessionType.Stabilization
                    : AppTrainingSessionType.Test,
            WeeklySessionKind.TransferOrStabilization =>
                branchLevelState is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing
                    ? AppTrainingSessionType.Stabilization
                    : AppTrainingSessionType.Transfer,
            WeeklySessionKind.Transfer => AppTrainingSessionType.Transfer,
            WeeklySessionKind.Stabilization => AppTrainingSessionType.Stabilization,
            WeeklySessionKind.Maintenance => AppTrainingSessionType.Maintenance,
            WeeklySessionKind.Recovery
                or WeeklySessionKind.RecoveryOrLightMaintenance
                or WeeklySessionKind.OffOrRecovery
                or WeeklySessionKind.RecoveryOrRetest => AppTrainingSessionType.Recovery,
            _ => AppTrainingSessionType.Practice,
        };
    }

    private static TrainingBranchLevelPresentation BranchLevelFor(
        BranchCode branch,
        GlobalLevelId? level,
        BranchLevelState? state)
    {
        return new TrainingBranchLevelPresentation(
            branch,
            level,
            BranchName(branch),
            level.HasValue ? LevelName(level.Value) : null,
            state);
    }

    private static string BranchName(BranchCode branch)
    {
        return ProgramCatalog.Branches.Single(item => item.Code == branch).Name;
    }

    private static string CompactBranchName(BranchCode branch)
    {
        return branch switch
        {
            BranchCode.FH => "Focus Hold",
            BranchCode.FS => "Focus Shift",
            BranchCode.WM => "Working Memory",
            BranchCode.IR => "Inhibition",
            BranchCode.DE => "Discrimination",
            BranchCode.CO => "Concept Operations",
            BranchCode.AI => "Pressure",
            BranchCode.TI => "Transfer",
            _ => BranchName(branch),
        };
    }

    private static string LevelName(GlobalLevelId level)
    {
        return ProgramCatalog.GlobalLevels.Single(item => item.Id == level).Name;
    }

    private static DrillDefinition DrillFor(DrillId drill)
    {
        return ProgramCatalog.Drills.Single(item => item.Id == drill);
    }

    private static BranchLevelStandard StandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards.Single(item => item.Branch == branch && item.Level == level);
    }

    private static AppTrainingSessionType ToAppSessionType(SessionType sessionType)
    {
        return sessionType switch
        {
            SessionType.Practice => AppTrainingSessionType.Practice,
            SessionType.Load => AppTrainingSessionType.Load,
            SessionType.Test => AppTrainingSessionType.Test,
            SessionType.Stabilization => AppTrainingSessionType.Stabilization,
            SessionType.Regression => AppTrainingSessionType.Regression,
            SessionType.Transfer => AppTrainingSessionType.Transfer,
            SessionType.Recovery => AppTrainingSessionType.Recovery,
            _ => AppTrainingSessionType.Practice,
        };
    }

    private static bool IsAdvancementWork(AppTrainingSessionType sessionType)
    {
        return sessionType is AppTrainingSessionType.Load
            or AppTrainingSessionType.Test
            or AppTrainingSessionType.Stabilization
            or AppTrainingSessionType.Transfer;
    }

    private static bool HasExecutableStandard(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill)
    {
        return ExecutableTrainingStandards.Supports(branch, level, drill);
    }

    private static int BranchOrder(BranchCode branch)
    {
        return Array.IndexOf(Enum.GetValues<BranchCode>(), branch);
    }

    private static int LevelRank(GlobalLevelId level)
    {
        return ((int)level) + 1;
    }
}
