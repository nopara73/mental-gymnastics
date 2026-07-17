using System.Globalization;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App;

public enum DailyTrainingWorkflowStatus
{
    Ready,
    Prepared,
    Active,
    BetweenBlocks,
    Done,
    Stopped,
    OffDay,
}

public enum DailyTrainingInterruptionReconciliationStatus
{
    NoChange,
    PreparedReset,
    ActiveResumable,
    Abandoned,
}

public sealed record DailyTrainingInterruptionReconciliationResult(
    DailyTrainingInterruptionReconciliationStatus Status,
    DailyTrainingWorkflowReadModel? DailyTraining,
    string? SessionId,
    string Detail);

public sealed record DailyGlobalReviewCompletionResult(
    DailyTrainingWorkflowReadModel DailyTraining,
    CurrentGlobalReviewReadModel GlobalReview,
    LocalEvidenceArtifactRecord EvidenceArtifact);

public sealed record DailyTrainingBlockReadModel(
    LocalDailyTrainingBlockRecord Record,
    AppTrainingSessionType SessionType)
{
    public int EstimatedMinutes => TrainingDoseDurationEstimator.RoundedMinutes(
        Record.Drill,
        Record.LoadVariables);

    public RequestedTrainingWork RequestedWork => new(
        Record.Branch,
        Record.Level,
        Record.Drill,
        SessionType,
        Record.LoadVariables);
}

public sealed class DailyTrainingWorkflowReadModel
{
    internal DailyTrainingWorkflowReadModel(LocalDailyTrainingPrescriptionRecord prescription)
    {
        ArgumentNullException.ThrowIfNull(prescription);

        Prescription = prescription;
        Blocks = prescription.Blocks
            .Select(block => new DailyTrainingBlockReadModel(block, SessionTypeFor(block.Role)))
            .ToArray();
        CurrentBlock = Blocks.FirstOrDefault(block => block.Record.State is
            LocalDailyTrainingBlockState.Active or LocalDailyTrainingBlockState.Prepared)
            ?? Blocks.FirstOrDefault(block => block.Record.State == LocalDailyTrainingBlockState.Planned);
        CompletedBlockCount = prescription.Blocks.Count(block => block.IsTerminal);
        SkippedBlockCount = prescription.Blocks.Count(block =>
            block.State == LocalDailyTrainingBlockState.Skipped);
        Status = StatusFor(prescription);
    }

    public LocalDailyTrainingPrescriptionRecord Prescription { get; }

    public IReadOnlyList<DailyTrainingBlockReadModel> Blocks { get; }

    public DailyTrainingBlockReadModel? CurrentBlock { get; }

    public int CompletedBlockCount { get; }

    public int SkippedBlockCount { get; }

    public int TotalBlockCount => Blocks.Count;

    public int EstimatedMinutes => Blocks.Sum(block => block.EstimatedMinutes);

    public DailyTrainingWorkflowStatus Status { get; }

    public bool CanPrepare =>
        Status is DailyTrainingWorkflowStatus.Ready or DailyTrainingWorkflowStatus.BetweenBlocks;

    public bool IsTerminal =>
        Status is DailyTrainingWorkflowStatus.Done or
            DailyTrainingWorkflowStatus.Stopped or
            DailyTrainingWorkflowStatus.OffDay;

    private static DailyTrainingWorkflowStatus StatusFor(
        LocalDailyTrainingPrescriptionRecord prescription)
    {
        if (prescription.Blocks.Count == 0)
        {
            return DailyTrainingWorkflowStatus.OffDay;
        }

        if (prescription.State == DailyTrainingDoseState.Completed)
        {
            return DailyTrainingWorkflowStatus.Done;
        }

        if (prescription.State == DailyTrainingDoseState.Stopped)
        {
            return DailyTrainingWorkflowStatus.Stopped;
        }

        if (prescription.Blocks.Any(block => block.State == LocalDailyTrainingBlockState.Active))
        {
            return DailyTrainingWorkflowStatus.Active;
        }

        if (prescription.Blocks.Any(block => block.State == LocalDailyTrainingBlockState.Prepared))
        {
            return DailyTrainingWorkflowStatus.Prepared;
        }

        return prescription.Blocks.Any(block => block.IsTerminal)
            ? DailyTrainingWorkflowStatus.BetweenBlocks
            : DailyTrainingWorkflowStatus.Ready;
    }

    private static AppTrainingSessionType SessionTypeFor(LocalDailyTrainingBlockRole role)
    {
        return role switch
        {
            LocalDailyTrainingBlockRole.Practice => AppTrainingSessionType.Practice,
            LocalDailyTrainingBlockRole.Load => AppTrainingSessionType.Load,
            LocalDailyTrainingBlockRole.Test => AppTrainingSessionType.Test,
            LocalDailyTrainingBlockRole.Stabilization => AppTrainingSessionType.Stabilization,
            LocalDailyTrainingBlockRole.Maintenance => AppTrainingSessionType.Maintenance,
            LocalDailyTrainingBlockRole.Regression => AppTrainingSessionType.Regression,
            LocalDailyTrainingBlockRole.Transfer => AppTrainingSessionType.Transfer,
            LocalDailyTrainingBlockRole.Recovery => AppTrainingSessionType.Recovery,
            LocalDailyTrainingBlockRole.Review => AppTrainingSessionType.Test,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown daily block role."),
        };
    }
}

