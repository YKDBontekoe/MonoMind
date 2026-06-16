using System.Collections.Generic;

namespace Autonocraft.World
{
    /// <summary>
    /// Per-chunk memoization for biome samples. BuildBaseColumn touches nine coordinates
    /// per height sample; padding expands the grid to ~32×32 unique cells per chunk.
    /// </summary>
    internal sealed class BiomeSampleCache
    {
        private readonly BiomeMap _map;
        private readonly Dictionary<(int x, int z), BiomeSample> _samples = new();

        public BiomeSampleCache(BiomeMap map) => _map = map;

        public BiomeSample Sample(int wx, int wz)
        {
            var key = (wx, wz);
            if (!_samples.TryGetValue(key, out var sample))
            {
                sample = _map.Sample(wx, wz);
                _samples[key] = sample;
            }

            return sample;
        }
    }
}
