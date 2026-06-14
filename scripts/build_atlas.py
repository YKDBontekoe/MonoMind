#!/usr/bin/env python3
"""Build block texture atlas procedurally for Autonocraft."""

import json
from pathlib import Path
from typing import Optional
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "Autonocraft"
TEXTURES = SRC / "base_textures"
LAYOUT_PATH = SRC / "atlas_layout.json"
OUTPUT = SRC / "atlas.png"

# Integer sin/cos approximations (* 1000) for 30-degree steps — cross-platform deterministic.
_DIRECTION_12_COS = (1000, 866, 500, 0, -500, -866, -1000, -866, -500, 0, 500, 866)
_DIRECTION_12_SIN = (0, 500, 866, 1000, 866, 500, 0, -500, -866, -1000, -866, -500)


def radial_offset(cx: int, cy: int, radius_x: int, radius_y: int, step_index: int) -> tuple[int, int]:
    """Return an endpoint using fixed integer trig tables."""
    cos_v = _DIRECTION_12_COS[step_index % 12]
    sin_v = _DIRECTION_12_SIN[step_index % 12]
    x2 = cx + (cos_v * radius_x) // 1000
    y2 = cy + (sin_v * radius_y) // 1000
    return x2, y2


def load_layout() -> dict:
    with LAYOUT_PATH.open(encoding="utf-8") as handle:
        return json.load(handle)


def shade(color: tuple[int, int, int], amount: int) -> tuple[int, int, int]:
    return tuple(max(0, min(255, c + amount)) for c in color)


def lighten(color: tuple[int, int, int], amount: int) -> tuple[int, int, int]:
    return shade(color, amount)


def darken(color: tuple[int, int, int], amount: int) -> tuple[int, int, int]:
    return shade(color, -amount)


