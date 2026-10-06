// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Margin and size arithmetic that follows how IcyUI arranges an element inside its slot.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><c>Left</c>/<c>Top</c>: positioned at the start margin.</description></item>
    /// <item><description><c>Right</c>/<c>Bottom</c>: positioned against the end margin.</description></item>
    /// <item><description><c>Center</c>, and <c>Stretch</c> with an explicit size: centered in the slack, then offset by the start margin; the end margin is ignored.</description></item>
    /// <item><description><c>Stretch</c> with no size: fills the slot between both margins.</description></item>
    /// </list>
    /// </remarks>
    public static class LayoutMath
    {
        /// <summary>
        /// Gets the margin that moves <paramref name="element"/> by (<paramref name="dx"/>, <paramref name="dy"/>) without
        /// changing its size.
        /// </summary>
        /// <param name="element">The element, for its alignment and explicit size.</param>
        /// <param name="start">The margin to move from.</param>
        /// <param name="dx">The horizontal offset.</param>
        /// <param name="dy">The vertical offset.</param>
        /// <returns>The new margin.</returns>
        public static Thickness Move(UIElement element, Thickness start, int dx, int dy)
        {
            ArgumentNullException.ThrowIfNull(element);
            (int left, int right) = MoveAxis(Kind(element.HorizontalAlignment, element.Width), start.Left, start.Right, dx);
            (int top, int bottom) = MoveAxis(Kind(element.VerticalAlignment, element.Height), start.Top, start.Bottom, dy);
            return new Thickness(left, top, right, bottom);
        }

        /// <summary>
        /// Resizes one axis of an element by dragging its start or end edge.
        /// </summary>
        /// <param name="kind">How the element is aligned on this axis.</param>
        /// <param name="startSize">The element's size on this axis when the gesture started.</param>
        /// <param name="startMarginStart">The start margin (left or top) when the gesture started.</param>
        /// <param name="startMarginEnd">The end margin (right or bottom) when the gesture started.</param>
        /// <param name="delta">How far the dragged edge moved, positive towards the end.</param>
        /// <param name="isStartEdge">Whether the start edge (left or top) is dragged.</param>
        /// <param name="anchored">
        /// <see langword="true"/> when the container anchors the element on this axis (a stack's stacking direction), so
        /// only the size changes, whatever the alignment.
        /// </param>
        /// <param name="sizeStretched">
        /// <see langword="true"/> when the container sizes its slot to the element on this axis (a stack's cross axis), so a
        /// <see cref="AxisKind.Stretched"/> element gets an explicit size instead of a margin: a margin can't grow a slot that
        /// follows the element.
        /// </param>
        /// <returns>The new size (<see langword="null"/> when the size isn't written) and margins.</returns>
        public static AxisResize ResizeAxis(AxisKind kind, int startSize, int startMarginStart, int startMarginEnd, int delta, bool isStartEdge, bool anchored, bool sizeStretched = false)
        {
            int size = Math.Max(1, isStartEdge ? startSize - delta : startSize + delta);
            int applied = isStartEdge ? startSize - size : size - startSize;

            // Checked before the stretched case: the slot follows the element here, so only a size can grow it.
            if (anchored || (kind == AxisKind.Stretched && sizeStretched))
                return new AxisResize(size, startMarginStart, startMarginEnd);

            if (kind == AxisKind.Stretched)
            {
                // A size would turn the element into a centered one; move the margin on the dragged side instead.
                return isStartEdge
                    ? new AxisResize(null, startMarginStart + delta, startMarginEnd)
                    : new AxisResize(null, startMarginStart, startMarginEnd - delta);
            }

            if (!isStartEdge)
            {
                // Keep the start edge where it was.
                return kind switch
                {
                    AxisKind.End => new AxisResize(size, startMarginStart, startMarginEnd - applied),
                    AxisKind.Centered => new AxisResize(size, startMarginStart + (applied / 2), startMarginEnd),
                    _ => new AxisResize(size, startMarginStart, startMarginEnd),
                };
            }

            // Keep the end edge where it was.
            return kind switch
            {
                AxisKind.Start => new AxisResize(size, startMarginStart + applied, startMarginEnd),
                AxisKind.Centered => new AxisResize(size, startMarginStart + (applied / 2), startMarginEnd),
                _ => new AxisResize(size, startMarginStart, startMarginEnd),
            };
        }

        /// <summary>
        /// Classifies an axis from its alignment and explicit size.
        /// </summary>
        /// <param name="alignment">The horizontal alignment.</param>
        /// <param name="size">The explicit width, or <see cref="float.NaN"/>.</param>
        /// <returns>The axis kind.</returns>
        public static AxisKind Kind(HorizontalAlignment alignment, float size) => alignment switch
        {
            HorizontalAlignment.Left => AxisKind.Start,
            HorizontalAlignment.Right => AxisKind.End,
            HorizontalAlignment.Stretch when float.IsNaN(size) => AxisKind.Stretched,
            _ => AxisKind.Centered,
        };

        /// <summary>
        /// Classifies an axis from its alignment and explicit size.
        /// </summary>
        /// <param name="alignment">The vertical alignment.</param>
        /// <param name="size">The explicit height, or <see cref="float.NaN"/>.</param>
        /// <returns>The axis kind.</returns>
        public static AxisKind Kind(VerticalAlignment alignment, float size) => alignment switch
        {
            VerticalAlignment.Top => AxisKind.Start,
            VerticalAlignment.Bottom => AxisKind.End,
            VerticalAlignment.Stretch when float.IsNaN(size) => AxisKind.Stretched,
            _ => AxisKind.Centered,
        };

        private static (int Start, int End) MoveAxis(AxisKind kind, int start, int end, int delta) => kind switch
        {
            AxisKind.End => (start, end - delta),
            AxisKind.Stretched => (start + delta, end - delta),
            _ => (start + delta, end),
        };
    }
}
