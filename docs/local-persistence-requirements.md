# MentalGymnastics Local Persistence Requirements

**Status:** Initial requirements for offline storage  
**Boundary:** [docs/local-persistence-boundary.md](local-persistence-boundary.md)  
**Domain authority:** [docs/core-library.md](core-library.md)

This document defines the local data that Mental Gymnastics must preserve so the app can work offline. Persistence stores facts and artifacts. It does not decide progression, duplicate catalog data, or replace core evaluators.

## Required Local Data

Persist these records locally on device:

| Area | Required data | Why it is persisted |
| --- | --- | --- |
| Practitioner state | One current whole-practitioner snapshot, including current category inputs and summary metadata needed to reload the program state. | Offline startup needs the latest training picture without a network or account. |
| Branch-level states | State for every relevant branch-level pair: branch, level, state, effective date, and the source event or decision that produced the state. | Readiness, stabilization, ownership, maintenance, decay, and restoration all depend on exact branch-level state. |
| Session history | Completed session records: date, session type, branches trained, drill or transfer task, intensity, load variables, clean/failed set notes, recovery/deload markers, and linked artifacts. | Weekly planning, stuck-state detection, maintenance, and global review need recent training history. |
| Formal test attempts | Branch, level, date, drill or transfer task, load variables, stated standard, critical constraints, score/rubric/pass-fail evidence, failure classification when failed, pass state, and artifact link. | Formal attempts are the observable history behind gates, stabilization, ownership, and failed-test responses. |
| Evidence artifacts | Artifact category, date, observable evidence entries, short output sample or local reference, and optional subjective note. | Artifacts constrain interpretation and prevent participation or self-description from replacing evidence. |
| Stabilization records | Passed-once source attempt, stabilization pass attempts, dates, unchanged standard marker, adjacent-work or distractor marker, and avoided failure mode. | Ownership cannot be reconstructed safely without repeatability evidence. |
| Maintenance history | Maintenance check records by branch and owned level, date, check kind, standard result, critical-constraint result, and artifact link. | Currency, warning, overdue, failed, decay, and dependency cap decisions require maintenance history. |
| Decay and restoration history | Decay event records, failed maintenance evidence, affected branch-level, restoration attempts, last-owned-standard pass, and lower-load transfer check. | Decay is distinct from ordinary training failure and must remain auditable. |
| Generated drill instances | Concrete prompts, cue sequences, delayed reconstruction tasks, discrimination item sets, rule examples, source content identifiers, freshness policy, equivalence group, and use date. | Retesting and transfer need fresh but equivalent content; offline use needs the exact generated instance. |
| Active runtime session snapshots | In-progress runtime session id, generated drill instance id, session definition, lifecycle state, active phase, timing state, pending cues, ordered runtime events, evidence facts, and correctable-event reference. | Android lifecycle interruption or process death needs honest resume data without turning an unfinished session into successful evidence. |
| Progress summaries | Generated progress summary snapshots with date, source snapshot or review period, branch owned levels, maintenance summary, active blockers, bottleneck, and next programmed emphasis. | Offline review screens can show the latest summary, while Core remains the authority for recomputing current decisions. |

All records should use core identifiers where available: `BranchCode`, `GlobalLevelId`, `BranchLevelState`, `SessionType`, `EvidenceArtifactCategory`, `FormalTestPassState`, `FailureType`, maintenance states, rubric outcomes, and related core vocabulary.

## Persisted References

Persist local references, not external dependencies:

- Local artifact identifiers or file paths for output samples.
- Generated content instance IDs.
- Source attempt IDs for stabilization, decay, restoration, and progress summary records.
- Program/catalog version metadata if introduced later, so old local records can be interpreted after migrations.

Do not require accounts, backend IDs, cloud URLs, telemetry IDs, analytics sessions, push tokens, or AI provider request IDs.

## Must Not Be Persisted

Do not persist static program catalog data that belongs to `MentalGymnastics.Core`:

- Branch definitions, branch types, branch names, unlock relationships, and prerequisite rules.
- Global level definitions.
- Capacity definitions.
- Standard drill definitions and drill protocol definitions.
- Branch-level standards, gates, stabilization descriptions, transfer definitions, and maintenance cadences.
- Qualitative rubric definitions.
- Load variable catalogs, forbidden load increases, allowed regressions, and forbidden regressions.
- Failure response mappings, bottleneck definitions, weekly programming templates, global review decision catalogs, prompt content kinds, and anti-self-deception rule definitions.

Do not persist derived decisions that can be recomputed from persisted facts and core evaluators:

- Current test readiness.
- Current maintenance currency.
- Current dependency caps.
- Current global balance issues.
- Current practitioner category.
- Current transfer eligibility.
- Current recovery or deload decision.
- Current stuck-state result.
- Current weekly plan.
- Current global review pass/fail result.
- Current gate decision when the original formal attempt and standard evidence are available.
- Active runtime snapshots as completed-session, pass, ownership, maintenance, stabilization, or gate authority.

If a derived result is saved for audit or display speed, store it as a non-authoritative read model with its source record IDs and generation date. The app must be able to discard and recompute it from persisted facts plus the core library.

## Storage Responsibility

The local store may serialize, migrate, index, and load these records. It must not implement progression rules. The expected flow is:

1. Load persisted facts.
2. Rehydrate core domain types or equivalent input records.
3. Call `MentalGymnastics.Core` catalogs and evaluators.
4. Persist new facts produced by sessions, attempts, evidence, generated content, or valid state changes.

Storage behavior remains offline-only, userless, and local to the device.
