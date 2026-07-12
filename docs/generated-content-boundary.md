# MentalGymnastics Generated Drill Content Layer

**Status:** Implemented local generated content reference
**Project:** `src/MentalGymnastics.Content`
**Tests:** `tests/MentalGymnastics.Content.Tests`
**Depends on:** [MentalGymnastics Core Library](core-library.md)
**Related layers:** [Local Persistence](local-persistence-boundary.md), [Session Runtime](session-runtime-boundary.md)
**Content requirements:** [Generated Content Requirements](generated-content-requirements.md)
**Source program:** [MentalGymnastics: Complete Training Program](program/training-program.md)

This document describes the implemented generated drill content layer for Mental Gymnastics. Generated content is the local, offline source of concrete drill material: target statements, distractors, cue sequences, memory items, transform tasks, go/no-go streams, exception rules, discrimination pairs, seeded audits, rule examples, structure mappings, pressure and disruption wrappers, transfer variants, and TI composite materials.

Generated content exists to serve the Core program standards, Runtime session execution, and Persistence records. It is not a second curriculum, a progression engine, a database, an Android UI feature, or an AI/API integration.

## Reference Direction

The allowed dependency direction is:

1. `MentalGymnastics.Core` owns program vocabulary, standards, drill protocols, freshness vocabulary, transfer rules, load and regression rules, and progression decisions.
2. `MentalGymnastics.Content` references Core and produces local generated drill instances that preserve Core identities, standards, load variables, and honesty constraints.
3. Runtime hosts or coordinators consume generated packages and cue/material facts when executing sessions.
4. Persistence stores generated instance facts, audit material, lifecycle state, and later completion metadata.
5. Android UI, CLI tools, or other frontends orchestrate these layers.

`MentalGymnastics.Core` must not reference generated content. Generated content must not reference Android UI, network clients, AI APIs, accounts, telemetry, notifications, backend services, persistence internals, or runtime internals. Runtime should not invent content; it should execute content supplied to it.

The current content project is a pure `net10.0` class library. It references Core only.

## Ownership Split

`MentalGymnastics.Core` owns:

- Fixed branch, level, drill, standard, rubric, load, critical-constraint, prompt/content, transfer, and freshness vocabulary.
- Deterministic rule decisions for readiness, gates, ownership, maintenance, decay, restoration, dependency caps, global balance, transfer eligibility, recovery, deload, weekly planning, practitioner classification, stuck state, failure routing, and global review.
- The meaning of advancement evidence and the anti-self-deception rules that affect progression.

`MentalGymnastics.Content` owns:

- Local deterministic generated drill instances and concrete content material.
- Generated instance identity, content identity metadata, content versioning, load fingerprints, equivalence classes, and deterministic seeds.
- Freshness and equivalence checks for generated content candidates.
- Local content banks and deterministic content selection.
- Load-to-content constraint mapping and difficulty auditing.
- Generated material validation and generated-content anti-self-deception guards.
- Branch generators for FH, FS, WM, IR, DE, CO, AI, TI, and transfer content.
- Runtime packaging records that preserve phases, cues, branch/level/drill identity, standards visibility, load variables, critical constraints, expected evidence facts, and generated instance identity.
- Persistence handoff records that preserve instance identity, content version, content summary, freshness/equivalence metadata, load context, and audit-relevant material.

`MentalGymnastics.Runtime` owns:

- Live session definitions, lifecycle, clocks, timers, phase scheduling, cue scheduling, command handling, response timing, scoring facts, evidence capture, completion results, protocol execution facts, snapshots, restore, and runtime-to-core or runtime-to-persistence handoffs.
- Runtime may consume generated packages, but it must not create prompt material, hidden content variants, or freshness decisions.

`MentalGymnastics.Persistence` owns:

- Local database initialization, schema versioning, migrations, integrity validation, backup and restore, stores, repository queries, and atomic programming-event commits.
- Long-term records for generated drill instances, generated instance runtime state, payload summaries, evidence artifacts, sessions, attempts, stabilization, maintenance, decay, restoration, and progress summaries.
- Persistence may store generated content handoff facts, but it must not decide generation, equivalence, transfer validity, or progression.

Android UI owns:

- Screens, navigation, controls, accessibility presentation, platform lifecycle behavior, and forwarding user actions into Runtime.
- Android should render generated material and runtime state. It must not create ad hoc prompts, cue schedules, hidden test variants, local content identity schemes, or screen-owned freshness/equivalence decisions.

## Main Concepts

