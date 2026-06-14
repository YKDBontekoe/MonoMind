using System;
using System.Collections.Generic;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public sealed class VillagePersistence
    {
        private readonly VillagerManager _villagers;

        public VillagePersistence(VillagerManager villagers)
        {
            _villagers = villagers;
        }

        public void LoadFromSave(
            List<Village> villages,
            HashSet<int> finalizedSites,
            IEnumerable<VillageSaveData> villageData,
            IEnumerable<VillagerSaveData> villagerData)
        {
            villages.Clear();
            finalizedSites.Clear();

            var villageList = new List<VillageSaveData>(villageData);
            var villagerList = new List<VillagerSaveData>(villagerData);

            int maxVillageId = 0;
            foreach (var entry in villageList)
            {
                maxVillageId = Math.Max(maxVillageId, entry.Id);
            }

            int maxSiteId = 0;
            foreach (var entry in villageList)
            {
                foreach (var site in entry.BuildingSites)
                {
                    maxSiteId = Math.Max(maxSiteId, site.Id);
                }
            }

            int maxVillagerId = 0;
            foreach (var entry in villagerList)
            {
                maxVillagerId = Math.Max(maxVillagerId, entry.Id);
            }

            int maxGoalId = 0;
            foreach (var entry in villageList)
            {
                foreach (var goal in entry.Goals)
                {
                    maxGoalId = Math.Max(maxGoalId, goal.Id);
                }
            }

            Village.ResetIdCounter(maxVillageId + 1);
            BuildingSite.ResetIdCounter(maxSiteId + 1);
            Villager.ResetIdCounter(maxVillagerId + 1);
            JobScheduler.ResetGoalIdCounter(maxGoalId + 1);

            _villagers.LoadVillagers(villagerList);

            foreach (var entry in villageList)
            {
                var village = new Village(entry.Name, entry.AnchorX, entry.AnchorY, entry.AnchorZ, entry.StorageSlots, entry.Id);
                village.Tier = (VillageTier)entry.Tier;
                village.FoodStock = entry.FoodStock;
                village.Happiness = entry.Happiness;
                village.PopulationCap = entry.PopulationCap > 0 ? entry.PopulationCap : village.PopulationCap;
                village.HousingCapacity = entry.HousingCapacity;
                if (entry.Radius > 0f)
                {
                    village.Radius = entry.Radius;
                }

                for (int i = 0; i < entry.Storage.Count && i < village.Storage.SlotCount; i++)
                {
                    village.Storage.SetSlot(i, WorldSaveManager.DeserializeItemStack(entry.Storage[i]));
                }

                foreach (var building in entry.Buildings)
                {
                    village.RestoreBuilding(building);
                }

                foreach (var siteEntry in entry.BuildingSites)
                {
                    if (PlayerStructureRegistry.TryGet(siteEntry.BlueprintId, out var blueprint))
                    {
                        village.RestoreBuildingSite(siteEntry, blueprint);
                        if (siteEntry.IsComplete)
                        {
                            finalizedSites.Add(siteEntry.Id);
                        }
                    }
                }

                village.RestoreOutputChests(entry.OutputChests);

                foreach (int vid in entry.VillagerIds)
                {
                    village.RegisterVillager(vid);
                }

                village.WorkQueue.Restore(entry.WorkQueue);
                foreach (var goalEntry in entry.Goals)
                {
                    village.Scheduler.RestoreGoal(new VillageGoal
                    {
                        Id = goalEntry.Id,
                        Description = goalEntry.Description,
                        Priority = goalEntry.Priority,
                        Completed = goalEntry.Completed,
                        Kind = (VillageGoalKind)goalEntry.Kind,
                        StockBlock = goalEntry.StockBlock.HasValue ? (BlockType)goalEntry.StockBlock.Value : null,
                        TargetCount = goalEntry.TargetCount,
                        BlueprintId = goalEntry.BlueprintId,
                        BuildQueued = goalEntry.BuildQueued
                    });
                }

                village.UpdateTier();
                villages.Add(village);
            }
        }

        public List<VillageSaveData> ExportVillages(IReadOnlyList<Village> villages)
        {
            var result = new List<VillageSaveData>();
            foreach (var village in villages)
            {
                var storage = new List<InventorySlotSaveData>();
                for (int i = 0; i < village.Storage.SlotCount; i++)
                {
                    storage.Add(WorldSaveManager.SerializeItemStack(village.Storage.GetSlot(i)));
                }

                var buildings = new List<BuildingSaveData>();
                foreach (var building in village.Buildings)
                {
                    buildings.Add(new BuildingSaveData
                    {
                        Id = building.Id,
                        BlueprintId = building.BlueprintId,
                        Kind = (int)building.Kind,
                        AnchorX = building.AnchorX,
                        AnchorY = building.AnchorY,
                        AnchorZ = building.AnchorZ,
                        IsComplete = building.IsComplete
                    });
                }

                var sites = new List<BuildingSiteSaveData>();
                foreach (var site in village.BuildingSites)
                {
                    sites.Add(new BuildingSiteSaveData
                    {
                        Id = site.Id,
                        VillageId = site.VillageId,
                        BlueprintId = site.BlueprintId,
                        AnchorX = site.AnchorX,
                        AnchorY = site.AnchorY,
                        AnchorZ = site.AnchorZ,
                        IsComplete = site.IsComplete
                    });
                }

                var goals = new List<VillageGoalSaveData>();
                foreach (var goal in village.Scheduler.Goals)
                {
                    goals.Add(new VillageGoalSaveData
                    {
                        Id = goal.Id,
                        Description = goal.Description,
                        Priority = goal.Priority,
                        Completed = goal.Completed,
                        Kind = (int)goal.Kind,
                        StockBlock = goal.StockBlock.HasValue ? (int)goal.StockBlock.Value : null,
                        TargetCount = goal.TargetCount,
                        BlueprintId = goal.BlueprintId,
                        BuildQueued = goal.BuildQueued
                    });
                }

                result.Add(new VillageSaveData
                {
                    Id = village.Id,
                    Name = village.Name,
                    AnchorX = village.AnchorX,
                    AnchorY = village.AnchorY,
                    AnchorZ = village.AnchorZ,
                    Tier = (int)village.Tier,
                    FoodStock = village.FoodStock,
                    Happiness = village.Happiness,
                    Radius = village.Radius,
                    StorageSlots = village.Storage.SlotCount,
                    PopulationCap = village.PopulationCap,
                    HousingCapacity = village.HousingCapacity,
                    Storage = storage,
                    VillagerIds = new List<int>(village.VillagerIds),
                    Buildings = buildings,
                    BuildingSites = sites,
                    WorkQueue = village.WorkQueue.Export(),
                    Goals = goals,
                    OutputChests = village.ExportOutputChests()
                });
            }

            return result;
        }
    }
}
