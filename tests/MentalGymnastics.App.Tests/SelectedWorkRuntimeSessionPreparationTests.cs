using MentalGymnastics.App;
using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App.Tests;

public sealed class SelectedWorkRuntimeSessionPreparationTests
{
    [Fact]
    public void PreparesFoundationalCueRuntimeSessionFromSelectedWorkAndGeneratedContent()
    {
        var selectedWork = SelectedWork(
            BranchCode.FS,
            GlobalLevelId.L1,
            DrillId.FS1CueSwitch,
            AppTrainingSessionType.Practice,
            [
                new LoadVariable("target count", "2"),
                new LoadVariable("switch count", "4"),
                new LoadVariable("cue density", "5 seconds"),
                new LoadVariable("return precision", "next cue"),
            ]);
        var generatedContent = PrepareGeneratedContent(
            selectedWork,
            PromptContentKind.CueSequence,
            "fs-l1-cue-switch",
            "runtime-prep-fs-foundational",
            [new CriticalConstraint("No anticipatory switching.")]);

        var prepared = new SelectedWorkRuntimeSessionPreparer().Prepare(
            new SelectedWorkRuntimeSessionPreparationRequest(
                "session-fs-l1-cue-switch",
                generatedContent));

        Assert.Equal(SelectedWorkRuntimeSessionPreparationStatus.Prepared, prepared.Status);
        Assert.True(prepared.IsPrepared);
        Assert.Empty(prepared.Rejections);
        Assert.Equal("session-fs-l1-cue-switch", prepared.SessionId);
        Assert.Equal(selectedWork, prepared.SelectedWork);
        Assert.NotNull(prepared.SessionDefinition);
        Assert.NotNull(prepared.PhasePlan);
        Assert.NotNull(prepared.CueSchedule);
        Assert.True(prepared.CanRenderLiveSession);
        Assert.False(prepared.OwnsTiming);
        Assert.False(prepared.OwnsCueScheduling);
        Assert.False(prepared.OwnsScoring);

        Assert.Equal(SessionType.Practice, prepared.SessionDefinition.SessionType);
        Assert.Equal(BranchCode.FS, prepared.SessionDefinition.Branch);
        Assert.Equal(GlobalLevelId.L1, prepared.SessionDefinition.Level);
        Assert.Equal(DrillId.FS1CueSwitch, prepared.SessionDefinition.Drill);
        Assert.Equal(selectedWork.LoadVariables, prepared.SessionDefinition.LoadVariables);
        Assert.Equal(selectedWork.Standard, prepared.SessionDefinition.Standard.Standard);
        Assert.Contains(
            prepared.SessionDefinition.CriticalConstraints,
            constraint => constraint.Description == selectedWork.HonestyConstraint);
        Assert.Contains(
            prepared.SessionDefinition.CriticalConstraints,
            constraint => constraint.Description == "No anticipatory switching.");

        Assert.Equal(
            generatedContent.GeneratedContent!.Result.InstanceId,
            prepared.SessionDefinition.GeneratedDrillInstance!.InstanceId);
        Assert.Equal(
            generatedContent.GeneratedContent.Result.ContentId,
            prepared.SessionDefinition.GeneratedDrillInstance.ContentIdentity.ContentId);
        Assert.Equal(
            prepared.SessionDefinition.GeneratedDrillInstance.InstanceId,
            prepared.CueSchedule.GeneratedDrillInstance.InstanceId);

        Assert.Collection(
            prepared.PhasePlan.Phases,
            phase => Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, phase.Kind),
            phase => Assert.Equal(RuntimeSessionPhaseKind.CueResponse, phase.Kind),
            phase => Assert.Equal(RuntimeSessionPhaseKind.Review, phase.Kind));
        Assert.NotEmpty(prepared.CueSchedule.Cues);
        Assert.All(prepared.CueSchedule.Cues, cue =>
        {
            Assert.Equal(RuntimeCueKind.FocusShift, cue.Kind);
            Assert.Equal(RuntimeCueResponseExpectation.ResponseRequired, cue.ResponseExpectation);
            Assert.True(cue.ScheduledAt.Offset > TimeSpan.Zero);
            Assert.True(cue.ResponseWindow.Value > TimeSpan.Zero);
        });
        Assert.Contains(
            prepared.InputMaterials,
            material => material.Kind == GeneratedContentMaterialKind.TargetSet);
        Assert.Contains(
            prepared.ExpectedEvidenceFacts,
            fact => fact.Value.Contains("valid cue responses", StringComparison.OrdinalIgnoreCase));

        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var handler = RuntimeInputCommandHandler.Start(
            prepared.SessionId,
            prepared.SessionDefinition,
            prepared.PhasePlan,
            clock);

