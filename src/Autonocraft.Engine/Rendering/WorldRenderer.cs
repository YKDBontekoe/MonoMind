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

    public sealed partial class WorldRenderer : IDisposable
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
        private float _currentWindIntensity = 0.0f;
        private bool _isLightingInitialized = false;
        private static readonly DepthStencilState WaterDepthState = new()
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = true
        };

        private const float WaterSurfaceAlpha = 0.8f;

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
                ctx.Weather.LightningIntensity,
                ctx.Weather.WindIntensity);

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
                _currentWindIntensity = targetLighting.WindIntensity;
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
                _currentWindIntensity = MathHelper.Lerp(_currentWindIntensity, targetLighting.WindIntensity, lerpFactor);
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
                FogMultiplier = _currentFogMultiplier,
                CloudIntensity = targetLighting.CloudIntensity,
                RainIntensity = targetLighting.RainIntensity,
                LightningIntensity = targetLighting.LightningIntensity,
                WindIntensity = _currentWindIntensity
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
            BuildVisibleChunkList(ctx.Grid, agentChunkX, agentChunkZ, renderDistance, _frustumPlanes, ctx.RestrictLod);

            var swSky = System.Diagnostics.Stopwatch.StartNew();
            DrawSkyBox(monoView, monoProj, lighting, ctx.TimeOfDay, ctx.Grid.Seed);
            DrawSunAndMoon(ctx.Camera, sunDir, moonDir);
            swSky.Stop();
            PerfCounters.DrawSkyMs = (float)swSky.Elapsed.TotalMilliseconds;

            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullClockwise;
            _device.SamplerStates[0] = SamplerState.PointClamp;

            float underwaterFactor = WaterQuery.IsCameraUnderwater(ctx.Grid, ctx.Camera.Position) ? 1f : 0f;
            float underlavaFactor = LavaQuery.IsCameraUnderLava(ctx.Grid, ctx.Camera.Position) ? 1f : 0f;
            float twilightFactor = lighting.TwilightFactor;
            if (underwaterFactor > 0f)
            {
                monoAmbColor *= 0.72f;
                monoFogColor = Microsoft.Xna.Framework.Vector3.Lerp(
                    monoFogColor,
                    new Microsoft.Xna.Framework.Vector3(0.08f, 0.22f, 0.34f),
                    0.65f);
            }
            else if (underlavaFactor > 0f)
            {
                monoAmbColor *= 0.5f;
                monoFogColor = Microsoft.Xna.Framework.Vector3.Lerp(
                    monoFogColor,
                    new Microsoft.Xna.Framework.Vector3(0.85f, 0.25f, 0.05f),
                    0.92f);
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
            else if (underlavaFactor > 0f)
            {
                DrawUnderlavaOverlay(sw, sh);
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

        private void DrawUnderlavaOverlay(float sw, float sh)
        {
            _device.DepthStencilState = DepthStencilState.None;
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_whiteTexture, new Rectangle(0, 0, (int)sw, (int)sh), new Color(0.85f, 0.22f, 0.05f, 0.45f));
            _spriteBatch.End();
        }



        private void BuildVisibleChunkList(
            VoxelWorld grid,
            int agentChunkX,
            int agentChunkZ,
            int renderDistance,
            Microsoft.Xna.Framework.Vector4[] frustumPlanes,
            bool restrictLod)
        {
            _visibleChunksScratch.Clear();
            foreach (var chunk in grid.ActiveChunks)
            {
                int chunkDistance = ChunkLod.GetChunkDistance(chunk.ChunkX, chunk.ChunkZ, agentChunkX, agentChunkZ);
                if (chunkDistance > renderDistance)
                {
                    continue;
                }

                if (!IsChunkVisible(chunk, frustumPlanes))
                {
                    continue;
                }

                var desiredDetail = ChunkLod.SelectRenderTarget(chunk, chunkDistance, renderDistance, restrictLod);
                if (!ChunkLod.TryGetRenderableDetail(chunk, desiredDetail, out var renderDetail))
                {
                    continue;
                }

                _visibleChunksScratch.Add(new VisibleChunkDrawInfo(chunk, renderDetail, chunkDistance));
            }
        }



    }
}
