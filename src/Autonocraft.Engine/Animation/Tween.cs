using System;
using Microsoft.Xna.Framework;

namespace Autonocraft.Engine.Animation
{
    public static class Tween
    {
        public static float SmoothDamp(float current, float target, float speed, float dt)
        {
            if (speed <= 0f || dt <= 0f)
            {
                return target;
            }

            float t = 1f - MathF.Exp(-speed * dt);
            return MathHelper.Lerp(current, target, t);
        }

        public static float EaseOut(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float Pulse(float time, float frequency)
        {
            return 0.5f + 0.5f * MathF.Sin(time * frequency * MathF.PI * 2f);
        }
    }
}
