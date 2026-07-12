# MentalGymnastics Session Runtime Layer

**Status:** Implemented headless runtime reference
**Project:** `src/MentalGymnastics.Runtime`
**Tests:** `tests/MentalGymnastics.Runtime.Tests`
**Depends on:** [MentalGymnastics Core Library](core-library.md), [MentalGymnastics Local Persistence Layer](local-persistence-boundary.md)
**Source program:** [MentalGymnastics: Complete Training Program](program/training-program.md)

This document describes the implemented session runtime layer for Mental Gymnastics. The runtime is a headless execution engine for live drill sessions. It turns planned work into deterministic phases, cues, commands, timing facts, scoring facts, evidence drafts, completion results, snapshots, and handoff records.

The runtime is not a progression engine, a database, a generated-content system, or an Android screen. It exists so Android, CLI tools, tests, or other frontends can administer live sessions without inventing local timers, hidden logs, protocol-specific pass/fail logic, or screen-owned drill flows.

## Reference Direction

The allowed dependency direction is:

1. `MentalGymnastics.Core` owns program rules, catalogs, standards, and evaluators.
2. `MentalGymnastics.Persistence` owns local offline storage of facts and history.
3. `MentalGymnastics.Runtime` references Core concepts and persistence records for handoff shapes.
4. Android UI, CLI tools, and test harnesses may reference the runtime.

`MentalGymnastics.Core` must not reference the runtime. Persistence must not depend on runtime execution internals. Android may host and render a runtime session, but Android screens must not own the session lifecycle, cue scheduler, phase timers, scoring event model, evidence classifier, or branch-specific drill protocol logic.

## Ownership Split

`MentalGymnastics.Core` owns:

- Fixed program vocabulary, branch maps, level meanings, drill protocols, standards, rubrics, transfer rules, maintenance cadences, failure responses, weekly planning, global review, and prompt/content abstractions.
- Deterministic decisions for readiness, gates, ownership, maintenance, decay, restoration, dependency caps, balance, failures, stuck state, transfer, recovery, deload, practitioner category, and global review.
- Branch-level progression state legality through `BranchLevelStateMachine`.

`MentalGymnastics.Persistence` owns:

- Local database initialization, schema versioning, migrations, integrity validation, backup and restore, stores, repository queries, and atomic programming-event commits.
- Long-term records for practitioner state, branch-level states, sessions, attempts, evidence, stabilization, maintenance, decay, restoration, generated drill instances, and progress summaries.
- Offline-only, userless, app-owned storage. It does not decide advancement.

Generated content owns:

- Producing prompt sets, cue sequences, delayed reconstruction material, discrimination items, rule examples, and equivalent fresh content.
- Content identity, equivalence class, and versioning before a runtime session starts.

Android UI owns:

- Screens, navigation, controls, accessibility presentation, platform lifecycle handling, and forwarding user actions into the runtime.
- Displaying runtime state and core evaluator results without weakening them.

`MentalGymnastics.Runtime` owns:

- Live session definitions, lifecycle state, deterministic time, phase scheduling, cue scheduling, user command validation, response timing, recovery windows, runtime events, scoring facts, evidence capture, completion results, protocol execution facts, snapshots, restore, and runtime-to-core or runtime-to-persistence handoff records.

## Explicit Non-Responsibilities

The runtime must not own:

- Advancement, ownership conversion, maintenance currency, dependency caps, weekly planning, or other progression decisions already modeled in Core.
- Static copies of branch lists, unlock rules, thresholds, standards, maintenance cadences, transfer definitions, or rubrics.
- Local database files, migrations, import/export, backup, integrity validation, or atomic commits.
- Android views, activities, fragments, composables, navigation, notification behavior, or platform UI copy.
- Accounts, sync, backend services, telemetry, analytics, push notifications, billing, network calls, AI/API calls, or uncontrolled randomness.
- Motivational interpretation of failure or completion.

