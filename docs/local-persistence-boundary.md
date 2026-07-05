# MentalGymnastics Local Persistence Layer

**Status:** Implemented local persistence reference  
**Project:** `src/MentalGymnastics.Persistence`  
**Tests:** `tests/MentalGymnastics.Persistence.Tests`  
**Depends on:** `src/MentalGymnastics.Core`  
**Domain authority:** [docs/core-library.md](core-library.md)  
**Data requirements:** [docs/local-persistence-requirements.md](local-persistence-requirements.md)

This document describes the implemented local persistence layer for Mental Gymnastics. Persistence is an offline, userless, device-local storage boundary. It records program facts and evidence so app layers can resume work after process death, device restart, or local backup restore, then ask `MentalGymnastics.Core` for rule decisions.

Persistence is not a second domain model. It stores and reloads core facts, local record identities, and non-authoritative read models. Progression rules remain in the core library.

## Reference Direction

The allowed project direction is:

1. `MentalGymnastics.Core`
2. `MentalGymnastics.Persistence` references `MentalGymnastics.Core`
3. Future frontends such as Android, CLI, or test tools may reference both

`MentalGymnastics.Core` must not reference persistence. Persistence must not reference Android UI, platform UI APIs, network clients, account systems, sync services, telemetry, push notifications, AI services, or backend code.

The current persistence project is a pure `net10.0` class library. It has no package references and consumes core domain types directly.

## Ownership Split

`MentalGymnastics.Core` owns:

- Static program vocabulary, catalogs, branch relationships, standards, rubrics, drills, load and regression rules, weekly templates, transfer definitions, and review definitions.
- Deterministic evaluators for readiness, gates, standard evaluation, stabilization ownership, maintenance currency, decay, restoration, dependency caps, global balance, failures, stuck state, transfer, recovery, deload, practitioner classification, weekly planning, prompt content selection, and global review.
- Branch-level lifecycle transition legality through `BranchLevelStateMachine`.
- The meaning of branch, level, drill, evidence, attempt, maintenance, and review domain concepts.

`MentalGymnastics.Persistence` owns:

- Local database initialization, schema version tracking, and migration primitives.
- Stable persisted identifiers for core enums and persistence-local enums.
- Local storage records and stores for practitioner state, branch-level states, sessions, formal attempts, evidence artifacts, stabilization passes, maintenance checks, restoration checks, decay history, restoration history, generated drill instances, and progress summaries.
- Repository/query APIs that return domain-focused records for future app screens and session runtime code.
- Atomic local programming-event commits for related state, evidence, attempt, and session writes.
- Local backup export/restore packages.
- Persistence integrity validation for missing required records, invalid references, unknown identifiers, impossible persisted lifecycle history, and orphaned evidence.
- Non-authoritative progress summaries derived from local facts and core rules.
- Active runtime session snapshots for honest local resume after process death or Android lifecycle interruption.

Persistence must not own progression decisions. If app code needs to know whether a test is ready, whether a formal attempt passes, whether ownership is earned, whether maintenance is overdue, whether a branch decays, whether an advanced dependency is capped, whether transfer is valid, or what a weekly plan should be, it must load facts from persistence and call `MentalGymnastics.Core`.

## Persisted Data

The local database stores a JSON document with `Kind` and `SchemaVersion` metadata. Records use stable identifiers rather than display text as source of truth.

