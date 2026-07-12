using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace MentalGymnastics.Android;

internal sealed class TargetMaterialView : LinearLayout
{
    private readonly TargetShapeView shape;
    private readonly TextView label;

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
        var display = Normalize(target);
        label.Text = display;
        shape.Update(display);
        ContentDescription = $"Target: {display}";
    }

    public void SetDense(bool dense)
    {
        var padding = MgSpacing.Dp(Context!, dense ? MgSpacing.Xs : MgSpacing.Sm);
        SetPadding(padding, padding, padding, padding);
        var height = MgSpacing.Dp(Context!, dense ? 48 : 84);
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
        string[] prefixes = ["Hold target phrase:", "Hold target word:", "Target:"];
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].Trim();
                break;
            }
        }

        return value.TrimEnd('.');
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

            var centerX = Width / 2f + (target.Contains("left", StringComparison.Ordinal)
                ? -Width * 0.18f
                : target.Contains("right", StringComparison.Ordinal)
                    ? Width * 0.18f
                    : 0f);
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

            if (target.Contains("line", StringComparison.Ordinal))
            {
                paint.SetStyle(Paint.Style.Stroke);
                canvas.DrawLine(
                    centerX - MgSpacing.Dp(Context!, 46) * scale,
                    centerY,
                    centerX + MgSpacing.Dp(Context!, 46) * scale,
                    centerY,
                    paint);
                return;
            }

            if (target.Contains("square", StringComparison.Ordinal))
            {
                paint.SetStyle(Paint.Style.Fill);
                var half = MgSpacing.Dp(Context!, 34) * scale;
                canvas.DrawRect(centerX - half, centerY - half, centerX + half, centerY + half, paint);
                return;
            }

            var radius = MgSpacing.Dp(
                Context!,
                target.Contains("dot", StringComparison.Ordinal) ? 24 : 38) * scale;
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

            return Color.Rgb(24, 29, 33);
        }
    }
}
