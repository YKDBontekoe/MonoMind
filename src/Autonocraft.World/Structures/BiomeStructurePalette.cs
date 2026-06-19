namespace Autonocraft.World.Structures
{
    public readonly struct BiomeStructurePalette
    {
        public BlockType Foundation { get; init; }
        public BlockType Floor { get; init; }
        public BlockType Wall { get; init; }
        public BlockType WallAccent { get; init; }
        public BlockType Roof { get; init; }
        public BlockType Pillar { get; init; }
        public BlockType Trim { get; init; }
        public BlockType Window { get; init; }
        public BlockType Accent { get; init; }
        public BlockType Path { get; init; }
        public BlockType GlowAccent { get; init; }
        public BlockType Ruin { get; init; }

        public static BiomeStructurePalette For(BiomeType biome) => biome switch
        {
            BiomeType.Desert or BiomeType.Badlands => new BiomeStructurePalette
            {
                Foundation = BlockType.Sandstone,
                Floor = BlockType.Sandstone,
                Wall = BlockType.Sandstone,
                WallAccent = BlockType.RedSand,
                Roof = BlockType.Sandstone,
                Pillar = BlockType.Sandstone,
                Trim = BlockType.GoldBlock,
                Window = BlockType.Glass,
                Accent = BlockType.GoldBlock,
                Path = BlockType.Sand,
                GlowAccent = BlockType.Lantern,
                Ruin = BlockType.RedSand
            },
            BiomeType.Swamp or BiomeType.Mangrove => new BiomeStructurePalette
            {
                Foundation = BlockType.Cobblestone,
                Floor = BlockType.MossStone,
                Wall = BlockType.MossStone,
                WallAccent = BlockType.WillowLog,
                Roof = BlockType.WillowLog,
                Pillar = BlockType.WillowLog,
                Trim = BlockType.Obsidian,
                Window = BlockType.RedStainedGlass,
                Accent = BlockType.Obsidian,
                Path = BlockType.Mud,
                GlowAccent = BlockType.Lantern,
                Ruin = BlockType.MossStone
            },
            BiomeType.SnowyPeaks or BiomeType.Mountains => new BiomeStructurePalette
            {
                Foundation = BlockType.Slate,
                Floor = BlockType.Slate,
                Wall = BlockType.Snow,
                WallAccent = BlockType.SlateBrick,
                Roof = BlockType.Snow,
                Pillar = BlockType.Slate,
                Trim = BlockType.Marble,
                Window = BlockType.Glass,
                Accent = BlockType.Marble,
                Path = BlockType.Gravel,
                GlowAccent = BlockType.Lantern,
                Ruin = BlockType.Snow
            },
            BiomeType.Volcanic => new BiomeStructurePalette
            {
                Foundation = BlockType.Basalt,
                Floor = BlockType.Basalt,
                Wall = BlockType.Obsidian,
                WallAccent = BlockType.Basalt,
                Roof = BlockType.Basalt,
                Pillar = BlockType.Basalt,
                Trim = BlockType.MagmaBlock,
                Window = BlockType.RedStainedGlass,
                Accent = BlockType.MagmaBlock,
                Path = BlockType.Basalt,
                GlowAccent = BlockType.MagmaBlock,
                Ruin = BlockType.BasaltBrick
            },
            BiomeType.Beach or BiomeType.MushroomForest => new BiomeStructurePalette
            {
                Foundation = BlockType.Sand,
                Floor = BlockType.OakPlank,
                Wall = BlockType.PalmLog,
                WallAccent = BlockType.OakPlank,
                Roof = BlockType.PalmLeaves,
                Pillar = BlockType.PalmLog,
                Trim = BlockType.OakLog,
                Window = BlockType.Glass,
                Accent = BlockType.Glowshroom,
                Path = BlockType.Sand,
                GlowAccent = BlockType.Glowshroom,
                Ruin = BlockType.MossStone
            },
            BiomeType.Forest => new BiomeStructurePalette
            {
                Foundation = BlockType.Cobblestone,
                Floor = BlockType.OakPlank,
                Wall = BlockType.OakPlank,
                WallAccent = BlockType.OakLog,
                Roof = BlockType.BirchPlank,
                Pillar = BlockType.OakLog,
                Trim = BlockType.BirchPlank,
                Window = BlockType.Glass,
                Accent = BlockType.Lantern,
                Path = BlockType.Gravel,
                GlowAccent = BlockType.Lantern,
                Ruin = BlockType.MossStone
            },
            BiomeType.Jungle => new BiomeStructurePalette
            {
                Foundation = BlockType.Cobblestone,
                Floor = BlockType.MahoganyPlank,
                Wall = BlockType.MahoganyPlank,
                WallAccent = BlockType.MahoganyLog,
                Roof = BlockType.MaplePlank,
                Pillar = BlockType.MahoganyLog,
                Trim = BlockType.MaplePlank,
                Window = BlockType.Glass,
                Accent = BlockType.Lantern,
                Path = BlockType.Dirt,
                GlowAccent = BlockType.Lantern,
                Ruin = BlockType.MossStone
            },
            _ => new BiomeStructurePalette
            {
                Foundation = BlockType.Cobblestone,
                Floor = BlockType.OakPlank,
                Wall = BlockType.OakPlank,
                WallAccent = BlockType.OakLog,
                Roof = BlockType.BirchPlank,
                Pillar = BlockType.OakLog,
                Trim = BlockType.SlateBrick,
                Window = BlockType.Glass,
                Accent = BlockType.Lantern,
                Path = BlockType.Gravel,
                GlowAccent = BlockType.Lantern,
                Ruin = BlockType.MossStone
            }
        };

        public static BiomeStructurePalette ForMega(BiomeType biome) => biome switch
        {
            BiomeType.Desert or BiomeType.Badlands => new BiomeStructurePalette
            {
                Foundation = BlockType.Sandstone,
                Floor = BlockType.MarbleBrick,
                Wall = BlockType.Brick,
                WallAccent = BlockType.Sandstone,
                Roof = BlockType.Sandstone,
                Pillar = BlockType.MarbleBrick,
                Trim = BlockType.GoldBlock,
                Window = BlockType.Glass,
                Accent = BlockType.GoldBlock,
                Path = BlockType.Sandstone,
                GlowAccent = BlockType.Lantern,
                Ruin = BlockType.RedSand
            },
            BiomeType.Swamp or BiomeType.Mangrove => new BiomeStructurePalette
            {
                Foundation = BlockType.Cobblestone,
                Floor = BlockType.MossStone,
                Wall = BlockType.MossStone,
                WallAccent = BlockType.Brick,
                Roof = BlockType.SlateBrick,
                Pillar = BlockType.WillowLog,
                Trim = BlockType.Obsidian,
                Window = BlockType.RedStainedGlass,
                Accent = BlockType.Lantern,
                Path = BlockType.Mud,
                GlowAccent = BlockType.Lantern,
                Ruin = BlockType.MossStone
            },
            BiomeType.SnowyPeaks or BiomeType.Mountains => new BiomeStructurePalette
            {
                Foundation = BlockType.Slate,
                Floor = BlockType.PolishedMarble,
                Wall = BlockType.SlateBrick,
                WallAccent = BlockType.MarbleBrick,
                Roof = BlockType.Snow,
                Pillar = BlockType.Marble,
                Trim = BlockType.Marble,
                Window = BlockType.Glass,
                Accent = BlockType.Lantern,
                Path = BlockType.Gravel,
                GlowAccent = BlockType.Lantern,
                Ruin = BlockType.Snow
            },
            BiomeType.Volcanic => new BiomeStructurePalette
            {
                Foundation = BlockType.BasaltBrick,
                Floor = BlockType.Basalt,
                Wall = BlockType.Obsidian,
                WallAccent = BlockType.BasaltBrick,
                Roof = BlockType.Basalt,
                Pillar = BlockType.Basalt,
                Trim = BlockType.MagmaBlock,
                Window = BlockType.RedStainedGlass,
                Accent = BlockType.MagmaBlock,
                Path = BlockType.Basalt,
                GlowAccent = BlockType.MagmaBlock,
                Ruin = BlockType.BasaltBrick
            },
            _ => new BiomeStructurePalette
            {
                Foundation = BlockType.Cobblestone,
                Floor = BlockType.PolishedMarble,
                Wall = BlockType.MossStone,
                WallAccent = BlockType.Brick,
                Roof = BlockType.SlateBrick,
                Pillar = BlockType.MarbleBrick,
                Trim = BlockType.Marble,
                Window = BlockType.Glass,
                Accent = BlockType.Lantern,
                Path = BlockType.Cobblestone,
                GlowAccent = BlockType.Lantern,
                Ruin = BlockType.MossStone
            }
        };
    }
}
