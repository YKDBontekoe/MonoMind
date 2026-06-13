# Autonocraft Agent Guidelines & Verification Tools

This document outlines the testing and interactive validation requirements for agent operations in the Autonocraft codebase.

> [!IMPORTANT]
> **CRITICAL RULE**: Any agent making modifications to player physics, movement, gravity, collision resolution, world chunking, blocks, or inventory MUST run the automated integration test suite to verify code correctness BEFORE concluding their task.

---

## 1. Automated Integration Test Suite
To verify core simulation subsystems (movement physics, gravity, ground collisions, jumping, inventory collection, mining, and block placing), run the built-in test suite:

```bash
dotnet run --project src/Autonocraft -- --test
```

### Expected Behavior
- The tests run in a headless, non-graphical world simulation (no Vulkan or GLFW displays are required).
- All tests should print `PASSED`.
- The process will exit with code `0` on success. Any failure will print a detailed stack trace and exit with code `1`.

---

## 2. Headless Agent Server
If you need to run the game in the background on a headless server (e.g., in a terminal sandbox without visual window rendering) to query state or simulate ticks:

```bash
dotnet run --project src/Autonocraft -- --headless
```

This starts the simulation loop at 60 Hz and spins up a local HTTP API server at `http://localhost:5000/`.

---

## 3. Visual Navigation & Interactive Mode
When you run the game normally (windowed mode):
```bash
dotnet run --project src/Autonocraft
```
The game opens on the screen, and **simultaneously starts the HTTP API server on port 5000 in the background**. This allows you to interact with the game visually and programmatically at the same time.

### Available HTTP Endpoints:
- `GET http://localhost:5000/state` - Returns JSON representing player coordinates, velocity, orientation, active hotbar slot, and inventory items.
- `GET http://localhost:5000/screenshot` - Captures the Vulkan framebuffer, saves it as a 32-bit BMP image on the server, and returns it.
- `POST http://localhost:5000/action?cmd=<command>&[params]` - Queues inputs or actions to be executed thread-safely on the game loop's main thread.

---

## 4. Interaction CLI Script
A Python helper script is provided at `tests/interact.py` to communicate with the game server.

### Examples:
- **Get state**:
  ```bash
  python3 tests/interact.py state
  ```
- **Take a screenshot**:
  ```bash
  python3 tests/interact.py screenshot my_view.bmp
  ```
- **Simulate moving forward**:
  ```bash
  python3 tests/interact.py action key_down key=w
  # wait a moment...
  python3 tests/interact.py action key_up key=w
  ```
- **Simulate mining**:
  ```bash
  python3 tests/interact.py action click button=left
  ```
- **Simulate block placement**:
  ```bash
  python3 tests/interact.py action click button=right
  ```
- **Teleport**:
  ```bash
  python3 tests/interact.py action teleport x=16.5 y=45.0 z=16.5
  ```
- **Shutdown**:
  ```bash
  python3 tests/interact.py action shutdown
  ```
