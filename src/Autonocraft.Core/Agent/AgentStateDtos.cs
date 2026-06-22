using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Autonocraft.Core;

/// <summary>
/// DTOs for the Agent HTTP JSON surface. These types are shaped to
/// exactly match the existing JSON fields of the /health, /state,
/// /village/debug, /village/chat and /village/chat/confirm endpoints.
/// </summary>
public sealed record AgentHealthDto(
    [property: JsonPropertyName("ready")] bool Ready,
    [property: JsonPropertyName("gameState")] string GameState);

public sealed record AgentActionResponseDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message);

public sealed record AgentChatResponseDto(
    [property: JsonPropertyName("reply")] string Reply,
    [property: JsonPropertyName("actions")] IReadOnlyList<string> Actions);

public sealed record AgentChatConfirmResponseDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("reply")] string Reply);

public sealed record AgentStateDto(
    [property: JsonPropertyName("gameState")] string GameState,
    [property: JsonPropertyName("worldSeed")] long WorldSeed,
    [property: JsonPropertyName("oxygen")] float Oxygen,
    [property: JsonPropertyName("position")] AgentVector3Dto Position,
    [property: JsonPropertyName("velocity")] AgentVector3Dto Velocity,
    [property: JsonPropertyName("yaw")] float Yaw,
    [property: JsonPropertyName("pitch")] float Pitch,
    [property: JsonPropertyName("creativeMode")] bool CreativeMode,
    [property: JsonPropertyName("isGrounded")] bool IsGrounded,
    [property: JsonPropertyName("health")] float Health,
    [property: JsonPropertyName("maxHealth")] float MaxHealth,
    [property: JsonPropertyName("hunger")] float Hunger,
    [property: JsonPropertyName("maxHunger")] float MaxHunger,
    [property: JsonPropertyName("earlyGuideStage")] int EarlyGuideStage,
    [property: JsonPropertyName("guidanceHint")] string GuidanceHint,
    [property: JsonPropertyName("timeOfDay")] float TimeOfDay,
    [property: JsonPropertyName("timeScale")] float TimeScale,
    [property: JsonPropertyName("timePaused")] bool TimePaused,
    [property: JsonPropertyName("selectedSlot")] int SelectedSlot,
    [property: JsonPropertyName("hotbar")] IReadOnlyList<object> Hotbar,
    [property: JsonPropertyName("skills")] AgentSkillsDto Skills,
    [property: JsonPropertyName("animals")] IReadOnlyList<AgentAnimalDto> Animals,
    [property: JsonPropertyName("targetBlock")] AgentTargetBlockDto? TargetBlock,
    [property: JsonPropertyName("nearbyStation")] string? NearbyStation,
    [property: JsonPropertyName("unlockedRecipes")] IReadOnlyList<string> UnlockedRecipes,
    [property: JsonPropertyName("playWithAi")] bool PlayWithAi,
    [property: JsonPropertyName("aiProvider")] string AiProvider,
    [property: JsonPropertyName("llmAvailable")] bool LlmAvailable,
    [property: JsonPropertyName("village")] AgentVillageSummaryDto? Village,
    [property: JsonPropertyName("villagers")] IReadOnlyList<AgentVillagerDto> Villagers,
    [property: JsonPropertyName("nearbyVillagerForChat")] AgentNearbyVillagerDto? NearbyVillagerForChat,
    [property: JsonPropertyName("worldType")] string WorldType,
    [property: JsonPropertyName("structureGallery")] bool StructureGallery);

public sealed record AgentVector3Dto(
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y,
    [property: JsonPropertyName("z")] float Z);

public sealed record AgentSkillsDto(
    [property: JsonPropertyName("mining")] AgentSkillDto Mining,
    [property: JsonPropertyName("woodcutting")] AgentSkillDto Woodcutting,
    [property: JsonPropertyName("combat")] AgentSkillDto Combat);

public sealed record AgentSkillDto(
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("xp")] float Xp);

public sealed record AgentAnimalDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("health")] float Health,
    [property: JsonPropertyName("maxHealth")] float MaxHealth,
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y,
    [property: JsonPropertyName("z")] float Z);

public sealed record AgentTargetBlockDto(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("z")] int Z,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("breakProgress")] float BreakProgress,
    [property: JsonPropertyName("isMining")] bool IsMining);