public sealed class DailyTrainingWorkflowService
{
    private readonly CurrentTrainingStateLoader stateLoader;
    private readonly LocalDailyTrainingPrescriptionStore prescriptionStore;
    private readonly LocalActiveRuntimeSessionSnapshotStore activeSnapshotStore;
    private readonly LocalGeneratedDrillInstanceStore generatedInstanceStore;
    private readonly LocalProgrammingEventTransaction transaction;

    public DailyTrainingWorkflowService(AppStartupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        stateLoader = new CurrentTrainingStateLoader(configuration);
        prescriptionStore = new LocalDailyTrainingPrescriptionStore(configuration.LocalDatabaseOptions);
        activeSnapshotStore = new LocalActiveRuntimeSessionSnapshotStore(configuration.LocalDatabaseOptions);
        generatedInstanceStore = new LocalGeneratedDrillInstanceStore(configuration.LocalDatabaseOptions);
        transaction = new LocalProgrammingEventTransaction(configuration.LocalDatabaseOptions);
    }

    public async ValueTask<DailyTrainingInterruptionReconciliationResult> ReconcileInterruptedStateAsync(
        TrainingDate date,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prescription = await prescriptionStore.LoadByDateAsync(date, cancellationToken)
            .ConfigureAwait(false);
        var block = prescription?.Blocks.FirstOrDefault(candidate => candidate.State is
            LocalDailyTrainingBlockState.Prepared or LocalDailyTrainingBlockState.Active);
        if (prescription is null || block?.SessionId is null)
        {
            return new DailyTrainingInterruptionReconciliationResult(
                DailyTrainingInterruptionReconciliationStatus.NoChange,
                prescription is null ? null : From(prescription),
                SessionId: null,
                "No interrupted daily work requires reconciliation.");
        }

        IReadOnlyList<LocalActiveRuntimeSessionSnapshotRecord> snapshots;
        var snapshotStoreUnreadable = false;
        try
        {
            snapshots = await activeSnapshotStore.ListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            snapshots = [];
            snapshotStoreUnreadable = true;
        }

        var snapshot = snapshots.FirstOrDefault(candidate => string.Equals(
            candidate.SessionId,
            block.SessionId,
            StringComparison.Ordinal));
        if (block.State == LocalDailyTrainingBlockState.Prepared &&
            snapshot is not null && SnapshotEnteredWork(snapshot))
        {
            return await AbandonInterruptedSessionAsync(
                date,
                block.SessionId,
                "The app closed after work began but before the daily block became active.",
                clearAllSnapshots: snapshotStoreUnreadable,
                cancellationToken).ConfigureAwait(false);
        }

        if (block.State == LocalDailyTrainingBlockState.Prepared)
        {
            var reset = ResetPreparedBlock(prescription, block);
            var generated = await GeneratedForSessionAsync(block.SessionId, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(context =>
            {
                context.SaveDailyTrainingPrescription(reset);
                if (generated is not null)
                {
                    context.SaveGeneratedDrillInstance(RetireGeneratedInstance(generated));
                }

                if (snapshotStoreUnreadable)
                {
                    context.ClearActiveRuntimeSessionSnapshots();
                }
                else
                {
                    context.DeleteActiveRuntimeSessionSnapshot(block.SessionId);
                }

                return ValueTask.CompletedTask;
            }, cancellationToken).ConfigureAwait(false);

            return new DailyTrainingInterruptionReconciliationResult(
                DailyTrainingInterruptionReconciliationStatus.PreparedReset,
                From(reset),
                block.SessionId,
                "Interrupted setup was reset; no training attempt was recorded.");
        }

        if (snapshot is not null && !snapshotStoreUnreadable)
        {
            return new DailyTrainingInterruptionReconciliationResult(
                DailyTrainingInterruptionReconciliationStatus.ActiveResumable,
                From(prescription),
                block.SessionId,
                "Active work has a persisted runtime snapshot and may be resumed.");
        }

        return await AbandonInterruptedSessionAsync(
            date,
            block.SessionId,
            snapshotStoreUnreadable
                ? "The active runtime snapshot could not be read safely."
                : "The active runtime snapshot is missing.",
            clearAllSnapshots: snapshotStoreUnreadable,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<DailyTrainingInterruptionReconciliationResult> AbandonInterruptedSessionAsync(
        TrainingDate date,
        string sessionId,
        string reason,
        bool clearAllSnapshots = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Interrupted runtime session id is required.", nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Interrupted runtime session reason is required.", nameof(reason));
        }

        var prescription = await prescriptionStore.LoadByDateAsync(date, cancellationToken)
            .ConfigureAwait(false);
        var block = prescription?.Blocks.FirstOrDefault(candidate => string.Equals(
            candidate.SessionId,
            sessionId,
            StringComparison.Ordinal));
        if (prescription is null || block is null || block.IsTerminal)
        {
            return new DailyTrainingInterruptionReconciliationResult(
                DailyTrainingInterruptionReconciliationStatus.NoChange,
                prescription is null ? null : From(prescription),
                sessionId,
                "The interrupted session is no longer active in this daily prescription.");
        }

        if (block.State is not LocalDailyTrainingBlockState.Prepared and not LocalDailyTrainingBlockState.Active)
        {
            throw new InvalidOperationException("Only prepared or active daily work can be reconciled as interrupted.");
        }

        var updated = ApplyInterruptedAbandonment(prescription, block);
        var artifact = InterruptedArtifact(block, date, sessionId, reason);
        var completedSession = InterruptedSession(block, date, sessionId, reason, artifact.ArtifactId);
        var generated = await GeneratedForSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(context =>
        {
            context.SaveEvidenceArtifact(artifact);
            context.SaveCompletedSession(completedSession);
            context.SaveDailyTrainingPrescription(updated);
            if (generated is not null)
            {
                context.SaveGeneratedDrillInstance(RetireGeneratedInstance(generated));
            }

            if (clearAllSnapshots)
            {
                context.ClearActiveRuntimeSessionSnapshots();
            }
            else
            {
                context.DeleteActiveRuntimeSessionSnapshot(sessionId);
            }

            return ValueTask.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);

        return new DailyTrainingInterruptionReconciliationResult(
            DailyTrainingInterruptionReconciliationStatus.Abandoned,
            From(updated),
            sessionId,
            "Interrupted active work was recorded as abandoned and today's remaining blocks were stopped.");
    }

    public async ValueTask<DailyTrainingWorkflowReadModel> LoadOrCreateAsync(
        TrainingDate date,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await prescriptionStore.LoadByDateAsync(date, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return await CompleteAutomaticReviewIfDueAsync(date, existing, cancellationToken)
                .ConfigureAwait(false);
        }

        var currentState = await stateLoader.LoadAsync(
            new CurrentTrainingStateQuery(date),
            cancellationToken).ConfigureAwait(false);
        var latest = await prescriptionStore.LoadLatestAsync(cancellationToken).ConfigureAwait(false);
        var cycleAnchor = latest is not null && latest.CycleAnchor.DaysUntil(date) >= 0
            ? latest.CycleAnchor
            : date;
        var daily = DailyTrainingProgrammingPlanner.Prescribe(
            date,
            cycleAnchor,
            currentState.WeeklyPlan);
        var blocks = BuildBlocks(daily, currentState);
        var record = new LocalDailyTrainingPrescriptionRecord(
            PrescriptionIdFor(date),
            date,
            cycleAnchor,
            daily.CycleDay,
            daily.Session,
            blocks.Count == 0
                ? DailyTrainingDoseState.Completed
                : DailyTrainingDoseState.Planned,
            blocks);

        await prescriptionStore.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        return await CompleteAutomaticReviewIfDueAsync(date, record, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DailyTrainingWorkflowReadModel> CompleteAutomaticReviewIfDueAsync(
        TrainingDate date,
        LocalDailyTrainingPrescriptionRecord record,
        CancellationToken cancellationToken)
    {
        var review = record.Blocks.SingleOrDefault(block =>
            block.Role == LocalDailyTrainingBlockRole.Review &&
            block.State == LocalDailyTrainingBlockState.Planned);
        if (review is null || record.Blocks.Any(block => block.State is
                LocalDailyTrainingBlockState.Prepared or LocalDailyTrainingBlockState.Active))
        {
            return From(record);
        }

        var completed = await CompleteDueGlobalReviewAsync(
            date,
            review.BlockId,
            cancellationToken).ConfigureAwait(false);
        return completed.DailyTraining;
    }

    public async ValueTask<DailyGlobalReviewCompletionResult> CompleteDueGlobalReviewAsync(
        TrainingDate date,
        string blockId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            throw new ArgumentException("Daily review block id is required.", nameof(blockId));
        }

        var current = await RequiredByDateAsync(date, cancellationToken).ConfigureAwait(false);
        var block = current.Blocks.SingleOrDefault(item => item.BlockId == blockId)
            ?? throw new InvalidOperationException("The daily review block does not exist.");
        if (block.Role != LocalDailyTrainingBlockRole.Review ||
            block.State != LocalDailyTrainingBlockState.Planned ||
            current.Blocks.Any(item => item.State is
                LocalDailyTrainingBlockState.Prepared or LocalDailyTrainingBlockState.Active))
        {
            throw new InvalidOperationException("Only the current unstarted review block can be completed.");
        }

        var state = await stateLoader.LoadAsync(
            new CurrentTrainingStateQuery(date),
            cancellationToken).ConfigureAwait(false);
        if (!state.GlobalReview.Cadence.IsDue)
        {
            throw new InvalidOperationException("The global review cadence is not due on this program date.");
        }

        var eventId = $"global-review-{DateId(date)}";
        var evaluation = state.GlobalReview.Evaluation;
        var failureSummary = evaluation.Failures.Count == 0
            ? "none"
            : string.Join(" | ", evaluation.Failures.Select(failure => failure.Detail));
        var decisionSummary = evaluation.Decisions.Count == 0
            ? "no programming decision"
            : string.Join(" | ", evaluation.Decisions.Select(decision => decision.Detail));
        var summary = evaluation.Passed
            ? $"Global review passed. {decisionSummary}."
            : $"Global review recorded {evaluation.Failures.Count} blocker(s): {failureSummary}. {decisionSummary}.";
        var artifact = new LocalEvidenceArtifactRecord(
            $"{eventId}-artifact",
            new LocalProgrammingEventReference(eventId, LocalProgrammingEventKind.GlobalReview),
            new EvidenceArtifact(
                EvidenceArtifactCategory.GlobalReview,
                date,
                [new ObservableEvidence(ObservableEvidenceKind.GlobalReviewSummary, summary)],
                summary));
        var completedSession = new LocalSessionHistoryRecord(
            eventId,
            date,
            LocalCompletedSessionType.Review,
            [new LocalSessionBranchLevel(block.Branch, block.Level)],
            block.Drill,
            transferTask: null,
            LocalSessionIntensity.Low,
            block.LoadVariables,
            cleanPerformance: evaluation.Passed,
            notes: summary,
            recoveryMarked: false,
            deloadMarked: state.DeloadDecision.ShouldDeload,
            evidenceArtifactIds: [artifact.ArtifactId]);
        var blocks = current.Blocks.Select(item => item.BlockId == block.BlockId
            ? CopyBlock(
                item,
                evaluation.Passed
                    ? LocalDailyTrainingBlockState.Completed
                    : LocalDailyTrainingBlockState.Failed,
                eventId)
            : item.IsTerminal
                ? item
                : CopyBlock(
                    item,
                    LocalDailyTrainingBlockState.Skipped,
                    sessionId: null)).ToArray();
        var updated = CopyPrescription(current, DailyTrainingDoseState.Completed, blocks);
        await transaction.CommitAsync(context =>
        {
            context.SaveEvidenceArtifact(artifact);
            context.SaveCompletedSession(completedSession);
            context.SaveDailyTrainingPrescription(updated);
            return ValueTask.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);

        return new DailyGlobalReviewCompletionResult(From(updated), state.GlobalReview, artifact);
    }

    public async ValueTask<DailyTrainingWorkflowReadModel> MarkPreparedAsync(
        TrainingDate date,
        string blockId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            throw new ArgumentException("Daily block id is required.", nameof(blockId));
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session id is required.", nameof(sessionId));
        }

        var current = await RequiredByDateAsync(date, cancellationToken).ConfigureAwait(false);
        if (current.IsTerminal || current.Blocks.Any(block => block.State is
                LocalDailyTrainingBlockState.Prepared or LocalDailyTrainingBlockState.Active))
        {
            throw new InvalidOperationException("The daily prescription cannot prepare another block now.");
        }

        var next = current.Blocks.FirstOrDefault(block =>
            block.State == LocalDailyTrainingBlockState.Planned)
            ?? throw new InvalidOperationException("The daily prescription has no remaining block to prepare.");
        if (!string.Equals(next.BlockId, blockId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Daily blocks must be prepared in prescribed order.");
        }

        var updated = ReplaceBlock(
            current,
            next,
            LocalDailyTrainingBlockState.Prepared,
            sessionId,
            doseState: current.State);
        await prescriptionStore.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return From(updated);
    }

    public async ValueTask<DailyTrainingWorkflowReadModel> MarkActiveAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var current = await RequiredBySessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var block = current.Blocks.Single(item =>
            string.Equals(item.SessionId, sessionId, StringComparison.Ordinal));
        if (block.State != LocalDailyTrainingBlockState.Prepared)
        {
            throw new InvalidOperationException("Only a prepared daily block can start active work.");
        }

        var updated = ReplaceBlock(
            current,
            block,
            LocalDailyTrainingBlockState.Active,
            sessionId,
            DailyTrainingDoseState.Active);
        await prescriptionStore.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return From(updated);
    }

    public async ValueTask<DailyTrainingWorkflowReadModel> CancelPreparedAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var current = await RequiredBySessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var block = current.Blocks.Single(item =>
            string.Equals(item.SessionId, sessionId, StringComparison.Ordinal));
        if (block.State != LocalDailyTrainingBlockState.Prepared)
        {
            throw new InvalidOperationException("Only setup that has not entered active work can be cancelled.");
        }

        var updated = ReplaceBlock(
            current,
            block,
            LocalDailyTrainingBlockState.Planned,
            sessionId: null,
            doseState: current.State);
        await prescriptionStore.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return From(updated);
    }

    public async ValueTask<DailyTrainingWorkflowReadModel> StopRemainingAsync(
        TrainingDate date,
        CancellationToken cancellationToken = default)
    {
        var current = await RequiredByDateAsync(date, cancellationToken).ConfigureAwait(false);
        if (current.IsTerminal)
        {
            return From(current);
        }

        if (current.Blocks.Any(block => block.State == LocalDailyTrainingBlockState.Active))
        {
            throw new InvalidOperationException("Active work must be abandoned through the runtime before stopping the day.");
        }

        var blocks = current.Blocks.Select(block => block.IsTerminal
            ? block
            : CopyBlock(
                block,
                LocalDailyTrainingBlockState.Skipped,
                sessionId: null)).ToArray();
        var updated = CopyPrescription(current, DailyTrainingDoseState.Stopped, blocks);
        await prescriptionStore.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return From(updated);
    }

    internal static LocalDailyTrainingPrescriptionRecord ApplyTerminalResult(
        LocalDailyTrainingPrescriptionRecord prescription,
        string sessionId,
        RuntimeSessionCompletionStatus completionStatus,
        StandardEvaluationResult? standardResult)
    {
        ArgumentNullException.ThrowIfNull(prescription);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session id is required.", nameof(sessionId));
        }

        var block = prescription.Blocks.SingleOrDefault(item =>
            string.Equals(item.SessionId, sessionId, StringComparison.Ordinal));
        if (block is null)
        {
            throw new InvalidOperationException("The completed runtime session is not part of this daily prescription.");
        }

        if (block.State != LocalDailyTrainingBlockState.Active)
        {
            throw new InvalidOperationException("Only active daily work can become a terminal attempt.");
        }

        if (completionStatus == RuntimeSessionCompletionStatus.Abandoned)
        {
            var stoppedBlocks = prescription.Blocks.Select(item =>
                item.BlockId == block.BlockId
                    ? CopyBlock(
                        item,
                        LocalDailyTrainingBlockState.Abandoned,
                        sessionId)
                    : item.IsTerminal
                        ? item
                        : CopyBlock(
                            item,
                            LocalDailyTrainingBlockState.Skipped,
                            sessionId: null))
                .ToArray();
            return CopyPrescription(prescription, DailyTrainingDoseState.Stopped, stoppedBlocks);
        }

        var terminalState = completionStatus switch
        {
            RuntimeSessionCompletionStatus.Completed when standardResult?.Passed is not false =>
                LocalDailyTrainingBlockState.Completed,
            RuntimeSessionCompletionStatus.Completed or RuntimeSessionCompletionStatus.Failed =>
                LocalDailyTrainingBlockState.Failed,
            RuntimeSessionCompletionStatus.TimedOut => LocalDailyTrainingBlockState.TimedOut,
            _ => throw new ArgumentOutOfRangeException(
                nameof(completionStatus),
                completionStatus,
                "Unknown terminal runtime outcome."),
        };
        var blocks = prescription.Blocks.Select(item => item.BlockId == block.BlockId
            ? CopyBlock(item, terminalState, sessionId)
            : item).ToArray();
        var nextState = blocks.All(item => item.IsTerminal)
            ? DailyTrainingDoseState.Completed
            : DailyTrainingDoseState.Active;
        return CopyPrescription(prescription, nextState, blocks);
    }

    internal static DailyTrainingWorkflowReadModel From(
        LocalDailyTrainingPrescriptionRecord prescription) => new(prescription);

    private async ValueTask<LocalGeneratedDrillInstanceRecord?> GeneratedForSessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var records = await generatedInstanceStore.ListAsync(cancellationToken).ConfigureAwait(false);
        return records.SingleOrDefault(record =>
            record.State == LocalGeneratedDrillInstanceState.InSession &&
            string.Equals(record.ActiveSessionId, sessionId, StringComparison.Ordinal));
    }

