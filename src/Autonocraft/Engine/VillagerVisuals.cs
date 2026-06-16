using System;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Microsoft.Xna.Framework;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{
    public readonly struct VillagerBodyLayout
    {
        public float BodyHalfW { get; init; }
        public float BodyHalfH { get; init; }
        public float BodyHalfD { get; init; }
        public float BodyCenterY { get; init; }
        public float HeadSize { get; init; }
        public float HeadCenterY { get; init; }
        public float HeadForward { get; init; }

        public static VillagerBodyLayout Default => new()
        {
            BodyHalfW = Villager.Width * 0.40f,
            BodyHalfH = Villager.Height * 0.50f * 0.5f,
            BodyHalfD = Villager.Width * 0.34f,
            BodyCenterY = Villager.Height * 0.40f,
            HeadSize = Villager.Width * 0.24f,
            HeadCenterY = Villager.Height * 0.78f,
            HeadForward = Villager.Width * 0.50f
        };

        public float BodyBottomYComputed => BodyCenterY - BodyHalfH;
    }

    public static class VillagerVisuals
    {
        public static Color GetRoleColor(VillagerRole role) => role switch
        {
            VillagerRole.Lumberjack => new Color(0.55f, 0.38f, 0.22f),
            VillagerRole.Builder => new Color(0.95f, 0.55f, 0.18f),
            VillagerRole.Farmer => new Color(0.35f, 0.72f, 0.38f),
            VillagerRole.Smith => new Color(0.45f, 0.48f, 0.52f),
            VillagerRole.Miner => new Color(0.58f, 0.58f, 0.62f),
            VillagerRole.Hauler => new Color(0.68f, 0.52f, 0.34f),
            _ => new Color(0.72f, 0.68f, 0.58f)
        };

        public static Color GetJobIndicatorColor(JobType job) => job switch
        {
            JobType.Gather or JobType.Lumber => new Color(0.35f, 0.78f, 0.32f, 0.92f),
            JobType.Mine => new Color(0.5f, 0.52f, 0.58f, 0.92f),
            JobType.Farm => new Color(0.55f, 0.82f, 0.28f, 0.92f),
            JobType.Build => new Color(0.28f, 0.62f, 0.95f, 0.92f),
            JobType.Haul => new Color(0.62f, 0.42f, 0.24f, 0.92f),
            JobType.Craft => new Color(0.92f, 0.78f, 0.22f, 0.92f),
            JobType.Sleep => new Color(0.35f, 0.45f, 0.72f, 0.75f),
            _ => Color.Transparent
        };

        public static bool ShouldDrawJobIndicator(JobType job) =>
            job is JobType.Gather or JobType.Lumber or JobType.Mine or JobType.Farm
                or JobType.Build or JobType.Haul or JobType.Craft or JobType.Sleep;

        public static float GetWalkPhase(Villager villager)
        {
            if (villager.WanderDistanceRemaining <= 0f &&
                MathF.Abs(villager.Velocity.X) < 0.01f &&
                MathF.Abs(villager.Velocity.Z) < 0.01f &&
                villager.CurrentJob == JobType.Idle)
            {
                return 0f;
            }

            return villager.Position.X * 8f + villager.Position.Z * 11f + villager.Id * 0.25f;
        }

        public static void DrawModelExtras(
            Matrix world,
            Villager villager,
            VillagerBodyLayout layout,
            float walkPhase,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float w = Villager.Width;
            float h = Villager.Height;
            float bodyBottomY = layout.BodyBottomYComputed;
            float legSwing = MathF.Sin(walkPhase * 10f) * 0.03f;
            var pantsColor = new Color(45, 50, 70);
            var skinColor = new Color(210, 160, 130);
            var hairColor = new Color(60, 40, 20);
            var bootColor = new Color(35, 30, 28);

            float legHalfW = w * 0.085f;
            float legHalfH = bodyBottomY * 0.5f;
            float legCenterY = legHalfH;
            float bootHalfH = MathF.Min(legHalfH * 0.28f, h * 0.025f);
            float legSpread = w * 0.13f;

            DrawVillagerLeg(world, -legSpread, legHalfW, legHalfH, legCenterY, bootHalfH, legSwing, pantsColor, bootColor, drawColored);
            DrawVillagerLeg(world, legSpread, legHalfW, legHalfH, legCenterY, bootHalfH, -legSwing, pantsColor, bootColor, drawColored);

            float armH = layout.BodyHalfH * 1.55f;
            float armW = w * 0.065f;
            float armY = layout.BodyCenterY - layout.BodyHalfH * 0.05f;
            float armSpread = w * 0.34f;
            float armSwing = -legSwing * 0.85f;
            var sleeveColor = GetRoleColor(villager.Role) * 0.75f;
            drawColored(world, armW, armH * 0.5f, armW, -armSpread, armY, armSwing, sleeveColor);
            drawColored(world, armW, armH * 0.5f, armW, armSpread, armY, -armSwing, sleeveColor);
            drawColored(world, armW * 0.65f, armH * 0.20f, armW * 0.55f, -armSpread, armY - armH * 0.38f, armSwing, skinColor);
            drawColored(world, armW * 0.65f, armH * 0.20f, armW * 0.55f, armSpread, armY - armH * 0.38f, -armSwing, skinColor);

            float shoulderPadH = h * 0.035f;
            drawColored(world, armW * 1.4f, shoulderPadH, armW * 1.2f, -armSpread, layout.BodyCenterY + layout.BodyHalfH - shoulderPadH, 0f, sleeveColor);
            drawColored(world, armW * 1.4f, shoulderPadH, armW * 1.2f, armSpread, layout.BodyCenterY + layout.BodyHalfH - shoulderPadH, 0f, sleeveColor);

            float neckH = layout.HeadCenterY - layout.HeadSize - (layout.BodyCenterY + layout.BodyHalfH);
            if (neckH > 0.02f)
            {
                float neckHalfH = neckH * 0.5f;
                drawColored(world, layout.HeadSize * 0.35f, neckHalfH, layout.HeadSize * 0.30f, 0f, layout.BodyCenterY + layout.BodyHalfH + neckHalfH, layout.HeadForward * 0.4f, skinColor);
            }

            drawColored(world, layout.HeadSize * 0.92f, layout.HeadSize * 0.32f, layout.HeadSize * 0.82f, 0f, layout.HeadCenterY + layout.HeadSize * 0.52f, layout.HeadForward, hairColor);

            DrawRoleProps(world, villager, layout, armY, armSwing, drawColored);
        }

        private static void DrawVillagerLeg(
            Matrix world,
            float x,
            float legHalfW,
            float legHalfH,
            float legCenterY,
            float bootHalfH,
            float swing,
            Color pantsColor,
            Color bootColor,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float upperHalfH = legHalfH - bootHalfH;
            float upperCenterY = bootHalfH + upperHalfH;
            drawColored(world, legHalfW, upperHalfH, legHalfW, x, upperCenterY + swing, 0f, pantsColor);
            drawColored(world, legHalfW * 1.05f, bootHalfH, legHalfW * 1.15f, x, bootHalfH + swing, 0f, bootColor);
        }

        private static void DrawRoleProps(
            Matrix world,
            Villager villager,
            VillagerBodyLayout layout,
            float armY,
            float armSwing,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float w = Villager.Width;
            switch (villager.Role)
            {
                case VillagerRole.Lumberjack:
                    drawColored(world, 0.05f, 0.05f, 0.15f, w * 0.34f, armY, w * 0.44f + armSwing, new Color(0.55f, 0.35f, 0.18f, 0.95f));
                    drawColored(world, 0.04f, 0.04f, 0.05f, w * 0.34f, armY + 0.06f, w * 0.52f + armSwing, new Color(0.72f, 0.72f, 0.76f, 0.95f));
                    break;
                case VillagerRole.Builder:
                    drawColored(world, w * 0.44f, 0.05f, w * 0.44f, 0f, layout.HeadCenterY + layout.HeadSize * 0.65f, layout.HeadForward, new Color(0.95f, 0.55f, 0.15f, 0.95f));
                    drawColored(world, w * 0.08f, 0.04f, w * 0.08f, 0f, layout.HeadCenterY + layout.HeadSize * 0.70f, layout.HeadForward, new Color(0.95f, 0.78f, 0.2f, 0.95f));
                    break;
                case VillagerRole.Farmer:
                    drawColored(world, w * 0.50f, 0.03f, w * 0.50f, 0f, layout.HeadCenterY + layout.HeadSize * 0.72f, layout.HeadForward, new Color(0.92f, 0.82f, 0.35f, 0.95f));
                    drawColored(world, w * 0.06f, 0.10f, w * 0.06f, w * 0.30f, layout.BodyCenterY, layout.HeadForward * 0.9f, new Color(0.55f, 0.38f, 0.18f, 0.95f));
                    break;
                case VillagerRole.Miner:
                    drawColored(world, w * 0.44f, 0.05f, w * 0.44f, 0f, layout.HeadCenterY + layout.HeadSize * 0.62f, layout.HeadForward, new Color(0.95f, 0.75f, 0.15f, 0.95f));
                    drawColored(world, 0.05f, 0.05f, 0.12f, w * 0.32f, layout.BodyCenterY, layout.HeadForward * 0.95f, new Color(0.50f, 0.50f, 0.55f, 0.95f));
                    break;
                case VillagerRole.Smith:
                    drawColored(world, w * 0.40f, layout.BodyHalfH * 0.42f, w * 0.08f, 0f, layout.BodyCenterY - layout.BodyHalfH * 0.12f, layout.HeadForward * 0.9f, new Color(0.35f, 0.35f, 0.38f, 0.90f));
                    break;
                case VillagerRole.Hauler:
                    drawColored(world, w * 0.22f, Villager.Height * 0.16f, w * 0.12f, 0f, layout.BodyCenterY + layout.BodyHalfH * 0.1f, -w * 0.30f, new Color(0.52f, 0.36f, 0.22f, 0.95f));
                    break;
                case VillagerRole.Hunter:
                    drawColored(world, 0.04f, 0.04f, 0.16f, w * 0.30f, layout.BodyCenterY, layout.HeadForward * 1.1f, new Color(0.45f, 0.30f, 0.18f, 0.95f));
                    break;
                case VillagerRole.Cook:
                    drawColored(world, w * 0.42f, 0.04f, w * 0.42f, 0f, layout.HeadCenterY + layout.HeadSize * 0.58f, layout.HeadForward, new Color(0.95f, 0.95f, 0.95f, 0.95f));
                    break;
                case VillagerRole.Mason:
                    drawColored(world, w * 0.40f, 0.04f, w * 0.40f, 0f, layout.HeadCenterY + layout.HeadSize * 0.58f, layout.HeadForward, new Color(0.95f, 0.55f, 0.15f, 0.95f));
                    break;
            }
        }
    }
}
