// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
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
        /// Computes the attributes for the handle dragged by <paramref name="delta"/> since the gesture started.
        /// </summary>
        /// <param name="delta">The pointer's offset from where the gesture started, in the container's local units.</param>
        /// <returns>The attribute values the element should have now.</returns>
        IReadOnlyList<AttributeEdit> Update(Vector2 delta);
    }
}
