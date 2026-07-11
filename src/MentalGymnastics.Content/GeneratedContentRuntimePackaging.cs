using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public enum GeneratedRuntimePhaseKind
{
    InstructionPrep,
    EncodeWindow,
    ActiveWork,
    DelayWindow,
    CueResponse,
    ReconstructionInput,
    Audit,
    Rest,
    Recovery,
    Review,
}

public enum GeneratedRuntimePhaseCompletionRule
{
    Manual,
    Timed,
    ManualOrTimed,
}

public sealed class GeneratedRuntimePhaseDefinition
{
    public GeneratedRuntimePhaseDefinition(
        string id,
        GeneratedRuntimePhaseKind kind,
        GeneratedRuntimePhaseCompletionRule completionRule,
        TimeSpan? scheduledDuration = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Generated runtime phase id is required.", nameof(id));
        }

        GeneratedContentValidation.EnsureDefined(kind, nameof(kind));
        GeneratedContentValidation.EnsureDefined(completionRule, nameof(completionRule));

        if (scheduledDuration.HasValue && scheduledDuration.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scheduledDuration),
                scheduledDuration,
                "Generated runtime phase duration must be positive when provided.");
        }

        var requiresDuration =
            completionRule is GeneratedRuntimePhaseCompletionRule.Timed or GeneratedRuntimePhaseCompletionRule.ManualOrTimed;
        if (requiresDuration && !scheduledDuration.HasValue)
        {
            throw new ArgumentException(
                "Timed generated runtime phases require a scheduled duration.",
                nameof(scheduledDuration));
        }

        Id = id;
        Kind = kind;
        CompletionRule = completionRule;
        ScheduledDuration = scheduledDuration;
    }

    public string Id { get; }

    public GeneratedRuntimePhaseKind Kind { get; }

    public GeneratedRuntimePhaseCompletionRule CompletionRule { get; }

    public TimeSpan? ScheduledDuration { get; }
}

public enum GeneratedRuntimeCueKind
{
    FocusShift,
    InvalidCueFilter,
    GoNoGo,
    Interruption,
    TimedResponse,
}

public enum GeneratedRuntimeCueResponseExpectation
{
    ResponseRequired,
    NoResponseExpected,
}

public sealed class GeneratedRuntimeCueDefinition
{
    public GeneratedRuntimeCueDefinition(
        string id,
        GeneratedRuntimeCueKind kind,
        string value,
        TimeSpan scheduledAtOffset,
        TimeSpan responseWindow,
        GeneratedRuntimeCueResponseExpectation responseExpectation,
        string? expectedResponse = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Generated runtime cue id is required.", nameof(id));
        }

        GeneratedContentValidation.EnsureDefined(kind, nameof(kind));

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Generated runtime cue value is required.", nameof(value));
        }

        if (scheduledAtOffset < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scheduledAtOffset),
                scheduledAtOffset,
                "Generated runtime cue offset cannot be negative.");
        }

        if (responseWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseWindow),
                responseWindow,
                "Generated runtime cue response window must be positive.");
        }

        GeneratedContentValidation.EnsureDefined(responseExpectation, nameof(responseExpectation));

        if (expectedResponse is not null && string.IsNullOrWhiteSpace(expectedResponse))
        {
            throw new ArgumentException("Expected generated runtime cue response cannot be blank.", nameof(expectedResponse));
        }

        Id = id;
        Kind = kind;
        Value = value;
        ScheduledAtOffset = scheduledAtOffset;
        ResponseWindow = responseWindow;
        ResponseExpectation = responseExpectation;
        ExpectedResponse = expectedResponse;
    }

    public string Id { get; }

    public GeneratedRuntimeCueKind Kind { get; }

    public string Value { get; }

    public TimeSpan ScheduledAtOffset { get; }

    public TimeSpan ResponseWindow { get; }

    public GeneratedRuntimeCueResponseExpectation ResponseExpectation { get; }

    public string? ExpectedResponse { get; }
}

