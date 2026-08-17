// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// ColumnDefinition/RowDefinition are a tightly-coupled pair (mirrors each other exactly, differing only in axis) -
// kept in one file rather than split, same precedent as VisualState/VisualStateGroup in UI/Styles/VisualState.cs.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

namespace Icy.UI.Controls
{
    /// <summary>
    /// Defines the sizing behavior of a single <see cref="Grid"/> column.
    /// </summary>
    public class ColumnDefinition
    {
        /// <summary>
        /// Gets or sets the column's sizing behavior.
        /// </summary>
        public GridLength Width { get; set; } = GridLength.Star;

        /// <summary>
        /// Gets the column's actual resolved width in pixels, as of the last <see cref="Grid"/> arrange pass.
        /// </summary>
        public float ActualWidth { get; internal set; }
    }

    /// <summary>
    /// Defines the sizing behavior of a single <see cref="Grid"/> row.
    /// </summary>
    public class RowDefinition
    {
        /// <summary>
        /// Gets or sets the row's sizing behavior.
        /// </summary>
        public GridLength Height { get; set; } = GridLength.Star;

        /// <summary>
        /// Gets the row's actual resolved height in pixels, as of the last <see cref="Grid"/> arrange pass.
        /// </summary>
        public float ActualHeight { get; internal set; }
    }
}