### Requests and Results

`GeneratedDrillContentRequest` represents a request for local content using Core concepts: branch, level, drill, session type, content kind, equivalence class, freshness policy, load variables, critical constraints, and previously used content ids.

`GeneratedDrillContentResult` pairs that request with a `GeneratedDrillInstanceDescriptor` and payload facts. Construction rejects results whose branch, level, drill, content kind, equivalence class, freshness policy, load variables, or critical constraints no longer match the request. Payload facts are required so Runtime and Persistence receive observable material rather than vague descriptions.

### Identity and Versioning

Every generated instance carries:

- Stable generated instance id.
- Core `PromptContentIdentity`: content id, branch, level, drill, content kind, and equivalence class.
- Content version or generator version.
- Retest freshness policy.
- Load variables and a load-context fingerprint.
- Critical or honesty constraints.
- `GeneratedDrillInstanceIdentityMetadata` for audit, replay, and local persistence.

Display text is not the source of truth. Identity must change when content, load context, content kind, critical constraints, or demand identity changes. Equivalent retests keep the same equivalence class while changing content id when freshness is required.

### Seeds and Determinism

`GeneratedContentSeed`, `GeneratedContentSeedPlan`, and `GeneratedContentSeedDeriver` make generated content reproducible from explicit local inputs. The same request and seed should produce the same content. Fresh variants require a recorded fresh-variant seed or selection input so replay and audit remain possible.

Generated content must not consult system time, device state, network state, accounts, telemetry, analytics, AI provider output, or uncontrolled randomness. Callers may pass dates or ids when Persistence needs them, but generation itself remains deterministic.

### Freshness and Equivalence

`GeneratedContentEquivalenceRequirement`, `GeneratedContentEquivalenceCandidate`, and `GeneratedContentFreshnessPolicy` enforce the retesting rule that some work requires fresh but equivalent content.

Fresh equivalent content must preserve:

- Same trained demand, branch, level, drill, and content kind.
- Same equivalence class.
- Same load intent and load variables unless the caller explicitly requested a new load.
- Same critical constraints, honesty constraints, and standards visibility.
- Same evidence shape for later Runtime and Core evaluation.

Fresh equivalent content must change concrete material or content id when freshness is required. Novelty alone is not transfer, advancement, or valid retest evidence.

### Materials and Validation

`GeneratedContentMaterial` and `GeneratedContentMaterialValidator` validate that generated content contains the material required by the branch, level, drill, load variables, and honesty constraints. `ValidatedGeneratedDrillContent` is the usable handoff shape after validation.

Material kinds cover targets, distractors, cue sequences, invalid cues, memory reconstruction material, transform operations, go/no-go streams, exception rules, discrimination pairs, seeded audits, rule examples, structure mappings, pressure and disruption metadata, transfer source standard evidence, and TI component evidence.

Validation rejects missing or malformed content before it can be accepted as a usable drill instance. It does not evaluate readiness, gates, ownership, or other progression outcomes.

### Load Constraints and Difficulty Auditing

`GeneratedContentLoadConstraintMapper` maps Core load variables into content-selection constraints. `GeneratedContentLoadConstraintValidator` verifies that generated material reflects the requested load constraints.

`GeneratedContentDifficultyAuditor` checks whether generated content preserved the requested branch, level, drill, load variables, equivalence constraints, core demand, and honesty constraints. It detects over-hard or under-specified content, unsupported multi-variable load changes during acquisition, wrong-demand content, and content that removes the core constraint.

Difficulty auditing is about content suitability. Progression decisions remain in Core.

### Local Content Banks

`LocalContentBank`, `LocalContentBankEntry`, and `LocalContentBankSelector` support packaged or locally generated reusable source material. Content bank entries include metadata for branch, level, drill, content kind, equivalence class, load variables, critical constraints, version, source kind, and payload facts.

Selection is deterministic from explicit request data and seed/selection input. Content banks must not require network access, AI services, accounts, telemetry, sync, Android UI, or device state.

### Branch Generators

Branch generators produce representative validated content for the documented drill library:

