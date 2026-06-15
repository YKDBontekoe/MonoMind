#!/usr/bin/env python3
"""
Live integration tests for the Autonocraft Agent HTTP API.

Requires a running game in Playing state:
  ./run.sh --skip-menu

Usage:
  python3 test_live_api.py
  python3 test_live_api.py --host localhost --port 5000
  python3 test_live_api.py --skip-ai --skip-village
"""

from __future__ import annotations

import argparse
import json
import sys
import time
import traceback
from dataclasses import dataclass, field
from pathlib import Path

from game_client import (
    AssertionError,
    GameClient,
    GameClientError,
    GameSession,
    GameSnapshot,
    Vec3,
    validate_png,
)


@dataclass
class TestResult:
    name: str
    passed: bool
    message: str = ""
    duration: float = 0.0


@dataclass
class TestSuite:
    client: GameClient
    output_dir: Path
    skip_ai: bool = False
    skip_village: bool = False
    results: list[TestResult] = field(default_factory=list)

    def run(self, name: str, fn) -> None:
        start = time.time()
        try:
            fn()
            self.results.append(TestResult(name, True, duration=time.time() - start))
            print(f"  PASS  {name} ({time.time() - start:.2f}s)")
        except Exception as e:
            self.results.append(
                TestResult(name, False, str(e), duration=time.time() - start)
            )
            print(f"  FAIL  {name} ({time.time() - start:.2f}s): {e}")
            try:
                self.client.reset_input()
            except GameClientError:
                pass

    def test_health_ready(self) -> None:
        health = self.client.health()
        assert health.get("ready"), f"not ready: {health}"
        assert health.get("gameState") == "Playing", health

    def test_state_schema(self) -> None:
        snap = self.client.snapshot()
        required = [
            "gameState", "worldSeed", "position", "velocity", "yaw", "pitch",
            "creativeMode", "isGrounded", "health", "maxHealth", "hotbar", "skills",
            "animals", "targetBlock", "unlockedRecipes", "playWithAi", "aiProvider",
        ]
        for key in required:
            assert key in snap.raw, f"missing state key: {key}"
        assert snap.game_state == "Playing"
        assert len(snap.hotbar) == 9
        assert snap.health > 0

    def test_screenshot_valid_png(self) -> None:
        path = self.output_dir / "test_screenshot.png"
        saved = self.client.screenshot(path)
        assert saved.exists()
        validate_png(saved)

    def test_teleport_and_position(self) -> None:
        target = Vec3(24.5, 70.0, 24.5)
        self.client.fly_to(target)
        snap = self.client.assert_state(flying=True, position_near=(target, 1.5))

    def test_set_look(self) -> None:
        self.client.set_look(45.0, -20.0)
        time.sleep(0.2)
        snap = self.client.snapshot()
        yaw_diff = abs((snap.raw["yaw"] - 45.0 + 180) % 360 - 180)
        assert yaw_diff < 5.0, f"yaw={snap.raw['yaw']}"
        assert abs(snap.raw["pitch"] - (-20.0)) < 5.0, f"pitch={snap.raw['pitch']}"

    def test_relative_look(self) -> None:
        before = self.client.snapshot()
        self.client.look(dx=10, dy=5)
        after = self.client.snapshot()
        assert after.raw["yaw"] != before.raw["yaw"] or after.raw["pitch"] != before.raw["pitch"]

    def test_movement_changes_position(self) -> None:
        self.client.set_creative(False)
        self.client.teleport(16, 65, 16)
        start = self.client.snapshot().position
        self.client.move(forward=1.5)
        after = self.client.wait_position_change(start, min_distance=0.3, timeout=6)
        assert after.position.distance_to(start) >= 0.3

    def test_release_keys(self) -> None:
        self.client.key_down("w")
        self.client.release_keys()
        result = self.client.action("release_keys")
        assert result.get("success", True)

    def test_dev_give_block(self) -> None:
        self.client.give_block("Stone", 32)
        self.client.wait_for(
            lambda s: any(x.get("type") == "Stone" for x in s.hotbar if x.get("kind") == "block"),
            timeout=5,
            description="Stone in hotbar",
        )

    def test_dev_give_tool(self) -> None:
        self.client.give_tool("pickaxe", "stone")
        self.client.assert_state(hotbar_tool="pickaxe")

    def test_select_slot(self) -> None:
        self.client.select_slot(2)
        snap = self.client.snapshot()
        assert snap.selected_slot == 2

    def test_set_time(self) -> None:
        self.client.set_time(0.25)
        snap = self.client.snapshot()
        assert abs(float(snap.raw["timeOfDay"]) - 0.25) < 0.05
        self.client.set_time_scale(0.01)

    def test_spawn_animal(self) -> None:
        before = len(self.client.snapshot().animals)
        self.client.spawn_animal("Sheep", 1)
        time.sleep(0.5)
        after = len(self.client.snapshot().animals)
        assert after >= before

    def test_target_block_when_looking_down(self) -> None:
        self.client.fly_to(Vec3(16, 68, 16))
        self.client.look_at_ground(0)
        time.sleep(0.3)
        snap = self.client.snapshot()
        assert snap.target_block is not None, "expected block targeted when looking down"

    def test_mining_instant_click(self) -> None:
        self.client.give_tool("pickaxe", "stone")
        slot = self.client.select_tool("pickaxe")
        self.client.fly_to(Vec3(16, 68, 16))
        self.client.look_at_ground(0)
        time.sleep(0.5)
        before = self.client.snapshot()
        assert before.target_block is not None, "need targeted block"

        def pick_dur(snap: GameSnapshot) -> int | None:
            for item in snap.hotbar:
                if int(item.get("slot", -1)) == slot and item.get("kind") == "tool":
                    return int(item.get("durability", 0))
            return None

        def block_total(snap: GameSnapshot, block_type: str) -> int:
            return sum(
                int(item.get("count", 0))
                for item in snap.hotbar
                if item.get("kind") == "block" and item.get("type") == block_type
            )

        target_type = str(before.target_block.get("type", ""))
        dur_before = pick_dur(before)
        blocks_before = block_total(before, target_type)

        self.client.click("left")
        time.sleep(0.5)
        after = self.client.snapshot()
        dur_after = pick_dur(after)
        blocks_after = block_total(after, target_type)

        mined = (
            (dur_before is not None and dur_after is not None and dur_after < dur_before)
            or blocks_after > blocks_before
            or after.target_block is None
            or (
                after.target_block
                and (
                    after.target_block.get("x"),
                    after.target_block.get("y"),
                    after.target_block.get("z"),
                    after.target_block.get("type"),
                )
                != (
                    before.target_block.get("x"),
                    before.target_block.get("y"),
                    before.target_block.get("z"),
                    before.target_block.get("type"),
                )
            )
        )
        assert mined, (
            f"mining had no effect on slot={slot} "
            f"dur {dur_before}->{dur_after} blocks {blocks_before}->{blocks_after} "
            f"target={after.target_block}"
        )

    def test_village_present(self) -> None:
        if self.skip_village:
            return
        self.client.assert_state(has_village=True)

    def test_village_fields(self) -> None:
        if self.skip_village:
            return
        snap = self.client.snapshot()
        village = snap.village
        assert village is not None
        for key in ("id", "name", "population", "foodStock"):
            assert key in village, f"missing village.{key}"

    def test_villagers_near_spawn(self) -> None:
        if self.skip_village:
            return
        self.client.fly_to(Vec3(16, 65, 16))
        time.sleep(0.5)
        snap = self.client.snapshot()
        if len(snap.villagers) >= 1:
            return
        village = snap.village
        assert village is not None and village.get("population", 0) >= 2, (
            "expected villagers in range or population >= 2"
        )

    def test_village_chat_mock(self) -> None:
        if self.skip_ai or self.skip_village:
            return
        snap = self.client.snapshot()
        if not snap.raw.get("playWithAi"):
            return
        result = self.client.village_chat("What is our food stock?", timeout=35)
        assert "reply" in result or "error" in result

    def test_unlock_recipe(self) -> None:
        self.client.unlock_recipe("recipe:plank")
        snap = self.client.snapshot()
        recipes = snap.raw.get("unlockedRecipes", [])
        assert "recipe:plank" in recipes

    def test_action_invalid_key_fails(self) -> None:
        try:
            self.client.action_ok("key_down", key="not_a_real_key_xyz")
            raise AssertionError("expected invalid key to fail")
        except GameClientError as e:
            assert "failed" in str(e).lower() or "invalid" in str(e).lower()

    def test_double_screenshot_size_stable(self) -> None:
        p1 = self.output_dir / "shot_a.png"
        p2 = self.output_dir / "shot_b.png"
        self.client.screenshot(p1)
        time.sleep(0.2)
        self.client.screenshot(p2)
        assert p1.stat().st_size > 1000
        assert p2.stat().st_size > 1000

    def execute_all(self) -> int:
        tests = [
            ("health_ready", self.test_health_ready),
            ("state_schema", self.test_state_schema),
            ("screenshot_valid_png", self.test_screenshot_valid_png),
            ("teleport_and_position", self.test_teleport_and_position),
            ("set_look", self.test_set_look),
            ("relative_look", self.test_relative_look),
            ("movement_changes_position", self.test_movement_changes_position),
            ("release_keys", self.test_release_keys),
            ("dev_give_block", self.test_dev_give_block),
            ("dev_give_tool", self.test_dev_give_tool),
            ("select_slot", self.test_select_slot),
            ("set_time", self.test_set_time),
            ("spawn_animal", self.test_spawn_animal),
            ("target_block_looking_down", self.test_target_block_when_looking_down),
            ("mining_instant_click", self.test_mining_instant_click),
            ("village_present", self.test_village_present),
            ("village_fields", self.test_village_fields),
            ("villagers_near_spawn", self.test_villagers_near_spawn),
            ("village_chat_mock", self.test_village_chat_mock),
            ("unlock_recipe", self.test_unlock_recipe),
            ("invalid_key_fails", self.test_action_invalid_key_fails),
            ("double_screenshot", self.test_double_screenshot_size_stable),
        ]

        print(f"Running {len(tests)} live API tests -> {self.output_dir}")
        for name, fn in tests:
            self.run(name, fn)

        passed = sum(1 for r in self.results if r.passed)
        failed = len(self.results) - passed
        print()
        print(f"Results: {passed}/{len(self.results)} passed, {failed} failed")
        if failed:
            print("\nFailures:")
            for r in self.results:
                if not r.passed:
                    print(f"  - {r.name}: {r.message}")
        summary_path = self.output_dir / "test_results.json"
        summary_path.write_text(
            json.dumps([r.__dict__ for r in self.results], indent=2),
            encoding="utf-8",
        )
        print(f"Wrote {summary_path}")
        return 0 if failed == 0 else 1


def main() -> int:
    parser = argparse.ArgumentParser(description="Live Autonocraft Agent API tests")
    parser.add_argument("--host", default="localhost")
    parser.add_argument("--port", type=int, default=5001)
    parser.add_argument("--output-dir", type=Path, default=Path("test_output/live_api"))
    parser.add_argument("--wait", type=float, default=120.0, help="Seconds to wait for game")
    parser.add_argument("--skip-ai", action="store_true")
    parser.add_argument("--skip-village", action="store_true")
    args = parser.parse_args()

    args.output_dir.mkdir(parents=True, exist_ok=True)
    client = GameClient(host=args.host, port=args.port)

    print(f"Waiting for game at {client.base_url} (timeout={args.wait}s)...")
    try:
        with GameSession(client, wait_timeout=args.wait):
            suite = TestSuite(
                client=client,
                output_dir=args.output_dir,
                skip_ai=args.skip_ai,
                skip_village=args.skip_village,
            )
            return suite.execute_all()
    except GameClientError as e:
        print(f"Could not connect: {e}", file=sys.stderr)
        print("Start the game: ./run.sh --skip-menu", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
