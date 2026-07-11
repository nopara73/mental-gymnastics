from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

from PIL import Image


OUT = Path(__file__).resolve().parent
PACKAGE = "com.nopara73.mentalgymnastics"
CONTACT_SHEET = "firsttimeuser-workflow-contact-sheet.png"
MANIFEST = "capture-manifest.json"

EXPECTED_STEPS = [
    ("01-first-opened-today.png", "today", ("Start Target Hold",), False),
    ("02-before-start-top.png", "before-start", ("Before you start", "When it counts"), False, ()),
    (
        "03-before-start-action.png",
        "before-start",
        ("Start 3-minute hold", "What gets saved"),
        True,
        ("Before you start",),
        3),
    ("04-live-get-ready.png", "live-get-ready", ("Start holding", "Not now"), False),
    ("05-live-hold.png", "live-hold", ("Mind wandered", "Stop early"), False),
    (
        "06-result-stopped-early.png",
        "stopped-result",
        (
            "Stopped early",
            "Stopped attempt saved",
            "Back to Today",
            "You stay on this exercise",
            "try it again",
            "Start Target Hold"),
        False),
    ("07-return-next-action.png", "return-today", ("Start Target Hold",), False),
]

OBSOLETE_FILES = [
    "02-prescribed-work.png",
    "03-session-preflight.png",
    "04-live-prep.png",
    "05-live-work.png",
    "06-result-abandoned.png",
]
BLOCKING_HOST_PROCESSES = [
    "dotnet",
    "msbuild",
    "vbcscompiler",
    "testhost",
    "vstest.console",
]

FORBIDDEN_VISIBLE_TEXT = [
    "FH",
    "+2",
    "Prep",
    "Preflight",
    "Evidence",
    "Runtime",
    ">",
    "protected control",
    "Focus Hold level 1",
    "Hold target phrase",
    "Hold target word",
    "Submit",
    "Response",
    "Guess",
    "Correct",
    "Prescribed work",
    "Live session",
    "Session state",
    "Opening the prepared session",
    "Practice · Target Hold",
    "Selecting prescribed work",
    "preparing the session",
    "Progress unchanged",
    "Success rules",
    "What gets recorded",
    "Why this matters",
    "Where this goes",
    "Focus Hold foundation",
    "Focus Shift",
    "Working Memory",
    "Inhibition",
    "Discrimination",
    "No promise this makes you smarter",
    "I wandered",
    "Drift",
    "drift",
    "Mark drift",
    "marked drifts",
    "Does not count if",
    "The stopped attempt was recorded",
    "Your place in Today is unchanged",
]

TOKEN_FORBIDDEN_VISIBLE_TEXT = {
    "+2",
    "Correct",
    "Data",
    "FH",
    "Guess",
    "Log",
    "Map",
    "OK",
    "Prep",
    "Response",
    "Review",
    "Running",
    "Submit",
}

REQUIRED_VISIBLE_TEXT_BY_FILE = {
    "01-first-opened-today.png": [
        "What should I do now?",
        "Start",
        "Target Hold",
        "Level 1",
        "Hold one simple target",
        "When attention leaves",
        "Mind wandered",
        "What you practice",
        "Practice one loop",
        "clean return",
        "How it gets harder",
        "After this is stable",
        "longer holds",
        "distraction",
        "memory",
        "switching",
        "transfer",
        "Start Target Hold",
    ],
    "02-before-start-top.png": [
        "Before you start",
        "Read",
        "Hold",
        "Steps",
        "When it counts",
        "Read the target",
        "This counts when:",
    ],
    "03-before-start-action.png": [
        "Start 3-minute hold",
        "What gets saved",
        "saves wander taps",
    ],
    "04-live-get-ready.png": [
        "Get ready: say the target",
        "Your target",
        "Start holding",
        "Not now",
    ],
    "05-live-hold.png": [
        "Hold the target now. If your mind wanders",
        "Your target",
        "Mind wandered",
        "Stop early",
    ],
    "06-result-stopped-early.png": [
        "Stopped early",
        "You stopped before finishing",
        "Stopped attempt saved",
        "No success counted",
        "You stay on this exercise",
        "try it again",
        "Start Target Hold",
        "Back to Today",
    ],
    "07-return-next-action.png": [
        "What should I do now?",
        "Start",
        "Target Hold",
        "Level 1",
        "Hold one simple target",
        "When attention leaves",
        "Mind wandered",
        "What you practice",
        "Practice one loop",
        "clean return",
        "How it gets harder",
        "After this is stable",
        "longer holds",
        "distraction",
        "memory",
        "switching",
        "transfer",
        "Start Target Hold",
    ],
}

