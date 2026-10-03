// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Describes one step of a drag.
    /// </summary>
    /// <param name="Kind">The pointer that drags.</param>
    /// <param name="Start">The position where the pointer was pressed, in physical pixels.</param>
    /// <param name="Position">The current pointer position, in physical pixels.</param>
    /// <param name="Delta">The movement since the previous drag event, in physical pixels.</param>
    /// <param name="Velocity">
    /// The release velocity in physical pixels per second - set on <see cref="IGestureEvents.DragCompleted"/> only,
    /// otherwise <see cref="Vector2.Zero"/>.
    /// </param>
    public readonly record struct DragInfo(PointerKind Kind, Point Start, Point Position, Vector2 Delta, Vector2 Velocity);
}
