using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{

    public sealed partial class WorldRenderer
    {
        private static void ExtractFrustumPlanes(Matrix viewProjection, Microsoft.Xna.Framework.Vector4[] planes)
        {
            planes[0] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M14 + viewProjection.M11,
                viewProjection.M24 + viewProjection.M21,
                viewProjection.M34 + viewProjection.M31,
                viewProjection.M44 + viewProjection.M41);
            planes[1] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M14 - viewProjection.M11,
                viewProjection.M24 - viewProjection.M21,
                viewProjection.M34 - viewProjection.M31,
                viewProjection.M44 - viewProjection.M41);
            planes[2] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M14 + viewProjection.M12,
                viewProjection.M24 + viewProjection.M22,
                viewProjection.M34 + viewProjection.M32,
                viewProjection.M44 + viewProjection.M42);
            planes[3] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M14 - viewProjection.M12,
                viewProjection.M24 - viewProjection.M22,
                viewProjection.M34 - viewProjection.M32,
                viewProjection.M44 - viewProjection.M42);
            planes[4] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M13,
                viewProjection.M23,
                viewProjection.M33,
                viewProjection.M43);
            planes[5] = new Microsoft.Xna.Framework.Vector4(
                viewProjection.M14 - viewProjection.M13,
                viewProjection.M24 - viewProjection.M23,
                viewProjection.M34 - viewProjection.M33,
                viewProjection.M44 - viewProjection.M43);

            for (int i = 0; i < planes.Length; i++)
            {
                float length = MathF.Sqrt(
                    planes[i].X * planes[i].X +
                    planes[i].Y * planes[i].Y +
                    planes[i].Z * planes[i].Z);
                if (length > 0f)
                {
                    planes[i] /= length;
                }
            }
        }

        private static bool IsPointVisible(Vector3 point, Microsoft.Xna.Framework.Vector4[] planes)
        {
            for (int i = 0; i < planes.Length; i++)
            {
                if (planes[i].X * point.X + planes[i].Y * point.Y + planes[i].Z * point.Z + planes[i].W < 0f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsChunkVisible(Chunk chunk, Microsoft.Xna.Framework.Vector4[] planes)
        {
            float minX = chunk.ChunkX * Chunk.Width;
            float minY = 0f;
            float minZ = chunk.ChunkZ * Chunk.Depth;
            float maxX = minX + Chunk.Width;
            float maxY = Chunk.Height;
            float maxZ = minZ + Chunk.Depth;

            for (int i = 0; i < planes.Length; i++)
            {
                float px = planes[i].X >= 0f ? maxX : minX;
                float py = planes[i].Y >= 0f ? maxY : minY;
                float pz = planes[i].Z >= 0f ? maxZ : minZ;

                if (planes[i].X * px + planes[i].Y * py + planes[i].Z * pz + planes[i].W < 0f)
                {
                    return false;
                }
            }

            return true;
        }

        private static Matrix ConvertMatrix(Matrix4x4 m)
        {
            return new Matrix(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44
            );
        }

        private static Microsoft.Xna.Framework.Vector3 ConvertVector(Vector3 v)
        {
            return new Microsoft.Xna.Framework.Vector3(v.X, v.Y, v.Z);
        }
    }
}
