# MentalGymnastics Android Visual State Language

**Status:** Visual design reference

**Applies to:** Future Android branch maps, work queues, session cards, evidence timelines, completion summaries, and review screens

**Depends on:** [Android UI Strategy](android-ui-strategy.md), [Complete Training Program](program/training-program.md), [Core Library](core-library.md), [Local Persistence Boundary](local-persistence-boundary.md), [Session Runtime Boundary](session-runtime-boundary.md), [Generated Content Boundary](generated-content-boundary.md), [Pre-UI App Integration Boundary](app-integration-boundary.md)

## Purpose

This document defines the visual vocabulary for Android training state. It is not UI implementation, a component spec, or a new state machine.

The visual language must make program truth visible with minimal text. A practitioner should be able to scan the map and see what is unopened, active, ready, partially passed, stabilizing, owned, under maintenance, decayed, blocked, in recovery, transferring, or under review. The same visual grammar should work across the branch ladder, prescribed work queue, session preflight, post-session outcome, evidence history, and global review.

## Source Of Truth

Visual states must be derived from app integration read models and lower-layer facts:

- Branch-level lifecycle states come from Core and persisted branch-level records: `Unopened`, `Training`, `TestReady`, `PassedOnce`, `Stabilizing`, `Owned`, `Maintenance`, and `Decayed`.
- Blocked, recovery, transfer, and review treatments are overlays or session/review roles from Core/App decisions, not replacement branch-level states.
- Runtime lifecycle states may affect live-session surfaces, but they must not visually imply advancement, ownership, maintenance currency, or transfer validity.
- Persistence summaries are display aids only. If a summary conflicts with current Core/App decisions, the visual treatment follows the current Core/App decision.

Android must never infer these states from screen completion, elapsed time, button taps, or UI-local flags.

## Visual Channels

Use multiple channels for every important state:

- **Shape:** node fill, outline, segmentation, fracture, rail, gate, or panel form.
- **Position:** where the item sits in the ladder, queue, edge, review board, or evidence timeline.
- **Icon:** a small familiar symbol, preferably from the Android icon set or a consistent icon library.
- **Density:** line weight, hatch, tick marks, segment count, or evidence-strip density.
- **Motion:** restrained transitions for state changes, never celebration or engagement.
- **Contrast:** tonal contrast and surface elevation, never color alone.
- **Hierarchy:** size, row priority, blocked-edge placement, and top-of-queue placement.

Color may reinforce meaning, but it must never be the only carrier of meaning.

## Base Objects

### Branch-Level Node

The branch-level node represents one branch at one global level. Its fill, outline, segmentation, and overlay markers show lifecycle state. The node should be compact and stable in size across all states so changing state does not reflow the ladder.

Visible label: branch code and level only, such as `FH L2`.

### Gate Edge

The gate edge connects a current node to a possible next node. It shows unlock, readiness, blocked advancement, transfer requirement, or review requirement. Blocked advancement belongs on the edge, not only inside a detail panel.

Visible label: none by default; terse blocker on expansion, such as `WM due` or `Stabilize 2/3`.

### Work Item

A work item represents an app-integration prescribed action. Its leading icon shows session role, the node chip shows branch/level, and its trailing marker shows ready, blocked, due, recovery, transfer, or review.

Visible label: session role plus branch/level, such as `Practice FH L1`, `Transfer WM L4`, or `Review`.

### Evidence Marker

An evidence marker represents a recorded attempt, pass, failure, maintenance check, transfer artifact, recovery session, or review. It should be small, chronological, and filterable.

Visible label: date or branch code only until expanded.

## State Precedence

When multiple states apply to the same branch-level or work item, visual priority is:

1. **Decayed**
2. **Blocked**
3. **Recovery or deload**
4. **Review required**
5. **Transfer required or active**
6. **Maintenance due or warning**
7. **Stabilizing**
8. **Passed once**
9. **Test-ready**
10. **Training**
11. **Owned**
12. **Unopened**

