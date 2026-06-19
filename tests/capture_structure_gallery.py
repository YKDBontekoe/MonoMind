#!/usr/bin/env python3
"""Capture front-facing screenshots of every structure in the gallery world."""
from __future__ import annotations

import json
import sys
import time
import urllib.error
from pathlib import Path

# Reuse the shared HTTP helper used by other test scripts.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from interact import build_base_url, query_api, wait_for_ready  # noqa: E402

DEFAULT_PORT = 5010
OUT_DIR = Path("test_output/structure_gallery")


def action(base_url: str, cmd: str, **params) -> dict:
    payload = {"cmd": cmd, **params}
    data, is_binary, _status = query_api(base_url, "/action", method="POST", params=payload)
    if is_binary:
        raise RuntimeError(f"/action returned PNG instead of JSON for cmd={cmd}")
    return json.loads(data)


def screenshot(base_url: str, out_path: Path) -> bytes:
    data, is_binary, _status = query_api(base_url, "/screenshot")
    if not is_binary:
        raise RuntimeError(f"/screenshot did not return PNG: {data}")
    out_path.write_bytes(data)
    return data


def main() -> int:
    port = int(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_PORT
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    base = build_base_url("127.0.0.1", port)

    print(f"Waiting for agent API on port {port}...")
    wait_for_ready(base)

    for close_cmd in ("close_village", "close_village_ui"):
        try:
            action(base, close_cmd)
        except urllib.error.HTTPError:
            pass

    action(base, "set_creative", creative="true")
    action(base, "set_time", value="0.35")
    action(base, "dev", cmd_line="time noon")
    action(base, "release_keys")
    time.sleep(0.5)

    catalog_raw, is_binary, _status = query_api(base, "/structures")
    if is_binary:
        raise RuntimeError("/structures returned PNG instead of JSON")
    catalog = json.loads(catalog_raw)
    structures = catalog["structures"]
    print(f"Capturing {len(structures)} structures...")

    manifest = []
    for entry in structures:
        sid = entry["id"]
        anchor = entry["anchor"]
        ax, ay, az = anchor["x"], anchor["y"], anchor["z"]
        radius = entry.get("footprintRadius", 2)
        dist = max(10, radius * 2 + 6)
        cam_y = ay + max(6, radius + 4)
        cam_z = max(2, az - dist)

        action(base, "teleport", x=str(ax + 0.5), y=str(float(cam_y)), z=str(cam_z + 0.5))
        action(base, "set_look", yaw="270", pitch="-15")
        time.sleep(1.0)

        out_path = OUT_DIR / f"{sid}.png"
        png = screenshot(base, out_path)
        print(f"  {sid}: {out_path} ({len(png)} bytes)")
        manifest.append({"id": sid, "path": str(out_path), "anchor": anchor})

    (OUT_DIR / "manifest.json").write_text(json.dumps(manifest, indent=2))
    overview_path = OUT_DIR / "overview.png"
    min_x = min(s["anchor"]["x"] for s in structures)
    max_x = max(s["anchor"]["x"] for s in structures)
    min_z = min(s["anchor"]["z"] for s in structures)
    max_z = max(s["anchor"]["z"] for s in structures)
    center_x = (min_x + max_x) / 2
    center_z = (min_z + max_z) / 2
    action(base, "teleport", x=str(center_x + 0.5), y="130", z=str(center_z + 0.5))
    action(base, "set_look", yaw="0", pitch="-85")
    time.sleep(0.5)
    png = screenshot(base, overview_path)
    print(f"Overview: {overview_path} ({len(png)} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
