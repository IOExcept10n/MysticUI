// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering.Brushes
{
    /// <summary>
    /// Represents an interface for all image brushes.
    /// </summary>
    /// <remarks>
    /// Image brushes contain a fixed-size texture to apply when rendering.
    /// </remarks>
    public interface IImage : IBrush
    {
        /// <summary>
        /// Gets the size of an image.
        /// </summary>
        Size Size { get; }
    }
}
