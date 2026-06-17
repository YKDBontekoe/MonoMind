namespace Autonocraft.Engine.Audio
{
    public static class ProceduralMusic
    {
        public static Microsoft.Xna.Framework.Audio.SoundEffect Build(MusicState state)
        {
            float[] samples = state switch
            {
                MusicState.Menu => BuildMenuTheme(),
                MusicState.Gameplay => BuildGameplayTheme(),
                _ => BuildMenuTheme()
            };

            return WavEncoder.ToSoundEffect(samples);
        }

        private static float[] BuildMenuTheme()
        {
            var buffer = WaveSynth.AllocateSeconds(16f);
            float[] freqs = { 130.81f, 164.81f, 196f };
            for (int layer = 0; layer < freqs.Length; layer++)
            {
                var voice = WaveSynth.AllocateSeconds(16f);
                float detune = freqs[layer] + (layer - 1) * 1.5f;
                WaveSynth.FillSine(voice, detune, 0.12f);
                for (int i = 0; i < voice.Length; i++)
                {
                    float t = i / (float)WavEncoder.DefaultSampleRate;
                    float swell = 0.55f + 0.45f * MathF.Sin(t * MathF.PI * 2f * 0.05f + layer);
                    voice[i] *= swell;
                }

                WaveSynth.Mix(buffer, voice);
            }

            WaveSynth.Normalize(buffer, 0.5f);
            return buffer;
        }

        private static float[] BuildGameplayTheme()
        {
            var buffer = WaveSynth.AllocateSeconds(12f);
            float[] notes = { 392f, 440f, 494f, 523f, 587f, 659f, 587f, 523f };
            float noteDuration = 12f / notes.Length;

            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * noteDuration * WavEncoder.DefaultSampleRate);
                int length = Math.Max(1, (int)(noteDuration * WavEncoder.DefaultSampleRate * 0.85f));
                var voice = new float[length];
                WaveSynth.FillSine(voice, notes[n], 0.08f);
                WaveSynth.ApplyEnvelope(voice, 0.08f, 0.2f, 0.5f, 0.35f);

                for (int i = 0; i < voice.Length && start + i < buffer.Length; i++)
                {
                    buffer[start + i] += voice[i];
                }
            }

            var pad = WaveSynth.AllocateSeconds(12f);
            WaveSynth.FillSine(pad, 196f, 0.04f);
            WaveSynth.FillSine(pad, 246.94f, 0.03f);
            WaveSynth.Mix(buffer, pad, 0.8f);
            WaveSynth.Normalize(buffer, 0.42f);
            return buffer;
        }
    }
}
