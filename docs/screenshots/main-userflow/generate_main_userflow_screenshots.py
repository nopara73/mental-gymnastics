from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from textwrap import wrap

from PIL import Image, ImageDraw, ImageFont


OUT = Path(__file__).resolve().parent
W, H = 1080, 2400

CANVAS = (246, 247, 244)
SURFACE = (255, 255, 252)
SURFACE_MUTED = (237, 239, 234)
INK = (26, 31, 33)
INK_MUTED = (89, 96, 99)
HAIRLINE = (206, 211, 205)
TRAINING = (39, 121, 112)
TEST_READY = (79, 82, 150)
PASSED_ONCE = (144, 107, 44)
OWNED = (35, 94, 68)
MAINTENANCE = (170, 118, 39)
BLOCKED = (157, 54, 58)
RECOVERY = (91, 104, 119)
TRANSFER = (108, 82, 143)


def font(name: str, size: int) -> ImageFont.FreeTypeFont:
    candidates = [
        Path("C:/Windows/Fonts") / name,
        Path("C:/Windows/Fonts/segoeui.ttf"),
        Path("C:/Windows/Fonts/arial.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


FONT_REG = font("segoeui.ttf", 36)
FONT_MED = font("seguisb.ttf", 36)
FONT_BOLD = font("segoeuib.ttf", 42)
FONT_TITLE = font("segoeuib.ttf", 58)
FONT_HERO = font("segoeuib.ttf", 82)
FONT_SMALL = font("segoeui.ttf", 28)
FONT_SMALL_BOLD = font("seguisb.ttf", 28)
FONT_MICRO = font("segoeui.ttf", 22)


@dataclass(frozen=True)
class Shot:
    file_name: str
    title: str
    subtitle: str
    active_nav: str


def new_canvas(shot: Shot) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    im = Image.new("RGB", (W, H), CANVAS)
    draw = ImageDraw.Draw(im)
    draw_status(draw)
    draw_header(draw, shot)
    draw_bottom_nav(draw, shot.active_nav)
    return im, draw


def draw_status(draw: ImageDraw.ImageDraw) -> None:
    draw.text((56, 32), "09:41", font=FONT_SMALL_BOLD, fill=INK)
    draw.rounded_rectangle((840, 42, 948, 66), radius=11, outline=INK, width=3)
    draw.rounded_rectangle((954, 48, 968, 60), radius=4, fill=INK)
    draw.rounded_rectangle((850, 49, 918, 59), radius=5, fill=INK)
    draw.text((700, 30), "Offline", font=FONT_MICRO, fill=INK_MUTED)


def draw_header(draw: ImageDraw.ImageDraw, shot: Shot) -> None:
    draw.text((56, 104), shot.title, font=FONT_TITLE, fill=INK)
    draw.text((58, 172), shot.subtitle, font=FONT_SMALL, fill=INK_MUTED)
    draw.line((56, 232, W - 56, 232), fill=HAIRLINE, width=2)


def draw_bottom_nav(draw: ImageDraw.ImageDraw, active: str) -> None:
    y = H - 178
    draw.rounded_rectangle((36, y, W - 36, H - 38), radius=44, fill=SURFACE, outline=HAIRLINE, width=2)
    items = [("Work", "W"), ("Map", "M"), ("Progress", "P"), ("Evidence", "E"), ("Local", "L")]
    step = (W - 96) // len(items)
    for idx, (label, code) in enumerate(items):
        x = 58 + idx * step
        selected = label == active
        color = TRAINING if selected else INK_MUTED
        if selected:
            draw.rounded_rectangle((x - 12, y + 24, x + 128, y + 116), radius=34, fill=(229, 242, 238))
        draw.rounded_rectangle((x + 40, y + 34, x + 78, y + 72), radius=12, outline=color, width=4)
        draw.text((x + 52, y + 42), code, font=FONT_MICRO, fill=color, anchor="mm")
        draw.text((x + 26, y + 84), label, font=FONT_MICRO, fill=color)


def card(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], title: str | None = None) -> None:
    draw.rounded_rectangle(box, radius=24, fill=SURFACE, outline=HAIRLINE, width=2)
    if title:
        draw.text((box[0] + 34, box[1] + 28), title, font=FONT_BOLD, fill=INK)


def panel_title(draw: ImageDraw.ImageDraw, x: int, y: int, title: str, detail: str | None = None) -> None:
    draw.text((x, y), title, font=FONT_BOLD, fill=INK)
    if detail:
        draw.text((x, y + 52), detail, font=FONT_SMALL, fill=INK_MUTED)


def chip(draw: ImageDraw.ImageDraw, x: int, y: int, text: str, color: tuple[int, int, int], fill: tuple[int, int, int] = SURFACE) -> int:
    pad = 20
    bbox = draw.textbbox((0, 0), text, font=FONT_SMALL_BOLD)
    width = bbox[2] - bbox[0] + pad * 2
    draw.rounded_rectangle((x, y, x + width, y + 48), radius=20, fill=fill, outline=color, width=3)
    draw.text((x + pad, y + 8), text, font=FONT_SMALL_BOLD, fill=color)
    return width


def button(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], label: str, color: tuple[int, int, int], enabled: bool = True) -> None:
    fill = color if enabled else SURFACE_MUTED
    text_fill = (255, 255, 252) if enabled else INK_MUTED
    draw.rounded_rectangle(box, radius=24, fill=fill, outline=color if not enabled else fill, width=3)
    draw.text(((box[0] + box[2]) // 2, (box[1] + box[3]) // 2), label, font=FONT_BOLD, fill=text_fill, anchor="mm")


def wrap_text(draw: ImageDraw.ImageDraw, text: str, x: int, y: int, max_width: int, font_obj: ImageFont.ImageFont, fill: tuple[int, int, int], line_gap: int = 12) -> int:
    words = text.split()
    lines: list[str] = []
    current = ""
    for word in words:
        candidate = word if not current else f"{current} {word}"
        if draw.textbbox((0, 0), candidate, font=font_obj)[2] <= max_width:
            current = candidate
        else:
            if current:
                lines.append(current)
            current = word
    if current:
        lines.append(current)
    yy = y
    for line in lines:
        draw.text((x, yy), line, font=font_obj, fill=fill)
        yy += font_obj.size + line_gap
    return yy


def state_node(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], label: str, state: str) -> None:
    x1, y1, x2, y2 = box
    mid_y = (y1 + y2) // 2
    if state == "unopened":
        draw.rounded_rectangle(box, radius=18, fill=CANVAS, outline=HAIRLINE, width=4)
        draw.line((x1 + 26, mid_y, x2 - 26, mid_y), fill=HAIRLINE, width=4)
    elif state == "training":
        draw.rounded_rectangle(box, radius=18, fill=SURFACE, outline=TRAINING, width=5)
        draw.arc((x1 + 12, y1 + 12, x2 - 12, y2 - 12), 20, 320, fill=TRAINING, width=5)
    elif state == "test":
        draw.rounded_rectangle(box, radius=18, fill=SURFACE, outline=TEST_READY, width=5)
        draw.polygon([(x2 - 34, y1 + 16), (x2 - 12, mid_y), (x2 - 34, y2 - 16)], fill=TEST_READY)
    elif state == "passed":
        draw.rounded_rectangle(box, radius=18, fill=SURFACE, outline=PASSED_ONCE, width=5)
        draw.rectangle((x1 + 4, y1 + 4, (x1 + x2) // 2, y2 - 4), fill=(236, 223, 197))
        draw.text((x2 - 26, y1 + 8), "1", font=FONT_MICRO, fill=PASSED_ONCE)
    elif state == "stab":
        draw.rounded_rectangle(box, radius=18, fill=SURFACE, outline=TEST_READY, width=5)
        seg_w = (x2 - x1 - 36) // 3
        for i in range(3):
            fill = TEST_READY if i < 2 else CANVAS
            sx = x1 + 16 + i * (seg_w + 6)
            draw.rounded_rectangle((sx, y2 - 22, sx + seg_w, y2 - 12), radius=5, fill=fill, outline=TEST_READY, width=2)
    elif state == "owned":
        draw.rounded_rectangle(box, radius=18, fill=OWNED, outline=OWNED, width=4)
    elif state == "maintenance":
        draw.rounded_rectangle(box, radius=18, fill=OWNED, outline=MAINTENANCE, width=7)
        draw.ellipse((x2 - 24, y1 + 8, x2 - 8, y1 + 24), fill=MAINTENANCE)
    elif state == "decayed":
        draw.rounded_rectangle(box, radius=18, fill=(252, 232, 232), outline=BLOCKED, width=7)
        draw.line((x1 + 14, y2 - 12, x2 - 14, y1 + 12), fill=BLOCKED, width=7)
        draw.line((x1 + 18, y1 + 18, x2 - 20, y2 - 18), fill=BLOCKED, width=3)
    elif state == "blocked":
        draw.rounded_rectangle(box, radius=18, fill=SURFACE, outline=BLOCKED, width=6)
        draw.line((x1 + 12, mid_y, x2 - 12, mid_y), fill=BLOCKED, width=9)
    draw.text(((x1 + x2) // 2, mid_y), label, font=FONT_SMALL_BOLD, fill=(255, 255, 252) if state == "owned" else INK, anchor="mm")


def evidence_ticks(draw: ImageDraw.ImageDraw, x: int, y: int, items: list[tuple[str, tuple[int, int, int], bool]]) -> None:
    for idx, (label, color, filled) in enumerate(items):
        xx = x + idx * 122
        draw.rounded_rectangle((xx, y, xx + 92, y + 48), radius=16, fill=color if filled else SURFACE, outline=color, width=3)
        draw.text((xx + 46, y + 24), label, font=FONT_MICRO, fill=(255, 255, 252) if filled else color, anchor="mm")


def branch_preview(draw: ImageDraw.ImageDraw, x: int, y: int) -> None:
    branches = [("FH", "passed"), ("FS", "training"), ("WM", "maintenance"), ("IR", "owned"), ("DE", "decayed")]
    for i, (code, state) in enumerate(branches):
        xx = x + i * 182
        state_node(draw, (xx, y, xx + 132, y + 86), code, state)
        if i < len(branches) - 1:
            draw.line((xx + 138, y + 43, xx + 172, y + 43), fill=BLOCKED if state == "decayed" else HAIRLINE, width=5)


def shot_home() -> None:
    shot = Shot("01-home-today.png", "Work", "Actionable training state", "Work")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 560))
    chip(d, 90, 318, "Due", MAINTENANCE, (255, 247, 229))
    chip(d, 184, 318, "Blocked", BLOCKED, (252, 232, 232))
    panel_title(d, 90, 388, "WM L3 maintenance is overdue", "Dependent advancement stays capped.")
    button(d, (730, 430, 974, 512), "Restore", BLOCKED, enabled=True)

    card(d, (56, 604, 1024, 1012), "Next allowed work")
    chip(d, 92, 690, "Practice", TRAINING, (229, 242, 238))
    d.text((92, 760), "FH L1 Target Hold", font=FONT_HERO, fill=INK)
    wrap_text(d, "3 min hold. Mark every drift. No target substitution.", 96, 860, 650, FONT_SMALL, INK_MUTED)
    button(d, (760, 842, 974, 932), "Start", TRAINING)

    card(d, (56, 1058, 1024, 1358), "Branch preview")
    branch_preview(d, 96, 1150)
    d.text((96, 1282), "1/3 passed is not owned. Decay blocks dependent gates.", font=FONT_SMALL, fill=INK_MUTED)

    card(d, (56, 1412, 1024, 2028), "Today")
    rows = [("Day 1", "Practice", "FH, FS, WM", TRAINING), ("Urgent", "Maintenance", "WM L3", MAINTENANCE), ("Blocked", "CO L1 test", "Prerequisites", BLOCKED)]
    yy = 1500
    for day, kind, target, color in rows:
        chip(d, 94, yy, day, color)
        d.text((248, yy + 2), kind, font=FONT_BOLD, fill=INK)
        d.text((248, yy + 54), target, font=FONT_SMALL, fill=INK_MUTED)
        yy += 142
    im.save(OUT / shot.file_name)


def shot_ladder() -> None:
    shot = Shot("02-branch-ladder.png", "Map", "Branch ladder and blocked edges", "Map")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 1928), "Branch ladder")
    x0, y0 = 174, 400
    col_w, row_h = 148, 154
    for j, level in enumerate(["L1", "L2", "L3", "L4", "L5"]):
        d.text((x0 + j * col_w + 42, y0 - 54), level, font=FONT_SMALL_BOLD, fill=INK_MUTED)
    matrix = {
        "FH": ["passed", "training", "unopened", "unopened", "unopened"],
        "FS": ["training", "unopened", "unopened", "unopened", "unopened"],
        "WM": ["test", "training", "maintenance", "unopened", "unopened"],
        "IR": ["owned", "training", "unopened", "unopened", "unopened"],
        "DE": ["owned", "decayed", "unopened", "unopened", "unopened"],
        "CO": ["blocked", "unopened", "unopened", "unopened", "unopened"],
        "AI": ["unopened", "unopened", "unopened", "unopened", "unopened"],
        "TI": ["unopened", "unopened", "unopened", "unopened", "unopened"],
    }
    for i, (branch, states) in enumerate(matrix.items()):
        y = y0 + i * row_h
        d.text((94, y + 38), branch, font=FONT_BOLD, fill=INK)
        for j, state in enumerate(states):
            x = x0 + j * col_w
            if j < len(states) - 1:
                d.line((x + 100, y + 43, x + col_w - 8, y + 43), fill=BLOCKED if state in {"decayed", "blocked"} else HAIRLINE, width=5)
            state_node(d, (x, y, x + 96, y + 86), "", state)
    card(d, (84, 1700, 996, 1860))
    chip(d, 120, 1744, "Decayed", BLOCKED, (252, 232, 232))
    d.text((308, 1736), "DE L2 caps CO advancement", font=FONT_BOLD, fill=INK)
    d.text((308, 1788), "Restore before dependent tests.", font=FONT_SMALL, fill=INK_MUTED)
    im.save(OUT / shot.file_name)


def shot_branch_detail() -> None:
    shot = Shot("03-branch-detail.png", "Map", "Branch detail: Focus Hold", "Map")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 690))
    d.text((92, 330), "FH L1", font=FONT_HERO, fill=INK)
    chip(d, 306, 350, "1/3", PASSED_ONCE, (250, 240, 219))
    d.line((120, 510, 900, 510), fill=HAIRLINE, width=6)
    states = [("L1", "passed"), ("L2", "blocked"), ("L3", "unopened")]
    for idx, (label, state) in enumerate(states):
        state_node(d, (118 + idx * 284, 456, 244 + idx * 284, 564), label, state)
    d.text((92, 604), "Next level remains locked until stabilization is owned.", font=FONT_SMALL, fill=INK_MUTED)

    card(d, (56, 744, 1024, 1266), "Standard")
    wrap_text(d, "Hold one simple target for 3 minutes. No more than 5 marked drifts. Each return within 10 seconds.", 96, 830, 840, FONT_REG, INK)
    d.line((96, 1046, 984, 1046), fill=HAIRLINE, width=2)
    d.text((96, 1084), "Honesty constraint", font=FONT_SMALL_BOLD, fill=BLOCKED)
    wrap_text(d, "Target is stated before set; every drift is marked. No target substitution.", 96, 1130, 830, FONT_SMALL, INK_MUTED)

    card(d, (56, 1320, 1024, 1660), "Evidence")
    evidence_ticks(d, 96, 1408, [("Pass", PASSED_ONCE, True), ("Drift", TRAINING, False), ("WM", RECOVERY, False)])
    d.text((96, 1510), "One formal pass. Two clean stabilization passes still required.", font=FONT_SMALL, fill=INK_MUTED)
    button(d, (636, 1540, 974, 1620), "Stabilize", TRAINING)
    im.save(OUT / shot.file_name)


