namespace MentalGymnastics.Core;

public sealed class TransferRetestPlan
{
    public TransferRetestPlan(
        int requiredTransferContexts,
        bool usesFreshEquivalentContexts)
    {
        if (requiredTransferContexts < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredTransferContexts),
                "Required transfer contexts cannot be negative.");
        }

        RequiredTransferContexts = requiredTransferContexts;
        UsesFreshEquivalentContexts = usesFreshEquivalentContexts;
    }

    public int RequiredTransferContexts { get; }

    public bool UsesFreshEquivalentContexts { get; }
}

public sealed record TransferTestDefinition(
    BranchCode SourceBranch,
    string TransferTask,
    string SameDemand,
    string ChangedContext,
    TransferRetestPlan RetestRequirement);

public sealed class TransferSourceStandardEvidence
{
    public TransferSourceStandardEvidence(
        BranchCode branch,
        GlobalLevelId level,
        string standard,
        bool visibleInTransferArtifact)
    {
        Branch = branch;
        Level = level;
        Standard = standard ?? string.Empty;
        VisibleInTransferArtifact = visibleInTransferArtifact;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public string Standard { get; }

    public bool VisibleInTransferArtifact { get; }
}

public sealed class TransferEligibilityRequest
{
    public TransferEligibilityRequest(
        BranchCode sourceBranch,
        GlobalLevelId sourceLevel,
        string transferTask,
        CapacityId? trainedCapacity,
        string sameDemand,
        string changedContext,
        TransferSourceStandardEvidence? sourceStandardEvidence,
        TransferRetestPlan? retestPlan)
    {
        SourceBranch = sourceBranch;
        SourceLevel = sourceLevel;
        TransferTask = transferTask ?? string.Empty;
        TrainedCapacity = trainedCapacity;
        SameDemand = sameDemand ?? string.Empty;
        ChangedContext = changedContext ?? string.Empty;
        SourceStandardEvidence = sourceStandardEvidence;
        RetestPlan = retestPlan;
    }

    public BranchCode SourceBranch { get; }

    public GlobalLevelId SourceLevel { get; }

    public string TransferTask { get; }

    public CapacityId? TrainedCapacity { get; }

    public string SameDemand { get; }

    public string ChangedContext { get; }

    public TransferSourceStandardEvidence? SourceStandardEvidence { get; }

    public TransferRetestPlan? RetestPlan { get; }
}

public sealed record TransferEligibilityFailure(
    TransferEligibilityFailureKind Kind,
    string Detail);

public sealed record TransferEligibilityResult(
    bool IsEligible,
    IReadOnlyList<TransferEligibilityFailure> Failures);

public static class TransferTestCatalog
{
    private static readonly TransferRetestPlan StandardRetestRequirement = new(
        requiredTransferContexts: 2,
        usesFreshEquivalentContexts: true);

    public static IReadOnlyList<TransferTestDefinition> TransferTests { get; } =
    [
        Transfer(
            BranchCode.FH,
            "Hold target during WM or DE task.",
            "Target, drift marking, no target substitution.",
            "Content and task format."),
        Transfer(
            BranchCode.FS,
            "Switch between two branch tasks.",
            "Cue obedience and return standard.",
            "Target type and branch context."),
        Transfer(
            BranchCode.WM,
            "Reconstruct structure from unfamiliar content.",
            "Encoding, delay, no invention.",
            "Domain or representation."),
        Transfer(
            BranchCode.IR,
            "Maintain rule in open task.",
            "Pre-stated rule and no critical breach.",
            "Task format and temptation source."),
        Transfer(
            BranchCode.DE,
            "Audit unstructured output.",
            "Marked uncertainty and error comparison.",
            "Output type and error distribution."),
        Transfer(
            BranchCode.CO,
            "Apply rule to unseen problem.",
            "Testable rule and prediction check.",
            "Domain or example set."),
        Transfer(
            BranchCode.AI,
            "Repeat standard under new pressure source.",
            "Original branch standard.",
            "Pressure source."),
        Transfer(
            BranchCode.TI,
            "Solve new composite task.",
            "Branch-specific evidence.",
            "Task family or transfer distance."),
    ];

    private static TransferTestDefinition Transfer(
        BranchCode sourceBranch,
        string transferTask,
        string sameDemand,
        string changedContext)
    {
        return new TransferTestDefinition(
            sourceBranch,
            transferTask,
            sameDemand,
            changedContext,
            StandardRetestRequirement);
    }
}

public static class TransferEligibilityEvaluator
{
    public static TransferEligibilityResult Evaluate(TransferEligibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var failures = new List<TransferEligibilityFailure>();
        var definition = TransferTestCatalog.TransferTests.Single(test => test.SourceBranch == request.SourceBranch);

        EvaluateTransferTask(request, definition, failures);
        EvaluateTrainedCapacity(request, failures);
        EvaluateSameDemand(request, definition, failures);
        EvaluateChangedContext(request, definition, failures);
        EvaluateSourceStandardEvidence(request, failures);
        EvaluateRetestRequirement(request, definition, failures);

        return new TransferEligibilityResult(failures.Count == 0, failures);
    }

