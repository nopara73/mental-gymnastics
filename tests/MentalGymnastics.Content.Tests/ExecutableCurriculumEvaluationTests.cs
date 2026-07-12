using System.Globalization;
using System.Text.RegularExpressions;
using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Content.Tests;

public sealed class ExecutableCurriculumEvaluationTests
{
    [Fact]
    public void EveryFullStandardHasAnObjectivelyPassingRuntimeEvidencePath()
    {
        var failures = new List<string>();
        foreach (var profile in TrainingLoadProfileCatalog.Profiles)
        {
            var (result, materials, package) = Generate(profile);
            var completion = IdealCompletion(result, package, materials);
            var handoff = RuntimeStandardEvaluationHandoffMapper.Map(
                completion,
                materials.Select(material => new RuntimeScoringMaterial(
                    material.Kind.ToString(),
                    material.Name,
                    material.Value)));
            var evaluated = StandardEvaluator.Evaluate(
                ExecutableStandardCatalog.Get(profile.Branch, profile.Level).EvaluatedStandard,
                new StandardEvaluationAttempt(
                    handoff.Measurements,
                    handoff.CriticalConstraintChecks,
                    handoff.OutputComplete,
                    handoff.RubricOutcome));
            if (!evaluated.Passed)
            {
                failures.Add(
                    $"{profile.Branch} {profile.Level}: " +
                    string.Join(", ", evaluated.Failures.Select(failure => failure.Detail)) +
                    "; measurements=" +
                    string.Join(",", handoff.Measurements.Select(item => $"{item.Name}={item.Value}")) +
                    "; constraints=" +
                    string.Join(",", handoff.CriticalConstraintChecks.Select(item => $"{item.Id}={item.Satisfied}")));
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static (
        GeneratedDrillContentResult Result,
        IReadOnlyList<GeneratedContentMaterial> Materials,
        GeneratedContentRuntimePackage Package) Generate(TrainingLoadProfile profile)
    {
        var protocol = DrillProtocolCatalog.StandardDrills.Single(item => item.Id == profile.Drill);
        var sessionType = profile.Level == GlobalLevelId.L4 ? SessionType.Transfer : SessionType.Test;
        var request = new GeneratedDrillContentRequest(
            profile.Branch,
            profile.Level,
            profile.Drill,
            sessionType,
            ContentKindFor(profile.Drill),
            $"evaluation-{profile.Branch}-{profile.Level}",
            PromptFreshnessPolicy.FreshEquivalentRequired,
            profile.TargetStage.LoadVariables,
            protocol.HonestyConstraints.Select(item => new CriticalConstraint(item.Description)));
        GeneratedDrillContentResult result;
        IReadOnlyList<GeneratedContentMaterial> materials;
        if (sessionType == SessionType.Transfer)
        {
            var capacity = ProgramCatalog.Drills.Single(item => item.Id == profile.Drill)
                .CapacityTrained.First();
            var generated = TransferGeneratedContentGenerator.Generate(
                new TransferContentGenerationRequest(request, capacity, "far transfer"),
                new GeneratedContentSeed($"evaluation-transfer-{profile.Branch}"));
            result = generated.Result;
            materials = generated.Materials;
        }
        else
        {
            var selected = GeneratedContentSelectionCoordinator.Select(
                GeneratedContentSelectionNeed.ForStandardContent(request),
                new GeneratedContentSeed($"evaluation-{profile.Branch}-{profile.Level}"));
            result = selected.Result;
            materials = selected.Materials;
        }

        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == profile.Branch && item.Level == profile.Level);
        return (
            result,
            materials,
            GeneratedContentRuntimePackager.Package(result, materials, standard));
    }

    private static RuntimeSessionCompletionResult IdealCompletion(
        GeneratedDrillContentResult result,
        GeneratedContentRuntimePackage package,
        IReadOnlyList<GeneratedContentMaterial> materials)
    {
        var executable = ExecutableStandardCatalog.Get(result.Branch, result.Level);
        var definition = new RuntimeSessionDefinition(
            result.SessionType,
            result.Branch,
            result.Level,
            result.Drill,
            result.Request.LoadVariables,
            package.Standard,
            result.Request.CriticalConstraints,
            new RuntimeGeneratedDrillInstanceIdentity(
                result.InstanceId,
                result.Instance.ContentIdentity,
                result.Instance.ContentVersion),
            package.SourceDrill);
        var phases = CompletedPhases(package, executable.EvaluatedStandard);
        var events = IdealEvents(package, materials);
        return new RuntimeSessionCompletionResult(
            "evaluation-session",
            definition,
            RuntimeSessionCompletionStatus.Completed,
            phases,
            events,
            scoringEvents: [],
            scoringFacts: [],
            evidenceDrafts: [],
            new RuntimeSessionEvidenceSummary(0, [], [], []),
            failureRelevantFacts: [],
            resultFacts: [],
            completedAt: new RuntimeInstant(TimeSpan.FromHours(1)));
    }

    private static IReadOnlyList<RuntimeCompletedSessionPhase> CompletedPhases(
        GeneratedContentRuntimePackage package,
        EvaluatedStandard standard)
    {
        var requiredActiveSeconds = standard.NumericThresholds
            .FirstOrDefault(threshold =>
                threshold.MeasurementName == TrainingStandardMeasurements.ActiveDurationSeconds &&
                threshold.Direction == NumericThresholdDirection.AtLeast)?.Value ?? 1;
        var activePhaseCount = Math.Max(1, package.Phases.Count(phase => phase.Kind is
            GeneratedRuntimePhaseKind.ActiveWork or
            GeneratedRuntimePhaseKind.CueResponse or
            GeneratedRuntimePhaseKind.Recovery));
        var cursor = TimeSpan.Zero;
        var completed = new List<RuntimeCompletedSessionPhase>();
        foreach (var phase in package.Phases)
        {
            var kind = Enum.Parse<RuntimeSessionPhaseKind>(phase.Kind.ToString());
            var duration = phase.ScheduledDuration ?? (phase.Kind is
                GeneratedRuntimePhaseKind.ActiveWork or
                GeneratedRuntimePhaseKind.CueResponse or
                GeneratedRuntimePhaseKind.Recovery
                    ? TimeSpan.FromSeconds((double)Math.Ceiling(requiredActiveSeconds / activePhaseCount))
                    : TimeSpan.FromSeconds(1));
            var definition = phase.CompletionRule switch
            {
                GeneratedRuntimePhaseCompletionRule.Manual =>
                    RuntimeSessionPhaseDefinition.Manual(phase.Id, kind),
                GeneratedRuntimePhaseCompletionRule.Timed =>
                    RuntimeSessionPhaseDefinition.Timed(phase.Id, kind, new RuntimeDuration(duration)),
                GeneratedRuntimePhaseCompletionRule.ManualOrTimed =>
                    RuntimeSessionPhaseDefinition.ManualOrTimed(phase.Id, kind, new RuntimeDuration(duration)),
                _ => throw new ArgumentOutOfRangeException(),
            };
            var started = new RuntimeInstant(cursor);
            cursor += duration;
            completed.Add(new RuntimeCompletedSessionPhase(
                definition,
                started,
                new RuntimeInstant(cursor),
                new RuntimeDuration(duration),
                phase.CompletionRule == GeneratedRuntimePhaseCompletionRule.Manual
                    ? RuntimeSessionPhaseCompletionCause.Explicit
                    : RuntimeSessionPhaseCompletionCause.Timeout));
        }

        return completed;
    }

    private static IReadOnlyList<RuntimeEvent> IdealEvents(
        GeneratedContentRuntimePackage package,
        IReadOnlyList<GeneratedContentMaterial> materials)
    {
        var events = new List<RuntimeEvent>();
        long sequence = 1;
        foreach (var phase in package.Phases)
        {
            var kind = Enum.Parse<RuntimeSessionPhaseKind>(phase.Kind.ToString());
            events.Add(Event(sequence++, RuntimeEventKind.PhaseStarted, phase.Id, kind));
            var answer = AnswerForPhase(package.SourceDrill ?? package.Drill, kind, materials);
            if (!string.IsNullOrWhiteSpace(answer))
            {
                events.Add(Event(
                    sequence++,
                    RuntimeEventKind.AnswerSubmitted,
                    phase.Id,
                    kind,
                    new RuntimeEventFact("answer_id", phase.Id),
                    new RuntimeEventFact("answer_reference", answer)));
            }
        }

        var cuePhase = package.Phases.FirstOrDefault(phase => phase.Kind is
            GeneratedRuntimePhaseKind.CueResponse or GeneratedRuntimePhaseKind.ActiveWork);
        foreach (var cue in package.Cues)
        {
            var phaseId = cuePhase?.Id ?? "cue-response";
            var phaseKind = cuePhase is null
                ? RuntimeSessionPhaseKind.CueResponse
                : Enum.Parse<RuntimeSessionPhaseKind>(cuePhase.Kind.ToString());
            events.Add(Event(
                sequence++,
                RuntimeEventKind.CueEmitted,
                phaseId,
                phaseKind,
                new RuntimeEventFact("cue_id", cue.Id),
                new RuntimeEventFact(
                    "response_expectation",
                    cue.ResponseExpectation == GeneratedRuntimeCueResponseExpectation.ResponseRequired
                        ? "response_required"
                        : "no_response_expected"),
                new RuntimeEventFact("cue_kind", cue.Kind.ToString().ToLowerInvariant())));
            if (cue.ResponseExpectation == GeneratedRuntimeCueResponseExpectation.ResponseRequired)
            {
                events.Add(Event(
                    sequence++,
                    RuntimeEventKind.CueResponseSubmitted,
                    phaseId,
                    phaseKind,
                    new RuntimeEventFact("cue_id", cue.Id),
                    new RuntimeEventFact("response_outcome", "correct"),
                    new RuntimeEventFact("cue_kind", cue.Kind == GeneratedRuntimeCueKind.Interruption
                        ? "interruption"
                        : cue.Kind.ToString().ToLowerInvariant()),
                    new RuntimeEventFact("response_time", "00:00:10")));
            }
        }

        return events;
    }

    private static string AnswerForPhase(
        DrillId drill,
        RuntimeSessionPhaseKind phase,
        IReadOnlyList<GeneratedContentMaterial> materials)
    {
        var parts = new List<string>();
        if (phase == RuntimeSessionPhaseKind.ActiveWork)
        {
            parts.Add("rule stated; relations named; assumption stated; prediction test result passed; confidence certain");
            parts.AddRange(ComponentAnswers(materials));
            if (drill is DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule)
            {
                parts.AddRange(materials
                    .Where(material => material.Kind is
                        GeneratedContentMaterialKind.RuleStatement or
                        GeneratedContentMaterialKind.ExceptionDefinition)
                    .Select(material => material.Value));
            }
            if (drill == DrillId.DE1PairDiscrimination)
            {
                parts.AddRange(PairAnswers(materials));
            }

            if (drill == DrillId.CO1RuleExtraction)
            {
                parts.AddRange(materials
                    .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedRule)
                    .Select(material => material.Value));
            }

            if (drill == DrillId.CO2StructureMapping)
            {
                parts.AddRange(materials
                    .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedMapping)
                    .Select(material => Segment(material.Value, "expected source relation ")));
            }
        }

        if (phase == RuntimeSessionPhaseKind.ReconstructionInput)
        {
            if (drill == DrillId.WM2MentalTransform)
            {
                parts.AddRange(materials
                    .Where(material => material.Kind == GeneratedContentMaterialKind.FinalExpectedOutput)
                    .Select(material => $"RESULT={material.Value}"));
                parts.Add("RULE=" + string.Join(
                    " then ",
                    materials
                        .Where(material => material.Kind == GeneratedContentMaterialKind.OperationStep)
                        .Select(material => material.Value)));
            }
            else
            {
                parts.AddRange(materials
                    .Where(material => material.Kind is
                        GeneratedContentMaterialKind.ExpectedReconstruction or
                        GeneratedContentMaterialKind.FinalExpectedOutput)
                    .Select(material => material.Value));
            }
            parts.AddRange(ClassificationAnswers(materials));
            parts.AddRange(MappingAnswers(materials));
            if (drill != DrillId.TI2GlobalReviewTask)
            {
                parts.AddRange(ComponentAnswers(materials));
            }
            if (drill == DrillId.TI1CompositeTask)
            {
                parts.Add("delayed artifact complete; reconstruction complete");
            }
        }

        if (phase == RuntimeSessionPhaseKind.Audit)
        {
            parts.AddRange(AuditAnswers(materials));
            parts.AddRange(materials
                .Where(material =>
                    material.Kind == GeneratedContentMaterialKind.ExpectedFinding &&
                    material.Name is "model-audit-key" or "global-review-audit-key")
                .Select(material => material.Value));
            if (drill == DrillId.CO2StructureMapping)
            {
                parts.Add("ASSUMPTION=evidence relation remains valid");
                parts.Add("TEST=held out relation evidence decides verdict");
            }
            if (drill != DrillId.TI2GlobalReviewTask)
            {
                parts.Add("audit complete with supported findings");
            }
        }

        return string.Join("; ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static IEnumerable<string> ComponentAnswers(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        foreach (var key in materials.Where(material =>
            material.Kind == GeneratedContentMaterialKind.BranchScoringKey))
        {
            var branch = Regex.Match(
                key.Value,
                @"(?:component\s+)?branch\s+([A-Z]{2})\b",
                RegexOptions.IgnoreCase).Groups[1].Value.ToUpperInvariant();
            var expected = Regex.Match(
                key.Value,
                @"expected\s+response\s+([^;]+)",
                RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            if (branch.Length > 0 && expected.Length > 0)
            {
                yield return $"{branch}={expected}";
            }
        }
    }

