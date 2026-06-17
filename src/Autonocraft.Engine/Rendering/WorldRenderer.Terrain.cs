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
                WaterDepthState,
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
                DepthStencilState.Default,
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
            _device.SamplerStates[0] = waterOnly ? SamplerState.LinearClamp : SamplerState.PointClamp;

            if (waterOnly)
            {
                float wave = MathF.Sin(waterAnimTime * 2.0f);
                _blockTerrainEffect.SetAlpha(WaterSurfaceAlpha + wave * 0.03f);
                var (waterFogStart, waterFogEnd) = ChunkLod.GetFogRange(renderDistance, ChunkMeshDetail.Full, twilightFactor);
                _blockTerrainEffect.SetFogRange(waterFogStart * fogMultiplier, waterFogEnd * fogMultiplier);
            }
            else
            {
                _blockTerrainEffect.SetAlpha(1f);
            }

            ChunkMeshDetail fogDetail = ChunkMeshDetail.Full;
            bool fogInitialized = waterOnly;
            foreach (var entry in _visibleChunksScratch)
            {
                if (waterOnly)
                {
                    if (!entry.Chunk.HasWaterBlocks)
                    {
                        continue;
                    }
                }
                else if (alphaCutoutOnly)
                {
                    if (!entry.Chunk.HasAlphaCutoutBlocks)
                    {
                        continue;
                    }
                }

                if (!waterOnly && (!fogInitialized || entry.RenderDetail != fogDetail))
                {
                    fogDetail = entry.RenderDetail;
                    var (fogStart, detailFogEnd) = ChunkLod.GetFogRange(renderDistance, fogDetail, twilightFactor);
                    _blockTerrainEffect.SetFogRange(fogStart * fogMultiplier, detailFogEnd * fogMultiplier);
                    fogInitialized = true;
                }

                var (vb, ib, count) = waterOnly
                    ? entry.Chunk.GetWaterMesh(entry.RenderDetail)
                    : entry.Chunk.GetMesh(entry.RenderDetail);
                if (vb == null || ib == null || count <= 0)
                {
                    continue;
                }

                _device.SetVertexBuffer(vb);
                _device.Indices = ib;
                foreach (var pass in _blockTerrainEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, count / 3);
                    PerfCounters.TerrainDrawCalls++;
                    switch (passKind)
                    {
                        case TerrainPassKind.Water:
                            PerfCounters.TerrainWaterDrawCalls++;
                            PerfCounters.TerrainWaterIndexCount += count;
                            break;
                        case TerrainPassKind.Cutout:
                            PerfCounters.TerrainCutoutDrawCalls++;
                            PerfCounters.TerrainCutoutIndexCount += count;
                            break;
                        default:
                            PerfCounters.TerrainOpaqueDrawCalls++;
                            PerfCounters.TerrainOpaqueIndexCount += count;
                            break;
                    }
                }
            }
        }
    }
}
