using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Diagnostics;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.Items;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.Core
{
    public partial class AutonocraftGame
    {
        private readonly HashSet<Key> _simulatedKeys = new HashSet<Key>();
        private bool _prevSpacePressed;
        private bool _prevQPressed;
        private bool _doubleTapSprintActive;
        private float _lastWPressTime;
        private bool _prevWPressed;

        private bool IsKeyPressed(KeyboardState kbState, Key key)
        {
            if (_simulatedKeys.Contains(key)) return true;
            var monoKey = MapKey(key);
            return monoKey.HasValue && kbState.IsKeyDown(monoKey.Value);
        }

        private Microsoft.Xna.Framework.Input.Keys? MapKey(Key key)
        {
            return key switch
            {
                Key.W => Microsoft.Xna.Framework.Input.Keys.W,
                Key.S => Microsoft.Xna.Framework.Input.Keys.S,
                Key.A => Microsoft.Xna.Framework.Input.Keys.A,
                Key.D => Microsoft.Xna.Framework.Input.Keys.D,
                Key.Space => Microsoft.Xna.Framework.Input.Keys.Space,
                Key.ShiftLeft => Microsoft.Xna.Framework.Input.Keys.LeftShift,
                Key.ShiftRight => Microsoft.Xna.Framework.Input.Keys.RightShift,
                Key.ControlLeft => Microsoft.Xna.Framework.Input.Keys.LeftControl,
                Key.Q => Microsoft.Xna.Framework.Input.Keys.Q,
                Key.G => Microsoft.Xna.Framework.Input.Keys.G,
                Key.Escape => Microsoft.Xna.Framework.Input.Keys.Escape,
                Key.Number1 => Microsoft.Xna.Framework.Input.Keys.D1,
                Key.Number2 => Microsoft.Xna.Framework.Input.Keys.D2,
                Key.Number3 => Microsoft.Xna.Framework.Input.Keys.D3,
                Key.Number4 => Microsoft.Xna.Framework.Input.Keys.D4,
                Key.Number5 => Microsoft.Xna.Framework.Input.Keys.D5,
                Key.Number6 => Microsoft.Xna.Framework.Input.Keys.D6,
                Key.Number7 => Microsoft.Xna.Framework.Input.Keys.D7,
                Key.Number8 => Microsoft.Xna.Framework.Input.Keys.D8,
                Key.Number9 => Microsoft.Xna.Framework.Input.Keys.D9,
                Key.Keypad1 => Microsoft.Xna.Framework.Input.Keys.NumPad1,
                Key.Keypad2 => Microsoft.Xna.Framework.Input.Keys.NumPad2,
                Key.Keypad3 => Microsoft.Xna.Framework.Input.Keys.NumPad3,
                Key.Keypad4 => Microsoft.Xna.Framework.Input.Keys.NumPad4,
                Key.Keypad5 => Microsoft.Xna.Framework.Input.Keys.NumPad5,
                Key.Keypad6 => Microsoft.Xna.Framework.Input.Keys.NumPad6,
                Key.Keypad7 => Microsoft.Xna.Framework.Input.Keys.NumPad7,
                Key.Keypad8 => Microsoft.Xna.Framework.Input.Keys.NumPad8,
                Key.Keypad9 => Microsoft.Xna.Framework.Input.Keys.NumPad9,
                _ => null
            };
        }

        /// <summary>
        /// Agent actions, inventory key, overlay shortcuts, hotbar, sprint, movement, and mouse combat input.
        /// Returns true when the frame should skip world simulation (overlay open or shortcut consumed).
        /// </summary>
        private bool ProcessGameplayInput(
            float deltaTime,
            KeyboardState kbState,
            MouseState mouseState,
            out bool leftHeld,
            out bool leftPressed,
            out bool rightPressed,
            out bool shiftRightPressed,
            out bool shiftHeld,
            out float mouseDx,
            out float mouseDy,
            out int centerX,
            out int centerY)
        {
            leftHeld = false;
            leftPressed = false;
            rightPressed = false;
            shiftRightPressed = false;
            shiftHeld = false;
            mouseDx = 0f;
            mouseDy = 0f;
            centerX = 0;
            centerY = 0;

            while (_pendingActions.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Console.WriteLine($"[Agent Action Error] {ex.Message}"); }
            }

            HandleInventoryKey(kbState);

            if (UpdateBlockingGameplayOverlays(deltaTime, kbState, mouseState))
            {
                return true;
            }

            if (!_screens.PauseMenu!.IsOpen && !_screens.DeathScreen!.IsOpen && _session.Player.IsAlive)
            {
                _session.Player.Stats.RecordPlayTime(deltaTime);
            }

            bool consoleToggle = (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F3) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F3))
                || (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemTilde) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemTilde));

            if (consoleToggle)
            {
                _screens.DevConsole!.Toggle();
                if (_screens.DevConsole.IsOpen)
                {
                    _input.MouseLockedBeforeConsole = _input.IsMouseLocked;
                    PrepareMouseForUi();
                }
                else
                {
                    _input.IsMouseLocked = _input.MouseLockedBeforeConsole;
                    RestoreMouseLockAfterOverlay();
                }
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F4) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F4))
            {
                PerfCounters.ShowPerfHud = !PerfCounters.ShowPerfHud;
                Console.WriteLine($"[Debug] Toggled performance HUD: {PerfCounters.ShowPerfHud}");
            }

            if (_screens.DevConsole!.IsOpen)
            {
                _screens.DevConsole.Update(GraphicsDevice.Viewport, kbState, _input.PrevKeyboard, _hostContext);
                return true;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V) && _session.Player.IsAlive)
            {
                if (_screens.VillageScreen!.IsOpen)
                {
                    CloseVillageUi();
                }
                else
                {
                    OpenVillageUi();
                }

                return true;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C) && _session.Player.IsAlive)
            {
                OpenVillageChatUi();
                return true;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J) && _session.Player.IsAlive)
            {
                _input.MouseLockedBeforeCrafting = _input.IsMouseLocked;
                PrepareMouseForUi();
                _session.Crafting.ToggleJournal();
                return true;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && _session.Player.IsAlive)
            {
                if (_blueprints.TryCancelOnEscape())
                {
                    return true;
                }

                OpenPauseMenu();
                return true;
            }

            _blueprints.TickPendingPreview();

            for (int i = 0; i < 9; i++)
            {
                var k = (Microsoft.Xna.Framework.Input.Keys)((int)Microsoft.Xna.Framework.Input.Keys.D1 + i);
                if (kbState.IsKeyDown(k) && !_input.PrevKeyboard.IsKeyDown(k))
                {
                    _session.Player.SelectedSlot = i;
                    var stack = _session.Player.Hotbar[i];
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {i + 1}: {FormatHotbarStack(stack)}");
                }
                var nk = (Microsoft.Xna.Framework.Input.Keys)((int)Microsoft.Xna.Framework.Input.Keys.NumPad1 + i);
                if (kbState.IsKeyDown(nk) && !_input.PrevKeyboard.IsKeyDown(nk))
                {
                    _session.Player.SelectedSlot = i;
                    var stack = _session.Player.Hotbar[i];
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {i + 1}: {FormatHotbarStack(stack)}");
                }
            }

            bool qPressed = IsKeyPressed(kbState, Key.Q);
            if (qPressed && !_prevQPressed && _session.Player.IsAlive)
            {
                var item = _session.Player.DropOneFromSelectedSlot();
                if (!item.IsEmpty)
                {
                    var dropPos = _camera.Position + _camera.Front * 0.5f;
                    var entity = _session.SpawnItemDrop(item, dropPos);
                    if (entity != null)
                    {
                        entity.Velocity = _camera.Front * 4.0f + Vector3.UnitY * 1.5f;
                    }
                }
            }
            _prevQPressed = qPressed;

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.G) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.G))
            {
                _session.Player.CreativeMode = !_session.Player.CreativeMode;
                _session.Player.Velocity = Vector3.Zero;
                Console.WriteLine($"[Mode] Toggled creative mode: {_session.Player.CreativeMode}");
            }

            if (CanProcessGameplayMouse())
            {
                var mouseLook = _input.ProcessMouseLook(mouseState, GraphicsDevice);
                mouseDx = mouseLook.DeltaX;
                mouseDy = mouseLook.DeltaY;
                centerX = mouseLook.CenterX;
                centerY = mouseLook.CenterY;

                if (mouseDx != 0f || mouseDy != 0f)
                {
                    _camera.Yaw += mouseDx * 0.15f;
                    _camera.Pitch = Math.Clamp(_camera.Pitch - mouseDy * 0.15f, -89f, 89f);

                    _session.Player.Yaw = _camera.Yaw;
                    _session.Player.Pitch = _camera.Pitch;
                }

                leftHeld = mouseState.LeftButton == ButtonState.Pressed;
                leftPressed = leftHeld && _input.PrevMouse.LeftButton == ButtonState.Released;
                rightPressed = mouseState.RightButton == ButtonState.Pressed && _input.PrevMouse.RightButton == ButtonState.Released;
                shiftHeld = kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                    || kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
                shiftRightPressed = rightPressed && shiftHeld;
                if (shiftHeld)
                {
                    rightPressed = false;
                }

                int scrollDelta = mouseState.ScrollWheelValue - _input.PrevMouse.ScrollWheelValue;
                if (scrollDelta != 0)
                {
                    int slotChange = -Math.Sign(scrollDelta);
                    int newSlot = _session.Player.SelectedSlot + slotChange;
                    if (newSlot < 0) newSlot = 8;
                    if (newSlot > 8) newSlot = 0;
                    _session.Player.SelectedSlot = newSlot;
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {newSlot + 1}: {FormatHotbarStack(_session.Player.Hotbar[newSlot])}");
                }
            }

            if (_session.Player.IsAlive)
            {
                ProcessAlivePlayerMovement(deltaTime, kbState, leftHeld, leftPressed, rightPressed, shiftRightPressed, shiftHeld);
            }

            return false;
        }

        private void ProcessAlivePlayerMovement(
            float deltaTime,
            KeyboardState kbState,
            bool leftHeld,
            bool leftPressed,
            bool rightPressed,
            bool shiftRightPressed,
            bool shiftHeld)
        {
            Vector3 front = _camera.Front;
            Vector3 right = _camera.Right;
            Vector3 frontHorizontal = Vector3.Normalize(new Vector3(front.X, 0f, front.Z));
            Vector3 rightHorizontal = Vector3.Normalize(new Vector3(right.X, 0f, right.Z));

            Vector3 moveDir = Vector3.Zero;
            if (IsKeyPressed(kbState, Key.W)) moveDir += frontHorizontal;
            if (IsKeyPressed(kbState, Key.S)) moveDir -= frontHorizontal;
            if (IsKeyPressed(kbState, Key.A)) moveDir -= rightHorizontal;
            if (IsKeyPressed(kbState, Key.D)) moveDir += rightHorizontal;

            var swPlayer = System.Diagnostics.Stopwatch.StartNew();

            bool wPressedThisFrame = IsKeyPressed(kbState, Key.W) && !_prevWPressed;
            _prevWPressed = IsKeyPressed(kbState, Key.W);
            if (wPressedThisFrame)
            {
                float timeSinceLastW = _waterAnimTime - _lastWPressTime;
                if (timeSinceLastW < 0.25f)
                {
                    _doubleTapSprintActive = true;
                }
                _lastWPressTime = _waterAnimTime;
            }
            if (!IsKeyPressed(kbState, Key.W))
            {
                _doubleTapSprintActive = false;
            }

            bool ctrlPressed = IsKeyPressed(kbState, Key.ControlLeft);
            bool isSprintingWanted = IsKeyPressed(kbState, Key.W) && (ctrlPressed || _doubleTapSprintActive);

            if (isSprintingWanted && _session.Player.Hunger > _session.Player.MaxHunger * SurvivalConstants.LowHungerFraction && !_session.Player.InWater)
            {
                _session.Player.IsSprinting = true;
            }
            else
            {
                _session.Player.IsSprinting = false;
            }

            if (_session.Player.CreativeMode)
            {
                if (IsKeyPressed(kbState, Key.Space)) moveDir += Vector3.UnitY;
                if (IsKeyPressed(kbState, Key.ShiftLeft) || IsKeyPressed(kbState, Key.ShiftRight)) moveDir -= Vector3.UnitY;
                _session.Player.Update(deltaTime, _session.Grid, moveDir);
            }
            else
            {
                bool swimUp = IsKeyPressed(kbState, Key.Space);
                bool swimDown = IsKeyPressed(kbState, Key.ShiftLeft) || IsKeyPressed(kbState, Key.ShiftRight);
                if (IsKeyPressed(kbState, Key.Space) && !_prevSpacePressed)
                {
                    if (!_session.Player.InWater || !_session.Player.HeadUnderwater)
                    {
                        _session.Player.Jump();
                        _session.PlayJumpSound();
                    }
                }

                _session.Player.Update(deltaTime, _session.Grid, moveDir, swimUp, swimDown);
            }

            swPlayer.Stop();
            PerfCounters.UpdatePlayerMs = (float)swPlayer.Elapsed.TotalMilliseconds;

            _prevSpacePressed = IsKeyPressed(kbState, Key.Space);
            _session.UpdateMovementAudio(deltaTime, moveDir);
            _session.InteractionAnimator.Update(deltaTime, _session.Player);
            _session.Weather.Update(deltaTime);

            UpdatePlayerEnvironmentEffects(deltaTime);

            _session.Combat.HandleLandingEffects(_session.Player, _session.Particles, _session.InteractionAnimator);
            SyncCameraFromPlayer();

            if (!CanProcessGameplayMouse())
            {
                return;
            }

            var solidRayHit = BlockInteractionSystem.RaycastSolidHit(
                _session.Grid,
                _camera.Position,
                _camera.Front,
                BlockInteractionSystem.RaycastRange);

            bool shiftLeftPressed = leftPressed && shiftHeld;
            bool handledClick = false;

            if (_blueprints.PendingBlueprintId != null && leftPressed)
            {
                _blueprints.ConfirmBlueprintPlacement();
                handledClick = true;
            }
            else if (_blueprints.WorkZonePlacementActive && leftPressed && solidRayHit.HasHit)
            {
                var zoneVillage = _session.Villages.GetActiveVillage(_session.Player.Position);
                if (zoneVillage != null)
                {
                    _blueprints.ConfirmWorkZoneCorner(
                        zoneVillage,
                        (int)solidRayHit.BlockPos.X,
                        (int)solidRayHit.BlockPos.Y,
                        (int)solidRayHit.BlockPos.Z);
                }

                handledClick = true;
            }
            else if (shiftLeftPressed && solidRayHit.HasHit)
            {
                if (_session.Villages.TryMarkWorkBlock(
                        _session.Grid,
                        (int)solidRayHit.BlockPos.X,
                        (int)solidRayHit.BlockPos.Y,
                        (int)solidRayHit.BlockPos.Z,
                        out string markMessage))
                {
                    _session.HudToast.Show(markMessage);
                }
                else
                {
                    _session.HudToast.Show(markMessage);
                }

                handledClick = true;
            }

            bool allowMining = leftHeld && !handledClick && !shiftHeld && !_session.Combat.BlocksMiningThisFrame;

            _session.Combat.Update(
                deltaTime,
                _session.Grid,
                _session.Player,
                _session.Animals,
                _session.BlockInteraction,
                _session.Particles,
                _session.InteractionAnimator,
                _camera.Position,
                _camera.Front,
                allowMining ? leftHeld : false,
                handledClick ? false : leftPressed,
                solidRayHit);

            _session.BlockInteraction.Update(
                deltaTime,
                _session.Grid,
                _session.Player,
                _camera.Position,
                _camera.Front,
                allowMining,
                rightPressed,
                shiftRightPressed,
                _session.Crafting,
                _session.Particles,
                GraphicsDevice,
                solidRayHit,
                _blueprints.PendingBlueprintId != null);

            if (_session.BlockInteraction.PendingStationOpen.HasValue)
            {
                var stationPos = _session.BlockInteraction.PendingStationOpen.Value;
                OpenCrucibleAt(
                    (int)stationPos.X,
                    (int)stationPos.Y,
                    (int)stationPos.Z,
                    _session.BlockInteraction.PendingStationType);
            }
        }

        private void UpdatePlayerEnvironmentEffects(float deltaTime)
        {
            var swParticles = System.Diagnostics.Stopwatch.StartNew();
            _session.Particles.Update(deltaTime, _session.Grid);
            _session.Particles.UpdateAmbient(deltaTime, _session.Player.Position, _session.Grid, _timeOfDay, _session.Weather);
            swParticles.Stop();
            PerfCounters.UpdateParticlesMs = (float)swParticles.Elapsed.TotalMilliseconds;

            if (_session.Player.InWater && !_playerWasInWater)
            {
                _session.Particles.SpawnWaterSplash(_session.Player.Position + new Vector3(0f, 0.2f, 0f), 0.9f);
                _session.PlayWaterSplashSound();
            }
            else if (!_session.Player.InWater && _playerWasInWater)
            {
                _session.Particles.SpawnWaterSplash(_session.Player.Position, 1.0f);
                _session.PlayWaterSplashSound();
            }
            else if (_session.Player.JustLanded && WaterQuery.IsLandingInWater(_session.Grid, _session.Player.Position))
            {
                _session.Particles.SpawnWaterSplash(_session.Player.Position, 1.3f);
                _session.PlayWaterSplashSound();
            }

            _playerWasInWater = _session.Player.InWater;

            if (_session.Player.InLava && !_playerWasInLava)
            {
                _session.Particles.SpawnWaterSplash(_session.Player.Position + new Vector3(0f, 0.2f, 0f), 0.9f);
                _session.PlayWaterSplashSound();
            }
            else if (!_session.Player.InLava && _playerWasInLava)
            {
                _session.Particles.SpawnWaterSplash(_session.Player.Position, 1.0f);
                _session.PlayWaterSplashSound();
            }
            else if (_session.Player.JustLanded && LavaQuery.IsLandingInLava(_session.Grid, _session.Player.Position))
            {
                _session.Particles.SpawnWaterSplash(_session.Player.Position, 1.3f);
                _session.PlayWaterSplashSound();
            }
            _playerWasInLava = _session.Player.InLava;

            if (_session.Player.HeadUnderwater)
            {
                _underwaterBubbleTimer += deltaTime;
                if (_underwaterBubbleTimer > 0.35f)
                {
                    _underwaterBubbleTimer = 0f;
                    _session.Particles.SpawnUnderwaterBubble(_camera.Position);
                }
            }
            else
            {
                _underwaterBubbleTimer = 0f;
            }
        }

        public void SimulateClick(MouseButton button)
        {
            var action = new Action(() =>
            {
                SyncCameraFromPlayer();
                if (button == MouseButton.Left)
                {
                    if (!_session.Combat.TryInstantAttack(_session.Grid, _session.Player, _session.Animals, _session.BlockInteraction, _session.Particles, _session.InteractionAnimator, _camera.Position, _camera.Front))
                    {
                        _session.BlockInteraction.InstantMine(_session.Grid, _session.Player, _camera.Position, _camera.Front, _session.Particles, _runTests ? null : GraphicsDevice);
                    }
                }
                else if (button == MouseButton.Right)
                {
                    _session.BlockInteraction.InstantPlace(_session.Grid, _session.Player, _camera.Position, _camera.Front, _session.Particles, _runTests ? null : GraphicsDevice);
                }
            });

            if (_runTests) action();
            else _pendingActions.Enqueue(action);
        }

        private static string FormatHotbarStack(ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return "empty";
            }

            if (stack.IsTool())
            {
                return $"{stack.GetDisplayName()} ({stack.Durability}/{stack.MaxDurability})";
            }

            return $"{stack.BlockType} (x{stack.Count})";
        }
    }
}
