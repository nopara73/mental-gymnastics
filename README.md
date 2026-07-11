# Mental Gymnastics

Mental Gymnastics is a native, offline Android training product for standards-based mental practice. It prescribes one counted session per local program day, changes one named load variable at a time from recorded evidence, executes all 40 documented branch-level standards, and lets the Core library decide progression without relying on streaks or self-certified advancement.

The implementation is split into pure domain rules, local persistence, deterministic generated content, a headless runtime, app workflows, and a native Android UI. The app has no accounts, network dependency, telemetry, cloud sync, or AI/API dependency.

## Architecture

| Layer | Project | Responsibility |
| --- | --- | --- |
| Core | `src/MentalGymnastics.Core` | The 40 executable standards, branch-level state machine, gates, progressive load, daily programming, readiness, ownership, stabilization, maintenance, decay, dependency caps, balance, transfer, recovery, deload, failure routing, and global review rules. |
| Persistence | `src/MentalGymnastics.Persistence` | Offline, userless, app-owned local JSON storage for daily prescriptions, practitioner state, sessions, attempts, evidence, stabilization, maintenance, decay/restoration, generated instances, active runtime snapshots, progress summaries, backup/restore, migrations, transactions, and integrity validation. |
| Runtime | `src/MentalGymnastics.Runtime` | Headless live session execution: definitions, lifecycle, UTC-comparable clocks, phases, cues, drill commands, objective scoring facts, evidence capture, completion results, snapshot/restore, and Core/Persistence handoffs. |
| Generated Content | `src/MentalGymnastics.Content` | Local deterministic drill material, content identity/versioning, freshness/equivalence, local content banks, validation, difficulty audit, runtime packaging, and persistence handoff. |
| App Integration | `src/MentalGymnastics.App` | Workflows that compose Core, Persistence, Runtime, and Generated Content for daily selection, pressure/recovery priority, content and runtime preparation, completion processing, active snapshots, global review, and progress refresh. |
| Android | `src/MentalGymnastics.Android` | Native Train, Map, Record, Review, live drill, result, evidence, and local backup/restore surfaces. It renders App/Runtime state and owns no progression or persistence rules. |

Dependency direction should stay one-way: lower libraries do not reference `MentalGymnastics.App` or Android. Android may reference the pre-UI app integration layer.

## Persistence

Persistence is intentionally local JSON right now. SQLite, Room, SharedPreferences progression mirrors, accounts, sync, backend services, telemetry, analytics, notifications, and AI/API dependencies are not required for the current architecture.

Use `LocalDatabaseOptions.ForAppOwnedPath(...)` and the persistence stores through app integration. If a future workflow needs new stored facts, add them to `MentalGymnastics.Persistence` with tests rather than creating a parallel Android-local store.

## Android Contract

The Android UI:

1. Supply an app-owned local JSON file path to `AppStartupConfiguration`.
2. Uses `PreUiTrainingWorkflowService`, `DailyTrainingWorkflowService`, and related app-layer services for startup, daily work, content preparation, runtime sessions, safe snapshot handling, completion, review, and progress refresh.
3. Render app-facing read models and runtime state.
4. Forward user actions into runtime/app integration commands.
5. Display Core decisions as returned without weakening prerequisites, standards, maintenance blocks, dependency caps, or failed/abandoned session outcomes.

Android does not create independent phase timers, cue schedulers, hidden evidence logs, screen-local pass/fail flags, SharedPreferences or Room progression state, ad hoc prompts, generated-content identity schemes, or direct advancement decisions.

## Documentation

Start with [docs/README.md](docs/README.md). The key references are:

- [Progression Against Vibes](docs/foundation/progression-against-vibes.md)
- [Standards-Based Skill Ladder](docs/foundation/standards-based-skill-ladder.md)
- [Complete Training Program](docs/program/training-program.md)
- [Core Library](docs/core-library.md)
- [Local Persistence Boundary](docs/local-persistence-boundary.md)
- [Session Runtime Boundary](docs/session-runtime-boundary.md)
- [Generated Content Boundary](docs/generated-content-boundary.md)
- [Pre-UI App Integration Boundary](docs/app-integration-boundary.md)
- [Android UI Layer](docs/android-ui-layer.md)

## Requirements

- .NET SDK 10.0.301 or newer compatible 10.0 SDK
- .NET Android workload for building/deploying the Android host
- Android SDK platform tools (`adb`) for device deployment
- A phone with Developer options and USB debugging enabled for deployment

## Build And Test

```powershell
dotnet build .\MentalGymnastics.sln -c Debug
dotnet test .\MentalGymnastics.sln --no-build
dotnet build .\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj -c Release
```

## Deploy To A USB-Connected Android Phone

1. Enable Developer options on the phone.
2. Enable USB debugging.
3. Connect the phone over USB and accept the RSA debugging prompt.
4. Verify that the device is authorized:

```powershell
adb devices
```

5. Build, install, and launch the app:

```powershell
.\eng\deploy-android.ps1
```

The app package ID is `com.nopara73.mentalgymnastics`.
