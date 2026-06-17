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
        private void DrawTexturedBox(
            Matrix animalWorld,
            float halfWidth,
            float halfHeight,
            float halfDepth,
            float localCenterX,
            float localCenterY,
            float localCenterZ,
            (float uMin, float vMin, float uMax, float vMax) bodyUV,
            (float uMin, float vMin, float uMax, float vMax) frontUV)
        {
            var localCenter = Matrix.CreateTranslation(localCenterX, localCenterY, localCenterZ);
            _worldEffect.World = localCenter * animalWorld;

            float x0 = -halfWidth;
            float x1 = halfWidth;
            float y0 = -halfHeight;
            float y1 = halfHeight;
            float z0 = -halfDepth;
            float z1 = halfDepth;

            var p0 = new Vector3(x0, y0, z1);
            var p1 = new Vector3(x1, y0, z1);
            var p2 = new Vector3(x1, y1, z1);
            var p3 = new Vector3(x0, y1, z1);
            var p4 = new Vector3(x0, y0, z0);
            var p5 = new Vector3(x1, y0, z0);
            var p6 = new Vector3(x1, y1, z0);
            var p7 = new Vector3(x0, y1, z0);

            var colVec = Vector3.One;

            var nFront = new Vector3(0, 0, 1);
            TexturedBoxVertices[0] = new Vertex(p0, colVec, nFront, new System.Numerics.Vector2(frontUV.uMin, frontUV.vMax));
            TexturedBoxVertices[1] = new Vertex(p1, colVec, nFront, new System.Numerics.Vector2(frontUV.uMax, frontUV.vMax));
            TexturedBoxVertices[2] = new Vertex(p2, colVec, nFront, new System.Numerics.Vector2(frontUV.uMax, frontUV.vMin));
            TexturedBoxVertices[3] = new Vertex(p3, colVec, nFront, new System.Numerics.Vector2(frontUV.uMin, frontUV.vMin));

            var nRight = new Vector3(1, 0, 0);
            TexturedBoxVertices[4] = new Vertex(p1, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            TexturedBoxVertices[5] = new Vertex(p5, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            TexturedBoxVertices[6] = new Vertex(p6, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            TexturedBoxVertices[7] = new Vertex(p2, colVec, nRight, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            var nBack = new Vector3(0, 0, -1);
            TexturedBoxVertices[8] = new Vertex(p5, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            TexturedBoxVertices[9] = new Vertex(p4, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            TexturedBoxVertices[10] = new Vertex(p7, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            TexturedBoxVertices[11] = new Vertex(p6, colVec, nBack, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            var nLeft = new Vector3(-1, 0, 0);
            TexturedBoxVertices[12] = new Vertex(p4, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            TexturedBoxVertices[13] = new Vertex(p0, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            TexturedBoxVertices[14] = new Vertex(p3, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            TexturedBoxVertices[15] = new Vertex(p7, colVec, nLeft, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            var nTop = new Vector3(0, 1, 0);
            TexturedBoxVertices[16] = new Vertex(p3, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            TexturedBoxVertices[17] = new Vertex(p2, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            TexturedBoxVertices[18] = new Vertex(p6, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            TexturedBoxVertices[19] = new Vertex(p7, colVec, nTop, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            var nBottom = new Vector3(0, -1, 0);
            TexturedBoxVertices[20] = new Vertex(p4, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMax));
            TexturedBoxVertices[21] = new Vertex(p5, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMax));
            TexturedBoxVertices[22] = new Vertex(p1, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMax, bodyUV.vMin));
            TexturedBoxVertices[23] = new Vertex(p0, colVec, nBottom, new System.Numerics.Vector2(bodyUV.uMin, bodyUV.vMin));

            foreach (var pass in _worldEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, TexturedBoxVertices, 0, 24, TexturedBoxIndices, 0, 12);
            }
        }

        private void DrawColoredBox(
            Matrix animalWorld,
            float halfWidth,
            float halfHeight,
            float halfDepth,
            float localCenterX,
            float localCenterY,
            float localCenterZ,
            Color color)
        {
            var localCenter = Matrix.CreateTranslation(localCenterX, localCenterY, localCenterZ);
            _worldEffect.World = localCenter * animalWorld;

            float x0 = -halfWidth;
            float x1 = halfWidth;
            float y0 = -halfHeight;
            float y1 = halfHeight;
            float z0 = -halfDepth;
            float z1 = halfDepth;

            ColoredBoxVertices[0] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y0, z1), color);
            ColoredBoxVertices[1] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y0, z1), color);
            ColoredBoxVertices[2] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y1, z1), color);
            ColoredBoxVertices[3] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y1, z1), color);
            ColoredBoxVertices[4] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y0, z0), color);
            ColoredBoxVertices[5] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y0, z0), color);
            ColoredBoxVertices[6] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x1, y1, z0), color);
            ColoredBoxVertices[7] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x0, y1, z0), color);

            foreach (var pass in _worldEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, ColoredBoxVertices, 0, 8, ColoredBoxIndices, 0, 12);
            }
        }

        private static short[] BuildBoxIndices()
        {
            var indices = new short[36];
            for (int f = 0; f < 6; f++)
            {
                int vOffset = f * 4;
                int iOffset = f * 6;
                indices[iOffset + 0] = (short)(vOffset + 0);
                indices[iOffset + 1] = (short)(vOffset + 1);
                indices[iOffset + 2] = (short)(vOffset + 2);
                indices[iOffset + 3] = (short)(vOffset + 0);
                indices[iOffset + 4] = (short)(vOffset + 2);
                indices[iOffset + 5] = (short)(vOffset + 3);
            }

            return indices;
        }
    }
}
