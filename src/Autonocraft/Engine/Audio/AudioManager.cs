using System;
using System.Collections.Generic;
using Autonocraft.Core;
using Autonocraft.Domain.World;
using Autonocraft.World;
using Microsoft.Xna.Framework.Audio;

namespace Autonocraft.Engine.Audio
{
    public sealed class AudioManager : IDisposable
    {
        private const int SfxPoolSize = 8;
        private const float CrossfadeSeconds = 2f;
        private const float DuckMultiplier = 0.3f;
        private const float WaterProximityRadius = 6f;

        private readonly bool _enabled;
        private readonly Dictionary<SfxKind, SoundEffect> _sfx = new();
        private readonly SoundEffectInstance?[] _sfxPool = new SoundEffectInstance?[SfxPoolSize];
        private readonly Dictionary<AmbientKind, SoundEffect> _ambient = new();
        private readonly Dictionary<AmbientKind, SoundEffectInstance> _ambientInstances = new();
        private readonly Dictionary<MusicState, SoundEffect> _music = new();
        private readonly Dictionary<MusicState, SoundEffectInstance> _musicInstances = new();

        private MusicState _currentMusic = MusicState.None;
        private MusicState _targetMusic = MusicState.None;
        private float _musicCrossfade;
        private bool _ducked;
        private float _waterTarget;
        private float _windTarget;
        private float _waterVolume;
        private float _windVolume;

        public bool Enabled => _enabled;

        public float MasterVolume { get; set; } = 1f;
        public float SfxVolume { get; set; } = 1f;
        public float AmbientVolume { get; set; } = 0.6f;
        public float MusicVolume { get; set; } = 0.5f;
        public bool MuteAudio { get; set; }

        public AudioManager(bool enabled = true)
        {
            _enabled = enabled;
        }

        public void Initialize()
        {
            if (!_enabled)
            {
                return;
            }

            foreach (SfxKind kind in Enum.GetValues<SfxKind>())
            {
                _sfx[kind] = ProceduralSfx.Build(kind);
            }

            foreach (AmbientKind kind in Enum.GetValues<AmbientKind>())
            {
                var effect = ProceduralAmbient.Build(kind);
                _ambient[kind] = effect;
                var instance = effect.CreateInstance();
                instance.IsLooped = true;
                instance.Volume = 0f;
                _ambientInstances[kind] = instance;
            }

            foreach (MusicState state in Enum.GetValues<MusicState>())
            {
                if (state == MusicState.None)
                {
                    continue;
                }

                var effect = ProceduralMusic.Build(state);
                _music[state] = effect;
                var instance = effect.CreateInstance();
                instance.IsLooped = true;
                instance.Volume = 0f;
                _musicInstances[state] = instance;
            }

            ApplyMasterVolumes();
        }

        public void ApplySettings(GameSettings settings)
        {
            MasterVolume = settings.MasterVolume;
            SfxVolume = settings.SfxVolume;
            AmbientVolume = settings.AmbientVolume;
            MusicVolume = settings.MusicVolume;
            MuteAudio = settings.MuteAudio;
            ApplyMasterVolumes();
        }

