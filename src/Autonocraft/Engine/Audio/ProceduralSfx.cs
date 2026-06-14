using Autonocraft.Domain.World;

namespace Autonocraft.Engine.Audio
{
    public static class ProceduralSfx
    {
        public static Microsoft.Xna.Framework.Audio.SoundEffect Build(SfxKind kind)
        {
            float[] samples = kind switch
            {
                SfxKind.Mine => BuildMine(),
                SfxKind.Place => BuildPlace(),
                SfxKind.MeleeHit => BuildMeleeHit(),
                SfxKind.PlayerHurt => BuildPlayerHurt(),
                SfxKind.AnimalDeath => BuildAnimalDeath(),
                SfxKind.ToolBreak => BuildToolBreak(),
                SfxKind.WaterSplash => BuildWaterSplash(),
                SfxKind.Jump => BuildJump(),
                SfxKind.Land => BuildLand(),
                SfxKind.Footstep => BuildFootstep(1f),
                SfxKind.UiClick => BuildUiClick(),
                SfxKind.Discovery => BuildDiscovery(),
                SfxKind.Invalid => BuildInvalid(),
                _ => BuildUiClick()
            };

            return WavEncoder.ToSoundEffect(samples);
        }

        public static float FootstepPitchForBlock(BlockType blockType)
        {
            return blockType switch
            {
                BlockType.Stone or BlockType.Cobblestone or BlockType.Brick or BlockType.IronBlock
                    or BlockType.GoldBlock or BlockType.CoalOre or BlockType.IronOre or BlockType.GoldOre
                    or BlockType.MossStone or BlockType.Sandstone => 1.15f,
                BlockType.Grass or BlockType.Dirt or BlockType.Mud or BlockType.Snow => 0.85f,
                BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog or BlockType.WillowLog
                    or BlockType.PalmLog or BlockType.OakPlank or BlockType.BirchPlank or BlockType.PinePlank
                    or BlockType.HayBale => 0.95f,
                BlockType.Sand or BlockType.Gravel => 0.9f,
                BlockType.Glass or BlockType.Ice => 1.25f,
                _ => 1f
            };
        }

        public static float MinePitchForBlock(BlockType blockType)
        {
            return blockType switch
            {
                BlockType.Stone or BlockType.Cobblestone or BlockType.CoalOre or BlockType.IronOre
                    or BlockType.GoldOre or BlockType.IronBlock or BlockType.GoldBlock => 0.85f,
                BlockType.Grass or BlockType.Dirt or BlockType.Sand => 1.1f,
                BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog => 1.05f,
                _ => 1f
            };
        }

        private static float[] BuildMine()
        {
            var buffer = WaveSynth.AllocateSeconds(0.08f);
            var noise = WaveSynth.AllocateSeconds(0.08f);
            WaveSynth.FillNoise(noise, 0.55f, seed: 11);
            WaveSynth.ApplyLowPass(noise, 900f);
            WaveSynth.Mix(buffer, noise);
            WaveSynth.FillSine(buffer, 120f, 0.35f);
            WaveSynth.ApplyEnvelope(buffer, 0.02f, 0.2f, 0.2f, 0.5f);
            WaveSynth.Normalize(buffer, 0.85f);
            return buffer;
        }

        private static float[] BuildPlace()
        {
            var buffer = WaveSynth.AllocateSeconds(0.04f);
            WaveSynth.FillSine(buffer, 800f, 0.5f);
            WaveSynth.ApplyEnvelope(buffer, 0.01f, 0.15f, 0.05f, 0.6f);
            WaveSynth.Normalize(buffer, 0.7f);
            return buffer;
        }

        private static float[] BuildMeleeHit()
        {
            var buffer = WaveSynth.AllocateSeconds(0.06f);
            var noise = WaveSynth.AllocateSeconds(0.06f);
            WaveSynth.FillNoise(noise, 0.45f, seed: 22);
            WaveSynth.Mix(buffer, noise);
            WaveSynth.FillSine(buffer, 200f, 0.4f);
            WaveSynth.ApplyEnvelope(buffer, 0.01f, 0.25f, 0.1f, 0.5f);
            WaveSynth.Normalize(buffer, 0.9f);
            return buffer;
        }

        private static float[] BuildPlayerHurt()
        {
            var buffer = WaveSynth.AllocateSeconds(0.15f);
            WaveSynth.FillDescendingSine(buffer, 400f, 200f, 0.55f);
            WaveSynth.ApplyEnvelope(buffer, 0.02f, 0.3f, 0.4f, 0.4f);
            WaveSynth.Normalize(buffer, 0.85f);
            return buffer;
        }

        private static float[] BuildAnimalDeath()
        {
            var buffer = WaveSynth.AllocateSeconds(0.2f);
            WaveSynth.FillDescendingSine(buffer, 320f, 80f, 0.5f);
            var noise = WaveSynth.AllocateSeconds(0.12f);
            WaveSynth.FillNoise(noise, 0.2f, seed: 33);
            WaveSynth.Mix(buffer, noise, 0.5f);
            WaveSynth.ApplyEnvelope(buffer, 0.02f, 0.35f, 0.3f, 0.5f);
            WaveSynth.Normalize(buffer, 0.8f);
            return buffer;
        }

