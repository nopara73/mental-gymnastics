using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class TransferTestEligibilityTests
{
    [Fact]
    public void ExposesDocumentedTransferTestsBySourceBranch()
    {
        Assert.Equal(
            Enum.GetValues<BranchCode>(),
            TransferTestCatalog.TransferTests.Select(test => test.SourceBranch));

        AssertTransfer(
            BranchCode.FH,
            "Hold target during WM or DE task.",
            "Target, drift marking, no target substitution.",
            "Content and task format.");
        AssertTransfer(
            BranchCode.FS,
            "Switch between two branch tasks.",
            "Cue obedience and return standard.",
            "Target type and branch context.");
        AssertTransfer(
            BranchCode.WM,
            "Reconstruct structure from unfamiliar content.",
            "Encoding, delay, no invention.",
            "Domain or representation.");
        AssertTransfer(
            BranchCode.IR,
            "Maintain rule in open task.",
            "Pre-stated rule and no critical breach.",
            "Task format and temptation source.");
        AssertTransfer(
            BranchCode.DE,
            "Audit unstructured output.",
            "Marked uncertainty and error comparison.",
            "Output type and error distribution.");
        AssertTransfer(
            BranchCode.CO,
            "Apply rule to unseen problem.",
            "Testable rule and prediction check.",
            "Domain or example set.");
        AssertTransfer(
            BranchCode.AI,
            "Repeat standard under new pressure source.",
            "Original branch standard.",
            "Pressure source.");
        AssertTransfer(
            BranchCode.TI,
            "Solve new composite task.",
            "Branch-specific evidence.",
            "Task family or transfer distance.");
    }

    [Fact]
    public void AllowsTransferWhenCapacityDemandContextStandardEvidenceAndRetestArePresent()
    {
        var result = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.WM,
                GlobalLevelId.L3,
                "Reconstruct structure from unfamiliar content.",
                CapacityId.EncodingFidelity,
                "Encoding, delay, no invention.",
                "Domain or representation.",
                SourceStandardEvidence(BranchCode.WM, GlobalLevelId.L3),
                new TransferRetestPlan(requiredTransferContexts: 2, usesFreshEquivalentContexts: true)));

        Assert.True(result.IsEligible);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void RejectsNovelTaskThatDoesNotPreserveTheSourceDemand()
    {
        var result = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.FH,
                GlobalLevelId.L3,
                "Hold target during WM or DE task.",
                CapacityId.SelectiveHold,
                "",
                "Content and task format.",
                SourceStandardEvidence(BranchCode.FH, GlobalLevelId.L3),
                new TransferRetestPlan(requiredTransferContexts: 2, usesFreshEquivalentContexts: true)));

        Assert.False(result.IsEligible);
        Assert.Contains(result.Failures, failure => failure.Kind == TransferEligibilityFailureKind.SourceDemandNotPreserved);
    }

    [Fact]
    public void RejectsTransferWithoutDocumentedChangedContext()
    {
        var result = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.IR,
                GlobalLevelId.L3,
                "Maintain rule in open task.",
                CapacityId.RuleFidelity,
                "Pre-stated rule and no critical breach.",
                "",
                SourceStandardEvidence(BranchCode.IR, GlobalLevelId.L3),
                new TransferRetestPlan(requiredTransferContexts: 2, usesFreshEquivalentContexts: true)));

        Assert.False(result.IsEligible);
        Assert.Contains(result.Failures, failure => failure.Kind == TransferEligibilityFailureKind.ContextNotChanged);
    }

    [Fact]
    public void RejectsNoveltyAloneWithoutSpecifiedTrainedCapacity()
    {
        var result = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.DE,
                GlobalLevelId.L3,
                "Audit unstructured output.",
                trainedCapacity: null,
                sameDemand: "Marked uncertainty and error comparison.",
                changedContext: "Output type and error distribution.",
                SourceStandardEvidence(BranchCode.DE, GlobalLevelId.L3),
                new TransferRetestPlan(requiredTransferContexts: 2, usesFreshEquivalentContexts: true)));

        Assert.False(result.IsEligible);
        Assert.Contains(result.Failures, failure => failure.Kind == TransferEligibilityFailureKind.TrainedCapacityNotSpecified);
    }

    [Fact]
    public void RejectsMissingSourceStandardEvidence()
    {
        var result = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.CO,
                GlobalLevelId.L3,
                "Apply rule to unseen problem.",
                CapacityId.RuleExtraction,
                "Testable rule and prediction check.",
                "Domain or example set.",
                null,
                new TransferRetestPlan(requiredTransferContexts: 2, usesFreshEquivalentContexts: true)));

        Assert.False(result.IsEligible);
        Assert.Contains(result.Failures, failure => failure.Kind == TransferEligibilityFailureKind.SourceStandardEvidenceMissing);
    }

    [Fact]
    public void RejectsSourceStandardEvidenceThatIsNotVisibleInTransferArtifact()
    {
        var hiddenSourceStandard = new TransferSourceStandardEvidence(
            BranchCode.AI,
            GlobalLevelId.L3,
            ProgramCatalog.Standards
                .Single(standard => standard.Branch == BranchCode.AI && standard.Level == GlobalLevelId.L3)
                .Standard,
            visibleInTransferArtifact: false);

        var result = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.AI,
                GlobalLevelId.L3,
                "Repeat standard under new pressure source.",
                CapacityId.PressureStableExecution,
                "Original branch standard.",
                "Pressure source.",
                hiddenSourceStandard,
                new TransferRetestPlan(requiredTransferContexts: 2, usesFreshEquivalentContexts: true)));

        Assert.False(result.IsEligible);
        Assert.Contains(result.Failures, failure => failure.Kind == TransferEligibilityFailureKind.SourceStandardNotVisible);
    }

    [Fact]
    public void RejectsMissingRetestRequirement()
    {
        var result = TransferEligibilityEvaluator.Evaluate(
            new TransferEligibilityRequest(
                BranchCode.TI,
                GlobalLevelId.L3,
                "Solve new composite task.",
                CapacityId.IntegratedTaskControl,
                "Branch-specific evidence.",
                "Task family or transfer distance.",
                SourceStandardEvidence(BranchCode.TI, GlobalLevelId.L3),
                new TransferRetestPlan(requiredTransferContexts: 1, usesFreshEquivalentContexts: true)));

        Assert.False(result.IsEligible);
        Assert.Contains(result.Failures, failure => failure.Kind == TransferEligibilityFailureKind.RetestRequirementMissing);
    }

    private static void AssertTransfer(
        BranchCode sourceBranch,
        string transferTask,
        string sameDemand,
        string changedContext)
    {
        var definition = TransferTestCatalog.TransferTests.Single(test => test.SourceBranch == sourceBranch);

        Assert.Equal(transferTask, definition.TransferTask);
        Assert.Equal(sameDemand, definition.SameDemand);
        Assert.Equal(changedContext, definition.ChangedContext);
        Assert.Equal(2, definition.RetestRequirement.RequiredTransferContexts);
        Assert.True(definition.RetestRequirement.UsesFreshEquivalentContexts);
    }

    private static TransferSourceStandardEvidence SourceStandardEvidence(
        BranchCode branch,
        GlobalLevelId level)
    {
        return new TransferSourceStandardEvidence(
            branch,
            level,
            ProgramCatalog.Standards.Single(standard => standard.Branch == branch && standard.Level == level).Standard,
            visibleInTransferArtifact: true);
    }
}
