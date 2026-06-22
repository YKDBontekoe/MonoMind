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
                .OrderBy(r => r.RequiresUnlock && !journal.IsUnlocked(r.Id) ? 1 : 0)
                .ThenBy(r => RecipeTier(r))
                .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public static bool CanCraftWithInventory(CraftRecipe recipe, CraftGridSize gridSize, IItemContainer inventory)
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
                if (CountMatchingInput(input, inventory) < input.Count)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryFillGrid(CraftRecipe recipe, CraftingGrid grid, IItemContainer inventory)
        {
            grid.Clear();
            if (!CanCraftWithInventory(recipe, grid.Size, inventory))
            {
                return false;
            }

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

        public static bool TryFillBenchSlots(CraftRecipe recipe, ItemStack[] benchSlots, int activeSlots, IItemContainer inventory)
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

        private static bool TryFillShapeless(CraftRecipe recipe, CraftingGrid grid, IItemContainer inventory)
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

        private static bool CanFillShapedPattern(IReadOnlyList<string> patternRows, int gridDim, IItemContainer inventory)
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

        private static int CountSymbol(char symbol, IItemContainer inventory) => symbol switch
        {
            'P' => CountPlanks(inventory),
            'T' => CountMaterial(inventory, ItemId.Stick),
            'L' => CountLogs(inventory),
            'S' => CountStoneLike(inventory),
            'C' => inventory.CountBlock(BlockType.Cobblestone),
            'I' => inventory.CountBlock(BlockType.IronBlock),
            'G' => inventory.CountBlock(BlockType.GoldBlock),
            'U' => inventory.CountBlock(BlockType.CopperBlock),
            'V' => inventory.CountBlock(BlockType.SilverBlock),
            'H' => inventory.CountBlock(BlockType.DiamondBlock),
            'E' => inventory.CountBlock(BlockType.EmeraldBlock),
            'W' => inventory.CountBlock(BlockType.Wheat),
            'D' => inventory.CountBlock(BlockType.Dirt),
            'A' => inventory.CountBlock(BlockType.Sand),
            'O' => CountOrganic(inventory),
            _ => 0
        };

        private static bool TryTakeSymbol(char symbol, IItemContainer inventory, out ItemStack stack)
        {
            stack = ItemStack.Empty;
            return symbol switch
            {
                'P' => TryTakePlank(inventory, out stack),
                'T' => TryTakeMaterial(inventory, ItemId.Stick, out stack),
                'L' => TryTakeLog(inventory, out stack),
                'S' => TryTakeFirstBlock(inventory, StoneLikeBlocks, out stack),
                'C' => TryTakeBlock(inventory, BlockType.Cobblestone, out stack),
                'I' => TryTakeBlock(inventory, BlockType.IronBlock, out stack),
                'G' => TryTakeBlock(inventory, BlockType.GoldBlock, out stack),
                'U' => TryTakeBlock(inventory, BlockType.CopperBlock, out stack),
                'V' => TryTakeBlock(inventory, BlockType.SilverBlock, out stack),
                'H' => TryTakeBlock(inventory, BlockType.DiamondBlock, out stack),
                'E' => TryTakeBlock(inventory, BlockType.EmeraldBlock, out stack),
                'W' => TryTakeBlock(inventory, BlockType.Wheat, out stack),
                'D' => TryTakeBlock(inventory, BlockType.Dirt, out stack),
                'A' => TryTakeBlock(inventory, BlockType.Sand, out stack),
                'O' => TryTakeMatchingBlock(inventory, b => b.IsAnyLeaves() || b.MatchesTag(MaterialTag.Organic), out stack),
                _ => false
            };
        }

        private static bool TryTakeBlock(IItemContainer inventory, BlockType blockType, out ItemStack stack)
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

        private static bool TryTakeMaterial(IItemContainer inventory, ItemId materialId, out ItemStack stack)
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

        private static bool TryTakeBlock(CraftInput input, IItemContainer inventory, out ItemStack stack)
        {
            stack = ItemStack.Empty;
            return TryTakeMatchingBlock(inventory, input.Matches, out stack);
        }

        private static int CountPlanks(IItemContainer inventory) =>
            inventory.CountBlock(BlockType.OakPlank)
            + inventory.CountBlock(BlockType.BirchPlank)
            + inventory.CountBlock(BlockType.PinePlank)
            + inventory.CountBlock(BlockType.CherryPlank)
            + inventory.CountBlock(BlockType.MahoganyPlank)
            + inventory.CountBlock(BlockType.MaplePlank);

        private static int CountLogs(IItemContainer inventory)
        {
            int total = 0;
            foreach (BlockType log in new[]
                     {
                         BlockType.OakLog, BlockType.BirchLog, BlockType.PineLog,
                         BlockType.WillowLog, BlockType.PalmLog, BlockType.CherryLog,
                         BlockType.MahoganyLog, BlockType.MapleLog
                     })
            {
                total += inventory.CountBlock(log);
            }

            return total;
        }

        private static int CountMaterial(IItemContainer inventory, ItemId materialId)
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

        private static bool TryTakePlank(IItemContainer inventory, out ItemStack stack)
        {
            foreach (BlockType plank in PlankBlocks)
            {
                if (TryTakeBlock(inventory, plank, out stack))
                {
                    return true;
                }
            }

            stack = ItemStack.Empty;
            return false;
        }

        private static bool TryTakeLog(IItemContainer inventory, out ItemStack stack)
        {
            foreach (BlockType log in LogBlocks)
            {
                if (TryTakeBlock(inventory, log, out stack))
                {
                    return true;
                }
            }

            stack = ItemStack.Empty;
            return false;
        }

        public static string DescribeOutput(CraftRecipe recipe)
        {
            string name = recipe.OutputKind switch
            {
                ItemKind.Tool => ToolRegistry.Get(recipe.OutputItem).DisplayName,
                ItemKind.Food => FoodRegistry.GetDisplayName(recipe.OutputItem),
                ItemKind.Material => MaterialRegistry.GetDisplayName(recipe.OutputItem),
                _ => FormatBlockName(recipe.Output)
            };

            return recipe.OutputCount > 1 ? $"{recipe.OutputCount}x {name}" : name;
        }

        public static string DescribeInputs(CraftRecipe recipe)
        {
            if (recipe.ShapedPattern is { Count: > 0 } pattern)
            {
                var parts = new List<string>();
                foreach (var (symbol, count) in CountPatternSymbols(pattern))
                {
                    parts.Add($"{count}x {DescribeSymbol(symbol)}");
                }

                return string.Join(", ", parts);
            }

            return string.Join(", ", recipe.Inputs.Select(i => $"{i.Count}x {DescribeInput(i)}"));
        }

        public static string DescribeStation(CraftRecipe recipe) => recipe.StationType switch
        {
            BlockType.StationBench => recipe.GridSize == CraftGridSize.ThreeByThree ? "Crafting bench" : "Inventory or bench",
            BlockType.StationForge => "Forge",
            BlockType.StationCrucible => "Crucible",
            BlockType.StationStonecutter => "Stonecutter",
            BlockType.StationSmoker => "Smoker",
            _ => FormatBlockName(recipe.StationType)
        };

        public static string DescribeRequirements(CraftRecipe recipe)
        {
            var parts = new List<string>();
            if (recipe.RequiresHeat)
            {
                parts.Add("heat");
            }

            if (recipe.RequiresWater)
            {
                parts.Add("water nearby");
            }

            if (recipe.RequiredTimePhase.HasValue)
            {
                parts.Add(recipe.RequiredTimePhase.Value.ToString().ToLowerInvariant());
            }

            if (recipe.RequiredBiome.HasValue)
            {
                parts.Add($"{recipe.RequiredBiome.Value} biome");
            }
            else if (recipe.AllowedBiomes is { Count: > 0 })
            {
                parts.Add(string.Join("/", recipe.AllowedBiomes));
            }

            return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
        }

        public static string GetGuideText(CraftRecipe recipe)
        {
            if (!string.IsNullOrWhiteSpace(recipe.GuideText))
            {
                return recipe.GuideText;
            }

            string output = DescribeOutput(recipe);
            string inputs = DescribeInputs(recipe);
            string station = DescribeStation(recipe);
            string requirements = DescribeRequirements(recipe);
            string suffix = string.IsNullOrEmpty(requirements) ? string.Empty : $" ({requirements})";
            return $"{inputs} -> {output} at {station}{suffix}";
        }

        public static string GetUnlockHint(CraftRecipe recipe)
        {
            if (!recipe.RequiresUnlock)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(recipe.UnlockHint))
            {
                return recipe.UnlockHint;
            }

            return recipe.StationType switch
            {
                BlockType.StationBench => "Discover the workbench sigil.",
                BlockType.StationForge => "Smelt the matching metal first.",
                _ => "Discover more materials to unlock."
            };
        }

        public static string GetAvailabilityText(CraftRecipe recipe, CraftGridSize gridSize, DiscoveryJournal journal, IItemContainer inventory)
        {
            if (recipe.RequiresUnlock && !journal.IsUnlocked(recipe.Id))
            {
                return GetUnlockHint(recipe);
            }

            if (recipe.GridSize > gridSize)
            {
                return "Needs a larger grid.";
            }

            return CanCraftWithInventory(recipe, gridSize, inventory)
                ? "Ready to craft"
                : $"Need {DescribeInputs(recipe)}";
        }

        private static int RecipeTier(CraftRecipe recipe)
        {
            if (recipe.Id is "recipe:plank" or "recipe:sticks")
            {
                return 0;
            }

            if (recipe.OutputKind == ItemKind.Tool && recipe.OutputItem.ToString().StartsWith("Wood", StringComparison.Ordinal))
            {
                return 1;
            }

            if (recipe.StationType == BlockType.StationBench)
            {
                return 2;
            }

            return 3;
        }

        private static Dictionary<char, int> CountPatternSymbols(IReadOnlyList<string> patternRows)
        {
            var counts = new Dictionary<char, int>();
            foreach (string row in patternRows)
            {
                foreach (char symbol in row)
                {
                    if (symbol == ' ' || symbol == '.')
                    {
                        continue;
                    }

                    counts.TryGetValue(symbol, out int count);
                    counts[symbol] = count + 1;
                }
            }

            return counts;
        }

        private static string DescribeInput(CraftInput input)
        {
            if (input.ExactBlock.HasValue)
            {
                return FormatBlockName(input.ExactBlock.Value);
            }

            return input.Tag switch
            {
                MaterialTag.Wood => "any wood",
                MaterialTag.Earth => "earth block",
                MaterialTag.Ore => "ore or ingot block",
                MaterialTag.Organic => "leaves or plants",
                MaterialTag.Fuel => "fuel",
                MaterialTag.Stone => "stone block",
                _ => "material"
            };
        }

        private static string DescribeSymbol(char symbol) => symbol switch
        {
            'P' => "planks",
            'T' => "sticks",
            'L' => "logs",
            'S' => "stone",
            'C' => "cobblestone",
            'I' => "iron blocks",
            'G' => "gold blocks",
            'U' => "copper blocks",
            'V' => "silver blocks",
            'H' => "diamond blocks",
            'E' => "emerald blocks",
            'W' => "wheat",
            'D' => "dirt",
            'A' => "sand",
            'O' => "leaves or plants",
            _ => "material"
        };

        private static string FormatBlockName(BlockType block)
        {
            string name = block.ToString();
            var chars = new List<char>(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    chars.Add(' ');
                }

                chars.Add(name[i]);
            }

            return new string(chars.ToArray());
        }

        private static int CountMatchingInput(CraftInput input, IItemContainer inventory)
        {
            int total = 0;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot.IsBlock() && input.Matches(slot.BlockType))
                {
                    total += slot.Count;
                }
            }

            return total;
        }

        private static int CountStoneLike(IItemContainer inventory) => StoneLikeBlocks.Sum(inventory.CountBlock);

        private static int CountOrganic(IItemContainer inventory)
        {
            int total = 0;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot.IsBlock() && (slot.BlockType.IsAnyLeaves() || slot.BlockType.MatchesTag(MaterialTag.Organic)))
                {
                    total += slot.Count;
                }
            }

            return total;
        }

        private static bool TryTakeFirstBlock(IItemContainer inventory, IReadOnlyList<BlockType> blocks, out ItemStack stack)
        {
            foreach (var block in blocks)
            {
                if (TryTakeBlock(inventory, block, out stack))
                {
                    return true;
                }
            }

            stack = ItemStack.Empty;
            return false;
        }

        private static bool TryTakeMatchingBlock(IItemContainer inventory, Func<BlockType, bool> matches, out ItemStack stack)
        {
            stack = ItemStack.Empty;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsBlock() || !matches(slot.BlockType))
                {
                    continue;
                }

                stack = ItemStack.CreateBlock(slot.BlockType, 1);
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

        private static readonly BlockType[] PlankBlocks =
        {
            BlockType.OakPlank, BlockType.BirchPlank, BlockType.PinePlank,
            BlockType.CherryPlank, BlockType.MahoganyPlank, BlockType.MaplePlank
        };

        private static readonly BlockType[] LogBlocks =
        {
            BlockType.OakLog, BlockType.BirchLog, BlockType.PineLog,
            BlockType.WillowLog, BlockType.PalmLog, BlockType.CherryLog,
            BlockType.MahoganyLog, BlockType.MapleLog
        };

        private static readonly BlockType[] StoneLikeBlocks =
        {
            BlockType.Stone, BlockType.Marble, BlockType.Basalt, BlockType.Slate,
            BlockType.Limestone, BlockType.Granite
        };
    }
}
