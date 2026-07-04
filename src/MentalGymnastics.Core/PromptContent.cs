namespace MentalGymnastics.Core;

public sealed class PromptContentIdentity
{
    public PromptContentIdentity(
        string contentId,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        PromptContentKind kind,
        string equivalenceClass)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            throw new ArgumentException("Prompt content must have a stable content id.", nameof(contentId));
        }

        if (string.IsNullOrWhiteSpace(equivalenceClass))
        {
            throw new ArgumentException(
                "Prompt content must name the equivalence class that preserves the tested demand.",
                nameof(equivalenceClass));
        }

        ContentId = contentId;
        Branch = branch;
        Level = level;
        Drill = drill;
        Kind = kind;
        EquivalenceClass = equivalenceClass;
    }

    public string ContentId { get; }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public PromptContentKind Kind { get; }

    public string EquivalenceClass { get; }
}

public interface IDrillPromptContent
{
    PromptContentIdentity Identity { get; }

    PromptFreshnessPolicy RetestFreshnessPolicy { get; }

    IReadOnlyList<LoadVariable> LoadVariables { get; }

    IReadOnlyList<CriticalConstraint> CriticalConstraints { get; }
}

public interface IEquivalentPromptContent : IDrillPromptContent
{
    string PromptText { get; }
}

public interface ICueSequenceContent : IDrillPromptContent
{
    IReadOnlyList<CueStep> Cues { get; }
}

public interface IDelayedReconstructionContent : IDrillPromptContent
{
    IReadOnlyList<ReconstructionItem> Items { get; }

    int DelaySeconds { get; }

    string ReconstructionInstruction { get; }
}

public interface IDiscriminationContent : IDrillPromptContent
{
    IReadOnlyList<DiscriminationItemPair> Items { get; }
}

public interface IRuleExampleContent : IDrillPromptContent
{
    IReadOnlyList<RuleExample> Examples { get; }
}

public interface IPromptContentSource
{
    IReadOnlyList<IDrillPromptContent> ListContent();
}

public abstract class DrillPromptContentBase : IDrillPromptContent
{
    protected DrillPromptContentBase(
        PromptContentIdentity identity,
        PromptFreshnessPolicy retestFreshnessPolicy,
        IEnumerable<LoadVariable> loadVariables,
        IEnumerable<CriticalConstraint> criticalConstraints)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(loadVariables);
        ArgumentNullException.ThrowIfNull(criticalConstraints);

        var loadVariableArray = loadVariables.ToArray();
        if (loadVariableArray.Length == 0)
        {
            throw new ArgumentException(
                "Prompt content must name the load variables it controls.",
                nameof(loadVariables));
        }

        var criticalConstraintArray = criticalConstraints.ToArray();
        if (criticalConstraintArray.Length == 0)
        {
            throw new ArgumentException(
                "Prompt content must preserve at least one honesty or critical constraint.",
                nameof(criticalConstraints));
        }

        Identity = identity;
        RetestFreshnessPolicy = retestFreshnessPolicy;
        LoadVariables = loadVariableArray;
        CriticalConstraints = criticalConstraintArray;
    }

    public PromptContentIdentity Identity { get; }

    public PromptFreshnessPolicy RetestFreshnessPolicy { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public IReadOnlyList<CriticalConstraint> CriticalConstraints { get; }
}

public sealed class EquivalentPromptContent : DrillPromptContentBase, IEquivalentPromptContent
{
    public EquivalentPromptContent(
        PromptContentIdentity identity,
        PromptFreshnessPolicy retestFreshnessPolicy,
        IEnumerable<LoadVariable> loadVariables,
        IEnumerable<CriticalConstraint> criticalConstraints,
        string promptText)
        : base(identity, retestFreshnessPolicy, loadVariables, criticalConstraints)
    {
        if (string.IsNullOrWhiteSpace(promptText))
        {
            throw new ArgumentException("Prompt text must be present.", nameof(promptText));
        }

        PromptText = promptText;
    }

    public string PromptText { get; }
}

public sealed class CueStep
{
    public CueStep(
        int position,
        string cue,
        bool isValidCue)
    {
        Position = position;
        Cue = cue;
        IsValidCue = isValidCue;
    }

    public int Position { get; }

    public string Cue { get; }

    public bool IsValidCue { get; }
}

