using System.Text.Json;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.Ai
{
    public static class VillageContextBuilder
    {
        public static string BuildSummary(Autonocraft.Village.VillageManager villageManager, VillageEntity village, VillagerManager villagerManager)
        {
            var villagers = new List<object>();
            foreach (int villagerId in village.VillagerIds)
            {
                if (!villagerManager.TryGet(villagerId, out var villager))
                {
                    continue;
                }

                villagers.Add(new
                {
                    id = villager.Id,
                    name = villager.Name,
                    role = villager.Role.ToString(),
                    job = villager.CurrentJob.ToString(),
                    happiness = villager.Happiness,
                    trait = villager.Persona.Trait,
                    skills = new
                    {
                        mining = villager.Skills.Mining.Level,
                        woodcutting = villager.Skills.Woodcutting.Level,
                        farming = villager.Skills.Farming.Level
                    }
                });
            }

            var storage = new List<object>();
            for (int i = 0; i < village.Storage.SlotCount; i++)
            {
                var stack = village.Storage.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                storage.Add(new
                {
                    slot = i,
                    kind = stack.Kind.ToString(),
                    type = stack.Kind switch
                    {
                        ItemKind.Block => stack.BlockType.ToString(),
                        ItemKind.Tool => stack.ToolId.ToString(),
                        _ => "empty"
                    },
                    count = stack.Count
                });
            }

            var goals = new List<object>();
            foreach (var goal in village.Scheduler.Goals)
            {
                var entry = new Dictionary<string, object?>
                {
                    ["id"] = goal.Id,
                    ["description"] = goal.Description,
                    ["priority"] = goal.Priority,
                    ["completed"] = goal.Completed,
                    ["kind"] = goal.Kind.ToString()
                };

                if (goal.Kind == VillageGoalKind.Stock && goal.StockBlock.HasValue)
                {
                    entry["block_type"] = goal.StockBlock.Value.ToString();
                    entry["target_count"] = goal.TargetCount;
                    entry["current_count"] = village.Scheduler.GetStockProgress(goal, village);
                }
                else if (goal.Kind == VillageGoalKind.Build && !string.IsNullOrEmpty(goal.BlueprintId))
                {
                    entry["blueprint_id"] = goal.BlueprintId;
                    entry["build_queued"] = goal.BuildQueued;
                    entry["building_complete"] = village.HasCompletedBuilding(goal.BlueprintId);
                }

                goals.Add(entry);
            }

            var buildingSites = new List<object>();
            foreach (var site in village.BuildingSites)
            {
                buildingSites.Add(new
                {
                    id = site.Id,
                    blueprint_id = site.BlueprintId,
                    anchor_x = site.AnchorX,
                    anchor_y = site.AnchorY,
                    anchor_z = site.AnchorZ,
                    complete = site.IsComplete
                });
            }

            var payload = new
            {
                village = new
                {
                    id = village.Id,
                    name = village.Name,
                    tier = village.Tier.ToString(),
                    population = village.Population,
                    population_cap = village.PopulationCap,
                    housing_capacity = village.HousingCapacity,
                    food_stock = village.FoodStock,
                    happiness = village.Happiness,
                    anchor_x = village.AnchorX,
                    anchor_y = village.AnchorY,
                    anchor_z = village.AnchorZ,
                    radius = village.Radius
                },
                villagers,
                storage,
                goals,
                building_sites = buildingSites,
                work_queue = village.WorkQueue.Count
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
        }
    }
}
