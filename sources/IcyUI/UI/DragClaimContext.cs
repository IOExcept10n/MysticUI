// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Input.Gestures;

namespace Icy.UI
{
    /// <summary>
    /// Describes a starting drag to an element deciding whether to claim it (see <see cref="UIElement.GetDragAxes(in DragClaimContext)"/>).
    /// </summary>
    /// <param name="Kind">The pointer that drags.</param>
    /// <param name="ScreenStart">The press position, in physical pixels - lets an element claim only part of itself.</param>
    /// <param name="LocalDirection">The movement that crossed the drag threshold, mapped into the element's own space.</param>
    /// <param name="StartedFromHold">Whether the drag grew out of a press-and-hold.</param>
    public readonly record struct DragClaimContext(PointerKind Kind, Point ScreenStart, Vector2 LocalDirection, bool StartedFromHold);
}
