// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering.Display
{
    /// <summary>
    /// An <see cref="IDesktopSizeProvider"/> that always reports the same size, or always reports it as unknown.
    /// </summary>
    /// <remarks>
    /// Used as the fallback on platforms without a dedicated provider (see <see cref="DesktopSizes.GetProvider"/>), and
    /// useful for tests or for forcing a size.
    /// </remarks>
    public sealed class FixedDesktopSizeProvider : IDesktopSizeProvider
    {
        private readonly Size? size;

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedDesktopSizeProvider"/> class.
        /// </summary>
        /// <param name="size">The size to report, or <see langword="null"/> (the default) to always report the size as unknown.</param>
        public FixedDesktopSizeProvider(Size? size = null)
        {
            this.size = size is { Width: > 0, Height: > 0 } ? size : null;
        }

        /// <inheritdoc/>
        public bool TryGetDesktopSize(in NativeWindowInfo window, out Size size)
        {
            size = this.size ?? Size.Empty;
            return this.size.HasValue;
        }
    }
}
