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
            BodyHalfW = Villager.Width * 0.34f,
            BodyHalfH = Villager.Height * 0.46f * 0.5f,
            BodyHalfD = Villager.Width * 0.28f,
            BodyCenterY = Villager.Height * 0.38f,
            HeadSize = Villager.Width * 0.28f,
            HeadCenterY = Villager.Height * 0.76f,
            HeadForward = Villager.Width * 0.42f
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

        public static float GetWalkPhase(Villager villager, float animTime)
        {
            float basePhase = villager.Position.X * 8f + villager.Position.Z * 11f + villager.Id * 0.25f;
            bool isMoving = villager.WanderDistanceRemaining > 0f ||
                MathF.Abs(villager.Velocity.X) > 0.01f ||
                MathF.Abs(villager.Velocity.Z) > 0.01f ||
                villager.AiPhase == VillagerAiPhase.PathTo;

            if (isMoving)
            {
                float pace = villager.CurrentJob == JobType.Haul ? 5.5f : 7f;
                if (villager.CurrentJob == JobType.Sleep)
                {
                    pace = 1.2f;
                }

                return basePhase + animTime * pace;
            }

            float idlePulse = villager.CurrentJob == JobType.Sleep ? 0.4f : 1f;
            return basePhase + animTime * idlePulse + villager.Id * 0.05f;
        }

        public static void DrawModelExtras(
            Matrix world,
            Villager villager,
            VillagerBodyLayout layout,
            float walkPhase,
            float animTime,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float w = Villager.Width;
            float h = Villager.Height;
            float bodyBottomY = layout.BodyBottomYComputed;
            float legSwing = MathF.Sin(walkPhase * 10f) * 0.03f;
            float action = GetActionMotion(villager);
            float idlePulse = MathF.Sin(animTime * 1.85f + villager.Id * 0.31f) * 0.015f;
            float bodyLean = villager.WanderDirection.LengthSquared() > 0.0001f ? villager.WanderDirection.Z * 0.02f : 0f;
            float bodyBob = idlePulse + MathF.Abs(action) * 0.01f;
            float bodyCenterY = layout.BodyCenterY + bodyBob;
            float headCenterY = layout.HeadCenterY + bodyBob * 1.4f;
            float headForward = layout.HeadForward + bodyLean;
            var pantsColor = new Color(45, 50, 70);
            var skinColor = new Color(210, 160, 130);
            var hairColor = new Color(60, 40, 20);
            var bootColor = new Color(35, 30, 28);
            var eyeColor = new Color(25, 22, 20);

            float legHalfW = w * 0.085f;
            float legHalfH = bodyBottomY * 0.5f;
            float legCenterY = legHalfH;
            float bootHalfH = MathF.Min(legHalfH * 0.28f, h * 0.025f);
            float legSpread = w * 0.13f;

            DrawVillagerLeg(world, -legSpread, legHalfW, legHalfH, legCenterY + bodyBob * 0.4f, bootHalfH, legSwing, pantsColor, bootColor, drawColored);
            DrawVillagerLeg(world, legSpread, legHalfW, legHalfH, legCenterY + bodyBob * 0.4f, bootHalfH, -legSwing, pantsColor, bootColor, drawColored);

            float armH = layout.BodyHalfH * 1.55f;
            float armW = w * 0.065f;
            float armY = bodyCenterY - layout.BodyHalfH * 0.05f;
            float armSpread = w * 0.34f;
            float armSwing = -legSwing * 0.85f + idlePulse * 0.25f;
            float leftArmY = armY;
            float rightArmY = armY;
            float leftArmZ = armSwing;
            float rightArmZ = -armSwing;
            if (action > 0f)
            {
                rightArmY += MathF.Abs(action) * h * 0.08f;
                rightArmZ += action * w * 0.48f;
                leftArmZ -= action * w * 0.22f;
            }

            if (villager.AiPhase == VillagerAiPhase.Sleeping)
            {
                leftArmY += h * 0.03f;
                rightArmY += h * 0.03f;
                leftArmZ *= 0.2f;
                rightArmZ *= 0.2f;
            }

            var sleeveColor = GetRoleColor(villager.Role) * 0.75f;
            drawColored(world, armW, armH * 0.5f, armW, -armSpread, leftArmY, leftArmZ, sleeveColor);
            drawColored(world, armW, armH * 0.5f, armW, armSpread, rightArmY, rightArmZ, sleeveColor);
            drawColored(world, armW * 0.65f, armH * 0.20f, armW * 0.55f, -armSpread, leftArmY - armH * 0.38f, leftArmZ, skinColor);
            drawColored(world, armW * 0.65f, armH * 0.20f, armW * 0.55f, armSpread, rightArmY - armH * 0.38f, rightArmZ, skinColor);

            float shoulderPadH = h * 0.035f;
            drawColored(world, armW * 1.4f, shoulderPadH, armW * 1.2f, -armSpread, bodyCenterY + layout.BodyHalfH - shoulderPadH, 0f, sleeveColor);
            drawColored(world, armW * 1.4f, shoulderPadH, armW * 1.2f, armSpread, bodyCenterY + layout.BodyHalfH - shoulderPadH, 0f, sleeveColor);

            float neckH = headCenterY - layout.HeadSize - (bodyCenterY + layout.BodyHalfH);
            if (neckH > 0.02f)
            {
                float neckHalfH = neckH * 0.5f;
                drawColored(world, layout.HeadSize * 0.35f, neckHalfH, layout.HeadSize * 0.30f, 0f, bodyCenterY + layout.BodyHalfH + neckHalfH, headForward * 0.4f, skinColor);
            }

            drawColored(world, layout.HeadSize * 0.92f, layout.HeadSize * 0.32f, layout.HeadSize * 0.82f, 0f, headCenterY + layout.HeadSize * 0.52f, headForward, hairColor);
            drawColored(world, layout.HeadSize * 0.09f, layout.HeadSize * 0.07f, layout.HeadSize * 0.04f, -layout.HeadSize * 0.34f, headCenterY + layout.HeadSize * 0.10f, headForward + layout.HeadSize * 0.86f, eyeColor);
            drawColored(world, layout.HeadSize * 0.09f, layout.HeadSize * 0.07f, layout.HeadSize * 0.04f, layout.HeadSize * 0.34f, headCenterY + layout.HeadSize * 0.10f, headForward + layout.HeadSize * 0.86f, eyeColor);
            drawColored(world, layout.HeadSize * 0.07f, layout.HeadSize * 0.11f, layout.HeadSize * 0.045f, 0f, headCenterY - layout.HeadSize * 0.05f, headForward + layout.HeadSize * 0.88f, new Color(180, 120, 95));
            drawColored(world, layout.HeadSize * 0.22f, layout.HeadSize * 0.035f, layout.HeadSize * 0.035f, 0f, headCenterY - layout.HeadSize * 0.30f, headForward + layout.HeadSize * 0.89f, new Color(95, 45, 35));
            drawColored(world, layout.BodyHalfW * 0.62f, layout.BodyHalfH * 0.13f, layout.BodyHalfD * 1.08f, 0f, bodyCenterY + layout.BodyHalfH * 0.35f, headForward * 0.10f, GetJobIndicatorColor(villager.CurrentJob));

            DrawRoleProps(world, villager, layout, rightArmY, rightArmZ, action, drawColored);
            DrawWorkEffect(world, villager, layout, action, drawColored);
        }

        private static float GetActionMotion(Villager villager)
        {
            if (villager.AiPhase != VillagerAiPhase.Working)
            {
                return 0f;
            }

            float progress = villager.BreakProgress > 0f
                ? villager.BreakProgress
                : Math.Clamp(villager.WorkTimer / Math.Max(0.01f, Villager.WorkInterval), 0f, 1f);
            return MathF.Sin(progress * MathF.PI * 2f) * 0.9f;
        }

        private static void DrawWorkEffect(
            Matrix world,
            Villager villager,
            VillagerBodyLayout layout,
            float action,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            if (MathF.Abs(action) < 0.05f || villager.AiPhase != VillagerAiPhase.Working)
            {
                return;
            }

            float w = Villager.Width;
            var flash = villager.CurrentJob switch
            {
                JobType.Build => new Color(0.28f, 0.62f, 0.95f, 0.55f),
                JobType.Mine => new Color(0.82f, 0.82f, 0.78f, 0.55f),
                JobType.Farm => new Color(0.55f, 0.82f, 0.28f, 0.50f),
                JobType.Lumber or JobType.Gather => new Color(0.45f, 0.78f, 0.32f, 0.50f),
                JobType.Craft => new Color(0.92f, 0.78f, 0.22f, 0.50f),
                _ => Color.Transparent
            };

            if (flash == Color.Transparent)
            {
                return;
            }

            float pulse = 0.04f + MathF.Abs(action) * 0.035f;
            drawColored(world, pulse, pulse, pulse, w * 0.40f, layout.BodyCenterY + layout.BodyHalfH * 0.25f, layout.HeadForward * 1.45f, flash);
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
            float action,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float w = Villager.Width;
            float workPulse = MathF.Abs(action) * 0.03f;
            switch (villager.Role)
            {
                case VillagerRole.Lumberjack:
                    drawColored(world, 0.05f, 0.05f, 0.15f, w * 0.34f, armY + workPulse, w * 0.44f + armSwing, new Color(0.55f, 0.35f, 0.18f, 0.95f));
                    drawColored(world, 0.04f, 0.04f, 0.05f, w * 0.34f, armY + 0.06f + workPulse, w * 0.52f + armSwing, new Color(0.72f, 0.72f, 0.76f, 0.95f));
                    break;
                case VillagerRole.Builder:
                    drawColored(world, w * 0.44f, 0.05f, w * 0.44f, 0f, layout.HeadCenterY + layout.HeadSize * 0.65f + workPulse, layout.HeadForward, new Color(0.95f, 0.55f, 0.15f, 0.95f));
                    drawColored(world, w * 0.08f, 0.04f, w * 0.08f, 0f, layout.HeadCenterY + layout.HeadSize * 0.70f + workPulse, layout.HeadForward, new Color(0.95f, 0.78f, 0.2f, 0.95f));
                    break;
                case VillagerRole.Farmer:
                    drawColored(world, w * 0.50f, 0.03f, w * 0.50f, 0f, layout.HeadCenterY + layout.HeadSize * 0.72f + workPulse, layout.HeadForward, new Color(0.92f, 0.82f, 0.35f, 0.95f));
                    drawColored(world, w * 0.06f, 0.10f, w * 0.06f, w * 0.30f, layout.BodyCenterY + workPulse, layout.HeadForward * 0.9f, new Color(0.55f, 0.38f, 0.18f, 0.95f));
                    break;
                case VillagerRole.Miner:
                    drawColored(world, w * 0.44f, 0.05f, w * 0.44f, 0f, layout.HeadCenterY + layout.HeadSize * 0.62f + workPulse, layout.HeadForward, new Color(0.95f, 0.75f, 0.15f, 0.95f));
                    drawColored(world, 0.05f, 0.05f, 0.12f, w * 0.32f, layout.BodyCenterY + workPulse, layout.HeadForward * 0.95f, new Color(0.50f, 0.50f, 0.55f, 0.95f));
                    break;
                case VillagerRole.Smith:
                    drawColored(world, w * 0.40f, layout.BodyHalfH * 0.42f, w * 0.08f, 0f, layout.BodyCenterY - layout.BodyHalfH * 0.12f + workPulse, layout.HeadForward * 0.9f, new Color(0.35f, 0.35f, 0.38f, 0.90f));
                    break;
                case VillagerRole.Hauler:
                    drawColored(world, w * 0.22f, Villager.Height * 0.16f, w * 0.12f, 0f, layout.BodyCenterY + layout.BodyHalfH * 0.1f + workPulse, -w * 0.30f, new Color(0.52f, 0.36f, 0.22f, 0.95f));
                    break;
                case VillagerRole.Hunter:
                    drawColored(world, 0.04f, 0.04f, 0.16f, w * 0.30f, layout.BodyCenterY + workPulse, layout.HeadForward * 1.1f, new Color(0.45f, 0.30f, 0.18f, 0.95f));
                    break;
                case VillagerRole.Cook:
                    drawColored(world, w * 0.42f, 0.04f, w * 0.42f, 0f, layout.HeadCenterY + layout.HeadSize * 0.58f + workPulse, layout.HeadForward, new Color(0.95f, 0.95f, 0.95f, 0.95f));
                    break;
                case VillagerRole.Mason:
                    drawColored(world, w * 0.40f, 0.04f, w * 0.40f, 0f, layout.HeadCenterY + layout.HeadSize * 0.58f + workPulse, layout.HeadForward, new Color(0.95f, 0.55f, 0.15f, 0.95f));
                    break;
            }
        }
    }
}
