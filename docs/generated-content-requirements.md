# MentalGymnastics Generated Content Requirements

**Status:** Requirements for the documented drill library
**Boundary:** [MentalGymnastics Generated Drill Content Boundary](generated-content-boundary.md)
**Domain authority:** [MentalGymnastics Core Library](core-library.md)
**Source program:** [MentalGymnastics: Complete Training Program](program/training-program.md)

This document defines what concrete local generated content the standard drill library requires. It is a content requirements document, not an implementation plan for progression, runtime execution, persistence, Android UI, or external services.

Generated content in Mental Gymnastics is local, offline, deterministic, and not AI-generated. It may come from embedded fixture banks, local static banks, or deterministic recipes driven by explicit inputs. It must not depend on network calls, AI APIs, accounts, sync, telemetry, notifications, backend services, Android UI, device state, or uncontrolled randomness.

## Scope

The generated content layer owns concrete drill material and generated instance identity:

- Target statements, target variants, and distractor prompts.
- Cue sequences, invalid cues, go/no-go streams, interruption cues, and response-window metadata.
- Memory reconstruction prompts, mental transform item sets, operation rules, and expected outputs.
- Discrimination pairs, seeded audit materials, expected findings, and non-error distractors.
- Rule extraction examples, unseen examples, negative examples, structure mappings, relation sets, and surface lures.
- Pressure variants, disruption variants, and composite task bundles.
- Content ids, content versions, equivalence classes, fixture or recipe ids, deterministic seeds, load variables, and critical constraints.

The generated content layer must not own:

- Advancement, gates, readiness, ownership, maintenance, decay, dependency caps, transfer eligibility, weekly planning, global review, recovery, deload, or failure routing.
- Static program catalogs, standards, thresholds, branch relationships, or rubrics already owned by Core.
- Live timers, phase scheduling, cue-response scoring, event logs, evidence capture, snapshots, or completion results already owned by Runtime.
- Local database writes, migrations, transactions, backup/restore, or integrity validation already owned by Persistence.
- Android screens, platform lifecycle behavior, notifications, accounts, sync, telemetry, analytics, backend services, network calls, or AI/API calls.

## Universal Instance Requirements

Every generated drill instance must carry:

- Stable generated instance id.
- `PromptContentIdentity`: content id, branch, level, drill, content kind, and equivalence class.
- Content version or generator recipe version.
- Fixture bank id or deterministic recipe id when applicable.
- Deterministic seed or selection input when the material is assembled rather than selected by fixed id.
- Branch, level, drill, and intended session role.
- Load variables administered by the content.
- Critical constraints and honesty constraints preserved by the content.
- Freshness policy and previously used content ids considered by selection.
- Concrete payload needed by Runtime.
- Expected observable evidence shape for later Core evaluation and Persistence records.

If fresh equivalent content is required, the content id must change and the equivalence class must remain stable. Freshness may change concrete items, cue order, target material, example set, pressure source, disruption timing, domain, or task format only when the branch, level, drill, load variables, demand, evidence shape, and honesty constraints remain equivalent.

Novelty alone is not transfer and must not be represented as advancement evidence.

## Determinism

Generated content must be reproducible from explicit local inputs:

- Fixture selection must be stable, such as sorted by content id or selected by an explicit seed.
- Recipe output must be reproducible from recipe id, recipe version, branch, level, drill, load variables, equivalence class, and seed.
- System time must not be used inside generation. Callers may supply dates or ids where persistence needs them.
- Network state, AI provider responses, account state, telemetry, analytics, and device state must not affect generated output.

## Stimulus Modality and Presentation Semantics

Generated material must preserve the modality that the drill actually tests. A visual object is not interchangeable with prose that names the object: showing `large green square` tests reading and verbal retention, while showing the corresponding shape tests perception, switching, inhibition, or discrimination. Descriptor prose may be the visible stimulus only when words, labels, or semantic content are themselves the documented tested material.

