# Android Practice Ergonomics Correction - 2026-07-12

## Why the Earlier Pass Missed the Problem

The earlier acceptance treated the live screen as an interface to inspect: every control had a rationale, every fact had a label, and every action was reachable. It did not treat the screen as part of the cognitive load while a person was actually trying to hold one target in mind.

That distinction matters. A timer, evidence counter, return button, stop action, and instruction can each look defensible in isolation while their combination directly contaminates a sustained-attention task. Reachability is not attentional ergonomics.

The same inspection bias also treated generated descriptor text as if it were the stimulus. A phrase such as `large green left square` may be structurally convenient data, but it is not a square. It adds an untested position term, reads as though “left square” were a category, and invites the practitioner to retain a phrase instead of attending to the visible object. That changes the capacity being trained while leaving the implementation looking superficially complete.

The durable rule is:

> Setup may explain. Review may measure. Live work may show only the stimulus and the action required at that instant.

## Corrections Made

### Direct Target Hold

- FH-1 and FH-2 active work now use a dedicated full-screen practice surface instead of the shared scrolling live-session layout.
- The surface contains only the target. FH-2 may additionally show the transient programmed distractor itself while it is active.
- The timer, instruction, target caption, card background, counters, response-pad label, top bar, navigation, pause, stop, and target-change controls are absent during the hold.
- Status and navigation bars are hidden during live cognitive work and restored for setup, review, errors, lifecycle pause, completion, and destruction. The screen is kept awake only while that work is active.
- The whole practice surface is one wander action. A deliberate tap records one `MarkDrift`; rapid taps are latched until the command completes. There is no visible acknowledgment to pull the eyes away from the target.
- The accessibility tree exposes one full-screen action: the target description plus “Double tap when attention wanders.” The decorative target and distractor are not separate focus stops.
- Target shapes are drawn inside measured horizontal and vertical bounds instead of being clipped by their container.
- Focus Hold targets now use only the tested visual attributes: size, color, and shape. Untested left/center/right tokens were removed from generation, rendering, setup copy, and protocol instructions.
- The visible descriptor under the target was removed. Setup and live work show the shape itself; a concise natural descriptor remains available to accessibility services.

### Wander Protocol

- A wander is now one standalone event. The practitioner taps when they notice it and immediately resumes the target.
- Current FH standards, generated content, Runtime handoff, App read models, and Android UI no longer require or score a second `BACK ON TARGET` action, return latency, late-return count, open-drift count, or return countdown.
- Consecutive wander taps are valid. Pause/restore does not create a hidden return obligation.
- Runtime retains the legacy return command only for old snapshot/direct-client compatibility; the current App neither presents nor sends it.
- Target substitution remains decisive evidence, but its report belongs in review rather than beside the held target.

### Shared Live-Screen Attention Budget

- Numeric timer rings are hidden during encode, active work, delay, cue response, reconstruction, and audit. A timer remains visible only when time itself is the immediate rest-state information.
- Generic phase instructions are hidden while the practitioner is performing; they remain available in setup, rest, recovery, and review.
- Cue metadata and response-window countdowns are hidden. The cue or stimulus itself remains visible when it is the programmed load.
- Pause and destructive stop controls are removed from live cognitive phases. System Back retains the guarded exit path.
- FH-2 still renders its controlled distractor, so visual cleanup does not remove the programmed constraint.
- FS cue work no longer repeats target-set material beside the current target, cue, and fixed response choices.
- IR cue work no longer shows rule cards or other material during the stimulus stream.
- WM-2 reconstruction no longer re-displays the transform rule.
- The existing neutral cue/no-go presentation remains: appearance and enabled state do not reveal the correct response.
- TI-1/TI-2 no longer expose a multi-component live dashboard. Content packages one Runtime-owned component phase at a time, App exposes only that component, and Android renders it directly. Component durations still sum exactly to the programmed task duration.
- Transfer contracts and source standards are setup material; they are withheld from live execution so they cannot push the actual task below the fold.
- Audit material remains hidden until Runtime records `AuditStarted`, so reading cannot begin before the evidence clock.

### Visual Stimulus Category Integrity

- A visible object is now represented as structured visual data and drawn as that object. The deterministic serialization used between layers is never displayed to the practitioner.
- FH target identity contains only the tested size, color, and shape. The object is centered; no `left`, `center`, or `right` term appears unless a future standard explicitly loads and tests spatial position.
- FS target choices and cues are rendered objects, so the practitioner switches between visual targets rather than memorizing their names.
- IR go/no-go cues and exception cues are rendered objects. Setup can state the response rule, but it does not require the practitioner to type, decode, or memorize a machine description of an exception.
- DE pairs show both visual objects and identify the one feature to compare. They do not replace perceptual discrimination with two lines of descriptive prose.
- Corrections and accessibility descriptions may use concise natural language, but live visual tasks do not place a caption beside the stimulus. Words remain visible task material only in drills that actually test words or semantic language.
- Untested features are not smuggled into copy. A feature such as position, direction, fill, orientation, or border is present only when it is part of target identity or a controlled load, lure, or relevant difference, and a declared visual feature changes the rendered object rather than merely its label.

This is a content-validity rule, not cosmetic polish. Substituting a descriptor for an object can produce clean-looking evidence about the wrong capacity. Preserving modality keeps the programmed load, observable errors, and advancement evidence tied to the skill the drill claims to test.

## First-Person Device Check

The correction was exercised on the physical Samsung SM-S948B, not accepted from a layout preview. The direct FH active hierarchy spans the full 1440 x 3120 display and contains one full-screen clickable node with no timer, instruction, button, top bar, navigation label, evidence text, or visible target caption. The target is centered and remains entirely inside its drawing bounds. The same full-screen node is the action regardless of where the practitioner taps.

The physical pass found three issues that source inspection alone had missed. Samsung initially reserved a black navigation region below the immersive canvas; the Window inset policy now allows the practice surface to draw through the hidden system-bar area. FS correctly rendered the cue and choices but still printed the encoded current target in a separate band; App now exposes a decoded current-target presentation and Android renders that object too. A white IR exception was technically rendered but nearly invisible on the pale canvas; white stimuli now receive a neutral contrast field without changing their encoded color identity.

FH setup, FH active work, FS cue response, IR rule declaration, IR cue response, and DE pair discrimination were then recaptured from the phone. The FS, IR, and DE accessibility hierarchies contain no serialized `visual-stimulus` values. The retained README screenshots come from these real device paths.

## Verification

- Full solution regression: 904 tests passed.
- Focused suites: Core 241, Runtime 155, Persistence 119, App 172, Content 217.
- Android Debug build: 0 warnings, 0 errors.
- Android Release build: 0 warnings, 0 errors; signed package installed on the physical SM-S948B after clearing the app's invalid practice state.
- Runtime coverage includes consecutive wander taps, pause/restore followed by another wander, and target-change reporting during post-hold review.
- Generated-content and App coverage assert that current FH material and commands contain no return-timing obligation.

## Remaining Product Work

The current implementation now enforces the task-only rule for direct focus holds, AI-wrapped focus holds, Runtime-sequenced TI components, and structured visual FS/IR/DE material. The next durable improvement is to replace remaining phase/material-kind heuristics with explicit App-facing presentation roles such as setup-only, current stimulus, programmed interference, current response, and review-only. That would make forbidden live material mechanically testable instead of relying on string-kind filters.
