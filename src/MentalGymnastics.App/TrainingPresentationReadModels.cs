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
    Abandoned,
    TimedOut,
    Failed,
    NoAdvancement,
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
    IReadOnlyList<LoadVariable> LoadVariables);

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
    bool GrantsAdvancementInApp);

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
    bool ExpectedEvidenceComplete);

public sealed record LiveSessionPresentationReadModel(
    TrainingPresentationWorkSummary Work,
    RuntimeSessionLifecycleStatus LifecycleStatus,
    RuntimePhaseSchedulerStatus SchedulerStatus,
    RuntimeSessionPhaseKind? CurrentPhaseKind,
    RuntimeSessionPhaseCompletionRule? CurrentPhaseCompletionRule,
    PreUiLiveSessionTimerState Timer,
    LiveCuePresentationSummary? ActiveCue,
    IReadOnlyList<PreUiLiveSessionMaterialState> CurrentMaterials,
    LiveCommandPresentationSummary? PrimaryCommand,
    IReadOnlyList<LiveCommandPresentationSummary> AvailableCommands,
    LiveEvidencePresentationSummary Evidence,
    bool IsTerminal,
    IReadOnlyList<TrainingPresentationReveal> RevealOnDemand,
    bool GrantsAdvancementInApp);

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
    IReadOnlyList<TrainingPresentationReveal> RevealOnDemand,
    bool GrantsAdvancementInApp);

public static class TrainingPresentationMapper
{
    public static CurrentTrainingPresentationReadModel FromCurrentState(
        CurrentTrainingStateReadModel state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var maintenancePriority = MaintenanceDecayPriorityFor(state);
        var urgentBlocker = FirstBlocker(state);
        var primaryWork = FirstWeeklyWork(state);
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

        var work = preparation.Selection.SelectedWork is null
            ? null
            : WorkFor(preparation.Selection.SelectedWork, WorkSourceFor(preparation.Selection.Kind));
        var runtimeSession = preparation.RuntimeSession;
        var runtimeDefinition = runtimeSession?.SessionDefinition;
        var blockers = preparation.Selection.Blockers
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
            PrimaryCommandFor(availableCommands),
            availableCommands,
            EvidenceSummaryFor(live.Evidence),
            live.IsTerminal,
            RevealsFor(live),
            GrantsAdvancementInApp: false);
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

