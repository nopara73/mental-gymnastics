using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Widget;

namespace MentalGymnastics.Android;

internal sealed class SessionActionButton : Button
{
    public SessionActionButton(Context context, string text, bool enabled)
        : base(context)
    {
        Text = text;
        Enabled = enabled;
        SetAllCaps(false);
        SetMinHeight(MgSpacing.Dp(context, 56));
        SetMinimumHeight(MgSpacing.Dp(context, 56));
        SetMinWidth(0);
        SetMinimumWidth(0);
        ContentDescription = enabled ? text : $"{text} unavailable";
        SetTextColor(enabled ? Color.White : MgColors.InkMuted);
        SetTextSize(ComplexUnitType.Sp, 15);
        SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
        Background = enabled
            ? MgTheme.ActionGradient(context)
            : MgTheme.MutedSurface(context);
        Elevation = enabled ? MgSpacing.Dp(context, 2) : 0;
        SetPadding(
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Md));
    }
}
