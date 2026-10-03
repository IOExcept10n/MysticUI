// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI
{
    /// <summary>
    /// Specifies the axes an element wants to own a drag along (see <see cref="UIElement.GetDragAxes(in DragClaimContext)"/>).
    /// </summary>
    [Flags]
    public enum DragAxes
    {
        /// <summary>
        /// The element doesn't want the drag.
        /// </summary>
        None = 0,

        /// <summary>
        /// The element wants drags along its horizontal axis.
        /// </summary>
        Horizontal = 1,

        /// <summary>
        /// The element wants drags along its vertical axis.
        /// </summary>
        Vertical = 2,

        /// <summary>
        /// The element wants drags along both axes.
        /// </summary>
        Both = Horizontal | Vertical,
    }
}
