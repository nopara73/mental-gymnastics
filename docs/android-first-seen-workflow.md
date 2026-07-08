# MentalGymnastics Android First-Seen Workflow

**Status:** First-seen Android workflow spec  
**Scope:** Presentation direction only. No UI code. No behavior changes.  
**Depends on:** [Android UI Operating Principle](android-ui-operating-principle.md), [Android UI Layer](android-ui-layer.md), [Pre-UI App Integration Boundary](app-integration-boundary.md), [Core Library](core-library.md), [Session Runtime Boundary](session-runtime-boundary.md), [Generated Content Boundary](generated-content-boundary.md), [Local Persistence Boundary](local-persistence-boundary.md)

## Purpose

This spec defines what a first-time practitioner sees when opening the Android app.

The first visible screen is actionable training state. It is not a dashboard, map, report, landing page, onboarding flow, explanation page, account prompt, marketing screen, motivation loop, or settings surface.

Android renders app-layer state and forwards actions. It does not decide first placement, readiness, standards, generated content, runtime timing, evidence classification, completion handling, progression, maintenance, decay, recovery, transfer, or review outcomes.

If first-run initialization has no prior records, the app-facing state should still resolve to prescribed work, such as the first eligible universal-assessment or protected-control training action. Android must not fill the gap with onboarding copy or a free-choice library.

## First-Level Rejections

The first-seen workflow explicitly rejects:

- Text-heavy explanations of the program, philosophy, app architecture, or layer boundaries.
- Decorative cards that do not carry a training state, action, blocker, standard, evidence, or result.
- Chip-heavy summaries that expose every enum, count, guard, source fact, and implementation status.
- Raw technical metadata: internal IDs, generated instance IDs, content IDs, hashes, local paths, source record IDs, equivalence class labels, app-layer jargon, runtime handoff wording, persistence wording, and debug-style flags.
- Any first-level UI that implies progress through opening the app, reading an explanation, tapping through setup, spending time, or choosing a preferred branch.

Technical and audit details may exist behind reveal-on-demand only when they support troubleshooting, evidence review, or local data operations.

## Workflow

The first-seen flow is:

1. Today / prescribed work
2. Session preflight
3. Live session
4. Result
5. Return to next prescribed action

No step may insert a landing page, onboarding carousel, feature tour, motivational explanation, profile setup, account creation, sync prompt, notification permission prompt, AI/API prompt, streak setup, or social feature.

## 1. Today / Prescribed Work

**Understand immediately:** what the app says to do now.

For a first-time user, this is the first prescribed training action returned by app integration, not an invitation to browse the whole program. If work is blocked, the blocker itself is the prescribed state.

**Primary action:** `Start` the prescribed work, or `Show blocker` when app/core state says work cannot start.

**Visual object:** one dominant work command: session role, branch/level, readiness/blocker marker, and action. A small branch rail may provide orientation, but it is secondary and must not become a dashboard.

**Unavoidable text:**

- Session role, branch code, level, and drill name or short drill code.
- `Start`, `Resume`, or `Blocked`.
- The shortest blocker reason when blocked.
- A compact readiness label such as `Ready`, `Due`, `Recovery`, `Stabilize`, or `Test`.

**Reveal-on-demand detail:**

- Why this work was selected.
- Full prerequisite chain.
- Full weekly-plan context.
- Branch description and level rationale.
- Recent evidence trace.
- Technical app/read-model/source details.

**Self-deception prevention:**

- No free branch library is presented as the main action.
- No dashboard summary lets the user feel progress before doing work.
- No placement flattery is shown; first-run placement remains constrained by app/core state.
- Blocked, recovery, due maintenance, decay, or unsafe resume outrank new practice.
- The screen does not show streaks, minutes, encouragement, or broad progress percentages.

## 2. Session Preflight

**Understand immediately:** what standard must be met before the session can honestly start.

Preflight is not a teaching article. It is the contract for the upcoming attempt.

**Primary action:** `Start`.

The action is available only when app preparation, generated content, and runtime preparation are startable. If not startable, the primary action becomes `Show blocker`.

**Visual object:** a compact standard-and-constraint panel attached to the start action. The panel must visibly bind branch/level/drill, load, standard, honesty constraint, and required evidence.

**Unavoidable text:**

- Branch, level, drill, and session role.
- Standard.
- Honesty constraint.
- Critical constraints.
- What counts as failure.
- Required generated material or target/rule prompt when needed before start.

**Reveal-on-demand detail:**

- Full drill purpose.
- Full load variables beyond what is needed to start.
- Full generated-content identity, freshness, and equivalence metadata.
- Full rejection list.
- Runtime phase plan.
- Expected evidence fact list.

**Self-deception prevention:**

- The user sees the standard and honesty constraint before starting.
- The UI cannot lower load, choose easier content, hide source standards, or bypass missing preparation.
- Transfer preflight shows preserved source demand plus changed context.
- Start is unavailable when app/runtime preparation is blocked.
- Preflight does not use a long confirmation ritual as substitute evidence.

