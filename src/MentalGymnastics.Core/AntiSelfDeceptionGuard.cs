namespace MentalGymnastics.Core;

public sealed class AntiSelfDeceptionGuardRequest
{
    public AntiSelfDeceptionGuardRequest(
        GateOutcome proposedOutcome,
        IEnumerable<AntiSelfDeceptionEvidenceKind> evidence,
        bool prerequisitesSatisfied,
        bool evidenceArtifactPresent,
        bool standardUnchanged,
        bool honestyConstraintsPreserved,
        bool sourceDemandPreserved,
        bool retestRequired,
        bool retestCompleted)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        ProposedOutcome = proposedOutcome;
        Evidence = evidence.Distinct().ToArray();
        PrerequisitesSatisfied = prerequisitesSatisfied;
        EvidenceArtifactPresent = evidenceArtifactPresent;
        StandardUnchanged = standardUnchanged;
        HonestyConstraintsPreserved = honestyConstraintsPreserved;
        SourceDemandPreserved = sourceDemandPreserved;
        RetestRequired = retestRequired;
        RetestCompleted = retestCompleted;
    }

    public GateOutcome ProposedOutcome { get; }

    public IReadOnlyList<AntiSelfDeceptionEvidenceKind> Evidence { get; }

    public bool PrerequisitesSatisfied { get; }

    public bool EvidenceArtifactPresent { get; }

    public bool StandardUnchanged { get; }

    public bool HonestyConstraintsPreserved { get; }

    public bool SourceDemandPreserved { get; }

    public bool RetestRequired { get; }

    public bool RetestCompleted { get; }
}

public sealed record AntiSelfDeceptionViolation(
    AntiSelfDeceptionViolationKind Kind,
    string Detail);

public sealed record AntiSelfDeceptionGuardResult(
    bool AdvancementAllowed,
    IReadOnlyList<AntiSelfDeceptionViolation> Violations);

public static class AntiSelfDeceptionGuard
{
    public static AntiSelfDeceptionGuardResult Evaluate(AntiSelfDeceptionGuardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsAdvancementOutcome(request.ProposedOutcome))
        {
            return new AntiSelfDeceptionGuardResult(AdvancementAllowed: true, []);
        }

        var violations = new List<AntiSelfDeceptionViolation>();

        EvaluateSubjectiveEvidenceAlone(request, violations);
        EvaluateNovelty(request, violations);
        EvaluatePrerequisites(request, violations);
        EvaluateEvidenceArtifact(request, violations);
        EvaluateStandardIntegrity(request, violations);
        EvaluateHonestyConstraints(request, violations);
        EvaluateRetest(request, violations);

