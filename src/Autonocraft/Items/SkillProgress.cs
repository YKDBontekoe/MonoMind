namespace Autonocraft.Items
{
    public struct SkillProgress
    {
        public int Level;
        public float Xp;

        public static SkillProgress Default => new() { Level = 1, Xp = 0f };

        public float XpForNextLevel()
        {
            return 50f * Level * Level;
        }

        public float ProgressToNextLevel()
        {
            float needed = XpForNextLevel();
            if (needed <= 0f)
            {
                return 1f;
            }

            return Math.Clamp(Xp / needed, 0f, 1f);
        }
    }
}
