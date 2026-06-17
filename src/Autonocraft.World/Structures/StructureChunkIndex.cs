using System;
using System.Collections.Generic;

namespace Autonocraft.World.Structures
{
    internal static class StructureCoords
    {
        public static int FloorDiv(int value, int divisor)
        {
            if (value >= 0)
            {
                return value / divisor;
            }

            return (value - divisor + 1) / divisor;
        }

        public static bool ChunkOverlapsFootprint(
            int chunkMinX,
            int chunkMaxX,
            int chunkMinZ,
            int chunkMaxZ,
            int anchorX,
            int anchorZ,
            int radius)
        {
            return chunkMaxX >= anchorX - radius
                && chunkMinX <= anchorX + radius
                && chunkMaxZ >= anchorZ - radius
                && chunkMinZ <= anchorZ + radius;
        }
    }

    internal sealed class StructureChunkIndex
    {
        private readonly Dictionary<(int X, int Z), StructureBlock[]> _buckets;

        private StructureChunkIndex(Dictionary<(int X, int Z), StructureBlock[]> buckets)
        {
            _buckets = buckets;
        }

        public static StructureChunkIndex Build(StructureBlock[] blocks)
        {
            var lists = new Dictionary<(int X, int Z), List<StructureBlock>>();
            foreach (var block in blocks)
            {
                int bucketX = StructureCoords.FloorDiv(block.Dx, Chunk.Width);
                int bucketZ = StructureCoords.FloorDiv(block.Dz, Chunk.Depth);
                var key = (bucketX, bucketZ);
                if (!lists.TryGetValue(key, out var list))
                {
                    list = new List<StructureBlock>();
                    lists[key] = list;
                }

                list.Add(block);
            }

            var buckets = new Dictionary<(int X, int Z), StructureBlock[]>(lists.Count);
            foreach (var entry in lists)
            {
                buckets[entry.Key] = entry.Value.ToArray();
            }

            return new StructureChunkIndex(buckets);
        }

        public IEnumerable<StructureBlock> EnumerateForChunk(
            int chunkMinX,
            int chunkMinZ,
            int anchorX,
            int anchorZ)
        {
            int minDx = chunkMinX - anchorX;
            int maxDx = chunkMinX + Chunk.Width - 1 - anchorX;
            int minDz = chunkMinZ - anchorZ;
            int maxDz = chunkMinZ + Chunk.Depth - 1 - anchorZ;

            int minBucketX = StructureCoords.FloorDiv(minDx, Chunk.Width);
            int maxBucketX = StructureCoords.FloorDiv(maxDx, Chunk.Width);
            int minBucketZ = StructureCoords.FloorDiv(minDz, Chunk.Depth);
            int maxBucketZ = StructureCoords.FloorDiv(maxDz, Chunk.Depth);

            for (int bx = minBucketX; bx <= maxBucketX; bx++)
            {
                for (int bz = minBucketZ; bz <= maxBucketZ; bz++)
                {
                    if (_buckets.TryGetValue((bx, bz), out var blocks))
                    {
                        foreach (var block in blocks)
                        {
                            yield return block;
                        }
                    }
                }
            }
        }
    }
}
