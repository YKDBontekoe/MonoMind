namespace Autonocraft.Domain.World
{
    public interface IBlockWriter
    {
        void SetBlock(int x, int y, int z, BlockType type);
    }
}
