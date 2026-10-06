// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// The fallback placement: an element moves by its margin, alignment-aware, and resizes by its size (or, on a
    /// stretched axis, by the margin on the dragged side). Used for any container without a registered strategy.
    /// </summary>
    /// <remarks>
    /// A container whose markup content is a single slot (a <c>Border</c>'s child, a <c>ContentControl</c>'s content)
    /// accepts a dropped element only when the slot is empty.
    /// </remarks>
    public class MarginPlacement : IPlacementStrategy
    {
        /// <inheritdoc/>
        public virtual IReadOnlyList<string> OwnedAttributes => [];

        /// <inheritdoc/>
        public virtual PlacementTarget? GetDropTarget(PlacementContext context, Vector2 point)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (!AcceptsDrop(context))
                return null;

            int dx = (int)MathF.Round(point.X) - context.ElementBounds.X;
            int dy = (int)MathF.Round(point.Y) - context.ElementBounds.Y;
            var area = new RectangleF(point.X, point.Y, context.ElementBounds.Width, context.ElementBounds.Height);
            if (!context.IsCurrentContainer)
            {
                // From another container: keep the margin, append as the last child.
                return new PlacementTarget(context.ContentCount, [], context.ContainerContent, IndicatorIsLine: false);
            }

            Thickness margin = LayoutMath.Move(context.Element, context.Element.Margin, dx, dy);
            return new PlacementTarget(CurrentIndex(context), [new AttributeEdit("Margin", MarkupValues.Format(margin))], area, IndicatorIsLine: false);
        }

        /// <inheritdoc/>
        public virtual IResizeOperation BeginResize(PlacementContext context, ResizeHandle handle)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new Resize(context, handle, anchorHorizontal: false, anchorVertical: false);
        }

        /// <summary>
        /// Gets the element's current content index, so a move inside the same container keeps its order.
        /// </summary>
        /// <param name="context">The container and the element.</param>
        /// <returns>The index <see cref="MarkupEditor.MoveElement"/> would leave unchanged.</returns>
        protected static int CurrentIndex(PlacementContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.ElementIndex >= 0)
                return Math.Min(context.ElementIndex, context.ContentCount);

            int index = 0;
            foreach (PlacementChild child in context.Children)
            {
                if (child.Instance.ActualBounds.Y > context.Element.ActualBounds.Y || (child.Instance.ActualBounds.Y == context.Element.ActualBounds.Y && child.Instance.ActualBounds.X > context.Element.ActualBounds.X))
                    break;
                index = child.Index + 1;
            }

            return Math.Min(index, context.ContentCount);
        }

        /// <summary>
        /// Decides whether the container can take a dropped element at all.
        /// </summary>
        /// <param name="context">The container and the dragged element.</param>
        /// <returns>
        /// <see langword="true"/> for list containers and for an empty single slot; <see langword="false"/> for a filled
        /// single slot that doesn't already hold the element.
        /// </returns>
        protected virtual bool AcceptsDrop(PlacementContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            bool singleSlot = IsSingleSlot(context.Container);
            return !singleSlot || context.IsCurrentContainer || context.ContentCount == 0;
        }

        /// <summary>
        /// Decides from the markup content property whether a container holds one child rather than a list.
        /// </summary>
        private static bool IsSingleSlot(UIElement container) =>
            Markup.ContentPropertyAttribute.GetContentPropertyName(container.GetType()) is { } name
            && container.GetType().GetProperty(name) is { } property
            && property.GetValue(container) is not System.Collections.IList;

        /// <summary>
        /// The size-or-margin resize every strategy builds on.
        /// </summary>
        protected internal sealed class Resize(PlacementContext context, ResizeHandle handle, bool anchorHorizontal, bool anchorVertical) : IResizeOperation
        {
            private readonly Rectangle bounds = context.ElementBounds;
            private readonly Thickness margin = context.Element.Margin;
            private readonly AxisKind horizontal = LayoutMath.Kind(context.Element.HorizontalAlignment, context.Element.Width);
            private readonly AxisKind vertical = LayoutMath.Kind(context.Element.VerticalAlignment, context.Element.Height);

            /// <inheritdoc/>
            public IReadOnlyList<AttributeEdit> Update(Vector2 delta)
            {
                var edits = new List<AttributeEdit>(3);
                int left = margin.Left, top = margin.Top, right = margin.Right, bottom = margin.Bottom;
                if ((handle & (ResizeHandle.Left | ResizeHandle.Right)) != 0)
                {
                    AxisResize x = LayoutMath.ResizeAxis(horizontal, bounds.Width, margin.Left, margin.Right, (int)MathF.Round(delta.X), (handle & ResizeHandle.Left) != 0, anchorHorizontal);
                    if (x.Size is int width)
                        edits.Add(new AttributeEdit("Width", MarkupValues.Format(width)));
                    (left, right) = (x.MarginStart, x.MarginEnd);
                }

                if ((handle & (ResizeHandle.Top | ResizeHandle.Bottom)) != 0)
                {
                    AxisResize y = LayoutMath.ResizeAxis(vertical, bounds.Height, margin.Top, margin.Bottom, (int)MathF.Round(delta.Y), (handle & ResizeHandle.Top) != 0, anchorVertical);
                    if (y.Size is int height)
                        edits.Add(new AttributeEdit("Height", MarkupValues.Format(height)));
                    (top, bottom) = (y.MarginStart, y.MarginEnd);
                }

                var next = new Thickness(left, top, right, bottom);
                if (next != margin)
                    edits.Add(new AttributeEdit("Margin", MarkupValues.Format(next)));
                return edits;
            }
        }
    }
}
