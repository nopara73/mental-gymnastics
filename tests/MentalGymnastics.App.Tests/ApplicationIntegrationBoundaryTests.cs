using MentalGymnastics.App;
using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class ApplicationIntegrationBoundaryTests
{
    [Fact]
    public void BoundaryDeclaresPreUiCompositionWithoutExternalServiceOwnership()
    {
        var capabilities = ApplicationIntegrationBoundary.Capabilities;

        Assert.True(capabilities.OfflineCapable);
        Assert.False(capabilities.RequiresAndroidUi);
        Assert.False(capabilities.AllowsAccounts);
        Assert.False(capabilities.AllowsSync);
        Assert.False(capabilities.AllowsBackendServices);
        Assert.False(capabilities.AllowsTelemetry);
        Assert.False(capabilities.AllowsNotifications);
        Assert.False(capabilities.AllowsAnalytics);
        Assert.False(capabilities.AllowsAiOrApiDependencies);
        Assert.False(capabilities.ReplacesJsonPersistenceWithSqlite);
        Assert.False(capabilities.OwnsProgressionRules);
        Assert.False(capabilities.OwnsPersistenceInternals);
        Assert.False(capabilities.OwnsRuntimeExecution);
        Assert.False(capabilities.OwnsGeneratedContent);
        Assert.True(capabilities.ComposesCore);
        Assert.True(capabilities.ComposesPersistence);
        Assert.True(capabilities.ComposesRuntime);
        Assert.True(capabilities.ComposesGeneratedContent);
    }

    [Fact]
    public void SmokeCompositionConsumesExistingLayerTypes()
    {
        var composition = ApplicationIntegrationBoundary.ComposeSmokeTest(BranchCode.FH);

        Assert.Equal(BranchCode.FH, composition.Branch.Code);
        Assert.Equal(BranchType.Foundational, composition.Branch.Type);
        Assert.True(composition.Persistence.OfflineOnly);
        Assert.True(composition.Runtime.OfflineCapable);
        Assert.True(composition.Content.OfflineCapable);
        Assert.True(composition.Content.DeterministicWithExplicitInputs);
        Assert.True(composition.CanComposeExistingLayers);
    }

    [Fact]
    public void DependencyDirectionKeepsExistingLibrariesIndependentOfAppLayer()
    {
        var appReferences = typeof(ApplicationIntegrationBoundary).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("MentalGymnastics.Core", appReferences);
        Assert.Contains("MentalGymnastics.Persistence", appReferences);
        Assert.Contains("MentalGymnastics.Runtime", appReferences);
        Assert.Contains("MentalGymnastics.Content", appReferences);
        Assert.DoesNotContain("MentalGymnastics.Android", appReferences);

        AssertDoesNotReferenceApp(typeof(ProgramCatalog).Assembly);
        AssertDoesNotReferenceApp(typeof(LocalPersistenceBoundary).Assembly);
        AssertDoesNotReferenceApp(typeof(SessionRuntimeBoundary).Assembly);
        AssertDoesNotReferenceApp(typeof(GeneratedContentBoundary).Assembly);
    }

    private static void AssertDoesNotReferenceApp(System.Reflection.Assembly assembly)
    {
        var references = assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("MentalGymnastics.App", references);
    }
}