        return new ResultPresentationReadModel(
            outcome,
            PrimaryActionFor(result),
            PrimaryActionEnabled: result.IsProcessed || result.RuntimeCompletionStatus.HasValue,
            result.RuntimeCompletionStatus,
            WorkFor(result.SessionState, TrainingPresentationWorkSource.Result),
            result.IsProcessed,
            ProducesSuccessfulEvidence(result, processing),
            processing?.SessionHistory.CleanPerformance ?? false,
            stateTransition,
            processing?.FormalGateDecision?.Outcome,
            processing?.MaintenanceCurrencyResult?.State,
            processing?.FailureResponse?.Failure.Type,
            BlockingFailuresFor(processing),
            evidenceSummary,
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
        TrainingPresentationWorkSource source)
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
            selectedWork.LoadVariables);
    }

    private static TrainingPresentationWorkSummary WorkFor(
        PreUiLiveSessionState live,
        TrainingPresentationWorkSource source = TrainingPresentationWorkSource.LiveSession)
    {
        var drill = DrillFor(live.Drill);
        var standard = StandardFor(live.Branch, live.Level);

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
            []);
    }

    private static TrainingPresentationWorkSummary? FirstWeeklyWork(
        CurrentTrainingStateReadModel state)
    {
        var work = state.AvailableNextWork
            .OrderBy(item => item.DayNumber)
            .FirstOrDefault();
        if (work is null)
        {
            return null;
        }

        return new TrainingPresentationWorkSummary(
            TrainingPresentationWorkSource.WeeklyPlan,
            work.BranchEmphasis.Select(branch => BranchLevelFor(branch, level: null, state: null)).ToArray(),
            Drill: null,
            DrillCode: null,
            DrillLabel: null,
            SessionType: null,
            work.Session,
            work.IsAdvancementWork,
            work.IsAdvancementWork,
            Demand: null,
            Standard: null,
            HonestyConstraint: null,
            LoadVariables: []);
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
            evidence.EvidenceFactCount >= evidence.ExpectedEvidenceFactCount);
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

    private static LiveCommandPresentationSummary? PrimaryCommandFor(
        IReadOnlyList<LiveCommandPresentationSummary> commands)
    {
        return FirstCommand(commands, RuntimeInputCommandKind.RespondToCue)
            ?? FirstCommand(commands, RuntimeInputCommandKind.SubmitAnswer)
            ?? FirstCommand(commands, RuntimeInputCommandKind.FinishPhase)
            ?? FirstCommand(commands, RuntimeInputCommandKind.Resume)
            ?? FirstCommand(commands, RuntimeInputCommandKind.Pause)
            ?? commands.FirstOrDefault();
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
        CompletedRuntimeSessionProcessingResult? processing)
    {
        if (processing is null)
        {
            return [];
        }

        return
        [
            .. processing.StandardEvaluationResult?.Failures.Select(failure => failure.Detail) ?? [],
            .. processing.FormalGateDecision?.BlockingFailures.Select(failure => failure.Detail) ?? [],
            .. processing.StabilizationOwnershipResult?.Failures.Select(failure => failure.Detail) ?? [],
            .. processing.DecayResult?.Failures.Select(failure => failure.Detail) ?? [],
            .. processing.TransferEligibilityResult?.Failures.Select(failure => failure.Detail) ?? [],
        ];
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
        AddReveal(reveals, TrainingPresentationRevealKind.EvidenceArtifacts, state.EvidenceSummaries.Count, "Evidence artifacts");
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
        AddReveal(reveals, TrainingPresentationRevealKind.BlockerDetails, preparation.Rejections.Count, "Preparation blockers");
        AddReveal(reveals, TrainingPresentationRevealKind.GeneratedContentDetails, preparation.GeneratedContent is null ? 0 : 1, "Prepared content");
        AddReveal(
            reveals,
            TrainingPresentationRevealKind.RuntimeProtocolDetails,
            (preparation.RuntimeSession?.PhasePlan?.Phases.Count ?? 0) +
                (preparation.RuntimeSession?.CueSchedule?.Cues.Count ?? 0),
            "Runtime protocol");
        AddReveal(reveals, TrainingPresentationRevealKind.LocalPersistenceDetails, preparation.GeneratedInstanceRecord is null ? 0 : 1, "Stored drill instance");
        return reveals;
    }

    private static IReadOnlyList<TrainingPresentationReveal> RevealsFor(
        PreUiLiveSessionState live)
    {
        var reveals = new List<TrainingPresentationReveal>();
        AddReveal(reveals, TrainingPresentationRevealKind.RuntimeProtocolDetails, live.Commands.Count, "Available commands");
        AddReveal(reveals, TrainingPresentationRevealKind.EvidenceArtifacts, live.Evidence.EvidenceFactCount, "Runtime evidence facts");
        return reveals;
    }

    private static IReadOnlyList<TrainingPresentationReveal> RevealsFor(
        PreUiLiveSessionCompletionResult result)
    {
        var reveals = new List<TrainingPresentationReveal>();
        AddReveal(reveals, TrainingPresentationRevealKind.CoreEvaluationDetails, result.WorkflowResult is null ? 0 : 1, "Core evaluation");
        AddReveal(reveals, TrainingPresentationRevealKind.EvidenceArtifacts, result.WorkflowResult?.ProcessingResult.EvidenceArtifacts.Count ?? 0, "Saved evidence");
        AddReveal(reveals, TrainingPresentationRevealKind.LocalPersistenceDetails, result.WorkflowResult is null ? 0 : 1, "Persisted result");
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

    private static int BranchOrder(BranchCode branch)
    {
        return Array.IndexOf(Enum.GetValues<BranchCode>(), branch);
    }

    private static int LevelRank(GlobalLevelId level)
    {
        return ((int)level) + 1;
    }
}
