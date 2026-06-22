using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{

    public sealed partial class WorldRenderer
    {
        private readonly VertexPositionColor[] _celestialVertices = new VertexPositionColor[4];
        private readonly short[] _celestialIndices = { 0, 1, 2, 0, 2, 3 };

        private void DrawSkyBox(Matrix view, Matrix projection, SceneLighting lighting, float timeOfDay, int worldSeed)
        {
            _skyDomeRenderer.Draw(_skyEffect, view, projection, lighting, timeOfDay, worldSeed);
            _cloudLayerRenderer.Draw(_device, _skyEffect, view, projection, timeOfDay, lighting);
        }

        private void DrawSunAndMoon(Camera camera, Vector3 sunDir, Vector3 moonDir)
        {
            var effect = _skyEffect.SkyDomeEffect;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.BlendState = BlendState.AlphaBlend;
            effect.TextureEnabled = false;

            float aspect = (float)_device.Viewport.Width / _device.Viewport.Height;
            effect.View = ConvertMatrix(camera.GetViewMatrix());
            effect.Projection = ConvertMatrix(camera.GetProjectionMatrix(aspect));
            effect.World = Matrix.Identity;

            if (sunDir.Y > -0.1f)
            {
                var sunPos = camera.Position + sunDir * 150f;
                float sunVisibility = MathHelper.Clamp((sunDir.Y + 0.1f) / 0.42f, 0f, 1f);
                var glowRight = camera.Right * 29f;
                var glowUp = camera.Up * 29f;
                DrawCelestialQuad(effect, sunPos, glowRight, glowUp, new Color(1.0f, 0.74f, 0.26f, 0.18f * sunVisibility));

                var right = camera.Right * 13f;
                var up = camera.Up * 13f;
                DrawCelestialQuad(effect, sunPos, right, up, new Color(1.0f, 0.91f, 0.52f, 0.92f * sunVisibility));
            }

            if (moonDir.Y > -0.1f)
            {
                var moonPos = camera.Position + moonDir * 150f;
                float moonVisibility = MathHelper.Clamp((moonDir.Y + 0.1f) / 0.42f, 0f, 1f);
                var glowRight = camera.Right * 23f;
                var glowUp = camera.Up * 23f;
                DrawCelestialQuad(effect, moonPos, glowRight, glowUp, new Color(0.58f, 0.70f, 1.0f, 0.14f * moonVisibility));

                var right = camera.Right * 10f;
                var up = camera.Up * 10f;
                DrawCelestialQuad(effect, moonPos, right, up, new Color(0.86f, 0.90f, 1.0f, 0.86f * moonVisibility));
            }

            _device.DepthStencilState = DepthStencilState.Default;
            _device.BlendState = BlendState.Opaque;
        }

        private void DrawCelestialQuad(
            BasicEffect effect,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            Color color)
        {
            var v0 = ConvertVector(center - right - up);
            var v1 = ConvertVector(center - right + up);
            var v2 = ConvertVector(center + right + up);
            var v3 = ConvertVector(center + right - up);
            _celestialVertices[0] = new VertexPositionColor(v0, color);
            _celestialVertices[1] = new VertexPositionColor(v1, color);
            _celestialVertices[2] = new VertexPositionColor(v2, color);
            _celestialVertices[3] = new VertexPositionColor(v3, color);

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _celestialVertices, 0, 4, _celestialIndices, 0, 2);
            }
        }
    }
}
