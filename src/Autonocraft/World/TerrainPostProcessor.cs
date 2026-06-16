using System;
using System.Collections.Generic;

namespace Autonocraft.World
{
    public static class TerrainPostProcessor
    {
        private const int Padding = 8;
        private const int PaddedWidth = Chunk.Width + Padding * 2;
        private const int PaddedDepth = Chunk.Depth + Padding * 2;
        private const float RiverFlowThreshold = 32f;
        private const float TributaryFlowThreshold = 10f;

        [ThreadStatic]
        private static float[,]? _heightsScratch;
        [ThreadStatic]
        private static float[,]? _smoothScratch;
        [ThreadStatic]
        private static float[,]? _rawHeightsScratch;
        [ThreadStatic]
        private static TerrainColumn[,]? _draftsScratch;
        [ThreadStatic]
        private static float[,]? _flowScratch;
        [ThreadStatic]
        private static int[,]? _downstreamXScratch;
        [ThreadStatic]
        private static int[,]? _downstreamZScratch;
        [ThreadStatic]
        private static List<(int x, int z, float height)>? _riverCellsScratch;

        private static void EnsureScratch()
        {
            _heightsScratch ??= new float[PaddedWidth, PaddedDepth];
            _smoothScratch ??= new float[PaddedWidth, PaddedDepth];
            _rawHeightsScratch ??= new float[PaddedWidth, PaddedDepth];
            _draftsScratch ??= new TerrainColumn[PaddedWidth, PaddedDepth];
            _flowScratch ??= new float[PaddedWidth, PaddedDepth];
            _downstreamXScratch ??= new int[PaddedWidth, PaddedDepth];
            _downstreamZScratch ??= new int[PaddedWidth, PaddedDepth];
            _riverCellsScratch ??= new List<(int x, int z, float height)>(PaddedWidth * PaddedDepth);
        }

        public static void ProcessChunk(
            int chunkX,
            int chunkZ,
            TerrainColumn[,] columns,
            Func<int, int, (float height, TerrainColumn draft)> sampleBase,
            bool enableRivers)
        {
            EnsureScratch();
            var heights = _heightsScratch!;
            var rawHeights = _rawHeightsScratch!;
            var drafts = _draftsScratch!;
            int width = PaddedWidth;
            int depth = PaddedDepth;
            int originX = chunkX * Chunk.Width - Padding;
            int originZ = chunkZ * Chunk.Depth - Padding;

            for (int lz = 0; lz < depth; lz++)
            {
                for (int lx = 0; lx < width; lx++)
                {
                    var sample = sampleBase(originX + lx, originZ + lz);
                    heights[lx, lz] = sample.height;
                    rawHeights[lx, lz] = sample.height;
                    drafts[lx, lz] = sample.draft;
                }
            }

            SmoothLandHeights(heights, drafts, passes: 5, maxSlope: 0.85f);
            SmoothCoastalHeights(heights, drafts, passes: 3);
            EnforcePlayableSlope(heights, drafts, maxStep: 1f);

            if (enableRivers)
            {
                CarveFlowRivers(originX, originZ, heights, drafts, rawHeights);
            }

            StabilizeCoastalHeights(heights, drafts);

            for (int lz = 0; lz < Chunk.Depth; lz++)
            {
                for (int lx = 0; lx < Chunk.Width; lx++)
                {
                    int px = lx + Padding;
                    int pz = lz + Padding;
                    columns[lx, lz] = FinalizeColumn(drafts[px, pz], heights[px, pz]);
                }
            }
        }

        private static void SmoothLandHeights(float[,] heights, TerrainColumn[,] drafts, int passes, float maxSlope)
        {
            int width = heights.GetLength(0);
            int depth = heights.GetLength(1);
            var next = _smoothScratch!;

            for (int pass = 0; pass < passes; pass++)
            {
                Array.Copy(heights, next, heights.Length);
                for (int z = 1; z < depth - 1; z++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        if (!IsSmoothableLand(drafts[x, z]))
                        {
                            continue;
                        }

                        float total = heights[x, z] * 4f;
                        float weight = 4f;

                        AccumulateNeighbor(x - 1, z, ref total, ref weight);
                        AccumulateNeighbor(x + 1, z, ref total, ref weight);
                        AccumulateNeighbor(x, z - 1, ref total, ref weight);
                        AccumulateNeighbor(x, z + 1, ref total, ref weight);
                        AccumulateNeighbor(x - 1, z - 1, ref total, ref weight);
                        AccumulateNeighbor(x + 1, z - 1, ref total, ref weight);
                        AccumulateNeighbor(x - 1, z + 1, ref total, ref weight);
                        AccumulateNeighbor(x + 1, z + 1, ref total, ref weight);

                        float target = total / weight;
                        float delta = Math.Clamp(target - heights[x, z], -maxSlope, maxSlope);
                        next[x, z] = heights[x, z] + delta;

                        void AccumulateNeighbor(int nx, int nz, ref float sum, ref float w)
                        {
                            if (!IsSmoothableLand(drafts[nx, nz]))
                            {
                                return;
                            }

                            sum += heights[nx, nz];
                            w += 1f;
                        }
                    }
                }

                SwapHeightBuffers(ref heights, ref next);
            }

