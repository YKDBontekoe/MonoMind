namespace Autonocraft.World.Generation
{
    /// <summary>Default terrain noise — wraps legacy <see cref="PerlinNoise2D"/>.</summary>
    public sealed class PerlinNoiseProvider : INoiseProvider
    {
        private readonly PerlinNoise2D _noise2D;
        private readonly PerlinNoise3D _noise3D;

        public PerlinNoiseProvider(int seed)
        {
            _noise2D = new PerlinNoise2D(seed);
            _noise3D = new PerlinNoise3D(seed);
        }

        public float Sample2D(float x, float y) => _noise2D.Noise(x, y);

        public float Sample3D(float x, float y, float z) => _noise3D.Noise(x, y, z);
    }
}
