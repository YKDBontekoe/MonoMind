using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    /// <summary>
    /// Terrain rendering effect backed by MonoGame's stock BasicEffect.
    /// </summary>
    public sealed class BlockTerrainEffect : IDisposable
    {
        private readonly BasicEffect _effect;

        public BlockTerrainEffect(GraphicsDevice device, Texture2D atlas, bool highQualityLighting = false)
        {
            _effect = new BasicEffect(device)
            {
                TextureEnabled = true,
                Texture = atlas,
                VertexColorEnabled = true,
                PreferPerPixelLighting = highQualityLighting,
                LightingEnabled = true,
                FogEnabled = true
            };
        }

        public EffectTechnique CurrentTechnique => _effect.CurrentTechnique;

        public void SetPreferPerPixelLighting(bool enabled)
        {
            _effect.PreferPerPixelLighting = enabled;
        }

        public void SetAtlas(Texture2D atlas)
        {
            _effect.Texture = atlas;
        }

        public void SetFogRange(float fogStart, float fogEnd)
        {
            _effect.FogStart = fogStart;
            _effect.FogEnd = fogEnd;
        }

        public void SetAlpha(float alpha)
        {
            _effect.Alpha = alpha;
        }

        public void ApplyTerrainPassBase(
            Matrix world,
            Matrix view,
            Matrix projection,
            Vector3 ambientColor,
            Vector3 fogColor,
            Vector3 sunDir,
            Vector3 sunColor,
            bool sunEnabled,
            Vector3 moonDir,
            Vector3 moonColor,
            bool moonEnabled,
            Texture2D atlas)
        {
            ApplyLightingAndFog(
                world,
                view,
                projection,
                ambientColor,
                fogColor,
                fogStart: 0f,
                fogEnd: 1f,
                sunDir,
                sunColor,
                sunEnabled,
                moonDir,
                moonColor,
                moonEnabled,
                atlas);
        }

        public void ApplyLightingAndFog(
            Matrix world,
            Matrix view,
            Matrix projection,
            Vector3 ambientColor,
            Vector3 fogColor,
            float fogStart,
            float fogEnd,
            Vector3 sunDir,
            Vector3 sunColor,
            bool sunEnabled,
            Vector3 moonDir,
            Vector3 moonColor,
            bool moonEnabled,
            Texture2D atlas)
        {
            _effect.World = world;
            _effect.View = view;
            _effect.Projection = projection;
            _effect.Texture = atlas;
            _effect.AmbientLightColor = ambientColor;
            _effect.FogEnabled = true;
            _effect.FogColor = fogColor;
            _effect.FogStart = fogStart;
            _effect.FogEnd = fogEnd;
            _effect.DirectionalLight0.Enabled = sunEnabled;
            _effect.DirectionalLight0.Direction = -sunDir;
            _effect.DirectionalLight0.DiffuseColor = sunColor;
            _effect.DirectionalLight1.Enabled = moonEnabled;
            _effect.DirectionalLight1.Direction = -moonDir;
            _effect.DirectionalLight1.DiffuseColor = moonColor;
        }

        public void Dispose()
        {
            _effect.Dispose();
        }
    }
}
