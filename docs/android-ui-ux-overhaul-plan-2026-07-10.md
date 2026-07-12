# Android UI/UX Overhaul Plan - 2026-07-10

**Status:** Approved implementation blueprint after full UX audit  
**Scope:** Entire Android product, App presentation boundary, and missing Core/Runtime behavior required for an honest user experience  
**Objective:** Build a clear, visual, low-word-count training instrument that makes the current demand, evidence, result, and next action obvious without weakening standards or progression rules.

> Historical note: the Target Hold return-timing interaction in this blueprint was superseded by `docs/android-practice-ergonomics-correction-2026-07-12.md`. Current FH work uses one standalone wander tap and no return timer or second action.

## Foundation

This plan is governed by:

- `docs/foundation/progression-against-vibes.md`
- `docs/foundation/standards-based-skill-ladder.md`
- `docs/program/training-program.md`
- `docs/android-ui-operating-principle.md`
- `docs/android-ui-layer.md`
- `docs/app-integration-boundary.md`
- `docs/session-runtime-boundary.md`
- `docs/generated-content-boundary.md`
- `docs/local-persistence-boundary.md`

The core UX interpretation is:

> The path carries part of the burden.

The practitioner should not have to decode internal architecture, infer what counts, remember what to do next, or negotiate whether a result changed their training state. The app must present the smallest honest interaction that produces the evidence required by the program.

## Audit Conclusions

The existing app has a serious domain architecture but does not yet provide a coherent product experience.

The highest-severity findings are:

1. Target Hold promises return timing and target-change evidence that the live App path cannot collect.
2. Completed live sessions are persisted with `CleanPerformance = false`, even when the visible standard is met.
3. Normal prescribed work hides Map, Record, Review, and Local Data, trapping the practitioner in one funnel.
4. Map, Evidence, and Review are mostly static reports rather than navigable workflows.
5. Other drills fall back to generic instructions and truncated material.
6. Live UI is destroyed and rebuilt every second, producing partial frames and breaking future text input.
7. First-run screens contain false affordances, duplicated actions, excessive containers, and evidence wording the app cannot support.
8. Accessibility targets, typography, navigation, and responsive behavior are below production quality.
9. Existing screenshot and contract tests prove text presence and read-model facts, not human comprehension or usable UI behavior.

The redesign must repair these problems rather than paint over them.

## Product Contract

Every user-visible training flow must answer five questions in order:

1. **Now:** What does the program want me to do?
2. **Demand:** What exact capacity and constraint am I practicing?
3. **Action:** What do I do at this moment?
4. **Evidence:** What did the app actually observe or ask me to mark?
5. **Next:** What changed, and what should I do after this?

The interface must never claim to record a fact that no Runtime/App input can produce.

The interface must never imply that a completed session was clean, passed, stabilized, owned, maintained, restored, or transferred unless Core evaluation says so.

The interface must never expose a standard without providing the controls needed to perform and record that standard.

## Experience Principles

### 1. Action Before Explanation

The primary action and current demand are visible before supporting rationale. Rationale is one tap away and never blocks ordinary practice unless it protects a standard.

### 2. Visual State Before Status Prose

Use position, state shape, progress segments, barriers, target material, timers, and evidence marks before explanatory paragraphs.

### 3. Exact Without Being Verbose

Standards remain exact, but numerical conditions become compact visual criteria. Full wording remains available in an expandable detail surface.

### 4. One Primary Action

Each screen has one visually dominant action. Rows that look tappable must be tappable. Decorative objects must not resemble buttons.

### 5. Honest Evidence

The live interaction mirrors the evidence model. For Target Hold this means separate actions for noticing a wander and completing the return, plus a way to record a target change.

### 6. Failure Is Specific, Not Punitive

Failure screens show the failed condition, the evidence behind it, and the programmed response. Declining before active work begins is cancellation, not a failed attempt.

### 7. Inspection Is Always Available

Prescribed work remains dominant, but Map, Record, Review, and local utility access are not hidden during normal operation.

