# Feature Specification: Villager Onboarding

**Feature Branch**: `005-villager-onboarding`

**Created**: 2026-06-23

**Status**: Draft

**Input**: User description: "I want to improve the whole starting flow of the villagers, currently the UI is buggy, the ui is ugly and i cannot spawn any villgaers, settelrs or do anything with it make it way more robust"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start the Village Without Guessing (Priority: P1)

As a player beginning a settlement, I want one clear opening flow that explains how to get my first villagers or settlers into the village, so that I can start the village without trial-and-error.

**Why this priority**: The starting flow is the first point where the villager system either feels usable or broken. If players cannot get past the opening steps, the rest of the settlement game never becomes meaningful.

**Independent Test**: Start a new world or fresh settlement, open the villager onboarding flow, and verify that the player can understand the required first step, see the current state, and complete the first villager-related action without external guidance.

**Acceptance Scenarios**:

1. **Given** a new or empty settlement, **When** the player opens the villager flow, **Then** the game shows a clear starting state and the next required action in plain language.
2. **Given** the player has no villagers or settlers yet, **When** they attempt to begin the settlement flow, **Then** the game explains how to proceed instead of showing an empty or broken screen.
3. **Given** the player has completed the first villager setup step, **When** the flow updates, **Then** the game confirms progress and shows the next actionable step.

---

### User Story 2 - Add Villagers Reliably (Priority: P1)

As a player, I want a dependable way to create, recruit, or add the first villagers or settlers when the game allows it, so that I can actually grow the settlement instead of getting stuck on a missing action.

**Why this priority**: The user explicitly cannot spawn or add villagers today, which blocks the core loop. This is a functional blocker, not just a polish issue.

**Independent Test**: Use the intended village-starting flow and verify that the player can complete a villager-add action when requirements are met, and receives a clear reason when requirements are not met.

**Acceptance Scenarios**:

1. **Given** the settlement meets the requirements for adding a villager or settler, **When** the player uses the add/recruit action, **Then** the action succeeds and the new villager appears in the settlement state.
2. **Given** the settlement does not meet the requirements, **When** the player tries to add a villager, **Then** the game explains the blocking reason and what the player should do next.
3. **Given** the player repeats the action or opens the flow again, **When** the settlement state has changed, **Then** the available actions and counts update to match the current state.

---

### User Story 3 - Make the Villager UI Clear and Usable (Priority: P2)

As a player managing the early village, I want the villager UI to be readable, attractive, and consistent, so that it feels like a finished part of the game rather than a buggy placeholder.

**Why this priority**: Even if the underlying flow works, a confusing or ugly UI prevents players from trusting it and makes every interaction feel fragile.

**Independent Test**: Open the villager UI at supported window sizes and verify that labels, buttons, lists, and status information are readable, visually consistent, and usable without clipping or overlap.

**Acceptance Scenarios**:

1. **Given** the villager UI is open, **When** the player scans the screen, **Then** the main actions and current settlement state are easy to identify at a glance.
2. **Given** the player uses a supported window size, **When** the UI is displayed, **Then** text and controls remain readable and do not overlap or disappear off-screen.
3. **Given** the player switches between open villager states, **When** the UI refreshes, **Then** it keeps a stable layout and does not show stale, duplicated, or contradictory information.

---

### User Story 4 - Recover From Failed or Partial Starting States (Priority: P2)

As a player, I want the villager start flow to recover cleanly from partial, failed, or unusual settlement states, so that I never feel trapped by a broken setup.

**Why this priority**: Early settlement flows often fail because of missing resources, missing settlement state, or repeated actions. Robust recovery is what makes the system feel dependable.

**Independent Test**: Put the settlement into a failing or partial state, reopen the villager flow, and verify that the UI gives a sensible recovery path instead of freezing, hiding actions, or showing incorrect counts.

**Acceptance Scenarios**:

1. **Given** the settlement has no valid recruit path available, **When** the player opens the villager flow, **Then** the game shows a recovery path or prerequisite checklist rather than a dead end.
2. **Given** the player leaves and returns to the same world, **When** they reopen the villager flow, **Then** the latest settlement state is restored and shown consistently.
3. **Given** an action fails partway through, **When** the player retries, **Then** the flow remains responsive and does not require restarting the game to continue.

