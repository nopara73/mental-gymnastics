# Android first-user UX checkpoint - 2026-07-09

## Status

Latest cycle status: passed with fresh Android screenshots, strict verifier, contact sheet, and final average-user review.

This cycle addresses the first-user problem that Target Hold felt like an isolated chore. Today now explains the Focus Hold foundation, why noticing and returning comes first, what foundational branches come next, what later load dimensions increase, and that transfer is tested later rather than promised.

Fresh device capture succeeded on `emulator-5554` using the SDK ADB path. The seven workflow PNGs, contact sheet, and `capture-manifest.json` were regenerated with `--clear-app-data`; the strict verifier passed. The written average-user review below is based on those fresh screenshots.

## Boundary Check

- Core progression rules remain in `MentalGymnastics.Core`.
- App owns workflow orchestration and Android-facing read models.
- Runtime owns live session state, timers, phases, and commands.
- Content owns generated Target Hold material.
- Persistence remains offline/local storage only.
- Android renders App/Runtime presentation state and forwards user actions; it does not create rules, timers, generated content, persistence state, or progress.

## Latest Failure Found

The Today screen still felt like: "do this small useless task because the app says so." It told the user what Target Hold is, but not the point of doing it, not what repeated practice should improve, and not how this connects to later work.

The live target card also displayed target words as sentence fragments, for example `lantern.`. That is counterproductive for Target Hold because the punctuation can look like part of the target or make the user wonder whether to include the period in the mental target.

The target vocabulary also assumed too much English. `Lantern`, `anchor`, `quiet line`, `steady point`, and similar targets make the first exercise partly a vocabulary task for non-native English speakers. That violates the drill purpose: Target Hold should train attention to a simple target, not language comprehension.

After those fixes, the UI still had a readability problem: it explained the exercise with too many rule fragments and still exposed the internal runtime/domain term `drift` as the primary live action. That made the app feel like "do this useless bullshit for x times" instead of showing a plain skill loop.

After the user-intent wording fix, Today still underexplained the ladder. It said Target Hold led to distraction, switching, and harder memory work, but it did not name the Focus Hold foundation, did not show the unlocked foundational branches, did not say how later load increases, and did not state that transfer is tested rather than promised. An average new user could still read the first task as an isolated chore.

During the final device pass, manually installing the normal Debug APK crashed after `--clear-app-data` because the build had used Android fast deployment and app-data clearing removed the fast-deployed assemblies. After installing an embedded-assembly Debug APK, capture succeeded. The first capture then exposed a verifier false positive: required copy says `stop automatic responses`, while the forbidden `Response` label was still substring-matched. That was tightened to standalone-token matching.

## Latest Fix Made

- Added App read-model fields for `Purpose`, `PracticeGain`, and `WhereItGoes` on `TrainingExercisePresentation`.
- Added concise first-user framing for Target Hold, later superseded by the scannable path lines below.
- Replaced the old Today trust strip (`Before the timer starts` / `Rules appear before the timer.`) with a compact `Why this matters` / `Where this goes` card shown before `Start Target Hold`.
- Updated the strict screenshot verifier so future Today captures must include the purpose/path explanation.
- Updated the screenshot workflow README so stale artifacts are not described as current evidence.
- Removed sentence periods from generated Focus Hold target words and phrases.
- Added App and Android display cleanup so older generated/runtime target material such as `Hold target word: lantern.` displays as `lantern`.
- Replaced uncommon or abstract Focus Hold targets with short visual targets: `red dot`, `blue dot`, `green dot`, `black line`, `blue square`, and `red circle`.
- Added a Content-layer test that cycles generated Target Hold variants and rejects the old uncommon vocabulary.
- Rewrote the first-user Target Hold surface around one user-facing loop: hold the target, notice when the mind wanders, tap `I wandered`, and return.
- Shortened before-start criteria to `Counts`, `Try again`, and `Honesty` sections instead of dense success/failure paragraphs.
- Renamed the pre-start primary action from `Start exercise` to `Start 3-minute hold`.
- Renamed the live abandon action from `Stop`/`Stop exercise` to `Stop early`.
- Renamed first-user live counters from `Drifts`/`Recorded` to `Wanders`/`Saved`.
- Updated the result screen labels from `Recorded`/`Today` to `Saved`/`Your place`, with human next-step copy.
- Tightened the screenshot verifier so stale captures fail if they show the old wordy/jargon terms (`Mark drift`, `Success rules`, `What gets recorded`, `marked drifts`, or `Your place in Today is unchanged`).
- Replaced the narrow Today path copy with scannable lines:
  - `Start the Focus Hold foundation: notice when attention moved and bring it back on purpose.`
  - `Repeats train: notice wandering sooner and return cleaner.`
  - `Next: Focus Shift (switch focus), Working Memory (hold information), Inhibition (stop automatic responses), Discrimination (spot differences).`
  - `Later: longer time, more items, distraction, delay, rule conflict, ambiguity, combined tasks.`
  - `Final direction: transfer tests outside this drill under interruption and pressure. No promise this makes you smarter.`
