# Performance Benchmark Baseline

Recorded **2026-06-16** before multi-assembly refactor. Re-run with:

```bash
dotnet run --project src/Autonocraft -- --bench
```

Seed **42424** | CPU cores **11** | GPU available

## Terrain generation (async, no mesh)

| RD | Chunks | Terrain ms | ms/chunk |
|----|--------|------------|----------|
| 4  | 81     | 773.8      | 9.55     |
| 8  | 289    | 2544.7     | 8.81     |
| 12 | 625    | 5562.0     | 8.90     |
| 16 | 1089   | 9712.6     | 8.92     |

## Single-chunk world generation (25 chunks, RD=2)

Avg **7.17** ms/chunk | total **179** ms

## CPU mesh build (BuildMeshCpuOnly, RD=2, 25 chunks)

Shell avg **1.82** ms | Full avg **19.46** ms | ratio **9%**

## GetBlock throughput (5M calls, RD=8)

| Pattern            | Throughput      | Time  |
|--------------------|-----------------|-------|
| Sequential column  | 67.37 M calls/s | 74 ms |
| Random XZ          | 16.19 M calls/s | 309 ms |

## Full initial load (terrain + GPU mesh)

| RD | Chunks | Load ms | ms/chunk | steps |
|----|--------|---------|----------|-------|
| 4  | 81     | 165     | 2.03     | 1699  |
| 8  | 289    | 526     | 1.82     | 455   |
| 12 | 625    | 1041    | 1.67     | 446   |
| 16 | 1089   | 2039    | 1.87     | 527   |

## Runtime streaming (teleport 40 chunks, RD=8)

40 chunk-hop frames: avg **1.55** ms/frame | peak pending mesh **140**

## Ocean water mesh load (RD=12, pos 64,64,64)

- Chunks loaded: **625**
- Water chunks: **320**
- Water tris: **149,326**
- Opaque tris: **53,126**
