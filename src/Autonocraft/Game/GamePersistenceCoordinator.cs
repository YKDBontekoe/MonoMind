using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Autonocraft.Diagnostics;
using Autonocraft.Domain.Core;
using Autonocraft.Engine;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Core
{
    public partial class AutonocraftGame
    {
        private string? _activeSlotId;
        private string? _activeSlotName;
        private WorldSaveData? _pendingSaveData;
        private bool _loadingFromSave;
        private bool _needsStarterSettlement;
        private float _autosaveTimer;
        private bool _saveInProgress;
        private bool _exitSaveDone;
        private bool _isStructureGalleryWorld;
        private int _worldSpawnX = GameConstants.DefaultSpawnX;
        private int _worldSpawnZ = GameConstants.DefaultSpawnZ;

        private void StartNewWorld(int seed, WorldType worldType)
        {
            _isStructureGalleryWorld = false;
            string slotName = WorldSaveManager.GenerateDefaultSlotName();
            string slotId = WorldSaveManager.CreateSlotId(slotName);

            _activeSlotId = slotId;
            _activeSlotName = slotName;
            _worldSpawnX = GameConstants.DefaultSpawnX;
            _worldSpawnZ = GameConstants.DefaultSpawnZ;
            _pendingSaveData = null;
            _loadingFromSave = false;
            _autosaveTimer = 0f;

            var genParams = WorldGenParams.ForType(worldType);
            ResetWorldState(seed, genParams);
            _session.ResetPlayer();
            _session.ResetCrafting();
            SyncCameraFromPlayer();
            SetTimeOfDay(0.15f);
            _timeScale = DayNightCycle.DefaultTimeScale;
            _timePaused = false;
            _hostContext.TimeScale = _timeScale;
            _hostContext.TimePaused = _timePaused;

            _needsStarterSettlement = true;
            StartWorldLoading();
        }

        private void StartStructureGalleryWorld()
        {
            _isStructureGalleryWorld = true;
            _activeSlotId = null;
            _activeSlotName = "Structure Gallery";
            (int spawnX, int spawnZ) = StructureGallery.GetPlayerSpawn();
            _worldSpawnX = spawnX;
            _worldSpawnZ = spawnZ;
            _pendingSaveData = null;
            _loadingFromSave = false;
            _autosaveTimer = 0f;

            var genParams = WorldGenParams.ForType(WorldType.StructureGallery);
            ResetWorldState(StructureGallery.Seed, genParams);
            _session.ResetPlayer();
            _session.ResetCrafting();
            _session.Player.CreativeMode = true;
            SyncCameraFromPlayer();
            SetTimeOfDay(0.35f);
            _timeScale = DayNightCycle.DefaultTimeScale;
            _timePaused = false;
            _hostContext.TimeScale = _timeScale;
            _hostContext.TimePaused = _timePaused;

            _needsStarterSettlement = false;
            _session.NearbyClaimHint = null;
            _session.Player.Stats.EarlyGuideStage = 5;
            StartWorldLoading();
        }

        private bool TryStartLoadedWorld(string slotId)
        {
            try
            {
                _isStructureGalleryWorld = false;
                var save = WorldSaveManager.Load(slotId);

                _activeSlotId = slotId;
                _activeSlotName = save.SlotName;
                _worldSpawnX = save.Spawn?.X ?? GameConstants.DefaultSpawnX;
                _worldSpawnZ = save.Spawn?.Z ?? GameConstants.DefaultSpawnZ;
                _pendingSaveData = save;
                _loadingFromSave = true;
                _autosaveTimer = 0f;

                ResetWorldState(save.Seed, WorldGenParams.ForType(WorldType.Default));
                _session.ResetPlayer();
                _session.ResetCrafting();
                _session.Crafting.LoadJournal(save.UnlockedCraftingIds);
                _session.Crafting.ShowCraftingHint = save.UnlockedCraftingIds == null || save.UnlockedCraftingIds.Count == 0;
                WorldSaveManager.ApplyPlayerSaveData(_session.Player, save.Player);
                SetTimeOfDay(save.Time.TimeOfDay);
                _timeScale = save.Time.TimeScale;
                _timePaused = save.Time.TimePaused;
                _hostContext.TimeScale = _timeScale;
                _hostContext.TimePaused = _timePaused;
                SyncCameraFromPlayer();
                _session.LoadVillageSave(
                    save.Villages,
                    save.Villagers,
                    save.ClaimedAnchors);

                StartWorldLoading();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Save] Failed to load slot '{slotId}': {ex.Message}");
                _screens.SaveSlotScreen?.SetLoadError($"Failed to load save: {ex.Message}");
                return false;
            }
        }

        private void RegenerateAtlasTexture(int seed)
        {
            if (GraphicsDevice == null)
            {
                return;
            }

            _atlasTexture?.Dispose();
            _atlasTexture = ProceduralAtlasBuilder.LoadOrGenerate(GraphicsDevice, seed);
            _blockTerrainEffect?.SetAtlas(_atlasTexture);
            _renderer?.SetAtlasTexture(_atlasTexture);
        }

        private void ResetWorldState(int seed, WorldGenParams? parameters = null)
        {
            _session.ReplaceWorld(seed, parameters);
            RegenerateAtlasTexture(seed);
            BlockAtlas.UseCpuBlockVariation = true;
            _session.WireWorldEvents();
            if (_ui != null)
            {
                _screens.RecreateLoadingScreen(GraphicsDevice, _ui, _session.Grid);
            }
        }

        private void PerformExitSave()
        {
            if (_exitSaveDone)
            {
                return;
            }

            _exitSaveDone = true;
            PerformAutosave(sync: true);
        }

        private void PerformAutosave(bool sync)
        {
            if (_screens.State != GameState.Playing)
            {
                return;
            }

            SaveWorld(sync);
        }

        private void SaveWorld(bool sync)
        {
            if (_isStructureGalleryWorld || string.IsNullOrEmpty(_activeSlotId) || _saveInProgress)
            {
                return;
            }

            string slotId = _activeSlotId;
            string slotName = _activeSlotName ?? slotId;
            var snapshot = _session.BuildSaveSnapshot(slotId, slotName, _timeOfDay, _timeScale, _timePaused, _worldSpawnX, _worldSpawnZ);
            var saveData = WorldSaveManager.BuildFromSnapshot(snapshot);

            if (sync)
            {
                try
                {
                    WorldSaveManager.Save(saveData);
                    Console.WriteLine($"[Save] World saved to slot '{slotName}'.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Save] Save failed: {ex.Message}");
                    if (_screens.State == GameState.Playing)
                    {
                        _session.HudToast.Show("Save failed!", new Color(1f, 0.35f, 0.35f));
                    }
                }

                return;
            }

            _saveInProgress = true;
            Task.Run(() =>
            {
                try
                {
                    WorldSaveManager.Save(saveData);
                    Console.WriteLine($"[Save] Auto-saved world '{slotName}'.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Save] Auto-save failed: {ex.Message}");
                    _pendingActions.Enqueue(() =>
                        _session.HudToast.Show("Auto-save failed!", new Color(1f, 0.35f, 0.35f)));
                }
                finally
                {
                    _saveInProgress = false;
                }
            });
        }

        private void TickAutosave(float deltaTime)
        {
            _autosaveTimer += deltaTime;
            if (_autosaveTimer >= GameConstants.AutosaveIntervalSeconds)
            {
                _autosaveTimer = 0f;
                PerformAutosave(sync: false);
            }
        }
    }
}
