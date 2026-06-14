using System.Numerics;

namespace Autonocraft.World
{
    public readonly struct ChunkStreamingProfile
    {
        public bool FastTravel { get; init; }
        public Vector3 Velocity { get; init; }
        public int AgentChunkX { get; init; }
        public int AgentChunkZ { get; init; }

        public static ChunkStreamingProfile FromMovement(Vector3 position, Vector3 velocity, bool flying)
        {
            VoxelWorld.GetChunkCoords(
                (int)MathF.Round(position.X),
                (int)MathF.Round(position.Z),
                out int agentCx,
                out int agentCz,
                out _,
                out _);

            float horizontalSpeed = MathF.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
            return new ChunkStreamingProfile
            {
                FastTravel = flying || horizontalSpeed > 8f,
                Velocity = velocity,
                AgentChunkX = agentCx,
                AgentChunkZ = agentCz
            };
        }

        public static ChunkStreamingProfile Stationary(Vector3 position)
        {
            return FromMovement(position, Vector3.Zero, flying: false);
        }
    }
}
