// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// Specifies whether a <see cref="ScrollViewer"/> scrolls along one axis.
    /// </summary>
    /// <remarks>
    /// Set per axis with <see cref="ScrollViewer.HorizontalScrollMode"/> and <see cref="ScrollViewer.VerticalScrollMode"/>.
    /// Unlike <see cref="PanningMode"/>, which only decides which drags pan, this decides how the content is laid out.
    /// </remarks>
    public enum ScrollMode
    {
        /// <summary>
        /// The content keeps its natural size along the axis and scrolls when it's larger than the viewport.
        /// </summary>
        Enabled,

        /// <summary>
        /// The content is laid out within the viewport along the axis, the way a non-scrolling parent would lay it out,
        /// and never scrolls along it.
        /// </summary>
        Disabled,
    }
}
