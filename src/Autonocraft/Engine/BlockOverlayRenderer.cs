using System;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Core;
using Autonocraft.Engine.Animation;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;
using Vector2 = System.Numerics.Vector2;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{
    public class BlockOverlayRenderer : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly BasicEffect _overlayEffect;
        private readonly Texture2D _atlasTexture;

        public BlockOverlayRenderer(GraphicsDevice device, Texture2D atlas)
        {
            _device = device;
            _atlasTexture = atlas;
            _overlayEffect = new BasicEffect(device)
            {
                TextureEnabled = true,
                Texture = atlas,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        public void Draw(
            BlockInteractionSystem interaction,
            Matrix view,
            Matrix projection,
            Camera camera,
            float animTime)
        {
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.PointClamp;
            _device.BlendState = BlendState.AlphaBlend;

            _overlayEffect.View = view;
            _overlayEffect.Projection = projection;
            _overlayEffect.Alpha = 1f;

            if (interaction.TargetBlockPos.HasValue && interaction.TargetBlockType != BlockType.Air)
            {
                float pulse = Tween.Pulse(animTime, 2.5f);
                float alpha = 0.35f + 0.25f * pulse;
                DrawWireframeCube(interaction.TargetBlockPos.Value, new Color(0.2f, 0.85f, 1.0f, alpha));
            }

            if (interaction.IsMining && interaction.TargetBlockPos.HasValue && interaction.TargetNormal.HasValue)
            {
                DrawCrackOverlay(
                    interaction.TargetBlockPos.Value,
                    interaction.TargetNormal.Value,
                    interaction.BreakProgress);
            }

            if (interaction.GhostBlockPos.HasValue && interaction.GhostBlockType != BlockType.Air)
            {
                float ghostAlpha = interaction.GhostValid ? 0.4f : 0.35f;
                var tint = interaction.GhostValid
                    ? new Color(1f, 1f, 1f, ghostAlpha)
                    : new Color(1f, 0.3f, 0.3f, ghostAlpha);
                DrawGhostBlock(interaction.GhostBlockPos.Value, interaction.GhostBlockType, tint);
            }

            if (interaction.PlacePop.Active)
            {
                float t = 1f - Math.Clamp(interaction.PlacePop.Timer / interaction.PlacePop.Duration, 0f, 1f);
                float scale = 0.85f + 0.15f * Tween.EaseOut(t);
                var popTint = new Color(1f, 1f, 1f, 0.7f * (1f - t));
                DrawScaledBlock(interaction.PlacePop.Position, interaction.PlacePop.BlockType, scale, popTint);
            }

            DrawParticles(interaction, view, projection, camera);
        }

        private void DrawWireframeCube(Vector3 blockPos, Color color)
        {
            float x0 = blockPos.X + 0.002f;
            float y0 = blockPos.Y + 0.002f;
            float z0 = blockPos.Z + 0.002f;
            float x1 = blockPos.X + 0.998f;
            float y1 = blockPos.Y + 0.998f;
            float z1 = blockPos.Z + 0.998f;

            var vertices = new VertexPositionColor[24];
            int i = 0;

            void AddLine(Vector3 a, Vector3 b)
            {
                vertices[i++] = new VertexPositionColor(ToMono(a), color);
                vertices[i++] = new VertexPositionColor(ToMono(b), color);
            }

            AddLine(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0));
            AddLine(new Vector3(x1, y0, z0), new Vector3(x1, y0, z1));
            AddLine(new Vector3(x1, y0, z1), new Vector3(x0, y0, z1));
            AddLine(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0));

            AddLine(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0));
            AddLine(new Vector3(x1, y1, z0), new Vector3(x1, y1, z1));
            AddLine(new Vector3(x1, y1, z1), new Vector3(x0, y1, z1));
            AddLine(new Vector3(x0, y1, z1), new Vector3(x0, y1, z0));

            AddLine(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0));
            AddLine(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0));
            AddLine(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1));
            AddLine(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1));

            _overlayEffect.World = Matrix.Identity;
            _overlayEffect.TextureEnabled = false;

            foreach (var pass in _overlayEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, 12);
            }

            _overlayEffect.TextureEnabled = true;
        }

        private void DrawCrackOverlay(Vector3 blockPos, Vector3 normal, float progress)
        {
            int stage = Math.Clamp((int)(progress * 10f), 0, 9);
            float crackAlpha = 0.2f + stage * 0.07f;
            var crackColor = new Vector3(1f, 1f, 1f) * crackAlpha;

            float offset = 0.001f;
            Vector3 n = Vector3.Normalize(normal);
            Vector3 center = blockPos + n * (0.5f + offset) + new Vector3(0.5f, 0.5f, 0.5f);

            Vector3 tangent;
            if (MathF.Abs(n.Y) > 0.5f)
            {
                tangent = Vector3.UnitX;
            }
            else
            {
                tangent = Vector3.UnitY;
            }

            Vector3 bitangent = Vector3.Normalize(Vector3.Cross(n, tangent));
            tangent = Vector3.Normalize(Vector3.Cross(bitangent, n));
            float half = 0.49f;

            var p0 = center - tangent * half - bitangent * half;
            var p1 = center + tangent * half - bitangent * half;
            var p2 = center + tangent * half + bitangent * half;
            var p3 = center - tangent * half + bitangent * half;

            var col = crackColor;
            var vertices = new Vertex[]
            {
                new Vertex(p0, col, n, Vector2.Zero),
                new Vertex(p1, col, n, Vector2.Zero),
                new Vertex(p2, col, n, Vector2.Zero),
                new Vertex(p3, col, n, Vector2.Zero)
            };

            DrawCrackPattern(stage, vertices);

            var indices = new short[] { 0, 1, 2, 0, 2, 3 };
            _overlayEffect.World = Matrix.Identity;
            _overlayEffect.TextureEnabled = false;

            foreach (var pass in _overlayEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 4, indices, 0, 2);
            }

            _overlayEffect.TextureEnabled = true;
        }

        private static void DrawCrackPattern(int stage, Vertex[] vertices)
        {
            float intensity = (stage + 1) / 10f;
            for (int i = 0; i < 4; i++)
            {
                vertices[i].Color *= intensity;
            }
        }

        private void DrawGhostBlock(Vector3 blockPos, BlockType blockType, Color tint)
        {
            DrawScaledBlock(blockPos, blockType, 1f, tint);
        }

        private void DrawScaledBlock(Vector3 blockPos, BlockType blockType, float scale, Color tint)
        {
            float half = 0.5f * scale;
            Vector3 center = blockPos + new Vector3(0.5f, 0.5f, 0.5f);

            float x0 = center.X - half;
            float y0 = center.Y - half;
            float z0 = center.Z - half;
            float x1 = center.X + half;
            float y1 = center.Y + half;
            float z1 = center.Z + half;

            var col = new Vector3(tint.R / 255f, tint.G / 255f, tint.B / 255f) * (tint.A / 255f);

            var p0 = new Vector3(x0, y0, z1);
            var p1 = new Vector3(x1, y0, z1);
            var p2 = new Vector3(x1, y1, z1);
            var p3 = new Vector3(x0, y1, z1);
            var p4 = new Vector3(x0, y0, z0);
            var p5 = new Vector3(x1, y0, z0);
            var p6 = new Vector3(x1, y1, z0);
            var p7 = new Vector3(x0, y1, z0);

            var uvTop = BlockAtlas.GetFaceUVs(blockType, new Vector3(0f, 1f, 0f));
            var uvSide = BlockAtlas.GetFaceUVs(blockType, new Vector3(0f, 0f, 1f));

            var vertices = new Vertex[24];

            void SetFace(int offset, Vector3 pA, Vector3 pB, Vector3 pC, Vector3 pD, Vector3 normal, (float uMin, float vMin, float uMax, float vMax) uv)
            {
                vertices[offset] = new Vertex(pA, col, normal, new Vector2(uv.uMin, uv.vMax));
                vertices[offset + 1] = new Vertex(pB, col, normal, new Vector2(uv.uMax, uv.vMax));
                vertices[offset + 2] = new Vertex(pC, col, normal, new Vector2(uv.uMax, uv.vMin));
                vertices[offset + 3] = new Vertex(pD, col, normal, new Vector2(uv.uMin, uv.vMin));
            }

            SetFace(0, p0, p1, p2, p3, new Vector3(0, 0, 1), uvSide);
            SetFace(4, p1, p5, p6, p2, new Vector3(1, 0, 0), uvSide);
            SetFace(8, p5, p4, p7, p6, new Vector3(0, 0, -1), uvSide);
            SetFace(12, p4, p0, p3, p7, new Vector3(-1, 0, 0), uvSide);
            SetFace(16, p3, p2, p6, p7, new Vector3(0, 1, 0), uvTop);
            SetFace(20, p4, p5, p1, p0, new Vector3(0, -1, 0), uvSide);

            var indices = new short[36];
            for (int f = 0; f < 6; f++)
            {
                int vOffset = f * 4;
                int iOffset = f * 6;
                indices[iOffset + 0] = (short)(vOffset + 0);
                indices[iOffset + 1] = (short)(vOffset + 1);
                indices[iOffset + 2] = (short)(vOffset + 2);
                indices[iOffset + 3] = (short)(vOffset + 0);
                indices[iOffset + 4] = (short)(vOffset + 2);
                indices[iOffset + 5] = (short)(vOffset + 3);
            }

            _overlayEffect.World = Matrix.Identity;
            _overlayEffect.TextureEnabled = true;

            foreach (var pass in _overlayEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 24, indices, 0, 12);
            }
        }

        private void DrawParticles(
            BlockInteractionSystem interaction,
            Matrix view,
            Matrix projection,
            Camera camera)
        {
            _overlayEffect.TextureEnabled = false;
            _overlayEffect.World = Matrix.Identity;

            foreach (var particle in interaction.Particles)
            {
                if (!particle.Active)
                {
                    continue;
                }

                float lifeRatio = particle.Lifetime / particle.MaxLifetime;
                float size = 0.06f * lifeRatio;
                var right = camera.Right * size;
                var up = camera.Up * size;
                var pos = particle.Position;

                var v0 = ToMono(pos - right - up);
                var v1 = ToMono(pos - right + up);
                var v2 = ToMono(pos + right + up);
                var v3 = ToMono(pos + right - up);

                var color = new Color(0.9f, 0.85f, 0.7f, lifeRatio);
                var vertices = new[]
                {
                    new VertexPositionColor(v0, color),
                    new VertexPositionColor(v1, color),
                    new VertexPositionColor(v2, color),
                    new VertexPositionColor(v3, color)
                };
                var indices = new short[] { 0, 1, 2, 0, 2, 3 };

                foreach (var pass in _overlayEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 4, indices, 0, 2);
                }
            }

            _overlayEffect.TextureEnabled = true;
        }

        private static Microsoft.Xna.Framework.Vector3 ToMono(Vector3 v)
        {
            return new Microsoft.Xna.Framework.Vector3(v.X, v.Y, v.Z);
        }

        public void Dispose()
        {
            _overlayEffect.Dispose();
        }
    }
}
