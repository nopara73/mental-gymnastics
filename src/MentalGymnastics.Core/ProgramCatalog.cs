namespace MentalGymnastics.Core;

public static class ProgramCatalog
{
    public static IReadOnlyList<BranchDefinition> Branches { get; } =
    [
        new(
            BranchCode.FH,
            "Focus Hold",
            BranchType.Foundational,
            "Trains holding a selected target without drifting into vague exposure.",
            "Prerequisite for every branch; maintenance branch after L2.",
            "Universal start."),
        new(
            BranchCode.FS,
            "Focus Shift and Recovery",
            BranchType.Foundational,
            "Trains deliberate switching and return instead of accidental attention drift.",
            "Parallel branch after FH L1; prerequisite for transfer and integration.",
            "FH L1 passed once."),
        new(
            BranchCode.WM,
            "Working Memory and Reconstruction",
            BranchType.Foundational,
            "Trains holding, manipulating, and reproducing information without external support.",
            "Parallel branch; prerequisite for Concept Operations and Transfer Integration.",
            "FH L1 passed once."),
        new(
            BranchCode.IR,
            "Inhibition and Response Control",
            BranchType.Foundational,
            "Trains withholding automatic responses and obeying task rules under pressure.",
            "Prerequisite for advanced branches; protects standards from cheating.",
            "FH L1 passed once."),
        new(
            BranchCode.DE,
            "Discrimination and Error Checking",
            BranchType.Foundational,
            "Trains detecting distinctions, comparing outputs, and finding errors.",
            "Parallel branch; prerequisite for Concept Operations and global reviews.",
            "FH L1 passed once."),
        new(
            BranchCode.CO,
            "Concept Operations",
            BranchType.Advanced,
            "Trains rule extraction, abstraction, mapping, and controlled inference.",
            "Advanced branch; depends on WM, IR, and DE.",
            "WM L3, IR L3, and DE L3 owned."),
        new(
            BranchCode.AI,
            "Affective Interference Control",
            BranchType.Advanced,
            "Trains preserving task standards under frustration, uncertainty, time pressure, or evaluative pressure.",
            "Advanced branch; depends on FH, FS, and IR; transfer branch for pressure.",
            "FH L3, FS L3, and IR L3 owned."),
        new(
            BranchCode.TI,
            "Transfer Integration",
            BranchType.Advanced,
            "Trains combined use of capacities in less protected contexts.",
            "Transfer branch and global integration branch.",
            "All foundational L3 owned; at least one CO L2 or AI L2 owned."),
    ];

    public static IReadOnlyList<BranchUnlockDefinition> BranchUnlocks { get; } =
    [
        Unlock(BranchCode.FH, [], [], []),
        Unlock(
            BranchCode.FS,
            [BranchCode.FH],
            [Requirement(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce)],
            []),
        Unlock(
            BranchCode.WM,
            [BranchCode.FH],
            [Requirement(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce)],
            []),
        Unlock(
            BranchCode.IR,
            [BranchCode.FH],
            [Requirement(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce)],
            []),
        Unlock(
            BranchCode.DE,
            [BranchCode.FH],
            [Requirement(BranchCode.FH, GlobalLevelId.L1, BranchLevelState.PassedOnce)],
            []),
        Unlock(
            BranchCode.CO,
            [BranchCode.WM, BranchCode.IR, BranchCode.DE],
            [
                Requirement(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Owned),
            ],
            []),
        Unlock(
            BranchCode.AI,
            [BranchCode.FH, BranchCode.FS, BranchCode.IR],
            [
                Requirement(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Owned),
            ],
            []),
        Unlock(
            BranchCode.TI,
            [BranchCode.FH, BranchCode.FS, BranchCode.WM, BranchCode.IR, BranchCode.DE, BranchCode.CO, BranchCode.AI],
            [
                Requirement(BranchCode.FH, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.FS, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.WM, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.IR, GlobalLevelId.L3, BranchLevelState.Owned),
                Requirement(BranchCode.DE, GlobalLevelId.L3, BranchLevelState.Owned),
            ],
            [
                new BranchLevelRequirementGroup(
                [
                    Requirement(BranchCode.CO, GlobalLevelId.L2, BranchLevelState.Owned),
                    Requirement(BranchCode.AI, GlobalLevelId.L2, BranchLevelState.Owned),
                ]),
            ]),
    ];

