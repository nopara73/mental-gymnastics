using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public enum WorkingMemoryRuntimeProtocolInvalidReason
{
    EncodeWindowAlreadyStarted,
    EncodeWindowNotStarted,
    EncodeWindowAlreadyClosed,
    EncodeWindowRequiredBeforeReconstruction,
    SourceItemsAlreadyEncoded,
    SourceItemsRequired,
    DuplicateSourceItemId,
    EncodeWindowMustCloseBeforeDelay,
    DelayWindowAlreadyStarted,
    DelayWindowNotStarted,
    DelayWindowAlreadyCompleted,
    DelayWindowMustCompleteBeforeReconstruction,
    ReconstructionAlreadySubmitted,
    MentalTransformNotSupportedByDrill,
    TransformRuleRequired,
    TransformRuleAlreadyStated,
    InvalidOperationCount,
}

public sealed class WorkingMemoryRuntimeItem
{
    public WorkingMemoryRuntimeItem(string id, string content)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Working memory item id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Working memory item content is required.", nameof(content));
        }

        Id = id;
        Content = content;
    }

    public string Id { get; }

    public string Content { get; }
}

public sealed class WorkingMemoryRuntimeReconstructionItem
{
    public WorkingMemoryRuntimeReconstructionItem(string id, string content)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Working memory reconstruction item id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Working memory reconstruction item content is required.", nameof(content));
        }

        Id = id;
        Content = content;
    }

    public string Id { get; }

    public string Content { get; }
}

public sealed record WorkingMemoryRuntimeProtocolResult(
    bool IsAccepted,
    WorkingMemoryRuntimeProtocolInvalidReason? InvalidReason,
    RuntimeEvent? Event);

public sealed class WorkingMemoryRuntimeProtocol
{
    private readonly IRuntimeClock _clock;
    private readonly Dictionary<string, WorkingMemoryRuntimeItem> _sourceItems = new(StringComparer.Ordinal);
    private RuntimeInstant? _encodeStartedAt;
    private RuntimeDuration? _plannedEncodeDuration;
    private RuntimeInstant? _delayStartedAt;
    private RuntimeDuration? _plannedDelayDuration;
    private bool _encodeClosed;
    private bool _delayCompleted;
    private bool _reconstructionSubmitted;
    private int _rereadAfterEncodeCount;
    private int _hiddenIntermediateNoteCount;
    private string? _transformRule;

    private WorkingMemoryRuntimeProtocol(
        IRuntimeClock clock,
        RuntimeEventLog eventLog)
    {
        _clock = clock;
        EventLog = eventLog;
    }

    public RuntimeEventLog EventLog { get; }

    public RuntimeSessionDefinition SessionDefinition => EventLog.SessionDefinition;

    public IReadOnlyList<WorkingMemoryRuntimeItem> SourceItems => _sourceItems.Values.ToArray();

    public static WorkingMemoryRuntimeProtocol Start(
        string sessionId,
        RuntimeSessionDefinition sessionDefinition,
        IRuntimeClock clock)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Working memory runtime session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(sessionDefinition);
        ArgumentNullException.ThrowIfNull(clock);
        ValidateWorkingMemorySession(sessionDefinition);

