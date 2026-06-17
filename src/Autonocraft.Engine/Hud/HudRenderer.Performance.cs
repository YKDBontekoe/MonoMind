using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    public sealed partial class HudRenderer
    {
        private void DrawPerformanceHud(UiLayout layout, GameRenderContext ctx)
        {
            float x = layout.S(10f);
            float y = layout.S(10f);
            float w = layout.S(280f);
            float h = layout.S(420f);

            DrawHudGlassPanel(_spriteBatch, x, y, w, h, UiTheme.Accent, 0.85f);

            float lineY = y + layout.S(10f);

            DrawHudText(_spriteBatch, _whiteTexture, "DIAGNOSTICS & PERFORMANCE", x + layout.S(10f), lineY, 0.85f, UiTheme.AccentGlow, 1f, semiBold: true);
            lineY += layout.S(16f);

            float fps = PerfCounters.RollingFps;
            float frameTime = PerfCounters.LastUpdateMs + PerfCounters.LastDrawMs;
            DrawPerfLine("FPS (Rolling)", $"{fps:F1}", x, lineY, w, layout, UiTheme.Title);
            lineY += layout.S(13f);
            DrawPerfLine("Frame Time", $"{frameTime:F2} ms", x, lineY, w, layout, UiTheme.Title);
            lineY += layout.S(13f);
            DrawPerfLine("Update Time (CPU)", $"{PerfCounters.LastUpdateMs:F2} ms", x, lineY, w, layout, UiTheme.StatValue);
            lineY += layout.S(13f);
            DrawPerfLine("Draw Time (GPU)", $"{PerfCounters.LastDrawMs:F2} ms", x, lineY, w, layout, UiTheme.StatValue);
            lineY += layout.S(13f);

            long workingSet = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            long managedHeap = GC.GetTotalMemory(false);
            DrawPerfLine("System RAM (WS)", $"{workingSet / (1024f * 1024f):F1} MB", x, lineY, w, layout, UiTheme.Subtitle);
            lineY += layout.S(13f);
            DrawPerfLine("Managed Heap", $"{managedHeap / (1024f * 1024f):F1} MB", x, lineY, w, layout, UiTheme.Subtitle);
            lineY += layout.S(13f);
            DrawPerfLine("GC collections (0/1/2)", $"{GC.CollectionCount(0)} / {GC.CollectionCount(1)} / {GC.CollectionCount(2)}", x, lineY, w, layout, UiTheme.Subtitle);
            lineY += layout.S(16f);

            DrawHudText(_spriteBatch, _whiteTexture, "CPU UPDATE BREAKDOWN", x + layout.S(10f), lineY, 0.75f, UiTheme.AccentGlow * 0.85f, 0.9f, semiBold: true);
            lineY += layout.S(12f);
            DrawPerfLine("  Player & Physics", $"{PerfCounters.UpdatePlayerMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Chunk Streaming", $"{PerfCounters.UpdateChunksMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Fluids Simulation", $"{PerfCounters.UpdateFluidsMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Animal Entity AI", $"{PerfCounters.UpdateAnimalsMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Villages & Survival", $"{PerfCounters.UpdateVillagesMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Particles Update", $"{PerfCounters.UpdateParticlesMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(16f);

            DrawHudText(_spriteBatch, _whiteTexture, "GPU DRAW BREAKDOWN", x + layout.S(10f), lineY, 0.75f, UiTheme.AccentGlow * 0.85f, 0.9f, semiBold: true);
            lineY += layout.S(12f);
            DrawPerfLine("  Skybox & Clouds", $"{PerfCounters.DrawSkyMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Terrain Opaque", $"{PerfCounters.DrawTerrainOpaqueMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Terrain Water", $"{PerfCounters.DrawTerrainWaterMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Terrain Cutout", $"{PerfCounters.DrawTerrainCutoutMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Flora rendering", $"{PerfCounters.DrawFloraMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Entity models", $"{PerfCounters.DrawEntitiesMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  HUD & UI screens", $"{PerfCounters.DrawUiMs:F2} ms", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(16f);

            DrawHudText(_spriteBatch, _whiteTexture, "RENDER STATISTICS", x + layout.S(10f), lineY, 0.75f, UiTheme.AccentGlow * 0.85f, 0.9f, semiBold: true);
            lineY += layout.S(12f);
            DrawPerfLine("  Active/Pending Chunks", $"{ctx.Grid.ActiveChunkCount} / {PerfCounters.PendingMeshCount}", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            DrawPerfLine("  Draw Calls (Terr/Flora)", $"{PerfCounters.TerrainDrawCalls} / {PerfCounters.FloraDrawCalls}", x, lineY, w, layout, UiTheme.Muted);
            lineY += layout.S(11f);
            int triangles = (PerfCounters.TerrainOpaqueIndexCount + PerfCounters.TerrainWaterIndexCount + PerfCounters.TerrainCutoutIndexCount) / 3;
            DrawPerfLine("  Terrain Triangles", $"{triangles:N0}", x, lineY, w, layout, UiTheme.Muted);
        }

        private void DrawPerfLine(string label, string value, float x, float y, float w, UiLayout layout, Color valueColor)
        {
            float size = 0.8f;
            DrawHudText(_spriteBatch, _whiteTexture, label, x + layout.S(10f), y, size, UiTheme.HudTextSecondary, 0.9f);
            float valW = MeasureHudText(value, size);
            DrawHudText(_spriteBatch, _whiteTexture, value, x + w - layout.S(10f) - valW, y, size, valueColor, 0.95f);
        }
    }
}
