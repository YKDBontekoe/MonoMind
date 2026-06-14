using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Village
{
    public sealed class VillageFoundingService
    {
        private readonly VillagerManager _villagers;
        private readonly HashSet<long> _claimedAnchors;
        private int _worldSeed;

        public Action<string>? ShowToast { get; set; }

        public VillageFoundingService(VillagerManager villagers, HashSet<long> claimedAnchors)
        {
            _villagers = villagers;
            _claimedAnchors = claimedAnchors;
        }

        public void SetWorldSeed(int seed) => _worldSeed = seed;

        public (int spawnX, int spawnZ) InitializeStarterSettlement(
            List<Village> villages,
            VoxelWorld world,
            int nearX,
            int nearZ,
            JobDispatcher dispatcher)
        {
            const string villageName = "Founder's Hamlet";
            int heartX = nearX + 4;
            int heartZ = nearZ;
            int heartY = StructureFingerprint.FindSurfaceAnchorY(world, heartX, heartZ);

            if (!PlayerStructureRegistry.TryGet("town_heart", out var blueprint))
            {
                return (nearX, nearZ);
            }

            ClearFootprint(world, blueprint, heartX, heartY, heartZ);
            PlaceBlueprintBlocks(world, blueprint, heartX, heartY, heartZ);

            var village = new Village(villageName, heartX, heartY, heartZ, blueprint.StorageSlots);
            villages.Add(village);
            village.RegisterClaimedBuilding(blueprint, heartX, heartY, heartZ);
            village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 16));
            village.Storage.AddItem(ItemStack.CreateBlock(BlockType.Cobblestone, 8));
            village.Storage.AddItem(ToolRegistry.CreateStack(ToolType.Axe, ToolTier.Wood));
            village.Storage.AddItem(ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood));
            village.FoodStock = 6f;

            var spawnPos = village.Center + new Vector3(-2.5f, 0f, 0.5f);
            var lumberjack = _villagers.Spawn(village.Id, spawnPos, _worldSeed ^ 1);
            lumberjack.Role = VillagerRole.Lumberjack;
            village.RegisterVillager(lumberjack.Id);

            var peasant = _villagers.Spawn(village.Id, spawnPos + new Vector3(1.5f, 0f, 1f), _worldSeed ^ 2);
            peasant.Role = VillagerRole.Hauler;
            village.RegisterVillager(peasant.Id);

            var gatherTarget = JobTargetScanner.FindNearbyLumberTarget(world, village, null, spawnPos);
            if (gatherTarget.HasValue)
            {
                dispatcher.TryAssignJob(village, lumberjack, JobType.Lumber, gatherTarget);
            }

            RecordClaimedAnchor(heartX, heartZ);
            ShowToast?.Invoke($"Welcome to {villageName}! Press V for the town board.");
            return (nearX, nearZ);
        }

        public bool TryFoundVillage(List<Village> villages, VoxelWorld world, string name, int anchorX, int anchorZ, out Village? village)
        {
            village = null;
            int anchorY = StructureFingerprint.FindSurfaceAnchorY(world, anchorX, anchorZ);
            if (!PlayerStructureRegistry.TryGet("town_heart", out var blueprint))
            {
                return false;
            }

            foreach (var block in blueprint.Template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                if (world.GetBlock(wx, wy, wz) != BlockType.Air)
                {
                    ShowToast?.Invoke("Not enough space for Town Heart.");
                    return false;
                }
            }

            village = new Village(name, anchorX, anchorY, anchorZ, blueprint.StorageSlots);
            villages.Add(village);
            village.QueueBuild(blueprint, anchorX, anchorY, anchorZ);
            ShowToast?.Invoke($"Founded '{name}'. Open BUILDINGS tab — builders will finish the Town Heart.");
            return true;
        }

        public bool TryFindClaimableStructure(
            VoxelWorld world,
            Vector3 playerPos,
            float radius,
            out int anchorX,
            out int anchorZ,
            out StructureDefinition? matched,
            bool quickScan = false)
        {
            anchorX = 0;
            anchorZ = 0;
            matched = null;
            int px = (int)MathF.Floor(playerPos.X);
            int pz = (int)MathF.Floor(playerPos.Z);
            float effectiveRadius = quickScan ? Math.Min(radius, 16f) : radius;
            int scanRadius = (int)MathF.Ceiling(effectiveRadius);
            int step = quickScan ? 4 : 2;
            float bestDist = float.MaxValue;

            for (int dx = -scanRadius; dx <= scanRadius; dx += step)
            {
                for (int dz = -scanRadius; dz <= scanRadius; dz += step)
                {
                    int ax = px + dx;
                    int az = pz + dz;
                    if (IsAnchorClaimed(ax, az))
                    {
                        continue;
                    }

                    if (!TryResolveClaimableMatch(world, ax, az, out var definition, out _, out _, quickScan))
                    {
                        continue;
                    }

                    if (!ClaimableStructureMap.IsClaimable(definition.Id))
                    {
                        continue;
                    }

                    float dist = Vector2.DistanceSquared(
                        new Vector2(playerPos.X, playerPos.Z),
                        new Vector2(ax + 0.5f, az + 0.5f));
                    if (dist > radius * radius || dist >= bestDist)
                    {
                        continue;
                    }

                    bestDist = dist;
                    anchorX = ax;
                    anchorZ = az;
                    matched = definition;
                }
            }

            return matched != null;
        }

        public bool TryClaimStructure(List<Village> villages, VoxelWorld world, int anchorX, int anchorZ, out Village? village)
        {
            village = null;
            if (IsAnchorClaimed(anchorX, anchorZ))
            {
                ShowToast?.Invoke("This outpost is already claimed.");
                return false;
            }

            if (!TryResolveClaimableMatch(world, anchorX, anchorZ, out var matched, out float ratio, out int anchorY))
            {
                ShowToast?.Invoke("No recognizable structure to claim.");
                return false;
            }

            string blueprintId = ClaimableStructureMap.GetBlueprintId(matched.Id);
            if (!PlayerStructureRegistry.TryGet(blueprintId, out var blueprint))
            {
                return false;
            }

            string name = ClaimableStructureMap.GetDefaultVillageName(matched.Id);
            village = new Village(name, anchorX, anchorY, anchorZ);
            villages.Add(village);
            village.RegisterClaimedBuilding(blueprint, anchorX, anchorY, anchorZ);
            village.FoodStock = 3f;
            village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));

            var spawn = village.Center + new Vector3(0.5f, 0f, 0.5f);
            var villager = _villagers.Spawn(village.Id, spawn, _worldSeed ^ (anchorX * 92821 + anchorZ));
            villager.Role = VillagerRole.Peasant;
            village.RegisterVillager(villager.Id);

            RecordClaimedAnchor(anchorX, anchorZ);
            ShowToast?.Invoke($"Claimed {matched.Id} ({ratio:P0} intact) as '{name}'.");
            return true;
        }

        public bool TryClaimAtBlock(List<Village> villages, VoxelWorld world, int blockX, int blockY, int blockZ)
        {
            for (int dx = -6; dx <= 6; dx += 2)
            {
                for (int dz = -6; dz <= 6; dz += 2)
                {
                    int ax = blockX + dx;
                    int az = blockZ + dz;
                    if (TryClaimStructure(villages, world, ax, az, out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void RecordClaimedAnchor(int anchorX, int anchorZ) =>
            _claimedAnchors.Add(PackAnchor(anchorX, anchorZ));

        public bool IsAnchorClaimed(int anchorX, int anchorZ) =>
            _claimedAnchors.Contains(PackAnchor(anchorX, anchorZ));

        public List<ClaimedAnchorSaveData> ExportClaimedAnchors()
        {
            var result = new List<ClaimedAnchorSaveData>();
            foreach (long packed in _claimedAnchors)
            {
                result.Add(new ClaimedAnchorSaveData
                {
                    X = (int)(packed >> 32),
                    Z = (int)(packed & 0xFFFFFFFF)
                });
            }

            return result;
        }

        public void LoadClaimedAnchors(IEnumerable<ClaimedAnchorSaveData>? anchors)
        {
            _claimedAnchors.Clear();
            if (anchors == null)
            {
                return;
            }

            foreach (var anchor in anchors)
            {
                RecordClaimedAnchor(anchor.X, anchor.Z);
            }
        }

        private static void ClearFootprint(VoxelWorld world, BuildingBlueprint blueprint, int anchorX, int anchorY, int anchorZ)
        {
            foreach (var block in blueprint.Template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                world.SetBlock(wx, wy, wz, BlockType.Air);
            }
        }

        private static void PlaceBlueprintBlocks(VoxelWorld world, BuildingBlueprint blueprint, int anchorX, int anchorY, int anchorZ)
        {
            foreach (var block in blueprint.Template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                world.SetBlock(wx, wy, wz, block.Type);
            }
        }

        private static bool TryResolveClaimableMatch(
            VoxelWorld world,
            int anchorX,
            int anchorZ,
            out StructureDefinition matched,
            out float matchRatio,
            out int anchorY,
            bool quickScan = false)
        {
            matched = StructureRegistry.All[0];
            matchRatio = 0f;
            anchorY = 0;
            int surfaceSearch = quickScan ? 1 : 8;
            int surfaceY = StructureFingerprint.FindSurfaceAnchorY(world, anchorX, anchorZ, surfaceSearch);
            float bestRatio = 0f;
            StructureDefinition? best = null;
            int bestY = surfaceY - 1;

            for (int y = surfaceY - 4; y <= surfaceY + 1; y++)
            {
                if (!StructureFingerprint.TryMatchWorldStructure(world, anchorX, y, anchorZ, out var candidate, out float ratio))
                {
                    continue;
                }

                if (!ClaimableStructureMap.IsClaimable(candidate.Id) || ratio <= bestRatio)
                {
                    continue;
                }

                bestRatio = ratio;
                best = candidate;
                bestY = y;
            }

            if (best == null)
            {
                return false;
            }

            matched = best;
            matchRatio = bestRatio;
            anchorY = bestY;
            return true;
        }

        private static long PackAnchor(int anchorX, int anchorZ) =>
            ((long)anchorX << 32) | (uint)anchorZ;
    }
}
