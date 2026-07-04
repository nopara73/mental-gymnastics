using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public enum LocalContentBankSourceKind
{
    PackagedWithApp,
    GeneratedLocally,
}

public sealed class LocalContentBankEntry
{
    public LocalContentBankEntry(
        string bankId,
        string entryId,
        PromptContentIdentity contentIdentity,
        string contentVersion,
        LocalContentBankSourceKind sourceKind,
        IEnumerable<LoadVariable> loadVariables,
        IEnumerable<CriticalConstraint> criticalConstraints,
        IEnumerable<GeneratedContentPayloadFact> payloadFacts,
        IEnumerable<GeneratedContentMaterial> materials)
    {
        if (string.IsNullOrWhiteSpace(bankId))
        {
            throw new ArgumentException("Local content bank entries must name the source bank.", nameof(bankId));
        }

        if (string.IsNullOrWhiteSpace(entryId))
        {
            throw new ArgumentException("Local content bank entries must have a stable entry id.", nameof(entryId));
        }

        ArgumentNullException.ThrowIfNull(contentIdentity);

        if (string.IsNullOrWhiteSpace(contentVersion))
        {
            throw new ArgumentException("Local content bank entries must expose a content version.", nameof(contentVersion));
        }

        GeneratedContentValidation.EnsureDefined(contentIdentity.Branch, nameof(contentIdentity));
        GeneratedContentValidation.EnsureDefined(contentIdentity.Level, nameof(contentIdentity));
        GeneratedContentValidation.EnsureDefined(contentIdentity.Drill, nameof(contentIdentity));
        GeneratedContentValidation.EnsureDefined(contentIdentity.Kind, nameof(contentIdentity));
        GeneratedContentValidation.EnsureDefined(sourceKind, nameof(sourceKind));

        ArgumentNullException.ThrowIfNull(payloadFacts);
        ArgumentNullException.ThrowIfNull(materials);

        var payloadFactArray = payloadFacts.ToArray();
        if (payloadFactArray.Length == 0 ||
            payloadFactArray.Any(fact =>
                string.IsNullOrWhiteSpace(fact.Name) ||
                string.IsNullOrWhiteSpace(fact.Value)))
        {
            throw new ArgumentException(
                "Local content bank entries must expose payload facts for runtime execution.",
                nameof(payloadFacts));
        }

        var materialArray = materials.ToArray();
        if (materialArray.Length == 0 || materialArray.Any(material => material is null))
        {
            throw new ArgumentException(
                "Local content bank entries must expose concrete generated content materials.",
                nameof(materials));
        }

        BankId = bankId;
        EntryId = entryId;
        ContentIdentity = contentIdentity;
        ContentVersion = contentVersion;
        SourceKind = sourceKind;
        LoadVariables = GeneratedContentValidation.RequireLoadVariables(
            loadVariables,
            "Local content bank entries must expose the load variables they satisfy.",
            "Local content bank load variables must include a name and value.",
            nameof(loadVariables));
        CriticalConstraints = GeneratedContentValidation.RequireCriticalConstraints(
            criticalConstraints,
            "Local content bank entries must preserve critical or honesty constraints.",
            "Local content bank critical constraints must include descriptions.",
            nameof(criticalConstraints));
        PayloadFacts = Array.AsReadOnly(payloadFactArray);
        Materials = Array.AsReadOnly(materialArray);
    }

    public string BankId { get; }

    public string EntryId { get; }

    public PromptContentIdentity ContentIdentity { get; }

    public string ContentVersion { get; }

    public LocalContentBankSourceKind SourceKind { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public IReadOnlyList<CriticalConstraint> CriticalConstraints { get; }

    public IReadOnlyList<GeneratedContentPayloadFact> PayloadFacts { get; }

    public IReadOnlyList<GeneratedContentMaterial> Materials { get; }

    public string ContentId => ContentIdentity.ContentId;

    public BranchCode Branch => ContentIdentity.Branch;

    public GlobalLevelId Level => ContentIdentity.Level;

    public DrillId Drill => ContentIdentity.Drill;

    public PromptContentKind ContentKind => ContentIdentity.Kind;

    public string EquivalenceClass => ContentIdentity.EquivalenceClass;

    public bool CanBeUsedOffline => true;

    public bool RequiresNetworkAccess => false;

    public bool AllowsAiOrApiDependencies => false;

    public bool OwnsProgressionDecision => false;

    internal GeneratedDrillContentResult ToGeneratedContentResult(GeneratedDrillContentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var descriptor = new GeneratedDrillInstanceDescriptor(
            $"{BankId}:{EntryId}",
            ContentIdentity,
            ContentVersion,
            request.FreshnessPolicy,
            request.LoadVariables,
            request.CriticalConstraints);

        return new GeneratedDrillContentResult(
            request,
            descriptor,
            PayloadFacts);
    }
}

public sealed class LocalContentBank
{
    public LocalContentBank(
        string bankId,
        string bankVersion,
        LocalContentBankSourceKind sourceKind,
        IEnumerable<LocalContentBankEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(bankId))
        {
            throw new ArgumentException("Local content banks must have a stable bank id.", nameof(bankId));
        }

