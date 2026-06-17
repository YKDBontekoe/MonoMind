namespace Autonocraft.World.Structures
{
    public readonly struct StructureBlock
    {
        public int Dx { get; init; }
        public int Dy { get; init; }
        public int Dz { get; init; }
        public BlockType Type { get; init; }
        public StructurePlacementMode Mode { get; init; }

        public StructureBlock(int dx, int dy, int dz, BlockType type, StructurePlacementMode mode = StructurePlacementMode.ReplaceSurface)
        {
            Dx = dx;
            Dy = dy;
            Dz = dz;
            Type = type;
            Mode = mode;
        }
    }
}
