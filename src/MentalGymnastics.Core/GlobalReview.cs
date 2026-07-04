namespace MentalGymnastics.Core;

public sealed record GlobalReviewOwnedLevel(
    BranchCode Branch,
    GlobalLevelId? OwnedLevel);

public sealed class GlobalReviewBottleneckInput
{
    public GlobalReviewBottleneckInput(
        BranchCode branch,
        BottleneckKind bottleneck,
        bool hasProgrammedResponse,
        bool needsEmphasis)
    {
        Branch = branch;
        Bottleneck = bottleneck;
        HasProgrammedResponse = hasProgrammedResponse;
        NeedsEmphasis = needsEmphasis;
    }

    public BranchCode Branch { get; }

    public BottleneckKind Bottleneck { get; }

    public bool HasProgrammedResponse { get; }

    public bool NeedsEmphasis { get; }
}

public sealed class GlobalReviewVolumeIntensityRecord
{
    public GlobalReviewVolumeIntensityRecord(
        BranchCode branch,
        int workingSets,
        TrainingIntensityKind intensity)
    {
        if (workingSets < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workingSets), "Working sets cannot be negative.");
        }

        Branch = branch;
        WorkingSets = workingSets;
        Intensity = intensity;
    }

    public BranchCode Branch { get; }

    public int WorkingSets { get; }

    public TrainingIntensityKind Intensity { get; }
}

public sealed class GlobalReviewRecoveryRecord
{
    public GlobalReviewRecoveryRecord(
        TrainingDate date,
        bool wasDeload)
    {
        Date = date;
        WasDeload = wasDeload;
    }

    public TrainingDate Date { get; }

    public bool WasDeload { get; }
}

public sealed class GlobalReviewAdvancementRecord
{
    public GlobalReviewAdvancementRecord(
        BranchCode branch,
        GlobalLevelId level,
        bool advancedByParticipationAlone)
    {
        Branch = branch;
        Level = level;
        AdvancedByParticipationAlone = advancedByParticipationAlone;
    }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public bool AdvancedByParticipationAlone { get; }
}

public sealed class GlobalReviewInput
{
    public GlobalReviewInput(
        TrainingDate asOf,
        PractitionerCategory practitionerCategory,
        PractitionerState practitionerState,
        IEnumerable<GlobalReviewOwnedLevel> currentOwnedLevels,
        IEnumerable<MaintenanceCurrencyResult> maintenanceStatus,
        IEnumerable<ClassifiedFailure> lastThreeFailures,
        IEnumerable<EvidenceArtifact> evidenceArtifacts,
        GlobalReviewBottleneckInput? bottleneck,
        IEnumerable<GlobalReviewVolumeIntensityRecord> volumeAndIntensityHistory,
        IEnumerable<GlobalReviewRecoveryRecord> recoveryOrDeloadHistory,
        IEnumerable<GlobalReviewAdvancementRecord> advancements,
        bool pauseTestsForDeload,
        bool openAdvancedBranch,
        bool attemptTransferIntegrationTransfer)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);
        ArgumentNullException.ThrowIfNull(currentOwnedLevels);
        ArgumentNullException.ThrowIfNull(maintenanceStatus);
        ArgumentNullException.ThrowIfNull(lastThreeFailures);
        ArgumentNullException.ThrowIfNull(evidenceArtifacts);
        ArgumentNullException.ThrowIfNull(volumeAndIntensityHistory);
        ArgumentNullException.ThrowIfNull(recoveryOrDeloadHistory);
        ArgumentNullException.ThrowIfNull(advancements);

        AsOf = asOf;
        PractitionerCategory = practitionerCategory;
        PractitionerState = practitionerState;
        CurrentOwnedLevels = currentOwnedLevels.ToArray();
        MaintenanceStatus = maintenanceStatus.ToArray();
        LastThreeFailures = lastThreeFailures.Take(3).ToArray();
        EvidenceArtifacts = evidenceArtifacts.ToArray();
        Bottleneck = bottleneck;
        VolumeAndIntensityHistory = volumeAndIntensityHistory.ToArray();
        RecoveryOrDeloadHistory = recoveryOrDeloadHistory.ToArray();
        Advancements = advancements.ToArray();
        PauseTestsForDeload = pauseTestsForDeload;
        OpenAdvancedBranch = openAdvancedBranch;
        AttemptTransferIntegrationTransfer = attemptTransferIntegrationTransfer;
    }

    public TrainingDate AsOf { get; }

    public PractitionerCategory PractitionerCategory { get; }

    public PractitionerState PractitionerState { get; }

    public IReadOnlyList<GlobalReviewOwnedLevel> CurrentOwnedLevels { get; }

    public IReadOnlyList<MaintenanceCurrencyResult> MaintenanceStatus { get; }

    public IReadOnlyList<ClassifiedFailure> LastThreeFailures { get; }

    public IReadOnlyList<EvidenceArtifact> EvidenceArtifacts { get; }

    public GlobalReviewBottleneckInput? Bottleneck { get; }

    public IReadOnlyList<GlobalReviewVolumeIntensityRecord> VolumeAndIntensityHistory { get; }

    public IReadOnlyList<GlobalReviewRecoveryRecord> RecoveryOrDeloadHistory { get; }

    public IReadOnlyList<GlobalReviewAdvancementRecord> Advancements { get; }

    public bool PauseTestsForDeload { get; }

    public bool OpenAdvancedBranch { get; }

    public bool AttemptTransferIntegrationTransfer { get; }
}

