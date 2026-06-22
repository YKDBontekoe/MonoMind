namespace Autonocraft.World.Generation.Trees
{
    public enum TreeShapeKind
    {
        Column,
        Conical,
        Round,
        Fan,
        Weeping,
        MultiTrunk
    }

    public readonly struct TreeSpecies
    {
        public BlockType Log { get; init; }
        public BlockType Leaves { get; init; }
        public TreeShapeKind Shape { get; init; }
        public int MinHeight { get; init; }
        public int MaxHeight { get; init; }
        public float BranchAngle { get; init; }
        public float BranchChance { get; init; }
        public int MaxBlocks { get; init; }

        public static TreeSpecies Oak() => new()
        {
            Log = BlockType.OakLog,
            Leaves = BlockType.OakLeaves,
            Shape = TreeShapeKind.Round,
            MinHeight = 4,
            MaxHeight = 7,
            BranchAngle = 35f,
            BranchChance = 0.30f,
            MaxBlocks = 140
        };

        public static TreeSpecies Birch() => new()
        {
            Log = BlockType.BirchLog,
            Leaves = BlockType.BirchLeaves,
            Shape = TreeShapeKind.Round,
            MinHeight = 5,
            MaxHeight = 8,
            BranchAngle = 30f,
            BranchChance = 0.25f,
            MaxBlocks = 64
        };

        public static TreeSpecies Pine() => new()
        {
            Log = BlockType.PineLog,
            Leaves = BlockType.PineLeaves,
            Shape = TreeShapeKind.Conical,
            MinHeight = 5,
            MaxHeight = 9,
            BranchAngle = 45f,
            BranchChance = 0.40f,
            MaxBlocks = 96
        };

        public static TreeSpecies Willow() => new()
        {
            Log = BlockType.WillowLog,
            Leaves = BlockType.WillowLeaves,
            Shape = TreeShapeKind.Weeping,
            MinHeight = 5,
            MaxHeight = 8,
            BranchAngle = 25f,
            BranchChance = 0.35f,
            MaxBlocks = 80
        };

        public static TreeSpecies Palm() => new()
        {
            Log = BlockType.PalmLog,
            Leaves = BlockType.PalmLeaves,
            Shape = TreeShapeKind.Fan,
            MinHeight = 4,
            MaxHeight = 6,
            BranchAngle = 20f,
            BranchChance = 0.10f,
            MaxBlocks = 48
        };

        public static TreeSpecies Cherry() => new()
        {
            Log = BlockType.CherryLog,
            Leaves = BlockType.CherryLeaves,
            Shape = TreeShapeKind.Round,
            MinHeight = 4,
            MaxHeight = 6,
            BranchAngle = 28f,
            BranchChance = 0.22f,
            MaxBlocks = 64
        };

        public static TreeSpecies Mahogany() => new()
        {
            Log = BlockType.MahoganyLog,
            Leaves = BlockType.MahoganyLeaves,
            Shape = TreeShapeKind.Round,
            MinHeight = 4,
            MaxHeight = 7,
            BranchAngle = 32f,
            BranchChance = 0.28f,
            MaxBlocks = 96
        };

        public static TreeSpecies Maple() => new()
        {
            Log = BlockType.MapleLog,
            Leaves = BlockType.MapleLeaves,
            Shape = TreeShapeKind.Round,
            MinHeight = 5,
            MaxHeight = 8,
            BranchAngle = 30f,
            BranchChance = 0.24f,
            MaxBlocks = 64
        };
    }

    public readonly struct TreeVoxel(int dx, int dy, int dz, BlockType type)
    {
        public int Dx { get; } = dx;
        public int Dy { get; } = dy;
        public int Dz { get; } = dz;
        public BlockType Type { get; } = type;
    }
}
