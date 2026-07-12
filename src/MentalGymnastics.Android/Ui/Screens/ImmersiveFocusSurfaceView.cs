using Android.Content;
using Android.Views;
using Android.Widget;
using MentalGymnastics.App;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Android;

/// <summary>
/// A deliberately chrome-free practice surface for focus-hold work, including
/// an AI wrapper whose immediate source task is a focus hold.
/// The whole surface is the wander-report action; setup and evidence belong
/// before or after the hold, never beside the held target.
/// </summary>
internal sealed class ImmersiveFocusSurfaceView : FrameLayout
{
    private const long AcknowledgementPulseMilliseconds = 160;

    private readonly TargetMaterialView target;
    private readonly TextView distractor;
    private bool commandPending;
    private PreUiLiveSessionCommandOutcome? lastCommandOutcome;
    private PreUiLiveSessionCommandOutcome? pendingPreviousOutcome;
    private RuntimeInputCommandKind? activeCommand;
    private string? activeTargetId;
    private string? activeValue;
    private string lastSemanticDescription = string.Empty;
    private int acknowledgementPulseGeneration;

    public ImmersiveFocusSurfaceView(Context context)
        : base(context)
    {
        SetBackgroundColor(MgColors.Canvas);
        Clickable = true;
        Focusable = true;
        HapticFeedbackEnabled = true;
        ImportantForAccessibility = ImportantForAccessibility.Yes;

        target = new TargetMaterialView(context, compact: false)
        {
            ImportantForAccessibility = ImportantForAccessibility.No,
        };
        target.SetLiveStimulusOnly(true);
        AddView(target, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));