    private static bool SnapshotEnteredWork(LocalActiveRuntimeSessionSnapshotRecord snapshot)
    {
        return snapshot.RuntimeEvents.Any(runtimeEvent =>
            string.Equals(runtimeEvent.Kind, RuntimeEventKind.PhaseStarted.ToString(), StringComparison.Ordinal) &&
            runtimeEvent.PhaseKind is not null &&
            !string.Equals(
                runtimeEvent.PhaseKind,
                RuntimeSessionPhaseKind.InstructionPrep.ToString(),
                StringComparison.Ordinal));
    }

    private static LocalDailyTrainingPrescriptionRecord ResetPreparedBlock(
        LocalDailyTrainingPrescriptionRecord prescription,
        LocalDailyTrainingBlockRecord block)
    {
        var blocks = prescription.Blocks.Select(candidate => candidate.BlockId == block.BlockId
            ? CopyBlock(
                candidate,
                LocalDailyTrainingBlockState.Planned,
                sessionId: null)
            : candidate).ToArray();
        return CopyPrescription(prescription, DailyTrainingDoseState.Planned, blocks);
    }

    private static LocalDailyTrainingPrescriptionRecord ApplyInterruptedAbandonment(
        LocalDailyTrainingPrescriptionRecord prescription,
        LocalDailyTrainingBlockRecord interrupted)
    {
        var blocks = prescription.Blocks.Select(block => block.BlockId == interrupted.BlockId
            ? CopyBlock(
                block,
                LocalDailyTrainingBlockState.Abandoned,
                interrupted.SessionId)
            : block.IsTerminal
                ? block
                : CopyBlock(
                    block,
                    LocalDailyTrainingBlockState.Skipped,
                    sessionId: null)).ToArray();
        return CopyPrescription(prescription, DailyTrainingDoseState.Stopped, blocks);
    }

