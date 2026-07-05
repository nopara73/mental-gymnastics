using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public static class MvpLocalContentBank
{
    public const string BankId = "mental-gymnastics-mvp-local-bank";
    public const string BankVersion = GeneratedContentSeedPlan.AlgorithmVersion;

    public static LocalContentBank Create()
    {
        return new LocalContentBank(
            BankId,
            BankVersion,
            LocalContentBankSourceKind.PackagedWithApp,
            CreateEntries());
    }

    private static IEnumerable<LocalContentBankEntry> CreateEntries()
    {
        yield return CreateWorkingMemoryEntry("wm1-delayed-reconstruction-a", "mvp-wm1-alpha");
        yield return CreateWorkingMemoryEntry("wm1-delayed-reconstruction-b", "mvp-wm1-bravo");
        yield return CreateWorkingMemoryEntry("wm1-delayed-reconstruction-c", "mvp-wm1-charlie");

        yield return CreateDiscriminationEntry("de1-pair-discrimination-a", "mvp-de1-alpha");
        yield return CreateDiscriminationEntry("de1-pair-discrimination-b", "mvp-de1-bravo");
        yield return CreateDiscriminationEntry("de1-pair-discrimination-c", "mvp-de1-charlie");

        yield return CreateConceptOperationsEntry("co1-rule-extraction-a", "mvp-co1-alpha");
        yield return CreateConceptOperationsEntry("co1-rule-extraction-b", "mvp-co1-bravo");
        yield return CreateConceptOperationsEntry("co1-rule-extraction-c", "mvp-co1-charlie");
    }

    private static LocalContentBankEntry CreateWorkingMemoryEntry(
        string entryId,
        string seed)
    {
        var generated = WorkingMemoryGeneratedContentGenerator.Generate(
            CreateDelayedReconstructionRequest(),
            new GeneratedContentSeed(seed));

        return CreateEntry(entryId, generated.Result, generated.Materials);
    }

    private static LocalContentBankEntry CreateDiscriminationEntry(
        string entryId,
        string seed)
    {
        var generated = DiscriminationGeneratedContentGenerator.Generate(
            CreatePairDiscriminationRequest(),
            new GeneratedContentSeed(seed));

        return CreateEntry(entryId, generated.Result, generated.Materials);
    }

    private static LocalContentBankEntry CreateConceptOperationsEntry(
        string entryId,
        string seed)
    {
        var generated = ConceptOperationsGeneratedContentGenerator.Generate(
            CreateRuleExtractionRequest(),
            new GeneratedContentSeed(seed));

        return CreateEntry(entryId, generated.Result, generated.Materials);
    }

    private static LocalContentBankEntry CreateEntry(
        string entryId,
        GeneratedDrillContentResult result,
        IReadOnlyList<GeneratedContentMaterial> materials)
    {
        return new LocalContentBankEntry(
            BankId,
            entryId,
            result.Instance.ContentIdentity,
            result.ContentVersion,
            LocalContentBankSourceKind.PackagedWithApp,
            result.Request.LoadVariables,
            result.Request.CriticalConstraints,
            result.PayloadFacts,
            materials);
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
