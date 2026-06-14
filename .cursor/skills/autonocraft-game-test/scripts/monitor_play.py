#!/usr/bin/env python3
"""
Real-time play monitor: moves the player around while streaming CPU/memory/FPS metrics.

Requires a running game in Playing state:
  ./run.sh --skip-menu --agent-port 5001 --debug-metrics

Usage:
  python3 monitor_play.py
  python3 monitor_play.py --duration 300 --port 5001
  python3 monitor_play.py --launch   # start game, monitor, then shutdown
"""

from __future__ import annotations

import argparse
import json
import math
import os
import signal
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
ROOT = SCRIPT_DIR.parents[3]
sys.path.insert(0, str(SCRIPT_DIR))

from game_client import GameClient, GameClientError, GameSession, Vec3


@dataclass
class Sample:
    t: float
    ok: bool
    fps: float = 0.0
    frame_ms: float = 0.0
    frame_peak: float = 0.0
    cpu: float = 0.0
    mem_mb: float = 0.0
    pending_mesh: int = 0
    chunks: int = 0
    pos: tuple[float, float, float] = (0.0, 0.0, 0.0)
    warmup: float = 0.0
    exceptions: int = 0
    crash_log: str = ""
    note: str = ""


def fetch_metrics(client: GameClient) -> dict:
    url = f"{client.base_url}/metrics"
    with urllib.request.urlopen(url, timeout=3.0) as resp:
        return json.loads(resp.read().decode("utf-8"))


def fetch_state(client: GameClient) -> dict:
    return client.state()


def fmt_sample(s: Sample) -> str:
    if not s.ok:
        return f"{s.t:6.1f}s  *** DISCONNECTED ***  {s.note}"
    return (
        f"{s.t:6.1f}s  fps={s.fps:5.1f}  frame={s.frame_ms:6.1f}ms peak={s.frame_peak:6.1f}ms  "
        f"cpu={s.cpu:5.1f}%  mem={s.mem_mb:6.0f}MB  mesh={s.pending_mesh:3d}  "
        f"chunks={s.chunks:3d}  warmup={s.warmup:4.1f}s  "
        f"pos=({s.pos[0]:6.1f},{s.pos[1]:5.1f},{s.pos[2]:6.1f})"
        + (f"  EX={s.exceptions}" if s.exceptions else "")
        + (f"  CRASH={s.crash_log}" if s.crash_log else "")
        + (f"  {s.note}" if s.note else "")
    )


def latest_crash_log() -> str:
    base = Path.home() / "Library/Application Support/Autonocraft/crashes"
    if not base.exists():
        base = Path.home() / ".local/share/Autonocraft/crashes"
    if not base.exists():
        return ""
    logs = sorted(base.glob("*.log"), key=lambda p: p.stat().st_mtime, reverse=True)
    return str(logs[0]) if logs else ""


class MovementController:
    """Background movement pattern to stress chunk streaming like real play."""

    def __init__(self, client: GameClient):
        self.client = client
        self._stop = threading.Event()
        self._thread: threading.Thread | None = None

    def start(self) -> None:
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        try:
            self.client.release_keys()
        except GameClientError:
            pass
        if self._thread:
            self._thread.join(timeout=3.0)

    def _run(self) -> None:
        try:
            self.client.set_flying(True)
            self.client.set_look(0, -15)
            waypoints = [
                Vec3(16, 72, 16),
                Vec3(48, 72, 16),
                Vec3(48, 72, 48),
                Vec3(16, 72, 48),
                Vec3(-16, 72, -16),
                Vec3(-48, 72, 16),
                Vec3(0, 80, 0),
                Vec3(64, 90, 64),
            ]
            step = 0
            while not self._stop.is_set():
                wp = waypoints[step % len(waypoints)]
                step += 1
                self.client.teleport(wp.x, wp.y, wp.z)
                self.client.set_look((step * 45) % 360, -20)
                # Walk forward in each direction to trigger streaming
                for yaw in (0, 90, 180, 270):
                    if self._stop.is_set():
                        break
                    self.client.set_look(float(yaw), -25)
                    self.client.key_down("w")
                    for _ in range(20):
                        if self._stop.is_set():
                            break
                        self.client.look(dx=8, dy=0)
                        time.sleep(0.1)
                    self.client.key_up("w")
                    time.sleep(0.15)
                # Brief ground mode to exercise physics
                self.client.set_flying(False)
                self.client.teleport(wp.x, max(65, wp.y - 5), wp.z)
                self.client.key_down("w")
                time.sleep(0.8)
                self.client.key_up("w")
                self.client.set_flying(True)
        except GameClientError as e:
            print(f"[movement] stopped: {e}", flush=True)


