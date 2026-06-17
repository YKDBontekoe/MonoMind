using System;

namespace Autonocraft.Engine.Audio
{
    public enum AmbientKind
    {
        Water,
        Wind,
        Forest,
        Swamp,
        Desert,
        Cave
    }

    public static class ProceduralAmbient
    {
        public static Microsoft.Xna.Framework.Audio.SoundEffect Build(AmbientKind kind)
        {
            float[] samples = kind switch
            {
                AmbientKind.Water => BuildWaterLoop(),
                AmbientKind.Wind => BuildWindLoop(),
                AmbientKind.Forest => BuildForestLoop(),
                AmbientKind.Swamp => BuildSwampLoop(),
                AmbientKind.Desert => BuildDesertLoop(),
                AmbientKind.Cave => BuildCaveLoop(),
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

        private static float[] BuildForestLoop()
        {
            var buffer = WaveSynth.AllocateSeconds(8f);
            // Forest background rustle
            WaveSynth.FillPinkNoise(buffer, 0.15f, seed: 303);
            WaveSynth.ApplyLowPass(buffer, 600f);

            // Add bird chirps randomly
            Random rand = new Random(101);
            for (int chirp = 0; chirp < 12; chirp++)
            {
                float startTime = chirp * 0.65f + (float)rand.NextDouble() * 0.2f;
                int startIndex = (int)(startTime * WavEncoder.DefaultSampleRate);
                if (startIndex + 8000 < buffer.Length)
                {
                    // Synthesize a bird chirp: high pitch descending sine wave
                    for (int j = 0; j < 8000; j++)
                    {
                        float t = j / (float)WavEncoder.DefaultSampleRate;
                        float freq = 3500f - t * 4000f; // sliding frequency
                        float envelope = MathF.Sin(j / 8000f * MathF.PI);
                        buffer[startIndex + j] += MathF.Sin(j * MathF.PI * 2f * freq / WavEncoder.DefaultSampleRate) * 0.08f * envelope;
                    }
                }
            }

            WaveSynth.Normalize(buffer, 0.45f);
            return buffer;
        }

        private static float[] BuildSwampLoop()
        {
            var buffer = WaveSynth.AllocateSeconds(6f);
            // Swamp damp wind
            WaveSynth.FillPinkNoise(buffer, 0.18f, seed: 404);
            WaveSynth.ApplyLowPass(buffer, 300f);

            // Add low frog croaks
            Random rand = new Random(202);
            for (int croak = 0; croak < 6; croak++)
            {
                float startTime = croak * 1.0f + (float)rand.NextDouble() * 0.3f;
                int startIndex = (int)(startTime * WavEncoder.DefaultSampleRate);
                if (startIndex + 12000 < buffer.Length)
                {
                    // Synthesize frog: low frequency modulated sine wave
                    for (int j = 0; j < 12000; j++)
                    {
                        float t = j / (float)WavEncoder.DefaultSampleRate;
                        float envelope = MathF.Sin(j / 12000f * MathF.PI);
                        float vibrato = MathF.Sin(t * MathF.PI * 2f * 45f); // 45Hz fast vibrato/croak
                        buffer[startIndex + j] += MathF.Sin(j * MathF.PI * 2f * 120f / WavEncoder.DefaultSampleRate) * 0.12f * envelope * (0.6f + 0.4f * vibrato);
                    }
                }
            }

            WaveSynth.Normalize(buffer, 0.40f);
            return buffer;
        }

        private static float[] BuildDesertLoop()
        {
            var buffer = WaveSynth.AllocateSeconds(8f);
            WaveSynth.FillPinkNoise(buffer, 0.35f, seed: 505);
            WaveSynth.ApplyLowPass(buffer, 350f);

            // High altitude whistle LFO
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)WavEncoder.DefaultSampleRate;
                float windSpeed = 0.4f + 0.6f * MathF.Sin(t * MathF.PI * 2f * 0.05f + MathF.Sin(t * 0.2f));
                buffer[i] *= windSpeed;
            }

            WaveSynth.Normalize(buffer, 0.40f);
            return buffer;
        }

        private static float[] BuildCaveLoop()
        {
            var buffer = WaveSynth.AllocateSeconds(10f);
            // Deep background rumble (brown/low noise)
            WaveSynth.FillPinkNoise(buffer, 0.25f, seed: 606);
            WaveSynth.ApplyLowPass(buffer, 120f);

            // Water drips
            Random rand = new Random(707);
            for (int drip = 0; drip < 8; drip++)
            {
                float startTime = drip * 1.2f + (float)rand.NextDouble() * 0.4f;
                int startIndex = (int)(startTime * WavEncoder.DefaultSampleRate);
                if (startIndex + 6000 < buffer.Length)
                {
                    // High pitch drip: fast decaying sine
                    for (int j = 0; j < 6000; j++)
                    {
                        float t = j / (float)WavEncoder.DefaultSampleRate;
                        float freq = 1200f * MathF.Exp(-t * 22f); // rapidly decaying frequency
                        float envelope = MathF.Exp(-t * 20f);
                        buffer[startIndex + j] += MathF.Sin(j * MathF.PI * 2f * freq / WavEncoder.DefaultSampleRate) * 0.15f * envelope;
                    }
                }
            }

            WaveSynth.Normalize(buffer, 0.40f);
            return buffer;
        }
    }
}
