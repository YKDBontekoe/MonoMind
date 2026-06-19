# Feature Specification: Premium Survival Start & Recipe Book

**Feature Branch**: `003-premium-survival-start`

**Created**: 2026-06-19

**Status**: Draft

**Input**: User description: "I want to improve the starting survival to make it more premium feel, fun, more like minecraft (start with no loot), improve the recipe book (always show recipes etc"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start From Nothing (Priority: P1)

As a new survival player, I want to begin with empty hands and no free items — like classic Minecraft — so that gathering my first resources and crafting my first tools feels earned, tense, and satisfying.

**Why this priority**: The current experience gives the player grass, logs, dirt, and wood tools before they take a single action. That undercuts the survival fantasy and makes the first minutes feel like a tutorial with training wheels rather than a premium opening act.

**Independent Test**: Start a new survival game, open inventory immediately, and verify the hotbar and main inventory are empty (except any explicitly documented cosmetic or UI-only items). Break a tree block with bare hands, pick up the drop, and confirm the player can progress toward planks and sticks without relying on pre-placed starter loot.

**Acceptance Scenarios**:

1. **Given** a new survival world, **When** the player spawns for the first time, **Then** their hotbar and inventory contain no blocks, tools, food, or materials.
2. **Given** a player with empty inventory, **When** they break a harvestable block (such as a log or leaves) using bare hands, **Then** the block drops loot the player can pick up and the action provides clear feedback (animation, sound, or HUD cue).
3. **Given** a player who has gathered their first log, **When** they open inventory and use the player crafting grid, **Then** they can craft planks and sticks following visible recipes without needing pre-unlocked items or village-supplied materials.
4. **Given** an existing saved world loaded from before this change, **When** the player continues playing, **Then** their saved inventory and village storage are preserved unchanged.

---

### User Story 2 - Premium First-Survival Loop (Priority: P2)

As a player in the opening minutes, I want a clear, rewarding progression from vulnerability to self-sufficiency — gather, craft, eat, shelter — so that early survival feels polished, fun, and intentional rather than confusing or hand-holdy.

**Why this priority**: Removing starter loot only works if the first loop is enjoyable. A premium feel comes from readable goals, satisfying feedback, and a natural difficulty curve through the first day and night.

**Independent Test**: Play a fresh survival session for 15 minutes without external guides. Verify the player can reach first tools, a food source, and basic awareness of night danger through in-game guidance and UI alone.

**Acceptance Scenarios**:

1. **Given** a new player with empty inventory near the starter settlement, **When** they spawn, **Then** they receive a short, non-intrusive welcome that orients them to the first three actions (gather wood, craft basic items, secure food before night) without giving free items.
2. **Given** a player progressing through early survival, **When** they complete milestones (first log, first plank, first tool, first cooked food), **Then** the game acknowledges progress with brief feedback (toast, HUD hint update, or journal entry) that feels rewarding rather than repetitive.
3. **Given** hunger is draining and night is approaching, **When** the player checks HUD guidance, **Then** they see prioritized, plain-language next steps (for example, "Punch trees for wood" before "Assign lumber workers") aligned with their current inventory state.
4. **Given** the player dies during early survival, **When** they respawn, **Then** consequences remain meaningful but recoverable — they do not receive a restock of starter loot, and guidance helps them rebuild.

---

### User Story 3 - Always-Visible Recipe Book (Priority: P1)

As a player at a crafting station, I want the recipe book to always list every recipe for that station with clear names and ingredients — even when I cannot craft them yet — so that I always know what to work toward, like Minecraft's recipe book.

**Why this priority**: Hidden recipes ("???") and empty recipe lists at game start make crafting feel opaque. Showing all recipes turns the book into a progression map and reduces friction for new and returning players.

**Independent Test**: Open inventory at a bench on a fresh world with empty inventory. Press the recipe book toggle and verify all bench recipes appear by name with ingredient summaries; confirm craftable recipes are visually distinct from those missing materials.

**Acceptance Scenarios**:

1. **Given** a player at a bench with an empty inventory, **When** they open the recipe book, **Then** all bench recipes are listed by name — none are hidden as "???" or omitted from the list.
2. **Given** a recipe the player cannot currently craft, **When** it appears in the recipe book, **Then** its name and required ingredients (or pattern summary) are visible, and it is visually de-emphasized compared to craftable recipes.
3. **Given** a recipe the player can craft with current inventory, **When** it appears in the recipe book, **Then** it is clearly highlighted as ready to craft and clicking it fills the crafting grid when materials allow.
4. **Given** a player at a forge or other station, **When** they open the recipe book, **Then** they see all recipes for that station type, filtered only by station — not by discovery unlock state.
5. **Given** a player who selects a non-craftable recipe, **When** they click it, **Then** the system either fills partial ingredients where possible or shows a brief explanation of what is missing — without hiding the recipe itself.

---

### User Story 4 - Recipe Book as Progression Companion (Priority: P3)

As a player advancing through tiers (wood → stone → metal), I want the recipe book to help me understand what I am building toward — including ingredient previews and craftability at a glance — so that crafting feels like discovery and planning, not guesswork.

**Why this priority**: "Always show recipes" is more than removing locks; a premium recipe book helps players plan their next gather trip and feel smart about progression.

**Independent Test**: With partial inventory (logs but no stone), open the recipe book and verify stone-tier recipes show ingredient requirements; gather stone and confirm craftable status updates immediately without reopening the screen.

**Acceptance Scenarios**:

1. **Given** a selected or hovered recipe, **When** the player views it in the recipe book, **Then** they see a readable summary of inputs (items and counts, or shaped pattern) and the output item.
2. **Given** inventory changes while the recipe book is open, **When** the player acquires or spends materials, **Then** craftable indicators update without requiring the player to close and reopen the book.
3. **Given** multiple recipe categories at a station (tools, blocks, food), **When** the list grows long, **Then** the player can scroll or browse comfortably with readable rows at supported resolutions.

---

### Edge Cases

- What happens when a player starts in Creative mode? Creative players retain immediate access to all items via creative conventions; empty survival start applies only to survival/default play.
- What happens when loading an old save that already has starter loot in inventory? Existing inventories are preserved; empty start applies only to newly created survival worlds.
- What happens when the recipe book is opened at a player inventory grid (2×2) versus a bench (3×3)? Recipes requiring a larger grid appear but are marked as requiring a bench; they cannot be auto-filled on the smaller grid.
- How does the starter settlement interact with "no loot"? The settlement may still exist as a social and economic hub, but the player does not receive free tools or blocks from personal inventory or a "welcome crate" equivalent at spawn.
- What happens when every recipe at a station is non-craftable? The recipe book still lists all recipes with ingredient hints — it never shows an empty "No recipes yet" state for standard stations with registered recipes.
- How are food and forge recipes handled? Cooking and smelting recipes appear in the book for their station with heat or environment requirements clearly indicated when not met.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST spawn new survival players with an empty hotbar and empty main inventory — no blocks, tools, food, or materials pre-granted.
- **FR-002**: System MUST allow bare-hand harvesting of appropriate early-game blocks (logs, leaves, dirt, sand, etc.) so progression is possible without starter tools.
- **FR-003**: System MUST preserve existing saved player inventories and village storage when loading worlds created before this feature.
- **FR-004**: System MUST NOT grant the player free items at spawn, on first open of inventory, or as part of the new-game welcome flow in survival mode.
- **FR-005**: System MUST update early-game guidance so first hints reflect empty-inventory progression (gather → craft → food → settlement) rather than assuming pre-equipped tools or hotbar materials.
- **FR-006**: System MUST display all recipes registered for the current crafting station in the recipe book, regardless of discovery unlock state.
- **FR-007**: System MUST show each recipe's display name and ingredient summary in the recipe book — locked or hidden "???" entries are not permitted for standard recipes.
- **FR-008**: System MUST visually distinguish craftable recipes (materials available, grid size sufficient) from non-craftable recipes (missing materials, wrong station, or unmet environment requirements).
- **FR-009**: System MUST allow clicking a craftable recipe to auto-fill the crafting grid when the player has sufficient materials.
- **FR-010**: System MUST update recipe craftability indicators when inventory contents change while the recipe book is open.
- **FR-011**: System MUST filter recipe book contents by current station type and maximum grid size available — not by discovery journal unlock state.
- **FR-012**: System MUST retain the recipe book toggle control (B) and its integration with inventory and bench screens unless an approved breaking change replaces it with an equally discoverable control.
- **FR-013**: System MUST provide brief, non-spammy milestone feedback for early survival progress (first resource, first craft, first tool, first food).
- **FR-014**: System MUST preserve save/load round-trip for player inventory, crafting state, and any remaining discovery or journal data after recipe visibility changes.
- **FR-015**: System MUST preserve documented player controls, HUD/UI behavior, agent API semantics, and hotbar slot conventions unless a breaking change is explicitly required.
- **FR-016**: System MUST define verification requirements for the affected domain, including integration testing for gameplay, inventory, crafting, early guidance, and UI changes.