        return new WorkingMemoryRuntimeProtocol(
            clock,
            RuntimeEventLog.Start(sessionId, sessionDefinition, clock.Now));
    }

    public WorkingMemoryRuntimeProtocolResult StartEncodeWindow(RuntimeDuration plannedDuration)
    {
        if (plannedDuration.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plannedDuration),
                plannedDuration,
                "Working memory encode window duration must be positive.");
        }

        if (_encodeStartedAt.HasValue)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowAlreadyStarted);
        }

        _encodeStartedAt = _clock.Now;
        _plannedEncodeDuration = plannedDuration;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "encode",
            RuntimeSessionPhaseKind.EncodeWindow,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("encode_window_duration", FormatDuration(plannedDuration)),
                new RuntimeEventFact("encode_window_open", "true"),
                new RuntimeEventFact("rereading_after_encode_allowed", "false"),
                new RuntimeEventFact("invented_items_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public WorkingMemoryRuntimeProtocolResult EncodeSourceItems(
        IEnumerable<WorkingMemoryRuntimeItem> sourceItems)
    {
        ArgumentNullException.ThrowIfNull(sourceItems);

        if (!_encodeStartedAt.HasValue)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowNotStarted);
        }

        if (_encodeClosed)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowAlreadyClosed);
        }

        if (_sourceItems.Count > 0)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.SourceItemsAlreadyEncoded);
        }

        var itemArray = sourceItems.ToArray();
        foreach (var item in itemArray)
        {
            ArgumentNullException.ThrowIfNull(item);
        }

        if (itemArray.Length == 0)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.SourceItemsRequired);
        }

        var duplicateItem = itemArray
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateItem is not null)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.DuplicateSourceItemId);
        }

        foreach (var item in itemArray)
        {
            _sourceItems.Add(item.Id, item);
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "encode",
            RuntimeSessionPhaseKind.EncodeWindow,
            [
                ..ProtocolFacts(),
                ..SourceItemFacts(),
                new RuntimeEventFact("source_items_encoded", "true"),
                new RuntimeEventFact("encode_window_open", "true"),
                new RuntimeEventFact("source_item_count", _sourceItems.Count.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("rereading_after_encode_allowed", "false"),
                new RuntimeEventFact("invented_items_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public WorkingMemoryRuntimeProtocolResult CloseEncodeWindow()
    {
        if (!_encodeStartedAt.HasValue)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowNotStarted);
        }

        if (_encodeClosed)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowAlreadyClosed);
        }

        _encodeClosed = true;
        var actualDuration = _clock.Now.ElapsedSince(_encodeStartedAt.Value);
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseEnded,
            _clock.Now,
            "encode",
            RuntimeSessionPhaseKind.EncodeWindow,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("phase_actual_duration", FormatDuration(actualDuration)),
                new RuntimeEventFact("encode_window_closed", "true"),
                new RuntimeEventFact("rereading_after_encode_allowed", "false"),
                new RuntimeEventFact("invented_items_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public WorkingMemoryRuntimeProtocolResult AttemptRereadAfterEncode(string rereadReference)
    {
        if (string.IsNullOrWhiteSpace(rereadReference))
        {
            throw new ArgumentException("Working memory reread reference is required.", nameof(rereadReference));
        }

        if (!_encodeStartedAt.HasValue)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowNotStarted);
        }

        if (!_encodeClosed)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowAlreadyClosed);
        }

        _rereadAfterEncodeCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "encode",
            RuntimeSessionPhaseKind.EncodeWindow,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("error_kind", "reread_after_encode"),
                new RuntimeEventFact("failed_item_list", $"reread after encode: {rereadReference}"),
                new RuntimeEventFact("failed_constraint", "no_rereading_after_encode"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
                new RuntimeEventFact("reread_after_encode_count", _rereadAfterEncodeCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("rereading_after_encode_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public WorkingMemoryRuntimeProtocolResult StartDelayWindow(RuntimeDuration plannedDuration)
    {
        if (plannedDuration.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plannedDuration),
                plannedDuration,
                "Working memory delay window duration must be positive.");
        }

        if (!_encodeClosed)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowMustCloseBeforeDelay);
        }

        if (_delayStartedAt.HasValue)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.DelayWindowAlreadyStarted);
        }

        _delayStartedAt = _clock.Now;
        _plannedDelayDuration = plannedDuration;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseStarted,
            _clock.Now,
            "delay",
            RuntimeSessionPhaseKind.DelayWindow,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("delay_window_duration", FormatDuration(plannedDuration)),
                new RuntimeEventFact("delay_window_open", "true"),
                new RuntimeEventFact("rereading_after_encode_allowed", "false"),
                new RuntimeEventFact("invented_items_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public WorkingMemoryRuntimeProtocolResult CompleteDelayWindow()
    {
        if (!_delayStartedAt.HasValue)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.DelayWindowNotStarted);
        }

        if (_delayCompleted)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.DelayWindowAlreadyCompleted);
        }

        _delayCompleted = true;
        var actualDuration = _clock.Now.ElapsedSince(_delayStartedAt.Value);
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.PhaseEnded,
            _clock.Now,
            "delay",
            RuntimeSessionPhaseKind.DelayWindow,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("phase_actual_duration", FormatDuration(actualDuration)),
                new RuntimeEventFact("delay_window_closed", "true"),
                new RuntimeEventFact("rereading_after_encode_allowed", "false"),
                new RuntimeEventFact("invented_items_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public WorkingMemoryRuntimeProtocolResult SubmitReconstruction(
        string reconstructionId,
        IEnumerable<WorkingMemoryRuntimeReconstructionItem> reconstructedItems)
    {
        if (string.IsNullOrWhiteSpace(reconstructionId))
        {
            throw new ArgumentException("Working memory reconstruction id is required.", nameof(reconstructionId));
        }

        ArgumentNullException.ThrowIfNull(reconstructedItems);

        var reconstructionReadiness = RequireReadyForReconstruction();
        if (reconstructionReadiness is not null)
        {
            return reconstructionReadiness;
        }

        if (_reconstructionSubmitted)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.ReconstructionAlreadySubmitted);
        }

        var reconstructionArray = reconstructedItems.ToArray();
        foreach (var item in reconstructionArray)
        {
            ArgumentNullException.ThrowIfNull(item);
        }

        if (reconstructionArray.Length == 0)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.SourceItemsRequired);
        }

        _reconstructionSubmitted = true;
        var exactMatches = reconstructionArray.Count(item =>
            _sourceItems.TryGetValue(item.Id, out var sourceItem) &&
            string.Equals(sourceItem.Content, item.Content, StringComparison.Ordinal));
        var omissions = Math.Max(0, _sourceItems.Count - exactMatches);
        var inventedItems = reconstructionArray
            .Where(item => !_sourceItems.ContainsKey(item.Id))
            .ToArray();
        var accuracy = Ratio(exactMatches, _sourceItems.Count);
        var failedItemList = BuildReconstructionFailedItemList(inventedItems, omissions);
        var outputSample = $"reconstruction {reconstructionId}: {FormatItems(reconstructionArray)}";
        var score =
            $"reconstruction_accuracy={accuracy}; exact_matches={exactMatches.ToString(CultureInfo.InvariantCulture)}; " +
            $"source_items={_sourceItems.Count.ToString(CultureInfo.InvariantCulture)}; " +
            $"omissions={omissions.ToString(CultureInfo.InvariantCulture)}; " +
            $"invented_items={inventedItems.Length.ToString(CultureInfo.InvariantCulture)}; " +
            $"reread_after_encode_count={_rereadAfterEncodeCount.ToString(CultureInfo.InvariantCulture)}";

        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.Add(new RuntimeEventFact("reconstruction_id", reconstructionId));
        facts.Add(new RuntimeEventFact("output_sample", outputSample));
        facts.Add(new RuntimeEventFact("reconstruction", FormatItems(reconstructionArray)));
        facts.Add(new RuntimeEventFact("score", score));
        facts.Add(new RuntimeEventFact("source_item_count", _sourceItems.Count.ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("reconstruction_item_count", reconstructionArray.Length.ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("exact_match_count", exactMatches.ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("omission_count", omissions.ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("invented_item_count", inventedItems.Length.ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("reconstruction_accuracy", accuracy));
        facts.Add(new RuntimeEventFact("rereading_after_encode_allowed", "false"));
        facts.Add(new RuntimeEventFact("invented_items_allowed", "false"));

        if (inventedItems.Length > 0)
        {
            facts.Add(new RuntimeEventFact("failed_constraint", "no_invented_items"));
            facts.Add(new RuntimeEventFact("error_kind", "invented_item"));
        }

        if (failedItemList is not null)
        {
            facts.Add(new RuntimeEventFact("failed_item_list", failedItemList));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "reconstruct",
            RuntimeSessionPhaseKind.ReconstructionInput,
            facts);

        return Accepted(runtimeEvent);
    }

    public WorkingMemoryRuntimeProtocolResult StateTransformRule(string transformRule)
    {
        if (string.IsNullOrWhiteSpace(transformRule))
        {
            throw new ArgumentException("Working memory transform rule is required.", nameof(transformRule));
        }

        if (!SupportsMentalTransform())
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.MentalTransformNotSupportedByDrill);
        }

        if (_transformRule is not null)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.TransformRuleAlreadyStated);
        }

        _transformRule = transformRule;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.UserAction,
            _clock.Now,
            "encode",
            RuntimeSessionPhaseKind.EncodeWindow,
            [
                ..ProtocolFacts(),
                new RuntimeEventFact("transform_rule", transformRule),
                new RuntimeEventFact("transform_rule_stated_before_transform", "true"),
                new RuntimeEventFact("intermediate_notes_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public WorkingMemoryRuntimeProtocolResult RecordHiddenIntermediateNote(string noteReference)
    {
        if (string.IsNullOrWhiteSpace(noteReference))
        {
            throw new ArgumentException("Working memory hidden note reference is required.", nameof(noteReference));
        }

        if (!SupportsMentalTransform())
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.MentalTransformNotSupportedByDrill);
        }

        _hiddenIntermediateNoteCount++;
        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.ErrorRecorded,
            _clock.Now,
            "delay",
            RuntimeSessionPhaseKind.DelayWindow,
            [
                ..ProtocolFacts(),
                TransformRuleFact(),
                new RuntimeEventFact("error_kind", "hidden_intermediate_notes"),
                new RuntimeEventFact("failed_item_list", $"hidden intermediate note: {noteReference}"),
                new RuntimeEventFact("failed_constraint", "intermediate_notes_prohibited"),
                new RuntimeEventFact("failure_type_candidate", "effort_failure"),
                new RuntimeEventFact("hidden_intermediate_note_count", _hiddenIntermediateNoteCount.ToString(CultureInfo.InvariantCulture)),
                new RuntimeEventFact("intermediate_notes_allowed", "false"),
            ]);

        return Accepted(runtimeEvent);
    }

    public WorkingMemoryRuntimeProtocolResult SubmitMentalTransform(
        string transformId,
        string finalOutput,
        string ruleExplanation,
        int correctOperationCount,
        int expectedOperationCount)
    {
        if (string.IsNullOrWhiteSpace(transformId))
        {
            throw new ArgumentException("Working memory transform id is required.", nameof(transformId));
        }

        if (string.IsNullOrWhiteSpace(finalOutput))
        {
            throw new ArgumentException("Working memory transform final output is required.", nameof(finalOutput));
        }

        if (string.IsNullOrWhiteSpace(ruleExplanation))
        {
            throw new ArgumentException("Working memory transform rule explanation is required.", nameof(ruleExplanation));
        }

        if (!SupportsMentalTransform())
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.MentalTransformNotSupportedByDrill);
        }

        if (_transformRule is null)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.TransformRuleRequired);
        }

        var reconstructionReadiness = RequireReadyForReconstruction();
        if (reconstructionReadiness is not null)
        {
            return reconstructionReadiness;
        }

        if (_reconstructionSubmitted)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.ReconstructionAlreadySubmitted);
        }

        if (expectedOperationCount <= 0 ||
            correctOperationCount < 0 ||
            correctOperationCount > expectedOperationCount)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.InvalidOperationCount);
        }

        _reconstructionSubmitted = true;
        var operationAccuracy = Ratio(correctOperationCount, expectedOperationCount);
        var outputSample = $"transform {transformId}: output={finalOutput}; explanation={ruleExplanation}";
        var score =
            $"operation_accuracy={operationAccuracy}; " +
            $"correct_operations={correctOperationCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"expected_operations={expectedOperationCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"hidden_intermediate_note_count={_hiddenIntermediateNoteCount.ToString(CultureInfo.InvariantCulture)}; " +
            $"reread_after_encode_count={_rereadAfterEncodeCount.ToString(CultureInfo.InvariantCulture)}";

        var facts = new List<RuntimeEventFact>();
        facts.AddRange(ProtocolFacts());
        facts.Add(new RuntimeEventFact("transform_id", transformId));
        facts.Add(TransformRuleFact());
        facts.Add(new RuntimeEventFact("final_output", finalOutput));
        facts.Add(new RuntimeEventFact("rule_explanation", ruleExplanation));
        facts.Add(new RuntimeEventFact("output_sample", outputSample));
        facts.Add(new RuntimeEventFact("score", score));
        facts.Add(new RuntimeEventFact("operation_accuracy", operationAccuracy));
        facts.Add(new RuntimeEventFact("correct_operation_count", correctOperationCount.ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("expected_operation_count", expectedOperationCount.ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("hidden_intermediate_note_count", _hiddenIntermediateNoteCount.ToString(CultureInfo.InvariantCulture)));
        facts.Add(new RuntimeEventFact("intermediate_notes_allowed", "false"));
        facts.Add(new RuntimeEventFact("rereading_after_encode_allowed", "false"));
        facts.Add(new RuntimeEventFact("invented_items_allowed", "false"));

        if (_hiddenIntermediateNoteCount > 0)
        {
            facts.Add(new RuntimeEventFact("failed_constraint", "intermediate_notes_prohibited"));
            facts.Add(new RuntimeEventFact("failed_item_list", "hidden intermediate notes were recorded"));
        }

        var runtimeEvent = EventLog.Append(
            RuntimeEventKind.AnswerSubmitted,
            _clock.Now,
            "reconstruct",
            RuntimeSessionPhaseKind.ReconstructionInput,
            facts);

        return Accepted(runtimeEvent);
    }

    private WorkingMemoryRuntimeProtocolResult? RequireReadyForReconstruction()
    {
        if (!_encodeStartedAt.HasValue)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowRequiredBeforeReconstruction);
        }

        if (!_encodeClosed)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.EncodeWindowAlreadyClosed);
        }

        if (_sourceItems.Count == 0)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.SourceItemsRequired);
        }

        if (!_delayStartedAt.HasValue)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.DelayWindowNotStarted);
        }

        if (!_delayCompleted)
        {
            return Rejected(WorkingMemoryRuntimeProtocolInvalidReason.DelayWindowMustCompleteBeforeReconstruction);
        }

        return null;
    }

    private bool SupportsMentalTransform()
    {
        return SessionDefinition.Drill == DrillId.WM2MentalTransform;
    }

    private IReadOnlyList<RuntimeEventFact> ProtocolFacts()
    {
        var facts = new List<RuntimeEventFact>
        {
            new("protocol_branch", "wm"),
            new("protocol_drill", StableDrill(SessionDefinition.Drill)),
            new("branch", SessionDefinition.Branch.ToString()),
            new("level", SessionDefinition.Level.ToString()),
            new("drill", StableDrill(SessionDefinition.Drill)),
        };

        if (_plannedEncodeDuration.HasValue)
        {
            facts.Add(new RuntimeEventFact("planned_encode_duration", FormatDuration(_plannedEncodeDuration.Value)));
        }

        if (_plannedDelayDuration.HasValue)
        {
            facts.Add(new RuntimeEventFact("planned_delay_duration", FormatDuration(_plannedDelayDuration.Value)));
        }

        return facts.AsReadOnly();
    }

    private IReadOnlyList<RuntimeEventFact> SourceItemFacts()
    {
        var facts = new List<RuntimeEventFact>
        {
            new("source_item_count", _sourceItems.Count.ToString(CultureInfo.InvariantCulture)),
            new("source_item_ids", string.Join(",", _sourceItems.Keys)),
        };

        foreach (var item in _sourceItems.Values)
        {
            facts.Add(new RuntimeEventFact("source_item_id", item.Id));
            facts.Add(new RuntimeEventFact("source_item_content", item.Content));
        }

        return facts.AsReadOnly();
    }

    private RuntimeEventFact TransformRuleFact()
    {
        return new RuntimeEventFact("transform_rule", _transformRule ?? "not_stated");
    }

    private static string? BuildReconstructionFailedItemList(
        IReadOnlyCollection<WorkingMemoryRuntimeReconstructionItem> inventedItems,
        int omissions)
    {
        var failedItems = new List<string>();
        if (inventedItems.Count > 0)
        {
            failedItems.Add("invented: " + string.Join(", ", inventedItems.Select(item => $"{item.Id}={item.Content}")));
        }

        if (omissions > 0)
        {
            failedItems.Add($"omissions: {omissions.ToString(CultureInfo.InvariantCulture)}");
        }

        return failedItems.Count == 0
            ? null
            : string.Join("; ", failedItems);
    }

    private static WorkingMemoryRuntimeProtocolResult Accepted(RuntimeEvent runtimeEvent)
    {
        return new WorkingMemoryRuntimeProtocolResult(true, InvalidReason: null, runtimeEvent);
    }

    private static WorkingMemoryRuntimeProtocolResult Rejected(WorkingMemoryRuntimeProtocolInvalidReason invalidReason)
    {
        return new WorkingMemoryRuntimeProtocolResult(false, invalidReason, Event: null);
    }

    private static void ValidateWorkingMemorySession(RuntimeSessionDefinition sessionDefinition)
    {
        if (sessionDefinition.Branch != BranchCode.WM)
        {
            throw new ArgumentException("Working memory runtime protocol requires a WM session.", nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill is not DrillId.WM1DelayedReconstruction and not DrillId.WM2MentalTransform)
        {
            throw new ArgumentException(
                "Working memory runtime protocol supports only WM-1 Delayed Reconstruction and WM-2 Mental Transform.",
                nameof(sessionDefinition));
        }

        if (!ContainsConstraint(sessionDefinition, "rereading") ||
            !ContainsConstraint(sessionDefinition, "invented"))
        {
            throw new ArgumentException(
                "Working memory runtime sessions must include no-rereading and no-invention constraints.",
                nameof(sessionDefinition));
        }

        if (sessionDefinition.Drill == DrillId.WM2MentalTransform &&
            !ContainsConstraint(sessionDefinition, "intermediate notes"))
        {
            throw new ArgumentException(
                "WM-2 runtime sessions must include the intermediate-note constraint.",
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
            DrillId.WM1DelayedReconstruction => "wm_1_delayed_reconstruction",
            DrillId.WM2MentalTransform => "wm_2_mental_transform",
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unsupported working memory drill."),
        };
    }

    private static string FormatItems(IEnumerable<WorkingMemoryRuntimeReconstructionItem> items)
    {
        return string.Join("; ", items.Select(item => $"{item.Id}={item.Content}"));
    }

    private static string Ratio(int numerator, int denominator)
    {
        return $"{numerator.ToString(CultureInfo.InvariantCulture)}/{denominator.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatDuration(RuntimeDuration duration)
    {
        return duration.Value.ToString("c", CultureInfo.InvariantCulture);
    }
}
