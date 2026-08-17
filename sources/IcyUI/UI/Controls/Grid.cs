// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.Drawing;
using Icy.Data.Markup;
using Icy.Data.Markup.Attributes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Arranges its <see cref="Panel.Children"/> into a grid of rows and columns defined by
    /// <see cref="RowDefinitions"/>/<see cref="ColumnDefinitions"/>, placed via the <c>Row</c>/<c>Column</c>
    /// attached properties (<see cref="GetRow"/>/<see cref="SetRow"/>, <see cref="GetColumn"/>/<see cref="SetColumn"/>).
    /// </summary>
    /// <remarks>
    /// A child with no explicit <c>Row</c>/<c>Column</c> defaults to cell (0, 0). A child whose assigned row/column
    /// index is out of range is clamped into the nearest valid track. Row/column spanning isn't supported in v1 -
    /// each child occupies exactly one cell.
    /// </remarks>
    [AttachedProperty(nameof(GetRow), nameof(SetRow), PropertyName = "Row")]
    [AttachedProperty(nameof(GetColumn), nameof(SetColumn), PropertyName = "Column")]
    public class Grid : Panel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Grid"/> class.
        /// </summary>
        public Grid()
        {
            RowDefinitions.CollectionChanged += (_, _) => InvalidateMeasure();
            ColumnDefinitions.CollectionChanged += (_, _) => InvalidateMeasure();
        }

        /// <summary>
        /// Gets the grid's column definitions. An empty collection behaves as a single implicit
        /// <see cref="GridLength.Star"/> column.
        /// </summary>
        public ObservableCollection<ColumnDefinition> ColumnDefinitions { get; } = [];

        /// <summary>
        /// Gets the grid's row definitions. An empty collection behaves as a single implicit
        /// <see cref="GridLength.Star"/> row.
        /// </summary>
        public ObservableCollection<RowDefinition> RowDefinitions { get; } = [];

        /// <summary>
        /// Gets the zero-based column <paramref name="element"/> is placed in within its parent <see cref="Grid"/>.
        /// </summary>
        /// <param name="element">The element to get the attached value for.</param>
        /// <returns>The element's assigned column index. Defaults to <c>0</c>.</returns>
        public static int GetColumn(UIElement element) => AttachedProperties.GetValue<int>(element, "Column");

        /// <summary>
        /// Gets the zero-based row <paramref name="element"/> is placed in within its parent <see cref="Grid"/>.
        /// </summary>
        /// <param name="element">The element to get the attached value for.</param>
        /// <returns>The element's assigned row index. Defaults to <c>0</c>.</returns>
        public static int GetRow(UIElement element) => AttachedProperties.GetValue<int>(element, "Row");

        /// <summary>
        /// Sets the zero-based column <paramref name="element"/> should be placed in within its parent <see cref="Grid"/>.
        /// </summary>
        /// <param name="element">The element to set the attached value for.</param>
        /// <param name="value">The column index to assign.</param>
        public static void SetColumn(UIElement element, int value)
        {
            AttachedProperties.SetValue(element, "Column", value);
            (element.Parent as Grid)?.InvalidateMeasure();
        }

        /// <summary>
        /// Sets the zero-based row <paramref name="element"/> should be placed in within its parent <see cref="Grid"/>.
        /// </summary>
        /// <param name="element">The element to set the attached value for.</param>
        /// <param name="value">The row index to assign.</param>
        public static void SetRow(UIElement element, int value)
        {
            AttachedProperties.SetValue(element, "Row", value);
            (element.Parent as Grid)?.InvalidateMeasure();
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            Rectangle content = ContentBounds;
            float[] colSizes = ResolveTracks(ColumnDefinitions.Count, i => ColumnDefinitions[i].Width, isColumn: true, content.Width);
            float[] rowSizes = ResolveTracks(RowDefinitions.Count, i => RowDefinitions[i].Height, isColumn: false, content.Height);

            for (int i = 0; i < ColumnDefinitions.Count; i++)
                ColumnDefinitions[i].ActualWidth = colSizes[i];
            for (int i = 0; i < RowDefinitions.Count; i++)
                RowDefinitions[i].ActualHeight = rowSizes[i];

            float[] colOffsets = CumulativeOffsets(colSizes);
            float[] rowOffsets = CumulativeOffsets(rowSizes);

            foreach (UIElement child in Children)
            {
                if (!child.IsVisible)
                    continue;

                int column = ClampedTrack(GetColumn(child), ColumnDefinitions.Count);
                int row = ClampedTrack(GetRow(child), RowDefinitions.Count);
                Rectangle cell = new(
                    content.X + (int)colOffsets[column],
                    content.Y + (int)rowOffsets[row],
                    (int)colSizes[column],
                    (int)rowSizes[row]);

                // The child's assigned cell may have moved/resized even if nothing about the child itself changed
                // (e.g. a Star column elsewhere grew) - force it to re-arrange into the new cell regardless.
                child.InvalidateArrange();
                child.Arrange(cell);
            }
        }

        /// <inheritdoc/>
        protected override Size MeasureContent()
        {
            // No incoming size constraint is available at measure time (see UIElement.MeasureContent), so Star
            // tracks contribute nothing to the grid's natural size here - only resolved once ArrangeContent knows
            // the actual available space, matching how Star sizing works in WPF's Grid.
            float[] colSizes = ResolveTracks(ColumnDefinitions.Count, i => ColumnDefinitions[i].Width, isColumn: true, availableSpace: null);
            float[] rowSizes = ResolveTracks(RowDefinitions.Count, i => RowDefinitions[i].Height, isColumn: false, availableSpace: null);
            return new Size((int)colSizes.Sum(), (int)rowSizes.Sum());
        }

        private static float[] CumulativeOffsets(float[] sizes)
        {
            float[] offsets = new float[sizes.Length];
            float running = 0;
            for (int i = 0; i < sizes.Length; i++)
            {
                offsets[i] = running;
                running += sizes[i];
            }

            return offsets;
        }

        private static int ClampedTrack(int index, int definitionCount) =>
            Math.Clamp(index, 0, Math.Max(definitionCount - 1, 0));

        private float MaxChildTrackSize(int trackIndex, bool isColumn)
        {
            float max = 0;
            foreach (UIElement child in Children)
            {
                if (!child.IsVisible)
                    continue;

                int index = isColumn
                    ? ClampedTrack(GetColumn(child), ColumnDefinitions.Count)
                    : ClampedTrack(GetRow(child), RowDefinitions.Count);
                if (index != trackIndex)
                    continue;

                Size desired = child.Measure();
                float size = isColumn ? desired.Width + child.Margin.Width : desired.Height + child.Margin.Height;
                max = Math.Max(max, size);
            }

            return max;
        }

        private float[] ResolveTracks(int definitionCount, Func<int, GridLength> lengths, bool isColumn, float? availableSpace)
        {
            int trackCount = Math.Max(definitionCount, 1);
            GridLength TrackLength(int i) => definitionCount > 0 ? lengths(i) : GridLength.Star;

            float[] sizes = new float[trackCount];
            for (int i = 0; i < trackCount; i++)
            {
                GridLength length = TrackLength(i);
                sizes[i] = length.UnitType switch
                {
                    GridUnitType.Pixel => Math.Max(length.Value, 0),
                    GridUnitType.Auto => MaxChildTrackSize(i, isColumn),
                    _ => 0, // Star, resolved below once availableSpace is known.
                };
            }

            if (availableSpace is float available)
            {
                float used = 0;
                float starWeightTotal = 0;
                for (int i = 0; i < trackCount; i++)
                {
                    GridLength length = TrackLength(i);
                    if (length.UnitType == GridUnitType.Star)
                        starWeightTotal += Math.Max(length.Value, 0);
                    else
                        used += sizes[i];
                }

                float remaining = Math.Max(0, available - used);
                for (int i = 0; i < trackCount; i++)
                {
                    GridLength length = TrackLength(i);
                    if (length.UnitType == GridUnitType.Star)
                        sizes[i] = starWeightTotal > 0 ? remaining * (Math.Max(length.Value, 0) / starWeightTotal) : 0;
                }
            }

            return sizes;
        }
    }
}
