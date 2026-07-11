using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class ExecutableCurriculumContentTests
{
    public static IEnumerable<object[]> AllStandards()
    {
        return TrainingLoadProfileCatalog.Profiles.Select(profile => new object[]
        {
            profile.Branch,
            profile.Level,
        });
    }

    [Theory]
    [MemberData(nameof(AllStandards))]
    public void GeneratesRuntimeConsumableMaterialAtTheFullStandardLoad(
        BranchCode branch,
        GlobalLevelId level)
    {
        var profile = TrainingLoadProfileCatalog.Get(branch, level);
        var protocol = DrillProtocolCatalog.StandardDrills.Single(item => item.Id == profile.Drill);
        var request = new GeneratedDrillContentRequest(
            branch,
            level,
            profile.Drill,
            SessionType.Test,
            ContentKindFor(profile.Drill),
            $"{branch.ToString().ToLowerInvariant()}-{level.ToString().ToLowerInvariant()}-full-standard",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            profile.TargetStage.LoadVariables,
            protocol.HonestyConstraints.Select(item => new CriticalConstraint(item.Description)));

        var selection = GeneratedContentSelectionCoordinator.Select(
            GeneratedContentSelectionNeed.ForStandardContent(request),
            new GeneratedContentSeed($"full-standard-{branch}-{level}"));

        Assert.True(selection.IsValid, FailureSummary(selection));
        Assert.True(selection.CanBeConsumedByRuntime);
        Assert.True(selection.CanBeRecordedByPersistence);
    }

    [Theory]
    [InlineData(BranchCode.FH)]
    [InlineData(BranchCode.FS)]
    [InlineData(BranchCode.WM)]
    [InlineData(BranchCode.IR)]
    [InlineData(BranchCode.DE)]
    [InlineData(BranchCode.CO)]
    [InlineData(BranchCode.AI)]
    [InlineData(BranchCode.TI)]
    public void TransferAtLevelFourContainsAnExecutableFreshSourceTask(BranchCode branch)
    {
        var profile = TrainingLoadProfileCatalog.Get(branch, GlobalLevelId.L4);
        var protocol = DrillProtocolCatalog.StandardDrills.Single(item => item.Id == profile.Drill);
        var request = new GeneratedDrillContentRequest(
            branch,
            GlobalLevelId.L4,
            profile.Drill,
            SessionType.Transfer,
            ContentKindFor(profile.Drill),
            $"{branch.ToString().ToLowerInvariant()}-l4-formal-transfer",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            profile.TargetStage.LoadVariables,
            protocol.HonestyConstraints.Select(item => new CriticalConstraint(item.Description)));
        var capacity = ProgramCatalog.Drills.Single(item => item.Id == profile.Drill)
            .CapacityTrained.First();

        var generated = TransferGeneratedContentGenerator.Generate(
            new TransferContentGenerationRequest(request, capacity, "far transfer"),
            new GeneratedContentSeed($"formal-transfer-{branch}"));
        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            generated.Materials,
            ProgramCatalog.Standards.Single(item =>
                item.Branch == branch && item.Level == GlobalLevelId.L4));

        Assert.True(generated.CanBeConsumedByRuntime);
        Assert.Contains(generated.Materials, item => item.Kind == GeneratedContentMaterialKind.TransferTask);
        Assert.Contains(generated.Materials, item => item.Kind == GeneratedContentMaterialKind.SourceBranchStandard);
        Assert.True(package.CanBeConsumedByRuntime);
        Assert.Contains(package.Phases, phase => phase.Kind is not GeneratedRuntimePhaseKind.InstructionPrep);
    }

    private static PromptContentKind ContentKindFor(DrillId drill)
    {
        return drill switch
        {
            DrillId.FH1TargetHold or DrillId.AI1PressureRepeat or
                DrillId.AI2DisruptionRecovery or DrillId.TI1CompositeTask or
                DrillId.TI2GlobalReviewTask => PromptContentKind.EquivalentPrompt,
            DrillId.FH2DistractorHold or DrillId.FS1CueSwitch or
                DrillId.FS2InvalidCueFilter or DrillId.IR1GoNoGoRule or
                DrillId.IR2ExceptionRule => PromptContentKind.CueSequence,
            DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform =>
                PromptContentKind.DelayedReconstructionTask,
            DrillId.DE1PairDiscrimination or DrillId.DE2SeededAudit =>
                PromptContentKind.DiscriminationItemSet,
            DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping =>
                PromptContentKind.RuleExampleSet,
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unknown drill."),
        };
    }

    private static string FailureSummary(GeneratedContentSelectionResult selection)
    {
        return string.Join(
            "; ",
            selection.FreshnessValidation.Failures.Select(item => item.Detail)
                .Concat(selection.AntiSelfDeceptionGuard.Findings.Select(item => item.Detail))
                .Concat(selection.DifficultyAudit?.Failures.Select(item => item.Detail) ?? []));
    }
}
