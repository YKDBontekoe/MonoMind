namespace Autonocraft.Items
{
    public static class ToolRegistry
    {
        private static readonly Dictionary<ItemId, ToolDefinition> _byId = BuildAll();

        public static IReadOnlyDictionary<ItemId, ToolDefinition> All => _byId;

        public static bool TryGet(ItemId id, out ToolDefinition definition)
        {
            return _byId.TryGetValue(id, out definition!);
        }

        public static ToolDefinition Get(ItemId id)
        {
            return _byId[id];
        }

        public static ItemId GetItemId(ToolType type, ToolTier tier)
        {
            return (type, tier) switch
            {
                (ToolType.Pickaxe, ToolTier.Wood) => ItemId.WoodPickaxe,
                (ToolType.Axe, ToolTier.Wood) => ItemId.WoodAxe,
                (ToolType.Shovel, ToolTier.Wood) => ItemId.WoodShovel,
                (ToolType.Pickaxe, ToolTier.Stone) => ItemId.StonePickaxe,
                (ToolType.Axe, ToolTier.Stone) => ItemId.StoneAxe,
                (ToolType.Shovel, ToolTier.Stone) => ItemId.StoneShovel,
                (ToolType.Pickaxe, ToolTier.Iron) => ItemId.IronPickaxe,
                (ToolType.Axe, ToolTier.Iron) => ItemId.IronAxe,
                (ToolType.Shovel, ToolTier.Iron) => ItemId.IronShovel,
                (ToolType.Sword, ToolTier.Iron) => ItemId.IronSword,
                (ToolType.Pickaxe, ToolTier.Gold) => ItemId.GoldPickaxe,
                (ToolType.Axe, ToolTier.Gold) => ItemId.GoldAxe,
                (ToolType.Shovel, ToolTier.Gold) => ItemId.GoldShovel,
                (ToolType.Sword, ToolTier.Gold) => ItemId.GoldSword,
                _ => ItemId.None
            };
        }

        public static string GetAtlasTileId(ItemId id)
        {
            var def = Get(id);
            return $"tool_{def.Tier.ToString().ToLowerInvariant()}_{def.ToolType.ToString().ToLowerInvariant()}";
        }

        public static ItemStack CreateStack(ItemId id)
        {
            var def = Get(id);
            return ItemStack.CreateTool(id, def.MaxDurability);
        }

        public static ItemStack CreateStack(ToolType type, ToolTier tier)
        {
            return CreateStack(GetItemId(type, tier));
        }

        private static Dictionary<ItemId, ToolDefinition> BuildAll()
        {
            var defs = new[]
            {
                Def(ItemId.WoodPickaxe, ToolType.Pickaxe, ToolTier.Wood, "Wood Pickaxe", 60, 1.5f, 2f),
                Def(ItemId.WoodAxe, ToolType.Axe, ToolTier.Wood, "Wood Axe", 60, 1.5f, 2f),
                Def(ItemId.WoodShovel, ToolType.Shovel, ToolTier.Wood, "Wood Shovel", 60, 1.5f, 2f),
                Def(ItemId.StonePickaxe, ToolType.Pickaxe, ToolTier.Stone, "Stone Pickaxe", 132, 2.0f, 3f),
                Def(ItemId.StoneAxe, ToolType.Axe, ToolTier.Stone, "Stone Axe", 132, 2.0f, 3f),
                Def(ItemId.StoneShovel, ToolType.Shovel, ToolTier.Stone, "Stone Shovel", 132, 2.0f, 3f),
                Def(ItemId.IronPickaxe, ToolType.Pickaxe, ToolTier.Iron, "Iron Pickaxe", 251, 3.0f, 5f),
                Def(ItemId.IronAxe, ToolType.Axe, ToolTier.Iron, "Iron Axe", 251, 3.0f, 5f),
                Def(ItemId.IronShovel, ToolType.Shovel, ToolTier.Iron, "Iron Shovel", 251, 3.0f, 5f),
                Def(ItemId.IronSword, ToolType.Sword, ToolTier.Iron, "Iron Sword", 251, 1.0f, 5f),
                Def(ItemId.GoldPickaxe, ToolType.Pickaxe, ToolTier.Gold, "Gold Pickaxe", 156, 4.0f, 4f),
                Def(ItemId.GoldAxe, ToolType.Axe, ToolTier.Gold, "Gold Axe", 156, 4.0f, 4f),
                Def(ItemId.GoldShovel, ToolType.Shovel, ToolTier.Gold, "Gold Shovel", 156, 4.0f, 4f),
                Def(ItemId.GoldSword, ToolType.Sword, ToolTier.Gold, "Gold Sword", 156, 1.0f, 4f)
            };

            return defs.ToDictionary(d => d.ItemId);
        }

        private static ToolDefinition Def(
            ItemId id,
            ToolType type,
            ToolTier tier,
            string name,
            int durability,
            float miningSpeed,
            float damage)
        {
            return new ToolDefinition
            {
                ItemId = id,
                ToolType = type,
                Tier = tier,
                DisplayName = name,
                MaxDurability = durability,
                MiningSpeedMultiplier = miningSpeed,
                MeleeDamage = damage
            };
        }
    }
}
