using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Engine;
using Autonocraft.Items;
using Autonocraft.Village;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.Core
{
    /// <summary>
    /// Blueprint ghost preview, founding placement, construction sites, and work-zone corner selection.
    /// </summary>
    internal sealed class BlueprintPlacementSystem
    {
        private readonly GameSession _session;
        private readonly Camera _camera;
        private readonly Func<IItemContainer> _hotbarProvider;
        private readonly Action<string> _showToast;

        private readonly List<BlueprintPlacementPreview> _constructionSitePreviews = new();

        public string? PendingBlueprintId { get; private set; }
        public bool PendingFoundingPlacement { get; private set; }
        public BlueprintPlacementPreview? BlueprintPreview { get; private set; }
        public WorkZonePlacementPreview? WorkZonePreview { get; private set; }
        public (int X, int Y, int Z)? WorkZoneCornerA { get; private set; }
        public bool WorkZonePlacementActive { get; private set; }

        public BlueprintPlacementSystem(
            GameSession session,
            Camera camera,
            Func<IItemContainer> hotbarProvider,
            Action<string> showToast)
        {
            _session = session;
            _camera = camera;
            _hotbarProvider = hotbarProvider;
            _showToast = showToast;
        }

        public bool HasPendingBlueprint => PendingBlueprintId != null;

        public string? GetHudPlacementHint()
        {
            if (PendingFoundingPlacement)
            {
                return "LOOK AT GROUND · LEFT CLICK PLACE TOWN HEART · ESC CANCEL";
            }

            if (PendingBlueprintId != null)
            {
                return "LOOK AT GROUND · LEFT CLICK PLACE · ESC CANCEL";
            }

            if (!WorkZonePlacementActive)
            {
                return null;
            }

            return WorkZoneCornerA.HasValue
                ? "CLICK OPPOSITE CORNER · ESC CANCEL"
                : "CLICK FIRST CORNER · ESC CANCEL";
        }

        public void StartFoundingTownHeartPlacement(Action closeVillageUi)
        {
            if (!PlayerStructureRegistry.TryGet("town_heart", out var blueprint))
            {
                return;
            }

            closeVillageUi();
            PendingFoundingPlacement = true;
            PendingBlueprintId = blueprint.Id;
            UpdateFoundingPlacementPreview(blueprint);
        }

        public void StartBlueprintPlacement(Village.Village village, string blueprintId, Action closeVillageUi)
        {
            if (!PlayerStructureRegistry.TryGet(blueprintId, out var blueprint))
            {
                return;
            }

            closeVillageUi();
            PendingBlueprintId = blueprintId;
            UpdateBlueprintPlacementPreview(village, blueprint);
        }

        public void CancelBlueprintPlacement()
        {
            PendingBlueprintId = null;
            PendingFoundingPlacement = false;
            BlueprintPreview = null;
        }

        public void ConfirmBlueprintPlacement()
        {
            if (PendingFoundingPlacement)
            {
                ConfirmFoundingPlacement();
                return;
            }

            if (PendingBlueprintId == null || BlueprintPreview == null || !BlueprintPreview.Valid)
            {
                return;
            }

            var village = _session.Villages.GetActiveVillage(_session.Player.Position);
            if (village == null)
            {
                CancelBlueprintPlacement();
                return;
            }

            var payer = village.Storage.HasSpaceFor(ItemStack.CreateBlock(BlockType.Dirt, 1))
                ? (IItemContainer)village.Storage
                : _hotbarProvider();

            _session.Villages.TryQueueBlueprint(
                _session.Grid,
                village,
                PendingBlueprintId,
                BlueprintPreview.AnchorX,
                BlueprintPreview.AnchorZ,
                payer,
                BlueprintPreview.AnchorY);

            CancelBlueprintPlacement();
        }

        private void ConfirmFoundingPlacement()
        {
            if (BlueprintPreview == null || !BlueprintPreview.Valid)
            {
                return;
            }

            if (!PlayerStructureRegistry.TryGet("town_heart", out var blueprint))
            {
                CancelBlueprintPlacement();
                return;
            }

            var payer = _hotbarProvider();
            if (!_session.Player.CreativeMode && !blueprint.CanAfford(payer))
            {
                _showToast("Need cobblestone and oak planks in your hotbar for the Town Heart.");
                return;
            }

            if (!_session.Villages.TryFoundVillage(
                    _session.Grid,
                    "New Settlement",
                    BlueprintPreview.AnchorX,
                    BlueprintPreview.AnchorZ,
                    out _,
                    BlueprintPreview.AnchorY))
            {
                _showToast("Need flat open ground for the Town Heart.");
                return;
            }

            if (!_session.Player.CreativeMode)
            {
                blueprint.TryConsumeCosts(payer);
            }

            CancelBlueprintPlacement();
            _showToast("Settlement founded! Your settler is building the Town Heart.");
        }

        public void TickPendingPreview()
        {
            if (PendingBlueprintId != null
                && PlayerStructureRegistry.TryGet(PendingBlueprintId, out var placementBlueprint))
            {
                if (PendingFoundingPlacement)
                {
                    UpdateFoundingPlacementPreview(placementBlueprint);
                }
                else
                {
                    var placementVillage = _session.Villages.GetActiveVillage(_session.Player.Position);
                    if (placementVillage != null)
                    {
                        UpdateBlueprintPlacementPreview(placementVillage, placementBlueprint);
                    }
                    else
                    {
                        CancelBlueprintPlacement();
                    }
                }
            }
            else if (WorkZonePlacementActive)
            {
                var zoneVillage = _session.Villages.GetActiveVillage(_session.Player.Position);
                if (zoneVillage != null)
                {
                    UpdateWorkZonePlacementPreview(zoneVillage);
                }
                else
                {
                    CancelWorkZonePlacement();
                }
            }
        }

        public bool TryCancelOnEscape()
        {
            if (PendingBlueprintId != null)
            {
                CancelBlueprintPlacement();
                return true;
            }

            if (WorkZonePlacementActive)
            {
                CancelWorkZonePlacement();
                return true;
            }

            return false;
        }

        private void UpdateBlueprintPlacementPreview(BuildingBlueprint blueprint, Village.Village? village)
        {
            var resolved = BlueprintPlacementHelper.ResolveFromLook(
                _session.Grid,
                _camera.Position,
                _camera.Front,
                BlockInteractionSystem.RaycastRange);

            if (!resolved.HasHit)
            {
                resolved = BlueprintPlacementHelper.ResolveFallbackNearPlayer(
                    _session.Grid,
                    _session.Player.Position,
                    _camera.Front);
            }

            bool valid = false;
            if (resolved.HasHit)
            {
                if (PendingFoundingPlacement)
                {
                    valid = _session.Villages.CanPlaceTownHeart(
                        _session.Grid,
                        resolved.AnchorX,
                        resolved.AnchorY,
                        resolved.AnchorZ,
                        _hotbarProvider());
                }
                else if (village != null)
                {
                    var payer = village.Storage.HasSpaceFor(ItemStack.CreateBlock(BlockType.Dirt, 1))
                        ? (IItemContainer)village.Storage
                        : _hotbarProvider();
                    valid = _session.Villages.CanPlaceBlueprint(
                        _session.Grid,
                        village,
                        blueprint,
                        resolved.AnchorX,
                        resolved.AnchorZ,
                        payer,
                        resolved.AnchorY);
                }
            }

            BlueprintPreview = new BlueprintPlacementPreview
            {
                Blueprint = blueprint,
                AnchorX = resolved.AnchorX,
                AnchorY = resolved.AnchorY,
                AnchorZ = resolved.AnchorZ,
                Valid = valid
            };
        }

        private void UpdateFoundingPlacementPreview(BuildingBlueprint blueprint) =>
            UpdateBlueprintPlacementPreview(blueprint, null);

        private void UpdateBlueprintPlacementPreview(Village.Village village, BuildingBlueprint blueprint) =>
            UpdateBlueprintPlacementPreview(blueprint, village);

        public void PopulateConstructionSitePreviews(GameRenderContext renderContext, VillageManager villages, Vector3 playerPos)
        {
            _constructionSitePreviews.Clear();
            var village = villages.GetActiveVillage(playerPos);
            if (village == null)
            {
                renderContext.PendingConstructionSites = null;
                return;
            }

            foreach (var site in village.BuildingSites)
            {
                if (site.IsComplete)
                {
                    continue;
                }

                if (!PlayerStructureRegistry.TryGet(site.BlueprintId, out var blueprint))
                {
                    continue;
                }

                _constructionSitePreviews.Add(new BlueprintPlacementPreview
                {
                    Blueprint = blueprint,
                    AnchorX = site.AnchorX,
                    AnchorY = site.AnchorY,
                    AnchorZ = site.AnchorZ,
                    Valid = true,
                    IsQueuedConstruction = true
                });
            }

            renderContext.PendingConstructionSites = _constructionSitePreviews.Count > 0
                ? _constructionSitePreviews
                : null;
        }

        public void StartWorkZonePlacement(Action closeVillageUi)
        {
            closeVillageUi();
            WorkZonePlacementActive = true;
            WorkZoneCornerA = null;
            WorkZonePreview = new WorkZonePlacementPreview
            {
                HasFirstCorner = false,
                Valid = false
            };
        }

        public void CancelWorkZonePlacement()
        {
            WorkZonePlacementActive = false;
            WorkZoneCornerA = null;
            WorkZonePreview = null;
        }

        public void ConfirmWorkZoneCorner(Village.Village village, int x, int y, int z)
        {
            if (!WorkZoneCornerA.HasValue)
            {
                WorkZoneCornerA = (x, y, z);
                UpdateWorkZonePreview(village, x, y, z);
                _showToast("Select opposite corner.");
                return;
            }

            var start = WorkZoneCornerA.Value;
            _session.Villages.TryMarkWorkZone(
                _session.Grid,
                village,
                start.X,
                start.Y,
                start.Z,
                x,
                y,
                z,
                out string message);
            _showToast(message);
            CancelWorkZonePlacement();
        }

        private void UpdateWorkZonePreview(Village.Village village, int bx, int by, int bz)
        {
            if (!WorkZoneCornerA.HasValue)
            {
                WorkZonePreview = new WorkZonePlacementPreview
                {
                    HasFirstCorner = false,
                    Valid = false
                };
                return;
            }

            var start = WorkZoneCornerA.Value;
            var bounds = GatherWorkQueue.NormalizeBounds(start.X, start.Y, start.Z, bx, by, bz);
            WorkZonePreview = new WorkZonePlacementPreview
            {
                MinX = bounds.minX,
                MinY = bounds.minY,
                MinZ = bounds.minZ,
                MaxX = bounds.maxX,
                MaxY = bounds.maxY,
                MaxZ = bounds.maxZ,
                HasFirstCorner = true,
                Valid = _session.Villages.CanMarkWorkZone(
                    village,
                    start.X,
                    start.Y,
                    start.Z,
                    bx,
                    by,
                    bz)
            };
        }

        private void UpdateWorkZonePlacementPreview(Village.Village village)
        {
            var (hitBlockPos, _, _, _) = BlockInteractionSystem.RaycastSolid(
                _session.Grid,
                _camera.Position,
                _camera.Front,
                BlockInteractionSystem.RaycastRange);

            if (!hitBlockPos.HasValue)
            {
                return;
            }

            int x = (int)MathF.Floor(hitBlockPos.Value.X);
            int y = (int)MathF.Floor(hitBlockPos.Value.Y);
            int z = (int)MathF.Floor(hitBlockPos.Value.Z);
            UpdateWorkZonePreview(village, x, y, z);
        }

        public void ApplyToRenderContext(GameRenderContext renderContext)
        {
            renderContext.BlueprintPlacement = BlueprintPreview;
            renderContext.WorkZonePlacement = WorkZonePreview;
            renderContext.HudPlacementHint = GetHudPlacementHint();
        }
    }
}
