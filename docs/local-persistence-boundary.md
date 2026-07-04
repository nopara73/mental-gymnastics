# MentalGymnastics Local Persistence Boundary

**Status:** Initial boundary and contracts
**Project:** `src/MentalGymnastics.Persistence`
**Tests:** `tests/MentalGymnastics.Persistence.Tests/LocalPersistenceBoundaryTests.cs`
**Depends on:** `src/MentalGymnastics.Core`

This document defines the persistence boundary for Mental Gymnastics. Persistence is a local storage concern only. It records program facts and evidence so app layers can later reload them and ask the core library for decisions.

The local data requirements are documented in [docs/local-persistence-requirements.md](local-persistence-requirements.md).

## Reference Direction

The allowed project direction is:

1. `MentalGymnastics.Core`
2. `MentalGymnastics.Persistence` references `MentalGymnastics.Core`
3. Future frontends such as Android or CLI tools may reference both

`MentalGymnastics.Core` must not reference persistence. Persistence must not reference Android UI, platform UI APIs, network clients, account systems, sync services, telemetry, push notifications, AI services, or backend code.

The current persistence project is a pure `net10.0` class library. It has no package references and only consumes core domain types.

## Owned Responsibilities

`MentalGymnastics.Persistence` owns the boundary for offline, userless, device-local program storage:

- The persistence capability declaration in `LocalPersistenceBoundary`.
- Whole-program snapshot contracts through `LocalProgramStateSnapshot`.
- The local load/save interface through `ILocalProgramStateStore`.
- Future local-only storage implementations, if they can remain independent of Android UI and external services.
- Future local migrations or schema mapping for stored program facts.

The snapshot currently records core types for:

- Practitioner branch-level state.
- Evidence artifacts.
- Formal test attempts.
- Maintenance checks.
- Classified failures.

These are stored as core concepts, not as parallel persistence concepts.

## Explicit Non-Responsibilities

Persistence must not own or implement:

- Accounts, profiles, sign-in, identity, or multi-user behavior.
- Sync, backend APIs, cloud storage, telemetry, analytics, push notifications, or AI/API dependencies.
- Android screens, view models, navigation, widgets, copy, or drill execution UI.
- Progression decisions, readiness checks, gate outcomes, stabilization, ownership, maintenance currency, decay, dependency caps, global balance, transfer eligibility, recovery, deload, weekly planning, practitioner classification, failure routing, or global review decisions.
- Alternate branch lists, level definitions, standards, rubrics, drill names, or unlock rules.

If app or storage code needs one of those decisions, it should load persisted facts, call the corresponding evaluator in `MentalGymnastics.Core`, and persist or display the result.

## Consumption Pattern

A future local store should implement `ILocalProgramStateStore`:

```csharp
public interface ILocalProgramStateStore
{
    ValueTask<LocalProgramStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(LocalProgramStateSnapshot snapshot, CancellationToken cancellationToken = default);
}
```

The intended flow is:

1. Load a `LocalProgramStateSnapshot` from local device storage.
2. Pass its core records to core evaluators such as `TestReadinessEvaluator`, `MaintenanceCurrencyEvaluator`, or `FormalGateDecisionEngine`.
3. Display or act on the returned core result in the frontend.
4. Save updated evidence, attempts, maintenance checks, failures, or branch-level state after a valid core decision.

Persistence may preserve dates, evidence references, and stored facts. It should not infer that participation, effort, insight, novelty, or missing evidence produces advancement.

## Tested Invariants

The persistence boundary tests lock down these invariants:

- Persistence declares itself offline-only, userless, and device-local.
- Persistence explicitly rejects accounts, sync, backend services, telemetry, push notifications, AI/API dependencies, and ownership of progression logic.
- Snapshots consume `MentalGymnastics.Core` domain types directly.
- Snapshots defensively copy mutable input collections.
- The store boundary loads and saves whole local snapshots.
- The persistence assembly references Core and does not reference Android.

Future storage implementations should add tests at this boundary before adding executable behavior. File, SQLite, or platform storage details are implementation choices only if they preserve the same local-only, no-duplicate-rules contract.
