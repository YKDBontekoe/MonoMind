using System;
using System.Runtime.InteropServices;

namespace Autonocraft.Core
{
    internal static class SdlWindowGrab
    {
        private enum SdlBool
        {
            False = 0,
            True = 1
        }

        [DllImport("SDL2", EntryPoint = "SDL_SetWindowGrab", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetWindowGrab(IntPtr window, SdlBool grabbed);

        [DllImport("SDL2", EntryPoint = "SDL_RaiseWindow", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RaiseWindowNative(IntPtr window);

        public static void RaiseWindow(IntPtr window)
        {
            if (window == IntPtr.Zero)
            {
                return;
            }

            try
            {
                RaiseWindowNative(window);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        public static void SetGrabbed(IntPtr window, bool grabbed)
        {
            if (window == IntPtr.Zero)
            {
                return;
            }

            try
            {
                SetWindowGrab(window, grabbed ? SdlBool.True : SdlBool.False);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }
}
