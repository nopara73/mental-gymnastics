# UI/UX Overhaul Release Captures

Captured from a fully trimmed Release candidate on `emulator-5554` on 2026-07-11. The later completion, content-depth, and FH preflight corrections are verified in `docs/android-ui-ux-overhaul-acceptance-2026-07-11.md`; the authoritative final APK has SHA-256 `F5EF17BB7583F55A0D1B93088B488C3ECC4BDD09E310BBB133B5AACBD684DA19`.

## Sequence

1. `01-today.png` - prescribed work and measurable criteria.
2. `02-preflight.png` - generated target and attempt contract; workflow navigation hidden.
3. `03-live.png` - active Target Hold controls and evidence counters.
4. `04-result-stopped.png` - saved stopped attempt with one specific failure reason.
5. `05-map-narrow.png` - 360dp Map with wrapped long branch labels.
6. `06-branch-detail.png` - Level 1 branch state, criteria, demand, and direct route.
7. `07-review.png` - unframed program decision and one primary action.
8. `08-record.png` - saved session timeline entry.
9. `09-record-detail.png` - inspectable evidence and human failure reason.
10. `10-local-data.png` - valid local state with unavailable backup actions disabled.
11. `11-restore-confirmation.png` - consequence before confirm, explicit cancel, and automatic reveal of the confirmation controls.

## Final Verification Captures

- `final-latest-first-day-composited.png` - clean install with one `2:00` daily prescription.
- `final-latest-large-font-150.png` - 150% text with the complete `0 of 1` label and visible controls.
- `final-latest-live-before-interruption.png` - active evidence-driven Target Hold.
- `final-latest-interrupted-composited.png` - truthful non-resumable interruption handling.
- `final-latest-stopped-result-composited.png` - confirmed stop with `Hold ended before 2:00`.
- `final-review-due-composited.png` - due global review replacing ordinary practice.
- `final-review-detail-composited.png` - review decision, blocked inputs, cadence, and branch evidence.
- `final-review-after-completion-composited.png` - hold decision persisted and cadence reset to 42 days.

All standard-phone captures use the emulator's physical `1080x1920` / `420dpi` profile. Responsive evidence also covers a 360dp phone, landscape, an 800dp tablet, and font scale `1.5`. Some raw `adb screencap` frames in this folder show emulator capture tearing; the `*-composited.png` frames use the emulator's composited screenshot path and are the visual references.
