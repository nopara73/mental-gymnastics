# MentalGymnastics Android UI Operating Principle

**Status:** Replacement UI direction  
**Scope:** Future Android redesign only. No behavior changes. No UI code.  
**Supersedes:** Earlier Android UI direction wherever this document conflicts with it.

## Operating Principle

The Android app is a training console, not a dashboard, report, journal, feed, or motivation product.

The first screen must answer: **what do I do now?**

Every screen must have one primary action. The UI either starts or resumes allowed work, shows why work is blocked, routes to the required restoration/maintenance/recovery/stabilization/transfer/review work, runs the live session, shows the result, or audits evidence.

If a screen cannot name its one primary action, it should not exist in the main flow.

## Current UI Diagnosis

The current Android UI respects the layer boundaries, but it presents the program like an implementation report.

Remove or redesign:

- Text-heavy panels that explain internals instead of shaping action.
- Chip soup where every enum, count, guard, status, and source fact competes for attention.
- Duplicated dashboards that restate branch state across Work, Map, Progress, Maintenance, and Review.
- Debug-style surfaces exposing generated instance IDs, content IDs, equivalence classes, load hashes, source record IDs, local database paths, app workflow wording, runtime handoff wording, and "no grant" jargon.
- First-screen layouts where the practitioner must read several panels before knowing whether to start, resume, restore, maintain, recover, stabilize, test, review, or inspect a blocker.

The replacement direction is subtraction first: keep standards, gates, evidence, blockers, and state truth; remove the report wrapper.

## Navigation

Primary areas:

- **Work:** launch screen and current prescribed action.
- **Map:** branch ladder, dependency edges, blockers, decay, maintenance marks, stabilization, and transfer bridges.
- **Evidence:** local audit trail.
- **Review:** whole-practitioner review, bottlenecks, balance, maintenance currency, and programmed response.

Do not keep a separate Progress dashboard. Progress is expressed through Map, Evidence, and Review.

Do not keep Maintenance as a normal top-level destination. Maintenance, warning, decay, and restoration are priority states surfaced in Work and Map. A focused maintenance route may open from those states.

Local Data is a utility behind overflow/settings, not a primary training destination.

## Work Priority

Work is the launch screen. It has one dominant object: the current training command.

Priority order:

1. Safe active session to resume.
2. Unsafe active session to inspect or clear.
3. Decayed branch requiring restoration.
4. Blocked advancement or dependency cap.
5. Due or warning maintenance.
6. Recovery or deload.
7. Stabilization pass.
8. Formal test.
9. Practice or load work.
10. Global review.

The primary action changes with the state: `Resume`, `Start`, `Restore`, `Maintain`, `Recover`, `Stabilize`, `Test`, `Review`, or `Show blocker`.

Secondary context may show a compact branch preview and a short evidence trace. It must not compete with the primary command.

## Visual Rule

Visual structure comes before text.

Use state-bearing objects:

- A single command card on Work.
- Branch-level nodes and gate edges on Map.
- Segmented stabilization markers.
- Hard barriers on blocked edges and unavailable actions.
- Fractured/interrupted nodes for decay.
- Service markers for due maintenance.
- Lower-load side routes for recovery and deload.
- Transfer bridges from source standard to changed context.
- Short evidence strips for outcomes.

Chips are not a layout system. Use at most two or three chips for exact, high-value facts. Do not render chip rows for every enum, count, implementation flag, or source object.

## Text Rule

The UI is low-text, not vague.

Visible text is required for standards, honesty constraints, critical constraints, generated drill material, failure conditions, blocker reasons, failure classification, maintenance/decay/restoration requirements, transfer source standards, evidence summaries, local restore warnings, and accessibility labels.

Hide these unless the user explicitly opens technical detail:

- Raw internal IDs.
- Content IDs and generated instance IDs.
- Equivalence classes and load hashes.
- Local database paths and file paths.
- Source record IDs and stable identifier names.
- App-layer jargon such as workflow, handoff, read model, rejection, runtime only, no grant, owns scoring, app state, or persistence primitive.
- Implementation explanations such as "Android UI grants no progress."

Use practitioner-facing wording instead: `Progress unchanged`, `Start blocked`, `Maintenance due`, `Restore required`, `Content ready`, `Local data valid`, `Evidence incomplete`.

## Honesty Rule

Standards must be preserved without turning the app into a report.

- **Failure:** show what failed, the classified failure type when available, and the programmed response. Do not rebrand failure as effort, insight, or progress.
- **Blocked advancement:** put the blocker on the action and on the map edge. Do not hide it inside disabled-button styling or detail panels.
- **Decay and maintenance:** decay outranks ordinary work, dependent caps stay visible, and restoration shows the required evidence path instead of a manual dismissal.
- **Stabilization:** passed once never looks owned. Stabilization shows required clean passes and keeps the next level locked.
- **Transfer:** show source standard and changed context together. New content without preserved source demand is not transfer. TI keeps component evidence separated.
- **Recovery and deload:** show reduced load and disabled advancement testing. They are not progress.
- **Evidence:** show the shortest useful trace: score, errors, drift, guesses, response timing, audit result, reconstruction result, failure type, or artifact summary. Subjective notes are never advancement evidence.

## Prohibited Additions

Do not add accounts, sync, backend services, telemetry, notifications, analytics, AI/API features, streaks, social features, motivation loops, onboarding fluff, or marketing screens.

Do not add behavior changes while applying this direction. Android continues to render app-layer state and forward commands. Core, Persistence, Runtime, Content, and App integration keep their existing responsibilities.

## Acceptance Check

A redesigned Android screen passes only if:

- The first screen answers "what do I do now?" in one glance.
- The screen has one primary action.
- Visual state is readable before explanatory text.
- Failure, blocked advancement, decay, stabilization, maintenance, transfer, recovery, and deload cannot be missed or faked.
- Standards and honesty constraints are exact where needed.
- Internal IDs, hashes, paths, and implementation jargon are hidden by default.
- Progress is never implied by screen completion, elapsed time, taps, effort, novelty, or self-description.
- No duplicate dashboard exists to restate the same branch facts.
