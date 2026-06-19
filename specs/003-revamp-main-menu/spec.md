# Feature Specification: Revamped Main Menu UI

**Feature Branch**: `003-revamp-main-menu`

**Created**: 2026-06-19

**Status**: Draft

**Input**: User description: "I want to revamp and improve the menu menu UI"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Arrive at a Clear, Polished Entry Screen (Priority: P1)

As a player launching the game, I want the main menu to feel intentional, readable, and welcoming — with obvious paths to continue playing, start fresh, adjust settings, or exit — so that I never wonder where to go or what the game offers before entering a world.

**Why this priority**: The main menu is the first impression. Today the flow jumps straight into save-slot management with limited top-level navigation; a revamp must establish hierarchy, visual identity, and primary actions up front.

**Independent Test**: Launch the game without `--skip-menu`, land on the revamped main menu, and verify that Continue/Play, New World, Settings, and Quit (or equivalent clearly labeled actions) are visible, readable, and operable via mouse and keyboard without opening secondary panels first.

**Acceptance Scenarios**:

1. **Given** a first-time player on the main menu, **When** the screen appears, **Then** the game title, a short tagline, and the primary actions are visible at supported window sizes without overlapping text or controls.
2. **Given** a returning player with at least one save, **When** they open the main menu, **Then** a prominent Continue or Play action reflects their most recent or selected save without forcing them through unrelated screens first.
3. **Given** a player on the main menu, **When** they use keyboard navigation (arrow keys or Tab plus Enter), **Then** they can reach every primary action and activate it without the mouse.
4. **Given** a player who chooses Quit, **When** they confirm or activate exit, **Then** the application closes predictably; Escape on the root menu MUST NOT feel like a hidden quit shortcut without visual affordance.

---

### User Story 2 - Manage Saves and Start Worlds with Confidence (Priority: P2)

As a player choosing or creating a world, I want save selection, world creation, and special modes (such as structure gallery) to be organized, informative, and forgiving — so that loading, renaming, deleting, and starting worlds feels safe and clear.

**Why this priority**: Save and world setup are the core job of the pre-game menu. Clarity here prevents lost progress, accidental deletes, and confusion about world types or seeds.

**Independent Test**: From the main menu, browse saves, inspect details for a selected slot, create a new world with a chosen type and seed, cancel back to the menu, and load an existing save — verifying feedback at each step and recovery from a failed load.

**Acceptance Scenarios**:

1. **Given** multiple save slots, **When** the player selects a slot, **Then** they see meaningful summary information (name, last played, play time or progress cues, thumbnail or world type if available) before committing to Load.
2. **Given** a player initiating New World, **When** they configure world type and seed, **Then** choices are explained in plain language and invalid seeds are rejected with a helpful message rather than silent failure.
3. **Given** a player attempting to delete a save, **When** they confirm deletion, **Then** a distinct confirmation step prevents accidental loss and success or failure is acknowledged.
4. **Given** a load failure, **When** the player returns to the menu, **Then** an inline error explains what went wrong and suggests a next step (retry, pick another slot, or create new).
5. **Given** a player choosing structure gallery or other documented special entry, **When** they activate it from the menu flow, **Then** it remains reachable and labeled consistently with existing CLI and agent expectations.

---

### User Story 3 - Adjust Settings and View Stats Without Getting Lost (Priority: P3)

As a player, I want settings, player statistics, and auxiliary menu panels to be easy to open, use, and dismiss — with consistent layout and navigation — so that pre-game configuration never traps me or hides how to get back.

**Why this priority**: Settings and stats currently live behind nested overlays; a revamp should make them discoverable from the main menu while preserving the full settings surface (graphics, audio, AI provider options).

**Independent Test**: Open Settings from the main menu, change render distance and a volume slider, save, return to the menu, reopen Settings and confirm values persisted; open player stats from the menu flow and return without losing menu context.

**Acceptance Scenarios**:

1. **Given** a player on the main menu, **When** they open Settings, **Then** graphics, audio, and AI-related options remain available and grouped under clear section headers with Save and Back actions.
2. **Given** a player editing Settings, **When** they press Escape or Back without saving, **Then** unsaved changes are discarded and they return to the prior menu screen.
3. **Given** a player opening player statistics from the menu, **When** they finish reviewing, **Then** they can close or go back to the same menu state they came from.
4. **Given** supported window sizes from small to large, **When** Settings or stats panels are open, **Then** content remains readable, scrollable if needed, and primary actions stay reachable.

---

### User Story 4 - Experience Cohesive Visual and Motion Design (Priority: P4)

As a player moving through menu screens, I want transitions, backdrop, typography, and button styling to feel like one cohesive product — so that the menu feels modern and intentional rather than a collection of separate panels.

**Why this priority**: Visual cohesion supports the “revamp” goal; consistent motion and hierarchy reduce cognitive load when moving between save list, new world setup, settings, and loading.

**Independent Test**: Step through main menu → save browse → new world → back → settings → back → load world (or loading screen), observing that shared visual language (backdrop, cards, buttons, hints, fade timing) stays consistent and does not introduce jarring layout shifts.

**Acceptance Scenarios**:

