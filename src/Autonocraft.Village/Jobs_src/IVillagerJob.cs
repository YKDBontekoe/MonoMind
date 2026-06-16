using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    public interface IVillagerJob
    {
        void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context);
    }
}
