#!/usr/bin/env python3
import argparse
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 5001


def build_base_url(host: str, port: int) -> str:
    return f"http://{host}:{port}"


def query_api(base_url: str, endpoint: str, method="GET", params=None, timeout=5):
    url = f"{base_url}{endpoint}"
    if params:
        query_string = urllib.parse.urlencode(params)
        url = f"{url}?{query_string}"

    req = urllib.request.Request(url, method=method, data=b"" if method == "POST" else None)
    if method == "POST":
        req.add_header("Content-Length", "0")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            if response.headers.get_content_type() == "image/png":
                return response.read(), True, response.status
            body = response.read().decode("utf-8")
            return body, False, response.status
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")
        try:
            parsed = json.loads(body)
            detail = parsed.get("error") or parsed.get("message") or body
        except json.JSONDecodeError:
            detail = body or str(e)
        print(f"HTTP {e.code}: {detail}")
        sys.exit(1)
    except Exception as e:
        print(f"Error connecting to Autonocraft game server at {base_url}: {e}")
        print("Make sure the game is in Playing state (past world load).")
        sys.exit(1)


def wait_for_ready(base_url: str, timeout_seconds: float = 120.0, poll_interval: float = 0.5):
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


def usage():
    print("Autonocraft Interaction Tool CLI")
    print("Usage:")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] state")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] wait [timeout_seconds]")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] screenshot [file_path]")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] village_chat \"Send peasants to gather wood\"")
    print("  python3 tests/interact.py [--host HOST] [--port PORT] action <cmd> [key=val ...]")
    print("\nExamples:")
    print("  python3 tests/interact.py wait")
    print("  python3 tests/interact.py state")
    print("  python3 tests/interact.py action key_down key=w")
    print("  python3 tests/interact.py action release_keys")
    print("  python3 tests/interact.py action dev cmd_line=\"give Stone 64\"")
    sys.exit(1)


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
        timeout = float(rest[0]) if rest else 120.0
        wait_for_ready(base_url, timeout_seconds=timeout)
        return

    if command == "state":
        data, is_binary, _status = query_api(base_url, "/state")
        parsed = json.loads(data)
        print(json.dumps(parsed, indent=2))
        return

    if command == "screenshot":
        filepath = rest[0] if rest else "screenshot.png"
        print(f"Requesting screenshot and saving to {filepath}...")
        data, is_binary, _status = query_api(base_url, "/screenshot", params={"path": filepath})
        if is_binary:
            with open(filepath, "wb") as f:
                f.write(data)
            print(f"Screenshot saved to {filepath} successfully.")
        else:
            print(f"Server returned error message: {data}")
        return

    if command == "village_chat":
        if not rest:
            print("Error: village_chat needs a message string.")
            usage()
        message = " ".join(rest)
        body = json.dumps({"message": message, "target": "mayor"}).encode("utf-8")
        req = urllib.request.Request(
            f"{base_url}/village/chat",
            data=body,
            method="POST",
            headers={"Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(req, timeout=30) as response:
                parsed = json.loads(response.read().decode("utf-8"))
                print(json.dumps(parsed, indent=2))
        except urllib.error.HTTPError as e:
            print(f"HTTP {e.code}: {e.read().decode('utf-8', errors='replace')}")
            sys.exit(1)
        return

    if command == "action":
        if not rest:
            print("Error: Action command needs a cmd name.")
            usage()

        action_cmd = rest[0]
        params = {"cmd": action_cmd}

        for arg in rest[1:]:
            if "=" in arg:
                k, v = arg.split("=", 1)
                params[k] = v
            else:
                print(f"Ignoring invalid argument: {arg}")

        print(f"Sending action '{action_cmd}' with parameters: {params}")
        data, is_binary, _status = query_api(base_url, "/action", method="POST", params=params)
        parsed = json.loads(data)
        print(json.dumps(parsed, indent=2))
        return

    print(f"Unknown command: {command}")
    usage()


if __name__ == "__main__":
    main()
