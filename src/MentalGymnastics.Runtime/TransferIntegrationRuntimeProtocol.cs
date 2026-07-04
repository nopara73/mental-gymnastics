using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum TransferIntegrationRuntimeProtocolInvalidReason
{
    CompositeTaskNotSupportedByDrill,
    GlobalReviewTaskNotSupportedByDrill,
    ComponentBranchesAlreadyStated,
    ComponentBranchesRequiredBeforeComposite,
    ComponentBranchesRequiredBeforeGlobalReview,
    ComponentsRequireAtLeastTwoBranches,
    DuplicateComponentId,
    CompositeTaskAlreadyStarted,
    CompositeTaskNotStarted,
    CompositeTaskAlreadyCompleted,
    GlobalReviewTaskAlreadyStarted,
    GlobalReviewTaskNotStarted,
    GlobalReviewTaskAlreadyCompleted,
    UnknownComponent,
    ComponentEvidenceAlreadyRecorded,
    ComponentEvidenceMissing,
    AuditEvidenceAlreadyRecorded,
    AuditEvidenceRequiredBeforeGlobalReviewCompletion,
    DelayedReconstructionEvidenceAlreadyRecorded,
    DelayedReconstructionEvidenceRequiredBeforeGlobalReviewCompletion,
}

public sealed class TransferIntegrationRuntimeComponent
{
    public TransferIntegrationRuntimeComponent(
        string id,
        BranchLevelStandard sourceStandard)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Transfer integration component id is required.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(sourceStandard);

        Id = id;
        SourceStandard = sourceStandard;
    }

    public string Id { get; }

    public BranchLevelStandard SourceStandard { get; }
}

public sealed record TransferIntegrationRuntimeProtocolResult(
    bool IsAccepted,
    TransferIntegrationRuntimeProtocolInvalidReason? InvalidReason,
    RuntimeEvent? Event);

public sealed class TransferIntegrationRuntimeProtocol
{
    private readonly IRuntimeClock _clock;
    private readonly Dictionary<string, ComponentState> _components = new(StringComparer.Ordinal);
    private bool _compositeStarted;
    private bool _compositeCompleted;
    private bool _globalReviewStarted;
    private bool _globalReviewCompleted;
    private bool _auditRecorded;
    private bool _auditPassed;
    private string? _auditSummary;
    private bool _delayedReconstructionRecorded;
    private bool _delayedReconstructionPassed;
    private RuntimeDuration? _delayedReconstructionDelay;
    private string? _delayedReconstructionSummary;

    private TransferIntegrationRuntimeProtocol(
        IRuntimeClock clock,
        RuntimeEventLog eventLog)
    {
        _clock = clock;
        EventLog = eventLog;
    }

    public RuntimeEventLog EventLog { get; }

    public RuntimeSessionDefinition SessionDefinition => EventLog.SessionDefinition;

    public IReadOnlyList<TransferIntegrationRuntimeComponent> Components =>
        _components.Values.Select(state => state.Component).ToArray();

    public static TransferIntegrationRuntimeProtocol Start(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        IRuntimeClock clock)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Transfer integration runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(clock);
        ValidateTransferIntegrationSession(sessionDefinition);

