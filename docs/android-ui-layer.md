# MentalGymnastics Android UI Layer

**Status:** Android UI layer reference

**Project:** `src/MentalGymnastics.Android`

**Depends on:** [Android UI Strategy](android-ui-strategy.md), [Android Visual State Language](android-visual-state-language.md), [Android Component Inventory](android-component-inventory.md), [Android Screen Plans](android-screen-plans.md), [Pre-UI App Integration Boundary](app-integration-boundary.md), [Core Library](core-library.md), [Local Persistence Boundary](local-persistence-boundary.md), [Session Runtime Boundary](session-runtime-boundary.md), [Generated Content Boundary](generated-content-boundary.md)

This document defines the Android UI layer as a host and presentation boundary. It describes what native Android code owns, which app-facing services it consumes, and which decisions must stay in Core, Persistence, Runtime, Content, and App integration.

Android exists to make the program visible and operable. It must not become a second training program.

## Dependency Rule

The intended dependency direction is:

1. `MentalGymnastics.Core` owns program rules.
2. `MentalGymnastics.Persistence` owns local offline storage.
3. `MentalGymnastics.Runtime` owns live session execution.
4. `MentalGymnastics.Content` owns deterministic generated drill material.
5. `MentalGymnastics.App` composes those layers into pre-UI workflows and read models.
6. `MentalGymnastics.Android` renders app-facing state and forwards user actions.

Android may reference `MentalGymnastics.App` and render lower-layer types returned by app read models. Android screens should not directly orchestrate Core, Persistence, Runtime, or Content workflows when an app-layer service exists.

If a screen needs a missing workflow or read model, add it to `MentalGymnastics.App` first. If the missing behavior is a rule, storage operation, runtime behavior, or content-generation behavior, add it to the owning lower layer with tests before exposing it to Android.

## Ownership Split

| Layer | Owns | Android must not duplicate |
| --- | --- | --- |
| Core | Branch/level vocabulary, standards, gates, readiness, ownership, stabilization, maintenance, decay, dependency caps, global balance, transfer, recovery, deload, weekly planning, failure routing, practitioner classification, and global review. | Prerequisite checks, gate outcomes, ownership conversion, decay/restoration decisions, transfer eligibility, recovery/deload decisions, weekly plan decisions, or global review rules. |
| Persistence | App-owned local JSON database, local stores, repository queries, transactions, integrity validation, active runtime snapshots, generated instance records, progress summaries, and local backup/restore packages. | SharedPreferences, Room, parallel JSON files, screen-local progress flags, local evidence mirrors, backup package parsing, database writes, or integrity validation. |
| Runtime | Session definitions, lifecycle, phase scheduling, timers, cue scheduling, command validation, response timing, event logs, scoring facts, evidence capture, completion results, and snapshot/restore safety. | Independent phase timers, cue schedulers, command state machines, hidden evidence logs, scoring, pass/fail classification, or completion result generation. |
| Content | Deterministic local generated material, content identity, equivalence/freshness, content validation, runtime packages, and persistence handoffs. | Screen-local prompts, random cue streams, easier variants, freshness decisions, transfer-content shortcuts, or generated-content identity schemes. |
| App | Startup, current-state loading, next-work selection, generated-content preparation, runtime-session preparation, live-session controller state, completion processing, active-session snapshot handling, local backup/restore workflow, and progress refresh. | Parallel orchestration paths from screens, direct lower-layer composition in `Activity` or `View` code, or UI-granted progress. |
| Android | Native views, navigation shell, visual hierarchy, accessibility presentation, platform lifecycle forwarding, ephemeral reveal/selection/input state, and invoking app workflows. | Any durable program fact, rule decision, database behavior, live protocol behavior, or generated-content behavior. |

## What Android Owns

Android owns presentation and platform mechanics:

- Native `Activity`, view, shell, and reusable component construction.
- Navigation between Work, Map, Progress, Evidence, Maintenance, Global Review, Local Data, Branch Detail, Session Start, Live Session, and Result surfaces.
- Visual state treatment using shape, position, density, icon, hierarchy, contrast, and restrained motion.
- Short visible labels, accessibility labels, reveal-on-demand detail, and destructive-action confirmation UI.
- Ephemeral UI state such as selected tab, selected branch/level, expanded panels, input text, restore confirmation arming, and loading/error display.
- Compact navigation labels and wrapped chip rows for small screens, with full accessibility labels where visible text is abbreviated.
- Platform lifecycle handling that asks app integration to persist or restore active session state.
- UI refresh polling that requests runtime-owned live state from app integration.

