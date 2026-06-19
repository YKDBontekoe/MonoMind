# Feature Specification: Improved Villager Flow

**Feature Branch**: `002-improve-villager-flow`

**Created**: 2026-06-19

**Status**: Draft

**Input**: User description: "Currently the villager flow is clunky and minimal. We want to improve it to make it clear, good ui, really make the villager system a core part of the system"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Understand Settlement Status at a Glance (Priority: P1)

As a player managing a settlement, I want to open one clear management view and immediately see what matters — population, food, active work, and the single most important next step — so that I never feel lost about what villagers need from me.

**Why this priority**: The core complaint is that the villager flow feels clunky and minimal. Without a clear status overview, every other improvement (jobs, recruiting, dialogue) stays buried behind trial-and-error.

**Independent Test**: Start or load a settlement with at least one villager, open the settlement management view, and verify that population health, food security, idle workers, and the recommended next action are visible without switching tabs or reading external documentation.

**Acceptance Scenarios**:

1. **Given** a settlement with idle villagers, **When** the player opens the settlement management view, **Then** idle workers are highlighted with a plain-language explanation of what to do next.
2. **Given** a settlement with low food, **When** the player opens the settlement management view, **Then** food risk is shown prominently with a suggested corrective action (for example, assign farming or build food production).
3. **Given** a new player with a starter settlement, **When** they open the settlement management view for the first time, **Then** a short guided path explains the first three actions to take without requiring prior knowledge of tab names or hotkeys.

---

### User Story 2 - Assign and Track Villager Work Easily (Priority: P2)

As a player, I want to assign jobs, see what each villager is doing, and confirm that work is progressing — so that managing labor feels intentional rather than opaque button-clicking.

**Why this priority**: Villagers are meant to be a core gameplay loop. Job assignment is the main lever players pull; it must be fast, readable, and rewarding to monitor.

**Independent Test**: Select a villager, assign a job, close the UI, observe the villager in the world, and confirm both the management view and in-world feedback reflect the assignment and current activity within a few seconds.

**Acceptance Scenarios**:

1. **Given** a selected villager on the People view, **When** the player assigns a job, **Then** the assignment succeeds with immediate confirmation and the villager's displayed status updates to reflect the new task.
2. **Given** a villager performing a job, **When** the player views that villager's detail, **Then** the player sees what they are doing in plain language (for example, "Chopping marked oak at (x, z)" or "Building Peasant House — 40% complete") rather than only a generic job label.
3. **Given** a construction or production task is blocked (missing materials, no work zone, at housing cap), **When** the player attempts to assign or queue work, **Then** the system explains why it is blocked and what to do instead.
4. **Given** a player near a villager in the world, **When** they use a direct interaction affordance, **Then** they can open that villager's detail or assign a common job without navigating unrelated menus first.

---

### User Story 3 - Grow and Care for the Settlement Over Time (Priority: P3)

As a player invested in my settlement, I want recruiting, housing, rations, and villager well-being to feel connected — so that growing the village is a deliberate progression rather than a scattered set of hidden rules.

**Why this priority**: Making villagers a "core part of the system" requires the growth loop (recruit → house → feed → specialize) to be understandable and satisfying, not just functional.

**Independent Test**: Play from starter settlement through recruiting a second villager and assigning specialized roles; verify each gate (food, housing, materials) is communicated before failure and that success feels acknowledged.

**Acceptance Scenarios**:

1. **Given** the player can recruit a new villager, **When** they view recruit options, **Then** requirements (housing space, food buffer, material costs) are shown before they commit.
2. **Given** recruitment or growth is blocked, **When** the player attempts the action, **Then** they receive a specific reason and a link to the relevant management area (for example, queue housing on Build).
3. **Given** villagers are hungry or idle for extended periods, **When** the player checks the settlement view, **Then** well-being warnings appear with prioritized remediation steps.
4. **Given** the player takes rations or manages shared food stock, **When** food levels change, **Then** the settlement view and guidance hints stay in sync so the player understands the impact on villagers.

---

### User Story 4 - Relate to Villagers as Characters (Priority: P4)

As a player, I want villagers to feel like distinct members of my settlement — with recognizable identity, activity, and optional conversation — so that the village feels alive and worth investing in beyond pure efficiency.

**Why this priority**: Character and clarity reinforce each other; villagers become "core" when players remember names, roles, and stories — not only job icons.

**Independent Test**: Identify two villagers by name and role from the management view, observe differentiated status summaries, and optionally initiate dialogue from the same flow where AI assistance is enabled.

**Acceptance Scenarios**:

1. **Given** multiple villagers in a settlement, **When** the player opens the People view, **Then** each villager is distinguishable by name, role, current activity, and a brief status summary.
2. **Given** AI-assisted play is enabled, **When** the player chooses to talk to a villager or the settlement steward, **Then** conversation is reachable from the villager management flow without breaking context (the player still knows who they are speaking with and what village state matters).
3. **Given** AI-assisted play is disabled, **When** the player interacts with villagers, **Then** non-AI management features remain fully usable with no dead-end prompts.

---

### Edge Cases

