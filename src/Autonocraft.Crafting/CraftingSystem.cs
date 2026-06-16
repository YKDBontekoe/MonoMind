using Autonocraft.Core;
using Autonocraft.Engine.Animation;
using Autonocraft.Items;
using Autonocraft.World;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Crafting
{
    public sealed class CrucibleSession
    {
        public const int MaxSlots = 9;

        public int StationX { get; private set; }
        public int StationY { get; private set; }
        public int StationZ { get; private set; }
        public BlockType StationType { get; private set; }
        public bool IsOpen { get; private set; }

        public ItemStack[] InputSlots { get; } = new ItemStack[MaxSlots];

        public CraftGridSize GridSize => StationType switch
        {
            BlockType.StationBench => CraftGridSize.ThreeByThree,
            _ => CraftGridSize.TwoByTwo
        };

        public int ActiveSlotCount => (int)GridSize * (int)GridSize;

        public void Open(int x, int y, int z, BlockType stationType)
        {
            StationX = x;
            StationY = y;
            StationZ = z;
            StationType = stationType;
            IsOpen = true;
            ClearSlots();
        }

        public void Close()
        {
            IsOpen = false;
            ClearSlots();
        }

        private void ClearSlots()
        {
            for (int i = 0; i < InputSlots.Length; i++)
            {
                InputSlots[i] = ItemStack.Empty;
            }
        }

        public bool DepositFromHotbar(Player player, int targetIndex = -1)
        {
            ref var hotbarSlot = ref player.Hotbar[player.SelectedSlot];
            if (hotbarSlot.IsEmpty || (!hotbarSlot.IsBlock() && !hotbarSlot.IsMaterial()))
            {
                return false;
            }

            if (targetIndex >= 0)
            {
                if (targetIndex >= ActiveSlotCount || !InputSlots[targetIndex].IsEmpty)
                {
                    return false;
                }

                InputSlots[targetIndex] = TakeOne(ref hotbarSlot);
                return true;
            }

            for (int i = 0; i < ActiveSlotCount; i++)
            {
                if (InputSlots[i].IsEmpty)
                {
                    InputSlots[i] = TakeOne(ref hotbarSlot);
                    return true;
                }
            }

            return false;
        }

        public bool WithdrawToHotbar(Player player, int inputIndex)
        {
            if (inputIndex < 0 || inputIndex >= ActiveSlotCount)
            {
                return false;
            }

            var stack = InputSlots[inputIndex];
            if (stack.IsEmpty)
            {
                return false;
            }

            player.AddItem(stack);
            InputSlots[inputIndex] = ItemStack.Empty;
            return true;
        }

        private static ItemStack TakeOne(ref ItemStack source)
        {
            if (source.IsBlock())
            {
                var result = ItemStack.CreateBlock(source.BlockType, 1);
                source.Count--;
                if (source.Count <= 0)
                {
                    source = ItemStack.Empty;
                }

                return result;
            }

            if (source.IsMaterial())
            {
                var result = ItemStack.CreateMaterial(source.MaterialId, 1);
                source.Count--;
                if (source.Count <= 0)
                {
                    source = ItemStack.Empty;
                }

                return result;
            }

            return ItemStack.Empty;
        }

        public CraftPreview GetPreview(DiscoveryJournal journal, CraftEnvironment env) =>
            GridCrafting.Preview(ToCraftingGrid(), StationType, journal, env);

        public CraftingGrid ToCraftingGrid()
        {
            var grid = new CraftingGrid();
            grid.SetSize(GridSize);
            for (int i = 0; i < grid.SlotCount; i++)
            {
                grid.SetSlot(i, InputSlots[i]);
            }

            return grid;
        }

        public void ApplyGrid(CraftingGrid grid)
        {
            for (int i = 0; i < ActiveSlotCount; i++)
            {
                InputSlots[i] = grid.GetSlot(i);
            }
        }
    }

    public sealed class CraftingSystem
    {
        public DiscoveryJournal Journal { get; } = new();
        public CrucibleSession Crucible { get; } = new();
        public CraftingGrid PlayerCraftGrid { get; } = new();
        public ItemStack CraftCursor { get; set; } = ItemStack.Empty;
        public bool InventoryOpen { get; private set; }
        public bool JournalOpen { get; private set; }
        public bool RecipeBookOpen { get; private set; }
        public UiTransition JournalTransition { get; } = new();
        public bool ShowCraftingHint { get; set; } = true;
        public Action<string>? OnDiscoveryUnlocked { get; set; }

        public CraftingSystem()
        {
            JournalTransition.SnapHidden();
            UnlockDefaultToolRecipes();
        }

        public void LoadJournal(IEnumerable<string>? ids)
        {
            Journal.Load(ids);
            UnlockDefaultToolRecipes();
        }

        private void UnlockDefaultToolRecipes()
        {
            Journal.Unlock("recipe:plank");
            Journal.Unlock("recipe:wood_pickaxe");
            Journal.Unlock("recipe:wood_axe");
            Journal.Unlock("recipe:wood_shovel");
            Journal.Unlock("recipe:wood_sword");
        }

        private static void UnlockStoneToolRecipes(DiscoveryJournal journal)
        {
            journal.Unlock("recipe:stone_pickaxe");
            journal.Unlock("recipe:stone_axe");
            journal.Unlock("recipe:stone_shovel");
            journal.Unlock("recipe:stone_sword");
            journal.Unlock("recipe:bread");
        }

        private static void UnlockIronToolRecipes(DiscoveryJournal journal)
        {
            journal.Unlock("recipe:iron_pickaxe");
            journal.Unlock("recipe:iron_axe");
            journal.Unlock("recipe:iron_shovel");
            journal.Unlock("recipe:iron_sword");
        }

        private static void UnlockGoldToolRecipes(DiscoveryJournal journal)
        {
            journal.Unlock("recipe:gold_pickaxe");
            journal.Unlock("recipe:gold_axe");
            journal.Unlock("recipe:gold_shovel");
            journal.Unlock("recipe:gold_sword");
        }

        public SigilActivationResult TryActivateSigil(VoxelWorld world, int cx, int cy, int cz, GraphicsDevice? device)
        {
            var preview = PreviewSigil(world, cx, cy, cz);
            if (preview.Success && preview.Pattern != null)
            {
                ApplySigilActivation(world, cx, cy, cz, preview.Pattern, device);
            }

            return preview;
        }

        public SigilActivationResult PreviewSigil(VoxelWorld world, int cx, int cy, int cz)
        {
            if (SigilMatcher.TryMatch(world, cx, cy, cz, out SigilPattern? pattern, out float partialScore) && pattern != null)
            {
                return new SigilActivationResult(true, false, pattern);
            }

            bool partial = partialScore >= 0.5f && partialScore < 1f;
            return new SigilActivationResult(false, partial, pattern);
        }

        public void ApplySigilActivation(VoxelWorld world, int cx, int cy, int cz, SigilPattern pattern, GraphicsDevice? device)
        {
            foreach (var (x, y, z) in pattern.GetConsumedPositions(cx, cy, cz))
            {
                world.SetBlock(x, y, z, BlockType.Air, device);
            }

            world.SetBlock(cx, cy, cz, pattern.OutputStation, device);
            Journal.Unlock(pattern.Id);
            OnDiscoveryUnlocked?.Invoke($"Discovered {pattern.DisplayName}");
            if (pattern.OutputStation == BlockType.StationBench)
            {
                UnlockStoneToolRecipes(Journal);
            }
            else if (pattern.OutputStation == BlockType.StationForge)
            {
                Journal.Unlock("recipe:cooked_meat");
            }

            ShowCraftingHint = false;
        }

        public CraftAttemptResult TryTransmute(VoxelWorld world, Player player, float timeOfDay)
        {
            if (!Crucible.IsOpen)
            {
                return CraftAttemptResult.Fail("Crucible closed");
            }

            bool hasFuel = Crucible.InputSlots.Any(t => t.IsBlock() && t.BlockType == BlockType.CoalOre);
            var env = CraftEnvironment.Sample(world, Crucible.StationX, Crucible.StationY, Crucible.StationZ, timeOfDay, hasFuel);

            foreach (var recipe in CraftRecipeRegistry.AvailableForStation(Crucible.StationType, Journal))
            {
                if (!recipe.EnvironmentMatches(env) || !recipe.IsFoodInput)
                {
                    continue;
                }

                if (!TryConsumeFoodFromPlayer(player, recipe.InputFood, recipe.InputFoodCount))
                {
                    continue;
                }

                player.AddItem(ItemStack.CreateFood(recipe.OutputItem, recipe.OutputCount));
                Journal.Unlock(recipe.Id);
                RecipeDiscovery.UnlockRelated(Journal, recipe.Id);
                player.Stats.RecordItemCrafted();
                OnDiscoveryUnlocked?.Invoke($"Unlocked {recipe.DisplayName}");
                return CraftAttemptResult.Success(recipe);
            }

            var benchGrid = Crucible.ToCraftingGrid();
            var matched = GridCrafting.FindMatch(benchGrid, Crucible.StationType, Journal, env);

            if (matched == null || !matched.TryMatchItemGrid(benchGrid.GetItemStacks(), (int)Crucible.GridSize, out var consumption))
            {
                return CraftAttemptResult.Fail("No matching transmutation");
            }

            benchGrid.ConsumeSlots(consumption);
            Crucible.ApplyGrid(benchGrid);

            if (matched.IsToolOutput)
            {
                player.AddItem(ToolRegistry.CreateStack(matched.OutputItem));
            }
            else if (matched.IsFoodOutput)
            {
                player.AddItem(ItemStack.CreateFood(matched.OutputItem, matched.OutputCount));
            }
            else if (matched.IsMaterialOutput)
            {
                player.AddItem(ItemStack.CreateMaterial(matched.OutputItem, matched.OutputCount));
            }
            else
            {
                player.GiveBlocks(matched.Output, matched.OutputCount);
            }

            Journal.Unlock(matched.Id);
            UnlockRecipesForCraft(matched.Id);
            RecipeDiscovery.UnlockRelated(Journal, matched.Id);
            player.Stats.RecordItemCrafted();
            OnDiscoveryUnlocked?.Invoke($"Unlocked {matched.DisplayName}");
            return CraftAttemptResult.Success(matched);
        }

        public CraftAttemptResult TryTransmuteToContainer(VoxelWorld world, IItemContainer output, float timeOfDay, ItemStack[] inputSlots, BlockType stationType)
        {
            bool hasFuel = inputSlots.Any(t => t.IsBlock() && t.BlockType == BlockType.CoalOre);
            var env = CraftEnvironment.Sample(world, Crucible.StationX, Crucible.StationY, Crucible.StationZ, timeOfDay, hasFuel);

            var grid = new CraftingGrid();
            grid.SetSize(InferGridSize(inputSlots.Length));
            for (int i = 0; i < grid.SlotCount; i++)
            {
                grid.SetSlot(i, i < inputSlots.Length ? inputSlots[i] : ItemStack.Empty);
            }

            foreach (var recipe in CraftRecipeRegistry.AvailableForStation(stationType, Journal))
            {
                if (!recipe.EnvironmentMatches(env))
                {
                    continue;
                }

                if (!recipe.TryMatchItemGrid(grid.GetItemStacks(), (int)grid.Size, out _))
                {
                    continue;
                }

                if (recipe.IsToolOutput)
                {
                    output.AddItem(ToolRegistry.CreateStack(recipe.OutputItem));
                }
                else if (recipe.IsMaterialOutput)
                {
                    output.AddItem(ItemStack.CreateMaterial(recipe.OutputItem, recipe.OutputCount));
                }
                else if (recipe.IsFoodOutput)
                {
                    output.AddItem(ItemStack.CreateFood(recipe.OutputItem, recipe.OutputCount));
                }
                else
                {
                    output.AddItem(ItemStack.CreateBlock(recipe.Output, recipe.OutputCount));
                }

                Journal.Unlock(recipe.Id);
                RecipeDiscovery.UnlockRelated(Journal, recipe.Id);
                return CraftAttemptResult.Success(recipe);
            }

            return CraftAttemptResult.Fail("No matching transmutation");
        }

        private void UnlockRecipesForCraft(string recipeId)
        {
            switch (recipeId)
            {
                case "recipe:iron_block":
                    UnlockIronToolRecipes(Journal);
                    break;
                case "recipe:gold_block":
                    UnlockGoldToolRecipes(Journal);
                    break;
            }
        }

        private static bool TryConsumeFoodFromPlayer(Player player, ItemId foodId, int count)
        {
            int remaining = count;
            for (int i = 0; i < player.Hotbar.Length && remaining > 0; i++)
            {
                ref var slot = ref player.Hotbar[i];
                if (!slot.IsFood() || slot.FoodId != foodId)
                {
                    continue;
                }

                int take = Math.Min(slot.Count, remaining);
                slot.Count -= take;
                if (slot.Count <= 0)
                {
                    slot = ItemStack.Empty;
                }

                remaining -= take;
            }

            return remaining <= 0;
        }

        public CraftEnvironment GetCurrentEnvironment(VoxelWorld world, float timeOfDay)
        {
            if (!Crucible.IsOpen)
            {
                return CraftEnvironment.Sample(world, 0, 0, 0, timeOfDay);
            }

            bool hasFuel = Crucible.InputSlots.Any(t => t.IsBlock() && t.BlockType == BlockType.CoalOre);
            return CraftEnvironment.Sample(world, Crucible.StationX, Crucible.StationY, Crucible.StationZ, timeOfDay, hasFuel);
        }

        public void ToggleRecipeBook() => RecipeBookOpen = !RecipeBookOpen;

        public void CloseRecipeBook() => RecipeBookOpen = false;

        public bool TryApplyRecipeBookSelection(CraftRecipe recipe, Player player)
        {
            var inventory = new PlayerInventoryAdapter(player);
            if (InventoryOpen)
            {
                return RecipeBookResolver.TryFillGrid(recipe, PlayerCraftGrid, inventory);
            }

            if (Crucible.IsOpen)
            {
                return RecipeBookResolver.TryFillBenchSlots(
                    recipe,
                    Crucible.InputSlots,
                    Crucible.ActiveSlotCount,
                    inventory);
            }

            return false;
        }

        public void OpenCrucible(int x, int y, int z, BlockType stationType)
        {
            Crucible.Open(x, y, z, stationType);
        }

        public void CloseCrucible() => Crucible.Close();

        public void ToggleJournal()
        {
            if (JournalOpen)
            {
                JournalTransition.BeginFadeOut(0.2f);
                JournalOpen = false;
            }
            else
            {
                JournalOpen = true;
                JournalTransition.BeginFadeInSlideUp(0.2f, 12f);
            }
        }

        public void CloseJournal()
        {
            JournalOpen = false;
            if (JournalTransition.Alpha > 0.01f || JournalTransition.IsAnimating)
            {
                JournalTransition.BeginFadeOut(0.2f);
            }
            else
            {
                JournalTransition.SnapHidden();
            }
        }

        public void UpdateJournal(float deltaTime) => JournalTransition.Update(deltaTime);

        public bool ShouldDrawJournal() =>
            JournalOpen || JournalTransition.IsAnimating || JournalTransition.Alpha > 0.01f;

        public bool IsJournalUiBlocking =>
            JournalOpen || JournalTransition.IsAnimating || JournalTransition.Alpha > 0.01f;

        public CraftPreview GetPlayerCraftPreview() =>
            GridCrafting.Preview(PlayerCraftGrid, BlockType.StationBench, Journal);

        public CraftAttemptResult TryPlayerCraft(Player player)
        {
            var result = GridCrafting.TryCraft(
                PlayerCraftGrid,
                new PlayerInventoryAdapter(player),
                BlockType.StationBench,
                Journal,
                onUnlock: UnlockRecipesForCraft);

            if (result.Succeeded)
            {
                player.Stats.RecordItemCrafted();
                OnDiscoveryUnlocked?.Invoke($"Crafted {result.Recipe!.DisplayName}");
            }

            return result;
        }

        public void ToggleInventory()
        {
            if (InventoryOpen)
            {
                InventoryOpen = false;
            }
            else
            {
                OpenInventory();
            }
        }

        public void OpenInventory()
        {
            PlayerCraftGrid.SetSize(CraftGridSize.TwoByTwo);
            InventoryOpen = true;
        }

        public void CloseInventory() => InventoryOpen = false;

        public void CloseAll()
        {
            CloseCrucible();
            CloseJournal();
            CloseInventory();
            CloseRecipeBook();
        }

        private static CraftGridSize InferGridSize(int slotCount) =>
            slotCount >= 9 ? CraftGridSize.ThreeByThree : CraftGridSize.TwoByTwo;
    }

    public readonly struct SigilActivationResult
    {
        public bool Success { get; init; }
        public bool PartialMatch { get; init; }
        public SigilPattern? Pattern { get; init; }

        public SigilActivationResult(bool success, bool partialMatch, SigilPattern? pattern)
        {
            Success = success;
            PartialMatch = partialMatch;
            Pattern = pattern;
        }
    }

    public readonly struct CraftAttemptResult
    {
        public bool Succeeded { get; init; }
        public string Message { get; init; }
        public CraftRecipe? Recipe { get; init; }

        public static CraftAttemptResult Success(CraftRecipe recipe) =>
            new() { Succeeded = true, Recipe = recipe, Message = recipe.DisplayName };

        public static CraftAttemptResult Fail(string message) =>
            new() { Succeeded = false, Message = message };
    }
}
