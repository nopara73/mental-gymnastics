# MentalGymnastics Core Library

**Status:** Completed pure core library reference
**Project:** `src/MentalGymnastics.Core`
**Tests:** `tests/MentalGymnastics.Tests`
**Source program:** [docs/program/training-program.md](program/training-program.md)

This document describes the C# core library as implemented. The core library is the domain authority for the training program rules that have already been modeled and tested. Future app layers should consume these types and evaluators instead of reimplementing progression logic in UI, Android, storage, or service code.

## Scope

`MentalGymnastics.Core` owns the pure domain model and deterministic rule engines for the program:

- Fixed program vocabulary: branches, branch types, global levels, capacities, drills, standards, session and gate terms, failure types, maintenance states, review decisions, and related enums.
- Program catalog data: the documented branch map, global levels, branch unlock prerequisites, capacities, drill definitions, standards, transfer tests, drill protocols, qualitative rubrics, load rules, and regression rules.
- Branch-level state representation and lifecycle validation.
- Practitioner state representation across branch-level pairs.
- Formal test attempt, evidence artifact, standard evaluation, rubric, gate decision, stabilization, ownership, maintenance, decay, restoration, dependency cap, and global balance primitives.
- Test readiness, transfer eligibility, recovery, deload, failure routing, repeated-failure and stuck-state detection, practitioner category classification, weekly programming templates, global review decisions, prompt/content abstractions, and anti-self-deception guards.

The core library intentionally does not own:

- Android, UI screens, interaction flows, navigation, view models, copy, or motivational messaging.
- Persistence, file storage, databases, serialization contracts, migrations, analytics, notifications, calendar integration, or network calls.
- System clock access. Callers pass `TrainingDate` or explicit durations into evaluators.
- Prompt generation services, AI APIs, random content selection, or external content banks. The core defines deterministic abstractions and selection behavior for future content sources.
- Timer execution, drill runtime loops, scoring widgets, or formal-test administration UX.
- User account identity, sync, permissions, billing, or any platform concerns.

App layers may collect inputs, display results, persist records, schedule reminders, and adapt content presentation. They should not decide whether advancement is allowed, whether prerequisites are satisfied, whether maintenance is stale, whether decay caps dependent branches, or whether transfer is valid when the core already has a type or evaluator for that rule.

The local persistence boundary is documented in [docs/local-persistence-boundary.md](local-persistence-boundary.md). Persistence code should load and save core facts, not create alternate progression rules.

## Consumption Pattern

Use the core as a pure decision library:

1. Load or construct explicit domain records from app state, storage, or user-entered evidence.
2. Pass those records to the relevant static evaluator or catalog.
3. Persist or display the returned result without weakening it in the app layer.
4. Add or update core tests before changing a rule.

Example shape:

```csharp
var readiness = TestReadinessEvaluator.Evaluate(
    new TestReadinessRequest(
        practitionerState,
        BranchCode.FS,
        GlobalLevelId.L3,
        DrillId.FS2InvalidCueFilter,
        drillDemand,
        recentPracticeSessions,
        prerequisiteMaintenanceChecks,
        statedStandard,
        namedHonestyConstraint));

if (!readiness.MayTest)
{
    // UI may show the failures, but it must not bypass them.
}
```

The library uses immutable or read-only data shapes for inputs and outputs. Evaluators are deterministic and side-effect free. They do not mutate `PractitionerState`; when a lifecycle change is legal, the state machine returns the next state/status.

## Main Concepts

### Program Catalog

Use `ProgramCatalog`, `DependencyCapCatalog`, `TransferTestCatalog`, `DrillProtocolCatalog`, `QualitativeRubricCatalog`, `LoadRegressionRuleCatalog`, and `GlobalReviewDecisionCatalog` for fixed program vocabulary and definitions.

Do not recreate branch lists, drill names, unlock rules, standards, transfer definitions, weekly session names, or rubric outcome definitions in app code. UI layers can format these definitions, but the source values should come from the catalog or enums.

### Branch-Level State

`BranchLevelStatus` records one branch-level pair. `PractitionerState` is the collection of those statuses.

`BranchLevelStateMachine.TryApply(...)` is the only implemented authority for lifecycle transitions among:

- `Unopened`
- `Training`
- `TestReady`
- `PassedOnce`
- `Stabilizing`
- `Owned`
- `Maintenance`
- `Decayed`

Illegal transitions return invalid results and preserve the current state. App code should never directly "upgrade" a branch-level pair from passed once to owned, from decayed to test-ready, or from training to passed once.

### Evidence and Attempts

