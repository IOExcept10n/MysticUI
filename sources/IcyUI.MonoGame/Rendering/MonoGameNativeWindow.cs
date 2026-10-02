// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.InteropServices;
using Icy.Rendering.Display;
using Microsoft.Xna.Framework;

namespace Icy.MonoGame.Rendering
{
    /// <summary>
    /// Builds <see cref="NativeWindowInfo"/> for a MonoGame window, for core display-scale detection.
    /// </summary>
    /// <remarks>
    /// On DesktopGL, <see cref="GameWindow.Handle"/> is an <c>SDL_Window*</c>; the Win32 <c>HWND</c> comes from
    /// <c>SDL_GetWindowWMInfo</c> in the <c>SDL2</c> library DesktopGL ships. On WindowsDX the handle already is an <c>HWND</c>.
    /// Any failure yields <c>Handle = 0</c>, and core then falls back to the system DPI.
    /// </remarks>
    internal static class MonoGameNativeWindow
    {
        private const int SdlSysWmWindows = 1;
        private const int SdlSubsystemOffset = 4;
        private const int SdlWindowHandleOffset = 8;

        /// <summary>
        /// Gets the native window info of the specified game's window.
        /// </summary>
        /// <param name="game">The game whose window to describe.</param>
        /// <returns>The window's <c>HWND</c> (Windows only, otherwise <c>0</c>), client size and back-buffer size.</returns>
        public static NativeWindowInfo GetInfo(Game game)
        {
            GameWindow window = game.Window;
            var parameters = game.GraphicsDevice.PresentationParameters;
            return new NativeWindowInfo(
                GetHwnd(window),
                new System.Drawing.Size(window.ClientBounds.Width, window.ClientBounds.Height),
                new System.Drawing.Size(parameters.BackBufferWidth, parameters.BackBufferHeight));
        }

        private static nint GetHwnd(GameWindow window)
        {
            if (!OperatingSystem.IsWindows() || window.Handle == 0)
                return 0;
            if (window.GetType().Name != "SdlGameWindow")
                return window.Handle;

            // SDL_SysWMinfo: { SDL_version version (3 bytes, padded to 4); int subsystem; union { HWND window; ... } }.
            // On 64-bit the union is 8-byte aligned, so the HWND sits at offset 8.
            byte[] info = new byte[256];
            info[0] = 2; // SDL_MAJOR_VERSION - SDL only checks that the caller's major version is supported.
            try
            {
                if (SDL_GetWindowWMInfo(window.Handle, info) == 0 || BitConverter.ToInt32(info, SdlSubsystemOffset) != SdlSysWmWindows)
                    return 0;
            }
            catch (DllNotFoundException)
            {
                return 0;
            }
            catch (EntryPointNotFoundException)
            {
                return 0;
            }

            return (nint)BitConverter.ToInt64(info, SdlWindowHandleOffset);
        }

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_GetWindowWMInfo(nint window, [In, Out] byte[] info);
    }
}
