using System;

namespace Autonocraft.Core
{
    public sealed class GameSettings
    {
        public const int MinRenderDistance = 2;
        public const int MaxRenderDistance = 12;
        public const int DefaultRenderDistance = 8;

        public int RenderDistance { get; set; } = DefaultRenderDistance;

        public void Clamp()
        {
            RenderDistance = Math.Clamp(RenderDistance, MinRenderDistance, MaxRenderDistance);
        }
    }
}