### 8. Progressive Disclosure

The default view contains only information needed for the next decision. Exact standards, history, source evidence, and rationale expand in place or open a detail screen.

### 9. Stable Live Surfaces

Timer updates change only the timer and runtime-dependent fields. They do not recreate the screen, move controls, reset input, close the keyboard, or flash blank content.

### 10. Accessibility Is Part Of Correctness

Minimum 48dp touch targets, minimum 12sp supporting text, non-color state channels, meaningful spoken labels, large-text support, predictable back behavior, and stable motor targets are release requirements.

## Information Architecture

### Primary Destinations

The persistent bottom navigation contains four destinations:

- **Train:** prescribed work, active resume, blocker, maintenance, recovery, or review action.
- **Map:** branch relationships, level states, gates, maintenance, decay, stabilization, and transfer.
- **Record:** evidence timeline and inspectable artifacts.
- **Review:** whole-practitioner state and the current programmed decision.

Local Data moves behind a utility/settings icon in the top app bar. It is not a training destination.

Bottom navigation remains available on Train, Map, Record, and Review. It is hidden only during Preflight, Live Session, and Result, where a standard back or close affordance remains visible.

### Screen Inventory

Primary screens:

- Train
- Map
- Record
- Review
- Local Data

Workflow and detail screens:

- Branch Detail
- Blocker Detail
- Maintenance / Restoration Detail
- Evidence Detail
- Review Input Detail
- Session Preflight
- Live Session
- Session Result
- Active Session Recovery

Every detail screen participates in a real back stack. Android system Back and the visible back button produce the same result.

## Core User Journey

```text
Train
  -> Preflight
      -> Live
          -> Result
              -> next programmed action

Train -> Map -> Branch Detail -> eligible work or blocker route
Train -> Record -> Evidence Detail -> related branch/result
Train -> Review -> programmed response -> relevant branch/work
Train -> Local Data -> validate/export/restore
```

## Screen Specifications

### Train

**Primary purpose:** Show the highest-priority current command.

**First viewport:**

- Screen title: `Train` or `Today`.
- Current role: practice, load, test, stabilize, maintain, restore, recover, review, or resume.
- Exercise name and human branch name.
- One-line demand.
- Compact criteria strip.
- One primary action.
- Small current-branch position indicator.

**Target Hold example:**

- `Target Hold`
- `Focus Hold - Level 1`
- `Keep one target in mind. Mark every wander. Return cleanly.`
- Criteria: `3:00`, `<=5 wanders`, `<=10s return`, `same target`
- Primary action: `Set up hold`

**Secondary interaction:** `Why this exercise?` expands one short purpose line and one next-load line. It does not create a separate card full of curriculum prose.

**Must not:**

- Hide primary navigation.
- Use a decorative play control that is not tappable.
- Repeat the primary action in multiple visual objects.
- Dump the future curriculum onto the first screen.

### Session Preflight

**Primary purpose:** Establish the exact attempt contract and prepare generated material.

**Layout:**

- Standard top app bar with back.
- Exercise and branch identity.
- Dominant generated material preview.
- Three-step interaction preview.
- Compact standard strip.
- Expandable `Full standard` detail.
- Sticky bottom primary action.

**Target Hold interaction preview:**

1. Hold the displayed target.
2. Tap `Attention wandered` when attention leaves.
3. Tap `Back on target` after returning.

Target-change recording is explained in the secondary action menu.

**Target rendering:** Known simple targets such as colored dots, circles, squares, and lines render as the actual visual shape with an accessible text label. Text remains the fallback for unsupported material.

**Cancellation:** Back or `Cancel` before active work returns to Train without creating failed or abandoned evidence.

**Must not:**

- Put the start action below an unmarked scrolling wall.
- Repeat headings such as `When it counts` and `This counts when`.
- Claim to save unavailable evidence.
- Count preflight time as active training time.

### Live Target Hold

**Primary purpose:** Keep the target and the current valid action unmistakable.

**Stable layout:**

