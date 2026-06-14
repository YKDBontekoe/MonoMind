using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Core;
using Autonocraft.World;

namespace Autonocraft.Engine
{
    public class Renderer : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly BlockTerrainEffect _blockTerrainEffect;
        private readonly WorldRenderer _worldRenderer;
        private readonly HudRenderer _hudRenderer;
        private readonly VillagerNameplateRenderer _villagerNameplateRenderer;
        private Texture2D _atlasTexture;

        public Renderer(GraphicsDevice device, Texture2D atlas, Texture2D white, BlockTerrainEffect blockTerrainEffect, SkyEffect skyEffect, bool highQualityLighting = false)
        {
            _device = device;
            _atlasTexture = atlas;
            _blockTerrainEffect = blockTerrainEffect;
            _worldRenderer = new WorldRenderer(device, atlas, white, blockTerrainEffect, skyEffect, highQualityLighting);
            _hudRenderer = new HudRenderer(device, atlas, white);
            _villagerNameplateRenderer = new VillagerNameplateRenderer(device, white);
        }

        public void SetPreferPerPixelLighting(bool enabled)
        {
            _blockTerrainEffect.SetPreferPerPixelLighting(enabled);
            _worldRenderer.SetPreferPerPixelLighting(enabled);
        }

        public void Draw(GameRenderContext ctx)
        {
            float sw = _device.Viewport.Width;
            float sh = _device.Viewport.Height;
            _worldRenderer.Draw(ctx);
            _villagerNameplateRenderer.Draw(ctx, sw, sh);
            _hudRenderer.Draw(ctx, sw, sh);
        }

        public void SetAtlasTexture(Texture2D atlas)
        {
            _atlasTexture = atlas;
            _blockTerrainEffect.SetAtlas(atlas);
            _worldRenderer.SetAtlasTexture(atlas);
            _hudRenderer.SetAtlasTexture(atlas);
        }

        public void UpdateWaterAnimation(float animTime)
        {
            int tileSize = World.BlockAtlas.LayoutData.TileSize;
            ProceduralAtlasBuilder.UpdateWaterTile(_atlasTexture, animTime, tileSize);
        }

        public void Dispose()
        {
            _worldRenderer.Dispose();
            _hudRenderer.Dispose();
            _villagerNameplateRenderer.Dispose();
            _blockTerrainEffect.Dispose();
        }
    }
}
