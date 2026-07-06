# Main Userflow Screenshots

These PNGs are deterministic generated mock screenshots for the Android main userflow. They are based on the Android UI layer, screen plans, visual state language, and current screen set.

No Android device or emulator was connected when these were created, so these are not `adb screencap` device captures.

Real Android emulator captures are stored in `device/`. They were captured from the `MentalGymnastics_API35` emulator after installing and launching `com.nopara73.mentalgymnastics`.

## Files

- `01-home-today.png`
- `02-branch-ladder.png`
- `03-branch-detail.png`
- `04-session-start.png`
- `05-live-session.png`
- `06-result.png`
- `07-progress-dashboard.png`
- `08-evidence-review.png`
- `09-maintenance-decay.png`
- `10-global-review.png`
- `11-backup-restore.png`
- `main-userflow-contact-sheet.png`

## Device Captures

- `device/01-home-today-device.png`
- `device/02-branch-ladder-device.png`
- `device/03-branch-detail-device.png`
- `device/04-session-start-device.png`
- `device/05-live-session-device.png`
- `device/06-result-device.png`
- `device/07-progress-dashboard-device.png`
- `device/08-evidence-review-device.png`
- `device/09-maintenance-decay-device.png`
- `device/10-global-review-device.png`
- `device/11-backup-restore-device.png`
- `device/device-main-userflow-contact-sheet.png`

Additional device captures show the lower session-start action area and the live action controls.

Regenerate with:

```powershell
python .\docs\screenshots\main-userflow\generate_main_userflow_screenshots.py
```
