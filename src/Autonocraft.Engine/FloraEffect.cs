using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    /// <summary>
    /// Flora rendering via stock AlphaTestEffect (no MGCB / custom shader required).
    /// </summary>
    public sealed class FloraEffect : IDisposable
    {
        private readonly AlphaTestEffect _effect;

        public FloraEffect(GraphicsDevice device, Texture2D atlas)
        {
            _effect = new AlphaTestEffect(device)
            {
                Texture = atlas,
                VertexColorEnabled = true,
                FogEnabled = true,
                AlphaFunction = CompareFunction.Greater,
                ReferenceAlpha = 128
            };
        }

        public EffectTechnique CurrentTechnique => _effect.CurrentTechnique;

        public void SetAtlas(Texture2D atlas)
        {
            _effect.Texture = atlas;
        }

        public void Apply(
            Matrix world,
            Matrix view,
            Matrix projection,
            Texture2D atlas,
            Vector3 fogColor,
            float fogStart,
            float fogEnd)
        {
            _effect.World = world;
            _effect.View = view;
            _effect.Projection = projection;
            _effect.Texture = atlas;
            _effect.AlphaFunction = CompareFunction.Greater;
            _effect.ReferenceAlpha = 128;
            _effect.FogEnabled = true;
            _effect.FogColor = fogColor;
            _effect.FogStart = fogStart;
            _effect.FogEnd = fogEnd;
        }

        public void Dispose()
        {
            _effect.Dispose();
        }
    }
}
