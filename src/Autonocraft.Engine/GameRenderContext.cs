using System.Collections.Generic;
using Autonocraft.Engine.Animation;
using Autonocraft.Entities;
using Autonocraft.Items.Rendering;
using Autonocraft.Village;
using Autonocraft.World;

namespace Autonocraft.Engine
{
    /// <summary>
    /// Read-only rendering context — decouples renderers from AutonocraftGame.
    /// </summary>
    public sealed class GameRenderContext
    {
        public Camera Camera { get; set; } = null!;
        public IPlayerHudView Player { get; set; } = null!;
        public VoxelWorld Grid { get; set; } = null!;
        public AnimalManager Animals { get; set; } = null!;
        public IReadOnlyList<IItemEntityRenderView> ItemEntities { get; set; } = null!;
        public VillagerManager Villagers { get; set; } = null!;
        public VillageManager Villages { get; set; } = null!;
        public IBlockInteractionOverlay BlockInteraction { get; set; } = null!;
        public ParticleSystem Particles { get; set; } = null!;
        public InteractionAnimator InteractionAnimator { get; set; } = null!;
        public ICraftingHudHint Crafting { get; set; } = null!;
        public HudToast HudToast { get; set; } = null!;
        public string? VillageHudHint { get; set; }
        public string? NearbyClaimHint { get; set; }
        public string? HudPlacementHint { get; set; }
        public bool VillageUiOpen { get; set; }
        public BlueprintPlacementPreview? BlueprintPlacement { get; set; }
        public IReadOnlyList<BlueprintPlacementPreview>? PendingConstructionSites { get; set; }
        public WorkZonePlacementPreview? WorkZonePlacement { get; set; }
        public float TimeOfDay { get; set; }
        public float WaterAnimTime { get; set; }
        public int RenderDistance { get; set; }
        public WeatherSystem Weather { get; set; } = null!;
    }
}
