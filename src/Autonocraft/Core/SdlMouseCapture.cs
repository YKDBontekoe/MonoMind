using System;
using System.Runtime.InteropServices;

namespace Autonocraft.Core
{
    /// <summary>
    /// SDL2 relative mouse mode for FPS look — avoids Mouse.SetPosition warping jitter.
    /// See: SDL_SetRelativeMouseMode / SDL_GetRelativeMouseState
    /// </summary>
    internal static class SdlMouseCapture
    {
        private static bool _relativeModeEnabled;

        private enum SdlBool
        {
            False = 0,
            True = 1
        }

        [DllImport("SDL2", EntryPoint = "SDL_SetHint", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int SetHintNative(string name, string value);

        [DllImport("SDL2", EntryPoint = "SDL_SetRelativeMouseMode", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SetRelativeMouseModeNative(SdlBool enabled);

        [DllImport("SDL2", EntryPoint = "SDL_GetRelativeMouseMode", CallingConvention = CallingConvention.Cdecl)]
        private static extern SdlBool GetRelativeMouseModeNative();

        [DllImport("SDL2", EntryPoint = "SDL_GetRelativeMouseState", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint GetRelativeMouseStateNative(out int x, out int y);

        private static bool _hintsConfigured;

        private static void EnsureHints()
        {
            if (_hintsConfigured)
            {
                return;
            }

            try
            {
                // SDL recenters internally in relative mode; do not warp from game code.
                SetHintNative("SDL_MOUSE_RELATIVE_MODE_WARP", "1");
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            _hintsConfigured = true;
        }

        public static bool IsRelativeModeEnabled => _relativeModeEnabled;

        public static bool TryEnableRelativeMode()
        {
            EnsureHints();
            try
            {
                if (SetRelativeMouseModeNative(SdlBool.True) == 0)
                {
                    _relativeModeEnabled = GetRelativeMouseModeNative() == SdlBool.True;
                    DrainRelativeDelta();
                    return _relativeModeEnabled;
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            _relativeModeEnabled = false;
            return false;
        }

        public static void DisableRelativeMode()
        {
            if (!_relativeModeEnabled)
            {
                return;
            }

            try
            {
                SetRelativeMouseModeNative(SdlBool.False);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            _relativeModeEnabled = false;
            try
            {
                if (GetRelativeMouseModeNative() == SdlBool.True)
                {
                    SetRelativeMouseModeNative(SdlBool.False);
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        public static bool TryGetRelativeDelta(out int dx, out int dy)
        {
            if (!_relativeModeEnabled)
            {
                dx = 0;
                dy = 0;
                return false;
            }

            try
            {
                GetRelativeMouseStateNative(out dx, out dy);
                return true;
            }
            catch (DllNotFoundException)
            {
                dx = 0;
                dy = 0;
                _relativeModeEnabled = false;
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                dx = 0;
                dy = 0;
                _relativeModeEnabled = false;
                return false;
            }
        }

        public static void DrainRelativeDelta()
        {
            TryGetRelativeDelta(out _, out _);
        }
    }
}