    private static LocalGeneratedDrillInstanceRecord RetireGeneratedInstance(
        LocalGeneratedDrillInstanceRecord generated)
    {
        return new LocalGeneratedDrillInstanceRecord(
            generated.InstanceId,
            generated.GeneratedOn,
            generated.Branch,
            generated.Level,
            generated.Drill,
            generated.LoadVariables,
            generated.ContentIdentity,
            LocalGeneratedDrillInstanceState.Abandoned,
            activeSessionId: null,
            resultEvidenceArtifactId: null,
            generated.ContentSummary,
            generated.FreshnessPolicy,
            generated.AuditMaterials);
    }

    private static LocalEvidenceArtifactRecord InterruptedArtifact(
        LocalDailyTrainingBlockRecord block,
        TrainingDate date,
        string sessionId,
        string reason)
    {
        var (eventKind, category) = EvidenceIdentityFor(block.Role);
        return new LocalEvidenceArtifactRecord(
            $"{sessionId}-interruption-evidence",
            new LocalProgrammingEventReference(
                sessionId,
                eventKind,
                block.Branch,
                block.Level,
                block.Drill),
            new EvidenceArtifact(
                category,
                date,
                [
                    new ObservableEvidence(
                        ObservableEvidenceKind.ErrorCount,
                        $"Interrupted active work produced no evaluable result. Reason: {reason.Trim()}"),
                ],
                "Interrupted attempt record."));
    }

