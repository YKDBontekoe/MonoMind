using System;

namespace Autonocraft.World
{
    public class PerlinNoise2D
    {
        private readonly int[] _p;

        public PerlinNoise2D(int seed)
        {
            var random = new Random(seed);
            _p = new int[512];
            var permutation = new int[256];
            for (int i = 0; i < 256; i++) permutation[i] = i;

            for (int i = 255; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
            }

            for (int i = 0; i < 256; i++)
            {
                _p[i] = permutation[i];
                _p[256 + i] = permutation[i];
            }
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float t, float a, float b) => a + t * (b - a);

        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 7;
            float u = h < 4 ? x : y;
            float v = h < 4 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? 2.0f * v : -2.0f * v);
        }

        public float Noise(float x, float y)
        {
            int X = (int)MathF.Floor(x) & 255;
            int Y = (int)MathF.Floor(y) & 255;

            x -= MathF.Floor(x);
            y -= MathF.Floor(y);

            float u = Fade(x);
            float v = Fade(y);

            int A = _p[X] + Y;
            int B = _p[X + 1] + Y;

            return Lerp(v, Lerp(u, Grad(_p[A], x, y),
                                   Grad(_p[B], x - 1, y)),
                           Lerp(u, Grad(_p[A + 1], x, y - 1),
                                   Grad(_p[B + 1], x - 1, y - 1)));
        }
    }
}