- Remaining session time.
- Large visual target.
- Current interaction state.
- One dominant evidence action.
- Small overflow action for target change and stop.

**States:**

- **Holding:** primary action `Attention wandered`.
- **Returning:** primary action `Back on target`; show the 10-second return window.
- **Late return:** continue the set, record the late return, and show the fact without moralizing.
- **Target changed:** record the failed constraint and keep the state visible.
- **Paused/resumed:** only when Runtime allows it.
- **Terminal:** transition once to Result.

The target, timer, and action controls stay in fixed positions across states.

**Must not:**

- Show implementation counters such as `Saved 1/1`.
- Rebuild the entire view every second.
- Treat timer completion as clean performance without Core evaluation.
- Hide the return action required by the visible standard.

### Result

**Primary purpose:** Show the evaluated truth and the next programmed action.

**First viewport:**

- Outcome: cancelled, stopped, incomplete, clean practice, failed standard, pass once, stabilizing, owned, maintained, warning, decayed, restored, transfer passed/failed, recovery, or blocked.
- Short evidence strip.
- State change, if any.
- One direct next action.

**Target Hold evidence strip:**

- Completed duration.
- Wander count.
- Returns recorded.
- Late returns.
- Target changes.

**Practice wording:** A clean practice set is recorded as clean practice, not advancement. The result may show readiness accumulation such as `1 of 2 clean practices before a test` when App/Core data supports it.

**Must not:**

- Repeat the same outcome across several panels.
- Show a decorative `Next step` row that cannot be tapped.
- Use `Back to Today` when the actual next action can be named and invoked directly.
- Hide the evidence that produced the result.

### Map

**Primary purpose:** Explain the program structure and the practitioner's current position visually.

**Default view:**

- Foundational branches first.
- Advanced branches separated behind visible prerequisite gates.
- Human names beside branch codes.
- Current relevant level emphasized.
- Quiet future levels, not forty equally weighted boxes.
- Blocked gates on the edge they affect.
- Maintenance, decay, and stabilization on their owning nodes.

Every branch row and relevant level node is selectable.

**Branch Detail shows:**

- What the branch trains.
- Current level and state.
- Current standard.
- Recent evidence.
- Prerequisite or blocker.
- Allowed next action.

**Must not:**

- Use unexplained codes as the only labels.
- Choose `L5 Locked` as the representative state for an unopened branch.
- Render blocked, decayed, passed-once, and owned states as minor color variations.
- Show a noninteractive diagram.

### Record

**Primary purpose:** Make evidence inspectable without becoming analytics.

**Default view:**

- Chronological timeline.
- Filter control for branch, result, and session role.
- Compact entry: date, exercise, outcome, decisive evidence.
- Failure, abandonment, timeout, clean practice, formal pass, maintenance, and transfer remain distinct.

Every entry opens Evidence Detail.

**Evidence Detail shows:**

- Standard attempted.
- Observable measurements.
- Critical constraints.
- Core evaluation.
- State change.
- Related next action.

**Must not:**

- Repeat meta-copy such as `failure was not softened`.
- Duplicate the latest record in several summary rows.
- Use total time, streaks, points, or engagement metrics.

### Review

**Primary purpose:** Present the whole-practitioner decision and route the response.

**Default view:**

- Current review status.
- Current programmed decision in plain language.
- Compact branch board.
- Bottleneck and maintenance state.
- One primary action that follows the decision.

Examples:

- `Build Focus Hold first` -> `Open Focus Hold`
- `Restore Working Memory` -> `Set up restoration`
- `Review not due` -> no destructive or blocking presentation

**Must not:**

- Present ordinary missing beginner data as a red global failure.
- Expose phrases such as `whole-practitioner input missing` without translation.
- Show a recommendation without a route to act on it.

### Local Data

**Primary purpose:** Provide clear local backup and integrity operations.

**Layout:**

- Current data integrity.
- Latest backup date/file, if one exists.
- Export and validate actions.
- Restore action enabled only when a valid backup exists.
- Explicit replacement confirmation with consequences.