public sealed record AgentVillageSummaryDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("population")] int Population,
    [property: JsonPropertyName("populationCap")] int PopulationCap,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("happiness")] float Happiness,
    [property: JsonPropertyName("foodStock")] float FoodStock,
    [property: JsonPropertyName("anchorX")] int AnchorX,
    [property: JsonPropertyName("anchorZ")] int AnchorZ,
    [property: JsonPropertyName("nextAction")] string? NextAction = null,
    [property: JsonPropertyName("idleWorkers")] int? IdleWorkers = null,
    [property: JsonPropertyName("foodRisk")] string? FoodRisk = null,
    [property: JsonPropertyName("favor")] int? Favor = null,
    [property: JsonPropertyName("familyGrowthProgress")] float? FamilyGrowthProgress = null,
    [property: JsonPropertyName("growth")] string? Growth = null,
    [property: JsonPropertyName("agentWorkOrderCost")] int? AgentWorkOrderCost = null);

public sealed record AgentVillagerDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("villageId")] int VillageId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("job")] string Job,
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y,
    [property: JsonPropertyName("z")] float Z,
    [property: JsonPropertyName("activity")] string? Activity = null,
    [property: JsonPropertyName("progress")] string? Progress = null,
    [property: JsonPropertyName("needsAttention")] bool? NeedsAttention = null);

public sealed record AgentNearbyVillagerDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name);

public sealed record AgentVillageDebugDto(
    [property: JsonPropertyName("village")] AgentVillageDebugSummaryDto Village,
    [property: JsonPropertyName("storage")] IReadOnlyList<AgentInventorySlotDto> Storage,
    [property: JsonPropertyName("buildings")] IReadOnlyList<AgentVillageBuildingDto> Buildings,
    [property: JsonPropertyName("buildingSites")] IReadOnlyList<AgentVillageBuildingSiteDto> BuildingSites,
    [property: JsonPropertyName("villagers")] IReadOnlyList<AgentVillageDebugVillagerDto> Villagers);

public sealed record AgentVillageDebugSummaryDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("anchorX")] int AnchorX,
    [property: JsonPropertyName("anchorY")] int AnchorY,
    [property: JsonPropertyName("anchorZ")] int AnchorZ,
    [property: JsonPropertyName("radius")] float Radius,
    [property: JsonPropertyName("population")] int Population,
    [property: JsonPropertyName("registryPopulation")] int RegistryPopulation,
    [property: JsonPropertyName("populationCap")] int PopulationCap,
    [property: JsonPropertyName("housingCapacity")] int HousingCapacity,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("foodStock")] float FoodStock,
    [property: JsonPropertyName("happiness")] float Happiness,
    [property: JsonPropertyName("workQueue")] int WorkQueue,
    [property: JsonPropertyName("favor")] int? Favor = null,
    [property: JsonPropertyName("familyGrowthProgress")] float? FamilyGrowthProgress = null,
    [property: JsonPropertyName("agentWorkOrderCost")] int? AgentWorkOrderCost = null);

public sealed record AgentInventorySlotDto(
    [property: JsonPropertyName("slot")] int Slot,
    [property: JsonPropertyName("stack")] object Stack);

public sealed record AgentVillageBuildingDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("blueprintId")] string BlueprintId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("complete")] bool Complete,
    [property: JsonPropertyName("anchorX")] int AnchorX,
    [property: JsonPropertyName("anchorY")] int AnchorY,
    [property: JsonPropertyName("anchorZ")] int AnchorZ);

public sealed record AgentVillageBuildingSiteDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("blueprintId")] string BlueprintId,
    [property: JsonPropertyName("complete")] bool Complete,
    [property: JsonPropertyName("remaining")] int Remaining,
    [property: JsonPropertyName("completion")] float Completion,
    [property: JsonPropertyName("anchorX")] int AnchorX,
    [property: JsonPropertyName("anchorY")] int AnchorY,
    [property: JsonPropertyName("anchorZ")] int AnchorZ);

public sealed record AgentVillageDebugVillagerDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("villageId")] int VillageId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("job")] string Job,
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y,
    [property: JsonPropertyName("z")] float Z,
    [property: JsonPropertyName("target")] AgentVector3Dto? Target,
    [property: JsonPropertyName("buildingSiteId")] int? BuildingSiteId,
    [property: JsonPropertyName("buildingId")] int? BuildingId,
    [property: JsonPropertyName("haulSourceVillagerId")] int? HaulSourceVillagerId,
    [property: JsonPropertyName("haulSourceChestId")] int? HaulSourceChestId,
    [property: JsonPropertyName("haulDelivering")] bool HaulDelivering,
    [property: JsonPropertyName("breakProgress")] float BreakProgress,
    [property: JsonPropertyName("workTimer")] float WorkTimer,
    [property: JsonPropertyName("equippedTool")] object EquippedTool,
    [property: JsonPropertyName("inventory")] IReadOnlyList<AgentInventorySlotDto> Inventory);
