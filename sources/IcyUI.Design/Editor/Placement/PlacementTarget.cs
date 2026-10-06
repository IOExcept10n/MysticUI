// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Where a dropped element lands in a container.
    /// </summary>
    /// <param name="Index">The content index to move the element to, as <see cref="MarkupEditor.MoveElement"/> counts it.</param>
    /// <param name="Edits">The attributes to write or remove on the element with the move.</param>
    /// <param name="Indicator">The area to highlight while dragging, in the container's local space.</param>
    /// <param name="IndicatorIsLine">
    /// <see langword="true"/> when <paramref name="Indicator"/> is an insertion line (a zero-width or zero-height
    /// rectangle) rather than an area.
    /// </param>
    public sealed record PlacementTarget(int Index, IReadOnlyList<AttributeEdit> Edits, RectangleF Indicator, bool IndicatorIsLine);
}