**Must not:**

- Enable backup validation or restore when no backup exists.
- Present four equal primary actions.
- expose full paths before requested.

## Runtime And Core Repair

The first implementation phase repairs Target Hold before visual redesign claims it works.

### Runtime Commands

Add Runtime-owned commands for:

- Mark wander.
- Record return for the matching wander.
- Record target substitution.

Runtime validates command availability, open-wander identity, return timing, target-change facts, active phase, lifecycle, and snapshot restoration.

### Runtime Evidence

Runtime completion for Target Hold must expose:

- Active duration.
- Marked wander count.
- Return count.
- Unreturned wander count.
- Maximum return time.
- Late return count.
- Target-substitution count.
- Target-stated-before-set fact.
- Completion/abandonment status.

These facts survive snapshot/restore.

### Core Standard

Core owns an executable FH L1 standard tied to the program document:

- Duration at least 180 seconds.
- Marked wanders at most 5.
- Every marked wander has a recorded return.
- Every return is within 10 seconds.
- No target substitution.
- Required output/evidence is complete.

Runtime maps facts into Core evaluation input. Core returns pass/fail. App uses that result when creating persistence records and result presentation.

### Practice Credit

A clean practice session:

- Is persisted as `CleanPerformance = true` only when Core standard evaluation passes.
- Contributes to test readiness.
- Does not itself grant pass-once or ownership.

An incomplete, failed, timed-out, or abandoned session:

- Remains non-clean.
- Preserves its evidence.
- Does not become progress.

### Cancellation Boundary

Leaving Preflight or instruction prep before active work begins is cancellation. It clears the prepared/active snapshot as appropriate without creating a failed completed-session artifact.

Stopping after active work begins is abandonment and remains visible evidence.

## Android Architecture

The 4,000-line `MgNavigationShell` must be decomposed without moving domain decisions into Android.

### Target Structure

```text
Ui/
  Navigation/
    MgAppShell.cs
    MgDestination.cs
    MgNavigationState.cs
  Screens/
    TrainScreen.cs
    MapScreen.cs
    BranchDetailScreen.cs
    RecordScreen.cs
    EvidenceDetailScreen.cs
    ReviewScreen.cs
    LocalDataScreen.cs
    PreflightScreen.cs
    LiveSessionScreen.cs
    ResultScreen.cs
  Components/
    AppTopBar.cs
    BottomNavigationBar.cs
    PrimaryActionBar.cs
    CriteriaStripView.cs
    TargetMaterialView.cs
    BranchRailView.cs
    StateNodeView.cs
    EvidenceRowView.cs
    ResultEvidenceStripView.cs
    ExpandableDetailView.cs
```

Names may adapt to established C# Android patterns, but responsibilities remain separated.

### Rendering Rules

- Screen renderers receive App presentation snapshots.
- Navigation state is ephemeral Android state.
- Durable training state remains in App/Persistence.
- Live views update individual controls in place.
- Timer refresh cannot recreate input controls or screen structure.
- Android does not infer pass/fail, readiness, maintenance, decay, or transfer.

## App Presentation Models

Add purpose-built presentation models rather than exposing broad implementation read models directly:

- Train command presentation.
- Branch map presentation.
- Branch detail presentation.
- Evidence timeline item and detail presentation.
- Review decision presentation.
- Local data action availability.
- Target Hold live interaction state.
- Result evidence measurements and direct next action.

Presentation models translate domain vocabulary into practitioner-facing language while preserving exact facts.

They do not store state or decide progression.

## Visual System

### Palette

Use a neutral canvas and ink as the base. Reserve semantic colors:

- Teal: active training action.
- Cobalt: test/readiness/standard.
- Green: owned or clean evaluated result.
- Amber: maintenance and caution.
- Red: failed constraint, decay, or destructive action.
- Violet: transfer.
- Gray: unavailable, cancelled, recovery context.

No screen should read as variations of teal alone.

### Typography

