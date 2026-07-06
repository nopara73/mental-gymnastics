# MentalGymnastics Android UI Strategy

**Status:** UI strategy

**Applies to:** Future Android screens, navigation, visual language, interaction design, and Android-hosted session presentation

**Depends on:** [Progression Against Vibes](foundation/progression-against-vibes.md), [Standards-Based Skill Ladder](foundation/standards-based-skill-ladder.md), [Complete Training Program](program/training-program.md), [Core Library](core-library.md), [Local Persistence Boundary](local-persistence-boundary.md), [Session Runtime Boundary](session-runtime-boundary.md), [Generated Content Boundary](generated-content-boundary.md), [Pre-UI App Integration Boundary](app-integration-boundary.md)

## Purpose

The Android UI should make the training program legible without turning it into a reading app, motivation app, social app, or habit tracker.

The product should feel like a compact training console: visual first, low text, direct about standards, and unwilling to turn participation into progress. The UI should show the current prescribed work, the visible state of each branch, the standard being attempted, the honesty constraint that protects the attempt, the evidence captured, and the next programming action.

The UI must not soften failure, hide decay, disguise blocked advancement, or imply that a completed screen equals earned progress.

## Boundary Rule

Android is a rendering and input layer.

Future Android screens should consume `MentalGymnastics.App` workflows and read models, render generated content and runtime state, and forward user actions into app/runtime commands. Screens must not own progression rules, readiness, gates, stabilization, maintenance, decay, dependency caps, global balance, transfer eligibility, recovery, deload, failure routing, weekly planning, practitioner classification, prompt generation, cue scheduling, phase timing, evidence classification, or local storage paths.

If a screen needs a decision, it should display the app-facing result produced from Core, Persistence, Runtime, Content, and App integration. If the needed decision is missing, add it to the owning layer with tests before adding UI.

## Product Posture

The UI should prefer:

- Capacity state over mood state.
- Current work over open-ended browsing.
- Visible gates over vague progress bars.
- Evidence summaries over encouragement.
- Blockers, decay, and maintenance warnings over motivational reinterpretation.
- Transfer and stabilization status over novelty.
- One clear next action over many self-directed options.

The UI should avoid:

- Streaks, badges, achievements, points, leaderboards, social comparison, sharing flows, celebratory animations, daily motivational copy, or engagement loops.
- Text that reframes failure as success.
- Cosmetic unlocks that resemble advancement.
- User-selected shortcuts around prerequisites, standards, retests, maintenance, or transfer.
- Hidden local flags, screen-local completion states, or UI-owned pass/fail markers.

## Visual-First Model

Visual-first means the primary state is understood through position, shape, density, lock state, and evidence markers before the user reads detail text.

### Branch Ladder

Use a compact branch-by-level matrix as the main progress surface:

| State | Visual treatment | Required meaning |
| --- | --- | --- |
| Unopened | Hollow node with lock | Prerequisites are missing. |
| Training | Open ring | Practice is active; no advancement yet. |
| Test-ready | Ring with test marker | Core says a formal test may be attempted. |
| Passed once | Half-filled node | One pass exists; ownership is not granted. |
| Stabilizing | Segmented ring | Additional clean passes are required. |
| Owned | Filled node | Standard is repeatable under ownership rules. |
| Maintenance | Small maintenance marker on owned node | Contact is required to prevent decay. |
| Warning | Amber outline or hatch | Maintenance failed once or is near overdue. |
| Decayed | Broken or diagonally hatched node | Dependent advancement is capped until restored. |
| Blocked | Stop marker on edge to next node | The next step is unavailable because of a specific rule. |

Do not rely on color alone. Every status needs a shape or pattern difference and an accessibility label.

### Work Queue

The primary home surface should show the next prescribed work as a small queue, not as an open library. Each item should show:

- Session type icon: practice, load, test, stabilization, regression, transfer, maintenance, recovery, review, or off.
- Branch and level code.
- Load marker or transfer marker.
- Status marker: ready, blocked, due, warning, decayed, resume, or recovery.
- One primary action when work is allowed.

The queue should come from app integration and Core planning. The UI may let the practitioner inspect alternatives only when the app layer exposes them as eligible.

### Live Session

The live session surface should be sparse:

- Current phase.
- Active generated material or cue.
- Timer or response window when runtime exposes one.
- Required action controls.
- Evidence controls such as mark drift, mark guess, respond, submit, correct, start audit, pause, resume, or abandon.
- A small standard/constraint indicator that can expand on demand.

