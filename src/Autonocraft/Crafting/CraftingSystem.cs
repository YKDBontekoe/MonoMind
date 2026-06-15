using Autonocraft.Core;
using Autonocraft.Engine.Animation;
using Autonocraft.Items;
using Autonocraft.World;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Crafting
{
    public sealed class CrucibleSession
    {
        public int StationX { get; private set; }
        public int StationY { get; private set; }
        public int StationZ { get; private set; }
        public BlockType StationType { get; private set; }
        public bool IsOpen { get; private set; }

        public BlockType[] InputSlots { get; } = new BlockType[4];

        public void Open(int x, int y, int z, BlockType stationType)
        {
            StationX = x;
            StationY = y;
            StationZ = z;
            StationType = stationType;
            IsOpen = true;
            Array.Fill(InputSlots, BlockType.Air);
        }

        public void Close()
        {
            IsOpen = false;
            Array.Fill(InputSlots, BlockType.Air);
        }

        public bool DepositFromHotbar(Player player, int targetIndex = -1)
        {
            var slot = player.Hotbar[player.SelectedSlot];
            if (!slot.IsBlock())
            {
                return false;
            }

            if (targetIndex >= 0)
            {
                if (targetIndex >= InputSlots.Length || InputSlots[targetIndex] != BlockType.Air)
                {
                    return false;
                }

                InputSlots[targetIndex] = slot.BlockType;
                player.UseSelectedBlock();
                return true;
            }

            for (int i = 0; i < InputSlots.Length; i++)
            {
                if (InputSlots[i] == BlockType.Air)
                {
                    InputSlots[i] = slot.BlockType;
                    player.UseSelectedBlock();
                    return true;
                }
            }

            return false;
        }

        public bool WithdrawToHotbar(Player player, int inputIndex)
        {
            if (inputIndex < 0 || inputIndex >= InputSlots.Length)
            {
                return false;
            }

            BlockType type = InputSlots[inputIndex];
            if (type == BlockType.Air)
            {
                return false;
            }

            player.GiveBlocks(type, 1);
            InputSlots[inputIndex] = BlockType.Air;
            return true;
        }
    }

    public sealed class CraftingSystem
    {
        public DiscoveryJournal Journal { get; } = new();
        public CrucibleSession Crucible { get; } = new();
        public bool JournalOpen { get; private set; }
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

        public void ResetForNewWorld()
        {
            Journal.Load(Array.Empty<string>());
            ShowCraftingHint = false;
        }

        private void UnlockDefaultToolRecipes()
        {
            Journal.Unlock("recipe:wood_pickaxe");
            Journal.Unlock("recipe:wood_axe");
            Journal.Unlock("recipe:wood_shovel");
        }

        private static void UnlockStoneToolRecipes(DiscoveryJournal journal)
        {
            journal.Unlock("recipe:stone_pickaxe");
            journal.Unlock("recipe:stone_axe");
            journal.Unlock("recipe:stone_shovel");
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

            ShowCraftingHint = false;
        }

        public CraftAttemptResult TryTransmute(VoxelWorld world, Player player, float timeOfDay)
        {
            if (!Crucible.IsOpen)
            {
                return CraftAttemptResult.Fail("Crucible closed");
            }

            bool hasFuel = Crucible.InputSlots.Any(t => t == BlockType.CoalOre);
            var env = CraftEnvironment.Sample(world, Crucible.StationX, Crucible.StationY, Crucible.StationZ, timeOfDay, hasFuel);

            foreach (var recipe in CraftRecipeRegistry.AvailableForStation(Crucible.StationType, Journal))
            {
                if (!recipe.EnvironmentMatches(env))
                {
                    continue;
                }

                if (!recipe.TryMatchInputs(Crucible.InputSlots, out var consumption))
                {
                    continue;
                }

                foreach (var (slotIndex, _) in consumption)
                {
                    Crucible.InputSlots[slotIndex] = BlockType.Air;
                }

                if (recipe.IsToolOutput)
                {
                    player.AddItem(ToolRegistry.CreateStack(recipe.OutputItem));
                }
                else if (recipe.IsConsumableOutput)
                {
                    player.AddItem(ItemStack.CreateConsumable(recipe.OutputItem, recipe.OutputCount));
                }
                else
                {
                    player.GiveBlocks(recipe.Output, recipe.OutputCount);
                }

                Journal.Unlock(recipe.Id);
                UnlockRecipesForCraft(recipe.Id);
                player.Stats.RecordItemCrafted();
                OnDiscoveryUnlocked?.Invoke($"Unlocked {recipe.DisplayName}");
                return CraftAttemptResult.Success(recipe);
            }

            return CraftAttemptResult.Fail("No matching transmutation");
        }

        public CraftAttemptResult TryTransmuteToContainer(VoxelWorld world, IItemContainer output, float timeOfDay, BlockType[] inputSlots, BlockType stationType)
        {
            bool hasFuel = inputSlots.Any(t => t == BlockType.CoalOre);
            var env = CraftEnvironment.Sample(world, Crucible.StationX, Crucible.StationY, Crucible.StationZ, timeOfDay, hasFuel);

            foreach (var recipe in CraftRecipeRegistry.AvailableForStation(stationType, Journal))
            {
                if (!recipe.EnvironmentMatches(env))
                {
                    continue;
                }

                if (!recipe.TryMatchInputs(inputSlots, out _))
                {
                    continue;
                }

                if (recipe.IsToolOutput)
                {
                    output.AddItem(ToolRegistry.CreateStack(recipe.OutputItem));
                }
                else if (recipe.IsConsumableOutput)
                {
                    output.AddItem(ItemStack.CreateConsumable(recipe.OutputItem, recipe.OutputCount));
                }
                else
                {
                    output.AddItem(ItemStack.CreateBlock(recipe.Output, recipe.OutputCount));
                }

                Journal.Unlock(recipe.Id);
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

        public CraftEnvironment GetCurrentEnvironment(VoxelWorld world, float timeOfDay)
        {
            if (!Crucible.IsOpen)
            {
                return CraftEnvironment.Sample(world, 0, 0, 0, timeOfDay);
            }

            bool hasFuel = Crucible.InputSlots.Any(t => t == BlockType.CoalOre);
            return CraftEnvironment.Sample(world, Crucible.StationX, Crucible.StationY, Crucible.StationZ, timeOfDay, hasFuel);
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

        public void CloseAll()
        {
            CloseCrucible();
            CloseJournal();
        }
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
