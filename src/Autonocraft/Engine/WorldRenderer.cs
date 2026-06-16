using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Core;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{
    public readonly struct VisibleChunkDrawInfo
    {
        public Chunk Chunk { get; }
        public ChunkMeshDetail RenderDetail { get; }
        public int ChunkDistance { get; }

        public VisibleChunkDrawInfo(Chunk chunk, ChunkMeshDetail renderDetail, int chunkDistance)
        {
            Chunk = chunk;
            RenderDetail = renderDetail;
            ChunkDistance = chunkDistance;
        }
    }

    public sealed class WorldRenderer : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly BasicEffect _worldEffect;
        private readonly SkyEffect _skyEffect;
        private readonly SkyDomeRenderer _skyDomeRenderer;
        private Texture2D _atlasTexture;
        private readonly BlockTerrainEffect _blockTerrainEffect;
        private readonly BlockOverlayRenderer _overlayRenderer;
        private readonly FloraRenderer _floraRenderer;
        private readonly CloudLayerRenderer _cloudLayerRenderer;
        private readonly Texture2D _whiteTexture;
        private readonly SpriteBatch _spriteBatch;
        private readonly Microsoft.Xna.Framework.Vector4[] _frustumPlanes = new Microsoft.Xna.Framework.Vector4[6];
        private readonly List<VisibleChunkDrawInfo> _visibleChunksScratch = new();

        private readonly System.Diagnostics.Stopwatch _drawStopwatch = System.Diagnostics.Stopwatch.StartNew();
        private Vector3 _currentSkyHorizon;
        private Vector3 _currentSkyZenith;
        private Vector3 _currentAmbientColor;
        private Vector3 _currentSunColor;
        private Vector3 _currentMoonColor;
        private float _currentFogMultiplier = 1.0f;
        private bool _isLightingInitialized = false;
        private Vertex[] _waterScratch = Array.Empty<Vertex>();

        private static readonly Vertex[] TexturedBoxVertices = new Vertex[24];
        private static readonly short[] TexturedBoxIndices = BuildBoxIndices();
        private static readonly VertexPositionColor[] ColoredBoxVertices = new VertexPositionColor[8];
        private static readonly short[] ColoredBoxIndices =
        {
            0, 1, 2, 0, 2, 3,
            1, 5, 6, 1, 6, 2,
            5, 4, 7, 5, 7, 6,
            4, 0, 3, 4, 3, 7,
            3, 2, 6, 3, 6, 7,
            4, 5, 1, 4, 1, 0
        };

        public WorldRenderer(GraphicsDevice device, Texture2D atlas, Texture2D white, BlockTerrainEffect blockTerrainEffect, SkyEffect skyEffect, bool highQualityLighting = false)
        {
            _device = device;
            _atlasTexture = atlas;
            _whiteTexture = white;
            _blockTerrainEffect = blockTerrainEffect;
            _skyEffect = skyEffect;
            _spriteBatch = new SpriteBatch(device);
            _cloudLayerRenderer = new CloudLayerRenderer(device);
            _skyDomeRenderer = new SkyDomeRenderer(device);

            _overlayRenderer = new BlockOverlayRenderer(device, atlas);
            _floraRenderer = new FloraRenderer(device, atlas);

            _worldEffect = new BasicEffect(device)
            {
                TextureEnabled = true,
                Texture = atlas,
                VertexColorEnabled = true,
                PreferPerPixelLighting = highQualityLighting,
                LightingEnabled = true
            };
        }

        public void SetPreferPerPixelLighting(bool enabled)
        {
            _worldEffect.PreferPerPixelLighting = enabled;
        }

        public void Draw(GameRenderContext ctx)
        {
            if (ctx.Grid == null)
            {
                return;
            }

            float sw = _device.Viewport.Width;
            float sh = _device.Viewport.Height;
            float aspect = sw / sh;

            var view = ctx.Camera.GetViewMatrix();
            int renderDistance = ctx.RenderDistance;
            var proj = ctx.Camera.GetProjectionMatrix(aspect, ChunkLod.GetProjectionFarPlane(renderDistance));

            // Get player's active biome smoothly
            int px = (int)MathF.Round(ctx.Camera.Position.X);
            int pz = (int)MathF.Round(ctx.Camera.Position.Z);
            BiomeType playerBiome = ctx.Grid.SampleBiome(px, pz).Primary;

            var targetLighting = SceneLighting.FromTimeOfDay(
                ctx.TimeOfDay,
                playerBiome,
                ctx.Weather.RainIntensity,
                ctx.Weather.CloudIntensity,
                ctx.Weather.LightningIntensity);

            float dt = (float)_drawStopwatch.Elapsed.TotalSeconds;
            _drawStopwatch.Restart();
            dt = Math.Clamp(dt, 0f, 0.1f);

            if (!_isLightingInitialized)
            {
                _currentSkyHorizon = targetLighting.SkyHorizon;
                _currentSkyZenith = targetLighting.SkyZenith;
                _currentAmbientColor = targetLighting.AmbientColor;
                _currentSunColor = targetLighting.SunColor;
                _currentMoonColor = targetLighting.MoonColor;
                _currentFogMultiplier = targetLighting.FogMultiplier;
                _isLightingInitialized = true;
            }
            else
            {
                // Smoothly interpolate towards target biome/time parameters.
                float lerpFactor = 1.5f * dt;
                _currentSkyHorizon = Vector3.Lerp(_currentSkyHorizon, targetLighting.SkyHorizon, lerpFactor);
                _currentSkyZenith = Vector3.Lerp(_currentSkyZenith, targetLighting.SkyZenith, lerpFactor);
                _currentAmbientColor = Vector3.Lerp(_currentAmbientColor, targetLighting.AmbientColor, lerpFactor);
                _currentSunColor = Vector3.Lerp(_currentSunColor, targetLighting.SunColor, lerpFactor);
                _currentMoonColor = Vector3.Lerp(_currentMoonColor, targetLighting.MoonColor, lerpFactor);
                _currentFogMultiplier = MathHelper.Lerp(_currentFogMultiplier, targetLighting.FogMultiplier, lerpFactor);
            }

            // Create custom interpolated SceneLighting instance for rendering
            var lighting = new SceneLighting
            {
                SunDirection = targetLighting.SunDirection,
                MoonDirection = targetLighting.MoonDirection,
                DayLight = targetLighting.DayLight,
                SunsetFactor = targetLighting.SunsetFactor,
                TwilightFactor = targetLighting.TwilightFactor,
                SkyHorizon = _currentSkyHorizon,
                SkyZenith = _currentSkyZenith,
                AmbientColor = _currentAmbientColor,
                SunColor = _currentSunColor,
                MoonColor = _currentMoonColor,
                FogMultiplier = _currentFogMultiplier
            };

            var sunDir = lighting.SunDirection;
            var moonDir = lighting.MoonDirection;

            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, lighting.SkyHorizonColor, 1.0f, 0);

            var monoWorld = Matrix.Identity;
            var monoView = ConvertMatrix(view);
            var monoProj = ConvertMatrix(proj);

            var monoAmbColor = lighting.ToMono(lighting.AmbientColor);
            var monoFogColor = lighting.ToMono(lighting.SkyHorizon);
            var monoLightColor = lighting.ToMono(lighting.SunColor);
            var monoSunDir = lighting.ToMono(lighting.SunDirection);
            var monoMoonDir = lighting.ToMono(lighting.MoonDirection);
            var monoMoonLight = lighting.ToMono(lighting.MoonColor);

            _worldEffect.World = monoWorld;
            _worldEffect.View = monoView;
            _worldEffect.Projection = monoProj;
            _worldEffect.AmbientLightColor = monoAmbColor;
            _worldEffect.DirectionalLight0.Enabled = lighting.SunEnabled;
            _worldEffect.DirectionalLight0.Direction = ConvertVector(-sunDir);
            _worldEffect.DirectionalLight0.DiffuseColor = monoLightColor;
            _worldEffect.DirectionalLight1.Enabled = lighting.MoonEnabled;
            _worldEffect.DirectionalLight1.Direction = ConvertVector(-moonDir);
            _worldEffect.DirectionalLight1.DiffuseColor = monoMoonLight;
            _worldEffect.FogEnabled = true;
            _worldEffect.FogColor = monoFogColor;

            VoxelWorld.GetChunkCoords(
                (int)MathF.Round(ctx.Camera.Position.X),
                (int)MathF.Round(ctx.Camera.Position.Z),
                out int agentChunkX,
                out int agentChunkZ,
                out _,
                out _);

            var viewProjection = monoView * monoProj;
            ExtractFrustumPlanes(viewProjection, _frustumPlanes);
            BuildVisibleChunkList(ctx.Grid, agentChunkX, agentChunkZ, renderDistance, _frustumPlanes);

            var swSky = System.Diagnostics.Stopwatch.StartNew();
            DrawSkyBox(monoView, monoProj, lighting, ctx.TimeOfDay, ctx.Grid.Seed);
            _cloudLayerRenderer.Draw(_device, _skyEffect, monoView, monoProj, ctx.TimeOfDay, lighting);
            DrawSunAndMoon(ctx.Camera, sunDir, moonDir);
            swSky.Stop();
            PerfCounters.DrawSkyMs = (float)swSky.Elapsed.TotalMilliseconds;

            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullClockwise;
            _device.SamplerStates[0] = SamplerState.PointClamp;

            float underwaterFactor = WaterQuery.IsCameraUnderwater(ctx.Grid, ctx.Camera.Position) ? 1f : 0f;
            float twilightFactor = lighting.TwilightFactor;
            if (underwaterFactor > 0f)
            {
                monoAmbColor *= 0.72f;
                monoFogColor = Microsoft.Xna.Framework.Vector3.Lerp(
                    monoFogColor,
                    new Microsoft.Xna.Framework.Vector3(0.08f, 0.22f, 0.34f),
                    0.65f);
            }

            DrawAllTerrainChunks(
                monoWorld,
                monoView,
                monoProj,
                monoAmbColor,
                monoFogColor,
                monoSunDir,
                monoLightColor,
                lighting.SunEnabled,
                monoMoonDir,
                monoMoonLight,
                lighting.MoonEnabled,
                renderDistance,
                twilightFactor,
                lighting.FogMultiplier,
                ctx.WaterAnimTime);

            var floraFogStart = ChunkLod.GetFogStart(renderDistance) * lighting.FogMultiplier;
            var floraFogEnd = ChunkLod.GetFogEnd(renderDistance, twilightFactor) * lighting.FogMultiplier;

            var swFlora = System.Diagnostics.Stopwatch.StartNew();
            _floraRenderer.Draw(
                _visibleChunksScratch,
                monoView,
                monoProj,
                lighting,
                floraFogStart,
                floraFogEnd,
                renderDistance,
                ctx.WaterAnimTime,
                _atlasTexture);
            swFlora.Stop();
            PerfCounters.DrawFloraMs = (float)swFlora.Elapsed.TotalMilliseconds;

            var swEntities = System.Diagnostics.Stopwatch.StartNew();
            DrawAnimals(ctx, monoView, monoProj, renderDistance, lighting);
            DrawVillagers(ctx, monoView, monoProj, renderDistance, lighting);
            DrawItemEntities(ctx, monoView, monoProj, renderDistance, lighting);
            swEntities.Stop();
            PerfCounters.DrawEntitiesMs = (float)swEntities.Elapsed.TotalMilliseconds;

            _overlayRenderer.Draw(
                ctx.BlockInteraction,
                ctx.Particles,
                monoView,
                monoProj,
                ctx.Camera,
                ctx.BlockInteraction.AnimTime,
                ctx.BlueprintPlacement,
                ctx.PendingConstructionSites,
                ctx.WorkZonePlacement);

            Draw3DHeldItem(ctx, monoView, monoProj, lighting);

            if (underwaterFactor > 0f)
            {
                DrawUnderwaterOverlay(sw, sh);
            }
        }

        public void SetAtlasTexture(Texture2D atlas)
        {
            _atlasTexture = atlas;
            _worldEffect.Texture = atlas;
            _blockTerrainEffect.SetAtlas(atlas);
            _floraRenderer.SetAtlas(atlas);
            _overlayRenderer.SetAtlasTexture(atlas);
        }

        public void Dispose()
        {
            _spriteBatch.Dispose();
            _cloudLayerRenderer.Dispose();
            _skyDomeRenderer.Dispose();
            _floraRenderer.Dispose();
            _overlayRenderer.Dispose();
            _worldEffect.Dispose();
        }

        private void DrawUnderwaterOverlay(float sw, float sh)
        {
            _device.DepthStencilState = DepthStencilState.None;
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_whiteTexture, new Rectangle(0, 0, (int)sw, (int)sh), new Color(0.08f, 0.28f, 0.42f, 0.22f));
            _spriteBatch.End();
        }

        private void DrawAnimals(GameRenderContext ctx, Matrix view, Matrix proj, int renderDistance, SceneLighting lighting)
        {
            var cameraPos = ctx.Camera.Position;
            var animals = ctx.Animals.GetAnimalsInRange(cameraPos, ChunkLod.GetAnimalCullRadius(renderDistance));
            if (animals.Count == 0)
            {
                return;
            }

            bool prevTextureEnabled = _worldEffect.TextureEnabled;
            _worldEffect.TextureEnabled = true;
            _worldEffect.Texture = _atlasTexture;
            _worldEffect.View = view;
            _worldEffect.Projection = proj;
            _worldEffect.AmbientLightColor = lighting.ToMono(lighting.AmbientColor);
            _worldEffect.DirectionalLight0.Enabled = lighting.SunEnabled;
            _worldEffect.DirectionalLight0.Direction = ConvertVector(-lighting.SunDirection);
            _worldEffect.DirectionalLight0.DiffuseColor = lighting.ToMono(lighting.SunColor);
            _worldEffect.DirectionalLight1.Enabled = lighting.MoonEnabled;
            _worldEffect.DirectionalLight1.Direction = ConvertVector(-lighting.MoonDirection);
            _worldEffect.DirectionalLight1.DiffuseColor = lighting.ToMono(lighting.MoonColor);
            _worldEffect.FogEnabled = true;
            _worldEffect.FogColor = lighting.ToMono(lighting.SkyHorizon);
            _worldEffect.FogStart = ChunkLod.GetFogStart(renderDistance) * lighting.FogMultiplier;
            _worldEffect.FogEnd = ChunkLod.GetFogEnd(renderDistance, lighting.TwilightFactor) * lighting.FogMultiplier;

            foreach (var animal in animals)
            {
                var stats = animal.Stats;
                float deathScale = animal.DeathScale;
                if (deathScale <= 0.01f)
                {
                    continue;
                }

                float yawRad = MathHelper.ToRadians(-animal.Yaw);
                var rotation = Matrix.CreateRotationY(yawRad);
                var scaleMatrix = Matrix.CreateScale(deathScale);
                var translation = Matrix.CreateTranslation(animal.Position.X, animal.Position.Y, animal.Position.Z);
                var animalWorld = scaleMatrix * rotation * translation;

                float flash = animal.HitFlashTimer > 0f ? animal.HitFlashTimer / 0.15f : 0f;

                int bodyCol = 4, bodyRow = 2;
                int headCol = 5, headRow = 2;

                if (BlockAtlas.LayoutData.Animals.TryGetValue(animal.Type.ToString(), out var animalTiles))
                {
                    var body = BlockAtlas.LayoutData.GetTile(animalTiles.Body);
                    var head = BlockAtlas.LayoutData.GetTile(animalTiles.Head);
                    bodyCol = body.Col;
                    bodyRow = body.Row;
                    headCol = head.Col;
                    headRow = head.Row;
                }

                var bodyUV = BlockAtlas.GetTileUVs(bodyCol, bodyRow);
                var headUV = BlockAtlas.GetTileUVs(headCol, headRow);

                float bodyHeight = stats.Height * 0.55f;
                float bodyCenterY = stats.Height * 0.35f;

                DrawTexturedBox(animalWorld, stats.Width * 0.45f, bodyHeight * 0.5f, stats.Width * 0.45f, 0f, bodyCenterY, 0f, bodyUV, bodyUV);

                float headSize = stats.Width * 0.22f;
                float headCenterY = stats.Height * 0.72f;
                float headForward = stats.Width * 0.35f;

                DrawTexturedBox(animalWorld, headSize, headSize, headSize, 0f, headCenterY, headForward, bodyUV, headUV);

                if (stats.HasAccent)
                {
                    _worldEffect.TextureEnabled = false;
                    float accentSize = headSize * 0.45f;
                    DrawColoredBox(animalWorld, accentSize, accentSize * 0.6f, accentSize * 0.8f, 0f, headCenterY, headForward + headSize * 1.2f, stats.AccentColor);
                    _worldEffect.TextureEnabled = true;
                }

                if (flash > 0f)
                {
                    _worldEffect.TextureEnabled = false;
                    var flashColor = new Color(1f, 1f, 1f, 0.45f * flash);
                    DrawColoredBox(animalWorld, stats.Width * 0.5f, stats.Height * 0.45f, stats.Width * 0.5f, 0f, stats.Height * 0.4f, 0f, flashColor);
                    _worldEffect.TextureEnabled = true;
                }
            }

            _worldEffect.TextureEnabled = prevTextureEnabled;
        }

        private void DrawVillagers(GameRenderContext ctx, Matrix view, Matrix proj, int renderDistance, SceneLighting lighting)
        {
            var cameraPos = ctx.Camera.Position;
            var villagers = ctx.Villagers.GetVillagersInRange(cameraPos, ChunkLod.GetAnimalCullRadius(renderDistance));
            if (villagers.Count == 0)
            {
                return;
            }

            bool prevTextureEnabled = _worldEffect.TextureEnabled;
            _worldEffect.TextureEnabled = true;
            _worldEffect.Texture = _atlasTexture;
            _worldEffect.View = view;
            _worldEffect.Projection = proj;
            _worldEffect.AmbientLightColor = lighting.ToMono(lighting.AmbientColor);
            _worldEffect.DirectionalLight0.Enabled = lighting.SunEnabled;
            _worldEffect.DirectionalLight0.Direction = ConvertVector(-lighting.SunDirection);
            _worldEffect.DirectionalLight0.DiffuseColor = lighting.ToMono(lighting.SunColor);
            _worldEffect.FogEnabled = true;
            _worldEffect.FogColor = lighting.ToMono(lighting.SkyHorizon);
            _worldEffect.FogStart = ChunkLod.GetFogStart(renderDistance) * lighting.FogMultiplier;
            _worldEffect.FogEnd = ChunkLod.GetFogEnd(renderDistance, lighting.TwilightFactor) * lighting.FogMultiplier;

            int bodyCol = 6;
            int bodyRow = 6;
            int headCol = 7;
            int headRow = 6;
            if (BlockAtlas.LayoutData.Villagers.TryGetValue("Default", out var villagerTiles))
            {
                var body = BlockAtlas.LayoutData.GetTile(villagerTiles.Body);
                var head = BlockAtlas.LayoutData.GetTile(villagerTiles.Head);
                bodyCol = body.Col;
                bodyRow = body.Row;
                headCol = head.Col;
                headRow = head.Row;
            }

            var bodyUV = BlockAtlas.GetTileUVs(bodyCol, bodyRow);
            var headUV = BlockAtlas.GetTileUVs(headCol, headRow);

            foreach (var villager in villagers)
            {
                float yawRad = MathHelper.ToRadians(-villager.Yaw);
                var rotation = Matrix.CreateRotationY(yawRad);
                var translation = Matrix.CreateTranslation(villager.Position.X, villager.Position.Y, villager.Position.Z);
                var villagerWorld = rotation * translation;

                float bodyHeight = Villager.Height * 0.55f;
                float bodyCenterY = Villager.Height * 0.35f;
                DrawTexturedBox(villagerWorld, Villager.Width * 0.45f, bodyHeight * 0.5f, Villager.Width * 0.45f, 0f, bodyCenterY, 0f, bodyUV, bodyUV);

                float headSize = Villager.Width * 0.22f;
                float headCenterY = Villager.Height * 0.72f;
                float headForward = Villager.Width * 0.35f;
                DrawTexturedBox(villagerWorld, headSize, headSize, headSize, 0f, headCenterY, headForward, bodyUV, headUV);

                var roleColor = VillagerVisuals.GetRoleColor(villager.Role);
                _worldEffect.TextureEnabled = false;
                float vestHeight = bodyHeight * 0.35f;
                DrawColoredBox(villagerWorld, Villager.Width * 0.38f, vestHeight * 0.5f, Villager.Width * 0.12f, 0f, bodyCenterY, Villager.Width * 0.38f, roleColor * 0.85f);

                if (villager.Role == VillagerRole.Lumberjack)
                {
                    DrawColoredBox(villagerWorld, 0.06f, 0.06f, 0.14f, Villager.Width * 0.35f, bodyCenterY + 0.05f, Villager.Width * 0.42f, new Color(0.55f, 0.35f, 0.18f, 0.95f));
                }
                else if (villager.Role == VillagerRole.Builder)
                {
                    DrawColoredBox(villagerWorld, Villager.Width * 0.42f, 0.04f, Villager.Width * 0.42f, 0f, headCenterY + headSize * 0.6f, 0f, new Color(0.95f, 0.55f, 0.15f, 0.95f));
                }

                if (VillagerVisuals.ShouldDrawJobIndicator(villager.CurrentJob))
                {
                    var jobColor = VillagerVisuals.GetJobIndicatorColor(villager.CurrentJob);
                    float indicatorSize = villager.CurrentJob == JobType.Sleep ? 0.1f : 0.12f;
                    DrawColoredBox(villagerWorld, indicatorSize, indicatorSize, indicatorSize, 0f, Villager.Height + 0.18f, 0f, jobColor);
                }

                _worldEffect.TextureEnabled = true;
            }

            _worldEffect.TextureEnabled = prevTextureEnabled;
        }

        private void DrawItemEntities(GameRenderContext ctx, Matrix view, Matrix proj, int renderDistance, SceneLighting lighting)
        {
            if (ctx.ItemEntities == null || ctx.ItemEntities.Count == 0)
            {
                return;
            }

            bool prevTextureEnabled = _worldEffect.TextureEnabled;
            _worldEffect.TextureEnabled = true;
            _worldEffect.Texture = _atlasTexture;
            _worldEffect.View = view;
            _worldEffect.Projection = proj;
            _worldEffect.AmbientLightColor = lighting.ToMono(lighting.AmbientColor);
            _worldEffect.DirectionalLight0.Enabled = lighting.SunEnabled;
            _worldEffect.DirectionalLight0.Direction = ConvertVector(-lighting.SunDirection);
            _worldEffect.DirectionalLight0.DiffuseColor = lighting.ToMono(lighting.SunColor);
            _worldEffect.DirectionalLight1.Enabled = lighting.MoonEnabled;
            _worldEffect.DirectionalLight1.Direction = ConvertVector(-lighting.MoonDirection);
            _worldEffect.DirectionalLight1.DiffuseColor = lighting.ToMono(lighting.MoonColor);
            _worldEffect.FogEnabled = true;
            _worldEffect.FogColor = lighting.ToMono(lighting.SkyHorizon);
            _worldEffect.FogStart = ChunkLod.GetFogStart(renderDistance) * lighting.FogMultiplier;
            _worldEffect.FogEnd = ChunkLod.GetFogEnd(renderDistance, lighting.TwilightFactor) * lighting.FogMultiplier;

            var cameraPos = ctx.Camera.Position;
            float cullRadius = ChunkLod.GetAnimalCullRadius(renderDistance);
            float cullRadiusSq = cullRadius * cullRadius;

            foreach (var itemEntity in ctx.ItemEntities)
            {
                if (itemEntity.ReadyForRemoval)
                {
                    continue;
                }

                var itemPos = itemEntity.Position;
                if (Vector3.DistanceSquared(cameraPos, itemPos) > cullRadiusSq)
                {
                    continue;
                }

                if (!IsPointVisible(itemPos, _frustumPlanes))
                {
                    continue;
                }

                float bobOffset = MathF.Sin(itemEntity.HoverTimer * 3.0f) * 0.08f;
                float rotAngle = itemEntity.Age * 1.5f;

                var rotation = Matrix.CreateRotationY(rotAngle);
                var translation = Matrix.CreateTranslation(
                    itemEntity.Position.X,
                    itemEntity.Position.Y + bobOffset + 0.12f,
                    itemEntity.Position.Z
                );
                var scaleMatrix = Matrix.CreateScale(1.0f);
                var itemWorld = scaleMatrix * rotation * translation;

                var stack = itemEntity.Item;
                if (stack.IsBlock())
                {
                    var type = stack.BlockType;
                    var uvTop = BlockAtlas.GetFaceUVs(type, new System.Numerics.Vector3(0f, 1f, 0f));
                    var uvSide = BlockAtlas.GetFaceUVs(type, new System.Numerics.Vector3(0f, 0f, 1f));
                    DrawTexturedBox(itemWorld, 0.12f, 0.12f, 0.12f, 0f, 0f, 0f, uvSide, uvTop);
                }
                else if (stack.IsTool())
                {
                    var uv = BlockAtlas.GetToolUVs(stack.ToolId);
                    DrawTexturedBox(itemWorld, 0.14f, 0.14f, 0.015f, 0f, 0f, 0f, uv, uv);
                }
                else if (stack.IsFluidContainer())
                {
                    _worldEffect.TextureEnabled = false;
                    Color color = stack.IsWaterBucket()
                        ? new Color(0.28f, 0.48f, 0.92f)
                        : new Color(0.62f, 0.58f, 0.52f);
                    DrawColoredBox(itemWorld, 0.08f, 0.08f, 0.08f, 0f, 0f, 0f, color);
                    _worldEffect.TextureEnabled = true;
                }
                else if (stack.IsFood())
                {
                    _worldEffect.TextureEnabled = false;
                    Color color = stack.FoodId switch
                    {
                        ItemId.RawMeat => new Color(0.78f, 0.32f, 0.28f),
                        ItemId.CookedMeat => new Color(0.62f, 0.38f, 0.24f),
                        ItemId.Bread => new Color(0.78f, 0.62f, 0.28f),
                        _ => Color.Pink
                    };
                    DrawColoredBox(itemWorld, 0.08f, 0.08f, 0.08f, 0f, 0f, 0f, color);
                    _worldEffect.TextureEnabled = true;
                }
                else if (stack.IsMaterial() && stack.MaterialId == ItemId.Stick)
                {
                    _worldEffect.TextureEnabled = false;
                    DrawColoredBox(itemWorld, 0.02f, 0.12f, 0.02f, 0f, 0f, 0f, new Color(0.62f, 0.45f, 0.24f));
                    _worldEffect.TextureEnabled = true;
                }
                else
                {
                    _worldEffect.TextureEnabled = false;
                    DrawColoredBox(itemWorld, 0.08f, 0.08f, 0.08f, 0f, 0f, 0f, Color.Gold);
                    _worldEffect.TextureEnabled = true;
                }
            }

            _worldEffect.TextureEnabled = prevTextureEnabled;
        }

        private void Draw3DHeldItem(GameRenderContext ctx, Matrix view, Matrix proj, SceneLighting lighting)
        {
            var player = ctx.Player;
            var animator = ctx.InteractionAnimator;
            var stack = player.GetSelectedStack();
            if (stack.IsEmpty)
            {
                return;
            }

            // Clear the depth buffer so the held item is always drawn on top of the world
            _device.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Transparent, 1.0f, 0);

            // Calculate swing rotations and offsets
            float swingDeg = animator.GetHeldItemSwingDegrees();
            float offsetY = animator.GetHeldItemOffsetY();

            var camera = ctx.Camera;
            Vector3 front = camera.Front;
            Vector3 right = camera.Right;
            Vector3 up = camera.Up;

            // Base position offset from camera eye in local coordinates
            float baseForward = 0.35f;
            float baseRight = 0.18f;
            float baseUp = -0.16f + offsetY * 0.05f;

            // Apply swing translation offset
            float swingRad = MathHelper.ToRadians(swingDeg);
            baseForward += MathF.Sin(swingRad) * 0.08f;
            baseRight -= MathF.Sin(swingRad) * 0.05f;

            Vector3 itemPos = camera.Position + front * baseForward + right * baseRight + up * baseUp;

            float yawRad = MathHelper.ToRadians(-camera.Yaw);
            float pitchRad = MathHelper.ToRadians(camera.Pitch);

            // Build camera rotation matrix
            Matrix camRot = Matrix.CreateRotationY(yawRad) * Matrix.CreateRotationX(pitchRad);

            // Item-specific rotation (angle held)
            Matrix itemRot;
            if (stack.IsBlock())
            {
                itemRot = Matrix.CreateRotationY(MathHelper.ToRadians(45f)) * Matrix.CreateRotationX(MathHelper.ToRadians(-15f));
            }
            else
            {
                itemRot = Matrix.CreateRotationY(MathHelper.ToRadians(45f)) * Matrix.CreateRotationX(MathHelper.ToRadians(-25f)) * Matrix.CreateRotationZ(MathHelper.ToRadians(-10f));
            }

            // Swing rotation around local X/Z axis
            Matrix swingRot = Matrix.CreateRotationX(MathHelper.ToRadians(-swingDeg * 1.2f)) * Matrix.CreateRotationZ(MathHelper.ToRadians(swingDeg * 0.8f));

            Matrix finalWorld = itemRot * swingRot * camRot * Matrix.CreateTranslation(itemPos.X, itemPos.Y, itemPos.Z);

            bool prevTextureEnabled = _worldEffect.TextureEnabled;
            _worldEffect.TextureEnabled = true;
            _worldEffect.Texture = _atlasTexture;
            _worldEffect.View = view;
            _worldEffect.Projection = proj;

            _worldEffect.AmbientLightColor = lighting.ToMono(lighting.AmbientColor * 1.25f); // slightly brighter for readability
            _worldEffect.DirectionalLight0.Enabled = lighting.SunEnabled;
            _worldEffect.DirectionalLight0.Direction = ConvertVector(-lighting.SunDirection);
            _worldEffect.DirectionalLight0.DiffuseColor = lighting.ToMono(lighting.SunColor);
            _worldEffect.FogEnabled = false;

            if (stack.IsBlock())
            {
                var type = stack.BlockType;
                var uvTop = BlockAtlas.GetFaceUVs(type, new System.Numerics.Vector3(0f, 1f, 0f));
                var uvSide = BlockAtlas.GetFaceUVs(type, new System.Numerics.Vector3(0f, 0f, 1f));
                DrawTexturedBox(finalWorld, 0.05f, 0.05f, 0.05f, 0f, 0f, 0f, uvSide, uvTop);
            }
            else if (stack.IsTool())
            {
                var uv = BlockAtlas.GetToolUVs(stack.ToolId);
                DrawTexturedBox(finalWorld, 0.08f, 0.08f, 0.005f, 0f, 0f, 0f, uv, uv);
            }

            _worldEffect.TextureEnabled = prevTextureEnabled;
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

            var p0 = new Vector3(x0, y0, z1);
            var p1 = new Vector3(x1, y0, z1);
            var p2 = new Vector3(x1, y1, z1);
            var p3 = new Vector3(x0, y1, z1);
            var p4 = new Vector3(x0, y0, z0);
            var p5 = new Vector3(x1, y0, z0);
            var p6 = new Vector3(x1, y1, z0);
            var p7 = new Vector3(x0, y1, z0);

            var colVec = Vector3.One;

            var nFront = new Vector3(0, 0, 1);
            TexturedBoxVertices[0] = new Vertex(p0, colVec, nFront, new System.Numerics.Vector2(frontUV.uMin, frontUV.vMax));
            TexturedBoxVertices[1] = new Vertex(p1, colVec, nFront, new System.Numerics.Vector2(frontUV.uMax, frontUV.vMax));
            TexturedBoxVertices[2] = new Vertex(p2, colVec, nFront, new System.Numerics.Vector2(frontUV.uMax, frontUV.vMin));
            TexturedBoxVertices[3] = new Vertex(p3, colVec, nFront, new System.Numerics.Vector2(frontUV.uMin, frontUV.vMin));

            var nRight = new Vector3(1, 0, 0);
            TexturedBoxVertices[4] = new Vertex(p1, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            TexturedBoxVertices[5] = new Vertex(p5, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            TexturedBoxVertices[6] = new Vertex(p6, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            TexturedBoxVertices[7] = new Vertex(p2, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            var nBack = new Vector3(0, 0, -1);
            TexturedBoxVertices[8] = new Vertex(p5, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            TexturedBoxVertices[9] = new Vertex(p4, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            TexturedBoxVertices[10] = new Vertex(p7, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            TexturedBoxVertices[11] = new Vertex(p6, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            var nLeft = new Vector3(-1, 0, 0);
            TexturedBoxVertices[12] = new Vertex(p4, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            TexturedBoxVertices[13] = new Vertex(p0, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            TexturedBoxVertices[14] = new Vertex(p3, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            TexturedBoxVertices[15] = new Vertex(p7, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            var nTop = new Vector3(0, 1, 0);
            TexturedBoxVertices[16] = new Vertex(p3, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            TexturedBoxVertices[17] = new Vertex(p2, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            TexturedBoxVertices[18] = new Vertex(p6, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            TexturedBoxVertices[19] = new Vertex(p7, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            var nBottom = new Vector3(0, -1, 0);
            TexturedBoxVertices[20] = new Vertex(p4, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            TexturedBoxVertices[21] = new Vertex(p5, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            TexturedBoxVertices[22] = new Vertex(p1, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            TexturedBoxVertices[23] = new Vertex(p0, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            foreach (var pass in _worldEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, TexturedBoxVertices, 0, 24, TexturedBoxIndices, 0, 12);
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

            ColoredBoxVertices[0] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y0, z1), color);
            ColoredBoxVertices[1] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y0, z1), color);
            ColoredBoxVertices[2] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y1, z1), color);
            ColoredBoxVertices[3] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y1, z1), color);
            ColoredBoxVertices[4] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y0, z0), color);
            ColoredBoxVertices[5] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y0, z0), color);
            ColoredBoxVertices[6] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y1, z0), color);
            ColoredBoxVertices[7] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y1, z0), color);

            foreach (var pass in _worldEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, ColoredBoxVertices, 0, 8, ColoredBoxIndices, 0, 12);
            }
        }

        private static short[] BuildBoxIndices()
        {
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

            return indices;
        }

        private void BuildVisibleChunkList(
            VoxelWorld grid,
            int agentChunkX,
            int agentChunkZ,
            int renderDistance,
            Microsoft.Xna.Framework.Vector4[] frustumPlanes)
        {
            _visibleChunksScratch.Clear();
            foreach (var chunk in grid.ActiveChunks)
            {
                if (!IsChunkVisible(chunk, frustumPlanes))
                {
                    continue;
                }

                int chunkDistance = ChunkLod.GetChunkDistance(chunk.ChunkX, chunk.ChunkZ, agentChunkX, agentChunkZ);
                var desiredDetail = ChunkLod.SelectRenderTarget(chunk, chunkDistance, renderDistance, restrictLod: false);
                if (!ChunkLod.TryGetRenderableDetail(chunk, desiredDetail, out var renderDetail))
                {
                    continue;
                }

                _visibleChunksScratch.Add(new VisibleChunkDrawInfo(chunk, renderDetail, chunkDistance));
            }
        }

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

        private void DrawAllTerrainChunks(
            Matrix monoWorld,
            Matrix monoView,
            Matrix monoProj,
            Microsoft.Xna.Framework.Vector3 monoAmbColor,
            Microsoft.Xna.Framework.Vector3 monoFogColor,
            Microsoft.Xna.Framework.Vector3 monoSunDir,
            Microsoft.Xna.Framework.Vector3 monoLightColor,
            bool sunEnabled,
            Microsoft.Xna.Framework.Vector3 monoMoonDir,
            Microsoft.Xna.Framework.Vector3 monoMoonLight,
            bool moonEnabled,
            int renderDistance,
            float twilightFactor,
            float fogMultiplier,
            float waterAnimTime)
        {
            _blockTerrainEffect.ApplyTerrainPassBase(
                monoWorld,
                monoView,
                monoProj,
                monoAmbColor,
                monoFogColor,
                monoSunDir,
                monoLightColor,
                sunEnabled,
                monoMoonDir,
                monoMoonLight,
                moonEnabled,
                _atlasTexture);

            var swOpaque = System.Diagnostics.Stopwatch.StartNew();
            DrawTerrainPass(
                waterAnimTime,
                renderDistance,
                twilightFactor,
                fogMultiplier,
                BlendState.Opaque,
                DepthStencilState.Default,
                waterOnly: false,
                alphaCutoutOnly: false,
                TerrainPassKind.Opaque);
            swOpaque.Stop();
            PerfCounters.DrawTerrainOpaqueMs = (float)swOpaque.Elapsed.TotalMilliseconds;

            var swWater = System.Diagnostics.Stopwatch.StartNew();
            DrawTerrainPass(
                waterAnimTime,
                renderDistance,
                twilightFactor,
                fogMultiplier,
                BlendState.AlphaBlend,
                DepthStencilState.DepthRead,
                waterOnly: true,
                alphaCutoutOnly: false,
                TerrainPassKind.Water);
            swWater.Stop();
            PerfCounters.DrawTerrainWaterMs = (float)swWater.Elapsed.TotalMilliseconds;

            var swCutout = System.Diagnostics.Stopwatch.StartNew();
            DrawTerrainPass(
                waterAnimTime,
                renderDistance,
                twilightFactor,
                fogMultiplier,
                BlendState.AlphaBlend,
                DepthStencilState.DepthRead,
                waterOnly: false,
                alphaCutoutOnly: true,
                TerrainPassKind.Cutout);
            swCutout.Stop();
            PerfCounters.DrawTerrainCutoutMs = (float)swCutout.Elapsed.TotalMilliseconds;
        }

        private enum TerrainPassKind
        {
            Opaque,
            Water,
            Cutout
        }

        private void DrawTerrainPass(
            float waterAnimTime,
            int renderDistance,
            float twilightFactor,
            float fogMultiplier,
            BlendState blendState,
            DepthStencilState depthState,
            bool waterOnly,
            bool alphaCutoutOnly,
            TerrainPassKind passKind)
        {
            _device.BlendState = blendState;
            _device.DepthStencilState = depthState;

            var bandDetails = new[]
            {
                ChunkMeshDetail.Full,
                ChunkMeshDetail.Surface,
                ChunkMeshDetail.Shell
            };

            foreach (var bandDetail in bandDetails)
            {
                if (!BandHasDrawableChunks(bandDetail, waterOnly, alphaCutoutOnly))
                {
                    continue;
                }

                var (fogStart, detailFogEnd) = ChunkLod.GetFogRange(renderDistance, bandDetail, twilightFactor);
                _blockTerrainEffect.SetFogRange(fogStart * fogMultiplier, detailFogEnd * fogMultiplier);

                foreach (var entry in _visibleChunksScratch)
                {
                    if (waterOnly && !entry.Chunk.HasWaterBlocks)
                    {
                        continue;
                    }

                    if (alphaCutoutOnly && !entry.Chunk.HasAlphaCutoutBlocks)
                    {
                        continue;
                    }

                    if (entry.RenderDetail != bandDetail)
                    {
                        continue;
                    }

                    var (vb, ib, count) = waterOnly
                        ? entry.Chunk.GetWaterMesh(entry.RenderDetail)
                        : entry.Chunk.GetMesh(entry.RenderDetail);
                    if (vb == null || ib == null || count <= 0)
                    {
                        continue;
                    }

                    if (waterOnly && entry.ChunkDistance <= 3)
                    {
                        var waterVertices = entry.Chunk.GetWaterVertices(entry.RenderDetail);
                        if (waterVertices != null && waterVertices.Length > 0)
                        {
                            EnsureWaterScratch(waterVertices.Length);
                            for (int i = 0; i < waterVertices.Length; i++)
                            {
                                var v = waterVertices[i];
                                if (v.Normal.Y > 0.9f)
                                {
                                    float wave = MathF.Sin(waterAnimTime * 2.2f + (v.Position.X * 0.45f) + (v.Position.Z * 0.45f)) * 0.08f;
                                    v.Position.Y += wave;
                                }
                                _waterScratch[i] = v;
                            }
                            vb.SetData(_waterScratch, 0, waterVertices.Length);
                        }
                    }

                    _device.SetVertexBuffer(vb);
                    _device.Indices = ib;
                    foreach (var pass in _blockTerrainEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, count / 3);
                        Autonocraft.Core.PerfCounters.TerrainDrawCalls++;
                        switch (passKind)
                        {
                            case TerrainPassKind.Water:
                                Autonocraft.Core.PerfCounters.TerrainWaterDrawCalls++;
                                Autonocraft.Core.PerfCounters.TerrainWaterIndexCount += count;
                                break;
                            case TerrainPassKind.Cutout:
                                Autonocraft.Core.PerfCounters.TerrainCutoutDrawCalls++;
                                Autonocraft.Core.PerfCounters.TerrainCutoutIndexCount += count;
                                break;
                            default:
                                Autonocraft.Core.PerfCounters.TerrainOpaqueDrawCalls++;
                                Autonocraft.Core.PerfCounters.TerrainOpaqueIndexCount += count;
                                break;
                        }
                    }
                }
            }
        }

        private void EnsureWaterScratch(int size)
        {
            if (_waterScratch.Length < size)
            {
                int capacity = Math.Max(size, _waterScratch.Length < 1 ? 2048 : _waterScratch.Length * 2);
                _waterScratch = new Vertex[capacity];
            }
        }

        private bool BandHasDrawableChunks(ChunkMeshDetail bandDetail, bool waterOnly, bool alphaCutoutOnly)
        {
            foreach (var entry in _visibleChunksScratch)
            {
                if (waterOnly && !entry.Chunk.HasWaterBlocks)
                {
                    continue;
                }

                if (alphaCutoutOnly && !entry.Chunk.HasAlphaCutoutBlocks)
                {
                    continue;
                }

                if (entry.RenderDetail != bandDetail)
                {
                    continue;
                }

                var (_, _, count) = waterOnly
                    ? entry.Chunk.GetWaterMesh(entry.RenderDetail)
                    : entry.Chunk.GetMesh(entry.RenderDetail);
                if (count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ExtractFrustumPlanes(Matrix viewProjection, Microsoft.Xna.Framework.Vector4[] planes)
        {
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
        }

        private static bool IsPointVisible(Vector3 point, Microsoft.Xna.Framework.Vector4[] planes)
        {
            for (int i = 0; i < planes.Length; i++)
            {
                if (planes[i].X * point.X + planes[i].Y * point.Y + planes[i].Z * point.Z + planes[i].W < 0f)
                {
                    return false;
                }
            }

            return true;
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

        private static Matrix ConvertMatrix(Matrix4x4 m)
        {
            return new Matrix(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44
            );
        }

        private static Microsoft.Xna.Framework.Vector3 ConvertVector(Vector3 v)
        {
            return new Microsoft.Xna.Framework.Vector3(v.X, v.Y, v.Z);
        }
    }
}
