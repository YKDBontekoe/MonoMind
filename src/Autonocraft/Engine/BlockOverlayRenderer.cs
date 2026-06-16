using System;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Core;
using Autonocraft.Engine.Animation;
using Autonocraft.Village;
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
        private Texture2D _atlasTexture;
        private readonly Vertex[] _texturedBatch = new Vertex[ParticleSystem.MaxParticles * 4];
        private readonly VertexPositionColor[] _colorBatch = new VertexPositionColor[ParticleSystem.MaxParticles * 4];
        private readonly short[] _quadIndices = BuildQuadIndices(ParticleSystem.MaxParticles);

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
            ParticleSystem particles,
            Matrix view,
            Matrix projection,
            Camera camera,
            float animTime,
            BlueprintPlacementPreview? blueprintPlacement = null,
            IReadOnlyList<BlueprintPlacementPreview>? constructionSites = null,
            WorkZonePlacementPreview? workZonePlacement = null)
        {
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.PointClamp;
            _device.BlendState = BlendState.AlphaBlend;

            _overlayEffect.View = view;
            _overlayEffect.Projection = projection;
            _overlayEffect.Alpha = 1f;

            if (blueprintPlacement != null)
            {
                DrawBlueprintGhost(blueprintPlacement);
            }

            if (constructionSites != null)
            {
                foreach (var site in constructionSites)
                {
                    DrawBlueprintGhost(site);
                }
            }

            if (workZonePlacement != null && workZonePlacement.HasFirstCorner)
            {
                DrawWorkZonePreview(workZonePlacement);
            }

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
                float eased = Tween.EaseOut(t);
                float scale = eased < 0.7f
                    ? 0.8f + 0.25f * (eased / 0.7f)
                    : 1.05f - 0.05f * ((eased - 0.7f) / 0.3f);
                var popTint = new Color(1f, 1f, 1f, 0.7f * (1f - t));
                DrawScaledBlock(interaction.PlacePop.Position, interaction.PlacePop.BlockType, scale, popTint);
            }

            DrawParticles(particles, camera);
        }

        private void DrawBlueprintGhost(BlueprintPlacementPreview preview)
        {
            var tint = preview.IsQueuedConstruction
                ? new Color(0.95f, 0.72f, 0.18f, 0.48f)
                : preview.Valid
                    ? new Color(0.35f, 0.92f, 0.45f, 0.52f)
                    : new Color(0.95f, 0.28f, 0.28f, 0.45f);

            if (!preview.IsQueuedConstruction)
            {
                foreach (var block in preview.Blueprint.Template.Blocks)
                {
                    if (block.Type == BlockType.Air)
                    {
                        continue;
                    }

                    int wx = preview.AnchorX + block.Dx;
                    int wy = preview.AnchorY + block.Dy;
                    int wz = preview.AnchorZ + block.Dz;
                    DrawGhostBlock(new Vector3(wx, wy, wz), block.Type, tint);
                }
            }

            BlueprintPlacementHelper.GetWorldBounds(
                preview.Blueprint,
                preview.AnchorX,
                preview.AnchorY,
                preview.AnchorZ,
                out int minX,
                out int minY,
                out int minZ,
                out int maxX,
                out int maxY,
                out int maxZ);

            var frame = preview.IsQueuedConstruction
                ? new Color(0.95f, 0.72f, 0.18f, 0.9f)
                : preview.Valid
                    ? new Color(0.35f, 0.92f, 0.45f, 0.9f)
                    : new Color(0.95f, 0.28f, 0.28f, 0.9f);
            DrawBoundsWireframe(minX, minY, minZ, maxX + 1, maxY + 1, maxZ + 1, frame);
        }

        private void DrawWorkZonePreview(WorkZonePlacementPreview preview)
        {
            var tint = preview.Valid
                ? new Color(0.95f, 0.72f, 0.18f, 0.75f)
                : new Color(0.95f, 0.28f, 0.28f, 0.65f);

            DrawBoundsWireframe(
                preview.MinX,
                preview.MinY,
                preview.MinZ,
                preview.MaxX + 1,
                preview.MaxY + 1,
                preview.MaxZ + 1,
                tint);
        }

        private void DrawBoundsWireframe(int minX, int minY, int minZ, int maxX, int maxY, int maxZ, Color color)
        {
            float x0 = minX + 0.002f;
            float y0 = minY + 0.002f;
            float z0 = minZ + 0.002f;
            float x1 = maxX - 0.002f;
            float y1 = maxY - 0.002f;
            float z1 = maxZ - 0.002f;

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
            if (stage <= 0) return;

            float offset = 0.002f; // prevent z-fighting
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
            float half = 0.495f;

            var p0 = center - tangent * half - bitangent * half;
            var p1 = center + tangent * half - bitangent * half;
            var p2 = center + tangent * half + bitangent * half;
            var p3 = center - tangent * half + bitangent * half;

            // Subtle dark background shading representing block fractures
            float intensity = 0.1f + stage * 0.025f;
            var col = new Vector3(0.04f, 0.04f, 0.04f) * intensity;
            var vertices = new Vertex[]
            {
                new Vertex(p0, col, n, Vector2.Zero),
                new Vertex(p1, col, n, Vector2.Zero),
                new Vertex(p2, col, n, Vector2.Zero),
                new Vertex(p3, col, n, Vector2.Zero)
            };

            var indices = new short[] { 0, 1, 2, 0, 2, 3 };
            _overlayEffect.World = Matrix.Identity;
            _overlayEffect.TextureEnabled = false;

            _device.BlendState = BlendState.AlphaBlend;

            foreach (var pass in _overlayEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 4, indices, 0, 2);
            }

            // Draw organic branching crack lines
            DrawCrackLines(blockPos, stage, p0, p1, p2, p3);

            _overlayEffect.TextureEnabled = true;
        }

        private void DrawCrackLines(
            Vector3 blockPos,
            int stage,
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3)
        {
            if (stage <= 0) return;

            int seed = HashBlockSeed(blockPos);
            var rng = new Random(seed);

            var points = new System.Collections.Generic.List<Vector2>();
            var segments = new System.Collections.Generic.List<(int from, int to)>();

            // Center of block face
            points.Add(new Vector2(0.5f, 0.5f));

            int mainBranchesCount = 3 + rng.Next(2); // 3 to 4 cracks
            for (int b = 0; b < mainBranchesCount; b++)
            {
                float angle = b * (MathF.PI * 2f / mainBranchesCount) + (float)(rng.NextDouble() - 0.5) * 0.4f;
                float maxLen = 0.15f + stage * 0.045f;
                
                int segmentsInBranch = 2 + stage / 3;
                int lastIdx = 0;
                
                for (int s = 1; s <= segmentsInBranch; s++)
                {
                    float t = s / (float)segmentsInBranch;
                    float currentLen = maxLen * t;
                    float jitterAngle = angle + (float)(rng.NextDouble() - 0.5) * 0.25f;
                    
                    var offset = new Vector2(MathF.Cos(jitterAngle), MathF.Sin(jitterAngle)) * currentLen;
                    var pt = new Vector2(0.5f, 0.5f) + offset;
                    
                    pt.X = Math.Clamp(pt.X, 0.02f, 0.98f);
                    pt.Y = Math.Clamp(pt.Y, 0.02f, 0.98f);
                    
                    points.Add(pt);
                    int newIdx = points.Count - 1;
                    segments.Add((lastIdx, newIdx));
                    lastIdx = newIdx;
                }

                if (stage >= 3 && rng.NextDouble() < 0.7)
                {
                    int parentIdx = points.Count - 1 - rng.Next(2);
                    if (parentIdx > 0)
                    {
                        float subAngle = angle + (rng.Next(2) == 0 ? 1f : -1f) * (1.0f + (float)rng.NextDouble() * 0.4f);
                        float subLen = maxLen * 0.4f;
                        var subOffset = new Vector2(MathF.Cos(subAngle), MathF.Sin(subAngle)) * subLen;
                        var subPt = points[parentIdx] + subOffset;
                        
                        subPt.X = Math.Clamp(subPt.X, 0.02f, 0.98f);
                        subPt.Y = Math.Clamp(subPt.Y, 0.02f, 0.98f);
                        
                        points.Add(subPt);
                        segments.Add((parentIdx, points.Count - 1));
                    }
                }
            }

            var lineVerts = new VertexPositionColor[segments.Count * 2];
            int vi = 0;
            var crackColor = new Color(0.04f, 0.04f, 0.04f, 0.70f + stage * 0.03f);

            Vector3 Local(Vector2 uv) =>
                p0 + (p1 - p0) * uv.X + (p3 - p0) * uv.Y;

            foreach (var segment in segments)
            {
                lineVerts[vi++] = new VertexPositionColor(Local(points[segment.from]), crackColor);
                lineVerts[vi++] = new VertexPositionColor(Local(points[segment.to]), crackColor);
            }

            _overlayEffect.World = Matrix.Identity;
            _overlayEffect.TextureEnabled = false;

            foreach (var pass in _overlayEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.LineList, lineVerts, 0, segments.Count);
            }
        }

        private static int HashBlockSeed(Vector3 blockPos)
        {
            int bx = (int)blockPos.X;
            int by = (int)blockPos.Y;
            int bz = (int)blockPos.Z;
            unchecked
            {
                return bx * 73856093 ^ by * 19349663 ^ bz * 83492791;
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

        private static short[] BuildQuadIndices(int maxQuads)
        {
            var indices = new short[maxQuads * 6];
            for (int q = 0; q < maxQuads; q++)
            {
                int v = q * 4;
                int i = q * 6;
                indices[i + 0] = (short)(v + 0);
                indices[i + 1] = (short)(v + 1);
                indices[i + 2] = (short)(v + 2);
                indices[i + 3] = (short)(v + 0);
                indices[i + 4] = (short)(v + 2);
                indices[i + 5] = (short)(v + 3);
            }

            return indices;
        }

        private void DrawParticles(ParticleSystem particles, Camera camera)
        {
            int texturedCount = 0;
            int colorCount = 0;

            foreach (var particle in particles.Particles)
            {
                if (!particle.Active)
                {
                    continue;
                }

                float lifeRatio = particle.MaxLifetime > 0f
                    ? Math.Clamp(particle.Lifetime / particle.MaxLifetime, 0f, 1f)
                    : 0f;
                float alpha = GetParticleAlpha(particle.Kind, lifeRatio);
                float size = particle.Size * (0.5f + 0.5f * lifeRatio);

                GetBillboardAxes(camera, particle.Rotation, size, out var right, out var up);
                if (particle.Kind == ParticleKind.RainDrop)
                {
                    right = camera.Right * (size * 0.12f);
                    up = Vector3.UnitY * (size * 3.0f);
                }
                var pos = particle.Position;

                if (particle.UseTexture)
                {
                    int offset = texturedCount * 4;
                    if (offset + 4 > _texturedBatch.Length)
                    {
                        break;
                    }

                    var uv = BlockAtlas.GetFaceUVs(particle.BlockType, new Vector3(0f, 1f, 0f));
                    float inset = 0.15f;
                    float u0 = uv.uMin + (uv.uMax - uv.uMin) * inset;
                    float u1 = uv.uMax - (uv.uMax - uv.uMin) * inset;
                    float v0 = uv.vMin + (uv.vMax - uv.vMin) * inset;
                    float v1 = uv.vMax - (uv.vMax - uv.vMin) * inset;
                    var color = particle.Color * alpha;

                    AddTexturedQuad(offset, pos, right, up, color, u0, v0, u1, v1);
                    texturedCount++;
                }
                else
                {
                    int offset = colorCount * 4;
                    if (offset + 4 > _colorBatch.Length)
                    {
                        break;
                    }

                    var color = new Color(
                        particle.Color.X,
                        particle.Color.Y,
                        particle.Color.Z,
                        alpha);
                    AddColorQuad(offset, pos, right, up, color);
                    colorCount++;
                }
            }

            _overlayEffect.World = Matrix.Identity;

            if (texturedCount > 0)
            {
                _overlayEffect.TextureEnabled = true;
                foreach (var pass in _overlayEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _texturedBatch,
                        0,
                        texturedCount * 4,
                        _quadIndices,
                        0,
                        texturedCount * 2);
                }
            }

            if (colorCount > 0)
            {
                _overlayEffect.TextureEnabled = false;
                foreach (var pass in _overlayEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _colorBatch,
                        0,
                        colorCount * 4,
                        _quadIndices,
                        0,
                        colorCount * 2);
                }
            }

            _overlayEffect.TextureEnabled = true;
        }

        private static float GetParticleAlpha(ParticleKind kind, float lifeRatio)
        {
            return kind switch
            {
                ParticleKind.Spark => lifeRatio * lifeRatio,
                ParticleKind.Hint => lifeRatio * 0.85f,
                ParticleKind.Dust => lifeRatio * 0.7f,
                ParticleKind.Bubble => lifeRatio * 0.55f,
                ParticleKind.DustMote => Math.Clamp(lifeRatio * (1f - lifeRatio) * 4f, 0f, 1f) * 0.45f,
                ParticleKind.Firefly => (0.4f + 0.6f * MathF.Sin(lifeRatio * MathF.PI * 8f)) * Math.Clamp(lifeRatio * (1f - lifeRatio) * 4f, 0f, 1f),
                ParticleKind.FallingLeaf => lifeRatio * 0.9f,
                ParticleKind.RainDrop => 0.45f * lifeRatio,
                ParticleKind.SnowFlake => 0.85f * lifeRatio,
                _ => lifeRatio
            };
        }

        private static void GetBillboardAxes(Camera camera, float rotation, float size, out Vector3 right, out Vector3 up)
        {
            right = camera.Right * size;
            up = camera.Up * size;

            if (MathF.Abs(rotation) <= 0.001f)
            {
                return;
            }

            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);
            var rotatedRight = right * cos + up * sin;
            up = up * cos - right * sin;
            right = rotatedRight;
        }

        private void AddTexturedQuad(
            int offset,
            Vector3 pos,
            Vector3 right,
            Vector3 up,
            Vector3 color,
            float u0,
            float v0,
            float u1,
            float v1)
        {
            var n = Vector3.UnitZ;
            _texturedBatch[offset + 0] = new Vertex(pos - right - up, color, n, new Vector2(u0, v1));
            _texturedBatch[offset + 1] = new Vertex(pos - right + up, color, n, new Vector2(u0, v0));
            _texturedBatch[offset + 2] = new Vertex(pos + right + up, color, n, new Vector2(u1, v0));
            _texturedBatch[offset + 3] = new Vertex(pos + right - up, color, n, new Vector2(u1, v1));
        }

        private void AddColorQuad(int offset, Vector3 pos, Vector3 right, Vector3 up, Color color)
        {
            _colorBatch[offset + 0] = new VertexPositionColor(ToMono(pos - right - up), color);
            _colorBatch[offset + 1] = new VertexPositionColor(ToMono(pos - right + up), color);
            _colorBatch[offset + 2] = new VertexPositionColor(ToMono(pos + right + up), color);
            _colorBatch[offset + 3] = new VertexPositionColor(ToMono(pos + right - up), color);
        }

        private static Microsoft.Xna.Framework.Vector3 ToMono(Vector3 v)
        {
            return new Microsoft.Xna.Framework.Vector3(v.X, v.Y, v.Z);
        }

        public void SetAtlasTexture(Texture2D atlas)
        {
            _atlasTexture = atlas;
            _overlayEffect.Texture = atlas;
        }

        public void Dispose()
        {
            _overlayEffect.Dispose();
        }
    }
}