- `FocusHoldGeneratedContentGenerator` for FH-1 Target Hold and FH-2 Distractor Hold.
- `FocusShiftGeneratedContentGenerator` for FS-1 Cue Switch and FS-2 Invalid Cue Filter.
- `WorkingMemoryGeneratedContentGenerator` for WM-1 Delayed Reconstruction and WM-2 Mental Transform.
- `InhibitionGeneratedContentGenerator` for IR-1 Go/No-Go Rule and IR-2 Exception Rule.
- `DiscriminationGeneratedContentGenerator` for DE-1 Pair Discrimination and DE-2 Seeded Audit.
- `ConceptOperationsGeneratedContentGenerator` for CO-1 Rule Extraction and CO-2 Structure Mapping.
- `AffectiveInterferenceGeneratedContentGenerator` for AI-1 Pressure Repeat and AI-2 Disruption Recovery.
- `TransferIntegrationGeneratedContentGenerator` for TI-1 Composite Task and TI-2 Global Review Task.

The generators preserve the documented purpose, load, expected evidence, and honesty constraints. They do not implement live execution or progression.

### Transfer Content

`TransferContentGenerationRequest`, `TransferContentCandidate`, `TransferContentRuleValidator`, `TransferGeneratedContent`, and `TransferGeneratedContentGenerator` represent transfer content by source branch, source level, source drill, same demand, changed context, transfer distance, source standard visibility, retest requirement, and generated material.

Transfer content must visibly preserve the source branch standard while changing task format or context. Novelty-only content, missing source standard evidence, same-context replacements, and changed-demand variants are invalid for transfer use.

### Selection Coordinator

`GeneratedContentSelectionCoordinator` coordinates a core-driven training need into a validated generated drill instance. It combines request construction, local content bank selection, deterministic seed inputs, freshness/equivalence checks, material validation, difficulty auditing, and anti-self-deception guards.

The coordinator must not decide readiness, advancement, ownership, maintenance, decay, recovery, deload, weekly planning, or global review.

### Runtime Packaging

`GeneratedContentRuntimePackager` converts validated generated content into runtime-consumable records:

- Generated runtime phase definitions.
- Generated cue definitions and response expectations.
- Session identity and generated instance identity.
- Branch, level, drill, session type, load variables, critical constraints, standard visibility, and expected evidence facts.

Packaging preserves content for Runtime; it does not run timers, score responses, capture evidence, or decide advancement.

### Persistence Handoff

`GeneratedContentPersistenceHandoffMapper` converts generated content into persistence-facing records:

- Instance id, generated date, content identity, content version, content kind, branch, level, drill, and equivalence class.
- Load variables, freshness policy, load-context fingerprint, and content summary.
- Audit-relevant material needed for offline replay, local backup, or later evidence review.

Persistence owns actual storage, integrity validation, transactions, backup, and restore. Generated content does not write files or maintain parallel use history.

### Boundary Handoff Validation

`GeneratedContentBoundaryHandoffValidation` protects cross-layer handoffs. It validates that standard content and transfer content are usable before Runtime or Persistence consumes them, including source-standard visibility and generated-content anti-self-deception checks.

## Android and Coordinator Consumption Pattern

Future Android, CLI, or session coordinator code should use generated content in this order:

1. Ask Core or planning code what branch, level, drill, standard, load variables, constraints, and freshness policy are needed.
2. Query Persistence for prior generated content ids, reusable generated instances, active generated instances, and relevant local history.
3. Build a `GeneratedContentSelectionNeed` or `GeneratedDrillContentRequest` from those Core and Persistence facts.
4. Ask `MentalGymnastics.Content` for a local deterministic generated instance or fresh equivalent variant.
5. Validate generated material and anti-self-deception guard results before exposing it to a session.
6. Create a Persistence handoff and save the generated instance through Persistence.
7. Create a Runtime package and build the runtime session definition/protocol inputs from it.
8. Run the live session through Runtime.
9. Use Runtime handoffs to Core for rule evaluation and to Persistence for atomic local records.

Android should render content and forward user actions. It should not generate prompts in screens, randomize cue sequences locally, pick easier variants, hide standards, create source-standard shortcuts, store content identity in UI flags, or decide that fresh novelty satisfies transfer.

## Tested Invariants

The generated content test suite documents behavior future agents should preserve and should not reimplement elsewhere.

### Boundary and Determinism

- Generated content declares offline, deterministic, no-Android, no-account, no-sync, no-backend, no-telemetry, no-notification, no-AI/API capability boundaries.
- The content project consumes Core concepts and does not depend on Android UI, Persistence internals, Runtime internals, network clients, or uncontrolled randomness.
- The same explicit request and seed produce repeatable content.
- Fresh variants are distinguishable from reused content when freshness is required.

### Requests, Identity, and Versioning

