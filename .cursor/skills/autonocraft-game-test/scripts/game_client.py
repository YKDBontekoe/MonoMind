#!/usr/bin/env python3
"""HTTP client for the Autonocraft Agent API. Stdlib only."""

from __future__ import annotations

import argparse
import json
import math
import struct
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable


DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 5001
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
MOVEMENT_KEYS = frozenset({"w", "a", "s", "d", "space", "shift"})


class GameClientError(Exception):
    """Raised when the agent API returns an error or connection fails."""


class AssertionError(GameClientError):
    """Raised when a state assertion fails during scripted testing."""


@dataclass(frozen=True)
class Vec3:
    x: float
    y: float
    z: float

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> Vec3:
        return cls(float(data["x"]), float(data["y"]), float(data["z"]))

    def distance_to(self, other: Vec3) -> float:
        return math.sqrt(
            (self.x - other.x) ** 2 + (self.y - other.y) ** 2 + (self.z - other.z) ** 2
        )

    def as_dict(self) -> dict[str, float]:
        return {"x": self.x, "y": self.y, "z": self.z}


@dataclass
class GameSnapshot:
    raw: dict[str, Any]

    @property
    def game_state(self) -> str:
        return str(self.raw.get("gameState", ""))

    @property
    def position(self) -> Vec3:
        return Vec3.from_dict(self.raw.get("position", {"x": 0, "y": 0, "z": 0}))

    @property
    def velocity(self) -> Vec3:
        return Vec3.from_dict(self.raw.get("velocity", {"x": 0, "y": 0, "z": 0}))

    @property
    def health(self) -> int:
        return int(self.raw.get("health", 0))

    @property
    def creative(self) -> bool:
        return bool(self.raw.get("creativeMode", self.raw.get("flyingMode", False)))

    @property
    def flying(self) -> bool:
        return self.creative

    @property
    def grounded(self) -> bool:
        return bool(self.raw.get("isGrounded", False))

    @property
    def selected_slot(self) -> int:
        return int(self.raw.get("selectedSlot", 0))

    @property
    def hotbar(self) -> list[dict[str, Any]]:
        return list(self.raw.get("hotbar", []))

    @property
    def target_block(self) -> dict[str, Any] | None:
        return self.raw.get("targetBlock")

    @property
    def village(self) -> dict[str, Any] | None:
        return self.raw.get("village")

    @property
    def animals(self) -> list[dict[str, Any]]:
        return list(self.raw.get("animals", []))

    @property
    def villagers(self) -> list[dict[str, Any]]:
        return list(self.raw.get("villagers", []))