    public static IReadOnlyList<GlobalLevelDefinition> GlobalLevels { get; } =
    [
        new(
            GlobalLevelId.L1,
            "Protected Control",
            "The capacity appears in a simple, low-interference format.",
            "Clean execution in protected drill conditions."),
        new(
            GlobalLevelId.L2,
            "Duration and Density",
            "The capacity survives longer exposure, more repetitions, or shorter rests.",
            "Clean execution across repeated sets."),
        new(
            GlobalLevelId.L3,
            "Interference and Delay",
            "The capacity survives distraction, rule conflict, fatigue, or delayed recall.",
            "Clean execution after adjacent work or interruption."),
        new(
            GlobalLevelId.L4,
            "Transfer",
            "The capacity appears in a related but non-identical task.",
            "Transfer test passed and retested."),
        new(
            GlobalLevelId.L5,
            "Integration Under Pressure",
            "The capacity holds while multiple branches are active and margins narrow.",
            "Global review plus branch test under pressure."),
    ];

    public static IReadOnlyList<CapacityDefinition> Capacities { get; } =
    [
        new(
            CapacityId.SelectiveHold,
            "Selective hold",
            [BranchCode.FH],
            "Keep a selected object or operation in the foreground.",
            "Duration, distractors, precision of return.",
            "No switching target; drift must be marked.",
            "Timed hold with drift count and recovery time."),
        new(
            CapacityId.ReturnAfterDrift,
            "Return after drift",
            [BranchCode.FH, BranchCode.FS],
            "Notice loss of target and resume without resetting the session.",
            "Shorter recovery window, more interruptions.",
            "Drift must be marked immediately.",
            "Return-time standard after controlled interruption."),
        new(
            CapacityId.DeliberateSwitching,
            "Deliberate switching",
            [BranchCode.FS],
            "Move attention between specified targets on command.",
            "Switch count, rule complexity, interval density.",
            "Switches occur only on cue; no anticipatory switching.",
            "Alternating target test with sequence accuracy."),
        new(
            CapacityId.EncodingFidelity,
            "Encoding fidelity",
            [BranchCode.WM],
            "Take in information accurately enough to reconstruct it later.",
            "Item count, detail density, delay.",
            "No rereading after encode window.",
            "Delayed reconstruction and comparison."),
        new(
            CapacityId.ManipulationInMind,
            "Manipulation in mind",
            [BranchCode.WM],
            "Transform held information without externalizing the intermediate steps.",
            "Operation steps, reversals, interference.",
            "Intermediate notes prohibited unless drill allows them.",
            "Final reconstruction plus operation explanation."),
        new(
            CapacityId.ResponseInhibition,
            "Response inhibition",
            [BranchCode.IR],
            "Withhold a tempting or automatic response.",
            "Cue conflict, speed pressure, emotional pressure.",
            "Premature response fails the item.",
            "Go/no-go, stop, or rule-conflict test."),
        new(
            CapacityId.RuleFidelity,
            "Rule fidelity",
            [BranchCode.IR, BranchCode.CO],
            "Follow the assigned rule even when another rule is easier.",
            "Rule count, exceptions, switching.",
            "Rule must be stated before the set.",
            "Error count and explanation after set."),
        new(
            CapacityId.FineDiscrimination,
            "Fine discrimination",
            [BranchCode.DE],
            "Detect relevant differences and ignore irrelevant ones.",
            "Similarity, quantity, time limit.",
            "Guessing must be marked; unmarked guesses fail.",
            "Comparison task with false-positive and false-negative counts."),
        new(
            CapacityId.ErrorAudit,
            "Error audit",
            [BranchCode.DE],
            "Find errors in one's own output.",
            "Output length, delay, error subtlety.",
            "Original output cannot be changed during audit.",
            "Audit report compared with seeded or known errors."),
        new(
            CapacityId.RuleExtraction,
            "Rule extraction",
            [BranchCode.CO],
            "Infer the rule behind examples without overfitting.",
            "Ambiguity, negative examples, transfer distance.",
            "Must state rule before new examples.",
            "Rule tested on unseen examples."),
        new(
            CapacityId.AbstractionMapping,
            "Abstraction mapping",
            [BranchCode.CO],
            "Map a structure from one context to another.",
            "Distance between contexts, number of relations.",
            "Mapping must preserve relations, not surface terms.",
            "New-context application scored by relation accuracy."),
        new(
            CapacityId.PressureStableExecution,
            "Pressure-stable execution",
            [BranchCode.AI],
            "Preserve the same standard under frustration, uncertainty, or evaluation.",
            "Time limit, consequence simulation, ambiguity, observation.",
            "Standard cannot be lowered during pressure condition.",
            "Branch test repeated under defined pressure."),
        new(
            CapacityId.RecoveryAfterDisruption,
            "Recovery after disruption",
            [BranchCode.AI, BranchCode.TI],
            "Resume clean execution after interruption or mistake.",
            "Interruption timing, restart delay, task complexity.",
            "No full restart unless standard allows it.",
            "Post-disruption performance compared with pre-disruption baseline."),
        new(
            CapacityId.IntegratedTaskControl,
            "Integrated task control",
            [BranchCode.TI],
            "Combine multiple capacities without hiding a weak branch.",
            "Branch count, task length, transfer distance.",
            "Each component branch must leave evidence.",
            "Composite task with branch-specific scoring."),
    ];

