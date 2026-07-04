using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class LocalPersistenceBoundaryTests
{
    [Fact]
    public void BoundaryDeclaresOfflineUserlessDeviceLocalCapabilities()
    {
        var capabilities = LocalPersistenceBoundary.Capabilities;

        Assert.True(capabilities.OfflineOnly);
        Assert.True(capabilities.Userless);
        Assert.True(capabilities.DeviceLocal);
        Assert.False(capabilities.AllowsAccounts);
        Assert.False(capabilities.AllowsSync);
        Assert.False(capabilities.AllowsBackendServices);
        Assert.False(capabilities.AllowsTelemetry);
        Assert.False(capabilities.AllowsPushNotifications);
        Assert.False(capabilities.AllowsAiOrApiDependencies);
        Assert.False(capabilities.OwnsProgressionLogic);
    }

    [Fact]
    public void SnapshotConsumesCoreDomainConceptsWithoutRedefiningProgramState()
    {
        var practitionerState = new PractitionerState(
        [
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L1, BranchLevelState.Training),
        ]);
        var artifact = CreateEvidenceArtifact(EvidenceArtifactCategory.Test);
        var attempt = CreateFormalAttempt(artifact);
        var maintenanceCheck = CreateMaintenanceCheck();
        var failure = new ClassifiedFailure(
            BranchCode.WM,
            GlobalLevelId.L1,
            FailureType.Overload,
            [FailureEvidenceSignal.ErrorsRiseAfterLoadIncrease, FailureEvidenceSignal.ConstraintPreserved]);

        var snapshot = new LocalProgramStateSnapshot(
            practitionerState,
            [artifact],
            [attempt],
            [maintenanceCheck],
            [failure]);

        Assert.Same(practitionerState, snapshot.PractitionerState);
        Assert.IsType<EvidenceArtifact>(Assert.Single(snapshot.EvidenceArtifacts));
        Assert.IsType<FormalTestAttempt>(Assert.Single(snapshot.FormalTestAttempts));
        Assert.IsType<MaintenanceCheckEvidence>(Assert.Single(snapshot.MaintenanceChecks));
        Assert.IsType<ClassifiedFailure>(Assert.Single(snapshot.ClassifiedFailures));
        Assert.Equal(BranchLevelState.Owned, snapshot.PractitionerState.GetBranchLevelState(BranchCode.FH, GlobalLevelId.L1));
    }

    [Fact]
    public void SnapshotDefensivelyCopiesMutableInputs()
    {
        var evidenceArtifacts = new List<EvidenceArtifact> { CreateEvidenceArtifact(EvidenceArtifactCategory.Practice) };
        var formalAttempts = new List<FormalTestAttempt> { CreateFormalAttempt(CreateEvidenceArtifact(EvidenceArtifactCategory.Test)) };
        var maintenanceChecks = new List<MaintenanceCheckEvidence> { CreateMaintenanceCheck() };
        var classifiedFailures = new List<ClassifiedFailure>
        {
            new(
                BranchCode.FH,
                GlobalLevelId.L1,
                FailureType.TechnicalFailure,
                [FailureEvidenceSignal.ConfusionAboutRule]),
        };

        var snapshot = new LocalProgramStateSnapshot(
            new PractitionerState([new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training)]),
            evidenceArtifacts,
            formalAttempts,
            maintenanceChecks,
            classifiedFailures);

        evidenceArtifacts.Add(CreateEvidenceArtifact(EvidenceArtifactCategory.Load));
        formalAttempts.Clear();
        maintenanceChecks.Clear();
        classifiedFailures.Clear();

        Assert.Single(snapshot.EvidenceArtifacts);
        Assert.Single(snapshot.FormalTestAttempts);
        Assert.Single(snapshot.MaintenanceChecks);
        Assert.Single(snapshot.ClassifiedFailures);
    }

    [Fact]
    public void SnapshotRequiresStateAndCollections()
    {
        var state = new PractitionerState([new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Training)]);

        Assert.Throws<ArgumentNullException>(() => new LocalProgramStateSnapshot(
            null!,
            [],
            [],
            [],
            []));
        Assert.Throws<ArgumentNullException>(() => new LocalProgramStateSnapshot(
            state,
            null!,
            [],
            [],
            []));
        Assert.Throws<ArgumentNullException>(() => new LocalProgramStateSnapshot(
            state,
            [],
            null!,
            [],
            []));
        Assert.Throws<ArgumentNullException>(() => new LocalProgramStateSnapshot(
            state,
            [],
            [],
            null!,
            []));
        Assert.Throws<ArgumentNullException>(() => new LocalProgramStateSnapshot(
            state,
            [],
            [],
            [],
            null!));
    }

    [Fact]
    public async Task StoreBoundaryLoadsAndSavesWholeLocalSnapshots()
    {
        ILocalProgramStateStore store = new InMemoryLocalProgramStateStore();
        var snapshot = CreateSnapshot();

        await store.SaveAsync(snapshot);
        var loaded = await store.LoadAsync();

        Assert.Same(snapshot, loaded);
    }

    [Fact]
    public void PersistenceAssemblyReferencesCoreWithoutReferencingAndroid()
    {
        var references = typeof(LocalProgramStateSnapshot)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Contains("MentalGymnastics.Core", references);
        Assert.DoesNotContain("MentalGymnastics.Android", references);
    }

    [Fact]
    public void CoreAssemblyDoesNotReferencePersistence()
    {
        var references = typeof(PractitionerState)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("MentalGymnastics.Persistence", references);
    }

    private static LocalProgramStateSnapshot CreateSnapshot()
    {
        var artifact = CreateEvidenceArtifact(EvidenceArtifactCategory.Test);

        return new LocalProgramStateSnapshot(
            new PractitionerState(
            [
                new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.Owned),
            ]),
            [artifact],
            [CreateFormalAttempt(artifact)],
            [CreateMaintenanceCheck()],
            [
                new ClassifiedFailure(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    FailureType.EffortFailure,
                    [FailureEvidenceSignal.BrokenHonestyConstraint]),
            ]);
    }

    private static EvidenceArtifact CreateEvidenceArtifact(EvidenceArtifactCategory category)
    {
        return new EvidenceArtifact(
            category,
            TrainingDate.From(2026, 7, 4),
            [new ObservableEvidence(ObservableEvidenceKind.OutputSample, "Observed branch artifact sample.")],
            "artifact://local/test");
    }

    private static FormalTestAttempt CreateFormalAttempt(EvidenceArtifact artifact)
    {
        return new FormalTestAttempt(
            BranchCode.FH,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 4),
            TestTask.ForDrill(DrillId.FH1TargetHold),
            [new LoadVariable("Duration", "3 minutes")],
            "Hold one simple target for 3 minutes.",
            [new CriticalConstraint("No target change; every drift is marked.")],
            new TestResultEvidence(TestResultEvidenceKind.PassFail, "pass"),
            failureType: null,
            FormalTestPassState.PassOnce,
            artifact);
    }

    private static MaintenanceCheckEvidence CreateMaintenanceCheck()
    {
        return new MaintenanceCheckEvidence(
            BranchCode.FH,
            GlobalLevelId.L1,
            TrainingDate.From(2026, 7, 4),
            MaintenanceCheckKind.StandardOrTransfer,
            new StandardEvaluationResult(Passed: true, Failures: []));
    }

    private sealed class InMemoryLocalProgramStateStore : ILocalProgramStateStore
    {
        private LocalProgramStateSnapshot? snapshot;

        public ValueTask<LocalProgramStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(snapshot);
        }

        public ValueTask SaveAsync(
            LocalProgramStateSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(snapshot);

            this.snapshot = snapshot;
            return ValueTask.CompletedTask;
        }
    }
}
