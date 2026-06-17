using System;
using Microsoft.Xna.Framework;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{
    public readonly struct EntityModelPart
    {
        public float HalfX { get; init; }
        public float HalfY { get; init; }
        public float HalfZ { get; init; }
        public float CenterX { get; init; }
        public float CenterY { get; init; }
        public float CenterZ { get; init; }

        public static EntityModelPart Box(
            float halfX, float halfY, float halfZ,
            float centerX, float centerY, float centerZ) =>
            new()
            {
                HalfX = halfX,
                HalfY = halfY,
                HalfZ = halfZ,
                CenterX = centerX,
                CenterY = centerY,
                CenterZ = centerZ
            };

        public static void Draw(
            Matrix world,
            EntityModelPart part,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored) =>
            drawColored(world, part.HalfX, part.HalfY, part.HalfZ, part.CenterX, part.CenterY, part.CenterZ, color);
    }
}