Android may use a UI refresh timer to call `RefreshLiveSessionAsync`. That timer is only a render update mechanism. It must not decide phase completion, cue timing, response deadlines, scoring, evidence, timeout, or pass/fail status.

## Current Android Host Shape

The current Android host uses:

- `MainActivity` as the platform lifecycle and event bridge.
- `MentalGymnasticsAndroidHost` as the Android adapter over app integration services.
- `MgNavigationShell` as the minimal navigation and screen rendering shell.
- Reusable visual components under `src/MentalGymnastics.Android/Ui/Components`.

`MentalGymnasticsAndroidHost` supplies app-owned storage paths, calls app-layer workflows, and returns Android snapshots:

- `AndroidTrainingStateSnapshot`
- `AndroidSessionStartSnapshot`
- `AndroidLiveSessionSnapshot`
- `AndroidLiveSessionCompletionSnapshot`
- `AndroidActiveSessionResumeSnapshot`
- `AndroidLocalDataOperationSnapshot`

These snapshots are presentation inputs, not new domain authority. They should remain thin wrappers around app read models, capabilities, local path display data, and local backup state.

## Navigation

The primary navigation model follows the screen plans:

- **Work:** home/today, active resume, current next work, and highest-priority blocker.
- **Map:** branch ladder and branch detail.
- **Progress:** owned levels, passed-once states, stabilization, maintenance currency, decay/restoration, blocked advancement, transfer status, and global balance.
- **Evidence:** evidence timeline, sessions, formal attempts, stabilization, transfer, maintenance, review, failures, abandonments, and timeouts.
- **Maintenance:** due checks, warnings, decayed branches, dependency caps, and restoration requirements.
- **Review:** global review inputs and decisions.
- **Local Data:** local backup export, validation, restore, and integrity status.

Session Start, Live Session, Result, and Branch Detail are workflow surfaces opened from the primary areas. There is no landing page, content feed, account area, streak page, social area, notification center, analytics page, backend surface, or AI coach surface.

Primary navigation uses short visible labels on narrow screens and full accessibility labels for the underlying destinations. Screen changes reset the shared scroll surface to the top so a previous scroll position cannot hide blocker, failure, standard, or local-data status on the newly selected screen. Live-session refreshes preserve scroll position because timer updates are render-only updates, not navigation.

## Screen Responsibilities

### Home/Today

Home renders the current actionable training state from `CurrentTrainingStateReadModel`, active-session resume state, due maintenance, blocked advancement, recovery/deload, test readiness, and local data status where relevant.

It may start app-eligible work or route to a blocker, but it must not expose a free library as the primary action or infer eligibility from UI state.

### Branch Ladder and Branch Detail

Map surfaces render `BranchLevelStatus`, due maintenance, blocked advancement, evidence summaries, progress records, and app-exposed next work.

They may reveal standards, prerequisites, recent evidence, and blocker detail. They must not visually equate passed once with owned, hide decay, or let a user start advancement when Core/App blocks it.

### Session Start

Session Start renders `PreUiTrainingWorkflowPreparationResult`, `SelectedTrainingWork`, generated-content readiness, runtime-session readiness, load variables, expected evidence, standard, and honesty constraint.

The user must see the standard and honesty constraint before start. The screen cannot lower standards, edit load variables, bypass prerequisites, create generated content, or start Runtime when `CanStartRuntimeSession` is false.

### Live Session

Live Session renders `PreUiLiveSessionState`: current phase, timer state, active cue, materials, available commands, last command result, lifecycle status, and evidence counters.

The screen forwards commands such as finish phase, respond to cue, submit answer, mark drift, mark guess, mark error, correct, pause, resume, start audit, and abandon. It does not own command availability, timer state, cue identity, response windows, scoring, evidence classification, or completion.

