using Autonocraft.Domain.Village;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public enum GatherCategory
    {
        Lumber,
        Mine
    }

    public static class GatherBlockClassifier
    {
        public static bool IsGatherable(BlockType block)
        {
            return GetCategory(block) != null;
        }

        public static GatherCategory? GetCategory(BlockType block)
        {
            if (IsLumber(block))
            {
                return GatherCategory.Lumber;
            }

            if (IsMine(block))
            {
                return GatherCategory.Mine;
            }

            return null;
        }

        public static VillagerRole GetPreferredRole(BlockType block)
        {
            return GetCategory(block) == GatherCategory.Mine
                ? VillagerRole.Miner
                : VillagerRole.Lumberjack;
        }

        public static bool CanGather(VillagerRole role, BlockType block)
        {
            var category = GetCategory(block);
            if (!category.HasValue)
            {
                return false;
            }

            return role switch
            {
                VillagerRole.Lumberjack => category == GatherCategory.Lumber,
                VillagerRole.Miner => category == GatherCategory.Mine,
                VillagerRole.Peasant => true,
                _ => false
            };
        }

        private static bool IsLumber(BlockType block)
        {
            return block is BlockType.OakLog
                or BlockType.BirchLog
                or BlockType.PineLog
                or BlockType.WillowLog
                or BlockType.PalmLog
                or BlockType.CherryLog
                or BlockType.MahoganyLog
                or BlockType.MapleLog;
        }

        private static bool IsMine(BlockType block)
        {
            return block is BlockType.Stone
                or BlockType.Cobblestone
                or BlockType.CoalOre
                or BlockType.IronOre
                or BlockType.GoldOre
                or BlockType.MossStone
                or BlockType.Sandstone
                or BlockType.Gravel;
        }
    }
}
