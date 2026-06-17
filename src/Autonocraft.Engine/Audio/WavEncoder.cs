using System;
using System.IO;
using Microsoft.Xna.Framework.Audio;

namespace Autonocraft.Engine.Audio
{
    public static class WavEncoder
    {
        public const int DefaultSampleRate = 22050;

        public static SoundEffect ToSoundEffect(float[] samples, int sampleRate = DefaultSampleRate)
        {
            using var stream = ToWavStream(samples, sampleRate);
            return SoundEffect.FromStream(stream);
        }

        public static MemoryStream ToWavStream(float[] samples, int sampleRate = DefaultSampleRate)
        {
            int sampleCount = samples.Length;
            int dataSize = sampleCount * 2;
            int fileSize = 44 + dataSize;

            var stream = new MemoryStream(fileSize);
            using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);

            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(fileSize - 8);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            for (int i = 0; i < sampleCount; i++)
            {
                float clamped = Math.Clamp(samples[i], -1f, 1f);
                short pcm = (short)(clamped * short.MaxValue);
                writer.Write(pcm);
            }

            stream.Position = 0;
            return stream;
        }
    }
}
