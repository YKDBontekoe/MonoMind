using System;

namespace Autonocraft.World
{
    public readonly struct TerrainColumn
    {
        public int SurfaceHeight { get; init; }
        public BiomeSample Biome { get; init; }
        public BiomeProfile Profile { get; init; }
        public float RiverStrength { get; init; }
        public bool IsRiver { get; init; }
        public bool IsLake { get; init; }
        public BlockType SurfaceBlock { get; init; }
        public BlockType SubsurfaceBlock { get; init; }
        public BlockType FillerBlock { get; init; }
    }

    public sealed class TerrainShaper
    {
        private readonly NoiseStack _terrainNoise;
        private readonly BiomeMap _biomeMap;
        private readonly WorldGenParams _params;

        public TerrainShaper(int seed, BiomeMap biomeMap, WorldGenParams parameters)
        {
            _params = parameters;
            _biomeMap = biomeMap;
            _terrainNoise = new NoiseStack(seed + 505);
        }

        public TerrainColumn BuildColumn(int wx, int wz)
        {
            const int biomeBlendStep = 5;
            var center = _biomeMap.Sample(wx, wz);
            var north = _biomeMap.Sample(wx, wz - biomeBlendStep);
            var south = _biomeMap.Sample(wx, wz + biomeBlendStep);
            var east = _biomeMap.Sample(wx + biomeBlendStep, wz);
            var west = _biomeMap.Sample(wx - biomeBlendStep, wz);
            var northEast = _biomeMap.Sample(wx + biomeBlendStep, wz - biomeBlendStep);
            var northWest = _biomeMap.Sample(wx - biomeBlendStep, wz - biomeBlendStep);
            var southEast = _biomeMap.Sample(wx + biomeBlendStep, wz + biomeBlendStep);
            var southWest = _biomeMap.Sample(wx - biomeBlendStep, wz + biomeBlendStep);
            var profile = _biomeMap.BlendProfiles(center, north, south, east, west, northEast, northWest, southEast, southWest);

            float broad = _terrainNoise.Fbm(wx * 0.006f, wz * 0.006f, 4);
            float detail = _terrainNoise.Fbm(wx * 0.018f, wz * 0.018f, 4);
            float micro = _terrainNoise.Fbm(wx * 0.045f, wz * 0.045f, 2);
            float ridge = _terrainNoise.Ridged(wx * 0.01f, wz * 0.01f, 4);
            float ruggedness = SmoothStep(0.1f, 0.55f, center.Erosion) * _params.MountainWeight;

            float height = profile.BaseHeight
                + broad * profile.HeightAmplitude * _params.HeightScale * 0.72f
                + detail * profile.HeightAmplitude * _params.HeightScale * 0.32f
                + micro * profile.HeightAmplitude * _params.HeightScale * 0.08f
                + ridge * profile.RidgeWeight * profile.HeightAmplitude * _params.HeightScale * (0.35f + ruggedness * 0.25f)
                + _params.HeightOffset;

            if (center.Primary == BiomeType.Ocean)
            {
                float shelf = SmoothStep(-0.55f, -0.2f, center.Continentalness);
                float oceanFloor = Lerp(WorldConstants.SeaLevel - 28, WorldConstants.SeaLevel - 7, shelf)
                    + detail * 3.5f
                    + micro * 1.5f;
                height = MathF.Min(height, oceanFloor);
            }

            if (center.Primary == BiomeType.Beach)
            {
                float beachRise = SmoothStep(-0.06f, 0.04f, center.Continentalness);
                height = Lerp(WorldConstants.SeaLevel - 1, WorldConstants.SeaLevel + 2.5f, beachRise) + detail * 1.25f;
            }

            var river = _biomeMap.SampleRiver(wx, wz, center, height);
            if (river.BankStrength > 0f)
            {
                float riverBed = WorldConstants.SeaLevel - 2.5f - river.Strength * 1.75f;
                height = Lerp(height, riverBed, Math.Clamp(river.BankStrength, 0f, 1f));
            }

            bool isLake = center.Primary == BiomeType.Swamp && center.Moisture > 0.32f && broad < -0.16f;
            if (isLake)
            {
                height = MathF.Min(height, WorldConstants.SeaLevel - 1);
            }

            int surfaceHeight = Math.Clamp((int)MathF.Round(height), 1, Chunk.Height - 12);

            BlockType surface = profile.SurfaceBlock;
            BlockType subsurface = profile.SubsurfaceBlock;

            if (center.Primary == BiomeType.Ocean && surfaceHeight < WorldConstants.SeaLevel - 10 && detail < -0.04f)
            {
                surface = BlockType.Gravel;
                subsurface = BlockType.Gravel;
            }
            else if (river.IsRiver)
            {
                surface = river.Strength > 0.72f ? BlockType.Gravel : BlockType.Sand;
                subsurface = surface;
            }
            else if (center.Primary == BiomeType.Beach || (surfaceHeight <= WorldConstants.BeachMaxHeight && center.Continentalness < 0.12f))
            {
                surface = BlockType.Sand;
                subsurface = BlockType.Sand;
            }

            if (surfaceHeight > WorldConstants.SeaLevel + 55 && surface != BlockType.Snow)
            {
                surface = BlockType.Snow;
                subsurface = BlockType.Stone;
            }

            return new TerrainColumn
            {
                SurfaceHeight = surfaceHeight,
                Biome = center,
                Profile = profile,
                RiverStrength = river.Strength,
                IsRiver = river.IsRiver,
                IsLake = isLake,
                SurfaceBlock = surface,
                SubsurfaceBlock = subsurface,
                FillerBlock = profile.FillerBlock
            };
        }

        public void FillColumn(Chunk chunk, int lx, int lz, TerrainColumn column)
        {
            int height = column.SurfaceHeight;

            for (int y = 0; y < Chunk.Height; y++)
            {
                BlockType block;
                if (y > height)
                {
                    if (y <= WorldConstants.SeaLevel && (column.Biome.Primary == BiomeType.Ocean || column.IsRiver || column.IsLake || y <= WorldConstants.SeaLevel - 2))
                    {
                        block = BlockType.Water;
                    }
                    else
                    {
                        block = BlockType.Air;
                    }
                }
                else if (y == height)
                {
                    block = column.SurfaceBlock;
                }
                else if (y > height - WorldConstants.DirtDepth)
                {
                    block = column.SubsurfaceBlock;
                }
                else if (y <= 2)
                {
                    block = BlockType.Stone;
                }
                else
                {
                    block = column.FillerBlock;
                }

                chunk.SetBlock(lx, y, lz, block);
            }
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Clamp(t, 0f, 1f);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
