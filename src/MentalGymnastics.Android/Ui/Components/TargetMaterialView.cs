using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace MentalGymnastics.Android;

internal sealed class TargetMaterialView : LinearLayout
{
    private static readonly HashSet<string> VisualColors = new(
        ["red", "blue", "green", "black", "amber", "violet"],
        StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> VisualShapes = new(
        ["dot", "line", "square", "circle", "triangle"],
        StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LegacyUntestedPositions = new(
        ["left", "center", "right"],
        StringComparer.OrdinalIgnoreCase);

    private readonly TargetShapeView shape;
    private readonly TextView label;
    private string display = "Target";
    private bool dense;
    private bool liveStimulusOnly;

    public TargetMaterialView(Context context, bool compact = false)
        : base(context)
    {
        Orientation = Orientation.Vertical;
        SetGravity(GravityFlags.CenterHorizontal);
        SetPadding(
            MgSpacing.Dp(context, compact ? MgSpacing.Sm : MgSpacing.Md),
            MgSpacing.Dp(context, compact ? MgSpacing.Sm : MgSpacing.Md),
            MgSpacing.Dp(context, compact ? MgSpacing.Sm : MgSpacing.Md),
            MgSpacing.Dp(context, compact ? MgSpacing.Sm : MgSpacing.Md));
        Background = MgTheme.MutedSurface(context, cornerRadius: 8);

        shape = new TargetShapeView(context, compact)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        AddView(shape, new LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            MgSpacing.Dp(context, compact ? 84 : 136)));

        label = new TextView(context)
        {
            Gravity = GravityFlags.Center,
        };
        label.SetMaxWidth(MgSpacing.Dp(context, 320));
        if (compact)
        {
            MgTypography.ApplyBody(label);
        }
        else
        {
            MgTypography.ApplyHeading(label);
        }
        AddView(label, new LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));
    }

    public void Update(string? target)
    {
        display = Normalize(target);
        label.Text = display;
        shape.Update(display);
        UpdateLabelVisibility();
        ContentDescription = $"Target: {display}";
    }

    public void SetDense(bool dense)
    {
        this.dense = dense;
        if (liveStimulusOnly)
        {
            return;
        }

        ApplyStandardLayout();
    }

    public void SetLiveStimulusOnly(bool enabled)
    {
        liveStimulusOnly = enabled;
        UpdateLabelVisibility();
        ImportantForAccessibility = enabled
            ? ImportantForAccessibility.No
            : ImportantForAccessibility.Auto;

        if (!enabled)
        {
            ApplyStandardLayout();
            return;
        }

        SetGravity(GravityFlags.Center);
        SetPadding(0, 0, 0, 0);
        Background = null;
        SetShapeHeight(MgSpacing.Dp(Context!, 220));
    }

    private void ApplyStandardLayout()
    {
        var padding = MgSpacing.Dp(Context!, dense ? MgSpacing.Xs : MgSpacing.Sm);
        SetGravity(GravityFlags.CenterHorizontal);
        SetPadding(padding, padding, padding, padding);
        Background = MgTheme.MutedSurface(Context!, cornerRadius: 8);
        SetShapeHeight(MgSpacing.Dp(Context!, dense ? 72 : 136));
    }

    private void UpdateLabelVisibility()
    {
        label.Visibility = liveStimulusOnly || IsVisualShapeDescriptor(display)
            ? ViewStates.Gone
            : ViewStates.Visible;
    }

