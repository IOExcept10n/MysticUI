// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// One resize gesture in progress. It remembers the element's state when the gesture started, so every update is
    /// computed from that state and rounding never accumulates.
    /// </summary>
    public interface IResizeOperation
    {
        /// <summary>
        /// Gets the area the element will occupy in its container after the last <see cref="Update(Vector2)"/>, in the
        /// container's local space, for the editor to outline; or <see langword="null"/> when the strategy has nothing
        /// beyond the element's own bounds to show.
        /// </summary>
        /// <remarks>The default implementation returns <see langword="null"/>.</remarks>
        RectangleF? Indicator => null;

        /// <summary>
        /// Computes the attributes for the handle dragged by <paramref name="delta"/> since the gesture started.
        /// </summary>
        /// <param name="delta">The pointer's offset from where the gesture started, in the container's local units.</param>
        /// <returns>The attribute values the element should have now.</returns>
        IReadOnlyList<AttributeEdit> Update(Vector2 delta);

        /// <summary>
        /// Computes the attributes for the handle dragged by <paramref name="delta"/> since the gesture started, with
        /// snapping turned on or off.
        /// </summary>
        /// <param name="delta">The pointer's offset from where the gesture started, in the container's local units.</param>
        /// <param name="snap">
        /// <see langword="true"/> to let the strategy snap the dragged edge to its guides (such as grid lines);
        /// <see langword="false"/> to follow the pointer exactly. The editor clears it while <see langword="Alt"/> is held.
        /// </param>
        /// <returns>The attribute values the element should have now.</returns>
        /// <remarks>The default implementation ignores <paramref name="snap"/> and calls <see cref="Update(Vector2)"/>.</remarks>
        IReadOnlyList<AttributeEdit> Update(Vector2 delta, bool snap) => Update(delta);
    }
}