Live presentation follows an attention budget: setup may explain, review may measure, and executing phases show only the current stimulus, programmed interference, and immediate response. Direct and AI-wrapped focus holds state their Runtime-owned duration before start, then use a dedicated full-screen target surface with whole-screen wander marking; they do not render an active timer, countdown, labeled pad, evidence, or lifecycle chrome. An accepted wander tap may produce one brief non-counting visual pulse plus haptic feedback, but no persistent count or status label. TI screens render only the component payload selected by Runtime's active phase. Android must not reintroduce hidden/future materials through expandable dashboards.

Direct Focus Holds end automatically when their timed active phase ends and do not have a practitioner-facing review phase. Result presentation shows only hold duration and wander count. Raw event counts, evidence-fact counts, expected-fact ratios, target-substitution self-reports, and transient completion-loading panels are not practitioner-facing information. Legacy snapshots already in the obsolete direct-Focus-Hold review phase complete without presenting a question.

Structured visual material must remain visual all the way to the practitioner. Content owns the deterministic visual-stimulus specification and codec; App presentation mapping decodes that handoff into structured, renderable data; Android draws the specified shape, color, fill, size, direction, mark, position, orientation, and border that are relevant to the instance. FS targets and cues, IR cues and exceptions, and DE comparison pairs must use the shared visual renderer rather than a text view containing a description of the object.

The serialized codec is never user-facing material. Android must not expose it in preflight lists, live cues, response choices, rule-declaration forms, corrections, review, errors, or accessibility text. If an app read model does not provide a decodable structured presentation for material that claims to be visual, fix the App/Content handoff rather than falling back to the raw string. Accessibility labels should be concise natural descriptions generated from the structured object; they do not justify adding a visible caption beside a visual stimulus.

Android must not repair visual semantics with string manipulation. Removing a token such as `left`, shortening an encoded value, or parsing a descriptor ad hoc can either preserve an untested feature or delete a genuinely tested one. The renderer follows typed features, and position changes the actual layout only when the generated instance declares position as a tested or controlled feature. Descriptor prose is an acceptable visible stimulus only when reading or semantic language is the documented demand.

Command buttons render runtime/app command availability. Timed phases cannot be manually advanced before Runtime reports that the scheduled duration has elapsed.

### Result

Result renders `PreUiLiveSessionCompletionResult` and `PreUiTrainingWorkflowCompletionResult`.

It must distinguish completed, failed, abandoned, timed-out, pass once, stabilization, owned, maintenance pass, warning, decayed, transfer failed, recovery, blocked, and no advancement states as returned by app/core/runtime results. It must not soften failure or treat ordinary completion as progress.

### Progress

Progress renders local user-visible progress from app/persistence read models: branch states, ownership, passed-once states, stabilization progress, maintenance currency, decay/restoration, recent failures, bottlenecks, test readiness, transfer, global balance, and review decisions where available.

Progress is not analytics. It must not show streaks, points, engagement metrics, social comparison, or backend-derived summaries.

### Evidence Review

Evidence Review renders local evidence artifacts, sessions, attempts, stabilization passes, transfer artifacts, maintenance checks, decay/restoration records, and global-review artifacts surfaced through app read models.

Evidence must stay tied to branch, level, drill, session, standard, critical constraint, score/rubric, and failure classification where those facts exist. Subjective notes may be shown as notes, but not as advancement evidence.

### Maintenance and Decay

Maintenance surfaces render due checks, warnings, decayed branches, dependency caps, and restoration requirements from app/core/persistence outputs.

The UI must not let the user dismiss decay, mark maintenance current manually, or restore dependent advancement without evidence.

### Global Review

Global Review renders existing app/core review inputs and decisions: owned levels, maintenance status, recent failures, evidence artifacts, bottleneck branch, volume/intensity/recovery/deload history, and review decision where available.

Global review is not a free-form self-assessment. Android displays app/core decisions and routes to programmed work.

### Local Backup and Restore

Local Data renders app-layer backup/restore read models and operation results. Restore must be explicit, local, integrity-validated, and blocked during active live sessions.

Android must not add cloud backup, accounts, remote storage, sync, telemetry, analytics, notifications, AI repair, or backend services.

## Lifecycle Handling

Android lifecycle events must preserve honesty:

- `OnPause` and `OnSaveInstanceState` should suspend active sessions through app integration.
- If runtime pause is available, Android may request pause through app/runtime command handling.
- If pause is not available, Android asks app integration to persist an active runtime snapshot.
- Rotation, process recreation, and return-to-app must restore only through app active-session snapshot services and Runtime restore rules.
- Unsafe, missing, terminal, or non-resumable snapshots are surfaced as app/runtime states, not converted into success.
- Active snapshots are never completed-session evidence, formal attempts, stabilization passes, maintenance passes, ownership, restoration, or gate outcomes.
- Restore invalidation may clear only the active snapshot; it must not delete completed history or evidence.

