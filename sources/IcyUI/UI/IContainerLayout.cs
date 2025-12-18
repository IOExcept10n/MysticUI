// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.UI
{
    /// <summary>
    /// Represents elements that can provide their layout to child elements.
    /// </summary>
    public interface IContainerLayout
    {
        /// <summary>
        /// Gets the area ready for children elements placement.
        /// </summary>
        Rectangle ContentBounds { get; }
    }
}