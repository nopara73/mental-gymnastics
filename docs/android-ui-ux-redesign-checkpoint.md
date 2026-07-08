# Android UI/UX Redesign Checkpoint

**Goal:** make the Android app feel like a serious training instrument, not a dashboard, report, debug viewer, or generic Android form.

## Current Direction

- Launch on one prescribed training command: resume, restore, maintain, recover, stabilize, test, practice, review, or show blocker.
- Keep Android as a thin presenter over `MentalGymnastics.App` presentation read models and runtime command state.
- Hide raw IDs, hashes, local paths, generated instance identity, runtime handoff language, and layer-boundary explanations at first level.
- Use visual state objects before text: command object, branch rails, hard blockers, fractured decay, service markers, evidence ticks, and result verdicts.
- Keep `Today -> Preflight -> Live -> Result -> Today` as the primary path; keep Map, Log, Review as inspection surfaces; keep Local Data as a utility opened from the header.

## Completed Milestones

- Required foundation, architecture boundary, Android UI, and generated/local persistence requirement docs have been read for this pass.
- Existing App presentation read models are present in `src/MentalGymnastics.App/TrainingPresentationReadModels.cs`.
- Existing Android shell already uses primary navigation `Today`, `Map`, `Log`, `Review`, with Local Data behind a header utility button.
- Android shell now presents the primary path as `Today -> Preflight -> Live -> Result -> Today`, with focused session screens hiding secondary navigation.
- Today uses a single prescribed work command with compact branch and readiness markers.
- Result now records an explicit verdict, evidence consequence, progress consequence, and next programmed action instead of leaving the user in an intermediate saving state.
- Evidence now reads as an inspection ledger with latest artifact and failure evidence, not dashboard totals.
- Review now shows blocked input, branch states, and programmed response directly rather than aggregate progress counters.
- Local Data is compressed into utility actions and remains outside the primary training path.
- Fresh device screenshots are available under `docs/screenshots/main-userflow/device-current/`.

## Remaining UI Failures

- Today is actionable, but the exact drill name appears first in Preflight rather than on the initial command.
- Preflight is now clearer and fits, but it is still text-heavy because the standard, constraint, and evidence requirement must remain explicit.
- The first clean Target Hold live session remains labeled `Prep` while the runtime timer runs; the UI is rendering runtime state, so this should be reviewed in `MentalGymnastics.Runtime` or `MentalGymnastics.App` before Android renames it.
- Clean first-run state has no due maintenance or decay item; the maintenance/decay screenshot is therefore represented by the Review state where those signals would surface.

## Known Tradeoffs

- The current Android UI is built in C# native Android views, not Compose; improvements should stay scoped to existing view helpers unless a broader rewrite becomes necessary.
- Some first-level labels remain necessary because standards, constraints, evidence, failure, and local restore warnings must stay explicit.
- Presentation read models may summarize truthful display inputs, but Android must not infer progression, readiness, maintenance, decay, transfer, runtime timing, or evidence classification.

## Validation Status

- `dotnet build .\MentalGymnastics.sln`: passed with 0 warnings and 0 errors.
- `dotnet test .\MentalGymnastics.sln --no-build`: passed 625 tests.
- Android emulator validation used `MentalGymnastics_API35` with a clean uninstall and non-fast-deploy debug install because clearing app data after fast deployment removes Xamarin override assemblies.
- Required screenshot set refreshed:
  - `01-first-opened-screen.png`
  - `02-prescribed-work.png`
  - `03-session-preflight.png`
  - `04-live-prep.png`
  - `05-live-work.png`
  - `06-result-abandoned.png`
  - `07-return-next-action.png`
  - `08-branch-map.png`
  - `09-evidence.png`
  - `09-maintenance-decay.png`
  - `10-global-review.png`
  - `11-local-backup-restore.png`
