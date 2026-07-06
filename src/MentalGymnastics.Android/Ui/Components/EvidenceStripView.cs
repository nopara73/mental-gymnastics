using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace MentalGymnastics.Android;

internal sealed class EvidenceStripView : LinearLayout
{
    public EvidenceStripView(Context context, int recentSessionCount, int evidenceCount, int blockerCount)
        : base(context)
    {
        Orientation = Orientation.Horizontal;
        SetGravity(GravityFlags.CenterVertical);
        ContentDescription = $"Evidence summary: {recentSessionCount} sessions, {evidenceCount} artifacts, {blockerCount} blockers";

        AddChip(context, $"{recentSessionCount} sessions");
        AddChip(context, $"{evidenceCount} artifacts");
        AddChip(context, $"{blockerCount} blockers", blockerCount > 0 ? MgColors.Blocked : MgColors.Hairline);
    }

    private void AddChip(Context context, string label, Color? strokeColor = null)
    {
        var chip = new TextView(context)
        {
            Text = label,
            Gravity = GravityFlags.Center,
        };
        MgTypography.ApplyMicro(chip);
        chip.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Xs),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Xs));
        chip.Background = MgTheme.Outline(context, strokeColor ?? MgColors.Hairline, cornerRadius: 16);

        var layout = new LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(0, 0, MgSpacing.Dp(context, MgSpacing.Sm), 0);
        AddView(chip, layout);
    }
}
