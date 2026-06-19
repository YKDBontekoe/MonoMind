# Research: Revamped Main Menu UI

## Decision: Two-Tier Hub + Save Browser Flow

**Rationale**: `GameState.MainMenu` currently renders `SaveSlotScreen` directly
(`ScreenManager.DrawMainMenu`), while `MainMenuScreen` exists with Play/Quit only
and is never instantiated. The spec requires a welcoming entry with primary
actions visible first (FR-001, SC-001). A root hub screen followed by the
existing save browser delivers P1 without rewriting save management.

**Alternatives considered**:

- **Enhance SaveSlotScreen only** — Rejected because the save browser is dense
  (sidebar, detail pane, seven action buttons); it cannot simultaneously serve as
  a minimal branded entry without clutter.
- **New GameState enum value** — Rejected; sub-navigation within `MainMenu` via
  `MenuNavigationState` avoids ripple changes to music routing, agent bypass, and
  fade logic tied to `GameState`.

## Decision: MenuNavigationState Over Scattered Overlay Booleans

**Rationale**: `ScreenManager` tracks `_mainMenuSettingsOpen` and
`_playerDashboardOpen` as independent flags while `SaveSlotScreen` is always the
base layer. A single `MenuNavigationState` enum/stack
(`RootHub`, `SaveBrowser`, `SettingsOverlay`, `StatsOverlay`) clarifies draw
order, back navigation, and test assertions.

**Alternatives considered**:

- **Keep boolean flags** — Rejected; back-from-settings behavior and keyboard
  focus reset are harder to test and reason about as more overlays accrue.

## Decision: Wire and Extend MainMenuScreen as Root Hub

**Rationale**: Reusing the existing class matches constitution I (prefer existing
code). Extend actions: Continue (when saves exist), Play/Browse Saves, New World,
Settings, Structure Gallery (footer or secondary row), Quit. Remove hidden
Escape-to-quit on root; show Quit explicitly per spec acceptance scenario P1-4.

**Alternatives considered**:

- **Delete MainMenuScreen and build hub inside SaveSlotScreen** — Rejected;
  duplicates title/backdrop code already in `MainMenuScreen` and increases
  `SaveSlotScreen` size (~900 lines).

## Decision: Continue Uses Most Recently Played Save

**Rationale**: Spec assumption allows Continue reflecting most recent save.
`SaveSlotInfo` / `WorldSaveManager` already expose slot metadata for sorting;
select most recent by `LastPlayedUtc` (or equivalent existing field) and load on
Continue without opening the browser.

**Alternatives considered**:

- **Continue opens save browser with selection** — Valid but adds an interaction;
  deferred as fallback when multiple saves exist and no recent metadata is
  available.

## Decision: Shared MenuChrome and MenuFocusList Helpers

**Rationale**: `SaveSlotScreen`, `NewWorldSetupScreen`, and `MainMenuSettingsScreen`
each duplicate backdrop draw, scrim, fade alpha, and ad-hoc hover index tracking.
Extract `MenuChrome.DrawBackdrop(...)` and `MenuFocusList` (vertical button list
with keyboard Up/Down/Enter) to satisfy FR-006 and FR-009 consistently.

**Alternatives considered**:

- **Full UI framework** — Rejected; over-engineering for ~6 screens.
- **Copy-paste per screen** — Rejected; violates cohesion goal (P4) and
  keyboard parity.

## Decision: Preserve SaveSlotScreen Feature Surface: Refine Layout Only

**Rationale**: `SaveSlotScreen` already supports load, new world, delete with
confirmation, rename, stats, settings, structure gallery, quit, keyboard
navigation, and error flash. FR-002 and FR-005 require preservation; revamp
focuses on visual hierarchy, empty-state copy, and consistent chrome — not
removing actions.

**Alternatives considered**:

- **Replace with simplified slot list** — Rejected; would drop lifetime stats,
  detail hero, and rename flows players rely on.

## Decision: Align Transition Timing with Existing UiTransition

**Rationale**: `UiTransition.BeginFadeIn(0.25f)` is used across
`ScreenManager.HandleStateTransition` and settings overlay. Spec SC-007 requires
<1s blocking animation; keep 0.2–0.3s fades and slide offsets already used by
`MainMenuSettingsScreen` and `NewWorldSetupScreen`.

**Alternatives considered**:

- **Longer cinematic transitions** — Rejected; conflicts with SC-007 and agent
  test startup expectations.

## Decision: No Agent HTTP or Save Format Changes

**Rationale**: Pre-game menu revamp is player-facing only. `--skip-menu` and
`--structure-gallery` bypass the menu entirely. Agent readiness and `/state` are
unaffected. Constitution III and V satisfied without contract churn.

**Alternatives considered**:

- **Expose menu state on agent API** — Deferred; not required by spec.

## Decision: Test Strategy — Layout Metrics + Navigation State

**Rationale**: No headless pixel tests exist for menu today; village flow added
`RunVillageScreenInputLayout` as precedent. Menu tests should assert:
`MenuNavigationState` transitions, Continue eligibility given fixture saves,
settings overlay open/close without mutating saved settings on cancel, and
computed layout rectangles within viewport at 1280×720 and 800×600.

**Alternatives considered**:

- **Manual-only verification** — Insufficient for constitution II on UI changes.

## Decision: Pause Menu Out of Scope; Token Alignment Optional

**Rationale**: Spec assumptions exclude pause menu redesign. `PauseMenuScreen`
duplicates settings sliders — optional follow-up to share `MenuChrome` only if
touch points overlap during implementation; not a planning gate.

**Alternatives considered**:

- **Unified pause + main menu settings component** — Good future refactor; include
  only if implementation touches shared slider drawing without scope creep.
