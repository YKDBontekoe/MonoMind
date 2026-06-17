using Autonocraft.Ai;
using Autonocraft.Domain.World;
using Autonocraft.Items;
using Autonocraft.Village;

namespace Autonocraft.Core.Agent.Serialization;

internal static class AgentStateSerializer
{
    public static AgentStateDto BuildStateDto(IGameAgentBridge bridge)
    {
        var host = bridge.Host;
        var session = host.Session;
        var player = session.Player;
        var interaction = session.BlockInteraction;
        var primaryVillage = session.Villages.GetActiveVillage(player.Position);
        string guidanceHint = EarlyGameGuide.GetGuidanceHint(player, primaryVillage, session.Villagers);

        string? nearbyStation = null;
        var target = session.BlockInteraction.TargetBlockType;
        if (target.IsStation())
        {
            nearbyStation = target switch
            {
                BlockType.StationForge => "Forge",
                BlockType.StationCrucible => "Crucible",
                BlockType.StationBench => "Bench",
                _ => target.ToString()
            };
        }

        var unlocked = session.Crafting.Journal.Export();
        var animalDtos = BuildAnimalDtos(session, player);
        var targetBlockDto = BuildTargetBlockDto(interaction);
        var village = session.Villages.GetActiveVillage(player.Position);
        var settings = host.Settings;

        AgentVillageSummaryDto? villageDto = null;
        List<Entities.Villager> nearbyVillagers = new();
        if (village != null)
        {
            session.Villages.SyncCitizensForVillage(village);
            int livePopulation = VillageSettlementHealth.GetLivePopulation(village, session.Villagers);
            villageDto = new AgentVillageSummaryDto(
                village.Id,
                village.Name,
                livePopulation,
                village.PopulationCap,
                village.Tier.ToString(),
                (float)Math.Round(village.Happiness, 2),
                (float)Math.Round(village.FoodStock, 1),
                village.AnchorX,
                village.AnchorZ);

            foreach (var v in VillageSettlementHealth.EnumerateLiveCitizens(village, session.Villagers))
            {
                nearbyVillagers.Add(v);
            }
        }
        else
        {
            nearbyVillagers = session.Villagers.GetVillagersInRange(player.Position, 32f);
        }

        var villagerDtos = BuildVillagerDtos(nearbyVillagers);
        var chatTarget = session.Villagers.GetNearest(player.Position, 5f);
        AgentNearbyVillagerDto? nearbyVillagerDto = chatTarget == null
            ? null
            : new AgentNearbyVillagerDto(chatTarget.Id, chatTarget.Name);

        var hotbar = BuildHotbar(player);
        var skills = new AgentSkillsDto(
            new AgentSkillDto(player.Skills.Mining.Level, player.Skills.Mining.Xp),
            new AgentSkillDto(player.Skills.Woodcutting.Level, player.Skills.Woodcutting.Xp),
            new AgentSkillDto(player.Skills.Combat.Level, player.Skills.Combat.Xp));

        return new AgentStateDto(
            bridge.CurrentGameState.ToString(),
            session.Grid.Seed,
            player.Oxygen,
            new AgentVector3Dto(player.Position.X, player.Position.Y, player.Position.Z),
            new AgentVector3Dto(player.Velocity.X, player.Velocity.Y, player.Velocity.Z),
            player.Yaw,
            player.Pitch,
            player.CreativeMode,
            player.IsGrounded,
            player.Health,
            player.MaxHealth,
            player.Hunger,
            player.MaxHunger,
            player.Stats.EarlyGuideStage,
            guidanceHint,
            host.TimeOfDay,
            host.TimeScale,
            host.TimePaused,
            player.SelectedSlot,
            hotbar,
            skills,
            animalDtos,
            targetBlockDto,
            nearbyStation,
            unlocked,
            settings.PlayWithAi,
            settings.AiProvider.ToString(),
            LlmClientFactory.IsAvailable(settings),
            villageDto,
            villagerDtos,
            nearbyVillagerDto);
    }