Higher-priority states may overlay lower-priority states, but they must not erase the underlying lifecycle state. For example, an owned prerequisite that is decayed should still read as a formerly owned level now invalid for dependent advancement.

## State Treatments

### Unopened

Meaning: prerequisites are missing; the branch-level cannot train except as exposure during assessment.

Treatment:

- Shape: hollow node with a thin outline and an internal lock cutout.
- Position: present in the ladder but visually behind the current path.
- Icon: lock.
- Density: empty interior, no evidence ticks.
- Motion: none except a short unlock transition when Core/App opens it.
- Contrast: low contrast but still readable.
- Hierarchy: below prescribed work and below current branch states.

Minimal label: `FH L3`.

### Training

Meaning: the level is being practiced below or near limit; no advancement has happened.

Treatment:

- Shape: open ring with a small gap at the forward edge.
- Position: current lane position for active practice.
- Icon: repeat or drill marker on work cards; no icon needed inside dense ladder nodes.
- Density: one to two light evidence ticks may sit below the node when recent practice exists.
- Motion: subtle ring progress when new practice evidence is recorded, then still.
- Contrast: normal contrast.
- Hierarchy: primary only when it is current prescribed work.

Minimal label: `WM L1`.

### Test-Ready

Meaning: recent practice, prerequisites, maintenance, stated standard, and named honesty constraint make a formal test eligible.

Treatment:

- Shape: open ring with a squared gate notch at the forward edge.
- Position: node remains in the branch lane; the outgoing edge to the test action becomes prominent.
- Icon: clipboard-check or target marker near the edge.
- Density: two recent clean-practice ticks under the node.
- Motion: a single gate-open transition when readiness first appears; no pulsing.
- Contrast: higher than training, lower than owned.
- Hierarchy: appears in the work queue only when the app layer exposes a test action.

Minimal label: `Test`.

### Passed Once

Meaning: one formal test passed; ownership is not granted and the next level remains locked.

Treatment:

- Shape: half-filled node with a hollow center and locked outgoing edge.
- Position: stays at the current level; do not visually move the practitioner to the next level.
- Icon: single check mark outside the node, not inside the owned fill area.
- Density: one formal-evidence tick only.
- Motion: brief fill to half, then immediate reveal of stabilization requirement.
- Contrast: stronger than test-ready but clearly weaker than owned.
- Hierarchy: paired with stabilization work, not with unlock language.

Minimal label: `1/3`.

Required distinction: passed once must never use the same filled node, check placement, edge unlock, or map hierarchy as owned.

### Stabilizing

Meaning: additional clean passes are being collected under ordinary variance, time, mild fatigue, distraction, or adjacent work.

Treatment:

- Shape: segmented ring with three pass slots; filled segments show clean passes.
- Position: current level remains active; next-level edge stays closed until ownership.
- Icon: layered check or repeat-check marker.
- Density: segment count is the primary information; optional tiny date ticks open on demand.
- Motion: one segment fills after each valid stabilization pass.
- Contrast: high enough to show incomplete work; lower finality than owned.
- Hierarchy: should appear before new-level work in the prescribed queue.

Minimal label: `2/3` or `Stab`.

### Owned

Meaning: the standard is repeatable under ownership rules; next level may unlock only if prerequisites and global balance allow.

Treatment:

- Shape: fully filled node with a stable outline.
- Position: anchors the completed portion of the branch lane.
- Icon: check inside the node only for owned.
- Density: no hatch; clean solid interior.
- Motion: restrained settle transition after ownership is granted.
- Contrast: stable medium-high contrast, not celebratory.
- Hierarchy: lower than active blockers, maintenance warnings, and current work.

Minimal label: branch/level only; the filled form carries ownership.

### Maintenance

Meaning: an owned level receives lower-volume exposure to prevent decay, or maintenance is due/current.

Treatment:

