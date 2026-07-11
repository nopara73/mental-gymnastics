namespace MentalGymnastics.Core;

public readonly record struct TrainingDate(int Year, int Month, int Day)
{
    public static TrainingDate From(int year, int month, int day)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(year);

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        if (day < 1 || day > DaysInMonth(year, month))
        {
            throw new ArgumentOutOfRangeException(nameof(day), "Day must be valid for the month.");
        }

        return new TrainingDate(year, month, day);
    }

    public int DaysUntil(TrainingDate other)
    {
        return other.ToDayNumber() - ToDayNumber();
    }

    internal int ToDayNumber()
    {
        var year = Year - 1;
        var daysBeforeYear = (year * 365) + (year / 4) - (year / 100) + (year / 400);
        var daysBeforeMonth = 0;

        for (var month = 1; month < Month; month++)
        {
            daysBeforeMonth += DaysInMonth(Year, month);
        }

        return daysBeforeYear + daysBeforeMonth + Day;
    }

    private static int DaysInMonth(int year, int month)
    {
        return month switch
        {
            1 or 3 or 5 or 7 or 8 or 10 or 12 => 31,
            4 or 6 or 9 or 11 => 30,
            2 => IsLeapYear(year) ? 29 : 28,
            _ => throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12."),
        };
    }

    private static bool IsLeapYear(int year)
    {
        return year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
    }
}

public sealed record ObservableEvidence(
    ObservableEvidenceKind Kind,
    string Description);

public sealed class EvidenceArtifact
{
    public EvidenceArtifact(
        EvidenceArtifactCategory category,
        TrainingDate date,
        IEnumerable<ObservableEvidence> observableEvidence,
        string summaryOrReference,
        string? subjectiveNote = null)
    {
        ArgumentNullException.ThrowIfNull(observableEvidence);

        var evidence = observableEvidence.ToArray();
        if (evidence.Length == 0)
        {
            throw new ArgumentException(
                "An evidence artifact must include observable evidence.",
                nameof(observableEvidence));
        }

        if (string.IsNullOrWhiteSpace(summaryOrReference))
        {
            throw new ArgumentException(
                "An evidence artifact must include a summary or reference.",
                nameof(summaryOrReference));
        }

        Category = category;
        Date = date;
        ObservableEvidence = evidence;
        SummaryOrReference = summaryOrReference;
        SubjectiveNote = subjectiveNote;
    }

    public EvidenceArtifactCategory Category { get; }

    public TrainingDate Date { get; }

    public IReadOnlyList<ObservableEvidence> ObservableEvidence { get; }

    public string SummaryOrReference { get; }

    public string? SubjectiveNote { get; }
}

public sealed record LoadVariable(
    string Name,
    string Value);

public sealed record CriticalConstraint(
    string Description);

public sealed record TestResultEvidence(
    TestResultEvidenceKind Kind,
    string Value);

public sealed class TestTask
{
    private TestTask(DrillId? drill, string? transferTask)
    {
        Drill = drill;
        TransferTask = transferTask;
    }

    public DrillId? Drill { get; }

    public string? TransferTask { get; }

    public static TestTask ForDrill(DrillId drill)
    {
        return new TestTask(drill, null);
    }

    public static TestTask ForTransfer(string transferTask)
    {
        if (string.IsNullOrWhiteSpace(transferTask))
        {
            throw new ArgumentException("Transfer task must be described.", nameof(transferTask));
        }

        return new TestTask(null, transferTask);
    }
}

public sealed class FormalTestAttempt
{
    public FormalTestAttempt(
        BranchCode branch,
        GlobalLevelId level,
        TrainingDate date,
        TestTask task,
        IEnumerable<LoadVariable> loadVariables,
        string standard,
        IEnumerable<CriticalConstraint> criticalConstraints,
        TestResultEvidence resultEvidence,
        FailureType? failureType,
        FormalTestPassState passState,
        EvidenceArtifact artifact,
        string? mainFailureModeAvoided = null)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(loadVariables);
        ArgumentNullException.ThrowIfNull(criticalConstraints);
        ArgumentNullException.ThrowIfNull(resultEvidence);
        ArgumentNullException.ThrowIfNull(artifact);

        var loadVariableArray = loadVariables.ToArray();
        if (loadVariableArray.Length == 0)
        {
            throw new ArgumentException(
                "A formal test attempt must record load variables.",
                nameof(loadVariables));
        }

        if (string.IsNullOrWhiteSpace(standard))
        {
            throw new ArgumentException(
                "A formal test attempt must record the standard.",
                nameof(standard));
        }

        var criticalConstraintArray = criticalConstraints.ToArray();
        if (criticalConstraintArray.Length == 0)
        {
            throw new ArgumentException(
                "A formal test attempt must record critical constraints.",
                nameof(criticalConstraints));
        }

        if (passState == FormalTestPassState.Fail && failureType is null)
        {
            throw new ArgumentException(
                "A failed formal test attempt must record the failure type.",
                nameof(failureType));
        }

        Branch = branch;
        Level = level;
        Date = date;
        Task = task;
        LoadVariables = loadVariableArray;
        Standard = standard;
        CriticalConstraints = criticalConstraintArray;
        ResultEvidence = resultEvidence;
        FailureType = failureType;
        PassState = passState;
        Artifact = artifact;
        MainFailureModeAvoided = string.IsNullOrWhiteSpace(mainFailureModeAvoided)
            ? null
            : mainFailureModeAvoided.Trim();
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public TrainingDate Date { get; }

    public TestTask Task { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public string Standard { get; }

    public IReadOnlyList<CriticalConstraint> CriticalConstraints { get; }

    public TestResultEvidence ResultEvidence { get; }

    public FailureType? FailureType { get; }

    public FormalTestPassState PassState { get; }

    public EvidenceArtifact Artifact { get; }

    public string? MainFailureModeAvoided { get; }
}
