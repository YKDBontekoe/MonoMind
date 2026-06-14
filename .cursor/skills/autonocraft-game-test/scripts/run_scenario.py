#!/usr/bin/env python3
"""Run declarative JSON test scenarios against a live Autonocraft game."""

from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path
from typing import Any

from game_client import AssertionError, GameClient, GameClientError, Vec3, validate_png


STEP_HANDLERS: dict[str, Any] = {}


def step(name: str):
    def decorator(fn):
        STEP_HANDLERS[name] = fn
        return fn
    return decorator


def _expect_dict(value: Any, step_name: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError(f"{step_name} value must be an object/dict")
    return value


def _vec3_from_dict(data: dict[str, Any]) -> Vec3:
    return Vec3(float(data["x"]), float(data["y"]), float(data["z"]))


@step("wait_ready")
def _wait_ready(client: GameClient, value: Any, index: int) -> None:
    timeout = float(value) if value is not True else 120.0
    client.wait_ready(timeout=timeout)
    print(f"[{index}] ready")


@step("sleep")
def _sleep(_client: GameClient, value: Any, index: int) -> None:
    seconds = float(value)
    time.sleep(seconds)
    print(f"[{index}] sleep {seconds}s")


@step("screenshot")
def _screenshot(client: GameClient, value: Any, index: int) -> None:
    path = Path(str(value))
    path.parent.mkdir(parents=True, exist_ok=True)
    saved = client.screenshot(path)
    print(f"[{index}] screenshot -> {saved} ({saved.stat().st_size} bytes)")


@step("state")
def _state(client: GameClient, value: Any, index: int) -> None:
    data = client.state()
    if value:
        out = Path(str(value))
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(data, indent=2), encoding="utf-8")
        print(f"[{index}] state -> {out}")
    else:
        print(json.dumps(data, indent=2))


@step("dev")
def _dev(client: GameClient, value: Any, index: int) -> None:
    result = client.dev(str(value))
    print(f"[{index}] dev: {result.get('message', result)}")


@step("teleport")
def _teleport(client: GameClient, value: Any, index: int) -> None:
    data = _expect_dict(value, "teleport")
    client.teleport(float(data["x"]), float(data["y"]), float(data["z"]))
    print(f"[{index}] teleport {data}")


@step("look")
def _look(client: GameClient, value: Any, index: int) -> None:
    data = _expect_dict(value, "look")
    if "yaw" in data:
        client.set_look(float(data["yaw"]), float(data.get("pitch", 0)))
    else:
        client.look(float(data.get("dx", 0)), float(data.get("dy", 0)))
    print(f"[{index}] look {data}")


@step("set_creative")
@step("set_flying")
def _set_creative(client: GameClient, value: Any, index: int) -> None:
    creative = value if isinstance(value, bool) else str(value).lower() == "true"
    client.set_creative(creative)
    print(f"[{index}] set_creative {creative}")


@step("select_slot")
def _select_slot(client: GameClient, value: Any, index: int) -> None:
    client.select_slot(int(value))
    print(f"[{index}] select_slot {value}")


@step("click")
def _click(client: GameClient, value: Any, index: int) -> None:
    if isinstance(value, dict):
        client.click(
            str(value.get("button", "left")),
            times=int(value.get("times", 1)),
            interval=float(value.get("interval", 0.1)),
        )
    else:
        client.click(str(value))
    print(f"[{index}] click {value}")


@step("release_keys")
def _release_keys(client: GameClient, _value: Any, index: int) -> None:
    client.release_keys()
    print(f"[{index}] release_keys")


@step("move")
def _move(client: GameClient, value: Any, index: int) -> None:
    data = _expect_dict(value, "move")
    client.move(
        forward=float(data.get("forward", 0)),
        back=float(data.get("back", 0)),
        left=float(data.get("left", 0)),
        right=float(data.get("right", 0)),
        jump=bool(data.get("jump", False)),
        sprint=bool(data.get("sprint", False)),
        keys=data.get("keys"),
        seconds=float(data["seconds"]) if "seconds" in data else None,
    )
    print(f"[{index}] move {data}")


@step("mine")
def _mine(client: GameClient, value: Any, index: int) -> None:
    clicks = 20
    interval = 0.15
    require_target = True
    if isinstance(value, dict):
        clicks = int(value.get("clicks", clicks))
        interval = float(value.get("interval", interval))
        require_target = bool(value.get("require_target", True))
    elif value is not None:
        clicks = int(value)
    snap = client.mine_block(clicks=clicks, interval=interval, require_target=require_target)
    tb = snap.target_block
    print(f"[{index}] mine clicks={clicks} target={tb}")


@step("place")
def _place(client: GameClient, value: Any, index: int) -> None:
    times = int(value) if value is not None else 1
    client.place_block(times=times)
    print(f"[{index}] place x{times}")


