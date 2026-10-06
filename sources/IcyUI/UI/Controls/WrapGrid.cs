// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using CommunityToolkit.Diagnostics;
using Icy.Data.Markup.Attributes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// An auto-flowing, virtualizing grid of uniformly-sized cells - realized items fill left-to-right and wrap
    /// to the next row, e.g. an icon/inventory grid.
    /// </summary>
    /// <remarks>
    /// Overrides <see cref="ItemsControl"/>'s virtualization geometry (<see cref="LocateViewportStart"/>/
    /// <see cref="RealizeRange(int, float)"/>/<see cref="ComputeExtentHeight"/>) with exact row/column
    /// arithmetic instead of the base's single-column variable-height estimation - every cell is a fixed,
    /// known-upfront <see cref="ItemWidth"/>×<see cref="ItemHeight"/> size, so no height cache or anchor-walk is
    /// needed. Pooling, <see cref="ItemsControl.CreateContainer(Styles.DataTemplate, object)"/> (realizes
    /// <see cref="SelectorItem"/>s, inherited via <see cref="SelectingItemsControl"/>), and
    /// <see cref="ItemsControl.ItemsSource"/>'s live <see cref="System.Collections.Specialized.INotifyCollectionChanged"/>
    /// reactivity are all inherited unchanged.
    /// </remarks>
    public class WrapGrid : SelectingItemsControl
    {
        private const float DefaultItemSize = 64f;

        private float itemHeight = DefaultItemSize;
        private float itemWidth = DefaultItemSize;

        /// <summary>
        /// Gets or sets the width, in pixels, of every realized cell.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not greater than zero.</exception>
        [Category("Layout")]
        [DefaultValue(DefaultItemSize)]
        [RegisterReference]
        public float ItemWidth
        {
            get => itemWidth;
            set
            {
                Guard.IsGreaterThan(value, 0f);
                if (SetProperty(ref itemWidth, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the height, in pixels, of every realized cell.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not greater than zero.</exception>
        [Category("Layout")]
        [DefaultValue(DefaultItemSize)]
        [RegisterReference]
        public float ItemHeight
        {
            get => itemHeight;
            set
            {
                Guard.IsGreaterThan(value, 0f);
                if (SetProperty(ref itemHeight, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets how many cells fit per row at the control's current arranged width - at least <c>1</c>, even if
        /// <see cref="ItemWidth"/> exceeds the available width.
        /// </summary>
        /// <remarks>
        /// Adds a small epsilon before truncating: a width/<see cref="ItemWidth"/> ratio intended to land on an
        /// exact integer (e.g. <c>87 / 5.8</c>) can otherwise come out a hair below it purely from float division
        /// rounding, silently under-counting by one column.
        /// </remarks>
        private int ColumnsPerRow => Math.Max(1, (int)((ContentBounds.Width / ItemWidth) + 0.0001f));

        /// <inheritdoc/>
        /// <remarks>
        /// Returns <c>0</c> before this control's first <see cref="UIElement.Arrange(System.Drawing.Rectangle)"/> -
        /// <see cref="Control.ContentBounds"/> is still empty then, and reporting <see cref="ColumnsPerRow"/>'s
        /// single-column fallback as a real extent would wildly overstate it (e.g. ~5x too tall for a 5-column
        /// grid) until the next layout pass corrects it.
        /// </remarks>
        protected override float ComputeExtentHeight() =>
            ContentBounds.Width <= 0 ? 0f : (float)Math.Ceiling(ItemCount / (float)ColumnsPerRow) * ItemHeight;

        /// <inheritdoc/>
        protected override (int Index, float Offset) LocateViewportStart()
        {
            if (ItemCount == 0)
                return (0, 0f);

            int row = (int)(verticalOffset / ItemHeight);
            int index = Math.Clamp(row * ColumnsPerRow, 0, ItemCount - 1);
            return (index, row * ItemHeight);
        }

        /// <inheritdoc/>
        /// <remarks>Rows are uniform: the item's row times <see cref="ItemHeight"/>.</remarks>
        protected override (float Top, float Height) GetItemExtent(int index) => (index / ColumnsPerRow * ItemHeight, ItemHeight);

        /// <inheritdoc/>
        /// <remarks>
        /// <paramref name="firstOffset"/> is intentionally unused - unlike the base's variable-height walk, this
        /// fixed-cell layout always re-derives the same value from <paramref name="firstIndex"/>/
        /// <see cref="ItemHeight"/>/<c>ColumnsPerRow</c> directly (see the per-item <c>row</c> computation below),
        /// so passing it through could only ever disagree with that computation, never usefully replace it.
        /// </remarks>
        protected override void RealizeRange(int firstIndex, float firstOffset)
        {
            int columnsPerRow = ColumnsPerRow;
            float rangeEnd = verticalOffset + viewportHeight + ScrollAheadBuffer;
            int lastRow = Math.Max(0, (int)(rangeEnd / ItemHeight));
            int lastIndex = Math.Min(ItemCount - 1, ((lastRow + 1) * columnsPerRow) - 1);

            for (int index = firstIndex; index <= lastIndex; index++)
            {
                EnsureRealized(index);

                int row = index / columnsPerRow;
                int column = index % columnsPerRow;
                var targetRect = new Rectangle(
                    ContentBounds.X + (int)(column * ItemWidth),
                    ContentBounds.Y + (int)((row * ItemHeight) - verticalOffset),
                    (int)ItemWidth,
                    (int)ItemHeight);

                ItemContainer container = realizedContainers[index];
                container.InvalidateArrange();
                container.Arrange(targetRect);
            }

            DerealizeOutOfRange(index => index >= firstIndex && index <= lastIndex);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// No-op: this control's virtualization geometry (<see cref="ComputeExtentHeight"/>/
        /// <see cref="LocateViewportStart"/>/<see cref="RealizeRange(int, float)"/>) never consults the base's
        /// measured-height cache or its above-viewport anchor correction - every cell uses a fixed, known-upfront
        /// <see cref="ItemWidth"/>x<see cref="ItemHeight"/> size instead. Folding a container's incidental measured
        /// content height in here would misapply that correction to unrelated fixed-size geometry, causing spurious
        /// scroll-position drift (see <see cref="ItemsControl.VerticalOffsetCorrectionRequested"/>).
        /// </remarks>
        protected override void RecordRealizedHeight(int index, float height)
        {
        }
    }
}