def shot_session_start() -> None:
    shot = Shot("04-session-start.png", "Start", "Preflight standard and constraint", "Work")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 520))
    chip(d, 92, 330, "Practice", TRAINING, (229, 242, 238))
    d.text((92, 396), "FH L1 Target Hold", font=FONT_HERO, fill=INK)

    card(d, (56, 574, 1024, 1320), "Standard panel")
    d.text((96, 662), "Load", font=FONT_SMALL_BOLD, fill=INK_MUTED)
    chip(d, 198, 654, "3 min", TRAINING)
    chip(d, 316, 654, "simple target", TRAINING)
    chip(d, 548, 654, "10 sec return", TRAINING)
    d.text((96, 760), "Standard", font=FONT_SMALL_BOLD, fill=INK)
    wrap_text(d, "No more than 5 marked drifts; each return within 10 seconds; no target change.", 96, 810, 850, FONT_REG, INK)
    d.text((96, 1016), "Honesty constraint", font=FONT_SMALL_BOLD, fill=BLOCKED)
    wrap_text(d, "Target is stated before the set. Every drift is marked immediately.", 96, 1066, 850, FONT_REG, INK)
    d.line((96, 1230, 984, 1230), fill=HAIRLINE, width=2)
    d.text((96, 1258), "Expected evidence: drift count, return timing, output sample", font=FONT_SMALL, fill=INK_MUTED)

    card(d, (56, 1374, 1024, 1718), "Generated content")
    chip(d, 96, 1460, "Fresh", TRANSFER, (239, 233, 247))
    d.text((96, 1532), "Target: steady count phrase", font=FONT_BOLD, fill=INK)
    d.text((96, 1592), "Instance id available on reveal", font=FONT_SMALL, fill=INK_MUTED)
    button(d, (56, 1782, 1024, 1890), "Start runtime session", TRAINING)
    im.save(OUT / shot.file_name)


