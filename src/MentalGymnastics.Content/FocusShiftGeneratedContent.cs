using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class FocusShiftGeneratedContent
{
    internal FocusShiftGeneratedContent(
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

public static class FocusShiftGeneratedContentGenerator
{
    private const string ValidCueConstraint = "Switch only on valid cue.";
    private const string InvalidCueConstraint = "Invalid cues must not trigger switch.";
    private const string NoAnticipatorySwitchingConstraint = "No anticipatory switching.";
    private const string DefaultCueDensity = "low";
    private const string DefaultInvalidCueDensity = "moderate";
    private const string DefaultRuleContrast = "valid symbol versus invalid lure";
    private const string DefaultReturnPrecision = "next cue";
    private const string DefaultResponseWindow = "before next cue";
    private const int DefaultTargetCount = 2;
    private const int DefaultCueSwitchCount = 4;
    private const int DefaultInvalidCueFilterCount = 6;

    private static readonly VisualStimulusColor[] TargetColors =
    [
        VisualStimulusColor.Red,
        VisualStimulusColor.Blue,
        VisualStimulusColor.Green,
        VisualStimulusColor.Black,
    ];

    private static readonly VisualStimulusShape[] TargetShapes =
    [
        VisualStimulusShape.Dot,
        VisualStimulusShape.Circle,
        VisualStimulusShape.Square,
        VisualStimulusShape.Bar,
    ];

    private static readonly TargetTemplate[] Targets = TargetColors
        .SelectMany(color => TargetShapes.Select(shape => new TargetTemplate(
            new VisualStimulusSpec(shape, color))))
        .ToArray();

    private static readonly VisualStimulusSpec[] InvalidCueStimuli =
    [
        new(VisualStimulusShape.Dot, VisualStimulusColor.Amber),
        new(VisualStimulusShape.Circle, VisualStimulusColor.Violet),
        new(VisualStimulusShape.Square, VisualStimulusColor.Gray),
        new(VisualStimulusShape.Bar, VisualStimulusColor.Amber),
        new(VisualStimulusShape.Triangle, VisualStimulusColor.Violet),
        new(
            VisualStimulusShape.Arrow,
            VisualStimulusColor.Gray,
            Direction: VisualStimulusDirection.North),
    ];

    public static FocusShiftGeneratedContent Generate(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(seed);

        EnsureSupportedFocusShiftRequest(request);

        var seedPlan = GeneratedContentSeedDeriver.Derive(request, seed);
        var targets = SelectTargets(seedPlan, TargetCountFor(request));
        var materials = BuildMaterials(request, seedPlan, targets);
        var result = new GeneratedDrillContentResult(
            request,
            seedPlan.Instance,
            seedPlan.PayloadFacts.Concat(BuildPayloadFacts(request, targets, materials)));

        return new FocusShiftGeneratedContent(result, materials);
    }

    private static void EnsureSupportedFocusShiftRequest(GeneratedDrillContentRequest request)
    {
        if (request.Branch != BranchCode.FS)
        {
            throw new ArgumentException(
                "Focus Shift generated content can only be produced for the FS branch.",
                nameof(request));
        }

        if (request.Drill != DrillId.FS1CueSwitch &&
            request.Drill != DrillId.FS2InvalidCueFilter)
        {
            throw new ArgumentException(
                "Focus Shift generated content supports only FS-1 Cue Switch and FS-2 Invalid Cue Filter.",
                nameof(request));
        }

        if (request.ContentKind != PromptContentKind.CueSequence)
        {
            throw new ArgumentException(
                $"Drill {request.Drill} requires generated content kind {PromptContentKind.CueSequence}.",
                nameof(request));
        }
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildMaterials(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan,
        IReadOnlyList<TargetTemplate> targets)
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
        AddTargetSet(materials, targets);

        var cueDensity = LoadValueOrDefault(
            request,
            "cue density",
            request.Drill == DrillId.FS2InvalidCueFilter ? DefaultInvalidCueDensity : DefaultCueDensity);
        var returnPrecision = LoadValueOrDefault(request, "return precision", DefaultReturnPrecision);
        var responseWindow = LoadValueOrDefault(request, "response window", DefaultResponseWindow);

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.CueDensity,
            "cue-density",
            cueDensity));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.ReturnPrecision,
            "return-precision",
            returnPrecision));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.ResponseWindow,
            "response-window",
            responseWindow));

        if (request.Drill == DrillId.FS2InvalidCueFilter)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.RuleContrast,
                "rule-contrast",
                LoadValueOrDefault(request, "rule contrast", DefaultRuleContrast)));
        }

        AddCueSequenceMaterials(materials, request, seedPlan, targets, responseWindow);

        if (request.Level == GlobalLevelId.L5)
        {
            ObjectiveComponentTaskCatalog.AddMaterials(
                materials,
                [BranchCode.TI],
                seedPlan.PayloadSeed,
                seedPlan.FreshnessOrdinal,
                "integrated-shift");
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

        AddDefaultHonestyConstraint(materials, added, "valid-cue-only", ValidCueConstraint);
        AddDefaultHonestyConstraint(materials, added, "no-anticipatory-switching", NoAnticipatorySwitchingConstraint);

        if (request.Drill == DrillId.FS2InvalidCueFilter)
        {
            AddDefaultHonestyConstraint(materials, added, "invalid-cue-no-switch", InvalidCueConstraint);
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

    private static void AddTargetSet(
        ICollection<GeneratedContentMaterial> materials,
        IReadOnlyList<TargetTemplate> targets)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.TargetSet,
                $"target-{(i + 1).ToString(CultureInfo.InvariantCulture)}",
                targets[i].Encoded));
        }
    }

    private static void AddCueSequenceMaterials(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan,
        IReadOnlyList<TargetTemplate> targets,
        string responseWindow)
    {
        var cueStepCount = SwitchCountFor(request);
        var activeTargetIndex = 0;
        var invalidCueOffset = GeneratedContentStableHash.OrdinalIndex(
            seedPlan.RequestFingerprint,
            "invalid-cue",
            seedPlan.FreshnessOrdinal,
            InvalidCueStimuli.Length);

        for (var i = 0; i < cueStepCount; i++)
        {
            var stepNumber = (i + 1).ToString(CultureInfo.InvariantCulture);
            var isInvalidCue = request.Drill == DrillId.FS2InvalidCueFilter &&
                (i + seedPlan.FreshnessOrdinal + 1) % 3 == 0;

            if (isInvalidCue)
            {
                var invalidCue = VisualStimulusCodec.Encode(
                    InvalidCueStimuli[(invalidCueOffset + i) % InvalidCueStimuli.Length]);
                materials.Add(new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.CueStep,
                    $"cue-step-{stepNumber}",
                    invalidCue));
                materials.Add(new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.InvalidCue,
                    $"invalid-cue-{stepNumber}",
                    invalidCue));
                materials.Add(new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.ExpectedActiveTarget,
                    $"expected-target-{stepNumber}",
                    targets[activeTargetIndex].Encoded));
                continue;
            }

            activeTargetIndex = (activeTargetIndex + 1) % targets.Count;
            var target = targets[activeTargetIndex];
            var validCue = target.Encoded;
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.CueStep,
                $"cue-step-{stepNumber}",
                validCue));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ValidCue,
                $"valid-cue-{stepNumber}",
                validCue));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ExpectedActiveTarget,
                $"expected-target-{stepNumber}",
                target.Encoded));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ResponseWindow,
                $"response-window-{stepNumber}",
                responseWindow));
        }
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildPayloadFacts(
        GeneratedDrillContentRequest request,
        IReadOnlyList<TargetTemplate> targets,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        yield return new GeneratedContentPayloadFact(
            "payload-family",
            request.Drill == DrillId.FS2InvalidCueFilter
                ? "fs-invalid-cue-filter"
                : "fs-cue-switch");
        yield return new GeneratedContentPayloadFact(
            "target-set",
            string.Join("|", targets.Select(target => target.Encoded)));
        yield return new GeneratedContentPayloadFact(
            "sequence-accuracy-evidence",
            "valid cue responses, expected active target after each cue, missed cues, and anticipatory switches");
        yield return new GeneratedContentPayloadFact(
            "uncued-switch-policy",
            "uncued and anticipatory switches are invalid behavior");

        if (request.Drill == DrillId.FS2InvalidCueFilter)
        {
            yield return new GeneratedContentPayloadFact(
                "invalid-cue-policy",
                "invalid cues must not trigger switch");
        }

        foreach (var cueStep in materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.CueStep))
        {
            yield return new GeneratedContentPayloadFact(cueStep.Name, cueStep.Value);
        }
    }

    private static IReadOnlyList<TargetTemplate> SelectTargets(
        GeneratedContentSeedPlan seedPlan,
        int targetCount)
    {
        var ordered = GeneratedContentStableHash.OrderByOrdinal(
            Targets,
            seedPlan.RequestFingerprint,
            "target-set",
            seedPlan.FreshnessOrdinal,
            target => target.Encoded);
        var targets = new List<TargetTemplate>();

        for (var i = 0; i < targetCount; i++)
        {
            targets.Add(ordered[i % ordered.Count]);
        }

        return Array.AsReadOnly(targets.ToArray());
    }

    private static int TargetCountFor(GeneratedDrillContentRequest request)
    {
        return Math.Clamp(
            ParseLoadCount(request, "target count") ?? DefaultTargetCount,
            2,
            Targets.Length);
    }

    private static int SwitchCountFor(GeneratedDrillContentRequest request)
    {
        var defaultCount = request.Drill == DrillId.FS2InvalidCueFilter
            ? DefaultInvalidCueFilterCount
            : DefaultCueSwitchCount;

        return Math.Max(ParseLoadCount(request, "switch count") ?? defaultCount, 1);
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

    private sealed record TargetTemplate(VisualStimulusSpec Stimulus)
    {
        public string Encoded => VisualStimulusCodec.Encode(Stimulus);
    }
}
