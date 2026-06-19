# Implementation Plan: Improved Villager Flow

**Branch**: `002-improve-villager-flow` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-improve-villager-flow/spec.md`

## Summary

Make the settlement and villager loop a first-class player experience by improving
clarity, guidance, and feedback around the existing Town Board (`V` key),
citizen management, recruitment, and in-world villager presence. The work extends
current village UI and domain helpers — `VillageScreen` + panel architecture,
`VillageViewModel`, `VillageGuidance`, `VillagerActivityText`, `VillageEvents`,
`EarlyGameGuide`, and `AgentStateSerializer` — rather than replacing village
simulation or save formats. Priorities: (1) at-a-glance settlement status and next
action on Overview without tab hunting, (2) richer per-villager activity text and
inline blocked-action explanations on job assign/recruit, (3) unified guidance
across HUD, Town Board, and toasts, (4) optional in-world affordances and agent
state fields for automation.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: MonoGame 3.8 DesktopGL; existing Autonocraft village,
entities, UI, engine HUD, and agent HTTP projects

**Storage**: No new save format version required. Villager assignments, population,
food stock, housing, and settlement metadata must continue round-tripping through
existing `world.json` save paths.

**Testing**: `dotnet run --project src/Autonocraft -- --test`; extend
`tests/Autonocraft.Tests/Integration/VillageTests.cs` for guidance, layout,
blocked-action messaging, and activity text; optional manual validation via
`--skip-menu` + `tests/interact.py`

**Target Platform**: Desktop game on macOS, Windows, and Linux through MonoGame
DesktopGL

**Project Type**: Desktop voxel sandbox game with settlement simulation and local
agent HTTP inspection

**Performance Goals**: Town Board refresh and guidance computation must not add
per-frame world scans. Reuse existing `VillageViewModel.Build` cadence (already
throttled in `VillageScreen`) and villager-local state. Target: no measurable
frame-time regression in settlement UI at 1280×720.

**Constraints**: Preserve `V` Town Board, `C` village chat (when AI enabled),
hotbar 0–8 indexing, existing HTTP action commands (`recruit_villager`,
`assign_job`), and save compatibility. UI must remain readable at 1280×720.

**Scale/Scope**: UX and presentation layer over existing village systems; no new
villager AI behaviors, professions, or economy rules unless required to surface
blocked states honestly.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Code Quality and Architecture**: PASS. Domain guidance and activity copy stay
  in `Autonocraft.Village` (`VillageGuidance`, `VillagerActivityText`, new
  `SettlementActionAdvisor` / `JobAssignmentResult` helpers). UI presentation
  stays in `src/Autonocraft/UI/` (`VillageScreen`, `VillagePanels`,
  `VillageViewModel`). Agent DTO extensions stay in
  `Autonocraft.Core/Agent/Serialization`. No new projects or dependencies.
- **Testing Evidence**: PASS. Mandatory integration suite:
  `dotnet run --project src/Autonocraft -- --test`. Add focused tests for
  guidance priority, activity descriptions, blocked recruit/assign reasons, and
  `RunVillageScreenInputLayout` at 1280×720. Document manual Town Board
  walkthrough in `quickstart.md`.
- **User Experience Consistency**: PASS. Existing hotkeys preserved; improved
  copy and discoverability only. Agent `/state` gains optional additive JSON
  fields (`nextAction`, `activity`, `blockedReason`) without breaking existing
  consumers. Non-AI players never hit dead-end chat prompts.
- **Performance Budget**: PASS. Guidance derives from village + villager manager
  state already loaded for UI. Activity text uses per-villager fields and
  village building/site lookups (bounded by settlement size). No chunk-wide
  scans in UI update paths.

### Post-Design Re-check (Phase 1)

All gates remain PASS. Contracts limit agent additions to optional fields.
`JobAssignmentResult` avoids duplicating assignment logic in UI.

## Project Structure

### Documentation (this feature)

```text
specs/002-improve-villager-flow/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── village-ui-contract.md
│   └── agent-state-contract.md
└── tasks.md             # Phase 2 (/speckit-tasks) — not created by /speckit-plan
```

### Source Code (repository root)

```text
src/
├── Autonocraft.Village/
│   ├── AI/VillageGuidance.cs
│   ├── Citizens/VillagerActivityText.cs
│   ├── Scheduling/JobDispatcher.cs
│   ├── VillageManager.cs
│   └── VillageEvents.cs
├── Autonocraft/
│   ├── UI/
│   │   ├── VillageScreen.cs (+ Chrome, Input, Founding, Helpers partials)
│   │   ├── VillageChatScreen.cs
│   │   ├── Village/VillageViewModel.cs
│   │   └── VillagePanels/
│   │       ├── OverviewPanel.cs
│   │       ├── PeoplePanel.cs
│   │       └── FoundingPanel.cs
│   └── Game/GameOverlayRouter.cs
├── Autonocraft.Core/
│   ├── Game/EarlyGameGuide.cs
│   └── Agent/Serialization/AgentStateSerializer.cs
└── Autonocraft.Engine/
    ├── Hud/HudRenderer.cs
    └── Visuals/VillagerNameplateRenderer.cs

tests/
└── Autonocraft.Tests/
    └── Integration/VillageTests.cs
```

**Structure Decision**: Extend the existing panel-based Town Board and village
domain text helpers. Introduce a small `JobAssignmentResult` (or equivalent) in
`Autonocraft.Village` so UI and tests share one source for assign/recruit
failure reasons instead of duplicating `JobDispatcher` switch logic in panels.

## Complexity Tracking

No constitution violations or added architectural complexity are currently
required.
