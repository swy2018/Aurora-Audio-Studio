from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "AuroraAudioStudio" / "Assets"
MASTER = ASSETS / "AuroraIcon.png"
SOURCE = ASSETS / "AuroraIconSource.png"
DOCS_ICON = ROOT.parent.parent / "docs" / "assets" / "aurora-icon.png"


def teal_mask(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    mask = Image.new("L", rgba.size)
    out = mask.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            red, green, blue, alpha = pixels[x, y]
            is_subject_area = 105 < x < 920 and 120 < y < 900
            if is_subject_area and alpha > 20 and green > 72 and blue > 66 and green > red * 1.28 and blue > red * 1.18:
                out[x, y] = alpha
    return mask


def gradient(size: tuple[int, int]) -> Image.Image:
    width, height = size
    result = Image.new("RGBA", size)
    draw = ImageDraw.Draw(result)
    top = (20, 229, 208)
    bottom = (0, 174, 159)
    for y in range(height):
        ratio = y / max(1, height - 1)
        color = tuple(round(top[i] * (1 - ratio) + bottom[i] * ratio) for i in range(3)) + (255,)
        draw.line((0, y, width, y), fill=color)
    return result


def largest_component(mask: Image.Image) -> Image.Image:
    source = mask.load()
    visited: set[tuple[int, int]] = set()
    largest: list[tuple[int, int]] = []
    for start_y in range(mask.height):
        for start_x in range(mask.width):
            start = (start_x, start_y)
            if start in visited or source[start_x, start_y] < 48:
                continue
            queue = deque([start])
            visited.add(start)
            component: list[tuple[int, int]] = []
            while queue:
                x, y = queue.popleft()
                component.append((x, y))
                for next_x, next_y in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    point = (next_x, next_y)
                    if 0 <= next_x < mask.width and 0 <= next_y < mask.height and point not in visited and source[next_x, next_y] >= 48:
                        visited.add(point)
                        queue.append(point)
            if len(component) > len(largest):
                largest = component
    result = Image.new("L", mask.size)
    output = result.load()
    for x, y in largest:
        output[x, y] = source[x, y]
    return result


def remove_original_waveform(source: Image.Image) -> Image.Image:
    result = source.copy()
    box = (335, 565, 690, 785)
    texture = source.crop((75, 250, 300, 470)).resize((box[2] - box[0], box[3] - box[1]), Image.Resampling.LANCZOS)
    replacement = source.copy()
    replacement.paste(texture, box)
    mask = Image.new("L", source.size)
    ImageDraw.Draw(mask).rounded_rectangle(box, radius=28, fill=255)
    return Image.composite(replacement, source, mask.filter(ImageFilter.GaussianBlur(9)))


def draw_waveform(size: tuple[int, int]) -> Image.Image:
    mask = Image.new("L", size)
    draw = ImageDraw.Draw(mask)
    heights = [26, 42, 58, 74, 50, 66, 38, 60, 86, 64, 46, 34, 24]
    bar_width = 12
    gap = 8
    total_width = len(heights) * bar_width + (len(heights) - 1) * gap
    start_x = (size[0] - total_width) // 2
    center_y = 684
    for index, height in enumerate(heights):
        x = start_x + index * (bar_width + gap)
        draw.rounded_rectangle((x, center_y - height // 2, x + bar_width, center_y + height // 2), radius=6, fill=255)
    return mask


def build_master(source: Image.Image) -> Image.Image:
    source = source.convert("RGBA")
    mask = teal_mask(source)
    source = remove_original_waveform(source)
    a_mask = largest_component(mask).filter(ImageFilter.MaxFilter(15))
    wave_mask = draw_waveform(source.size)

    bold_mask = ImageChops.lighter(a_mask, wave_mask)
    bold_mask = ImageEnhance.Contrast(bold_mask).enhance(1.12)
    colored = gradient(source.size)
    result = Image.composite(colored, source, bold_mask)

    rounded = Image.new("L", source.size)
    ImageDraw.Draw(rounded).rounded_rectangle((36, 36, 987, 987), radius=205, fill=255)
    result.putalpha(ImageChops.darker(result.getchannel("A"), rounded))
    return result


def resized(master: Image.Image, size: tuple[int, int]) -> Image.Image:
    image = master.resize(size, Image.Resampling.LANCZOS)
    if min(size) <= 88:
        image = image.filter(ImageFilter.UnsharpMask(radius=0.7, percent=165, threshold=2))
    return image


def centered(master: Image.Image, canvas_size: tuple[int, int], icon_size: int) -> Image.Image:
    canvas = Image.new("RGBA", canvas_size)
    icon = resized(master, (icon_size, icon_size))
    canvas.alpha_composite(icon, ((canvas_size[0] - icon_size) // 2, (canvas_size[1] - icon_size) // 2))
    return canvas


def main() -> None:
    master = build_master(Image.open(SOURCE))
    master.save(MASTER, optimize=True)
    resized(master, (256, 256)).save(ASSETS / "AppIcon.ico", format="ICO", sizes=[
        (16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)
    ], bitmap_format="png")

    for name, size in {
        "LockScreenLogo.scale-200.png": (48, 48),
        "Square150x150Logo.scale-200.png": (300, 300),
        "Square44x44Logo.scale-200.png": (88, 88),
        "Square44x44Logo.targetsize-24_altform-unplated.png": (24, 24),
        "Square44x44Logo.targetsize-48_altform-lightunplated.png": (48, 48),
        "StoreLogo.png": (50, 50),
    }.items():
        resized(master, size).save(ASSETS / name, optimize=True)

    centered(master, (620, 300), 240).save(ASSETS / "Wide310x150Logo.scale-200.png", optimize=True)
    centered(master, (1240, 600), 390).save(ASSETS / "SplashScreen.scale-200.png", optimize=True)
    resized(master, (256, 256)).save(DOCS_ICON, optimize=True)


if __name__ == "__main__":
    main()