def shot_live_session() -> None:
    shot = Shot("05-live-session.png", "Live", "Runtime-owned phase and commands", "Work")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 650))
    chip(d, 92, 330, "Active work", TRAINING, (229, 242, 238))
    chip(d, 326, 330, "Constraint visible", BLOCKED, (252, 232, 232))
    d.ellipse((388, 428, 692, 732), fill=SURFACE, outline=HAIRLINE, width=22)
    d.arc((388, 428, 692, 732), -90, 145, fill=TRAINING, width=22)
    d.text((540, 580), "01:58", font=FONT_HERO, fill=INK, anchor="mm")

    card(d, (56, 740, 1024, 1148))
    d.text((92, 790), "Target", font=FONT_SMALL_BOLD, fill=INK_MUTED)
    d.text((92, 870), "steady count phrase", font=FONT_HERO, fill=INK)
    d.text((92, 1002), "Drift must be marked. Return inside 10 seconds.", font=FONT_SMALL, fill=INK_MUTED)

    card(d, (56, 1202, 1024, 1590), "Actions")
    button(d, (92, 1288, 468, 1378), "Mark drift", TRAINING)
    button(d, (612, 1288, 988, 1378), "Guess", RECOVERY)
    button(d, (92, 1418, 468, 1508), "Error", BLOCKED)
    button(d, (612, 1418, 988, 1508), "Pause", RECOVERY)

    card(d, (56, 1644, 1024, 1944), "Evidence state")
    evidence_ticks(d, 96, 1730, [("Drift 2", TRAINING, False), ("Guess 0", RECOVERY, False), ("Error 0", BLOCKED, False), ("Cue 0", TEST_READY, False)])
    button(d, (760, 1834, 974, 1914), "Abandon", BLOCKED)
    im.save(OUT / shot.file_name)