public sealed class GeneratedContentRuntimePackage
{
    internal GeneratedContentRuntimePackage(
        GeneratedDrillContentResult result,
        BranchLevelStandard standard,
        DrillId? sourceDrill,
        IEnumerable<GeneratedRuntimePhaseDefinition> phases,
        IEnumerable<GeneratedRuntimeCueDefinition> cues,
        IEnumerable<GeneratedContentMaterial> inputMaterials,
        IEnumerable<GeneratedContentPayloadFact> expectedEvidenceFacts)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(standard);
        ArgumentNullException.ThrowIfNull(phases);
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(inputMaterials);
        ArgumentNullException.ThrowIfNull(expectedEvidenceFacts);

        var phaseArray = phases.ToArray();
        if (phaseArray.Length == 0)
        {
            throw new ArgumentException("Generated runtime packages must include at least one phase.", nameof(phases));
        }

        foreach (var phase in phaseArray)
        {
            ArgumentNullException.ThrowIfNull(phase);
        }

        var cueArray = cues.ToArray();
        foreach (var cue in cueArray)
        {
            ArgumentNullException.ThrowIfNull(cue);
        }

        var materialArray = inputMaterials.ToArray();
        if (materialArray.Length == 0)
        {
            throw new ArgumentException("Generated runtime packages must include input material.", nameof(inputMaterials));
        }

        foreach (var material in materialArray)
        {
            ArgumentNullException.ThrowIfNull(material);
        }

        var evidenceFactArray = expectedEvidenceFacts.ToArray();
        if (evidenceFactArray.Length == 0)
        {
            throw new ArgumentException(
                "Generated runtime packages must include expected evidence facts.",
                nameof(expectedEvidenceFacts));
        }

        Result = result;
        Standard = standard;
        SourceDrill = sourceDrill;
        Phases = Array.AsReadOnly(phaseArray);
        Cues = Array.AsReadOnly(cueArray);
        InputMaterials = Array.AsReadOnly(materialArray);
        ExpectedEvidenceFacts = Array.AsReadOnly(evidenceFactArray);
    }

    public GeneratedDrillContentResult Result { get; }

    public GeneratedDrillInstanceDescriptor GeneratedInstance => Result.Instance;

    public BranchCode Branch => Result.Branch;

    public GlobalLevelId Level => Result.Level;

    public DrillId Drill => Result.Drill;

    public DrillId? SourceDrill { get; }

    public SessionType SessionType => Result.SessionType;

    public IReadOnlyList<LoadVariable> LoadVariables => Result.Request.LoadVariables;

    public IReadOnlyList<CriticalConstraint> CriticalConstraints => Result.Request.CriticalConstraints;

    public BranchLevelStandard Standard { get; }

    public IReadOnlyList<GeneratedRuntimePhaseDefinition> Phases { get; }

    public IReadOnlyList<GeneratedRuntimeCueDefinition> Cues { get; }

    public IReadOnlyList<GeneratedContentMaterial> InputMaterials { get; }

    public IReadOnlyList<GeneratedContentPayloadFact> ExpectedEvidenceFacts { get; }

    public bool CanBeConsumedByRuntime => true;

    public bool RuntimeInventsContent => false;

    public bool GrantsAdvancement => false;
}

public static class GeneratedContentRuntimePackager
{
    private static readonly TimeSpan DefaultCueInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultResponseWindow = TimeSpan.FromSeconds(2);

    public static GeneratedContentRuntimePackage Package(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials,
        BranchLevelStandard standard)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(standard);

        if (standard.Branch != result.Branch || standard.Level != result.Level)
        {
            throw new ArgumentException(
                "Generated runtime package standard must match the generated branch and level.",
                nameof(standard));
        }

