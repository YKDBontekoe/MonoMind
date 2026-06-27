#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any

DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 5001
DEFAULT_TIMEOUT = 5
DEFAULT_WAIT_TIMEOUT = 120.0


def build_base_url(host: str, port: int) -> str:
    return f"http://{host}:{port}"


def query_api(
    base_url: str,
    endpoint: str,
    method: str = "GET",
    params: dict[str, Any] | None = None,
    timeout: float = DEFAULT_TIMEOUT,
    body: bytes | None = None,
    headers: dict[str, str] | None = None,
):
    if not endpoint.startswith("/"):
        endpoint = f"/{endpoint}"

    url = f"{base_url}{endpoint}"
    if params:
        query_string = urllib.parse.urlencode(params)
        url = f"{url}{'&' if '?' in url else '?'}{query_string}"

    request_body = body if body is not None else (b"" if method.upper() == "POST" else None)
    req = urllib.request.Request(url, method=method.upper(), data=request_body)
    if method.upper() == "POST" and body is None:
        req.add_header("Content-Length", "0")
    if headers:
        for key, value in headers.items():
            req.add_header(key, value)

    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            content_type = response.headers.get_content_type()
            raw = response.read()
            if content_type == "image/png":
                return raw, True, response.status
            return raw.decode("utf-8"), False, response.status
    except urllib.error.HTTPError as e:
        body_text = e.read().decode("utf-8", errors="replace")
        try:
            parsed = json.loads(body_text)
            detail = parsed.get("error") or parsed.get("message") or body_text
        except json.JSONDecodeError:
            detail = body_text or str(e)
        print(f"HTTP {e.code}: {detail}")
        sys.exit(1)
    except Exception as e:
        print(f"Error connecting to Autonocraft game server at {base_url}: {e}")
        print("Make sure the game is in Playing state (past world load).")
        sys.exit(1)


def wait_for_ready(base_url: str, timeout_seconds: float = DEFAULT_WAIT_TIMEOUT, poll_interval: float = 0.5):
    deadline = time.time() + timeout_seconds
    attempt = 0
    while time.time() < deadline:
        attempt += 1
        try:
            req = urllib.request.Request(f"{base_url}/health", method="GET")
            with urllib.request.urlopen(req, timeout=2) as response:
                body = response.read().decode("utf-8")
                data = json.loads(body)
                if data.get("ready"):
                    print(json.dumps(data, indent=2))
                    return
                if attempt == 1 or attempt % 10 == 0:
                    print(f"Waiting for game... state={data.get('gameState', 'unknown')}", file=sys.stderr)
        except urllib.error.HTTPError as e:
            if e.code == 503:
                try:
                    data = json.loads(e.read().decode("utf-8"))
                    if attempt == 1 or attempt % 10 == 0:
                        print(f"Waiting for game... state={data.get('gameState', 'unknown')}", file=sys.stderr)
                except json.JSONDecodeError:
                    pass
            elif attempt == 1 or attempt % 10 == 0:
                print(f"Waiting for server... HTTP {e.code}", file=sys.stderr)
        except Exception:
            if attempt == 1 or attempt % 10 == 0:
                print("Waiting for server...", file=sys.stderr)
        time.sleep(poll_interval)

    print(f"Timed out after {timeout_seconds}s waiting for game readiness.", file=sys.stderr)
    sys.exit(1)


def _usage_error(message: str) -> None:
    print(f"Error: {message}", file=sys.stderr)
    usage()


def usage():
    print("Autonocraft Interaction Tool CLI")
    print("Usage:")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] wait [timeout_seconds]")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] health")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] state")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] metrics")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] village_debug")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] structures")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] slabscan [radius]")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] screenshot [file_path]")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] chat [target=<id>] <message...>")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] chat_confirm [target=<id>] confirm=<true|false>")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] action <cmd> [key=val ...]")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] request <method> <endpoint> [key=val ...]")
    print("\nExamples:")
    print("  python3 tests/interact.py wait")
    print("  python3 tests/interact.py state")
    print("  python3 tests/interact.py action key_down key=w")
    print("  python3 tests/interact.py action release_keys")
    print("  python3 tests/interact.py action dev cmd_line=\"give Stone 64\"")
    print("  python3 tests/interact.py chat target=mayor hello there")
    print("  python3 tests/interact.py request GET /village/debug")
    sys.exit(1)


def _split_tokens(tokens: list[str]) -> tuple[dict[str, str], list[str]]:
    params: dict[str, str] = {}
    positional: list[str] = []
    for token in tokens:
        if "=" in token:
            key, value = token.split("=", 1)
            if not key:
                _usage_error(f"Invalid parameter token: {token}")
            params[key] = value
        else:
            positional.append(token)
    return params, positional


def _print_json(text: str) -> None:
    parsed = json.loads(text)
    print(json.dumps(parsed, indent=2))


def _print_query_result(data: Any, is_binary: bool, path: str | None = None) -> None:
    if is_binary:
        if path is not None:
            Path(path).parent.mkdir(parents=True, exist_ok=True)
            Path(path).write_bytes(data)
            print(f"Saved binary response to {path}")
            return
        sys.stdout.buffer.write(data)
        return

    if isinstance(data, str):
        try:
            _print_json(data)
        except json.JSONDecodeError:
            print(data)
        return

    print(json.dumps(data, indent=2))


def _request_json(
    base_url: str,
    endpoint: str,
    method: str = "GET",
    params: dict[str, Any] | None = None,
    timeout: float = DEFAULT_TIMEOUT,
) -> dict[str, Any]:
    data, is_binary, _status = query_api(base_url, endpoint, method=method, params=params, timeout=timeout)
    if is_binary:
        raise RuntimeError(f"{endpoint} returned binary data")
    return json.loads(data)


