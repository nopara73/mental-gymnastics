from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


OUT = Path(__file__).resolve().parent
PACKAGE = "com.nopara73.mentalgymnastics"
SCREENSHOT_STEP_EXTRA = "mental_gymnastics.screenshot_step"
WINDOW_DUMP_PATH = "/sdcard/mental-gymnastics-window.xml"
CONTACT_SHEET = "firsttimeuser-workflow-contact-sheet.png"
MANIFEST = "capture-manifest.json"
VERIFIER = "verify_firsttimeuser_artifacts.py"
OBSOLETE_FILES = [
    "02-prescribed-work.png",
    "03-session-preflight.png",
    "04-live-prep.png",
    "05-live-work.png",
    "06-result-abandoned.png",
]
BLOCKING_HOST_PROCESSES = (
    "dotnet",
    "msbuild",
    "vbcscompiler",
    "testhost",
    "vstest.console",
)
OBSERVED_HOST_PROCESSES = (
    *BLOCKING_HOST_PROCESSES,
    "adb",
    "emulator",
    "gradle",
    "java",
    "qemu-system-x86_64",
)

CANVAS = (246, 247, 244)
SURFACE = (255, 255, 252)
HAIRLINE = (206, 211, 205)
INK = (26, 31, 33)


@dataclass(frozen=True)
class CaptureStep:
    file_name: str
    screenshot_step: str
    wait_for: tuple[str, ...]
    scroll_before_capture: bool = False
    pre_scroll_wait_for: tuple[str, ...] = ()
    max_scroll_attempts: int = 1


STEPS = [
    CaptureStep("01-first-opened-today.png", "today", ("Start Target Hold",)),
    CaptureStep("02-before-start-top.png", "before-start", ("Before you start", "When it counts")),
    CaptureStep(
        "03-before-start-action.png",
        "before-start",
        ("Start 3-minute hold", "What gets saved"),
        scroll_before_capture=True,
        pre_scroll_wait_for=("Before you start",),
        max_scroll_attempts=3),
    CaptureStep("04-live-get-ready.png", "live-get-ready", ("Start holding", "Not now")),
    CaptureStep("05-live-hold.png", "live-hold", ("Mind wandered", "Stop early")),
    CaptureStep(
        "06-result-stopped-early.png",
        "stopped-result",
        (
            "Stopped early",
            "Stopped attempt saved",
            "Back to Today",
            "You stay on this exercise",
            "try it again",
            "Start Target Hold")),
    CaptureStep("07-return-next-action.png", "return-today", ("Start Target Hold",)),
]


