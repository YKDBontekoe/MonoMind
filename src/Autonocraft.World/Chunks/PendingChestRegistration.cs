namespace Autonocraft.World
{
    internal readonly struct PendingChestRegistration
    {
        public int LocalX { get; init; }
        public int LocalY { get; init; }
        public int LocalZ { get; init; }
        public string LootTableId { get; init; }
        public int RollSeed { get; init; }
    }
}
