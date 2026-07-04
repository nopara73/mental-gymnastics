using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentPersistenceHandoffTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.Content.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GeneratedInstanceHandoffCanBeSavedAndReconstructedForAuditAndResume()
    {
        var generated = WorkingMemoryGeneratedContentGenerator.Generate(
            CreateDelayedReconstructionRequest(),
            new GeneratedContentSeed("wm-persistence-handoff"));

        var handoff = GeneratedContentPersistenceHandoffMapper.Create(
            generated.Result,
            generated.Materials,
            TrainingDate.From(2026, 7, 4));

        Assert.True(handoff.CanBeRecordedByPersistence);
        Assert.False(handoff.OwnsPersistence);
        Assert.False(handoff.GrantsAdvancement);
        Assert.Equal(generated.Result.InstanceId, handoff.InstanceId);
        Assert.Equal(generated.Result.ContentVersion, handoff.ContentVersion);
        Assert.Equal(generated.Result.Branch, handoff.Branch);
        Assert.Equal(generated.Result.Level, handoff.Level);
        Assert.Equal(generated.Result.Drill, handoff.Drill);
        Assert.Equal(generated.Result.Request.LoadVariables, handoff.LoadVariables);
        Assert.Equal(generated.Result.ContentId, handoff.ContentIdentity.ContentId);
        Assert.Equal(generated.Result.EquivalenceClass, handoff.ContentIdentity.EquivalenceClass);
        Assert.Equal(PromptFreshnessPolicy.FreshEquivalentRequired, handoff.FreshnessPolicy);
        Assert.Contains("payload-family=wm-delayed-reconstruction", handoff.ContentSummary, StringComparison.Ordinal);
        Assert.Contains(handoff.AuditMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.EncodeItem);
        Assert.Contains(handoff.AuditMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.ExpectedReconstruction);
        Assert.Contains(handoff.AuditMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.HonestyConstraint &&
            material.Value == NoRereadConstraint);
        Assert.Contains(handoff.AuditMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.HonestyConstraint &&
            material.Value == NoInventedItemsConstraint);

        var databasePath = Path.Combine(tempDirectory, "mental-gymnastics.db");
        var record = new LocalGeneratedDrillInstanceRecord(
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

        await new LocalGeneratedDrillInstanceStore(Options(databasePath)).SaveAsync(record);

        var loaded = await new LocalGeneratedDrillInstanceStore(Options(databasePath)).LoadAsync(handoff.InstanceId);
        Assert.NotNull(loaded);
        Assert.Equal(handoff.ContentSummary, loaded.ContentSummary);
        Assert.Equal(handoff.FreshnessPolicy, loaded.FreshnessPolicy);
        Assert.Equal(handoff.ContentIdentity.ContentId, loaded.ContentIdentity.ContentId);
        Assert.Equal(handoff.ContentIdentity.EquivalenceClass, loaded.ContentIdentity.EquivalenceClass);
        Assert.Equal(handoff.ContentVersion, loaded.ContentIdentity.Version);
        Assert.Contains(loaded.AuditMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.EncodeItem.ToString());
        Assert.Contains(loaded.AuditMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.ExpectedReconstruction.ToString());

        var reconstructed = GeneratedContentPersistenceHandoff.Restore(
            loaded.InstanceId,
            loaded.GeneratedOn,
            loaded.ContentIdentity.ToPromptContentIdentity(),
            loaded.ContentIdentity.Version,
            loaded.LoadVariables,
            loaded.FreshnessPolicy!.Value,
            loaded.ContentSummary!,
            loaded.AuditMaterials.Select(material =>
                new GeneratedContentPersistenceAuditMaterial(
                    Enum.Parse<GeneratedContentMaterialKind>(material.Kind),
                    material.Name,
                    material.Value)));

        Assert.Equal(handoff.InstanceId, reconstructed.InstanceId);
        Assert.Equal(handoff.ContentIdentity.ContentId, reconstructed.ContentIdentity.ContentId);
        Assert.Equal(handoff.ContentVersion, reconstructed.ContentVersion);
        Assert.Equal(handoff.ContentSummary, reconstructed.ContentSummary);
        Assert.Equal(handoff.LoadVariables, reconstructed.LoadVariables);
        Assert.Equal(
            handoff.AuditMaterials.Select(material => (material.Kind, material.Name, material.Value)),
            reconstructed.AuditMaterials.Select(material => (material.Kind, material.Name, material.Value)));
        Assert.Contains(reconstructed.AuditMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.ExpectedReconstruction);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private const string NoRereadConstraint = "No rereading after encode window.";
    private const string NoInventedItemsConstraint = "No invented items.";

    private static LocalDatabaseOptions Options(string databasePath)
    {
        return LocalDatabaseOptions.ForAppOwnedPath(databasePath);
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
            [
                new CriticalConstraint(NoRereadConstraint),
                new CriticalConstraint(NoInventedItemsConstraint),
            ]);
    }
}
