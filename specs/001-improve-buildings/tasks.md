# Tasks: Improved Buildings

**Input**: Design documents from `/specs/001-improve-buildings/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required. This feature touches structures, world generation, villages,
saves, rendering validation, and agent inspection. The final gate includes
`dotnet run --project src/Autonocraft -- --test`.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the structure-quality baseline and reusable test surface before changing buildings.

- [X] T001 Review the current registered structure catalog and note target small/medium building IDs in `src/Autonocraft.World/Structures/StructureRegistry.cs`
- [X] T002 [P] Review current procedural building helpers and identify reusable exterior/interior helpers in `src/Autonocraft.World/Structures/StructureBuilder.cs`
- [X] T003 [P] Review current decorative helpers and identify extension points in `src/Autonocraft.World/Structures/MedievalDetailKit.cs`
- [X] T004 [P] Review current room/detail stamping helpers and identify extension points in `src/Autonocraft.World/Structures/RoomStamper.cs`
- [X] T005 [P] Review current gallery and fingerprint coverage for structures in `tests/Autonocraft.Tests/Integration/WorldGenTests.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add reusable validation helpers and shared structure-detail primitives that all user stories depend on.

**CRITICAL**: No user story implementation should begin until this phase is complete.

- [X] T006 Add reusable structure quality assertion helpers for entrances, interior air volume, distinctive detail counts, and footprint bounds in `tests/Autonocraft.Tests/Integration/StructureQualityAssertions.cs`
- [X] T007 Add deterministic template inspection helpers for registered structures and gallery placements in `tests/Autonocraft.Tests/Integration/StructureQualityAssertions.cs`
- [X] T008 Extend reusable exterior detail helpers for porches, awnings, facade depth, dormers, roof trims, and landmark accents in `src/Autonocraft.World/Structures/MedievalDetailKit.cs`
- [X] T009 Extend reusable interior detail helpers for shelves, bedrolls, work corners, light fixtures, storage nooks, and clear walking lanes in `src/Autonocraft.World/Structures/RoomStamper.cs`
- [X] T010 Update shared approach/entrance helpers so generated buildings can keep visible, unblocked entrances in `src/Autonocraft.World/Structures/StructurePaths.cs`
- [ ] T011 Run focused structure quality helper tests and fix compile issues in `tests/Autonocraft.Tests/Integration/StructureQualityAssertions.cs`

**Checkpoint**: Shared validation and helper primitives are ready for story work.

---

## Phase 3: User Story 1 - Discover Memorable Buildings (Priority: P1) MVP

**Goal**: Existing generated buildings have recognizable silhouettes, coherent materials, and distinctive exterior features.

**Independent Test**: Load structure templates or the structure gallery and verify updated buildings have non-flat silhouettes, coherent materials, and at least one distinctive exterior feature visible from approach distance.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [X] T012 [P] [US1] Add structure catalog quality tests for distinctive exterior features on target small/medium buildings in `tests/Autonocraft.Tests/Integration/WorldGenTests.cs`
- [X] T013 [P] [US1] Add tier footprint and deterministic variant tests for updated building templates in `tests/Autonocraft.Tests/Integration/WorldGenTests.cs`
- [X] T014 [P] [US1] Add structure catalog contract notes for exterior quality acceptance in `specs/001-improve-buildings/contracts/structure-catalog-contract.md`

### Implementation for User Story 1

- [X] T015 [US1] Improve `ForestShelter` exterior silhouette, material layering, roof profile, porch/path approach, and visible landmark detail in `src/Autonocraft.World/Structures/ProceduralStructures.cs`
- [X] T016 [US1] Improve `PlainsCottage` exterior silhouette, half-timber depth, roof variation, chimney/porch details, and visible role cues in `src/Autonocraft.World/Structures/ProceduralStructures.cs`
- [X] T017 [US1] Improve `VillageOutpost` exterior silhouette, watch/settlement identity, entrance readability, and approach detail in `src/Autonocraft.World/Structures/ProceduralStructures.cs`
- [X] T018 [US1] Improve `ForestWatchtower`, `SnowyHut`, and other target medium building exteriors with tier-appropriate distinctive details in `src/Autonocraft.World/Structures/ProceduralStructures.cs`
- [X] T019 [US1] Tune biome-specific material palettes for improved buildings without changing public structure IDs in `src/Autonocraft.World/Structures/BiomeStructurePalette.cs`
- [ ] T020 [US1] Verify all US1 exterior tests pass and update expected thresholds if needed in `tests/Autonocraft.Tests/Integration/WorldGenTests.cs`

**Checkpoint**: User Story 1 is independently complete when updated buildings are visually distinct from approach and pass catalog/exterior tests.

---

## Phase 4: User Story 2 - Inspect Useful Interiors (Priority: P2)

**Goal**: Enterable buildings contain meaningful, navigable interiors and reachable points of interest.

**Independent Test**: Enter each updated building type and verify clear walkable space, meaningful interior details, reachable content, and no blocked movement paths.

