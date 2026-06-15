#!/usr/bin/env python3
import argparse
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request


def request(base_url, path, method="GET", params=None, timeout=5):
    url = f"{base_url}{path}"
    if params:
        url += "?" + urllib.parse.urlencode(params)
    data = b"" if method == "POST" else None
    req = urllib.request.Request(url, method=method, data=data)
    if method == "POST":
        req.add_header("Content-Length", "0")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            body = response.read().decode("utf-8")
            return json.loads(body)
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        raise AssertionError(f"{method} {path} failed with HTTP {exc.code}: {body}") from exc


def action(base_url, cmd, **params):
    payload = {"cmd": cmd}
    payload.update(params)
    result = request(base_url, "/action", method="POST", params=payload)
    if not result.get("success"):
        raise AssertionError(f"Action {cmd} failed: {result}")
    return result


def try_action(base_url, cmd, **params):
    payload = {"cmd": cmd}
    payload.update(params)
    try:
        return request(base_url, "/action", method="POST", params=payload)
    except AssertionError:
        return {"success": False}


def wait_until(label, timeout, poll, predicate):
    deadline = time.time() + timeout
    last = None
    while time.time() < deadline:
        last = predicate()
        if last:
            return last
        time.sleep(poll)
    raise AssertionError(f"Timed out waiting for {label}. Last value: {last!r}")


def total_block(slots, block_type):
    total = 0
    for slot in slots:
        stack = slot.get("stack", {})
        if stack.get("kind") == "block" and stack.get("blockType") == block_type:
            total += int(stack.get("count", 0))
    return total


def villager_inventory_total(villager, *block_types):
    total = 0
    for block_type in block_types:
        total += total_block(villager.get("inventory", []), block_type)
    return total


def main():
    parser = argparse.ArgumentParser(description="Live end-to-end villager system test")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5001)
    parser.add_argument("--lumber-timeout", type=float, default=90.0)
    args = parser.parse_args()

    base_url = f"http://{args.host}:{args.port}"
    health = request(base_url, "/health")
    if not health.get("ready"):
        raise AssertionError(f"Game is not ready: {health}")

    state = request(base_url, "/state")
    village = state.get("village")
    if not village:
        raise AssertionError("Expected starter village in /state")
    if int(village.get("population", 0)) < 2:
        raise AssertionError(f"Expected at least two citizens, got: {village}")

    debug = request(base_url, "/village/debug")
    citizens = debug.get("villagers", [])
    if len(citizens) < 2:
        raise AssertionError(f"Expected at least two debug citizens, got: {citizens}")

    action(base_url, "teleport", x=village["anchorX"] + 0.5, y=debug["village"]["anchorY"] + 4, z=village["anchorZ"] + 0.5)
    action(base_url, "open_village")
    action(base_url, "close_village")

    debug = request(base_url, "/village/debug")
    worker = next((v for v in debug["villagers"] if v["role"] == "Lumberjack"), debug["villagers"][0])
    action(base_url, "assign_job", villager_id=worker["id"], job="Lumber")

    def lumber_started():
        snap = request(base_url, "/village/debug")
        current = next(v for v in snap["villagers"] if v["id"] == worker["id"])
        if current["job"] == "Lumber" and current["phase"] in ("PathTo", "Working"):
            return current
        return None

    started = wait_until("lumber job to start", 10.0, 0.5, lumber_started)

    before_logs = villager_inventory_total(started, "OakLog", "BirchLog", "PineLog", "WillowLog", "PalmLog")

    def lumber_produced():
        snap = request(base_url, "/village/debug")
        current = next(v for v in snap["villagers"] if v["id"] == worker["id"])
        logs = villager_inventory_total(current, "OakLog", "BirchLog", "PineLog", "WillowLog", "PalmLog")
        if logs > before_logs:
            return {"snap": snap, "worker": current, "logs": logs}
        return None

    produced = wait_until("lumberjack to break a log", args.lumber_timeout, 1.0, lumber_produced)

    storage_before_recruit = total_block(produced["snap"].get("storage", []), "OakPlank")
    if storage_before_recruit < 4:
        raise AssertionError(f"Expected at least 4 OakPlank in village storage for recruit path, got {storage_before_recruit}")

    action(base_url, "recruit_villager")

    recruited = wait_until(
        "population to increase after recruit",
        20.0,
        0.5,
        lambda: (snap if (snap := request(base_url, "/village/debug"))["village"]["population"] >= 3 else None),
    )

    action(base_url, "dev", cmd_line="creative on")
    time.sleep(0.5)

    queued = None
    anchor_x = recruited["village"]["anchorX"]
    anchor_z = recruited["village"]["anchorZ"]
    candidate_offsets = []
    for radius in (6, 8, 10, 12, 16, 20, 24, 28):
        candidate_offsets.extend((
            (radius, 0),
            (-radius, 0),
            (0, radius),
            (0, -radius),
            (radius, radius),
            (-radius, radius),
            (radius, -radius),
            (-radius, -radius),
        ))

    for dx, dz in candidate_offsets:
        candidate = try_action(
            base_url,
            "queue_build",
            blueprint_id="farm_plot",
            anchor_x=anchor_x + dx,
            anchor_z=anchor_z + dz,
        )
        if candidate.get("success"):
            queued = {"x": anchor_x + dx, "z": anchor_z + dz}
            break

    if queued is None:
        raise AssertionError("Could not queue farm_plot at any candidate location")

    after_queue = request(base_url, "/village/debug")
    site = next((s for s in after_queue["buildingSites"] if s["blueprintId"] == "farm_plot" and not s["complete"]), None)
    if site is None:
        raise AssertionError(f"Expected pending farm_plot site, got: {after_queue['buildingSites']}")

    builder = next(
        (v for v in after_queue["villagers"] if v["id"] != produced["worker"]["id"]),
        after_queue["villagers"][0],
    )
    action(base_url, "assign_job", villager_id=builder["id"], job="Build")

    def farm_plot_built():
        snap = request(base_url, "/village/debug")
        if any(b["blueprintId"] == "farm_plot" and b["complete"] for b in snap["buildings"]):
            return snap
        return None

    built = wait_until(
        "farm_plot building to complete",
        45.0,
        0.5,
        farm_plot_built,
    )

    result = {
        "village": recruited["village"],
        "worker": {
            "id": produced["worker"]["id"],
            "job": produced["worker"]["job"],
            "phase": produced["worker"]["phase"],
            "logs": produced["logs"],
        },
        "storageOakPlankBeforeRecruit": storage_before_recruit,
        "citizensAfterRecruit": len(recruited["villagers"]),
        "queuedFarmPlotAt": queued,
        "farmPlotBuilt": any(b["blueprintId"] == "farm_plot" and b["complete"] for b in built["buildings"]),
    }
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
