using System;

namespace Autonocraft.World
{
    public sealed class BiomeMap
    {
        private readonly NoiseStack _temperatureNoise;
        private readonly NoiseStack _moistureNoise;
        private readonly NoiseStack _continentNoise;
        private readonly NoiseStack _erosionNoise;
        private readonly WorldGenParams _params;

        public BiomeMap(int seed, WorldGenParams parameters)
        {
            _params = parameters;
            _temperatureNoise = new NoiseStack(seed + 101);
            _moistureNoise = new NoiseStack(seed + 202);
            _continentNoise = new NoiseStack(seed + 303);
            _erosionNoise = new NoiseStack(seed + 404);
        }

        public BiomeSample Sample(int wx, int wz)
        {
            // Keep continentalness on large, unwarped noise so oceans/beaches form coherent coastlines.
            float continentalness = _continentNoise.Fbm(wx * 0.0014f, wz * 0.0014f, 4)
                + _params.ContinentalnessBias;

            var (warpX, warpZ) = _continentNoise.DomainWarp(wx * 0.0022f, wz * 0.0022f, 2.5f);
            float temperature = _temperatureNoise.Fbm(warpX + 50f, warpZ + 50f, 4);
            float moisture = _moistureNoise.Fbm(warpX + 150f, warpZ + 150f, 4);
            float erosion = _erosionNoise.Fbm(warpX + 250f, warpZ + 250f, 4);

            BiomeType biome = Classify(temperature, moisture, continentalness, erosion);
            return new BiomeSample
            {
                Primary = biome,
                Temperature = temperature,
                Moisture = moisture,
                Continentalness = continentalness,
                Erosion = erosion,
                Profile = BiomeProfile.For(biome)
            };
        }

        public BiomeProfile BlendProfiles(
            BiomeSample center,
            BiomeSample north,
            BiomeSample south,
            BiomeSample east,
            BiomeSample west,
            BiomeSample northEast,
            BiomeSample northWest,
            BiomeSample southEast,
            BiomeSample southWest)
        {
            float baseHeight = 0f;
            float amplitude = 0f;
            float ridgeWeight = 0f;
            float treeDensity = 0f;
            float totalWeight = 0f;
            bool allowTallGrass = false;
            bool allowFlowers = false;
            bool allowCactus = false;

            void Add(BiomeProfile profile, float weight)
            {
                baseHeight += profile.BaseHeight * weight;
                amplitude += profile.HeightAmplitude * weight;
                ridgeWeight += profile.RidgeWeight * weight;
                treeDensity += profile.TreeDensity * weight;
                totalWeight += weight;
                allowTallGrass |= profile.AllowTallGrass;
                allowFlowers |= profile.AllowFlowers;
                allowCactus |= profile.AllowCactus;
            }

            Add(center.Profile, 2f);
            Add(north.Profile, 1f);
            Add(south.Profile, 1f);
            Add(east.Profile, 1f);
            Add(west.Profile, 1f);
            Add(northEast.Profile, 0.55f);
            Add(northWest.Profile, 0.55f);
            Add(southEast.Profile, 0.55f);
            Add(southWest.Profile, 0.55f);

            float inv = totalWeight > 0f ? 1f / totalWeight : 1f;
            return new BiomeProfile
            {
                Type = center.Primary,
                BaseHeight = baseHeight * inv,
                HeightAmplitude = amplitude * inv,
                RidgeWeight = ridgeWeight * inv,
                SurfaceBlock = center.Profile.SurfaceBlock,
                SubsurfaceBlock = center.Profile.SubsurfaceBlock,
                FillerBlock = center.Profile.FillerBlock,
                TreeDensity = treeDensity * inv,
                AllowTallGrass = allowTallGrass,
                AllowFlowers = allowFlowers,
                AllowCactus = allowCactus
            };
        }

        private BiomeType Classify(float temperature, float moisture, float continentalness, float erosion)
        {
            if (continentalness < -0.22f)
            {
                return BiomeType.Ocean;
            }

            if (continentalness < -0.08f)
            {
                return BiomeType.Beach;
            }

            bool rugged = erosion > 0.5f * _params.MountainWeight && continentalness > 0.08f;
            if (temperature < -0.18f)
            {
                return rugged ? BiomeType.SnowyPeaks : BiomeType.Mountains;
            }

            if (rugged)
            {
                return temperature < 0.1f ? BiomeType.SnowyPeaks : BiomeType.Mountains;
            }

            if (temperature > 0.22f && moisture < -0.1f)
            {
                return BiomeType.Desert;
            }

            if (moisture > 0.18f && temperature > -0.08f && continentalness < 0.18f)
            {
                return BiomeType.Swamp;
            }

            if (moisture > 0.08f)
            {
                return BiomeType.Forest;
            }

            return BiomeType.Plains;
        }

    }
}
