using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content.Tests;

public sealed class InhibitionGeneratedContentTests
{
    [Fact]
    public void GoNoGoGenerationProducesValidCueStreamWithoutAdvancement()
    {
        var request = CreateGoNoGoRequest();

        var generated = InhibitionGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("ir-seed-alpha"));

        var materialValidation = GeneratedContentMaterialValidator.Validate(
            generated.Result,
            generated.Materials);
        var loadValidation = GeneratedContentLoadConstraintValidator.Validate(
            generated.Result,
            generated.Materials);

        Assert.True(materialValidation.IsValid);
        Assert.True(loadValidation.IsValid);
        Assert.True(generated.ValidatedContent.CanBeConsumedByRuntime);
        Assert.False(generated.GrantsAdvancement);
        Assert.Equal(BranchCode.IR, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L1, generated.Result.Level);
        Assert.Equal(DrillId.IR1GoNoGoRule, generated.Result.Drill);
        Assert.Equal(PromptContentKind.CueSequence, generated.Result.ContentKind);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.CueConflict, "simple go/no-go symbols");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.CuePace, "2-second cadence");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.NoGoFrequency, "every third cue");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.ResponseWindow, "2 seconds");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, PrematureResponseConstraint);

        var cueStream = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.GoNoGoCue)
            .ToArray();
        var expectedActions = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedAction)
            .ToArray();
        Assert.Equal(9, cueStream.Length);
        Assert.Equal(cueStream.Length, expectedActions.Length);
        Assert.All(cueStream, cue =>
        {
            Assert.True(
                VisualStimulusCodec.TryDecode(cue.Value, out var stimulus),
                $"Go/no-go cue {cue.Name} must contain canonical visual stimulus material.");
            Assert.NotNull(stimulus);
            Assert.DoesNotContain("go:", cue.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("no-go:", cue.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("withhold", cue.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("respond", cue.Value, StringComparison.OrdinalIgnoreCase);
            var expectedAction = Assert.Single(expectedActions, action =>
                string.Equals(
                    action.Name,
                    cue.Name.Replace("cue-", "expected-action-", StringComparison.Ordinal),
                    StringComparison.Ordinal));
            var isGo = IsGoStimulus(stimulus!);
            var isNoGo = IsNoGoStimulus(stimulus!);
            Assert.NotEqual(isGo, isNoGo);
            Assert.Equal(
                expectedAction.Value.Contains("respond", StringComparison.OrdinalIgnoreCase),
                isGo);
            Assert.Equal(
                expectedAction.Value.Contains("withhold", StringComparison.OrdinalIgnoreCase),
                isNoGo);
        });

        var noGoActions = expectedActions
            .Where(action => action.Value.Contains("withhold", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(3, noGoActions.Length);
        Assert.All(noGoActions, action =>
        {
            Assert.Contains("no response expected", action.Value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("premature response fails item", action.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("valid response", action.Value, StringComparison.OrdinalIgnoreCase);
        });

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "ir-go-no-go");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "cue-pace" &&
            fact.Value == "2-second cadence");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "premature-response-policy" &&
            fact.Value.Contains("fails the item", StringComparison.OrdinalIgnoreCase) &&
            fact.Value.Contains("cannot be treated as valid", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "commission-evidence" &&
            fact.Value.Contains("response on no-go", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SameSeedRepeatsContentAndFreshRequestChangesCueStreamWithoutChangingDemand()
    {
        var request = CreateGoNoGoRequest();
        var seed = new GeneratedContentSeed("ir-seed-bravo");

        var first = InhibitionGeneratedContentGenerator.Generate(request, seed);
        var repeated = InhibitionGeneratedContentGenerator.Generate(CreateGoNoGoRequest(), seed);
        var fresh = InhibitionGeneratedContentGenerator.Generate(
            CreateGoNoGoRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(GoNoGoCueValues(first.Materials), GoNoGoCueValues(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.NoGoFrequency, "every third cue");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.CuePace, "2-second cadence");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.HonestyConstraint, PrematureResponseConstraint);
    }

    [Fact]
    public void ExceptionRuleGenerationProducesValidRuleStreamWithoutAdvancement()
    {
        var request = CreateExceptionRuleRequest();

        var generated = InhibitionGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("ir-exception-alpha"));

        var materialValidation = GeneratedContentMaterialValidator.Validate(
            generated.Result,
            generated.Materials);
        var loadValidation = GeneratedContentLoadConstraintValidator.Validate(
            generated.Result,
            generated.Materials);

        Assert.True(materialValidation.IsValid);
        Assert.True(loadValidation.IsValid);
        Assert.True(generated.ValidatedContent.CanBeConsumedByRuntime);
        Assert.False(generated.GrantsAdvancement);
        Assert.Equal(BranchCode.IR, generated.Result.Branch);
        Assert.Equal(GlobalLevelId.L2, generated.Result.Level);
        Assert.Equal(DrillId.IR2ExceptionRule, generated.Result.Drill);
        Assert.Equal(PromptContentKind.CueSequence, generated.Result.ContentKind);

        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.CuePace, "2-second cadence");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.ResponseWindow, "2 seconds");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.Similarity, "near symbols");
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, RuleAndExceptionsBeforeSetConstraint);
        AssertMaterial(generated.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoRuleChangeConstraint);

        var ruleStatement = Assert.Single(generated.Materials, material =>
            material.Kind == GeneratedContentMaterialKind.RuleStatement);
        Assert.Contains("before set", ruleStatement.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exceptions", ruleStatement.Value, StringComparison.OrdinalIgnoreCase);

        var exceptions = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExceptionDefinition)
            .ToArray();
        Assert.Equal(3, exceptions.Length);
        var exceptionStimuli = exceptions
            .Select(exception => VisualStimulusCodec.DecodeException(exception.Value))
            .ToArray();
        Assert.All(exceptions, exception =>
        {
            Assert.Contains("exception", exception.Value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("instead", exception.Value, StringComparison.OrdinalIgnoreCase);
        });
        Assert.All(exceptionStimuli, exception =>
        {
            Assert.True(exception.Ordinal > 0);
            Assert.False(string.IsNullOrWhiteSpace(exception.Reason));
            Assert.True(Enum.IsDefined(exception.ExpectedAction));
        });

        var cueSteps = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.CueStep)
            .ToArray();
        var expectedActions = generated.Materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExpectedAction)
            .ToArray();
        Assert.Equal(8, cueSteps.Length);
        Assert.Equal(cueSteps.Length, expectedActions.Length);
        Assert.All(cueSteps, cue =>
        {
            Assert.True(
                VisualStimulusCodec.TryDecode(cue.Value, out var stimulus),
                $"Exception-rule cue {cue.Name} must contain canonical visual stimulus material.");
            Assert.NotNull(stimulus);
            Assert.DoesNotContain("exception:", cue.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("base rule:", cue.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("withhold", cue.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("respond", cue.Value, StringComparison.OrdinalIgnoreCase);
        });
        Assert.All(exceptionStimuli, exception =>
        {
            var cue = Assert.Single(cueSteps, candidate => string.Equals(
                candidate.Value,
                VisualStimulusCodec.Encode(exception.Stimulus),
                StringComparison.Ordinal));
            var expectedAction = Assert.Single(expectedActions, action =>
                string.Equals(
                    action.Name,
                    cue.Name.Replace("cue-step-", "expected-action-", StringComparison.Ordinal),
                    StringComparison.Ordinal));
            Assert.Contains(
                exception.ExpectedAction.ToString(),
                expectedAction.Value,
                StringComparison.OrdinalIgnoreCase);
        });
        var encodedExceptionStimuli = exceptionStimuli
            .Select(exception => VisualStimulusCodec.Encode(exception.Stimulus))
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(
            cueSteps.Where(cue => !encodedExceptionStimuli.Contains(cue.Value)),
            cue =>
            {
                var stimulus = VisualStimulusCodec.Decode(cue.Value);
                var expectedAction = Assert.Single(expectedActions, action =>
                    string.Equals(
                        action.Name,
                        cue.Name.Replace("cue-step-", "expected-action-", StringComparison.Ordinal),
                        StringComparison.Ordinal));
                var isRound = stimulus.Shape is
                    VisualStimulusShape.Dot or
                    VisualStimulusShape.Circle or
                    VisualStimulusShape.Ring;
                Assert.Contains(
                    isRound ? "tap" : "withhold",
                    expectedAction.Value,
                    StringComparison.OrdinalIgnoreCase);
            });
        Assert.Contains(expectedActions, action =>
            action.Value.Contains("apply exception", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "payload-family" &&
            fact.Value == "ir-exception-rule");
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "rule-change-evidence" &&
            fact.Value.Contains("mid-set", StringComparison.OrdinalIgnoreCase) &&
            fact.Value.Contains("detectable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generated.Result.PayloadFacts, fact =>
            fact.Name == "rule-fidelity-evidence" &&
            fact.Value.Contains("pre-stated rule", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExceptionRuleFreshVariantChangesExceptionsWithoutChangingDemand()
    {
        var request = CreateExceptionRuleRequest();
        var seed = new GeneratedContentSeed("ir-exception-bravo");

        var first = InhibitionGeneratedContentGenerator.Generate(request, seed);
        var repeated = InhibitionGeneratedContentGenerator.Generate(CreateExceptionRuleRequest(), seed);
        var fresh = InhibitionGeneratedContentGenerator.Generate(
            CreateExceptionRuleRequest(previouslyUsedContentIds: [first.Result.ContentId]),
            seed);

        Assert.Equal(first.Result.ContentId, repeated.Result.ContentId);
        Assert.Equal(MaterialSnapshot(first.Materials), MaterialSnapshot(repeated.Materials));

        Assert.NotEqual(first.Result.ContentId, fresh.Result.ContentId);
        Assert.NotEqual(ExceptionValues(first.Materials), ExceptionValues(fresh.Materials));
        Assert.NotEqual(CueStepValues(first.Materials), CueStepValues(fresh.Materials));
        Assert.Equal(first.Result.Branch, fresh.Result.Branch);
        Assert.Equal(first.Result.Level, fresh.Result.Level);
        Assert.Equal(first.Result.Drill, fresh.Result.Drill);
        Assert.Equal(first.Result.ContentKind, fresh.Result.ContentKind);
        Assert.Equal(first.Result.EquivalenceClass, fresh.Result.EquivalenceClass);
        Assert.Equal(first.Result.Request.LoadVariables, fresh.Result.Request.LoadVariables);
        Assert.Equal(first.Result.Request.CriticalConstraints, fresh.Result.Request.CriticalConstraints);
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.CuePace, "2-second cadence");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.Similarity, "near symbols");
        AssertMaterial(fresh.Materials, GeneratedContentMaterialKind.HonestyConstraint, NoRuleChangeConstraint);
    }

    [Fact]
    public void InhibitionGenerationRejectsNonInhibitionDrills()
    {
        var request = new GeneratedDrillContentRequest(
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
            ],
            [new CriticalConstraint("Switch only on valid cue.")]);

        Assert.Throws<ArgumentException>(() => InhibitionGeneratedContentGenerator.Generate(
            request,
            new GeneratedContentSeed("ir-seed-alpha")));
    }

    private const string PrematureResponseConstraint = "Premature response fails item.";

    private const string RuleAndExceptionsBeforeSetConstraint = "Rule and exceptions stated before set.";

    private const string NoRuleChangeConstraint = "Rule cannot change mid-set.";

    private static GeneratedDrillContentRequest CreateGoNoGoRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
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
            [
                new CriticalConstraint(PrematureResponseConstraint),
            ],
            previouslyUsedContentIds);
    }

    private static GeneratedDrillContentRequest CreateExceptionRuleRequest(
        IEnumerable<string>? previouslyUsedContentIds = null)
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
            [
                new CriticalConstraint(RuleAndExceptionsBeforeSetConstraint),
                new CriticalConstraint(NoRuleChangeConstraint),
            ],
            previouslyUsedContentIds);
    }

    private static void AssertMaterial(
        IReadOnlyCollection<GeneratedContentMaterial> materials,
        GeneratedContentMaterialKind kind,
        string value)
    {
        Assert.Contains(materials, material =>
            material.Kind == kind &&
            string.Equals(material.Value, value, StringComparison.Ordinal));
    }

    private static IReadOnlyList<(GeneratedContentMaterialKind Kind, string Name, string Value)> MaterialSnapshot(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Select(material => (material.Kind, material.Name, material.Value))
            .ToArray();
    }

    private static IReadOnlyList<string> GoNoGoCueValues(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.GoNoGoCue)
            .Select(material => material.Value)
            .ToArray();
    }

    private static IReadOnlyList<string> ExceptionValues(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.ExceptionDefinition)
            .Select(material => material.Value)
            .ToArray();
    }

    private static IReadOnlyList<string> CueStepValues(
        IEnumerable<GeneratedContentMaterial> materials)
    {
        return materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.CueStep)
            .Select(material => material.Value)
            .ToArray();
    }

    private static bool IsGoStimulus(VisualStimulusSpec stimulus)
    {
        return stimulus.Color is
                VisualStimulusColor.Green or
                VisualStimulusColor.Blue or
                VisualStimulusColor.White ||
            stimulus is
            {
                Shape: VisualStimulusShape.Arrow,
                Direction: VisualStimulusDirection.North,
            } ||
            stimulus is
            {
                Shape: VisualStimulusShape.Dot,
                Color: VisualStimulusColor.Black,
                Fill: VisualStimulusFill.Solid,
            } ||
            stimulus is
            {
                Shape: VisualStimulusShape.Ring,
                Color: VisualStimulusColor.Black,
                Fill: VisualStimulusFill.Outline,
            };
    }

    private static bool IsNoGoStimulus(VisualStimulusSpec stimulus)
    {
        return stimulus.Color is VisualStimulusColor.Red or VisualStimulusColor.Amber ||
            stimulus is
            {
                Shape: VisualStimulusShape.Arrow,
                Direction: VisualStimulusDirection.South,
            } ||
            stimulus is
            {
                Shape: VisualStimulusShape.Bar,
                Color: VisualStimulusColor.Black,
                Fill: VisualStimulusFill.Striped,
            } ||
            stimulus is
            {
                Shape: VisualStimulusShape.Dot,
                Color: VisualStimulusColor.Black,
                Fill: VisualStimulusFill.Outline,
            } ||
            stimulus is
            {
                Shape: VisualStimulusShape.Ring,
                Color: VisualStimulusColor.Black,
                Fill: VisualStimulusFill.Crossed,
            };
    }
}
