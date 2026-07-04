# MentalGymnastics Training Program Requirement Review

**Status:** Review and audit record
**Reviewed specification:** [docs/program/training-program.md](../training-program.md)
**Reviewed diagrams:** [docs/diagrams/README.md](../../diagrams/README.md)
**Scope:** This document records requirement checks. It is intentionally separate from the programming specification.

## Review Method

The check was run as a thinking test against the ordered requirements. Failures were handled by fixing the first concrete failure before continuing.

## Correction Log

| Cycle | First Failed Requirement | Finding | Fix |
| --- | --- | --- | --- |
| 1 | 118-119 | The programming specification contained a requirement pass report and open coding questions. | Moved review material into this document and left the specification focused on program structure. |
| 2 | 98-99 | The requested state set included `Stabilizing`, but the program state table did not define it explicitly. | Added `Stabilizing` and defined `BranchLevelState` and `PractitionerState`. |
| 3 | 96, 101-106 | The initial diagram set did not include every required model view. | Added evidence artifact flow and global review diagrams, and aligned the domain and state diagrams with the program terms. |

## Program Requirement Pass Report

| Requirement Range | Result | Evidence |
| --- | --- | --- |
| 1-3 Philosophy and ladder | Pass | The purpose, core terms, branch map, and advancement logic prioritize demonstrated capacity, criteria, and observable gates. |
| 4-15 Capacities and prerequisites | Pass | Trainable capacities, branch relationships, levels, unlock rules, and preparation-before-test rules define capacities that can be loaded, regressed, stabilized, and tested. |
| 16-23 Drill integrity and regressions | Pass | The drill library names purpose, capacity, load, honesty constraint, clean performance, failure modes, and regression while forbidding regressions that remove the core demand. |
| 24-31 Stabilization, retesting, transfer, novelty control | Pass | Advancement logic separates passed once from owned, requires stabilization, defines retesting, and rejects novelty as advancement. |
| 32-45 Load, volume, intensity, recovery, failure response | Pass | Load rules, volume, intensity, frequency, recovery, deload, failure classification, and response rules are explicit and branch-aware. |
| 46-51 Anti-cheating, evidence, maintenance | Pass | Critical constraints, evidence artifacts, customization limits, maintenance rules, and decay blocks make overclaiming and premature advancement harder. |
| 52-60 Sessions, weekly structure, start rules | Pass | Session types, weekly templates, universal assessment, and placement rules reduce beginner self-programming and prevent flattering placement. |
| 61-69 Practitioner categories and encodable structure | Pass | Practitioner categories, BranchLevelState, PractitionerState, formal test records, gates, and branch tables are formal enough for later software modeling without adding app behavior. |
| 70-81 Terminology and standards | Pass | Core terms, mixed-performance order, passing versus excellence, and qualitative rubrics keep terminology consistent and standards observable. |
| 82-90 Balance, bottlenecks, reviews, earned advancement | Pass | Global balance rules, bottleneck capacities, maintenance decay, transfer links, and global review prevent advancement through strengths while ignoring blockers. |

## Diagram Requirement Pass Report

| Requirement Range | Result | Evidence |
| --- | --- | --- |
| 91-95 Purpose and terminology | Pass | The diagram index states each diagram's reason to exist; diagram labels use program terminology and do not redefine the program. |
| 96 Domain model | Pass | `domain-model.mmd` includes Program, Branch, Capacity, Level, Drill, Standard, Gate, Regression, SessionType, TestAttempt, EvidenceArtifact, FailureType, MaintenanceRule, TransferTest, GlobalReview, PractitionerState, and BranchLevelState. |
| 97 Skill tree | Pass | `branch-skill-tree.mmd` shows FH, FS, WM, IR, DE, CO, AI, TI, unlock rules, prerequisite gates, global balance, and decay blocks. |
| 98-100 State and advancement | Pass | `branch-level-state-machine.mmd` distinguishes PassedOnce from Owned, includes Stabilizing, decay, restoration, regression, review, and next-level unlock; `advancement-workflow.mmd` makes participation lead to no advancement. |
| 101 Failure handling | Pass | `failure-handling.mmd` separates technical failure, effort failure, overload, and bad programming with distinct responses. |
| 102 Weekly programming | Pass | `weekly-programming.mmd` separates beginner, intermediate, and advanced structures. |
| 103-104 Transfer and maintenance | Pass | `transfer-maintenance.mmd` shows source standards, transfer paths, maintenance, decay, restoration, and dependent advancement caps. |
| 105 Global review | Pass | `global-review.mmd` shows whole-practitioner inputs, decisions, outputs, and review pass conditions. |
| 106 Evidence artifacts | Pass | `evidence-artifact-flow.mmd` separates lightweight practice artifacts from formal test, stabilization, transfer, maintenance, and global review artifacts. |
| 107-113 Maintainability and syntax | Pass | Diagram files are split by purpose, named clearly, start with valid Mermaid diagram declarations under static inspection, and avoid overloaded single-purpose files. No Mermaid parser was locally available. |
| 114-120 Render status and separation | Pass | No rendered outputs were created because local render tools were unavailable; diagrams support but do not replace the specification, and review material remains separate from the program spec. |

## Rendering Status

No rendered SVG or PNG files were created.

Checked local rendering options:

- `mmdc`: not available on PATH.
- `plantuml`: not available on PATH.
- `npx --no-install @mermaid-js/mermaid-cli --version`: Mermaid CLI package not locally available.

No new rendering tools were installed.

## Known Open Questions Before Coding

1. What exact prompt banks or content generators will provide equivalent but fresh test material for WM, DE, CO, and TI?
2. Will formal tests be self-administered, peer-reviewed, or partly system-scored?
3. What artifact storage model is sufficient without turning evidence into journaling busywork?
4. Which qualitative rubrics need external review before they can be trusted for advancement?
5. How much time should the default beginner session assume: 20, 30, or 45 minutes?