- Screen title: 26-28sp.
- Section heading: 18-20sp.
- Body: 15-16sp.
- Labels: 12-13sp minimum.
- No 10sp visible training text.
- No viewport-scaled font sizes.

### Spacing And Targets

- 8dp spacing grid.
- 48dp minimum interactive target.
- 56dp primary action height.
- Stable dimensions for timers, target material, nodes, and live controls.
- Text wraps instead of shrinking below minimum size.

### Containers

- Use unframed page sections and full-width bands by default.
- Use cards only for repeated evidence items, modals, and truly bounded tools.
- Do not place cards inside cards.
- Avoid icon-badge/card repetition as the primary visual grammar.

### Motion

Allowed motion explains state:

- Timer progress.
- Wander -> returning state transition.
- Stabilization segment fill.
- Gate open/close.
- Transfer bridge reveal.

No celebration, pulsing engagement, or ownership animation before Core grants ownership.

## Implementation Phases

### Phase 0: Baseline And Plan

- Preserve the full audit.
- Record this implementation blueprint.
- Establish current behavioral and screenshot baseline.
- Identify pre-existing worktree changes and avoid reverting them.

**Exit evidence:** plan exists; baseline build/tests recorded; current first-run captures reviewed.

### Phase 1: Honest Target Hold Runtime

- Add return and target-substitution Runtime commands.
- Add validation and snapshot/restore behavior.
- Produce complete FH evidence facts.
- Add executable Core FH L1 standard.
- Map Runtime evidence to Core evaluation.
- Persist Core-evaluated clean practice correctly.
- Distinguish pre-active cancellation from abandonment.
- Add deterministic Core, Runtime, App, and persistence integration tests.

**Exit evidence:** a clean 3-minute hold becomes clean practice; late/unreturned/too-many-wander/target-change cases fail for the exact reason; resume preserves open-return state.

### Phase 2: Navigation And Rendering Foundation

- Introduce persistent four-destination navigation.
- Add real back stack and visible back affordances.
- Decompose screen rendering responsibilities.
- Add sticky primary action area.
- Replace whole-screen live rerender with in-place updates.
- Preserve text input focus across timer ticks.
- Establish 48dp targets and 12sp minimum labels.

**Exit evidence:** all primary destinations are reachable whenever no modal workflow is active; Back is predictable; 10 sampled live screenshots contain no partial frame; active text input survives timer updates.

### Phase 3: Core Training Loop Redesign

- Redesign Train.
- Redesign Target Hold Preflight.
- Add visual generated-target renderer.
- Redesign live holding/returning/target-change states.
- Redesign Result with actual evidence and direct next action.
- Remove false affordances, duplicate actions, `Saved n/n`, and unsupported claims.

**Exit evidence:** first-time user can explain the task, perform each evidence action, see an honest result, and identify the next action without opening technical detail.

### Phase 4: Map And Branch Detail

- Build interactive foundational/advanced map.
- Implement state nodes, gates, blockers, stabilization, maintenance, decay, and transfer visuals.
- Add human branch names.
- Add branch detail and eligible-work/blocker routes.
- Correct representative-level selection.

**Exit evidence:** every branch is inspectable; passed-once cannot look owned; decay and blockers appear on nodes and edges; branch detail has one actionable route.

### Phase 5: Record, Review, Maintenance, Local Data

- Build filterable evidence timeline and evidence detail.
- Remove duplicate/meta evidence rows.
- Make Review decisions actionable and beginner-appropriate.
- Add focused maintenance/restoration flow.
- Correct local backup action availability and confirmation.

**Exit evidence:** evidence is traceable to standards and results; Review routes its decision; restore cannot run without a valid backup and explicit confirmation.

### Phase 6: Drill Family Presentation

Implement purpose-built live presentation for every supported drill family:

- Focus Shift cue switching and invalid-cue filtering.
- Working Memory encode, delay, reconstruction, and transform.
- Inhibition go/no-go and exception rules.
- Discrimination pairs and seeded audit.
- Concept rule extraction and structure mapping.
- Affective pressure and disruption recovery.
- Transfer Integration composite evidence.

