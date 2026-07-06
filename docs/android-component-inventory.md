# MentalGymnastics Android Component Inventory

**Status:** Component inventory
**Applies to:** Future Android reusable UI components
**Depends on:** [Android UI Strategy](android-ui-strategy.md), [Android Visual State Language](android-visual-state-language.md), [Complete Training Program](program/training-program.md), [Core Library](core-library.md), [Pre-UI App Integration Boundary](app-integration-boundary.md)

This document defines the reusable Android UI components needed before full native screens are designed or implemented.

The inventory is intentionally visual-first and low-text. Components should show state, readiness, failure, blocked advancement, stabilization, maintenance, decay, recovery, and transfer through shape, position, icon, density, motion, contrast, and hierarchy. Text is used only where the user needs an exact standard, an honesty constraint, a blocker reason, a destructive restore warning, or a reveal-on-demand explanation.

## Boundary

Android components render app-layer read models and forward user actions into app/runtime commands. They do not own progression rules, generated prompts, cue schedules, timers, scoring, evidence classification, local storage paths, completion processing, or advancement decisions.

Components must not introduce marketing pages, onboarding fluff, streaks, social UI, accounts, sync, telemetry, notifications, analytics, backend services, or AI/API dependencies.

The default data source is `MentalGymnastics.App`. If a component needs information that is not exposed there, add an app-layer read model or workflow first, with lower-layer behavior remaining in Core, Persistence, Runtime, or Content as defined by their boundaries.

## Shared App-Layer Data

The components below may consume these app-layer outputs:

- `CurrentTrainingStateReadModel`: practitioner state, branch-level states, due maintenance, recent sessions, evidence summaries, progress records, category classification, weekly plan, blocked advancement, and available next work.
- `BranchLevelStatus`: branch-level state rendered by branch tiles, level cells, rails, markers, and maintenance indicators.
- `CurrentTrainingStateBlocker` and `NextTrainingWorkBlocker`: explicit reasons that advancement or work selection is blocked.
- `CurrentTrainingStateNextWork`: weekly-session placement and branch emphasis for allowed upcoming work.
- `NextTrainingWorkSelection`: selected work, selection status, blockers, readiness, transfer eligibility, recovery, and deload decisions.
- `SelectedTrainingWork`: branch, level, drill, session type, demand, standard, honesty constraint, load variables, and whether advancement work is allowed.
- `SelectedWorkGeneratedContentPreparationResult`: generated-content preparation status, generated content, runtime package, persistence handoff, and rejections.
- `SelectedWorkRuntimeSessionPreparationResult`: runtime session definition, phase plan, cue schedule, input options, input materials, expected evidence facts, and rejections.
- `PreUiTrainingWorkflowPreparationResult`: combined work-selection, generated-content, and runtime-session readiness for a startable session.
- `PreUiTrainingWorkflowStartResult`: started session id, command handler, cue scheduler, active session, and start status.
- `PreUiActiveSessionResumeState`: resumable session status, session identity, lifecycle status, active phase, pending cues, runtime event count, and evidence fact count.
- `PreUiTrainingWorkflowCompletionResult` and `CompletedRuntimeSessionProcessingResult`: completion status, standard evaluation, formal gate decision, stabilization ownership, maintenance currency, decay, transfer eligibility, failure response, state transition, and refreshed state.
- `ApplicationIntegrationCapabilities`: app boundary flags confirming offline-only, userless, local operation and the absence of accounts, sync, backend, telemetry, notifications, analytics, and AI/API dependencies.

Backup and restore controls consume app-layer backup/restore read models and operation results. Those read models may wrap persistence backup and integrity results, but Android components must not talk directly to new Android-owned files, `SharedPreferences`, Room entities, cloud storage, accounts, or sync services.

## Components

### Branch Tile

**Use:** A compact entry point for one training branch in dashboard, review, and selection surfaces.

**Consumes:** `CurrentTrainingStateReadModel.BranchLevelStates` filtered by branch, `DueMaintenance`, `BlockedAdvancement`, `AvailableNextWork`, `ProgressRecords`, `CategoryClassification`, and the relevant branch facts from `CurrentPractitionerState`.

**Visual communication:** The branch tile uses the branch code as the dominant label, a small row or stack of level cells, and a clear overlay when the branch has due maintenance, decay, or blocked advancement. Branch readiness is shown by density and structure, not by encouragement copy. If the branch is the next scheduled emphasis, the tile may lift slightly in hierarchy or position.

**Minimal visible labels:** Branch code, current level marker, and short urgent statuses such as `Due`, `Blocked`, or `Decayed`.