    private static LocalSessionHistoryRecord InterruptedSession(
        LocalDailyTrainingBlockRecord block,
        TrainingDate date,
        string sessionId,
        string reason,
        string artifactId)
    {
        var sessionType = CompletedSessionTypeFor(block.Role);
        return new LocalSessionHistoryRecord(
            sessionId,
            date,
            sessionType,
            [new LocalSessionBranchLevel(block.Branch, block.Level)],
            block.Drill,
            sessionType == LocalCompletedSessionType.Transfer
                ? TransferTestCatalog.TransferTests.Single(test =>
                    test.SourceBranch == block.Branch).TransferTask
                : null,
            LocalSessionIntensity.Low,
            block.LoadVariables,
            cleanPerformance: false,
            $"Interrupted attempt: {reason.Trim()}",
            recoveryMarked: false,
            deloadMarked: false,
            [artifactId]);
    }

    private static (LocalProgrammingEventKind EventKind, EvidenceArtifactCategory Category) EvidenceIdentityFor(
        LocalDailyTrainingBlockRole role)
    {
        return role switch
        {
            LocalDailyTrainingBlockRole.Load =>
                (LocalProgrammingEventKind.Load, EvidenceArtifactCategory.Load),
            LocalDailyTrainingBlockRole.Test or LocalDailyTrainingBlockRole.Review =>
                (LocalProgrammingEventKind.FormalTest, EvidenceArtifactCategory.Test),
            LocalDailyTrainingBlockRole.Stabilization =>
                (LocalProgrammingEventKind.Stabilization, EvidenceArtifactCategory.Stabilization),
            LocalDailyTrainingBlockRole.Transfer =>
                (LocalProgrammingEventKind.Transfer, EvidenceArtifactCategory.Transfer),
            LocalDailyTrainingBlockRole.Maintenance =>
                (LocalProgrammingEventKind.Maintenance, EvidenceArtifactCategory.Maintenance),
            _ => (LocalProgrammingEventKind.Practice, EvidenceArtifactCategory.Practice),
        };
    }

