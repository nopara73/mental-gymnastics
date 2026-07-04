using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public sealed class LocalProgramStateSnapshot
{
    public LocalProgramStateSnapshot(
        PractitionerState practitionerState,
        IEnumerable<EvidenceArtifact> evidenceArtifacts,
        IEnumerable<FormalTestAttempt> formalTestAttempts,
        IEnumerable<MaintenanceCheckEvidence> maintenanceChecks,
        IEnumerable<ClassifiedFailure> classifiedFailures)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);
        ArgumentNullException.ThrowIfNull(evidenceArtifacts);
        ArgumentNullException.ThrowIfNull(formalTestAttempts);
        ArgumentNullException.ThrowIfNull(maintenanceChecks);
        ArgumentNullException.ThrowIfNull(classifiedFailures);

        PractitionerState = practitionerState;
        EvidenceArtifacts = evidenceArtifacts.ToArray();
        FormalTestAttempts = formalTestAttempts.ToArray();
        MaintenanceChecks = maintenanceChecks.ToArray();
        ClassifiedFailures = classifiedFailures.ToArray();
    }

    public PractitionerState PractitionerState { get; }

    public IReadOnlyList<EvidenceArtifact> EvidenceArtifacts { get; }

    public IReadOnlyList<FormalTestAttempt> FormalTestAttempts { get; }

    public IReadOnlyList<MaintenanceCheckEvidence> MaintenanceChecks { get; }

    public IReadOnlyList<ClassifiedFailure> ClassifiedFailures { get; }
}
