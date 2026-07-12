using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using MentalGymnastics.App;
using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.Android;

internal sealed class LiveTrainingScreenView : LinearLayout
{
    private const string DraftSessionKey = "mental_gymnastics.live_draft.session";
    private const string DraftPhaseKey = "mental_gymnastics.live_draft.phase";
    private const string DraftResponseKey = "mental_gymnastics.live_draft.response";
    private const string DraftFieldKeysKey = "mental_gymnastics.live_draft.field_keys";
    private const string DraftFieldValuesKey = "mental_gymnastics.live_draft.field_values";
    private const string DraftChoiceKeysKey = "mental_gymnastics.live_draft.choice_keys";
    private const string DraftChoiceValuesKey = "mental_gymnastics.live_draft.choice_values";
    private const string DraftExpandedKey = "mental_gymnastics.live_draft.expanded";
    private const string DraftGuessedPairsKey = "mental_gymnastics.live_draft.guessed_pairs";
    private const string DraftHeldTargetKey = "mental_gymnastics.live_draft.held_target_unlocked";

    private readonly TextView workLabel;
    private readonly TimerRingView timer;
    private readonly TextView instruction;
    private readonly LinearLayout focusTargetBand;
    private readonly TextView focusTargetValue;
    private readonly LinearLayout cueBand;
    private readonly TextView cueMode;
    private readonly TextView cueText;
    private readonly LinearLayout correctionBand;
    private readonly TextView correctionTitle;
    private readonly TextView correctionCue;
    private readonly TextView correctionResponse;
    private readonly LinearLayout correctionChoices;
    private readonly TargetMaterialView target;
    private readonly LinearLayout materialList;
    private readonly LinearLayout cueChoices;
    private readonly SessionActionButton sourceActionButton;
    private readonly EditText responseInput;
    private readonly LinearLayout structuredResponse;
    private readonly SessionActionButton primaryButton;
    private readonly LinearLayout secondaryCommands;
    private readonly LinearLayout evidenceStrip;
    private readonly TextView[] evidenceValues;
    private readonly TextView[] evidenceLabels;

    private AndroidLiveSessionSnapshot? snapshot;
    private RuntimeInputCommandKind? primaryCommand;
    private string? sessionId;
    private string materialSignature = string.Empty;
    private string secondarySignature = string.Empty;
    private string structuredSignature = string.Empty;
    private string correctionSignature = string.Empty;
    private bool stopConfirmationArmed;
    private bool incompleteSubmissionArmed;
    private bool heldTargetReportUnlocked;
    private string incompleteSubmissionContext = string.Empty;
    private string? lastFeedbackCueId;
    private readonly HashSet<int> guessedPairIndexes = [];
    private readonly HashSet<string> expandedComponents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EditText> structuredFields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> structuredChoices = new(StringComparer.Ordinal);
    private int pairIndex;
    private LiveInputDraft? pendingDraft;