The runtime owns phase timing, cue identity, response windows, scoring facts, and completion status. The screen only renders runtime state and sends commands.

### Completion

The completion surface should report what happened, not how to feel about it.

Show the outcome as a gate/programming result:

- Failed.
- Pass once.
- Stabilization pass.
- Owned.
- Maintenance pass.
- Maintenance warning.
- Decayed.
- Restored.
- Transfer valid or transfer failed.
- Recovery prescribed.
- Deload prescribed.
- Advancement blocked.

Every completion result should show a short evidence strip: score, errors, drift marks, guesses, response timing, audit result, reconstruction result, failure type, or artifact reference. Longer detail should be available on demand.

## Screen Strategy

### Startup And Resume

The first screen should answer:

- Is there an active session to resume?
- What work is currently prescribed?
- Is any maintenance, decay, blocker, or recovery state more important than new work?

There should be no landing page, feed, onboarding carousel, account prompt, cloud prompt, or motivational dashboard. First-run placement may explain the assessment briefly, then move directly to the first executable step.

### Home

Home should be a training-state console:

- Top: current allowed next work or active blocker.
- Middle: branch ladder preview.
- Bottom: due maintenance, recent evidence, and next review cue.

Use terse labels such as `FH L2`, `Stabilize 2/3`, `Blocked: WM maintenance`, or `Transfer due`. Longer explanations open from the status marker.

### Branch Detail

Branch detail should be a visual progression lane:

- Current level and state.
- Required standard.
- Honesty constraint.
- Last evidence artifact.
- Required stabilization or maintenance passes.
- Blockers and prerequisites.
- Allowed next actions.

Historical sessions and full drill descriptions should be collapsed by default.

### Session Preflight

Before live work starts, the practitioner must see enough text to preserve the standard:

- Branch, level, and drill.
- Session type and load variable.
- Standard.
- Honesty constraint.
- Critical constraints.
- What counts as failure.
- What evidence will be captured.

The start control should remain unavailable until the standard and honesty constraint have been presented. The UI should not require a long confirmation ritual unless the app layer needs explicit stated-standard evidence.

### Live Session

During active work, text should drop to the minimum required by the drill:

- Generated material.
- Current rule or target when required.
- Cue labels when required.
- Input labels.
- Timer and phase label.
- Evidence actions.

Detailed standard text should be one tap away, but opening it must not pause or alter runtime unless the runtime explicitly allows pause.

### Post-Session

Post-session should show:

- Completion status.
- Evidence strip.
- Gate or maintenance result.
- Failure classification when failed.
- Next programming action.

Do not show generic success copy. A completed practice session can be useful, but it is not advancement unless Core says the relevant gate changed.

### Evidence And History

Evidence history should be scan-first:

- Timeline of attempts, checks, stabilization passes, transfer tests, recovery sessions, and reviews.
- Visual markers for pass, fail, warning, decay, restore, abandon, timeout, and invalidated evidence.
- Filter by branch, level, session type, or failure type.

Full artifacts, rubric details, and runtime event details should be reveal-on-demand.

### Global Review

Global review should be a whole-practitioner board:

- Current owned level by branch.
- Maintenance status.
- Last failures and classifications.
- Bottleneck branch.
- Transfer or stabilization currency.
- Recovery or deload status.
- Core review decision.

The review screen should make bottlenecks and blocked advancement visible without turning them into identity labels.

## Text Policy

The UI should be low-text, not text-avoidant. Text is unavoidable when it protects standards, evidence, accessibility, or user control.

### Text Is Unavoidable For

- Stated standards before tests, stabilization attempts, transfer attempts, and maintenance checks.
- Honesty constraints and critical constraints.
- Generated drill material that is textual by nature.
- Rules, exception lists, memory items, reconstruction prompts, audit instructions, and transfer source standards.
- Failure classification and the next programmed response.
- Decay, blocked advancement, dependency caps, and maintenance due reasons.
- Evidence artifact summaries.
- Local backup/restore warnings and data-destructive confirmations.
- Accessibility labels, screen-reader content descriptions, and input error messages.

### Reveal On Demand

Keep these collapsed unless the user asks or the state requires attention:

- Full branch rationale.
- Full level description.
- Drill purpose and capacity explanation.
- Complete rubric tables.
- Detailed prerequisite chains.
- Maintenance cadence definitions.
- Historical runtime event logs.
- Full generated-content identity, equivalence, and freshness metadata.
- Global review input details.
- Failure-response rationale beyond the immediate next action.

### Avoided Text

Do not add:

