using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class GeneratedDrillContentRequest
{
    public GeneratedDrillContentRequest(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        SessionType sessionType,
        PromptContentKind contentKind,
        string equivalenceClass,
        PromptFreshnessPolicy freshnessPolicy,
        IEnumerable<LoadVariable> loadVariables,
        IEnumerable<CriticalConstraint> criticalConstraints,
        IEnumerable<string>? previouslyUsedContentIds = null)
    {
        GeneratedContentValidation.EnsureDefined(branch, nameof(branch));
        GeneratedContentValidation.EnsureDefined(level, nameof(level));
        GeneratedContentValidation.EnsureDefined(drill, nameof(drill));
        GeneratedContentValidation.EnsureDefined(sessionType, nameof(sessionType));
        GeneratedContentValidation.EnsureDefined(contentKind, nameof(contentKind));
        GeneratedContentValidation.EnsureDefined(freshnessPolicy, nameof(freshnessPolicy));

        if (string.IsNullOrWhiteSpace(equivalenceClass))
        {
            throw new ArgumentException(
                "Generated content requests must name the equivalence class.",
                nameof(equivalenceClass));
        }

        Branch = branch;
        Level = level;
        Drill = drill;
        SessionType = sessionType;
        ContentKind = contentKind;
        EquivalenceClass = equivalenceClass;
        FreshnessPolicy = freshnessPolicy;
        LoadVariables = GeneratedContentValidation.RequireLoadVariables(
            loadVariables,
            "Generated content requests must include load variables.",
            "Generated content request load variables must include a name and value.",
            nameof(loadVariables));
        CriticalConstraints = GeneratedContentValidation.RequireCriticalConstraints(
            criticalConstraints,
            "Generated content requests must include critical or honesty constraints.",
            "Generated content request critical constraints must include descriptions.",
            nameof(criticalConstraints));
        PreviouslyUsedContentIds = (previouslyUsedContentIds ?? [])
            .Where(contentId => !string.IsNullOrWhiteSpace(contentId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public SessionType SessionType { get; }

    public PromptContentKind ContentKind { get; }

    public string EquivalenceClass { get; }

    public PromptFreshnessPolicy FreshnessPolicy { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public IReadOnlyList<CriticalConstraint> CriticalConstraints { get; }

    public IReadOnlyList<string> PreviouslyUsedContentIds { get; }
}

public sealed record GeneratedContentPayloadFact(
    string Name,
    string Value);

public sealed class GeneratedDrillContentResult
{
    public GeneratedDrillContentResult(
        GeneratedDrillContentRequest request,
        GeneratedDrillInstanceDescriptor instance,
        IEnumerable<GeneratedContentPayloadFact> payloadFacts)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(payloadFacts);

        if (instance.Branch != request.Branch ||
            instance.Level != request.Level ||
            instance.Drill != request.Drill ||
            instance.ContentKind != request.ContentKind ||
            !string.Equals(instance.EquivalenceClass, request.EquivalenceClass, StringComparison.Ordinal) ||
            instance.RetestFreshnessPolicy != request.FreshnessPolicy)
        {
            throw new ArgumentException(
                "Generated content result identity must match the request demand.",
                nameof(instance));
        }

        if (!instance.LoadVariables.SequenceEqual(request.LoadVariables))
        {
            throw new ArgumentException(
                "Generated content result load variables must match the request.",
                nameof(instance));
        }

        if (!instance.CriticalConstraints.SequenceEqual(request.CriticalConstraints))
        {
            throw new ArgumentException(
                "Generated content result critical constraints must match the request.",
                nameof(instance));
        }

        var payloadFactArray = payloadFacts.ToArray();
        if (payloadFactArray.Length == 0)
        {
            throw new ArgumentException(
                "Generated content results must expose payload facts for runtime execution and persistence.",
                nameof(payloadFacts));
        }

        if (payloadFactArray.Any(fact =>
                string.IsNullOrWhiteSpace(fact.Name) ||
                string.IsNullOrWhiteSpace(fact.Value)))
        {
            throw new ArgumentException(
                "Generated content payload facts must include a name and value.",
                nameof(payloadFacts));
        }

        Request = request;
        Instance = instance;
        PayloadFacts = Array.AsReadOnly(payloadFactArray);
    }

    public GeneratedDrillContentRequest Request { get; }

    public GeneratedDrillInstanceDescriptor Instance { get; }

    public GeneratedDrillInstanceIdentityMetadata IdentityMetadata => Instance.IdentityMetadata;

    public IReadOnlyList<GeneratedContentPayloadFact> PayloadFacts { get; }

    public string InstanceId => Instance.InstanceId;

    public string ContentId => Instance.ContentIdentity.ContentId;

    public string ContentVersion => Instance.ContentVersion;

    public BranchCode Branch => Request.Branch;

    public GlobalLevelId Level => Request.Level;

    public DrillId Drill => Request.Drill;

    public SessionType SessionType => Request.SessionType;

    public PromptContentKind ContentKind => Request.ContentKind;

    public string EquivalenceClass => Request.EquivalenceClass;

    public string LoadContextFingerprint => Instance.IdentityMetadata.LoadContextFingerprint;

    public bool CanBeConsumedByRuntime => true;

    public bool CanBeRecordedByPersistence => true;
}
