using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public static class RecipeBookResolver
    {
        public static IReadOnlyList<CraftRecipe> GetVisibleRecipes(
            BlockType stationType,
            CraftGridSize gridSize,
            DiscoveryJournal journal) =>
            CraftRecipeRegistry.ForStation(stationType)
                .Where(r => !r.IsFoodInput && r.GridSize <= gridSize)
                .Where(r => !r.RequiresUnlock || journal.IsUnlocked(r.Id))
                .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public static bool CanCraftWithInventory(CraftRecipe recipe, CraftGridSize gridSize, PlayerInventoryAdapter inventory)
        {
            if (recipe.GridSize > gridSize)
            {
                return false;
            }

            if (recipe.ShapedPattern is { Count: > 0 })
            {
                return CanFillShapedPattern(recipe.ShapedPattern, (int)gridSize, inventory);
            }

            foreach (var input in recipe.Inputs)
            {
                if (inventory.CountBlock(input.ExactBlock ?? BlockType.Air) < input.Count)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryFillGrid(CraftRecipe recipe, CraftingGrid grid, PlayerInventoryAdapter inventory)
        {
            grid.Clear();
            if (recipe.ShapedPattern is not { Count: > 0 } pattern)
            {
                return TryFillShapeless(recipe, grid, inventory);
            }

            int gridDim = (int)grid.Size;
            for (int py = 0; py < pattern.Count; py++)
            {
                string row = pattern[py];
                for (int px = 0; px < row.Length; px++)
                {
                    char symbol = row[px];
                    if (symbol == ' ' || symbol == '.')
                    {
                        continue;
                    }

                    int slotIndex = py * gridDim + px;
                    if (!TryTakeSymbol(symbol, inventory, out var stack))
                    {
                        grid.Clear();
                        return false;
                    }

                    grid.SetSlot(slotIndex, stack);
                }
            }

            return true;
        }

        public static bool TryFillBenchSlots(CraftRecipe recipe, ItemStack[] benchSlots, int activeSlots, PlayerInventoryAdapter inventory)
        {
            for (int i = 0; i < benchSlots.Length; i++)
            {
                benchSlots[i] = ItemStack.Empty;
            }

            var grid = new CraftingGrid();
            grid.SetSize(recipe.GridSize);
            if (!TryFillGrid(recipe, grid, inventory))
            {
                return false;
            }

            for (int i = 0; i < activeSlots; i++)
            {
                benchSlots[i] = grid.GetSlot(i);
            }

            return true;
        }

        private static bool TryFillShapeless(CraftRecipe recipe, CraftingGrid grid, PlayerInventoryAdapter inventory)
        {
            int slot = 0;
            foreach (var input in recipe.Inputs)
            {
                for (int i = 0; i < input.Count; i++)
                {
                    if (!TryTakeBlock(input, inventory, out var stack))
                    {
                        grid.Clear();
                        return false;
                    }

                    grid.SetSlot(slot++, stack);
                }
            }

            return true;
        }

        private static bool CanFillShapedPattern(IReadOnlyList<string> patternRows, int gridDim, PlayerInventoryAdapter inventory)
        {
            var needs = new Dictionary<char, int>();
            foreach (string row in patternRows)
            {
                foreach (char symbol in row)
                {
                    if (symbol == ' ' || symbol == '.')
                    {
                        continue;
                    }

                    needs.TryGetValue(symbol, out int count);
                    needs[symbol] = count + 1;
                }
            }

            foreach (var (symbol, count) in needs)
            {
                if (CountSymbol(symbol, inventory) < count)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountSymbol(char symbol, PlayerInventoryAdapter inventory) => symbol switch
        {
            'P' => CountPlanks(inventory),
            'T' => CountMaterial(inventory, ItemId.Stick),
            'L' => CountLogs(inventory),
            'S' => inventory.CountBlock(BlockType.Stone),
            'C' => inventory.CountBlock(BlockType.Cobblestone),
            'I' => inventory.CountBlock(BlockType.IronBlock),
            'G' => inventory.CountBlock(BlockType.GoldBlock),
            _ => 0
        };

        private static bool TryTakeSymbol(char symbol, PlayerInventoryAdapter inventory, out ItemStack stack)
        {
            stack = ItemStack.Empty;
            return symbol switch
            {
                'P' => TryTakePlank(inventory, out stack),
                'T' => TryTakeMaterial(inventory, ItemId.Stick, out stack),
                'L' => TryTakeLog(inventory, out stack),
                'S' => TryTakeBlock(inventory, BlockType.Stone, out stack),
                'C' => TryTakeBlock(inventory, BlockType.Cobblestone, out stack),
                'I' => TryTakeBlock(inventory, BlockType.IronBlock, out stack),
                'G' => TryTakeBlock(inventory, BlockType.GoldBlock, out stack),
                _ => false
            };
        }

        private static bool TryTakeBlock(PlayerInventoryAdapter inventory, BlockType blockType, out ItemStack stack)
        {
            stack = ItemStack.Empty;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsBlock() || slot.BlockType != blockType)
                {
                    continue;
                }

                stack = ItemStack.CreateBlock(blockType, 1);
                slot.Count--;
                if (slot.Count <= 0)
                {
                    slot = ItemStack.Empty;
                }

                inventory.SetSlot(i, slot);
                return true;
            }

            return false;
        }

        private static bool TryTakeMaterial(PlayerInventoryAdapter inventory, ItemId materialId, out ItemStack stack)
        {
            stack = ItemStack.Empty;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsMaterial() || slot.MaterialId != materialId)
                {
                    continue;
                }

                stack = ItemStack.CreateMaterial(materialId, 1);
                slot.Count--;
                if (slot.Count <= 0)
                {
                    slot = ItemStack.Empty;
                }

                inventory.SetSlot(i, slot);
                return true;
            }

            return false;
        }

        private static bool TryTakeBlock(CraftInput input, PlayerInventoryAdapter inventory, out ItemStack stack)
        {
            stack = ItemStack.Empty;
            if (!input.ExactBlock.HasValue)
            {
                return false;
            }

            return TryTakeBlock(inventory, input.ExactBlock.Value, out stack);
        }

        private static int CountPlanks(PlayerInventoryAdapter inventory) =>
            inventory.CountBlock(BlockType.OakPlank)
            + inventory.CountBlock(BlockType.BirchPlank)
            + inventory.CountBlock(BlockType.PinePlank);

        private static int CountLogs(PlayerInventoryAdapter inventory)
        {
            int total = 0;
            foreach (BlockType log in new[]
                     {
                         BlockType.OakLog, BlockType.BirchLog, BlockType.PineLog,
                         BlockType.WillowLog, BlockType.PalmLog
                     })
            {
                total += inventory.CountBlock(log);
            }

            return total;
        }

        private static int CountMaterial(PlayerInventoryAdapter inventory, ItemId materialId)
        {
            int total = 0;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot.IsMaterial() && slot.MaterialId == materialId)
                {
                    total += slot.Count;
                }
            }

            return total;
        }

        private static bool TryTakePlank(PlayerInventoryAdapter inventory, out ItemStack stack)
        {
            foreach (BlockType plank in new[] { BlockType.OakPlank, BlockType.BirchPlank, BlockType.PinePlank })
            {
                if (TryTakeBlock(inventory, plank, out stack))
                {
                    return true;
                }
            }

            stack = ItemStack.Empty;
            return false;
        }

        private static bool TryTakeLog(PlayerInventoryAdapter inventory, out ItemStack stack)
        {
            foreach (BlockType log in new[]
                     {
                         BlockType.OakLog, BlockType.BirchLog, BlockType.PineLog,
                         BlockType.WillowLog, BlockType.PalmLog
                     })
            {
                if (TryTakeBlock(inventory, log, out stack))
                {
                    return true;
                }
            }

            stack = ItemStack.Empty;
            return false;
        }
    }
}
