# MentalGymnastics Android Screen Plans

**Status:** Low-fidelity screen plan

**Applies to:** Future Android main workflow screens

**Depends on:** [Android UI Strategy](android-ui-strategy.md), [Android Visual State Language](android-visual-state-language.md), [Android Component Inventory](android-component-inventory.md), [Complete Training Program](program/training-program.md), [Pre-UI App Integration Boundary](app-integration-boundary.md)

This document plans the main Android workflow without implementing UI code. It defines what each screen should make visible, which app-layer data it should consume, and how it prevents the practitioner from mistaking participation, novelty, or completion for earned progress.

The first screen is actionable training state. There is no landing page, marketing page, account prompt, onboarding carousel, feed, streak surface, social surface, notification surface, analytics surface, backend surface, or AI/API surface.

## Screen Model

Android screens are thin renderers over `MentalGymnastics.App` workflows and app-facing read models. They may choose visual hierarchy and collect input, but they must not own progression decisions, local storage paths, generated content, cue schedules, timers, evidence classification, completion processing, maintenance/decay decisions, transfer eligibility, recovery, deload, weekly planning, or global review decisions.

The main workflow is:

1. **Home/Today:** show current prescribed work, resume, blocker, maintenance, or review.
2. **Branch Ladder:** inspect the whole branch map and blocked edges.
3. **Branch Detail:** inspect one branch-level path and allowed next action.
4. **Session Start:** present the standard, honesty constraint, generated material readiness, and start action.
5. **Live Session:** render runtime state and forward commands.
6. **Result:** show what changed, what failed, and what is next.
7. **Progress:** scan branch state, weekly emphasis, stabilization, transfer, and blockers.
8. **Evidence Review:** inspect attempts, artifacts, failures, and reviews.
9. **Maintenance/Decay:** resolve due checks, warnings, decay, restoration, and dependency caps.
10. **Global Review:** inspect whole-practitioner review inputs and decisions.
11. **Backup/Restore:** local export, restore, and integrity utility flow.

Use a compact primary navigation model: Work, Map, Evidence, Review, and Local Data. Work opens Home/Today by default.

## Shared Text Policy

Visible text should be short and exact. Use visual state language, component hierarchy, icons, rails, markers, evidence ticks, and reveal-on-demand detail before paragraph text.

Text is unavoidable for:

- Formal standards.
- Honesty constraints.
- Critical constraints.
- Generated drill material.
- Failure classification.
- Blocker reasons.
- Decay and restoration requirements.
- Transfer source standards.
- Backup/restore destructive warnings.
- Accessibility labels.

Collapsed detail may include full drill rationale, prerequisite chain, generated-content identity, runtime event logs, rubric tables, and backup validation details. Failure, decay, due maintenance, and blocked advancement must remain visible without expansion.

## Home/Today

**Purpose:** First actionable screen; answers what should happen now.

**Primary visual object:** A prescribed-work stack with one dominant work item, a compact branch-ladder preview, and an urgent-state strip for resume, maintenance, decay, blocker, recovery, deload, or review.

**Primary user action:** Start the allowed session, resume a safe active session, inspect the blocking reason, or enter restoration/maintenance work.

**App-layer data source:** `PreUiActiveSessionResumeState` for resumable work; `CurrentTrainingStateReadModel.AvailableNextWork`, `DueMaintenance`, `BlockedAdvancement`, `WeeklyPlan`, `CategoryClassification`, `BranchLevelStates`, `ProgressRecords`, and `RecentSessions`; `NextTrainingWorkSelection` when the user opens a specific work item.

**Low-fidelity layout:**

- Top: active resume or highest-priority programmed state.
- Center: next prescribed work item with session role icon, branch/level, load/transfer marker, and one action.
- Lower: branch preview rail and due/blocked/review markers.

**Minimal visible text:** `Resume`, `Start`, `Blocked`, `Decayed`, `Due`, session role, branch code, level id, and stabilization count such as `2/3`.

**Anti-self-deception guard:** Home must not present a free library as the main action. It shows app-eligible work first and makes decay, blocked advancement, recovery, deload, and due maintenance outrank novelty or user preference.

## Branch Ladder

**Purpose:** Whole-map inspection of branches, levels, dependencies, and blocked edges.

**Primary visual object:** A compact branch-by-level rail using level cells, gate edges, transfer bridges, maintenance badges, blocker markers, and review overlays.

**Primary user action:** Select a branch-level or blocker to inspect detail; choose an eligible next work item only when the app layer exposes it.

