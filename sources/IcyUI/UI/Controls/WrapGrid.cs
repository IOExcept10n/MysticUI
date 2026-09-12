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
        private int ColumnsPerRow => Math.Max(1, (int)(ContentBounds.Width / ItemWidth));
    }
}
