using Android.Content;
using Android.Widget;

namespace MentalGymnastics.Android;

internal class MgPanel : LinearLayout
{
    public MgPanel(Context context)
        : base(context)
    {
        Orientation = Orientation.Vertical;
        SetPadding(
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Lg),
            MgSpacing.Dp(context, MgSpacing.Lg));
        Background = MgTheme.Surface(context);
    }
}
