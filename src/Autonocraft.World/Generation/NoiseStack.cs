using System;
using Autonocraft.World.Generation;

namespace Autonocraft.World
{
    public sealed class NoiseStack
    {
        private readonly INoiseProvider _noise;

        public NoiseStack(int seed)
        {
            _noise = new PerlinNoiseProvider(seed);
        }

        public float Fbm(float x, float y, int octaves = 5, float lacunarity = 2f, float persistence = 0.5f)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                total += _noise.Sample2D(x * frequency, y * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return maxValue > 0f ? total / maxValue : 0f;
        }

        public float Ridged(float x, float y, int octaves = 4, float lacunarity = 2f, float persistence = 0.5f)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - MathF.Abs(_noise.Sample2D(x * frequency, y * frequency));
                n *= n;
                total += n * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return maxValue > 0f ? total / maxValue : 0f;
        }

        public (float wx, float wz) DomainWarp(float x, float y, float strength = 12f)
        {
            float warpX = Fbm(x + 5.2f, y + 1.3f, 3, 2f, 0.5f) * strength;
            float warpZ = Fbm(x + 8.7f, y + 4.1f, 3, 2f, 0.5f) * strength;
            return (x + warpX, y + warpZ);
        }

        public float Raw(float x, float y) => _noise.Sample2D(x, y);
    }
}
