using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum AffectiveInterferenceRuntimeProtocolInvalidReason
{
    PressureRepeatNotSupportedByDrill,
    DisruptionRecoveryNotSupportedByDrill,
    SourceStandardAlreadyStated,
    SourceStandardRequiredBeforePressure,
    SourceStandardRequiredBeforeDisruption,
    PressureSourceAlreadyDefined,
    PressureSourceRequiredBeforePressure,
    PressureRepeatAlreadyStarted,
    PressureRepeatNotStarted,
    PressureRepeatAlreadyCompleted,
    SourceBranchScoreAlreadyRecorded,
    DisruptionPlanAlreadyDefined,
    DisruptionPlanRequiredBeforeRecovery,
    DisruptionRecoveryAlreadyStarted,
    DisruptionRecoveryNotStarted,
    DisruptionRecoveryAlreadyCompleted,
    InterruptionAlreadyRecorded,
    InterruptionRequiredBeforeRecovery,
    PostDisruptionRecoveryAlreadyRecorded,
}

public sealed class AffectiveInterferenceRuntimePressureSource
{
    public AffectiveInterferenceRuntimePressureSource(
        string id,
        string description,
        RuntimeDuration duration,
        string administrationNote)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Affective interference pressure source id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Affective interference pressure source description is required.", nameof(description));
        }

        if (duration.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Affective interference pressure duration must be positive.");
        }

        if (string.IsNullOrWhiteSpace(administrationNote))
        {
            throw new ArgumentException("Affective interference pressure administration note is required.", nameof(administrationNote));
        }

        Id = id;
        Description = description;
        Duration = duration;
        AdministrationNote = administrationNote;
    }

    public string Id { get; }

    public string Description { get; }

    public RuntimeDuration Duration { get; }

    public string AdministrationNote { get; }
}

public sealed class AffectiveInterferenceRuntimeDisruptionPlan
{
    public AffectiveInterferenceRuntimeDisruptionPlan(
        string id,
        RuntimeDuration interruptionAt,
        RuntimeDuration recoveryWindow,
        bool fullRestartAllowed,
        string description)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Affective interference disruption id is required.", nameof(id));
        }

        if (interruptionAt.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interruptionAt), interruptionAt, "Affective interference interruption timing must be positive.");
        }

        if (recoveryWindow.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(recoveryWindow), recoveryWindow, "Affective interference recovery window must be positive.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Affective interference disruption description is required.", nameof(description));
        }

        Id = id;
        InterruptionAt = interruptionAt;
        RecoveryWindow = recoveryWindow;
        FullRestartAllowed = fullRestartAllowed;
        Description = description;
    }

    public string Id { get; }

    public RuntimeDuration InterruptionAt { get; }

    public RuntimeDuration RecoveryWindow { get; }

    public bool FullRestartAllowed { get; }

    public string Description { get; }
}

public sealed record AffectiveInterferenceRuntimeProtocolResult(
    bool IsAccepted,
    AffectiveInterferenceRuntimeProtocolInvalidReason? InvalidReason,
    RuntimeEvent? Event);

public sealed class AffectiveInterferenceRuntimeProtocol
{
    private readonly IRuntimeClock _clock;
    private BranchLevelStandard? _sourceStandard;
    private AffectiveInterferenceRuntimePressureSource? _pressureSource;
    private AffectiveInterferenceRuntimeDisruptionPlan? _disruptionPlan;
    private RuntimeInstant? _activeStartedAt;
    private RuntimeInstant? _interruptionRecordedAt;
    private bool _pressureRepeatStarted;
    private bool _pressureRepeatCompleted;
    private bool _sourceBranchScoreRecorded;
    private bool _sourceBranchStandardMet;
    private bool _sourceBranchCriticalConstraintBreached;
    private string? _sourceBranchScore;
    private int _standardLoweringAttemptCount;
    private bool _disruptionRecoveryStarted;
    private bool _disruptionRecoveryCompleted;
    private bool _postDisruptionRecoveryRecorded;
    private bool _postDisruptionAboveThreshold;
    private bool _postDisruptionRecoveryWithinWindow;
    private string? _postDisruptionEvidence;
    private RuntimeDuration? _postDisruptionRecoveryDuration;
    private int _fullRestartAttemptCount;

