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

        public static bool TrySmithWork(VillageStorage storage, bool creative = false, Action<string>? onCrafted = null, Action<string>? onRepaired = null)
        {
            if (TryCraftBasicWoodToolFallback(storage, onCrafted))
            {
                return true;
            }

            if (TryRepairTool(storage, ToolType.Pickaxe, creative, onRepaired))
            {
                return true;
            }

            if (TryRepairTool(storage, ToolType.Axe, creative, onRepaired))
            {
                return true;
            }

            return TryCraftBest(storage, creative, onCrafted);
        }

        public static bool TryCraftBest(VillageStorage storage, bool creative = false, Action<string>? onCrafted = null)
        {
            if (!TryFindRecipe(storage, out var recipe, creative))
            {
                return TryCraftBasicWoodToolFallback(storage, onCrafted);
            }

            return TryExecute(storage, recipe, creative, onCrafted) ||
                   TryCraftBasicWoodToolFallback(storage, onCrafted);
        }

        private static bool TryCraftBasicWoodToolFallback(VillageStorage storage, Action<string>? onCrafted)
        {
            if (storage.CountTools(ToolType.Pickaxe) < MinimumToolsPerType &&
                TryConsumeBasicWoodToolInputs(storage))
            {
                return AddFallbackTool(storage, ItemId.WoodPickaxe, onCrafted);
            }

            if (storage.CountTools(ToolType.Axe) < MinimumToolsPerType &&
                TryConsumeBasicWoodToolInputs(storage))
            {
                return AddFallbackTool(storage, ItemId.WoodAxe, onCrafted);
            }

            if (storage.CountTools(ToolType.Shovel) < MinimumToolsPerType &&
                TryConsumeBasicWoodToolInputs(storage))
            {
                return AddFallbackTool(storage, ItemId.WoodShovel, onCrafted);
            }

            return false;
        }

        private static bool TryConsumeBasicWoodToolInputs(VillageStorage storage)
        {
            if (!storage.TryConsumeBlock(BlockType.OakPlank, 2))
            {
                return false;
            }

            if (TryConsumeAnyLog(storage))
            {
                return true;
            }

            storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 2));
            return false;
        }

        private static bool TryConsumeAnyLog(VillageStorage storage)
        {
            return storage.TryConsumeBlock(BlockType.OakLog, 1) ||
                   storage.TryConsumeBlock(BlockType.BirchLog, 1) ||
                   storage.TryConsumeBlock(BlockType.PineLog, 1) ||
                   storage.TryConsumeBlock(BlockType.WillowLog, 1) ||
                   storage.TryConsumeBlock(BlockType.PalmLog, 1);
        }

        private static bool AddFallbackTool(VillageStorage storage, ItemId itemId, Action<string>? onCrafted)
        {
            var stack = ToolRegistry.CreateStack(itemId);
            if (!storage.AddItem(stack))
            {
                return false;
            }

            string outputName = ToolRegistry.TryGet(itemId, out var def) ? def.DisplayName : itemId.ToString();
            onCrafted?.Invoke(outputName);
            return true;
        }

        private static bool TryRepairTool(VillageStorage storage, ToolType toolType, bool creative, Action<string>? onRepaired)
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
            if (storage.TryRepairTool(slotIndex, repairAmount))
            {
                onRepaired?.Invoke(stack.GetDisplayName());
                return true;
            }
            return false;
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

        private static bool TryExecute(VillageStorage storage, CraftRecipe recipe, bool creative, Action<string>? onCrafted)
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

            string outputName = recipe.IsToolOutput
                ? (ToolRegistry.TryGet(recipe.OutputItem, out var def) ? def.DisplayName : recipe.OutputItem.ToString())
                : recipe.Output.ToString();

            if (recipe.IsToolOutput)
            {
                bool success = storage.AddItem(ToolRegistry.CreateStack(recipe.OutputItem));
                if (success)
                {
                    onCrafted?.Invoke(outputName);
                }
                return success;
            }

            bool res = storage.AddItem(ItemStack.CreateBlock(recipe.Output, recipe.OutputCount));
            if (res)
            {
                onCrafted?.Invoke(outputName);
            }
            return res;
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
