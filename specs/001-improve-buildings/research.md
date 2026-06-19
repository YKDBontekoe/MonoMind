# Research: Improved Buildings

## Decision: Improve Existing Structure Definitions First

**Rationale**: The current catalog already includes generated structures across
biomes and tiers through `StructureRegistry` and `ProceduralStructures`. Updating
the existing small and medium structures gives immediate player-facing value,
keeps structure gallery coverage intact, and avoids introducing a parallel
building system.

**Alternatives considered**: Add a new building generator framework; import
external structure assets; add only new structure IDs. These options increase
scope before solving the visible quality issue in existing buildings.

## Decision: Use Reusable Detail Helpers for Visual Variety

**Rationale**: `StructureBuilder`, `MedievalDetailKit`, `RoomStamper`, and
`StructurePaths` already centralize common construction patterns such as roofs,
arches, paths, walls, and interiors. Extending these helpers keeps visual rules
consistent and reduces duplicated block stamping across structure definitions.

**Alternatives considered**: Hand-code every detail inside each structure
method. That makes variation harder to maintain and increases regression risk.

## Decision: Focus on Exteriors, Interiors, and Placement Together

**Rationale**: Better buildings need more than decorative blocks. The spec
requires recognizable silhouettes, meaningful interiors, clear entrances, and
reliable terrain placement. Planning must treat these as one acceptance surface
because a visually rich building still fails if players cannot enter it or if it
clips into the ground.

**Alternatives considered**: Exterior-only upgrades; interior-only upgrades.
Both miss core parts of the user complaint and success criteria.

## Decision: Preserve Current Public and Agent Workflows

**Rationale**: Existing automation uses the structure gallery, `/structures`,
teleport actions, and screenshots for inspection. The feature can be validated
through these paths without changing public HTTP contracts.

**Alternatives considered**: Add new agent endpoints for quality inspection.
This may be useful later, but the current feature can use existing catalog and
screenshot flows.

## Decision: Add/Extend Tests Around Structural Quality Signals

**Rationale**: Existing tests verify structure placement, fingerprint matching,
gallery inclusion, overlap separation, chest presence, and village claiming.
The feature should add targeted assertions for accessibility, interior detail,
distinctive block/detail counts, or tier-appropriate footprint constraints where
practical.

**Alternatives considered**: Rely only on visual review. Visual review is needed
for aesthetics, but automated tests are required to prevent broken entrances,
overlap, lost chests, and claim regressions.

## Decision: Keep Performance Bounded by Existing Tier Footprints

**Rationale**: Structure generation runs during chunk generation. Improved
buildings must avoid unbounded loops and excessive block counts, and should use
existing template chunk indexing. Tier footprints and gallery placement provide
natural limits.

**Alternatives considered**: Allow highly variable procedural sprawl. That would
make gallery layout, chunk generation cost, and placement flatness harder to
control.
