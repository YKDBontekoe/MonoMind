using Autonocraft.Domain.World;

namespace Autonocraft.Domain.Rendering
{
    /// <summary>Read-only block targeting / placement state for HUD and world overlays.</summary>
    public interface IBlockInteractionOverlay
    {
        CrosshairState Crosshair { get; }
        float HotbarPulseScale { get; }
        float CrosshairFlashAlpha { get; }
        System.Numerics.Vector3? TargetBlockPos { get; }
        System.Numerics.Vector3? TargetNormal { get; }
        BlockType TargetBlockType { get; }
        System.Numerics.Vector3? GhostBlockPos { get; }
        BlockType GhostBlockType { get; }
        bool GhostValid { get; }
        float BreakProgress { get; }
        bool IsMining { get; }
        float AnimTime { get; }
        PlacePopEffect PlacePop { get; }
    }
}
