using Android.Content;
using Android.Views;
using Android.Widget;
using MentalGymnastics.Core;

namespace MentalGymnastics.Android;

internal sealed class BranchTileView : MgPanel
{
    public BranchTileView(Context context, BranchCode branch, IEnumerable<BranchLevelStatus> levels)
        : base(context)
    {
        var levelArray = levels
            .OrderBy(level => level.Level)
            .ToArray();
        var branchName = BranchName(branch);
        ContentDescription = $"{branchName} skill. {string.Join(", ", levelArray.Select(level => $"Level {LevelNumber(level.Level)} {StateMarkerView.LabelTextFor(level.State)}"))}";
        ImportantForAccessibility = ImportantForAccessibility.Yes;

        var header = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var branchLabel = new TextView(context)
        {
            Text = branchName,
        };
        MgTypography.ApplyHeading(branchLabel);
        header.AddView(branchLabel, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));

        var currentState = levelArray.FirstOrDefault(level => level.State != BranchLevelState.Unopened).State;
        if (levelArray.Length > 0)
        {
            header.AddView(new StateMarkerView(context, currentState));
        }

        var rail = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants,
        };
        rail.SetGravity(GravityFlags.CenterVertical);
        rail.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Md), 0, 0);

        foreach (var status in levelArray)
        {
            rail.AddView(new LevelCellView(context, status), new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        }

        AddView(header);
        AddView(rail);
    }

    private static int LevelNumber(GlobalLevelId level) => ((int)level) + 1;

    private static string BranchName(BranchCode branch)
    {
        return branch switch
        {
            BranchCode.FH => "Target Hold",
            BranchCode.FS => "Cue Switching",
            BranchCode.WM => "Memory",
            BranchCode.IR => "Response Control",
            BranchCode.DE => "Error Checking",
            BranchCode.CO => "Rule Finding",
            BranchCode.AI => "Pressure Control",
            BranchCode.TI => "Combined Task",
            _ => "Skill",
        };
    }
}