def shot_result() -> None:
    shot = Shot("06-result.png", "Result", "Outcome, evidence, next programming action", "Work")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 656))
    chip(d, 92, 330, "Failed", BLOCKED, (252, 232, 232))
    d.text((92, 404), "No advancement", font=FONT_HERO, fill=INK)
    wrap_text(d, "The session completed with an overload failure. Completion is recorded, but it does not pass the standard.", 96, 520, 840, FONT_SMALL, INK_MUTED)

    card(d, (56, 710, 1024, 1128), "Evidence strip")
    evidence_ticks(d, 96, 804, [("Drift 8", BLOCKED, True), ("Return", BLOCKED, False), ("Target", TRAINING, False), ("Time", RECOVERY, False)])
    d.text((96, 920), "Failed threshold: more than 5 marked drifts.", font=FONT_BOLD, fill=BLOCKED)
    d.text((96, 980), "Honesty constraint intact: every drift was marked.", font=FONT_SMALL, fill=INK_MUTED)

    card(d, (56, 1182, 1024, 1640), "Next")
    chip(d, 96, 1268, "Regression", RECOVERY, (239, 242, 244))
    wrap_text(d, "Reduce duration to 90 seconds. Keep the same drift marking and target stability constraint.", 96, 1342, 800, FONT_REG, INK)
    button(d, (616, 1504, 974, 1588), "Open work", TRAINING)
    im.save(OUT / shot.file_name)