- Updated App tests to require the Focus Hold foundation, unlocked foundational branches, later load dimensions, tested transfer outside the drill, and the no-smarter-promise statement.
- Updated the strict first-user screenshot verifier and workflow README so fresh Today and return-to-Today captures must include the fuller program-path framing.
- Added simple explanations beside the more formal branch names so `Inhibition` and `Discrimination` do not appear as unexplained first-level jargon.
- Updated the first-user capture script to prefer the Android SDK `platform-tools\adb.exe` by default, accept an explicit `--adb-path`, and record the actual ADB path in `capture-manifest.json`.
- Updated the strict artifact verifier to require `adbPath` in fresh manifests.
- Documented the embedded-assembly Debug APK install path required for first-user capture with `--clear-app-data`.
- Tightened single-word forbidden verifier labels including `Response`, `Submit`, `Guess`, and `Correct` to standalone-token matching so the required phrase `stop automatic responses` is accepted.

## Validation Evidence

- `dotnet test .\tests\MentalGymnastics.App.Tests\MentalGymnastics.App.Tests.csproj --no-restore --filter TrainingPresentationReadModelTests` passed: 21 tests.
- `dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj --no-restore` passed with 0 warnings and 0 errors.
- `dotnet build .\MentalGymnastics.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test .\MentalGymnastics.sln --no-build` passed: 629 tests.
- `dotnet test .\tests\MentalGymnastics.Content.Tests\MentalGymnastics.Content.Tests.csproj --no-restore --filter FocusHoldGeneratedContentTests` passed: 4 tests after the target punctuation fix.
- `dotnet test .\tests\MentalGymnastics.App.Tests\MentalGymnastics.App.Tests.csproj --no-restore --filter TrainingPresentationReadModelTests` passed: 21 tests after the target punctuation fix.
- `dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj --no-restore` passed with 0 warnings and 0 errors after the target punctuation fix.
- `dotnet test .\tests\MentalGymnastics.Content.Tests\MentalGymnastics.Content.Tests.csproj --no-restore --filter FocusHoldGeneratedContentTests` passed: 5 tests after the vocabulary simplification fix.
- `dotnet test .\tests\MentalGymnastics.App.Tests\MentalGymnastics.App.Tests.csproj --no-restore --filter TrainingPresentationReadModelTests` passed: 21 tests after the vocabulary simplification fix.
- `dotnet build-server shutdown` was run afterward; the compiler server shut down successfully.
- `dotnet test .\tests\MentalGymnastics.App.Tests\MentalGymnastics.App.Tests.csproj --no-restore --filter "FullyQualifiedName~TrainingPresentationReadModelTests|FullyQualifiedName~PreUiTrainingWorkflowActiveSnapshotTests"` passed: 32 tests after the user-intent wording fix.
- `dotnet test .\tests\MentalGymnastics.Content.Tests\MentalGymnastics.Content.Tests.csproj --no-restore --filter FocusHoldGeneratedContentTests` passed: 5 tests after the user-intent wording fix.
- `dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj --no-restore` passed with 0 warnings and 0 errors after the user-intent wording fix.
- `dotnet build .\MentalGymnastics.sln --no-restore` passed with 0 warnings and 0 errors after the user-intent wording fix.
- `dotnet test .\MentalGymnastics.sln --no-build` passed: 630 tests after the user-intent wording fix.
- `git diff --check` passed; Git reported only line-ending normalization warnings.
- `dotnet build-server shutdown` was run afterward; no `dotnet.exe` processes remained after shutdown.
- `python -m py_compile .\docs\screenshots\firsttimeuser-workflow\capture_firsttimeuser_device_screenshots.py .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` passed.
- `adb devices` found no ready attached device. `adb kill-server` was run afterward; no `adb.exe`, `emulator.exe`, `qemu-system-x86_64.exe`, or `dotnet.exe` processes remained.
- `dotnet test .\tests\MentalGymnastics.App.Tests\MentalGymnastics.App.Tests.csproj --no-restore --filter "FullyQualifiedName~TrainingPresentationReadModelTests|FullyQualifiedName~PreUiTrainingWorkflowActiveSnapshotTests"` passed: 32 tests after the program-path copy fix.
- `dotnet test .\tests\MentalGymnastics.Content.Tests\MentalGymnastics.Content.Tests.csproj --no-restore --filter FocusHoldGeneratedContentTests` passed: 5 tests after the program-path copy fix.
- `dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj --no-restore` passed with 0 warnings and 0 errors after the program-path copy fix.
- `python -m py_compile .\docs\screenshots\firsttimeuser-workflow\capture_firsttimeuser_device_screenshots.py .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` passed after the verifier expectation update.
- `dotnet build .\MentalGymnastics.sln --no-restore` passed with 0 warnings and 0 errors after the program-path copy fix.
- `dotnet test .\MentalGymnastics.sln --no-build` passed: 630 tests after the program-path copy fix.
- `dotnet build-server shutdown` was run afterward; MSBuild and VB/C# compiler servers shut down successfully.
- `adb devices` found no ready attached device. `adb kill-server` was run afterward; no `dotnet.exe`, `msbuild.exe`, `vbcscompiler.exe`, `testhost.exe`, `vstest.console.exe`, `adb.exe`, `emulator.exe`, or `qemu-system-x86_64.exe` processes remained.
- `python .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` was run and failed as expected because fresh capture was not possible and obsolete workflow screenshots are still present: `02-prescribed-work.png`, `03-session-preflight.png`, `04-live-prep.png`, `05-live-work.png`, and `06-result-abandoned.png`.
- `dotnet test .\tests\MentalGymnastics.App.Tests\MentalGymnastics.App.Tests.csproj --no-restore --filter "FullyQualifiedName~TrainingPresentationReadModelTests|FullyQualifiedName~PreUiTrainingWorkflowActiveSnapshotTests"` passed: 32 tests after converting the path copy into scannable `Next` / `Later` / `Transfer tests` lines.
- `dotnet test .\tests\MentalGymnastics.Content.Tests\MentalGymnastics.Content.Tests.csproj --no-restore --filter FocusHoldGeneratedContentTests` passed: 5 tests after the scannable path copy update.
- `python -m py_compile .\docs\screenshots\firsttimeuser-workflow\capture_firsttimeuser_device_screenshots.py .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` passed after the scannable verifier update.
- `dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj --no-restore` passed with 0 warnings and 0 errors after the scannable path copy update.
- `dotnet build .\MentalGymnastics.sln --no-restore` passed with 0 warnings and 0 errors after the scannable path copy update.
- `dotnet test .\MentalGymnastics.sln --no-build` passed: 630 tests after the scannable path copy update.
- `dotnet build-server shutdown` was run afterward; MSBuild and VB/C# compiler servers shut down successfully.
- `adb devices` again found no ready attached device, so screenshot capture was not attempted and no emulator was started. `adb kill-server` was run afterward.
- `python .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` was rerun after the verifier update and failed as expected because obsolete workflow screenshots are still present: `02-prescribed-work.png`, `03-session-preflight.png`, `04-live-prep.png`, `05-live-work.png`, and `06-result-abandoned.png`.
- After that validation pass, no `dotnet.exe`, `msbuild.exe`, `vbcscompiler.exe`, `testhost.exe`, `vstest.console.exe`, `adb.exe`, `emulator.exe`, or `qemu-system-x86_64.exe` processes remained.
- `adb devices` again found no ready attached device. `MentalGymnastics_API35` exists as a configured emulator, but CPU load was measured at 97% and unrelated .NET/testhost work was active, so the emulator was not started.
- `dotnet test .\tests\MentalGymnastics.App.Tests\MentalGymnastics.App.Tests.csproj --no-restore --filter "FullyQualifiedName~TrainingPresentationReadModelTests|FullyQualifiedName~PreUiTrainingWorkflowActiveSnapshotTests"` passed: 32 tests after adding plain-language explanations beside formal branch names.
- `dotnet test .\tests\MentalGymnastics.Content.Tests\MentalGymnastics.Content.Tests.csproj --no-restore --filter FocusHoldGeneratedContentTests` passed: 5 tests after the branch-name explanation update.
- `python -m py_compile .\docs\screenshots\firsttimeuser-workflow\capture_firsttimeuser_device_screenshots.py .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` passed after the verifier expectation update.
- `dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj --no-restore` passed with 0 warnings and 0 errors after the branch-name explanation update.
- `dotnet build .\MentalGymnastics.sln --no-restore` passed with 0 warnings and 0 errors after the branch-name explanation update.
- `dotnet test .\MentalGymnastics.sln --no-build` passed: 630 tests after the branch-name explanation update.
- `dotnet build-server shutdown` was run afterward; MSBuild and VB/C# compiler servers shut down successfully.
- `python .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` still failed because obsolete workflow screenshots are present: `02-prescribed-work.png`, `03-session-preflight.png`, `04-live-prep.png`, `05-live-work.png`, and `06-result-abandoned.png`.
- `adb kill-server` was run afterward.
- A final process check still showed unrelated `LongevityWorldCup` `dotnet.exe` / `testhost.exe` / compiler processes from another worktree; they were left untouched. No `adb.exe`, `emulator.exe`, or `qemu-system-x86_64.exe` process remained from this pass.
- `adb devices` again found no ready attached device. System load then looked acceptable (`CPU 35%`, about `17 GB` free memory), so `MentalGymnastics_API35` was started with `-no-snapshot-save`.
- The emulator process started, but ADB reported `emulator-5554 offline` for the full three-minute wait. Restarting ADB did not recover it.
- While stuck offline, the emulator pushed CPU load to `100%`, so the emulator and QEMU processes were stopped and `adb kill-server` was run.
- `python .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` was rerun after the failed emulator attempt and still failed because obsolete workflow screenshots are present: `02-prescribed-work.png`, `03-session-preflight.png`, `04-live-prep.png`, `05-live-work.png`, and `06-result-abandoned.png`.
- A final process check showed unrelated `LongevityWorldCup` `dotnet.exe` / compiler processes from other worktrees; they were left untouched. No `adb.exe`, `emulator.exe`, or `qemu-system-x86_64.exe` process remained from this pass.
- `python -m py_compile .\docs\screenshots\firsttimeuser-workflow\capture_firsttimeuser_device_screenshots.py .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` passed after the capture-script ADB path update.
- With CPU around 36% and about 17 GB free memory, `MentalGymnastics_API35` was started again using the SDK ADB path explicitly plus `-no-snapshot-save -no-boot-anim`.
- SDK ADB still reported `emulator-5554 offline` for the full two-minute wait. `adb reconnect offline` did not recover it.
- The emulator and QEMU processes were stopped, SDK ADB was killed, and no `adb.exe`, `emulator.exe`, or `qemu-system-x86_64.exe` process remained.
- `python .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` still failed because obsolete workflow screenshots are present: `02-prescribed-work.png`, `03-session-preflight.png`, `04-live-prep.png`, `05-live-work.png`, and `06-result-abandoned.png`.
- `dotnet test .\tests\MentalGymnastics.App.Tests\MentalGymnastics.App.Tests.csproj --no-restore --filter "FullyQualifiedName~TrainingPresentationReadModelTests|FullyQualifiedName~PreUiTrainingWorkflowActiveSnapshotTests"` passed: 32 tests after the capture-script ADB path update.
- `dotnet test .\tests\MentalGymnastics.Content.Tests\MentalGymnastics.Content.Tests.csproj --no-restore --filter FocusHoldGeneratedContentTests` passed: 5 tests after the capture-script ADB path update.
- `dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj --no-restore` passed with 0 warnings and 0 errors after the capture-script ADB path update.
- `dotnet build .\MentalGymnastics.sln --no-restore` passed with 0 warnings and 0 errors after the capture-script ADB path update.
- `dotnet test .\MentalGymnastics.sln --no-build` passed: 630 tests after the capture-script ADB path update.
- `dotnet build-server shutdown` was run afterward; MSBuild and VB/C# compiler servers shut down successfully, and no matching build/test/ADB/emulator process remained from this repo.
- `dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj --no-restore -p:EmbedAssembliesIntoApk=true` passed and produced an installable Debug APK that survives `pm clear`.
- The embedded Debug APK was installed on `emulator-5554` with SDK ADB and verified to launch after `pm clear`.
- `python .\docs\screenshots\firsttimeuser-workflow\capture_firsttimeuser_device_screenshots.py --clear-app-data --serial emulator-5554 --adb-path 'C:\Users\user\AppData\Local\Android\Sdk\platform-tools\adb.exe' --adb-timeout-seconds 45 --ui-wait-timeout-seconds 30` passed and printed `Captured and verified 7 workflow screenshots plus contact sheet`.
- Fresh manifest evidence: `generatedAtUtc` `2026-07-09T14:28:11.684189+00:00`, `serial` `emulator-5554`, `adbPath` `C:\Users\user\AppData\Local\Android\Sdk\platform-tools\adb.exe`, `clearAppData` true, `debuggablePackageVerified` true, 7 steps, contact sheet `firsttimeuser-workflow-contact-sheet.png`.
- `python .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` passed: `First-user workflow artifacts are fresh and internally consistent.`
- `dotnet test .\tests\MentalGymnastics.App.Tests\MentalGymnastics.App.Tests.csproj --no-restore --filter "FullyQualifiedName~TrainingPresentationReadModelTests|FullyQualifiedName~PreUiTrainingWorkflowActiveSnapshotTests"` passed: 32 tests after the final verifier/docs update.
- `dotnet test .\tests\MentalGymnastics.Content.Tests\MentalGymnastics.Content.Tests.csproj --no-restore --filter FocusHoldGeneratedContentTests` passed: 5 tests after the final verifier/docs update.
- `dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj --no-restore` passed with 0 warnings and 0 errors after the final verifier/docs update.
- `python -m py_compile .\docs\screenshots\firsttimeuser-workflow\capture_firsttimeuser_device_screenshots.py .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py` passed after the final verifier/docs update.
- `dotnet build .\MentalGymnastics.sln --no-restore` passed with 0 warnings and 0 errors after the final verifier/docs update.
- `dotnet test .\MentalGymnastics.sln --no-build` passed: 630 tests after the final verifier/docs update.
- `git diff --check` passed with only line-ending normalization warnings.
- `dotnet build-server shutdown` was run afterward; MSBuild and VB/C# compiler servers shut down successfully.

