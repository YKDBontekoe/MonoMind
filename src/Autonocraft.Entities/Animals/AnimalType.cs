using Microsoft.Xna.Framework;

namespace Autonocraft.Entities
{
    public enum AnimalType : byte
    {
        Sheep,
        Pig,
        Chicken,
        Wolf,
        Cow,
        Bear,
        Fox,
        Deer
    }

    public readonly struct AnimalStats
    {
        private static readonly AnimalStats Default = new(
            0.6f, 0.8f, 2.5f, 8f, 1f,
            Color.White,
            Color.Gray,
            Color.Transparent,
            false);

        private static readonly AnimalStats[] Catalog =
        {
            new(
                0.9f, 1.0f, 2.2f, 8f, 1f,
                new Color(240, 240, 245),
                new Color(50, 50, 55),
                Color.Transparent,
                false),
            new(
                0.9f, 0.8f, 2.8f, 10f, 2f,
                new Color(240, 170, 180),
                new Color(220, 140, 150),
                new Color(200, 120, 130),
                true),
            new(
                0.5f, 0.6f, 3.5f, 4f, 1f,
                new Color(245, 245, 240),
                new Color(200, 40, 40),
                new Color(240, 200, 40),
                true),
            new(
                0.9f, 0.9f, 4.5f, 12f, 3.5f,
                new Color(55, 55, 60),
                new Color(35, 35, 40),
                new Color(80, 80, 85),
                true),
            new(
                1.2f, 1.4f, 2.0f, 15f, 0f,
                new Color(90, 60, 40),
                new Color(60, 40, 25),
                new Color(240, 240, 240),
                true),
            new(
                1.4f, 1.6f, 3.5f, 30f, 5f,
                new Color(40, 25, 15),
                new Color(30, 20, 10),
                new Color(15, 10, 5),
                true),
            new(
                0.6f, 0.6f, 4.8f, 8f, 1f,
                new Color(230, 110, 30),
                new Color(245, 245, 245),
                new Color(40, 40, 40),
                true),
            new(
                1.0f, 1.5f, 4.0f, 12f, 0f,
                new Color(160, 110, 70),
                new Color(130, 90, 50),
                new Color(240, 240, 240),
                true)
        };

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
            int index = (int)type;
            return index >= 0 && index < Catalog.Length ? Catalog[index] : Default;
        }
    }
}
