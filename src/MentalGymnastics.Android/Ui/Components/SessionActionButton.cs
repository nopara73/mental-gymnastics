using Android.Content;
using Android.Graphics;
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
        SetMinHeight(0);
        SetMinimumHeight(0);
        SetMinWidth(0);
        SetMinimumWidth(0);
        ContentDescription = enabled ? text : $"{text} unavailable";
        SetTextColor(enabled ? Color.White : MgColors.InkMuted);
        Background = enabled
            ? MgTheme.Filled(context, MgColors.Training)
            : MgTheme.MutedSurface(context);
        SetPadding(
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Md));
    }
}
