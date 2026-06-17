namespace Autonocraft.World.Generation.Caves
{
    public sealed class CaveBiomeMap
    {
        private readonly PerlinNoise3D _humidityNoise;
        private readonly PerlinNoise3D _crystalNoise;
        private readonly PerlinNoise3D _dripstoneNoise;

        public CaveBiomeMap(int seed)
        {
            _humidityNoise = new PerlinNoise3D(seed + 808);
            _crystalNoise = new PerlinNoise3D(seed + 909);
            _dripstoneNoise = new PerlinNoise3D(seed + 1010);
        }

        public CaveBiomeType Sample(int wx, int y, int wz)
        {
            float depth = y / (float)Chunk.Height;
            float humidity = _humidityNoise.Noise(wx * 0.008f, y * 0.012f, wz * 0.008f);
            float crystal = _crystalNoise.Noise(wx * 0.014f, y * 0.014f, wz * 0.014f);
            float drip = _dripstoneNoise.Noise(wx * 0.011f, y * 0.009f, wz * 0.011f);

            if (depth < 0.08f && y < 18)
            {
                return CaveBiomeType.DeepDark;
            }

            if (crystal > 0.40f && y < 52)
            {
                return CaveBiomeType.Crystal;
            }

            if (humidity > 0.34f && y > 22 && y < 70)
            {
                return CaveBiomeType.Lush;
            }

            if (humidity > 0.26f && y > 14 && y < 58)
            {
                return CaveBiomeType.Mushroom;
            }

            if (drip > 0.36f && y > 10 && y < 64)
            {
                return CaveBiomeType.Dripstone;
            }

            return CaveBiomeType.Stone;
        }

        public CaveBiomeProfile SampleProfile(int wx, int y, int wz) =>
            CaveBiomeProfile.For(Sample(wx, y, wz));
    }
}
