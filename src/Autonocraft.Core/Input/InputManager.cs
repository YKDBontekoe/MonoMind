using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Engine;

namespace Autonocraft.Core
{
    /// <summary>
    /// Keyboard/mouse state caching, window focus handling, and relative mouse capture for gameplay.
    /// </summary>
    internal sealed class InputManager
    {
        private const int MouseWarpRejectThreshold = 120;

        private readonly Game _game;

        private bool _wasActive = true;
        private bool _deferPrevMouseReset;
        private bool _useWarpMouseFallback;
        private bool _isWindowActive = true;

        public bool UsesWarpMouseFallback => _useWarpMouseFallback;
        public bool IsWindowActive => _isWindowActive;
        public KeyboardState PrevKeyboard { get; private set; }
        public MouseState PrevMouse { get; private set; }
        public bool IsMouseLocked { get; set; } = true;
        public bool SkipMouseLookFrame { get; set; }
        public int GameFrameCount { get; private set; }
        public int LastInventoryKeyFrame { get; set; } = -1;

        public bool MouseLockedBeforeCrafting { get; set; }
        public bool MouseLockedBeforeVillageUi { get; set; }
        public bool MouseLockedBeforeConsole { get; set; }
        public bool MouseLockedBeforePause { get; set; }
        public bool MouseLockedBeforeDeath { get; set; }

        public InputManager(Game game) => _game = game;

        public void InitializeAtStartup()
        {
            _game.IsMouseVisible = true;
            IsMouseLocked = false;
            _wasActive = false;
        }

        public void SeedInitialInputState()
        {
            PrevMouse = GetUiMouseState();
            PrevKeyboard = Keyboard.GetState();
        }

        public bool ShouldCaptureMouse(GameState state, in GameplayInputBlockers blockers) =>
            state == GameState.Playing
            && IsMouseLocked
            && !blockers.HasBlockingOverlay;

        public void PrepareMouseForUi()
        {
            IsMouseLocked = false;
            ReleaseMouseCapture();
            _game.IsMouseVisible = true;
            _deferPrevMouseReset = true;
            SdlWindowGrab.RaiseWindow(_game.Window.Handle);
        }

        public void EnsureCursorFreeOutsideGameplay(GameState state, in GameplayInputBlockers blockers)
        {
            if (state == GameState.Playing && ShouldCaptureMouse(state, blockers))
            {
                return;
            }

            if (SdlMouseCapture.IsRelativeModeEnabled || !_game.IsMouseVisible)
            {
                ReleaseMouseCapture();
                _game.IsMouseVisible = true;
            }
        }

        public MouseState GetUiMouseState() => Mouse.GetState();

        public void EnsureUiPointerMode()
        {
            IsMouseLocked = false;
            ReleaseMouseCapture();
            _game.IsMouseVisible = true;
        }

        public void TryActivateWindow() => SdlWindowGrab.RaiseWindow(_game.Window.Handle);

        public void ApplyMouseCapture()
        {
            _game.IsMouseVisible = false;
            _useWarpMouseFallback = !SdlMouseCapture.TryEnableRelativeMode();
            PrevMouse = GetUiMouseState();
            SkipMouseLookFrame = true;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SdlWindowGrab.SetGrabbed(_game.Window.Handle, true);
            }

            InputDebugTrace.Log($"MOUSE_CAPTURE relative={SdlMouseCapture.IsRelativeModeEnabled} warpFallback={_useWarpMouseFallback}");
        }

