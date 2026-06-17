using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.World;
using NumericVector3 = System.Numerics.Vector3;

namespace Autonocraft.Engine
{
    /// <summary>
    /// Generates and caches small procedural terrain previews from world seeds for menu UI.
    /// </summary>
    public sealed class WorldThumbnailRenderer : IDisposable
    {
        public const int ThumbnailSize = 64;

        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, Texture2D> _cache = new();

        public WorldThumbnailRenderer(GraphicsDevice device)
        {
            _device = device;
        }

        public Texture2D GetThumbnail(int seed)
        {
            if (_cache.TryGetValue(seed, out Texture2D? cached))
            {
                return cached;
            }

            var texture = Generate(seed);
            _cache[seed] = texture;
            return texture;
        }

        public string GetBiomeSummary(int seed)
        {
            var counts = new Dictionary<BiomeType, int>();
            var (originX, originZ, span) = GetSampleRegion(seed);
            var biomeMap = new BiomeMap(seed, WorldGenParams.ForType(WorldType.Default));

            for (int sy = 0; sy < 3; sy++)
            {
                for (int sx = 0; sx < 3; sx++)
                {
                    int wx = originX + sx * span / 2;
                    int wz = originZ + sy * span / 2;
                    var biome = biomeMap.Sample(wx, wz).Primary;
                    counts.TryGetValue(biome, out int count);
                    counts[biome] = count + 1;
                }
            }

            BiomeType dominant = BiomeType.Plains;
            int best = 0;
            foreach (var pair in counts)
            {
                if (pair.Value > best)
                {
                    best = pair.Value;
                    dominant = pair.Key;
                }
            }

            return FormatBiome(dominant);
        }

        public void ClearCache()
        {
            foreach (var texture in _cache.Values)
            {
                texture.Dispose();
            }

            _cache.Clear();
        }

        public void Dispose()
        {
            ClearCache();
        }

        private Texture2D Generate(int seed)
        {
            var pixels = new Color[ThumbnailSize * ThumbnailSize];
            var biomeMap = new BiomeMap(seed, WorldGenParams.ForType(WorldType.Default));
            var shaper = new TerrainShaper(seed, biomeMap, WorldGenParams.ForType(WorldType.Default));
            var (originX, originZ, span) = GetSampleRegion(seed);

            var samples = new Sample[ThumbnailSize * ThumbnailSize];
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            for (int py = 0; py < ThumbnailSize; py++)
            {
                for (int px = 0; px < ThumbnailSize; px++)
                {
                    int wx = originX + px * span / ThumbnailSize;
                    int wz = originZ + py * span / ThumbnailSize;
                    var (height, column) = shaper.BuildBaseColumn(wx, wz);
                    int index = py * ThumbnailSize + px;
                    samples[index] = new Sample(height, column);
                    minHeight = MathF.Min(minHeight, height);
                    maxHeight = MathF.Max(maxHeight, height);
                }
            }

            float heightRange = MathF.Max(8f, maxHeight - minHeight);
            var rng = new Random(seed ^ 0x5F3759DF);

            for (int py = 0; py < ThumbnailSize; py++)
            {
                for (int px = 0; px < ThumbnailSize; px++)
                {
                    int index = py * ThumbnailSize + px;
                    var sample = samples[index];
                    pixels[index] = ColorizeSample(sample, px, py, seed, minHeight, heightRange, rng);
                }
            }

            var texture = new Texture2D(_device, ThumbnailSize, ThumbnailSize);
            texture.SetData(pixels);
            return texture;
        }

        private static (int originX, int originZ, int span) GetSampleRegion(int seed)
        {
            uint hash = (uint)seed * 2654435761u;
            int originX = (int)(hash & 0x7FFF) + 256;
            int originZ = (int)((hash >> 15) & 0x7FFF) + 256;
            return (originX, originZ, 320);
        }

        private static Color ColorizeSample(Sample sample, int px, int py, int seed, float minHeight, float heightRange, Random rng)
        {
            float height = sample.Height;
            var column = sample.Column;
            bool underwater = column.Biome.Primary == BiomeType.Ocean
                || height < WorldConstants.SeaLevel - 0.5f
                || column.IsLake;

            Color color;
            if (underwater)
            {
                float depth = Math.Clamp((WorldConstants.SeaLevel - height) / 18f, 0f, 1f);
                var water = BlockParticleColors.GetColor(BlockType.Water);
                color = ToColor(water);
                color = Color.Lerp(color, color * 0.55f, depth);
            }
            else
            {
                color = ToColor(BlockParticleColors.GetColor(column.SurfaceBlock));
                float shade = Math.Clamp((height - minHeight) / heightRange, 0f, 1f);
                color = Color.Lerp(color * 0.82f, color * 1.08f, shade);

                if (column.Profile.TreeDensity > 0.18f)
                {
                    uint treeHash = HashPixel(seed, px, py);
                    if ((treeHash & 0xFF) < column.Profile.TreeDensity * 90f)
                    {
                        color = Color.Lerp(color, ToColor(BlockParticleColors.GetColor(BlockType.OakLeaves)), 0.55f);
                    }
                }
            }

            float vignette = 1f - MathF.Sqrt(
                MathF.Pow((px - (ThumbnailSize - 1) * 0.5f) / (ThumbnailSize * 0.55f), 2f)
                + MathF.Pow((py - (ThumbnailSize - 1) * 0.5f) / (ThumbnailSize * 0.55f), 2f));
            vignette = Math.Clamp(vignette, 0.82f, 1f);
            return color * vignette;
        }

        private static uint HashPixel(int seed, int px, int py)
        {
            unchecked
            {
                uint h = (uint)seed;
                h = h * 374761393u + (uint)px;
                h = h * 668265263u + (uint)py;
                h ^= h >> 13;
                h *= 1274126177u;
                return h;
            }
        }

        private static Color ToColor(NumericVector3 rgb)
        {
            return new Color(rgb.X, rgb.Y, rgb.Z);
        }

        private static string FormatBiome(BiomeType biome) => biome switch
        {
            BiomeType.Ocean => "Ocean",
            BiomeType.Beach => "Beach",
            BiomeType.Plains => "Plains",
            BiomeType.Forest => "Forest",
            BiomeType.Desert => "Desert",
            BiomeType.Mountains => "Mountains",
            BiomeType.SnowyPeaks => "Snowy peaks",
            BiomeType.Swamp => "Swamp",
            _ => "Wildlands"
        };

        private readonly struct Sample
        {
            public float Height { get; }
            public TerrainColumn Column { get; }

            public Sample(float height, TerrainColumn column)
            {
                Height = height;
                Column = column;
            }
        }
    }
}
