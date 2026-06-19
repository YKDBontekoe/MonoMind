# Autonocraft

[![CI](https://github.com/YKDBontekoe/MonoMind/actions/workflows/ci.yml/badge.svg)](https://github.com/YKDBontekoe/MonoMind/actions/workflows/ci.yml)
[![CodeQL](https://github.com/YKDBontekoe/MonoMind/actions/workflows/codeql.yml/badge.svg)](https://github.com/YKDBontekoe/MonoMind/actions/workflows/codeql.yml)

A 3D voxel sandbox built with **MonoGame** (DesktopGL) on **.NET 10**. Explore procedurally generated biomes, mine and place blocks, fight animals, craft stations via sigil patterns, use tools and skills, and save worlds to disk. An HTTP agent API lets automated tools drive the game while it runs.

> **Repo:** MonoMind · **Game:** Autonocraft

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **macOS (optional):** Homebrew OpenGL libs; `run.sh` and `start.command` set `DYLD_LIBRARY_PATH=/opt/homebrew/lib`
- **Python 3** (optional) — for `tests/interact.py` and E2E scripts

## Quick Start

```bash
# Restore dependencies (first time)
dotnet build Autonocraft.slnx

# Run the game (root hub → continue, browse saves, new world, or settings)
dotnet run --project src/Autonocraft

# Skip the menu and jump straight into a new world
dotnet run --project src/Autonocraft -- --skip-menu

# Run the headless integration test suite
dotnet run --project src/Autonocraft -- --test
```

On macOS you can also double-click `start.command` or run `./run.sh`.

## Controls (in-game)

| Input | Action |
|-------|--------|
| Mouse | Look around |
| WASD | Move |
| Space | Jump (physics) / swim up / fly up (creative mode) |
| Left Shift | Swim down / fly down (creative mode) |
| G | Toggle survival / creative mode |
| Left click | Attack animals or mine blocks (5-block range) |
| Right click | Place block or open crafting station UI |
| Shift + Right click | Activate sigil pattern → crafting station |
| 1–9 / scroll wheel | Select hotbar slot |
| J | Open discovery journal |
| Escape | Pause menu (save, main menu, quit) |
| F3 or `` ` `` | Developer console |

Worlds auto-save every 5 minutes and on exit.

## CLI Flags

| Flag | Description |
|------|-------------|
| `--test` | Run integration tests headlessly and exit (code 0 = pass, 1 = fail) |
| `--skip-menu` | Bypass main menu; start world generation immediately |

## Project Layout

```
MonoMind/
├── src/
│   ├── Autonocraft/          # Main game executable
│   │   ├── Core/             # Game loop, player, systems, HTTP API
│   │   ├── Engine/           # Rendering, camera, shaders, particles
│   │   ├── World/            # Voxel world, chunks, generation, saves
│   │   │   └── Structures/   # Procedural structure placement
│   │   ├── Items/            # Tools, skills, mining calculator
│   │   ├── Crafting/         # Sigils, recipes, crucible, journal
│   │   ├── Entities/         # Animals, collision, raycasts
│   │   ├── UI/               # Menus, crucible, journal, death screen
│   │   ├── base_textures/    # Source animal texture PNGs
│   │   └── Content/          # MonoGame content (BlockEffect.fx → .xnb)
│   └── Autonocraft.Domain/   # Shared types (BlockType, items, crafting defs)
├── tests/
│   ├── Autonocraft.Tests/    # Integration + unit tests
│   └── interact.py           # HTTP agent CLI helper
├── scripts/                  # Atlas build and texture utilities
├── .cursor/rules/            # Cursor agent guardrails
├── AGENTS.md                 # Agent / automation guidelines
├── agent.md                  # Pointer to AGENTS.md
└── docs/
    ├── ARCHITECTURE.md       # Technical architecture reference
    └── CODEMAP.md            # File index and task playbooks
```

## Saves and Settings

| Data | Location |
|------|----------|
| World saves | `~/Library/Application Support/Autonocraft/saves/` (macOS), `%LOCALAPPDATA%/Autonocraft/saves/` (Windows), `~/.local/share/Autonocraft/saves/` (Linux) |
| Game settings | Same `Autonocraft/` folder under local app data (`settings.json`) |

Each save slot contains `world.json` (version 4) with seed, player state (position, hotbar with tools, skills), block modifications, fluid modifications, crafting discoveries, and time-of-day. Animals are not saved — they respawn on load.

## Texture Atlas

Block and entity textures are packed into `src/Autonocraft/atlas.png` using layout metadata in `atlas_layout.json`. Regenerate with:

```bash
dotnet run --project src/Autonocraft.AtlasBuild
```

The C# runtime also has `ProceduralAtlasBuilder` as a fallback when the PNG is missing.

## Agent HTTP API

When gameplay starts, a local server listens on **http://localhost:5001/** (override with `--agent-port`). Use it to query state, capture screenshots, and queue input actions. See [AGENTS.md](AGENTS.md) for the full API reference and [tests/interact.py](tests/interact.py) for a Python CLI wrapper.

## Releases

Pushes to `main` trigger an automated release pipeline after CI passes:

1. **Version** — bumps `VERSION` (semver from conventional commits: `feat` → minor, `fix` → patch, `!`/`BREAKING CHANGE` → major), prepends [CHANGELOG.md](CHANGELOG.md), commits `chore(release): vX.Y.Z`, and pushes tag `vX.Y.Z`.
2. **Release** — builds and publishes zip assets for Linux, Windows, and macOS (x64 + arm64) to [GitHub Releases](https://github.com/YKDBontekoe/MonoMind/releases).

Manual releases are still supported via **Actions → Release → Run workflow** (creates a draft).

## Continuous Integration

GitHub Actions runs on every push and pull request to `main`/`master`, plus a nightly schedule at 06:00 UTC.

| Workflow | What it runs |
|----------|----------------|
| [CI](.github/workflows/ci.yml) | Build + unit tests + headless integration (`--test`) on Ubuntu, Windows, and macOS |
| [Quality](.github/workflows/quality.yml) | `dotnet format`, atlas validation, unit-test code coverage |
| [CodeQL](.github/workflows/codeql.yml) | C# security analysis |
| [Version](.github/workflows/version.yml) | After CI on `main`: semver bump, [CHANGELOG.md](CHANGELOG.md) update, git tag |
| [Release](.github/workflows/release.yml) | Multi-platform publish on version tags (`v*.*.*`) |

Reproduce CI locally:

```bash
# Unit tests (same filter as CI)
dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Unit"

# Headless integration (same as CI integration job)
dotnet run --project src/Autonocraft -c Release -- --test

# Format and atlas checks
dotnet format Autonocraft.slnx --verify-no-changes
dotnet run --project src/Autonocraft.AtlasBuild --check
```

## OpenRouter (planned)

`openrouter_key.example.txt` documents how to configure an OpenRouter API key for future LLM integration. Copy it to `openrouter_key.txt` (gitignored) or set `OPENROUTER_API_KEY` in the environment.

## Documentation

- **[AGENTS.md](AGENTS.md)** — Rules and tools for AI agents modifying or testing the codebase
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — World generation, rendering, and subsystem design
- **[docs/CODEMAP.md](docs/CODEMAP.md)** — File index and task playbooks for agents

## Development

```bash
# Build entire solution
dotnet build Autonocraft.slnx -c Release

# Faster iteration (SkipMonoGameContent is a no-op; no MGCB step in this repo)
dotnet build src/Autonocraft -p:SkipMonoGameContent=true

# Run unit tests only
dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Unit"

# Run headless integration suite
dotnet run --project src/Autonocraft -c Release -- --test
```

When changing player physics, world generation, chunking, blocks, inventory, tools, crafting, fluids, animals, combat, or saves, always run `--test` before finishing. See AGENTS.md for the required test areas and HTTP verification workflow.
