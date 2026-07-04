using System.Globalization;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class DiscriminationGeneratedContent
{
    internal DiscriminationGeneratedContent(
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

public static class DiscriminationGeneratedContentGenerator
{
    private const string MarkedGuessConstraint = "Guessing must be marked.";
    private const string OriginalOutputLockedConstraint = "Original output cannot be edited during audit.";
    private const string DefaultSimilarity = "near match";
    private const string DefaultTimeLimit = "60 seconds";
    private const string DefaultErrorSubtlety = "subtle wording errors";
    private const string DefaultOutputLength = "6 lines";
    private const string DefaultAuditDelay = "5 minutes";
    private const int DefaultPairCount = 6;
    private const int DefaultSeededErrorCount = 3;

    private static readonly DiscriminationPairTemplate[] PairTemplates =
    [
        new(
            "arc-17 blue left notch",
            "arc-17 blue right notch",
            "center notch position",
            "blue shade",
            false),
        new(
            "grid-42 three center dots",
            "grid-42 three center dots with pale border",
            "center dot count",
            "border shade",
            true),
        new(
            "line-8 long-short-long",
            "line-8 long-short-long with rotated label",
            "line length sequence",
            "label rotation",
            true),
        new(
            "tile-31 small upper gap",
            "tile-31 small lower gap",
            "gap position",
            "tile color",
            false),
        new(
            "mark-5 double inner slash",
            "mark-5 double inner slash with outer shadow",
            "inner slash count",
            "outer shadow",
            true),
        new(
            "node-26 closed hook",
            "node-26 open hook",
            "hook closure",
            "node label",
            false),
        new(
            "card-14 left offset bar",
            "card-14 right offset bar",
            "bar offset direction",
            "card texture",
            false),
        new(
            "ring-9 two inner ticks",
            "ring-9 two inner ticks with darker rim",
            "inner tick count",
            "rim darkness",
            true),
        new(
            "glyph-63 tall crossbar",
            "glyph-63 short crossbar",
            "crossbar height",
            "glyph tint",
            false),
        new(
            "panel-7 diagonal slot",
            "panel-7 diagonal slot with faint corner mark",
            "slot angle",
            "corner mark",
            true),
    ];

    private static readonly SeededAuditTemplate[] SeededAuditTemplates =
    [
        new(
            "route-summary",
            [
                "The north route opened at 08:10 after the gate check.",
                "Three crews inspected the east barrier before the report.",
                "The west marker was logged as blue in the first pass.",
                "All damaged panels were listed before noon.",
                "The follow-up count named twelve usable tags.",
                "The final note said the backup route remained closed.",
                "A later clerk copied the report into the archive.",
                "The review sheet kept the same route order.",
            ],
            [
                new(
                    "crew-count",
                    2,
                    "number mismatch",
                    "critical",
                    "the source key requires two crews, not three",
                    "finding: line 2 should report two crews instead of three"),
                new(
                    "marker-color",
                    3,
                    "attribute mismatch",
                    "noncritical",
                    "the west marker should be green, not blue",
                    "finding: line 3 should name the west marker as green"),
                new(
                    "tag-count",
                    5,
                    "number mismatch",
                    "critical",
                    "the follow-up count should name ten usable tags, not twelve",
                    "finding: line 5 should report ten usable tags"),
                new(
                    "route-state",
                    6,
                    "state mismatch",
                    "noncritical",
                    "the backup route remained open, not closed",
                    "finding: line 6 should say the backup route remained open"),
            ],
            [
                new("line 1 gate-check time is consistent with the source; not an error"),
                new("line 4 panel listing is complete; not an error"),
            ]),
        new(
            "inventory-note",
            [
                "The intake shelf held nine labeled packets before sorting.",
                "A yellow seal was attached to the second packet.",
                "The recorder placed the violet sample under tray B.",
                "The checksum field ended with code 47-A.",
                "No packet was removed during the first audit.",
                "The closing note assigned review to Mira.",
                "The duplicate sheet stayed in the gray binder.",
                "The final count matched the intake count.",
            ],
            [
                new(
                    "packet-count",
                    1,
                    "number mismatch",
                    "critical",
                    "the intake shelf held eight packets, not nine",
                    "finding: line 1 should report eight labeled packets"),
                new(
                    "seal-color",
                    2,
                    "attribute mismatch",
                    "noncritical",
                    "the seal should be orange, not yellow",
                    "finding: line 2 should name an orange seal"),
                new(
                    "checksum",
                    4,
                    "symbol mismatch",
                    "critical",
                    "the checksum should end with code 47-K, not 47-A",
                    "finding: line 4 should end with code 47-K"),
                new(
                    "review-owner",
                    6,
                    "name mismatch",
                    "noncritical",
                    "the review was assigned to Nira, not Mira",
                    "finding: line 6 should assign review to Nira"),
            ],
            [
                new("line 3 tray placement is correct; not an error"),
                new("line 5 removal statement is accurate; not an error"),
            ]),
        new(
            "signal-log",
            [
                "The first pulse arrived after the short calibration tone.",
                "Channel D carried the backup signal for the test.",
                "The operator paused the log after the fourth pulse.",
                "The green indicator stayed lit during the retry.",
                "The second retry used the lower gain setting.",
                "The status field marked the sequence as unstable.",
                "The spare receiver remained unplugged.",
                "The session label matched the morning batch.",
            ],
            [
                new(
                    "channel",
                    2,
                    "letter mismatch",
                    "critical",
                    "channel B carried the backup signal, not channel D",
                    "finding: line 2 should name channel B"),
                new(
                    "pause-count",
                    3,
                    "number mismatch",
                    "noncritical",
                    "the operator paused after the third pulse, not the fourth",
                    "finding: line 3 should say third pulse"),
                new(
                    "indicator-color",
                    4,
                    "attribute mismatch",
                    "critical",
                    "the indicator was amber, not green",
                    "finding: line 4 should name an amber indicator"),
                new(
                    "status-field",
                    6,
                    "state mismatch",
                    "noncritical",
                    "the sequence was marked stable, not unstable",
                    "finding: line 6 should mark the sequence as stable"),
            ],
            [
                new("line 1 calibration ordering is correct; not an error"),
                new("line 7 receiver state is correct; not an error"),
            ]),
    ];

    public static DiscriminationGeneratedContent Generate(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(seed);

        EnsureSupportedDiscriminationRequest(request);

        var seedPlan = GeneratedContentSeedDeriver.Derive(request, seed);
        var materials = request.Drill == DrillId.DE2SeededAudit
            ? BuildSeededAuditMaterials(request, BuildSeededAuditPlan(request, seedPlan))
            : BuildPairDiscriminationMaterials(request, SelectPairs(seedPlan, PairCountFor(request)));
        var result = new GeneratedDrillContentResult(
            request,
            seedPlan.Instance,
            seedPlan.PayloadFacts.Concat(BuildPayloadFacts(request, materials)));

        return new DiscriminationGeneratedContent(result, materials);
    }

    private static void EnsureSupportedDiscriminationRequest(GeneratedDrillContentRequest request)
    {
        if (request.Branch != BranchCode.DE)
        {
            throw new ArgumentException(
                "Discrimination generated content can only be produced for the DE branch.",
                nameof(request));
        }

        if (request.Drill is not DrillId.DE1PairDiscrimination and not DrillId.DE2SeededAudit)
        {
            throw new ArgumentException(
                "Discrimination generated content supports only DE-1 Pair Discrimination and DE-2 Seeded Audit.",
                nameof(request));
        }

        if (request.ContentKind != PromptContentKind.DiscriminationItemSet)
        {
            throw new ArgumentException(
                $"Drill {request.Drill} requires generated content kind {PromptContentKind.DiscriminationItemSet}.",
                nameof(request));
        }
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildPairDiscriminationMaterials(
        GeneratedDrillContentRequest request,
        IReadOnlyList<DiscriminationPairTemplate> pairs)
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

        var similarity = LoadValueOrDefault(request, "similarity", DefaultSimilarity);
        var timeLimit = LoadValueOrDefault(request, "time limit", DefaultTimeLimit);

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.RelevantFeature,
            "relevant-feature",
            "Compare only the named relevant feature; irrelevant differences are distractors and do not change the expected answer."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.Similarity,
            "similarity",
            similarity));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TimePressure,
            "time-limit",
            timeLimit));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.GuessHandling,
            "guess-handling",
            "Mark every guess; unmarked guesses fail the item and remain visible to runtime scoring."));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.FalsePositiveFalseNegativeKey,
            "false-positive-false-negative-key",
            "false positive = marked different when relevant feature matches; false negative = marked same or missed relevant difference."));

        for (var i = 0; i < pairs.Count; i++)
        {
            var pairNumber = (i + 1).ToString(CultureInfo.InvariantCulture);
            var pair = pairs[i];
            var truth = pair.RelevantFeaturesMatch ? "match" : "mismatch";
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.DiscriminationPair,
                $"pair-{pairNumber}",
                $"pair-{pairNumber}: left '{pair.LeftItem}' vs right '{pair.RightItem}'; relevant feature '{pair.RelevantFeature}'; irrelevant difference '{pair.IrrelevantDifference}'; expected {truth}"));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.MatchTruth,
                $"pair-{pairNumber}-truth",
                $"pair-{pairNumber}: {truth}; expected answer based only on relevant feature"));
        }

        return Array.AsReadOnly(materials.ToArray());
    }

    private static SeededAuditPlan BuildSeededAuditPlan(
        GeneratedDrillContentRequest request,
        GeneratedContentSeedPlan seedPlan)
    {
        var outputLength = LoadValueOrDefault(request, "output length", DefaultOutputLength);
        var lineCount = Math.Clamp(
            ParseLoadCount(request, "output length") ?? 6,
            3,
            8);
        var seededErrorCount = SeededErrorCountFor(request);
        var template = SelectSeededAuditTemplate(seedPlan);
        var availableErrors = template.Errors
            .Where(error => error.LineNumber <= lineCount)
            .ToArray();
        var errorStart = SelectIndex(seedPlan.PayloadSeed, "seeded-error", seedPlan.VariantIndex, availableErrors.Length);
        var selectedErrors = new List<SeededAuditErrorTemplate>();

        for (var i = 0; i < Math.Min(seededErrorCount, availableErrors.Length); i++)
        {
            selectedErrors.Add(availableErrors[(errorStart + i) % availableErrors.Length]);
        }

        return new SeededAuditPlan(
            template.TemplateId,
            template.Lines.Take(lineCount).ToArray(),
            selectedErrors,
            template.NonErrorDistractors,
            LoadValueOrDefault(request, "error subtlety", DefaultErrorSubtlety),
            outputLength,
            LoadValueOrDefault(request, "audit delay", DefaultAuditDelay));
    }

    private static IReadOnlyList<GeneratedContentMaterial> BuildSeededAuditMaterials(
        GeneratedDrillContentRequest request,
        SeededAuditPlan plan)
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

        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.LockedOriginalOutput,
            "locked-original-output",
            "locked original output; " + string.Join(
                " | ",
                plan.Lines.Select((line, index) =>
                    $"line {(index + 1).ToString(CultureInfo.InvariantCulture)}: {line}"))));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.ErrorSubtlety,
            "error-subtlety",
            plan.ErrorSubtlety));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.TaskLength,
            "output-length",
            plan.OutputLength));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.AuditDelay,
            "audit-delay",
            plan.AuditDelay));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.AuditInstruction,
            "audit-instruction",
            "Audit the locked original output after the delay; do not edit the original; report findings with line, error type, and evidence."));

        for (var i = 0; i < plan.Errors.Count; i++)
        {
            var error = plan.Errors[i];
            var errorNumber = (i + 1).ToString(CultureInfo.InvariantCulture);
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.SeededError,
                $"seeded-error-{errorNumber}",
                $"seeded error {error.ErrorId}: location line {error.LineNumber.ToString(CultureInfo.InvariantCulture)}; type {error.ErrorType}; criticality {error.Criticality}; {error.Description}"));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ExpectedFinding,
                $"expected-finding-{errorNumber}",
                $"finding {error.ErrorId}: {error.ExpectedFinding}"));
        }

        for (var i = 0; i < plan.NonErrorDistractors.Count; i++)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.NonErrorDistractor,
                $"non-error-distractor-{(i + 1).ToString(CultureInfo.InvariantCulture)}",
                plan.NonErrorDistractors[i]));
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

        AddDefaultHonestyConstraint(
            materials,
            added,
            request.Drill == DrillId.DE2SeededAudit
                ? "original-output-lock"
                : "marked-guesses",
            request.Drill == DrillId.DE2SeededAudit
                ? OriginalOutputLockedConstraint
                : MarkedGuessConstraint);
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

    private static IEnumerable<GeneratedContentPayloadFact> BuildPayloadFacts(
        GeneratedDrillContentRequest request,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        return request.Drill == DrillId.DE2SeededAudit
            ? BuildSeededAuditPayloadFacts(request, materials)
            : BuildPairDiscriminationPayloadFacts(request, materials);
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildPairDiscriminationPayloadFacts(
        GeneratedDrillContentRequest request,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var pairs = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.DiscriminationPair)
            .Select(material => material.Value)
            .ToArray();
        var matchTruth = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.MatchTruth)
            .Select(material => material.Value)
            .ToArray();
        var relevantFeature = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.RelevantFeature)
            .Value;

        yield return new GeneratedContentPayloadFact("payload-family", "de-pair-discrimination");
        yield return new GeneratedContentPayloadFact("item-pairs", string.Join("|", pairs));
        yield return new GeneratedContentPayloadFact("comparison-key", string.Join("|", matchTruth));
        yield return new GeneratedContentPayloadFact("relevant-feature-policy", relevantFeature);
        yield return new GeneratedContentPayloadFact(
            "similarity",
            LoadValueOrDefault(request, "similarity", DefaultSimilarity));
        yield return new GeneratedContentPayloadFact(
            "time-limit",
            LoadValueOrDefault(request, "time limit", DefaultTimeLimit));
        yield return new GeneratedContentPayloadFact(
            "guess-marking-policy",
            "all guesses must be marked; unmarked guesses fail and cannot be converted into clean evidence");
        yield return new GeneratedContentPayloadFact(
            "false-positive-evidence",
            "false positive evidence = marked different when relevant feature matches");
        yield return new GeneratedContentPayloadFact(
            "false-negative-evidence",
            "false negative evidence = marked same or missed relevant difference when relevant feature differs");
        yield return new GeneratedContentPayloadFact(
            "irrelevant-difference-policy",
            "irrelevant differences are recorded as distractors and cannot justify a different answer");
    }

    private static IEnumerable<GeneratedContentPayloadFact> BuildSeededAuditPayloadFacts(
        GeneratedDrillContentRequest request,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var lockedOutput = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.LockedOriginalOutput)
            .Value;
        var seededErrors = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.SeededError)
            .Select(material => material.Value)
            .ToArray();
        var expectedFindings = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedFinding)
            .Select(material => material.Value)
            .ToArray();
        var nonErrors = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.NonErrorDistractor)
            .Select(material => material.Value)
            .ToArray();

        yield return new GeneratedContentPayloadFact("payload-family", "de-seeded-audit");
        yield return new GeneratedContentPayloadFact("locked-original-output", lockedOutput);
        yield return new GeneratedContentPayloadFact("seeded-error-key", string.Join("|", seededErrors));
        yield return new GeneratedContentPayloadFact("expected-findings", string.Join("|", expectedFindings));
        yield return new GeneratedContentPayloadFact("non-error-distractors", string.Join("|", nonErrors));
        yield return new GeneratedContentPayloadFact(
            "error-subtlety",
            LoadValueOrDefault(request, "error subtlety", DefaultErrorSubtlety));
        yield return new GeneratedContentPayloadFact(
            "output-length",
            LoadValueOrDefault(request, "output length", DefaultOutputLength));
        yield return new GeneratedContentPayloadFact(
            "audit-delay",
            LoadValueOrDefault(request, "audit delay", DefaultAuditDelay));
        yield return new GeneratedContentPayloadFact(
            "original-output-lock",
            "the original output is locked and cannot be edited during audit; findings must reference line ids");
        yield return new GeneratedContentPayloadFact(
            "false-correction-evidence",
            "false correction evidence = marking a listed non-error distractor or unchanged correct line as an error");
        yield return new GeneratedContentPayloadFact(
            "invented-error-evidence",
            "invented errors remain auditable because expected findings and non-error distractors are fixed before audit");
    }

    private static IReadOnlyList<DiscriminationPairTemplate> SelectPairs(
        GeneratedContentSeedPlan seedPlan,
        int pairCount)
    {
        var firstIndex = (
            SelectIndex(seedPlan.PayloadSeed, "discrimination-pair", seedPlan.VariantIndex, PairTemplates.Length) +
            (seedPlan.VariantIndex * pairCount)) %
            PairTemplates.Length;
        var pairs = new List<DiscriminationPairTemplate>();

        for (var i = 0; i < pairCount; i++)
        {
            pairs.Add(PairTemplates[(firstIndex + i) % PairTemplates.Length]);
        }

        return Array.AsReadOnly(pairs.ToArray());
    }

    private static SeededAuditTemplate SelectSeededAuditTemplate(
        GeneratedContentSeedPlan seedPlan)
    {
        var index = (
            SelectIndex(seedPlan.PayloadSeed, "seeded-audit-template", seedPlan.VariantIndex, SeededAuditTemplates.Length) +
            seedPlan.VariantIndex) %
            SeededAuditTemplates.Length;

        return SeededAuditTemplates[index];
    }

    private static int PairCountFor(GeneratedDrillContentRequest request)
    {
        return Math.Clamp(
            ParseLoadCount(request, "item quantity") ??
            ParseLoadCount(request, "quantity") ??
            DefaultPairCount,
            1,
            PairTemplates.Length);
    }

    private static int SeededErrorCountFor(GeneratedDrillContentRequest request)
    {
        return Math.Clamp(
            ParseLoadCount(request, "quantity") ??
            DefaultSeededErrorCount,
            1,
            4);
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

    private sealed record DiscriminationPairTemplate(
        string LeftItem,
        string RightItem,
        string RelevantFeature,
        string IrrelevantDifference,
        bool RelevantFeaturesMatch);

    private sealed record SeededAuditTemplate(
        string TemplateId,
        IReadOnlyList<string> Lines,
        IReadOnlyList<SeededAuditErrorTemplate> Errors,
        IReadOnlyList<string> NonErrorDistractors);

    private sealed record SeededAuditErrorTemplate(
        string ErrorId,
        int LineNumber,
        string ErrorType,
        string Criticality,
        string Description,
        string ExpectedFinding);

    private sealed record SeededAuditPlan(
        string TemplateId,
        IReadOnlyList<string> Lines,
        IReadOnlyList<SeededAuditErrorTemplate> Errors,
        IReadOnlyList<string> NonErrorDistractors,
        string ErrorSubtlety,
        string OutputLength,
        string AuditDelay);
}