        if (string.IsNullOrWhiteSpace(bankVersion))
        {
            throw new ArgumentException("Local content banks must expose a bank version.", nameof(bankVersion));
        }

        GeneratedContentValidation.EnsureDefined(sourceKind, nameof(sourceKind));
        ArgumentNullException.ThrowIfNull(entries);

        var entryArray = entries.ToArray();
        if (entryArray.Length == 0 || entryArray.Any(entry => entry is null))
        {
            throw new ArgumentException(
                "Local content banks must contain at least one local entry.",
                nameof(entries));
        }

        if (entryArray.Any(entry => !string.Equals(entry.BankId, bankId, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Every local content bank entry must belong to the same bank id.",
                nameof(entries));
        }

        if (entryArray.Any(entry => entry.SourceKind != sourceKind))
        {
            throw new ArgumentException(
                "Every local content bank entry must use the same local source kind as the bank.",
                nameof(entries));
        }

        BankId = bankId;
        BankVersion = bankVersion;
        SourceKind = sourceKind;
        Entries = Array.AsReadOnly(entryArray);
    }

    public string BankId { get; }

    public string BankVersion { get; }

    public LocalContentBankSourceKind SourceKind { get; }

    public IReadOnlyList<LocalContentBankEntry> Entries { get; }

    public bool CanBeUsedOffline => true;

    public bool RequiresNetworkAccess => false;

    public bool AllowsAiOrApiDependencies => false;

    public bool OwnsProgressionDecision => false;
}

public sealed class LocalContentBankSelectionRequest
{
    public LocalContentBankSelectionRequest(
        GeneratedDrillContentRequest request,
        string? requiredContentVersion = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (requiredContentVersion is not null && string.IsNullOrWhiteSpace(requiredContentVersion))
        {
            throw new ArgumentException(
                "Required local content version must be non-empty when supplied.",
                nameof(requiredContentVersion));
        }

        Request = request;
        RequiredContentVersion = requiredContentVersion;
    }

    public GeneratedDrillContentRequest Request { get; }

    public string? RequiredContentVersion { get; }
}

public enum LocalContentBankSelectionFailureKind
{
    NoMatchingLocalContent,
    FreshEquivalentContentUnavailable,
    BranchMismatch,
    LevelMismatch,
    DrillMismatch,
    ContentKindMismatch,
    EquivalenceClassMismatch,
    VersionMismatch,
    LoadVariablesMismatch,
    CriticalConstraintsMismatch,
    MaterialValidationFailed,
}

public sealed record LocalContentBankSelectionFailure(
    LocalContentBankSelectionFailureKind Kind,
    string Detail);

public sealed class LocalContentBankSelectionResult
{
    public LocalContentBankSelectionResult(
        LocalContentBankEntry? entry,
        ValidatedGeneratedDrillContent? content,
        IEnumerable<LocalContentBankSelectionFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        Entry = entry;
        Content = content;
        Failures = Array.AsReadOnly(failures.ToArray());
    }

    public LocalContentBankEntry? Entry { get; }

    public ValidatedGeneratedDrillContent? Content { get; }

    public IReadOnlyList<LocalContentBankSelectionFailure> Failures { get; }

    public bool HasSelection => Entry is not null && Content is not null;

    public bool CanUseContent => HasSelection;

    public bool GrantsAdvancement => false;

    public bool OwnsProgressionDecision => false;
}

public static class LocalContentBankSelector
{
    public static LocalContentBankSelectionResult Select(
        LocalContentBank bank,
        LocalContentBankSelectionRequest selectionRequest)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(selectionRequest);

        var request = selectionRequest.Request;
        var failures = new List<LocalContentBankSelectionFailure>();
        var usedContentIds = request.PreviouslyUsedContentIds.ToHashSet(StringComparer.Ordinal);

        foreach (var entry in bank.Entries
            .OrderBy(entry => entry.ContentId, StringComparer.Ordinal)
            .ThenBy(entry => entry.EntryId, StringComparer.Ordinal))
        {
            if (!MatchesDemand(entry, request, failures))
            {
                continue;
            }

            if (!MatchesRequiredVersion(entry, selectionRequest.RequiredContentVersion, failures))
            {
                continue;
            }

            if (!MatchesLoadVariables(entry.LoadVariables, request.LoadVariables))
            {
                failures.Add(new LocalContentBankSelectionFailure(
                    LocalContentBankSelectionFailureKind.LoadVariablesMismatch,
                    $"Local content entry {entry.EntryId} does not match requested load variables."));
                continue;
            }

            if (!MatchesCriticalConstraints(entry.CriticalConstraints, request.CriticalConstraints))
            {
                failures.Add(new LocalContentBankSelectionFailure(
                    LocalContentBankSelectionFailureKind.CriticalConstraintsMismatch,
                    $"Local content entry {entry.EntryId} does not preserve requested critical constraints."));
                continue;
            }

            if (RequiresFreshContent(request) && usedContentIds.Contains(entry.ContentId))
            {
                failures.Add(new LocalContentBankSelectionFailure(
                    LocalContentBankSelectionFailureKind.FreshEquivalentContentUnavailable,
                    $"Local content entry {entry.EntryId} was already used and fresh equivalent content is required."));
                continue;
            }

            var result = entry.ToGeneratedContentResult(request);
            try
            {
                var validated = ValidatedGeneratedDrillContent.Create(result, entry.Materials);
                return new LocalContentBankSelectionResult(entry, validated, []);
            }
            catch (InvalidOperationException exception)
            {
                failures.Add(new LocalContentBankSelectionFailure(
                    LocalContentBankSelectionFailureKind.MaterialValidationFailed,
                    exception.Message));
            }
        }