- Shape: owned node with a small outer service ring or corner tick.
- Position: attached to the owned node and repeated in the work queue when due.
- Icon: wrench, refresh, or calendar-check marker.
- Density: due state adds one outer tick; warning adds hatch to the ring.
- Motion: none for current; a small ring reveal when maintenance becomes due.
- Contrast: current maintenance is secondary; due or warning maintenance rises in hierarchy.
- Hierarchy: due maintenance outranks optional new work.

Minimal label: `Due`, `Warn`, or no label when current.

### Decayed

Meaning: maintenance failure or prerequisite decay caps dependent advancement until restoration.

Treatment:

- Shape: formerly owned node appears fractured with a diagonal break and heavy outline.
- Position: the affected branch lane is interrupted; dependent outgoing edges show caps.
- Icon: alert-triangle or broken-link marker.
- Density: diagonal hatch across the node and dependent edges.
- Motion: immediate snap to decayed state with a short shake only at transition; respect reduced motion by using instant contrast change.
- Contrast: highest non-modal contrast in the map.
- Hierarchy: top of work queue and review board until restoration path is shown.

Minimal label: `Decayed`.

Required emphasis: decay must be impossible to miss. It should appear on the node, on dependent gate edges, and in the prescribed work queue when it blocks advancement.

### Blocked

Meaning: advancement or work is unavailable because of a specific rule: missing prerequisite, overdue maintenance, decay, stabilization gap, transfer gap, failed global review, dependency cap, recovery, or deload.

Treatment:

- Shape: thick barrier across the gate edge or work action; do not hide it inside the node.
- Position: on the edge between current state and desired next state, plus top-level summary when it affects next work.
- Icon: stop, ban, or lock-alert marker.
- Density: dense crossbar or double-stroke line.
- Motion: none by default; a brief edge closure when a new block appears.
- Contrast: highest hierarchy alongside decay.
- Hierarchy: blocks replace unavailable action buttons with the reason and restoration route.

Minimal label: `Blocked`.

Required emphasis: blocked advancement must be impossible to miss. A blocked next level may remain visible, but the path to it must be visibly interrupted.

### Recovery

Meaning: reduced-load work is prescribed to protect clean execution. Recovery is not advancement and cannot test.

Treatment:

- Shape: soft-edged work item with a downward load step marker; branch node remains in its underlying lifecycle state.
- Position: appears in the work queue before load, test, or advancement work.
- Icon: arrow-down-right, shield, or low-load marker.
- Density: reduced density: fewer ticks, thinner interior fill, one required evidence slot.
- Motion: slow fade into lower-load treatment; no celebratory completion motion.
- Contrast: moderate contrast, but priority above normal training when prescribed.
- Hierarchy: clear enough to prevent accidental testing.

Minimal label: `Recovery`.

### Transfer

Meaning: a capacity is being tested in a related but non-identical task while the source standard remains visible.

Treatment:

- Shape: two-node bridge from source branch node to changed-context task node.
- Position: spans source branch and target/context area; never appears as a standalone novelty tile.
- Icon: split-arrow or bridge marker.
- Density: source-side standard marker plus target-side evidence marker; TI transfer may show separate component ticks.
- Motion: bridge draw from source to changed context when a transfer task is prepared.
- Contrast: source standard and changed context must both remain visible.
- Hierarchy: transfer requirement outranks optional new-content work.

Minimal label: `Transfer`.

Required distinction: transfer visuals must show both preserved demand and changed context. New content without the source-standard bridge is not transfer.

### Review

Meaning: whole-practitioner review is due, active, passed, failed, or producing a programming decision.

Treatment:

- Shape: board-level frame around branch lanes, not a branch-level node.
- Position: separate review surface plus a compact top-level marker on Home.
- Icon: scan, checklist, or grid marker.
- Density: dense summary strip: branch states, maintenance status, last failures, bottleneck, recovery/deload, transfer/stabilization currency.
- Motion: none for due state; review result may reorder summary rows by programmed priority.
- Contrast: high when review blocks classification or advancement; normal when it only schedules emphasis.
- Hierarchy: review failure that blocks advanced classification must outrank ordinary owned progress.