If Android cannot honestly resume a session, it should show the unsafe or abandoned state and route to the programmed next action. It must not pretend the session completed successfully.

## Offline Constraints

Android remains offline-first and userless:

- Use the app-owned files directory for the local database path supplied to `AppStartupConfiguration`.
- Use app/persistence backup services for local backup and restore.
- Do not add accounts, sync, backend services, remote storage, telemetry, analytics, push notifications, AI/API dependencies, social features, or cloud repair.
- Do not replace the current local JSON persistence with SQLite, Room, or SharedPreferences without an explicit storage requirement and Persistence-layer change.
- Do not persist practitioner state, branch-level states, sessions, evidence, generated instances, active snapshots, or progress summaries in Android-owned stores.

Local backup files may live in Android-accessible local app storage, but the backup package and restore validation belong to Persistence and App integration.

## Local Progress Visibility

Android should make local progress visible without becoming a motivational dashboard.

Visible progress should come from:

- `CurrentTrainingStateReadModel.CurrentPractitionerState`
- `BranchLevelStates`
- `DueMaintenance`
- `RecentSessions`
- `EvidenceSummaries`
- `ProgressRecords`
- `CategoryClassification`
- `WeeklyPlan`
- `BlockedAdvancement`
- `AvailableNextWork`
- Completion and result read models returned by app workflows

Android may format, filter, group, sort, and reveal this information. It may not store or infer a second progress state from taps, elapsed time, view completion, scroll position, local preferences, or component flags.

## Anti-Self-Deception UI Guards

Android screens must preserve these guards:

- A visible completed screen is not progress.
- Button taps are not evidence.
- Time spent in a screen is not evidence.
- Failed, timed-out, abandoned, unsafe, invalidated, or incomplete sessions do not become successful evidence.
- Passed once must never look owned.
- Stabilization must look unfinished until app/core ownership is granted.
- Decay and blocked advancement must be visible at top-level hierarchy and on the affected node or edge.
- Standards, load, honesty constraints, and critical constraints must be shown before tests, stabilization, transfer, maintenance, and review-relevant work.
- The UI cannot lower standards, hide failed constraints, remove required evidence, skip prerequisites, dismiss decay, or route around dependency caps.
- Transfer must show preserved source standard plus changed context; novelty alone is not transfer.
- Recovery and deload are reduced-load programming states, not advancement.
- Local backup restore must warn that it replaces local data and must show validation status.

## Testing Expectations

Prefer deterministic tests around workflow contracts rather than brittle visual snapshots:

- Android-facing screens consume app read models and app workflow results.
- Blocked preparation does not expose start actions.
- UI-only actions cannot grant advancement or mutate branch-level state.
- Live session commands are forwarded to Runtime through app integration, and invalid commands do not create evidence.
- Structured FS, IR, and DE presentation renders the decoded visual objects and never exposes serialized visual-stimulus values or descriptor prose in their place.
- Active session snapshots survive lifecycle events when Runtime permits restore.
- Unsafe restore is visible and non-successful.
- Result surfaces preserve failed, abandoned, timed-out, pass-once, stabilization, ownership, maintenance, warning, decay, and no-advancement distinctions.
- Local backup/restore remains local, explicit, and integrity-validated.

If a test reveals a mismatch between documentation and implemented behavior, fix the narrow mismatch in the owning layer or documentation before expanding UI behavior.

## Implementation Checklist

Before adding or changing an Android screen:

1. Read this document and the App/Core/Persistence/Runtime/Content boundary docs.
2. Identify the exact app-layer read model or workflow the screen consumes.
3. Confirm the screen does not call lower-layer rules, stores, runtime internals, or content generation directly.
4. Confirm any UI refresh timer only refreshes runtime-owned state.
5. Confirm all durable program facts remain in Persistence through App integration.
6. Confirm standards, honesty constraints, failure, decay, blocked advancement, stabilization, transfer, and maintenance remain visible.
7. Add or update deterministic tests at the owning layer for behavior changes.
