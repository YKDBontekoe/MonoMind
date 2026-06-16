using System;

namespace Autonocraft.Engine.Audio
{
    public static class WaveSynth
    {
        private static readonly Random Rng = new();

        public static float[] AllocateSeconds(float seconds, int sampleRate = WavEncoder.DefaultSampleRate)
        {
            int count = Math.Max(1, (int)(seconds * sampleRate));
            return new float[count];
        }

        public static void FillSine(float[] buffer, float frequency, float amplitude, int sampleRate = WavEncoder.DefaultSampleRate)
        {
            float phase = 0f;
            float step = (MathF.PI * 2f * frequency) / sampleRate;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] += MathF.Sin(phase) * amplitude;
                phase += step;
            }
        }

        public static void FillDescendingSine(
            float[] buffer,
            float startFrequency,
            float endFrequency,
            float amplitude,
            int sampleRate = WavEncoder.DefaultSampleRate)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = buffer.Length <= 1 ? 1f : i / (float)(buffer.Length - 1);
                float frequency = startFrequency + (endFrequency - startFrequency) * t;
                float phase = (MathF.PI * 2f * frequency * i) / sampleRate;
                buffer[i] += MathF.Sin(phase) * amplitude;
            }
        }

        public static void FillNoise(float[] buffer, float amplitude, int seed = 0)
        {
            var rng = seed == 0 ? Rng : new Random(seed);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] += ((float)rng.NextDouble() * 2f - 1f) * amplitude;
            }
        }

        public static void FillPinkNoise(float[] buffer, float amplitude, int seed = 0)
        {
            var rng = seed == 0 ? Rng : new Random(seed);
            float b0 = 0f, b1 = 0f, b2 = 0f, b3 = 0f, b4 = 0f, b5 = 0f, b6 = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                b0 = 0.99886f * b0 + white * 0.0555179f;
                b1 = 0.99332f * b1 + white * 0.0750759f;
                b2 = 0.96900f * b2 + white * 0.1538520f;
                b3 = 0.86650f * b3 + white * 0.3104856f;
                b4 = 0.55000f * b4 + white * 0.5329522f;
                b5 = -0.7616f * b5 - white * 0.0168980f;
                float pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f;
                b6 = white * 0.115926f;
                buffer[i] += pink * amplitude * 0.11f;
            }
        }

        public static void ApplyEnvelope(float[] buffer, float attack, float decay, float sustain, float release)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            int attackSamples = (int)(attack * buffer.Length);
            int decaySamples = (int)(decay * buffer.Length);
            int releaseSamples = (int)(release * buffer.Length);
            int sustainEnd = Math.Max(0, buffer.Length - releaseSamples);

            for (int i = 0; i < buffer.Length; i++)
            {
                float env;
                if (i < attackSamples && attackSamples > 0)
                {
                    env = i / (float)attackSamples;
                }
                else if (i < attackSamples + decaySamples && decaySamples > 0)
                {
                    float t = (i - attackSamples) / (float)decaySamples;
                    env = 1f - (1f - sustain) * t;
                }
                else if (i < sustainEnd)
                {
                    env = sustain;
                }
                else if (releaseSamples > 0)
                {
                    float t = (i - sustainEnd) / (float)releaseSamples;
                    env = sustain * (1f - Math.Clamp(t, 0f, 1f));
                }
                else
                {
                    env = sustain;
                }

                buffer[i] *= env;
            }
        }

        public static void ApplyLowPass(float[] buffer, float cutoffHz, int sampleRate = WavEncoder.DefaultSampleRate)
        {
            float rc = 1f / (MathF.PI * 2f * cutoffHz);
            float dt = 1f / sampleRate;
            float alpha = dt / (rc + dt);
            float prev = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                prev = prev + alpha * (buffer[i] - prev);
                buffer[i] = prev;
            }
        }

        public static void ApplyRisingLowPass(float[] buffer, float startCutoff, float endCutoff, int sampleRate = WavEncoder.DefaultSampleRate)
        {
            float prev = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = buffer.Length <= 1 ? 1f : i / (float)(buffer.Length - 1);
                float cutoff = startCutoff + (endCutoff - startCutoff) * t;
                float rc = 1f / (MathF.PI * 2f * cutoff);
                float dt = 1f / sampleRate;
                float alpha = dt / (rc + dt);
                prev = prev + alpha * (buffer[i] - prev);
                buffer[i] = prev;
            }
        }

        public static void Mix(float[] target, float[] source, float gain = 1f)
        {
            int count = Math.Min(target.Length, source.Length);
            for (int i = 0; i < count; i++)
            {
                target[i] += source[i] * gain;
            }
        }

        public static void Normalize(float[] buffer, float peak = 0.9f)
        {
            float max = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                max = MathF.Max(max, MathF.Abs(buffer[i]));
            }

            if (max <= 0.0001f)
            {
                return;
            }

            float scale = peak / max;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] *= scale;
            }
        }

        public static float RandomPitch(float center = 1f, float spread = 0.08f)
        {
            return center + ((float)Rng.NextDouble() * 2f - 1f) * spread;
        }
    }
}