1. **Given** any pre-game menu screen, **When** it is displayed, **Then** it uses the same design tokens for title hierarchy, button styles, hints, and panel chrome as other menu screens in this flow.
2. **Given** a transition between menu screens, **When** navigation occurs, **Then** motion is brief, non-disorienting, and does not block input longer than necessary.
3. **Given** hover or focus on interactive elements, **When** the player moves between items, **Then** feedback is immediate and accessible (visible focus state for keyboard users).

---

### Edge Cases

- Empty save list: first-time players see guided copy toward New World rather than a blank or confusing panel.
- Maximum save slots: list remains scrollable or paginated; selection and actions stay usable.
- Very small window (minimum supported resolution): no clipped primary actions; critical controls remain reachable via scroll or stacked layout.
- Very large window: content stays centered or proportionally laid out without excessive empty dead zones that push actions off-screen.
- Rapid double-clicks or repeated Enter: no duplicate world loads or duplicate quit requests.
- `--skip-menu` and agent/CI launch paths: bypass behavior unchanged; revamped menu does not break headless or automation entry points.
- Failed or timed-out world load: player returns to menu with retained slot list and visible error state.
- Long save names or rename input: truncation or wrapping preserves readability without breaking layout.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pre-game main menu MUST present a clear top-level hierarchy with labeled primary actions for playing (continue/load), creating a new world, opening settings, and exiting the application.
- **FR-002**: Save slot management MUST remain fully functional: list slots, select, load, rename, delete with confirmation, and display per-slot summary information before load.
- **FR-003**: New world setup MUST remain accessible from the menu flow with world type selection, seed entry, validation, and a clear path back without losing menu context.
- **FR-004**: Settings opened from the menu MUST expose existing configuration categories (graphics, audio, village AI provider options) with save and cancel/back behavior that matches player expectations.
- **FR-005**: Player statistics and structure gallery entry points MUST remain reachable from the pre-game menu flow with labels consistent with current product behavior.
- **FR-006**: All primary menu actions MUST be operable via keyboard as well as mouse, with visible focus indication for the active item.
- **FR-007**: Menu screens MUST remain readable and usable at supported window sizes without overlapping controls or illegible text.
- **FR-008**: Loading and error states MUST communicate progress or failure in plain language and return the player to an appropriate menu screen when complete or on failure.
- **FR-009**: Visual and interaction patterns (backdrop, panels, buttons, hints, transitions) MUST be consistent across all screens in the pre-game menu flow.
- **FR-010**: Documented player controls, `--skip-menu` behavior, save/load semantics, settings persistence, and agent/structure-gallery entry expectations MUST remain unchanged unless a breaking change is explicitly approved.
- **FR-011**: The feature MUST define verification for menu navigation, save operations, settings round-trip, new world creation, load failure recovery, keyboard accessibility, and layout at supported resolutions.

### Key Entities *(include if feature involves data)*

- **Main Menu Screen**: Top-level pre-game entry presenting primary navigation and brand presentation.
- **Save Slot**: A persisted world slot with identifier, display name, metadata (last played, stats), and load/delete/rename affordances.
- **New World Setup**: Configuration step for world type, seed, and creation confirmation before loading.
- **Menu Settings Panel**: Pre-game overlay for adjusting persisted game settings.
- **Menu Navigation State**: Which screen or overlay is active and how back/forward transitions preserve context.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a structured review of the pre-game flow, 100% of primary actions (play/continue, new world, settings, quit) are reachable within two interactions from the root menu screen.
- **SC-002**: In keyboard-only walkthroughs, 100% of primary menu actions complete successfully without mouse input.
- **SC-003**: At minimum and maximum supported window sizes used in manual review, 100% of menu screens pass a layout check with no overlapping primary controls or unreadable title text.
- **SC-004**: Save load, new world create, settings save, rename, and delete-with-confirmation flows each complete successfully in acceptance testing with zero unintended data loss in a 10-trial sample.
- **SC-005**: At least 80% of informal reviewers (or a single structured self-review checklist) rate the revamped menu as clearer or more polished than the prior save-slot-first entry experience.
- **SC-006**: Existing menu-related integration tests and `--skip-menu` / structure gallery / agent launch paths continue to pass without regression.
- **SC-007**: Transitions between menu screens complete within a perceived instant window (under 1 second of blocking animation) for 100% of standard navigation steps in manual testing.

## Assumptions

- “Menu menu UI” refers to the pre-game main menu experience (entry, save selection, new world setup, settings, and related overlays), not a full redesign of in-game HUD, pause menu, village UI, or inventory screens in this feature — though visual tokens may align where practical.
- The existing save slot, settings, and new world capabilities are preserved; this feature improves presentation, navigation, and cohesion rather than removing established functionality.
- Continue/Play behavior may surface the most recently played save or the currently selected slot, following common game menu conventions.
- Structure gallery and `--skip-menu` remain supported for agents, CI, and power users.
- Player statistics and AI provider settings remain optional panels reachable from the menu, not mandatory steps before play.
- Minimum supported resolution matches current game window defaults documented in project guidance unless planning discovers a stricter bound.
