# Research: Villager Onboarding

## Decision: Keep the Existing Town Board and Founding Flow

**Rationale**: The repository already has a working panel architecture for settlement management, and the starter settlement flow can be made robust by improving the existing onboarding path instead of building a parallel UI. This keeps controls learnable and avoids splitting the early-game mental model.

**Alternatives considered**: Separate villager wizard, modal recruit screen, or new dedicated onboarding scene. These options would add more transitions and create a second mental model for the same settlement actions.

## Decision: Reuse `SettlementGuidance` and Existing Recruit Results

**Rationale**: The codebase already exposes `SettlementGuidance.Compute` and a structured `RecruitResult` from `VillageManager.TryRecruit`. Those are the right sources of truth for starter flow copy, blocked reasons, and next-step recommendations. The feature should refine these outputs rather than invent duplicate logic in the UI.

**Alternatives considered**: UI heuristics or hard-coded strings in panels. Rejected because they would drift from simulation truth and make blocked states harder to test.

## Decision: Make Recovery and State Refresh First-Class

**Rationale**: The user complaint is not only visual; it is also that the flow fails to let them do anything. The plan therefore treats empty, partial, or failed starter states as normal cases that must recover cleanly when the player reopens the UI or retries an action.

**Alternatives considered**: Treat failed startup as an exceptional edge case. Rejected because early settlement failures are common and should not trap the player.

## Decision: Improve Readability Through Layout and Copy, Not More Controls

**Rationale**: A polished onboarding UI should surface the current state, the single best next action, and any blockers without requiring the player to hunt for controls. The UI should stay within the existing supported resolution range and avoid duplicate or stale status text.

**Alternatives considered**: Add more tabs, more buttons, or denser dashboards. Rejected because that would make the starter flow harder to parse.

## Decision: Keep Agent State Additive

**Rationale**: If the agent HTTP state needs to reflect the improved starter flow, it should do so with optional fields only, preserving compatibility for existing tooling and tests.

**Alternatives considered**: New endpoints or breaking JSON changes. Deferred because the feature does not require a contract break to achieve its goals.

## Decision: Use Mandatory Integration Tests for Verification

**Rationale**: This feature touches villagers, villages, and UI, which are explicitly covered by the repository rules for mandatory headless integration testing. Validation should include the full `--test` suite plus focused layout and blocked-action assertions.

**Alternatives considered**: Manual-only validation. Rejected because it would not satisfy the project’s quality gate for gameplay and UI changes.
