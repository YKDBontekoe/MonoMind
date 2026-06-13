using System;

namespace Autonocraft.World
{
    public class PerlinNoise3D
    {
        private readonly int[] _p;

        public PerlinNoise3D(int seed)
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

        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        public float Noise(float x, float y, float z)
        {
            int X = (int)MathF.Floor(x) & 255;
            int Y = (int)MathF.Floor(y) & 255;
            int Z = (int)MathF.Floor(z) & 255;

            x -= MathF.Floor(x);
            y -= MathF.Floor(y);
            z -= MathF.Floor(z);

            float u = Fade(x);
            float v = Fade(y);
            float w = Fade(z);

            int A = _p[X] + Y;
            int AA = _p[A] + Z;
            int AB = _p[A + 1] + Z;
            int B = _p[X + 1] + Y;
            int BA = _p[B] + Z;
            int BB = _p[B + 1] + Z;

            return Lerp(w,
                Lerp(v, Lerp(u, Grad(_p[AA], x, y, z), Grad(_p[BA], x - 1, y, z)),
                          Lerp(u, Grad(_p[AB], x, y - 1, z), Grad(_p[BB], x - 1, y - 1, z))),
                Lerp(v, Lerp(u, Grad(_p[AA + 1], x, y, z - 1), Grad(_p[BA + 1], x - 1, y, z - 1)),
                          Lerp(u, Grad(_p[AB + 1], x, y - 1, z - 1), Grad(_p[BB + 1], x - 1, y - 1, z - 1))));
        }
    }
}
