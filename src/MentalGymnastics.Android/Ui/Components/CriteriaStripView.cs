using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace MentalGymnastics.Android;

internal sealed class CriteriaStripView : LinearLayout
{
    public CriteriaStripView(
        Context context,
        IReadOnlyList<(string Value, string Label)> criteria)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        Orientation = Orientation.Horizontal;
        SetBackgroundColor(MgColors.Surface);
        SetPadding(0, MgSpacing.Dp(context, MgSpacing.Sm), 0, MgSpacing.Dp(context, MgSpacing.Sm));
        ContentDescription = string.Join(
            ", ",
            criteria.Select(item => $"{item.Label}: {item.Value}"));

        for (var index = 0; index < criteria.Count; index++)
        {
            if (index > 0)
            {
                var divider = new View(context);
                divider.SetBackgroundColor(MgColors.Hairline);
                AddView(divider, new LayoutParams(MgSpacing.Dp(context, 1), MgSpacing.Dp(context, 40)));
            }

            var item = criteria[index];
            var stack = new LinearLayout(context)
            {
                Orientation = Orientation.Vertical,
                ImportantForAccessibility = ImportantForAccessibility.No,
            };
            stack.SetGravity(GravityFlags.Center);

            var value = new TextView(context)
            {
                Text = item.Value,
                Gravity = GravityFlags.Center,
                ImportantForAccessibility = ImportantForAccessibility.No,
            };
            MgTypography.ApplyHeading(value);
            value.SetTextColor(MgColors.Ink);

            var label = new TextView(context)
            {
                Text = item.Label,
                Gravity = GravityFlags.Center,
                ImportantForAccessibility = ImportantForAccessibility.No,
            };
            MgTypography.ApplyLabel(label);
            label.SetTextColor(MgColors.InkMuted);

            stack.AddView(value, new LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent));
            stack.AddView(label, new LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent));
            AddView(stack, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        }
    }
}
