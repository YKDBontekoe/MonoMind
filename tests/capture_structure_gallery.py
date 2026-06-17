#!/usr/bin/env python3
"""Capture front-facing screenshots of every structure in the gallery world."""
from __future__ import annotations

import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

DEFAULT_PORT = 5010
OUT_DIR = Path("test_output/structure_gallery")


def base_url(port: int) -> str:
    return f"http://127.0.0.1:{port}"


def request(port: int, endpoint: str, method: str = "GET", params: dict | None = None, timeout: float = 30):
    url = base_url(port) + endpoint
    if params:
        url += "?" + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, method=method, data=b"" if method == "POST" else None)
    if method == "POST":
        req.add_header("Content-Length", "0")
    with urllib.request.urlopen(req, timeout=timeout) as response:
        content_type = response.headers.get_content_type()
        body = response.read()
        if content_type == "image/png":
            return body, True
        return json.loads(body.decode("utf-8")), False


def wait_ready(port: int, timeout: float = 120) -> None:
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            health, _ = request(port, "/health", timeout=2)
            if health.get("ready"):
                return
        except (urllib.error.URLError, TimeoutError):
            pass
        time.sleep(0.5)
    raise TimeoutError(f"Agent API on port {port} not ready after {timeout}s")


def action(port: int, cmd: str, **params) -> dict:
    payload = {"cmd": cmd, **params}
    result, _ = request(port, "/action", method="POST", params=payload)
    return result


def main() -> int:
    port = int(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_PORT
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    print(f"Waiting for agent API on port {port}...")
    wait_ready(port)

    for close_cmd in ("close_village", "close_village_ui"):
        try:
            action(port, close_cmd)
        except urllib.error.HTTPError:
            pass

    action(port, "set_creative", creative="true")
    action(port, "set_time", value="0.35")
    action(port, "dev", cmd_line="time noon")
    action(port, "release_keys")
    time.sleep(0.5)

    catalog, _ = request(port, "/structures")
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

        action(port, "teleport", x=str(ax + 0.5), y=str(float(cam_y)), z=str(cam_z + 0.5))
        action(port, "set_look", yaw="270", pitch="-15")
        time.sleep(1.0)

        out_path = OUT_DIR / f"{sid}.png"
        png, is_png = request(port, "/screenshot", params={"path": str(out_path.resolve())})
        if is_png:
            out_path.write_bytes(png)
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
    action(port, "teleport", x=str(center_x + 0.5), y="130", z=str(center_z + 0.5))
    action(port, "set_look", yaw="0", pitch="-85")
    time.sleep(0.5)
    png, is_png = request(port, "/screenshot", params={"path": str(overview_path.resolve())})
    if is_png:
        overview_path.write_bytes(png)
    print(f"Overview: {overview_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
