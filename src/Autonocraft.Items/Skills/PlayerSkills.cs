namespace Autonocraft.Items
{
    public sealed class PlayerSkills
    {
        public SkillProgress Mining = SkillProgress.Default;
        public SkillProgress Woodcutting = SkillProgress.Default;
        public SkillProgress Combat = SkillProgress.Default;

        public ref SkillProgress GetProgress(PlayerSkill skill)
        {
            switch (skill)
            {
                case PlayerSkill.Mining:
                    return ref Mining;
                case PlayerSkill.Woodcutting:
                    return ref Woodcutting;
                case PlayerSkill.Combat:
                    return ref Combat;
                default:
                    throw new ArgumentOutOfRangeException(nameof(skill));
            }
        }

        public int GetLevel(PlayerSkill skill) => GetProgress(skill).Level;

        public float GetBonus(PlayerSkill skill)
        {
            int level = GetLevel(skill);
            return 1f + 0.05f * (level - 1);
        }

        public bool AddXp(PlayerSkill skill, float amount)
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