        return new TransferIntegrationRuntimeProtocol(
            clock,
            RuntimeEventLog.Start(sessionId, sessionDefinition, clock.Now));
    }

    public TransferIntegrationRuntimeProtocolResult StateComponentBranches(
        IEnumerable<TransferIntegrationRuntimeComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        if (_components.Count > 0)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.ComponentBranchesAlreadyStated);
        }

        var componentArray = components.ToArray();
        foreach (var component in componentArray)
        {
            ArgumentNullException.ThrowIfNull(component);
        }

        if (componentArray.Length < 2)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.ComponentsRequireAtLeastTwoBranches);
        }

        var duplicateComponent = componentArray
            .GroupBy(component => component.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateComponent is not null)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.DuplicateComponentId);
        }

        foreach (var component in componentArray)
        {
            _components.Add(component.Id, new ComponentState(component));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "instruction",
            RuntimeSessionPhaseKind.InstructionPrep,
            [
                ..ProtocolFacts(),
                ..ComponentSetFacts(),
                new RuntimeEventFact("branch_specific_evidence_required", "true"),
                new RuntimeEventFact("strong_component_cannot_hide_weak_component", "true"),
                new RuntimeEventFact("honesty_constraint", "branch_specific_evidence_required"),
            ]);

        return Accepted(runtimeEvent);
    }

    public TransferIntegrationRuntimeProtocolResult StartCompositeTask()
    {
        if (!SupportsCompositeTask())
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.CompositeTaskNotSupportedByDrill);
        }

        if (_components.Count == 0)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.ComponentBranchesRequiredBeforeComposite);
        }

        if (_compositeStarted)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.CompositeTaskAlreadyStarted);
        }

        _compositeStarted = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..ComponentSetFacts(),
                new RuntimeEventFact("branch_specific_evidence_required", "true"),
                new RuntimeEventFact("strong_component_cannot_hide_weak_component", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public TransferIntegrationRuntimeProtocolResult StartGlobalReviewTask()
    {
        if (!SupportsGlobalReviewTask())
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.GlobalReviewTaskNotSupportedByDrill);
        }

        if (_components.Count == 0)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.ComponentBranchesRequiredBeforeGlobalReview);
        }

        if (_globalReviewStarted)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.GlobalReviewTaskAlreadyStarted);
        }

        _globalReviewStarted = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            [
                ..ProtocolFacts(),
                ..ComponentSetFacts(),
                new RuntimeEventFact("branch_specific_evidence_required", "true"),
                new RuntimeEventFact("strong_component_cannot_hide_weak_component", "true"),
                new RuntimeEventFact("audit_required", "true"),
                new RuntimeEventFact("delayed_reconstruction_required", "true"),
            ]);

        return Accepted(runtimeEvent);
    }

    public TransferIntegrationRuntimeProtocolResult RecordComponentEvidence(
        string componentId,
        string evidence,
        bool branchStandardMet,
        bool criticalConstraintBreached)
    {
        if (string.IsNullOrWhiteSpace(componentId))
        {
            throw new ArgumentException("Transfer integration component id is required.", nameof(componentId));
        }

        if (string.IsNullOrWhiteSpace(evidence))
        {
            throw new ArgumentException("Transfer integration component evidence is required.", nameof(evidence));
        }

        var activeResult = RequireTaskInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (!_components.TryGetValue(componentId, out var state))
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.UnknownComponent);
        }

        if (state.HasEvidence)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.ComponentEvidenceAlreadyRecorded);
        }

        state.HasEvidence = true;
        state.Evidence = evidence;
        state.BranchStandardMet = branchStandardMet;
        state.CriticalConstraintBreached = criticalConstraintBreached;

        var clean = state.IsPassing;
        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.AddRange(ComponentFacts(state.Component));
        facts.Add(new RuntimeEventFact("branch_specific_evidence", evidence));
        facts.Add(new RuntimeEventFact("component_standard_met", BoolText(branchStandardMet)));
        facts.Add(new RuntimeEventFact("critical_constraint_breached", BoolText(criticalConstraintBreached)));
        facts.Add(new RuntimeEventFact("response_outcome", clean ? "correct" : "incorrect"));
        facts.Add(new RuntimeEventFact("expected_response", "component_standard_met"));
        facts.Add(new RuntimeEventFact("branch_specific_evidence_required", "true"));
        facts.Add(new RuntimeEventFact("strong_component_cannot_hide_weak_component", "true"));

        if (!clean)
        {
            var errorKind = criticalConstraintBreached
                ? "component_critical_constraint_breach"
                : "component_branch_below_passing";
            facts.Add(new RuntimeEventFact("error_kind", errorKind));
            facts.Add(new RuntimeEventFact("failed_item_list", $"{state.Component.SourceStandard.Branch} component failed: {evidence}"));
            facts.Add(new RuntimeEventFact("failed_constraint", criticalConstraintBreached ? "component_critical_constraint" : "component_branch_standard_visible"));
            facts.Add(new RuntimeEventFact("failure_type_candidate", "overload"));
        }

        facts.Add(new RuntimeEventFact("component_pass_count", PassingComponentCount().ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("component_fail_count", FailingComponentCount().ToString(CultureInfo.InvariantCulture)));

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            facts);

        return Accepted(runtimeEvent);
    }

    public TransferIntegrationRuntimeProtocolResult RecordAuditEvidence(
        string auditId,
        string auditSummary,
        bool auditPassed)
    {
        if (string.IsNullOrWhiteSpace(auditId))
        {
            throw new ArgumentException("Transfer integration audit id is required.", nameof(auditId));
        }

        if (string.IsNullOrWhiteSpace(auditSummary))
        {
            throw new ArgumentException("Transfer integration audit summary is required.", nameof(auditSummary));
        }

        var activeResult = RequireGlobalReviewInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (_auditRecorded)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.AuditEvidenceAlreadyRecorded);
        }

        _auditRecorded = true;
        _auditPassed = auditPassed;
        _auditSummary = auditSummary;

        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.Add(new RuntimeEventFact("audit_id", auditId));
        facts.Add(new RuntimeEventFact("audit_required", "true"));
        facts.Add(new RuntimeEventFact("audit_result", auditSummary));
        facts.Add(new RuntimeEventFact("audit_findings", auditSummary));
        facts.Add(new RuntimeEventFact("audit_passed", BoolText(auditPassed)));
        facts.Add(new RuntimeEventFact("response_outcome", auditPassed ? "correct" : "incorrect"));
        facts.Add(new RuntimeEventFact("expected_response", "audit_passed"));

        if (!auditPassed)
        {
            facts.Add(new RuntimeEventFact("error_kind", "global_review_audit_failed"));
            facts.Add(new RuntimeEventFact("failed_item_list", $"global review audit failed: {auditSummary}"));
            facts.Add(new RuntimeEventFact("failed_constraint", "audit_required"));
            facts.Add(new RuntimeEventFact("failure_type_candidate", "technical_failure"));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "audit",
            RuntimeSessionPhaseKind.Audit,
            facts);

        return Accepted(runtimeEvent);
    }

    public TransferIntegrationRuntimeProtocolResult RecordDelayedReconstruction(
        string reconstructionId,
        RuntimeDuration delay,
        string reconstructionSummary,
        bool reconstructionPassed)
    {
        if (string.IsNullOrWhiteSpace(reconstructionId))
        {
            throw new ArgumentException("Transfer integration delayed reconstruction id is required.", nameof(reconstructionId));
        }

        if (delay.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Transfer integration delayed reconstruction delay must be positive.");
        }

        if (string.IsNullOrWhiteSpace(reconstructionSummary))
        {
            throw new ArgumentException("Transfer integration delayed reconstruction summary is required.", nameof(reconstructionSummary));
        }

        var activeResult = RequireGlobalReviewInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (_delayedReconstructionRecorded)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.DelayedReconstructionEvidenceAlreadyRecorded);
        }

        _delayedReconstructionRecorded = true;
        _delayedReconstructionPassed = reconstructionPassed;
        _delayedReconstructionDelay = delay;
        _delayedReconstructionSummary = reconstructionSummary;

        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.Add(new RuntimeEventFact("reconstruction_id", reconstructionId));
        facts.Add(new RuntimeEventFact("delayed_reconstruction_required", "true"));
        facts.Add(new RuntimeEventFact("delay_duration", FormatDuration(delay)));
        facts.Add(new RuntimeEventFact("delayed_reconstruction", reconstructionSummary));
        facts.Add(new RuntimeEventFact("delayed_reconstruction_passed", BoolText(reconstructionPassed)));
        facts.Add(new RuntimeEventFact("response_outcome", reconstructionPassed ? "correct" : "incorrect"));
        facts.Add(new RuntimeEventFact("expected_response", "delayed_reconstruction_passed"));

        if (!reconstructionPassed)
        {
            facts.Add(new RuntimeEventFact("error_kind", "delayed_reconstruction_failed"));
            facts.Add(new RuntimeEventFact("failed_item_list", $"delayed reconstruction failed: {reconstructionSummary}"));
            facts.Add(new RuntimeEventFact("failed_constraint", "delayed_reconstruction_required"));
            facts.Add(new RuntimeEventFact("failure_type_candidate", "technical_failure"));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.CueResponseSubmitted,
            _clock.Now,
            "reconstruction",
            RuntimeSessionPhaseKind.ReconstructionInput,
            facts);

        return Accepted(runtimeEvent);
    }

    public TransferIntegrationRuntimeProtocolResult CompleteCompositeTask(string compositeId)
    {
        if (string.IsNullOrWhiteSpace(compositeId))
        {
            throw new ArgumentException("Transfer integration composite id is required.", nameof(compositeId));
        }

        var activeResult = RequireCompositeInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (!AllComponentsHaveEvidence())
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.ComponentEvidenceMissing);
        }

        _compositeCompleted = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "active",
            RuntimeSessionPhaseKind.ActiveWork,
            CompletionFacts(compositeId, globalReviewSummary: false));

        return Accepted(runtimeEvent);
    }

    public TransferIntegrationRuntimeProtocolResult CompleteGlobalReviewTask(string reviewId)
    {
        if (string.IsNullOrWhiteSpace(reviewId))
        {
            throw new ArgumentException("Transfer integration global review id is required.", nameof(reviewId));
        }

        var activeResult = RequireGlobalReviewInProgress();
        if (activeResult is not null)
        {
            return activeResult;
        }

        if (!AllComponentsHaveEvidence())
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.ComponentEvidenceMissing);
        }

        if (!_auditRecorded)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.AuditEvidenceRequiredBeforeGlobalReviewCompletion);
        }

        if (!_delayedReconstructionRecorded)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.DelayedReconstructionEvidenceRequiredBeforeGlobalReviewCompletion);
        }

        _globalReviewCompleted = true;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "review",
            RuntimeSessionPhaseKind.Review,
            CompletionFacts(reviewId, globalReviewSummary: true));

        return Accepted(runtimeEvent);
    }

    private TransferIntegrationRuntimeProtocolResult? RequireTaskInProgress()
    {
        if (SupportsCompositeTask())
        {
            return RequireCompositeInProgress();
        }

        return RequireGlobalReviewInProgress();
    }

    private TransferIntegrationRuntimeProtocolResult? RequireCompositeInProgress()
    {
        if (!SupportsCompositeTask())
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.CompositeTaskNotSupportedByDrill);
        }

        if (!_compositeStarted)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.CompositeTaskNotStarted);
        }

        if (_compositeCompleted)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.CompositeTaskAlreadyCompleted);
        }

        return null;
    }

    private TransferIntegrationRuntimeProtocolResult? RequireGlobalReviewInProgress()
    {
        if (!SupportsGlobalReviewTask())
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.GlobalReviewTaskNotSupportedByDrill);
        }

        if (!_globalReviewStarted)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.GlobalReviewTaskNotStarted);
        }

        if (_globalReviewCompleted)
        {
            return Rejected(TransferIntegrationRuntimeProtocolInvalidReason.GlobalReviewTaskAlreadyCompleted);
        }

        return null;
    }

    private bool SupportsCompositeTask()
    {
        return SessionDefinition.Drill == DrillId.TI1CompositeTask;
    }

    private bool SupportsGlobalReviewTask()
    {
        return SessionDefinition.Drill == DrillId.TI2GlobalReviewTask;
    }

    private bool AllComponentsHaveEvidence()
    {
        return _components.Count > 0 && _components.Values.All(component => component.HasEvidence);
    }

    private int PassingComponentCount()
    {
        return _components.Values.Count(component => component.HasEvidence && component.IsPassing);
    }

    private int FailingComponentCount()
    {
        return _components.Values.Count(component => component.HasEvidence && !component.IsPassing);
    }

    private IReadOnlyList<RuntimeEventFact> CompletionFacts(
        string taskId,
        bool globalReviewSummary)
    {
        var passCount = PassingComponentCount();
        var failCount = FailingComponentCount();
        var allComponentsPassing = failCount == 0 && _components.Count > 0;
        var branchMapping = ComponentMappingSummary();
        var score =
            $"component_pass_count={passCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"component_fail_count={failCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"all_component_standards_met={BoolText(allComponentsPassing)}; " +
            $"audit_passed={BoolText(_auditRecorded && _auditPassed)}; " +
            $"delayed_reconstruction_passed={BoolText(_delayedReconstructionRecorded && _delayedReconstructionPassed)}";

        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.AddRange(ComponentSetFacts());
        facts.Add(new RuntimeEventFact("task_id", taskId));
        facts.Add(new RuntimeEventFact("branch_mapping", branchMapping));
        facts.Add(new RuntimeEventFact("score", score));
        facts.Add(new RuntimeEventFact("component_pass_count", passCount.ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("component_fail_count", failCount.ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("all_component_standards_met", BoolText(allComponentsPassing)));
        facts.Add(new RuntimeEventFact("strong_component_cannot_hide_weak_component", "true"));
        facts.Add(new RuntimeEventFact("branch_specific_evidence_required", "true"));

        if (globalReviewSummary)
        {
            facts.Add(new RuntimeEventFact("global_review_summary", $"components={ComponentBranchList()}; {score}"));
            facts.Add(new RuntimeEventFact("audit_required", "true"));
            facts.Add(new RuntimeEventFact("audit_result", _auditSummary ?? "not_recorded"));
            facts.Add(new RuntimeEventFact("audit_passed", BoolText(_auditPassed)));
            facts.Add(new RuntimeEventFact("delayed_reconstruction_required", "true"));
            facts.Add(new RuntimeEventFact("delayed_reconstruction", _delayedReconstructionSummary ?? "not_recorded"));
            facts.Add(new RuntimeEventFact("delayed_reconstruction_delay", FormatDuration(_delayedReconstructionDelay ?? RuntimeDuration.Zero)));
            facts.Add(new RuntimeEventFact("delayed_reconstruction_passed", BoolText(_delayedReconstructionPassed)));
        }

        return facts.AsReadOnly();
    }

    private string ComponentMappingSummary()
    {
        return string.Join(
            "; ",
            _components.Values.Select(state =>
                $"{state.Component.SourceStandard.Branch}:{state.Component.SourceStandard.Level} evidence={EvidenceStatus(state)}"));
    }

    private string ComponentBranchList()
    {
        return string.Join(",", _components.Values.Select(state => state.Component.SourceStandard.Branch.ToString()));
    }

    private static string EvidenceStatus(ComponentState state)
    {
        if (!state.HasEvidence)
        {
            return "missing";
        }

        return state.IsPassing ? "passing" : "below_passing";
    }

    private IReadOnlyList<RuntimeEventFact> ProtocolFacts()
    {
        return
        [
            new RuntimeEventFact("protocol_branch", "ti"),
            new RuntimeEventFact("protocol_drill", StableDrill(SessionDefinition.Drill)),
            new RuntimeEventFact("branch", SessionDefinition.Branch.ToString()),
            new RuntimeEventFact("level", SessionDefinition.Level.ToString()),
            new RuntimeEventFact("drill", StableDrill(SessionDefinition.Drill)),
        ];
    }

    private IReadOnlyList<RuntimeEventFact> ComponentSetFacts()
    {
        var facts = new List<RuntimeEventFact>
        {
            new("component_count", _components.Count.ToString(CultureInfo.InvariantCulture)),
            new("component_ids", string.Join(",", _components.Keys)),
            new("component_branches", ComponentBranchList()),
        };

        foreach (var component in _components.Values.Select(state => state.Component))
        {
            facts.AddRange(ComponentFacts(component));
        }

        return facts.AsReadOnly();
    }

    private IReadOnlyList<RuntimeEventFact> ComponentFacts(TransferIntegrationRuntimeComponent component)
    {
        return
        [
            new RuntimeEventFact("component_id", component.Id),
            new RuntimeEventFact("component_branch", component.SourceStandard.Branch.ToString()),
            new RuntimeEventFact("component_level", component.SourceStandard.Level.ToString()),
            new RuntimeEventFact("component_standard", $"{component.SourceStandard.Branch} {component.SourceStandard.Level}: {component.SourceStandard.Standard}"),
        ];
    }

    private static TransferIntegrationRuntimeProtocolResult Accepted(RuntimeEvent runtimeEvent)
    {
        return new TransferIntegrationRuntimeProtocolResult(true, InvalidReason: null, runtimeEvent);
    }

    private static TransferIntegrationRuntimeProtocolResult Rejected(TransferIntegrationRuntimeProtocolInvalidReason invalidReason)
    {
        return new TransferIntegrationRuntimeProtocolResult(false, invalidReason, Event: null);
    }

    private static void ValidateTransferIntegrationSession(RuntimeSessionDefinition sessionDefinition)
    {
        if (sessionDefinition.Branch != BranchCode.TI)
        {
            throw new ArgumentException("Transfer integration runtime protocol requires a TI session.", nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill is not DrillId.TI1CompositeTask and not DrillId.TI2GlobalReviewTask)
        {
            throw new ArgumentException(
                "Transfer integration runtime protocol supports only TI-1 Composite Task and TI-2 Global Review Task.",
                nameof(sessionDefinition));
        }

        if (!ContainsConstraint(sessionDefinition, "component") ||
            !ContainsConstraint(sessionDefinition, "evidence"))
        {
            throw new ArgumentException(
                "TI runtime sessions must include component branch evidence constraints.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.TI2GlobalReviewTask &&
            (!ContainsConstraint(sessionDefinition, "audit") || !ContainsConstraint(sessionDefinition, "delayed reconstruction")))
        {
            throw new ArgumentException(
                "TI-2 runtime sessions must include audit and delayed reconstruction constraints.",
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
            DrillId.TI1CompositeTask => "ti_1_composite_task",
            DrillId.TI2GlobalReviewTask => "ti_2_global_review_task",
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unsupported transfer integration drill."),
        };
    }

    private static string FormatDuration(RuntimeDuration duration)
    {
        return duration.Value.ToString("c", CultureInfo.InvariantCulture);
    }

    private static string BoolText(bool value)
    {
        return value ? "true" : "false";
    }

    private sealed class ComponentState
    {
        public ComponentState(TransferIntegrationRuntimeComponent component)
        {
            Component = component;
        }

        public TransferIntegrationRuntimeComponent Component { get; }

        public bool HasEvidence { get; set; }

        public string? Evidence { get; set; }

        public bool BranchStandardMet { get; set; }

        public bool CriticalConstraintBreached { get; set; }

        public bool IsPassing => HasEvidence && BranchStandardMet && !CriticalConstraintBreached;
    }
}