`EvidenceArtifact` requires observable evidence. Subjective notes may be attached where supported, but they are not advancement evidence by themselves.

`FormalTestAttempt` records the formal test facts required by the program: branch, level, task, load variables, standard, critical constraints, score/rubric/pass-fail evidence, failure type when failed, pass state, and artifact. A failed formal attempt must include a classified failure type.

### Standards, Gates, and Ownership

`StandardEvaluator` handles numerical thresholds, output completeness, critical constraints, and rubric requirements. A broken or missing critical constraint fails the evaluation regardless of score.

`FormalGateDecisionEngine` turns a formal attempt plus standard evidence into a gate outcome. It does not grant ownership from one pass.

`StabilizationOwnershipEvaluator` is the ownership authority. Ownership requires three clean passes of the same standard, across at least seven days, with two stabilization passes, one adjacent-work or controlled-distractor pass, unchanged standard, and a named main failure mode avoided.

### Readiness and Prerequisites

`TestReadinessEvaluator` checks whether a branch-level may be formally tested. It requires:

- Documented prerequisite branch-level states.
- Two recent clean practice sessions on the same drill demand.
- Current maintenance for owned prerequisites.
- The catalog standard stated before the test.
- The drill honesty constraint named before the test.

Branch-level requirement satisfaction is centralized inside the core. App layers should not reimplement the difference between "passed once" prerequisites and "owned" prerequisites.

### Maintenance, Decay, and Dependency Caps

`MaintenanceCurrencyEvaluator` applies the documented maintenance cadences:

- Foundational L1-L2: weekly.
- Foundational L3+: 7 to 10 days.
- Advanced branches: 10 to 14 days.
- TI L3+: global composite every 28 days.

One failed maintenance check creates warning state. Two consecutive failed checks can mark a maintenance branch decayed through `DecayRestorationEvaluator`.

`DecayRestorationEvaluator` separates ordinary training failure from maintenance decay. Restoration requires the documented evidence: one pass of the last owned standard and one lower-load transfer check.

`DependencyCapEvaluator` blocks dependent advanced work when prerequisite branches are decayed or prerequisite maintenance is overdue or missing. It represents the CO, AI, and TI dependency blocks from the program.

### Global Balance and Practitioner Category

`GlobalBalanceEvaluator` prevents advancement through strengths while ignoring blockers. It enforces foundational level spread, advanced prerequisite maintenance, decayed prerequisite blocks, TI component failures from the last global review, and advanced classification dependence on a passed global review.

`PractitionerCategoryClassifier` classifies beginner, intermediate, and advanced from program state and maintenance status, not self-description.

### Load, Regression, Recovery, and Deload

`LoadChangeEvaluator` and `RegressionRuleEvaluator` represent branch-specific load variables, prohibited load increases, allowed regressions, and forbidden regressions.

A valid regression must preserve the same core demand and honesty constraint. If a proposed regression removes drift marking, permits unmarked guesses, lowers an AI source standard, or removes TI branch-specific evidence, it is not a valid regression in the core.

`RecoveryDecisionEvaluator` triggers recovery only from documented evidence: consecutive overload failures, rising errors with unchanged load, broken honesty constraints, broad adjacent decay, or same-branch high-intensity testing within 24 hours. Recovery reduces one load variable, preserves the core constraint, records one artifact, and disallows advancement testing.

`DeloadDecisionEvaluator` triggers deload when two or more branches show overload or decay in the same week. Deload suspends advancement testing while preserving maintenance checks.

### Failure Routing and Stuck State

`FailureResponseRouter` accepts classified failures and returns the documented programming response for technical failure, effort failure, overload, or bad programming. The router does not infer psychological intent.

`StuckStateDetector` detects stuck states from repeated evidence, not frustration. It covers repeated gate failures, repeated critical-constraint regression failures, repeated prerequisite decay during dependent training, repeated bottlenecks across global reviews, and isolated drill success paired with repeated related transfer failures.

### Transfer and Prompt Content

`TransferEligibilityEvaluator` validates transfer tests by source branch, trained capacity, same demand, changed context, visible source standard evidence, and fresh equivalent retest requirement. Novelty alone is not transfer.

Prompt/content abstractions (`IPromptContentSource`, prompt content models, and `DeterministicPromptContentSelector`) allow future content sources to provide equivalent prompts, cue sequences, delayed reconstruction tasks, discrimination items, and rule examples. Future generators or prompt banks must plug into these abstractions instead of making transfer or retest freshness decisions elsewhere.

### Weekly Planning and Global Review

