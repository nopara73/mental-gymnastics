# Agent Instructions

These instructions apply to the entire repository.

## Required Reading

Before starting any work in this repo, every agent must read these documents in full:

- [docs/foundation/progression-against-vibes.md](docs/foundation/progression-against-vibes.md)
- [docs/foundation/standards-based-skill-ladder.md](docs/foundation/standards-based-skill-ladder.md)

Treat these documents as the foundational product philosophy and programming frame for Mental Gymnastics. They are not decorative context. They should shape product choices, UX language, domain modeling, progression mechanics, training standards, and engineering tradeoffs.

## Working Standard

- Prefer progression models, criteria, and observable capacity over vague engagement.
- Design against self-deception where user progress is private or subjective.
- Avoid adding features that merely make practice feel meaningful without improving structure, feedback, or advancement.
- When proposing or implementing app behavior, be able to explain how it supports load, constraints, progression, testing, stabilization, or transfer.

## Core Library Reuse

The pure C# core library in `src/MentalGymnastics.Core` is the domain authority for implemented training-program rules. Before adding or changing app, Android, CLI, persistence, or service behavior that touches progression, readiness, gates, stabilization, ownership, maintenance, decay, dependency caps, global balance, failures, transfer, recovery, deload, weekly planning, practitioner classification, prompt/content abstractions, or global review, read [docs/core-library.md](docs/core-library.md) and use the existing core types and evaluators.

Do not reimplement core domain logic in UI, Android, persistence, network, or infrastructure layers. Those layers may collect inputs, display outputs, persist records, and schedule work, but documented and tested rule decisions belong in `MentalGymnastics.Core`. If a required rule is missing, add it to the core with tests first and tie it back to [docs/program/training-program.md](docs/program/training-program.md).

## Local Persistence Boundary

Local persistence contracts and stores live in `src/MentalGymnastics.Persistence` and are documented in [docs/local-persistence-boundary.md](docs/local-persistence-boundary.md). Persistence must remain offline-only, userless, and local to the device. It may store and reload core domain facts, but it must not introduce accounts, sync, backend services, telemetry, push notifications, AI/API dependencies, Android UI behavior, or duplicate progression logic.

Before adding Android, CLI, session runtime, import/export, backup, or local storage behavior, use the existing persistence layer instead of inventing new local storage paths. Prefer `LocalDatabaseInitializer`, the specific local stores, `LocalProgramRepository`, `LocalProgrammingEventTransaction`, `LocalBackupService`, and `LocalPersistenceIntegrityValidator` as appropriate. Do not create parallel SharedPreferences files, Room entities, JSON files, caches, or UI-owned flags for practitioner state, branch-level states, sessions, attempts, evidence, stabilization, maintenance, decay, restoration, generated drill instances, or progress summaries. If the persistence layer is missing a needed record or query, add it there with tests and keep progression decisions delegated to `MentalGymnastics.Core`.

## Session Runtime Reuse

Headless live drill execution lives in `src/MentalGymnastics.Runtime` and is documented in [docs/session-runtime-boundary.md](docs/session-runtime-boundary.md). Before adding Android, CLI, test-harness, or generated-content behavior that touches session definitions, lifecycle, timers, phases, cues, command handling, response timing, scoring events, evidence capture, protocol execution, snapshot/restore, completion results, or runtime-to-core/persistence handoffs, use the existing runtime layer instead of creating ad hoc drill execution logic.

Android UI should render runtime state and forward user actions. It must not maintain independent phase timers, cue schedulers, hidden session logs, screen-local evidence classifiers, protocol-specific pass/fail logic, or local progression flags. The runtime may produce Core and Persistence handoff records, but progression decisions remain in `MentalGymnastics.Core` and storage remains in `MentalGymnastics.Persistence`. If runtime support is missing, add it in `MentalGymnastics.Runtime` with deterministic tests first.

## Generated Content Reuse

Local deterministic generated drill content lives in `src/MentalGymnastics.Content` and is documented in [docs/generated-content-boundary.md](docs/generated-content-boundary.md). Before adding Android, CLI, runtime, persistence, or test-harness behavior that creates prompt material, target holds, distractors, cue sequences, memory items, transform tasks, go/no-go streams, exception rules, discrimination pairs, seeded audits, rule examples, structure mappings, pressure/disruption variants, transfer content, or TI composite materials, use the existing generated content layer instead of creating ad hoc content.

Android UI should render generated material and forward actions into `MentalGymnastics.Runtime`. It must not create screen-local prompts, cue schedules, hidden test variants, local content identity schemes, or freshness/equivalence decisions. Generated content may package instances for Runtime and hand off records to Persistence, but progression decisions remain in `MentalGymnastics.Core`, live execution remains in `MentalGymnastics.Runtime`, and storage remains in `MentalGymnastics.Persistence`. If generated content support is missing, add it in `MentalGymnastics.Content` with deterministic tests first.
