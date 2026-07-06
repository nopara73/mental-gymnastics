using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Content.Tests;

public sealed class GeneratedContentRuntimePackagingTests
{
    [Fact]
    public void FocusHoldTargetHoldPackageUsesGeneratedHoldDurationAsTimedActiveWork()
    {
        var generated = FocusHoldGeneratedContentGenerator.Generate(
            CreateTargetHoldRequest(),
            new GeneratedContentSeed("fh-runtime-package"));
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == BranchCode.FH &&
            item.Level == GlobalLevelId.L1);

        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            generated.Materials,
            standard);

        Assert.True(package.CanBeConsumedByRuntime);
        Assert.False(package.RuntimeInventsContent);
        Assert.False(package.GrantsAdvancement);
        Assert.Empty(package.Cues);
        Assert.Collection(
            package.Phases,
            phase =>
            {
                Assert.Equal(GeneratedRuntimePhaseKind.InstructionPrep, phase.Kind);
                Assert.Equal(GeneratedRuntimePhaseCompletionRule.Manual, phase.CompletionRule);
            },
            phase =>
            {
                Assert.Equal("active-work", phase.Id);
                Assert.Equal(GeneratedRuntimePhaseKind.ActiveWork, phase.Kind);
                Assert.Equal(GeneratedRuntimePhaseCompletionRule.Timed, phase.CompletionRule);
                Assert.Equal(TimeSpan.FromMinutes(3), phase.ScheduledDuration);
            },
            phase =>
            {
                Assert.Equal(GeneratedRuntimePhaseKind.Review, phase.Kind);
                Assert.Equal(GeneratedRuntimePhaseCompletionRule.Manual, phase.CompletionRule);
            });
        Assert.Contains(package.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.HoldDuration &&
            material.Value == "3 minutes");
        Assert.Contains(package.ExpectedEvidenceFacts, fact =>
            fact.Value.Contains("drift", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CueSequencePackageCanBeConsumedByRuntimeWithoutRuntimeInventingContent()
    {
        var generated = FocusShiftGeneratedContentGenerator.Generate(
            CreateCueSwitchRequest(),
            new GeneratedContentSeed("fs-runtime-package"));
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == BranchCode.FS &&
            item.Level == GlobalLevelId.L1);

        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            generated.Materials,
            standard);

        Assert.True(package.CanBeConsumedByRuntime);
        Assert.False(package.RuntimeInventsContent);
        Assert.False(package.GrantsAdvancement);
        Assert.Equal(generated.Result.Instance, package.GeneratedInstance);
        Assert.Equal(generated.Result.Branch, package.Branch);
        Assert.Equal(generated.Result.Level, package.Level);
        Assert.Equal(generated.Result.Drill, package.Drill);
        Assert.Equal(generated.Result.Request.LoadVariables, package.LoadVariables);
        Assert.Equal(generated.Result.Request.CriticalConstraints, package.CriticalConstraints);
        Assert.Equal(standard, package.Standard);

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
        var phasePlan = new RuntimeSessionPhasePlan(package.Phases.Select(ToRuntimePhase));
        var cueSchedule = new RuntimeCueSchedule(runtimeIdentity, package.Cues.Select(ToRuntimeCue));

        Assert.Equal(generated.Result.InstanceId, runtimeDefinition.GeneratedDrillInstance!.InstanceId);
        Assert.Equal(generated.Result.ContentId, runtimeDefinition.GeneratedDrillInstance.ContentIdentity.ContentId);
        Assert.Equal(generated.Result.ContentVersion, runtimeDefinition.GeneratedDrillInstance.ContentVersion);
        Assert.Equal(standard.Standard, runtimeDefinition.Standard.Standard);
        Assert.Contains(runtimeDefinition.CriticalConstraints, constraint =>
            constraint.Description == ValidCueConstraint);
        Assert.Contains(runtimeDefinition.CriticalConstraints, constraint =>
            constraint.Description == NoAnticipatorySwitchingConstraint);

        Assert.Collection(
            phasePlan.Phases,
            phase => Assert.Equal(RuntimeSessionPhaseKind.InstructionPrep, phase.Kind),
            phase => Assert.Equal(RuntimeSessionPhaseKind.CueResponse, phase.Kind),
            phase => Assert.Equal(RuntimeSessionPhaseKind.Review, phase.Kind));
        Assert.Equal(4, cueSchedule.Cues.Count);
        Assert.All(cueSchedule.Cues, cue =>
        {
            Assert.Equal(RuntimeCueKind.FocusShift, cue.Kind);
            Assert.Equal(RuntimeCueResponseExpectation.ResponseRequired, cue.ResponseExpectation);
            Assert.Equal(TimeSpan.FromSeconds(2), cue.ResponseWindow.Value);
        });
        Assert.Equal(TimeSpan.FromSeconds(5), cueSchedule.Cues[0].ScheduledAt.Offset);
        Assert.Equal(TimeSpan.FromSeconds(20), cueSchedule.Cues[^1].ScheduledAt.Offset);

        var firstExpectedTarget = Assert.Single(package.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.ExpectedActiveTarget &&
            material.Name == "expected-target-1");
        Assert.Equal(firstExpectedTarget.Value, cueSchedule.Cues[0].ExpectedResponse);
        Assert.Contains(package.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.TargetSet);
        Assert.Contains(package.ExpectedEvidenceFacts, fact =>
            fact.Name == "sequence-accuracy-evidence" &&
            fact.Value.Contains("valid cue responses", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(package.ExpectedEvidenceFacts, fact =>
            fact.Name == "uncued-switch-policy" &&
            fact.Value.Contains("invalid", StringComparison.OrdinalIgnoreCase));

        var clock = new ManualRuntimeClock(RuntimeInstant.Zero);
        var log = RuntimeEventLog.Start("fs-runtime-packaged-session", runtimeDefinition, clock.Now);
        var scheduler = new RuntimeCueScheduler(cueSchedule, clock, log);
        var cuePhase = phasePlan.Phases.Single(phase => phase.Kind == RuntimeSessionPhaseKind.CueResponse);

        clock.AdvanceBy(RuntimeDuration.FromSeconds(5));
        var firstCue = scheduler.AdvanceToCurrentTime(cuePhase);

        Assert.Collection(firstCue.EmittedCues, cue =>
        {
            Assert.Equal(cueSchedule.Cues[0].Id, cue.Id);
            Assert.Equal(cueSchedule.Cues[0].ExpectedResponse, cue.ExpectedResponse);
        });
        Assert.Contains(log.Events[^1].Facts, fact =>
            fact.Name == "generated_instance_id" &&
            fact.Value == generated.Result.InstanceId);
        Assert.Contains(log.Events[^1].Facts, fact =>
            fact.Name == "content_id" &&
            fact.Value == generated.Result.ContentId);
    }

    private const string ValidCueConstraint = "Switch only on valid cue.";
    private const string NoAnticipatorySwitchingConstraint = "No anticipatory switching.";
    private const string TargetAndDriftConstraint = "Target is stated before set; every drift is marked.";
    private const string NoTargetSubstitutionConstraint = "No target substitution.";

    private static GeneratedDrillContentRequest CreateTargetHoldRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.FH,
            GlobalLevelId.L1,
            DrillId.FH1TargetHold,
            SessionType.Practice,
            PromptContentKind.EquivalentPrompt,
            "fh-l1-target-hold",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("duration", "3 minutes"),
                new LoadVariable("target subtlety", "simple phrase"),
                new LoadVariable("recovery window", "10 seconds"),
            ],
            [
                new CriticalConstraint(TargetAndDriftConstraint),
                new CriticalConstraint(NoTargetSubstitutionConstraint),
            ]);
    }

    private static GeneratedDrillContentRequest CreateCueSwitchRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.FS,
            GlobalLevelId.L1,
            DrillId.FS1CueSwitch,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "fs-l1-cue-switch",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("target count", "2"),
                new LoadVariable("switch count", "4"),
                new LoadVariable("cue density", "5 seconds"),
                new LoadVariable("return precision", "next cue"),
            ],
            [
                new CriticalConstraint(ValidCueConstraint),
                new CriticalConstraint(NoAnticipatorySwitchingConstraint),
            ]);
    }

    private static RuntimeSessionPhaseDefinition ToRuntimePhase(GeneratedRuntimePhaseDefinition phase)
    {
        var kind = Enum.Parse<RuntimeSessionPhaseKind>(phase.Kind.ToString());
        var duration = phase.ScheduledDuration.HasValue
            ? new RuntimeDuration(phase.ScheduledDuration.Value)
            : RuntimeDuration.Zero;

        return phase.CompletionRule switch
        {
            GeneratedRuntimePhaseCompletionRule.Manual => RuntimeSessionPhaseDefinition.Manual(phase.Id, kind),
            GeneratedRuntimePhaseCompletionRule.Timed => RuntimeSessionPhaseDefinition.Timed(phase.Id, kind, duration),
            GeneratedRuntimePhaseCompletionRule.ManualOrTimed => RuntimeSessionPhaseDefinition.ManualOrTimed(phase.Id, kind, duration),
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase.CompletionRule, "Unknown phase completion rule."),
        };
    }

    private static RuntimeScheduledCue ToRuntimeCue(GeneratedRuntimeCueDefinition cue)
    {
        var cueKind = Enum.Parse<RuntimeCueKind>(cue.Kind.ToString());
        var expectation = cue.ResponseExpectation == GeneratedRuntimeCueResponseExpectation.ResponseRequired
            ? RuntimeCueResponseExpectation.ResponseRequired
            : RuntimeCueResponseExpectation.NoResponseExpected;

        return new RuntimeScheduledCue(
            cue.Id,
            cueKind,
            cue.Value,
            new RuntimeInstant(cue.ScheduledAtOffset),
            new RuntimeDuration(cue.ResponseWindow),
            expectation,
            cue.ExpectedResponse);
    }
}