    private static LocalCompletedSessionType CompletedSessionTypeFor(LocalDailyTrainingBlockRole role)
    {
        return role switch
        {
            LocalDailyTrainingBlockRole.Practice => LocalCompletedSessionType.Practice,
            LocalDailyTrainingBlockRole.Load => LocalCompletedSessionType.Load,
            LocalDailyTrainingBlockRole.Test => LocalCompletedSessionType.Test,
            LocalDailyTrainingBlockRole.Review => LocalCompletedSessionType.Review,
            LocalDailyTrainingBlockRole.Stabilization => LocalCompletedSessionType.Stabilization,
            LocalDailyTrainingBlockRole.Maintenance => LocalCompletedSessionType.Maintenance,
            LocalDailyTrainingBlockRole.Regression => LocalCompletedSessionType.Regression,
            LocalDailyTrainingBlockRole.Transfer => LocalCompletedSessionType.Transfer,
            LocalDailyTrainingBlockRole.Recovery => LocalCompletedSessionType.Recovery,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown daily block role."),
        };
    }

    private static IReadOnlyList<LocalDailyTrainingBlockRecord> BuildBlocks(
        DailyTrainingPrescription daily,
        CurrentTrainingStateReadModel currentState)
    {
        var candidates = SelectCandidates(daily, currentState);
        return candidates.Select((candidate, index) =>
        {
            var appSessionType = candidate.ForcedSessionType ??
                DefaultTrainingWorkPolicy.SessionTypeFor(
                    daily.Session,
                    candidate.Status.State,
                    candidate.Status.Level);
            var profile = TrainingLoadProfileCatalog.Get(
                candidate.Status.Branch,
                candidate.Status.Level);
            var history = currentState.RecentSessions
                .Where(session =>
                    (session.SessionType is LocalCompletedSessionType.Practice or
                        LocalCompletedSessionType.Load) &&
                    session.Drill == profile.Drill &&
                    session.BranchLevels.Contains(new LocalSessionBranchLevel(
                        candidate.Status.Branch,
                        candidate.Status.Level)))
                .OrderBy(session => session.Date.Year)
                .ThenBy(session => session.Date.Month)
                .ThenBy(session => session.Date.Day)
                .Select(session => new TrainingLoadHistoryEntry(
                    session.LoadVariables,
                    session.CleanPerformance,
                    Overload: !session.CleanPerformance))
                .ToArray();
            var load = ProgressiveLoadPlanner.Prescribe(
                profile,
                DefaultTrainingWorkPolicy.CoreSessionTypeFor(appSessionType),
                history);

            return new LocalDailyTrainingBlockRecord(
                BlockIdFor(daily.Date, index + 1),
                index + 1,
                candidate.Status.Branch,
                candidate.Status.Level,
                profile.Drill,
                candidate.ForcedRole ?? RoleFor(appSessionType),
                load.Stage.LoadVariables,
                LocalDailyTrainingBlockState.Planned);
        }).ToArray();
    }

