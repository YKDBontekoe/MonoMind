using System;
using System.Collections.Generic;
using Autonocraft.Crafting;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public static class VillageWorkshopCrafting
    {
        public const int RepairPlankCost = 1;
        public const float RepairFraction = 0.25f;
        public const int MinimumToolsPerType = 1;

        private static readonly string[] WorkshopRecipeIds =
        {
            "recipe:wood_pickaxe",
            "recipe:wood_axe",
            "recipe:wood_shovel",
            "recipe:plank",
            "recipe:stone_pickaxe",
            "recipe:stone_axe",
            "recipe:stone_shovel",
            "recipe:cobblestone"
        };

        public static bool NeedsSmithWork(VillageStorage storage, bool creative = false)
        {
            if (storage.CountTools(ToolType.Pickaxe) < MinimumToolsPerType ||
                storage.CountTools(ToolType.Axe) < MinimumToolsPerType)
            {
                return true;
            }

            if (storage.TryFindDamagedTool(ToolType.Pickaxe, out _) ||
                storage.TryFindDamagedTool(ToolType.Axe, out _))
            {
                return true;
            }

            return HasCraftableRecipe(storage, creative);
        }

        public static bool HasCraftableRecipe(VillageStorage storage, bool creative = false)
            => TryFindRecipe(storage, out _, creative);

        public static bool TrySmithWork(VillageStorage storage, bool creative = false)
        {
            if (TryRepairTool(storage, ToolType.Pickaxe, creative))
            {
                return true;
            }

            if (TryRepairTool(storage, ToolType.Axe, creative))
            {
                return true;
            }

            return TryCraftBest(storage, creative);
        }

        public static bool TryCraftBest(VillageStorage storage, bool creative = false)
        {
            if (!TryFindRecipe(storage, out var recipe, creative))
            {
                return false;
            }

            return TryExecute(storage, recipe, creative);
        }

        private static bool TryRepairTool(VillageStorage storage, ToolType toolType, bool creative)
        {
            if (!storage.TryFindDamagedTool(toolType, out int slotIndex))
            {
                return false;
            }

            var stack = storage.GetSlot(slotIndex);
            if (!creative && !storage.TryConsumeBlock(BlockType.OakPlank, RepairPlankCost))
            {
                return false;
            }

            int repairAmount = Math.Max(1, (int)MathF.Ceiling(stack.MaxDurability * RepairFraction));
            return storage.TryRepairTool(slotIndex, repairAmount);
        }

        private static bool TryFindRecipe(VillageStorage storage, out CraftRecipe recipe, bool creative)
        {
            recipe = null!;
            var slotTypes = GetSlotBlockTypes(storage);

            foreach (string recipeId in WorkshopRecipeIds)
            {
                var candidate = FindRecipe(recipeId);
                if (candidate == null || candidate.RequiresHeat || candidate.RequiresWater || candidate.RequiresUnlock)
                {
                    continue;
                }

                if (candidate.IsToolOutput &&
                    ToolRegistry.TryGet(candidate.OutputItem, out var toolDef) &&
                    storage.CountTools(toolDef.ToolType) >= MinimumToolsPerType)
                {
                    continue;
                }

                if (!creative && !candidate.TryMatchInputs(slotTypes, out _))
                {
                    continue;
                }

                if (!HasOutputSpace(storage, candidate))
                {
                    continue;
                }

                recipe = candidate;
                return true;
            }

            return false;
        }

        private static bool TryExecute(VillageStorage storage, CraftRecipe recipe, bool creative)
        {
            if (!creative)
            {
                var slotTypes = GetSlotBlockTypes(storage);
                if (!recipe.TryMatchInputs(slotTypes, out var consumption))
                {
                    return false;
                }

                foreach (var (slot, amount) in consumption)
                {
                    var stack = storage.GetSlot(slot);
                    if (!stack.IsBlock())
                    {
                        return false;
                    }

                    if (amount >= stack.Count)
                    {
                        storage.SetSlot(slot, ItemStack.Empty);
                    }
                    else
                    {
                        stack.Count -= amount;
                        storage.SetSlot(slot, stack);
                    }
                }
            }

            if (recipe.IsToolOutput)
            {
                return storage.AddItem(ToolRegistry.CreateStack(recipe.OutputItem));
            }

            return storage.AddItem(ItemStack.CreateBlock(recipe.Output, recipe.OutputCount));
        }

        private static bool HasOutputSpace(VillageStorage storage, CraftRecipe recipe)
        {
            if (recipe.IsToolOutput)
            {
                for (int i = 0; i < storage.SlotCount; i++)
                {
                    if (storage.GetSlot(i).IsEmpty)
                    {
                        return true;
                    }
                }

                return false;
            }

            return storage.HasSpaceFor(ItemStack.CreateBlock(recipe.Output, recipe.OutputCount));
        }

        private static List<BlockType> GetSlotBlockTypes(VillageStorage storage)
        {
            var slotTypes = new List<BlockType>(storage.SlotCount);
            for (int i = 0; i < storage.SlotCount; i++)
            {
                var stack = storage.GetSlot(i);
                slotTypes.Add(stack.IsBlock() ? stack.BlockType : BlockType.Air);
            }

            return slotTypes;
        }

        private static CraftRecipe? FindRecipe(string recipeId)
        {
            foreach (var recipe in CraftRecipeRegistry.All)
            {
                if (recipe.Id == recipeId && recipe.StationType == BlockType.StationBench)
                {
                    return recipe;
                }
            }

            return null;
        }
    }
}