If a live session needs a rule decision, collect the facts and call Core at a boundary. If it needs to save records, produce handoff records and let Persistence write them.

## Main Runtime Concepts

### Session Definition

`RuntimeSessionDefinition` represents planned live work. It records the core concepts needed to administer a session:

- Session type, branch, level, drill, intensity, and load variables.
- Stated standard and honesty constraint.
- Critical constraints.
- Optional generated drill instance identity through `RuntimeGeneratedDrillInstanceIdentity`.

Construction rejects missing essentials. The runtime does not silently replace a missing standard, honesty constraint, branch, level, drill, or generated content identity with a default that weakens the session.

### Session Lifecycle

`RuntimeSessionLifecycleStateMachine` models live execution lifecycle states separately from core branch-level progression states.

Runtime lifecycle states include:

- `NotStarted`
- `Running`
- `Paused`
- `Completed`
- `Failed`
- `Abandoned`

Legal runtime transitions are explicit. Representative invalid transitions, such as completing before start, restarting a completed session, abandoning after completion, or pausing where pause is not allowed, are rejected without mutating state. Passing a branch-level standard still belongs to Core gate and stabilization rules, not to the runtime lifecycle.

### Timing Model

Runtime time is deterministic and injectable:

- `RuntimeInstant` and `RuntimeDuration` are value types for runtime timestamps and elapsed time.
- `IRuntimeClock` abstracts time.
- `ManualRuntimeClock` supports deterministic tests without sleeping.
- `TimeProviderRuntimeClock` and `RuntimePhaseTimer` provide production-adapter behavior suitable for an Android host.
- `RuntimeTimedPhase` tracks elapsed time, remaining time, deadlines, and timeout events.

Tests should not depend on wall-clock sleeps. Runtime code may observe time, emit ticks, and detect timeouts, but it must not make progression decisions from time alone.

### Phase Scheduling

`RuntimeSessionPhaseDefinition`, `RuntimeSessionPhasePlan`, and `RuntimeSessionPhaseSequence` represent phase plans. Phase kinds cover the generic drill structure needed by the program:

- Instruction and prep.
- Encode window.
- Active work.
- Delay window.
- Cue response.
- Reconstruction/input.
- Audit.
- Rest.
- Recovery.
- Review.

`RuntimePhaseScheduler` advances a phase plan as time and valid events occur. It emits phase-start, phase-end, timeout, and session-complete events. Timed phases can complete by timeout; manual phases require an allowed finish command; manual-or-timed phases support both. Invalid phase progression is rejected without corrupting the active phase or completed phase history.

### Command Handling

`RuntimeInputCommandHandler` accepts structured `RuntimeInputCommand` values and validates them against the current lifecycle and active phase.

Supported command kinds include:

- Start session.
- Finish phase.
- Pause where allowed.
- Resume.
- Abandon.
- Mark drift.
- Respond to cue.
- Submit answer.
- Mark guess.
- Correct within an allowed window.
- Start audit.

Invalid commands are represented by `RuntimeInputCommandResult` and `RuntimeInputCommandInvalidReason`. They do not mutate runtime state, advance phases, or erase prior evidence.

`MarkReturn` remains in the runtime command vocabulary only so older snapshots and direct runtime clients can be read safely. Current FH sessions do not expose or require it: each noticed wander is one `MarkDrift`, another wander may be marked immediately afterward, and FH standard evaluation ignores legacy return events.

### Runtime Events

`RuntimeEvent` and `RuntimeEventLog` record ordered, session-associated facts about execution. Event kinds include session start, phase changes, timer ticks, cues, user actions, answers, drift marks, guesses, interruptions, corrections, errors, recovery, abandon, and completion.

Runtime events are storage-ready facts, not UI messages. They should contain observable names and values such as cue id, response outcome, failed constraint, recovery time, output sample, or phase duration.

### Cue Schedules

