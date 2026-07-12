using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class FocusHoldGeneratedContent
{
    internal FocusHoldGeneratedContent(
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

public static class FocusHoldGeneratedContentGenerator
{
    private const string TargetAndDriftConstraint = "Target is stated before set; every noticed drift is marked once.";
    private const string NoSubstitutionConstraint = "No target substitution.";
    private const string NoDistractorResponseConstraint = "Do not respond to distractor unless drill says so.";
    private const string DefaultTargetSubtlety = "simple visual target";
    private const string DefaultTargetHoldDuration = "3 minutes";
    private const string DefaultDistractorHoldDuration = "5 minutes";
    private const string DefaultDistractorFrequency = "periodic";
    private const string DefaultDistractorSalience = "low";

    private static readonly string[] TargetColors = ["red", "blue", "green", "black", "amber", "violet"];

    private static readonly string[] TargetSizes = ["small", "medium", "large"];

    private static readonly string[] TargetShapes = ["dot", "line", "square", "circle", "triangle"];

    private static readonly string[] DistractorPrompts =
    [
        "mirror",
        "47",
        "orbit",
        "#",
        "206",
        "cedar",
        "?",
        "81",
        "north",
        "+",
    ];

    public static FocusHoldGeneratedContent Generate(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(seed);

        EnsureSupportedFocusHoldRequest(request);

        var seedPlan = GeneratedContentSeedDeriver.Derive(request, seed);
        var target = SelectTarget(seedPlan);
        var materials = BuildMaterials(request, seedPlan, target);
        var result = new GeneratedDrillContentResult(
            request,
            seedPlan.Instance,
            seedPlan.PayloadFacts.Concat(BuildPayloadFacts(request, target, materials)));

        return new FocusHoldGeneratedContent(result, materials);
    }

    private static void EnsureSupportedFocusHoldRequest(GeneratedDrillContentRequest request)
    {
        if (request.Branch != BranchCode.FH)
        {
            throw new ArgumentException(
                "Focus Hold generated content can only be produced for the FH branch.",
                nameof(request));
        }

        var expectedKind = request.Drill switch
        {
            DrillId.FH1TargetHold => PromptContentKind.EquivalentPrompt,
            DrillId.FH2DistractorHold => PromptContentKind.CueSequence,
            _ => throw new ArgumentException(
                "Focus Hold generated content supports only FH-1 Target Hold and FH-2 Distractor Hold.",
                nameof(request)),
        };

        if (request.ContentKind != expectedKind)
        {
            throw new ArgumentException(
                $"Drill {request.Drill} requires generated content kind {expectedKind}.",
                nameof(request));
        }
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildMaterials(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan,
        TargetTemplate target)
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

        var duration = LoadValueOrDefault(
            request,
            "duration",
            request.Drill == DrillId.FH2DistractorHold
                ? DefaultDistractorHoldDuration
                : DefaultTargetHoldDuration);
        var targetSubtlety = LoadValueOrDefault(request, "target subtlety", DefaultTargetSubtlety);

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TargetStatement,
            "target-statement",
            target.Statement));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TargetType,
            "target-type",
            target.Type));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TargetSubtlety,
            "target-subtlety",
            targetSubtlety));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.HoldDuration,
            "duration",
            duration));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DriftMarkingEvidenceShape,
            "drift-evidence",
            "mark every noticed drift once; continue with the same target; target substitution prohibited"));

        if (request.Drill == DrillId.FH2DistractorHold)
        {
            AddDistractorMaterials(materials, request, seedPlan, target);
        }

        if (request.Level == GlobalLevelId.L5)
        {
            ObjectiveComponentTaskCatalog.AddMaterials(
                materials,
                [BranchCode.TI],
                seedPlan.PayloadSeed,
                seedPlan.FreshnessOrdinal,
                "integrated-hold");
        }

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

        AddDefaultHonestyConstraint(materials, added, "target-and-drift", TargetAndDriftConstraint);
        AddDefaultHonestyConstraint(materials, added, "no-target-substitution", NoSubstitutionConstraint);

        if (request.Drill == DrillId.FH2DistractorHold)
        {
            AddDefaultHonestyConstraint(
                materials,
                added,
                "no-distractor-response",
                NoDistractorResponseConstraint);
        }
    }

    private static void AddDefaultHonestyConstraint(
        ICollection<GeneratedContentMaterial> materials,
        ISet<string> added,
        string name,
        string value)
    {
        if (!added.Add(value))
        {
            return;
        }

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.HonestyConstraint,
            name,
            value));
    }

    private static void AddDistractorMaterials(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan,
        TargetTemplate target)
    {
        var frequency = LoadValueOrDefault(request, "distractor frequency", DefaultDistractorFrequency);
        var salience = LoadValueOrDefault(request, "distractor salience", DefaultDistractorSalience);
        var duration = LoadValueOrDefault(request, "duration", DefaultDistractorHoldDuration);
        var distractorCount = DistractorCountFor(frequency);
        var targetCombinationCount = TargetColors.Length * TargetSizes.Length * TargetShapes.Length;
        var distractorCycle = seedPlan.FreshnessOrdinal / targetCombinationCount;
        var firstIndex = GeneratedContentStableHash.OrdinalIndex(
            seedPlan.RequestFingerprint,
            "distractor",
            distractorCycle,
            DistractorPrompts.Length);

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DistractorFrequency,
            "distractor-frequency",
            frequency));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DistractorSalience,
            "distractor-salience",
            salience));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DistractorNoResponseRule,
            "distractor-no-response-rule",
            "no response; distractors are irrelevant and not part of target"));

        for (var i = 0; i < distractorCount; i++)
        {
            var prompt = SelectDistractorPrompt(target, firstIndex + i);
            var number = (i + 1).ToString(CultureInfo.InvariantCulture);
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.DistractorPrompt,
                $"distractor-{number}",
                prompt));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.DistractorTiming,
                $"distractor-{number}-timing",
                $"position {number} of {distractorCount.ToString(CultureInfo.InvariantCulture)} within {duration}"));
        }
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildPayloadFacts(
        GeneratedDrillContentRequest request,
        TargetTemplate target,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        yield return new GeneratedContentPayloadFact(
            "payload-family",
            request.Drill == DrillId.FH2DistractorHold
                ? "fh-distractor-hold"
                : "fh-target-hold");
        yield return new GeneratedContentPayloadFact("target-statement", target.Statement);
        yield return new GeneratedContentPayloadFact("target-type", target.Type);
        yield return new GeneratedContentPayloadFact(
            "evidence-shape",
            "one mark per noticed drift");

        foreach (var distractor in materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.DistractorPrompt))
        {
            yield return new GeneratedContentPayloadFact(distractor.Name, distractor.Value);
        }
    }

    private static TargetTemplate SelectTarget(GeneratedContentSeedPlan seedPlan)
    {
        var combinationCount = TargetColors.Length * TargetSizes.Length * TargetShapes.Length;
        var combinationIndex = GeneratedContentStableHash.OrdinalIndex(
            seedPlan.RequestFingerprint,
            "target",
            seedPlan.FreshnessOrdinal,
            combinationCount);
        var color = TargetColors[combinationIndex % TargetColors.Length];
        combinationIndex /= TargetColors.Length;
        var size = TargetSizes[combinationIndex % TargetSizes.Length];
        combinationIndex /= TargetSizes.Length;
        var shape = TargetShapes[combinationIndex % TargetShapes.Length];
        return new TargetTemplate(
            "visual shape",
            $"Visual target: {size} {color} {shape}");
    }

    private static string SelectDistractorPrompt(TargetTemplate target, int index)
    {
        for (var offset = 0; offset < DistractorPrompts.Length; offset++)
        {
            var prompt = DistractorPrompts[(index + offset) % DistractorPrompts.Length];
            if (!target.Statement.Contains(prompt, StringComparison.Ordinal))
            {
                return prompt;
            }
        }

        throw new InvalidOperationException("Unable to select a distractor outside the target statement.");
    }

    private static int DistractorCountFor(string frequency)
    {
        var numericPrefix = new string(frequency
            .Trim()
            .TakeWhile(char.IsDigit)
            .ToArray());
        if (int.TryParse(numericPrefix, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            return Math.Clamp(count, 1, DistractorPrompts.Length);
        }

        return frequency.Trim().ToLowerInvariant() switch
        {
            "low" => 2,
            "moderate" => 3,
            "periodic" => 3,
            "high" => 4,
            _ => 3,
        };
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

    private sealed record TargetTemplate(
        string Type,
        string Statement);
}