def shot_progress() -> None:
    shot = Shot("07-progress-dashboard.png", "Progress", "Local standards-backed progress", "Progress")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 566))
    panel_title(d, 92, 328, "Beginner", "Weekly plan is limited by maintenance.")
    chip(d, 92, 454, "Owned 2", OWNED, (231, 242, 235))
    chip(d, 250, 454, "Passed once 1", PASSED_ONCE, (250, 240, 219))
    chip(d, 506, 454, "Decayed 1", BLOCKED, (252, 232, 232))

    card(d, (56, 620, 1024, 1370), "Branch rails")
    branches = [("FH", "passed"), ("FS", "training"), ("WM", "maintenance"), ("IR", "owned"), ("DE", "decayed"), ("CO", "blocked")]
    yy = 708
    for code, state in branches:
        d.text((96, yy + 24), code, font=FONT_BOLD, fill=INK)
        state_node(d, (178, yy, 304, yy + 78), "L1", state)
        d.line((318, yy + 39, 880, yy + 39), fill=BLOCKED if state in {"decayed", "blocked"} else HAIRLINE, width=5)
        label = {"passed": "1/3 stabilize", "training": "training", "maintenance": "due", "owned": "owned", "decayed": "restore", "blocked": "dependency cap"}[state]
        d.text((900, yy + 18), label, font=FONT_SMALL, fill=BLOCKED if state in {"decayed", "blocked"} else INK_MUTED, anchor="ra")
        yy += 104

    card(d, (56, 1424, 1024, 1948), "Bottlenecks")
    rows = [("WM encoding fidelity", "Maintenance overdue", MAINTENANCE), ("DE audit accuracy", "Decayed prerequisite", BLOCKED), ("FH stabilization", "2 passes required", PASSED_ONCE)]
    yy = 1510
    for title, detail, color in rows:
        chip(d, 96, yy, "Guard", color)
        d.text((256, yy - 2), title, font=FONT_BOLD, fill=INK)
        d.text((256, yy + 50), detail, font=FONT_SMALL, fill=INK_MUTED)
        yy += 130
    im.save(OUT / shot.file_name)


