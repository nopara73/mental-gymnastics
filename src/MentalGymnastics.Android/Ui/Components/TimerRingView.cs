using Android.Content;
using Android.Graphics;
using Android.Views;
using MentalGymnastics.App;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Android;

internal sealed class TimerRingView : View
{
    private readonly Paint trackPaint;
    private readonly Paint progressPaint;
    private readonly Paint textPaint;
    private PreUiLiveSessionTimerState timer =
        new(TimeSpan.Zero, Remaining: null, Progress: null, IsTimed: false);
    private RuntimeSessionLifecycleStatus lifecycleStatus = RuntimeSessionLifecycleStatus.Running;

    public TimerRingView(Context context)
        : base(context)
    {
        trackPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = MgColors.Hairline,
            StrokeWidth = MgSpacing.Dp(context, 10),
        };
        trackPaint.SetStyle(Paint.Style.Stroke);
        trackPaint.StrokeCap = Paint.Cap.Round;

        progressPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = MgColors.Training,
            StrokeWidth = MgSpacing.Dp(context, 10),
        };
        progressPaint.SetStyle(Paint.Style.Stroke);
        progressPaint.StrokeCap = Paint.Cap.Round;

        textPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = MgColors.Ink,
            TextAlign = Paint.Align.Center,
            TextSize = MgSpacing.Dp(context, 20),
        };
        textPaint.SetTypeface(Typeface.Create(Typeface.Default, TypefaceStyle.Bold));

        SetMinimumWidth(MgSpacing.Dp(context, 144));
        SetMinimumHeight(MgSpacing.Dp(context, 144));
    }

    public void Update(
        PreUiLiveSessionTimerState nextTimer,
        RuntimeSessionLifecycleStatus nextLifecycleStatus)
    {
        timer = nextTimer ?? throw new ArgumentNullException(nameof(nextTimer));
        lifecycleStatus = nextLifecycleStatus;
        progressPaint.Color = ColorFor(nextLifecycleStatus);
        ContentDescription = timer.IsTimed
            ? $"Timer, {Format(timer.Remaining ?? TimeSpan.Zero)} remaining"
            : $"Timer, {Format(timer.Elapsed)} elapsed";
        Invalidate();
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        var stroke = progressPaint.StrokeWidth;
        var size = Math.Min(Width, Height) - (int)(stroke * 2);
        if (size <= 0)
        {
            return;
        }

        var left = (Width - size) / 2f;
        var top = (Height - size) / 2f;
        using var bounds = new RectF(left, top, left + size, top + size);

        canvas.DrawOval(bounds, trackPaint);
        var progress = timer.Progress ?? 0d;
        canvas.DrawArc(bounds, -90, (float)(360d * Math.Clamp(progress, 0d, 1d)), false, progressPaint);

        var centerY = Height / 2f - (textPaint.Descent() + textPaint.Ascent()) / 2f;
        canvas.DrawText(timer.IsTimed ? Format(timer.Remaining ?? TimeSpan.Zero) : Format(timer.Elapsed), Width / 2f, centerY, textPaint);
    }

    private static Color ColorFor(RuntimeSessionLifecycleStatus status)
    {
        return status switch
        {
            RuntimeSessionLifecycleStatus.Paused => MgColors.Recovery,
            RuntimeSessionLifecycleStatus.Abandoned or RuntimeSessionLifecycleStatus.Failed => MgColors.Blocked,
            RuntimeSessionLifecycleStatus.Completed => MgColors.Owned,
            _ => MgColors.Training,
        };
    }

    private static string Format(TimeSpan value)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(value.TotalSeconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}