### Edge Cases

- The flow must handle a settlement with zero villagers, one villager, or a partially created starter group without showing broken counts or empty placeholders that look like errors.
- The flow must handle missing housing, missing food, or other blocked prerequisites with a direct explanation and a visible next step.
- The flow must not trap the player in an unrecoverable screen if they close the menu, move away, or reload the world during the starter process.
- The flow must remain legible and functional at supported window sizes and must not rely on a specific aspect ratio to be usable.
- The flow must avoid duplicate recruitment or spawning when the player clicks the same action repeatedly.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The game MUST provide a clear villager onboarding flow that explains the current settlement state, the next intended action, and the progress of the starter village setup.
- **FR-002**: The flow MUST allow the player to add, recruit, or otherwise obtain the first villagers or settlers through the intended gameplay path when requirements are met.
- **FR-003**: The flow MUST explain why a villager-add action is blocked when requirements are not met and MUST show what the player needs to do next.
- **FR-004**: The flow MUST keep villager and settlement counts accurate after each successful, failed, or repeated action.
- **FR-005**: The villager UI MUST present primary actions, current state, and important warnings in a readable hierarchy that is easy to scan at a glance.
- **FR-006**: The villager UI MUST remain usable at supported window sizes without overlapping text, clipped controls, or hidden primary actions.
- **FR-007**: The villager UI MUST use consistent labels and terminology for settlers, villagers, recruitment, spawning, and starter settlement states.
- **FR-008**: The flow MUST preserve the latest settlement state when the player closes the UI, reopens it, changes worlds, or reloads a save.
- **FR-009**: The flow MUST provide a recovery path for partial or failed starter states so that players can continue without restarting the game.
- **FR-010**: The flow MUST avoid stale, duplicated, or contradictory information when the player changes state quickly or repeats actions.
- **FR-011**: The feature MUST preserve existing player controls and other village-management workflows unless a change is explicitly required to make the starter flow usable.
- **FR-012**: The feature MUST define and keep verification coverage for the starter villager flow, blocked-action handling, state recovery, and UI layout consistency.

### Key Entities *(include if feature involves data)*

- **Villager Onboarding Flow**: The opening sequence or management view that helps the player obtain and manage the first villagers or settlers.
- **Starter Settlement State**: The current village condition at the beginning of play, including whether villagers exist, what is blocked, and what actions are available.
- **Villager Add Action**: The player-facing action that creates, recruits, or introduces a villager into the settlement.
- **Blocked Action State**: A situation where the player cannot proceed yet and must be told why and what to do next.
- **UI Status Summary**: The short set of visible indicators that show settlement counts, warnings, and the next step.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a guided test of new players, at least 90% can correctly identify how to start the villager flow within 30 seconds of opening it.
- **SC-002**: At least 95% of valid villager-add attempts in test scenarios complete successfully without requiring a restart or workaround.
- **SC-003**: At least 95% of blocked villager-add attempts present a clear reason and next step on the same screen.
- **SC-004**: In layout checks across supported window sizes, 100% of tested screens keep the primary actions readable and free of overlapping controls.
- **SC-005**: In save/load and reopen scenarios, the settlement state remains consistent in 100% of tested cases, with no duplicated villagers or incorrect starter counts.
- **SC-006**: Players in a short usability review rate the villager onboarding flow as clearer and more trustworthy than the current version in at least 80% of responses.

## Assumptions

- The feature focuses on the first-villager and starter-settlement experience, not a full redesign of all mid-game or late-game village management.
- "Spawn", "recruit", and "add" refer to the player-visible ways the game introduces the first villagers or settlers during the intended village flow.
- Existing settlement simulation rules remain in place; this work improves discoverability, reliability, recovery, and presentation.
- The user-facing flow should remain usable for both mouse and keyboard players and should not require hidden commands to make the starter path work.
- AI-assisted dialogue, if present, is optional and should not be required to complete the core starting flow.