public sealed class CueSequenceContent : DrillPromptContentBase, ICueSequenceContent
{
    public CueSequenceContent(
        PromptContentIdentity identity,
        PromptFreshnessPolicy retestFreshnessPolicy,
        IEnumerable<LoadVariable> loadVariables,
        IEnumerable<CriticalConstraint> criticalConstraints,
        IEnumerable<CueStep> cues)
        : base(identity, retestFreshnessPolicy, loadVariables, criticalConstraints)
    {
        ArgumentNullException.ThrowIfNull(cues);

        var cueArray = cues.ToArray();
        if (cueArray.Length == 0)
        {
            throw new ArgumentException("Cue sequence content must include cues.", nameof(cues));
        }

        if (cueArray.Any(cue => cue.Position <= 0 || string.IsNullOrWhiteSpace(cue.Cue)))
        {
            throw new ArgumentException("Every cue must have a positive position and cue value.", nameof(cues));
        }

        Cues = cueArray.OrderBy(cue => cue.Position).ToArray();
    }

    public IReadOnlyList<CueStep> Cues { get; }
}

public sealed record ReconstructionItem(
    int Position,
    string Content);

public sealed class DelayedReconstructionContent : DrillPromptContentBase, IDelayedReconstructionContent
{
    public DelayedReconstructionContent(
        PromptContentIdentity identity,
        PromptFreshnessPolicy retestFreshnessPolicy,
        IEnumerable<LoadVariable> loadVariables,
        IEnumerable<CriticalConstraint> criticalConstraints,
        IEnumerable<ReconstructionItem> items,
        int delaySeconds,
        string reconstructionInstruction)
        : base(identity, retestFreshnessPolicy, loadVariables, criticalConstraints)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(delaySeconds);

        if (string.IsNullOrWhiteSpace(reconstructionInstruction))
        {
            throw new ArgumentException(
                "Delayed reconstruction content must include a reconstruction instruction.",
                nameof(reconstructionInstruction));
        }

        var itemArray = items.ToArray();
        if (itemArray.Length == 0)
        {
            throw new ArgumentException(
                "Delayed reconstruction content must include items to encode.",
                nameof(items));
        }

        if (itemArray.Any(item => item.Position <= 0 || string.IsNullOrWhiteSpace(item.Content)))
        {
            throw new ArgumentException(
                "Every reconstruction item must have a positive position and content.",
                nameof(items));
        }

        Items = itemArray.OrderBy(item => item.Position).ToArray();
        DelaySeconds = delaySeconds;
        ReconstructionInstruction = reconstructionInstruction;
    }

    public IReadOnlyList<ReconstructionItem> Items { get; }

    public int DelaySeconds { get; }

    public string ReconstructionInstruction { get; }
}

public sealed class DiscriminationItemPair
{
    public DiscriminationItemPair(
        string left,
        string right,
        string relevantDifference,
        bool isMatch)
    {
        Left = left;
        Right = right;
        RelevantDifference = relevantDifference;
        IsMatch = isMatch;
    }

    public string Left { get; }

    public string Right { get; }

    public string RelevantDifference { get; }

    public bool IsMatch { get; }
}

public sealed class DiscriminationItemSetContent : DrillPromptContentBase, IDiscriminationContent
{
    public DiscriminationItemSetContent(
        PromptContentIdentity identity,
        PromptFreshnessPolicy retestFreshnessPolicy,
        IEnumerable<LoadVariable> loadVariables,
        IEnumerable<CriticalConstraint> criticalConstraints,
        IEnumerable<DiscriminationItemPair> items)
        : base(identity, retestFreshnessPolicy, loadVariables, criticalConstraints)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemArray = items.ToArray();
        if (itemArray.Length == 0)
        {
            throw new ArgumentException(
                "Discrimination content must include comparison items.",
                nameof(items));
        }

        if (itemArray.Any(item =>
            string.IsNullOrWhiteSpace(item.Left) ||
            string.IsNullOrWhiteSpace(item.Right) ||
            string.IsNullOrWhiteSpace(item.RelevantDifference)))
        {
            throw new ArgumentException(
                "Every discrimination item must expose both sides and the relevant difference.",
                nameof(items));
        }

        Items = itemArray;
    }

    public IReadOnlyList<DiscriminationItemPair> Items { get; }
}

public sealed class RuleExample
{
    public RuleExample(
        string input,
        string expectedClassification,
        bool isPositiveExample,
        bool isUnseenTestExample)
    {
        Input = input;
        ExpectedClassification = expectedClassification;
        IsPositiveExample = isPositiveExample;
        IsUnseenTestExample = isUnseenTestExample;
    }

    public string Input { get; }

    public string ExpectedClassification { get; }

    public bool IsPositiveExample { get; }

    public bool IsUnseenTestExample { get; }
}