@step("select_tool")
def _select_tool(client: GameClient, value: Any, index: int) -> None:
    tool = str(value) if value is not None else "pickaxe"
    slot = client.select_tool(tool)
    print(f"[{index}] select_tool {tool} -> slot {slot}")


@step("give_block")
def _give_block(client: GameClient, value: Any, index: int) -> None:
    if isinstance(value, dict):
        client.give_block(str(value["type"]), int(value.get("count", 64)))
    else:
        client.give_block(str(value))
    print(f"[{index}] give_block {value}")


@step("give_tool")
def _give_tool(client: GameClient, value: Any, index: int) -> None:
    if isinstance(value, dict):
        client.give_tool(str(value.get("tool", "pickaxe")), str(value.get("tier", "stone")))
    else:
        client.give_tool(str(value))
    print(f"[{index}] give_tool {value}")


@step("spawn_animal")
def _spawn_animal(client: GameClient, value: Any, index: int) -> None:
    if isinstance(value, dict):
        client.spawn_animal(str(value["type"]), int(value.get("count", 1)))
    else:
        client.spawn_animal(str(value))
    print(f"[{index}] spawn_animal {value}")


@step("set_time")
def _set_time(client: GameClient, value: Any, index: int) -> None:
    client.set_time(float(value))
    print(f"[{index}] set_time {value}")


@step("set_time_scale")
def _set_time_scale(client: GameClient, value: Any, index: int) -> None:
    client.set_time_scale(float(value))
    print(f"[{index}] set_time_scale {value}")


@step("open_crucible")
def _open_crucible(client: GameClient, _value: Any, index: int) -> None:
    result = client.open_crucible()
    print(f"[{index}] open_crucible: {result.get('message')}")


@step("recruit_villager")
def _recruit_villager(client: GameClient, _value: Any, index: int) -> None:
    result = client.recruit_villager()
    print(f"[{index}] recruit_villager: {result.get('message')}")


@step("assign_job")
def _assign_job(client: GameClient, value: Any, index: int) -> None:
    data = _expect_dict(value, "assign_job")
    target = None
    if "target" in data:
        target = _vec3_from_dict(data["target"])
    client.assign_job(int(data["villager_id"]), str(data["job"]), target=target)
    print(f"[{index}] assign_job {data}")


@step("village_chat")
def _village_chat(client: GameClient, value: Any, index: int) -> None:
    target = "mayor"
    message = value
    if isinstance(value, dict):
        message = value["message"]
        target = value.get("target", "mayor")
    result = client.village_chat(str(message), target=str(target))
    print(f"[{index}] village_chat: {json.dumps(result)[:200]}")


@step("action")
def _action(client: GameClient, value: Any, index: int) -> None:
    data = dict(_expect_dict(value, "action"))
    cmd = data.pop("cmd")
    result = client.action_ok(cmd, **data)
    print(f"[{index}] action {cmd}: {result}")


@step("assert")
def _assert(client: GameClient, value: Any, index: int) -> None:
    data = _expect_dict(value, "assert")
    kwargs: dict[str, Any] = {}
    if "health_min" in data:
        kwargs["health_min"] = int(data["health_min"])
    if "health_max" in data:
        kwargs["health_max"] = int(data["health_max"])
    if "flying" in data:
        kwargs["flying"] = bool(data["flying"])
    if "grounded" in data:
        kwargs["grounded"] = bool(data["grounded"])
    if "has_village" in data:
        kwargs["has_village"] = bool(data["has_village"])
    if "hotbar_block" in data:
        kwargs["hotbar_block"] = str(data["hotbar_block"])
    if "hotbar_tool" in data:
        kwargs["hotbar_tool"] = str(data["hotbar_tool"])
    if "nearby_station" in data:
        kwargs["nearby_station"] = str(data["nearby_station"])
    if "target_block_type" in data:
        kwargs["target_block_type"] = str(data["target_block_type"])
    if "position_near" in data:
        pn = data["position_near"]
        pos = _vec3_from_dict(pn["at"])
        tol = float(pn.get("tolerance", 2.0))
        kwargs["position_near"] = (pos, tol)
    snap = client.assert_state(**kwargs)
    print(f"[{index}] assert OK pos={snap.position.as_dict()}")


