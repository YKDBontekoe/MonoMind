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
        private void DrawSkyBox(Matrix view, Matrix projection, SceneLighting lighting, float timeOfDay, int worldSeed)
        {
            _skyDomeRenderer.Draw(_skyEffect, view, projection, lighting, timeOfDay, worldSeed);
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
                var right = camera.Right * 15f;
                var up = camera.Up * 15f;
                DrawCelestialQuad(effect, sunPos, right, up, new Color(1.0f, 0.9f, 0.4f));
            }

            if (moonDir.Y > -0.1f)
            {
                var moonPos = camera.Position + moonDir * 150f;
                var right = camera.Right * 14f;
                var up = camera.Up * 14f;
                DrawCelestialQuad(effect, moonPos, right, up, Color.White);
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
            var vertices = new[]
            {
                new VertexPositionColor(v0, color),
                new VertexPositionColor(v1, color),
                new VertexPositionColor(v2, color),
                new VertexPositionColor(v3, color)
            };
            var indices = new short[] { 0, 1, 2, 0, 2, 3 };

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 4, indices, 0, 2);
            }
        }
    }
}
