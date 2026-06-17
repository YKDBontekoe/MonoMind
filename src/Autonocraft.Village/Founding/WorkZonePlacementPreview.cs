namespace Autonocraft.Village
{
    public sealed class WorkZonePlacementPreview
    {
        public int MinX { get; init; }
        public int MinY { get; init; }
        public int MinZ { get; init; }
        public int MaxX { get; init; }
        public int MaxY { get; init; }
        public int MaxZ { get; init; }
        public bool Valid { get; init; }
        public bool HasFirstCorner { get; init; }
    }
}