`RuntimeCueSchedule` and `RuntimeCueScheduler` support deterministic cue work from generated drill instance data. Cues may represent focus shifts, invalid cue filtering, go/no-go prompts, interruptions, distractors, and timed response tasks.

The scheduler emits due cues at expected runtime instants, records presentation facts, evaluates responses against the active cue context, and tracks on-time, early, late, missed, correct, incorrect, commission, and invalid-response cases. Response deadlines are based on actual cue presentation time, not a guessed schedule start.

Cue scheduling is not a UI animation system. Android can render cues, but the cue identity, expectation, due time, and response result belong to the runtime.

### Response Timing and Recovery

`RuntimeResponseWindow` and `RuntimeRecoveryWindow` model response deadlines and recovery windows. The runtime can represent:

- On-time responses.
- Early responses.
- Late responses.
- Missed responses.
- Recovery after marked drift.
- Recovery after disruption.

These results become evidence for later scoring and Core evaluation. They do not themselves grant advancement.

### Scoring Facts and Evidence Capture

`RuntimeScoringEventFactory` converts runtime behavior into observable scoring events such as correct response, incorrect response, omission, commission, premature response, late response, marked drift, unmarked drift where detectable, marked guess, correction, and timeout.

`RuntimeEvidenceCapture` creates evidence drafts for the categories required by the program:

- Best set.
- Failed set.
- Bottleneck note.
- Formal attempt evidence.
- Stabilization evidence.
- Transfer evidence.
- Maintenance evidence.
- Audit evidence.

Evidence must constrain interpretation. Vague encouragement, participation, effort, insight, or novelty is not replacement evidence.

### Completion Results

`RuntimeSessionCompletionResultGenerator` produces `RuntimeSessionCompletionResult` values for completed, failed, abandoned, and timed-out sessions. A completion result includes:

- Session identity and generated drill instance identity.
- Branch, level, drill, session type, and load variables.
- Phase history.
- Runtime events.
- Scoring facts.
- Evidence summary.
- Completion status.
- Failure-relevant facts where applicable.

The result says what happened in the live session. It does not grant advancement, ownership, maintenance currency, restoration, transfer validity, or global review pass.

Failed or timed-out sessions cannot carry a best-set evidence draft as successful evidence. Abandoned sessions carry abandonment facts and are not accepted as successful evidence.

## Protocol Support

Runtime protocol classes produce branch-specific execution facts for the standard drill library. They preserve the live constraints from the program without deciding advancement.

| Branch | Runtime support | Preserved execution facts |
| --- | --- | --- |
| FH | `FocusHoldRuntimeProtocol` | Target statement before set, one mark per noticed drift, distractor handling, no target substitution. |
| FS | `FocusShiftRuntimeProtocol` | Cue obedience, valid cue responses, invalid cue filtering, sequence accuracy, no anticipatory switching. |
| WM | `WorkingMemoryRuntimeProtocol` | Encode and delay windows, delayed reconstruction, no rereading after encode, no invented items, no hidden intermediate notes where prohibited. |
| IR | `InhibitionRuntimeProtocol` | Go/no-go handling, premature-response facts, cue pace, rule statement before set, exception handling, post-error cascade evidence. |
| DE | `DiscriminationRuntimeProtocol` | Marked guesses, false positives, false negatives, original-output lock, seeded error findings, invented-error facts. |
| CO | `ConceptOperationsRuntimeProtocol` | Rule statement before unseen examples, negative examples, relation naming, surface-match rejection, unsupported inference evidence. |
| AI | `AffectiveInterferenceRuntimeProtocol` | Source branch standard visibility, defined pressure source, no lowered standard, interruption timing, no prohibited full restart, post-disruption evidence. |
| TI | `TransferIntegrationRuntimeProtocol` | Branch-specific component evidence, audit requirement, delayed reconstruction requirement, and protection against strong components hiding weak components. |

Protocol results expose runtime events and facts suitable for `RuntimeScoringEventFactory`, evidence capture, and later handoff. They do not contain branch-level progression outcomes.