**Must not:** Show streaks, total time practiced, inspirational language, social comparison, or a vague progress percentage disconnected from standards.

### Level Cell

**Use:** The reusable unit for one branch level inside ladders, rails, branch tiles, and review views.

**Consumes:** One `BranchLevelStatus`, matching `CurrentTrainingStateBlocker` entries, matching due-maintenance records, and any relevant evidence summaries or recent-session records surfaced through `CurrentTrainingStateReadModel`.

**Visual communication:** The cell follows the Android visual state language: unopened is hollow and quiet; training is open and active; test-ready has a gate cue; passed once is visibly provisional; stabilizing is segmented; owned is structurally complete; maintenance is marked as serviceable but not new progress; decayed is fractured; blocked is interrupted by a hard barrier. Passed once must never use the same fill, weight, or completion treatment as owned.

**Minimal visible labels:** Level id such as `L1` and, when unavoidable, a tiny count such as `1/3` for stabilization progress.

**Must not:** Infer state from UI-local flags, hide decay, or collapse blocked and merely unavailable states into the same treatment.

### State Marker

**Use:** A small reusable marker attached to branch tiles, level cells, action buttons, rails, and summaries.

**Consumes:** `BranchLevelStatus`, `NextTrainingWorkSelection.Kind`, `NextTrainingWorkBlocker`, `CurrentTrainingStateBlocker`, `PreUiActiveSessionResumeState.Status`, and completion statuses exposed by `CompletedRuntimeSessionProcessingResult`.

**Visual communication:** State markers use icon, outline, shape, and position before text. Blocked uses a hard stop/barrier marker; decayed uses a fracture or drop marker; stabilization uses a segmented marker; transfer uses a bridge marker; review uses a loop or inspection marker. The marker is an annotation on app-layer state, not the source of that state.

**Minimal visible labels:** None by default. Use short labels only for high-salience conditions: `Blocked`, `Decayed`, `Due`, `Review`.

**Must not:** Rely on color alone or turn failure into a decorative warning that can be missed.

### Timer Ring

**Use:** A live session timing component for phases, cue windows, response windows, and recovery intervals.

**Consumes:** `SelectedWorkRuntimeSessionPreparationResult.PhasePlan`, `CueSchedule`, and `InputOptions`; live state from `PreUiTrainingWorkflowStartResult.CommandHandler`, `CueScheduler`, and active session snapshots; resume state from `PreUiActiveSessionResumeState.ActivePhaseId`, `ActivePhaseKind`, and pending cue ids.

**Visual communication:** The ring shows the current phase boundary, remaining or elapsed time, and deadline pressure through ring completeness, tick marks, pulse restraint, and contrast changes. It should visually distinguish timed evidence windows from rest or instruction phases.

**Minimal visible labels:** A short phase label only when the icon is ambiguous, plus exact time when timing precision matters.

**Must not:** Maintain an independent screen-local phase timer, schedule cues itself, score performance, or display time as an achievement mechanic.

### Cue Panel

**Use:** The focused live-work component for generated materials, current cue, target hold, response options, or active rule.

**Consumes:** `PreUiTrainingWorkflowPreparationResult.GeneratedContent`, `SelectedWorkGeneratedContentPreparationResult.GeneratedContent`, `RuntimePackage`, `SelectedWorkRuntimeSessionPreparationResult.InputMaterials`, `ExpectedEvidenceFacts`, `CueSchedule`, and live cue state from `PreUiTrainingWorkflowStartResult.CueScheduler` or app-exposed runtime snapshots.

**Visual communication:** The panel presents one active cue or material group at a time, with clear response affordance and minimal surrounding text. Timing pressure, current target, inhibition/no-go state, and response acceptance are shown through placement, marker changes, and component density. Reveal-on-demand can expose generated-content identity, equivalence class, and expected evidence facts for audit or debugging surfaces.

**Minimal visible labels:** Only the cue content itself, short command words when needed, and terse response options.

**Must not:** Generate prompts in Android, invent hidden variants, decide freshness/equivalence, or keep a hidden screen-local evidence log.

### Evidence Strip

**Use:** A compact record of what happened in a session, level, branch, review, or result summary.

**Consumes:** `CurrentTrainingStateReadModel.EvidenceSummaries`, `RecentSessions`, `CompletedRuntimeSessionProcessingResult.EvidenceArtifacts`, `FormalTestAttempt`, `StabilizationPass`, `MaintenanceCheck`, `StandardEvaluationResult`, `TransferEligibilityResult`, and `FailureResponse`.

