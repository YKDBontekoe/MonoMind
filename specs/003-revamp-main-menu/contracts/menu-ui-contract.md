# Contract: Pre-Game Main Menu UI

## Purpose

Define player-facing behavior for the revamped pre-game menu flow (launch through
world load) without prescribing MonoGame classes, pixel layout, or file names.

## Entry Points

| Trigger | Behavior |
|---------|----------|
| Normal launch (no `--skip-menu`) | Opens **Root Hub** with title, tagline, and primary actions |
| `--skip-menu` | Bypasses menu; loads world immediately (unchanged) |
| `--structure-gallery` | Bypasses hub; loads structure gallery world (unchanged) |
| Return from gameplay (pause/death → Main Menu) | Opens **Save Browser** with slots refreshed; may also land on Root Hub per implementation choice, but save list MUST be current |

## Root Hub

The player MUST see without opening sub-panels:

| Element | Requirement |
|---------|-------------|
| Title | Game name visible |
| Tagline | Short descriptive subtitle |
| Continue | Shown when a recent save exists; loads that save in one action |
| Play / Browse Saves | Opens save browser |
| New World | Opens new world setup |
| Settings | Opens settings overlay |
| Quit | Exits application explicitly |

Keyboard: arrow keys or Tab + Enter MUST reach and activate every primary action.
Escape on Root Hub MUST NOT quit silently without a labeled Quit action visible.

## Save Browser

| Element | Requirement |
|---------|-------------|
| Slot list | Selectable saves with scroll when count exceeds visible window |
| Detail pane | Name, last played, stats/progress cues for selected slot |
| Load | Loads selected save; double-click shortcut preserved |
| New World | Opens new world setup |
| Rename | Inline rename with confirm/cancel |
| Delete | Two-step confirmation before permanent delete |
| Stats | Opens player statistics overlay |
| Settings | Opens settings overlay |
| Structure Gallery | Loads gallery world (label consistent with docs) |
| Quit | Exits application |
| Back | Returns to Root Hub |
| Error banner | Inline message after load failure with retry guidance |

Keyboard: Up/Down select slots; Enter loads; Delete triggers delete confirmation
flow; Escape cancels rename or navigates back.

## New World Setup

| Element | Requirement |
|---------|-------------|
| World type | Selectable types with plain-language labels |
| Seed | Editable seed with validation feedback |
| Create | Starts world loading |
| Back | Returns to prior menu layer without creating |

## Settings Overlay

| Section | Requirement |
|---------|-------------|
| Graphics | Render distance slider, VSync toggle, high-quality lighting toggle |
| Audio | Master/SFX/Ambient/Music sliders, mute toggle |
| Village AI | Play-with-AI toggle, provider cycle, credential fields |
| Save | Persists to settings file and applies live |
| Back / Escape | Discards unsaved edits |

## Stats Overlay

| Element | Requirement |
|---------|-------------|
| Content | Player/world statistics for selected save (existing dashboard scope) |
| Close / Back | Returns to prior menu layer |

## Loading Screen

| Element | Requirement |
|---------|-------------|
| Progress | Plain-language loading status |
| Failure | Returns to Save Browser with error; slot list intact |
| Timeout | Message suggests alternate action (existing timeout reason pattern) |

## Visual Consistency

All pre-game menu screens MUST share:

- Common backdrop/scrim treatment
- `UiTheme` title, body, hint, and button styles
- Brief fade transitions (<1s perceived blocking time)
- Visible hover and keyboard focus states on interactive elements

## Out of Scope

- In-game pause menu layout changes (token sharing optional)
- Village Town Board, inventory, journal, or HUD redesign
- Agent HTTP API changes

## Accessibility & Layout

| Resolution | Requirement |
|------------|-------------|
| 800×600 | Primary actions readable; no overlapping controls |
| 1280×720 | Default reference; balanced layout |
| Large windows | Content centered; no orphaned off-screen actions |
