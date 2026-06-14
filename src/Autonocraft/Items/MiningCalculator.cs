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
                var skill = category == BlockHarvestCategory.Wood
                    ? PlayerSkill.Woodcutting
                    : PlayerSkill.Mining;
                float multiplier = toolDef.MiningSpeedMultiplier * skills.GetBonus(skill);
                return baseTime / Math.Max(0.01f, multiplier);
            }

            if (category == BlockHarvestCategory.Stone)
            {
                return baseTime * WrongToolHardBlockPenalty;
            }

            return baseTime;
        }

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