**App-layer data source:** `CurrentTrainingStateReadModel.BranchLevelStates`, `BlockedAdvancement`, `DueMaintenance`, `AvailableNextWork`, `WeeklyPlan`, `ProgressRecords`, and `CategoryClassification`.

**Low-fidelity layout:**

- Rows: branch codes `FH`, `FS`, `WM`, `IR`, `DE`, `CO`, `AI`, `TI`.
- Columns: global levels `L1` through `L5`.
- Edges: prerequisite and advancement gates, interrupted by blockers.
- Side strip: maintenance/decay/review priority.

**Minimal visible text:** Branch codes, level ids, `Blocked`, `Decayed`, `Due`, `Review`, and stabilization fractions.

**Anti-self-deception guard:** Passed once uses provisional treatment and locked outgoing edge. Owned uses a distinct completed node. Decayed prerequisites and blocked advancement are visible on both the affected node and dependent edge, so the map cannot visually overstate progress.

## Branch Detail

**Purpose:** Focused view of one branch, current level, standard, evidence, blockers, and allowed next action.

**Primary visual object:** One branch progression lane with the current branch-level node enlarged, evidence strip below it, and a standard panel attached to the eligible action.

**Primary user action:** Prepare eligible work, inspect the standard, reveal blocker detail, or route to maintenance/restoration.

**App-layer data source:** `CurrentTrainingStateReadModel` filtered to the branch; matching `BranchLevelStatus`, `RecentSessions`, `EvidenceSummaries`, `DueMaintenance`, and `BlockedAdvancement`; `NextTrainingWorkSelection` and `SelectedTrainingWork` for selected work.

**Low-fidelity layout:**

- Top: branch code and progression lane.
- Middle: current node, standard panel, evidence strip.
- Bottom: primary action or blocker marker with restoration route.

**Minimal visible text:** Branch/level, session role, `Standard`, `Constraint`, `Evidence`, and exact blocker or decay labels when present.

**Anti-self-deception guard:** The screen must show the actual branch-level state returned by app/core decisions. It may reveal drill purpose and history, but it must not let the practitioner start tests, transfer, or advancement work when readiness, prerequisites, maintenance, global balance, recovery, or deload blocks it.

## Session Start

**Purpose:** Preflight surface before runtime begins.

**Primary visual object:** A standard panel paired with a start action: branch/level/drill chip, session role, load variables, standard, honesty constraint, critical constraints, and generated-content readiness.

**Primary user action:** Start the prepared runtime session, return to branch detail, or reveal why preparation is rejected.

**App-layer data source:** `NextTrainingWorkSelection`, `SelectedTrainingWork`, `PreUiTrainingWorkflowPreparationResult`, `SelectedWorkGeneratedContentPreparationResult`, `SelectedWorkRuntimeSessionPreparationResult`, and their rejection lists.

**Low-fidelity layout:**

- Top: branch/level/drill and session role marker.
- Center: standard and honesty constraint in compact rows.
- Lower: generated material readiness, expected evidence shape, and start button.
- Reveal: critical constraints, load variables, content identity, freshness/equivalence, and rejection detail.

**Minimal visible text:** Standard and honesty constraint are visible enough to preserve exactness. Other labels stay terse: `Load`, `Evidence`, `Fresh`, `Start`.

**Anti-self-deception guard:** The start action is unavailable until app preparation succeeds. Android does not generate content, choose an easier variant, hide the source standard for transfer, or start runtime with missing standard/honesty constraint data.

## Live Session

**Purpose:** Sparse execution surface for active runtime work.

**Primary visual object:** Cue panel plus timer ring, current phase marker, evidence action row, and compact standard/constraint indicator.

**Primary user action:** Respond to cues, submit answers, mark drift, mark guess, correct within the allowed window, finish a phase, pause where runtime allows it, resume, start audit, or abandon.

**App-layer data source:** `PreUiTrainingWorkflowStartResult.CommandHandler`, `CueScheduler`, and active session; app-exposed runtime state, `SelectedWorkRuntimeSessionPreparationResult.PhasePlan`, `CueSchedule`, `InputOptions`, `InputMaterials`, `ExpectedEvidenceFacts`, and resume state from `PreUiActiveSessionResumeState`.

**Low-fidelity layout:**

- Top: phase icon and small standard/constraint marker.
- Center: active cue, material, target, memory item, rule, comparison pair, audit item, or transfer source context.
- Side or bottom: timer ring and stable response controls.
- Bottom: evidence action row and abandon/pause controls.

**Minimal visible text:** Generated material, active rule/target where required, short phase label, exact timer when needed, and short commands such as `Mark`, `Submit`, `Correct`, `Audit`, `Abandon`.

