namespace Autonocraft.Domain.World
{
    public static class WorldConstants
    {
        public const int SeaLevel = 62;
        public const int BedrockLevel = 0;
        public const int DirtDepth = 4;
        public const int BeachMaxHeight = SeaLevel + 3;
        public const int DefaultSeed = 1337;

        /// <summary>Blocks above sea level where surface snow begins on high terrain.</summary>
        public const int SnowLineOffset = 100;

        /// <summary>Elevation band (above sea level) where river spring heads may spawn.</summary>
        public const int RiverHeadMinOffset = 6;
        public const int RiverHeadMaxOffset = 130;
    }
}
