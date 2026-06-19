using System;

namespace Autonocraft.World.Structures
{
    public sealed class StructureTemplate
    {
        public int FootprintRadius { get; init; }
        public StructureBlock[] Blocks { get; init; } = Array.Empty<StructureBlock>();
        public StructureChestMarker[] Chests { get; init; } = Array.Empty<StructureChestMarker>();
        internal StructureChunkIndex? ChunkIndex { get; init; }
    }
}