        public void ReleaseMouseCapture()
        {
            _useWarpMouseFallback = false;
            SdlMouseCapture.DisableRelativeMode();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SdlWindowGrab.SetGrabbed(_game.Window.Handle, false);
            }
        }

        public Point GetMouseClientCenter(GraphicsDevice graphicsDevice)
        {
            int viewportCenterX = graphicsDevice.Viewport.Width / 2;
            int viewportCenterY = graphicsDevice.Viewport.Height / 2;

            var bounds = _game.Window.ClientBounds;
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                return UiInput.ToClient(new Point(viewportCenterX, viewportCenterY), _game.Window, graphicsDevice);
            }

            return new Point(viewportCenterX, viewportCenterY);
        }

        public void HandleFocusGained(in GameplayInputBlockers blockers, GameState state)
        {
            if (ShouldCaptureMouse(state, blockers))
            {
                ApplyMouseCapture();
                InputDebugTrace.Log("FOCUS_GAINED recaptured mouse");
            }
        }

        public void HandleFocusLost(in GameplayInputBlockers blockers, GameState state)
        {
            if (state == GameState.Playing && IsMouseLocked)
            {
                ReleaseMouseCapture();
                _game.IsMouseVisible = true;
                InputDebugTrace.Log("FOCUS_LOST disabled relative mouse");
            }
        }

        public bool CanProcessGameplayMouse(GameState state, in GameplayInputBlockers blockers) =>
            _isWindowActive
            && state == GameState.Playing
            && IsMouseLocked
            && !blockers.HasBlockingOverlay;

        public void TryRecaptureMouseOnClick(GameState state, in GameplayInputBlockers blockers, MouseState mouseState)
        {
            if (!_isWindowActive
                || state != GameState.Playing
                || IsMouseLocked
                || blockers.HasBlockingOverlay)
            {
                return;
            }

            if (mouseState.LeftButton != ButtonState.Pressed || PrevMouse.LeftButton != ButtonState.Released)
            {
                return;
            }

            IsMouseLocked = true;
            ApplyMouseCapture();
            InputDebugTrace.Log("CLICK_RECAPTURE mouse lock restored");
        }

        public void EnsureMouseLockedForGameplay()
        {
            if (!IsMouseLocked)
            {
                IsMouseLocked = true;
                ApplyMouseCapture();
            }
        }

        public void ResetInactiveTimer()
        {
        }

        public void ReleaseMouseOnLeavePlaying()
        {
            IsMouseLocked = false;
            ReleaseMouseCapture();
            _game.IsMouseVisible = true;
            InputDebugTrace.Log("LEFT_PLAYING, cursor released");
        }

        public void RestoreMouseLockAfterOverlay()
        {
            if (IsMouseLocked && _isWindowActive)
            {
                ApplyMouseCapture();
            }
            else
            {
                _game.IsMouseVisible = true;
            }
        }

        public void UpdateFocusAndInactiveTimer(
            bool isActive,
            float deltaTime,
            GameState state,
            in GameplayInputBlockers blockers)
        {
            _isWindowActive = isActive;

            if (isActive && !_wasActive)
            {
                HandleFocusGained(blockers, state);
            }
            else if (!isActive && _wasActive)
            {
                HandleFocusLost(blockers, state);
            }

            if (!isActive && state == GameState.Playing && IsMouseLocked)
            {
                if (SdlMouseCapture.IsRelativeModeEnabled || !_game.IsMouseVisible)
                {
                    ReleaseMouseCapture();
                    _game.IsMouseVisible = true;
                }
            }

            _wasActive = isActive;
        }

        public void BeginFrame() => GameFrameCount++;

        public void EndFrame(KeyboardState kbState, MouseState mouseState)
        {
            PrevKeyboard = kbState;
            if (_deferPrevMouseReset)
            {
                PrevMouse = GetUiMouseState();
                _deferPrevMouseReset = false;
            }
            else
            {
                PrevMouse = mouseState;
            }
        }

        public bool IsKeyJustPressed(KeyboardState kbState, Keys key) =>
            kbState.IsKeyDown(key) && !PrevKeyboard.IsKeyDown(key);

        public MouseLookResult ProcessMouseLook(MouseState mouseState, GraphicsDevice graphicsDevice)
        {
            var clientCenter = GetMouseClientCenter(graphicsDevice);
            float mouseDx = 0f;
            float mouseDy = 0f;

            if (!SkipMouseLookFrame)
            {
                if (SdlMouseCapture.TryGetRelativeDelta(out int relativeDx, out int relativeDy))
                {
                    mouseDx = relativeDx;
                    mouseDy = relativeDy;
                }
                else if (_useWarpMouseFallback)
                {
                    mouseDx = mouseState.X - clientCenter.X;
                    mouseDy = mouseState.Y - clientCenter.Y;
                    if (Math.Abs(mouseDx) > MouseWarpRejectThreshold || Math.Abs(mouseDy) > MouseWarpRejectThreshold)
                    {
                        mouseDx = 0f;
                        mouseDy = 0f;
                    }

                    TryWarpMouseToCenter(clientCenter);
                }
                else
                {
                    mouseDx = mouseState.X - PrevMouse.X;
                    mouseDy = mouseState.Y - PrevMouse.Y;
                    if (Math.Abs(mouseDx) > MouseWarpRejectThreshold || Math.Abs(mouseDy) > MouseWarpRejectThreshold)
                    {
                        mouseDx = 0f;
                        mouseDy = 0f;
                    }
                }
            }
            else
            {
                SdlMouseCapture.DrainRelativeDelta();
                SkipMouseLookFrame = false;
            }

            return new MouseLookResult(mouseDx, mouseDy, clientCenter.X, clientCenter.Y);
        }

        private void TryWarpMouseToCenter(Point clientCenter)
        {
            try
            {
                Mouse.SetPosition(clientCenter.X, clientCenter.Y);
            }
            catch (Exception ex)
            {
                InputDebugTrace.Log($"MOUSE_WARP failed: {ex.Message}");
            }
        }

        public readonly struct GameplayInputBlockers
        {
            public bool DevConsoleOpen { get; init; }
            public bool PauseMenuOpen { get; init; }
            public bool DeathScreenOpen { get; init; }
            public bool VillageScreenOpen { get; init; }
            public bool VillageChatOpen { get; init; }
            public bool JournalOpen { get; init; }
            public bool CrucibleOpen { get; init; }
            public bool InventoryOpen { get; init; }

            public bool HasBlockingOverlay =>
                DevConsoleOpen
                || PauseMenuOpen
                || DeathScreenOpen
                || VillageScreenOpen
                || VillageChatOpen
                || JournalOpen
                || CrucibleOpen
                || InventoryOpen;
        }

        public readonly struct MouseLookResult
        {
            public MouseLookResult(float dx, float dy, int centerX, int centerY)
            {
                DeltaX = dx;
                DeltaY = dy;
                CenterX = centerX;
                CenterY = centerY;
            }

            public float DeltaX { get; }
            public float DeltaY { get; }
            public int CenterX { get; }
            public int CenterY { get; }
        }
    }
}
