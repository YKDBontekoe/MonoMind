using Autonocraft.Crafting;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Core
{
    /// <summary>
    /// Read-only rendering context — decouples Engine from AutonocraftGame.
    /// </summary>
    public sealed class GameRenderContext
    {
        public Camera Camera { get; set; } = null!;
        public Player Player { get; set; } = null!;
        public VoxelWorld Grid { get; set; } = null!;
        public AnimalManager Animals { get; set; } = null!;
        public VillagerManager Villagers { get; set; } = null!;
        public BlockInteractionSystem BlockInteraction { get; set; } = null!;
        public ParticleSystem Particles { get; set; } = null!;
        public InteractionAnimator InteractionAnimator { get; set; } = null!;
        public CraftingSystem Crafting { get; set; } = null!;
        public HudToast HudToast { get; set; } = null!;
        public bool ShowVillageHint { get; set; }
        public string? NearbyClaimHint { get; set; }
        public float TimeOfDay { get; set; }
        public float WaterAnimTime { get; set; }
        public int RenderDistance { get; set; }
    }
}