public sealed record GlobalReviewFailure(
    GlobalReviewFailureKind Kind,
    BranchCode? Branch,
    GlobalLevelId? Level,
    string Detail);

public sealed record GlobalReviewDecision(
    GlobalReviewDecisionKind Kind,
    BranchCode? Branch,
    string Detail);

public sealed record GlobalReviewEvaluationResult(
    bool Passed,
    IReadOnlyList<GlobalReviewFailure> Failures,
    IReadOnlyList<GlobalReviewDecision> Decisions);

public static class GlobalReviewDecisionCatalog
{
    public static IReadOnlyList<GlobalReviewDecisionKind> PossibleDecisions { get; } =
    [
        GlobalReviewDecisionKind.ContinueCurrentProgression,
        GlobalReviewDecisionKind.EmphasizeBottleneckBranch,
        GlobalReviewDecisionKind.RestoreDecayedBranch,
        GlobalReviewDecisionKind.OpenAdvancedBranch,
        GlobalReviewDecisionKind.PauseTestsForDeload,
        GlobalReviewDecisionKind.AttemptTransferIntegrationTransfer,
    ];
}

public static class GlobalReviewEvaluator
{
    public static GlobalReviewEvaluationResult Evaluate(GlobalReviewInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var failures = new List<GlobalReviewFailure>();
        var decisions = new List<GlobalReviewDecision>();

        EvaluateWholePractitionerInputs(input, failures);
        EvaluateDecayedBranches(input, failures, decisions);
        EvaluateMaintenanceCurrency(input, failures);
        EvaluateBottleneckResponse(input, failures, decisions);
        EvaluateCurrentTransferOrStabilizationEvidence(input, failures);
        EvaluateParticipationOnlyAdvancement(input, failures);
        AddDecisionInputs(input, decisions, failures.Count == 0);

        if (failures.Count == 0 && decisions.Count == 0)
        {
            decisions.Add(new GlobalReviewDecision(
                GlobalReviewDecisionKind.ContinueCurrentProgression,
                Branch: null,
                "Continue current progression."));
        }

        return new GlobalReviewEvaluationResult(
            Passed: failures.Count == 0,
            Failures: failures,
            Decisions: decisions);
    }

    private static void EvaluateWholePractitionerInputs(
        GlobalReviewInput input,
        ICollection<GlobalReviewFailure> failures)
    {
        var representedBranches = input.CurrentOwnedLevels
            .Select(level => level.Branch)
            .Distinct()
            .ToArray();

        if (Enum.GetValues<BranchCode>().Any(branch => !representedBranches.Contains(branch)))
        {
            failures.Add(new GlobalReviewFailure(
                GlobalReviewFailureKind.WholePractitionerInputMissing,
                Branch: null,
                Level: null,
                "Global review requires current owned level input for every branch."));
        }

        foreach (var ownedLevel in input.CurrentOwnedLevels)
        {
            if (ownedLevel.OwnedLevel is null)
            {
                continue;
            }

            var hasMaintenanceInput = input.MaintenanceStatus.Any(status =>
                status.Branch == ownedLevel.Branch &&
                status.OwnedLevel == ownedLevel.OwnedLevel.Value);

            if (hasMaintenanceInput)
            {
                continue;
            }

            failures.Add(new GlobalReviewFailure(
                GlobalReviewFailureKind.WholePractitionerInputMissing,
                ownedLevel.Branch,
                ownedLevel.OwnedLevel,
                $"{ownedLevel.Branch} {ownedLevel.OwnedLevel} maintenance input is missing."));
        }

        if (input.VolumeAndIntensityHistory.Count == 0)
        {
            failures.Add(new GlobalReviewFailure(
                GlobalReviewFailureKind.WholePractitionerInputMissing,
                Branch: null,
                Level: null,
                "Global review requires volume and intensity history."));
        }

        if (input.RecoveryOrDeloadHistory.Count == 0)
        {
            failures.Add(new GlobalReviewFailure(
                GlobalReviewFailureKind.WholePractitionerInputMissing,
                Branch: null,
                Level: null,
                "Global review requires recovery or deload history."));
        }
    }

