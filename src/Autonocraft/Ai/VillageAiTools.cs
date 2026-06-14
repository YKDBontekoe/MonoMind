using System.Numerics;
using System.Text.Json;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.Ai
{
    public static class VillageAiTools
    {
        public static (bool success, string message) ExecuteTool(
            string name,
            string argsJson,
            Autonocraft.Village.VillageManager villageManager,
            VillagerManager villagerManager,
            VillageEntity village,
            IItemContainer? payer = null)
        {
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
                var root = doc.RootElement;

                return name switch
                {
                    "get_village_summary" => (true, VillageContextBuilder.BuildSummary(villageManager, village, villagerManager)),
                    "list_villagers" => ListVillagers(villagerManager, village),
                    "assign_job" => AssignJob(villageManager, villagerManager, village, root),
                    "recruit_villager" => Recruit(villageManager, village),
                    "queue_build" => QueueBuild(villageManager, villagerManager, village, root, payer),
                    "mark_resource" => MarkResource(villagerManager, village, root),
                    "cancel_job" => CancelJob(village, villagerManager, root),
                    "set_village_goal" => SetVillageGoal(village, root),
                    _ => (false, $"Unknown tool '{name}'.")
                };
            }
            catch (JsonException ex)
            {
                return (false, $"Invalid tool args JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static (bool success, string message) ListVillagers(VillagerManager villagerManager, VillageEntity village)
        {
            var lines = new List<string>();
            foreach (int villagerId in village.VillagerIds)
            {
                if (!villagerManager.TryGet(villagerId, out var villager))
                {
                    continue;
                }

                lines.Add($"{villager.Id}: {villager.Name} ({villager.Role}, {villager.CurrentJob})");
            }

            if (lines.Count == 0)
            {
                return (true, "No villagers in this village.");
            }

            return (true, string.Join("\n", lines));
        }

        private static (bool success, string message) AssignJob(
            Autonocraft.Village.VillageManager villageManager,
            VillagerManager villagerManager,
            VillageEntity village,
            JsonElement root)
        {
            if (!TryGetInt(root, "villager_id", out int villagerId) ||
                !villagerManager.TryGet(villagerId, out var villager))
            {
                return (false, "Villager not found.");
            }

            if (!TryParseJob(root, out JobType job))
            {
                return (false, "Invalid job type.");
            }

            Vector3? target = null;
            if (TryGetInt(root, "target_x", out int tx) &&
                TryGetInt(root, "target_y", out int ty) &&
                TryGetInt(root, "target_z", out int tz))
            {
                target = new Vector3(tx + 0.5f, ty, tz + 0.5f);
            }

            int? buildingSiteId = TryGetInt(root, "building_site_id", out int siteId) ? siteId : null;

            if (!villageManager.TryAssignJob(village, villager, job, target, buildingSiteId))
            {
                return (false, $"Could not assign {job} to {villager.Name}.");
            }

            return (true, $"Assigned {villager.Name} to {job}.");
        }

        private static (bool success, string message) Recruit(Autonocraft.Village.VillageManager villageManager, VillageEntity village)
        {
            if (!villageManager.TryRecruit(village))
            {
                return (false, "Cannot recruit: at population cap or need 4 oak planks in storage.");
            }

            return (true, "A new villager joined the village.");
        }

        private static (bool success, string message) QueueBuild(
            Autonocraft.Village.VillageManager villageManager,
            VillagerManager villagerManager,
            VillageEntity village,
            JsonElement root,
            IItemContainer? payer)
        {
            if (payer == null)
            {
                return (false, "No inventory available to pay build costs.");
            }

            if (!root.TryGetProperty("blueprint_id", out var blueprintNode) ||
                blueprintNode.ValueKind != JsonValueKind.String)
            {
                return (false, "blueprint_id is required.");
            }

            if (!TryGetInt(root, "anchor_x", out int anchorX) || !TryGetInt(root, "anchor_z", out int anchorZ))
            {
                return (false, "anchor_x and anchor_z are required.");
            }

            VoxelWorld? world = villagerManager.World;
            if (world == null)
            {
                return (false, "World is not available.");
            }

            string blueprintId = blueprintNode.GetString() ?? string.Empty;
            if (!villageManager.TryQueueBlueprint(world, village, blueprintId, anchorX, anchorZ, payer))
            {
                return (false, $"Could not queue blueprint '{blueprintId}'.");
            }

            return (true, $"Queued {blueprintId} at ({anchorX}, {anchorZ}).");
        }

        private static (bool success, string message) MarkResource(
            VillagerManager villagerManager,
            VillageEntity village,
            JsonElement root)
        {
            if (!TryGetInt(root, "villager_id", out int villagerId) ||
                !villagerManager.TryGet(villagerId, out var villager) ||
                villager.VillageId != village.Id)
            {
                return (false, "Villager not found in this village.");
            }

            if (!TryGetInt(root, "x", out int x) ||
                !TryGetInt(root, "y", out int y) ||
                !TryGetInt(root, "z", out int z))
            {
                return (false, "x, y, and z are required.");
            }

            villager.MarkedResource = new Vector3(x + 0.5f, y, z + 0.5f);
            if (villager.CurrentJob == JobType.Idle)
            {
                villager.AssignJob(JobType.Gather, villager.MarkedResource, null);
            }

            return (true, $"Marked resource at ({x}, {y}, {z}) for {villager.Name}.");
        }

        private static (bool success, string message) CancelJob(
            VillageEntity village,
            VillagerManager villagerManager,
            JsonElement root)
        {
            if (!TryGetInt(root, "villager_id", out int villagerId) ||
                !villagerManager.TryGet(villagerId, out var villager))
            {
                return (false, "Villager not found.");
            }

            village.Scheduler.CancelJob(villager);
            return (true, $"Cancelled job for {villager.Name}.");
        }

        private static (bool success, string message) SetVillageGoal(VillageEntity village, JsonElement root)
        {
            if (!root.TryGetProperty("description", out var descNode) ||
                descNode.ValueKind != JsonValueKind.String)
            {
                return (false, "description is required.");
            }

            int priority = TryGetInt(root, "priority", out int p) ? p : 0;
            string description = descNode.GetString() ?? string.Empty;
            int goalId = village.Scheduler.AddGoal(description, priority);
            return (true, $"Set village goal #{goalId}: {description}");
        }

        private static bool TryParseJob(JsonElement root, out JobType job)
        {
            job = JobType.Idle;
            if (!root.TryGetProperty("job", out var jobNode) || jobNode.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            string raw = jobNode.GetString() ?? string.Empty;
            return Enum.TryParse(raw, ignoreCase: true, out job);
        }

        private static bool TryGetInt(JsonElement root, string name, out int value)
        {
            value = 0;
            if (!root.TryGetProperty(name, out var node))
            {
                return false;
            }

            if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out value))
            {
                return true;
            }

            return node.ValueKind == JsonValueKind.String && int.TryParse(node.GetString(), out value);
        }
    }
}
