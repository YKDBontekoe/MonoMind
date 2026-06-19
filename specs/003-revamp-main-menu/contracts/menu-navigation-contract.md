# Contract: Pre-Game Menu Navigation

## Purpose

Define internal navigation semantics for the pre-game menu stack so
`ScreenManager`, `GameStateMachine`, and tests share one state model. This
contract is for implementers and automated tests; players interact through the
[menu-ui-contract.md](menu-ui-contract.md) behaviors.

## GameState vs Menu Layer

| `GameState` | Menu layers allowed |
|-------------|---------------------|
| `MainMenu` | `RootHub`, `SaveBrowser`, `SettingsOverlay`, `StatsOverlay` |
| `NewWorldSetup` | N/A (full-screen setup; back target recorded) |
| `WorldLoading` | N/A (loading screen only) |
| `Playing` | N/A |

`GameState` transitions to `NewWorldSetup` and `WorldLoading` remain owned by
`GameStateMachine`; menu layers apply only while `GameState == MainMenu`.

## Layer Definitions

### RootHub

- **Draw order**: `MainMenuScreen` full viewport
- **Input owner**: `MainMenuScreen`
- **Entry**: Application start; Back from `SaveBrowser`
- **Exit actions**: See state diagram in [data-model.md](../data-model.md)

### SaveBrowser

- **Draw order**: `SaveSlotScreen` full viewport
- **Input owner**: `SaveSlotScreen`
- **Entry**: Play/Browse from `RootHub`; return from failed load
- **Overlays**: Settings and Stats render above when active

### SettingsOverlay

- **Draw order**: Scrim + `MainMenuSettingsScreen` above base layer
- **Input owner**: `MainMenuSettingsScreen` (blocks base layer input)
- **Entry**: Settings from `RootHub` or `SaveBrowser`
- **Exit**: Save → persist + pop overlay; Cancel/Escape → pop without persist

### StatsOverlay

- **Draw order**: Scrim + `PlayerDashboardScreen` above base layer
- **Input owner**: `PlayerDashboardScreen`
- **Entry**: Stats from `SaveBrowser` only
- **Exit**: Close → pop overlay

## Draw Stack Rules

```text
When SettingsOverlay active:
  1. Draw base layer (RootHub OR SaveBrowser — hidden visually under scrim)
  2. Draw MenuBackdrop/scrim at overlay alpha
  3. Draw MainMenuSettingsScreen

When StatsOverlay active:
  1. Draw SaveBrowser (base)
  2. Draw scrim
  3. Draw PlayerDashboardScreen

When no overlay:
  Draw exactly one of RootHub or SaveBrowser
```

## Back Navigation Matrix

| Current | Input | Result |
|---------|-------|--------|
| SettingsOverlay | Escape / Back | Pop overlay; restore `previousLayer` |
| StatsOverlay | Escape / Close | Pop overlay → SaveBrowser |
| SaveBrowser | Back | → RootHub |
| SaveBrowser | Escape | Cancel rename if active; else → RootHub |
| RootHub | Escape | No-op (no silent quit) |
| NewWorldSetup | Back | → recorded back target (SaveBrowser or RootHub) |

## Continue Action Contract

**Precondition**: `WorldSaveManager` returns ≥1 slot.

**Selection rule**: Choose slot with greatest `lastPlayedUtc` (ties: stable sort by
`slotId`).

**Behavior**: Invoke same load pipeline as Save Browser Load for `targetSlotId`
without switching visible layer first.

**Failure**: On load error, transition to `SaveBrowser` with error set on
matching slot.

## Window Title Conventions

| Layer / State | Title suffix |
|---------------|--------------|
| RootHub | `Autonocraft` or `Autonocraft \| Main Menu` |
| SaveBrowser | `Autonocraft \| Main Menu` |
| SettingsOverlay | `Autonocraft \| Settings` |
| StatsOverlay | `Autonocraft \| Player Stats` |
| NewWorldSetup | `Autonocraft \| New World` |
| WorldLoading | `Autonocraft \| Loading World...` |

## Test Invariants

Automated tests MUST assert:

1. Initial layer on simulated normal launch is `RootHub`.
2. `--skip-menu` never initializes menu layers.
3. Settings cancel does not change persisted `GameSettings` file contents.
4. Delete still requires two confirmations before `WorldSaveManager.DeleteSlot`.
5. Layout metrics: primary button rects within viewport height/width at 800×600
   and 1280×720.

## Non-Goals

- Changing `GameState` enum values
- Persisting menu layer to save file
- Exposing menu layer via agent HTTP API