def shot_evidence() -> None:
    shot = Shot("08-evidence-review.png", "Evidence", "Local artifacts and failure visibility", "Evidence")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 492))
    chip(d, 92, 334, "All", TRAINING, (229, 242, 238))
    chip(d, 184, 334, "Fail", BLOCKED, (252, 232, 232))
    chip(d, 292, 334, "Transfer", TRANSFER, (239, 233, 247))
    chip(d, 468, 334, "Maintenance", MAINTENANCE, (255, 247, 229))

    card(d, (56, 546, 1024, 1880), "Timeline")
    entries = [
        ("Jul 5", "FH L1 practice", "Failed set", "drifts=8; overload", BLOCKED),
        ("Jul 4", "FH L1 test", "Pass once", "formal attempt; 1/3", PASSED_ONCE),
        ("Jun 30", "WM L3 check", "Maintenance due", "last current check aging", MAINTENANCE),
        ("Jun 28", "DE L2", "Decayed", "two failed checks", BLOCKED),
        ("Jun 22", "FS L1", "Practice", "cue sequence clean", TRAINING),
    ]
    yy = 636
    for date, title, kind, detail, color in entries:
        d.line((132, yy + 40, 132, yy + 154), fill=HAIRLINE, width=4)
        d.ellipse((110, yy + 18, 154, yy + 62), fill=color)
        d.text((184, yy), date, font=FONT_SMALL_BOLD, fill=INK_MUTED)
        d.text((184, yy + 44), title, font=FONT_BOLD, fill=INK)
        d.text((184, yy + 94), f"{kind} - {detail}", font=FONT_SMALL, fill=INK_MUTED)
        yy += 224
    im.save(OUT / shot.file_name)


