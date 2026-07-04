using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentIntegrationPassTests
{
    [Fact]
    public void SelectedStandardContentCarriesGuardThroughRuntimeAndPersistenceBoundaries()
    {
        var selection = GeneratedContentSelectionCoordinator.Select(
            GeneratedContentSelectionNeed.ForStandardContent(CreateInvalidCueFilterRequest()),
            new GeneratedContentSeed("integration-standard-fs"));

        Assert.True(selection.IsValid, FailureSummary(selection));
        Assert.True(selection.AntiSelfDeceptionGuard.IsValid, GuardSummary(selection));
        Assert.True(selection.AntiSelfDeceptionGuard.CanBeConsumedByRuntime);
        Assert.True(selection.AntiSelfDeceptionGuard.CanBeRecordedByPersistence);
        Assert.False(selection.AntiSelfDeceptionGuard.OwnsProgressionDecision);
        Assert.False(selection.AntiSelfDeceptionGuard.GrantsAdvancement);

        var standard = StandardFor(selection.Result);
        var package = GeneratedContentRuntimePackager.Package(
            selection.Result,
            selection.Materials,
            standard);
        var runtimeIdentity = new RuntimeGeneratedDrillInstanceIdentity(
            package.GeneratedInstance.InstanceId,
            package.GeneratedInstance.ContentIdentity,
            package.GeneratedInstance.ContentVersion);
        var runtimeDefinition = new RuntimeSessionDefinition(
            package.SessionType,
            package.Branch,
            package.Level,
            package.Drill,
            package.LoadVariables,
            package.Standard,
            package.CriticalConstraints,
            runtimeIdentity);

        Assert.True(package.CanBeConsumedByRuntime);
        Assert.False(package.RuntimeInventsContent);
        Assert.False(package.GrantsAdvancement);
        Assert.Equal(selection.Result.InstanceId, runtimeDefinition.GeneratedDrillInstance!.InstanceId);
        Assert.Equal(selection.Result.ContentId, runtimeDefinition.GeneratedDrillInstance.ContentIdentity.ContentId);
        Assert.Contains(package.Cues, cue =>
            cue.Kind == GeneratedRuntimeCueKind.InvalidCueFilter &&
            cue.ResponseExpectation == GeneratedRuntimeCueResponseExpectation.NoResponseExpected);
        Assert.Contains(package.ExpectedEvidenceFacts, fact =>
            fact.Value.Contains("invalid", StringComparison.OrdinalIgnoreCase));

        var handoff = GeneratedContentPersistenceHandoffMapper.Create(
            selection.Result,
            selection.Materials,
            TrainingDate.From(2026, 7, 4));
        var persistenceRecord = new LocalGeneratedDrillInstanceRecord(
            handoff.InstanceId,
            handoff.GeneratedOn,
            handoff.Branch,
            handoff.Level,
            handoff.Drill,
            handoff.LoadVariables,
            new LocalGeneratedDrillContentIdentity(handoff.ContentIdentity, handoff.ContentVersion),
            contentSummary: handoff.ContentSummary,
            freshnessPolicy: handoff.FreshnessPolicy,
            auditMaterials: handoff.AuditMaterials.Select(material =>
                new LocalGeneratedDrillAuditMaterial(
                    material.Kind.ToString(),
                    material.Name,
                    material.Value)));

        Assert.True(handoff.CanBeRecordedByPersistence);
        Assert.False(handoff.GrantsAdvancement);
        Assert.True(persistenceRecord.CanBeReused);
        Assert.Equal(selection.Result.ContentId, persistenceRecord.ContentIdentity.ContentId);
        Assert.Contains(persistenceRecord.AuditMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.InvalidCue.ToString());
    }

    [Fact]
    public void SelectedTransferContentCarriesTransferGuardThroughRuntimeAndPersistenceBoundaries()
    {
        var selection = GeneratedContentSelectionCoordinator.Select(
            GeneratedContentSelectionNeed.ForTransferContent(CreateTransferRequest()),
            new GeneratedContentSeed("integration-transfer-wm"));

        Assert.True(selection.IsValid, FailureSummary(selection));
        Assert.True(selection.IsTransfer);
        Assert.True(selection.AntiSelfDeceptionGuard.IsValid, GuardSummary(selection));
        Assert.True(selection.AntiSelfDeceptionGuard.CanBeConsumedByRuntime);
        Assert.True(selection.AntiSelfDeceptionGuard.CanBeRecordedByPersistence);
        Assert.Null(selection.DifficultyAudit);
        Assert.NotNull(selection.TransferEligibilityRequest);
        Assert.False(selection.OwnsProgressionDecision);
        Assert.False(selection.GrantsAdvancement);

        var coreEligibility = TransferEligibilityEvaluator.Evaluate(selection.TransferEligibilityRequest!);
        Assert.True(
            coreEligibility.IsEligible,
            string.Join("; ", coreEligibility.Failures.Select(failure => failure.Detail)));

        var package = GeneratedContentRuntimePackager.Package(
            selection.Result,
            selection.Materials,
            StandardFor(selection.Result));

        Assert.True(package.CanBeConsumedByRuntime);
        Assert.Equal(SessionType.Transfer, package.SessionType);
        Assert.False(package.RuntimeInventsContent);
        Assert.False(package.GrantsAdvancement);
        Assert.Contains(package.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.SourceBranchStandard);
        Assert.Contains(package.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.SameDemand);
        Assert.Contains(package.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.ChangedContext);
        Assert.Contains(package.ExpectedEvidenceFacts, fact =>
            fact.Name == "source-standard-visibility");
        Assert.Contains(package.ExpectedEvidenceFacts, fact =>
            fact.Name == "novelty-policy" &&
            fact.Value.Contains("novelty alone is not transfer", StringComparison.OrdinalIgnoreCase));

        var handoff = GeneratedContentPersistenceHandoffMapper.Create(
            selection.Result,
            selection.Materials,
            TrainingDate.From(2026, 7, 4));

        Assert.True(handoff.CanBeRecordedByPersistence);
        Assert.False(handoff.GrantsAdvancement);
        Assert.Equal(selection.Result.InstanceId, handoff.InstanceId);
        Assert.Equal(selection.Result.ContentId, handoff.ContentIdentity.ContentId);
        Assert.Contains("novelty-policy", handoff.ContentSummary, StringComparison.Ordinal);
        Assert.Contains(handoff.AuditMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.SourceBranchStandard);
        Assert.Contains(handoff.AuditMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.RetestRequirement);
    }

    private const string ValidCueConstraint = "Switch only on valid cue.";
    private const string InvalidCueConstraint = "Invalid cues must not trigger switch.";
    private const string NoAnticipatorySwitchingConstraint = "No anticipatory switching.";
    private const string NoRereadConstraint = "No rereading after encode window.";
    private const string NoInventedItemsConstraint = "No invented items.";

    private static GeneratedDrillContentRequest CreateInvalidCueFilterRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.FS,
            GlobalLevelId.L3,
            DrillId.FS2InvalidCueFilter,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "fs-l3-invalid-cue-filter",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("target count", "2"),
                new LoadVariable("switch count", "6"),
                new LoadVariable("cue density", "moderate"),
                new LoadVariable("rule contrast", "valid symbol versus invalid lure"),
                new LoadVariable("return precision", "next valid cue"),
            ],
            [
                new CriticalConstraint(ValidCueConstraint),
                new CriticalConstraint(InvalidCueConstraint),
                new CriticalConstraint(NoAnticipatorySwitchingConstraint),
            ]);
    }

    private static TransferContentGenerationRequest CreateTransferRequest()
    {
        return new TransferContentGenerationRequest(
            new GeneratedDrillContentRequest(
                BranchCode.WM,
                GlobalLevelId.L4,
                DrillId.WM1DelayedReconstruction,
                SessionType.Transfer,
                PromptContentKind.EquivalentPrompt,
                "wm-l4-transfer-unfamiliar-structure",
                PromptFreshnessPolicy.FreshEquivalentRequired,
                [new LoadVariable("transfer distance", "far transfer to unfamiliar visual structure")],
                [
                    new CriticalConstraint(NoRereadConstraint),
                    new CriticalConstraint(NoInventedItemsConstraint),
                ]),
            CapacityId.EncodingFidelity,
            "far transfer to unfamiliar visual structure");
    }

    private static BranchLevelStandard StandardFor(GeneratedDrillContentResult result)
    {
        return ProgramCatalog.Standards.Single(standard =>
            standard.Branch == result.Branch &&
            standard.Level == result.Level);
    }

    private static string FailureSummary(GeneratedContentSelectionResult selection)
    {
        return string.Join(
            "; ",
            selection.FreshnessValidation.Failures.Select(failure => failure.Detail)
                .Concat(selection.DifficultyAudit?.Failures.Select(failure => failure.Detail) ?? [])
                .Concat(selection.TransferValidation?.Failures.Select(failure => failure.Detail) ?? [])
                .Concat(selection.AntiSelfDeceptionGuard.Findings.Select(finding => finding.Detail)));
    }

    private static string GuardSummary(GeneratedContentSelectionResult selection)
    {
        return string.Join("; ", selection.AntiSelfDeceptionGuard.Findings.Select(finding => finding.Detail));
    }
}
