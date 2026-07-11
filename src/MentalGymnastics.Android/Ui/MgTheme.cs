using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;

namespace MentalGymnastics.Android;

internal static class MgTheme
{
    public static GradientDrawable Surface(Context context, int cornerRadius = 8)
    {
        return RoundedRectangle(
            context,
            MgColors.Surface,
            MgColors.HairlineSoft,
            strokeDp: 1,
            cornerRadius);
    }

    public static GradientDrawable MutedSurface(Context context, int cornerRadius = 8)
    {
        return RoundedRectangle(
            context,
            MgColors.SurfaceMuted,
            MgColors.HairlineSoft,
            strokeDp: 1,
            cornerRadius);
    }

    public static GradientDrawable TintedSurface(
        Context context,
        Color fillColor,
        Color strokeColor,
        int cornerRadius = 8)
    {
        return RoundedRectangle(
            context,
            fillColor,
            strokeColor,
            strokeDp: 1,
            cornerRadius);
    }

    public static GradientDrawable Outline(Context context, Color strokeColor, int cornerRadius = 8)
    {
        return RoundedRectangle(
            context,
            Color.Transparent,
            strokeColor,
            strokeDp: 2,
            cornerRadius);
    }

    public static GradientDrawable Filled(Context context, Color fillColor, int cornerRadius = 8)
    {
        return RoundedRectangle(
            context,
            fillColor,
            fillColor,
            strokeDp: 1,
            cornerRadius);
    }

    public static GradientDrawable ActionGradient(Context context, int cornerRadius = 8)
    {
        ArgumentNullException.ThrowIfNull(context);

        var drawable = new GradientDrawable(
            GradientDrawable.Orientation.LeftRight,
            [MgColors.TrainingDark.ToArgb(), MgColors.Training.ToArgb()]);
        drawable.SetShape(ShapeType.Rectangle);
        drawable.SetStroke(MgSpacing.Dp(context, 1), MgColors.TrainingDark);
        drawable.SetCornerRadius(MgSpacing.Dp(context, cornerRadius));
        return drawable;
    }

    public static GradientDrawable RoundedRectangle(
        Context context,
        Color fillColor,
        Color strokeColor,
        int strokeDp,
        int cornerRadius)
    {
        ArgumentNullException.ThrowIfNull(context);

        var drawable = new GradientDrawable();
        drawable.SetShape(ShapeType.Rectangle);
        drawable.SetColor(fillColor);
        drawable.SetStroke(MgSpacing.Dp(context, strokeDp), strokeColor);
        drawable.SetCornerRadius(MgSpacing.Dp(context, cornerRadius));

        return drawable;
    }
}
