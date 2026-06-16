#!/usr/bin/env python3
"""Scan nearby terrain columns via debug/slabscan and capture verification screenshots."""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

DEFAULT_PORT = 5001
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "test_output" / "slab_verify"
PORT = DEFAULT_PORT
BASE = f"http://127.0.0.1:{PORT}"


def http_get(path: str, timeout: float = 5.0) -> dict:
    with urllib.request.urlopen(f"{BASE}{path}", timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def http_action(params: dict, timeout: float = 10.0) -> dict:
    query = "&".join(f"{k}={urllib.parse.quote(str(v))}" for k, v in params.items())
    req = urllib.request.Request(f"{BASE}/action?{query}", method="POST")
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def wait_ready(max_wait: float = 120.0) -> None:
    deadline = time.time() + max_wait
    while time.time() < deadline:
        try:
            state = http_get("/health", timeout=2.0)
            if state.get("ready"):
                return
        except (urllib.error.URLError, TimeoutError):
            pass
        time.sleep(1.0)
    raise RuntimeError("Game agent API did not become ready in time")


def screenshot(name: str) -> Path:
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / name
    with urllib.request.urlopen(f"{BASE}/screenshot?path={path}", timeout=30) as resp:
        resp.read()
    return path


def teleport(x: float, y: float, z: float) -> None:
    http_action({"cmd": "teleport", "x": x, "y": y, "z": z})


def look(yaw: float, pitch: float) -> None:
    http_action({"cmd": "set_look", "yaw": yaw, "pitch": pitch})


def slab_scan(radius: int = 12) -> dict:
    return http_get(f"/debug/slabscan?radius={radius}", timeout=60.0)


def main() -> int:
    global PORT, BASE

    parser = argparse.ArgumentParser(description="Verify terrain slab placement in-game.")
    parser.add_argument("--port", type=int, default=int(os.environ.get("AGENT_PORT", DEFAULT_PORT)))
    args = parser.parse_args()
    PORT = args.port
    BASE = f"http://127.0.0.1:{PORT}"

    print("Waiting for agent API...")
    wait_ready()

    state = http_get("/state")
    print(f"Player at {state['position']}, seed={state.get('worldSeed')}")

    checks = [
        ("plains_spawn.png", 16.5, 72.0, 16.5, 45.0, -35.0),
        ("mountain_high.png", 11.5, 80.0, 84.5, 200.0, -25.0),
        ("mountain_cliff.png", -3.5, 72.0, 44.5, 90.0, -20.0),
    ]

    reports = []
    for name, x, y, z, yaw, pitch in checks:
        teleport(x, y, z)
        time.sleep(0.5)
        look(yaw, pitch)
        time.sleep(0.5)
        path = screenshot(name)
        scan = slab_scan()
        reports.append({"screenshot": str(path), "position": {"x": x, "y": y, "z": z}, "scan": scan})
        print(
            f"{name}: slabs={scan.get('slabCount', '?')} "
            f"stoneSlabs={scan.get('stoneSlabCount', '?')} "
            f"snowSlabs={scan.get('snowSlabCount', '?')} "
            f"maxY={scan.get('maxSlabY', '?')}"
        )

    report_path = OUT / "report.json"
    report_path.write_text(json.dumps(reports, indent=2), encoding="utf-8")
    print(f"Wrote {report_path}")

    for report in reports:
        scan = report["scan"]
        if scan.get("stoneSlabCount", 0) > 0 or scan.get("snowSlabCount", 0) > 0:
            print(f"FAILED: forbidden mountain slabs near {report['position']}", file=sys.stderr)
            return 1
        if scan.get("maxSlabY", 0) > 71:
            print(f"FAILED: slab above lowland cap near {report['position']}", file=sys.stderr)
            return 1

    print("In-game slab verification passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
