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
    private readonly TextView workLabel;
    private readonly TimerRingView timer;
    private readonly TextView instruction;
    private readonly LinearLayout cueBand;
    private readonly TextView cueMode;
    private readonly TextView cueText;
    private readonly TargetMaterialView target;
    private readonly LinearLayout materialList;
    private readonly LinearLayout cueChoices;
    private readonly EditText responseInput;
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
    private bool stopConfirmationArmed;
    private readonly List<string> pairAnswers = [];
    private readonly HashSet<string> expandedComponents = new(StringComparer.Ordinal);
    private int pairIndex;

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

        target = new TargetMaterialView(context, compact: true);
        AddView(target, MatchWrapWithTop(MgSpacing.Md));

        materialList = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
        };
        AddView(materialList, MatchWrapWithTop(MgSpacing.Md));

        cueChoices = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            Visibility = ViewStates.Gone,
        };
        AddView(cueChoices, MatchWrapWithTop(MgSpacing.Md));

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

        primaryButton = new SessionActionButton(context, "Continue", enabled: false);
        primaryButton.Click += (_, _) => SendPrimaryCommand();
        AddView(primaryButton, MatchWrapWithTop(MgSpacing.Md));

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

    public void Update(AndroidLiveSessionSnapshot next)
    {
        ArgumentNullException.ThrowIfNull(next);

        snapshot = next;
        var live = next.LiveSession;
        var presentation = next.Presentation;
        if (!string.Equals(sessionId, live.SessionId, StringComparison.Ordinal))
        {
            sessionId = live.SessionId;
            responseInput.Text = string.Empty;
            materialSignature = string.Empty;
            secondarySignature = string.Empty;
            stopConfirmationArmed = false;
            pairAnswers.Clear();
            expandedComponents.Clear();
            pairIndex = 0;
        }

        var drill = presentation.Work.Drill ?? live.Drill;
        workLabel.Text = presentation.Work.Exercise.BranchLevelLabel;
        timer.Update(presentation.Timer, presentation.LifecycleStatus);
        UpdateTimerSize(drill, presentation.CurrentPhaseKind);
        instruction.Text = presentation.CurrentInstruction;
        var isPrep = presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.InstructionPrep;
        timer.Visibility = !isPrep && presentation.Timer.IsTimed ? ViewStates.Visible : ViewStates.Gone;
        evidenceStrip.Visibility = isPrep ? ViewStates.Gone : ViewStates.Visible;

        UpdateCue(presentation);
        UpdateCueChoices(presentation);

        var effectiveDrill = presentation.SourceDrill ?? drill;
        var isFocusHold = effectiveDrill is DrillId.FH1TargetHold or DrillId.FH2DistractorHold;
        var isReview = presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.Review;
        target.Visibility = isFocusHold && !isReview ? ViewStates.Visible : ViewStates.Gone;
        materialList.Visibility = isFocusHold || isReview || presentation.CurrentMaterials.Count == 0
            ? ViewStates.Gone
            : ViewStates.Visible;
        if (isFocusHold && !isReview)
        {
            target.Update(presentation.SourceDrill.HasValue
                ? TargetMaterial(presentation)
                : presentation.Work.Exercise.PrimaryMaterial ?? TargetMaterial(presentation));
        }
        else if (!isReview)
        {
            UpdateMaterialList(presentation);
        }

        var isPairWork = drill == DrillId.DE1PairDiscrimination &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork;
        var needsInput = presentation.AvailableCommands.Any(command =>
            RequiresTextInput(command.Command, drill)) && !isPairWork;
        responseInput.Visibility = needsInput ? ViewStates.Visible : ViewStates.Gone;
        responseInput.Hint = ResponseHint(
            effectiveDrill,
            presentation.CurrentPhaseKind,
            presentation.CurrentMaterials);

        primaryCommand = presentation.PrimaryCommand?.Command;
        UpdatePrimaryButton(presentation);
        UpdateSecondaryCommands(presentation);
        UpdateEvidence(presentation);
    }

    private void UpdatePrimaryButton(LiveSessionPresentationReadModel presentation)
    {
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
        var enabled = true;
        var label = DisplayLabel(presentation.PrimaryCommand, presentation, primary: true);
        primaryButton.Text = label;
        primaryButton.Enabled = enabled;
        primaryButton.ContentDescription = label;
        primaryButton.SetTextColor(Color.White);
        primaryButton.Background = MgTheme.ActionGradient(Context!, cornerRadius: 8);
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

        if (presentation.Work.Drill == DrillId.TI2GlobalReviewTask &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork)
        {
            RenderGlobalReviewComponents(presentation);
            return;
        }

        if (presentation.Work.Drill == DrillId.CO2StructureMapping &&
            presentation.CurrentPhaseKind == RuntimeSessionPhaseKind.ActiveWork)
        {
            RenderStructureMapping(presentation);
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

    private void RenderGlobalReviewComponents(LiveSessionPresentationReadModel presentation)
    {
        var components = presentation.CurrentMaterials
            .Where(material => string.Equals(material.Kind, "ComponentPayload", StringComparison.Ordinal))
            .ToArray();
        var signature = "global-review:" + string.Join(
            '|',
            components.Select(component => $"{component.Name}:{expandedComponents.Contains(component.Name)}"));
        if (string.Equals(signature, materialSignature, StringComparison.Ordinal))
        {
            return;
        }

        materialSignature = signature;
        materialList.RemoveAllViews();
        materialList.Visibility = components.Length == 0 ? ViewStates.Gone : ViewStates.Visible;
        for (var index = 0; index < components.Length; index += 2)
        {
            var row = new LinearLayout(Context)
            {
                Orientation = Orientation.Horizontal,
            };
            row.SetGravity(GravityFlags.FillHorizontal);
            for (var column = 0; column < 2; column++)
            {
                var componentIndex = index + column;
                if (componentIndex >= components.Length)
                {
                    row.AddView(new Space(Context), new LayoutParams(0, 1, 1));
                    continue;
                }

                var component = components[componentIndex];
                var summary = GlobalReviewComponentSummary(component.Value);
                var expanded = expandedComponents.Contains(component.Name);
                var tile = new LinearLayout(Context)
                {
                    Orientation = Orientation.Vertical,
                    Background = MgTheme.MutedSurface(Context!, cornerRadius: 8),
                    ContentDescription = $"{summary.Branch}, {summary.Role}. {summary.Standard}",
                    Clickable = true,
                    Focusable = true,
                };
                tile.SetPadding(
                    MgSpacing.Dp(Context!, MgSpacing.Sm),
                    MgSpacing.Dp(Context!, MgSpacing.Sm),
                    MgSpacing.Dp(Context!, MgSpacing.Sm),
                    MgSpacing.Dp(Context!, MgSpacing.Sm));
                var header = new TextView(Context)
                {
                    Text = $"{summary.Branch}    {(expanded ? "-" : "+")}",
                };
                MgTypography.ApplyLabel(header);
                header.SetTextColor(MgColors.TrainingDark);
                tile.AddView(header, MatchWrap());
                var role = new TextView(Context)
                {
                    Text = summary.Role,
                };
                MgTypography.ApplyBody(role);
                tile.AddView(role, MatchWrapWithTop(MgSpacing.Xs));
                if (expanded)
                {
                    var challenge = new TextView(Context)
                    {
                        Text = summary.Challenge,
                    };
                    MgTypography.ApplyBody(challenge);
                    tile.AddView(challenge, MatchWrapWithTop(MgSpacing.Sm));

                    var standard = new TextView(Context)
                    {
                        Text = summary.Standard,
                    };
                    MgTypography.ApplyLabel(standard);
                    standard.SetTextColor(MgColors.InkMuted);
                    tile.AddView(standard, MatchWrapWithTop(MgSpacing.Sm));
                }

                tile.Click += (_, _) =>
                {
                    if (!expandedComponents.Add(component.Name))
                    {
                        expandedComponents.Remove(component.Name);
                    }

                    materialSignature = string.Empty;
                    RenderGlobalReviewComponents(presentation);
                };
                var layout = new LayoutParams(0, LayoutParams.WrapContent, 1);
                if (column > 0)
                {
                    layout.SetMargins(MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0, 0);
                }

                row.AddView(tile, layout);
            }

            materialList.AddView(row, MatchWrapWithTop(index == 0 ? 0 : MgSpacing.Xs));
        }
    }

    private void UpdateTimerSize(DrillId drill, RuntimeSessionPhaseKind? phase)
    {
        var size = drill == DrillId.TI2GlobalReviewTask && phase == RuntimeSessionPhaseKind.ActiveWork
            ? 96
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
        var source = presentation.CurrentMaterials.FirstOrDefault(material => material.Kind == "SourceTask")?.Value ?? string.Empty;
        var disruption = presentation.CurrentMaterials.FirstOrDefault(material => material.Kind == "DisruptionEvent")?.Value ?? string.Empty;
        var restart = presentation.CurrentMaterials.FirstOrDefault(material => material.Kind == "RestartRule")?.Value ?? string.Empty;
        var signature = $"recovery:{source}|{disruption}|{restart}";
        if (string.Equals(signature, materialSignature, StringComparison.Ordinal))
        {
            return;
        }

        materialSignature = signature;
        materialList.RemoveAllViews();
        materialList.Visibility = ViewStates.Visible;
        var row = new LinearLayout(Context)
        {
            Orientation = Orientation.Horizontal,
        };
        var stages = new[]
        {
            (Label: "TASK", Value: CompactRecoverySource(source)),
            (Label: "DISRUPT", Value: CompactDisruption(disruption)),
            (Label: "RESUME", Value: CompactRestart(restart)),
        };
        for (var index = 0; index < stages.Length; index++)
        {
            var stage = stages[index];
            var tile = new LinearLayout(Context)
            {
                Orientation = Orientation.Vertical,
                Background = MgTheme.MutedSurface(Context!, cornerRadius: 8),
            };
            tile.SetPadding(
                MgSpacing.Dp(Context!, MgSpacing.Sm),
                MgSpacing.Dp(Context!, MgSpacing.Sm),
                MgSpacing.Dp(Context!, MgSpacing.Sm),
                MgSpacing.Dp(Context!, MgSpacing.Sm));
            var label = new TextView(Context)
            {
                Text = stage.Label,
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyLabel(label);
            label.SetTextColor(MgColors.TrainingDark);
            tile.AddView(label, MatchWrap());
            var value = new TextView(Context)
            {
                Text = stage.Value,
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyBody(value);
            tile.AddView(value, MatchWrapWithTop(MgSpacing.Xs));
            var layout = new LayoutParams(0, LayoutParams.WrapContent, 1);
            if (index > 0)
            {
                layout.SetMargins(MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0, 0);
            }

            row.AddView(tile, layout);
        }

        materialList.AddView(row, MatchWrap());
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
        var signature = $"pair:{pairIndex}:{string.Join('|', pairs.Select(pair => pair.Value))}";
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
                Text = $"{pairAnswers.Count} comparisons ready",
                Gravity = GravityFlags.Center,
            };
            MgTypography.ApplyHeading(ready);
            materialList.AddView(ready, MatchWrap());
            responseInput.Text = string.Join("; ", pairAnswers);
            UpdatePrimaryButton(presentation);
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
            pairAnswers.Add($"{pair.Name}={value}");
            pairIndex++;
            materialSignature = string.Empty;
            RenderPairDiscrimination(presentation);
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
            presentation.Work.Drill == DrillId.AI2DisruptionRecovery;
        var neutralCue = drill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter or DrillId.IR2ExceptionRule;
        var activeStyle = neutralCue || respond;
        cueBand.Visibility = ViewStates.Visible;
        cueBand.Background = MgTheme.TintedSurface(
            Context!,
            activeStyle ? MgColors.TrainingTint : MgColors.MaintenanceTint,
            activeStyle ? MgColors.Training : MgColors.Maintenance,
            cornerRadius: 8);
        cueMode.Text = isDisruption ? "DISRUPTION" : neutralCue ? "CUE" : respond ? "RESPOND" : "IGNORE";
        cueMode.SetTextColor(neutralCue || respond ? MgColors.TrainingDark : MgColors.Ink);
        cueText.Text = isDisruption
            ? "Resume from the last stable step."
            : drill == DrillId.IR2ExceptionRule ? CompactRuleCue(cue.Cue) : cue.Cue;
    }

    private void UpdateCueChoices(LiveSessionPresentationReadModel presentation)
    {
        cueChoices.RemoveAllViews();
        var effectiveDrill = presentation.SourceDrill ?? presentation.Work.Drill;
        var canRespond = presentation.ActiveCue?.RequiresResponse == true &&
            presentation.ActiveCue.Kind != RuntimeCueKind.Interruption &&
            presentation.AvailableCommands.Any(command =>
                command.Command == RuntimeInputCommandKind.RespondToCue);
        if (effectiveDrill is not (DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter) || !canRespond)
        {
            cueChoices.Visibility = ViewStates.Gone;
            return;
        }

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
                ContentDescription = $"Switch to {response}",
            };
            button.SetAllCaps(false);
            button.SetSingleLine(false);
            button.SetMinHeight(MgSpacing.Dp(Context!, 52));
            button.SetTextSize(global::Android.Util.ComplexUnitType.Sp, MgTypography.BodySp);
            button.SetTextColor(Color.White);
            button.Background = MgTheme.ActionGradient(Context!, cornerRadius: 8);
            button.Click += (_, _) => SendCueResponse(response);
            var layout = new LayoutParams(0, MgSpacing.Dp(Context!, 60), 1);
            if (index > 0)
            {
                layout.SetMargins(MgSpacing.Dp(Context!, MgSpacing.Xs), 0, 0, 0);
            }

            cueChoices.AddView(button, layout);
        }
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
            SendCommand(primaryCommand.Value);
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
            RuntimeInputCommandKind.RespondToCue or
            RuntimeInputCommandKind.SubmitAnswer or
            RuntimeInputCommandKind.MarkError or
            RuntimeInputCommandKind.Correct => responseInput.Text,
            RuntimeInputCommandKind.MarkTargetChange => "different target",
            RuntimeInputCommandKind.Abandon => live.CurrentPhaseKind == RuntimeSessionPhaseKind.InstructionPrep
                ? "user cancelled setup before work"
                : "user stopped active session",
            _ => null,
        };

        CommandRequested?.Invoke(command, targetId, value);
    }

    private void SendCueResponse(string response)
    {
        var cueId = snapshot?.LiveSession.ActiveCue?.CueId;
        if (cueId is null)
        {
            return;
        }

        CommandRequested?.Invoke(RuntimeInputCommandKind.RespondToCue, cueId, response);
    }

    private static bool RequiresTextInput(
        RuntimeInputCommandKind command,
        DrillId drill)
    {
        _ = drill;
        return command is RuntimeInputCommandKind.SubmitAnswer or RuntimeInputCommandKind.Correct;
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
            .ToArray();
        var semantic = candidates
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
        return command.Command switch
        {
            RuntimeInputCommandKind.FinishPhase when phase == RuntimeSessionPhaseKind.InstructionPrep =>
                StartLabel(presentation.SourceDrill ?? drill),
            RuntimeInputCommandKind.FinishPhase when phase == RuntimeSessionPhaseKind.Review => primary ? "Finish session" : "Finish",
            RuntimeInputCommandKind.RespondToCue when presentation.ActiveCue?.Kind == RuntimeCueKind.Interruption => "Resume",
            RuntimeInputCommandKind.RespondToCue when (presentation.SourceDrill ?? drill) is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter => "Switch",
            RuntimeInputCommandKind.RespondToCue => "Respond",
            RuntimeInputCommandKind.SubmitAnswer => SubmitLabel(drill),
            RuntimeInputCommandKind.MarkGuess when drill is
                DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery => "Mark uncertain",
            RuntimeInputCommandKind.MarkGuess => "I guessed",
            RuntimeInputCommandKind.MarkError => "Report error",
            RuntimeInputCommandKind.Correct => "Replace answer",
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
            DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter or
            DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => "Start cues",
            DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform => "Show material",
            DrillId.DE2SeededAudit or DrillId.TI2GlobalReviewTask => "Open audit",
            _ => "Start task",
        };
    }

    private static string SubmitLabel(DrillId drill)
    {
        return drill switch
        {
            DrillId.WM1DelayedReconstruction => "Submit reconstruction",
            DrillId.WM2MentalTransform => "Submit result",
            DrillId.DE1PairDiscrimination => "Submit comparison",
            DrillId.DE2SeededAudit => "Submit findings",
            DrillId.CO1RuleExtraction => "Submit rule",
            DrillId.CO2StructureMapping => "Submit mapping",
            DrillId.AI1PressureRepeat or DrillId.AI2DisruptionRecovery => "Submit attempt",
            DrillId.TI1CompositeTask or DrillId.TI2GlobalReviewTask => "Submit evidence",
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

            return drill == DrillId.WM2MentalTransform
                ? "Final result and rule"
                : "Reconstruction";
        }

        if (phase == RuntimeSessionPhaseKind.Audit && drill == DrillId.CO2StructureMapping)
        {
            return "ASSUMPTION=...; LIMIT=SUPPORTED; PREDICTION=PASSED";
        }

        return drill switch
        {
            DrillId.DE1PairDiscrimination => "Same or different, with reason",
            DrillId.DE2SeededAudit => "Supported findings",
            DrillId.CO1RuleExtraction => "Rule and unseen classifications",
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
            "LockedOriginalOutput" => "Original",
            "AuditInstruction" => "Audit",
            "AuditPayload" => "Audit input",
            "DelayedReconstructionPayload" => "Delayed reconstruction",
            "SourceBranchStandard" => "Standard",
            "PressureSource" => "Pressure",
            "SourceTask" => "Task",
            "DisruptionEvent" => "Disruption",
            "RestartRule" => "Restart rule",
            "CompositeTaskPrompt" => "Task",
            "ComponentPayload" => "Component",
            "ComponentEvidenceRequirement" => "Evidence required",
            "BranchScoringKey" => "Passing standard",
            "ExpectedReconstruction" or "FinalExpectedOutput" or
            "ExpectedFinding" or "ExpectedClassification" or "ExpectedMapping" => "Answer key",
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

    private static string CompactSourceStandard(string value)
    {
        var branch = SegmentValue(value, "; branch ") ?? "source";
        var level = SegmentValue(value, "; level ") ?? "level";
        var standard = SegmentValue(value, "; standard ") ?? "Keep the original passing standard.";
        var branchName = branch switch
        {
            "FH" => "Focus Hold",
            "FS" => "Focus Shift",
            "WM" => "Working Memory",
            "IR" => "Inhibition",
            "DE" => "Discrimination",
            "CO" => "Concept Operations",
            _ => branch,
        };
        return $"{branchName} · {level}{Environment.NewLine}{standard}";
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
        var standard = CompactStandard(
            SegmentValue(value, "; source standard ") ?? "Keep the branch standard passing.");
        var challenge = SegmentValue(value, "; challenge ") ?? "Complete the branch task.";
        return $"{branch} · {level}{Environment.NewLine}{role}{Environment.NewLine}{challenge}{Environment.NewLine}Enter {branch}=answer{Environment.NewLine}{standard}";
    }

    private static (string Branch, string Role, string Challenge, string Standard) GlobalReviewComponentSummary(string value)
    {
        var branch = SegmentValue(value, "component branch ") ?? "component";
        branch = branch.Split(':', StringSplitOptions.TrimEntries)[0];
        var role = branch switch
        {
            "FH" => "Hold target",
            "FS" => "Valid switches",
            "WM" => "Reconstruct",
            "IR" => "Keep rule",
            "DE" => "Audit errors",
            "CO" => "Model relations",
            "AI" => "Hold standards",
            _ => CompactComponentRole(
                SegmentValue(value, "; task role ") ?? "Complete component"),
        };
        var standard = CompactStandard(
            SegmentValue(value, "; source standard ") ?? "Keep the branch standard passing.");
        var challenge = SegmentValue(value, "; challenge ") ?? "Complete the branch task.";
        return (branch, role, $"{challenge}{Environment.NewLine}Enter {branch}=answer", standard);
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
            if (material.Kind is "SourceTask" or "SourceBranchStandard" or
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