        if (string.IsNullOrWhiteSpace(standard.Standard))
        {
            throw new ArgumentException("Generated runtime package standard text is required.", nameof(standard));
        }

        var materialArray = GeneratedContentBoundaryHandoffValidation.EnsureCanHandoff(
            result,
            materials);

        var sourceDrill = SourceDrillFor(result, materialArray);
        var phases = BuildPhases(result, materialArray, sourceDrill);
        var cues = BuildCues(result, materialArray, sourceDrill);
        if (result.ContentKind == PromptContentKind.CueSequence && cues.Count == 0)
        {
            throw new InvalidOperationException(
                "Generated cue sequence content cannot be packaged for runtime without cue definitions.");
        }

        var evidenceFacts = ExpectedEvidenceFacts(result).ToArray();
        return new GeneratedContentRuntimePackage(
            result,
            standard,
            sourceDrill,
            phases,
            cues,
            materialArray,
            evidenceFacts.Length == 0 ? result.PayloadFacts : evidenceFacts);
    }

    private static IReadOnlyList<GeneratedRuntimePhaseDefinition> BuildPhases(
        GeneratedDrillContentResult result,
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        DrillId? sourceDrill)
    {
        var phases = new List<GeneratedRuntimePhaseDefinition>
        {
            ManualPhase("instruction-prep", GeneratedRuntimePhaseKind.InstructionPrep),
        };

        if (result.Drill == DrillId.TI2GlobalReviewTask)
        {
            var taskLength = FirstDuration(materials, GeneratedContentMaterialKind.TaskLength) ??
                TimeSpan.FromMinutes(20);
            var delay = FirstDuration(materials, GeneratedContentMaterialKind.DelayLength) ??
                TimeSpan.FromMinutes(5);
            phases.Add(ManualOrTimedPhase("active-work", GeneratedRuntimePhaseKind.ActiveWork, taskLength));
            phases.Add(ManualPhase("audit", GeneratedRuntimePhaseKind.Audit));
            phases.Add(TimedPhase("delay-window", GeneratedRuntimePhaseKind.DelayWindow, delay));
            phases.Add(ManualPhase("reconstruction-input", GeneratedRuntimePhaseKind.ReconstructionInput));
            phases.Add(ManualPhase("review", GeneratedRuntimePhaseKind.Review));
            return Array.AsReadOnly(phases.ToArray());
        }

        if (result.Drill == DrillId.TI1CompositeTask)
        {
            var taskLength = FirstDuration(materials, GeneratedContentMaterialKind.TaskLength) ??
                TimeSpan.FromMinutes(12);
            phases.Add(ManualOrTimedPhase("active-work", GeneratedRuntimePhaseKind.ActiveWork, taskLength));
            var delay = FirstDuration(materials, GeneratedContentMaterialKind.DelayLength);
            if (delay.HasValue)
            {
                phases.Add(TimedPhase("delay-window", GeneratedRuntimePhaseKind.DelayWindow, delay.Value));
                phases.Add(ManualPhase("reconstruction-input", GeneratedRuntimePhaseKind.ReconstructionInput));
            }

            phases.Add(ManualPhase("review", GeneratedRuntimePhaseKind.Review));
            return Array.AsReadOnly(phases.ToArray());
        }

        if (result.Drill == DrillId.CO1RuleExtraction)
        {
            phases.Add(ManualPhase("rule-statement", GeneratedRuntimePhaseKind.ActiveWork));
            phases.Add(ManualPhase("unseen-test", GeneratedRuntimePhaseKind.ReconstructionInput));
            phases.Add(ManualPhase("review", GeneratedRuntimePhaseKind.Review));
            return Array.AsReadOnly(phases.ToArray());
        }

        if (result.Drill == DrillId.CO2StructureMapping)
        {
            phases.Add(ManualPhase("relation-naming", GeneratedRuntimePhaseKind.ActiveWork));
            phases.Add(ManualPhase("mapping-input", GeneratedRuntimePhaseKind.ReconstructionInput));
            if (materials.Any(material => material.Kind == GeneratedContentMaterialKind.AuditPayload))
            {
                phases.Add(ManualPhase("model-audit", GeneratedRuntimePhaseKind.Audit));
            }
            phases.Add(ManualPhase("review", GeneratedRuntimePhaseKind.Review));
            return Array.AsReadOnly(phases.ToArray());
        }

        if (result.Branch == BranchCode.AI && sourceDrill.HasValue)
        {
            if (sourceDrill == DrillId.FH2DistractorHold)
            {
                var holdDuration = FirstDuration(materials, GeneratedContentMaterialKind.HoldDuration) ??
                    TimeSpan.FromMinutes(5);
                phases.Add(TimedPhase("active-work", GeneratedRuntimePhaseKind.ActiveWork, holdDuration));
            }
            else
            {
                phases.Add(ManualPhase("cue-response", GeneratedRuntimePhaseKind.CueResponse));
            }

            AddComponentEvidencePhase(phases, materials);
            phases.Add(ManualPhase("review", GeneratedRuntimePhaseKind.Review));
            return Array.AsReadOnly(phases.ToArray());
        }

        var addedWorkPhase = false;
        if (result.Drill == DrillId.WM2MentalTransform &&
            materials.Any(material => material.Kind == GeneratedContentMaterialKind.ComponentPayload) &&
            FirstDuration(materials, GeneratedContentMaterialKind.TaskLength) is { } integratedTaskLength)
        {
            phases.Add(ManualOrTimedPhase("integrated-work", GeneratedRuntimePhaseKind.ActiveWork, integratedTaskLength));
            addedWorkPhase = true;
        }

        if (result.Drill == DrillId.FH2DistractorHold)
        {
            var holdDuration = FirstDuration(materials, GeneratedContentMaterialKind.HoldDuration) ??
                TimeSpan.FromMinutes(5);
            phases.Add(TimedPhase("active-work", GeneratedRuntimePhaseKind.ActiveWork, holdDuration));
            addedWorkPhase = true;
        }
        else if (result.ContentKind == PromptContentKind.CueSequence ||
            materials.Any(material => material.Kind is
                GeneratedContentMaterialKind.CueStep or
                GeneratedContentMaterialKind.ValidCue or
                GeneratedContentMaterialKind.InvalidCue or
                GeneratedContentMaterialKind.GoNoGoCue))
        {
            phases.Add(ManualPhase("cue-response", GeneratedRuntimePhaseKind.CueResponse));
            addedWorkPhase = true;
        }
        else
        {
            var encodeDuration = FirstDuration(materials, GeneratedContentMaterialKind.EncodeInstruction);
            if (materials.Any(material => material.Kind is
                    GeneratedContentMaterialKind.EncodeInstruction or
                    GeneratedContentMaterialKind.EncodeItem or
                    GeneratedContentMaterialKind.SourceItem))
            {
                phases.Add(encodeDuration.HasValue
                    ? TimedPhase("encode-window", GeneratedRuntimePhaseKind.EncodeWindow, encodeDuration.Value)
                    : ManualPhase("encode-window", GeneratedRuntimePhaseKind.EncodeWindow));
                addedWorkPhase = true;
            }

            var delayDuration = FirstDuration(materials, GeneratedContentMaterialKind.DelayLength) ??
                FirstDuration(materials, GeneratedContentMaterialKind.AuditDelay);
            if (delayDuration.HasValue)
            {
                phases.Add(TimedPhase("delay-window", GeneratedRuntimePhaseKind.DelayWindow, delayDuration.Value));
                addedWorkPhase = true;
            }

            if (materials.Any(material => material.Kind is
                    GeneratedContentMaterialKind.ReconstructionInstruction or
                    GeneratedContentMaterialKind.ExpectedReconstruction or
                    GeneratedContentMaterialKind.FinalExpectedOutput or
                    GeneratedContentMaterialKind.DelayedReconstructionPayload))
            {
                phases.Add(ManualPhase("reconstruction-input", GeneratedRuntimePhaseKind.ReconstructionInput));
                addedWorkPhase = true;
            }

            if (materials.Any(material => material.Kind is
                    GeneratedContentMaterialKind.AuditInstruction or
                    GeneratedContentMaterialKind.AuditPayload or
                    GeneratedContentMaterialKind.ExpectedFinding))
            {
                phases.Add(ManualPhase("audit", GeneratedRuntimePhaseKind.Audit));
                addedWorkPhase = true;
            }
        }

        if (!addedWorkPhase)
        {
            var holdDuration = FirstDuration(materials, GeneratedContentMaterialKind.HoldDuration);
            phases.Add(holdDuration.HasValue
                ? TimedPhase("active-work", GeneratedRuntimePhaseKind.ActiveWork, holdDuration.Value)
                : ManualPhase("active-work", GeneratedRuntimePhaseKind.ActiveWork));
        }

        AddComponentEvidencePhase(phases, materials);
        phases.Add(ManualPhase("review", GeneratedRuntimePhaseKind.Review));
        return Array.AsReadOnly(phases.ToArray());
    }

    private static void AddComponentEvidencePhase(
        ICollection<GeneratedRuntimePhaseDefinition> phases,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        if (!materials.Any(material => material.Kind == GeneratedContentMaterialKind.ComponentPayload) ||
            phases.Any(phase => phase.Kind == GeneratedRuntimePhaseKind.ReconstructionInput))
        {
            return;
        }

        phases.Add(ManualPhase("component-evidence", GeneratedRuntimePhaseKind.ReconstructionInput));
    }

    private static IReadOnlyList<GeneratedRuntimeCueDefinition> BuildCues(
        GeneratedDrillContentResult result,
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        DrillId? sourceDrill)
    {
        var cueDrill = sourceDrill ?? result.Drill;
        if (cueDrill == DrillId.FH2DistractorHold)
        {
            var distractorCues = BuildDistractorCues(materials);
            return result.Drill == DrillId.AI2DisruptionRecovery
                ? AddDisruptionCue(distractorCues, materials)
                : distractorCues;
        }

        if (result.ContentKind != PromptContentKind.CueSequence && sourceDrill is null)
        {
            return [];
        }

        var cueInterval = FirstDuration(materials, GeneratedContentMaterialKind.CueDensity) ??
            FirstDuration(materials, GeneratedContentMaterialKind.CuePace) ??
            DefaultCueInterval;
        var responseWindow = FirstDuration(
            materials,
            GeneratedContentMaterialKind.ResponseWindow,
            material => string.Equals(material.Name, "response-window", StringComparison.Ordinal)) ?? DefaultResponseWindow;
        var expectedTargets = IndexedMaterials(materials, GeneratedContentMaterialKind.ExpectedActiveTarget);
        var responseWindows = IndexedMaterials(materials, GeneratedContentMaterialKind.ResponseWindow);
        var expectedActions = IndexedMaterials(materials, GeneratedContentMaterialKind.ExpectedAction);
        var validCues = IndexedMaterials(materials, GeneratedContentMaterialKind.ValidCue);
        var invalidCues = IndexedMaterials(materials, GeneratedContentMaterialKind.InvalidCue);
        var goNoGoCues = IndexedMaterials(materials, GeneratedContentMaterialKind.GoNoGoCue);
        var genericCueSteps = IndexedMaterials(materials, GeneratedContentMaterialKind.CueStep);
        var cueSteps = validCues.Keys
            .Concat(invalidCues.Keys)
            .Concat(goNoGoCues.Keys)
            .Concat(genericCueSteps.Keys)
            .Distinct()
            .OrderBy(step => step);
        var cues = new List<GeneratedRuntimeCueDefinition>();

        foreach (var step in cueSteps)
        {
            var stepResponseWindow = responseWindows.TryGetValue(step, out var windowMaterial)
                ? ParseDuration(windowMaterial.Value) ?? responseWindow
                : responseWindow;

            if (validCues.TryGetValue(step, out var validCue))
            {
                var expectedResponse = expectedTargets.TryGetValue(step, out var expectedTarget)
                    ? expectedTarget.Value
                    : null;

                cues.Add(new GeneratedRuntimeCueDefinition(
                    validCue.Name,
                    CueKindForValidCue(cueDrill),
                    validCue.Value,
                    ScheduledOffset(cueInterval, cues.Count + 1),
                    stepResponseWindow,
                    GeneratedRuntimeCueResponseExpectation.ResponseRequired,
                    expectedResponse));
                continue;
            }

            if (invalidCues.TryGetValue(step, out var invalidCue))
            {
                cues.Add(new GeneratedRuntimeCueDefinition(
                    invalidCue.Name,
                    GeneratedRuntimeCueKind.InvalidCueFilter,
                    invalidCue.Value,
                    ScheduledOffset(cueInterval, cues.Count + 1),
                    stepResponseWindow,
                    GeneratedRuntimeCueResponseExpectation.NoResponseExpected));
                continue;
            }

            var expectedAction = expectedActions.TryGetValue(step, out var action)
                ? action.Value
                : null;
            var expectation = IsWithholdAction(expectedAction)
                ? GeneratedRuntimeCueResponseExpectation.NoResponseExpected
                : GeneratedRuntimeCueResponseExpectation.ResponseRequired;

            if (goNoGoCues.TryGetValue(step, out var goNoGoCue))
            {
                cues.Add(new GeneratedRuntimeCueDefinition(
                    goNoGoCue.Name,
                    GeneratedRuntimeCueKind.GoNoGo,
                    goNoGoCue.Value,
                    ScheduledOffset(cueInterval, cues.Count + 1),
                    stepResponseWindow,
                    expectation,
                    expectation == GeneratedRuntimeCueResponseExpectation.ResponseRequired
                        ? ExpectedResponseToken(expectedAction)
                        : null));
                continue;
            }

            if (!genericCueSteps.TryGetValue(step, out var genericCue))
            {
                continue;
            }

            cues.Add(new GeneratedRuntimeCueDefinition(
                genericCue.Name,
                GeneratedRuntimeCueKind.TimedResponse,
                genericCue.Value,
                ScheduledOffset(cueInterval, cues.Count + 1),
                stepResponseWindow,
                expectation,
                expectation == GeneratedRuntimeCueResponseExpectation.ResponseRequired
                    ? ExpectedResponseToken(expectedAction)
                    : null));
        }

        var ordered = Array.AsReadOnly(cues
            .OrderBy(cue => cue.ScheduledAtOffset)
            .ThenBy(cue => cue.Id, StringComparer.Ordinal)
            .ToArray());
        return result.Drill == DrillId.AI2DisruptionRecovery
            ? AddDisruptionCue(ordered, materials)
            : ordered;
    }

    private static IReadOnlyList<GeneratedRuntimeCueDefinition> AddDisruptionCue(
        IReadOnlyList<GeneratedRuntimeCueDefinition> sourceCues,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var disruption = materials.Single(material =>
            material.Kind == GeneratedContentMaterialKind.DisruptionEvent);
        var recoveryWindow = FirstDuration(materials, GeneratedContentMaterialKind.RecoveryWindow) ??
            TimeSpan.FromSeconds(30);
        var insertionIndex = Math.Min(2, sourceCues.Count);
        var priorOffset = insertionIndex == 0
            ? TimeSpan.Zero
            : sourceCues[insertionIndex - 1].ScheduledAtOffset;
        var disruptionOffset = priorOffset + TimeSpan.FromSeconds(3);
        var shifted = sourceCues
            .Select((cue, index) => index < insertionIndex
                ? cue
                : new GeneratedRuntimeCueDefinition(
                    cue.Id,
                    cue.Kind,
                    cue.Value,
                    cue.ScheduledAtOffset + recoveryWindow,
                    cue.ResponseWindow,
                    cue.ResponseExpectation,
                    cue.ExpectedResponse))
            .ToList();
        shifted.Add(new GeneratedRuntimeCueDefinition(
            "controlled-disruption",
            GeneratedRuntimeCueKind.Interruption,
            disruption.Value,
            disruptionOffset,
            recoveryWindow,
            GeneratedRuntimeCueResponseExpectation.ResponseRequired,
            "resume"));

        return Array.AsReadOnly(shifted
            .OrderBy(cue => cue.ScheduledAtOffset)
            .ThenBy(cue => cue.Id, StringComparer.Ordinal)
            .ToArray());
    }

    private static DrillId? SourceDrillFor(
        GeneratedDrillContentResult result,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        if (result.Branch != BranchCode.AI)
        {
            return null;
        }

        var value = materials.Single(material =>
            material.Kind == GeneratedContentMaterialKind.SourceDrill).Value;
        if (!Enum.TryParse<DrillId>(value, out var sourceDrill) || sourceDrill is not
            (DrillId.FH2DistractorHold or DrillId.FS2InvalidCueFilter or DrillId.IR2ExceptionRule))
        {
            throw new InvalidOperationException(
                "Affective Interference content must identify an executable foundational source drill.");
        }

        return sourceDrill;
    }

    private static IReadOnlyList<GeneratedRuntimeCueDefinition> BuildDistractorCues(
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var distractors = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.DistractorPrompt)
            .OrderBy(material => material.Name, StringComparer.Ordinal)
            .ToArray();
        if (distractors.Length == 0)
        {
            return [];
        }

        var holdDuration = FirstDuration(materials, GeneratedContentMaterialKind.HoldDuration) ??
            TimeSpan.FromMinutes(5);
        var interval = TimeSpan.FromTicks(holdDuration.Ticks / (distractors.Length + 1));
        return Array.AsReadOnly(distractors
            .Select((material, index) => new GeneratedRuntimeCueDefinition(
                material.Name,
                GeneratedRuntimeCueKind.Interruption,
                material.Value,
                ScheduledOffset(interval, index + 1),
                TimeSpan.FromSeconds(5),
                GeneratedRuntimeCueResponseExpectation.NoResponseExpected))
            .ToArray());
    }

    private static GeneratedRuntimeCueKind CueKindForValidCue(DrillId drill)
    {
        return drill switch
        {
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter => GeneratedRuntimeCueKind.FocusShift,
            DrillId.IR1GoNoGoRule => GeneratedRuntimeCueKind.GoNoGo,
            _ => GeneratedRuntimeCueKind.TimedResponse,
        };
    }

    private static bool IsWithholdAction(string? expectedAction)
    {
        if (string.IsNullOrWhiteSpace(expectedAction))
        {
            return false;
        }

        var normalized = expectedAction.Trim();
        return normalized.StartsWith("withhold", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(": withhold", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExpectedResponseToken(string? expectedAction)
    {
        if (string.IsNullOrWhiteSpace(expectedAction))
        {
            return "respond";
        }

        var action = expectedAction;
        var colon = action.IndexOf(':');
        if (colon >= 0 && colon < action.Length - 1)
        {
            action = action[(colon + 1)..];
        }

        var within = action.IndexOf(" within", StringComparison.OrdinalIgnoreCase);
        if (within >= 0)
        {
            action = action[..within];
        }

        var instead = action.IndexOf(" instead", StringComparison.OrdinalIgnoreCase);
        if (instead >= 0)
        {
            action = action[..instead];
        }

        var token = action.Trim();
        return token.Length == 0 ? "respond" : token;
    }

    private static TimeSpan ScheduledOffset(TimeSpan interval, int oneBasedPosition)
    {
        return TimeSpan.FromTicks(interval.Ticks * oneBasedPosition);
    }

    private static IReadOnlyDictionary<int, GeneratedContentMaterial> IndexedMaterials(
        IEnumerable<GeneratedContentMaterial> materials,
        GeneratedContentMaterialKind kind)
    {
        return materials
            .Where(material => material.Kind == kind)
            .Select(material => (Index: ParseTrailingNumber(material.Name), Material: material))
            .Where(item => item.Index.HasValue)
            .GroupBy(item => item.Index!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Material.Name, StringComparer.Ordinal).First().Material);
    }

    private static int? ParseTrailingNumber(string value)
    {
        var digits = new string(value
            .Reverse()
            .TakeWhile(char.IsDigit)
            .Reverse()
            .ToArray());

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static IEnumerable<GeneratedContentPayloadFact> ExpectedEvidenceFacts(GeneratedDrillContentResult result)
    {
        return result.PayloadFacts.Where(fact =>
            fact.Name.Contains("evidence", StringComparison.OrdinalIgnoreCase) ||
            fact.Name.Contains("policy", StringComparison.OrdinalIgnoreCase) ||
            fact.Name.Contains("expected", StringComparison.OrdinalIgnoreCase) ||
            fact.Name.Contains("constraint", StringComparison.OrdinalIgnoreCase) ||
            fact.Value.Contains("evidence", StringComparison.OrdinalIgnoreCase) ||
            fact.Value.Contains("standard", StringComparison.OrdinalIgnoreCase));
    }

    private static TimeSpan? FirstDuration(
        IEnumerable<GeneratedContentMaterial> materials,
        GeneratedContentMaterialKind kind,
        Func<GeneratedContentMaterial, bool>? predicate = null)
    {
        var material = materials.FirstOrDefault(material =>
            material.Kind == kind &&
            (predicate is null || predicate(material)));

        return material is null ? null : ParseDuration(material.Value);
    }

    private static TimeSpan? ParseDuration(string value)
    {
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > TimeSpan.Zero)
        {
            return parsed;
        }

        var trimmed = value.Trim();
        var digits = new string(trimmed.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length == 0 ||
            !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0)
        {
            return null;
        }

        var unit = trimmed[digits.Length..].Trim().TrimStart('-').TrimStart().ToLowerInvariant();
        if (unit.StartsWith("second", StringComparison.Ordinal) ||
            unit is "s" or "sec" or "secs")
        {
            return TimeSpan.FromSeconds(amount);
        }

        if (unit.StartsWith("minute", StringComparison.Ordinal) ||
            unit is "m" or "min" or "mins")
        {
            return TimeSpan.FromMinutes(amount);
        }

        return null;
    }

    private static GeneratedRuntimePhaseDefinition ManualPhase(
        string id,
        GeneratedRuntimePhaseKind kind)
    {
        return new GeneratedRuntimePhaseDefinition(
            id,
            kind,
            GeneratedRuntimePhaseCompletionRule.Manual);
    }

    private static GeneratedRuntimePhaseDefinition TimedPhase(
        string id,
        GeneratedRuntimePhaseKind kind,
        TimeSpan duration)
    {
        return new GeneratedRuntimePhaseDefinition(
            id,
            kind,
            GeneratedRuntimePhaseCompletionRule.Timed,
            duration);
    }

    private static GeneratedRuntimePhaseDefinition ManualOrTimedPhase(
        string id,
        GeneratedRuntimePhaseKind kind,
        TimeSpan duration)
    {
        return new GeneratedRuntimePhaseDefinition(
            id,
            kind,
            GeneratedRuntimePhaseCompletionRule.ManualOrTimed,
            duration);
    }
}
