namespace Autonocraft.Domain.World
{
    public interface IBlockReader
    {
        BlockType GetBlock(int x, int y, int z);
        bool IsSolid(int x, int y, int z);
    }
}