**Anti-self-deception guard:** The screen only renders runtime-owned phase, cue, timing, and command state. Invalid commands do not mutate visual progress, abandoned sessions do not become successful evidence, and opening detail does not pause or alter timing unless runtime explicitly allows pause.

## Result

**Purpose:** Post-session truth surface: what happened, what changed, and what is next.

**Primary visual object:** Result summary with outcome marker, evidence strip, branch-level transition marker, and constrained next action.

**Primary user action:** Continue to prescribed next work, inspect evidence, route to recovery/regression/restoration, or return to Home/Today.

**App-layer data source:** `PreUiTrainingWorkflowCompletionResult.RefreshedState` and `ProcessingResult`; `CompletedRuntimeSessionProcessingResult.CompletionStatus`, `StandardEvaluationResult`, `FormalGateDecision`, `StabilizationOwnershipResult`, `MaintenanceCurrencyResult`, `DecayResult`, `TransferEligibilityResult`, `FailureResponse`, `StateTransition`, and `ProgressSummary`.

**Low-fidelity layout:**

- Top: true outcome marker.
- Center: evidence strip and any branch-level state change.
- Lower: failure classification or gate decision, then one next programmed action.
- Reveal: standard evaluation detail, runtime event detail, artifact detail, and failure response rationale.

**Minimal visible text:** Exact outcome labels are required: `Failed`, `Passed once`, `Stabilizing`, `Owned`, `Maintenance`, `Decayed`, `Recovery`, `Blocked`, `No advancement`.

**Anti-self-deception guard:** Completion is not treated as progress. The outcome must distinguish failed, passed once, stabilizing, owned, maintenance warning, decay, transfer failure, and no advancement. Positive completion copy is avoided.

## Progress

**Purpose:** Scan progress without turning it into a motivational dashboard.

**Primary visual object:** Whole-practitioner progress board: branch rails, ownership markers, stabilization segments, transfer bridges, maintenance badges, blocker markers, and weekly emphasis.

**Primary user action:** Inspect a branch, inspect a blocker, open evidence behind a state, or open the next eligible work item.

**App-layer data source:** `CurrentTrainingStateReadModel.BranchLevelStates`, `ProgressRecords`, `WeeklyPlan`, `AvailableNextWork`, `BlockedAdvancement`, `DueMaintenance`, `RecentSessions`, `EvidenceSummaries`, and `CategoryClassification`.

**Low-fidelity layout:**

- Top: category and weekly programmed emphasis, not identity language.
- Center: branch progress rails with state markers.
- Lower: bottleneck, stabilization, transfer, and maintenance strips.

**Minimal visible text:** Branch/level, `Owned`, `Stab 2/3`, `Transfer`, `Due`, `Blocked`, `Decayed`, and weekly day/session role.

**Anti-self-deception guard:** The screen does not show streaks, points, total minutes, achievement counts, or broad completion percentages. It shows only standards-backed states, blockers, evidence-backed changes, and app/core planning outputs.

## Evidence Review

**Purpose:** Audit recorded work, failures, artifacts, stabilization, transfer, maintenance, and review evidence.

**Primary visual object:** Chronological evidence timeline with filterable markers and artifact detail on demand.

**Primary user action:** Filter evidence, inspect an artifact, trace an outcome back to source facts, or open a related branch/session result.

**App-layer data source:** `CurrentTrainingStateReadModel.EvidenceSummaries`, `RecentSessions`, `ProgressRecords`, local evidence and session records surfaced by app integration, plus completion details from `CompletedRuntimeSessionProcessingResult` when reached immediately after a session.

**Low-fidelity layout:**

- Top: filters by branch, level, session type, artifact kind, failure type, transfer, maintenance, or review.
- Center: evidence timeline with pass/fail/warning/decay/abandon/timeout markers.
- Detail drawer: standard, critical constraint, score/rubric, artifact summary, failure classification, and linked runtime facts.

**Minimal visible text:** Date, branch/level, artifact kind, `Pass`, `Fail`, `Warn`, `Decay`, `Transfer`, `Review`, `Abandon`, `Timeout`.

**Anti-self-deception guard:** Failed, abandoned, timed-out, invalidated, or incomplete evidence is visible and cannot be collapsed into ordinary practice success. Subjective notes may appear, but they are not displayed as advancement evidence.

## Maintenance/Decay

**Purpose:** Operational surface for due maintenance, warnings, decay, restoration, and dependent advancement caps.

**Primary visual object:** Maintenance board with owned-node service rings, decayed-node fractures, dependency cap edges, and restoration route.

