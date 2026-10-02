// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Rendering.Display;
using Stride.Engine;

namespace Icy.Stride.Rendering
{
    /// <summary>
    /// Builds <see cref="NativeWindowInfo"/> for a Stride game window, for core display-scale detection.
    /// </summary>
    internal static class StrideNativeWindow
    {
        /// <summary>
        /// Gets the native window info of the specified game's window.
        /// </summary>
        /// <param name="game">The game whose window to describe.</param>
        /// <returns>
        /// The window's <c>HWND</c> (Windows only, otherwise <c>0</c>), client size and back-buffer size. The back-buffer size
        /// falls back to the client size when the presenter isn't available yet.
        /// </returns>
        public static NativeWindowInfo GetInfo(Game game)
        {
            var window = game.Window;
            var client = window.ClientBounds;
            var backBuffer = game.GraphicsDevice.Presenter?.BackBuffer;
            nint handle = OperatingSystem.IsWindows() ? window.NativeWindow?.Handle ?? 0 : 0;
            return new NativeWindowInfo(
                handle,
                new System.Drawing.Size(client.Width, client.Height),
                new System.Drawing.Size(backBuffer?.Width ?? client.Width, backBuffer?.Height ?? client.Height));
        }
    }
}
