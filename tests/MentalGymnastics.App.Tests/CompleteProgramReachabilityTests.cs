using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App.Tests;

public sealed class CompleteProgramReachabilityTests
{
    [Fact]
    public void PerfectPracticeCanReachAllFortyOwnedLevelsAndACompletedCurriculum()
    {
        var result = Simulate(maxDays: 2500);

        Assert.True(
            result.Completion.CurriculumComplete,
            $"Stopped after {result.CalendarDays} days with " +
            $"{result.Completion.EarnedLevelCount}/{result.Completion.TotalLevelCount} earned. " +
            $"States: {result.StateSummary}");
        Assert.Equal(40, result.Completion.EarnedLevelCount);
        var forecast = ProgramDurationForecastCatalog.FirstInstallPerfectPath;
        Assert.Equal(forecast.BestCaseCalendarDays, result.CalendarDays);
        Assert.Equal(forecast.BestCaseTrainingDays, result.TrainingDays);
        Assert.Equal(
            forecast.AverageMinutesPerTrainingDay,
            (int)Math.Ceiling(result.AverageMinutesPerTrainingDay));
        Assert.InRange(result.AverageMinutesPerTrainingDay, 1, 60);
        Assert.True(result.SessionCounts.Values.Sum() >= result.TrainingDays);
        Assert.All(
            ProgramDurationForecastCatalog.FirstInstallPerfectPathSessionsByDrill,
            expected => Assert.Equal(expected.Value, result.SessionCounts[expected.Key]));
        Console.WriteLine(
            $"Perfect-path curriculum: {result.CalendarDays} calendar days; " +
            $"{result.TrainingDays} training days; " +
            $"{result.SessionCounts.Values.Sum()} drill sessions; " +
            $"{result.AverageMinutesPerTrainingDay:F1} minutes per training day.");
        Console.WriteLine(
            "Perfect-path sessions by drill: " +
            string.Join(", ", result.SessionCounts
                .Where(pair => pair.Value > 0)
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}={pair.Value}")));
    }

    private static SimulationResult Simulate(int maxDays)
    {
        var state = InitialPractitionerStateFactory.Create();
        var started = new DateOnly(2026, 1, 1);
        var loadHistory = new Dictionary<(BranchCode Branch, GlobalLevelId Level), List<TrainingLoadHistoryEntry>>();
        var formalPassDates = new Dictionary<(BranchCode Branch, GlobalLevelId Level), TrainingDate>();
        var stabilizationPassDates = new Dictionary<(BranchCode Branch, GlobalLevelId Level), List<TrainingDate>>();
        var progressRecords = new LocalProgressRecords([], [], [], [], [], latestSummary: null);
        GlobalReviewResult? review = null;
        var trainingDays = 0;
        var totalMinutes = 0;
        var sessionsByDrill = Enum.GetValues<DrillId>()
            .ToDictionary(drill => drill, _ => 0);

        for (var dayIndex = 0; dayIndex < maxDays; dayIndex++)
        {
            var dateOnly = started.AddDays(dayIndex);
            var date = TrainingDate.From(dateOnly.Year, dateOnly.Month, dateOnly.Day);
            var maintenance = CurrentMaintenance(state);
            if (dayIndex >= 42)
            {
                review = new GlobalReviewResult(
                    passed: true,
                    ProgramCatalog.Branches.Select(branch =>
                        new GlobalReviewComponentScore(branch.Code, Passed: true)));
            }

            var category = PractitionerCategoryClassifier.Classify(
                new PractitionerCategoryClassificationRequest(state, maintenance, review));
            var weeklyRequest = CurrentTrainingStateLoader.BuildWeeklyProgrammingRequest(
                state,
                maintenance,
                category,
                progressRecords,
                globalReviewDecisions: [],
                recoveryRequired: false);
            var weeklyPlan = WeeklyProgrammingPlanner.Generate(weeklyRequest);
            var daily = DailyTrainingProgrammingPlanner.Prescribe(
                date,
                TrainingDate.From(started.Year, started.Month, started.Day),
                weeklyPlan);
            var candidates = Candidates(daily, state);
            if (candidates.Count > 0)
            {
                trainingDays++;
            }

            foreach (var status in candidates)
            {
                var sessionType = DefaultTrainingWorkPolicy.SessionTypeFor(
                    daily.Session,
                    status.State,
                    status.Level);
                var profile = TrainingLoadProfileCatalog.Get(status.Branch, status.Level);
                sessionsByDrill[profile.Drill]++;
                var key = (status.Branch, status.Level);
                if (!loadHistory.TryGetValue(key, out var history))
                {
                    history = [];
                    loadHistory[key] = history;
                }

                var load = ProgressiveLoadPlanner.Prescribe(
                    profile,
                    DefaultTrainingWorkPolicy.CoreSessionTypeFor(sessionType),
                    history);
                totalMinutes += TrainingDoseDurationEstimator.RoundedMinutes(
                    profile.Drill,
                    load.Stage.LoadVariables);

                state = ApplyPerfectResult(
                    state,
                    status,
                    sessionType,
                    profile,
                    load,
                    history,
                    date,
                    maintenance,
                    review,
                    formalPassDates,
                    stabilizationPassDates);
            }

            var completion = ProgramCompletionEvaluator.Evaluate(
                state,
                CurrentMaintenance(state),
                review);
            if (completion.CurriculumComplete)
            {
                return Result(
                    dayIndex + 1,
                    trainingDays,
                    totalMinutes,
                    state,
                    completion,
                    sessionsByDrill);
            }
        }

        return Result(
            maxDays,
            trainingDays,
            totalMinutes,
            state,
            ProgramCompletionEvaluator.Evaluate(state, CurrentMaintenance(state), review),
            sessionsByDrill);
    }

    private static PractitionerState ApplyPerfectResult(
        PractitionerState state,
        BranchLevelStatus status,
        AppTrainingSessionType sessionType,
        TrainingLoadProfile profile,
        ProgressiveLoadPrescription load,
        List<TrainingLoadHistoryEntry> history,
        TrainingDate date,
        IReadOnlyList<MaintenanceCurrencyResult> maintenance,
        GlobalReviewResult? review,
        IDictionary<(BranchCode Branch, GlobalLevelId Level), TrainingDate> formalPassDates,
        IDictionary<(BranchCode Branch, GlobalLevelId Level), List<TrainingDate>> stabilizationPassDates)
    {
        var key = (status.Branch, status.Level);
        BranchLevelStatusTransitionResult? transition = null;
        if (sessionType is AppTrainingSessionType.Practice or AppTrainingSessionType.Load)
        {
            history.Add(new TrainingLoadHistoryEntry(load.Stage.LoadVariables, Clean: true, Overload: false));
            if (status.State == BranchLevelState.Training &&
                history.Count(entry =>
                    entry.Clean &&
                    TrainingLoadStageStandardResolver.IsFormalStandardLoad(profile, entry.LoadVariables)) >= 2)
            {
                var readiness = TestReadinessEvaluator.Evaluate(new TestReadinessRequest(
                    state,
                    status.Branch,
                    status.Level,
                    profile.Drill,
                    ProgramCatalog.Standards.Single(item =>
                        item.Branch == status.Branch && item.Level == status.Level).Demand,
                    [
                        PerfectPractice(status, profile),
                        PerfectPractice(status, profile),
                    ],
                    maintenance.Select(item => new PrerequisiteMaintenanceCheck(
                        item.Branch,
                        item.OwnedLevel,
                        item.State == MaintenanceCurrencyState.Current)),
                    ProgramCatalog.Standards.Single(item =>
                        item.Branch == status.Branch && item.Level == status.Level).Standard,
                    ProgramCatalog.Drills.Single(item => item.Id == profile.Drill).HonestyConstraint));
                if (readiness.MayTest)
                {
                    transition = BranchLevelStateMachine.TryApply(
                        status,
                        BranchLevelTransition.MarkTestReady);
                }
            }
        }
        else if (sessionType == AppTrainingSessionType.Test ||
            sessionType == AppTrainingSessionType.Transfer && status.Level == GlobalLevelId.L4)
        {
            if (status.State == BranchLevelState.TestReady)
            {
                transition = BranchLevelStateMachine.TryApply(
                    status,
                    BranchLevelTransition.PassFormalTestOnce);
                formalPassDates[key] = date;
                stabilizationPassDates[key] = [];
            }
        }
        else if (sessionType == AppTrainingSessionType.Stabilization &&
            status.State is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing &&
            formalPassDates.TryGetValue(key, out var formalDate))
        {
            var dates = stabilizationPassDates[key];
            dates.Add(date);
            var evidence = new List<StabilizationPassEvidence>
            {
                StabilizationPass(status, formalDate, FormalTestPassState.PassOnce, controlled: false),
            };
            evidence.AddRange(dates.Select(passDate =>
                StabilizationPass(status, passDate, FormalTestPassState.StabilizationPass, controlled: true)));
            var ownership = StabilizationOwnershipEvaluator.Evaluate(
                new StabilizationEvidence(status.Branch, status.Level, evidence));
            if (ownership.Failures.Any(failure =>
                    failure.Kind == StabilizationOwnershipFailureKind.StabilizationWindowMissed))
            {
                transition = BranchLevelStateMachine.TryApply(
                    status,
                    BranchLevelTransition.FailStabilization);
                formalPassDates.Remove(key);
                stabilizationPassDates.Remove(key);
            }
            else
            {
                transition = ownership.IsOwned
                    ? BranchLevelStateMachine.TryApply(status, BranchLevelTransition.CompleteStabilization)
                    : ownership.BranchLevelState == BranchLevelState.Stabilizing
                        ? BranchLevelStateMachine.TryApply(status, BranchLevelTransition.EnterStabilization)
                        : null;
            }
        }

        if (transition is { IsValid: true })
        {
            state = Replace(state, transition.Value.NextStatus);
        }

        return PractitionerProgressionProjector.Project(
            new PractitionerProgressionProjectionRequest(
                state,
                CurrentMaintenance(state),
                review)).PractitionerState;
    }

    private static IReadOnlyList<BranchLevelStatus> Candidates(
        DailyTrainingPrescription daily,
        PractitionerState state)
    {
        if (daily.IsOff)
        {
            return [];
        }

        if (daily.Session == WeeklySessionKind.RecoveryOrRetest)
        {
            var retest = state.BranchLevels
                .Where(status => status.State is
                    BranchLevelState.PassedOnce or
                    BranchLevelState.Stabilizing or
                    BranchLevelState.TestReady)
                .OrderBy(status => status.State is
                    BranchLevelState.PassedOnce or BranchLevelState.Stabilizing ? 0 : 1)
                .ThenBy(status => status.Level)
                .ThenBy(status => status.Branch)
                .FirstOrDefault();
            if (retest != default)
            {
                return [retest];
            }
        }

        var emphasized = daily.BranchEmphasis
            .Select(branch => DefaultTrainingWorkPolicy.SelectStatus(state, branch))
            .OfType<BranchLevelStatus>()
            .DistinctBy(status => (status.Branch, status.Level))
            .ToArray();
        if (emphasized.Length > 0)
        {
            return emphasized;
        }

        var fallback = Enum.GetValues<BranchCode>()
            .Select(branch => DefaultTrainingWorkPolicy.SelectStatus(state, branch))
            .OfType<BranchLevelStatus>()
            .OrderBy(status => status.Branch)
            .FirstOrDefault();
        return fallback == default ? [] : [fallback];
    }

    private static TestReadinessPracticeSession PerfectPractice(
        BranchLevelStatus status,
        TrainingLoadProfile profile)
    {
        return new TestReadinessPracticeSession(
            status.Branch,
            status.Level,
            profile.Drill,
            ProgramCatalog.Standards.Single(item =>
                item.Branch == status.Branch && item.Level == status.Level).Demand,
            clean: true);
    }

    private static StabilizationPassEvidence StabilizationPass(
        BranchLevelStatus status,
        TrainingDate date,
        FormalTestPassState passState,
        bool controlled)
    {
        return new StabilizationPassEvidence(
            status.Branch,
            status.Level,
            date,
            ProgramCatalog.Standards.Single(item =>
                item.Branch == status.Branch && item.Level == status.Level).Standard,
            passState,
            new StandardEvaluationResult(Passed: true, Failures: []),
            controlled,
            "Documented main failure mode avoided.");
    }

    private static PractitionerState Replace(
        PractitionerState state,
        BranchLevelStatus replacement)
    {
        return new PractitionerState(state.BranchLevels.Select(status =>
            status.Branch == replacement.Branch && status.Level == replacement.Level
                ? replacement
                : status));
    }

    private static IReadOnlyList<MaintenanceCurrencyResult> CurrentMaintenance(
        PractitionerState state)
    {
        return MaintenanceScope.HighestEarnedByBranch(state)
            .Select(status => new MaintenanceCurrencyResult(
                status.Branch,
                status.Level,
                MaintenanceCurrencyState.Current,
                MaintenanceCurrencyEvaluator.CadenceFor(status.Branch, status.Level),
                DaysSinceLastPassingCheck: 1,
                ConsecutiveFailures: 0))
            .ToArray();
    }

    private static SimulationResult Result(
        int calendarDays,
        int trainingDays,
        int totalMinutes,
        PractitionerState state,
        ProgramCompletionResult completion,
        IReadOnlyDictionary<DrillId, int> sessionsByDrill)
    {
        var summary = string.Join(
            ", ",
            state.BranchLevels
                .Where(status => status.State is not BranchLevelState.Owned and not BranchLevelState.Maintenance)
                .OrderBy(status => status.Branch)
                .ThenBy(status => status.Level)
                .Select(status => $"{status.Branch}-{status.Level}:{status.State}"));
        return new SimulationResult(
            calendarDays,
            trainingDays,
            trainingDays == 0 ? 0 : (double)totalMinutes / trainingDays,
            completion,
            summary,
            sessionsByDrill);
    }

    private sealed record SimulationResult(
        int CalendarDays,
        int TrainingDays,
        double AverageMinutesPerTrainingDay,
        ProgramCompletionResult Completion,
        string StateSummary,
        IReadOnlyDictionary<DrillId, int> SessionCounts);
}
