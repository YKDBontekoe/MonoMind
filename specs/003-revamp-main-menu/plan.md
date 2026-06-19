# Implementation Plan: Revamped Main Menu UI

**Branch**: `003-revamp-main-menu` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-revamp-main-menu/spec.md`

## Summary

Revamp the pre-game menu into a clear two-tier flow: a polished **root hub**
(`MainMenuScreen`, currently unused) for primary actions, and the existing
**save browser** (`SaveSlotScreen`) for world selection and management. Unify
navigation, keyboard focus, transitions, and visual chrome across
`MainMenuScreen`, `SaveSlotScreen`, `NewWorldSetupScreen`, `MainMenuSettingsScreen`,
`PlayerDashboardScreen`, and `LoadingScreen` using existing `UiTheme`,
`MenuBackdrop`, `UiTransition`, and `UiLayout` — no new dependencies or save
format changes. Wire `MainMenuScreen` into `ScreenManager` and
`GameStateMachine.UpdateMainMenu`, add a small `MenuNavigationState` model to
replace scattered overlay booleans, and extend integration tests for menu layout
and navigation invariants.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: MonoGame 3.8 DesktopGL; existing Autonocraft UI engine
(`Autonocraft.Engine`: `UiRenderer`, `UiTheme`, `MenuBackdrop`, `UiTransition`,
`UiLayout`); world persistence via `WorldSaveManager` / `SaveSlotInfo`; settings
via `GameSettingsManager`

**Storage**: No new save format version. Settings continue round-tripping through
`settings.json` via `GameSettingsManager`. Save slot metadata read from existing
world save directories.

**Testing**: Mandatory `dotnet run --project src/Autonocraft -- --test`. Add
focused menu tests in `tests/Autonocraft.Tests/Integration/MenuTests.cs` for
navigation state, keyboard focus indices, settings overlay open/close, and
layout metrics at 1280×720 and 800×600. Existing `RunGameSettingsRoundTrip` must
remain green. Manual validation documented in `quickstart.md`.

**Target Platform**: Desktop game on macOS, Windows, and Linux through MonoGame
DesktopGL

**Project Type**: Desktop voxel sandbox game with pre-game menu flow and agent
CLI bypass (`--skip-menu`, `--structure-gallery`)

**Performance Goals**: Menu screens are idle UI; target no measurable frame-time
regression. Reuse existing `MenuBackdrop` animation and 0.2–0.3s
`UiTransition` fades. Avoid per-frame allocations in menu update (reuse hover
arrays, pre-sized focus lists).

**Constraints**: Preserve `--skip-menu`, structure gallery CLI entry, save/load
semantics, settings fields, and window title conventions. Root menu Escape must
not silently quit without affordance (move quit to explicit action). Minimum
supported layout: 800×600 (CI agent window) through default 1280×720.

**Scale/Scope**: Presentation and navigation layer over six existing menu
screens; no pause menu, village UI, or HUD redesign in this feature.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Code Quality and Architecture**: PASS. UI stays in `src/Autonocraft/UI/`.
  Navigation orchestration stays in `ScreenManager` and
  `Game/GameStateMachine.cs`. Shared focus/navigation helpers may live in
  `src/Autonocraft/UI/Menu/` (new folder, same project). Persistence stays in
  `Autonocraft.World` / `Autonocraft.Core` — no domain logic in draw paths.
  Reuse orphaned `MainMenuScreen` rather than duplicating hub layout inside
  `SaveSlotScreen`.
- **Testing Evidence**: PASS. Mandatory integration suite:
  `dotnet run --project src/Autonocraft -- --test`. Add `MenuTests` for
  navigation stack, Continue shortcut eligibility, and layout bounds. Manual
  keyboard walkthrough in `quickstart.md`.
- **User Experience Consistency**: PASS. Primary actions visible on root hub
  within two interactions (SC-001). `--skip-menu` and structure gallery
  unchanged. Settings categories and save operations preserved. Keyboard Tab/arrow
  + Enter navigation added consistently; no agent HTTP contract changes.
- **Performance Budget**: PASS. No world/chunk/render hot-path changes. Menu
  backdrop and card drawing already exist; revamp reorganizes screens and
  shared chrome only.

### Post-Design Re-check (Phase 1)

All gates remain PASS. `MenuNavigationState` is a UI-layer read/write model only.
Contracts document player-facing flows without prescribing MonoGame classes.
No save format or agent API changes.

## Project Structure

### Documentation (this feature)

```text
specs/003-revamp-main-menu/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── menu-ui-contract.md
│   └── menu-navigation-contract.md
└── tasks.md             # Phase 2 (/speckit-tasks) — not created by /speckit-plan
```

### Source Code (repository root)

```text
src/
├── Autonocraft/
│   ├── ScreenManager.cs
│   ├── Game/
│   │   ├── GameStateMachine.cs
│   │   └── AutonocraftGame.Draw.cs
│   └── UI/
│       ├── MainMenuScreen.cs          # wire as root hub; extend actions
│       ├── SaveSlotScreen.cs          # refine as save browser sub-screen
│       ├── NewWorldSetupScreen.cs
│       ├── MainMenuSettingsScreen.cs
│       ├── PlayerDashboardScreen.cs
│       ├── LoadingScreen.cs
│       └── Menu/                      # new shared helpers (optional folder)
│           ├── MenuNavigationState.cs
│           ├── MenuFocusList.cs
│           └── MenuChrome.cs
├── Autonocraft.Engine/
│   └── Ui/
│       ├── UiTheme.cs
│       ├── MenuBackdrop.cs
│       ├── UiTransition.cs
│       └── UiLayout.cs
└── Autonocraft.World/
    └── WorldSaveManager.cs            # most-recent save lookup for Continue

tests/
└── Autonocraft.Tests/
    └── Integration/
        └── MenuTests.cs               # new
```

**Structure Decision**: Extend existing menu screen classes and wire
`MainMenuScreen` into the live flow. Introduce a small `Menu/` helper folder for
shared focus and navigation state rather than a new project or framework.

## Complexity Tracking

No constitution violations or added architectural complexity are currently
required.
