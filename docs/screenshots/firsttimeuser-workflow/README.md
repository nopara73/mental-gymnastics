# First User Experience Workflow

These screenshots document the clean first-user path through the Android app. The review is written from the perspective of an average new user, so the first-level labels should explain the actual exercise and next action rather than runtime or domain terminology.

## Expected Files

1. `01-first-opened-today.png` - first opened Today screen with `What should I do now?`, a plain `Start` marker, the concrete `Target Hold` exercise, `Level 1`, a short instruction about holding a simple target, the `Mind wandered` action, a compact `What you practice` / `How it gets harder` explanation, and `Start Target Hold` action, without secondary navigation, branch names, or program-status chips competing for attention.
2. `02-before-start-top.png` - before-start explanation with plain `Read`/`Hold` markers, steps, the actual target text, and `When it counts`.
3. `03-before-start-action.png` - before-start lower area showing the try-again criteria, what gets saved, and then the `Start 3-minute hold` action.
4. `04-live-get-ready.png` - live setup state with `Your target`, only `Start holding` and `Not now`, led by the get-ready instruction rather than a generic running-state label.
5. `05-live-hold.png` - active Target Hold state showing `Your target`, `Mind wandered` as the main action, and `Stop early` as the only stop control, led by the actual hold instruction.
6. `06-result-stopped-early.png` - stopped-session result explaining that the stopped attempt was saved, no success counted, the user stays on this exercise, they should try it again when ready, and the way back is `Back to Today`.
7. `07-return-next-action.png` - returned Today screen showing `What should I do now?`, a plain `Start` marker, `Target Hold`, `Level 1`, the same simple exercise loop, and `Start Target Hold` clearly, again without secondary navigation or program-status chips competing for attention.

`firsttimeuser-workflow-contact-sheet.png` is the contact sheet used for the written average-user review.

## Device Capture

The repeatable device-capture script uses a Debug-only Android screenshot mode plus UIAutomator text checks. It assumes an Android device or emulator is already running and a Debug APK is already installed; it does not start an emulator, build the app, tap through the workflow, or use `monkey`. Before touching the device, it refuses to run when .NET build/test processes are active, and it pauses briefly between capture steps so the device work is not rapid-fire. Live screenshot states are started without saving an active-session snapshot.

For first-user capture with `--clear-app-data`, install a Debug APK that embeds the managed assemblies. Do not rely on the Android `Install` target's fast-deployment payload for this capture, because clearing app data removes the fast-deployed assemblies and the app can crash back to the launcher.

```powershell
dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj --no-restore -p:EmbedAssembliesIntoApk=true
adb -s <adb-device-serial> install -r -d .\src\MentalGymnastics.Android\bin\Debug\net10.0-android\com.nopara73.mentalgymnastics-Signed.apk
```

```powershell
python .\docs\screenshots\firsttimeuser-workflow\capture_firsttimeuser_device_screenshots.py --clear-app-data
```

Use `--serial <adb-device-serial>` when more than one device is attached. The script checks for exactly one ready ADB device, the installed Android package, and a debuggable app build before deleting previous screenshots. Use `--adb-timeout-seconds <seconds>` to adjust the per-command timeout; the default is 30 seconds so a capture problem fails quickly instead of hanging. Use `--ui-wait-timeout-seconds <seconds>` to adjust how long each screen may take to expose the expected text; the default is 20 seconds.

The script prefers the Android SDK `platform-tools\adb.exe` by default when it is available and records the resolved path in `capture-manifest.json`. Use `--adb-path <path-to-adb>` only when you need to override that choice.

`--clear-app-data` resets local app data for the installed Android package before each capture step. Use it only on a test device or emulator.

The script requires either `--clear-app-data` or `--keep-app-data`. First-user validation must use `--clear-app-data`; `--keep-app-data` is only for debugging non-first-user states, skips the strict artifact verifier, and prints that the screenshots are unverified.

