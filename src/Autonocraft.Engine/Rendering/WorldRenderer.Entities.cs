using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{

    public sealed partial class WorldRenderer
    {
        private void DrawAnimals(GameRenderContext ctx, Matrix view, Matrix proj, int renderDistance, SceneLighting lighting)
        {
            var cameraPos = ctx.Camera.Position;
            var animals = ctx.Animals.GetAnimalsInRange(cameraPos, ChunkLod.GetAnimalCullRadius(renderDistance));
            if (animals.Count == 0)
            {
                return;
            }

            bool prevTextureEnabled = _worldEffect.TextureEnabled;
            _worldEffect.TextureEnabled = true;
            _worldEffect.Texture = _atlasTexture;
            _worldEffect.View = view;
            _worldEffect.Projection = proj;
            _worldEffect.AmbientLightColor = lighting.ToMono(lighting.AmbientColor);
            _worldEffect.DirectionalLight0.Enabled = lighting.SunEnabled;
            _worldEffect.DirectionalLight0.Direction = ConvertVector(-lighting.SunDirection);
            _worldEffect.DirectionalLight0.DiffuseColor = lighting.ToMono(lighting.SunColor);
            _worldEffect.DirectionalLight1.Enabled = lighting.MoonEnabled;
            _worldEffect.DirectionalLight1.Direction = ConvertVector(-lighting.MoonDirection);
            _worldEffect.DirectionalLight1.DiffuseColor = lighting.ToMono(lighting.MoonColor);
            _worldEffect.FogEnabled = true;
            _worldEffect.FogColor = lighting.ToMono(lighting.SkyHorizon);
            _worldEffect.FogStart = ChunkLod.GetFogStart(renderDistance) * lighting.FogMultiplier;
            _worldEffect.FogEnd = ChunkLod.GetFogEnd(renderDistance, lighting.TwilightFactor) * lighting.FogMultiplier;

            foreach (var animal in animals)
            {
                var stats = animal.Stats;
                float deathScale = animal.DeathScale;
                if (deathScale <= 0.01f)
                {
                    continue;
                }

                float yawRad = MathHelper.ToRadians(-animal.Yaw);
                var rotation = Matrix.CreateRotationY(yawRad);
                var scaleMatrix = Matrix.CreateScale(deathScale);
                var translation = Matrix.CreateTranslation(animal.Position.X, animal.Position.Y, animal.Position.Z);
                var animalWorld = scaleMatrix * rotation * translation;

                float flash = animal.HitFlashTimer > 0f ? animal.HitFlashTimer / 0.15f : 0f;

                int bodyCol = 4, bodyRow = 2;
                int headCol = 5, headRow = 2;

                if (BlockAtlas.LayoutData.Animals.TryGetValue(animal.Type.ToString(), out var animalTiles))
                {
                    var body = BlockAtlas.LayoutData.GetTile(animalTiles.Body);
                    var head = BlockAtlas.LayoutData.GetTile(animalTiles.Head);
                    bodyCol = body.Col;
                    bodyRow = body.Row;
                    headCol = head.Col;
                    headRow = head.Row;
                }

                var bodyUV = BlockAtlas.GetTileUVs(bodyCol, bodyRow);
                var headUV = BlockAtlas.GetTileUVs(headCol, headRow);

                var shape = AnimalVisuals.GetShape(animal.Type);
                var layout = AnimalBodyLayout.From(shape, stats);
                float walkPhase = AnimalVisuals.GetWalkPhase(animal);

                _worldEffect.TextureEnabled = false;
                AnimalVisuals.DrawLegs(animalWorld, animal.Type, stats, shape, walkPhase, DrawColoredBox);
                _worldEffect.TextureEnabled = true;

                DrawTexturedBox(animalWorld, layout.BodyHalfW, layout.BodyHalfH, layout.BodyHalfD, 0f, layout.BodyCenterY, 0f, bodyUV, bodyUV);

                _worldEffect.TextureEnabled = false;
                AnimalVisuals.DrawNeck(animalWorld, animal.Type, stats, shape, DrawColoredBox);
                _worldEffect.TextureEnabled = true;

                DrawTexturedBox(animalWorld, layout.HeadHalfW, layout.HeadHalfH, layout.HeadHalfD, 0f, layout.HeadCenterY, layout.HeadForward, bodyUV, headUV);

                _worldEffect.TextureEnabled = false;
                AnimalVisuals.DrawFeatures(animalWorld, animal.Type, stats, shape, walkPhase, DrawColoredBox);
                _worldEffect.TextureEnabled = true;

                if (stats.HasAccent && AnimalVisuals.UsesAccentBox(animal.Type))
                {
                    _worldEffect.TextureEnabled = false;
                    float accentSize = layout.HeadSize * 0.45f;
                    DrawColoredBox(animalWorld, accentSize, accentSize * 0.6f, accentSize * 0.8f, 0f, layout.HeadCenterY, layout.HeadForward + layout.HeadSize * 1.2f, stats.AccentColor);
                    _worldEffect.TextureEnabled = true;
                }

                if (flash > 0f)
                {
                    _worldEffect.TextureEnabled = false;
                    var flashColor = new Color(1f, 1f, 1f, 0.45f * flash);
                    DrawColoredBox(animalWorld, stats.Width * 0.5f, stats.Height * 0.45f, stats.Width * 0.5f, 0f, stats.Height * 0.4f, 0f, flashColor);
                    _worldEffect.TextureEnabled = true;
                }
            }

            _worldEffect.TextureEnabled = prevTextureEnabled;
        }

        private void DrawVillagers(GameRenderContext ctx, Matrix view, Matrix proj, int renderDistance, SceneLighting lighting)
        {
            var cameraPos = ctx.Camera.Position;
            var villagers = ctx.Villagers.GetVillagersInRange(cameraPos, ChunkLod.GetAnimalCullRadius(renderDistance));
            if (villagers.Count == 0)
            {
                return;
            }

            bool prevTextureEnabled = _worldEffect.TextureEnabled;
            _worldEffect.TextureEnabled = true;
            _worldEffect.Texture = _atlasTexture;
            _worldEffect.View = view;
            _worldEffect.Projection = proj;
            _worldEffect.AmbientLightColor = lighting.ToMono(lighting.AmbientColor);
            _worldEffect.DirectionalLight0.Enabled = lighting.SunEnabled;
            _worldEffect.DirectionalLight0.Direction = ConvertVector(-lighting.SunDirection);
            _worldEffect.DirectionalLight0.DiffuseColor = lighting.ToMono(lighting.SunColor);
            _worldEffect.FogEnabled = true;
            _worldEffect.FogColor = lighting.ToMono(lighting.SkyHorizon);
            _worldEffect.FogStart = ChunkLod.GetFogStart(renderDistance) * lighting.FogMultiplier;
            _worldEffect.FogEnd = ChunkLod.GetFogEnd(renderDistance, lighting.TwilightFactor) * lighting.FogMultiplier;

            int bodyCol = 6;
            int bodyRow = 6;
            int headCol = 7;
            int headRow = 6;
            if (BlockAtlas.LayoutData.Villagers.TryGetValue("Default", out var villagerTiles))
            {
                var body = BlockAtlas.LayoutData.GetTile(villagerTiles.Body);
                var head = BlockAtlas.LayoutData.GetTile(villagerTiles.Head);
                bodyCol = body.Col;
                bodyRow = body.Row;
                headCol = head.Col;
                headRow = head.Row;
            }

            var bodyUV = BlockAtlas.GetTileUVs(bodyCol, bodyRow);
            var headUV = BlockAtlas.GetTileUVs(headCol, headRow);

            foreach (var villager in villagers)
            {
                float yawRad = MathHelper.ToRadians(-villager.Yaw);
                var rotation = Matrix.CreateRotationY(yawRad);
                var translation = Matrix.CreateTranslation(villager.Position.X, villager.Position.Y, villager.Position.Z);
                var villagerWorld = rotation * translation;

                var layout = VillagerBodyLayout.Default;
                var roleColor = VillagerVisuals.GetRoleColor(villager.Role);
                _worldEffect.TextureEnabled = false;
                float walkPhase = VillagerVisuals.GetWalkPhase(villager);
                VillagerVisuals.DrawModelExtras(villagerWorld, villager, layout, walkPhase, DrawColoredBox);
                _worldEffect.TextureEnabled = true;

                DrawTexturedBox(villagerWorld, layout.BodyHalfW, layout.BodyHalfH, layout.BodyHalfD, 0f, layout.BodyCenterY, 0f, bodyUV, bodyUV);
                DrawTexturedBox(villagerWorld, layout.HeadSize, layout.HeadSize, layout.HeadSize, 0f, layout.HeadCenterY, layout.HeadForward, bodyUV, headUV);

                _worldEffect.TextureEnabled = false;
                float vestHeight = layout.BodyHalfH * 0.72f;
                DrawColoredBox(villagerWorld, Villager.Width * 0.34f, vestHeight * 0.5f, Villager.Width * 0.09f, 0f, layout.BodyCenterY + layout.BodyHalfH * 0.02f, layout.HeadForward * 0.95f, roleColor * 0.88f);

                if (VillagerVisuals.ShouldDrawJobIndicator(villager.CurrentJob))
                {
                    var jobColor = VillagerVisuals.GetJobIndicatorColor(villager.CurrentJob);
                    float indicatorSize = villager.CurrentJob == JobType.Sleep ? 0.1f : 0.12f;
                    DrawColoredBox(villagerWorld, indicatorSize, indicatorSize, indicatorSize, 0f, Villager.Height + 0.18f, 0f, jobColor);
                }

                _worldEffect.TextureEnabled = true;
            }

            _worldEffect.TextureEnabled = prevTextureEnabled;
        }

        private void DrawItemEntities(GameRenderContext ctx, Matrix view, Matrix proj, int renderDistance, SceneLighting lighting)
        {
            if (ctx.ItemEntities == null || ctx.ItemEntities.Count == 0)
            {
                return;
            }

            bool prevTextureEnabled = _worldEffect.TextureEnabled;
            _worldEffect.TextureEnabled = true;
            _worldEffect.Texture = _atlasTexture;
            _worldEffect.View = view;
            _worldEffect.Projection = proj;
            _worldEffect.AmbientLightColor = lighting.ToMono(lighting.AmbientColor);
            _worldEffect.DirectionalLight0.Enabled = lighting.SunEnabled;
            _worldEffect.DirectionalLight0.Direction = ConvertVector(-lighting.SunDirection);
            _worldEffect.DirectionalLight0.DiffuseColor = lighting.ToMono(lighting.SunColor);
            _worldEffect.DirectionalLight1.Enabled = lighting.MoonEnabled;
            _worldEffect.DirectionalLight1.Direction = ConvertVector(-lighting.MoonDirection);
            _worldEffect.DirectionalLight1.DiffuseColor = lighting.ToMono(lighting.MoonColor);
            _worldEffect.FogEnabled = true;
            _worldEffect.FogColor = lighting.ToMono(lighting.SkyHorizon);
            _worldEffect.FogStart = ChunkLod.GetFogStart(renderDistance) * lighting.FogMultiplier;
            _worldEffect.FogEnd = ChunkLod.GetFogEnd(renderDistance, lighting.TwilightFactor) * lighting.FogMultiplier;

            var cameraPos = ctx.Camera.Position;
            float cullRadius = ChunkLod.GetAnimalCullRadius(renderDistance);
            float cullRadiusSq = cullRadius * cullRadius;

            foreach (var itemEntity in ctx.ItemEntities)
            {
                if (itemEntity.ReadyForRemoval)
                {
                    continue;
                }

                var itemPos = itemEntity.Position;
                if (Vector3.DistanceSquared(cameraPos, itemPos) > cullRadiusSq)
                {
                    continue;
                }

                if (!IsPointVisible(itemPos, _frustumPlanes))
                {
                    continue;
                }

                float bobOffset = MathF.Sin(itemEntity.HoverTimer * 3.0f) * 0.08f;
                float rotAngle = itemEntity.Age * 1.5f;

                var rotation = Matrix.CreateRotationY(rotAngle);
                var translation = Matrix.CreateTranslation(
                    itemEntity.Position.X,
                    itemEntity.Position.Y + bobOffset + 0.12f,
                    itemEntity.Position.Z
                );
                var scaleMatrix = Matrix.CreateScale(1.0f);
                var itemWorld = scaleMatrix * rotation * translation;

                var stack = itemEntity.Item;
                if (stack.IsBlock())
                {
                    var type = stack.BlockType;
                    var uvTop = BlockAtlas.GetFaceUVs(type, new System.Numerics.Vector3(0f, 1f, 0f));
                    var uvSide = BlockAtlas.GetFaceUVs(type, new System.Numerics.Vector3(0f, 0f, 1f));
                    DrawTexturedBox(itemWorld, 0.12f, 0.12f, 0.12f, 0f, 0f, 0f, uvSide, uvTop);
                }
                else if (stack.IsTool())
                {
                    var uv = BlockAtlas.GetToolUVs(stack.ToolId);
                    DrawTexturedBox(itemWorld, 0.14f, 0.14f, 0.015f, 0f, 0f, 0f, uv, uv);
                }
                else if (stack.IsFluidContainer())
                {
                    _worldEffect.TextureEnabled = false;
                    Color color = stack.IsWaterBucket()
                        ? new Color(0.28f, 0.48f, 0.92f)
                        : new Color(0.62f, 0.58f, 0.52f);
                    DrawColoredBox(itemWorld, 0.08f, 0.08f, 0.08f, 0f, 0f, 0f, color);
                    _worldEffect.TextureEnabled = true;
                }
                else if (stack.IsFood())
                {
                    _worldEffect.TextureEnabled = false;
                    Color color = stack.FoodId switch
                    {
                        ItemId.RawMeat => new Color(0.78f, 0.32f, 0.28f),
                        ItemId.CookedMeat => new Color(0.62f, 0.38f, 0.24f),
                        ItemId.Bread => new Color(0.78f, 0.62f, 0.28f),
                        _ => Color.Pink
                    };
                    DrawColoredBox(itemWorld, 0.08f, 0.08f, 0.08f, 0f, 0f, 0f, color);
                    _worldEffect.TextureEnabled = true;
                }
                else if (stack.IsMaterial() && stack.MaterialId == ItemId.Stick)
                {
                    _worldEffect.TextureEnabled = false;
                    DrawColoredBox(itemWorld, 0.02f, 0.12f, 0.02f, 0f, 0f, 0f, new Color(0.62f, 0.45f, 0.24f));
                    _worldEffect.TextureEnabled = true;
                }
                else
                {
                    _worldEffect.TextureEnabled = false;
                    DrawColoredBox(itemWorld, 0.08f, 0.08f, 0.08f, 0f, 0f, 0f, Color.Gold);
                    _worldEffect.TextureEnabled = true;
                }
            }

            _worldEffect.TextureEnabled = prevTextureEnabled;
        }

        private void Draw3DHeldItem(GameRenderContext ctx, Matrix view, Matrix proj, SceneLighting lighting)
        {
            var player = ctx.Player;
            var animator = ctx.InteractionAnimator;
            var stack = player.GetSelectedStack();
            if (stack.IsEmpty)
            {
                return;
            }

            // Clear the depth buffer so the held item is always drawn on top of the world
            _device.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Transparent, 1.0f, 0);

            // Calculate swing rotations and offsets
            float swingDeg = animator.GetHeldItemSwingDegrees();
            float offsetY = animator.GetHeldItemOffsetY();

            var camera = ctx.Camera;
            Vector3 front = camera.Front;
            Vector3 right = camera.Right;
            Vector3 up = camera.Up;

            // Base position offset from camera eye in local coordinates
            float baseForward = 0.35f;
            float baseRight = 0.18f;
            float baseUp = -0.16f + offsetY * 0.05f;

            // Apply swing translation offset
            float swingRad = MathHelper.ToRadians(swingDeg);
            baseForward += MathF.Sin(swingRad) * 0.08f;
            baseRight -= MathF.Sin(swingRad) * 0.05f;

            Vector3 itemPos = camera.Position + front * baseForward + right * baseRight + up * baseUp;

            float yawRad = MathHelper.ToRadians(-camera.Yaw);
            float pitchRad = MathHelper.ToRadians(camera.Pitch);

            // Build camera rotation matrix
            Matrix camRot = Matrix.CreateRotationY(yawRad) * Matrix.CreateRotationX(pitchRad);

            // Item-specific rotation (angle held)
            Matrix itemRot;
            if (stack.IsBlock())
            {
                itemRot = Matrix.CreateRotationY(MathHelper.ToRadians(45f)) * Matrix.CreateRotationX(MathHelper.ToRadians(-15f));
            }
            else
            {
                itemRot = Matrix.CreateRotationY(MathHelper.ToRadians(45f)) * Matrix.CreateRotationX(MathHelper.ToRadians(-25f)) * Matrix.CreateRotationZ(MathHelper.ToRadians(-10f));
            }

            // Swing rotation around local X/Z axis
            Matrix swingRot = Matrix.CreateRotationX(MathHelper.ToRadians(-swingDeg * 1.2f)) * Matrix.CreateRotationZ(MathHelper.ToRadians(swingDeg * 0.8f));

            Matrix finalWorld = itemRot * swingRot * camRot * Matrix.CreateTranslation(itemPos.X, itemPos.Y, itemPos.Z);

            bool prevTextureEnabled = _worldEffect.TextureEnabled;
            _worldEffect.TextureEnabled = true;
            _worldEffect.Texture = _atlasTexture;
            _worldEffect.View = view;
            _worldEffect.Projection = proj;

            _worldEffect.AmbientLightColor = lighting.ToMono(lighting.AmbientColor * 1.25f); // slightly brighter for readability
            _worldEffect.DirectionalLight0.Enabled = lighting.SunEnabled;
            _worldEffect.DirectionalLight0.Direction = ConvertVector(-lighting.SunDirection);
            _worldEffect.DirectionalLight0.DiffuseColor = lighting.ToMono(lighting.SunColor);
            _worldEffect.FogEnabled = false;

            if (stack.IsBlock())
            {
                var type = stack.BlockType;
                var uvTop = BlockAtlas.GetFaceUVs(type, new System.Numerics.Vector3(0f, 1f, 0f));
                var uvSide = BlockAtlas.GetFaceUVs(type, new System.Numerics.Vector3(0f, 0f, 1f));
                DrawTexturedBox(finalWorld, 0.05f, 0.05f, 0.05f, 0f, 0f, 0f, uvSide, uvTop);
            }
            else if (stack.IsTool())
            {
                var uv = BlockAtlas.GetToolUVs(stack.ToolId);
                DrawTexturedBox(finalWorld, 0.08f, 0.08f, 0.005f, 0f, 0f, 0f, uv, uv);
            }

            _worldEffect.TextureEnabled = prevTextureEnabled;
        }
    }
}