### Tests for User Story 2

- [X] T021 [P] [US2] Add interior navigability and clear-walking-lane tests for enterable updated buildings in `tests/Autonocraft.Tests/Integration/WorldGenTests.cs`
- [X] T022 [P] [US2] Add reachable chest/station/interior-feature tests for target buildings in `tests/Autonocraft.Tests/Integration/WorldGenTests.cs`
- [X] T023 [P] [US2] Add village claim preservation coverage for improved claimable interiors in `tests/Autonocraft.Tests/Integration/VillageTests.cs`

### Implementation for User Story 2

- [X] T024 [US2] Add meaningful interior layouts, furnishing, light, storage, and clear walkable paths to `ForestShelter` in `src/Autonocraft.World/Structures/ProceduralStructures.cs`
- [X] T025 [US2] Add meaningful interior layouts, furnishing, station/storage cues, and clear walkable paths to `PlainsCottage` in `src/Autonocraft.World/Structures/ProceduralStructures.cs`
- [X] T026 [US2] Add meaningful interior layouts, watch/settlement cues, reachable storage, and clear walkable paths to `VillageOutpost` in `src/Autonocraft.World/Structures/ProceduralStructures.cs`
- [X] T027 [US2] Apply reusable room-stamping details to other enterable target structures without blocking traversal in `src/Autonocraft.World/Structures/RoomStamper.cs`
- [ ] T028 [US2] Verify all US2 interior and village-claim tests pass in `tests/Autonocraft.Tests/Integration/WorldGenTests.cs`

**Checkpoint**: User Story 2 is independently complete when interiors are readable, navigable, useful, and existing claim/storage/station affordances remain reachable.

---

## Phase 5: User Story 3 - Preserve World and Settlement Playability (Priority: P3)

**Goal**: Improved buildings remain reliable in normal worlds, saves, villages, structure gallery, screenshots, and agent inspection.

**Independent Test**: Generate worlds and the structure gallery, visit updated buildings, and verify players and automation can approach, enter, screenshot, save, and reload without regressions.

### Tests for User Story 3

- [X] T029 [P] [US3] Extend structure gallery placement tests for updated building reachability, non-overlap, and no major clipping in `tests/Autonocraft.Tests/Integration/WorldGenTests.cs`
- [X] T030 [P] [US3] Extend save/reload or world round-trip coverage for improved claimable/generated buildings in `tests/Autonocraft.Tests/Integration/SaveTests.cs`
- [X] T031 [P] [US3] Add agent inspection workflow validation notes for `/structures`, teleport, and screenshot coverage in `specs/001-improve-buildings/contracts/agent-inspection-contract.md`

### Implementation for User Story 3

- [X] T032 [US3] Adjust structure footprint radii or gallery spacing only if improved templates require it in `src/Autonocraft.World/Structures/StructureRegistry.cs`
- [X] T033 [US3] Adjust structure gallery placement metadata only if improved templates require it in `src/Autonocraft.World/Structures/StructureGallery.cs`
- [X] T034 [US3] Ensure placement logic still grounds updated templates correctly on valid terrain in `src/Autonocraft.World/Structures/StructurePlacer.cs`
- [X] T035 [US3] Preserve deterministic template resolution and chunk indexing for improved structures in `src/Autonocraft.World/Structures/StructurePlacementKeys.cs`
- [ ] T036 [US3] Verify all US3 gallery, save, and placement tests pass in `tests/Autonocraft.Tests/Integration/WorldGenTests.cs`

**Checkpoint**: User Story 3 is independently complete when improved buildings remain compatible with gallery inspection, saves, villages, and automation.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate the full feature, capture visual evidence, and update documentation.

- [X] T037 [P] Update structure documentation and building-quality notes in `docs/ARCHITECTURE.md`
- [X] T038 [P] Update agent validation workflow notes for structure gallery screenshots in `AGENTS.md`
- [ ] T039 Run the mandatory integration suite and record the result in `specs/001-improve-buildings/quickstart.md`
- [X] T040 Run structure gallery visual inspection screenshots for updated buildings and store outputs under `test_output/structure_gallery/`
- [X] T041 Run atlas validation only if block textures or atlas entries changed and record the result in `specs/001-improve-buildings/quickstart.md`
- [X] T042 Review generated templates for avoidable duplication and refactor shared patterns into `src/Autonocraft.World/Structures/MedievalDetailKit.cs`
- [ ] T043 Confirm all checklist tasks and validation evidence are complete in `specs/001-improve-buildings/tasks.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup; blocks all user story implementation.
- **User Story 1 (Phase 3)**: Depends on Foundational; MVP scope.
- **User Story 2 (Phase 4)**: Depends on Foundational and benefits from US1 exterior structure changes.
- **User Story 3 (Phase 5)**: Depends on Foundational and should run after any template footprint/content changes from US1/US2.
- **Polish (Phase 6)**: Depends on all selected user stories.

### User Story Dependencies

- **US1 Discover Memorable Buildings**: First deliverable and suggested MVP.
- **US2 Inspect Useful Interiors**: Can start after foundational helpers, but final interiors should align with US1 exterior footprints.
- **US3 Preserve World and Settlement Playability**: Validates the whole feature and should complete before release.

### Within Each User Story

- Tests MUST be written before implementation changes.
- Shared helpers before template updates.
- Template updates before gallery/placement tuning.
- Story checkpoint validation before moving to the next priority.

### Parallel Opportunities

- T002, T003, T004, and T005 can run in parallel during setup.
- T012, T013, and T014 can run in parallel for US1 test/contract preparation.
- T021, T022, and T023 can run in parallel for US2 tests.
- T029, T030, and T031 can run in parallel for US3 validation preparation.
- T037 and T038 can run in parallel once implementation behavior is final.

---

## Parallel Example: User Story 1

```bash
# Parallel test/contract preparation:
Task: "T012 Add structure catalog quality tests in tests/Autonocraft.Tests/Integration/WorldGenTests.cs"
Task: "T013 Add tier footprint and deterministic variant tests in tests/Autonocraft.Tests/Integration/WorldGenTests.cs"
Task: "T014 Add exterior quality notes in specs/001-improve-buildings/contracts/structure-catalog-contract.md"

