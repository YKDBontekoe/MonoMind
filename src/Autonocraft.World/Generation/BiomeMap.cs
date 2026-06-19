using System;

namespace Autonocraft.World
{
    public sealed class BiomeMap
    {
        private readonly NoiseStack _temperatureNoise;
        private readonly NoiseStack _moistureNoise;
        private readonly NoiseStack _continentNoise;
        private readonly NoiseStack _erosionNoise;
        private readonly NoiseStack _upliftNoise;
        private readonly NoiseStack _volcanicBeltNoise;
        private readonly WorldGenParams _params;

        public BiomeMap(int seed, WorldGenParams parameters)
        {
            _params = parameters;
            _temperatureNoise = new NoiseStack(seed + 101);
            _moistureNoise = new NoiseStack(seed + 202);
            _continentNoise = new NoiseStack(seed + 303);
            _erosionNoise = new NoiseStack(seed + 404);
            _upliftNoise = new NoiseStack(seed + 515);
            _volcanicBeltNoise = new NoiseStack(seed + 616);
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

            // Broad FBM domes form wide mountain massifs; ridged arcs are reserved for volcanic belts only.
            float uplift = _upliftNoise.Fbm(wx * 0.00038f, wz * 0.00038f, 5)
                + _upliftNoise.Fbm(wx * 0.00022f, wz * 0.00022f, 4) * 0.58f;
            uplift *= _params.MountainWeight;

            float volcanicArc = _volcanicBeltNoise.Ridged(wx * 0.00050f, wz * 0.00050f, 3);
            float volcanicBasin = _volcanicBeltNoise.Fbm(wx * 0.0016f, wz * 0.0016f, 3);
            float volcanicStrength = volcanicArc * 0.74f + volcanicBasin * 0.26f;

            BiomeType biome = Classify(temperature, moisture, continentalness, erosion, uplift, volcanicStrength);
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
            float floraDensity = 0f;
            float totalWeight = 0f;
            bool allowTallGrass = false;
            bool allowFlowers = false;
            bool allowCactus = false;
            bool allowUnderstory = false;

            void Add(BiomeProfile profile, float weight)
            {
                baseHeight += profile.BaseHeight * weight;
                amplitude += profile.HeightAmplitude * weight;
                ridgeWeight += profile.RidgeWeight * weight;
                treeDensity += profile.TreeDensity * weight;
                floraDensity += profile.FloraDensity * weight;
                totalWeight += weight;
                allowTallGrass |= profile.AllowTallGrass;
                allowFlowers |= profile.AllowFlowers;
                allowCactus |= profile.AllowCactus;
                allowUnderstory |= profile.AllowUnderstory;
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
                FloraDensity = floraDensity * inv,
                AllowTallGrass = allowTallGrass,
                AllowFlowers = allowFlowers,
                AllowCactus = allowCactus,
                AllowUnderstory = allowUnderstory
            };
        }

        private static BiomeType Classify(
            float temperature,
            float moisture,
            float continentalness,
            float erosion,
            float uplift,
            float volcanicStrength)
        {
            if (continentalness < -0.22f)
            {
                return BiomeType.Ocean;
            }

            if (continentalness < -0.08f)
            {
                return BiomeType.Beach;
            }

            if (continentalness < 0f && continentalness > -0.18f && moisture > 0.12f && temperature > 0f)
            {
                return BiomeType.Mangrove;
            }

            bool inland = continentalness > 0.05f;
            bool inVolcanicBelt = inland && volcanicStrength > 0.08f;
            bool inMountainBelt = inland && uplift > 0.0f && !inVolcanicBelt;
            bool inHighlands = inland && uplift > -0.05f;
            bool inValley = inland && uplift < -0.10f;

            if (inVolcanicBelt && temperature > -0.10f)
            {
                return BiomeType.Volcanic;
            }

            if (temperature > 0.06f && moisture > 0.14f && uplift < 0.10f && volcanicStrength < 0.08f)
            {
                return BiomeType.Jungle;
            }

            if (moisture > 0.22f && temperature > -0.10f && temperature < 0.24f && uplift < 0.08f)
            {
                return BiomeType.MushroomForest;
            }

            if (inMountainBelt)
            {
                if (temperature < 0.04f || (temperature < 0.12f && erosion > 0.30f))
                {
                    return BiomeType.SnowyPeaks;
                }

                return BiomeType.Mountains;
            }

            if (temperature > 0.10f && moisture < -0.02f && uplift < 0.06f)
            {
                if (moisture < -0.10f && erosion > 0.26f)
                {
                    return BiomeType.Badlands;
                }

                return BiomeType.Desert;
            }

            if (moisture > 0.10f && temperature > -0.12f && uplift < 0.02f && (inValley || continentalness < 0.28f))
            {
                return BiomeType.Swamp;
            }

            if (temperature < -0.02f && moisture > -0.20f && uplift < 0.08f)
            {
                return BiomeType.BorealTaiga;
            }

            if (temperature < -0.05f && moisture > -0.12f && inHighlands)
            {
                return BiomeType.SnowyPeaks;
            }

            if (inValley && temperature > -0.10f && temperature < 0.20f && moisture > -0.14f && moisture < 0.16f)
            {
                return BiomeType.Plains;
            }

            if (uplift < 0.02f && moisture > -0.10f && moisture < 0.10f && temperature > -0.06f && temperature < 0.14f)
            {
                return BiomeType.Plains;
            }

            return BiomeType.Forest;
        }
    }
}
