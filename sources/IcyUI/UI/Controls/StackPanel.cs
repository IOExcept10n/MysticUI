// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Arranges its <see cref="Panel.Children"/> in a single line, sequentially along <see cref="Orientation"/>.
    /// </summary>
    /// <remarks>
    /// Cross-axis placement (e.g. horizontal position within a <see cref="Orientation.Vertical"/> stack) is left to
    /// each child's own <see cref="UIElement.HorizontalAlignment"/>/<see cref="UIElement.VerticalAlignment"/> and
    /// <see cref="UIElement.Margin"/>, resolved against the full cross-axis extent of <see cref="Panel.ContentBounds"/>.
    /// </remarks>
    public class StackPanel : Panel
    {
        private Orientation orientation = Orientation.Vertical;

        /// <summary>
        /// Gets or sets the axis children are stacked along.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(Orientation.Vertical)]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public Orientation Orientation
        {
            get => orientation;
            set
            {
                if (SetProperty(ref orientation, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            Rectangle content = ContentBounds;
            int offset = 0;
            foreach (UIElement child in Children)
            {
                if (!child.IsVisible)
                    continue;

                Size desired = child.Measure();
                Rectangle slot = Orientation == Orientation.Vertical
                    ? new Rectangle(content.X, content.Y + offset, content.Width, desired.Height + child.Margin.Height)
                    : new Rectangle(content.X + offset, content.Y, desired.Width + child.Margin.Width, content.Height);

                offset += Orientation == Orientation.Vertical ? slot.Height : slot.Width;

                // The child may not have been independently invalidated even though its assigned slot just moved
                // (e.g. a sibling before it changed size) - force it to re-arrange into the new slot regardless.
                child.InvalidateArrange();
                child.Arrange(slot);
            }
        }

        /// <inheritdoc/>
        protected override Size MeasureContent()
        {
            int totalMain = 0;
            int maxCross = 0;
            foreach (UIElement child in Children)
            {
                if (!child.IsVisible)
                    continue;

                Size desired = child.Measure();
                if (Orientation == Orientation.Vertical)
                {
                    totalMain += desired.Height;
                    maxCross = Math.Max(maxCross, desired.Width);
                }
                else
                {
                    totalMain += desired.Width;
                    maxCross = Math.Max(maxCross, desired.Height);
                }
            }

            return Orientation == Orientation.Vertical ? new Size(maxCross, totalMain) : new Size(totalMain, maxCross);
        }
    }
}
