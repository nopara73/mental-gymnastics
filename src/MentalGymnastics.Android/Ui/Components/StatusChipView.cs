using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace MentalGymnastics.Android;

internal sealed class StatusChipView : TextView
{
    public StatusChipView(Context context, string text, Color strokeColor, bool filled = false)
        : base(context)
    {
        Text = text;
        Gravity = GravityFlags.Center;
        SetMinWidth(MgSpacing.Dp(context, 44));
        SetPadding(
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Xs),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Xs));
        MgTypography.ApplyMicro(this);
        SetTextColor(filled ? Color.White : MgColors.Ink);
        Background = filled
            ? MgTheme.Filled(context, strokeColor, cornerRadius: 16)
            : MgTheme.Outline(context, strokeColor, cornerRadius: 16);
    }
}
