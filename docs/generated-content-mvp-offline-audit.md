# MentalGymnastics Generated Content MVP Offline Depth Audit

This audit reviews the generated content layer against the standard drill
library and expected repeated offline use. It is a requirements and coverage
audit only: no UI, service, network, AI/API, telemetry, sync, account, or
storage-technology changes are implied.

## Summary

The generated content layer is structurally ready for MVP integration: it is
local, deterministic, versioned, validated, and separated from Core progression
rules, Runtime execution, Persistence storage, and Android UI. Every documented
standard drill has representation coverage.

The current content depth is not yet sufficient for honest repeated offline
practice. Most branch generators use small hard-coded template pools or wrapper
recipes that prove the boundary and invariants, but would repeat recognizable
material quickly during acquisition, formal retests, stabilization, transfer,
maintenance, and global review.

## Sufficient Areas

- Boundary ownership is clear. Generated Content owns drill instances, prompt
  material, cue sequences, item sets, seeded errors, transfer variants,
  freshness/equivalence metadata, runtime packaging, and persistence handoff.
- The layer remains offline-only. There are no AI/API, network, backend, sync,
  account, analytics, telemetry, notification, Android UI, or SQLite concerns.
- The standard drill library is covered by generators for FH-1, FH-2, FS-1,
  FS-2, WM-1, WM-2, IR-1, IR-2, DE-1, DE-2, CO-1, CO-2, AI-1, AI-2, TI-1,
  and TI-2.
- Generated instance identity, content version metadata, deterministic seed
  behavior, freshness, equivalence, validation, difficulty audit, and
  anti-self-deception guards are implemented and tested.
- Runtime and persistence handoffs preserve generated identity, branch, level,
  drill, load variables, constraints, standards visibility, and audit-relevant
  facts without moving progression decisions into generated content.
- `LocalContentBank` provides a deterministic offline selection boundary for
  packaged or locally generated bank entries, including version, load,
  equivalence, constraint, material, and freshness checks.
- `MvpLocalContentBank` provides an initial packaged local corpus for WM-1,
  DE-1, and CO-1 so the most obvious fresh-equivalent retest drills can select
  multiple validated offline variants without Android, network, AI/API, or
  persistence-owned content generation.

## Thin Areas For Repeated Offline Practice

| Area | Current depth | MVP offline risk |
| --- | --- | --- |
| FH-1 Target Hold and FH-2 Distractor Hold | 6 target templates and 6 distractor prompts | Targets and distractors will become recognizable quickly; repeated practice may train familiarity instead of stable target holding under varied distractors. |
| FS-1 Cue Switch and FS-2 Invalid Cue Filter | 6 target templates, 6 invalid cue values, short deterministic cue schedules | Cue and invalid-cue patterns can become predictable; target-set and rule-contrast variety is too narrow for repeated retests. |
| WM-1 Delayed Reconstruction | 20 simple object items with contiguous item selection | Material is limited to one simple object family; it lacks structured passages, relational item sets, detail-density bands, and enough fresh equivalents for repeated reconstruction. |
| WM-2 Mental Transform | Same 20-item pool with a small cyclic operation set | Transform operations are easy to learn as recipe patterns; there is limited source-material variety, interference variety, and operation-family depth. |
| IR-1 Go/No-Go Rule | 6 go cues, 6 no-go cues, 9-item default stream | Streams are short and built from a narrow cue vocabulary; repeated use risks pattern learning rather than inhibition under varied cue pace and no-go distribution. |
| IR-2 Exception Rule | 6 base rule items, 6 exception templates, 8-item default stream | Exception families and similarity structures are thin; longer sets and varied temptation sources are needed for realistic repeated practice. |
| DE-1 Pair Discrimination | 10 pair templates | Pair families are too few to sustain marked-guess and false-positive/false-negative work across many sessions. |
| DE-2 Seeded Audit | 3 seeded audit templates with short outputs | Audit scenarios will repeat quickly; output length, seeded error families, subtlety, and false-correction traps need broader local corpora. |
| CO-1 Rule Extraction | 3 rule families | Learners can memorize rule-family shapes; more example families, negative examples, ambiguous boundaries, and unseen test cases are needed. |
| CO-2 Structure Mapping | 3 mapping templates | Mapping domains and surface-lure types are too limited for repeated relation naming and surface-match rejection. |
| AI-1 Pressure Repeat | 3 source standards and 3 pressure sources | AI content mostly wraps existing source tasks; it needs a broader local set of source-task references and pressure variants while preserving the original standard. |
| AI-2 Disruption Recovery | 3 source standards and 3 disruption events | Disruption content is structurally correct but shallow; more interruption types, timing recipes, restart-delay bands, and source-task pairings are needed. |
| TI-1 Composite Task | 5 component templates and 3 task frames | Composite work has branch-separated evidence, but lacks enough composite task material, component pairings, transfer distances, and task families for repeated sessions. |
| TI-2 Global Review Task | 7 global-review component templates and 3 task frames | Review structure is covered, but broader task material is needed so global review evaluates the whole practitioner rather than familiarity with fixed frames. |
| Transfer content | One static transfer definition per source branch, with generated materials built from catalog text | Transfer preserves standards and rejects novelty-only content, but actual changed-context material is shallow and should be backed by branch-specific transfer banks or recipes. |
| Local content banks | Initial packaged bank entries exist for WM-1, DE-1, and CO-1 only | MVP can prove bank selection for the clearest fresh-equivalent retest cases, but broader app-packaged bank entries are still needed before repeated offline practice can rely primarily on bank depth. |

