namespace Autonocraft.Items
{
    public sealed class VillagerSkills
    {
        public SkillProgress Mining = SkillProgress.Default;
        public SkillProgress Woodcutting = SkillProgress.Default;
        public SkillProgress Farming = SkillProgress.Default;

        public ref SkillProgress GetProgress(VillagerSkill skill)
        {
            switch (skill)
            {
                case VillagerSkill.Mining:
                    return ref Mining;
                case VillagerSkill.Woodcutting:
                    return ref Woodcutting;
                case VillagerSkill.Farming:
                    return ref Farming;
                default:
                    throw new ArgumentOutOfRangeException(nameof(skill));
            }
        }

        public int GetLevel(VillagerSkill skill) => GetProgress(skill).Level;

        public float GetBonus(VillagerSkill skill)
        {
            int level = GetLevel(skill);
            return 1f + 0.05f * (level - 1);
        }

        public bool AddXp(VillagerSkill skill, float amount)
        {
            if (amount <= 0f)
            {
                return false;
            }

            ref var progress = ref GetProgress(skill);
            progress.Xp += amount;
            bool leveled = false;

            while (progress.Xp >= progress.XpForNextLevel())
            {
                progress.Xp -= progress.XpForNextLevel();
                progress.Level++;
                leveled = true;
            }

            return leveled;
        }
    }
}
