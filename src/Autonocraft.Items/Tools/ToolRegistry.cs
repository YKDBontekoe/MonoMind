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
                (ToolType.Sword, ToolTier.Wood) => ItemId.WoodSword,
                (ToolType.Pickaxe, ToolTier.Stone) => ItemId.StonePickaxe,
                (ToolType.Axe, ToolTier.Stone) => ItemId.StoneAxe,
                (ToolType.Shovel, ToolTier.Stone) => ItemId.StoneShovel,
                (ToolType.Sword, ToolTier.Stone) => ItemId.StoneSword,
                (ToolType.Pickaxe, ToolTier.Iron) => ItemId.IronPickaxe,
                (ToolType.Axe, ToolTier.Iron) => ItemId.IronAxe,
                (ToolType.Shovel, ToolTier.Iron) => ItemId.IronShovel,
                (ToolType.Sword, ToolTier.Iron) => ItemId.IronSword,
                (ToolType.Pickaxe, ToolTier.Gold) => ItemId.GoldPickaxe,
                (ToolType.Axe, ToolTier.Gold) => ItemId.GoldAxe,
                (ToolType.Shovel, ToolTier.Gold) => ItemId.GoldShovel,
                (ToolType.Sword, ToolTier.Gold) => ItemId.GoldSword,
                (ToolType.Pickaxe, ToolTier.Copper) => ItemId.CopperPickaxe,
                (ToolType.Axe, ToolTier.Copper) => ItemId.CopperAxe,
                (ToolType.Shovel, ToolTier.Copper) => ItemId.CopperShovel,
                (ToolType.Sword, ToolTier.Copper) => ItemId.CopperSword,
                (ToolType.Pickaxe, ToolTier.Silver) => ItemId.SilverPickaxe,
                (ToolType.Axe, ToolTier.Silver) => ItemId.SilverAxe,
                (ToolType.Shovel, ToolTier.Silver) => ItemId.SilverShovel,
                (ToolType.Sword, ToolTier.Silver) => ItemId.SilverSword,
                (ToolType.Pickaxe, ToolTier.Diamond) => ItemId.DiamondPickaxe,
                (ToolType.Axe, ToolTier.Diamond) => ItemId.DiamondAxe,
                (ToolType.Shovel, ToolTier.Diamond) => ItemId.DiamondShovel,
                (ToolType.Sword, ToolTier.Diamond) => ItemId.DiamondSword,
                (ToolType.Pickaxe, ToolTier.Emerald) => ItemId.EmeraldPickaxe,
                (ToolType.Axe, ToolTier.Emerald) => ItemId.EmeraldAxe,
                (ToolType.Shovel, ToolTier.Emerald) => ItemId.EmeraldShovel,
                (ToolType.Sword, ToolTier.Emerald) => ItemId.EmeraldSword,
                (ToolType.Pickaxe, ToolTier.Relic) => ItemId.RelicPickaxe,
                (ToolType.Axe, ToolTier.Relic) => ItemId.RelicAxe,
                (ToolType.Shovel, ToolTier.Relic) => ItemId.RelicShovel,
                (ToolType.Sword, ToolTier.Relic) => ItemId.RelicSword,
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

        public static bool IsLootOnly(ItemId id)
        {
            return TryGet(id, out var def) && def.LootOnly;
        }

        private static Dictionary<ItemId, ToolDefinition> BuildAll()
        {
            var defs = new[]
            {
                Def(ItemId.WoodPickaxe, ToolType.Pickaxe, ToolTier.Wood, "Wood Pickaxe", 60, 1.5f, 2f),
                Def(ItemId.WoodAxe, ToolType.Axe, ToolTier.Wood, "Wood Axe", 60, 1.5f, 2f),
                Def(ItemId.WoodShovel, ToolType.Shovel, ToolTier.Wood, "Wood Shovel", 60, 1.5f, 2f),
                Def(ItemId.WoodSword, ToolType.Sword, ToolTier.Wood, "Wood Sword", 60, 1.0f, 3f),
                Def(ItemId.StonePickaxe, ToolType.Pickaxe, ToolTier.Stone, "Stone Pickaxe", 132, 2.0f, 3f),
                Def(ItemId.StoneAxe, ToolType.Axe, ToolTier.Stone, "Stone Axe", 132, 2.0f, 3f),
                Def(ItemId.StoneShovel, ToolType.Shovel, ToolTier.Stone, "Stone Shovel", 132, 2.0f, 3f),
                Def(ItemId.StoneSword, ToolType.Sword, ToolTier.Stone, "Stone Sword", 132, 1.0f, 4f),
                Def(ItemId.IronPickaxe, ToolType.Pickaxe, ToolTier.Iron, "Iron Pickaxe", 251, 3.0f, 5f),
                Def(ItemId.IronAxe, ToolType.Axe, ToolTier.Iron, "Iron Axe", 251, 3.0f, 5f),
                Def(ItemId.IronShovel, ToolType.Shovel, ToolTier.Iron, "Iron Shovel", 251, 3.0f, 5f),
                Def(ItemId.IronSword, ToolType.Sword, ToolTier.Iron, "Iron Sword", 251, 1.0f, 5f),
                Def(ItemId.GoldPickaxe, ToolType.Pickaxe, ToolTier.Gold, "Gold Pickaxe", 156, 4.0f, 4f),
                Def(ItemId.GoldAxe, ToolType.Axe, ToolTier.Gold, "Gold Axe", 156, 4.0f, 4f),
                Def(ItemId.GoldShovel, ToolType.Shovel, ToolTier.Gold, "Gold Shovel", 156, 4.0f, 4f),
                Def(ItemId.GoldSword, ToolType.Sword, ToolTier.Gold, "Gold Sword", 156, 1.0f, 4f),
                Def(ItemId.CopperPickaxe, ToolType.Pickaxe, ToolTier.Copper, "Copper Pickaxe", 180, 2.5f, 3.5f),
                Def(ItemId.CopperAxe, ToolType.Axe, ToolTier.Copper, "Copper Axe", 180, 2.5f, 3.5f),
                Def(ItemId.CopperShovel, ToolType.Shovel, ToolTier.Copper, "Copper Shovel", 180, 2.5f, 3.5f),
                Def(ItemId.CopperSword, ToolType.Sword, ToolTier.Copper, "Copper Sword", 180, 1.0f, 4.5f),
                Def(ItemId.SilverPickaxe, ToolType.Pickaxe, ToolTier.Silver, "Silver Pickaxe", 200, 3.5f, 4.5f),
                Def(ItemId.SilverAxe, ToolType.Axe, ToolTier.Silver, "Silver Axe", 200, 3.5f, 4.5f),
                Def(ItemId.SilverShovel, ToolType.Shovel, ToolTier.Silver, "Silver Shovel", 200, 3.5f, 4.5f),
                Def(ItemId.SilverSword, ToolType.Sword, ToolTier.Silver, "Silver Sword", 200, 1.0f, 5.5f),
                Def(ItemId.DiamondPickaxe, ToolType.Pickaxe, ToolTier.Diamond, "Diamond Pickaxe", 1561, 5.0f, 6.0f),
                Def(ItemId.DiamondAxe, ToolType.Axe, ToolTier.Diamond, "Diamond Axe", 1561, 5.0f, 6.0f),
                Def(ItemId.DiamondShovel, ToolType.Shovel, ToolTier.Diamond, "Diamond Shovel", 1561, 5.0f, 6.0f),
                Def(ItemId.DiamondSword, ToolType.Sword, ToolTier.Diamond, "Diamond Sword", 1561, 1.0f, 7.0f),
                Def(ItemId.EmeraldPickaxe, ToolType.Pickaxe, ToolTier.Emerald, "Emerald Pickaxe", 2000, 6.0f, 7.0f),
                Def(ItemId.EmeraldAxe, ToolType.Axe, ToolTier.Emerald, "Emerald Axe", 2000, 6.0f, 7.0f),
                Def(ItemId.EmeraldShovel, ToolType.Shovel, ToolTier.Emerald, "Emerald Shovel", 2000, 6.0f, 7.0f),
                Def(ItemId.EmeraldSword, ToolType.Sword, ToolTier.Emerald, "Emerald Sword", 2000, 1.0f, 8.0f),
                Def(ItemId.RelicPickaxe, ToolType.Pickaxe, ToolTier.Relic, "Relic Pickaxe", 2800, 7.5f, 8.0f, lootOnly: true),
                Def(ItemId.RelicAxe, ToolType.Axe, ToolTier.Relic, "Relic Axe", 2800, 7.5f, 8.0f, lootOnly: true),
                Def(ItemId.RelicShovel, ToolType.Shovel, ToolTier.Relic, "Relic Shovel", 2800, 7.5f, 8.0f, lootOnly: true),
                Def(ItemId.RelicSword, ToolType.Sword, ToolTier.Relic, "Relic Sword", 2800, 1.0f, 10.0f, lootOnly: true)
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
            float damage,
            bool lootOnly = false)
        {
            return new ToolDefinition
            {
                ItemId = id,
                ToolType = type,
                Tier = tier,
                DisplayName = name,
                MaxDurability = durability,
                MiningSpeedMultiplier = miningSpeed,
                MeleeDamage = damage,
                LootOnly = lootOnly
            };
        }
    }
}
