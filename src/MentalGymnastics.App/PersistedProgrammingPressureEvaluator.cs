using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App;

internal sealed record PersistedProgrammingPressureResult(
    RecoveryDecisionResult? RecoveryDecision,
    DeloadDecisionResult DeloadDecision)
{
    public bool RecoveryRequired =>
        RecoveryDecision?.ShouldRecover == true || DeloadDecision.ShouldDeload;
}

internal static class PersistedProgrammingPressureEvaluator
{
    public static PersistedProgrammingPressureResult Evaluate(
        TrainingDate asOf,
        PractitionerState practitionerState,
        IReadOnlyList<LocalSessionHistoryRecord> recentSessions,
        LocalProgressRecords progressRecords)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);
        ArgumentNullException.ThrowIfNull(recentSessions);
        ArgumentNullException.ThrowIfNull(progressRecords);

        var statuses = practitionerState.BranchLevels
            .Where(status => status.State != BranchLevelState.Unopened)
            .GroupBy(status => status.Branch)
            .Select(group => group
                .OrderByDescending(status => (int)status.Level)
                .ThenBy(status => status.State == BranchLevelState.Decayed ? 1 : 0)
                .First())
            .OrderBy(status => status.Branch)
            .ToArray();
        var recoveries = statuses
            .Select(status => RecoveryDecisionEvaluator.Evaluate(BuildRecoveryRequest(
                asOf,
                status,
                practitionerState,
                recentSessions,
                progressRecords)))
            .Where(result => result.ShouldRecover)
            .OrderByDescending(result => result.Triggers.Count)
            .ThenBy(result => result.Branch)
            .ToArray();
        var deload = DeloadDecisionEvaluator.Evaluate(BuildDeloadRequest(
            asOf,
            practitionerState,
            recentSessions,
            progressRecords));

        return new PersistedProgrammingPressureResult(recoveries.FirstOrDefault(), deload);
    }

    private static RecoveryDecisionRequest BuildRecoveryRequest(
        TrainingDate asOf,
        BranchLevelStatus status,
        PractitionerState practitionerState,
        IReadOnlyList<LocalSessionHistoryRecord> recentSessions,
        LocalProgressRecords progressRecords)
    {
        var latestRecovery = recentSessions
            .Where(session =>
                (session.SessionType == LocalCompletedSessionType.Recovery || session.RecoveryMarked) &&
                session.BranchLevels.Any(level => level.Branch == status.Branch))
            .OrderByDescending(session => session.Date.Year)
            .ThenByDescending(session => session.Date.Month)
            .ThenByDescending(session => session.Date.Day)
            .Select(session => (TrainingDate?)session.Date)
            .FirstOrDefault();
        var matchingSessions = recentSessions
            .Where(session =>
                InRecentWindow(session.Date, asOf, 7) &&
                (latestRecovery is null || IsAfter(session.Date, latestRecovery.Value)) &&
                session.BranchLevels.Contains(new LocalSessionBranchLevel(status.Branch, status.Level)))
            .OrderBy(session => session.Date.Year)
            .ThenBy(session => session.Date.Month)
            .ThenBy(session => session.Date.Day)
            .ThenBy(session => session.SessionId, StringComparer.Ordinal)
            .ToArray();
        var overloadDates = progressRecords.FormalTestAttempts
            .Where(record =>
                record.Attempt.Branch == status.Branch &&
                record.Attempt.Level == status.Level &&
                record.Attempt.FailureType == FailureType.Overload &&
                (latestRecovery is null || IsAfter(record.Attempt.Date, latestRecovery.Value)) &&
                InRecentWindow(record.Attempt.Date, asOf, 7))
            .Select(record => record.Attempt.Date)
            .ToHashSet();
        var setResults = matchingSessions
            .GroupBy(session => session.Date)
            .SelectMany(group => group.Select((session, index) => new RecoverySetResultEvidence(
                status.Branch,
                status.Level,
                session.Date,
                index + 1,
                failedFromOverload: !session.CleanPerformance &&
                    (session.SessionType == LocalCompletedSessionType.Load || overloadDates.Contains(session.Date)))))
            .ToArray();
        var errorTrends = matchingSessions
            .Zip(matchingSessions.Skip(1), (previous, current) => (previous, current))
            .Where(pair => SameLoad(pair.previous.LoadVariables, pair.current.LoadVariables))
            .Select(pair => new RecoveryErrorTrendEvidence(
                status.Branch,
                status.Level,
                pair.previous.CleanPerformance ? 0 : 1,
                pair.current.CleanPerformance ? 0 : 1,
                loadUnchanged: true))
            .ToArray();
        var honesty = progressRecords.FormalTestAttempts
            .Where(record =>
                record.Attempt.Branch == status.Branch &&
                record.Attempt.Level == status.Level &&
                (latestRecovery is null || IsAfter(record.Attempt.Date, latestRecovery.Value)) &&
                InRecentWindow(record.Attempt.Date, asOf, 7))
            .Select(record => new RecoveryHonestyConstraintEvidence(
                status.Branch,
                status.Level,
                Broken: record.Attempt.FailureType == FailureType.EffortFailure))
            .ToArray();
        var adjacentDecay = practitionerState.BranchLevels
            .Where(candidate => candidate.Branch != status.Branch)
            .Select(candidate => new AdjacentBranchDecayEvidence(
                status.Branch,
                candidate.Branch,
                candidate.State == BranchLevelState.Decayed))
            .ToArray();
        var recentHighIntensityTests = matchingSessions
            .Where(session =>
                session.Date.DaysUntil(asOf) <= 1 &&
                session.Intensity == LocalSessionIntensity.High &&
                session.SessionType is LocalCompletedSessionType.Test or
                    LocalCompletedSessionType.Stabilization or
                    LocalCompletedSessionType.Transfer)
            .Select(session => new RecentHighIntensityTestEvidence(
                status.Branch,
                status.Level,
                TrainingIntensityKind.High,
                Math.Clamp(session.Date.DaysUntil(asOf) * 24, 0, 24)))
            .ToArray();
        var profile = TrainingLoadProfileCatalog.Get(status.Branch, status.Level);
        var variableName = profile.TargetStage.IncreasedVariable ??
            profile.TargetStage.LoadVariables[0].Name;
        var drill = ProgramCatalog.Drills.Single(item => item.Id == profile.Drill);

        return new RecoveryDecisionRequest(
            status.Branch,
            status.Level,
            TrainingLoadProfileCatalog.KindFor(variableName),
            drill.HonestyConstraint,
            setResults,
            errorTrends,
            honesty,
            adjacentDecay,
            recentHighIntensityTests,
            subjectiveNotes: []);
    }

    private static DeloadDecisionRequest BuildDeloadRequest(
        TrainingDate asOf,
        PractitionerState practitionerState,
        IReadOnlyList<LocalSessionHistoryRecord> recentSessions,
        LocalProgressRecords progressRecords)
    {
        var weekStart = AddDays(asOf, -6);
        var latestDeload = recentSessions
            .Where(session => session.DeloadMarked)
            .OrderByDescending(session => session.Date.Year)
            .ThenByDescending(session => session.Date.Month)
            .ThenByDescending(session => session.Date.Day)
            .Select(session => (TrainingDate?)session.Date)
            .FirstOrDefault();
        var completedDeloadBoundary = latestDeload is { } deloadStart &&
            deloadStart.DaysUntil(asOf) >= 7
                ? deloadStart
                : (TrainingDate?)null;
        var branches = Enum.GetValues<BranchCode>();
        var evidence = branches.Select(branch =>
        {
            var overload = progressRecords.FormalTestAttempts.Any(record =>
                    record.Attempt.Branch == branch &&
                    record.Attempt.FailureType == FailureType.Overload &&
                    (completedDeloadBoundary is null || IsAfter(record.Attempt.Date, completedDeloadBoundary.Value)) &&
                    InRecentWindow(record.Attempt.Date, asOf, 7)) ||
                recentSessions.Any(session =>
                    InRecentWindow(session.Date, asOf, 7) &&
                    (completedDeloadBoundary is null || IsAfter(session.Date, completedDeloadBoundary.Value)) &&
                    !session.CleanPerformance &&
                    session.SessionType == LocalCompletedSessionType.Load &&
                    session.BranchLevels.Any(level => level.Branch == branch));
            var decay = practitionerState.BranchLevels.Any(status =>
                status.Branch == branch && status.State == BranchLevelState.Decayed);
            return new DeloadBranchWeekEvidence(branch, weekStart, overload, decay);
        });

        var activeDeloadStartedOn = latestDeload is { } startedOn &&
            startedOn.DaysUntil(asOf) is >= 0 and < 7
                ? startedOn
                : (TrainingDate?)null;
        return new DeloadDecisionRequest(
            weekStart,
            evidence,
            subjectiveNotes: [],
            activeDeloadStartedOn,
            asOf);
    }

    private static bool SameLoad(
        IReadOnlyList<LoadVariable> left,
        IReadOnlyList<LoadVariable> right)
    {
        return left.Count == right.Count && left.All(variable => right.Any(candidate =>
            string.Equals(candidate.Name, variable.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Value, variable.Value, StringComparison.Ordinal)));
    }

    private static bool InRecentWindow(TrainingDate date, TrainingDate asOf, int days)
    {
        var age = date.DaysUntil(asOf);
        return age >= 0 && age < days;
    }

    private static bool IsAfter(TrainingDate candidate, TrainingDate boundary)
    {
        return boundary.DaysUntil(candidate) > 0;
    }

    private static TrainingDate AddDays(TrainingDate date, int days)
    {
        var value = new DateOnly(date.Year, date.Month, date.Day).AddDays(days);
        return TrainingDate.From(value.Year, value.Month, value.Day);
    }
}
