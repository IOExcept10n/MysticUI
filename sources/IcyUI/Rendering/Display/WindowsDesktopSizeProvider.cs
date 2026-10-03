// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Runtime.InteropServices;

namespace Icy.Rendering.Display
{
    /// <summary>
    /// Reads the desktop resolution of a window's monitor on Windows, through <c>MonitorFromWindow</c>,
    /// <c>GetMonitorInfoW</c> and <c>EnumDisplaySettingsW(ENUM_REGISTRY_SETTINGS)</c>.
    /// </summary>
    /// <remarks>
    /// The registry settings are the resolution the user configured. They stay at the panel's resolution even while an
    /// exclusive-fullscreen game has switched the display to a smaller mode, which the current mode would report instead.
    /// </remarks>
    internal sealed partial class WindowsDesktopSizeProvider : IDesktopSizeProvider
    {
        private const uint MonitorDefaultToNearest = 2;
        private const int EnumRegistrySettings = -2;

        // MONITORINFOEXW: cbSize, rcMonitor, rcWork, dwFlags (40 bytes), then szDevice[32] WCHARs.
        private const int MonitorInfoExSize = 104;
        private const int MonitorInfoDeviceOffset = 40;

        // DEVMODEW: dmSize at 68, dmPelsWidth at 172, dmPelsHeight at 176, 220 bytes in total.
        private const int DevModeSize = 220;
        private const int DevModeSizeOffset = 68;
        private const int DevModeWidthOffset = 172;
        private const int DevModeHeightOffset = 176;

        /// <inheritdoc/>
        public unsafe bool TryGetDesktopSize(in NativeWindowInfo window, out Size size)
        {
            size = Size.Empty;
            string? deviceName = null;

            if (window.Handle != 0)
            {
                nint monitor = MonitorFromWindow(window.Handle, MonitorDefaultToNearest);
                byte* info = stackalloc byte[MonitorInfoExSize];
                new Span<byte>(info, MonitorInfoExSize).Clear();
                *(int*)info = MonitorInfoExSize;
                if (monitor != 0 && GetMonitorInfo(monitor, info))
                    deviceName = new string((char*)(info + MonitorInfoDeviceOffset));
            }

            byte* devMode = stackalloc byte[DevModeSize];
            new Span<byte>(devMode, DevModeSize).Clear();
            *(ushort*)(devMode + DevModeSizeOffset) = DevModeSize;

            // A null device name means the display the calling thread runs on (the primary one).
            if (!EnumDisplaySettings(deviceName, EnumRegistrySettings, devMode))
                return false;

            int width = (int)*(uint*)(devMode + DevModeWidthOffset);
            int height = (int)*(uint*)(devMode + DevModeHeightOffset);
            if (width <= 0 || height <= 0)
                return false;

            size = new Size(width, height);
            return true;
        }

        [LibraryImport("user32.dll")]
        private static partial nint MonitorFromWindow(nint hwnd, uint flags);

        [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool GetMonitorInfo(nint monitor, byte* info);

        [LibraryImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool EnumDisplaySettings(string? deviceName, int modeNumber, byte* devMode);
    }
}
