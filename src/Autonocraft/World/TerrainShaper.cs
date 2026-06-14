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

        public (float height, TerrainColumn draft) BuildBaseColumn(int wx, int wz)
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

            float broad = _terrainNoise.Fbm(wx * 0.0045f, wz * 0.0045f, 4);
            float detail = _terrainNoise.Fbm(wx * 0.012f, wz * 0.012f, 3);
            float ridge = _terrainNoise.Ridged(wx * 0.009f, wz * 0.009f, 4);
            float ruggedness = SmoothStep(0.1f, 0.55f, center.Erosion) * _params.MountainWeight;
            bool isMountain = center.Primary is BiomeType.Mountains or BiomeType.SnowyPeaks;

            float height = profile.BaseHeight
                + broad * profile.HeightAmplitude * _params.HeightScale * 0.68f
                + detail * profile.HeightAmplitude * _params.HeightScale * (isMountain ? 0.18f : 0.07f)
                + ridge * profile.RidgeWeight * profile.HeightAmplitude * _params.HeightScale * (isMountain ? (0.35f + ruggedness * 0.3f) : 0f)
                + _params.HeightOffset;

            if (center.Primary == BiomeType.Ocean)
            {
                float depthFactor = SmoothStep(-0.55f, -0.24f, center.Continentalness);
                float coastalFloor = WorldConstants.SeaLevel - 3f;
                float deepFloor = WorldConstants.SeaLevel - 24f;
                float oceanFloor = Lerp(deepFloor, coastalFloor, depthFactor)
                    + detail * Lerp(1.2f, 0.35f, depthFactor);
                height = MathF.Min(height, oceanFloor);
            }

            if (center.Primary == BiomeType.Beach)
            {
                float beachRise = SmoothStep(-0.16f, -0.06f, center.Continentalness);
                height = Lerp(WorldConstants.SeaLevel + 0.5f, WorldConstants.SeaLevel + 3f, beachRise)
                    + detail * 0.2f;
            }

            bool isLake = center.Primary == BiomeType.Swamp && center.Moisture > 0.32f && broad < -0.16f;
            if (isLake)
            {
                height = MathF.Min(height, WorldConstants.SeaLevel - 1);
            }

            BlockType surface = profile.SurfaceBlock;
            BlockType subsurface = profile.SubsurfaceBlock;

            if (center.Primary == BiomeType.Ocean && height < WorldConstants.SeaLevel - 10 && detail < -0.04f)
            {
                surface = BlockType.Gravel;
                subsurface = BlockType.Gravel;
            }
            else if (center.Primary == BiomeType.Beach || (height <= WorldConstants.BeachMaxHeight && center.Continentalness < 0.05f))
            {
                surface = BlockType.Sand;
                subsurface = BlockType.Sand;
            }

            if (height > WorldConstants.SeaLevel + 55 && surface != BlockType.Snow)
            {
                surface = BlockType.Snow;
                subsurface = BlockType.Stone;
            }

            var draft = new TerrainColumn
            {
                Biome = center,
                Profile = profile,
                IsLake = isLake,
                SurfaceBlock = surface,
                SubsurfaceBlock = subsurface,
                FillerBlock = profile.FillerBlock
            };

            return (height, draft);
        }

        public void FillColumn(Chunk chunk, int lx, int lz, TerrainColumn column)
        {
            int height = column.SurfaceHeight;

            for (int y = 0; y < Chunk.Height; y++)
            {
                BlockType block;
                if (y > height)
                {
                    if (y <= WorldConstants.SeaLevel && (column.Biome.Primary == BiomeType.Ocean || column.IsRiver || column.IsLake))
                    {
                        bool freezeSurface = column.Biome.Primary == BiomeType.SnowyPeaks
                            || column.Biome.Temperature < -0.08f;
                        block = y == WorldConstants.SeaLevel && freezeSurface
                            ? BlockType.Ice
                            : BlockType.Water;
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
