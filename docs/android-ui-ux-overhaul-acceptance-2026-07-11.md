# Android UI/UX Overhaul Acceptance - 2026-07-11

## Outcome

The full Android experience now follows the audit and implementation blueprint in
`docs/android-ui-ux-overhaul-plan-2026-07-10.md`.

The app is organized around one visible training decision, one current action, observable evidence, an honest result, and a direct next step. Train, Map, Record, and Review remain available outside modal training workflows. Local Data is a utility, not a training destination.

## Philosophy Check

- Progression is represented as branch and level state, not activity streaks or mood.
- The visible criteria match evidence Runtime can collect.
- All 40 branch-level standards have executable Core evaluation and can grant only the transitions Core approves.
- Practice, formal tests, stabilization, maintenance, restoration, transfer, and global review retain their actual session type through completion and persistence.
- Stopped, failed, incomplete, and blocked work is saved without becoming clean practice.
- Result and Record surfaces show the observed reason for a miss and keep the practitioner at the programmed level.
- Map distinguishes training, passed-once, stabilization, ownership, maintenance, decay, and blocked states without collapsing them into a generic completion mark.

## Release Evidence

- `dotnet test MentalGymnastics.sln --no-restore --nologo`: 855 passed, 0 failed, 0 skipped.
  - Core: 241
  - Runtime: 152
  - Content: 173
  - Persistence: 119
  - App: 170
- Release Android build and install: 0 warnings, 0 errors.
- Signed Release APK SHA-256: `F5EF17BB7583F55A0D1B93088B488C3ECC4BDD09E310BBB133B5AACBD684DA19`.
- `git diff --check`: no whitespace errors; only repository line-ending notices.
- Clean release launch produced a branded splash and interactive Today surface without a new app ANR.
- Release FH flow verified on device: Today -> setup -> live hold -> Wandered haptic -> Back on target -> review. Setup explicitly says eyes open, keeps the target visible, and places the full Start action above system navigation without scrolling.
- The live FH lower half is one stable touch pad. It toggles between `WANDERED` and `BACK ON TARGET`, leaves the target visible, and emits `TEXTURE_TICK` feedback on the wander mark.
- All 16 drill response states were captured and mechanically audited for semantic controls, viewport bounds, enabled actions, and valid scroll access. Focus return, honest omission, cue filtering, no-go, committed comparison, audit, rule, mapping, pressure recovery, and composite evidence interactions were exercised directly.
- The complete-program simulation reaches all 40 owned levels and a passing global review in 727 calendar days, with 624 active days, 1,230 drill blocks, and a 15.4-minute active-day average.
- Offline generation produces a distinct full material signature for every drill block on that path; a new content ID around repeated material is rejected by the test.
- Full-trim persistence was exercised on device after removing every generic `JsonArray.Add<T>` path.
- Runtime-restorable lifecycle phases preserve generated material, timer state, cue schedules, and open response state across process clocks. An honesty-sensitive active phase that cannot be reconstructed is saved as the day's stopped attempt.
- Responsive checks covered 360dp phone, standard phone, 1.3x and 1.5x text, landscape, and 800dp tablet.
- Narrow-phone Map long names wrap without colliding with state markers.
- Shared interactive controls meet the 48dp target floor; composite evidence and map nodes expose one accessibility summary instead of decorative child stops.
- Release visual checks covered Train, Preflight, Live hold/return, Result, Map, Branch Detail, Record, Record Detail, Review, due global review, Local Data, backup, restore confirmation, process interruption, and safe lifecycle resume.

## Honest Boundary

Every documented level is executable, but execution does not imply advancement. Generated material supplies expected answers and audit facts, Runtime records objective evidence, and Core applies the exact standard, prerequisites, dependency caps, stabilization, ownership, maintenance, restoration, transfer, and balance rules. Missing or blocked evidence produces no advancement.

The daily experience permits one counted prescription. Setup can be cancelled without consuming it; terminal work consumes it. Interrupted work resumes only when Runtime can preserve the drill's honesty constraints. Otherwise the same attempt is persisted as stopped and no second attempt is offered that day.

## Acceptance Result

The overhaul passes the product contract:

1. The current programmed action is obvious.
2. The active demand and material are visible.
3. The live control matches the evidence being recorded.
4. The result states what was observed and whether it counted.
5. The next programmed action is direct.

No known UI/UX release blocker remains in the audited Android surface.