- What happens when all villagers are dead, despawned, or out of sync with the settlement registry? The UI must show a recovery path (for example, summon settlers) rather than an empty or misleading state.
- How does the system behave when the player is far from the Town Heart? Distance-aware guidance should still orient the player without opening a management view that cannot act on distant settlements.
- What happens when housing is full, food is zero, and multiple construction sites are queued? Warnings must be prioritized so the player sees the most urgent issue first.
- How does the flow work during founding (no Town Heart yet) versus established settlement? Founding and management modes must feel like one continuous journey with consistent language and controls.
- What happens when the player rapidly assigns and reassigns jobs? The UI must not show stale activity text; the latest assignment always wins with clear feedback.
- How are simultaneous settlement events surfaced (recruit success, build complete, food crisis)? Players should receive concise notifications that do not overwhelm or duplicate Town Board content.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a single settlement management entry point that surfaces population, food stock, housing capacity, idle worker count, and active construction or production at a glance.
- **FR-002**: System MUST display a prioritized, plain-language "next best action" recommendation that updates based on settlement state (for example, assign idle workers, address low food, queue housing, recruit).
- **FR-003**: System MUST allow players to view a list of all settlement citizens with name, role, current job, activity summary, and well-being indicators where applicable.
- **FR-004**: System MUST allow players to assign and change villager jobs from the citizen detail view with immediate visual confirmation of success or failure.
- **FR-005**: System MUST explain blocked actions with specific reasons and suggested follow-up steps (missing materials, at housing cap, no work zone, no marked resources).
- **FR-006**: System MUST show per-villager activity in human-readable form that reflects what the villager is doing in the world, not only enum labels.
- **FR-007**: System MUST expose recruit requirements and outcomes before recruitment is committed, including housing cap and material costs.
- **FR-008**: System MUST keep founding, starter settlement, and established settlement flows visually and linguistically consistent so players learn one mental model.
- **FR-009**: System MUST provide contextual in-world affordances to open settlement management or a specific villager's detail when the player is near relevant entities.
- **FR-010**: System MUST synchronize guidance shown in the heads-up display, settlement management view, and toast or event notifications so they do not contradict each other.
- **FR-011**: System MUST support optional villager or steward conversation from the villager management flow when AI-assisted play is enabled, with clear speaker identity and without blocking non-AI players.
- **FR-012**: System MUST preserve existing player controls and hotbar conventions unless a breaking change is explicitly approved; any changed shortcuts must be discoverable in-game.
- **FR-013**: System MUST preserve save/load round-trip for villager assignments, population, food stock, housing, and settlement metadata after UI improvements.
- **FR-014**: System MUST remain usable at supported window resolutions without overlapping text, clipped controls, or unreadable citizen lists.

### Key Entities

- **Settlement (Village)**: The player's claimed community; attributes include name, anchor location, population cap, food stock, storage, building queue, work zones, and guidance state.
- **Citizen (Villager)**: An individual worker; attributes include name, role, assigned job, activity phase, well-being, equipped tools, inventory, home assignment, and optional persona for dialogue.
- **Job Assignment**: The link between a citizen and work type (idle, lumber, mine, farm, build, haul); includes target location or site when applicable.
- **Settlement Guidance**: Dynamic recommendations and tutorial steps derived from settlement health (population, food, idle workers, construction backlog).
- **Recruitment Offer**: The conditions and cost to add a citizen, tied to housing cap and shared storage materials.
- **Settlement Notification**: Short-lived player-facing messages for recruit success, build completion, crises, and milestone progress.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In moderated playtests, at least 80% of new players correctly identify their next settlement action within 30 seconds of opening the management view, without external documentation.
- **SC-002**: Players can assign a job to a selected villager in under 15 seconds from opening the settlement management view (median across three attempts).
- **SC-003**: At least 90% of blocked job or recruit attempts display a reason and suggested remediation visible on the same screen (verified via structured test scenarios).
- **SC-004**: After a 20-minute starter settlement session, at least 70% of playtest participants report that villagers feel like a "main part of gameplay" on a brief post-session survey (agree or strongly agree).
- **SC-005**: Settlement management UI remains readable and fully operable at 1280×720 with no clipped primary controls or citizen list rows (verified by layout checklist).
- **SC-006**: Existing automated settlement and villager regression scenarios continue to pass after the UX improvements, confirming no behavioral regressions in recruitment, assignment, food, or save round-trip.

## Assumptions

- The feature improves clarity, layout, guidance, and interaction flow around the existing settlement simulation (jobs, hauling, building, food, recruitment) rather than replacing core simulation rules.
- "Good UI" means plain language, prioritized information hierarchy, consistent tabs or sections, and discoverable actions — not a full visual redesign of unrelated game systems.
- Villager dialogue remains optional and gated by the player's AI-assisted play setting; non-AI players receive full value from management and activity improvements alone.
- In-world villager interaction affordances complement — but do not fully replace — the Town Board / settlement management hotkey flow.
- Starter settlement onboarding builds on existing early-game guidance stages rather than introducing a separate tutorial mode.
- Agent and automation users benefit from the same clearer settlement state in the HTTP game state payload; detailed agent API expansion is in scope only where needed to reflect improved player-visible status fields.
- Performance impact is limited to UI refresh and guidance computation; no unbounded per-frame scans of the entire world are introduced.
