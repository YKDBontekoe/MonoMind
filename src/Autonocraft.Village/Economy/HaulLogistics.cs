using System;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public enum HaulItemCategory
    {
        BuildMaterial = 0,
        Ore = 1,
        Food = 2,
        General = 3
    }

    public static class HaulLogistics
    {
        public static HaulItemCategory Categorize(BlockType block) => block switch
        {
            BlockType.HayBale => HaulItemCategory.Food,
            BlockType.Wheat or BlockType.Carrot => HaulItemCategory.Food,
            BlockType.CoalOre or BlockType.IronOre or BlockType.GoldOre => HaulItemCategory.Ore,
            BlockType.Stone or BlockType.Cobblestone or BlockType.OakPlank or BlockType.OakLog
                or BlockType.Dirt or BlockType.Gravel or BlockType.BirchLog or BlockType.PineLog
                or BlockType.WillowLog or BlockType.PalmLog => HaulItemCategory.BuildMaterial,
            _ => HaulItemCategory.General
        };

        public static int GetCategoryPriority(HaulItemCategory category) => (int)category;

        public static Vector3 GetOutputChestPosition(BuildingBlueprint blueprint, int anchorX, int anchorY, int anchorZ)
        {
            foreach (var block in blueprint.Template.Blocks)
            {
                if (block.Type == BlockType.OakLog && block.Dy > 0)
                {
                    return new Vector3(anchorX + block.Dx + 0.5f, anchorY + block.Dy, anchorZ + block.Dz + 0.5f);
                }
            }

            return new Vector3(anchorX + 0.5f, anchorY, anchorZ + 0.5f);
        }

        public static bool SiteNeedsBlock(BuildingSite site, BlockType blockType)
        {
            foreach (var pending in site.PendingBlocks)
            {
                if (pending.Type == blockType)
                {
                    return true;
                }
            }

            return false;
        }

        public static Vector3 ResolveDeliveryTarget(Village village, BlockType blockType, Vector3 from, out bool toFoodStock)
        {
            toFoodStock = false;
            var category = Categorize(blockType);

            if (category == HaulItemCategory.Food)
            {
                toFoodStock = true;
                var kitchen = village.GetNearestBuilding(BuildingKind.TownHeart, from)
                    ?? village.GetNearestBuilding(BuildingKind.FarmPlot, from);
                if (kitchen != null)
                {
                    return new Vector3(kitchen.AnchorX + 0.5f, kitchen.AnchorY, kitchen.AnchorZ + 0.5f);
                }

                return village.StoragePosition;
            }

            if (category == HaulItemCategory.Ore)
            {
                var storage = village.GetNearestBuilding(BuildingKind.Storage, from);
                if (storage != null)
                {
                    return new Vector3(storage.AnchorX + 0.5f, storage.AnchorY, storage.AnchorZ + 0.5f);
                }

                return village.StoragePosition;
            }

            if (category == HaulItemCategory.BuildMaterial)
            {
                BuildingSite? bestSite = null;
                float bestDist = float.MaxValue;
                foreach (var site in village.BuildingSites)
                {
                    if (site.IsComplete || !SiteNeedsBlock(site, blockType))
                    {
                        continue;
                    }

                    var pos = new Vector3(site.AnchorX + 0.5f, site.AnchorY, site.AnchorZ + 0.5f);
                    float dist = Vector3.DistanceSquared(from, pos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestSite = site;
                    }
                }

                if (bestSite != null)
                {
                    return new Vector3(bestSite.AnchorX + 0.5f, bestSite.AnchorY, bestSite.AnchorZ + 0.5f);
                }
            }

            var storageBuilding = village.GetNearestBuilding(BuildingKind.Storage, from);
            if (storageBuilding != null)
            {
                return new Vector3(storageBuilding.AnchorX + 0.5f, storageBuilding.AnchorY, storageBuilding.AnchorZ + 0.5f);
            }

            return village.StoragePosition;
        }

        public static bool TryGetHighestPriorityStack(Inventory inventory, out int slotIndex, out ItemStack stack)
        {
            slotIndex = -1;
            stack = ItemStack.Empty;
            int bestPriority = int.MaxValue;

            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var candidate = inventory.GetSlot(i);
                if (candidate.IsEmpty || !candidate.IsBlock())
                {
                    continue;
                }

                int priority = GetCategoryPriority(Categorize(candidate.BlockType));
                if (priority >= bestPriority)
                {
                    continue;
                }

                bestPriority = priority;
                slotIndex = i;
                stack = candidate;
            }

            return slotIndex >= 0;
        }

        public static void OffloadInventoryToChest(Villager worker, OutputChest chest)
        {
            for (int i = 0; i < worker.Inventory.SlotCount; i++)
            {
                var stack = worker.Inventory.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                if (chest.Buffer.AddItem(stack))
                {
                    worker.Inventory.SetSlot(i, ItemStack.Empty);
                }
            }
        }

        public static bool TryPickupChestToHauler(OutputChest chest, Villager hauler)
        {
            bool moved = false;
            for (int i = 0; i < chest.Buffer.SlotCount; i++)
            {
                var stack = chest.Buffer.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                if (hauler.Inventory.AddItem(stack))
                {
                    chest.Buffer.SetSlot(i, ItemStack.Empty);
                    moved = true;
                }
            }

            return moved;
        }

        public static bool TryPickupVillagerToHauler(Villager source, Villager hauler)
        {
            bool moved = false;
            for (int i = 0; i < source.Inventory.SlotCount; i++)
            {
                var stack = source.Inventory.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                if (hauler.Inventory.AddItem(stack))
                {
                    source.Inventory.SetSlot(i, ItemStack.Empty);
                    moved = true;
                }
            }

            return moved;
        }

        public static bool IsCarryFull(Inventory inventory) =>
            !inventory.HasSpaceFor(ItemStack.CreateBlock(BlockType.Stone, 1));
    }
}
