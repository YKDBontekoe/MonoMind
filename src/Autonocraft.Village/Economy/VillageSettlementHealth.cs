using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Domain.World;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public static class VillageSettlementHealth
    {
        public static int GetLivePopulation(Village village, VillagerManager villagers)
        {
            int count = 0;
            foreach (var villager in villagers.All)
            {
                if (villager.VillageId == village.Id)
                {
                    count++;
                }
            }

            return count;
        }

        public static void SyncPopulationRegistry(Village village, VillagerManager villagers)
        {
            village.ReconcileVillagerRegistry(villagers.All);
        }

        public static IEnumerable<Villager> EnumerateLiveCitizens(Village village, VillagerManager villagers)
        {
            foreach (var villager in villagers.All)
            {
                if (villager.VillageId == village.Id)
                {
                    yield return villager;
                }
            }
        }

        public static int CountLiveCitizens(Village village, VillagerManager villagers)
        {
            int count = 0;
            foreach (var _ in EnumerateLiveCitizens(village, villagers))
            {
                count++;
            }

            return count;
        }

        public static void AdoptNearbyOrphanedCitizens(
            Village village,
            VillagerManager villagers,
            IReadOnlyList<Village> allVillages)
        {
            foreach (var villager in villagers.All)
            {
                if (villager.VillageId == village.Id)
                {
                    continue;
                }

                if (!IsNearTownHeart(villager.Position, village, 36f) && !village.Contains(villager.Position))
                {
                    continue;
                }

                if (!TryClaimOrphan(villager, village, allVillages))
                {
                    continue;
                }

                villager.VillageId = village.Id;
                village.RegisterVillager(villager.Id);
            }
        }

        private static bool TryClaimOrphan(Villager villager, Village village, IReadOnlyList<Village> allVillages)
        {
            var assigned = FindVillage(allVillages, villager.VillageId);
            if (assigned == null)
            {
                return true;
            }

            if (assigned.Id == village.Id)
            {
                return true;
            }

            float assignedDist = HorizontalDistanceSquared(villager.Position, assigned);
            float hereDist = HorizontalDistanceSquared(villager.Position, village);
            return hereDist < assignedDist;
        }

        public static bool HasEstablishedSettlement(Village village)
        {
            if (village.HasBuilding(BuildingKind.TownHeart))
            {
                return true;
            }

            if (village.HasPendingOrCompleteBuilding("town_heart"))
            {
                return true;
            }

            if (village.Buildings.Count > 0)
            {
                return true;
            }

            if (village.BuildingSites.Count > 0)
            {
                return true;
            }

            if (village.Storage.CountBlock(BlockType.OakPlank) >= Village.RecruitFoodCost)
            {
                return true;
            }

            return village.FoodStock > 0f;
        }

        public static bool NeedsCitizenRepair(Village village, VillagerManager villagers)
        {
            if (GetLivePopulation(village, villagers) > 0)
            {
                return false;
            }

            return HasEstablishedSettlement(village);
        }

        public static int CountStrandedCitizens(Village village, VillagerManager villagers, float maxDistance = 48f)
        {
            int count = 0;
            float maxDistSq = maxDistance * maxDistance;
            foreach (var villager in villagers.All)
            {
                if (villager.VillageId == village.Id)
                {
                    continue;
                }

                if (HorizontalDistanceSquared(villager.Position, village) <= maxDistSq || village.Contains(villager.Position))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// When a settlement shows zero citizens but villagers are physically present, attach them to this village.
        /// </summary>
        public static int RelinkStrandedCitizens(
            Village village,
            VillagerManager villagers,
            IReadOnlyList<Village> allVillages)
        {
            if (GetLivePopulation(village, villagers) > 0)
            {
                return 0;
            }

            var populationByVillageId = new Dictionary<int, int>();
            foreach (var otherVillage in allVillages)
            {
                populationByVillageId[otherVillage.Id] = GetLivePopulation(otherVillage, villagers);
            }

            int linked = 0;
            foreach (var villager in villagers.All)
            {
                if (villager.VillageId == village.Id)
                {
                    continue;
                }

                if (!IsNearTownHeart(villager.Position, village, 48f) && !village.Contains(villager.Position))
                {
                    continue;
                }

                var assigned = FindVillage(allVillages, villager.VillageId);
                if (assigned != null && assigned.Id != village.Id)
                {
                    populationByVillageId.TryGetValue(assigned.Id, out int assignedPop);
                    float assignedDist = HorizontalDistanceSquared(villager.Position, assigned);
                    float hereDist = HorizontalDistanceSquared(villager.Position, village);
                    if (assignedPop > 0 && assignedDist <= hereDist)
                    {
                        continue;
                    }
                }

                villager.VillageId = village.Id;
                village.RegisterVillager(villager.Id);
                linked++;
            }

            return linked;
        }

        public static bool IsPlayerManagingSettlement(Village village, Vector3 playerPos)
            => village.Contains(playerPos) || IsPlayerNearTownHeart(village, playerPos, 32f);

        public static bool IsPlayerNearTownHeart(Village village, Vector3 playerPos, float maxHorizontalDistance = 16f)
            => IsNearTownHeart(playerPos, village, maxHorizontalDistance);

        public static void EnsureVillageChunksLoaded(VoxelWorld world, Village village)
        {
            world.EnsureChunksLoaded(village.Center, chunkRadius: 2);
        }

        private static bool IsNearTownHeart(Vector3 position, Village village, float maxHorizontalDistance)
        {
            float dx = position.X - (village.AnchorX + 0.5f);
            float dz = position.Z - (village.AnchorZ + 0.5f);
            return dx * dx + dz * dz <= maxHorizontalDistance * maxHorizontalDistance;
        }

        private static float HorizontalDistanceSquared(Vector3 position, Village village)
        {
            float dx = position.X - (village.AnchorX + 0.5f);
            float dz = position.Z - (village.AnchorZ + 0.5f);
            return dx * dx + dz * dz;
        }

        private static Village? FindVillage(IReadOnlyList<Village> villages, int villageId)
        {
            foreach (var village in villages)
            {
                if (village.Id == villageId)
                {
                    return village;
                }
            }

            return null;
        }
    }
}
