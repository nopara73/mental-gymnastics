using Android.Content;
using Android.Views;
using Android.Widget;

namespace MentalGymnastics.Android;

internal sealed class StandardPanelView : MgPanel
{
    public StandardPanelView(
        Context context,
        string title,
        string standard,
        string honestyConstraint)
        : base(context)
    {
        ContentDescription = $"{title}. What counts: {standard}. Required rule: {honestyConstraint}.";

        var titleView = new TextView(context)
        {
            Text = title,
        };
        MgTypography.ApplyLabel(titleView);
        AddView(titleView);

        AddRow(context, "Standard", standard);
        AddRow(context, "Required rule", honestyConstraint);
    }

    private void AddRow(Context context, string label, string value)
    {
        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        row.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Sm), 0, 0);

        var labelView = new TextView(context)
        {
            Text = label,
        };
        MgTypography.ApplyMicro(labelView);

        var valueView = new TextView(context)
        {
            Text = value,
        };
        MgTypography.ApplyBody(valueView);

        row.AddView(labelView);
        row.AddView(valueView);
        AddView(row);
    }
}