        if (failures.Count == 0)
        {
            failures.Add(new LocalContentBankSelectionFailure(
                LocalContentBankSelectionFailureKind.NoMatchingLocalContent,
                "No local content bank entry matched the requested generated content demand."));
        }

        return new LocalContentBankSelectionResult(
            entry: null,
            content: null,
            failures);
    }

    private static bool MatchesDemand(
        LocalContentBankEntry entry,
        GeneratedDrillContentRequest request,
        ICollection<LocalContentBankSelectionFailure> failures)
    {
        if (entry.Branch != request.Branch)
        {
            failures.Add(new LocalContentBankSelectionFailure(
                LocalContentBankSelectionFailureKind.BranchMismatch,
                $"Local content entry {entry.EntryId} is for {entry.Branch}, not {request.Branch}."));
            return false;
        }

        if (entry.Level != request.Level)
        {
            failures.Add(new LocalContentBankSelectionFailure(
                LocalContentBankSelectionFailureKind.LevelMismatch,
                $"Local content entry {entry.EntryId} is for {entry.Level}, not {request.Level}."));
            return false;
        }

        if (entry.Drill != request.Drill)
        {
            failures.Add(new LocalContentBankSelectionFailure(
                LocalContentBankSelectionFailureKind.DrillMismatch,
                $"Local content entry {entry.EntryId} is for {entry.Drill}, not {request.Drill}."));
            return false;
        }

        if (entry.ContentKind != request.ContentKind)
        {
            failures.Add(new LocalContentBankSelectionFailure(
                LocalContentBankSelectionFailureKind.ContentKindMismatch,
                $"Local content entry {entry.EntryId} is {entry.ContentKind}, not {request.ContentKind}."));
            return false;
        }

        if (!string.Equals(entry.EquivalenceClass, request.EquivalenceClass, StringComparison.Ordinal))
        {
            failures.Add(new LocalContentBankSelectionFailure(
                LocalContentBankSelectionFailureKind.EquivalenceClassMismatch,
                $"Local content entry {entry.EntryId} is in equivalence class {entry.EquivalenceClass}, not {request.EquivalenceClass}."));
            return false;
        }

        return true;
    }

    private static bool MatchesRequiredVersion(
        LocalContentBankEntry entry,
        string? requiredContentVersion,
        ICollection<LocalContentBankSelectionFailure> failures)
    {
        if (requiredContentVersion is null ||
            string.Equals(entry.ContentVersion, requiredContentVersion, StringComparison.Ordinal))
        {
            return true;
        }

        failures.Add(new LocalContentBankSelectionFailure(
            LocalContentBankSelectionFailureKind.VersionMismatch,
            $"Local content entry {entry.EntryId} is version {entry.ContentVersion}, not {requiredContentVersion}."));
        return false;
    }

    private static bool RequiresFreshContent(GeneratedDrillContentRequest request)
    {
        return request.FreshnessPolicy == PromptFreshnessPolicy.FreshEquivalentRequired;
    }

    private static bool MatchesLoadVariables(
        IEnumerable<LoadVariable> left,
        IEnumerable<LoadVariable> right)
    {
        return left
            .OrderBy(variable => variable.Name, StringComparer.Ordinal)
            .ThenBy(variable => variable.Value, StringComparer.Ordinal)
            .Select(variable => (variable.Name, variable.Value))
            .SequenceEqual(
                right
                    .OrderBy(variable => variable.Name, StringComparer.Ordinal)
                    .ThenBy(variable => variable.Value, StringComparer.Ordinal)
                    .Select(variable => (variable.Name, variable.Value)));
    }

    private static bool MatchesCriticalConstraints(
        IEnumerable<CriticalConstraint> left,
        IEnumerable<CriticalConstraint> right)
    {
        return left
            .OrderBy(constraint => constraint.Description, StringComparer.Ordinal)
            .Select(constraint => constraint.Description)
            .SequenceEqual(
                right
                    .OrderBy(constraint => constraint.Description, StringComparer.Ordinal)
                    .Select(constraint => constraint.Description));
    }
}
