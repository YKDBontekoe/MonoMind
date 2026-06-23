using System;
using Autonocraft.Entities;
using Microsoft.Xna.Framework;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{
    public readonly struct AnimalShape
    {
        public float BodyWidthMul { get; init; }
        public float BodyDepthMul { get; init; }
        public float BodyHeightMul { get; init; }
        public float BodyCenterYFrac { get; init; }
        public float NeckHeightMul { get; init; }
        public float NeckDepthMul { get; init; }
        public float HeadWidthMul { get; init; }
        public float HeadHeightMul { get; init; }
        public float HeadDepthMul { get; init; }
        public float LegWidthMul { get; init; }
    }

    public readonly struct AnimalBodyLayout
    {
        public float Width { get; init; }
        public float Height { get; init; }
        public float BodyHalfW { get; init; }
        public float BodyHalfH { get; init; }
        public float BodyHalfD { get; init; }
        public float BodyCenterY { get; init; }
        public float BodyBottomY { get; init; }
        public float BodyTopY { get; init; }
        public float NeckHalfW { get; init; }
        public float NeckHalfH { get; init; }
        public float NeckHalfD { get; init; }
        public float NeckCenterY { get; init; }
        public float NeckCenterZ { get; init; }
        public float HeadHalfW { get; init; }
        public float HeadHalfH { get; init; }
        public float HeadHalfD { get; init; }
        public float HeadCenterY { get; init; }
        public float HeadForward { get; init; }
        public float LegWidth { get; init; }

        public float HeadSize => MathF.Max(HeadHalfW, MathF.Max(HeadHalfH, HeadHalfD));

        public static AnimalBodyLayout From(AnimalShape shape, AnimalStats stats)
        {
            float w = stats.Width;
            float h = stats.Height;

            float bodyHalfW = w * shape.BodyWidthMul;
            float bodyHalfD = w * shape.BodyDepthMul;
            float bodyHalfH = h * shape.BodyHeightMul * 0.5f;
            float bodyCenterY = h * shape.BodyCenterYFrac;
            float bodyBottomY = MathF.Max(bodyCenterY - bodyHalfH, h * 0.06f);
            float bodyTopY = bodyCenterY + bodyHalfH;

            float neckHalfH = h * shape.NeckHeightMul * 0.5f;
            float neckHalfW = w * shape.HeadWidthMul * 0.55f;
            float neckHalfD = w * shape.NeckDepthMul * 0.5f;
            float neckCenterY = bodyTopY + neckHalfH;
            float neckCenterZ = bodyHalfD + neckHalfD;

            float headHalfW = w * shape.HeadWidthMul;
            float headHalfH = h * shape.HeadHeightMul;
            float headHalfD = w * shape.HeadDepthMul;
            float headCenterY = bodyTopY + neckHalfH * 2f + headHalfH;
            float headForward = neckCenterZ + neckHalfD + headHalfD * 0.55f;

            return new AnimalBodyLayout
            {
                Width = w,
                Height = h,
                BodyHalfW = bodyHalfW,
                BodyHalfH = bodyHalfH,
                BodyHalfD = bodyHalfD,
                BodyCenterY = bodyCenterY,
                BodyBottomY = bodyBottomY,
                BodyTopY = bodyTopY,
                NeckHalfW = neckHalfW,
                NeckHalfH = neckHalfH,
                NeckHalfD = neckHalfD,
                NeckCenterY = neckCenterY,
                NeckCenterZ = neckCenterZ,
                HeadHalfW = headHalfW,
                HeadHalfH = headHalfH,
                HeadHalfD = headHalfD,
                HeadCenterY = headCenterY,
                HeadForward = headForward,
                LegWidth = w * shape.LegWidthMul
            };
        }
    }

    public static class AnimalVisuals
    {
        public static AnimalShape GetShape(AnimalType type) => type switch
        {
            AnimalType.Chicken => new AnimalShape
            {
                BodyWidthMul = 0.34f,
                BodyDepthMul = 0.30f,
                BodyHeightMul = 0.36f,
                BodyCenterYFrac = 0.30f,
                NeckHeightMul = 0.04f,
                NeckDepthMul = 0.10f,
                HeadWidthMul = 0.14f,
                HeadHeightMul = 0.14f,
                HeadDepthMul = 0.13f,
                LegWidthMul = 0.07f
            },
            AnimalType.Pig => new AnimalShape
            {
                BodyWidthMul = 0.50f,
                BodyDepthMul = 0.46f,
                BodyHeightMul = 0.44f,
                BodyCenterYFrac = 0.42f,
                NeckHeightMul = 0.05f,
                NeckDepthMul = 0.14f,
                HeadWidthMul = 0.20f,
                HeadHeightMul = 0.16f,
                HeadDepthMul = 0.18f,
                LegWidthMul = 0.10f
            },
            AnimalType.Cow => new AnimalShape
            {
                BodyWidthMul = 0.54f,
                BodyDepthMul = 0.72f,
                BodyHeightMul = 0.50f,
                BodyCenterYFrac = 0.46f,
                NeckHeightMul = 0.10f,
                NeckDepthMul = 0.22f,
                HeadWidthMul = 0.22f,
                HeadHeightMul = 0.18f,
                HeadDepthMul = 0.20f,
                LegWidthMul = 0.11f
            },
            AnimalType.Bear => new AnimalShape
            {
                BodyWidthMul = 0.56f,
                BodyDepthMul = 0.50f,
                BodyHeightMul = 0.48f,
                BodyCenterYFrac = 0.42f,
                NeckHeightMul = 0.06f,
                NeckDepthMul = 0.16f,
                HeadWidthMul = 0.24f,
                HeadHeightMul = 0.20f,
                HeadDepthMul = 0.22f,
                LegWidthMul = 0.13f
            },
            AnimalType.Fox => new AnimalShape
            {
                BodyWidthMul = 0.36f,
                BodyDepthMul = 0.50f,
                BodyHeightMul = 0.38f,
                BodyCenterYFrac = 0.40f,
                NeckHeightMul = 0.06f,
                NeckDepthMul = 0.14f,
                HeadWidthMul = 0.18f,
                HeadHeightMul = 0.15f,
                HeadDepthMul = 0.17f,
                LegWidthMul = 0.08f
            },
            AnimalType.Deer => new AnimalShape
            {
                BodyWidthMul = 0.34f,
                BodyDepthMul = 0.52f,
                BodyHeightMul = 0.40f,
                BodyCenterYFrac = 0.50f,
                NeckHeightMul = 0.14f,
                NeckDepthMul = 0.18f,
                HeadWidthMul = 0.16f,
                HeadHeightMul = 0.14f,
                HeadDepthMul = 0.15f,
                LegWidthMul = 0.07f
            },
            AnimalType.Wolf => new AnimalShape
            {
                BodyWidthMul = 0.38f,
                BodyDepthMul = 0.48f,
                BodyHeightMul = 0.40f,
                BodyCenterYFrac = 0.42f,
                NeckHeightMul = 0.07f,
                NeckDepthMul = 0.15f,
                HeadWidthMul = 0.19f,
                HeadHeightMul = 0.16f,
                HeadDepthMul = 0.18f,
                LegWidthMul = 0.09f
            },
            AnimalType.Sheep => new AnimalShape
            {
                BodyWidthMul = 0.50f,
                BodyDepthMul = 0.46f,
                BodyHeightMul = 0.46f,
                BodyCenterYFrac = 0.44f,
                NeckHeightMul = 0.06f,
                NeckDepthMul = 0.14f,
                HeadWidthMul = 0.18f,
                HeadHeightMul = 0.14f,
                HeadDepthMul = 0.16f,
                LegWidthMul = 0.10f
            },
            _ => new AnimalShape
            {
                BodyWidthMul = 0.44f,
                BodyDepthMul = 0.44f,
                BodyHeightMul = 0.46f,
                BodyCenterYFrac = 0.40f,
                NeckHeightMul = 0.06f,
                NeckDepthMul = 0.14f,
                HeadWidthMul = 0.18f,
                HeadHeightMul = 0.15f,
                HeadDepthMul = 0.16f,
                LegWidthMul = 0.10f
            }
        };

        public static bool UsesAccentBox(AnimalType type) =>
            type is AnimalType.Chicken;

        public static float GetWalkPhase(Animal animal, float animTime)
        {
            float basePhase = animal.Position.X * 9f + animal.Position.Z * 13f + animal.Id * 0.3f;

            if (animal.IsPanicking)
            {
                return basePhase + animTime * 18f;
            }

            bool isMoving = animal.WanderDistanceRemaining > 0f ||
                MathF.Abs(animal.Velocity.X) > 0.01f ||
                MathF.Abs(animal.Velocity.Z) > 0.01f;

            if (isMoving)
            {
                float pace = animal.IsGrounded ? 6.5f : 3.2f;
                return basePhase + animTime * pace;
            }

            return basePhase + animTime * 0.9f + animal.Id * 0.11f;
        }

        public static void DrawLegs(
            Matrix world,
            AnimalType type,
            AnimalStats stats,
            AnimalShape shape,
            float walkPhase,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            var layout = AnimalBodyLayout.From(shape, stats);
            float stride = MathF.Sin(walkPhase * 10f);
            float legSwing = stride * layout.Height * 0.04f;
            float bodyBob = MathF.Sin(walkPhase * 2.1f) * layout.Height * 0.008f;
            float chestLift = MathF.Sin(walkPhase * 1.7f + 0.5f) * layout.Height * 0.006f;

            switch (type)
            {
                case AnimalType.Chicken:
                    DrawChickenLegs(world, stats, layout, walkPhase, drawColored);
                    break;
                case AnimalType.Sheep:
                    DrawQuadrupedLegs(world, layout, stats.BodyColor * 0.9f, new Color(40, 35, 30), legSwing + bodyBob, 1.15f, drawColored);
                    break;
                case AnimalType.Pig:
                    DrawQuadrupedLegs(world, layout, Darken(stats.BodyColor, 0.10f), new Color(45, 30, 25), legSwing + bodyBob * 0.8f, 1.0f, drawColored);
                    break;
                case AnimalType.Cow:
                    DrawQuadrupedLegs(world, layout, new Color(70, 50, 35), new Color(35, 25, 18), legSwing + bodyBob * 0.7f, 1.25f, drawColored);
                    break;
                case AnimalType.Bear:
                    DrawQuadrupedLegs(world, layout, Darken(stats.BodyColor, 0.14f), new Color(20, 15, 10), legSwing + bodyBob * 0.6f, 1.05f, drawColored);
                    break;
                case AnimalType.Fox:
                    DrawQuadrupedLegs(world, layout, Darken(stats.BodyColor, 0.12f), new Color(30, 25, 20), legSwing + bodyBob * 1.2f, 1.0f, drawColored);
                    break;
                case AnimalType.Deer:
                    DrawQuadrupedLegs(world, layout, new Color(130, 95, 60), new Color(90, 65, 40), legSwing + bodyBob * 0.9f, 1.45f, drawColored);
                    break;
                case AnimalType.Wolf:
                    DrawQuadrupedLegs(world, layout, Darken(stats.BodyColor, 0.08f), new Color(25, 22, 20), legSwing + bodyBob * 0.7f, 1.05f, drawColored);
                    break;
            }
        }

        public static void DrawNeck(
            Matrix world,
            AnimalType type,
            AnimalStats stats,
            AnimalShape shape,
            float motionPhase,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            if (type == AnimalType.Chicken)
            {
                return;
            }

            var layout = AnimalBodyLayout.From(shape, stats);
            if (layout.NeckHalfH <= 0.001f)
            {
                return;
            }

            var neckColor = type is AnimalType.Cow or AnimalType.Deer or AnimalType.Sheep
                ? stats.HeadColor
                : stats.BodyColor;
            float neckBob = MathF.Sin(motionPhase * 1.7f) * stats.Height * 0.007f;
            float neckTilt = MathF.Sin(motionPhase * 1.2f) * layout.Width * 0.015f;
            drawColored(world, layout.NeckHalfW, layout.NeckHalfH, layout.NeckHalfD,
                neckTilt, layout.NeckCenterY + neckBob, layout.NeckCenterZ, neckColor);
        }

        public static void DrawFeatures(
            Matrix world,
            AnimalType type,
            AnimalStats stats,
            AnimalShape shape,
            float walkPhase,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            var layout = AnimalBodyLayout.From(shape, stats);
            float sway = MathF.Sin(walkPhase * 1.75f) * layout.Width * 0.01f;
            float headLift = MathF.Sin(walkPhase * 1.2f + 0.3f) * layout.Height * 0.004f;

            switch (type)
            {
                case AnimalType.Chicken:
                    DrawChickenFeatures(world, stats, layout, drawColored);
                    break;
                case AnimalType.Sheep:
                    DrawFloppyEars(world, layout, stats.HeadColor, drawColored);
                    DrawTail(world, layout.Width, -0.30f + sway, layout.BodyCenterY + headLift, 0.05f, 0.06f, 0.04f, stats.BodyColor, drawColored);
                    DrawWoolTufts(world, layout, stats.BodyColor, drawColored);
                    DrawColoredSnout(world, layout, stats.HeadColor, drawColored);
                    break;
                case AnimalType.Pig:
                    DrawFloppyEars(world, layout, stats.HeadColor, drawColored);
                    DrawColoredSnout(world, layout, stats.AccentColor, drawColored);
                    DrawTail(world, layout.Width, -0.34f + sway * 0.7f, layout.BodyCenterY + layout.Height * 0.02f + headLift, 0.04f, 0.05f, 0.04f, stats.AccentColor, drawColored);
                    break;
                case AnimalType.Cow:
                    DrawHorns(world, layout, new Color(200, 195, 175), drawColored);
                    DrawFloppyEars(world, layout, stats.HeadColor, drawColored);
                    DrawColoredSnout(world, layout, stats.HeadColor * 0.85f, drawColored);
                    DrawMuzzle(world, layout, stats.AccentColor, drawColored);
                    DrawTail(world, layout.Width, -0.38f + sway * 0.8f, layout.BodyCenterY + headLift, 0.03f, 0.08f, 0.03f, stats.HeadColor, drawColored);
                    DrawUdder(world, layout, stats.AccentColor, drawColored);
                    break;
                case AnimalType.Bear:
                    DrawRoundEars(world, layout, stats.HeadColor, drawColored);
                    DrawColoredSnout(world, layout, stats.AccentColor, drawColored);
                    DrawTail(world, layout.Width, -0.32f + sway * 0.6f, layout.BodyCenterY - layout.Height * 0.02f + headLift, 0.05f, 0.05f, 0.05f, stats.BodyColor, drawColored);
                    break;
                case AnimalType.Fox:
                    DrawPointyEars(world, layout, stats.BodyColor, drawColored);
                    DrawColoredSnout(world, layout, stats.AccentColor, drawColored);
                    DrawBushyTail(world, layout, stats.AccentColor, drawColored);
                    DrawChestPatch(world, layout, stats.AccentColor, drawColored);
                    break;
                case AnimalType.Deer:
                    DrawAntlers(world, layout, new Color(150, 110, 70), drawColored);
                    DrawPointyEars(world, layout, stats.HeadColor, drawColored);
                    DrawColoredSnout(world, layout, stats.HeadColor * 0.9f, drawColored);
                    DrawTail(world, layout.Width, -0.36f + sway * 0.7f, layout.BodyCenterY + layout.Height * 0.04f + headLift, 0.04f, 0.06f, 0.03f, stats.AccentColor, drawColored);
                    break;
                case AnimalType.Wolf:
                    DrawPointyEars(world, layout, stats.HeadColor, drawColored);
                    DrawColoredSnout(world, layout, stats.AccentColor, drawColored);
                    DrawBushyTail(world, layout, stats.AccentColor, drawColored);
                    break;
            }
        }

        private static void DrawQuadrupedLegs(
            Matrix world,
            AnimalBodyLayout layout,
            Color legColor,
            Color hoofColor,
            float legSwing,
            float legLengthMul,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float legHalfW = layout.LegWidth * 0.5f;
            float legHalfH = layout.BodyBottomY * 0.5f * legLengthMul;
            float legCenterY = legHalfH;
            float hoofHalfH = MathF.Min(legHalfH * 0.22f, layout.Height * 0.035f);
            float hoofCenterY = hoofHalfH;
            float spreadX = layout.Width * 0.26f;
            float spreadZ = layout.Width * 0.22f;
            float hipPadH = layout.Height * 0.035f;

            DrawLeg(world, -spreadX, spreadZ, legHalfW, legHalfH, legCenterY, hoofHalfH, hoofCenterY, legSwing, legColor, hoofColor, drawColored);
            DrawLeg(world, spreadX, spreadZ, legHalfW, legHalfH, legCenterY, hoofHalfH, hoofCenterY, -legSwing, legColor, hoofColor, drawColored);
            DrawLeg(world, -spreadX, -spreadZ, legHalfW, legHalfH, legCenterY, hoofHalfH, hoofCenterY, -legSwing, legColor, hoofColor, drawColored);
            DrawLeg(world, spreadX, -spreadZ, legHalfW, legHalfH, legCenterY, hoofHalfH, hoofCenterY, legSwing, legColor, hoofColor, drawColored);

            EntityModelPart.Draw(world,
                EntityModelPart.Box(legHalfW * 1.2f, hipPadH, legHalfW, -spreadX, layout.BodyBottomY + hipPadH, spreadZ),
                legColor, drawColored);
            EntityModelPart.Draw(world,
                EntityModelPart.Box(legHalfW * 1.2f, hipPadH, legHalfW, spreadX, layout.BodyBottomY + hipPadH, spreadZ),
                legColor, drawColored);
            EntityModelPart.Draw(world,
                EntityModelPart.Box(legHalfW * 1.2f, hipPadH, legHalfW, -spreadX, layout.BodyBottomY + hipPadH, -spreadZ),
                legColor, drawColored);
            EntityModelPart.Draw(world,
                EntityModelPart.Box(legHalfW * 1.2f, hipPadH, legHalfW, spreadX, layout.BodyBottomY + hipPadH, -spreadZ),
                legColor, drawColored);
        }

        private static void DrawLeg(
            Matrix world,
            float x, float z,
            float legHalfW, float legHalfH, float legCenterY,
            float hoofHalfH, float hoofCenterY,
            float swing,
            Color legColor, Color hoofColor,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float upperHalfH = legHalfH - hoofHalfH;
            float upperCenterY = hoofHalfH + upperHalfH;
            drawColored(world, legHalfW, upperHalfH, legHalfW, x, upperCenterY + swing, z, legColor);
            drawColored(world, legHalfW * 1.08f, hoofHalfH, legHalfW * 1.15f, x, hoofCenterY + swing, z, hoofColor);
        }

        private static void DrawChickenLegs(
            Matrix world,
            AnimalStats stats,
            AnimalBodyLayout layout,
            float walkPhase,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float w = stats.Width;
            float h = stats.Height;
            float legHalfW = layout.LegWidth * 0.5f;
            float legHalfH = layout.BodyBottomY * 0.55f;
            float legCenterY = legHalfH;
            float hoofHalfH = MathF.Min(legHalfH * 0.28f, h * 0.025f);
            float spread = w * 0.12f;
            float swing = MathF.Sin(walkPhase * 12f) * h * 0.03f;
            var legColor = new Color(255, 140, 30);

            drawColored(world, legHalfW, legHalfH - hoofHalfH, legHalfW, -spread, legCenterY + hoofHalfH + swing, w * 0.08f, legColor);
            drawColored(world, legHalfW, legHalfH - hoofHalfH, legHalfW, spread, legCenterY + hoofHalfH - swing, w * 0.08f, legColor);
            drawColored(world, legHalfW, hoofHalfH, legHalfW * 1.1f, -spread, hoofHalfH + swing, w * 0.08f, new Color(255, 100, 20));
            drawColored(world, legHalfW, hoofHalfH, legHalfW * 1.1f, spread, hoofHalfH - swing, w * 0.08f, new Color(255, 100, 20));
        }

        private static void DrawChickenFeatures(
            Matrix world,
            AnimalStats stats,
            AnimalBodyLayout layout,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float w = stats.Width;
            float h = stats.Height;
            float wingH = h * 0.10f;
            float wingW = w * 0.20f;
            float wingY = layout.BodyCenterY;
            drawColored(world, wingW * 0.5f, wingH * 0.5f, wingW * 0.22f, -w * 0.28f, wingY, w * 0.04f, stats.BodyColor);
            drawColored(world, wingW * 0.5f, wingH * 0.5f, wingW * 0.22f, w * 0.28f, wingY, w * 0.04f, stats.BodyColor);
            drawColored(world, w * 0.05f, h * 0.035f, w * 0.08f, 0f, layout.HeadCenterY + layout.HeadHalfH * 0.55f, layout.HeadForward + layout.HeadHalfD * 0.6f, new Color(200, 30, 30));
            drawColored(world, layout.HeadHalfW * 0.55f, layout.HeadHalfH * 0.35f, layout.HeadHalfD * 0.45f, 0f, layout.HeadCenterY - layout.HeadHalfH * 0.15f, layout.HeadForward + layout.HeadHalfD * 0.75f, new Color(255, 170, 40));
        }

        private static void DrawFloppyEars(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float ear = layout.HeadHalfW * 0.85f;
            drawColored(world, ear * 0.35f, ear * 0.55f, ear * 0.10f, -layout.HeadHalfW * 0.95f, layout.HeadCenterY + layout.HeadHalfH * 0.15f, layout.HeadForward, color);
            drawColored(world, ear * 0.35f, ear * 0.55f, ear * 0.10f, layout.HeadHalfW * 0.95f, layout.HeadCenterY + layout.HeadHalfH * 0.15f, layout.HeadForward, color);
        }

        private static void DrawPointyEars(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float ear = layout.Width * 0.08f;
            drawColored(world, ear * 0.35f, ear * 0.80f, ear * 0.20f, -layout.HeadHalfW * 0.75f, layout.HeadCenterY + layout.HeadHalfH * 0.55f, layout.HeadForward, color);
            drawColored(world, ear * 0.35f, ear * 0.80f, ear * 0.20f, layout.HeadHalfW * 0.75f, layout.HeadCenterY + layout.HeadHalfH * 0.55f, layout.HeadForward, color);
        }

        private static void DrawRoundEars(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float ear = layout.Width * 0.10f;
            drawColored(world, ear * 0.5f, ear * 0.5f, ear * 0.32f, -layout.HeadHalfW * 0.85f, layout.HeadCenterY + layout.HeadHalfH * 0.35f, layout.HeadForward * 0.95f, color);
            drawColored(world, ear * 0.5f, ear * 0.5f, ear * 0.32f, layout.HeadHalfW * 0.85f, layout.HeadCenterY + layout.HeadHalfH * 0.35f, layout.HeadForward * 0.95f, color);
        }

        private static void DrawHorns(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            drawColored(world, 0.03f, 0.12f, 0.03f, -layout.HeadHalfW * 0.55f, layout.HeadCenterY + layout.HeadHalfH * 0.75f, layout.HeadForward, color);
            drawColored(world, 0.03f, 0.12f, 0.03f, layout.HeadHalfW * 0.55f, layout.HeadCenterY + layout.HeadHalfH * 0.75f, layout.HeadForward, color);
        }

        private static void DrawAntlers(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            float headZ = layout.HeadForward;
            drawColored(world, 0.025f, 0.16f, 0.025f, -layout.HeadHalfW * 0.40f, layout.HeadCenterY + layout.HeadHalfH * 0.80f, headZ, color);
            drawColored(world, 0.04f, 0.04f, 0.02f, -layout.HeadHalfW * 0.55f, layout.HeadCenterY + layout.HeadHalfH * 1.05f, headZ, color);
            drawColored(world, 0.025f, 0.16f, 0.025f, layout.HeadHalfW * 0.40f, layout.HeadCenterY + layout.HeadHalfH * 0.80f, headZ, color);
            drawColored(world, 0.04f, 0.04f, 0.02f, layout.HeadHalfW * 0.55f, layout.HeadCenterY + layout.HeadHalfH * 1.05f, headZ, color);
        }

        private static void DrawTail(
            Matrix world,
            float w,
            float zFrac,
            float tailY,
            float tailW,
            float tailH,
            float tailD,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            drawColored(world, tailW, tailH * 0.5f, tailD, 0f, tailY, w * zFrac, color);
        }

        private static void DrawBushyTail(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            drawColored(world, layout.Width * 0.10f, layout.Height * 0.12f, layout.Width * 0.08f, 0f, layout.BodyCenterY, -layout.Width * 0.38f, color);
            drawColored(world, layout.Width * 0.07f, layout.Height * 0.08f, layout.Width * 0.06f, 0f, layout.BodyCenterY + layout.Height * 0.05f, -layout.Width * 0.44f, color * 0.9f);
        }

        private static void DrawChestPatch(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            drawColored(world, layout.Width * 0.16f, layout.Height * 0.12f, layout.Width * 0.05f, 0f, layout.BodyCenterY - layout.BodyHalfH * 0.2f, layout.BodyHalfD * 0.85f, color);
        }

        private static void DrawUdder(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            drawColored(world, layout.Width * 0.12f, layout.Height * 0.05f, layout.Width * 0.10f, 0f, layout.BodyBottomY + layout.Height * 0.04f, -layout.Width * 0.06f, color);
        }

        private static void DrawColoredSnout(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            drawColored(world, layout.HeadHalfW * 0.55f, layout.HeadHalfH * 0.42f, layout.HeadHalfD * 0.55f, 0f, layout.HeadCenterY - layout.HeadHalfH * 0.15f, layout.HeadForward + layout.HeadHalfD * 0.65f, color);
        }

        private static void DrawMuzzle(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            drawColored(world, layout.HeadHalfW * 0.35f, layout.HeadHalfH * 0.22f, layout.HeadHalfD * 0.25f, 0f, layout.HeadCenterY - layout.HeadHalfH * 0.35f, layout.HeadForward + layout.HeadHalfD * 0.92f, color);
        }

        private static void DrawWoolTufts(
            Matrix world,
            AnimalBodyLayout layout,
            Color color,
            Action<Matrix, float, float, float, float, float, float, Color> drawColored)
        {
            var wool = Lighten(color, 0.12f);
            drawColored(world, layout.Width * 0.11f, layout.Height * 0.07f, layout.Width * 0.09f, -layout.Width * 0.16f, layout.BodyTopY - layout.Height * 0.02f, layout.Width * 0.06f, wool);
            drawColored(world, layout.Width * 0.11f, layout.Height * 0.07f, layout.Width * 0.09f, layout.Width * 0.16f, layout.BodyTopY - layout.Height * 0.02f, -layout.Width * 0.04f, wool);
            drawColored(world, layout.Width * 0.09f, layout.Height * 0.06f, layout.Width * 0.07f, 0f, layout.BodyTopY, -layout.Width * 0.10f, wool);
        }

        private static Color Lighten(Color color, float amount) =>
            new Color(
                Math.Clamp(color.R / 255f + amount, 0f, 1f),
                Math.Clamp(color.G / 255f + amount, 0f, 1f),
                Math.Clamp(color.B / 255f + amount, 0f, 1f),
                color.A / 255f);

        private static Color Darken(Color color, float amount) =>
            new Color(
                Math.Clamp(color.R / 255f - amount, 0f, 1f),
                Math.Clamp(color.G / 255f - amount, 0f, 1f),
                Math.Clamp(color.B / 255f - amount, 0f, 1f),
                color.A / 255f);
    }
}