def lerp_color(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    t = max(0.0, min(1.0, t))
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def noise_value(name: str, x: int, y: int, salt: int = 0) -> int:
    seed = sum((i + 1) * ord(ch) for i, ch in enumerate(name)) + salt * 131
    value = x * 374761393 + y * 668265263 + seed * 2246822519
    value = (value ^ (value >> 13)) * 1274126177
    return (value ^ (value >> 16)) & 255


def fill_pixel_cluster_tile(
    name: str,
    tile: int,
    base: tuple[int, int, int],
    palette: list[tuple[int, int, int]],
    cell: int = 8,
    variation: int = 18,
) -> Image.Image:
    colors = palette or [base]
    img = Image.new("RGBA", (tile, tile), base + (255,))
    pixels = img.load()
    for cy in range(0, tile, cell):
        for cx in range(0, tile, cell):
            pick = noise_value(name, cx // cell, cy // cell) % len(colors)
            cell_color = colors[pick]
            accent = noise_value(name, cx // cell, cy // cell, 11) % 6
            if accent == 0:
                cell_color = shade(cell_color, variation)
            elif accent == 1:
                cell_color = shade(cell_color, -variation // 2)
            for y in range(cy, min(tile, cy + cell)):
                for x in range(cx, min(tile, cx + cell)):
                    fine = noise_value(name, x, y, 19) % 9
                    pixel = cell_color
                    if fine == 0:
                        pixel = shade(cell_color, 10)
                    elif fine == 1:
                        pixel = shade(cell_color, -8)
                    pixels[x, y] = pixel + (255,)
    return img


def fill_noisy_tile(name: str, tile: int, base: tuple[int, int, int], variation: int) -> Image.Image:
    return fill_pixel_cluster_tile(name, tile, base, [base], 8, variation)


def scatter(
    draw: ImageDraw.ImageDraw,
    name: str,
    tile: int,
    colors: list[tuple[int, int, int]],
    count: int,
    size: int,
) -> None:
    for i in range(count):
        x = noise_value(name, i, 17, 3) % tile
        y = noise_value(name, i, 31, 5) % tile
        color = colors[noise_value(name, i, 47, 9) % len(colors)]
        draw.rectangle((x, y, min(tile - 1, x + size), min(tile - 1, y + size)), fill=color + (255,))


def vertical_stripe_tile(
    name: str,
    tile: int,
    base: tuple[int, int, int],
    stripe: tuple[int, int, int],
    light_stripe: tuple[int, int, int],
    stripe_spacing: int,
) -> Image.Image:
    img = fill_noisy_tile(name, tile, base, 14)
    draw = ImageDraw.Draw(img)
    for x in range(0, tile, stripe_spacing):
        draw.line((x, 0, x, tile), fill=stripe + (255,), width=3)
        draw.line((x + 2, 0, x + 2, tile), fill=light_stripe + (255,), width=1)
    for y in range(8, tile, 24):
        offset = noise_value(name, y, 0) % 6
        draw.line((offset, y, tile - 1, y), fill=shade(base, -18) + (255,), width=2)
    return img


def leaf_cluster_tile(
    name: str,
    tile: int,
    base: tuple[int, int, int],
    clusters: list[tuple[int, int, int]],
) -> Image.Image:
    palette = clusters + [shade(base, -16), shade(base, 18)]
    img = fill_pixel_cluster_tile(name, tile, shade(base, -12), palette, 6, 16)
    scatter(ImageDraw.Draw(img), name, tile, clusters, 90, 5)
    scatter(ImageDraw.Draw(img), name + "_hi", tile, [shade(c, 32) for c in clusters], 38, 3)
    scatter(ImageDraw.Draw(img), name + "_dk", tile, [shade(c, -28) for c in clusters], 22, 4)
    return img


def compose_grass_side(dirt: Image.Image, grass_fringe: Image.Image, tile: int) -> Image.Image:
    dirt_tile = dirt.convert("RGBA").resize((tile, tile), Image.Resampling.NEAREST)
    fringe_height = max(1, int(tile * 0.36))
    fringe = grass_fringe.convert("RGBA").resize((tile, fringe_height), Image.Resampling.NEAREST)
    result = dirt_tile.copy()
    result.paste(fringe, (0, 0))
    return result


def make_animal_tile(name: str, tile: int, base: tuple[int, int, int], accent: tuple[int, int, int]) -> Image.Image:
    img = fill_noisy_tile(name, tile, base, 8)
    draw = ImageDraw.Draw(img)
    margin = tile // 6
    draw.rectangle((margin, margin, tile - margin, tile - margin), outline=shade(accent, -30) + (255,), width=3)
    scatter(draw, name, tile, [accent, shade(accent, 20)], 12, 2)
    return img


def make_procedural_tile(name: str, tile: int) -> Optional[Image.Image]:
    if name == "grass_top.png":
        palette = [(38, 92, 34), (50, 114, 42), (60, 128, 48), (32, 82, 30), (70, 138, 52)]
        img = fill_pixel_cluster_tile(name, tile, (52, 118, 44), palette, 8, 14)
        scatter(ImageDraw.Draw(img), name, tile, palette, 28, 2)
        return img

    if name == "dirt.png":
        palette = [(92, 68, 42), (110, 82, 52), (128, 96, 62), (84, 60, 38)]
        img = fill_pixel_cluster_tile(name, tile, (110, 82, 52), palette, 8, 22)
        scatter(ImageDraw.Draw(img), name, tile, palette, 28, 3)
        return img

    if name == "grass_side.png":
        dirt = make_procedural_tile("dirt.png", tile)
        fringe = fill_noisy_tile(name + "_fringe", tile, (68, 104, 48), 14)
        scatter(ImageDraw.Draw(fringe), name, tile, [(46, 106, 40), (68, 134, 50)], 30, 2)
        if dirt is None:
            return fringe
        return compose_grass_side(dirt, fringe, tile)

    if name == "stone.png":
        palette = [(98, 98, 102), (120, 120, 124), (142, 142, 146), (108, 108, 112), (88, 88, 92)]
        img = fill_pixel_cluster_tile(name, tile, (120, 120, 124), palette, 8, 26)
        scatter(ImageDraw.Draw(img), name, tile, palette, 55, 4)
        draw = ImageDraw.Draw(img)
        for i in range(8):
            x0 = noise_value(name, i, 3, 17) % (tile - 24)
            y0 = noise_value(name, i, 7, 19) % (tile - 24)
            draw.line((x0, y0, x0 + 18, y0 + 2), fill=shade(palette[0], -22) + (255,), width=2)
        return img

    if name == "oak_log.png":
        return vertical_stripe_tile(name, tile, (96, 72, 48), (72, 52, 34), (118, 92, 62), 14)

    if name == "birch_log.png":
        img = vertical_stripe_tile(name, tile, (210, 205, 188), (188, 182, 168), (232, 228, 214), 16)
        draw = ImageDraw.Draw(img)
        for i in range(14):
            x = noise_value(name, i, 3, 11) % tile
            y = noise_value(name, i, 7, 13) % tile
            draw.rectangle((x, y, x + 3, y + 8), fill=(48, 44, 40, 255))
        return img

    if name == "pine_log.png":
        return vertical_stripe_tile(name, tile, (78, 58, 36), (58, 42, 26), (98, 74, 48), 12)

    if name == "oak_leaves.png":
        return leaf_cluster_tile(name, tile, (52, 120, 48), [(42, 98, 38), (68, 142, 58), (34, 88, 32)])

    if name == "birch_leaves.png":
        return leaf_cluster_tile(name, tile, (72, 140, 62), [(58, 124, 52), (88, 158, 72), (48, 108, 44)])

    if name == "pine_leaves.png":
        return leaf_cluster_tile(name, tile, (34, 92, 48), [(24, 78, 38), (48, 108, 56), (18, 68, 32)])

    if name == "water.png":
        deep = (26, 68, 142)
        mid = (40, 104, 186)
        shallow = (56, 136, 212)
        highlight = (148, 208, 248)
        img = Image.new("RGBA", (tile, tile))
        px = img.load()
        for y in range(tile):
            for x in range(tile):
                n0 = noise_value(name, x // 3, y // 3, 1)
                n1 = noise_value(name, x // 5, y // 5, 7)
                n2 = noise_value(name, x // 2, y // 2, 13)
                n3 = noise_value(name, x // 7, y // 4, 3)
                blend = ((n0 & 255) / 255.0 * 0.55) + ((n3 & 255) / 255.0 * 0.45)
                base = lerp_color(lerp_color(deep, mid, blend), shallow, blend * blend)
                base = lerp_color(base, lighten(base, 14), (n2 & 15) / 15.0 * 0.28)
                base = lerp_color(base, darken(base, 8), ((n1 & 7) / 7.0) * 0.22)
                caustic = (noise_value(name, x, y, 31) * noise_value(name, y, x, 37)) / (255.0 * 255.0)
                if caustic > 0.68:
                    base = lerp_color(base, highlight, (caustic - 0.68) * 1.5)
                px[x, y] = base + (255,)
        return img

    if name == "water_side.png":
        deep = (16, 48, 104)
        mid = (24, 72, 138)
        shallow = (34, 88, 154)
        img = Image.new("RGBA", (tile, tile))
        px = img.load()
        for y in range(tile):
            for x in range(tile):
                n0 = noise_value(name, x // 4, y // 5, 11)
                n1 = noise_value(name, x // 6, y // 3, 19)
                blend = ((n0 & 255) / 255.0 * 0.6) + ((n1 & 255) / 255.0 * 0.4)
                base = lerp_color(lerp_color(deep, mid, blend), shallow, blend * 0.35)
                if (n0 & 7) == 0:
                    base = lighten(base, 5)
                elif (n0 & 7) == 1:
                    base = darken(base, 6)
                px[x, y] = base + (255,)
        return img

    if name == "sand.png":
        img = fill_noisy_tile(name, tile, (210, 196, 132), 20)
        scatter(ImageDraw.Draw(img), name, tile, [(184, 168, 105), (232, 218, 158)], 95, 2)
        return img

    if name == "snow.png":
        img = fill_noisy_tile(name, tile, (232, 238, 242), 10)
        draw = ImageDraw.Draw(img)
        draw.line((0, tile // 3, tile, tile // 3 - 8), fill=(208, 220, 228, 255), width=2)
        draw.line((0, tile * 2 // 3, tile, tile * 2 // 3 + 5), fill=(246, 250, 252, 255), width=3)
        return img

    if name == "gravel.png":
        img = fill_noisy_tile(name, tile, (128, 126, 120), 36)
        scatter(ImageDraw.Draw(img), name, tile, [(88, 88, 84), (160, 158, 150), (108, 106, 102)], 150, 4)
        return img

    ore_colors = {
        "coal_ore.png": (42, 42, 44),
        "iron_ore.png": (188, 132, 86),
        "gold_ore.png": (224, 184, 62),
    }
    if name in ore_colors:
        img = fill_noisy_tile(name, tile, (112, 112, 116), 22)
        draw = ImageDraw.Draw(img)
        for i in range(18):
            x = noise_value(name, i, 23, 11) % (tile - 12)
            y = noise_value(name, i, 37, 13) % (tile - 12)
            color = ore_colors[name]
            draw.rectangle((x, y, x + 5, y + 5), fill=color + (255,))
            draw.point((x + 1, y + 1), fill=shade(color, 34) + (255,))
        return img

    if name == "cactus.png":
        img = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)
        cx = tile // 2
        body_w = max(6, tile // 5)
        top = tile // 10
        bottom = tile - 6
        draw.rectangle((cx - body_w // 2, top + body_w // 2, cx + body_w // 2, bottom), fill=(58, 132, 52, 255))
        draw.ellipse((cx - body_w // 2, top, cx + body_w // 2, top + body_w), fill=(58, 132, 52, 255))
        for x in range(cx - body_w // 2, cx + body_w // 2 + 1, max(2, body_w // 3)):
            draw.line((x, top + 4, x, bottom - 4), fill=(42, 104, 46, 255), width=1)
        draw.line((cx - body_w // 2 - 1, top + body_w // 2, cx - body_w // 2 - 1, bottom - 4), fill=(78, 154, 76, 255), width=1)
        draw.line((cx + body_w // 2 + 1, top + body_w // 2, cx + body_w // 2 + 1, bottom - 4), fill=(78, 154, 76, 255), width=1)
        scatter(draw, name, tile, [(228, 236, 214)], 16, 1)
        return img

    if name == "tall_grass.png":
        img = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)
        for i in range(28):
            x = noise_value(name, i, 11, 17) % tile
            y0 = tile - 2
            y1 = tile // 5 + noise_value(name, i, 19, 21) % (tile // 2)
            draw.line((x, y0, max(0, x - 6 + i % 13), y1), fill=(48, 112, 42, 255), width=3)
            draw.line((x, y0, min(tile - 1, x + 4 - i % 11), y1 + 8), fill=(72, 142, 52, 220), width=2)
        return img

    if name == "flower.png":
        img = fill_noisy_tile(name, tile, (64, 130, 50), 14)
        draw = ImageDraw.Draw(img)
        for i in range(11):
            x = 10 + noise_value(name, i, 5, 25) % (tile - 20)
            y = 16 + noise_value(name, i, 7, 27) % (tile - 30)
            color = [(214, 76, 106), (238, 208, 72), (220, 120, 214)][i % 3]
            draw.rectangle((x - 2, y - 2, x + 2, y + 2), fill=color + (255,))
            draw.point((x, y), fill=(246, 236, 180, 255))
        return img

    if name == "station_bench.png":
        img = fill_noisy_tile(name, tile, (118, 92, 58), 16)
        draw = ImageDraw.Draw(img)
        margin = tile // 6
        draw.rectangle((margin, margin, tile - margin, tile - margin), outline=(72, 52, 34, 255), width=4)
        draw.line((margin, tile // 2, tile - margin, tile // 2), fill=(148, 112, 72, 255), width=3)
        return img

    if name == "station_forge.png":
        img = fill_noisy_tile(name, tile, (88, 48, 38), 20)
        draw = ImageDraw.Draw(img)
        for i in range(6):
            x = tile // 4 + i * 8
            draw.rectangle((x, tile // 3, x + 6, tile * 2 // 3), fill=(42, 42, 44, 255))
        draw.rectangle((tile // 4, tile // 4, tile * 3 // 4, tile * 3 // 4), outline=(224, 120, 48, 255), width=4)
        return img

    if name == "station_crucible.png":
        img = fill_noisy_tile(name, tile, (96, 104, 118), 18)
        draw = ImageDraw.Draw(img)
        inset = tile // 5
        draw.ellipse((inset, inset, tile - inset, tile - inset), outline=(58, 132, 176, 255), width=5)
        draw.ellipse((tile // 3, tile // 3, tile * 2 // 3, tile * 2 // 3), fill=(42, 98, 176, 200))
        return img

    if name == "oak_plank.png":
        img = fill_noisy_tile(name, tile, (156, 118, 72), 14)
        draw = ImageDraw.Draw(img)
        for y in range(0, tile, tile // 4):
            draw.line((0, y, tile, y), fill=(132, 98, 58, 255), width=2)
        return img

    if name == "glass.png":
        img = fill_noisy_tile(name, tile, (180, 220, 240), 8)
        draw = ImageDraw.Draw(img)
        draw.line((0, 0, tile, tile), fill=(220, 240, 255, 180), width=2)
        draw.line((tile, 0, 0, tile), fill=(140, 190, 220, 140), width=2)
        return img

    if name == "clay.png":
        img = fill_noisy_tile(name, tile, (168, 112, 88), 18)
        scatter(ImageDraw.Draw(img), name, tile, [(148, 96, 72), (188, 128, 98)], 60, 3)
        return img

    if name == "iron_block.png":
        img = fill_noisy_tile(name, tile, (168, 172, 178), 12)
        draw = ImageDraw.Draw(img)
        draw.rectangle((tile // 5, tile // 5, tile * 4 // 5, tile * 4 // 5), outline=(120, 124, 130, 255), width=3)
        return img

    if name == "sandstone.png":
        img = fill_noisy_tile(name, tile, (196, 168, 108), 16)
        draw = ImageDraw.Draw(img)
        for y in range(tile // 6, tile, tile // 5):
            draw.line((0, y, tile, y), fill=(168, 140, 88, 255), width=2)
        return img

    if name == "gold_block.png":
        img = fill_noisy_tile(name, tile, (224, 188, 64), 14)
        draw = ImageDraw.Draw(img)
        draw.rectangle((tile // 5, tile // 5, tile * 4 // 5, tile * 4 // 5), outline=(188, 148, 32, 255), width=3)
        return img

    if name == "willow_log.png":
        return vertical_stripe_tile(name, tile, (68, 52, 38), (48, 36, 26), (88, 68, 48), 13)

    if name == "willow_leaves.png":
        return leaf_cluster_tile(name, tile, (58, 98, 62), [(42, 82, 48), (72, 118, 68), (36, 72, 42)])

    if name == "palm_log.png":
        img = vertical_stripe_tile(name, tile, (168, 138, 88), (138, 108, 68), (198, 168, 118), 18)
        draw = ImageDraw.Draw(img)
        for y in range(6, tile, 14):
            draw.line((tile // 3, y, tile * 2 // 3, y + 4), fill=(128, 98, 58, 255), width=2)
        return img

    if name == "palm_leaves.png":
        img = fill_noisy_tile(name, tile, (58, 118, 48), 14)
        draw = ImageDraw.Draw(img)
        cx, cy = tile // 2, tile // 3
        radius_x = (tile * 38) // 100
        radius_y = (tile * 28) // 100
        for step in range(12):
            x2, y2 = radial_offset(cx, cy, radius_x, radius_y, step)
            draw.line((cx, cy, x2, y2), fill=(42, 98, 38, 255), width=4)
        draw.ellipse((cx - 8, cy - 6, cx + 8, cy + 6), fill=(72, 142, 52, 255))
        return img

    if name == "birch_plank.png":
        img = fill_noisy_tile(name, tile, (198, 188, 168), 12)
        draw = ImageDraw.Draw(img)
        for y in range(0, tile, tile // 4):
            draw.line((0, y, tile, y), fill=(172, 162, 142, 255), width=2)
        return img

    if name == "pine_plank.png":
        img = fill_noisy_tile(name, tile, (128, 96, 58), 14)
        draw = ImageDraw.Draw(img)
        for y in range(0, tile, tile // 4):
            draw.line((0, y, tile, y), fill=(98, 72, 42, 255), width=2)
        return img

    if name == "cobblestone.png":
        img = fill_noisy_tile(name, tile, (108, 108, 112), 30)
        draw = ImageDraw.Draw(img)
        for i in range(24):
            x = noise_value(name, i, 11, 3) % (tile - 20)
            y = noise_value(name, i, 17, 5) % (tile - 20)
            w = 12 + noise_value(name, i, 23, 7) % 10
            h = 10 + noise_value(name, i, 29, 9) % 8
            draw.rectangle((x, y, x + w, y + h), outline=(88, 88, 92, 255), width=2)
        return img

    if name == "brick.png":
        img = fill_noisy_tile(name, tile, (148, 72, 52), 14)
        draw = ImageDraw.Draw(img)
        row_h = tile // 5
        for row in range(5):
            offset = (row % 2) * (tile // 10)
            y = row * row_h
            for col in range(4):
                x = offset + col * (tile // 4)
                draw.rectangle((x + 2, y + 2, x + tile // 4 - 2, y + row_h - 2), outline=(108, 52, 38, 255), width=2)
        return img

    if name == "moss_stone.png":
        img = fill_noisy_tile(name, tile, (98, 104, 92), 22)
        scatter(ImageDraw.Draw(img), name, tile, [(58, 108, 48), (72, 128, 58), (48, 92, 42)], 70, 4)
        return img

    if name == "mud.png":
        img = fill_noisy_tile(name, tile, (72, 58, 42), 20)
        scatter(ImageDraw.Draw(img), name, tile, [(58, 46, 34), (88, 72, 52)], 55, 3)
        return img

    if name == "reed.png":
        img = fill_noisy_tile(name, tile, (48, 88, 42), 12)
        draw = ImageDraw.Draw(img)
        for i in range(28):
            x = noise_value(name, i, 11, 17) % tile
            y0 = tile - 1
            y1 = tile // 6 + noise_value(name, i, 19, 21) % (tile // 2)
            draw.line((x, y0, x, y1), fill=(38, 98, 48, 255), width=3)
            draw.point((x, y1), fill=(72, 142, 58, 255))
        return img

    if name == "sunflower.png":
        img = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)
        cx, cy = tile // 2, tile // 3
        draw.line((cx, tile - 3, cx, cy + 18), fill=(42, 98, 38, 255), width=5)
        for step in range(12):
            x2, y2 = radial_offset(cx, cy, 22, 16, step)
            draw.ellipse((x2 - 7, y2 - 7, x2 + 7, y2 + 7), fill=(238, 198, 42, 255))
        draw.ellipse((cx - 12, cy - 10, cx + 12, cy + 10), fill=(68, 48, 28, 255))
        return img

    if name == "hay_bale.png":
        img = fill_noisy_tile(name, tile, (188, 158, 68), 16)
        draw = ImageDraw.Draw(img)
        for y in range(tile // 8, tile, tile // 6):
            draw.line((0, y, tile, y), fill=(158, 128, 48, 255), width=2)
        draw.rectangle((tile // 6, tile // 6, tile * 5 // 6, tile * 5 // 6), outline=(138, 108, 38, 255), width=3)
        return img

    if name == "ice.png":
        img = fill_noisy_tile(name, tile, (168, 212, 238), 10)
        draw = ImageDraw.Draw(img)
        draw.line((0, tile // 4, tile, tile // 4 + 6), fill=(200, 232, 248, 200), width=3)
        draw.line((tile // 3, 0, tile // 3 + 8, tile), fill=(140, 190, 228, 160), width=2)
        return img

    animal_colors = {
        "sheep_body.png": ((220, 220, 220), (180, 180, 180)),
        "sheep_head.png": ((210, 210, 210), (170, 170, 170)),
        "pig_body.png": ((240, 170, 170), (200, 120, 120)),
        "pig_head.png": ((230, 160, 160), (190, 110, 110)),
        "chicken_body.png": ((240, 220, 120), (200, 160, 60)),
        "chicken_head.png": ((230, 210, 110), (190, 150, 50)),
    }
    if name in animal_colors:
        base, accent = animal_colors[name]
        return make_animal_tile(name, tile, base, accent)

    if name.startswith("tool_"):
        return make_tool_icon(name, tile)

    return None


def make_tool_icon(name: str, tile: int) -> Image.Image:
    stem = name.replace(".png", "")
    parts = stem.split("_")
    if len(parts) < 3:
        return Image.new("RGBA", (tile, tile), (0, 0, 0, 0))

    tier = parts[1]
    tool_type = parts[2]

    img = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    tier_palette = {
        "wood": ((168, 122, 72), (118, 82, 48)),
        "stone": ((156, 156, 162), (104, 104, 110)),
        "iron": ((204, 210, 218), (142, 148, 158)),
        "gold": ((252, 214, 72), (198, 158, 28)),
    }
    head, head_dark = tier_palette.get(tier, ((160, 160, 160), (110, 110, 110)))
    handle = (124, 88, 52)
    handle_dark = (84, 58, 34)
    highlight = shade(head, 36)

    s = max(4, tile // 16)
    cx, cy = tile // 2, tile // 2

    def wood_handle(x0: int, y0: int, x1: int, y1: int, width: int) -> None:
        draw.line((x0, y0, x1, y1), fill=handle + (255,), width=width)
        draw.line((x0 + 1, y0 + 1, x1 + 1, y1 + 1), fill=handle_dark + (255,), width=max(1, width // 3))

    if tool_type == "pickaxe":
        wood_handle(cx - 3 * s, cy + 4 * s, cx + s, cy - s, s)
        draw.rectangle((cx - 4 * s, cy - 3 * s, cx + 4 * s, cy - s), fill=head + (255,))
        draw.rectangle((cx - 4 * s, cy - 3 * s, cx + 4 * s, cy - 2 * s), fill=highlight + (255,))
        draw.polygon(
            [(cx - 4 * s, cy - 2 * s), (cx - 5 * s, cy + s), (cx - 3 * s, cy - s)],
            fill=head_dark + (255,),
        )
        draw.polygon(
            [(cx + 4 * s, cy - 2 * s), (cx + 5 * s, cy + s), (cx + 3 * s, cy - s)],
            fill=head_dark + (255,),
        )
    elif tool_type == "axe":
        wood_handle(cx - s // 2, cy + 4 * s, cx - s // 2, cy - 2 * s, s)
        draw.polygon(
            [
                (cx + s, cy - 4 * s),
                (cx + 5 * s, cy - s),
                (cx + 4 * s, cy + 2 * s),
                (cx + 2 * s, cy - s),
            ],
            fill=head + (255,),
        )
        draw.line((cx + 2 * s, cy - 3 * s, cx + 4 * s, cy), fill=highlight + (255,), width=max(2, s // 2))
        draw.polygon([(cx + 4 * s, cy + 2 * s), (cx + 5 * s, cy + 3 * s), (cx + 3 * s, cy + s)], fill=head_dark + (255,))
    elif tool_type == "shovel":
        wood_handle(cx, cy + 4 * s, cx, cy - s, s)
        draw.polygon(
            [
                (cx, cy - 4 * s),
                (cx - 3 * s, cy - s),
                (cx - 2 * s, cy + s),
                (cx + 2 * s, cy + s),
                (cx + 3 * s, cy - s),
            ],
            fill=head + (255,),
        )
        draw.line((cx - s, cy - 2 * s, cx + s, cy - 2 * s), fill=highlight + (255,), width=max(2, s // 2))
        draw.rectangle((cx - 2 * s, cy, cx + 2 * s, cy + s), fill=head_dark + (255,))
    elif tool_type == "sword":
        draw.rectangle((cx - s // 2, cy + 3 * s, cx + s // 2, cy + 5 * s), fill=handle + (255,))
        draw.rectangle((cx - 2 * s, cy + 2 * s, cx + 2 * s, cy + 3 * s), fill=head_dark + (255,))
        draw.rectangle((cx - s, cy - 4 * s, cx + s, cy + 2 * s), fill=head + (255,))
        draw.rectangle((cx - s + 1, cy - 3 * s, cx + s - 1, cy - 2 * s), fill=highlight + (255,))
        draw.polygon([(cx, cy - 5 * s), (cx - s, cy - 4 * s), (cx + s, cy - 4 * s)], fill=highlight + (255,))

    # subtle drop shadow
    shadow = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow)
    shadow_draw.ellipse((cx - 4 * s, cy + 5 * s, cx + 4 * s, cy + 7 * s), fill=(0, 0, 0, 48))
    img = Image.alpha_composite(shadow, img)
    return img


def load_tile(filename: str, tile: int) -> Image.Image:
    path = TEXTURES / filename
    if path.exists():
        img = Image.open(path).convert("RGBA")
        if filename == "grass_side.png" and (TEXTURES / "dirt.png").exists():
            dirt = Image.open(TEXTURES / "dirt.png")
            img = compose_grass_side(dirt, img, tile)
        else:
            img = img.resize((tile, tile), Image.Resampling.NEAREST)
        return img

    procedural = make_procedural_tile(filename, tile)
    if procedural is not None:
        return procedural

    img = Image.new("RGBA", (tile, tile), (180, 80, 180, 255))
    return img


def validate_layout(layout: dict) -> None:
    required_keys = ("gridCols", "gridRows", "tileSize", "tiles")
    for key in required_keys:
        if key not in layout:
            raise ValueError(f"atlas_layout.json missing required key: {key}")

    if layout["gridCols"] <= 0 or layout["gridRows"] <= 0 or layout["tileSize"] <= 0:
        raise ValueError("gridCols, gridRows, and tileSize must be positive")

    tiles = layout["tiles"]
    if not isinstance(tiles, dict) or not tiles:
        raise ValueError("tiles must be a non-empty object")

    seen_slots: set[tuple[int, int]] = set()
    for tile_id, slot in tiles.items():
        if not isinstance(slot, dict):
            raise ValueError(f"tile '{tile_id}' must be an object")
        for field in ("file", "col", "row"):
            if field not in slot:
                raise ValueError(f"tile '{tile_id}' missing '{field}'")
        coord = (int(slot["col"]), int(slot["row"]))
        if coord in seen_slots:
            raise ValueError(f"duplicate atlas slot at {coord}")
        seen_slots.add(coord)
        if coord[0] >= layout["gridCols"] or coord[1] >= layout["gridRows"]:
            raise ValueError(f"tile '{tile_id}' slot {coord} is outside grid bounds")


def image_pixel_hash(image: Image.Image) -> str:
    import hashlib

    return hashlib.sha256(image.convert("RGBA").tobytes()).hexdigest()


def build_atlas(layout: dict, output_path: Path) -> tuple[int, int]:
    cols = layout["gridCols"]
    rows = layout["gridRows"]
    tile = layout["tileSize"]
    width = cols * tile
    height = rows * tile

    atlas = Image.new("RGBA", (width, height), (0, 0, 0, 255))
    tile_items = sorted(
        layout["tiles"].items(),
        key=lambda item: (item[1]["row"], item[1]["col"], item[0]),
    )
    for tile_id, slot in tile_items:
        filename = slot["file"]
        image = load_tile(filename, tile)
        atlas.paste(image, (slot["col"] * tile, slot["row"] * tile))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(output_path, format="PNG", compress_level=6, optimize=False)
    return width, height


def check_atlas() -> None:
    import argparse
    import tempfile

    parser = argparse.ArgumentParser(description="Build or validate Autonocraft texture atlas")
    parser.add_argument(
        "--check",
        action="store_true",
        help="Validate atlas_layout.json and ensure generation succeeds; compare atlas.png if present",
    )
    args = parser.parse_args()

    layout = load_layout()
    validate_layout(layout)

    if args.check:
        with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as handle:
            temp_path = Path(handle.name)
        try:
            width, height = build_atlas(layout, temp_path)
            print(
                f"Atlas generation OK ({width}x{height}, "
                f"{layout['gridCols']}x{layout['gridRows']} tiles @ {layout['tileSize']}px)"
            )
            if OUTPUT.exists():
                committed_img = Image.open(OUTPUT).convert("RGBA")
                generated_img = Image.open(temp_path).convert("RGBA")
                committed_hash = image_pixel_hash(committed_img)
                generated_hash = image_pixel_hash(generated_img)
                if committed_hash != generated_hash:
                    raise SystemExit(
                        f"atlas.png is out of date (committed_pixels={committed_hash[:12]}, "
                        f"generated_pixels={generated_hash[:12]}). "
                        "Run: python3 scripts/build_atlas.py"
                    )
                print(f"Committed atlas matches generated output ({OUTPUT})")
            else:
                print("No committed atlas.png; layout and generation checks passed")
        finally:
            temp_path.unlink(missing_ok=True)
        return

    width, height = build_atlas(layout, OUTPUT)
    print(f"Wrote {OUTPUT} ({width}x{height}, {layout['gridCols']}x{layout['gridRows']} tiles @ {layout['tileSize']}px)")


def main() -> None:
    check_atlas()


if __name__ == "__main__":
    main()
