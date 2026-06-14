using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    internal sealed class SleepJob : IVillagerJob
    {
        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            villager.SetAiPhase(VillagerAiPhase.Sleeping);
            villager.Velocity = System.Numerics.Vector3.Zero;
            villager.WanderDirection = System.Numerics.Vector3.Zero;
        }
    }
}
