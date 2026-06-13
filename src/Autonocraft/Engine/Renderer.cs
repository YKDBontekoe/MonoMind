using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Core;
using Autonocraft.Entities;
using Autonocraft.Engine.Animation;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{
    public class Renderer : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly BasicEffect _worldEffect;
        private readonly BasicEffect _skyEffect;
        private readonly BasicEffect _hudEffect;
        private readonly Texture2D _atlasTexture;
        private readonly Texture2D _whiteTexture;

        private readonly SpriteBatch _spriteBatch;
        private readonly BlockOverlayRenderer _overlayRenderer;

        // 5x7 Vector Pixel Font (binary rows representing column pixels)
        private static readonly Dictionary<char, byte[]> Font = new Dictionary<char, byte[]>
        {
            ['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E },
            ['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F },
            ['3'] = new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E },
            ['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 },
            ['5'] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E },
            ['6'] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E },
            ['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 },
            ['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E },
            ['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C },
            [':'] = new byte[] { 0x00, 0x04, 0x00, 0x00, 0x00, 0x04, 0x00 },
            ['.'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C },
            [','] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x04, 0x08 },
            ['-'] = new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 },
            ['+'] = new byte[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 },
            ['('] = new byte[] { 0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02 },
            [')'] = new byte[] { 0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08 },
            ['['] = new byte[] { 0x0E, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0E },
            [']'] = new byte[] { 0x0E, 0x02, 0x02, 0x02, 0x02, 0x02, 0x0E },
            ['*'] = new byte[] { 0x00, 0x15, 0x0E, 0x04, 0x0E, 0x15, 0x00 },
            ['/'] = new byte[] { 0x01, 0x02, 0x04, 0x08, 0x10, 0x10, 0x10 },
            ['%'] = new byte[] { 0x18, 0x19, 0x02, 0x04, 0x08, 0x13, 0x03 },
            ['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
            ['C'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E },
            ['D'] = new byte[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C },
            ['E'] = new byte[] { 0x1F, 0x10, 0x10, 0x1C, 0x10, 0x10, 0x1F },
            ['F'] = new byte[] { 0x1F, 0x10, 0x10, 0x1C, 0x10, 0x10, 0x10 },
            ['G'] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F },
            ['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['J'] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C },
            ['K'] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 },
            ['L'] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F },
            ['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x11, 0x11, 0x11, 0x11 },
            ['N'] = new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 },
            ['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 },
            ['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D },
            ['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 },
            ['S'] = new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E },
            ['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
            ['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 },
            ['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 },
            ['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
            ['Y'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 },
            ['Z'] = new byte[] { 0x1F, 0x02, 0x04, 0x08, 0x10, 0x10, 0x1F },
            [' '] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
        };

        public Renderer(GraphicsDevice device, Effect? dummyWorld, Effect? dummySky, Texture2D atlas, Texture2D? dummyHeightmap, Texture2D white)
        {
            _device = device;
            _atlasTexture = atlas;
            _whiteTexture = white;

            _spriteBatch = new SpriteBatch(device);
            _overlayRenderer = new BlockOverlayRenderer(device, atlas);

            // Configure built-in BasicEffect for world chunk rendering
            _worldEffect = new BasicEffect(device)
            {
                TextureEnabled = true,
                Texture = atlas,
                VertexColorEnabled = true,
                PreferPerPixelLighting = true,
                LightingEnabled = true
            };

            // Configure built-in BasicEffect for sky stars/sun/moon quads
            _skyEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };

            // Configure built-in BasicEffect for HUD 3D isometric slots
            _hudEffect = new BasicEffect(device)
            {
                TextureEnabled = true,
                Texture = atlas,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        public void Draw(AutonocraftGame game)
        {
            float sw = _device.Viewport.Width;
            float sh = _device.Viewport.Height;
            float aspect = sw / sh;

            var view = game.Camera.GetViewMatrix();
            int renderDistance = game.RenderDistance;
            var proj = game.Camera.GetProjectionMatrix(aspect, ChunkLod.GetProjectionFarPlane(renderDistance));

            // Time parameters and directions
            float time = game.TimeOfDay;
            float sunAngle = time * MathF.PI * 2.0f;
            var sunDir = new Vector3(0f, MathF.Sin(sunAngle), MathF.Cos(sunAngle));
            var moonDir = -sunDir;

            float sunY = sunDir.Y;
            float dayLight = Math.Clamp((sunY + 0.2f) / 0.4f, 0.0f, 1.0f);
            float sunIntensity = Math.Clamp((sunY - 0.1f) / 0.5f, 0.0f, 1.0f);
            float sunsetFactor = Math.Clamp(1.0f - Math.Abs(sunY) / 0.3f, 0.0f, 1.0f);

            var lightColor = new Microsoft.Xna.Framework.Vector3(1.0f, 0.95f, 0.85f) * (sunIntensity * 0.8f);

            var ambNight = new Microsoft.Xna.Framework.Vector3(0.05f, 0.05f, 0.1f) * 0.2f;
            var ambDay = new Microsoft.Xna.Framework.Vector3(0.4f, 0.45f, 0.6f) * 0.4f;
            var ambColor = Microsoft.Xna.Framework.Vector3.Lerp(ambNight, ambDay, dayLight);

            // Sky gradient Horizon transition colors
            var skyHorizonNight = new Microsoft.Xna.Framework.Vector3(0.05f, 0.05f, 0.1f);
            var skyHorizonDay = new Microsoft.Xna.Framework.Vector3(0.5f, 0.7f, 0.9f);
            var skyHorizonSunset = new Microsoft.Xna.Framework.Vector3(0.8f, 0.4f, 0.2f);

            var skyHorizonBase = Microsoft.Xna.Framework.Vector3.Lerp(skyHorizonNight, skyHorizonDay, dayLight);
            var skyHorizon = Microsoft.Xna.Framework.Vector3.Lerp(skyHorizonBase, skyHorizonSunset, sunsetFactor);

            var clearColor = new Color(skyHorizon.X, skyHorizon.Y, skyHorizon.Z);

            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, clearColor, 1.0f, 0);

            var monoWorld = Matrix.Identity;
            var monoView = ConvertMatrix(view);
            var monoProj = ConvertMatrix(proj);

            // 1. Draw Sun and Moon (billboarded sky bodies)
            DrawSunAndMoon(game.Camera, sunDir, moonDir);

            // 2. Draw world geometry using BasicEffect
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullClockwise;
            _device.SamplerStates[0] = SamplerState.PointClamp;
            _device.BlendState = BlendState.Opaque;

            _worldEffect.World = monoWorld;
            _worldEffect.View = monoView;
            _worldEffect.Projection = monoProj;

            _worldEffect.AmbientLightColor = ambColor;

            _worldEffect.DirectionalLight0.Enabled = sunDir.Y > 0.0f;
            _worldEffect.DirectionalLight0.Direction = ConvertVector(-sunDir);
            _worldEffect.DirectionalLight0.DiffuseColor = lightColor;

            _worldEffect.DirectionalLight1.Enabled = moonDir.Y > 0.0f;
            _worldEffect.DirectionalLight1.Direction = ConvertVector(-moonDir);
            _worldEffect.DirectionalLight1.DiffuseColor = new Microsoft.Xna.Framework.Vector3(0.12f, 0.15f, 0.25f);

            _worldEffect.FogEnabled = true;
            _worldEffect.FogColor = new Microsoft.Xna.Framework.Vector3(skyHorizon.X, skyHorizon.Y, skyHorizon.Z);

            VoxelWorld.GetChunkCoords(
                (int)MathF.Round(game.Camera.Position.X),
                (int)MathF.Round(game.Camera.Position.Z),
                out int agentChunkX,
                out int agentChunkZ,
                out _,
                out _);

            var viewProjection = monoView * monoProj;
            var frustumPlanes = ExtractFrustumPlanes(viewProjection);

            foreach (var chunk in game.Grid.GetActiveChunks())
            {
                if (!IsChunkVisible(chunk, frustumPlanes))
                {
                    continue;
                }

                int chunkDistance = ChunkLod.GetChunkDistance(chunk.ChunkX, chunk.ChunkZ, agentChunkX, agentChunkZ);
                var detail = ChunkLod.SelectDetail(chunkDistance, renderDistance);
                chunk.EnsureMesh(_device, game.Grid, detail);
                var (vb, ib, count) = chunk.GetMesh(detail);

                if (vb != null && ib != null && count > 0)
                {
                    var (fogStart, detailFogEnd) = ChunkLod.GetFogRange(renderDistance, detail);
                    _worldEffect.FogStart = fogStart;
                    _worldEffect.FogEnd = detailFogEnd;

                    _device.SetVertexBuffer(vb);
                    _device.Indices = ib;
                    foreach (var pass in _worldEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, count / 3);
                    }
                }
            }

            DrawAnimals(game, monoView, monoProj, renderDistance, skyHorizon);

            _overlayRenderer.Draw(
                game.BlockInteraction,
                monoView,
                monoProj,
                game.Camera,
                game.BlockInteraction.AnimTime);

            // 3. Draw HUD
            DrawHUD(game, sw, sh);
        }

        private void DrawAnimals(AutonocraftGame game, Matrix view, Matrix proj, int renderDistance, Microsoft.Xna.Framework.Vector3 skyHorizon)
        {
            var cameraPos = game.Camera.Position;
            var animals = game.Animals.GetAnimalsInRange(cameraPos, ChunkLod.GetAnimalCullRadius(renderDistance));
            if (animals.Count == 0)
            {
                return;
            }

            bool prevTextureEnabled = _worldEffect.TextureEnabled;
            _worldEffect.TextureEnabled = true;
            _worldEffect.Texture = _atlasTexture;
            _worldEffect.View = view;
            _worldEffect.Projection = proj;
            _worldEffect.FogEnabled = true;
            _worldEffect.FogColor = skyHorizon;
            _worldEffect.FogStart = ChunkLod.GetFogStart(renderDistance);
            _worldEffect.FogEnd = ChunkLod.GetFogEnd(renderDistance);

            foreach (var animal in animals)
            {
                var stats = animal.Stats;
                float yawRad = MathHelper.ToRadians(-animal.Yaw);
                var rotation = Matrix.CreateRotationY(yawRad);
                var translation = Matrix.CreateTranslation(animal.Position.X, animal.Position.Y, animal.Position.Z);
                var animalWorld = rotation * translation;

                // Default to sheep atlas coordinates (8x8 atlas layout)
                int bodyCol = 4, bodyRow = 2;
                int headCol = 5, headRow = 2;

                switch (animal.Type)
                {
                    case AnimalType.Sheep:
                        bodyCol = 4; bodyRow = 2;
                        headCol = 5; headRow = 2;
                        break;
                    case AnimalType.Pig:
                        bodyCol = 4; bodyRow = 3;
                        headCol = 5; headRow = 3;
                        break;
                    case AnimalType.Chicken:
                        bodyCol = 4; bodyRow = 4;
                        headCol = 5; headRow = 4;
                        break;
                }

                var bodyUV = GetAtlasUVs(bodyCol, bodyRow);
                var headUV = GetAtlasUVs(headCol, headRow);

                float bodyHeight = stats.Height * 0.55f;
                float bodyCenterY = stats.Height * 0.35f;

                // Draw body (all sides textured with body wool/skin texture)
                DrawTexturedBox(animalWorld, stats.Width * 0.45f, bodyHeight * 0.5f, stats.Width * 0.45f, 0f, bodyCenterY, 0f, bodyUV, bodyUV);

                float headSize = stats.Width * 0.22f;
                float headCenterY = stats.Height * 0.72f;
                float headForward = stats.Width * 0.35f;

                // Draw head (front side has face texture, other 5 sides have body wool/skin texture)
                DrawTexturedBox(animalWorld, headSize, headSize, headSize, 0f, headCenterY, headForward, bodyUV, headUV);

                if (stats.HasAccent)
                {
                    // Draw accent (snout/beak/wattles) as a colored box by temporarily disabling textures
                    _worldEffect.TextureEnabled = false;
                    float accentSize = headSize * 0.45f;
                    DrawColoredBox(animalWorld, accentSize, accentSize * 0.6f, accentSize * 0.8f, 0f, headCenterY, headForward + headSize * 1.2f, stats.AccentColor);
                    _worldEffect.TextureEnabled = true;
                }
            }

            _worldEffect.TextureEnabled = prevTextureEnabled;
        }

        private static (float uMin, float vMin, float uMax, float vMax) GetAtlasUVs(int col, int row)
        {
            float gridWidth = 4f;
            float gridHeight = 4f;
            float atlasWidth = 1024f;
            float atlasHeight = 1024f;

            float uMin = col / gridWidth;
            float vMin = row / gridHeight;
            float uMax = uMin + 1f / gridWidth;
            float vMax = vMin + 1f / gridHeight;

            float halfPixelU = 0.5f / atlasWidth;
            float halfPixelV = 0.5f / atlasHeight;
            return (uMin + halfPixelU, vMin + halfPixelV, uMax - halfPixelU, vMax - halfPixelV);
        }

        private void DrawTexturedBox(
            Matrix animalWorld,
            float halfWidth,
            float halfHeight,
            float halfDepth,
            float localCenterX,
            float localCenterY,
            float localCenterZ,
            (float uMin, float vMin, float uMax, float vMax) bodyUV,
            (float uMin, float vMin, float uMax, float vMax) frontUV)
        {
            var localCenter = Matrix.CreateTranslation(localCenterX, localCenterY, localCenterZ);
            _worldEffect.World = localCenter * animalWorld;

            float x0 = -halfWidth;
            float x1 = halfWidth;
            float y0 = -halfHeight;
            float y1 = halfHeight;
            float z0 = -halfDepth;
            float z1 = halfDepth;

            // 8 corners
            var p0 = new Vector3(x0, y0, z1);
            var p1 = new Vector3(x1, y0, z1);
            var p2 = new Vector3(x1, y1, z1);
            var p3 = new Vector3(x0, y1, z1);
            var p4 = new Vector3(x0, y0, z0);
            var p5 = new Vector3(x1, y0, z0);
            var p6 = new Vector3(x1, y1, z0);
            var p7 = new Vector3(x0, y1, z0);

            var colVec = Vector3.One;

            // 6 faces * 4 vertices = 24 vertices
            var vertices = new Vertex[24];

            // 1. Front (+Z) - uses frontUV
            var nFront = new Vector3(0, 0, 1);
            vertices[0] = new Vertex(p0, colVec, nFront, new System.Numerics.Vector2(frontUV.uMin, frontUV.vMax));
            vertices[1] = new Vertex(p1, colVec, nFront, new System.Numerics.Vector2(frontUV.uMax, frontUV.vMax));
            vertices[2] = new Vertex(p2, colVec, nFront, new System.Numerics.Vector2(frontUV.uMax, frontUV.vMin));
            vertices[3] = new Vertex(p3, colVec, nFront, new System.Numerics.Vector2(frontUV.uMin, frontUV.vMin));

            // 2. Right (+X) - uses bodyUV
            var nRight = new Vector3(1, 0, 0);
            vertices[4] = new Vertex(p1, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            vertices[5] = new Vertex(p5, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            vertices[6] = new Vertex(p6, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            vertices[7] = new Vertex(p2, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            // 3. Back (-Z) - uses bodyUV
            var nBack = new Vector3(0, 0, -1);
            vertices[8]  = new Vertex(p5, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            vertices[9]  = new Vertex(p4, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            vertices[10] = new Vertex(p7, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            vertices[11] = new Vertex(p6, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            // 4. Left (-X) - uses bodyUV
            var nLeft = new Vector3(-1, 0, 0);
            vertices[12] = new Vertex(p4, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            vertices[13] = new Vertex(p0, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            vertices[14] = new Vertex(p3, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            vertices[15] = new Vertex(p7, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            // 5. Top (+Y) - uses bodyUV
            var nTop = new Vector3(0, 1, 0);
            vertices[16] = new Vertex(p3, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            vertices[17] = new Vertex(p2, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            vertices[18] = new Vertex(p6, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            vertices[19] = new Vertex(p7, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            // 6. Bottom (-Y) - uses bodyUV
            var nBottom = new Vector3(0, -1, 0);
            vertices[20] = new Vertex(p4, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            vertices[21] = new Vertex(p5, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            vertices[22] = new Vertex(p1, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            vertices[23] = new Vertex(p0, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

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

            foreach (var pass in _worldEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 24, indices, 0, 12);
            }
        }

        private void DrawColoredBox(
            Matrix animalWorld,
            float halfWidth,
            float halfHeight,
            float halfDepth,
            float localCenterX,
            float localCenterY,
            float localCenterZ,
            Color color)
        {
            var localCenter = Matrix.CreateTranslation(localCenterX, localCenterY, localCenterZ);
            _worldEffect.World = localCenter * animalWorld;

            float x0 = -halfWidth;
            float x1 = halfWidth;
            float y0 = -halfHeight;
            float y1 = halfHeight;
            float z0 = -halfDepth;
            float z1 = halfDepth;

            var vertices = new[]
            {
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y0, z1), color),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y0, z1), color),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y1, z1), color),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y1, z1), color),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y0, z0), color),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y0, z0), color),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y1, z0), color),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y1, z0), color),
            };

            var indices = new short[]
            {
                0, 1, 2, 0, 2, 3,
                1, 5, 6, 1, 6, 2,
                5, 4, 7, 5, 7, 6,
                4, 0, 3, 4, 3, 7,
                3, 2, 6, 3, 6, 7,
                4, 5, 1, 4, 1, 0
            };

            foreach (var pass in _worldEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 8, indices, 0, 12);
            }
        }

        private void DrawSunAndMoon(Camera camera, Vector3 sunDir, Vector3 moonDir)
        {
            _device.DepthStencilState = DepthStencilState.None; // Render behind everything else
            _device.RasterizerState = RasterizerState.CullNone;

            float aspect = (float)_device.Viewport.Width / _device.Viewport.Height;
            _skyEffect.View = ConvertMatrix(camera.GetViewMatrix());
            _skyEffect.Projection = ConvertMatrix(camera.GetProjectionMatrix(aspect));
            _skyEffect.World = Matrix.Identity;

            // Render Sun (Flat square Minecraft-style)
            if (sunDir.Y > -0.1f)
            {
                var sunPos = camera.Position + sunDir * 150f;
                var right = camera.Right * 15f;
                var up = camera.Up * 15f;

                var v0 = ConvertVector(sunPos - right - up);
                var v1 = ConvertVector(sunPos - right + up);
                var v2 = ConvertVector(sunPos + right + up);
                var v3 = ConvertVector(sunPos + right - up);

                var sunColor = new Color(1.0f, 0.9f, 0.4f);
                var vertices = new[]
                {
                    new VertexPositionColor(v0, sunColor),
                    new VertexPositionColor(v1, sunColor),
                    new VertexPositionColor(v2, sunColor),
                    new VertexPositionColor(v3, sunColor)
                };
                var indices = new short[] { 0, 1, 2, 0, 2, 3 };

                foreach (var pass in _skyEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 4, indices, 0, 2);
                }
            }

            // Render Moon (Flat square Minecraft-style)
            if (moonDir.Y > -0.1f)
            {
                var moonPos = camera.Position + moonDir * 150f;
                var right = camera.Right * 12f;
                var up = camera.Up * 12f;

                var v0 = ConvertVector(moonPos - right - up);
                var v1 = ConvertVector(moonPos - right + up);
                var v2 = ConvertVector(moonPos + right + up);
                var v3 = ConvertVector(moonPos + right - up);

                var moonColor = new Color(0.9f, 0.9f, 1.0f);
                var vertices = new[]
                {
                    new VertexPositionColor(v0, moonColor),
                    new VertexPositionColor(v1, moonColor),
                    new VertexPositionColor(v2, moonColor),
                    new VertexPositionColor(v3, moonColor)
                };
                var indices = new short[] { 0, 1, 2, 0, 2, 3 };

                foreach (var pass in _skyEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 4, indices, 0, 2);
                }
            }

            _device.DepthStencilState = DepthStencilState.Default; // Restore depth settings
        }

        private void DrawHUD(AutonocraftGame game, float sw, float sh)
        {
            var layout = new UiLayout(sw, sh);
            float cx = layout.CenterX;
            float cy = layout.CenterY;
            var player = game.Player;
            var interaction = game.BlockInteraction;
            int activeChunksCount = game.Grid.GetActiveChunks().Count;
            float crosshairArm = layout.S(10f);
            float crosshairGap = layout.S(3f);

            Color crosshairColor = interaction.Crosshair switch
            {
                CrosshairState.Mining => new Color(1.0f, 0.7f, 0.3f),
                CrosshairState.ValidPlace => new Color(0.3f, 1.0f, 0.4f),
                CrosshairState.InvalidPlace => new Color(1.0f, 0.3f, 0.3f),
                CrosshairState.Flash => Color.White,
                _ => Color.White
            };

            float crosshairAlpha = 0.5f;
            if (interaction.Crosshair == CrosshairState.Mining)
            {
                crosshairArm *= 1.15f;
                crosshairAlpha = 0.7f;
            }
            else if (interaction.Crosshair == CrosshairState.Flash)
            {
                crosshairAlpha = 0.5f + 0.5f * interaction.CrosshairFlashAlpha;
            }

            float hotbarPulse = interaction.HotbarPulseScale;

            // --- HUD BATCH 1: Flat Panels and backgrounds ---
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            // 1. Crosshair lines and center dot
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx - 1), (int)(cy - 1), 2, 2), crosshairColor * crosshairAlpha);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx - crosshairArm), (int)(cy - 0.5f), (int)layout.S(7f), 1), crosshairColor * crosshairAlpha);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx + crosshairGap), (int)(cy - 0.5f), (int)layout.S(7f), 1), crosshairColor * crosshairAlpha);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx - 0.5f), (int)(cy - crosshairArm), 1, (int)layout.S(7f)), crosshairColor * crosshairAlpha);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx - 0.5f), (int)(cy + crosshairGap), 1, (int)layout.S(7f)), crosshairColor * crosshairAlpha);

            // 2. Diagnostics glass panel (Top-Left)
            float diagX = layout.Padding;
            float diagY = layout.Padding;
            float diagW = layout.S(280f);
            float diagH = layout.S(120f);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)diagX, (int)diagY, (int)diagW, (int)diagH), new Color(0.04f, 0.04f, 0.06f) * 0.85f);
            DrawRectOutline(_spriteBatch, diagX, diagY, diagW, diagH, 1f, new Color(0.2f, 0.3f, 0.4f), 0.6f);

            // 3. Bottom HUD Status Bars (edge-anchored for ultra-wide)
            float barW = layout.S(200f);
            float barH = layout.S(8f);
            float barY = layout.Height - layout.S(40f);

            // Health (Bottom Left)
            float hpX = layout.Padding;
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(hpX - 2), (int)(barY - 2), (int)(barW + 4), (int)(barH + 4)), Color.Black * 0.7f);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)hpX, (int)barY, (int)barW, (int)barH), new Color(0.15f, 0.05f, 0.05f) * 0.9f);
            float hpRatio = Math.Clamp(player.Health / player.MaxHealth, 0f, 1f);
            if (hpRatio > 0.01f)
            {
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)hpX, (int)barY, (int)(barW * hpRatio), (int)barH), new Color(1.0f, 0.15f, 0.25f));
            }

            // Flight energy / Stamina (Bottom Right)
            float enX = layout.Width - layout.Padding - barW;
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(enX - 2), (int)(barY - 2), (int)(barW + 4), (int)(barH + 4)), Color.Black * 0.7f);
            if (player.FlyingMode)
            {
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)enX, (int)barY, (int)barW, (int)barH), new Color(0.05f, 0.15f, 0.25f) * 0.9f);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)enX, (int)barY, (int)barW, (int)barH), new Color(0.2f, 0.75f, 1.0f));
            }
            else
            {
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)enX, (int)barY, (int)barW, (int)barH), new Color(0.05f, 0.15f, 0.05f) * 0.9f);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)enX, (int)barY, (int)barW, (int)barH), new Color(0.2f, 0.9f, 0.4f));
            }

            // 4. Hotbar plate background (Bottom-Center)
            float slotSize = layout.S(44f);
            float slotSpacing = layout.S(6f);
            float totalWidth = (9 * slotSize) + (8 * slotSpacing);
            float hotbarXMin = cx - totalWidth / 2f;
            float hotbarYMin = layout.Height - layout.S(60f);
            float hotbarPad = layout.S(10f);

            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(hotbarXMin - hotbarPad), (int)(hotbarYMin - hotbarPad), (int)(totalWidth + hotbarPad * 2), (int)(slotSize + hotbarPad * 2)), new Color(0.02f, 0.02f, 0.03f) * 0.85f);
            DrawRectOutline(_spriteBatch, hotbarXMin - hotbarPad, hotbarYMin - hotbarPad, totalWidth + hotbarPad * 2, slotSize + hotbarPad * 2, 1f, new Color(0.15f, 0.15f, 0.2f), 0.8f);

            // Draw hotbar slots
            for (int i = 0; i < 9; i++)
            {
                float slotXMin = hotbarXMin + i * (slotSize + slotSpacing);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)slotXMin, (int)hotbarYMin, (int)slotSize, (int)slotSize), new Color(0.08f, 0.08f, 0.1f) * 0.8f);

                if (i == player.SelectedSlot)
                {
                    float pulsePad = 2f * hotbarPulse;
                    float pulseSize = (slotSize + 4f) * hotbarPulse;
                    float pulseX = slotXMin - pulsePad;
                    float pulseY = hotbarYMin - pulsePad;
                    DrawRectOutline(_spriteBatch, pulseX, pulseY, pulseSize, pulseSize, 2f, new Color(0.0f, 0.8f, 1.0f), 1.0f);
                    _spriteBatch.Draw(_whiteTexture, new Rectangle((int)slotXMin, (int)hotbarYMin, (int)slotSize, (int)slotSize), new Color(0.15f, 0.25f, 0.35f) * 0.4f);
                }
                else
                {
                    DrawRectOutline(_spriteBatch, slotXMin, hotbarYMin, slotSize, slotSize, 1f, new Color(0.15f, 0.15f, 0.18f), 1.0f);
                }
            }

            _spriteBatch.End();

            // --- HUD BATCH 2: 3D Isometric block icons ---
            _hudEffect.View = Matrix.CreateLookAt(new Microsoft.Xna.Framework.Vector3(0, 0, 1), Microsoft.Xna.Framework.Vector3.Zero, Microsoft.Xna.Framework.Vector3.Up);
            _hudEffect.Projection = Matrix.CreateOrthographicOffCenter(0, sw, sh, 0, -1, 1);
            _hudEffect.World = Matrix.Identity;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.PointClamp;

            for (int i = 0; i < 9; i++)
            {
                var slotItem = player.Hotbar[i];
                if (slotItem.Type != BlockType.Air)
                {
                    float slotXMin = hotbarXMin + i * (slotSize + slotSpacing);
                    float slotCx = slotXMin + slotSize / 2f;
                    float slotCy = hotbarYMin + slotSize / 2f;

                    DrawIsometricBlock(slotCx, slotCy + layout.S(8f), layout.S(14f), slotItem.Type);
                }
            }

            // --- HUD BATCH 3: Diagnostics & Inventory Text Overlays ---
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            // Diagnostics panel labels
            float diagTextPad = layout.S(15f);
            float diagLineH = layout.S(18f);
            DrawString(_spriteBatch, "AUTONOCRAFT ENGINE", diagX + diagTextPad, diagY + diagTextPad, layout.S(1.4f), new Color(0.8f, 0.9f, 1.0f), 1.0f);
            DrawString(_spriteBatch, $"POS:  {player.Position.X:F1}, {player.Position.Y:F1}, {player.Position.Z:F1}", diagX + diagTextPad, diagY + diagTextPad + diagLineH, layout.S(1.2f), new Color(0.7f, 0.7f, 0.75f), 1.0f);
            DrawString(_spriteBatch, $"DIR:  {GetDirection(player.Yaw)} ({player.Yaw:F0}*)", diagX + diagTextPad, diagY + diagTextPad + diagLineH * 2f, layout.S(1.2f), new Color(0.7f, 0.7f, 0.75f), 1.0f);
            DrawString(_spriteBatch, $"MODE: {(player.FlyingMode ? "CREATIVE" : "SURVIVAL")}", diagX + diagTextPad, diagY + diagTextPad + diagLineH * 3f, layout.S(1.2f), player.FlyingMode ? new Color(0.3f, 0.8f, 1.0f) : new Color(1.0f, 0.6f, 0.2f), 1.0f);
            DrawString(_spriteBatch, $"LOAD: {activeChunksCount} CHUNKS", diagX + diagTextPad, diagY + diagTextPad + diagLineH * 4f, layout.S(1.2f), new Color(0.4f, 0.9f, 0.6f), 1.0f);

            // Status bar labels
            float labelSize = layout.S(1.1f);
            DrawString(_spriteBatch, "HP", hpX, barY - layout.S(14f), labelSize, new Color(1.0f, 0.15f, 0.25f), 1.0f);
            if (player.FlyingMode)
            {
                DrawString(_spriteBatch, "FLIGHT", enX + barW - layout.S(44f), barY - layout.S(14f), labelSize, new Color(0.2f, 0.75f, 1.0f), 1.0f);
            }
            else
            {
                DrawString(_spriteBatch, "STAMINA", enX + barW - layout.S(52f), barY - layout.S(14f), labelSize, new Color(0.2f, 0.9f, 0.4f), 1.0f);
            }

            // Selected block name above hotbar
            var activeType = player.GetSelectedBlockType();
            if (activeType != BlockType.Air)
            {
                string activeName = activeType.ToString().ToUpperInvariant();
                int count = player.Hotbar[player.SelectedSlot].Count;
                string labelText = $"{activeName} ({count})";
                float activeLabelSize = layout.S(1.2f);
                float labelWidth = labelText.Length * 6f * activeLabelSize;
                DrawString(_spriteBatch, labelText, cx - labelWidth / 2f, hotbarYMin - layout.S(28f), activeLabelSize, Color.White, 0.9f);
            }

            // Block inventory slot counts
            float countLabelSize = layout.S(1.0f);
            for (int i = 0; i < 9; i++)
            {
                var slotItem = player.Hotbar[i];
                if (slotItem.Type != BlockType.Air)
                {
                    float slotXMin = hotbarXMin + i * (slotSize + slotSpacing);
                    float slotXMax = slotXMin + slotSize;
                    float slotYMax = hotbarYMin + slotSize;

                    string countStr = slotItem.Count.ToString();
                    float textX = slotXMax - countStr.Length * 6f * countLabelSize - layout.S(2f);
                    float textY = slotYMax - layout.S(9f);

                    // Text shadow
                    DrawString(_spriteBatch, countStr, textX + 1f, textY + 1f, countLabelSize, Color.Black, 0.8f);
                    // Text foreground
                    DrawString(_spriteBatch, countStr, textX, textY, countLabelSize, Color.White, 1.0f);
                }
            }

            _spriteBatch.End();
        }

        private void DrawRectOutline(SpriteBatch sb, float x, float y, float w, float h, float thickness, Color color, float alpha)
        {
            Color drawCol = color * alpha;
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)thickness), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + h - thickness), (int)w, (int)thickness), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)(x + w - thickness), (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), drawCol);
        }

        private void DrawString(SpriteBatch sb, string text, float startX, float startY, float pixelSize, Color color, float alpha)
        {
            float curX = startX;
            Color drawCol = color * alpha;
            foreach (char c in text.ToUpperInvariant())
            {
                char lookup = Font.ContainsKey(c) ? c : ' ';
                byte[] rows = Font[lookup];

                for (int r = 0; r < 7; r++)
                {
                    byte rowVal = rows[r];
                    for (int col = 0; col < 5; col++)
                    {
                        if (((rowVal >> (4 - col)) & 1) == 1)
                        {
                            int px = (int)(curX + col * pixelSize);
                            int py = (int)(startY + r * pixelSize);
                            int sz = (int)Math.Max(1f, pixelSize);
                            sb.Draw(_whiteTexture, new Rectangle(px, py, sz, sz), drawCol);
                        }
                    }
                }
                curX += 6f * pixelSize;
            }
        }

        private void DrawIsometricBlock(float cx, float cy, float r, BlockType type)
        {
            float h = r * 0.5f;
            float w = r * 0.866f;

            var pTop = new Microsoft.Xna.Framework.Vector3(cx, cy - r, 0f);
            var pBottom = new Microsoft.Xna.Framework.Vector3(cx, cy + r, 0f);
            var pLeft = new Microsoft.Xna.Framework.Vector3(cx - w, cy - h, 0f);
            var pRight = new Microsoft.Xna.Framework.Vector3(cx + w, cy - h, 0f);
            var pCenter = new Microsoft.Xna.Framework.Vector3(cx, cy, 0f);
            var pBottomLeft = new Microsoft.Xna.Framework.Vector3(cx - w, cy + h, 0f);
            var pBottomRight = new Microsoft.Xna.Framework.Vector3(cx + w, cy + h, 0f);

            var uvTop = BlockAtlas.GetFaceUVs(type, new Vector3(0f, 1f, 0f));
            var uvSide = BlockAtlas.GetFaceUVs(type, new Vector3(0f, 0f, 1f));

            var vertices = new VertexPositionColorTexture[12];
            
            // Top Face (1.0 brightness)
            vertices[0] = new VertexPositionColorTexture(pTop, Color.White, new Microsoft.Xna.Framework.Vector2(uvTop.uMin, uvTop.vMin));
            vertices[1] = new VertexPositionColorTexture(pLeft, Color.White, new Microsoft.Xna.Framework.Vector2(uvTop.uMin, uvTop.vMax));
            vertices[2] = new VertexPositionColorTexture(pCenter, Color.White, new Microsoft.Xna.Framework.Vector2(uvTop.uMax, uvTop.vMax));
            vertices[3] = new VertexPositionColorTexture(pRight, Color.White, new Microsoft.Xna.Framework.Vector2(uvTop.uMax, uvTop.vMin));

            // Left Face (0.8 brightness)
            var leftColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            vertices[4] = new VertexPositionColorTexture(pLeft, leftColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMin, uvSide.vMin));
            vertices[5] = new VertexPositionColorTexture(pBottomLeft, leftColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMin, uvSide.vMax));
            vertices[6] = new VertexPositionColorTexture(pBottom, leftColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMax, uvSide.vMax));
            vertices[7] = new VertexPositionColorTexture(pCenter, leftColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMax, uvSide.vMin));

            // Right Face (0.6 brightness)
            var rightColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            vertices[8] = new VertexPositionColorTexture(pCenter, rightColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMin, uvSide.vMin));
            vertices[9] = new VertexPositionColorTexture(pBottom, rightColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMin, uvSide.vMax));
            vertices[10] = new VertexPositionColorTexture(pBottomRight, rightColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMax, uvSide.vMax));
            vertices[11] = new VertexPositionColorTexture(pRight, rightColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMax, uvSide.vMin));

            var indices = new short[]
            {
                0, 1, 2, 0, 2, 3, // Top
                4, 5, 6, 4, 6, 7, // Left
                8, 9, 10, 8, 10, 11 // Right
            };

            foreach (var pass in _hudEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 12, indices, 0, 6);
            }
        }

        private static Microsoft.Xna.Framework.Vector4[] ExtractFrustumPlanes(Matrix viewProjection)
        {
            var planes = new Microsoft.Xna.Framework.Vector4[6];

            planes[0] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M14 + viewProjection.M11,
                viewProjection.M24 + viewProjection.M21,
                viewProjection.M34 + viewProjection.M31,
                viewProjection.M44 + viewProjection.M41);
            planes[1] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M14 - viewProjection.M11,
                viewProjection.M24 - viewProjection.M21,
                viewProjection.M34 - viewProjection.M31,
                viewProjection.M44 - viewProjection.M41);
            planes[2] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M14 + viewProjection.M12,
                viewProjection.M24 + viewProjection.M22,
                viewProjection.M34 + viewProjection.M32,
                viewProjection.M44 + viewProjection.M42);
            planes[3] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M14 - viewProjection.M12,
                viewProjection.M24 - viewProjection.M22,
                viewProjection.M34 - viewProjection.M32,
                viewProjection.M44 - viewProjection.M42);
            planes[4] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M13,
                viewProjection.M23,
                viewProjection.M33,
                viewProjection.M43);
            planes[5] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M14 - viewProjection.M13,
                viewProjection.M24 - viewProjection.M23,
                viewProjection.M34 - viewProjection.M33,
                viewProjection.M44 - viewProjection.M43);

            for (int i = 0; i < planes.Length; i++)
            {
                float length = MathF.Sqrt(
                    planes[i].X * planes[i].X +
                    planes[i].Y * planes[i].Y +
                    planes[i].Z * planes[i].Z);
                if (length > 0f)
                {
                    planes[i] /= length;
                }
            }

            return planes;
        }

        private static bool IsChunkVisible(Chunk chunk, Microsoft.Xna.Framework.Vector4[] planes)
        {
            float minX = chunk.ChunkX * Chunk.Width;
            float minY = 0f;
            float minZ = chunk.ChunkZ * Chunk.Depth;
            float maxX = minX + Chunk.Width;
            float maxY = Chunk.Height;
            float maxZ = minZ + Chunk.Depth;

            for (int i = 0; i < planes.Length; i++)
            {
                float px = planes[i].X >= 0f ? maxX : minX;
                float py = planes[i].Y >= 0f ? maxY : minY;
                float pz = planes[i].Z >= 0f ? maxZ : minZ;

                if (planes[i].X * px + planes[i].Y * py + planes[i].Z * pz + planes[i].W < 0f)
                {
                    return false;
                }
            }

            return true;
        }

        private Matrix ConvertMatrix(Matrix4x4 m)
        {
            return new Matrix(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44
            );
        }

        private Microsoft.Xna.Framework.Vector3 ConvertVector(Vector3 v)
        {
            return new Microsoft.Xna.Framework.Vector3(v.X, v.Y, v.Z);
        }

        private string GetDirection(float yaw)
        {
            float angle = (yaw % 360f + 360f) % 360f;
            if (angle >= 45f && angle < 135f) return "SOUTH";
            if (angle >= 135f && angle < 225f) return "WEST";
            if (angle >= 225f && angle < 315f) return "NORTH";
            return "EAST";
        }

        public void Dispose()
        {
            _overlayRenderer.Dispose();
            _worldEffect.Dispose();
            _skyEffect.Dispose();
            _hudEffect.Dispose();
            _spriteBatch.Dispose();
        }
    }
}