    private static IEnumerable<string> PairAnswers(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        foreach (var truth in materials.Where(material =>
            material.Kind == GeneratedContentMaterialKind.MatchTruth))
        {
            var index = Regex.Match(truth.Value, @"pair-(\d+)", RegexOptions.IgnoreCase).Groups[1].Value;
            yield return $"pair-{index}={(truth.Value.Contains(": match", StringComparison.OrdinalIgnoreCase) ? "same" : "different")}";
        }
    }

    private static IEnumerable<string> ClassificationAnswers(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        foreach (var expected in materials.Where(material =>
            material.Kind == GeneratedContentMaterialKind.ExpectedClassification))
        {
            var index = Regex.Match(expected.Name, @"(\d+)$").Value;
            var colon = expected.Value.IndexOf(':');
            var semicolon = expected.Value.IndexOf(';', colon + 1);
            var token = colon < 0
                ? expected.Value
                : (semicolon < 0 ? expected.Value[(colon + 1)..] : expected.Value[(colon + 1)..semicolon]).Trim();
            yield return $"{index}={token}";
        }
    }

    private static IEnumerable<string> MappingAnswers(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        foreach (var expected in materials.Where(material =>
            material.Kind == GeneratedContentMaterialKind.ExpectedMapping))
        {
            var index = Regex.Match(expected.Name, @"(\d+)$").Value;
            var source = Segment(expected.Value, "expected source relation ");
            var target = Segment(expected.Value, "expected target relation ");
            yield return $"{index}={source} -> {target} because both preserve the relation";
        }
    }

