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
        await SeedEligibleL4StateAsync(configuration, BranchCode.FH);
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
        Assert.DoesNotContain(prepared.RuntimeSession.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.ComponentPayload &&
            material.Value.Contains("component branch FH", StringComparison.OrdinalIgnoreCase));
        var preflight = TrainingPresentationMapper.FromPreflight(prepared);
        Assert.Equal("Hold target during WM or DE task.", preflight.Work!.Exercise.PrimaryMaterial);
        Assert.Contains(preflight.Work.Exercise.SetupItems, item =>
            item == "SAME DEMAND  Target, drift marking, return standard.");
        Assert.Contains(preflight.Work.Exercise.SetupItems, item =>
            item == "NEW CONTEXT  Content and task format.");

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
        Assert.Contains(active.CurrentMaterials, material => material.Kind == "TransferTask");
        Assert.Contains(active.CurrentMaterials, material => material.Kind == "SameDemand");
        Assert.Contains(active.CurrentMaterials, material => material.Kind == "ChangedContext");
        Assert.Contains(active.CurrentMaterials, material => material.Kind == "SourceBranchStandard");
        Assert.Contains(active.CurrentMaterials, material => material.Kind == "ComponentPayload");
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
        var restoredAtReview = new PreUiLiveSessionController(
            workflow,
            started.CommandHandler!,
            started.CueScheduler,
            prepared.RuntimeSession.InputMaterials,
            prepared.RuntimeSession.ExpectedEvidenceFacts,
            AppTrainingSessionType.Transfer,
            saveActiveSnapshot: false);
        var terminal = await restoredAtReview.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.True(terminal.IsTerminal);

        var completed = await restoredAtReview.CompleteAsync(
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

    [Fact]
    public async Task ConceptOperationsL4TransferRunsTheVisibleHeldOutAuditBeforePassing()
    {
        var configuration = Configuration();
        await SeedEligibleL4StateAsync(configuration, BranchCode.CO);
        var profile = TrainingLoadProfileCatalog.Get(BranchCode.CO, GlobalLevelId.L4);
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    TestDate,
                    new RequestedTrainingWork(
                        BranchCode.CO,
                        GlobalLevelId.L4,
                        DrillId.CO2StructureMapping,
                        AppTrainingSessionType.Transfer,
                        profile.TargetStage.LoadVariables)),
                "formal-transfer-co-l4-audit"));
        Assert.True(prepared.IsPrepared, string.Join(" | ", prepared.Rejections.Select(item => item.Detail)));
        var runtimeSession = prepared.RuntimeSession!;

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

        var relationNaming = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.ActiveWork, relationNaming.CurrentPhaseKind);
        await controller.HandleCommandAsync(new PreUiLiveSessionCommandRequest(
            RuntimeInputCommandKind.SubmitAnswer,
            value: SourceRelations(runtimeSession.InputMaterials)));
        var mapping = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.ReconstructionInput, mapping.CurrentPhaseKind);
        await controller.HandleCommandAsync(new PreUiLiveSessionCommandRequest(
            RuntimeInputCommandKind.SubmitAnswer,
            value: MappingAnswers(runtimeSession.InputMaterials)));
        var audit = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));

        Assert.Equal(RuntimeSessionPhaseKind.Audit, audit.CurrentPhaseKind);
        Assert.Contains(audit.CurrentMaterials, material =>
            material.Kind == "AuditPayload" &&
            material.Value.Contains("UNSEEN PROBE", StringComparison.Ordinal));
        Assert.Contains(audit.CurrentMaterials, material => material.Kind == "SourceStructure");
        Assert.Contains(audit.CurrentMaterials, material => material.Kind == "TargetStructure");
        Assert.DoesNotContain(audit.CurrentMaterials, material => material.Kind == "ExpectedFinding");
        Assert.True(audit.Commands.Single(command => command.Command == RuntimeInputCommandKind.StartAudit).IsAvailable);

        await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.StartAudit));
        var expectedFinding = runtimeSession.InputMaterials.Single(material =>
            material.Kind == GeneratedContentMaterialKind.ExpectedFinding).Value;
        await controller.HandleCommandAsync(new PreUiLiveSessionCommandRequest(
            RuntimeInputCommandKind.SubmitAnswer,
            value: $"{expectedFinding}; TEST=held out relation evidence decides the verdict"));
        var review = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.Equal(RuntimeSessionPhaseKind.Review, review.CurrentPhaseKind);
        var terminal = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.True(terminal.IsTerminal);

        var completed = await controller.CompleteAsync(new PreUiLiveSessionCompletionRequest(
            TestDate,
            mainFailureModeAvoided: "unsupported inference"));
        Assert.True(completed.WorkflowResult!.ProcessingResult.StandardEvaluationResult!.Passed);
        Assert.True(completed.WorkflowResult.ProcessingResult.TransferEligibilityResult!.IsEligible);
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
    public async Task EveryL4TransferExposesItsContractBeforeAndDuringWork(BranchCode branch)
    {
        var configuration = Configuration();
        await SeedEligibleL4StateAsync(configuration, branch);
        var profile = TrainingLoadProfileCatalog.Get(branch, GlobalLevelId.L4);
        var workflow = new PreUiTrainingWorkflowService(configuration);
        var prepared = await workflow.PrepareNextSessionWithDefaultsAsync(
            new PreUiTrainingWorkflowDefaultPreparationRequest(
                new NextTrainingWorkSelectionQuery(
                    TestDate,
                    new RequestedTrainingWork(
                        branch,
                        GlobalLevelId.L4,
                        profile.Drill,
                        AppTrainingSessionType.Transfer,
                        profile.TargetStage.LoadVariables)),
                $"formal-transfer-contract-{branch}"));

        Assert.True(prepared.IsPrepared, string.Join(" | ", prepared.Rejections.Select(item => item.Detail)));
        var preflight = TrainingPresentationMapper.FromPreflight(prepared);
        var definition = TransferTestCatalog.TransferTests.Single(item => item.SourceBranch == branch);
        Assert.Equal(definition.TransferTask, preflight.Work!.Exercise.PrimaryMaterial);
        Assert.Contains(preflight.Work.Exercise.SetupItems, item =>
            item == $"SAME DEMAND  {definition.SameDemand}");
        Assert.Contains(preflight.Work.Exercise.SetupItems, item =>
            item == $"NEW CONTEXT  {definition.ChangedContext}");

        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var started = await workflow.StartResumableSessionAsync(
            new PreUiTrainingWorkflowStartRequest(
                prepared.RuntimeSession!,
                clock,
                saveActiveSnapshot: false));
        var controller = new PreUiLiveSessionController(
            workflow,
            prepared.RuntimeSession!,
            started,
            saveActiveSnapshot: false);

        AssertTransferContract(controller.CaptureState(), definition);
        if (branch == BranchCode.AI)
        {
            Assert.Contains(controller.CaptureState().CurrentMaterials, material =>
                material.Kind == "SourceTask" &&
                material.Value.Contains("wrapped source criterion", StringComparison.OrdinalIgnoreCase));
        }
        var firstWork = await controller.HandleCommandAsync(
            new PreUiLiveSessionCommandRequest(RuntimeInputCommandKind.FinishPhase));
        Assert.NotEqual(RuntimeSessionPhaseKind.InstructionPrep, firstWork.CurrentPhaseKind);
        AssertTransferContract(firstWork, definition);
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

    private static async Task SeedEligibleL4StateAsync(
        AppStartupConfiguration configuration,
        BranchCode requestedBranch)
    {
        var statuses = Enum.GetValues<BranchCode>()
            .Select(branch => new BranchLevelStatus(
                branch,
                GlobalLevelId.L3,
                BranchLevelState.Maintenance))
            .Append(new BranchLevelStatus(
                requestedBranch,
                GlobalLevelId.L4,
                BranchLevelState.TestReady))
            .ToArray();
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

    private static void AssertTransferContract(
        PreUiLiveSessionState state,
        TransferTestDefinition definition)
    {
        Assert.Contains(state.CurrentMaterials, material =>
            material.Kind == "TransferTask" && material.Value == definition.TransferTask);
        Assert.Contains(state.CurrentMaterials, material =>
            material.Kind == "SameDemand" && material.Value == definition.SameDemand);
        Assert.Contains(state.CurrentMaterials, material =>
            material.Kind == "ChangedContext" && material.Value == definition.ChangedContext);
        Assert.Contains(state.CurrentMaterials, material =>
            material.Kind == "SourceBranchStandard" &&
            material.Value.Contains("visible in the transfer artifact", StringComparison.OrdinalIgnoreCase));
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

    private static string SourceRelations(IEnumerable<GeneratedContentMaterial> materials)
    {
        return string.Join("; ", materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedMapping)
            .Select(material => MappingSegment(material.Value, "expected source relation ")));
    }

    private static string MappingAnswers(IEnumerable<GeneratedContentMaterial> materials)
    {
        return string.Join("; ", materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedMapping)
            .Select(material =>
            {
                var index = Regex.Match(material.Name, @"(\d+)$").Value;
                var source = MappingSegment(material.Value, "expected source relation ");
                var target = MappingSegment(material.Value, "expected target relation ");
                var evidence = MappingSegment(material.Value, "mapping evidence ");
                return $"{index}={source} -> {target} because {evidence}";
            }));
    }

    private static string MappingSegment(string value, string marker)
    {
        var start = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) + marker.Length;
        var end = value.IndexOf(';', start);
        return (end < 0 ? value[start..] : value[start..end]).Trim().TrimEnd('.');
    }
}