## Screenshot And Review Status

Passed. The latest seven first-user screenshots and contact sheet are fresh for this cycle:

- `docs/screenshots/firsttimeuser-workflow/01-first-opened-today.png`
- `docs/screenshots/firsttimeuser-workflow/02-before-start-top.png`
- `docs/screenshots/firsttimeuser-workflow/03-before-start-action.png`
- `docs/screenshots/firsttimeuser-workflow/04-live-get-ready.png`
- `docs/screenshots/firsttimeuser-workflow/05-live-hold.png`
- `docs/screenshots/firsttimeuser-workflow/06-result-stopped-early.png`
- `docs/screenshots/firsttimeuser-workflow/07-return-next-action.png`
- `docs/screenshots/firsttimeuser-workflow/firsttimeuser-workflow-contact-sheet.png`

The strict verifier passed without stale mode. The first and returned Today screenshots are intentionally similar because returning to Today shows continuity, but they are not accidental duplicates: the workflow between them is distinct, and the result screen explains that the user stays on the same exercise and can start it again.

## Final Average-User Review

01 first opened Today:
The screen reads as a clear next-work screen. The user is told to start `Target Hold`, sees `Level 1`, and gets the simple action: hold one target for 3 minutes and tap if the mind wanders. The `Why this matters` and `Where this goes` card explains that this is the Focus Hold foundation, names the next branches, names later load increases, and explicitly avoids the smarter-person claim. `Inhibition` and `Discrimination` are formal terms, but each has a plain parenthetical explanation, so they do not function as unexplained jargon. The primary action is unambiguous: `Start Target Hold`.

