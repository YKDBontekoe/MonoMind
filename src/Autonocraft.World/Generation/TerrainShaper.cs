using System;
using Autonocraft.World.Structures;

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
        public float SmoothedHeight { get; init; }
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

        public (float height, TerrainColumn draft) BuildBaseColumn(int wx, int wz) =>
            BuildBaseColumn(wx, wz, biomeCache: null);

        internal (float height, TerrainColumn draft) BuildBaseColumn(int wx, int wz, BiomeSampleCache? biomeCache)
        {
            if (StructureGallery.IsGalleryWorld(_params.WorldType))
            {
                BiomeSample centerSample = biomeCache != null ? biomeCache.Sample(wx, wz) : _biomeMap.Sample(wx, wz);
                var centerProfile = _biomeMap.BlendProfiles(centerSample, centerSample, centerSample, centerSample, centerSample, centerSample, centerSample, centerSample, centerSample);
                float h = WorldConstants.SeaLevel;
                var draftColumn = new TerrainColumn
                {
                    SurfaceHeight = (int)h,
                    Biome = centerSample,
                    Profile = centerProfile,
                    SurfaceBlock = BlockType.Grass,
                    SubsurfaceBlock = BlockType.Dirt,
                    FillerBlock = BlockType.Stone,
                    SmoothedHeight = h
                };
                return (h, draftColumn);
            }
            BiomeSample SampleAt(int x, int z) =>
                biomeCache != null ? biomeCache.Sample(x, z) : _biomeMap.Sample(x, z);

            const int biomeBlendStep = 5;
            var center = SampleAt(wx, wz);
            var north = SampleAt(wx, wz - biomeBlendStep);
            var south = SampleAt(wx, wz + biomeBlendStep);
            var east = SampleAt(wx + biomeBlendStep, wz);
            var west = SampleAt(wx - biomeBlendStep, wz);
            var northEast = SampleAt(wx + biomeBlendStep, wz - biomeBlendStep);
            var northWest = SampleAt(wx - biomeBlendStep, wz - biomeBlendStep);
            var southEast = SampleAt(wx + biomeBlendStep, wz + biomeBlendStep);
            var southWest = SampleAt(wx - biomeBlendStep, wz + biomeBlendStep);
            var profile = _biomeMap.BlendProfiles(center, north, south, east, west, northEast, northWest, southEast, southWest);

            float broad = _terrainNoise.Fbm(wx * 0.0045f, wz * 0.0045f, 4);
            float detail = _terrainNoise.Fbm(wx * 0.012f, wz * 0.012f, 3);
            float ridge = _terrainNoise.Ridged(wx * 0.009f, wz * 0.009f, 4);
            float ruggedness = SmoothStep(0.1f, 0.55f, center.Erosion) * _params.MountainWeight;
            bool isVolcanic = center.Primary == BiomeType.Volcanic;
            bool isMountain = center.Primary is BiomeType.Mountains or BiomeType.SnowyPeaks;
            bool isElevatedTerrain = isMountain || isVolcanic;

            float amp = profile.HeightAmplitude * _params.HeightScale;
            float height = profile.BaseHeight + _params.HeightOffset;

            if (isMountain)
            {
                float massif = _terrainNoise.Fbm(wx * 0.0015f, wz * 0.0015f, 5);
                float foothills = _terrainNoise.Fbm(wx * 0.0036f, wz * 0.0036f, 4);
                float peakRidges = _terrainNoise.Ridged(wx * 0.0052f, wz * 0.0052f, 4);
                float cliffBands = _terrainNoise.Ridged(wx * 0.013f, wz * 0.013f, 3);
                float valleyNoise = _terrainNoise.Fbm(wx * 0.0062f, wz * 0.0062f, 3);

                float massifLift = SmoothStep(-0.30f, 0.58f, massif);
                float foothillLift = ((foothills + 1f) * 0.5f) * massifLift;

                height += massifLift * amp * 0.78f;
                height += foothillLift * amp * 0.32f;
                height += peakRidges * massifLift * profile.RidgeWeight * amp * (0.20f + ruggedness * 0.16f);
                height += detail * amp * 0.12f;

                float erosionStrength = SmoothStep(-0.08f, 0.40f, center.Erosion);
                float valleyCut = erosionStrength * (0.60f - ((valleyNoise + 1f) * 0.5f) * 0.40f) * amp * 0.28f;
                height -= MathF.Max(0f, valleyCut);

                float cliffStrength = cliffBands * massifLift * SmoothStep(0.18f, 0.72f, peakRidges);
                height += cliffStrength * amp * 0.13f;

                if (center.Primary == BiomeType.SnowyPeaks)
                {
                    float glacierNoise = _terrainNoise.Fbm(wx * 0.0068f, wz * 0.0068f, 3);
                    float glacierMask = SmoothStep(0.22f, 0.58f, glacierNoise);
                    float snowLine = WorldConstants.SeaLevel + WorldConstants.SnowLineOffset;
                    float glacierBlend = SmoothStep(snowLine - 24f, snowLine + 6f, height);
                    float targetPlateau = profile.BaseHeight + amp * (0.76f + glacierMask * 0.14f);
                    height = Lerp(height, targetPlateau, glacierBlend * glacierMask * 0.58f);
                }
            }
            else
            {
                height += broad * amp * 0.68f
                    + detail * amp * (isElevatedTerrain ? (isVolcanic ? 0.14f : 0.18f) : 0.07f)
                    + ridge * profile.RidgeWeight * amp * (isElevatedTerrain ? (isVolcanic ? 0.42f : (0.35f + ruggedness * 0.3f)) : 0f);
            }

            if (isVolcanic)
            {
                float cone = _terrainNoise.Ridged(wx * 0.018f, wz * 0.018f, 3);
                float coneMask = SmoothStep(0.35f, 0.85f, cone);
                height += coneMask * profile.HeightAmplitude * _params.HeightScale * 0.55f;
            }

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

            if (height > WorldConstants.SeaLevel + WorldConstants.SnowLineOffset && surface != BlockType.Snow && !isVolcanic)
            {
                surface = BlockType.Snow;
                subsurface = BlockType.Stone;
            }

            if (isMountain && center.Primary == BiomeType.SnowyPeaks
                && height > WorldConstants.SeaLevel + WorldConstants.SnowLineOffset - 4f)
            {
                float glacierNoise = _terrainNoise.Fbm(wx * 0.0068f, wz * 0.0068f, 3);
                if (glacierNoise > 0.26f)
                {
                    surface = BlockType.Ice;
                    subsurface = BlockType.Ice;
                }
            }
            else if (isMountain && height > WorldConstants.SeaLevel + 14f
                && height < WorldConstants.SeaLevel + WorldConstants.SnowLineOffset - 8f)
            {
                float scree = _terrainNoise.Ridged(wx * 0.013f, wz * 0.013f, 3);
                if (scree > 0.58f)
                {
                    surface = BlockType.Gravel;
                    subsurface = BlockType.Stone;
                }
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
            chunk.FillTerrainColumn(lx, lz, column);
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
