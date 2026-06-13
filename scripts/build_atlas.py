#!/usr/bin/env python3
"""Build block texture atlas from base_textures/ for Autonocraft."""

from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "Autonocraft"
TEXTURES = SRC / "base_textures"
OUTPUT = SRC / "atlas.png"

COLS = 8
ROWS = 8
TILE = 128
WIDTH = COLS * TILE
HEIGHT = ROWS * TILE

# Row-major tile placement (col, row) -> source filename
LAYOUT = {
    (0, 0): "grass_top.png",
    (1, 0): "grass_side.png",
    (2, 0): "oak_log.png",
    (3, 0): "stone.png",
    (0, 1): "dirt.png",
    (1, 1): "oak_leaves.png",
    (2, 1): "birch_log.png",
    (3, 1): "birch_leaves.png",
    (0, 2): "pine_log.png",
    (1, 2): "pine_leaves.png",
    (2, 2): "water.png",
    (3, 2): "sand.png",
    (0, 3): "snow.png",
    (1, 3): "gravel.png",
    (2, 3): "coal_ore.png",
    (3, 3): "iron_ore.png",
    (0, 4): "gold_ore.png",
    (1, 4): "tall_grass.png",
    (2, 4): "flower.png",
    (3, 4): "cactus_top.png",
    (2, 5): "cactus_side.png",
    (2, 2): "sheep_body.png",
    (3, 2): "sheep_head.png",
    (0, 3): "pig_body.png",
    (1, 3): "pig_head.png",
    (2, 3): "chicken_body.png",
    (3, 3): "chicken_head.png",
}

PLACEHOLDER_COLORS = {
    "grass_top.png": (72, 150, 58),
    "grass_side.png": (88, 120, 58),
    "oak_log.png": (96, 72, 48),
    "stone.png": (120, 120, 124),
    "dirt.png": (110, 82, 52),
    "oak_leaves.png": (52, 120, 48),
    "birch_log.png": (210, 205, 188),
    "birch_leaves.png": (72, 140, 62),
    "pine_log.png": (78, 58, 36),
    "pine_leaves.png": (34, 92, 48),
    "water.png": (42, 92, 180),
    "sand.png": (210, 198, 140),
    "snow.png": (236, 240, 244),
    "gravel.png": (140, 138, 132),
    "coal_ore.png": (70, 70, 74),
    "iron_ore.png": (150, 126, 108),
    "gold_ore.png": (188, 164, 72),
    "tall_grass.png": (88, 150, 58),
    "flower.png": (220, 92, 120),
    "cactus_top.png": (72, 140, 62),
    "cactus_side.png": (62, 128, 54),
    "sheep_body.png": (220, 220, 220),
    "sheep_head.png": (210, 210, 210),
    "pig_body.png": (240, 170, 170),
    "pig_head.png": (230, 160, 160),
    "chicken_body.png": (240, 220, 120),
    "chicken_head.png": (230, 210, 110),
}


def is_background_pixel(r: int, g: int, b: int) -> bool:
    return r > 230 and g > 230 and b > 230


def is_pale_fringe_pixel(r: int, g: int, b: int) -> bool:
    return is_background_pixel(r, g, b) or (g > 180 and r > 150 and b > 150)


def find_grass_fringe_start(img: Image.Image) -> int:
    pixels = img.load()
    width, height = img.size
    for y in range(height):
        greenish = 0
        for x in range(0, width, 8):
            r, g, b, a = pixels[x, y]
            if g > r + 10 and g > b + 10 and g > 50 and not is_pale_fringe_pixel(r, g, b):
                greenish += 1
        if greenish >= width // 64:
            return y
    return height // 4


def compose_grass_side(dirt: Image.Image, grass_side: Image.Image, tile: int) -> Image.Image:
    dirt_tile = dirt.convert("RGBA").resize((tile, tile), Image.Resampling.NEAREST)
    source = grass_side.convert("RGBA")
    start_y = find_grass_fringe_start(source)
    crop_bottom = int(source.height * 0.58)
    cropped = source.crop((0, start_y, source.width, crop_bottom))
    fringe_height = max(1, int(tile * 0.36))
    fringe = cropped.resize((tile, fringe_height), Image.Resampling.NEAREST)
    result = dirt_tile.copy()
    result.paste(fringe, (0, 0))
    return result


def make_placeholder(name: str, tile: int) -> Image.Image:
    color = PLACEHOLDER_COLORS.get(name, (180, 80, 180))
    img = Image.new("RGBA", (tile, tile), color + (255,))
    draw = ImageDraw.Draw(img)
    draw.rectangle((4, 4, tile - 5, tile - 5), outline=(0, 0, 0, 80), width=2)
    return img


def load_tile(name: str) -> Image.Image:
    path = TEXTURES / name
    if path.exists():
        img = Image.open(path).convert("RGBA")
        if name == "grass_side.png" and (TEXTURES / "dirt.png").exists():
            dirt = Image.open(TEXTURES / "dirt.png")
            img = compose_grass_side(dirt, img, TILE)
        else:
            img = img.resize((TILE, TILE), Image.Resampling.NEAREST)
        return img
    return make_placeholder(name, TILE)


def main() -> None:
    TEXTURES.mkdir(parents=True, exist_ok=True)
    atlas = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 255))

    # Animal tiles keep legacy positions when present; world tiles use dedicated slots.
    world_layout = {
        (0, 0): "grass_top.png",
        (1, 0): "grass_side.png",
        (2, 0): "oak_log.png",
        (3, 0): "stone.png",
        (0, 1): "dirt.png",
        (1, 1): "oak_leaves.png",
        (2, 1): "birch_log.png",
        (3, 1): "birch_leaves.png",
        (0, 2): "pine_log.png",
        (1, 2): "pine_leaves.png",
        (2, 2): "water.png",
        (3, 2): "sand.png",
        (0, 3): "snow.png",
        (1, 3): "gravel.png",
        (2, 3): "coal_ore.png",
        (3, 3): "iron_ore.png",
        (0, 4): "gold_ore.png",
        (1, 4): "tall_grass.png",
        (2, 4): "flower.png",
        (3, 4): "cactus_top.png",
        (2, 5): "cactus_side.png",
        (4, 2): "sheep_body.png",
        (5, 2): "sheep_head.png",
        (4, 3): "pig_body.png",
        (5, 3): "pig_head.png",
        (4, 4): "chicken_body.png",
        (5, 4): "chicken_head.png",
    }

    for (col, row), filename in world_layout.items():
        tile = load_tile(filename)
        atlas.paste(tile, (col * TILE, row * TILE))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(OUTPUT, format="PNG", optimize=True)
    print(f"Wrote {OUTPUT} ({WIDTH}x{HEIGHT}, {COLS}x{ROWS} tiles @ {TILE}px)")


if __name__ == "__main__":
    main()
