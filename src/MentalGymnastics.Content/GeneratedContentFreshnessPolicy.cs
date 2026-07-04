using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class GeneratedContentEquivalenceRequirement
{
    public GeneratedContentEquivalenceRequirement(
        GeneratedDrillContentRequest request,
        string visibleStandard,
        string loadIntent)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(visibleStandard))
        {
            throw new ArgumentException("Generated content equivalence requires a visible standard.", nameof(visibleStandard));
        }

        if (string.IsNullOrWhiteSpace(loadIntent))
        {
            throw new ArgumentException("Generated content equivalence requires a load intent.", nameof(loadIntent));
        }

        Request = request;
        VisibleStandard = visibleStandard;
        LoadIntent = loadIntent;
    }

    public GeneratedDrillContentRequest Request { get; }

    public string VisibleStandard { get; }

    public string LoadIntent { get; }
}

public sealed class GeneratedContentEquivalenceCandidate
{
    public GeneratedContentEquivalenceCandidate(
        GeneratedDrillInstanceDescriptor instance,
        string visibleStandard,
        string loadIntent)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (string.IsNullOrWhiteSpace(visibleStandard))
        {
            throw new ArgumentException("Generated content candidates must preserve the visible standard.", nameof(visibleStandard));
        }

        if (string.IsNullOrWhiteSpace(loadIntent))
        {
            throw new ArgumentException("Generated content candidates must preserve the load intent.", nameof(loadIntent));
        }

        Instance = instance;
        VisibleStandard = visibleStandard;
        LoadIntent = loadIntent;
    }

    public GeneratedDrillInstanceDescriptor Instance { get; }

    public string VisibleStandard { get; }

    public string LoadIntent { get; }
}

public enum GeneratedContentPolicyFailureKind
{
    FreshEquivalentContentAlreadyUsed,
    BranchDemandChanged,
    StandardVisibilityChanged,
    LoadIntentChanged,
    LoadVariablesChanged,
    CriticalConstraintsChanged,
}

public sealed record GeneratedContentPolicyFailure(
    GeneratedContentPolicyFailureKind Kind,
    string Detail);

public sealed class GeneratedContentFreshnessPolicyResult
{
    public GeneratedContentFreshnessPolicyResult(
        bool requiresFreshContent,
        bool isFreshContent,
        bool isEquivalent,
        bool meetsFreshnessPolicy,
        IEnumerable<GeneratedContentPolicyFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        RequiresFreshContent = requiresFreshContent;
        IsFreshContent = isFreshContent;
        IsEquivalent = isEquivalent;
        MeetsFreshnessPolicy = meetsFreshnessPolicy;
        Failures = Array.AsReadOnly(failures.ToArray());
    }

    public bool RequiresFreshContent { get; }

    public bool IsFreshContent { get; }

    public bool IsEquivalent { get; }

    public bool MeetsFreshnessPolicy { get; }

    public bool CanUseContent => IsEquivalent && MeetsFreshnessPolicy;

    public bool GrantsAdvancement => false;

    public IReadOnlyList<GeneratedContentPolicyFailure> Failures { get; }
}