        return new AntiSelfDeceptionGuardResult(
            AdvancementAllowed: violations.Count == 0,
            violations);
    }

    private static void EvaluateSubjectiveEvidenceAlone(
        AntiSelfDeceptionGuardRequest request,
        ICollection<AntiSelfDeceptionViolation> violations)
    {
        if (HasCapacityEvidence(request.Evidence))
        {
            return;
        }

        AddIfPresent(
            request,
            violations,
            AntiSelfDeceptionEvidenceKind.Participation,
            AntiSelfDeceptionViolationKind.AdvancementByParticipation,
            "Participation cannot produce advancement without demonstrated standard performance.");
        AddIfPresent(
            request,
            violations,
            AntiSelfDeceptionEvidenceKind.Effort,
            AntiSelfDeceptionViolationKind.AdvancementByEffort,
            "Effort cannot produce advancement without demonstrated capacity under constraint.");
        AddIfPresent(
            request,
            violations,
            AntiSelfDeceptionEvidenceKind.Insight,
            AntiSelfDeceptionViolationKind.AdvancementByInsight,
            "Insight must appear as repeatable performance before it can support advancement.");
    }

    private static void EvaluateNovelty(
        AntiSelfDeceptionGuardRequest request,
        ICollection<AntiSelfDeceptionViolation> violations)
    {
        if (request.Evidence.Contains(AntiSelfDeceptionEvidenceKind.Novelty) &&
            (!request.SourceDemandPreserved || !request.Evidence.Contains(AntiSelfDeceptionEvidenceKind.TransferPerformance)))
        {
            violations.Add(new AntiSelfDeceptionViolation(
                AntiSelfDeceptionViolationKind.NoveltyPresentedAsAdvancement,
                "Novelty is not advancement unless the trained demand remains visible in transfer performance."));
        }
    }

    private static void EvaluatePrerequisites(
        AntiSelfDeceptionGuardRequest request,
        ICollection<AntiSelfDeceptionViolation> violations)
    {
        if (!request.PrerequisitesSatisfied)
        {
            violations.Add(new AntiSelfDeceptionViolation(
                AntiSelfDeceptionViolationKind.SkippedPrerequisite,
                "Prerequisites must be satisfied before advancement."));
        }
    }

    private static void EvaluateEvidenceArtifact(
        AntiSelfDeceptionGuardRequest request,
        ICollection<AntiSelfDeceptionViolation> violations)
    {
        if (!request.EvidenceArtifactPresent ||
            !request.Evidence.Contains(AntiSelfDeceptionEvidenceKind.ObservableArtifact) ||
            !HasCapacityEvidence(request.Evidence))
        {
            violations.Add(new AntiSelfDeceptionViolation(
                AntiSelfDeceptionViolationKind.MissingEvidence,
                "Advancement requires an observable evidence artifact."));
        }
    }

    private static void EvaluateStandardIntegrity(
        AntiSelfDeceptionGuardRequest request,
        ICollection<AntiSelfDeceptionViolation> violations)
    {
        if (!request.StandardUnchanged)
        {
            violations.Add(new AntiSelfDeceptionViolation(
                AntiSelfDeceptionViolationKind.ChangedStandard,
                "The standard cannot be changed to make an attempt pass."));
        }
    }

    private static void EvaluateHonestyConstraints(
        AntiSelfDeceptionGuardRequest request,
        ICollection<AntiSelfDeceptionViolation> violations)
    {
        if (!request.HonestyConstraintsPreserved)
        {
            violations.Add(new AntiSelfDeceptionViolation(
                AntiSelfDeceptionViolationKind.RemovedHonestyConstraint,
                "The honesty constraint cannot be removed from the task or regression."));
        }
    }

    private static void EvaluateRetest(
        AntiSelfDeceptionGuardRequest request,
        ICollection<AntiSelfDeceptionViolation> violations)
    {
        if (request.RetestRequired && !request.RetestCompleted)
        {
            violations.Add(new AntiSelfDeceptionViolation(
                AntiSelfDeceptionViolationKind.RetestAvoided,
                "Required retesting must be completed before advancement."));
        }
    }

    private static bool IsAdvancementOutcome(GateOutcome outcome)
    {
        return outcome is GateOutcome.PassOnce or GateOutcome.Stabilize or GateOutcome.Own;
    }

    private static bool HasCapacityEvidence(IReadOnlyList<AntiSelfDeceptionEvidenceKind> evidence)
    {
        return evidence.Any(item =>
            item is AntiSelfDeceptionEvidenceKind.StandardPerformance or
                AntiSelfDeceptionEvidenceKind.StabilizationRetest or
                AntiSelfDeceptionEvidenceKind.TransferPerformance);
    }

    private static void AddIfPresent(
        AntiSelfDeceptionGuardRequest request,
        ICollection<AntiSelfDeceptionViolation> violations,
        AntiSelfDeceptionEvidenceKind evidenceKind,
        AntiSelfDeceptionViolationKind violationKind,
        string detail)
    {
        if (request.Evidence.Contains(evidenceKind))
        {
            violations.Add(new AntiSelfDeceptionViolation(violationKind, detail));
        }
    }
}
