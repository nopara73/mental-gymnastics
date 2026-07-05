# MentalGymnastics Pre-UI App Integration Boundary

**Status:** Implemented pre-UI application integration reference
**Project:** `src/MentalGymnastics.App`
**Tests:** `tests/MentalGymnastics.App.Tests`
**Composes:** [Core](core-library.md), [Local Persistence](local-persistence-boundary.md), [Session Runtime](session-runtime-boundary.md), [Generated Content](generated-content-boundary.md)
**Source program:** [MentalGymnastics: Complete Training Program](program/training-program.md)

This document describes the implemented boundary for the pre-UI application integration layer. The project establishes the non-visual composition boundary for the implemented domain, storage, runtime, and content libraries and provides app-facing workflows that future Android screens can call.

The app integration layer is not Android UI. It should contain no screens, layouts, navigation, visual design, Compose or View code, UI tests, notifications, accounts, sync, backend services, telemetry, analytics, or AI/API dependencies.

## Reference Direction

The intended dependency direction is:

1. `MentalGymnastics.Core` owns program rules and decisions.
2. `MentalGymnastics.Persistence` owns local offline JSON storage.
3. `MentalGymnastics.Runtime` owns live session execution mechanics.
4. `MentalGymnastics.Content` owns generated drill instances and content packaging.
5. `MentalGymnastics.App` composes those libraries into use-case workflows.
6. Future Android UI references `MentalGymnastics.App` and renders app-facing state.

Core, Persistence, Runtime, and Content must not reference the app integration layer. Android UI should not bypass the app integration layer to create parallel orchestration paths.

## What App Integration Owns

The app integration layer owns workflow orchestration and app-facing use cases:

- App startup initialization: create `LocalDatabaseOptions` from an app-owned local path, initialize JSON persistence, and load the current local programming picture.
- App-facing query models: combine `LocalProgramRepository` records with Core evaluator results into screen-ready data shapes without storing those shapes as authority.
- Session preparation: choose a branch, level, drill, load, standard, and freshness need from Core/planning inputs; query Persistence for current state and used content ids; request generated content from Content; persist the generated instance; and build Runtime session inputs.
- Live session coordination: host Runtime objects, pass user actions into Runtime commands, expose runtime state to UI, and never maintain a second timer, cue schedule, or evidence log.
- Session completion handling: convert Runtime completion into Core evaluation inputs and Persistence records; commit related state, evidence, attempts, sessions, maintenance, stabilization, generated-instance updates, and summaries through Persistence transactions where needed.
- Recovery and resume orchestration: load generated instance facts, runtime snapshots where they exist, and persisted local records; resume only through Runtime restore rules; record abandonments as non-successful outcomes.
- Active session snapshot persistence: save Runtime snapshot facts through `LocalActiveRuntimeSessionSnapshotStore`, restore them with `ActiveRuntimeSessionSnapshotPersistenceService`, and treat Runtime-rejected snapshots as unsafe rather than successful evidence.
- Maintenance and review workflows: load local facts, call Core maintenance, decay, dependency cap, global balance, weekly planning, and global review evaluators, then save only facts and source evidence.
- Local backup and restore entry points: call Persistence backup/restore services for local backup only.

The layer may define use-case classes, request/response records, coordinators, and app-facing DTOs. Those DTOs are convenience shapes for Android or CLI callers, not new domain authority.

## What App Integration Does Not Own

App integration must not own:

- Progression rules, prerequisites, readiness, gates, ownership, maintenance, decay, dependency caps, global balance, transfer eligibility, recovery, deload, weekly planning, practitioner classification, failure routing, stuck-state detection, global review, standards, rubrics, load rules, or anti-self-deception rules. These belong to Core.
- JSON schema, migrations, stable identifiers, local stores, repository storage details, transactions, integrity validation, backup/restore internals, or alternate persistence formats. These belong to Persistence.
- Timers, phase scheduling, cue scheduling, response timing, runtime command validation, scoring facts, live evidence capture, protocol execution, completion result generation, snapshot/restore internals, or runtime handoff internals. These belong to Runtime.
- Prompt material, cue sequences, generated instance identity, deterministic seeds, content banks, freshness/equivalence checks, content validation, transfer content, runtime packaging, or persistence handoff construction. These belong to Content.
- Android screens, layouts, navigation, visual styling, accessibility widgets, platform lifecycle presentation, UI copy, or UI tests. These belong to future Android UI.
- Accounts, sync, backend services, telemetry, analytics, notifications, AI/API calls, network access, or cloud storage.

## Persistence Constraint

Local JSON persistence is the accepted storage boundary for this stage. The app integration layer should consume `MentalGymnastics.Persistence` and its JSON database abstractions directly. It must not replace JSON with SQLite, Room, SharedPreferences, a second JSON file, a cache-owned store, or a UI-local flag system for program facts.

