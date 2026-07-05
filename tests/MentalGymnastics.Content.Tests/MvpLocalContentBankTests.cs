using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class MvpLocalContentBankTests
{
    [Fact]
    public void MvpBankIsPackagedOfflineAndContainsFreshEquivalentRetestDepth()
    {
        var bank = MvpLocalContentBank.Create();

        Assert.Equal(MvpLocalContentBank.BankId, bank.BankId);
        Assert.Equal(MvpLocalContentBank.BankVersion, bank.BankVersion);
        Assert.Equal(LocalContentBankSourceKind.PackagedWithApp, bank.SourceKind);
        Assert.True(bank.CanBeUsedOffline);
        Assert.False(bank.RequiresNetworkAccess);
        Assert.False(bank.AllowsAiOrApiDependencies);
        Assert.False(bank.OwnsProgressionDecision);
        AssertHasDepth(bank, BranchCode.WM, DrillId.WM1DelayedReconstruction, "wm-l1-delayed-reconstruction");
        AssertHasDepth(bank, BranchCode.DE, DrillId.DE1PairDiscrimination, "de-l1-pair-discrimination");
        AssertHasDepth(bank, BranchCode.CO, DrillId.CO1RuleExtraction, "co-l1-rule-extraction");
    }

    [Theory]
    [MemberData(nameof(FreshEquivalentCases))]
    public void MvpBankSelectsFreshEquivalentVariantsThroughTheLocalBankBoundary(
        GeneratedDrillContentRequest request,
        GeneratedContentMaterialKind expectedAuditKind,
        string expectedPayloadFamily)
    {
        var bank = MvpLocalContentBank.Create();
        var selectedContentIds = new List<string>();
        var selectedMaterialSnapshots = new List<string>();

        for (var variant = 0; variant < 3; variant++)
        {
            var variantRequest = WithPreviouslyUsedContentIds(request, selectedContentIds);
            var selection = LocalContentBankSelector.Select(
                bank,
                new LocalContentBankSelectionRequest(variantRequest, MvpLocalContentBank.BankVersion));

            Assert.True(selection.CanUseContent, FormatFailures(selection.Failures));
            Assert.False(selection.GrantsAdvancement);
            Assert.False(selection.OwnsProgressionDecision);
            Assert.NotNull(selection.Entry);
            Assert.NotNull(selection.Content);
            Assert.DoesNotContain(selection.Entry!.ContentId, selectedContentIds);
            Assert.Equal(MvpLocalContentBank.BankId, selection.Entry.BankId);
            Assert.Equal(MvpLocalContentBank.BankVersion, selection.Entry.ContentVersion);
            Assert.Equal(request.Branch, selection.Content!.Result.Branch);
            Assert.Equal(request.Level, selection.Content.Result.Level);
            Assert.Equal(request.Drill, selection.Content.Result.Drill);
            Assert.Equal(request.ContentKind, selection.Content.Result.ContentKind);
            Assert.Equal(request.EquivalenceClass, selection.Content.Result.EquivalenceClass);
            Assert.Equal(request.LoadVariables, selection.Content.Result.Request.LoadVariables);
            Assert.Equal(request.CriticalConstraints, selection.Content.Result.Request.CriticalConstraints);
            Assert.True(selection.Content.CanBeConsumedByRuntime);
            Assert.False(selection.Content.GrantsAdvancement);
            Assert.True(selection.Content.Result.CanBeRecordedByPersistence);
            Assert.Contains(selection.Content.Materials, material => material.Kind == expectedAuditKind);
            Assert.Contains(selection.Content.Result.PayloadFacts, fact =>
                fact.Name == "payload-family" &&
                fact.Value == expectedPayloadFamily);

            selectedContentIds.Add(selection.Entry.ContentId);
            selectedMaterialSnapshots.Add(MaterialSnapshot(selection.Content.Materials));
        }

        Assert.Equal(3, selectedContentIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, selectedMaterialSnapshots.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(AuditabilityCases))]
    public void MvpBankEntriesPreserveDrillSpecificAuditEvidence(
        GeneratedDrillContentRequest request,
        IReadOnlySet<GeneratedContentMaterialKind> expectedKinds,
        IReadOnlySet<string> expectedPayloadFacts)
    {
        var bank = MvpLocalContentBank.Create();
        var selection = LocalContentBankSelector.Select(
            bank,
            new LocalContentBankSelectionRequest(request, MvpLocalContentBank.BankVersion));

        Assert.True(selection.CanUseContent, FormatFailures(selection.Failures));
        Assert.NotNull(selection.Content);

        var materialKinds = selection.Content!.Materials
            .Select(material => material.Kind)
            .ToHashSet();
        foreach (var expectedKind in expectedKinds)
        {
            Assert.Contains(expectedKind, materialKinds);
        }

        var payloadFactNames = selection.Content.Result.PayloadFacts
            .Select(fact => fact.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var expectedPayloadFact in expectedPayloadFacts)
        {
            Assert.Contains(expectedPayloadFact, payloadFactNames);
        }
    }

    public static IEnumerable<object[]> FreshEquivalentCases()
    {
        yield return
        [
            CreateDelayedReconstructionRequest(),
            GeneratedContentMaterialKind.ExpectedReconstruction,
            "wm-delayed-reconstruction",
        ];
        yield return
        [
            CreatePairDiscriminationRequest(),
            GeneratedContentMaterialKind.FalsePositiveFalseNegativeKey,
            "de-pair-discrimination",
        ];
        yield return
        [
            CreateRuleExtractionRequest(),
            GeneratedContentMaterialKind.ExpectedClassification,
            "co-rule-extraction",
        ];
    }

    public static IEnumerable<object[]> AuditabilityCases()
    {
        yield return
        [
            CreateDelayedReconstructionRequest(),
            new HashSet<GeneratedContentMaterialKind>
            {
                GeneratedContentMaterialKind.EncodeItem,
                GeneratedContentMaterialKind.DelayLength,
                GeneratedContentMaterialKind.ExpectedReconstruction,
                GeneratedContentMaterialKind.HonestyConstraint,
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                "comparison-key",
                "omission-evidence",
                "invention-evidence",
            },
        ];
        yield return
        [
            CreatePairDiscriminationRequest(),
            new HashSet<GeneratedContentMaterialKind>
            {
                GeneratedContentMaterialKind.DiscriminationPair,
                GeneratedContentMaterialKind.MatchTruth,
                GeneratedContentMaterialKind.GuessHandling,
                GeneratedContentMaterialKind.FalsePositiveFalseNegativeKey,
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                "comparison-key",
                "guess-marking-policy",
                "false-positive-evidence",
                "false-negative-evidence",
            },
        ];
        yield return
        [
            CreateRuleExtractionRequest(),
            new HashSet<GeneratedContentMaterialKind>
            {
                GeneratedContentMaterialKind.RuleStatement,
                GeneratedContentMaterialKind.PositiveExample,
                GeneratedContentMaterialKind.NegativeExample,
                GeneratedContentMaterialKind.UnseenExample,
                GeneratedContentMaterialKind.ExpectedClassification,
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                "rule-statement-before-test-evidence",
                "overfitting-evidence",
                "rewrite-after-feedback-evidence",
                "vague-rule-evidence",
            },
        ];
    }

    private static void AssertHasDepth(
        LocalContentBank bank,
        BranchCode branch,
        DrillId drill,
        string equivalenceClass)
    {
        var matchingEntries = bank.Entries
            .Where(entry =>
                entry.Branch == branch &&
                entry.Drill == drill &&
                entry.EquivalenceClass == equivalenceClass)
            .ToArray();

        Assert.True(matchingEntries.Length >= 3);
        Assert.Equal(
            matchingEntries.Length,
            matchingEntries
                .Select(entry => entry.ContentId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(matchingEntries, entry =>
        {
            Assert.Equal(LocalContentBankSourceKind.PackagedWithApp, entry.SourceKind);
            Assert.Equal(MvpLocalContentBank.BankVersion, entry.ContentVersion);
            Assert.True(entry.CanBeUsedOffline);
            Assert.False(entry.RequiresNetworkAccess);
            Assert.False(entry.AllowsAiOrApiDependencies);
            Assert.False(entry.OwnsProgressionDecision);
        });
    }

    private static GeneratedDrillContentRequest WithPreviouslyUsedContentIds(
        GeneratedDrillContentRequest request,
        IEnumerable<string> contentIds)
    {
        return new GeneratedDrillContentRequest(
            request.Branch,
            request.Level,
            request.Drill,
            request.SessionType,
            request.ContentKind,
            request.EquivalenceClass,
            request.FreshnessPolicy,
            request.LoadVariables,
            request.CriticalConstraints,
            contentIds);
    }

    private static string FormatFailures(IEnumerable<LocalContentBankSelectionFailure> failures)
    {
        return string.Join(" | ", failures.Select(failure => $"{failure.Kind}: {failure.Detail}"));
    }

    private static string MaterialSnapshot(IEnumerable<GeneratedContentMaterial> materials)
    {
        return string.Join(
            "\n",
            materials
                .Select(material => $"{material.Kind}:{material.Name}:{material.Value}")
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static GeneratedDrillContentRequest CreateDelayedReconstructionRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.WM,
            GlobalLevelId.L1,
            DrillId.WM1DelayedReconstruction,
            SessionType.Practice,
            PromptContentKind.DelayedReconstructionTask,
            "wm-l1-delayed-reconstruction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("item count", "5"),
                new LoadVariable("detail density", "simple objects"),
                new LoadVariable("delay", "60 seconds"),
            ],
            ProtocolCriticalConstraintsFor(DrillId.WM1DelayedReconstruction));
    }

    private static GeneratedDrillContentRequest CreatePairDiscriminationRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.DE,
            GlobalLevelId.L1,
            DrillId.DE1PairDiscrimination,
            SessionType.Practice,
            PromptContentKind.DiscriminationItemSet,
            "de-l1-pair-discrimination",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("similarity", "near match"),
                new LoadVariable("item quantity", "6"),
                new LoadVariable("time limit", "60 seconds"),
            ],
            ProtocolCriticalConstraintsFor(DrillId.DE1PairDiscrimination));
    }

    private static GeneratedDrillContentRequest CreateRuleExtractionRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.CO,
            GlobalLevelId.L1,
            DrillId.CO1RuleExtraction,
            SessionType.Practice,
            PromptContentKind.RuleExampleSet,
            "co-l1-rule-extraction",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("rule ambiguity", "clear examples"),
                new LoadVariable("example count", "8"),
            ],
            ProtocolCriticalConstraintsFor(DrillId.CO1RuleExtraction));
    }

    private static IReadOnlyList<CriticalConstraint> ProtocolCriticalConstraintsFor(DrillId drill)
    {
        return DrillProtocolCatalog.StandardDrills
            .Single(protocol => protocol.Id == drill)
            .HonestyConstraints
            .Select(constraint => new CriticalConstraint(constraint.Description))
            .ToArray();
    }
}
