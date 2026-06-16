using System;
using System.Numerics;
using Autonocraft.Core;

namespace Autonocraft.Engine.Animation
{
    public enum SwingKind
    {
        Mine,
        Melee,
        Place
    }

    public sealed class InteractionAnimator
    {
        private const float SwingDuration = 0.25f;
        private const float LandPunchDuration = 0.12f;
        private const float DamageShakeDuration = 0.2f;
        private const float DamageFlashDuration = 0.3f;
        private const float InvalidShakeDuration = 0.18f;
        private const float MeleeCrosshairDuration = 0.15f;

        private float _swingTimer;
        private SwingKind _swingKind;
        private bool _isMining;
        private float _walkBobPhase;
        private float _landPunchTimer;
        private float _damageShakeTimer;
        private float _damageFlashTimer;
        private float _damageFlashStrength = 1f;
        private float _invalidShakeTimer;
        private float _pitchRecoil;
        private float _pitchRecoilDecay;
        private float _meleeCrosshairTimer;
        private float _animTime;
        private float _damageShakeSeed;
        private float _lastSwingPhase;

        public float AnimTime => _animTime;
        public bool SwingActive => _swingTimer > 0f;
        public float SwingPhase => _swingTimer > 0f ? 1f - _swingTimer / SwingDuration : 0f;
        public SwingKind CurrentSwingKind => _swingKind;
        public bool IsMining => _isMining;
        public float WalkBobOffsetY { get; private set; }
        public float DamageFlashAlpha { get; private set; }
        public float InvalidShakePhase { get; private set; }
        public float PitchRecoil => _pitchRecoil;
        public bool MeleeCrosshairActive => _meleeCrosshairTimer > 0f;
        public float MeleeCrosshairAlpha => _meleeCrosshairTimer > 0f
            ? Math.Clamp(_meleeCrosshairTimer / MeleeCrosshairDuration, 0f, 1f)
            : 0f;

        public Vector3 PositionOffset { get; private set; }
        public float HotbarWiggleScale { get; private set; } = 1f;

        public bool SwingPeakCrossed { get; private set; }

        public void SetMining(bool mining)
        {
            _isMining = mining;
            if (mining && _swingTimer <= 0f)
            {
                TriggerSwing(SwingKind.Mine);
            }
        }

        public void TriggerSwing(SwingKind kind)
        {
            _swingKind = kind;
            _swingTimer = SwingDuration;

            if (kind == SwingKind.Melee)
            {
                _pitchRecoil = 4f;
                _pitchRecoilDecay = 12f;
                _meleeCrosshairTimer = MeleeCrosshairDuration;
            }
            else if (kind == SwingKind.Place)
            {
                _pitchRecoil = 2f;
                _pitchRecoilDecay = 10f;
            }
            else
            {
                _pitchRecoil = 1.5f;
                _pitchRecoilDecay = 8f;
            }
        }

        public void TriggerLand(float fallDistance)
        {
            if (fallDistance < 0.5f)
            {
                return;
            }

            _landPunchTimer = LandPunchDuration;
        }

        public void TriggerDamage(float strength = 1f)
        {
            _damageShakeTimer = DamageShakeDuration;
            _damageFlashTimer = DamageFlashDuration;
            _damageFlashStrength = Math.Clamp(strength, 0.4f, 1.5f);
            _damageShakeSeed = _animTime;
        }

        public void TriggerInvalidAction()
        {
            _invalidShakeTimer = InvalidShakeDuration;
            HotbarWiggleScale = 1.12f;
        }

        public void Update(float deltaTime, Player player)
        {
            _animTime += deltaTime;
            SwingPeakCrossed = false;

            float prevSwingPhase = SwingPhase;

            if (_swingTimer > 0f)
            {
                _swingTimer = Math.Max(0f, _swingTimer - deltaTime);
            }

            float newSwingPhase = SwingPhase;
            if (prevSwingPhase < 0.5f && newSwingPhase >= 0.5f && SwingActive)
            {
                SwingPeakCrossed = true;
            }

            if (_isMining && _swingTimer <= 0f)
            {
                TriggerSwing(SwingKind.Mine);
            }

            if (_landPunchTimer > 0f)
            {
                _landPunchTimer = Math.Max(0f, _landPunchTimer - deltaTime);
            }

            if (_damageShakeTimer > 0f)
            {
                _damageShakeTimer = Math.Max(0f, _damageShakeTimer - deltaTime);
            }

            if (_damageFlashTimer > 0f)
            {
                _damageFlashTimer = Math.Max(0f, _damageFlashTimer - deltaTime);
                DamageFlashAlpha = (_damageFlashTimer / DamageFlashDuration) * _damageFlashStrength;
            }
            else
            {
                DamageFlashAlpha = 0f;
            }

            if (_invalidShakeTimer > 0f)
            {
                _invalidShakeTimer = Math.Max(0f, _invalidShakeTimer - deltaTime);
                InvalidShakePhase = (_invalidShakeTimer / InvalidShakeDuration) * MathF.Sin(_animTime * 48f);
            }
            else
            {
                InvalidShakePhase = 0f;
            }

            if (_meleeCrosshairTimer > 0f)
            {
                _meleeCrosshairTimer = Math.Max(0f, _meleeCrosshairTimer - deltaTime);
            }

            if (_pitchRecoil > 0f)
            {
                _pitchRecoil = Math.Max(0f, _pitchRecoil - _pitchRecoilDecay * deltaTime);
            }

            HotbarWiggleScale = Tween.SmoothDamp(HotbarWiggleScale, 1f, 14f, deltaTime);

            float horizontalSpeed = MathF.Sqrt(player.Velocity.X * player.Velocity.X + player.Velocity.Z * player.Velocity.Z);
            if (player.IsGrounded && !player.CreativeMode && horizontalSpeed > 0.5f)
            {
                _walkBobPhase += deltaTime * 4f * MathF.PI * 2f;
                WalkBobOffsetY = MathF.Sin(_walkBobPhase) * 0.03f * Math.Clamp(horizontalSpeed / Player.WalkSpeed, 0f, 1f);
            }
            else
            {
                WalkBobOffsetY = Tween.SmoothDamp(WalkBobOffsetY, 0f, 8f, deltaTime);
            }

            float landOffset = 0f;
            if (_landPunchTimer > 0f)
            {
                float t = 1f - _landPunchTimer / LandPunchDuration;
                landOffset = -0.06f * MathF.Sin(t * MathF.PI);
            }

            float shakeX = 0f;
            float shakeY = 0f;
            if (_damageShakeTimer > 0f)
            {
                float shakeT = _damageShakeTimer / DamageShakeDuration;
                float amp = 0.025f * shakeT;
                shakeX = MathF.Sin((_animTime - _damageShakeSeed) * 80f) * amp;
                shakeY = MathF.Cos((_animTime - _damageShakeSeed) * 65f) * amp * 0.6f;
            }

            float miningBob = _isMining ? MathF.Sin(_animTime * 10f) * 0.008f : 0f;

            PositionOffset = new Vector3(shakeX, WalkBobOffsetY + landOffset + miningBob, 0f);
            _lastSwingPhase = newSwingPhase;
        }

        public float GetHeldItemSwingDegrees()
        {
            if (!SwingActive)
            {
                return _isMining ? MathF.Sin(_animTime * 10f) * 3f : 0f;
            }

            float t = 1f - SwingPhase;
            return -35f * Tween.EaseOut(t);
        }

        public float GetHeldItemOffsetY()
        {
            if (_isMining)
            {
                return MathF.Sin(_animTime * 10f) * 4f;
            }

            return 0f;
        }
    }
}