**Primary user action:** Start due maintenance, start restoration work, inspect dependent caps, or return to eligible non-advancement work.

**App-layer data source:** `CurrentTrainingStateReadModel.DueMaintenance`, `BranchLevelStates`, `BlockedAdvancement`, `RecentSessions`, `EvidenceSummaries`, `ProgressRecords`, `NextTrainingWorkSelection` for maintenance/restoration work, and completion outputs such as `MaintenanceCurrencyResult` and `DecayResult` when returning from a session.

**Low-fidelity layout:**

- Top: highest-priority due, warning, or decayed branch.
- Center: maintenance lane by branch with due/current/warn/decayed markers.
- Lower: dependent blocked edges and restoration requirements.

**Minimal visible text:** `Due`, `Warn`, `Decayed`, `Restore`, branch/level, and a terse cap reason.

**Anti-self-deception guard:** Decay is impossible to miss and outranks normal advancement. Restoration shows required evidence: last owned standard pass plus lower-load transfer check. The screen must not frame maintenance as a streak-preservation loop or optional activity when it blocks dependencies.

## Global Review

**Purpose:** Whole-practitioner review screen for review due, active review, pass/fail decision, bottleneck, and programming outputs.

**Primary visual object:** Review board with branch-state matrix, maintenance strip, recent failure strip, bottleneck marker, transfer/stabilization currency, recovery/deload status, and decision panel.

**Primary user action:** Start a due review workflow when app layer exposes one, inspect review inputs, follow the programmed decision, or route to bottleneck/maintenance/restoration work.

**App-layer data source:** `CurrentTrainingStateReadModel.CurrentPractitionerState`, `BranchLevelStates`, `DueMaintenance`, `RecentSessions`, `EvidenceSummaries`, `ProgressRecords`, `CategoryClassification`, `WeeklyPlan`, and `BlockedAdvancement`. A dedicated app-layer global review read model should be added before implementing full review execution if current outputs are not enough.

**Low-fidelity layout:**

- Top: review status and decision marker.
- Center: branch-state matrix plus maintenance and failure strips.
- Lower: bottleneck and next programmed response.
- Reveal: review pass requirements and source evidence.

**Minimal visible text:** `Review`, `Pass`, `Fail`, `Bottleneck`, `Deload`, `Restore`, `Open`, branch codes, and exact blocking labels when present.

**Anti-self-deception guard:** Review evaluates the whole practitioner, not a favorite branch. Advanced classification and dependent advancement remain blocked when review fails, prerequisites decay, maintenance is overdue, bottleneck response is missing, current transfer/stabilization evidence is missing, or participation-only advancement is detected by app/core data.

## Backup/Restore

**Purpose:** Local data utility surface for explicit backup export, restore, and integrity validation.

**Primary visual object:** Local data control panel with export, restore, validate, selected file, last local backup status, validation status, and destructive restore warning.

**Primary user action:** Export local backup, validate local data/package, restore from a local backup package, or cancel.

**App-layer data source:** `ApplicationIntegrationCapabilities` and a future app-layer backup/restore read model wrapping `LocalBackupService`, `LocalBackupPackage`, and `LocalPersistenceIntegrityValidator` results. Android should not implement this screen against direct Android-owned storage paths until that app-layer workflow exists.

**Low-fidelity layout:**

- Top: offline/local capability marker.
- Center: export, validate, and restore controls.
- Lower: selected local file, schema/integrity status, and destructive restore confirmation.

**Minimal visible text:** `Export`, `Validate`, `Restore`, file name/date, `Valid`, `Invalid`, `Replaces local data`.

**Anti-self-deception guard:** Backup/restore must stay local and explicit. There is no account, cloud sync, remote backup, automatic upload, telemetry, analytics, social sharing, notification reminder, or AI/API repair. Restore cannot silently replace state or skip integrity validation.

## Main Workflow Guardrails

- Home/Today is the first screen and must always show actionable training state or the highest-priority blocker.
- Every screen renders app-layer state and commands. Missing app-layer data is a product gap to fill in `MentalGymnastics.App`, not a reason to add UI-owned rules.
- The visible hierarchy must prioritize decayed, blocked, recovery/deload, review, transfer, and maintenance states before optional practice.
- Standards and honesty constraints are shown before tests, stabilization, transfer, maintenance checks, and any work where exactness protects evidence.
- Detail is reveal-on-demand, except failure, decay, due maintenance, and blocked advancement, which remain visible at the top level.
- Screen completion, time spent, taps, and visible effort never grant advancement.