## Anti-Self-Deception Guards

`RuntimeAntiSelfDeceptionGuard` validates attempts to bypass live constraints and returns evidence-bearing results. Runtime guards prevent or record:

- Skipped phases.
- Changed target.
- Unallowed rereading.
- Hidden notes where prohibited.
- Unmarked guesses where marking is required.
- Premature responses.
- Invalid restarts.
- Removed branch-specific evidence.
- Abandoned evidence.

The guard returns a disposition and facts. It does not create motivational copy or infer intent beyond the represented evidence.

## Snapshot and Restore

`RuntimeSessionSnapshot`, `RuntimePhaseSchedulerSnapshot`, and `RuntimeCueSchedulerSnapshot` preserve live state for process death or Android lifecycle interruption when continuation is permitted.

Snapshots preserve:

- Session definition and generated drill instance identity.
- Lifecycle state.
- Active phase id and index.
- Elapsed time and phase history.
- Pending cue state and response state.
- Ordered runtime events.
- Evidence facts.
- Last correctable event where relevant.

Restore rejects unsafe snapshots that would create false successful evidence, such as active sessions without an active phase, completion snapshots with inconsistent status, or snapshots that try to restore progression facts. Snapshot/restore keeps live execution coherent; it is not a persistence transaction and does not replace local storage.

## Handoff Boundaries

### Runtime to Core

`RuntimeCoreEvaluationHandoffMapper` converts a runtime completion result plus caller-provided rule inputs into Core-ready inputs:

- Standard evaluation input.
- Formal gate input.
- Readiness practice input.
- Stabilization pass evidence.
- Maintenance check evidence.
- Transfer eligibility request.
- Failure response request.

The handoff preserves scoring facts, evidence facts, stated standards, critical constraints, artifact summaries, branch/level/drill identities, and failure-relevant facts. It does not decide whether the practitioner advances. The caller passes these records to Core evaluators.

### Runtime to Persistence

`RuntimePersistenceHandoffMapper` converts a runtime completion result into persistence-facing records:

- Session history record.
- Evidence artifact record.
- Formal attempt record where applicable.
- Stabilization record where applicable.
- Maintenance record where applicable.
- Generated drill instance completion metadata.

Atomic writes remain the responsibility of `LocalProgrammingEventTransaction` and the relevant persistence stores. The runtime does not write local files or decide database consistency.

### Session Coordinator

`RuntimeSessionCoordinator` coordinates one completed live-session flow in tests and future host code:

1. Accept a session definition, event inputs, completion inputs, optional Core handoff inputs, and optional persistence handoff inputs.
2. Capture events and completion facts.
3. Produce a completion result.
4. Produce Core and Persistence handoff records when requested.

The coordinator is orchestration glue. It does not duplicate Core rule decisions or Persistence internals.

## Android Consumption Pattern

Future Android code should use the runtime in this order:

1. Load catalog definitions and rule inputs from Core.
2. Load generated drill instances and recent local history from Persistence.
3. Build a `RuntimeSessionDefinition` with stated standard, honesty constraint, load variables, and generated drill instance identity.
4. Use the relevant protocol class, `RuntimePhaseScheduler`, `RuntimeCueScheduler`, and `RuntimeInputCommandHandler` with an injected clock.
5. Forward screen events into runtime commands and render runtime state.
6. Generate a `RuntimeSessionCompletionResult`.
7. Map the result to Core inputs and call Core evaluators for readiness, gates, stabilization, maintenance, transfer, failure routing, recovery, deload, or review as appropriate.
8. Map the result to Persistence records and save them through the local persistence layer, using a transaction when one programming event writes related records.

Android should not create independent phase timers, cue schedulers, drill state machines, hidden evidence logs, screen-local pass/fail flags, or SharedPreferences/Room progression mirrors for runtime facts.

## Tested Invariants

The runtime test suite documents behavior future agents should preserve and should not reimplement elsewhere.

### Boundaries