            if (!ReferenceEquals(heights, _heightsScratch))
            {
                Array.Copy(heights, _heightsScratch!, heights.Length);
            }
        }

        private static void SmoothCoastalHeights(float[,] heights, TerrainColumn[,] drafts, int passes)
        {
            int width = heights.GetLength(0);
            int depth = heights.GetLength(1);
            var next = _smoothScratch!;

            for (int pass = 0; pass < passes; pass++)
            {
                Array.Copy(heights, next, heights.Length);
                for (int z = 1; z < depth - 1; z++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        if (!IsCoastalCell(drafts[x, z]))
                        {
                            continue;
                        }

                        float total = heights[x, z] * 3f;
                        float weight = 3f;

                        AccumulateCoastalNeighbor(x - 1, z, ref total, ref weight);
                        AccumulateCoastalNeighbor(x + 1, z, ref total, ref weight);
                        AccumulateCoastalNeighbor(x, z - 1, ref total, ref weight);
                        AccumulateCoastalNeighbor(x, z + 1, ref total, ref weight);

                        float target = total / weight;
                        next[x, z] = Lerp(heights[x, z], target, 0.55f);

                        void AccumulateCoastalNeighbor(int nx, int nz, ref float sum, ref float w)
                        {
                            if (!IsCoastalCell(drafts[nx, nz]) && !IsWaterCell(drafts[nx, nz]))
                            {
                                return;
                            }

                            sum += heights[nx, nz];
                            w += 1f;
                        }
                    }
                }

                SwapHeightBuffers(ref heights, ref next);
            }