`WeeklyProgrammingPlanner` generates beginner, intermediate, and advanced weekly structures from the program. It respects practitioner category, maintenance currency, recovery requirements, and global-review advancement pauses. Beginners follow the fixed template until the classification rules say otherwise.

`GlobalReviewEvaluator` evaluates the whole practitioner. It requires current owned-level inputs, maintenance status, failure classifications, evidence artifacts, bottleneck response, volume/intensity history, recovery/deload history, and advancement records. A review cannot pass when prerequisite branches are decayed, maintenance is overdue, bottleneck response is missing, current transfer/stabilization evidence is missing, or advancement happened by participation alone.

## Tested Invariants

The test suite documents behavior future agents should preserve. These invariants should not be reimplemented in app layers.

### False Advancement

- Participation, effort, insight, novelty, self-description, missing evidence, changed standards, removed honesty constraints, skipped prerequisites, and avoided retests cannot support advancement.
- Excellence can be represented, but it does not bypass prerequisites or ownership requirements.
- Formal gates require passing standard evidence; participation or effort text in an attempt does not pass the gate.

### Critical Constraints

- A broken or missing critical constraint fails an attempt regardless of score, output completeness, or rubric outcome.
- Incomplete output, numerical threshold failures, and rubric failures independently prevent passing.

### State Machine

- Passing once does not equal ownership.
- Ownership is reachable only through stabilization completion, not directly from training, test-ready, passed-once, maintenance, or decay.
- Illegal lifecycle transitions are rejected without mutating state.
- Decay and restoration remain separate from ordinary advancement.

### Readiness and Prerequisites

- Foundational branches beyond FH require FH L1 passed once before L1 testing.
- Higher levels require documented owned prerequisites.
- Advanced branches require their documented prerequisite branches and levels.
- Test readiness requires two clean recent practice sessions on the same drill demand, current prerequisite maintenance, stated standard, and named honesty constraint.

### Maintenance, Decay, and Caps

- Future maintenance checks do not make maintenance current.
- Wrong maintenance check kind does not satisfy TI global composite cadence.
- One failed maintenance check is warning; two can create decay.
- A decayed prerequisite or stale prerequisite maintenance caps dependent advanced advancement.
- Dependency caps cover CO, AI, and TI routes, including TI's "one of CO L2 or AI L2" route.
- Restored prerequisite state restores advancement eligibility only when restoration evidence is present.

### Global Balance

- Foundational branches cannot spread more than two owned levels apart.
- Advanced branches cannot advance through decayed or overdue prerequisite branches.
- TI cannot advance when a component branch failed the last global review.
- Advanced classification requires the last global review to have passed.

### Transfer

- Transfer requires the documented source branch, trained capacity, same demand, changed context, visible source standard evidence, and fresh equivalent retesting.
- Unsupported transfer, capacity mismatch, hidden source standard, wrong source level, missing source standard, missing retest, or identical-content retest requirement is rejected.
- Novelty without preserved source demand is not transfer.

### Load, Regression, Recovery, and Deload

- Acquisition load may increase only one primary load variable at a time.
- Advanced integration may increase two variables only when both are stable separately.
- Forbidden load increases are rejected.
- Regressions must preserve core demand and honesty constraints.
- Recovery and deload are triggered only by documented evidence, not subjective notes.
- Recovery and deload suspend advancement testing while keeping maintenance contact.

### Failure and Review

- Failure responses are routed by classified evidence: technical failure, effort failure, overload, and bad programming have distinct prescribed actions.
- Stuck state requires repeated evidence, not subjective frustration.
- Global review evaluates the whole practitioner, not isolated drill performance.
- Global review pass requires no decayed prerequisite branch, no overdue maintenance, programmed bottleneck response, current transfer or stabilization artifact, and no participation-only advancement.

## Future Work Rules

When adding app, Android, CLI, API, or persistence layers:

1. Prefer calling `MentalGymnastics.Core` evaluators and catalogs over duplicating branch, gate, maintenance, transfer, review, or planning logic.
2. Keep platform-specific code responsible for collection, presentation, persistence, and scheduling only.
3. Pass explicit dates and evidence into the core. Do not add direct system clock, network, database, Android, or UI dependencies to the core.
4. If the app needs a rule not currently modeled, add the rule to the core with tests first, and tie it back to `docs/program/training-program.md`.
5. If a documentation statement and a test disagree, treat the test and program document as the immediate evidence. Fix the mismatch with a narrow core test or documentation correction before building on it.
6. Do not introduce parallel "helper" logic in frontends for advancement, readiness, caps, ownership, maintenance, transfer, recovery, deload, weekly planning, category classification, or global review.