public static class GeneratedContentFreshnessPolicy
{
    public static GeneratedContentFreshnessPolicyResult Evaluate(
        GeneratedContentEquivalenceRequirement requirement,
        GeneratedContentEquivalenceCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(candidate);

        var failures = new List<GeneratedContentPolicyFailure>();
        var request = requirement.Request;
        var instance = candidate.Instance;
        var usedContentIds = request.PreviouslyUsedContentIds.ToHashSet(StringComparer.Ordinal);
        var requiresFreshContent =
            request.FreshnessPolicy == PromptFreshnessPolicy.FreshEquivalentRequired ||
            instance.RetestFreshnessPolicy == PromptFreshnessPolicy.FreshEquivalentRequired;
        var isFreshContent = !usedContentIds.Contains(instance.ContentIdentity.ContentId);

        if (requiresFreshContent && !isFreshContent)
        {
            failures.Add(new GeneratedContentPolicyFailure(
                GeneratedContentPolicyFailureKind.FreshEquivalentContentAlreadyUsed,
                "Fresh equivalent content is required, so a previously used content id cannot be reused."));
        }

        if (DemandChanged(request, instance))
        {
            failures.Add(new GeneratedContentPolicyFailure(
                GeneratedContentPolicyFailureKind.BranchDemandChanged,
                "Equivalent content must preserve branch, level, drill, content kind, and equivalence class."));
        }

        if (!SameText(requirement.VisibleStandard, candidate.VisibleStandard))
        {
            failures.Add(new GeneratedContentPolicyFailure(
                GeneratedContentPolicyFailureKind.StandardVisibilityChanged,
                "Equivalent content must keep the same branch standard visible."));
        }

        if (!SameText(requirement.LoadIntent, candidate.LoadIntent))
        {
            failures.Add(new GeneratedContentPolicyFailure(
                GeneratedContentPolicyFailureKind.LoadIntentChanged,
                "Equivalent content must preserve the same load intent."));
        }

        if (!SameLoadVariables(request.LoadVariables, instance.LoadVariables))
        {
            failures.Add(new GeneratedContentPolicyFailure(
                GeneratedContentPolicyFailureKind.LoadVariablesChanged,
                "Equivalent content must preserve the requested load variables."));
        }

        if (!SameCriticalConstraints(request.CriticalConstraints, instance.CriticalConstraints))
        {
            failures.Add(new GeneratedContentPolicyFailure(
                GeneratedContentPolicyFailureKind.CriticalConstraintsChanged,
                "Equivalent content must preserve the critical and honesty constraints."));
        }

        var meetsFreshnessPolicy = !failures.Any(
            failure => failure.Kind == GeneratedContentPolicyFailureKind.FreshEquivalentContentAlreadyUsed);
        var isEquivalent = !failures.Any(failure =>
            failure.Kind != GeneratedContentPolicyFailureKind.FreshEquivalentContentAlreadyUsed);

        return new GeneratedContentFreshnessPolicyResult(
            requiresFreshContent,
            isFreshContent,
            isEquivalent,
            meetsFreshnessPolicy,
            failures);
    }

    private static bool DemandChanged(
        GeneratedDrillContentRequest request,
        GeneratedDrillInstanceDescriptor instance)
    {
        return instance.Branch != request.Branch ||
            instance.Level != request.Level ||
            instance.Drill != request.Drill ||
            instance.ContentKind != request.ContentKind ||
            !string.Equals(instance.EquivalenceClass, request.EquivalenceClass, StringComparison.Ordinal);
    }

    private static bool SameLoadVariables(
        IEnumerable<LoadVariable> expected,
        IEnumerable<LoadVariable> actual)
    {
        return expected
            .OrderBy(variable => variable.Name, StringComparer.Ordinal)
            .ThenBy(variable => variable.Value, StringComparer.Ordinal)
            .Select(variable => (variable.Name, variable.Value))
            .SequenceEqual(
                actual
                    .OrderBy(variable => variable.Name, StringComparer.Ordinal)
                    .ThenBy(variable => variable.Value, StringComparer.Ordinal)
                    .Select(variable => (variable.Name, variable.Value)));
    }

    private static bool SameCriticalConstraints(
        IEnumerable<CriticalConstraint> expected,
        IEnumerable<CriticalConstraint> actual)
    {
        return expected
            .OrderBy(constraint => constraint.Description, StringComparer.Ordinal)
            .Select(constraint => constraint.Description)
            .SequenceEqual(
                actual
                    .OrderBy(constraint => constraint.Description, StringComparer.Ordinal)
                    .Select(constraint => constraint.Description));
    }

    private static bool SameText(string expected, string actual)
    {
        return string.Equals(expected.Trim(), actual.Trim(), StringComparison.Ordinal);
    }
}
