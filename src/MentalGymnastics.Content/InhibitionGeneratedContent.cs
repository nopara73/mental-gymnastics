using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class InhibitionGeneratedContent
{
    internal InhibitionGeneratedContent(
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

public static class InhibitionGeneratedContentGenerator
{
    private const string PrematureResponseConstraint = "Premature response fails item.";
    private const string RuleAndExceptionsBeforeSetConstraint = "Rule and exceptions stated before set.";
    private const string NoRuleChangeConstraint = "Rule cannot change mid-set.";
    private const string DefaultCueConflict = "simple go/no-go symbols";
    private const string DefaultResponseWindow = "2 seconds";
    private const string DefaultCuePace = "2-second cadence";
    private const string DefaultNoGoFrequency = "every third cue";
    private const string DefaultSimilarity = "near symbols";
    private const int DefaultCueCount = 9;
    private const int DefaultNoGoInterval = 3;
    private const int DefaultExceptionCount = 3;
    private const int DefaultExceptionCueCount = 8;

    private static readonly CueTemplate[] GoCues =
    [
        new(new(VisualStimulusShape.Circle, VisualStimulusColor.Green), "tap"),
        new(new(VisualStimulusShape.Square, VisualStimulusColor.Blue), "tap"),
        new(new(VisualStimulusShape.Bar, VisualStimulusColor.White), "tap"),
        new(new(
            VisualStimulusShape.Arrow,
            VisualStimulusColor.Black,
            Direction: VisualStimulusDirection.North), "tap"),
        new(new(VisualStimulusShape.Dot, VisualStimulusColor.Black), "tap"),
        new(new(
            VisualStimulusShape.Ring,
            VisualStimulusColor.Black,
            Fill: VisualStimulusFill.Outline), "tap"),
    ];

    private static readonly CueTemplate[] NoGoCues =
    [
        new(new(VisualStimulusShape.Circle, VisualStimulusColor.Red), "withhold"),
        new(new(VisualStimulusShape.Square, VisualStimulusColor.Amber), "withhold"),
        new(new(
            VisualStimulusShape.Bar,
            VisualStimulusColor.Black,
            Fill: VisualStimulusFill.Striped), "withhold"),
        new(new(
            VisualStimulusShape.Arrow,
            VisualStimulusColor.Black,
            Direction: VisualStimulusDirection.South), "withhold"),
        new(new(
            VisualStimulusShape.Dot,
            VisualStimulusColor.Black,
            Fill: VisualStimulusFill.Outline), "withhold"),
        new(new(
            VisualStimulusShape.Ring,
            VisualStimulusColor.Black,
            Fill: VisualStimulusFill.Crossed), "withhold"),
    ];

    private static readonly BaseRuleItem[] BaseRuleItems =
    [
        new(new(VisualStimulusShape.Circle, VisualStimulusColor.Green), "tap"),
        new(new(
            VisualStimulusShape.Ring,
            VisualStimulusColor.Blue,
            Fill: VisualStimulusFill.Outline), "tap"),
        new(new(VisualStimulusShape.Dot, VisualStimulusColor.Gray), "tap"),
        new(new(VisualStimulusShape.Triangle, VisualStimulusColor.Red), "withhold"),
        new(new(VisualStimulusShape.Square, VisualStimulusColor.Amber), "withhold"),
        new(new(VisualStimulusShape.Diamond, VisualStimulusColor.Black), "withhold"),
    ];

    private static readonly ExceptionTemplate[] ExceptionTemplates =
    [
        new(new(VisualStimulusShape.Triangle, VisualStimulusColor.Green), "tap", "green triangle is tapped instead of withheld"),
        new(new(VisualStimulusShape.Circle, VisualStimulusColor.Red), "withhold", "red circle is withheld instead of tapped"),
        new(new(VisualStimulusShape.Diamond, VisualStimulusColor.Blue), "tap", "blue diamond is tapped instead of withheld"),
        new(new(
            VisualStimulusShape.Ring,
            VisualStimulusColor.Amber,
            Fill: VisualStimulusFill.Outline), "withhold", "amber ring is withheld instead of tapped"),
        new(new(VisualStimulusShape.Square, VisualStimulusColor.White), "tap", "white square is tapped instead of withheld"),
        new(new(VisualStimulusShape.Dot, VisualStimulusColor.Black), "withhold", "black dot is withheld instead of tapped"),
    ];

    public static InhibitionGeneratedContent Generate(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(seed);

        EnsureSupportedInhibitionRequest(request);

        var seedPlan = GeneratedContentSeedDeriver.Derive(request, seed);
        var materials = BuildMaterials(request, seedPlan);
        var result = new GeneratedDrillContentResult(
            request,
            seedPlan.Instance,
            seedPlan.PayloadFacts.Concat(BuildPayloadFacts(request, materials)));

        return new InhibitionGeneratedContent(result, materials);
    }

    private static void EnsureSupportedInhibitionRequest(GeneratedDrillContentRequest request)
    {
        if (request.Branch != BranchCode.IR)
        {
            throw new ArgumentException(
                "Inhibition generated content can only be produced for the IR branch.",
                nameof(request));
        }

        if (request.Drill is not DrillId.IR1GoNoGoRule and not DrillId.IR2ExceptionRule)
        {
            throw new ArgumentException(
                "Inhibition generated content supports only IR-1 Go/No-Go Rule and IR-2 Exception Rule.",
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
        GeneratedContentSeedPlan seedPlan)
    {
        if (request.Drill == DrillId.IR2ExceptionRule)
        {
            return BuildExceptionRuleMaterials(request, seedPlan);
        }

        var materials = new List<GeneratedContentMaterial>();

        foreach (var loadVariable in request.LoadVariables)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.LoadVariable,
                loadVariable.Name,
                loadVariable.Value));
        }

        AddHonestyConstraints(materials, request);

        var cueConflict = LoadValueOrDefault(request, "cue conflict", DefaultCueConflict);
        var responseWindow = LoadValueOrDefault(request, "response speed", DefaultResponseWindow);
        var noGoFrequency = LoadValueOrDefault(request, "no-go frequency", DefaultNoGoFrequency);
        var noGoInterval = NoGoIntervalFor(noGoFrequency);
        var cuePace = CuePaceFor(responseWindow);

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.CueConflict,
            "cue-conflict",
            cueConflict));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.CuePace,
            "cue-pace",
            cuePace));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.NoGoFrequency,
            "no-go-frequency",
            noGoFrequency));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.ResponseWindow,
            "response-window",
            responseWindow));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RuleStatement,
            "rule-statement",
            "Rule before set: TAP green, blue, or white shapes; north arrows; solid black dots; or open black rings. " +
            "WITHHOLD red or amber shapes; south arrows; striped black bars; hollow black dots; or crossed black rings. " +
            "The rule cannot be changed after the cue stream starts."));

        AddGoNoGoCueStream(materials, seedPlan, noGoInterval, responseWindow);

        return Array.AsReadOnly(materials.ToArray());
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildExceptionRuleMaterials(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan)
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

        var responseWindow = LoadValueOrDefault(request, "response speed", DefaultResponseWindow);
        var cuePace = CuePaceFor(responseWindow);
        var similarity = LoadValueOrDefault(request, "similarity", DefaultSimilarity);
        var cueConflict = LoadValueOrDefault(request, "cue conflict", DefaultCueConflict);
        var exceptions = SelectExceptions(seedPlan, ExceptionCountFor(request));

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RuleStatement,
            "rule-statement",
            "Rule before set: tap round symbols and withhold angular symbols; named exceptions override base rule and cannot be rewritten mid-set."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.CuePace,
            "cue-pace",
            cuePace));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.ResponseWindow,
            "response-window",
            responseWindow));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.CueConflict,
            "cue-conflict",
            cueConflict));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.Similarity,
            "similarity",
            similarity));

        AddOptionalLoadMaterial(
            materials,
            request,
            "pressure",
            GeneratedContentMaterialKind.PressureSource,
            "pressure-source");
        AddOptionalLoadMaterial(
            materials,
            request,
            "task length",
            GeneratedContentMaterialKind.TaskLength,
            "task-length");

        for (var i = 0; i < exceptions.Count; i++)
        {
            var exceptionNumber = (i + 1).ToString(CultureInfo.InvariantCulture);
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ExceptionDefinition,
                $"exception-{exceptionNumber}",
                VisualStimulusCodec.EncodeException(new VisualStimulusExceptionSpec(
                    i + 1,
                    exceptions[i].Stimulus,
                    ResponseActionFor(exceptions[i].ExpectedAction),
                    exceptions[i].Reason))));
        }

        AddExceptionRuleCueStream(materials, seedPlan, exceptions, responseWindow);

        if (request.Level == GlobalLevelId.L5)
        {
            ObjectiveComponentTaskCatalog.AddMaterials(
                materials,
                [BranchCode.TI],
                seedPlan.PayloadSeed,
                seedPlan.FreshnessOrdinal,
                "integrated-inhibition");
        }

        return Array.AsReadOnly(materials.ToArray());
    }

    private static void AddOptionalLoadMaterial(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedDrillContentRequest request,
        string loadName,
        GeneratedContentMaterialKind materialKind,
        string materialName)
    {
        var load = request.LoadVariables.FirstOrDefault(loadVariable =>
            string.Equals(loadVariable.Name, loadName, StringComparison.OrdinalIgnoreCase));
        if (load is not null)
        {
            materials.Add(new GeneratedContentMaterial(materialKind, materialName, load.Value));
        }
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

        if (request.Drill == DrillId.IR2ExceptionRule)
        {
            AddDefaultHonestyConstraint(
                materials,
                added,
                "rule-and-exceptions-before-set",
                RuleAndExceptionsBeforeSetConstraint);
            AddDefaultHonestyConstraint(materials, added, "no-rule-change", NoRuleChangeConstraint);
            return;
        }

        AddDefaultHonestyConstraint(materials, added, "premature-response-fails", PrematureResponseConstraint);
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

    private static void AddGoNoGoCueStream(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedContentSeedPlan seedPlan,
        int noGoInterval,
        string responseWindow)
    {
        var orderedGoCues = GeneratedContentStableHash.OrderByOrdinal(
            GoCues,
            seedPlan.RequestFingerprint,
            "go-cue",
            seedPlan.FreshnessOrdinal,
            cue => cue.Encoded);
        var orderedNoGoCues = GeneratedContentStableHash.OrderByOrdinal(
            NoGoCues,
            seedPlan.RequestFingerprint,
            "no-go-cue",
            seedPlan.FreshnessOrdinal,
            cue => cue.Encoded);
        var goIndex = 0;
        var noGoIndex = 0;

        for (var i = 0; i < DefaultCueCount; i++)
        {
            var cueNumber = i + 1;
            var cueName = cueNumber.ToString(CultureInfo.InvariantCulture);
            var isNoGoCue = (cueNumber + seedPlan.FreshnessOrdinal) % noGoInterval == 0;

            if (isNoGoCue)
            {
                var cue = orderedNoGoCues[noGoIndex++ % orderedNoGoCues.Count];
                materials.Add(new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.GoNoGoCue,
                    $"cue-{cueName}",
                    cue.Encoded));
                materials.Add(new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.ExpectedAction,
                    $"expected-action-{cueName}",
                    "withhold: no response expected; premature response fails item"));
                continue;
            }

            var goCue = orderedGoCues[goIndex++ % orderedGoCues.Count];
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.GoNoGoCue,
                $"cue-{cueName}",
                goCue.Encoded));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ExpectedAction,
                $"expected-action-{cueName}",
                $"respond: {goCue.ExpectedAction} within {responseWindow}"));
        }
    }

    private static void AddExceptionRuleCueStream(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedContentSeedPlan seedPlan,
        IReadOnlyList<ExceptionTemplate> exceptions,
        string responseWindow)
    {
        var exceptionPermutationCount = GeneratedContentStableHash.PermutationCount(
            ExceptionTemplates.Length,
            exceptions.Count);
        var baseCycle = seedPlan.FreshnessOrdinal / exceptionPermutationCount;
        var baseStart = GeneratedContentStableHash.OrdinalIndex(
            seedPlan.RequestFingerprint,
            "base-rule-item",
            baseCycle,
            BaseRuleItems.Length);
        var exceptionIndex = 0;

        for (var i = 0; i < DefaultExceptionCueCount; i++)
        {
            var cueNumber = i + 1;
            var cueName = cueNumber.ToString(CultureInfo.InvariantCulture);
            var shouldUseException = i % 3 == 1 && exceptionIndex < exceptions.Count;

            if (shouldUseException)
            {
                var exception = exceptions[exceptionIndex];
                exceptionIndex++;
                materials.Add(new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.CueStep,
                    $"cue-step-{cueName}",
                    exception.Encoded));
                materials.Add(new GeneratedContentMaterial(
                    GeneratedContentMaterialKind.ExpectedAction,
                    $"expected-action-{cueName}",
                    $"apply exception: {exception.ExpectedAction} within {responseWindow}"));
                continue;
            }

            var item = BaseRuleItems[(baseStart + i) % BaseRuleItems.Length];
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.CueStep,
                $"cue-step-{cueName}",
                item.Encoded));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ExpectedAction,
                $"expected-action-{cueName}",
                $"base rule: {item.ExpectedAction} within {responseWindow}"));
        }
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildPayloadFacts(
        GeneratedDrillContentRequest request,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        if (request.Drill == DrillId.IR2ExceptionRule)
        {
            foreach (var fact in BuildExceptionRulePayloadFacts(request, materials))
            {
                yield return fact;
            }

            yield break;
        }

        var cueValues = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.GoNoGoCue)
            .Select(material => material.Value)
            .ToArray();
        var expectedActions = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedAction)
            .Select(material => material.Value)
            .ToArray();

        yield return new GeneratedContentPayloadFact("payload-family", "ir-go-no-go");
        yield return new GeneratedContentPayloadFact("cue-stream", string.Join("|", cueValues));
        yield return new GeneratedContentPayloadFact("expected-action-key", string.Join("|", expectedActions));
        yield return new GeneratedContentPayloadFact(
            "cue-pace",
            CuePaceFor(LoadValueOrDefault(request, "response speed", DefaultResponseWindow)));
        yield return new GeneratedContentPayloadFact(
            "no-go-frequency",
            LoadValueOrDefault(request, "no-go frequency", DefaultNoGoFrequency));
        yield return new GeneratedContentPayloadFact(
            "premature-response-policy",
            "premature response fails the item and cannot be treated as valid");
        yield return new GeneratedContentPayloadFact(
            "commission-evidence",
            "response on no-go cue remains observable as commission failure evidence");
        yield return new GeneratedContentPayloadFact(
            "omission-evidence",
            "missing response on go cue remains observable against the expected action key");
        yield return new GeneratedContentPayloadFact(
            "post-error-cascade-evidence",
            "errors after a premature response remain separately observable in cue order");
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildExceptionRulePayloadFacts(
        GeneratedDrillContentRequest request,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var rule = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.RuleStatement)
            .Value;
        var exceptions = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExceptionDefinition)
            .Select(material => material.Value)
            .ToArray();
        var cueSteps = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.CueStep)
            .Select(material => material.Value)
            .ToArray();
        var expectedActions = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedAction)
            .Select(material => material.Value)
            .ToArray();

        yield return new GeneratedContentPayloadFact("payload-family", "ir-exception-rule");
        yield return new GeneratedContentPayloadFact("pre-stated-rule", rule);
        yield return new GeneratedContentPayloadFact("exception-key", string.Join("|", exceptions));
        yield return new GeneratedContentPayloadFact("cue-stream", string.Join("|", cueSteps));
        yield return new GeneratedContentPayloadFact("expected-action-key", string.Join("|", expectedActions));
        yield return new GeneratedContentPayloadFact(
            "cue-pace",
            CuePaceFor(LoadValueOrDefault(request, "response speed", DefaultResponseWindow)));
        yield return new GeneratedContentPayloadFact(
            "similarity",
            LoadValueOrDefault(request, "similarity", DefaultSimilarity));
        yield return new GeneratedContentPayloadFact(
            "rule-fidelity-evidence",
            "responses are checked against the pre-stated rule, exception key, cue order, and expected action key");
        yield return new GeneratedContentPayloadFact(
            "rule-change-evidence",
            "mid-set rule changes are detectable because the original rule and exception key are fixed before cue presentation");
        yield return new GeneratedContentPayloadFact(
            "exception-forgetting-evidence",
            "exception cues and base-rule cues remain separately observable in the expected action key");
    }

    private static int NoGoIntervalFor(string noGoFrequency)
    {
        var digits = new string(noGoFrequency.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var interval) &&
            interval > 1)
        {
            return interval;
        }

        return noGoFrequency.Contains("third", StringComparison.OrdinalIgnoreCase)
            ? 3
            : DefaultNoGoInterval;
    }

    private static int ExceptionCountFor(GeneratedDrillContentRequest request)
    {
        return Math.Clamp(
            ParseLoadCount(request, "exception count") ?? DefaultExceptionCount,
            1,
            ExceptionTemplates.Length);
    }

    private static IReadOnlyList<ExceptionTemplate> SelectExceptions(
        GeneratedContentSeedPlan seedPlan,
        int exceptionCount)
    {
        return GeneratedContentStableHash.OrderByOrdinal(
                ExceptionTemplates,
                seedPlan.RequestFingerprint,
                "exception-definition",
                seedPlan.FreshnessOrdinal,
                exception => exception.Encoded)
            .Take(exceptionCount)
            .ToArray();
    }

    private static string CuePaceFor(string responseWindow)
    {
        if (responseWindow.EndsWith("cadence", StringComparison.OrdinalIgnoreCase))
        {
            return responseWindow;
        }

        var trimmed = responseWindow.Trim();
        if (string.Equals(trimmed, DefaultResponseWindow, StringComparison.OrdinalIgnoreCase))
        {
            return DefaultCuePace;
        }

        return trimmed.EndsWith(" seconds", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^8] + "-second cadence"
            : $"{trimmed} cadence";
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

    private static VisualStimulusResponseAction ResponseActionFor(string expectedAction)
    {
        return expectedAction switch
        {
            "tap" => VisualStimulusResponseAction.Tap,
            "withhold" => VisualStimulusResponseAction.Withhold,
            _ => throw new InvalidOperationException(
                $"Unsupported visual stimulus response action {expectedAction}."),
        };
    }

    private sealed record CueTemplate(
        VisualStimulusSpec Stimulus,
        string ExpectedAction)
    {
        public string Encoded => VisualStimulusCodec.Encode(Stimulus);
    }

    private sealed record BaseRuleItem(
        VisualStimulusSpec Stimulus,
        string ExpectedAction)
    {
        public string Encoded => VisualStimulusCodec.Encode(Stimulus);
    }

    private sealed record ExceptionTemplate(
        VisualStimulusSpec Stimulus,
        string ExpectedAction,
        string Reason)
    {
        public string Encoded => VisualStimulusCodec.Encode(Stimulus);
    }
}
