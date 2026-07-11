using MentalGymnastics.Core;

namespace MentalGymnastics.App;

internal sealed record DefaultTrainingWorkCandidate(
    CurrentTrainingStateNextWork WeeklyWork,
    BranchLevelStatus Status);

internal static class DefaultTrainingWorkPolicy
{
    public static DrillId PrimaryDrillFor(BranchCode branch, GlobalLevelId level)
    {
        return (branch, level) switch
        {
            (BranchCode.FH, _) when (int)level >= (int)GlobalLevelId.L3 => DrillId.FH2DistractorHold,
            (BranchCode.FS, _) when (int)level >= (int)GlobalLevelId.L3 => DrillId.FS2InvalidCueFilter,
            (BranchCode.WM, _) when (int)level >= (int)GlobalLevelId.L3 => DrillId.WM2MentalTransform,
            (BranchCode.IR, _) when (int)level >= (int)GlobalLevelId.L2 => DrillId.IR2ExceptionRule,
            (BranchCode.DE, _) when (int)level >= (int)GlobalLevelId.L3 => DrillId.DE2SeededAudit,
            (BranchCode.CO, _) when (int)level >= (int)GlobalLevelId.L3 => DrillId.CO2StructureMapping,
            (BranchCode.AI, _) when (int)level >= (int)GlobalLevelId.L3 => DrillId.AI2DisruptionRecovery,
            (BranchCode.TI, GlobalLevelId.L5) => DrillId.TI2GlobalReviewTask,
            (BranchCode.FH, _) => DrillId.FH1TargetHold,
            (BranchCode.FS, _) => DrillId.FS1CueSwitch,
            (BranchCode.WM, _) => DrillId.WM1DelayedReconstruction,
            (BranchCode.IR, _) => DrillId.IR1GoNoGoRule,
            (BranchCode.DE, _) => DrillId.DE1PairDiscrimination,
            (BranchCode.CO, _) => DrillId.CO1RuleExtraction,
            (BranchCode.AI, _) => DrillId.AI1PressureRepeat,
            (BranchCode.TI, _) => DrillId.TI1CompositeTask,
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "Unknown training branch."),
        };
    }

    public static DefaultTrainingWorkCandidate? Select(CurrentTrainingStateReadModel state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.AvailableNextWork
            .SelectMany((work, workIndex) => work.BranchEmphasis.Select((branch, branchIndex) => new
            {
                Work = work,
                WorkIndex = workIndex,
                BranchIndex = branchIndex,
                Status = SelectStatus(state.CurrentPractitionerState, branch),
            }))
            .Where(candidate => candidate.Status is not null)
            .Select(candidate => new
            {
                candidate.Work,
                candidate.WorkIndex,
                candidate.BranchIndex,
                Status = candidate.Status!.Value,
            })
            .OrderBy(candidate => StatusPriority(candidate.Status.State))
            .ThenBy(candidate => SessionPriority(candidate.Work.Session, candidate.Status.State))
            .ThenBy(candidate => candidate.Work.DayNumber)
            .ThenBy(candidate => candidate.WorkIndex)
            .ThenBy(candidate => candidate.BranchIndex)
            .ThenByDescending(candidate => LevelRank(candidate.Status.Level))
            .Select(candidate => new DefaultTrainingWorkCandidate(candidate.Work, candidate.Status))
            .FirstOrDefault();
    }

    public static BranchLevelStatus? SelectStatus(
        PractitionerState currentState,
        BranchCode branch)
    {
        return currentState.BranchLevels
            .Where(status => status.Branch == branch && status.State != BranchLevelState.Unopened)
            .OrderBy(status => StatusPriority(status.State))
            .ThenByDescending(status => LevelRank(status.Level))
            .FirstOrDefault();
    }

    public static AppTrainingSessionType SessionTypeFor(
        WeeklySessionKind weeklySession,
        BranchLevelState branchLevelState)
    {
        if (branchLevelState == BranchLevelState.Decayed)
        {
            return weeklySession is WeeklySessionKind.Recovery
                or WeeklySessionKind.RecoveryOrLightMaintenance
                or WeeklySessionKind.OffOrRecovery
                or WeeklySessionKind.RecoveryOrRetest
                    ? AppTrainingSessionType.Recovery
                    : AppTrainingSessionType.Regression;
        }

        if (weeklySession == WeeklySessionKind.RecoveryOrRetest &&
            branchLevelState == BranchLevelState.TestReady)
        {
            return AppTrainingSessionType.Test;
        }

        return weeklySession switch
        {
            WeeklySessionKind.Load => AppTrainingSessionType.Load,
            WeeklySessionKind.TestOrStabilization =>
                branchLevelState is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing
                    ? AppTrainingSessionType.Stabilization
                    : branchLevelState == BranchLevelState.TestReady
                        ? AppTrainingSessionType.Test
                        : AppTrainingSessionType.Practice,
            WeeklySessionKind.TransferOrStabilization =>
                branchLevelState is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing
                    ? AppTrainingSessionType.Stabilization
                    : branchLevelState is BranchLevelState.Owned or BranchLevelState.Maintenance
                        ? AppTrainingSessionType.Transfer
                        : AppTrainingSessionType.Practice,
            WeeklySessionKind.Transfer => AppTrainingSessionType.Transfer,
            WeeklySessionKind.Stabilization =>
                branchLevelState is BranchLevelState.PassedOnce or BranchLevelState.Stabilizing
                    ? AppTrainingSessionType.Stabilization
                    : AppTrainingSessionType.Practice,
            WeeklySessionKind.Maintenance => AppTrainingSessionType.Maintenance,
            WeeklySessionKind.Recovery
                or WeeklySessionKind.RecoveryOrLightMaintenance
                or WeeklySessionKind.OffOrRecovery
                or WeeklySessionKind.RecoveryOrRetest => AppTrainingSessionType.Recovery,
            _ => AppTrainingSessionType.Practice,
        };
    }

    public static SessionType CoreSessionTypeFor(AppTrainingSessionType sessionType)
    {
        return sessionType switch
        {
            AppTrainingSessionType.Practice => SessionType.Practice,
            AppTrainingSessionType.Load => SessionType.Load,
            AppTrainingSessionType.Test => SessionType.Test,
            AppTrainingSessionType.Stabilization => SessionType.Stabilization,
            AppTrainingSessionType.Regression => SessionType.Regression,
            AppTrainingSessionType.Transfer => SessionType.Transfer,
            AppTrainingSessionType.Recovery => SessionType.Recovery,
            AppTrainingSessionType.Maintenance => SessionType.Test,
            _ => throw new ArgumentOutOfRangeException(
                nameof(sessionType),
                sessionType,
                "Unknown app training session type."),
        };
    }

    public static IReadOnlyList<LoadVariable> DefaultLoadVariablesFor(
        BranchCode branch,
        GlobalLevelId level,
        AppTrainingSessionType sessionType)
    {
        var profile = TrainingLoadProfileCatalog.Get(branch, level);
        return ProgressiveLoadPlanner.Prescribe(
            profile,
            CoreSessionTypeFor(sessionType)).Stage.LoadVariables;
    }

    private static int SessionPriority(
        WeeklySessionKind session,
        BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.TestReady => session switch
            {
                WeeklySessionKind.TestOrStabilization => 0,
                WeeklySessionKind.RecoveryOrRetest => 1,
                WeeklySessionKind.Practice => 2,
                WeeklySessionKind.Load => 3,
                _ => 4,
            },
            BranchLevelState.PassedOnce or BranchLevelState.Stabilizing => session switch
            {
                WeeklySessionKind.TestOrStabilization
                    or WeeklySessionKind.TransferOrStabilization
                    or WeeklySessionKind.Stabilization => 0,
                WeeklySessionKind.Practice => 1,
                WeeklySessionKind.Load => 2,
                _ => 3,
            },
            BranchLevelState.Training => session switch
            {
                WeeklySessionKind.Practice => 0,
                WeeklySessionKind.Load => 1,
                _ => 2,
            },
            BranchLevelState.Maintenance or BranchLevelState.Owned => session switch
            {
                WeeklySessionKind.Maintenance => 0,
                WeeklySessionKind.Practice => 1,
                WeeklySessionKind.RecoveryOrLightMaintenance => 2,
                _ => 3,
            },
            BranchLevelState.Decayed => session switch
            {
                WeeklySessionKind.Recovery
                    or WeeklySessionKind.RecoveryOrLightMaintenance
                    or WeeklySessionKind.RecoveryOrRetest => 0,
                WeeklySessionKind.Maintenance => 1,
                _ => 2,
            },
            _ => 3,
        };
    }

    private static int StatusPriority(BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.TestReady => 0,
            BranchLevelState.PassedOnce or BranchLevelState.Stabilizing => 1,
            BranchLevelState.Training => 2,
            BranchLevelState.Maintenance => 3,
            BranchLevelState.Owned => 4,
            BranchLevelState.Decayed => 5,
            _ => 6,
        };
    }

    private static int LevelRank(GlobalLevelId level)
    {
        return ((int)level) + 1;
    }
}
