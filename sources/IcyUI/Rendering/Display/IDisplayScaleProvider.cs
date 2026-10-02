// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Display
{
    /// <summary>
    /// Represents a platform-specific way to read the display scale of a native window.
    /// </summary>
    /// <remarks>
    /// Obtain the implementation for the current platform from <see cref="DisplayScales.GetProvider"/>.
    /// </remarks>
    public interface IDisplayScaleProvider
    {
        /// <summary>
        /// Gets the display scale of the specified window, where <c>1.0</c> means 96 DPI (100 %).
        /// </summary>
        /// <param name="window">The native window to query.</param>
        /// <returns>The display scale. Implementations must not throw; they return <c>1.0</c> when the scale can't be determined.</returns>
        float GetScale(in NativeWindowInfo window);
    }
}
