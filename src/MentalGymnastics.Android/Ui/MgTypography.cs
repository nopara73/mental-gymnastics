using Android.Graphics;
using Android.Util;

namespace MentalGymnastics.Android;

internal static class MgTypography
{
    public const float TitleSp = 28;
    public const float HeadingSp = 20;
    public const float BodySp = 16;
    public const float LabelSp = 13;
    public const float MicroSp = 12;
    public const float AppTitleSp = 20;
    public const float AppSubtitleSp = 13;

    public static void ApplyTitle(TextView view)
    {
        Apply(view, TitleSp, TypefaceStyle.Bold, MgColors.Ink);
    }

    public static void ApplyHeading(TextView view)
    {
        Apply(view, HeadingSp, TypefaceStyle.Bold, MgColors.Ink);
    }

    public static void ApplyBody(TextView view)
    {
        Apply(view, BodySp, TypefaceStyle.Normal, MgColors.Ink);
    }

    public static void ApplyLabel(TextView view)
    {
        Apply(view, LabelSp, TypefaceStyle.Bold, MgColors.InkMuted);
    }

    public static void ApplyMicro(TextView view)
    {
        Apply(view, MicroSp, TypefaceStyle.Bold, MgColors.InkMuted);
    }

    public static void ApplyAppTitle(TextView view)
    {
        Apply(view, AppTitleSp, TypefaceStyle.Bold, MgColors.Ink);
    }

    public static void ApplyAppSubtitle(TextView view)
    {
        Apply(view, AppSubtitleSp, TypefaceStyle.Bold, MgColors.InkMuted);
    }

    private static void Apply(TextView view, float textSizeSp, TypefaceStyle style, Color color)
    {
        ArgumentNullException.ThrowIfNull(view);

        view.SetTextSize(ComplexUnitType.Sp, textSizeSp);
        var family = style == TypefaceStyle.Normal ? "sans-serif" : "sans-serif-medium";
        view.SetTypeface(Typeface.Create(family, style), style);
        view.SetTextColor(color);
        view.SetIncludeFontPadding(false);
    }
}
