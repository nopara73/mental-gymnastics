using System.Globalization;
using System.Text;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public sealed class GeneratedContentSeed
{
    public GeneratedContentSeed(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Generated content seed must be provided.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}

public sealed class GeneratedContentSeedPlan
{
    public const string AlgorithmVersion = "deterministic-seed-v3";

    internal GeneratedContentSeedPlan(
        GeneratedDrillContentRequest request,
        int variantIndex,
        string requestFingerprint,
        string payloadSeed,
        GeneratedDrillInstanceDescriptor instance)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(variantIndex);

        if (string.IsNullOrWhiteSpace(requestFingerprint))
        {
            throw new ArgumentException("Request fingerprint is required.", nameof(requestFingerprint));
        }

        if (string.IsNullOrWhiteSpace(payloadSeed))
        {
            throw new ArgumentException("Payload seed is required.", nameof(payloadSeed));
        }

        ArgumentNullException.ThrowIfNull(instance);

        Request = request;
        VariantIndex = variantIndex;
        FreshnessOrdinal = request.PreviouslyUsedContentIds.Count + variantIndex;
        RequestFingerprint = requestFingerprint;
        PayloadSeed = payloadSeed;
        Instance = instance;
        PayloadFacts = Array.AsReadOnly(
            [
                new GeneratedContentPayloadFact("algorithm-version", AlgorithmVersion),
                new GeneratedContentPayloadFact("request-fingerprint", requestFingerprint),
                new GeneratedContentPayloadFact("variant-index", variantIndex.ToString(CultureInfo.InvariantCulture)),
                new GeneratedContentPayloadFact("freshness-ordinal", FreshnessOrdinal.ToString(CultureInfo.InvariantCulture)),
                new GeneratedContentPayloadFact("payload-seed", payloadSeed),
            ]);
    }

    public GeneratedDrillContentRequest Request { get; }

    public int VariantIndex { get; }

    public int FreshnessOrdinal { get; }

    public string RequestFingerprint { get; }

    public string PayloadSeed { get; }

    public string ContentVersion => Instance.ContentVersion;

    public GeneratedDrillInstanceDescriptor Instance { get; }

    public GeneratedDrillInstanceIdentityMetadata IdentityMetadata => Instance.IdentityMetadata;

    public IReadOnlyList<GeneratedContentPayloadFact> PayloadFacts { get; }

    public GeneratedDrillContentResult ToGeneratedResult()
    {
        return new GeneratedDrillContentResult(Request, Instance, PayloadFacts);
    }
}

public static class GeneratedContentSeedDeriver
{
    private const int MaxFreshVariantSearch = 10000;

    public static GeneratedContentSeedPlan Derive(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(seed);

        var requestFingerprint = BuildRequestFingerprint(request);
        var usedContentIds = request.PreviouslyUsedContentIds.ToHashSet(StringComparer.Ordinal);
        var requiresFreshContent = request.FreshnessPolicy == PromptFreshnessPolicy.FreshEquivalentRequired;

        for (var variantIndex = 0; variantIndex < MaxFreshVariantSearch; variantIndex++)
        {
            var candidate = BuildPlan(request, seed, requestFingerprint, variantIndex);
            if (!requiresFreshContent || !usedContentIds.Contains(candidate.Instance.ContentIdentity.ContentId))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Unable to derive a fresh equivalent generated content seed within the supported variant range.");
    }

    private static GeneratedContentSeedPlan BuildPlan(
        GeneratedDrillContentRequest request,
        GeneratedContentSeed seed,
        string requestFingerprint,
        int variantIndex)
    {
        var variantMaterial = string.Join(
            "|",
            GeneratedContentSeedPlan.AlgorithmVersion,
            requestFingerprint,
            seed.Value,
            variantIndex.ToString(CultureInfo.InvariantCulture));
        var contentHash = GeneratedContentStableHash.HashSegment("content|" + variantMaterial);
        var instanceHash = GeneratedContentStableHash.HashSegment("instance|" + variantMaterial);
        var payloadSeed = GeneratedContentStableHash.HashSegment("payload|" + variantMaterial);
        var contentId = string.Join(
            "-",
            "generated",
            ToStableIdPart(request.Branch),
            ToStableIdPart(request.Level),
            ToStableIdPart(request.Drill),
            contentHash);
        var instanceId = string.Join(
            "-",
            "generated-instance",
            ToStableIdPart(request.Branch),
            ToStableIdPart(request.Level),
            instanceHash);

        var instance = new GeneratedDrillInstanceDescriptor(
            instanceId,
            new PromptContentIdentity(
                contentId,
                request.Branch,
                request.Level,
                request.Drill,
                request.ContentKind,
                request.EquivalenceClass),
            GeneratedContentSeedPlan.AlgorithmVersion,
            request.FreshnessPolicy,
            request.LoadVariables,
            request.CriticalConstraints);

        return new GeneratedContentSeedPlan(
            request,
            variantIndex,
            requestFingerprint,
            payloadSeed,
            instance);
    }

    private static string BuildRequestFingerprint(GeneratedDrillContentRequest request)
    {
        var builder = new StringBuilder();
        GeneratedContentStableHash.AppendLengthPrefixed(builder, GeneratedContentSeedPlan.AlgorithmVersion);
        GeneratedContentStableHash.AppendLengthPrefixed(builder, request.Branch.ToString());
        GeneratedContentStableHash.AppendLengthPrefixed(builder, request.Level.ToString());
        GeneratedContentStableHash.AppendLengthPrefixed(builder, request.Drill.ToString());
        GeneratedContentStableHash.AppendLengthPrefixed(builder, request.SessionType.ToString());
        GeneratedContentStableHash.AppendLengthPrefixed(builder, request.ContentKind.ToString());
        GeneratedContentStableHash.AppendLengthPrefixed(builder, request.EquivalenceClass);
        GeneratedContentStableHash.AppendLengthPrefixed(builder, request.FreshnessPolicy.ToString());

        foreach (var loadVariable in request.LoadVariables
            .OrderBy(variable => variable.Name, StringComparer.Ordinal)
            .ThenBy(variable => variable.Value, StringComparer.Ordinal))
        {
            GeneratedContentStableHash.AppendLengthPrefixed(builder, "load");
            GeneratedContentStableHash.AppendLengthPrefixed(builder, loadVariable.Name);
            GeneratedContentStableHash.AppendLengthPrefixed(builder, loadVariable.Value);
        }

        foreach (var criticalConstraint in request.CriticalConstraints
            .OrderBy(constraint => constraint.Description, StringComparer.Ordinal))
        {
            GeneratedContentStableHash.AppendLengthPrefixed(builder, "constraint");
            GeneratedContentStableHash.AppendLengthPrefixed(builder, criticalConstraint.Description);
        }

        return GeneratedContentStableHash.HashSegment(builder.ToString());
    }

    private static string ToStableIdPart<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return value.ToString().ToLowerInvariant();
    }
}