            if (!ReferenceEquals(heights, _heightsScratch))
            {
                Array.Copy(heights, _heightsScratch!, heights.Length);
            }
        }

        private static void SwapHeightBuffers(ref float[,] primary, ref float[,] secondary)
        {
            var temp = primary;
            primary = secondary;
            secondary = temp;
        }

        private static void StabilizeCoastalHeights(float[,] heights, TerrainColumn[,] drafts)
        {
            int width = heights.GetLength(0);
            int depth = heights.GetLength(1);

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    var draft = drafts[x, z];
                    if (draft.Biome.Primary == BiomeType.Beach)
                    {
                        heights[x, z] = Math.Clamp(
                            heights[x, z],
                            WorldConstants.SeaLevel + 0.5f,
                            WorldConstants.BeachMaxHeight);
                    }
                    else if (draft.Biome.Primary == BiomeType.Ocean)
                    {
                        float shelf = SmoothStep(-0.24f, -0.55f, draft.Biome.Continentalness);
                        float minFloor = Lerp(WorldConstants.SeaLevel - 3f, WorldConstants.SeaLevel - 22f, shelf);
                        heights[x, z] = MathF.Min(heights[x, z], minFloor + 0.5f);
                    }
                }
            }
        }

        private static void EnforcePlayableSlope(float[,] heights, TerrainColumn[,] drafts, float maxStep)
        {
            int width = heights.GetLength(0);
            int depth = heights.GetLength(1);

            for (int pass = 0; pass < 3; pass++)
            {
                for (int z = 1; z < depth - 1; z++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        if (!IsPlayableLand(drafts[x, z]))
                        {
                            continue;
                        }

                        float center = heights[x, z];
                        AdjustTowardNeighbor(x - 1, z);
                        AdjustTowardNeighbor(x + 1, z);
                        AdjustTowardNeighbor(x, z - 1);
                        AdjustTowardNeighbor(x, z + 1);

                        void AdjustTowardNeighbor(int nx, int nz)
                        {
                            if (!IsPlayableLand(drafts[nx, nz]))
                            {
                                return;
                            }

                            float neighbor = heights[nx, nz];
                            if (center - neighbor > maxStep)
                            {
                                heights[x, z] = neighbor + maxStep;
                                center = heights[x, z];
                            }
                            else if (neighbor - center > maxStep)
                            {
                                heights[x, z] = neighbor - maxStep;
                                center = heights[x, z];
                            }
                        }
                    }
                }
            }
        }

        private static void CarveFlowRivers(int originX, int originZ, float[,] heights, TerrainColumn[,] drafts, float[,] rawHeights)
        {
            int width = heights.GetLength(0);
            int depth = heights.GetLength(1);
            var flow = _flowScratch!;
            var downstreamX = _downstreamXScratch!;
            var downstreamZ = _downstreamZScratch!;
            var cells = _riverCellsScratch!;
            cells.Clear();

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int wx = originX + x;
                    int wz = originZ + z;
                    flow[x, z] = GetInitialFlow(wx, wz, drafts[x, z], heights, rawHeights, x, z);
                    var (dx, dz) = FindDownstream(x, z, heights, drafts);
                    downstreamX[x, z] = dx;
                    downstreamZ[x, z] = dz;
                }
            }

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (flow[x, z] > 0f)
                    {
                        cells.Add((x, z, heights[x, z]));
                    }
                }
            }

            cells.Sort((a, b) => b.height.CompareTo(a.height));

            foreach (var cell in cells)
            {
                int dx = downstreamX[cell.x, cell.z];
                int dz = downstreamZ[cell.x, cell.z];
                if (dx < 0)
                {
                    continue;
                }

                flow[dx, dz] += flow[cell.x, cell.z];
            }

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsRiverCapable(drafts[x, z]))
                    {
                        continue;
                    }

                    float amount = flow[x, z];
                    if (amount < TributaryFlowThreshold)
                    {
                        continue;
                    }

                    bool mainChannel = amount >= RiverFlowThreshold;
                    float carveStrength = mainChannel ? 1f : SmoothStep(TributaryFlowThreshold, RiverFlowThreshold, amount);
                    float bedHeight = WorldConstants.SeaLevel + 0.5f - MathF.Log(amount + 1f) * 0.12f;
                    bedHeight = MathF.Max(bedHeight, WorldConstants.SeaLevel - 0.25f);

                    if (mainChannel)
                    {
                        heights[x, z] = MathF.Min(heights[x, z], bedHeight);
                        WidenRiverBed(heights, drafts, x, z, amount);
                    }
                    else
                    {
                        heights[x, z] = Lerp(heights[x, z], bedHeight, carveStrength * 0.65f);
                    }

                    bool isRiverCell = heights[x, z] <= WorldConstants.SeaLevel + 0.5f;
                    drafts[x, z] = drafts[x, z] with
                    {
                        RiverStrength = Math.Clamp(amount / 60f, 0f, 1f),
                        IsRiver = isRiverCell
                    };
                }
            }
        }

        private static void WidenRiverBed(float[,] heights, TerrainColumn[,] drafts, int centerX, int centerZ, float flow)
        {
            int radius = flow >= 55f ? 2 : 1;
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    int x = centerX + dx;
                    int z = centerZ + dz;
                    if (x < 0 || z < 0 || x >= heights.GetLength(0) || z >= heights.GetLength(1))
                    {
                        continue;
                    }

                    if (!IsRiverCapable(drafts[x, z]))
                    {
                        continue;
                    }

                    float bedHeight = WorldConstants.SeaLevel + 0.5f;
                    heights[x, z] = MathF.Min(heights[x, z], Lerp(heights[x, z], bedHeight, 0.45f));
                }
            }
        }

        private static (int x, int z) FindDownstream(int x, int z, float[,] heights, TerrainColumn[,] drafts)
        {
            float current = heights[x, z];
            if (current <= WorldConstants.SeaLevel + 1f || IsWaterCell(drafts[x, z]))
            {
                return (-1, -1);
            }

            int bestX = -1;
            int bestZ = -1;
            float bestScore = 0f;
            float currentContinentalness = drafts[x, z].Biome.Continentalness;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    int nx = x + dx;
                    int nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= heights.GetLength(0) || nz >= heights.GetLength(1))
                    {
                        continue;
                    }

                    float neighbor = heights[nx, nz];
                    float drop = current - neighbor;
                    if (drop <= 0.005f)
                    {
                        continue;
                    }

                    float dist = MathF.Sqrt(dx * dx + dz * dz);
                    float coastBias = (currentContinentalness - drafts[nx, nz].Biome.Continentalness) * 0.35f;
                    float score = (drop + coastBias) / dist;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestX = nx;
                        bestZ = nz;
                    }
                }
            }

            return (bestX, bestZ);
        }

        private static TerrainColumn FinalizeColumn(TerrainColumn draft, float height)
        {
            int surfaceHeight = Math.Clamp((int)MathF.Round(height), 1, Chunk.Height - 12);
            bool isRiver = draft.IsRiver;

            BlockType surface = draft.SurfaceBlock;
            BlockType subsurface = draft.SubsurfaceBlock;

            if (draft.Biome.Primary == BiomeType.Beach)
            {
                surfaceHeight = Math.Clamp(surfaceHeight, WorldConstants.SeaLevel, WorldConstants.BeachMaxHeight);
            }

            if (isRiver)
            {
                surface = draft.RiverStrength > 0.65f ? BlockType.Gravel : BlockType.Sand;
                subsurface = surface;
            }
            else if (draft.IsLake)
            {
                surfaceHeight = Math.Min(surfaceHeight, WorldConstants.SeaLevel - 1);
            }

            return draft with
            {
                SurfaceHeight = surfaceHeight,
                SurfaceBlock = surface,
                SubsurfaceBlock = subsurface
            };
        }

        private static bool IsSmoothableLand(TerrainColumn column)
        {
            return column.Biome.Primary is BiomeType.Plains
                or BiomeType.Forest
                or BiomeType.Swamp
                or BiomeType.Desert
                or BiomeType.Beach;
        }

        private static bool IsCoastalCell(TerrainColumn column)
        {
            return column.Biome.Primary is BiomeType.Beach
                || (column.Biome.Continentalness < 0.02f && IsSmoothableLand(column));
        }

        private static bool IsWaterCell(TerrainColumn column)
        {
            return column.Biome.Primary is BiomeType.Ocean or BiomeType.Beach;
        }

        private static bool IsRiverCapable(TerrainColumn column)
        {
            return column.Biome.Primary is BiomeType.Plains
                or BiomeType.Forest
                or BiomeType.Mountains
                or BiomeType.SnowyPeaks
                or BiomeType.Swamp;
        }

        private static bool IsPlayableLand(TerrainColumn column)
        {
            return column.Biome.Primary is BiomeType.Plains or BiomeType.Forest or BiomeType.Swamp or BiomeType.Desert;
        }

        private static float GetInitialFlow(int wx, int wz, TerrainColumn column, float[,] heights, float[,] rawHeights, int x, int z)
        {
            if (!IsRiverCapable(column))
            {
                return 0f;
            }

            float height = heights[x, z];
            float flow = 0f;

            if (column.Biome.Primary is BiomeType.Mountains or BiomeType.SnowyPeaks && height > WorldConstants.SeaLevel + 20f)
            {
                flow += 0.45f + MathF.Max(0f, height - WorldConstants.SeaLevel - 20f) * 0.025f;
            }

            if (IsRiverHead(wx, wz, column, rawHeights, x, z))
            {
                flow += 2.5f;
            }

            return flow;
        }

        private static bool IsRiverHead(int wx, int wz, TerrainColumn column, float[,] rawHeights, int x, int z)
        {
            float height = rawHeights[x, z];
            if (height < WorldConstants.SeaLevel + 16f || height > WorldConstants.SeaLevel + 52f)
            {
                return false;
            }

            if (column.Biome.Moisture < -0.12f)
            {
                return false;
            }

            if (!IsLocalHeightMaximum(rawHeights, x, z, minDelta: 0.3f))
            {
                return false;
            }

            uint hash = HashCoordinates(wx, wz);
            int sparsityMask = column.Biome.Primary is BiomeType.Mountains or BiomeType.SnowyPeaks ? 0x2F : 0x7F;
            return (hash & sparsityMask) == 0;
        }

        private static bool IsLocalHeightMaximum(float[,] heights, int x, int z, float minDelta)
        {
            float center = heights[x, z];
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    int nx = x + dx;
                    int nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= heights.GetLength(0) || nz >= heights.GetLength(1))
                    {
                        continue;
                    }

                    if (heights[nx, nz] > center - minDelta)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static uint HashCoordinates(int x, int z)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)x) * 16777619u;
                hash = (hash ^ (uint)z) * 16777619u;
                return hash;
            }
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Clamp(t, 0f, 1f);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
