# Data Model: Improved Buildings

## Building Type

Represents a category of generated structure with a recognizable role.

**Fields**

- `id`: Stable catalog identifier, such as `PlainsCottage` or `ForestShelter`
- `tier`: Placement scale category (`Small`, `Medium`, `Large`, `Mega`)
- `allowedBiomes`: Biomes where the building may appear
- `role`: Player-facing purpose or mood, such as shelter, outpost, shrine,
  worksite, landmark, or ruin
- `claimable`: Whether the building participates in village claiming workflows

**Relationships**

- Has one or more Building Variants.
- Appears in one or more Placement Contexts.
- May contain Interior Features.

**Validation Rules**

- ID remains stable for existing structures unless a migration is explicitly
  planned.
- Tier footprint remains compatible with gallery spacing and placement flatness.
- Existing claimable structures retain their claim affordances.

## Building Variant

Represents one deterministic presentation of a Building Type.

**Fields**

- `sourceTypeId`: Building Type identifier
- `variantSeed`: Deterministic seed/salt input used to choose variation
- `silhouetteFeatures`: Distinguishing exterior traits, such as roof profile,
  tower, porch, chimney, arch, dock, ruin break, or landmark element
- `materialPalette`: Coherent block family selected for biome and role
- `footprintRadius`: Maximum expected footprint around the anchor
- `blockCountBand`: Expected approximate size band for validation

**Relationships**

- Belongs to one Building Type.
- Contains zero or more Interior Features.
- Is placed in a Placement Context.

**Validation Rules**

- Variants must be deterministic for the same world seed, anchor, and salt.
- Variants must not exceed tier-appropriate footprint expectations without
  updating gallery spacing and placement tests.
- Variants must include at least one exterior distinction and one interior or
  purpose detail when the structure is enterable.

## Interior Feature

Represents an internal point of interest or room detail.

**Fields**

- `featureKind`: Furnishing, storage, work area, light, path, bed/rest cue,
  station cue, village cue, decorative identity, or loot marker
- `location`: Relative position within the building template
- `accessibility`: Whether the player can reach or use the feature
- `gameplayRole`: Optional role such as loot, crafting, village claim cue, or
  shelter signal

**Relationships**

- Belongs to a Building Variant.
- May depend on existing block types, loot markers, or station blocks.

**Validation Rules**

- Must not block required entrances or navigation paths.
- Must not place interactable content in unreachable cells.
- Must preserve existing gameplay roles tied to chests, stations, or village
  claiming.

## Placement Context

Represents the terrain and catalog context where a building appears.

**Fields**

- `worldType`: Default world, structure gallery, or another world preset
- `biome`: Biome sampled at the anchor
- `anchor`: Relative or world placement point
- `surfaceY`: Ground level used for placement
- `flatnessAllowance`: Maximum supported terrain height delta
- `nearbyObstacles`: Terrain, trees, fluids, or neighboring structures that can
  affect access

**Relationships**

- Places one Building Variant.
- Is validated by world generation, structure gallery, and visual inspection.

**Validation Rules**

- Entrances remain reachable from surrounding terrain.
- Structures avoid major buried, floating, overlapping, or clipped sections.
- Gallery placement includes every registered structure and keeps non-overlap
  spacing valid.

## State Transitions

```text
Registered building type
  -> deterministic variant resolved for seed/anchor/biome
  -> template placed in world or gallery
  -> player discovers exterior
  -> player enters and inspects interior
  -> optional claim/use/loot interaction
  -> save and reload preserves world state and gameplay affordances
```
