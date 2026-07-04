using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class AffectiveInterferenceGeneratedContent
{
    internal AffectiveInterferenceGeneratedContent(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);

        var materialArray = materials.ToArray();
        Result = result;
        Materials = Array.AsReadOnly(materialArray);
        ValidatedContent = ValidatedGeneratedDrillContent.Create(result, materialArray);
    }

    public GeneratedDrillContentResult Result { get; }

    public IReadOnlyList<GeneratedContentMaterial> Materials { get; }

    public ValidatedGeneratedDrillContent ValidatedContent { get; }

    public bool CanBeConsumedByRuntime => ValidatedContent.CanBeConsumedByRuntime;

    public bool CanBeRecordedByPersistence => Result.CanBeRecordedByPersistence;

    public bool GrantsAdvancement => false;
}

public static class AffectiveInterferenceGeneratedContentGenerator
{
    private const string OriginalStandardCannotBeLoweredConstraint = "Original standard cannot be lowered.";
    private const string FullRestartProhibitedConstraint = "Full restart prohibited unless specified.";
    private const string DefaultTimePressure = "mild countdown";
    private const string DefaultEvaluativePressure = "visible score review";
    private const string DefaultInterruptionTiming = "after first stable segment";
    private const string DefaultRestartDelay = "10 seconds";
    private const string DefaultTaskComplexity = "source task baseline complexity";
    private const string DefaultRecoveryWindow = "30 seconds";

    private static readonly SourceStandardTemplate[] SourceStandards =
    [
        new("FH-L3", BranchCode.FH, GlobalLevelId.L3, DrillId.FH2DistractorHold),
        new("FS-L3", BranchCode.FS, GlobalLevelId.L3, DrillId.FS2InvalidCueFilter),
        new("IR-L3", BranchCode.IR, GlobalLevelId.L3, DrillId.IR2ExceptionRule),
    ];

    private static readonly PressureSourceTemplate[] PressureSources =
    [
        new(
            "visible-countdown",
            "defined pressure source: visible countdown label while the original source task is performed",
            "countdown is visible but does not hide the source task, scoring key, or artifact capture"),
        new(
            "observer-review",
            "defined pressure source: visible evaluator note that the artifact will be reviewed after the set",
            "review label is declared before the set and leaves the same source-task evidence fields intact"),
        new(
            "consequence-simulation",
            "defined pressure source: simulated consequence flag attached to the set result",
            "simulated consequence is recorded separately and cannot replace source-task errors or omissions"),
    ];

    private static readonly DisruptionEventTemplate[] DisruptionEvents =
    [
        new(
            "rule-interruption",
            "disruption event: rule-interruption cue interrupts active work; underlying source task continues after acknowledgement"),
        new(
            "context-switch",
            "disruption event: context-switch cue appears between source-task steps; underlying source task continues without reset"),
        new(
            "artifact-check",
            "disruption event: artifact-check prompt requests a brief mark; underlying source task continues with prior evidence intact"),
    ];

    public static AffectiveInterferenceGeneratedContent Generate(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(seed);

        EnsureSupportedAffectiveInterferenceRequest(request);

        var seedPlan = GeneratedContentSeedDeriver.Derive(request, seed);
        IReadOnlyList<GeneratedContentMaterial> materials;
        IEnumerable<GeneratedContentPayloadFact> payloadFacts;

        if (request.Drill == DrillId.AI2DisruptionRecovery)
        {
            var plan = BuildDisruptionRecoveryPlan(request, seedPlan);
            materials = BuildDisruptionRecoveryMaterials(request, plan);
            payloadFacts = BuildDisruptionRecoveryPayloadFacts(materials);
        }
        else
        {
            var plan = BuildPressureRepeatPlan(request, seedPlan);
            materials = BuildPressureRepeatMaterials(request, plan);
            payloadFacts = BuildPressureRepeatPayloadFacts(materials);
        }

        var result = new GeneratedDrillContentResult(
            request,
            seedPlan.Instance,
            seedPlan.PayloadFacts.Concat(payloadFacts));

        return new AffectiveInterferenceGeneratedContent(result, materials);
    }