def shot_maintenance() -> None:
    shot = Shot("09-maintenance-decay.png", "Maintenance", "Due checks, decay, restoration", "Progress")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 660))
    chip(d, 92, 330, "Decayed", BLOCKED, (252, 232, 232))
    d.text((92, 404), "DE L2 blocks CO", font=FONT_HERO, fill=INK)
    wrap_text(d, "Restore with last owned standard pass plus lower-load transfer check.", 96, 520, 820, FONT_SMALL, INK_MUTED)

    card(d, (56, 714, 1024, 1328), "Maintenance board")
    rows = [("FH L1", "current", OWNED), ("WM L3", "due", MAINTENANCE), ("DE L2", "decayed", BLOCKED), ("IR L1", "current", OWNED)]
    yy = 810
    for label, state, color in rows:
        d.text((96, yy + 20), label, font=FONT_BOLD, fill=INK)
        chip(d, 330, yy, state.title(), color, (252, 232, 232) if color == BLOCKED else SURFACE)
        d.line((96, yy + 74, 984, yy + 74), fill=HAIRLINE, width=2)
        yy += 122

    card(d, (56, 1382, 1024, 1848), "Restoration route")
    d.text((96, 1474), "1", font=FONT_HERO, fill=BLOCKED)
    wrap_text(d, "Pass the last owned DE standard without changing the audit constraint.", 180, 1482, 740, FONT_REG, INK)
    d.text((96, 1638), "2", font=FONT_HERO, fill=BLOCKED)
    wrap_text(d, "Pass one lower-load transfer check before dependent advancement reopens.", 180, 1646, 740, FONT_REG, INK)
    im.save(OUT / shot.file_name)


def shot_review() -> None:
    shot = Shot("10-global-review.png", "Review", "Whole-practitioner decision board", "Progress")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 596))
    chip(d, 92, 330, "Review failed", BLOCKED, (252, 232, 232))
    d.text((92, 406), "Restore bottleneck first", font=FONT_HERO, fill=INK)
    d.text((92, 520), "Advanced classification remains blocked.", font=FONT_SMALL, fill=INK_MUTED)

    card(d, (56, 650, 1024, 1328), "Inputs")
    metrics = [("Owned", "FH, IR"), ("Maintenance", "WM due"), ("Failures", "DE audit x2"), ("Bottleneck", "DE audit accuracy"), ("Recovery", "not active")]
    yy = 740
    for label, value in metrics:
        d.text((96, yy), label, font=FONT_SMALL_BOLD, fill=INK_MUTED)
        d.text((380, yy), value, font=FONT_BOLD, fill=INK)
        yy += 104

    card(d, (56, 1382, 1024, 1854), "Decision")
    rows = [("Restore decayed branch", BLOCKED), ("Pause CO advancement", BLOCKED), ("Run WM maintenance", MAINTENANCE), ("Retest after evidence", TRAINING)]
    yy = 1476
    for label, color in rows:
        chip(d, 96, yy, "Next", color)
        d.text((258, yy + 4), label, font=FONT_BOLD, fill=INK)
        yy += 112
    im.save(OUT / shot.file_name)


