using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.World
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FloraVertex : IVertexType
    {
        public Vector3 Position;
        public Vector3 Color;
        public Vector2 TexCoords;
        public float WindPhase;
        public float HeightFactor;

        public FloraVertex(Vector3 position, Vector3 color, Vector2 texCoords, float windPhase, float heightFactor)
        {
            Position = position;
            Color = color;
            TexCoords = texCoords;
            WindPhase = windPhase;
            HeightFactor = heightFactor;
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Color, 0),
            new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(32, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(36, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 2));

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
