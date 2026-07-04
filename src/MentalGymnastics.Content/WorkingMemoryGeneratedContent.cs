using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class WorkingMemoryGeneratedContent
{
    internal WorkingMemoryGeneratedContent(
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

public static class WorkingMemoryGeneratedContentGenerator
{
    private const string NoRereadConstraint = "No rereading after encode window.";
    private const string NoInventedItemsConstraint = "No invented items.";
    private const string NoHiddenIntermediateNotesConstraint = "Intermediate notes prohibited unless specified.";
    private const string DefaultDetailDensity = "simple objects";
    private const string DefaultDelayedReconstructionDelay = "60 seconds";
    private const string DefaultMentalTransformDelay = "2 minutes";
    private const string DefaultInterference = "reversal";
    private const int DefaultDelayedReconstructionItemCount = 5;
    private const int DefaultMentalTransformItemCount = 6;
    private const int DefaultOperationStepCount = 2;

    private static readonly string[] SimpleObjectItems =
    [
        "brass key",
        "cedar cup",
        "blue tile",
        "paper ring",
        "silver pin",
        "cotton cord",
        "green bead",
        "plain card",
        "wooden block",
        "glass pebble",
        "linen tag",
        "red button",
        "stone marker",
        "black clip",
        "amber coin",
        "white shell",
        "iron hook",
        "violet thread",
        "clay disk",
        "clear cube",
    ];

    public static WorkingMemoryGeneratedContent Generate(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(seed);

        EnsureSupportedWorkingMemoryRequest(request);

        var seedPlan = GeneratedContentSeedDeriver.Derive(request, seed);
        IReadOnlyList<GeneratedContentMaterial> materials;
        IEnumerable<GeneratedContentPayloadFact> payloadFacts;

        if (request.Drill == DrillId.WM1DelayedReconstruction)
        {
            var encodeItems = SelectEncodeItems(
                seedPlan,
                ItemCountFor(request, DefaultDelayedReconstructionItemCount));
            materials = BuildDelayedReconstructionMaterials(request, encodeItems);
            payloadFacts = BuildDelayedReconstructionPayloadFacts(encodeItems);
        }
        else
        {
            var sourceItems = SelectSourceItems(
                seedPlan,
                ItemCountFor(request, DefaultMentalTransformItemCount));
            var transformPlan = BuildMentalTransformPlan(request, sourceItems);
            materials = BuildMentalTransformMaterials(request, transformPlan);
            payloadFacts = BuildMentalTransformPayloadFacts(transformPlan);
        }

        var result = new GeneratedDrillContentResult(
            request,
            seedPlan.Instance,
            seedPlan.PayloadFacts.Concat(payloadFacts));

        return new WorkingMemoryGeneratedContent(result, materials);
    }

    private static void EnsureSupportedWorkingMemoryRequest(GeneratedDrillContentRequest request)
    {
        if (request.Branch != BranchCode.WM)
        {
            throw new ArgumentException(
                "Working Memory generated content can only be produced for the WM branch.",
                nameof(request));
        }

        if (request.Drill is not DrillId.WM1DelayedReconstruction and not DrillId.WM2MentalTransform)
        {
            throw new ArgumentException(
                "Working Memory generation supports only WM-1 Delayed Reconstruction and WM-2 Mental Transform.",
                nameof(request));
        }

        if (request.ContentKind != PromptContentKind.DelayedReconstructionTask)
        {
            throw new ArgumentException(
                $"Drill {request.Drill} requires generated content kind {PromptContentKind.DelayedReconstructionTask}.",
                nameof(request));
        }
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildDelayedReconstructionMaterials(
        GeneratedDrillContentRequest request,
        IReadOnlyList<string> encodeItems)
    {
        var materials = new List<GeneratedContentMaterial>();

        AddLoadVariables(materials, request);
        AddHonestyConstraints(
            materials,
            request,
            [
                ("no-reread", NoRereadConstraint),
                ("no-invention", NoInventedItemsConstraint),
            ]);

        var detailDensity = LoadValueOrDefault(request, "detail density", DefaultDetailDensity);
        var delay = LoadValueOrDefault(request, "delay", DefaultDelayedReconstructionDelay);

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.EncodeInstruction,
            "encode-instruction",
            "Study the encode items once; no rereading after the encode window closes."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DetailDensity,
            "detail-density",
            detailDensity));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DelayLength,
            "delay",
            delay));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.ReconstructionInstruction,
            "reconstruction-instruction",
            "Reconstruct the items in order without rereading; do not invent items; leave omissions visible."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.ExpectedReconstruction,
            "expected-reconstruction",
            string.Join("|", encodeItems)));

        for (var i = 0; i < encodeItems.Count; i++)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.EncodeItem,
                $"encode-item-{(i + 1).ToString(CultureInfo.InvariantCulture)}",
                encodeItems[i]));
        }

        return Array.AsReadOnly(materials.ToArray());
    }

    private static MentalTransformPlan BuildMentalTransformPlan(
        GeneratedDrillContentRequest request,
        IReadOnlyList<string> sourceItems)
    {
        var operationStepCount = Math.Max(ParseLoadCount(request, "operation steps") ?? DefaultOperationStepCount, 1);
        var operations = BuildOperationSteps(operationStepCount);
        var finalItems = ApplyOperationSteps(sourceItems, operationStepCount);
        var interference = LoadValueOrDefault(request, "interference", DefaultInterference);
        var delay = LoadValueOrDefault(request, "delay", DefaultMentalTransformDelay);
        var detailDensity = LoadValueOrDefault(request, "detail density", DefaultDetailDensity);
        var transformRule = string.Equals(interference, DefaultInterference, StringComparison.OrdinalIgnoreCase)
            ? "Reverse the held source order, then apply each listed operation step without external notes."
            : $"Apply the listed operation steps to the held source items while preserving the {interference} interference demand.";
        var hiddenNotePolicy =
            "Hidden intermediate notes are prohibited; only the final output and operation explanation are recorded.";
        var explanationPrompt =
            "Explain the operation sequence used and how hidden intermediate notes were avoided.";

        return new MentalTransformPlan(
            sourceItems,
            operations,
            string.Join("|", finalItems),
            detailDensity,
            delay,
            interference,
            transformRule,
            hiddenNotePolicy,
            explanationPrompt);
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildMentalTransformMaterials(
        GeneratedDrillContentRequest request,
        MentalTransformPlan transformPlan)
    {
        var materials = new List<GeneratedContentMaterial>();

        AddLoadVariables(materials, request);
        AddHonestyConstraints(
            materials,
            request,
            [("no-hidden-intermediate-notes", NoHiddenIntermediateNotesConstraint)]);

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.SourceTask,
            "source-task",
            "Hold the source items, close the encode window, wait through the delay, transform mentally, then submit final output."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DetailDensity,
            "detail-density",
            transformPlan.DetailDensity));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.DelayLength,
            "delay",
            transformPlan.Delay));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.Interference,
            "interference",
            transformPlan.Interference));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TransformRule,
            "transform-rule",
            transformPlan.TransformRule));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.HiddenNotePolicy,
            "hidden-note-policy",
            transformPlan.HiddenNotePolicy));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RuleExplanationPrompt,
            "operation-explanation-prompt",
            transformPlan.ExplanationPrompt));

        for (var i = 0; i < transformPlan.SourceItems.Count; i++)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.SourceItem,
                $"source-item-{(i + 1).ToString(CultureInfo.InvariantCulture)}",
                transformPlan.SourceItems[i]));
        }

        for (var i = 0; i < transformPlan.OperationSteps.Count; i++)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.OperationStep,
                $"operation-step-{(i + 1).ToString(CultureInfo.InvariantCulture)}",
                transformPlan.OperationSteps[i]));
        }

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.FinalExpectedOutput,
            "final-expected-output",
            transformPlan.FinalExpectedOutput));

        return Array.AsReadOnly(materials.ToArray());
    }

    private static void AddLoadVariables(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedDrillContentRequest request)
    {
        foreach (var loadVariable in request.LoadVariables)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.LoadVariable,
                loadVariable.Name,
                loadVariable.Value));
        }
    }

    private static void AddHonestyConstraints(
        ICollection<GeneratedContentMaterial> materials,
        GeneratedDrillContentRequest request,
        IEnumerable<(string Name, string Value)> defaultConstraints)
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

        foreach (var (name, value) in defaultConstraints)
        {
            AddDefaultHonestyConstraint(materials, added, name, value);
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

    private static IEnumerable<GeneratedContentPayloadFact> BuildDelayedReconstructionPayloadFacts(
        IReadOnlyList<string> encodeItems)
    {
        var expectedReconstruction = string.Join("|", encodeItems);
        yield return new GeneratedContentPayloadFact("payload-family", "wm-delayed-reconstruction");
        yield return new GeneratedContentPayloadFact("comparison-key", expectedReconstruction);
        yield return new GeneratedContentPayloadFact(
            "omission-evidence",
            "missing expected item or blank position remains observable during comparison");
        yield return new GeneratedContentPayloadFact(
            "invention-evidence",
            "submitted item not present in encode set remains observable during comparison");
        yield return new GeneratedContentPayloadFact(
            "comparison-evidence",
            "compare submitted order against expected reconstruction item by item");
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildMentalTransformPayloadFacts(
        MentalTransformPlan transformPlan)
    {
        yield return new GeneratedContentPayloadFact("payload-family", "wm-mental-transform");
        yield return new GeneratedContentPayloadFact("source-items", string.Join("|", transformPlan.SourceItems));
        yield return new GeneratedContentPayloadFact("operation-steps", string.Join(" || ", transformPlan.OperationSteps));
        yield return new GeneratedContentPayloadFact("interference", transformPlan.Interference);
        yield return new GeneratedContentPayloadFact("delay", transformPlan.Delay);
        yield return new GeneratedContentPayloadFact("final-expected-output", transformPlan.FinalExpectedOutput);
        yield return new GeneratedContentPayloadFact(
            "operation-explanation-evidence",
            "final output must explain the operation sequence and avoided failure mode");
        yield return new GeneratedContentPayloadFact(
            "hidden-note-policy",
            transformPlan.HiddenNotePolicy);
        yield return new GeneratedContentPayloadFact(
            "source-retention-evidence",
            "lost, replaced, or invented source items remain observable against the source item list");
    }

    private static IReadOnlyList<string> SelectEncodeItems(
        GeneratedContentSeedPlan seedPlan,
        int itemCount)
    {
        return SelectItems(seedPlan, itemCount, "encode-items");
    }

    private static IReadOnlyList<string> SelectSourceItems(
        GeneratedContentSeedPlan seedPlan,
        int itemCount)
    {
        return SelectItems(seedPlan, itemCount, "source-items");
    }

    private static IReadOnlyList<string> SelectItems(
        GeneratedContentSeedPlan seedPlan,
        int itemCount,
        string purpose)
    {
        var firstIndex = (
            SelectIndex(seedPlan.PayloadSeed, purpose, seedPlan.VariantIndex, SimpleObjectItems.Length) +
            (seedPlan.VariantIndex * itemCount)) %
            SimpleObjectItems.Length;
        var selected = new List<string>();

        for (var i = 0; i < itemCount; i++)
        {
            selected.Add(SimpleObjectItems[(firstIndex + i) % SimpleObjectItems.Length]);
        }

        return Array.AsReadOnly(selected.ToArray());
    }

    private static IReadOnlyList<string> BuildOperationSteps(int operationStepCount)
    {
        var operations = new List<string>();

        for (var i = 0; i < operationStepCount; i++)
        {
            var stepNumber = (i + 1).ToString(CultureInfo.InvariantCulture);
            operations.Add((i % 4) switch
            {
                0 => $"Step {stepNumber}: reverse the held source item order.",
                1 => $"Step {stepNumber}: rotate the current order one position left.",
                2 => $"Step {stepNumber}: swap adjacent pairs in the current order.",
                _ => $"Step {stepNumber}: rotate the current order one position right.",
            });
        }

        return Array.AsReadOnly(operations.ToArray());
    }

    private static IReadOnlyList<string> ApplyOperationSteps(
        IReadOnlyList<string> sourceItems,
        int operationStepCount)
    {
        var transformed = sourceItems.ToList();

        for (var i = 0; i < operationStepCount; i++)
        {
            switch (i % 4)
            {
                case 0:
                    transformed.Reverse();
                    break;
                case 1:
                    RotateLeft(transformed);
                    break;
                case 2:
                    SwapAdjacentPairs(transformed);
                    break;
                default:
                    RotateRight(transformed);
                    break;
            }
        }

        return Array.AsReadOnly(transformed.ToArray());
    }

    private static void RotateLeft(IList<string> items)
    {
        if (items.Count <= 1)
        {
            return;
        }

        var first = items[0];
        items.RemoveAt(0);
        items.Add(first);
    }

    private static void RotateRight(IList<string> items)
    {
        if (items.Count <= 1)
        {
            return;
        }

        var last = items[^1];
        items.RemoveAt(items.Count - 1);
        items.Insert(0, last);
    }

    private static void SwapAdjacentPairs(IList<string> items)
    {
        for (var i = 0; i < items.Count - 1; i += 2)
        {
            (items[i], items[i + 1]) = (items[i + 1], items[i]);
        }
    }

    private static int SelectIndex(
        string payloadSeed,
        string purpose,
        int variantIndex,
        int length)
    {
        var hash = GeneratedContentStableHash.HashSegment(
            string.Join("|", payloadSeed, purpose, variantIndex.ToString(CultureInfo.InvariantCulture)));
        var baseIndex = Convert.ToInt32(hash[..6], 16);

        return (baseIndex + variantIndex) % length;
    }

    private static int ItemCountFor(
        GeneratedDrillContentRequest request,
        int defaultItemCount)
    {
        return Math.Clamp(
            ParseLoadCount(request, "item count") ?? defaultItemCount,
            1,
            SimpleObjectItems.Length);
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

    private sealed record MentalTransformPlan(
        IReadOnlyList<string> SourceItems,
        IReadOnlyList<string> OperationSteps,
        string FinalExpectedOutput,
        string DetailDensity,
        string Delay,
        string Interference,
        string TransformRule,
        string HiddenNotePolicy,
        string ExplanationPrompt);
}