public sealed class RuleExampleSetContent : DrillPromptContentBase, IRuleExampleContent
{
    public RuleExampleSetContent(
        PromptContentIdentity identity,
        PromptFreshnessPolicy retestFreshnessPolicy,
        IEnumerable<LoadVariable> loadVariables,
        IEnumerable<CriticalConstraint> criticalConstraints,
        IEnumerable<RuleExample> examples)
        : base(identity, retestFreshnessPolicy, loadVariables, criticalConstraints)
    {
        ArgumentNullException.ThrowIfNull(examples);

        var exampleArray = examples.ToArray();
        if (exampleArray.Length == 0)
        {
            throw new ArgumentException("Rule example content must include examples.", nameof(examples));
        }

        if (exampleArray.Any(example =>
            string.IsNullOrWhiteSpace(example.Input) ||
            string.IsNullOrWhiteSpace(example.ExpectedClassification)))
        {
            throw new ArgumentException(
                "Every rule example must include input and expected classification.",
                nameof(examples));
        }

        Examples = exampleArray;
    }

    public IReadOnlyList<RuleExample> Examples { get; }
}

public sealed class PromptContentSelectionRequest
{
    public PromptContentSelectionRequest(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        PromptContentKind kind,
        string equivalenceClass,
        PromptFreshnessPolicy freshnessPolicy,
        IEnumerable<string> previouslyUsedContentIds)
    {
        ArgumentNullException.ThrowIfNull(previouslyUsedContentIds);

        if (string.IsNullOrWhiteSpace(equivalenceClass))
        {
            throw new ArgumentException(
                "Selection requests must name an equivalence class.",
                nameof(equivalenceClass));
        }

        Branch = branch;
        Level = level;
        Drill = drill;
        Kind = kind;
        EquivalenceClass = equivalenceClass;
        FreshnessPolicy = freshnessPolicy;
        PreviouslyUsedContentIds = previouslyUsedContentIds
            .Where(contentId => !string.IsNullOrWhiteSpace(contentId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public PromptContentKind Kind { get; }

    public string EquivalenceClass { get; }

    public PromptFreshnessPolicy FreshnessPolicy { get; }

    public IReadOnlyList<string> PreviouslyUsedContentIds { get; }
}

public sealed record PromptContentSelectionFailure(
    PromptContentSelectionFailureKind Kind,
    string Detail);

public sealed record PromptContentSelectionResult(
    IDrillPromptContent? SelectedContent,
    IReadOnlyList<PromptContentSelectionFailure> Failures)
{
    public bool HasSelection => SelectedContent is not null;
}

public static class DeterministicPromptContentSelector
{
    public static PromptContentSelectionResult Select(
        IPromptContentSource source,
        PromptContentSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        var equivalentCandidates = source.ListContent()
            .Where(content => MatchesRequest(content.Identity, request))
            .OrderBy(content => content.Identity.ContentId, StringComparer.Ordinal)
            .ToArray();

        if (equivalentCandidates.Length == 0)
        {
            return Failure(
                PromptContentSelectionFailureKind.NoEquivalentContentAvailable,
                "No content matched the requested branch, level, drill, kind, and equivalence class.");
        }

        var eligibleCandidates = ApplyFreshnessPolicy(equivalentCandidates, request).ToArray();
        if (eligibleCandidates.Length == 0)
        {
            return Failure(
                PromptContentSelectionFailureKind.FreshEquivalentContentUnavailable,
                "Fresh equivalent content is required, but every equivalent fixture was already used.");
        }

        return new PromptContentSelectionResult(eligibleCandidates[0], []);
    }

    private static bool MatchesRequest(
        PromptContentIdentity identity,
        PromptContentSelectionRequest request)
    {
        return identity.Branch == request.Branch &&
            identity.Level == request.Level &&
            identity.Drill == request.Drill &&
            identity.Kind == request.Kind &&
            string.Equals(identity.EquivalenceClass, request.EquivalenceClass, StringComparison.Ordinal);
    }

    private static IEnumerable<IDrillPromptContent> ApplyFreshnessPolicy(
        IEnumerable<IDrillPromptContent> candidates,
        PromptContentSelectionRequest request)
    {
        var usedContentIds = request.PreviouslyUsedContentIds.ToHashSet(StringComparer.Ordinal);
        return candidates.Where(candidate =>
            !RequiresFreshEquivalentContent(candidate, request) ||
            !usedContentIds.Contains(candidate.Identity.ContentId));
    }

    private static bool RequiresFreshEquivalentContent(
        IDrillPromptContent candidate,
        PromptContentSelectionRequest request)
    {
        return request.FreshnessPolicy == PromptFreshnessPolicy.FreshEquivalentRequired ||
            candidate.RetestFreshnessPolicy == PromptFreshnessPolicy.FreshEquivalentRequired;
    }

    private static PromptContentSelectionResult Failure(
        PromptContentSelectionFailureKind kind,
        string detail)
    {
        return new PromptContentSelectionResult(
            SelectedContent: null,
            [new PromptContentSelectionFailure(kind, detail)]);
    }
}
