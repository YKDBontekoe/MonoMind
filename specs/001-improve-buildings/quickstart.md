# Quickstart: Improved Buildings Validation

## Prerequisites

- .NET 10 SDK installed
- Python 3 available for `tests/interact.py`
- Working tree contains the improved building implementation

## 1. Run Required Integration Tests

```bash
dotnet run --project src/Autonocraft -- --test
```

Expected result:

```text
ALL TESTS PASSED SUCCESSFULLY! (EXIT CODE: 0)
```

This is mandatory because the feature touches structures, world generation,
villages, saves, UI-free agent inspection, and rendering validation.

## 2. Validate Structure Gallery Visually

Start the structure gallery:

```bash
dotnet run --project src/Autonocraft -- --structure-gallery --agent-port 5001
```

In another terminal:

```bash
python3 tests/interact.py wait
python3 tests/interact.py state
```

Expected result:

- Agent API reports `gameState` as `Playing`
- `worldType` is `StructureGallery`
- `structureGallery` is `true`

## 3. Inspect Updated Buildings

Fetch the structure catalog:

```bash
curl -s http://localhost:5001/structures
```

For each updated structure, teleport near its catalog anchor:

```bash
python3 tests/interact.py action teleport x=<anchor.x> y=<anchor.y+4> z=<anchor.z>
python3 tests/interact.py screenshot test_output/structure_gallery/<structure-id>.png
```

Expected result:

- Building is reachable and visually grounded
- Entrance is visible or discoverable from approach
- Interior can be inspected where the building is enterable
- Screenshot shows a nonblank, recognizable structure

## 4. Validate Claimable Building Flows

For claimable structures such as cottages or outposts, confirm existing village
claim behavior still works through the integration suite and optional manual
inspection:

```bash
python3 tests/interact.py action dev cmd_line="creative on"
python3 tests/interact.py action dev cmd_line="time noon"
```

Expected result:

- Claimable buildings preserve their visible claim affordance
- Village-related tests remain green after implementation

## 5. Performance Smoke Check

While moving around the structure gallery and a default world, confirm there are
no noticeable new stalls when approaching improved buildings. If a benchmark is
needed for a large structure change, compare against `docs/BENCHMARK_BASELINE.md`
and document the result in completion notes.

## Current Evidence

- Structure gallery screenshot artifacts are present under
  `test_output/structure_gallery/`.
- `test_output/structure_gallery/manifest.json` lists the captured gallery
  anchors and screenshot paths for the current catalog, including
  `ForestShelter.png`, `PlainsCottage.png`, `ForestWatchtower.png`,
  `SnowyHut.png`, and `VillageOutpost.png`.
- Atlas validation is not required for this feature revision because no block
  textures, atlas layout entries, or atlas build sources were changed.

## 6. Shutdown

```bash
python3 tests/interact.py action shutdown
```
