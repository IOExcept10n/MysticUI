// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Turns moves and resizes inside one kind of container into attribute edits: an insertion index for a stack, a
    /// cell for a grid, margins for anything else.
    /// </summary>
    /// <remarks>
    /// Register strategies for custom containers in <see cref="PlacementRegistry"/>. All coordinates are in the
    /// container's local space.
    /// </remarks>
    public interface IPlacementStrategy
    {
        /// <summary>
        /// Gets the attributes that only mean something inside this kind of container, such as <c>Grid.Row</c>. They're
        /// removed from an element that moves into a different container.
        /// </summary>
        IReadOnlyList<string> OwnedAttributes { get; }

        /// <summary>
        /// Decides where an element dropped at <paramref name="point"/> lands.
        /// </summary>
        /// <param name="context">The container and the dragged element.</param>
        /// <param name="point">Where the dragged element's top-left corner would be, in the container's local space.</param>
        /// <returns>The target, or <see langword="null"/> when the container can't take the element.</returns>
        PlacementTarget? GetDropTarget(PlacementContext context, Vector2 point);

        /// <summary>
        /// Starts resizing an element of this container from <paramref name="handle"/>.
        /// </summary>
        /// <param name="context">The container and the element, as they were when the gesture started.</param>
        /// <param name="handle">The dragged handle.</param>
        /// <returns>The operation to update while the pointer moves.</returns>
        IResizeOperation BeginResize(PlacementContext context, ResizeHandle handle);
    }
}
