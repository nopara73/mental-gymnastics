using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using MentalGymnastics.Core;

namespace MentalGymnastics.Android;

internal sealed class StateMarkerView : TextView
{
    public StateMarkerView(
        Context context,
        BranchLevelState state,
        bool maintenanceDue = false)
        : base(context)
    {
        Text = maintenanceDue ? "Due" : LabelTextFor(state);
        ContentDescription = $"State: {Text}";
        Gravity = GravityFlags.Center;
        SetMinWidth(MgSpacing.Dp(context, 72));
        SetPadding(
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Xs),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Xs));
        MgTypography.ApplyMicro(this);
        SetTextColor(maintenanceDue ? MgColors.Ink : TextColorFor(state));
        Background = maintenanceDue
            ? MgTheme.Outline(context, MgColors.Maintenance)
            : BackgroundFor(context, state);
    }

    public static string LabelTextFor(BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.Unopened => "Locked",
            BranchLevelState.Training => "Practice",
            BranchLevelState.TestReady => "Test next",
            BranchLevelState.PassedOnce => "1 of 3",
            BranchLevelState.Stabilizing => "Repeats",
            BranchLevelState.Owned => "Stable",
            BranchLevelState.Maintenance => "Recheck",
            BranchLevelState.Decayed => "Recheck",
            _ => state.ToString(),
        };
    }

    private static Color TextColorFor(BranchLevelState state)
    {
        return state is BranchLevelState.Owned ? Color.White : MgColors.Ink;
    }

    private static Drawable BackgroundFor(Context context, BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.Unopened => MgTheme.Outline(context, MgColors.Hairline),
            BranchLevelState.Training => MgTheme.Outline(context, MgColors.Training),
            BranchLevelState.TestReady => MgTheme.Outline(context, MgColors.TestReady),
            BranchLevelState.PassedOnce => MgTheme.Outline(context, MgColors.PassedOnce),
            BranchLevelState.Stabilizing => MgTheme.Outline(context, MgColors.TestReady),
            BranchLevelState.Owned => MgTheme.Filled(context, MgColors.Owned),
            BranchLevelState.Maintenance => MgTheme.Outline(context, MgColors.Maintenance),
            BranchLevelState.Decayed => MgTheme.Outline(context, MgColors.Blocked),
            _ => MgTheme.MutedSurface(context),
        };
    }
}
