using System;

namespace Autonocraft.World.Structures
{
    public sealed class StructureDefinition
    {
        public string Id { get; init; } = string.Empty;
        public StructureTier Tier { get; init; }
        public BiomeType[] AllowedBiomes { get; init; } = Array.Empty<BiomeType>();
        public StructureTemplate Template { get; init; } = new StructureTemplate();
    }
}
