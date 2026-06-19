# Implementation Plan: Improved Buildings

**Branch**: `fix/gameplay-perf-structures` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-improve-buildings/spec.md`

## Summary

Improve the existing generated building catalog so structures are more memorable,
varied, useful, and visually grounded. The implementation will extend the
current procedural structure system in `Autonocraft.World/Structures`, reusing
`StructureRegistry`, `ProceduralStructures`, `StructureBuilder`,
`MedievalDetailKit`, `RoomStamper`, `StructurePaths`, placement/fingerprint
helpers, and the existing structure gallery. The plan prioritizes existing
small/medium buildings first because they are most frequently encountered and
most directly match the user complaint.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: MonoGame 3.8 DesktopGL; existing Autonocraft world,
village, rendering, and test projects

**Storage**: No new storage format expected. Existing world saves must continue
to round-trip building-related world state and village claims.

**Testing**: `dotnet run --project src/Autonocraft -- --test`; focused unit or
integration coverage in `tests/Autonocraft.Tests`; optional visual validation
through `--structure-gallery` and `tests/interact.py screenshot`

**Target Platform**: Desktop game on macOS, Windows, and Linux through MonoGame
DesktopGL

**Project Type**: Desktop voxel sandbox game with procedural world generation
and local agent HTTP inspection

**Performance Goals**: No noticeable new stalls when approaching generated
buildings. Structure template generation must remain deterministic and bounded
by existing tier footprint expectations.

**Constraints**: Preserve existing controls, saves, village claiming behavior,
structure gallery behavior, `/structures` catalog semantics, and headless test
execution. Any structure touching world generation, structures, villages,
rendering, or saves requires the integration suite before completion.

**Scale/Scope**: Update the existing building/structure catalog, focusing on
encountered buildings and claimable settlement structures before adding entirely
new gameplay systems.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Code Quality and Architecture**: PASS. Work stays in existing world
  structure boundaries: `src/Autonocraft.World/Structures/` for templates,
  reusable detail helpers, palettes, paths, room stamping, and placement
  metadata. Tests stay under `tests/Autonocraft.Tests/Integration/`. No new
  project, dependency, serializer, or public abstraction is required.
- **Testing Evidence**: PASS. Required validation includes the headless
  integration suite: `dotnet run --project src/Autonocraft -- --test`. Feature
  work must also preserve structure gallery fingerprint/overlap coverage and
  village claim coverage. Visual validation should use structure gallery
  screenshots for the updated catalog.
- **User Experience Consistency**: PASS. The feature improves world content but
  does not change controls, HUD, hotbar indexing, dev commands, save/load UI, or
  agent endpoint semantics. Existing `/structures`, `/state`, `/action`, and
  screenshot workflows remain usable for inspection.
- **Performance Budget**: PASS. Structure templates must remain deterministic,
  finite, chunk-indexed, and appropriate for their tier footprints. Avoid
  unbounded random loops, large per-frame scans, and expensive runtime work
  outside existing generation/template resolution paths.

## Project Structure

### Documentation (this feature)

```text
specs/001-improve-buildings/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── structure-catalog-contract.md
│   └── agent-inspection-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── Autonocraft.World/
│   └── Structures/
│       ├── StructureRegistry.cs
│       ├── ProceduralStructures.cs
│       ├── StructureBuilder.cs
│       ├── MedievalDetailKit.cs
│       ├── RoomStamper.cs
│       ├── StructurePaths.cs
│       ├── StructureGallery.cs
│       └── StructurePlacementKeys.cs
├── Autonocraft.Domain/
│   └── World/
│       └── BlockType.cs
└── Autonocraft.AtlasBuild/

tests/
└── Autonocraft.Tests/
    └── Integration/
        ├── WorldGenTests.cs
        └── VillageTests.cs
```

**Structure Decision**: Use the existing procedural structure system. Update
current definitions and reusable helper kits before adding new structure IDs.
Only touch `BlockType.cs`, atlas build, or rendering if planning reveals a
specific missing block needed for the visual quality bar.

## Complexity Tracking

No constitution violations or added architectural complexity are currently
required.
