using Android.Content;
using Android.Graphics;
using Android.Views;
using GraphicsPath = global::Android.Graphics.Path;

namespace MentalGymnastics.Android;

internal enum MgGlyphKind
{
    Start,
    Read,
    Hold,
    Target,
    Timer,
    Drift,
    Stop,
    Record,
    Today,
    Next,
    Check,
    Alert,
    Map,
    Review,
    Data,
    Back,
}

internal sealed class MgGlyphView : View
{
    private readonly Paint fillPaint;
    private readonly Paint strokePaint;
    private readonly Paint iconPaint;
    private readonly MgGlyphKind kind;
    private Color color;
    private readonly bool filled;
    private readonly bool showContainer;

    public MgGlyphView(
        Context context,
        MgGlyphKind kind,
        Color color,
        bool filled = true,
        bool showContainer = true)
        : base(context)
    {
        this.kind = kind;
        this.color = color;
        this.filled = filled;
        this.showContainer = showContainer;

        fillPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = color,
        };
        fillPaint.SetStyle(Paint.Style.Fill);

        strokePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = color,
            StrokeWidth = MgSpacing.Dp(context, filled ? 1 : 2),
        };
        strokePaint.SetStyle(Paint.Style.Stroke);

        iconPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = filled ? Color.White : color,
            StrokeWidth = MgSpacing.Dp(context, 2),
            StrokeCap = Paint.Cap.Round,
            StrokeJoin = Paint.Join.Round,
        };
        iconPaint.SetStyle(Paint.Style.Stroke);

        SetMinimumWidth(MgSpacing.Dp(context, 44));
        SetMinimumHeight(MgSpacing.Dp(context, 44));
        ImportantForAccessibility = ImportantForAccessibility.No;
        ContentDescription = null;
    }

    public void SetGlyphColor(Color nextColor)
    {
        color = nextColor;
        fillPaint.Color = nextColor;
        strokePaint.Color = nextColor;
        iconPaint.Color = filled ? Color.White : nextColor;
        Invalidate();
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        var inset = MgSpacing.Dp(Context!, 1);
        var radius = MgSpacing.Dp(Context!, 8);
        using var bounds = new RectF(inset, inset, Width - inset, Height - inset);

        if (showContainer && filled)
        {
            canvas.DrawRoundRect(bounds, radius, radius, fillPaint);
        }

        if (showContainer)
        {
            canvas.DrawRoundRect(bounds, radius, radius, strokePaint);
        }

        var size = Math.Min(Width, Height);
        var cx = Width / 2f;
        var cy = Height / 2f;
        var unit = size / 44f;
        iconPaint.StrokeWidth = Math.Max(1f, 2.2f * unit);
        iconPaint.Color = filled ? Color.White : color;

        switch (kind)
        {
            case MgGlyphKind.Start:
                DrawStart(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Read:
                DrawRead(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Hold:
            case MgGlyphKind.Target:
                DrawTarget(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Timer:
                DrawTimer(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Drift:
                DrawDrift(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Stop:
                DrawStop(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Record:
                DrawRecord(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Today:
                DrawToday(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Next:
                DrawNext(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Check:
                DrawCheck(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Alert:
                DrawAlert(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Map:
                DrawMap(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Review:
                DrawReview(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Data:
                DrawData(canvas, cx, cy, unit);
                break;
            case MgGlyphKind.Back:
                DrawBack(canvas, cx, cy, unit);
                break;
        }
    }

    private void DrawStart(Canvas canvas, float cx, float cy, float unit)
    {
        iconPaint.SetStyle(Paint.Style.Fill);
        using var path = new GraphicsPath();
        path.MoveTo(cx - 5f * unit, cy - 9f * unit);
        path.LineTo(cx - 5f * unit, cy + 9f * unit);
        path.LineTo(cx + 10f * unit, cy);
        path.Close();
        canvas.DrawPath(path, iconPaint);
        iconPaint.SetStyle(Paint.Style.Stroke);
    }

    private void DrawRead(Canvas canvas, float cx, float cy, float unit)
    {
        using var page = new RectF(cx - 10f * unit, cy - 12f * unit, cx + 10f * unit, cy + 12f * unit);
        canvas.DrawRoundRect(page, 3f * unit, 3f * unit, iconPaint);
        canvas.DrawLine(cx - 5f * unit, cy - 5f * unit, cx + 5f * unit, cy - 5f * unit, iconPaint);
        canvas.DrawLine(cx - 5f * unit, cy, cx + 6f * unit, cy, iconPaint);
        canvas.DrawLine(cx - 5f * unit, cy + 5f * unit, cx + 2f * unit, cy + 5f * unit, iconPaint);
    }

    private void DrawTarget(Canvas canvas, float cx, float cy, float unit)
    {
        canvas.DrawCircle(cx, cy, 11f * unit, iconPaint);
        canvas.DrawCircle(cx, cy, 5.5f * unit, iconPaint);
        canvas.DrawLine(cx - 14f * unit, cy, cx - 9f * unit, cy, iconPaint);
        canvas.DrawLine(cx + 9f * unit, cy, cx + 14f * unit, cy, iconPaint);
        canvas.DrawLine(cx, cy - 14f * unit, cx, cy - 9f * unit, iconPaint);
        canvas.DrawLine(cx, cy + 9f * unit, cx, cy + 14f * unit, iconPaint);
    }

    private void DrawTimer(Canvas canvas, float cx, float cy, float unit)
    {
        canvas.DrawCircle(cx, cy, 11f * unit, iconPaint);
        canvas.DrawLine(cx, cy, cx, cy - 7f * unit, iconPaint);
        canvas.DrawLine(cx, cy, cx + 6f * unit, cy + 3f * unit, iconPaint);
    }

    private void DrawDrift(Canvas canvas, float cx, float cy, float unit)
    {
        using var path = new GraphicsPath();
        path.MoveTo(cx - 12f * unit, cy + 6f * unit);
        path.CubicTo(cx - 5f * unit, cy - 10f * unit, cx + 1f * unit, cy + 12f * unit, cx + 12f * unit, cy - 5f * unit);
        canvas.DrawPath(path, iconPaint);
        iconPaint.SetStyle(Paint.Style.Fill);
        canvas.DrawCircle(cx + 13f * unit, cy - 6f * unit, 2f * unit, iconPaint);
        iconPaint.SetStyle(Paint.Style.Stroke);
    }

    private void DrawStop(Canvas canvas, float cx, float cy, float unit)
    {
        using var square = new RectF(cx - 8f * unit, cy - 8f * unit, cx + 8f * unit, cy + 8f * unit);
        canvas.DrawRoundRect(square, 2f * unit, 2f * unit, iconPaint);
    }

    private void DrawRecord(Canvas canvas, float cx, float cy, float unit)
    {
        DrawSmallCheck(canvas, cx - 9f * unit, cy - 7f * unit, unit);
        DrawSmallCheck(canvas, cx - 9f * unit, cy, unit);
        DrawSmallCheck(canvas, cx - 9f * unit, cy + 7f * unit, unit);
        canvas.DrawLine(cx - 1f * unit, cy - 7f * unit, cx + 11f * unit, cy - 7f * unit, iconPaint);
        canvas.DrawLine(cx - 1f * unit, cy, cx + 11f * unit, cy, iconPaint);
        canvas.DrawLine(cx - 1f * unit, cy + 7f * unit, cx + 7f * unit, cy + 7f * unit, iconPaint);
    }

    private void DrawToday(Canvas canvas, float cx, float cy, float unit)
    {
        using var calendar = new RectF(cx - 11f * unit, cy - 10f * unit, cx + 11f * unit, cy + 11f * unit);
        canvas.DrawRoundRect(calendar, 3f * unit, 3f * unit, iconPaint);
        canvas.DrawLine(cx - 11f * unit, cy - 4f * unit, cx + 11f * unit, cy - 4f * unit, iconPaint);
        canvas.DrawLine(cx - 5f * unit, cy - 13f * unit, cx - 5f * unit, cy - 8f * unit, iconPaint);
        canvas.DrawLine(cx + 5f * unit, cy - 13f * unit, cx + 5f * unit, cy - 8f * unit, iconPaint);
        canvas.DrawPoint(cx - 4f * unit, cy + 2f * unit, iconPaint);
        canvas.DrawPoint(cx + 4f * unit, cy + 2f * unit, iconPaint);
        canvas.DrawPoint(cx - 4f * unit, cy + 7f * unit, iconPaint);
    }

    private void DrawNext(Canvas canvas, float cx, float cy, float unit)
    {
        canvas.DrawLine(cx - 11f * unit, cy, cx + 10f * unit, cy, iconPaint);
        canvas.DrawLine(cx + 4f * unit, cy - 6f * unit, cx + 10f * unit, cy, iconPaint);
        canvas.DrawLine(cx + 4f * unit, cy + 6f * unit, cx + 10f * unit, cy, iconPaint);
    }

    private void DrawCheck(Canvas canvas, float cx, float cy, float unit)
    {
        using var path = new GraphicsPath();
        path.MoveTo(cx - 10f * unit, cy);
        path.LineTo(cx - 3f * unit, cy + 7f * unit);
        path.LineTo(cx + 11f * unit, cy - 8f * unit);
        canvas.DrawPath(path, iconPaint);
    }

    private void DrawAlert(Canvas canvas, float cx, float cy, float unit)
    {
        canvas.DrawLine(cx, cy - 10f * unit, cx, cy + 2f * unit, iconPaint);
        iconPaint.SetStyle(Paint.Style.Fill);
        canvas.DrawCircle(cx, cy + 9f * unit, 2f * unit, iconPaint);
        iconPaint.SetStyle(Paint.Style.Stroke);
    }

    private void DrawSmallCheck(Canvas canvas, float cx, float cy, float unit)
    {
        using var path = new GraphicsPath();
        path.MoveTo(cx - 3f * unit, cy);
        path.LineTo(cx - 1f * unit, cy + 3f * unit);
        path.LineTo(cx + 4f * unit, cy - 3f * unit);
        canvas.DrawPath(path, iconPaint);
    }

    private void DrawMap(Canvas canvas, float cx, float cy, float unit)
    {
        canvas.DrawLine(cx - 9f * unit, cy + 8f * unit, cx, cy - 7f * unit, iconPaint);
        canvas.DrawLine(cx, cy - 7f * unit, cx + 10f * unit, cy + 7f * unit, iconPaint);
        iconPaint.SetStyle(Paint.Style.Fill);
        canvas.DrawCircle(cx - 10f * unit, cy + 9f * unit, 3f * unit, iconPaint);
        canvas.DrawCircle(cx, cy - 9f * unit, 3f * unit, iconPaint);
        canvas.DrawCircle(cx + 11f * unit, cy + 9f * unit, 3f * unit, iconPaint);
        iconPaint.SetStyle(Paint.Style.Stroke);
    }

    private void DrawReview(Canvas canvas, float cx, float cy, float unit)
    {
        canvas.DrawCircle(cx - 3f * unit, cy - 3f * unit, 8f * unit, iconPaint);
        canvas.DrawLine(cx + 3f * unit, cy + 3f * unit, cx + 11f * unit, cy + 11f * unit, iconPaint);
        using var path = new GraphicsPath();
        path.MoveTo(cx - 7f * unit, cy - 3f * unit);
        path.LineTo(cx - 3f * unit, cy + 1f * unit);
        path.LineTo(cx + 3f * unit, cy - 6f * unit);
        canvas.DrawPath(path, iconPaint);
    }

    private void DrawData(Canvas canvas, float cx, float cy, float unit)
    {
        using var top = new RectF(cx - 11f * unit, cy - 10f * unit, cx + 11f * unit, cy - 3f * unit);
        using var bottom = new RectF(cx - 11f * unit, cy + 4f * unit, cx + 11f * unit, cy + 11f * unit);
        canvas.DrawOval(top, iconPaint);
        canvas.DrawLine(cx - 11f * unit, cy - 7f * unit, cx - 11f * unit, cy + 8f * unit, iconPaint);
        canvas.DrawLine(cx + 11f * unit, cy - 7f * unit, cx + 11f * unit, cy + 8f * unit, iconPaint);
        canvas.DrawArc(bottom, 0, 180, false, iconPaint);
        canvas.DrawArc(new RectF(cx - 11f * unit, cy - 3f * unit, cx + 11f * unit, cy + 4f * unit), 0, 180, false, iconPaint);
    }

    private void DrawBack(Canvas canvas, float cx, float cy, float unit)
    {
        canvas.DrawLine(cx - 10f * unit, cy, cx + 11f * unit, cy, iconPaint);
        canvas.DrawLine(cx - 10f * unit, cy, cx - 3f * unit, cy - 7f * unit, iconPaint);
        canvas.DrawLine(cx - 10f * unit, cy, cx - 3f * unit, cy + 7f * unit, iconPaint);
    }

}