## 3. Live Session

**Understand immediately:** the current runtime demand and the one valid action to take now.

The live session is sparse. It should not show program explanations, progress dashboards, or implementation status.

**Primary action:** the current runtime command, such as `Respond`, `Submit`, `Mark drift`, `Mark guess`, `Correct`, `Finish phase`, `Pause`, `Resume`, `Audit`, or `Abandon`.

When several runtime commands are available, hierarchy must identify the command that matches the active phase. Evidence commands remain available where required, but they do not compete visually with the active response demand.

**Visual object:** cue/material panel plus timer/phase marker and stable command controls. The cue/material panel carries the demand; the timer/phase marker carries timing; evidence controls carry honesty constraints.

**Unavoidable text:**

- Active target, rule, cue, item, comparison, reconstruction prompt, or audit prompt.
- Current phase label when needed.
- Timer or response window when timing matters.
- Short command labels.
- Compact standard/constraint indicator available during the session.

**Reveal-on-demand detail:**

- Full standard text.
- Full critical constraints.
- Full phase plan.
- Runtime event history.
- Generated-content identity and cue schedule.
- Evidence counters beyond what the user must act on.

**Self-deception prevention:**

- Android only renders runtime-owned phase, cue, timing, and command availability.
- Invalid commands do not advance visual progress or erase evidence.
- Abandon, timeout, missed cues, unmarked drift, unmarked guesses, wrong answers, and corrections remain recordable facts.
- Opening detail does not pause, extend, restart, or soften the session unless runtime explicitly allows pause.
- The live screen never implies advancement from elapsed time, effort, or completion of a visual sequence.

## 4. Result

**Understand immediately:** what happened, what changed, and what did not change.

The result screen reports a programming outcome, not a feeling.

**Primary action:** follow the next programmed response, such as `Continue`, `Recover`, `Repeat`, `Stabilize`, `Maintain`, `Restore`, `Review`, or `Show blocker`.

**Visual object:** result summary with outcome marker, evidence strip, state-transition marker, and next-action control.

**Unavoidable text:**

- Exact outcome: `Failed`, `Passed once`, `Stabilizing`, `Owned`, `Maintenance`, `Warning`, `Decayed`, `Transfer failed`, `Recovery`, `Blocked`, or `No advancement`.
- Short evidence trace: score, errors, drift, guesses, response timing, audit result, reconstruction result, failed constraint, failure type, or artifact summary.
- Next programmed response.

**Reveal-on-demand detail:**

- Full standard evaluation.
- Full formal gate decision.
- Runtime event detail.
- Evidence artifact detail.
- Failure-response rationale.
- State transition detail.
- Generated instance completion metadata.

**Self-deception prevention:**

- Completion is not progress unless app/core results say a state changed.
- Passed once never looks owned.
- Failed, abandoned, timed-out, invalid, unsafe, or incomplete sessions remain visibly non-successful.
- Failure is classified and routed; it is not reframed as inspiration.
- Recovery and deload are shown as reduced-load programming, not advancement.
- Result copy avoids celebration, praise, streaks, and motivational reinterpretation.

## 5. Return To Next Prescribed Action

**Understand immediately:** what the program asks for after the result.

The return state is not a dashboard landing. It is the next work command or the highest-priority blocker after the result has been processed.

**Primary action:** `Start`, `Resume`, `Recover`, `Repeat`, `Stabilize`, `Maintain`, `Restore`, `Review`, or `Show blocker`.

**Visual object:** refreshed work command at the top of the Work surface. A short evidence strip may remain attached to the prior result, but the next action owns the hierarchy.

**Unavoidable text:**

- Next session role or blocker.
- Branch/level when applicable.
- Required recovery, stabilization, maintenance, restoration, review, or blocked reason.
- `No advancement` only when needed to prevent misunderstanding of the previous result.

**Reveal-on-demand detail:**

- Full result history.
- Evidence artifact detail.
- Why the next action changed.
- Updated branch map.
- Updated weekly plan.
- Maintenance and dependency-cap detail.

**Self-deception prevention:**

- The user cannot continue into favorite or novel work when app/core state prescribes recovery, maintenance, restoration, stabilization, deload, or review.
- Any blocker remains on the action and related map edge.
- The previous session's evidence remains auditable but does not become a motivational summary.
- The UI does not turn the completed workflow into a dashboard, score page, streak, badge, or social/share moment.

## Acceptance Check

The first-seen workflow passes only if:

- Opening the app immediately shows actionable training state.
- Each step has one primary action.
- Visual state is readable before explanatory text.
- Standards and honesty constraints are visible before live work.
- Failure, blockers, decay, stabilization, maintenance, transfer, recovery, deload, unsafe resume, and no-advancement states cannot be missed or faked.
- Raw technical metadata is hidden by default.
- Detail is revealed only when it supports action, evidence, audit, or safety.
- No account, sync, backend, telemetry, notification, analytics, AI/API, streak, social, onboarding, or marketing feature appears in the flow.
