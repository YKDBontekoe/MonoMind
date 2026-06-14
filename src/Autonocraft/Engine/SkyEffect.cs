using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    /// <summary>
    /// Sky dome and cloud rendering via BasicEffect (no MGCB / Wine shader compile required).
    /// </summary>
    public sealed class SkyEffect : IDisposable
    {
        private readonly BasicEffect _skyEffect;
        private readonly BasicEffect _cloudEffect;

        private SkyEffect(GraphicsDevice device)
        {
            _skyEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                TextureEnabled = false
            };

            _cloudEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                TextureEnabled = true
            };
        }

        public static SkyEffect Create(GraphicsDevice device) => new(device);

        public void ApplySkyDome(Matrix view, Matrix projection)
        {
            _skyEffect.View = view;
            _skyEffect.Projection = projection;
            _skyEffect.World = Matrix.Identity;
            _skyEffect.FogEnabled = false;
            _skyEffect.TextureEnabled = false;
        }

        public void ApplyCloudLayer(
            Matrix world,
            Matrix view,
            Matrix projection,
            Color cloudColor,
            Texture2D cloudTexture)
        {
            _ = cloudColor;
            _cloudEffect.View = view;
            _cloudEffect.Projection = projection;
            _cloudEffect.World = world;
            _cloudEffect.FogEnabled = false;
            _cloudEffect.TextureEnabled = true;
            _cloudEffect.Texture = cloudTexture;
        }

        public IEnumerable<EffectPass> GetSkyDomePasses()
        {
            foreach (var pass in _skyEffect.CurrentTechnique.Passes)
            {
                yield return pass;
            }
        }

        public IEnumerable<EffectPass> GetCloudLayerPasses()
        {
            foreach (var pass in _cloudEffect.CurrentTechnique.Passes)
            {
                yield return pass;
            }
        }

        public BasicEffect SkyDomeEffect => _skyEffect;

        public static Matrix StripTranslation(Matrix view)
        {
            view.M41 = 0f;
            view.M42 = 0f;
            view.M43 = 0f;
            return view;
        }

        public void Dispose()
        {
            _skyEffect.Dispose();
            _cloudEffect.Dispose();
        }
    }
}
