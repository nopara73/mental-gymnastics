namespace MentalGymnastics.Core;

public sealed class ClassifiedFailure
{
    public ClassifiedFailure(
        BranchCode branch,
        GlobalLevelId level,
        FailureType type,
        IEnumerable<FailureEvidenceSignal> evidenceSignals)
    {
        ArgumentNullException.ThrowIfNull(evidenceSignals);

        Branch = branch;
        Level = level;
        Type = type;
        EvidenceSignals = evidenceSignals.ToArray();
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public FailureType Type { get; }

    public IReadOnlyList<FailureEvidenceSignal> EvidenceSignals { get; }
}

public sealed class StuckStateResponseContext
{
    public StuckStateResponseContext(
        bool isStuck,
        bool failuresAppearInMoreThanOneBranch,
        bool transferIsStuckPoint)
    {
        IsStuck = isStuck;
        FailuresAppearInMoreThanOneBranch = failuresAppearInMoreThanOneBranch;
        TransferIsStuckPoint = transferIsStuckPoint;
    }

    public static StuckStateResponseContext None { get; } = new(
        isStuck: false,
        failuresAppearInMoreThanOneBranch: false,
        transferIsStuckPoint: false);

    public bool IsStuck { get; }

    public bool FailuresAppearInMoreThanOneBranch { get; }

    public bool TransferIsStuckPoint { get; }
}

public sealed class FailureResponseRequest
{
    public FailureResponseRequest(
        ClassifiedFailure failure,
        bool isFirstFailureOfType,
        bool repeatedOverloadInSameBranch,
        StuckStateResponseContext stuckStateContext)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(stuckStateContext);

        Failure = failure;
        IsFirstFailureOfType = isFirstFailureOfType;
        RepeatedOverloadInSameBranch = repeatedOverloadInSameBranch;
        StuckStateContext = stuckStateContext;
    }

    public ClassifiedFailure Failure { get; }

    public bool IsFirstFailureOfType { get; }

    public bool RepeatedOverloadInSameBranch { get; }

    public StuckStateResponseContext StuckStateContext { get; }
}

public sealed record FailureResponse(
    ClassifiedFailure Failure,
    IReadOnlyList<ProgrammingResponseAction> Actions);

public static class FailureResponseRouter
{
    public static FailureResponse Route(FailureResponseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actions = new List<ProgrammingResponseAction>();
        AddPrescribedFailureResponse(request, actions);
        AddStuckStateResponse(request.StuckStateContext, actions);

        return new FailureResponse(request.Failure, actions);
    }

    private static void AddPrescribedFailureResponse(
        FailureResponseRequest request,
        ICollection<ProgrammingResponseAction> actions)
    {
        switch (request.Failure.Type)
        {
            case FailureType.TechnicalFailure:
                actions.Add(ProgrammingResponseAction.StopTest);
                actions.Add(ProgrammingResponseAction.ReturnToPractice);
                actions.Add(ProgrammingResponseAction.SimplifyInstruction);
                actions.Add(ProgrammingResponseAction.RetestLater);
                if (request.IsFirstFailureOfType)
                {
                    actions.Add(ProgrammingResponseAction.PracticeDrillFormAtLowIntensityBeforeRetest);
                }

                break;

            case FailureType.EffortFailure:
                actions.Add(ProgrammingResponseAction.FailAttempt);
                actions.Add(ProgrammingResponseAction.RepeatSameOrLowerLoadWithStricterEvidence);
                if (request.IsFirstFailureOfType)
                {
                    actions.Add(ProgrammingResponseAction.RequireCleanPracticeArtifactBeforeRepeat);
                }

                break;

            case FailureType.Overload:
                actions.Add(ProgrammingResponseAction.ReduceOneLoadVariable);
                actions.Add(ProgrammingResponseAction.TrainRegression);
                actions.Add(ProgrammingResponseAction.RetestAfterCleanPractice);
                if (request.IsFirstFailureOfType)
                {
                    actions.Add(ProgrammingResponseAction.StabilizeRegression);
                }

                if (request.RepeatedOverloadInSameBranch)
                {
                    actions.Add(ProgrammingResponseAction.InspectPrerequisiteBranch);
                    actions.Add(ProgrammingResponseAction.ReduceWeeklyLoad);
                }

                break;

            case FailureType.BadProgramming:
                actions.Add(ProgrammingResponseAction.Deload);
                actions.Add(ProgrammingResponseAction.RestorePrerequisites);
                actions.Add(ProgrammingResponseAction.ReviseWeeklyEmphasis);
                actions.Add(ProgrammingResponseAction.SuspendAdvancementTestingForOneWeek);
                actions.Add(ProgrammingResponseAction.RunMaintenanceChecks);
                actions.Add(ProgrammingResponseAction.NoNewTests);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    request.Failure.Type,
                    "Unsupported failure type.");
        }
    }

    private static void AddStuckStateResponse(
        StuckStateResponseContext context,
        ICollection<ProgrammingResponseAction> actions)
    {
        if (!context.IsStuck)
        {
            return;
        }

        actions.Add(ProgrammingResponseAction.StopAdvancementTestsInStuckBranch);
        actions.Add(ProgrammingResponseAction.IdentifyStuckPoint);
        actions.Add(ProgrammingResponseAction.TrainNearestPrerequisiteForOneWeekAtModerateIntensity);
        actions.Add(ProgrammingResponseAction.UseRegressionPreservingFailedConstraint);
        if (context.FailuresAppearInMoreThanOneBranch)
        {
            actions.Add(ProgrammingResponseAction.ReduceTotalWeeklyLoad);
        }

        actions.Add(ProgrammingResponseAction.RetestFailedConstraintBeforeWholeGate);
        if (context.TransferIsStuckPoint)
        {
            actions.Add(ProgrammingResponseAction.ReturnToSourceBranch);
            actions.Add(ProgrammingResponseAction.TestNearerTransferDistance);
        }
    }
}
