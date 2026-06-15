using Autonocraft.World;

namespace Autonocraft.Items
{
    public struct ItemStack
    {
        public ItemKind Kind;
        public ItemId ToolId;
        public ItemId FoodId;
        public BlockType BlockType;
        public int Count;
        public int Durability;
        public int MaxDurability;

        public static ItemStack Empty => default;

        public bool IsEmpty =>
            Kind == ItemKind.Empty ||
            (Kind == ItemKind.Block && (BlockType == BlockType.Air || Count <= 0)) ||
            (Kind == ItemKind.Tool && ToolId == ItemId.None) ||
            (Kind == ItemKind.FluidContainer && ToolId == ItemId.None) ||
            (Kind == ItemKind.Food && FoodId == ItemId.None);

        public static ItemStack CreateBlock(BlockType blockType, int count)
        {
            return new ItemStack
            {
                Kind = ItemKind.Block,
                BlockType = blockType,
                Count = count
            };
        }

        public static ItemStack CreateTool(ItemId toolId, int durability)
        {
            var def = ToolRegistry.Get(toolId);
            return new ItemStack
            {
                Kind = ItemKind.Tool,
                ToolId = toolId,
                Count = 1,
                Durability = durability,
                MaxDurability = def.MaxDurability
            };
        }

        public static ItemStack CreateFood(ItemId foodId, int count)
        {
            return new ItemStack
            {
                Kind = ItemKind.Food,
                FoodId = foodId,
                Count = count
            };
        }

        public bool IsFood() => Kind == ItemKind.Food && FoodId != ItemId.None;

        public bool IsTool() => Kind == ItemKind.Tool && ToolId != ItemId.None;

        public bool IsBlock() => Kind == ItemKind.Block && BlockType != BlockType.Air && Count > 0;

        public bool IsFluidContainer() => Kind == ItemKind.FluidContainer && ToolId != ItemId.None;

        public bool IsWaterBucket() => IsFluidContainer() && ToolId == ItemId.WaterBucket;

        public bool IsEmptyBucket() => IsFluidContainer() && ToolId == ItemId.EmptyBucket;

        public static ItemStack CreateFluidContainer(ItemId containerId)
        {
            return new ItemStack
            {
                Kind = ItemKind.FluidContainer,
                ToolId = containerId,
                Count = 1
            };
        }

        public bool CanStackWith(in ItemStack other)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return false;
            }

            if (Kind != other.Kind)
            {
                return false;
            }

            return Kind switch
            {
                ItemKind.Block => BlockType == other.BlockType && Count < 64 && other.Count < 64,
                ItemKind.Tool => ToolId == other.ToolId &&
                                 Durability == other.Durability &&
                                 MaxDurability == other.MaxDurability &&
                                 Count < 64,
                ItemKind.Food => FoodId == other.FoodId && Count < 64 && other.Count < 64,
                _ => false
            };
        }

        public string GetDisplayName()
        {
            if (IsEmpty)
            {
                return "Empty";
            }

            if (IsTool())
            {
                return ToolRegistry.Get(ToolId).DisplayName;
            }

            if (IsFluidContainer())
            {
                return ToolId switch
                {
                    ItemId.WaterBucket => "Water Bucket",
                    ItemId.EmptyBucket => "Empty Bucket",
                    _ => ToolId.ToString()
                };
            }

            if (IsFood())
            {
                return FoodRegistry.GetDisplayName(FoodId);
            }

            return BlockType.ToString();
        }
    }
}
