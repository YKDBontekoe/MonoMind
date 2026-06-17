using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public interface IJobAssignment
    {
        bool CreativeMode { get; }
        bool TryAssignJob(
            Village village,
            Villager villager,
            JobType job,
            Vector3? target = null,
            int? buildingSiteId = null,
            int? buildingId = null);
        bool TryAssignStockGoalWorker(Village village, VoxelWorld world, Villager villager, BlockType blockType);
        bool TryQueueBlueprint(
            VoxelWorld world,
            Village village,
            string blueprintId,
            int anchorX,
            int anchorZ,
            IItemContainer payer,
            int anchorY = -1);
    }
}
