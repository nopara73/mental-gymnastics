using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using MentalGymnastics.Core;

namespace MentalGymnastics.Android;

internal sealed class LevelCellView : LinearLayout
{
    public LevelCellView(
        Context context,
        BranchLevelStatus status,
        bool blocked = false,
        bool dueMaintenance = false,
        bool nextWork = false,
        bool selected = false)
        : base(context)
    {
        Orientation = Orientation.Vertical;
        SetGravity(GravityFlags.Center);
        ContentDescription = BuildContentDescription(status, blocked, dueMaintenance, nextWork);
        SetPadding(
            MgSpacing.Dp(context, MgSpacing.Xs),
            MgSpacing.Dp(context, MgSpacing.Xs),
            MgSpacing.Dp(context, MgSpacing.Xs),
            MgSpacing.Dp(context, MgSpacing.Xs));
        Background = selected ? MgTheme.Outline(context, MgColors.Ink, cornerRadius: 8) : null;

        var label = new TextView(context)
        {
            Text = status.Level.ToString(),
            Gravity = GravityFlags.Center,
        };
        MgTypography.ApplyMicro(label);

        var marker = new TextView(context)
        {
            Text = MarkerTextFor(status.State),
            Gravity = GravityFlags.Center,
            Background = DotBackground(context, status.State),
        };
        MgTypography.ApplyMicro(marker);
        marker.SetTextColor(TextColorFor(status.State));
        marker.SetMinWidth(MgSpacing.Dp(context, 30));
        marker.SetMinHeight(MgSpacing.Dp(context, 26));
        marker.LayoutParameters = new LayoutParams(
            MgSpacing.Dp(context, 34),
            MgSpacing.Dp(context, 28));

        AddView(marker);
        AddView(label);

        var overlayRow = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        overlayRow.SetGravity(GravityFlags.Center);
        overlayRow.SetMinimumHeight(MgSpacing.Dp(context, 8));
        if (blocked)
        {
            overlayRow.AddView(OverlayDot(context, MgColors.Blocked, widthDp: 20));
        }

        if (dueMaintenance)
        {
            overlayRow.AddView(OverlayDot(context, MgColors.Maintenance, widthDp: 12));
        }

        if (nextWork)
        {
            overlayRow.AddView(OverlayDot(context, MgColors.Training, widthDp: 8));
        }

        AddView(overlayRow);
    }

    private static Drawable DotBackground(Context context, BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.Unopened => MgTheme.Outline(context, MgColors.Hairline, cornerRadius: 6),
            BranchLevelState.Training => MgTheme.Outline(context, MgColors.Training, cornerRadius: 16),
            BranchLevelState.TestReady => MgTheme.Outline(context, MgColors.TestReady, cornerRadius: 6),
            BranchLevelState.PassedOnce => MgTheme.Outline(context, MgColors.PassedOnce, cornerRadius: 16),
            BranchLevelState.Stabilizing => MgTheme.Outline(context, MgColors.TestReady, cornerRadius: 16),
            BranchLevelState.Owned => MgTheme.Filled(context, MgColors.Owned, cornerRadius: 16),
            BranchLevelState.Maintenance => MgTheme.Outline(context, MgColors.Maintenance, cornerRadius: 16),
            BranchLevelState.Decayed => MgTheme.Outline(context, MgColors.Blocked, cornerRadius: 4),
            _ => MgTheme.MutedSurface(context, cornerRadius: 16),
        };
    }

    private static string MarkerTextFor(BranchLevelState state)
    {
        return state switch
        {
            BranchLevelState.Unopened => "",
            BranchLevelState.Training => "",
            BranchLevelState.TestReady => "T",
            BranchLevelState.PassedOnce => "1",
            BranchLevelState.Stabilizing => "S",
            BranchLevelState.Owned => "O",
            BranchLevelState.Maintenance => "M",
            BranchLevelState.Decayed => "D",
            _ => "",
        };
    }

    private static Color TextColorFor(BranchLevelState state)
    {
        return state == BranchLevelState.Owned ? Color.White : MgColors.Ink;
    }

    private static View OverlayDot(Context context, Color color, int widthDp)
    {
        var dot = new View(context)
        {
            Background = MgTheme.Filled(context, color, cornerRadius: 4),
        };
        var layout = new LayoutParams(
            MgSpacing.Dp(context, widthDp),
            MgSpacing.Dp(context, 4));
        layout.SetMargins(MgSpacing.Dp(context, 1), MgSpacing.Dp(context, 2), MgSpacing.Dp(context, 1), 0);
        dot.LayoutParameters = layout;
        return dot;
    }

    private static string BuildContentDescription(
        BranchLevelStatus status,
        bool blocked,
        bool dueMaintenance,
        bool nextWork)
    {
        var overlays = new List<string>();
        if (blocked)
        {
            overlays.Add("blocked");
        }

        if (dueMaintenance)
        {
            overlays.Add("maintenance due");
        }

        if (nextWork)
        {
            overlays.Add("next prescribed work");
        }

        return overlays.Count == 0
            ? $"{status.Branch} {status.Level}, {status.State}"
            : $"{status.Branch} {status.Level}, {status.State}, {string.Join(", ", overlays)}";
    }
}