    private static IReadOnlyList<DailyBlockCandidate> SelectCandidates(
        DailyTrainingPrescription daily,
        CurrentTrainingStateReadModel currentState)
    {
        if (currentState.DueMaintenance.Count > 0)
        {
            return currentState.DueMaintenance
                .Take(3)
                .Select(item => new DailyBlockCandidate(
                    item.BranchLevel,
                    AppTrainingSessionType.Maintenance))
                .DistinctBy(candidate => (candidate.Status.Branch, candidate.Status.Level))
                .ToArray();
        }

        if (currentState.RecoveryRequired)
        {
            var recoveryBranch = currentState.RecoveryDecision?.Branch ??
                currentState.ProgressRecords.LatestSummary?.BottleneckBranch ??
                BranchCode.FH;
            var recoveryStatus = DefaultTrainingWorkPolicy.SelectStatus(
                    currentState.CurrentPractitionerState,
                    recoveryBranch) ??
                currentState.CurrentPractitionerState.BranchLevels
                    .Where(status => status.State != BranchLevelState.Unopened)
                    .OrderBy(status => status.Branch)
                    .ThenByDescending(status => (int)status.Level)
                    .First();
            return [new DailyBlockCandidate(recoveryStatus, AppTrainingSessionType.Recovery)];
        }

        if (currentState.GlobalReview.Cadence.IsDue)
        {
            var reviewBranch = currentState.GlobalReview.Input.Bottleneck?.Branch ??
                currentState.ProgressRecords.LatestSummary?.BottleneckBranch ??
                BranchCode.FH;
            var reviewStatus = DefaultTrainingWorkPolicy.SelectStatus(
                    currentState.CurrentPractitionerState,
                    reviewBranch) ??
                currentState.CurrentPractitionerState.BranchLevels
                    .Where(status => status.State != BranchLevelState.Unopened)
                    .OrderBy(status => status.Branch)
                    .ThenByDescending(status => (int)status.Level)
                    .First();
            return
            [
                new DailyBlockCandidate(
                    reviewStatus,
                    AppTrainingSessionType.Test,
                    LocalDailyTrainingBlockRole.Review),
            ];
        }

        if (daily.IsOff)
        {
            return [];
        }

        if (daily.Session == WeeklySessionKind.RecoveryOrRetest)
        {
            var retest = currentState.CurrentPractitionerState.BranchLevels
                .Where(status => status.State is
                    BranchLevelState.PassedOnce or
                    BranchLevelState.Stabilizing or
                    BranchLevelState.TestReady)
                .OrderBy(status => status.State is
                    BranchLevelState.PassedOnce or BranchLevelState.Stabilizing ? 0 : 1)
                .ThenBy(status => status.Level)
                .ThenBy(status => status.Branch)
                .FirstOrDefault();
            if (retest != default)
            {
                return [new DailyBlockCandidate(retest, ForcedSessionType: null)];
            }
        }

        var candidates = daily.BranchEmphasis
            .Select(branch => DefaultTrainingWorkPolicy.SelectStatus(
                currentState.CurrentPractitionerState,
                branch))
            .OfType<BranchLevelStatus>()
            .Select(status => new DailyBlockCandidate(status, ForcedSessionType: null))
            .DistinctBy(candidate => (candidate.Status.Branch, candidate.Status.Level))
            .ToArray();
        if (candidates.Length > 0)
        {
            return candidates;
        }

        var fallbackCandidates = Enum.GetValues<BranchCode>()
            .Select(branch => DefaultTrainingWorkPolicy.SelectStatus(
                currentState.CurrentPractitionerState,
                branch))
            .OfType<BranchLevelStatus>()
            .OrderBy(status => status.State == BranchLevelState.Decayed ? 0 : 1)
            .ThenBy(status => status.Branch)
            .ToArray();
        return fallbackCandidates.Length == 0
            ? []
            : [new DailyBlockCandidate(fallbackCandidates[0], ForcedSessionType: null)];
    }