### Key Entities

- **Survival Start Profile**: Defines what a new survival player receives at spawn — empty inventory, spawn position, and whether any non-item bootstrap (settlement anchor, citizens) occurs.
- **Player Inventory**: Hotbar and main slots holding blocks, tools, materials, and food; must start empty in new survival games.
- **Craft Recipe**: A station-bound crafting definition with display name, inputs or pattern, output, grid size, and optional environment requirements; always visible in the recipe book for its station.
- **Recipe Book View**: The UI listing of recipes for the active station, with craftability state, ingredient preview, scroll support, and click-to-fill behavior.
- **Early Survival Guidance**: Dynamic hints and milestone toasts that adapt to inventory state and time of day, guiding the player through the first session without granting items.
- **Discovery Journal**: Optional progress tracker; may continue recording unlocks for achievements or guidance but MUST NOT gate recipe visibility in the recipe book.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In playtests, 100% of new survival sessions begin with zero items in player hotbar and inventory (verified by automated new-game scenario).
- **SC-002**: At least 90% of playtest participants reach their first crafted tool within 10 minutes of a fresh survival start without using pre-placed starter items.
- **SC-003**: Opening the recipe book at a bench on a fresh world shows 100% of registered bench recipes by name — zero hidden or "???" entries for those recipes.
- **SC-004**: Players can identify missing ingredients for a non-craftable recipe from the recipe book alone in under 10 seconds (median across three recipe checks in moderated testing).
- **SC-005**: At least 75% of playtest participants rate the first 15 minutes as "fun" or "engaging" on a brief post-session survey after this change (compared to baseline starter-loot experience).
- **SC-006**: Recipe book UI remains readable and fully scrollable at 1280×720 with no clipped recipe rows or overlapping ingredient text.
- **SC-007**: Existing automated inventory, crafting, save/load, and integration test scenarios continue to pass, with new scenarios added for empty-start and full-recipe-book visibility.

## Assumptions

- "Start with no loot" applies to the **player's personal inventory** on new survival worlds. The starter settlement (Town Heart, citizens, village food stock) may remain as an existing gameplay pillar, but the player does not receive duplicate free tools or building materials in their own inventory or a personal starter chest.
- Village founding storage that currently includes free planks, cobblestone, tools, and cooked meat is out of scope for player "no loot" unless playtesting shows it undermines the survival fantasy — initial implementation focuses on empty player inventory; village bootstrap tuning can follow in the same feature if testing confirms it is needed.
- Creative mode is unchanged: players who choose or enable creative play are not subject to empty-start rules.
- Recipe discovery unlocks may remain in the journal for achievements, statistics, or optional guidance toasts, but they no longer hide or anonymize recipes in the recipe book.
- Minecraft-like recipe book behavior is the reference: all recipes visible, craftability indicated by highlighting, ingredient information always readable.
- Early game guide stages will be rewritten to match empty-start flow; settlement features (Town Board, rations, villager jobs) remain available but are introduced after basic self-sufficiency steps.
- Existing saves are not retroactively stripped of items; only newly created survival worlds use the empty start profile.