    private static bool IsVisualShapeDescriptor(string value)
    {
        var tokens = value.Split(
            [' ', ',', '.', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hasColor = tokens.Any(VisualColors.Contains);
        var hasShape = tokens.Any(VisualShapes.Contains);
        return hasColor && hasShape;
    }

    private void SetShapeHeight(int height)
    {
        shape.SetMinimumHeight(height);
        var layout = shape.LayoutParameters;
        if (layout is not null && layout.Height != height)
        {
            layout.Height = height;
            shape.LayoutParameters = layout;
        }
    }

    private static string Normalize(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return "Target";
        }

        var value = target.Trim();
        string[] prefixes = ["Visual target:", "Hold target phrase:", "Hold target word:", "Target:"];
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].Trim();
                break;
            }
        }

        value = value.TrimEnd('.');
        if (!IsVisualShapeDescriptor(value))
        {
            return value;
        }

        return string.Join(
            " ",
            value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => !LegacyUntestedPositions.Contains(token)));
    }

    private sealed class TargetShapeView : View
    {
        private readonly Paint paint;
        private string target = "target";

        public TargetShapeView(Context context, bool compact)
            : base(context)
        {
            paint = new Paint(PaintFlags.AntiAlias);
            SetMinimumHeight(MgSpacing.Dp(context, compact ? 84 : 136));
        }

        public void Update(string value)
        {
            target = value.ToLowerInvariant();
            Invalidate();
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            var centerX = Width / 2f;
            var centerY = Height / 2f;
            var scale = target.Contains("small", StringComparison.Ordinal)
                ? 0.72f
                : target.Contains("large", StringComparison.Ordinal)
                    ? 1.18f
                    : 1f;
            var color = TargetColor(target);
            paint.Color = color;
            paint.StrokeWidth = MgSpacing.Dp(Context!, 6);
            paint.StrokeCap = Paint.Cap.Round;

            var halfStroke = paint.StrokeWidth / 2f;
            var horizontalRoom = Math.Min(centerX, Width - centerX) - halfStroke;
            var verticalRoom = Height / 2f - halfStroke;
            var availableRadius = Math.Max(0, Math.Min(horizontalRoom, verticalRoom));

            if (target.Contains("line", StringComparison.Ordinal))
            {
                paint.SetStyle(Paint.Style.Stroke);
                var halfLength = Math.Max(
                    0,
                    Math.Min(MgSpacing.Dp(Context!, 46) * scale, horizontalRoom));
                canvas.DrawLine(
                    centerX - halfLength,
                    centerY,
                    centerX + halfLength,
                    centerY,
                    paint);
                return;
            }

            if (target.Contains("square", StringComparison.Ordinal))
            {
                paint.SetStyle(Paint.Style.Fill);
                var half = Math.Min(MgSpacing.Dp(Context!, 34) * scale, availableRadius);
                canvas.DrawRect(centerX - half, centerY - half, centerX + half, centerY + half, paint);
                return;
            }

            if (target.Contains("triangle", StringComparison.Ordinal))
            {
                paint.SetStyle(Paint.Style.Fill);
                var triangleRadius = Math.Min(MgSpacing.Dp(Context!, 40) * scale, availableRadius);
                using var path = new global::Android.Graphics.Path();
                path.MoveTo(centerX, centerY - triangleRadius);
                path.LineTo(centerX - (triangleRadius * 0.87f), centerY + (triangleRadius * 0.5f));
                path.LineTo(centerX + (triangleRadius * 0.87f), centerY + (triangleRadius * 0.5f));
                path.Close();
                canvas.DrawPath(path, paint);
                return;
            }

            var radius = Math.Min(
                MgSpacing.Dp(
                    Context!,
                    target.Contains("dot", StringComparison.Ordinal) ? 24 : 38) * scale,
                availableRadius);
            paint.SetStyle(target.Contains("circle", StringComparison.Ordinal)
                ? Paint.Style.Stroke
                : Paint.Style.Fill);
            canvas.DrawCircle(centerX, centerY, radius, paint);
        }

        private static Color TargetColor(string value)
        {
            if (value.Contains("red", StringComparison.Ordinal))
            {
                return Color.Rgb(201, 50, 62);
            }

            if (value.Contains("blue", StringComparison.Ordinal))
            {
                return Color.Rgb(41, 103, 186);
            }

            if (value.Contains("green", StringComparison.Ordinal))
            {
                return Color.Rgb(30, 132, 87);
            }

            if (value.Contains("amber", StringComparison.Ordinal))
            {
                return Color.Rgb(190, 112, 12);
            }

            if (value.Contains("violet", StringComparison.Ordinal))
            {
                return Color.Rgb(112, 72, 170);
            }

            return Color.Rgb(24, 29, 33);
        }
    }
}
