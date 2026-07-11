using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace MentalGymnastics.Android;

internal sealed class MgBottomNavigationBar : LinearLayout
{
    private readonly Dictionary<MgNavigationDestination, NavigationItem> items = [];

    public MgBottomNavigationBar(Context context)
        : base(context)
    {
        Orientation = Orientation.Horizontal;
        SetBackgroundColor(MgColors.Surface);
        SetPadding(0, MgSpacing.Dp(context, 2), 0, 0);
        Elevation = MgSpacing.Dp(context, 8);

        AddItem(MgNavigationDestination.Train, "Train", MgGlyphKind.Target);
        AddItem(MgNavigationDestination.Map, "Map", MgGlyphKind.Map);
        AddItem(MgNavigationDestination.Record, "Record", MgGlyphKind.Record);
        AddItem(MgNavigationDestination.Review, "Review", MgGlyphKind.Review);
        Update(MgNavigationDestination.Train, trainSessionActive: false);
    }

    public event Action<MgNavigationDestination>? DestinationSelected;

    public void Update(
        MgNavigationDestination selected,
        bool trainSessionActive)
    {
        foreach (var (destination, item) in items)
        {
            var isSelected = destination == selected;
            var color = isSelected ? MgColors.TrainingDark : MgColors.InkMuted;
            item.Indicator.SetBackgroundColor(isSelected ? MgColors.Training : Color.Transparent);
            item.Icon.SetGlyphColor(color);
            item.Label.SetTextColor(color);
            item.Root.Selected = isSelected;
            item.Root.ContentDescription = destination == MgNavigationDestination.Train && trainSessionActive
                ? $"{item.Label.Text}, active session"
                : item.Label.Text;
        }
    }

    private void AddItem(
        MgNavigationDestination destination,
        string label,
        MgGlyphKind glyph)
    {
        var root = new LinearLayout(Context)
        {
            Orientation = Orientation.Vertical,
            Clickable = true,
            Focusable = true,
        };
        root.SetGravity(GravityFlags.CenterHorizontal);
        root.SetBackgroundColor(Color.Transparent);
        root.SetPadding(0, 0, 0, MgSpacing.Dp(Context!, MgSpacing.Xs));

        var indicator = new View(Context);
        root.AddView(indicator, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            MgSpacing.Dp(Context!, 3)));

        var icon = new MgGlyphView(
            Context!,
            glyph,
            MgColors.InkMuted,
            filled: false,
            showContainer: false)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        root.AddView(icon, new LinearLayout.LayoutParams(
            MgSpacing.Dp(Context!, 28),
            MgSpacing.Dp(Context!, 28)));

        var text = new TextView(Context)
        {
            Text = label,
            Gravity = GravityFlags.Center,
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        MgTypography.ApplyLabel(text);
        root.AddView(text, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            0,
            1));

        root.Click += (_, _) => DestinationSelected?.Invoke(destination);
        AddView(root, new LinearLayout.LayoutParams(0, MgSpacing.Dp(Context!, 64), 1));
        items.Add(destination, new NavigationItem(root, indicator, icon, text));
    }

    private sealed record NavigationItem(
        LinearLayout Root,
        View Indicator,
        MgGlyphView Icon,
        TextView Label);
}
