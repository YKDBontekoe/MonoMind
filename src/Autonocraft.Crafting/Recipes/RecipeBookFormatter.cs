using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public static class RecipeBookFormatter
    {
        public static IReadOnlyList<RecipeBookEntry> BuildEntries(
            BlockType stationType,
            CraftGridSize activeGridSize,
            IItemContainer inventory,
            CraftEnvironment? environment = null)
        {
            return CraftRecipeRegistry.ForStation(stationType)
                .Where(r => !r.IsFoodInput)
                .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(r => BuildEntry(r, activeGridSize, inventory, environment))
                .ToList();
        }

        public static RecipeBookEntry BuildEntry(
            CraftRecipe recipe,
            CraftGridSize activeGridSize,
            IItemContainer inventory,
            CraftEnvironment? environment = null)
        {
            string ingredientSummary = FormatIngredients(recipe);
            string outputSummary = FormatOutput(recipe);
            var craftability = ClassifyCraftability(recipe, activeGridSize, inventory, environment, out string? missingHint);

            return new RecipeBookEntry
            {
                Recipe = recipe,
                DisplayName = recipe.DisplayName,
                IngredientSummary = ingredientSummary,
                OutputSummary = outputSummary,
                Craftability = craftability,
                MissingHint = missingHint
            };
        }

        private static RecipeBookCraftability ClassifyCraftability(
            CraftRecipe recipe,
            CraftGridSize activeGridSize,
            IItemContainer inventory,
            CraftEnvironment? environment,
            out string? missingHint)
        {
            missingHint = null;

            if (recipe.GridSize > activeGridSize)
            {
                missingHint = "Requires bench (3×3)";
                return RecipeBookCraftability.NeedsLargerGrid;
            }

            if (environment != null)
            {
                if (recipe.RequiresHeat && !environment.HasAdjacentHeat && !environment.HasFuelInInputs)
                {
                    missingHint = "Requires heat";
                    return RecipeBookCraftability.NeedsEnvironment;
                }

                if (recipe.RequiresWater && !environment.HasAdjacentWater)
                {
                    missingHint = "Requires water";
                    return RecipeBookCraftability.NeedsEnvironment;
                }

                if (!recipe.EnvironmentMatches(environment))
                {
                    missingHint = "Environment requirements not met";
                    return RecipeBookCraftability.NeedsEnvironment;
                }
            }
            else if (recipe.RequiresHeat || recipe.RequiresWater || recipe.RequiredTimePhase.HasValue
                     || recipe.RequiredBiome.HasValue || recipe.AllowedBiomes is { Count: > 0 })
            {
                missingHint = recipe.RequiresHeat ? "Requires heat"
                    : recipe.RequiresWater ? "Requires water"
                    : "Special environment required";
                return RecipeBookCraftability.NeedsEnvironment;
            }

            if (RecipeBookResolver.CanCraftWithInventory(recipe, activeGridSize, inventory))
            {
                return RecipeBookCraftability.Craftable;
            }

            missingHint = BuildMissingMaterialsHint(recipe, inventory);
            return RecipeBookCraftability.MissingMaterials;
        }

        private static string BuildMissingMaterialsHint(CraftRecipe recipe, IItemContainer inventory)
        {
            if (recipe.ShapedPattern is { Count: > 0 } pattern)
            {
                var needs = new Dictionary<char, int>();
                foreach (string row in pattern)
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
                    int have = CountPatternSymbol(symbol, inventory);
                    if (have < count)
                    {
                        return $"Need {count - have}× {DescribePatternSymbol(symbol)}";
                    }
                }
            }
            else
            {
                foreach (var input in recipe.Inputs)
                {
                    int have = input.ExactBlock.HasValue
                        ? inventory.CountBlock(input.ExactBlock.Value)
                        : 0;
                    if (have < input.Count)
                    {
                        string name = input.ExactBlock?.ToString() ?? "material";
                        return $"Need {input.Count - have}× {name}";
                    }
                }
            }

            return "Missing ingredients";
        }

        private static int CountPatternSymbol(char symbol, IItemContainer inventory) => symbol switch
        {
            'P' => inventory.CountBlock(BlockType.OakPlank)
                   + inventory.CountBlock(BlockType.BirchPlank)
                   + inventory.CountBlock(BlockType.PinePlank),
            'T' => CountMaterial(inventory, ItemId.Stick),
            'L' => CountLogs(inventory),
            'S' => inventory.CountBlock(BlockType.Stone),
            'C' => inventory.CountBlock(BlockType.Cobblestone),
            'I' => inventory.CountBlock(BlockType.IronBlock),
            'G' => inventory.CountBlock(BlockType.GoldBlock),
            _ => 0
        };

        private static int CountLogs(IItemContainer inventory)
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

        private static string FormatIngredients(CraftRecipe recipe)
        {
            if (recipe.ShapedPattern is { Count: > 0 } pattern)
            {
                var needs = new Dictionary<char, int>();
                foreach (string row in pattern)
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

                return string.Join(" + ", needs.Select(kvp => $"{kvp.Value}× {DescribePatternSymbol(kvp.Key)}"));
            }

            if (recipe.Inputs.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(" + ", recipe.Inputs.Select(i =>
                $"{i.Count}× {(i.ExactBlock?.ToString() ?? "item")}"));
        }

        private static string FormatOutput(CraftRecipe recipe)
        {
            if (recipe.IsToolOutput)
            {
                return ToolRegistry.TryGet(recipe.OutputItem, out var def) ? def.DisplayName : recipe.DisplayName;
            }

            if (recipe.IsFoodOutput || recipe.IsMaterialOutput)
            {
                return $"{recipe.OutputCount}× {recipe.DisplayName}";
            }

            return $"{recipe.OutputCount}× {recipe.Output}";
        }

        private static string DescribePatternSymbol(char symbol) => symbol switch
        {
            'P' => "Plank",
            'T' => "Stick",
            'L' => "Log",
            'S' => "Stone",
            'C' => "Cobblestone",
            'I' => "Iron",
            'G' => "Gold",
            _ => symbol.ToString()
        };
    }
}