        public void PlaySfx(SfxKind kind, float pitch = 1f, float volume = 1f)
        {
            if (!_enabled || MuteAudio || !_sfx.TryGetValue(kind, out var effect))
            {
                return;
            }

            float finalPitch = ToSoundPitch(pitch * WaveSynth.RandomPitch());
            float finalVolume = volume * SfxVolume * MasterVolume;

            try
            {
                PlaySfxInstance(effect, finalPitch, finalVolume);
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        private void PlaySfxInstance(SoundEffect effect, float finalPitch, float finalVolume)
        {
            finalPitch = Math.Clamp(finalPitch, -1f, 1f);
            finalVolume = Math.Clamp(finalVolume, 0f, 1f);

            for (int i = 0; i < SfxPoolSize; i++)
            {
                var slot = _sfxPool[i];
                if (slot == null || slot.State == SoundState.Stopped)
                {
                    slot?.Dispose();
                    slot = effect.CreateInstance();
                    slot.Pitch = finalPitch;
                    slot.Volume = finalVolume;
                    slot.Play();
                    _sfxPool[i] = slot;
                    return;
                }
            }

            _sfxPool[0]?.Stop();
            _sfxPool[0]?.Dispose();
            var fallback = effect.CreateInstance();
            fallback.Pitch = finalPitch;
            fallback.Volume = finalVolume;
            fallback.Play();
            _sfxPool[0] = fallback;
        }

        public void PlaySfxForBlock(SfxKind kind, BlockType blockType)
        {
            float pitch = kind switch
            {
                SfxKind.Mine => ProceduralSfx.MinePitchForBlock(blockType),
                SfxKind.Footstep => ProceduralSfx.FootstepPitchForBlock(blockType),
                _ => 1f
            };
            PlaySfx(kind, pitch);
        }

        public void SetMusicState(MusicState state)
        {
            if (!_enabled)
            {
                return;
            }

            if (_targetMusic == state)
            {
                return;
            }

            _targetMusic = state;
            _musicCrossfade = 0f;

            if (state == MusicState.None)
            {
                StopAllMusic();
                _currentMusic = MusicState.None;
                return;
            }

            if (!_musicInstances.TryGetValue(state, out var instance))
            {
                return;
            }

            if (instance.State == SoundState.Stopped)
            {
                instance.Volume = 0f;
                instance.Play();
            }
        }

        public void SetDucked(bool ducked)
        {
            _ducked = ducked;
        }

        public void Update(float deltaTime, GameState gameState, VoxelWorld? world, Player? player)
        {
            if (!_enabled)
            {
                return;
            }

            ApplyMasterVolumes();
            UpdateMusicCrossfade(deltaTime);
            UpdateAmbient(gameState, world, player, deltaTime);
        }

        private void UpdateMusicCrossfade(float deltaTime)
        {
            if (_targetMusic == MusicState.None && _currentMusic == MusicState.None)
            {
                return;
            }

            if (_targetMusic != _currentMusic)
            {
                _musicCrossfade += deltaTime;
                float t = Math.Clamp(_musicCrossfade / CrossfadeSeconds, 0f, 1f);
                float duck = _ducked ? DuckMultiplier : 1f;
                float targetVol = MusicVolume * MasterVolume * duck;

                if (_currentMusic != MusicState.None && _musicInstances.TryGetValue(_currentMusic, out var outgoing))
                {
                    outgoing.Volume = targetVol * (1f - t);
                    if (t >= 1f && outgoing.State == SoundState.Playing)
                    {
                        outgoing.Stop();
                    }
                }

                if (_targetMusic != MusicState.None && _musicInstances.TryGetValue(_targetMusic, out var incoming))
                {
                    if (incoming.State == SoundState.Stopped)
                    {
                        incoming.Play();
                    }

                    incoming.Volume = targetVol * t;
                }

                if (t >= 1f)
                {
                    _currentMusic = _targetMusic;
                    _musicCrossfade = 0f;
                }
            }
            else if (_currentMusic != MusicState.None && _musicInstances.TryGetValue(_currentMusic, out var current))
            {
                float duck = _ducked ? DuckMultiplier : 1f;
                current.Volume = MusicVolume * MasterVolume * duck;
            }
        }

        private void UpdateAmbient(GameState gameState, VoxelWorld? world, Player? player, float deltaTime)
        {
            float duck = _ducked ? DuckMultiplier : 1f;
            bool playing = gameState == GameState.Playing && world != null && player != null;

            if (playing)
            {
                _waterTarget = player!.InWater || IsWaterNearby(world!, player.Position) ? 1f : 0f;
                _windTarget = IsOutdoors(world!, player.Position) ? 0.7f : 0.15f;
            }
            else
            {
                _waterTarget = 0f;
                _windTarget = gameState == GameState.MainMenu || gameState == GameState.NewWorldSetup ? 0.25f : 0f;
            }

            float fadeSpeed = 2.5f * deltaTime;
            _waterVolume = MoveToward(_waterVolume, _waterTarget, fadeSpeed);
            _windVolume = MoveToward(_windVolume, _windTarget, fadeSpeed);

            SetAmbientVolume(AmbientKind.Water, _waterVolume * AmbientVolume * MasterVolume * duck);
            SetAmbientVolume(AmbientKind.Wind, _windVolume * AmbientVolume * MasterVolume * duck);
        }

        private bool IsWaterNearby(VoxelWorld world, System.Numerics.Vector3 position)
        {
            int px = (int)MathF.Floor(position.X);
            int py = (int)MathF.Floor(position.Y);
            int pz = (int)MathF.Floor(position.Z);
            int radius = (int)MathF.Ceiling(WaterProximityRadius);

            for (int dy = -1; dy <= 2; dy++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dz * dz + dy * dy > WaterProximityRadius * WaterProximityRadius)
                        {
                            continue;
                        }

                        if (world.GetBlock(px + dx, py + dy, pz + dz).IsWater())
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void SetAmbientVolume(AmbientKind kind, float volume)
        {
            if (!_ambientInstances.TryGetValue(kind, out var instance))
            {
                return;
            }

            if (volume <= 0.001f)
            {
                if (instance.State == SoundState.Playing)
                {
                    instance.Stop();
                }

                instance.Volume = 0f;
                return;
            }

            if (instance.State == SoundState.Stopped)
            {
                instance.Play();
            }

            instance.Volume = Math.Clamp(volume, 0f, 1f);
        }

        private void StopAllMusic()
        {
            foreach (var instance in _musicInstances.Values)
            {
                instance.Stop();
                instance.Volume = 0f;
            }
        }

        private void ApplyMasterVolumes()
        {
            if (!_enabled)
            {
                return;
            }

            SoundEffect.MasterVolume = MuteAudio ? 0f : MasterVolume;
        }

        private static bool IsOutdoors(VoxelWorld world, System.Numerics.Vector3 position)
        {
            int x = (int)MathF.Floor(position.X);
            int y = (int)MathF.Floor(position.Y + Core.Player.EyeHeight);
            int z = (int)MathF.Floor(position.Z);

            int airCount = 0;
            for (int dy = 1; dy <= 12; dy++)
            {
                var block = world.GetBlock(x, y + dy, z);
                if (block.IsTransparent() && !block.IsFluid())
                {
                    airCount++;
                }
            }

            return airCount >= 8;
        }

        private static float ToSoundPitch(float multiplier)
        {
            if (multiplier <= 0f)
            {
                return 0f;
            }

            return Math.Clamp(MathF.Log2(multiplier), -1f, 1f);
        }

        private static float MoveToward(float current, float target, float maxDelta)
        {
            if (current < target)
            {
                return Math.Min(current + maxDelta, target);
            }

            return Math.Max(current - maxDelta, target);
        }

        public void Dispose()
        {
            if (!_enabled)
            {
                return;
            }

            foreach (var slot in _sfxPool)
            {
                slot?.Dispose();
            }

            foreach (var instance in _ambientInstances.Values)
            {
                instance.Dispose();
            }

            foreach (var instance in _musicInstances.Values)
            {
                instance.Dispose();
            }

            foreach (var effect in _sfx.Values)
            {
                effect.Dispose();
            }

            foreach (var effect in _ambient.Values)
            {
                effect.Dispose();
            }

            foreach (var effect in _music.Values)
            {
                effect.Dispose();
            }
        }
    }
}