## Concrete Non-UI Content Gaps

Before offline MVP practice, add a packaged local content corpus or richer
deterministic recipes for each drill family. The corpus should be app-owned and
local, not network fetched or AI generated.

- FH needs target banks grouped by target type, subtlety, duration band, and
  allowed distractor salience; distractors should vary without becoming target
  substitutions.
- FS needs larger target-set banks, valid/invalid cue families, cue-density
  schedules, rule-contrast variants, and longer deterministic schedules for
  stabilization and maintenance.
- WM needs multiple material families: simple item sets, structured passages,
  relational or spatial sets, detail-density bands, delay bands, operation
  families, reversals, interference variants, and expected-answer keys.
- IR needs larger go/no-go streams, exception-rule families, similarity bands,
  pace bands, no-go distributions, premature-response traps, and post-error
  cascade scenarios.
- DE needs broader discrimination pair banks by relevant difference,
  irrelevant difference, similarity, time limit, and answer key; seeded audits
  need longer outputs, more domains, seeded-error taxonomies, and false
  correction traps.
- CO needs more rule-extraction families, negative examples, ambiguous
  boundary cases, unseen tests, structure-mapping domains, relation sets,
  surface lures, and unsupported-inference traps.
- AI needs pressure and disruption wrappers that can attach to a larger set of
  source branch tasks, with explicit source-standard visibility, clean evidence
  preservation, pressure metadata, interruption timing, and restart rules.
- TI needs composite task banks with separate component evidence requirements,
  task lengths, transfer distances, branch-specific scoring channels, delayed
  reconstruction requirements, audit requirements, and global-review variants.
- Transfer needs branch-specific changed-context material that preserves the
  source standard visibly, not only catalog-level transfer descriptions.
- Content-bank packaging beyond the initial WM-1, DE-1, and CO-1 MVP entries
  needs versioned entries, equivalence classes, load metadata, content
  summaries, material payload facts, and a process for validating every
  packaged entry through the existing generated content validators before
  release.
- Content-depth requirements should be made explicit before adding count-based
  tests. The current code proves correctness of shape and invariants, not
  sufficiency of corpus size for weeks of offline practice.

## Test Impact

Executable tests now cover the initial MVP packaged bank for WM-1, DE-1, and
CO-1. They prove local selection of multiple fresh-equivalent variants, concrete
material differences, validator-clean audit evidence, stable version metadata,
and no generated-content advancement authority. Broader corpus-size
requirements are still requirements, not hard count-based invariants.