    private static void EvaluateDecayedBranches(
        GlobalReviewInput input,
        ICollection<GlobalReviewFailure> failures,
        ICollection<GlobalReviewDecision> decisions)
    {
        var decayedBranches = input.PractitionerState.BranchLevels
            .Where(level => level.State == BranchLevelState.Decayed)
            .GroupBy(level => level.Branch)
            .Select(group => group.First())
            .ToArray();

        foreach (var decayed in decayedBranches)
        {
            failures.Add(new GlobalReviewFailure(
                GlobalReviewFailureKind.PrerequisiteBranchDecayed,
                decayed.Branch,
                decayed.Level,
                $"{decayed.Branch} is decayed and must be restored before the review can pass."));

            decisions.Add(new GlobalReviewDecision(
                GlobalReviewDecisionKind.RestoreDecayedBranch,
                decayed.Branch,
                $"Restore {decayed.Branch}."));
        }
    }

    private static void EvaluateMaintenanceCurrency(
        GlobalReviewInput input,
        ICollection<GlobalReviewFailure> failures)
    {
        foreach (var maintenance in input.MaintenanceStatus.Where(
            status => status.State is MaintenanceCurrencyState.Due or MaintenanceCurrencyState.Failed))
        {
            failures.Add(new GlobalReviewFailure(
                GlobalReviewFailureKind.MaintenanceCheckOverdue,
                maintenance.Branch,
                maintenance.OwnedLevel,
                $"{maintenance.Branch} {maintenance.OwnedLevel} maintenance is overdue."));
        }
    }

    private static void EvaluateBottleneckResponse(
        GlobalReviewInput input,
        ICollection<GlobalReviewFailure> failures,
        ICollection<GlobalReviewDecision> decisions)
    {
        if (input.Bottleneck is null || !input.Bottleneck.HasProgrammedResponse)
        {
            failures.Add(new GlobalReviewFailure(
                GlobalReviewFailureKind.BottleneckProgrammedResponseMissing,
                input.Bottleneck?.Branch,
                Level: null,
                "Bottleneck branch must have a programmed response."));
            return;
        }

        if (input.Bottleneck.NeedsEmphasis)
        {
            decisions.Add(new GlobalReviewDecision(
                GlobalReviewDecisionKind.EmphasizeBottleneckBranch,
                input.Bottleneck.Branch,
                $"Emphasize bottleneck branch {input.Bottleneck.Branch}."));
        }
    }

    private static void EvaluateCurrentTransferOrStabilizationEvidence(
        GlobalReviewInput input,
        ICollection<GlobalReviewFailure> failures)
    {
        var cycleDays = input.PractitionerCategory == PractitionerCategory.Beginner ? 42 : 28;
        var hasCurrentArtifact = input.EvidenceArtifacts.Any(artifact =>
            artifact.Category is EvidenceArtifactCategory.Transfer or EvidenceArtifactCategory.Stabilization &&
            ArtifactIsCurrent(artifact, input.AsOf, cycleDays));

        if (!hasCurrentArtifact)
        {
            failures.Add(new GlobalReviewFailure(
                GlobalReviewFailureKind.CurrentTransferOrStabilizationArtifactMissing,
                Branch: null,
                Level: null,
                "Global review requires a current transfer or stabilization artifact."));
        }
    }

    private static void EvaluateParticipationOnlyAdvancement(
        GlobalReviewInput input,
        ICollection<GlobalReviewFailure> failures)
    {
        foreach (var advancement in input.Advancements.Where(item => item.AdvancedByParticipationAlone))
        {
            failures.Add(new GlobalReviewFailure(
                GlobalReviewFailureKind.ParticipationOnlyAdvancement,
                advancement.Branch,
                advancement.Level,
                $"{advancement.Branch} {advancement.Level} advanced by participation alone."));
        }
    }

    private static void AddDecisionInputs(
        GlobalReviewInput input,
        ICollection<GlobalReviewDecision> decisions,
        bool reviewPassed)
    {
        if (input.PauseTestsForDeload)
        {
            decisions.Add(new GlobalReviewDecision(
                GlobalReviewDecisionKind.PauseTestsForDeload,
                Branch: null,
                "Pause advancement tests for deload."));
            return;
        }

        if (!reviewPassed)
        {
            return;
        }

        if (input.OpenAdvancedBranch)
        {
            decisions.Add(new GlobalReviewDecision(
                GlobalReviewDecisionKind.OpenAdvancedBranch,
                Branch: null,
                "Open an advanced branch."));
        }

        if (input.AttemptTransferIntegrationTransfer)
        {
            decisions.Add(new GlobalReviewDecision(
                GlobalReviewDecisionKind.AttemptTransferIntegrationTransfer,
                BranchCode.TI,
                "Attempt TI transfer."));
        }
    }

    private static bool ArtifactIsCurrent(
        EvidenceArtifact artifact,
        TrainingDate asOf,
        int cycleDays)
    {
        var ageDays = artifact.Date.DaysUntil(asOf);
        return ageDays >= 0 && ageDays <= cycleDays;
    }
}
