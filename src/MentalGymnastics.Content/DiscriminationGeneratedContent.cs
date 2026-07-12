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
    private const string SourceReferenceHiddenConstraint =
        "The source record cannot be reopened, copied, or externally noted after study.";
    private const string DefaultSimilarity = "near match";
    private const string DefaultTimeLimit = "60 seconds";
    private const string DefaultErrorSubtlety = "subtle wording errors";
    private const string DefaultOutputLength = "6 lines";
    private const string DefaultAuditDelay = "5 minutes";
    private const int DefaultPairCount = 6;
    private const int DefaultSeededErrorCount = 3;

    private static readonly DiscriminationPairTemplate[] PairTemplates =
    [
        new(new VisualStimulusPairSpec(
            new(
                VisualStimulusShape.Circle,
                VisualStimulusColor.Blue,
                Mark: VisualStimulusMark.Notch,
                MarkPosition: VisualStimulusPosition.Left,
                MarkCount: 1),
            new(
                VisualStimulusShape.Circle,
                VisualStimulusColor.Blue,
                Mark: VisualStimulusMark.Notch,
                MarkPosition: VisualStimulusPosition.Right,
                MarkCount: 1),
            VisualStimulusFeature.MarkPosition)),
        new(new VisualStimulusPairSpec(
            new(
                VisualStimulusShape.Square,
                VisualStimulusColor.Blue,
                Mark: VisualStimulusMark.Dot,
                MarkCount: 3,
                Border: VisualStimulusBorder.Light),
            new(
                VisualStimulusShape.Square,
                VisualStimulusColor.Blue,
                Mark: VisualStimulusMark.Dot,
                MarkCount: 3,
                Border: VisualStimulusBorder.Heavy),
            VisualStimulusFeature.MarkCount)),
        new(new VisualStimulusPairSpec(
            new(
                VisualStimulusShape.Square,
                VisualStimulusColor.Gray,
                Mark: VisualStimulusMark.Bar,
                MarkCount: 1,
                MarkOrientation: VisualStimulusOrientation.Horizontal),
            new(
                VisualStimulusShape.Square,
                VisualStimulusColor.Black,
                Mark: VisualStimulusMark.Bar,
                MarkCount: 1,
                MarkOrientation: VisualStimulusOrientation.Horizontal),
            VisualStimulusFeature.MarkOrientation)),
        new(new VisualStimulusPairSpec(
            new(
                VisualStimulusShape.Square,
                VisualStimulusColor.Green,
                Size: VisualStimulusSize.Small,
                Mark: VisualStimulusMark.Notch,
                MarkPosition: VisualStimulusPosition.Top,
                MarkCount: 1),
            new(
                VisualStimulusShape.Square,
                VisualStimulusColor.Green,
                Size: VisualStimulusSize.Small,
                Mark: VisualStimulusMark.Notch,
                MarkPosition: VisualStimulusPosition.Bottom,
                MarkCount: 1),
            VisualStimulusFeature.MarkPosition)),
        new(new VisualStimulusPairSpec(
            new(
                VisualStimulusShape.Circle,
                VisualStimulusColor.Black,
                Mark: VisualStimulusMark.Slash,
                MarkCount: 2,
                MarkOrientation: VisualStimulusOrientation.DiagonalRight),
            new(
                VisualStimulusShape.Circle,
                VisualStimulusColor.Black,
                Mark: VisualStimulusMark.Slash,
                MarkCount: 2,
                MarkOrientation: VisualStimulusOrientation.DiagonalRight,
                Border: VisualStimulusBorder.Heavy),
            VisualStimulusFeature.MarkCount)),
        new(new VisualStimulusPairSpec(
            new(
                VisualStimulusShape.Ring,
                VisualStimulusColor.Black,
                Fill: VisualStimulusFill.Outline),
            new(
                VisualStimulusShape.Ring,
                VisualStimulusColor.Black,
                Fill: VisualStimulusFill.Crossed),
            VisualStimulusFeature.Fill)),
        new(new VisualStimulusPairSpec(
            new(
                VisualStimulusShape.Arrow,
                VisualStimulusColor.Gray,
                Direction: VisualStimulusDirection.West),
            new(
                VisualStimulusShape.Arrow,
                VisualStimulusColor.Gray,
                Direction: VisualStimulusDirection.East),
            VisualStimulusFeature.Direction)),
        new(new VisualStimulusPairSpec(
            new(
                VisualStimulusShape.Ring,
                VisualStimulusColor.Violet,
                Fill: VisualStimulusFill.Outline,
                Mark: VisualStimulusMark.Tick,
                MarkCount: 2,
                MarkOrientation: VisualStimulusOrientation.Vertical,
                Border: VisualStimulusBorder.Light),
            new(
                VisualStimulusShape.Ring,
                VisualStimulusColor.Violet,
                Fill: VisualStimulusFill.Outline,
                Mark: VisualStimulusMark.Tick,
                MarkCount: 2,
                MarkOrientation: VisualStimulusOrientation.Vertical,
                Border: VisualStimulusBorder.Heavy),
            VisualStimulusFeature.MarkCount)),
        new(new VisualStimulusPairSpec(
            new(
                VisualStimulusShape.Square,
                VisualStimulusColor.Amber,
                Size: VisualStimulusSize.Large,
                Mark: VisualStimulusMark.Crossbar,
                MarkCount: 1,
                MarkOrientation: VisualStimulusOrientation.Horizontal),
            new(
                VisualStimulusShape.Square,
                VisualStimulusColor.Amber,
                Size: VisualStimulusSize.Small,
                Mark: VisualStimulusMark.Crossbar,
                MarkCount: 1,
                MarkOrientation: VisualStimulusOrientation.Horizontal),
            VisualStimulusFeature.Size)),
        new(new VisualStimulusPairSpec(
            new(
                VisualStimulusShape.Square,
                VisualStimulusColor.Blue,
                Mark: VisualStimulusMark.Slot,
                MarkCount: 1,
                MarkOrientation: VisualStimulusOrientation.DiagonalRight),
            new(
                VisualStimulusShape.Square,
                VisualStimulusColor.Gray,
                Mark: VisualStimulusMark.Slot,
                MarkCount: 1,
                MarkOrientation: VisualStimulusOrientation.DiagonalRight),
            VisualStimulusFeature.MarkOrientation)),
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
                    "finding: line 2 should report two crews instead of three",
                    "Two crews inspected the east barrier before the report."),
                new(
                    "marker-color",
                    3,
                    "attribute mismatch",
                    "noncritical",
                    "the west marker should be green, not blue",
                    "finding: line 3 should name the west marker as green",
                    "The west marker was logged as green in the first pass."),
                new(
                    "tag-count",
                    5,
                    "number mismatch",
                    "critical",
                    "the follow-up count should name ten usable tags, not twelve",
                    "finding: line 5 should report ten usable tags",
                    "The follow-up count named ten usable tags."),
                new(
                    "route-state",
                    6,
                    "state mismatch",
                    "noncritical",
                    "the backup route remained open, not closed",
                    "finding: line 6 should say the backup route remained open",
                    "The final note said the backup route remained open."),
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
                    "finding: line 1 should report eight labeled packets",
                    "The intake shelf held eight labeled packets before sorting."),
                new(
                    "seal-color",
                    2,
                    "attribute mismatch",
                    "noncritical",
                    "the seal should be orange, not yellow",
                    "finding: line 2 should name an orange seal",
                    "An orange seal was attached to the second packet."),
                new(
                    "checksum",
                    4,
                    "symbol mismatch",
                    "critical",
                    "the checksum should end with code 47-K, not 47-A",
                    "finding: line 4 should end with code 47-K",
                    "The checksum field ended with code 47-K."),
                new(
                    "review-owner",
                    6,
                    "name mismatch",
                    "noncritical",
                    "the review was assigned to Nira, not Mira",
                    "finding: line 6 should assign review to Nira",
                    "The closing note assigned review to Nira."),
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
                    "finding: line 2 should name channel B",
                    "Channel B carried the backup signal for the test."),
                new(
                    "pause-count",
                    3,
                    "number mismatch",
                    "noncritical",
                    "the operator paused after the third pulse, not the fourth",
                    "finding: line 3 should say third pulse",
                    "The operator paused the log after the third pulse."),
                new(
                    "indicator-color",
                    4,
                    "attribute mismatch",
                    "critical",
                    "the indicator was amber, not green",
                    "finding: line 4 should name an amber indicator",
                    "The amber indicator stayed lit during the retry."),
                new(
                    "status-field",
                    6,
                    "state mismatch",
                    "noncritical",
                    "the sequence was marked stable, not unstable",
                    "finding: line 6 should mark the sequence as stable",
                    "The status field marked the sequence as stable."),
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
            var truth = pair.Pair.RelevantFeatureMatches ? "match" : "mismatch";
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.DiscriminationPair,
                $"pair-{pairNumber}",
                VisualStimulusCodec.EncodePair(pair.Pair)));
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
            16);
        var seededErrorCount = SeededErrorCountFor(request);
        var sectionCount = Math.Max(
            (int)Math.Ceiling(lineCount / 8m),
            (int)Math.Ceiling(seededErrorCount / 4m));
        var templateSlot = seedPlan.FreshnessOrdinal % SeededAuditTemplates.Length;
        var templateStart = GeneratedContentStableHash.OrdinalIndex(
            seedPlan.RequestFingerprint,
            "seeded-audit-template",
            templateSlot,
            SeededAuditTemplates.Length);
        var lines = new List<string>();
        var referenceLines = new List<string>();
        var errors = new List<SeededAuditErrorTemplate>();
        var distractors = new List<string>();
        var templateIds = new List<string>();
        for (var section = 0; section < sectionCount; section++)
        {
            var template = SeededAuditTemplates[(templateStart + section) % SeededAuditTemplates.Length];
            var lineOffset = lines.Count;
            templateIds.Add(template.TemplateId);
            lines.AddRange(template.Lines);
            var templateReference = template.Lines.ToArray();
            foreach (var error in template.Errors)
            {
                templateReference[error.LineNumber - 1] = error.CorrectLine;
            }

            referenceLines.AddRange(templateReference);
            errors.AddRange(template.Errors.Select(error => error with
            {
                ErrorId = $"{template.TemplateId}-{error.ErrorId}",
                LineNumber = lineOffset + error.LineNumber,
            }));
            distractors.AddRange(template.NonErrorDistractors.Select(item =>
                $"section {section + 1}: {item}"));
        }

        var availableErrors = errors
            .Where(error => error.LineNumber <= lineCount)
            .ToArray();
        var selectedErrors = GeneratedContentStableHash.OrderByOrdinal(
                availableErrors,
                seedPlan.RequestFingerprint,
                "seeded-error",
                seedPlan.FreshnessOrdinal / SeededAuditTemplates.Length,
                error => error.ErrorId)
            .Take(Math.Min(seededErrorCount, availableErrors.Length))
            .ToArray();

        var selectedReference = referenceLines.Take(lineCount).ToArray();
        var lockedOutput = selectedReference.ToArray();
        foreach (var error in selectedErrors)
        {
            lockedOutput[error.LineNumber - 1] = lines[error.LineNumber - 1];
        }

        return new SeededAuditPlan(
            string.Join("+", templateIds),
            selectedReference,
            lockedOutput,
            selectedErrors,
            distractors,
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
            GeneratedContentMaterialKind.AuditReference,
            "audit-reference",
            "source record; " + string.Join(
                " | ",
                plan.ReferenceLines.Select((line, index) =>
                    $"line {(index + 1).ToString(CultureInfo.InvariantCulture)}: {line}"))));
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.LockedOriginalOutput,
            "locked-original-output",
            "locked original output; " + string.Join(
                " | ",
                plan.LockedOutputLines.Select((line, index) =>
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
        var taskLength = request.LoadVariables.FirstOrDefault(loadVariable =>
            string.Equals(loadVariable.Name, "task length", StringComparison.OrdinalIgnoreCase));
        if (taskLength is not null)
        {
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.TaskLength,
                "task-length",
                taskLength.Value));
        }
        materials.Add(new GeneratedContentMaterial(
            GeneratedContentMaterialKind.AuditInstruction,
            "audit-instruction",
            $"Study the source record, then audit the locked report after the delay; do not edit the report; " +
            $"do not copy or note the source; report {plan.Errors.Count.ToString(CultureInfo.InvariantCulture)} findings, " +
            "one per line, with line number, mismatch type, and exact correction."));

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

        if (request.Drill == DrillId.DE2SeededAudit)
        {
            AddDefaultHonestyConstraint(
                materials,
                added,
                "original-output-lock",
                OriginalOutputLockedConstraint);
            AddDefaultHonestyConstraint(
                materials,
                added,
                "source-reference-hidden",
                SourceReferenceHiddenConstraint);
            return;
        }

        AddDefaultHonestyConstraint(
            materials,
            added,
            "marked-guesses",
            MarkedGuessConstraint);
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
        var auditReference = materials
            .Single(material => material.Kind == GeneratedContentMaterialKind.AuditReference)
            .Value;
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
        yield return new GeneratedContentPayloadFact("audit-reference", auditReference);
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
        var ordered = GeneratedContentStableHash.OrderByOrdinal(
            PairTemplates,
            seedPlan.RequestFingerprint,
            "discrimination-pair",
            seedPlan.FreshnessOrdinal,
            pair => VisualStimulusCodec.EncodePair(pair.Pair));
        var pairs = new List<DiscriminationPairTemplate>();

        for (var i = 0; i < pairCount; i++)
        {
            var template = ordered[i % ordered.Count];
            var cycle = i / PairTemplates.Length;
            pairs.Add(cycle % 2 == 0
                ? template
                : template with
                {
                    Pair = template.Pair with
                    {
                        First = template.Pair.Second,
                        Second = template.Pair.First,
                    },
                });
        }

        return Array.AsReadOnly(pairs.ToArray());
    }

    private static SeededAuditTemplate SelectSeededAuditTemplate(
        GeneratedContentSeedPlan seedPlan)
    {
        var index = GeneratedContentStableHash.OrdinalIndex(
            seedPlan.RequestFingerprint,
            "seeded-audit-template",
            seedPlan.FreshnessOrdinal,
            SeededAuditTemplates.Length);

        return SeededAuditTemplates[index];
    }

    private static int PairCountFor(GeneratedDrillContentRequest request)
    {
        return Math.Max(
            ParseLoadCount(request, "item quantity") ??
            ParseLoadCount(request, "quantity") ??
            DefaultPairCount,
            1);
    }

    private static int SeededErrorCountFor(GeneratedDrillContentRequest request)
    {
        return Math.Clamp(
            ParseLoadCount(request, "quantity") ??
            DefaultSeededErrorCount,
            1,
            8);
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

    private sealed record DiscriminationPairTemplate(VisualStimulusPairSpec Pair);

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
        string ExpectedFinding,
        string CorrectLine);

    private sealed record SeededAuditPlan(
        string TemplateId,
        IReadOnlyList<string> ReferenceLines,
        IReadOnlyList<string> LockedOutputLines,
        IReadOnlyList<SeededAuditErrorTemplate> Errors,
        IReadOnlyList<string> NonErrorDistractors,
        string ErrorSubtlety,
        string OutputLength,
        string AuditDelay);
}
