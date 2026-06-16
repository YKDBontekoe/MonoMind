using System.Collections.Generic;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    public static class JobRegistry
    {
        private static readonly Dictionary<JobType, IVillagerJob> Jobs = new()
        {
            [JobType.Sleep] = new SleepJob(),
            [JobType.Gather] = new LumberJob(),
            [JobType.Lumber] = new LumberJob(),
            [JobType.Mine] = new MineJob(),
            [JobType.Farm] = new FarmJob(),
            [JobType.Build] = new BuildJob(),
            [JobType.Haul] = new HaulJob(),
            [JobType.Craft] = new CraftJob(),
            [JobType.Hunt] = new HunterJob(),
            [JobType.Mason] = new MasonJob(),
            [JobType.Cook] = new CookJob(),
            [JobType.Idle] = new IdleJob(),
        };

        private static readonly IVillagerJob DefaultJob = Jobs[JobType.Idle];

        public static void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (Jobs.TryGetValue(villager.CurrentJob, out var job))
            {
                job.Tick(villager, deltaTime, world, context);
            }
            else
            {
                DefaultJob.Tick(villager, deltaTime, world, context);
            }
        }

        public static void Register(JobType jobType, IVillagerJob handler) => Jobs[jobType] = handler;
    }
}