After the host-process, device, installed-package, and debuggable-build checks pass, the script removes the previous seven workflow PNGs, obsolete old-name workflow PNGs, the contact sheet, and `capture-manifest.json`. It derives scroll coordinates from the connected device size instead of assuming a fixed emulator resolution. The scrolled before-start capture waits for the top of the before-start screen, performs at least one swipe so it cannot duplicate the top capture on a tall device, then uses up to three bounded swipes until `Start 3-minute hold` and `What gets saved` are visible. A first-user capture with `--clear-app-data` validates every workflow PNG and the contact sheet, rejects duplicate workflow PNGs, writes a fresh `capture-manifest.json` with the UTC generation time, resolved device serial, package path, debug-build verification, expected text checks, pre-scroll expected text checks, maximum and actual scroll attempts, captured visible text, timeout settings, step cooldown, host-process safety snapshot, scroll steps, PNG dimensions, byte sizes, and SHA-256 hashes, then runs the strict artifact verifier before printing `Captured and verified`. The live and result steps wait for the same proof text used by the verifier, including `Not now`, `Mind wandered`, `Stop early`, `Stopped attempt saved`, `You stay on this exercise`, `try it again`, `Back to Today`, and `Start Target Hold`. Use that manifest as the freshness check before running the average-user review.

## Artifact Verification

Before using the contact sheet for the average-user review, run the local artifact verifier:

```powershell
python .\docs\screenshots\firsttimeuser-workflow\verify_firsttimeuser_artifacts.py
```

The verifier requires a fresh `capture-manifest.json`, checks the expected workflow files and contact sheet, verifies manifest hashes and dimensions, verifies the host-process safety check was not skipped, verifies screen-specific visible text for the instruction, before-start count/try-again criteria across the top and scrolled before-start screenshots, live action, stopped result, and next action, verifies exact UI labels for the main markers/actions instead of accepting only substring matches, verifies both Today screens ask `What should I do now?`, show a plain `Start` marker, and name `Target Hold` plus `Level 1`, verifies before-start uses plain `Read` and `Hold` markers, verifies the scrolled before-start screen shows `What gets saved` before `Start 3-minute hold`, verifies the get-ready Target Hold screen exposes `Your target` plus only `Start holding` and `Not now`, verifies the active Target Hold screen exposes `Your target`, `Mind wandered`, and `Stop early` without pause/resume or generic answer controls, verifies the first and returned Today screens are focused on the next exercise instead of secondary navigation, branch names, or program-status chips, verifies live screens lead with exercise instructions instead of a generic `Running` label, verifies the captured workflow includes the exact first-user criteria, rejects raw generated-target prefixes such as `Hold target phrase`, rejects unexplained marker symbols such as `>`, rejects forbidden first-user labels and category wording, rejects obsolete screenshot filenames, and fails if two workflow screenshots are identical. Short forbidden labels such as `FH`, `Prep`, `OK`, `Log`, `Map`, `Response`, `Submit`, `Guess`, and `Correct` are matched as standalone tokens, while phrase checks remain phrase-based. For diagnosing the current stale folder only, use `--allow-stale`; that mode still rejects duplicate workflow PNGs, but it does not prove freshness and must not be treated as passing review evidence.

## Written Review Gate

After the capture script prints `Captured and verified`, review the fresh contact sheet screen by screen from the perspective of an average first-time user. Record the review in `docs/android-ui-ux-redesign-checkpoint-2026-07-08.md` before accepting the workflow.

For each of the seven screenshots, answer these questions:

- What do I think this screen is?
- What am I supposed to do next?
- What words confuse me?
- What feels like internal jargon?
- What feels punitive, vague, duplicated, or pointless?
- Would I trust this app enough to continue?
- Would I know whether I succeeded, failed, or should try again?

The review fails if any screen leaves the user unsure what to do next, if a primary action is ambiguous, if first-level text depends on unexplained labels such as `FH`, `+2`, `Prep`, `evidence`, or protected-control language, if the first two screenshots are duplicates, if the live screen gives only a protocol state instead of the actual exercise instruction, or if the result screen does not explain what happened and the sensible next step in human terms. A failed review requires another implementation/validation cycle before completion.

## Current Evidence Status

The current screenshot expectations require a short Today exercise-loop explanation and the user-intent live action `Mind wandered`. After UI or copy changes, regenerate the seven workflow PNGs, contact sheet, and `capture-manifest.json` with `--clear-app-data` before treating the review as current. `02-before-start-top.png` and `03-before-start-action.png` intentionally represent the same before-start screen at different scroll positions; the scrolled screenshot must show `What gets saved` before `Start 3-minute hold`, so it is not a duplicate of the top capture. The stopped-result screenshot must show `Stopped attempt saved`, say to try it again, and point to `Start Target Hold` through the `Start` / `Next step` row.