    public static AgentVillageDebugDto BuildVillageDebugDto(GameSession session, Autonocraft.Village.Village village)
    {
        session.Villages.SyncCitizensForVillage(village);

        var villagers = new List<AgentVillageDebugVillagerDto>();
        foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, session.Villagers))
        {
            villagers.Add(new AgentVillageDebugVillagerDto(
                villager.Id,
                villager.VillageId,
                villager.Name,
                villager.Role.ToString(),
                villager.CurrentJob.ToString(),
                villager.AiPhase.ToString(),
                villager.Position.X,
                villager.Position.Y,
                villager.Position.Z,
                villager.JobTarget.HasValue
                    ? new AgentVector3Dto(villager.JobTarget.Value.X, villager.JobTarget.Value.Y, villager.JobTarget.Value.Z)
                    : null,
                villager.AssignedBuildingSiteId,
                villager.AssignedBuildingId,
                villager.HaulSourceVillagerId,
                villager.HaulSourceChestId,
                villager.HaulIsDelivering,
                villager.BreakProgress,
                villager.WorkTimer,
                FormatDebugStack(villager.EquippedTool),
                ExportInventory(villager.Inventory)));
        }

        var buildings = new List<AgentVillageBuildingDto>();
        foreach (var building in village.Buildings)
        {
            buildings.Add(new AgentVillageBuildingDto(
                building.Id,
                building.BlueprintId,
                building.Kind.ToString(),
                building.IsComplete,
                building.AnchorX,
                building.AnchorY,
                building.AnchorZ));
        }

        var sites = new List<AgentVillageBuildingSiteDto>();
        foreach (var site in village.BuildingSites)
        {
            sites.Add(new AgentVillageBuildingSiteDto(
                site.Id,
                site.BlueprintId,
                site.IsComplete,
                site.RemainingCount,
                site.CompletionRatio,
                site.AnchorX,
                site.AnchorY,
                site.AnchorZ));
        }

        return new AgentVillageDebugDto(
            new AgentVillageDebugSummaryDto(
                village.Id,
                village.Name,
                village.AnchorX,
                village.AnchorY,
                village.AnchorZ,
                village.Radius,
                VillageSettlementHealth.GetLivePopulation(village, session.Villagers),
                village.Population,
                village.PopulationCap,
                village.HousingCapacity,
                village.Tier.ToString(),
                village.FoodStock,
                village.Happiness,
                village.WorkQueue.Count),
            ExportInventory(village.Storage),
            buildings,
            sites,
            villagers);
    }

    public static List<AgentInventorySlotDto> ExportInventory(IItemContainer inventory)
    {
        var slots = new List<AgentInventorySlotDto>();
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var stack = inventory.GetSlot(i);
            if (stack.IsEmpty)
            {
                continue;
            }

            slots.Add(new AgentInventorySlotDto(i, FormatDebugStack(stack)));
        }

        return slots;
    }

    public static object FormatDebugStack(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            return new { kind = "empty" };
        }

        if (stack.IsTool())
        {
            return new
            {
                kind = "tool",
                toolId = stack.ToolId.ToString(),
                name = stack.GetDisplayName(),
                durability = stack.Durability,
                maxDurability = stack.MaxDurability
            };
        }

        if (stack.IsFluidContainer())
        {
            return new
            {
                kind = "fluid_container",
                itemId = stack.ToolId.ToString(),
                name = stack.GetDisplayName(),
                filled = stack.IsWaterBucket()
            };
        }

        return new
        {
            kind = "block",
            blockType = stack.BlockType.ToString(),
            count = stack.Count
        };
    }

    private static List<AgentAnimalDto> BuildAnimalDtos(GameSession session, Player player)
    {
        var nearbyAnimals = session.Animals.GetAnimalsInRange(player.Position, 32f);
        List<AgentAnimalDto> animalDtos = new();
        for (int i = 0; i < nearbyAnimals.Count; i++)
        {
            var animal = nearbyAnimals[i];
            if (!animal.IsAlive)
            {
                continue;
            }

            animalDtos.Add(new AgentAnimalDto(
                animal.Id,
                animal.Type.ToString(),
                animal.Health,
                animal.MaxHealth,
                animal.Position.X,
                animal.Position.Y,
                animal.Position.Z));
        }

        return animalDtos;
    }

    private static AgentTargetBlockDto? BuildTargetBlockDto(BlockInteractionSystem interaction)
    {
        if (!interaction.TargetBlockPos.HasValue || interaction.TargetBlockType == BlockType.Air)
        {
            return null;
        }

        var tpos = interaction.TargetBlockPos.Value;
        return new AgentTargetBlockDto(
            (int)tpos.X,
            (int)tpos.Y,
            (int)tpos.Z,
            interaction.TargetBlockType.ToString(),
            interaction.BreakProgress,
            interaction.IsMining);
    }

    private static List<AgentVillagerDto> BuildVillagerDtos(List<Entities.Villager> nearbyVillagers)
    {
        List<AgentVillagerDto> villagerDtos = new();
        for (int i = 0; i < nearbyVillagers.Count; i++)
        {
            var v = nearbyVillagers[i];
            villagerDtos.Add(new AgentVillagerDto(
                v.Id,
                v.VillageId,
                v.Name,
                v.Role.ToString(),
                v.CurrentJob.ToString(),
                v.Position.X,
                v.Position.Y,
                v.Position.Z));
        }

        return villagerDtos;
    }

    private static List<object> BuildHotbar(Player player)
    {
        List<object> hotbar = new();
        for (int i = 0; i < 9; i++)
        {
            var slot = player.Hotbar[i];
            if (slot.IsEmpty)
            {
                hotbar.Add(new
                {
                    slot = i,
                    kind = "empty"
                });
            }
            else if (slot.IsTool())
            {
                hotbar.Add(new
                {
                    slot = i,
                    kind = "tool",
                    toolId = slot.ToolId,
                    name = slot.GetDisplayName(),
                    durability = slot.Durability,
                    maxDurability = slot.MaxDurability
                });
            }
            else if (slot.IsFood())
            {
                hotbar.Add(new
                {
                    slot = i,
                    kind = "food",
                    itemId = slot.FoodId,
                    name = slot.GetDisplayName(),
                    count = slot.Count
                });
            }
            else if (slot.IsFluidContainer())
            {
                hotbar.Add(new
                {
                    slot = i,
                    kind = "fluid_container",
                    itemId = slot.ToolId,
                    name = slot.GetDisplayName(),
                    filled = slot.IsWaterBucket()
                });
            }
            else
            {
                hotbar.Add(new
                {
                    slot = i,
                    kind = "block",
                    type = slot.BlockType.ToString(),
                    count = slot.Count
                });
            }
        }

        return hotbar;
    }
}
