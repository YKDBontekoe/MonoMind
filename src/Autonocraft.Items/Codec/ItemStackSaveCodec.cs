using Autonocraft.Domain.Items;
using Autonocraft.Domain.Persistence;
using Autonocraft.Domain.World;

namespace Autonocraft.Items
{
    public static class ItemStackSaveCodec
    {
        public static InventorySlotSaveData Serialize(ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return new InventorySlotSaveData();
            }

            if (stack.IsTool())
            {
                return new InventorySlotSaveData
                {
                    Kind = (byte)ItemKind.Tool,
                    ToolId = (ushort)stack.ToolId,
                    Count = stack.Count,
                    Durability = stack.Durability,
                    MaxDurability = stack.MaxDurability
                };
            }

            if (stack.IsFluidContainer())
            {
                return new InventorySlotSaveData
                {
                    Kind = (byte)ItemKind.FluidContainer,
                    ToolId = (ushort)stack.ToolId,
                    Count = 1
                };
            }

            if (stack.IsFood())
            {
                return new InventorySlotSaveData
                {
                    Kind = (byte)ItemKind.Food,
                    ToolId = (ushort)stack.FoodId,
                    Count = stack.Count
                };
            }

            if (stack.IsMaterial())
            {
                return new InventorySlotSaveData
                {
                    Kind = (byte)ItemKind.Material,
                    ToolId = (ushort)stack.MaterialId,
                    Count = stack.Count
                };
            }

            return new InventorySlotSaveData
            {
                Kind = (byte)ItemKind.Block,
                Block = (byte)stack.BlockType,
                Count = stack.Count
            };
        }

        public static ItemStack Deserialize(InventorySlotSaveData data)
        {
            var kind = (ItemKind)data.Kind;
            if (kind == ItemKind.FluidContainer && data.ToolId != 0)
            {
                return ItemStack.CreateFluidContainer((ItemId)data.ToolId);
            }

            if (kind == ItemKind.Food && data.ToolId != 0)
            {
                if (!FoodRegistry.TryGet((ItemId)data.ToolId, out _))
                {
                    return ItemStack.Empty;
                }

                return ItemStack.CreateFood((ItemId)data.ToolId, Math.Max(1, data.Count));
            }

            if (kind == ItemKind.Material && data.ToolId != 0)
            {
                return ItemStack.CreateMaterial((ItemId)data.ToolId, Math.Max(1, data.Count));
            }

            if (kind == ItemKind.Tool && data.ToolId != 0)
            {
                var toolId = (ItemId)data.ToolId;
                if (!ToolRegistry.TryGet(toolId, out _))
                {
                    return ItemStack.Empty;
                }

                int maxDurability = data.MaxDurability > 0 ? data.MaxDurability : ToolRegistry.Get(toolId).MaxDurability;
                int durability = data.Durability > 0 ? data.Durability : maxDurability;
                return new ItemStack
                {
                    Kind = ItemKind.Tool,
                    ToolId = toolId,
                    Count = Math.Max(1, data.Count),
                    Durability = durability,
                    MaxDurability = maxDurability
                };
            }

            if (data.Count <= 0 || data.Block == (byte)BlockType.Air || !Enum.IsDefined(typeof(BlockType), data.Block))
            {
                return ItemStack.Empty;
            }

            return ItemStack.CreateBlock((BlockType)data.Block, data.Count);
        }
    }
}
