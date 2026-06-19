# Data Model: Revamped Main Menu UI

## Menu Navigation State

Tracks which pre-game screen or overlay is active while `GameState == MainMenu`.
Not persisted; held in `ScreenManager` (or `MenuNavigationState` helper).

**Fields**

- `layer`: `RootHub`, `SaveBrowser`, `SettingsOverlay`, `StatsOverlay`
- `previousLayer`: For back navigation and focus restore
- `transition`: Reference to active `UiTransition` alpha/offset for draw

**State transitions**

```text
RootHub
  ├─ Continue (has recent save) → WorldLoading
  ├─ Play / Browse Saves → SaveBrowser
  ├─ New World → GameState.NewWorldSetup
  ├─ Settings → SettingsOverlay
  ├─ Structure Gallery → WorldLoading (gallery world)
  └─ Quit → application exit

SaveBrowser
  ├─ Load selected → WorldLoading
  ├─ New World → GameState.NewWorldSetup
  ├─ Settings → SettingsOverlay
  ├─ Stats → StatsOverlay
  ├─ Back → RootHub
  └─ Quit → application exit

SettingsOverlay / StatsOverlay
  ├─ Save (settings only) → persist GameSettings, close overlay
  ├─ Back / Escape (cancel) → discard unsaved settings edits, restore previousLayer
  └─ Close stats → restore previousLayer
```

**Validation Rules**

- Only one overlay (`Settings`, `Stats`) active at a time.
- `RootHub` MUST be the initial layer on normal launch (not `--skip-menu`).
- Back from `SaveBrowser` returns to `RootHub`, not application exit.

## Menu Focus List (transient UI)

Keyboard/mouse focus model for vertical button rows on hub and primary actions.

**Fields**

- `items`: Ordered list of focusable action ids (`Continue`, `Play`, `NewWorld`, …)
- `focusedIndex`: Currently highlighted item (-1 when mouse-only hover overrides)
- `hoverIndex`: Mouse hover target for visual feedback

**Validation Rules**

- Up/Down wrap or clamp within `items` bounds (match existing `SaveSlotScreen`
  arrow-key behavior).
- Enter activates `items[focusedIndex]` when focus visible.
- Tab cycles focusable fields in settings overlay (existing behavior preserved).

## Save Slot (read model)

Existing entity from `WorldSaveManager`; menu consumes metadata for display and
Continue.

**Fields**

- `slotId`: Unique save identifier
- `displayName`: Player-visible world name
- `lastPlayedUtc`: Timestamp for Continue sorting
- `worldType`: Optional type label for detail pane
- `playTime`, `playerStats`: Summary stats for detail hero (existing)
- `loadError`: Transient inline error message after failed load (existing
  `_loadErrorMessage` pattern)

**Relationships**

- Many slots listed in `SaveBrowser` layer.
- One slot selected as `selectedSlotId` for load/rename/delete/stats.
- Most recent slot drives `Continue` on `RootHub`.

**Validation Rules**

- Delete requires two-step confirmation (`_confirmingDelete` pattern preserved).
- Rename buffer max 32 characters (existing `MaxRenameLength`).
- Empty slot list shows guided empty state pointing to New World (edge case).

## New World Setup (transient configuration)

Existing `NewWorldSetupScreen` state; unchanged semantics.

**Fields**

- `selectedWorldType`: `WorldType` enum index
- `seedText` / `selectedSeed`: Parsed world seed
- `seedFocused`: Text entry focus flag

**State transitions**

- Create → `GameState.WorldLoading` with pending new world parameters
- Back → `SaveBrowser` or `RootHub` depending on entry path

**Validation Rules**

- Invalid seed rejected with inline message (FR-003).
- Back preserves menu navigation stack without orphan overlays.

## Menu Settings Panel (working copy)

Existing `MainMenuSettingsScreen` working copy of `GameSettings`.

**Fields**

- `working`: Clone of `GameSettings` on open
- `activeField`: Text field focus for AI provider credentials
- `sliderTarget`: Active drag target for render distance / volume sliders

**State transitions**

- Open → clone current settings into `working`
- Save → `GameSettingsManager.Save`, apply live settings callbacks, close overlay
- Cancel → discard `working`, close overlay

**Validation Rules**

- `working.Clamp()` applied before save (existing).
- API keys masked in display (existing `MaskKey`).
- Unsaved changes MUST NOT persist on cancel (FR-004).

## Continue Action Eligibility (derived)

Computed when rendering `RootHub`.

**Fields**

- `isAvailable`: True when at least one save exists with valid metadata
- `targetSlotId`: Most recently played slot id
- `label`: e.g. "Continue — {displayName}" or "Play"

**Validation Rules**

- When `isAvailable` false, Continue hidden or replaced with Play/Browse Saves
  only.
- Continue load uses same path as `SaveSlotScreen.TryRequestLoad` for consistency.
