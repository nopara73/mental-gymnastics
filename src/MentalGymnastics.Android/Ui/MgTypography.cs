using Android.Graphics;
using Android.Util;

namespace MentalGymnastics.Android;

internal static class MgTypography
{
    public const float TitleSp = 22;
    public const float HeadingSp = 17;
    public const float BodySp = 14;
    public const float LabelSp = 12;
    public const float MicroSp = 10;

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

    private static void Apply(TextView view, float textSizeSp, TypefaceStyle style, Color color)
    {
        ArgumentNullException.ThrowIfNull(view);

        view.SetTextSize(ComplexUnitType.Sp, textSizeSp);
        view.SetTypeface(Typeface.Default, style);
        view.SetTextColor(color);
        view.SetIncludeFontPadding(true);
    }
}
