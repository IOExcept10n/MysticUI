// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Linq;
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
        private int ColumnsPerRow => Math.Max(1, (int)(ContentBounds.Width / ItemWidth));

        /// <inheritdoc/>
        protected override float ComputeExtentHeight() =>
            (float)Math.Ceiling(ItemCount / (float)ColumnsPerRow) * ItemHeight;

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
        protected override void RealizeRange(int firstIndex, float firstOffset)
        {
            const float ScrollAheadBuffer = 100f;
            int columnsPerRow = ColumnsPerRow;
            float rangeEnd = verticalOffset + viewportHeight + ScrollAheadBuffer;
            int lastRow = (int)(rangeEnd / ItemHeight);
            int lastIndex = Math.Min(ItemCount - 1, ((lastRow + 1) * columnsPerRow) - 1);

            var stillRealized = new HashSet<int>();
            for (int index = firstIndex; index <= lastIndex; index++)
            {
                EnsureRealized(index);
                stillRealized.Add(index);

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

            foreach (int realizedIndex in realizedContainers.Keys.ToList())
            {
                if (!stillRealized.Contains(realizedIndex))
                    Derealize(realizedIndex);
            }
        }
    }
}