- Motivational quotes.
- Daily affirmations.
- Engagement prompts.
- Vague encouragement after failure.
- Identity labels such as smart, disciplined, resilient, advanced-minded, or gifted.
- Copy that implies effort, time spent, or screen completion earned progress.

## Preserving Honesty In UI

### Standards

Standards must remain visible before formal tests and available during attempts. The UI must not summarize a standard so aggressively that thresholds, critical constraints, or required evidence disappear.

### Failure

Failure states should be plain and actionable:

- What failed.
- Which constraint or threshold failed.
- How the failure was classified.
- What the programmed response is.

Do not rename failure as partial success. Excellence can be shown, but it cannot visually imply skipped ownership, prerequisites, or stabilization.

### Stabilization

Stabilization should look unfinished by design. A passed-once branch should not use the same visual weight as an owned branch. Segmented progress should count required clean passes, show dates where needed, and keep the next level visibly locked until ownership rules are satisfied.

### Transfer

Transfer should never appear as novelty. Transfer screens should show the source branch standard beside the changed context:

- Same demand preserved.
- Context changed.
- Source standard visible.
- Fresh-equivalent requirement when relevant.
- Branch-specific evidence required.

### Decay And Maintenance

Maintenance should be visible before it becomes decay. A decayed prerequisite should visibly cap dependent work and route the next action toward restoration. The UI should not hide decayed branches behind a positive global progress summary.

### Blocked Advancement

Blocked advancement should be shown on the edge between current state and desired next state. The reason should be terse at first view and exact on expansion: missing prerequisite, overdue maintenance, decayed branch, missing stabilization pass, missing transfer, failed global review, dependency cap, recovery, or deload.

## Navigation Model

Use four primary areas:

1. **Work:** current prescribed work, active resume, and eligible next actions.
2. **Map:** visual branch ladder and branch details.
3. **Evidence:** attempts, artifacts, maintenance, failures, transfer, and review history.
4. **Review:** global review, bottlenecks, maintenance currency, and planning decisions.

Avoid a separate motivational dashboard, content feed, community area, streak page, account page, notification center, analytics page, or AI coach area.

## Allowed Personalization

Personalization may affect presentation and eligible content choices, not standards.

Allowed:

- Theme contrast and accessibility settings.
- Text size and reduced motion.
- Session day selection when frequency and recovery rules remain valid.
- Content domain selection only when the app layer exposes equivalent content that preserves the same capacity, load, standard, and freshness requirement.
- Pressure source selection for AI only when defined before the test and accepted by the app/core workflow.

Not allowed:

- Hiding failure, blockers, or decay.
- Lowering standards from the UI.
- Skipping stabilization.
- Replacing tests with self-description.
- Removing evidence artifacts.
- Choosing only strong branches while bottlenecks decay.
- Treating new content as transfer unless the transfer rule is satisfied.

## Offline And Privacy Boundary

The Android UI should remain offline-first and userless:

- No accounts.
- No sync.
- No backend services.
- No telemetry.
- No analytics.
- No notifications.
- No AI/API dependencies.
- No social features.
- No cloud backup implementation in this strategy.

Local backup/restore may be exposed later only through the Persistence backup boundary and must remain local, explicit, and user-controlled.

## Accessibility

Low-text visual UI still needs accessible meaning.

Every state marker should have a spoken label that includes branch, level, state, and blocker where relevant. Color must never be the only carrier of meaning. Timers, cue changes, failures, invalid commands, and required evidence actions need accessible announcements when they affect the live session.

Accessibility support must not alter standards. Accommodations may change presentation and input mechanics, but if a change modifies the trained demand, it must be modeled as a different eligible workflow or regression through the app/core boundary.

## Android Implementation Guidance

When UI implementation begins:

1. Start with app integration read models and Runtime state snapshots.
2. Build the visual state vocabulary around Core states and app-facing decisions.
3. Keep screens thin: render state, collect input, forward commands.
4. Use local generated content packages for drill material.
5. Save active sessions through app integration and Persistence snapshot workflows.
6. Treat failed, timed-out, abandoned, or unsafe restored sessions as non-successful evidence unless Core later evaluates valid evidence.
7. Add UI behavior tests only after executable UI behavior exists; rule, storage, runtime, content, and app orchestration tests belong in their owning layers first.

The first useful Android UI should be narrow: startup, current work, branch ladder, one runnable session path, completion, and evidence summary. Breadth should come after the UI proves that it can preserve standards, failure, decay, stabilization, and transfer without adding parallel logic.
