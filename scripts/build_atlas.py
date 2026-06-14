#!/usr/bin/env python3
"""Build block texture atlas procedurally for Autonocraft."""

import json
import math
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

CELL_ORGANIC = 3
CELL_EARTH = 3
CELL_WOOD = 2


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
    cell: int = CELL_EARTH,
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


def fill_noisy_tile(name: str, tile: int, base: tuple[int, int, int], variation: int, cell: int = CELL_EARTH) -> Image.Image:
    return fill_pixel_cluster_tile(name, tile, base, [base], cell, variation)


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


def apply_cell_rims(img: Image.Image, cell_size: int, dark_amount: int, light_amount: int) -> None:
    pixels = img.load()
    tile = img.width
    for cy in range(0, tile, cell_size):
        for cx in range(0, tile, cell_size):
            max_x = min(tile - 1, cx + cell_size - 1)
            max_y = min(tile - 1, cy + cell_size - 1)
            for x in range(cx, max_x + 1):
                p = pixels[x, cy]
                pixels[x, cy] = shade(p[:3], -dark_amount) + (p[3],) if len(p) == 4 else shade(p, -dark_amount)
                p = pixels[x, max_y]
                pixels[x, max_y] = shade(p[:3], light_amount // 2) + (p[3],) if len(p) == 4 else shade(p, light_amount // 2)
            for y in range(cy, max_y + 1):
                p = pixels[cx, y]
                pixels[cx, y] = shade(p[:3], -dark_amount) + (p[3],) if len(p) == 4 else shade(p, -dark_amount)
                p = pixels[max_x, y]
                pixels[max_x, y] = shade(p[:3], light_amount) + (p[3],) if len(p) == 4 else shade(p, light_amount)


def expand_palette(base: tuple[int, int, int], dark: tuple[int, int, int], light: tuple[int, int, int]) -> list[tuple[int, int, int]]:
    return [base, dark, light, shade(base, -8), shade(base, 6)]


def make_wood_plank_tile(name: str, tile: int, base: tuple[int, int, int], seam: tuple[int, int, int], grain: tuple[int, int, int]) -> Image.Image:
    palette = expand_palette(base, seam, grain)
    img = fill_pixel_cluster_tile(name, tile, base, palette, CELL_WOOD, 10)
    draw = ImageDraw.Draw(img)
    pixels = img.load()
    plank_h = tile // 4
    for row in range(4):
        y0 = row * plank_h
        y1 = min(tile - 1, y0 + plank_h - 1)
        draw.line((0, y0, tile - 1, y0), fill=shade(base, 14) + (255,), width=1)
        draw.line((0, y1, tile - 1, y1), fill=shade(seam, -10) + (255,), width=2)
        for g in range(5):
            gy = y0 + 4 + g * (plank_h // 5)
            gx0 = noise_value(name, row, g, 11) % 10
            draw.line((gx0, gy, tile - 1 - noise_value(name, row, g, 13) % 8, gy + 1), fill=grain + (255,), width=1)
        if noise_value(name, row, 0, 17) % 3 == 0:
            kx = noise_value(name, row, 1, 19) % (tile - 16) + 8
            ky = y0 + plank_h // 2
            draw.ellipse((kx - 4, ky - 3, kx + 4, ky + 3), fill=shade(grain, -16) + (255,))
    return img


def make_dirt_tile(name: str, tile: int, palette: list[tuple[int, int, int]], cell_size: int = CELL_EARTH) -> Image.Image:
    img = fill_pixel_cluster_tile(name, tile, palette[1], palette, cell_size, 16)
    draw = ImageDraw.Draw(img)
    pixels = img.load()
    for i in range(12):
        cx = noise_value(name, i, 31, 33) % tile
        cy = noise_value(name, i, 35, 37) % tile
        rx = 4 + noise_value(name, i, 39, 41) % 5
        ry = 3 + noise_value(name, i, 43, 47) % 4
        clod_color = shade(palette[0], -(14 + i % 6))
        draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=clod_color + (255,))
        for hx in range(cx - rx + 1, cx + rx):
            set_pixel(pixels, tile, hx, cy - ry + 1, shade(clod_color, 8))

    pebble_colors = [(140, 135, 130), (165, 155, 145)]
    for i in range(15):
        px = noise_value(name, i, 53, 59) % (tile - 4)
        py = noise_value(name, i, 61, 67) % (tile - 4)
        pebble = pebble_colors[i % 2]
        set_pixel(pixels, tile, px, py, pebble)
        set_pixel(pixels, tile, px + 1, py, shade(pebble, 14))
        set_pixel(pixels, tile, px, py + 1, shade(pebble, -10))
        set_pixel(pixels, tile, px + 1, py + 1, shade(pebble, -16))

    apply_cell_rims(img, cell_size, -10, 4)
    return img


def make_grass_fringe_tile(name: str, tile: int, palette: list[tuple[int, int, int]]) -> Image.Image:
    img = fill_pixel_cluster_tile(name, tile, palette[0], palette, CELL_ORGANIC, 18)
    pixels = img.load()
    fringe_rows = max(8, tile * 42 // 100)
    for i in range(64):
        x = noise_value(name, i, 3, 5) % tile
        len_val = 6 + noise_value(name, i, 7, 9) % fringe_rows
        blade = palette[noise_value(name, i, 11, 13) % len(palette)]
        for d in range(len_val):
            y = d
            sway = (noise_value(name, i, 17, 19) % 3) - 1
            wx = x + sway
            if 0 <= wx < tile and 0 <= y < tile:
                pixels[wx, y] = (shade(blade, 8) if d == 0 else blade) + (255,)
    return img


def make_sand_tile(name: str, tile: int, palette: list[tuple[int, int, int]]) -> Image.Image:
    img = fill_pixel_cluster_tile(name, tile, palette[1], palette, CELL_ORGANIC, 16)
    draw = ImageDraw.Draw(img)
    pixels = img.load()
    scatter(draw, name, tile, palette, 30, 2)
    for i in range(18):
        x = noise_value(name, i, 3, 5) % tile
        y = noise_value(name, i, 7, 9) % tile
        set_pixel(pixels, tile, x, y, shade(palette[2], 20))
    apply_cell_rims(img, CELL_ORGANIC, -10, 6)
    return img


def make_metal_block_tile(name: str, tile: int, base_color: tuple[int, int, int], edge: tuple[int, int, int]) -> Image.Image:
    palette = [base_color, edge, shade(base_color, 18), shade(base_color, -8), shade(base_color, 6)]
    img = fill_pixel_cluster_tile(name, tile, base_color, palette, CELL_EARTH, 10)
    draw = ImageDraw.Draw(img)
    inset = tile // 5
    draw_rect_outline_wrapped(draw, tile, inset, inset, tile - inset, tile - inset, edge, 3)
    draw.line((inset, inset, tile - inset, inset), fill=shade(base_color, 22) + (255,), width=2)
    draw.line((inset, inset, inset, tile - inset), fill=shade(base_color, 12) + (255,), width=2)
    apply_cell_rims(img, CELL_EARTH, -8, 6)
    return img


def make_log_top_tile(name: str, tile: int, bark_base: tuple[int, int, int], bark_dark: tuple[int, int, int], wood_base: tuple[int, int, int], wood_ring: tuple[int, int, int]) -> Image.Image:
    img = Image.new("RGBA", (tile, tile))
    px = img.load()
    cx = tile / 2.0
    cy = tile / 2.0
    for y in range(tile):
        for x in range(tile):
            dx = x - cx + 0.5
            dy = y - cy + 0.5
            dist = math.sqrt(dx * dx + dy * dy)
            theta = math.atan2(dy, dx)

            angle_idx = int(round((theta + math.pi) / (2.0 * math.pi) * 32.0)) % 32
            wobble = (noise_value(name, angle_idx, 0, 45) % 6) - 3

            adjusted_dist = dist + wobble
            c = (0, 0, 0)

            if adjusted_dist > (tile / 2.0) - 5.0:
                bark_noise = noise_value(name, x, y, 77) % 3
                if bark_noise == 0:
                    c = bark_dark
                elif bark_noise == 1:
                    c = bark_base
                else:
                    c = shade(bark_base, -12)
            else:
                wood_noise = (noise_value(name, x, y, 99) % 3 - 1) * 0.5
                adjusted_dist_wood = adjusted_dist + wood_noise

                if adjusted_dist_wood < 5.0:
                    t = 0.8
                else:
                    ring_phase = adjusted_dist_wood % 10.0
                    if ring_phase < 2.5:
                        t = 0.7 + 0.3 * (1.0 - ring_phase / 2.5)
                    else:
                        t = 0.2 * (ring_phase - 2.5) / 7.5

                c = lerp_color(wood_base, wood_ring, t)
                grain_noise = (noise_value(name, x, y, 123) % 15) - 7
                c = shade(c, grain_noise)

            px[x, y] = c + (255,)
    return img


def make_wood_log_tile(name: str, tile: int, base: tuple[int, int, int], ring_dark: tuple[int, int, int], ring_light: tuple[int, int, int], spacing: int) -> Image.Image:
    palette = expand_palette(base, ring_dark, ring_light)
    img = fill_pixel_cluster_tile(name, tile, base, palette, CELL_WOOD, 12)
    draw = ImageDraw.Draw(img)
    for x in range(2, tile, spacing):
        wobble = (noise_value(name, x, 0, 31) % 5) - 2
        rx = x + wobble
        draw.rectangle((rx, 0, rx + 3, tile - 1), fill=ring_dark + (255,))
        draw.rectangle((rx + 1, 0, rx + 1, tile - 1), fill=ring_light + (255,))
    
    for y in range(10, tile, 22):
        offset = noise_value(name, y, 0, 37) % 8
        draw.line((offset, y, tile - 1, y), fill=shade(base, -22) + (255,), width=2)
        
    for i in range(4):
        kx = noise_value(name, i, 41, 43) % (tile - 20) + 10
        ky = noise_value(name, i, 47, 49) % (tile - 20) + 10
        kr = 6 + noise_value(name, i, 51, 53) % 6
        draw.ellipse((kx - kr, ky - kr, kx + kr, ky + kr), fill=shade(ring_dark, -8) + (255,))
        draw.ellipse((kx - kr // 2, ky - kr // 2, kx + kr // 2, ky + kr // 2), fill=shade(ring_light, 6) + (255,))
    return img


def make_brick_tile(name: str, tile: int, brick: tuple[int, int, int], mortar: tuple[int, int, int], brick_hi: tuple[int, int, int], brick_lo: tuple[int, int, int]) -> Image.Image:
    img = Image.new("RGBA", (tile, tile), mortar + (255,))
    draw = ImageDraw.Draw(img)
    pixels = img.load()
    row_h = tile // 5
    for row in range(5):
        offset = (row % 2) * (tile // 8)
        y = row * row_h
        for col in range(4):
            x = offset + col * (tile // 4)
            base_color = brick_hi if (row + col) % 2 == 0 else brick_lo
            fill_rect_wrapped(draw, tile, x + 2, y + 2, tile // 4 - 3, row_h - 3, base_color)
            for bx in range(x + 2, x + tile // 4 - 1):
                set_pixel_wrapped(pixels, tile, bx, y + 2, shade(base_color, 18))
                set_pixel_wrapped(pixels, tile, bx, y + row_h - 2, shade(base_color, -18))
            for by in range(y + 2, y + row_h - 1):
                set_pixel_wrapped(pixels, tile, x + 2, by, shade(base_color, 14))
                set_pixel_wrapped(pixels, tile, x + tile // 4 - 2, by, shade(base_color, -22))
    return img


def vertical_stripe_tile(
    name: str,
    tile: int,
    base: tuple[int, int, int],
    stripe: tuple[int, int, int],
    light_stripe: tuple[int, int, int],
    stripe_spacing: int,
) -> Image.Image:
    img = fill_noisy_tile(name, tile, base, 14, CELL_WOOD)
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
    img = fill_pixel_cluster_tile(name, tile, shade(base, -12), palette, CELL_ORGANIC, 16)
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
    img = fill_noisy_tile(name, tile, base, 8, CELL_ORGANIC)
    draw = ImageDraw.Draw(img)
    margin = tile // 6
    draw.rectangle((margin, margin, tile - margin, tile - margin), outline=shade(accent, -30) + (255,), width=3)
    scatter(draw, name, tile, [accent, shade(accent, 20)], 12, 2)
    return img


def set_pixel_wrapped(px, tile_size, x, y, color):
    wx = (x % tile_size + tile_size) % tile_size
    wy = (y % tile_size + tile_size) % tile_size
    px[wx, wy] = color + (255,) if len(color) == 3 else color

def set_pixel(px, tile_size, x, y, color):
    if 0 <= x < tile_size and 0 <= y < tile_size:
        px[x, y] = color + (255,) if len(color) == 3 else color

def make_flora_sprite_tile(name: str, tile: int, palette: list[tuple[int, int, int]], blade_count: int, add_heads: bool = False) -> Image.Image:
    img = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    pixels = img.load()
    for i in range(blade_count):
        x = noise_value(name, i, 11, 17) % tile
        y1 = tile // 5 + noise_value(name, i, 19, 21) % (tile // 2)
        blade = palette[noise_value(name, i, 23, 29) % len(palette)]
        tip_x = max(0, min(tile - 1, x + (noise_value(name, i, 31, 33) % 13) - 6))
        draw.line((x, tile - 2, tip_x, y1), fill=blade + (255,), width=2)
        set_pixel(pixels, tile, tip_x, y1, shade(blade, 24))
        if add_heads and i % 4 == 0:
            set_pixel(pixels, tile, tip_x, y1 - 1, (72, 142, 58))
    return img

def make_tall_grass_clump(name: str, tile: int, palette: list[tuple[int, int, int]]) -> Image.Image:
    return make_flora_sprite_tile(name, tile, palette, 34)

def make_short_grass_sprite(name: str, tile: int, palette: list[tuple[int, int, int]]) -> Image.Image:
    img = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    pixels = img.load()
    for i in range(22):
        x = noise_value(name, i, 11, 17) % tile
        y1 = tile * 2 // 3 + noise_value(name, i, 19, 21) % (tile // 4)
        blade = palette[noise_value(name, i, 23, 29) % len(palette)]
        tip_x = max(0, min(tile - 1, x + (noise_value(name, i, 31, 33) % 9) - 4))
        draw.line((x, tile - 2, tip_x, y1), fill=blade + (255,), width=2)
        set_pixel(pixels, tile, tip_x, y1, shade(blade, 18))
    return img

def make_flower_stem_sprite(name: str, tile: int, stem: tuple[int, int, int], petal_colors: list[tuple[int, int, int]], center: tuple[int, int, int]) -> Image.Image:
    img = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx = tile // 2 + (noise_value(name, 0, 3, 5) % 7) - 3
    bloom_y = tile // 4 + noise_value(name, 1, 7, 11) % (tile // 6)
    draw.line((cx, tile - 2, cx + (noise_value(name, 2, 13, 17) % 5) - 2, bloom_y + 10), fill=stem + (255,), width=2)
    petal = petal_colors[noise_value(name, 3, 19, 23) % len(petal_colors)]
    petal_count = 5 + noise_value(name, 4, 29, 31) % 2
    for i in range(petal_count):
        rad = i * (6.28318 / petal_count) + noise_value(name, i, 37, 41) * 0.08
        px = int(cx + math.cos(rad) * (tile / 7))
        py = int(bloom_y + math.sin(rad) * (tile / 9))
        draw.ellipse((px - 4, py - 3, px + 4, py + 3), fill=petal + (255,))
    draw.ellipse((cx - 4, bloom_y - 3, cx + 4, bloom_y + 3), fill=center + (255,))
    draw.line((cx - 6, tile // 2, cx - 10, tile // 2 + 6), fill=shade(stem, 8) + (255,), width=1)
    return img

def make_reed_sprite(name: str, tile: int, palette: list[tuple[int, int, int]], head: tuple[int, int, int]) -> Image.Image:
    img = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    pixels = img.load()
    for i in range(10):
        x = tile // 4 + noise_value(name, i, 11, 17) % (tile // 2)
        top = tile // 6 + noise_value(name, i, 19, 21) % (tile // 3)
        stalk = palette[noise_value(name, i, 23, 29) % len(palette)]
        draw.line((x, tile - 2, x + (noise_value(name, i, 31, 33) % 5) - 2, top), fill=stalk + (255,), width=2)
        for seg in range(top, tile - 4, 8):
            set_pixel(pixels, tile, x, seg, shade(stalk, 10))
        if i % 2 == 0:
            draw.ellipse((x - 3, top - 4, x + 3, top + 2), fill=head + (255,))
    return img

def make_sunflower_sprite(name: str, tile: int, stem: tuple[int, int, int], petal: tuple[int, int, int], center: tuple[int, int, int]) -> Image.Image:
    img = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = tile // 2, tile // 3
    draw.line((cx, tile - 3, cx, cy + 18), fill=stem + (255,), width=3)
    draw.line((cx - 5, tile // 2, cx - 9, tile // 2 + 8), fill=shade(stem, 10) + (255,), width=2)
    for ring in range(2):
        radius_x = 20 - ring * 4
        radius_y = 14 - ring * 3
        ring_petal = petal if ring == 0 else shade(petal, 12)
        for step in range(12):
            x2, y2 = radial_offset(cx, cy, radius_x, radius_y, step)
            draw.ellipse((x2 - 6, y2 - 5, x2 + 6, y2 + 5), fill=ring_petal + (255,))
    draw.ellipse((cx - 12, cy - 10, cx + 12, cy + 10), fill=center + (255,))
    draw.ellipse((cx - 6, cy - 5, cx + 2, cy + 1), fill=shade(center, 16) + (255,))
    return img

def pack_flora_variants(tile: int, generator) -> Image.Image:
    half = tile // 2
    img = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
    for variant in range(4):
        variant_img = generator(half, variant)
        ox = (variant % 2) * half
        oy = (variant // 2) * half
        img.paste(variant_img, (ox, oy))
    return img

def fill_rect_wrapped(draw, tile_size, x, y, w, h, color):
    for dy in (-tile_size, 0, tile_size):
        for dx in (-tile_size, 0, tile_size):
            draw.rectangle((x + dx, y + dy, x + dx + w - 1, y + dy + h - 1), fill=color + (255,) if len(color) == 3 else color)

def draw_rect_outline_wrapped(draw, tile_size, x0, y0, x1, y1, color, thickness):
    for dy in (-tile_size, 0, tile_size):
        for dx in (-tile_size, 0, tile_size):
            for t in range(thickness):
                draw.rectangle((x0 + dx + t, y0 + dy + t, x1 + dx - t, y1 + dy - t), outline=color + (255,) if len(color) == 3 else color)

def voronoi_tile(name: str, tile: int, cell_colors: list, grout: tuple, seed_count: int, grout_width: float) -> Image.Image:
    seeds = []
    for i in range(seed_count):
        sx = noise_value(name, i, 1, 3) % tile
        sy = noise_value(name, i, 2, 5) % tile
        scolor = cell_colors[noise_value(name, i, 3, 7) % len(cell_colors)]
        seeds.append((sx, sy, scolor))

    img = Image.new("RGBA", (tile, tile))
    pixels = img.load()

    for y in range(tile):
        for x in range(tile):
            d1 = float('inf')
            d2 = float('inf')
            nearest = cell_colors[0]
            nearest_idx = 0
            for i, (sx, sy, scolor) in enumerate(seeds):
                dx = x - sx
                dy = y - sy
                d = dx * dx + dy * dy
                if d < d1:
                    d2 = d1
                    d1 = d
                    nearest = scolor
                    nearest_idx = i
                elif d < d2:
                    d2 = d

            import math
            edge = math.sqrt(d2) - math.sqrt(d1)
            if edge < grout_width:
                g = noise_value(name, x, y, 23) % 5
                if g == 0:
                    c = shade(grout, 6)
                elif g == 1:
                    c = shade(grout, -6)
                else:
                    c = grout
                pixels[x, y] = c + (255,)
            else:
                shade_val = min(1.0, math.sqrt(d1) / (tile * 0.42))
                c = lerp_color(shade(nearest, 12), shade(nearest, -16), shade_val)
                
                # Directional lighting highlight
                angle_x = x - seeds[nearest_idx][0]
                angle_y = y - seeds[nearest_idx][1]
                if angle_x < -1 and angle_y < -1:
                    c = shade(c, 16)
                elif angle_x > 1 and angle_y > 1:
                    c = shade(c, -18)
                
                pixels[x, y] = c + (255,)
    return img

def make_procedural_tile(name: str, tile: int) -> Optional[Image.Image]:
    if name.startswith("tool_"):
        return make_tool_icon(name, tile)

    if name == "oak_log_top.png":
        return make_log_top_tile(name, tile, (96, 72, 48), (68, 48, 30), (156, 118, 72), (118, 86, 52))

    if name == "birch_log_top.png":
        return make_log_top_tile(name, tile, (210, 205, 188), (184, 178, 162), (198, 188, 168), (158, 148, 128))

    if name == "pine_log_top.png":
        return make_log_top_tile(name, tile, (78, 58, 36), (52, 38, 22), (128, 96, 58), (92, 68, 40))

    if name == "willow_log_top.png":
        return make_log_top_tile(name, tile, (68, 52, 38), (44, 32, 22), (136, 106, 68), (102, 78, 48))

    if name == "palm_log_top.png":
        return make_log_top_tile(name, tile, (168, 138, 88), (138, 108, 68), (208, 182, 128), (178, 148, 96))

    if name == "grass_top.png":
        palette = [(38, 92, 34), (50, 114, 42), (60, 128, 48), (32, 82, 30), (70, 138, 52)]
        img = fill_pixel_cluster_tile(name, tile, palette[1], palette, CELL_ORGANIC, 12)
        px = img.load()
        # Scatter lush grass blade clumps
        for i in range(35):
            cx = noise_value(name, i, 3, 7) % tile
            cy = noise_value(name, i, 5, 9) % tile
            blade = palette[noise_value(name, i, 11, 13) % len(palette)]
            set_pixel_wrapped(px, tile, cx, cy, shade(blade, 12))
            set_pixel_wrapped(px, tile, cx - 1, cy - 1, blade)
            set_pixel_wrapped(px, tile, cx + 1, cy - 1, blade)
            set_pixel_wrapped(px, tile, cx, cy + 1, shade(blade, -8))

        # Scatter small colorful flowers
        flower_colors = [(240, 90, 120), (240, 210, 60), (90, 180, 240)]
        for i in range(8):
            cx = noise_value(name, i, 23, 31) % tile
            cy = noise_value(name, i, 27, 37) % tile
            petal = flower_colors[noise_value(name, i, 41, 43) % len(flower_colors)]
            center = (250, 240, 160)
            set_pixel_wrapped(px, tile, cx, cy, center)
            set_pixel_wrapped(px, tile, cx - 1, cy, petal)
            set_pixel_wrapped(px, tile, cx + 1, cy, petal)
            set_pixel_wrapped(px, tile, cx, cy - 1, petal)
            set_pixel_wrapped(px, tile, cx, cy + 1, petal)
        apply_cell_rims(img, CELL_ORGANIC, -10, 4)
        return img

    if name == "dirt.png":
        palette = [(92, 68, 42), (110, 82, 52), (128, 96, 62), (84, 60, 38)]
        return make_dirt_tile(name, tile, palette)

    if name == "grass_side.png":
        dirt = make_procedural_tile("dirt.png", tile)
        fringe = make_grass_fringe_tile(name + "_fringe", tile, [(46, 106, 40), (58, 124, 46), (68, 134, 50)])
        if dirt is None:
            return fringe
        return compose_grass_side(dirt, fringe, tile)

    if name == "stone.png":
        palette = [(96, 96, 100), (120, 120, 124), (142, 142, 146), (108, 108, 112), (88, 88, 92)]
        grout = (72, 72, 76)
        img = voronoi_tile(name, tile, palette, grout, 18, 2.4)
        draw = ImageDraw.Draw(img)

        # Draw rock fractures and clefts
        for i in range(8):
            x0 = noise_value(name, i, 3, 17) % (tile - 30) + 15
            y0 = noise_value(name, i, 7, 19) % (tile - 30) + 15
            length = 12 + noise_value(name, i, 11, 23) % 16
            dx = length if noise_value(name, i, 13, 29) % 2 == 0 else -length
            dy = 4 + noise_value(name, i, 17, 31) % 8
            
            crack_color = shade(grout, -36)
            highlight_color = shade(palette[2], 24)

            draw.line((x0, y0, x0 + dx, y0 + dy), fill=crack_color + (255,), width=1)
            draw.line((x0, y0 - 1, x0 + dx, y0 + dy - 1), fill=highlight_color + (255,), width=1)
        return img

    if name == "oak_log.png":
        return make_wood_log_tile(name, tile, (96, 72, 48), (72, 52, 34), (118, 92, 62), 14)

    if name == "birch_log.png":
        img = make_wood_log_tile(name, tile, (210, 205, 188), (188, 182, 168), (232, 228, 214), 16)
        draw = ImageDraw.Draw(img)
        for i in range(14):
            x = noise_value(name, i, 3, 11) % tile
            y = noise_value(name, i, 7, 13) % tile
            draw.rectangle((x, y, x + 2, y + 7), fill=(48, 44, 40, 255))
        return img

    if name == "pine_log.png":
        return make_wood_log_tile(name, tile, (78, 58, 36), (58, 42, 26), (98, 74, 48), 12)

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
        return make_sand_tile(name, tile, [(180, 162, 102), (210, 196, 132), (236, 222, 158), (194, 176, 118)])

    if name == "snow.png":
        palette = [(228, 236, 244), (240, 246, 252), (252, 254, 255)]
        img = fill_pixel_cluster_tile(name, tile, palette[1], palette, CELL_EARTH, 8)
        draw = ImageDraw.Draw(img)
        draw.line((0, tile // 3, tile - 1, tile // 3 - 6), fill=(208, 220, 228, 255), width=2)
        draw.line((0, tile * 2 // 3, tile - 1, tile * 2 // 3 + 4), fill=(255, 255, 255, 255), width=2)
        apply_cell_rims(img, CELL_EARTH, -6, 8)
        return img

    if name == "gravel.png":
        img = fill_noisy_tile(name, tile, (128, 126, 120), 36)
        scatter(ImageDraw.Draw(img), name, tile, [(88, 88, 84), (160, 158, 150), (108, 106, 102)], 150, 4)
        return img

    ore_colors = {
        "coal_ore.png": ((42, 42, 44), (70, 70, 75)),
        "iron_ore.png": ((188, 132, 86), (220, 170, 130)),
        "gold_ore.png": ((224, 184, 62), (255, 225, 120)),
    }
    if name in ore_colors:
        stone_palette = [(98, 98, 102), (120, 120, 124), (142, 142, 146), (108, 108, 112), (88, 88, 92)]
        img = voronoi_tile(name, tile, stone_palette, (72, 72, 76), 18, 2.4)
        draw = ImageDraw.Draw(img)

        # Draw rock cracks
        for i in range(8):
            x0 = noise_value(name, i, 3, 17) % (tile - 30) + 15
            y0 = noise_value(name, i, 7, 19) % (tile - 30) + 15
            length = 12 + noise_value(name, i, 11, 23) % 16
            dx = length if noise_value(name, i, 13, 29) % 2 == 0 else -length
            dy = 4 + noise_value(name, i, 17, 31) % 8
            draw.line((x0, y0, x0 + dx, y0 + dy), fill=shade((72, 72, 76), -36) + (255,), width=1)
            draw.line((x0, y0 - 1, x0 + dx, y0 + dy - 1), fill=shade(stone_palette[2], 24) + (255,), width=1)

        ore, ore_hi = ore_colors[name]
        # Draw organic mineral veins
        for cluster in range(6):
            cx = noise_value(name, cluster, 3, 7) % (tile - 24) + 12
            cy = noise_value(name, cluster, 5, 9) % (tile - 24) + 12
            pieces = 4 + noise_value(name, cluster, 11, 13) % 4
            for p in range(pieces):
                x = cx + (noise_value(name, cluster, p, 17) % 14) - 7
                y = cy + (noise_value(name, cluster, p, 19) % 14) - 7
                r = 3 + noise_value(name, cluster, p, 21) % 4
                dark_outline = shade(ore, -30)
                draw.ellipse((x - r - 1, y - r - 1, x + r + 1, y + r + 1), fill=dark_outline + (255,))
                draw.ellipse((x - r, y - r, x + r, y + r), fill=ore + (255,))
                draw.ellipse((x - r // 2, y - r // 2, x + r // 4, y + r // 4), fill=ore_hi + (255,))
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
        palette = [(48, 112, 42), (62, 138, 52), (78, 158, 58)]
        return pack_flora_variants(tile, lambda half, variant: make_tall_grass_clump(f"tall_grass_v{variant}", half, palette))

    if name == "flower.png":
        petal_colors = [(214, 76, 106), (238, 208, 72), (220, 120, 214), (120, 160, 230)]
        stem = (42, 98, 38)
        center = (246, 236, 180)
        return pack_flora_variants(tile, lambda half, variant: make_flower_stem_sprite(f"flower_v{variant}", half, stem, petal_colors, center))

    if name == "station_bench.png":
        img = make_wood_plank_tile(name, tile, (118, 92, 58), (88, 64, 38), (104, 78, 48))
        draw = ImageDraw.Draw(img)
        margin = tile // 6
        draw_rect_outline_wrapped(draw, tile, margin, margin, tile - margin, tile - margin, (72, 52, 34), 4)
        draw.line((margin, tile // 2, tile - margin, tile // 2), fill=(148, 112, 72) + (255,), width=3)
        
        # Draw tiny hammer and blueprint details on the workbench
        draw.line((margin + 12, margin + 12, margin + 12, margin + 28), fill=(124, 88, 52, 255), width=2)
        draw.line((margin + 6, margin + 12, margin + 18, margin + 12), fill=(156, 156, 162, 255), width=4)
        
        draw.rectangle((tile - margin - 30, tile - margin - 22, tile - margin - 6, tile - margin - 6), fill=(42, 98, 176, 255))
        draw.rectangle((tile - margin - 26, tile - margin - 18, tile - margin - 10, tile - margin - 10), outline=(220, 240, 255, 180), width=1)
        return img

    if name == "station_forge.png":
        img = make_brick_tile(name, tile, (100, 100, 105), shade((80, 80, 84), -16), (120, 120, 125), (80, 80, 84))
        draw = ImageDraw.Draw(img)
        cx, cy = tile // 2, tile // 2
        size = tile // 2
        x, y = cx - size // 2, cy - size // 2
        draw.rectangle((x, y, x + size - 1, y + size - 1), fill=(24, 24, 26, 255))
        draw.rectangle((x - 2, y - 2, x + size + 1, y + size + 1), outline=(58, 38, 30, 255), width=3)
        draw.rectangle((x - 1, y - 1, x + size, y + size), outline=(90, 70, 60, 255), width=1)
        
        # Glowing embers
        for i in range(15):
            ex = x + 3 + noise_value(name, i, 3, 11) % (size - 6)
            ey = y + size - 8 - noise_value(name, i, 7, 13) % 10
            r = 2 + noise_value(name, i, 11, 17) % 3
            col = (255, 230, 60) if i % 3 == 0 else ((255, 110, 30) if i % 2 == 0 else (220, 40, 20))
            draw.ellipse((ex - r, ey - r, ex + r, ey + r), fill=col + (255,))
        return img

    if name == "station_crucible.png":
        base_palette = [(110, 110, 114), (130, 130, 135), (90, 90, 94)]
        img = voronoi_tile(name, tile, base_palette, shade(base_palette[2], -12), 14, 2.5)
        draw = ImageDraw.Draw(img)
        cx, cy = tile // 2, tile // 2
        r = tile // 3
        draw.ellipse((cx - r - 4, cy - r - 4, cx + r + 4, cy + r + 4), fill=(48, 48, 52, 255))
        draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(58, 132, 176, 255))
        draw.ellipse((cx - r + 5, cy - r + 5, cx + r - 5, cy + r - 5), fill=(42, 98, 176, 200))
        draw.ellipse((cx - r + 9, cy - r + 9, cx + r - 9, cy + r - 9), fill=shade((42, 98, 176), -16) + (200,))
        draw.ellipse((cx - r + 13, cy - r + 13, cx + r - 13, cy + r - 13), fill=shade((42, 98, 176), 40) + (255,))
        draw.ellipse((cx - r + 16, cy - r + 16, cx + r - 16, cy + r - 16), fill=(42, 98, 176, 200))
        return img

    if name == "oak_plank.png":
        return make_wood_plank_tile(name, tile, (156, 118, 72), (118, 86, 52), (138, 104, 64))

    if name == "glass.png":
        img = Image.new("RGBA", (tile, tile), (180, 220, 240, 32))
        draw = ImageDraw.Draw(img)
        draw.rectangle((0, 0, tile - 1, tile - 1), outline=(110, 150, 180, 140))
        draw.line((0, 0, tile - 1, 0), fill=(240, 250, 255, 180), width=2)
        draw.line((0, 0, 0, tile - 1), fill=(240, 250, 255, 180), width=2)
        cx, cy = tile // 2, tile // 2
        draw.line((cx - 16, cy - 8, cx - 4, cy + 4), fill=(255, 255, 255, 150), width=2)
        draw.line((cx + 4, cy - 8, cx + 16, cy + 4), fill=(255, 255, 255, 150), width=2)
        return img

    if name == "clay.png":
        palette = [(148, 96, 72), (168, 112, 88), (188, 128, 98), (132, 86, 64)]
        return make_dirt_tile(name, tile, palette)

    if name == "iron_block.png":
        return make_metal_block_tile(name, tile, (168, 172, 178), (108, 112, 120))

    if name == "sandstone.png":
        return make_wood_plank_tile(name, tile, (196, 168, 108), (148, 122, 78), (176, 148, 96))

    if name == "gold_block.png":
        return make_metal_block_tile(name, tile, (224, 188, 64), (168, 132, 28))

    if name == "willow_log.png":
        return make_wood_log_tile(name, tile, (68, 52, 38), (44, 32, 22), (92, 72, 50), 11)

    if name == "willow_leaves.png":
        return leaf_cluster_tile(name, tile, (58, 98, 62), [(42, 82, 48), (72, 118, 68), (36, 72, 42)])

    if name == "palm_log.png":
        img = make_wood_log_tile(name, tile, (168, 138, 88), (138, 108, 68), (198, 168, 118), 14)
        draw = ImageDraw.Draw(img)
        for y in range(6, tile, 14):
            draw.line((tile // 3, y, tile * 2 // 3, y + 4), fill=(128, 98, 58, 255), width=2)
        return img

    if name == "palm_leaves.png":
        palette = [(42, 98, 38), (58, 118, 48), (72, 142, 52), (36, 88, 34)]
        img = leaf_cluster_tile(name, tile, palette[1], palette)
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
        return make_wood_plank_tile(name, tile, (198, 188, 168), (158, 148, 128), (178, 168, 148))

    if name == "pine_plank.png":
        return make_wood_plank_tile(name, tile, (128, 96, 58), (92, 68, 40), (108, 82, 50))

    if name == "cobblestone.png":
        stones = [(96, 96, 100), (120, 120, 124), (88, 88, 92), (132, 132, 136)]
        return voronoi_tile(name, tile, stones, (64, 64, 68), 20, 3.2)

    if name == "brick.png":
        return make_brick_tile(name, tile, (148, 72, 52), (92, 88, 84), (168, 88, 62), (128, 58, 42))

    if name == "moss_stone.png":
        stone_palette = [(92, 98, 86), (108, 114, 100), (84, 90, 78)]
        img = voronoi_tile(name, tile, stone_palette, (68, 72, 62), 20, 3.2)
        moss_palette = [(48, 98, 42), (62, 118, 52), (38, 84, 36)]
        paint_blobs_pil(img, name + "_moss", moss_palette, 18, 4, 10)
        return img

    if name == "mud.png":
        palette = [(58, 46, 34), (72, 58, 42), (88, 72, 52), (64, 50, 38)]
        return make_dirt_tile(name, tile, palette)

    if name == "reed.png":
        palette = [(88, 98, 48), (98, 112, 52), (112, 128, 58)]
        head = (148, 128, 62)
        return pack_flora_variants(tile, lambda half, variant: make_reed_sprite(f"reed_v{variant}", half, palette, head))

    if name == "sunflower.png":
        stem = (42, 98, 38)
        petal = (238, 198, 42)
        center = (68, 48, 28)
        return pack_flora_variants(tile, lambda half, variant: make_sunflower_sprite(f"sunflower_v{variant}", half, stem, petal, center))

    if name == "hay_bale.png":
        palette = [(168, 138, 58), (188, 158, 68), (208, 178, 78)]
        img = fill_pixel_cluster_tile(name, tile, palette[1], palette, CELL_EARTH, 14)
        draw = ImageDraw.Draw(img)
        # Straw lines
        for y in range(4, tile, 6):
            length = tile // 2 + noise_value(name, y, 11) % (tile // 2)
            draw.line((0, y, length, y), fill=shade(palette[0], 12) + (255,), width=1)
            draw.line((length + 2, y, tile - 1, y), fill=shade(palette[2], -12) + (255,), width=1)
        # Ropes
        rope_color = (158, 88, 48)
        rope_hi = (208, 128, 68)
        rope_shadow = (108, 58, 28)
        rope1, rope2 = tile // 4, tile * 3 // 4
        draw.rectangle((rope1 - 3, 0, rope1 + 2, tile), fill=rope_shadow + (255,))
        draw.rectangle((rope1 - 2, 0, rope1 + 1, tile), fill=rope_color + (255,))
        draw.rectangle((rope1 - 1, 0, rope1, tile), fill=rope_hi + (255,))
        draw.rectangle((rope2 - 3, 0, rope2 + 2, tile), fill=rope_shadow + (255,))
        draw.rectangle((rope2 - 2, 0, rope2 + 1, tile), fill=rope_color + (255,))
        draw.rectangle((rope2 - 1, 0, rope2, tile), fill=rope_hi + (255,))
        apply_cell_rims(img, CELL_EARTH, -10, 5)
        return img

    if name == "ice.png":
        base_color = (168, 212, 238)
        palette = [base_color, shade(base_color, 12), shade(base_color, -10)]
        img = fill_pixel_cluster_tile(name, tile, base_color, palette, CELL_ORGANIC, 8)
        draw = ImageDraw.Draw(img)
        ice_crack = (220, 240, 255, 220)
        ice_shadow = (130, 180, 215, 180)
        draw.line((10, tile // 4, tile // 2, tile // 4 + 8), fill=ice_crack, width=2)
        draw.line((tile // 2, tile // 4 + 8, tile - 15, tile // 4 - 2), fill=ice_crack, width=1)
        draw.line((tile // 2, tile // 4 + 8, tile // 2 - 4, tile * 3 // 4), fill=ice_shadow, width=1)
        draw.line((tile * 3 // 4, 15, tile * 2 // 3, tile * 2 // 3), fill=ice_crack, width=1)
        draw.line((tile * 2 // 3, tile * 2 // 3, tile // 3, tile - 10), fill=ice_shadow, width=2)
        apply_cell_rims(img, CELL_ORGANIC, -6, 10)
        return img

    if name == "sheep_body.png":
        img = Image.new("RGBA", (tile, tile), (210, 210, 210, 255))
        draw = ImageDraw.Draw(img)
        for i in range(28):
            cx = noise_value(name, i, 3, 7) % tile
            cy = noise_value(name, i, 5, 9) % tile
            r = 10 + noise_value(name, i, 11, 13) % 8
            wool = (245, 245, 245) if i % 2 == 0 else (230, 230, 230)
            draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=shade(wool, -20) + (255,))
            draw.ellipse((cx - r + 1, cy - r + 1, cx + r - 1, cy + r - 1), fill=wool + (255,))
            draw.ellipse((cx - r + 2, cy - r + 2, cx + r // 4, cy + r // 4), fill=(255, 255, 255, 255))
        return img

    if name == "sheep_head.png":
        base, accent = (210, 210, 210), (170, 170, 170)
        img = fill_noisy_tile(name, tile, base, 8)
        draw = ImageDraw.Draw(img)
        margin = tile // 8
        draw.rectangle((margin, margin, tile - margin, tile - margin), outline=shade(accent, -20) + (255,), width=2)
        cx, cy = tile // 2, tile // 2
        eye_y = cy - tile // 16
        eye_offset = tile // 4
        # Left eye
        draw.rectangle((cx - eye_offset - 4, eye_y - 2, cx - eye_offset + 3, eye_y + 2), fill=(255, 255, 255, 255))
        draw.rectangle((cx - eye_offset - 2, eye_y - 2, cx - eye_offset + 1, eye_y + 2), fill=(0, 0, 0, 255))
        # Right eye
        draw.rectangle((cx + eye_offset - 4, eye_y - 2, cx + eye_offset + 3, eye_y + 2), fill=(255, 255, 255, 255))
        draw.rectangle((cx + eye_offset - 2, eye_y - 2, cx + eye_offset + 1, eye_y + 2), fill=(0, 0, 0, 255))
        # Snout
        draw.rectangle((cx - 6, cy + tile // 8, cx + 6, cy + tile // 8 + 6), fill=(240, 150, 160, 255))
        draw.point((cx - 2, cy + tile // 8 + 2), fill=(80, 30, 40, 255))
        draw.point((cx + 2, cy + tile // 8 + 2), fill=(80, 30, 40, 255))
        # Wool cap
        for x in range(margin, tile - margin, 6):
            r = 5 + noise_value(name, x, 73) % 4
            draw.ellipse((x - r, margin - r + 3, x + r, margin + r), fill=(245, 245, 245, 255))
        return img

    if name == "pig_body.png":
        base = (240, 170, 170)
        img = fill_noisy_tile(name, tile, base, 8)
        draw = ImageDraw.Draw(img)
        # Mud spots
        for i in range(4):
            cx = noise_value(name, i, 11, 13) % tile
            cy = noise_value(name, i, 17, 19) % tile
            r = 6 + noise_value(name, i, 23, 29) % 8
            color = (110, 80, 65) if i % 2 == 0 else shade(base, -26)
            draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=color + (255,))
            draw.ellipse((cx - r + 2, cy - r + 1, cx + r - 1, cy + r - 1), fill=shade(color, 8) + (255,))
        return img

    if name == "pig_head.png":
        base, accent = (230, 160, 160), (190, 110, 110)
        img = fill_noisy_tile(name, tile, base, 8)
        draw = ImageDraw.Draw(img)
        margin = tile // 8
        draw.rectangle((margin, margin, tile - margin, tile - margin), outline=shade(accent, -20) + (255,), width=2)
        cx, cy = tile // 2, tile // 2
        eye_y = cy - tile // 12
        eye_offset = tile // 5
        # Left eye
        draw.rectangle((cx - eye_offset - 3, eye_y - 2, cx - eye_offset + 2, eye_y + 2), fill=(255, 255, 255, 255))
        draw.rectangle((cx - eye_offset - 1, eye_y - 2, cx - eye_offset + 1, eye_y + 2), fill=(0, 0, 0, 255))
        # Right eye
        draw.rectangle((cx + eye_offset - 3, eye_y - 2, cx + eye_offset + 2, eye_y + 2), fill=(255, 255, 255, 255))
        draw.rectangle((cx + eye_offset - 1, eye_y - 2, cx + eye_offset + 1, eye_y + 2), fill=(0, 0, 0, 255))
        # Snout
        snout_w = tile // 3
        snout_h = tile // 5
        snout_color = shade(base, -22)
        draw.rectangle((cx - snout_w // 2, cy + 2, cx + snout_w // 2, cy + 2 + snout_h), fill=snout_color + (255,))
        draw.rectangle((cx - snout_w // 2, cy + 2, cx + snout_w // 2, cy + 2 + snout_h), outline=shade(snout_color, -20) + (255,), width=1)
        draw.rectangle((cx - snout_w // 4 - 1, cy + 2 + snout_h // 2 - 1, cx - snout_w // 4 + 1, cy + 2 + snout_h // 2 + 1), fill=(60, 20, 20, 255))
        draw.rectangle((cx + snout_w // 4 - 2, cy + 2 + snout_h // 2 - 1, cx + snout_w // 4, cy + 2 + snout_h // 2 + 1), fill=(60, 20, 20, 255))
        return img

    if name == "chicken_body.png":
        base = (240, 220, 120)
        img = Image.new("RGBA", (tile, tile), base + (255,))
        draw = ImageDraw.Draw(img)
        row_spacing = tile // 8
        for y in range(4, tile, row_spacing):
            offset = (y // row_spacing % 2) * (tile // 16)
            for x in range(-10, tile + 10, tile // 6):
                cx = x + offset
                cy = y
                r = tile // 10
                feather = shade(base, (y // row_spacing) * 3 - 8)
                draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=shade(feather, -18) + (255,))
                draw.ellipse((cx - r + 1, cy - r + 1, cx + r - 1, cy + r - 1), fill=feather + (255,))
                for tx in range(cx - r + 2, cx + r - 1):
                    draw.point((tx, cy + r - 1), fill=shade(feather, 22) + (255,))
        return img

    if name == "chicken_head.png":
        base, accent = (230, 210, 110), (190, 150, 50)
        img = fill_noisy_tile(name, tile, base, 8)
        draw = ImageDraw.Draw(img)
        margin = tile // 8
        draw.rectangle((margin, margin, tile - margin, tile - margin), outline=shade(accent, -20) + (255,), width=2)
        cx, cy = tile // 2, tile // 2
        eye_y = cy - tile // 8
        eye_offset = tile // 4
        # Left eye
        draw.rectangle((cx - eye_offset - 3, eye_y - 2, cx - eye_offset + 2, eye_y + 2), fill=(255, 255, 255, 255))
        draw.rectangle((cx - eye_offset - 1, eye_y - 2, cx - eye_offset + 1, eye_y + 2), fill=(0, 0, 0, 255))
        # Right eye
        draw.rectangle((cx + eye_offset - 3, eye_y - 2, cx + eye_offset + 2, eye_y + 2), fill=(255, 255, 255, 255))
        draw.rectangle((cx + eye_offset - 1, eye_y - 2, cx + eye_offset + 1, eye_y + 2), fill=(0, 0, 0, 255))
        # Beak
        beak_size = tile // 6
        beak_color = (255, 170, 30)
        draw.ellipse((cx - beak_size, cy - 2, cx + beak_size, cy + beak_size + 2), fill=beak_color + (255,))
        draw.line((cx - beak_size + 1, cy + 2, cx + beak_size - 1, cy + 2), fill=shade(beak_color, -24) + (255,), width=1)
        # Comb & wattle
        draw.rectangle((cx - 4, margin - 6, cx + 4, margin + 2), fill=(220, 30, 30, 255))
        draw.rectangle((cx - 3, cy + beak_size + 1, cx + 2, cy + beak_size + 7), fill=(220, 30, 30, 255))
        return img

    return None


def paint_blobs_pil(img: Image.Image, name: str, colors: list, count: int, min_r: int, max_r: int) -> None:
    draw = ImageDraw.Draw(img)
    tile = img.size[0]
    for i in range(count):
        cx = noise_value(name, i, 3, 7) % tile
        cy = noise_value(name, i, 5, 9) % tile
        rx = min_r + noise_value(name, i, 11, 13) % (max_r - min_r + 1)
        ry = min_r + noise_value(name, i, 15, 17) % (max_r - min_r + 1)
        color = colors[noise_value(name, i, 19, 21) % len(colors)]
        draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=color + (255,))
        draw.ellipse((cx - rx // 2, cy - ry // 2, cx + rx // 3, cy + ry // 3), fill=shade(color, 18) + (255,))


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
        draw.polygon(
            [
                (cx - 5 * s, cy - s),
                (cx - 3 * s, cy - 3 * s),
                (cx + 3 * s, cy - 3 * s),
                (cx + 5 * s, cy - s),
                (cx + 4 * s, cy),
                (cx, cy - 2 * s),
                (cx - 4 * s, cy)
            ],
            fill=head + (255,),
        )
        draw.line((cx - 4 * s, cy - 2 * s, cx + 4 * s, cy - 2 * s), fill=highlight + (255,), width=max(1, s // 2))
        draw.polygon(
            [(cx - 5 * s, cy - s), (cx - 6 * s, cy + s), (cx - 4 * s, cy)],
            fill=head_dark + (255,),
        )
        draw.polygon(
            [(cx + 5 * s, cy - s), (cx + 6 * s, cy + s), (cx + 4 * s, cy)],
            fill=head_dark + (255,),
        )
    elif tool_type == "axe":
        wood_handle(cx - s // 2, cy + 4 * s, cx - s // 2, cy - 2 * s, s)
        draw.rectangle((cx - s - 1, cy - 3 * s, cx + s, cy - s), fill=head_dark + (255,))
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
        # central crease
        draw.line((cx, cy - 3 * s, cx, cy + s), fill=head_dark + (255,), width=max(1, s // 3))
        draw.line((cx - 1, cy - 3 * s, cx - 1, cy + s), fill=highlight + (255,), width=max(1, s // 3))
    elif tool_type == "sword":
        draw.ellipse((cx - s, cy + 5 * s, cx + s, cy + 7 * s), fill=head_dark + (255,))
        draw.rectangle((cx - s // 2, cy + 3 * s, cx + s // 2, cy + 5 * s), fill=(100, 70, 40, 255))
        draw.polygon(
            [
                (cx - 3 * s, cy + s),
                (cx - 2 * s, cy + 2 * s),
                (cx + 2 * s, cy + 2 * s),
                (cx + 3 * s, cy + s),
                (cx, cy + 2 * s)
            ],
            fill=head_dark + (255,),
        )
        draw.rectangle((cx - s, cy - 4 * s, cx, cy + s), fill=head + (255,))
        draw.rectangle((cx, cy - 4 * s, cx + s, cy + s), fill=head_dark + (255,))
        draw.rectangle((cx - s, cy - 4 * s, cx - s + 1, cy + s), fill=highlight + (255,))
        draw.polygon([(cx, cy - 5 * s), (cx - s, cy - 4 * s), (cx, cy - 4 * s)], fill=highlight + (255,))
        draw.polygon([(cx, cy - 5 * s), (cx, cy - 4 * s), (cx + s, cy - 4 * s)], fill=head_dark + (255,))

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