02 before-start top:
The screen reads as a short preparation screen, not a hidden rulebook. `Read` and `Hold` are plain markers. The user sees the target `red circle`, knows to say it once in the head, and knows the timer begins after that. `Counts`, `Try again`, and `Honesty` are direct enough for a first user; they explain success without moralizing.

03 before-start action:
The lower preparation screen continues the same thought and exposes what is tracked before the start button. The user can see that the app tracks wander taps, return time, target changes, finish/stop, then sees the clear action `Start 3-minute hold`. There is no runtime terminology and no second competing primary action.

04 live get-ready:
The screen now says what to do: say the target once before the timer starts. `Your target` is visible, with `red circle`. The only actions are `Start holding` and `Not now`, so the user is not asked to interpret a protocol state. The small counters are subordinate and do not compete with the instruction.

05 live hold:
The screen says `Hold the target now. If your mind wanders, tap I wandered and return.` That is the actual exercise loop. `I wandered` is the obvious main action, and `Stop early` is honest without looking like the recommended path. No `drift`, `response`, pause/resume, or abstract running-state label is visible.

06 stopped result:
The screen explains the outcome in human terms: stopped before finishing, no successful set counted, stopped attempt saved, stay on this exercise, try again when ready. It is firm but not punitive. The next step row says `Start Target Hold when ready`, and the large action returns to Today, so the user knows what happened and what to do.

07 return Today:
Returning to Today feels continuous rather than reset. The same next exercise is still there with the same purpose/path explanation and `Start Target Hold` action. That matches the result screen's promise that the user stays on this exercise and can try again when ready.

Final review result: passes with flying colors. No screen leaves the user unsure what to do next, Target Hold no longer feels arbitrary, the ladder and destination appear before starting, the app does not overclaim transfer or intelligence gains, and first-level labels avoid unexplained runtime/domain jargon.

## Remaining Caveats

- No UX blocker remains for this goal.
- First-user screenshot capture with `--clear-app-data` requires an embedded-assembly Debug APK; this is now documented in the screenshot workflow README.
- The worktree remains intentionally dirty and uncommitted; no commit or push was performed.
