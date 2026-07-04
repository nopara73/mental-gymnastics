# Documentation

This section contains the product and engineering foundation for Mental Gymnastics.

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
