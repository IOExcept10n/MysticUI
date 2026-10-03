// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering.Display
{
    /// <summary>
    /// Represents a platform-specific way to read the desktop resolution of the monitor a native window is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The desktop resolution is the one the user configured, not a mode a fullscreen game switched to temporarily. It is
    /// the physical panel a touch screen measures against: engine connectors use it to map touches onto a back buffer that
    /// fullscreen presents letterboxed (see <see cref="Input.Devices.PointerSurfaceMapping"/>).
    /// </para>
    /// <para>
    /// Obtain the implementation for the current platform from <see cref="DesktopSizes.GetProvider"/>. Platforms without one
    /// report the size as unknown, and callers keep their unmapped behavior.
    /// </para>
    /// </remarks>
    public interface IDesktopSizeProvider
    {
        /// <summary>
        /// Tries to get the desktop resolution of the monitor <paramref name="window"/> is on.
        /// </summary>
        /// <param name="window">The native window; a <see cref="NativeWindowInfo.Handle"/> of <c>0</c> means the primary monitor.</param>
        /// <param name="size">The desktop resolution in physical pixels, or <see cref="Size.Empty"/> when unknown.</param>
        /// <returns><see langword="true"/> if the size is known; otherwise <see langword="false"/>. Implementations must not throw.</returns>
        bool TryGetDesktopSize(in NativeWindowInfo window, out Size size);
    }
}
