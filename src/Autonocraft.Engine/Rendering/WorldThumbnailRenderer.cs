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
        public const int MapPreviewSize = 96;

        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, Texture2D> _cache = new();
        private readonly Dictionary<MapPreviewKey, Texture2D> _mapCache = new();

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

        public Texture2D GetMapPreview(int seed, int centerX, int centerZ, int span = 512)
        {
            var key = new MapPreviewKey(seed, centerX / 16, centerZ / 16, span);
            if (_mapCache.TryGetValue(key, out Texture2D? cached))
            {
                return cached;
            }

            var texture = Generate(seed, MapPreviewSize, centerX - span / 2, centerZ - span / 2, span);
            _mapCache[key] = texture;
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

            foreach (var texture in _mapCache.Values)
            {
                texture.Dispose();
            }

            _mapCache.Clear();
        }

        public void Dispose()
        {
            ClearCache();
        }

        private Texture2D Generate(int seed)
        {
            var (originX, originZ, span) = GetSampleRegion(seed);
            return Generate(seed, ThumbnailSize, originX, originZ, span);
        }

        private Texture2D Generate(int seed, int size, int originX, int originZ, int span)
        {
            var pixels = new Color[size * size];
            var biomeMap = new BiomeMap(seed, WorldGenParams.ForType(WorldType.Default));
            var shaper = new TerrainShaper(seed, biomeMap, WorldGenParams.ForType(WorldType.Default));

            var samples = new Sample[size * size];
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    int wx = originX + px * span / size;
                    int wz = originZ + py * span / size;
                    var (height, column) = shaper.BuildBaseColumn(wx, wz);
                    int index = py * size + px;
                    samples[index] = new Sample(height, column);
                    minHeight = MathF.Min(minHeight, height);
                    maxHeight = MathF.Max(maxHeight, height);
                }
            }

            float heightRange = MathF.Max(8f, maxHeight - minHeight);
            var rng = new Random(seed ^ 0x5F3759DF);

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    int index = py * size + px;
                    var sample = samples[index];
                    pixels[index] = ColorizeSample(sample, px, py, size, seed, minHeight, heightRange, rng);
                }
            }

            var texture = new Texture2D(_device, size, size);
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

        private static Color ColorizeSample(Sample sample, int px, int py, int size, int seed, float minHeight, float heightRange, Random rng)
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
                MathF.Pow((px - (size - 1) * 0.5f) / (size * 0.62f), 2f)
                + MathF.Pow((py - (size - 1) * 0.5f) / (size * 0.62f), 2f));
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
            BiomeType.Jungle => "Jungle",
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

        private readonly struct MapPreviewKey : IEquatable<MapPreviewKey>
        {
            private readonly int _seed;
            private readonly int _centerChunkX;
            private readonly int _centerChunkZ;
            private readonly int _span;

            public MapPreviewKey(int seed, int centerChunkX, int centerChunkZ, int span)
            {
                _seed = seed;
                _centerChunkX = centerChunkX;
                _centerChunkZ = centerChunkZ;
                _span = span;
            }

            public bool Equals(MapPreviewKey other) =>
                _seed == other._seed &&
                _centerChunkX == other._centerChunkX &&
                _centerChunkZ == other._centerChunkZ &&
                _span == other._span;

            public override bool Equals(object? obj) => obj is MapPreviewKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(_seed, _centerChunkX, _centerChunkZ, _span);
        }
    }
}
