using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class AntiSelfDeceptionGuardTests
{
    [Fact]
    public void BlocksAdvancementByParticipationEffortOrInsightAlone()
    {
        var result = AntiSelfDeceptionGuard.Evaluate(
            Request(
                evidence:
                [
                    AntiSelfDeceptionEvidenceKind.Participation,
                    AntiSelfDeceptionEvidenceKind.Effort,
                    AntiSelfDeceptionEvidenceKind.Insight,
                ]));

        Assert.False(result.AdvancementAllowed);
        Assert.Contains(result.Violations, violation => violation.Kind == AntiSelfDeceptionViolationKind.AdvancementByParticipation);
        Assert.Contains(result.Violations, violation => violation.Kind == AntiSelfDeceptionViolationKind.AdvancementByEffort);
        Assert.Contains(result.Violations, violation => violation.Kind == AntiSelfDeceptionViolationKind.AdvancementByInsight);
    }

    [Fact]
    public void BlocksNoveltyPresentedAsAdvancementWhenSourceDemandIsNotPreserved()
    {
        var result = AntiSelfDeceptionGuard.Evaluate(
            Request(
                evidence:
                [
                    AntiSelfDeceptionEvidenceKind.Novelty,
                    AntiSelfDeceptionEvidenceKind.ObservableArtifact,
                ],
                sourceDemandPreserved: false));

        Assert.False(result.AdvancementAllowed);
        Assert.Contains(result.Violations, violation => violation.Kind == AntiSelfDeceptionViolationKind.NoveltyPresentedAsAdvancement);
    }

    [Fact]
    public void BlocksSkippedPrerequisitesAndMissingEvidenceArtifacts()
    {
        var result = AntiSelfDeceptionGuard.Evaluate(
            Request(
                evidence: [AntiSelfDeceptionEvidenceKind.StandardPerformance],
                prerequisitesSatisfied: false,
                evidenceArtifactPresent: false));

        Assert.False(result.AdvancementAllowed);
        Assert.Contains(result.Violations, violation => violation.Kind == AntiSelfDeceptionViolationKind.SkippedPrerequisite);
        Assert.Contains(result.Violations, violation => violation.Kind == AntiSelfDeceptionViolationKind.MissingEvidence);
    }

    [Fact]
    public void BlocksChangedStandardsAndRemovedHonestyConstraints()
    {
        var result = AntiSelfDeceptionGuard.Evaluate(
            Request(
                evidence: [AntiSelfDeceptionEvidenceKind.StandardPerformance],
                standardUnchanged: false,
                honestyConstraintsPreserved: false));

        Assert.False(result.AdvancementAllowed);
        Assert.Contains(result.Violations, violation => violation.Kind == AntiSelfDeceptionViolationKind.ChangedStandard);
        Assert.Contains(result.Violations, violation => violation.Kind == AntiSelfDeceptionViolationKind.RemovedHonestyConstraint);
    }

    [Fact]
    public void BlocksAvoidedRetestsWhenRetestIsRequiredForTheProposedAdvance()
    {
        var result = AntiSelfDeceptionGuard.Evaluate(
            Request(
                proposedOutcome: GateOutcome.Own,
                evidence:
                [
                    AntiSelfDeceptionEvidenceKind.StandardPerformance,
                    AntiSelfDeceptionEvidenceKind.ObservableArtifact,
                ],
                retestRequired: true,
                retestCompleted: false));

        Assert.False(result.AdvancementAllowed);
        Assert.Contains(result.Violations, violation => violation.Kind == AntiSelfDeceptionViolationKind.RetestAvoided);
    }

    [Fact]
    public void AllowsAdvancementOnlyWhenObservableEvidencePrerequisitesStandardsConstraintsAndRetestsHold()
    {
        var result = AntiSelfDeceptionGuard.Evaluate(
            Request(
                proposedOutcome: GateOutcome.Own,
                evidence:
                [
                    AntiSelfDeceptionEvidenceKind.StandardPerformance,
                    AntiSelfDeceptionEvidenceKind.StabilizationRetest,
                    AntiSelfDeceptionEvidenceKind.ObservableArtifact,
                ],
                retestRequired: true,
                retestCompleted: true));

        Assert.True(result.AdvancementAllowed);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void DoesNotBlockNonAdvancementOutcomes()
    {
        var result = AntiSelfDeceptionGuard.Evaluate(
            Request(
                proposedOutcome: GateOutcome.Fail,
                evidence: [AntiSelfDeceptionEvidenceKind.Participation],
                prerequisitesSatisfied: false,
                evidenceArtifactPresent: false,
                standardUnchanged: false,
                honestyConstraintsPreserved: false));

        Assert.True(result.AdvancementAllowed);
        Assert.Empty(result.Violations);
    }

    private static AntiSelfDeceptionGuardRequest Request(
        GateOutcome proposedOutcome = GateOutcome.PassOnce,
        IEnumerable<AntiSelfDeceptionEvidenceKind>? evidence = null,
        bool prerequisitesSatisfied = true,
        bool evidenceArtifactPresent = true,
        bool standardUnchanged = true,
        bool honestyConstraintsPreserved = true,
        bool sourceDemandPreserved = true,
        bool retestRequired = false,
        bool retestCompleted = false)
    {
        return new AntiSelfDeceptionGuardRequest(
            proposedOutcome,
            evidence ?? [AntiSelfDeceptionEvidenceKind.StandardPerformance, AntiSelfDeceptionEvidenceKind.ObservableArtifact],
            prerequisitesSatisfied,
            evidenceArtifactPresent,
            standardUnchanged,
            honestyConstraintsPreserved,
            sourceDemandPreserved,
            retestRequired,
            retestCompleted);
    }
}