Visual material for FS, IR, and DE therefore uses a structured visual-stimulus specification rather than display-ready descriptor strings. The specification carries only the features needed by the generated instance, such as shape, color, fill, size, direction, mark, position, orientation, or border. A feature may enter the material only when it is either part of the tested identity or a controlled similarity, interference, or discrimination variable. Untested features must remain neutral; they must not be named as if the practitioner must remember them.

The serialized visual-stimulus codec is a deterministic handoff format, not user-facing copy. App-facing presentation must decode it and Android must render the described object. The raw codec, enum names, canonical ids, or a prose paraphrase of the object must never be substituted for the stimulus on setup, live, response, correction, or review surfaces. Natural-language descriptions remain appropriate for accessibility metadata, and concise setup language may name the rule or relevant feature without duplicating the live stimulus as a reading task.

Position has the same rule as every other visual feature. Terms such as `left`, `right`, `top`, or `bottom` are valid only when spatial position is an executable load or tested distinction and the object is actually rendered in that position. A position word embedded in an otherwise visual descriptor is neither a spatial stimulus nor useful metadata.

## Branch and Drill Requirements

| Branch | Drill | Required generated content | Must preserve | Freshness requirement |
| --- | --- | --- | --- | --- |
| FH | FH-1 Target Hold | Target statement, target type, target subtlety tag, hold duration metadata, and expected standalone wander-marking evidence shape. | Target is stated before the set, target cannot be substituted, and every noticed wander is marked once before attention resumes. | Fresh variants change target material or target type while preserving duration, subtlety, and wander-marking standard. |
| FH | FH-2 Distractor Hold | FH target material plus distractor prompts with ids, salience tags, timing offsets or positions, frequency, and expected no-response behavior. | Distractors are irrelevant unless the drill explicitly says otherwise; responding to distractors remains detectable. | Fresh variants change distractor material, order, or salience within the same load. |
| FS | FS-1 Cue Switch | Target set, valid cue sequence, cue ids, cue timing or position metadata, expected active target after each cue, and response windows. | Switching occurs only on valid cues; anticipatory switching and missed cues remain observable. | Known cue sequences may be reusable only when Core freshness policy allows; fresh variants change target pair, cue order, or timing while preserving density and target count. |
| FS | FS-2 Invalid Cue Filter | Target set, valid cues, invalid cues, invalid cue ratio, rule contrast metadata, expected switch/no-switch state, and response windows. | Invalid cues must not trigger a switch; treating all cues as valid remains observable. | Fresh variants change cue values or order while preserving valid/invalid ratio, rule contrast, and cue density. |
| WM | WM-1 Delayed Reconstruction | Encode items, item order, detail density, encode instruction, delay length, reconstruction instruction, and expected reconstruction data. | No rereading after the encode window; no invented items; omissions and inventions remain auditable. | Fresh equivalent content is required for retests and stabilization unless Core explicitly allows reuse. |
| WM | WM-2 Mental Transform | Source items, transform rule, operation steps, reversal or interference metadata, final expected output, and rule-explanation prompt. | Hidden intermediate notes remain prohibited unless the drill explicitly allows them; final output and operation explanation stay auditable. | Fresh variants change item set or rule family while preserving operation-step count and demand. |
| IR | IR-1 Go/No-Go Rule | Go/no-go cue stream, cue ids, cue pace, no-go frequency, expected response or no-response per cue, and response deadlines. | Premature responses fail the item; omissions, commissions, late responses, and post-error cascades remain observable. | Known timing or inhibition cues may be reusable only when Core freshness policy allows; fresh variants change stream order or cue symbols without changing pace and no-go frequency. |
| IR | IR-2 Exception Rule | Rule statement, named exceptions, cue or item stream, exception markers, expected action/classification, similarity metadata, and pace. | Rule and exceptions are stated before the set; rule changes, forgotten exceptions, and unmarked rule drift remain observable. | Fresh variants change exception set or item order while preserving exception count, pace, and similarity. |
| DE | DE-1 Pair Discrimination | Item pairs, relevant feature, similarity level, match/mismatch truth, expected uncertainty or guess handling, and false-positive/false-negative keys. | Guesses must be marked; unmarked guesses, overcorrections, and ignored relevant features remain auditable. | Fresh equivalent item pairs are required for retests and stabilization. |
| DE | DE-2 Seeded Audit | Locked original output, seeded error ids, error type, location, criticality, expected findings, non-error distractors, and audit instruction. | Original output cannot be edited during audit; missing critical errors and invented errors remain observable. | Fresh seeded audits are required unless explicitly testing known-error maintenance. If Core needs a dedicated seeded-audit content kind, add Core vocabulary first. |
| CO | CO-1 Rule Extraction | Positive examples, negative examples where applicable, unseen examples, expected classifications, rule-family id, ambiguity level, and rule-limit hints only when allowed. | Rule is stated before unseen examples; rewriting after feedback, vague rules, and overfitting remain auditable. | Fresh equivalent example sets are required for retests, stabilization, and transfer. |
| CO | CO-2 Structure Mapping | Source structure, target context, required relation set, relation names, irrelevant surface similarities, expected mapping, and expected limits. | Relations must be named; surface-only mapping and unsupported inference remain detectable. | Fresh variants change domains or relation instances while preserving relation count, distance, and required structure. |
| AI | AI-1 Pressure Repeat | Source branch content reference or payload, original branch standard identity, pressure source, time limit or observation metadata, and no-standard-lowering marker. | Original standard remains visible and cannot be lowered; pressure does not excuse errors or missing artifacts. | Fresh variants change pressure source while preserving source branch demand and standard. Source content reuse follows Core freshness policy for that source task. |
| AI | AI-2 Disruption Recovery | Source task payload, disruption event id, disruption timing, disruption type, restart rule, recovery window, and post-disruption evidence requirements. | Full restart remains prohibited unless specified; recovery timing and post-disruption cascade errors remain observable. | Fresh variants change disruption timing or disruption type while preserving source demand and recovery requirement. |
| TI | TI-1 Composite Task | Component content bundle for two or more branches, component ids, branch-specific evidence requirements, task prompt, branch scoring keys, transfer distance, and integration load. | Each component branch leaves separate evidence; strong components cannot hide weak components. | Fresh variants change task format, component pairing, or domain while preserving branch count and visible component evidence. |
| TI | TI-2 Global Review Task | Multi-branch task material, pressure or ambiguity variant, audit payload, delayed reconstruction payload, component branch evidence keys, expected review artifacts, and global task version. | Audit and delayed reconstruction are required; branch evidence stays separated; pressure rule remains intact. | Fresh global-review task material is required across review cycles. |

