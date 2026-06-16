namespace Autonocraft.Items
{
    public sealed class ToolDefinition
    {
        public ItemId ItemId { get; init; }
        public ToolType ToolType { get; init; }
        public ToolTier Tier { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public int MaxDurability { get; init; }
        public float MiningSpeedMultiplier { get; init; }
        public float MeleeDamage { get; init; }
    }
}