If an app workflow needs a record that Persistence cannot yet store, add that storage capability to `MentalGymnastics.Persistence` with tests. If a workflow needs a rule decision that Core cannot yet make, add the rule to `MentalGymnastics.Core` with tests.

## Core Workflow Shapes

### Startup

1. Android supplies an app-owned local file path.
2. App integration creates `LocalDatabaseOptions.ForAppOwnedPath(...)`.
3. Persistence initializes the JSON database and applies migrations.
4. App integration loads current practitioner state, recent sessions, evidence history, due maintenance inputs, generated instances, and progress summaries.
5. Core evaluators produce current app-facing decision summaries where needed.

### Select Or Prepare Work

1. App integration loads current state and recent facts from Persistence.
2. Core supplies category, weekly plan, readiness, recovery, deload, dependency cap, and balance decisions.
3. App integration chooses only among Core-eligible work options.
4. Content receives explicit branch, level, drill, session type, load variables, constraints, freshness policy, and used content ids.
5. Content returns validated generated material and handoff facts.
6. Persistence stores the generated instance locally before or as part of session start.

### Start Or Resume Session

1. App integration builds a `RuntimeSessionDefinition` and runtime phase/cue/material inputs from Content packages and Core standards.
2. Runtime owns lifecycle, phase, cue, timing, command, evidence, and snapshot behavior.
3. Android renders the app-facing session state and forwards user actions to app integration.
4. App integration forwards commands to Runtime and exposes results; it does not interpret invalid commands as success.
5. During lifecycle interruption, app integration may save `RuntimeSessionSnapshot` and `RuntimeCueSchedulerSnapshot` facts to local JSON persistence; resume must call Runtime restore, and unsafe snapshots must not become completed evidence.

### Complete Session

1. Runtime produces a completion result with phase history, events, scoring facts, evidence summary, generated instance identity, completion status, and failure-relevant facts.
2. App integration maps the result to Core-ready inputs using Runtime handoffs and caller-provided standard inputs.
3. Core evaluates gates, standards, stabilization, maintenance, transfer, failure response, recovery, deload, or review as appropriate.
4. App integration maps the result to Persistence-facing records.
5. Persistence commits related records atomically when one programming event writes state, evidence, attempts, session history, maintenance, stabilization, or generated-instance completion metadata.

### Review And Summary

1. App integration loads local facts from Persistence.
2. Core evaluates maintenance, decay, dependency caps, global balance, stuck state, practitioner category, weekly planning, and global review.
3. Persistence refreshes non-authoritative summaries with source references where useful.
4. Android displays the results without granting progress or hiding blockers.

## Anti-Self-Deception Guardrails

The app integration layer must preserve the existing guardrails:

- Button taps, screen completion, elapsed time, participation, effort, insight, and novelty are not advancement evidence.
- Generated content must be validated before Runtime consumes it.
- Runtime failed, timed-out, or abandoned sessions must not become successful evidence.
- Runtime snapshots that are unsafe or non-resumable must not become successful evidence.
- Core decisions must be displayed or persisted as returned; app integration must not weaken prerequisites, standards, caps, or maintenance blocks.
- Persistence must store source facts and evidence, not UI-local progression flags.
- Transfer and retest workflows must preserve source standard visibility and fresh-equivalent requirements.

## Testing Expectations

When executable app integration behavior is added, use deterministic tests before implementation. High-value tests should prove:

- Startup initializes local JSON persistence and loads current app state without Android APIs or external services.
- Session preparation composes Persistence, Core, Content, and Runtime without duplicating rule, storage, runtime, or generation logic.
- Ineligible work is rejected because Core says it is ineligible, not because app integration reimplements the rule.
- Generated content is stored and then packaged into Runtime inputs without Runtime inventing content.
- Completed sessions produce Core evaluation inputs and Persistence records while preserving failed, timed-out, and abandoned outcomes.
- Atomic completion commits keep branch state, evidence, attempt, session history, and generated-instance updates consistent.
- App-facing summaries can be recomputed from local facts and Core decisions.
- No workflow uses accounts, sync, backend services, telemetry, analytics, notifications, AI/API calls, network access, SQLite, Room, SharedPreferences progression state, or Android UI APIs.

## Future Work Rules

1. Add executable app integration workflows only when a concrete workflow is requested.
2. Keep it as a pure C# layer suitable for Android, CLI, tests, or other frontends.
3. Reference Core, Persistence, Runtime, and Content instead of duplicating their responsibilities.
4. Add tests at this boundary for orchestration behavior; add tests in the owning lower layer for rules, storage, live execution, or generated content behavior.
5. Keep future Android screens thin: render app-facing state, pass user actions into app integration, and avoid direct rule, storage, runtime, or content orchestration in screens.
