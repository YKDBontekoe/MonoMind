using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Crafting;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.Engine.Audio;
using Autonocraft.Entities;
using Autonocraft.Village;
using Autonocraft.World;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Core
{
    /// <summary>
    /// Owns gameplay systems and core simulation tick. AutonocraftGame delegates simulation here.
    /// </summary>
    public sealed class GameSession
    {
        private readonly BlockInteractionSystem _blockInteraction = new();
        private readonly CombatSystem _combatSystem = new();
        private readonly ParticleSystem _particles = new();
        private readonly InteractionAnimator _interactionAnimator = new();
        private CraftingSystem _craftingSystem = new();
        private readonly HudToast _hudToast = new();
        private GameRenderContext? _renderContext;
        private AudioManager? _audio;
        private float _footstepTimer;

        public Player Player { get; private set; }
        public VoxelWorld Grid { get; private set; }
        public AnimalManager Animals { get; private set; }
        public VillagerManager Villagers { get; private set; }
        public VillageManager Villages { get; private set; }
        public HudToast HudToast => _hudToast;
        public bool ShowVillageOnboarding { get; set; }
        public bool ShowVillageHint { get; set; } = true;
        public string? NearbyClaimHint { get; set; }
        private Vector3 _lastClaimHintScanPos = new(float.MinValue, 0f, float.MinValue);
        public BlockInteractionSystem BlockInteraction => _blockInteraction;
        public CombatSystem Combat => _combatSystem;
        public ParticleSystem Particles => _particles;
        public InteractionAnimator InteractionAnimator => _interactionAnimator;
        public CraftingSystem Crafting => _craftingSystem;

        public GameSession(int seed, WorldGenParams? parameters = null)
        {
            Player = CreateDefaultPlayer();
            Grid = new VoxelWorld(seed, parameters);
            Animals = new AnimalManager(seed);
            Villagers = new VillagerManager();
            Villages = new VillageManager(Villagers);
            Villages.SetWorldSeed(seed);
            _blockInteraction.BindAnimator(_interactionAnimator);
            WireNotifications();
        }

        public void BindAudio(AudioManager? audio)
        {
            _audio = audio;
            WireNotifications();
        }

        private void WireNotifications()
        {
            Player.ShowToast = msg => _hudToast.Show(msg);
            _blockInteraction.ShowToast = msg => _hudToast.Show(msg);
            _combatSystem.ShowToast = msg => _hudToast.Show(msg);
            Villages.ShowToast = msg => _hudToast.Show(msg);
            _craftingSystem.OnDiscoveryUnlocked = msg =>
            {
                _hudToast.Show(msg, new Microsoft.Xna.Framework.Color(0.45f, 0.95f, 0.72f));
                _audio?.PlaySfx(SfxKind.Discovery);
            };
            _blockInteraction.TryClaimStructureAt = (world, x, y, z) => Villages.TryClaimAtBlock(world, x, y, z);
            _blockInteraction.PlaySfx = (kind, blockType) =>
            {
                if (blockType == BlockType.Air)
                {
                    _audio?.PlaySfx(kind);
                }
                else
                {
                    _audio?.PlaySfxForBlock(kind, blockType);
                }
            };
            _combatSystem.PlaySfx = (kind, volume) => _audio?.PlaySfx(kind, volume: volume);
        }

        public static Player CreateDefaultPlayer()
        {
            var player = new Player(new Vector3(GameConstants.DefaultSpawnX + 0.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f));
            player.Yaw = -90f;
            player.Pitch = 0f;
            return player;
        }

        public void ResetPlayer()
        {
            Player = CreateDefaultPlayer();
            WireNotifications();
        }

        public void ResetCrafting()
        {
            _craftingSystem = new CraftingSystem();
            WireNotifications();
        }

        public void ReplaceWorld(int seed, WorldGenParams? parameters, Action<VoxelWorld>? onDispose = null)
        {
            onDispose?.Invoke(Grid);
            Grid.Dispose();
            Grid = new VoxelWorld(seed, parameters);
            Animals = new AnimalManager(seed);
            Villagers = new VillagerManager();
            Villages = new VillageManager(Villagers);
            Villages.SetWorldSeed(seed);
            WireNotifications();
        }

        public bool DeferAmbientSpawns { get; set; }

        public void WireWorldEvents()
        {
            Grid.ChunksLoaded += coords =>
            {
                if (DeferAmbientSpawns || Grid.PendingMeshCount > 24)
                {
                    return;
                }

                Animals.OnChunksLoaded(coords, Grid);
            };
            Grid.ChunksUnloaded += coords => Animals.OnChunksUnloaded(coords);
        }

        public void PlacePlayerOnSurface(int spawnX = GameConstants.DefaultSpawnX, int spawnZ = GameConstants.DefaultSpawnZ)
        {
            var spawnPos = Player.FindSafeSpawnPosition(Grid, spawnX, spawnZ);
            Player.Position = spawnPos;
            Player.Velocity = Vector3.Zero;
            Player.CreativeMode = false;
        }

        public void PopulateAnimals(int renderDistance, int spawnX = GameConstants.DefaultSpawnX, int spawnZ = GameConstants.DefaultSpawnZ)
        {
            Animals.PopulateAroundSpawn(Grid, spawnX, spawnZ, renderDistance);
        }

        public void UpdateNearbyClaimHint()
        {
            const float moveRescanSq = 24f * 24f;
            const float idleRescanSq = 8f * 8f;
            float movedSq = Vector3.DistanceSquared(Player.Position, _lastClaimHintScanPos);

            // Skip expensive world scan unless the player moved meaningfully.
            if (NearbyClaimHint != null && movedSq < moveRescanSq)
            {
                return;
            }

            if (NearbyClaimHint == null && movedSq < idleRescanSq)
            {
                return;
            }

            _lastClaimHintScanPos = Player.Position;

            if (Villages.TryFindClaimableStructure(Grid, Player.Position, 16f, out _, out _, out _, quickScan: true))
            {
                NearbyClaimHint = "Abandoned outpost nearby — press V to claim";
            }
            else
            {
                NearbyClaimHint = null;
            }
        }

        public static void AdvanceTime(ref float timeOfDay, float timeScale, bool timePaused, float deltaTime, ref float waterAnimTime)
        {
            if (!timePaused)
            {
                timeOfDay += deltaTime * timeScale;
                timeOfDay -= MathF.Floor(timeOfDay);
            }

            waterAnimTime += deltaTime;
        }

        public void UpdateFluids(GraphicsDevice? device, float deltaTime)
        {
            Grid.Fluids.Update(Grid, deltaTime, device);
        }

        public void UpdatePlayerMovement(
            float deltaTime,
            Vector3 moveDir,
            bool creativeMode,
            bool swimUp,
            bool swimDown,
            bool jumpPressed)
        {
            if (!Player.IsAlive)
            {
                return;
            }

            if (creativeMode)
            {
                Player.Update(deltaTime, Grid, moveDir);
            }
            else
            {
                if (jumpPressed && (!Player.InWater || Player.OnWaterSurface))
                {
                    Player.Jump();
                }

                Player.Update(deltaTime, Grid, moveDir, swimUp, swimDown);
            }

            _interactionAnimator.Update(deltaTime, Player);
            _particles.Update(deltaTime);
        }

        public void PlayJumpSound() => _audio?.PlaySfx(SfxKind.Jump);

        public void UpdateMovementAudio(float deltaTime, Vector3 moveDir)
        {
            if (_audio == null || !Player.IsAlive || Player.CreativeMode)
            {
                return;
            }

            if (Player.JustLanded && !Player.CreativeMode)
            {
                float volume = Math.Clamp(Player.FallDistance / 6f, 0.3f, 1f);
                _audio.PlaySfx(SfxKind.Land, volume: volume);
            }

            if (!Player.IsGrounded || Player.InWater)
            {
                _footstepTimer = 0f;
                return;
            }

            float horizontalSpeed = new Vector2(moveDir.X, moveDir.Z).Length();
            if (horizontalSpeed < 0.1f)
            {
                _footstepTimer = 0f;
                return;
            }

            _footstepTimer -= deltaTime;
            if (_footstepTimer > 0f)
            {
                return;
            }

            int footX = (int)MathF.Floor(Player.Position.X);
            int footY = (int)MathF.Floor(Player.Position.Y) - 1;
            int footZ = (int)MathF.Floor(Player.Position.Z);
            var groundBlock = Grid.GetBlock(footX, footY, footZ);
            _audio.PlaySfxForBlock(SfxKind.Footstep, groundBlock);
            _footstepTimer = 0.42f;
        }

        public void PlayWaterSplashSound() => _audio?.PlaySfx(SfxKind.WaterSplash);

        public void UpdateWaterEffects(ref bool playerWasInWater)
        {
            if (Player.InWater && !playerWasInWater)
            {
                Particles.SpawnWaterSplash(Player.Position + new Vector3(0f, 0.2f, 0f), 0.9f);
                PlayWaterSplashSound();
            }
            else if (!Player.InWater && playerWasInWater)
            {
                Particles.SpawnWaterSplash(Player.Position, 1.0f);
                PlayWaterSplashSound();
            }
            else if (Player.JustLanded && WaterQuery.IsLandingInWater(Grid, Player.Position))
            {
                Particles.SpawnWaterSplash(Player.Position, 1.3f);
                PlayWaterSplashSound();
            }

            playerWasInWater = Player.InWater;
            Combat.HandleLandingEffects(Player, Particles, InteractionAnimator);
        }

        public void UpdateCombatAndInteraction(
            float deltaTime,
            Camera camera,
            bool leftHeld,
            bool leftPressed,
            bool rightPressed,
            bool shiftRightPressed,
            GraphicsDevice? device)
        {
            if (!Player.IsAlive)
            {
                return;
            }

            var solidRayHit = BlockInteractionSystem.RaycastSolidHit(
                Grid,
                camera.Position,
                camera.Front,
                BlockInteractionSystem.RaycastRange);

            Combat.Update(
                deltaTime,
                Grid,
                Player,
                Animals,
                BlockInteraction,
                Particles,
                InteractionAnimator,
                camera.Position,
                camera.Front,
                leftHeld,
                leftPressed,
                solidRayHit);

            BlockInteraction.Update(
                deltaTime,
                Grid,
                Player,
                camera.Position,
                camera.Front,
                leftHeld && !Combat.BlocksMiningThisFrame,
                rightPressed,
                shiftRightPressed,
                Crafting,
                Particles,
                device,
                solidRayHit);
        }

        public void UpdateChunks(
            GraphicsDevice? device,
            Vector3 cameraPosition,
            int renderDistance,
            int maxTerrainPerFrame = VoxelWorld.DefaultTerrainChunksPerFrame,
            int maxMeshPerFrame = VoxelWorld.DefaultMeshChunksPerFrame)
        {
            var profile = ChunkStreamingProfile.FromMovement(Player.Position, Player.Velocity, Player.CreativeMode);
            Grid.UpdateChunksAround(device, cameraPosition, renderDistance, profile);
            Grid.ProcessPendingWork(
                device,
                cameraPosition,
                renderDistance,
                profile,
                maxTerrainPerFrame: maxTerrainPerFrame,
                maxMeshPerFrame: maxMeshPerFrame);
        }

        public void UpdateAnimals(float deltaTime)
        {
            Animals.Update(deltaTime, Grid);
        }

        public void UpdateVillages(float deltaTime, float timeOfDay)
        {
            Villages.CreativeMode = Player.CreativeMode;
            Villages.Update(deltaTime, Grid, timeOfDay);
        }

        public void LoadVillageSave(
            IEnumerable<VillageSaveData>? villages,
            IEnumerable<VillagerSaveData>? villagers,
            IEnumerable<ClaimedAnchorSaveData>? claimedAnchors = null)
        {
            Villages.LoadFromSave(
                villages ?? Array.Empty<VillageSaveData>(),
                villagers ?? Array.Empty<VillagerSaveData>(),
                claimedAnchors);
        }

        public GameRenderContext PrepareRenderContext(Camera camera, float timeOfDay, float waterAnimTime, int renderDistance)
        {
            if (_renderContext == null)
            {
                _renderContext = new GameRenderContext();
            }

            _renderContext.Camera = camera;
            _renderContext.Player = Player;
            _renderContext.Grid = Grid;
            _renderContext.Animals = Animals;
            _renderContext.Villagers = Villagers;
            _renderContext.BlockInteraction = BlockInteraction;
            _renderContext.Particles = Particles;
            _renderContext.InteractionAnimator = InteractionAnimator;
            _renderContext.Crafting = Crafting;
            _renderContext.HudToast = _hudToast;
            _renderContext.ShowVillageHint = ShowVillageHint;
            _renderContext.NearbyClaimHint = NearbyClaimHint;
            _renderContext.TimeOfDay = timeOfDay;
            _renderContext.WaterAnimTime = waterAnimTime;
            _renderContext.RenderDistance = renderDistance;
            return _renderContext;
        }

        public GameRenderContext CreateRenderContext(Camera camera, float timeOfDay, float waterAnimTime, int renderDistance)
            => PrepareRenderContext(camera, timeOfDay, waterAnimTime, renderDistance);

        public SaveSnapshot BuildSaveSnapshot(string slotId, string slotName, float timeOfDay, float timeScale, bool timePaused, int spawnX, int spawnZ)
        {
            return new SaveSnapshot
            {
                SlotId = slotId,
                SlotName = slotName,
                Seed = Grid.Seed,
                SpawnX = spawnX,
                SpawnZ = spawnZ,
                Time = new TimeSaveData
                {
                    TimeOfDay = timeOfDay,
                    TimeScale = timeScale,
                    TimePaused = timePaused
                },
                Modifications = Grid.ExportModifications(),
                FluidModifications = Grid.Fluids.ExportModifications(),
                UnlockedCraftingIds = Crafting.Journal.Export(),
                Villages = Villages.ExportVillages(),
                Villagers = Villagers.ExportVillagers(),
                ClaimedAnchors = Villages.ExportClaimedAnchors(),
                Player = WorldSaveManager.BuildPlayerSaveData(Player)
            };
        }
    }
}