    private async ValueTask<LocalDailyTrainingPrescriptionRecord> RequiredByDateAsync(
        TrainingDate date,
        CancellationToken cancellationToken)
    {
        return await prescriptionStore.LoadByDateAsync(date, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("No daily prescription exists for this date.");
    }

    private async ValueTask<LocalDailyTrainingPrescriptionRecord> RequiredBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session id is required.", nameof(sessionId));
        }

        var records = await prescriptionStore.ListAsync(cancellationToken).ConfigureAwait(false);
        return records.SingleOrDefault(record => record.Blocks.Any(block =>
                string.Equals(block.SessionId, sessionId, StringComparison.Ordinal)))
            ?? throw new InvalidOperationException("No daily prescription references this runtime session.");
    }

    private static LocalDailyTrainingPrescriptionRecord ReplaceBlock(
        LocalDailyTrainingPrescriptionRecord prescription,
        LocalDailyTrainingBlockRecord block,
        LocalDailyTrainingBlockState state,
        string? sessionId,
        DailyTrainingDoseState doseState)
    {
        var blocks = prescription.Blocks.Select(item => item.BlockId == block.BlockId
            ? CopyBlock(item, state, sessionId)
            : item).ToArray();
        return CopyPrescription(prescription, doseState, blocks);
    }

    private static LocalDailyTrainingBlockRecord CopyBlock(
        LocalDailyTrainingBlockRecord block,
        LocalDailyTrainingBlockState state,
        string? sessionId)
    {
        return new LocalDailyTrainingBlockRecord(
            block.BlockId,
            block.Order,
            block.Branch,
            block.Level,
            block.Drill,
            block.Role,
            block.LoadVariables,
            state,
            sessionId);
    }

    private static LocalDailyTrainingPrescriptionRecord CopyPrescription(
        LocalDailyTrainingPrescriptionRecord prescription,
        DailyTrainingDoseState state,
        IEnumerable<LocalDailyTrainingBlockRecord> blocks)
    {
        return new LocalDailyTrainingPrescriptionRecord(
            prescription.PrescriptionId,
            prescription.Date,
            prescription.CycleAnchor,
            prescription.CycleDay,
            prescription.WeeklySession,
            state,
            blocks);
    }

    private static LocalDailyTrainingBlockRole RoleFor(AppTrainingSessionType sessionType)
    {
        return sessionType switch
        {
            AppTrainingSessionType.Practice => LocalDailyTrainingBlockRole.Practice,
            AppTrainingSessionType.Load => LocalDailyTrainingBlockRole.Load,
            AppTrainingSessionType.Test => LocalDailyTrainingBlockRole.Test,
            AppTrainingSessionType.Stabilization => LocalDailyTrainingBlockRole.Stabilization,
            AppTrainingSessionType.Maintenance => LocalDailyTrainingBlockRole.Maintenance,
            AppTrainingSessionType.Regression => LocalDailyTrainingBlockRole.Regression,
            AppTrainingSessionType.Transfer => LocalDailyTrainingBlockRole.Transfer,
            AppTrainingSessionType.Recovery => LocalDailyTrainingBlockRole.Recovery,
            _ => throw new ArgumentOutOfRangeException(
                nameof(sessionType),
                sessionType,
                "Unknown app training session type."),
        };
    }

    private static string PrescriptionIdFor(TrainingDate date) =>
        "daily-" + DateId(date);

    private static string BlockIdFor(TrainingDate date, int order) =>
        $"daily-{DateId(date)}-block-{order.ToString("00", CultureInfo.InvariantCulture)}";

    private static string DateId(TrainingDate date) =>
        date.Year.ToString("0000", CultureInfo.InvariantCulture) +
        date.Month.ToString("00", CultureInfo.InvariantCulture) +
        date.Day.ToString("00", CultureInfo.InvariantCulture);

    private sealed record DailyBlockCandidate(
        BranchLevelStatus Status,
        AppTrainingSessionType? ForcedSessionType,
        LocalDailyTrainingBlockRole? ForcedRole = null);
}
