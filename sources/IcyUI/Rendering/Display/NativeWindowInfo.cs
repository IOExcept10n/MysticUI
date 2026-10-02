// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering.Display
{
    /// <summary>
    /// Describes the native window an engine renders into - the only data an engine integration supplies for display-scale detection.
    /// </summary>
    /// <param name="Handle">
    /// The native window handle: an <c>HWND</c> on Windows, or <c>0</c> when unavailable (Windows then falls back to the system DPI).
    /// Other platforms ignore it.
    /// </param>
    /// <param name="WindowSize">The window client size in OS window units (points on macOS).</param>
    /// <param name="DrawableSize">The back-buffer size in physical pixels.</param>
    public readonly record struct NativeWindowInfo(nint Handle, Size WindowSize, Size DrawableSize);
}
