// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Design.Editor
{
    /// <summary>
    /// What the editor draws this frame, in surface units.
    /// </summary>
    /// <param name="Hover">The outline of the element a click would select, in Edit mode.</param>
    /// <param name="Selection">The selected element's outline.</param>
    /// <param name="Handles">The resize handles; empty in Interact mode and while blocked.</param>
    /// <param name="Dimmed">Whether the selection is drawn dimmed (Interact mode).</param>
    /// <param name="Indicator">The drop target's insertion line or area while moving.</param>
    /// <param name="IndicatorIsLine">Whether <paramref name="Indicator"/> is a line.</param>
    /// <param name="Ghost">The dragged element's outline at the pointer while moving.</param>
    internal sealed record AdornerScene(
        RectangleF? Hover,
        RectangleF? Selection,
        IReadOnlyList<RectangleF> Handles,
        bool Dimmed,
        RectangleF? Indicator,
        bool IndicatorIsLine,
        RectangleF? Ghost);
}
