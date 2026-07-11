from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
RESOURCES = ROOT / "src" / "MentalGymnastics.Android" / "Resources"

DENSITIES = {
    "mdpi": (48, 108),
    "hdpi": (72, 162),
    "xhdpi": (96, 216),
    "xxhdpi": (144, 324),
    "xxxhdpi": (192, 432),
}

BG_TOP = (22, 132, 118, 255)
BG_BOTTOM = (14, 98, 89, 255)
BG_DEEP = (10, 67, 65, 255)
INK = (253, 254, 252, 255)
ACCENT = (161, 227, 214, 255)


def lerp(a: int, b: int, t: float) -> int:
    return round(a + (b - a) * t)


def make_background(size: int, rounded: bool) -> Image.Image:
    image = Image.new("RGBA", (size, size), BG_BOTTOM)
    pixels = image.load()
    for y in range(size):
        t = y / max(1, size - 1)
        for x in range(size):
            diagonal = max(0, (x - y) / size) * 0.16
            mix = min(1, t * 0.9 + diagonal)
            pixels[x, y] = tuple(lerp(BG_TOP[i], BG_BOTTOM[i], mix) for i in range(4))

    overlay = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    band_width = max(2, size // 24)
    draw.line(
        [(round(size * 0.05), round(size * 0.86)), (round(size * 0.88), round(size * 0.03))],
        fill=(255, 255, 255, 18),
        width=band_width)
    draw.line(
        [(round(size * 0.23), size), (size, round(size * 0.23))],
        fill=(0, 0, 0, 22),
        width=band_width)
    image.alpha_composite(overlay)

    if rounded:
        mask = Image.new("L", (size, size), 0)
        mask_draw = ImageDraw.Draw(mask)
        radius = round(size * 0.20)
        mask_draw.rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
        image.putalpha(mask)

    return image


def scaled_points(points: list[tuple[float, float]], size: int) -> list[tuple[int, int]]:
    return [(round(x * size / 432), round(y * size / 432)) for x, y in points]


def draw_round_line(
    draw: ImageDraw.ImageDraw,
    points: list[tuple[int, int]],
    fill: tuple[int, int, int, int],
    width: int) -> None:
    draw.line(points, fill=fill, width=width, joint="curve")
    radius = width / 2
    for x, y in points:
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=fill)


def make_foreground(size: int) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow)

    points = scaled_points(
        [
            (132, 292),
            (132, 154),
            (216, 238),
            (300, 154),
            (300, 292),
        ],
        size)
    stroke = max(4, round(size * 0.090))
    shadow_points = [(x, y + round(size * 0.020)) for x, y in points]
    draw_round_line(shadow_draw, shadow_points, (0, 0, 0, 70), stroke)

    target_center = (round(size * 0.5), round(size * 0.742))
    target_radius = round(size * 0.061)
    shadow_draw.ellipse(
        (
            target_center[0] - target_radius,
            target_center[1] - target_radius + round(size * 0.020),
            target_center[0] + target_radius,
            target_center[1] + target_radius + round(size * 0.020),
        ),
        fill=(0, 0, 0, 58))
    shadow = shadow.filter(ImageFilter.GaussianBlur(max(1, round(size * 0.010))))
    image.alpha_composite(shadow)

    draw = ImageDraw.Draw(image)
    draw_round_line(draw, points, INK, stroke)
    ring_width = max(2, round(size * 0.021))
    draw.ellipse(
        (
            target_center[0] - target_radius,
            target_center[1] - target_radius,
            target_center[0] + target_radius,
            target_center[1] + target_radius,
        ),
        outline=ACCENT,
        width=ring_width)
    dot_radius = max(2, round(size * 0.018))
    draw.ellipse(
        (
            target_center[0] - dot_radius,
            target_center[1] - dot_radius,
            target_center[0] + dot_radius,
            target_center[1] + dot_radius,
        ),
        fill=INK)

    return image


def resize(image: Image.Image, size: int) -> Image.Image:
    return image.resize((size, size), Image.Resampling.LANCZOS)


def main() -> None:
    master_background = make_background(432, rounded=False)
    master_foreground = make_foreground(432)

    for density, (legacy_size, adaptive_size) in DENSITIES.items():
        folder = RESOURCES / f"mipmap-{density}"
        folder.mkdir(parents=True, exist_ok=True)

        background = resize(master_background, adaptive_size)
        foreground = resize(master_foreground, adaptive_size)
        background.save(folder / "appicon_background.png")
        foreground.save(folder / "appicon_foreground.png")

        legacy = resize(make_background(432, rounded=True), legacy_size)
        legacy.alpha_composite(resize(master_foreground, legacy_size))
        legacy.save(folder / "appicon.png")


if __name__ == "__main__":
    main()
