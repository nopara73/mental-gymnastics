using Android.Graphics;

namespace MentalGymnastics.Android;

internal static class MgColors
{
    public static readonly Color Canvas = Color.Rgb(243, 247, 250);
    public static readonly Color Surface = Color.Rgb(253, 254, 252);
    public static readonly Color SurfaceMuted = Color.Rgb(238, 244, 242);
    public static readonly Color SurfaceStrong = Color.Rgb(231, 239, 243);
    public static readonly Color InkDeep = Color.Rgb(7, 38, 45);
    public static readonly Color Ink = Color.Rgb(20, 29, 33);
    public static readonly Color InkMuted = Color.Rgb(88, 101, 109);
    public static readonly Color InkSoft = Color.Rgb(209, 225, 226);
    public static readonly Color Hairline = Color.Rgb(202, 215, 218);
    public static readonly Color HairlineSoft = Color.Rgb(224, 232, 233);
    public static readonly Color Training = Color.Rgb(22, 132, 118);
    public static readonly Color TrainingDark = Color.Rgb(14, 98, 89);
    public static readonly Color TrainingAction = Color.Rgb(38, 190, 170);
    public static readonly Color TrainingTint = Color.Rgb(226, 245, 241);
    public static readonly Color TrainingPanel = Color.Rgb(210, 241, 235);
    public static readonly Color TestReady = Color.Rgb(70, 84, 166);
    public static readonly Color TestReadyTint = Color.Rgb(235, 237, 250);
    public static readonly Color PassedOnce = Color.Rgb(166, 114, 39);
    public static readonly Color Owned = Color.Rgb(30, 105, 73);
    public static readonly Color Maintenance = Color.Rgb(179, 123, 43);
    public static readonly Color MaintenanceTint = Color.Rgb(250, 242, 226);
    public static readonly Color Blocked = Color.Rgb(174, 61, 70);
    public static readonly Color BlockedTint = Color.Rgb(252, 237, 239);
    public static readonly Color Recovery = Color.Rgb(89, 111, 130);
    public static readonly Color RecoveryTint = Color.Rgb(235, 241, 246);
    public static readonly Color Transfer = Color.Rgb(116, 84, 157);

    public static Color ReadableTextOn(Color background)
    {
        return ContrastRatio(background, Color.White) >= ContrastRatio(background, Ink)
            ? Color.White
            : Ink;
    }

    private static double ContrastRatio(Color first, Color second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05d) /
            (Math.Min(firstLuminance, secondLuminance) + 0.05d);
    }

    private static double RelativeLuminance(Color color)
    {
        var argb = unchecked((uint)color.ToArgb());
        var red = Linearize((argb >> 16) & 0xff);
        var green = Linearize((argb >> 8) & 0xff);
        var blue = Linearize(argb & 0xff);
        return 0.2126d * red + 0.7152d * green + 0.0722d * blue;
    }

    private static double Linearize(uint component)
    {
        var value = component / 255d;
        return value <= 0.04045d
            ? value / 12.92d
            : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
    }
}