        private static float[] BuildToolBreak()
        {
            var buffer = WaveSynth.AllocateSeconds(0.1f);
            var noise = WaveSynth.AllocateSeconds(0.1f);
            WaveSynth.FillNoise(noise, 0.7f, seed: 44);
            WaveSynth.ApplyLowPass(noise, 1200f);
            WaveSynth.Mix(buffer, noise);
            WaveSynth.FillSine(buffer, 180f, 0.25f);
            WaveSynth.ApplyEnvelope(buffer, 0.01f, 0.2f, 0.15f, 0.55f);
            WaveSynth.Normalize(buffer, 0.85f);
            return buffer;
        }

        private static float[] BuildWaterSplash()
        {
            var buffer = WaveSynth.AllocateSeconds(0.2f);
            var noise = WaveSynth.AllocateSeconds(0.2f);
            WaveSynth.FillNoise(noise, 0.5f, seed: 55);
            WaveSynth.ApplyRisingLowPass(noise, 400f, 1800f);
            WaveSynth.Mix(buffer, noise);
            WaveSynth.ApplyEnvelope(buffer, 0.02f, 0.25f, 0.35f, 0.45f);
            WaveSynth.Normalize(buffer, 0.75f);
            return buffer;
        }

        private static float[] BuildJump()
        {
            var buffer = WaveSynth.AllocateSeconds(0.08f);
            WaveSynth.FillDescendingSine(buffer, 500f, 300f, 0.35f);
            WaveSynth.ApplyEnvelope(buffer, 0.02f, 0.2f, 0.2f, 0.4f);
            WaveSynth.Normalize(buffer, 0.65f);
            return buffer;
        }

        private static float[] BuildLand()
        {
            var buffer = WaveSynth.AllocateSeconds(0.07f);
            var noise = WaveSynth.AllocateSeconds(0.07f);
            WaveSynth.FillNoise(noise, 0.4f, seed: 66);
            WaveSynth.ApplyLowPass(noise, 500f);
            WaveSynth.Mix(buffer, noise);
            WaveSynth.FillSine(buffer, 90f, 0.35f);
            WaveSynth.ApplyEnvelope(buffer, 0.01f, 0.25f, 0.15f, 0.5f);
            WaveSynth.Normalize(buffer, 0.8f);
            return buffer;
        }

        private static float[] BuildFootstep(float pitchScale)
        {
            var buffer = WaveSynth.AllocateSeconds(0.05f);
            var noise = WaveSynth.AllocateSeconds(0.05f);
            WaveSynth.FillNoise(noise, 0.35f, seed: 77);
            WaveSynth.ApplyLowPass(noise, 600f * pitchScale);
            WaveSynth.Mix(buffer, noise);
            WaveSynth.FillSine(buffer, 110f * pitchScale, 0.2f);
            WaveSynth.ApplyEnvelope(buffer, 0.01f, 0.2f, 0.1f, 0.55f);
            WaveSynth.Normalize(buffer, 0.55f);
            return buffer;
        }

        private static float[] BuildUiClick()
        {
            var buffer = WaveSynth.AllocateSeconds(0.03f);
            WaveSynth.FillSine(buffer, 1200f, 0.35f);
            WaveSynth.ApplyEnvelope(buffer, 0.05f, 0.2f, 0.05f, 0.5f);
            WaveSynth.Normalize(buffer, 0.5f);
            return buffer;
        }

        private static float[] BuildDiscovery()
        {
            var buffer = WaveSynth.AllocateSeconds(0.25f);
            WaveSynth.FillSine(buffer, 523f, 0.3f);
            var layer = WaveSynth.AllocateSeconds(0.25f);
            WaveSynth.FillSine(layer, 659f, 0.25f);
            WaveSynth.ApplyEnvelope(layer, 0.05f, 0.1f, 0.7f, 0.3f);
            WaveSynth.Mix(buffer, layer);
            var layer2 = WaveSynth.AllocateSeconds(0.2f);
            WaveSynth.FillSine(layer2, 784f, 0.2f);
            WaveSynth.ApplyEnvelope(layer2, 0.1f, 0.15f, 0.5f, 0.35f);
            WaveSynth.Mix(buffer, layer2, 0.8f);
            WaveSynth.Normalize(buffer, 0.7f);
            return buffer;
        }

        private static float[] BuildInvalid()
        {
            var buffer = WaveSynth.AllocateSeconds(0.08f);
            WaveSynth.FillSine(buffer, 220f, 0.35f);
            WaveSynth.ApplyEnvelope(buffer, 0.02f, 0.2f, 0.2f, 0.45f);
            WaveSynth.Normalize(buffer, 0.6f);
            return buffer;
        }
    }
}
