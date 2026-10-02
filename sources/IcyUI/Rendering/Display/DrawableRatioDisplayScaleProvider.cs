// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Display
{
    /// <summary>
    /// Derives the display scale from the ratio of the drawable (back-buffer) width to the window width, as on macOS
    /// Retina displays and Linux compositors that expose high-DPI drawables.
    /// </summary>
    internal sealed class DrawableRatioDisplayScaleProvider : IDisplayScaleProvider
    {
        /// <inheritdoc/>
        public float GetScale(in NativeWindowInfo window) =>
            window.WindowSize.Width > 0 && window.DrawableSize.Width > 0
                ? (float)window.DrawableSize.Width / window.WindowSize.Width
                : 1f;
    }
}
