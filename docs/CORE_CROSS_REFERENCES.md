# Core Cross-Reference Inventory (Phase 0)

> **Status:** `Autonocraft.Engine` is now a standalone assembly (no `using Autonocraft.Core`). World and Crafting mitigations below may still apply.

Catalog of `using Autonocraft.Core` from **World**, **Engine**, and **Crafting** before assembly extraction. Target: eliminate World→Core and Engine→Core references via Diagnostics, Domain contracts, and `GameRenderContext` relocation.

## World → Core (5 files)

| File | Core types used |
|------|-----------------|
| `World/VoxelWorld.cs` | `PerfCounters` (GetBlockCalls, RecordMeshBuild, PendingMeshCount) |
| `World/MeshBuildScheduler.cs` | `PerfCounters.RecordMeshBuild` |
| `World/TerrainGenScheduler.cs` | `PerfCounters` |
| `World/WorldSaveData.cs` | `SurvivalConstants.MaxHunger` (player save defaults) |
| `World/WorldSaveManager.cs` | `SaveSnapshot`, `PlayerStatistics`, `PlayerStatisticsSaveData` |

**Mitigation:** Move `PerfCounters` → `Autonocraft.Diagnostics`; save DTOs + `SaveSnapshot` → `Autonocraft.Domain.Persistence`; `SurvivalConstants.MaxHunger` default → Domain constant.

## Engine → Core (9 files)

| File | Core types used |
|------|-----------------|
| `Engine/Renderer.cs` | `GameRenderContext` |
| `Engine/WorldRenderer.cs` | `GameRenderContext`, `PerfCounters`, `BlockInteractionSystem` |
| `Engine/HudRenderer.cs` | `GameRenderContext`, `PerfCounters`, `Player`, `BlockInteractionSystem` |
| `Engine/FloraRenderer.cs` | `PerfCounters` |
| `Engine/BlockOverlayRenderer.cs` | `BlockInteractionSystem` |
| `Engine/VillagerNameplateRenderer.cs` | `GameRenderContext` |
| `Engine/Animation/InteractionAnimator.cs` | `Player` |
| `Engine/Animation/UiTransition.cs` | (minimal Core usage) |
| `Engine/Audio/AudioManager.cs` | Core settings/types |

**Mitigation:** Move `GameRenderContext` + `PerfCounters` usage to Engine/Diagnostics; renderers consume context only (no direct Core import for `GameRenderContext`).

## Crafting → Core (1 file)

| File | Core types used |
|------|-----------------|
| `Crafting/CraftingSystem.cs` | `Player` (inventory/hotbar), `Engine.Animation.InteractionAnimator` via Core |

**Mitigation:** `IPlayerInventory` + `ICraftingFx` in Domain; Core implements adapters.

## Baseline benchmark (2026-06-16, seed 42424)

Recorded via `dotnet run --project src/Autonocraft -- --bench`. See `docs/BENCHMARK_BASELINE.md` for full numbers.
