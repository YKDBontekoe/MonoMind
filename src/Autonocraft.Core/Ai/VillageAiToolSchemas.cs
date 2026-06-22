namespace Autonocraft.Ai
{
    internal static class VillageAiToolSchemas
    {
        public const string Json = """
[
  {
    "type": "function",
    "function": {
      "name": "get_village_summary",
      "description": "Get the current village status, villagers, storage, jobs, build sites, and goals.",
      "parameters": { "type": "object", "properties": {}, "additionalProperties": false }
    }
  },
  {
    "type": "function",
    "function": {
      "name": "list_villagers",
      "description": "List live citizens and their current jobs.",
      "parameters": { "type": "object", "properties": {}, "additionalProperties": false }
    }
  },
  {
    "type": "function",
    "function": {
      "name": "assign_job",
      "description": "Assign a villager to a job. Use target coordinates only when the player named a location.",
      "parameters": {
        "type": "object",
        "properties": {
          "villager_id": { "type": "integer" },
          "job": { "type": "string", "enum": ["Idle", "Lumber", "Mine", "Farm", "Build", "Haul", "Gather"] },
          "target_x": { "type": "integer" },
          "target_y": { "type": "integer" },
          "target_z": { "type": "integer" },
          "building_site_id": { "type": "integer" }
        },
        "required": ["villager_id", "job"],
        "additionalProperties": false
      }
    }
  },
  {
    "type": "function",
    "function": {
      "name": "recruit_villager",
      "description": "Recruit a peasant into the active village when housing and food allow it.",
      "parameters": { "type": "object", "properties": {}, "additionalProperties": false }
    }
  },
  {
    "type": "function",
    "function": {
      "name": "queue_build",
      "description": "Queue a village building blueprint at an anchor position paid from the player inventory.",
      "parameters": {
        "type": "object",
        "properties": {
          "blueprint_id": { "type": "string", "enum": ["peasant_house"] },
          "anchor_x": { "type": "integer" },
          "anchor_z": { "type": "integer" }
        },
        "required": ["blueprint_id", "anchor_x", "anchor_z"],
        "additionalProperties": false
      }
    }
  },
  {
    "type": "function",
    "function": {
      "name": "mark_resource",
      "description": "Mark a world block as a villager resource target.",
      "parameters": {
        "type": "object",
        "properties": {
          "villager_id": { "type": "integer" },
          "x": { "type": "integer" },
          "y": { "type": "integer" },
          "z": { "type": "integer" }
        },
        "required": ["villager_id", "x", "y", "z"],
        "additionalProperties": false
      }
    }
  },
  {
    "type": "function",
    "function": {
      "name": "cancel_job",
      "description": "Cancel a villager's current scheduled job.",
      "parameters": {
        "type": "object",
        "properties": { "villager_id": { "type": "integer" } },
        "required": ["villager_id"],
        "additionalProperties": false
      }
    }
  },
  {
    "type": "function",
    "function": {
      "name": "set_village_goal",
      "description": "Create a stockpile or build goal for the village scheduler.",
      "parameters": {
        "type": "object",
        "properties": {
          "kind": { "type": "string", "enum": ["stock", "build"] },
          "block_type": { "type": "string" },
          "target_count": { "type": "integer" },
          "blueprint_id": { "type": "string" },
          "description": { "type": "string" },
          "priority": { "type": "integer" }
        },
        "additionalProperties": false
      }
    }
  }
]
""";
    }
}
