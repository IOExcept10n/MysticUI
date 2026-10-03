// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.InteropServices;

namespace Icy.Rendering.Display
{
    /// <summary>
    /// Provides static methods to get the desktop-size provider for the current platform.
    /// </summary>
    /// <remarks>
    /// Only Windows has a dedicated provider today. Other platforms get a <see cref="FixedDesktopSizeProvider"/> that reports
    /// the size as unknown; a macOS (CoreGraphics), Linux (XRandR/Wayland) or engine-supplied (e.g. SDL) provider can be
    /// added without API changes.
    /// </remarks>
    public static class DesktopSizes
    {
        /// <summary>
        /// Gets the desktop-size provider for the current platform, or a <see cref="FixedDesktopSizeProvider"/> reporting
        /// the size as unknown when the platform has none.
        /// </summary>
        /// <returns>A desktop-size provider; never <see langword="null"/>.</returns>
        public static IDesktopSizeProvider GetProvider() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new WindowsDesktopSizeProvider() : new FixedDesktopSizeProvider();
    }
}