    private AffectiveInterferenceRuntimeProtocol(
        IRuntimeClock clock,
        RuntimeEventLog eventLog)
    {
        _clock = clock;
        EventLog = eventLog;
    }

    public RuntimeEventLog EventLog { get; }

    public RuntimeSessionDefinition SessionDefinition => EventLog.SessionDefinition;

    public static AffectiveInterferenceRuntimeProtocol Start(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        IRuntimeClock clock)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Affective interference runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(clock);
        ValidateAffectiveInterferenceSession(sessionDefinition);

        return new AffectiveInterferenceRuntimeProtocol(
            clock,
            RuntimeEventLog.Start(sessionId, sessionDefinition, clock.Now));
    }

    public AffectiveInterferenceRuntimeProtocolResult StateOriginalBranchStandard(
        BranchLevelStandard sourceStandard)
    {
        ArgumentNullException.ThrowIfNull(sourceStandard);

        if (_sourceStandard is not null)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.SourceStandardAlreadyStated);
        }

        _sourceStandard = sourceStandard;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..SourceStandardFacts(),
                new RuntimeEventFact("original_standard_visible", "true"),
                new RuntimeEventFact("source_standard_stated_before_pressure", "true"),
                new RuntimeEventFact("honesty_constraint", "original_standard_visible"),
            ]);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult DefinePressureSource(
        AffectiveInterferenceRuntimePressureSource pressureSource)
    {
        ArgumentNullException.ThrowIfNull(pressureSource);

        if (!SupportsPressureRepeat())
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.PressureRepeatNotSupportedByDrill);
        }

        if (_pressureSource is not null)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.PressureSourceAlreadyDefined);
        }

        _pressureSource = pressureSource;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..PressureSourceFacts(),
                new RuntimeEventFact("pressure_source_defined", "true"),
                new RuntimeEventFact("standard_lowering_allowed", "false"),
                new RuntimeEventFact("honesty_constraint", "original_standard_cannot_be_lowered"),
            ]);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult StartPressureRepeat()
    {
        if (!SupportsPressureRepeat())
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.PressureRepeatNotSupportedByDrill);
        }

        if (_sourceStandard is null)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.SourceStandardRequiredBeforePressure);
        }

        if (_pressureSource is null)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.PressureSourceRequiredBeforePressure);
        }

        if (_pressureRepeatStarted)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.PressureRepeatAlreadyStarted);
        }

        _pressureRepeatStarted = true;
        _activeStartedAt = _clock.Now;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..SourceStandardFacts(),
                ..PressureSourceFacts(),
                new RuntimeEventFact("original_standard_visible", "true"),
                new RuntimeEventFact("defined_pressure_source_active", "true"),
                new RuntimeEventFact("standard_lowering_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult RecordStandardLoweringAttempt(string attemptedChange)
    {
        if (string.IsNullOrWhiteSpace(attemptedChange))
        {
            throw new ArgumentException("Affective interference standard lowering attempt is required.", nameof(attemptedChange));
        }

        var activeResult = RequirePressureRepeatInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _standardLoweringAttemptCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..SourceStandardFacts(),
                ..PressureSourceFacts(),
                new RuntimeEventFact("error_kind", "standard_lowered_during_pressure"),
                new RuntimeEventFact("failed_item_list", $"standard lowering attempt: {attemptedChange}"),
                new RuntimeEventFact("failed_constraint", "original_standard_cannot_be_lowered"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
                new RuntimeEventFact("standard_lowering_attempt_count", _standardLoweringAttemptCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("standard_lowering_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult RecordSourceBranchScore(
        string scoreId,
        string scoreSummary,
        bool originalStandardMet,
        bool criticalConstraintBreached)
    {
        if (string.IsNullOrWhiteSpace(scoreId))
        {
            throw new ArgumentException("Affective interference source branch score id is required.", nameof(scoreId));
        }

        if (string.IsNullOrWhiteSpace(scoreSummary))
        {
            throw new ArgumentException("Affective interference source branch score summary is required.", nameof(scoreSummary));
        }

        var activeResult = RequirePressureRepeatInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (_sourceBranchScoreRecorded)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.SourceBranchScoreAlreadyRecorded);
        }

        _sourceBranchScoreRecorded = true;
        _sourceBranchStandardMet = originalStandardMet;
        _sourceBranchCriticalConstraintBreached = criticalConstraintBreached;
        _sourceBranchScore = scoreSummary;
        var clean = originalStandardMet && !criticalConstraintBreached;
        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.AddRange(SourceStandardFacts());
        facts.AddRange(PressureSourceFacts());
        facts.Add(new RuntimeEventFact("score_id", scoreId));
        facts.Add(new RuntimeEventFact("source_branch_score", scoreSummary));
        facts.Add(new RuntimeEventFact("original_standard_met", originalStandardMet ? "true" : "false"));
        facts.Add(new RuntimeEventFact("critical_constraint_breached", criticalConstraintBreached ? "true" : "false"));
        facts.Add(new RuntimeEventFact("response_outcome", clean ? "correct" : "incorrect"));
        facts.Add(new RuntimeEventFact("expected_response", "original_standard_met"));
        facts.Add(new RuntimeEventFact("original_standard_visible", "true"));

        if (!clean)
        {
            facts.Add(new RuntimeEventFact("error_kind", criticalConstraintBreached ? "pressure_constraint_breach" : "source_standard_failed_under_pressure"));
            facts.Add(new RuntimeEventFact("failed_item_list", $"source branch score failed under pressure: {scoreSummary}"));
            facts.Add(new RuntimeEventFact("failed_constraint", criticalConstraintBreached ? "source_branch_critical_constraint" : "original_standard_remains_passing"));
            facts.Add(new RuntimeEventFact("failure_type_candidate", "overload"));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult CompletePressureRepeat(string repeatId)
    {
        if (string.IsNullOrWhiteSpace(repeatId))
        {
            throw new ArgumentException("Affective interference pressure repeat id is required.", nameof(repeatId));
        }

        var activeResult = RequirePressureRepeatInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _pressureRepeatCompleted = true;
        var elapsed = _activeStartedAt.HasValue
            ? _clock.Now.ElapsedSince(_activeStartedAt.Value)
            : RuntimeDuration.Zero;
        var score =
            $"original_standard_met={BoolText(_sourceBranchStandardMet)}; " +
            $"critical_constraint_breached={BoolText(_sourceBranchCriticalConstraintBreached)}; " +
            $"standard_lowering_attempt_count={_standardLoweringAttemptCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"pressure_source={_pressureSource!.Description}; elapsed={FormatDuration(elapsed)}";
        var branchMapping =
            $"source {_sourceStandard!.Branch} {_sourceStandard.Level} repeated under pressure source {_pressureSource.Description}";

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..SourceStandardFacts(),
                ..PressureSourceFacts(),
                new RuntimeEventFact("repeat_id", repeatId),
                new RuntimeEventFact("branch_mapping", branchMapping),
                new RuntimeEventFact("score", score),
                new RuntimeEventFact("source_branch_score", _sourceBranchScore ?? "not_recorded"),
                new RuntimeEventFact("original_standard_met", BoolText(_sourceBranchStandardMet)),
                new RuntimeEventFact("critical_constraint_breached", BoolText(_sourceBranchCriticalConstraintBreached)),
                new RuntimeEventFact("standard_lowering_attempt_count", _standardLoweringAttemptCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("standard_lowering_allowed", "false"),
                new RuntimeEventFact("original_standard_visible", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult DefineDisruptionPlan(
        AffectiveInterferenceRuntimeDisruptionPlan disruptionPlan)
    {
        ArgumentNullException.ThrowIfNull(disruptionPlan);

        if (!SupportsDisruptionRecovery())
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.DisruptionRecoveryNotSupportedByDrill);
        }

        if (_disruptionPlan is not null)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.DisruptionPlanAlreadyDefined);
        }

        _disruptionPlan = disruptionPlan;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..DisruptionPlanFacts(),
                new RuntimeEventFact("disruption_plan_defined", "true"),
                new RuntimeEventFact("honesty_constraint", "no_full_restart_unless_allowed"),
                new RuntimeEventFact("honesty_constraint", "post_disruption_evidence_required"),
            ]);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult StartDisruptionRecovery()
    {
        if (!SupportsDisruptionRecovery())
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.DisruptionRecoveryNotSupportedByDrill);
        }

        if (_disruptionPlan is null)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.DisruptionPlanRequiredBeforeRecovery);
        }

        if (_sourceStandard is null)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.SourceStandardRequiredBeforeDisruption);
        }

        if (_disruptionRecoveryStarted)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.DisruptionRecoveryAlreadyStarted);
        }

        _disruptionRecoveryStarted = true;
        _activeStartedAt = _clock.Now;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..SourceStandardFacts(),
                ..DisruptionPlanFacts(),
                new RuntimeEventFact("original_standard_visible", "true"),
                new RuntimeEventFact("full_restart_allowed", BoolText(_disruptionPlan.FullRestartAllowed)),
                new RuntimeEventFact("post_disruption_evidence_required", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult RecordInterruption(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Affective interference interruption description is required.", nameof(description));
        }

        var activeResult = RequireDisruptionRecoveryInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (_interruptionRecordedAt is not null)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.InterruptionAlreadyRecorded);
        }

        _interruptionRecordedAt = _clock.Now;
        var actualAt = _clock.Now.ElapsedSince(_activeStartedAt!.Value);
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.InterruptionRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..SourceStandardFacts(),
                ..DisruptionPlanFacts(),
                new RuntimeEventFact("interruption_description", description),
                new RuntimeEventFact("interruption_planned_at", FormatDuration(_disruptionPlan!.InterruptionAt)),
                new RuntimeEventFact("interruption_actual_at", FormatDuration(actualAt)),
                new RuntimeEventFact("interruption_timing", InterruptionTiming(actualAt, _disruptionPlan.InterruptionAt)),
            ]);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult RecordFullRestartAttempt(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Affective interference restart attempt description is required.", nameof(description));
        }

        var activeResult = RequireInterruptionRecorded();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _fullRestartAttemptCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..SourceStandardFacts(),
                ..DisruptionPlanFacts(),
                new RuntimeEventFact("error_kind", "full_restart_attempt"),
                new RuntimeEventFact("failed_item_list", $"full restart attempt: {description}"),
                new RuntimeEventFact("failed_constraint", "no_full_restart_unless_allowed"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
                new RuntimeEventFact("full_restart_allowed", BoolText(_disruptionPlan!.FullRestartAllowed)),
                new RuntimeEventFact("full_restart_attempt_count", _fullRestartAttemptCount.ToString(CultureInfo.InvariantCulture)),
            ]);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult RecordPostDisruptionRecovery(
        string recoveryId,
        string evidence,
        bool postDisruptionAboveThreshold)
    {
        if (string.IsNullOrWhiteSpace(recoveryId))
        {
            throw new ArgumentException("Affective interference recovery id is required.", nameof(recoveryId));
        }

        if (string.IsNullOrWhiteSpace(evidence))
        {
            throw new ArgumentException("Affective interference post-disruption evidence is required.", nameof(evidence));
        }

        var activeResult = RequireInterruptionRecorded();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (_postDisruptionRecoveryRecorded)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.PostDisruptionRecoveryAlreadyRecorded);
        }

        _postDisruptionRecoveryRecorded = true;
        _postDisruptionAboveThreshold = postDisruptionAboveThreshold;
        _postDisruptionEvidence = evidence;
        _postDisruptionRecoveryDuration = _clock.Now.ElapsedSince(_interruptionRecordedAt!.Value);
        _postDisruptionRecoveryWithinWindow = _postDisruptionRecoveryDuration.Value.Value <= _disruptionPlan!.RecoveryWindow.Value;
        var clean = postDisruptionAboveThreshold && _postDisruptionRecoveryWithinWindow;
        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.AddRange(SourceStandardFacts());
        facts.AddRange(DisruptionPlanFacts());
        facts.Add(new RuntimeEventFact("recovery_id", recoveryId));
        facts.Add(new RuntimeEventFact("post_disruption_evidence", evidence));
        facts.Add(new RuntimeEventFact("recovery_duration", FormatDuration(_postDisruptionRecoveryDuration.Value)));
        facts.Add(new RuntimeEventFact("recovery_within_window", BoolText(_postDisruptionRecoveryWithinWindow)));
        facts.Add(new RuntimeEventFact("post_disruption_above_threshold", BoolText(postDisruptionAboveThreshold)));
        facts.Add(new RuntimeEventFact("response_outcome", clean ? "correct" : "incorrect"));
        facts.Add(new RuntimeEventFact("expected_response", "post_disruption_above_threshold"));

        if (!clean)
        {
            facts.Add(new RuntimeEventFact("error_kind", postDisruptionAboveThreshold ? "recovery_window_missed" : "post_disruption_below_threshold"));
            facts.Add(new RuntimeEventFact("failed_item_list", $"post-disruption recovery failed: {evidence}"));
            facts.Add(new RuntimeEventFact("failed_constraint", postDisruptionAboveThreshold ? "resume_within_window" : "finish_above_threshold"));
            facts.Add(new RuntimeEventFact("failure_type_candidate", "overload"));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);

        return Accepted(runtimeEvent);
    }

    public AffectiveInterferenceRuntimeProtocolResult CompleteDisruptionRecovery(string setId)
    {
        if (string.IsNullOrWhiteSpace(setId))
        {
            throw new ArgumentException("Affective interference disruption recovery set id is required.", nameof(setId));
        }

        var activeResult = RequireDisruptionRecoveryInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        _disruptionRecoveryCompleted = true;
        var branchMapping =
            $"source {_sourceStandard!.Branch} {_sourceStandard.Level} recovered after disruption {_disruptionPlan!.Id}";
        var score =
            $"post_disruption_above_threshold={BoolText(_postDisruptionAboveThreshold)}; " +
            $"recovery_within_window={BoolText(_postDisruptionRecoveryWithinWindow)}; " +
            $"full_restart_attempt_count={_fullRestartAttemptCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"recovery_duration={FormatDuration(_postDisruptionRecoveryDuration ?? RuntimeDuration.Zero)}";

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..SourceStandardFacts(),
                ..DisruptionPlanFacts(),
                new RuntimeEventFact("set_id", setId),
                new RuntimeEventFact("branch_mapping", branchMapping),
                new RuntimeEventFact("score", score),
                new RuntimeEventFact("post_disruption_evidence", _postDisruptionEvidence ?? "not_recorded"),
                new RuntimeEventFact("post_disruption_above_threshold", BoolText(_postDisruptionAboveThreshold)),
                new RuntimeEventFact("recovery_within_window", BoolText(_postDisruptionRecoveryWithinWindow)),
                new RuntimeEventFact("full_restart_attempt_count", _fullRestartAttemptCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("full_restart_allowed", BoolText(_disruptionPlan.FullRestartAllowed)),
                new RuntimeEventFact("original_standard_visible", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    private AffectiveInterferenceRuntimeProtocolResult? RequirePressureRepeatInProgress()
    {
        if (!SupportsPressureRepeat())
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.PressureRepeatNotSupportedByDrill);
        }

        if (!_pressureRepeatStarted)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.PressureRepeatNotStarted);
        }

        if (_pressureRepeatCompleted)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.PressureRepeatAlreadyCompleted);
        }

        return null;
    }

    private AffectiveInterferenceRuntimeProtocolResult? RequireDisruptionRecoveryInProgress()
    {
        if (!SupportsDisruptionRecovery())
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.DisruptionRecoveryNotSupportedByDrill);
        }

        if (!_disruptionRecoveryStarted)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.DisruptionRecoveryNotStarted);
        }

        if (_disruptionRecoveryCompleted)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.DisruptionRecoveryAlreadyCompleted);
        }

        return null;
    }

    private AffectiveInterferenceRuntimeProtocolResult? RequireInterruptionRecorded()
    {
        var activeResult = RequireDisruptionRecoveryInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (_interruptionRecordedAt is null)
        {
            return Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason.InterruptionRequiredBeforeRecovery);
        }

        return null;
    }

    private bool SupportsPressureRepeat()
    {
        return SessionDefinition.Drill == DrillId.AI1PressureRepeat;
    }

    private bool SupportsDisruptionRecovery()
    {
        return SessionDefinition.Drill == DrillId.AI2DisruptionRecovery;
    }

    private IReadOnlyList<RuntimeEventFact> ProtocolFacts()
    {
        return
        [
            new RuntimeEventFact("protocol_branch", "ai"),
            new RuntimeEventFact("protocol_drill", StableDrill(SessionDefinition.Drill)),
            new RuntimeEventFact("branch", SessionDefinition.Branch.ToString()),
            new RuntimeEventFact("level", SessionDefinition.Level.ToString()),
            new RuntimeEventFact("drill", StableDrill(SessionDefinition.Drill)),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> SourceStandardFacts()
    {
        if (_sourceStandard is null)
        {
            return
            [
                new RuntimeEventFact("source_standard", "not_stated"),
            ];
        }

        return
        [
            new RuntimeEventFact("source_branch", _sourceStandard.Branch.ToString()),
            new RuntimeEventFact("source_level", _sourceStandard.Level.ToString()),
            new RuntimeEventFact("source_standard", $"source {_sourceStandard.Branch} {_sourceStandard.Level}: {_sourceStandard.Standard}"),
            new RuntimeEventFact("source_demand", _sourceStandard.Demand),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> PressureSourceFacts()
    {
        if (_pressureSource is null)
        {
            return
            [
                new RuntimeEventFact("pressure_source", "not_defined"),
            ];
        }

        return
        [
            new RuntimeEventFact("pressure_source_id", _pressureSource.Id),
            new RuntimeEventFact("pressure_source", _pressureSource.Description),
            new RuntimeEventFact("pressure_duration", FormatDuration(_pressureSource.Duration)),
            new RuntimeEventFact("pressure_administration", _pressureSource.AdministrationNote),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> DisruptionPlanFacts()
    {
        if (_disruptionPlan is null)
        {
            return
            [
                new RuntimeEventFact("disruption_plan", "not_defined"),
            ];
        }

        return
        [
            new RuntimeEventFact("disruption_id", _disruptionPlan.Id),
            new RuntimeEventFact("disruption_description", _disruptionPlan.Description),
            new RuntimeEventFact("interruption_planned_at", FormatDuration(_disruptionPlan.InterruptionAt)),
            new RuntimeEventFact("recovery_window", FormatDuration(_disruptionPlan.RecoveryWindow)),
            new RuntimeEventFact("full_restart_allowed", BoolText(_disruptionPlan.FullRestartAllowed)),
        ];
    }

    private static AffectiveInterferenceRuntimeProtocolResult Accepted(RuntimeEvent runtimeEvent)
    {
        return new AffectiveInterferenceRuntimeProtocolResult(true, InvalidReason: null, runtimeEvent);
    }

    private static AffectiveInterferenceRuntimeProtocolResult Rejected(AffectiveInterferenceRuntimeProtocolInvalidReason invalidReason)
    {
        return new AffectiveInterferenceRuntimeProtocolResult(false, invalidReason, Event: null);
    }

    private static void ValidateAffectiveInterferenceSession(RuntimeSessionDefinition sessionDefinition)
    {
        if (sessionDefinition.Branch != BranchCode.AI)
        {
            throw new ArgumentException("Affective interference runtime protocol requires an AI session.", nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill is not DrillId.AI1PressureRepeat and not DrillId.AI2DisruptionRecovery)
        {
            throw new ArgumentException(
                "Affective interference runtime protocol supports only AI-1 Pressure Repeat and AI-2 Disruption Recovery.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.AI1PressureRepeat &&
            (!ContainsConstraint(sessionDefinition, "original standard") || !ContainsConstraint(sessionDefinition, "pressure")))
        {
            throw new ArgumentException(
                "AI-1 runtime sessions must include original-standard and pressure-source constraints.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.AI2DisruptionRecovery &&
            (!ContainsConstraint(sessionDefinition, "full restart") || !ContainsConstraint(sessionDefinition, "post-disruption")))
        {
            throw new ArgumentException(
                "AI-2 runtime sessions must include full-restart and post-disruption evidence constraints.",
                nameof(sessionDefinition));
        }
    }

    private static bool ContainsConstraint(RuntimeSessionDefinition sessionDefinition, string text)
    {
        return sessionDefinition.CriticalConstraints.Any(constraint =>
            constraint.Description.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static string StableDrill(DrillId drill)
    {
        return drill switch
        {
            DrillId.AI1PressureRepeat => "ai_1_pressure_repeat",
            DrillId.AI2DisruptionRecovery => "ai_2_disruption_recovery",
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unsupported affective interference drill."),
        };
    }

    private static string InterruptionTiming(RuntimeDuration actualAt, RuntimeDuration plannedAt)
    {
        if (actualAt.Value == plannedAt.Value)
        {
            return "planned";
        }

        return actualAt.Value < plannedAt.Value ? "early" : "late";
    }

    private static string FormatDuration(RuntimeDuration duration)
    {
        return duration.Value.ToString("c", CultureInfo.InvariantCulture);
    }

    private static string BoolText(bool value)
    {
        return value ? "true" : "false";
    }
}