def shot_backup() -> None:
    shot = Shot("11-backup-restore.png", "Local", "Local backup and integrity", "Local")
    im, d = new_canvas(shot)
    card(d, (56, 284, 1024, 572))
    chip(d, 92, 330, "Offline", TRAINING, (229, 242, 238))
    chip(d, 226, 330, "No sync", RECOVERY, (239, 242, 244))
    d.text((92, 410), "Local data only", font=FONT_HERO, fill=INK)
    d.text((92, 522), "App-owned JSON plus local backups.", font=FONT_SMALL, fill=INK_MUTED)

    card(d, (56, 626, 1024, 1126), "Integrity")
    d.text((96, 718), "Current local data", font=FONT_BOLD, fill=INK)
    chip(d, 704, 708, "Valid", OWNED, (231, 242, 235))
    d.text((96, 810), "Latest backup", font=FONT_BOLD, fill=INK)
    d.text((96, 866), "mental-gymnastics-local-backup-20260705.json", font=FONT_SMALL, fill=INK_MUTED)
    chip(d, 704, 852, "Readable", TRAINING, (229, 242, 238))
    button(d, (96, 982, 404, 1064), "Validate", TRAINING)
    button(d, (448, 982, 756, 1064), "Export", RECOVERY)

    card(d, (56, 1180, 1024, 1700), "Restore")
    chip(d, 96, 1270, "Replaces local data", BLOCKED, (252, 232, 232))
    wrap_text(d, "Restore runs integrity validation first. It cannot silently corrupt progress state.", 96, 1344, 820, FONT_REG, INK)
    button(d, (96, 1540, 984, 1642), "Restore latest local backup", BLOCKED)
    im.save(OUT / shot.file_name)


def contact_sheet(files: list[str]) -> None:
    thumb_w, thumb_h = 324, 720
    margin = 36
    title_h = 90
    cols = 3
    rows = (len(files) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (thumb_w + margin) + margin, rows * (thumb_h + title_h + margin) + margin), CANVAS)
    d = ImageDraw.Draw(sheet)
    for idx, file_name in enumerate(files):
        image = Image.open(OUT / file_name)
        image.thumbnail((thumb_w, thumb_h))
        col = idx % cols
        row = idx // cols
        x = margin + col * (thumb_w + margin)
        y = margin + row * (thumb_h + title_h + margin)
        d.rounded_rectangle((x - 8, y - 8, x + thumb_w + 8, y + thumb_h + title_h), radius=20, fill=SURFACE, outline=HAIRLINE)
        sheet.paste(image, (x, y))
        label = file_name.removesuffix(".png").replace("-", " ")
        d.text((x, y + thumb_h + 18), label, font=FONT_SMALL, fill=INK)
    sheet.save(OUT / "main-userflow-contact-sheet.png")


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    generators = [
        shot_home,
        shot_ladder,
        shot_branch_detail,
        shot_session_start,
        shot_live_session,
        shot_result,
        shot_progress,
        shot_evidence,
        shot_maintenance,
        shot_review,
        shot_backup,
    ]
    for generator in generators:
        generator()
    files = [
        "01-home-today.png",
        "02-branch-ladder.png",
        "03-branch-detail.png",
        "04-session-start.png",
        "05-live-session.png",
        "06-result.png",
        "07-progress-dashboard.png",
        "08-evidence-review.png",
        "09-maintenance-decay.png",
        "10-global-review.png",
        "11-backup-restore.png",
    ]
    contact_sheet(files)
    print(f"Generated {len(files)} screenshots plus contact sheet in {OUT}")


if __name__ == "__main__":
    main()