## Content Payload Families

### Target Hold Payloads

Target hold payloads must include the target statement and metadata needed to preserve target subtlety, duration, and standalone wander marking. They must not add return-timing material, make the target easier during a session, or replace drift evidence with a retrospective subjective attention report.

For the current visual Focus Hold targets, generated material names only the tested visual attributes: size, color, and shape. It must not synthesize left/center/right wording or position the target away from center unless a future executable standard and load variable explicitly test spatial position. Android renders the shape itself as the visible target; its textual descriptor is accessibility metadata, not a second on-screen memorization cue.

FH-2's current distractor renderer is textual. Its generated distractors therefore use honest words, numbers, or symbols. It must not label text as a color, shape, sound, or action stimulus, and it must not show action words such as `tap` that conflict with the whole-screen wander action. A future non-text distractor must carry an explicit presentation kind and receive a real renderer before it can enter the cue stream.

### Distractor Payloads

Distractor payloads must include distractor ids, salience, frequency, timing or position, and whether a response is prohibited or expected. FH distractors usually require no response; AI and TI disruptions may require recovery evidence rather than simple ignoring.

### Cue Sequence Payloads

Cue sequence payloads must include cue ids, cue kind, timing or position, expected response, invalid cue markers, response windows, and target state transitions. FS targets, valid cues, invalid cues, and expected active targets use structured visual-stimulus specifications so switching is performed between rendered objects. Invalid-cue lures must stay in the same visible modality as valid cues unless a documented rule intentionally tests a cross-modal distinction. Runtime owns cue presentation and response evaluation; generated content owns the sequence material.

