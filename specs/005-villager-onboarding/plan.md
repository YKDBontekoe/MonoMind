# Implementation Plan: Villager Onboarding

**Branch**: `005-villager-onboarding` | **Date**: 2026-06-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/005-villager-onboarding/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Improve the village-starting experience so players can reliably obtain the first villagers or settlers, understand why an action is blocked, and manage the starter settlement through a clearer and more polished UI. The plan keeps the work inside the existing village simulation and UI architecture: `VillageManager.TryRecruit`, `SettlementGuidance`, `VillageGuidance`, `VillageViewModel`, `VillageScreen` / panel layout, `VillagerActivityText`, and `AgentStateSerializer`. Priority order: (1) make the first villager-add path work and explain failures, (2) stabilize starter-state recovery and state refresh, (3) clean up the UI hierarchy and layout at supported resolutions, and (4) keep player-facing and agent-facing state aligned.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: MonoGame 3.8 DesktopGL; existing Autonocraft village, UI, engine, and agent HTTP projects

**Storage**: Existing file-based save data (`world.json`) with no new save version for this feature

**Testing**: `dotnet run --project src/Autonocraft -- --test`; focused village integration coverage in `tests/Autonocraft.Tests/Integration/VillageTests.*.cs` and targeted layout / blocked-action assertions

**Target Platform**: Desktop game on macOS, Windows, and Linux via MonoGame DesktopGL

**Project Type**: Desktop voxel sandbox game with settlement simulation and local agent HTTP inspection

**Performance Goals**: No measurable frame-time regression in settlement UI; guidance and validation must stay bounded to the active village and villagers already loaded for the UI

**Constraints**: Preserve hotbar 0–8 indexing, existing village-management shortcuts and controls, existing recruit/assign semantics, and save compatibility while making the starter flow clear and reliable

**Scale/Scope**: UX and presentation work over the current village onboarding path; no new village economy rules or villager AI systems unless needed to expose truthful blocked states

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Code Quality and Architecture**: PASS. The work stays inside existing village and UI boundaries: `Autonocraft.Village` for guidance and recruit results, `Autonocraft.UI` for panel and onboarding presentation, and `Autonocraft.Core` for agent serialization only if additive fields are needed. No new projects or external dependencies are required.
- **Testing Evidence**: PASS. This feature touches villagers, villages, and UI, so `dotnet run --project src/Autonocraft -- --test` is mandatory. Add or extend integration assertions for recruit success/failure reasons, starter-state recovery, and 1280×720 layout behavior.
- **User Experience Consistency**: PASS. Preserve existing controls and hotkeys, keep recruit/assign terminology consistent, show blocked reasons in plain language, and keep agent state additive if surfaced.
- **Performance Budget**: PASS. The feature should not introduce broad scans or per-frame allocations beyond the current village dashboard refresh cadence. Validation is by existing settlement UI test paths and the mandatory integration suite, with no expected frame-time regression.

### Post-Design Re-check (Phase 1)

All gates remain PASS. The design reuses existing recruit results and guidance structures, and any agent-state additions remain optional and backward compatible.

## Project Structure

### Documentation (this feature)

```text
specs/005-villager-onboarding/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── Autonocraft.Village/
│   ├── AI/SettlementGuidance.cs
│   ├── AI/VillageGuidance.cs
│   ├── Scheduling/JobAssignmentResult.cs
│   ├── VillageManager.cs
│   └── VillageEvents.cs
├── Autonocraft/
│   ├── UI/VillageScreen*.cs
│   ├── UI/Village/
│   │   └── VillageViewModel.cs
│   └── UI/VillagePanels/
│       ├── OverviewPanel.cs
│       ├── PeoplePanel.cs
│       └── BuildPanel.cs
└── Autonocraft.Core/
    └── Agent/Serialization/AgentStateSerializer.cs

tests/
└── Autonocraft.Tests/
    └── Integration/VillageTests.*.cs
```

**Structure Decision**: Extend the existing village simulation and panel-based Town Board rather than introducing a separate onboarding surface. Keep the domain truth in `Autonocraft.Village`, the presentation in `Autonocraft.UI`, and the agent contract additive in `Autonocraft.Core`.

## Complexity Tracking

No constitution violations or added architectural complexity are currently required.
