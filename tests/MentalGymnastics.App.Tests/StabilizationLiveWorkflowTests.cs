using MentalGymnastics.App;
using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class StabilizationLiveWorkflowTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ControlledDistractorMustActuallyAppearBeforeStabilizationRecordsIt()
    {
        var configuration = Configuration();
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(
                    BranchCode.FH,
                    GlobalLevelId.L1,
                    BranchLevelState.PassedOnce),
            ]));
        var date = TrainingDate.From(2026, 7, 11);
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.FH, GlobalLevelId.L1);
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    date,
                    new RequestedTrainingWork(
                        BranchCode.FH,
                        GlobalLevelId.L1,
                        DrillId.FH1TargetHold,
                        AppTrainingSessionType.Stabilization,
                        profile.TargetStage.LoadVariables)),
                "stabilization-live-controlled-demand"));

        Assert.True(
            prepared.IsPrepared,
            string.Join(
                " | ",
                prepared.Selection.Blockers.Select(blocker => blocker.Detail)
                    .Concat(prepared.Rejections.Select(rejection => rejection.Detail))
                    .Concat(prepared.GeneratedContent?.Rejections.Select(rejection => rejection.Detail) ?? [])));
        Assert.Contains(prepared.RuntimeSession!.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.Interference &&
            material.Name == StabilizationGeneratedContent.ControlledDistractorId);

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

        clock.AdvanceBy(RuntimeDuration.FromSeconds(1));
        var distracted = await controller.RefreshAsync();
        Assert.True(distracted.ActiveCue?.IsControlledDistractor);
        Assert.Equal(RuntimeCueResponseExpectation.NoResponseExpected, distracted.ActiveCue?.ResponseExpectation);

        clock.AdvanceBy(new RuntimeDuration(TimeSpan.FromMinutes(3)));
        var review = await controller.RefreshAsync();
        Assert.Equal(RuntimeSessionPhaseKind.Review, review.CurrentPhaseKind);
        var terminal = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.True(terminal.IsTerminal);

        var completed = await controller.CompleteAsync(
            new PreUiLiveSessionCompletionRequest(
                date,
                mainFailureModeAvoided: "Target substitution avoided."));

        Assert.True(completed.IsProcessed);
        Assert.True(completed.WorkflowResult!.ProcessingResult
            .StabilizationPass!.Evidence.AfterAdjacentWorkOrControlledDistractor);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private AppStartupConfiguration Configuration()
    {
        return AppStartupConfiguration.ForAppOwnedLocalStoragePath(
            Path.Combine(tempDirectory, "mental-gymnastics.json"));
    }
}