For each family:

- Render complete generated material.
- Present one current action.
- Preserve phase-specific constraints.
- Keep inputs stable.
- Show branch-specific evidence.
- Evaluate and route results honestly.

**Exit evidence:** no implemented drill uses `do the current exercise step`, no required material is ellipsized, and every required Runtime command has an understandable control.

### Phase 7: Accessibility And Responsive Hardening

- Verify 1.0x, 1.3x, and 1.5x font scales.
- Verify narrow phone, standard phone, tall phone, landscape, and tablet constraints.
- Add complete content descriptions and live-region announcements.
- Remove redundant decorative accessibility nodes.
- Verify contrast and non-color state distinctions.
- Verify touch targets and keyboard behavior.

**Exit evidence:** no clipping, overlap, inaccessible action, color-only state, or sub-48dp control in supported layouts.

### Phase 8: Visual QA And Acceptance

- Extend deterministic debug fixture/capture modes for every primary and critical state.
- Capture clean-device first-run flow.
- Capture map states including passed-once, stabilizing, owned, due, decayed, blocked, and transfer.
- Capture evidence, review, maintenance, restore, recovery, cancellation, abandonment, failure, clean practice, and ownership outcomes.
- Add blank-frame and duplicate-frame checks.
- Conduct screen-by-screen average-user review using task-based questions rather than required-phrase presence.

**Exit evidence:** fresh screenshots, manifests, interaction checks, automated tests, and written review all agree with the current build.

## Verification Matrix

| Requirement | Authoritative evidence |
| --- | --- |
| Current action is obvious | Fresh device screenshot plus UIAutomator primary-action check |
| Visible standard is measurable | Runtime command/evidence tests and App completion integration test |
| Clean practice is credited honestly | Core evaluation result, persisted session record, refreshed state test |
| Failure cannot become progress | Core/App tests for each terminal/failure path |
| Navigation is available | Device interaction test reaching all four destinations |
| Back behavior is predictable | Activity/device navigation test |
| Map is interactive | Device taps open branch detail and blocker routes |
| Evidence is inspectable | Device taps open evidence detail tied to source record |
| Review is actionable | Presentation test and device action route |
| Live UI is stable | Repeated screenshots/canvas checks during timer and input |
| No material truncation | Drill-family fixture screenshots and text assertions |
| Accessibility targets | Static helper tests and device bounds inspection |
| Large text works | Screenshots at 1.3x and 1.5x font scale |
| Responsive layouts work | Screenshots at required viewport classes |
| Restore is safe | App/Persistence tests plus device confirmation flow |
| Philosophy is preserved | Requirement-by-requirement final audit against foundation docs |

## Task-Based Review Questions

For every primary screenshot and interaction state, answer:

1. What does this screen want me to do?
2. What object or rule am I working with?
3. What counts as clean?
4. What action do I take if something goes wrong?
5. What did the app actually observe?
6. Did this change my training state?
7. What happens next?
8. Is any visible object pretending to be interactive?
9. Is any visible text implementation jargon?
10. Could the same meaning be communicated visually with fewer words?

A screen fails if a normal practitioner cannot answer the relevant questions without reading hidden technical detail.

## Definition Of Done

The overhaul is complete only when:

- The full app, not only first-run, follows the new information architecture.
- Every implemented drill has a usable, phase-correct live presentation.
- Every claimed standard has matching Runtime inputs and Core evaluation.
- Every result exposes decisive evidence and a direct next action.
- Map, Record, Review, maintenance, and Local Data are real workflows.
- Navigation, Back, lifecycle, resume, and cancellation behave predictably.
- Accessibility and responsive-layout acceptance gates pass.
- Automated Core, Runtime, Content, Persistence, App, and Android-facing tests pass.
- Fresh device screenshots verify all named states.
- A final completion audit proves every objective requirement with authoritative evidence.

Passing tests alone is not completion. Fresh screenshots alone are not completion. Copy presence alone is not completion. The behavior, presentation, evidence, and progression state must agree.