- Requests reject missing branch, level, drill, session type, content kind, equivalence class, freshness policy, load variables, or critical constraints.
- Results reject identity, load, or critical-constraint drift from the request.
- Every generated instance exposes stable identity metadata, content version, branch, level, drill, content kind, equivalence class, freshness policy, and load-context fingerprint.
- Generated identity changes when critical constraints or load context change.

### Freshness, Equivalence, and Transfer

- Fresh-equivalent retests skip previously used content while preserving demand identity.
- Reusable content is allowed only when the freshness policy allows reuse.
- Non-equivalent replacements are rejected when they change branch demand, content kind, equivalence class, standard visibility, load intent, or honesty constraints.
- Transfer content requires same demand, changed context, visible source branch standard, transfer distance, and retest requirement.
- Novelty-only transfer and hidden source-standard content are rejected.

### Material Validation and Branch Constraints

- Generated material must include the content required by its branch and drill before Runtime can consume it.
- FH content preserves target statement, standalone wander marking, distractors where applicable, and no target substitution. It must not introduce a return timer or second return action.
- FS content preserves valid cue handling, invalid cue filtering, cue density, target count, sequence accuracy, and no anticipatory switching.
- WM content preserves encode windows, delay windows, expected reconstruction or transform output, no rereading, no invented items, and no hidden intermediate notes where prohibited.
- IR content preserves go/no-go streams, exception rules, cue pace, premature-response evidence, rule statement before the set, and post-error cascade evidence.
- DE content preserves pair discrimination evidence, marked guesses, false-positive and false-negative facts, locked original audit output, seeded errors, and invented-error evidence.
- CO content preserves rule examples, negative examples, unseen test cases, structure mappings, relation names, surface-match rejection, and unsupported inference evidence.
- AI content preserves source branch standard visibility, pressure or disruption metadata, no lowered standard, interruption timing, restart constraints, and post-disruption evidence.
- TI content preserves branch-specific evidence for each component, audit requirement, delayed reconstruction requirement, and the rule that a strong component cannot hide a weak one.
- TI runtime packaging assigns each component payload its own ordered active-work phase, preserves total programmed duration exactly across those phases, and places a held-target component last so Android never invents a screen-local component cursor.

### Load, Difficulty, and Anti-Self-Deception

- Load constraints reflect requested branch load variables without creating undocumented difficulty shortcuts.
- Difficulty audits detect content that changes multiple primary load variables during acquisition, removes the core demand, removes honesty constraints, or is over-hard or under-specified for the request.
- Anti-self-deception guards reject or flag too-easy variants, missing evidence, removed constraints, untracked guesses, hidden rereading, novelty-only transfer, unsupported pressure changes, participation-only content, and abandoned-evidence shortcuts.
- Guards produce evidence facts and validity results, not motivational copy.

### Handoffs

- Runtime packaging preserves generated identity, branch, level, drill, load variables, constraints, standards visibility, phases, cues, and expected evidence facts without runtime inventing content.
- Persistence handoff preserves identity, version, branch, level, drill, load variables, content summary, freshness/equivalence metadata, and audit-relevant material without writing storage directly.
- Boundary handoff validation rejects standard or transfer content that would let Runtime or Persistence consume invalid material.
- No generated content path grants advancement, ownership, maintenance currency, restoration, or transfer validity. Those decisions remain in Core.

## Future Work Rules

When adding Android, CLI, runtime, persistence, or generated-content behavior:

1. Use `MentalGymnastics.Content` for prompt material, cue sequences, item sets, seeded errors, rule examples, transfer variants, pressure/disruption wrappers, and composite materials.
2. Use `MentalGymnastics.Core` for progression decisions, standards, transfer eligibility, load/regression rules, and program vocabulary.
3. Use `MentalGymnastics.Runtime` for live timers, phases, cues, command handling, scoring facts, evidence capture, completion results, and snapshot/restore.
4. Use `MentalGymnastics.Persistence` for local records, transactions, generated instance stores, backup, restore, and integrity validation.
5. Keep Android UI responsible for rendering content and forwarding actions only.
6. Add generated-content tests before adding new generators, content bank behavior, freshness/equivalence policy behavior, runtime packaging shapes, or persistence handoff shapes.
7. If a missing requirement is a progression rule, add it to Core with tests first. If it is live execution behavior, add it to Runtime. If it is storage behavior, add it to Persistence. If it is concrete content material or generation behavior, add it to Content.
8. Do not add AI/API, network, backend, accounts, sync, telemetry, analytics, notifications, Android UI, device-state randomness, or external-service dependencies to generated content.