def font(size: int) -> ImageFont.ImageFont:
    candidates = [
        Path("C:/Windows/Fonts/segoeui.ttf"),
        Path("C:/Windows/Fonts/arial.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


FONT_LABEL = font(22)


class DeviceCapture:
    def __init__(self, serial: str | None, timeout_seconds: float, adb_path: str) -> None:
        self.serial = serial
        self.timeout_seconds = timeout_seconds
        self.adb_path = adb_path
        self.launcher_component: str | None = None

    def adb(self, *args: str, capture_output: bool = True, check: bool = True) -> subprocess.CompletedProcess[bytes]:
        command = [self.adb_path]
        if self.serial:
            command.extend(["-s", self.serial])
        command.extend(args)
        try:
            result = subprocess.run(
                command,
                capture_output=capture_output,
                check=False,
                timeout=self.timeout_seconds)
        except FileNotFoundError as exception:
            raise RuntimeError("adb was not found on PATH. Start from an Android SDK shell or add platform-tools.") from exception
        except subprocess.TimeoutExpired as exception:
            raise TimeoutError(f"adb command timed out after {self.timeout_seconds:g}s: {' '.join(command)}") from exception

        if check and result.returncode != 0:
            stderr = result.stderr.decode(errors="replace").strip()
            stdout = result.stdout.decode(errors="replace").strip()
            raise RuntimeError(f"adb command failed: {' '.join(command)}\n{stderr or stdout}")
        return result

    def ensure_ready(self) -> None:
        command = [self.adb_path, "devices"]
        try:
            result = subprocess.run(
                command,
                capture_output=True,
                check=False,
                timeout=self.timeout_seconds)
        except FileNotFoundError as exception:
            raise RuntimeError("adb was not found on PATH. Start from an Android SDK shell or add platform-tools.") from exception
        except subprocess.TimeoutExpired as exception:
            raise TimeoutError(f"adb command timed out after {self.timeout_seconds:g}s: {' '.join(command)}") from exception
        if result.returncode != 0:
            stderr = result.stderr.decode(errors="replace").strip()
            stdout = result.stdout.decode(errors="replace").strip()
            raise RuntimeError(f"adb command failed: {' '.join(command)}\n{stderr or stdout}")

        lines = result.stdout.decode(errors="replace").splitlines()[1:]
        devices: list[tuple[str, str]] = []
        for line in lines:
            parts = line.split()
            if len(parts) >= 2:
                devices.append((parts[0], parts[1]))

        ready = [serial for serial, state in devices if state == "device"]

        if self.serial:
            if self.serial not in ready:
                states = ", ".join(f"{serial}:{state}" for serial, state in devices) or "none"
                raise RuntimeError(f"ADB device {self.serial!r} is not ready. Attached devices: {states}")
            return

        if len(ready) != 1:
            states = ", ".join(f"{serial}:{state}" for serial, state in devices) or "none"
            raise RuntimeError(
                "Expected exactly one ready ADB device. "
                f"Use --serial when multiple devices are attached. Attached devices: {states}")

        self.serial = ready[0]

    def ensure_app_installed(self) -> list[str]:
        result = self.adb("shell", "pm", "path", PACKAGE)
        paths = [
            line.strip()
            for line in result.stdout.decode(errors="replace").splitlines()
            if line.strip().startswith("package:")
        ]
        if not paths:
            raise RuntimeError(
                f"Android package {PACKAGE!r} is not installed on device {self.serial!r}. "
                "Install the Debug APK before running screenshot capture.")

        return paths

    def ensure_debuggable_app(self) -> None:
        result = self.adb("shell", "run-as", PACKAGE, "id", check=False)
        if result.returncode != 0:
            stderr = result.stderr.decode(errors="replace").strip()
            stdout = result.stdout.decode(errors="replace").strip()
            raise RuntimeError(
                f"Android package {PACKAGE!r} is installed but is not debuggable on device {self.serial!r}. "
                "Install the Debug APK before running screenshot capture."
                f"\n{stderr or stdout}")

    def resolve_launcher_component(self) -> str:
        if self.launcher_component is not None:
            return self.launcher_component

        result = self.adb(
            "shell",
            "cmd",
            "package",
            "resolve-activity",
            "--brief",
            "-a",
            "android.intent.action.MAIN",
            "-c",
            "android.intent.category.LAUNCHER",
            PACKAGE)
        lines = [
            line.strip()
            for line in result.stdout.decode(errors="replace").splitlines()
            if line.strip()
        ]
        component = next((line for line in reversed(lines) if "/" in line), None)
        if component is None:
            raise RuntimeError(f"Could not resolve launcher activity for {PACKAGE!r}.")

        self.launcher_component = component
        return component

    def clear_app_data(self) -> None:
        self.adb("shell", "pm", "clear", PACKAGE)

    def start_app(self, screenshot_step: str) -> None:
        launcher_component = self.resolve_launcher_component()
        self.adb("shell", "am", "force-stop", PACKAGE)
        self.adb(
            "shell",
            "am",
            "start",
            "-W",
            "-a",
            "android.intent.action.MAIN",
            "-c",
            "android.intent.category.LAUNCHER",
            "-n",
            launcher_component,
            "--es",
            SCREENSHOT_STEP_EXTRA,
            screenshot_step,
        )

    def dump_ui(self) -> ET.Element:
        self.adb("shell", "uiautomator", "dump", WINDOW_DUMP_PATH)
        xml_bytes = self.adb("exec-out", "cat", WINDOW_DUMP_PATH).stdout
        return ET.fromstring(xml_bytes.decode(errors="replace"))

    def visible_text(self) -> str:
        root = self.dump_ui()
        values: list[str] = []
        for node in root.iter("node"):
            for attribute in ("text", "content-desc"):
                value = (node.attrib.get(attribute) or "").strip()
                if value:
                    values.append(value)
        return "\n".join(values)

    def wait_for_all(self, labels: tuple[str, ...], timeout_seconds: float = 20) -> str:
        deadline = time.monotonic() + timeout_seconds
        last_text = ""
        while time.monotonic() < deadline:
            last_text = self.visible_text()
            if all(label in last_text for label in labels):
                return last_text
            time.sleep(0.5)

        expected = ", ".join(labels)
        raise TimeoutError(f"Timed out waiting for: {expected}\n\nVisible text:\n{last_text}")

    def screenshot(self, file_name: str) -> None:
        result = self.adb("exec-out", "screencap", "-p")
        destination = OUT / file_name
        destination.write_bytes(result.stdout)

    def screen_size(self) -> tuple[int, int]:
        result = self.adb("shell", "wm", "size")
        lines = result.stdout.decode(errors="replace").splitlines()
        for line in reversed(lines):
            if ":" not in line:
                continue

            candidate = line.split(":", 1)[1].strip().split()[0]
            if "x" not in candidate:
                continue

            width_text, height_text = candidate.split("x", 1)
            return int(width_text), int(height_text)

        raise RuntimeError(f"Could not read Android screen size from: {result.stdout.decode(errors='replace')}")

    def swipe_up(self) -> None:
        width, height = self.screen_size()
        x = str(width // 2)
        start_y = str(int(height * 0.78))
        end_y = str(int(height * 0.34))
        self.adb("shell", "input", "swipe", x, start_y, x, end_y, "250")


def contact_sheet(files: list[str]) -> None:
    thumb_w, thumb_h = 324, 720
    margin = 36
    title_h = 88
    cols = 2
    rows = (len(files) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (thumb_w + margin) + margin, rows * (thumb_h + title_h + margin) + margin), CANVAS)
    draw = ImageDraw.Draw(sheet)

    for index, file_name in enumerate(files):
        with Image.open(OUT / file_name) as image:
            image.thumbnail((thumb_w, thumb_h))
            thumb = image.copy()
        col = index % cols
        row = index // cols
        x = margin + col * (thumb_w + margin)
        y = margin + row * (thumb_h + title_h + margin)
        draw.rounded_rectangle((x - 8, y - 8, x + thumb_w + 8, y + thumb_h + title_h), radius=20, fill=SURFACE, outline=HAIRLINE)
        sheet.paste(thumb, (x, y))
        label = file_name.removesuffix(".png").replace("-", " ")
        draw.text((x, y + thumb_h + 18), label, font=FONT_LABEL, fill=INK)

    sheet.save(OUT / CONTACT_SHEET)


def remove_previous_outputs(files: list[str]) -> None:
    for file_name in [*files, *OBSOLETE_FILES, CONTACT_SHEET, MANIFEST]:
        path = OUT / file_name
        if path.exists():
            path.unlink()


def normalize_process_name(value: str) -> str:
    name = value.strip().lower()
    if name.endswith(".exe"):
        name = name[:-4]
    return name


def host_process_counts(names: tuple[str, ...]) -> dict[str, int]:
    wanted = {normalize_process_name(name) for name in names}
    counts = {name: 0 for name in wanted}

    try:
        if os.name == "nt":
            result = subprocess.run(
                ["tasklist", "/fo", "csv", "/nh"],
                capture_output=True,
                check=False,
                timeout=5)
            if result.returncode != 0:
                return counts

            rows = csv.reader(result.stdout.decode(errors="replace").splitlines())
            process_names = [row[0] for row in rows if row]
        else:
            result = subprocess.run(
                ["ps", "-eo", "comm="],
                capture_output=True,
                check=False,
                timeout=5)
            if result.returncode != 0:
                return counts

            process_names = result.stdout.decode(errors="replace").splitlines()
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return counts

    for process_name in process_names:
        normalized = normalize_process_name(Path(process_name).name)
        if normalized in counts:
            counts[normalized] += 1

    return dict(sorted(counts.items()))


def active_process_counts(counts: dict[str, int], names: tuple[str, ...]) -> dict[str, int]:
    wanted = {normalize_process_name(name) for name in names}
    return {name: count for name, count in counts.items() if name in wanted and count > 0}


def ensure_no_blocking_host_processes(counts: dict[str, int]) -> None:
    active = active_process_counts(counts, BLOCKING_HOST_PROCESSES)
    if active:
        details = ", ".join(f"{name}={count}" for name, count in active.items())
        raise RuntimeError(
            "Refusing screenshot capture while .NET build/test processes are active. "
            f"Stop them first or rerun with --skip-host-process-check. Active: {details}")


def png_metadata(file_name: str) -> dict[str, object]:
    path = OUT / file_name
    if not path.exists():
        raise FileNotFoundError(f"Missing expected screenshot artifact: {path}")

    with Image.open(path) as image:
        width, height = image.size
        image.verify()

    size = path.stat().st_size
    if size <= 0:
        raise ValueError(f"Screenshot artifact is empty: {path}")

    sha256 = hashlib.sha256(path.read_bytes()).hexdigest()
    return {
        "file": file_name,
        "bytes": size,
        "width": width,
        "height": height,
        "sha256": sha256,
    }


def output_metadata(files: list[str]) -> tuple[list[dict[str, object]], dict[str, object]]:
    screenshots = [png_metadata(file_name) for file_name in files]
    contact = png_metadata(CONTACT_SHEET)
    hashes: dict[str, str] = {}
    for screenshot in screenshots:
        file_name = str(screenshot["file"])
        screenshot_hash = str(screenshot["sha256"])
        duplicate = hashes.get(screenshot_hash)
        if duplicate is not None:
            raise ValueError(f"Duplicate screenshot image: {duplicate} and {file_name}.")
        hashes[screenshot_hash] = file_name

    return screenshots, contact


def write_manifest(
    screenshots: list[dict[str, object]],
    contact_sheet_metadata: dict[str, object],
    args: argparse.Namespace,
    serial: str | None,
    package_paths: list[str],
    host_process_counts_before: dict[str, int],
    host_process_counts_after: dict[str, int],
    visible_text_by_file: dict[str, str],
    scroll_attempts_by_file: dict[str, int]) -> None:
    generated_at = datetime.now(timezone.utc).isoformat()
    manifest = {
        "generatedAtUtc": generated_at,
        "package": PACKAGE,
        "serial": serial,
        "adbPath": args.adb_path,
        "packagePaths": package_paths,
        "debuggablePackageVerified": True,
        "clearAppData": bool(args.clear_app_data),
        "keptAppData": bool(args.keep_app_data),
        "adbTimeoutSeconds": args.adb_timeout_seconds,
        "uiWaitTimeoutSeconds": args.ui_wait_timeout_seconds,
        "stepCooldownSeconds": args.step_cooldown_seconds,
        "hostProcessSafety": {
            "skipHostProcessCheck": bool(args.skip_host_process_check),
            "blockingProcessNames": list(BLOCKING_HOST_PROCESSES),
            "observedProcessNames": list(OBSERVED_HOST_PROCESSES),
            "before": host_process_counts_before,
            "after": host_process_counts_after,
        },
        "steps": [
            {
                "file": step.file_name,
                "screenshotStep": step.screenshot_step,
                "waitFor": list(step.wait_for),
                "scrollBeforeCapture": step.scroll_before_capture,
                "preScrollWaitFor": list(step.pre_scroll_wait_for),
                "maxScrollAttempts": step.max_scroll_attempts,
                "scrollAttempts": scroll_attempts_by_file[step.file_name],
                "visibleText": visible_text_by_file[step.file_name],
                "artifact": next(item for item in screenshots if item["file"] == step.file_name),
            }
            for step in STEPS
        ],
        "contactSheet": contact_sheet_metadata,
    }

    (OUT / MANIFEST).write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def run_artifact_verifier() -> None:
    verifier = OUT / VERIFIER
    if not verifier.exists():
        raise FileNotFoundError(f"Missing artifact verifier: {verifier}")

    result = subprocess.run(
        [sys.executable, str(verifier)],
        capture_output=True,
        check=False,
        timeout=30)
    if result.returncode != 0:
        output = result.stderr.decode(errors="replace").strip() or result.stdout.decode(errors="replace").strip()
        raise RuntimeError(output or "Artifact verifier failed.")


def positive_float(value: str) -> float:
    try:
        parsed = float(value)
    except ValueError as exception:
        raise argparse.ArgumentTypeError("value must be a number") from exception

    if parsed <= 0:
        raise argparse.ArgumentTypeError("value must be greater than zero")
    return parsed


def non_negative_float(value: str) -> float:
    try:
        parsed = float(value)
    except ValueError as exception:
        raise argparse.ArgumentTypeError("value must be a number") from exception

    if parsed < 0:
        raise argparse.ArgumentTypeError("value must be zero or greater")
    return parsed


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Capture the Android first-user workflow from an already-running device or emulator.")
    parser.add_argument(
        "--adb-path",
        default=default_adb_path(),
        help="ADB executable path. Defaults to the Android SDK platform-tools adb when found, otherwise adb on PATH.")
    parser.add_argument("--serial", help="ADB device serial. Omit when only one device is attached.")
    parser.add_argument(
        "--adb-timeout-seconds",
        type=positive_float,
        default=30.0,
        help="Per-ADB-command timeout. Defaults to 30 seconds so capture fails quickly instead of hanging.")
    parser.add_argument(
        "--ui-wait-timeout-seconds",
        type=positive_float,
        default=20.0,
        help="How long to wait for expected text on each screen. Defaults to 20 seconds.")
    parser.add_argument(
        "--step-cooldown-seconds",
        type=non_negative_float,
        default=0.5,
        help="Pause between capture steps to avoid rapid-fire device work. Defaults to 0.5 seconds.")
    parser.add_argument(
        "--skip-host-process-check",
        action="store_true",
        help="Skip the preflight check that refuses capture while .NET build/test processes are active.")
    data_group = parser.add_mutually_exclusive_group(required=True)
    data_group.add_argument(
        "--clear-app-data",
        action="store_true",
        help="Clear local app data before each capture step. This is required for first-user validation.")
    data_group.add_argument(
        "--keep-app-data",
        action="store_true",
        help="Keep existing app data. This is only for unverified debugging of non-first-user states.")
    return parser.parse_args()


def default_adb_path() -> str:
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        sdk_adb = Path(local_app_data) / "Android" / "Sdk" / "platform-tools" / "adb.exe"
        if sdk_adb.exists():
            return str(sdk_adb)

    android_home = os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT")
    if android_home:
        executable = "adb.exe" if os.name == "nt" else "adb"
        sdk_adb = Path(android_home) / "platform-tools" / executable
        if sdk_adb.exists():
            return str(sdk_adb)

    return "adb"


def main() -> None:
    args = parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    device = DeviceCapture(args.serial, args.adb_timeout_seconds, args.adb_path)
    files = [step.file_name for step in STEPS]
    host_process_counts_before = host_process_counts(OBSERVED_HOST_PROCESSES)
    if not args.skip_host_process_check:
        ensure_no_blocking_host_processes(host_process_counts_before)

    device.ensure_ready()
    package_paths = device.ensure_app_installed()
    device.ensure_debuggable_app()
    remove_previous_outputs(files)

    visible_text_by_file: dict[str, str] = {}
    scroll_attempts_by_file: dict[str, int] = {}
    for step in STEPS:
        if args.clear_app_data:
            device.clear_app_data()
        device.start_app(step.screenshot_step)
        scroll_attempts_by_file[step.file_name] = 0
        if step.scroll_before_capture:
            if step.pre_scroll_wait_for:
                _ = device.wait_for_all(step.pre_scroll_wait_for, args.ui_wait_timeout_seconds)
            for attempt in range(step.max_scroll_attempts):
                device.swipe_up()
                scroll_attempts_by_file[step.file_name] = attempt + 1
                time.sleep(0.6)
                visible_text = device.visible_text()
                if all(label in visible_text for label in step.wait_for):
                    break
        visible_text_by_file[step.file_name] = device.wait_for_all(step.wait_for, args.ui_wait_timeout_seconds)
        device.screenshot(step.file_name)
        if args.step_cooldown_seconds > 0:
            time.sleep(args.step_cooldown_seconds)

    contact_sheet(files)
    screenshots, contact_sheet_metadata = output_metadata(files)
    host_process_counts_after = host_process_counts(OBSERVED_HOST_PROCESSES)
    write_manifest(
        screenshots,
        contact_sheet_metadata,
        args,
        device.serial,
        package_paths,
        host_process_counts_before,
        host_process_counts_after,
        visible_text_by_file,
        scroll_attempts_by_file)
    if args.clear_app_data:
        run_artifact_verifier()
        print(f"Captured and verified {len(STEPS)} workflow screenshots plus contact sheet in {OUT}")
    else:
        print(
            f"Captured {len(STEPS)} unverified debugging screenshots plus contact sheet in {OUT}. "
            "Rerun with --clear-app-data for first-user review evidence.")


if __name__ == "__main__":
    try:
        main()
    except (OSError, RuntimeError, TimeoutError, ValueError, ET.ParseError) as exception:
        print(f"Capture failed: {exception}", file=sys.stderr)
        sys.exit(1)
