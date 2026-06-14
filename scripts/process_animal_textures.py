#!/usr/bin/env python3
import os
import glob
import sys
from PIL import Image

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(SCRIPT_DIR)
TARGET_DIR = os.path.join(REPO_ROOT, "src", "Autonocraft", "base_textures")

# Optional: pass source directory as first argument (defaults to base_textures itself for re-processing)
DEFAULT_SOURCE_DIR = TARGET_DIR


def crop_dark_border(img):
    width, height = img.size
    pixels = img.load()

    top = 0
    while top < height // 4:
        dark_count = sum(1 for x in range(width) if sum(pixels[x, top][:3]) < 120)
        if dark_count > width * 0.7:
            top += 1
        else:
            break

    bottom = height - 1
    while bottom > 3 * height // 4:
        dark_count = sum(1 for x in range(width) if sum(pixels[x, bottom][:3]) < 120)
        if dark_count > width * 0.7:
            bottom -= 1
        else:
            break

    left = 0
    while left < width // 4:
        dark_count = sum(1 for y in range(height) if sum(pixels[left, y][:3]) < 120)
        if dark_count > height * 0.7:
            left += 1
        else:
            break

    right = width - 1
    while right > 3 * width // 4:
        dark_count = sum(1 for y in range(height) if sum(pixels[right, y][:3]) < 120)
        if dark_count > height * 0.7:
            right -= 1
        else:
            break

    if top > 0 or bottom < height - 1 or left > 0 or right < width - 1:
        print(f"  Cropping borders: L={left}, T={top}, R={right}, B={bottom}")
        return img.crop((left, top, right + 1, bottom + 1))
    return img


def main():
    source_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SOURCE_DIR
    names = ["sheep_body", "sheep_head", "pig_body", "pig_head", "chicken_body", "chicken_head"]

    os.makedirs(TARGET_DIR, exist_ok=True)

    for name in names:
        pattern = os.path.join(source_dir, f"{name}*.png")
        matches = glob.glob(pattern)
        if not matches:
            print(f"Warning: No source image found for {name} in {source_dir}")
            continue

        matches.sort()
        src_path = matches[-1]
        print(f"Processing {name} from {os.path.basename(src_path)}...")

        img = Image.open(src_path).convert("RGBA")
        img = crop_dark_border(img)
        img_resized = img.resize((256, 256), Image.Resampling.NEAREST)

        dest_path = os.path.join(TARGET_DIR, f"{name}.png")
        img_resized.save(dest_path)
        print(f"  Saved to {dest_path}")


if __name__ == "__main__":
    main()
