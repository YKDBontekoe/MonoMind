namespace Autonocraft.World.Structures
{
    internal static class StableOrdinalHash
    {
        public static int Hash(string value)
        {
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }

                return hash;
            }
        }
    }
}
