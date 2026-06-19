# Contract: Structure Catalog Quality

## Purpose

Define the expected externally visible behavior of the generated building
catalog after the improvement feature. This contract is validated through normal
world generation, the structure gallery, and gameplay inspection.

## Catalog Expectations

- Every registered structure remains present in the structure gallery catalog.
- Existing structure IDs remain stable unless a migration is explicitly planned.
- Existing claimable buildings remain claimable.
- Updated buildings expose a recognizable exterior silhouette and at least one
  purpose or interior detail when enterable.
- Updated buildings remain grounded, reachable, and non-overlapping in the
  structure gallery.

## Player-Facing Expectations

- A player approaching a building can visually distinguish it from terrain and
  from the simplest prior box-like form.
- A player can locate an entrance for enterable buildings without hidden or
  blocked access.
- A player can move through the intended interior route without being trapped by
  decorative details.
- Useful content such as chests, stations, or village cues remains reachable.

## Acceptance Checks

- Structure gallery includes all registered structures.
- Structure gallery fingerprint or equivalent placement checks pass for updated
  structures.
- Updated structures avoid major buried, floating, clipped, or overlapping
  placement.
- Village claim tests continue to pass for claimable world structures.
- Visual review confirms each updated building type has at least one distinctive
  exterior feature and one meaningful interior or purpose detail.
