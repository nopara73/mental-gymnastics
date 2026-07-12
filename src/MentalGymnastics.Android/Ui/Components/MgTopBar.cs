using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace MentalGymnastics.Android;

internal sealed class MgTopBar : LinearLayout
{
    private readonly FrameLayout leadingAction;
    private readonly ImageView brandIcon;
    private readonly MgGlyphView backIcon;
    private readonly TextView title;
    private readonly FrameLayout dataAction;
    private readonly MgGlyphView dataIcon;

    public MgTopBar(Context context)
        : base(context)
    {
        Orientation = Orientation.Horizontal;
        SetGravity(GravityFlags.CenterVertical);
        SetBackgroundColor(MgColors.InkDeep);
        SetPadding(MgSpacing.Dp(context, MgSpacing.Xs), 0, MgSpacing.Dp(context, MgSpacing.Xs), 0);
        Elevation = MgSpacing.Dp(context, 4);

        leadingAction = IconSlot("Back");
        brandIcon = new ImageView(context)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        brandIcon.SetImageResource(Resource.Mipmap.appicon);
        brandIcon.SetAdjustViewBounds(true);
        backIcon = BareGlyph(MgGlyphKind.Back, Color.White);
        leadingAction.AddView(brandIcon, Centered(28));
        leadingAction.AddView(backIcon, Centered(28));
        leadingAction.Click += (_, _) => BackRequested?.Invoke();
        AddView(leadingAction, Fixed(48));

        title = new TextView(context)
        {
            Text = "Train",
            Gravity = GravityFlags.CenterVertical,
            Ellipsize = global::Android.Text.TextUtils.TruncateAt.End,
        };
        title.SetMaxLines(1);
        MgTypography.ApplyHeading(title);
        title.SetTextColor(Color.White);
        AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1));

        dataAction = IconSlot("Local data");
        dataIcon = BareGlyph(MgGlyphKind.Data, Color.White);
        dataAction.AddView(dataIcon, Centered(28));
        dataAction.Click += (_, _) => DataRequested?.Invoke();
        AddView(dataAction, Fixed(48));
    }

    public event Action? BackRequested;

    public event Action? DataRequested;

    public void Update(
        string screenTitle,
        bool canGoBack,
        bool dataSelected,
        bool showDataAction = true)
    {
        title.Text = screenTitle;
        title.SetTextSize(
            global::Android.Util.ComplexUnitType.Sp,
            screenTitle.Length > 20 ? 18 : MgTypography.HeadingSp);
        backIcon.Visibility = canGoBack ? ViewStates.Visible : ViewStates.Gone;
        brandIcon.Visibility = canGoBack ? ViewStates.Gone : ViewStates.Visible;
        leadingAction.Clickable = canGoBack;
        leadingAction.Focusable = canGoBack;
        leadingAction.ContentDescription = canGoBack ? "Back" : null;
        dataAction.Visibility = showDataAction ? ViewStates.Visible : ViewStates.Invisible;
        dataAction.Clickable = showDataAction;
        dataAction.Focusable = showDataAction;
        dataIcon.SetGlyphColor(dataSelected ? MgColors.TrainingAction : Color.White);
        dataAction.ContentDescription = dataSelected ? "Local data, current screen" : "Local data";
    }

    private FrameLayout IconSlot(string description)
    {
        return new FrameLayout(Context!)
        {
            Clickable = true,
            Focusable = true,
            ContentDescription = description,
        };
    }

    private MgGlyphView BareGlyph(MgGlyphKind glyph, Color color)
    {
        return new MgGlyphView(
            Context!,
            glyph,
            color,
            filled: false,
            showContainer: false)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
    }

    private LinearLayout.LayoutParams Fixed(int sizeDp)
    {
        return new LinearLayout.LayoutParams(
            MgSpacing.Dp(Context!, sizeDp),
            ViewGroup.LayoutParams.MatchParent);
    }

    private FrameLayout.LayoutParams Centered(int sizeDp)
    {
        return new FrameLayout.LayoutParams(
            MgSpacing.Dp(Context!, sizeDp),
            MgSpacing.Dp(Context!, sizeDp),
            GravityFlags.Center);
    }
}