| Category | Primary types | What is persisted |
| --- | --- | --- |
| Capability and snapshot boundary | `LocalPersistenceBoundary`, `LocalProgramStateSnapshot`, `ILocalProgramStateStore` | Offline-only capability declaration and whole-program snapshot contracts for simple storage adapters. |
| Database metadata | `LocalDatabaseInitializer`, `LocalDatabaseSchema`, `LocalDatabaseOptions` | App-owned path, database kind, current schema version, storage ownership, connectivity, and applied migrations. |
| Stable identifiers | `StableDomainIdentifiers` | Stable string ids for branches, levels, drills, session types, gate outcomes, failure types, maintenance states, branch-level states, artifact categories, observable evidence kinds, result evidence kinds, pass states, and prompt content kinds. |
| Practitioner and branch-level state | `LocalPractitionerStateStore`, `LocalBranchLevelStateStore` | Current `PractitionerState` as branch-level pairs with branch, level, and `BranchLevelState`. |
| Session history | `LocalSessionHistoryStore`, `LocalSessionHistoryRecord` | Completed sessions with date, session type, branch-levels, drill or transfer task, intensity, load variables, clean-performance marker, recovery/deload markers, notes, and evidence artifact ids. |
| Evidence artifacts | `LocalEvidenceArtifactStore`, `LocalEvidenceArtifactRecord` | Programming event reference, artifact category, artifact date, observable evidence entries, summary or local reference, and optional subjective note. |
| Formal attempts | `LocalFormalTestAttemptStore`, `LocalFormalTestAttemptRecord` | Formal test attempt id, evidence artifact id, branch, level, date, task, load variables, standard, critical constraints, result evidence, failure type when applicable, pass state, and embedded artifact facts from core. |
| Stabilization passes | `LocalStabilizationPassStore`, `LocalStabilizationPassRecord` | Stabilization pass id, evidence artifact id, optional source attempt/session ids, drill, condition, condition description, and `StabilizationPassEvidence`. |
| Maintenance and restoration checks | `LocalMaintenanceCheckStore`, `LocalMaintenanceCheckRecord`, `LocalRestorationCheckRecord` | Maintenance and restoration check ids, evidence artifact ids, optional session ids, drill, stated standard, and core check evidence. |
| Decay and restoration history | `LocalDecayRestorationHistoryStore`, `LocalDecayHistoryRecord`, `LocalRestorationHistoryRecord` | Decay/restoration ids, dates, current and next branch-level statuses, core state-machine transition, source check ids, and restoration evidence. |
| Generated drill instances | `LocalGeneratedDrillInstanceStore`, `LocalGeneratedDrillInstanceRecord` | Generated instance id, generated date, branch, level, drill, load variables, content identity, content kind, equivalence class, version, runtime state, active session id, and result evidence artifact id. |
| Active runtime snapshots | `LocalActiveRuntimeSessionSnapshotStore`, `LocalActiveRuntimeSessionSnapshotRecord` | Active session id, generated drill instance identity, runtime session definition, lifecycle state, phase plan, active phase/timing state, pending cue state, ordered runtime events, evidence facts, and correctable-event reference needed for Runtime restore. |
| Progress summaries | `LocalProgressSummaryStore`, `LocalProgressSummaryRecord` | Non-authoritative local read models: branch summaries, maintenance summaries, blockers, bottleneck branch, next programmed emphasis, counts, dates, and source record references. |
| Repository/query view | `LocalProgramRepository` | App-facing queries for current state, recent sessions, due maintenance, evidence history, progress-relevant records, and generated drill instances. |
| Backup packages | `LocalBackupService`, `LocalBackupPackage` | Local-only export/restore envelope containing schema metadata, offline/app-owned flags, and the local database document. |

Static program catalog data is not persisted. Branch definitions, unlock rules, level meanings, standards, drill protocols, rubrics, maintenance cadences, transfer rules, failure responses, weekly templates, and anti-self-deception rules come from `MentalGymnastics.Core`.

Derived rule decisions should not be persisted as authority. Progress summaries may cache display-oriented conclusions only as non-authoritative read models with source record references.

## Lifecycle

### Initialization

`LocalDatabaseInitializer` creates the local database document if it does not exist and records `LocalDatabaseSchema.CurrentVersion`. Initialization is repeatable and local. It does not contact external services and does not require accounts.

Callers create options with `LocalDatabaseOptions.ForAppOwnedPath(...)`. Paths that look like external service targets are rejected.

### Reads

Stores initialize the database before reading. Missing record sections are treated as empty collections where that is valid. Stores rehydrate records into core types or persistence records that wrap core types.

Repository APIs are for app convenience only. `LocalProgramRepository` may call core evaluators when building read models such as due maintenance, but it does not persist those decisions as progression truth.

### Writes

Stores write local JSON records using stable identifiers and replace the database file through a temporary file and atomic move. Writes are local file operations only.

Individual stores validate record shape and core-type invariants in constructors. They do not decide whether advancement is earned.

### Atomic Programming Events

Use `LocalProgrammingEventTransaction` when branch state, evidence, formal attempts, or session history belong to one programming event. The transaction edits an in-memory database document, runs `LocalPersistenceIntegrityValidator` before replacement, and writes the document only if the update is coherent.

This protects against partial writes that would leave a branch state, attempt, evidence artifact, or session record inconsistent.

### Import and Export

`LocalBackupService.ExportAsync(...)` creates a local backup package from the current database document. `RestoreAsync(...)` accepts only the supported local backup schema and current database schema, verifies app-owned/offline metadata, validates persistence integrity, and only then replaces the local database.

Backup is local backup/restore support, not cloud sync. It must not grow accounts, server ids, analytics, telemetry, push tokens, or AI provider metadata.

### Progress Summary Refresh

`LocalProgressSummaryStore.RefreshAsync(...)` derives a display-oriented summary from persisted facts and core maintenance rules, saves it as non-authoritative, and records source references. App code may display the summary, but the underlying sessions, attempts, evidence, maintenance checks, and practitioner state remain authoritative.

### Active Runtime Resume

`LocalActiveRuntimeSessionSnapshotStore` saves the live facts needed to resume an in-progress Runtime session from the local JSON database. It records Runtime snapshot data without referencing `MentalGymnastics.Runtime`, so the dependency direction remains Persistence -> Core only.

Saved active snapshots are not successful evidence and must not grant advancement. App integration must restore them through Runtime snapshot/restore APIs. If Runtime rejects a snapshot as unsafe or non-resumable, the app must treat it as non-successful continuation state, not as a completed session, formal attempt, stabilization pass, maintenance pass, or gate result.

