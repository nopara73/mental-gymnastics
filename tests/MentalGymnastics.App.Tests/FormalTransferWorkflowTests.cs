using System.Text.RegularExpressions;
using MentalGymnastics.App;
using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class FormalTransferWorkflowTests : IDisposable
{
    private static readonly TrainingDate TestDate = TrainingDate.From(2026, 7, 11);
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FocusHoldL4TransferExecutesAndPersistsEveryFormalDecision()
    {
        var configuration = Configuration();
        await SeedEligibleStateAsync(configuration);
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.FH, GlobalLevelId.L4);
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    TestDate,
                    new RequestedTrainingWork(
                        BranchCode.FH,
                        GlobalLevelId.L4,
                        DrillId.FH2DistractorHold,
                        AppTrainingSessionType.Transfer,
                        profile.TargetStage.LoadVariables)),
                "formal-transfer-fh-l4"));

        Assert.True(prepared.IsPrepared);
        Assert.Equal(AppTrainingSessionType.Transfer, prepared.Selection.SelectedWork!.SessionType);
        Assert.Contains(prepared.RuntimeSession!.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.ComponentPayload);

        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession,
            started,
            saveActiveSnapshot: false);

        var active = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, active.CurrentPhaseKind);
        clock.AdvanceBy(new RuntimeDuration(TimeSpan.FromMinutes(5)));
        var components = await controller.RefreshAsync();
        Assert.Equal(RuntimeSessionPhaseKind.ReconstructionInput, components.CurrentPhaseKind);
        var submitted = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(
                RuntimeInputCommandKind.SubmitAnswer,
                value: ComponentAnswers(prepared.RuntimeSession.InputMaterials)));
        Assert.True(submitted.LastCommand!.IsAccepted);
        var review = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.Review, review.CurrentPhaseKind);
        var terminal = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.True(terminal.IsTerminal);

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(
                TestDate,
                mainFailureModeAvoided: "responding to distractor"));

        var processing = completed.WorkflowResult!.ProcessingResult;
        Assert.True(processing.StandardEvaluationResult!.Passed);
        Assert.Equal(GateOutcome.PassOnce, processing.FormalGateDecision!.Outcome);
        Assert.True(processing.TransferEligibilityResult!.IsEligible);
        Assert.Equal(LocalCompletedSessionType.Transfer, processing.SessionHistory.SessionType);
        Assert.Equal(EvidenceArtifactCategory.Transfer, processing.EvidenceArtifacts.Single().Artifact.Category);
        Assert.Equal(FormalTestPassState.PassOnce, processing.FormalTestAttempt!.Attempt.PassState);
        Assert.Equal(
            BranchLevelState.PassedOnce,
            completed.WorkflowResult.RefreshedState.CurrentPractitionerState.GetBranchLevelState(
                BranchCode.FH,
                GlobalLevelId.L4));
        Assert.NotNull(await new LocalFormalTestAttemptStore(configuration.LocalDatabaseOptions)
            .LoadAsync(processing.FormalTestAttempt.AttemptId));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private AppStartupConfiguration Configuration() =>
        AppStartupConfiguration.ForAppOwnedLocalStoragePath(
            Path.Combine(tempDirectory, "mental-gymnastics.json"));

    private static async Task SeedEligibleStateAsync(AppStartupConfiguration configuration)
    {
        var statuses = new[]
        {
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Maintenance),
            new BranchLevelStatus(BranchCode.FH, GlobalLevelId.L4, BranchLevelState.TestReady),
            new BranchLevelStatus(BranchCode.FS, GlobalLevelId.L2, BranchLevelState.Maintenance),
            new BranchLevelStatus(BranchCode.WM, GlobalLevelId.L2, BranchLevelState.Maintenance),
            new BranchLevelStatus(BranchCode.IR, GlobalLevelId.L2, BranchLevelState.Maintenance),
            new BranchLevelStatus(BranchCode.DE, GlobalLevelId.L2, BranchLevelState.Maintenance),
        };
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions)
            .SaveAsync(new PractitionerState(statuses));
        var maintenanceStore = new LocalMaintenanceCheckStore(configuration.LocalDatabaseOptions);
        var artifactStore = new LocalEvidenceArtifactStore(configuration.LocalDatabaseOptions);
        foreach (var status in statuses.Where(status => status.State == BranchLevelState.Maintenance))
        {
            var profile = TrainingLoadProfileCatalog.Get(status.Branch, status.Level);
            var checkId = $"maintenance-{status.Branch}-{status.Level}";
            var artifactId = $"artifact-{checkId}";
            var result = new StandardEvaluationResult(true, []);
            await artifactStore.SaveAsync(new LocalEvidenceArtifactRecord(
                artifactId,
                new LocalProgrammingEventReference(
                    checkId,
                    LocalProgrammingEventKind.Maintenance,
                    status.Branch,
                    status.Level,
                    profile.Drill),
                new EvidenceArtifact(
                    EvidenceArtifactCategory.Maintenance,
                    TestDate,
                    [new ObservableEvidence(ObservableEvidenceKind.MaintenanceCheck, "Full standard passed.")],
                    "Current maintenance check.")));
            await maintenanceStore.SaveMaintenanceAsync(new LocalMaintenanceCheckRecord(
                checkId,
                artifactId,
                completedSessionId: null,
                profile.Drill,
                ProgramCatalog.Standards.Single(standard =>
                    standard.Branch == status.Branch && standard.Level == status.Level).Standard,
                new MaintenanceCheckEvidence(
                    status.Branch,
                    status.Level,
                    TestDate,
                    MaintenanceCurrencyEvaluator.CadenceFor(status.Branch, status.Level).RequiredCheckKind,
                    result)));
        }
    }

    private static string ComponentAnswers(IEnumerable<GeneratedContentMaterial> materials)
    {
        return string.Join("; ", materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.BranchScoringKey)
            .Select(material =>
            {
                var branch = Regex.Match(
                    material.Value,
                    @"(?:component\s+)?branch\s+([A-Z]{2})\b",
                    RegexOptions.IgnoreCase).Groups[1].Value.ToUpperInvariant();
                var expected = Regex.Match(
                    material.Value,
                    @"expected\s+response\s+([^;]+)",
                    RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                return $"{branch}={expected}";
            }));
    }
}
