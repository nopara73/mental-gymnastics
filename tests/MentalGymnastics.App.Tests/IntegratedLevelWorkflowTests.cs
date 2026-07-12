using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class IntegratedLevelWorkflowTests : IDisposable
{
    private static readonly TrainingDate TestDate = TrainingDate.From(2026, 7, 11);
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MentalGymnastics.App.Tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(BranchCode.FH)]
    [InlineData(BranchCode.FS)]
    [InlineData(BranchCode.WM)]
    [InlineData(BranchCode.IR)]
    [InlineData(BranchCode.AI)]
    public async Task EveryIntegratedL5DrillExposesBranchTasksAndSeparateEvidence(BranchCode branch)
    {
        var configuration = Configuration();
        await new LocalPractitionerStateStore(configuration.LocalDatabaseOptions).SaveAsync(
            new PractitionerState(
            [
                new BranchLevelStatus(branch, GlobalLevelId.L5, BranchLevelState.Maintenance),
            ]));
        var profile = TrainingLoadProfileCatalog.Get(branch, GlobalLevelId.L5);
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    TestDate,
                    new RequestedTrainingWork(
                        branch,
                        GlobalLevelId.L5,
                        profile.Drill,
                        AppTrainingSessionType.Maintenance)),
                $"integrated-l5-{branch}"));

        Assert.True(prepared.IsPrepared, string.Join(" | ", prepared.Rejections.Select(item => item.Detail)));
        var runtimeSession = prepared.RuntimeSession ??
            throw new InvalidOperationException("Prepared integrated work must include a runtime session.");
        var generatedComponents = runtimeSession.InputMaterials
            .Where(material =>
                material.Kind == MentalGymnastics.Content.GeneratedContentMaterialKind.ComponentPayload)
            .ToArray();
        var heldTarget = HeldTarget(generatedComponents);
        var runtimeDefinition = runtimeSession.SessionDefinition ??
            throw new InvalidOperationException("Prepared integrated work must include a runtime definition.");
        var executedSourceBranch = branch == BranchCode.AI
            ? BranchFor(runtimeDefinition.SourceDrill)
            : branch;
        Assert.Equal(branch == BranchCode.AI ? 3 : 1, generatedComponents.Length);
        Assert.DoesNotContain(generatedComponents, material => material.Value.Contains(
            $"component branch {executedSourceBranch}",
            StringComparison.OrdinalIgnoreCase));
        Assert.All(generatedComponents, material =>
            Assert.Contains("pass criterion exact response", material.Value, StringComparison.OrdinalIgnoreCase));

        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                runtimeSession,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            runtimeSession,
            started,
            saveActiveSnapshot: false);
        var prep = controller.CaptureState();
        Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, prep.CurrentPhaseKind);
        Assert.True(HasComponents(prep));
        Assert.DoesNotContain(prep.CurrentMaterials, material => material.Kind == "BranchScoringKey");
        if (heldTarget is not null)
        {
            Assert.Contains(prep.CurrentMaterials, material =>
                material.Kind == "ComponentPayload" &&
                material.Value.Contains(heldTarget, StringComparison.Ordinal));
        }
        if (branch == BranchCode.AI)
        {
            Assert.Contains(prep.CurrentMaterials, material => material.Kind == "SourceBranchStandard");
            Assert.Contains(prep.CurrentMaterials, material =>
                material.Kind == "SourceTask" &&
                material.Value.Contains("wrapped source criterion", StringComparison.OrdinalIgnoreCase));
        }
        var state = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));

        for (var step = 0; step < 4 && !HasComponents(state); step++)
        {
            var submit = state.Commands.FirstOrDefault(command =>
                command.Command == RuntimeInputCommandKind.SubmitAnswer && command.IsAvailable);
            if (submit is not null)
            {
                state = await controller.HandleCommandAsync(new PreUiLiveSessionCommandRequest(
                    RuntimeInputCommandKind.SubmitAnswer,
                    value: "Rule declared before work."));
                continue;
            }

            var finish = state.Commands.FirstOrDefault(command =>
                command.Command == RuntimeInputCommandKind.FinishPhase && command.IsAvailable);
            Assert.NotNull(finish);
            state = await controller.HandleCommandAsync(
                new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        }

        var components = state.CurrentMaterials
            .Where(material => material.Kind == "ComponentPayload")
            .ToArray();
        var evidenceRequirements = state.CurrentMaterials
            .Where(material => material.Kind == "ComponentEvidenceRequirement")
            .ToArray();
        Assert.True(components.Length >= 1);
        Assert.Equal(components.Length, evidenceRequirements.Length);
        Assert.DoesNotContain(state.CurrentMaterials, material => material.Kind == "BranchScoringKey");
        if (heldTarget is not null)
        {
            Assert.DoesNotContain(state.CurrentMaterials, material =>
                material.Value.Contains(heldTarget, StringComparison.Ordinal));
            Assert.Contains(components, material => material.Value.Contains(
                "target memorized during setup",
                StringComparison.OrdinalIgnoreCase));
        }
        Assert.True(
            state.Timer.IsTimed || state.Commands.Any(command =>
                command.IsAvailable && command.Command is not
                    RuntimeInputCommandKind.Pause and not
                    RuntimeInputCommandKind.Abandon and not
                    RuntimeInputCommandKind.FinishPhase),
            "Integrated work must expose either a running protocol or a direct response control.");
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

    private static bool HasComponents(PreUiLiveSessionState state) =>
        state.CurrentMaterials.Any(material => material.Kind == "ComponentPayload");

    private static string? HeldTarget(
        IReadOnlyList<MentalGymnastics.Content.GeneratedContentMaterial> components)
    {
        var focusComponent = components.FirstOrDefault(material => material.Value.Contains(
            "component branch FH:",
            StringComparison.OrdinalIgnoreCase));
        if (focusComponent is null)
        {
            return null;
        }

        const string startMarker = "; challenge Hold ";
        const string endMarker = " while completing";
        var start = focusComponent.Value.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        var end = start < 0
            ? -1
            : focusComponent.Value.IndexOf(
                endMarker,
                start + startMarker.Length,
                StringComparison.OrdinalIgnoreCase);
        return start < 0 || end < 0
            ? null
            : focusComponent.Value[(start + startMarker.Length)..end];
    }

    private static BranchCode BranchFor(DrillId? sourceDrill)
    {
        return sourceDrill switch
        {
            DrillId.FH1TargetHold or DrillId.FH2DistractorHold => BranchCode.FH,
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter => BranchCode.FS,
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => BranchCode.IR,
            _ => throw new InvalidOperationException($"Unsupported integrated source drill: {sourceDrill}"),
        };
    }
}
