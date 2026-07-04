using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class TransferIntegrationGeneratedContent
{
    internal TransferIntegrationGeneratedContent(
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

public static class TransferIntegrationGeneratedContentGenerator
{
    private const string SeparateEvidenceConstraint = "Each branch must leave separate evidence.";
    private const string AuditAndDelayedReconstructionConstraint = "Audit and delayed reconstruction are required.";
    private const string DefaultTaskLength = "10 minutes";
    private const string DefaultTransferDistance = "near transfer";
    private const string DefaultGlobalReviewTaskLength = "15 minutes";
    private const string DefaultGlobalReviewPressure = "visible review pressure";
    private const string DefaultGlobalReviewAmbiguity = "moderate ambiguity";
    private const string DefaultGlobalReviewDelay = "5 minutes";

    private static readonly ComponentTemplate[] ComponentTemplates =
    [
        new(
            BranchCode.FH,
            GlobalLevelId.L3,
            DrillId.FH2DistractorHold,
            "hold target while the composite task runs",
            "drift marks, return timing, distractor no-response record, and unchanged target",
            "FH score uses drift threshold and return timing; it cannot be replaced by composite total"),
        new(
            BranchCode.FS,
            GlobalLevelId.L3,
            DrillId.FS2InvalidCueFilter,
            "switch between active component tracks only on valid cues",
            "sequence accuracy, valid cue responses, invalid cue inhibition, and anticipatory-switch log",
            "FS score uses valid response and invalid inhibition thresholds; it cannot be replaced by composite total"),
        new(
            BranchCode.WM,
            GlobalLevelId.L3,
            DrillId.WM2MentalTransform,
            "hold and transform source items while another component remains active",
            "reconstruction accuracy, operation explanation, source-item fidelity, and hidden-note check",
            "WM score uses reconstruction and operation accuracy; it cannot be replaced by composite total"),
        new(
            BranchCode.IR,
            GlobalLevelId.L3,
            DrillId.IR2ExceptionRule,
            "preserve the stated rule while composite cues create temptation",
            "rule statement, exception handling, premature-response count, and post-error correction window",
            "IR score uses rule fidelity and premature-response evidence; it cannot be replaced by composite total"),
        new(
            BranchCode.DE,
            GlobalLevelId.L3,
            DrillId.DE2SeededAudit,
            "audit component output without editing the original artifact",
            "seeded findings, false corrections, uncertainty marks, and original-output lock",
            "DE score uses audit findings and false-correction evidence; it cannot be replaced by composite total"),
    ];

    private static readonly ComponentTemplate[] GlobalReviewComponentTemplates =
    [
        new(
            BranchCode.FH,
            GlobalLevelId.L5,
            DrillId.FH2DistractorHold,
            "maintain the declared target while the global review task runs",
            "target preservation, drift marks, return timing, and no pressure-driven target substitution",
            "FH score uses hold stability and target preservation; it cannot be replaced by composite total"),
        new(
            BranchCode.FS,
            GlobalLevelId.L5,
            DrillId.FS2InvalidCueFilter,
            "switch only on valid review cues while ignoring invalid cue pressure",
            "switch log, valid cue obedience, invalid cue inhibition, and no anticipatory switching",
            "FS score uses switch fidelity and invalid-cue inhibition; it cannot be replaced by composite total"),
        new(
            BranchCode.WM,
            GlobalLevelId.L5,
            DrillId.WM2MentalTransform,
            "preserve critical information for delayed reconstruction after the review task",
            "delayed reconstruction accuracy, critical omission list, memory gap markers, and hidden-note check",
            "WM score uses delayed reconstruction and critical omission evidence; it cannot be replaced by composite total"),
        new(
            BranchCode.IR,
            GlobalLevelId.L5,
            DrillId.IR2ExceptionRule,
            "preserve stated rules under interruption and time pressure",
            "rule statement, exception handling, premature-response count, and post-error cascade evidence",
            "IR score uses rule fidelity and premature-response evidence; it cannot be replaced by composite total"),
        new(
            BranchCode.DE,
            GlobalLevelId.L5,
            DrillId.DE2SeededAudit,
            "audit the composite artifact without editing the original output",
            "audit findings, critical error detection, false corrections, and original-output lock",
            "DE score uses audit findings and false-correction evidence; it cannot be replaced by composite total"),
        new(
            BranchCode.CO,
            GlobalLevelId.L5,
            DrillId.CO2StructureMapping,
            "name the model relations and reject surface-only matches inside the review task",
            "relation names, critical assumptions, prediction test result, and unsupported inference markers",
            "CO score uses relation preservation and prediction evidence; it cannot be replaced by composite total"),
        new(
            BranchCode.AI,
            GlobalLevelId.L5,
            DrillId.AI1PressureRepeat,
            "keep the original branch standards passing under the declared pressure source",
            "source standard visibility, pressure source record, no-standard-lowering marker, and pressure-rule integrity",
            "AI score uses pressure-stable execution against the original standards; it cannot be replaced by composite total"),
    ];

    private static readonly CompositeTaskFrame[] TaskFrames =
    [
        new(
            "dual-track-check",
            "composite task frame dual-track-check: maintain two component tracks and submit branch-separated evidence"),
        new(
            "interleaved-output",
            "composite task frame interleaved-output: alternate component work while preserving separate branch artifacts"),
        new(
            "shared-artifact-audit",
            "composite task frame shared-artifact-audit: produce one artifact with explicit component boundaries and branch scoring keys"),
    ];

    public static TransferIntegrationGeneratedContent Generate(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(seed);

        EnsureSupportedTransferIntegrationRequest(request);

        var seedPlan = GeneratedContentSeedDeriver.Derive(request, seed);
        var components = request.Drill == DrillId.TI2GlobalReviewTask
            ? SelectGlobalReviewComponents(request, seedPlan)
            : SelectComponents(request, seedPlan);
        var taskFrame = SelectTaskFrame(seedPlan);
        var materials = request.Drill == DrillId.TI2GlobalReviewTask
            ? BuildGlobalReviewMaterials(request, components, taskFrame)
            : BuildMaterials(request, components, taskFrame);
        var payloadFacts = request.Drill == DrillId.TI2GlobalReviewTask
            ? BuildGlobalReviewPayloadFacts(components, materials)
            : BuildPayloadFacts(components, materials);
        var result = new GeneratedDrillContentResult(
            request,
            seedPlan.Instance,
            seedPlan.PayloadFacts.Concat(payloadFacts));

        return new TransferIntegrationGeneratedContent(result, materials);
    }

    private static void EnsureSupportedTransferIntegrationRequest(GeneratedDrillContentRequest request)
    {
        if (request.Branch != BranchCode.TI)
        {
            throw new ArgumentException(
                "Transfer Integration generated content can only be produced for the TI branch.",
                nameof(request));
        }

        if (request.Drill is not DrillId.TI1CompositeTask and not DrillId.TI2GlobalReviewTask)
        {
            throw new ArgumentException(
                "Transfer Integration generated content currently supports TI-1 Composite Task and TI-2 Global Review Task.",
                nameof(request));
        }

        if (request.ContentKind != PromptContentKind.EquivalentPrompt)
        {
            throw new ArgumentException(
                $"Drill {request.Drill} requires generated content kind {PromptContentKind.EquivalentPrompt}.",
                nameof(request));
        }
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildMaterials(
        GeneratedDrillContentRequest request,
        IReadOnlyList<ComponentTemplate> components,
        CompositeTaskFrame taskFrame)
    {
        var materials = new List<GeneratedContentMaterial>();

        foreach (var loadVariable in request.LoadVariables)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.LoadVariable,
                loadVariable.Name,
                loadVariable.Value));
        }

        AddHonestyConstraints(materials, request, "separate-component-evidence", SeparateEvidenceConstraint);

        var taskLength = LoadValueOrDefault(request, "task length", DefaultTaskLength);
        var transferDistance = LoadValueOrDefault(request, "transfer distance", DefaultTransferDistance);
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TaskLength,
            "task-length",
            taskLength));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TransferDistance,
            "transfer-distance",
            transferDistance));

        AddOptionalExactLoadMaterial(
            materials,
            request,
            "domain distance",
            GeneratedContentMaterialKind.DomainDistance,
            "domain-distance");
        AddOptionalExactLoadMaterial(
            materials,
            request,
            "interference",
            GeneratedContentMaterialKind.Interference,
            "interference");

        foreach (var component in components)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentPayload,
                ComponentMaterialName("component", component.Branch),
                BuildComponentPayloadValue(component)));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                ComponentMaterialName("component-evidence", component.Branch),
                $"branch {component.Branch} required evidence: {component.EvidenceRequirement}; missing this branch evidence fails component visibility."));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.BranchScoringKey,
                ComponentMaterialName("branch-scoring", component.Branch),
                $"branch {component.Branch} scoring key: {component.ScoringKey}; strong branch cannot hide missing or failing {component.Branch} evidence."));
        }

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.CompositeTaskPrompt,
            "composite-task-prompt",
            BuildCompositeTaskPrompt(taskFrame, components, taskLength, transferDistance)));

        return Array.AsReadOnly(materials.ToArray());
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildGlobalReviewMaterials(
        GeneratedDrillContentRequest request,
        IReadOnlyList<ComponentTemplate> components,
        CompositeTaskFrame taskFrame)
    {
        var materials = new List<GeneratedContentMaterial>();

        foreach (var loadVariable in request.LoadVariables)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.LoadVariable,
                loadVariable.Name,
                loadVariable.Value));
        }

        AddHonestyConstraints(
            materials,
            request,
            "audit-delayed-reconstruction-required",
            AuditAndDelayedReconstructionConstraint);

        var taskLength = LoadValueOrDefault(request, "task length", DefaultGlobalReviewTaskLength);
        var pressure = LoadValueOrDefault(
            request,
            "pressure",
            LoadValueOrDefault(request, "pressure source", DefaultGlobalReviewPressure));
        var ambiguity = LoadValueOrDefault(
            request,
            "ambiguity",
            LoadValueOrDefault(request, "rule ambiguity", DefaultGlobalReviewAmbiguity));
        var delay = LoadValueOrDefault(request, "delay", DefaultGlobalReviewDelay);

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TaskLength,
            "task-length",
            taskLength));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.PressureSource,
            "pressure-source",
            pressure));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RuleAmbiguity,
            "ambiguity",
            ambiguity));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DelayLength,
            "review-delay",
            delay));

        foreach (var component in components)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentPayload,
                ComponentMaterialName("global-review-component", component.Branch),
                BuildComponentPayloadValue(component)));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                ComponentMaterialName("global-review-evidence", component.Branch),
                $"branch {component.Branch} required evidence: {component.EvidenceRequirement}; this branch evidence remains separate from the global product."));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.BranchScoringKey,
                ComponentMaterialName("global-review-scoring", component.Branch),
                $"branch {component.Branch} scoring key: {component.ScoringKey}; strong branch cannot hide missing or failing {component.Branch} evidence."));
        }

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.CompositeTaskPrompt,
            "global-review-task-prompt",
            BuildGlobalReviewTaskPrompt(taskFrame, components, taskLength, pressure, ambiguity, delay)));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.AuditPayload,
            "global-review-audit-payload",
            BuildGlobalReviewAuditPayload(components, ambiguity)));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DelayedReconstructionPayload,
            "global-review-delayed-reconstruction-payload",
            BuildGlobalReviewDelayedReconstructionPayload(components, delay)));

        return Array.AsReadOnly(materials.ToArray());
    }

    private static void AddHonestyConstraints(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedDrillContentRequest request,
        string defaultMaterialName,
        string defaultConstraint)
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

        if (!added.Add(defaultConstraint))
        {
            return;
        }

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.HonestyConstraint,
            defaultMaterialName,
            defaultConstraint));
    }

    private static void AddOptionalExactLoadMaterial(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedDrillContentRequest request,
        string loadName,
        GeneratedContentMaterialKind materialKind,
        string materialName)
    {
        var value = request.LoadVariables
            .FirstOrDefault(loadVariable => string.Equals(
                loadVariable.Name,
                loadName,
                StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (value is null)
        {
            return;
        }

        materials.Add(new GeneratedContentMaterial(materialKind, materialName, value));
    }

    private static string BuildComponentPayloadValue(ComponentTemplate component)
    {
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == component.Branch &&
            item.Level == component.Level);
        var drill = ProgramCatalog.Drills.Single(item => item.Id == component.Drill);

        return $"component branch {component.Branch}: level {component.Level}; drill {component.Drill}; task role {component.TaskRole}; source demand {standard.Demand}; source standard {standard.Standard}; component honesty constraint {drill.HonestyConstraint}; component boundary and evidence remain separate.";
    }

    private static string BuildCompositeTaskPrompt(
        CompositeTaskFrame taskFrame,
        IReadOnlyCollection<ComponentTemplate> components,
        string taskLength,
        string transferDistance)
    {
        var componentList = string.Join(", ", components.Select(component => component.Branch));
        return $"{taskFrame.Description}; composite task combines {componentList} for {taskLength}; transfer distance {transferDistance}; every component branch must leave branch-specific evidence and a visible branch scoring key; a strong branch cannot hide missing evidence from a weak branch.";
    }

    private static string BuildGlobalReviewTaskPrompt(
        CompositeTaskFrame taskFrame,
        IReadOnlyCollection<ComponentTemplate> components,
        string taskLength,
        string pressure,
        string ambiguity,
        string delay)
    {
        var componentList = string.Join(", ", components.Select(component => component.Branch));
        return $"{taskFrame.Description}; global review task combines {componentList} for {taskLength}; pressure source {pressure}; ambiguity {ambiguity}; delayed reconstruction after {delay}; composite output, audit, and delayed reconstruction all matter; branch-specific scoring stays visible for every component branch; pressure rule remains intact.";
    }

    private static string BuildGlobalReviewAuditPayload(
        IReadOnlyCollection<ComponentTemplate> components,
        string ambiguity)
    {
        var componentList = string.Join(", ", components.Select(component => component.Branch));
        return $"audit required: lock the original output, inspect each component branch ({componentList}) for critical errors under {ambiguity}, record unsupported changes and false corrections, and keep audit failure separate from composite output quality.";
    }

    private static string BuildGlobalReviewDelayedReconstructionPayload(
        IReadOnlyCollection<ComponentTemplate> components,
        string delay)
    {
        var componentList = string.Join(", ", components.Select(component => component.Branch));
        return $"delayed reconstruction required after {delay}: reconstruct critical information from component branches ({componentList}) without rereading, record omissions and memory gap evidence, and keep delayed reconstruction failure separate from audit or composite score.";
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildPayloadFacts(
        IReadOnlyList<ComponentTemplate> components,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var componentBranches = string.Join("|", components.Select(component => component.Branch));
        var componentPayloads = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ComponentPayload)
            .Select(material => material.Value)
            .ToArray();
        var evidenceRequirements = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ComponentEvidenceRequirement)
            .Select(material => material.Value)
            .ToArray();
        var scoringKeys = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.BranchScoringKey)
            .Select(material => material.Value)
            .ToArray();
        var prompt = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.CompositeTaskPrompt)
            .Value;

        yield return new GeneratedContentPayloadFact("payload-family", "ti-composite-task");
        yield return new GeneratedContentPayloadFact("component-branches", componentBranches);
        yield return new GeneratedContentPayloadFact("component-payloads", string.Join("|", componentPayloads));
        yield return new GeneratedContentPayloadFact("component-evidence-requirements", string.Join("|", evidenceRequirements));
        yield return new GeneratedContentPayloadFact("branch-scoring-keys", string.Join("|", scoringKeys));
        yield return new GeneratedContentPayloadFact("composite-task-prompt", prompt);
        yield return new GeneratedContentPayloadFact(
            "anti-collapse-policy",
            "each component branch has its own evidence requirement and branch scoring key; a strong branch cannot hide missing evidence or a weak component failure");
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildGlobalReviewPayloadFacts(
        IReadOnlyList<ComponentTemplate> components,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var componentBranches = string.Join("|", components.Select(component => component.Branch));
        var componentPayloads = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ComponentPayload)
            .Select(material => material.Value)
            .ToArray();
        var evidenceRequirements = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ComponentEvidenceRequirement)
            .Select(material => material.Value)
            .ToArray();
        var scoringKeys = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.BranchScoringKey)
            .Select(material => material.Value)
            .ToArray();
        var prompt = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.CompositeTaskPrompt)
            .Value;
        var auditPayload = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.AuditPayload)
            .Value;
        var delayedReconstructionPayload = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.DelayedReconstructionPayload)
            .Value;

        yield return new GeneratedContentPayloadFact("payload-family", "ti-global-review-task");
        yield return new GeneratedContentPayloadFact("component-branches", componentBranches);
        yield return new GeneratedContentPayloadFact("component-payloads", string.Join("|", componentPayloads));
        yield return new GeneratedContentPayloadFact("component-evidence-requirements", string.Join("|", evidenceRequirements));
        yield return new GeneratedContentPayloadFact("branch-scoring-keys", string.Join("|", scoringKeys));
        yield return new GeneratedContentPayloadFact("composite-task-prompt", prompt);
        yield return new GeneratedContentPayloadFact("audit-payload", auditPayload);
        yield return new GeneratedContentPayloadFact("delayed-reconstruction-payload", delayedReconstructionPayload);
        yield return new GeneratedContentPayloadFact(
            "required-evidence-channels",
            "composite output|audit|delayed reconstruction|branch-specific scoring");
        yield return new GeneratedContentPayloadFact(
            "all-channels-required-policy",
            "composite output, audit, and delayed reconstruction all matter; no channel can be replaced by strength in another channel");
    }

    private static IReadOnlyList<ComponentTemplate> SelectComponents(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan)
    {
        var componentCount = Math.Clamp(
            ParseLoadCount(request, "number of branches") ??
                ParseLoadCount(request, "branch count") ??
                2,
            2,
            ComponentTemplates.Length);
        return SelectComponentsFromTemplates(
            request,
            seedPlan,
            ComponentTemplates,
            componentCount,
            "component");
    }

    private static IReadOnlyList<ComponentTemplate> SelectGlobalReviewComponents(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan)
    {
        var requestedBranches = BranchTokens(request.EquivalenceClass, GlobalReviewComponentTemplates);
        var componentCount = Math.Clamp(
            ParseLoadCount(request, "number of branches") ??
                ParseLoadCount(request, "branch count") ??
                Math.Max(4, requestedBranches.Count),
            3,
            GlobalReviewComponentTemplates.Length);

        return SelectComponentsFromTemplates(
            request,
            seedPlan,
            GlobalReviewComponentTemplates,
            componentCount,
            "global-review-component");
    }

    private static IReadOnlyList<ComponentTemplate> SelectComponentsFromTemplates(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan,
        IReadOnlyList<ComponentTemplate> templates,
        int componentCount,
        string purpose)
    {
        var requestedBranches = BranchTokens(request.EquivalenceClass, templates);
        var selected = templates
            .Where(component => requestedBranches.Contains(component.Branch))
            .ToList();

        if (selected.Count < componentCount)
        {
            var startIndex = SelectIndex(
                seedPlan.PayloadSeed,
                purpose,
                seedPlan.VariantIndex,
                templates.Count);

            for (var i = 0; selected.Count < componentCount && i < templates.Count * 2; i++)
            {
                var candidate = templates[(startIndex + i) % templates.Count];
                if (selected.All(component => component.Branch != candidate.Branch))
                {
                    selected.Add(candidate);
                }
            }
        }

        return Array.AsReadOnly(selected.Take(componentCount).ToArray());
    }

    private static CompositeTaskFrame SelectTaskFrame(GeneratedContentSeedPlan seedPlan)
    {
        var index = SelectIndex(
            seedPlan.PayloadSeed,
            "task-frame",
            seedPlan.VariantIndex,
            TaskFrames.Length);

        return TaskFrames[index];
    }

    private static IReadOnlySet<BranchCode> BranchTokens(string equivalenceClass)
    {
        return BranchTokens(equivalenceClass, ComponentTemplates);
    }

    private static IReadOnlySet<BranchCode> BranchTokens(
        string equivalenceClass,
        IEnumerable<ComponentTemplate> templates)
    {
        var tokens = equivalenceClass
            .Split(['-', '_', '.', '|', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var branches = new HashSet<BranchCode>();

        foreach (var component in templates)
        {
            if (tokens.Contains(component.Branch.ToString()))
            {
                branches.Add(component.Branch);
            }
        }

        return branches;
    }

    private static int? ParseLoadCount(
        GeneratedDrillContentRequest request,
        string name)
    {
        var rawValue = request.LoadVariables
            .FirstOrDefault(loadVariable => string.Equals(
                loadVariable.Name,
                name,
                StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (rawValue is null)
        {
            return null;
        }

        var numericPrefix = new string(rawValue
            .Trim()
            .TakeWhile(char.IsDigit)
            .ToArray());

        return int.TryParse(numericPrefix, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count
            : null;
    }

    private static string LoadValueOrDefault(
        GeneratedDrillContentRequest request,
        string name,
        string defaultValue)
    {
        return request.LoadVariables
            .FirstOrDefault(loadVariable => string.Equals(
                loadVariable.Name,
                name,
                StringComparison.OrdinalIgnoreCase))
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

    private static string ComponentMaterialName(string prefix, BranchCode branch)
    {
        return prefix + "-" + branch.ToString().ToLowerInvariant();
    }

    private sealed record ComponentTemplate(
        BranchCode Branch,
        GlobalLevelId Level,
        DrillId Drill,
        string TaskRole,
        string EvidenceRequirement,
        string ScoringKey);

    private sealed record CompositeTaskFrame(
        string FrameId,
        string Description);
}
