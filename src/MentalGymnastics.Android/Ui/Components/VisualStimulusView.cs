using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using MentalGymnastics.Content;

namespace MentalGymnastics.Android;

/// <summary>
/// Renders Content-owned visual stimulus data as an actual object. Descriptor
/// strings stay out of the sighted task surface and are used only for
/// accessibility through the structured specification.
/// </summary>
internal sealed class VisualStimulusView : View
{
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private VisualStimulusSpec? stimulus;

    public VisualStimulusView(Context context)
        : base(context)
    {
        ImportantForAccessibility = ImportantForAccessibility.Yes;
        SetMinimumHeight(MgSpacing.Dp(context, 112));
    }

    public void Update(VisualStimulusSpec? value)
    {
        stimulus = value;
        ContentDescription = value is null
            ? "Visual stimulus unavailable"
            : Describe(value);
        Invalidate();
    }

    internal static string Describe(VisualStimulusSpec value)
    {
        var parts = new List<string>
        {
            value.Size.ToString().ToLowerInvariant(),
            value.Color.ToString().ToLowerInvariant(),
            value.Fill.ToString().ToLowerInvariant(),
            value.Shape.ToString().ToLowerInvariant(),
        };
        if (value.Direction != VisualStimulusDirection.None)
        {
            parts.Add($"pointing {value.Direction.ToString().ToLowerInvariant()}");
        }

        if (value.Mark != VisualStimulusMark.None)
        {
            parts.Add($"with {Math.Max(1, value.MarkCount)} " +
                      $"{value.Mark.ToString().ToLowerInvariant()} mark at " +
                      value.MarkPosition.ToString().ToLowerInvariant());
        }

        if (value.Border != VisualStimulusBorder.None)
        {
            parts.Add($"with {value.Border.ToString().ToLowerInvariant()} border");
        }

        return string.Join(" ", parts);
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        if (stimulus is not { } value || Width <= 0 || Height <= 0)
        {
            return;
        }

        var centerX = Width / 2f;
        var centerY = Height / 2f;
        var sizeScale = value.Size switch
        {
            VisualStimulusSize.Small => 0.72f,
            VisualStimulusSize.Large => 1.12f,
            _ => 0.92f,
        };
        var radius = Math.Max(1f, Math.Min(Width, Height) * 0.29f * sizeScale);
        var color = StimulusColor(value.Color);

        DrawContrastField(canvas, value);
        DrawOuterBorder(canvas, value, centerX, centerY, radius, color);
        DrawShape(canvas, value, centerX, centerY, radius, color);
        DrawMarks(canvas, value, centerX, centerY, radius, ContrastColor(value.Color));
    }

    private void DrawContrastField(Canvas canvas, VisualStimulusSpec value)
    {
        if (value.Color != VisualStimulusColor.White)
        {
            return;
        }

        // White is a meaningful programmed color, but the app canvas is also
        // light. Give the entire stimulus viewport a neutral field so the
        // object stays visibly white without changing its encoded identity.
        var inset = MgSpacing.Dp(Context!, 6);
        paint.Color = Color.Rgb(112, 124, 132);
        paint.SetStyle(Paint.Style.Fill);
        canvas.DrawRoundRect(
            inset,
            inset,
            Math.Max(inset, Width - inset),
            Math.Max(inset, Height - inset),
            MgSpacing.Dp(Context!, 10),
            MgSpacing.Dp(Context!, 10),
            paint);
    }

    private void DrawOuterBorder(
        Canvas canvas,
        VisualStimulusSpec value,
        float centerX,
        float centerY,
        float radius,
        Color color)
    {
        if (value.Border == VisualStimulusBorder.None)
        {
            return;
        }

        paint.SetStyle(Paint.Style.Stroke);
        paint.Color = value.Border == VisualStimulusBorder.Light
            ? Color.Argb(90, color.R, color.G, color.B)
            : color;
        paint.StrokeWidth = MgSpacing.Dp(Context!, value.Border == VisualStimulusBorder.Heavy ? 6 : 3);
        var outer = radius * 1.23f;
        canvas.DrawRoundRect(
            centerX - outer,
            centerY - outer,
            centerX + outer,
            centerY + outer,
            outer * 0.16f,
            outer * 0.16f,
            paint);
    }

