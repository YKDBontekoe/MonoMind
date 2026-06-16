using Autonocraft.World;

namespace Autonocraft.Items
{
    public static class MiningCalculator
    {
        public const float WrongToolHardBlockPenalty = 3f;

        public static float GetEffectiveBreakTime(
            BlockType block,
            in ItemStack heldItem,
            PlayerSkills skills)
        {
            return GetEffectiveBreakTime(
                block,
                heldItem,
                skills.GetBonus(GetSkillForBlock(block)));
        }

        public static float GetEffectiveBreakTime(
            BlockType block,
            in ItemStack heldItem,
            VillagerSkills skills)
        {
            return GetEffectiveBreakTime(
                block,
                heldItem,
                skills.GetBonus(ToVillagerSkill(GetSkillForBlock(block))));
        }

        private static float GetEffectiveBreakTime(
            BlockType block,
            in ItemStack heldItem,
            float skillBonus)
        {
            float baseTime = block.GetBreakTime();
            if (baseTime <= 0f)
            {
                return 0f;
            }

            var category = block.GetHarvestCategory();
            if (category == BlockHarvestCategory.None)
            {
                return 0f;
            }

            var preferredTool = category.GetPreferredTool();
            if (!preferredTool.HasValue)
            {
                return baseTime;
            }

            if (!heldItem.IsTool() || !ToolRegistry.TryGet(heldItem.ToolId, out var toolDef))
            {
                if (category == BlockHarvestCategory.Stone)
                {
                    return baseTime * WrongToolHardBlockPenalty;
                }

                return baseTime;
            }

            if (toolDef.ToolType == preferredTool.Value)
            {
                float multiplier = toolDef.MiningSpeedMultiplier * skillBonus;
                return baseTime / Math.Max(0.01f, multiplier);
            }

            if (category == BlockHarvestCategory.Stone)
            {
                return baseTime * WrongToolHardBlockPenalty;
            }

            return baseTime;
        }

        public static VillagerSkill ToVillagerSkill(PlayerSkill skill) =>
            skill switch
            {
                PlayerSkill.Woodcutting => VillagerSkill.Woodcutting,
                _ => VillagerSkill.Mining
            };

        public static PlayerSkill GetSkillForBlock(BlockType block)
        {
            return block.GetHarvestCategory() switch
            {
                BlockHarvestCategory.Wood => PlayerSkill.Woodcutting,
                BlockHarvestCategory.Earth => PlayerSkill.Mining,
                BlockHarvestCategory.Stone => PlayerSkill.Mining,
                _ => PlayerSkill.Mining
            };
        }

        public static float GetXpForBlock(BlockType block)
        {
            return block.GetHarvestCategory() switch
            {
                BlockHarvestCategory.Wood => 2f,
                BlockHarvestCategory.Earth => 1f,
                BlockHarvestCategory.Stone => 3f,
                _ => 0f
            };
        }
    }
}
