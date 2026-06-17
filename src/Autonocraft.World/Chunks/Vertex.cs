using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.World
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex : IVertexType
    {
        public Vector3 Position;
        public Vector3 Color;
        public Vector3 Normal;
        public Vector2 TexCoords;

        public Vertex(Vector3 position, Vector3 color, Vector3 normal, Vector2 texCoords)
        {
            Position = position;
            Color = color;
            Normal = normal;
            TexCoords = texCoords;
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Color, 0),
            new VertexElement(24, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(36, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
        );

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
