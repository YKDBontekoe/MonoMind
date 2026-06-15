using Microsoft.Xna.Framework;

namespace Autonocraft.Entities
{
    public enum AnimalType : byte
    {
        Sheep,
        Pig,
        Chicken,
        Wolf
    }

    public readonly struct AnimalStats
    {
        public float Width { get; }
        public float Height { get; }
        public float WalkSpeed { get; }
        public float MaxHealth { get; }
        public float RetaliationDamage { get; }
        public Color BodyColor { get; }
        public Color HeadColor { get; }
        public Color AccentColor { get; }
        public bool HasAccent { get; }

        public AnimalStats(
            float width,
            float height,
            float walkSpeed,
            float maxHealth,
            float retaliationDamage,
            Color bodyColor,
            Color headColor,
            Color accentColor,
            bool hasAccent)
        {
            Width = width;
            Height = height;
            WalkSpeed = walkSpeed;
            MaxHealth = maxHealth;
            RetaliationDamage = retaliationDamage;
            BodyColor = bodyColor;
            HeadColor = headColor;
            AccentColor = accentColor;
            HasAccent = hasAccent;
        }

        public static AnimalStats For(AnimalType type)
        {
            return type switch
            {
                AnimalType.Sheep => new AnimalStats(
                    0.9f, 1.0f, 2.2f, 8f, 1f,
                    new Color(240, 240, 245),
                    new Color(50, 50, 55),
                    Color.Transparent,
                    false),
                AnimalType.Pig => new AnimalStats(
                    0.9f, 0.8f, 2.8f, 10f, 2f,
                    new Color(240, 170, 180),
                    new Color(220, 140, 150),
                    new Color(200, 120, 130),
                    true),
                AnimalType.Chicken => new AnimalStats(
                    0.5f, 0.6f, 3.5f, 4f, 1f,
                    new Color(245, 245, 240),
                    new Color(200, 40, 40),
                    new Color(240, 200, 40),
                    true),
                AnimalType.Wolf => new AnimalStats(
                    0.9f, 1.0f, 4.8f, 12f, 3f,
                    new Color(70, 72, 78),
                    new Color(45, 48, 55),
                    new Color(90, 92, 98),
                    true),
                _ => new AnimalStats(0.6f, 0.8f, 2.5f, 8f, 1f, Color.White, Color.Gray, Color.Transparent, false)
            };
        }
    }
}