    public static IReadOnlyList<DrillDefinition> Drills { get; } =
    [
        new(
            DrillId.FH1TargetHold,
            "FH-1",
            "Target Hold",
            "Build selective hold.",
            [CapacityId.SelectiveHold, CapacityId.ReturnAfterDrift],
            "Duration, target subtlety, distractor salience.",
            "Target is stated before set; every drift is marked.",
            "Target maintained within drift threshold; returns inside time window.",
            "Unmarked drift, target substitution, restarting after drift.",
            "Shorter duration with same marking rule."),
        new(
            DrillId.FH2DistractorHold,
            "FH-2",
            "Distractor Hold",
            "Hold while irrelevant prompts appear.",
            [CapacityId.SelectiveHold],
            "Distractor frequency and salience.",
            "Do not respond to distractor unless drill says so.",
            "Passing hold score while distractors are ignored.",
            "Responding to distractor, losing target, refusing to mark drift.",
            "Fewer distractors with same no-response rule."),
        new(
            DrillId.FS1CueSwitch,
            "FS-1",
            "Cue Switch",
            "Switch deliberately on cue.",
            [CapacityId.DeliberateSwitching],
            "Cue density, target count.",
            "Switch only on valid cue.",
            "Sequence accuracy meets threshold; wrong switches corrected.",
            "Anticipatory switch, missed cue, continuing wrong target.",
            "Longer cue intervals and two targets."),
        new(
            DrillId.FS2InvalidCueFilter,
            "FS-2",
            "Invalid Cue Filter",
            "Switch while ignoring invalid cues.",
            [CapacityId.DeliberateSwitching, CapacityId.ResponseInhibition],
            "Invalid cue ratio, rule contrast.",
            "Invalid cues must not trigger switch.",
            "Valid and invalid response thresholds both met.",
            "Treating all cues as valid, freezing, rule drift.",
            "Reduce invalid cue count."),
        new(
            DrillId.WM1DelayedReconstruction,
            "WM-1",
            "Delayed Reconstruction",
            "Encode, wait, reconstruct.",
            [CapacityId.EncodingFidelity],
            "Item count, detail density, delay.",
            "No rereading after encode window; no invented items.",
            "Reconstruction meets accuracy threshold.",
            "Invention, omission cluster, changing answer during comparison.",
            "Fewer items or shorter delay."),
        new(
            DrillId.WM2MentalTransform,
            "WM-2",
            "Mental Transform",
            "Transform held information.",
            [CapacityId.ManipulationInMind],
            "Operation steps, reversals, interference.",
            "Intermediate notes prohibited unless specified.",
            "Final output and rule explanation pass.",
            "Externalizing hidden steps, rule error, lost source item.",
            "Fewer steps with same no-note rule."),
        new(
            DrillId.IR1GoNoGoRule,
            "IR-1",
            "Go/No-Go Rule",
            "Withhold automatic response.",
            [CapacityId.ResponseInhibition],
            "Cue pace, no-go frequency.",
            "Premature response fails item.",
            "Accuracy and premature-response thresholds met.",
            "Early response, late response, post-error cascade.",
            "Slower pace with same no-go rule."),
        new(
            DrillId.IR2ExceptionRule,
            "IR-2",
            "Exception Rule",
            "Preserve rule fidelity.",
            [CapacityId.RuleFidelity],
            "Exception count, speed, similarity.",
            "Rule and exceptions stated before set.",
            "Errors within threshold; corrections timely.",
            "Changing rule mid-set, forgetting exception, unmarked drift.",
            "Fewer exceptions with same pre-stated rule."),
        new(
            DrillId.DE1PairDiscrimination,
            "DE-1",
            "Pair Discrimination",
            "Detect relevant differences.",
            [CapacityId.FineDiscrimination],
            "Similarity, item quantity, time limit.",
            "Guessing must be marked.",
            "False positives and false negatives within threshold.",
            "Unmarked guesses, overcorrecting, ignoring relevant feature.",
            "Larger differences or fewer items."),
        new(
            DrillId.DE2SeededAudit,
            "DE-2",
            "Seeded Audit",
            "Find known or seeded errors.",
            [CapacityId.ErrorAudit],
            "Error subtlety, output length, delay.",
            "Original output cannot be edited during audit.",
            "Finds required errors without excessive false corrections.",
            "Fixing during audit, missing critical error, inventing errors.",
            "Fewer seeded errors or shorter output."),
        new(
            DrillId.CO1RuleExtraction,
            "CO-1",
            "Rule Extraction",
            "Infer a rule from examples.",
            [CapacityId.RuleExtraction],
            "Ambiguity, example count, negative examples.",
            "Rule stated before unseen examples.",
            "Unseen classification meets threshold.",
            "Overfitting, rewriting rule after feedback, vague rule.",
            "Clearer examples with same unseen test."),
        new(
            DrillId.CO2StructureMapping,
            "CO-2",
            "Structure Mapping",
            "Map relations across domains.",
            [CapacityId.AbstractionMapping],
            "Relation count, domain distance.",
            "Relations must be named; surface matches do not count.",
            "Required relations preserved.",
            "Surface analogy, missing relation, unsupported inference.",
            "Fewer relations or nearer domain."),
        new(
            DrillId.AI1PressureRepeat,
            "AI-1",
            "Pressure Repeat",
            "Repeat owned standard under pressure.",
            [CapacityId.PressureStableExecution],
            "Time limit, observation, consequence simulation.",
            "Original standard cannot be lowered.",
            "Branch standard remains passing.",
            "Excusing errors as pressure, rushing, abandoning artifact.",
            "Milder pressure with same branch standard."),
        new(
            DrillId.AI2DisruptionRecovery,
            "AI-2",
            "Disruption Recovery",
            "Resume after interruption or mistake.",
            [CapacityId.RecoveryAfterDisruption],
            "Interruption timing, restart delay, task complexity.",
            "Full restart prohibited unless specified.",
            "Resume within window and finish above threshold.",
            "Restarting secretly, abandoning rule, post-disruption error cascade.",
            "Earlier disruption or simpler task."),
        new(
            DrillId.TI1CompositeTask,
            "TI-1",
            "Composite Task",
            "Combine branches in one task.",
            [CapacityId.IntegratedTaskControl],
            "Branch count, task length, transfer distance.",
            "Each branch must leave separate evidence.",
            "Each branch meets minimum passing quality.",
            "Strong branch hides weak branch, missing evidence, vague scoring.",
            "Reduce branch count with same evidence rule."),
        new(
            DrillId.TI2GlobalReviewTask,
            "TI-2",
            "Global Review Task",
            "Test integrated performance over review cycle.",
            [CapacityId.IntegratedTaskControl, CapacityId.ErrorAudit, CapacityId.EncodingFidelity],
            "Task length, pressure, ambiguity, delay.",
            "Audit and delayed reconstruction are required; no rereading after encode window.",
            "Composite, audit, and delayed reconstruction all pass.",
            "Good product with failed audit, memory gap, rule drift under pressure.",
            "Shorter composite with same audit and delay requirements."),
    ];

