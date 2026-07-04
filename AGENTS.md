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

Local persistence contracts live in `src/MentalGymnastics.Persistence` and are documented in [docs/local-persistence-boundary.md](docs/local-persistence-boundary.md). Persistence must remain offline-only, userless, and local to the device. It may store and reload core domain facts, but it must not introduce accounts, sync, backend services, telemetry, push notifications, AI/API dependencies, Android UI behavior, or duplicate progression logic.
