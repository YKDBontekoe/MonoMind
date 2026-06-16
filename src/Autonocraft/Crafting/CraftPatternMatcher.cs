using Autonocraft.Domain.Items;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Crafting
{
    /// <summary>Matches shaped recipe patterns against a square crafting grid (Minecraft-style).</summary>
    public static class CraftPatternMatcher
    {
        public static bool TryMatch(
            IReadOnlyList<string> patternRows,
            int gridSize,
            IReadOnlyList<BlockType> slots,
            out Dictionary<int, int> slotConsumption)
        {
            var stacks = new ItemStack[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                stacks[i] = slots[i] == BlockType.Air
                    ? ItemStack.Empty
                    : ItemStack.CreateBlock(slots[i], 1);
            }

            return TryMatch(patternRows, gridSize, stacks, out slotConsumption);
        }

        public static bool TryMatch(
            IReadOnlyList<string> patternRows,
            int gridSize,
            IReadOnlyList<ItemStack> slots,
            out Dictionary<int, int> slotConsumption)
        {
            slotConsumption = new Dictionary<int, int>();
            if (patternRows.Count == 0 || gridSize <= 0)
            {
                return false;
            }

            int patternHeight = patternRows.Count;
            int patternWidth = patternRows.Max(r => r.Length);
            if (patternWidth == 0)
            {
                return false;
            }

            for (int offsetY = 0; offsetY <= gridSize - patternHeight; offsetY++)
            {
                for (int offsetX = 0; offsetX <= gridSize - patternWidth; offsetX++)
                {
                    if (!PatternFitsAt(patternRows, gridSize, slots, offsetX, offsetY, out var match))
                    {
                        continue;
                    }

                    slotConsumption = match;
                    return true;
                }
            }

            return false;
        }

        private static bool PatternFitsAt(
            IReadOnlyList<string> patternRows,
            int gridSize,
            IReadOnlyList<ItemStack> slots,
            int offsetX,
            int offsetY,
            out Dictionary<int, int> consumption)
        {
            consumption = new Dictionary<int, int>();
            var usedSlots = new bool[gridSize * gridSize];

            for (int py = 0; py < patternRows.Count; py++)
            {
                string row = patternRows[py];
                for (int px = 0; px < row.Length; px++)
                {
                    char symbol = row[px];
                    if (symbol == ' ' || symbol == '.')
                    {
                        continue;
                    }

                    int gx = offsetX + px;
                    int gy = offsetY + py;
                    int slotIndex = gy * gridSize + gx;
                    if (slotIndex < 0 || slotIndex >= slots.Count)
                    {
                        return false;
                    }

                    if (usedSlots[slotIndex])
                    {
                        return false;
                    }

                    if (!SymbolMatches(symbol, slots[slotIndex]))
                    {
                        return false;
                    }

                    usedSlots[slotIndex] = true;
                    consumption[slotIndex] = 1;
                }
            }

            for (int i = 0; i < gridSize * gridSize; i++)
            {
                if (!usedSlots[i] && !slots[i].IsEmpty)
                {
                    return false;
                }
            }

            return consumption.Count > 0;
        }

        public static bool SymbolMatches(char symbol, ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return false;
            }

            if (stack.IsBlock())
            {
                return SymbolMatchesBlock(symbol, stack.BlockType);
            }

            return symbol switch
            {
                'T' => stack.IsMaterial() && stack.MaterialId == ItemId.Stick,
                _ => false
            };
        }

        private static bool SymbolMatchesBlock(char symbol, BlockType block)
        {
            return symbol switch
            {
                'P' => block is BlockType.OakPlank or BlockType.BirchPlank or BlockType.PinePlank,
                'L' => block.IsAnyLog(),
                'T' => false,
                'S' => block == BlockType.Stone,
                'C' => block == BlockType.Cobblestone,
                'I' => block == BlockType.IronBlock,
                'G' => block == BlockType.GoldBlock,
                'W' => block == BlockType.Wheat,
                'D' => block == BlockType.Dirt,
                'A' => block == BlockType.Sand,
                'O' => block.IsAnyLeaves() || block.MatchesTag(MaterialTag.Organic),
                _ => false
            };
        }
    }
}
