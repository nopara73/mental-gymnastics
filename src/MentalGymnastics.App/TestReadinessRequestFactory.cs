using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App;

internal static class TestReadinessRequestFactory
{
    public static TestReadinessRequest Create(
        PractitionerState practitionerState,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        IEnumerable<TestReadinessPracticeSession> recentPracticeSessions,
        IEnumerable<MaintenanceCurrencyResult> maintenanceCurrency)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);
        ArgumentNullException.ThrowIfNull(recentPracticeSessions);
        ArgumentNullException.ThrowIfNull(maintenanceCurrency);

        var standard = StandardFor(branch, level);
        var drillDefinition = DrillFor(drill);
        return new TestReadinessRequest(
            practitionerState,
            branch,
            level,
            drill,
            standard.Demand,
            recentPracticeSessions,
            maintenanceCurrency.Select(currency => new PrerequisiteMaintenanceCheck(
                currency.Branch,
                currency.OwnedLevel,
                currency.State == MaintenanceCurrencyState.Current)),
            standard.Standard,
            drillDefinition.HonestyConstraint);
    }

    public static IReadOnlyList<TestReadinessPracticeSession> FromSessionHistory(
        IEnumerable<LocalSessionHistoryRecord> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        return sessions
            .Where(session => session.SessionType is LocalCompletedSessionType.Practice or LocalCompletedSessionType.Load)
            .Where(session => session.Drill.HasValue)
            .SelectMany(session => session.BranchLevels
                .Where(branchLevel => IsFormalStandardLoad(session, branchLevel))
                .Select(branchLevel =>
                    new TestReadinessPracticeSession(
                        branchLevel.Branch,
                        branchLevel.Level,
                        session.Drill!.Value,
                        StandardFor(branchLevel.Branch, branchLevel.Level).Demand,
                        session.CleanPerformance)))
            .ToArray();
    }

    private static bool IsFormalStandardLoad(
        LocalSessionHistoryRecord session,
        LocalSessionBranchLevel branchLevel)
    {
        var profile = TrainingLoadProfileCatalog.Get(branchLevel.Branch, branchLevel.Level);
        return profile.Drill == session.Drill &&
            TrainingLoadStageStandardResolver.IsFormalStandardLoad(
                profile,
                session.LoadVariables);
    }

    private static BranchLevelStandard StandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards.Single(item => item.Branch == branch && item.Level == level);
    }

    private static DrillDefinition DrillFor(DrillId drill)
    {
        return ProgramCatalog.Drills.Single(item => item.Id == drill);
    }
}