def _action(base_url: str, cmd: str, **params: Any) -> dict[str, Any]:
    payload = {"cmd": cmd, **params}
    return _request_json(base_url, "/action", method="POST", params=payload, timeout=30)


def _send_chat(base_url: str, message: str, target: str = "mayor") -> dict[str, Any]:
    body = json.dumps({"message": message, "target": target}).encode("utf-8")
    data, is_binary, _status = query_api(
        base_url,
        "/village/chat",
        method="POST",
        body=body,
        headers={"Content-Type": "application/json"},
        timeout=30,
    )
    if is_binary:
        raise RuntimeError("/village/chat returned binary data")
    return json.loads(data)


def _send_chat_confirm(base_url: str, confirm: bool, target: str = "mayor") -> dict[str, Any]:
    body = json.dumps({"confirm": str(confirm).lower(), "target": target}).encode("utf-8")
    data, is_binary, _status = query_api(
        base_url,
        "/village/chat/confirm",
        method="POST",
        body=body,
        headers={"Content-Type": "application/json"},
        timeout=15,
    )
    if is_binary:
        raise RuntimeError("/village/chat/confirm returned binary data")
    return json.loads(data)


def _parse_bool(value: str | None, default: bool | None = None) -> bool:
    if value is None:
        if default is None:
            raise ValueError("missing boolean value")
        return default
    normalized = value.lower()
    if normalized in {"true", "1", "yes", "y", "on"}:
        return True
    if normalized in {"false", "0", "no", "n", "off"}:
        return False
    raise ValueError(f"invalid boolean value: {value}")


def _print_response(result: Any) -> None:
    print(json.dumps(result, indent=2))


def main():
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--host", default=DEFAULT_HOST)
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)
    parser.add_argument("command", nargs="?")
    parser.add_argument("rest", nargs=argparse.REMAINDER)
    args, _unknown = parser.parse_known_args()

    if not args.command:
        usage()

    base_url = build_base_url(args.host, args.port)
    command = args.command.lower()
    rest = args.rest

    if command == "wait":
        timeout = float(rest[0]) if rest else DEFAULT_WAIT_TIMEOUT
        wait_for_ready(base_url, timeout_seconds=timeout)
        return

    if command == "health":
        _print_response(_request_json(base_url, "/health"))
        return

    if command == "state":
        _print_response(_request_json(base_url, "/state"))
        return

    if command == "metrics":
        _print_response(_request_json(base_url, "/metrics"))
        return

    if command in {"village_debug", "village-debug"}:
        _print_response(_request_json(base_url, "/village/debug"))
        return

    if command == "structures":
        _print_response(_request_json(base_url, "/structures"))
        return

    if command == "slabscan":
        radius = rest[0] if rest else "12"
        _print_response(_request_json(base_url, "/debug/slabscan", params={"radius": radius}))
        return

    if command == "screenshot":
        filepath = rest[0] if rest else "screenshot.png"
        print(f"Requesting screenshot and saving to {filepath}...")
        data, is_binary, _status = query_api(base_url, "/screenshot", params={"path": filepath}, timeout=30)
        _print_query_result(data, is_binary, path=filepath if is_binary else None)
        if is_binary:
            print(f"Screenshot saved to {filepath} successfully.")
        return

    if command in {"chat", "village_chat"}:
        params, positional = _split_tokens(rest)
        target = params.pop("target", "mayor")
        message = params.pop("message", " ".join(positional)).strip()
        if not message:
            _usage_error("chat needs a message string")
        _print_response(_send_chat(base_url, message, target=target))
        return

    if command in {"chat_confirm", "chat-confirm"}:
        params, _positional = _split_tokens(rest)
        target = params.pop("target", "mayor")
        confirm_value = params.pop("confirm", None)
        try:
            confirm = _parse_bool(confirm_value)
        except ValueError as exc:
            _usage_error(str(exc))
        _print_response(_send_chat_confirm(base_url, confirm, target=target))
        return

    if command == "action":
        if not rest:
            _usage_error("action needs a cmd name")

        action_cmd = rest[0]
        params, _positional = _split_tokens(rest[1:])
        _print_response(_action(base_url, action_cmd, **params))
        return

    if command == "request":
        if len(rest) < 2:
            _usage_error("request needs a method and endpoint")

        method = rest[0].upper()
        endpoint = rest[1]
        params, _positional = _split_tokens(rest[2:])
        body = None
        body_text = params.pop("body", None)
        if body_text is not None:
            body = body_text.encode("utf-8")
        data, is_binary, _status = query_api(base_url, endpoint, method=method, params=params or None, body=body, timeout=30)
        _print_query_result(data, is_binary)
        return

    if command == "dev":
        params, positional = _split_tokens(rest)
        dev_cmd = params.pop("cmd_line", None) or " ".join(positional)
        if not dev_cmd:
            _usage_error("dev needs cmd_line=<command> or a command string")
        _print_response(_action(base_url, "dev", cmd_line=dev_cmd))
        return

    if command in {"open_village", "close_village", "close_village_ui", "load_structure_gallery", "open_crucible", "release_keys", "shutdown"}:
        _print_response(_action(base_url, command))
        return

    if command in {"key_down", "key_up", "click", "set_look", "look", "teleport", "set_creative", "select_slot", "set_time", "set_time_scale", "recruit_villager", "assign_job", "queue_build"}:
        params, _positional = _split_tokens(rest)
        _print_response(_action(base_url, command, **params))
        return

    print(f"Unknown command: {command}")
    usage()


if __name__ == "__main__":
    main()