        Assert.Equal(RuntimeSessionLifecycleStatus.Running, handler.LifecycleState.Status);
        Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, handler.CurrentPhase?.Kind);
    }

    [Fact]
    public void PreparesAdvancedRuleRuntimeSessionWithoutCueScheduleOrProgressionDecision()
    {
        var selectedWork = SelectedWork(
            BranchCode.CO,
            GlobalLevelId.L1,
            DrillId.CO1RuleExtraction,
            AppTrainingSessionType.Practice,
            [
                new LoadVariable("rule ambiguity", "clear examples"),
                new LoadVariable("example count", "8"),
            ]);
        var generatedContent = PrepareGeneratedContent(
            selectedWork,
            PromptContentKind.RuleExampleSet,
            "co-l1-rule-extraction",
            "runtime-prep-co-advanced");

        var prepared = new SelectedWorkRuntimeSessionPreparer().Prepare(
            new SelectedWorkRuntimeSessionPreparationRequest(
                "session-co-l1-rule-extraction",
                generatedContent));

        Assert.Equal(SelectedWorkRuntimeSessionPreparationStatus.Prepared, prepared.Status);
        Assert.NotNull(prepared.SessionDefinition);
        Assert.NotNull(prepared.PhasePlan);
        Assert.Null(prepared.CueSchedule);
        Assert.Equal(BranchCode.CO, prepared.SessionDefinition.Branch);
        Assert.Equal(GlobalLevelId.L1, prepared.SessionDefinition.Level);
        Assert.Equal(DrillId.CO1RuleExtraction, prepared.SessionDefinition.Drill);
        Assert.Equal(selectedWork.LoadVariables, prepared.SessionDefinition.LoadVariables);
        Assert.Equal(selectedWork.Standard, prepared.SessionDefinition.Standard.Standard);
        Assert.Equal(
            generatedContent.GeneratedContent!.Result.InstanceId,
            prepared.SessionDefinition.GeneratedDrillInstance!.InstanceId);
        Assert.Contains(
            prepared.PhasePlan.Phases,
            phase => phase.Kind == RuntimeSessionPhaseKind.ActiveWork);
        Assert.Contains(
            prepared.InputMaterials,
            material => material.Kind == GeneratedContentMaterialKind.UnseenExample);
        Assert.Contains(
            prepared.InputMaterials,
            material => material.Kind == GeneratedContentMaterialKind.RuleStatement);
        Assert.Contains(
            prepared.ExpectedEvidenceFacts,
            fact => fact.Value.Contains("rule", StringComparison.OrdinalIgnoreCase));
        Assert.False(prepared.GrantsAdvancement);
    }

    [Fact]
    public void PreparesAffectiveInterferenceWithExecutableSourceTaskAndDisruptionCue()
    {
        var selectedWork = SelectedWork(
            BranchCode.AI,
            GlobalLevelId.L3,
            DrillId.AI2DisruptionRecovery,
            AppTrainingSessionType.Practice,
            [
                new LoadVariable("interruption timing", "mid-task after first checkpoint"),
                new LoadVariable("restart delay", "10 seconds"),
                new LoadVariable("task complexity", "two-target cue sequence"),
                new LoadVariable("recovery window", "30 seconds"),
            ]);
        var generatedContent = PrepareGeneratedContent(
            selectedWork,
            PromptContentKind.EquivalentPrompt,
            "ai-l3-disruption-recovery-fs-l3",
            "runtime-prep-ai2");

        var prepared = new SelectedWorkRuntimeSessionPreparer().Prepare(
            new SelectedWorkRuntimeSessionPreparationRequest(
                "session-ai-l3-disruption",
                generatedContent));

        Assert.True(prepared.IsPrepared);
        Assert.Equal(DrillId.FS2InvalidCueFilter, prepared.GeneratedContent.RuntimePackage!.SourceDrill);
        Assert.Equal(DrillId.FS2InvalidCueFilter, prepared.SessionDefinition!.SourceDrill);
        Assert.Contains(prepared.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.TargetSet);
        Assert.Contains(prepared.InputMaterials, material =>
            material.Kind is GeneratedContentMaterialKind.ValidCue or GeneratedContentMaterialKind.InvalidCue);
        Assert.NotNull(prepared.CueSchedule);
        Assert.Contains(prepared.CueSchedule.Cues, cue =>
            cue.Id == "controlled-disruption" &&
            cue.Kind == RuntimeCueKind.Interruption &&
            cue.ExpectedResponse == "resume");
        Assert.Collection(
            prepared.PhasePlan!.Phases,
            phase => Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, phase.Kind),
            phase => Assert.Equal(RuntimeSessionPhaseKind.CueResponse, phase.Kind),
            phase => Assert.Equal(RuntimeSessionPhaseKind.Review, phase.Kind));
    }

    private static SelectedWorkGeneratedContentPreparationResult PrepareGeneratedContent(
        SelectedTrainingWork selectedWork,
        PromptContentKind contentKind,
        string equivalenceClass,
        string seed,
        IEnumerable<CriticalConstraint>? additionalCriticalConstraints = null)
    {
        var result = new SelectedWorkGeneratedContentPreparer().Prepare(
            new SelectedWorkGeneratedContentPreparationRequest(
                selectedWork,
                contentKind,
                equivalenceClass,
                PromptFreshnessPolicy.FreshEquivalentRequired,
                new GeneratedContentSeed(seed),
                TrainingDate.From(2026, 7, 5),
                additionalCriticalConstraints: additionalCriticalConstraints));

        Assert.True(result.IsPrepared, string.Join("; ", result.Rejections.Select(rejection => rejection.Detail)));
        return result;
    }

    private static SelectedTrainingWork SelectedWork(
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        AppTrainingSessionType sessionType,
        IEnumerable<LoadVariable> loadVariables)
    {
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == branch &&
            item.Level == level);
        var drillDefinition = ProgramCatalog.Drills.Single(item => item.Id == drill);

        return new SelectedTrainingWork(
            branch,
            level,
            drill,
            sessionType,
            standard.Demand,
            standard.Standard,
            drillDefinition.HonestyConstraint,
            loadVariables,
            advancementWorkAllowed: true);
    }
}
