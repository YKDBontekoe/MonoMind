using System;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Ai;
using Autonocraft.Crafting;
using Autonocraft.Items;
using Autonocraft.Village;
using Autonocraft.Domain.Items;
using Autonocraft.World.Loot;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.Core
{
    public partial class AutonocraftGame
    {
        private string? _deathCauseText;
        private string? _deathPenaltyText;

        private void OpenPauseMenu()
        {
            _input.MouseLockedBeforePause = _input.IsMouseLocked;
            _screens.OpenPauseMenu(PrepareMouseForUi, title => Window.Title = title);
        }

        private void ClosePauseMenu()
        {
            _input.IsMouseLocked = _input.MouseLockedBeforePause;
            _screens.ClosePauseMenu(RestoreMouseLockAfterOverlay);
        }

        private void OpenDeathScreen()
        {
            var player = _session.Player;
            if (!player.DeathConsequencesApplied)
            {
                DeathConsequences.ApplyOnDeath(player);
                player.DeathConsequencesApplied = true;
            }

            _deathCauseText = FormatDeathCause(player.LastDeathCause);
            _deathPenaltyText = "You dropped some supplies.";
            player.Stats.RecordDeath();
            _input.MouseLockedBeforeDeath = _input.IsMouseLocked;
            _screens.OpenDeathScreen(_deathCauseText, _deathPenaltyText, () =>
            {
                _input.IsMouseLocked = false;
                ReleaseMouseCapture();
                IsMouseVisible = true;
            });
            Window.Title = "Autonocraft | You Died";
        }

        private void CloseDeathScreen()
        {
            _input.IsMouseLocked = _input.MouseLockedBeforeDeath;
            _screens.CloseDeathScreen(RestoreMouseLockAfterOverlay);
        }

        private void QuitFromPauseMenu()
        {
            SaveWorld(sync: true);
            Exit();
        }

        private bool UpdateBlockingGameplayOverlays(
            float deltaTime,
            KeyboardState kbState,
            MouseState mouseState)
        {
            if (_screens.DevConsole!.IsOpen)
            {
                EnsureUiPointerMode();
                _screens.DevConsole.Update(GraphicsDevice.Viewport, kbState, _input.PrevKeyboard, _hostContext);
                return true;
            }

            if (_screens.DeathScreen!.IsOpen)
            {
                EnsureUiPointerMode();
                bool simulatedRespawn = IsKeyPressed(kbState, Key.Enter);
                _screens.DeathScreen.Update(GraphicsDevice.Viewport, kbState, mouseState, _input.PrevKeyboard, _input.PrevMouse, deltaTime);

                if (_screens.DeathScreen.RespawnRequested || simulatedRespawn)
                {
                    CombatSystem.RespawnPlayer(_session.Grid, _session.Player, _worldSpawnX, _worldSpawnZ);
                    _deathCauseText = null;
                    _deathPenaltyText = null;
                    CloseDeathScreen();
                }
                else if (_screens.DeathScreen.MainMenuRequested)
                {
                    ReturnToMainMenu();
                }

                return true;
            }

            if (_screens.PauseMenu!.IsOpen)
            {
                EnsureUiPointerMode();
                bool simulatedEscape = IsKeyPressed(kbState, Key.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape);
                _screens.PauseMenu.Update(
                    GraphicsDevice.Viewport,
                    kbState,
                    mouseState,
                    _input.PrevKeyboard,
                    _input.PrevMouse,
                    deltaTime,
                    simulatedEscape);

                if (_screens.PauseMenu.ResumeRequested)
                {
                    ClosePauseMenu();
                }
                else if (_screens.PauseMenu.SaveNowRequested)
                {
                    SaveWorld(sync: true);
                    _session.HudToast.Show("World saved");
                }
                else if (_screens.PauseMenu.MainMenuRequested)
                {
                    ReturnToMainMenu();
                }
                else if (_screens.PauseMenu.QuitRequested)
                {
                    QuitFromPauseMenu();
                }

                return true;
            }

            if (_screens.VillageChatScreen!.IsOpen)
            {
                EnsureUiPointerMode();
                _screens.VillageChatScreen.Update(GraphicsDevice.Viewport, _session, kbState, _input.PrevKeyboard, mouseState, _input.PrevMouse, deltaTime);
                if (!_screens.VillageChatScreen.IsOpen)
                {
                    _input.IsMouseLocked = _input.MouseLockedBeforeVillageUi;
                    RestoreMouseLockAfterOverlay();
                    return true;
                }

                return true;
            }

            if (_screens.VillageScreen!.IsOpen)
            {
                EnsureUiPointerMode();
                if ((IsKeyPressed(kbState, Key.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
                    || (IsKeyPressed(kbState, Key.V) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V)))
                {
                    CloseVillageUi();
                    return true;
                }

                if (IsKeyPressed(kbState, Key.R) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.R))
                {
                    var village = _screens.VillageScreen.CurrentVillage
                        ?? _session.Villages.GetActiveVillage(_session.Player.Position);
                    if (village != null)
                    {
                        var recruitResult = _session.Villages.TryRecruit(village, _session.Grid);
                        _screens.VillageScreen.SetRecruitFeedback(recruitResult);
                    }

                    return true;
                }

                _screens.VillageScreen.SetPlayerPosition(_session.Player.Position);
                _screens.VillageScreen.Update(GraphicsDevice.Viewport, kbState, mouseState, _input.PrevKeyboard, _input.PrevMouse);
                if (_screens.VillageScreen.CloseRequested)
                {
                    CloseVillageUi();
                }
                else
                {
                    HandleVillageScreenActions();
                }

                return true;
            }

            if (_session.Crafting.InventoryOpen)
            {
                UpdateInventoryOverlay(kbState, mouseState);
                return true;
            }

            if (_session.Crafting.Crucible.IsOpen)
            {
                UpdateCrucibleOverlay(deltaTime, kbState, mouseState);
                return true;
            }

            if (_session.Chest.IsOpen)
            {
                UpdateChestOverlay(deltaTime, kbState, mouseState);
                return true;
            }

            if (_session.Crafting.IsJournalUiBlocking)
            {
                EnsureUiPointerMode();
                _session.Crafting.UpdateJournal(deltaTime);

                bool closeJournal = (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J))
                    || (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape));

                if (closeJournal && (_session.Crafting.JournalOpen || _session.Crafting.JournalTransition.Alpha > 0.01f))
                {
                    _session.Crafting.CloseJournal();
                    _input.IsMouseLocked = _input.MouseLockedBeforeCrafting;
                    RestoreMouseLockAfterOverlay();
                }

                return true;
            }

            return false;
        }

        private void HandleVillageScreenActions()
        {
            if (_screens.VillageScreen!.IsFoundingMode)
            {
                if (_screens.VillageScreen.PlaceTownHeartRequested)
                {
                    _blueprints.StartFoundingTownHeartPlacement(CloseVillageUi);
                    EnsureMouseLockedForGameplay();
                }
                else if (_screens.VillageScreen.ClaimRequested)
                {
                    if (_session.Villages.TryFindClaimableStructure(
                            _session.Grid,
                            _session.Player.Position,
                            24f,
                            out int ax,
                            out int az,
                            out _))
                    {
                        if (_session.Villages.TryClaimStructure(_session.Grid, ax, az, out _))
                        {
                            CloseVillageUi();
                            OpenVillageUi();
                        }
                    }
                }

                return;
            }

            var village = _screens.VillageScreen!.CurrentVillage
                ?? _session.Villages.GetActiveVillage(_session.Player.Position);
            if (village == null)
            {
                return;
            }

            if (_screens.VillageScreen.RecruitRequested)
            {
                var recruitResult = _session.Villages.TryRecruit(village, _session.Grid);
                _screens.VillageScreen.SetRecruitFeedback(recruitResult);
            }
            else if (_screens.VillageScreen.ClaimRequested)
            {
                if (_session.Villages.TryFindClaimableStructure(
                        _session.Grid,
                        _session.Player.Position,
                        24f,
                        out int ax,
                        out int az,
                        out _))
                {
                    _session.Villages.TryClaimStructure(_session.Grid, ax, az, out _);
                }
            }
            else if (_screens.VillageScreen.RequestedBlueprintId != null && _screens.VillageScreen.RequestBlueprintPlacement)
            {
                _blueprints.StartBlueprintPlacement(village, _screens.VillageScreen.RequestedBlueprintId, CloseVillageUi);
                EnsureMouseLockedForGameplay();
            }
            else if (_screens.VillageScreen.RequestWorkZonePlacement)
            {
                _blueprints.StartWorkZonePlacement(CloseVillageUi);
                EnsureMouseLockedForGameplay();
            }
            else if (_screens.VillageScreen.RequestedStewardChat)
            {
                CloseVillageUi();
                OpenVillageChatUi();
            }
            else if (_screens.VillageScreen.RequestedChatVillagerId >= 0 &&
                     _session.Villagers.TryGet(_screens.VillageScreen.RequestedChatVillagerId, out var chatVillager))
            {
                CloseVillageUi();
                OpenVillageChatWithVillager(village, chatVillager.Id, chatVillager.Name);
            }
            else if (_screens.VillageScreen.RequestedAssignVillagerId >= 0 &&
                     _session.Villagers.TryGet(_screens.VillageScreen.RequestedAssignVillagerId, out var villager))
            {
                var result = _session.Villages.TryAssignJob(village, villager, _screens.VillageScreen.RequestedAssignJob);
                _screens.VillageScreen.SetAssignFeedback(result);
                if (!result.Success)
                {
                    _session.VillageEvents.OnAssignFailure(result.PlayerMessage);
                }
            }
        }

        private void OpenVillageChatWithVillager(Village.Village village, int villagerId, string villagerName)
        {
            _input.MouseLockedBeforeVillageUi = _input.IsMouseLocked;
            PrepareMouseForUi();
            _screens.VillageChatScreen!.OpenWithVillager(village, villagerId, villagerName);
        }

        public void OpenCrucibleAt(int x, int y, int z, BlockType stationType)
        {
            _input.MouseLockedBeforeCrafting = _input.IsMouseLocked;
            PrepareMouseForUi();
            _session.Crafting.OpenCrucible(x, y, z, stationType);
        }

        public void OpenChestAt(int x, int y, int z)
        {
            if (!_session.Grid.Containers.TryGet(x, y, z, out var chest) || chest == null)
            {
                if (_session.Grid.GetBlock(x, y, z) != BlockType.Chest)
                {
                    return;
                }

                _session.HudToast.Show("Chest loot is still loading — try again.", durationSeconds: 2f);
                return;
            }

            _input.MouseLockedBeforeCrafting = _input.IsMouseLocked;
            PrepareMouseForUi();
            _session.Chest.Open(x, y, z, chest.Inventory);
            chest.Opened = true;

            if (!_session.Player.CreativeMode)
            {
                var rarity = chest.HighestRarity ?? LootRoller.PeekHighestRarity(
                    Enumerable.Range(0, chest.Inventory.SlotCount)
                        .Select(i => chest.Inventory.GetSlot(i))
                        .Where(s => !s.IsEmpty)
                        .ToList());

                if (rarity is LootRarity.Epic or LootRarity.Legendary)
                {
                    _session.HudToast.Show($"{rarity} loot discovered!", durationSeconds: 3f);
                }
            }
        }

        private void CloseChestUi()
        {
            _session.Chest.Close();
            _input.IsMouseLocked = _input.MouseLockedBeforeCrafting;
            RestoreMouseLockAfterOverlay();
        }

        private void UpdateChestOverlay(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            EnsureUiPointerMode();
            _screens.ChestScreen!.Update(
                deltaTime,
                GraphicsDevice.Viewport,
                _session.Chest,
                kbState,
                mouseState,
                _input.PrevKeyboard,
                _input.PrevMouse);

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                CloseChestUi();
                return;
            }

            int slot = _screens.ChestScreen.ClickedSlotIndex;
            if (slot < 0)
            {
                return;
            }

            if (_screens.ChestScreen.RightClickedSlot)
            {
                if (_session.Chest.DepositFromPlayer(_session.Player, slot))
                {
                    _screens.ChestScreen.SetStatus("DEPOSITED");
                }
                else
                {
                    _screens.ChestScreen.SetStatus("SELECT HOTBAR ITEM");
                }
            }
            else if (_session.Chest.WithdrawToPlayer(_session.Player, slot))
            {
                var stack = _session.Player.Hotbar[_session.Player.SelectedSlot];
                if (stack.Kind == ItemKind.Tool && ToolRegistry.IsLootOnly(stack.ToolId))
                {
                    var def = ToolRegistry.Get(stack.ToolId);
                    _screens.ChestScreen.SetStatus($"{def.DisplayName.ToUpperInvariant()}!");
                }
                else
                {
                    _screens.ChestScreen.SetStatus("TAKEN");
                }
            }
            else
            {
                _screens.ChestScreen.SetStatus("INVENTORY FULL");
            }
        }

        private void CloseCrucibleUi()
        {
            _session.Crafting.CloseCrucible();
            _session.Crafting.CloseRecipeBook();
            _input.IsMouseLocked = _input.MouseLockedBeforeCrafting;
            RestoreMouseLockAfterOverlay();
        }

        private void CloseInventoryUi()
        {
            if (!_session.Crafting.CraftCursor.IsEmpty)
            {
                _session.Player.AddItem(_session.Crafting.CraftCursor);
                _session.Crafting.CraftCursor = ItemStack.Empty;
            }

            _session.Crafting.CloseInventory();
            _session.Crafting.CloseRecipeBook();
            _input.IsMouseLocked = _input.MouseLockedBeforeCrafting;
            RestoreMouseLockAfterOverlay();
        }

        private void UpdateInventoryOverlay(KeyboardState kbState, MouseState mouseState)
        {
            EnsureUiPointerMode();
            _screens.InventoryScreen!.Update(
                GraphicsDevice.Viewport,
                _session.Player,
                _session.Crafting,
                kbState,
                mouseState,
                _input.PrevKeyboard,
                _input.PrevMouse);

            _screens.InventoryScreen.HandleClicks(_session.Player, _session.Crafting);
            _screens.InventoryScreen.HandleRecipeBookClick(_session.Crafting, _session.Player);

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.B) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.B))
            {
                _session.Crafting.ToggleRecipeBook();
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                CloseInventoryUi();
            }
        }

        /// <summary>
        /// Toggle inventory on I edge. Frame guard prevents fixed-timestep double Update from open+close on one press.
        /// </summary>
        private bool HandleInventoryKey(KeyboardState kbState)
        {
            if (!_session.Player.IsAlive)
            {
                return false;
            }

            if (_screens.PauseMenu?.IsOpen == true
                || _screens.DeathScreen?.IsOpen == true
                || _screens.VillageScreen?.IsOpen == true
                || _session.Crafting.Crucible.IsOpen
                || _session.Chest.IsOpen
                || _session.Crafting.IsJournalUiBlocking)
            {
                return false;
            }

            if (!kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.I) || _input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.I))
            {
                return false;
            }

            if (_input.LastInventoryKeyFrame == _input.GameFrameCount)
            {
                return false;
            }

            _input.LastInventoryKeyFrame = _input.GameFrameCount;

            if (_session.Crafting.InventoryOpen)
            {
                CloseInventoryUi();
            }
            else
            {
                _input.MouseLockedBeforeCrafting = _input.IsMouseLocked;
                PrepareMouseForUi();
                _session.Crafting.OpenInventory();
            }

            return true;
        }

        private void OpenVillageUi()
        {
            var village = _session.Villages.GetActiveVillage(_session.Player.Position);
            if (village == null)
            {
                _input.MouseLockedBeforeVillageUi = _input.IsMouseLocked;
                PrepareMouseForUi();
                TryActivateWindow();
                _screens.VillageScreen!.OpenFounding(
                    _session.Villages,
                    _session.Grid,
                    _session.Player.Position,
                    new PlayerHotbarAdapter(_session.Player),
                    _session.Player.CreativeMode,
                    _settings.PlayWithAi && _settings.AiProvider != AiProviderKind.Disabled);
                _session.VillageEvents.TownBoardOpen = true;
                return;
            }

            _session.Villages.EnsureStarterCitizens(village, _session.Grid);
            _session.Villages.SyncCitizensForVillage(village);

            string? openingNote = null;
            int citizens = VillageSettlementHealth.GetLivePopulation(village, _session.Villagers);
            if (citizens == 0)
            {
                openingNote = "Village roster is empty. Close and reopen the Town Board to repair this save.";
            }
            else if (citizens < 2 && village.Name.Contains("Founder", StringComparison.OrdinalIgnoreCase))
            {
                openingNote = $"{citizens} villager(s) on site — open PEOPLE to assign LUMBER or BUILD.";
            }

            _input.MouseLockedBeforeVillageUi = _input.IsMouseLocked;
            PrepareMouseForUi();
            TryActivateWindow();
            _screens.VillageScreen!.Open(
                village,
                _session.Villages,
                _session.Grid,
                _session.Player.Position,
                new PlayerHotbarAdapter(_session.Player),
                _session.Player.CreativeMode,
                openingNote,
                _settings.PlayWithAi && _settings.AiProvider != AiProviderKind.Disabled,
                earlyGuideStage: _session.Player.Stats.EarlyGuideStage,
                guidePlayer: _session.Player);
            _session.VillageEvents.TownBoardOpen = true;
            if (_session.NearbyManageVillagerId.HasValue)
            {
                _screens.VillageScreen.OpenPeopleTab(_session.NearbyManageVillagerId);
            }
        }

        private void OpenVillageUiToPeopleTab(int? villagerId = null)
        {
            OpenVillageUi();
            _screens.VillageScreen?.OpenPeopleTab(villagerId ?? _session.NearbyManageVillagerId);
        }

        private static string FormatDeathCause(DeathCause cause) => cause switch
        {
            DeathCause.Fall => "You fell from a great height.",
            DeathCause.Drown => "You drowned.",
            DeathCause.Starvation => "You starved.",
            DeathCause.Wolf => "A wolf killed you.",
            DeathCause.Animal => "An animal killed you.",
            DeathCause.Lava => "You tried to swim in lava.",
            DeathCause.Suffocate => "You suffocated.",
            _ => "YOUR ADVENTURE ISN'T OVER"
        };

        private IItemContainer WrapPlayerHotbar() => new PlayerHotbarAdapter(_session.Player);

        private void CloseVillageUi()
        {
            _session.VillageEvents.TownBoardOpen = false;
            _screens.VillageScreen!.Close();
            _input.IsMouseLocked = _input.MouseLockedBeforeVillageUi;
            RestoreMouseLockAfterOverlay();
        }

        private void OpenVillageChatUi()
        {
            if (!_settings.PlayWithAi || _settings.AiProvider == AiProviderKind.Disabled)
            {
                _session.HudToast.Show("Village AI is off. Enable it in main menu Settings.");
                return;
            }

            var village = _session.Villages.GetActiveVillage(_session.Player.Position);
            if (village == null)
            {
                _session.HudToast.Show("No village nearby. Press V to place a Town Heart.");
                return;
            }

            var nearest = _session.Villagers.GetNearest(_session.Player.Position, 8f);
            string target = nearest != null ? nearest.Id.ToString() : "mayor";
            _input.MouseLockedBeforeVillageUi = _input.IsMouseLocked;
            PrepareMouseForUi();
            _screens.VillageChatScreen!.Open(village, target, nearest == null);
        }

        private sealed class PlayerHotbarAdapter : IItemContainer
        {
            private readonly Player _player;
            public PlayerHotbarAdapter(Player player) => _player = player;
            public int SlotCount => _player.Hotbar.Length;
            public ItemStack GetSlot(int index) => _player.Hotbar[index];
            public void SetSlot(int index, ItemStack stack) => _player.Hotbar[index] = stack;
            public bool AddItem(ItemStack item) => _player.AddItem(item);
            public bool TryConsumeBlock(BlockType blockType, int count)
            {
                if (_player.CreativeMode)
                {
                    return true;
                }

                int have = CountBlock(blockType);
                if (have < count) return false;
                int remaining = count;
                for (int i = 0; i < _player.Hotbar.Length && remaining > 0; i++)
                {
                    if (!_player.Hotbar[i].IsBlock() || _player.Hotbar[i].BlockType != blockType) continue;
                    int take = Math.Min(_player.Hotbar[i].Count, remaining);
                    _player.Hotbar[i].Count -= take;
                    remaining -= take;
                    if (_player.Hotbar[i].Count <= 0) _player.Hotbar[i] = ItemStack.Empty;
                }
                return remaining == 0;
            }
            public int CountBlock(BlockType blockType)
            {
                if (_player.CreativeMode)
                {
                    return int.MaxValue / 2;
                }

                int total = 0;
                for (int i = 0; i < _player.Hotbar.Length; i++)
                {
                    if (_player.Hotbar[i].IsBlock() && _player.Hotbar[i].BlockType == blockType)
                        total += _player.Hotbar[i].Count;
                }
                return total;
            }
            public bool HasSpaceFor(ItemStack item) => true;
        }

        private void UpdateCrucibleOverlay(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            var env = _session.Crafting.GetCurrentEnvironment(_session.Grid, _timeOfDay);
            _screens.CrucibleScreen!.Update(
                GraphicsDevice.Viewport,
                _session.Crafting.Crucible,
                env,
                _session.Crafting.Journal,
                _session.Crafting,
                _session.Player,
                kbState,
                mouseState,
                _input.PrevKeyboard,
                _input.PrevMouse,
                deltaTime);

            _screens.CrucibleScreen.HandleRecipeBookClick(_session.Crafting, _session.Player);

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.B) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.B))
            {
                _session.Crafting.ToggleRecipeBook();
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                CloseCrucibleUi();
                return;
            }

            if (_screens.CrucibleScreen.ClickedSlotIndex >= 0)
            {
                if (_screens.CrucibleScreen.RightClickedSlot)
                {
                    if (_session.Crafting.Crucible.WithdrawToHotbar(_session.Player, _screens.CrucibleScreen.ClickedSlotIndex))
                    {
                        _screens.CrucibleScreen.TriggerSlotPulse(_screens.CrucibleScreen.ClickedSlotIndex);
                    }
                }
                else
                {
                    if (_session.Crafting.Crucible.DepositFromHotbar(_session.Player, _screens.CrucibleScreen.ClickedSlotIndex))
                    {
                        _screens.CrucibleScreen.TriggerSlotPulse(_screens.CrucibleScreen.ClickedSlotIndex);
                    }
                    else
                    {
                        var slot = _session.Player.GetSelectedStack();
                        if (!slot.IsBlock() && !slot.IsMaterial())
                        {
                            _screens.CrucibleScreen.SetStatus("SELECT BLOCKS OR STICKS");
                        }
                        else
                        {
                            _screens.CrucibleScreen.SetStatus("SLOT FULL");
                        }
                    }
                }
            }

            if (_screens.CrucibleScreen.TransmuteRequested)
            {
                _screens.CrucibleScreen.BeginTransmuteAnimation();
            }

            if (_screens.CrucibleScreen.TransmuteReady)
            {
                var result = _session.Crafting.TryTransmute(_session.Grid, _session.Player, _timeOfDay);
                if (result.Succeeded)
                {
                    string created = result.Recipe!.IsToolOutput
                        ? result.Recipe.DisplayName.ToUpperInvariant()
                        : result.Recipe.IsMaterialOutput
                            ? $"{result.Recipe.DisplayName.ToUpperInvariant()} x{result.Recipe.OutputCount}"
                            : result.Recipe.Output.ToString().ToUpperInvariant();
                    _screens.CrucibleScreen.SetStatus($"CREATED {created}");
                    _session.BlockInteraction.TriggerCrosshairFlash();
                    _session.Particles.SpawnHint(_session.Player.Position + new Vector3(0f, Player.EyeHeight, 0f));
                }
                else
                {
                    _screens.CrucibleScreen.SetStatus(result.Message.ToUpperInvariant());
                }
            }
        }
    }
}