    public static IReadOnlyList<BranchLevelStandard> Standards { get; } =
    [
        Standard(BranchCode.FH, GlobalLevelId.L1, "Hold one simple target for 3 minutes.", "No more than 5 marked drifts; each return within 10 seconds; no target change.", "Pass once enters stabilization.", "Repeat twice within 14 days; one after a short WM set.", "Hold a different target type with same standard."),
        Standard(BranchCode.FH, GlobalLevelId.L2, "Hold for 6 minutes or two 3-minute sets with 60 seconds rest.", "No more than 7 total drifts; average return within 8 seconds.", "Own L1 required.", "Repeat on two days; one after light distraction.", "Hold during low-noise environment change."),
        Standard(BranchCode.FH, GlobalLevelId.L3, "Hold with controlled distractor.", "5 minutes with periodic irrelevant prompts; no response to distractor; no more than 5 drifts.", "Own L2 required.", "Repeat after FS practice.", "Hold while using unfamiliar but simple content."),
        Standard(BranchCode.FH, GlobalLevelId.L4, "Transfer hold to another branch task.", "Maintain stated target while completing WM or DE task; branch score remains passing.", "Own L3 required.", "Two transfer passes in different branches.", "Required transfer is the level test."),
        Standard(BranchCode.FH, GlobalLevelId.L5, "Hold under integrated pressure.", "Maintain target during 12-minute TI task; no branch-critical constraint breach.", "Own L4 and TI L3 required.", "Repeat during global review cycle.", "Global task with branch-specific scoring."),

        Standard(BranchCode.FS, GlobalLevelId.L1, "Alternate between two targets on cue for 4 minutes.", "At least 90% correct cue responses; no more than 3 anticipatory switches.", "FH L1 passed once.", "Repeat twice; one after FH hold.", "Use a new pair of targets."),
        Standard(BranchCode.FS, GlobalLevelId.L2, "Increase cue density.", "6 minutes; at least 92% correct; recovery after wrong switch within next cue.", "Own L1 required.", "Two passes on separate days.", "Switch between task types, not just objects."),
        Standard(BranchCode.FS, GlobalLevelId.L3, "Add rule conflict.", "At least 90% valid-cue accuracy; invalid cues must never trigger a switch.", "Own L2 and IR L2 required.", "Repeat after WM set.", "Transfer to DE comparison task."),
        Standard(BranchCode.FS, GlobalLevelId.L4, "Transfer switching to complex work.", "Alternate between two branch tasks without losing either task's passing standard.", "Own L3 required.", "Two different branch pairings.", "Required transfer is the level test."),
        Standard(BranchCode.FS, GlobalLevelId.L5, "Integrated switching under pressure.", "Maintain rule fidelity through 15-minute TI task with scheduled and unscheduled switches.", "Own L4 and TI L3 required.", "Repeat in global review.", "Global task with switch log."),

        Standard(BranchCode.WM, GlobalLevelId.L1, "Encode and reconstruct 5 simple items after 60 seconds.", "At least 4 of 5 exact; no invented items.", "FH L1 passed once.", "Repeat twice with new item sets.", "Use a different content type."),
        Standard(BranchCode.WM, GlobalLevelId.L2, "Increase item count or detail density.", "7 items or equivalent detail set after 90 seconds; at least 85% accurate.", "Own L1 required.", "Two passes; one after FH or FS.", "Reconstruct from visual and verbal formats."),
        Standard(BranchCode.WM, GlobalLevelId.L3, "Add manipulation and delay.", "Transform 6 held items by a stated rule after 2-minute delay; at least 80% accurate; rule explanation correct.", "Own L2 and IR L2 required.", "Repeat with different rule families.", "Apply memory operation inside DE task."),
        Standard(BranchCode.WM, GlobalLevelId.L4, "Transfer to untrained content.", "Reconstruct structure, not surface wording, after 5-minute delay in a new domain; at least passing rubric.", "Own L3 required.", "Two transfer contexts.", "Required transfer is the level test."),
        Standard(BranchCode.WM, GlobalLevelId.L5, "Integrated memory under pressure.", "Preserve critical information through 15-minute TI task; no critical omissions.", "Own L4 and TI L3 required.", "Repeat during global review.", "Global task with delayed reconstruction."),

        Standard(BranchCode.IR, GlobalLevelId.L1, "Follow a simple go/no-go rule.", "At least 90% correct; no more than 2 premature responses.", "FH L1 passed once.", "Repeat with different cue set.", "Use a non-identical cue format."),
        Standard(BranchCode.IR, GlobalLevelId.L2, "Add speed or exception pressure.", "At least 90% correct under fixed pace; all exceptions named before set.", "Own L1 required.", "Two passes; one after FS.", "Use rule conflict in WM or DE."),
        Standard(BranchCode.IR, GlobalLevelId.L3, "Withhold easier rule under conflict.", "At least 88% correct; no unmarked rule drift; correction within 2 items after error.", "Own L2 required.", "Repeat with different conflict rule.", "Transfer to CO example sorting."),
        Standard(BranchCode.IR, GlobalLevelId.L4, "Transfer inhibition to open task.", "Maintain stated rule while solving branch task; no critical rule breach.", "Own L3 required.", "Two open-task transfers.", "Required transfer is the level test."),
        Standard(BranchCode.IR, GlobalLevelId.L5, "Inhibition under integrated pressure.", "Preserve rule fidelity through TI task with interruption and time pressure; no critical breach.", "Own L4 and TI L3 required.", "Repeat during global review.", "Global task with rule audit."),

        Standard(BranchCode.DE, GlobalLevelId.L1, "Compare simple pairs for relevant differences.", "At least 90% accuracy; guesses marked; no more than 2 unmarked guesses.", "FH L1 passed once.", "Repeat with new pair set.", "Use another item type."),
        Standard(BranchCode.DE, GlobalLevelId.L2, "Increase similarity and quantity.", "At least 88% accuracy across 20 comparisons; false positives and false negatives both below threshold.", "Own L1 required.", "Two passes; one after WM.", "Compare outputs produced by self."),
        Standard(BranchCode.DE, GlobalLevelId.L3, "Audit delayed output.", "Find at least 80% of seeded errors after 5-minute delay; no more than 2 false corrections.", "Own L2 and WM L2 required.", "Repeat with different error types.", "Audit WM or CO artifact."),
        Standard(BranchCode.DE, GlobalLevelId.L4, "Transfer audit to unstructured output.", "Identify critical errors and uncertainty in a short self-produced artifact.", "Own L3 required.", "Two transfer artifacts.", "Required transfer is the level test."),
        Standard(BranchCode.DE, GlobalLevelId.L5, "Integrated audit under pressure.", "Audit TI artifact under time limit; find all critical errors and at least 80% noncritical errors.", "Own L4 and TI L3 required.", "Repeat during global review.", "Global task audit."),

        Standard(BranchCode.CO, GlobalLevelId.L1, "Extract rule from clear examples.", "State a testable rule; classify unseen examples at least 85% correctly.", "WM L3, IR L3, DE L3 owned.", "Repeat with two rule families.", "Use a different content domain."),
        Standard(BranchCode.CO, GlobalLevelId.L2, "Add exceptions and negative examples.", "Rule handles exceptions without changing after feedback; unseen classification at least 82%.", "Own L1 required.", "Two passes; one after DE audit.", "Explain failed examples without rewriting history."),
        Standard(BranchCode.CO, GlobalLevelId.L3, "Map structure across domains.", "Preserve at least 80% of required relations; no surface-only mapping accepted.", "Own L2 required.", "Repeat with two distant domains.", "Apply mapping to a new problem."),
        Standard(BranchCode.CO, GlobalLevelId.L4, "Transfer abstraction to open problem.", "Produce a rule or model that predicts unseen cases and survives audit.", "Own L3 and DE L4 required.", "Two open-problem transfers.", "Required transfer is the level test."),
        Standard(BranchCode.CO, GlobalLevelId.L5, "Concept operation under pressure.", "Build and audit a model inside TI task; critical assumptions named; prediction test passed.", "Own L4 and TI L3 required.", "Repeat during global review.", "Global task model audit."),

        Standard(BranchCode.AI, GlobalLevelId.L1, "Repeat an owned foundational standard under mild time pressure.", "Original branch standard remains passing; no critical constraint breach.", "FH L3, FS L3, IR L3 owned.", "Repeat with two foundational branches.", "Use a different pressure source."),
        Standard(BranchCode.AI, GlobalLevelId.L2, "Add uncertainty or evaluative pressure.", "Standard remains passing; uncertainty is marked rather than hidden.", "Own L1 required.", "Two passes; one after a failed practice item.", "Transfer to WM or DE."),
        Standard(BranchCode.AI, GlobalLevelId.L3, "Recover after controlled disruption.", "Resume within defined window and finish above branch pass threshold.", "Own L2 required.", "Repeat with different disruption timing.", "Transfer to CO or TI pretask."),
        Standard(BranchCode.AI, GlobalLevelId.L4, "Transfer pressure control to open task.", "Complete open branch task without lowering stated standard.", "Own L3 and relevant branch L4 required.", "Two open-task transfers.", "Required transfer is the level test."),
        Standard(BranchCode.AI, GlobalLevelId.L5, "Integrated pressure tolerance.", "Complete TI pressure task; each component branch remains at passing quality.", "Own L4 and TI L3 required.", "Repeat during global review.", "Global pressure task."),

        Standard(BranchCode.TI, GlobalLevelId.L1, "Combine two foundational branches.", "Both branch standards remain passing in one task; evidence separated by branch.", "All foundational L3 owned; CO L2 or AI L2 owned.", "Repeat with two branch pairings.", "New task format with same pair."),
        Standard(BranchCode.TI, GlobalLevelId.L2, "Combine three branches with delay.", "All component branches meet minimum passing quality; delayed artifact included.", "Own L1 required.", "Two passes; one after maintenance work.", "New content domain."),
        Standard(BranchCode.TI, GlobalLevelId.L3, "Add advanced branch.", "Foundational branch scores remain passing while CO or AI demand is active.", "Own L2 and advanced branch L2 owned.", "Repeat with different advanced branch or pressure source.", "New task family."),
        Standard(BranchCode.TI, GlobalLevelId.L4, "Transfer to untrained but related problem.", "Branch evidence remains visible; no bottleneck branch falls below pass.", "Own L3 required.", "Two far-transfer tasks.", "Required transfer is the level test."),
        Standard(BranchCode.TI, GlobalLevelId.L5, "Global performance review task.", "Multi-branch task passes, audit passes, delayed reconstruction passes, and pressure rule remains intact.", "Own L4, CO L4, AI L4 required.", "Repeat in separate global review cycles.", "Global review itself."),
    ];

    private static BranchLevelStandard Standard(
        BranchCode branch,
        GlobalLevelId level,
        string demand,
        string standard,
        string gate,
        string stabilization,
        string transfer)
    {
        return new BranchLevelStandard(branch, level, demand, standard, gate, stabilization, transfer);
    }

    private static BranchUnlockDefinition Unlock(
        BranchCode branch,
        IReadOnlyList<BranchCode> prerequisiteBranches,
        IReadOnlyList<BranchLevelRequirement> requiredLevels,
        IReadOnlyList<BranchLevelRequirementGroup> anyOfLevelGroups)
    {
        return new BranchUnlockDefinition(branch, prerequisiteBranches, requiredLevels, anyOfLevelGroups);
    }

    private static BranchLevelRequirement Requirement(
        BranchCode branch,
        GlobalLevelId level,
        BranchLevelState requiredState)
    {
        return new BranchLevelRequirement(branch, level, requiredState);
    }
}