    private static void EnsureSupportedAffectiveInterferenceRequest(GeneratedDrillContentRequest request)
    {
        if (request.Branch != BranchCode.AI)
        {
            throw new ArgumentException(
                "Affective Interference generated content can only be produced for the AI branch.",
                nameof(request));
        }

        if (request.Drill is not (DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery))
        {
            throw new ArgumentException(
                "Affective Interference generated content supports only AI-1 Pressure Repeat and AI-2 Disruption Recovery.",
                nameof(request));
        }

        if (request.ContentKind != PromptContentKind.EquivalentPrompt)
        {
            throw new ArgumentException(
                $"Drill {request.Drill} requires generated content kind {PromptContentKind.EquivalentPrompt}.",
                nameof(request));
        }
    }

    private static PressureRepeatPlan BuildPressureRepeatPlan(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan)
    {
        return new PressureRepeatPlan(
            SelectSourceStandard(request),
            SelectPressureSource(seedPlan));
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildPressureRepeatMaterials(
        GeneratedDrillContentRequest request,
        PressureRepeatPlan plan)
    {
        var materials = new List<GeneratedContentMaterial>();

        foreach (var loadVariable in request.LoadVariables)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.LoadVariable,
                loadVariable.Name,
                loadVariable.Value));
        }

        AddHonestyConstraints(materials, request);

        var sourceStandardValue = BuildSourceBranchStandardValue(plan.SourceStandard);
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.SourceBranchStandard,
            "source-branch-standard",
            sourceStandardValue));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.SourceTask,
            "source-content-reference",
            $"source content reference {plan.SourceStandard.StandardId}: host must attach or reuse content for {plan.SourceStandard.Drill}; this pressure wrapper does not replace the source task or lower its demand."));

        AddPressureMetadataMaterials(materials, request);

        var pressureMetadataSummary = BuildPressureMetadataSummary(materials);
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.PressureSource,
            "pressure-source",
            $"{plan.PressureSource.Description}; pressure metadata: {pressureMetadataSummary}; clean evidence collection remains possible because {plan.PressureSource.CleanEvidenceGuard}."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.NoStandardLoweringMarker,
            "no-standard-lowering-marker",
            $"original standard cannot be lowered: {plan.SourceStandard.StandardId} remains the scoring standard; pressure cannot excuse errors, missing artifacts, skipped constraints, or abandoned evidence."));

        return Array.AsReadOnly(materials.ToArray());
    }

    private static DisruptionRecoveryPlan BuildDisruptionRecoveryPlan(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan)
    {
        return new DisruptionRecoveryPlan(
            SelectSourceStandard(request),
            SelectDisruptionEvent(seedPlan),
            LoadValueOrDefault(request, "interruption timing", DefaultInterruptionTiming),
            LoadValueOrDefault(request, "restart delay", DefaultRestartDelay),
            LoadValueOrDefault(request, "task complexity", DefaultTaskComplexity),
            LoadValueOrDefault(request, "recovery window", DefaultRecoveryWindow));
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildDisruptionRecoveryMaterials(
        GeneratedDrillContentRequest request,
        DisruptionRecoveryPlan plan)
    {
        var materials = new List<GeneratedContentMaterial>();

        foreach (var loadVariable in request.LoadVariables)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.LoadVariable,
                loadVariable.Name,
                loadVariable.Value));
        }

        AddHonestyConstraints(materials, request);

        var sourceStandardValue = BuildSourceBranchStandardValue(plan.SourceStandard);
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.SourceBranchStandard,
            "source-branch-standard",
            sourceStandardValue));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.SourceTask,
            "source-task",
            $"source task {plan.SourceStandard.StandardId}: branch {plan.SourceStandard.Branch}; level {plan.SourceStandard.Level}; drill {plan.SourceStandard.Drill}; underlying branch demand and source standard remain visible; task complexity {plan.TaskComplexity}; this disruption wrapper does not replace, reset, or reduce the source task."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DisruptionEvent,
            plan.DisruptionEvent.EventId,
            $"{plan.DisruptionEvent.Description}; underlying source task continues and evidence before disruption remains in scope."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DisruptionTiming,
            "interruption-timing",
            plan.InterruptionTiming));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RestartDelay,
            "restart-delay",
            plan.RestartDelay));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TaskComplexity,
            "task-complexity",
            plan.TaskComplexity));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RestartRule,
            "restart-rule",
            $"full restart prohibited unless specified; restart delay metadata {plan.RestartDelay}; resume the same source task without resetting evidence or changing the source standard."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RecoveryWindow,
            "recovery-window",
            plan.RecoveryWindow));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.PostDisruptionEvidence,
            "post-disruption-evidence",
            $"record recovery time within {plan.RecoveryWindow}; post-disruption errors; post-error cascade; whether the source standard remained visible; no full restart."));

        return Array.AsReadOnly(materials.ToArray());
    }

    private static void AddHonestyConstraints(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedDrillContentRequest request)
    {
        var added = new HashSet<string>(StringComparer.Ordinal);
        var index = 1;
        foreach (var constraint in request.CriticalConstraints)
        {
            if (added.Add(constraint.Description))
            {
                materials.Add(new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.HonestyConstraint,
                    $"request-constraint-{index.ToString(CultureInfo.InvariantCulture)}",
                    constraint.Description));
            }

            index++;
        }

        var defaultConstraint = request.Drill == DrillId.AI2DisruptionRecovery
            ? FullRestartProhibitedConstraint
            : OriginalStandardCannotBeLoweredConstraint;

        if (!added.Add(defaultConstraint))
        {
            return;
        }

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.HonestyConstraint,
            request.Drill == DrillId.AI2DisruptionRecovery
                ? "full-restart-prohibited"
                : "original-standard-not-lowered",
            defaultConstraint));
    }

    private static void AddPressureMetadataMaterials(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedDrillContentRequest request)
    {
        var added = new HashSet<string>(StringComparer.Ordinal);
        foreach (var loadVariable in request.LoadVariables)
        {
            var normalizedName = loadVariable.Name.Trim().ToLowerInvariant();
            var materialKind = normalizedName switch
            {
                "time pressure" or "time limit" => GeneratedContentMaterialKind.TimePressure,
                "observation" or "evaluative pressure" => GeneratedContentMaterialKind.EvaluativePressure,
                "frustration" => GeneratedContentMaterialKind.FrustrationPressure,
                "uncertainty" => GeneratedContentMaterialKind.UncertaintyPressure,
                _ => (GeneratedContentMaterialKind?)null,
            };

            if (materialKind is null)
            {
                continue;
            }

            AddPressureMetadataMaterial(
                materials,
                added,
                materialKind.Value,
                StableMaterialName(materialKind.Value, added.Count + 1),
                loadVariable.Value);
        }

        if (!materials.Any(material => material.Kind == GeneratedContentMaterialKind.TimePressure))
        {
            AddPressureMetadataMaterial(
                materials,
                added,
                GeneratedContentMaterialKind.TimePressure,
                "time-pressure-default",
                DefaultTimePressure);
        }

        if (!materials.Any(material => material.Kind == GeneratedContentMaterialKind.EvaluativePressure))
        {
            AddPressureMetadataMaterial(
                materials,
                added,
                GeneratedContentMaterialKind.EvaluativePressure,
                "evaluative-pressure-default",
                DefaultEvaluativePressure);
        }
    }

    private static void AddPressureMetadataMaterial(
        ICollection<GeneratedContentMaterial> materials,
        ISet<string> added,
        GeneratedContentMaterialKind kind,
        string name,
        string value)
    {
        var key = string.Join("|", kind.ToString(), value);
        if (!added.Add(key))
        {
            return;
        }

        materials.Add(new GeneratedContentMaterial(kind, name, value));
    }

    private static string BuildPressureMetadataSummary(IEnumerable<GeneratedContentMaterial> materials)
    {
        var pressureMaterials = materials
            .Where(material => material.Kind is
                GeneratedContentMaterialKind.TimePressure or
                GeneratedContentMaterialKind.EvaluativePressure or
                GeneratedContentMaterialKind.FrustrationPressure or
                GeneratedContentMaterialKind.UncertaintyPressure)
            .Select(material => $"{material.Kind}={material.Value}")
            .ToArray();

        return string.Join("; ", pressureMaterials);
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildPressureRepeatPayloadFacts(
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var sourceStandard = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.SourceBranchStandard)
            .Value;
        var sourceReference = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.SourceTask)
            .Value;
        var pressureSource = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.PressureSource)
            .Value;
        var noStandardLowering = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.NoStandardLoweringMarker)
            .Value;
        var pressureMetadata = BuildPressureMetadataSummary(materials);

        yield return new GeneratedContentPayloadFact("payload-family", "ai-pressure-repeat");
        yield return new GeneratedContentPayloadFact("source-branch-standard", sourceStandard);
        yield return new GeneratedContentPayloadFact("source-content-reference", sourceReference);
        yield return new GeneratedContentPayloadFact("pressure-source", pressureSource);
        yield return new GeneratedContentPayloadFact("pressure-metadata", pressureMetadata);
        yield return new GeneratedContentPayloadFact("no-standard-lowering-evidence", noStandardLowering);
        yield return new GeneratedContentPayloadFact(
            "source-standard-visibility",
            "the original branch standard remains visible before, during, and after pressure-repeat execution");
        yield return new GeneratedContentPayloadFact(
            "clean-evidence-collection-policy",
            "pressure content must preserve clean evidence collection and cannot hide scores, artifacts, errors, or critical-constraint breaches");
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildDisruptionRecoveryPayloadFacts(
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var sourceStandard = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.SourceBranchStandard)
            .Value;
        var sourceTask = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.SourceTask)
            .Value;
        var disruptionEvent = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.DisruptionEvent)
            .Value;
        var interruptionTiming = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.DisruptionTiming)
            .Value;
        var restartDelay = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.RestartDelay)
            .Value;
        var taskComplexity = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.TaskComplexity)
            .Value;
        var restartRule = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.RestartRule)
            .Value;
        var recoveryWindow = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.RecoveryWindow)
            .Value;
        var postDisruptionEvidence = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.PostDisruptionEvidence)
            .Value;

        yield return new GeneratedContentPayloadFact("payload-family", "ai-disruption-recovery");
        yield return new GeneratedContentPayloadFact("source-branch-standard", sourceStandard);
        yield return new GeneratedContentPayloadFact("source-task", sourceTask);
        yield return new GeneratedContentPayloadFact("disruption-event", disruptionEvent);
        yield return new GeneratedContentPayloadFact("interruption-timing", interruptionTiming);
        yield return new GeneratedContentPayloadFact("restart-delay", restartDelay);
        yield return new GeneratedContentPayloadFact("task-complexity", taskComplexity);
        yield return new GeneratedContentPayloadFact("restart-rule", restartRule);
        yield return new GeneratedContentPayloadFact("recovery-window", recoveryWindow);
        yield return new GeneratedContentPayloadFact("post-disruption-evidence", postDisruptionEvidence);
        yield return new GeneratedContentPayloadFact("restart-prohibition-evidence", restartRule);
        yield return new GeneratedContentPayloadFact(
            "source-demand-preservation",
            $"underlying branch demand preserved through disruption wrapper: {sourceTask}");
    }

    private static SourceStandardTemplate SelectSourceStandard(GeneratedDrillContentRequest request)
    {
        var equivalenceClass = request.EquivalenceClass;
        if (equivalenceClass.Contains("fh", StringComparison.OrdinalIgnoreCase))
        {
            return SourceStandards.Single(source => source.Branch == BranchCode.FH);
        }

        if (equivalenceClass.Contains("fs", StringComparison.OrdinalIgnoreCase))
        {
            return SourceStandards.Single(source => source.Branch == BranchCode.FS);
        }

        if (equivalenceClass.Contains("ir", StringComparison.OrdinalIgnoreCase))
        {
            return SourceStandards.Single(source => source.Branch == BranchCode.IR);
        }

        var index = SelectIndex(equivalenceClass, "source-standard", variantIndex: 0, SourceStandards.Length);
        return SourceStandards[index];
    }

    private static PressureSourceTemplate SelectPressureSource(GeneratedContentSeedPlan seedPlan)
    {
        var baseIndex = SelectIndex(
            seedPlan.RequestFingerprint,
            "pressure-source",
            variantIndex: 0,
            PressureSources.Length);

        return PressureSources[(baseIndex + seedPlan.VariantIndex) % PressureSources.Length];
    }

    private static DisruptionEventTemplate SelectDisruptionEvent(GeneratedContentSeedPlan seedPlan)
    {
        var baseIndex = SelectIndex(
            seedPlan.RequestFingerprint,
            "disruption-event",
            variantIndex: 0,
            DisruptionEvents.Length);

        return DisruptionEvents[(baseIndex + seedPlan.VariantIndex) % DisruptionEvents.Length];
    }

    private static string BuildSourceBranchStandardValue(SourceStandardTemplate source)
    {
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == source.Branch &&
            item.Level == source.Level);
        var drill = ProgramCatalog.Drills.Single(item => item.Id == source.Drill);

        return $"original branch standard {source.StandardId}: branch {source.Branch}; level {source.Level}; drill {source.Drill}; demand {standard.Demand}; standard {standard.Standard}; source honesty constraint {drill.HonestyConstraint}; pressure repeat requires this branch standard remains visible and passing.";
    }

    private static string LoadValueOrDefault(
        GeneratedDrillContentRequest request,
        string name,
        string defaultValue)
    {
        return request.LoadVariables
            .FirstOrDefault(variable => string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Value ?? defaultValue;
    }

    private static int SelectIndex(
        string seedMaterial,
        string purpose,
        int variantIndex,
        int length)
    {
        var hash = GeneratedContentStableHash.HashSegment(
            string.Join("|", seedMaterial, purpose, variantIndex.ToString(CultureInfo.InvariantCulture)));
        var baseIndex = Convert.ToInt32(hash[..6], 16);

        return (baseIndex + variantIndex) % length;
    }

    private static string StableMaterialName(
        GeneratedContentMaterialKind kind,
        int sequence)
    {
        var suffix = sequence.ToString(CultureInfo.InvariantCulture);
        return kind switch
        {
            GeneratedContentMaterialKind.TimePressure => "time-pressure-" + suffix,
            GeneratedContentMaterialKind.EvaluativePressure => "evaluative-pressure-" + suffix,
            GeneratedContentMaterialKind.FrustrationPressure => "frustration-pressure-" + suffix,
            GeneratedContentMaterialKind.UncertaintyPressure => "uncertainty-pressure-" + suffix,
            _ => "pressure-metadata-" + suffix,
        };
    }

    private sealed record SourceStandardTemplate(
        string StandardId,
        BranchCode Branch,
        GlobalLevelId Level,
        DrillId Drill);

    private sealed record PressureSourceTemplate(
        string SourceId,
        string Description,
        string CleanEvidenceGuard);

    private sealed record DisruptionEventTemplate(
        string EventId,
        string Description);

    private sealed record PressureRepeatPlan(
        SourceStandardTemplate SourceStandard,
        PressureSourceTemplate PressureSource);

    private sealed record DisruptionRecoveryPlan(
        SourceStandardTemplate SourceStandard,
        DisruptionEventTemplate DisruptionEvent,
        string InterruptionTiming,
        string RestartDelay,
        string TaskComplexity,
        string RecoveryWindow);
}
