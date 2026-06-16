using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public static class GridCrafting
    {
        public static CraftRecipe? FindMatch(
            BlockType[] slots,
            CraftGridSize gridSize,
            BlockType stationType,
            DiscoveryJournal journal,
            CraftEnvironment? env = null) =>
            FindMatch(ToItemStacks(slots), gridSize, stationType, journal, env);

        public static CraftRecipe? FindMatch(
            CraftingGrid grid,
            BlockType stationType,
            DiscoveryJournal journal,
            CraftEnvironment? env = null) =>
            FindMatch(grid.GetItemStacks(), grid.Size, stationType, journal, env);

        public static CraftRecipe? FindMatch(
            IReadOnlyList<ItemStack> slots,
            CraftGridSize gridSize,
            BlockType stationType,
            DiscoveryJournal journal,
            CraftEnvironment? env = null)
        {
            var candidates = CraftRecipeRegistry.AvailableForStation(stationType, journal).ToList();
            var shaped = candidates.Where(r => r.ShapedPattern is { Count: > 0 });
            var shapeless = candidates.Where(r => r.ShapedPattern is not { Count: > 0 });

            foreach (var pass in new[] { shaped, shapeless })
            {
                foreach (var recipe in pass)
                {
                    if (recipe.GridSize > gridSize)
                    {
                        continue;
                    }

                    if (env != null && !recipe.EnvironmentMatches(env))
                    {
                        continue;
                    }

                    if (recipe.IsFoodInput)
                    {
                        continue;
                    }

                    if (recipe.TryMatchItemGrid(slots, (int)gridSize, out _))
                    {
                        return recipe;
                    }
                }
            }

            return null;
        }

        public static CraftPreview Preview(
            CraftingGrid grid,
            BlockType stationType,
            DiscoveryJournal journal,
            CraftEnvironment? env = null)
        {
            var recipe = FindMatch(grid, stationType, journal, env);
            if (recipe == null)
            {
                return CraftPreview.Empty;
            }

            if (recipe.IsToolOutput)
            {
                return new CraftPreview(ToolRegistry.CreateStack(recipe.OutputItem), recipe);
            }

            if (recipe.IsFoodOutput)
            {
                return new CraftPreview(ItemStack.CreateFood(recipe.OutputItem, recipe.OutputCount), recipe);
            }

            if (recipe.IsMaterialOutput)
            {
                return new CraftPreview(ItemStack.CreateMaterial(recipe.OutputItem, recipe.OutputCount), recipe);
            }

            return new CraftPreview(ItemStack.CreateBlock(recipe.Output, recipe.OutputCount), recipe);
        }

        public static CraftAttemptResult TryCraft(
            CraftingGrid grid,
            IItemContainer output,
            BlockType stationType,
            DiscoveryJournal journal,
            CraftEnvironment? env = null,
            Action<string>? onUnlock = null)
        {
            var slots = grid.GetItemStacks();
            var recipe = FindMatch(slots, grid.Size, stationType, journal, env);
            if (recipe == null)
            {
                return CraftAttemptResult.Fail("No matching recipe");
            }

            if (!recipe.TryMatchItemGrid(slots, (int)grid.Size, out var consumption))
            {
                return CraftAttemptResult.Fail("Recipe no longer matches");
            }

            ItemStack result;
            if (recipe.IsToolOutput)
            {
                result = ToolRegistry.CreateStack(recipe.OutputItem);
            }
            else if (recipe.IsFoodOutput)
            {
                result = ItemStack.CreateFood(recipe.OutputItem, recipe.OutputCount);
            }
            else if (recipe.IsMaterialOutput)
            {
                result = ItemStack.CreateMaterial(recipe.OutputItem, recipe.OutputCount);
            }
            else
            {
                result = ItemStack.CreateBlock(recipe.Output, recipe.OutputCount);
            }

            if (!output.HasSpaceFor(result))
            {
                return CraftAttemptResult.Fail("Inventory full");
            }

            grid.ConsumeSlots(consumption);
            output.AddItem(result);
            journal.Unlock(recipe.Id);
            RecipeDiscovery.UnlockRelated(journal, recipe.Id);
            onUnlock?.Invoke(recipe.Id);
            return CraftAttemptResult.Success(recipe);
        }

        private static ItemStack[] ToItemStacks(BlockType[] slots)
        {
            var stacks = new ItemStack[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                stacks[i] = slots[i] == BlockType.Air
                    ? ItemStack.Empty
                    : ItemStack.CreateBlock(slots[i], 1);
            }

            return stacks;
        }
    }

    public readonly struct CraftPreview
    {
        public static CraftPreview Empty => new(ItemStack.Empty, null);

        public ItemStack Result { get; }
        public CraftRecipe? Recipe { get; }

        public bool HasMatch => Recipe != null && !Result.IsEmpty;

        public CraftPreview(ItemStack result, CraftRecipe? recipe)
        {
            Result = result;
            Recipe = recipe;
        }
    }
}
