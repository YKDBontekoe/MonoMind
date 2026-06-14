namespace Autonocraft.Village
{
    public sealed class BlueprintPlacementPreview
    {
        public BuildingBlueprint Blueprint { get; init; } = null!;
        public int AnchorX { get; init; }
        public int AnchorY { get; init; }
        public int AnchorZ { get; init; }
        public bool Valid { get; init; }
    }
}
