# Implementation Plan: Early Game Polish

**Branch**: `004-early-game-polish` | **Date**: 2026-06-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-early-game-polish/spec.md`

## Summary

Polish the opening minutes of a new world by building on the existing
starter-settlement and early-guide systems instead of adding a separate tutorial
mode. The plan keeps `EarlyGameGuide`, `PlayerStatistics.EarlyGuideStage`, the
starter settlement created in `GameStateMachine.EnterPlaying`, and the current
spawn warmup flow, then tightens the player-facing copy, first-goal pacing, and
starting-area presentation so the first few minutes feel clearer and more
intentional. No save format or agent API changes are required.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: MonoGame 3.8 DesktopGL; existing Autonocraft
systems in `Autonocraft.Core` (`EarlyGameGuide`, `GameSession`,
`GameStateMachine`, `GamePersistenceCoordinator`), `Autonocraft.Village`
(`VillageFoundingService`, `VillageManager`), `Autonocraft.World`
(`VoxelWorld`, `WorldGenParams`, structures/world generation), and
`Autonocraft.UI` for in-game presentation

**Storage**: Existing world saves and player statistics only. Opening progress
continues to use `PlayerStatistics.EarlyGuideStage` within existing save data.
No new persistence format is expected.

**Testing**: Mandatory `dotnet run --project src/Autonocraft -- --test`
because the feature touches gameplay, world generation, villagers, UI/polish,
and rendering-adjacent presentation. Add or extend integration coverage in
`tests/Autonocraft.Tests/Integration/` for starter settlement behavior,
opening-stage progression, and return-to-world behavior. Manual visual
verification via `tests/interact.py` for the opening sequence and screenshot
checks.

**Target Platform**: Desktop game on macOS, Windows, and Linux through
MonoGame DesktopGL

**Project Type**: Desktop voxel sandbox game with interactive gameplay and
agent/CLI launch paths

**Performance Goals**: No noticeable regression in startup, spawn warmup, or
early-frame simulation. Preserve the current reduced spawn-load behavior while
the opening guide is active. Any added world-generation or spawn-area work must
remain bounded to the starting region and avoid broad scans during normal play.

**Constraints**: Preserve `--skip-menu`, `--structure-gallery`, agent HTTP
readiness and `/state` fields, hotbar slot conventions, save/load semantics,
and the current town-board/village controls. Keep the opening guidance
dismissible and non-blocking. Avoid introducing a separate tutorial mode or a
new save schema unless later planning explicitly widens scope.

**Scale/Scope**: One gameplay feature centered on the first world entry, the
starter settlement, and the opening presentation state. No pause-menu, combat,
inventory, or full HUD redesign.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Code Quality and Architecture**: PASS. The likely touch points are
  `src/Autonocraft.Core/Game/EarlyGameGuide.cs`,
  `src/Autonocraft.Core/Game/GameSession.cs`,
  `src/Autonocraft/Game/GameStateMachine.cs`,
  `src/Autonocraft/Game/GamePersistenceCoordinator.cs`,
  `src/Autonocraft.Village/VillageFoundingService.cs`, and supporting UI/world
  generation code. This plan reuses existing startup and village systems rather
  than introducing a parallel tutorial framework.
- **Testing Evidence**: PASS. The feature changes gameplay/world-start
  behavior, so the headless integration suite is required:
  `dotnet run --project src/Autonocraft -- --test`. Targeted coverage should
  include starter settlement initialization, opening-stage progression, and
  opening HUD/prompt behavior.
- **User Experience Consistency**: PASS. The plan preserves existing controls,
  `V` town-board workflow, `--skip-menu`/structure-gallery launch paths, and
  `/state` / agent semantics. Any new guidance must remain dismissible and must
  not break keyboard or mouse play.
- **Performance Budget**: PASS. The feature should stay inside the current
  spawn warmup envelope and bounded local-world startup work. No new global
  scans or per-frame allocations are justified by the spec.

### Post-Design Re-check (Phase 1)

All gates remain PASS. The design does not require new public contracts or a
save migration. Existing startup state already carries the progress signal
needed for the opening sequence.

## Project Structure

### Documentation (this feature)

```text
specs/004-early-game-polish/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md             # Phase 2 (/speckit-tasks) — not created by /speckit-plan
```

### Source Code (repository root)

```text
src/
├── Autonocraft.Core/
│   ├── Game/
│   │   ├── EarlyGameGuide.cs
│   │   └── GameSession.cs
│   ├── Player/
│   │   └── PlayerStatistics.cs
│   └── Agent/
│       └── Serialization/
│           └── AgentStateSerializer.cs
├── Autonocraft/
│   ├── Game/
│   │   ├── GameStateMachine.cs
│   │   └── GamePersistenceCoordinator.cs
│   └── AutonocraftGame.cs
├── Autonocraft.Village/
│   ├── VillageFoundingService.cs
│   └── VillageManager.cs
├── Autonocraft.World/
│   └── Generation/
│       ├── WorldGenerator.cs
│       ├── BiomeMap.cs
│       ├── Decorator.cs
│       └── Structure-related generation helpers
├── Autonocraft.UI/
│   └── In-game prompt and toast presentation screens
└── Autonocraft.Engine/
    └── Rendering / UI support used for opening polish

tests/
└── Autonocraft.Tests/
    └── Integration/
        ├── VillageTests.Founding.cs
        ├── VillageTests.Ui.cs
        ├── SurvivalTests.cs
        └── WorldGenTests.cs
```

**Structure Decision**: Keep the feature inside the existing gameplay,
village, and world-generation projects. No new project, no new public service
layer, and no new external contract are required for this scope.

## Complexity Tracking

No constitution violations or added architectural complexity are currently
required.
