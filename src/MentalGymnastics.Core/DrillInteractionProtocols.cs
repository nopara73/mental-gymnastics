namespace MentalGymnastics.Core;

public enum DrillInteractionInputKind
{
    FocusReturnPad,
    CueChoices,
    GoNoGoPad,
    MemoryResponse,
    ComparisonChoices,
    AuditResponse,
    RuleResponse,
    RelationResponse,
    PressureTask,
    RecoveryTask,
    ComponentEvidence,
}

public enum DrillInteractionAcknowledgement
{
    Visual,
    VisualAndHaptic,
}

public sealed record DrillInteractionStep(
    string Label,
    string Instruction);

public sealed record DrillInteractionProtocol(
    DrillId Drill,
    DrillInteractionInputKind InputKind,
    string AttentionInstruction,
    string DeviceInstruction,
    string ActionInstruction,
    string ScreenBehavior,
    DrillInteractionAcknowledgement Acknowledgement,
    IReadOnlyList<DrillInteractionStep> Steps);

public static class DrillInteractionProtocolCatalog
{
    public static IReadOnlyList<DrillInteractionProtocol> Protocols { get; } =
    [
        Protocol(
            DrillId.FH1TargetHold,
            DrillInteractionInputKind.FocusReturnPad,
            "Eyes open. Keep the target on screen.",
            "Rest the phone where one hand can reach the lower pad without searching.",
            "Tap Wandered when you notice attention left. The tap vibrates. Return to the same target, then tap Back on target.",
            "The target remains visible for the whole hold.",
            DrillInteractionAcknowledgement.VisualAndHaptic,
            ("EYES OPEN", "Keep the stated target in view."),
            ("NOTICE", "Tap as soon as you notice a wander."),
            ("RETURN", "Reacquire the same target, then confirm.")),
        Protocol(
            DrillId.FH2DistractorHold,
            DrillInteractionInputKind.FocusReturnPad,
            "Eyes open. Keep the target on screen and let distractors pass without answering them.",
            "Rest the phone where one hand can reach the lower pad without searching.",
            "Tap Wandered only for loss of the target. The tap vibrates. Return to the same target, then tap Back on target.",
            "The target and controlled distractors remain visible during the hold.",
            DrillInteractionAcknowledgement.VisualAndHaptic,
            ("EYES OPEN", "Keep the stated target foregrounded."),
            ("IGNORE", "Do not answer distractors."),
            ("RETURN", "Mark, reacquire, and confirm.")),
        Protocol(
            DrillId.FS1CueSwitch,
            DrillInteractionInputKind.CueChoices,
            "Eyes open. Watch the cue band and the named target choices.",
            "Keep the phone still with the target buttons in easy thumb reach.",
            "On a valid cue, tap the target named by the rule. Do not switch before the cue.",
            "The active cue and target choices stay visible until the response window closes.",
            DrillInteractionAcknowledgement.VisualAndHaptic,
            ("WATCH", "Wait for the cue."),
            ("CHOOSE", "Tap the required target."),
            ("HOLD", "Stay there until the next cue.")),
        Protocol(
            DrillId.FS2InvalidCueFilter,
            DrillInteractionInputKind.CueChoices,
            "Eyes open. Watch every cue; invalid cues require no touch.",
            "Keep the phone still with the target buttons in easy thumb reach.",
            "Tap the required target on valid cues. On an invalid cue, do nothing until it clears.",
            "Target buttons stay in the same place. They mute during invalid cues, and any tap is recorded as an invalid switch.",
            DrillInteractionAcknowledgement.VisualAndHaptic,
            ("CHECK", "Decide whether the cue is valid."),
            ("SWITCH", "Tap only for a valid cue."),
            ("WITHHOLD", "Leave invalid cues unanswered.")),
        Protocol(
            DrillId.WM1DelayedReconstruction,
            DrillInteractionInputKind.MemoryResponse,
            "Eyes open while studying. During the delay, the items are hidden.",
            "Keep the phone face up; do not take a screenshot or use notes.",
            "Study once, wait through the hidden delay, then enter the reconstruction from memory.",
            "The app removes the items when encoding ends and does not reveal them before submission.",
            DrillInteractionAcknowledgement.Visual,
            ("STUDY", "Encode the visible items once."),
            ("HIDDEN", "Hold them without reopening or notes."),
            ("REBUILD", "Enter only what you remember.")),
        Protocol(
            DrillId.WM2MentalTransform,
            DrillInteractionInputKind.MemoryResponse,
            "Eyes open while studying. The source is hidden before the response.",
            "Keep the phone face up; use no notes or intermediate text.",
            "Apply every operation mentally, then enter the final result and explain the rule used.",
            "The source is hidden during the delay and response; only the transform rule remains.",
            DrillInteractionAcknowledgement.Visual,
            ("STUDY", "Encode the source and operations."),
            ("TRANSFORM", "Carry out the steps mentally."),
            ("ANSWER", "Enter the final result and rule.")),
        Protocol(
            DrillId.IR1GoNoGoRule,
            DrillInteractionInputKind.GoNoGoPad,
            "Eyes open. Watch one cue at a time.",
            "Keep one thumb resting near the large response pad.",
            "Tap the pad on GO. On WITHHOLD, do not touch the screen until the cue clears.",
            "The response pad stays in one place for every cue; it mutes on WITHHOLD, and any touch is recorded.",
            DrillInteractionAcknowledgement.VisualAndHaptic,
            ("READ", "Apply the stated rule."),
            ("TAP", "Respond only to GO."),
            ("STOP", "Leave WITHHOLD untouched.")),
        Protocol(
            DrillId.IR2ExceptionRule,
            DrillInteractionInputKind.GoNoGoPad,
            "Eyes open. Check the exception before applying the base rule.",
            "Keep one thumb resting near the large response pad.",
            "Tap only when the final rule says GO. If the exception says WITHHOLD, do nothing.",
            "The rule remains available before the set; the stable pad mutes on WITHHOLD and still records an accidental touch.",
            DrillInteractionAcknowledgement.VisualAndHaptic,
            ("CHECK", "Test the exception first."),
            ("APPLY", "Use the resulting action."),
            ("WITHHOLD", "No touch is a recorded response.")),
        Protocol(
            DrillId.DE1PairDiscrimination,
            DrillInteractionInputKind.ComparisonChoices,
            "Eyes open. Compare only the named feature.",
            "Keep both Same and Different buttons within easy reach.",
            "If uncertain, mark Not sure before choosing. Then tap Same or Different once.",
            "One pair is shown at a time and cannot be reopened after the choice.",
            DrillInteractionAcknowledgement.VisualAndHaptic,
            ("COMPARE", "Use only the relevant feature."),
            ("MARK", "Declare uncertainty before answering."),
            ("CHOOSE", "Commit Same or Different.")),
        Protocol(
            DrillId.DE2SeededAudit,
            DrillInteractionInputKind.AuditResponse,
            "Eyes open. Study the source record, then inspect the locked report after the delay.",
            "Use the phone where every line is readable. Do not screenshot, copy, or take notes from the source record.",
            "Compare the locked report with the source held in memory. Record each supported line, mismatch type, and exact correction, then submit once.",
            "The source disappears before the delay; the report is locked during audit and the answer key remains hidden until review.",
            DrillInteractionAcknowledgement.Visual,
            ("STUDY", "Encode the correct source record."),
            ("AUDIT", "Compare from memory and record only supported mismatches."),
            ("SUBMIT", "Finish the audit once.")),
        Protocol(
            DrillId.CO1RuleExtraction,
            DrillInteractionInputKind.RuleResponse,
            "Eyes open. Compare positive and negative examples before unseen cases appear.",
            "Use the phone where the full examples can be read without accidental taps.",
            "State one testable rule, lock it, then classify each unseen example without rewriting the rule.",
            "The rule is submitted before unseen examples are revealed.",
            DrillInteractionAcknowledgement.Visual,
            ("INFER", "Find one rule fitting all shown examples."),
            ("LOCK", "Submit the rule before the test."),
            ("CLASSIFY", "Apply it to every unseen case.")),
        Protocol(
            DrillId.CO2StructureMapping,
            DrillInteractionInputKind.RelationResponse,
            "Eyes open. Compare roles and relations, not shared words.",
            "Use the phone where source and target structures remain readable.",
            "Name the source relations first, then enter the matching target relation for each row.",
            "Source and target structures stay visible; expected mappings stay hidden until review.",
            DrillInteractionAcknowledgement.Visual,
            ("NAME", "Identify the source relations."),
            ("MAP", "Pair each with a target relation."),
            ("TEST", "Reject unsupported surface matches.")),
        Protocol(
            DrillId.AI1PressureRepeat,
            DrillInteractionInputKind.PressureTask,
            "Eyes open. Use the source task controls exactly as before.",
            "Place the phone as required by the source task before starting the pressure condition.",
            "Complete the source task to its unchanged standard; mark uncertainty instead of hiding it.",
            "Pressure and the unchanged source standard remain visible together.",
            DrillInteractionAcknowledgement.VisualAndHaptic,
            ("STANDARD", "Read the unchanged pass rule."),
            ("PRESSURE", "Accept the added constraint."),
            ("REPEAT", "Use the source controls normally.")),
        Protocol(
            DrillId.AI2DisruptionRecovery,
            DrillInteractionInputKind.RecoveryTask,
            "Eyes open. Continue using the source task controls.",
            "Keep the phone in the source task position throughout the disruption.",
            "When interrupted, resume from the last stable step without restarting; tap Resumed only after resuming.",
            "The disruption is distinct from ordinary cues and the recovery window stays visible.",
            DrillInteractionAcknowledgement.VisualAndHaptic,
            ("WORK", "Follow the source rule."),
            ("DISRUPT", "Do not restart from the beginning."),
            ("RESUME", "Continue, then confirm the return.")),
        Protocol(
            DrillId.TI1CompositeTask,
            DrillInteractionInputKind.ComponentEvidence,
            "Eyes open. Work through the visible components in order.",
            "Use the phone where every component can be expanded and answered without losing prior entries.",
            "Complete every component and enter its exact response in its own labeled field before submitting.",
            "Component responses remain separate; one correct response cannot replace a missing branch.",
            DrillInteractionAcknowledgement.Visual,
            ("PARTS", "Open each required component."),
            ("PERFORM", "Keep its branch standard."),
            ("RESPOND", "Fill every labeled branch field.")),
        Protocol(
            DrillId.TI2GlobalReviewTask,
            DrillInteractionInputKind.ComponentEvidence,
            "Eyes open. Complete, audit, and reconstruct the composite in the shown order.",
            "Use the phone where every component and evidence field can be reviewed without losing entries.",
            "Answer each branch separately, name the planted mismatch and correction, then reconstruct the locked report after the hidden delay.",
            "Each phase reveals only the material allowed for that phase; answer keys stay hidden until review.",
            DrillInteractionAcknowledgement.Visual,
            ("COMPLETE", "Meet each component standard."),
            ("AUDIT", "Name the wrong branch and correction."),
            ("REBUILD", "Recreate the locked report after the delay.")),
    ];

    public static DrillInteractionProtocol Get(DrillId drill)
    {
        return Protocols.Single(protocol => protocol.Drill == drill);
    }

    private static DrillInteractionProtocol Protocol(
        DrillId drill,
        DrillInteractionInputKind inputKind,
        string attentionInstruction,
        string deviceInstruction,
        string actionInstruction,
        string screenBehavior,
        DrillInteractionAcknowledgement acknowledgement,
        params (string Label, string Instruction)[] steps)
    {
        return new DrillInteractionProtocol(
            drill,
            inputKind,
            attentionInstruction,
            deviceInstruction,
            actionInstruction,
            screenBehavior,
            acknowledgement,
            steps.Select(step => new DrillInteractionStep(step.Label, step.Instruction)).ToArray());
    }
}
