namespace Autonocraft.Engine.Audio
{
    public enum AmbientKind
    {
        Water,
        Wind
    }

    public static class ProceduralAmbient
    {
        public static Microsoft.Xna.Framework.Audio.SoundEffect Build(AmbientKind kind)
        {
            float[] samples = kind switch
            {
                AmbientKind.Water => BuildWaterLoop(),
                AmbientKind.Wind => BuildWindLoop(),
                _ => BuildWindLoop()
            };

            return WavEncoder.ToSoundEffect(samples);
        }

        private static float[] BuildWaterLoop()
        {
            var buffer = WaveSynth.AllocateSeconds(4f);
            WaveSynth.FillPinkNoise(buffer, 0.35f, seed: 101);
            WaveSynth.ApplyLowPass(buffer, 900f);

            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)WavEncoder.DefaultSampleRate;
                float lfo = 0.65f + 0.35f * MathF.Sin(t * MathF.PI * 2f * 0.2f);
                buffer[i] *= lfo;
            }

            WaveSynth.Normalize(buffer, 0.55f);
            return buffer;
        }

        private static float[] BuildWindLoop()
        {
            var buffer = WaveSynth.AllocateSeconds(8f);
            WaveSynth.FillPinkNoise(buffer, 0.3f, seed: 202);
            WaveSynth.ApplyLowPass(buffer, 450f);

            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)WavEncoder.DefaultSampleRate;
                float lfo = 0.5f + 0.5f * MathF.Sin(t * MathF.PI * 2f * 0.08f + MathF.Sin(t * 0.3f));
                buffer[i] *= lfo;
            }

            WaveSynth.Normalize(buffer, 0.45f);
            return buffer;
        }
    }
}
