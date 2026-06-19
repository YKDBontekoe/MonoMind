using Autonocraft.Domain.Village;
using Autonocraft.Engine;
using Autonocraft.Entities;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.UI.VillagePanels
{
    internal readonly struct PeopleDetailLayout
    {
        public float TalkButtonY { get; }
        public float JobSectionY { get; }

        private PeopleDetailLayout(float talkButtonY, float jobSectionY)
        {
            TalkButtonY = talkButtonY;
            JobSectionY = jobSectionY;
        }

        public static PeopleDetailLayout Compute(
            UiLayout layout,
            Villager villager,
            VillageEntity village,
            string? assignFeedback)
        {
            float pad = layout.S(16f);
            float y = pad;
            y += layout.S(28f + 22f + 18f);
            if (!string.IsNullOrEmpty(VillagerActivityText.DescribeProgress(villager, village)))
            {
                y += layout.S(18f);
            }

            y += layout.S(4f + 22f + 28f + 52f);
            float talkButtonY = y;
            y += layout.S(48f);
            if (!string.IsNullOrEmpty(assignFeedback))
            {
                y += layout.S(22f);
            }

            y += layout.S(24f);
            return new PeopleDetailLayout(talkButtonY, y);
        }
    }
}