FORBIDDEN_VISIBLE_TEXT_BY_FILE = {
    "01-first-opened-today.png": [
        "Data",
        "Map",
        "Log",
        "Review",
        "Ready",
        "Load set",
        "Advance work",
        "No advance",
    ],
    "04-live-get-ready.png": [
        "I wandered",
        "Pause",
        "Resume",
        "Running",
        "Stop early",
        "Submit answer",
    ],
    "05-live-hold.png": [
        "Start holding",
        "Fix answer",
        "Mark error",
        "Mark guess",
        "Pause",
        "Resume",
        "Running",
        "Submit answer",
        "Not now",
    ],
    "06-result-stopped-early.png": [
        "!",
        "Log",
        "OK",
        "failed or stopped",
        "progress stayed unchanged",
        "Next action",
    ],
    "07-return-next-action.png": [
        "Data",
        "Map",
        "Log",
        "Review",
        "Ready",
        "Load set",
        "Advance work",
        "No advance",
    ],
}

REQUIRED_WORKFLOW_VISIBLE_TEXT = [
    "5 taps or fewer",
    "Each return is within 10 seconds",
    "Same target the whole time",
    "Tap Mind wandered",
    "Target changed",
    "Wander not tapped",
    "More than 5 taps",
    "Slow return",
    "You stop early",
]

REQUIRED_BEFORE_START_VISIBLE_TEXT = [
    "This counts when:",
    "3 minutes finished",
    "5 taps or fewer",
    "Each return is within 10 seconds",
    "Same target the whole time",
    "Try again when:",
    "You stop early",
    "Target changed",
    "Wander not tapped",
    "More than 5 taps",
    "Slow return",
    "tap Mind wandered",
]

REQUIRED_EXACT_VISIBLE_TEXT_BY_FILE = {
    "01-first-opened-today.png": [
        "Start",
        "Target Hold",
        "Level 1",
        "Start Target Hold",
    ],
    "02-before-start-top.png": [
        "Read",
        "Hold",
        "Before you start",
        "Steps",
        "When it counts",
    ],
    "04-live-get-ready.png": [
        "Start holding",
        "Not now",
    ],
    "05-live-hold.png": [
        "Mind wandered",
        "Stop early",
    ],
    "06-result-stopped-early.png": [
        "Stopped early",
        "Saved",
        "Your place",
        "Start",
        "Next step",
        "Back to Today",
    ],
    "07-return-next-action.png": [
        "Start",
        "Target Hold",
        "Level 1",
        "Start Target Hold",
    ],
}