### Memory and Transform Payloads

Memory payloads must include encode material, item order, delay, reconstruction instruction, and expected reconstruction. Transform payloads must also include the operation rule, step count, allowed or prohibited notes, final expected output, and explanation prompt.

### Rule Stream Payloads

Go/no-go and exception-rule payloads must include the pre-stated rule, cue or item stream, exception set, pace, expected action per item, and evidence keys for premature responses, no-go failures, exception failures, and post-error cascades. IR cues and exception identities use structured visual-stimulus specifications. Stable machine ids may associate an exception with its expected action, but the practitioner-facing declaration and cue stream must present the rendered exception object rather than require entry or memorization of an encoded descriptor.

### Discrimination and Audit Payloads

Discrimination payloads must include structured visual item pairs, the explicitly relevant feature, similarity level, match truth derived from that feature, and expected false-positive/false-negative scoring. Both objects must be rendered so the practitioner compares the visible feature itself; prose labels for the two objects are not equivalent discrimination material. Irrelevant pair features may vary only as controlled lures, and their values must not leak the correct answer. Seeded audit payloads must include locked source output, seeded error ids, criticality, expected findings, and non-error distractors.

### Rule and Mapping Payloads

Rule extraction payloads must include training examples, unseen test examples, expected classifications, and rule-family metadata. Structure mapping payloads must include source and target structures, required relations, surface lures, expected relation names, and mapping limits.

### Pressure and Disruption Payloads

Pressure payloads must reference the original branch standard and define the pressure source before the session. Disruption payloads must define timing, interruption type, restart allowance, recovery window, and post-disruption evidence.

### Composite Payloads

Composite payloads must carry separate component payloads, branch-specific evidence keys, branch scoring facts, and component boundaries. They must not collapse TI work into one vague output or allow a strong branch to hide a weak one.

## Relationship to Existing Layers

Generated content should consume Core prompt/content identifiers, freshness policies, drill ids, branch ids, level ids, load variables, and critical constraints. It should produce content instances that Runtime can execute and Persistence can store.

Runtime remains responsible for timers, phases, cue emission, response timing, command validation, scoring facts, evidence capture, snapshots, and completion results.

Persistence remains responsible for storing generated instance identity, payloads or payload references, runtime state, completion metadata, backup, migrations, and integrity validation.

Core remains responsible for standards, gates, readiness, ownership, maintenance, decay, dependency caps, transfer eligibility, recovery, deload, weekly planning, global review, and anti-self-deception rules.

## Future Implementation Tests

When executable generation is added, tests should prove:

- The same explicit inputs produce the same content.
- Fresh equivalent variants change content id and concrete material while preserving branch, level, drill, content kind, equivalence class, load variables, critical constraints, and evidence shape.
- Used content ids are excluded when fresh content is required.
- Known cue or inhibition content is reusable only when Core freshness policy permits.
- Structured visual material round-trips deterministically, rejects malformed or non-canonical encodings, and preserves the tested feature semantics used to derive expected answers.
- FS, IR, and DE presentation handoffs expose renderable visual stimuli without exposing codec strings or replacing objects with descriptor prose.
- Generated content cannot remove drift marking, permit target substitution, allow unallowed rereading, allow hidden notes, allow premature responses, allow unmarked guesses, lower source standards, remove branch-specific evidence, or replace evidence with novelty.
- Runtime and Persistence can consume generated instance facts without redefining generation, progression, storage, or live execution rules.
