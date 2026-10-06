// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Places elements in a <see cref="StackPanel"/>: a drop lands in the nearest gap between children, and a resize along
    /// the stacking direction changes only the size, since a stack anchors its children there.
    /// </summary>
    public sealed class StackPanelPlacement : MarginPlacement
    {
        /// <inheritdoc/>
        public override PlacementTarget? GetDropTarget(PlacementContext context, Vector2 point)
        {
            ArgumentNullException.ThrowIfNull(context);
            bool vertical = ((StackPanel)context.Container).Orientation == Orientation.Vertical;
            float along = vertical ? point.Y : point.X;

            int index = context.ContentCount;
            float lineAt = float.NaN;
            foreach (PlacementChild child in context.Children)
            {
                Rectangle bounds = PlacementContext.ToLocal(context.Container, child.Instance.ActualBounds);
                float start = vertical ? bounds.Top : bounds.Left;
                float middle = vertical ? bounds.Top + (bounds.Height / 2f) : bounds.Left + (bounds.Width / 2f);
                if (along < middle)
                {
                    index = child.Index;
                    lineAt = start;
                    break;
                }

                lineAt = vertical ? bounds.Bottom : bounds.Right;
            }

            if (float.IsNaN(lineAt))
                lineAt = vertical ? context.ContainerContent.Top : context.ContainerContent.Left;

            Rectangle content = context.ContainerContent;
            RectangleF line = vertical
                ? new RectangleF(content.Left, lineAt, content.Width, 0)
                : new RectangleF(lineAt, content.Top, 0, content.Height);
            return new PlacementTarget(index, [], line, IndicatorIsLine: true);
        }

        /// <inheritdoc/>
        public override IResizeOperation BeginResize(PlacementContext context, ResizeHandle handle)
        {
            ArgumentNullException.ThrowIfNull(context);
            bool vertical = ((StackPanel)context.Container).Orientation == Orientation.Vertical;
            return new Resize(context, handle, anchorHorizontal: !vertical, anchorVertical: vertical);
        }
    }
}