- Runtime declares live execution responsibility and rejects progression ownership.
- Runtime remains independent of Android UI, backend, accounts, sync, telemetry, notifications, network calls, AI/API calls, and uncontrolled randomness.
- Core and Persistence remain the authorities for progression rules and storage.

### Definitions and Lifecycle

- Session definitions reject missing branch, level, drill, session type, standard, honesty constraint, and required generated instance identity.
- Runtime lifecycle transitions are explicit and illegal transitions are rejected without mutation.
- Runtime lifecycle does not replace Core branch-level progression state.

### Timing and Phases

- Manual clocks advance deterministically.
- Production timer behavior is observable without flaky sleeps.
- Elapsed time, remaining time, deadlines, and timeout events are represented consistently.
- Phase ordering, phase duration, completion cause, timeout, and session-complete events are deterministic.
- Invalid phase progression does not corrupt active phase or completed history.

### Commands and Cues

- Commands are accepted only in valid phases and lifecycle states.
- Invalid commands do not mutate state or erase evidence.
- Cue presentation and response timing are tied to the active cue context.
- Cue-response mismatches, invalid cue responses, missed cues, premature responses, and deadlines are recorded as evidence facts.
- Pause and resume preserve elapsed time, active phase, pending cues, runtime events, and evidence history where pause is allowed.

### Evidence and Completion

- Scoring events record observable facts and do not replace Core standard or gate evaluation.
- Evidence drafts must constrain interpretation; vague self-description cannot replace evidence.
- Completion results preserve phase history, events, scoring facts, evidence summary, completion status, load variables, and generated instance identity.
- Failed and timed-out sessions cannot be mistaken for best-set successful evidence.
- Abandoned sessions carry abandonment facts and no successful evidence draft.

### Protocol Honesty Constraints

- FH records target statements, one mark per noticed drift, distractor responses, and target substitution attempts. Resuming the same target is implicit and is not timed or separately confirmed.
- FS records valid cue obedience, invalid cue inhibition, sequence accuracy, and anticipatory switches.
- WM records encode closure, delay, reconstruction accuracy, invented items, rereading attempts, and hidden-note attempts.
- IR records go/no-go results, premature responses, rule statements, exceptions, and post-error cascades.
- DE records marked guesses, false positives, false negatives, original-output locks, seeded error findings, and invented errors.
- CO records pre-stated rules, negative examples, unseen classification, relation naming, surface-match rejection, and unsupported inference.
- AI records source standard visibility, pressure source, standard-lowering attempts, interruption timing, prohibited restarts, and recovery evidence.
- TI records separate component-branch evidence, audit, delayed reconstruction, and component failures that cannot be hidden by stronger branches.

### Snapshot and Handoff

- Snapshot/restore preserves active phase, elapsed time, pending cues, generated drill instance identity, user events, and evidence facts.
- Restore rejects unsafe snapshots that would create false successful evidence.
- Runtime-to-Core handoff preserves Core evaluator inputs without performing advancement decisions.
- Runtime-to-Persistence handoff preserves session, evidence, attempt, stabilization, maintenance, and generated instance records without writing storage directly.

## Future Work Rules

When adding Android, CLI, generated content, persistence, or new drill execution behavior:

1. Use `MentalGymnastics.Runtime` for live execution mechanics instead of creating ad hoc timers, cue schedulers, drill state machines, or evidence logs in the frontend.
2. Use `MentalGymnastics.Core` for progression decisions and program rules.
3. Use `MentalGymnastics.Persistence` for local records, transactions, backup, restore, and integrity validation.
4. Keep generated content deterministic at the runtime boundary by passing explicit instance identity, content version, equivalence class, and cue/item data into the runtime.
5. Add runtime tests before changing live execution behavior.
6. If a needed rule is a progression rule, add it to Core with tests first. If it is storage behavior, add it to Persistence with tests first. If it is live execution behavior, add it to Runtime with deterministic tests first.
