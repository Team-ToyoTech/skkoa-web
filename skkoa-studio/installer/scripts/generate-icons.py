from __future__ import annotations

import base64
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "image" / "skkoa_logo_remove_background_large.png"
EDITOR_ICONS = ROOT / "skkoa-studio" / "editor" / "assets" / "icons"
INSTALLER_ICONS = ROOT / "skkoa-studio" / "installer" / "assets" / "icons"
INSTALLER_WIZARD = ROOT / "skkoa-studio" / "installer" / "assets" / "wizard"
SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
PRIMARY = "#a259ff"
DARK_BACKGROUND = "#111111"
DARK_SURFACE = "#1b1b1f"
DARK_BORDER = "#33333a"


def fit_icon(source: Image.Image, size: int) -> Image.Image:
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    margin = max(1, int(size * 0.04))
    image = source.copy()
    image.thumbnail((size - margin * 2, size - margin * 2), Image.Resampling.LANCZOS)
    x = (size - image.width) // 2
    y = (size - image.height) // 2
    canvas.alpha_composite(image, (x, y))
    return canvas


def add_download_badge(icon: Image.Image) -> Image.Image:
    result = icon.copy()
    draw = ImageDraw.Draw(result)
    size = result.width
    badge = int(size * 0.28)
    pad = max(1, int(size * 0.04))
    x0 = size - badge - pad
    y0 = size - badge - pad
    x1 = size - pad
    y1 = size - pad
    radius = max(2, int(badge * 0.22))
    draw.rounded_rectangle((x0, y0, x1, y1), radius=radius, fill=(162, 89, 255, 235))
    cx = (x0 + x1) // 2
    top = y0 + int(badge * 0.20)
    bottom = y0 + int(badge * 0.62)
    line_width = max(1, int(size * 0.035))
    draw.line((cx, top, cx, bottom), fill=(255, 255, 255, 255), width=line_width)
    arrow = max(2, int(badge * 0.18))
    draw.polygon(
        [(cx - arrow, bottom - arrow), (cx + arrow, bottom - arrow), (cx, bottom + arrow)],
        fill=(255, 255, 255, 255),
    )
    tray_y = y0 + int(badge * 0.78)
    draw.line((x0 + int(badge * 0.25), tray_y, x1 - int(badge * 0.25), tray_y), fill=(255, 255, 255, 255), width=line_width)
    return result


def write_svg(path: Path, png_path: Path, installer: bool) -> None:
    encoded = base64.b64encode(png_path.read_bytes()).decode("ascii")
    overlay = ""
    if installer:
        overlay = f"""
  <rect x="178" y="178" width="66" height="66" rx="14" fill="{PRIMARY}" opacity="0.94"/>
  <path d="M211 191v31m-13-12 13 14 13-14m-24 22h22" fill="none" stroke="#ffffff" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>
"""
    path.write_text(
        f"""<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <image width="256" height="256" href="data:image/png;base64,{encoded}" preserveAspectRatio="xMidYMid meet"/>
{overlay}</svg>
""",
        encoding="utf-8",
    )


def write_wizard_bitmaps(source: Image.Image) -> None:
    INSTALLER_WIZARD.mkdir(parents=True, exist_ok=True)

    sidebar = Image.new("RGB", (164, 314), DARK_BACKGROUND)
    draw = ImageDraw.Draw(sidebar)
    draw.rectangle((0, 0, 6, sidebar.height), fill=PRIMARY)
    draw.rectangle((18, 18, 146, 296), outline=DARK_BORDER, width=2)
    draw.rectangle((22, 22, 142, 292), fill=DARK_SURFACE)
    logo = fit_icon(source, 92)
    sidebar.paste(logo.convert("RGB"), ((sidebar.width - logo.width) // 2, 68), logo)
    draw.rectangle((42, 202, 122, 206), fill=PRIMARY)
    draw.rectangle((54, 222, 110, 225), fill=(242, 242, 242))
    draw.rectangle((64, 236, 100, 239), fill=(169, 169, 179))
    sidebar.save(INSTALLER_WIZARD / "skkoa-wizard.bmp")

    small = Image.new("RGB", (55, 55), DARK_BACKGROUND)
    small_draw = ImageDraw.Draw(small)
    small_draw.rectangle((0, 0, 54, 54), outline=DARK_BORDER, width=1)
    small_draw.rectangle((0, 0, 4, 54), fill=PRIMARY)
    small_logo = fit_icon(source, 42)
    small.paste(small_logo.convert("RGB"), (8, 6), small_logo)
    small.save(INSTALLER_WIZARD / "skkoa-wizard-small.bmp")


def main() -> None:
    if not SOURCE.exists():
        raise SystemExit(f"Source icon not found: {SOURCE}")

    EDITOR_ICONS.mkdir(parents=True, exist_ok=True)
    INSTALLER_ICONS.mkdir(parents=True, exist_ok=True)

    source = Image.open(SOURCE).convert("RGBA")
    editor_images = [fit_icon(source, size[0]) for size in SIZES]
    installer_images = [add_download_badge(image) for image in editor_images]

    editor_png = EDITOR_ICONS / "skkoa-source.png"
    source.save(editor_png)
    write_svg(EDITOR_ICONS / "skkoa.svg", editor_png, installer=False)
    editor_images[-1].save(EDITOR_ICONS / "skkoa.ico", sizes=SIZES)
    editor_images[-1].save(EDITOR_ICONS / "skkoa-file.ico", sizes=SIZES)

    installer_png = INSTALLER_ICONS / "skkoa-installer-source.png"
    installer_images[-1].save(installer_png)
    write_svg(INSTALLER_ICONS / "skkoa-installer.svg", installer_png, installer=True)
    installer_images[-1].save(INSTALLER_ICONS / "skkoa-installer.ico", sizes=SIZES)
    write_wizard_bitmaps(source)

    (EDITOR_ICONS / "README.md").write_text(
        "SKKOA Studio icons are generated from `image/skkoa_logo_remove_background_large.png`. "
        "No standalone SVG original was present in the repository, so `skkoa.svg` embeds that source PNG. "
        "Replace the PNG and rerun `skkoa-studio/installer/scripts/generate-icons.py` to regenerate ICO assets.\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