    private void DrawShape(
        Canvas canvas,
        VisualStimulusSpec value,
        float centerX,
        float centerY,
        float radius,
        Color color)
    {
        paint.Color = color;
        paint.StrokeWidth = MgSpacing.Dp(Context!, 5);
        paint.StrokeCap = Paint.Cap.Round;
        paint.StrokeJoin = Paint.Join.Round;
        paint.SetStyle(value.Fill == VisualStimulusFill.Solid || value.Shape == VisualStimulusShape.Dot
            ? Paint.Style.Fill
            : Paint.Style.Stroke);

        switch (value.Shape)
        {
            case VisualStimulusShape.Dot:
                canvas.DrawCircle(centerX, centerY, radius * 0.55f, paint);
                break;
            case VisualStimulusShape.Circle:
            case VisualStimulusShape.Ring:
                if (value.Shape == VisualStimulusShape.Ring)
                {
                    paint.SetStyle(Paint.Style.Stroke);
                }
                canvas.DrawCircle(centerX, centerY, radius, paint);
                break;
            case VisualStimulusShape.Square:
                canvas.DrawRect(centerX - radius, centerY - radius, centerX + radius, centerY + radius, paint);
                break;
            case VisualStimulusShape.Bar:
                canvas.DrawRoundRect(
                    centerX - radius,
                    centerY - (radius * 0.28f),
                    centerX + radius,
                    centerY + (radius * 0.28f),
                    radius * 0.12f,
                    radius * 0.12f,
                    paint);
                break;
            case VisualStimulusShape.Arrow:
                DrawArrow(canvas, value.Direction, centerX, centerY, radius);
                break;
            case VisualStimulusShape.Triangle:
                using (var triangle = Polygon(
                           (centerX, centerY - radius),
                           (centerX - (radius * 0.88f), centerY + (radius * 0.62f)),
                           (centerX + (radius * 0.88f), centerY + (radius * 0.62f))))
                {
                    canvas.DrawPath(triangle, paint);
                }
                break;
            case VisualStimulusShape.Diamond:
                using (var diamond = Polygon(
                           (centerX, centerY - radius),
                           (centerX - radius, centerY),
                           (centerX, centerY + radius),
                           (centerX + radius, centerY)))
                {
                    canvas.DrawPath(diamond, paint);
                }
                break;
        }

        if (value.Fill == VisualStimulusFill.Striped)
        {
            paint.SetStyle(Paint.Style.Stroke);
            paint.StrokeWidth = MgSpacing.Dp(Context!, 3);
            for (var offset = -0.55f; offset <= 0.55f; offset += 0.55f)
            {
                canvas.DrawLine(
                    centerX - (radius * 0.55f),
                    centerY + (radius * offset),
                    centerX + (radius * 0.55f),
                    centerY + (radius * (offset - 0.45f)),
                    paint);
            }
        }
        else if (value.Fill == VisualStimulusFill.Crossed)
        {
            paint.SetStyle(Paint.Style.Stroke);
            paint.StrokeWidth = MgSpacing.Dp(Context!, 4);
            canvas.DrawLine(centerX - radius * 0.6f, centerY - radius * 0.6f, centerX + radius * 0.6f, centerY + radius * 0.6f, paint);
            canvas.DrawLine(centerX + radius * 0.6f, centerY - radius * 0.6f, centerX - radius * 0.6f, centerY + radius * 0.6f, paint);
        }
    }

    private void DrawArrow(
        Canvas canvas,
        VisualStimulusDirection direction,
        float centerX,
        float centerY,
        float radius)
    {
        var degrees = direction switch
        {
            VisualStimulusDirection.East => 90f,
            VisualStimulusDirection.South => 180f,
            VisualStimulusDirection.West => 270f,
            _ => 0f,
        };
        canvas.Save();
        canvas.Rotate(degrees, centerX, centerY);
        using var arrow = Polygon(
            (centerX, centerY - radius),
            (centerX - radius * 0.75f, centerY),
            (centerX - radius * 0.28f, centerY),
            (centerX - radius * 0.28f, centerY + radius),
            (centerX + radius * 0.28f, centerY + radius),
            (centerX + radius * 0.28f, centerY),
            (centerX + radius * 0.75f, centerY));
        canvas.DrawPath(arrow, paint);
        canvas.Restore();
    }