def launch_game(port: int) -> subprocess.Popen:
    env = os.environ.copy()
    env["DYLD_LIBRARY_PATH"] = "/opt/homebrew/lib" + (
        f":{env['DYLD_LIBRARY_PATH']}" if env.get("DYLD_LIBRARY_PATH") else ""
    )
    log_path = ROOT / "test_output" / "monitor_game.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)
    log_file = open(log_path, "w")
    proc = subprocess.Popen(
        [
            "dotnet", "run", "--project", str(ROOT / "src/Autonocraft"),
            "--no-build", "--",
            "--skip-menu", "--agent-port", str(port), "--debug-metrics",
        ],
        cwd=str(ROOT),
        env=env,
        stdout=log_file,
        stderr=subprocess.STDOUT,
    )
    print(f"Launched game pid={proc.pid} log={log_path}")
    return proc


def monitor(args: argparse.Namespace) -> int:
    client = GameClient(port=args.port)
    game_proc: subprocess.Popen | None = None

    if args.launch:
        game_proc = launch_game(args.port)
        time.sleep(2)

    print(f"Waiting for world at {client.base_url} (timeout={args.wait}s)...")
    try:
        with GameSession(client, wait_timeout=args.wait) as session:
            print("World ready — starting movement + metrics stream")
            print("-" * 120)
            mover = MovementController(client)
            mover.start()

            start = time.time()
            failures = 0
            peak_frame = 0.0
            peak_mem = 0.0
            last_ok = start

            try:
                while True:
                    elapsed = time.time() - start
                    if elapsed >= args.duration:
                        break

                    sample = Sample(t=elapsed, ok=False)
                    try:
                        metrics = fetch_metrics(client)
                        state = fetch_state(client)
                        pos = state.get("position", {})
                        sample.ok = True
                        sample.fps = float(metrics.get("fps", 0))
                        sample.frame_ms = float(metrics.get("frameMsLast", 0))
                        sample.frame_peak = float(metrics.get("frameMsPeak", 0))
                        sample.cpu = float(metrics.get("cpuPercent", 0))
                        sample.mem_mb = float(metrics.get("memoryWorkingSetMb", 0))
                        sample.pending_mesh = int(metrics.get("pendingMesh", 0))
                        sample.chunks = int(metrics.get("activeChunks", 0))
                        sample.warmup = float(metrics.get("spawnWarmupRemaining", 0))
                        sample.exceptions = int(metrics.get("managedExceptions", 0))
                        sample.crash_log = str(metrics.get("latestCrashLog", "") or "")
                        sample.pos = (
                            float(pos.get("x", 0)),
                            float(pos.get("y", 0)),
                            float(pos.get("z", 0)),
                        )
                        peak_frame = max(peak_frame, sample.frame_ms)
                        peak_mem = max(peak_mem, sample.mem_mb)
                        last_ok = time.time()
                        failures = 0

                        if sample.frame_ms >= args.lag_threshold_ms:
                            sample.note = "LAG SPIKE"
                        if sample.pending_mesh >= args.mesh_alert:
                            sample.note = (sample.note + " HIGH_MESH").strip()

                    except (GameClientError, urllib.error.URLError, TimeoutError, json.JSONDecodeError) as e:
                        failures += 1
                        sample.note = str(e)
                        crash = latest_crash_log()
                        if crash:
                            sample.crash_log = crash

                    print(fmt_sample(sample), flush=True)

                    if not sample.ok:
                        if failures >= args.disconnect_limit:
                            print(
                                f"\nGame lost after {time.time() - last_ok:.1f}s without response "
                                f"(>{args.disconnect_limit} consecutive failures)",
                                flush=True,
                            )
                            if sample.crash_log:
                                print(f"Latest crash log: {sample.crash_log}", flush=True)
                                try:
                                    print(Path(sample.crash_log).read_text()[-4000:], flush=True)
                                except OSError:
                                    pass
                            return 1
                    time.sleep(args.interval)

            finally:
                mover.stop()

            print("-" * 120)
            print(
                f"Completed {args.duration:.0f}s monitor. "
                f"peak_frame={peak_frame:.1f}ms peak_mem={peak_mem:.0f}MB"
            )
            return 0

    except GameClientError as e:
        print(f"Failed to connect: {e}", flush=True)
        return 1
    finally:
        if game_proc and args.launch:
            print("Shutting down launched game...")
            game_proc.send_signal(signal.SIGTERM)
            try:
                game_proc.wait(timeout=8)
            except subprocess.TimeoutExpired:
                game_proc.kill()


def main() -> int:
    parser = argparse.ArgumentParser(description="Move around in-world and stream live metrics")
    parser.add_argument("--port", type=int, default=5001)
    parser.add_argument("--duration", type=float, default=180.0, help="Monitor duration in seconds")
    parser.add_argument("--interval", type=float, default=0.5, help="Metrics poll interval")
    parser.add_argument("--wait", type=float, default=180.0, help="Seconds to wait for world load")
    parser.add_argument("--launch", action="store_true", help="Start the game process automatically")
    parser.add_argument("--lag-threshold-ms", type=float, default=50.0)
    parser.add_argument("--mesh-alert", type=int, default=32)
    parser.add_argument("--disconnect-limit", type=int, default=6)
    args = parser.parse_args()
    return monitor(args)


if __name__ == "__main__":
    raise SystemExit(main())
