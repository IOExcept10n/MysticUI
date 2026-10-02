// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.InteropServices;

namespace Icy.Rendering.Display
{
    /// <summary>
    /// Reads the display scale on Windows through <c>GetDpiForWindow</c>, falling back to <c>GetDpiForSystem</c>.
    /// </summary>
    /// <remarks>
    /// Both functions respect the calling process's DPI awareness: a process that isn't DPI-aware always gets 96 DPI
    /// (scale <c>1.0</c>) while Windows stretches its window, so OS scaling and IcyUI scaling never stack.
    /// </remarks>
    internal sealed partial class WindowsDisplayScaleProvider : IDisplayScaleProvider
    {
        private const float DefaultDpi = 96f;

        /// <inheritdoc/>
        public float GetScale(in NativeWindowInfo window)
        {
            uint dpi = window.Handle != 0 ? GetDpiForWindow(window.Handle) : 0;
            if (dpi == 0)
                dpi = GetDpiForSystem();
            return dpi == 0 ? 1f : dpi / DefaultDpi;
        }

        [LibraryImport("user32.dll")]
        private static partial uint GetDpiForWindow(nint hwnd);

        [LibraryImport("user32.dll")]
        private static partial uint GetDpiForSystem();
    }
}