class GameClient:
    """Thin but complete wrapper around http://localhost:5000/."""

    def __init__(
        self,
        host: str = DEFAULT_HOST,
        port: int = DEFAULT_PORT,
        timeout: float = 15.0,
        action_delay: float = 0.05,
    ):
        self.base_url = f"http://{host}:{port}"
        self.timeout = timeout
        self.action_delay = action_delay

    # --- HTTP core ---

    def _request(
        self,
        endpoint: str,
        method: str = "GET",
        params: dict[str, str] | None = None,
        body: bytes | None = None,
        headers: dict[str, str] | None = None,
        timeout: float | None = None,
    ) -> tuple[Any, bool, int]:
        url = f"{self.base_url}{endpoint}"
        if params:
            url = f"{url}?{urllib.parse.urlencode(params)}"

        req_headers = dict(headers or {})
        req = urllib.request.Request(url, data=body, method=method, headers=req_headers)
        try:
            with urllib.request.urlopen(req, timeout=timeout or self.timeout) as response:
                content_type = response.headers.get_content_type()
                if content_type == "image/png":
                    return response.read(), True, response.status
                text = response.read().decode("utf-8")
                return json.loads(text) if text else {}, False, response.status
        except urllib.error.HTTPError as e:
            raw = e.read().decode("utf-8", errors="replace")
            try:
                detail = json.loads(raw)
            except json.JSONDecodeError:
                detail = {"error": raw or str(e)}
            raise GameClientError(f"HTTP {e.code}: {detail}") from e
        except urllib.error.URLError as e:
            raise GameClientError(
                f"Cannot connect to {self.base_url}. Start the game with --skip-menu."
            ) from e

    def _action_ok(self, result: dict[str, Any], cmd: str) -> dict[str, Any]:
        if not result.get("success", True):
            raise GameClientError(f"Action '{cmd}' failed: {result}")
        time.sleep(self.action_delay)
        return result

    # --- Read endpoints ---

    def health(self) -> dict[str, Any]:
        try:
            data, _, status = self._request("/health", timeout=3.0)
            return {"http_status": status, **data}
        except GameClientError as e:
            if "HTTP 503" in str(e):
                return {"ready": False, "gameState": "Loading"}
            raise

    def metrics(self) -> dict[str, Any]:
        return self._request("/metrics")[0]

    def is_ready(self) -> bool:
        return bool(self.health().get("ready"))

    def wait_ready(self, timeout: float = 120.0, poll_interval: float = 0.5) -> dict[str, Any]:
        deadline = time.time() + timeout
        last: dict[str, Any] = {"ready": False, "gameState": "unknown"}
        while time.time() < deadline:
            try:
                data, _, _ = self._request("/health", timeout=3.0)
                last = data
                if data.get("ready"):
                    return data
            except GameClientError as e:
                if "HTTP 503" not in str(e) and "Cannot connect" not in str(e):
                    raise
            time.sleep(poll_interval)
        raise GameClientError(f"Timed out after {timeout}s (last={last})")

    def state(self) -> dict[str, Any]:
        return self._request("/state")[0]

    def snapshot(self) -> GameSnapshot:
        return GameSnapshot(self.state())

    def screenshot(self, path: str | Path = "screenshot.png", min_bytes: int = 1000) -> Path:
        path = Path(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        # Agent API only accepts paths under AppContext.BaseDirectory/screenshots/.
        server_path = path.name
        data, is_binary, _ = self._request("/screenshot", params={"path": server_path})
        if not is_binary:
            raise GameClientError(f"Screenshot failed: {data}")
        path.write_bytes(data)
        validate_png(path, min_bytes=min_bytes)
        return path

    # --- Action endpoints ---

    def action(self, cmd: str, **params: str | int | float | bool) -> dict[str, Any]:
        query: dict[str, str] = {"cmd": cmd}
        for key, value in params.items():
            if value is None:
                continue
            query[key] = str(value).lower() if isinstance(value, bool) else str(value)
        return self._request("/action", method="POST", params=query)[0]

    def action_ok(self, cmd: str, **params: str | int | float | bool) -> dict[str, Any]:
        return self._action_ok(self.action(cmd, **params), cmd)

    def key_down(self, key: str) -> dict[str, Any]:
        return self.action_ok("key_down", key=key)

    def key_up(self, key: str) -> dict[str, Any]:
        return self.action_ok("key_up", key=key)

    def release_keys(self) -> dict[str, Any]:
        return self.action_ok("release_keys")

    def click(self, button: str = "left", times: int = 1, interval: float = 0.1) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for i in range(times):
            result = self.action_ok("click", button=button)
            if i + 1 < times:
                time.sleep(interval)
        return result

    def look(self, dx: float = 0, dy: float = 0) -> dict[str, Any]:
        return self.action_ok("look", dx=dx, dy=dy)

    def set_look(self, yaw: float, pitch: float) -> dict[str, Any]:
        return self.action_ok("set_look", yaw=yaw, pitch=pitch)

    def teleport(self, x: float, y: float, z: float) -> dict[str, Any]:
        return self.action_ok("teleport", x=x, y=y, z=z)

    def teleport_vec(self, pos: Vec3) -> dict[str, Any]:
        return self.teleport(pos.x, pos.y, pos.z)

    def set_creative(self, creative: bool) -> dict[str, Any]:
        return self.action_ok("set_creative", creative=creative)

    def set_flying(self, flying: bool) -> dict[str, Any]:
        return self.set_creative(flying)

    def select_slot(self, slot: int) -> dict[str, Any]:
        return self.action_ok("select_slot", slot=slot)

    def set_time(self, value: float) -> dict[str, Any]:
        return self.action_ok("set_time", value=value)

    def set_time_scale(self, value: float) -> dict[str, Any]:
        return self.action_ok("set_time_scale", value=value)

    def open_crucible(self) -> dict[str, Any]:
        return self._action_ok(self.action("open_crucible"), "open_crucible")

    def recruit_villager(self) -> dict[str, Any]:
        return self._action_ok(self.action("recruit_villager"), "recruit_villager")

    def assign_job(
        self,
        villager_id: int,
        job: str,
        target: Vec3 | None = None,
    ) -> dict[str, Any]:
        params: dict[str, str | int | float | bool] = {
            "villager_id": villager_id,
            "job": job,
        }
        if target is not None:
            params["target_x"] = target.x
            params["target_y"] = target.y
            params["target_z"] = target.z
        return self._action_ok(self.action("assign_job", **params), "assign_job")

    def dev(self, cmd_line: str) -> dict[str, Any]:
        return self._action_ok(self.action("dev", cmd_line=cmd_line), "dev")

    def shutdown(self) -> dict[str, Any]:
        return self.action("shutdown")

    def village_chat(
        self,
        message: str,
        target: str = "mayor",
        timeout: float = 35.0,
    ) -> dict[str, Any]:
        body = json.dumps({"message": message, "target": target}).encode("utf-8")
        return self._request(
            "/village/chat",
            method="POST",
            body=body,
            headers={"Content-Type": "application/json"},
            timeout=timeout,
        )[0]

    def village_chat_confirm(self) -> dict[str, Any]:
        return self._request(
            "/village/chat/confirm",
            method="POST",
            body=b"{}",
            headers={"Content-Type": "application/json"},
        )[0]

    # --- High-level helpers ---

    def move(
        self,
        *,
        forward: float = 0,
        back: float = 0,
        left: float = 0,
        right: float = 0,
        jump: bool = False,
        sprint: bool = False,
        keys: list[str] | None = None,
        seconds: float | None = None,
    ) -> None:
        if keys is None:
            keys = []
            duration = seconds
            if forward:
                keys.append("w")
                duration = duration or forward
            if back:
                keys.append("s")
                duration = duration or back
            if left:
                keys.append("a")
                duration = duration or left
            if right:
                keys.append("d")
                duration = duration or right
            if jump:
                keys.append("space")
            if sprint:
                keys.append("shift")
            seconds = duration if duration is not None else 1.0
        else:
            seconds = seconds if seconds is not None else 1.0

        if not keys:
            return

        for key in keys:
            self.key_down(key)
        time.sleep(seconds)
        for key in keys:
            self.key_up(key)

    def give_block(self, block_type: str, count: int = 64) -> dict[str, Any]:
        return self.dev(f"give {block_type} {count}")

    def give_tool(self, tool: str = "pickaxe", tier: str = "stone") -> dict[str, Any]:
        return self.dev(f"give tool {tool} {tier}")

    def spawn_animal(self, animal_type: str, count: int = 1) -> dict[str, Any]:
        return self.dev(f"spawn {animal_type} {count}")

    def unlock_recipe(self, recipe_id: str) -> dict[str, Any]:
        return self.dev(f"unlock {recipe_id}")

    def fly_to(self, pos: Vec3) -> None:
        self.set_creative(True)
        self.teleport_vec(pos)

    def look_at_ground(self, yaw: float = 0, pitch: float = -60) -> dict[str, Any]:
        return self.set_look(yaw, pitch)

    def find_hotbar_slot(
        self,
        *,
        block_type: str | None = None,
        tool_contains: str | None = None,
    ) -> int | None:
        snap = self.snapshot()
        for slot in snap.hotbar:
            if block_type and slot.get("kind") == "block" and slot.get("type") == block_type:
                return int(slot["slot"])
            if tool_contains and slot.get("kind") == "tool":
                if tool_contains.lower() in str(slot.get("toolId", "")).lower():
                    return int(slot["slot"])
        return None

    def select_tool(self, tool_contains: str = "pickaxe") -> int:
        slot = self.find_hotbar_slot(tool_contains=tool_contains)
        if slot is None:
            raise GameClientError(f"No hotbar tool matching '{tool_contains}'")
        self.select_slot(slot)
        return slot

    def mine_block(
        self,
        clicks: int = 30,
        interval: float = 0.15,
        require_target: bool = True,
    ) -> GameSnapshot:
        snap = self.snapshot()
        if require_target and not snap.target_block:
            raise GameClientError("No target block — adjust look/position first")
        for i in range(clicks):
            self.click("left")
            if i + 1 < clicks:
                time.sleep(interval)
        return self.snapshot()

    def place_block(self, times: int = 1) -> GameSnapshot:
        self.click("right", times=times)
        return self.snapshot()

    def wait_for(
        self,
        predicate: Callable[[GameSnapshot], bool],
        timeout: float = 10.0,
        poll_interval: float = 0.2,
        description: str = "condition",
    ) -> GameSnapshot:
        deadline = time.time() + timeout
        last = self.snapshot()
        while time.time() < deadline:
            last = self.snapshot()
            if predicate(last):
                return last
            time.sleep(poll_interval)
        raise GameClientError(f"Timed out waiting for {description}: {last.raw}")

    def wait_position_change(
        self,
        from_pos: Vec3,
        min_distance: float = 0.5,
        timeout: float = 5.0,
    ) -> GameSnapshot:
        return self.wait_for(
            lambda s: s.position.distance_to(from_pos) >= min_distance,
            timeout=timeout,
            description=f"position change >= {min_distance}",
        )

    def wait_mining_progress(self, min_progress: float = 0.1, timeout: float = 5.0) -> GameSnapshot:
        def _mining(s: GameSnapshot) -> bool:
            tb = s.target_block
            return tb is not None and float(tb.get("breakProgress", 0)) >= min_progress

        return self.wait_for(_mining, timeout=timeout, description=f"mining progress >= {min_progress}")

    def assert_state(
        self,
        *,
        health_min: int | None = None,
        health_max: int | None = None,
        flying: bool | None = None,
        grounded: bool | None = None,
        has_village: bool | None = None,
        hotbar_block: str | None = None,
        hotbar_tool: str | None = None,
        nearby_station: str | None = None,
        position_near: tuple[Vec3, float] | None = None,
        target_block_type: str | None = None,
    ) -> GameSnapshot:
        snap = self.snapshot()
        if health_min is not None and snap.health < health_min:
            raise AssertionError(f"health {snap.health} < {health_min}")
        if health_max is not None and snap.health > health_max:
            raise AssertionError(f"health {snap.health} > {health_max}")
        if flying is not None and snap.creative != flying:
            raise AssertionError(f"creativeMode expected {flying}, got {snap.creative}")
        if grounded is not None and snap.grounded != grounded:
            raise AssertionError(f"isGrounded expected {grounded}, got {snap.grounded}")
        if has_village is not None:
            has = snap.village is not None
            if has != has_village:
                raise AssertionError(f"village expected {has_village}, got {has}")
        if hotbar_block is not None:
            if not any(
                s.get("kind") == "block" and s.get("type") == hotbar_block for s in snap.hotbar
            ):
                raise AssertionError(f"hotbar missing block {hotbar_block}")
        if hotbar_tool is not None:
            if not any(
                s.get("kind") == "tool" and hotbar_tool.lower() in str(s.get("toolId", "")).lower()
                for s in snap.hotbar
            ):
                raise AssertionError(f"hotbar missing tool matching {hotbar_tool}")
        if nearby_station is not None:
            actual = snap.raw.get("nearbyStation")
            if actual != nearby_station:
                raise AssertionError(f"nearbyStation expected {nearby_station}, got {actual}")
        if position_near is not None:
            target, tol = position_near
            dist = snap.position.distance_to(target)
            if dist > tol:
                raise AssertionError(
                    f"position {snap.position.as_dict()} not within {tol} of {target.as_dict()} (dist={dist:.2f})"
                )
        if target_block_type is not None:
            tb = snap.target_block
            if not tb or tb.get("type") != target_block_type:
                raise AssertionError(f"targetBlock expected {target_block_type}, got {tb}")
        return snap

    def reset_input(self) -> None:
        self.release_keys()

    def session(self) -> GameSession:
        return GameSession(self)


class GameSession:
    """Context manager: wait for ready, release keys on exit."""

    def __init__(self, client: GameClient, wait_timeout: float = 120.0):
        self.client = client
        self.wait_timeout = wait_timeout

    def __enter__(self) -> GameClient:
        self.client.wait_ready(timeout=self.wait_timeout)
        return self.client

    def __exit__(self, exc_type, exc, tb) -> None:
        try:
            self.client.reset_input()
        except GameClientError:
            pass


def validate_png(path: Path, min_bytes: int = 1000) -> None:
    data = path.read_bytes()
    if len(data) < min_bytes:
        raise GameClientError(f"PNG too small ({len(data)} bytes): {path}")
    if not data.startswith(PNG_SIGNATURE):
        raise GameClientError(f"Not a valid PNG: {path}")
    if len(data) >= 24:
        width, height = struct.unpack(">II", data[16:24])
        if width < 16 or height < 16:
            raise GameClientError(f"PNG dimensions too small ({width}x{height}): {path}")


def parse_key_args(rest: list[str]) -> dict[str, str]:
    params: dict[str, str] = {}
    for arg in rest:
        if "=" in arg:
            k, v = arg.split("=", 1)
            params[k] = v
    return params


def _cli() -> int:
    parser = argparse.ArgumentParser(
        description="Autonocraft game HTTP client",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Commands:
  wait [timeout]              Poll /health until ready
  health                      One-shot readiness check
  state                       Full game state JSON
  screenshot [path]           Save PNG screenshot
  action <cmd> [k=v ...]      Raw POST /action
  dev <cmd_line>              Dev console command
  teleport <x> <y> <z>        Teleport player
  move <seconds> [w|a|s|d]    Hold keys then release
  mine [clicks]               Mine targeted block
  village_chat <message>      Steward chat (AI must be enabled)
  shutdown                    Exit game
""",
    )
    parser.add_argument("--host", default=DEFAULT_HOST)
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)
    parser.add_argument("command")
    parser.add_argument("rest", nargs=argparse.REMAINDER)
    args = parser.parse_args()

    client = GameClient(host=args.host, port=args.port)
    cmd = args.command.lower()
    rest = args.rest

    try:
        if cmd == "wait":
            timeout = float(rest[0]) if rest else 120.0
            print(json.dumps(client.wait_ready(timeout=timeout), indent=2))
            return 0
        if cmd == "health":
            print(json.dumps(client.health(), indent=2))
            return 0
        if cmd == "state":
            print(json.dumps(client.state(), indent=2))
            return 0
        if cmd == "screenshot":
            path = rest[0] if rest else "screenshot.png"
            saved = client.screenshot(path)
            print(f"Screenshot saved to {saved} ({saved.stat().st_size} bytes)")
            return 0
        if cmd == "action":
            if not rest:
                print("action requires cmd name", file=sys.stderr)
                return 1
            print(json.dumps(client.action_ok(rest[0], **parse_key_args(rest[1:])), indent=2))
            return 0
        if cmd == "dev":
            if not rest:
                print("dev requires a command string", file=sys.stderr)
                return 1
            print(json.dumps(client.dev(" ".join(rest)), indent=2))
            return 0
        if cmd == "teleport":
            if len(rest) < 3:
                print("teleport requires x y z", file=sys.stderr)
                return 1
            print(json.dumps(client.teleport(float(rest[0]), float(rest[1]), float(rest[2])), indent=2))
            return 0
        if cmd == "move":
            seconds = float(rest[0]) if rest else 1.0
            keys = [k.lower() for k in rest[1:]] if len(rest) > 1 else ["w"]
            client.move(keys=keys, seconds=seconds)
            print(json.dumps({"success": True, "message": f"moved {keys} for {seconds}s"}, indent=2))
            return 0
        if cmd == "mine":
            clicks = int(rest[0]) if rest else 20
            snap = client.mine_block(clicks=clicks)
            print(json.dumps(snap.raw, indent=2))
            return 0
        if cmd == "village_chat":
            if not rest:
                print("village_chat requires a message", file=sys.stderr)
                return 1
            print(json.dumps(client.village_chat(" ".join(rest)), indent=2))
            return 0
        if cmd == "shutdown":
            print(json.dumps(client.shutdown(), indent=2))
            return 0
        print(f"Unknown command: {cmd}", file=sys.stderr)
        return 1
    except (GameClientError, AssertionError) as e:
        print(e, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(_cli())