        distractor = new TextView(context)
        {
            Gravity = GravityFlags.Center,
            ImportantForAccessibility = ImportantForAccessibility.No,
            Visibility = ViewStates.Gone,
        };
        MgTypography.ApplyTitle(distractor);
        distractor.SetTextColor(MgColors.InkDeep);
        var distractorLayout = new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent,
            GravityFlags.CenterHorizontal | GravityFlags.Bottom);
        distractorLayout.SetMargins(
            MgSpacing.Dp(context, MgSpacing.Xl),
            0,
            MgSpacing.Dp(context, MgSpacing.Xl),
            MgSpacing.Dp(context, 72));
        AddView(distractor, distractorLayout);

        Click += (_, _) => PerformSurfaceAction();
    }

    public event Action<RuntimeInputCommandKind, string?, string?>? CommandRequested;

    public void Update(AndroidLiveSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var presentation = snapshot.Presentation;
        if (commandPending &&
            !ReferenceEquals(snapshot.LiveSession.LastCommand, pendingPreviousOutcome))
        {
            commandPending = false;
        }
        lastCommandOutcome = snapshot.LiveSession.LastCommand;

        var responseRequiredInterruption = snapshot.LiveSession.ActiveCue is
        {
            Kind: RuntimeCueKind.Interruption,
            ResponseExpectation: RuntimeCueResponseExpectation.ResponseRequired,
        };
        var canRespondToInterruption = responseRequiredInterruption &&
            presentation.AvailableCommands.Any(command =>
                command.Command == RuntimeInputCommandKind.RespondToCue);
        var canMarkDrift = presentation.AvailableCommands.Any(command =>
            command.Command == RuntimeInputCommandKind.MarkDrift);
        activeCommand = canRespondToInterruption
            ? RuntimeInputCommandKind.RespondToCue
            : canMarkDrift
                ? RuntimeInputCommandKind.MarkDrift
                : null;
        activeTargetId = canRespondToInterruption
            ? snapshot.LiveSession.ActiveCue?.CueId
            : null;
        activeValue = canRespondToInterruption ? "resume" : null;

        var targetMaterial = presentation.SourceDrill.HasValue
            ? null
            : presentation.Work.Exercise.PrimaryMaterial;
        if (string.IsNullOrWhiteSpace(targetMaterial))
        {
            targetMaterial = presentation.CurrentMaterials
                .FirstOrDefault(material =>
                    string.Equals(material.Kind, "Target", StringComparison.OrdinalIgnoreCase) ||
                    material.Kind.Contains("Target", StringComparison.OrdinalIgnoreCase))
                ?.Value;
        }

        var displayTarget = CleanMaterial(targetMaterial, "Target");
        target.Update(displayTarget);

        var activeDistractor = snapshot.LiveSession.ActiveCue is
        {
            IsControlledDistractor: true,
        } cue
            ? CleanMaterial(cue.Cue, string.Empty)
            : string.Empty;
        var interruption = canRespondToInterruption
            ? CompactInterruption(snapshot.LiveSession.ActiveCue?.Cue)
            : string.Empty;
        var actionPrompt = canRespondToInterruption
            ? $"{interruption}{Environment.NewLine}Resume from the last stable step.{Environment.NewLine}Tap anywhere after resuming."
            : activeDistractor;
        distractor.Text = actionPrompt;
        distractor.Visibility = string.IsNullOrWhiteSpace(actionPrompt)
            ? ViewStates.Gone
            : ViewStates.Visible;

        var semanticDescription = canRespondToInterruption
            ? $"Target: {displayTarget}. {interruption}. Resume from the last stable step, then double tap anywhere."
            : string.IsNullOrWhiteSpace(activeDistractor)
            ? $"Target: {displayTarget}. Double tap when attention wanders."
            : $"Target: {displayTarget}. Distractor: {activeDistractor}. Double tap when attention wanders.";
        if (!string.Equals(lastSemanticDescription, semanticDescription, StringComparison.Ordinal))
        {
            lastSemanticDescription = semanticDescription;
            ContentDescription = semanticDescription;
        }

        Enabled = activeCommand.HasValue && !commandPending;
    }

    public void Reset()
    {
        acknowledgementPulseGeneration++;
        SetBackgroundColor(MgColors.Canvas);
        commandPending = false;
        activeCommand = null;
        activeTargetId = null;
        activeValue = null;
        lastCommandOutcome = null;
        pendingPreviousOutcome = null;
        Enabled = false;
        distractor.Visibility = ViewStates.Gone;
    }

    private void PerformSurfaceAction()
    {
        if (activeCommand is not { } command || commandPending)
        {
            return;
        }

        commandPending = true;
        pendingPreviousOutcome = lastCommandOutcome;
        Enabled = false;
        PulseAcknowledgement();
        PerformHapticFeedback(FeedbackConstants.ClockTick);
        CommandRequested?.Invoke(command, activeTargetId, activeValue);
    }

    private void PulseAcknowledgement()
    {
        var generation = ++acknowledgementPulseGeneration;
        SetBackgroundColor(MgColors.TrainingPanel);
        PostDelayed(
            () =>
            {
                if (generation == acknowledgementPulseGeneration)
                {
                    SetBackgroundColor(MgColors.Canvas);
                }
            },
            AcknowledgementPulseMilliseconds);
    }

    private static string CleanMaterial(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var cleaned = value.Trim().TrimEnd('.');
        var separator = cleaned.IndexOf(':');
        if (separator >= 0 && separator < cleaned.Length - 1)
        {
            cleaned = cleaned[(separator + 1)..].Trim();
        }

        return cleaned;
    }

    private static string CompactInterruption(string? value)
    {
        var cleaned = CleanMaterial(value, "Interruption");
        if (cleaned.Contains("rule-interruption", StringComparison.OrdinalIgnoreCase))
        {
            return "Rule interruption";
        }

        if (cleaned.Contains("context-switch", StringComparison.OrdinalIgnoreCase))
        {
            return "Context switch";
        }

        if (cleaned.Contains("artifact-check", StringComparison.OrdinalIgnoreCase))
        {
            return "Artifact check";
        }

        return cleaned.Split(';', 2, StringSplitOptions.TrimEntries)[0];
    }
}
