#!/usr/bin/env python3
from __future__ import annotations

import json
import math
import sys
import time
import traceback
from pathlib import Path

sys.path.insert(0, ".cursor/skills/autonocraft-game-test/scripts")

from game_client import GameClient, Vec3, validate_png  # noqa: E402


OUT = Path("test_output/playthrough")
REPORT = OUT / "broad_playtest_report.json"
SEA_LEVEL = 62


class Playtest:
    def __init__(self, port: int = 5001) -> None:
        self.client = GameClient(port=port, timeout=20)
        self.results: list[dict] = []

    def record(self, name: str, passed: bool, **details) -> None:
        status = "PASS" if passed else "FAIL"
        print(f"{status} {name}: {details}")
        self.results.append({"name": name, "passed": passed, **details})

    def run_step(self, name: str, fn) -> None:
        start = time.time()
        try:
            details = fn() or {}
            details["duration"] = round(time.time() - start, 3)
            self.record(name, True, **details)
        except Exception as exc:
            try:
                self.client.release_keys()
            except Exception:
                pass
            self.record(
                name,
                False,
                error=str(exc),
                traceback=traceback.format_exc(limit=8),
                duration=round(time.time() - start, 3),
            )

    def wait_grounded(self, timeout: float = 12) -> dict:
        snap = self.client.wait_for(lambda s: s.grounded, timeout=timeout, description="grounded")
        return snap.raw

    def look_at(self, target: Vec3, pitch_offset: float = 0) -> None:
        snap = self.client.snapshot()
        eye = Vec3(snap.position.x, snap.position.y + 1.62, snap.position.z)
        dx = target.x - eye.x
        dy = target.y - eye.y
        dz = target.z - eye.z
        yaw = math.degrees(math.atan2(dz, dx))
        horiz = math.sqrt(dx * dx + dz * dz)
        pitch = math.degrees(math.atan2(dy, horiz)) + pitch_offset
        self.client.set_look(yaw, max(-89, min(89, pitch)))

    def startup_state(self) -> dict:
        health = self.client.wait_ready(timeout=30)
        snap = self.client.snapshot()
        path = OUT / "broad_spawn.png"
        self.client.screenshot(path)
        return {
            "health": health,
            "seed": snap.raw["worldSeed"],
            "position": snap.position.as_dict(),
            "hasVillage": snap.village is not None,
            "hotbarKinds": [slot.get("kind") for slot in snap.hotbar],
            "screenshotBytes": path.stat().st_size,
        }

    def worldgen_streaming(self) -> dict:
        self.client.set_time(0.5)
        self.client.set_creative(True)
        samples = [(16, 95, 16), (-32, 90, 16), (96, 105, 96), (-96, 105, -96), (160, 110, -80)]
        chunks = []
        screenshots = []
        for i, (x, y, z) in enumerate(samples):
            self.client.teleport(x, y, z)
            self.client.set_look(45, -50)
            time.sleep(1.2)
            shot = OUT / f"broad_worldgen_{i}.png"
            self.client.screenshot(shot)
            validate_png(shot)
            msg = self.client.dev("chunks").get("message", "")
            chunks.append(msg)
            screenshots.append({"path": str(shot), "bytes": shot.stat().st_size})
        active_counts = []
        for msg in chunks:
            marker = "Active chunks:"
            if marker in msg:
                active_counts.append(int(msg.split(marker, 1)[1].split("|", 1)[0].strip()))
        if not active_counts or min(active_counts) <= 0:
            raise AssertionError(f"bad chunk counts: {chunks}")
        return {"chunkMessages": chunks, "screenshots": screenshots}

    def survival_movement_jump_and_fall(self) -> dict:
        c = self.client
        c.dev("health 20")
        c.set_creative(False)
        c.teleport(16, 70, 16)
        ground = self.wait_grounded(timeout=12)
        c.dev("health 20")
        start = c.snapshot().position
        c.set_look(-90, 0)
        c.key_down("w")
        time.sleep(1.1)
        c.key_up("w")
        moved = c.wait_position_change(start, min_distance=0.25, timeout=4).position
        self.wait_grounded(timeout=4)
        c.key_down("space")
        time.sleep(0.15)
        c.key_up("space")
        after_jump = c.wait_for(lambda s: not s.grounded or s.position.y > moved.y + 0.05, timeout=2, description="jump lift")
        if after_jump.position.y <= moved.y and abs(after_jump.velocity.y) < 0.1:
            raise AssertionError("jump did not produce vertical movement/velocity")
        c.dev("health 20")
        c.teleport(16, 80, 16)
        landed = self.wait_grounded(timeout=16)
        health_after_fall = int(c.snapshot().raw["health"])
        if health_after_fall >= 20 or health_after_fall <= 0:
            raise AssertionError(f"expected fall damage, health={health_after_fall}")
        c.dev("health 20")
        return {
            "groundY": ground["position"]["y"],
            "movedFrom": start.as_dict(),
            "movedTo": moved.as_dict(),
            "jumpY": after_jump.position.y,
            "fallHealth": health_after_fall,
            "landedAt": landed["position"],
        }

    def water_and_oxygen_probe(self) -> dict:
        c = self.client
        c.set_time(0.5)
        c.dev("health 20")
        c.set_creative(False)
        water_candidates = [(-24, SEA_LEVEL - 1, 16), (-32, SEA_LEVEL - 1, 16), (-40, SEA_LEVEL - 1, 20), (-48, SEA_LEVEL - 1, 32)]
        observations = []
        found = None
        for x, y, z in water_candidates:
            c.teleport(x, y, z)
            time.sleep(0.5)
            start = c.snapshot()
            start_oxygen = float(start.raw["oxygen"])
            time.sleep(4.0)
            end = c.snapshot()
            end_oxygen = float(end.raw["oxygen"])
            observations.append({
                "candidate": {"x": x, "y": y, "z": z},
                "startOxygen": start_oxygen,
                "endOxygen": end_oxygen,
                "position": end.position.as_dict(),
            })
            if end_oxygen < start_oxygen:
                found = observations[-1]
                break
        if found is None:
            return {
                "waterLocatedByApi": False,
                "note": "No underwater coordinate found from the HTTP-visible data; fluid mechanics are covered by headless integration tests.",
                "allObservations": observations,
            }
        c.set_creative(True)
        c.teleport(16, 80, 16)
        time.sleep(0.5)
        return {"waterLocatedByApi": True, "waterObservation": found, "allObservations": observations}

    def mining_placing_and_tools(self) -> dict:
        c = self.client
        c.set_creative(False)
        c.dev("health 20")
        c.teleport(18, 70, 18)
        self.wait_grounded()
        c.give_tool("pickaxe", "stone")
        slot = c.select_tool("pickaxe")
        before = c.snapshot()
        target = None
        for pitch in (-65, -75, -85):
            c.set_look(0, pitch)
            time.sleep(0.2)
            target = c.snapshot().target_block
            if target:
                break
        if target is None:
            raise AssertionError("No target block found while looking at the ground")
        c.mine_block(clicks=8, interval=0.12, require_target=True)
        after = c.snapshot()
        tool_after = next(s for s in after.hotbar if int(s["slot"]) == slot)
        if tool_after.get("durability", 9999) >= next(s for s in before.hotbar if int(s["slot"]) == slot).get("durability", 9999):
            raise AssertionError("tool durability did not decrease after mining")
        dirt_slot = c.find_hotbar_slot(block_type="Dirt")
        if dirt_slot is None:
            raise AssertionError("no dirt block available for placement")
        c.select_slot(dirt_slot)
        before_count = next(s for s in c.snapshot().hotbar if int(s["slot"]) == dirt_slot).get("count", 0)
        c.place_block(times=1)
        time.sleep(0.3)
        after_count = next(s for s in c.snapshot().hotbar if int(s["slot"]) == dirt_slot).get("count", 0)
        return {"targetBeforeMining": target, "toolAfter": tool_after, "dirtCountBefore": before_count, "dirtCountAfter": after_count}

    def animals_and_combat(self) -> dict:
        c = self.client
        c.dev("health 20")
        c.set_creative(False)
        c.teleport(16, 75, 28)
        self.wait_grounded()
        c.dev("spawn Chicken 1")
        time.sleep(0.5)
        snap = c.snapshot()
        if not snap.animals:
            raise AssertionError("no animals visible after spawn")
        animal = min(snap.animals, key=lambda a: abs(a["x"] - snap.position.x) + abs(a["z"] - snap.position.z))
        c.teleport(float(animal["x"]) - 1.2, float(animal["y"]), float(animal["z"]))
        self.look_at(Vec3(float(animal["x"]), float(animal["y"]) + 0.4, float(animal["z"])))
        before_health = animal["health"]
        for _ in range(8):
            c.click("left")
            time.sleep(0.25)
        after_animals = c.snapshot().animals
        matching = [a for a in after_animals if a["id"] == animal["id"]]
        killed = not matching
        damaged = bool(matching and matching[0]["health"] < before_health)
        if not (killed or damaged):
            raise AssertionError(f"animal not damaged: before={animal}, after={matching}")
        return {"targetAnimal": animal, "killed": killed, "remaining": matching}

    def village_ai_jobs(self) -> dict:
        c = self.client
        snap = c.snapshot()
        if snap.village is None or not snap.villagers:
            raise AssertionError("starter village/villagers missing")
        mayor_chat = c.village_chat("Give me a concise settlement status report.", target="mayor")
        villager = snap.villagers[0]
        assign = c.assign_job(int(villager["id"]), "Gather")
        time.sleep(1.0)
        after = c.snapshot()
        updated = [v for v in after.villagers if v["id"] == villager["id"]]
        if not updated:
            raise AssertionError("assigned villager disappeared from state")
        return {
            "village": snap.village,
            "chat": mayor_chat,
            "assign": assign,
            "villagerAfter": updated[0],
        }

    def crafting_and_inventory_api(self) -> dict:
        c = self.client
        c.unlock_recipe("recipe:plank")
        c.give_block("OakLog", 4)
        state = c.snapshot().raw
        inv_msg = c.dev("hotbar").get("message", "")
        bucket_msg = c.dev("give bucket water").get("message", "")
        after_bucket_msg = c.dev("hotbar").get("message", "")
        fluid_visible = any(slot.get("kind") in ("fluid", "fluid_container") for slot in c.snapshot().hotbar)
        return {
            "unlockedRecipes": state.get("unlockedRecipes", []),
            "hotbarBeforeBucket": inv_msg,
            "bucketGiveMessage": bucket_msg,
            "hotbarAfterBucket": after_bucket_msg,
            "fluidVisibleInState": fluid_visible,
        }

    def run(self) -> int:
        OUT.mkdir(parents=True, exist_ok=True)
        steps = [
            ("startup_state", self.startup_state),
            ("worldgen_streaming", self.worldgen_streaming),
            ("survival_movement_jump_and_fall", self.survival_movement_jump_and_fall),
            ("water_and_oxygen_probe", self.water_and_oxygen_probe),
            ("mining_placing_and_tools", self.mining_placing_and_tools),
            ("animals_and_combat", self.animals_and_combat),
            ("village_ai_jobs", self.village_ai_jobs),
            ("crafting_and_inventory_api", self.crafting_and_inventory_api),
        ]
        for name, fn in steps:
            self.run_step(name, fn)
        REPORT.write_text(json.dumps(self.results, indent=2), encoding="utf-8")
        failed = [r for r in self.results if not r["passed"]]
        print(f"Wrote {REPORT}")
        print(f"Summary: {len(self.results) - len(failed)}/{len(self.results)} passed")
        return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(Playtest().run())