@step("wait_for")
def _wait_for(client: GameClient, value: Any, index: int) -> None:
    data = _expect_dict(value, "wait_for")
    timeout = float(data.get("timeout", 10))
    poll = float(data.get("poll_interval", 0.2))

    if "position_change" in data:
        from_pos = client.snapshot().position
        min_dist = float(data["position_change"])
        client.wait_position_change(from_pos, min_distance=min_dist, timeout=timeout)
        print(f"[{index}] wait_for position_change >= {min_dist}")
        return

    if "mining_progress" in data:
        min_prog = float(data["mining_progress"])
        client.wait_mining_progress(min_progress=min_prog, timeout=timeout)
        print(f"[{index}] wait_for mining_progress >= {min_prog}")
        return

    if "hotbar_block" in data:
        block = str(data["hotbar_block"])

        def _has_block(s):
            return any(x.get("kind") == "block" and x.get("type") == block for x in s.hotbar)

        client.wait_for(_has_block, timeout=timeout, poll_interval=poll, description=f"hotbar_block={block}")
        print(f"[{index}] wait_for hotbar_block={block}")
        return

    if "grounded" in data:
        want = bool(data["grounded"])

        def _grounded(s):
            return s.grounded == want

        client.wait_for(_grounded, timeout=timeout, poll_interval=poll, description=f"grounded={want}")
        print(f"[{index}] wait_for grounded={want}")
        return

    raise ValueError("wait_for needs position_change, mining_progress, hotbar_block, or grounded")


@step("repeat")
def _repeat(client: GameClient, value: Any, index: int) -> None:
    data = _expect_dict(value, "repeat")
    count = int(data.get("count", 1))
    steps = data.get("steps", [])
    for n in range(count):
        for j, step in enumerate(steps, 1):
            _run_step(client, step, f"{index}.{n+1}.{j}")


@step("shutdown")
def _shutdown(client: GameClient, _value: Any, index: int) -> None:
    client.shutdown()
    print(f"[{index}] shutdown")


def _run_step(client: GameClient, step: dict, step_index: int | str) -> None:
    if len(step) != 1:
        raise ValueError(f"Step {step_index}: each step must have exactly one key, got {list(step)}")
    key, value = next(iter(step.items()))
    handler = STEP_HANDLERS.get(key)
    if handler is None:
        raise ValueError(f"Unknown step key: {key}. Known: {sorted(STEP_HANDLERS)}")
    handler(client, value, step_index)


def _load_scenario(path: Path) -> dict:
    with path.open(encoding="utf-8") as f:
        return json.load(f)


def run_scenario(client: GameClient, scenario: dict, scenario_path: Path | None = None) -> None:
    name = scenario.get("name", scenario_path.stem if scenario_path else "scenario")
    steps = scenario.get("steps", [])
    if not steps:
        raise ValueError("Scenario has no steps")
    print(f"Scenario: {name} ({len(steps)} steps)")
    for i, step in enumerate(steps, 1):
        _run_step(client, step, i)


def main() -> int:
    parser = argparse.ArgumentParser(description="Run Autonocraft JSON test scenarios")
    parser.add_argument("scenario", type=Path, nargs="?", help="Path to scenario JSON file")
    parser.add_argument("--host", default="localhost")
    parser.add_argument("--port", type=int, default=5001)
    parser.add_argument("--dry-run", action="store_true", help="Print steps without executing")
    parser.add_argument("--all", action="store_true", help="Run all examples/*.json")
    parser.add_argument("--output-dir", type=Path, default=Path("test_output"), help="Artifact directory")
    args = parser.parse_args()

    if args.all:
        examples_dir = Path(__file__).resolve().parent.parent / "examples"
        scenarios = sorted(examples_dir.glob("*.json"))
        if not scenarios:
            print("No scenarios found in examples/", file=sys.stderr)
            return 1
        client = GameClient(host=args.host, port=args.port)
        args.output_dir.mkdir(parents=True, exist_ok=True)
        client.wait_ready()
        failed = 0
        for path in scenarios:
            try:
                run_scenario(client, _load_scenario(path), path)
                print(f"PASSED {path.name}\n")
            except (GameClientError, ValueError, AssertionError) as e:
                print(f"FAILED {path.name}: {e}\n", file=sys.stderr)
                failed += 1
                try:
                    client.release_keys()
                except GameClientError:
                    pass
        print(f"Summary: {len(scenarios) - failed}/{len(scenarios)} passed")
        return 1 if failed else 0

    if not args.scenario:
        parser.error("scenario path required unless --all is set")

    scenario = _load_scenario(args.scenario)
    name = scenario.get("name", args.scenario.stem)
    steps = scenario.get("steps", [])
    if not steps:
        print("Scenario has no steps", file=sys.stderr)
        return 1

    print(f"Scenario: {name} ({len(steps)} steps)")
    if args.dry_run:
        for i, step in enumerate(steps, 1):
            print(f"  [{i}] {step}")
        return 0

    args.output_dir.mkdir(parents=True, exist_ok=True)
    client = GameClient(host=args.host, port=args.port)
    try:
        run_scenario(client, scenario, args.scenario)
    except (GameClientError, ValueError, AssertionError) as e:
        print(f"FAILED: {e}", file=sys.stderr)
        try:
            client.release_keys()
        except GameClientError:
            pass
        return 1

    print("PASSED")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
