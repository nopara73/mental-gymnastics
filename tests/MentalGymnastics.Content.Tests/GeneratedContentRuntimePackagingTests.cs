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
    public void DistractorHoldPackageKeepsTimedHoldAndSchedulesNoResponseInterruptions()
    {
        var generated = FocusHoldGeneratedContentGenerator.Generate(
            CreateDistractorHoldRequest(),
            new GeneratedContentSeed("fh2-runtime-package"));
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == BranchCode.FH &&
            item.Level == GlobalLevelId.L3);

        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            generated.Materials,
            standard);

        Assert.Collection(
            package.Phases,
            phase => Assert.Equal(GeneratedRuntimePhaseKind.InstructionPrep, phase.Kind),
            phase =>
            {
                Assert.Equal(GeneratedRuntimePhaseKind.ActiveWork, phase.Kind);
                Assert.Equal(GeneratedRuntimePhaseCompletionRule.Timed, phase.CompletionRule);
                Assert.Equal(TimeSpan.FromMinutes(5), phase.ScheduledDuration);
            },
            phase => Assert.Equal(GeneratedRuntimePhaseKind.Review, phase.Kind));
        Assert.NotEmpty(package.Cues);
        Assert.All(package.Cues, cue =>
        {
            Assert.Equal(GeneratedRuntimeCueKind.Interruption, cue.Kind);
            Assert.Equal(GeneratedRuntimeCueResponseExpectation.NoResponseExpected, cue.ResponseExpectation);
            Assert.Null(cue.ExpectedResponse);
            Assert.InRange(cue.ScheduledAtOffset, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        });
        Assert.Equal(
            generated.Materials.Count(material => material.Kind == GeneratedContentMaterialKind.DistractorPrompt),
            package.Cues.Count);
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
            Assert.DoesNotContain("valid cue", cue.Cue, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lure", cue.Cue, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void GoNoGoPackagePreservesWithholdAndCanonicalResponseActions()
    {
        var generated = InhibitionGeneratedContentGenerator.Generate(
            CreateGoNoGoRequest(),
            new GeneratedContentSeed("ir-runtime-package"));
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == BranchCode.IR &&
            item.Level == GlobalLevelId.L1);

        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            generated.Materials,
            standard);

        var noGoCues = package.Cues
            .Where(cue => cue.Value.StartsWith("no-go:", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var goCues = package.Cues
            .Where(cue => cue.Value.StartsWith("go:", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(noGoCues);
        Assert.NotEmpty(goCues);
        Assert.All(noGoCues, cue =>
        {
            Assert.Equal(GeneratedRuntimeCueResponseExpectation.NoResponseExpected, cue.ResponseExpectation);
            Assert.Null(cue.ExpectedResponse);
            Assert.DoesNotContain("cue id", cue.Value, StringComparison.OrdinalIgnoreCase);
        });
        Assert.All(goCues, cue =>
        {
            Assert.Equal(GeneratedRuntimeCueResponseExpectation.ResponseRequired, cue.ResponseExpectation);
            Assert.Equal("tap", cue.ExpectedResponse);
            Assert.DoesNotContain("pace", cue.Value, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ExceptionRuleCueStepsPackageAsExecutableTimedResponses()
    {
        var generated = InhibitionGeneratedContentGenerator.Generate(
            CreateExceptionRuleRequest(),
            new GeneratedContentSeed("ir2-runtime-package"));
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == BranchCode.IR &&
            item.Level == GlobalLevelId.L2);

        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            generated.Materials,
            standard);

        Assert.Equal(8, package.Cues.Count);
        Assert.Equal(TimeSpan.FromSeconds(2), package.Cues[0].ScheduledAtOffset);
        Assert.Equal(TimeSpan.FromSeconds(16), package.Cues[^1].ScheduledAtOffset);
        Assert.All(package.Cues, cue =>
        {
            Assert.Equal(GeneratedRuntimeCueKind.TimedResponse, cue.Kind);
            Assert.DoesNotContain("cue id", cue.Value, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(package.Cues, cue =>
            cue.ResponseExpectation == GeneratedRuntimeCueResponseExpectation.ResponseRequired &&
            cue.ExpectedResponse == "tap");
        Assert.Contains(package.Cues, cue =>
            cue.ResponseExpectation == GeneratedRuntimeCueResponseExpectation.NoResponseExpected &&
            cue.ExpectedResponse is null);
    }

    [Fact]
    public void PressureRepeatPackageExecutesItsFoundationalSourceDrill()
    {
        var generated = AffectiveInterferenceGeneratedContentGenerator.Generate(
            CreatePressureRepeatRequest(),
            new GeneratedContentSeed("ai1-runtime-package"));
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == BranchCode.AI && item.Level == GlobalLevelId.L1);

        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            generated.Materials,
            standard);

        Assert.Equal(DrillId.FH2DistractorHold, package.SourceDrill);
        Assert.Collection(
            package.Phases,
            phase => Assert.Equal(GeneratedRuntimePhaseKind.InstructionPrep, phase.Kind),
            phase =>
            {
                Assert.Equal(GeneratedRuntimePhaseKind.ActiveWork, phase.Kind);
                Assert.Equal(TimeSpan.FromMinutes(5), phase.ScheduledDuration);
            },
            phase => Assert.Equal(GeneratedRuntimePhaseKind.Review, phase.Kind));
        Assert.NotEmpty(package.Cues);
        Assert.All(package.Cues, cue =>
            Assert.Equal(GeneratedRuntimeCueResponseExpectation.NoResponseExpected, cue.ResponseExpectation));
        Assert.Contains(package.InputMaterials, material =>
            material.Kind == GeneratedContentMaterialKind.TargetStatement);
    }

    [Fact]
    public void DisruptionRecoveryPackageInterruptsThenContinuesItsSourceCueStream()
    {
        var generated = AffectiveInterferenceGeneratedContentGenerator.Generate(
            CreateDisruptionRecoveryRequest(),
            new GeneratedContentSeed("ai2-runtime-package"));
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == BranchCode.AI && item.Level == GlobalLevelId.L3);

        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            generated.Materials,
            standard);

        Assert.Equal(DrillId.FS2InvalidCueFilter, package.SourceDrill);
        Assert.Collection(
            package.Phases,
            phase => Assert.Equal(GeneratedRuntimePhaseKind.InstructionPrep, phase.Kind),
            phase => Assert.Equal(GeneratedRuntimePhaseKind.CueResponse, phase.Kind),
            phase => Assert.Equal(GeneratedRuntimePhaseKind.Review, phase.Kind));
        var disruption = Assert.Single(package.Cues, cue => cue.Id == "controlled-disruption");
        Assert.Equal(GeneratedRuntimeCueKind.Interruption, disruption.Kind);
        Assert.Equal(GeneratedRuntimeCueResponseExpectation.ResponseRequired, disruption.ResponseExpectation);
        Assert.Equal("resume", disruption.ExpectedResponse);
        Assert.Equal(TimeSpan.FromSeconds(30), disruption.ResponseWindow);
        Assert.Contains(package.Cues, cue => cue.ScheduledAtOffset < disruption.ScheduledAtOffset);
        Assert.Contains(package.Cues, cue => cue.ScheduledAtOffset > disruption.ScheduledAtOffset);
    }

    [Fact]
    public void GlobalReviewPackageOrdersWorkAuditDelayAndReconstruction()
    {
        var generated = TransferIntegrationGeneratedContentGenerator.Generate(
            CreateGlobalReviewRequest(),
            new GeneratedContentSeed("ti2-runtime-package"));
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == BranchCode.TI &&
            item.Level == GlobalLevelId.L5);

        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            generated.Materials,
            standard);

        Assert.Collection(
            package.Phases,
            phase => Assert.Equal(GeneratedRuntimePhaseKind.InstructionPrep, phase.Kind),
            phase =>
            {
                Assert.Equal(GeneratedRuntimePhaseKind.ActiveWork, phase.Kind);
                Assert.Equal(GeneratedRuntimePhaseCompletionRule.ManualOrTimed, phase.CompletionRule);
                Assert.Equal(TimeSpan.FromMinutes(20), phase.ScheduledDuration);
            },
            phase => Assert.Equal(GeneratedRuntimePhaseKind.Audit, phase.Kind),
            phase =>
            {
                Assert.Equal(GeneratedRuntimePhaseKind.DelayWindow, phase.Kind);
                Assert.Equal(TimeSpan.FromMinutes(5), phase.ScheduledDuration);
            },
            phase => Assert.Equal(GeneratedRuntimePhaseKind.ReconstructionInput, phase.Kind),
            phase => Assert.Equal(GeneratedRuntimePhaseKind.Review, phase.Kind));
    }

    [Fact]
    public void SeededAuditPackageEnforcesDelayBeforeOriginalIsAudited()
    {
        var generated = DiscriminationGeneratedContentGenerator.Generate(
            CreateSeededAuditRequest(),
            new GeneratedContentSeed("de2-runtime-package"));
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == BranchCode.DE &&
            item.Level == GlobalLevelId.L3);

        var package = GeneratedContentRuntimePackager.Package(
            generated.Result,
            generated.Materials,
            standard);

        Assert.Collection(
            package.Phases,
            phase => Assert.Equal(GeneratedRuntimePhaseKind.InstructionPrep, phase.Kind),
            phase =>
            {
                Assert.Equal(GeneratedRuntimePhaseKind.DelayWindow, phase.Kind);
                Assert.Equal(TimeSpan.FromMinutes(5), phase.ScheduledDuration);
            },
            phase => Assert.Equal(GeneratedRuntimePhaseKind.Audit, phase.Kind),
            phase => Assert.Equal(GeneratedRuntimePhaseKind.Review, phase.Kind));
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

    private static GeneratedDrillContentRequest CreateDistractorHoldRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.FH,
            GlobalLevelId.L3,
            DrillId.FH2DistractorHold,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "fh-l3-distractor-hold",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("duration", "5 minutes"),
                new LoadVariable("target subtlety", "simple phrase"),
                new LoadVariable("recovery window", "10 seconds"),
                new LoadVariable("distractor frequency", "periodic"),
                new LoadVariable("distractor salience", "low"),
            ],
            [
                new CriticalConstraint(TargetAndDriftConstraint),
                new CriticalConstraint(NoTargetSubstitutionConstraint),
                new CriticalConstraint("Do not respond to distractor unless drill says so."),
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

    private static GeneratedDrillContentRequest CreateGoNoGoRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.IR,
            GlobalLevelId.L1,
            DrillId.IR1GoNoGoRule,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "ir-l1-go-no-go",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("cue conflict", "simple go/no-go symbols"),
                new LoadVariable("response speed", "2 seconds"),
                new LoadVariable("no-go frequency", "every third cue"),
            ],
            [new CriticalConstraint("Premature response fails item.")]);
    }

    private static GeneratedDrillContentRequest CreateExceptionRuleRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.IR,
            GlobalLevelId.L2,
            DrillId.IR2ExceptionRule,
            SessionType.Practice,
            PromptContentKind.CueSequence,
            "ir-l2-exception-rule",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("exception count", "3"),
                new LoadVariable("response speed", "2 seconds"),
                new LoadVariable("similarity", "near symbols"),
            ],
            [new CriticalConstraint("Rule and exceptions stated before set.")]);
    }

    private static GeneratedDrillContentRequest CreateGlobalReviewRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.TI,
            GlobalLevelId.L5,
            DrillId.TI2GlobalReviewTask,
            SessionType.Test,
            PromptContentKind.EquivalentPrompt,
            "ti-l5-global-review",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("task length", "20 minutes"),
                new LoadVariable("pressure", "visible review pressure"),
                new LoadVariable("ambiguity", "moderate ambiguity"),
                new LoadVariable("delay", "5 minutes"),
                new LoadVariable("number of branches", "4"),
            ],
            [
                new CriticalConstraint("Audit and delayed reconstruction are required."),
                new CriticalConstraint("No rereading after encode window."),
            ]);
    }

    private static GeneratedDrillContentRequest CreatePressureRepeatRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.AI,
            GlobalLevelId.L1,
            DrillId.AI1PressureRepeat,
            SessionType.Practice,
            PromptContentKind.EquivalentPrompt,
            "ai-l1-pressure-repeat-fh-l3",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("time pressure", "90 seconds"),
                new LoadVariable("observation", "visible evaluator note"),
            ],
            [new CriticalConstraint("Original standard cannot be lowered.")]);
    }

    private static GeneratedDrillContentRequest CreateDisruptionRecoveryRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.AI,
            GlobalLevelId.L3,
            DrillId.AI2DisruptionRecovery,
            SessionType.Practice,
            PromptContentKind.EquivalentPrompt,
            "ai-l3-disruption-recovery-fs-l3",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("interruption timing", "mid-task after first checkpoint"),
                new LoadVariable("restart delay", "10 seconds"),
                new LoadVariable("task complexity", "two-target cue sequence"),
                new LoadVariable("recovery window", "30 seconds"),
            ],
            [new CriticalConstraint("Full restart prohibited unless specified.")]);
    }

    private static GeneratedDrillContentRequest CreateSeededAuditRequest()
    {
        return new GeneratedDrillContentRequest(
            BranchCode.DE,
            GlobalLevelId.L3,
            DrillId.DE2SeededAudit,
            SessionType.Practice,
            PromptContentKind.DiscriminationItemSet,
            "de-l3-seeded-audit",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            [
                new LoadVariable("error subtlety", "subtle wording errors"),
                new LoadVariable("output length", "6 lines"),
                new LoadVariable("audit delay", "5 minutes"),
                new LoadVariable("quantity", "3"),
            ],
            [new CriticalConstraint("Original output cannot be edited during audit.")]);
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