    private static void EvaluateTransferTask(
        TransferEligibilityRequest request,
        TransferTestDefinition definition,
        ICollection<TransferEligibilityFailure> failures)
    {
        if (!SameText(request.TransferTask, definition.TransferTask))
        {
            failures.Add(new TransferEligibilityFailure(
                TransferEligibilityFailureKind.TransferTaskDoesNotMatchSourceBranch,
                definition.TransferTask));
        }
    }

    private static void EvaluateTrainedCapacity(
        TransferEligibilityRequest request,
        ICollection<TransferEligibilityFailure> failures)
    {
        if (request.TrainedCapacity is null)
        {
            failures.Add(new TransferEligibilityFailure(
                TransferEligibilityFailureKind.TrainedCapacityNotSpecified,
                "A transfer test must specify the trained capacity; novelty alone is not transfer."));
            return;
        }

        var capacity = ProgramCatalog.Capacities.Single(item => item.Id == request.TrainedCapacity.Value);
        if (!capacity.Branches.Contains(request.SourceBranch))
        {
            failures.Add(new TransferEligibilityFailure(
                TransferEligibilityFailureKind.TrainedCapacityNotInSourceBranch,
                $"{request.TrainedCapacity.Value} is not trained by {request.SourceBranch}."));
        }
    }

    private static void EvaluateSameDemand(
        TransferEligibilityRequest request,
        TransferTestDefinition definition,
        ICollection<TransferEligibilityFailure> failures)
    {
        if (!SameText(request.SameDemand, definition.SameDemand))
        {
            failures.Add(new TransferEligibilityFailure(
                TransferEligibilityFailureKind.SourceDemandNotPreserved,
                definition.SameDemand));
        }
    }

    private static void EvaluateChangedContext(
        TransferEligibilityRequest request,
        TransferTestDefinition definition,
        ICollection<TransferEligibilityFailure> failures)
    {
        if (!SameText(request.ChangedContext, definition.ChangedContext))
        {
            failures.Add(new TransferEligibilityFailure(
                TransferEligibilityFailureKind.ContextNotChanged,
                definition.ChangedContext));
        }
    }

    private static void EvaluateSourceStandardEvidence(
        TransferEligibilityRequest request,
        ICollection<TransferEligibilityFailure> failures)
    {
        if (request.SourceStandardEvidence is null)
        {
            failures.Add(new TransferEligibilityFailure(
                TransferEligibilityFailureKind.SourceStandardEvidenceMissing,
                "Transfer requires visible source-branch standard evidence."));
            return;
        }

        var expectedStandard = ProgramCatalog.Standards.SingleOrDefault(
            standard => standard.Branch == request.SourceBranch && standard.Level == request.SourceLevel);

        if (expectedStandard is null ||
            request.SourceStandardEvidence.Branch != request.SourceBranch ||
            request.SourceStandardEvidence.Level != request.SourceLevel ||
            !SameText(request.SourceStandardEvidence.Standard, expectedStandard.Standard))
        {
            failures.Add(new TransferEligibilityFailure(
                TransferEligibilityFailureKind.SourceStandardDoesNotMatchCatalog,
                $"{request.SourceBranch} {request.SourceLevel} source standard must remain visible."));
        }

        if (!request.SourceStandardEvidence.VisibleInTransferArtifact)
        {
            failures.Add(new TransferEligibilityFailure(
                TransferEligibilityFailureKind.SourceStandardNotVisible,
                "The source branch standard must be visible in the transfer artifact."));
        }
    }

    private static void EvaluateRetestRequirement(
        TransferEligibilityRequest request,
        TransferTestDefinition definition,
        ICollection<TransferEligibilityFailure> failures)
    {
        if (request.RetestPlan is null ||
            request.RetestPlan.RequiredTransferContexts < definition.RetestRequirement.RequiredTransferContexts ||
            !request.RetestPlan.UsesFreshEquivalentContexts)
        {
            failures.Add(new TransferEligibilityFailure(
                TransferEligibilityFailureKind.RetestRequirementMissing,
                "Transfer requires two fresh equivalent transfer contexts."));
        }
    }

    private static bool SameText(string actual, string expected)
    {
        return string.Equals(actual.Trim(), expected.Trim(), StringComparison.Ordinal);
    }
}