    private static string Segment(string value, string marker)
    {
        var start = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        start += marker.Length;
        var end = value.IndexOf(';', start);
        return (end < 0 ? value[start..] : value[start..end]).Trim().TrimEnd('.');
    }

    private static IEnumerable<string> AuditAnswers(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        var materialArray = materials.ToArray();
        foreach (var error in materials.Where(material =>
            material.Kind == GeneratedContentMaterialKind.SeededError))
        {
            var index = Regex.Match(error.Name, @"(\d+)$").Value;
            var line = Regex.Match(error.Value, @"line\s+(\d+)", RegexOptions.IgnoreCase)
                .Groups[1].Value;
            var type = Regex.Match(error.Value, @"type\s+([^;]+)", RegexOptions.IgnoreCase)
                .Groups[1].Value.Trim();
            var expected = materialArray.First(material =>
                material.Kind == GeneratedContentMaterialKind.ExpectedFinding &&
                material.Name.EndsWith($"-{index}", StringComparison.Ordinal));
            yield return $"FINDING-{index}=line {line}, {type}, {expected.Value}";
        }
    }

    private static RuntimeEvent Event(
        long sequence,
        RuntimeEventKind kind,
        string? phaseId,
        RuntimeSessionPhaseKind? phaseKind,
        params RuntimeEventFact[] facts)
    {
        return new RuntimeEvent(
            "evaluation-session",
            sequence,
            kind,
            new RuntimeInstant(TimeSpan.FromSeconds(sequence)),
            phaseId,
            phaseKind,
            facts);
    }

    private static PromptContentKind ContentKindFor(DrillId drill)
    {
        return drill switch
        {
            DrillId.FH1TargetHold or DrillId.AI1PressureRepeat or
                DrillId.AI2DisruptionRecovery or DrillId.TI1CompositeTask or
                DrillId.TI2GlobalReviewTask => PromptContentKind.EquivalentPrompt,
            DrillId.FH2DistractorHold or DrillId.FS1CueSwitch or
                DrillId.FS2InvalidCueFilter or DrillId.IR1GoNoGoRule or
                DrillId.IR2ExceptionRule => PromptContentKind.CueSequence,
            DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform =>
                PromptContentKind.DelayedReconstructionTask,
            DrillId.DE1PairDiscrimination or DrillId.DE2SeededAudit =>
                PromptContentKind.DiscriminationItemSet,
            DrillId.CO1RuleExtraction or DrillId.CO2StructureMapping =>
                PromptContentKind.RuleExampleSet,
            _ => throw new ArgumentOutOfRangeException(nameof(drill), drill, "Unknown drill."),
        };
    }
}
