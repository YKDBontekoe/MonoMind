namespace Autonocraft.World.Structures
{
    public sealed class StructureChestMarker
    {
        public int Dx { get; init; }
        public int Dy { get; init; }
        public int Dz { get; init; }
        public string LootTableId { get; init; } = string.Empty;
    }
}
