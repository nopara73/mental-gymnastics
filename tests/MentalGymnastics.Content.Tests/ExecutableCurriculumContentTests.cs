using MentalGymnastics.App;
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

    [Fact]
    public void EveryDrillFamilyProvidesEnoughFreshMaterialForThePerfectPathDemand()
    {
        var failures = new List<string>();
        foreach (var profile in TrainingLoadProfileCatalog.Profiles
                     .GroupBy(item => item.Drill)
                     .Select(group => group.First()))
        {
            var protocol = DrillProtocolCatalog.StandardDrills.Single(item => item.Id == profile.Drill);
            var requiredSessions = ProgramDurationForecastCatalog
                .FirstInstallPerfectPathSessionsByDrill[profile.Drill];
            var usedContentIds = new List<string>(requiredSessions);
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < requiredSessions; index++)
            {
                var request = new GeneratedDrillContentRequest(
                    profile.Branch,
                    profile.Level,
                    profile.Drill,
                    SessionType.Test,
                    ContentKindFor(profile.Drill),
                    $"offline-diversity-{profile.Drill}",
                    PromptFreshnessPolicy.FreshEquivalentRequired,
                    profile.TargetStage.LoadVariables,
                    protocol.HonestyConstraints.Select(item => new CriticalConstraint(item.Description)),
                    usedContentIds);
                var selection = GeneratedContentSelectionCoordinator.Select(
                    GeneratedContentSelectionNeed.ForStandardContent(request),
                    new GeneratedContentSeed($"offline-diversity-{profile.Drill}-{index}"));
                if (!selection.IsValid)
                {
                    failures.Add($"{profile.Drill}: session {index + 1} was not valid");
                    break;
                }

                usedContentIds.Add(selection.Result.ContentId);
                signatures.Add(string.Join(
                    Environment.NewLine,
                    selection.Materials
                        .OrderBy(material => material.Kind)
                        .ThenBy(material => material.Name, StringComparer.Ordinal)
                        .Select(material => $"{material.Kind}|{material.Value}")));
            }

            if (signatures.Count < requiredSessions)
            {
                failures.Add($"{profile.Drill}: {signatures.Count}/{requiredSessions} distinct sessions");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
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