    public LiveTrainingScreenView(Context context)
        : base(context)
    {
        Orientation = Orientation.Vertical;

        workLabel = new TextView(context)
        {
            Gravity = GravityFlags.Center,
        };
        MgTypography.ApplyLabel(workLabel);
        workLabel.SetTextColor(MgColors.InkMuted);
        AddView(workLabel, MatchWrap());

        timer = new TimerRingView(context);
        var timerLayout = new LayoutParams(
            MgSpacing.Dp(context, 144),
            MgSpacing.Dp(context, 144))
        {
            Gravity = GravityFlags.CenterHorizontal,
        };
        timerLayout.SetMargins(0, MgSpacing.Dp(context, MgSpacing.Sm), 0, 0);
        AddView(timer, timerLayout);

        instruction = new TextView(context)
        {
            Gravity = GravityFlags.Center,
            AccessibilityLiveRegion = AccessibilityLiveRegion.Polite,
        };
        MgTypography.ApplyHeading(instruction);
        AddView(instruction, MatchWrapWithTop(MgSpacing.Sm));

        focusTargetBand = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Visibility = ViewStates.Gone,
        };
        focusTargetBand.SetGravity(GravityFlags.CenterVertical);
        focusTargetBand.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Sm));
        focusTargetBand.Background = MgTheme.TintedSurface(
            context,
            MgColors.TrainingTint,
            MgColors.HairlineSoft,
            cornerRadius: 8);
        var focusTargetLabel = new TextView(context)
        {
            Text = "ACTIVE TARGET",
        };
        MgTypography.ApplyLabel(focusTargetLabel);
        focusTargetLabel.SetTextColor(MgColors.TrainingDark);
        focusTargetBand.AddView(
            focusTargetLabel,
            new LayoutParams(MgSpacing.Dp(context, 112), LayoutParams.WrapContent));
        focusTargetValue = new TextView(context)
        {
            Gravity = GravityFlags.End | GravityFlags.CenterVertical,
        };
        MgTypography.ApplyHeading(focusTargetValue);
        focusTargetBand.AddView(focusTargetValue, new LayoutParams(0, LayoutParams.WrapContent, 1));
        AddView(focusTargetBand, MatchWrapWithTop(MgSpacing.Sm));

        cueBand = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Visibility = ViewStates.Gone,
        };
        cueBand.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Md));
        cueMode = new TextView(context);
        MgTypography.ApplyLabel(cueMode);
        cueBand.AddView(cueMode, MatchWrap());
        cueText = new TextView(context)
        {
            Gravity = GravityFlags.Center,
            AccessibilityLiveRegion = AccessibilityLiveRegion.Assertive,
        };
        MgTypography.ApplyTitle(cueText);
        cueBand.AddView(cueText, MatchWrapWithTop(MgSpacing.Xs));
        AddView(cueBand, MatchWrapWithTop(MgSpacing.Md));

        correctionBand = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Visibility = ViewStates.Gone,
        };
        correctionBand.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Md));
        correctionBand.Background = MgTheme.TintedSurface(
            context,
            MgColors.BlockedTint,
            MgColors.Blocked,
            cornerRadius: 8);
        correctionTitle = new TextView(context);
        MgTypography.ApplyLabel(correctionTitle);
        correctionTitle.SetTextColor(MgColors.Blocked);
        correctionBand.AddView(correctionTitle, MatchWrap());
        correctionCue = new TextView(context);
        MgTypography.ApplyTitle(correctionCue);
        correctionBand.AddView(correctionCue, MatchWrapWithTop(MgSpacing.Xs));
        correctionResponse = new TextView(context);
        MgTypography.ApplyBody(correctionResponse);
        correctionResponse.SetTextColor(MgColors.InkMuted);
        correctionBand.AddView(correctionResponse, MatchWrapWithTop(MgSpacing.Xs));
        correctionChoices = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        correctionBand.AddView(correctionChoices, MatchWrapWithTop(MgSpacing.Sm));

        target = new TargetMaterialView(context, compact: true);
        AddView(target, MatchWrapWithTop(MgSpacing.Md));

        materialList = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };

        cueChoices = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Visibility = ViewStates.Gone,
        };
        AddView(cueChoices, MatchWrapWithTop(MgSpacing.Md));

        sourceActionButton = new SessionActionButton(context, "Respond", enabled: false)
        {
            Visibility = ViewStates.Gone,
            HapticFeedbackEnabled = true,
        };
        sourceActionButton.Click += (_, _) => SendPrimaryCommand();
        AddView(sourceActionButton, MatchWrapWithTop(MgSpacing.Md));
        AddView(correctionBand, MatchWrapWithTop(MgSpacing.Sm));

        AddView(materialList, MatchWrapWithTop(MgSpacing.Md));

        responseInput = new EditText(context)
        {
            Hint = "Your answer",
        };
        responseInput.SetSingleLine(false);
        responseInput.SetMinLines(2);
        responseInput.SetMaxLines(5);
        responseInput.SetTextSize(global::Android.Util.ComplexUnitType.Sp, MgTypography.BodySp);
        responseInput.Background = MgTheme.Outline(context, MgColors.Hairline, cornerRadius: 8);
        responseInput.SetPadding(
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Sm),
            MgSpacing.Dp(context, MgSpacing.Md),
            MgSpacing.Dp(context, MgSpacing.Sm));
        AddView(responseInput, MatchWrapWithTop(MgSpacing.Md));

        structuredResponse = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            Visibility = ViewStates.Gone,
        };
        AddView(structuredResponse, MatchWrapWithTop(MgSpacing.Md));

        primaryButton = new SessionActionButton(context, "Continue", enabled: false);
        primaryButton.Click += (_, _) => SendPrimaryCommand();
        primaryButton.HapticFeedbackEnabled = true;
        AddView(primaryButton, MatchWrapWithTop(MgSpacing.Md));
        responseInput.TextChanged += (_, _) => HandleResponseChanged();

        secondaryCommands = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        AddView(secondaryCommands, MatchWrapWithTop(MgSpacing.Sm));

        evidenceStrip = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
        };
        evidenceStrip.SetPadding(0, MgSpacing.Dp(context, MgSpacing.Md), 0, 0);
        evidenceValues = new TextView[4];
        evidenceLabels = new TextView[4];
        for (var index = 0; index < evidenceValues.Length; index++)
        {
            var stack = new LinearLayout(context)
            {
                Orientation = Orientation.Vertical,
            };
            stack.SetGravity(GravityFlags.Center);
            var value = new TextView(context)
            {
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyHeading(value);
            var label = new TextView(context)
            {
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyLabel(label);
            label.SetTextColor(MgColors.InkMuted);
            stack.AddView(value, MatchWrap());
            stack.AddView(label, MatchWrap());
            evidenceStrip.AddView(stack, new LayoutParams(0, MgSpacing.Dp(context, 54), 1));
            evidenceValues[index] = value;
            evidenceLabels[index] = label;
        }

        AddView(evidenceStrip, MatchWrap());
    }

    public event Action<RuntimeInputCommandKind, string?, string?>? CommandRequested;

    public void SaveDraft(Bundle state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (snapshot?.LiveSession is not { } live)
        {
            return;
        }

        state.PutString(DraftSessionKey, live.SessionId);
        state.PutString(DraftPhaseKey, live.CurrentPhaseId);
        state.PutString(DraftResponseKey, responseInput.Text ?? string.Empty);
        state.PutStringArray(DraftFieldKeysKey, structuredFields.Keys.ToArray());
        state.PutStringArray(
            DraftFieldValuesKey,
            structuredFields.Values.Select(field => field.Text ?? string.Empty).ToArray());
        var choices = structuredChoices
            .Where(pair => !pair.Key.StartsWith("preserved:", StringComparison.Ordinal))
            .ToArray();
        state.PutStringArray(DraftChoiceKeysKey, choices.Select(pair => pair.Key).ToArray());
        state.PutStringArray(DraftChoiceValuesKey, choices.Select(pair => pair.Value).ToArray());
        state.PutStringArray(DraftExpandedKey, expandedComponents.ToArray());
        state.PutIntArray(DraftGuessedPairsKey, guessedPairIndexes.ToArray());
        state.PutBoolean(DraftHeldTargetKey, heldTargetReportUnlocked);
    }

    public void RestoreDraft(Bundle state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var draftSession = state.GetString(DraftSessionKey);
        if (string.IsNullOrWhiteSpace(draftSession))
        {
            return;
        }

        pendingDraft = new LiveInputDraft(
            draftSession,
            state.GetString(DraftPhaseKey),
            state.GetString(DraftResponseKey) ?? string.Empty,
            DictionaryFrom(
                state.GetStringArray(DraftFieldKeysKey),
                state.GetStringArray(DraftFieldValuesKey)),
            DictionaryFrom(
                state.GetStringArray(DraftChoiceKeysKey),
                state.GetStringArray(DraftChoiceValuesKey)),
            state.GetStringArray(DraftExpandedKey) ?? [],
            state.GetIntArray(DraftGuessedPairsKey) ?? [],
            state.GetBoolean(DraftHeldTargetKey, false));
    }

    public void Update(AndroidLiveSessionSnapshot next)
    {
        ArgumentNullException.ThrowIfNull(next);

        snapshot = next;
        var live = next.LiveSession;
        var presentation = next.Presentation;
        var submissionContext = $"{live.SessionId}:{live.CurrentPhaseId}";
        if (!string.Equals(
                incompleteSubmissionContext,
                submissionContext,
                StringComparison.Ordinal))
        {
            incompleteSubmissionContext = submissionContext;
            incompleteSubmissionArmed = false;
            heldTargetReportUnlocked = false;
        }

        if (!string.Equals(sessionId, live.SessionId, StringComparison.Ordinal))
        {
            sessionId = live.SessionId;
            responseInput.Text = string.Empty;
            materialSignature = string.Empty;
            secondarySignature = string.Empty;
            structuredSignature = string.Empty;
            correctionSignature = string.Empty;
            stopConfirmationArmed = false;
            incompleteSubmissionArmed = false;
            heldTargetReportUnlocked = false;
            lastFeedbackCueId = null;
            guessedPairIndexes.Clear();
            expandedComponents.Clear();
            structuredFields.Clear();
            structuredChoices.Clear();
            pairIndex = 0;
        }

        var drill = presentation.Work.Drill ?? live.Drill;
        if (drill == DrillId.DE1PairDiscrimination &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork)
        {
            var pairCount = presentation.CurrentMaterials.Count(material =>
                string.Equals(material.Kind, "DiscriminationPair", StringComparison.Ordinal));
            var persistedPairIndex = Math.Clamp(presentation.Evidence.AnswerCount, 0, pairCount);
            if (pairIndex != persistedPairIndex)
            {
                pairIndex = persistedPairIndex;
                materialSignature = string.Empty;
            }
        }

        var hasIntegratedComponents = presentation.CurrentMaterials.Any(material =>
            material.Kind == "ComponentPayload");
        var effectiveDrill = presentation.SourceDrill ?? drill;
        var isFocusHold = effectiveDrill is DrillId.FH1TargetHold or DrillId.FH2DistractorHold;
        var usesCompactFocusLayout = isFocusHold && hasIntegratedComponents;
        workLabel.Text = presentation.Work.Exercise.BranchLevelLabel;
        timer.Update(presentation.Timer, presentation.LifecycleStatus);
        UpdateTimerSize(drill, presentation.CurrentPhaseKind, hasIntegratedComponents);
        instruction.Text = usesCompactFocusLayout
            ? CompactFocusInstruction(presentation)
            : presentation.CurrentInstruction;
        var isPrep = presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.InstructionPrep;
        timer.Visibility = !isPrep && presentation.Timer.IsTimed && !usesCompactFocusLayout
            ? ViewStates.Visible
            : ViewStates.Gone;
        evidenceStrip.Visibility = isPrep ? ViewStates.Gone : ViewStates.Visible;
        var showsShiftTarget = effectiveDrill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.CueResponse &&
            !string.IsNullOrWhiteSpace(presentation.CurrentFocusTarget);
        focusTargetBand.Visibility = showsShiftTarget ? ViewStates.Visible : ViewStates.Gone;
        focusTargetValue.Text = showsShiftTarget ? presentation.CurrentFocusTarget : string.Empty;

        UpdateCue(presentation);
        UpdateCueChoices(presentation);
        UpdatePendingCorrection(presentation);

        var isReview = presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.Review;
        var showsFocusTarget = isFocusHold && presentation.CurrentPhaseKind is
            RuntimeSessionPhaseKind.InstructionPrep or
            RuntimeSessionPhaseKind.ActiveWork or
            RuntimeSessionPhaseKind.CueResponse or
            RuntimeSessionPhaseKind.Recovery;
        target.SetDense(usesCompactFocusLayout);
        UpdateMaterialOrder(usesCompactFocusLayout);
        target.Visibility = showsFocusTarget ? ViewStates.Visible : ViewStates.Gone;
        materialList.Visibility = isFocusHold && !hasIntegratedComponents ||
            isReview || presentation.CurrentMaterials.Count == 0
            ? ViewStates.Gone
            : ViewStates.Visible;
        if (showsFocusTarget)
        {
            target.Update(presentation.SourceDrill.HasValue
                ? TargetMaterial(presentation)
                : presentation.Work.Exercise.PrimaryMaterial ?? TargetMaterial(presentation));
            if (hasIntegratedComponents)
            {
                UpdateMaterialList(presentation);
            }
        }
        else if (!isReview)
        {
            UpdateMaterialList(presentation);
        }

        var isPairWork = drill == DrillId.DE1PairDiscrimination &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork;
        var needsInput = presentation.AvailableCommands.Any(command =>
            RequiresTextInput(command.Command, effectiveDrill)) && !isPairWork;
        var usesStructuredResponse = UpdateStructuredResponse(presentation);
        if (usesStructuredResponse &&
            drill == DrillId.CO1RuleExtraction &&
            presentation.CurrentMaterials.Any(material => material.Kind == "UnseenExample"))
        {
            materialList.Visibility = ViewStates.Gone;
        }

        responseInput.Visibility = needsInput && !usesStructuredResponse
            ? ViewStates.Visible
            : ViewStates.Gone;
        responseInput.Hint = ResponseHint(
            effectiveDrill,
            presentation.CurrentPhaseKind,
            presentation.CurrentMaterials);

        primaryCommand = presentation.PrimaryCommand?.Command;
        UpdatePrimaryButton(presentation);
        UpdateSecondaryCommands(presentation);
        UpdateEvidence(presentation);
        ApplyPendingDraft(presentation, live);
    }

    private void ApplyPendingDraft(
        LiveSessionPresentationReadModel presentation,
        PreUiLiveSessionState live)
    {
        if (pendingDraft is not { } draft)
        {
            return;
        }

        pendingDraft = null;
        if (!string.Equals(draft.SessionId, live.SessionId, StringComparison.Ordinal) ||
            !string.Equals(draft.PhaseId, live.CurrentPhaseId, StringComparison.Ordinal))
        {
            return;
        }

        heldTargetReportUnlocked = draft.HeldTargetReportUnlocked;
        structuredChoices.Clear();
        foreach (var choice in draft.Choices)
        {
            structuredChoices[choice.Key] = choice.Value;
        }

        expandedComponents.Clear();
        expandedComponents.UnionWith(draft.ExpandedComponents);
        guessedPairIndexes.Clear();
        guessedPairIndexes.UnionWith(draft.GuessedPairIndexes);
        structuredSignature = string.Empty;
        UpdateStructuredResponse(presentation);
        foreach (var field in draft.Fields)
        {
            if (structuredFields.TryGetValue(field.Key, out var input))
            {
                input.Text = field.Value;
            }
        }

        responseInput.Text = draft.Response;
        materialSignature = string.Empty;
        if (presentation.CurrentPhaseKind != RuntimeSessionPhaseKind.Review)
        {
            UpdateMaterialList(presentation);
        }

        UpdatePrimaryButton(presentation);
    }

    private static IReadOnlyDictionary<string, string> DictionaryFrom(
        string[]? keys,
        string[]? values)
    {
        if (keys is null || values is null || keys.Length != values.Length)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return keys
            .Select((key, index) => (key, values[index]))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.key))
            .ToDictionary(pair => pair.key, pair => pair.Item2, StringComparer.Ordinal);
    }

    private void UpdatePrimaryButton(LiveSessionPresentationReadModel presentation)
    {
        if (UpdateSourceActionButton(presentation))
        {
            primaryButton.Visibility = ViewStates.Gone;
            primaryButton.Enabled = false;
            return;
        }

        if (cueChoices.Visibility == ViewStates.Visible)
        {
            primaryButton.Visibility = ViewStates.Gone;
            primaryButton.Enabled = false;
            return;
        }

        var pairCount = presentation.CurrentMaterials.Count(material =>
            string.Equals(material.Kind, "DiscriminationPair", StringComparison.Ordinal));
        if (presentation.Work.Drill == DrillId.DE1PairDiscrimination &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork &&
            pairCount > 0 &&
            pairIndex < pairCount)
        {
            primaryButton.Visibility = ViewStates.Gone;
            primaryButton.Enabled = false;
            return;
        }

        if (presentation.PrimaryCommand is null)
        {
            primaryButton.Visibility = ViewStates.Gone;
            primaryButton.Enabled = false;
            return;
        }

        primaryButton.Visibility = ViewStates.Visible;
        var enabled = PrimaryInputIsReady(presentation);
        var label = DisplayLabel(presentation.PrimaryCommand, presentation, primary: true);
        if (presentation.PrimaryCommand.Command == RuntimeInputCommandKind.SubmitAnswer &&
            ShouldDeferHeldTargetReport(presentation) &&
            !heldTargetReportUnlocked)
        {
            label = "Report held target";
        }
        else if (presentation.PrimaryCommand.Command == RuntimeInputCommandKind.SubmitAnswer &&
            !SubmissionInputIsComplete(presentation))
        {
            label = incompleteSubmissionArmed ? "Confirm incomplete" : "Submit incomplete";
        }

        UpdatePrimaryButtonSize(presentation);
        primaryButton.Text = label;
        primaryButton.Enabled = enabled;
        primaryButton.ContentDescription = label;
        primaryButton.SetTextColor(Color.White);
        primaryButton.Background = MgTheme.ActionGradient(Context!, cornerRadius: 8);
    }

    private bool UpdateSourceActionButton(LiveSessionPresentationReadModel presentation)
    {
        if (presentation.PrimaryCommand is not { } primary)
        {
            sourceActionButton.Visibility = ViewStates.Gone;
            sourceActionButton.Enabled = false;
            return false;
        }

        var effectiveDrill = presentation.SourceDrill ?? presentation.Work.Drill ??
            throw new InvalidOperationException("Live work must identify its effective drill.");
        var inputKind = DrillInteractionProtocolCatalog.Get(effectiveDrill).InputKind;
        var isDisruptionConfirmation = primary.Command == RuntimeInputCommandKind.RespondToCue &&
            presentation.ActiveCue is { Kind: RuntimeCueKind.Interruption, RequiresResponse: true };
        var usesPad = isDisruptionConfirmation || inputKind switch
        {
            DrillInteractionInputKind.FocusReturnPad => primary.Command is
                RuntimeInputCommandKind.MarkDrift or RuntimeInputCommandKind.MarkReturn,
            DrillInteractionInputKind.GoNoGoPad => primary.Command == RuntimeInputCommandKind.RespondToCue,
            _ => false,
        };
        if (!usesPad)
        {
            sourceActionButton.Visibility = ViewStates.Gone;
            sourceActionButton.Enabled = false;
            return false;
        }

        var label = DisplayLabel(primary, presentation, primary: true);
        var isWithholdPad = primary.Command == RuntimeInputCommandKind.RespondToCue &&
            presentation.ActiveCue?.RequiresResponse == false;
        var height = InteractivePadHeight(
            isDisruptionConfirmation ? DrillInteractionInputKind.GoNoGoPad : inputKind,
            inputKind == DrillInteractionInputKind.FocusReturnPad &&
                presentation.CurrentMaterials.Any(material => material.Kind == "ComponentPayload"));
        var layout = sourceActionButton.LayoutParameters ?? MatchWrap();
        layout.Height = MgSpacing.Dp(Context!, height);
        sourceActionButton.LayoutParameters = layout;
        sourceActionButton.Text = label;
        sourceActionButton.ContentDescription = isWithholdPad
            ? "Withhold. Do not tap. Touching this pad records an error."
            : label.Replace('\n', ' ');
        sourceActionButton.Gravity = GravityFlags.Center;
        sourceActionButton.SetSingleLine(false);
        sourceActionButton.Enabled = true;
        sourceActionButton.Visibility = ViewStates.Visible;
        sourceActionButton.SetTextColor(isWithholdPad ? MgColors.InkMuted : Color.White);
        sourceActionButton.Background = isWithholdPad
            ? MgTheme.Outline(Context!, MgColors.Maintenance, cornerRadius: 8)
            : MgTheme.ActionGradient(Context!, cornerRadius: 8);
        return true;
    }

    private void UpdatePrimaryButtonSize(LiveSessionPresentationReadModel presentation)
    {
        var inputKind = presentation.Work.Exercise.InteractionProtocol.InputKind;
        var height = InteractivePadHeight(inputKind, compact: false);
        var layout = primaryButton.LayoutParameters ?? MatchWrap();
        var heightPixels = MgSpacing.Dp(Context!, height);
        if (layout.Height != heightPixels)
        {
            layout.Height = heightPixels;
            primaryButton.LayoutParameters = layout;
        }

        primaryButton.Gravity = GravityFlags.Center;
        primaryButton.SetSingleLine(false);
    }

    private void UpdateSecondaryCommands(LiveSessionPresentationReadModel presentation)
    {
        var commands = SelectSecondaryCommands(presentation);
        var signature = string.Join(
            '|',
            commands.Select(command => $"{command.Command}:{command.Label}:{presentation.CurrentPhaseKind}"));
        if (string.Equals(signature, secondarySignature, StringComparison.Ordinal))
        {
            return;
        }

        secondarySignature = signature;
        secondaryCommands.RemoveAllViews();
        if (commands.Length == 0)
        {
            secondaryCommands.Visibility = ViewStates.Gone;
            return;
        }

        secondaryCommands.Visibility = ViewStates.Visible;
        var row = new LinearLayout(Context)
        {
            Orientation = Orientation.Horizontal,
        };
        for (var index = 0; index < commands.Length; index++)
        {
            var command = commands[index];
            var label = command.Command == RuntimeInputCommandKind.Abandon &&
                stopConfirmationArmed &&
                presentation.CurrentPhaseKind != RuntimeSessionPhaseKind.InstructionPrep
                    ? "Confirm stop"
                    : DisplayLabel(command, presentation, primary: false);
            var button = new Button(Context)
            {
                Text = label,
                ContentDescription = label,
            };
            button.SetAllCaps(false);
            button.SetSingleLine(false);
            button.SetMinHeight(MgSpacing.Dp(Context!, 48));
            button.SetMinimumHeight(MgSpacing.Dp(Context!, 48));
            button.SetTextSize(global::Android.Util.ComplexUnitType.Sp, MgTypography.LabelSp);
            var destructive = command.Command == RuntimeInputCommandKind.Abandon &&
                presentation.CurrentPhaseKind != RuntimeSessionPhaseKind.InstructionPrep;
            button.SetTextColor(destructive
                ? MgColors.Blocked
                : MgColors.Ink);
            button.Background = destructive
                ? MgTheme.Outline(Context!, MgColors.Blocked, cornerRadius: 8)
                : MgTheme.MutedSurface(Context!, cornerRadius: 8);
            button.Click += (_, _) => HandleSecondaryCommand(command.Command, presentation);
            var layout = new LayoutParams(0, MgSpacing.Dp(Context!, 52), 1);
            if (index > 0)
            {
                layout.SetMargins(MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0, 0);
            }

            row.AddView(button, layout);
        }

        secondaryCommands.AddView(row, MatchWrap());
    }

    private void UpdateMaterialList(LiveSessionPresentationReadModel presentation)
    {
        if (presentation.Work.Drill == DrillId.DE1PairDiscrimination &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork)
        {
            RenderPairDiscrimination(presentation);
            return;
        }

        var hasComponentPayload = presentation.CurrentMaterials.Any(material =>
            string.Equals(material.Kind, "ComponentPayload", StringComparison.Ordinal));
        if (hasComponentPayload &&
            (presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.InstructionPrep ||
                (presentation.Work.Drill is not
                    (DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery) &&
                 presentation.CurrentPhaseKind is
                    RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.CueResponse)))
        {
            RenderGlobalReviewComponents(presentation);
            return;
        }

        if (presentation.Work.Drill == DrillId.AI2DisruptionRecovery &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork)
        {
            RenderDisruptionRecovery(presentation);
            return;
        }

        var visibleMaterials = presentation.CurrentMaterials
            .Where(material => ShouldShowMaterial(presentation, material))
            .ToArray();
        materialList.Visibility = visibleMaterials.Length == 0 ? ViewStates.Gone : ViewStates.Visible;
        var signature = string.Join(
            '|',
            visibleMaterials.Select(material => $"{material.Kind}:{material.Name}:{material.Value}"));
        if (string.Equals(signature, materialSignature, StringComparison.Ordinal))
        {
            return;
        }

        materialSignature = signature;
        materialList.RemoveAllViews();
        var materialGroups = visibleMaterials
            .GroupBy(MaterialGroupingKey, StringComparer.Ordinal)
            .ToArray();
        foreach (var group in materialGroups)
        {
            var material = group.First();
            var block = new LinearLayout(Context)
            {
                Orientation = Orientation.Vertical,
                Background = MgTheme.MutedSurface(Context!, cornerRadius: 8),
            };
            block.SetPadding(
                MgSpacing.Dp(Context!, MgSpacing.Md),
                MgSpacing.Dp(Context!, MgSpacing.Sm),
                MgSpacing.Dp(Context!, MgSpacing.Md),
                MgSpacing.Dp(Context!, MgSpacing.Sm));

            var label = new TextView(Context)
            {
                Text = MaterialLabel(material),
            };
            MgTypography.ApplyLabel(label);
            label.SetTextColor(MgColors.InkMuted);
            block.AddView(label, MatchWrap());

            var value = new TextView(Context)
            {
                Text = MaterialGroupValue(group),
            };
            value.SetTextIsSelectable(true);
            MgTypography.ApplyBody(value);
            block.AddView(value, MatchWrapWithTop(MgSpacing.Xs));
            materialList.AddView(block, MatchWrapWithTop(materialList.ChildCount == 0 ? 0 : MgSpacing.Sm));
        }
    }

    private void UpdateMaterialOrder(bool beforeSourceAction)
    {
        var materialIndex = IndexOfChild(materialList);
        var sourceIndex = IndexOfChild(sourceActionButton);
        var correctionIndex = IndexOfChild(correctionBand);
        var alreadyOrdered = beforeSourceAction
            ? materialIndex == sourceIndex - 1
            : materialIndex == correctionIndex + 1;
        if (alreadyOrdered)
        {
            return;
        }

        RemoveView(materialList);
        sourceIndex = IndexOfChild(sourceActionButton);
        correctionIndex = IndexOfChild(correctionBand);
        AddView(
            materialList,
            beforeSourceAction ? sourceIndex : correctionIndex + 1,
            MatchWrapWithTop(MgSpacing.Md));
    }

    private void RenderGlobalReviewComponents(LiveSessionPresentationReadModel presentation)
    {
        var transferContract = TransferContractMaterials(presentation);
        var components = presentation.CurrentMaterials
            .Where(material => string.Equals(material.Kind, "ComponentPayload", StringComparison.Ordinal))
            .ToArray();
        var signature = "global-review:" + string.Join(
            '|',
            transferContract
                .Select(material => $"{material.Kind}:{material.Value}")
                .Concat(components.Select(component =>
                    $"{component.Name}:{expandedComponents.Contains(component.Name)}")));
        if (string.Equals(signature, materialSignature, StringComparison.Ordinal))
        {
            return;
        }

        materialSignature = signature;
        materialList.RemoveAllViews();
        materialList.Visibility = components.Length == 0 && transferContract.Length == 0
            ? ViewStates.Gone
            : ViewStates.Visible;
        AddTransferContract(transferContract);
        AddComponentTiles(presentation, components, transferContract.Length > 0);
    }

    private void AddComponentTiles(
        LiveSessionPresentationReadModel presentation,
        IReadOnlyList<PreUiLiveSessionMaterialState> components,
        bool hasPriorContent)
    {
        var effectiveDrill = presentation.SourceDrill ?? presentation.Work.Drill;
        var hasExpandedComponent = components.Any(component =>
            expandedComponents.Contains(component.Name));
        var compactFocusComponents =
            effectiveDrill is (DrillId.FH1TargetHold or DrillId.FH2DistractorHold) &&
            components.Count is > 0 and <= 3 &&
            !hasExpandedComponent;
        var columns = compactFocusComponents ? components.Count : 2;
        for (var index = 0; index < components.Count; index += columns)
        {
            var row = new LinearLayout(Context)
            {
                Orientation = Orientation.Horizontal,
            };
            row.SetGravity(GravityFlags.FillHorizontal);
            for (var column = 0; column < columns; column++)
            {
                var componentIndex = index + column;
                if (componentIndex >= components.Count)
                {
                    row.AddView(new Space(Context), new LayoutParams(0, 1, 1));
                    continue;
                }

                var component = components[componentIndex];
                var summary = GlobalReviewComponentSummary(component.Value);
                var isHeldTarget = string.Equals(summary.Branch, "FH", StringComparison.Ordinal);
                var isSetup = presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.InstructionPrep;
                var expanded = isHeldTarget
                    ? isSetup
                    : expandedComponents.Contains(component.Name);
                var tile = new LinearLayout(Context)
                {
                    Orientation = Orientation.Vertical,
                    Background = MgTheme.MutedSurface(Context!, cornerRadius: 8),
                    ContentDescription = $"{summary.Branch}, {summary.Role}. {summary.Criterion}",
                    Clickable = !isHeldTarget,
                    Focusable = !isHeldTarget,
                };
                tile.SetPadding(
                    MgSpacing.Dp(Context!, MgSpacing.Sm),
                    MgSpacing.Dp(Context!, MgSpacing.Sm),
                    MgSpacing.Dp(Context!, MgSpacing.Sm),
                    MgSpacing.Dp(Context!, MgSpacing.Sm));
                var header = new TextView(Context)
                {
                    Text = isHeldTarget
                        ? $"{summary.Branch}    {(isSetup ? "HOLD" : "HELD")}"
                        : $"{summary.Branch}    {(expanded ? "-" : "+")}",
                };
                MgTypography.ApplyLabel(header);
                header.SetTextColor(MgColors.TrainingDark);
                tile.AddView(header, MatchWrap());
                var role = new TextView(Context)
                {
                    Text = isHeldTarget
                        ? isSetup ? "Memorize and hold" : "Target hidden"
                        : summary.Role,
                };
                MgTypography.ApplyBody(role);
                tile.AddView(role, MatchWrapWithTop(MgSpacing.Xs));
                if (expanded)
                {
                    var challenge = new TextView(Context)
                    {
                        Text = isHeldTarget
                            ? $"{HeldTargetValue(summary.Challenge)}{Environment.NewLine}Report last"
                            : $"{summary.Challenge}{Environment.NewLine}" +
                                (presentation.AvailableCommands.Any(command =>
                                    command.Command == RuntimeInputCommandKind.SubmitAnswer)
                                    ? $"Enter {summary.Branch}=answer below."
                                    : "Keep the answer for the response step."),
                    };
                    if (isHeldTarget)
                    {
                        MgTypography.ApplyTitle(challenge);
                        challenge.Gravity = GravityFlags.Center;
                    }
                    else
                    {
                        MgTypography.ApplyBody(challenge);
                    }

                    tile.AddView(challenge, MatchWrapWithTop(MgSpacing.Sm));

                    if (!isHeldTarget)
                    {
                        var criterion = new TextView(Context)
                        {
                            Text = summary.Criterion,
                        };
                        MgTypography.ApplyLabel(criterion);
                        criterion.SetTextColor(MgColors.InkMuted);
                        tile.AddView(criterion, MatchWrapWithTop(MgSpacing.Sm));
                    }
                }

                if (!isHeldTarget)
                {
                    tile.Click += (_, _) =>
                    {
                        if (!expandedComponents.Add(component.Name))
                        {
                            expandedComponents.Remove(component.Name);
                        }

                        materialSignature = string.Empty;
                        UpdateMaterialList(presentation);
                    };
                }
                var layout = new LayoutParams(0, LayoutParams.WrapContent, 1);
                if (column > 0)
                {
                    layout.SetMargins(MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0, 0);
                }

                row.AddView(tile, layout);
            }

            materialList.AddView(
                row,
                MatchWrapWithTop(index == 0 && !hasPriorContent ? 0 : MgSpacing.Xs));
        }
    }

    private void UpdateTimerSize(
        DrillId drill,
        RuntimeSessionPhaseKind? phase,
        bool hasIntegratedComponents)
    {
        var screenHeight = Context?.Resources?.Configuration?.ScreenHeightDp ?? 720;
        var size = hasIntegratedComponents
            ? 96
            : drill == DrillId.TI2GlobalReviewTask && phase == RuntimeSessionPhaseKind.ActiveWork
            ? 96
            : screenHeight < 600
                ? 112
                : screenHeight < 720
                    ? 128
                    : 144;
        if (timer.LayoutParameters?.Width == MgSpacing.Dp(Context!, size))
        {
            return;
        }

        var layout = timer.LayoutParameters ?? new LayoutParams(0, 0);
        layout.Width = MgSpacing.Dp(Context!, size);
        layout.Height = MgSpacing.Dp(Context!, size);
        timer.LayoutParameters = layout;
    }

    private int InteractivePadHeight(DrillInteractionInputKind inputKind, bool compact)
    {
        if (inputKind is not
            (DrillInteractionInputKind.FocusReturnPad or DrillInteractionInputKind.GoNoGoPad))
        {
            return 56;
        }

        if (compact)
        {
            return 112;
        }

        var screenHeight = Context?.Resources?.Configuration?.ScreenHeightDp ?? 720;
        if (screenHeight < 600)
        {
            return 124;
        }

        if (screenHeight < 720)
        {
            return inputKind == DrillInteractionInputKind.FocusReturnPad ? 176 : 144;
        }

        return inputKind == DrillInteractionInputKind.FocusReturnPad ? 220 : 160;
    }

    private void RenderStructureMapping(LiveSessionPresentationReadModel presentation)
    {
        var relations = presentation.CurrentMaterials
            .Where(material => string.Equals(material.Kind, "RequiredRelation", StringComparison.Ordinal))
            .ToArray();
        var signature = "structure-map:" + string.Join('|', relations.Select(relation => relation.Value));
        if (string.Equals(signature, materialSignature, StringComparison.Ordinal))
        {
            return;
        }

        materialSignature = signature;
        materialList.RemoveAllViews();
        materialList.Visibility = relations.Length == 0 ? ViewStates.Gone : ViewStates.Visible;
        foreach (var relation in relations)
        {
            var mapping = StructureRelation(relation.Value);
            var row = new LinearLayout(Context)
            {
                Orientation = Orientation.Horizontal,
                Background = MgTheme.MutedSurface(Context!, cornerRadius: 8),
            };
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(
                MgSpacing.Dp(Context!, MgSpacing.Sm),
                MgSpacing.Dp(Context!, MgSpacing.Sm),
                MgSpacing.Dp(Context!, MgSpacing.Sm),
                MgSpacing.Dp(Context!, MgSpacing.Sm));
            var source = new TextView(Context)
            {
                Text = mapping.Source,
                Gravity = GravityFlags.CenterVertical,
            };
            MgTypography.ApplyBody(source);
            row.AddView(source, new LayoutParams(0, LayoutParams.WrapContent, 1));
            var arrow = new TextView(Context)
            {
                Text = ">",
                Gravity = GravityFlags.Center,
                ImportantForAccessibility = ImportantForAccessibility.No,
            };
            MgTypography.ApplyHeading(arrow);
            arrow.SetTextColor(MgColors.TrainingDark);
            row.AddView(arrow, new LayoutParams(MgSpacing.Dp(Context!, 32), LayoutParams.WrapContent));
            var targetValue = new TextView(Context)
            {
                Text = mapping.Target,
                Gravity = GravityFlags.CenterVertical,
            };
            MgTypography.ApplyBody(targetValue);
            row.AddView(targetValue, new LayoutParams(0, LayoutParams.WrapContent, 1));
            materialList.AddView(row, MatchWrapWithTop(materialList.ChildCount == 0 ? 0 : MgSpacing.Xs));
        }
    }

    private static (string Source, string Target) StructureRelation(string value)
    {
        const string sourceMarker = "source relation ";
        const string targetMarker = "; target relation ";
        var sourceStart = value.IndexOf(sourceMarker, StringComparison.OrdinalIgnoreCase);
        var targetStart = value.IndexOf(targetMarker, StringComparison.OrdinalIgnoreCase);
        if (sourceStart < 0 || targetStart <= sourceStart)
        {
            return (value, "Map relation");
        }

        sourceStart += sourceMarker.Length;
        var source = value[sourceStart..targetStart].Trim().TrimEnd('.');
        targetStart += targetMarker.Length;
        var targetValue = value[targetStart..].Trim().TrimEnd('.');
        return (SentenceCase(source), SentenceCase(targetValue));
    }

    private void RenderDisruptionRecovery(LiveSessionPresentationReadModel presentation)
    {
        var transferContract = TransferContractMaterials(presentation);
        var components = presentation.CurrentMaterials
            .Where(material => string.Equals(material.Kind, "ComponentPayload", StringComparison.Ordinal))
            .ToArray();
        var source = presentation.CurrentMaterials.FirstOrDefault(material => material.Kind == "SourceTask")?.Value ?? string.Empty;
        var sourceStandard = presentation.CurrentMaterials.FirstOrDefault(material =>
            material.Kind == "SourceBranchStandard")?.Value ?? string.Empty;
        var disruption = presentation.CurrentMaterials.FirstOrDefault(material => material.Kind == "DisruptionEvent")?.Value ?? string.Empty;
        var restart = presentation.CurrentMaterials.FirstOrDefault(material => material.Kind == "RestartRule")?.Value ?? string.Empty;
        var signature = $"recovery:{source}|{sourceStandard}|{disruption}|{restart}|" + string.Join(
            '|',
            transferContract
                .Select(material => material.Value)
                .Concat(components.Select(component =>
                    $"{component.Name}:{expandedComponents.Contains(component.Name)}")));
        if (string.Equals(signature, materialSignature, StringComparison.Ordinal))
        {
            return;
        }

        materialSignature = signature;
        materialList.RemoveAllViews();
        materialList.Visibility = ViewStates.Visible;
        AddTransferContract(transferContract);
        var plan = new LinearLayout(Context)
        {
            Orientation = Orientation.Horizontal,
            Background = MgTheme.MutedSurface(Context!, cornerRadius: 8),
        };
        plan.SetGravity(GravityFlags.CenterVertical);
        plan.SetPadding(
            MgSpacing.Dp(Context!, MgSpacing.Sm),
            MgSpacing.Dp(Context!, MgSpacing.Sm),
            MgSpacing.Dp(Context!, MgSpacing.Sm),
            MgSpacing.Dp(Context!, MgSpacing.Sm));
        var planLabel = new TextView(Context)
        {
            Text = "PASS RULE",
        };
        MgTypography.ApplyLabel(planLabel);
        planLabel.SetTextColor(MgColors.TrainingDark);
        plan.AddView(planLabel, new LayoutParams(MgSpacing.Dp(Context!, 96), LayoutParams.WrapContent));
        var planValue = new TextView(Context)
        {
            Text = $"{CompactWrappedSourceCriterion(source) ?? CompactSourceCriterion(sourceStandard)}" +
                $"{Environment.NewLine}{CompactDisruption(disruption)} · " +
                CompactRestart(restart).Replace(Environment.NewLine, " · ", StringComparison.Ordinal),
        };
        MgTypography.ApplyBody(planValue);
        plan.AddView(planValue, new LayoutParams(0, LayoutParams.WrapContent, 1));

        materialList.AddView(plan, MatchWrapWithTop(transferContract.Length == 0 ? 0 : MgSpacing.Xs));
        AddComponentTiles(presentation, components, hasPriorContent: true);
    }

    private static string CompactFocusInstruction(LiveSessionPresentationReadModel presentation)
    {
        if (!presentation.Timer.IsTimed)
        {
            return presentation.CurrentInstruction;
        }

        var remaining = presentation.Timer.Remaining ?? presentation.Timer.Elapsed;
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        var time = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        return $"{presentation.CurrentInstruction.Trim().TrimEnd('.')}  ·  {time}";
    }

    private static PreUiLiveSessionMaterialState[] TransferContractMaterials(
        LiveSessionPresentationReadModel presentation)
    {
        return presentation.CurrentMaterials
            .Where(material => material.Kind is
                "TransferTask" or
                "SameDemand" or
                "ChangedContext" or
                "SourceBranchStandard")
            .ToArray();
    }

    private void AddTransferContract(IReadOnlyList<PreUiLiveSessionMaterialState> materials)
    {
        if (materials.Count == 0)
        {
            return;
        }

        var block = new LinearLayout(Context)
        {
            Orientation = Orientation.Vertical,
            Background = MgTheme.TintedSurface(
                Context!,
                MgColors.TrainingTint,
                MgColors.HairlineSoft,
                cornerRadius: 8),
        };
        block.SetPadding(
            MgSpacing.Dp(Context!, MgSpacing.Md),
            MgSpacing.Dp(Context!, MgSpacing.Sm),
            MgSpacing.Dp(Context!, MgSpacing.Md),
            MgSpacing.Dp(Context!, MgSpacing.Sm));

        var header = new TextView(Context)
        {
            Text = "TRANSFER",
        };
        MgTypography.ApplyLabel(header);
        header.SetTextColor(MgColors.TrainingDark);
        block.AddView(header, MatchWrap());

        foreach (var material in materials)
        {
            var row = new LinearLayout(Context)
            {
                Orientation = Orientation.Horizontal,
            };
            row.SetGravity(GravityFlags.Top);

            var label = new TextView(Context)
            {
                Text = MaterialLabel(material),
            };
            MgTypography.ApplyLabel(label);
            label.SetTextColor(MgColors.InkMuted);
            row.AddView(label, new LayoutParams(MgSpacing.Dp(Context!, 76), LayoutParams.WrapContent));

            var value = new TextView(Context)
            {
                Text = CompactMaterialValue(material.Kind, material.Value),
            };
            MgTypography.ApplyBody(value);
            row.AddView(value, new LayoutParams(0, LayoutParams.WrapContent, 1));
            block.AddView(row, MatchWrapWithTop(MgSpacing.Xs));
        }

        materialList.AddView(block, MatchWrap());
    }

    private static string CompactRecoverySource(string value)
    {
        var standard = SegmentValue(value, "source task ") ?? "source task";
        standard = standard.Split(':', StringSplitOptions.TrimEntries)[0];
        return standard.Replace('-', ' ');
    }

    private static string CompactDisruption(string value)
    {
        if (value.Contains("context-switch", StringComparison.OrdinalIgnoreCase))
        {
            return "Context switch";
        }

        if (value.Contains("rule-interruption", StringComparison.OrdinalIgnoreCase))
        {
            return "Rule cue";
        }

        return value.Contains("artifact-check", StringComparison.OrdinalIgnoreCase)
            ? "Evidence check"
            : "Interruption";
    }

    private static string CompactRestart(string value)
    {
        var delay = SegmentValue(value, "restart delay metadata ") ?? "10 seconds";
        return $"{delay}{Environment.NewLine}Last step";
    }

    private void RenderPairDiscrimination(LiveSessionPresentationReadModel presentation)
    {
        var pairs = presentation.CurrentMaterials
            .Where(material => string.Equals(material.Kind, "DiscriminationPair", StringComparison.Ordinal))
            .ToArray();
        var signature = $"pair:{pairIndex}:{guessedPairIndexes.Contains(pairIndex)}:{string.Join('|', pairs.Select(pair => pair.Value))}";
        if (string.Equals(signature, materialSignature, StringComparison.Ordinal))
        {
            return;
        }

        materialSignature = signature;
        materialList.RemoveAllViews();
        if (pairs.Length == 0 || pairIndex >= pairs.Length)
        {
            var ready = new TextView(Context)
            {
                Text = $"{pairIndex} comparisons recorded",
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyHeading(ready);
            materialList.AddView(ready, MatchWrap());
            return;
        }

        var progress = new TextView(Context)
        {
            Text = $"PAIR {pairIndex + 1} OF {pairs.Length}",
            Gravity = GravityFlags.Center,
        };
        MgTypography.ApplyLabel(progress);
        progress.SetTextColor(MgColors.InkMuted);
        materialList.AddView(progress, MatchWrap());

        var comparison = new TextView(Context)
        {
            Text = DiscriminationPairDisplay(pairs[pairIndex].Value),
            Gravity = GravityFlags.Center,
            Background = MgTheme.MutedSurface(Context!, cornerRadius: 8),
        };
        comparison.SetPadding(
            MgSpacing.Dp(Context!, MgSpacing.Md),
            MgSpacing.Dp(Context!, MgSpacing.Md),
            MgSpacing.Dp(Context!, MgSpacing.Md),
            MgSpacing.Dp(Context!, MgSpacing.Md));
        MgTypography.ApplyHeading(comparison);
        materialList.AddView(comparison, MatchWrapWithTop(MgSpacing.Sm));

        var uncertainty = new Button(Context)
        {
            Text = guessedPairIndexes.Contains(pairIndex) ? "Not sure marked" : "Not sure",
            ContentDescription = guessedPairIndexes.Contains(pairIndex)
                ? $"Uncertainty marked for pair {pairIndex + 1}"
                : $"Mark uncertainty for pair {pairIndex + 1}",
            Enabled = !guessedPairIndexes.Contains(pairIndex),
        };
        uncertainty.SetAllCaps(false);
        uncertainty.SetTextColor(MgColors.Ink);
        uncertainty.Background = MgTheme.MutedSurface(Context!, cornerRadius: 8);
        uncertainty.Click += (_, _) =>
        {
            guessedPairIndexes.Add(pairIndex);
            PerformAcknowledgement(
                presentation.Work.Exercise.InteractionProtocol,
                RuntimeInputCommandKind.MarkGuess);
            CommandRequested?.Invoke(
                RuntimeInputCommandKind.MarkGuess,
                pairs[pairIndex].Name,
                null);
            materialSignature = string.Empty;
            RenderPairDiscrimination(presentation);
        };
        materialList.AddView(
            uncertainty,
            new LayoutParams(LayoutParams.MatchParent, MgSpacing.Dp(Context!, 48))
            {
                TopMargin = MgSpacing.Dp(Context!, MgSpacing.Sm),
            });

        var choices = new LinearLayout(Context)
        {
            Orientation = Orientation.Horizontal,
        };
        AddPairChoice(choices, "Same", "same", pairs[pairIndex], presentation, first: true);
        AddPairChoice(choices, "Different", "different", pairs[pairIndex], presentation, first: false);
        materialList.AddView(choices, MatchWrapWithTop(MgSpacing.Sm));
    }

    private void AddPairChoice(
        LinearLayout row,
        string label,
        string value,
        PreUiLiveSessionMaterialState pair,
        LiveSessionPresentationReadModel presentation,
        bool first)
    {
        var button = new Button(Context)
        {
            Text = label,
            ContentDescription = $"{label}, pair {pairIndex + 1}",
        };
        button.SetAllCaps(false);
        button.SetTextSize(global::Android.Util.ComplexUnitType.Sp, MgTypography.BodySp);
        button.SetTextColor(MgColors.Ink);
        button.Background = MgTheme.Outline(Context!, MgColors.Training, cornerRadius: 8);
        button.Click += (_, _) =>
        {
            button.Enabled = false;
            PerformAcknowledgement(presentation.Work.Exercise.InteractionProtocol, RuntimeInputCommandKind.SubmitAnswer);
            var response = $"{pair.Name}={value}";
            pairIndex++;
            materialSignature = string.Empty;
            RenderPairDiscrimination(presentation);
            CommandRequested?.Invoke(RuntimeInputCommandKind.SubmitAnswer, pair.Name, response);
        };
        var layout = new LayoutParams(0, MgSpacing.Dp(Context!, 52), 1);
        if (!first)
        {
            layout.SetMargins(MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0, 0);
        }

        row.AddView(button, layout);
    }

    private static string DiscriminationPairDisplay(string raw)
    {
        var visible = raw.Split("; expected", 2, StringSplitOptions.TrimEntries)[0];
        visible = visible.Contains(':') ? visible[(visible.IndexOf(':') + 1)..].Trim() : visible;
        var parts = visible.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var pair = parts.FirstOrDefault() ?? visible;
        var feature = parts.FirstOrDefault(part =>
            part.StartsWith("relevant feature", StringComparison.OrdinalIgnoreCase));
        pair = pair.Replace(" vs right ", Environment.NewLine + "vs" + Environment.NewLine, StringComparison.OrdinalIgnoreCase)
            .Replace("left ", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (feature is null)
        {
            return pair;
        }

        var featureValue = feature.Replace("relevant feature", string.Empty, StringComparison.OrdinalIgnoreCase).Trim(' ', '\'');
        return $"{pair}{Environment.NewLine}{Environment.NewLine}Compare: {featureValue}";
    }

    private void UpdateEvidence(LiveSessionPresentationReadModel presentation)
    {
        var evidence = presentation.Evidence;
        var effectiveDrill = presentation.SourceDrill ?? presentation.Work.Drill;
        if (effectiveDrill is DrillId.FH1TargetHold or DrillId.FH2DistractorHold)
        {
            SetEvidence(0, evidence.DriftCount.ToString(), "wanders", MgColors.Ink);
            SetEvidence(1, evidence.ReturnCount.ToString(), "returns", MgColors.Ink);
            SetEvidence(2, evidence.LateReturnCount.ToString(), "late", evidence.LateReturnCount > 0 ? MgColors.Blocked : MgColors.Ink);
            SetEvidence(3, evidence.TargetChangeCount.ToString(), "changes", evidence.TargetChangeCount > 0 ? MgColors.Blocked : MgColors.Ink);
            return;
        }

        if (effectiveDrill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter or
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule)
        {
            SetEvidence(0, evidence.CueCount.ToString(), "cues", MgColors.Ink);
            SetEvidence(1, evidence.CueResponseCount.ToString(), "responses", MgColors.Ink);
            SetEvidence(2, evidence.ErrorCount.ToString(), "errors", evidence.ErrorCount > 0 ? MgColors.Blocked : MgColors.Ink);
            SetEvidence(3, evidence.CorrectionCount.ToString(), "fixes", MgColors.Ink);
            return;
        }

        SetEvidence(0, evidence.AnswerCount.ToString(), "submissions", MgColors.Ink);
        SetEvidence(1, evidence.GuessCount.ToString(), "guesses", MgColors.Ink);
        SetEvidence(2, evidence.ErrorCount.ToString(), "errors", evidence.ErrorCount > 0 ? MgColors.Blocked : MgColors.Ink);
        SetEvidence(3, evidence.CorrectionCount.ToString(), "fixes", MgColors.Ink);
    }

    private void UpdateCue(LiveSessionPresentationReadModel presentation)
    {
        if (presentation.ActiveCue is not { } cue)
        {
            cueBand.Visibility = ViewStates.Gone;
            return;
        }

        var respond = cue.RequiresResponse;
        var drill = presentation.SourceDrill ?? presentation.Work.Drill;
        var isDisruption = cue.Kind == RuntimeCueKind.Interruption &&
            cue.RequiresResponse &&
            presentation.Work.Drill == DrillId.AI2DisruptionRecovery;
        var activeStyle = respond;
        cueBand.Visibility = ViewStates.Visible;
        cueBand.Background = MgTheme.TintedSurface(
            Context!,
            activeStyle ? MgColors.TrainingTint : MgColors.MaintenanceTint,
            activeStyle ? MgColors.Training : MgColors.Maintenance,
            cornerRadius: 8);
        cueMode.Text = isDisruption
            ? "DISRUPTION"
            : drill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter
                ? respond ? "SWITCH" : "WITHHOLD"
                : drill is DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule
                    ? respond ? "GO" : "NO-GO"
                    : respond ? "RESPOND" : "IGNORE";
        cueMode.SetTextColor(respond ? MgColors.TrainingDark : MgColors.Ink);
        cueText.Text = isDisruption
            ? "Resume from the last stable step."
            : drill == DrillId.IR2ExceptionRule ? CompactRuleCue(cue.Cue) : cue.Cue;

        var cueId = snapshot?.LiveSession.ActiveCue?.CueId;
        if (snapshot?.LiveSession.ActiveCue?.IsControlledDistractor == true &&
            !string.Equals(cueId, lastFeedbackCueId, StringComparison.Ordinal))
        {
            lastFeedbackCueId = cueId;
            PerformHapticFeedback(FeedbackConstants.ClockTick);
        }
    }

    private void UpdateCueChoices(LiveSessionPresentationReadModel presentation)
    {
        cueChoices.RemoveAllViews();
        var effectiveDrill = presentation.SourceDrill ?? presentation.Work.Drill;
        var capturesCueTap = presentation.ActiveCue is { Kind: not RuntimeCueKind.Interruption } &&
            presentation.AvailableCommands.Any(command =>
                command.Command == RuntimeInputCommandKind.RespondToCue);
        if (effectiveDrill is not (DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter) || !capturesCueTap)
        {
            cueChoices.Visibility = ViewStates.Gone;
            return;
        }

        var requiresResponse = presentation.ActiveCue?.RequiresResponse == true;
        var targets = presentation.CurrentMaterials
            .Where(material => string.Equals(material.Kind, "TargetSet", StringComparison.Ordinal))
            .Select(material => material.Value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (targets.Length < 2)
        {
            cueChoices.Visibility = ViewStates.Gone;
            return;
        }

        cueChoices.Visibility = ViewStates.Visible;
        for (var index = 0; index < targets.Length; index++)
        {
            var response = targets[index];
            var button = new Button(Context)
            {
                Text = SentenceCase(response),
                ContentDescription = requiresResponse
                    ? $"Switch to {response}"
                    : $"Do not switch to {response}; tapping records an invalid switch",
            };
            button.SetAllCaps(false);
            button.SetSingleLine(false);
            button.SetMinHeight(MgSpacing.Dp(Context!, 52));
            button.SetTextSize(global::Android.Util.ComplexUnitType.Sp, MgTypography.BodySp);
            button.SetTextColor(requiresResponse ? Color.White : MgColors.InkMuted);
            button.Background = requiresResponse
                ? MgTheme.ActionGradient(Context!, cornerRadius: 8)
                : MgTheme.Outline(Context!, MgColors.Maintenance, cornerRadius: 8);
            button.Click += (_, _) => SendCueResponse(response);
            var layout = new LayoutParams(0, MgSpacing.Dp(Context!, 60), 1);
            if (index > 0)
            {
                layout.SetMargins(MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0, 0);
            }

            cueChoices.AddView(button, layout);
        }
    }

    private void UpdatePendingCorrection(LiveSessionPresentationReadModel presentation)
    {
        if (presentation.PendingCorrection is not { } correction)
        {
            correctionBand.Visibility = ViewStates.Gone;
            correctionSignature = string.Empty;
            correctionChoices.RemoveAllViews();
            return;
        }

        correctionBand.Visibility = ViewStates.Visible;
        correctionTitle.Text = $"FIX LAST CUE  {Math.Max(1, (int)Math.Ceiling(correction.Remaining.TotalSeconds))}s";
        correctionCue.Text = presentation.SourceDrill == DrillId.IR2ExceptionRule
            ? CompactRuleCue(correction.Cue)
            : correction.Cue;
        correctionResponse.Text = string.Equals(
            correction.SubmittedResponse,
            "omitted",
            StringComparison.OrdinalIgnoreCase)
                ? "No response"
                : $"Your response: {SentenceCase(correction.SubmittedResponse)}";

        var signature = $"{correction.SourceEventSequenceNumber}:" +
            string.Join('|', correction.ResponseOptions);
        if (string.Equals(signature, correctionSignature, StringComparison.Ordinal))
        {
            return;
        }

        correctionSignature = signature;
        correctionChoices.RemoveAllViews();
        for (var optionIndex = 0; optionIndex < correction.ResponseOptions.Count; optionIndex += 2)
        {
            var row = new LinearLayout(Context)
            {
                Orientation = Orientation.Horizontal,
            };
            for (var column = 0; column < 2 && optionIndex + column < correction.ResponseOptions.Count; column++)
            {
                var option = correction.ResponseOptions[optionIndex + column];
                var label = CorrectionOptionLabel(option, presentation.SourceDrill ?? presentation.Work.Drill);
                var button = new Button(Context)
                {
                    Text = label,
                    ContentDescription = $"Correct last cue to {label}",
                    HapticFeedbackEnabled = true,
                };
                button.SetAllCaps(false);
                button.SetSingleLine(false);
                button.SetTextSize(global::Android.Util.ComplexUnitType.Sp, MgTypography.BodySp);
                button.SetTextColor(MgColors.InkDeep);
                button.Background = MgTheme.Outline(Context!, MgColors.Blocked, cornerRadius: 8);
                button.Click += (_, _) =>
                {
                    button.PerformHapticFeedback(FeedbackConstants.VirtualKey);
                    CommandRequested?.Invoke(
                        RuntimeInputCommandKind.Correct,
                        correction.SourceEventSequenceNumber.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        option);
                };
                var layout = new LayoutParams(0, MgSpacing.Dp(Context!, 52), 1);
                if (column > 0)
                {
                    layout.SetMargins(MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0, 0);
                }

                row.AddView(button, layout);
            }

            var rowLayout = MatchWrap();
            if (optionIndex > 0)
            {
                rowLayout.SetMargins(0, MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0);
            }

            correctionChoices.AddView(row, rowLayout);
        }
    }

    private static string CorrectionOptionLabel(string option, DrillId? drill)
    {
        if (string.Equals(option, "withhold", StringComparison.OrdinalIgnoreCase))
        {
            return drill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter
                ? "No switch"
                : "Withhold";
        }

        return SentenceCase(option);
    }

    private void SetEvidence(int index, string value, string label, Color color)
    {
        evidenceValues[index].Text = value;
        evidenceValues[index].SetTextColor(color);
        evidenceLabels[index].Text = label;
    }

    private void SendPrimaryCommand()
    {
        if (primaryCommand.HasValue)
        {
            stopConfirmationArmed = false;
            if (snapshot?.Presentation is { } presentation)
            {
                if (primaryCommand.Value == RuntimeInputCommandKind.SubmitAnswer &&
                    ShouldDeferHeldTargetReport(presentation) &&
                    !heldTargetReportUnlocked)
                {
                    heldTargetReportUnlocked = true;
                    incompleteSubmissionArmed = false;
                    structuredSignature = string.Empty;
                    primaryButton.PerformHapticFeedback(FeedbackConstants.LongPress);
                    UpdateStructuredResponse(presentation);
                    UpdatePrimaryButton(presentation);
                    return;
                }

                if (primaryCommand.Value == RuntimeInputCommandKind.SubmitAnswer &&
                    !SubmissionInputIsComplete(presentation) &&
                    !incompleteSubmissionArmed)
                {
                    incompleteSubmissionArmed = true;
                    primaryButton.PerformHapticFeedback(FeedbackConstants.LongPress);
                    UpdatePrimaryButton(presentation);
                    return;
                }

                PerformAcknowledgement(
                    EffectiveInteractionProtocol(presentation),
                    primaryCommand.Value);
            }

            incompleteSubmissionArmed = false;
            SendCommand(primaryCommand.Value);
        }
    }

    private void HandleResponseChanged()
    {
        incompleteSubmissionArmed = false;
        if (snapshot?.Presentation is { } presentation)
        {
            UpdatePrimaryButton(presentation);
        }
    }

    private void HandleSecondaryCommand(
        RuntimeInputCommandKind command,
        LiveSessionPresentationReadModel presentation)
    {
        if (command == RuntimeInputCommandKind.Abandon &&
            presentation.CurrentPhaseKind != RuntimeSessionPhaseKind.InstructionPrep &&
            !stopConfirmationArmed)
        {
            stopConfirmationArmed = true;
            secondarySignature = string.Empty;
            UpdateSecondaryCommands(presentation);
            return;
        }

        if (command != RuntimeInputCommandKind.Abandon)
        {
            stopConfirmationArmed = false;
            secondarySignature = string.Empty;
        }

        SendCommand(command);
    }

    private void SendCommand(RuntimeInputCommandKind command)
    {
        var live = snapshot?.LiveSession;
        if (live is null)
        {
            return;
        }

        var targetId = command == RuntimeInputCommandKind.RespondToCue
            ? live.ActiveCue?.CueId
            : null;
        var value = command switch
        {
            RuntimeInputCommandKind.RespondToCue => CueResponseValue(live),
            RuntimeInputCommandKind.SubmitAnswer or
            RuntimeInputCommandKind.MarkError or
            RuntimeInputCommandKind.Correct => CurrentResponseValue(),
            RuntimeInputCommandKind.MarkTargetChange => "different target",
            RuntimeInputCommandKind.Abandon => live.CurrentPhaseKind == RuntimeSessionPhaseKind.InstructionPrep
                ? "user cancelled setup before work"
                : "user stopped active session",
            _ => null,
        };

        CommandRequested?.Invoke(command, targetId, value);
    }

    private static string CueResponseValue(PreUiLiveSessionState live)
    {
        if (live.ActiveCue is
            {
                Kind: RuntimeCueKind.Interruption,
                ResponseExpectation: RuntimeCueResponseExpectation.ResponseRequired,
            })
        {
            return "resume";
        }

        return (live.SourceDrill ?? live.Drill) is
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule
                ? "tap"
                : "respond";
    }

    private void SendCueResponse(string response)
    {
        var cueId = snapshot?.LiveSession.ActiveCue?.CueId;
        if (cueId is null)
        {
            return;
        }

        if (snapshot?.Presentation is { } presentation)
        {
            PerformAcknowledgement(
                EffectiveInteractionProtocol(presentation),
                RuntimeInputCommandKind.RespondToCue);
        }

        CommandRequested?.Invoke(RuntimeInputCommandKind.RespondToCue, cueId, response);
    }

    private void PerformAcknowledgement(
        DrillInteractionProtocol protocol,
        RuntimeInputCommandKind command)
    {
        if (protocol.Acknowledgement != DrillInteractionAcknowledgement.VisualAndHaptic ||
            command is RuntimeInputCommandKind.FinishPhase or
                RuntimeInputCommandKind.Correct or
                RuntimeInputCommandKind.Abandon)
        {
            return;
        }

        var feedback = command == RuntimeInputCommandKind.MarkDrift
            ? FeedbackConstants.ClockTick
            : FeedbackConstants.VirtualKey;
        var feedbackView = sourceActionButton.Visibility == ViewStates.Visible
            ? sourceActionButton
            : primaryButton;
        feedbackView.PerformHapticFeedback(feedback);
    }

    private static DrillInteractionProtocol EffectiveInteractionProtocol(
        LiveSessionPresentationReadModel presentation)
    {
        var drill = presentation.SourceDrill ?? presentation.Work.Drill ??
            throw new InvalidOperationException("Live work must identify its effective drill.");
        return DrillInteractionProtocolCatalog.Get(drill);
    }

    private bool UpdateStructuredResponse(LiveSessionPresentationReadModel presentation)
    {
        var canSubmit = presentation.AvailableCommands.Any(command =>
            command.Command == RuntimeInputCommandKind.SubmitAnswer);
        var effectiveDrill = presentation.SourceDrill ?? presentation.Work.Drill;
        if (effectiveDrill is DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork &&
            canSubmit &&
            presentation.CurrentMaterials.Any(material => material.Kind == "RuleStatement"))
        {
            EnsureRuleDeclarationForm(presentation, effectiveDrill.Value);
            return true;
        }

        if (presentation.Work.Drill == DrillId.DE2SeededAudit &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.Audit &&
            canSubmit)
        {
            EnsureSeededAuditForm(presentation);
            return true;
        }

        if (presentation.Work.Drill == DrillId.TI2GlobalReviewTask &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.Audit &&
            canSubmit &&
            presentation.CurrentMaterials.Any(material => material.Kind == "AuditPayload"))
        {
            EnsureGlobalReviewAuditForm(presentation);
            return true;
        }

        if (presentation.Work.Drill == DrillId.CO2StructureMapping &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.Audit &&
            canSubmit &&
            presentation.CurrentMaterials.Any(material => material.Kind == "AuditPayload"))
        {
            EnsureModelAuditForm(presentation);
            return true;
        }

        if (presentation.Work.Drill == DrillId.WM2MentalTransform &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ReconstructionInput &&
            canSubmit)
        {
            var recallRequirements = presentation.CurrentMaterials
                .Where(material => material.Kind == "ComponentEvidenceRequirement")
                .ToArray();
            EnsureMentalTransformForm(presentation, recallRequirements);
            return true;
        }

        var components = presentation.CurrentMaterials
            .Where(material => string.Equals(material.Kind, "ComponentPayload", StringComparison.Ordinal))
            .ToArray();
        if (components.Length > 0 &&
            presentation.AvailableCommands.Any(command => command.Command == RuntimeInputCommandKind.SubmitAnswer))
        {
            EnsureComponentResponseForm(presentation, components);
            return true;
        }

        var unseen = presentation.CurrentMaterials
            .Where(material => string.Equals(material.Kind, "UnseenExample", StringComparison.Ordinal))
            .ToArray();
        if (presentation.Work.Drill == DrillId.CO1RuleExtraction && unseen.Length > 0)
        {
            EnsureClassificationForm(presentation, unseen);
            return true;
        }

        var relations = presentation.CurrentMaterials
            .Where(material => string.Equals(material.Kind, "RequiredRelation", StringComparison.Ordinal))
            .ToArray();
        if (presentation.Work.Drill == DrillId.CO2StructureMapping && relations.Length > 0)
        {
            EnsureRelationForm(presentation, relations);
            return true;
        }

        var componentEvidence = presentation.CurrentMaterials
            .Where(material => string.Equals(
                material.Kind,
                "ComponentEvidenceRequirement",
                StringComparison.Ordinal))
            .ToArray();
        if (presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ReconstructionInput &&
            componentEvidence.Length > 0)
        {
            EnsureComponentRecallForm(presentation, componentEvidence);
            return true;
        }

        structuredResponse.Visibility = ViewStates.Gone;
        return false;
    }

    private void EnsureComponentResponseForm(
        LiveSessionPresentationReadModel presentation,
        IReadOnlyList<PreUiLiveSessionMaterialState> components)
    {
        var orderedComponents = components
            .OrderBy(component => string.Equals(
                ComponentBranch(component),
                "FH",
                StringComparison.Ordinal) ? 1 : 0)
            .ToArray();
        var componentKeys = orderedComponents.Select(ComponentBranch).ToArray();
        var signature = $"components:{presentation.CurrentPhaseKind}:{heldTargetReportUnlocked}:" +
            string.Join('|', componentKeys);
        if (string.Equals(signature, structuredSignature, StringComparison.Ordinal))
        {
            structuredResponse.Visibility = ViewStates.Visible;
            return;
        }

        BeginStructuredForm(signature, "RESPONSES BY BRANCH");
        foreach (var component in orderedComponents)
        {
            var branch = ComponentBranch(component);
            if (branch.Length == 0)
            {
                continue;
            }

            if (string.Equals(branch, "FH", StringComparison.Ordinal) &&
                !heldTargetReportUnlocked)
            {
                continue;
            }

            var summary = GlobalReviewComponentSummary(component.Value);
            AddStructuredTextField(
                presentation,
                branch,
                $"{branch}  {summary.Role}",
                string.Equals(branch, "FH", StringComparison.Ordinal)
                    ? "Held target from setup"
                    : "Exact response from this component",
                enabled: !heldTargetReportUnlocked ||
                    string.Equals(branch, "FH", StringComparison.Ordinal));
        }
    }

    private void EnsureRuleDeclarationForm(
        LiveSessionPresentationReadModel presentation,
        DrillId drill)
    {
        var exceptions = presentation.CurrentMaterials
            .Where(material => material.Kind == "ExceptionDefinition")
            .ToArray();
        var signature = $"rule-declaration:{drill}:" +
            string.Join('|', exceptions.Select(item => item.Name));
        if (string.Equals(signature, structuredSignature, StringComparison.Ordinal))
        {
            structuredResponse.Visibility = ViewStates.Visible;
            return;
        }

        BeginStructuredForm(signature, "LOCK THE RULE", preserveFields: false);
        AddStructuredTextField(
            presentation,
            "RULE",
            "BASE RULE",
            drill == DrillId.IR1GoNoGoRule
                ? "Respond to GO; withhold on NO-GO"
                : "Tap round; withhold angular");
        for (var index = 0; index < exceptions.Length; index++)
        {
            AddStructuredTextField(
                presentation,
                $"EXCEPTION-{index + 1}",
                $"EXCEPTION {index + 1}",
                "Symbol and override action");
        }
    }

    private void EnsureGlobalReviewAuditForm(LiveSessionPresentationReadModel presentation)
    {
        const string signature = "global-review-audit";
        if (string.Equals(signature, structuredSignature, StringComparison.Ordinal))
        {
            structuredResponse.Visibility = ViewStates.Visible;
            return;
        }

        BeginStructuredForm(signature, "FIND THE MISMATCH", preserveFields: false);
        AddStructuredTextField(presentation, "BRANCH", "WRONG BRANCH", "Two-letter branch code");
        AddStructuredTextField(presentation, "CORRECTION", "CORRECT RESPONSE", "Exact response for that branch");
    }

    private void EnsureSeededAuditForm(LiveSessionPresentationReadModel presentation)
    {
        var instruction = presentation.CurrentMaterials.FirstOrDefault(material =>
            material.Kind == "AuditInstruction")?.Value ?? string.Empty;
        var count = AuditFindingCount(instruction);
        var signature = $"seeded-audit:{count}";
        if (string.Equals(signature, structuredSignature, StringComparison.Ordinal))
        {
            structuredResponse.Visibility = ViewStates.Visible;
            return;
        }

        BeginStructuredForm(signature, "SUPPORTED MISMATCHES", preserveFields: false);
        for (var index = 1; index <= count; index++)
        {
            AddStructuredTextField(
                presentation,
                $"FINDING-{index}",
                $"FINDING {index}",
                "Line # · mismatch type · exact correction");
        }
    }

    private static int AuditFindingCount(string instruction)
    {
        const string marker = "report ";
        var start = instruction.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return 1;
        }

        start += marker.Length;
        var digits = new string(instruction[start..].TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(
            digits,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var count)
                ? Math.Max(1, count)
                : 1;
    }

    private void EnsureModelAuditForm(LiveSessionPresentationReadModel presentation)
    {
        var payload = presentation.CurrentMaterials.First(material => material.Kind == "AuditPayload").Value;
        var includesLimit = payload.Contains("critical assumption", StringComparison.OrdinalIgnoreCase);
        var signature = $"model-audit:{includesLimit}";
        if (string.Equals(signature, structuredSignature, StringComparison.Ordinal))
        {
            structuredResponse.Visibility = ViewStates.Visible;
            return;
        }

        BeginStructuredForm(signature, "TEST THE UNSEEN PROBE");
        if (includesLimit)
        {
            AddStructuredTextField(
                presentation,
                "ASSUMPTION",
                "CRITICAL ASSUMPTION",
                "A condition the mapping depends on");
        }

        AddAuditChoiceRow(
            presentation,
            "PREDICTION",
            "PROBE VERDICT",
            ("Preserved", "PASSED"),
            ("Broken", "FAILED"));
        if (includesLimit)
        {
            AddAuditChoiceRow(
                presentation,
                "LIMIT",
                "MAPPING LIMIT",
                ("Supported", "SUPPORTED"),
                ("Exceeded", "EXCEEDED"));
        }

        AddStructuredTextField(
            presentation,
            "TEST",
            "DECIDING EVIDENCE",
            "The concrete relation that decides the verdict");
    }

    private void EnsureComponentRecallForm(
        LiveSessionPresentationReadModel presentation,
        IReadOnlyList<PreUiLiveSessionMaterialState> requirements)
    {
        var branches = requirements
            .Select(EvidenceBranch)
            .Where(branch => branch.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var signature = "component-recall:" + string.Join('|', branches);
        if (string.Equals(signature, structuredSignature, StringComparison.Ordinal))
        {
            structuredResponse.Visibility = ViewStates.Visible;
            return;
        }

        BeginStructuredForm(signature, "RECONSTRUCT THE LOCKED REPORT", preserveFields: false);
        foreach (var branch in branches)
        {
            AddStructuredTextField(
                presentation,
                branch,
                branch,
                presentation.Work.Drill == DrillId.TI2GlobalReviewTask
                    ? "Exact response from the locked report"
                    : "Critical result or evidence remembered for this branch");
        }
    }

    private void EnsureMentalTransformForm(
        LiveSessionPresentationReadModel presentation,
        IReadOnlyList<PreUiLiveSessionMaterialState> requirements)
    {
        var branches = requirements
            .Select(EvidenceBranch)
            .Where(branch => branch.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var signature = "mental-transform:" + string.Join('|', branches);
        if (string.Equals(signature, structuredSignature, StringComparison.Ordinal))
        {
            structuredResponse.Visibility = ViewStates.Visible;
            return;
        }

        BeginStructuredForm(signature, "FINAL TRANSFORM", preserveFields: false);
        AddStructuredTextField(
            presentation,
            "RESULT",
            "FINAL ORDER",
            "Exact items in order, separated by | characters");
        AddStructuredTextField(
            presentation,
            "RULE",
            "OPERATIONS USED",
            "Describe the operation sequence you applied mentally");
        foreach (var branch in branches)
        {
            AddStructuredTextField(
                presentation,
                branch,
                branch,
                "Exact response remembered for this branch");
        }
    }

    private void EnsureClassificationForm(
        LiveSessionPresentationReadModel presentation,
        IReadOnlyList<PreUiLiveSessionMaterialState> unseen)
    {
        var signature = "classifications:" + string.Join('|', unseen.Select(item => item.Name));
        if (string.Equals(signature, structuredSignature, StringComparison.Ordinal))
        {
            structuredResponse.Visibility = ViewStates.Visible;
            return;
        }

        BeginStructuredForm(signature, "APPLY THE LOCKED RULE");
        foreach (var example in unseen)
        {
            var key = TrailingIndex(example.Name);
            var block = new LinearLayout(Context)
            {
                Orientation = Orientation.Vertical,
                Background = MgTheme.MutedSurface(Context!, cornerRadius: 8),
            };
            block.SetPadding(
                MgSpacing.Dp(Context!, MgSpacing.Sm),
                MgSpacing.Dp(Context!, MgSpacing.Sm),
                MgSpacing.Dp(Context!, MgSpacing.Sm),
                MgSpacing.Dp(Context!, MgSpacing.Sm));
            var text = new TextView(Context)
            {
                Text = StripMaterialPrefix(example.Value),
            };
            MgTypography.ApplyBody(text);
            block.AddView(text, MatchWrap());

            var row = new LinearLayout(Context)
            {
                Orientation = Orientation.Horizontal,
            };
            AddStructuredChoice(presentation, row, key, "Positive", "positive", first: true);
            AddStructuredChoice(presentation, row, key, "Negative", "negative", first: false);
            block.AddView(row, MatchWrapWithTop(MgSpacing.Sm));
            structuredResponse.AddView(block, MatchWrapWithTop(MgSpacing.Sm));
        }
    }

    private void EnsureRelationForm(
        LiveSessionPresentationReadModel presentation,
        IReadOnlyList<PreUiLiveSessionMaterialState> relations)
    {
        var signature = "relations:" + string.Join('|', relations.Select(item => item.Name));
        if (string.Equals(signature, structuredSignature, StringComparison.Ordinal))
        {
            structuredResponse.Visibility = ViewStates.Visible;
            return;
        }

        BeginStructuredForm(signature, "PRESERVED RELATIONS");
        foreach (var relation in relations)
        {
            var relationIndex = TrailingIndex(relation.Name);
            AddStructuredTextField(
                presentation,
                relationIndex,
                $"RELATION {relationIndex}",
                "Relation name; source -> target; preserving evidence");
        }
    }

    private void BeginStructuredForm(
        string signature,
        string heading,
        bool preserveFields = true)
    {
        var preservedValues = preserveFields
            ? structuredFields.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Text ?? string.Empty,
                StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        structuredSignature = signature;
        structuredResponse.RemoveAllViews();
        structuredFields.Clear();
        if (!preserveFields)
        {
            structuredChoices.Clear();
        }
        structuredResponse.Visibility = ViewStates.Visible;
        var title = new TextView(Context)
        {
            Text = heading,
        };
        MgTypography.ApplyLabel(title);
        title.SetTextColor(MgColors.InkMuted);
        structuredResponse.AddView(title, MatchWrap());
        foreach (var pair in preservedValues)
        {
            structuredChoices.TryAdd($"preserved:{pair.Key}", pair.Value);
        }
    }

    private void AddStructuredTextField(
        LiveSessionPresentationReadModel presentation,
        string key,
        string label,
        string hint,
        bool enabled = true)
    {
        var labelView = new TextView(Context)
        {
            Text = label,
        };
        MgTypography.ApplyLabel(labelView);
        labelView.SetTextColor(MgColors.TrainingDark);
        structuredResponse.AddView(labelView, MatchWrapWithTop(MgSpacing.Sm));

        var field = new EditText(Context)
        {
            Hint = hint,
            Text = structuredChoices.Remove($"preserved:{key}", out var preserved)
                ? preserved
                : string.Empty,
        };
        field.SetSingleLine(false);
        field.Enabled = enabled;
        field.SetMinLines(2);
        field.SetMaxLines(4);
        field.SetTextSize(global::Android.Util.ComplexUnitType.Sp, MgTypography.BodySp);
        field.Background = MgTheme.Outline(Context!, MgColors.Hairline, cornerRadius: 8);
        field.SetPadding(
            MgSpacing.Dp(Context!, MgSpacing.Md),
            MgSpacing.Dp(Context!, MgSpacing.Sm),
            MgSpacing.Dp(Context!, MgSpacing.Md),
            MgSpacing.Dp(Context!, MgSpacing.Sm));
        field.TextChanged += (_, _) =>
        {
            incompleteSubmissionArmed = false;
            UpdatePrimaryButton(presentation);
        };
        structuredFields[key] = field;
        structuredResponse.AddView(field, MatchWrapWithTop(MgSpacing.Xs));
    }

    private void AddStructuredChoice(
        LiveSessionPresentationReadModel presentation,
        LinearLayout row,
        string key,
        string label,
        string value,
        bool first)
    {
        var selected = structuredChoices.TryGetValue(key, out var current) && current == value;
        var button = new Button(Context)
        {
            Text = label,
            ContentDescription = $"{label}, unseen example {key}",
        };
        button.SetAllCaps(false);
        button.SetTextColor(selected ? Color.White : MgColors.Ink);
        button.Background = selected
            ? MgTheme.ActionGradient(Context!, cornerRadius: 8)
            : MgTheme.Outline(Context!, MgColors.Training, cornerRadius: 8);
        button.Click += (_, _) =>
        {
            incompleteSubmissionArmed = false;
            structuredChoices[key] = value;
            structuredSignature = string.Empty;
            EnsureClassificationForm(
                presentation,
                presentation.CurrentMaterials
                    .Where(material => material.Kind == "UnseenExample")
                    .ToArray());
            UpdatePrimaryButton(presentation);
        };
        var layout = new LayoutParams(0, MgSpacing.Dp(Context!, 52), 1);
        if (!first)
        {
            layout.SetMargins(MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0, 0);
        }

        row.AddView(button, layout);
    }

    private void AddAuditChoiceRow(
        LiveSessionPresentationReadModel presentation,
        string key,
        string heading,
        (string Label, string Value) firstChoice,
        (string Label, string Value) secondChoice)
    {
        var label = new TextView(Context)
        {
            Text = heading,
        };
        MgTypography.ApplyLabel(label);
        label.SetTextColor(MgColors.TrainingDark);
        structuredResponse.AddView(label, MatchWrapWithTop(MgSpacing.Sm));

        var row = new LinearLayout(Context)
        {
            Orientation = Orientation.Horizontal,
        };
        AddAuditChoice(presentation, row, key, firstChoice.Label, firstChoice.Value, first: true);
        AddAuditChoice(presentation, row, key, secondChoice.Label, secondChoice.Value, first: false);
        structuredResponse.AddView(row, MatchWrapWithTop(MgSpacing.Xs));
    }

    private void AddAuditChoice(
        LiveSessionPresentationReadModel presentation,
        LinearLayout row,
        string key,
        string label,
        string value,
        bool first)
    {
        var selected = structuredChoices.TryGetValue(key, out var current) && current == value;
        var button = new Button(Context)
        {
            Text = label,
            ContentDescription = $"{label}, {key.ToLowerInvariant()}",
        };
        button.SetAllCaps(false);
        button.SetTextColor(selected ? Color.White : MgColors.Ink);
        button.Background = selected
            ? MgTheme.ActionGradient(Context!, cornerRadius: 8)
            : MgTheme.Outline(Context!, MgColors.Training, cornerRadius: 8);
        button.Click += (_, _) =>
        {
            incompleteSubmissionArmed = false;
            structuredChoices[key] = value;
            structuredSignature = string.Empty;
            EnsureModelAuditForm(presentation);
            UpdatePrimaryButton(presentation);
        };
        var layout = new LayoutParams(0, MgSpacing.Dp(Context!, 52), 1);
        if (!first)
        {
            layout.SetMargins(MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0, 0);
        }

        row.AddView(button, layout);
    }

    private bool PrimaryInputIsReady(LiveSessionPresentationReadModel presentation)
    {
        if (presentation.PrimaryCommand?.Command == RuntimeInputCommandKind.SubmitAnswer)
        {
            return true;
        }

        if (presentation.PrimaryCommand?.Command != RuntimeInputCommandKind.Correct)
        {
            return true;
        }

        return SubmissionInputIsComplete(presentation);
    }

    private static bool ShouldDeferHeldTargetReport(LiveSessionPresentationReadModel presentation)
    {
        return presentation.PrimaryCommand?.Command == RuntimeInputCommandKind.SubmitAnswer &&
            presentation.CurrentMaterials.Any(material =>
                string.Equals(material.Kind, "ComponentPayload", StringComparison.Ordinal) &&
                string.Equals(ComponentBranch(material), "FH", StringComparison.Ordinal));
    }

    private bool SubmissionInputIsComplete(LiveSessionPresentationReadModel presentation)
    {
        if (structuredResponse.Visibility == ViewStates.Visible)
        {
            return structuredFields.Values.All(field => !string.IsNullOrWhiteSpace(field.Text)) &&
                RequiredStructuredChoiceKeys(presentation).All(structuredChoices.ContainsKey);
        }

        return !string.IsNullOrWhiteSpace(responseInput.Text);
    }

    private static IReadOnlyList<string> RequiredStructuredChoiceKeys(
        LiveSessionPresentationReadModel presentation)
    {
        var unseen = presentation.CurrentMaterials
            .Where(material => material.Kind == "UnseenExample")
            .Select(material => TrailingIndex(material.Name))
            .ToArray();
        if (unseen.Length > 0)
        {
            return unseen;
        }

        var auditPayload = presentation.CurrentMaterials.FirstOrDefault(material =>
            material.Kind == "AuditPayload")?.Value;
        if (presentation.Work.Drill == DrillId.CO2StructureMapping &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.Audit &&
            auditPayload is not null)
        {
            return auditPayload.Contains("critical assumption", StringComparison.OrdinalIgnoreCase)
                ? ["PREDICTION", "LIMIT"]
                : ["PREDICTION"];
        }

        return [];
    }

    private string? CurrentResponseValue()
    {
        if (structuredResponse.Visibility != ViewStates.Visible)
        {
            return string.IsNullOrWhiteSpace(responseInput.Text)
                ? RuntimeResponseMarkers.Omitted
                : responseInput.Text;
        }

        var parts = structuredFields
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.Text))
            .Select(pair => $"{pair.Key}={pair.Value.Text!.Trim()}")
            .Concat(structuredChoices
                .Where(pair => !pair.Key.StartsWith("preserved:", StringComparison.Ordinal))
                .Select(pair => $"{pair.Key}={pair.Value}"));
        var response = string.Join("; ", parts);
        return response.Length == 0 ? RuntimeResponseMarkers.Omitted : response;
    }

    private static string ComponentBranch(PreUiLiveSessionMaterialState component)
    {
        return (SegmentValue(component.Value, "component branch ") ?? string.Empty)
            .Split(':', StringSplitOptions.TrimEntries)[0]
            .Trim()
            .ToUpperInvariant();
    }

    private static string EvidenceBranch(PreUiLiveSessionMaterialState requirement)
    {
        var value = requirement.Value.Trim();
        if (!value.StartsWith("branch ", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return value["branch ".Length..]
            .Split(' ', 2, StringSplitOptions.TrimEntries)[0]
            .TrimEnd(':')
            .ToUpperInvariant();
    }

    private static string TrailingIndex(string value)
    {
        var digits = new string(value.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return digits.Length == 0 ? value : digits;
    }

    private static string StripMaterialPrefix(string value)
    {
        var colon = value.IndexOf(':');
        var compact = colon >= 0 && colon < value.Length - 1 ? value[(colon + 1)..] : value;
        var instruction = compact.IndexOf("; classify", StringComparison.OrdinalIgnoreCase);
        return (instruction >= 0 ? compact[..instruction] : compact).Trim();
    }

    private static string? QuotedValue(string value)
    {
        var start = value.IndexOf('\'');
        var end = start < 0 ? -1 : value.IndexOf('\'', start + 1);
        return start >= 0 && end > start ? value[(start + 1)..end] : null;
    }

    private static bool RequiresTextInput(
        RuntimeInputCommandKind command,
        DrillId drill)
    {
        return command == RuntimeInputCommandKind.SubmitAnswer ||
            command == RuntimeInputCommandKind.Correct && drill is not
                (DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter or
                    DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule);
    }

    private static int SecondaryRank(LiveCommandPresentationSummary command)
    {
        return command.Command switch
        {
            RuntimeInputCommandKind.MarkTargetChange or
            RuntimeInputCommandKind.MarkGuess or
            RuntimeInputCommandKind.MarkError or
            RuntimeInputCommandKind.Correct or
            RuntimeInputCommandKind.FinishPhase => 0,
            RuntimeInputCommandKind.Pause or RuntimeInputCommandKind.Resume => 8,
            RuntimeInputCommandKind.Abandon => 9,
            _ => 3,
        };
    }

    private LiveCommandPresentationSummary[] SelectSecondaryCommands(
        LiveSessionPresentationReadModel presentation)
    {
        var candidates = presentation.AvailableCommands
            .Where(command => command.Command != primaryCommand)
            .Where(command => presentation.PendingCorrection is null ||
                command.Command != RuntimeInputCommandKind.Correct)
            .Where(command => !(presentation.Work.Drill == DrillId.DE1PairDiscrimination &&
                presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork &&
                command.Command == RuntimeInputCommandKind.MarkGuess))
            .Where(command => command.Command != RuntimeInputCommandKind.MarkError ||
                presentation.Evidence.AnswerCount > 0 ||
                presentation.Evidence.CueResponseCount > 0)
            .ToArray();
        var effectiveDrill = presentation.SourceDrill ?? presentation.Work.Drill;
        var focusRecoveryCommand = presentation.ActiveCue is
            { Kind: RuntimeCueKind.Interruption, RequiresResponse: true } &&
            (effectiveDrill is DrillId.FH1TargetHold or DrillId.FH2DistractorHold)
                ? candidates.FirstOrDefault(command => command.Command == RuntimeInputCommandKind.MarkReturn) ??
                    candidates.FirstOrDefault(command => command.Command == RuntimeInputCommandKind.MarkDrift)
                : null;
        var semantic = focusRecoveryCommand ?? candidates
            .Where(command => SecondaryRank(command) < 8)
            .OrderBy(SecondaryRank)
            .FirstOrDefault();
        var lifecycle = candidates.FirstOrDefault(command => command.Command is
            RuntimeInputCommandKind.Pause or RuntimeInputCommandKind.Resume);
        var stop = candidates.FirstOrDefault(command => command.Command == RuntimeInputCommandKind.Abandon);

        return new[] { semantic, lifecycle, stop }
            .Where(command => command is not null)
            .Cast<LiveCommandPresentationSummary>()
            .DistinctBy(command => command.Command)
            .ToArray();
    }

    private static string DisplayLabel(
        LiveCommandPresentationSummary command,
        LiveSessionPresentationReadModel presentation,
        bool primary)
    {
        var phase = presentation.CurrentPhaseKind;
        var drill = presentation.Work.Drill ??
            throw new InvalidOperationException("Live work must identify its drill.");
        var effectiveDrill = presentation.SourceDrill ?? drill;
        return command.Command switch
        {
            RuntimeInputCommandKind.FinishPhase => FinishPhaseLabel(drill, effectiveDrill, phase, primary),
            RuntimeInputCommandKind.RespondToCue when presentation.ActiveCue is
                { Kind: RuntimeCueKind.Interruption, RequiresResponse: true } =>
                "RESUMED\nTap after continuing",
            RuntimeInputCommandKind.RespondToCue when (presentation.SourceDrill ?? drill) is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter => "Switch",
            RuntimeInputCommandKind.RespondToCue when
                ((presentation.SourceDrill ?? drill) is DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule) &&
                presentation.ActiveCue?.RequiresResponse == false => "WITHHOLD\nDo not tap",
            RuntimeInputCommandKind.RespondToCue when (presentation.SourceDrill ?? drill) is DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => "TAP\nGO",
            RuntimeInputCommandKind.RespondToCue => "Respond",
            RuntimeInputCommandKind.MarkDrift => "WANDERED\nTap this pad",
            RuntimeInputCommandKind.MarkReturn => "BACK ON TARGET\nTap after returning",
            RuntimeInputCommandKind.SubmitAnswer => SubmitLabel(drill, phase),
            RuntimeInputCommandKind.MarkGuess when drill is
                DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery => "Mark uncertain",
            RuntimeInputCommandKind.MarkGuess when drill == DrillId.DE2SeededAudit => "Mark uncertain",
            RuntimeInputCommandKind.MarkGuess => "I guessed",
            RuntimeInputCommandKind.MarkError => "Report error",
            RuntimeInputCommandKind.Correct => "Correct last response",
            RuntimeInputCommandKind.Abandon when phase == RuntimeSessionPhaseKind.InstructionPrep => "Cancel setup",
            RuntimeInputCommandKind.Abandon => "Stop",
            _ => command.Label,
        };
    }

    private static string StartLabel(DrillId drill)
    {
        return drill switch
        {
            DrillId.FH1TargetHold or DrillId.FH2DistractorHold => "Start holding",
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter => "Start cues",
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => "State rule",
            DrillId.WM1DelayedReconstruction => "Show items",
            DrillId.WM2MentalTransform => "Show source",
            DrillId.DE1PairDiscrimination => "Start pairs",
            DrillId.DE2SeededAudit => "Study source",
            DrillId.CO1RuleExtraction => "Infer rule",
            DrillId.CO2StructureMapping => "Name relations",
            DrillId.AI1PressureRepeat => "Start pressure task",
            DrillId.AI2DisruptionRecovery => "Start source task",
            DrillId.TI1CompositeTask => "Start composite",
            DrillId.TI2GlobalReviewTask => "Start review",
            _ => "Start task",
        };
    }

    private static string FinishPhaseLabel(
        DrillId drill,
        DrillId effectiveDrill,
        RuntimeSessionPhaseKind? phase,
        bool primary)
    {
        if (phase == RuntimeSessionPhaseKind.InstructionPrep)
        {
            return StartLabel(effectiveDrill);
        }

        if (phase == RuntimeSessionPhaseKind.Review)
        {
            return primary ? "Finish session" : "Finish";
        }

        if (phase == RuntimeSessionPhaseKind.EncodeWindow)
        {
            return drill switch
            {
                DrillId.WM1DelayedReconstruction => "Hide items",
                DrillId.WM2MentalTransform or DrillId.DE2SeededAudit => "Hide source",
                _ => "Hide material",
            };
        }

        if (phase == RuntimeSessionPhaseKind.ActiveWork)
        {
            if (effectiveDrill is DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule)
            {
                return "Start cues";
            }

            return drill switch
            {
                DrillId.CO1RuleExtraction => "Show test cases",
                DrillId.CO2StructureMapping => "Start mapping",
                DrillId.TI1CompositeTask => "Lock components",
                DrillId.TI2GlobalReviewTask => "Start audit",
                DrillId.DE1PairDiscrimination => "Review choices",
                _ => "Continue task",
            };
        }

        if (phase == RuntimeSessionPhaseKind.CueResponse)
        {
            return drill is DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery
                ? "Report source result"
                : "Review result";
        }

        if (phase == RuntimeSessionPhaseKind.Audit && drill == DrillId.TI2GlobalReviewTask)
        {
            return "Start hidden delay";
        }

        if (phase == RuntimeSessionPhaseKind.ReconstructionInput && drill == DrillId.CO2StructureMapping)
        {
            return "Lock mapping";
        }

        return phase is RuntimeSessionPhaseKind.Audit or RuntimeSessionPhaseKind.ReconstructionInput
            ? "Review result"
            : "Continue task";
    }

    private static string SubmitLabel(DrillId drill, RuntimeSessionPhaseKind? phase)
    {
        if (drill == DrillId.CO1RuleExtraction)
        {
            return phase == RuntimeSessionPhaseKind.ReconstructionInput
                ? "Submit classifications"
                : "Submit rule";
        }

        if (drill == DrillId.CO2StructureMapping)
        {
            return phase switch
            {
                RuntimeSessionPhaseKind.ActiveWork => "Lock relations",
                RuntimeSessionPhaseKind.ReconstructionInput => "Submit mapping",
                RuntimeSessionPhaseKind.Audit => "Submit test",
                _ => "Submit response",
            };
        }

        if (drill == DrillId.TI2GlobalReviewTask)
        {
            return phase switch
            {
                RuntimeSessionPhaseKind.ActiveWork => "Submit components",
                RuntimeSessionPhaseKind.Audit => "Submit mismatch",
                RuntimeSessionPhaseKind.ReconstructionInput => "Submit reconstruction",
                _ => "Submit response",
            };
        }

        return drill switch
        {
            DrillId.WM1DelayedReconstruction => "Submit reconstruction",
            DrillId.WM2MentalTransform => "Submit result",
            DrillId.DE1PairDiscrimination => "Submit comparison",
            DrillId.DE2SeededAudit => "Submit findings",
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => "Lock rule",
            DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery => "Submit attempt",
            DrillId.TI1CompositeTask => "Submit components",
            _ => "Submit answer",
        };
    }

    private static string ResponseHint(
        DrillId drill,
        RuntimeSessionPhaseKind? phase,
        IReadOnlyList<PreUiLiveSessionMaterialState> materials)
    {
        var hasComponents = materials.Any(material =>
            string.Equals(material.Kind, "ComponentPayload", StringComparison.Ordinal));
        if (phase == RuntimeSessionPhaseKind.ReconstructionInput)
        {
            if (hasComponents)
            {
                return drill == DrillId.WM2MentalTransform
                    ? "Final result; rule; BRANCH=answer"
                    : "BRANCH=answer; BRANCH=answer";
            }

            return drill switch
            {
                DrillId.WM2MentalTransform => "Final result and rule",
                DrillId.WM1DelayedReconstruction => "One item per line, in original order",
                _ => "Reconstruction",
            };
        }

        if (phase == RuntimeSessionPhaseKind.Audit && drill == DrillId.CO2StructureMapping)
        {
            return "ASSUMPTION=...; LIMIT=SUPPORTED; PREDICTION=PASSED";
        }

        if (phase == RuntimeSessionPhaseKind.Audit && drill == DrillId.TI2GlobalReviewTask)
        {
            return "Wrong branch and exact correction";
        }

        return drill switch
        {
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => "State the rule and every exception",
            DrillId.DE1PairDiscrimination => "Same or different, with reason",
            DrillId.DE2SeededAudit => "One per line: line #, mismatch type, exact correction",
            DrillId.CO1RuleExtraction when phase == RuntimeSessionPhaseKind.ActiveWork => "State one testable rule",
            DrillId.CO1RuleExtraction => "Classify each unseen example",
            DrillId.CO2StructureMapping when phase == RuntimeSessionPhaseKind.ActiveWork => "Name the source relations",
            DrillId.CO2StructureMapping => "Relation mapping",
            DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery => "Task result",
            DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask => "FH=answer; FS=answer; ...",
            _ => "Response",
        };
    }

    private static string MaterialLabel(PreUiLiveSessionMaterialState material)
    {
        var kind = material.Kind switch
        {
            "TargetSet" => "Targets",
            "HonestyConstraint" => "Rules",
            "EncodeInstruction" => "Encode",
            "EncodeItem" => "Items",
            "SourceItem" => "Source items",
            "TransformRule" => "Transform",
            "OperationStep" => "Operation",
            "ReconstructionInstruction" => "Reconstruct",
            "RuleStatement" => "Rule",
            "ExceptionDefinition" => "Exception",
            "DiscriminationPair" => "Pair",
            "RelevantFeature" => "Compare",
            "PositiveExample" => "Examples",
            "NegativeExample" => "Counterexamples",
            "UnseenExample" => "Classify",
            "SourceStructure" => "Source structure",
            "TargetStructure" => "Target structure",
            "RequiredRelation" => "Required relation",
            "AuditReference" => "Source record",
            "LockedOriginalOutput" => "Locked report",
            "AuditInstruction" => "Audit",
            "AuditPayload" => "Audit input",
            "DelayedReconstructionPayload" => "Delayed reconstruction",
            "SourceBranchStandard" => "Standard",
            "TransferTask" => "Task",
            "SameDemand" => "Keep",
            "ChangedContext" => "Change",
            "PressureSource" => "Pressure",
            "SourceTask" => "Task",
            "DisruptionEvent" => "Disruption",
            "RestartRule" => "Restart rule",
            "CompositeTaskPrompt" => "Task",
            "ComponentPayload" => "Component",
            "ComponentEvidenceRequirement" => "Evidence required",
            "BranchScoringKey" => "Passing standard",
            "ExpectedReconstruction" or "FinalExpectedOutput" or
            "ExpectedFinding" or "ExpectedClassification" or "ExpectedRule" or "ExpectedMapping" => "Answer key",
            _ => Humanize(material.Name),
        };

        return kind.ToUpperInvariant();
    }

    private static string MaterialGroupingKey(PreUiLiveSessionMaterialState material)
    {
        return material.Kind is "TargetSet" or "HonestyConstraint" or "EncodeItem" or "SourceItem" or
            "OperationStep" or
            "PositiveExample" or "NegativeExample" or "UnseenExample" or
            "ComponentPayload" or "ComponentEvidenceRequirement" or "BranchScoringKey"
            ? material.Kind
            : $"{material.Kind}:{material.Name}";
    }

    private static string MaterialGroupValue(
        IEnumerable<PreUiLiveSessionMaterialState> materials)
    {
        var materialArray = materials.ToArray();
        var kind = materialArray[0].Kind;
        var items = materialArray
            .Select(material => CompactMaterialValue(kind, material.Value))
            .Where(value => value.Length > 0)
            .GroupBy(NormalizedMaterialValue, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (kind == "HonestyConstraint")
        {
            items = items.Select(SentenceCase).ToArray();
        }

        return kind switch
        {
            "TargetSet" => string.Join(" / ", items),
            "ComponentPayload" => string.Join(Environment.NewLine + Environment.NewLine, items),
            _ => string.Join(Environment.NewLine, items),
        };
    }

    private static string CompactMaterialValue(string kind, string value)
    {
        var compact = value.Trim();
        compact = kind switch
        {
            "SourceBranchStandard" => CompactSourceStandard(compact),
            "PressureSource" => CompactPressure(compact),
            "NoStandardLoweringMarker" => "Same standard. No exceptions.",
            "CompositeTaskPrompt" => CompactCompositePrompt(compact),
            "ComponentPayload" => CompactComponent(compact),
            "ComponentEvidenceRequirement" => CompactComponentEvidence(compact),
            "BranchScoringKey" => "Score each branch separately.",
            "OperationStep" => CompactOperationStep(compact),
            "AuditReference" => CompactAuditRecord(compact),
            "LockedOriginalOutput" => CompactLockedOriginal(compact),
            _ => compact,
        };
        if (kind is "PositiveExample" or "NegativeExample" or "UnseenExample")
        {
            var colon = compact.IndexOf(':');
            if (colon >= 0 && colon < compact.Length - 1)
            {
                compact = compact[(colon + 1)..].Trim();
            }
        }

        if (kind == "RuleFamily")
        {
            compact = compact.Replace('-', ' ').Replace('_', ' ');
            compact = SentenceCase(compact);
        }

        return compact;
    }

    private static string CompactOperationStep(string value)
    {
        var compact = value
            .Replace("Step 1: reverse the held source item order.", "1. Reverse order", StringComparison.OrdinalIgnoreCase)
            .Replace("Step 2: rotate the current order one position left.", "2. Rotate left 1", StringComparison.OrdinalIgnoreCase);
        return SentenceCase(compact.Trim().TrimEnd('.'));
    }

    private static string CompactLockedOriginal(string value)
    {
        var compact = value.StartsWith("Locked original output; ", StringComparison.OrdinalIgnoreCase)
            ? value["Locked original output; ".Length..]
            : value;
        return compact.Replace(" | ", Environment.NewLine, StringComparison.Ordinal);
    }

    private static string CompactAuditRecord(string value)
    {
        var compact = value.StartsWith("Source record; ", StringComparison.OrdinalIgnoreCase)
            ? value["Source record; ".Length..]
            : value;
        return compact.Replace(" | ", Environment.NewLine, StringComparison.Ordinal);
    }

    private static string CompactSourceStandard(string value)
    {
        var branch = SegmentValue(value, "; branch ") ?? "source";
        var level = SegmentValue(value, "; level ") ?? "level";
        var standard = SourceStandardCriterion(value);
        var branchName = branch switch
        {
            "FH" => "Focus Hold",
            "FS" => "Focus Shift",
            "WM" => "Working Memory",
            "IR" => "Inhibition",
            "DE" => "Discrimination",
            "CO" => "Concept Operations",
            "AI" => "Pressure Control",
            "TI" => "Integration",
            _ => branch,
        };
        return $"{branchName} · {level}{Environment.NewLine}{CompactSourceCriterion(standard)}";
    }

    private static string? CompactWrappedSourceCriterion(string value)
    {
        var criterion = DelimitedSegment(value, "wrapped source criterion ", "; underlying branch demand") ??
            DelimitedSegment(value, "wrapped source criterion ", "; task complexity") ??
            DelimitedSegment(value, "wrapped source criterion ", "; complete ");
        return criterion is null ? null : CompactSourceCriterion(criterion);
    }

    private static string SourceStandardCriterion(string value)
    {
        return DelimitedSegment(value, "; standard ", "; source honesty constraint") ??
            DelimitedSegment(value, "; standard ", "; visible in the transfer artifact") ??
            SegmentValue(value, "; standard ") ??
            "Keep the original passing standard";
    }

    private static string CompactSourceCriterion(string value)
    {
        if (value.Contains("periodic irrelevant prompts", StringComparison.OrdinalIgnoreCase))
        {
            return "5:00 · ignore prompts · max 5 wanders";
        }

        if (value.Contains("invalid inhibition", StringComparison.OrdinalIgnoreCase))
        {
            return "90% valid · no invalid taps";
        }

        if (value.Contains("invalid cues must never", StringComparison.OrdinalIgnoreCase))
        {
            return "90% valid · no invalid taps";
        }

        if (value.Contains("correction within 2 items", StringComparison.OrdinalIgnoreCase))
        {
            return "88% correct · no rule drift · correct within 2";
        }

        return CompactStandard(value);
    }

    private static string? DelimitedSegment(string value, string marker, string endMarker)
    {
        var start = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = value.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
        return (end < 0 ? value[start..] : value[start..end]).Trim().TrimEnd('.');
    }

    private static string CompactCompositePrompt(string value)
    {
        var combines = value.IndexOf("combines ", StringComparison.OrdinalIgnoreCase);
        if (combines < 0)
        {
            return "Complete every component to its own standard.";
        }

        combines += "combines ".Length;
        var end = value.IndexOf(';', combines);
        var summary = (end < 0 ? value[combines..] : value[combines..end]).Trim();
        return summary.Replace(", ", " + ", StringComparison.Ordinal);
    }

    private static string CompactComponent(string value)
    {
        var branch = SegmentValue(value, "component branch ") ?? "component";
        branch = branch.Split(':', StringSplitOptions.TrimEntries)[0];
        var level = SegmentValue(value, " level ") ?? string.Empty;
        var role = CompactComponentRole(
            SegmentValue(value, "; task role ") ?? "Complete the component task.");
        var criterion = CompactStandard(
            SegmentValue(value, "; pass criterion ") ?? "Exact response; no component error.");
        var challenge = SegmentValue(value, "; challenge ") ?? "Complete the branch task.";
        return $"{branch} · {level}{Environment.NewLine}{role}{Environment.NewLine}{challenge}{Environment.NewLine}{criterion}";
    }

    private static (string Branch, string Role, string Challenge, string Criterion) GlobalReviewComponentSummary(string value)
    {
        var branch = SegmentValue(value, "component branch ") ?? "component";
        branch = branch.Split(':', StringSplitOptions.TrimEntries)[0];
        var role = branch switch
        {
            "FH" => "Hold target",
            "FS" => "Valid switches",
            "WM" => "Recall",
            "IR" => "Keep rule",
            "DE" => "Audit errors",
            "CO" => "Model relations",
            "AI" => "Hold standards",
            _ => CompactComponentRole(
                SegmentValue(value, "; task role ") ?? "Complete component"),
        };
        var criterion = CompactStandard(
            SegmentValue(value, "; pass criterion ") ?? "Exact response; no component error.");
        var challenge = SegmentValue(value, "; challenge ") ?? "Complete the branch task.";
        return (branch, role, challenge, criterion);
    }

    private static string HeldTargetValue(string challenge)
    {
        const string startMarker = "Hold ";
        const string endMarker = " while";
        if (!challenge.StartsWith(startMarker, StringComparison.OrdinalIgnoreCase))
        {
            return "Held target";
        }

        var end = challenge.IndexOf(endMarker, startMarker.Length, StringComparison.OrdinalIgnoreCase);
        return end < 0
            ? challenge[startMarker.Length..].Trim().TrimEnd('.')
            : challenge[startMarker.Length..end].Trim();
    }

    private static string CompactComponentRole(string role)
    {
        if (role.Contains("preserve the stated rule", StringComparison.OrdinalIgnoreCase))
        {
            return "Keep the stated rule under composite cues.";
        }

        if (role.Contains("audit component output", StringComparison.OrdinalIgnoreCase))
        {
            return "Audit output without editing the original.";
        }

        return SentenceCase(role.Trim().TrimEnd('.')) + ".";
    }

    private static string CompactStandard(string standard)
    {
        return standard
            .Replace("At least ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Find at least ", "Find ", StringComparison.OrdinalIgnoreCase)
            .Replace("no more than ", "max ", StringComparison.OrdinalIgnoreCase)
            .Replace("no unmarked rule drift", "no rule drift", StringComparison.OrdinalIgnoreCase)
            .Replace("correction within 2 items after error", "correct within 2 items", StringComparison.OrdinalIgnoreCase)
            .Replace("; ", " · ", StringComparison.Ordinal);
    }

    private static string CompactComponentEvidence(string value)
    {
        var branch = value.StartsWith("branch ", StringComparison.OrdinalIgnoreCase)
            ? value["branch ".Length..].Split(' ', 2)[0]
            : "Each branch";
        return $"{branch}: passing evidence required";
    }

    private static string CompactPressure(string value)
    {
        var time = AssignmentValue(value, "TimePressure=");
        var evaluator = AssignmentValue(value, "EvaluativePressure=");
        if (time is not null && evaluator is not null)
        {
            return $"{time} · {evaluator}";
        }

        return time ?? evaluator ?? "Declared pressure";
    }

    private static string? SegmentValue(string value, string marker)
    {
        var start = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = value.IndexOf(';', start);
        return (end < 0 ? value[start..] : value[start..end]).Trim().TrimEnd('.');
    }

    private static string? AssignmentValue(string value, string marker)
    {
        var start = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = value.IndexOf(';', start);
        return (end < 0 ? value[start..] : value[start..end]).Trim().TrimEnd('.');
    }

    private static bool ShouldShowMaterial(
        LiveSessionPresentationReadModel presentation,
        PreUiLiveSessionMaterialState material)
    {
        if (material.Kind == "BranchScoringKey")
        {
            return false;
        }

        if (presentation.Work.Drill is DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery)
        {
            if (material.Kind is "SourceTask" or
                "NoStandardLoweringMarker" or "RestartRule" or "DisruptionEvent")
            {
                return false;
            }

            if (presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.CueResponse &&
                material.Kind == "ResponseWindow")
            {
                return false;
            }
        }

        if (presentation.Work.Drill == DrillId.IR2ExceptionRule &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.CueResponse)
        {
            return false;
        }

        if (presentation.Work.Drill == DrillId.WM2MentalTransform &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.EncodeWindow &&
            material.Kind is "TransformRule" or "HiddenNotePolicy")
        {
            return false;
        }

        if (presentation.Work.Drill == DrillId.WM2MentalTransform &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ReconstructionInput &&
            material.Kind is "RuleExplanationPrompt" or "HiddenNotePolicy")
        {
            return false;
        }

        if (presentation.Work.Drill == DrillId.DE2SeededAudit &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.Audit &&
            material.Kind is "AuditInstruction" or "NonErrorDistractor")
        {
            return false;
        }

        if (presentation.Work.Drill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.CueResponse &&
            material.Kind == "ResponseWindow")
        {
            return false;
        }

        if (presentation.Work.Drill == DrillId.TI2GlobalReviewTask &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork)
        {
            return material.Kind is "CompositeTaskPrompt" or "ComponentPayload";
        }

        return presentation.Work.Drill != DrillId.CO1RuleExtraction ||
            presentation.CurrentPhaseKind != RuntimeSessionPhaseKind.ActiveWork ||
            material.Kind != "RuleFamily";
    }

    private static string CompactRuleCue(string value)
    {
        var colon = value.IndexOf(':');
        return colon >= 0 && colon < value.Length - 1
            ? value[(colon + 1)..].Trim()
            : value;
    }

    private static string NormalizedMaterialValue(string value)
    {
        return value.Trim().TrimEnd('.', ';', ':').TrimEnd();
    }

    private static string SentenceCase(string value)
    {
        return value.Length == 0 || char.IsUpper(value[0])
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string? TargetMaterial(LiveSessionPresentationReadModel presentation)
    {
        return presentation.CurrentMaterials.FirstOrDefault(material =>
            string.Equals(material.Kind, "TargetStatement", StringComparison.Ordinal))?.Value;
    }

    private static string Humanize(string value)
    {
        return value.Replace('-', ' ').Replace('_', ' ');
    }

    private sealed record LiveInputDraft(
        string SessionId,
        string? PhaseId,
        string Response,
        IReadOnlyDictionary<string, string> Fields,
        IReadOnlyDictionary<string, string> Choices,
        IReadOnlyList<string> ExpandedComponents,
        IReadOnlyList<int> GuessedPairIndexes,
        bool HeldTargetReportUnlocked);

    private LayoutParams MatchWrap()
    {
        return new LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
    }

    private LayoutParams MatchWrapWithTop(int topDp)
    {
        var layout = MatchWrap();
        layout.SetMargins(0, MgSpacing.Dp(Context!, topDp), 0, 0);
        return layout;
    }
}
