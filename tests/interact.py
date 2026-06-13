#!/usr/bin/env python3
import sys
import urllib.request
import urllib.parse
import json

BASE_URL = "http://localhost:5000"

def usage():
    print("Autonocraft Interaction Tool CLI")
    print("Usage:")
    print("  python3 tests/interact.py state                     - Query current player/world state")
    print("  python3 tests/interact.py screenshot [file_path]    - Capture Vulkan framebuffer and save it")
    print("  python3 tests/interact.py action <cmd> [key/val]    - Send action command")
    print("\nExamples:")
    print("  python3 tests/interact.py action key_down key=w")
    print("  python3 tests/interact.py action key_up key=w")
    print("  python3 tests/interact.py action click button=left")
    print("  python3 tests/interact.py action set_flying flying=false")
    print("  python3 tests/interact.py action teleport x=16 y=45 z=16")
    print("  python3 tests/interact.py action look dx=10 dy=0")
    print("  python3 tests/interact.py action select_slot slot=2")
    print("  python3 tests/interact.py action shutdown")
    sys.exit(1)

def query_api(endpoint, method="GET", params=None):
    url = f"{BASE_URL}{endpoint}"
    if params:
        query_string = urllib.parse.urlencode(params)
        url = f"{url}?{query_string}"
    
    req = urllib.request.Request(url, method=method)
    try:
        with urllib.request.urlopen(req, timeout=5) as response:
            if response.headers.get_content_type() == "image/bmp":
                return response.read(), True
            return response.read().decode('utf-8'), False
    except Exception as e:
        print(f"Error connecting to Autonocraft game server: {e}")
        print("Make sure the game is running in windowed or headless mode (port 5000).")
        sys.exit(1)

def main():
    if len(sys.argv) < 2:
        usage()

    command = sys.argv[1].lower()

    if command == "state":
        data, is_binary = query_api("/state")
        parsed = json.loads(data)
        print(json.dumps(parsed, indent=2))
        
    elif command == "screenshot":
        filepath = sys.argv[2] if len(sys.argv) > 2 else "screenshot.bmp"
        print(f"Requesting screenshot and saving to {filepath}...")
        
        # We also pass the filepath parameter to the server so it writes it on the server-side,
        # but we also download the response bytes and write them locally to be sure.
        data, is_binary = query_api("/screenshot", params={"path": filepath})
        if is_binary:
            with open(filepath, "wb") as f:
                f.write(data)
            print(f"Screenshot saved to {filepath} successfully.")
        else:
            print(f"Server returned error message: {data}")
            
    elif command == "action":
        if len(sys.argv) < 3:
            print("Error: Action command needs a cmd name.")
            usage()
        
        action_cmd = sys.argv[2]
        params = {"cmd": action_cmd}
        
        # Parse extra key=value arguments
        for arg in sys.argv[3:]:
            if "=" in arg:
                k, v = arg.split("=", 1)
                params[k] = v
            else:
                print(f"Ignoring invalid argument: {arg}")
                
        print(f"Sending action '{action_cmd}' with parameters: {params}")
        data, is_binary = query_api("/action", method="POST", params=params)
        parsed = json.loads(data)
        print(json.dumps(parsed, indent=2))
        
    else:
        print(f"Unknown command: {command}")
        usage()

if __name__ == "__main__":
    main()