Minimal label: `Review`.

## Session Role Icons

Use the same role icons wherever a session appears:

| Session role | Icon direction | Visual cue |
| --- | --- | --- |
| Practice | repeat or drill | open ring |
| Load | step-up | thicker forward edge |
| Test | target or clipboard-check | gate notch |
| Stabilization | repeat-check | segmented ring |
| Regression | step-down | lower-load marker |
| Transfer | bridge or split-arrow | source-to-context edge |
| Maintenance | wrench or refresh | service ring |
| Recovery | shield or arrow-down-right | reduced-density card |
| Review | grid or checklist | board frame |

Icon labels should be available to accessibility services, but visible text should stay terse.

## Motion Rules

Motion should clarify state change, not create engagement.

Allowed:

- Opening a gate when test readiness appears.
- Filling one stabilization segment after a valid pass.
- Drawing a transfer bridge from source standard to changed context.
- Snapping to a decayed or blocked treatment when a rule blocks advancement.
- Reordering review rows by programmed priority after review completes.

Not allowed:

- Celebration animations for completion.
- Pulsing to drive engagement.
- Streak-style loops.
- Motion that hides failure, decay, or blocked advancement.
- Motion that implies ownership before Core/App grants ownership.

Reduced-motion mode should replace transitions with instant state changes and stronger static contrast.

## Contrast And Hierarchy

Hierarchy should communicate programming priority:

- Decayed and blocked states sit highest.
- Recovery, deload, overdue maintenance, and failed review sit above normal work.
- Stabilization sits above new-level work.
- Test-ready sits above ordinary training only when the app layer exposes a test action.
- Owned is stable background context unless it is due for maintenance or used as transfer source evidence.
- Unopened remains visible but quiet.

Use elevation and line weight sparingly. Dense hatching and heavy barriers are reserved for decayed and blocked states so they remain unmistakable.

## Minimal Label Rules

Default visible labels should be short:

- Branch-level nodes: `FH L1`, `WM L3`.
- Stabilization: `1/3`, `2/3`.
- Maintenance: `Due`, `Warn`, or no label when current.
- Decay: `Decayed`.
- Blocked edge: `Blocked`.
- Work cards: `Practice FH L1`, `Test DE L2`, `Transfer IR L4`, `Review`.

Reveal on demand should expose the exact reason, standard, honesty constraint, evidence summary, prerequisite chain, transfer source standard, recovery rule, or review failure input.

## Accessibility

Every state marker needs a full nonvisual label:

- Branch and level.
- Lifecycle state.
- Overlay state, if any.
- Whether advancement is allowed.
- Blocker or restoration route when blocked or decayed.
- Evidence count for passed once or stabilizing.

Example spoken label: `Working Memory level 2, stabilizing, two of three clean passes complete, next level locked until ownership`.

Accessibility adaptations may change presentation or input mechanics, but they must not change the trained demand unless Core/App exposes a valid regression or alternate workflow.

## Alignment Checklist

Before implementing any Android component using this language:

1. Confirm the visual state comes from app integration read models or direct Runtime state exposed through app integration.
2. Confirm branch-level lifecycle states are not inferred in the screen.
3. Confirm passed once is visually distinct from owned.
4. Confirm decay appears on the node, dependent edges, and prescribed work surface.
5. Confirm blocked advancement appears on the gate edge, not only in detail text.
6. Confirm transfer shows source standard plus changed context.
7. Confirm recovery cannot be mistaken for advancement or a test.
8. Confirm review evaluates the whole practitioner, not a single branch card.
9. Confirm color is never the only state signal.
10. Confirm visible labels are minimal and details are reveal-on-demand.