    private void DrawMarks(
        Canvas canvas,
        VisualStimulusSpec value,
        float centerX,
        float centerY,
        float radius,
        Color color)
    {
        if (value.Mark == VisualStimulusMark.None)
        {
            return;
        }

        var (markX, markY) = value.MarkPosition switch
        {
            VisualStimulusPosition.Left => (centerX - radius * 0.55f, centerY),
            VisualStimulusPosition.Right => (centerX + radius * 0.55f, centerY),
            VisualStimulusPosition.Top => (centerX, centerY - radius * 0.55f),
            VisualStimulusPosition.Bottom => (centerX, centerY + radius * 0.55f),
            _ => (centerX, centerY),
        };
        var count = Math.Clamp(value.MarkCount <= 0 ? 1 : value.MarkCount, 1, 4);
        paint.Color = color;
        paint.StrokeWidth = MgSpacing.Dp(Context!, value.Mark == VisualStimulusMark.Notch ? 8 : 5);
        paint.StrokeCap = Paint.Cap.Round;
        paint.SetStyle(value.Mark == VisualStimulusMark.Dot ? Paint.Style.Fill : Paint.Style.Stroke);

        for (var index = 0; index < count; index++)
        {
            var offset = (index - ((count - 1) / 2f)) * radius * 0.24f;
            var x = markX + (value.MarkOrientation == VisualStimulusOrientation.Vertical ? 0 : offset);
            var y = markY + (value.MarkOrientation == VisualStimulusOrientation.Vertical ? offset : 0);
            switch (value.Mark)
            {
                case VisualStimulusMark.Dot:
                    canvas.DrawCircle(x, y, radius * 0.09f, paint);
                    break;
                case VisualStimulusMark.Notch:
                case VisualStimulusMark.Bar:
                case VisualStimulusMark.Crossbar:
                    canvas.DrawLine(x - radius * 0.2f, y, x + radius * 0.2f, y, paint);
                    break;
                case VisualStimulusMark.Slash:
                case VisualStimulusMark.Slot:
                    var diagonalRight = value.MarkOrientation != VisualStimulusOrientation.DiagonalLeft;
                    canvas.DrawLine(
                        x - radius * 0.14f,
                        y + (diagonalRight ? radius * 0.2f : -radius * 0.2f),
                        x + radius * 0.14f,
                        y + (diagonalRight ? -radius * 0.2f : radius * 0.2f),
                        paint);
                    break;
                case VisualStimulusMark.Tick:
                    canvas.DrawLine(x, y - radius * 0.16f, x, y + radius * 0.16f, paint);
                    break;
                case VisualStimulusMark.Corner:
                    canvas.DrawLine(x, y, x + radius * 0.18f, y, paint);
                    canvas.DrawLine(x, y, x, y + radius * 0.18f, paint);
                    break;
            }
        }
    }

    private static global::Android.Graphics.Path Polygon(params (float X, float Y)[] points)
    {
        var path = new global::Android.Graphics.Path();
        path.MoveTo(points[0].X, points[0].Y);
        foreach (var point in points.Skip(1))
        {
            path.LineTo(point.X, point.Y);
        }
        path.Close();
        return path;
    }

    private static Color StimulusColor(VisualStimulusColor color)
    {
        return color switch
        {
            VisualStimulusColor.Red => Color.Rgb(201, 50, 62),
            VisualStimulusColor.Blue => Color.Rgb(41, 103, 186),
            VisualStimulusColor.Green => Color.Rgb(30, 132, 87),
            VisualStimulusColor.White => Color.Rgb(244, 247, 248),
            VisualStimulusColor.Amber => Color.Rgb(190, 112, 12),
            VisualStimulusColor.Violet => Color.Rgb(112, 72, 170),
            VisualStimulusColor.Gray => Color.Rgb(105, 116, 123),
            _ => Color.Rgb(24, 29, 33),
        };
    }

    private static Color ContrastColor(VisualStimulusColor color)
    {
        return color is VisualStimulusColor.White or VisualStimulusColor.Amber
            ? Color.Rgb(24, 29, 33)
            : Color.White;
    }
}

internal sealed class VisualStimulusPairView : LinearLayout
{
    private readonly VisualStimulusView first;
    private readonly VisualStimulusView second;
    private readonly TextView feature;

    public VisualStimulusPairView(Context context)
        : base(context)
    {
        Orientation = Orientation.Vertical;
        ImportantForAccessibility = ImportantForAccessibility.Yes;

        var row = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants,
        };
        first = StimulusPanel(context, row, first: true);
        second = StimulusPanel(context, row, first: false);
        AddView(row, new LayoutParams(LayoutParams.MatchParent, MgSpacing.Dp(context, 176)));

        feature = new TextView(context)
        {
            Gravity = GravityFlags.Center,
        };
        MgTypography.ApplyBody(feature);
        feature.SetTextColor(MgColors.TrainingDark);
        AddView(feature, new LayoutParams(LayoutParams.MatchParent, LayoutParams.WrapContent)
        {
            TopMargin = MgSpacing.Dp(context, MgSpacing.Sm),
        });
    }

    public void Update(VisualStimulusPairSpec pair)
    {
        first.Update(pair.First);
        second.Update(pair.Second);
        var label = pair.RelevantFeatureName;
        feature.Text = $"Compare only: {label}";
        ContentDescription =
            $"Visual comparison. First: {VisualStimulusView.Describe(pair.First)}. " +
            $"Second: {VisualStimulusView.Describe(pair.Second)}. Compare only {label}.";
    }

    private static VisualStimulusView StimulusPanel(Context context, LinearLayout row, bool first)
    {
        var panel = new FrameLayout(context)
        {
            Background = MgTheme.MutedSurface(context, cornerRadius: 8),
            ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants,
        };
        var stimulus = new VisualStimulusView(context)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        panel.AddView(stimulus, new FrameLayout.LayoutParams(
            LayoutParams.MatchParent,
            LayoutParams.MatchParent));
        var layout = new LayoutParams(0, LayoutParams.MatchParent, 1);
        if (!first)
        {
            layout.SetMargins(MgSpacing.Dp(context, MgSpacing.Sm), 0, 0, 0);
        }
        row.AddView(panel, layout);
        return stimulus;
    }
}
