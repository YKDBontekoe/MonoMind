using System;
using System.Numerics;
using Autonocraft.Domain.Rendering;

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

        public void Update(float deltaTime, IPlayerMotionView player)
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
            float motionPulse = player.IsGrounded && !player.CreativeMode
                ? Tween.Pulse(_animTime + horizontalSpeed * 0.08f, Math.Max(1.1f, horizontalSpeed * 0.75f))
                : 0f;
            if (player.IsGrounded && !player.CreativeMode && horizontalSpeed > 0.5f)
            {
                _walkBobPhase += deltaTime * 4f * MathF.PI * 2f;
                WalkBobOffsetY = MathF.Sin(_walkBobPhase) * 0.03f * Math.Clamp(horizontalSpeed / PlayerConstants.WalkSpeed, 0f, 1f);
            }
            else
            {
                WalkBobOffsetY = Tween.SmoothDamp(WalkBobOffsetY, motionPulse * 0.01f, 8f, deltaTime);
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

            float swingPulse = GetSwingPulse();
            float miningBob = _isMining ? MathF.Sin(_animTime * 10f) * 0.008f : 0f;
            float swingLeanX = swingPulse * GetSwingLeanX();
            float swingLeanZ = swingPulse * GetSwingLeanZ();
            float swingLeanY = swingPulse * GetSwingLeanY();
            if (_invalidShakeTimer > 0f)
            {
                swingLeanX += InvalidShakePhase * 0.02f;
            }

            PositionOffset = new Vector3(
                shakeX + swingLeanX,
                WalkBobOffsetY + landOffset + miningBob + swingLeanY + shakeY,
                swingLeanZ);
        }

        public float GetHeldItemSwingDegrees()
        {
            if (!SwingActive)
            {
                return _isMining ? MathF.Sin(_animTime * 10f) * 3f : 0f;
            }

            float t = SwingPhase;
            float arc = Tween.EaseOut(t);
            return CurrentSwingKind switch
            {
                SwingKind.Melee => -82f * arc + MathF.Sin(t * MathF.PI) * 10f,
                SwingKind.Mine => -48f * arc + MathF.Sin(t * MathF.PI * 2f) * 6f,
                SwingKind.Place => -30f * arc + MathF.Sin(t * MathF.PI) * 4f,
                _ => -35f * arc
            };
        }

        public float GetHeldItemOffsetY()
        {
            if (!SwingActive)
            {
                return _isMining ? MathF.Sin(_animTime * 10f) * 4f : 0f;
            }

            float t = SwingPhase;
            float arc = MathF.Sin(t * MathF.PI);
            return CurrentSwingKind switch
            {
                SwingKind.Melee => -10f * arc - 3f * arc * arc,
                SwingKind.Mine => -6f * arc,
                SwingKind.Place => -3f * arc + MathF.Sin(t * MathF.PI * 2f) * 1.5f,
                _ => -4f * arc
            };
        }

        public float GetHeldItemOffsetX()
        {
            if (!SwingActive)
            {
                return 0f;
            }

            float t = SwingPhase;
            float arc = MathF.Sin(t * MathF.PI);
            return CurrentSwingKind switch
            {
                SwingKind.Melee => 0.15f - 0.35f * arc,
                SwingKind.Mine => 0.05f - 0.18f * arc,
                SwingKind.Place => 0.08f - 0.12f * arc,
                _ => 0f
            };
        }

        public float GetHeldItemOffsetZ()
        {
            if (!SwingActive)
            {
                return 0f;
            }

            float t = SwingPhase;
            float arc = MathF.Sin(t * MathF.PI);
            return CurrentSwingKind switch
            {
                SwingKind.Melee => 0.28f * arc,
                SwingKind.Mine => 0.16f * arc,
                SwingKind.Place => 0.10f * arc,
                _ => 0f
            };
        }

        public float GetHeldItemRollDegrees()
        {
            if (!SwingActive)
            {
                return 0f;
            }

            float t = SwingPhase;
            float arc = MathF.Sin(t * MathF.PI);
            return CurrentSwingKind switch
            {
                SwingKind.Melee => -22f * arc,
                SwingKind.Mine => -10f * arc,
                SwingKind.Place => -6f * arc,
                _ => 0f
            };
        }

        public float GetHeldItemPitchDegrees()
        {
            if (!SwingActive)
            {
                return 0f;
            }

            float t = SwingPhase;
            float arc = MathF.Sin(t * MathF.PI);
            return CurrentSwingKind switch
            {
                SwingKind.Melee => 18f * arc,
                SwingKind.Mine => 12f * arc,
                SwingKind.Place => 8f * arc,
                _ => 0f
            };
        }

        private float GetSwingPulse()
        {
            if (!SwingActive)
            {
                return 0f;
            }

            float t = SwingPhase;
            if (CurrentSwingKind == SwingKind.Melee)
            {
                return MathF.Sin(t * MathF.PI) * (0.6f + 0.4f * MathF.Sin(t * MathF.PI * 0.5f));
            }

            if (CurrentSwingKind == SwingKind.Mine)
            {
                return MathF.Sin(t * MathF.PI) * 0.7f;
            }

            return MathF.Sin(t * MathF.PI) * 0.45f;
        }

        private float GetSwingLeanX()
        {
            if (!SwingActive)
            {
                return 0f;
            }

            return CurrentSwingKind switch
            {
                SwingKind.Melee => -0.03f,
                SwingKind.Mine => -0.012f,
                SwingKind.Place => -0.01f,
                _ => 0f
            };
        }

        private float GetSwingLeanY()
        {
            if (!SwingActive)
            {
                return 0f;
            }

            return CurrentSwingKind switch
            {
                SwingKind.Melee => -0.015f,
                SwingKind.Mine => -0.01f,
                SwingKind.Place => -0.005f,
                _ => 0f
            };
        }

        private float GetSwingLeanZ()
        {
            if (!SwingActive)
            {
                return 0f;
            }

            return CurrentSwingKind switch
            {
                SwingKind.Melee => 0.02f,
                SwingKind.Mine => 0.01f,
                SwingKind.Place => 0.008f,
                _ => 0f
            };
        }
    }
}
