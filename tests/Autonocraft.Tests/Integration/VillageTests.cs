using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autonocraft.Core;
using DevCommands = Autonocraft.Core.DevCommands.DevCommandRouter;
using Autonocraft.Ai;
using Autonocraft.Domain.Core;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Tests.Integration;

public static partial class VillageTests
{
    private static bool TryFoundTestVillage(
        VillageManager villages,
        VoxelWorld world,
        string name,
        int ax,
        int az,
        out VillageEntity? village)
    {
        world.UpdateChunksAround(null, new Vector3(ax + 0.5f, 64f, az + 0.5f), 2);
        if (PlayerStructureRegistry.TryGet("town_heart", out var heart))
        {
            int ay = StructureFingerprint.FindSurfaceAnchorY(world, ax, az);
            foreach (var block in heart.Template.Blocks)
            {
                world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, BlockType.Air);
            }
        }

        return villages.TryFoundVillage(world, name, ax, az, out village);
    }

    private static void EnsureFlatVillagePad(VoxelWorld world, VillageEntity village, int radius)
    {
        world.UpdateChunksAround(null, village.Center, 3);
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                int x = village.AnchorX + dx;
                int z = village.AnchorZ + dz;
                int targetY = village.AnchorY;
                int top = world.GetHighestSolidY(x, z);
                for (int y = top; y > targetY; y--)
                {
                    world.SetBlock(x, y, z, BlockType.Air);
                }

                world.SetBlock(x, targetY, z, BlockType.Grass);
                if (targetY > 1)
                {
                    world.SetBlock(x, targetY - 1, z, BlockType.Dirt);
                }
            }
        }
    }

    private static void ClearStructureFootprint(VoxelWorld world, int ax, int ay, int az, int radius, int height)
    {
        for (int x = ax - radius; x <= ax + radius; x++)
        {
            for (int z = az - radius; z <= az + radius; z++)
            {
                for (int y = ay; y <= ay + height; y++)
                {
                    world.SetBlock(x, y, z, BlockType.Air);
                }
            }
        }
    }

    private static void StampTemplate(VoxelWorld world, int ax, int ay, int az, StructureTemplate template)
    {
        foreach (var block in template.Blocks)
        {
            world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, block.Type);
        }

        foreach (var chest in template.Chests)
        {
            world.SetBlock(ax + chest.Dx, ay + chest.Dy, az + chest.Dz, BlockType.Chest);
            world.SetBlock(ax + chest.Dx, ay + chest.Dy + 1, az + chest.Dz, BlockType.Air);
        }
    }
}