REQUIRED_VISIBLE_TEXT_ORDER_BY_FILE = {
    "03-before-start-action.png": [
        ("What gets saved", "Start 3-minute hold"),
    ],
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def png_metadata(file_name: str) -> dict[str, Any]:
    path = OUT / file_name
    if not path.exists():
        raise ValueError(f"Missing artifact: {file_name}")

    with Image.open(path) as image:
        width, height = image.size
        image.verify()

    byte_count = path.stat().st_size
    if byte_count <= 0:
        raise ValueError(f"Artifact is empty: {file_name}")

    return {
        "file": file_name,
        "bytes": byte_count,
        "width": width,
        "height": height,
        "sha256": sha256(path),
    }


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def contains_forbidden_text(visible_text: str, forbidden: str) -> bool:
    if forbidden in TOKEN_FORBIDDEN_VISIBLE_TEXT:
        return re.search(rf"(?<![A-Za-z0-9]){re.escape(forbidden)}(?![A-Za-z0-9])", visible_text, re.IGNORECASE) is not None

    return forbidden.casefold() in visible_text.casefold()


def visible_text_nodes(visible_text: str) -> set[str]:
    return {line.strip() for line in visible_text.splitlines() if line.strip()}


def load_manifest() -> dict[str, Any]:
    path = OUT / MANIFEST
    if not path.exists():
        raise ValueError(f"Missing {MANIFEST}. Rerun device capture before review.")

    return json.loads(path.read_text(encoding="utf-8"))


def verify_manifest_header(manifest: dict[str, Any]) -> None:
    require(manifest.get("package") == PACKAGE, "Manifest package does not match the Android app package.")
    require(bool(manifest.get("adbPath")), "Manifest must record the ADB executable path used for capture.")
    require(manifest.get("clearAppData") is True, "First-user review requires manifest clearAppData=true.")
    require(manifest.get("keptAppData") is False, "First-user review must not use kept app data.")
    require(bool(manifest.get("serial")), "Manifest must record the resolved ADB device serial.")
    require(bool(manifest.get("packagePaths")), "Manifest must record installed package paths.")
    require(
        manifest.get("debuggablePackageVerified") is True,
        "Manifest must record debuggable package verification.")
    require(float(manifest.get("adbTimeoutSeconds", 0)) > 0, "Manifest must record a positive ADB timeout.")
    require(float(manifest.get("uiWaitTimeoutSeconds", 0)) > 0, "Manifest must record a positive UI wait timeout.")
    require(float(manifest.get("stepCooldownSeconds", -1)) >= 0, "Manifest must record a step cooldown.")

    host_safety = manifest.get("hostProcessSafety")
    require(isinstance(host_safety, dict), "Manifest must record host process safety checks.")
    require(
        host_safety.get("skipHostProcessCheck") is False,
        "Fresh first-user capture must not skip the host process safety check.")
    for name in BLOCKING_HOST_PROCESSES:
        before = int(host_safety.get("before", {}).get(name, 0))
        after = int(host_safety.get("after", {}).get(name, 0))
        require(before == 0, f"Capture started while blocking host process was active: {name}")
        require(after == 0, f"Capture ended while blocking host process was active: {name}")


def verify_steps(manifest: dict[str, Any]) -> None:
    steps = manifest.get("steps")
    require(isinstance(steps, list), "Manifest steps must be a list.")
    require(len(steps) == len(EXPECTED_STEPS), "Manifest has the wrong number of workflow steps.")

    hashes: dict[str, str] = {}
    workflow_visible_text: list[str] = []
    before_start_visible_text: list[str] = []
    for index, expected_step in enumerate(EXPECTED_STEPS):
        if len(expected_step) == 4:
            file_name, screenshot_step, wait_for, scroll_before_capture = expected_step
            pre_scroll_wait_for: tuple[str, ...] = ()
            max_scroll_attempts = 1
        elif len(expected_step) == 5:
            file_name, screenshot_step, wait_for, scroll_before_capture, pre_scroll_wait_for = expected_step
            max_scroll_attempts = 1
        else:
            file_name, screenshot_step, wait_for, scroll_before_capture, pre_scroll_wait_for, max_scroll_attempts = expected_step
        step = steps[index]
        require(step.get("file") == file_name, f"Unexpected file for step {index + 1}.")
        require(step.get("screenshotStep") == screenshot_step, f"Unexpected screenshot step for {file_name}.")
        require(step.get("waitFor") == list(wait_for), f"Unexpected wait text for {file_name}.")
        require(
            step.get("scrollBeforeCapture") is scroll_before_capture,
            f"Unexpected scroll setting for {file_name}.")
        require(
            step.get("preScrollWaitFor", []) == list(pre_scroll_wait_for),
            f"Unexpected pre-scroll wait text for {file_name}.")
        require(
            step.get("maxScrollAttempts", 1) == max_scroll_attempts,
            f"Unexpected max scroll attempts for {file_name}.")
        scroll_attempts = step.get("scrollAttempts", 0)
        require(isinstance(scroll_attempts, int), f"Manifest scroll attempts must be an integer for {file_name}.")
        if scroll_before_capture:
            require(1 <= scroll_attempts <= max_scroll_attempts, f"Scroll attempts out of range for {file_name}.")
        else:
            require(scroll_attempts == 0, f"Non-scrolled step recorded scroll attempts for {file_name}.")

        visible_text = step.get("visibleText")
        require(isinstance(visible_text, str) and visible_text.strip(), f"Missing visible text for {file_name}.")
        workflow_visible_text.append(visible_text)
        if screenshot_step == "before-start":
            before_start_visible_text.append(visible_text)
        for label in wait_for:
            require(label in visible_text, f"Visible text for {file_name} does not contain expected label: {label}")
        for label in REQUIRED_VISIBLE_TEXT_BY_FILE[file_name]:
            require(label in visible_text, f"Visible text for {file_name} does not contain required text: {label}")
        visible_nodes = visible_text_nodes(visible_text)
        for label in REQUIRED_EXACT_VISIBLE_TEXT_BY_FILE.get(file_name, []):
            require(label in visible_nodes, f"Visible text for {file_name} does not contain exact label: {label}")
        for first, second in REQUIRED_VISIBLE_TEXT_ORDER_BY_FILE.get(file_name, []):
            first_index = visible_text.find(first)
            second_index = visible_text.find(second)
            require(
                first_index >= 0 and second_index >= 0 and first_index < second_index,
                f"Visible text for {file_name} must show {first!r} before {second!r}.")

        for forbidden in FORBIDDEN_VISIBLE_TEXT:
            require(
                not contains_forbidden_text(visible_text, forbidden),
                f"Visible text for {file_name} contains forbidden text: {forbidden}")
        for forbidden in FORBIDDEN_VISIBLE_TEXT_BY_FILE.get(file_name, []):
            require(
                not contains_forbidden_text(visible_text, forbidden),
                f"Visible text for {file_name} contains forbidden text for this screen: {forbidden}")

        actual = png_metadata(file_name)
        recorded = step.get("artifact")
        require(isinstance(recorded, dict), f"Missing manifest artifact metadata for {file_name}.")
        for key in ("bytes", "width", "height", "sha256"):
            require(recorded.get(key) == actual[key], f"Manifest {key} mismatch for {file_name}.")

        duplicate = hashes.get(actual["sha256"])
        require(duplicate is None, f"Duplicate screenshot image: {duplicate} and {file_name}.")
        hashes[actual["sha256"]] = file_name

    combined_visible_text = "\n".join(workflow_visible_text)
    for label in REQUIRED_WORKFLOW_VISIBLE_TEXT:
        require(
            label in combined_visible_text,
            f"Captured workflow text does not contain required criteria text: {label}")

    combined_before_start_text = "\n".join(before_start_visible_text)
    for label in REQUIRED_BEFORE_START_VISIBLE_TEXT:
        require(
            label in combined_before_start_text,
            f"Before-start screenshots do not contain required criteria text: {label}")


def verify_contact_sheet(manifest: dict[str, Any]) -> None:
    actual = png_metadata(CONTACT_SHEET)
    recorded = manifest.get("contactSheet")
    require(isinstance(recorded, dict), "Missing contact sheet metadata in manifest.")
    for key in ("bytes", "width", "height", "sha256"):
        require(recorded.get(key) == actual[key], f"Manifest contact sheet {key} mismatch.")


def verify_obsolete_files_absent() -> None:
    present = [file_name for file_name in OBSOLETE_FILES if (OUT / file_name).exists()]
    require(not present, f"Obsolete workflow screenshots are still present: {', '.join(present)}")


def verify_no_duplicate_workflow_pngs() -> None:
    hashes: dict[str, str] = {}
    for file_name, *_ in EXPECTED_STEPS:
        actual = png_metadata(file_name)
        duplicate = hashes.get(actual["sha256"])
        require(duplicate is None, f"Duplicate screenshot image: {duplicate} and {file_name}.")
        hashes[actual["sha256"]] = file_name


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Verify first-user workflow screenshots, contact sheet, and freshness manifest.")
    parser.add_argument(
        "--allow-stale",
        action="store_true",
        help="Verify PNG readability and duplicate-image rejection when the manifest is missing. This is for diagnosing stale artifacts.")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    verify_obsolete_files_absent()

    if args.allow_stale and not (OUT / MANIFEST).exists():
        for file_name, *_ in EXPECTED_STEPS:
            _ = png_metadata(file_name)
        _ = png_metadata(CONTACT_SHEET)
        verify_no_duplicate_workflow_pngs()
        print("Stale artifact readability check passed, but manifest is missing.")
        return

    manifest = load_manifest()
    verify_manifest_header(manifest)
    verify_steps(manifest)
    verify_contact_sheet(manifest)
    print("First-user workflow artifacts are fresh and internally consistent.")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, json.JSONDecodeError) as exception:
        print(f"Verification failed: {exception}", file=sys.stderr)
        sys.exit(1)
