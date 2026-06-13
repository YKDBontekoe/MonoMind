using System;

namespace Autonocraft.World
{
    public readonly struct RiverSample
    {
        public float Strength { get; init; }
        public float BankStrength { get; init; }
        public bool IsRiver => Strength > 0.3f;
    }

    public sealed class BiomeMap
    {
        private readonly NoiseStack _temperatureNoise;
        private readonly NoiseStack _moistureNoise;
        private readonly NoiseStack _continentNoise;
        private readonly NoiseStack _erosionNoise;
        private readonly NoiseStack _riverNoise;
        private readonly WorldGenParams _params;

        public BiomeMap(int seed, WorldGenParams parameters)
        {
            _params = parameters;
            _temperatureNoise = new NoiseStack(seed + 101);
            _moistureNoise = new NoiseStack(seed + 202);
            _continentNoise = new NoiseStack(seed + 303);
            _erosionNoise = new NoiseStack(seed + 404);
            _riverNoise = new NoiseStack(seed + 606);
        }

        public BiomeSample Sample(int wx, int wz)
        {
            var (warpX, warpZ) = _continentNoise.DomainWarp(wx * 0.0022f, wz * 0.0022f, 4.5f);

            float temperature = _temperatureNoise.Fbm(warpX + 50f, warpZ + 50f, 4);
            float moisture = _moistureNoise.Fbm(warpX + 150f, warpZ + 150f, 4);
            float continentalness = _continentNoise.Fbm(warpX, warpZ, 5) + _params.ContinentalnessBias;
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
            if (continentalness < -0.2f)
            {
                return BiomeType.Ocean;
            }

            if (continentalness < -0.06f)
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

        public RiverSample SampleRiver(int wx, int wz, BiomeSample biome, float surfaceHeight)
        {
            if (!_params.EnableRivers)
            {
                return default;
            }

            if (biome.Primary is BiomeType.Ocean or BiomeType.Beach)
            {
                return default;
            }

            if (surfaceHeight > WorldConstants.SeaLevel + 30 || biome.Continentalness < -0.02f)
            {
                return default;
            }

            var (riverX, riverZ) = _riverNoise.DomainWarp(wx * 0.0065f, wz * 0.0065f, 2.2f);
            float mainChannel = MathF.Abs(_riverNoise.Raw(riverX, riverZ));
            float tributary = MathF.Abs(_riverNoise.Raw(riverX * 1.7f + 19.7f, riverZ * 1.7f - 31.3f)) + 0.018f;
            float channel = MathF.Min(mainChannel, tributary);

            float moistureWidth = Math.Clamp((biome.Moisture + 0.35f) * 0.018f, 0f, 0.018f);
            float width = 0.032f + moistureWidth;
            float bankEdge = width + 0.075f;
            float strength = 1f - SmoothStep(width, bankEdge, channel);
            if (strength <= 0f)
            {
                return default;
            }

            return new RiverSample
            {
                Strength = strength,
                BankStrength = 1f - SmoothStep(width + 0.035f, bankEdge + 0.07f, channel)
            };
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
