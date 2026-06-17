namespace Autonocraft.World.Generation
{
    public interface INoiseProvider
    {
        float Sample2D(float x, float y);
        float Sample3D(float x, float y, float z);
    }
}
