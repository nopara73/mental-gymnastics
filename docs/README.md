# Documentation

This section contains the product and engineering foundation for Mental Gymnastics.

## Current Non-UI Stack

The repository now contains a completed pre-UI stack for the training program:

- `MentalGymnastics.Core` owns progression rules and static program vocabulary.
- `MentalGymnastics.Persistence` owns offline, userless, app-owned local JSON storage. SQLite is intentionally not required at this stage.
- `MentalGymnastics.Runtime` owns headless live drill execution.
- `MentalGymnastics.Content` owns local deterministic generated drill content and runtime/persistence handoffs.
- `MentalGymnastics.App` composes those layers into non-visual workflows for future Android UI.

Future Android screens should consume `MentalGymnastics.App` workflow/read-model services, render their output, and forward user actions into app/runtime commands. Android UI should not bypass the app integration layer to reimplement progression decisions, local storage paths, timers, cue schedules, prompt generation, evidence logs, completion processing, or progress refresh.

## Foundation Documents

- [MentalGymnastics: Progression Against Vibes](foundation/progression-against-vibes.md)
- [MentalGymnastics: Standards-Based Skill Ladder](foundation/standards-based-skill-ladder.md)

The foundational philosophy is not background reading. It is the governing product premise for the app: inner practice should be designed as progression, not atmosphere. The programming frame translates that premise into the shape of the training system before the actual progressions are designed.

## Program Documents

- [MentalGymnastics: Complete Training Program](program/training-program.md)

## Core Library

- [MentalGymnastics Core Library](core-library.md)

The core library documentation explains which training-program rules are already implemented in pure C#, which concerns belong outside the core, and which tested invariants app layers should consume rather than reimplement.

## Persistence Boundary

- [MentalGymnastics Local Persistence Boundary](local-persistence-boundary.md)
- [MentalGymnastics Local Persistence Requirements](local-persistence-requirements.md)

The persistence documentation explains where local offline storage contracts live, what facts must be stored locally, how they reference the core library, and which responsibilities must stay out of persistence.

## Session Runtime

- [MentalGymnastics Session Runtime Layer](session-runtime-boundary.md)

The session runtime documentation explains how the implemented headless runtime administers live drill sessions, captures evidence, supports protocol execution, handles snapshot/restore, and hands records to Core and Persistence without taking over progression rules, local storage, Android UI, or external-service concerns.

## Generated Content

- [MentalGymnastics Generated Drill Content Boundary](generated-content-boundary.md)
- [MentalGymnastics Generated Content Requirements](generated-content-requirements.md)
- [MentalGymnastics Generated Content MVP Offline Depth Audit](generated-content-mvp-offline-audit.md)

The generated content documentation explains how the implemented local deterministic content layer produces drill instances, prompt material, cue sequences, item sets, seeded errors, transfer variants, and fresh equivalent variants without taking over Core progression rules, Persistence storage, Runtime execution, Android UI, or external-service concerns.

## App Integration

- [MentalGymnastics Pre-UI App Integration Boundary](app-integration-boundary.md)

The app integration documentation defines the pure C# layer that composes Core, Persistence, Runtime, and Generated Content into app-facing workflows while keeping Android UI and external-service concerns out of scope. Use it for startup configuration, first-run state initialization, current-state loading, work selection, generated content preparation, runtime session preparation, completed-session processing, active session snapshot handling, and progress refresh.

## Android UI

- [MentalGymnastics Android UI Layer](android-ui-layer.md)
- [MentalGymnastics Android UI Strategy](android-ui-strategy.md)
- [MentalGymnastics Android Visual State Language](android-visual-state-language.md)
- [MentalGymnastics Android Component Inventory](android-component-inventory.md)
- [MentalGymnastics Android Screen Plans](android-screen-plans.md)

The Android UI documents define the native UI ownership boundary plus a simple, visual-first, low-text direction for screens, including state language, reusable component contracts, and low-fidelity workflow screen plans, while preserving standards, honesty constraints, failure, decay, blocked advancement, stabilization, transfer, offline-only storage, and the pre-UI layer boundaries.