**Visual communication:** The strip uses ordered ticks or small chips for attempts, passes, failures, correction windows, guess markers, drift, audit results, stabilization passes, maintenance checks, and transfer evidence. Evidence quality is shown with density and shape: thin ticks for ordinary attempts, stronger ticks for formal evidence, broken ticks for invalid or failed evidence, and grouped ticks for stabilization sequences.

**Minimal visible labels:** Terse codes only where needed, such as `Fail`, `Pass`, `Guess`, `Drift`, `Audit`, or `Transfer`.

**Must not:** Summarize private effort as proof of ability, hide failed attempts, or present evidence as motivational history.

### Progress Rail

**Use:** A visual ladder or path connecting level cells within a branch, across dependencies, or through weekly work.

**Consumes:** `CurrentTrainingStateReadModel.BranchLevelStates`, `BlockedAdvancement`, `DueMaintenance`, `AvailableNextWork`, `WeeklyPlan`, `ProgressRecords`, and relevant `CurrentTrainingStateNextWork` entries.

**Visual communication:** The rail shows where the practitioner is allowed to work, where progression has stopped, where maintenance is due, where decay has interrupted ownership, and where transfer bridges exist. Hard blockers break the rail. Review nodes sit off the main advancement path. Recovery or deload work appears as a side route, not as advancement.

**Minimal visible labels:** Branch code, level id, and urgent state labels only.

**Must not:** Smooth over dependency caps, global balance constraints, or blocked advancement to make the path look more complete than it is.

### Maintenance Badge

**Use:** A marker for due maintenance, maintenance checks, preserved ownership, warning states, and decay.

**Consumes:** `CurrentTrainingStateReadModel.DueMaintenance`, relevant `BranchLevelStatus`, `CompletedRuntimeSessionProcessingResult.MaintenanceCheck`, `MaintenanceCurrencyResult`, and `DecayResult`.

**Visual communication:** The badge uses a service or inspection shape, countdown density, and contrast to separate routine maintenance from urgent decay. Maintenance due should be visible before the user opens detail. Decay should interrupt the owning component and reduce its hierarchy as owned skill until restoration occurs.

**Minimal visible labels:** `Due`, `Warn`, or `Decayed` when the visual marker alone is not enough.

**Must not:** Treat maintenance as a reward, an engagement loop, or a streak-preservation device.

### Blocker Marker

**Use:** A high-salience interruption marker for advancement, work selection, preparation, runtime start, or restore paths that cannot proceed.

**Consumes:** `CurrentTrainingStateBlocker`, `NextTrainingWorkBlocker`, `NextTrainingWorkSelection.Blockers`, `PreUiTrainingWorkflowPreparationResult.Rejections`, `SelectedWorkGeneratedContentPreparationResult.Rejections`, `SelectedWorkRuntimeSessionPreparationResult.Rejections`, and weekly-programming constraint data exposed by `CurrentTrainingStateReadModel.WeeklyPlan`.

**Visual communication:** The marker is impossible to miss: a barrier shape crosses the blocked action, rail segment, tile edge, or level cell. It uses stronger hierarchy than ordinary unavailable states. Reveal-on-demand shows the exact blocker source, such as dependency cap, test readiness, global balance, maintenance currency, transfer eligibility, weekly programming, or decay.

**Minimal visible labels:** `Blocked` plus a short reason on reveal, such as `Dependency`, `Balance`, `Readiness`, `Maintenance`, or `Decay`.

**Must not:** Hide blockers behind disabled-button styling alone or allow a user to start advancement work from a blocked UI state.

### Session Action Button

**Use:** The reusable command component for starting, resuming, pausing where allowed, submitting responses, marking correction facts, abandoning, or moving through runtime phases.

**Consumes:** `NextTrainingWorkSelection.Kind`, `SelectedTrainingWork`, `PreUiTrainingWorkflowPreparationResult.CanStartRuntimeSession`, `PreUiTrainingWorkflowStartResult.Status`, `PreUiActiveSessionResumeState.CanResume`, `SelectedWorkRuntimeSessionPreparationResult.InputOptions`, and command availability exposed by the runtime command handler through the app workflow.

**Visual communication:** The button uses icon-first commands and a strong enabled/disabled hierarchy. Startable work uses a direct action shape; resumable work uses a return marker; blocked work shows the blocker marker attached to the button, not a vague inactive state. Response buttons inside a live session are spatially stable so timing and motor behavior are not distorted by text changes.

**Minimal visible labels:** Short verbs only: `Start`, `Resume`, `Submit`, `Pause`, `Mark`, `Abandon`.