# Sequential implementation after tests:
Task: "T015 Improve ForestShelter exterior in src/Autonocraft.World/Structures/ProceduralStructures.cs"
Task: "T016 Improve PlainsCottage exterior in src/Autonocraft.World/Structures/ProceduralStructures.cs"
```

## Parallel Example: User Story 2

```bash
# Parallel test preparation:
Task: "T021 Add interior navigability tests in tests/Autonocraft.Tests/Integration/WorldGenTests.cs"
Task: "T022 Add reachable feature tests in tests/Autonocraft.Tests/Integration/WorldGenTests.cs"
Task: "T023 Add village claim preservation coverage in tests/Autonocraft.Tests/Integration/VillageTests.cs"
```

## Parallel Example: User Story 3

```bash
# Parallel validation preparation:
Task: "T029 Extend structure gallery placement tests in tests/Autonocraft.Tests/Integration/WorldGenTests.cs"
Task: "T030 Extend save/reload coverage in tests/Autonocraft.Tests/Integration/SaveTests.cs"
Task: "T031 Add agent inspection validation notes in specs/001-improve-buildings/contracts/agent-inspection-contract.md"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and Phase 2.
2. Add failing exterior/catalog quality tests for US1.
3. Improve exteriors for target small/medium buildings.
4. Validate US1 independently through tests and structure gallery inspection.

### Incremental Delivery

1. US1: Memorable exteriors and visual distinction.
2. US2: Useful interiors and reachable details.
3. US3: Full gallery/world/save/village/agent reliability.
4. Polish: Documentation, screenshots, integration suite, and final evidence.

### Final Validation

Run:

```bash
dotnet run --project src/Autonocraft -- --test
```

Then follow `specs/001-improve-buildings/quickstart.md` for structure gallery
visual inspection and screenshot capture.

---

## Notes

- [P] tasks = different files or independently preparable work with no dependency on incomplete tasks.
- [US1], [US2], and [US3] labels map tasks to the prioritized user stories in `specs/001-improve-buildings/spec.md`.
- The feature should not introduce new public agent endpoints unless a later plan explicitly expands scope.
- Existing structure IDs should remain stable to preserve gallery, save, and claim behavior.

## Phase 7: Convergence

- [ ] T044 Run `dotnet run --project src/Autonocraft -- --test`, fix any remaining improved-building regressions, and record a successful required-suite result in `specs/001-improve-buildings/quickstart.md` per FR-007 / SC-004 (partial)
- [X] T045 Extend `tests/Autonocraft.Tests/Integration/VillageTests.cs` and `tests/Autonocraft.Tests/Integration/SaveTests.cs` to prove improved claimable buildings preserve claim, reachable content, and save/reload stability for the updated interiors per FR-008 / US2/AC2 / US3/AC2 (partial)
- [X] T046 Validate enlarged improved-building footprints in `src/Autonocraft.World/Structures/ProceduralStructures.cs` against `src/Autonocraft.World/Structures/StructureGallery.cs`, `src/Autonocraft.World/Structures/StructureRegistry.cs`, and `src/Autonocraft.World/Structures/StructurePlacer.cs`, adjusting metadata or spacing only where gallery overlap, clipping, or grounding issues remain per FR-005 / SC-002 / US3/AC1 (partial)
- [X] T047 Update `docs/ARCHITECTURE.md` and `AGENTS.md` with the improved-building quality approach, gallery validation workflow, and required inspection commands per plan: Structure Decision / Constitution III (missing)
- [ ] T048 Record final visual and performance validation evidence in `specs/001-improve-buildings/quickstart.md`, including the structure gallery screenshot set under `test_output/structure_gallery/` and timed-suite observations for SC-006 / Constitution IV (partial)