Active snapshot delete and clear operations affect only the in-progress resume collection. They must not replace completed session history, evidence artifacts, formal attempts, or generated instance completion records.

## Migrations

Schema versioning is explicit through `LocalDatabaseSchema.CurrentVersion`.

Migrations implement `ILocalDatabaseMigration` with `FromVersion`, `ToVersion`, and deterministic `ApplyAsync(...)`. `LocalDatabaseInitializer` applies migrations sequentially from the existing local version to the current version, updates the schema version after each successful migration, and writes the migrated document by replacement.

Migration failures throw `LocalDatabaseMigrationException` and must not silently corrupt progression data. A migration should transform storage shape only. It should not reinterpret standards, grant advancement, alter branch relationships, or encode new domain decisions. If a migration needs a new progression rule, add the rule to `MentalGymnastics.Core` with tests first.

## Integrity Validation

`LocalPersistenceIntegrityValidator` checks the local document for storage coherence:

- Missing required records.
- Invalid cross-record references.
- Unknown branch or level identifiers and other stable identifier failures.
- Impossible persisted branch-level lifecycle history, checked through `BranchLevelStateMachine`.
- Orphaned evidence artifacts.

The validator protects stored facts. It does not evaluate test readiness, formal gate outcomes, maintenance currency, ownership, dependency caps, transfer eligibility, weekly plans, or global review decisions.

Integrity validation is used directly as a diagnostic tool and is enforced before local backup restore and before atomic programming-event commits. Future bulk import, repair, compaction, or schema migration workflows should run it before replacing authoritative local data.

## Consumption From Android Or Session Runtime

Future Android UI and session runtime code should consume persistence in this order:

1. Use `LocalDatabaseOptions.ForAppOwnedPath(...)` for the app-owned local file path.
2. Initialize storage with `LocalDatabaseInitializer` at app startup or first use.
3. Use `LocalProgramRepository` for app-screen queries: current state, recent sessions, evidence history, due maintenance, progress records, and reusable generated drill instances.
4. Use specific stores when a screen or runtime needs one record type, such as sessions, evidence artifacts, attempts, maintenance checks, or generated drill instances.
5. Use `LocalActiveRuntimeSessionSnapshotStore` only for active in-progress session resume data, then restore through `MentalGymnastics.Runtime` before allowing continuation.
6. Use `LocalProgrammingEventTransaction` when one completed programming event must save related state, evidence, attempt, and session records atomically.
7. Pass loaded facts into `MentalGymnastics.Core` evaluators for readiness, gate, stabilization, ownership, maintenance, decay, restoration, dependency cap, global balance, transfer, recovery, deload, weekly planning, category classification, stuck-state, and global review decisions.
8. Persist the resulting facts or source evidence, not a parallel copy of the rule.

Android UI may format records, collect inputs, display core evaluator results, and choose navigation. It must not create alternate local databases, SharedPreferences progression stores, Room entities that redefine core facts, duplicated branch catalogs, duplicate maintenance calculations, or UI-local advancement flags.

Session runtime code may reserve or complete generated drill instances, save local session results, attach evidence artifacts, and record attempts. It must not generate fresh-equivalent or transfer eligibility decisions outside the core prompt/content and transfer contracts.

## Offline-Only Constraints

Persistence must remain:

- Local to the device.
- Userless and account-free.
- Offline-only.
- Free of backend, sync, telemetry, analytics, push notification, billing, or AI/API dependencies.
- Independent of Android UI and platform UI APIs.
- Deterministic in tests.

Local backup files may be moved by the user or platform UI later, but this layer does not implement sharing UI or cloud behavior.

## Tested Invariants

The persistence test suite locks down these invariants:

- The persistence boundary declares app-owned, local-device, offline-only, userless storage and rejects ownership of progression logic.
- Persistence consumes `MentalGymnastics.Core` types and does not reference Android.
- Database initialization is repeatable and tracks schema version.
- Migrations are deterministic and fail without silently corrupting data.
- Stable domain identifiers round trip and do not use display wording as source of truth.
- Stores can save, load, update, list, order, and filter their record categories.
- Evidence artifacts reject vague encouragement as replacement evidence.
- Formal attempts, stabilization records, maintenance records, decay/restoration history, generated drill instances, and summaries preserve the data needed by core evaluators.
- Transaction commits are atomic and integrity-checked.
- Local backup export/restore preserves offline data and rejects invalid or integrity-broken packages without replacing existing data.
- Integrity validation detects representative corrupted local data.
- Repository queries expose app-needed local records without duplicating core progression logic.
- Active runtime snapshots round trip locally, preserve generated instance, phase/timing, pending cues, event history, and evidence facts, and reject progression/gate decision facts as active state.

When future agents add storage behavior, add tests at this boundary and keep rule changes in `MentalGymnastics.Core`.