**Must not:** Grant progress, bypass app preparation, continue a session after unsafe restore, or create motivational engagement actions.

### Standard Panel

**Use:** A compact standards-and-constraints panel for selected work, test readiness, live sessions, and result review.

**Consumes:** `SelectedTrainingWork.Demand`, `Standard`, `HonestyConstraint`, `LoadVariables`, `Drill`, `NextTrainingWorkSelection.TestReadiness`, `TransferEligibility`, generated-content constraints from `SelectedWorkGeneratedContentPreparationResult`, and expected evidence from `SelectedWorkRuntimeSessionPreparationResult.ExpectedEvidenceFacts`.

**Visual communication:** The panel shows the demand, standard, honesty constraint, and load variables as compact structured rows or chips. The visible state should emphasize what must be demonstrated, not why the user should feel motivated. Detail such as full standard text, critical constraints, generated-content identity, and readiness failure details should be reveal-on-demand.

**Minimal visible labels:** `Standard`, `Constraint`, and short load-variable values when necessary for exactness.

**Must not:** Replace formal standards with subjective difficulty labels or hide honesty constraints behind decorative copy.

### Result Summary

**Use:** The post-session and review component that shows what the session changed, what it did not change, and what should happen next.

**Consumes:** `PreUiTrainingWorkflowCompletionResult.ProcessingResult`, `RefreshedState`, and all relevant `CompletedRuntimeSessionProcessingResult` outputs: `CompletionStatus`, `StandardEvaluationResult`, `FormalGateDecision`, `StabilizationOwnershipResult`, `MaintenanceCurrencyResult`, `DecayResult`, `TransferEligibilityResult`, `FailureResponse`, `StateTransition`, and `ProgressSummary`.

**Visual communication:** The summary leads with the true outcome: failed, passed once, stabilizing, owned, maintenance preserved, maintenance warning, decayed, transfer eligible, transfer failed, recovery prescribed, or no advancement. It pairs the state marker with an evidence strip and a constrained next action. Failure and blocked advancement stay visible even when the session completed normally.

**Minimal visible labels:** Exact outcome labels are unavoidable: `Failed`, `Passed once`, `Stabilizing`, `Owned`, `Decayed`, `Recovery`, `Blocked`, `No advancement`.

**Must not:** Congratulate completion as progress, make passed once look owned, or obscure failure response behind positive language.

### Backup/Restore Controls

**Use:** Utility controls for local backup export, restore, validation, and recovery from local persistence issues.

**Consumes:** `ApplicationIntegrationCapabilities`, Android local-data snapshots from the app host, and app-layer backup/restore workflow results that wrap persistence backup, restore, and integrity-validation results.

**Visual communication:** Controls are plain and operational: export, restore, validate, selected local file, last backup time, restore risk, and validation status. Restore uses a high-contrast destructive-action treatment and requires explicit confirmation because it can replace local state.

**Minimal visible labels:** `Export`, `Restore`, `Validate`, file name/date, and exact validation or restore status.

**Must not:** Offer cloud backup, sync, accounts, remote storage, telemetry, analytics, automatic upload, social sharing, or restore without validation and explicit destructive confirmation.

## Reveal-On-Demand Rules

Visible surfaces should stay sparse. Detail is revealed only when it changes a decision or supports audit:

- Standards, honesty constraints, and load variables may reveal full text from `SelectedTrainingWork`.
- Blockers reveal source and exact detail from `CurrentTrainingStateBlocker` or `NextTrainingWorkBlocker`.
- Evidence reveals artifact detail, attempt detail, and failure response from app completion results.
- Generated-content and runtime details reveal identity, cue schedule, expected evidence facts, and rejection reasons only for session preparation, audit, review, or debugging surfaces.
- Backup and restore reveal local file paths, validation findings, and destructive restore consequences only inside the utility flow.

Reveal-on-demand must not become a substitute for visible failure, decay, due maintenance, or blocked advancement. Those states remain visible at the component level.

## Implementation Guardrails

- Use app-layer outputs as component inputs; do not bind components directly to Core, Persistence, Runtime, or Content services unless an app-layer workflow explicitly returns that object for rendering or command forwarding.
- Do not store component-owned progression state, generated-content state, session logs, evidence facts, maintenance flags, blocked flags, or backup metadata in Android UI.
- Do not infer readiness, ownership, decay, transfer eligibility, or recovery from visual state. Render decisions already made by the app/core layers.
- Keep labels short and exact. Prefer icons, shape, position, density, contrast, hierarchy, and motion for state communication.
- Keep all components offline-only and local-device scoped.
